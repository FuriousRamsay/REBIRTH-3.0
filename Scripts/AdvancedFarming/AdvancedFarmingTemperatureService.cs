using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Centralized Advanced Farming crop temperature evaluation.
///
/// Temperature enclosure is intentionally separate from crop light. Normal intact windows/glass
/// can pass natural light while still sealing the structure thermally; broken/damaged windows
/// can pass both light and temperature. Centered window/glass covers and window-shaped centered
/// aperture covers are placement-specific exceptions: if their centered rotation leaves the
/// aperture open enough for SUN to pass, they also leave the thermal enclosure open.
/// Generated shape blocks use a runtime opacity category table: exact cube seals temperature;
/// partial/open generated shape categories attenuate SUN but do not thermally seal the opening.
/// </summary>
public static class AdvancedFarmingTemperatureService
{
    private const float CampfireHeatBonus = 30f;
    private const float WoodStoveHeatBonus = 50f;
    private const float GreenhouseHeatBonus = 20f;
    private const int ThermalSearchHorizontalRadius = 8;
    private const int ThermalSearchUp = 10;
    private const int ThermalSearchDown = 4;
    private const int ThermalSearchMaxNodes = 4096;
    // v125 correctness rollback: long per-position thermal TTL/jitter could leave neighboring crops
    // displaying different enclosed/open temperature states after dynamic cover changes if any
    // invalidation path was missed. Thermal searches are cheap in the profiler now, so prefer
    // coherence over the previous 120+60s cache lifetime.
    private const int ThermalExposureCacheSeconds = 15;
    private const int ThermalExposureCacheJitterSeconds = 0;
    private const int MaxThermalExposureCacheEntries = 4096;
    private const int ThermalSearchMemoCells = (ThermalSearchHorizontalRadius * 2 + 1) * (ThermalSearchUp + ThermalSearchDown + 1) * (ThermalSearchHorizontalRadius * 2 + 1);

    private enum ThermalRule : byte
    {
        AlwaysPass = 0,
        AlwaysBlock = 1,
        CurtainOrBlind = 2,
        Shutter = 3,
        WallHatch = 4,
        GeneratedShapePass = 5,
        GeneratedShapeBlock = 6,
        CenteredNonGlass = 7,
        CenteredWindowGlass = 8,
        WindowGlass = 9
    }

    private struct CachedThermalExposure
    {
        public Vector3i Pos;
        public int WorldSeconds;
        public int PolicyVersion;
        public bool Complete;
        public bool OpenToExterior;
        public string Mode;
        public Vector3i ExitPos;
        public Vector3i BlockingPos;
        public string BlockingBlock;
        public int NodesVisited;
    }

    private static readonly Dictionary<long, CachedThermalExposure> s_thermalExposureCache = new Dictionary<long, CachedThermalExposure>(MaxThermalExposureCacheEntries);
    private static readonly Dictionary<int, ThermalRule> s_thermalRuleByBlockType = new Dictionary<int, ThermalRule>(1024);
    private static readonly List<long> s_thermalCacheRemovalScratch = new List<long>(64);
    private static readonly Vector3i[] s_thermalSearchQueue = new Vector3i[ThermalSearchMaxNodes];
    private static readonly int[] s_thermalSearchStamp = new int[ThermalSearchMemoCells];
    private static readonly byte[] s_thermalPassMemo = new byte[ThermalSearchMemoCells];
    private static readonly int[] s_thermalPassMemoStamp = new int[ThermalSearchMemoCells];
    private static int s_thermalSearchStampId;

    public struct TemperatureResult
    {
        public float BaseTemperature;
        public string BaseTemperatureSource;
        public float WeatherPenalty;
        public float HeatBonus;
        public float EnclosedBiomeAdjustment;
        public float EnclosedBiomeHotAdjustment;
        public float EnclosedBiomeColdAdjustment;
        public float EnclosedBiomeColdBlend;
        public string EnclosedBiomeAdjustmentMode;
        public float Temperature;
        public bool TemperatureOverride;
        public bool IsNight;
        public bool Enclosed;
        public bool ThermalEvaluationComplete;
        public bool HasCampfireHeat;
        public bool HasWoodStoveHeat;
        public bool HasGreenhouseHeat;
        public string HeatSource;
        public string BiomeName;
        public bool ThermalOpenToExterior;
        public string ThermalExposureMode;
        public Vector3i ThermalExitPos;
        public Vector3i ThermalBlockingPos;
        public string ThermalBlockingBlock;
        public int ThermalNodesVisited;
    }

