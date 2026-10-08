using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Advanced Farming light validation helper.
///
/// Model: crop-position SUN light (max of SUN at the crop and one block above, mirroring the
/// vanilla player-enclosure concept) is the base authority. Full blocks already occlude vanilla
/// SUN correctly, so cropSun below the required level always fails and needs no geometry.
///
/// When cropSun is sufficient, a per-face verification runs, because doors, hatches, curtains/drapes
/// and partial/rotatable shapes can leak vanilla SUN light without physically sealing the space.
/// Vanilla LightOpacity is a static per-shape+material value and, per the base game's own design,
/// does not vary with a block's rotation - so it cannot distinguish "this door in this rotation
/// seals the north opening" from "the same door rotated 90 degrees seals the ceiling instead."
/// Rotation only matters to the physical, per-face collision data the engine already tracks for
/// movement, so that is the signal this service uses instead of a single "is it open" flag.
///
/// Every step of the flood crosses a specific shared face between two specific cells (e.g.
/// stepping +X crosses cell A's East face and cell B's West face). Each side is asked
/// individually, for that one face only, whether its current shape+rotation+meta blocks it -
/// never a combined "any face" query, which is what caused the previous version to report a
/// door as blocking even when only an unrelated face of that same door was sealed. A block that
/// blocks this one face but is not uniformly solid on every face (IsMovementBlocked(All) is
/// false) is a "cover" - the only kind of blocker allowed to override sufficient cropSun. A
/// block uniformly solid on every face is "structural" and can never override cropSun, because
/// vanilla's own scalar opacity would already be low there regardless of rotation.
///
/// The verification succeeds the moment any reached cell has this service's own open column to
/// the sky (memoized per cell). It may only FAIL when at least one cover was actually encountered
/// blocking the flood; if the flood exhausts having met no cover anywhere, the crop PASSES on the
/// strength of its vanilla cropSun ("fail-open" for unmodeled geometry).
///
/// No physics raycasts: physics colliders are not reliably present on dedicated servers or in
/// player-distant chunks. XML `AdvFarmingNaturalLight` (pass|block) remains the final override for
/// any block whose per-face collision data does not represent its true light-blocking behavior
/// (e.g. a purely decorative curtain mesh with no collision at all).
///
/// VERIFY-BEFORE-TRUST NOTE: the mapping from BlockFaceFlag.North/South/East/West to world
/// +X/-X/+Z/-Z direction below is the standard 7 Days to Die compass convention and has not been
/// confirmed against this project's assembly. BlockFaceFlag.Top/Bottom are unambiguous. To verify
/// the horizontal mapping, place any block with a genuinely asymmetric shape (a plate/half-slab
/// that only occupies one horizontal side) in a known rotation and run `blockforensics` at that
/// position: exactly one of isMovementBlockedNorth/South/East/West should be true, matching the
/// side the shape visually occupies. If the wrong pair reports blocked, swap the two lines marked
/// below in <see cref="StepToFaces"/> - nothing else in the file depends on which physical
/// direction each flag name refers to, only that the two faces of one shared boundary agree.
/// </summary>
public static class AdvancedFarmingLightService
{
    private const int DirectColumnScanHeight = 0; // 0 means scan to world top; do not cap sky checks.
    private const int ExposureSearchHorizontalRadius = 12;
    private const int ExposureSearchUp = 8;
    private const int ExposureSearchDown = 1;
    private const int ExposureSearchWidth = ExposureSearchHorizontalRadius * 2 + 1;
    private const int ExposureSearchHeight = ExposureSearchUp + ExposureSearchDown + 1;
    private const int MaxExposureNodes = ExposureSearchWidth * ExposureSearchWidth * ExposureSearchHeight;
    // v126 correctness rollback: long per-position light TTL/jitter can leave adjacent
    // crops displaying different natural-light answers if any dynamic-cover invalidation
    // path is missed. Light exposure evaluation has been profiled as cheap enough that
    // coherence is more important than the previous 120+60s cache lifetime.
    private const int CacheSeconds = 15;
    private const int CacheJitterSeconds = 0;
    private const int MaxCacheEntries = 4096;

    private struct CachedCoverPresence
    {
        public Vector3i Pos;
        public int WorldSeconds;
        public int PolicyVersion;
        public bool HasCover;
    }

    // Keep cover-presence TTL aligned with the light result TTL. A stale "no nearby cover"
    // answer is enough to bypass the exact-cover search and make neighbors disagree.
    private const int CoverPresenceCacheSeconds = 15;
    private const int CoverPresenceCacheJitterSeconds = 0;

    private static readonly Dictionary<long, CachedLightResult> s_lightCache = new Dictionary<long, CachedLightResult>(1024);
    private static readonly Dictionary<int, NaturalLightRule> s_ruleByBlockType = new Dictionary<int, NaturalLightRule>(1024);
    private static readonly Dictionary<long, CachedCoverPresence> s_coverPresenceCache = new Dictionary<long, CachedCoverPresence>(1024);
    private static readonly List<long> s_cacheRemovalScratch = new List<long>(128);

    private static readonly int[] s_visitedStamp = new int[MaxExposureNodes];
    private static readonly Vector3i[] s_queue = new Vector3i[MaxExposureNodes];
    private static readonly int[] s_sourceMemoStamp = new int[MaxExposureNodes];
    private static readonly bool[] s_sourceMemoOpen = new bool[MaxExposureNodes];
    private static readonly BlockValue[] s_blockSnapshot = new BlockValue[MaxExposureNodes];
    private static readonly int[] s_blockSnapshotStamp = new int[MaxExposureNodes];

    private static int s_stamp;

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

    private enum CellVerdict : byte
    {
        Pass = 0,
        StructureBlocked = 1,
        CoverBlocked = 2
    }

    public struct LightResult
    {
        public bool HasSunLight;
        public bool HasBlockLight;
        public bool HasOpenSky;
        public bool RawHasSunLight;
        public bool RawHasOpenSky;
        public bool NaturalLightPathOpen;
        public int RequiredLightLevel;
        public int RequiredNaturalSunlight;
        public bool NaturalLightStrongEnough;
        public byte SunLight;
        public byte BlockLight;
        public byte CropSunLight;
        public float VanillaAmountEnclosed;
        public bool VanillaEnclosed;
        public bool GeometryBlockerOverride;
        public string NaturalLightMode;
        public Vector3i BlockingPos;
        public string BlockingBlock;
        public Vector3i ExitPos;
        public int ExposureNodesVisited;
        public int ExposureTransparentCandidates;
        public int ExposureAirTransparentCandidates;
        public int ExposureNonAirTransparentCandidates;
        public int CoverBlockersOnPath;
        public bool TrustedCropSunWithoutCover;
        public bool CacheHit;
        public int CacheAgeSeconds;
        public int CacheLifetimeSeconds;
    }

    public struct LightColumnEntry
    {
        public Vector3i Pos;
        public string BlockName;
        public string ClassName;
        public bool IsAir;
        public bool IsWater;
        public bool IsChild;
        public int LightOpacity;
        public int EffectiveLightOpacity;
        public string ShapeOpacityPolicy;
        public bool IsSeeThrough;
        public bool IsMovementBlocked;
        public bool MaterialIsPlant;
        public string Rule;
        public string DoorDecision;
        public bool Transparent;
    }

    public struct LightColumnDiagnostic
    {
        public Vector3i RootPos;
        public bool Open;
        public Vector3i FirstBlockerPos;
        public string FirstBlockerBlock;
        public int AirCount;
        public int NonAirTransparentCount;
        public int NonAirOpaqueCount;
        public List<LightColumnEntry> Entries;
    }

    public struct LightSearchColumnEntry
    {
        public Vector3i RootPos;
        public int Dx;
        public int Dz;
        public int ChebyshevDistance;
        public bool Open;
        public Vector3i FirstBlockerPos;
        public string FirstBlockerBlock;
        public int AirCount;
        public int NonAirTransparentCount;
        public int NonAirOpaqueCount;
    }

    public struct LightSearchDiagnostic
    {
        public Vector3i PlantPos;
        public Vector3i StartPos;
        public bool StartTransparent;
        public string StartBlock;
        public int SearchHorizontalRadius;
        public int SearchUp;
        public int ColumnsChecked;
        public int OpenColumns;
        public int BlockedColumns;
        public List<LightSearchColumnEntry> OpenColumnSamples;
        public List<LightSearchColumnEntry> BlockedColumnSamples;
    }

    private struct CachedLightResult
    {
        public LightResult Result;
        public Vector3i Pos;
        public int WorldSeconds;
        public int PolicyVersion;
        public bool IsNight;
    }

    private struct NaturalLightExposure
    {
        public bool Open;
        public bool UsedSearch;
        public string Mode;
        public Vector3i BlockingPos;
        public string BlockingBlock;
        public Vector3i ExitPos;
        public int NodesVisited;
        public int TransparentCandidates;
        public int AirTransparentCandidates;
        public int NonAirTransparentCandidates;
        public int CoverBlockersOnPath;
        public bool TrustedCropSunWithoutCover;
    }

    public static void ClearCache()
    {
        s_lightCache.Clear();
        s_coverPresenceCache.Clear();
        AdvancedFarmingHoverTextService.ClearConditionCache();
    }

    public static void NotifyWorldBlockChanged(Vector3i blockPos)
    {
        // Geometry changes must be reflected immediately in crop hover/growth checks.
        ClearCacheAround(blockPos, ExposureSearchHorizontalRadius + 2, ExposureSearchUp + ExposureSearchDown + 2);
    }

