using HarmonyLib;

// Older campers were saved before canHaveCosmetic was included in their tags.
// Native ReadData restores a zero-length saved array as null, regardless of new tags.
// Add only the missing empty slot; loading never grants or rerolls a dye.
[HarmonyPatch(typeof(ItemValue), "ReadData")]
internal static class RebirthCamperCosmeticMigration
{
    private static void Postfix(ItemValue __instance)
    {
#if REBIRTH_CAMPER_DIAGNOSTICS
        if (GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest") return;
#endif
        if (__instance == null || __instance.IsEmpty()) return;
        ItemClass item = __instance.ItemClass;
        if (item == null || item.GetItemName() != "rebirthCamper80sPlaceable"
            || !item.Properties.GetBool("RebirthCamperRandomInitialDye")) return;
        if (__instance.cosmeticMods == null || __instance.cosmeticMods.Length == 0)
            __instance.cosmeticMods = new[] { ItemValue.None };
    }
}
