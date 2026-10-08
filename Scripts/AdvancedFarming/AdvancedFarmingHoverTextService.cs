using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Read-only hover text builder for Advanced Farming.
///
/// It intentionally does not mutate gameplay state. It may predict display countdown by combining
/// persisted crop progress with elapsed game ticks since the last processed crop update.
/// </summary>
public static class AdvancedFarmingHoverTextService
{
    private const int ConditionCacheSeconds = 1;
    private const int MaxConditionCacheEntries = 2048;
    private const int MaxClientDisplayBaselines = 2048;
    private static readonly Dictionary<long, CachedPlantConditions> s_plantConditionCache = new Dictionary<long, CachedPlantConditions>(256);
    private static readonly Dictionary<long, ClientDisplayBaseline> s_clientDisplayBaselines = new Dictionary<long, ClientDisplayBaseline>(256);
    private static CachedHoverText s_lastPlantHoverText;
    private static CachedHoverText s_lastFarmPlotHoverText;
    private static CachedHoverText s_lastWaterProviderHoverText;

    private static bool s_hoverTraceEnabled;
    private static Vector3i s_hoverTracePos;
    private static long s_hoverTraceCalls;
    private static ulong s_hoverTraceFirstNowTicks;
    private static ulong s_hoverTraceLastNowTicks;
    private static ulong s_hoverTraceFirstRemainingSeconds;
    private static ulong s_hoverTraceLastRemainingSeconds;
    private static ulong s_hoverTraceMinRemainingSeconds;
    private static ulong s_hoverTraceMaxRemainingSeconds;
    private static ulong s_hoverTraceLastAccumulatedTicks;
    private static ulong s_hoverTraceFirstAccumulatedTicks;
    private static long s_hoverTraceDisplayChanges;
    private static long s_hoverTraceForwardJumps;
    private static long s_hoverTraceBackwardJumps;
    private static ulong s_hoverTraceMaxDisplayDeltaSeconds;
    private static string s_hoverTraceLastText = string.Empty;

    public static void ClearConditionCache()
    {
        s_plantConditionCache.Clear();
        s_lastPlantHoverText.Text = null;
        s_lastFarmPlotHoverText.Text = null;
        s_lastWaterProviderHoverText.Text = null;
    }

    public static void ClearDisplayPredictionState()
    {
        s_clientDisplayBaselines.Clear();
    }

    public static void ClearAllState()
    {
        ClearConditionCache();
        ClearDisplayPredictionState();
    }

    public static void InvalidatePlantState(Vector3i blockPos)
    {
        long key = MakeKey(blockPos);
        s_plantConditionCache.Remove(key);
        s_clientDisplayBaselines.Remove(key);
        if (s_lastPlantHoverText.Key == key)
            s_lastPlantHoverText.Text = null;
    }

    public static string StartHoverTrace(Vector3i pos)
    {
        s_hoverTracePos = pos;
        ResetHoverTraceCounters();
        s_hoverTraceEnabled = true;
        return "[AdvancedFarming HoverTrace] enabled=true pos=" + pos;
    }

    public static string StopHoverTrace()
    {
        s_hoverTraceEnabled = false;
        return "[AdvancedFarming HoverTrace] enabled=false pos=" + s_hoverTracePos;
    }

    public static string ResetHoverTrace()
    {
        ResetHoverTraceCounters();
        return "[AdvancedFarming HoverTrace] reset pos=" + s_hoverTracePos + " enabled=" + s_hoverTraceEnabled;
    }

