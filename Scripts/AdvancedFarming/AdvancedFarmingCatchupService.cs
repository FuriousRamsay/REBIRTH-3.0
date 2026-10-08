using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

/// <summary>
/// Ordered catch-up coordinator for Advanced Farming.
///
/// Required order:
/// 1. water providers
/// 2. farm plots
/// 3. plants
///
/// This intentionally keeps UpdateTick as a wake-up only. The actual catch-up ordering lives here.
/// </summary>
public static class AdvancedFarmingCatchupService
{
    private const int AreaRadius = 8;
    private const int MaxProvidersPerArea = 32;
    private const int MaxPlotsPerArea = 64;

    private static readonly Dictionary<long, int> s_lastAreaProcessSecond = new Dictionary<long, int>(256);
    private static readonly Dictionary<long, int> s_lastSharedAreaProcessSecond = new Dictionary<long, int>(256);


    // Diagnostic isolation flags used only by rbafperf isolate. These are intentionally
    // broad subsystem gates so long-window tests can prove whether frame/GC spikes
    // disappear when a specific Advanced Farming path is bypassed. Defaults are all
    // false and the profiler restores the previous state after every run.
    public static bool DiagnosticPauseAllProcessArea;
    public static bool DiagnosticPauseAreaMaintenance;
    public static bool DiagnosticPauseActiveCropProcessing;
    public static bool DiagnosticBypassLightEvaluation;
    public static bool DiagnosticBypassThermalEvaluation;
    public static bool DiagnosticBypassWaterConsumption;
    // Stored in RuntimePolicy because it also gates ModApi/Harmony services outside ProcessArea.

    public struct DiagnosticIsolationState
    {
        public bool PauseAllProcessArea;
        public bool PauseAreaMaintenance;
        public bool PauseActiveCropProcessing;
        public bool BypassLightEvaluation;
        public bool BypassThermalEvaluation;
        public bool BypassWaterConsumption;
        public bool HardDisableAll;
    }

    public static DiagnosticIsolationState CaptureDiagnosticIsolationState()
    {
        DiagnosticIsolationState state = new DiagnosticIsolationState();
        state.PauseAllProcessArea = DiagnosticPauseAllProcessArea;
        state.PauseAreaMaintenance = DiagnosticPauseAreaMaintenance;
        state.PauseActiveCropProcessing = DiagnosticPauseActiveCropProcessing;
        state.BypassLightEvaluation = DiagnosticBypassLightEvaluation;
        state.BypassThermalEvaluation = DiagnosticBypassThermalEvaluation;
        state.BypassWaterConsumption = DiagnosticBypassWaterConsumption;
        state.HardDisableAll = AdvancedFarmingRuntimePolicy.DiagnosticHardDisableAll;
        return state;
    }

    public static void RestoreDiagnosticIsolationState(DiagnosticIsolationState state)
    {
        DiagnosticPauseAllProcessArea = state.PauseAllProcessArea;
        DiagnosticPauseAreaMaintenance = state.PauseAreaMaintenance;
        DiagnosticPauseActiveCropProcessing = state.PauseActiveCropProcessing;
        DiagnosticBypassLightEvaluation = state.BypassLightEvaluation;
        DiagnosticBypassThermalEvaluation = state.BypassThermalEvaluation;
        DiagnosticBypassWaterConsumption = state.BypassWaterConsumption;
        AdvancedFarmingRuntimePolicy.SetDiagnosticHardDisableAll(state.HardDisableAll, "restore isolation state");
    }

    public static void ClearDiagnosticIsolationState()
    {
        DiagnosticPauseAllProcessArea = false;
        DiagnosticPauseAreaMaintenance = false;
        DiagnosticPauseActiveCropProcessing = false;
        DiagnosticBypassLightEvaluation = false;
        DiagnosticBypassThermalEvaluation = false;
        DiagnosticBypassWaterConsumption = false;
        AdvancedFarmingRuntimePolicy.SetDiagnosticHardDisableAll(false, "clear isolation state");
    }

    public static string BuildDiagnosticIsolationStatus()
    {
        return "pauseAll=" + DiagnosticPauseAllProcessArea
            + " pauseMaintenance=" + DiagnosticPauseAreaMaintenance
            + " pauseActive=" + DiagnosticPauseActiveCropProcessing
            + " bypassLight=" + DiagnosticBypassLightEvaluation
            + " bypassThermal=" + DiagnosticBypassThermalEvaluation
            + " bypassWaterConsumption=" + DiagnosticBypassWaterConsumption
            + " hardDisableAll=" + AdvancedFarmingRuntimePolicy.DiagnosticHardDisableAll;
    }

    private const int CatchupDebugMaxPlantDetails = 16;
    private const int CatchupDebugMaxWaterDetails = 16;
    private static bool s_catchupDebugEnabled;
    private static int s_catchupDebugSequence;

    private static readonly object s_catchupWatchLock = new object();
    private static bool s_catchupWatchActive;
    private static bool s_catchupWatchCompleted;
    private static Vector3i s_catchupWatchPos;
    private static string s_catchupWatchLabel;
    private static int s_catchupWatchGrowthSeconds;
    private static int s_catchupWatchWaitSeconds;
    private static int s_catchupWatchStartWorldSeconds;
    private static ulong s_catchupWatchStartGameTimerTicks;
    private static long s_catchupWatchStartUtcTicks;
    private static string s_catchupWatchBeforeBlockName;
    private static ulong s_catchupWatchBeforeAccumulatedTicks;
    private static bool s_catchupWatchBeforeWatered;
    private static ulong s_catchupWatchBeforeGameTimerTicks;
    private static int s_catchupWatchBeforeLastProcessedWorldSeconds;

    public static void ResetSchedulingState()
    {
        s_lastAreaProcessSecond.Clear();
        s_lastSharedAreaProcessSecond.Clear();
    }

    public static string SetCatchupDebug(bool enabled)
    {
        s_catchupDebugEnabled = enabled;
        if (enabled)
            s_catchupDebugSequence = 0;

        return BuildCatchupDebugStatus();
    }

    public static string BuildCatchupDebugStatus()
    {
        return "[AdvancedFarming CatchupDebug] enabled=" + s_catchupDebugEnabled
            + " note=This is a passive logging toggle only. It does not set growth rate, reset crops, backdate timers, force catch-up, or mutate crop state.";
    }

    public static bool CatchupDebugEnabled
    {
        get { return s_catchupDebugEnabled; }
    }

    public static void LogCatchupDebug(string message)
    {
        if (!s_catchupDebugEnabled)
            return;

        s_catchupDebugSequence++;
        Log.Out("[AdvancedFarming CatchupDebug] #" + s_catchupDebugSequence + " " + message);
    }

    private static string SafeBlockName(BlockValue blockValue)
    {
        return blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>";
    }

    private static string SafePlantState(TileEntityPlantGrowingRebirth te)
    {
        if (te == null)
            return "te=null";

        return "accum=" + te.AccumulatedTicks
            + " watered=" + te.bWatered
            + " gameTicks=" + te.GameTimerTicks
            + " lastWorldSeconds=" + te.LastProcessedWorldSeconds;
    }

    private static string FormatStopwatchMs(long elapsedStopwatchTicks)
    {
        double frequency = System.Diagnostics.Stopwatch.Frequency;
        double ms = frequency > 0d ? (double)elapsedStopwatchTicks * 1000d / frequency : 0d;
        return ms.ToString("0.###");
    }

    public static bool ProcessArea(WorldBase world, Vector3i anchorPos)
    {
        if (world == null)
            return false;

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return false;

        if (!AdvancedFarmingRuntimePolicy.Enabled || DiagnosticPauseAllProcessArea)
            return false;

        if (!AdvancedFarmingActiveAreaRegistry.IsPlantProcessable(world, anchorPos))
            return false;

        int now = RebirthUtilities.TotalGameSecondsPassed();
        bool debug = s_catchupDebugEnabled;

        bool sampling = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        long perfStartTicks = 0L;
        long processAreaManagedStartBytes = 0L;
        if (sampling)
        {
            perfStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            processAreaManagedStartBytes = GC.GetTotalMemory(false);
        }

        long maintenanceElapsedTicks = 0L;
        long activeLightEvaluateBeforeTicks = 0L;
        long activeThermalEvaluateBeforeTicks = 0L;
        long activeLightEvaluateElapsedTicks = 0L;
        long activeThermalEvaluateElapsedTicks = 0L;

        int providers = 0;
        int plots = 0;
        int plants = 0;

        TileEntityPlantGrowingRebirth anchorPlantTe = world.GetTileEntity(anchorPos) as TileEntityPlantGrowingRebirth;
        BlockValue anchorBlockValue = world.GetBlock(anchorPos);

        if (sampling)
            AdvancedFarmingPerfSnapshotService.RecordProcessAreaAnchor(anchorBlockValue, anchorPlantTe != null);

        int activeNowSeconds = (int)(GameTimer.Instance.ticks / 20UL);
        long key = MakeAreaKey(anchorPos);

        int minProcessSeconds = AdvancedFarmingRuntimePolicy.GetMinAreaProcessSeconds();
        bool runAreaMaintenance = true;
        if (s_lastAreaProcessSecond.TryGetValue(key, out int last) && activeNowSeconds - last < minProcessSeconds)
            runAreaMaintenance = false;

        if (debug)
            LogCatchupDebug("ProcessArea ENTER anchor=" + anchorPos
                + " nowSeconds=" + now
                + " activeNowSeconds=" + activeNowSeconds
                + " block=" + SafeBlockName(anchorBlockValue)
                + " anchorTe=" + (anchorPlantTe != null)
                + " anchorState=" + SafePlantState(anchorPlantTe)
                + " minProcessSeconds=" + minProcessSeconds
                + " runAreaMaintenance=" + runAreaMaintenance);

        if (DiagnosticPauseAreaMaintenance)
            runAreaMaintenance = false;

        if (runAreaMaintenance)
        {
            s_lastAreaProcessSecond[key] = activeNowSeconds;

            int minY = anchorPos.y - 2;
            int maxY = anchorPos.y + 3;

            long maintenanceStartTicks = 0L;
            long maintenanceManagedStartBytes = 0L;
            if (sampling)
            {
                maintenanceStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                maintenanceManagedStartBytes = GC.GetTotalMemory(false);
            }

            // Phase 1/2 provider discovery is registry-based and now collected in one
            // pass. Water providers are still processed before farm plots so newly
            // collected/rained water is visible to the plant wake that follows.
            List<AdvancedFarmingWaterProviderEntry> providerEntries;
            List<AdvancedFarmingWaterProviderEntry> plotEntries;
            long waterMaintenanceManagedStartBytes = sampling ? GC.GetTotalMemory(false) : 0L;
            AdvancedFarmingWaterProviderRegistry.CollectProvidersAndPlotsInArea(world, anchorPos, AreaRadius, minY, maxY, MaxProvidersPerArea, MaxPlotsPerArea, out providerEntries, out plotEntries);
            if (sampling)
                AdvancedFarmingPerfSnapshotService.RecordManagedMemoryDelta(AdvancedFarmingManagedMemoryPhase.WaterMaintenance, GC.GetTotalMemory(false) - waterMaintenanceManagedStartBytes);

            for (int i = 0; i < providerEntries.Count; i++)
            {
                AdvancedFarmingWaterProviderEntry entry = providerEntries[i];
                if (entry == null || entry.Tank == null)
                    continue;

                ProcessWaterProvider(world, entry.Pos, entry.BlockValue, entry.Tank, now);
                providers++;
            }

            for (int i = 0; i < plotEntries.Count; i++)
            {
                AdvancedFarmingWaterProviderEntry entry = plotEntries[i];
                if (entry == null || entry.Plot == null)
                    continue;

                ProcessFarmPlot(world, entry.Pos, entry.BlockValue, entry.Plot, now);
                plots++;
            }

            if (sampling)
            {
                maintenanceElapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - maintenanceStartTicks;
                AdvancedFarmingPerfSnapshotService.RecordAreaMaintenancePhase(maintenanceElapsedTicks);
                AdvancedFarmingPerfSnapshotService.RecordManagedMemoryDelta(AdvancedFarmingManagedMemoryPhase.AreaMaintenance, GC.GetTotalMemory(false) - maintenanceManagedStartBytes);
            }
        }

        // The crop that woke up must always get a chance to process, but water-provider
        // and rain maintenance must run first so newly collected/rained water is visible
        // to the same wake.
        if (sampling)
        {
            activeLightEvaluateBeforeTicks = AdvancedFarmingPerfSnapshotService.LightEvaluateElapsedStopwatchTicks;
            activeThermalEvaluateBeforeTicks = AdvancedFarmingPerfSnapshotService.ThermalEvaluateElapsedStopwatchTicks;
        }

        if (!DiagnosticPauseActiveCropProcessing && ProcessActiveAnchorPlant(world, anchorPos, now))
            plants++;

        if (sampling)
        {
            activeLightEvaluateElapsedTicks = AdvancedFarmingPerfSnapshotService.LightEvaluateElapsedStopwatchTicks - activeLightEvaluateBeforeTicks;
            activeThermalEvaluateElapsedTicks = AdvancedFarmingPerfSnapshotService.ThermalEvaluateElapsedStopwatchTicks - activeThermalEvaluateBeforeTicks;
        }

        if (sampling)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - perfStartTicks;
            AdvancedFarmingPerfSnapshotService.RecordAreaProcess(elapsedTicks, providers, plots, plants, runAreaMaintenance);
            AdvancedFarmingPerfSnapshotService.RecordManagedMemoryDelta(AdvancedFarmingManagedMemoryPhase.ProcessArea, GC.GetTotalMemory(false) - processAreaManagedStartBytes);
            if (AdvancedFarmingStutterTraceService.ShouldRecordSlow(elapsedTicks))
            {
                long otherElapsedTicks = elapsedTicks - maintenanceElapsedTicks - activeLightEvaluateElapsedTicks - activeThermalEvaluateElapsedTicks;
                if (otherElapsedTicks < 0L)
                    otherElapsedTicks = 0L;

                AdvancedFarmingStutterTraceService.RecordSlowOperation(
                    "processArea",
                    anchorPos,
                    elapsedTicks,
                    "providers=" + providers
                        + " plots=" + plots
                        + " plants=" + plants
                        + " areaMaintenance=" + runAreaMaintenance
                        + " lightMs=" + FormatStopwatchMs(activeLightEvaluateElapsedTicks)
                        + " thermalMs=" + FormatStopwatchMs(activeThermalEvaluateElapsedTicks)
                        + " maintMs=" + FormatStopwatchMs(maintenanceElapsedTicks)
                        + " otherMs=" + FormatStopwatchMs(otherElapsedTicks)
                        + " anchorHasPlantTE=" + (anchorPlantTe != null)
                        + " block=" + (anchorBlockValue.Block != null ? anchorBlockValue.Block.GetBlockName() : "<null>"));
            }
        }

        if (debug)
            LogCatchupDebug("ProcessArea EXIT anchor=" + anchorPos
                + " providers=" + providers
                + " plots=" + plots
                + " plants=" + plants
                + " runAreaMaintenance=" + runAreaMaintenance
                + " anchorBlock=" + SafeBlockName(world.GetBlock(anchorPos))
                + " anchorState=" + SafePlantState(world.GetTileEntity(anchorPos) as TileEntityPlantGrowingRebirth));

#if DEBUG
        if (AdvancedFarmingDebug.Catchup)
            AdvancedFarmingDebug.Log("catchup", "area anchor=" + anchorPos + " providers=" + providers + " plots=" + plots + " plants=" + plants + " areaMaintenance=" + runAreaMaintenance);
#endif

        TryCompleteCatchupWatchFromArea(world, anchorPos);

