#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GamePath;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// One-command Hybrid Path Smoothing performance benchmark.
/// The benchmark is compiled only in Debug builds and prints no per-frame or
/// per-entity output while measuring.
/// </summary>
[Preserve]
public sealed class ConsoleCmdRebirthHybridPathPerformance : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient
    {
        get { return false; }
    }

    public override string[] getCommands()
    {
        return new[] { "rbpathperf", "rbpathbenchmark" };
    }

    public override string getDescription()
    {
        return "Runs one silent Hybrid Path Smoothing performance benchmark and prints a final report.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbpathperf\n"
             + "  rbpathperf [measurementSeconds]\n"
             + "  rbpathperf stop\n"
             + "\n"
             + "The command performs a 5-second warm-up followed by a silent measurement.\n"
             + "It automatically resets counters, gathers frame, GC, entity, pathing, and\n"
             + "physics-query statistics, stops by GameUpdate deadline, and prints one final report.\n"
             + "Default measurement duration: 60 seconds. Allowed range: 20-180 seconds.\n"
             + "Use 'rbpathperf stop' to cancel a warm-up or measurement and immediately reset it.\n";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        if (parameters != null && parameters.Count > 1)
        {
            Log.Out(getHelp());
            return;
        }

        if (parameters != null
            && parameters.Count == 1
            && string.Equals(parameters[0], "stop", StringComparison.OrdinalIgnoreCase))
        {
            Log.Out(RebirthHybridPathPerformanceBenchmark.Stop());
            return;
        }

        float seconds = 60f;
        if (parameters != null && parameters.Count == 1)
        {
            float parsed;
            if (float.TryParse(
                parameters[0],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed))
            {
                seconds = parsed;
            }
            else
            {
                Log.Out(getHelp());
                return;
            }
        }

        EntityPlayer requester = ResolveRequester(senderInfo);
        Log.Out(RebirthHybridPathPerformanceBenchmark.Start(requester, seconds));
    }

    private static EntityPlayer ResolveRequester(CommandSenderInfo senderInfo)
    {
        GameManager gameManager = GameManager.Instance;
        World world = gameManager != null ? gameManager.World : null;
        if (world == null)
            return null;

        if (senderInfo.RemoteClientInfo != null)
            return world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;

        return world.GetPrimaryPlayer();
    }
}

/// <summary>
/// Debug-only lifecycle pump. The benchmark deadline is checked from
/// ModEvents.GameUpdate, not from an entity movement callback, so it completes
/// even when the selected population becomes inactive or despawns.
/// </summary>
public sealed class RebirthHybridPathPerformanceModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        ModEvents.GameUpdate.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        RebirthHybridPathPerformanceBenchmark.Pump();
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthHybridPathPerformanceBenchmark.Abort("worldShuttingDown");
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthHybridPathPerformanceBenchmark.Abort("gameShutdown");
    }
}

/// <summary>
/// Allocation-free during the measured interval apart from any allocations
/// performed internally by the game itself. All sorting and report formatting
/// occurs after measurement has stopped.
/// </summary>
public static class RebirthHybridPathPerformanceBenchmark
{
    private const float WarmupSeconds = 5f;
    private const float DefaultMeasurementSeconds = 60f;
    private const float MinMeasurementSeconds = 20f;
    private const float MaxMeasurementSeconds = 180f;
    private const float CensusRadius = 80f;
    private const int ExpectedMaximumFramesPerSecond = 360;

    private enum BenchmarkPhase
    {
        Inactive,
        Warmup,
        Measuring
    }

    private struct PopulationSnapshot
    {
        public int Alive;
        public int Zombies;
        public int Attacking;
        public int AttackingReferencePlayer;
        public int MoveActive;
        public int UsablePath;
    }

    private static BenchmarkPhase phase;
    private static int requesterPlayerId = -1;
    private static bool smoothingEnabledAtStart;
    private static bool previousDebugLogging;
    private static float requestedMeasurementSeconds;
    private static float warmupEndRealtime;
    private static float measurementStartRealtime;
    private static float measurementEndRealtime;
    private static float lastFrameRealtime;
    private static ulong startGameTicks;
    private static int startGc0;
    private static int startGc1;
    private static int startGc2;
    private static long startManagedBytes;

    private static float[] frameSeconds = new float[0];
    private static int frameSampleCount;
    private static int droppedFrameSamples;

    private static long prefixCalls;
    private static long prefixTotalStopwatchTicks;
    private static long prefixMaxStopwatchTicks;
    private static long clearanceCalls;
    private static long clearanceHits;
    private static long clearanceTotalStopwatchTicks;
    private static long clearanceMaxStopwatchTicks;

