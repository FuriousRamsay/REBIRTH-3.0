using System;
using System.IO;

// Native decoder for the journal's resolver callback. No fallback to default gear:
// the saved ItemValue must resolve to the requested REBIRTH-owned equipment slot.
public static class RebirthGearItemCodec
{
    public static string ResolveGearItemId(string encoded, string slotId)
    {
        if (slotId != "backpack" && slotId != "belt" && slotId != "support" && slotId != "walkman") return null;
        ItemValue value;
        if (!TryDecode(encoded, out value)) return null;
        string itemId = value.ItemClass.GetItemName();
        RebirthTraitSupportProfileDefinition profile;
        if (!RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(itemId, out profile)
            || profile == null || !string.Equals(profile.Kind, "survivor_gear", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(profile.GearSlotId, slotId, StringComparison.Ordinal)) return null;
        return itemId;
    }

    public static bool TryDecode(string encoded, out ItemValue value)
    {
        return RebirthNativeItemCodec.TryDecode(encoded, out value);
    }
}
