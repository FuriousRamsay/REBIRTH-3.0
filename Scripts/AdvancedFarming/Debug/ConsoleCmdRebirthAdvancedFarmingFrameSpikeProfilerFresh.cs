#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Platform;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthAdvancedFarmingFrameSpikeProfilerFresh : ConsoleCmdAbstract
{
    private static RebirthAdvancedFarmingFrameSpikeProfilerRunner s_runner;
    private static RebirthAdvancedFarmingIsolationProfilerRunner s_isolationRunner;

    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbafperf", "rbafspikes", "rbfarmspikes" };
    }

    public override string getDescription()
    {
        return "Runs an automated Advanced Farming frame-spike profiler with GC and rolling pre-spike AF counter history.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbafperf auto [durationSeconds] [intervalSeconds] [spikeMs] [severeMs] [maxSpikes] [warmupSeconds]\n"
             + "  rbafperf isolate [totalSeconds] [phaseWarmupSeconds] [spikeMs] [severeMs]\n"
             + "  rbafperf movement [totalSeconds] [phaseWarmupSeconds] [spikeMs] [severeMs]\n"
             + "  rbafperf stop\n"
             + "  rbafperf status\n"
             + "\n"
             + "Recommended:\n"
             + "  rbafperf auto 180 10 25 40 64 5\n"
             + "  rbafperf isolate 360 5 25 40\n"
             + "  rbafperf movement 360 5 25 40\n"
             + "\n"
             + "The auto command is non-invasive: it does not disable farming systems or mutate crop behavior.\n"
             + "It samples every frame, stores frame spikes without live per-spike logging, correlates them with Advanced Farming counters,\n"
             + "prints interval summaries, and restores the previous sampling state at completion.\n"
             + "The isolate command is invasive diagnostic-only: it pauses one Advanced Farming subsystem per phase, includes an aggressive hard-disable phase, restores everything, and runs 9 phases across the requested total duration.\n"
             + "The movement command runs the same isolation phases but labels the run for repeated god-mode flight or movement-stutter reproduction.\n"
             + "Toolbelt messages announce start, intervals/phases, and completion.";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string cmd = (_params == null || _params.Count == 0)
            ? "auto"
            : (_params[0] ?? string.Empty).Trim().ToLowerInvariant();

        if (cmd == "help" || cmd == "?")
        {
            Log.Out(getHelp());
            return;
        }

        if (cmd == "stop" || cmd == "cancel")
        {
            StopRunner(true);
            return;
        }

        if (cmd == "status")
        {
            Log.Out("[AFFrameSpikeProfiler] autoRunning=" + (s_runner != null)
                + " isolateRunning=" + (s_isolationRunner != null)
                + " isolation=" + AdvancedFarmingCatchupService.BuildDiagnosticIsolationStatus());
            return;
        }

        if (cmd == "isolate" || cmd == "isolation" ||
            cmd == "movement" || cmd == "move" || cmd == "fly")
        {
            bool movementScenario = cmd == "movement" || cmd == "move" || cmd == "fly";
            int totalSeconds = ParseInt(_params, 1, 360, 80, 900);
            int phaseWarmupSeconds = ParseInt(_params, 2, 5, 0, 20);
            double isolateSpikeMs = ParseDouble(_params, 3, 25d, 5d, 250d);
            double isolateSevereMs = ParseDouble(_params, 4, 40d, isolateSpikeMs, 500d);

            StopRunner(false);

            GameObject isolateGo = new GameObject("RebirthAdvancedFarmingIsolationProfilerRunner");
            UnityEngine.Object.DontDestroyOnLoad(isolateGo);
            s_isolationRunner = isolateGo.AddComponent<RebirthAdvancedFarmingIsolationProfilerRunner>();
            s_isolationRunner.Configure(totalSeconds, phaseWarmupSeconds, isolateSpikeMs, isolateSevereMs, movementScenario, OnIsolationRunnerFinished);

            Log.Out("[AFIsolationProfiler] Started scenario=" + (movementScenario ? "movement" : "stationary")
                + " totalSeconds=" + totalSeconds
                + " phaseWarmupSeconds=" + phaseWarmupSeconds
                + " spikeMs=" + isolateSpikeMs.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + " severeMs=" + isolateSevereMs.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + (movementScenario
                    ? ". Reproduce the same short god-mode flight loop continuously until completion."
                    : ". Keep camera/player still until completion."));
            RebirthAdvancedFarmingFrameSpikeProfilerRunner.NotifyToolbelt(
                movementScenario
                    ? "AF movement isolation started for " + totalSeconds + "s. Repeat the same short flight loop."
                    : "AF isolation profiler started for " + totalSeconds + "s. Keep camera/player still.");
            return;
        }

        if (cmd != "auto" && cmd != "run" && cmd != "start")
        {
            Log.Out("[AFFrameSpikeProfiler] Unknown subcommand: " + cmd);
            Log.Out(getHelp());
            return;
        }

        int durationSeconds = ParseInt(_params, 1, 180, 10, 600);
        int intervalSeconds = ParseInt(_params, 2, 10, 2, 60);
        double spikeMs = ParseDouble(_params, 3, 25d, 5d, 250d);
        double severeMs = ParseDouble(_params, 4, 40d, spikeMs, 500d);
        int maxSpikes = ParseInt(_params, 5, 64, 8, 256);
        int warmupSeconds = ParseInt(_params, 6, 5, 0, 60);
        if (warmupSeconds >= durationSeconds) warmupSeconds = Math.Max(0, durationSeconds - 1);

        StopRunner(false);

        GameObject go = new GameObject("RebirthAdvancedFarmingFrameSpikeProfilerRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<RebirthAdvancedFarmingFrameSpikeProfilerRunner>();
        s_runner.Configure(durationSeconds, intervalSeconds, spikeMs, severeMs, maxSpikes, warmupSeconds, OnRunnerFinished);

        Log.Out("[AFFrameSpikeProfiler] Started durationSeconds=" + durationSeconds
            + " intervalSeconds=" + intervalSeconds
            + " spikeMs=" + spikeMs.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " severeMs=" + severeMs.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
            + " maxSpikes=" + maxSpikes
            + " warmupSeconds=" + warmupSeconds
            + ". Keep camera/player still until completion.");
        RebirthAdvancedFarmingFrameSpikeProfilerRunner.NotifyToolbelt("AF spike profiler started. Keep camera/player still until completion.");
    }

    private static int ParseInt(List<string> args, int index, int defaultValue, int min, int max)
    {
        if (args == null || args.Count <= index)
            return defaultValue;

        int value;
        if (!int.TryParse(args[index], out value))
            return defaultValue;

        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private static double ParseDouble(List<string> args, int index, double defaultValue, double min, double max)
    {
        if (args == null || args.Count <= index)
            return defaultValue;

        double value;
        if (!double.TryParse(args[index], out value) || double.IsNaN(value) || double.IsInfinity(value))
            return defaultValue;

        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    private static void StopRunner(bool log)
    {
        bool hadRunner = s_runner != null || s_isolationRunner != null;

        if (s_runner != null)
        {
            try
            {
                s_runner.AbortAndRestore();
            }
            catch
            {
            }

            UnityEngine.Object.Destroy(s_runner.gameObject);
            s_runner = null;
        }

        if (s_isolationRunner != null)
        {
            try
            {
                s_isolationRunner.AbortAndRestore();
            }
            catch
            {
            }

            UnityEngine.Object.Destroy(s_isolationRunner.gameObject);
            s_isolationRunner = null;
        }

        if (log)
        {
            if (hadRunner)
            {
                Log.Out("[AFFrameSpikeProfiler] Stopped and restored sampling/isolation state.");
                RebirthAdvancedFarmingFrameSpikeProfilerRunner.NotifyToolbelt("AF profiler stopped and state restored.");
            }
            else
            {
                Log.Out("[AFFrameSpikeProfiler] No run is active.");
            }
        }
    }

    private static void OnRunnerFinished(RebirthAdvancedFarmingFrameSpikeProfilerRunner runner)
    {
        if (s_runner == runner)
            s_runner = null;
    }

    private static void OnIsolationRunnerFinished(RebirthAdvancedFarmingIsolationProfilerRunner runner)
    {
        if (s_isolationRunner == runner)
            s_isolationRunner = null;
    }
}

public class RebirthAdvancedFarmingFrameSpikeProfilerRunner : MonoBehaviour
{
    private struct SpikeRecord
    {
        public int Index;
        public double TimeSeconds;
        public double FrameMs;
        public double Fps;
        public long MemoryDeltaBytes;
        public long TotalManagedMemoryBytes;
        public int Gc0Delta;
        public int Gc1Delta;
        public int Gc2Delta;
        public int TotalGc0;
        public int TotalGc1;
        public int TotalGc2;
        public AdvancedFarmingPerfSnapshotService.SampleSnapshot Before;
        public AdvancedFarmingPerfSnapshotService.SampleSnapshot After;
        public FrameHistory Window1s;
        public FrameHistory Window5s;
        public FrameHistory Window10s;
        public bool HasWindow1s;
        public bool HasWindow5s;
        public bool HasWindow10s;
    }

    private struct FrameHistory
    {
        public bool Valid;
        public double TimeSeconds;
        public long ManagedMemoryBytes;
        public int Gc0;
        public int Gc1;
        public int Gc2;
        public AdvancedFarmingPerfSnapshotService.SampleSnapshot Snapshot;
    }

    private int m_durationSeconds;
    private int m_intervalSeconds;
    private double m_spikeMs;
    private double m_severeMs;
    private int m_maxSpikes;
    private int m_warmupSeconds;
    private bool m_warmupAnnounced;
    private Action<RebirthAdvancedFarmingFrameSpikeProfilerRunner> m_finishedCallback;
    private bool m_abort;
    private bool m_samplingWasEnabled;
    private AdvancedFarmingPerfSnapshotService.SampleSnapshot m_startSnapshot;
    private AdvancedFarmingPerfSnapshotService.SampleSnapshot m_intervalSnapshot;
    private AdvancedFarmingPerfSnapshotService.SampleSnapshot m_lastFrameSnapshot;
    private SpikeRecord[] m_spikes;
    private int m_spikeCount;
    private int m_totalSpikeCount;
    private int m_severeSpikeCount;
    private double m_elapsed;
    private double m_intervalElapsed;
    private double m_intervalFrameMsTotal;
    private double m_intervalFrameMsMax;
    private double m_intervalFrameMsMin;
    private int m_intervalFrames;
    private int m_intervalSpikeCount;
    private int m_intervalSevereCount;
    private long m_lastMemory;
    private int m_lastGc0;
    private int m_lastGc1;
    private int m_lastGc2;
    private int m_startGc0;
    private int m_startGc1;
    private int m_startGc2;
    private int m_intervalGc0;
    private int m_intervalGc1;
    private int m_intervalGc2;
    private long m_startManagedMemory;
    private long m_intervalManagedMemory;
    private FrameHistory[] m_history;
    private int m_historyNext;
    private int m_historyCount;

    public void Configure(
        int durationSeconds,
        int intervalSeconds,
        double spikeMs,
        double severeMs,
        int maxSpikes,
        int warmupSeconds,
        Action<RebirthAdvancedFarmingFrameSpikeProfilerRunner> finishedCallback)
    {
        m_durationSeconds = durationSeconds;
        m_intervalSeconds = intervalSeconds;
        m_spikeMs = spikeMs;
        m_severeMs = severeMs;
        m_maxSpikes = maxSpikes;
        m_warmupSeconds = warmupSeconds;
        m_finishedCallback = finishedCallback;
        m_spikes = new SpikeRecord[m_maxSpikes];
        m_history = new FrameHistory[4096];
        m_historyNext = 0;
        m_historyCount = 0;
        StartCoroutine(RunProfile());
    }

    public void AbortAndRestore()
    {
        m_abort = true;
        RestoreSamplingState();
    }

    private IEnumerator RunProfile()
    {
        m_samplingWasEnabled = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        AdvancedFarmingPerfSnapshotService.SetSampling(true);
        m_startSnapshot = AdvancedFarmingPerfSnapshotService.GetSnapshot();
        m_intervalSnapshot = m_startSnapshot;
        m_lastFrameSnapshot = m_startSnapshot;
        m_lastMemory = GC.GetTotalMemory(false);
        m_startManagedMemory = m_lastMemory;
        m_intervalManagedMemory = m_lastMemory;
        m_lastGc0 = GC.CollectionCount(0);
        m_lastGc1 = GC.CollectionCount(1);
        m_lastGc2 = GC.CollectionCount(2);
        m_startGc0 = m_lastGc0;
        m_startGc1 = m_lastGc1;
        m_startGc2 = m_lastGc2;
        m_intervalGc0 = m_lastGc0;
        m_intervalGc1 = m_lastGc1;
        m_intervalGc2 = m_lastGc2;
        AddHistory(0d, m_startSnapshot, m_lastMemory, m_lastGc0, m_lastGc1, m_lastGc2);
        ResetIntervalFrameStats();

        Log.Out("[AFFrameSpikeProfiler] === BEGIN Advanced Farming frame-spike profiler ===");
        Log.Out("[AFFrameSpikeProfiler] durationSeconds=" + m_durationSeconds
            + " intervalSeconds=" + m_intervalSeconds
            + " spikeMs=" + FormatDouble(m_spikeMs)
            + " severeMs=" + FormatDouble(m_severeMs)
            + " maxStoredSpikes=" + m_maxSpikes
            + " warmupSeconds=" + m_warmupSeconds
            + " liveSpikeLogging=False"
            + " rollingWindows=1s,5s,10s"
            + " gcCounters=True"
            + " samplingWasEnabled=" + m_samplingWasEnabled);
        NotifyToolbelt("AF spike profiler running for " + m_durationSeconds + "s. Warmup=" + m_warmupSeconds + "s. First interval report in " + m_intervalSeconds + "s.");

        while (!m_abort && m_elapsed < (double)m_durationSeconds)
        {
            yield return null;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0.000001f)
                continue;

            double frameMs = (double)dt * 1000.0d;
            m_elapsed += (double)dt;
            m_intervalElapsed += (double)dt;
            m_intervalFrames++;
            m_intervalFrameMsTotal += frameMs;
            if (frameMs > m_intervalFrameMsMax)
                m_intervalFrameMsMax = frameMs;
            if (m_intervalFrameMsMin <= 0d || frameMs < m_intervalFrameMsMin)
                m_intervalFrameMsMin = frameMs;

            AdvancedFarmingPerfSnapshotService.SampleSnapshot now = AdvancedFarmingPerfSnapshotService.GetSnapshot();
            long memoryNow = GC.GetTotalMemory(false);
            long memoryDelta = memoryNow - m_lastMemory;
            int gc0Now = GC.CollectionCount(0);
            int gc1Now = GC.CollectionCount(1);
            int gc2Now = GC.CollectionCount(2);
            int gc0Delta = gc0Now - m_lastGc0;
            int gc1Delta = gc1Now - m_lastGc1;
            int gc2Delta = gc2Now - m_lastGc2;
            AddHistory(m_elapsed, now, memoryNow, gc0Now, gc1Now, gc2Now);

            bool warmedUp = m_elapsed >= (double)m_warmupSeconds;
            if (warmedUp && !m_warmupAnnounced)
            {
                m_warmupAnnounced = true;
                NotifyToolbelt("AF spike profiler warmup complete. Recording spikes now.");
            }

            if (warmedUp && frameMs >= m_spikeMs)
            {
                m_totalSpikeCount++;
                m_intervalSpikeCount++;
                if (frameMs >= m_severeMs)
                {
                    m_severeSpikeCount++;
                    m_intervalSevereCount++;
                }

                RecordSpike(m_totalSpikeCount, m_elapsed, frameMs, memoryDelta, memoryNow, gc0Delta, gc1Delta, gc2Delta, gc0Now, gc1Now, gc2Now, m_lastFrameSnapshot, now);
            }

            m_lastFrameSnapshot = now;
            m_lastMemory = memoryNow;
            m_lastGc0 = gc0Now;
            m_lastGc1 = gc1Now;
            m_lastGc2 = gc2Now;

            if (m_intervalElapsed >= (double)m_intervalSeconds)
            {
                LogInterval(now);
                m_intervalSnapshot = now;
                m_intervalManagedMemory = GC.GetTotalMemory(false);
                m_intervalGc0 = GC.CollectionCount(0);
                m_intervalGc1 = GC.CollectionCount(1);
                m_intervalGc2 = GC.CollectionCount(2);
                ResetIntervalFrameStats();
            }
        }

        if (!m_abort)
        {
            AdvancedFarmingPerfSnapshotService.SampleSnapshot end = AdvancedFarmingPerfSnapshotService.GetSnapshot();
            if (m_intervalFrames > 0)
                LogInterval(end);
            LogFinal(end);
            NotifyToolbelt("AF spike profiler completed. Send the latest output_log_client zip.");
        }

        RestoreSamplingState();
        Log.Out("[AFFrameSpikeProfiler] === END Advanced Farming frame-spike profiler ===");

        if (m_finishedCallback != null)
            m_finishedCallback(this);
        UnityEngine.Object.Destroy(gameObject);
    }

    private void RestoreSamplingState()
    {
        AdvancedFarmingPerfSnapshotService.SetSampling(m_samplingWasEnabled);
    }

    private void OnDestroy()
    {
        RestoreSamplingState();
    }

    private void ResetIntervalFrameStats()
    {
        m_intervalElapsed = 0d;
        m_intervalFrameMsTotal = 0d;
        m_intervalFrameMsMax = 0d;
        m_intervalFrameMsMin = 0d;
        m_intervalFrames = 0;
        m_intervalSpikeCount = 0;
        m_intervalSevereCount = 0;
    }

    private void RecordSpike(
        int index,
        double elapsedSeconds,
        double frameMs,
        long memoryDelta,
        long totalManagedMemory,
        int gc0Delta,
        int gc1Delta,
        int gc2Delta,
        int totalGc0,
        int totalGc1,
        int totalGc2,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot before,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot after)
    {
        SpikeRecord r = new SpikeRecord();
        r.Index = index;
        r.TimeSeconds = elapsedSeconds;
        r.FrameMs = frameMs;
        r.Fps = frameMs > 0d ? 1000.0d / frameMs : 0d;
        r.MemoryDeltaBytes = memoryDelta;
        r.TotalManagedMemoryBytes = totalManagedMemory;
        r.Gc0Delta = gc0Delta;
        r.Gc1Delta = gc1Delta;
        r.Gc2Delta = gc2Delta;
        r.TotalGc0 = totalGc0;
        r.TotalGc1 = totalGc1;
        r.TotalGc2 = totalGc2;
        r.Before = before;
        r.After = after;
        r.HasWindow1s = TryGetHistoryAtOrBefore(elapsedSeconds - 1.0d, out r.Window1s);
        r.HasWindow5s = TryGetHistoryAtOrBefore(elapsedSeconds - 5.0d, out r.Window5s);
        r.HasWindow10s = TryGetHistoryAtOrBefore(elapsedSeconds - 10.0d, out r.Window10s);

        if (m_spikeCount < m_spikes.Length)
        {
            m_spikes[m_spikeCount++] = r;
            return;
        }

        int lowest = 0;
        double lowestMs = m_spikes[0].FrameMs;
        for (int i = 1; i < m_spikes.Length; i++)
        {
            if (m_spikes[i].FrameMs < lowestMs)
            {
                lowest = i;
                lowestMs = m_spikes[i].FrameMs;
            }
        }

        if (r.FrameMs > lowestMs)
            m_spikes[lowest] = r;
    }

    private void LogInterval(AdvancedFarmingPerfSnapshotService.SampleSnapshot now)
    {
        double avgFrameMs = m_intervalFrames > 0 ? m_intervalFrameMsTotal / (double)m_intervalFrames : 0d;
        double avgFps = avgFrameMs > 0d ? 1000.0d / avgFrameMs : 0d;
        double minFps = m_intervalFrameMsMax > 0d ? 1000.0d / m_intervalFrameMsMax : 0d;
        double maxFps = m_intervalFrameMsMin > 0d ? 1000.0d / m_intervalFrameMsMin : 0d;

        Log.Out("[AFFrameSpikeProfiler] INTERVAL elapsed=" + FormatDouble(m_elapsed)
            + " frames=" + m_intervalFrames
            + " avgFps=" + FormatDouble(avgFps)
            + " minFps=" + FormatDouble(minFps)
            + " maxFps=" + FormatDouble(maxFps)
            + " avgFrameMs=" + FormatDouble(avgFrameMs)
            + " maxFrameMs=" + FormatDouble(m_intervalFrameMsMax)
            + " spikes=" + m_intervalSpikeCount
            + " severe=" + m_intervalSevereCount
            + " gc0=" + (GC.CollectionCount(0) - m_intervalGc0)
            + " gc1=" + (GC.CollectionCount(1) - m_intervalGc1)
            + " gc2=" + (GC.CollectionCount(2) - m_intervalGc2)
            + " memDeltaKB=" + ((GC.GetTotalMemory(false) - m_intervalManagedMemory) / 1024L));

        Log.Out(BuildCounterDeltaLine("INTERVAL-COUNTERS", m_intervalSnapshot, now));
        NotifyToolbelt("AF spike profiler " + ((int)m_elapsed).ToString() + "s/" + m_durationSeconds + "s: spikes=" + m_totalSpikeCount + " severe=" + m_severeSpikeCount + ".");
    }

    private void LogFinal(AdvancedFarmingPerfSnapshotService.SampleSnapshot end)
    {
        Log.Out("[AFFrameSpikeProfiler] FINAL elapsed=" + FormatDouble(m_elapsed)
            + " totalSpikes=" + m_totalSpikeCount
            + " severeSpikes=" + m_severeSpikeCount
            + " storedTopSpikes=" + m_spikeCount
            + " spikeThresholdMs=" + FormatDouble(m_spikeMs)
            + " severeThresholdMs=" + FormatDouble(m_severeMs)
            + " gc0=" + (GC.CollectionCount(0) - m_startGc0)
            + " gc1=" + (GC.CollectionCount(1) - m_startGc1)
            + " gc2=" + (GC.CollectionCount(2) - m_startGc2)
            + " memDeltaKB=" + ((GC.GetTotalMemory(false) - m_startManagedMemory) / 1024L));
        Log.Out(BuildCounterDeltaLine("TOTAL-COUNTERS", m_startSnapshot, end));

        SortStoredSpikesDescending();
        if (m_spikeCount == 0)
        {
            Log.Out("[AFFrameSpikeProfiler] TOP-SPIKES none");
            return;
        }

        int limit = m_spikeCount < 20 ? m_spikeCount : 20;
        for (int i = 0; i < limit; i++)
        {
            SpikeRecord r = m_spikes[i];
            Log.Out(BuildSpikeLine("TOP-SPIKE rank=" + (i + 1), r));
        }

        Log.Out("[AFFrameSpikeProfiler] Interpretation: TOP-SPIKE lines now include same-frame deltas plus pre1s/pre5s/pre10s Advanced Farming and GC/memory history. If same-frame deltas are zero but pre-window AF deltas are high and GC deltas/memory drops occur, suspect delayed GC/allocation pressure. If both same-frame and pre-window AF deltas are low, suspect non-AF engine/render/background work.");
    }

    private void SortStoredSpikesDescending()
    {
        for (int i = 0; i < m_spikeCount - 1; i++)
        {
            int best = i;
            double bestValue = m_spikes[i].FrameMs;
            for (int j = i + 1; j < m_spikeCount; j++)
            {
                if (m_spikes[j].FrameMs > bestValue)
                {
                    best = j;
                    bestValue = m_spikes[j].FrameMs;
                }
            }

            if (best != i)
            {
                SpikeRecord tmp = m_spikes[i];
                m_spikes[i] = m_spikes[best];
                m_spikes[best] = tmp;
            }
        }
    }

    private static string BuildSpikeLine(string label, SpikeRecord r)
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.Append("[AFFrameSpikeProfiler] ").Append(label)
          .Append(" index=").Append(r.Index)
          .Append(" t=").Append(FormatDouble(r.TimeSeconds))
          .Append(" frameMs=").Append(FormatDouble(r.FrameMs))
          .Append(" fps=").Append(FormatDouble(r.FrameMs > 0d ? 1000.0d / r.FrameMs : 0d))
          .Append(" memDeltaKB=").Append(r.MemoryDeltaBytes / 1024L)
          .Append(" managedKB=").Append(r.TotalManagedMemoryBytes / 1024L)
          .Append(" gc0=").Append(r.Gc0Delta)
          .Append(" gc1=").Append(r.Gc1Delta)
          .Append(" gc2=").Append(r.Gc2Delta);
        AppendCounterDeltas(sb, r.Before, r.After);
        AppendHistoryWindow(sb, "pre1s", r.HasWindow1s, r.Window1s, r.After, r.TotalManagedMemoryBytes, r.TotalGc0, r.TotalGc1, r.TotalGc2);
        AppendHistoryWindow(sb, "pre5s", r.HasWindow5s, r.Window5s, r.After, r.TotalManagedMemoryBytes, r.TotalGc0, r.TotalGc1, r.TotalGc2);
        AppendHistoryWindow(sb, "pre10s", r.HasWindow10s, r.Window10s, r.After, r.TotalManagedMemoryBytes, r.TotalGc0, r.TotalGc1, r.TotalGc2);
        return sb.ToString();
    }

    private void AddHistory(
        double timeSeconds,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot snapshot,
        long managedMemoryBytes,
        int gc0,
        int gc1,
        int gc2)
    {
        if (m_history == null || m_history.Length == 0)
            return;

        FrameHistory h = new FrameHistory();
        h.Valid = true;
        h.TimeSeconds = timeSeconds;
        h.Snapshot = snapshot;
        h.ManagedMemoryBytes = managedMemoryBytes;
        h.Gc0 = gc0;
        h.Gc1 = gc1;
        h.Gc2 = gc2;

        m_history[m_historyNext] = h;
        m_historyNext++;
        if (m_historyNext >= m_history.Length)
            m_historyNext = 0;
        if (m_historyCount < m_history.Length)
            m_historyCount++;
    }

    private bool TryGetHistoryAtOrBefore(double targetTimeSeconds, out FrameHistory result)
    {
        result = default(FrameHistory);
        if (m_history == null || m_historyCount <= 0)
            return false;

        bool found = false;
        double bestTime = -1.0d;
        for (int i = 0; i < m_historyCount; i++)
        {
            FrameHistory h = m_history[i];
            if (!h.Valid)
                continue;
            if (h.TimeSeconds <= targetTimeSeconds && h.TimeSeconds > bestTime)
            {
                result = h;
                bestTime = h.TimeSeconds;
                found = true;
            }
        }

        if (found)
            return true;

        // If the run is younger than the requested window, use the oldest available history.
        double oldest = double.MaxValue;
        for (int i = 0; i < m_historyCount; i++)
        {
            FrameHistory h = m_history[i];
            if (!h.Valid)
                continue;
            if (h.TimeSeconds < oldest)
            {
                result = h;
                oldest = h.TimeSeconds;
                found = true;
            }
        }

        return found;
    }

    private static void AppendHistoryWindow(
        StringBuilder sb,
        string label,
        bool hasWindow,
        FrameHistory start,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot after,
        long endManagedMemoryBytes,
        int endGc0,
        int endGc1,
        int endGc2)
    {
        sb.Append(' ').Append(label);
        if (!hasWindow)
        {
            sb.Append("=none");
            return;
        }

        long gc0 = endGc0 - start.Gc0;
        long gc1 = endGc1 - start.Gc1;
        long gc2 = endGc2 - start.Gc2;
        long managedDeltaKb = (endManagedMemoryBytes - start.ManagedMemoryBytes) / 1024L;

        sb.Append(" memDeltaKB=").Append(managedDeltaKb)
          .Append(" gc0=").Append(gc0)
          .Append(" gc1=").Append(gc1)
          .Append(" gc2=").Append(gc2);
        AppendCounterDeltas(sb, start.Snapshot, after);
    }

    private static string BuildCounterDeltaLine(
        string label,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot before,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot after)
    {
        StringBuilder sb = new StringBuilder(1024);
        sb.Append("[AFFrameSpikeProfiler] ").Append(label);
        AppendCounterDeltas(sb, before, after);
        return sb.ToString();
    }

    private static void AppendCounterDeltas(
        StringBuilder sb,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot before,
        AdvancedFarmingPerfSnapshotService.SampleSnapshot after)
    {
        long areaCalls = after.AreaCalls - before.AreaCalls;
        long areaTicks = after.TotalElapsedStopwatchTicks - before.TotalElapsedStopwatchTicks;
        long anchorOnlyCalls = after.AnchorOnlyCalls - before.AnchorOnlyCalls;
        long maintenanceCalls = after.AreaMaintenanceCalls - before.AreaMaintenanceCalls;
        long plantSyncs = after.PlantSyncs - before.PlantSyncs;
        long lightEvalCalls = after.LightEvaluateCalls - before.LightEvaluateCalls;
        long lightEvalTicks = after.LightEvaluateElapsedStopwatchTicks - before.LightEvaluateElapsedStopwatchTicks;
        long thermalEvalCalls = after.ThermalEvaluateCalls - before.ThermalEvaluateCalls;
        long thermalEvalTicks = after.ThermalEvaluateElapsedStopwatchTicks - before.ThermalEvaluateElapsedStopwatchTicks;
        long maintPhaseCalls = after.AreaMaintenancePhaseCalls - before.AreaMaintenancePhaseCalls;
        long maintPhaseTicks = after.AreaMaintenancePhaseElapsedStopwatchTicks - before.AreaMaintenancePhaseElapsedStopwatchTicks;
        long lightSearches = after.LightExposureSearches - before.LightExposureSearches;
        long lightNodes = after.LightExposureNodes - before.LightExposureNodes;
        long lightTransparent = after.LightExposureTransparentCandidates - before.LightExposureTransparentCandidates;
        long thermalSearches = after.ThermalExposureSearches - before.ThermalExposureSearches;
        long thermalNodes = after.ThermalExposureNodes - before.ThermalExposureNodes;
        long waterMaintQueries = after.WaterRegistryMaintenanceQueries - before.WaterRegistryMaintenanceQueries;
        long waterMaintCandidates = after.WaterRegistryMaintenanceCandidates - before.WaterRegistryMaintenanceCandidates;
        long waterMaintTe = after.WaterRegistryMaintenanceTileEntityLookups - before.WaterRegistryMaintenanceTileEntityLookups;
        long waterResolveCacheHits = after.WaterRegistryResolveCacheHits - before.WaterRegistryResolveCacheHits;
        long managedAreaKb = (after.ProcessAreaManagedPositiveBytes - before.ProcessAreaManagedPositiveBytes) / 1024L;
        long managedMaintKb = (after.AreaMaintenanceManagedPositiveBytes - before.AreaMaintenanceManagedPositiveBytes) / 1024L;
        long managedWaterMaintKb = (after.WaterMaintenanceManagedPositiveBytes - before.WaterMaintenanceManagedPositiveBytes) / 1024L;
        long managedLightKb = (after.LightEvaluateManagedPositiveBytes - before.LightEvaluateManagedPositiveBytes) / 1024L;
        long managedThermalKb = (after.ThermalEvaluateManagedPositiveBytes - before.ThermalEvaluateManagedPositiveBytes) / 1024L;
        long managedAreaNegKb = (after.ProcessAreaManagedNegativeBytes - before.ProcessAreaManagedNegativeBytes) / 1024L;
        long managedMaintNegKb = (after.AreaMaintenanceManagedNegativeBytes - before.AreaMaintenanceManagedNegativeBytes) / 1024L;
        long managedWaterMaintNegKb = (after.WaterMaintenanceManagedNegativeBytes - before.WaterMaintenanceManagedNegativeBytes) / 1024L;
        long managedLightNegKb = (after.LightEvaluateManagedNegativeBytes - before.LightEvaluateManagedNegativeBytes) / 1024L;
        long managedThermalNegKb = (after.ThermalEvaluateManagedNegativeBytes - before.ThermalEvaluateManagedNegativeBytes) / 1024L;
        long waterConsumptionQueries = after.WaterRegistryConsumptionQueries - before.WaterRegistryConsumptionQueries;
        long waterConsumptionCandidates = after.WaterRegistryConsumptionCandidates - before.WaterRegistryConsumptionCandidates;
        long waterConsumptionTe = after.WaterRegistryConsumptionTileEntityLookups - before.WaterRegistryConsumptionTileEntityLookups;
        long heatRegistryQueries = after.HeatRegistryQueries - before.HeatRegistryQueries;
        long heatFallbackScans = after.HeatRegistryFallbackScans - before.HeatRegistryFallbackScans;
        long heatScanCalls = after.HeatScanCalls - before.HeatScanCalls;
        long heatScanPositions = after.HeatScanPositions - before.HeatScanPositions;
        long sunQueued = after.SunRefreshQueued - before.SunRefreshQueued;
        long sunRebuilds = after.SunRefreshRebuilds - before.SunRefreshRebuilds;
        long sunTicks = after.SunRefreshElapsedStopwatchTicks - before.SunRefreshElapsedStopwatchTicks;
        long lpSunRefresh = after.LightProcessorRefreshSunlightCalls - before.LightProcessorRefreshSunlightCalls;
        long lpLocalSun = after.LightProcessorRefreshLocalSunCalls - before.LightProcessorRefreshLocalSunCalls;
        long lpLocalBlock = after.LightProcessorRefreshLocalBlockCalls - before.LightProcessorRefreshLocalBlockCalls;
        long lpSpreadSun = after.LightProcessorSpreadSunCalls - before.LightProcessorSpreadSunCalls;
        long lpSpreadBlock = after.LightProcessorSpreadBlockCalls - before.LightProcessorSpreadBlockCalls;
        long lpUnspreadSun = after.LightProcessorUnspreadSunCalls - before.LightProcessorUnspreadSunCalls;
        long lpUnspreadBlock = after.LightProcessorUnspreadBlockCalls - before.LightProcessorUnspreadBlockCalls;
        long exposureBlockChanges = after.ExposureInvalidationBlockChanges - before.ExposureInvalidationBlockChanges;
        long exposureSkippedPlant = after.ExposureInvalidationSkippedPlantLike - before.ExposureInvalidationSkippedPlantLike;
        long exposureLightClears = after.ExposureInvalidationLightClears - before.ExposureInvalidationLightClears;
        long exposureThermalClears = after.ExposureInvalidationThermalClears - before.ExposureInvalidationThermalClears;
        long activeBlockQueries = after.ActiveBlockLightPrecheckQueries - before.ActiveBlockLightPrecheckQueries;
        long activeBlockPasses = after.ActiveBlockLightPrecheckPasses - before.ActiveBlockLightPrecheckPasses;

        sb.Append(" areaCalls=").Append(areaCalls)
          .Append(" areaMs=").Append(FormatDouble(TicksToMs(areaTicks)))
          .Append(" anchorCalls=").Append(anchorOnlyCalls)
          .Append(" maintCalls=").Append(maintenanceCalls)
          .Append(" maintMs=").Append(FormatDouble(TicksToMs(maintPhaseTicks)))
          .Append(" maintPhaseCalls=").Append(maintPhaseCalls)
          .Append(" plantSyncs=").Append(plantSyncs)
          .Append(" lightEvalCalls=").Append(lightEvalCalls)
          .Append(" lightEvalMs=").Append(FormatDouble(TicksToMs(lightEvalTicks)))
          .Append(" thermalEvalCalls=").Append(thermalEvalCalls)
          .Append(" thermalEvalMs=").Append(FormatDouble(TicksToMs(thermalEvalTicks)))
          .Append(" lightSearches=").Append(lightSearches)
          .Append(" lightNodes=").Append(lightNodes)
          .Append(" lightTransparent=").Append(lightTransparent)
          .Append(" thermalSearches=").Append(thermalSearches)
          .Append(" thermalNodes=").Append(thermalNodes)
          .Append(" waterMaintQ=").Append(waterMaintQueries)
          .Append(" waterMaintCand=").Append(waterMaintCandidates)
          .Append(" waterMaintTE=").Append(waterMaintTe)
          .Append(" waterResolveCacheHits=").Append(waterResolveCacheHits)
          .Append(" managedAreaKB=").Append(managedAreaKb).Append("/-").Append(managedAreaNegKb)
          .Append(" managedMaintKB=").Append(managedMaintKb).Append("/-").Append(managedMaintNegKb)
          .Append(" managedWaterMaintKB=").Append(managedWaterMaintKb).Append("/-").Append(managedWaterMaintNegKb)
          .Append(" managedLightKB=").Append(managedLightKb).Append("/-").Append(managedLightNegKb)
          .Append(" managedThermalKB=").Append(managedThermalKb).Append("/-").Append(managedThermalNegKb)
          .Append(" waterConsumeQ=").Append(waterConsumptionQueries)
          .Append(" waterConsumeCand=").Append(waterConsumptionCandidates)
          .Append(" waterConsumeTE=").Append(waterConsumptionTe)
          .Append(" heatRegistryQ=").Append(heatRegistryQueries)
          .Append(" heatFallback=").Append(heatFallbackScans)
          .Append(" heatScanCalls=").Append(heatScanCalls)
          .Append(" heatScanPositions=").Append(heatScanPositions)
          .Append(" sunQueued=").Append(sunQueued)
          .Append(" sunRebuilds=").Append(sunRebuilds)
          .Append(" sunRebuildMs=").Append(FormatDouble(TicksToMs(sunTicks)))
          .Append(" lpSunRefresh=").Append(lpSunRefresh)
          .Append(" lpLocalSun=").Append(lpLocalSun)
          .Append(" lpLocalBlock=").Append(lpLocalBlock)
          .Append(" lpSpreadSun=").Append(lpSpreadSun)
          .Append(" lpSpreadBlock=").Append(lpSpreadBlock)
          .Append(" lpUnspreadSun=").Append(lpUnspreadSun)
          .Append(" lpUnspreadBlock=").Append(lpUnspreadBlock)
          .Append(" exposureChanges=").Append(exposureBlockChanges)
          .Append(" exposureSkippedPlant=").Append(exposureSkippedPlant)
          .Append(" exposureLightClears=").Append(exposureLightClears)
          .Append(" exposureThermalClears=").Append(exposureThermalClears)
          .Append(" blockLightPrecheck=").Append(activeBlockQueries)
          .Append(" blockLightPasses=").Append(activeBlockPasses);
    }

    public static void NotifyToolbelt(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null)
                return;

            EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
            if (player == null)
                return;

            GameManager.ShowTooltip(player, message, true, false, 4f);
        }
        catch
        {
        }
    }

    private static double TicksToMs(long ticks)
    {
        return Stopwatch.Frequency > 0L ? (double)ticks * 1000.0d / (double)Stopwatch.Frequency : 0d;
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}