    public static TemperatureResult Evaluate(
        WorldBase world,
        Vector3i plantPos,
        bool hasNaturalLight,
        bool advancedFarming)
    {
        bool thermalOpen = hasNaturalLight;
        string thermalMode = hasNaturalLight ? "legacyNaturalLightOpen" : "legacyNaturalLightClosed";
        Vector3i thermalExitPos = hasNaturalLight ? plantPos : Vector3i.zero;
        Vector3i thermalBlockingPos = Vector3i.zero;
        string thermalBlockingBlock = string.Empty;
        int thermalNodesVisited = 0;
        bool thermalEvaluationComplete = true;

        if (advancedFarming && world != null)
        {
            CachedThermalExposure thermal = EvaluateThermalExposure(world, plantPos);
            thermalEvaluationComplete = thermal.Complete;
            if (thermal.Complete)
            {
                thermalOpen = thermal.OpenToExterior;
                thermalMode = thermal.Mode;
                thermalExitPos = thermal.ExitPos;
            }
            else
            {
                // Chunk.OnLoad can run while adjacent chunks in the search radius are not yet
                // available through World.GetChunkSync. Do not invent a sealed/open enclosure
                // from incomplete geometry. Retain the caller's current natural-light fallback
                // and retry the uncached thermal search on a later evaluation.
                thermalMode = thermal.Mode + (thermalOpen ? "FallbackOpen" : "FallbackClosed");
            }

            thermalBlockingPos = thermal.BlockingPos;
            thermalBlockingBlock = thermal.BlockingBlock;
            thermalNodesVisited = thermal.NodesVisited;
        }

        TemperatureResult result = new TemperatureResult
        {
            BaseTemperature = 0f,
            BaseTemperatureSource = "none",
            WeatherPenalty = 0f,
            HeatBonus = 0f,
            EnclosedBiomeAdjustment = 0f,
            EnclosedBiomeHotAdjustment = 0f,
            EnclosedBiomeColdAdjustment = 0f,
            EnclosedBiomeColdBlend = 0f,
            EnclosedBiomeAdjustmentMode = "none",
            Temperature = 0f,
            TemperatureOverride = false,
            IsNight = IsNight(world),
            Enclosed = !thermalOpen,
            ThermalEvaluationComplete = thermalEvaluationComplete,
            HasCampfireHeat = false,
            HasWoodStoveHeat = false,
            HasGreenhouseHeat = false,
            HeatSource = "none",
            BiomeName = GetBiomeName(world, plantPos),
            ThermalOpenToExterior = thermalOpen,
            ThermalExposureMode = thermalMode,
            ThermalExitPos = thermalExitPos,
            ThermalBlockingPos = thermalBlockingPos,
            ThermalBlockingBlock = thermalBlockingBlock,
            ThermalNodesVisited = thermalNodesVisited
        };

        bool temperatureOverridden = AdvancedFarmingHeatQueryService.TryGetTemperatureOverride(out float temp);
        result.TemperatureOverride = temperatureOverridden;

        if (temperatureOverridden)
        {
            result.BaseTemperatureSource = "override";
        }
        else
        {
            temp = GetPositionOutdoorTemperature(world, plantPos, out string temperatureSource);
            result.BaseTemperatureSource = temperatureSource;
            // The per-biome authoritative value already contains the server's weather
            // temperature effects and is sent through NetPackageWeather. Do not subtract the
            // old client-local cloud/rain approximation a second time.
            result.WeatherPenalty = 0f;
        }

        result.BaseTemperature = temp;

        if (advancedFarming && world != null)
        {
            // Protected Growing heater is intentionally enclosure-dependent. It is useful only when
            // the crop is actually inside a thermally enclosed structure, which keeps the Knowledge
            // benefit tied to greenhouse construction rather than becoming an outdoor radius buff.
            if (result.Enclosed && RebirthUtilities.CheckForHeat(world, plantPos, "rebirthGreenhouseHeatStove", 8, 4, 1))
            {
                result.HasGreenhouseHeat = true;
                result.HeatSource = "greenhouse";
                result.HeatBonus = GreenhouseHeatBonus;
                temp += GreenhouseHeatBonus;
            }
            else if (RebirthUtilities.CheckForHeat(world, plantPos, "campfire", 6, 3, 1))
            {
                result.HasCampfireHeat = true;
                result.HeatSource = "campfire";
                result.HeatBonus = CampfireHeatBonus;
                temp += CampfireHeatBonus;
            }
            else if (RebirthUtilities.CheckForHeat(world, plantPos, "cntWoodBurningStove", 10, 5, 1))
            {
                result.HasWoodStoveHeat = true;
                result.HeatSource = "woodstove";
                result.HeatBonus = WoodStoveHeatBonus;
                temp += WoodStoveHeatBonus;
            }
        }

        if (advancedFarming && result.Enclosed)
        {
            result.EnclosedBiomeAdjustment = GetEnclosedBiomeAdjustment(world, result.BiomeName, out float hotAdjust, out float coldAdjust, out float coldBlend, out string adjustmentMode);
            result.EnclosedBiomeHotAdjustment = hotAdjust;
            result.EnclosedBiomeColdAdjustment = coldAdjust;
            result.EnclosedBiomeColdBlend = coldBlend;
            result.EnclosedBiomeAdjustmentMode = adjustmentMode;
            temp += result.EnclosedBiomeAdjustment;
        }

        result.Temperature = temp;
        return result;
    }

    public static void ClearCache()
    {
        s_thermalExposureCache.Clear();
        AdvancedFarmingHoverTextService.ClearConditionCache();
    }

    public static void NotifyWorldBlockChanged(Vector3i blockPos)
    {
        ClearCacheAround(blockPos, ThermalSearchHorizontalRadius + 2, ThermalSearchUp + ThermalSearchDown + 2);
        AdvancedFarmingHoverTextService.ClearConditionCache();
    }

