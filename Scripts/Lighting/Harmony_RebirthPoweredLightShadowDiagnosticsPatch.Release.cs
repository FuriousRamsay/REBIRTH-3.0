#if !DEBUG
#nullable disable

/// <summary>
/// Release compatibility facade. Powered-light shadow diagnostics and their Harmony patch are omitted.
/// </summary>
public static class Harmony_RebirthPoweredLightShadowDiagnosticsPatch
{
    public static bool DisablePoweredLightShadows;
    public static bool PatchInstalled { get { return false; } }
    public static void Postfix(LightLOD instance) { }
    private static string Disabled() { return "Powered-light shadow diagnostics require a Debug build."; }
    public static string SetDisabled(bool disabled) { DisablePoweredLightShadows = false; return Disabled(); }
    public static string Toggle() { return Disabled(); }
    public static string BuildStatus() { return Disabled(); }
    public static int ApplyImmediateSuppression() { return 0; }
    public static int ApplyImmediateRestore() { return 0; }
}
#endif