    private static readonly HashSet<int> evaluatedEntityIds = new HashSet<int>();
    private static readonly List<Entity> censusEntities = new List<Entity>(256);

    private static PopulationSnapshot populationStart;

    public static bool IsMeasuring
    {
        get { return phase == BenchmarkPhase.Measuring; }
    }

    public static string Start(EntityPlayer requester, float seconds)
    {
        if (phase != BenchmarkPhase.Inactive)
        {
            return "[RebirthHybridPathPerf] START FAILED benchmarkAlreadyRunning phase="
                + phase.ToString().ToLowerInvariant();
        }

        GameManager gameManager = GameManager.Instance;
        World world = gameManager != null ? gameManager.World : null;
        if (world == null)
            return "[RebirthHybridPathPerf] START FAILED world=<null>";

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer)
            return "[RebirthHybridPathPerf] START FAILED authoritativeServer=False";

        requestedMeasurementSeconds = Mathf.Clamp(
            seconds <= 0f ? DefaultMeasurementSeconds : seconds,
            MinMeasurementSeconds,
            MaxMeasurementSeconds);

        requesterPlayerId = requester != null ? requester.entityId : -1;
        smoothingEnabledAtStart = RebirthHybridPathSmoothing.Enabled;
        previousDebugLogging = RebirthHybridPathSmoothing.DebugLogging;
        RebirthHybridPathSmoothing.DebugLogging = false;

        int frameCapacity = Mathf.Max(
            1024,
            Mathf.CeilToInt(requestedMeasurementSeconds * ExpectedMaximumFramesPerSecond) + 256);
        frameSeconds = new float[frameCapacity];
        frameSampleCount = 0;
        droppedFrameSamples = 0;

        // Force the HashSet backing storage to be created before measurement.
        evaluatedEntityIds.Clear();
        for (int i = 0; i < 128; i++)
            evaluatedEntityIds.Add(int.MinValue + i);
        evaluatedEntityIds.Clear();

        ResetTimingAccumulators();

        float now = Time.realtimeSinceStartup;
        warmupEndRealtime = now + WarmupSeconds;
        measurementStartRealtime = 0f;
        measurementEndRealtime = 0f;
        lastFrameRealtime = 0f;
        phase = BenchmarkPhase.Warmup;

