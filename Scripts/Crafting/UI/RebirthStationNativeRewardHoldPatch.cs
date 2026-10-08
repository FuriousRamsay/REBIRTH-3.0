using HarmonyLib;

// Generic receipts remain intact until authoritative terminal/reward settlement.
// A mixed batch is held together; ordinary batches retain native behavior.
[HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.CheckForCraftComplete))]
internal static class RebirthStationNativeRewardHoldPatch
{
    private static bool Prefix(TileEntityWorkstation __instance)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        return RebirthStationCompletionReceipt.CanUseNativeRewards(__instance?.CraftCompleteList);
    }
}