        return true;
    }


    public static bool ProcessSharedAreaCatchup(WorldBase world, Vector3i anchorPos, string reason)
    {
        return ProcessSharedAreaCatchup(world, anchorPos, reason, false);
    }

    private static bool ProcessSharedAreaCatchup(WorldBase world, Vector3i anchorPos, string reason, bool ignoreThrottle)
    {
        if (world == null)
            return false;

        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return false;

        if (!AdvancedFarmingRuntimePolicy.Enabled || DiagnosticPauseAllProcessArea)
            return false;

        if (!AdvancedFarmingActiveAreaRegistry.IsPlantProcessable(world, anchorPos))
            return false;

        int now = RebirthUtilities.TotalGameSecondsPassed();
        bool debug = s_catchupDebugEnabled;
        long key = MakeAreaKey(anchorPos);

        if (debug)
            LogCatchupDebug("SharedArea ENTER reason=" + (string.IsNullOrEmpty(reason) ? "<none>" : reason)
                + " anchor=" + anchorPos
                + " nowSeconds=" + now
                + " ignoreThrottle=" + ignoreThrottle);

        if (!ignoreThrottle)
        {
            if (s_lastSharedAreaProcessSecond.TryGetValue(key, out int lastShared) && lastShared == now)
            {
                if (debug)
                    LogCatchupDebug("SharedArea SKIP_THROTTLE reason=" + (string.IsNullOrEmpty(reason) ? "<none>" : reason)
                        + " anchor=" + anchorPos
                        + " nowSeconds=" + now
                        + " lastShared=" + lastShared);

                TryCompleteCatchupWatchFromArea(world, anchorPos);
                return false;
            }
        }

        s_lastSharedAreaProcessSecond[key] = now;

        int providers = 0;
        int plots = 0;
        int plants = 0;
        int minY = anchorPos.y - 2;
        int maxY = anchorPos.y + 3;

        if (!DiagnosticPauseAreaMaintenance)
        {
            List<AdvancedFarmingWaterProviderEntry> providerEntries = AdvancedFarmingWaterProviderRegistry.CollectProvidersInArea(world, anchorPos, AreaRadius, minY, maxY, true, false, MaxProvidersPerArea);
            for (int i = 0; i < providerEntries.Count; i++)
            {
                AdvancedFarmingWaterProviderEntry entry = providerEntries[i];
                if (entry == null || entry.Tank == null)
                    continue;

                ProcessWaterProvider(world, entry.Pos, entry.BlockValue, entry.Tank, now);
                providers++;
            }

            List<AdvancedFarmingWaterProviderEntry> plotEntries = AdvancedFarmingWaterProviderRegistry.CollectProvidersInArea(world, anchorPos, AreaRadius, minY, maxY, false, true, MaxPlotsPerArea);
            for (int i = 0; i < plotEntries.Count; i++)
            {
                AdvancedFarmingWaterProviderEntry entry = plotEntries[i];
                if (entry == null || entry.Plot == null)
                    continue;

                ProcessFarmPlot(world, entry.Pos, entry.BlockValue, entry.Plot, now);
                plots++;
            }
        }

        List<PlantCatchupEntry> plantEntries = CollectPlants(world, anchorPos);
        List<WaterCatchupEntry> waterEntries = CollectSharedWater(world, anchorPos);
        if (debug)
            LogCatchupDebug("SharedArea COLLECTED reason=" + (string.IsNullOrEmpty(reason) ? "<none>" : reason)
                + " anchor=" + anchorPos
                + " plantEntries=" + (plantEntries != null ? plantEntries.Count : 0)
                + " waterEntries=" + (waterEntries != null ? waterEntries.Count : 0)
                + " providerRegistry=" + providers
                + " plotRegistry=" + plots);

        if (!DiagnosticPauseActiveCropProcessing)
            plants = ProcessPlantsShared(world, plantEntries, waterEntries, now);

        if (debug)
            LogCatchupDebug("SharedArea EXIT reason=" + (string.IsNullOrEmpty(reason) ? "<none>" : reason)
                + " anchor=" + anchorPos
                + " providers=" + providers
                + " plots=" + plots
                + " plantsProcessed=" + plants);

#if DEBUG
        if (AdvancedFarmingDebug.Catchup)
            AdvancedFarmingDebug.Log("catchup", "shared area reason=" + (string.IsNullOrEmpty(reason) ? "<none>" : reason) + " anchor=" + anchorPos + " providers=" + providers + " plots=" + plots + " plants=" + plants + " ignoreThrottle=" + ignoreThrottle);
#endif

        TryCompleteCatchupWatchFromArea(world, anchorPos);
        return true;
    }

    public static void PumpCatchupWatchForGameUpdate(WorldBase world)
    {
        // v86: the catchupwatch experiment is disabled. Use rbfarming catchupdebug on/off/status.
    }


    private static bool ProcessActiveAnchorPlant(WorldBase world, Vector3i anchorPos, int nowSeconds)
    {
        TileEntityPlantGrowingRebirth te = world.GetTileEntity(anchorPos) as TileEntityPlantGrowingRebirth;
        if (te == null)
            return false;

        BlockValue blockValue = world.GetBlock(anchorPos);
        PlantCatchupEntry plant = new PlantCatchupEntry
        {
            Pos = anchorPos,
            BlockValue = blockValue,
            TileEntity = te,
            IsMushroom = IsMushroom(blockValue)
        };

        return ProcessSingleActivePlant(world, plant, nowSeconds);
    }

    private static bool ProcessSingleActivePlant(WorldBase world, PlantCatchupEntry plant, int nowSeconds)
    {
        if (world == null || plant == null || plant.TileEntity == null)
            return false;

        TileEntityPlantGrowingRebirth te = plant.TileEntity;
        ulong nowTicks = GameTimer.Instance.ticks;
        bool debug = s_catchupDebugEnabled;

        if (debug)
            LogCatchupDebug("ActivePlant ENTER pos=" + plant.Pos
                + " block=" + SafeBlockName(plant.BlockValue)
                + " nowSeconds=" + nowSeconds
                + " nowTicks=" + nowTicks
                + " state=" + SafePlantState(te));

        if (IsFullyGrownPlant(plant.BlockValue))
        {
            if (debug)
                LogCatchupDebug("ActivePlant SKIP pos=" + plant.Pos
                    + " block=" + SafeBlockName(plant.BlockValue)
                    + " reason=fullyGrown");
            return true;
        }

        te.SanitizeTimingBaselines();

        ulong previousTicks = te.GameTimerTicks;
        if (previousTicks <= 0UL)
            previousTicks = nowTicks;

        ulong oldAccumulatedTicks = te.AccumulatedTicks;
        bool oldWatered = te.bWatered;
        ulong oldGameTimerTicks = te.GameTimerTicks;
        int oldLastProcessedWorldSeconds = te.LastProcessedWorldSeconds;

        ulong elapsedTicks = nowTicks >= previousTicks ? nowTicks - previousTicks : 0UL;
        int elapsedSeconds = (int)(elapsedTicks / 20UL);

        if (elapsedSeconds <= 0)
        {
            te.LastProcessedWorldSeconds = nowSeconds;
            if (debug)
                LogCatchupDebug("ActivePlant NO_ELAPSED pos=" + plant.Pos
                    + " previousTicks=" + previousTicks
                    + " nowTicks=" + nowTicks
                    + " elapsedTicks=" + elapsedTicks
                    + " elapsedSeconds=" + elapsedSeconds
                    + " state=" + SafePlantState(te));
            MarkPlantChanged(plant.Pos, plant.BlockValue, te, oldAccumulatedTicks, oldWatered, oldGameTimerTicks, oldLastProcessedWorldSeconds);
            return true;
        }

        int stageSeconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds(world, plant.Pos);
        if (stageSeconds < 1)
            stageSeconds = 1;

        if (elapsedSeconds > AdvancedFarmingRuntimePolicy.GetActiveDirectMaxElapsedSeconds())
            elapsedSeconds = AdvancedFarmingRuntimePolicy.GetActiveDirectMaxElapsedSeconds();

        if (!PreparePlantActiveDirect(world, plant, stageSeconds))
        {
            te.GameTimerTicks = nowTicks;
            te.LastProcessedWorldSeconds = nowSeconds;
            if (debug)
                LogCatchupDebug("ActivePlant PREPARE_FAIL pos=" + plant.Pos
                    + " block=" + SafeBlockName(plant.BlockValue)
                    + " elapsedSeconds=" + elapsedSeconds
                    + " stageSeconds=" + stageSeconds
                    + " state=" + SafePlantState(te));
            MarkPlantChanged(plant.Pos, plant.BlockValue, te, oldAccumulatedTicks, oldWatered, oldGameTimerTicks, oldLastProcessedWorldSeconds);
            return true;
        }

        int secondsApplied = 0;
        for (int second = 0; second < elapsedSeconds; second++)
        {
            if (!ProcessPlantSingleActiveStep(world, plant))
                break;

            secondsApplied++;
        }

        TileEntityPlantGrowingRebirth finalTe = plant.TileEntity;
        if (finalTe == null)
            return true;

        if (secondsApplied > 0)
        {
            ulong appliedTicks = (ulong)secondsApplied * 20UL;
            finalTe.GameTimerTicks = previousTicks + appliedTicks;
            finalTe.LastProcessedWorldSeconds = nowSeconds;
        }
        else
        {
            finalTe.GameTimerTicks = nowTicks;
            finalTe.LastProcessedWorldSeconds = nowSeconds;
        }

        if (debug)
            LogCatchupDebug("ActivePlant EXIT pos=" + plant.Pos
                + " block=" + SafeBlockName(world.GetBlock(plant.Pos))
                + " elapsedSeconds=" + elapsedSeconds
                + " secondsApplied=" + secondsApplied
                + " previousTicks=" + previousTicks
                + " nowTicks=" + nowTicks
                + " oldAccum=" + oldAccumulatedTicks
                + " oldWatered=" + oldWatered
                + " oldGameTicks=" + oldGameTimerTicks
                + " oldLastWorldSeconds=" + oldLastProcessedWorldSeconds
                + " newState=" + SafePlantState(finalTe));

        if (finalTe == te)
        {
            MarkPlantChanged(plant.Pos, plant.BlockValue, finalTe, oldAccumulatedTicks, oldWatered, oldGameTimerTicks, oldLastProcessedWorldSeconds);
        }
        else
        {
            finalTe.setModified();
            AdvancedFarmingSyncService.SendPlant(plant.Pos, plant.BlockValue.type, finalTe.AccumulatedTicks, finalTe.bWatered);
        }
        return true;
    }

    private static bool ProcessPlantSingleActiveStep(WorldBase world, PlantCatchupEntry plant)
    {
        TileEntityPlantGrowingRebirth te = plant.TileEntity;
        if (te == null)
            return false;

        if (IsFullyGrownPlant(plant.BlockValue))
            return true;

        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;

        if (advancedFarming && !plant.IsMushroom && !te.bWatered)
        {
            if (!DiagnosticBypassWaterConsumption)
            {
                if (!RebirthUtilities.TryConsumePlantWater(world, plant.Pos, plant.Depletion))
                    return false;
            }

            te.bWatered = true;
        }

        te.AccumulatedTicks += 20UL;

        if (te.AccumulatedTicks >= plant.StageRateTicks)
        {
            string oldBlockName = SafeBlockName(plant.BlockValue);
            if (!TryAdvanceToNextStage(world, plant.Pos, plant.BlockValue, te, out BlockValue nextValue, out TileEntityPlantGrowingRebirth nextTe))
                return false;

            plant.BlockValue = nextValue;
            plant.TileEntity = nextTe;
            plant.IsMushroom = IsMushroom(nextValue);
            if (s_catchupDebugEnabled)
                LogCatchupDebug("ActivePlant ADVANCE pos=" + plant.Pos
                    + " from=" + oldBlockName
                    + " to=" + SafeBlockName(plant.BlockValue));
        }

        return true;
    }


    private static void MarkPlantChanged(Vector3i pos, BlockValue blockValue, TileEntityPlantGrowingRebirth te, ulong oldAccumulatedTicks, bool oldWatered, ulong oldGameTimerTicks, int oldLastProcessedWorldSeconds)
    {
        if (te == null)
            return;

        if (te.AccumulatedTicks == oldAccumulatedTicks
            && te.bWatered == oldWatered
            && te.GameTimerTicks == oldGameTimerTicks
            && te.LastProcessedWorldSeconds == oldLastProcessedWorldSeconds)
        {
            return;
        }

        bool wateredChanged = te.bWatered != oldWatered;
        bool progressChanged = te.AccumulatedTicks != oldAccumulatedTicks;
        bool timingOnlyChanged = !wateredChanged && !progressChanged;

        // Do not dirty/save/sync every active second just because accumulated ticks or timing baselines moved.
        // Persist progress in coarse buckets, persist meaningful state flips immediately, and persist
        // timing-only blocked baselines only occasionally; the load sanitizer already handles stale baselines.
        bool crossedSaveBucket = progressChanged && ((te.AccumulatedTicks / 400UL) != (oldAccumulatedTicks / 400UL));
        bool crossedTimingBucket = timingOnlyChanged && ((te.LastProcessedWorldSeconds / 300) != (oldLastProcessedWorldSeconds / 300));
        bool shouldMarkModified = wateredChanged || crossedSaveBucket || crossedTimingBucket;

        if (shouldMarkModified)
            te.setModified();

        // Clients can predict the visible countdown locally. Only broadcast meaningful plant
        // resource-state changes, not every accumulated-tick increment.
        if (wateredChanged)
            AdvancedFarmingSyncService.SendPlant(pos, blockValue.type, te.AccumulatedTicks, te.bWatered);
    }

    private static long MakeAreaKey(Vector3i pos)
    {
        unchecked
        {
            long cx = (long)(pos.x >> 4) & 0xFFFFFL;
            long cz = (long)(pos.z >> 4) & 0xFFFFFL;
            long cy = (long)(pos.y >> 4) & 0xFFL;
            return (cx << 28) ^ (cz << 8) ^ cy;
        }
    }

    public static void ProcessWaterProvider(WorldBase world, Vector3i pos, BlockValue blockValue, TileEntityWaterTankRebirth te, int now)
    {
        if (te == null)
            return;

        bool debug = s_catchupDebugEnabled;
        int beforeWater = te.waterCount;
        int beforeTimeLapsed = te.timeLapsed;
        int beforeLastProcessed = te.LastProcessedWorldSeconds;

        AdvancedFarmingWaterProviderRegistry.RegisterKnownProvider(world, pos, te, blockValue);

        int max = ResolveWaterProviderMax(blockValue, te);
        te.waterMax = max;

        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;
        bool isDewCollector = !string.IsNullOrEmpty(blockName)
            && blockName.IndexOf("DewCollector", StringComparison.OrdinalIgnoreCase) >= 0;

        int previous = te.timeLapsed > 0 ? te.timeLapsed : te.LastProcessedWorldSeconds;
        if (previous <= 0 || previous > now)
        {
            te.timeLapsed = now;
            te.LastProcessedWorldSeconds = now;

            if (debug)
                LogCatchupDebug("Provider BASELINE_RESET pos=" + pos
                    + " block=" + blockName
                    + " previous=" + previous
                    + " now=" + now
                    + " reason=" + (previous <= 0 ? "missingBaseline" : "futureBaseline")
                    + " water=" + te.waterCount
                    + "/" + max);
            return;
        }

        // Manual water tanks do not generate water. Keep their timing baseline current so
        // converting/replacing a block cannot inherit a large stale dew-collection backlog.
        if (!isDewCollector)
        {
            te.timeLapsed = now;
            te.LastProcessedWorldSeconds = now;

            if (debug)
                LogCatchupDebug("Provider SKIP pos=" + pos
                    + " block=" + blockName
                    + " reason=notDewCollector"
                    + " previous=" + previous
                    + " now=" + now
                    + " waterBefore=" + beforeWater
                    + " waterAfter=" + te.waterCount
                    + " max=" + max);
            return;
        }

        // A full collector must not build a hidden production backlog. If water is later
        // removed, collection starts a fresh cycle from that moment.
        if (te.waterCount >= max)
        {
            te.timeLapsed = now;
            te.LastProcessedWorldSeconds = now;

            if (debug)
                LogCatchupDebug("Provider SKIP pos=" + pos
                    + " block=" + blockName
                    + " reason=alreadyFull"
                    + " previous=" + previous
                    + " now=" + now
                    + " waterBefore=" + beforeWater
                    + " waterAfter=" + te.waterCount
                    + " max=" + max);
            return;
        }

        int elapsed = now - previous;

        int cycleRealSeconds = 150;
        if (blockValue.Block != null && blockValue.Block.Properties != null && blockValue.Block.Properties.Values.ContainsKey("TickRate"))
        {
            if (int.TryParse(blockValue.Block.Properties.Values["TickRate"], out int parsedTicks) && parsedTicks > 0)
                cycleRealSeconds = Math.Max(1, parsedTicks / 20);
        }

        int worldTimeIncrementPerRealSecond = GameStats.GetInt(EnumGameStats.TimeOfDayIncPerSec);
        if (worldTimeIncrementPerRealSecond <= 0)
        {
            if (debug)
                LogCatchupDebug("Provider SKIP pos=" + pos
                    + " block=" + blockName
                    + " reason=worldTimePaused"
                    + " previous=" + previous
                    + " now=" + now
                    + " elapsedStoredSeconds=" + elapsed
                    + " cycleRealSeconds=" + cycleRealSeconds);
            return;
        }

        // TotalGameSecondsPassed stores worldTime / 20, not real seconds. Compare in raw
        // world-time units so fractional stored units are not lost on long day lengths.
        // The old implementation compared elapsed stored units directly with 150. On the
        // default day length, 150 real seconds advances only about 45 stored units, so no
        // collection cycle could complete.
        ulong nowWorldTime = world.GetWorldTime();
        ulong previousWorldTime = (ulong)previous * 20UL;
        ulong cycleWorldTimeUnits = (ulong)cycleRealSeconds * (ulong)worldTimeIncrementPerRealSecond;
        ulong elapsedWorldTimeUnits = nowWorldTime >= previousWorldTime
            ? nowWorldTime - previousWorldTime
            : 0UL;

        if (elapsedWorldTimeUnits == 0UL)
        {
            if (debug)
                LogCatchupDebug("Provider SKIP pos=" + pos
                    + " block=" + blockName
                    + " reason=noElapsed"
                    + " previous=" + previous
                    + " now=" + now
                    + " waterBefore=" + beforeWater
                    + " waterAfter=" + te.waterCount
                    + " max=" + max);
            return;
        }

        ulong completedCycles = elapsedWorldTimeUnits / cycleWorldTimeUnits;
        if (completedCycles == 0UL)
        {
            // Preserve the original baseline. ProcessWaterProvider is also called by the
            // bounded area-maintenance path, often every few active seconds. Advancing the
            // baseline here would discard partial progress forever.
            if (debug)
                LogCatchupDebug("Provider WAIT pos=" + pos
                    + " block=" + blockName
                    + " previous=" + previous
                    + " now=" + now
                    + " elapsedStoredSeconds=" + elapsed
                    + " elapsedWorldTimeUnits=" + elapsedWorldTimeUnits
                    + " cycleRealSeconds=" + cycleRealSeconds
                    + " cycleWorldTimeUnits=" + cycleWorldTimeUnits
                    + " worldTimeIncrementPerRealSecond=" + worldTimeIncrementPerRealSecond
                    + " water=" + te.waterCount
                    + "/" + max);
            return;
        }

        int iterations = (int)Math.Min(completedCycles, 512UL);

        // Consume only complete collection cycles and preserve any remainder for the next
        // call. This also ensures failed chance rolls cannot be rerolled repeatedly after a
        // save/reload or a nearby crop wake.
        ulong processedThroughWorldTime = previousWorldTime + (ulong)iterations * cycleWorldTimeUnits;
        if (processedThroughWorldTime > nowWorldTime)
            processedThroughWorldTime = nowWorldTime;

        int processedThrough = (int)Math.Min((ulong)int.MaxValue, processedThroughWorldTime / 20UL);
        te.timeLapsed = processedThrough;
        te.LastProcessedWorldSeconds = processedThrough;

        BiomeDefinition biome = null;
        if (!world.IsEditor())
            biome = world.ChunkCache.ChunkProvider.GetBiomeProvider().GetBiomeAt(pos.x, pos.z);

        string biomeName = biome != null ? biome.m_sBiomeName : string.Empty;
        AdvancedFarmingRuleSet rule = AdvancedFarmingRuleResolver.Resolve(biomeName);
        int chance = (rule.DewChanceMin + rule.DewChanceMax) / 2;

        int gained = 0;
        int firstCycleIndex = (int)((previousWorldTime / cycleWorldTimeUnits) & 0x7FFFFFFFUL);
        for (int i = 0; i < iterations && te.waterCount + gained < max; i++)
        {
            int roll = AdvancedFarmingRuleResolver.DeterministicRollPercent(pos, firstCycleIndex + i, 27183);
            if (roll <= chance)
                gained++;
        }

        if (gained > 0)
        {
            te.waterCount += gained;
            if (te.waterCount > max)
                te.waterCount = max;

            AdvancedFarmingSyncService.SendWaterTank(pos, te.waterCount);
        }

        // Persist the consumed cycle baseline even when all deterministic chance rolls fail;
        // otherwise those same cycles would be replayed after reload.
        te.setModified();

        if (debug)
            LogCatchupDebug("Provider " + (gained > 0 ? "APPLY" : "NO_GAIN")
                + " pos=" + pos
                + " block=" + blockName
                + " biome=" + biomeName
                + " previous=" + previous
                + " now=" + now
                + " processedThrough=" + processedThrough
                + " elapsedStoredSeconds=" + elapsed
                + " cycleRealSeconds=" + cycleRealSeconds
                + " cycleWorldTimeUnits=" + cycleWorldTimeUnits
                + " worldTimeIncrementPerRealSecond=" + worldTimeIncrementPerRealSecond
                + " iterations=" + iterations
                + " chance=" + chance
                + " gained=" + gained
                + " waterBefore=" + beforeWater
                + " waterAfter=" + te.waterCount
                + " max=" + max
                + " timeLapsedBefore=" + beforeTimeLapsed
                + " lastProcessedBefore=" + beforeLastProcessed);

#if DEBUG
        if (AdvancedFarmingDebug.Catchup || AdvancedFarmingDebug.Water)
            AdvancedFarmingDebug.Log("catchup", "provider " + blockName + " pos=" + pos + " elapsedStoredSeconds=" + elapsed + " cycles=" + iterations + " gained=" + gained + " water=" + te.waterCount + "/" + max);
#endif
    }

    public static void ProcessFarmPlot(WorldBase world, Vector3i pos, BlockValue blockValue, TileEntityFarmPlotRebirth te, int now)
    {
        if (world == null || te == null || !te.FarmingActivated)
            return;

        bool debug = s_catchupDebugEnabled;
        int beforeWater = te.waterCount;
        ulong beforeExposure = te.RainExposureScaledWorldTimeUnits;
        int beforeLastProcessed = te.LastProcessedWorldSeconds;
        ulong beforeLastRainSample = te.LastRainSampleWorldTime;
        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : string.Empty;

        AdvancedFarmingWaterProviderRegistry.RegisterKnownProvider(world, pos, te, blockValue);

        int max = ResolveFarmPlotMax(blockValue, te);
        te.waterMax = max;

        World authoritativeWorld = world as World;
        if (authoritativeWorld == null)
            return;

        ulong nowWorldTime = authoritativeWorld.GetWorldTime();
        if (te.LastRainSampleWorldTime == 0UL || te.LastRainSampleWorldTime > nowWorldTime)
        {
            te.LastRainSampleWorldTime = nowWorldTime;
            te.LastProcessedWorldSeconds = now;
            return;
        }

        ulong elapsedWorldTimeUnits = nowWorldTime - te.LastRainSampleWorldTime;
        te.LastRainSampleWorldTime = nowWorldTime;
        te.LastProcessedWorldSeconds = now;

        // Crop wakes and multiple players may reach the same plot repeatedly during one
        // world-time unit. The baseline has already been refreshed, so avoid all weather
        // and height-map work when there is no elapsed exposure to integrate.
        if (elapsedWorldTimeUnits == 0UL)
            return;

        // Keep the sample baseline current while full, but do not evaluate weather until
        // water has been consumed. Any legacy/partial backlog is discarded once.
        if (te.waterCount >= max)
        {
            if (te.RainExposureScaledWorldTimeUnits != 0UL)
            {
                te.RainExposureScaledWorldTimeUnits = 0UL;
                te.setModified();
            }
            return;
        }

        int worldTimeIncrementPerRealSecond = GameStats.GetInt(EnumGameStats.TimeOfDayIncPerSec);
        int rainCollectionSeconds = ResolveFarmPlotRainCollectionSeconds(blockValue);
        ulong collectionThresholdScaled = worldTimeIncrementPerRealSecond > 0
            ? (ulong)rainCollectionSeconds * (ulong)worldTimeIncrementPerRealSecond * 1000UL
            : 0UL;

        float rainfall = ResolveRainfallAtPosition(authoritativeWorld, pos);
        bool openSky = IsOpenSkyAboveLoadedChunk(world, pos + Vector3i.up);
        bool trustworthyElapsed = IsTrustworthyRainSampleElapsed(blockValue, elapsedWorldTimeUnits, worldTimeIncrementPerRealSecond);
        int gained = 0;

        if (rainfall > 0f && openSky && trustworthyElapsed && collectionThresholdScaled > 0UL)
        {
            int rainfallScale = (int)Math.Round(Utils.FastClamp01(rainfall) * 1000f);
            if (rainfallScale > 0 && elapsedWorldTimeUnits > 0UL)
            {
                ulong addedExposure;
                if (elapsedWorldTimeUnits > ulong.MaxValue / (ulong)rainfallScale)
                    addedExposure = ulong.MaxValue;
                else
                    addedExposure = elapsedWorldTimeUnits * (ulong)rainfallScale;

                ulong newExposure = te.RainExposureScaledWorldTimeUnits;
                if (ulong.MaxValue - newExposure < addedExposure)
                    newExposure = ulong.MaxValue;
                else
                    newExposure += addedExposure;

                while (newExposure >= collectionThresholdScaled && te.waterCount + gained < max)
                {
                    newExposure -= collectionThresholdScaled;
                    gained++;
                }

                te.RainExposureScaledWorldTimeUnits = te.waterCount + gained >= max ? 0UL : newExposure;
            }
        }

        if (gained > 0)
        {
            te.waterCount += gained;
            if (te.waterCount > max)
                te.waterCount = max;
        }

        bool waterChanged = te.waterCount != beforeWater;
        bool exposureChanged = te.RainExposureScaledWorldTimeUnits != beforeExposure;
        bool exposurePersistenceBucketChanged = false;
        if (exposureChanged && collectionThresholdScaled > 0UL)
        {
            ulong persistenceBucket = Math.Max(1UL, collectionThresholdScaled / 2UL);
            exposurePersistenceBucketChanged = beforeExposure / persistenceBucket
                != te.RainExposureScaledWorldTimeUnits / persistenceBucket;
        }

        if (waterChanged || exposurePersistenceBucketChanged)
            te.setModified();

        if (waterChanged)
            AdvancedFarmingSyncService.SendFarmPlot(pos, te.waterCount);

        if (debug)
            LogCatchupDebug("FarmPlot RAIN pos=" + pos
                + " block=" + blockName
                + " rainfall=" + rainfall.ToString("0.###")
                + " openSky=" + openSky
                + " trustworthyElapsed=" + trustworthyElapsed
                + " elapsedWorldTimeUnits=" + elapsedWorldTimeUnits
                + " worldTimeIncrementPerRealSecond=" + worldTimeIncrementPerRealSecond
                + " rainCollectionSeconds=" + rainCollectionSeconds
                + " thresholdScaled=" + collectionThresholdScaled
                + " exposureBefore=" + beforeExposure
                + " exposureAfter=" + te.RainExposureScaledWorldTimeUnits
                + " gained=" + gained
                + " waterBefore=" + beforeWater
                + " waterAfter=" + te.waterCount
                + " max=" + max
                + " lastProcessedBefore=" + beforeLastProcessed
                + " lastRainSampleBefore=" + beforeLastRainSample);

#if DEBUG
        if (AdvancedFarmingDebug.Catchup)
            AdvancedFarmingDebug.Log("catchup", "farm plot pos=" + pos + " rainfall=" + rainfall.ToString("0.###") + " openSky=" + openSky + " gained=" + gained + " water=" + te.waterCount + "/" + max);
#endif
    }

    private static int ResolveFarmPlotRainCollectionSeconds(BlockValue blockValue)
    {
        const int DefaultSeconds = 600;
        if (blockValue.Block == null || blockValue.Block.Properties == null)
            return DefaultSeconds;

        if (blockValue.Block.Properties.Values.ContainsKey("RainCollectionSeconds")
            && int.TryParse(blockValue.Block.Properties.Values["RainCollectionSeconds"], out int parsed)
            && parsed > 0)
        {
            return parsed;
        }

        BlockFarmPlotRebirth farmPlot = blockValue.Block as BlockFarmPlotRebirth;
        return farmPlot != null && farmPlot.rainCollectionSeconds > 0
            ? farmPlot.rainCollectionSeconds
            : DefaultSeconds;
    }

    private static float ResolveRainfallAtPosition(World world, Vector3i pos)
    {
        if (world == null || WeatherManager.Instance == null)
            return 0f;

        if (WeatherManager.forceRain >= 0f)
            return Utils.FastClamp01(WeatherManager.forceRain);

        BiomeDefinition biome = world.GetBiome(pos.x, pos.z);
        if (biome != null)
        {
            WeatherManager.BiomeWeather biomeWeather = WeatherManager.Instance.FindBiomeWeather(biome.m_BiomeType);
            if (biomeWeather != null)
                return Utils.FastClamp01(biomeWeather.rainParam.value);
        }

        return Utils.FastClamp01(WeatherManager.Instance.GetCurrentRainfallPercent());
    }

    private static bool IsOpenSkyAboveLoadedChunk(WorldBase world, Vector3i pos)
    {
        if (world == null)
            return false;

        IChunk chunk = world.GetChunkSync(World.toChunkXZ(pos.x), World.toChunkXZ(pos.z));
        return chunk != null
            && pos.y >= chunk.GetHeight(World.toBlockXZ(pos.x), World.toBlockXZ(pos.z));
    }

    private static bool IsTrustworthyRainSampleElapsed(BlockValue blockValue, ulong elapsedWorldTimeUnits, int worldTimeIncrementPerRealSecond)
    {
        if (elapsedWorldTimeUnits == 0UL || worldTimeIncrementPerRealSecond <= 0)
            return elapsedWorldTimeUnits == 0UL;

        // Farm plots normally wake every 150-180 real seconds. Reject larger gaps because
        // weather history is not persisted; treating a long unloaded gap as current rain
        // would violate the requirement that water is gained only while rain actually fell.
        int maxTrustedRealSeconds = 240;
        if (blockValue.Block != null && blockValue.Block.Properties != null
            && blockValue.Block.Properties.Values.ContainsKey("TickRate")
            && int.TryParse(blockValue.Block.Properties.Values["TickRate"], out int parsedTicks)
            && parsedTicks > 0)
        {
            maxTrustedRealSeconds = Math.Max(maxTrustedRealSeconds, parsedTicks / 20 + 90);
        }

        ulong maxTrustedWorldTimeUnits = (ulong)maxTrustedRealSeconds * (ulong)worldTimeIncrementPerRealSecond;
        return elapsedWorldTimeUnits <= maxTrustedWorldTimeUnits;
    }


    private sealed class PlantCatchupEntry
    {
        public Vector3i Pos;
        public BlockValue BlockValue;
        public TileEntityPlantGrowingRebirth TileEntity;
        public bool IsMushroom;
        public bool Halted;
        public bool Deferred;
        public bool Dirty;
        public int ValidSecondsRemaining;
        public float Temperature;
        public int Depletion;
        public ulong StageRateTicks;
    }

    private sealed class WaterCatchupEntry
    {
        public Vector3i Pos;
        public TileEntityWaterTankRebirth Tank;
        public TileEntityFarmPlotRebirth Plot;
        public int OriginalWater;
        public int Water
        {
            get
            {
                if (Tank != null)
                    return Tank.waterCount;
                return Plot != null ? Plot.waterCount : 0;
            }
            set
            {
                if (Tank != null)
                    Tank.waterCount = value;
                if (Plot != null)
                    Plot.waterCount = value;
            }
        }
    }

    private static List<PlantCatchupEntry> CollectPlants(WorldBase world, Vector3i anchorPos)
    {
        List<PlantCatchupEntry> result = new List<PlantCatchupEntry>();
        int plantBudget = AdvancedFarmingRuntimePolicy.MaxCatchupPlantsPerWake;
        if (plantBudget < 1)
            plantBudget = 1;

        for (int x = anchorPos.x - AreaRadius; x <= anchorPos.x + AreaRadius && result.Count < plantBudget; x++)
        {
            for (int z = anchorPos.z - AreaRadius; z <= anchorPos.z + AreaRadius && result.Count < plantBudget; z++)
            {
                for (int y = anchorPos.y - 1; y <= anchorPos.y + 4 && result.Count < plantBudget; y++)
                {
                    Vector3i pos = new Vector3i(x, y, z);
                    TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
                    if (te == null)
                        continue;

                    BlockValue blockValue = world.GetBlock(pos);
                    if (AdvancedFarmingActiveAreaRegistry.IsFullyGrownCrop(world, pos))
                        continue;

                    result.Add(new PlantCatchupEntry
                    {
                        Pos = pos,
                        BlockValue = blockValue,
                        TileEntity = te,
                        IsMushroom = IsMushroom(blockValue)
                    });
                }
            }
        }

        return result;
    }

    private static List<WaterCatchupEntry> CollectSharedWater(WorldBase world, Vector3i anchorPos)
    {
        List<WaterCatchupEntry> result = new List<WaterCatchupEntry>();
        if (world == null)
            return result;

        List<AdvancedFarmingWaterProviderEntry> providers = AdvancedFarmingWaterProviderRegistry.CollectProvidersInArea(world, anchorPos, AreaRadius, anchorPos.y - 2, anchorPos.y + 3, true, true, -1);
        for (int i = 0; i < providers.Count; i++)
        {
            AdvancedFarmingWaterProviderEntry provider = providers[i];
            if (provider == null)
                continue;

            if (provider.Tank != null)
            {
                result.Add(new WaterCatchupEntry { Pos = provider.Pos, Tank = provider.Tank, OriginalWater = provider.Tank.waterCount });
                continue;
            }

            if (provider.Plot != null)
                result.Add(new WaterCatchupEntry { Pos = provider.Pos, Plot = provider.Plot, OriginalWater = provider.Plot.waterCount });
        }

        return result;
    }


    private static int ProcessPlantsActiveDirect(WorldBase world, List<PlantCatchupEntry> plants, List<WaterCatchupEntry> waterEntries, int nowSeconds)
    {
        if (plants == null || plants.Count == 0)
            return 0;

        ulong nowTicks = GameTimer.Instance.ticks;
        int processed = 0;

        for (int i = 0; i < plants.Count; i++)
        {
            PlantCatchupEntry plant = plants[i];
            if (plant == null || plant.TileEntity == null)
                continue;

            int stageSeconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds(world, plant.Pos);
            if (stageSeconds < 1) stageSeconds = 1;
            TileEntityPlantGrowingRebirth te = plant.TileEntity;

            if (IsFullyGrownPlant(plant.BlockValue))
            {
                // Fully grown harvest-stage crops are inert until harvested.
                // Do not set modified, do not sync, do not consume water, and do not reset timing.
                processed++;
                continue;
            }

            ulong previousTicks = te.GameTimerTicks;
            if (previousTicks <= 0UL)
                previousTicks = nowTicks;

            ulong elapsedTicks = nowTicks >= previousTicks ? nowTicks - previousTicks : 0UL;
            int elapsedSeconds = (int)(elapsedTicks / 20UL);

            if (elapsedSeconds <= 0)
            {
                // Do not move GameTimerTicks here. Moving it before a full second elapsed
                // discards fractional ticks every wake and makes active growth crawl.
                te.LastProcessedWorldSeconds = nowSeconds;
                processed++;
                continue;
            }

            if (elapsedSeconds > AdvancedFarmingRuntimePolicy.GetActiveDirectMaxElapsedSeconds())
                elapsedSeconds = AdvancedFarmingRuntimePolicy.GetActiveDirectMaxElapsedSeconds();

            if (!PreparePlantActiveDirect(world, plant, stageSeconds))
            {
                // Blocked active time should not bank. When invalid, move the baseline to now.
                te.GameTimerTicks = nowTicks;
                te.LastProcessedWorldSeconds = nowSeconds;
                te.setModified();
                AdvancedFarmingSyncService.SendPlant(plant.Pos, plant.BlockValue.type, te.AccumulatedTicks, te.bWatered);
                processed++;
                continue;
            }

            int secondsApplied = 0;
            for (int second = 0; second < elapsedSeconds; second++)
            {
                if (!ProcessPlantSharedStep(world, plant, waterEntries))
                    break;

                secondsApplied++;
            }

            // ProcessPlantSharedStep may have committed a stage replacement. Continue through
            // the new tile entity rather than writing timing/sync state into the destroyed one.
            te = plant.TileEntity;
            if (te == null)
            {
                processed++;
                continue;
            }

            if (secondsApplied > 0)
            {
                ulong appliedTicks = (ulong)secondsApplied * 20UL;
                te.GameTimerTicks = previousTicks + appliedTicks;
                te.LastProcessedWorldSeconds = nowSeconds;
                te.setModified();
                AdvancedFarmingSyncService.SendPlant(plant.Pos, plant.BlockValue.type, te.AccumulatedTicks, te.bWatered);
            }
            else
            {
                // No growth was applied because water or another per-cycle requirement failed.
                // Discard this blocked interval so it cannot be paid out later.
                te.GameTimerTicks = nowTicks;
                te.LastProcessedWorldSeconds = nowSeconds;
                te.setModified();
                AdvancedFarmingSyncService.SendPlant(plant.Pos, plant.BlockValue.type, te.AccumulatedTicks, te.bWatered);
            }

            processed++;
        }

        CommitSharedWater(waterEntries);
        return processed;
    }

    private static bool PreparePlantActiveDirect(WorldBase world, PlantCatchupEntry plant, int stageSeconds)
    {
        if (world == null || plant == null || plant.TileEntity == null)
            return false;

        if (IsFullyGrownPlant(plant.BlockValue))
            return false;

        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;
        string biomeName = GetBiomeName(plant.Pos);
        if (string.Equals(biomeName, "wasteland", StringComparison.OrdinalIgnoreCase))
            return false;

        bool isNight = !GameManager.Instance.World.IsDaytime();
        bool sampling = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        const int RequiredActiveLight = 3;

        bool hasSunLight = false;
        bool hasBlockLight = false;
        bool hasOpenSky = false;
        bool usedBlockLightShortcut = false;

        if (DiagnosticBypassLightEvaluation)
        {
            hasSunLight = true;
            hasOpenSky = true;
            hasBlockLight = true;
            usedBlockLightShortcut = true;
            AdvancedFarmingPerfSnapshotService.RecordActiveBlockLightPrecheck(true);
        }
        else if (advancedFarming && !plant.IsMushroom)
        {
            byte currentBlockLight;
            bool currentBlockLightPass = AdvancedFarmingLightService.TryGetCurrentBlockLight(world, plant.Pos, out currentBlockLight)
                && currentBlockLight >= RequiredActiveLight;
            AdvancedFarmingPerfSnapshotService.RecordActiveBlockLightPrecheck(currentBlockLightPass);

            if (currentBlockLightPass)
            {
                hasBlockLight = true;
                usedBlockLightShortcut = true;
            }
        }

        if (!usedBlockLightShortcut)
        {
            long lightStartTicks = sampling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            long lightManagedStartBytes = sampling ? GC.GetTotalMemory(false) : 0L;
            AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
                world,
                plant.Pos,
                RequiredActiveLight,
                RequiredActiveLight,
                plant.IsMushroom,
                advancedFarming);
            if (sampling)
            {
                AdvancedFarmingPerfSnapshotService.RecordLightEvaluate(System.Diagnostics.Stopwatch.GetTimestamp() - lightStartTicks);
                AdvancedFarmingPerfSnapshotService.RecordManagedMemoryDelta(AdvancedFarmingManagedMemoryPhase.LightEvaluate, GC.GetTotalMemory(false) - lightManagedStartBytes);
            }

            hasSunLight = light.HasSunLight;
            hasBlockLight = light.HasBlockLight;
            hasOpenSky = light.HasOpenSky;
        }

        bool lightPass = (((hasSunLight || hasOpenSky) && !isNight) || hasBlockLight || plant.IsMushroom);
        if (plant.TileEntity.SetAuthoritativeLightPass(lightPass))
            plant.TileEntity.setModified();
        if (!lightPass)
            return false;

        if (DiagnosticBypassThermalEvaluation)
        {
            plant.Temperature = 70f;
        }
        else
        {
            long thermalStartTicks = sampling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            long thermalManagedStartBytes = sampling ? GC.GetTotalMemory(false) : 0L;
            plant.Temperature = AdvancedFarmingTemperatureService.Evaluate(world, plant.Pos, hasSunLight || hasOpenSky, advancedFarming).Temperature;
            if (sampling)
            {
                AdvancedFarmingPerfSnapshotService.RecordThermalEvaluate(System.Diagnostics.Stopwatch.GetTimestamp() - thermalStartTicks);
                AdvancedFarmingPerfSnapshotService.RecordManagedMemoryDelta(AdvancedFarmingManagedMemoryPhase.ThermalEvaluate, GC.GetTotalMemory(false) - thermalManagedStartBytes);
            }
        }
        if (advancedFarming && !plant.IsMushroom && plant.Temperature < 45f)
            return false;

        plant.Depletion = GetWaterDepletion(plant.BlockValue, plant.Temperature);
        plant.StageRateTicks = (ulong)Math.Max(1, stageSeconds * 20);
        return true;
    }

    private static int GetMaxPlantActiveElapsedSeconds(List<PlantCatchupEntry> plants)
    {
        int max = 0;
        if (plants == null)
            return max;

        ulong nowTicks = GameTimer.Instance.ticks;

        for (int i = 0; i < plants.Count; i++)
        {
            PlantCatchupEntry plant = plants[i];
            if (plant == null || plant.TileEntity == null)
                continue;

            ulong previousTicks = plant.TileEntity.GameTimerTicks;
            if (previousTicks <= 0UL)
                previousTicks = nowTicks;

            ulong elapsedTicks = nowTicks >= previousTicks ? nowTicks - previousTicks : 0UL;
            int elapsedSeconds = (int)(elapsedTicks / 20UL);
            if (elapsedSeconds > max)
                max = elapsedSeconds;
        }

        return max;
    }

    private static int ProcessPlantsShared(WorldBase world, List<PlantCatchupEntry> plants, List<WaterCatchupEntry> waterEntries, int nowSeconds)
    {
        if (plants == null || plants.Count == 0)
        {
            if (s_catchupDebugEnabled)
                LogCatchupDebug("SharedPlants SKIP noPlants nowSeconds=" + nowSeconds);
            return 0;
        }

        bool debug = s_catchupDebugEnabled;
        ulong nowTicks = GameTimer.Instance.ticks;
        int baseGrowthSeconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds();

        if (debug)
            LogCatchupDebug("SharedPlants ENTER plants=" + plants.Count
                + " waterEntries=" + (waterEntries != null ? waterEntries.Count : 0)
                + " nowSeconds=" + nowSeconds
                + " nowTicks=" + nowTicks
                + " baseGrowthSeconds=" + baseGrowthSeconds);

        int processed = 0;
        int totalSteps = 0;
        int detailCount = 0;

        for (int i = 0; i < plants.Count; i++)
        {
            PlantCatchupEntry plant = plants[i];
            if (plant == null || plant.TileEntity == null)
                continue;

            TileEntityPlantGrowingRebirth beforeTe = plant.TileEntity;
            string beforeBlock = SafeBlockName(plant.BlockValue);
            ulong beforeAccum = beforeTe.AccumulatedTicks;
            bool beforeWatered = beforeTe.bWatered;
            ulong beforeGameTicks = beforeTe.GameTimerTicks;
            int beforeLastWorld = beforeTe.LastProcessedWorldSeconds;
            int stageSeconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds(world, plant.Pos);
            if (stageSeconds < 1) stageSeconds = 1;

            PreparePlantCatchup(world, plant, nowSeconds, nowTicks, stageSeconds);
            if (plant.Deferred)
                continue;

            int applied = 0;
            if (!plant.Halted && plant.ValidSecondsRemaining > 0)
                applied = ApplySharedCatchupToPlant(world, plant, waterEntries);
            totalSteps += applied;

            TileEntityPlantGrowingRebirth te = plant.TileEntity;
            if (te == null)
            {
                if (debug && detailCount < CatchupDebugMaxPlantDetails)
                {
                    detailCount++;
                    LogCatchupDebug("SharedPlant DETAIL pos=" + plant.Pos
                        + " beforeBlock=" + beforeBlock
                        + " afterTe=null"
                        + " appliedSeconds=" + applied
                        + " halted=" + plant.Halted);
                }
                continue;
            }

            te.GameTimerTicks = nowTicks;
            te.LastProcessedWorldSeconds = nowSeconds;

            if (plant.Dirty)
            {
                te.setModified();
                AdvancedFarmingSyncService.SendPlant(plant.Pos, plant.BlockValue.type, te.AccumulatedTicks, te.bWatered);
            }

            if (debug && detailCount < CatchupDebugMaxPlantDetails)
            {
                detailCount++;
                LogCatchupDebug("SharedPlant DETAIL pos=" + plant.Pos
                    + " beforeBlock=" + beforeBlock
                    + " afterBlock=" + SafeBlockName(world.GetBlock(plant.Pos))
                    + " stageSeconds=" + stageSeconds
                    + " appliedSeconds=" + applied
                    + " halted=" + plant.Halted
                    + " dirty=" + plant.Dirty
                    + " validSecondsRemaining=" + plant.ValidSecondsRemaining
                    + " beforeAccum=" + beforeAccum
                    + " afterAccum=" + te.AccumulatedTicks
                    + " beforeWatered=" + beforeWatered
                    + " afterWatered=" + te.bWatered
                    + " beforeGameTicks=" + beforeGameTicks
                    + " afterGameTicks=" + te.GameTimerTicks
                    + " beforeLastWorldSeconds=" + beforeLastWorld
                    + " afterLastWorldSeconds=" + te.LastProcessedWorldSeconds
                    + " depletion=" + plant.Depletion
                    + " temperature=" + plant.Temperature);
            }

            processed++;
        }

        if (debug && waterEntries != null)
        {
            int waterDetails = 0;
            for (int i = 0; i < waterEntries.Count && waterDetails < CatchupDebugMaxWaterDetails; i++)
            {
                WaterCatchupEntry entry = waterEntries[i];
                if (entry == null)
                    continue;

                string kind = entry.Tank != null ? "tank" : (entry.Plot != null ? "plot" : "unknown");
                LogCatchupDebug("SharedWater DETAIL kind=" + kind
                    + " pos=" + entry.Pos
                    + " original=" + entry.OriginalWater
                    + " staged=" + entry.Water
                    + " delta=" + (entry.Water - entry.OriginalWater));
                waterDetails++;
            }
        }

        CommitSharedWater(waterEntries);

        if (debug)
            LogCatchupDebug("SharedPlants EXIT plants=" + plants.Count
                + " processed=" + processed
                + " appliedSeconds=" + totalSteps);

#if DEBUG
        if (AdvancedFarmingDebug.Catchup)
            AdvancedFarmingDebug.Log("catchup", "shared plant catchup plants=" + plants.Count + " appliedSeconds=" + totalSteps);
#endif

        return processed;
    }

    private static int ApplySharedCatchupToPlant(WorldBase world, PlantCatchupEntry plant, List<WaterCatchupEntry> waterEntries)
    {
        int appliedSeconds = 0;
        int safetyTransitions = 0;
        bool debug = s_catchupDebugEnabled;

        while (!plant.Halted && plant.ValidSecondsRemaining > 0 && safetyTransitions < 16)
        {
            TileEntityPlantGrowingRebirth te = plant.TileEntity;
            if (te == null || IsFullyGrownPlant(plant.BlockValue))
                break;

            bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;
            if (advancedFarming && !plant.IsMushroom && !te.bWatered)
            {
                if (!TryConsumeSharedWater(plant.Pos, waterEntries, plant.Depletion))
                {
                    plant.Halted = true;
                    if (debug)
                        LogCatchupDebug("SharedPlant HALT_NO_WATER pos=" + plant.Pos
                            + " block=" + SafeBlockName(plant.BlockValue)
                            + " depletion=" + plant.Depletion
                            + " appliedSeconds=" + appliedSeconds
                            + " validSecondsRemaining=" + plant.ValidSecondsRemaining);
                    break;
                }

                if (debug)
                    LogCatchupDebug("SharedPlant WATER_CONSUMED pos=" + plant.Pos
                        + " block=" + SafeBlockName(plant.BlockValue)
                        + " depletion=" + plant.Depletion
                        + " appliedSeconds=" + appliedSeconds
                        + " validSecondsRemaining=" + plant.ValidSecondsRemaining);

                te.bWatered = true;
                plant.Dirty = true;
            }

            ulong remainingTicks = te.AccumulatedTicks >= plant.StageRateTicks ? 0UL : plant.StageRateTicks - te.AccumulatedTicks;
            int secondsToStage = (int)((remainingTicks + 19UL) / 20UL);
            if (secondsToStage < 1)
                secondsToStage = 1;

            int grantSeconds = plant.ValidSecondsRemaining < secondsToStage ? plant.ValidSecondsRemaining : secondsToStage;
            if (grantSeconds < 1)
                break;

            te.AccumulatedTicks += (ulong)grantSeconds * 20UL;
            plant.ValidSecondsRemaining -= grantSeconds;
            appliedSeconds += grantSeconds;
            plant.Dirty = true;

            if (te.AccumulatedTicks < plant.StageRateTicks)
                continue;

            string oldBlockName = SafeBlockName(plant.BlockValue);
            if (!TryAdvanceToNextStage(world, plant.Pos, plant.BlockValue, te, out BlockValue nextValue, out TileEntityPlantGrowingRebirth nextTe))
            {
                // Keep paid water and accumulated progress on the old plant. The next pass can
                // retry the transition without charging the cycle again.
                plant.Deferred = true;
                break;
            }

            plant.BlockValue = nextValue;
            plant.TileEntity = nextTe;
            plant.IsMushroom = IsMushroom(nextValue);
            te = nextTe;
            if (debug)
                LogCatchupDebug("SharedPlant ADVANCE pos=" + plant.Pos
                    + " from=" + oldBlockName
                    + " to=" + SafeBlockName(plant.BlockValue)
                    + " appliedSeconds=" + appliedSeconds
                    + " validSecondsRemaining=" + plant.ValidSecondsRemaining
                    + " transitionIndex=" + safetyTransitions);
            safetyTransitions++;
        }

        return appliedSeconds;
    }

    private static void PreparePlantCatchup(WorldBase world, PlantCatchupEntry plant, int nowSeconds, ulong nowTicks, int stageSeconds)
    {
        if (plant == null || plant.TileEntity == null)
            return;

        bool debug = s_catchupDebugEnabled;
        plant.Deferred = false;
        TileEntityPlantGrowingRebirth te = plant.TileEntity;
        te.SanitizeTimingBaselines();

        int rawPreviousSeconds = te.LastProcessedWorldSeconds;
        int previousSeconds = rawPreviousSeconds;
        if (previousSeconds <= 0)
            previousSeconds = nowSeconds;

        int elapsedWorldSeconds = nowSeconds - previousSeconds;
        if (elapsedWorldSeconds < 0)
            elapsedWorldSeconds = 0;

        ulong rawPreviousTicks = te.GameTimerTicks;
        int elapsedTickSeconds = -1;
        if (rawPreviousTicks > 0UL && nowTicks >= rawPreviousTicks)
        {
            ulong elapsedTicks = nowTicks - rawPreviousTicks;
            ulong elapsedSecondsFromTicks = elapsedTicks / 20UL;
            elapsedTickSeconds = elapsedSecondsFromTicks > int.MaxValue ? int.MaxValue : (int)elapsedSecondsFromTicks;
        }

        bool useTickElapsed = elapsedTickSeconds >= 0 && elapsedTickSeconds > elapsedWorldSeconds;
        int elapsedSeconds = useTickElapsed ? elapsedTickSeconds : elapsedWorldSeconds;
        string elapsedSource = useTickElapsed ? "gameTimerTicks" : "worldSeconds";

        string biomeName = GetBiomeName(plant.Pos);
        if (string.Equals(biomeName, "wasteland", StringComparison.OrdinalIgnoreCase))
        {
            plant.Halted = true;
            if (debug)
                LogCatchupDebug("PreparePlant HALT pos=" + plant.Pos
                    + " block=" + SafeBlockName(plant.BlockValue)
                    + " reason=wasteland"
                    + " rawPreviousSeconds=" + rawPreviousSeconds
                    + " previousSeconds=" + previousSeconds
                    + " nowSeconds=" + nowSeconds
                    + " elapsedSeconds=" + elapsedSeconds
                    + " elapsedWorldSeconds=" + elapsedWorldSeconds
                    + " elapsedTickSeconds=" + elapsedTickSeconds
                    + " elapsedSource=" + elapsedSource);
            return;
        }

        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;
        bool isNight = !GameManager.Instance.World.IsDaytime();

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            plant.Pos,
            3,
            3,
            plant.IsMushroom,
            advancedFarming);

        bool hasSunLight = light.HasSunLight;
        bool hasBlockLight = light.HasBlockLight;
        bool hasOpenSky = light.HasOpenSky;
        bool lightPass = plant.IsMushroom || (((hasSunLight || hasOpenSky) && !isNight) || hasBlockLight);
        if (te.SetAuthoritativeLightPass(lightPass))
            te.setModified();

        AdvancedFarmingTemperatureService.TemperatureResult temperature = AdvancedFarmingTemperatureService.Evaluate(
            world,
            plant.Pos,
            hasSunLight || hasOpenSky,
            advancedFarming);
        plant.Temperature = temperature.Temperature;

        if (advancedFarming && !temperature.ThermalEvaluationComplete)
        {
            // A chunk-load callback can arrive before every chunk touched by the enclosure
            // search is synchronously available. Preserve the crop's existing catch-up
            // baselines and retry later instead of consuming elapsed time using incomplete
            // enclosure geometry.
            plant.Deferred = true;
            if (debug)
                LogCatchupDebug("PreparePlant DEFER pos=" + plant.Pos
                    + " block=" + SafeBlockName(plant.BlockValue)
                    + " reason=thermalChunkUnavailable"
                    + " rawPreviousSeconds=" + rawPreviousSeconds
                    + " previousSeconds=" + previousSeconds
                    + " nowSeconds=" + nowSeconds
                    + " elapsedSeconds=" + elapsedSeconds
                    + " elapsedWorldSeconds=" + elapsedWorldSeconds
                    + " elapsedTickSeconds=" + elapsedTickSeconds
                    + " elapsedSource=" + elapsedSource);
            return;
        }

        if (advancedFarming && !plant.IsMushroom && plant.Temperature < 45f)
        {
            plant.Halted = true;
            if (debug)
                LogCatchupDebug("PreparePlant HALT pos=" + plant.Pos
                    + " block=" + SafeBlockName(plant.BlockValue)
                    + " reason=temperature"
                    + " temp=" + plant.Temperature
                    + " rawPreviousSeconds=" + rawPreviousSeconds
                    + " previousSeconds=" + previousSeconds
                    + " nowSeconds=" + nowSeconds
                    + " elapsedSeconds=" + elapsedSeconds
                    + " elapsedWorldSeconds=" + elapsedWorldSeconds
                    + " elapsedTickSeconds=" + elapsedTickSeconds
                    + " elapsedSource=" + elapsedSource
                    + " sun=" + hasSunLight
                    + " openSky=" + hasOpenSky
                    + " blockLight=" + hasBlockLight
                    + " isNight=" + isNight);
            return;
        }

        bool currentBlockLightValid = hasBlockLight || plant.IsMushroom;
        bool outsideOpenSky = hasOpenSky || hasSunLight;

        int validSecondsBeforeCap = elapsedSeconds;
        string validReason = "rawElapsed";
        int estimatedDaylightWorldSeconds = -1;

        if (advancedFarming && !plant.IsMushroom)
        {
            if (currentBlockLightValid)
            {
                // Artificial/block light is only trusted for the active evaluation window because
                // we cannot know how long it was powered while the chunk was away.
                validSecondsBeforeCap = Math.Min(elapsedSeconds, AdvancedFarmingRuntimePolicy.GetActiveLightTrustedSeconds());
                validReason = "blockLightTrustedWindow";
            }
            else if (outsideOpenSky)
            {
                estimatedDaylightWorldSeconds = EstimateDaylightSeconds(previousSeconds, nowSeconds);
                validSecondsBeforeCap = ScaleDaylightEstimateToElapsedSeconds(
                    elapsedSeconds,
                    elapsedWorldSeconds,
                    estimatedDaylightWorldSeconds,
                    isNight);
                validReason = useTickElapsed ? "estimatedDaylightScaledFromGameTimerTicks" : "estimatedDaylight";
            }
            else
            {
                validSecondsBeforeCap = 0;
                validReason = "noTrustedLight";
            }
        }

        if (validSecondsBeforeCap <= 0)
        {
            plant.Halted = true;
            if (debug)
                LogCatchupDebug("PreparePlant HALT pos=" + plant.Pos
                    + " block=" + SafeBlockName(plant.BlockValue)
                    + " reason=" + validReason
                    + " rawPreviousSeconds=" + rawPreviousSeconds
                    + " previousSeconds=" + previousSeconds
                    + " nowSeconds=" + nowSeconds
                    + " elapsedSeconds=" + elapsedSeconds
                    + " elapsedWorldSeconds=" + elapsedWorldSeconds
                    + " elapsedTickSeconds=" + elapsedTickSeconds
                    + " elapsedSource=" + elapsedSource
                    + " estimatedDaylightWorldSeconds=" + estimatedDaylightWorldSeconds
                    + " validSecondsBeforeCap=" + validSecondsBeforeCap
                    + " sun=" + hasSunLight
                    + " openSky=" + hasOpenSky
                    + " blockLight=" + hasBlockLight
                    + " isNight=" + isNight
                    + " temp=" + plant.Temperature);
            return;
        }

        int maxGrant = AdvancedFarmingRuntimePolicy.GetMaxCatchupGrantSeconds();
        int validSeconds = validSecondsBeforeCap;
        if (validSeconds > maxGrant)
            validSeconds = maxGrant;

        plant.ValidSecondsRemaining = validSeconds;
        plant.Depletion = GetWaterDepletion(plant.BlockValue, plant.Temperature);
        plant.StageRateTicks = (ulong)Math.Max(1, stageSeconds * 20);

        if (debug)
            LogCatchupDebug("PreparePlant PASS pos=" + plant.Pos
                + " block=" + SafeBlockName(plant.BlockValue)
                + " rawPreviousSeconds=" + rawPreviousSeconds
                + " previousSeconds=" + previousSeconds
                + " nowSeconds=" + nowSeconds
                + " elapsedSeconds=" + elapsedSeconds
                + " elapsedWorldSeconds=" + elapsedWorldSeconds
                + " elapsedTickSeconds=" + elapsedTickSeconds
                + " elapsedSource=" + elapsedSource
                + " rawPreviousTicks=" + rawPreviousTicks
                + " nowTicks=" + nowTicks
                + " validReason=" + validReason
                + " estimatedDaylightWorldSeconds=" + estimatedDaylightWorldSeconds
                + " validSecondsBeforeCap=" + validSecondsBeforeCap
                + " validSecondsAfterCap=" + validSeconds
                + " maxGrant=" + maxGrant
                + " growthSeconds=" + stageSeconds
                + " stageRateTicks=" + plant.StageRateTicks
                + " accum=" + te.AccumulatedTicks
                + " watered=" + te.bWatered
                + " depletion=" + plant.Depletion
                + " temp=" + plant.Temperature
                + " sun=" + hasSunLight
                + " openSky=" + hasOpenSky
                + " blockLight=" + hasBlockLight
                + " isNight=" + isNight);
    }


    private static int ScaleDaylightEstimateToElapsedSeconds(int elapsedSeconds, int elapsedWorldSeconds, int daylightWorldSeconds, bool isNight)
    {
        if (elapsedSeconds <= 0)
            return 0;

        if (elapsedWorldSeconds <= 0)
            return isNight ? 0 : elapsedSeconds;

        if (daylightWorldSeconds <= 0)
            return 0;

        if (daylightWorldSeconds >= elapsedWorldSeconds)
            return elapsedSeconds;

        long scaled = ((long)elapsedSeconds * daylightWorldSeconds) / elapsedWorldSeconds;
        if (scaled < 0L)
            return 0;

        if (scaled > elapsedSeconds)
            return elapsedSeconds;

        return (int)scaled;
    }


    private static string ResolveSeedStageNameForHarvest(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return string.Empty;

        string lower = blockName.ToLowerInvariant();
        int harvestIndex = lower.IndexOf("3harvest", StringComparison.Ordinal);
        if (harvestIndex < 0)
            return string.Empty;

        string candidate = blockName.Substring(0, harvestIndex) + "1";
        BlockValue candidateValue = Block.GetBlockValue(candidate);
        if (candidateValue.isair || candidateValue.Block == null)
            return string.Empty;

        return candidate;
    }

    private static bool IsFullyGrownPlant(BlockValue blockValue)
    {
        Block block = blockValue.Block;
        if (block == null)
            return false;

        string name = block.GetBlockName();
        return !string.IsNullOrEmpty(name) && name.IndexOf("3Harvest", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ProcessPlantSharedStep(WorldBase world, PlantCatchupEntry plant, List<WaterCatchupEntry> waterEntries)
    {
        TileEntityPlantGrowingRebirth te = plant.TileEntity;
        if (te == null)
            return false;

        if (IsFullyGrownPlant(plant.BlockValue))
            return true;

        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;

        if (advancedFarming && !plant.IsMushroom && !te.bWatered)
        {
            if (!TryConsumeSharedWater(plant.Pos, waterEntries, plant.Depletion))
                return false;

            te.bWatered = true;
        }

        te.AccumulatedTicks += 20UL;

        if (te.AccumulatedTicks >= plant.StageRateTicks)
        {
            if (!TryAdvanceToNextStage(world, plant.Pos, plant.BlockValue, te, out BlockValue nextValue, out TileEntityPlantGrowingRebirth nextTe))
                return false;

            plant.BlockValue = nextValue;
            plant.TileEntity = nextTe;
            plant.IsMushroom = IsMushroom(nextValue);
        }

        return true;
    }

    private static bool TryConsumeSharedWater(Vector3i plantPos, List<WaterCatchupEntry> waterEntries, int depletion)
    {
        if (depletion <= 0)
            return true;

        if (waterEntries == null)
            return false;

        WaterCatchupEntry best = null;
        int bestWater = -1;
        for (int i = 0; i < waterEntries.Count; i++)
        {
            WaterCatchupEntry entry = waterEntries[i];
            if (entry == null || entry.Tank == null || entry.Water < depletion)
                continue;

            if (!IsSharedWaterInRange(plantPos, entry.Pos))
                continue;

            if (entry.Water > bestWater)
            {
                best = entry;
                bestWater = entry.Water;
            }
        }

        if (best != null)
        {
            best.Water = best.Water - depletion;
            return true;
        }

        Vector3i directPlotPos = plantPos + Vector3i.down;
        for (int i = 0; i < waterEntries.Count; i++)
        {
            WaterCatchupEntry entry = waterEntries[i];
            if (entry == null || entry.Plot == null || entry.Water < depletion)
                continue;

            if (entry.Pos.x == directPlotPos.x && entry.Pos.y == directPlotPos.y && entry.Pos.z == directPlotPos.z)
            {
                entry.Water = entry.Water - depletion;
                return true;
            }
        }

        return false;
    }

    private static bool IsSharedWaterInRange(Vector3i plantPos, Vector3i waterPos)
    {
        return Math.Abs(plantPos.x - waterPos.x) <= 4
            && Math.Abs(plantPos.z - waterPos.z) <= 4
            && waterPos.y >= plantPos.y - 1
            && waterPos.y <= plantPos.y;
    }

    private static void CommitSharedWater(List<WaterCatchupEntry> waterEntries)
    {
        if (waterEntries == null)
            return;

        for (int i = 0; i < waterEntries.Count; i++)
        {
            WaterCatchupEntry entry = waterEntries[i];
            if (entry == null)
                continue;

            if (entry.Water == entry.OriginalWater)
                continue;

            if (entry.Tank != null)
            {
                entry.Tank.waterCount = entry.Water;
                int now = RebirthUtilities.TotalGameSecondsPassed();
                entry.Tank.timeLapsed = now;
                entry.Tank.LastProcessedWorldSeconds = now;
                entry.Tank.setModified();
                AdvancedFarmingSyncService.SendWaterTank(entry.Pos, entry.Tank.waterCount);
            }
            else if (entry.Plot != null)
            {
                entry.Plot.waterCount = entry.Water;
                entry.Plot.LastProcessedWorldSeconds = RebirthUtilities.TotalGameSecondsPassed();
                entry.Plot.setModified();
                AdvancedFarmingSyncService.SendFarmPlot(entry.Pos, entry.Plot.waterCount);
            }
        }
    }

    private static int EstimateDaylightSeconds(int startSecond, int endSecond)
    {
        if (endSecond <= startSecond)
            return 0;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        int dawn = world != null ? world.DawnHour : 4;
        int dusk = world != null ? world.DuskHour : 22;

        // RebirthUtilities.TotalGameSecondsPassed() stores world.GetWorldTime() / 20.
        // 7DTD world time is 24000 units per day and 1000 units per hour, so the
        // persisted catch-up unit is 1200 units per day and 50 units per hour.
        // The old implementation used real seconds (24 * 3600, dawn * 3600), which
        // made early-day values like 614 look permanently before dawn and caused
        // daylight catch-up to produce validSeconds=0 even while World.IsDaytime() was true.
        const int WorldTimeUnitsPerStoredSecond = 20;
        int daySeconds = 24000 / WorldTimeUnitsPerStoredSecond;
        int hourSeconds = 1000 / WorldTimeUnitsPerStoredSecond;
        int dawnSecond = dawn * hourSeconds;
        int duskSecond = dusk * hourSeconds;

        if (daySeconds <= 0 || hourSeconds <= 0)
            return endSecond - startSecond;

        if (duskSecond <= dawnSecond)
            return endSecond - startSecond;

        int total = 0;
        int cursor = startSecond;

        while (cursor < endSecond)
        {
            int dayIndex = cursor / daySeconds;
            int dayStart = dayIndex * daySeconds;
            int nextDay = dayStart + daySeconds;

            int sliceEnd = Math.Min(endSecond, nextDay);
            int lightStart = dayStart + dawnSecond;
            int lightEnd = dayStart + duskSecond;

            int validStart = Math.Max(cursor, lightStart);
            int validEnd = Math.Min(sliceEnd, lightEnd);

            if (validEnd > validStart)
                total += validEnd - validStart;

            cursor = sliceEnd;
        }

        return total;
    }

    public static void ProcessPlant(WorldBase world, Vector3i pos, BlockValue blockValue, TileEntityPlantGrowingRebirth te, int nowSeconds)
    {
        if (world == null || te == null)
            return;

        List<PlantCatchupEntry> plants = new List<PlantCatchupEntry>();
        plants.Add(new PlantCatchupEntry
        {
            Pos = pos,
            BlockValue = blockValue,
            TileEntity = te,
            IsMushroom = IsMushroom(blockValue)
        });

        List<WaterCatchupEntry> water = CollectSharedWater(world, pos);
        ProcessPlantsShared(world, plants, water, nowSeconds);
    }

    private static ulong ClampPlantProgressTicks(ulong elapsedTicks)
    {
        ulong cap = AdvancedFarmingRuntimePolicy.GetPlantProgressGrantCapTicks();
        if (cap < 1UL)
            cap = 1UL;

        return elapsedTicks > cap ? cap : elapsedTicks;
    }



    public static string StartCatchupWatch(WorldBase world, Vector3i pos, int growthSeconds, int waitSeconds, string label)
    {
        StringBuilder sb = new StringBuilder(8192);
        if (growthSeconds < 1)
            growthSeconds = 1;
        if (growthSeconds > 86400 * 7)
            growthSeconds = 86400 * 7;
        if (waitSeconds < 1)
            waitSeconds = 1;
        if (waitSeconds > 86400 * 7)
            waitSeconds = 86400 * 7;

        sb.AppendLine("[AdvancedFarming CatchupWatch] START requested pos=" + pos
            + " growthSeconds=" + growthSeconds
            + " waitSeconds=" + waitSeconds
            + " label=" + (string.IsNullOrEmpty(label) ? "<none>" : label));

        if (world == null)
        {
            sb.AppendLine("[AdvancedFarming CatchupWatch] abort world=null");
            return sb.ToString();
        }

        TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if (te == null)
        {
            sb.AppendLine("[AdvancedFarming CatchupWatch] abort no crop tile entity at pos=" + pos);
            return sb.ToString();
        }

        BlockValue startBlockValue = world.GetBlock(pos);
        if (IsFullyGrownPlant(startBlockValue))
        {
            string harvestName = GetBlockName(startBlockValue);
            string seedStageName = ResolveSeedStageNameForHarvest(harvestName);
            if (string.IsNullOrEmpty(seedStageName))
            {
                sb.AppendLine("[AdvancedFarming CatchupWatch] abort target is already fully grown/harvest stage block=" + harvestName + " pos=" + pos + "; could not resolve first growth stage for a controlled test reset.");
                return sb.ToString();
            }

            BlockValue resetValue = Block.GetBlockValue(seedStageName);
            if (resetValue.isair || resetValue.Block == null)
            {
                sb.AppendLine("[AdvancedFarming CatchupWatch] abort target is harvest stage block=" + harvestName + " pos=" + pos + " resolvedSeedStage=" + seedStageName + " but seed block was invalid.");
                return sb.ToString();
            }

            resetValue.rotation = startBlockValue.rotation;
            resetValue.meta = startBlockValue.meta;
            resetValue.meta2 = startBlockValue.meta2;
            SetExposureNeutralPlantBlockRpc(world, pos, startBlockValue, resetValue);
            startBlockValue = world.GetBlock(pos);
            te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
            if (te == null)
            {
                sb.AppendLine("[AdvancedFarming CatchupWatch] abort reset harvest block=" + harvestName + " to seedStage=" + seedStageName + " but no crop tile entity was available afterward pos=" + pos);
                return sb.ToString();
            }

            te.AccumulatedTicks = 0UL;
            te.bWatered = false;
            te.setModified();
            AdvancedFarmingSyncService.SendPlant(pos, startBlockValue.type, te.AccumulatedTicks, te.bWatered);
            sb.AppendLine("[AdvancedFarming CatchupWatch] resetHarvestForTest pos=" + pos + " from=" + harvestName + " to=" + GetBlockName(startBlockValue) + " note=The watch command selected/reset a harvest-stage crop because no immature crop was available in the radius scan.");
        }

        sb.AppendLine("[AdvancedFarming CatchupWatch] beforeBaseline:");
        AppendCatchupDiagnostic(sb, world, pos, "watchBeforeBaseline");

        AdvancedFarmingRuntimePolicy.SetGrowthSeconds(growthSeconds);
        ResetSchedulingState();

        int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();
        ulong nowTicks = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;

        te.LastProcessedWorldSeconds = nowSeconds;
        te.GameTimerTicks = nowTicks;
        te.setModified();
        AdvancedFarmingSyncService.SendPlant(pos, startBlockValue.type, te.AccumulatedTicks, te.bWatered);

        BlockValue baselineBlockValue = world.GetBlock(pos);
        string baselineBlockName = GetBlockName(baselineBlockValue);
        ulong baselineAccumulatedTicks = te.AccumulatedTicks;
        bool baselineWatered = te.bWatered;
        ulong baselineGameTimerTicks = te.GameTimerTicks;
        int baselineLastProcessedWorldSeconds = te.LastProcessedWorldSeconds;

        lock (s_catchupWatchLock)
        {
            s_catchupWatchActive = true;
            s_catchupWatchCompleted = false;
            s_catchupWatchPos = pos;
            s_catchupWatchLabel = string.IsNullOrEmpty(label) ? "catchupwatch" : label;
            s_catchupWatchGrowthSeconds = growthSeconds;
            s_catchupWatchWaitSeconds = waitSeconds;
            s_catchupWatchStartWorldSeconds = nowSeconds;
            s_catchupWatchStartGameTimerTicks = nowTicks;
            s_catchupWatchStartUtcTicks = DateTime.UtcNow.Ticks;
            s_catchupWatchBeforeBlockName = baselineBlockName;
            s_catchupWatchBeforeAccumulatedTicks = baselineAccumulatedTicks;
            s_catchupWatchBeforeWatered = baselineWatered;
            s_catchupWatchBeforeGameTimerTicks = baselineGameTimerTicks;
            s_catchupWatchBeforeLastProcessedWorldSeconds = baselineLastProcessedWorldSeconds;
        }

        sb.AppendLine("[AdvancedFarming CatchupWatch] baselineSet pos=" + pos
            + " growthSeconds=" + growthSeconds
            + " waitSeconds=" + waitSeconds
            + " startWorldSeconds=" + nowSeconds
            + " startGameTimerTicks=" + nowTicks
            + " block=" + baselineBlockName
            + " accumulatedTicks=" + baselineAccumulatedTicks
            + " watered=" + baselineWatered
            + " lastProcessedWorldSeconds=" + baselineLastProcessedWorldSeconds
            + " note=The command arms a watch and returns immediately. Leave/unload the chunk now. After waitSeconds, when the target crop tile entity is loaded again, the GameUpdate pump will run shared catch-up for the area and log the delta automatically. You can also run rbfarming catchupwatch finish after returning.");
        sb.AppendLine("[AdvancedFarming CatchupWatch] afterBaseline:");
        AppendCatchupDiagnostic(sb, world, pos, "watchAfterBaseline");
        return sb.ToString();
    }

    public static string StopCatchupWatch()
    {
        lock (s_catchupWatchLock)
        {
            if (!s_catchupWatchActive)
                return "[AdvancedFarming CatchupWatch] no active watch";

            string message = "[AdvancedFarming CatchupWatch] stopped pos=" + s_catchupWatchPos
                + " label=" + (string.IsNullOrEmpty(s_catchupWatchLabel) ? "<none>" : s_catchupWatchLabel);
            s_catchupWatchActive = false;
            s_catchupWatchCompleted = true;
            return message;
        }
    }

    public static string BuildCatchupWatchStatus(WorldBase world)
    {
        bool active;
        bool completed;
        Vector3i pos;
        string label;
        int growthSeconds;
        int waitSeconds;
        int startWorldSeconds;
        ulong startGameTimerTicks;
        long startUtcTicks;

        lock (s_catchupWatchLock)
        {
            active = s_catchupWatchActive;
            completed = s_catchupWatchCompleted;
            pos = s_catchupWatchPos;
            label = s_catchupWatchLabel;
            growthSeconds = s_catchupWatchGrowthSeconds;
            waitSeconds = s_catchupWatchWaitSeconds;
            startWorldSeconds = s_catchupWatchStartWorldSeconds;
            startGameTimerTicks = s_catchupWatchStartGameTimerTicks;
            startUtcTicks = s_catchupWatchStartUtcTicks;
        }

        StringBuilder sb = new StringBuilder(4096);
        int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();
        ulong nowTicks = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
        int elapsedWorldSeconds = nowSeconds - startWorldSeconds;
        if (elapsedWorldSeconds < 0)
            elapsedWorldSeconds = 0;
        long elapsedRealSeconds = startUtcTicks > 0L ? (DateTime.UtcNow.Ticks - startUtcTicks) / TimeSpan.TicksPerSecond : 0L;
        if (elapsedRealSeconds < 0L)
            elapsedRealSeconds = 0L;

        sb.AppendLine("[AdvancedFarming CatchupWatch] status active=" + active
            + " completed=" + completed
            + " pos=" + pos
            + " label=" + (string.IsNullOrEmpty(label) ? "<none>" : label)
            + " growthSeconds=" + growthSeconds
            + " waitSeconds=" + waitSeconds
            + " elapsedWorldSeconds=" + elapsedWorldSeconds
            + " elapsedRealSeconds=" + elapsedRealSeconds
            + " elapsedGameTimerTicks=" + (nowTicks >= startGameTimerTicks ? nowTicks - startGameTimerTicks : 0UL));

        if (active && world != null)
        {
            sb.AppendLine("[AdvancedFarming CatchupWatch] current:");
            AppendCatchupDiagnostic(sb, world, pos, "watchStatusCurrent");
        }

        return sb.ToString();
    }

    public static string FinishCatchupWatch(WorldBase world, string reason)
    {
        return CompleteCatchupWatch(world, reason, true, Vector3i.zero);
    }

    public static void TryCompleteCatchupWatchFromArea(WorldBase world, Vector3i anchorPos)
    {
        if (!IsCatchupWatchCompletionDue(anchorPos))
            return;

        string report = CompleteCatchupWatch(world, "areaProcess", false, anchorPos);
        if (!string.IsNullOrEmpty(report))
            Log.Out(report);
    }

    private static bool IsCatchupWatchCompletionDue(Vector3i anchorPos)
    {
        lock (s_catchupWatchLock)
        {
            if (!s_catchupWatchActive || s_catchupWatchCompleted)
                return false;

            if (!IsPositionInCatchupWatchArea(anchorPos, s_catchupWatchPos))
                return false;

            int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();
            int elapsedWorldSeconds = nowSeconds - s_catchupWatchStartWorldSeconds;
            if (elapsedWorldSeconds < 0)
                elapsedWorldSeconds = 0;
            int elapsedRealSeconds = s_catchupWatchStartUtcTicks > 0L ? (int)((DateTime.UtcNow.Ticks - s_catchupWatchStartUtcTicks) / TimeSpan.TicksPerSecond) : 0;
            if (elapsedRealSeconds < 0)
                elapsedRealSeconds = 0;
            return elapsedWorldSeconds >= s_catchupWatchWaitSeconds || elapsedRealSeconds >= s_catchupWatchWaitSeconds;
        }
    }

    private static bool IsPositionInCatchupWatchArea(Vector3i anchorPos, Vector3i watchedPos)
    {
        return Math.Abs(anchorPos.x - watchedPos.x) <= AreaRadius
            && Math.Abs(anchorPos.z - watchedPos.z) <= AreaRadius
            && Math.Abs(anchorPos.y - watchedPos.y) <= 8;
    }

    private static string CompleteCatchupWatch(WorldBase world, string reason, bool force, Vector3i anchorPos)
    {
        bool active;
        Vector3i pos;
        string label;
        int growthSeconds;
        int waitSeconds;
        int startWorldSeconds;
        ulong startGameTimerTicks;
        long startUtcTicks;
        string beforeBlockName;
        ulong beforeAccumulatedTicks;
        bool beforeWatered;
        ulong beforeGameTimerTicks;
        int beforeLastProcessedWorldSeconds;

        lock (s_catchupWatchLock)
        {
            active = s_catchupWatchActive;
            if (!active && !force)
                return string.Empty;
            if (!active)
                return "[AdvancedFarming CatchupWatch] no active watch";

            pos = s_catchupWatchPos;
            label = s_catchupWatchLabel;
            growthSeconds = s_catchupWatchGrowthSeconds;
            waitSeconds = s_catchupWatchWaitSeconds;
            startWorldSeconds = s_catchupWatchStartWorldSeconds;
            startGameTimerTicks = s_catchupWatchStartGameTimerTicks;
            startUtcTicks = s_catchupWatchStartUtcTicks;
            beforeBlockName = s_catchupWatchBeforeBlockName;
            beforeAccumulatedTicks = s_catchupWatchBeforeAccumulatedTicks;
            beforeWatered = s_catchupWatchBeforeWatered;
            beforeGameTimerTicks = s_catchupWatchBeforeGameTimerTicks;
            beforeLastProcessedWorldSeconds = s_catchupWatchBeforeLastProcessedWorldSeconds;
        }

        int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();
        int elapsedWorldSeconds = nowSeconds - startWorldSeconds;
        if (elapsedWorldSeconds < 0)
            elapsedWorldSeconds = 0;
        ulong nowTicks = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
        ulong elapsedGameTimerTicks = nowTicks >= startGameTimerTicks ? nowTicks - startGameTimerTicks : 0UL;
        long elapsedRealSeconds = startUtcTicks > 0L ? (DateTime.UtcNow.Ticks - startUtcTicks) / TimeSpan.TicksPerSecond : 0L;
        if (elapsedRealSeconds < 0L)
            elapsedRealSeconds = 0L;

        if (!force && elapsedWorldSeconds < waitSeconds)
            return string.Empty;

        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[AdvancedFarming CatchupWatch] COMPLETE reason=" + (string.IsNullOrEmpty(reason) ? "<none>" : reason)
            + " force=" + force
            + " label=" + (string.IsNullOrEmpty(label) ? "<none>" : label)
            + " pos=" + pos
            + " anchorPos=" + anchorPos
            + " growthSeconds=" + growthSeconds
            + " waitSeconds=" + waitSeconds
            + " elapsedWorldSeconds=" + elapsedWorldSeconds
            + " elapsedRealSeconds=" + elapsedRealSeconds
            + " elapsedGameTimerTicks=" + elapsedGameTimerTicks);

        AppendCatchupDelta(sb, world, pos, beforeBlockName, beforeAccumulatedTicks, beforeWatered, beforeGameTimerTicks, beforeLastProcessedWorldSeconds);
        sb.AppendLine("[AdvancedFarming CatchupWatch] after:");
        AppendCatchupDiagnostic(sb, world, pos, "watchAfter");
        sb.AppendLine("[AdvancedFarming CatchupWatch] END pos=" + pos);

        lock (s_catchupWatchLock)
        {
            s_catchupWatchActive = false;
            s_catchupWatchCompleted = true;
        }

        return sb.ToString();
    }

    public static string BuildCatchupDiagnostic(WorldBase world, Vector3i pos)
    {
        StringBuilder sb = new StringBuilder(4096);
        AppendCatchupDiagnostic(sb, world, pos, "current");
        return sb.ToString();
    }

    public static string RunCatchupDiagnostic(WorldBase world, Vector3i pos, int backdateSeconds, bool processArea)
    {
        StringBuilder sb = new StringBuilder(8192);
        if (backdateSeconds < 0)
            backdateSeconds = 0;
        if (backdateSeconds > 86400 * 7)
            backdateSeconds = 86400 * 7;

        sb.AppendLine("[AdvancedFarming CatchupDiag] BEGIN pos=" + pos + " mode=" + (processArea ? "runarea" : "runplant") + " backdateSeconds=" + backdateSeconds);
        AppendCatchupDiagnostic(sb, world, pos, "before");

        if (world == null)
        {
            sb.AppendLine("[AdvancedFarming CatchupDiag] abort world=null");
            return sb.ToString();
        }

        TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if (te == null)
        {
            sb.AppendLine("[AdvancedFarming CatchupDiag] abort no crop tile entity at pos=" + pos);
            return sb.ToString();
        }

        BlockValue beforeBlockValue = world.GetBlock(pos);
        string beforeBlockName = beforeBlockValue.Block != null ? beforeBlockValue.Block.GetBlockName() : "<null>";
        ulong beforeAccumulatedTicks = te.AccumulatedTicks;
        bool beforeWatered = te.bWatered;
        ulong beforeGameTimerTicks = te.GameTimerTicks;
        int beforeLastProcessedWorldSeconds = te.LastProcessedWorldSeconds;

        if (backdateSeconds > 0)
        {
            BackdatePlantTimingForDiagnostic(te, backdateSeconds);
            te.setModified();
            sb.AppendLine("[AdvancedFarming CatchupDiag] backdated pos=" + pos
                + " seconds=" + backdateSeconds
                + " newGameTimerTicks=" + te.GameTimerTicks
                + " newLastProcessedSeconds=" + te.LastProcessedWorldSeconds);
            AppendCatchupDiagnostic(sb, world, pos, "afterBackdateBeforeRun");

            beforeBlockValue = world.GetBlock(pos);
            beforeBlockName = beforeBlockValue.Block != null ? beforeBlockValue.Block.GetBlockName() : "<null>";
            beforeAccumulatedTicks = te.AccumulatedTicks;
            beforeWatered = te.bWatered;
            beforeGameTimerTicks = te.GameTimerTicks;
            beforeLastProcessedWorldSeconds = te.LastProcessedWorldSeconds;
        }

        bool result;
        if (processArea)
        {
            result = ProcessArea(world, pos);
        }
        else
        {
            TileEntityPlantGrowingRebirth runTe = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
            if (runTe == null)
            {
                sb.AppendLine("[AdvancedFarming CatchupDiag] abort no crop tile entity before run pos=" + pos);
                return sb.ToString();
            }

            ProcessPlant(world, pos, world.GetBlock(pos), runTe, RebirthUtilities.TotalGameSecondsPassed());
            result = true;
        }

        sb.AppendLine("[AdvancedFarming CatchupDiag] runResult=" + result);
        AppendCatchupDelta(sb, world, pos, beforeBlockName, beforeAccumulatedTicks, beforeWatered, beforeGameTimerTicks, beforeLastProcessedWorldSeconds);
        AppendCatchupDiagnostic(sb, world, pos, "after");
        sb.AppendLine("[AdvancedFarming CatchupDiag] END pos=" + pos);
        return sb.ToString();
    }

    private static void BackdatePlantTimingForDiagnostic(TileEntityPlantGrowingRebirth te, int seconds)
    {
        if (te == null)
            return;

        int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();
        ulong nowTicks = GameTimer.Instance.ticks;
        ulong backTicks = (ulong)seconds * 20UL;

        te.LastProcessedWorldSeconds = nowSeconds > seconds ? nowSeconds - seconds : 0;
        te.GameTimerTicks = nowTicks > backTicks ? nowTicks - backTicks : 0UL;
    }

    private static void AppendCatchupDelta(StringBuilder sb, WorldBase world, Vector3i pos, string beforeBlockName, ulong beforeAccumulatedTicks, bool beforeWatered, ulong beforeGameTimerTicks, int beforeLastProcessedWorldSeconds)
    {
        if (sb == null)
            return;

        TileEntityPlantGrowingRebirth te = world != null ? world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth : null;
        string afterBlockName = world != null ? GetBlockName(world.GetBlock(pos)) : "<null>";

        if (te == null)
        {
            sb.AppendLine("[AdvancedFarming CatchupDiag] delta pos=" + pos
                + " beforeBlock=" + beforeBlockName
                + " afterBlock=" + afterBlockName
                + " afterTE=null");
            return;
        }

        long accumulatedDelta = (long)te.AccumulatedTicks - (long)beforeAccumulatedTicks;
        long gameTimerDelta = (long)te.GameTimerTicks - (long)beforeGameTimerTicks;
        int lastProcessedDelta = te.LastProcessedWorldSeconds - beforeLastProcessedWorldSeconds;
        bool stageChanged = !string.Equals(beforeBlockName, afterBlockName, StringComparison.OrdinalIgnoreCase);

        sb.AppendLine("[AdvancedFarming CatchupDiag] delta pos=" + pos
            + " stageChanged=" + stageChanged
            + " beforeBlock=" + beforeBlockName
            + " afterBlock=" + afterBlockName
            + " accumulatedBefore=" + beforeAccumulatedTicks
            + " accumulatedAfter=" + te.AccumulatedTicks
            + " accumulatedDelta=" + accumulatedDelta
            + " wateredBefore=" + beforeWatered
            + " wateredAfter=" + te.bWatered
            + " gameTimerTicksBefore=" + beforeGameTimerTicks
            + " gameTimerTicksAfter=" + te.GameTimerTicks
            + " gameTimerTicksDelta=" + gameTimerDelta
            + " lastProcessedBefore=" + beforeLastProcessedWorldSeconds
            + " lastProcessedAfter=" + te.LastProcessedWorldSeconds
            + " lastProcessedDelta=" + lastProcessedDelta);
    }

    private static void AppendCatchupDiagnostic(StringBuilder sb, WorldBase world, Vector3i pos, string label)
    {
        if (sb == null)
            return;

        if (world == null)
        {
            sb.AppendLine("[AdvancedFarming CatchupDiag] " + label + " world=null pos=" + pos);
            return;
        }

        TileEntityPlantGrowingRebirth te = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if (te == null)
        {
            sb.AppendLine("[AdvancedFarming CatchupDiag] " + label + " no crop tile entity pos=" + pos + " block=" + GetBlockName(world.GetBlock(pos)));
            return;
        }

        BlockValue blockValue = world.GetBlock(pos);
        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;
        bool isMushroom = IsMushroom(blockValue);
        bool fullyGrown = IsFullyGrownPlant(blockValue);
        string biomeName = GetBiomeName(pos);
        bool biomePass = !string.Equals(biomeName, "wasteland", StringComparison.OrdinalIgnoreCase);
        bool isNight = !GameManager.Instance.World.IsDaytime();
        int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();
        ulong nowTicks = GameTimer.Instance.ticks;

        ulong previousTicks = te.GameTimerTicks;
        ulong normalizedPreviousTicks = previousTicks > 0UL ? previousTicks : nowTicks;
        ulong elapsedTicks = nowTicks >= normalizedPreviousTicks ? nowTicks - normalizedPreviousTicks : 0UL;
        int activeElapsedSecondsRaw = (int)(elapsedTicks / 20UL);
        int activeElapsedSecondsCapped = activeElapsedSecondsRaw;
        int activeMaxElapsedSeconds = AdvancedFarmingRuntimePolicy.GetActiveDirectMaxElapsedSeconds();
        if (activeElapsedSecondsCapped > activeMaxElapsedSeconds)
            activeElapsedSecondsCapped = activeMaxElapsedSeconds;

        int previousSeconds = te.LastProcessedWorldSeconds;
        int normalizedPreviousSeconds = previousSeconds > 0 ? previousSeconds : nowSeconds;
        int sharedElapsedSecondsRaw = nowSeconds - normalizedPreviousSeconds;
        if (sharedElapsedSecondsRaw < 0)
            sharedElapsedSecondsRaw = 0;

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
        bool lightPass = (((hasSunLight || hasOpenSky) && !isNight) || hasBlockLight || isMushroom);
        float temperature = AdvancedFarmingTemperatureService.Evaluate(world, pos, hasSunLight || hasOpenSky, advancedFarming).Temperature;
        bool tempPass = !advancedFarming || isMushroom || temperature >= 45f;
        int depletion = GetWaterDepletion(blockValue, temperature);

        int tankWater = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayWaterTank", 4, 1, 0);
        int collectorWater = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayDewCollector", 4, 1, 0);
        int plotWater = 0;
        TileEntityFarmPlotRebirth plot = world.GetTileEntity(pos + Vector3i.down) as TileEntityFarmPlotRebirth;
        if (plot != null)
            plotWater = plot.waterCount;

        bool canStartWaterCycle = isMushroom || te.bWatered || tankWater >= depletion || collectorWater >= depletion || plotWater >= depletion;

        int growthSeconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds(world, pos);
        ulong stageRateTicks = (ulong)Math.Max(1, growthSeconds * 20);
        ulong remainingTicks = te.AccumulatedTicks >= stageRateTicks ? 0UL : stageRateTicks - te.AccumulatedTicks;
        int secondsToNextStage = (int)((remainingTicks + 19UL) / 20UL);
        if (secondsToNextStage < 1 && !fullyGrown)
            secondsToNextStage = 1;

        string sharedReason;
        int sharedValidBeforeCap = EstimateSharedValidSecondsForDiagnostic(sharedElapsedSecondsRaw, hasBlockLight, hasOpenSky || hasSunLight, isMushroom, advancedFarming, biomePass, tempPass, normalizedPreviousSeconds, nowSeconds, out sharedReason);
        int sharedValidCapped = sharedValidBeforeCap;
        int maxGrant = AdvancedFarmingRuntimePolicy.GetMaxCatchupGrantSeconds();
        if (sharedValidCapped > maxGrant)
            sharedValidCapped = maxGrant;

        int potentialTransitionsIgnoringWater = 0;
        if (!fullyGrown && stageRateTicks > 0UL)
        {
            ulong potentialTicks = te.AccumulatedTicks + (ulong)Math.Max(0, sharedValidCapped) * 20UL;
            potentialTransitionsIgnoringWater = (int)(potentialTicks / stageRateTicks);
        }

        bool activeEligible = !fullyGrown && biomePass && lightPass && tempPass;
        bool sharedEligible = !fullyGrown && sharedValidCapped > 0;

        sb.AppendLine("[AdvancedFarming CatchupDiag] " + label
            + " pos=" + pos
            + " block=" + GetBlockName(blockValue)
            + " nextStage=" + ResolveNextStageName(blockValue)
            + " fullyGrown=" + fullyGrown
            + " advanced=" + advancedFarming
            + " mushroom=" + isMushroom
            + " biome=" + biomeName
            + " biomePass=" + biomePass
            + " nowSeconds=" + nowSeconds
            + " lastProcessedSeconds=" + te.LastProcessedWorldSeconds
            + " normalizedLastProcessedSeconds=" + normalizedPreviousSeconds
            + " sharedElapsedSecondsRaw=" + sharedElapsedSecondsRaw
            + " sharedValidSecondsBeforeCap=" + sharedValidBeforeCap
            + " sharedValidSecondsCapped=" + sharedValidCapped
            + " sharedReason=" + sharedReason
            + " maxCatchupGrantSeconds=" + maxGrant
            + " nowTicks=" + nowTicks
            + " gameTimerTicks=" + te.GameTimerTicks
            + " normalizedGameTimerTicks=" + normalizedPreviousTicks
            + " activeElapsedSecondsRaw=" + activeElapsedSecondsRaw
            + " activeElapsedSecondsCapped=" + activeElapsedSecondsCapped
            + " activeMaxElapsedSeconds=" + activeMaxElapsedSeconds
            + " activeEligible=" + activeEligible
            + " sharedEligible=" + sharedEligible
            + " growthSeconds=" + growthSeconds
            + " stageRateTicks=" + stageRateTicks
            + " accumulatedTicks=" + te.AccumulatedTicks
            + " secondsToNextStage=" + secondsToNextStage
            + " potentialTransitionsIgnoringWater=" + potentialTransitionsIgnoringWater
            + " watered=" + te.bWatered
            + " depletion=" + depletion
            + " waterCanStartCycle=" + canStartWaterCycle
            + " tankWater=" + tankWater
            + " collectorWater=" + collectorWater
            + " plotWater=" + plotWater
            + " lightPass=" + lightPass
            + " sunRaw=" + light.SunLight
            + " blockRaw=" + light.BlockLight
            + " hasSunLight=" + hasSunLight
            + " hasOpenSky=" + hasOpenSky
            + " hasBlockLight=" + hasBlockLight
            + " isNight=" + isNight
            + " temperature=" + temperature.ToString("0.00")
            + " tempPass=" + tempPass);
    }

    private static int EstimateSharedValidSecondsForDiagnostic(int elapsedSeconds, bool hasBlockLight, bool outsideOpenSky, bool isMushroom, bool advancedFarming, bool biomePass, bool tempPass, int previousSeconds, int nowSeconds, out string reason)
    {
        if (elapsedSeconds <= 0)
        {
            reason = "noElapsedSeconds";
            return 0;
        }

        if (!advancedFarming || isMushroom)
        {
            reason = !advancedFarming ? "advancedFarmingDisabled" : "mushroomIgnoresLightTemp";
            return elapsedSeconds;
        }

        if (!biomePass)
        {
            reason = "blockedByBiome";
            return 0;
        }

        if (!tempPass)
        {
            reason = "blockedByTemperature";
            return 0;
        }

        if (hasBlockLight)
        {
            reason = "blockLightTrustedWindow";
            int trusted = AdvancedFarmingRuntimePolicy.GetActiveLightTrustedSeconds();
            return elapsedSeconds < trusted ? elapsedSeconds : trusted;
        }

        if (outsideOpenSky)
        {
            reason = "estimatedDaylightSeconds";
            return EstimateDaylightSeconds(previousSeconds, nowSeconds);
        }

        reason = "blockedByLight";
        return 0;
    }

    private static string GetBlockName(BlockValue blockValue)
    {
        return blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>";
    }


    public static string DescribePlantTiming(WorldBase world, Vector3i pos, BlockValue blockValue, TileEntityPlantGrowingRebirth te)
    {
        if (world == null)
            return "[AdvancedFarming Probe] world=null pos=" + pos;

        if (te == null)
            return "[AdvancedFarming Probe] no crop tile entity pos=" + pos;

        int nowSeconds = RebirthUtilities.TotalGameSecondsPassed();
        ulong nowTicks = GameTimer.Instance.ticks;
        ulong previousTicks = te.GameTimerTicks;
        ulong elapsedTicks = nowTicks >= previousTicks ? nowTicks - previousTicks : 0UL;
        int elapsedSeconds = nowSeconds - te.LastProcessedWorldSeconds;

        int growthSeconds = AdvancedFarmingRuntimePolicy.GetEffectiveGrowthSeconds(world, pos);
        ulong stageRate = (ulong)Math.Max(1, growthSeconds * 20);
        float percent = stageRate > 0UL ? (float)te.AccumulatedTicks * 100f / (float)stageRate : 0f;

        bool advancedFarming = AdvancedFarmingRuntimePolicy.Enabled;
        bool isMushroom = IsMushroom(blockValue);
        string blockName = blockValue.Block != null ? blockValue.Block.GetBlockName() : "<null>";
        string biomeName = GetBiomeName(pos);
        bool isNight = !GameManager.Instance.World.IsDaytime();

        AdvancedFarmingLightService.LightResult light = AdvancedFarmingLightService.Evaluate(
            world,
            pos,
            3,
            3,
            isMushroom,
            advancedFarming);

        byte sun = light.SunLight;
        byte block = light.BlockLight;
        bool hasSunLight = light.HasSunLight;
        bool hasBlockLight = light.HasBlockLight;
        bool hasOpenSky = light.HasOpenSky;

        bool lightPass = (((hasSunLight || hasOpenSky) && !isNight) || hasBlockLight || isMushroom);
        float temp = AdvancedFarmingTemperatureService.Evaluate(world, pos, hasSunLight || hasOpenSky, advancedFarming).Temperature;
        bool tempPass = !advancedFarming || isMushroom || temp >= 45f;
        bool biomePass = !string.Equals(biomeName, "wasteland", StringComparison.OrdinalIgnoreCase);

        int depletion = GetWaterDepletion(blockValue, temp);
        int tankWater = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayWaterTank", 4, 1, 0);
        int collectorWater = RebirthUtilities.CheckForWater(world, pos, "FuriousRamsayDewCollector", 4, 1, 0);
        int plotWater = 0;
        TileEntityFarmPlotRebirth plot = world.GetTileEntity(pos + Vector3i.down) as TileEntityFarmPlotRebirth;
        if (plot != null)
            plotWater = plot.waterCount;

        bool hasWaterNow = isMushroom || te.bWatered || tankWater >= depletion || collectorWater >= depletion || plotWater >= depletion;
        int activeElapsedSeconds = (int)(elapsedTicks / 20UL);
        int maxElapsed = GetMaxPlantActiveElapsedSeconds(CollectPlants(world, pos));
        string activePath = "active-anchor-direct";

        return "[AdvancedFarming Probe] pos=" + pos
            + " block=" + blockName
            + " path=" + activePath
            + " growthSeconds=" + growthSeconds
            + " stageRateTicks=" + stageRate
            + " accumulatedTicks=" + te.AccumulatedTicks
            + " progressPercent=" + percent.ToString("0.00")
            + " remainingTicks=" + (te.AccumulatedTicks >= stageRate ? 0UL : stageRate - te.AccumulatedTicks)
            + " watered=" + te.bWatered
            + " nowSeconds=" + nowSeconds
            + " lastProcessedSeconds=" + te.LastProcessedWorldSeconds
            + " elapsedSeconds=" + elapsedSeconds
            + " activeElapsedSeconds=" + activeElapsedSeconds
            + " nowTicks=" + nowTicks
            + " gameTimerTicks=" + te.GameTimerTicks
            + " elapsedTicks=" + elapsedTicks
            + " minAreaProcessSeconds=" + AdvancedFarmingRuntimePolicy.GetMinAreaProcessSeconds()
            + " activeMaxElapsedSeconds=" + AdvancedFarmingRuntimePolicy.GetActiveDirectMaxElapsedSeconds()
            + " plantWakeDelayTicks=" + AdvancedFarmingRuntimePolicy.GetPlantWakeDelay(pos, 100UL)
            + " maxPlantElapsedSecondsInArea=" + maxElapsed
            + " lightPass=" + lightPass
            + " sunRaw=" + sun
            + " blockRaw=" + block
            + " hasSunLight=" + hasSunLight
            + " hasBlockLight=" + hasBlockLight
            + " hasOpenSky=" + hasOpenSky
            + " isNight=" + isNight
            + " temp=" + temp.ToString("0.00")
            + " tempPass=" + tempPass
            + " biome=" + biomeName
            + " biomePass=" + biomePass
            + " depletion=" + depletion
            + " waterPass=" + hasWaterNow
            + " tankWater=" + tankWater
            + " collectorWater=" + collectorWater
            + " plotWater=" + plotWater;
    }

    private static void HaltPlant(TileEntityPlantGrowingRebirth te, int nowSeconds, ulong nowTicks)
    {
        if (te == null)
            return;

        te.GameTimerTicks = nowTicks;
        te.LastProcessedWorldSeconds = nowSeconds;
        te.setModified();
    }

    private static int ResolveWaterProviderMax(BlockValue blockValue, TileEntityWaterTankRebirth te)
    {
        if (blockValue.Block != null && blockValue.Block.Properties != null && blockValue.Block.Properties.Values.ContainsKey("MaxWater"))
        {
            if (int.TryParse(blockValue.Block.Properties.Values["MaxWater"], out int max) && max > 0)
                return max;
        }

        return te.waterMax > 0 ? te.waterMax : 400;
    }

    private static int ResolveFarmPlotMax(BlockValue blockValue, TileEntityFarmPlotRebirth te)
    {
        if (blockValue.Block != null && blockValue.Block.Properties != null && blockValue.Block.Properties.Values.ContainsKey("WaterMax"))
        {
            if (int.TryParse(blockValue.Block.Properties.Values["WaterMax"], out int max) && max > 0)
                return max;
        }

        return te.waterMax > 0 ? te.waterMax : 6;
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

    private static int GetWaterDepletion(BlockValue blockValue, float temp)
    {
        return RebirthUtilities.GetCropWaterDepletion(blockValue, temp);
    }

    private static bool TryAdvanceToNextStage(
        WorldBase world,
        Vector3i pos,
        BlockValue currentValue,
        TileEntityPlantGrowingRebirth currentTe,
        out BlockValue committedValue,
        out TileEntityPlantGrowingRebirth committedTe)
    {
        committedValue = currentValue;
        committedTe = currentTe;
        if (world == null || currentTe == null)
            return false;

        BlockValue liveCurrent = world.GetBlock(pos);
        if (liveCurrent.type != currentValue.type)
            return false;

        string nextStage = ResolveNextStageName(currentValue);
        if (string.IsNullOrEmpty(nextStage))
            return false;

        BlockValue next = Block.GetBlockValue(nextStage);
        if (next.isair || next.Block == null)
            return false;

        next.rotation = currentValue.rotation;
        next.meta = currentValue.meta;

        BiomeDefinition biome = ((World)world).GetBiome(pos.x, pos.z);
        if (biome != null && biome.Replacements != null && biome.Replacements.ContainsKey(next.type))
            next.type = biome.Replacements[next.type];

        byte requestedRotation = next.rotation;
        byte requestedMeta = next.meta;
        next = BlockPlaceholderMap.Instance.Replace(next, world.GetGameRandom(), pos.x, pos.z);
        next.rotation = requestedRotation;
        next.meta = requestedMeta;
        next.meta2 = (byte)0;
        if (next.isair || next.Block == null)
            return false;
        if (next.type == currentValue.type && next.meta == currentValue.meta && next.meta2 == currentValue.meta2)
            return false;

        RebirthCropProvenanceSnapshot provenance = RebirthCropProvenanceAdapter.CaptureSnapshot(world, pos);
        ulong oldIncarnation = currentTe.PlantIncarnation;
        ulong oldRevision = currentTe.StateRevision;
        ulong oldAccumulated = currentTe.AccumulatedTicks;
        bool oldWatered = currentTe.bWatered;
        ulong oldGameTicks = currentTe.GameTimerTicks;
        int oldWorldSeconds = currentTe.LastProcessedWorldSeconds;
        AdvancedFarmingAuthoritativeLightState oldLight = currentTe.AuthoritativeLightState;

        SetExposureNeutralPlantBlockRpc(world, pos, currentValue, next);
        BlockValue liveNext = world.GetBlock(pos);
        TileEntityPlantGrowingRebirth nextTe = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
        if (liveNext.type != next.type || nextTe == null)
        {
            // The native replacement did not commit the expected crop incarnation. Compensate
            // back to the old block where possible and restore the paid state so no water/progress
            // is consumed by a failed transition.
            BlockValue rollbackFrom = liveNext;
            if (liveNext.type != currentValue.type)
                SetExposureNeutralPlantBlockRpc(world, pos, rollbackFrom, currentValue);
            TileEntityPlantGrowingRebirth rollbackTe = world.GetTileEntity(pos) as TileEntityPlantGrowingRebirth;
            if (rollbackTe != null)
            {
                rollbackTe.PlantIncarnation = oldIncarnation;
                rollbackTe.StateRevision = oldRevision;
                rollbackTe.AccumulatedTicks = oldAccumulated;
                rollbackTe.bWatered = oldWatered;
                rollbackTe.GameTimerTicks = oldGameTicks;
                rollbackTe.LastProcessedWorldSeconds = oldWorldSeconds;
                rollbackTe.AuthoritativeLightState = oldLight;
                rollbackTe.setModified();
                RebirthCropProvenanceAdapter.RestoreSnapshot(world, pos, provenance);
                committedValue = world.GetBlock(pos);
                committedTe = rollbackTe;
            }
            return false;
        }

        RebirthCropProvenanceAdapter.RestoreSnapshot(world, pos, provenance);
        nextTe.AdoptStageTransitionIdentity(oldIncarnation, oldRevision);
        nextTe.AccumulatedTicks = 0UL;
        nextTe.bWatered = false;
        nextTe.AuthoritativeLightState = oldLight;
        nextTe.setModified();
        committedValue = liveNext;
        committedTe = nextTe;
        return true;
    }

    private static void SetExposureNeutralPlantBlockRpc(WorldBase world, Vector3i pos, BlockValue oldValue, BlockValue newValue)
    {
        if (world == null)
            return;

        bool skipLightUpdate = Harmony_RebirthAdvancedFarmingLightStateRefreshPatch.IsExposureNeutralPlantLikeSwap(oldValue, newValue);
        world.SetBlockRPC(new BlockChangeInfo((BlockValueRef)pos, newValue, !skipLightUpdate));
    }

    private static string ResolveNextStageName(BlockValue currentValue)
    {
        Block block = currentValue.Block;
        if (block == null || block.Properties == null)
            return string.Empty;

        BlockPlantGrowingRebirth rebirthPlant = block as BlockPlantGrowingRebirth;
        if (rebirthPlant != null && !string.IsNullOrEmpty(rebirthPlant.nextStage))
            return rebirthPlant.nextStage;

        if (block.Properties.Values != null && block.Properties.Values.ContainsKey("NextStage"))
            return block.Properties.Values["NextStage"];

        if (block.Properties.Classes != null && block.Properties.Classes.ContainsKey("PlantGrowing"))
        {
            DynamicProperties growProps = block.Properties.Classes["PlantGrowing"];
            if (growProps != null && growProps.Values != null && growProps.Values.ContainsKey("Next"))
                return growProps.Values["Next"];
        }

        return string.Empty;
    }
}
