using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Fast spatial gate for Advanced Farming.
///
/// A world area is considered active only when it contains a real REBIRTH farm
/// plot block. POI crops, decorative plants and base-game farm-plot-looking
/// blocks do not activate Advanced Farming. Global Harmony hooks consult this
/// registry before doing light, thermal, water or crop work.
/// </summary>
public static class AdvancedFarmingActiveAreaRegistry
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<long, List<Vector3i>> FarmPlotsByChunk =
        new Dictionary<long, List<Vector3i>>(64);
    private static readonly Dictionary<Vector3i, long> FarmPlotPositionToChunk =
        new Dictionary<Vector3i, long>(256);

    // Global lighting hooks can run extremely often and potentially off the
    // main thread. Plot mutations are rare, so maintain a copy-on-write coarse
    // chunk snapshot. Calls far from every farm return without taking Sync.
    private const int CoarseInfluenceRadiusBlocks = 96;
    private const int CoarseInfluenceRadiusChunks =
        (CoarseInfluenceRadiusBlocks + 15) / 16;
    private static volatile HashSet<long> s_coarseInfluenceChunks =
        new HashSet<long>();
    private static volatile HashSet<Vector3i> s_farmPlotPositionSnapshot =
        new HashSet<Vector3i>();
    private static volatile int s_farmPlotCount;
    private static volatile bool s_snapshotDirty;

    public static bool HasAnyFarmPlots
    {
        get { return s_farmPlotCount > 0; }
    }

    public static int FarmPlotCount
    {
        get { return s_farmPlotCount; }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            FarmPlotsByChunk.Clear();
            FarmPlotPositionToChunk.Clear();
            s_coarseInfluenceChunks = new HashSet<long>();
            s_farmPlotPositionSnapshot = new HashSet<Vector3i>();
            s_farmPlotCount = 0;
            s_snapshotDirty = false;
        }
    }

    public static void RegisterFarmPlot(Vector3i pos)
    {
        if (pos.y <= 0 || pos.y >= byte.MaxValue)
            return;

        lock (Sync)
        {
            if (FarmPlotPositionToChunk.ContainsKey(pos))
                return;

            long chunkKey = MakeChunkKey(World.toChunkXZ(pos.x), World.toChunkXZ(pos.z));
            List<Vector3i> bucket;
            if (!FarmPlotsByChunk.TryGetValue(chunkKey, out bucket))
            {
                bucket = new List<Vector3i>(4);
                FarmPlotsByChunk.Add(chunkKey, bucket);
            }

            bucket.Add(pos);
            FarmPlotPositionToChunk.Add(pos, chunkKey);
            s_farmPlotCount++;
            s_snapshotDirty = true;
        }
    }

    public static void UnregisterFarmPlot(Vector3i pos)
    {
        lock (Sync)
        {
            long chunkKey;
            if (!FarmPlotPositionToChunk.TryGetValue(pos, out chunkKey))
                return;

            FarmPlotPositionToChunk.Remove(pos);

            List<Vector3i> bucket;
            if (FarmPlotsByChunk.TryGetValue(chunkKey, out bucket))
            {
                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    if (SamePosition(bucket[i], pos))
                        bucket.RemoveAt(i);
                }

                if (bucket.Count == 0)
                    FarmPlotsByChunk.Remove(chunkKey);
            }

            if (s_farmPlotCount > 0)
                s_farmPlotCount--;
            s_snapshotDirty = true;
        }
    }


    /// <summary>Publishes at most one immutable coarse/position snapshot for all plot
    /// mutations observed since the previous game update. Queries fail open to the
    /// authoritative dictionaries while this publication is pending.</summary>
    public static void PublishPendingSnapshot()
    {
        if (!s_snapshotDirty)
            return;

        lock (Sync)
        {
            if (!s_snapshotDirty)
                return;

            RebuildCoarseInfluenceSnapshotLocked();
            s_snapshotDirty = false;
        }
    }

    /// <summary>
    /// Returns true only for a plant directly supported by a loaded REBIRTH farm
    /// plot block. A block name containing "farmPlot" is not sufficient.
    /// </summary>
    public static bool IsFullyGrownCrop(WorldBase world, Vector3i plantPos)
    {
        if (world == null)
            return false;

        BlockValue blockValue = world.GetBlock(plantPos);
        Block block = blockValue.Block;
        if (block == null)
            return false;

        string name = block.GetBlockName();
        return !string.IsNullOrEmpty(name) &&
            name.IndexOf("3Harvest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Returns true only for an immature crop directly above a registered
    /// REBIRTH farm plot. Fully grown harvest-stage crops are inert and are
    /// excluded from recurring crop, water, thermal, light and load
    /// reconciliation work until harvesting replaces the block with an
    /// immature stage.
    /// </summary>
    public static bool IsPlantProcessable(WorldBase world, Vector3i plantPos)
    {
        return IsPlantInActiveFarm(world, plantPos) &&
            !IsFullyGrownCrop(world, plantPos);
    }

    public static bool IsPlantInActiveFarm(WorldBase world, Vector3i plantPos)
    {
        if (world == null)
            return false;

        if (!AdvancedFarmingPlantOriginService.IsPlayerPlanted(world, plantPos))
            return false;
        Vector3i plotPos = plantPos + Vector3i.down;
        BlockValue plotValue = world.GetBlock(plotPos);
        if (!(plotValue.Block is BlockFarmPlotRebirth))
            return false;

        // The custom block type is the authoritative activation marker. The
        // tile entity can be instantiated later in the chunk-load order, so
        // requiring it here would briefly send valid crops through vanilla
        // scheduling and miss their Advanced Farming initialization.
        HashSet<Vector3i> positionSnapshot = s_farmPlotPositionSnapshot;
        if (positionSnapshot == null || !positionSnapshot.Contains(plotPos))
            RegisterFarmPlot(plotPos);
        return true;
    }

    public static bool IsHorizontalAreaActive(Vector3i center, int horizontalRadius)
    {
        if (!HasAnyFarmPlots)
            return false;

        if (horizontalRadius < 0)
            horizontalRadius = 0;

        if (!PassesCoarseChunkGate(center, horizontalRadius))
            return false;

        int minChunkX = World.toChunkXZ(center.x - horizontalRadius);
        int maxChunkX = World.toChunkXZ(center.x + horizontalRadius);
        int minChunkZ = World.toChunkXZ(center.z - horizontalRadius);
        int maxChunkZ = World.toChunkXZ(center.z + horizontalRadius);

        lock (Sync)
        {
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                {
                    List<Vector3i> bucket;
                    if (!FarmPlotsByChunk.TryGetValue(
                        MakeChunkKey(chunkX, chunkZ), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        Vector3i plotPos = bucket[i];
                        if (Math.Abs(plotPos.x - center.x) <= horizontalRadius &&
                            Math.Abs(plotPos.z - center.z) <= horizontalRadius)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public static bool IsAreaActive(
        Vector3i center,
        int horizontalRadius,
        int verticalRadius)
    {
        if (!HasAnyFarmPlots)
            return false;

        if (horizontalRadius < 0)
            horizontalRadius = 0;
        if (verticalRadius < 0)
            verticalRadius = 0;

        if (!PassesCoarseChunkGate(center, horizontalRadius))
            return false;

        int minChunkX = World.toChunkXZ(center.x - horizontalRadius);
        int maxChunkX = World.toChunkXZ(center.x + horizontalRadius);
        int minChunkZ = World.toChunkXZ(center.z - horizontalRadius);
        int maxChunkZ = World.toChunkXZ(center.z + horizontalRadius);

        lock (Sync)
        {
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                {
                    List<Vector3i> bucket;
                    if (!FarmPlotsByChunk.TryGetValue(
                        MakeChunkKey(chunkX, chunkZ), out bucket))
                    {
                        continue;
                    }

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        Vector3i plotPos = bucket[i];
                        if (Math.Abs(plotPos.x - center.x) <= horizontalRadius &&
                            Math.Abs(plotPos.z - center.z) <= horizontalRadius &&
                            Math.Abs(plotPos.y - center.y) <= verticalRadius)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    public static string BuildStatus()
    {
        return "[AdvancedFarming ActiveAreas] farmPlots=" + FarmPlotCount
            + " active=" + HasAnyFarmPlots;
    }

    private static bool PassesCoarseChunkGate(
        Vector3i center,
        int horizontalRadius)
    {
        // Queries larger than the precomputed radius are rare diagnostic paths;
        // let the exact lookup handle them rather than risk a false negative.
        if (horizontalRadius > CoarseInfluenceRadiusBlocks || s_snapshotDirty)
            return true;

        HashSet<long> coarse = s_coarseInfluenceChunks;
        return coarse != null && coarse.Contains(MakeChunkKey(
            World.toChunkXZ(center.x),
            World.toChunkXZ(center.z)));
    }

    private static void RebuildCoarseInfluenceSnapshotLocked()
    {
        HashSet<long> snapshot = new HashSet<long>();
        foreach (KeyValuePair<Vector3i, long> pair in FarmPlotPositionToChunk)
        {
            Vector3i pos = pair.Key;
            int centerChunkX = World.toChunkXZ(pos.x);
            int centerChunkZ = World.toChunkXZ(pos.z);
            for (int dz = -CoarseInfluenceRadiusChunks;
                 dz <= CoarseInfluenceRadiusChunks;
                 dz++)
            {
                for (int dx = -CoarseInfluenceRadiusChunks;
                     dx <= CoarseInfluenceRadiusChunks;
                     dx++)
                {
                    snapshot.Add(MakeChunkKey(
                        centerChunkX + dx,
                        centerChunkZ + dz));
                }
            }
        }

        s_coarseInfluenceChunks = snapshot;
        s_farmPlotPositionSnapshot =
            new HashSet<Vector3i>(FarmPlotPositionToChunk.Keys);
    }

    private static long MakeChunkKey(int chunkX, int chunkZ)
    {
        return ((long)chunkX << 32) ^ (uint)chunkZ;
    }

    private static bool SamePosition(Vector3i a, Vector3i b)
    {
        return a.x == b.x && a.y == b.y && a.z == b.z;
    }
}
