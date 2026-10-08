using System;
using System.Collections.Generic;
using System.Diagnostics;

#nullable disable

/// <summary>
/// Runtime light-opacity bridge for Advanced Farming.
///
/// Vanilla LightProcessor uses Block.list[id].lightOpacity, which is a static block-type value.
/// Composite/model-entity covers such as doors and curtains/drapes can change their effective occlusion
/// by tile-entity state, rotation, and mesh damage state without changing block id or static
/// LightOpacity. This service supplies the missing per-instance opacity to the patched
/// LightProcessor while preserving vanilla behavior for ordinary blocks.
/// </summary>
public static class AdvancedFarmingDynamicLightOpacityService
{
    private const int FullSunBlockOpacity = 15;
    // 6 plus the light flood's normal one-step falloff leaves the first interior cell
    // at SUN 8 from a 15-strength exterior source. That is the Advanced Farming
    // minimum natural sunlight threshold, so one half curtain dims light but still
    // counts as light-through.
    private const int HalfCurtainSunBlockOpacity = 6;
    private const int DoorStateRefreshRadius = 5;
    // SUN from a ceiling hatch/skylight propagates downward much farther than upward.
    // Keep the automatic refresh volume asymmetric: a small upward seed margin and a larger
    // downward recompute range for stacked greenhouse rooms / shaft farms. The pending queue stores
    // the downward vertical radius; RebuildSunlightVolumeAround derives the upward radius from it.
    private const int DoorStateRefreshVerticalRadius = 8;
    private const int DoorStateRefreshUpRadius = 2;
    private const int MaxCoalescedRefreshRadius = 16;
    private const int MaxCoalescedRefreshVerticalRadius = 14;
    private const int MaxCoalescedRefreshUpRadius = 3;
    private const int DoorStateRefreshCoalesceMilliseconds = 150;
    private const int DoorStateRefreshMaxDebounceMilliseconds = 500;

    // Persisted chunk SUN values were calculated before Advanced Farming's runtime generated-shape
    // opacity was necessarily applied. Crop tile entities queue one delayed reconciliation per
    // loaded crop chunk so saved/received SUN values are rebuilt after chunk lighting settles.
    private const int LoadedCropChunkRefreshRadius = 12;
    private const int LoadedCropChunkRefreshVerticalRadius = 12;
    private const int LoadedCropChunkRefreshDelayMilliseconds = 1500;
    private const int LoadedCropChunkRefreshLatestMilliseconds = 10000;
    private const int LoadedCropOverheadProbeHeight = 32;

    private enum NaturalLightRule : byte
    {
        Compute = 0,
        AlwaysBlock = 1,
        AlwaysPass = 2,
        DoorStateDependent = 3
    }

    private enum DoorPlacementPlane : byte
    {
        Unknown = 0,
        FloorCeiling = 1,
        Wall = 2
    }

    private struct PendingSunlightRefresh
    {
        public Vector3i Pos;
        public int Radius;
        public int VerticalRadius;
        public int DueWorldSecond;
        public long QueuedStopwatchTicks;
        public long DueStopwatchTicks;
        public long LatestStopwatchTicks;
        public bool WaitForChunkLightingReady;
    }

    private struct NaturalLightDescriptor
    {
        public bool Initialized;
        public NaturalLightRule Rule;
        public bool CurtainOrBlindLike;
        public bool HalfCurtainLike;
        public bool ShutterLike;
        public bool HatchLike;
        public bool CenteredNonGlassNaturalLightCover;
        public bool DoorLike;
        public bool TransparentWindowLike;
        public bool CoverLike;
        public int DamageStateLightPassIndex;
    }

    // 64 is only an initial allocation hint/diagnostic batch size. It is not a
    // correctness cap: world mutations must never be discarded because the queue is busy.
    private const int PendingSunlightRefreshInitialCapacity = 64;
    private static readonly List<PendingSunlightRefresh> s_pendingSunlightRefreshes = new List<PendingSunlightRefresh>(PendingSunlightRefreshInitialCapacity);
    private static readonly Dictionary<int, NaturalLightDescriptor> s_naturalLightDescriptorByBlockId = new Dictionary<int, NaturalLightDescriptor>(1024);

    // Crop OnLoad can fire for hundreds of world-generated/POI crop tile entities
    // while a player flies through new chunks. Persisted SUN reconciliation is only
    // needed where a crop can actually be under a roof/cover. Cache small 4x4 crop
    // subcells after a cheap overhead probe and schedule at most one rebuild per
    // chunk/vertical band for the entire world session.
    private static readonly HashSet<long> s_evaluatedLoadedCropSubcells = new HashSet<long>();
    private static readonly HashSet<long> s_scheduledLoadedCropChunkBands = new HashSet<long>();

    // SUN rebuilds are main-thread and non-reentrant. Reuse scratch storage to avoid per-rebuild
    // byte/bool/Queue allocations during door spam, explosions, or rapid shape placement tests.
    private const byte CoverFaceOpacityUnset = byte.MaxValue;
    private const byte CachedRuleCompute = (byte)NaturalLightRule.Compute;
    private const byte CachedRuleAlwaysBlock = (byte)NaturalLightRule.AlwaysBlock;
    private const byte CachedRuleAlwaysPass = (byte)NaturalLightRule.AlwaysPass;
    private const byte CachedRuleDoorStateDependent = (byte)NaturalLightRule.DoorStateDependent;

    private static byte[] s_rebuildNewSun;
    private static bool[] s_rebuildQueued;
    private static bool[] s_rebuildSealedSunCells;
    private static int[] s_rebuildQueue;
    private static BlockValue[] s_rebuildBlockValues;
    private static int[] s_rebuildBaseOpacity;
    private static byte[] s_rebuildNaturalLightRule;
    private static byte[] s_rebuildSourceFaceOpacity;
    private static byte[] s_rebuildTargetFaceOpacity;
    private static bool s_refreshingCropLightStatesAfterSunRebuild;

    public static int GetVerticalSunOpacity(IChunkAccess chunkAccess, Chunk chunk, int localX, int y, int localZ)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return 0;

        if (chunk == null || y < 0 || y > byte.MaxValue)
            return 0;

