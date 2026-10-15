#if DEBUG
using System;
using System.Text;

#nullable disable

/// <summary>
/// Release-safe, opt-in stutter trace for Advanced Farming.
/// Disabled by default. When enabled it records aggregate perf counters plus a bounded list of
/// slow Advanced Farming operations. All string construction for slow events is gated behind the
/// Active/threshold checks at call sites.
/// </summary>
public static class AdvancedFarmingStutterTraceService
{
    private const int DefaultDurationSeconds = 60;
    private const int DefaultIntervalSeconds = 5;
    private const double DefaultSlowThresholdMs = 8d;
    private const int MaxEvents = 96;

    private struct SlowEvent
    {
        public long TimestampTicks;
        public string Category;
        public Vector3i Pos;
        public double ElapsedMs;
        public string Details;
    }

    private static readonly object s_lock = new object();
    private static readonly SlowEvent[] s_events = new SlowEvent[MaxEvents];
    private static float s_nextIntervalRealtime;
    private static bool s_samplingWasEnabled;
    private static bool s_active;
    private static int s_durationSeconds;
    private static int s_intervalSeconds;
    private static double s_slowThresholdMs;
    private static long s_startUtcTicks;
    private static int s_eventWriteIndex;
    private static int s_eventCount;
    private static AdvancedFarmingPerfSnapshotService.SampleSnapshot s_startSnapshot;
    private static AdvancedFarmingPerfSnapshotService.SampleSnapshot s_lastSnapshot;

    public static bool Active { get { return s_active; } }

    public static bool ShouldRecordSlow(long elapsedStopwatchTicks)
    {
        if (!s_active)
            return false;

        return StopwatchTicksToMs(elapsedStopwatchTicks) >= s_slowThresholdMs;
    }

    public static string StartDefault()
    {
        return Start(DefaultDurationSeconds, DefaultIntervalSeconds, DefaultSlowThresholdMs);
    }

    public static string Start(int durationSeconds, int intervalSeconds, double slowThresholdMs)
    {
        if (durationSeconds < 5)
            durationSeconds = 5;
        if (durationSeconds > 300)
            durationSeconds = 300;
        if (intervalSeconds < 1)
            intervalSeconds = 1;
        if (intervalSeconds > 60)
            intervalSeconds = 60;
        if (slowThresholdMs < 1d)
            slowThresholdMs = 1d;
        if (slowThresholdMs > 250d)
            slowThresholdMs = 250d;

        lock (s_lock)
        {
            StopLocked(false);

            s_durationSeconds = durationSeconds;
            s_intervalSeconds = intervalSeconds;
            s_slowThresholdMs = slowThresholdMs;
            s_eventWriteIndex = 0;
            s_eventCount = 0;
            for (int i = 0; i < MaxEvents; i++)
                s_events[i] = default(SlowEvent);

            s_samplingWasEnabled = AdvancedFarmingPerfSnapshotService.SamplingEnabled;
            AdvancedFarmingPerfSnapshotService.ResetSampling();
            AdvancedFarmingPerfSnapshotService.SetSampling(true);
            s_startSnapshot = AdvancedFarmingPerfSnapshotService.GetSnapshot();
            s_lastSnapshot = s_startSnapshot;
            s_startUtcTicks = DateTime.UtcNow.Ticks;
            s_active = true;
            s_nextIntervalRealtime = UnityEngine.Time.realtimeSinceStartup + intervalSeconds;
        }

        return "[AdvancedFarming StutterTrace] started durationSeconds=" + durationSeconds
            + " intervalSeconds=" + intervalSeconds
            + " slowThresholdMs=" + slowThresholdMs.ToString("0.0")
            + " logging=boundedSlowEventsOnly sampling=true";
    }

    public static string Stop()
    {
        lock (s_lock)
        {
            return StopLocked(true);
        }
    }

    public static string Status()
    {
        lock (s_lock)
        {
            return BuildReportLocked("status");
        }
    }

