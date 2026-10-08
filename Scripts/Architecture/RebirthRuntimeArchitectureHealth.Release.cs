#if !DEBUG
#nullable disable

public enum RebirthRuntimeHealthStatus
{
    Unknown,
    Green,
    Yellow,
    Red,
    Blocked
}

/// <summary>
/// Release compatibility facade. Architecture-health diagnostics are omitted.
/// </summary>
public static class RebirthRuntimeArchitectureHealth
{
    private static string Disabled() { return "[RebirthRuntimeHealth] Debug build required."; }
    public static string GetSummaryReport() { return Disabled(); }
    public static string GetFullReport() { return Disabled(); }
    public static string GetBlockersReport() { return Disabled(); }
    public static string GetRecommendedNextReport() { return Disabled(); }
    public static string GetSafetyReport() { return Disabled(); }
}
#endif
