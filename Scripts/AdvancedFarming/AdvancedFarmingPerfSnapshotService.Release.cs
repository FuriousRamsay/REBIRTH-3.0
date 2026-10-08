#if !DEBUG
#nullable disable

/// <summary>
/// Release compatibility facade. Advanced Farming performance sampling is compiled only in Debug builds.
/// Every recording method is a no-op and no counters, strings, timers, scans, or allocations are retained.
/// </summary>
public enum AdvancedFarmingManagedMemoryPhase
{
    ProcessArea = 0,
    AreaMaintenance = 1,
    LightEvaluate = 2,
    ThermalEvaluate = 3,
    WaterMaintenance = 4
}

public static class AdvancedFarmingPerfSnapshotService
{
public struct SampleSnapshot
        {
            public long StopwatchTicks;
            public long AreaCalls;
            public long TotalElapsedStopwatchTicks;
            public long MaxElapsedStopwatchTicks;
            public long TotalProviders;
            public long TotalPlots;
            public long TotalPlants;
            public long AnchorOnlyCalls;
            public long AreaMaintenanceCalls;
            public long AnchorOnlyElapsedStopwatchTicks;
            public long AreaMaintenanceElapsedStopwatchTicks;
            public long AnchorOnlyMaxElapsedStopwatchTicks;
            public long AreaMaintenanceMaxElapsedStopwatchTicks;
            public long PlantSyncs;
            public long AnchorPlantCalls;
            public long AnchorNoPlantCalls;
            public long AnchorNullBlockCalls;
            public long HeatScanCalls;
            public long HeatScanPositions;
            public long HeatScanTileEntityLookups;
            public long HeatScanHits;
            public long HeatCacheQueries;
            public long HeatCacheHits;
            public long HeatCacheMisses;
            public long HeatCacheInvalidations;
            public long HeatRegistryQueries;
            public long HeatRegistryHits;
            public long HeatRegistryMisses;
            public long HeatRegistryFallbackScans;
            public long HeatRegistryObservedOn;
            public long HeatRegistryObservedOff;
            public long WaterScanCalls;
            public long WaterScanPositions;
            public long WaterScanTileEntityLookups;
            public long WaterScanHits;
            public long WaterCacheQueries;
            public long WaterCacheHits;
            public long WaterCacheMisses;
            public long WaterCacheInvalidations;
            public long WaterRegistryQueries;
            public long WaterRegistryCandidates;
            public long WaterRegistryTileEntityLookups;
            public long WaterRegistryHits;
            public long WaterRegistryMaintenanceQueries;
            public long WaterRegistryMaintenanceCandidates;
            public long WaterRegistryMaintenanceTileEntityLookups;
            public long WaterRegistryMaintenanceHits;
            public long WaterRegistryConsumptionQueries;
            public long WaterRegistryConsumptionCandidates;
            public long WaterRegistryConsumptionTileEntityLookups;
            public long WaterRegistryConsumptionHits;
            public long WaterRegistryPrunes;
            public long LightExposureCacheQueries;
            public long LightExposureCacheHits;
            public long LightExposureEvaluations;
            public long LightExposureSearches;
            public long LightExposureNodes;
            public long LightExposureTransparentCandidates;
            public long LightExposureOpenResults;
            public long SunRefreshQueued;
            public long SunRefreshCoalesced;
            public long SunRefreshRebuilds;
            public long SunRefreshCells;
            public long SunRefreshElapsedStopwatchTicks;
            public long SunRefreshMaxElapsedStopwatchTicks;
            public long VisualRefreshQueued;
            public long ThermalExposureQueries;
            public long ThermalExposureCacheHits;
            public long ThermalExposureSearches;
            public long ThermalExposureNodes;
            public long ThermalExposureOpenResults;
            public long ThermalExposureClosedResults;
            public long LightEvaluateCalls;
            public long LightEvaluateElapsedStopwatchTicks;
            public long LightEvaluateMaxStopwatchTicks;
            public long ThermalEvaluateCalls;
            public long ThermalEvaluateElapsedStopwatchTicks;
            public long ThermalEvaluateMaxStopwatchTicks;
            public long AreaMaintenancePhaseCalls;
            public long AreaMaintenancePhaseElapsedStopwatchTicks;
            public long AreaMaintenancePhaseMaxStopwatchTicks;
            public long ActiveBlockLightPrecheckQueries;
            public long ActiveBlockLightPrecheckPasses;
            public long ExposureInvalidationBlockChanges;
            public long ExposureInvalidationSkippedPlantLike;
            public long ExposureInvalidationLightClears;
            public long ExposureInvalidationThermalClears;
            public long ExposureInvalidationSunRefreshQueues;
            public long LightProcessorRefreshSunlightCalls;
            public long LightProcessorRefreshSunlightElapsedStopwatchTicks;
            public long LightProcessorRefreshSunlightMaxStopwatchTicks;
            public long LightProcessorRefreshLocalSunCalls;
            public long LightProcessorRefreshLocalSunElapsedStopwatchTicks;
            public long LightProcessorRefreshLocalSunMaxStopwatchTicks;
            public long LightProcessorRefreshLocalBlockCalls;
            public long LightProcessorRefreshLocalBlockElapsedStopwatchTicks;
            public long LightProcessorRefreshLocalBlockMaxStopwatchTicks;
            public long LightProcessorSpreadSunCalls;
            public long LightProcessorSpreadSunElapsedStopwatchTicks;
            public long LightProcessorSpreadSunMaxStopwatchTicks;
            public long LightProcessorSpreadBlockCalls;
            public long LightProcessorSpreadBlockElapsedStopwatchTicks;
            public long LightProcessorSpreadBlockMaxStopwatchTicks;
            public long LightProcessorUnspreadSunCalls;
            public long LightProcessorUnspreadSunElapsedStopwatchTicks;
            public long LightProcessorUnspreadSunMaxStopwatchTicks;
            public long LightProcessorUnspreadBlockCalls;
            public long LightProcessorUnspreadBlockElapsedStopwatchTicks;
            public long LightProcessorUnspreadBlockMaxStopwatchTicks;
            public long WaterRegistryResolveCacheHits;
            public long ProcessAreaManagedPositiveBytes;
            public long ProcessAreaManagedNegativeBytes;
            public long AreaMaintenanceManagedPositiveBytes;
            public long AreaMaintenanceManagedNegativeBytes;
            public long LightEvaluateManagedPositiveBytes;
            public long LightEvaluateManagedNegativeBytes;
            public long ThermalEvaluateManagedPositiveBytes;
            public long ThermalEvaluateManagedNegativeBytes;
            public long WaterMaintenanceManagedPositiveBytes;
            public long WaterMaintenanceManagedNegativeBytes;
        }

