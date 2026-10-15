using HarmonyLib;

// Native terrain placement converts ItemValue to BlockValue and creates a new
// tile entity; it does not adopt the portable camper snapshot. Stop before that
// conversion and before the delayed inventory debit, retaining malformed payloads
// as well as valid ones. Camper installation uses its separate transfer path.
[HarmonyPatch(typeof(ItemActionPlaceAsBlock), nameof(ItemActionPlaceAsBlock.ExecuteAction))]
public static class RebirthCamperPortablePlacementGuard
{
    private static bool Prefix(ItemActionData __0, bool __1)
    {
        ItemValue item = __0?.invData?.itemValue;
        if (item?.Metadata == null || !item.Metadata.ContainsKey(RebirthCamperPortableStation.MetadataKey)) return true;
        // Native PlaceAsBlock executes on release. Do not show a second prompt
        // for the press callback, but block both callbacks for portable items.
        if (__1 && __0.invData.holdingEntity is EntityPlayerLocal player)
            GameManager.ShowTooltip(player, Localization.Get("xuiRebirthCamperPortableInstall"));
        return false;
    }
}
