/// <summary>
/// Caller-side optional-work policy. Category configuration, build eligibility, manual
/// sessions and essential errors are separate controls; there is no new master switch.
/// This file intentionally lives outside Debug so release callers can use it.
/// </summary>
public static class RebirthDiagnosticPolicy
{
    // Build eligibility is distinct from category configuration and manual capture scopes.
    public static bool DeveloperBuildEligible
    {
        get
        {
#if DEBUG || REBIRTH_DEBUG
            return true;
#else
            return false;
#endif
        }
    }
    public static bool MayPrepare(bool categoryEnabled) { return categoryEnabled; }

    public static void TickManualScopes()
    {
        RebirthCookingDiagnostics.Tick();
        QuickStackHotkeyDiagnostics.Tick();
        RebirthDroneQualitySpeed.TickTelemetry();
    }

    public static void ResetManualScopes()
    {
        RebirthCookingDiagnostics.Stop();
        QuickStackHotkeyDiagnostics.SetEnabled(false);
        RebirthDroneQualitySpeed.StopTelemetry();
        XUiC_RebirthExpandableBackpackScroll.ResetDiagnosticsForWorldChange();
        RebirthHeatMapCheckToSpawnPatch.ResetWarnings();
    }

}