    public static void ClearCacheAround(Vector3i center, int horizontalRadius, int verticalRadius)
    {
        AdvancedFarmingHoverTextService.ClearConditionCache();

        if (horizontalRadius < 0)
            horizontalRadius = 0;
        if (verticalRadius < 0)
            verticalRadius = 0;

        s_cacheRemovalScratch.Clear();
        foreach (KeyValuePair<long, CachedLightResult> pair in s_lightCache)
        {
            Vector3i pos = pair.Value.Pos;
            if (Math.Abs(pos.x - center.x) <= horizontalRadius
                && Math.Abs(pos.z - center.z) <= horizontalRadius
                && Math.Abs(pos.y - center.y) <= verticalRadius)
            {
                s_cacheRemovalScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < s_cacheRemovalScratch.Count; i++)
            s_lightCache.Remove(s_cacheRemovalScratch[i]);
        s_cacheRemovalScratch.Clear();

        if (s_coverPresenceCache.Count == 0)
            return;

        foreach (KeyValuePair<long, CachedCoverPresence> pair in s_coverPresenceCache)
        {
            Vector3i pos = pair.Value.Pos;
            if (Math.Abs(pos.x - center.x) <= horizontalRadius
                && Math.Abs(pos.z - center.z) <= horizontalRadius
                && Math.Abs(pos.y - center.y) <= verticalRadius)
            {
                s_cacheRemovalScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < s_cacheRemovalScratch.Count; i++)
            s_coverPresenceCache.Remove(s_cacheRemovalScratch[i]);
        s_cacheRemovalScratch.Clear();
    }

    public static string BuildStatus()
    {
        return "[AdvancedFarming LightService] cacheSeconds=" + CacheSeconds
            + "+jitterSeconds=" + CacheJitterSeconds
            + " coverPresenceCacheSeconds=" + CoverPresenceCacheSeconds
            + "+coverJitterSeconds=" + CoverPresenceCacheJitterSeconds
            + " exteriorSourceScanHeight=" + (DirectColumnScanHeight > 0 ? DirectColumnScanHeight.ToString() : "worldTop")
            + " exposureSearchHorizontalRadius=" + ExposureSearchHorizontalRadius
            + " exposureSearchUp=" + ExposureSearchUp
            + " lightModel=cropSunFromPatchedLightProcessor"
            + " overridePolicy=noneActive_lightProcessorOwnsOcclusion"
            + " doorState=perFaceMovementBlockedByExactStepDirection"
            + " faceMappingVerified=false(seeVerifyBeforeTrustNote)"
            + " physicsRaycast=false"
            + " vanillaExit=false"
            + " ruleCacheEntries=" + s_ruleByBlockType.Count
            + " cacheEntries=" + s_lightCache.Count
            + " dynamicOpacity=LightProcessorPatch"
            + " minimumNaturalSunlight=" + AdvancedFarmingRuntimePolicy.MinimumNaturalSunlight;
    }

    public static bool TryGetCurrentBlockLight(WorldBase world, Vector3i plantPos, out byte blockLight)
    {
        blockLight = 0;
        if (world == null)
            return false;

        ChunkCluster chunkCluster = world.ChunkCache;
        if (chunkCluster == null)
            return false;

        Vector3i lightPos = plantPos + Vector3i.up;
        byte blockAtPlant = chunkCluster.GetLight(plantPos, Chunk.LIGHT_TYPE.BLOCK);
        byte blockAtAbove = chunkCluster.GetLight(lightPos, Chunk.LIGHT_TYPE.BLOCK);
        blockLight = blockAtPlant > blockAtAbove ? blockAtPlant : blockAtAbove;
        return true;
    }

    public static LightResult Evaluate(
        WorldBase world,
        Vector3i plantPos,
        int lightLevelStay,
        int lightLevelGrow,
        bool isMushroom,
        bool advancedFarming)
    {
        LightResult defaultResult = new LightResult
        {
            HasSunLight = true,
            HasBlockLight = true,
            HasOpenSky = true,
            RawHasSunLight = true,
            RawHasOpenSky = true,
            NaturalLightPathOpen = true,
            RequiredLightLevel = 0,
            RequiredNaturalSunlight = 0,
            NaturalLightStrongEnough = true,
            SunLight = 0,
            BlockLight = 0,
            CropSunLight = 15,
            VanillaAmountEnclosed = 0f,
            VanillaEnclosed = false,
            GeometryBlockerOverride = false,
            NaturalLightMode = "bypass",
            BlockingPos = Vector3i.zero,
            BlockingBlock = string.Empty,
            ExitPos = Vector3i.zero,
            ExposureNodesVisited = 0,
            ExposureTransparentCandidates = 0,
            ExposureAirTransparentCandidates = 0,
            ExposureNonAirTransparentCandidates = 0,
            CoverBlockersOnPath = 0,
            TrustedCropSunWithoutCover = false,
            CacheHit = false,
            CacheAgeSeconds = 0,
            CacheLifetimeSeconds = 0
        };

        if (world == null)
            return defaultResult;

        if (!advancedFarming || isMushroom)
            return defaultResult;

        ChunkCluster chunkCluster = world.ChunkCache;
        if (chunkCluster == null)
            return defaultResult;

        long stutterTraceStartTicks = 0L;
        if (AdvancedFarmingStutterTraceService.Active)
            stutterTraceStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();

        int required = Math.Max(lightLevelStay, lightLevelGrow);
        int requiredNaturalSunlight = AdvancedFarmingRuntimePolicy.GetRequiredNaturalSunlight(required);
        int now = RebirthUtilities.TotalGameSecondsPassed();
        bool isNight = AdvancedFarmingTemperatureService.IsNight(world);
        int policyVersion = AdvancedFarmingRuntimePolicy.PolicyVersion;
        long cacheKey = MakeCacheKey(plantPos, required);

        if (s_lightCache.TryGetValue(cacheKey, out CachedLightResult cached))
        {
            int cacheAgeSeconds = now - cached.WorldSeconds;
            int cacheLifetimeSeconds = GetLightCacheLifetimeSeconds(cached.Pos, cached.Result.RequiredLightLevel);
            if (cacheAgeSeconds >= 0
                && cacheAgeSeconds <= cacheLifetimeSeconds
                && cached.PolicyVersion == policyVersion
                && cached.IsNight == isNight)
            {
                AdvancedFarmingPerfSnapshotService.RecordLightExposureCacheQuery(true);
                LightResult cachedResult = cached.Result;
                cachedResult.CacheHit = true;
                cachedResult.CacheAgeSeconds = cacheAgeSeconds;
                cachedResult.CacheLifetimeSeconds = cacheLifetimeSeconds;
                if (AdvancedFarmingStutterTraceService.Active)
                {
                    long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - stutterTraceStartTicks;
                    if (AdvancedFarmingStutterTraceService.ShouldRecordSlow(elapsedTicks))
                        AdvancedFarmingStutterTraceService.RecordSlowOperation("lightEvaluate", plantPos, elapsedTicks, "cacheHit=true age=" + cacheAgeSeconds + " lifetime=" + cacheLifetimeSeconds + " required=" + required + " requiredNaturalSunlight=" + requiredNaturalSunlight);
                }
                return cachedResult;
            }
        }

        AdvancedFarmingPerfSnapshotService.RecordLightExposureCacheQuery(false);

        Vector3i lightPos = plantPos + Vector3i.up;
        byte sunAtPlant = chunkCluster.GetLight(plantPos, Chunk.LIGHT_TYPE.SUN);
        byte sunAtAbove = chunkCluster.GetLight(lightPos, Chunk.LIGHT_TYPE.SUN);
        byte sun = sunAtPlant > sunAtAbove ? sunAtPlant : sunAtAbove;
        byte blockAtPlant = chunkCluster.GetLight(plantPos, Chunk.LIGHT_TYPE.BLOCK);
        byte blockAtAbove = chunkCluster.GetLight(lightPos, Chunk.LIGHT_TYPE.BLOCK);
        byte block = blockAtPlant > blockAtAbove ? blockAtPlant : blockAtAbove;

        bool rawSun = sun >= requiredNaturalSunlight;
        bool rawBlock = block >= required;
        bool rawOpenSky = IsOpenSkyAboveLoadedChunk(world, plantPos) ||
                          IsOpenSkyAboveLoadedChunk(world, lightPos);

        // Vanilla-style crop SUN enclosure signal, evaluated at the crop instead of the player.
        float vanillaAmountEnclosed = 1f - ((float)sun / 15f);
        if (vanillaAmountEnclosed < 0f)
            vanillaAmountEnclosed = 0f;
        if (vanillaAmountEnclosed > 1f)
            vanillaAmountEnclosed = 1f;

        bool cropSunSufficient = rawSun;
        bool vanillaEnclosed = !cropSunSufficient;

        NaturalLightExposure exposure;
        bool geometryBlockerOverride = false;
        bool geometryLightOpenOverride = false;
        if (HasNearbyStateDependentCover(world, plantPos))
        {
            // The patched chunk SUN value is the primary light result, but state-dependent
            // covers can be wrong in both directions while the actual SUN map catches up:
            //   1) stale lit value through an opened horizontal door that now blocks passage;
            //   2) stale dark value through an opened horizontal door that now clears passage.
            // Always run the exact-face cover search when such a cover is nearby, then use it
            // only to correct a contradiction between stored SUN and modeled cover geometry.
            exposure = SearchCoverAwareSunPath(world, plantPos);
            geometryBlockerOverride = cropSunSufficient && !exposure.Open && exposure.CoverBlockersOnPath > 0;
            // The exact-face search can prove that a cover blocks an otherwise sufficient SUN
            // value, but it must not fabricate usable daylight when the stored SUN is too dim.
            // The two-door diagnostic showed cropSun=3 reaching crops through low-opacity
            // doors; that is a real, dim residual light value and must fail the minimum
            // natural-sunlight threshold instead of being promoted by geometry alone.
            geometryLightOpenOverride = false;
        }
        else if (cropSunSufficient)
        {
            exposure = new NaturalLightExposure
            {
                Open = true,
                UsedSearch = false,
                Mode = "cropSunFromPatchedLightProcessor",
                BlockingPos = Vector3i.zero,
                BlockingBlock = string.Empty,
                ExitPos = plantPos,
                NodesVisited = 0,
                TransparentCandidates = 0,
                AirTransparentCandidates = 0,
                NonAirTransparentCandidates = 0,
                CoverBlockersOnPath = 0,
                TrustedCropSunWithoutCover = true
            };
        }
        else
        {
            exposure = new NaturalLightExposure
            {
                Open = false,
                UsedSearch = false,
                Mode = "cropSunBelowRequired",
                BlockingPos = Vector3i.zero,
                BlockingBlock = string.Empty,
                ExitPos = Vector3i.zero,
                NodesVisited = 0,
                TransparentCandidates = 0,
                AirTransparentCandidates = 0,
                NonAirTransparentCandidates = 0,
                CoverBlockersOnPath = 0,
                TrustedCropSunWithoutCover = false
            };
        }

        bool hasOpenSky = cropSunSufficient && !geometryBlockerOverride;
        bool hasNaturalSun = cropSunSufficient && hasOpenSky && !isNight;
        byte effectiveSun = sun;
        bool naturalLightStrongEnough = effectiveSun >= requiredNaturalSunlight;

        LightResult result = new LightResult
        {
            RequiredLightLevel = required,
            RequiredNaturalSunlight = requiredNaturalSunlight,
            NaturalLightStrongEnough = naturalLightStrongEnough,
            SunLight = effectiveSun,
            BlockLight = block,
            CropSunLight = effectiveSun,
            VanillaAmountEnclosed = geometryLightOpenOverride ? 1f - ((float)effectiveSun / 15f) : vanillaAmountEnclosed,
            VanillaEnclosed = geometryLightOpenOverride ? false : vanillaEnclosed,
            GeometryBlockerOverride = geometryBlockerOverride,
            RawHasSunLight = rawSun || geometryLightOpenOverride,
            RawHasOpenSky = rawOpenSky,
            NaturalLightPathOpen = hasOpenSky,
            NaturalLightMode = exposure.Mode,
            BlockingPos = exposure.BlockingPos,
            BlockingBlock = exposure.BlockingBlock,
            ExitPos = exposure.ExitPos,
            ExposureNodesVisited = exposure.NodesVisited,
            ExposureTransparentCandidates = exposure.TransparentCandidates,
            ExposureAirTransparentCandidates = exposure.AirTransparentCandidates,
            ExposureNonAirTransparentCandidates = exposure.NonAirTransparentCandidates,
            CoverBlockersOnPath = exposure.CoverBlockersOnPath,
            TrustedCropSunWithoutCover = exposure.TrustedCropSunWithoutCover,
            CacheHit = false,
            CacheAgeSeconds = 0,
            CacheLifetimeSeconds = GetLightCacheLifetimeSeconds(plantPos, required),
            HasSunLight = hasNaturalSun,
            HasBlockLight = rawBlock,
            HasOpenSky = hasOpenSky
        };

        AdvancedFarmingPerfSnapshotService.RecordLightExposureEvaluation(exposure.UsedSearch, exposure.NodesVisited, exposure.TransparentCandidates, exposure.Open);

        EvictLightCacheIfNeeded(now);

        s_lightCache[cacheKey] = new CachedLightResult
        {
            Result = result,
            Pos = plantPos,
            WorldSeconds = now,
            PolicyVersion = policyVersion,
            IsNight = isNight
        };

        if (AdvancedFarmingStutterTraceService.Active)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - stutterTraceStartTicks;
            if (AdvancedFarmingStutterTraceService.ShouldRecordSlow(elapsedTicks))
                AdvancedFarmingStutterTraceService.RecordSlowOperation("lightEvaluate", plantPos, elapsedTicks, "cacheHit=false usedSearch=" + exposure.UsedSearch + " nodes=" + exposure.NodesVisited + " transparent=" + exposure.TransparentCandidates + " open=" + exposure.Open + " cropSun=" + effectiveSun + " block=" + block + " mode=" + exposure.Mode);
        }

        return result;
    }

    private static void EvictLightCacheIfNeeded(int now)
    {
        if (s_lightCache.Count < MaxCacheEntries)
            return;

        s_cacheRemovalScratch.Clear();
        foreach (KeyValuePair<long, CachedLightResult> pair in s_lightCache)
        {
            if (now - pair.Value.WorldSeconds > GetLightCacheLifetimeSeconds(pair.Value.Pos, pair.Value.Result.RequiredLightLevel))
                s_cacheRemovalScratch.Add(pair.Key);
        }

        for (int i = 0; i < s_cacheRemovalScratch.Count; i++)
            s_lightCache.Remove(s_cacheRemovalScratch[i]);
        s_cacheRemovalScratch.Clear();

        if (s_lightCache.Count >= MaxCacheEntries)
            s_lightCache.Clear();
    }

    private static void EvictCoverPresenceCacheIfNeeded(int now)
    {
        if (s_coverPresenceCache.Count < MaxCacheEntries)
            return;

        s_cacheRemovalScratch.Clear();
        foreach (KeyValuePair<long, CachedCoverPresence> pair in s_coverPresenceCache)
        {
            int age = now - pair.Value.WorldSeconds;
            if (age < 0 || age > GetCoverPresenceCacheLifetimeSeconds(pair.Value.Pos))
                s_cacheRemovalScratch.Add(pair.Key);
        }

        for (int i = 0; i < s_cacheRemovalScratch.Count; i++)
            s_coverPresenceCache.Remove(s_cacheRemovalScratch[i]);
        s_cacheRemovalScratch.Clear();

        if (s_coverPresenceCache.Count >= MaxCacheEntries)
            s_coverPresenceCache.Clear();
    }


    private static bool IsOpenSkyAboveLoadedChunk(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return false;

        IChunk chunk = world.GetChunkSync(World.toChunkXZ(pos.x), World.toChunkXZ(pos.z));
        return chunk != null
            && pos.y >= chunk.GetHeight(World.toBlockXZ(pos.x), World.toBlockXZ(pos.z));
    }

    private static int GetLightCacheLifetimeSeconds(Vector3i pos, int required)
    {
        return CacheSeconds + PositiveModulo(HashPosition(pos) ^ required, CacheJitterSeconds);
    }

    private static int GetCoverPresenceCacheLifetimeSeconds(Vector3i pos)
    {
        return CoverPresenceCacheSeconds + PositiveModulo(HashPosition(pos) ^ 0x51f15e, CoverPresenceCacheJitterSeconds);
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

    public static bool HasUnblockedNaturalLightColumn(WorldBase world, Vector3i plantPos)
    {
        // Compatibility helper: any cover-aware path from the crop to an open sky column.
        NaturalLightExposure exposure = SearchCoverAwareSunPath(world, plantPos);
        return exposure.Open;
    }

    public static string BuildDiagnostic(WorldBase world, Vector3i plantPos, int lightLevelStay, int lightLevelGrow, bool isMushroom, bool advancedFarming)
    {
        LightResult result = Evaluate(world, plantPos, lightLevelStay, lightLevelGrow, isMushroom, advancedFarming);
        return "rawSun=" + result.RawHasSunLight
            + " rawOpenSky=" + result.RawHasOpenSky
            + " sun=" + result.SunLight
            + " cropSun=" + result.CropSunLight
            + " requiredLight=" + result.RequiredLightLevel
            + " requiredNaturalSunlight=" + result.RequiredNaturalSunlight
            + " naturalLightStrongEnough=" + result.NaturalLightStrongEnough
            + " vanillaAmountEnclosed=" + result.VanillaAmountEnclosed.ToString("0.###")
            + " vanillaEnclosed=" + result.VanillaEnclosed
            + " geometryBlockerOverride=" + result.GeometryBlockerOverride
            + " coverBlockersOnPath=" + result.CoverBlockersOnPath
            + " trustedCropSunWithoutCover=" + result.TrustedCropSunWithoutCover
            + " lightCacheHit=" + result.CacheHit
            + " lightCacheAge=" + result.CacheAgeSeconds
            + " lightCacheLifetime=" + result.CacheLifetimeSeconds
            + " blockLight=" + result.BlockLight
            + " hasSun=" + result.HasSunLight
            + " hasOpenSky=" + result.HasOpenSky
            + " hasBlockLight=" + result.HasBlockLight
            + " naturalPath=" + result.NaturalLightPathOpen
            + " mode=" + result.NaturalLightMode
            + " exitPos=" + result.ExitPos
            + " nodes=" + result.ExposureNodesVisited
            + " transparentCandidates=" + result.ExposureTransparentCandidates
            + " transparentAir=" + result.ExposureAirTransparentCandidates
            + " transparentNonAir=" + result.ExposureNonAirTransparentCandidates
            + " blockerPos=" + result.BlockingPos
            + " blockerBlock=" + result.BlockingBlock;
    }

    /// <summary>
    /// Raw, unfiltered dump of every signal this service could plausibly use to judge a single
    /// block instance's light/passability state - written so the next decision about doors is
    /// made from real data instead of another guess. Every optional API call is individually
    /// try/caught so one unsupported call never hides the rest of the dump. Run this against
    /// the exact failing door in both the "should pass light" and "should block light" states
    /// (same block, same position, toggled) and compare which field actually flips.
    /// </summary>

    public static string BuildDoorStateDiagnostic(WorldBase world, Vector3i pos, string label)
    {
        if (world == null)
            return "[AdvancedFarming DoorRotationTestDoor] label=" + label + " pos=" + pos + " noWorld=true";

        BlockValue blockValue = world.GetBlock(pos);
        Block block = blockValue.Block;
        string blockName = block != null ? block.GetBlockName() : "<null>";
        string className = block != null ? block.GetType().Name : "<null>";

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        byte stateRotation = stateValue.rotation;
        bool openState = block != null && IsUprightDoorOpenState(world, pos, blockValue, block);
        BlockFaceFlag closedMask = block != null ? GetClosedDoorBlockingFaceMaskFromRotation(stateRotation) : BlockFaceFlag.None;
        BlockFaceFlag openedMask = block != null ? GetOpenedDoorBlockingFaceMaskFromRotation(stateRotation) : BlockFaceFlag.None;
        BlockFaceFlag activeMask = block != null ? GetActiveUprightDoorBlockingFaceMask(world, pos, blockValue, block) : BlockFaceFlag.None;

        System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
        sb.Append("[AdvancedFarming DoorRotationTestDoor] label=").Append(label);
        sb.Append(" pos=").Append(pos);
        sb.Append(" block=").Append(blockName);
        sb.Append(" class=").Append(className);
        sb.Append(" type=").Append(blockValue.type);
        sb.Append(" rotation=").Append(blockValue.rotation);
        sb.Append(" meta=").Append(blockValue.meta);
        sb.Append(" meta2=").Append(blockValue.meta2);
        sb.Append(" damage=").Append(blockValue.damage);
        sb.Append(" child=").Append(blockValue.ischild);
        sb.Append(" parentOffset=").Append(blockValue.ischild ? blockValue.parent.ToString() : "0, 0, 0");
        sb.Append(" statePos=").Append(statePos);
        sb.Append(" stateBlock=").Append(stateBlock != null ? stateBlock.GetBlockName() : "<null>");
        sb.Append(" stateRotation=").Append(stateRotation);
        sb.Append(" stateMeta=").Append(stateValue.meta);
        sb.Append(" stateMeta2=").Append(stateValue.meta2);
        sb.Append(" stateDamage=").Append(stateValue.damage);
        sb.Append(" doorOpenState=").Append(openState);
        sb.Append(" isAllRotationFullPlane=").Append(IsAllRotationFullPlaneDoorRotation(stateRotation));
        if (IsAllRotationFullPlaneDoorRotation(stateRotation) && block != null)
            sb.Append(" doorPlacementPlane=").Append(InferAllRotationDoorPlacementPlane(world, pos, blockValue, block, BlockFaceFlag.All));
        sb.Append(" openedHorizontalSealsTopBottom=").Append(IsOpenedHorizontalDoorStateSealingRotation(stateRotation));
        sb.Append(" closedDoorBlockFaces=").Append(closedMask);
        sb.Append(" openedDoorBlockFaces=").Append(openedMask);
        sb.Append(" activeDoorBlockFaces=").Append(activeMask);
        AppendSafe(sb, "classifyFaceTop", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.Top).ToString());
        AppendSafe(sb, "classifyFaceBottom", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.Bottom).ToString());
        AppendSafe(sb, "classifyFaceNorth", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.North).ToString());
        AppendSafe(sb, "classifyFaceSouth", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.South).ToString());
        AppendSafe(sb, "classifyFaceEast", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.East).ToString());
        AppendSafe(sb, "classifyFaceWest", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.West).ToString());
        return sb.ToString();
    }
    public static string BuildBlockForensics(WorldBase world, Vector3i pos)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
        sb.Append("[AdvancedFarming BlockForensics] pos=").Append(pos);

        if (world == null)
        {
            sb.Append(" world=null");
            return sb.ToString();
        }

        BlockValue blockValue = world.GetBlock(pos);
        sb.Append(" isAir=").Append(blockValue.isair);
        sb.Append(" isWater=").Append(blockValue.isWater);
        sb.Append(" isChild=").Append(blockValue.ischild);
        sb.Append(" rotation=").Append(blockValue.rotation);
        sb.Append(" meta=").Append(blockValue.meta);
        sb.Append(" meta2=").Append(blockValue.meta2);
        sb.Append(" damage=").Append(blockValue.damage);
        sb.Append(" typeId=").Append(blockValue.type);

        ChunkCluster chunkCluster = world.ChunkCache;
        if (chunkCluster != null)
        {
            AppendSafe(sb, "sun", () => chunkCluster.GetLight(pos, Chunk.LIGHT_TYPE.SUN).ToString());
            AppendSafe(sb, "blockLight", () => chunkCluster.GetLight(pos, Chunk.LIGHT_TYPE.BLOCK).ToString());
        }
        AppendSafe(sb, "openSkyAbove", () => IsOpenSkyAboveLoadedChunk(world, pos).ToString());

        Block block = blockValue.Block;
        if (block == null)
        {
            sb.Append(" block=<null>");
            return sb.ToString();
        }

        sb.Append(" name=").Append(block.GetBlockName());
        sb.Append(" class=").Append(block.GetType().Name);
        sb.Append(" blockID=").Append(block.blockID);
        sb.Append(" lightOpacity=").Append(block.lightOpacity);
        AppendSafe(sb, "shapeOpacityPolicy", () => AdvancedFarmingShapeOpacityPolicy.BuildDebugSummary(block));
        sb.Append(" materialIsPlant=").Append(block.blockMaterial != null && block.blockMaterial.IsPlant);
        sb.Append(" resolvedRule=").Append(ResolveNaturalLightRule(block));
        AppendDamageStateForensics(sb, world, pos, blockValue, block);
        AppendSafe(sb, "classifyFaceTop", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.Top).ToString());
        AppendSafe(sb, "classifyFaceBottom", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.Bottom).ToString());
        AppendSafe(sb, "classifyFaceNorth", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.North).ToString());
        AppendSafe(sb, "classifyFaceSouth", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.South).ToString());
        AppendSafe(sb, "classifyFaceEast", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.East).ToString());
        AppendSafe(sb, "classifyFaceWest", () => ClassifyFace(world, pos, blockValue, BlockFaceFlag.West).ToString());

        AppendSafe(sb, "isSeeThrough", () => block.IsSeeThrough(world, pos, blockValue).ToString());
        AppendSafe(sb, "isMovementBlockedAll", () => block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.All).ToString());
        AppendSafe(sb, "isMovementBlockedTop", () => block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.Top).ToString());
        AppendSafe(sb, "isMovementBlockedBottom", () => block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.Bottom).ToString());
        AppendSafe(sb, "isMovementBlockedNorth", () => block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.North).ToString());
        AppendSafe(sb, "isMovementBlockedSouth", () => block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.South).ToString());
        AppendSafe(sb, "isMovementBlockedEast", () => block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.East).ToString());
        AppendSafe(sb, "isMovementBlockedWest", () => block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.West).ToString());

        if (block is BlockPoweredDoor)
            AppendSafe(sb, "poweredDoorIsOpen", () => BlockPoweredDoor.IsDoorOpen(blockValue.meta).ToString());
        if (block is BlockTrapDoor)
        {
            sb.Append(" trapDoorRawMeta=").Append(blockValue.meta);
            AppendSafe(sb, "trapDoorExactBlockedFaces", () => GetMovementBlockedFaceMask(world, pos, blockValue, block).ToString());
        }

        sb.Append(" properties=[");
        if (block.Properties != null && block.Properties.Values != null)
        {
            bool first = true;
            foreach (KeyValuePair<string, string> kv in block.Properties.Values)
            {
                if (!first)
                    sb.Append("; ");
                sb.Append(kv.Key).Append('=').Append(kv.Value);
                first = false;
            }
        }
        sb.Append(']');

        return sb.ToString();
    }

    private static void AppendSafe(System.Text.StringBuilder sb, string label, Func<string> getValue)
    {
        sb.Append(' ').Append(label).Append('=');
        try
        {
            sb.Append(getValue());
        }
        catch (Exception ex)
        {
            sb.Append("<threw:").Append(ex.GetType().Name).Append('>');
        }
    }

    public static LightColumnDiagnostic BuildColumnDiagnostic(WorldBase world, Vector3i rootPos)
    {
        LightColumnDiagnostic diagnostic = new LightColumnDiagnostic
        {
            RootPos = rootPos,
            Open = true,
            FirstBlockerPos = Vector3i.zero,
            FirstBlockerBlock = string.Empty,
            AirCount = 0,
            NonAirTransparentCount = 0,
            NonAirOpaqueCount = 0,
            Entries = new List<LightColumnEntry>(32)
        };

        if (world == null)
            return diagnostic;

        int startY = rootPos.y + 1;
        int endY = byte.MaxValue - 1;
        Vector3i prevPos = rootPos;
        BlockValue prevValue = world.GetBlock(rootPos);
        for (int y = startY; y <= endY; y++)
        {
            Vector3i checkPos = new Vector3i(rootPos.x, y, rootPos.z);
            BlockValue blockValue = world.GetBlock(checkPos);
            CellVerdict verdict = ClassifyEdge(world, prevPos, prevValue, checkPos, blockValue, 0, 1, 0);
            bool transparent = verdict == CellVerdict.Pass;
            bool isAir = blockValue.isair;
            prevPos = checkPos;
            prevValue = blockValue;
            if (isAir)
            {
                diagnostic.AirCount++;
                continue;
            }

            LightColumnEntry entry = BuildColumnEntry(world, checkPos, blockValue, transparent);
            diagnostic.Entries.Add(entry);

            if (transparent)
            {
                diagnostic.NonAirTransparentCount++;
                continue;
            }

            diagnostic.NonAirOpaqueCount++;
            if (diagnostic.Open)
            {
                diagnostic.Open = false;
                diagnostic.FirstBlockerPos = checkPos;
                diagnostic.FirstBlockerBlock = entry.BlockName;
            }
        }

        return diagnostic;
    }

    public static LightSearchDiagnostic BuildSearchDiagnostic(WorldBase world, Vector3i plantPos, int maxOpenSamples, int maxBlockedSamples)
    {
        if (maxOpenSamples < 0)
            maxOpenSamples = 0;
        if (maxBlockedSamples < 0)
            maxBlockedSamples = 0;

        LightSearchDiagnostic diagnostic = new LightSearchDiagnostic
        {
            PlantPos = plantPos,
            StartPos = plantPos + Vector3i.up,
            StartTransparent = false,
            StartBlock = string.Empty,
            SearchHorizontalRadius = ExposureSearchHorizontalRadius,
            SearchUp = ExposureSearchUp,
            ColumnsChecked = 0,
            OpenColumns = 0,
            BlockedColumns = 0,
            OpenColumnSamples = new List<LightSearchColumnEntry>(Math.Max(1, maxOpenSamples)),
            BlockedColumnSamples = new List<LightSearchColumnEntry>(Math.Max(1, maxBlockedSamples))
        };

        if (world == null)
            return diagnostic;

        BlockValue startValue = world.GetBlock(diagnostic.StartPos);
        BlockValue plantValue = world.GetBlock(plantPos);
        diagnostic.StartTransparent = ClassifyEdge(world, plantPos, plantValue, diagnostic.StartPos, startValue, 0, 1, 0) == CellVerdict.Pass;
        diagnostic.StartBlock = GetBlockName(startValue);

        for (int dist = 0; dist <= ExposureSearchHorizontalRadius; dist++)
        {
            for (int dx = -dist; dx <= dist; dx++)
            {
                for (int dz = -dist; dz <= dist; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != dist)
                        continue;

                    Vector3i root = new Vector3i(plantPos.x + dx, plantPos.y, plantPos.z + dz);
                    LightColumnDiagnostic column = BuildColumnDiagnostic(world, root);
                    diagnostic.ColumnsChecked++;

                    LightSearchColumnEntry entry = new LightSearchColumnEntry
                    {
                        RootPos = root,
                        Dx = dx,
                        Dz = dz,
                        ChebyshevDistance = dist,
                        Open = column.Open,
                        FirstBlockerPos = column.FirstBlockerPos,
                        FirstBlockerBlock = column.FirstBlockerBlock,
                        AirCount = column.AirCount,
                        NonAirTransparentCount = column.NonAirTransparentCount,
                        NonAirOpaqueCount = column.NonAirOpaqueCount
                    };

                    if (column.Open)
                    {
                        diagnostic.OpenColumns++;
                        if (diagnostic.OpenColumnSamples.Count < maxOpenSamples)
                            diagnostic.OpenColumnSamples.Add(entry);
                    }
                    else
                    {
                        diagnostic.BlockedColumns++;
                        if (diagnostic.BlockedColumnSamples.Count < maxBlockedSamples)
                            diagnostic.BlockedColumnSamples.Add(entry);
                    }
                }
            }
        }

        return diagnostic;
    }

    /// <summary>
    /// Cover-aware flood from the crop. Succeeds when any reached cell has an open sky column
    /// under this service's own cover-aware rules. Fails only when at least one cover block
    /// (door/hatch/curtain family) blocked the flood. If the flood exhausts without meeting any
    /// cover block, the result is Open=true with TrustedCropSunWithoutCover=true: vanilla cropSun
    /// already proved light reached the crop and no cover exists to explain a leak, so this
    /// service must not override the engine (handoff section 11 requirement).
    /// </summary>
    private static bool HasNearbyStateDependentCover(WorldBase world, Vector3i plantPos)
    {
        if (world == null)
            return false;

        int now = RebirthUtilities.TotalGameSecondsPassed();
        int policyVersion = AdvancedFarmingRuntimePolicy.PolicyVersion;
        long key = MakePositionCacheKey(plantPos);
        int cachedAge = 0;
        if (s_coverPresenceCache.TryGetValue(key, out CachedCoverPresence cached)
            && (cachedAge = now - cached.WorldSeconds) >= 0
            && cachedAge <= GetCoverPresenceCacheLifetimeSeconds(cached.Pos)
            && cached.PolicyVersion == policyVersion)
        {
            return cached.HasCover;
        }

        bool hasCover = ScanNearbyStateDependentCover(world, plantPos);
        EvictCoverPresenceCacheIfNeeded(now);

        s_coverPresenceCache[key] = new CachedCoverPresence
        {
            Pos = plantPos,
            WorldSeconds = now,
            PolicyVersion = policyVersion,
            HasCover = hasCover
        };
        return hasCover;
    }

    private static bool ScanNearbyStateDependentCover(WorldBase world, Vector3i plantPos)
    {
        int minX = plantPos.x - ExposureSearchHorizontalRadius;
        int maxX = plantPos.x + ExposureSearchHorizontalRadius;
        int minZ = plantPos.z - ExposureSearchHorizontalRadius;
        int maxZ = plantPos.z + ExposureSearchHorizontalRadius;
        int minY = Math.Max(0, plantPos.y - ExposureSearchDown);
        int maxY = Math.Min(byte.MaxValue - 1, plantPos.y + ExposureSearchUp);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    BlockValue value = world.GetBlock(new Vector3i(x, y, z));
                    if (value.isair || value.isWater)
                        continue;

                    Block block = value.Block;
                    if (block != null && ResolveNaturalLightRule(block) == NaturalLightRule.DoorStateDependent)
                        return true;
                }
            }
        }

        return false;
    }

    private static NaturalLightExposure SearchCoverAwareSunPath(WorldBase world, Vector3i plantPos)
    {
        NaturalLightExposure result = new NaturalLightExposure
        {
            Open = false,
            UsedSearch = true,
            Mode = "noOpenColumnReached",
            BlockingPos = Vector3i.zero,
            BlockingBlock = string.Empty,
            ExitPos = Vector3i.zero,
            NodesVisited = 0,
            TransparentCandidates = 0,
            AirTransparentCandidates = 0,
            NonAirTransparentCandidates = 0,
            CoverBlockersOnPath = 0,
            TrustedCropSunWithoutCover = false
        };

        if (world == null)
            return result;

        int minX = plantPos.x - ExposureSearchHorizontalRadius;
        int maxX = plantPos.x + ExposureSearchHorizontalRadius;
        int minZ = plantPos.z - ExposureSearchHorizontalRadius;
        int maxZ = plantPos.z + ExposureSearchHorizontalRadius;
        int minY = Math.Max(0, plantPos.y - ExposureSearchDown);
        int maxY = Math.Min(byte.MaxValue - 1, plantPos.y + ExposureSearchUp);

        int widthX = maxX - minX + 1;
        int widthZ = maxZ - minZ + 1;
        int height = maxY - minY + 1;
        int maxNodes = widthX * widthZ * height;
        if (maxNodes <= 0 || maxNodes > MaxExposureNodes)
            return result;

        int stamp = NextVisitStamp();
        int head = 0;
        int tail = 0;

        // Seed the flood directly - the crop's own cell and the cell above are always valid
        // starts (a plant cell always classifies as Pass against anything).
        EnqueueCell(plantPos, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail);
        EnqueueCell(plantPos + Vector3i.up, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail);

        while (head < tail)
        {
            Vector3i pos = s_queue[head++];
            result.NodesVisited++;

            if (IsOpenSkyColumnMemoized(world, pos, minX, minY, minZ, widthX, widthZ, height, stamp, ref result))
            {
                result.Open = true;
                result.Mode = (pos == plantPos || pos == plantPos + Vector3i.up) ? "openColumnAtCrop" : "openColumnAtReachedCell";
                result.ExitPos = pos;
                return result;
            }

            BlockValue posValue = GetBlockMemoized(world, pos, minX, minY, minZ, widthX, widthZ, height, stamp);
            TryEnqueueNeighbor(world, pos, posValue, pos.x + 1, pos.y, pos.z, 1, 0, 0, minX, maxX, minY, maxY, minZ, maxZ, widthX, widthZ, height, stamp, ref tail, ref result);
            TryEnqueueNeighbor(world, pos, posValue, pos.x - 1, pos.y, pos.z, -1, 0, 0, minX, maxX, minY, maxY, minZ, maxZ, widthX, widthZ, height, stamp, ref tail, ref result);
            TryEnqueueNeighbor(world, pos, posValue, pos.x, pos.y, pos.z + 1, 0, 0, 1, minX, maxX, minY, maxY, minZ, maxZ, widthX, widthZ, height, stamp, ref tail, ref result);
            TryEnqueueNeighbor(world, pos, posValue, pos.x, pos.y, pos.z - 1, 0, 0, -1, minX, maxX, minY, maxY, minZ, maxZ, widthX, widthZ, height, stamp, ref tail, ref result);
            TryEnqueueNeighbor(world, pos, posValue, pos.x, pos.y + 1, pos.z, 0, 1, 0, minX, maxX, minY, maxY, minZ, maxZ, widthX, widthZ, height, stamp, ref tail, ref result);
            TryEnqueueNeighbor(world, pos, posValue, pos.x, pos.y - 1, pos.z, 0, -1, 0, minX, maxX, minY, maxY, minZ, maxZ, widthX, widthZ, height, stamp, ref tail, ref result);
        }

        // Flood exhausted without reaching an open column.
        if (result.CoverBlockersOnPath > 0)
        {
            // A cover block genuinely stands between the crop and every open column found.
            // This is the only situation allowed to override sufficient cropSun.
            result.Open = false;
            result.Mode = "coverBlocked";
            return result;
        }

        // No cover block was involved anywhere. Vanilla cropSun says light reached the crop
        // and there is nothing this service models that could be leaking it, so trust vanilla.
        // This also self-heals unknown/unmodeled geometry instead of hard-failing crops.
        result.Open = true;
        result.TrustedCropSunWithoutCover = true;
        result.Mode = "noCoverOnAnyPath";
        result.BlockingPos = Vector3i.zero;
        result.BlockingBlock = string.Empty;
        return result;
    }

    private static bool IsOpenSkyColumnMemoized(
        WorldBase world,
        Vector3i pos,
        int minX,
        int minY,
        int minZ,
        int widthX,
        int widthZ,
        int height,
        int stamp,
        ref NaturalLightExposure result)
    {
        // A reachable cell counts as a natural-light source only if this service's own
        // cover-aware column from that cell reaches sky. Vanilla open-sky and vanilla SUN
        // values are deliberately not used here: they leak through doors, curtains, and
        // other non-occluding utility blocks, which is the entire problem being solved.
        // Memoized per exact cell: cells at different heights in the same column can have
        // different verdicts (a cell above a roof has an open column while a cell below it
        // does not), so column-level sharing would be incorrect.
        int key = MakeLocalIndex(pos, minX, minY, minZ, widthX, widthZ, height);
        if (key < 0 || key >= s_sourceMemoStamp.Length)
            return false;

        if (s_sourceMemoStamp[key] == stamp)
            return s_sourceMemoOpen[key];

        int surfaceY = world.GetHeight(pos.x, pos.z);
        if (pos.y >= surfaceY)
        {
            s_sourceMemoStamp[key] = stamp;
            s_sourceMemoOpen[key] = true;
            return true;
        }

        bool open = true;
        int endY = surfaceY;
        if (endY > byte.MaxValue - 1)
            endY = byte.MaxValue - 1;
        Vector3i belowPos = pos;
        BlockValue belowValue = GetBlockMemoized(world, pos, minX, minY, minZ, widthX, widthZ, height, stamp);
        for (int y = pos.y + 1; y <= endY; y++)
        {
            Vector3i checkPos = new Vector3i(pos.x, y, pos.z);
            BlockValue blockValue = GetBlockMemoized(world, checkPos, minX, minY, minZ, widthX, widthZ, height, stamp);
            CellVerdict verdict = ClassifyEdge(world, belowPos, belowValue, checkPos, blockValue, 0, 1, 0);
            belowPos = checkPos;
            belowValue = blockValue;
            if (verdict == CellVerdict.Pass)
            {
                result.TransparentCandidates++;
                if (blockValue.isair)
                    result.AirTransparentCandidates++;
                else
                    result.NonAirTransparentCandidates++;
                continue;
            }

            open = false;
            if (verdict == CellVerdict.CoverBlocked)
            {
                result.CoverBlockersOnPath++;
                if (string.IsNullOrEmpty(result.BlockingBlock))
                {
                    result.BlockingPos = checkPos;
                    result.BlockingBlock = GetBlockName(blockValue);
                }
            }
            break;
        }

        s_sourceMemoStamp[key] = stamp;
        s_sourceMemoOpen[key] = open;
        return open;
    }

    private static void TryEnqueueNeighbor(
        WorldBase world,
        Vector3i fromPos,
        BlockValue fromValue,
        int x,
        int y,
        int z,
        int dx,
        int dy,
        int dz,
        int minX,
        int maxX,
        int minY,
        int maxY,
        int minZ,
        int maxZ,
        int widthX,
        int widthZ,
        int height,
        int stamp,
        ref int tail,
        ref NaturalLightExposure result)
    {
        if (x < minX || x > maxX || y < minY || y > maxY || z < minZ || z > maxZ)
            return;

        Vector3i toPos = new Vector3i(x, y, z);
        int key = MakeLocalIndex(toPos, minX, minY, minZ, widthX, widthZ, height);
        if (key < 0 || key >= s_visitedStamp.Length || s_visitedStamp[key] == stamp)
            return;

        BlockValue toValue = GetBlockMemoized(world, toPos, minX, minY, minZ, widthX, widthZ, height, stamp);
        CellVerdict verdict = ClassifyEdge(world, fromPos, fromValue, toPos, toValue, dx, dy, dz);
        if (verdict != CellVerdict.Pass)
        {
            // Only COVER blocks are meaningful blockers for this model. Structural blocks
            // (walls, plots, terrain) are not recorded: if structure truly sealed the crop,
            // vanilla cropSun would already be below the required level and no search would run.
            if (verdict == CellVerdict.CoverBlocked)
            {
                result.CoverBlockersOnPath++;
                if (string.IsNullOrEmpty(result.BlockingBlock))
                {
                    result.BlockingPos = toPos;
                    result.BlockingBlock = GetBlockName(toValue);
                }
            }
            return;
        }

        result.TransparentCandidates++;
        if (toValue.isair)
            result.AirTransparentCandidates++;
        else
            result.NonAirTransparentCandidates++;

        if (tail >= s_queue.Length)
            return;

        // A cell is visited only after this incoming edge has been admitted. A blocked
        // first approach must not suppress a later valid direction into the same cell.
        s_visitedStamp[key] = stamp;
        s_queue[tail++] = toPos;
    }

    /// <summary>Pure queue-mechanics enqueue for the two flood seed cells (the crop's own cell
    /// and the cell above it), which are not reached via a directional step from another cell.</summary>
    private static void EnqueueCell(
        Vector3i pos,
        int minX,
        int minY,
        int minZ,
        int widthX,
        int widthZ,
        int height,
        int stamp,
        ref int tail)
    {
        int key = MakeLocalIndex(pos, minX, minY, minZ, widthX, widthZ, height);
        if (key < 0 || key >= s_visitedStamp.Length || s_visitedStamp[key] == stamp)
            return;

        s_visitedStamp[key] = stamp;
        if (tail >= s_queue.Length)
            return;

        s_queue[tail++] = pos;
    }

    private static int NextVisitStamp()
    {
        s_stamp++;
        if (s_stamp != int.MaxValue)
            return s_stamp;

        s_stamp = 1;
        for (int i = 0; i < s_visitedStamp.Length; i++)
            s_visitedStamp[i] = 0;
        for (int i = 0; i < s_sourceMemoStamp.Length; i++)
            s_sourceMemoStamp[i] = 0;
        for (int i = 0; i < s_blockSnapshotStamp.Length; i++)
            s_blockSnapshotStamp[i] = 0;
        return s_stamp;
    }

    private static int MakeLocalIndex(Vector3i pos, int minX, int minY, int minZ, int widthX, int widthZ, int height)
    {
        int dx = pos.x - minX;
        int dy = pos.y - minY;
        int dz = pos.z - minZ;
        if (dx < 0 || dx >= widthX || dy < 0 || dy >= height || dz < 0 || dz >= widthZ)
            return -1;

        return ((dy * widthZ) + dz) * widthX + dx;
    }

    private static BlockValue GetBlockMemoized(WorldBase world, Vector3i pos, int minX, int minY, int minZ, int widthX, int widthZ, int height, int stamp)
    {
        int key = MakeLocalIndex(pos, minX, minY, minZ, widthX, widthZ, height);
        if (key < 0 || key >= s_blockSnapshotStamp.Length)
            return world.GetBlock(pos);

        if (s_blockSnapshotStamp[key] == stamp)
            return s_blockSnapshot[key];

        BlockValue value = world.GetBlock(pos);
        s_blockSnapshotStamp[key] = stamp;
        s_blockSnapshot[key] = value;
        return value;
    }

    private static long MakePositionCacheKey(Vector3i pos)
    {
        unchecked
        {
            return ((long)(pos.x & 0x1FFFFF) << 43)
                ^ ((long)(pos.y & 0x3FF) << 33)
                ^ (long)(pos.z & 0x1FFFFF);
        }
    }

    /// <summary>
    /// Maps a single axis-aligned step (dx,dy,dz - exactly one component nonzero, value +-1) to
    /// the exact face pair for the two cells sharing that boundary: the face of the FROM cell
    /// facing the step direction, and the face of the TO cell facing back. Top/Bottom are
    /// unambiguous. The North/South/East/West assignment below is the standard compass
    /// convention (+X=East, -X=West, +Z=North, -Z=South) and is flagged for verification in the
    /// class-level doc comment - if verification shows it backwards, swap the two lines marked
    /// HORIZONTAL below; nothing else in the file depends on which physical direction each name
    /// refers to, only that a step and its reverse use opposite faces consistently.
    /// </summary>
    private static void StepToFaces(int dx, int dy, int dz, out BlockFaceFlag fromFace, out BlockFaceFlag toFace)
    {
        if (dy > 0) { fromFace = BlockFaceFlag.Top; toFace = BlockFaceFlag.Bottom; return; }
        if (dy < 0) { fromFace = BlockFaceFlag.Bottom; toFace = BlockFaceFlag.Top; return; }

        if (dx > 0) { fromFace = BlockFaceFlag.East; toFace = BlockFaceFlag.West; return; }   // HORIZONTAL +X
        if (dx < 0) { fromFace = BlockFaceFlag.West; toFace = BlockFaceFlag.East; return; }   // HORIZONTAL -X
        if (dz > 0) { fromFace = BlockFaceFlag.North; toFace = BlockFaceFlag.South; return; } // HORIZONTAL +Z
        if (dz < 0) { fromFace = BlockFaceFlag.South; toFace = BlockFaceFlag.North; return; } // HORIZONTAL -Z

        fromFace = BlockFaceFlag.All;
        toFace = BlockFaceFlag.All;
    }

    /// <summary>
    /// Classifies whether natural light can cross the specific shared boundary between two
    /// adjacent cells, given the exact step direction. Each side is asked about only the one
    /// face that boundary corresponds to on that side - never a combined "any face" query -
    /// so a partial shape (door, hatch, plate, frame) that seals one opening in one rotation but
    /// not another is judged correctly regardless of which wall/ceiling/floor slot it occupies.
    ///
    /// Pass: light crosses. StructureBlocked: at least one side is uniformly solid on every face
    /// (IsMovementBlocked(All)==true) - equivalent to a full block, so vanilla's own scalar
    /// opacity would already reflect it; this can never override sufficient cropSun.
    /// CoverBlocked: at least one side blocks specifically this face while NOT being uniformly
    /// solid - a rotation/state-dependent partial seal; this is the only class allowed to
    /// override sufficient cropSun.
    /// </summary>
    private static CellVerdict ClassifyEdge(WorldBase world, Vector3i fromPos, BlockValue fromValue, Vector3i toPos, BlockValue toValue, int dx, int dy, int dz)
    {
        StepToFaces(dx, dy, dz, out BlockFaceFlag fromFace, out BlockFaceFlag toFace);
        CellVerdict fromVerdict = ClassifyFace(world, fromPos, fromValue, fromFace);
        if (fromVerdict == CellVerdict.StructureBlocked)
            return CellVerdict.StructureBlocked;

        CellVerdict toVerdict = ClassifyFace(world, toPos, toValue, toFace);
        if (toVerdict == CellVerdict.StructureBlocked)
            return CellVerdict.StructureBlocked;

        if (fromVerdict == CellVerdict.CoverBlocked || toVerdict == CellVerdict.CoverBlocked)
            return CellVerdict.CoverBlocked;

        return CellVerdict.Pass;
    }

    private static CellVerdict ClassifyFace(WorldBase world, Vector3i pos, BlockValue blockValue, BlockFaceFlag face)
    {
        if (blockValue.isair || blockValue.isWater)
            return CellVerdict.Pass;

        Block block = blockValue.Block;
        if (block == null)
            return CellVerdict.Pass;

        if (block.blockMaterial != null && block.blockMaterial.IsPlant)
            return CellVerdict.Pass;

        NaturalLightRule rule = ResolveNaturalLightRule(block);
        if (rule == NaturalLightRule.AlwaysPass)
            return CellVerdict.Pass;

        // Composite/model-entity cover blocks can keep the same block id/class while swapping
        // visible prefab damage states (for example a damaged door mesh turning into a hole or
        // frame). Movement and see-through APIs still report the base closed door, so cover
        // damage states must be checked before AlwaysBlock/DoorStateDependent cover rules.
        if ((rule == NaturalLightRule.AlwaysBlock || rule == NaturalLightRule.DoorStateDependent)
            && IsLightPassingCoverDamageState(world, pos, blockValue, block))
            return CellVerdict.Pass;

        if (rule == NaturalLightRule.AlwaysBlock)
            return CellVerdict.CoverBlocked;

        if (rule == NaturalLightRule.DoorStateDependent)
        {
            // Composite cover doors are represented as one block cell even when the
            // panel changes plane. Do not treat open as an unconditional pass: a
            // rotated/open door or hatch can still physically cover the light opening.
            // Instead classify the exact crossed face against the current cover plane.
            if (IsUprightDoorPlaneBlockingFace(world, pos, blockValue, block, face))
                return CellVerdict.CoverBlocked;

            return CellVerdict.Pass;
        }

        // Generated shape blocks can have runtime Advanced Farming category opacity that differs
        // from vanilla shapes.xml. Exact cube remains a full structural blocker; partial/open
        // categories attenuate patched SUN propagation but should not become crop-side geometry
        // blockers when the resulting SUN level is sufficient.
        int generatedShapeOpacity;
        bool generatedShapeThermalPassable;
        string generatedShapeCategory;
        if (AdvancedFarmingShapeOpacityPolicy.TryGetGeneratedShapePolicy(block, out generatedShapeOpacity, out generatedShapeThermalPassable, out generatedShapeCategory))
            return generatedShapeOpacity >= 15 ? CellVerdict.StructureBlocked : CellVerdict.Pass;

        // Generic/remnant/frame/rod/partial shapes are not door-state covers. Do not use
        // movement blocking by itself for these blocks, because frames, rods, bars, rails,
        // and partial meshes can block movement while still letting natural light through.
        // They only become structural if the normal light/visibility data says they do.
        if (block.lightOpacity > 0)
            return CellVerdict.StructureBlocked;

        try
        {
            if (!block.IsSeeThrough(world, pos, blockValue))
                return CellVerdict.StructureBlocked;
        }
        catch
        {
            return CellVerdict.StructureBlocked;
        }

        return CellVerdict.Pass;
    }

    /// <summary>
    /// Returns true when the current cover block instance is in a model damage state that
    /// should pass natural light. This is deliberately generic and state-based, not tied to
    /// oldWoodDoor or any block name. Multi-block children resolve to their parent block value
    /// for damage because child BlockValues keep damage=0 while the parent stores the actual
    /// MeshDamage/health state.
    ///
    /// Default policy for cover blocks with MeshDamage: state 0 blocks, while state 1+
    /// passes light. This matches vault-style doors where the first damage state opens a
    /// visible light path. Specific families whose first damage state is still solid/no-hole
    /// should opt out in XML by setting AdvFarmingLightPassDamageStateIndex to a higher value
    /// on their inherited parent block, not by hard-coded C# names.
    /// </summary>
    private static bool IsLightPassingCoverDamageState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (block == null || !(block.shape is BlockShapeModelEntity))
            return false;

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);
        if (stateBlock == null || !(stateBlock.shape is BlockShapeModelEntity stateShape))
            return false;

        int passIndex = 1;
        string passIndexText = GetBlockProperty(stateBlock, "AdvFarmingLightPassDamageStateIndex");
        if (!string.IsNullOrEmpty(passIndexText))
        {
            int parsed;
            if (int.TryParse(passIndexText, out parsed))
                passIndex = parsed;
        }

        int index;
        try
        {
            index = stateShape.GetDamageStateIndex(stateValue);
        }
        catch
        {
            return false;
        }

        if (index >= passIndex)
            return true;

        // Also preserve the vanilla pathing terminal-state rule for model entities whose
        // final damage state is explicitly non-obstructing.
        int remaining = stateBlock.MaxDamagePlusDowngrades - stateValue.damage;
        if (remaining != 1)
            return false;

        try
        {
            return !stateShape.IsObstructionForDamageState(stateValue);
        }
        catch
        {
            return false;
        }
    }

    private static void ResolveDamageStateBlock(
        WorldBase world,
        Vector3i pos,
        BlockValue blockValue,
        Block block,
        out Vector3i statePos,
        out BlockValue stateValue,
        out Block stateBlock)
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

    private static void AppendDamageStateForensics(System.Text.StringBuilder sb, WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (block == null)
            return;

        sb.Append(" parentOffset=").Append(blockValue.ischild ? blockValue.parent.ToString() : "0, 0, 0");

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        sb.Append(" damageStatePos=").Append(statePos);
        sb.Append(" damageStateIsParent=").Append(!(statePos.x == pos.x && statePos.y == pos.y && statePos.z == pos.z));
        sb.Append(" damageStateDamage=").Append(stateValue.damage);
        sb.Append(" damageStateTypeId=").Append(stateValue.type);

        if (stateBlock == null)
            return;

        sb.Append(" damageStateBlock=").Append(stateBlock.GetBlockName());
        sb.Append(" damageStateRotation=").Append(stateValue.rotation);
        sb.Append(" damageStateMeta=").Append(stateValue.meta);
        sb.Append(" damageStateMeta2=").Append(stateValue.meta2);
        AppendSafe(sb, "doorOpenState", () => IsUprightDoorOpenState(world, pos, blockValue, block).ToString());
        AppendSafe(sb, "doorPlacementPlane", () => InferAllRotationDoorPlacementPlane(world, pos, blockValue, block, BlockFaceFlag.All).ToString());
        AppendSafe(sb, "closedDoorBlockFaces", () => GetClosedUprightDoorBlockingFaceMask(world, pos, blockValue, block).ToString());
        AppendSafe(sb, "openedDoorBlockFaces", () => GetOpenedDoorBlockingFaceMaskFromRotation(GetDoorStateRotation(world, pos, blockValue, block)).ToString());
        AppendSafe(sb, "horizontalDoorOpenStateSeals", () => IsOpenedHorizontalDoorStateSealingRotation(GetDoorStateRotation(world, pos, blockValue, block)).ToString());
        AppendSafe(sb, "activeDoorBlockFaces", () => GetActiveUprightDoorBlockingFaceMask(world, pos, blockValue, block).ToString());
        sb.Append(" maxDamage=").Append(stateBlock.MaxDamage);
        sb.Append(" maxDamagePlusDowngrades=").Append(stateBlock.MaxDamagePlusDowngrades);
        sb.Append(" damageStateRemainingTotal=").Append(stateBlock.MaxDamagePlusDowngrades - stateValue.damage);

        if (stateBlock.shape is BlockShapeModelEntity shape)
        {
            AppendSafe(sb, "damageStateIndex", () => shape.GetDamageStateIndex(stateValue).ToString());
            AppendSafe(sb, "damageStateCount", () => shape.GetDamageStateCount().ToString());
            AppendSafe(sb, "damageStateShapeObstructs", () => shape.IsObstructionForDamageState(stateValue).ToString());
            AppendSafe(sb, "damageStateLightPass", () => IsLightPassingCoverDamageState(world, pos, blockValue, block).ToString());
            AppendSafe(sb, "damageStateLightPassIndex", () => GetDamageStateLightPassIndex(stateBlock).ToString());
        }
    }

    private static bool IsUprightDoorPlaneBlockingFace(WorldBase world, Vector3i pos, BlockValue blockValue, Block block, BlockFaceFlag face)
    {
        if (block == null)
            return false;

        bool? wallHatchPass = TryGetVerifiedWallHatchPassState(world, pos, blockValue, block);
        if (wallHatchPass.HasValue)
            return !wallHatchPass.Value;

        if (IsCurtainOrBlindLike(block))
        {
            // Half curtains use the same placement/rotation rule as full curtains, but
            // the actual SUN attenuation is partial in AdvancedFarmingDynamicLightOpacityService.
            // The boolean cover search cannot represent partial opacity, so half curtains must
            // never be promoted to a full geometry blocker here.
            if (IsHalfCurtainLike(block))
                return false;

            return !IsVerifiedCurtainBlindPassState(world, pos, blockValue, block);
        }

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

    private static BlockFaceFlag GetActiveAllRotationDoorBlockingFaceMask(WorldBase world, Vector3i pos, BlockValue blockValue, Block block, byte rotation, BlockFaceFlag testedFace)
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


    private static DoorPlacementPlane InferAllRotationDoorPlacementPlane(WorldBase world, Vector3i pos, BlockValue blockValue, Block block, BlockFaceFlag testedFace)
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

    private static int GetAxisOpenScore(WorldBase world, Vector3i pos, int dx, int dy, int dz)
    {
        return GetPlacementProbeOpenScore(world, new Vector3i(pos.x + dx, pos.y + dy, pos.z + dz))
            + GetPlacementProbeOpenScore(world, new Vector3i(pos.x - dx, pos.y - dy, pos.z - dz));
    }

    private static int GetPlacementProbeOpenScore(WorldBase world, Vector3i pos)
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

        int generatedShapeOpacity;
        bool generatedShapeThermalPassable;
        string generatedShapeCategory;
        if (AdvancedFarmingShapeOpacityPolicy.TryGetGeneratedShapePolicy(neighbor, out generatedShapeOpacity, out generatedShapeThermalPassable, out generatedShapeCategory))
            return generatedShapeOpacity >= 15 ? 0 : 1;

        return neighbor.lightOpacity >= 15 ? 0 : 1;
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

    private static BlockFaceFlag GetWallDoorApertureFaceMask(WorldBase world, Vector3i pos, BlockFaceFlag testedFace, byte rotation)
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


    private static bool IsUprightDoorOpenState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (block == null)
            return false;

        bool? blockedAll = SafeIsMovementBlocked(world, pos, blockValue, block, BlockFaceFlag.All);
        if (blockedAll.HasValue)
            return !blockedAll.Value;

        // Fallback only. Composite/model-entity doors can report IsSeeThrough=true for
        // reasons unrelated to the configured damage-state light-pass exception, so this
        // must never override a valid movement-blocked state.
        try
        {
            return block.IsSeeThrough(world, pos, blockValue);
        }
        catch
        {
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

    private static BlockFaceFlag GetActiveUprightDoorBlockingFaceMask(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (block == null)
            return BlockFaceFlag.None;
        if (block is BlockTrapDoor)
            return GetMovementBlockedFaceMask(world, pos, blockValue, block);
        if (block is BlockPoweredDoor)
            return BlockPoweredDoor.IsDoorOpen(blockValue.meta) ? BlockFaceFlag.None : BlockFaceFlag.All;

        byte rotation = GetDoorStateRotation(world, pos, blockValue, block);
        if (IsAllRotationFullPlaneDoorRotation(rotation))
            return GetActiveAllRotationDoorBlockingFaceMask(world, pos, blockValue, block, rotation, BlockFaceFlag.All);

        return IsUprightDoorOpenState(world, pos, blockValue, block)
            ? GetOpenedDoorBlockingFaceMaskFromRotation(rotation)
            : GetClosedDoorBlockingFaceMaskFromRotation(rotation);
    }

    private static BlockFaceFlag GetClosedUprightDoorBlockingFaceMask(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (block == null)
            return BlockFaceFlag.All;

        Vector3i statePos;
        BlockValue stateValue;
        Block stateBlock;
        ResolveDamageStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        return GetClosedDoorBlockingFaceMaskFromRotation(stateValue.rotation);
    }


    private static byte GetDoorStateRotation(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
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

    private static BlockFaceFlag GetMovementBlockedFaceMask(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        BlockFaceFlag mask = BlockFaceFlag.None;
        AddMovementBlockedFace(ref mask, world, pos, blockValue, block, BlockFaceFlag.Top);
        AddMovementBlockedFace(ref mask, world, pos, blockValue, block, BlockFaceFlag.Bottom);
        AddMovementBlockedFace(ref mask, world, pos, blockValue, block, BlockFaceFlag.North);
        AddMovementBlockedFace(ref mask, world, pos, blockValue, block, BlockFaceFlag.South);
        AddMovementBlockedFace(ref mask, world, pos, blockValue, block, BlockFaceFlag.East);
        AddMovementBlockedFace(ref mask, world, pos, blockValue, block, BlockFaceFlag.West);
        return mask;
    }

    private static void AddMovementBlockedFace(ref BlockFaceFlag mask, WorldBase world, Vector3i pos, BlockValue blockValue, Block block, BlockFaceFlag face)
    {
        bool? blocked = SafeIsMovementBlocked(world, pos, blockValue, block, face);
        if (blocked.HasValue && blocked.Value)
            mask |= face;
    }

    /// <summary>
    /// Queries whether one specific face of a block instance blocks movement. Powered doors
    /// retain their vanilla open-state helper because the class reports a whole-door state.
    /// Trap doors/hatches deliberately use the generic exact-face engine query so their
    /// inverted default/open placement semantics are not collapsed to a meta shortcut.
    /// Returns null only if no signal could be obtained at all, so callers can fall back
    /// themselves rather than receive a silently wrong true/false.
    /// </summary>
    private static bool? SafeIsMovementBlocked(WorldBase world, Vector3i pos, BlockValue blockValue, Block block, BlockFaceFlag face)
    {
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


    private static NaturalLightRule ResolveNaturalLightRule(Block block)
    {
        if (block == null)
            return NaturalLightRule.Compute;

        int blockId = block.blockID;
        if (s_ruleByBlockType.TryGetValue(blockId, out NaturalLightRule cached))
            return cached;

        NaturalLightRule rule = ComputeNaturalLightRule(block);
        s_ruleByBlockType[blockId] = rule;
        return rule;
    }

    private static NaturalLightRule ComputeNaturalLightRule(Block block)
    {
        string explicitRule = GetBlockProperty(block, "AdvFarmingNaturalLight");
        if (!string.IsNullOrEmpty(explicitRule))
        {
            if (explicitRule.Equals("pass", StringComparison.OrdinalIgnoreCase) || explicitRule.Equals("allow", StringComparison.OrdinalIgnoreCase))
                return NaturalLightRule.AlwaysPass;
            if (explicitRule.Equals("block", StringComparison.OrdinalIgnoreCase) || explicitRule.Equals("deny", StringComparison.OrdinalIgnoreCase))
                return NaturalLightRule.AlwaysBlock;
            if (explicitRule.Equals("door", StringComparison.OrdinalIgnoreCase) || explicitRule.Equals("state", StringComparison.OrdinalIgnoreCase))
                return NaturalLightRule.DoorStateDependent;
        }

        string name = block.GetBlockName();
        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");

        // XML AdvFarmingNaturalLight remains the primary data-driven override for specific
        // block families such as downgraded rods/rails/frames. The C# classifier should not
        // hard-code individual block names here; non-door remnants fall through to the generic
        // lightOpacity/IsSeeThrough path.

        // Curtains/drapes/blinds are stateful visual covers for natural light. They default to pass,
        // except for verified intact/first-damage-state wall rotations that visibly cover the
        // aperture. Damage state 1+ always passes. Keep them state-dependent so both the
        // crop-side classifier and patched LightProcessor can apply the same per-instance rule.
        if (IsCurtainOrBlindLike(block))
            return NaturalLightRule.DoorStateDependent;

        // Shutters are physical hinged covers, closer to doors than curtains/blinds.
        // They block light by default, but verified placement-specific rotation/open
        // states can leave the opening clear and must pass light/thermal exposure.
        if (IsShutterLike(block))
            return NaturalLightRule.DoorStateDependent;

        // Centered non-glass cover plates/plugs are shape-state natural-light covers.
        // They are not transparent window glass even when the block name contains "window"
        // (for example windowPlug01CTRPlate). Classify them before the generic window/glass
        // rule so their placement-specific rotation matrix can override static opacity 255.
        if (IsCenteredNonGlassNaturalLightCover(block))
            return NaturalLightRule.DoorStateDependent;

        // Intact windows/glass are transparent covers for natural crop light. Do this before
        // the generic lightOpacity/IsSeeThrough fallback because many model/shape windows can
        // report non-zero lightOpacity or movement blocking even though the visible block is
        // transparent. Curtains/blinds/shutters remain blockers above, and XML
        // AdvFarmingNaturalLight remains the authoritative override for exceptions.
        if (IsTransparentWindowLikeName(name) || IsTransparentWindowLikeName(tags)
            || IsTransparentWindowLikeName(blockTag) || IsTransparentWindowLikeName(place))
            return NaturalLightRule.AlwaysPass;

        if (block is BlockPoweredDoor || block is BlockTrapDoor)
            return NaturalLightRule.DoorStateDependent;

        // Composite 3.0 doors expose their current physical state through the per-face
        // movement queries. Do not treat every door-like block as AlwaysBlock: open doors
        // or rotated/non-obstructing doors should let natural light through.
        if (IsDoorLikeName(name) || IsDoorLikeName(tags) || IsDoorLikeName(blockTag) || IsDoorLikeName(place))
            return NaturalLightRule.DoorStateDependent;

        return NaturalLightRule.Compute;
    }


    private static bool IsShutterLike(Block block)
    {
        if (block == null)
            return false;

        string name = block.GetBlockName();
        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        return ContainsIgnoreCase(name, "shutter") || ContainsIgnoreCase(tags, "shutter")
            || ContainsIgnoreCase(blockTag, "shutter") || ContainsIgnoreCase(place, "shutter");
    }

    private static bool IsCurtainOrBlindLike(Block block)
    {
        if (block == null)
            return false;

        string name = block.GetBlockName();
        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        return ContainsIgnoreCase(name, "curtain") || ContainsIgnoreCase(tags, "curtain")
            || ContainsIgnoreCase(blockTag, "curtain") || ContainsIgnoreCase(place, "curtain")
            || ContainsIgnoreCase(name, "drape") || ContainsIgnoreCase(tags, "drape")
            || ContainsIgnoreCase(blockTag, "drape") || ContainsIgnoreCase(place, "drape")
            || ContainsIgnoreCase(name, "blind") || ContainsIgnoreCase(tags, "blind")
            || ContainsIgnoreCase(blockTag, "blind") || ContainsIgnoreCase(place, "blind");
    }

    private static bool IsHalfCurtainLike(Block block)
    {
        if (block == null)
            return false;

        string name = block.GetBlockName();
        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        return (ContainsIgnoreCase(name, "curtain") || ContainsIgnoreCase(tags, "curtain")
            || ContainsIgnoreCase(blockTag, "curtain") || ContainsIgnoreCase(place, "curtain")
            || ContainsIgnoreCase(name, "drape") || ContainsIgnoreCase(tags, "drape")
            || ContainsIgnoreCase(blockTag, "drape") || ContainsIgnoreCase(place, "drape"))
            && (ContainsIgnoreCase(name, "half") || ContainsIgnoreCase(tags, "half")
                || ContainsIgnoreCase(blockTag, "half") || ContainsIgnoreCase(place, "half"));
    }

    private static bool IsCenteredNonGlassNaturalLightCover(Block block)
    {
        if (block == null)
            return false;

        // Already-covered families keep their own placement/state rules.
        if (IsCurtainOrBlindLike(block) || IsShutterLike(block))
            return false;

        string name = block.GetBlockName();
        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        string model = GetBlockProperty(block, "Model");
        string shape = GetBlockProperty(block, "Shape");

        if (IsDoorLikeName(name) || IsDoorLikeName(tags) || IsDoorLikeName(blockTag) || IsDoorLikeName(place))
            return false;

        // Glass-centered plates are intentionally excluded. They remain transparent window/glass.
        if (ContainsIgnoreCase(name, "glass") || ContainsIgnoreCase(tags, "glass")
            || ContainsIgnoreCase(blockTag, "glass") || ContainsIgnoreCase(place, "glass")
            || ContainsIgnoreCase(model, "glass") || ContainsIgnoreCase(shape, "glass"))
            return false;

        bool centered = ContainsIgnoreCase(name, "ctrplate")
            || ContainsIgnoreCase(name, "centered")
            || ContainsIgnoreCase(tags, "centered")
            || ContainsIgnoreCase(blockTag, "centered")
            || ContainsIgnoreCase(place, "centered")
            || ContainsIgnoreCase(model, "centered")
            || ContainsIgnoreCase(shape, "centered");

        return centered && block.lightOpacity > 0;
    }


    private static bool? TryGetVerifiedWallHatchPassState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
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
        if (block == null)
            return false;

        if (block is BlockTrapDoor)
            return true;

        string name = block.GetBlockName();
        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        return ContainsIgnoreCase(name, "hatch") || ContainsIgnoreCase(tags, "hatch")
            || ContainsIgnoreCase(blockTag, "hatch") || ContainsIgnoreCase(place, "hatch")
            || ContainsIgnoreCase(name, "trapdoor") || ContainsIgnoreCase(tags, "trapdoor")
            || ContainsIgnoreCase(blockTag, "trapdoor") || ContainsIgnoreCase(place, "trapdoor");
    }

    private static bool IsVerifiedCurtainBlindPassState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
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

        if (IsCurtainBlindDamagedBeyondFirstState(world, statePos, stateValue, stateBlock))
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

    private static bool IsCurtainBlindDamagedBeyondFirstState(WorldBase world, Vector3i statePos, BlockValue stateValue, Block stateBlock)
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

    private static bool IsVerifiedCenteredNonGlassPassState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
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

    private static bool IsVerifiedShutterPassState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
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

    private static ShutterWallApertureAxis GetVerifiedWallShutterApertureAxis(WorldBase world, Vector3i pos)
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

    private static bool IsDoorLikeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return ContainsIgnoreCase(value, "door")
            || ContainsIgnoreCase(value, "hatch")
            || ContainsIgnoreCase(value, "trapdoor");
    }

    private static bool IsTransparentWindowLikeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return ContainsIgnoreCase(value, "window")
            || ContainsIgnoreCase(value, "glass");
    }


    private static bool ContainsIgnoreCase(string value, string pattern)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetBlockProperty(Block block, string key)
    {
        if (block == null || block.Properties == null || block.Properties.Values == null)
            return null;

        if (!block.Properties.Values.ContainsKey(key))
            return null;

        return block.Properties.Values[key];
    }

    private static LightColumnEntry BuildColumnEntry(WorldBase world, Vector3i pos, BlockValue blockValue, bool transparent)
    {
        Block block = blockValue.Block;
        bool isSeeThrough = false;
        bool isMovementBlocked = false;
        bool materialPlant = false;
        string blockName = "<null>";
        string className = "<null>";
        string rule = "Compute";
        string doorDecision = string.Empty;
        int effectiveLightOpacity = block != null ? block.lightOpacity : 0;
        string shapeOpacityPolicy = "none";

        if (block != null)
        {
            blockName = block.GetBlockName();
            className = block.GetType().Name;
            materialPlant = block.blockMaterial != null && block.blockMaterial.IsPlant;
            try
            {
                isSeeThrough = block.IsSeeThrough(world, pos, blockValue);
            }
            catch
            {
                isSeeThrough = false;
            }
            try
            {
                isMovementBlocked = block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.All);
            }
            catch
            {
                isMovementBlocked = true;
            }
            int generatedShapeOpacity;
            bool generatedShapeThermalPassable;
            string generatedShapeCategory;
            if (AdvancedFarmingShapeOpacityPolicy.TryGetGeneratedShapePolicy(block, out generatedShapeOpacity, out generatedShapeThermalPassable, out generatedShapeCategory))
            {
                effectiveLightOpacity = generatedShapeOpacity;
                shapeOpacityPolicy = generatedShapeCategory + ":lightOpacity=" + generatedShapeOpacity + ":thermalPass=" + generatedShapeThermalPassable;
            }

            NaturalLightRule naturalRule = ResolveNaturalLightRule(block);
            rule = naturalRule.ToString();
            if (naturalRule == NaturalLightRule.DoorStateDependent)
                doorDecision = ClassifyFace(world, pos, blockValue, BlockFaceFlag.All) == CellVerdict.Pass ? "allFacesOpen" : "faceBlockedByCurrentCoverPlane";
        }

        return new LightColumnEntry
        {
            Pos = pos,
            BlockName = blockName,
            ClassName = className,
            IsAir = blockValue.isair,
            IsWater = blockValue.isWater,
            IsChild = blockValue.ischild,
            LightOpacity = block != null ? block.lightOpacity : 0,
            EffectiveLightOpacity = effectiveLightOpacity,
            ShapeOpacityPolicy = shapeOpacityPolicy,
            IsSeeThrough = isSeeThrough,
            IsMovementBlocked = isMovementBlocked,
            MaterialIsPlant = materialPlant,
            Rule = rule,
            DoorDecision = doorDecision,
            Transparent = transparent
        };
    }

    private static string GetBlockName(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        return block != null ? block.GetBlockName() : "<null>";
    }

    private static long MakeCacheKey(Vector3i pos, int required)
    {
        unchecked
        {
            long key = 1469598103934665603L;
            key = (key ^ pos.x) * 1099511628211L;
            key = (key ^ pos.y) * 1099511628211L;
            key = (key ^ pos.z) * 1099511628211L;
            key = (key ^ required) * 1099511628211L;
            return key;
        }
    }
}
