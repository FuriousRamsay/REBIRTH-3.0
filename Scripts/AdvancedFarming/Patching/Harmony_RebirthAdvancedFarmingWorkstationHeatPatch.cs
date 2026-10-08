#nullable disable

public static class Harmony_RebirthAdvancedFarmingWorkstationHeatPatch
{
    public static void Postfix(TileEntityWorkstation __instance)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (__instance == null)
            return;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;

        Vector3i pos = __instance.ToWorldPos();
        if (!AdvancedFarmingActiveAreaRegistry.IsAreaActive(pos, 32, 32))
            return;

        AdvancedFarmingHeatQueryService.ObserveWorkstationAt(
            world, pos, __instance.isBurning);
    }
}