        Vector3i toPos = new Vector3i(chunk.GetBlockWorldPosX(localX), y, chunk.GetBlockWorldPosZ(localZ));
        BlockValue toValue = chunk.GetBlock(localX, y, localZ);
        Vector3i fromPos = new Vector3i(toPos.x, y + 1, toPos.z);
        BlockValue fromValue = y < byte.MaxValue ? GetBlockAt(chunkAccess, fromPos) : BlockValue.Air;
        return GetEdgeOpacity(chunkAccess, fromPos, fromValue, toPos, toValue, 0, -1, 0, GetBaseOpacity(toValue));
    }

    public static int GetLightStepOpacity(IChunkAccess chunkAccess, Vector3i fromPos, BlockValue fromValue, Vector3i toPos, BlockValue toValue)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return GetBaseOpacity(toValue);

        int dx = Math.Sign(toPos.x - fromPos.x);
        int dy = Math.Sign(toPos.y - fromPos.y);
        int dz = Math.Sign(toPos.z - fromPos.z);
        return GetEdgeOpacity(chunkAccess, fromPos, fromValue, toPos, toValue, dx, dy, dz, GetBaseOpacity(toValue));
    }

    public static int GetLightStepOpacity(IChunkAccess chunkAccess, Vector3i fromPos, Vector3i toPos)
    {
        BlockValue fromValue = GetBlockAt(chunkAccess, fromPos);
        BlockValue toValue = GetBlockAt(chunkAccess, toPos);
        return GetLightStepOpacity(chunkAccess, fromPos, fromValue, toPos, toValue);
    }

    public static bool HasDynamicLightOpacity(IBlockAccess world, Vector3i pos, BlockValue value)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return false;

        Block block = value.Block;
        if (block == null)
            return false;

        int shapeOpacity;
        bool shapeThermalPassable;
        string shapeCategory;
        if (AdvancedFarmingShapeOpacityPolicy.TryGetGeneratedShapePolicy(block, out shapeOpacity, out shapeThermalPassable, out shapeCategory))
            return true;

        NaturalLightRule rule = ResolveNaturalLightRule(block);
        if (rule == NaturalLightRule.AlwaysBlock || rule == NaturalLightRule.AlwaysPass || rule == NaturalLightRule.DoorStateDependent)
            return true;

        return IsCoverLikeByData(block);
    }

    public static void RefreshSunlightAround(Vector3i worldPos, int horizontalRadius)
    {
        // Rebuild the actual SUN values in a local volume. This is deliberately the
        // foundational light-map path: opaque prefab/composite covers must change the
        // same chunk SUN values that full blocks change, instead of being compensated
        // later in crop-only logic.
        RebuildSunlightVolumeAround(worldPos, horizontalRadius, MaxCoalescedRefreshVerticalRadius);
    }

    public static void RefreshSunlightAroundDoorState(Vector3i worldPos, int horizontalRadius)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled ||
            !AdvancedFarmingActiveAreaRegistry.IsAreaActive(
                worldPos, Math.Max(8, horizontalRadius + 8), 32))
        {
            return;
        }

        // Guard against invalid tile-entity feature positions. Some door-feature callbacks can
        // report y=0 even when the visible block is above ground; rebuilding a SUN volume there is
        // both wrong and very expensive because the direct-sun pass scans from world top to y=1.
        // The Harmony patch now resolves TEFeatureDoor through its parent tile entity, but keep
        // this defensive guard for placement/removal edge cases.
        if (worldPos.y <= 0 || worldPos.y >= byte.MaxValue)
            return;

        // Door SetOpen can fire repeatedly while the player toggles several nearby doors, and
        // block-change callbacks can observe the same interaction. Do not rebuild synchronously
        // on the door interaction stack, because the measured rebuild cost can exceed a frame.
        //
        // Queue for the next GameUpdate pump instead of delaying by a whole world second. That
        // preserves coalescing and avoids interaction-thread stalls, while removing the visible
        // delay introduced by the v21 one-second deferred refresh.
        int radius = horizontalRadius;
        if (radius <= 0 || radius > DoorStateRefreshRadius)
            radius = DoorStateRefreshRadius;

        QueueSunlightRefresh(worldPos, radius, DoorStateRefreshVerticalRadius, 0);

        // v94: the SUN rebuild fixes gameplay light/temperature, but model/composite
        // covers such as hatches and vault doors can retain stale _MacroAO render values
        // until a reload. Queue a small neighborhood, not just the interaction cell, because
        // multiblock/composite vault doors can report the state change from a child cell while
        // the live BlockEntityData/UpdateLight lives on the master cell.
        AdvancedFarmingDoorVisualRefreshService.QueueDoorVisualRefreshNeighborhood(worldPos);
    }

    /// <summary>
    /// Reconciles persisted/networked chunk SUN values around a loaded crop chunk after vanilla
    /// chunk lighting is ready. The queue is chunk-centered so dozens of crop tile entities in the
    /// same farm collapse into one rebuild. This is required for generated frame/shape blocks whose
    /// Advanced Farming runtime opacity is not serialized into the chunk light map.
    /// </summary>
    public static void QueueLoadedCropChunkSunlightReconciliation(Vector3i cropWorldPos)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        if (cropWorldPos.y <= 0 || cropWorldPos.y >= byte.MaxValue)
            return;

        // Loaded-crop SUN reconciliation exists to repair saved light around enclosed
        // player farms. World-generated and POI crops can use the same growing tile
        // entity, but rebuilding a 25x25x25 light volume for those crops while the
        // player streams chunks causes severe god-mode movement hitches. A player
        // crop must be supported by an actual farm plot, so reject every other crop
        // before performing the overhead probe or scheduling any main-thread rebuild.
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null ||
            !AdvancedFarmingActiveAreaRegistry.IsPlantProcessable(world, cropWorldPos))
        {
            return;
        }

        int chunkX = World.toChunkXZ(cropWorldPos.x);
        int chunkZ = World.toChunkXZ(cropWorldPos.z);
        int verticalBand = cropWorldPos.y >> 4;
        int localSubX = (cropWorldPos.x & 15) >> 2;
        int localSubZ = (cropWorldPos.z & 15) >> 2;
        long subcellKey = BuildLoadedCropSubcellKey(
            chunkX, chunkZ, verticalBand, localSubX, localSubZ);

        if (!s_evaluatedLoadedCropSubcells.Add(subcellKey))
            return;

        // Outdoor and open-sky POI crops do not need a local SUN-map rebuild.
        // The previous unconditional path rebuilt 8k-14k cells for every crop
        // chunk encountered during movement, even when no player had farmed.
        if (!HasNearbyOverheadCover(cropWorldPos))
            return;

        long chunkBandKey = BuildLoadedCropChunkBandKey(chunkX, chunkZ, verticalBand);
        if (!s_scheduledLoadedCropChunkBands.Add(chunkBandKey))
            return;

        Vector3i chunkCenter = new Vector3i(
            (chunkX << 4) + 8,
            (verticalBand << 4) + 8,
            (chunkZ << 4) + 8);
        QueueSunlightRefresh(
            chunkCenter,
            LoadedCropChunkRefreshRadius,
            LoadedCropChunkRefreshVerticalRadius,
            0,
            LoadedCropChunkRefreshDelayMilliseconds,
            LoadedCropChunkRefreshLatestMilliseconds,
            true,
            false);
    }

    private static bool HasNearbyOverheadCover(Vector3i cropWorldPos)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return false;

        int maxY = Math.Min(byte.MaxValue - 1,
            cropWorldPos.y + LoadedCropOverheadProbeHeight);

        // Probe the crop column plus its immediate neighbors. This is enough to
        // identify roofs, greenhouse glazing and overhangs while avoiding a full
        // volume rebuild for crops standing in open fields. Vegetation above a
        // crop is ignored because it does not represent an enclosed farm shell.
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int y = cropWorldPos.y + 1; y <= maxY; y++)
                {
                    BlockValue value = world.GetBlock(new Vector3i(
                        cropWorldPos.x + dx, y, cropWorldPos.z + dz));
                    if (value.isair || value.Block == null)
                        continue;

                    Block block = value.Block;
                    if (HasFilterTag(block, "SC_crops") ||
                        HasFilterTag(block, "SC_trees") ||
                        HasFilterTag(block, "SC_shrubbery"))
                        continue;

                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasFilterTag(Block block, string tag)
    {
        if (block == null || block.FilterTags == null || string.IsNullOrEmpty(tag))
            return false;

        for (int i = 0; i < block.FilterTags.Length; i++)
        {
            if (string.Equals(block.FilterTags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static long BuildLoadedCropChunkBandKey(
        int chunkX, int chunkZ, int verticalBand)
    {
        unchecked
        {
            long key = ((long)chunkX << 32) ^ (uint)chunkZ;
            key = key * 397L ^ verticalBand;
            return key;
        }
    }

    private static long BuildLoadedCropSubcellKey(
        int chunkX, int chunkZ, int verticalBand, int localSubX, int localSubZ)
    {
        unchecked
        {
            long key = BuildLoadedCropChunkBandKey(chunkX, chunkZ, verticalBand);
            key = key * 397L ^ localSubX;
            key = key * 397L ^ localSubZ;
            return key;
        }
    }

    public static void PumpPendingSunlightRefreshesForGameUpdate()
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled ||
            !AdvancedFarmingActiveAreaRegistry.HasAnyFarmPlots)
        {
            if (s_pendingSunlightRefreshes.Count > 0)
                s_pendingSunlightRefreshes.Clear();
            return;
        }

        ProcessPendingSunlightRefreshes(1);
    }

    public static void ClearPendingSunlightRefreshes()
    {
        s_pendingSunlightRefreshes.Clear();
        s_evaluatedLoadedCropSubcells.Clear();
        s_scheduledLoadedCropChunkBands.Clear();
    }

    public static void ProcessPendingSunlightRefreshes()
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        ProcessPendingSunlightRefreshes(1);
    }

    public static int GetPendingSunlightRefreshCount()
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return 0;

        return s_pendingSunlightRefreshes.Count;
    }

    public static void ProcessAllPendingSunlightRefreshesForDiagnostics()
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        ProcessPendingSunlightRefreshes(s_pendingSunlightRefreshes.Count);
    }

    private static void ProcessPendingSunlightRefreshes(int maxRebuilds)
    {
        if (!AdvancedFarmingRuntimePolicy.Enabled ||
            !AdvancedFarmingActiveAreaRegistry.HasAnyFarmPlots ||
            s_refreshingCropLightStatesAfterSunRebuild)
        {
            if (!AdvancedFarmingActiveAreaRegistry.HasAnyFarmPlots)
                s_pendingSunlightRefreshes.Clear();
            return;
        }

        if (s_pendingSunlightRefreshes.Count == 0)
            return;

        if (maxRebuilds <= 0)
            maxRebuilds = 1;

        int processed = 0;
        while (processed < maxRebuilds && s_pendingSunlightRefreshes.Count > 0)
        {
            int now = RebirthUtilities.TotalGameSecondsPassed();
            long nowStopwatchTicks = Stopwatch.GetTimestamp();
            int bestIndex = -1;
            long bestDeadline = long.MaxValue;
            long bestDue = long.MaxValue;

            // Choose the oldest hard deadline among work that is actually ready. This keeps
            // later list entries from starving an older request during sustained mutation.
            for (int i = 0; i < s_pendingSunlightRefreshes.Count; i++)
            {
                PendingSunlightRefresh refresh = s_pendingSunlightRefreshes[i];
                if (refresh.DueWorldSecond > now)
                    continue;
                if (refresh.DueStopwatchTicks > nowStopwatchTicks
                    && refresh.QueuedStopwatchTicks > 0L
                    && (refresh.LatestStopwatchTicks <= 0L || nowStopwatchTicks < refresh.LatestStopwatchTicks))
                {
                    continue;
                }
                if (refresh.WaitForChunkLightingReady
                    && !IsSunlightReconciliationAreaReady(refresh.Pos, refresh.Radius))
                {
                    continue;
                }

                long deadline = refresh.LatestStopwatchTicks > 0L
                    ? refresh.LatestStopwatchTicks
                    : refresh.DueStopwatchTicks;
                if (bestIndex < 0 || deadline < bestDeadline
                    || (deadline == bestDeadline && refresh.DueStopwatchTicks < bestDue))
                {
                    bestIndex = i;
                    bestDeadline = deadline;
                    bestDue = refresh.DueStopwatchTicks;
                }
            }

            if (bestIndex < 0)
                return;

            PendingSunlightRefresh selected = s_pendingSunlightRefreshes[bestIndex];
            s_pendingSunlightRefreshes.RemoveAt(bestIndex);
            RebuildSunlightVolumeAround(selected.Pos, selected.Radius, selected.VerticalRadius);
            processed++;
        }
    }

    private static void QueueSunlightRefresh(Vector3i worldPos, int horizontalRadius, int verticalRadius, int delayWorldSeconds)
    {
        QueueSunlightRefresh(
            worldPos,
            horizontalRadius,
            verticalRadius,
            delayWorldSeconds,
            DoorStateRefreshCoalesceMilliseconds,
            DoorStateRefreshMaxDebounceMilliseconds,
            false,
            true);
    }

    private static void QueueSunlightRefresh(
        Vector3i worldPos,
        int horizontalRadius,
        int verticalRadius,
        int delayWorldSeconds,
        int delayMilliseconds,
        int latestMilliseconds,
        bool waitForChunkLightingReady,
        bool clearCachesOnQueue)
    {
        if (clearCachesOnQueue)
            ClearLightAndThermalCachesForCoverRefresh(worldPos, horizontalRadius + 14, verticalRadius + 14);

        if (horizontalRadius < 4)
            horizontalRadius = 4;
        if (horizontalRadius > MaxCoalescedRefreshRadius)
            horizontalRadius = MaxCoalescedRefreshRadius;
        if (verticalRadius < 4)
            verticalRadius = 4;
        if (verticalRadius > MaxCoalescedRefreshVerticalRadius)
            verticalRadius = MaxCoalescedRefreshVerticalRadius;

        int due = RebirthUtilities.TotalGameSecondsPassed() + Math.Max(0, delayWorldSeconds);
        long nowStopwatchTicks = Stopwatch.GetTimestamp();
        long dueStopwatchTicks = nowStopwatchTicks + MillisecondsToStopwatchTicks(Math.Max(0, delayMilliseconds));
        long latestStopwatchTicks = nowStopwatchTicks + MillisecondsToStopwatchTicks(Math.Max(delayMilliseconds, latestMilliseconds));

        for (int i = 0; i < s_pendingSunlightRefreshes.Count; i++)
        {
            PendingSunlightRefresh existing = s_pendingSunlightRefreshes[i];
            if (existing.WaitForChunkLightingReady != waitForChunkLightingReady)
                continue;

            // Crop-load entries are intentionally one per chunk. Adjacent crop chunks overlap,
            // but merging them into a radius-capped volume can drop the far wall of either chunk.
            if (waitForChunkLightingReady
                && (existing.Pos.x != worldPos.x || existing.Pos.z != worldPos.z))
            {
                continue;
            }

            if (Math.Abs(existing.DueWorldSecond - due) > 1)
                continue;

            int dx = Math.Abs(existing.Pos.x - worldPos.x);
            int dz = Math.Abs(existing.Pos.z - worldPos.z);
            int dy = Math.Abs(existing.Pos.y - worldPos.y);
            int overlapRadius = existing.Radius + horizontalRadius;
            int overlapVertical = existing.VerticalRadius + verticalRadius;
            if (dx > overlapRadius || dz > overlapRadius || dy > overlapVertical)
                continue;

            int minX = Math.Min(existing.Pos.x - existing.Radius, worldPos.x - horizontalRadius);
            int maxX = Math.Max(existing.Pos.x + existing.Radius, worldPos.x + horizontalRadius);
            int minZ = Math.Min(existing.Pos.z - existing.Radius, worldPos.z - horizontalRadius);
            int maxZ = Math.Max(existing.Pos.z + existing.Radius, worldPos.z + horizontalRadius);
            int minY = Math.Min(existing.Pos.y - existing.VerticalRadius, worldPos.y - verticalRadius);
            int maxY = Math.Max(existing.Pos.y + existing.VerticalRadius, worldPos.y + verticalRadius);

            Vector3i mergedPos = new Vector3i((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
            int mergedRadius = Utils.FastMax(maxX - mergedPos.x, mergedPos.x - minX);
            mergedRadius = Utils.FastMax(mergedRadius, Utils.FastMax(maxZ - mergedPos.z, mergedPos.z - minZ));
            int mergedVertical = Utils.FastMax(maxY - mergedPos.y, mergedPos.y - minY);

            // Never clip a requested union to the coalescing bounds. If the combined
            // volume would exceed the supported single-rebuild radius, keep both jobs.
            if (mergedRadius > MaxCoalescedRefreshRadius
                || mergedVertical > MaxCoalescedRefreshVerticalRadius)
            {
                continue;
            }

            existing.Pos = mergedPos;
            existing.Radius = mergedRadius;
            existing.VerticalRadius = mergedVertical;
            existing.DueWorldSecond = Math.Min(existing.DueWorldSecond, due);
            if (existing.QueuedStopwatchTicks == 0L)
                existing.QueuedStopwatchTicks = nowStopwatchTicks;
            if (existing.DueStopwatchTicks <= 0L || dueStopwatchTicks < existing.DueStopwatchTicks)
                existing.DueStopwatchTicks = dueStopwatchTicks;
            if (existing.LatestStopwatchTicks <= 0L || latestStopwatchTicks < existing.LatestStopwatchTicks)
                existing.LatestStopwatchTicks = latestStopwatchTicks;
            existing.WaitForChunkLightingReady = waitForChunkLightingReady;
            s_pendingSunlightRefreshes[i] = existing;
            AdvancedFarmingPerfSnapshotService.RecordSunRefreshQueued(true);
            return;
        }

        s_pendingSunlightRefreshes.Add(new PendingSunlightRefresh
        {
            Pos = worldPos,
            Radius = horizontalRadius,
            VerticalRadius = verticalRadius,
            DueWorldSecond = due,
            QueuedStopwatchTicks = nowStopwatchTicks,
            DueStopwatchTicks = dueStopwatchTicks,
            LatestStopwatchTicks = latestStopwatchTicks,
            WaitForChunkLightingReady = waitForChunkLightingReady
        });
        AdvancedFarmingPerfSnapshotService.RecordSunRefreshQueued(false);
    }

    private static bool IsSunlightReconciliationAreaReady(Vector3i center, int horizontalRadius)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || world.ChunkCache == null)
            return false;

        if (!world.IsChunkAreaLoaded(center.x, center.y, center.z))
            return false;

        int minChunkX = World.toChunkXZ(center.x - horizontalRadius);
        int maxChunkX = World.toChunkXZ(center.x + horizontalRadius);
        int minChunkZ = World.toChunkXZ(center.z - horizontalRadius);
        int maxChunkZ = World.toChunkXZ(center.z + horizontalRadius);

        for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
        {
            for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
            {
                Chunk chunk = world.GetChunkSync(chunkX, chunkZ) as Chunk;
                if (chunk == null || chunk.InProgressUnloading || chunk.InProgressLighting || chunk.NeedsLightCalculation)
                    return false;
            }
        }

        return true;
    }

    private static long MillisecondsToStopwatchTicks(int milliseconds)
    {
        if (milliseconds <= 0)
            return 0L;

        return (long)((double)milliseconds * Stopwatch.Frequency / 1000.0);
    }

    private static void ClearLightAndThermalCachesForCoverRefresh(Vector3i center, int horizontalRadius, int verticalRadius)
    {
        try
        {
            AdvancedFarmingLightService.ClearCacheAround(center, horizontalRadius, verticalRadius);
            AdvancedFarmingTemperatureService.ClearCacheAround(center, horizontalRadius, verticalRadius);
        }
        catch
        {
            // Cache invalidation is best-effort and must never break block placement/state changes.
        }
    }

    public static void RebuildSunlightVolumeAround(Vector3i worldPos, int horizontalRadius, int verticalRadius)
    {
        if (worldPos.y <= 0 || worldPos.y >= byte.MaxValue)
            return;

        ClearLightAndThermalCachesForCoverRefresh(worldPos, horizontalRadius + 14, verticalRadius + 14);

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        ChunkCluster chunkCluster = world != null ? world.ChunkCache : null;
        if (chunkCluster == null)
            return;

        if (horizontalRadius < 4)
            horizontalRadius = 4;
        if (horizontalRadius > 24)
            horizontalRadius = 24;
        if (verticalRadius < 4)
            verticalRadius = 4;
        if (verticalRadius > 24)
            verticalRadius = 24;

        int minX = worldPos.x - horizontalRadius;
        int maxX = worldPos.x + horizontalRadius;
        int minZ = worldPos.z - horizontalRadius;
        int maxZ = worldPos.z + horizontalRadius;
        int upRadius = Math.Min(verticalRadius, MaxCoalescedRefreshUpRadius);
        if (upRadius < DoorStateRefreshUpRadius)
            upRadius = DoorStateRefreshUpRadius;

        int minY = Math.Max(1, worldPos.y - verticalRadius);
        int maxY = Math.Min(byte.MaxValue - 1, worldPos.y + upRadius);

        int widthX = maxX - minX + 1;
        int widthZ = maxZ - minZ + 1;
        int height = maxY - minY + 1;
        int count = widthX * widthZ * height;
        if (count <= 0 || count > 200000)
            return;

        long perfSampleStartTicks = 0L;
        bool perfSampling = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        if (perfSampling)
            perfSampleStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

        long stutterTraceStartTicks = 0L;
        if (AdvancedFarmingStutterTraceService.Active)
            stutterTraceStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

        EnsureRebuildScratchCapacity(count);
        byte[] newSun = s_rebuildNewSun;
        bool[] queued = s_rebuildQueued;
        bool[] sealedSunCells = s_rebuildSealedSunCells;
        int[] queue = s_rebuildQueue;
        BlockValue[] blockValues = s_rebuildBlockValues;
        int[] baseOpacity = s_rebuildBaseOpacity;
        byte[] ruleCache = s_rebuildNaturalLightRule;
        byte[] sourceFaceOpacity = s_rebuildSourceFaceOpacity;
        byte[] targetFaceOpacity = s_rebuildTargetFaceOpacity;
        Array.Clear(newSun, 0, count);
        Array.Clear(queued, 0, count);
        Array.Clear(sealedSunCells, 0, count);
        ResetCoverFaceOpacityCache(sourceFaceOpacity, count);
        ResetCoverFaceOpacityCache(targetFaceOpacity, count);
        SnapshotRebuildVolume(chunkCluster, minX, minY, minZ, widthX, widthZ, height, blockValues, baseOpacity, ruleCache);
        int queueHead = 0;
        int queueTail = 0;

        // Cells containing a horizontal stateful cover plane (for example an opened all-rotation
        // door lying across a ceiling hole) must not be relit by the lateral SUN flood after
        // the direct vertical pass correctly shadows them. Without this barrier array, the
        // top/bottom face classifier can return CoverBlocked while the rebuilt chunk SUN value
        // immediately becomes 14 again through side-face spread from adjacent lit cells.
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    int index = MakeVolumeIndex(x, y, z, minX, minY, minZ, widthX, widthZ);
                    if (index < 0)
                        continue;

                    Vector3i cellPos = new Vector3i(x, y, z);
                    BlockValue cellValue = blockValues[index];
                    sealedSunCells[index] = GetCachedCoverFaceOpacity(chunkCluster, cellPos, cellValue, index, BlockFaceFlag.Top, false, targetFaceOpacity) >= FullSunBlockOpacity
                        || GetCachedCoverFaceOpacity(chunkCluster, cellPos, cellValue, index, BlockFaceFlag.Bottom, false, targetFaceOpacity) >= FullSunBlockOpacity;
                }
            }
        }

        // Direct sunlight seeds must be derived from the sky, not copied from the current
        // light value immediately above this small rebuild volume. A door can admit lateral SUN
        // into upper room cells while open. If the close-state rebuild reuses that stale interior
        // value as its top seed, it manufactures a permanent SUN source below the roof and leaves
        // the crops nearest the door at SUN 8-9. Recompute the direct column from world top through
        // every real block above maxY, then walk maxY..minY through the snapshotted local volume.
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                int currentSun = ComputeDirectSunAboveRebuildVolume(chunkCluster, x, maxY, z);

                for (int y = maxY; y >= minY; y--)
                {
                    int opacity = GetVerticalSunOpacityAtSnapshot(chunkCluster, chunkCluster, x, y, z, minX, minY, minZ, widthX, widthZ, maxY, blockValues, baseOpacity, ruleCache, sourceFaceOpacity, targetFaceOpacity);
                    currentSun = Utils.FastMax(0, currentSun - opacity);

                    int index = MakeVolumeIndex(x, y, z, minX, minY, minZ, widthX, widthZ);
                    if (index < 0)
                        continue;

                    if (sealedSunCells[index])
                    {
                        newSun[index] = 0;
                    }
                    else
                    {
                        newSun[index] = (byte)currentSun;
                        if (currentSun > 0 && !queued[index])
                        {
                            queue[queueTail++] = index;
                            queued[index] = true;
                        }
                    }
                }
            }
        }

        // Lateral sunlight spread inside the volume, also using dynamic per-instance
        // opacity. This is the part vanilla misses when a low-LightOpacity model door
        // visually seals a side opening.
        while (queueHead < queueTail)
        {
            int index = queue[queueHead++];
            queued[index] = false;
            byte light = newSun[index];
            if (light == 0 || sealedSunCells[index])
                continue;

            int lx, ly, lz;
            DecodeVolumeIndex(index, minX, minY, minZ, widthX, widthZ, out lx, out ly, out lz);
            Vector3i fromPos = new Vector3i(lx, ly, lz);
            BlockValue fromValue = blockValues[index];

            for (int i = 0; i < Vector3i.AllDirections.Length; i++)
            {
                Vector3i dir = Vector3i.AllDirections[i];
                int nx = lx + dir.x;
                int ny = ly + dir.y;
                int nz = lz + dir.z;
                if (nx < minX || nx > maxX || ny < minY || ny > maxY || nz < minZ || nz > maxZ)
                    continue;

                int toIndex = MakeVolumeIndex(nx, ny, nz, minX, minY, minZ, widthX, widthZ);
                if (toIndex < 0)
                    continue;

                Vector3i toPos = new Vector3i(nx, ny, nz);
                BlockValue toValue = blockValues[toIndex];
                int opacity = GetLightStepOpacityFromSnapshot(chunkCluster, fromPos, fromValue, index, toPos, toValue, toIndex, dir.x, dir.y, dir.z, baseOpacity[toIndex], ruleCache, sourceFaceOpacity, targetFaceOpacity);
                int next = light - (opacity != 0 ? opacity : 1);
                if (next <= 0)
                    continue;

                if (sealedSunCells[toIndex] || next <= newSun[toIndex])
                    continue;

                newSun[toIndex] = (byte)next;
                if (!queued[toIndex])
                {
                    queue[queueTail++] = toIndex;
                    queued[toIndex] = true;
                }
            }
        }

        // Commit the rebuilt SUN values to the actual chunks. This is the value read by
        // EntityAlive.GetAmountEnclosed(), cropenv cropSun, and the visual inside-lighting
        // path once the game refreshes its normal player stats.
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Chunk chunk = chunkCluster.GetChunkFromWorldPos(x, worldPos.y, z) as Chunk;
                if (chunk == null)
                    continue;

                bool anyChangedInColumn = false;
                for (int y = minY; y <= maxY; y++)
                {
                    int index = MakeVolumeIndex(x, y, z, minX, minY, minZ, widthX, widthZ);
                    byte desired = index >= 0 ? newSun[index] : (byte)0;
                    int lx = World.toBlockXZ(x);
                    int lz = World.toBlockXZ(z);
                    byte current = chunk.GetLight(lx, y, lz, Chunk.LIGHT_TYPE.SUN);
                    if (current != desired)
                    {
                        chunk.SetLight(lx, y, lz, desired, Chunk.LIGHT_TYPE.SUN);
                        anyChangedInColumn = true;
                    }
                }

                if (anyChangedInColumn)
                    chunk.NeedsRegeneration = true;
            }
        }

        // Geometry changes must update crop eligibility when the rebuilt SUN values become
        // authoritative, not when each crop's normal 10-15 second staggered growth wake happens.
        // This pass is server-only, scans just the rebuilt volume, does not advance crop progress
        // or consume water, and synchronizes only crops whose final light pass/fail state changed.
        s_refreshingCropLightStatesAfterSunRebuild = true;
        try
        {
            AdvancedFarmingSyncService.RefreshAuthoritativePlantLightStatesInVolume(
                world,
                minX,
                maxX,
                minY,
                maxY,
                minZ,
                maxZ);
        }
        finally
        {
            s_refreshingCropLightStatesAfterSunRebuild = false;
        }

        if (perfSampling)
        {
            long perfElapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - perfSampleStartTicks;
            AdvancedFarmingPerfSnapshotService.RecordSunRebuild(perfElapsedTicks, count);
        }

        if (AdvancedFarmingStutterTraceService.Active)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - stutterTraceStartTicks;
            if (AdvancedFarmingStutterTraceService.ShouldRecordSlow(elapsedTicks))
                AdvancedFarmingStutterTraceService.RecordSlowOperation("sunRebuild", worldPos, elapsedTicks, "horizontalRadius=" + horizontalRadius + " downRadius=" + verticalRadius + " upRadius=" + upRadius + " volumeCells=" + count + " bounds=" + minX + "," + minY + "," + minZ + ".." + maxX + "," + maxY + "," + maxZ);
        }
    }

    private static void EnsureRebuildScratchCapacity(int count)
    {
        if (count <= 0)
            count = 1;

        if (s_rebuildNewSun == null || s_rebuildNewSun.Length < count)
            s_rebuildNewSun = new byte[count];
        if (s_rebuildQueued == null || s_rebuildQueued.Length < count)
            s_rebuildQueued = new bool[count];
        if (s_rebuildSealedSunCells == null || s_rebuildSealedSunCells.Length < count)
            s_rebuildSealedSunCells = new bool[count];
        int queueCapacity = count > 0 && count < int.MaxValue / 16 ? count * 16 : count;
        if (queueCapacity < count)
            queueCapacity = count;
        if (s_rebuildQueue == null || s_rebuildQueue.Length < queueCapacity)
            s_rebuildQueue = new int[queueCapacity];
        if (s_rebuildBlockValues == null || s_rebuildBlockValues.Length < count)
            s_rebuildBlockValues = new BlockValue[count];
        if (s_rebuildBaseOpacity == null || s_rebuildBaseOpacity.Length < count)
            s_rebuildBaseOpacity = new int[count];
        if (s_rebuildNaturalLightRule == null || s_rebuildNaturalLightRule.Length < count)
            s_rebuildNaturalLightRule = new byte[count];

        int faceCacheCount = count * 6;
        if (s_rebuildSourceFaceOpacity == null || s_rebuildSourceFaceOpacity.Length < faceCacheCount)
            s_rebuildSourceFaceOpacity = new byte[faceCacheCount];
        if (s_rebuildTargetFaceOpacity == null || s_rebuildTargetFaceOpacity.Length < faceCacheCount)
            s_rebuildTargetFaceOpacity = new byte[faceCacheCount];
    }

    private static void ResetCoverFaceOpacityCache(byte[] cache, int cellCount)
    {
        if (cache == null)
            return;

        int length = cellCount * 6;
        if (length > cache.Length)
            length = cache.Length;

        for (int i = 0; i < length; i++)
            cache[i] = CoverFaceOpacityUnset;
    }

    private static void SnapshotRebuildVolume(ChunkCluster chunkCluster, int minX, int minY, int minZ, int widthX, int widthZ, int height, BlockValue[] blockValues, int[] baseOpacity, byte[] ruleCache)
    {
        if (chunkCluster == null || blockValues == null || baseOpacity == null || ruleCache == null)
            return;

        int maxX = minX + widthX - 1;
        int maxZ = minZ + widthZ - 1;
        int maxY = minY + height - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Chunk chunk = chunkCluster.GetChunkFromWorldPos(x, minY, z) as Chunk;
                int localX = World.toBlockXZ(x);
                int localZ = World.toBlockXZ(z);
                for (int y = minY; y <= maxY; y++)
                {
                    int index = MakeVolumeIndex(x, y, z, minX, minY, minZ, widthX, widthZ);
                    if (index < 0)
                        continue;

                    BlockValue value = chunk != null ? chunk.GetBlock(localX, y, localZ) : BlockValue.Air;
                    blockValues[index] = value;
                    baseOpacity[index] = GetBaseOpacity(value);
                    ruleCache[index] = (byte)GetCellNaturalLightRule(value);
                }
            }
        }
    }

    private static NaturalLightRule GetCellNaturalLightRule(BlockValue blockValue)
    {
        if (blockValue.isair || blockValue.isWater)
            return NaturalLightRule.AlwaysPass;

        Block block = blockValue.Block;
        if (block == null)
            return NaturalLightRule.Compute;

        if (block.blockMaterial != null && block.blockMaterial.IsPlant)
            return NaturalLightRule.AlwaysPass;

        return ResolveNaturalLightRule(block);
    }

    public static string BuildStatus()
    {
        return "[AdvancedFarming DynamicLightOpacity] mode=patchedLightProcessorAndVolumeSunRebuild source=perInstanceCompositeState patch=v170_eventDrivenCropLightRefresh fullOpacity=" + FullSunBlockOpacity + " halfCurtainOpacity=" + HalfCurtainSunBlockOpacity + " generatedShapePolicy=runtimeCategoryOpacity persistedCropChunkSunReconciliation=true eventDrivenCropLightRefresh=true"
            + " pendingSunRefreshes=" + s_pendingSunlightRefreshes.Count
            + " doorRefreshRadius=" + DoorStateRefreshRadius
            + " doorRefreshDownRadius=" + DoorStateRefreshVerticalRadius
            + " doorRefreshUpRadius=" + DoorStateRefreshUpRadius
            + " doorRefreshDelay=nextGameUpdate visualRefreshQueued=true visualBleedSunNeighborSuppression=false invalidYRefreshGuard=true topOfWorldDirectSunSeed=true pooledSunScratch=true volumeBlockSnapshot=true perCellFaceOpacityMemo=true scopedCacheInvalidation=true refreshCoalesceMs=" + DoorStateRefreshCoalesceMilliseconds + " refreshMaxDebounceMs=" + DoorStateRefreshMaxDebounceMilliseconds + " loadedCropChunkRadius=" + LoadedCropChunkRefreshRadius + " loadedCropChunkDelayMs=" + LoadedCropChunkRefreshDelayMilliseconds + " loadedCropChunkLatestMs=" + LoadedCropChunkRefreshLatestMilliseconds + " descriptorCache=" + s_naturalLightDescriptorByBlockId.Count + "";
    }


    private static int GetVerticalSunOpacityAtSnapshot(IBlockAccess world, IChunkAccess chunkAccess, int x, int y, int z, int minX, int minY, int minZ, int widthX, int widthZ, int maxY, BlockValue[] blockValues, int[] baseOpacity, byte[] ruleCache, byte[] sourceFaceOpacity, byte[] targetFaceOpacity)
    {
        Vector3i toPos = new Vector3i(x, y, z);
        int toIndex = MakeVolumeIndex(x, y, z, minX, minY, minZ, widthX, widthZ);
        BlockValue toValue = toIndex >= 0 ? blockValues[toIndex] : GetBlockAt(chunkAccess, toPos);

        Vector3i fromPos = new Vector3i(x, y + 1, z);
        int fromIndex = y < maxY ? MakeVolumeIndex(x, y + 1, z, minX, minY, minZ, widthX, widthZ) : -1;
        BlockValue fromValue = fromIndex >= 0 ? blockValues[fromIndex] : (y < byte.MaxValue ? GetBlockAt(chunkAccess, fromPos) : BlockValue.Air);
        int targetBaseOpacity = toIndex >= 0 ? baseOpacity[toIndex] : GetBaseOpacity(toValue);
        return GetLightStepOpacityFromSnapshot(world, fromPos, fromValue, fromIndex, toPos, toValue, toIndex, 0, -1, 0, targetBaseOpacity, ruleCache, sourceFaceOpacity, targetFaceOpacity);
    }

    private static int GetLightStepOpacityFromSnapshot(IBlockAccess world, Vector3i fromPos, BlockValue fromValue, int fromIndex, Vector3i toPos, BlockValue toValue, int toIndex, int dx, int dy, int dz, int targetBaseOpacity, byte[] ruleCache, byte[] sourceFaceOpacity, byte[] targetFaceOpacity)
    {
        StepToFaces(dx, dy, dz, out BlockFaceFlag fromFace, out BlockFaceFlag toFace);

        int fromCoverOpacity = fromIndex >= 0
            ? GetCachedCoverFaceOpacity(world, fromPos, fromValue, fromIndex, fromFace, true, sourceFaceOpacity)
            : GetCoverFaceOpacity(world, fromPos, fromValue, fromFace, true);
        if (fromCoverOpacity > 0)
            return fromCoverOpacity;

        int toCoverOpacity = toIndex >= 0
            ? GetCachedCoverFaceOpacity(world, toPos, toValue, toIndex, toFace, false, targetFaceOpacity)
            : GetCoverFaceOpacity(world, toPos, toValue, toFace, false);
        if (toCoverOpacity > 0)
            return toCoverOpacity;

        if (toIndex >= 0 && ruleCache != null)
        {
            byte rule = ruleCache[toIndex];
            if (rule == CachedRuleDoorStateDependent || rule == CachedRuleAlwaysPass)
                return 0;
        }
        else
        {
            if (IsPassingStateDependentNaturalLightCell(toValue) || IsAlwaysPassNaturalLightCell(toValue))
                return 0;
        }

        return targetBaseOpacity;
    }

    private static int GetCachedCoverFaceOpacity(IBlockAccess world, Vector3i pos, BlockValue blockValue, int cellIndex, BlockFaceFlag face, bool sourceCell, byte[] cache)
    {
        int faceIndex = FaceToCacheIndex(face);
        if (cellIndex < 0 || faceIndex < 0 || cache == null)
            return GetCoverFaceOpacity(world, pos, blockValue, face, sourceCell);

        int cacheIndex = cellIndex * 6 + faceIndex;
        if (cacheIndex < 0 || cacheIndex >= cache.Length)
            return GetCoverFaceOpacity(world, pos, blockValue, face, sourceCell);

        byte cached = cache[cacheIndex];
        if (cached != CoverFaceOpacityUnset)
            return cached;

        int opacity = GetCoverFaceOpacity(world, pos, blockValue, face, sourceCell);
        if (opacity < 0)
            opacity = 0;
        if (opacity > FullSunBlockOpacity)
            opacity = FullSunBlockOpacity;

        cache[cacheIndex] = (byte)opacity;
        return opacity;
    }

    private static int FaceToCacheIndex(BlockFaceFlag face)
    {
        switch (face)
        {
            case BlockFaceFlag.Top:
                return 0;
            case BlockFaceFlag.Bottom:
                return 1;
            case BlockFaceFlag.East:
                return 2;
            case BlockFaceFlag.West:
                return 3;
            case BlockFaceFlag.North:
                return 4;
            case BlockFaceFlag.South:
                return 5;
            default:
                return -1;
        }
    }

    private static int ComputeDirectSunAboveRebuildVolume(IChunkAccess chunkAccess, int x, int maxY, int z)
    {
        if (chunkAccess == null)
            return 0;

        int currentSun = 15;
        int firstY = byte.MaxValue;
        int stopY = Math.Min(byte.MaxValue, maxY + 1);

        for (int y = firstY; y >= stopY && currentSun > 0; y--)
        {
            int opacity = GetVerticalSunOpacityAt(chunkAccess, x, y, z);
            if (opacity == byte.MaxValue)
                return 0;

            currentSun = Utils.FastMax(0, currentSun - opacity);
        }

        return currentSun;
    }

    private static int GetVerticalSunOpacityAt(IChunkAccess chunkAccess, int x, int y, int z)
    {
        Vector3i toPos = new Vector3i(x, y, z);
        BlockValue toValue = GetBlockAt(chunkAccess, toPos);
        Vector3i fromPos = new Vector3i(x, y + 1, z);
        BlockValue fromValue = y < byte.MaxValue ? GetBlockAt(chunkAccess, fromPos) : BlockValue.Air;
        return GetEdgeOpacity(chunkAccess, fromPos, fromValue, toPos, toValue, 0, -1, 0, GetBaseOpacity(toValue));
    }

    private static int MakeVolumeIndex(int x, int y, int z, int minX, int minY, int minZ, int widthX, int widthZ)
    {
        int dx = x - minX;
        int dy = y - minY;
        int dz = z - minZ;
        if (dx < 0 || dz < 0 || dy < 0 || dx >= widthX || dz >= widthZ)
            return -1;
        return ((dy * widthZ) + dz) * widthX + dx;
    }

    private static void DecodeVolumeIndex(int index, int minX, int minY, int minZ, int widthX, int widthZ, out int x, out int y, out int z)
    {
        int plane = widthX * widthZ;
        int dy = index / plane;
        int rem = index - dy * plane;
        int dz = rem / widthX;
        int dx = rem - dz * widthX;
        x = minX + dx;
        y = minY + dy;
        z = minZ + dz;
    }

    private static int GetEdgeOpacity(IChunkAccess chunkAccess, Vector3i fromPos, BlockValue fromValue, Vector3i toPos, BlockValue toValue, int dx, int dy, int dz, int vanillaTargetOpacity)
    {
        IBlockAccess blockAccess = chunkAccess as IBlockAccess;
        StepToFaces(dx, dy, dz, out BlockFaceFlag fromFace, out BlockFaceFlag toFace);

        int fromCoverOpacity = GetCoverFaceOpacity(blockAccess, fromPos, fromValue, fromFace, true);
        if (fromCoverOpacity > 0)
            return fromCoverOpacity;

        int toCoverOpacity = GetCoverFaceOpacity(blockAccess, toPos, toValue, toFace, false);
        if (toCoverOpacity > 0)
            return toCoverOpacity;

        // If a state-dependent cover did not block this crossed face, the cover is visually
        // transparent for this light step. Do not leave the block's static XML lightOpacity
        // in place: curtains/drapes/blinds have low non-zero opacity (for example 4), which caused
        // v67 pass rotations to still dim SUN instead of fully letting light through.
        if (blockAccess != null && IsPassingStateDependentNaturalLightCell(toValue))
            return 0;

        // v56: Windows/glass can be transparent for Advanced Farming even when the
        // vanilla block definition still reports high LightOpacity (for example
        // woodShapes:window01 reports lightOpacity=255 while intact). The v54
        // crop-side classifier already returned AlwaysPass for these blocks, but
        // the patched LightProcessor still used the vanilla target opacity during
        // actual SUN propagation, so intact windows stayed dark until broken.
        //
        // Only override the target-cell opacity here. Source-cell covers must still
        // be able to block the crossed face above, and normal opaque targets must
        // remain opaque.
        if (IsAlwaysPassNaturalLightCell(toValue))
            return 0;

        return vanillaTargetOpacity;
    }

    private static bool IsAlwaysPassNaturalLightCell(BlockValue blockValue)
    {
        if (blockValue.isair || blockValue.isWater)
            return true;

        Block block = blockValue.Block;
        if (block == null)
            return false;

        if (block.blockMaterial != null && block.blockMaterial.IsPlant)
            return true;

        return ResolveNaturalLightRule(block) == NaturalLightRule.AlwaysPass;
    }

    private static bool IsPassingStateDependentNaturalLightCell(BlockValue blockValue)
    {
        if (blockValue.isair || blockValue.isWater)
            return false;

        Block block = blockValue.Block;
        if (block == null)
            return false;

        if (block.blockMaterial != null && block.blockMaterial.IsPlant)
            return false;

        return ResolveNaturalLightRule(block) == NaturalLightRule.DoorStateDependent;
    }

    public static int ComputeCoverFaceOpacitySignature(IBlockAccess world, Vector3i pos, BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block == null)
            return int.MinValue;

        unchecked
        {
            int signature = blockValue.type;
            signature = signature * 397 ^ blockValue.meta;
            signature = signature * 397 ^ blockValue.rotation;
            signature = signature * 397 ^ (int)blockValue.rawData;
            signature = signature * 397 ^ blockValue.damage;
            signature = signature * 397 ^ (blockValue.isair ? 1 : 0);
            signature = signature * 397 ^ (blockValue.ischild ? 1 : 0);

            for (int i = 0; i < Vector3i.AllDirections.Length; i++)
            {
                BlockFaceFlag face = DirectionToFace(Vector3i.AllDirections[i]);
                int targetOpacity = GetCoverFaceOpacity(world, pos, blockValue, face, false);
                int sourceOpacity = GetCoverFaceOpacity(world, pos, blockValue, face, true);
                signature = signature * 397 ^ ((i + 1) * 31 + targetOpacity * 7 + sourceOpacity);
            }

            return signature;
        }
    }

    private static bool IsCoverBlockingFace(IBlockAccess world, Vector3i pos, BlockValue blockValue, BlockFaceFlag face)
    {
        return GetCoverFaceOpacity(world, pos, blockValue, face, false) > 0;
    }

    private static int GetCoverFaceOpacity(IBlockAccess world, Vector3i pos, BlockValue blockValue, BlockFaceFlag face, bool sourceCell)
    {
        if (blockValue.isair || blockValue.isWater)
            return 0;

        Block block = blockValue.Block;
        if (block == null)
            return 0;

        if (block.blockMaterial != null && block.blockMaterial.IsPlant)
            return 0;

        NaturalLightRule rule = ResolveNaturalLightRule(block);
        if (rule == NaturalLightRule.AlwaysPass)
            return 0;

        if ((rule == NaturalLightRule.AlwaysBlock || rule == NaturalLightRule.DoorStateDependent) && IsLightPassingCoverDamageState(world, pos, blockValue, block))
            return 0;

        if (rule == NaturalLightRule.AlwaysBlock)
            return FullSunBlockOpacity;

        if (rule == NaturalLightRule.DoorStateDependent)
        {
            if (IsCurtainOrBlindLike(block) && IsHalfCurtainLike(block))
            {
                if (!IsDoorPlaneBlockingFace(world, pos, blockValue, block, face))
                    return 0;

                // The patched light flood already paid the partial cost when light entered the
                // half-curtain cell. Do not charge it again when light exits the same cell, or
                // a single half curtain would become effectively full-dark.
                return sourceCell ? 0 : HalfCurtainSunBlockOpacity;
            }

            return IsDoorPlaneBlockingFace(world, pos, blockValue, block, face) ? FullSunBlockOpacity : 0;
        }

        return 0;
    }

    private static bool IsDoorPlaneBlockingFace(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block, BlockFaceFlag face)
    {
        if (block == null)
            return false;

        bool? wallHatchPass = TryGetVerifiedWallHatchPassState(world, pos, blockValue, block);
        if (wallHatchPass.HasValue)
            return !wallHatchPass.Value;

        if (IsCurtainOrBlindLike(block))
            return !IsVerifiedCurtainBlindPassState(world, pos, blockValue, block);

        if (IsShutterLike(block))
            return !IsVerifiedShutterPassState(world, pos, blockValue, block);

        if (IsCenteredNonGlassNaturalLightCover(block))
            return !IsVerifiedCenteredNonGlassPassState(world, pos, blockValue, block);

        // Trap doors/hatches expose their real occupied plane through the exact face
        // movement query. Do not collapse them to a meta/open shortcut: their default
        // placement can leave the shaft clear while the opened state can swing into it.
        if (block is BlockTrapDoor)
        {
            bool? blocked = SafeIsMovementBlocked(world, pos, blockValue, block, face);
            return !blocked.HasValue || blocked.Value;
        }

        // Powered doors still use the vanilla open-state helper because their movement
        // implementation is not a reliable directional signal in 3.0. Ordinary composite
        // doors continue through the explicit rotation-plane mask below.
        if (block is BlockPoweredDoor)
        {
            bool? blocked = SafeIsMovementBlocked(world, pos, blockValue, block, face);
            return !blocked.HasValue || blocked.Value;
        }

        if (face == BlockFaceFlag.None)
            return false;
        if (face == BlockFaceFlag.All)
            return true;

        byte rotation = GetDoorStateRotation(world, pos, blockValue, block);

        // All-rotation ordinary doors need two normalized occupancy masks: one for the
        // closed model pose and one for the opened model pose. Different rotation bytes can
        // normalize to the same occupied plane, so do not decide the behavior from a single
        // rotation bucket such as "8..15" or "16..23". The active mask is selected only
        // after the closed/open state is known.
        if (IsAllRotationFullPlaneDoorRotation(rotation))
        {
            BlockFaceFlag activeAllRotationMask = GetActiveAllRotationDoorBlockingFaceMask(world, pos, blockValue, block, rotation, face);
            return (activeAllRotationMask & face) != BlockFaceFlag.None;
        }

        BlockFaceFlag closedMask = GetClosedDoorBlockingFaceMaskFromRotation(rotation);
        if (closedMask == BlockFaceFlag.None)
            return false;

        bool open = IsUprightDoorOpenState(world, pos, blockValue, block);
        BlockFaceFlag activeMask = open ? GetOpenedDoorBlockingFaceMaskFromRotation(rotation) : closedMask;
        return (activeMask & face) != BlockFaceFlag.None;
    }

    private static BlockFaceFlag GetActiveAllRotationDoorBlockingFaceMask(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block, byte rotation, BlockFaceFlag testedFace)
    {
        bool open = IsUprightDoorOpenState(world, pos, blockValue, block);
        bool openedSealsAperture = IsOpenedHorizontalDoorStateSealingRotation(rotation);
        bool activeSealsAperture = open ? openedSealsAperture : !openedSealsAperture;

        DoorPlacementPlane placementPlane = InferAllRotationDoorPlacementPlane(world, pos, blockValue, block, testedFace);

        if (placementPlane == DoorPlacementPlane.Wall)
        {
            // v32: a wall-mounted all-rotation door must block the wall aperture axis,
            // not the door leaf's cardinal side-plane. The same rotation byte can be
            // used on ceiling/floor and wall placements; using GetHorizontalDoorSidePlaneMask
            // for wall placements made rot=8/10/16/18 block North/South even when the
            // actual doorway/light path crossed East/West. Infer the aperture from the
            // neighboring open cells around this door segment.
            bool wallActiveSealsAperture = !activeSealsAperture;
            return wallActiveSealsAperture ? GetWallDoorApertureFaceMask(world, pos, testedFace, rotation) : BlockFaceFlag.None;
        }

        if (!activeSealsAperture)
            return BlockFaceFlag.None;

        // Preserve the v27 floor/ceiling behavior for known horizontal placements and for
        // ambiguous cases. Unknown must remain conservative so an unrecognized ceiling door
        // does not silently leak sunlight.
        return BlockFaceFlag.Top | BlockFaceFlag.Bottom;
    }


    private static DoorPlacementPlane InferAllRotationDoorPlacementPlane(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block, BlockFaceFlag testedFace)
    {
        if (world == null)
            return DoorPlacementPlane.Unknown;

        int verticalScore = GetAxisOpenScore(world, pos, 0, 1, 0);
        int eastWestScore = GetAxisOpenScore(world, pos, 1, 0, 0);
        int northSouthScore = GetAxisOpenScore(world, pos, 0, 0, 1);
        int horizontalScore = Math.Max(eastWestScore, northSouthScore);

        if (verticalScore > horizontalScore)
            return DoorPlacementPlane.FloorCeiling;
        if (horizontalScore > verticalScore)
            return DoorPlacementPlane.Wall;

        return DoorPlacementPlane.Unknown;
    }

    private static int GetAxisOpenScore(IBlockAccess world, Vector3i pos, int dx, int dy, int dz)
    {
        return GetPlacementProbeOpenScore(world, new Vector3i(pos.x + dx, pos.y + dy, pos.z + dz))
            + GetPlacementProbeOpenScore(world, new Vector3i(pos.x - dx, pos.y - dy, pos.z - dz));
    }

    private static int GetPlacementProbeOpenScore(IBlockAccess world, Vector3i pos)
    {
        if (world == null || pos.y < 0 || pos.y > byte.MaxValue)
            return 0;

        BlockValue value;
        try
        {
            value = world.GetBlock(pos);
        }
        catch
        {
            return 0;
        }

        if (value.isair || value.isWater)
            return 2;

        Block neighbor = value.Block;
        if (neighbor == null)
            return 2;

        NaturalLightRule rule = ResolveNaturalLightRule(neighbor);
        if (rule == NaturalLightRule.AlwaysBlock)
            return 0;
        if (rule == NaturalLightRule.AlwaysPass)
            return 2;
        if (rule == NaturalLightRule.DoorStateDependent)
            return 1;

        return GetBaseOpacity(value) >= FullSunBlockOpacity ? 0 : 1;
    }

    private static bool IsAllRotationFullPlaneDoorRotation(byte rotation)
    {
        return rotation >= 4 && rotation < 24;
    }

    private static bool IsOpenedHorizontalDoorStateSealingRotation(byte rotation)
    {
        // This answers only one normalized question: does the opened model pose occupy the
        // Top/Bottom light boundary? Multiple rotation bytes can normalize to the same
        // answer. Rotations 24+ are diagonal/45-degree partial states and are excluded by
        // IsAllRotationFullPlaneDoorRotation before this helper is used.
        //
        // Grounded from the door rotation structure-crop command:
        //   2026-07-06 13:39 -> rotations 12..15 and 20..23 were re-tested after the v24
        //                         inversion. The user supplied that log as another failing
        //                         set and stated those results should also be reversed.
        //                         Those bands therefore need the 0x04-flipped parity map.
        //
        // Rotation band summary for ordinary all-rotation horizontal doors:
        //   4,6              -> opened seals Top/Bottom
        //   5,7              -> opened clears Top/Bottom
        //   8..11,16..19 odd -> opened seals Top/Bottom
        //   8..11,16..19 even-> opened clears Top/Bottom
        //   12..15,20..23 even -> opened seals Top/Bottom
        //   12..15,20..23 odd  -> opened clears Top/Bottom
        //   24..31           -> diagonal/45-degree partial states, excluded before this helper.
        if (rotation < 4 || rotation >= 24)
            return false;

        if (rotation < 8)
            return (rotation & 1) == 0;

        bool oddRotation = (rotation & 1) != 0;
        bool flippedQuarter = (rotation & 4) != 0;
        return oddRotation ^ flippedQuarter;
    }

    private static BlockFaceFlag GetHorizontalDoorSidePlaneMask(byte rotation)
    {
        byte cardinalRotation = (byte)(rotation & 3);
        return cardinalRotation == 0 || cardinalRotation == 2
            ? BlockFaceFlag.North | BlockFaceFlag.South
            : BlockFaceFlag.East | BlockFaceFlag.West;
    }

    private static BlockFaceFlag GetWallDoorApertureFaceMask(IBlockAccess world, Vector3i pos, BlockFaceFlag testedFace, byte rotation)
    {
        if (world != null)
        {
            int eastWestScore = GetAxisOpenScore(world, pos, 1, 0, 0);
            int northSouthScore = GetAxisOpenScore(world, pos, 0, 0, 1);

            if (eastWestScore > northSouthScore)
                return BlockFaceFlag.East | BlockFaceFlag.West;
            if (northSouthScore > eastWestScore)
                return BlockFaceFlag.North | BlockFaceFlag.South;
        }

        if ((testedFace & (BlockFaceFlag.East | BlockFaceFlag.West)) != BlockFaceFlag.None)
            return BlockFaceFlag.East | BlockFaceFlag.West;
        if ((testedFace & (BlockFaceFlag.North | BlockFaceFlag.South)) != BlockFaceFlag.None)
            return BlockFaceFlag.North | BlockFaceFlag.South;

        return GetHorizontalDoorSidePlaneMask(rotation);
    }


    private static bool IsUprightDoorOpenState(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (block == null)
            return false;

        bool? blockedAll = SafeIsMovementBlocked(world, pos, blockValue, block, BlockFaceFlag.All);
        if (blockedAll.HasValue)
            return !blockedAll.Value;

        // Fallback only. Composite/model-entity doors can report IsSeeThrough=true for
        // reasons unrelated to the configured damage-state light-pass exception, so this
        // must never override a valid movement-blocked state.
        WorldBase worldBase = world as WorldBase;
        if (worldBase != null)
        {
            try
            {
                return block.IsSeeThrough(worldBase, pos, blockValue);
            }
            catch
            {
            }
        }

        return false;
    }

    private static BlockFaceFlag GetPerpendicularDoorFaceMask(BlockFaceFlag mask)
    {
        bool northSouth = (mask & (BlockFaceFlag.North | BlockFaceFlag.South)) != 0;
        bool eastWest = (mask & (BlockFaceFlag.East | BlockFaceFlag.West)) != 0;

        if (northSouth && !eastWest)
            return BlockFaceFlag.East | BlockFaceFlag.West;
        if (eastWest && !northSouth)
            return BlockFaceFlag.North | BlockFaceFlag.South;

        return mask;
    }

    private static bool IsLightPassingCoverDamageState(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (block == null || !(block.shape is BlockShapeModelEntity))
            return false;

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);
        if (stateBlock == null || !(stateBlock.shape is BlockShapeModelEntity shape))
            return false;

        int passIndex = GetDamageStateLightPassIndex(stateBlock);
        int index;
        try
        {
            index = shape.GetDamageStateIndex(stateValue);
        }
        catch
        {
            return false;
        }

        if (index >= passIndex)
            return true;

        int remaining = stateBlock.MaxDamagePlusDowngrades - stateValue.damage;
        if (remaining != 1)
            return false;

        try
        {
            return !shape.IsObstructionForDamageState(stateValue);
        }
        catch
        {
            return false;
        }
    }

    private static void ResolveDamageStateBlock(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block, out Vector3i statePos, out BlockValue stateValue, out Block stateBlock)
    {
        statePos = pos;
        stateValue = blockValue;
        stateBlock = block;

        if (world == null || !blockValue.ischild)
            return;

        Vector3i parentPos = pos + blockValue.parent;
        BlockValue parentValue = world.GetBlock(parentPos);
        Block parentBlock = parentValue.Block;
        if (parentBlock == null || parentValue.ischild || parentValue.type != blockValue.type)
            return;

        statePos = parentPos;
        stateValue = parentValue;
        stateBlock = parentBlock;
    }

    private static int GetDamageStateLightPassIndex(Block block)
    {
        return GetNaturalLightDescriptor(block).DamageStateLightPassIndex;
    }

    private static NaturalLightRule ResolveNaturalLightRule(Block block)
    {
        return GetNaturalLightDescriptor(block).Rule;
    }

    private static NaturalLightDescriptor GetNaturalLightDescriptor(Block block)
    {
        if (block == null)
            return default(NaturalLightDescriptor);

        int blockId = block.blockID;
        NaturalLightDescriptor descriptor;
        if (s_naturalLightDescriptorByBlockId.TryGetValue(blockId, out descriptor) && descriptor.Initialized)
            return descriptor;

        descriptor = BuildNaturalLightDescriptor(block);
        descriptor.Initialized = true;
        s_naturalLightDescriptorByBlockId[blockId] = descriptor;
        return descriptor;
    }

    private static NaturalLightDescriptor BuildNaturalLightDescriptor(Block block)
    {
        NaturalLightDescriptor descriptor = new NaturalLightDescriptor
        {
            Initialized = true,
            Rule = NaturalLightRule.Compute,
            DamageStateLightPassIndex = ParseDamageStateLightPassIndex(block)
        };

        if (block == null)
            return descriptor;

        string name = block.GetBlockName();
        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        string model = GetBlockProperty(block, "Model");
        string shape = GetBlockProperty(block, "Shape");

        descriptor.CurtainOrBlindLike = ContainsIgnoreCase(name, "curtain") || ContainsIgnoreCase(tags, "curtain")
            || ContainsIgnoreCase(blockTag, "curtain") || ContainsIgnoreCase(place, "curtain")
            || ContainsIgnoreCase(name, "drape") || ContainsIgnoreCase(tags, "drape")
            || ContainsIgnoreCase(blockTag, "drape") || ContainsIgnoreCase(place, "drape")
            || ContainsIgnoreCase(name, "blind") || ContainsIgnoreCase(tags, "blind")
            || ContainsIgnoreCase(blockTag, "blind") || ContainsIgnoreCase(place, "blind");

        bool curtainOrDrape = ContainsIgnoreCase(name, "curtain") || ContainsIgnoreCase(tags, "curtain")
            || ContainsIgnoreCase(blockTag, "curtain") || ContainsIgnoreCase(place, "curtain")
            || ContainsIgnoreCase(name, "drape") || ContainsIgnoreCase(tags, "drape")
            || ContainsIgnoreCase(blockTag, "drape") || ContainsIgnoreCase(place, "drape");
        descriptor.HalfCurtainLike = curtainOrDrape && (ContainsIgnoreCase(name, "half") || ContainsIgnoreCase(tags, "half")
            || ContainsIgnoreCase(blockTag, "half") || ContainsIgnoreCase(place, "half"));

        descriptor.ShutterLike = ContainsIgnoreCase(name, "shutter") || ContainsIgnoreCase(tags, "shutter")
            || ContainsIgnoreCase(blockTag, "shutter") || ContainsIgnoreCase(place, "shutter");

        descriptor.HatchLike = block is BlockTrapDoor || ContainsIgnoreCase(name, "hatch") || ContainsIgnoreCase(tags, "hatch")
            || ContainsIgnoreCase(blockTag, "hatch") || ContainsIgnoreCase(place, "hatch")
            || ContainsIgnoreCase(name, "trapdoor") || ContainsIgnoreCase(tags, "trapdoor")
            || ContainsIgnoreCase(blockTag, "trapdoor") || ContainsIgnoreCase(place, "trapdoor");

        descriptor.DoorLike = block is BlockPoweredDoor || block is BlockTrapDoor
            || IsDoorLikeName(name) || IsDoorLikeName(tags) || IsDoorLikeName(blockTag) || IsDoorLikeName(place);

        descriptor.TransparentWindowLike = IsTransparentWindowLikeName(name) || IsTransparentWindowLikeName(tags)
            || IsTransparentWindowLikeName(blockTag) || IsTransparentWindowLikeName(place);

        bool glassLike = ContainsIgnoreCase(name, "glass") || ContainsIgnoreCase(tags, "glass")
            || ContainsIgnoreCase(blockTag, "glass") || ContainsIgnoreCase(place, "glass")
            || ContainsIgnoreCase(model, "glass") || ContainsIgnoreCase(shape, "glass");
        bool centered = ContainsIgnoreCase(name, "ctrplate")
            || ContainsIgnoreCase(name, "centered")
            || ContainsIgnoreCase(tags, "centered")
            || ContainsIgnoreCase(blockTag, "centered")
            || ContainsIgnoreCase(place, "centered")
            || ContainsIgnoreCase(model, "centered")
            || ContainsIgnoreCase(shape, "centered");
        descriptor.CenteredNonGlassNaturalLightCover = centered && !glassLike && !descriptor.CurtainOrBlindLike
            && !descriptor.ShutterLike && !descriptor.DoorLike && block.lightOpacity > 0;

        string explicitRule = GetBlockProperty(block, "AdvFarmingNaturalLight");
        if (!string.IsNullOrEmpty(explicitRule))
        {
            if (explicitRule.Equals("pass", StringComparison.OrdinalIgnoreCase) || explicitRule.Equals("allow", StringComparison.OrdinalIgnoreCase))
                descriptor.Rule = NaturalLightRule.AlwaysPass;
            else if (explicitRule.Equals("block", StringComparison.OrdinalIgnoreCase) || explicitRule.Equals("deny", StringComparison.OrdinalIgnoreCase))
                descriptor.Rule = NaturalLightRule.AlwaysBlock;
            else if (explicitRule.Equals("door", StringComparison.OrdinalIgnoreCase) || explicitRule.Equals("state", StringComparison.OrdinalIgnoreCase))
                descriptor.Rule = NaturalLightRule.DoorStateDependent;
        }
        else if (descriptor.CurtainOrBlindLike || descriptor.ShutterLike || descriptor.CenteredNonGlassNaturalLightCover)
        {
            descriptor.Rule = NaturalLightRule.DoorStateDependent;
        }
        else if (descriptor.TransparentWindowLike)
        {
            descriptor.Rule = NaturalLightRule.AlwaysPass;
        }
        else if (descriptor.HatchLike || descriptor.DoorLike)
        {
            descriptor.Rule = NaturalLightRule.DoorStateDependent;
        }

        descriptor.CoverLike = descriptor.DoorLike || descriptor.CurtainOrBlindLike || descriptor.ShutterLike || descriptor.CenteredNonGlassNaturalLightCover;
        return descriptor;
    }

    private static int ParseDamageStateLightPassIndex(Block block)
    {
        int passIndex = 1;
        string passIndexText = GetBlockProperty(block, "AdvFarmingLightPassDamageStateIndex");
        if (!string.IsNullOrEmpty(passIndexText))
        {
            int parsed;
            if (int.TryParse(passIndexText, out parsed))
                passIndex = parsed;
        }
        return passIndex;
    }


    private static bool IsShutterLike(Block block)
    {
        return GetNaturalLightDescriptor(block).ShutterLike;
    }

    private static bool IsCurtainOrBlindLike(Block block)
    {
        return GetNaturalLightDescriptor(block).CurtainOrBlindLike;
    }

    private static bool IsHalfCurtainLike(Block block)
    {
        return GetNaturalLightDescriptor(block).HalfCurtainLike;
    }

    private static bool IsCenteredNonGlassNaturalLightCover(Block block)
    {
        return GetNaturalLightDescriptor(block).CenteredNonGlassNaturalLightCover;
    }


    private static bool? TryGetVerifiedWallHatchPassState(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block)
    {
        // Hatches are door-like, but the vanilla movement-plane query does not match the
        // visual aperture for every placement, rotation, and open/closed state.
        // Use grounded placement-specific matrices from the 2026-07-07 hatch logs.
        if (world == null || block == null || !IsHatchLike(block))
            return null;

        DoorPlacementPlane placementPlane = InferAllRotationDoorPlacementPlane(world, pos, blockValue, block, BlockFaceFlag.All);
        if (placementPlane != DoorPlacementPlane.Wall && placementPlane != DoorPlacementPlane.FloorCeiling)
            return null;

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        bool open = IsUprightDoorOpenState(world, statePos, stateValue, stateBlock);
        if (placementPlane == DoorPlacementPlane.FloorCeiling)
            return !IsVerifiedCeilingHatchBlockState(stateValue.rotation, open);

        ShutterWallApertureAxis apertureAxis = GetVerifiedWallShutterApertureAxis(world, pos);
        if (apertureAxis != ShutterWallApertureAxis.EastWest && apertureAxis != ShutterWallApertureAxis.NorthSouth)
            return null;

        return !IsVerifiedWallHatchBlockState(apertureAxis, stateValue.rotation, open);
    }

    private static bool IsVerifiedWallHatchBlockState(ShutterWallApertureAxis apertureAxis, byte rotation, bool open)
    {
        if (apertureAxis == ShutterWallApertureAxis.EastWest)
            return IsVerifiedFirstWallHatchBlockState(rotation, open);

        if (apertureAxis == ShutterWallApertureAxis.NorthSouth)
            return IsVerifiedSecondWallHatchBlockState(rotation, open);

        return false;
    }

    private static bool IsVerifiedFirstWallHatchBlockState(byte rotation, bool open)
    {
        if (open)
        {
            switch (rotation)
            {
                case 1:
                case 3:
                case 5:
                case 7:
                case 9:
                case 11:
                case 17:
                case 19:
                    return true;
                default:
                    return false;
            }
        }

        switch (rotation)
        {
            case 12:
            case 13:
            case 14:
            case 15:
            case 20:
            case 21:
            case 22:
            case 23:
                return true;
            default:
                return false;
        }
    }

    private static bool IsVerifiedSecondWallHatchBlockState(byte rotation, bool open)
    {
        if (open)
        {
            switch (rotation)
            {
                case 0:
                case 2:
                case 4:
                case 6:
                case 12:
                case 14:
                case 20:
                case 22:
                    return true;
                default:
                    return false;
            }
        }

        switch (rotation)
        {
            case 8:
            case 9:
            case 10:
            case 11:
            case 16:
            case 17:
            case 18:
            case 19:
                return true;
            default:
                return false;
        }
    }

    private static bool IsVerifiedCeilingHatchBlockState(byte rotation, bool open)
    {
        if (open)
        {
            switch (rotation)
            {
                case 8:
                case 10:
                case 13:
                case 15:
                case 16:
                case 18:
                case 21:
                case 23:
                    return true;
                default:
                    return false;
            }
        }

        switch (rotation)
        {
            case 0:
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
            case 7:
                return true;
            default:
                return false;
        }
    }

    private static bool IsHatchLike(Block block)
    {
        return GetNaturalLightDescriptor(block).HatchLike;
    }

    private static bool IsVerifiedCurtainBlindPassState(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block)
    {
        // Grounded from the curtain/drape/blind logs and user-visible rules:
        // - any damage state beyond the first must pass light;
        // - intact curtains/drapes/blinds default to pass;
        // - only placement-specific logged wall/ceiling rotations block.
        // Placement matters: the same rotation byte is not globally meaningful across
        // east/west wall, north/south wall, and floor/ceiling apertures.
        if (world == null || block == null)
            return true;

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        if (IsCurtainBlindDamagedBeyondFirstState(statePos, stateValue, stateBlock))
            return true;

        DoorPlacementPlane placementPlane = InferAllRotationDoorPlacementPlane(world, pos, blockValue, block, BlockFaceFlag.All);
        if (placementPlane == DoorPlacementPlane.FloorCeiling)
            return !IsVerifiedCeilingCurtainBlindBlockRotation(stateValue.rotation);

        if (placementPlane != DoorPlacementPlane.Wall)
            return true;

        ShutterWallApertureAxis apertureAxis = GetVerifiedWallShutterApertureAxis(world, pos);
        if (apertureAxis == ShutterWallApertureAxis.EastWest)
            return !IsVerifiedFirstWallCurtainBlindBlockRotation(stateValue.rotation);

        if (apertureAxis == ShutterWallApertureAxis.NorthSouth)
            return !IsVerifiedSecondWallCurtainBlindBlockRotation(stateValue.rotation);

        return true;
    }

    private static bool IsCurtainBlindDamagedBeyondFirstState(Vector3i statePos, BlockValue stateValue, Block stateBlock)
    {
        if (stateBlock == null)
            return false;

        if (stateValue.damage > 0)
            return true;

        if (stateBlock.shape is BlockShapeModelEntity shape)
        {
            try
            {
                return shape.GetDamageStateIndex(stateValue) > 0;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsVerifiedFirstWallCurtainBlindBlockRotation(byte rotation)
    {
        // First/east-west wall curtain blocker set from output_log_client__2026-07-07__14-27-55.
        switch (rotation)
        {
            case 1:
            case 3:
            case 5:
            case 7:
            case 9:
            case 11:
            case 17:
            case 19:
                return true;
            default:
                return false;
        }
    }

    private static bool IsVerifiedSecondWallCurtainBlindBlockRotation(byte rotation)
    {
        // Second/north-south wall curtain blocker set from output_log_client__2026-07-07__15-33-44.
        // All other rotations on that placement default to pass.
        switch (rotation)
        {
            case 0:
            case 2:
            case 4:
            case 6:
            case 12:
            case 14:
            case 20:
            case 22:
                return true;
            default:
                return false;
        }
    }

    private static bool IsVerifiedCeilingCurtainBlindBlockRotation(byte rotation)
    {
        // Ceiling/floor curtain blocker set from output_log_client__2026-07-07__15-48-57.
        // All other rotations on that placement default to pass.
        switch (rotation)
        {
            case 8:
            case 10:
            case 13:
            case 15:
            case 16:
            case 18:
            case 21:
            case 23:
                return true;
            default:
                return false;
        }
    }

    private static bool IsVerifiedCenteredNonGlassPassState(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block)
    {
        // Grounded from centered non-glass window plug/CTR plate logs:
        // - first/east-west wall blocker set: output_log_client__2026-07-07__16-02-14;
        // - second/north-south wall blocker set: output_log_client__2026-07-07__16-17-18;
        // - ceiling/floor blocker set: output_log_client__2026-07-07__16-27-54.
        // Centered non-glass covers default to pass; only placement-specific logged
        // rotations block. Glass-centered plates remain excluded by the classifier.
        if (world == null || block == null)
            return true;

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        DoorPlacementPlane placementPlane = InferAllRotationDoorPlacementPlane(world, pos, blockValue, block, BlockFaceFlag.All);
        if (placementPlane == DoorPlacementPlane.FloorCeiling)
            return !IsVerifiedCeilingCenteredNonGlassBlockRotation(stateValue.rotation);

        if (placementPlane != DoorPlacementPlane.Wall)
            return true;

        ShutterWallApertureAxis apertureAxis = GetVerifiedWallShutterApertureAxis(world, pos);
        if (apertureAxis == ShutterWallApertureAxis.EastWest)
            return !IsVerifiedFirstWallCenteredNonGlassBlockRotation(stateValue.rotation);

        if (apertureAxis == ShutterWallApertureAxis.NorthSouth)
            return !IsVerifiedSecondWallCenteredNonGlassBlockRotation(stateValue.rotation);

        return true;
    }

    private static bool IsVerifiedFirstWallCenteredNonGlassBlockRotation(byte rotation)
    {
        // First/east-west wall centered non-glass blocker set from windowPlug01CTRPlate /
        // plainWoodWindowPlugCTRPlate in output_log_client__2026-07-07__16-02-14.
        // All other rotations on this placement default to pass.
        switch (rotation)
        {
            case 1:
            case 3:
            case 5:
            case 7:
            case 9:
            case 11:
            case 17:
            case 19:
                return true;
            default:
                return false;
        }
    }

    private static bool IsVerifiedSecondWallCenteredNonGlassBlockRotation(byte rotation)
    {
        // Second/north-south wall centered non-glass blocker set from windowPlug01CTRPlate
        // in output_log_client__2026-07-07__16-17-18.
        // All other rotations on this placement default to pass.
        switch (rotation)
        {
            case 0:
            case 2:
            case 4:
            case 6:
            case 12:
            case 14:
            case 20:
            case 22:
                return true;
            default:
                return false;
        }
    }

    private static bool IsVerifiedCeilingCenteredNonGlassBlockRotation(byte rotation)
    {
        // Ceiling/floor centered non-glass blocker set from windowPlug01CTRPlate
        // in output_log_client__2026-07-07__16-27-54.
        // All other rotations on this placement default to pass.
        switch (rotation)
        {
            case 8:
            case 10:
            case 13:
            case 15:
            case 16:
            case 18:
            case 21:
            case 23:
                return true;
            default:
                return false;
        }
    }

    private enum ShutterWallApertureAxis : byte
    {
        Unknown = 0,
        EastWest = 1,
        NorthSouth = 2
    }

    private static bool IsVerifiedShutterPassState(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (world == null || block == null)
            return false;

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        bool open = IsUprightDoorOpenState(world, pos, blockValue, block);
        DoorPlacementPlane placementPlane = InferAllRotationDoorPlacementPlane(world, pos, blockValue, block, BlockFaceFlag.All);
        if (placementPlane == DoorPlacementPlane.FloorCeiling)
            return IsVerifiedCeilingShutterPassRotationState(stateValue.rotation, open);

        if (placementPlane != DoorPlacementPlane.Wall)
            return false;

        ShutterWallApertureAxis apertureAxis = GetVerifiedWallShutterApertureAxis(world, pos);
        if (apertureAxis == ShutterWallApertureAxis.EastWest)
            return IsVerifiedPreviousWallShutterPassRotationState(stateValue.rotation, open);

        if (apertureAxis == ShutterWallApertureAxis.NorthSouth)
            return IsVerifiedNewerWallShutterPassState(stateValue.rotation, open);

        return false;
    }

    private static ShutterWallApertureAxis GetVerifiedWallShutterApertureAxis(IBlockAccess world, Vector3i pos)
    {
        if (world == null)
            return ShutterWallApertureAxis.Unknown;

        int eastWestScore = GetAxisOpenScore(world, pos, 1, 0, 0);
        int northSouthScore = GetAxisOpenScore(world, pos, 0, 0, 1);

        if (eastWestScore > northSouthScore)
            return ShutterWallApertureAxis.EastWest;
        if (northSouthScore > eastWestScore)
            return ShutterWallApertureAxis.NorthSouth;
        return ShutterWallApertureAxis.Unknown;
    }

    private static bool IsVerifiedPreviousWallShutterPassRotationState(byte rotation, bool open)
    {
        // Verified east/west-wall shutter table from the 12:14 and 12:34 logs.
        // Preserve this table exactly so the first validated wall does not regress.
        if (rotation >= 24 && rotation <= 31)
            return true;

        switch (rotation)
        {
            case 0:
            case 2:
            case 4:
            case 6:
            case 8:
            case 10:
            case 12:
            case 13:
            case 14:
            case 15:
            case 16:
            case 18:
            case 20:
            case 21:
            case 22:
            case 23:
                return true;
            case 1:
            case 3:
            case 5:
            case 7:
            case 9:
            case 11:
            case 17:
            case 19:
                return open;
            default:
                return false;
        }
    }

    private static bool IsVerifiedNewerWallShutterPassState(byte rotation, bool open)
    {
        // Grounded from the perpendicular north/south wall shutter logs:
        // - output_log_client__2026-07-07__12-45-49: the listed closed states block.
        // - output_log_client__2026-07-07__13-30-41: the same rotation/meta/meta2 tuples can pass
        //   when the tile-entity door feature is open. Therefore BlockValue meta/meta2 cannot be
        //   used as the state discriminator for shutters; use the runtime door open state.
        if (open)
            return true;

        switch (rotation)
        {
            case 0:
            case 2:
            case 4:
            case 6:
            case 12:
            case 14:
            case 20:
            case 22:
                return false;
            default:
                return true;
        }
    }

    private static bool IsVerifiedCeilingShutterPassRotationState(byte rotation, bool open)
    {
        // Grounded from the ceiling shutter blocker log:
        // - output_log_client__2026-07-07__13-44-25 contains closed ceiling shutters at
        //   rotations 8, 10, 13, 15, 16, 18, 21, and 23 that must remain blockers.
        // - Ceiling shutters otherwise default to pass-through because the placement plane,
        //   not the rotation byte alone, determines whether the shutter covers the aperture.
        if (open)
            return true;

        switch (rotation)
        {
            case 8:
            case 10:
            case 13:
            case 15:
            case 16:
            case 18:
            case 21:
            case 23:
                return false;
            default:
                return true;
        }
    }

    private static bool IsCoverLikeByData(Block block)
    {
        return GetNaturalLightDescriptor(block).CoverLike;
    }

    private static BlockValue GetBlockAt(IChunkAccess chunkAccess, Vector3i pos)
    {
        if (pos.y < 0 || pos.y > byte.MaxValue || chunkAccess == null)
            return BlockValue.Air;

        IChunk chunk = chunkAccess.GetChunkFromWorldPos(pos.x, pos.y, pos.z);
        if (chunk == null)
            return BlockValue.Air;

        Chunk concreteChunk = chunk as Chunk;
        if (concreteChunk != null)
            return concreteChunk.GetBlock(World.toBlockXZ(pos.x), pos.y, World.toBlockXZ(pos.z));

        IBlockAccess blockAccess = chunkAccess as IBlockAccess;
        return blockAccess != null ? blockAccess.GetBlock(pos) : BlockValue.Air;
    }

    private static int GetBaseOpacity(BlockValue value)
    {
        Block block = value.Block;
        if (block == null)
            return 0;

        int shapeOpacity;
        bool shapeThermalPassable;
        string shapeCategory;
        if (AdvancedFarmingShapeOpacityPolicy.TryGetGeneratedShapePolicy(block, out shapeOpacity, out shapeThermalPassable, out shapeCategory))
            return shapeOpacity;

        return block.lightOpacity;
    }

    private static bool? SafeIsMovementBlocked(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block, BlockFaceFlag face)
    {
        // Powered doors expose a whole-door vanilla open state. Trap doors/hatches do not
        // use a meta shortcut here: their current occupied plane must be queried on the
        // exact face being crossed so default/open inverted placements work correctly.
        if (block is BlockPoweredDoor)
            return !BlockPoweredDoor.IsDoorOpen(blockValue.meta);

        if (world == null)
            return null;

        try
        {
            return block.IsMovementBlocked(world, pos, blockValue, face);
        }
        catch
        {
            return null;
        }
    }

    private static BlockFaceFlag GetClosedUprightDoorBlockingFaceMask(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block)
    {
        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        return GetClosedDoorBlockingFaceMaskFromRotation(stateValue.rotation);
    }


    private static byte GetDoorStateRotation(IBlockAccess world, Vector3i pos, BlockValue blockValue, Block block)
    {
        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);
        return stateValue.rotation;
    }

    private static BlockFaceFlag GetClosedDoorBlockingFaceMaskFromRotation(byte rotation)
    {
        // 7DTD stores door placement orientation in the full rotation byte. The low two
        // bits still identify cardinal yaw for normal upright doors. For all-rotation
        // horizontal/ceiling-door placements, however, the only light-sealing question is
        // whether the current pose occupies the vertical Top/Bottom aperture. When that
        // pose does NOT occupy the aperture, return None instead of a side-plane mask: the
        // swung-away leaf can still be a visual side plane, but treating that side plane as
        // a SUN propagation blocker prevents light from entering the room after it passes
        // through the ceiling hole.
        if (rotation < 4)
            return GetHorizontalDoorSidePlaneMask(rotation);

        if (rotation >= 24)
            return BlockFaceFlag.None;

        return IsOpenedHorizontalDoorStateSealingRotation(rotation)
            ? BlockFaceFlag.None
            : BlockFaceFlag.Top | BlockFaceFlag.Bottom;
    }

    private static BlockFaceFlag GetOpenedDoorBlockingFaceMaskFromRotation(byte rotation)
    {
        if (rotation < 4)
            return GetPerpendicularDoorFaceMask(GetHorizontalDoorSidePlaneMask(rotation));

        if (rotation >= 24)
            return BlockFaceFlag.None;

        return IsOpenedHorizontalDoorStateSealingRotation(rotation)
            ? BlockFaceFlag.Top | BlockFaceFlag.Bottom
            : BlockFaceFlag.None;
    }

    private static BlockFaceFlag DirectionToFace(Vector3i dir)
    {
        if (dir.x > 0) return BlockFaceFlag.East;
        if (dir.x < 0) return BlockFaceFlag.West;
        if (dir.y > 0) return BlockFaceFlag.Top;
        if (dir.y < 0) return BlockFaceFlag.Bottom;
        if (dir.z > 0) return BlockFaceFlag.North;
        if (dir.z < 0) return BlockFaceFlag.South;
        return BlockFaceFlag.None;
    }

    private static void StepToFaces(int dx, int dy, int dz, out BlockFaceFlag fromFace, out BlockFaceFlag toFace)
    {
        if (dy > 0) { fromFace = BlockFaceFlag.Top; toFace = BlockFaceFlag.Bottom; return; }
        if (dy < 0) { fromFace = BlockFaceFlag.Bottom; toFace = BlockFaceFlag.Top; return; }
        if (dx > 0) { fromFace = BlockFaceFlag.East; toFace = BlockFaceFlag.West; return; }
        if (dx < 0) { fromFace = BlockFaceFlag.West; toFace = BlockFaceFlag.East; return; }
        if (dz > 0) { fromFace = BlockFaceFlag.North; toFace = BlockFaceFlag.South; return; }
        if (dz < 0) { fromFace = BlockFaceFlag.South; toFace = BlockFaceFlag.North; return; }
        fromFace = BlockFaceFlag.All;
        toFace = BlockFaceFlag.All;
    }

    private static bool IsDoorLikeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        return ContainsIgnoreCase(value, "door") || ContainsIgnoreCase(value, "hatch") || ContainsIgnoreCase(value, "trapdoor");
    }

    private static bool IsTransparentWindowLikeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        return ContainsIgnoreCase(value, "window") || ContainsIgnoreCase(value, "glass");
    }

    private static bool ContainsIgnoreCase(string value, string pattern)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetBlockProperty(Block block, string key)
    {
        if (block == null || block.Properties == null || block.Properties.Values == null)
            return null;
        return block.Properties.Values.ContainsKey(key) ? block.Properties.Values[key] : null;
    }
}