    public static void ClearCacheAround(Vector3i center, int horizontalRadius, int verticalRadius)
    {
        AdvancedFarmingHoverTextService.ClearConditionCache();

        if (s_thermalExposureCache.Count == 0)
            return;

        if (horizontalRadius < 0)
            horizontalRadius = 0;
        if (verticalRadius < 0)
            verticalRadius = 0;

        s_thermalCacheRemovalScratch.Clear();
        foreach (KeyValuePair<long, CachedThermalExposure> pair in s_thermalExposureCache)
        {
            Vector3i pos = pair.Value.Pos;
            if (Math.Abs(pos.x - center.x) <= horizontalRadius
                && Math.Abs(pos.z - center.z) <= horizontalRadius
                && Math.Abs(pos.y - center.y) <= verticalRadius)
            {
                s_thermalCacheRemovalScratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < s_thermalCacheRemovalScratch.Count; i++)
            s_thermalExposureCache.Remove(s_thermalCacheRemovalScratch[i]);
        s_thermalCacheRemovalScratch.Clear();
    }

    public static string BuildThermalExposureDiagnostic(WorldBase world, Vector3i plantPos)
    {
        CachedThermalExposure exposure = EvaluateThermalExposure(world, plantPos);
        return "thermalComplete=" + exposure.Complete
            + " thermalOpenToExterior=" + exposure.OpenToExterior
            + " thermalMode=" + exposure.Mode
            + " thermalExitPos=" + exposure.ExitPos
            + " thermalBlockingPos=" + exposure.BlockingPos
            + " thermalBlockingBlock=" + exposure.BlockingBlock
            + " thermalNodesVisited=" + exposure.NodesVisited;
    }

    private static CachedThermalExposure EvaluateThermalExposure(WorldBase world, Vector3i plantPos)
    {
        CachedThermalExposure fallback = new CachedThermalExposure
        {
            Pos = plantPos,
            WorldSeconds = RebirthUtilities.TotalGameSecondsPassed(),
            PolicyVersion = AdvancedFarmingRuntimePolicy.PolicyVersion,
            Complete = false,
            OpenToExterior = true,
            Mode = "bypass",
            ExitPos = plantPos,
            BlockingPos = Vector3i.zero,
            BlockingBlock = string.Empty,
            NodesVisited = 0
        };

        if (world == null)
            return fallback;

        int now = RebirthUtilities.TotalGameSecondsPassed();
        int policyVersion = AdvancedFarmingRuntimePolicy.PolicyVersion;
        long key = MakeThermalCacheKey(plantPos);
        int cachedAge = 0;
        if (s_thermalExposureCache.TryGetValue(key, out CachedThermalExposure cached)
            && (cachedAge = now - cached.WorldSeconds) >= 0
            && cachedAge <= GetThermalExposureCacheLifetimeSeconds(cached.Pos)
            && cached.PolicyVersion == policyVersion)
        {
            AdvancedFarmingPerfSnapshotService.RecordThermalExposureCacheQuery(true);
            return cached;
        }

        AdvancedFarmingPerfSnapshotService.RecordThermalExposureCacheQuery(false);

        CachedThermalExposure result = SearchThermalOpeningToExterior(world, plantPos);
        result.Pos = plantPos;
        result.WorldSeconds = now;
        result.PolicyVersion = policyVersion;

        // An incomplete result means the flood reached at least one chunk that is not yet
        // present in the synchronous chunk cache. Do not cache that temporary state; the next
        // evaluation must retry after chunk loading advances.
        if (result.Complete)
        {
            EvictThermalCacheIfNeeded(now);
            s_thermalExposureCache[key] = result;
        }

        return result;
    }

    private static void EvictThermalCacheIfNeeded(int now)
    {
        if (s_thermalExposureCache.Count < MaxThermalExposureCacheEntries)
            return;

        s_thermalCacheRemovalScratch.Clear();
        foreach (KeyValuePair<long, CachedThermalExposure> pair in s_thermalExposureCache)
        {
            if (now - pair.Value.WorldSeconds > GetThermalExposureCacheLifetimeSeconds(pair.Value.Pos))
                s_thermalCacheRemovalScratch.Add(pair.Key);
        }

        for (int i = 0; i < s_thermalCacheRemovalScratch.Count; i++)
            s_thermalExposureCache.Remove(s_thermalCacheRemovalScratch[i]);
        s_thermalCacheRemovalScratch.Clear();

        if (s_thermalExposureCache.Count >= MaxThermalExposureCacheEntries)
            s_thermalExposureCache.Clear();
    }


    private static int GetThermalExposureCacheLifetimeSeconds(Vector3i pos)
    {
        return ThermalExposureCacheSeconds + PositiveModulo(HashPosition(pos) ^ 0x2f17c3, ThermalExposureCacheJitterSeconds);
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

    private static CachedThermalExposure SearchThermalOpeningToExterior(WorldBase world, Vector3i plantPos)
    {
        int minX = plantPos.x - ThermalSearchHorizontalRadius;
        int maxX = plantPos.x + ThermalSearchHorizontalRadius;
        int minZ = plantPos.z - ThermalSearchHorizontalRadius;
        int maxZ = plantPos.z + ThermalSearchHorizontalRadius;
        int minY = Math.Max(0, plantPos.y - ThermalSearchDown);
        int maxY = Math.Min(byte.MaxValue - 1, plantPos.y + ThermalSearchUp);
        int widthX = maxX - minX + 1;
        int widthZ = maxZ - minZ + 1;
        int height = maxY - minY + 1;

        int stamp = ++s_thermalSearchStampId;
        if (stamp == int.MaxValue)
        {
            Array.Clear(s_thermalSearchStamp, 0, s_thermalSearchStamp.Length);
            Array.Clear(s_thermalPassMemoStamp, 0, s_thermalPassMemoStamp.Length);
            s_thermalSearchStampId = 1;
            stamp = 1;
        }

        int head = 0;
        int tail = 0;
        EnqueueThermalCell(plantPos, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail);
        EnqueueThermalCell(plantPos + Vector3i.up, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail);

        Vector3i firstBlockerPos = Vector3i.zero;
        string firstBlockerBlock = string.Empty;
        bool incomplete = false;
        Vector3i firstUnavailablePos = Vector3i.zero;
        int nodes = 0;

        while (head < tail && nodes < ThermalSearchMaxNodes)
        {
            Vector3i pos = s_thermalSearchQueue[head++];
            nodes++;

            if (!TryGetLoadedOpenSkyState(world, pos, out bool openSky))
            {
                MarkThermalSearchIncomplete(pos, ref incomplete, ref firstUnavailablePos);
                continue;
            }

            if (openSky)
            {
                AdvancedFarmingPerfSnapshotService.RecordThermalExposureSearch(nodes, true);
                return new CachedThermalExposure
                {
                    Complete = true,
                    OpenToExterior = true,
                    Mode = "openExteriorAtReachedCell",
                    ExitPos = pos,
                    BlockingPos = firstBlockerPos,
                    BlockingBlock = firstBlockerBlock,
                    NodesVisited = nodes
                };
            }

            bool posPassable = IsThermalPassableMemoized(world, pos, minX, minY, minZ, widthX, widthZ, height, stamp);
            TryEnqueueThermalNeighbor(world, pos, posPassable, 1, 0, 0, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail, ref firstBlockerPos, ref firstBlockerBlock, ref incomplete, ref firstUnavailablePos);
            TryEnqueueThermalNeighbor(world, pos, posPassable, -1, 0, 0, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail, ref firstBlockerPos, ref firstBlockerBlock, ref incomplete, ref firstUnavailablePos);
            TryEnqueueThermalNeighbor(world, pos, posPassable, 0, 1, 0, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail, ref firstBlockerPos, ref firstBlockerBlock, ref incomplete, ref firstUnavailablePos);
            TryEnqueueThermalNeighbor(world, pos, posPassable, 0, -1, 0, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail, ref firstBlockerPos, ref firstBlockerBlock, ref incomplete, ref firstUnavailablePos);
            TryEnqueueThermalNeighbor(world, pos, posPassable, 0, 0, 1, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail, ref firstBlockerPos, ref firstBlockerBlock, ref incomplete, ref firstUnavailablePos);
            TryEnqueueThermalNeighbor(world, pos, posPassable, 0, 0, -1, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail, ref firstBlockerPos, ref firstBlockerBlock, ref incomplete, ref firstUnavailablePos);
        }

        AdvancedFarmingPerfSnapshotService.RecordThermalExposureSearch(nodes, false);
        if (incomplete)
        {
            return new CachedThermalExposure
            {
                Complete = false,
                OpenToExterior = false,
                Mode = "thermalSearchDeferredChunkUnavailable",
                ExitPos = Vector3i.zero,
                BlockingPos = firstUnavailablePos,
                BlockingBlock = "<chunk-unavailable>",
                NodesVisited = nodes
            };
        }

        // Hitting the node budget is not proof of enclosure if admitted cells remain queued.
        // Return an explicitly incomplete result so the caller will not cache a false seal.
        if (nodes >= ThermalSearchMaxNodes && head < tail)
        {
            return new CachedThermalExposure
            {
                Complete = false,
                OpenToExterior = false,
                Mode = "thermalSearchDeferredNodeBudget",
                ExitPos = Vector3i.zero,
                BlockingPos = firstBlockerPos,
                BlockingBlock = firstBlockerBlock,
                NodesVisited = nodes
            };
        }

        return new CachedThermalExposure
        {
            Complete = true,
            OpenToExterior = false,
            Mode = "noThermalOpening",
            ExitPos = Vector3i.zero,
            BlockingPos = firstBlockerPos,
            BlockingBlock = firstBlockerBlock,
            NodesVisited = nodes
        };
    }

    private static bool TryGetLoadedOpenSkyState(WorldBase world, Vector3i pos, out bool openSky)
    {
        openSky = false;
        if (world == null)
            return false;

        IChunk chunk = world.GetChunkSync(World.toChunkXZ(pos.x), World.toChunkXZ(pos.z));
        if (chunk == null)
            return false;

        openSky = pos.y >= chunk.GetHeight(World.toBlockXZ(pos.x), World.toBlockXZ(pos.z));
        return true;
    }

    private static bool IsThermalChunkLoaded(WorldBase world, Vector3i pos)
    {
        return world != null
            && world.GetChunkSync(World.toChunkXZ(pos.x), World.toChunkXZ(pos.z)) != null;
    }

    private static void MarkThermalSearchIncomplete(
        Vector3i pos,
        ref bool incomplete,
        ref Vector3i firstUnavailablePos)
    {
        if (!incomplete)
            firstUnavailablePos = pos;
        incomplete = true;
    }

    private static void TryEnqueueThermalNeighbor(
        WorldBase world,
        Vector3i from,
        bool fromPassable,
        int dx,
        int dy,
        int dz,
        int minX,
        int minY,
        int minZ,
        int widthX,
        int widthZ,
        int height,
        int stamp,
        ref int tail,
        ref Vector3i firstBlockerPos,
        ref string firstBlockerBlock,
        ref bool incomplete,
        ref Vector3i firstUnavailablePos)
    {
        Vector3i to = new Vector3i(from.x + dx, from.y + dy, from.z + dz);
        if (to.x < minX || to.x >= minX + widthX || to.y < minY || to.y >= minY + height || to.z < minZ || to.z >= minZ + widthZ)
            return;

        if (!fromPassable)
        {
            if (firstBlockerBlock.Length == 0)
            {
                firstBlockerPos = from;
                firstBlockerBlock = GetBlockNameAt(world, from);
            }
            return;
        }

        if (!IsThermalChunkLoaded(world, to))
        {
            MarkThermalSearchIncomplete(to, ref incomplete, ref firstUnavailablePos);
            return;
        }

        bool toPassable = IsThermalPassableMemoized(world, to, minX, minY, minZ, widthX, widthZ, height, stamp);
        if (!toPassable)
        {
            if (firstBlockerBlock.Length == 0)
            {
                firstBlockerPos = to;
                firstBlockerBlock = GetBlockNameAt(world, to);
            }
            return;
        }

        EnqueueThermalCell(to, minX, minY, minZ, widthX, widthZ, height, stamp, ref tail);
    }

    private static bool IsThermalPassableMemoized(WorldBase world, Vector3i pos, int minX, int minY, int minZ, int widthX, int widthZ, int height, int stamp)
    {
        int index = MakeThermalIndex(pos, minX, minY, minZ, widthX, widthZ, height);
        if (index < 0 || index >= s_thermalPassMemoStamp.Length)
            return IsThermalPassableCell(world, pos);

        if (s_thermalPassMemoStamp[index] == stamp)
            return s_thermalPassMemo[index] == 1;

        bool passable = IsThermalPassableCell(world, pos);
        s_thermalPassMemoStamp[index] = stamp;
        s_thermalPassMemo[index] = passable ? (byte)1 : (byte)2;
        return passable;
    }

    private static string GetBlockNameAt(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return "<null>";

        try
        {
            BlockValue value = world.GetBlock(pos);
            return value.Block != null ? value.Block.GetBlockName() : "<null>";
        }
        catch
        {
            return "<error>";
        }
    }

    private static void EnqueueThermalCell(Vector3i pos, int minX, int minY, int minZ, int widthX, int widthZ, int height, int stamp, ref int tail)
    {
        if (tail >= s_thermalSearchQueue.Length)
            return;

        int index = MakeThermalIndex(pos, minX, minY, minZ, widthX, widthZ, height);
        if (index < 0 || index >= s_thermalSearchStamp.Length || s_thermalSearchStamp[index] == stamp)
            return;

        s_thermalSearchStamp[index] = stamp;
        s_thermalSearchQueue[tail++] = pos;
    }

    private static int MakeThermalIndex(Vector3i pos, int minX, int minY, int minZ, int widthX, int widthZ, int height)
    {
        int x = pos.x - minX;
        int y = pos.y - minY;
        int z = pos.z - minZ;
        if (x < 0 || x >= widthX || y < 0 || y >= height || z < 0 || z >= widthZ)
            return -1;
        return ((y * widthZ) + z) * widthX + x;
    }

    private static bool IsThermalPassableCell(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return true;

        BlockValue blockValue = world.GetBlock(pos);
        if (blockValue.isair)
            return true;

        Block block = blockValue.Block;
        if (block == null)
            return true;

        ThermalRule rule = ResolveThermalRule(block, blockValue);
        switch (rule)
        {
            case ThermalRule.AlwaysPass:
            case ThermalRule.CurtainOrBlind:
            case ThermalRule.GeneratedShapePass:
                return true;
            case ThermalRule.AlwaysBlock:
            case ThermalRule.GeneratedShapeBlock:
                return false;
            case ThermalRule.Shutter:
                return IsVerifiedWallShutterPassState(world, pos, blockValue, block);
            case ThermalRule.WallHatch:
            {
                string lower = GetLowerBlockName(block);
                bool? pass = TryGetVerifiedWallHatchThermalPassState(world, pos, blockValue, block, lower);
                if (pass.HasValue)
                    return pass.Value;
                return IsThermalPassableAfterHatchFallback(world, pos, blockValue, block, lower);
            }
            case ThermalRule.CenteredNonGlass:
                return IsVerifiedCenteredCoverThermalPassState(world, pos, blockValue, block);
            case ThermalRule.CenteredWindowGlass:
            {
                string lower = GetLowerBlockName(block);
                return IsBrokenOrThermallyOpenWindow(world, pos, blockValue, block, lower)
                    || IsVerifiedCenteredCoverThermalPassState(world, pos, blockValue, block);
            }
            case ThermalRule.WindowGlass:
            {
                string lower = GetLowerBlockName(block);
                return IsBrokenOrThermallyOpenWindow(world, pos, blockValue, block, lower);
            }
            default:
                return false;
        }
    }

    private static ThermalRule ResolveThermalRule(Block block, BlockValue blockValue)
    {
        if (block == null)
            return ThermalRule.AlwaysPass;

        int blockId = blockValue.type;
        if (s_thermalRuleByBlockType.TryGetValue(blockId, out ThermalRule cached))
            return cached;

        ThermalRule rule = ComputeThermalRule(block);
        s_thermalRuleByBlockType[blockId] = rule;
        return rule;
    }

    private static ThermalRule ComputeThermalRule(Block block)
    {
        if (block == null)
            return ThermalRule.AlwaysPass;

        string name = block.GetBlockName() ?? string.Empty;
        if (string.Equals(name, "air", StringComparison.OrdinalIgnoreCase))
            return ThermalRule.AlwaysPass;

        string lower = name.ToLowerInvariant();
        if (lower.Contains("plant") || lower.Contains("crop"))
            return ThermalRule.AlwaysPass;

        if (IsCurtainOrBlind(lower))
            return ThermalRule.CurtainOrBlind;

        if (IsShutter(lower))
            return ThermalRule.Shutter;

        if (IsHatch(block, lower))
            return ThermalRule.WallHatch;

        int generatedShapeOpacity;
        bool generatedShapeThermalPassable;
        string generatedShapeCategory;
        if (AdvancedFarmingShapeOpacityPolicy.TryGetGeneratedShapePolicy(block, out generatedShapeOpacity, out generatedShapeThermalPassable, out generatedShapeCategory))
            return generatedShapeThermalPassable ? ThermalRule.GeneratedShapePass : ThermalRule.GeneratedShapeBlock;

        if (IsCenteredNonGlassCover(block, lower))
            return ThermalRule.CenteredNonGlass;

        if (IsCenteredWindowOrGlassCover(block, lower))
            return ThermalRule.CenteredWindowGlass;

        if (IsWindowOrGlassLike(block, lower))
            return ThermalRule.WindowGlass;

        if (block.lightOpacity <= 0)
            return ThermalRule.AlwaysPass;

        if (block.blockMaterial != null && block.blockMaterial.IsPlant)
            return ThermalRule.AlwaysPass;

        return ThermalRule.AlwaysBlock;
    }

    private static string GetLowerBlockName(Block block)
    {
        string name = block != null ? block.GetBlockName() : string.Empty;
        return string.IsNullOrEmpty(name) ? string.Empty : name.ToLowerInvariant();
    }

    private static bool IsThermalPassableAfterHatchFallback(WorldBase world, Vector3i pos, BlockValue blockValue, Block block, string lower)
    {
        int generatedShapeOpacity;
        bool generatedShapeThermalPassable;
        string generatedShapeCategory;
        if (AdvancedFarmingShapeOpacityPolicy.TryGetGeneratedShapePolicy(block, out generatedShapeOpacity, out generatedShapeThermalPassable, out generatedShapeCategory))
            return generatedShapeThermalPassable;

        if (IsCenteredNonGlassCover(block, lower))
            return IsVerifiedCenteredCoverThermalPassState(world, pos, blockValue, block);

        if (IsCenteredWindowOrGlassCover(block, lower))
            return IsBrokenOrThermallyOpenWindow(world, pos, blockValue, block, lower)
                || IsVerifiedCenteredCoverThermalPassState(world, pos, blockValue, block);

        if (IsWindowOrGlassLike(block, lower))
            return IsBrokenOrThermallyOpenWindow(world, pos, blockValue, block, lower);

        if (block.lightOpacity <= 0)
            return true;

        if (block.blockMaterial != null && block.blockMaterial.IsPlant)
            return true;

        return false;
    }

    private static bool IsVerifiedWallShutterPassState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (world == null || block == null)
            return false;

        byte rotation = ResolveStateRotation(world, pos, blockValue, block);
        if (InferPlacementPlane(world, pos) != ThermalPlacementPlane.Wall)
            return false;

        if (!IsVerifiedWallShutterPassApertureAxis(world, pos))
            return false;

        bool open = IsStateOpen(world, pos, blockValue, block);
        return IsVerifiedWallShutterPassRotationState(rotation, open);
    }

    private static bool IsVerifiedWallShutterPassApertureAxis(WorldBase world, Vector3i pos)
    {
        // The verified shutter pass table was captured on the east/west wall aperture.
        // The perpendicular north/south wall test uses the same rotation/state values
        // but should block light and seal temperature. Keep the table axis-scoped.
        if (world == null)
            return false;

        int eastWestScore = GetAxisOpenScore(world, pos, 1, 0, 0);
        int northSouthScore = GetAxisOpenScore(world, pos, 0, 0, 1);
        return eastWestScore > northSouthScore;
    }

    private static bool IsVerifiedWallShutterPassRotationState(byte rotation, bool open)
    {
        // Grounded pass table for wall-mounted shutters, but only for the verified
        // east/west aperture axis. A later perpendicular-wall test showed the same
        // rotation/state rows must block on the north/south aperture, so the axis
        // guard lives in IsVerifiedWallShutterPassApertureAxis(...).
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

    private enum ThermalPlacementPlane
    {
        Unknown,
        Wall,
        FloorCeiling
    }

    private enum ThermalWallApertureAxis
    {
        Unknown,
        EastWest,
        NorthSouth
    }

    private static ThermalPlacementPlane InferPlacementPlane(WorldBase world, Vector3i pos)
    {
        int verticalScore = GetAxisOpenScore(world, pos, 0, 1, 0);
        int eastWestScore = GetAxisOpenScore(world, pos, 1, 0, 0);
        int northSouthScore = GetAxisOpenScore(world, pos, 0, 0, 1);
        int horizontalScore = Math.Max(eastWestScore, northSouthScore);

        if (verticalScore > horizontalScore)
            return ThermalPlacementPlane.FloorCeiling;
        if (horizontalScore > verticalScore)
            return ThermalPlacementPlane.Wall;
        return ThermalPlacementPlane.Unknown;
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

        if (neighbor.blockMaterial != null && neighbor.blockMaterial.IsPlant)
            return 2;

        int generatedShapeOpacity;
        bool generatedShapeThermalPassable;
        string generatedShapeCategory;
        if (AdvancedFarmingShapeOpacityPolicy.TryGetGeneratedShapePolicy(neighbor, out generatedShapeOpacity, out generatedShapeThermalPassable, out generatedShapeCategory))
            return generatedShapeOpacity >= 15 ? 0 : 1;

        return neighbor.lightOpacity >= 15 ? 0 : 1;
    }

    private static byte ResolveStateRotation(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (world != null && blockValue.ischild)
        {
            Vector3i parentPos = pos + blockValue.parent;
            BlockValue parentValue = world.GetBlock(parentPos);
            if (!parentValue.ischild && parentValue.type == blockValue.type)
                return parentValue.rotation;
        }
        return blockValue.rotation;
    }

    private static bool IsStateOpen(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        try
        {
            if (block.IsSeeThrough(world, pos, blockValue))
                return true;
        }
        catch
        {
        }

        try
        {
            return !block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.All);
        }
        catch
        {
            return false;
        }
    }


    private static bool? TryGetVerifiedWallHatchThermalPassState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block, string lowerName)
    {
        // Mirrors the hatch light matrices. For each logged placement, only the
        // rotation/open-state combinations from the matching log seal temperature;
        // other combinations are visually open enough to exchange temperature/wind.
        if (world == null || block == null || !IsHatch(block, lowerName))
            return null;

        ThermalPlacementPlane placementPlane = InferPlacementPlane(world, pos);
        if (placementPlane != ThermalPlacementPlane.Wall && placementPlane != ThermalPlacementPlane.FloorCeiling)
            return null;

        BlockValue stateValue;
        Block stateBlock;
        Vector3i statePos;
        ResolveStateBlock(world, pos, blockValue, block, out statePos, out stateValue, out stateBlock);

        bool open = IsHatchOpenState(world, statePos, stateValue, stateBlock);
        if (placementPlane == ThermalPlacementPlane.FloorCeiling)
            return !IsVerifiedCeilingHatchBlockState(stateValue.rotation, open);

        ThermalWallApertureAxis apertureAxis = GetWallApertureAxis(world, pos);
        if (apertureAxis != ThermalWallApertureAxis.EastWest && apertureAxis != ThermalWallApertureAxis.NorthSouth)
            return null;

        return !IsVerifiedWallHatchBlockState(apertureAxis, stateValue.rotation, open);
    }

