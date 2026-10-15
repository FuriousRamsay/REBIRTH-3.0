using System;

// Native cosmetic slot is the selector. No custom state, metadata or request
// transport: native item modifications already save and replicate this item.
public static class RebirthCamperPattern
{
    public const string None = "none";
    public const string Disco = "disco";
    public const string DiscoModifier = "modVehiclePatternDisco";

    public static string Current(EntityVehicle vehicle)
    {
        if (vehicle?.vehicle == null || !RebirthCamperRadial.IsCamper(vehicle)) return None;
        var item = vehicle.vehicle.GetUpdatedItemValue();
        if (item == null) return None;
        for (int slot = 0; slot < item.CosmeticModCount; slot++)
        {
            var cosmetic = item.GetCosmeticMod(slot);
            if (cosmetic != null && !cosmetic.IsEmpty()
                && string.Equals(cosmetic.ItemClass?.GetItemName(), DiscoModifier, StringComparison.Ordinal)) return Disco;
        }
        return None;
    }
}