public class RebirthAdvancedFarmingIsolationProfilerRunner : MonoBehaviour
{
    private struct PhaseSpec
    {
        public string Name;
        public bool PauseAll;
        public bool PauseMaintenance;
        public bool PauseActive;
        public bool BypassLight;
        public bool BypassThermal;
        public bool BypassWater;
        public bool HardDisableAll;
    }

    private struct PhaseResult
    {
        public string Name;
        public double AvgFps;
        public double MinFps;
        public double MaxFps;
        public double AvgFrameMs;
        public double MaxFrameMs;
        public int Frames;
        public int Spikes;
        public int Severe;
        public int Gc0;
        public int Gc1;
        public int Gc2;
        public long MemDeltaKb;
        public AdvancedFarmingPerfSnapshotService.SampleSnapshot StartSnapshot;
        public AdvancedFarmingPerfSnapshotService.SampleSnapshot EndSnapshot;
    }

    private int m_totalSeconds;
    private int m_phaseWarmupSeconds;
    private double m_spikeMs;
    private double m_severeMs;
    private double m_phaseSeconds;
    private Action<RebirthAdvancedFarmingIsolationProfilerRunner> m_finishedCallback;
    private bool m_abort;
    private bool m_samplingWasEnabled;
    private bool m_movementScenario;
    private AdvancedFarmingCatchupService.DiagnosticIsolationState m_originalIsolationState;
    private PhaseSpec[] m_phases;
    private PhaseResult[] m_results;

