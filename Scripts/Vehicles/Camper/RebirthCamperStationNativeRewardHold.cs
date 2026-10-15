using HarmonyLib;

// Native completion also mutates craft counters, quests and level XP. Keep the
// original reserved records until mobile settlement authorizes those effects,
// regardless of Survivor mode. Ordinary terrain completions are unaffected.
[HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.CheckForCraftComplete))]
internal static class RebirthCamperStationNativeRewardHold
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(TileEntityWorkstation __instance)
    {
        if(!(__instance is RebirthCamperStationState station))return true;
        return !RebirthCamperStationCompletionCallsite.IsHeld(station)&&
            RebirthStationCompletionReceipt.CanUseNativeRewards(station.CraftCompleteList);
    }
}
