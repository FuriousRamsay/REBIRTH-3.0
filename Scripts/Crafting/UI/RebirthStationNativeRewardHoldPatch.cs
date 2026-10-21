using HarmonyLib;

// Only REBIRTH-marked receipts need the custom settlement path. Ordinary
// workstation completion and rewards remain with the native game flow.
[HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.CheckForCraftComplete))]
internal static class RebirthStationNativeRewardHoldPatch
{
    private static bool Prefix(TileEntityWorkstation __instance)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        return RebirthStationCompletionReceipt.CanUseNativeRewards(__instance?.CraftCompleteList);
    }
}
