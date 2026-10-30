using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

static class Program
{
    static int checks;
    static void Assert(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    static EntityVehicle Truck(World w) => new EntityVehicle { world = w };
    static void Main()
    {
        var rows = (List<RebirthPurgePlayerWrapAssignments.Assignment>)typeof(RebirthPurgePlayerWrapAssignments)
            .GetField("Rows", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Assert(rows.Count == 8, "Eight owner-approved production assignments");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("EOS_0002bb72bdeb454295e656063b9d8838").Single() == "modVehicleWrapHeyWeps", "Owner exact HeyWeps assignment");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("EOS_00024e82683f45e09594ae9d6f6893cc").Single() == "modVehicleWrapFuriousRamsay", "Owner's exact Furious assignment");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("EOS_00029b75a39747cb85a3278a0fb31ac3").Single() == "modVehicleWrapCaptStress", "CaptStress assignment");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("EOS_00025e46dead4c0f90abddffa76bc8d1").Single() == "modVehicleWrapPurpleLlama", "Purple Llama assignment");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("EOS_0002be0688df4e849ff8dd9ac2025cca").SequenceEqual(new[] { "modVehicleWrapUndeadChill", "modVehicleWrapUndeadChillAfterHours" }), "Both Undead Chill assignments");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("EOS_00026266d8b842f981e60ea4640fdc8f").Single() == "modVehicleWrapGenmaSaotome", "Genma assignment");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("EOS_0002d44f5ff04025b6661810fd559abe").Single() == "modVehicleWrapMissBanjo", "MissBanjo assignment");
        rows.Clear(); // Fixture process only: isolate the general routing cases below.
        rows.Add(new RebirthPurgePlayerWrapAssignments.Assignment("EOS_test", "wrapA"));
        rows.Add(new RebirthPurgePlayerWrapAssignments.Assignment("EOS_test", "wrapB"));
        rows.Add(new RebirthPurgePlayerWrapAssignments.Assignment("EOS_test", "wrapC"));
        rows.Add(new RebirthPurgePlayerWrapAssignments.Assignment("EOS_test", "wrapA"));
        rows.Add(new RebirthPurgePlayerWrapAssignments.Assignment("EOS_other", "wrapOther"));
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("eos_TEST").Length == 3, "Identity and dedup");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer("Steam_test").Length == 0, "EOS table only");
        Assert(RebirthPurgePlayerWrapAssignments.ForPlayer(null).Length == 0, "Null ID");
        var choices = new HashSet<string>();
        for (int seed = 0; seed < 100; seed++)
        {
            var plan = RebirthPurgePlayerWrapAssignments.Select(new[] { "wrapA", "wrapB", "bad" }, n => n != "bad", new Random(seed));
            Assert(plan.Length == 2 && plan.Distinct().Count() == 2 && !plan.Contains("bad"), "Valid plan partition");
            choices.Add(plan[0]);
        }
        Assert(choices.Count == 2, "Random can choose either wrap");
        Assert(RebirthPurgePlayerWrapAssignments.Select(new[] { "bad" }, n => false, new Random(1)).Length == 0, "All missing no plan");
        Assert(RebirthPurgePlayerWrapAssignments.Select(new[] { "wrapA" }, n => true, new Random(1)).Single() == "wrapA", "Single selects only wrap");
        var world = new World(); GameManager.Instance = new GameManager { World = world };
        var player = world.Player;
        var truck = Truck(world);
        RebirthPurgePlayerWraps.Prepare(truck, "EOS_unassigned");
        Assert(truck.vehicle.Item.Cosmetic == "dye", "Unassigned retains dye");
        RebirthPurgeTruckStart.Enabled = false;
        RebirthPurgePlayerWraps.Prepare(truck, "EOS_test");
        Assert(truck.vehicle.Item.Cosmetic == "dye", "Non-Purge no equip");
        RebirthPurgeTruckStart.Enabled = true;
        world.Remote = true;
        RebirthPurgePlayerWraps.Prepare(truck, "EOS_test");
        Assert(truck.vehicle.Item.Cosmetic == "dye", "Remote cannot choose");
        world.Remote = false;
        RebirthPurgePlayerWraps.Prepare(truck, "EOS_test");
        string chosen = truck.vehicle.Item.Cosmetic;
        Assert(new[] { "wrapA", "wrapB", "wrapC" }.Contains(chosen), "Valid equip");
        Assert(truck.vehicle.Item.Metadata["RebirthPurgeStarterWrapItems"].Split(';')[0] == chosen, "Selection saved");
        RebirthPurgePlayerWraps.Prepare(truck, "EOS_other");
        Assert(truck.vehicle.Item.Cosmetic == chosen, "Prepared choice cannot reroll");
        truck.vehicle.Item.SetMetadata("RebirthPurgeStarterOwner", "EOS_other"); player.AttachedToEntity = truck;
        RebirthPurgePlayerWraps.Tick(true);
        Assert(player.bag.Delivered.Count == 0, "Wrong owner no grant");
        truck.vehicle.Item.SetMetadata("RebirthPurgeStarterOwner", "EOS_test");
        player.bag.Space = false;
        RebirthPurgePlayerWraps.Tick(true);
        Assert(player.bag.Delivered.Count == 0 && GameManager.Notices == 1, "Full bag pending notice");
        player.AttachedToEntity = null; player.bag.Space = true;
        RebirthPurgePlayerWraps.Tick(true);
        Assert(player.bag.Delivered.Count == 2 && !player.bag.Delivered.Contains(chosen), "Pending delivered after dismount");
        RebirthPurgePlayerWraps.Tick(true);
        Assert(player.bag.Delivered.Count == 2, "No repeat delivery");
        // Cold service state, same native-saved CVars: no regrant after reload.
        GameManager.Instance.World = new World { Player = player };
        RebirthPurgePlayerWraps.Tick(true);
        Assert(player.bag.Delivered.Count == 2, "Saved receipt suppresses replay");
        player = new EntityPlayerLocal(); GameManager.Instance.World.Player = player;
        player.AttachedToEntity = truck; player.bag.ThrowAfterInsert = true;
        RebirthPurgePlayerWraps.Tick(true);
        int moved = player.bag.Delivered.Count;
        RebirthPurgePlayerWraps.Tick(true);
        Assert(moved == 2 && player.bag.Delivered.Count == moved, "Post-mutation throw cannot duplicate");
        player = new EntityPlayerLocal(); GameManager.Instance.World.Player = player;
        player.AttachedToEntity = truck;
        RebirthPurgeTruckStart.Enabled = false; RebirthPurgePlayerWraps.Tick(true);
        Assert(player.bag.Delivered.Count == 0, "Non-Purge no backpack grant");
        RebirthPurgeTruckStart.Enabled = true; GameManager.IsDedicatedServer = true;
        RebirthPurgePlayerWraps.Tick(true);
        Assert(player.bag.Delivered.Count == 0, "Dedicated does not mutate remote bag");
        GameManager.IsDedicatedServer = false;
        truck.vehicle.Item.SetMetadata("RebirthPurgeStarterWrapItems", "wrapA;wrapOther");
        RebirthPurgePlayerWraps.Tick(true);
        Assert(player.bag.Delivered.Count == 0, "Unassigned metadata rejected");
        Console.WriteLine(checks + " actual-source checks PASS; native APIs controlled, gameplay NOT RUN.");
    }
}
public class World { public bool Remote; public EntityPlayerLocal Player = new EntityPlayerLocal(); public bool IsRemote() => Remote; public EntityPlayerLocal GetPrimaryPlayer() => Player; }
public class EntityPlayerLocal { public Buffs Buffs = new Buffs(); public Bag bag = new Bag(); public object AttachedToEntity; public bool IsSpawned() => true; public bool IsDead() => false; }
public class EntityVehicle { public World world; public Vehicle vehicle = new Vehicle(); }
public class Vehicle { public ItemValue Item = new ItemValue(); public ItemValue GetUpdatedItemValue() => Item; public void SetItemValue(ItemValue item) { Item = item; } }
public class ItemValue
{
    public int CosmeticModCount = 1; public string Cosmetic = "dye"; public string Name;
    public object ItemClass = new object(); public Dictionary<string, string> Metadata = new Dictionary<string, string>();
    public bool IsEmpty() => false;
    public void SetCosmeticMod(int slot, ItemValue value) { Cosmetic = value.Name; }
    public void SetMetadata(string key, string value) { Metadata[key] = value; }
    public bool TryGetMetadata(string key, out string value) => Metadata.TryGetValue(key, out value);
}
public class ItemClass { public static ItemValue GetItem(string name, bool random) => new ItemValue { Name = name }; }
public class ItemStack { public ItemValue itemValue; public int count; public ItemStack(ItemValue value, int amount) { itemValue = value; count = amount; } }
public class Bag
{
    public bool Space = true, ThrowAfterInsert; public List<string> Delivered = new List<string>();
    public bool CanStack(ItemStack stack) => Space;
    public bool AddItem(ItemStack stack) { Delivered.Add(stack.itemValue.Name); if (ThrowAfterInsert) throw new Exception("Listener"); return true; }
}
public class Buffs { public Dictionary<string, float> Values = new Dictionary<string, float>(); public float GetCustomVar(string key) => Values.TryGetValue(key, out var value) ? value : 0; public void SetCustomVar(string key, float value, bool sync) { Values[key] = value; } }
public class Identifier { public string CombinedString = "EOS_test"; }
public class Persistent { public Identifier PrimaryId = new Identifier(); }
public class GameManager
{
    public static GameManager Instance; public static bool IsDedicatedServer; public static int Notices; public World World;
    public Persistent GetPersistentLocalPlayer() => new Persistent(); public Identifier getPersistentPlayerID(object client) => new Identifier();
    public static void ShowTooltip(EntityPlayerLocal p, string text, bool a, bool b, float seconds) { Notices++; }
}
public static class RebirthPurgeTruckStart { public static bool Enabled = true; }
public static class RebirthCamperWrapCatalog { public static string FromItem(string name) => name.StartsWith("wrap") ? name : null; }
public static class RebirthCharacterCreationHoldService { public static bool IsHeld(EntityPlayerLocal p) => false; }
public static class RebirthBackpackLibraryReservation { public static bool IsHeld(EntityPlayerLocal p) => false; }
public static class Localization { public static string Get(string key) => key; }
public static class Log { public static void Warning(string text) { } }
namespace UnityEngine { public static class Time { public static float realtimeSinceStartup; } }
