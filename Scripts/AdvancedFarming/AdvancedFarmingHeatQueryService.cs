using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public static class AdvancedFarmingHeatQueryService
{
    private struct HeatSourceEntry
    {
        public Vector3i Pos;
        public int BlockType;
        public int LastSeenWorldSeconds;
        public bool Burning;
    }

    private struct CachedHeatQueryResult
    {
        public Vector3i PlantPos;
        public int TargetType;
        public int Range;
        public int HeightMin;
        public int HeightMax;
        public int WorldSeconds;
        public int PolicyVersion;
        public bool Result;
    }

    // v125 correctness rollback: heat affects displayed crop temperature directly. A longer
    // per-position jittered result cache can make adjacent crops disagree after heat-source
    // state changes. Registered heat-source checks are cheap enough to keep this short.
    private const int HeatQueryCacheSeconds = 2;
    private const int HeatQueryCacheJitterSeconds = 0;
    private const int MaxHeatQueryCacheEntries = 4096;

    private static readonly Dictionary<long, HeatSourceEntry> s_heatSources = new Dictionary<long, HeatSourceEntry>(32);
    private static readonly Dictionary<long, CachedHeatQueryResult> s_heatQueryCache = new Dictionary<long, CachedHeatQueryResult>(256);
    private static readonly List<long> s_heatQueryCacheRemovalScratch = new List<long>(64);
    private static int s_policyVersion = 1;
    private static bool s_temperatureOverrideEnabled;
    private static float s_temperatureOverrideValue;

    public static bool TemperatureOverrideEnabled { get { return s_temperatureOverrideEnabled; } }
    public static float TemperatureOverrideValue { get { return s_temperatureOverrideValue; } }

    public static bool TryGetTemperatureOverride(out float value)
    {
        if (s_temperatureOverrideEnabled)
        {
            value = s_temperatureOverrideValue;
            return true;
        }

        value = 0f;
        return false;
    }

    public static string SetTemperatureOverride(float value)
    {
        if (s_temperatureOverrideEnabled && Math.Abs(s_temperatureOverrideValue - value) < 0.001f)
            return "[AdvancedFarming Temperature] override=true value=" + value.ToString("0.##");

        s_temperatureOverrideValue = value;
        s_temperatureOverrideEnabled = true;
        InvalidateConditionCaches("temperature override changed");
        return "[AdvancedFarming Temperature] override=true value=" + value.ToString("0.##");
    }

    public static string ClearTemperatureOverride()
    {
        if (!s_temperatureOverrideEnabled && Math.Abs(s_temperatureOverrideValue) < 0.001f)
            return "[AdvancedFarming Temperature] override=false";

        s_temperatureOverrideEnabled = false;
        s_temperatureOverrideValue = 0f;
        InvalidateConditionCaches("temperature override cleared");
        return "[AdvancedFarming Temperature] override=false";
    }

    public static string TemperatureStatus()
    {
        return "[AdvancedFarming Temperature] override=" + s_temperatureOverrideEnabled + " value=" + s_temperatureOverrideValue.ToString("0.##");
    }

    public static void InvalidateAll(string reason)
    {
        InvalidateConditionCaches(reason);
    }

    private static void InvalidateConditionCaches(string reason)
    {
        AdvancedFarmingHoverTextService.ClearConditionCache();
        s_policyVersion++;
        s_heatQueryCache.Clear();
        AdvancedFarmingPerfSnapshotService.RecordHeatCacheInvalidation();
    }

    public static void ClearRegisteredHeatSources()
    {
        s_heatSources.Clear();
        InvalidateConditionCaches("heat source registry cleared");
    }

    public static void ObserveWorkstation(WorldBase world, TileEntityWorkstation workstation)
    {
        if (world == null || workstation == null)
            return;

        Vector3i pos = workstation.ToWorldPos();
        ObserveWorkstationAt(world, pos, workstation.isBurning);
    }

    public static void ObserveWorkstationAt(WorldBase world, Vector3i pos, bool isBurning)
    {
        if (world == null)
            return;

        BlockValue blockValue = world.GetBlock(pos);
        int blockType = blockValue.type;
        if (!IsKnownHeatSourceBlock(blockType))
            return;

        long key = MakePosKey(pos);
        int now = RebirthUtilities.TotalGameSecondsPassed();

        if (isBurning)
        {
            if (s_heatSources.TryGetValue(key, out HeatSourceEntry existing)
                && existing.Burning
                && existing.BlockType == blockType)
            {
                return;
            }

            s_heatSources[key] = new HeatSourceEntry
            {
                Pos = pos,
                BlockType = blockType,
                LastSeenWorldSeconds = now,
                Burning = true
            };

            AdvancedFarmingPerfSnapshotService.RecordHeatSourceRegistryObserve(true);
            InvalidateConditionCaches("heat source changed to burning");
            return;
        }

        if (s_heatSources.Remove(key))
        {
            AdvancedFarmingPerfSnapshotService.RecordHeatSourceRegistryObserve(false);
            InvalidateConditionCaches("heat source changed to not burning");
        }
    }

    public static bool CheckForHeat(WorldBase world, Vector3i plantPos, string blockName, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        if (world == null || string.IsNullOrEmpty(blockName))
            return false;

        int targetType = RebirthUtilities.GetCachedHeatBlockIdForAdvancedFarming(blockName);
        if (targetType < 0)
        {
            AdvancedFarmingPerfSnapshotService.RecordHeatRegistryQuery();
            AdvancedFarmingPerfSnapshotService.RecordHeatRegistryMiss();
            return false;
        }

        // No fallback block scan here by design.
        // Heat is based only on observed/registered burning workstation tile entities.
        return CheckRegisteredHeatSources(world, plantPos, targetType, blockRange, blockHeightMin, blockHeightMax);
    }

    public static string BuildStatus()
    {
        return "[AdvancedFarming HeatRegistry] registeredHeatSources=" + s_heatSources.Count
            + " resultCache=true"
            + " resultCacheEntries=" + s_heatQueryCache.Count
            + " resultCacheSeconds=" + HeatQueryCacheSeconds
            + "+jitterSeconds=" + HeatQueryCacheJitterSeconds
            + " sourceTtlSeconds=none"
            + " policyVersion=" + s_policyVersion
            + " temperatureOverride=" + s_temperatureOverrideEnabled
            + " temperatureValue=" + s_temperatureOverrideValue.ToString("0.##");
    }

    private static bool CheckRegisteredHeatSources(WorldBase world, Vector3i plantPos, int targetType, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        AdvancedFarmingPerfSnapshotService.RecordHeatRegistryQuery();

        int now = RebirthUtilities.TotalGameSecondsPassed();
        long cacheKey = MakeHeatQueryCacheKey(plantPos, targetType, blockRange, blockHeightMin, blockHeightMax);
        int cachedAge = 0;
        if (s_heatQueryCache.TryGetValue(cacheKey, out CachedHeatQueryResult cached)
            && cached.PolicyVersion == s_policyVersion
            && (cachedAge = now - cached.WorldSeconds) >= 0
            && cachedAge <= GetHeatQueryCacheLifetimeSeconds(cached.PlantPos))
        {
            if (cached.Result)
                AdvancedFarmingPerfSnapshotService.RecordHeatRegistryHit();
            else
                AdvancedFarmingPerfSnapshotService.RecordHeatRegistryMiss();

            return cached.Result;
        }

        if (s_heatSources.Count == 0)
        {
            AdvancedFarmingPerfSnapshotService.RecordHeatRegistryMiss();
            StoreHeatQueryCache(cacheKey, plantPos, targetType, blockRange, blockHeightMin, blockHeightMax, now, false);
            return false;
        }

        int minY = plantPos.y - blockHeightMin;
        int maxY = plantPos.y + blockHeightMax;
        List<long> stale = null;

        foreach (KeyValuePair<long, HeatSourceEntry> pair in s_heatSources)
        {
            HeatSourceEntry source = pair.Value;
            if (!source.Burning || source.BlockType != targetType)
                continue;

            if (source.Pos.y < minY || source.Pos.y > maxY)
                continue;

            int dx = Math.Abs(source.Pos.x - plantPos.x);
            int dz = Math.Abs(source.Pos.z - plantPos.z);
            if (dx > blockRange || dz > blockRange)
                continue;

            if (!IsStillBurningHeatSource(world, source.Pos, targetType))
            {
                if (stale == null)
                    stale = new List<long>();
                stale.Add(pair.Key);
                continue;
            }

            AdvancedFarmingPerfSnapshotService.RecordHeatRegistryHit();
            StoreHeatQueryCache(cacheKey, plantPos, targetType, blockRange, blockHeightMin, blockHeightMax, now, true);
            return true;
        }

        if (stale != null)
        {
            for (int i = 0; i < stale.Count; i++)
                s_heatSources.Remove(stale[i]);

            InvalidateConditionCaches("stale heat source pruned");
        }

        AdvancedFarmingPerfSnapshotService.RecordHeatRegistryMiss();
        StoreHeatQueryCache(cacheKey, plantPos, targetType, blockRange, blockHeightMin, blockHeightMax, now, false);
        return false;
    }

    private static void StoreHeatQueryCache(long cacheKey, Vector3i plantPos, int targetType, int blockRange, int blockHeightMin, int blockHeightMax, int now, bool result)
    {
        EvictHeatQueryCacheIfNeeded(now);
        s_heatQueryCache[cacheKey] = new CachedHeatQueryResult
        {
            PlantPos = plantPos,
            TargetType = targetType,
            Range = blockRange,
            HeightMin = blockHeightMin,
            HeightMax = blockHeightMax,
            WorldSeconds = now,
            PolicyVersion = s_policyVersion,
            Result = result
        };
    }

    private static void EvictHeatQueryCacheIfNeeded(int now)
    {
        if (s_heatQueryCache.Count < MaxHeatQueryCacheEntries)
            return;

        s_heatQueryCacheRemovalScratch.Clear();
        foreach (KeyValuePair<long, CachedHeatQueryResult> pair in s_heatQueryCache)
        {
            int age = now - pair.Value.WorldSeconds;
            if (age < 0 || age > GetHeatQueryCacheLifetimeSeconds(pair.Value.PlantPos))
                s_heatQueryCacheRemovalScratch.Add(pair.Key);
        }

        for (int i = 0; i < s_heatQueryCacheRemovalScratch.Count; i++)
            s_heatQueryCache.Remove(s_heatQueryCacheRemovalScratch[i]);

        s_heatQueryCacheRemovalScratch.Clear();

        if (s_heatQueryCache.Count >= MaxHeatQueryCacheEntries)
            s_heatQueryCache.Clear();
    }

    private static int GetHeatQueryCacheLifetimeSeconds(Vector3i plantPos)
    {
        return HeatQueryCacheSeconds + PositiveModulo(HashPosition(plantPos) ^ 0x68127d, HeatQueryCacheJitterSeconds);
    }

    private static long MakeHeatQueryCacheKey(Vector3i plantPos, int targetType, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        unchecked
        {
            long key = 1469598103934665603L;
            key = (key ^ plantPos.x) * 1099511628211L;
            key = (key ^ plantPos.y) * 1099511628211L;
            key = (key ^ plantPos.z) * 1099511628211L;
            key = (key ^ targetType) * 1099511628211L;
            key = (key ^ blockRange) * 1099511628211L;
            key = (key ^ blockHeightMin) * 1099511628211L;
            key = (key ^ blockHeightMax) * 1099511628211L;
            return key;
        }
    }

    private static int PositiveModulo(int value, int divisor)
    {
        if (divisor <= 0)
            return 0;

        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static int HashPosition(Vector3i pos)
    {
        unchecked
        {
            int hash = 216613626;
            hash = (hash ^ pos.x) * 16777619;
            hash = (hash ^ pos.y) * 16777619;
            hash = (hash ^ pos.z) * 16777619;
            return hash;
        }
    }

    private static bool IsStillBurningHeatSource(WorldBase world, Vector3i pos, int targetType)
    {
        if (world == null)
            return false;

        BlockValue blockValue = world.GetBlock(pos);
        if (blockValue.type != targetType)
            return false;

        TileEntityWorkstation workstation = world.GetTileEntity(pos) as TileEntityWorkstation;
        return workstation != null && workstation.isBurning;
    }

    private static bool IsKnownHeatSourceBlock(int blockType)
    {
        int campfire = RebirthUtilities.GetCachedHeatBlockIdForAdvancedFarming("campfire");
        if (blockType == campfire)
            return true;

        int stove = RebirthUtilities.GetCachedHeatBlockIdForAdvancedFarming("cntWoodBurningStove");
        if (blockType == stove)
            return true;

        int greenhouse = RebirthUtilities.GetCachedHeatBlockIdForAdvancedFarming("rebirthGreenhouseHeatStove");
        return blockType == greenhouse;
    }

    private static long MakePosKey(Vector3i pos)
    {
        unchecked
        {
            long key = 1469598103934665603L;
            key = (key ^ pos.x) * 1099511628211L;
            key = (key ^ pos.y) * 1099511628211L;
            key = (key ^ pos.z) * 1099511628211L;
            return key;
        }
    }
}
