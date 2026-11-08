using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;
using Random = System.Random;

#nullable disable

/// <summary>The Modifier Station's two slots, for an armor piece and a kit. No recipe materials apply here.</summary>
[Preserve]
public sealed class XUiC_RebirthKitInputGrid : XUiC_WorkstationInputGrid
{
    public override void UpdateBackend(ItemStack[] stacks)
    {
        var original = WorkstationData?.TileEntity?.Input;
        if (original == null || stacks == null) return;
        var merged = ItemStack.CreateArray(original.Length);
        for (int i = 0; i < original.Length; i++) merged[i] = original[i].Clone();
        for (int i = 0; i < stacks.Length && i < merged.Length; i++) merged[i] = stacks[i].Clone();
        base.UpdateBackend(merged);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        SetSlots(WorkstationData.TileEntity.Input);
    }

    // Owner 2026-11-07: the first slot takes only armor and the second only kits (2.6 apply order), each shows a grayed
    // placeholder icon while empty, and shift-click sends armor to the first and kits to the second.
    internal const int ArmorSlot = 0, KitSlot = 1;
    private static readonly string[] KitNames = { "kitSurvivorUpgrade", "kitAdjustment", "kitRetuning", "kitComprehensiveUpgrade", "kitEliteReinforcement", "kitBreakdown" };
    private static readonly string[] PlaceholderArmor = { "armorScavengerJacket", "armorRaiderChest", "armorLumberjackJacket", "armorPrimitiveChest" };

    public override void Init()
    {
        base.Init();
        if (itemControllers == null) return;
        if (itemControllers.Length > ArmorSlot && itemControllers[ArmorSlot] is XUiC_RequiredItemStack armor)
        {
            var allowed = new System.Collections.Generic.List<ItemClass>();
            foreach (var name in PlaceholderArmor) { var c = ItemClass.GetItemClass(name); if (c is ItemClassArmor) { allowed.Add(c); break; } }
            foreach (var c in ItemClass.list)
                if (c is ItemClassArmor && RebirthArmorBonusKits.TryGetSlot(new ItemValue(c.Id), out _) && !allowed.Contains(c)) allowed.Add(c);
            Restrict(armor, allowed);
        }
        if (itemControllers.Length > KitSlot && itemControllers[KitSlot] is XUiC_RequiredItemStack kit)
        {
            var allowed = new System.Collections.Generic.List<ItemClass>();
            foreach (var name in KitNames) { var c = ItemClass.GetItemClass(name); if (c != null) allowed.Add(c); }
            Restrict(kit, allowed);
        }
    }

    private static void Restrict(XUiC_RequiredItemStack slot, System.Collections.Generic.List<ItemClass> allowed)
    {
        slot.ClearAllowedItemClasses();
        foreach (var c in allowed) slot.allowedItemClasses.Add(c);
        slot.RequiredType = XUiC_RequiredItemStack.RequiredTypes.ItemClass;
        slot.RequiredItemOnly = true;   // only these classes; the first one is the grayed icon while empty
        slot.IsDirty = true;
    }

    /// <summary>Shift-click: armor to the armor slot, kits to the kit slot; anything else is refused.</summary>
    public override bool AddItem(ItemClass _itemClass, ItemStack _itemStack)
    {
        if (_itemStack == null || _itemStack.IsEmpty() || itemControllers == null) return false;
        int index = _itemStack.itemValue.ItemClass is ItemClassArmor ? ArmorSlot : RebirthArmorBonusKits.IsKit(_itemStack.itemValue) ? KitSlot : -1;
        if (index < 0 || index >= itemControllers.Length) return false;
        var slot = itemControllers[index];
        var held = slot.ItemStack;
        if (held == null || held.IsEmpty())
        {
            slot.ItemStack = _itemStack.Clone();
            _itemStack.count = 0;
            slot.ForceRefreshItemStack();
            return true;
        }
        if (held.itemValue.type != _itemStack.itemValue.type) return false;
        int moved = _itemStack.count;
        if (!held.CanStackPartly(ref moved) || moved <= 0) return false;
        held.count += moved;
        slot.ItemStack = held;
        _itemStack.count -= moved;
        slot.ForceRefreshItemStack();
        return true;
    }
}