    public static string BuildHoverTraceReport()
    {
        ulong tickSpan = s_hoverTraceLastNowTicks >= s_hoverTraceFirstNowTicks ? s_hoverTraceLastNowTicks - s_hoverTraceFirstNowTicks : 0UL;
        ulong secondSpan = tickSpan / 20UL;
        ulong minRemaining = s_hoverTraceMinRemainingSeconds == ulong.MaxValue ? 0UL : s_hoverTraceMinRemainingSeconds;

        return "[AdvancedFarming HoverTrace] enabled=" + s_hoverTraceEnabled
            + " pos=" + s_hoverTracePos
            + " calls=" + s_hoverTraceCalls
            + " spanTicks=" + tickSpan
            + " spanSeconds=" + secondSpan
            + " firstRemainingSeconds=" + s_hoverTraceFirstRemainingSeconds
            + " lastRemainingSeconds=" + s_hoverTraceLastRemainingSeconds
            + " minRemainingSeconds=" + minRemaining
            + " maxRemainingSeconds=" + s_hoverTraceMaxRemainingSeconds
            + " displayChanges=" + s_hoverTraceDisplayChanges
            + " forwardJumps=" + s_hoverTraceForwardJumps
            + " backwardJumps=" + s_hoverTraceBackwardJumps
            + " maxDisplayDeltaSeconds=" + s_hoverTraceMaxDisplayDeltaSeconds
            + " firstAccumulatedTicks=" + s_hoverTraceFirstAccumulatedTicks
            + " lastAccumulatedTicks=" + s_hoverTraceLastAccumulatedTicks
            + " lastText=" + SanitizeTraceText(s_hoverTraceLastText);
    }

    private static void ResetHoverTraceCounters()
    {
        s_hoverTraceCalls = 0L;
        s_hoverTraceFirstNowTicks = 0UL;
        s_hoverTraceLastNowTicks = 0UL;
        s_hoverTraceFirstRemainingSeconds = 0UL;
        s_hoverTraceLastRemainingSeconds = 0UL;
        s_hoverTraceMinRemainingSeconds = ulong.MaxValue;
        s_hoverTraceMaxRemainingSeconds = 0UL;
        s_hoverTraceLastAccumulatedTicks = 0UL;
        s_hoverTraceFirstAccumulatedTicks = 0UL;
        s_hoverTraceDisplayChanges = 0L;
        s_hoverTraceForwardJumps = 0L;
        s_hoverTraceBackwardJumps = 0L;
        s_hoverTraceMaxDisplayDeltaSeconds = 0UL;
        s_hoverTraceLastText = string.Empty;
    }

    private static void RecordHoverTrace(Vector3i pos, TileEntityPlantGrowingRebirth te, ulong remainingTicks, string activationText)
    {
        if (!s_hoverTraceEnabled)
            return;

        if (pos.x != s_hoverTracePos.x || pos.y != s_hoverTracePos.y || pos.z != s_hoverTracePos.z)
            return;

        ulong nowTicks = GameTimer.Instance.ticks;
        ulong remainingSeconds = (remainingTicks + 19UL) / 20UL;
        ulong accumulatedTicks = te != null ? te.AccumulatedTicks : 0UL;

        if (s_hoverTraceCalls == 0L)
        {
            s_hoverTraceFirstNowTicks = nowTicks;
            s_hoverTraceFirstRemainingSeconds = remainingSeconds;
            s_hoverTraceMinRemainingSeconds = remainingSeconds;
            s_hoverTraceMaxRemainingSeconds = remainingSeconds;
            s_hoverTraceFirstAccumulatedTicks = accumulatedTicks;
        }
        else if (remainingSeconds != s_hoverTraceLastRemainingSeconds)
        {
            s_hoverTraceDisplayChanges++;

            ulong delta = remainingSeconds > s_hoverTraceLastRemainingSeconds
                ? remainingSeconds - s_hoverTraceLastRemainingSeconds
                : s_hoverTraceLastRemainingSeconds - remainingSeconds;

            if (delta > s_hoverTraceMaxDisplayDeltaSeconds)
                s_hoverTraceMaxDisplayDeltaSeconds = delta;

            if (remainingSeconds > s_hoverTraceLastRemainingSeconds)
                s_hoverTraceBackwardJumps++;
            else if (delta > 1UL)
                s_hoverTraceForwardJumps++;
        }

        if (remainingSeconds < s_hoverTraceMinRemainingSeconds)
            s_hoverTraceMinRemainingSeconds = remainingSeconds;

        if (remainingSeconds > s_hoverTraceMaxRemainingSeconds)
            s_hoverTraceMaxRemainingSeconds = remainingSeconds;

        s_hoverTraceCalls++;
        s_hoverTraceLastNowTicks = nowTicks;
        s_hoverTraceLastRemainingSeconds = remainingSeconds;
        s_hoverTraceLastAccumulatedTicks = accumulatedTicks;
        s_hoverTraceLastText = activationText;
    }

