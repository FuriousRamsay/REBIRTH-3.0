using System;
using System.Collections.Generic;

#nullable disable

public enum AdvancedFarmingWaterProviderKind
{
    Unknown = 0,
    WaterTank = 1,
    DewCollector = 2,
    FarmPlot = 3
}

public sealed class AdvancedFarmingWaterProviderEntry
{
    public Vector3i Pos;
    public AdvancedFarmingWaterProviderKind Kind;
    public int BlockType;
    public TileEntityWaterTankRebirth Tank;
    public TileEntityFarmPlotRebirth Plot;
    public BlockValue BlockValue;
}

public static class AdvancedFarmingWaterProviderRegistry
{
    private static readonly Dictionary<long, List<ProviderEntry>> s_providersByChunk = new Dictionary<long, List<ProviderEntry>>(64);
    private static readonly Dictionary<long, long> s_posToChunk = new Dictionary<long, long>(256);
    private static readonly List<AdvancedFarmingWaterProviderEntry> s_collectStorageScratch = new List<AdvancedFarmingWaterProviderEntry>(64);
    private static readonly List<AdvancedFarmingWaterProviderEntry> s_collectPlotScratch = new List<AdvancedFarmingWaterProviderEntry>(128);
    private const int ProviderEntryResolveCacheSeconds = 8;

    private struct ProviderEntry
    {
        public Vector3i Pos;
        public AdvancedFarmingWaterProviderKind Kind;
        public int BlockType;
        public TileEntityWaterTankRebirth CachedTank;
        public TileEntityFarmPlotRebirth CachedPlot;
        public int CacheSecond;
    }

    public struct ProviderCountSummary
    {
        public int WaterTanks;
        public int DewCollectors;
        public int FarmPlots;
        public int Unknown;

        public int Total
        {
            get { return WaterTanks + DewCollectors + FarmPlots + Unknown; }
        }
    }

    public struct ProviderDebugEntry
    {
        public Vector3i Pos;
        public AdvancedFarmingWaterProviderKind Kind;
        public int BlockType;
        public string BlockName;
    }

    public static void Clear()
    {
        s_providersByChunk.Clear();
        s_posToChunk.Clear();
    }

    public static void RegisterProvider(WorldBase world, Vector3i pos, BlockValue blockValue)
    {
        AdvancedFarmingWaterProviderKind kind = ResolveKind(blockValue);
        if (kind == AdvancedFarmingWaterProviderKind.Unknown)
            return;

        if (kind == AdvancedFarmingWaterProviderKind.FarmPlot && !BlockFarmPlotRebirth.IsActivated(world, pos)) return;
        Register(pos, kind, blockValue.type);
    }

    public static void RegisterTileEntity(TileEntity te)
    {
        if (te == null)
            return;

        Vector3i pos = te.ToWorldPos();
        if (pos.x == 0 && pos.y == 0 && pos.z == 0)
            return;

        TileEntityFarmPlotRebirth plot = te as TileEntityFarmPlotRebirth;
        if (plot != null && plot.FarmingActivated)
        {
            Register(pos, AdvancedFarmingWaterProviderKind.FarmPlot, te.blockValue.type, null, plot);
            return;
        }

        TileEntityWaterTankRebirth tank = te as TileEntityWaterTankRebirth;
        if (tank != null)
        {
            Register(pos, ResolveStorageKind(te.blockValue), te.blockValue.type, tank, null);
            return;
        }
    }

    public static void RegisterKnownProvider(WorldBase world, Vector3i pos, TileEntity te, BlockValue blockValue)
    {
        if (te == null)
            return;

        if (te is TileEntityFarmPlotRebirth && ((TileEntityFarmPlotRebirth)te).FarmingActivated)
        {
            Register(pos, AdvancedFarmingWaterProviderKind.FarmPlot, blockValue.type, null, te as TileEntityFarmPlotRebirth);
            return;
        }

        if (te is TileEntityWaterTankRebirth)
        {
            Register(pos, ResolveStorageKind(blockValue), blockValue.type, te as TileEntityWaterTankRebirth, null);
            return;
        }
    }