    private static bool IsVerifiedWallHatchBlockState(ThermalWallApertureAxis apertureAxis, byte rotation, bool open)
    {
        if (apertureAxis == ThermalWallApertureAxis.EastWest)
            return IsVerifiedFirstWallHatchBlockState(rotation, open);

        if (apertureAxis == ThermalWallApertureAxis.NorthSouth)
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

    private static void ResolveStateBlock(WorldBase world, Vector3i pos, BlockValue blockValue, Block block, out Vector3i statePos, out BlockValue stateValue, out Block stateBlock)
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

    private static bool IsHatchOpenState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        if (block == null)
            return false;

        try
        {
            return !block.IsMovementBlocked(world, pos, blockValue, BlockFaceFlag.All);
        }
        catch
        {
        }

        try
        {
            return block.IsSeeThrough(world, pos, blockValue);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHatch(Block block, string lowerName)
    {
        if (block == null)
            return false;

        if (block is BlockTrapDoor)
            return true;

        if (ContainsIgnoreCase(lowerName, "hatch") || ContainsIgnoreCase(lowerName, "trapdoor"))
            return true;

        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        return ContainsIgnoreCase(tags, "hatch") || ContainsIgnoreCase(blockTag, "hatch")
            || ContainsIgnoreCase(place, "hatch") || ContainsIgnoreCase(tags, "trapdoor")
            || ContainsIgnoreCase(blockTag, "trapdoor") || ContainsIgnoreCase(place, "trapdoor");
    }

    private static bool IsVerifiedCenteredCoverThermalPassState(WorldBase world, Vector3i pos, BlockValue blockValue, Block block)
    {
        // Mirrors AdvancedFarmingLightService centered-cover placement rules:
        // default pass, except for the placement-specific logged blocker rotations.
        // Used by centered non-glass plugs and centered window/glass plates so thermal
        // openness matches the rotations where SUN actually passes through the aperture.
        if (world == null || block == null)
            return true;

        byte rotation = ResolveStateRotation(world, pos, blockValue, block);
        ThermalPlacementPlane placementPlane = InferPlacementPlane(world, pos);
        if (placementPlane == ThermalPlacementPlane.FloorCeiling)
            return !IsVerifiedCeilingCenteredNonGlassBlockRotation(rotation);

        if (placementPlane != ThermalPlacementPlane.Wall)
            return true;

        ThermalWallApertureAxis apertureAxis = GetWallApertureAxis(world, pos);
        if (apertureAxis == ThermalWallApertureAxis.EastWest)
            return !IsVerifiedFirstWallCenteredNonGlassBlockRotation(rotation);

        if (apertureAxis == ThermalWallApertureAxis.NorthSouth)
            return !IsVerifiedSecondWallCenteredNonGlassBlockRotation(rotation);

        return true;
    }

    private static ThermalWallApertureAxis GetWallApertureAxis(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return ThermalWallApertureAxis.Unknown;

        int eastWestScore = GetAxisOpenScore(world, pos, 1, 0, 0);
        int northSouthScore = GetAxisOpenScore(world, pos, 0, 0, 1);

        if (eastWestScore > northSouthScore)
            return ThermalWallApertureAxis.EastWest;
        if (northSouthScore > eastWestScore)
            return ThermalWallApertureAxis.NorthSouth;
        return ThermalWallApertureAxis.Unknown;
    }

    private static bool IsVerifiedFirstWallCenteredNonGlassBlockRotation(byte rotation)
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

    private static bool IsVerifiedSecondWallCenteredNonGlassBlockRotation(byte rotation)
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

    private static bool IsVerifiedCeilingCenteredNonGlassBlockRotation(byte rotation)
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

    private static bool IsWindowOrGlassLike(Block block, string lowerName)
    {
        if (lowerName.Contains("window") || lowerName.Contains("glass"))
            return true;

        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        string combined = (tags + " " + blockTag + " " + place).ToLowerInvariant();
        return combined.Contains("window") || combined.Contains("glass");
    }

    private static bool IsBrokenOrThermallyOpenWindow(WorldBase world, Vector3i pos, BlockValue blockValue, Block block, string lowerName)
    {
        if (lowerName.Contains("broken") || lowerName.Contains("destroyed"))
            return true;

        if (block.lightOpacity <= 0)
            return true;

        int passIndex = GetDamageStateLightPassIndex(block);
        int stateIndex = ResolveDamageStateIndexFromName(lowerName);
        return passIndex >= 0 && stateIndex >= passIndex;
    }

    private static int ResolveDamageStateIndexFromName(string lowerName)
    {
        if (string.IsNullOrEmpty(lowerName))
            return 0;

        int dIndex = lowerName.LastIndexOf("d", StringComparison.Ordinal);
        if (dIndex >= 0 && dIndex + 1 < lowerName.Length && char.IsDigit(lowerName[dIndex + 1]))
        {
            int value = 0;
            int i = dIndex + 1;
            bool any = false;
            while (i < lowerName.Length && char.IsDigit(lowerName[i]))
            {
                any = true;
                value = (value * 10) + (lowerName[i] - '0');
                i++;
            }
            if (any)
                return value;
        }

        return lowerName.Contains("broken") || lowerName.Contains("destroyed") ? 1 : 0;
    }

    private static int GetDamageStateLightPassIndex(Block block)
    {
        string text = GetBlockProperty(block, "AdvFarmingLightPassDamageStateIndex");
        if (int.TryParse(text, out int value))
            return value;
        return -1;
    }

    private static string GetBlockProperty(Block block, string key)
    {
        if (block == null || block.Properties == null || string.IsNullOrEmpty(key))
            return string.Empty;
        try
        {
            if (block.Properties.Values != null && block.Properties.Values.TryGetValue(key, out string value))
                return value ?? string.Empty;
        }
        catch
        {
        }
        return string.Empty;
    }

    private static bool IsCurtainOrBlind(string lowerName)
    {
        return lowerName.Contains("curtain") || lowerName.Contains("drape") || lowerName.Contains("blind");
    }

    private static bool IsShutter(string lowerName)
    {
        return lowerName.Contains("shutter");
    }

    private static bool IsCenteredNonGlassCover(Block block, string lowerName)
    {
        if (block == null)
            return false;

        // Already-covered families keep their own thermal rules.
        if (IsCurtainOrBlind(lowerName) || IsShutter(lowerName))
            return false;

        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        string model = GetBlockProperty(block, "Model");
        string shape = GetBlockProperty(block, "Shape");

        if (ContainsIgnoreCase(lowerName, "door") || ContainsIgnoreCase(tags, "door")
            || ContainsIgnoreCase(blockTag, "door") || ContainsIgnoreCase(place, "door")
            || ContainsIgnoreCase(lowerName, "hatch") || ContainsIgnoreCase(tags, "hatch")
            || ContainsIgnoreCase(blockTag, "hatch") || ContainsIgnoreCase(place, "hatch")
            || ContainsIgnoreCase(lowerName, "trapdoor") || ContainsIgnoreCase(tags, "trapdoor")
            || ContainsIgnoreCase(blockTag, "trapdoor") || ContainsIgnoreCase(place, "trapdoor"))
            return false;

        // Actual glass-centered plates are handled by IsCenteredWindowOrGlassCover(...).
        if (ContainsIgnoreCase(lowerName, "glass") || ContainsIgnoreCase(tags, "glass")
            || ContainsIgnoreCase(blockTag, "glass") || ContainsIgnoreCase(place, "glass")
            || ContainsIgnoreCase(model, "glass") || ContainsIgnoreCase(shape, "glass"))
            return false;

        bool centered = ContainsIgnoreCase(lowerName, "ctrplate")
            || ContainsIgnoreCase(lowerName, "centered")
            || ContainsIgnoreCase(tags, "centered")
            || ContainsIgnoreCase(blockTag, "centered")
            || ContainsIgnoreCase(place, "centered")
            || ContainsIgnoreCase(model, "centered")
            || ContainsIgnoreCase(shape, "centered");

        return centered && block.lightOpacity > 0;
    }

    private static bool IsCenteredWindowOrGlassCover(Block block, string lowerName)
    {
        if (block == null)
            return false;

        // Already-covered and door-like families keep their own thermal rules.
        if (IsCurtainOrBlind(lowerName) || IsShutter(lowerName))
            return false;

        string tags = GetBlockProperty(block, "Tags");
        string blockTag = GetBlockProperty(block, "BlockTag");
        string place = GetBlockProperty(block, "Place");
        string model = GetBlockProperty(block, "Model");
        string shape = GetBlockProperty(block, "Shape");
        string shapeCategories = GetBlockProperty(block, "ShapeCategories");
        string customIcon = GetBlockProperty(block, "CustomIcon");
        if (ContainsIgnoreCase(lowerName, "door") || ContainsIgnoreCase(tags, "door")
            || ContainsIgnoreCase(blockTag, "door") || ContainsIgnoreCase(place, "door")
            || ContainsIgnoreCase(lowerName, "hatch") || ContainsIgnoreCase(tags, "hatch")
            || ContainsIgnoreCase(blockTag, "hatch") || ContainsIgnoreCase(place, "hatch")
            || ContainsIgnoreCase(lowerName, "trapdoor") || ContainsIgnoreCase(tags, "trapdoor")
            || ContainsIgnoreCase(blockTag, "trapdoor") || ContainsIgnoreCase(place, "trapdoor"))
            return false;

        if (block.lightOpacity <= 0)
            return false;

        bool windowOrGlass = ContainsIgnoreCase(lowerName, "window") || ContainsIgnoreCase(lowerName, "glass")
            || ContainsIgnoreCase(tags, "window") || ContainsIgnoreCase(tags, "glass")
            || ContainsIgnoreCase(blockTag, "window") || ContainsIgnoreCase(blockTag, "glass")
            || ContainsIgnoreCase(place, "window") || ContainsIgnoreCase(place, "glass")
            || ContainsIgnoreCase(model, "window") || ContainsIgnoreCase(model, "glass")
            || ContainsIgnoreCase(shape, "window") || ContainsIgnoreCase(shape, "glass")
            || ContainsIgnoreCase(shapeCategories, "window") || ContainsIgnoreCase(shapeCategories, "glass")
            || ContainsIgnoreCase(customIcon, "window") || ContainsIgnoreCase(customIcon, "glass");

        if (!windowOrGlass)
            return false;

        bool explicitCentered = ContainsIgnoreCase(lowerName, "ctrplate")
            || ContainsIgnoreCase(lowerName, "centered")
            || ContainsIgnoreCase(tags, "centered")
            || ContainsIgnoreCase(blockTag, "centered")
            || ContainsIgnoreCase(place, "centered")
            || ContainsIgnoreCase(model, "centered")
            || ContainsIgnoreCase(shape, "centered")
            || ContainsIgnoreCase(shapeCategories, "centered")
            || ContainsIgnoreCase(customIcon, "centered");

        // Some 3.0 centered window shapes do not contain CTR/centered in the block name.
        // Example from the diagnostic log: woodShapes:windowIndustrial01Full has
        // ShapeCategories=Basic,Windows and Model=@:Shapes/window_industrial_01_full.fbx,
        // and behaves like the centered plug rotation matrix.
        bool windowShapeAperture = ContainsIgnoreCase(shapeCategories, "windows")
            || ContainsIgnoreCase(model, "/window")
            || ContainsIgnoreCase(model, "\\window")
            || ContainsIgnoreCase(model, "window_")
            || ContainsIgnoreCase(customIcon, "shapewindow");

        return explicitCentered || windowShapeAperture;
    }

    private static bool ContainsIgnoreCase(string text, string value)
    {
        return !string.IsNullOrEmpty(text)
            && !string.IsNullOrEmpty(value)
            && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static long MakeThermalCacheKey(Vector3i pos)
    {
        unchecked
        {
            long key = ((long)(pos.x & 0x1FFFFF) << 43)
                ^ ((long)(pos.y & 0x3FF) << 33)
                ^ (long)(pos.z & 0x1FFFFF);
            return key;
        }
    }

    private static float GetPositionOutdoorTemperature(WorldBase world, Vector3i pos, out string source)
    {
        source = "none";

        WeatherManager weatherManager = WeatherManager.Instance;
        if (weatherManager == null)
            return 0f;

        if (WeatherManager.forceTemperature > -100f)
        {
            source = "forcedTemperature";
            return WeatherManager.forceTemperature;
        }

        World realWorld = world as World;
        if (realWorld == null && GameManager.Instance != null)
            realWorld = GameManager.Instance.World;

        BiomeDefinition biome = realWorld != null ? realWorld.GetBiome(pos.x, pos.z) : null;

        // Vanilla does not derive the early-game grace temperature from the normal biome
        // weather simulation. It assigns a fixed temperature to the local currentWeather
        // object. A dedicated server has no local player blend, so currentWeather remains at
        // its zero/default value. Reproduce the vanilla grace rule deterministically by biome
        // on both server and client.
        if (IsVanillaWeatherGracePeriod(weatherManager))
        {
            source = "vanillaGraceBiome";
            return GetVanillaGraceTemperature(biome);
        }

        if (biome != null)
        {
            WeatherManager.BiomeWeather biomeWeather = weatherManager.FindBiomeWeather((int)biome.m_Id);
            if (biomeWeather != null)
            {
                // NetPackageWeather sends the server's parameterFinals values into each
                // client's parameter targets. Read the network target on a remote client so
                // the crop rule uses the exact authoritative value instead of the client's
                // visual interpolation toward it. The server reads its calculated final.
                if (world != null
                    && world.IsRemote()
                    && biomeWeather.parameters != null
                    && biomeWeather.parameters.Length > 0
                    && biomeWeather.parameters[0] != null)
                {
                    source = "biomeWeatherNetworkTarget";
                    return biomeWeather.parameters[0].target;
                }

                if (biomeWeather.parameterFinals != null
                    && biomeWeather.parameterFinals.Length > 0)
                {
                    source = "biomeWeatherFinal";
                    return biomeWeather.parameterFinals[0];
                }
            }
        }

        source = "currentWeatherFallback";
        return weatherManager.GetCurrentTemperatureValue();
    }

    private static bool IsVanillaWeatherGracePeriod(WeatherManager weatherManager)
    {
        if (weatherManager == null)
            return false;

        return (WeatherManager.worldTime < 22000 || !weatherManager.isGameModeNormal)
            && Math.Abs(weatherManager.CustomWeatherTime + 1f) < 0.001f;
    }

    private static float GetVanillaGraceTemperature(BiomeDefinition biome)
    {
        if (biome == null)
            return 70f;

        switch (biome.m_BiomeType)
        {
            case BiomeDefinition.BiomeType.Snow:
                return 45f;
            case BiomeDefinition.BiomeType.Forest:
            case BiomeDefinition.BiomeType.PineForest:
                return 60f;
            default:
                return 70f;
        }
    }

    public static string GetBiomeName(WorldBase world, Vector3i pos)
    {
        World realWorld = world as World;
        if (realWorld == null && GameManager.Instance != null)
            realWorld = GameManager.Instance.World;

        BiomeDefinition biome = realWorld != null ? realWorld.GetBiome(pos.x, pos.z) : null;
        return biome != null ? biome.m_sBiomeName : string.Empty;
    }

    public static bool IsNight(WorldBase world)
    {
        World realWorld = world as World;
        if (realWorld == null && GameManager.Instance != null)
            realWorld = GameManager.Instance.World;

        return realWorld != null && !realWorld.IsDaytime();
    }

    /// <summary>
    /// Compatibility helper for diagnostics that need to print the configured endpoint values.
    /// isNight=false returns the hot/day-midpoint endpoint, while isNight=true returns the
    /// cold/night-midpoint endpoint. The live crop value is no longer a hard day/night switch;
    /// Evaluate calls the WorldBase overload below and blends between these endpoints smoothly.
    /// </summary>
    public static float GetEnclosedBiomeAdjustment(string biomeName, bool isNight)
    {
        if (TryGetEnclosedBiomeAdjustmentEndpoints(biomeName, out float hotAdjust, out float coldAdjust))
            return isNight ? coldAdjust : hotAdjust;

        return 0f;
    }

    public static float GetEnclosedBiomeAdjustment(
        WorldBase world,
        string biomeName,
        out float hotAdjust,
        out float coldAdjust,
        out float coldBlend,
        out string mode)
    {
        if (!TryGetEnclosedBiomeAdjustmentEndpoints(biomeName, out hotAdjust, out coldAdjust))
        {
            coldBlend = 0f;
            mode = "noBiomeAdjustment";
            return 0f;
        }

        coldBlend = GetEnclosedColdBlend(world, out mode);
        return Lerp(hotAdjust, coldAdjust, coldBlend);
    }

    private static bool TryGetEnclosedBiomeAdjustmentEndpoints(string biomeName, out float hotAdjust, out float coldAdjust)
    {
        // These are thermal-buffer endpoints, not hard day/night values.
        // hotAdjust applies at the warmest part of the day, coldAdjust applies at the coldest
        // part of the night, and the runtime value linearly blends between them.
        // Negative values mean the enclosure cools an overly hot biome; positive values mean the
        // enclosure warms an overly cold biome. Biomes like snow can therefore remain positive all
        // day, while desert can remain negative all day.
        if (BiomeEquals(biomeName, "pine_forest"))
        {
            hotAdjust = -10f;
            coldAdjust = 10f;
            return true;
        }

        if (BiomeEquals(biomeName, "desert"))
        {
            hotAdjust = -20f;
            coldAdjust = -10f;
            return true;
        }

        if (BiomeEquals(biomeName, "snow"))
        {
            hotAdjust = 15f;
            coldAdjust = 25f;
            return true;
        }

        if (BiomeEquals(biomeName, "wasteland") || BiomeEquals(biomeName, "burnt_forest") || BiomeEquals(biomeName, "burnt"))
        {
            hotAdjust = 0f;
            coldAdjust = 5f;
            return true;
        }

        hotAdjust = 0f;
        coldAdjust = 0f;
        return false;
    }

    private static float GetEnclosedColdBlend(WorldBase world, out string mode)
    {
        World realWorld = world as World;
        if (realWorld == null && GameManager.Instance != null)
            realWorld = GameManager.Instance.World;

        if (realWorld == null)
        {
            mode = "fallbackNoWorldHalfBlend";
            return 0.5f;
        }

        float dawnHour = ClampHour(realWorld.DawnHour);
        float duskHour = ClampHour(realWorld.DuskHour);
        if (Math.Abs(dawnHour - duskHour) < 0.001f)
        {
            mode = "fallbackInvalidDawnDuskHalfBlend";
            return 0.5f;
        }

        float currentHour = GetWorldHour(realWorld);
        float hotMidpoint = CircularMidpointHour(dawnHour, duskHour);
        float coldMidpoint = CircularMidpointHour(duskHour, dawnHour + 24f);

        if (IsOnClockwiseArc(currentHour, hotMidpoint, coldMidpoint))
        {
            mode = "linearHotMidpointToColdMidpoint";
            return ClockwiseProgress(currentHour, hotMidpoint, coldMidpoint);
        }

        mode = "linearColdMidpointToHotMidpoint";
        return 1f - ClockwiseProgress(currentHour, coldMidpoint, hotMidpoint);
    }

    private static float GetWorldHour(World world)
    {
        if (world == null)
            return 0f;

        ulong worldTime = world.GetWorldTime();
        int hour = GameUtils.WorldTimeToHours(worldTime);
        int minute = GameUtils.WorldTimeToMinutes(worldTime);
        return NormalizeHour(hour + (minute / 60f));
    }

    private static float CircularMidpointHour(float startHour, float endHour)
    {
        float midpoint = startHour + (PositiveModuloFloat(endHour - startHour, 24f) * 0.5f);
        return NormalizeHour(midpoint);
    }

    private static bool IsOnClockwiseArc(float valueHour, float startHour, float endHour)
    {
        float arcLength = PositiveModuloFloat(endHour - startHour, 24f);
        float valueDistance = PositiveModuloFloat(valueHour - startHour, 24f);
        return valueDistance <= arcLength;
    }

    private static float ClockwiseProgress(float valueHour, float startHour, float endHour)
    {
        float arcLength = PositiveModuloFloat(endHour - startHour, 24f);
        if (arcLength <= 0.001f)
            return 0f;

        float valueDistance = PositiveModuloFloat(valueHour - startHour, 24f);
        if (valueDistance < 0f)
            valueDistance = 0f;
        if (valueDistance > arcLength)
            valueDistance = arcLength;
        return valueDistance / arcLength;
    }

    private static float NormalizeHour(float hour)
    {
        return PositiveModuloFloat(hour, 24f);
    }

    private static float PositiveModuloFloat(float value, float divisor)
    {
        if (divisor <= 0.001f)
            return 0f;

        float result = value % divisor;
        return result < 0f ? result + divisor : result;
    }

    private static float ClampHour(int hour)
    {
        if (hour < 0)
            return 0f;
        if (hour > 23)
            return 23f;
        return hour;
    }

    private static float Lerp(float from, float to, float t)
    {
        if (t < 0f)
            t = 0f;
        else if (t > 1f)
            t = 1f;
        return from + ((to - from) * t);
    }

    private static bool BiomeEquals(string biomeName, string expected)
    {
        return string.Equals(biomeName, expected, StringComparison.OrdinalIgnoreCase);
    }
}