[HarmonyPatch(typeof(XUiC_WorkstationInputGrid), nameof(XUiC_WorkstationInputGrid.AcceptsMaterial))]
internal static class RebirthKitGridAdmission
{
    private static bool Prefix(XUiC_WorkstationInputGrid __instance, ref bool __result)
    {
        if (!(__instance is XUiC_RebirthKitInputGrid)) return true;
        __result = true;
        return false;
    }
}

/// <summary>2.6 enchanting window: put in armor and a kit, press Apply, and the result goes to Output.</summary>
[Preserve]
public sealed class XUiC_RebirthModifierPanel : XUiController
{
    private XUiC_RebirthKitInputGrid grid;
    private XUiV_Label status, bonuses;
    private string message = string.Empty;
    private float nextPoll;
    private readonly Random random = new Random();
    internal static XUiC_RebirthModifierPanel Current { get; private set; }

    public override void Init()
    {
        base.Init();
        grid = GetChildByType<XUiC_RebirthKitInputGrid>();
        status = GetChildById("recyclerGate")?.ViewComponent as XUiV_Label;
        bonuses = GetChildById("recyclerYield")?.ViewComponent as XUiV_Label;
        var apply = GetChildById("btnEnchant") as XUiC_SimpleButton;
        if (apply != null) apply.OnPressed += delegate { Apply(); };
    }

    public override void OnOpen()
    {
        base.OnOpen();
        message = string.Empty;
        Current = this;
    }