    public void Configure(
        int totalSeconds,
        int phaseWarmupSeconds,
        double spikeMs,
        double severeMs,
        bool movementScenario,
        Action<RebirthAdvancedFarmingIsolationProfilerRunner> finishedCallback)
    {
        m_totalSeconds = totalSeconds;
        m_phaseWarmupSeconds = phaseWarmupSeconds;
        m_spikeMs = spikeMs;
        m_severeMs = severeMs;
        m_movementScenario = movementScenario;
        m_finishedCallback = finishedCallback;
        m_phases = BuildPhases();
        m_results = new PhaseResult[m_phases.Length];
        m_phaseSeconds = m_phases.Length > 0 ? (double)m_totalSeconds / (double)m_phases.Length : (double)m_totalSeconds;
        StartCoroutine(RunIsolation());
    }

    public void AbortAndRestore()
    {
        m_abort = true;
        RestoreState();
    }

    private static PhaseSpec[] BuildPhases()
    {
        PhaseSpec[] phases = new PhaseSpec[9];
        phases[0] = MakePhase("baseline", false, false, false, false, false, false, false);
        phases[1] = MakePhase("pause_all_process_area", true, false, false, false, false, false, false);
        phases[2] = MakePhase("pause_area_maintenance", false, true, false, false, false, false, false);
        phases[3] = MakePhase("pause_active_crop_processing", false, false, true, false, false, false, false);
        phases[4] = MakePhase("bypass_light_evaluation", false, false, false, true, false, false, false);
        phases[5] = MakePhase("bypass_thermal_evaluation", false, false, false, false, true, false, false);
        phases[6] = MakePhase("bypass_water_consumption", false, false, false, false, false, true, false);
        phases[7] = MakePhase("hard_disable_all_advanced_farming_runtime", true, true, true, true, true, true, true);
        phases[8] = MakePhase("restored_final", false, false, false, false, false, false, false);
        return phases;
    }

