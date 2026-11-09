using System;
using System.Linq;
using Random = System.Random;

#nullable disable

/// <summary>
/// "rbbonus selftest" (armor bonus focus stage 6): checks the category tree, focused rolls and every kit rule against
/// real rolls on test items. Nothing is given to or taken from the player. Each check prints PASS or FAIL, then
/// "rbbonus selftest: N passed, M failed" (the Game Bridge scenario armor_bonus_focus.json reads that line).
/// </summary>
public static class RebirthArmorBonusFocusSelfTest
{
    private static int passed, failed;
    private static Action<string> say;

    private static readonly EquipmentSlots[] Slots = { EquipmentSlots.Head, EquipmentSlots.Chest, EquipmentSlots.Hands, EquipmentSlots.Feet };

    private static void Check(string name, bool ok, string detail = null)
    {
        if (ok) passed++; else failed++;
        say((ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? string.Empty : " (" + detail + ")"));
    }

    private static ItemValue Armor(EquipmentSlots slot, int quality = 4)
    {
        foreach (var itemClass in ItemClass.list)
            if (itemClass is ItemClassArmor armor && armor.EquipSlot == slot)
                return new ItemValue(armor.Id) { Quality = (ushort)quality };
        return null;
    }

    private static ItemValue WithBonuses(EquipmentSlots slot, int count, Random random)
    {
        var item = Armor(slot);
        RebirthArmorBonusStorage.Write(item, RebirthArmorBonusRoller.Roll(slot, new RebirthArmorRolledBonus[0], count, 2, 4, random));
        return item;
    }

    private static string Key(RebirthArmorRolledBonus bonus) => RebirthArmorBonusTree.ExactKey(bonus.Definition.Id);
    private static string CategoryOf(RebirthArmorRolledBonus bonus) => RebirthArmorBonusTree.CategoryOf(bonus.Definition)?.Id;

    private static bool Apply(string kit, ItemValue item, RebirthArmorBonusKitRequest request, float tailoring, Random random, out RebirthArmorRolledBonus[] result, out string failKey)
    {
        bool ok = RebirthArmorBonusKits.TryApply(ItemClass.GetItem(kit), item, request, tailoring, random, out var after, out failKey);
        result = ok ? RebirthArmorBonusStorage.Read(after) : new RebirthArmorRolledBonus[0];
        return ok;
    }

    private static RebirthArmorBonusKitRequest Rows(params RebirthArmorBonusRowAction[] actions)
    {
        var request = new RebirthArmorBonusKitRequest();
        foreach (var action in actions) request.Row(action);
        return request;
    }