    public static void RecordSlowOperation(string category, Vector3i pos, long elapsedStopwatchTicks, string details)
    {
        if (!s_active)
            return;

        double elapsedMs = StopwatchTicksToMs(elapsedStopwatchTicks);
        if (elapsedMs < s_slowThresholdMs)
            return;

        lock (s_lock)
        {
            if (!s_active)
                return;

            s_events[s_eventWriteIndex] = new SlowEvent
            {
                TimestampTicks = DateTime.UtcNow.Ticks,
                Category = category ?? string.Empty,
                Pos = pos,
                ElapsedMs = elapsedMs,
                Details = details ?? string.Empty
            };
            s_eventWriteIndex = (s_eventWriteIndex + 1) % MaxEvents;
            if (s_eventCount < MaxEvents)
                s_eventCount++;
        }
    }

    /// <summary>Called from the Advanced Farming game-update hook; all engine/snapshot access remains on the main thread.</summary>
    public static void Pump()
    {
        if (!s_active) return;
        string intervalReport = null;
        string stopReport = null;
        lock (s_lock)
        {
            if (!s_active) return;
            float nowRealtime = UnityEngine.Time.realtimeSinceStartup;
            if (nowRealtime >= s_nextIntervalRealtime)
            {
                AdvancedFarmingPerfSnapshotService.SampleSnapshot now = AdvancedFarmingPerfSnapshotService.GetSnapshot();
                intervalReport = AdvancedFarmingPerfSnapshotService.BuildSamplingDeltaReport("stutterTrace interval", s_lastSnapshot, now);
                s_lastSnapshot = now;
                s_nextIntervalRealtime = nowRealtime + s_intervalSeconds;
            }
            long elapsedTicks = DateTime.UtcNow.Ticks - s_startUtcTicks;
            if ((int)(elapsedTicks / TimeSpan.TicksPerSecond) >= s_durationSeconds)
                stopReport = StopLocked(true);
        }
        if (!string.IsNullOrEmpty(intervalReport)) Log.Out(intervalReport);
        if (!string.IsNullOrEmpty(stopReport)) Log.Out(stopReport);
    }

    private static string StopLocked(bool includeReport)
    {
        bool wasActive = s_active;
        s_active = false;
        if (!s_samplingWasEnabled) AdvancedFarmingPerfSnapshotService.SetSampling(false);
        else AdvancedFarmingPerfSnapshotService.SetSampling(true);
        s_nextIntervalRealtime = 0f;

        if (!includeReport)
            return wasActive ? "[AdvancedFarming StutterTrace] stopped" : "[AdvancedFarming StutterTrace] stopped active=false";

        return BuildReportLocked(wasActive ? "stopped" : "inactive");
    }

    private static string BuildReportLocked(string label)
    {
        AdvancedFarmingPerfSnapshotService.SampleSnapshot now = AdvancedFarmingPerfSnapshotService.GetSnapshot();
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[AdvancedFarming StutterTrace] " + label
            + " active=" + s_active
            + " durationSeconds=" + s_durationSeconds
            + " intervalSeconds=" + s_intervalSeconds
            + " slowThresholdMs=" + s_slowThresholdMs.ToString("0.0")
            + " slowEvents=" + s_eventCount + "/" + MaxEvents);
        sb.AppendLine(AdvancedFarmingPerfSnapshotService.BuildSamplingDeltaReport("stutterTrace total", s_startSnapshot, now));
        AppendEventsLocked(sb);
        return sb.ToString();
    }

    private static void AppendEventsLocked(StringBuilder sb)
    {
        if (s_eventCount <= 0)
        {
            sb.AppendLine("[AdvancedFarming StutterTraceEvent] none");
            return;
        }

        int start = s_eventCount == MaxEvents ? s_eventWriteIndex : 0;
        for (int i = 0; i < s_eventCount; i++)
        {
            int index = (start + i) % MaxEvents;
            SlowEvent e = s_events[index];
            int elapsedSeconds = s_startUtcTicks > 0L ? (int)Math.Max(0L, (e.TimestampTicks - s_startUtcTicks) / TimeSpan.TicksPerSecond) : 0;
            sb.AppendLine("[AdvancedFarming StutterTraceEvent] t=" + elapsedSeconds
                + " category=" + e.Category
                + " elapsedMs=" + e.ElapsedMs.ToString("0.000")
                + " pos=" + e.Pos
                + " " + e.Details);
        }
    }

    private static double StopwatchTicksToMs(long elapsedStopwatchTicks)
    {
        double frequency = System.Diagnostics.Stopwatch.Frequency;
        return frequency > 0d ? (double)elapsedStopwatchTicks * 1000d / frequency : 0d;
    }
}
#endif
