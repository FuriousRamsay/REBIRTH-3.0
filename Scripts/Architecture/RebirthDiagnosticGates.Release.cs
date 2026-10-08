#if !DEBUG
#nullable disable

public enum RebirthDiagnosticGateKind
{
    Unknown,
    ReleaseSafe,
    DeveloperOnly,
    ProfilingOnly,
    CompatibilityAuditOnly,
    ManualReviewRequired
}

public enum RebirthDiagnosticGateCostClass
{
    Unknown,
    FreeFlagRead,
    CheapBranch,
    Allocates,
    ScansCollections,
    UsesReflection,
    CapturesStackTrace,
    ManualReviewRequired
}

/// <summary>
/// Release compatibility facade. All diagnostic gates are permanently disabled.
/// </summary>
public static class RebirthDiagnosticGates
{
    public const bool ReleaseDiagnosticsEnabled = false;
    public const bool DeveloperDiagnosticsEnabled = false;
    public const bool ProfilingDiagnosticsEnabled = false;
    public const bool CompatibilityAuditDiagnosticsEnabled = false;
    public static bool IsReleaseDiagnosticEnabled { get { return false; } }
    public static bool IsDeveloperDiagnosticEnabled { get { return false; } }
    public static bool IsProfilingDiagnosticEnabled { get { return false; } }
    public static bool IsCompatibilityAuditDiagnosticEnabled { get { return false; } }
    public static bool IsAnyDiagnosticEnabled { get { return false; } }
    public static bool CanBuildDeveloperMessage() { return false; }
    public static bool CanBuildProfilingMessage() { return false; }
    public static bool CanBuildCompatibilityAuditMessage() { return false; }
    public static bool CanPerformExpensiveDiagnosticWork(RebirthDiagnosticGateKind kind) { return false; }
    private static string Disabled() { return "[RebirthDiagnostics] Debug build required."; }
    public static string GetSummaryReport() { return Disabled(); }
    public static string GetRulesReport() { return Disabled(); }
    public static string GetExampleReport() { return Disabled(); }
    public static string GetSafetyReport() { return Disabled(); }
}
#endif
