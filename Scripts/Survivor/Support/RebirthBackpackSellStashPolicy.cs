using System;

// Extra sale storage follows backpack tiers, independently of normal bag capacity.
public static class RebirthBackpackSellStashPolicy
{
    public const int SlotsPerTier = 10;
    public const int MaxSlots = 80;
    public static int CapacityForBackpack(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) ||
            !RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(itemId, out var profile) ||
            profile == null || !string.Equals(profile.Kind, "survivor_gear", StringComparison.Ordinal) ||
            !string.Equals(profile.GearSlotId, RebirthSurvivorGearService.BackpackSlotId, StringComparison.Ordinal) ||
            profile.GearBagSlotBonus <= 0 || profile.GearBagSlotBonus % 11 != 0) return 0;
        return Math.Min(MaxSlots, profile.GearBagSlotBonus / 11 * SlotsPerTier);
    }
    // No type/category whitelist: every valid native item can be stored.
    public static bool IsStorableItem(ItemValue item)
        => item != null && !item.IsEmpty() && item.ItemClass != null;
}