#if !DEBUG
#nullable disable

public enum RebirthPerfDryRunAction
{
    Unknown,
    Baseline,
    EnableModule,
    DisableModule,
    Sweep,
    Status
}

public enum RebirthPerfDryRunSafety
{
    Unknown,
    SafeLiveDryRunOnly,
    WouldRequireWorldReload,
    WouldRequireMainMenu,
    WouldRequireRestart,
    ProfilingOnly,
    BlockedManualReview,
    BlockedStructural
}

public sealed class RebirthPerfDryRunResult
{
    public string ModuleId;
    public RebirthPerfDryRunAction Action;
    public RebirthPerfDryRunSafety Safety;
    public string ImpactMode;
    public string ToggleMode;
    public string HotMethod;
    public string Message;
}

/// <summary>
/// Release compatibility facade. Performance dry-run planning is a Debug-only tool.
/// </summary>
public static class RebirthPerformanceDryRunPlanner
{
    private static string Disabled() { return "[RebirthPerfDryRun] Debug build required."; }
    public static string GetSummaryReport() { return Disabled(); }
    public static string GetBaselinePlanReport() { return Disabled(); }
    public static string GetDryRunReport(bool enable, string moduleFilter) { return Disabled(); }
    public static string GetSweepPlanReport() { return Disabled(); }
    public static string GetSafetyReport() { return Disabled(); }
}
#endif
