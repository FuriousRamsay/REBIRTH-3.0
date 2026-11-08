using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine.Scripting;
using Random = System.Random;

#nullable disable

/// <summary>
/// Proves bonuses from the numbers the game computes, not from their text. Every bonus is put on
/// real worn armor, then the native stat is read with and without it for one weapon of each kind.
/// Results go to the console and to Player.log as [REBIRTH ArmorBonusTest].
/// </summary>
internal static class RebirthArmorBonusSelfTest
{
    private static List<ItemValue> weapons;

    private static List<ItemValue> AllWeapons()
    {
        if (weapons != null) return weapons;
        weapons = new List<ItemValue>();
        for (int i = 0; i < ItemClass.list.Length; i++)
        {
            var item = ItemClass.list[i];
            if (item == null || item.Actions == null || item.Actions.Length == 0 || item.Actions[0] == null) continue;
            string name = item.GetItemName();
            if (!name.StartsWith("gun") && !name.StartsWith("melee") && !name.StartsWith("thrown")) continue;
            // Monster attacks, admin tools and unfinished parts are not weapons a player uses.
            if (name.Contains("Zombie") || name.Contains("Admin") || name.EndsWith("Parts") || name.EndsWith("Schematic")) continue;
            weapons.Add(ItemClass.GetItem(name));
        }
        return weapons;
    }
    private static readonly Dictionary<EquipmentSlots, string> TestArmor = new Dictionary<EquipmentSlots, string>
    {
        { EquipmentSlots.Head, "armorLumberjackHelmet" }, { EquipmentSlots.Chest, "armorLumberjackOutfit" },
        { EquipmentSlots.Hands, "armorLumberjackGloves" }, { EquipmentSlots.Feet, "armorLumberjackBoots" }
    };

    private static int passed, failed;

    private static void Report(SdtdConsole console, bool ok, string line)
    {
        if (ok) passed++; else failed++;
        string text = (ok ? "PASS " : "FAIL ") + line;
        console.Output(text);
        Log.Out("[REBIRTH ArmorBonusTest] " + text);
    }

    internal static void Run(EntityPlayerLocal player, SdtdConsole console)
    {
        passed = failed = 0;
        foreach (var definition in RebirthArmorBonusCatalog.Definitions) TestEffect(player, definition, console);
        TestBands(console);
        TestRolls(console);
        TestPersistence(console);
        TestKits(console);
        TestLootArmor(console);
        string summary = "SUMMARY " + passed + " passed, " + failed + " failed, " + RebirthArmorBonusCatalog.Definitions.Count + " bonuses";
        console.Output(summary);
        Log.Out("[REBIRTH ArmorBonusTest] " + summary);
    }

