#nullable disable

public static class AdvancedFarmingWaterQueryService
{
    public static void InvalidateAll(string reason)
    {
        // Compatibility shim for older callers. The water system no longer caches mutable
        // water values, so water-count changes do not require invalidation.
        AdvancedFarmingPerfSnapshotService.RecordWaterCacheInvalidation();
    }

    public static int CheckForWater(WorldBase world, Vector3i plantPos, string blockName, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        return AdvancedFarmingWaterProviderRegistry.CheckForWater(world, plantPos, blockName, blockRange, blockHeightMin, blockHeightMax);
    }

    public static string BuildStatus()
    {
        return AdvancedFarmingWaterProviderRegistry.BuildStatus();
    }
}