        return "[RebirthHybridPathPerf] START"
            + " smoothing=" + (smoothingEnabledAtStart ? "On" : "Off")
            + " warmupSeconds=" + WarmupSeconds.ToString("0")
            + " measurementSeconds=" + requestedMeasurementSeconds.ToString("0")
            + " radius=" + CensusRadius.ToString("0")
            + " requester=" + requesterPlayerId
            + " output=finalReportOnly";
    }

    public static void Pump()
    {
        if (phase == BenchmarkPhase.Inactive)
            return;

        float now = Time.realtimeSinceStartup;

        if (phase == BenchmarkPhase.Warmup)
        {
            if (now >= warmupEndRealtime)
                BeginMeasurement(now);
            return;
        }

        float delta = now - lastFrameRealtime;
        lastFrameRealtime = now;
        if (delta > 0f && delta < 5f)
        {
            if (frameSampleCount < frameSeconds.Length)
                frameSeconds[frameSampleCount++] = delta;
            else
                droppedFrameSamples++;
        }

        if (now >= measurementEndRealtime)
            Log.Out(Finish(now, "completed"));
    }

    public static string Stop()
    {
        if (phase == BenchmarkPhase.Inactive)
            return "[RebirthHybridPathPerf] STOP noBenchmarkRunning";

        float now = Time.realtimeSinceStartup;
        if (phase == BenchmarkPhase.Warmup)
        {
            string result = "[RebirthHybridPathPerf] RESULT"
                + "\n  completion=cancelled phase=warmup"
                + " smoothing=" + (smoothingEnabledAtStart ? "On" : "Off")
                + " measuredSeconds=0.00";
            CleanupAfterRun();
            return result;
        }

        return Finish(now, "cancelled");
    }

    public static void Abort(string reason)
    {
        if (phase == BenchmarkPhase.Inactive)
            return;

        CleanupAfterRun();

        if (!string.IsNullOrEmpty(reason))
            Log.Out("[RebirthHybridPathPerf] ABORTED reason=" + reason);
    }

    public static long BeginPathEvaluation()
    {
        return phase == BenchmarkPhase.Measuring
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
    }

    public static void EndPathEvaluation(long startTimestamp, EntityMoveHelper moveHelper)
    {
        if (startTimestamp == 0L || phase != BenchmarkPhase.Measuring)
            return;

        long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
        prefixCalls++;
        prefixTotalStopwatchTicks += elapsed;
        if (elapsed > prefixMaxStopwatchTicks)
            prefixMaxStopwatchTicks = elapsed;

        EntityAlive entity = moveHelper != null ? moveHelper.entity : null;
        if (entity != null)
            evaluatedEntityIds.Add(entity.entityId);
    }

    public static long BeginClearanceQuery()
    {
        return phase == BenchmarkPhase.Measuring
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
    }

    public static void EndClearanceQuery(long startTimestamp, bool hitSomething)
    {
        if (startTimestamp == 0L || phase != BenchmarkPhase.Measuring)
            return;

        long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
        clearanceCalls++;
        if (hitSomething)
            clearanceHits++;
        clearanceTotalStopwatchTicks += elapsed;
        if (elapsed > clearanceMaxStopwatchTicks)
            clearanceMaxStopwatchTicks = elapsed;
    }

    private static void BeginMeasurement(float now)
    {
        phase = BenchmarkPhase.Measuring;
        measurementStartRealtime = now;
        measurementEndRealtime = now + requestedMeasurementSeconds;
        lastFrameRealtime = now;
        startGameTicks = GameTimer.Instance != null ? GameTimer.Instance.ticks : 0UL;
        startGc0 = GC.CollectionCount(0);
        startGc1 = GC.CollectionCount(1);
        startGc2 = GC.CollectionCount(2);
        startManagedBytes = GC.GetTotalMemory(false);

        frameSampleCount = 0;
        droppedFrameSamples = 0;
        evaluatedEntityIds.Clear();
        ResetTimingAccumulators();
        RebirthHybridPathSmoothing.ResetCounters();
        populationStart = CapturePopulation();
    }

    private static string Finish(float now, string reason)
    {
        if (phase != BenchmarkPhase.Measuring)
            return "[RebirthHybridPathPerf] STOP noActiveMeasurement";

        // Stop instrumentation before sorting, census, allocation queries, and
        // report construction so none of that work contaminates the sample.
        phase = BenchmarkPhase.Inactive;

        float measuredSeconds = Mathf.Max(0.001f, now - measurementStartRealtime);
        ulong endGameTicks = GameTimer.Instance != null ? GameTimer.Instance.ticks : startGameTicks;
        int endGc0 = GC.CollectionCount(0);
        int endGc1 = GC.CollectionCount(1);
        int endGc2 = GC.CollectionCount(2);
        long endManagedBytes = GC.GetTotalMemory(false);
        PopulationSnapshot populationEnd = CapturePopulation();
        RebirthHybridPathCounterSnapshot counters = RebirthHybridPathSmoothing.GetCounterSnapshot();

        string report = BuildReport(
            reason,
            measuredSeconds,
            endGameTicks,
            endGc0,
            endGc1,
            endGc2,
            endManagedBytes,
            populationEnd,
            counters);

        CleanupAfterRun();
        return report;
    }

    private static void CleanupAfterRun()
    {
        RebirthHybridPathSmoothing.DebugLogging = previousDebugLogging;
        phase = BenchmarkPhase.Inactive;
        requesterPlayerId = -1;
        frameSeconds = new float[0];
        frameSampleCount = 0;
        droppedFrameSamples = 0;
        evaluatedEntityIds.Clear();
        ResetTimingAccumulators();
        warmupEndRealtime = 0f;
        measurementStartRealtime = 0f;
        measurementEndRealtime = 0f;
        lastFrameRealtime = 0f;
    }

    private static string BuildReport(
        string reason,
        float measuredSeconds,
        ulong endGameTicks,
        int endGc0,
        int endGc1,
        int endGc2,
        long endManagedBytes,
        PopulationSnapshot populationEnd,
        RebirthHybridPathCounterSnapshot counters)
    {
        if (frameSampleCount > 1)
            Array.Sort(frameSeconds, 0, frameSampleCount);

        double frameTotalSeconds = 0.0;
        for (int i = 0; i < frameSampleCount; i++)
            frameTotalSeconds += frameSeconds[i];

        double averageFrameMs = frameSampleCount > 0
            ? frameTotalSeconds * 1000.0 / frameSampleCount
            : 0.0;
        double averageFps = frameTotalSeconds > 0.0
            ? frameSampleCount / frameTotalSeconds
            : 0.0;
        double p50FrameMs = PercentileFrameMilliseconds(0.50f);
        double p95FrameMs = PercentileFrameMilliseconds(0.95f);
        double p99FrameMs = PercentileFrameMilliseconds(0.99f);
        double maxFrameMs = frameSampleCount > 0
            ? frameSeconds[frameSampleCount - 1] * 1000.0
            : 0.0;
        double onePercentLowFps = p99FrameMs > 0.0 ? 1000.0 / p99FrameMs : 0.0;

        double timestampFrequency = System.Diagnostics.Stopwatch.Frequency;
        double prefixTotalMs = prefixTotalStopwatchTicks * 1000.0 / timestampFrequency;
        double prefixAverageUs = prefixCalls > 0
            ? prefixTotalStopwatchTicks * 1000000.0 / timestampFrequency / prefixCalls
            : 0.0;
        double prefixMaxUs = prefixMaxStopwatchTicks * 1000000.0 / timestampFrequency;
        double prefixMsPerSecond = prefixTotalMs / measuredSeconds;

        double clearanceTotalMs = clearanceTotalStopwatchTicks * 1000.0 / timestampFrequency;
        double clearanceAverageUs = clearanceCalls > 0
            ? clearanceTotalStopwatchTicks * 1000000.0 / timestampFrequency / clearanceCalls
            : 0.0;
        double clearanceMaxUs = clearanceMaxStopwatchTicks * 1000000.0 / timestampFrequency;
        double clearanceCallsPerSecond = clearanceCalls / measuredSeconds;

        double gameTicksPerSecond = (endGameTicks - startGameTicks) / measuredSeconds;
        long managedDelta = endManagedBytes - startManagedBytes;

        StringBuilder report = new StringBuilder(1800);
        report.Append("[RebirthHybridPathPerf] RESULT")
            .Append("\n  completion=").Append(reason)
            .Append(" smoothing=").Append(smoothingEnabledAtStart ? "On" : "Off")
            .Append(" measuredSeconds=").Append(measuredSeconds.ToString("0.00"))
            .Append(" gameTicksPerSecond=").Append(gameTicksPerSecond.ToString("0.00"))
            .Append("\n  frames samples=").Append(frameSampleCount)
            .Append(" dropped=").Append(droppedFrameSamples)
            .Append(" averageFPS=").Append(averageFps.ToString("0.00"))
            .Append(" onePercentLowFPS=").Append(onePercentLowFps.ToString("0.00"))
            .Append(" averageMs=").Append(averageFrameMs.ToString("0.000"))
            .Append(" p50Ms=").Append(p50FrameMs.ToString("0.000"))
            .Append(" p95Ms=").Append(p95FrameMs.ToString("0.000"))
            .Append(" p99Ms=").Append(p99FrameMs.ToString("0.000"))
            .Append(" maxMs=").Append(maxFrameMs.ToString("0.000"))
            .Append("\n  hybrid calls=").Append(prefixCalls)
            .Append(" uniqueEntities=").Append(evaluatedEntityIds.Count)
            .Append(" totalMs=").Append(prefixTotalMs.ToString("0.000"))
            .Append(" msPerSecond=").Append(prefixMsPerSecond.ToString("0.000"))
            .Append(" averageUs=").Append(prefixAverageUs.ToString("0.000"))
            .Append(" maxUs=").Append(prefixMaxUs.ToString("0.000"))
            .Append("\n  clearance calls=").Append(clearanceCalls)
            .Append(" callsPerSecond=").Append(clearanceCallsPerSecond.ToString("0.00"))
            .Append(" hits=").Append(clearanceHits)
            .Append(" totalMs=").Append(clearanceTotalMs.ToString("0.000"))
            .Append(" averageUs=").Append(clearanceAverageUs.ToString("0.000"))
            .Append(" maxUs=").Append(clearanceMaxUs.ToString("0.000"))
            .Append("\n  decisions evaluated=").Append(counters.Evaluated)
            .Append(" activated=").Append(counters.Activated)
            .Append(" directUpdates=").Append(counters.DirectUpdates)
            .Append(" blocked=").Append(counters.Blocked)
            .Append(" vertical=").Append(counters.Vertical)
            .Append(" route=").Append(counters.Route)
            .Append(" specialMove=").Append(counters.SpecialMove)
            .Append(" cooldown=").Append(counters.Cooldown)
            .Append(" noPath=").Append(counters.NoPath)
            .Append(" noTarget=").Append(counters.NoTarget)
            .Append(" stuck=").Append(counters.Stuck)
            .Append(" stopRange=").Append(counters.StopRange)
            .Append(" clearanceSkipped=").Append(counters.ClearanceSkipped)
            .Append(" verticalPathScans=").Append(counters.VerticalPathScans)
            .Append(" verticalPathCacheHits=").Append(counters.VerticalPathCacheHits)
            .Append("\n  populationStart alive=").Append(populationStart.Alive)
            .Append(" zombies=").Append(populationStart.Zombies)
            .Append(" attacking=").Append(populationStart.Attacking)
            .Append(" attackingPlayer=").Append(populationStart.AttackingReferencePlayer)
            .Append(" moveActive=").Append(populationStart.MoveActive)
            .Append(" usablePath=").Append(populationStart.UsablePath)
            .Append("\n  populationEnd alive=").Append(populationEnd.Alive)
            .Append(" zombies=").Append(populationEnd.Zombies)
            .Append(" attacking=").Append(populationEnd.Attacking)
            .Append(" attackingPlayer=").Append(populationEnd.AttackingReferencePlayer)
            .Append(" moveActive=").Append(populationEnd.MoveActive)
            .Append(" usablePath=").Append(populationEnd.UsablePath)
            .Append("\n  gc gen0=").Append(endGc0 - startGc0)
            .Append(" gen1=").Append(endGc1 - startGc1)
            .Append(" gen2=").Append(endGc2 - startGc2)
            .Append(" managedStartMB=").Append(BytesToMegabytes(startManagedBytes).ToString("0.00"))
            .Append(" managedEndMB=").Append(BytesToMegabytes(endManagedBytes).ToString("0.00"))
            .Append(" managedDeltaMB=").Append(BytesToMegabytes(managedDelta).ToString("+0.00;-0.00;0.00"))
            .Append("\n  comparisonKey=Use_the_same_save_population_location_camera_and_duration_for_Off_and_On_runs.");

        return report.ToString();
    }

    private static PopulationSnapshot CapturePopulation()
    {
        PopulationSnapshot result = new PopulationSnapshot();

        GameManager gameManager = GameManager.Instance;
        World world = gameManager != null ? gameManager.World : null;
        if (world == null)
            return result;

        EntityPlayer referencePlayer = requesterPlayerId >= 0
            ? world.GetEntity(requesterPlayerId) as EntityPlayer
            : null;
        if (referencePlayer == null)
            referencePlayer = world.GetPrimaryPlayer();
        if (referencePlayer == null)
            return result;

        censusEntities.Clear();
        world.GetEntitiesInBounds(
            typeof(EntityAlive),
            new Bounds(
                referencePlayer.position,
                new Vector3(CensusRadius * 2f, CensusRadius, CensusRadius * 2f)),
            censusEntities);

        for (int i = 0; i < censusEntities.Count; i++)
        {
            EntityAlive entity = censusEntities[i] as EntityAlive;
            if (entity == null || entity.IsDead() || entity is EntityPlayer)
                continue;

            result.Alive++;
            if (entity is EntityZombie)
                result.Zombies++;

            EntityAlive target = entity.GetAttackTarget();
            if (target != null && !target.IsDead())
            {
                result.Attacking++;
                if (target == referencePlayer)
                    result.AttackingReferencePlayer++;
            }

            EntityMoveHelper moveHelper = entity.moveHelper;
            if (moveHelper != null && moveHelper.IsActive)
                result.MoveActive++;

            PathNavigate navigator = entity.getNavigator();
            PathEntity path = navigator != null ? navigator.getPath() : null;
            if (path != null
                && path.points != null
                && path.pathLength >= 2
                && !path.isFinished())
            {
                result.UsablePath++;
            }
        }

        return result;
    }

    private static double PercentileFrameMilliseconds(float percentile)
    {
        if (frameSampleCount <= 0)
            return 0.0;

        int index = Mathf.Clamp(
            Mathf.CeilToInt(frameSampleCount * percentile) - 1,
            0,
            frameSampleCount - 1);
        return frameSeconds[index] * 1000.0;
    }

    private static double BytesToMegabytes(long bytes)
    {
        return bytes / (1024.0 * 1024.0);
    }

    private static void ResetTimingAccumulators()
    {
        prefixCalls = 0L;
        prefixTotalStopwatchTicks = 0L;
        prefixMaxStopwatchTicks = 0L;
        clearanceCalls = 0L;
        clearanceHits = 0L;
        clearanceTotalStopwatchTicks = 0L;
        clearanceMaxStopwatchTicks = 0L;
    }
}
#endif
