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

    /// <summary>Applies a kit to a copy of the item. The server calls this, so rolls are authoritative.</summary>
    public static bool TryApply(ItemValue kit, ItemValue item, Random random, out ItemValue result, out string failKey)
    {
        result = null;
        failKey = null;
        if (!IsKit(kit)) { failKey = "aeEnchantInvalidKit"; return false; }
        EquipmentSlots slot;
        if (item == null || item.IsEmpty() || !TryGetSlot(item, out slot)) { failKey = "aeEnchantItemOnly"; return false; }

        var current = RebirthArmorBonusStorage.Read(item);
        int count = current.Length, quality = item.Quality;
        List<RebirthArmorRolledBonus> rolled;
        switch (kit.ItemClass.GetItemName())
        {
            case "kitSurvivorUpgrade":
                if (count > 0) { failKey = "aeEnchantKitNeedsNoBonuses"; return false; }
                rolled = RebirthArmorBonusRoller.Roll(slot, NoBonuses, 1, 1, quality, random);
                break;
            case "kitAdjustment":
                if (count != 1) { failKey = count == 0 ? "aeEnchantAdjustmentNeedsOne" : "aeEnchantAdjustmentExactOne"; return false; }
                rolled = RebirthArmorBonusRoller.Roll(slot, current, 2, 2, quality, random);
                break;
            case "kitRetuning":
                if (count == 0) { failKey = "aeEnchantRetuningNeedsBonus"; return false; }
                if (count > 2) { failKey = "aeEnchantRetuningOneOrTwo"; return false; }
                rolled = RebirthArmorBonusRoller.Roll(slot, NoBonuses, count, 2, quality, random, current.Select(b => b.Definition.Id));
                break;
            case "kitComprehensiveUpgrade":
                if (count > 0) { failKey = "aeEnchantComprehensiveNeedsNone"; return false; }
                rolled = RebirthArmorBonusRoller.Roll(slot, NoBonuses, RebirthArmorBonusRoller.RollTargetCount(random), 3, quality, random);
                break;
            case "kitEliteReinforcement":
                if (count != 2) { failKey = "aeEnchantEliteNeedsTwo"; return false; }
                rolled = RebirthArmorBonusRoller.Roll(slot, current, RebirthArmorBonusRoller.RollTargetCount(random), 3, quality, random);
                break;
            default: // kitBreakdown
                if (count == 0) { failKey = "aeEnchantBreakdownNeedsBonus"; return false; }
                result = item.Clone();
                RebirthArmorBonusStorage.Clear(result);
                return true;
        }
        if (rolled.Count == 0) { failKey = "aeEnchantNoEligibleBonus"; return false; }
        result = item.Clone();
        RebirthArmorBonusStorage.Write(result, rolled);
        return true;
    }
}