    public override void OnClose()
    {
        if (Current == this) Current = null;
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (windowGroup?.isShowing != true || Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.25f;
        var slots = grid?.GetSlots();
        ItemValue item = null;
        if (slots != null)
            foreach (var slot in slots)
                if (slot != null && !slot.IsEmpty() && !RebirthArmorBonusKits.IsKit(slot.itemValue)) { item = slot.itemValue; break; }
        if (bonuses != null) bonuses.Text = item != null ? RebirthArmorBonusText.DescribeAll(item) : string.Empty;
        if (status != null) status.Text = message.Length > 0 ? message : Localization.Get("xuiEnchantSlotHint");
    }

    private void Apply()
    {
        var station = (windowGroup.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;
        var slots = grid?.GetSlots();
        if (station == null || slots == null || slots.Length < 2) { Fail("xuiEnchantSlotsUnavailable"); return; }
        grid.UpdateBackend(slots);

        // The server rolls the kit. A client asks, and gets the slots back in the reply.
        if (GameManager.Instance.World.IsRemote())
        {
            var player = xui.playerUI.entityPlayer;
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthModifierApply>().Setup(player.entityId, station.ToWorldPos()));
            return;
        }
        string failKey;
        if (!RebirthModifierStationService.TryApply(station, random, out failKey)) { Fail(failKey); return; }
        Show(station.Input, station.Output);
    }

    internal void Show(ItemStack[] input, ItemStack[] output)
    {
        grid.SetSlots(input);
        windowGroup.Controller.GetChildByType<XUiC_WorkstationOutputGrid>()?.SetSlots(output);
        message = string.Empty;
        windowGroup.Controller.SetAllChildrenDirty();
    }

    internal bool Shows(Vector3i position)
    {
        var station = (windowGroup?.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;
        return windowGroup?.isShowing == true && station != null && station.ToWorldPos() == position;
    }

    internal void Fail(string key)
    {
        message = Localization.Get(key);
        GameManager.ShowTooltip(xui.playerUI.entityPlayer, message, string.Empty, "ui_denied");
    }
}

/// <summary>Applies a kit at a Modifier Station. Runs where the world is authoritative.</summary>
public static class RebirthModifierStationService
{
    private const string RequiredTool = "FuriousRamsayHammerPliers";

    public static bool TryApply(TileEntityWorkstation station, Random random, out string failKey)
    {
        failKey = null;
        var slots = station.Input;
        if (slots == null || slots.Length < 2) { failKey = "xuiEnchantSlotsUnavailable"; return false; }

        const int itemIndex = 0, kitIndex = 1; // 2.6: the first slot holds the item, the second the kit.
        if (slots[itemIndex] == null || slots[itemIndex].IsEmpty() || !(slots[itemIndex].itemValue.ItemClass is ItemClassArmor)) { failKey = "xuiEnchantNotItem"; return false; }
        if (slots[kitIndex] == null || slots[kitIndex].IsEmpty() || !RebirthArmorBonusKits.IsKit(slots[kitIndex].itemValue)) { failKey = "xuiEnchantNotAKit"; return false; }

        int toolType = ItemClass.GetItem(RequiredTool).type;
        if (station.Tools == null || !station.Tools.Any(t => t != null && !t.IsEmpty() && t.itemValue.type == toolType)) { failKey = "xuiEnchantToolRequired"; return false; }

        var outputs = ItemStack.Clone(station.Output);
        int free = Array.FindIndex(outputs, s => s == null || s.IsEmpty());
        if (free < 0) { failKey = "xuiEnchantOutputFull"; return false; }

        ItemValue result;
        if (!RebirthArmorBonusKits.TryApply(slots[kitIndex].itemValue, slots[itemIndex].itemValue, random, out result, out failKey)) return false;

        // Nothing is consumed unless the kit applied.
        outputs[free] = new ItemStack(result, 1);
        var updated = ItemStack.Clone(slots);
        updated[itemIndex] = updated[itemIndex].count > 1 ? new ItemStack(updated[itemIndex].itemValue, updated[itemIndex].count - 1) : ItemStack.Empty.Clone();
        updated[kitIndex] = updated[kitIndex].count > 1 ? new ItemStack(updated[kitIndex].itemValue, updated[kitIndex].count - 1) : ItemStack.Empty.Clone();
        station.Input = updated;
        station.Output = outputs;
        station.SetModified();
        return true;
    }
}

[Preserve]
public sealed class NetPackageRebirthModifierApply : NetPackage
{
    private const float MaximumDistance = 8f;
    private static readonly Random random = new Random();
    private int playerEntityId;
    private Vector3i position;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthModifierApply Setup(int playerId, Vector3i stationPosition)
    {
        playerEntityId = playerId;
        position = stationPosition;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        var b = (System.IO.BinaryReader)reader;
        playerEntityId = b.ReadInt32();
        position = StreamUtils.ReadVector3i(b);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        var b = (System.IO.BinaryWriter)writer;
        b.Write(playerEntityId);
        StreamUtils.Write(b, position);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerEntityId)) return;
        var player = world.GetEntity(playerEntityId) as EntityPlayer;
        var station = world.GetTileEntity(position) as TileEntityWorkstation;
        if (player == null || station == null || station.block?.GetBlockName() != "ModifierStation") return;
        if ((player.position - position.ToVector3()).sqrMagnitude > MaximumDistance * MaximumDistance) return;
        string failKey;
        bool applied = RebirthModifierStationService.TryApply(station, random, out failKey);
        Sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthModifierResult>().Setup(position, applied, failKey, station.Input, station.Output));
    }

    public int GetLength() => 20;
}

[Preserve]
public sealed class NetPackageRebirthModifierResult : NetPackage
{
    private Vector3i position;
    private bool applied;
    private string failKey;
    private ItemStack[] input, output;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthModifierResult Setup(Vector3i stationPosition, bool wasApplied, string failure, ItemStack[] inputStacks, ItemStack[] outputStacks)
    {
        position = stationPosition;
        applied = wasApplied;
        failKey = failure ?? string.Empty;
        input = inputStacks;
        output = outputStacks;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        var b = (System.IO.BinaryReader)reader;
        position = StreamUtils.ReadVector3i(b);
        applied = b.ReadBoolean();
        failKey = b.ReadString();
        input = GameUtils.ReadItemStack(reader);
        output = GameUtils.ReadItemStack(reader);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        var b = (System.IO.BinaryWriter)writer;
        StreamUtils.Write(b, position);
        b.Write(applied);
        b.Write(failKey);
        GameUtils.WriteItemStack(writer, input);
        GameUtils.WriteItemStack(writer, output);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        var panel = XUiC_RebirthModifierPanel.Current;
        if (panel == null || !panel.Shows(position)) return;
        if (applied)
        {
            var station = world?.GetTileEntity(position) as TileEntityWorkstation;
            if (station != null) { station.Input = input; station.Output = output; }
            panel.Show(input, output);
        }
        else panel.Fail(failKey);
    }

