#if !DEBUG
#nullable disable

/// <summary>
/// Release compatibility facade. Hybrid-path performance benchmarking is compiled
/// only in Debug builds and adds no timing or census work in Release.
/// </summary>
public static class RebirthHybridPathPerformanceBenchmark
{
    public static bool IsMeasuring { get { return false; } }
    public static string Start(EntityPlayer requester, float seconds) { return "[RebirthHybridPathPerf] Debug build required."; }
    public static void Pump() { }
    public static string Stop() { return "[RebirthHybridPathPerf] Debug build required."; }
    public static void Abort(string reason) { }
    public static long BeginPathEvaluation() { return 0L; }
    public static void EndPathEvaluation(long startTimestamp, EntityMoveHelper moveHelper) { }
    public static long BeginClearanceQuery() { return 0L; }
    public static void EndClearanceQuery(long startTimestamp, bool hitSomething) { }
}
#endif