    private static PhaseSpec MakePhase(string name, bool pauseAll, bool pauseMaintenance, bool pauseActive, bool bypassLight, bool bypassThermal, bool bypassWater, bool hardDisableAll)
    {
        PhaseSpec p = new PhaseSpec();
        p.Name = name;
        p.PauseAll = pauseAll;
        p.PauseMaintenance = pauseMaintenance;
        p.PauseActive = pauseActive;
        p.BypassLight = bypassLight;
        p.BypassThermal = bypassThermal;
        p.BypassWater = bypassWater;
        p.HardDisableAll = hardDisableAll;
        return p;
    }

    private IEnumerator RunIsolation()
    {
        m_samplingWasEnabled = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
        m_originalIsolationState = AdvancedFarmingCatchupService.CaptureDiagnosticIsolationState();
        AdvancedFarmingPerfSnapshotService.SetSampling(true);

        Log.Out("[AFIsolationProfiler] === BEGIN Advanced Farming isolation profiler ===");
        Log.Out("[AFIsolationProfiler] scenario=" + (m_movementScenario ? "movement" : "stationary")
            + " totalSeconds=" + m_totalSeconds
            + " phaseCount=" + m_phases.Length
            + " phaseSeconds=" + FormatDouble(m_phaseSeconds)
            + " phaseWarmupSeconds=" + m_phaseWarmupSeconds
            + " spikeMs=" + FormatDouble(m_spikeMs)
            + " severeMs=" + FormatDouble(m_severeMs)
            + " samplingWasEnabled=" + m_samplingWasEnabled);
        RebirthAdvancedFarmingFrameSpikeProfilerRunner.NotifyToolbelt(
            (m_movementScenario ? "AF movement isolation running: " : "AF isolation profiler running: ")
            + m_phases.Length + " phases, " + FormatDouble(m_phaseSeconds) + "s each.");

        for (int i = 0; i < m_phases.Length && !m_abort; i++)
        {
            PhaseSpec phase = m_phases[i];
            ApplyPhase(phase);
            yield return null;
            yield return StartCoroutine(RunPhase(i, phase));
        }

        if (!m_abort)
        {
            RestoreState();
            LogFinalSummary();
            RebirthAdvancedFarmingFrameSpikeProfilerRunner.NotifyToolbelt("AF isolation profiler completed. Send the latest output_log_client zip.");
        }

        RestoreState();
        Log.Out("[AFIsolationProfiler] === END Advanced Farming isolation profiler ===");
        if (m_finishedCallback != null)
            m_finishedCallback(this);
        UnityEngine.Object.Destroy(gameObject);
    }