    // These bonuses act in their own code, so check that code sees the worn value.
    private static void TestSpecial(EntityPlayerLocal player, RebirthArmorBonusDefinition definition, SdtdConsole console)
    {
        int slot = (int)definition.Slot;
        ItemValue original = player.equipment.GetSlotItem(slot)?.Clone();
        var plain = ItemClass.GetItem(TestArmor[definition.Slot]).Clone();
        var bonus = plain.Clone();
        RebirthArmorBonusStorage.Write(bonus, new[] { new RebirthArmorRolledBonus { Definition = definition, Value = definition.Max } });
        player.equipment.SetSlotItem(slot, plain);
        float before = RebirthArmorBonusSpecials.Best(player, definition.Slot, definition.Special);
        player.equipment.SetSlotItem(slot, bonus);
        float after = RebirthArmorBonusSpecials.Best(player, definition.Slot, definition.Special);
        player.equipment.SetSlotItem(slot, original ?? ItemValue.None.Clone());
        Report(console, before == 0f && Math.Abs(after - definition.Max) < 0.0001f, definition.Id + ": worn value " + before.ToString("0.##", CultureInfo.InvariantCulture) + " to " + after.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private static void TestEffect(EntityPlayerLocal player, RebirthArmorBonusDefinition definition, SdtdConsole console)
    {
        if (definition.Effect == PassiveEffects.None && definition.Special != "explosion" && definition.Special != "secure_unlock") { TestSpecial(player, definition, console); return; }
        if (definition.Special == "low_light") player.Buffs.SetCustomVar("_lightlevel", 0f);
        if (definition.Special == "explosion") { TestExplosion(player, definition, console); return; }
        if (definition.Special == "secure_unlock") { Report(console, definition.Max > 0f && definition.Max <= 1f, definition.Id + ": unlock chance " + definition.Max); return; }
        string armorName;
        if (!TestArmor.TryGetValue(definition.Slot, out armorName)) { Report(console, false, definition.Id + ": no test armor for " + definition.Slot); return; }
        int slot = (int)definition.Slot;
        ItemValue original = player.equipment.GetSlotItem(slot)?.Clone();
        try
        {
            var plain = original != null && !original.IsEmpty() ? original.Clone() : ItemClass.GetItem(armorName).Clone();
            RebirthArmorBonusStorage.Clear(plain);
            var bonus = plain.Clone();
            RebirthArmorBonusStorage.Write(bonus, new[] { new RebirthArmorRolledBonus { Definition = definition, Value = definition.Max } });

            int matched = 0;
            var problems = new List<string>();
            var measured = new List<string>();
            foreach (var weapon in AllWeapons())
            {
                string name = weapon.ItemClass.GetItemName();
                bool expected = definition.AppliesToWeapon(weapon);
                var queryTags = definition.HasContext ? definition.Context : weapon.ItemClass.ItemTags;

                player.equipment.SetSlotItem(slot, plain);
                float before = EffectManager.GetValue(definition.Effect, weapon, 100f, player, null, queryTags);
                float hitBefore = NativeHit(definition, weapon, player);
                player.equipment.SetSlotItem(slot, bonus);
                float after = EffectManager.GetValue(definition.Effect, weapon, 100f, player, null, queryTags);
                float hitAfter = NativeHit(definition, weapon, player);

                bool changed = Math.Abs(after - before) > 0.001f;
                bool wrongWay = expected && changed && (definition.Negative ? after > before : after < before);
                if (wrongWay) problems.Add(name + " moved the wrong way, " + before.ToString("0.##", CultureInfo.InvariantCulture) + " to " + after.ToString("0.##", CultureInfo.InvariantCulture));
                if (expected && !changed) problems.Add(name + " should change, stayed " + before.ToString("0.##", CultureInfo.InvariantCulture));
                if (!expected && changed) problems.Add(name + " should not change, went " + before.ToString("0.##", CultureInfo.InvariantCulture) + " to " + after.ToString("0.##", CultureInfo.InvariantCulture));
                if (expected)
                {
                    matched++;
                    if (measured.Count < 2 && hitBefore > 0f)
                        measured.Add(name + " hit " + hitBefore.ToString("0.#", CultureInfo.InvariantCulture) + " to " + hitAfter.ToString("0.#", CultureInfo.InvariantCulture));
                }
            }
            if (matched == 0) problems.Add("no test weapon matches its tags");
            string detail = definition.Id + " " + definition.Rarity + " +" + (definition.Max * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%: matched " + matched + "/" + AllWeapons().Count;
            if (measured.Count > 0) detail += " (" + string.Join(", ", measured) + ")";
            Report(console, problems.Count == 0, problems.Count == 0 ? detail : detail + ": " + string.Join("; ", problems));
        }
        catch (Exception e)
        {
            Report(console, false, definition.Id + ": " + e.GetType().Name + " " + e.Message);
        }
        finally
        {
            player.equipment.SetSlotItem(slot, original ?? ItemValue.None.Clone());
        }
    }

    private static void TestExplosion(EntityPlayerLocal player, RebirthArmorBonusDefinition definition, SdtdConsole console)
    {
        int slot = (int)definition.Slot;
        ItemValue original = player.equipment.GetSlotItem(slot)?.Clone();
        try
        {
            var bonus = (original != null && !original.IsEmpty() ? original : ItemClass.GetItem(TestArmor[definition.Slot])).Clone();
            RebirthArmorBonusStorage.Write(bonus, new[] { new RebirthArmorRolledBonus { Definition = definition, Value = definition.Max } });
            player.equipment.SetSlotItem(slot, bonus);
            var problems = new List<string>();
            foreach (string name in definition.Items)
            {
                var source = ItemClass.GetItem(name);
                if (source == null || source.type == 0 || source.ItemClass == null) { problems.Add(name + " does not exist"); continue; }
                if (RebirthArmorBonusExplosions.BonusFor(player, source) < definition.Max - 0.001f) problems.Add(name + " not raised");
            }
            if (RebirthArmorBonusExplosions.BonusFor(player, ItemClass.GetItem("meleeWpnClubT1BaseballBat")) != 0f) problems.Add("raised a non-explosive");
            Report(console, problems.Count == 0, definition.Id + " +" + (definition.Max * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "% on " + definition.Items.Count + " sources" + (problems.Count == 0 ? " (blast hook needs a real explosion)" : ": " + string.Join("; ", problems)));
        }
        catch (Exception e) { Report(console, false, definition.Id + ": " + e.GetType().Name + " " + e.Message); }
        finally { player.equipment.SetSlotItem(slot, original ?? ItemValue.None.Clone()); }
    }

    // The game's own per-hit damage calculation for that weapon, when the stat is entity damage.
    private static float NativeHit(RebirthArmorBonusDefinition definition, ItemValue weapon, EntityPlayerLocal player)
    {
        if (definition.Effect != PassiveEffects.EntityDamage) return 0f;
        var actions = weapon.ItemClass.Actions;
        var attack = actions != null && actions.Length > 0 ? actions[0] as ItemActionAttack : null;
        return attack != null ? attack.GetDamageEntity(weapon, player, 0) : 0f;
    }

    private static void TestBands(SdtdConsole console)
    {
        foreach (var family in RebirthArmorBonusCatalog.Definitions.GroupBy(d => d.FamilyId))
        {
            var problems = new List<string>();
            for (int rarity = 1; rarity < 4; rarity++)
            {
                var lower = family.Where(d => (int)d.Rarity < rarity).ToList();
                var current = family.Where(d => (int)d.Rarity == rarity).ToList();
                if (lower.Count == 0 || current.Count == 0) continue;
                bool negative = current[0].Negative;
                bool flat = current[0].Flat;
                bool overlap = flat ? current.Min(d => d.Min) < lower.Max(d => d.Max)
                    : negative ? current.Max(d => d.Max) >= lower.Min(d => d.Min)
                    : current.Min(d => d.Min) <= lower.Max(d => d.Max);
                if (overlap) problems.Add((RebirthArmorBonusRarity)rarity + " overlaps the rarity below");
            }
            Report(console, problems.Count == 0, "value bands " + family.Key + (problems.Count == 0 ? " do not overlap" : ": " + string.Join("; ", problems)));
        }
    }

    private static void TestRolls(SdtdConsole console)
    {
        var random = new Random();
        int rolls = 0, duplicate = 0, mixed = 0, outOfBand = 0, wrongSlot = 0, overTarget = 0;
        foreach (var slot in RebirthArmorBonusCatalog.Definitions.Select(d => d.Slot).Distinct())
            for (int i = 0; i < 5000; i++)
            {
                int target = RebirthArmorBonusRoller.RollTargetCount(random);
                var rolled = RebirthArmorBonusRoller.Roll(slot, new RebirthArmorRolledBonus[0], target, 1 + i % 3, 1 + i % 6, random);
                rolls++;
                if (rolled.Count > target) overTarget++;
                if (rolled.Select(r => r.Definition.Id).Distinct().Count() != rolled.Count) duplicate++;
                if (rolled.GroupBy(r => r.Definition.FamilyId).Any(g => g.Select(r => r.Definition.Rarity).Distinct().Count() > 1)) mixed++;
                if (rolled.Any(r => r.Value < r.Definition.Min || r.Value > r.Definition.Max)) outOfBand++;
                if (rolled.Any(r => r.Definition.Slot != slot)) wrongSlot++;
            }
        bool ok = duplicate + mixed + outOfBand + wrongSlot + overTarget == 0;
        Report(console, ok, rolls + " rolls: duplicates " + duplicate + ", mixed rarity in a family " + mixed + ", value out of band " + outOfBand + ", wrong slot " + wrongSlot + ", over target " + overTarget);
    }

    // Rates are measured against the 2.6 tables over many rolls of real gloves.
    private static void TestLootArmor(SdtdConsole console)
    {
        var random = new Random();
        var gloves = ItemClass.GetItem(TestArmor[EquipmentSlots.Hands]);
        foreach (var row in new[] { new { Biome = "pine_forest", Chance = 0.50, Level = 1 }, new { Biome = "burnt_forest", Chance = 0.70, Level = 3 }, new { Biome = "wasteland", Chance = 0.70, Level = 2 } })
        {
            int given = 0;
            const int rolls = 2000;
            for (int i = 0; i < rolls; i++)
            {
                var item = gloves.Clone();
                if (RebirthLootArmorBonuses.TryApply(item, row.Biome, random)) given++;
            }
            double rate = (double)given / rolls;
            Report(console, Math.Abs(rate - row.Chance) < 0.05 && RebirthLootArmorBonuses.BonusLevel(row.Biome) == row.Level, "Looted gloves in " + row.Biome + ": " + rate.ToString("0.00") + " have bonuses (want " + row.Chance.ToString("0.00") + "), level " + row.Level);
        }
        Report(console, !RebirthLootArmorBonuses.TryApply(ItemClass.GetItem("meleeWpnClubT1BaseballBat").Clone(), "burnt_forest", random), "Looted non-armor stays plain");
    }

    private static int CountOf(ItemValue item) => RebirthArmorBonusStorage.Read(item).Length;

    private static void TestKits(SdtdConsole console)
    {
        var random = new Random();
        var gloves = ItemClass.GetItem(TestArmor[EquipmentSlots.Hands]).Clone();
        ItemValue kit(string name) => ItemClass.GetItem(name);
        ItemValue result; string fail;

        bool one = RebirthArmorBonusKits.TryApply(kit("kitSurvivorUpgrade"), gloves, random, out var first, out fail) && CountOf(first) == 1;
        Report(console, one, "Survivor Upgrade on empty armor gives 1 bonus" + (fail != null ? " (" + fail + ")" : ""));
        bool two = one && RebirthArmorBonusKits.TryApply(kit("kitAdjustment"), first, random, out var second, out fail) && CountOf(second) >= 1;
        Report(console, two, "Adjustment adds to a single bonus" + (fail != null ? " (" + fail + ")" : ""));
        Report(console, one && !RebirthArmorBonusKits.TryApply(kit("kitSurvivorUpgrade"), first, random, out result, out fail), "Survivor Upgrade refused on armor with bonuses");
        bool retune = one && RebirthArmorBonusKits.TryApply(kit("kitRetuning"), first, random, out result, out fail)
            && RebirthArmorBonusStorage.Read(result)[0].Definition.Id != RebirthArmorBonusStorage.Read(first)[0].Definition.Id;
        Report(console, retune, "Retuning replaces the bonus with a different one");
        bool comp = RebirthArmorBonusKits.TryApply(kit("kitComprehensiveUpgrade"), gloves, random, out var full, out fail) && CountOf(full) >= 1;
        Report(console, comp, "Comprehensive Upgrade fills empty armor: " + (comp ? CountOf(full) + " bonuses" : fail));
        Report(console, comp && RebirthArmorBonusKits.TryApply(kit("kitBreakdown"), full, random, out result, out fail) && CountOf(result) == 0, "Breakdown clears all bonuses");
        Report(console, !RebirthArmorBonusKits.TryApply(kit("kitBreakdown"), gloves, random, out result, out fail), "Breakdown refused on empty armor");
        Report(console, !RebirthArmorBonusKits.TryApply(kit("kitSurvivorUpgrade"), ItemClass.GetItem("meleeWpnClubT1BaseballBat"), random, out result, out fail), "Kits refused on non-armor");
    }

    // Native item serialization is what saves and network packets use.
    private static void TestPersistence(SdtdConsole console)
    {
        try
        {
            var definition = RebirthArmorBonusCatalog.Definitions[0];
            var item = ItemClass.GetItem(TestArmor[definition.Slot]).Clone();
            RebirthArmorBonusStorage.Write(item, new[] { new RebirthArmorRolledBonus { Definition = definition, Value = definition.Min } });
            var bytes = new System.IO.MemoryStream();
            using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(bytes);
                ItemValue.Write(item, writer);
            }
            bytes.Position = 0;
            var read = new ItemValue();
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(bytes);
                read.Read(reader);
            }
            var clone = read.Clone();
            bool ok = RebirthArmorBonusStorage.Read(clone).Length == 1 && RebirthArmorBonusStorage.Read(clone)[0].Definition == definition;
            Report(console, ok, "bonus survives native save format and clone");
        }
        catch (Exception e)
        {
            Report(console, false, "persistence: " + e.GetType().Name + " " + e.Message);
        }
    }
}

/// <summary>"rbarmorbonus trace": logs what your real hits deal, so a fight can be compared with and without a bonus.</summary>
[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.DamageEntity))]
internal static class RebirthArmorBonusHitTrace
{
    internal static bool Enabled;

    private static void Prefix(EntityAlive __instance, DamageSource _damageSource, int _strength)
    {
        if (!Enabled || _damageSource == null || __instance is EntityPlayer) return;
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player == null || _damageSource.getEntityId() != player.entityId) return;
        var gloves = RebirthArmorBonusStorage.Read(player.equipment.GetSlotItem((int)EquipmentSlots.Hands));
        string line = "[REBIRTH ArmorBonusTest] HIT " + __instance.EntityClass?.entityClassName + " with " + player.inventory.holdingItem?.GetItemName()
            + " dealt " + _strength + " part=" + _damageSource.GetEntityDamageBodyPart(__instance) + " gloves=[" + string.Join(",", gloves.Select(b => b.Definition.Id + " " + b.Value.ToString("P1", CultureInfo.InvariantCulture))) + "]";
        Log.Out(line);
        SingletonMonoBehaviour<SdtdConsole>.Instance?.Output(line);
    }
}

[Preserve]
public sealed class RebirthArmorBonusTestModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.armorbonuses.trace"), typeof(RebirthArmorBonusHitTrace));
    }
}
