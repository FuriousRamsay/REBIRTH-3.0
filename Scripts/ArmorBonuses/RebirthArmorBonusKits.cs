using System;
using System.Collections.Generic;
using System.Linq;
using Random = System.Random;

#nullable disable

/// <summary>The six 2.6 kits. Each returns false with a text key and leaves the item unchanged when it cannot apply.</summary>
public static class RebirthArmorBonusKits
{
    private static readonly RebirthArmorRolledBonus[] NoBonuses = new RebirthArmorRolledBonus[0];

    public static bool IsKit(ItemValue kit)
    {
        switch (kit?.ItemClass?.GetItemName())
        {
            case "kitSurvivorUpgrade":
            case "kitAdjustment":
            case "kitRetuning":
            case "kitComprehensiveUpgrade":
            case "kitEliteReinforcement":
            case "kitBreakdown":
                return true;
            default:
                return false;
        }
    }

    public static bool TryGetSlot(ItemValue item, out EquipmentSlots slot)
    {
        slot = EquipmentSlots.None;
        var armor = item?.ItemClass as ItemClassArmor;
        if (armor == null) return false;
        switch (armor.EquipSlot)
        {
            case EquipmentSlots.Head:
            case EquipmentSlots.Chest:
            case EquipmentSlots.Hands:
            case EquipmentSlots.Feet:
                slot = armor.EquipSlot;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Kit level for the rarity odds: Survivor 1, Adjustment and Retuning 2, Comprehensive and Elite 3 (2.6).</summary>
    public static int LevelOf(string kitName)
    {
        switch (kitName)
        {
            case "kitSurvivorUpgrade": return 1;
            case "kitAdjustment":
            case "kitRetuning": return 2;
            case "kitComprehensiveUpgrade":
            case "kitEliteReinforcement": return 3;
            default: return 0;
        }
    }

    /// <summary>Kit requirements on the number of bonuses already on the item (unchanged from 2.6).</summary>
    public static bool CheckRequirement(string kitName, int count, out string failKey)
    {
        failKey = null;
        switch (kitName)
        {
            case "kitSurvivorUpgrade": if (count > 0) failKey = "aeEnchantKitNeedsNoBonuses"; break;
            case "kitAdjustment": if (count != 1) failKey = count == 0 ? "aeEnchantAdjustmentNeedsOne" : "aeEnchantAdjustmentExactOne"; break;
            case "kitRetuning": if (count == 0) failKey = "aeEnchantRetuningNeedsBonus"; else if (count > 2) failKey = "aeEnchantRetuningOneOrTwo"; break;
            case "kitComprehensiveUpgrade": if (count > 0) failKey = "aeEnchantComprehensiveNeedsNone"; break;
            case "kitEliteReinforcement": if (count != 2) failKey = "aeEnchantEliteNeedsTwo"; break;
            case "kitBreakdown": if (count == 0) failKey = "aeEnchantBreakdownNeedsBonus"; break;
            default: failKey = "aeEnchantInvalidKit"; break;
        }
        return failKey == null;
    }

    /// <summary>How many new-bonus rows the kit has (the 25% extra bonus is never a row).</summary>
    public static int AddRowsOf(string kitName)
    {
        switch (kitName)
        {
            case "kitSurvivorUpgrade":
            case "kitAdjustment":
            case "kitEliteReinforcement": return 1;
            case "kitComprehensiveUpgrade": return 3;
            default: return 0;
        }
    }

    /// <summary>Applies a kit with no focus (the behaviour before the focus system).</summary>
    public static bool TryApply(ItemValue kit, ItemValue item, Random random, out ItemValue result, out string failKey)
        => TryApply(kit, item, null, 0f, random, out result, out failKey);

    /// <summary>
    /// Applies a kit to a copy of the item following the player's request (spec section 6). The server calls this, so
    /// rolls are authoritative. <paramref name="tailoring"/> is the player's effective Tailoring, checked against each
    /// focus depth. On failure the item is unchanged and <paramref name="failKey"/> names the reason.
    /// </summary>
    public static bool TryApply(ItemValue kit, ItemValue item, RebirthArmorBonusKitRequest request, float tailoring, Random random, out ItemValue result, out string failKey)
    {
        result = null;
        failKey = null;
        if (!IsKit(kit)) { failKey = "aeEnchantInvalidKit"; return false; }
        EquipmentSlots slot;
        if (item == null || item.IsEmpty() || !TryGetSlot(item, out slot)) { failKey = "aeEnchantItemOnly"; return false; }

        string kitName = kit.ItemClass.GetItemName();
        request = request ?? new RebirthArmorBonusKitRequest();
        var current = RebirthArmorBonusStorage.Read(item);
        int count = current.Length, quality = item.Quality, level = LevelOf(kitName);

        // A request made for another kit or another state of the item is refused rather than guessed at.
        if ((!string.IsNullOrEmpty(request.Kit) && request.Kit != kitName) ||
            (request.ItemBonuses != null && request.ItemBonuses != RebirthArmorBonusKitRequest.BonusSignature(item)))
        { failKey = "xuiEnchantRequestStale"; return false; }

        if (!CheckRequirement(kitName, count, out failKey)) return false;

        // The shape of the request must fit the kit.
        bool usesRows = kitName == "kitRetuning" || kitName == "kitBreakdown";
        if (request.Adds.Count > AddRowsOf(kitName) || (!usesRows && request.Rows.Count > 0) || (usesRows && request.Rows.Count != 0 && request.Rows.Count != count))
        { failKey = "xuiEnchantRequestInvalid"; return false; }

        // Every focus: right armor type, enough Tailoring, and no family clash with what stays on the item.
        var focuses = new List<RebirthArmorBonusFocus>(request.Adds);
        for (int i = 0; i < request.Rows.Count; i++) if (request.Rows[i] == RebirthArmorBonusRowAction.Replace) focuses.Add(request.RowFocus[i]);
        foreach (var focus in focuses)
        {
            if (focus.Depth == RebirthArmorBonusFocusDepth.Any) continue;
            if (RebirthArmorBonusTree.Pool(slot, focus).Count == 0) { failKey = "xuiEnchantFocusWrongSlot"; return false; }
            // Combination families are picked by subject; an exact combination would fix its rarity (owner 2026-11-08).
            if (focus.Depth == RebirthArmorBonusFocusDepth.Exact && RebirthArmorBonusTree.IsComboFamily(FamilyOf(focus))) { failKey = "xuiEnchantRequestInvalid"; return false; }
            if (tailoring < focus.RequiredTailoring) { failKey = focus.RequiredTailoring >= 25 ? "xuiEnchantFocusNeedsTailoring25" : "xuiEnchantFocusNeedsTailoring10"; return false; }
        }

        var bonuses = new List<RebirthArmorRolledBonus>();
        switch (kitName)
        {
            case "kitBreakdown":
            {
                // One bonus is removed directly; otherwise remove the marked ones (no rows = remove all, as before).
                if (count > 1 && request.Rows.Count == count)
                {
                    if (request.Rows.Any(r => r != RebirthArmorBonusRowAction.Keep && r != RebirthArmorBonusRowAction.Remove)) { failKey = "xuiEnchantRequestInvalid"; return false; }
                    if (!request.Rows.Contains(RebirthArmorBonusRowAction.Remove)) { failKey = "xuiEnchantBreakdownNothing"; return false; }
                    for (int i = 0; i < count; i++) if (request.Rows[i] == RebirthArmorBonusRowAction.Keep) bonuses.Add(current[i]);
                }
                result = item.Clone();
                if (bonuses.Count == 0) RebirthArmorBonusStorage.Clear(result); else RebirthArmorBonusStorage.Write(result, bonuses);
                return true;
            }
            case "kitRetuning":
            {
                // No rows = replace every bonus at random, excluding the old ones (the behaviour before the focus system).
                var actions = request.Rows.Count == count ? request.Rows : Enumerable.Repeat(RebirthArmorBonusRowAction.Replace, count).ToList();
                var rowFocus = request.Rows.Count == count ? request.RowFocus : Enumerable.Repeat(RebirthArmorBonusFocus.Any, count).ToList();
                if (actions.Any(r => r == RebirthArmorBonusRowAction.Remove)) { failKey = "xuiEnchantRequestInvalid"; return false; }
                if (actions.All(r => r == RebirthArmorBonusRowAction.Keep)) { failKey = "xuiEnchantRetuneNothing"; return false; }
                var slots = new RebirthArmorRolledBonus?[count];
                for (int i = 0; i < count; i++)
                {
                    if (actions[i] == RebirthArmorBonusRowAction.Keep) slots[i] = current[i];
                    else if (actions[i] == RebirthArmorBonusRowAction.Reroll) slots[i] = RebirthArmorBonusRoller.RerollSame(current[i], level, quality, random);
                }
                var staying = slots.Where(s => s.HasValue).Select(s => s.Value).ToList();
                var replaceRows = Enumerable.Range(0, count).Where(i => actions[i] == RebirthArmorBonusRowAction.Replace).ToList();
                if (!CheckFamilies(staying, replaceRows.Select(i => rowFocus[i]), out failKey)) return false;
                if (!TryRollRows(slot, staying, replaceRows.Select(i => rowFocus[i]).ToList(), level, quality, random, current.Select(b => b.Definition.Id).ToList(), out var replaced))
                { failKey = "aeEnchantNoEligibleBonus"; return false; }
                for (int r = 0; r < replaceRows.Count; r++) slots[replaceRows[r]] = replaced[r];
                bonuses.AddRange(slots.Select(s => s.Value));
                break;
            }
            default:
            {
                // Adding kits: kept bonuses, then one roll per row (open rows are random), then the 25% extra for level 3.
                var rows = new List<RebirthArmorBonusFocus>(request.Adds);
                while (rows.Count < AddRowsOf(kitName)) rows.Add(RebirthArmorBonusFocus.Any);
                var kept = current.ToList();
                if (!CheckFamilies(kept, rows, out failKey)) return false;
                if (!TryRollRows(slot, kept, rows, level, quality, random, null, out var added)) { failKey = "aeEnchantNoEligibleBonus"; return false; }
                bonuses.AddRange(kept);
                bonuses.AddRange(added);
                if (level == 3 && RebirthArmorBonusRoller.RollTargetCount(random) == 4)
                    bonuses = RebirthArmorBonusRoller.Roll(slot, bonuses, 4, level, quality, random);
                break;
            }
        }
        result = item.Clone();
        RebirthArmorBonusStorage.Write(result, bonuses);
        return true;
    }

    /// <summary>The family an effect or exact focus is fixed to (null for Any and Category).</summary>
    public static string FamilyOf(RebirthArmorBonusFocus focus)
    {
        if (focus.Depth == RebirthArmorBonusFocusDepth.Effect) return focus.FamilyId;
        if (focus.Depth == RebirthArmorBonusFocusDepth.Exact && RebirthArmorBonusTree.TryGetExact(focus.ExactKey, out var exact)) return exact.FamilyId;
        if (focus.Depth == RebirthArmorBonusFocusDepth.Subject && RebirthArmorBonusTree.TryGetSubject(focus.ExactKey, out var subject)) return subject.Base.FamilyId;
        return null;
    }

    /// <summary>A family appears once per item: a fixed-family focus may not repeat a staying bonus's family or another row's.</summary>
    private static bool CheckFamilies(IEnumerable<RebirthArmorRolledBonus> staying, IEnumerable<RebirthArmorBonusFocus> rows, out string failKey)
    {
        failKey = null;
        // Only the same bonus twice is refused (owner 2026-11-08); different bonuses of one family may sit together.
        var taken = new HashSet<string>(staying.Select(b => RebirthArmorBonusTree.ExactKey(b.Definition.Id)), StringComparer.Ordinal);
        foreach (var focus in rows)
        {
            if (focus.Depth != RebirthArmorBonusFocusDepth.Exact) continue;
            if (!taken.Add(focus.ExactKey)) { failKey = "xuiEnchantFocusFamilyTaken"; return false; }
        }
        return true;
    }

    /// <summary>
    /// One bonus per row. The narrowest focuses roll first so a wide row cannot take the only family a narrow row has;
    /// if rows still collide (families shared between categories), the whole set is drawn again, up to 20 times.
    /// </summary>
    private static bool TryRollRows(EquipmentSlots slot, List<RebirthArmorRolledBonus> staying, List<RebirthArmorBonusFocus> rows, int level, int quality, Random random, List<string> excludedIds, out RebirthArmorRolledBonus[] rolled)
    {
        rolled = new RebirthArmorRolledBonus[rows.Count];
        var order = Enumerable.Range(0, rows.Count)
            .OrderBy(i => rows[i].Depth == RebirthArmorBonusFocusDepth.Any ? int.MaxValue : RebirthArmorBonusTree.Pool(slot, rows[i]).Count).ToList();
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var onItem = new List<RebirthArmorRolledBonus>(staying);
            bool ok = true;
            foreach (int i in order)
            {
                var next = RebirthArmorBonusRoller.Roll(slot, onItem, onItem.Count + 1, level, quality, random, rows[i], excludedIds);
                if (next.Count <= onItem.Count) { ok = false; break; }
                rolled[i] = next[next.Count - 1];
                onItem = next;
            }
            if (ok) return true;
        }
        return false;
    }
}