    private IEnumerator RunPhase(int index, PhaseSpec phase)
    {
        Log.Out("[AFIsolationProfiler] PHASE-START index=" + (index + 1) + "/" + m_phases.Length
            + " name=" + phase.Name
            + " isolation=" + AdvancedFarmingCatchupService.BuildDiagnosticIsolationStatus());
        RebirthAdvancedFarmingFrameSpikeProfilerRunner.NotifyToolbelt("AF isolation phase " + (index + 1) + "/" + m_phases.Length + ": " + phase.Name);

        AdvancedFarmingPerfSnapshotService.SampleSnapshot startSnapshot = AdvancedFarmingPerfSnapshotService.GetSnapshot();
        long startMem = GC.GetTotalMemory(false);
        int startGc0 = GC.CollectionCount(0);
        int startGc1 = GC.CollectionCount(1);
        int startGc2 = GC.CollectionCount(2);

        double elapsed = 0d;
        double sampleElapsed = 0d;
        double frameMsTotal = 0d;
        double frameMsMax = 0d;
        double frameMsMin = 0d;
        int frames = 0;
        int spikes = 0;
        int severe = 0;
        bool warmupAnnounced = false;

        while (!m_abort && elapsed < m_phaseSeconds)
        {
            yield return null;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0.000001f)
                continue;

            elapsed += (double)dt;
            bool warmed = elapsed >= (double)m_phaseWarmupSeconds;
            if (warmed && !warmupAnnounced)
            {
                warmupAnnounced = true;
                RebirthAdvancedFarmingFrameSpikeProfilerRunner.NotifyToolbelt("AF isolation phase " + (index + 1) + " sampling now: " + phase.Name);
            }

            if (!warmed)
                continue;

            double frameMs = (double)dt * 1000.0d;
            sampleElapsed += (double)dt;
            frames++;
            frameMsTotal += frameMs;
            if (frameMs > frameMsMax)
                frameMsMax = frameMs;
            if (frameMsMin <= 0d || frameMs < frameMsMin)
                frameMsMin = frameMs;
            if (frameMs >= m_spikeMs)
            {
                spikes++;
                if (frameMs >= m_severeMs)
                    severe++;
            }
        }

