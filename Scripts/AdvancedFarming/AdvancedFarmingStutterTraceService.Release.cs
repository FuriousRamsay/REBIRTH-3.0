#if !DEBUG
#nullable disable

/// <summary>
/// Release compatibility facade. The stutter-trace implementation is compiled only in Debug builds.
/// </summary>
public static class AdvancedFarmingStutterTraceService
{
    public static bool Active { get { return false; } }
    public static bool ShouldRecordSlow(long elapsedStopwatchTicks) { return false; }
    public static string StartDefault() { return "[AdvancedFarming] Stutter trace is available only in a Debug build."; }
    public static string Start(int durationSeconds, int intervalSeconds, double slowThresholdMs) { return StartDefault(); }
    public static string Stop() { return StartDefault(); }
    public static string Status() { return StartDefault(); }
    public static void RecordSlowOperation(string category, Vector3i pos, long elapsedStopwatchTicks, string details) { }
}
#endif