    private static string SanitizeTraceText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text.Replace("\r", "\\r").Replace("\n", "\\n");
    }


    private struct CachedHoverText
    {
        public long Key;
        public int WorldSeconds;
        public int BlockType;
        public int ParamA;
        public int ParamB;
        public int ParamC;
        public string Text;
    }

    private struct CachedPlantConditions
    {
        public int WorldSeconds;
        public int PolicyVersion;
        public bool HasSunLight;
        public bool HasBlockLight;
        public bool HasOpenSky;
        public bool IsNight;
        public string BiomeName;
        public float Temperature;
        public int FarmPlotWater;
        public int WaterInTank;
        public int WaterInCollector;
    }

    private struct ClientDisplayBaseline
    {
        public int BlockType;
        public ulong StageRateTicks;
        public ulong AuthoritativeGameTimerTicks;
        public ulong AuthoritativeAccumulatedTicks;
        public ulong LocalBaselineTicks;
        public bool GrowthBlocked;
    }

    public static string GetFarmPlotText(WorldBase world, BlockValue blockValue, Vector3i blockPos)
    {
        TileEntityFarmPlotRebirth te = world.GetTileEntity(blockPos) as TileEntityFarmPlotRebirth;
        if (te == null)
            return string.Empty;

        long hoverKey = MakeKey(blockPos);
        int hoverNow = RebirthUtilities.TotalGameSecondsPassed();
        string cachedHoverText;
        if (TryGetHoverTextCache(ref s_lastFarmPlotHoverText, hoverKey, hoverNow, blockValue.type, te.waterCount, 0, 0, out cachedHoverText))
            return cachedHoverText;

        int max = ResolveFarmPlotMax(blockValue, te);
#if DEBUG
        if (AdvancedFarmingDebug.Hover)
            AdvancedFarmingDebug.Log("hover", "farm plot hover at " + blockPos + " water=" + te.waterCount + "/" + max);
#endif
        string color = te.waterCount >= max ? "5ed680" : "e3dc5b";
        string text = Localization.Get("ttWaterContent") + " [[" + color + "]" + te.waterCount + "[-]/[5ed680]" + max + "[-]]";
        StoreHoverTextCache(ref s_lastFarmPlotHoverText, hoverKey, hoverNow, blockValue.type, te.waterCount, 0, 0, text);
        return text;
    }

    public static string GetWaterProviderText(WorldBase world, BlockValue blockValue, Vector3i blockPos)
    {
        TileEntityWaterTankRebirth te = world.GetTileEntity(blockPos) as TileEntityWaterTankRebirth;
        long hoverKey = MakeKey(blockPos);
        int hoverNow = RebirthUtilities.TotalGameSecondsPassed();
        int waterCount = te != null ? te.waterCount : -1;
        string cachedHoverText;
        if (TryGetHoverTextCache(ref s_lastWaterProviderHoverText, hoverKey, hoverNow, blockValue.type, waterCount, 0, 0, out cachedHoverText))
            return cachedHoverText;

        if (te == null || te.waterCount <= 0)
        {
            string emptyText = Localization.Get("ttTankEmpty");
            StoreHoverTextCache(ref s_lastWaterProviderHoverText, hoverKey, hoverNow, blockValue.type, waterCount, 0, 0, emptyText);
            return emptyText;
        }

        int max = ResolveWaterProviderMax(blockValue, te);
        string color = te.waterCount >= max ? "5ed680" : "e3dc5b";
        string text = Localization.Get("ttWaterContent") + " [[" + color + "]" + te.waterCount + "[-]/[5ed680]" + max + "[-]]";
        StoreHoverTextCache(ref s_lastWaterProviderHoverText, hoverKey, hoverNow, blockValue.type, waterCount, 0, 0, text);
        return text;
    }

    public static string GetPlantText(
        BlockPlantGrowingRebirth plantBlock,
        WorldBase world,
        BlockValue blockValue,
        Vector3i blockPos,
        int lightLevelStay,
        int lightLevelGrow,
        int minTemp)
    {
        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : plantBlock.GetBlockName();
        int growthStage = GetPlantGrowthStageFromBlockName(blockName);
        long hoverKey = MakeKey(blockPos);
        // The countdown is based on GameTimer ticks, so its display cache must use the same
        // 20 Hz gameplay clock. World.GetWorldTime() is scaled by the configured day length
        // and can advance one stored "second" only every several real/gameplay seconds.
        int hoverNow = GetDisplaySecond();
        string cachedHoverText;
        if (TryGetHoverTextCache(ref s_lastPlantHoverText, hoverKey, hoverNow, blockValue.type, lightLevelStay, lightLevelGrow, minTemp, out cachedHoverText))
            return cachedHoverText;

#if DEBUG
        if (AdvancedFarmingDebug.Hover)
            AdvancedFarmingDebug.Log("hover", "plant hover at " + blockPos + " block=" + blockName + " stage=" + growthStage);
#endif

        string blockLocalization = Localization.Get(blockName);

        // Stage-1 crops expose the standard native take command. Use the native pickupPrompt
        // format here as the hover header so PlayerMoveController can replace its preserved
        // {0} token with the player's current Activate binding (E or any remapped key).
        if (plantBlock.IsSeedStage(blockValue))
            blockLocalization = string.Format(Localization.Get("pickupPrompt"), (object)blockLocalization);

        string activationText = blockLocalization;

        // A harvest-stage crop has no countdown or growth conditions to prepare.
        // Return before light/thermal/water/stage-rate work so hover remains observational.
        if (growthStage == 3)
        {
            TileEntityPlantGrowingRebirth matureTe = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
            string fullyGrownText = GetFullyGrownCropText(blockName);
            StoreHoverTextCache(ref s_lastPlantHoverText, hoverKey, hoverNow, blockValue.type, lightLevelStay, lightLevelGrow, minTemp, fullyGrownText);
            RecordHoverTrace(blockPos, matureTe, 0UL, fullyGrownText);
            return fullyGrownText;
        }

        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;
        bool isMushroom = IsMushroom(blockValue);

        TileEntityPlantGrowingRebirth te = world.GetTileEntity(blockPos) as TileEntityPlantGrowingRebirth;
        ulong stageRate = GetStageRate(world, blockPos);
        if (stageRate < 1UL)
            stageRate = 1UL;

        CachedPlantConditions conditions = GetPlantConditions(world, blockPos, blockValue, lightLevelStay, lightLevelGrow, isMushroom, advancedFarming);
        int depletion = RebirthUtilities.GetCropWaterDepletion(blockValue, conditions.Temperature);

        bool hasValidLight = (((conditions.HasSunLight || conditions.HasOpenSky) && !conditions.IsNight) || conditions.HasBlockLight || isMushroom);
        if (world.IsRemote() && !isMushroom)
        {
            // A remote client's local SUN map is not authoritative during chunk join. Until the
            // server's crop state arrives, fail closed rather than showing a countdown that the
            // dedicated server will not honor. Once synchronized, use the server's final light
            // pass/fail result and keep local light evaluation diagnostic-only.
            hasValidLight = te != null
                && te.AuthoritativeLightState == AdvancedFarmingAuthoritativeLightState.Pass;
        }
        bool hasValidTemperature = !advancedFarming || isMushroom || conditions.Temperature >= minTemp;
        bool isBlockedBiome = !isMushroom && string.Equals(conditions.BiomeName, "wasteland", StringComparison.OrdinalIgnoreCase);
        bool alreadyWateredForCycle = te != null && te.bWatered;
        bool hasValidWater = true;

        if (advancedFarming && !isMushroom && !alreadyWateredForCycle)
        {
            int bestStorageWater = Math.Max(conditions.WaterInTank, conditions.WaterInCollector);
            hasValidWater = bestStorageWater >= depletion || conditions.FarmPlotWater >= depletion;
        }

        bool growthBlocked = advancedFarming && (!hasValidLight || !hasValidTemperature || isBlockedBiome || !hasValidWater);
        ulong traceRemainingTicks = te != null && te.AccumulatedTicks < stageRate ? stageRate - te.AccumulatedTicks : 0UL;

        if (te != null)
        {
            ulong displayAccumulated = GetPredictedDisplayAccumulatedTicks(
                world,
                blockPos,
                blockValue.type,
                te,
                stageRate,
                growthBlocked);
            ulong remaining = displayAccumulated >= stageRate ? 0UL : stageRate - displayAccumulated;
            traceRemainingTicks = remaining;
            string timerText = FormatTimer(remaining);

            if (growthStage == 1)
                activationText = blockLocalization + "\n" + Localization.Get("ttNextCycle1") + timerText;
            else if (growthStage == 2)
                activationText = blockLocalization + "\n" + Localization.Get("ttNextCycle2") + timerText;
        }

        if (!advancedFarming)
        {
            StoreHoverTextCache(ref s_lastPlantHoverText, hoverKey, hoverNow, blockValue.type, lightLevelStay, lightLevelGrow, minTemp, activationText);
            return activationText;
        }

        if (!hasValidLight)
            activationText = blockLocalization + "\n" + Localization.Get("ttNeedLight");

        if (!isMushroom && !alreadyWateredForCycle && !hasValidWater)
        {
            bool storageMissing = conditions.WaterInTank < 0 && conditions.WaterInCollector < 0;
            if (storageMissing)
                activationText = blockLocalization + "\n" + Localization.Get("ttNeedWater");
            else
                activationText = blockLocalization + "\n" + Localization.Get("ttNoWaterInTank");
        }

        if (isBlockedBiome)
            activationText = blockLocalization + "\n" + Localization.Get("ttNoGrowWasteland");

        string tempColor = "[6bafb0]";

        if (!hasValidTemperature)
        {
            activationText = blockLocalization + "\n" + Localization.Get("ttNoGrowTooCold");
            tempColor = "[e0dd7e]";
        }

        bool usesExtraWater = RebirthUtilities.GetCropWaterDepletion(blockValue, conditions.Temperature) > 1;

        if (!isMushroom && usesExtraWater)
        {
            activationText += Localization.Get("ttUsesMoreWater");
            tempColor = "[d47474]";
        }

        activationText += "\n(" + Localization.Get("ttCropTempStart") + minTemp + Localization.Get("ttCropTempEnd") + ": "
            + tempColor + conditions.Temperature.ToString("0.00") + "[-] " + Localization.Get("ttTempUnit") + ")";

        StoreHoverTextCache(ref s_lastPlantHoverText, hoverKey, hoverNow, blockValue.type, lightLevelStay, lightLevelGrow, minTemp, activationText);
        RecordHoverTrace(blockPos, te, traceRemainingTicks, activationText);
        return activationText;
    }

    private static CachedPlantConditions GetPlantConditions(
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        int lightLevelStay,
        int lightLevelGrow,
        bool isMushroom,
        bool advancedFarming)
    {
        int now = RebirthUtilities.TotalGameSecondsPassed();
        long key = MakeKey(blockPos);

        bool currentIsNight = !GameManager.Instance.World.IsDaytime();

        int policyVersion = AdvancedFarmingRuntimePolicy.PolicyVersion;

        if (s_plantConditionCache.TryGetValue(key, out CachedPlantConditions cached) && now - cached.WorldSeconds <= ConditionCacheSeconds && cached.IsNight == currentIsNight && cached.PolicyVersion == policyVersion)
            return cached;

        CachedPlantConditions c = new CachedPlantConditions();
        c.WorldSeconds = now;
        c.PolicyVersion = policyVersion;
        c.HasSunLight = true;
        c.HasBlockLight = true;
        c.HasOpenSky = true;
        c.IsNight = currentIsNight;

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            blockPos,
            lightLevelStay,
            lightLevelGrow,
            isMushroom,
            advancedFarming);
        c.HasSunLight = light.HasSunLight;
        c.HasBlockLight = light.HasBlockLight;
        c.HasOpenSky = light.HasOpenSky;

        if (isMushroom)
            c.HasBlockLight = true;

        if (RebirthVariables.customScenario == "hive" && !RebirthUtilities.IsHiveDayActive() && RebirthVariables.customMLPDarkness)
        {
            c.HasSunLight = false;
            c.IsNight = true;
        }

        c.BiomeName = GetBiomeName(blockPos);
        c.Temperature = AdvancedFarmingTemperatureService.Evaluate(world, blockPos, c.HasSunLight || c.HasOpenSky, advancedFarming).Temperature;

        c.WaterInTank = -1;
        c.WaterInCollector = -1;
        c.FarmPlotWater = 0;

        if (!isMushroom)
        {
            c.WaterInTank = RebirthUtilities.CheckForWater(world, blockPos, "FuriousRamsayWaterTank", 4, 1, 0);
            c.WaterInCollector = RebirthUtilities.CheckForWater(world, blockPos, "FuriousRamsayDewCollector", 4, 1, 0);

            TileEntityFarmPlotRebirth farmPlot = world.GetTileEntity(blockPos + Vector3i.down) as TileEntityFarmPlotRebirth;
            if (farmPlot != null)
                c.FarmPlotWater = farmPlot.waterCount;
        }

        if (s_plantConditionCache.Count > MaxConditionCacheEntries)
            s_plantConditionCache.Clear();

        s_plantConditionCache[key] = c;
        return c;
    }

    public static ulong GetPredictedDisplayAccumulatedTicksForDiagnostic(
        WorldBase world,
        Vector3i blockPos,
        int blockType,
        TileEntityPlantGrowingRebirth te,
        ulong stageRate,
        bool growthBlocked)
    {
        if (te == null)
            return 0UL;

        return GetPredictedDisplayAccumulatedTicks(world, blockPos, blockType, te, stageRate, growthBlocked);
    }

    private static ulong GetPredictedDisplayAccumulatedTicks(
        WorldBase world,
        Vector3i blockPos,
        int blockType,
        TileEntityPlantGrowingRebirth te,
        ulong stageRate,
        bool growthBlocked)
    {
        ulong accumulated = te.AccumulatedTicks;
        if (accumulated > stageRate)
            accumulated = stageRate;

        if (world == null || !world.IsRemote())
        {
            if (growthBlocked)
                return accumulated;

            ulong nowTicks = GameTimer.Instance.ticks;
            ulong previous = te.GameTimerTicks;
            ulong elapsed = nowTicks >= previous ? nowTicks - previous : 0UL;
            return AddDisplayElapsed(accumulated, elapsed, stageRate);
        }

        // GameTimer ticks are process-local. A remote client receives the dedicated server's
        // GameTimerTicks value with the tile entity, but its own GameTimer starts from a
        // different epoch. Comparing those values directly can keep elapsed at zero for hours.
        // Pair the authoritative accumulated value with a client-local receipt/display clock.
        long key = MakeKey(blockPos);
        ulong localNow = GameTimer.Instance.ticks;

        bool reset = !s_clientDisplayBaselines.TryGetValue(key, out ClientDisplayBaseline baseline)
            || baseline.BlockType != blockType
            || baseline.StageRateTicks != stageRate
            || baseline.AuthoritativeGameTimerTicks != te.GameTimerTicks
            || baseline.AuthoritativeAccumulatedTicks != te.AccumulatedTicks
            || baseline.GrowthBlocked != growthBlocked
            || localNow < baseline.LocalBaselineTicks;

        if (reset)
        {
            if (s_clientDisplayBaselines.Count >= MaxClientDisplayBaselines)
                s_clientDisplayBaselines.Clear();

            baseline = new ClientDisplayBaseline
            {
                BlockType = blockType,
                StageRateTicks = stageRate,
                AuthoritativeGameTimerTicks = te.GameTimerTicks,
                AuthoritativeAccumulatedTicks = te.AccumulatedTicks,
                LocalBaselineTicks = localNow,
                GrowthBlocked = growthBlocked
            };
            s_clientDisplayBaselines[key] = baseline;
        }

        if (growthBlocked)
            return accumulated;

        ulong localElapsed = localNow >= baseline.LocalBaselineTicks
            ? localNow - baseline.LocalBaselineTicks
            : 0UL;

        return AddDisplayElapsed(baseline.AuthoritativeAccumulatedTicks, localElapsed, stageRate);
    }

    private static ulong AddDisplayElapsed(ulong accumulated, ulong elapsed, ulong stageRate)
    {
        ulong predicted;
        if (ulong.MaxValue - accumulated < elapsed)
            predicted = ulong.MaxValue;
        else
            predicted = accumulated + elapsed;

        if (predicted > stageRate)
            predicted = stageRate;

        return predicted;
    }

    private static string FormatTimer(ulong remainingTicks)
    {
        ulong totalSeconds = (remainingTicks + 19UL) / 20UL;
        ulong minutes = totalSeconds / 60UL;
        ulong seconds = totalSeconds % 60UL;

        string timerText = string.Empty;

        if (minutes > 1UL)
            timerText = " " + minutes + "[-] " + Localization.Get("ttMinutes");
        else if (minutes == 1UL)
            timerText = " 1[-] " + Localization.Get("ttMinute");

        if (seconds > 1UL)
            timerText += " [d6d97e]" + seconds + "[-] " + Localization.Get("ttSeconds");
        else
            timerText += " [d6d97e]" + seconds + "[-] " + Localization.Get("ttSecond");

        return timerText;
    }

    private static ulong GetStageRate(WorldBase world, Vector3i pos)
    {
        int seconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds(world, pos);
        if (seconds < 1)
            seconds = 1;

        return (ulong)seconds * 20UL;
    }

    private static int GetPlantGrowthStageFromBlockName(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return 0;

        if (blockName.IndexOf("3Harvest", StringComparison.OrdinalIgnoreCase) >= 0)
            return 3;

        if (blockName.IndexOf("2", StringComparison.OrdinalIgnoreCase) >= 0)
            return 2;

        if (blockName.IndexOf("1", StringComparison.OrdinalIgnoreCase) >= 0)
            return 1;

        return 0;
    }

    private static string GetFullyGrownCropText(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return string.Empty;

        string descKey = blockName.Replace("Player", "") + "Desc";
        string desc = Localization.Get(descKey);

        if (!string.IsNullOrEmpty(desc) && !string.Equals(desc, descKey, StringComparison.OrdinalIgnoreCase))
            return desc;

        string localized = Localization.Get(blockName);
        if (!string.IsNullOrEmpty(localized) && !string.Equals(localized, blockName, StringComparison.OrdinalIgnoreCase))
            return localized;

        return blockName;
    }

    private static bool IsMushroom(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        return block != null && block.GetBlockName().IndexOf("mushroom", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetBiomeName(Vector3i pos)
    {
        BiomeDefinition biome = GameManager.Instance.World.GetBiome(pos.x, pos.z);
        return biome != null ? biome.m_sBiomeName : string.Empty;
    }

    private static int ResolveFarmPlotMax(BlockValue blockValue, TileEntityFarmPlotRebirth te)
    {
        if (blockValue.Block != null && blockValue.Block.Properties != null && blockValue.Block.Properties.Values.ContainsKey("WaterMax"))
        {
            if (int.TryParse(blockValue.Block.Properties.Values["WaterMax"], out int parsed) && parsed > 0)
                return parsed;
        }

        return te != null && te.waterMax > 0 ? te.waterMax : 6;
    }

    private static int ResolveWaterProviderMax(BlockValue blockValue, TileEntityWaterTankRebirth te)
    {
        if (blockValue.Block != null && blockValue.Block.Properties != null && blockValue.Block.Properties.Values.ContainsKey("MaxWater"))
        {
            if (int.TryParse(blockValue.Block.Properties.Values["MaxWater"], out int parsed) && parsed > 0)
                return parsed;
        }

        return te != null && te.waterMax > 0 ? te.waterMax : 400;
    }


    private static int GetDisplaySecond()
    {
        // GameTimer runs at 20 ticks per gameplay second and respects pause/time scale.
        // This keeps the visible countdown at one-second cadence without increasing
        // the actual crop simulation frequency.
        return (int)(GameTimer.Instance.ticks / 20UL);
    }

    private static bool TryGetHoverTextCache(
        ref CachedHoverText cache,
        long key,
        int worldSeconds,
        int blockType,
        int paramA,
        int paramB,
        int paramC,
        out string text)
    {
        text = null;
        if (s_hoverTraceEnabled)
            return false;

        if (cache.Text == null)
            return false;

        if (cache.Key != key || cache.WorldSeconds != worldSeconds || cache.BlockType != blockType
            || cache.ParamA != paramA || cache.ParamB != paramB || cache.ParamC != paramC)
            return false;

        text = cache.Text;
        return true;
    }

    private static void StoreHoverTextCache(
        ref CachedHoverText cache,
        long key,
        int worldSeconds,
        int blockType,
        int paramA,
        int paramB,
        int paramC,
        string text)
    {
        if (s_hoverTraceEnabled)
            return;

        cache.Key = key;
        cache.WorldSeconds = worldSeconds;
        cache.BlockType = blockType;
        cache.ParamA = paramA;
        cache.ParamB = paramB;
        cache.ParamC = paramC;
        cache.Text = text;
    }

    private static long MakeKey(Vector3i pos)
    {
        unchecked
        {
            return (((long)pos.x & 0xFFFFFL) << 28)
                ^ (((long)pos.z & 0xFFFFFL) << 8)
                ^ ((long)pos.y & 0xFFL);
        }
    }
}