        AdvancedFarmingPerfSnapshotService.SampleSnapshot endSnapshot = AdvancedFarmingPerfSnapshotService.GetSnapshot();
        long endMem = GC.GetTotalMemory(false);
        int endGc0 = GC.CollectionCount(0);
        int endGc1 = GC.CollectionCount(1);
        int endGc2 = GC.CollectionCount(2);

        PhaseResult result = new PhaseResult();
        result.Name = phase.Name;
        result.Frames = frames;
        result.AvgFrameMs = frames > 0 ? frameMsTotal / (double)frames : 0d;
        result.AvgFps = result.AvgFrameMs > 0d ? 1000.0d / result.AvgFrameMs : 0d;
        result.MinFps = frameMsMax > 0d ? 1000.0d / frameMsMax : 0d;
        result.MaxFps = frameMsMin > 0d ? 1000.0d / frameMsMin : 0d;
        result.MaxFrameMs = frameMsMax;
        result.Spikes = spikes;
        result.Severe = severe;
        result.Gc0 = endGc0 - startGc0;
        result.Gc1 = endGc1 - startGc1;
        result.Gc2 = endGc2 - startGc2;
        result.MemDeltaKb = (endMem - startMem) / 1024L;
        result.StartSnapshot = startSnapshot;
        result.EndSnapshot = endSnapshot;
        m_results[index] = result;

