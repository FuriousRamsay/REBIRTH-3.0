using System; using System.Collections.Generic;
static class Before {
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
    }public static HashSet<long> Coarse => s_coarseInfluenceChunks; public static HashSet<Vector3i> Positions => s_farmPlotPositionSnapshot; public static void Rebuild()=>RebuildCoarseInfluenceSnapshotLocked();}