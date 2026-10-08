using HarmonyLib;

[HarmonyPatch(typeof(TileEntityWorkstation),"HandleRecipeQueue")]
internal static class RebirthStationNativePublicationHoldPatch
{
    private static bool Prefix(TileEntityWorkstation __instance)
        =>RebirthStationObservationDispatcher.CanProcessNativeQueue(__instance);
}