        Log.Out("[AFIsolationProfiler] PHASE-SUMMARY index=" + (index + 1) + "/" + m_phases.Length
            + " name=" + result.Name
            + " elapsed=" + FormatDouble(elapsed)
            + " sampled=" + FormatDouble(sampleElapsed)
            + " frames=" + result.Frames
            + " avgFps=" + FormatDouble(result.AvgFps)
            + " minFps=" + FormatDouble(result.MinFps)
            + " maxFps=" + FormatDouble(result.MaxFps)
            + " avgFrameMs=" + FormatDouble(result.AvgFrameMs)
            + " maxFrameMs=" + FormatDouble(result.MaxFrameMs)
            + " spikes=" + result.Spikes
            + " severe=" + result.Severe
            + " gc0=" + result.Gc0
            + " gc1=" + result.Gc1
            + " gc2=" + result.Gc2
            + " memDeltaKB=" + result.MemDeltaKb);
        Log.Out(AdvancedFarmingPerfSnapshotService.BuildSamplingDeltaReport("ISOLATE-COUNTERS phase=" + result.Name, startSnapshot, endSnapshot));
        RebirthAdvancedFarmingFrameSpikeProfilerRunner.NotifyToolbelt("AF isolation phase " + (index + 1) + " complete: " + result.Name + " avg=" + FormatDouble(result.AvgFps) + " min=" + FormatDouble(result.MinFps) + " severe=" + result.Severe + ".");
    }

    private void ApplyPhase(PhaseSpec phase)
    {
        AdvancedFarmingCatchupService.ClearDiagnosticIsolationState();
        AdvancedFarmingRuntimePolicy.SetDiagnosticHardDisableAll(phase.HardDisableAll, "rbafperf isolate phase " + phase.Name);
        AdvancedFarmingCatchupService.DiagnosticPauseAllProcessArea = phase.PauseAll;
        AdvancedFarmingCatchupService.DiagnosticPauseAreaMaintenance = phase.PauseMaintenance;
        AdvancedFarmingCatchupService.DiagnosticPauseActiveCropProcessing = phase.PauseActive;
        AdvancedFarmingCatchupService.DiagnosticBypassLightEvaluation = phase.BypassLight;
        AdvancedFarmingCatchupService.DiagnosticBypassThermalEvaluation = phase.BypassThermal;
        AdvancedFarmingCatchupService.DiagnosticBypassWaterConsumption = phase.BypassWater;
    }

    private void RestoreState()
    {
        AdvancedFarmingCatchupService.RestoreDiagnosticIsolationState(m_originalIsolationState);
        if (!m_samplingWasEnabled)
            AdvancedFarmingPerfSnapshotService.SetSampling(false);
    }

    private void LogFinalSummary()
    {
        Log.Out("[AFIsolationProfiler] FINAL scenario=" + (m_movementScenario ? "movement" : "stationary")
            + " phaseCount=" + m_phases.Length + " totalSeconds=" + m_totalSeconds
            + " phaseSeconds=" + FormatDouble(m_phaseSeconds));
        if (m_results == null || m_results.Length == 0)
            return;

        PhaseResult baseline = m_results[0];
        for (int i = 0; i < m_results.Length; i++)
        {
            PhaseResult r = m_results[i];
            Log.Out("[AFIsolationProfiler] FINAL-PHASE rank=" + (i + 1)
                + " name=" + r.Name
                + " avgFps=" + FormatDouble(r.AvgFps)
                + " avgDeltaVsBaseline=" + FormatDouble(r.AvgFps - baseline.AvgFps)
                + " minFps=" + FormatDouble(r.MinFps)
                + " minDeltaVsBaseline=" + FormatDouble(r.MinFps - baseline.MinFps)
                + " maxFrameMs=" + FormatDouble(r.MaxFrameMs)
                + " spikes=" + r.Spikes
                + " spikeDeltaVsBaseline=" + (r.Spikes - baseline.Spikes)
                + " severe=" + r.Severe
                + " severeDeltaVsBaseline=" + (r.Severe - baseline.Severe)
                + " gc0=" + r.Gc0
                + " gc1=" + r.Gc1
                + " gc2=" + r.Gc2
                + " memDeltaKB=" + r.MemDeltaKb);
        }

        Log.Out("[AFIsolationProfiler] Interpretation: if pauses reduce severe/spike counts or raise min FPS versus baseline, the paused subsystem is a suspect. The hard_disable_all_advanced_farming_runtime phase is the aggressive test: ProcessArea, ModApi pumps, SUN/door visual queues, workstation heat observing, SetBlock invalidation and LightProcessor dynamic-opacity prefixes are bypassed. If that phase does not improve spikes/min FPS, the dips are likely outside Advanced Farming runtime hooks or are caused by loaded-scene/render/chunk/engine behavior that remains after runtime code is paused.");
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
#endif