    public static bool SamplingEnabled { get { return false; } }
    private static string Disabled() { return "[AdvancedFarming] Performance diagnostics require a Debug build."; }
    public static string SetSampling(bool enabled) { return Disabled(); }
    public static void ResetSampling() { }
    public static void RecordMissingPlantTileEntityWakeSkip(BlockValue blockValue) { }
    public static void RecordProcessAreaAnchor(BlockValue blockValue, bool hasPlantTileEntity) { }
    public static void RecordAreaProcess(long elapsedStopwatchTicks, int providers, int plots, int plants, bool areaMaintenanceRan) { }
    public static void RecordWaterScan(long positions, long tileEntityLookups, bool hit) { }
    public static void RecordWaterCacheQuery(bool hit) { }
    public static void RecordWaterCacheInvalidation() { }
    public static void RecordWaterRegistryQuery(long candidates, long tileEntityLookups, bool hit) { }
    public static void RecordWaterRegistryMaintenanceCollect(long candidates, long tileEntityLookups, bool hit) { }
    public static void RecordWaterRegistryConsumption(long candidates, long tileEntityLookups, bool hit) { }
    public static void RecordWaterRegistryPrune() { }
    public static void RecordWaterRegistryResolveCacheHit() { }
    public static void RecordManagedMemoryDelta(AdvancedFarmingManagedMemoryPhase phase, long deltaBytes) { }
    public static void RecordLightExposureCacheQuery(bool hit) { }
    public static void RecordLightExposureEvaluation(bool usedSearch, long nodesVisited, long transparentCandidates, bool open) { }
    public static void RecordHeatRegistryQuery() { }
    public static void RecordHeatRegistryHit() { }
    public static void RecordHeatRegistryMiss() { }
    public static void RecordHeatRegistryFallbackScan() { }
    public static void RecordHeatSourceRegistryObserve(bool burning) { }
    public static void RecordHeatCacheQuery(bool hit) { }
    public static void RecordHeatCacheInvalidation() { }
    public static void RecordHeatScan(long scannedPositions, long tileEntityLookups, bool hit) { }
    public static void RecordPlantSync() { }
    public static void RecordSunRefreshQueued(bool coalesced) { }
    public static void RecordSunRebuild(long elapsedStopwatchTicks, int cells) { }
    public static void RecordVisualRefreshQueued() { }
    public static void RecordThermalExposureCacheQuery(bool hit) { }
    public static void RecordThermalExposureSearch(int nodesVisited, bool openToExterior) { }
    public static void RecordLightEvaluate(long elapsedStopwatchTicks) { }
    public static void RecordThermalEvaluate(long elapsedStopwatchTicks) { }
    public static void RecordAreaMaintenancePhase(long elapsedStopwatchTicks) { }
    public static long LightEvaluateElapsedStopwatchTicks { get { return 0L; } }
    public static long ThermalEvaluateElapsedStopwatchTicks { get { return 0L; } }
    public static void RecordActiveBlockLightPrecheck(bool pass) { }
    public static void RecordExposureInvalidationBlockChange(bool skippedPlantLike, bool clearedLight, bool clearedThermal, bool queuedSunRefresh) { }
    public static void RecordLightProcessorRefreshSunlight(long elapsedStopwatchTicks) { }
    public static void RecordLightProcessorRefreshLocal(Chunk.LIGHT_TYPE type, long elapsedStopwatchTicks) { }
    public static void RecordLightProcessorSpread(Chunk.LIGHT_TYPE type, long elapsedStopwatchTicks) { }
    public static void RecordLightProcessorUnspread(Chunk.LIGHT_TYPE type, long elapsedStopwatchTicks) { }
    public static SampleSnapshot GetSnapshot() { return new SampleSnapshot(); }
    public static string BuildSamplingDeltaReport(string label, SampleSnapshot start, SampleSnapshot end) { return Disabled(); }
    public static string BuildAnchorDiagnosticsReport() { return Disabled(); }
    public static string BuildSamplingReport() { return Disabled(); }
    public static string BuildReport(WorldBase world, Vector3i center, int radius) { return Disabled(); }
}
#endif