    public static void InvalidateResolvedEntryAt(Vector3i pos)
    {
        long posKey = MakePosKey(pos);
        if (!s_posToChunk.TryGetValue(posKey, out long chunkKey))
            return;

        if (!s_providersByChunk.TryGetValue(chunkKey, out List<ProviderEntry> bucket))
            return;

        for (int i = 0; i < bucket.Count; i++)
        {
            ProviderEntry entry = bucket[i];
            if (!SamePos(entry.Pos, pos))
                continue;

            entry.CachedTank = null;
            entry.CachedPlot = null;
            entry.CacheSecond = 0;
            bucket[i] = entry;
            return;
        }
    }

    public static void EnsureRegisteredFromWorld(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return;

        TileEntity te = world.GetTileEntity(pos);
        if (te == null)
            return;

        RegisterKnownProvider(world, pos, te, world.GetBlock(pos));
    }

    public static void UnregisterProvider(Vector3i pos)
    {
        long posKey = MakePosKey(pos);
        if (!s_posToChunk.TryGetValue(posKey, out long chunkKey))
            return;

        s_posToChunk.Remove(posKey);

        if (!s_providersByChunk.TryGetValue(chunkKey, out List<ProviderEntry> bucket))
            return;

        for (int i = bucket.Count - 1; i >= 0; i--)
        {
            if (SamePos(bucket[i].Pos, pos))
                bucket.RemoveAt(i);
        }

        if (bucket.Count == 0)
            s_providersByChunk.Remove(chunkKey);
    }