    public int GetLength() => 256;
}

[Preserve]
public sealed class RebirthModifierStationModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        var harmony = new Harmony("rebirth.armorbonuses.modifier");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthKitGridAdmission));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthControlPanelSalvage));
    }
}

/// <summary>2.6: destroying a control panel with a salvage tool can give kit parts, by tool tier.</summary>
[HarmonyPatch(typeof(Block), nameof(Block.OnBlockDestroyedBy))]
internal static class RebirthControlPanelSalvage
{
    private static readonly string[] Parts =
    {
        "partRetrofitHousing", "partTuningBoard", "partFieldMechanismPack", "partRegulatorPack", "partPurgeCanister",
        "partServiceHousing", "partStabilityClampSet", "partRetuningCartridge", "partAdjustmentMatrix", "partControlShunt", "partProfileBridge",
        "partReinforcedHousing", "partReinforcementCore", "partStabilityFrame", "partControlLattice", "partRegulatorAssembly",
        "partMasterHousing", "partComprehensiveCore", "partControlArray", "partMasterRegulator", "partFieldArray"
    };
    private static readonly int[] Weights = { 100, 100, 100, 100, 100, 40, 40, 40, 40, 40, 40, 12, 12, 12, 12, 12, 3, 3, 3, 3, 3 };
    // Roll below the first value gives 0 parts, then 1, then 2, otherwise 3. Index is the tool tier.
    private static readonly float[] Zero = { 0.35f, 0.20f, 0.05f }, One = { 0.60f, 0.45f, 0.30f }, Two = { 0.85f, 0.80f, 0.75f };
    private static readonly Random random = new Random();

    private static void Postfix(Block __instance, WorldBase _world, BlockValueRef _bvRef, int _entityId, bool _bUseHarvestTool)
    {
        if (!_bUseHarvestTool || _world == null || GameManager.IsDedicatedServer) return;
        if (!(__instance.GetBlockName() ?? string.Empty).StartsWith("controlPanel", StringComparison.OrdinalIgnoreCase)) return;
        var player = _world.GetEntity(_entityId) as EntityPlayerLocal;
        if (player == null) return;
        string tool = player.inventory?.holdingItem?.GetItemName() ?? string.Empty;
        int tier = tool.StartsWith("meleeToolSalvageT3") ? 2 : tool.StartsWith("meleeToolSalvageT2") ? 1 : tool.StartsWith("meleeToolSalvageT1") ? 0 : -1;
        if (tier < 0) return;
        double roll = random.NextDouble();
        int count = roll < Zero[tier] ? 0 : roll < One[tier] ? 1 : roll < Two[tier] ? 2 : 3;
        var taken = new bool[Parts.Length];
        for (int drop = 0; drop < count; drop++)
        {
            int total = 0;
            for (int i = 0; i < Parts.Length; i++) if (!taken[i]) total += Weights[i];
            int pick = random.Next(total), chosen = 0;
            for (int i = 0; i < Parts.Length; i++)
            {
                if (taken[i]) continue;
                if (pick < Weights[i]) { chosen = i; break; }
                pick -= Weights[i];
            }
            taken[chosen] = true;
            var stack = new ItemStack(ItemClass.GetItem(Parts[chosen]), 1);
            var inventory = player.PlayerUI?.xui?.PlayerInventory;
            if (inventory == null || !inventory.AddItem(stack, true))
                GameManager.Instance.ItemDropServer(stack, player.position, Vector3.zero, player.entityId, 60f, false);
        }
    }
}

/// <summary>
/// Modifier Station armor and kit slots: restricted like a native RequiredItemStack, but drawn exactly like the
/// backpack and output slots (same palette, rarity tint and quality bar) (owner 2026-11-08).
/// </summary>
[UnityEngine.Scripting.Preserve]
public sealed class XUiC_RebirthModifierSlot : XUiC_RequiredItemStack
{
    private RebirthSlotPalette palette;
    public override void Init() { base.Init(); palette = new RebirthSlotPalette(this); }
    public override bool GetBindingValueInternal(ref string value, string name)
    {
        if (name == "backgroundcolor") { value = RebirthSlotPalette.Normal; return true; }
        return base.GetBindingValueInternal(ref value, name);
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        palette?.Apply(false);
    }
}
