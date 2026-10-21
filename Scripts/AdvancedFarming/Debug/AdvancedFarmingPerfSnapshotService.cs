#if DEBUG
using System;
using System.Diagnostics;
using System.Text;

#nullable disable

/// <summary>
/// Release-safe, on-demand farming diagnostics.
/// This does not log per tick. It samples/scans only when a command asks for a final report.
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
    private static bool s_samplingEnabled;
    private static long s_resetEpoch;
    private static long s_sampleStartTicks;
    private static long s_areaCalls;
    private static long s_totalElapsedStopwatchTicks;
    private static long s_maxElapsedStopwatchTicks;
    private static long s_totalProviders;
    private static long s_totalPlots;
    private static long s_totalPlants;
    private static long s_anchorOnlyCalls;
    private static long s_areaMaintenanceCalls;
    private static long s_anchorOnlyElapsedStopwatchTicks;
    private static long s_areaMaintenanceElapsedStopwatchTicks;
    private static long s_anchorOnlyMaxElapsedStopwatchTicks;
    private static long s_areaMaintenanceMaxElapsedStopwatchTicks;
    private static long s_plantSyncs;
    private static long s_anchorPlantCalls;
    private static long s_anchorNoPlantCalls;
    private static long s_anchorNullBlockCalls;
    private static long s_missingPlantTileEntityWakeSkips;
    private static long s_heatScanCalls;
    private static long s_heatScanPositions;
    private static long s_heatScanTileEntityLookups;
    private static long s_heatScanHits;
    private static long s_heatCacheQueries;
    private static long s_heatCacheHits;
    private static long s_heatCacheMisses;
    private static long s_heatCacheInvalidations;
    private static long s_heatRegistryQueries;
    private static long s_heatRegistryHits;
    private static long s_heatRegistryMisses;
    private static long s_heatRegistryFallbackScans;
    private static long s_heatRegistryObservedOn;
    private static long s_heatRegistryObservedOff;
    private static long s_waterScanCalls;
    private static long s_waterScanPositions;
    private static long s_waterScanTileEntityLookups;
    private static long s_waterScanHits;
    private static long s_waterCacheQueries;
    private static long s_waterCacheHits;
    private static long s_waterCacheMisses;
    private static long s_waterCacheInvalidations;
    private static long s_waterRegistryQueries;
    private static long s_waterRegistryCandidates;
    private static long s_waterRegistryTileEntityLookups;
    private static long s_waterRegistryHits;
    private static long s_waterRegistryMaintenanceQueries;
    private static long s_waterRegistryMaintenanceCandidates;
    private static long s_waterRegistryMaintenanceTileEntityLookups;
    private static long s_waterRegistryMaintenanceHits;
    private static long s_waterRegistryConsumptionQueries;
    private static long s_waterRegistryConsumptionCandidates;
    private static long s_waterRegistryConsumptionTileEntityLookups;
    private static long s_waterRegistryConsumptionHits;
    private static long s_waterRegistryPrunes;
    private static long s_lightExposureCacheQueries;
    private static long s_lightExposureCacheHits;
    private static long s_lightExposureEvaluations;
    private static long s_lightExposureSearches;
    private static long s_lightExposureNodes;
    private static long s_lightExposureTransparentCandidates;
    private static long s_lightExposureOpenResults;
    private static long s_sunRefreshQueued;
    private static long s_sunRefreshCoalesced;
    private static long s_sunRefreshRebuilds;
    private static long s_sunRefreshCells;
    private static long s_sunRefreshElapsedStopwatchTicks;
    private static long s_sunRefreshMaxElapsedStopwatchTicks;
    private static long s_visualRefreshQueued;
    private static long s_thermalExposureQueries;
    private static long s_thermalExposureCacheHits;
    private static long s_thermalExposureSearches;
    private static long s_thermalExposureNodes;
    private static long s_thermalExposureOpenResults;
    private static long s_thermalExposureClosedResults;
    private static long s_lightEvaluateCalls;
    private static long s_lightEvaluateElapsedStopwatchTicks;
    private static long s_lightEvaluateMaxStopwatchTicks;
    private static long s_thermalEvaluateCalls;
    private static long s_thermalEvaluateElapsedStopwatchTicks;
    private static long s_thermalEvaluateMaxStopwatchTicks;
    private static long s_areaMaintenancePhaseCalls;
    private static long s_areaMaintenancePhaseElapsedStopwatchTicks;
    private static long s_areaMaintenancePhaseMaxStopwatchTicks;
    private static long s_activeBlockLightPrecheckQueries;
    private static long s_activeBlockLightPrecheckPasses;
    private static long s_exposureInvalidationBlockChanges;
    private static long s_exposureInvalidationSkippedPlantLike;
    private static long s_exposureInvalidationLightClears;
    private static long s_exposureInvalidationThermalClears;
    private static long s_exposureInvalidationSunRefreshQueues;
    private static long s_lightProcessorRefreshSunlightCalls;
    private static long s_lightProcessorRefreshSunlightElapsedStopwatchTicks;
    private static long s_lightProcessorRefreshSunlightMaxStopwatchTicks;
    private static long s_lightProcessorRefreshLocalSunCalls;
    private static long s_lightProcessorRefreshLocalSunElapsedStopwatchTicks;
    private static long s_lightProcessorRefreshLocalSunMaxStopwatchTicks;
    private static long s_lightProcessorRefreshLocalBlockCalls;
    private static long s_lightProcessorRefreshLocalBlockElapsedStopwatchTicks;
    private static long s_lightProcessorRefreshLocalBlockMaxStopwatchTicks;
    private static long s_lightProcessorSpreadSunCalls;
    private static long s_lightProcessorSpreadSunElapsedStopwatchTicks;
    private static long s_lightProcessorSpreadSunMaxStopwatchTicks;
    private static long s_lightProcessorSpreadBlockCalls;
    private static long s_lightProcessorSpreadBlockElapsedStopwatchTicks;
    private static long s_lightProcessorSpreadBlockMaxStopwatchTicks;
    private static long s_lightProcessorUnspreadSunCalls;
    private static long s_lightProcessorUnspreadSunElapsedStopwatchTicks;
    private static long s_lightProcessorUnspreadSunMaxStopwatchTicks;
    private static long s_lightProcessorUnspreadBlockCalls;
    private static long s_lightProcessorUnspreadBlockElapsedStopwatchTicks;
    private static long s_lightProcessorUnspreadBlockMaxStopwatchTicks;
    private static long s_waterRegistryResolveCacheHits;
    private static long s_processAreaManagedPositiveBytes;
    private static long s_processAreaManagedNegativeBytes;
    private static long s_areaMaintenanceManagedPositiveBytes;
    private static long s_areaMaintenanceManagedNegativeBytes;
    private static long s_lightEvaluateManagedPositiveBytes;
    private static long s_lightEvaluateManagedNegativeBytes;
    private static long s_thermalEvaluateManagedPositiveBytes;
    private static long s_thermalEvaluateManagedNegativeBytes;
    private static long s_waterMaintenanceManagedPositiveBytes;
    private static long s_waterMaintenanceManagedNegativeBytes;
    private static string[] s_anchorNoPlantNames = new string[8];
    private static long[] s_anchorNoPlantCounts = new long[8];


    public struct SampleSnapshot
    {
        public long ResetEpoch;
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


    public static bool SamplingEnabled { get { return s_samplingEnabled; } }
    private static long s_samplingRevision;
    public static long SamplingRevision=>System.Threading.Interlocked.Read(ref s_samplingRevision);

    public static string SetSampling(bool enabled)
    {
        System.Threading.Interlocked.Increment(ref s_samplingRevision);
        if (enabled)
        {
            ResetSampling();
            s_samplingEnabled = true;
            return "[AdvancedFarming PerfSample] enabled=true";
        }

        s_samplingEnabled = false;
        return "[AdvancedFarming PerfSample] enabled=false";
    }

    public static void ResetSampling()
    {
        System.Threading.Interlocked.Increment(ref s_samplingRevision);
        System.Threading.Interlocked.Increment(ref s_resetEpoch);
        s_sampleStartTicks = Stopwatch.GetTimestamp();
        s_areaCalls = 0L;
        s_totalElapsedStopwatchTicks = 0L;
        s_maxElapsedStopwatchTicks = 0L;
        s_totalProviders = 0L;
        s_totalPlots = 0L;
        s_totalPlants = 0L;
        s_anchorOnlyCalls = 0L;
        s_areaMaintenanceCalls = 0L;
        s_anchorOnlyElapsedStopwatchTicks = 0L;
        s_areaMaintenanceElapsedStopwatchTicks = 0L;
        s_anchorOnlyMaxElapsedStopwatchTicks = 0L;
        s_areaMaintenanceMaxElapsedStopwatchTicks = 0L;
        s_plantSyncs = 0L;
        s_anchorPlantCalls = 0L;
        s_anchorNoPlantCalls = 0L;
        s_anchorNullBlockCalls = 0L;
        s_missingPlantTileEntityWakeSkips = 0L;
        s_heatScanCalls = 0L;
        s_heatScanPositions = 0L;
        s_heatScanTileEntityLookups = 0L;
        s_heatScanHits = 0L;
        s_heatCacheQueries = 0L;
        s_heatCacheHits = 0L;
        s_heatCacheMisses = 0L;
        s_heatCacheInvalidations = 0L;
        s_heatRegistryQueries = 0L;
        s_heatRegistryHits = 0L;
        s_heatRegistryMisses = 0L;
        s_heatRegistryFallbackScans = 0L;
        s_heatRegistryObservedOn = 0L;
        s_heatRegistryObservedOff = 0L;
        s_waterScanCalls = 0L;
        s_waterScanPositions = 0L;
        s_waterScanTileEntityLookups = 0L;
        s_waterScanHits = 0L;
        s_waterCacheQueries = 0L;
        s_waterCacheHits = 0L;
        s_waterCacheMisses = 0L;
        s_waterCacheInvalidations = 0L;
        s_waterRegistryQueries = 0L;
        s_waterRegistryCandidates = 0L;
        s_waterRegistryTileEntityLookups = 0L;
        s_waterRegistryHits = 0L;
        s_waterRegistryMaintenanceQueries = 0L;
        s_waterRegistryMaintenanceCandidates = 0L;
        s_waterRegistryMaintenanceTileEntityLookups = 0L;
        s_waterRegistryMaintenanceHits = 0L;
        s_waterRegistryConsumptionQueries = 0L;
        s_waterRegistryConsumptionCandidates = 0L;
        s_waterRegistryConsumptionTileEntityLookups = 0L;
        s_waterRegistryConsumptionHits = 0L;
        s_waterRegistryPrunes = 0L;
        s_lightExposureCacheQueries = 0L;
        s_lightExposureCacheHits = 0L;
        s_lightExposureEvaluations = 0L;
        s_lightExposureSearches = 0L;
        s_lightExposureNodes = 0L;
        s_lightExposureTransparentCandidates = 0L;
        s_lightExposureOpenResults = 0L;
        s_sunRefreshQueued = 0L;
        s_sunRefreshCoalesced = 0L;
        s_sunRefreshRebuilds = 0L;
        s_sunRefreshCells = 0L;
        s_sunRefreshElapsedStopwatchTicks = 0L;
        s_sunRefreshMaxElapsedStopwatchTicks = 0L;
        s_visualRefreshQueued = 0L;
        s_thermalExposureQueries = 0L;
        s_thermalExposureCacheHits = 0L;
        s_thermalExposureSearches = 0L;
        s_thermalExposureNodes = 0L;
        s_thermalExposureOpenResults = 0L;
        s_thermalExposureClosedResults = 0L;
        s_lightEvaluateCalls = 0L;
        s_lightEvaluateElapsedStopwatchTicks = 0L;
        s_lightEvaluateMaxStopwatchTicks = 0L;
        s_thermalEvaluateCalls = 0L;
        s_thermalEvaluateElapsedStopwatchTicks = 0L;
        s_thermalEvaluateMaxStopwatchTicks = 0L;
        s_areaMaintenancePhaseCalls = 0L;
        s_areaMaintenancePhaseElapsedStopwatchTicks = 0L;
        s_areaMaintenancePhaseMaxStopwatchTicks = 0L;
        s_activeBlockLightPrecheckQueries = 0L;
        s_activeBlockLightPrecheckPasses = 0L;
        s_exposureInvalidationBlockChanges = 0L;
        s_exposureInvalidationSkippedPlantLike = 0L;
        s_exposureInvalidationLightClears = 0L;
        s_exposureInvalidationThermalClears = 0L;
        s_exposureInvalidationSunRefreshQueues = 0L;
        s_lightProcessorRefreshSunlightCalls = 0L;
        s_lightProcessorRefreshSunlightElapsedStopwatchTicks = 0L;
        s_lightProcessorRefreshSunlightMaxStopwatchTicks = 0L;
        s_lightProcessorRefreshLocalSunCalls = 0L;
        s_lightProcessorRefreshLocalSunElapsedStopwatchTicks = 0L;
        s_lightProcessorRefreshLocalSunMaxStopwatchTicks = 0L;
        s_lightProcessorRefreshLocalBlockCalls = 0L;
        s_lightProcessorRefreshLocalBlockElapsedStopwatchTicks = 0L;
        s_lightProcessorRefreshLocalBlockMaxStopwatchTicks = 0L;
        s_lightProcessorSpreadSunCalls = 0L;
        s_lightProcessorSpreadSunElapsedStopwatchTicks = 0L;
        s_lightProcessorSpreadSunMaxStopwatchTicks = 0L;
        s_lightProcessorSpreadBlockCalls = 0L;
        s_lightProcessorSpreadBlockElapsedStopwatchTicks = 0L;
        s_lightProcessorSpreadBlockMaxStopwatchTicks = 0L;
        s_lightProcessorUnspreadSunCalls = 0L;
        s_lightProcessorUnspreadSunElapsedStopwatchTicks = 0L;
        s_lightProcessorUnspreadSunMaxStopwatchTicks = 0L;
        s_lightProcessorUnspreadBlockCalls = 0L;
        s_lightProcessorUnspreadBlockElapsedStopwatchTicks = 0L;
        s_lightProcessorUnspreadBlockMaxStopwatchTicks = 0L;
        s_waterRegistryResolveCacheHits = 0L;
        s_processAreaManagedPositiveBytes = 0L;
        s_processAreaManagedNegativeBytes = 0L;
        s_areaMaintenanceManagedPositiveBytes = 0L;
        s_areaMaintenanceManagedNegativeBytes = 0L;
        s_lightEvaluateManagedPositiveBytes = 0L;
        s_lightEvaluateManagedNegativeBytes = 0L;
        s_thermalEvaluateManagedPositiveBytes = 0L;
        s_thermalEvaluateManagedNegativeBytes = 0L;
        s_waterMaintenanceManagedPositiveBytes = 0L;
        s_waterMaintenanceManagedNegativeBytes = 0L;
        for (int i = 0; i < s_anchorNoPlantNames.Length; i++)
        {
            s_anchorNoPlantNames[i] = null;
            s_anchorNoPlantCounts[i] = 0L;
        }
        System.Threading.Interlocked.Increment(ref s_resetEpoch);
    }


    public static void RecordMissingPlantTileEntityWakeSkip(BlockValue blockValue)
    {
        if (!s_samplingEnabled)
            return;

        s_missingPlantTileEntityWakeSkips++;
        RecordProcessAreaAnchor(blockValue, false);
    }

    public static void RecordProcessAreaAnchor(BlockValue blockValue, bool hasPlantTileEntity)
    {
        if (!s_samplingEnabled)
            return;

        if (hasPlantTileEntity)
        {
            s_anchorPlantCalls++;
            return;
        }

        s_anchorNoPlantCalls++;

        string name = null;
        Block block = blockValue.Block;
        if (block != null)
            name = block.GetBlockName();

        if (string.IsNullOrEmpty(name))
        {
            s_anchorNullBlockCalls++;
            name = "<null>";
        }

        RecordNoPlantAnchorName(name);
    }

    private static void RecordNoPlantAnchorName(string name)
    {
        if (string.IsNullOrEmpty(name))
            name = "<empty>";

        int empty = -1;
        long lowestCount = long.MaxValue;
        int lowestIndex = 0;

        for (int i = 0; i < s_anchorNoPlantNames.Length; i++)
        {
            if (s_anchorNoPlantNames[i] == name)
            {
                s_anchorNoPlantCounts[i]++;
                return;
            }

            if (s_anchorNoPlantNames[i] == null && empty < 0)
                empty = i;

            if (s_anchorNoPlantCounts[i] < lowestCount)
            {
                lowestCount = s_anchorNoPlantCounts[i];
                lowestIndex = i;
            }
        }

        int target = empty >= 0 ? empty : lowestIndex;
        s_anchorNoPlantNames[target] = name;
        s_anchorNoPlantCounts[target] = 1L;
    }

    public static void RecordAreaProcess(long elapsedStopwatchTicks, int providers, int plots, int plants, bool areaMaintenanceRan)
    {
        if (!s_samplingEnabled)
            return;

        s_areaCalls++;
        s_totalElapsedStopwatchTicks += elapsedStopwatchTicks;
        if (elapsedStopwatchTicks > s_maxElapsedStopwatchTicks)
            s_maxElapsedStopwatchTicks = elapsedStopwatchTicks;

        if (areaMaintenanceRan)
        {
            s_areaMaintenanceCalls++;
            s_areaMaintenanceElapsedStopwatchTicks += elapsedStopwatchTicks;
            if (elapsedStopwatchTicks > s_areaMaintenanceMaxElapsedStopwatchTicks)
                s_areaMaintenanceMaxElapsedStopwatchTicks = elapsedStopwatchTicks;
        }
        else
        {
            s_anchorOnlyCalls++;
            s_anchorOnlyElapsedStopwatchTicks += elapsedStopwatchTicks;
            if (elapsedStopwatchTicks > s_anchorOnlyMaxElapsedStopwatchTicks)
                s_anchorOnlyMaxElapsedStopwatchTicks = elapsedStopwatchTicks;
        }

        s_totalProviders += providers;
        s_totalPlots += plots;
        s_totalPlants += plants;
    }

    public static void RecordWaterScan(long positions, long tileEntityLookups, bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_waterScanCalls++;
        s_waterScanPositions += positions;
        s_waterScanTileEntityLookups += tileEntityLookups;
        if (hit)
            s_waterScanHits++;
    }

    public static void RecordWaterCacheQuery(bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_waterCacheQueries++;
        if (hit)
            s_waterCacheHits++;
        else
            s_waterCacheMisses++;
    }

    public static void RecordWaterCacheInvalidation()
    {
        if (!s_samplingEnabled)
            return;

        s_waterCacheInvalidations++;
    }


    public static void RecordWaterRegistryQuery(long candidates, long tileEntityLookups, bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_waterRegistryQueries++;
        s_waterRegistryCandidates += candidates;
        s_waterRegistryTileEntityLookups += tileEntityLookups;
        if (hit)
            s_waterRegistryHits++;
    }

    public static void RecordWaterRegistryMaintenanceCollect(long candidates, long tileEntityLookups, bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_waterRegistryMaintenanceQueries++;
        s_waterRegistryMaintenanceCandidates += candidates;
        s_waterRegistryMaintenanceTileEntityLookups += tileEntityLookups;
        if (hit)
            s_waterRegistryMaintenanceHits++;
    }

    public static void RecordWaterRegistryConsumption(long candidates, long tileEntityLookups, bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_waterRegistryConsumptionQueries++;
        s_waterRegistryConsumptionCandidates += candidates;
        s_waterRegistryConsumptionTileEntityLookups += tileEntityLookups;
        if (hit)
            s_waterRegistryConsumptionHits++;
    }

    public static void RecordWaterRegistryPrune()
    {
        if (!s_samplingEnabled)
            return;

        s_waterRegistryPrunes++;
    }

    public static void RecordWaterRegistryResolveCacheHit()
    {
        if (!s_samplingEnabled)
            return;

        s_waterRegistryResolveCacheHits++;
    }

    public static void RecordManagedMemoryDelta(AdvancedFarmingManagedMemoryPhase phase, long deltaBytes)
    {
        if (!s_samplingEnabled || deltaBytes == 0L)
            return;

        long positive = deltaBytes > 0L ? deltaBytes : 0L;
        long negative = deltaBytes < 0L ? -deltaBytes : 0L;

        switch (phase)
        {
            case AdvancedFarmingManagedMemoryPhase.ProcessArea:
                s_processAreaManagedPositiveBytes += positive;
                s_processAreaManagedNegativeBytes += negative;
                break;
            case AdvancedFarmingManagedMemoryPhase.AreaMaintenance:
                s_areaMaintenanceManagedPositiveBytes += positive;
                s_areaMaintenanceManagedNegativeBytes += negative;
                break;
            case AdvancedFarmingManagedMemoryPhase.LightEvaluate:
                s_lightEvaluateManagedPositiveBytes += positive;
                s_lightEvaluateManagedNegativeBytes += negative;
                break;
            case AdvancedFarmingManagedMemoryPhase.ThermalEvaluate:
                s_thermalEvaluateManagedPositiveBytes += positive;
                s_thermalEvaluateManagedNegativeBytes += negative;
                break;
            case AdvancedFarmingManagedMemoryPhase.WaterMaintenance:
                s_waterMaintenanceManagedPositiveBytes += positive;
                s_waterMaintenanceManagedNegativeBytes += negative;
                break;
        }
    }

    public static void RecordLightExposureCacheQuery(bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_lightExposureCacheQueries++;
        if (hit)
            s_lightExposureCacheHits++;
    }

    public static void RecordLightExposureEvaluation(bool usedSearch, long nodesVisited, long transparentCandidates, bool open)
    {
        if (!s_samplingEnabled)
            return;

        s_lightExposureEvaluations++;
        if (usedSearch)
            s_lightExposureSearches++;
        s_lightExposureNodes += nodesVisited;
        s_lightExposureTransparentCandidates += transparentCandidates;
        if (open)
            s_lightExposureOpenResults++;
    }

    public static void RecordHeatRegistryQuery()
    {
        if (!s_samplingEnabled)
            return;

        s_heatRegistryQueries++;
    }

    public static void RecordHeatRegistryHit()
    {
        if (!s_samplingEnabled)
            return;

        s_heatRegistryHits++;
    }

    public static void RecordHeatRegistryMiss()
    {
        if (!s_samplingEnabled)
            return;

        s_heatRegistryMisses++;
    }

    public static void RecordHeatRegistryFallbackScan()
    {
        if (!s_samplingEnabled)
            return;

        s_heatRegistryFallbackScans++;
    }

    public static void RecordHeatSourceRegistryObserve(bool burning)
    {
        if (!s_samplingEnabled)
            return;

        if (burning)
            s_heatRegistryObservedOn++;
        else
            s_heatRegistryObservedOff++;
    }

    public static void RecordHeatCacheQuery(bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_heatCacheQueries++;
        if (hit)
            s_heatCacheHits++;
        else
            s_heatCacheMisses++;
    }

    public static void RecordHeatCacheInvalidation()
    {
        if (!s_samplingEnabled)
            return;

        s_heatCacheInvalidations++;
    }

    public static void RecordHeatScan(long scannedPositions, long tileEntityLookups, bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_heatScanCalls++;
        s_heatScanPositions += scannedPositions;
        s_heatScanTileEntityLookups += tileEntityLookups;
        if (hit)
            s_heatScanHits++;
    }

    public static void RecordPlantSync()
    {
        if (!s_samplingEnabled)
            return;

        s_plantSyncs++;
    }


    public static void RecordSunRefreshQueued(bool coalesced)
    {
        if (!s_samplingEnabled)
            return;

        s_sunRefreshQueued++;
        if (coalesced)
            s_sunRefreshCoalesced++;
    }

    public static void RecordSunRebuild(long elapsedStopwatchTicks, int cells)
    {
        if (!s_samplingEnabled)
            return;

        s_sunRefreshRebuilds++;
        if (cells > 0)
            s_sunRefreshCells += cells;
        s_sunRefreshElapsedStopwatchTicks += elapsedStopwatchTicks;
        if (elapsedStopwatchTicks > s_sunRefreshMaxElapsedStopwatchTicks)
            s_sunRefreshMaxElapsedStopwatchTicks = elapsedStopwatchTicks;
    }

    public static void RecordVisualRefreshQueued()
    {
        if (!s_samplingEnabled)
            return;

        s_visualRefreshQueued++;
    }

    public static void RecordThermalExposureCacheQuery(bool hit)
    {
        if (!s_samplingEnabled)
            return;

        s_thermalExposureQueries++;
        if (hit)
            s_thermalExposureCacheHits++;
    }

    public static void RecordThermalExposureSearch(int nodesVisited, bool openToExterior)
    {
        if (!s_samplingEnabled)
            return;

        s_thermalExposureSearches++;
        if (nodesVisited > 0)
            s_thermalExposureNodes += nodesVisited;
        if (openToExterior)
            s_thermalExposureOpenResults++;
        else
            s_thermalExposureClosedResults++;
    }

    public static void RecordLightEvaluate(long elapsedStopwatchTicks)
    {
        if (!s_samplingEnabled)
            return;

        s_lightEvaluateCalls++;
        s_lightEvaluateElapsedStopwatchTicks += elapsedStopwatchTicks;
        if (elapsedStopwatchTicks > s_lightEvaluateMaxStopwatchTicks)
            s_lightEvaluateMaxStopwatchTicks = elapsedStopwatchTicks;
    }

    public static void RecordThermalEvaluate(long elapsedStopwatchTicks)
    {
        if (!s_samplingEnabled)
            return;

        s_thermalEvaluateCalls++;
        s_thermalEvaluateElapsedStopwatchTicks += elapsedStopwatchTicks;
        if (elapsedStopwatchTicks > s_thermalEvaluateMaxStopwatchTicks)
            s_thermalEvaluateMaxStopwatchTicks = elapsedStopwatchTicks;
    }

    public static void RecordAreaMaintenancePhase(long elapsedStopwatchTicks)
    {
        if (!s_samplingEnabled)
            return;

        s_areaMaintenancePhaseCalls++;
        s_areaMaintenancePhaseElapsedStopwatchTicks += elapsedStopwatchTicks;
        if (elapsedStopwatchTicks > s_areaMaintenancePhaseMaxStopwatchTicks)
            s_areaMaintenancePhaseMaxStopwatchTicks = elapsedStopwatchTicks;
    }

    public static long LightEvaluateElapsedStopwatchTicks
    {
        get { return s_lightEvaluateElapsedStopwatchTicks; }
    }

    public static long ThermalEvaluateElapsedStopwatchTicks
    {
        get { return s_thermalEvaluateElapsedStopwatchTicks; }
    }

    public static void RecordActiveBlockLightPrecheck(bool pass)
    {
        if (!s_samplingEnabled)
            return;

        s_activeBlockLightPrecheckQueries++;
        if (pass)
            s_activeBlockLightPrecheckPasses++;
    }

    public static void RecordExposureInvalidationBlockChange(bool skippedPlantLike, bool clearedLight, bool clearedThermal, bool queuedSunRefresh)
    {
        if (!s_samplingEnabled)
            return;

        s_exposureInvalidationBlockChanges++;
        if (skippedPlantLike)
            s_exposureInvalidationSkippedPlantLike++;
        if (clearedLight)
            s_exposureInvalidationLightClears++;
        if (clearedThermal)
            s_exposureInvalidationThermalClears++;
        if (queuedSunRefresh)
            s_exposureInvalidationSunRefreshQueues++;
    }

    private static void AddTimedCounter(ref long calls, ref long totalTicks, ref long maxTicks, long elapsedStopwatchTicks)
    {
        calls++;
        totalTicks += elapsedStopwatchTicks;
        if (elapsedStopwatchTicks > maxTicks)
            maxTicks = elapsedStopwatchTicks;
    }

    public static void RecordLightProcessorRefreshSunlight(long elapsedStopwatchTicks)
    {
        if (!s_samplingEnabled)
            return;

        AddTimedCounter(ref s_lightProcessorRefreshSunlightCalls, ref s_lightProcessorRefreshSunlightElapsedStopwatchTicks, ref s_lightProcessorRefreshSunlightMaxStopwatchTicks, elapsedStopwatchTicks);
    }

    public static void RecordLightProcessorRefreshLocal(Chunk.LIGHT_TYPE type, long elapsedStopwatchTicks)
    {
        if (!s_samplingEnabled)
            return;

        if (type == Chunk.LIGHT_TYPE.SUN)
            AddTimedCounter(ref s_lightProcessorRefreshLocalSunCalls, ref s_lightProcessorRefreshLocalSunElapsedStopwatchTicks, ref s_lightProcessorRefreshLocalSunMaxStopwatchTicks, elapsedStopwatchTicks);
        else
            AddTimedCounter(ref s_lightProcessorRefreshLocalBlockCalls, ref s_lightProcessorRefreshLocalBlockElapsedStopwatchTicks, ref s_lightProcessorRefreshLocalBlockMaxStopwatchTicks, elapsedStopwatchTicks);
    }

    public static void RecordLightProcessorSpread(Chunk.LIGHT_TYPE type, long elapsedStopwatchTicks)
    {
        if (!s_samplingEnabled)
            return;

        if (type == Chunk.LIGHT_TYPE.SUN)
            AddTimedCounter(ref s_lightProcessorSpreadSunCalls, ref s_lightProcessorSpreadSunElapsedStopwatchTicks, ref s_lightProcessorSpreadSunMaxStopwatchTicks, elapsedStopwatchTicks);
        else
            AddTimedCounter(ref s_lightProcessorSpreadBlockCalls, ref s_lightProcessorSpreadBlockElapsedStopwatchTicks, ref s_lightProcessorSpreadBlockMaxStopwatchTicks, elapsedStopwatchTicks);
    }

    public static void RecordLightProcessorUnspread(Chunk.LIGHT_TYPE type, long elapsedStopwatchTicks)
    {
        if (!s_samplingEnabled)
            return;

        if (type == Chunk.LIGHT_TYPE.SUN)
            AddTimedCounter(ref s_lightProcessorUnspreadSunCalls, ref s_lightProcessorUnspreadSunElapsedStopwatchTicks, ref s_lightProcessorUnspreadSunMaxStopwatchTicks, elapsedStopwatchTicks);
        else
            AddTimedCounter(ref s_lightProcessorUnspreadBlockCalls, ref s_lightProcessorUnspreadBlockElapsedStopwatchTicks, ref s_lightProcessorUnspreadBlockMaxStopwatchTicks, elapsedStopwatchTicks);
    }

    public static SampleSnapshot GetSnapshot()
    {
        SampleSnapshot s = new SampleSnapshot();
        s.ResetEpoch=System.Threading.Interlocked.Read(ref s_resetEpoch);
        s.StopwatchTicks = Stopwatch.GetTimestamp();
        s.AreaCalls = s_areaCalls;
        s.TotalElapsedStopwatchTicks = s_totalElapsedStopwatchTicks;
        s.MaxElapsedStopwatchTicks = s_maxElapsedStopwatchTicks;
        s.TotalProviders = s_totalProviders;
        s.TotalPlots = s_totalPlots;
        s.TotalPlants = s_totalPlants;
        s.AnchorOnlyCalls = s_anchorOnlyCalls;
        s.AreaMaintenanceCalls = s_areaMaintenanceCalls;
        s.AnchorOnlyElapsedStopwatchTicks = s_anchorOnlyElapsedStopwatchTicks;
        s.AreaMaintenanceElapsedStopwatchTicks = s_areaMaintenanceElapsedStopwatchTicks;
        s.AnchorOnlyMaxElapsedStopwatchTicks = s_anchorOnlyMaxElapsedStopwatchTicks;
        s.AreaMaintenanceMaxElapsedStopwatchTicks = s_areaMaintenanceMaxElapsedStopwatchTicks;
        s.PlantSyncs = s_plantSyncs;
        s.AnchorPlantCalls = s_anchorPlantCalls;
        s.AnchorNoPlantCalls = s_anchorNoPlantCalls;
        s.AnchorNullBlockCalls = s_anchorNullBlockCalls;
        s.HeatScanCalls = s_heatScanCalls;
        s.HeatScanPositions = s_heatScanPositions;
        s.HeatScanTileEntityLookups = s_heatScanTileEntityLookups;
        s.HeatScanHits = s_heatScanHits;
        s.HeatCacheQueries = s_heatCacheQueries;
        s.HeatCacheHits = s_heatCacheHits;
        s.HeatCacheMisses = s_heatCacheMisses;
        s.HeatCacheInvalidations = s_heatCacheInvalidations;
        s.HeatRegistryQueries = s_heatRegistryQueries;
        s.HeatRegistryHits = s_heatRegistryHits;
        s.HeatRegistryMisses = s_heatRegistryMisses;
        s.HeatRegistryFallbackScans = s_heatRegistryFallbackScans;
        s.HeatRegistryObservedOn = s_heatRegistryObservedOn;
        s.HeatRegistryObservedOff = s_heatRegistryObservedOff;
        s.WaterScanCalls = s_waterScanCalls;
        s.WaterScanPositions = s_waterScanPositions;
        s.WaterScanTileEntityLookups = s_waterScanTileEntityLookups;
        s.WaterScanHits = s_waterScanHits;
        s.WaterCacheQueries = s_waterCacheQueries;
        s.WaterCacheHits = s_waterCacheHits;
        s.WaterCacheMisses = s_waterCacheMisses;
        s.WaterCacheInvalidations = s_waterCacheInvalidations;
        s.WaterRegistryQueries = s_waterRegistryQueries;
        s.WaterRegistryCandidates = s_waterRegistryCandidates;
        s.WaterRegistryTileEntityLookups = s_waterRegistryTileEntityLookups;
        s.WaterRegistryHits = s_waterRegistryHits;
        s.WaterRegistryMaintenanceQueries = s_waterRegistryMaintenanceQueries;
        s.WaterRegistryMaintenanceCandidates = s_waterRegistryMaintenanceCandidates;
        s.WaterRegistryMaintenanceTileEntityLookups = s_waterRegistryMaintenanceTileEntityLookups;
        s.WaterRegistryMaintenanceHits = s_waterRegistryMaintenanceHits;
        s.WaterRegistryConsumptionQueries = s_waterRegistryConsumptionQueries;
        s.WaterRegistryConsumptionCandidates = s_waterRegistryConsumptionCandidates;
        s.WaterRegistryConsumptionTileEntityLookups = s_waterRegistryConsumptionTileEntityLookups;
        s.WaterRegistryConsumptionHits = s_waterRegistryConsumptionHits;
        s.WaterRegistryPrunes = s_waterRegistryPrunes;
        s.LightExposureCacheQueries = s_lightExposureCacheQueries;
        s.LightExposureCacheHits = s_lightExposureCacheHits;
        s.LightExposureEvaluations = s_lightExposureEvaluations;
        s.LightExposureSearches = s_lightExposureSearches;
        s.LightExposureNodes = s_lightExposureNodes;
        s.LightExposureTransparentCandidates = s_lightExposureTransparentCandidates;
        s.LightExposureOpenResults = s_lightExposureOpenResults;
        s.SunRefreshQueued = s_sunRefreshQueued;
        s.SunRefreshCoalesced = s_sunRefreshCoalesced;
        s.SunRefreshRebuilds = s_sunRefreshRebuilds;
        s.SunRefreshCells = s_sunRefreshCells;
        s.SunRefreshElapsedStopwatchTicks = s_sunRefreshElapsedStopwatchTicks;
        s.SunRefreshMaxElapsedStopwatchTicks = s_sunRefreshMaxElapsedStopwatchTicks;
        s.VisualRefreshQueued = s_visualRefreshQueued;
        s.ThermalExposureQueries = s_thermalExposureQueries;
        s.ThermalExposureCacheHits = s_thermalExposureCacheHits;
        s.ThermalExposureSearches = s_thermalExposureSearches;
        s.ThermalExposureNodes = s_thermalExposureNodes;
        s.ThermalExposureOpenResults = s_thermalExposureOpenResults;
        s.ThermalExposureClosedResults = s_thermalExposureClosedResults;
        s.LightEvaluateCalls = s_lightEvaluateCalls;
        s.LightEvaluateElapsedStopwatchTicks = s_lightEvaluateElapsedStopwatchTicks;
        s.LightEvaluateMaxStopwatchTicks = s_lightEvaluateMaxStopwatchTicks;
        s.ThermalEvaluateCalls = s_thermalEvaluateCalls;
        s.ThermalEvaluateElapsedStopwatchTicks = s_thermalEvaluateElapsedStopwatchTicks;
        s.ThermalEvaluateMaxStopwatchTicks = s_thermalEvaluateMaxStopwatchTicks;
        s.AreaMaintenancePhaseCalls = s_areaMaintenancePhaseCalls;
        s.AreaMaintenancePhaseElapsedStopwatchTicks = s_areaMaintenancePhaseElapsedStopwatchTicks;
        s.AreaMaintenancePhaseMaxStopwatchTicks = s_areaMaintenancePhaseMaxStopwatchTicks;
        s.ActiveBlockLightPrecheckQueries = s_activeBlockLightPrecheckQueries;
        s.ActiveBlockLightPrecheckPasses = s_activeBlockLightPrecheckPasses;
        s.ExposureInvalidationBlockChanges = s_exposureInvalidationBlockChanges;
        s.ExposureInvalidationSkippedPlantLike = s_exposureInvalidationSkippedPlantLike;
        s.ExposureInvalidationLightClears = s_exposureInvalidationLightClears;
        s.ExposureInvalidationThermalClears = s_exposureInvalidationThermalClears;
        s.ExposureInvalidationSunRefreshQueues = s_exposureInvalidationSunRefreshQueues;
        s.LightProcessorRefreshSunlightCalls = s_lightProcessorRefreshSunlightCalls;
        s.LightProcessorRefreshSunlightElapsedStopwatchTicks = s_lightProcessorRefreshSunlightElapsedStopwatchTicks;
        s.LightProcessorRefreshSunlightMaxStopwatchTicks = s_lightProcessorRefreshSunlightMaxStopwatchTicks;
        s.LightProcessorRefreshLocalSunCalls = s_lightProcessorRefreshLocalSunCalls;
        s.LightProcessorRefreshLocalSunElapsedStopwatchTicks = s_lightProcessorRefreshLocalSunElapsedStopwatchTicks;
        s.LightProcessorRefreshLocalSunMaxStopwatchTicks = s_lightProcessorRefreshLocalSunMaxStopwatchTicks;
        s.LightProcessorRefreshLocalBlockCalls = s_lightProcessorRefreshLocalBlockCalls;
        s.LightProcessorRefreshLocalBlockElapsedStopwatchTicks = s_lightProcessorRefreshLocalBlockElapsedStopwatchTicks;
        s.LightProcessorRefreshLocalBlockMaxStopwatchTicks = s_lightProcessorRefreshLocalBlockMaxStopwatchTicks;
        s.LightProcessorSpreadSunCalls = s_lightProcessorSpreadSunCalls;
        s.LightProcessorSpreadSunElapsedStopwatchTicks = s_lightProcessorSpreadSunElapsedStopwatchTicks;
        s.LightProcessorSpreadSunMaxStopwatchTicks = s_lightProcessorSpreadSunMaxStopwatchTicks;
        s.LightProcessorSpreadBlockCalls = s_lightProcessorSpreadBlockCalls;
        s.LightProcessorSpreadBlockElapsedStopwatchTicks = s_lightProcessorSpreadBlockElapsedStopwatchTicks;
        s.LightProcessorSpreadBlockMaxStopwatchTicks = s_lightProcessorSpreadBlockMaxStopwatchTicks;
        s.LightProcessorUnspreadSunCalls = s_lightProcessorUnspreadSunCalls;
        s.LightProcessorUnspreadSunElapsedStopwatchTicks = s_lightProcessorUnspreadSunElapsedStopwatchTicks;
        s.LightProcessorUnspreadSunMaxStopwatchTicks = s_lightProcessorUnspreadSunMaxStopwatchTicks;
        s.LightProcessorUnspreadBlockCalls = s_lightProcessorUnspreadBlockCalls;
        s.LightProcessorUnspreadBlockElapsedStopwatchTicks = s_lightProcessorUnspreadBlockElapsedStopwatchTicks;
        s.LightProcessorUnspreadBlockMaxStopwatchTicks = s_lightProcessorUnspreadBlockMaxStopwatchTicks;
        s.WaterRegistryResolveCacheHits = s_waterRegistryResolveCacheHits;
        s.ProcessAreaManagedPositiveBytes = s_processAreaManagedPositiveBytes;
        s.ProcessAreaManagedNegativeBytes = s_processAreaManagedNegativeBytes;
        s.AreaMaintenanceManagedPositiveBytes = s_areaMaintenanceManagedPositiveBytes;
        s.AreaMaintenanceManagedNegativeBytes = s_areaMaintenanceManagedNegativeBytes;
        s.LightEvaluateManagedPositiveBytes = s_lightEvaluateManagedPositiveBytes;
        s.LightEvaluateManagedNegativeBytes = s_lightEvaluateManagedNegativeBytes;
        s.ThermalEvaluateManagedPositiveBytes = s_thermalEvaluateManagedPositiveBytes;
        s.ThermalEvaluateManagedNegativeBytes = s_thermalEvaluateManagedNegativeBytes;
        s.WaterMaintenanceManagedPositiveBytes = s_waterMaintenanceManagedPositiveBytes;
        s.WaterMaintenanceManagedNegativeBytes = s_waterMaintenanceManagedNegativeBytes;
        if(s.ResetEpoch!=System.Threading.Interlocked.Read(ref s_resetEpoch))s.ResetEpoch=-1;
        return s;
    }

    public static string BuildSamplingDeltaReport(string label, SampleSnapshot start, SampleSnapshot end)
    {
        if(start.ResetEpoch!=end.ResetEpoch||(start.ResetEpoch&1L)!=0)
            return "[AdvancedFarming PerfSample] invalid=true reason=counter-reset-during-run label="+label;
        double frequency = Stopwatch.Frequency;
        double seconds = frequency > 0d ? (double)(end.StopwatchTicks - start.StopwatchTicks) / frequency : 0d;

        long areaCalls = end.AreaCalls - start.AreaCalls;
        long totalElapsed = end.TotalElapsedStopwatchTicks - start.TotalElapsedStopwatchTicks;
        long providers = end.TotalProviders - start.TotalProviders;
        long plots = end.TotalPlots - start.TotalPlots;
        long plants = end.TotalPlants - start.TotalPlants;
        long anchorOnlyCalls = end.AnchorOnlyCalls - start.AnchorOnlyCalls;
        long maintenanceCalls = end.AreaMaintenanceCalls - start.AreaMaintenanceCalls;
        long anchorElapsed = end.AnchorOnlyElapsedStopwatchTicks - start.AnchorOnlyElapsedStopwatchTicks;
        long maintenanceElapsed = end.AreaMaintenanceElapsedStopwatchTicks - start.AreaMaintenanceElapsedStopwatchTicks;
        long plantSyncs = end.PlantSyncs - start.PlantSyncs;
        long anchorPlantCalls = end.AnchorPlantCalls - start.AnchorPlantCalls;
        long anchorNoPlantCalls = end.AnchorNoPlantCalls - start.AnchorNoPlantCalls;
        long anchorNullBlockCalls = end.AnchorNullBlockCalls - start.AnchorNullBlockCalls;
        long heatScanCalls = end.HeatScanCalls - start.HeatScanCalls;
        long heatScanPositions = end.HeatScanPositions - start.HeatScanPositions;
        long heatScanTileEntityLookups = end.HeatScanTileEntityLookups - start.HeatScanTileEntityLookups;
        long heatScanHits = end.HeatScanHits - start.HeatScanHits;
        long heatCacheQueries = end.HeatCacheQueries - start.HeatCacheQueries;
        long heatCacheHits = end.HeatCacheHits - start.HeatCacheHits;
        long heatCacheMisses = end.HeatCacheMisses - start.HeatCacheMisses;
        long heatCacheInvalidations = end.HeatCacheInvalidations - start.HeatCacheInvalidations;
        long heatRegistryQueries = end.HeatRegistryQueries - start.HeatRegistryQueries;
        long heatRegistryHits = end.HeatRegistryHits - start.HeatRegistryHits;
        long heatRegistryMisses = end.HeatRegistryMisses - start.HeatRegistryMisses;
        long heatRegistryFallbackScans = end.HeatRegistryFallbackScans - start.HeatRegistryFallbackScans;
        long heatRegistryObservedOn = end.HeatRegistryObservedOn - start.HeatRegistryObservedOn;
        long heatRegistryObservedOff = end.HeatRegistryObservedOff - start.HeatRegistryObservedOff;
        long waterScanCalls = end.WaterScanCalls - start.WaterScanCalls;
        long waterScanPositions = end.WaterScanPositions - start.WaterScanPositions;
        long waterScanTileEntityLookups = end.WaterScanTileEntityLookups - start.WaterScanTileEntityLookups;
        long waterScanHits = end.WaterScanHits - start.WaterScanHits;
        long waterCacheQueries = end.WaterCacheQueries - start.WaterCacheQueries;
        long waterCacheHits = end.WaterCacheHits - start.WaterCacheHits;
        long waterCacheMisses = end.WaterCacheMisses - start.WaterCacheMisses;
        long waterCacheInvalidations = end.WaterCacheInvalidations - start.WaterCacheInvalidations;
        long waterRegistryQueries = end.WaterRegistryQueries - start.WaterRegistryQueries;
        long waterRegistryCandidates = end.WaterRegistryCandidates - start.WaterRegistryCandidates;
        long waterRegistryTileEntityLookups = end.WaterRegistryTileEntityLookups - start.WaterRegistryTileEntityLookups;
        long waterRegistryHits = end.WaterRegistryHits - start.WaterRegistryHits;
        long waterRegistryMaintenanceQueries = end.WaterRegistryMaintenanceQueries - start.WaterRegistryMaintenanceQueries;
        long waterRegistryMaintenanceCandidates = end.WaterRegistryMaintenanceCandidates - start.WaterRegistryMaintenanceCandidates;
        long waterRegistryMaintenanceTileEntityLookups = end.WaterRegistryMaintenanceTileEntityLookups - start.WaterRegistryMaintenanceTileEntityLookups;
        long waterRegistryMaintenanceHits = end.WaterRegistryMaintenanceHits - start.WaterRegistryMaintenanceHits;
        long waterRegistryConsumptionQueries = end.WaterRegistryConsumptionQueries - start.WaterRegistryConsumptionQueries;
        long waterRegistryConsumptionCandidates = end.WaterRegistryConsumptionCandidates - start.WaterRegistryConsumptionCandidates;
        long waterRegistryConsumptionTileEntityLookups = end.WaterRegistryConsumptionTileEntityLookups - start.WaterRegistryConsumptionTileEntityLookups;
        long waterRegistryConsumptionHits = end.WaterRegistryConsumptionHits - start.WaterRegistryConsumptionHits;
        long waterRegistryPrunes = end.WaterRegistryPrunes - start.WaterRegistryPrunes;
        long waterRegistryResolveCacheHits = end.WaterRegistryResolveCacheHits - start.WaterRegistryResolveCacheHits;
        long processAreaManagedPositiveKb = (end.ProcessAreaManagedPositiveBytes - start.ProcessAreaManagedPositiveBytes) / 1024L;
        long processAreaManagedNegativeKb = (end.ProcessAreaManagedNegativeBytes - start.ProcessAreaManagedNegativeBytes) / 1024L;
        long areaMaintenanceManagedPositiveKb = (end.AreaMaintenanceManagedPositiveBytes - start.AreaMaintenanceManagedPositiveBytes) / 1024L;
        long areaMaintenanceManagedNegativeKb = (end.AreaMaintenanceManagedNegativeBytes - start.AreaMaintenanceManagedNegativeBytes) / 1024L;
        long lightEvaluateManagedPositiveKb = (end.LightEvaluateManagedPositiveBytes - start.LightEvaluateManagedPositiveBytes) / 1024L;
        long lightEvaluateManagedNegativeKb = (end.LightEvaluateManagedNegativeBytes - start.LightEvaluateManagedNegativeBytes) / 1024L;
        long thermalEvaluateManagedPositiveKb = (end.ThermalEvaluateManagedPositiveBytes - start.ThermalEvaluateManagedPositiveBytes) / 1024L;
        long thermalEvaluateManagedNegativeKb = (end.ThermalEvaluateManagedNegativeBytes - start.ThermalEvaluateManagedNegativeBytes) / 1024L;
        long waterMaintenanceManagedPositiveKb = (end.WaterMaintenanceManagedPositiveBytes - start.WaterMaintenanceManagedPositiveBytes) / 1024L;
        long waterMaintenanceManagedNegativeKb = (end.WaterMaintenanceManagedNegativeBytes - start.WaterMaintenanceManagedNegativeBytes) / 1024L;
        long lightExposureCacheQueries = end.LightExposureCacheQueries - start.LightExposureCacheQueries;
        long lightExposureCacheHits = end.LightExposureCacheHits - start.LightExposureCacheHits;
        long lightExposureEvaluations = end.LightExposureEvaluations - start.LightExposureEvaluations;
        long lightExposureSearches = end.LightExposureSearches - start.LightExposureSearches;
        long lightExposureNodes = end.LightExposureNodes - start.LightExposureNodes;
        long lightExposureTransparentCandidates = end.LightExposureTransparentCandidates - start.LightExposureTransparentCandidates;
        long lightExposureOpenResults = end.LightExposureOpenResults - start.LightExposureOpenResults;
        long sunRefreshQueued = end.SunRefreshQueued - start.SunRefreshQueued;
        long sunRefreshCoalesced = end.SunRefreshCoalesced - start.SunRefreshCoalesced;
        long sunRefreshRebuilds = end.SunRefreshRebuilds - start.SunRefreshRebuilds;
        long sunRefreshCells = end.SunRefreshCells - start.SunRefreshCells;
        long sunRefreshElapsed = end.SunRefreshElapsedStopwatchTicks - start.SunRefreshElapsedStopwatchTicks;
        long visualRefreshQueued = end.VisualRefreshQueued - start.VisualRefreshQueued;
        long thermalExposureQueries = end.ThermalExposureQueries - start.ThermalExposureQueries;
        long thermalExposureCacheHits = end.ThermalExposureCacheHits - start.ThermalExposureCacheHits;
        long thermalExposureSearches = end.ThermalExposureSearches - start.ThermalExposureSearches;
        long thermalExposureNodes = end.ThermalExposureNodes - start.ThermalExposureNodes;
        long thermalExposureOpenResults = end.ThermalExposureOpenResults - start.ThermalExposureOpenResults;
        long thermalExposureClosedResults = end.ThermalExposureClosedResults - start.ThermalExposureClosedResults;
        long lightEvaluateCalls = end.LightEvaluateCalls - start.LightEvaluateCalls;
        long lightEvaluateElapsed = end.LightEvaluateElapsedStopwatchTicks - start.LightEvaluateElapsedStopwatchTicks;
        long thermalEvaluateCalls = end.ThermalEvaluateCalls - start.ThermalEvaluateCalls;
        long thermalEvaluateElapsed = end.ThermalEvaluateElapsedStopwatchTicks - start.ThermalEvaluateElapsedStopwatchTicks;
        long areaMaintenancePhaseCalls = end.AreaMaintenancePhaseCalls - start.AreaMaintenancePhaseCalls;
        long areaMaintenancePhaseElapsed = end.AreaMaintenancePhaseElapsedStopwatchTicks - start.AreaMaintenancePhaseElapsedStopwatchTicks;
        long activeBlockLightPrecheckQueries = end.ActiveBlockLightPrecheckQueries - start.ActiveBlockLightPrecheckQueries;
        long activeBlockLightPrecheckPasses = end.ActiveBlockLightPrecheckPasses - start.ActiveBlockLightPrecheckPasses;
        long exposureInvalidationBlockChanges = end.ExposureInvalidationBlockChanges - start.ExposureInvalidationBlockChanges;
        long exposureInvalidationSkippedPlantLike = end.ExposureInvalidationSkippedPlantLike - start.ExposureInvalidationSkippedPlantLike;
        long exposureInvalidationLightClears = end.ExposureInvalidationLightClears - start.ExposureInvalidationLightClears;
        long exposureInvalidationThermalClears = end.ExposureInvalidationThermalClears - start.ExposureInvalidationThermalClears;
        long exposureInvalidationSunRefreshQueues = end.ExposureInvalidationSunRefreshQueues - start.ExposureInvalidationSunRefreshQueues;
        long lightProcessorRefreshSunlightCalls = end.LightProcessorRefreshSunlightCalls - start.LightProcessorRefreshSunlightCalls;
        long lightProcessorRefreshSunlightElapsed = end.LightProcessorRefreshSunlightElapsedStopwatchTicks - start.LightProcessorRefreshSunlightElapsedStopwatchTicks;
        long lightProcessorRefreshLocalSunCalls = end.LightProcessorRefreshLocalSunCalls - start.LightProcessorRefreshLocalSunCalls;
        long lightProcessorRefreshLocalSunElapsed = end.LightProcessorRefreshLocalSunElapsedStopwatchTicks - start.LightProcessorRefreshLocalSunElapsedStopwatchTicks;
        long lightProcessorRefreshLocalBlockCalls = end.LightProcessorRefreshLocalBlockCalls - start.LightProcessorRefreshLocalBlockCalls;
        long lightProcessorRefreshLocalBlockElapsed = end.LightProcessorRefreshLocalBlockElapsedStopwatchTicks - start.LightProcessorRefreshLocalBlockElapsedStopwatchTicks;
        long lightProcessorSpreadSunCalls = end.LightProcessorSpreadSunCalls - start.LightProcessorSpreadSunCalls;
        long lightProcessorSpreadSunElapsed = end.LightProcessorSpreadSunElapsedStopwatchTicks - start.LightProcessorSpreadSunElapsedStopwatchTicks;
        long lightProcessorSpreadBlockCalls = end.LightProcessorSpreadBlockCalls - start.LightProcessorSpreadBlockCalls;
        long lightProcessorSpreadBlockElapsed = end.LightProcessorSpreadBlockElapsedStopwatchTicks - start.LightProcessorSpreadBlockElapsedStopwatchTicks;
        long lightProcessorUnspreadSunCalls = end.LightProcessorUnspreadSunCalls - start.LightProcessorUnspreadSunCalls;
        long lightProcessorUnspreadSunElapsed = end.LightProcessorUnspreadSunElapsedStopwatchTicks - start.LightProcessorUnspreadSunElapsedStopwatchTicks;
        long lightProcessorUnspreadBlockCalls = end.LightProcessorUnspreadBlockCalls - start.LightProcessorUnspreadBlockCalls;
        long lightProcessorUnspreadBlockElapsed = end.LightProcessorUnspreadBlockElapsedStopwatchTicks - start.LightProcessorUnspreadBlockElapsedStopwatchTicks;

        double totalMs = frequency > 0d ? (double)totalElapsed * 1000d / frequency : 0d;
        double anchorMs = frequency > 0d ? (double)anchorElapsed * 1000d / frequency : 0d;
        double maintenanceMs = frequency > 0d ? (double)maintenanceElapsed * 1000d / frequency : 0d;
        double callsPerSecond = seconds > 0d ? (double)areaCalls / seconds : 0d;
        double plantsPerSecond = seconds > 0d ? (double)plants / seconds : 0d;
        double syncsPerSecond = seconds > 0d ? (double)plantSyncs / seconds : 0d;
        double heatPositionsPerSecond = seconds > 0d ? (double)heatScanPositions / seconds : 0d;
        double heatTeLookupsPerSecond = seconds > 0d ? (double)heatScanTileEntityLookups / seconds : 0d;
        double avgMs = areaCalls > 0L ? totalMs / (double)areaCalls : 0d;
        double anchorAvgMs = anchorOnlyCalls > 0L ? anchorMs / (double)anchorOnlyCalls : 0d;
        double maintenanceAvgMs = maintenanceCalls > 0L ? maintenanceMs / (double)maintenanceCalls : 0d;
        double sunRefreshMs = frequency > 0d ? (double)sunRefreshElapsed * 1000d / frequency : 0d;
        double sunRefreshAvgMs = sunRefreshRebuilds > 0L ? sunRefreshMs / (double)sunRefreshRebuilds : 0d;
        double sunRefreshCellsPerSecond = seconds > 0d ? (double)sunRefreshCells / seconds : 0d;
        double thermalExposureCacheHitRate = thermalExposureQueries > 0L ? (double)thermalExposureCacheHits * 100d / (double)thermalExposureQueries : 0d;
        double thermalExposureAvgNodes = thermalExposureSearches > 0L ? (double)thermalExposureNodes / (double)thermalExposureSearches : 0d;
        double lightEvaluateMs = frequency > 0d ? (double)lightEvaluateElapsed * 1000d / frequency : 0d;
        double lightEvaluateAvgMs = lightEvaluateCalls > 0L ? lightEvaluateMs / (double)lightEvaluateCalls : 0d;
        double thermalEvaluateMs = frequency > 0d ? (double)thermalEvaluateElapsed * 1000d / frequency : 0d;
        double thermalEvaluateAvgMs = thermalEvaluateCalls > 0L ? thermalEvaluateMs / (double)thermalEvaluateCalls : 0d;
        double areaMaintenancePhaseMs = frequency > 0d ? (double)areaMaintenancePhaseElapsed * 1000d / frequency : 0d;
        double areaMaintenancePhaseAvgMs = areaMaintenancePhaseCalls > 0L ? areaMaintenancePhaseMs / (double)areaMaintenancePhaseCalls : 0d;
        double activeBlockLightPrecheckPassRate = activeBlockLightPrecheckQueries > 0L ? (double)activeBlockLightPrecheckPasses * 100d / (double)activeBlockLightPrecheckQueries : 0d;
        double lightProcessorRefreshSunlightMs = frequency > 0d ? (double)lightProcessorRefreshSunlightElapsed * 1000d / frequency : 0d;
        double lightProcessorRefreshSunlightAvgMs = lightProcessorRefreshSunlightCalls > 0L ? lightProcessorRefreshSunlightMs / (double)lightProcessorRefreshSunlightCalls : 0d;
        double lightProcessorRefreshLocalSunMs = frequency > 0d ? (double)lightProcessorRefreshLocalSunElapsed * 1000d / frequency : 0d;
        double lightProcessorRefreshLocalSunAvgMs = lightProcessorRefreshLocalSunCalls > 0L ? lightProcessorRefreshLocalSunMs / (double)lightProcessorRefreshLocalSunCalls : 0d;
        double lightProcessorRefreshLocalBlockMs = frequency > 0d ? (double)lightProcessorRefreshLocalBlockElapsed * 1000d / frequency : 0d;
        double lightProcessorRefreshLocalBlockAvgMs = lightProcessorRefreshLocalBlockCalls > 0L ? lightProcessorRefreshLocalBlockMs / (double)lightProcessorRefreshLocalBlockCalls : 0d;
        double lightProcessorSpreadSunMs = frequency > 0d ? (double)lightProcessorSpreadSunElapsed * 1000d / frequency : 0d;
        double lightProcessorSpreadSunAvgMs = lightProcessorSpreadSunCalls > 0L ? lightProcessorSpreadSunMs / (double)lightProcessorSpreadSunCalls : 0d;
        double lightProcessorSpreadBlockMs = frequency > 0d ? (double)lightProcessorSpreadBlockElapsed * 1000d / frequency : 0d;
        double lightProcessorSpreadBlockAvgMs = lightProcessorSpreadBlockCalls > 0L ? lightProcessorSpreadBlockMs / (double)lightProcessorSpreadBlockCalls : 0d;
        double lightProcessorUnspreadSunMs = frequency > 0d ? (double)lightProcessorUnspreadSunElapsed * 1000d / frequency : 0d;
        double lightProcessorUnspreadSunAvgMs = lightProcessorUnspreadSunCalls > 0L ? lightProcessorUnspreadSunMs / (double)lightProcessorUnspreadSunCalls : 0d;
        double lightProcessorUnspreadBlockMs = frequency > 0d ? (double)lightProcessorUnspreadBlockElapsed * 1000d / frequency : 0d;
        double lightProcessorUnspreadBlockAvgMs = lightProcessorUnspreadBlockCalls > 0L ? lightProcessorUnspreadBlockMs / (double)lightProcessorUnspreadBlockCalls : 0d;

        return "[AdvancedFarming AutoBench] " + label
            + " seconds=" + seconds.ToString("0.00")
            + " areaCalls=" + areaCalls
            + " callsPerSecond=" + callsPerSecond.ToString("0.00")
            + " totalPlants=" + plants
            + " plantsPerSecond=" + plantsPerSecond.ToString("0.00")
            + " plantSyncs=" + plantSyncs
            + " syncsPerSecond=" + syncsPerSecond.ToString("0.00")
            + " avgAreaMs=" + avgMs.ToString("0.000")
            + " anchorOnlyCalls=" + anchorOnlyCalls
            + " anchorOnlyAvgMs=" + anchorAvgMs.ToString("0.000")
            + " maintenanceCalls=" + maintenanceCalls
            + " maintenanceAvgMs=" + maintenanceAvgMs.ToString("0.000")
            + " providers=" + providers
            + " plots=" + plots
            + " anchorPlantCalls=" + anchorPlantCalls
            + " anchorNoPlantCalls=" + anchorNoPlantCalls
            + " anchorNullBlockCalls=" + anchorNullBlockCalls
            + " heatScanCalls=" + heatScanCalls
            + " heatScanPositions=" + heatScanPositions
            + " heatPositionsPerSecond=" + heatPositionsPerSecond.ToString("0.00")
            + " heatScanTileEntityLookups=" + heatScanTileEntityLookups
            + " heatTeLookupsPerSecond=" + heatTeLookupsPerSecond.ToString("0.00")
            + " heatScanHits=" + heatScanHits
            + " heatCacheQueries=" + heatCacheQueries
            + " heatCacheHits=" + heatCacheHits
            + " heatCacheMisses=" + heatCacheMisses
            + " heatCacheInvalidations=" + heatCacheInvalidations
            + " heatRegistryQueries=" + heatRegistryQueries
            + " heatRegistryHits=" + heatRegistryHits
            + " heatRegistryMisses=" + heatRegistryMisses
            + " heatRegistryFallbackScans=" + heatRegistryFallbackScans
            + " heatRegistryObservedOn=" + heatRegistryObservedOn
            + " heatRegistryObservedOff=" + heatRegistryObservedOff
            + " waterScanCalls=" + waterScanCalls
            + " waterScanPositions=" + waterScanPositions
            + " waterScanTileEntityLookups=" + waterScanTileEntityLookups
            + " waterScanHits=" + waterScanHits
            + " waterCacheQueries=" + waterCacheQueries
            + " waterCacheHits=" + waterCacheHits
            + " waterCacheMisses=" + waterCacheMisses
            + " waterCacheInvalidations=" + waterCacheInvalidations
            + " waterRegistryQueries=" + waterRegistryQueries
            + " waterRegistryCandidates=" + waterRegistryCandidates
            + " waterRegistryTileEntityLookups=" + waterRegistryTileEntityLookups
            + " waterRegistryHits=" + waterRegistryHits
            + " waterRegistryMaintenanceQueries=" + waterRegistryMaintenanceQueries
            + " waterRegistryMaintenanceCandidates=" + waterRegistryMaintenanceCandidates
            + " waterRegistryMaintenanceTileEntityLookups=" + waterRegistryMaintenanceTileEntityLookups
            + " waterRegistryMaintenanceHits=" + waterRegistryMaintenanceHits
            + " waterRegistryConsumptionQueries=" + waterRegistryConsumptionQueries
            + " waterRegistryConsumptionCandidates=" + waterRegistryConsumptionCandidates
            + " waterRegistryConsumptionTileEntityLookups=" + waterRegistryConsumptionTileEntityLookups
            + " waterRegistryConsumptionHits=" + waterRegistryConsumptionHits
            + " waterRegistryPrunes=" + waterRegistryPrunes
            + " waterRegistryResolveCacheHits=" + waterRegistryResolveCacheHits
            + " managedAreaKB=" + processAreaManagedPositiveKb + "/-" + processAreaManagedNegativeKb
            + " managedMaintKB=" + areaMaintenanceManagedPositiveKb + "/-" + areaMaintenanceManagedNegativeKb
            + " managedWaterMaintKB=" + waterMaintenanceManagedPositiveKb + "/-" + waterMaintenanceManagedNegativeKb
            + " managedLightKB=" + lightEvaluateManagedPositiveKb + "/-" + lightEvaluateManagedNegativeKb
            + " managedThermalKB=" + thermalEvaluateManagedPositiveKb + "/-" + thermalEvaluateManagedNegativeKb
            + " lightExposureCacheQueries=" + lightExposureCacheQueries
            + " lightExposureCacheHits=" + lightExposureCacheHits
            + " lightExposureEvaluations=" + lightExposureEvaluations
            + " lightExposureSearches=" + lightExposureSearches
            + " lightExposureNodes=" + lightExposureNodes
            + " lightExposureTransparentCandidates=" + lightExposureTransparentCandidates
            + " lightExposureOpenResults=" + lightExposureOpenResults
            + " sunRefreshQueued=" + sunRefreshQueued
            + " sunRefreshCoalesced=" + sunRefreshCoalesced
            + " sunRefreshRebuilds=" + sunRefreshRebuilds
            + " sunRefreshCells=" + sunRefreshCells
            + " sunRefreshCellsPerSecond=" + sunRefreshCellsPerSecond.ToString("0.00")
            + " sunRefreshTotalMs=" + sunRefreshMs.ToString("0.000")
            + " sunRefreshAvgMs=" + sunRefreshAvgMs.ToString("0.000")
            + " visualRefreshQueued=" + visualRefreshQueued
            + " thermalExposureQueries=" + thermalExposureQueries
            + " thermalExposureCacheHits=" + thermalExposureCacheHits
            + " thermalExposureCacheHitRate=" + thermalExposureCacheHitRate.ToString("0.0")
            + " thermalExposureSearches=" + thermalExposureSearches
            + " thermalExposureNodes=" + thermalExposureNodes
            + " thermalExposureAvgNodes=" + thermalExposureAvgNodes.ToString("0.0")
            + " thermalExposureOpenResults=" + thermalExposureOpenResults
            + " thermalExposureClosedResults=" + thermalExposureClosedResults
            + " lightEvaluateCalls=" + lightEvaluateCalls
            + " lightEvaluateTotalMs=" + lightEvaluateMs.ToString("0.000")
            + " lightEvaluateAvgMs=" + lightEvaluateAvgMs.ToString("0.000")
            + " thermalEvaluateCalls=" + thermalEvaluateCalls
            + " thermalEvaluateTotalMs=" + thermalEvaluateMs.ToString("0.000")
            + " thermalEvaluateAvgMs=" + thermalEvaluateAvgMs.ToString("0.000")
            + " areaMaintenancePhaseCalls=" + areaMaintenancePhaseCalls
            + " areaMaintenancePhaseTotalMs=" + areaMaintenancePhaseMs.ToString("0.000")
            + " areaMaintenancePhaseAvgMs=" + areaMaintenancePhaseAvgMs.ToString("0.000")
            + " activeBlockLightPrecheckQueries=" + activeBlockLightPrecheckQueries
            + " activeBlockLightPrecheckPasses=" + activeBlockLightPrecheckPasses
            + " activeBlockLightPrecheckPassRate=" + activeBlockLightPrecheckPassRate.ToString("0.0")
            + " exposureInvalidationBlockChanges=" + exposureInvalidationBlockChanges
            + " exposureInvalidationSkippedPlantLike=" + exposureInvalidationSkippedPlantLike
            + " exposureInvalidationLightClears=" + exposureInvalidationLightClears
            + " exposureInvalidationThermalClears=" + exposureInvalidationThermalClears
            + " exposureInvalidationSunRefreshQueues=" + exposureInvalidationSunRefreshQueues
            + " lightProcessorRefreshSunlightCalls=" + lightProcessorRefreshSunlightCalls
            + " lightProcessorRefreshSunlightTotalMs=" + lightProcessorRefreshSunlightMs.ToString("0.000")
            + " lightProcessorRefreshSunlightAvgMs=" + lightProcessorRefreshSunlightAvgMs.ToString("0.000")
            + " lightProcessorRefreshLocalSunCalls=" + lightProcessorRefreshLocalSunCalls
            + " lightProcessorRefreshLocalSunTotalMs=" + lightProcessorRefreshLocalSunMs.ToString("0.000")
            + " lightProcessorRefreshLocalSunAvgMs=" + lightProcessorRefreshLocalSunAvgMs.ToString("0.000")
            + " lightProcessorRefreshLocalBlockCalls=" + lightProcessorRefreshLocalBlockCalls
            + " lightProcessorRefreshLocalBlockTotalMs=" + lightProcessorRefreshLocalBlockMs.ToString("0.000")
            + " lightProcessorRefreshLocalBlockAvgMs=" + lightProcessorRefreshLocalBlockAvgMs.ToString("0.000")
            + " lightProcessorSpreadSunCalls=" + lightProcessorSpreadSunCalls
            + " lightProcessorSpreadSunTotalMs=" + lightProcessorSpreadSunMs.ToString("0.000")
            + " lightProcessorSpreadSunAvgMs=" + lightProcessorSpreadSunAvgMs.ToString("0.000")
            + " lightProcessorSpreadBlockCalls=" + lightProcessorSpreadBlockCalls
            + " lightProcessorSpreadBlockTotalMs=" + lightProcessorSpreadBlockMs.ToString("0.000")
            + " lightProcessorSpreadBlockAvgMs=" + lightProcessorSpreadBlockAvgMs.ToString("0.000")
            + " lightProcessorUnspreadSunCalls=" + lightProcessorUnspreadSunCalls
            + " lightProcessorUnspreadSunTotalMs=" + lightProcessorUnspreadSunMs.ToString("0.000")
            + " lightProcessorUnspreadSunAvgMs=" + lightProcessorUnspreadSunAvgMs.ToString("0.000")
            + " lightProcessorUnspreadBlockCalls=" + lightProcessorUnspreadBlockCalls
            + " lightProcessorUnspreadBlockTotalMs=" + lightProcessorUnspreadBlockMs.ToString("0.000")
            + " lightProcessorUnspreadBlockAvgMs=" + lightProcessorUnspreadBlockAvgMs.ToString("0.000");
    }

    public static string BuildAnchorDiagnosticsReport()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.AppendLine("[AdvancedFarming AnchorDiag] anchorPlantCalls=" + s_anchorPlantCalls + " anchorNoPlantCalls=" + s_anchorNoPlantCalls + " anchorNullBlockCalls=" + s_anchorNullBlockCalls + " missingPlantTileEntityWakeSkips=" + s_missingPlantTileEntityWakeSkips);

        for (int i = 0; i < s_anchorNoPlantNames.Length; i++)
        {
            if (s_anchorNoPlantNames[i] == null)
                continue;

            sb.AppendLine("[AdvancedFarming AnchorDiag] noPlantBlock name=" + s_anchorNoPlantNames[i] + " count=" + s_anchorNoPlantCounts[i]);
        }

        return sb.ToString();
    }

    public static string BuildSamplingReport()
    {
        double frequency = Stopwatch.Frequency;
        double totalMs = frequency > 0d ? (double)s_totalElapsedStopwatchTicks * 1000d / frequency : 0d;
        double maxMs = frequency > 0d ? (double)s_maxElapsedStopwatchTicks * 1000d / frequency : 0d;
        double avgMs = s_areaCalls > 0L ? totalMs / (double)s_areaCalls : 0d;
        double sampleSeconds = frequency > 0d ? (double)(Stopwatch.GetTimestamp() - s_sampleStartTicks) / frequency : 0d;
        double anchorOnlyMs = frequency > 0d ? (double)s_anchorOnlyElapsedStopwatchTicks * 1000d / frequency : 0d;
        double maintenanceMs = frequency > 0d ? (double)s_areaMaintenanceElapsedStopwatchTicks * 1000d / frequency : 0d;
        double anchorOnlyAvgMs = s_anchorOnlyCalls > 0L ? anchorOnlyMs / (double)s_anchorOnlyCalls : 0d;
        double maintenanceAvgMs = s_areaMaintenanceCalls > 0L ? maintenanceMs / (double)s_areaMaintenanceCalls : 0d;
        double anchorOnlyMaxMs = frequency > 0d ? (double)s_anchorOnlyMaxElapsedStopwatchTicks * 1000d / frequency : 0d;
        double maintenanceMaxMs = frequency > 0d ? (double)s_areaMaintenanceMaxElapsedStopwatchTicks * 1000d / frequency : 0d;
        double callsPerSecond = sampleSeconds > 0d ? (double)s_areaCalls / sampleSeconds : 0d;
        double sunRefreshMs = frequency > 0d ? (double)s_sunRefreshElapsedStopwatchTicks * 1000d / frequency : 0d;
        double sunRefreshMaxMs = frequency > 0d ? (double)s_sunRefreshMaxElapsedStopwatchTicks * 1000d / frequency : 0d;
        double sunRefreshAvgMs = s_sunRefreshRebuilds > 0L ? sunRefreshMs / (double)s_sunRefreshRebuilds : 0d;
        double thermalExposureHitRate = s_thermalExposureQueries > 0L ? (double)s_thermalExposureCacheHits * 100d / (double)s_thermalExposureQueries : 0d;
        double thermalExposureAvgNodes = s_thermalExposureSearches > 0L ? (double)s_thermalExposureNodes / (double)s_thermalExposureSearches : 0d;
        double lightEvaluateMs = frequency > 0d ? (double)s_lightEvaluateElapsedStopwatchTicks * 1000d / frequency : 0d;
        double lightEvaluateAvgMs = s_lightEvaluateCalls > 0L ? lightEvaluateMs / (double)s_lightEvaluateCalls : 0d;
        double lightEvaluateMaxMs = frequency > 0d ? (double)s_lightEvaluateMaxStopwatchTicks * 1000d / frequency : 0d;
        double thermalEvaluateMs = frequency > 0d ? (double)s_thermalEvaluateElapsedStopwatchTicks * 1000d / frequency : 0d;
        double thermalEvaluateAvgMs = s_thermalEvaluateCalls > 0L ? thermalEvaluateMs / (double)s_thermalEvaluateCalls : 0d;
        double thermalEvaluateMaxMs = frequency > 0d ? (double)s_thermalEvaluateMaxStopwatchTicks * 1000d / frequency : 0d;
        double areaMaintenancePhaseMs = frequency > 0d ? (double)s_areaMaintenancePhaseElapsedStopwatchTicks * 1000d / frequency : 0d;
        double areaMaintenancePhaseAvgMs = s_areaMaintenancePhaseCalls > 0L ? areaMaintenancePhaseMs / (double)s_areaMaintenancePhaseCalls : 0d;
        double areaMaintenancePhaseMaxMs = frequency > 0d ? (double)s_areaMaintenancePhaseMaxStopwatchTicks * 1000d / frequency : 0d;
        double activeBlockLightPrecheckPassRate = s_activeBlockLightPrecheckQueries > 0L ? (double)s_activeBlockLightPrecheckPasses * 100d / (double)s_activeBlockLightPrecheckQueries : 0d;
        double lightProcessorRefreshSunlightMs = frequency > 0d ? (double)s_lightProcessorRefreshSunlightElapsedStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorRefreshSunlightAvgMs = s_lightProcessorRefreshSunlightCalls > 0L ? lightProcessorRefreshSunlightMs / (double)s_lightProcessorRefreshSunlightCalls : 0d;
        double lightProcessorRefreshSunlightMaxMs = frequency > 0d ? (double)s_lightProcessorRefreshSunlightMaxStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorRefreshLocalSunMs = frequency > 0d ? (double)s_lightProcessorRefreshLocalSunElapsedStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorRefreshLocalSunAvgMs = s_lightProcessorRefreshLocalSunCalls > 0L ? lightProcessorRefreshLocalSunMs / (double)s_lightProcessorRefreshLocalSunCalls : 0d;
        double lightProcessorRefreshLocalSunMaxMs = frequency > 0d ? (double)s_lightProcessorRefreshLocalSunMaxStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorRefreshLocalBlockMs = frequency > 0d ? (double)s_lightProcessorRefreshLocalBlockElapsedStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorRefreshLocalBlockAvgMs = s_lightProcessorRefreshLocalBlockCalls > 0L ? lightProcessorRefreshLocalBlockMs / (double)s_lightProcessorRefreshLocalBlockCalls : 0d;
        double lightProcessorRefreshLocalBlockMaxMs = frequency > 0d ? (double)s_lightProcessorRefreshLocalBlockMaxStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorSpreadSunMs = frequency > 0d ? (double)s_lightProcessorSpreadSunElapsedStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorSpreadSunAvgMs = s_lightProcessorSpreadSunCalls > 0L ? lightProcessorSpreadSunMs / (double)s_lightProcessorSpreadSunCalls : 0d;
        double lightProcessorSpreadSunMaxMs = frequency > 0d ? (double)s_lightProcessorSpreadSunMaxStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorSpreadBlockMs = frequency > 0d ? (double)s_lightProcessorSpreadBlockElapsedStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorSpreadBlockAvgMs = s_lightProcessorSpreadBlockCalls > 0L ? lightProcessorSpreadBlockMs / (double)s_lightProcessorSpreadBlockCalls : 0d;
        double lightProcessorSpreadBlockMaxMs = frequency > 0d ? (double)s_lightProcessorSpreadBlockMaxStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorUnspreadSunMs = frequency > 0d ? (double)s_lightProcessorUnspreadSunElapsedStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorUnspreadSunAvgMs = s_lightProcessorUnspreadSunCalls > 0L ? lightProcessorUnspreadSunMs / (double)s_lightProcessorUnspreadSunCalls : 0d;
        double lightProcessorUnspreadSunMaxMs = frequency > 0d ? (double)s_lightProcessorUnspreadSunMaxStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorUnspreadBlockMs = frequency > 0d ? (double)s_lightProcessorUnspreadBlockElapsedStopwatchTicks * 1000d / frequency : 0d;
        double lightProcessorUnspreadBlockAvgMs = s_lightProcessorUnspreadBlockCalls > 0L ? lightProcessorUnspreadBlockMs / (double)s_lightProcessorUnspreadBlockCalls : 0d;
        double lightProcessorUnspreadBlockMaxMs = frequency > 0d ? (double)s_lightProcessorUnspreadBlockMaxStopwatchTicks * 1000d / frequency : 0d;

        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[AdvancedFarming PerfSample] enabled=" + s_samplingEnabled + " sampleSeconds=" + sampleSeconds.ToString("0.00") + " areaCalls=" + s_areaCalls + " callsPerSecond=" + callsPerSecond.ToString("0.00"));
        sb.AppendLine("[AdvancedFarming PerfSample] totalAreaMs=" + totalMs.ToString("0.000") + " avgAreaMs=" + avgMs.ToString("0.000") + " maxAreaMs=" + maxMs.ToString("0.000"));
        sb.AppendLine("[AdvancedFarming PerfSample] anchorOnlyCalls=" + s_anchorOnlyCalls + " anchorOnlyTotalMs=" + anchorOnlyMs.ToString("0.000") + " anchorOnlyAvgMs=" + anchorOnlyAvgMs.ToString("0.000") + " anchorOnlyMaxMs=" + anchorOnlyMaxMs.ToString("0.000"));
        sb.AppendLine("[AdvancedFarming PerfSample] maintenanceCalls=" + s_areaMaintenanceCalls + " maintenanceTotalMs=" + maintenanceMs.ToString("0.000") + " maintenanceAvgMs=" + maintenanceAvgMs.ToString("0.000") + " maintenanceMaxMs=" + maintenanceMaxMs.ToString("0.000"));
        sb.AppendLine("[AdvancedFarming PerfSample] totalProviders=" + s_totalProviders + " totalPlots=" + s_totalPlots + " totalPlants=" + s_totalPlants + " plantSyncs=" + s_plantSyncs);
        sb.AppendLine("[AdvancedFarming PerfSample] anchorPlantCalls=" + s_anchorPlantCalls + " anchorNoPlantCalls=" + s_anchorNoPlantCalls + " anchorNullBlockCalls=" + s_anchorNullBlockCalls + " missingPlantTileEntityWakeSkips=" + s_missingPlantTileEntityWakeSkips);
        sb.AppendLine("[AdvancedFarming PerfSample] heatScanCalls=" + s_heatScanCalls + " heatScanPositions=" + s_heatScanPositions + " heatScanTileEntityLookups=" + s_heatScanTileEntityLookups + " heatScanHits=" + s_heatScanHits);
        sb.AppendLine("[AdvancedFarming PerfSample] heatCacheQueries=" + s_heatCacheQueries + " heatCacheHits=" + s_heatCacheHits + " heatCacheMisses=" + s_heatCacheMisses + " heatCacheInvalidations=" + s_heatCacheInvalidations);
        sb.AppendLine("[AdvancedFarming PerfSample] heatRegistryQueries=" + s_heatRegistryQueries + " heatRegistryHits=" + s_heatRegistryHits + " heatRegistryMisses=" + s_heatRegistryMisses + " heatRegistryFallbackScans=" + s_heatRegistryFallbackScans + " heatRegistryObservedOn=" + s_heatRegistryObservedOn + " heatRegistryObservedOff=" + s_heatRegistryObservedOff);
        sb.AppendLine("[AdvancedFarming PerfSample] waterScanCalls=" + s_waterScanCalls + " waterScanPositions=" + s_waterScanPositions + " waterScanTileEntityLookups=" + s_waterScanTileEntityLookups + " waterScanHits=" + s_waterScanHits + " waterCacheQueries=" + s_waterCacheQueries + " waterCacheHits=" + s_waterCacheHits + " waterCacheMisses=" + s_waterCacheMisses + " waterCacheInvalidations=" + s_waterCacheInvalidations);
        sb.AppendLine("[AdvancedFarming PerfSample] waterRegistryQueries=" + s_waterRegistryQueries + " waterRegistryCandidates=" + s_waterRegistryCandidates + " waterRegistryTileEntityLookups=" + s_waterRegistryTileEntityLookups + " waterRegistryHits=" + s_waterRegistryHits + " waterRegistryConsumptionQueries=" + s_waterRegistryConsumptionQueries + " waterRegistryConsumptionCandidates=" + s_waterRegistryConsumptionCandidates + " waterRegistryConsumptionTileEntityLookups=" + s_waterRegistryConsumptionTileEntityLookups + " waterRegistryConsumptionHits=" + s_waterRegistryConsumptionHits + " waterRegistryPrunes=" + s_waterRegistryPrunes + " waterRegistryResolveCacheHits=" + s_waterRegistryResolveCacheHits);
        sb.AppendLine("[AdvancedFarming PerfSample] managedMemoryPositiveKB area=" + (s_processAreaManagedPositiveBytes / 1024L) + " maintenance=" + (s_areaMaintenanceManagedPositiveBytes / 1024L) + " waterMaintenance=" + (s_waterMaintenanceManagedPositiveBytes / 1024L) + " light=" + (s_lightEvaluateManagedPositiveBytes / 1024L) + " thermal=" + (s_thermalEvaluateManagedPositiveBytes / 1024L));
        sb.AppendLine("[AdvancedFarming PerfSample] managedMemoryNegativeKB area=" + (s_processAreaManagedNegativeBytes / 1024L) + " maintenance=" + (s_areaMaintenanceManagedNegativeBytes / 1024L) + " waterMaintenance=" + (s_waterMaintenanceManagedNegativeBytes / 1024L) + " light=" + (s_lightEvaluateManagedNegativeBytes / 1024L) + " thermal=" + (s_thermalEvaluateManagedNegativeBytes / 1024L));
        sb.AppendLine("[AdvancedFarming PerfSample] lightExposureCacheQueries=" + s_lightExposureCacheQueries + " lightExposureCacheHits=" + s_lightExposureCacheHits + " lightExposureEvaluations=" + s_lightExposureEvaluations + " lightExposureSearches=" + s_lightExposureSearches + " lightExposureNodes=" + s_lightExposureNodes + " lightExposureTransparentCandidates=" + s_lightExposureTransparentCandidates + " lightExposureOpenResults=" + s_lightExposureOpenResults);
        sb.AppendLine("[AdvancedFarming PerfSample] sunRefreshQueued=" + s_sunRefreshQueued + " sunRefreshCoalesced=" + s_sunRefreshCoalesced + " sunRefreshRebuilds=" + s_sunRefreshRebuilds + " sunRefreshCells=" + s_sunRefreshCells + " sunRefreshTotalMs=" + sunRefreshMs.ToString("0.000") + " sunRefreshAvgMs=" + sunRefreshAvgMs.ToString("0.000") + " sunRefreshMaxMs=" + sunRefreshMaxMs.ToString("0.000") + " visualRefreshQueued=" + s_visualRefreshQueued);
        sb.AppendLine("[AdvancedFarming PerfSample] thermalExposureQueries=" + s_thermalExposureQueries + " thermalExposureCacheHits=" + s_thermalExposureCacheHits + " thermalExposureHitRate=" + thermalExposureHitRate.ToString("0.0") + " thermalExposureSearches=" + s_thermalExposureSearches + " thermalExposureNodes=" + s_thermalExposureNodes + " thermalExposureAvgNodes=" + thermalExposureAvgNodes.ToString("0.0") + " thermalExposureOpenResults=" + s_thermalExposureOpenResults + " thermalExposureClosedResults=" + s_thermalExposureClosedResults);
        sb.AppendLine("[AdvancedFarming PerfSample] lightEvaluateCalls=" + s_lightEvaluateCalls + " lightEvaluateTotalMs=" + lightEvaluateMs.ToString("0.000") + " lightEvaluateAvgMs=" + lightEvaluateAvgMs.ToString("0.000") + " lightEvaluateMaxMs=" + lightEvaluateMaxMs.ToString("0.000") + " thermalEvaluateCalls=" + s_thermalEvaluateCalls + " thermalEvaluateTotalMs=" + thermalEvaluateMs.ToString("0.000") + " thermalEvaluateAvgMs=" + thermalEvaluateAvgMs.ToString("0.000") + " thermalEvaluateMaxMs=" + thermalEvaluateMaxMs.ToString("0.000") + " areaMaintenancePhaseCalls=" + s_areaMaintenancePhaseCalls + " areaMaintenancePhaseTotalMs=" + areaMaintenancePhaseMs.ToString("0.000") + " areaMaintenancePhaseAvgMs=" + areaMaintenancePhaseAvgMs.ToString("0.000") + " areaMaintenancePhaseMaxMs=" + areaMaintenancePhaseMaxMs.ToString("0.000"));
        sb.AppendLine("[AdvancedFarming PerfSample] activeBlockLightPrecheckQueries=" + s_activeBlockLightPrecheckQueries + " activeBlockLightPrecheckPasses=" + s_activeBlockLightPrecheckPasses + " activeBlockLightPrecheckPassRate=" + activeBlockLightPrecheckPassRate.ToString("0.0"));
        sb.AppendLine("[AdvancedFarming PerfSample] exposureInvalidationBlockChanges=" + s_exposureInvalidationBlockChanges + " exposureInvalidationSkippedPlantLike=" + s_exposureInvalidationSkippedPlantLike + " exposureInvalidationLightClears=" + s_exposureInvalidationLightClears + " exposureInvalidationThermalClears=" + s_exposureInvalidationThermalClears + " exposureInvalidationSunRefreshQueues=" + s_exposureInvalidationSunRefreshQueues);
        sb.AppendLine("[AdvancedFarming PerfSample] lightProcessorRefreshSunlightCalls=" + s_lightProcessorRefreshSunlightCalls + " lightProcessorRefreshSunlightTotalMs=" + lightProcessorRefreshSunlightMs.ToString("0.000") + " lightProcessorRefreshSunlightAvgMs=" + lightProcessorRefreshSunlightAvgMs.ToString("0.000") + " lightProcessorRefreshSunlightMaxMs=" + lightProcessorRefreshSunlightMaxMs.ToString("0.000"));
        sb.AppendLine("[AdvancedFarming PerfSample] lightProcessorRefreshLocalSunCalls=" + s_lightProcessorRefreshLocalSunCalls + " lightProcessorRefreshLocalSunTotalMs=" + lightProcessorRefreshLocalSunMs.ToString("0.000") + " lightProcessorRefreshLocalSunAvgMs=" + lightProcessorRefreshLocalSunAvgMs.ToString("0.000") + " lightProcessorRefreshLocalSunMaxMs=" + lightProcessorRefreshLocalSunMaxMs.ToString("0.000") + " lightProcessorRefreshLocalBlockCalls=" + s_lightProcessorRefreshLocalBlockCalls + " lightProcessorRefreshLocalBlockTotalMs=" + lightProcessorRefreshLocalBlockMs.ToString("0.000") + " lightProcessorRefreshLocalBlockAvgMs=" + lightProcessorRefreshLocalBlockAvgMs.ToString("0.000") + " lightProcessorRefreshLocalBlockMaxMs=" + lightProcessorRefreshLocalBlockMaxMs.ToString("0.000"));
        sb.AppendLine("[AdvancedFarming PerfSample] lightProcessorSpreadSunCalls=" + s_lightProcessorSpreadSunCalls + " lightProcessorSpreadSunTotalMs=" + lightProcessorSpreadSunMs.ToString("0.000") + " lightProcessorSpreadSunAvgMs=" + lightProcessorSpreadSunAvgMs.ToString("0.000") + " lightProcessorSpreadSunMaxMs=" + lightProcessorSpreadSunMaxMs.ToString("0.000") + " lightProcessorSpreadBlockCalls=" + s_lightProcessorSpreadBlockCalls + " lightProcessorSpreadBlockTotalMs=" + lightProcessorSpreadBlockMs.ToString("0.000") + " lightProcessorSpreadBlockAvgMs=" + lightProcessorSpreadBlockAvgMs.ToString("0.000") + " lightProcessorSpreadBlockMaxMs=" + lightProcessorSpreadBlockMaxMs.ToString("0.000"));
        sb.AppendLine("[AdvancedFarming PerfSample] lightProcessorUnspreadSunCalls=" + s_lightProcessorUnspreadSunCalls + " lightProcessorUnspreadSunTotalMs=" + lightProcessorUnspreadSunMs.ToString("0.000") + " lightProcessorUnspreadSunAvgMs=" + lightProcessorUnspreadSunAvgMs.ToString("0.000") + " lightProcessorUnspreadSunMaxMs=" + lightProcessorUnspreadSunMaxMs.ToString("0.000") + " lightProcessorUnspreadBlockCalls=" + s_lightProcessorUnspreadBlockCalls + " lightProcessorUnspreadBlockTotalMs=" + lightProcessorUnspreadBlockMs.ToString("0.000") + " lightProcessorUnspreadBlockAvgMs=" + lightProcessorUnspreadBlockAvgMs.ToString("0.000") + " lightProcessorUnspreadBlockMaxMs=" + lightProcessorUnspreadBlockMaxMs.ToString("0.000"));
        sb.Append(AdvancedFarmingPerfSnapshotService.BuildAnchorDiagnosticsReport());
        sb.AppendLine("[AdvancedFarming PerfSample] use rbfarming perfsample reset before a clean run; use start, stop, report.");
        return sb.ToString();
    }


    private const int DefaultRadius = 24;
    private const int MaxRadius = 64;
    private const int ScanHeightBelow = 4;
    private const int ScanHeightAbove = 5;

    public static string BuildReport(WorldBase world, Vector3i center, int radius)
    {
        if (world == null)
            return "[AdvancedFarming Perf] world=null";

        if (radius < 1)
            radius = DefaultRadius;
        if (radius > MaxRadius)
            radius = MaxRadius;

        Stopwatch sw = Stopwatch.StartNew();

        int plants = 0;
        int fullyGrown = 0;
        int growing = 0;
        int plots = 0;
        int waterTanks = 0;
        int dewCollectors = 0;
        int otherWaterProviders = 0;
        int wateredPlants = 0;
        int unwateredPlants = 0;
        int validLight = 0;
        int invalidLight = 0;
        int validWater = 0;
        int invalidWater = 0;
        int validTemp = 0;
        int invalidTemp = 0;
        int wastelandBlocked = 0;
        int totalProviderWater = 0;
        int totalPlotWater = 0;
        int activeGrowthCandidates = 0;
        int skippedFullyGrown = 0;
        int nearCompleteNotFullyGrown = 0;
        int nearCompleteWaterPass = 0;
        int nearCompleteWaterFail = 0;
        int nearCompleteWatered = 0;
        int nearCompleteUnwatered = 0;
        int nearCompleteActiveElapsedZero = 0;
        ulong maxNearCompleteAccumulatedTicks = 0UL;
        string firstNearCompleteLine = string.Empty;

        ulong stageRate = (ulong)Math.Max(1, AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds() * 20);

        int minX = center.x - radius;
        int maxX = center.x + radius;
        int minZ = center.z - radius;
        int maxZ = center.z + radius;
        int minY = center.y - ScanHeightBelow;
        int maxY = center.y + ScanHeightAbove;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    TileEntity te = world.GetTileEntity(pos);

                    TileEntityPlantGrowingRebirth plant = te as TileEntityPlantGrowingRebirth;
                    if (plant != null)
                    {
                        plants++;
                        BlockValue blockValue = world.GetBlock(pos);
                        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
                        bool isMushroom = blockName.IndexOf("mushroom", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isFullyGrown = blockName.IndexOf("3Harvest", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isFullyGrown)
                        {
                            fullyGrown++;
                            skippedFullyGrown++;
                        }
                        else
                        {
                            growing++;
                            activeGrowthCandidates++;
                        }

                        if (plant.bWatered)
                            wateredPlants++;
                        else
                            unwateredPlants++;

                        PlantConditionSummary conditions = EvaluatePlantConditions(world, pos, blockValue, plant, isMushroom);
                        if (conditions.LightPass) validLight++; else invalidLight++;
                        if (conditions.WaterPass) validWater++; else invalidWater++;
                        if (conditions.TempPass) validTemp++; else invalidTemp++;
                        if (conditions.WastelandBlocked) wastelandBlocked++;

                        if (!isFullyGrown && stageRate > 0UL && plant.AccumulatedTicks >= stageRate - 20UL)
                        {
                            nearCompleteNotFullyGrown++;
                            if (conditions.WaterPass) nearCompleteWaterPass++; else nearCompleteWaterFail++;
                            if (plant.bWatered) nearCompleteWatered++; else nearCompleteUnwatered++;

                            ulong nowTicks = GameTimer.Instance.ticks;
                            ulong previousTicks = plant.GameTimerTicks;
                            ulong elapsedTicks = nowTicks >= previousTicks ? nowTicks - previousTicks : 0UL;
                            int activeElapsedSeconds = (int)(elapsedTicks / 20UL);
                            if (activeElapsedSeconds <= 0)
                                nearCompleteActiveElapsedZero++;

                            if (plant.AccumulatedTicks > maxNearCompleteAccumulatedTicks)
                                maxNearCompleteAccumulatedTicks = plant.AccumulatedTicks;

                            if (string.IsNullOrEmpty(firstNearCompleteLine))
                                firstNearCompleteLine = "[AdvancedFarming Perf] firstNearComplete pos=" + pos + " block=" + blockName + " accumulatedTicks=" + plant.AccumulatedTicks + "/" + stageRate + " watered=" + plant.bWatered + " waterPass=" + conditions.WaterPass + " activeElapsedSeconds=" + activeElapsedSeconds + " elapsedTicks=" + elapsedTicks;
                        }

                        continue;
                    }

                    TileEntityFarmPlotRebirth plot = te as TileEntityFarmPlotRebirth;
                    if (plot != null)
                    {
                        plots++;
                        totalPlotWater += plot.waterCount;
                        continue;
                    }

                    TileEntityWaterTankRebirth tank = te as TileEntityWaterTankRebirth;
                    if (tank != null)
                    {
                        BlockValue blockValue = world.GetBlock(pos);
                        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;

                        if (blockName.IndexOf("DewCollector", StringComparison.OrdinalIgnoreCase) >= 0)
                            dewCollectors++;
                        else if (blockName.IndexOf("WaterTank", StringComparison.OrdinalIgnoreCase) >= 0)
                            waterTanks++;
                        else
                            otherWaterProviders++;

                        totalProviderWater += tank.waterCount;
                    }
                }
            }
        }

        sw.Stop();

        int width = maxX - minX + 1;
        int depth = maxZ - minZ + 1;
        int height = maxY - minY + 1;
        int scannedPositions = width * depth * height;

        int perCropLegacyWaterChecks = plants * 2 * 9 * 9 * 2;
        int perCropLegacyHoverWaterChecks = plants * 2 * 9 * 9 * 2;

        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[AdvancedFarming Perf] center=" + center + " radius=" + radius + " scannedPositions=" + scannedPositions + " elapsedMs=" + sw.ElapsedMilliseconds);
        sb.AppendLine("[AdvancedFarming Perf] counts plants=" + plants + " growing=" + growing + " fullyGrown=" + fullyGrown + " plots=" + plots + " waterTanks=" + waterTanks + " dewCollectors=" + dewCollectors + " otherWaterProviders=" + otherWaterProviders);
        sb.AppendLine("[AdvancedFarming Perf] activeGrowthCandidates=" + activeGrowthCandidates + " skippedFullyGrownInert=" + skippedFullyGrown);
        sb.AppendLine("[AdvancedFarming Perf] water totalProviderWater=" + totalProviderWater + " totalPlotWater=" + totalPlotWater + " wateredPlants=" + wateredPlants + " unwateredPlants=" + unwateredPlants);
        sb.AppendLine("[AdvancedFarming Perf] conditions lightPass=" + validLight + " lightFail=" + invalidLight + " waterPass=" + validWater + " waterFail=" + invalidWater + " tempPass=" + validTemp + " tempFail=" + invalidTemp + " wastelandBlocked=" + wastelandBlocked);
        sb.AppendLine("[AdvancedFarming Perf] nearCompleteNotFullyGrown=" + nearCompleteNotFullyGrown + " waterPass=" + nearCompleteWaterPass + " waterFail=" + nearCompleteWaterFail + " watered=" + nearCompleteWatered + " unwatered=" + nearCompleteUnwatered + " activeElapsedZero=" + nearCompleteActiveElapsedZero + " maxAccumulatedTicks=" + maxNearCompleteAccumulatedTicks + "/" + stageRate);
        if (!string.IsNullOrEmpty(firstNearCompleteLine))
            sb.AppendLine(firstNearCompleteLine);
        sb.AppendLine("[AdvancedFarming Perf] activeLoadedPlantPath=ActiveAnchorDirect activeAnchorPriority=true activeWakeDelayTicks=20 activeMaxElapsedSeconds=1 displayPredictionCapTicks=20 sharedOfflineFallbackFromActive=false");
        sb.AppendLine("[AdvancedFarming Perf] currentWaterRange horizontalSquareRadius=4 diagonalIncluded=true yBelow=1 yAbove=0 checkedY=plantY-1..plantY");
        sb.AppendLine("[AdvancedFarming Perf] worstCaseLegacyWaterLookupsPerActiveSweep~" + perCropLegacyWaterChecks + " hoverWaterLookupsIfHoveringAll~" + perCropLegacyHoverWaterChecks);
        sb.AppendLine("[AdvancedFarming Perf] notes fullyGrown crops should not consume water; report command is one-shot and does not log per tick.");
        return sb.ToString();
    }

    private struct PlantConditionSummary
    {
        public bool LightPass;
        public bool WaterPass;
        public bool TempPass;
        public bool WastelandBlocked;
    }

    private static PlantConditionSummary EvaluatePlantConditions(WorldBase world, Vector3i pos, BlockValue blockValue, TileEntityPlantGrowingRebirth te, bool isMushroom)
    {
        PlantConditionSummary result = new PlantConditionSummary();

        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;
        bool isNight = GameManager.Instance != null && GameManager.Instance.World != null && !GameManager.Instance.World.IsDaytime();

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            pos,
            3,
            3,
            isMushroom,
            advancedFarming);

        bool hasSunLight = light.HasSunLight;
        bool hasBlockLight = light.HasBlockLight;
        bool hasOpenSky = light.HasOpenSky;

        result.LightPass = (((hasSunLight || hasOpenSky) && !isNight) || hasBlockLight || isMushroom);

        string biomeName = string.Empty;
        World realWorld = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (realWorld != null)
        {
            BiomeDefinition biome = realWorld.GetBiome(pos.x, pos.z);
            biomeName = biome != null ? biome.m_sBiomeName : string.Empty;
        }

        result.WastelandBlocked = !isMushroom && string.Equals(biomeName, "wasteland", StringComparison.OrdinalIgnoreCase);

        AdvancedFarmingTemperatureService.TemperatureResult temperature = AdvancedFarmingTemperatureService.Evaluate(
            world,
            pos,
            hasSunLight || hasOpenSky,
            advancedFarming);
        result.TempPass = !advancedFarming || isMushroom || temperature.Temperature >= 45f;

        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
        int depletion = RebirthUtilities.GetCropWaterDepletion(blockValue, temperature.Temperature);

        int tankWater = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayWaterTank", 4, 1, 0);
        int collectorWater = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayDewCollector", 4, 1, 0);
        int plotWater = 0;
        TileEntityFarmPlotRebirth plot = world.GetTileEntity(pos + Vector3i.down) as TileEntityFarmPlotRebirth;
        if (plot != null)
            plotWater = plot.waterCount;

        bool fullyGrown = blockName.IndexOf("3Harvest", StringComparison.OrdinalIgnoreCase) >= 0;
        result.WaterPass = fullyGrown || isMushroom || te.bWatered || tankWater >= depletion || collectorWater >= depletion || plotWater >= depletion;
        return result;
    }
}
#endif