    public static int CheckForWater(WorldBase world, Vector3i plantPos, string blockName, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        if (world == null || string.IsNullOrEmpty(blockName))
            return -1;

        AdvancedFarmingWaterProviderKind kind = ResolveKind(blockName);
        if (kind == AdvancedFarmingWaterProviderKind.FarmPlot)
            return GetDirectFarmPlotWater(world, plantPos);

        int targetType = ResolveBlockType(blockName);
        if (kind == AdvancedFarmingWaterProviderKind.Unknown && targetType < 0)
            return -1;

        int best = -1;
        int candidates = 0;
        int tileEntityLookups = 0;
        int minChunkX = (plantPos.x - blockRange) >> 4;
        int maxChunkX = (plantPos.x + blockRange) >> 4;
        int minChunkZ = (plantPos.z - blockRange) >> 4;
        int maxChunkZ = (plantPos.z + blockRange) >> 4;

        for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                if (!s_providersByChunk.TryGetValue(MakeChunkKey(cx, cz), out List<ProviderEntry> bucket))
                    continue;

                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    ProviderEntry entry = bucket[i];
                    if (!MatchesRequestedProvider(entry, kind, targetType))
                        continue;

                    if (!IsInWaterRange(plantPos, entry.Pos, blockRange, blockHeightMin, blockHeightMax))
                        continue;

                    candidates++;

                    TileEntityWaterTankRebirth tank;
                    TileEntityFarmPlotRebirth farmPlot;
                    bool cacheHit;
                    if (!TryResolveEntry(world, bucket, i, true, false, out entry, out tank, out farmPlot, out cacheHit))
                    {
                        if (!cacheHit)
                            tileEntityLookups++;
                        RemoveEntryAt(bucket, i, entry.Pos);
                        continue;
                    }

                    if (!cacheHit)
                        tileEntityLookups++;

                    if (tank.waterCount > best)
                        best = tank.waterCount;
                }
            }
        }

        AdvancedFarmingPerfSnapshotService.RecordWaterRegistryQuery(candidates, tileEntityLookups, best >= 0);
        return best;
    }


    public static AdvancedFarmingWaterProviderKind SelectConsumptionProvider(WorldBase world, Vector3i plantPos, int depletion)
    {
        if (world == null || depletion <= 0)
            return AdvancedFarmingWaterProviderKind.Unknown;

        AdvancedFarmingWaterProviderKind bestKind = AdvancedFarmingWaterProviderKind.Unknown;
        int bestWater = -1;
        const int blockRange = 4;
        int minChunkX = (plantPos.x - blockRange) >> 4;
        int maxChunkX = (plantPos.x + blockRange) >> 4;
        int minChunkZ = (plantPos.z - blockRange) >> 4;
        int maxChunkZ = (plantPos.z + blockRange) >> 4;

        for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                if (!s_providersByChunk.TryGetValue(MakeChunkKey(cx, cz), out List<ProviderEntry> bucket))
                    continue;

                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    ProviderEntry entry = bucket[i];
                    if (entry.Kind != AdvancedFarmingWaterProviderKind.WaterTank && entry.Kind != AdvancedFarmingWaterProviderKind.DewCollector)
                        continue;

                    if (!IsInWaterRange(plantPos, entry.Pos, blockRange, 1, 0))
                        continue;

                    TileEntityWaterTankRebirth tank;
                    TileEntityFarmPlotRebirth farmPlot;
                    bool cacheHit;
                    if (!TryResolveEntry(world, bucket, i, true, false, out entry, out tank, out farmPlot, out cacheHit))
                    {
                        RemoveEntryAt(bucket, i, entry.Pos);
                        continue;
                    }

                    if (tank.waterCount >= depletion && tank.waterCount > bestWater)
                    {
                        bestWater = tank.waterCount;
                        bestKind = entry.Kind;
                    }
                }
            }
        }

        if (bestKind != AdvancedFarmingWaterProviderKind.Unknown)
            return bestKind;

        Vector3i plotPos = plantPos + Vector3i.down;
        TileEntityFarmPlotRebirth plot = world.GetTileEntity(plotPos) as TileEntityFarmPlotRebirth;
        if (plot != null && plot.waterCount >= depletion)
            return AdvancedFarmingWaterProviderKind.FarmPlot;

        return AdvancedFarmingWaterProviderKind.Unknown;
    }

    public static bool TryDepleteBestWaterStorage(WorldBase world, Vector3i plantPos, int depletion)
    {
        if (world == null || depletion <= 0)
            return false;

        TileEntityWaterTankRebirth bestTank = null;
        Vector3i bestPos = Vector3i.zero;
        int bestWater = -1;
        int candidates = 0;
        int tileEntityLookups = 0;
        const int blockRange = 4;
        int minChunkX = (plantPos.x - blockRange) >> 4;
        int maxChunkX = (plantPos.x + blockRange) >> 4;
        int minChunkZ = (plantPos.z - blockRange) >> 4;
        int maxChunkZ = (plantPos.z + blockRange) >> 4;

        for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                if (!s_providersByChunk.TryGetValue(MakeChunkKey(cx, cz), out List<ProviderEntry> bucket))
                    continue;

                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    ProviderEntry entry = bucket[i];
                    if (entry.Kind != AdvancedFarmingWaterProviderKind.WaterTank && entry.Kind != AdvancedFarmingWaterProviderKind.DewCollector)
                        continue;

                    if (!IsInWaterRange(plantPos, entry.Pos, blockRange, 1, 0))
                        continue;

                    candidates++;

                    TileEntityWaterTankRebirth tank;
                    TileEntityFarmPlotRebirth farmPlot;
                    bool cacheHit;
                    if (!TryResolveEntry(world, bucket, i, true, false, out entry, out tank, out farmPlot, out cacheHit))
                    {
                        if (!cacheHit)
                            tileEntityLookups++;
                        RemoveEntryAt(bucket, i, entry.Pos);
                        continue;
                    }

                    if (!cacheHit)
                        tileEntityLookups++;

                    if (tank.waterCount >= depletion && tank.waterCount > bestWater)
                    {
                        bestTank = tank;
                        bestPos = entry.Pos;
                        bestWater = tank.waterCount;
                    }
                }
            }
        }

        AdvancedFarmingPerfSnapshotService.RecordWaterRegistryConsumption(candidates, tileEntityLookups, bestTank != null);

        if (bestTank == null)
            return false;

        bestTank.waterCount -= depletion;
        int now = RebirthUtilities.TotalGameSecondsPassed();
        bestTank.timeLapsed = now;
        bestTank.LastProcessedWorldSeconds = now;
        bestTank.setModified();
        AdvancedFarmingSyncService.SendWaterTank(bestPos, bestTank.waterCount);
        return true;
    }

    public static bool TryDepleteDirectFarmPlot(WorldBase world, Vector3i plantPos, int depletion)
    {
        if (world == null || depletion <= 0)
            return false;

        Vector3i plotPos = plantPos + Vector3i.down;
        TileEntityFarmPlotRebirth plot = world.GetTileEntity(plotPos) as TileEntityFarmPlotRebirth;
        if (plot != null && plot.FarmingActivated)
            RegisterKnownProvider(world, plotPos, plot, world.GetBlock(plotPos));

        AdvancedFarmingPerfSnapshotService.RecordWaterRegistryConsumption(1, 1, plot != null && plot.waterCount >= depletion);

        if (plot == null || plot.waterCount < depletion)
            return false;

        plot.waterCount -= depletion;
        plot.LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
        plot.setModified();
        AdvancedFarmingSyncService.SendFarmPlot(plotPos, plot.waterCount);
        return true;
    }

    public static int GetDirectFarmPlotWater(WorldBase world, Vector3i plantPos)
    {
        if (world == null)
            return -1;

        Vector3i plotPos = plantPos + Vector3i.down;
        TileEntityFarmPlotRebirth plot = world.GetTileEntity(plotPos) as TileEntityFarmPlotRebirth;
        if (plot != null && plot.FarmingActivated)
            RegisterKnownProvider(world, plotPos, plot, world.GetBlock(plotPos));

        AdvancedFarmingPerfSnapshotService.RecordWaterRegistryQuery(1, 1, plot != null);
        return plot != null ? plot.waterCount : -1;
    }

    public static void CollectProvidersAndPlotsInArea(
        WorldBase world,
        Vector3i center,
        int radius,
        int minY,
        int maxY,
        int maxStorageEntries,
        int maxPlotEntries,
        out List<AdvancedFarmingWaterProviderEntry> storageEntries,
        out List<AdvancedFarmingWaterProviderEntry> plotEntries)
    {
        s_collectStorageScratch.Clear();
        s_collectPlotScratch.Clear();
        storageEntries = s_collectStorageScratch;
        plotEntries = s_collectPlotScratch;

        if (world == null || radius < 0 || (maxStorageEntries == 0 && maxPlotEntries == 0))
            return;

        int candidates = 0;
        int tileEntityLookups = 0;
        int minChunkX = (center.x - radius) >> 4;
        int maxChunkX = (center.x + radius) >> 4;
        int minChunkZ = (center.z - radius) >> 4;
        int maxChunkZ = (center.z + radius) >> 4;

        for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                if (!s_providersByChunk.TryGetValue(MakeChunkKey(cx, cz), out List<ProviderEntry> bucket))
                    continue;

                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    ProviderEntry entry = bucket[i];
                    bool storage = entry.Kind == AdvancedFarmingWaterProviderKind.WaterTank || entry.Kind == AdvancedFarmingWaterProviderKind.DewCollector;
                    bool plot = entry.Kind == AdvancedFarmingWaterProviderKind.FarmPlot;
                    if (!storage && !plot)
                        continue;

                    if (entry.Pos.x < center.x - radius || entry.Pos.x > center.x + radius || entry.Pos.z < center.z - radius || entry.Pos.z > center.z + radius || entry.Pos.y < minY || entry.Pos.y > maxY)
                        continue;

                    if (storage && maxStorageEntries > 0 && storageEntries.Count >= maxStorageEntries)
                        continue;
                    if (plot && maxPlotEntries > 0 && plotEntries.Count >= maxPlotEntries)
                        continue;

                    candidates++;

                    TileEntityWaterTankRebirth tank;
                    TileEntityFarmPlotRebirth farmPlot;
                    bool cacheHit;
                    if (!TryResolveEntry(world, bucket, i, storage, plot, out entry, out tank, out farmPlot, out cacheHit))
                    {
                        if (!cacheHit)
                            tileEntityLookups++;
                        RemoveEntryAt(bucket, i, entry.Pos);
                        continue;
                    }

                    if (!cacheHit)
                        tileEntityLookups++;

                    AdvancedFarmingWaterProviderEntry publicEntry = new AdvancedFarmingWaterProviderEntry
                    {
                        Pos = entry.Pos,
                        Kind = entry.Kind,
                        BlockType = entry.BlockType,
                        Tank = tank,
                        Plot = farmPlot,
                        BlockValue = world.GetBlock(entry.Pos)
                    };

                    if (storage)
                        storageEntries.Add(publicEntry);
                    else
                        plotEntries.Add(publicEntry);
                }
            }
        }

        AdvancedFarmingPerfSnapshotService.RecordWaterRegistryMaintenanceCollect(candidates, tileEntityLookups, storageEntries.Count + plotEntries.Count > 0);
    }

    public static List<AdvancedFarmingWaterProviderEntry> CollectAllFarmPlots(WorldBase world)
    {
        List<AdvancedFarmingWaterProviderEntry> result = new List<AdvancedFarmingWaterProviderEntry>();
        if (world == null)
            return result;

        foreach (KeyValuePair<long, List<ProviderEntry>> pair in s_providersByChunk)
        {
            List<ProviderEntry> bucket = pair.Value;
            if (bucket == null)
                continue;

            for (int i = bucket.Count - 1; i >= 0; i--)
            {
                ProviderEntry entry = bucket[i];
                if (entry.Kind != AdvancedFarmingWaterProviderKind.FarmPlot)
                    continue;

                TileEntityWaterTankRebirth tank;
                TileEntityFarmPlotRebirth farmPlot;
                bool cacheHit;
                if (!TryResolveEntry(world, bucket, i, false, true, out entry, out tank, out farmPlot, out cacheHit))
                {
                    RemoveEntryAt(bucket, i, entry.Pos);
                    continue;
                }

                result.Add(new AdvancedFarmingWaterProviderEntry
                {
                    Pos = entry.Pos,
                    Kind = entry.Kind,
                    BlockType = entry.BlockType,
                    Plot = farmPlot,
                    BlockValue = world.GetBlock(entry.Pos)
                });
            }
        }

        return result;
    }


    public static List<AdvancedFarmingWaterProviderEntry> CollectProvidersInArea(WorldBase world, Vector3i center, int radius, int minY, int maxY, bool includeStorage, bool includePlots, int maxEntries)
    {
        List<AdvancedFarmingWaterProviderEntry> result = new List<AdvancedFarmingWaterProviderEntry>();
        if (world == null || radius < 0 || maxEntries == 0)
            return result;

        int candidates = 0;
        int tileEntityLookups = 0;
        int minChunkX = (center.x - radius) >> 4;
        int maxChunkX = (center.x + radius) >> 4;
        int minChunkZ = (center.z - radius) >> 4;
        int maxChunkZ = (center.z + radius) >> 4;

        for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                if (!s_providersByChunk.TryGetValue(MakeChunkKey(cx, cz), out List<ProviderEntry> bucket))
                    continue;

                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    if (maxEntries > 0 && result.Count >= maxEntries)
                        break;

                    ProviderEntry entry = bucket[i];
                    bool storage = entry.Kind == AdvancedFarmingWaterProviderKind.WaterTank || entry.Kind == AdvancedFarmingWaterProviderKind.DewCollector;
                    bool plot = entry.Kind == AdvancedFarmingWaterProviderKind.FarmPlot;

                    if ((!includeStorage || !storage) && (!includePlots || !plot))
                        continue;

                    if (entry.Pos.x < center.x - radius || entry.Pos.x > center.x + radius || entry.Pos.z < center.z - radius || entry.Pos.z > center.z + radius || entry.Pos.y < minY || entry.Pos.y > maxY)
                        continue;

                    candidates++;

                    TileEntityWaterTankRebirth tank;
                    TileEntityFarmPlotRebirth farmPlot;
                    bool cacheHit;
                    if (!TryResolveEntry(world, bucket, i, storage, plot, out entry, out tank, out farmPlot, out cacheHit))
                    {
                        if (!cacheHit)
                            tileEntityLookups++;
                        RemoveEntryAt(bucket, i, entry.Pos);
                        continue;
                    }

                    if (!cacheHit)
                        tileEntityLookups++;

                    result.Add(new AdvancedFarmingWaterProviderEntry
                    {
                        Pos = entry.Pos,
                        Kind = entry.Kind,
                        BlockType = entry.BlockType,
                        Tank = tank,
                        Plot = farmPlot,
                        BlockValue = world.GetBlock(entry.Pos)
                    });
                }
            }
        }

        AdvancedFarmingPerfSnapshotService.RecordWaterRegistryMaintenanceCollect(candidates, tileEntityLookups, result.Count > 0);
        return result;
    }

    public static ProviderCountSummary CountAllProviders()
    {
        ProviderCountSummary counts = new ProviderCountSummary();
        foreach (KeyValuePair<long, List<ProviderEntry>> pair in s_providersByChunk)
        {
            List<ProviderEntry> bucket = pair.Value;
            for (int i = 0; i < bucket.Count; i++)
                AddCount(ref counts, bucket[i].Kind);
        }

        return counts;
    }

    public static ProviderCountSummary CountProvidersInArea(WorldBase world, Vector3i center, int radius, int minY, int maxY, bool liveOnly)
    {
        ProviderCountSummary counts = new ProviderCountSummary();
        if (radius < 0)
            return counts;

        int minChunkX = (center.x - radius) >> 4;
        int maxChunkX = (center.x + radius) >> 4;
        int minChunkZ = (center.z - radius) >> 4;
        int maxChunkZ = (center.z + radius) >> 4;

        for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                if (!s_providersByChunk.TryGetValue(MakeChunkKey(cx, cz), out List<ProviderEntry> bucket))
                    continue;

                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    ProviderEntry entry = bucket[i];
                    if (entry.Pos.x < center.x - radius || entry.Pos.x > center.x + radius || entry.Pos.z < center.z - radius || entry.Pos.z > center.z + radius || entry.Pos.y < minY || entry.Pos.y > maxY)
                        continue;

                    if (liveOnly && world != null && !IsLiveProvider(world, entry))
                    {
                        RemoveEntryAt(bucket, i, entry.Pos);
                        continue;
                    }

                    AddCount(ref counts, entry.Kind);
                }
            }
        }

        return counts;
    }

    public static List<ProviderDebugEntry> CollectProviderDebugEntriesInArea(WorldBase world, Vector3i center, int radius, int minY, int maxY, bool liveOnly)
    {
        List<ProviderDebugEntry> result = new List<ProviderDebugEntry>();
        if (radius < 0)
            return result;

        int minChunkX = (center.x - radius) >> 4;
        int maxChunkX = (center.x + radius) >> 4;
        int minChunkZ = (center.z - radius) >> 4;
        int maxChunkZ = (center.z + radius) >> 4;

        for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
        {
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                if (!s_providersByChunk.TryGetValue(MakeChunkKey(cx, cz), out List<ProviderEntry> bucket))
                    continue;

                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    ProviderEntry entry = bucket[i];
                    if (entry.Pos.x < center.x - radius || entry.Pos.x > center.x + radius || entry.Pos.z < center.z - radius || entry.Pos.z > center.z + radius || entry.Pos.y < minY || entry.Pos.y > maxY)
                        continue;

                    if (liveOnly && world != null && !IsLiveProvider(world, entry))
                    {
                        RemoveEntryAt(bucket, i, entry.Pos);
                        continue;
                    }

                    BlockValue blockValue = world != null ? world.GetBlock(entry.Pos) : default(BlockValue);
                    string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
                    result.Add(new ProviderDebugEntry
                    {
                        Pos = entry.Pos,
                        Kind = entry.Kind,
                        BlockType = entry.BlockType,
                        BlockName = blockName
                    });
                }
            }
        }

        return result;
    }

    public static string BuildStatus()
    {
        ProviderCountSummary counts = CountAllProviders();

        return "[AdvancedFarming WaterRegistry] chunks=" + s_providersByChunk.Count
            + " providers=" + counts.Total
            + " waterTanks=" + counts.WaterTanks
            + " dewCollectors=" + counts.DewCollectors
            + " farmPlots=" + counts.FarmPlots
            + " unknown=" + counts.Unknown
            + " valueCache=false"
            + " positionRegistry=true";
    }

    private static void Register(Vector3i pos, AdvancedFarmingWaterProviderKind kind, int blockType)
    {
        Register(pos, kind, blockType, null, null);
    }

    private static void Register(Vector3i pos, AdvancedFarmingWaterProviderKind kind, int blockType, TileEntityWaterTankRebirth tank, TileEntityFarmPlotRebirth plot)
    {
        if (kind == AdvancedFarmingWaterProviderKind.Unknown)
            return;

        UnregisterProvider(pos);

        long chunkKey = MakeChunkKey(pos);
        if (!s_providersByChunk.TryGetValue(chunkKey, out List<ProviderEntry> bucket))
        {
            bucket = new List<ProviderEntry>(8);
            s_providersByChunk[chunkKey] = bucket;
        }

        bucket.Add(new ProviderEntry
        {
            Pos = pos,
            Kind = kind,
            BlockType = blockType,
            CachedTank = tank,
            CachedPlot = plot,
            CacheSecond = RebirthUtilities.TotalGameSecondsPassed()
        });

        s_posToChunk[MakePosKey(pos)] = chunkKey;
    }

    private static bool IsTileEntityLive(TileEntity te)
    {
        return te != null && te.GetChunk() != null && !te.IsRemoving;
    }

    private static bool TryResolveEntry(
        WorldBase world,
        List<ProviderEntry> bucket,
        int index,
        bool acceptStorage,
        bool acceptPlot,
        out ProviderEntry entry,
        out TileEntityWaterTankRebirth tank,
        out TileEntityFarmPlotRebirth farmPlot,
        out bool cacheHit)
    {
        entry = default(ProviderEntry);
        tank = null;
        farmPlot = null;
        cacheHit = false;

        if (world == null || bucket == null || index < 0 || index >= bucket.Count)
            return false;

        entry = bucket[index];
        bool wantsStorage = acceptStorage && (entry.Kind == AdvancedFarmingWaterProviderKind.WaterTank || entry.Kind == AdvancedFarmingWaterProviderKind.DewCollector);
        bool wantsPlot = acceptPlot && entry.Kind == AdvancedFarmingWaterProviderKind.FarmPlot;
        if (!wantsStorage && !wantsPlot)
            return false;

        int now = RebirthUtilities.TotalGameSecondsPassed();
        if (now - entry.CacheSecond >= 0 && now - entry.CacheSecond <= ProviderEntryResolveCacheSeconds)
        {
            if (wantsStorage && entry.CachedTank != null)
            {
                if (IsTileEntityLive(entry.CachedTank))
                {
                    tank = entry.CachedTank;
                    cacheHit = true;
                    AdvancedFarmingPerfSnapshotService.RecordWaterRegistryResolveCacheHit();
                    return true;
                }

                entry.CachedTank = null;
                entry.CacheSecond = 0;
                bucket[index] = entry;
            }

            if (wantsPlot && entry.CachedPlot != null)
            {
                if (IsTileEntityLive(entry.CachedPlot))
                {
                    farmPlot = entry.CachedPlot;
                    cacheHit = true;
                    AdvancedFarmingPerfSnapshotService.RecordWaterRegistryResolveCacheHit();
                    return true;
                }

                entry.CachedPlot = null;
                entry.CacheSecond = 0;
                bucket[index] = entry;
            }
        }

        TileEntity te = world.GetTileEntity(entry.Pos);
        tank = te as TileEntityWaterTankRebirth;
        farmPlot = te as TileEntityFarmPlotRebirth;

        if (wantsStorage && tank != null)
        {
            entry.CachedTank = tank;
            entry.CachedPlot = null;
            entry.CacheSecond = now;
            bucket[index] = entry;
            return true;
        }

        if (wantsPlot && farmPlot != null)
        {
            entry.CachedTank = null;
            entry.CachedPlot = farmPlot;
            entry.CacheSecond = now;
            bucket[index] = entry;
            return true;
        }

        entry.CachedTank = null;
        entry.CachedPlot = null;
        entry.CacheSecond = now;
        bucket[index] = entry;
        return false;
    }

    private static bool MatchesRequestedProvider(ProviderEntry entry, AdvancedFarmingWaterProviderKind kind, int targetType)
    {
        if (kind != AdvancedFarmingWaterProviderKind.Unknown)
            return entry.Kind == kind;

        return targetType >= 0 && entry.BlockType == targetType;
    }

    private static void RemoveEntryAt(List<ProviderEntry> bucket, int index, Vector3i pos)
    {
        if (bucket == null || index < 0 || index >= bucket.Count)
            return;

        bucket.RemoveAt(index);
        s_posToChunk.Remove(MakePosKey(pos));
        AdvancedFarmingPerfSnapshotService.RecordWaterRegistryPrune();
    }

    private static bool IsInWaterRange(Vector3i plantPos, Vector3i waterPos, int blockRange, int blockHeightMin, int blockHeightMax)
    {
        return Math.Abs(plantPos.x - waterPos.x) <= blockRange
            && Math.Abs(plantPos.z - waterPos.z) <= blockRange
            && waterPos.y >= plantPos.y - blockHeightMin
            && waterPos.y <= plantPos.y + blockHeightMax;
    }

    private static bool IsLiveProvider(WorldBase world, ProviderEntry entry)
    {
        if (world == null)
            return false;

        TileEntity te = world.GetTileEntity(entry.Pos);
        if (entry.Kind == AdvancedFarmingWaterProviderKind.FarmPlot)
        {
            if (!(te is TileEntityFarmPlotRebirth))
                return false;

            AdvancedFarmingWaterProviderKind blockKind = ResolveKind(world.GetBlock(entry.Pos));
            return blockKind == AdvancedFarmingWaterProviderKind.Unknown || blockKind == AdvancedFarmingWaterProviderKind.FarmPlot;
        }

        if (entry.Kind == AdvancedFarmingWaterProviderKind.WaterTank || entry.Kind == AdvancedFarmingWaterProviderKind.DewCollector)
        {
            if (!(te is TileEntityWaterTankRebirth))
                return false;

            AdvancedFarmingWaterProviderKind blockKind = ResolveKind(world.GetBlock(entry.Pos));
            return blockKind == AdvancedFarmingWaterProviderKind.Unknown || blockKind == entry.Kind;
        }

        return false;
    }

    private static void AddCount(ref ProviderCountSummary counts, AdvancedFarmingWaterProviderKind kind)
    {
        if (kind == AdvancedFarmingWaterProviderKind.WaterTank)
            counts.WaterTanks++;
        else if (kind == AdvancedFarmingWaterProviderKind.DewCollector)
            counts.DewCollectors++;
        else if (kind == AdvancedFarmingWaterProviderKind.FarmPlot)
            counts.FarmPlots++;
        else
            counts.Unknown++;
    }

    private static AdvancedFarmingWaterProviderKind ResolveKind(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block == null)
            return AdvancedFarmingWaterProviderKind.Unknown;

        if (block is BlockFarmPlotRebirth)
            return AdvancedFarmingWaterProviderKind.FarmPlot;

        if (block is BlockWaterTankRebirth)
            return ResolveStorageKindFromName(block.GetBlockName());

        return ResolveKind(block.GetBlockName());
    }

    private static AdvancedFarmingWaterProviderKind ResolveStorageKind(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block != null)
            return ResolveStorageKindFromName(block.GetBlockName());

        return AdvancedFarmingWaterProviderKind.WaterTank;
    }

    private static AdvancedFarmingWaterProviderKind ResolveStorageKindFromName(string blockName)
    {
        AdvancedFarmingWaterProviderKind kind = ResolveKind(blockName);
        return kind == AdvancedFarmingWaterProviderKind.DewCollector ? kind : AdvancedFarmingWaterProviderKind.WaterTank;
    }

    private static AdvancedFarmingWaterProviderKind ResolveKind(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return AdvancedFarmingWaterProviderKind.Unknown;

        if (blockName.IndexOf("DewCollector", StringComparison.OrdinalIgnoreCase) >= 0)
            return AdvancedFarmingWaterProviderKind.DewCollector;

        if (blockName.IndexOf("WaterTank", StringComparison.OrdinalIgnoreCase) >= 0)
            return AdvancedFarmingWaterProviderKind.WaterTank;

        if (blockName.IndexOf("FarmPlot", StringComparison.OrdinalIgnoreCase) >= 0 || blockName.IndexOf("farmPlot", StringComparison.OrdinalIgnoreCase) >= 0)
            return AdvancedFarmingWaterProviderKind.FarmPlot;

        return AdvancedFarmingWaterProviderKind.Unknown;
    }

    private static int ResolveBlockType(string blockName)
    {
        Block block = Block.GetBlockByName(blockName, false);
        return block != null ? block.blockID : -1;
    }

    private static long MakeChunkKey(Vector3i pos)
    {
        return MakeChunkKey(pos.x >> 4, pos.z >> 4);
    }

    private static long MakeChunkKey(int chunkX, int chunkZ)
    {
        unchecked
        {
            return ((long)chunkX << 32) ^ (uint)chunkZ;
        }
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

    private static bool SamePos(Vector3i a, Vector3i b)
    {
        return a.x == b.x && a.y == b.y && a.z == b.z;
    }
}