    public static void Run(Action<string> output)
    {
        say = output;
        passed = failed = 0;
        var random = new Random(777);
        RebirthArmorRolledBonus[] result;
        string fail;

        // Tree
        int catalog = RebirthArmorBonusCatalog.Definitions.Count(d => !d.Retired);
        int assigned = Slots.Sum(s => RebirthArmorBonusTree.Categories(s).Sum(c => c.RowCount));
        Check("tree assigns every bonus", assigned == catalog && RebirthArmorBonusTree.Problems.Count == 0, assigned + "/" + catalog + ", problems " + RebirthArmorBonusTree.Problems.Count);

        // Focused rolls stay in their pool
        foreach (var slot in Slots)
            foreach (var category in RebirthArmorBonusTree.Categories(slot))
            {
                var focus = RebirthArmorBonusFocus.ForCategory(category.Id);
                var pool = RebirthArmorBonusTree.Pool(slot, focus);
                int outside = 0;
                for (int i = 0; i < 200; i++)
                {
                    var r = RebirthArmorBonusRoller.Roll(slot, new RebirthArmorRolledBonus[0], 1, 2, 4, random, focus);
                    if (r.Count != 1 || !pool.Contains(r[0].Definition.Id)) outside++;
                }
                Check("category roll stays in " + category.Id, outside == 0, outside + " outside");
            }

        var single = RebirthArmorBonusTree.Categories(EquipmentSlots.Head).SelectMany(c => c.Effects).SelectMany(e => e.Exacts).First(x => x.Rarities.Length == 1);
        int wrong = 0;
        for (int i = 0; i < 200; i++)
        {
            var r = RebirthArmorBonusRoller.Roll(EquipmentSlots.Head, new RebirthArmorRolledBonus[0], 1, 3, 6, random, RebirthArmorBonusFocus.ForExact(single.Key));
            if (r.Count != 1 || Key(r[0]) != single.Key || r[0].Definition.Rarity != single.Rarities[0]) wrong++;
        }
        Check("single-rarity exact always gives that rarity", wrong == 0, single.Key);

        // Gates and request shape
        string melee = "hands_melee", tools = "hands_tools", ranged = "hands_ranged_damage";
        Check("survivor without focus gives 1", Apply("kitSurvivorUpgrade", Armor(EquipmentSlots.Hands), null, 0f, random, out result, out fail) && result.Length == 1, fail);
        var catRequest = new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.ForCategory(melee));
        Check("category focus refused below Tailoring 10", !Apply("kitSurvivorUpgrade", Armor(EquipmentSlots.Hands), catRequest, 5f, random, out result, out fail) && fail == "xuiEnchantFocusNeedsTailoring10", fail);
        Check("category focus works at Tailoring 12", Apply("kitSurvivorUpgrade", Armor(EquipmentSlots.Hands), catRequest, 12f, random, out result, out fail) && CategoryOf(result[0]) == melee, fail);
        RebirthArmorBonusTree.TryGetCategory(melee, out var meleeCategory);
        // Combination families are picked by subject (owner 2026-11-08): the result always includes the subject, the
        // rarity follows the kit odds, and an exact combination (which would fix its rarity) is refused.
        var subject = meleeCategory.Effects.First(e => e.Subjects.Count > 0).Subjects.First();
        var exact = subject.Base;
        var exactRequest = new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.ForSubject(subject.Key));
        Check("subject pick refused below Tailoring 25", !Apply("kitSurvivorUpgrade", Armor(EquipmentSlots.Hands), exactRequest, 12f, random, out result, out fail) && fail == "xuiEnchantFocusNeedsTailoring25", fail);
        Check("subject pick works at Tailoring 30", Apply("kitSurvivorUpgrade", Armor(EquipmentSlots.Hands), exactRequest, 30f, random, out result, out fail) && subject.Members.Any(m => m.Key == Key(result[0])), fail);
        Check("focus for another armor type refused", !Apply("kitSurvivorUpgrade", Armor(EquipmentSlots.Head), exactRequest, 30f, random, out result, out fail) && fail == "xuiEnchantFocusWrongSlot", fail);
        var comboMember = RebirthArmorBonusTree.Categories(EquipmentSlots.Head).SelectMany(c => c.Effects).SelectMany(e => e.Subjects).SelectMany(sj => sj.Members).First(m => m.Tokens != null && m.Tokens.Length > 1);
        Check("exact combination pick refused", !Apply("kitSurvivorUpgrade", Armor(EquipmentSlots.Head), new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.ForExact(comboMember.Key)), 30f, random, out result, out fail) && fail == "xuiEnchantRequestInvalid", fail);
        var loot = RebirthArmorBonusTree.Categories(EquipmentSlots.Head).SelectMany(c => c.Effects).SelectMany(e => e.Subjects).First(sj => sj.Members.Count > 2);
        int notIncluded = 0; var rarities = new System.Collections.Generic.HashSet<RebirthArmorBonusRarity>();
        for (int i = 0; i < 400; i++)
        {
            if (!Apply("kitComprehensiveUpgrade", Armor(EquipmentSlots.Head, 6), new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.ForSubject(loot.Key)), 30f, random, out result, out fail)) { notIncluded++; continue; }
            if (!loot.Members.Any(m => m.Key == Key(result[0]))) notIncluded++;
            rarities.Add(result[0].Definition.Rarity);
        }
        Check("subject always included, rarity follows the kit odds (" + loot.Key + ")", notIncluded == 0 && rarities.Count >= 2, notIncluded + " not included, " + rarities.Count + " rarities");
        Check("too many rows refused", !Apply("kitSurvivorUpgrade", Armor(EquipmentSlots.Hands), new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.Any).Add(RebirthArmorBonusFocus.Any), 30f, random, out result, out fail) && fail == "xuiEnchantRequestInvalid", fail);

        // Adjustment: family rule and stale requests
        var one = WithBonuses(EquipmentSlots.Feet, 1, random); // feet bonuses are not combinations, so exact picks apply
        var existing = RebirthArmorBonusStorage.Read(one)[0];
        // Rule (owner 2026-11-08): only the same bonus twice is refused; another bonus of the same family is allowed.
        string existingKey = Key(existing);
        Check("adjustment refuses the same bonus already on the item", !Apply("kitAdjustment", one, new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.ForExact(existingKey)), 30f, random, out result, out fail) && fail == "xuiEnchantFocusFamilyTaken", fail);
        var sibling = RebirthArmorBonusTree.Categories(EquipmentSlots.Feet).SelectMany(c => c.Effects).Where(e => e.FamilyId == existing.Definition.FamilyId).SelectMany(e => e.Exacts).FirstOrDefault(x => x.Key != existingKey);
        if (sibling != null)
            Check("adjustment allows another bonus of the same family", Apply("kitAdjustment", one, new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.ForExact(sibling.Key)), 30f, random, out result, out fail) && result.Length == 2 && Key(result[1]) == sibling.Key, fail);
        Check("adjustment keeps the bonus and adds a random one of another family (2.6 rule for random rows)", Apply("kitAdjustment", one, new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.Any), 0f, random, out result, out fail)
            && result.Length == 2 && result[0].Definition.Id == existing.Definition.Id && result[1].Definition.FamilyId != existing.Definition.FamilyId, fail);
        Check("stale request refused", !Apply("kitAdjustment", one, new RebirthArmorBonusKitRequest { ItemBonuses = "not_this" }, 0f, random, out result, out fail) && fail == "xuiEnchantRequestStale", fail);
        Check("request for another kit refused", !Apply("kitAdjustment", one, new RebirthArmorBonusKitRequest { Kit = "kitRetuning" }, 0f, random, out result, out fail) && fail == "xuiEnchantRequestStale", fail);

        // Comprehensive: each row follows its category, one family each, 4th bonus about 25%
        int bad = 0, fours = 0, runs = 400;
        var comp = new RebirthArmorBonusKitRequest().Add(RebirthArmorBonusFocus.ForCategory(melee)).Add(RebirthArmorBonusFocus.ForCategory(tools)).Add(RebirthArmorBonusFocus.ForCategory(ranged));
        for (int i = 0; i < runs; i++)
        {
            if (!Apply("kitComprehensiveUpgrade", Armor(EquipmentSlots.Hands), comp, 12f, random, out result, out fail) || result.Length < 3) { bad++; continue; }
            if (CategoryOf(result[0]) != melee || CategoryOf(result[1]) != tools || CategoryOf(result[2]) != ranged) bad++;
            if (result.Select(b => Key(b)).Distinct().Count() != result.Length) bad++;
            if (result.Length == 4) fours++;
        }
        Check("comprehensive rows follow their categories, no bonus twice", bad == 0, bad + " bad of " + runs);
        Check("comprehensive 4th bonus about 25%", fours > runs * 0.17 && fours < runs * 0.33, (100.0 * fours / runs).ToString("0.0") + "%");

        // Retuning
        var two = WithBonuses(EquipmentSlots.Head, 2, random);
        var twoBonuses = RebirthArmorBonusStorage.Read(two);
        Check("retuning with every row on Keep refused", !Apply("kitRetuning", two, Rows(RebirthArmorBonusRowAction.Keep, RebirthArmorBonusRowAction.Keep), 0f, random, out result, out fail) && fail == "xuiEnchantRetuneNothing", fail);
        bool rerollOk = true;
        for (int i = 0; i < 50 && rerollOk; i++)
            rerollOk = Apply("kitRetuning", two, Rows(RebirthArmorBonusRowAction.Keep, RebirthArmorBonusRowAction.Reroll), 0f, random, out result, out fail)
                && result.Length == 2 && result[0].Definition.Id == twoBonuses[0].Definition.Id && result[0].Value == twoBonuses[0].Value && Key(result[1]) == Key(twoBonuses[1]);
        Check("retuning keep + reroll keeps the first and the second's exact bonus", rerollOk, fail);
        var target = RebirthArmorBonusTree.Categories(EquipmentSlots.Head).SelectMany(c => c.Effects).Where(e => e.FamilyId != twoBonuses[0].Definition.FamilyId && e.Subjects.Count == 0).SelectMany(e => e.Exacts).First();
        var replace = new RebirthArmorBonusKitRequest().Row(RebirthArmorBonusRowAction.Keep).Row(RebirthArmorBonusRowAction.Replace, RebirthArmorBonusFocus.ForExact(target.Key));
        Check("retuning replace with an exact focus", Apply("kitRetuning", two, replace, 30f, random, out result, out fail)
            && result.Length == 2 && result[0].Definition.Id == twoBonuses[0].Definition.Id && Key(result[1]) == target.Key, fail);
        Check("retuning without choices replaces both (old behaviour)", Apply("kitRetuning", two, null, 0f, random, out result, out fail)
            && result.Length == 2 && !result.Any(b => twoBonuses.Any(o => o.Definition.Id == b.Definition.Id)), fail);

        // Breakdown
        var three = WithBonuses(EquipmentSlots.Chest, 3, random);
        var threeBonuses = RebirthArmorBonusStorage.Read(three);
        Check("breakdown keeps the marked bonuses", Apply("kitBreakdown", three, Rows(RebirthArmorBonusRowAction.Keep, RebirthArmorBonusRowAction.Remove, RebirthArmorBonusRowAction.Keep), 0f, random, out result, out fail)
            && result.Length == 2 && result[0].Definition.Id == threeBonuses[0].Definition.Id && result[1].Definition.Id == threeBonuses[2].Definition.Id, fail);
        Check("breakdown with nothing marked refused", !Apply("kitBreakdown", three, Rows(RebirthArmorBonusRowAction.Keep, RebirthArmorBonusRowAction.Keep, RebirthArmorBonusRowAction.Keep), 0f, random, out result, out fail) && fail == "xuiEnchantBreakdownNothing", fail);
        Check("breakdown without choices removes all (old behaviour)", Apply("kitBreakdown", three, null, 0f, random, out result, out fail) && result.Length == 0, fail);
        Check("breakdown on one bonus removes it", Apply("kitBreakdown", WithBonuses(EquipmentSlots.Feet, 1, random), null, 0f, random, out result, out fail) && result.Length == 0, fail);

        // Network form
        var trip = new RebirthArmorBonusKitRequest { Kit = "kitRetuning", ItemBonuses = "a,b" }.Add(RebirthArmorBonusFocus.ForCategory(melee)).Row(RebirthArmorBonusRowAction.Replace, RebirthArmorBonusFocus.ForSubject(subject.Key));
        var stream = new System.IO.MemoryStream();
        trip.Write(new System.IO.BinaryWriter(stream));
        stream.Position = 0;
        var back = RebirthArmorBonusKitRequest.Read(new System.IO.BinaryReader(stream));
        Check("request survives the network form", back != null && back.ToString() == trip.ToString() && back.ItemBonuses == trip.ItemBonuses, back?.ToString() ?? "null");

        say("rbbonus selftest: " + passed + " passed, " + failed + " failed");
    }
}
