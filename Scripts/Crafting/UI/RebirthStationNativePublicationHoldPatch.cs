using HarmonyLib;

[HarmonyPatch(typeof(TileEntityWorkstation),"HandleRecipeQueue")]
internal static class RebirthStationNativePublicationHoldPatch
{
    private static bool Prefix(TileEntityWorkstation __instance)
    {
        if (!RebirthStationObservationDispatcher.CanProcessNativeQueue(__instance)) return false;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return true;
        var queue = __instance?.Queue;
        if (queue == null || queue.Length == 0) return true;
        var active = queue[queue.Length - 1];
        return active?.Recipe == null || active.Multiplier <= 0 || active.Recipe.IsScrap ||
            RebirthStationToolAvailability.HasRequirement(__instance.Tools, active.Recipe);
    }
}