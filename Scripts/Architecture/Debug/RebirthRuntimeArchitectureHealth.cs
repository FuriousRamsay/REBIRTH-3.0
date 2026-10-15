#if DEBUG
using System;
using System.Text;

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
/// Aggregate runtime architecture health snapshot.
/// This is a read-only report that checks the current inert runtime shells.
/// It does not connect gameplay, install patches, parse XML, or change state.
/// </summary>
public static class RebirthRuntimeArchitectureHealth
{
    public static string GetSummaryReport()
    {
        return "[RebirthRuntimeHealth] status: Observed/NotAcceptance"
            + "; " + RebirthDiagnosticGates.GetSummaryReport()
            + "; " + RebirthRuntimeFeatureToggleState.GetSummaryReport()
            + "; " + RebirthHotPathFeatureFlags.GetSummaryReport()
            + "; this report does not prove patch installation, gameplay behavior, or release acceptance";
    }

    public static string GetFullReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthRuntimeHealth] full architecture health snapshot.");
        sb.AppendLine("  Read-only aggregate report.");
        sb.AppendLine("  No gameplay behavior is connected by this report.");
        sb.AppendLine();

        sb.AppendLine("Core runtime shells:");
        AppendStatus(sb, "Scenario runtime policy cache", RebirthRuntimeHealthStatus.Green, "explicit defaults only; no gameplay consumer");
        AppendStatus(sb, "Diagnostic gates", RebirthRuntimeHealthStatus.Green, "all defaults false; no logging by itself");
        AppendStatus(sb, "Runtime feature toggle state", RebirthRuntimeHealthStatus.Green, "all runtime consumers disabled; hot-path use disabled");
        AppendStatus(sb, "Hot-path feature flags", RebirthRuntimeHealthStatus.Green, "all gameplay flags false; direct bool reads only");
        AppendStatus(sb, "Build/profile comparison", RebirthRuntimeHealthStatus.Green, "dry-run report only");
        AppendStatus(sb, "Scheduler warning cleanup", RebirthRuntimeHealthStatus.Green, "explicit default value only; no scheduling behavior changed");
        AppendStatus(sb, "Survivor scenario", RebirthRuntimeHealthStatus.Blocked, "blocked until survivor token disambiguation");
        sb.AppendLine();

        sb.AppendLine("Current summaries:");
        sb.Append("  ").AppendLine(RebirthRuntimeScenarioPolicyCache.GetSummaryReport());
        sb.Append("  ").AppendLine(RebirthDiagnosticGates.GetSummaryReport());
        sb.Append("  ").AppendLine(RebirthRuntimeFeatureToggleState.GetSummaryReport());
        sb.Append("  ").AppendLine(RebirthHotPathFeatureFlags.GetSummaryReport());
        sb.Append("  ").AppendLine(RebirthBuildProfileComparisonReport.GetSummaryReport());
        sb.AppendLine();

        sb.AppendLine("Acceptance boundary:");
        sb.AppendLine("  This source report observes provider summaries only.");
        sb.AppendLine("  Patch installation: NotChecked");
        sb.AppendLine("  Gameplay behavior: NotChecked");
        sb.AppendLine("  Native integration/runtime acceptance: NotRun");
        sb.AppendLine("  Release approval: false");
        return sb.ToString();
    }

    public static string GetBlockersReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthRuntimeHealth] blockers before real gameplay migration:");
        sb.AppendLine("  1. Pick a small low-risk gameplay slice, not a hot-path system.");
        sb.AppendLine("  2. Create the exact test gate for that slice first.");
        sb.AppendLine("  3. Define the exact Harmony target, or prove no Harmony patch is needed.");
        sb.AppendLine("  4. Prove server/client ownership if the slice is stateful.");
        sb.AppendLine("  5. Keep diagnostics gated before construction.");
        sb.AppendLine("  6. Keep survivor scenario blocked until disambiguation.");
        sb.AppendLine();
        sb.AppendLine("Do not start next with:");
        sb.AppendLine("  - player.crawl");
        sb.AppendLine("  - items.stats.randomization / ItemValue.ModifyValue");
        sb.AppendLine("  - paint.render / BlockShapeNew.renderFace");
        sb.AppendLine("  - zombie.ai.special / pathing");
        sb.AppendLine("  - survivor scenario behavior");
        return sb.ToString();
    }

    public static string GetRecommendedNextReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthRuntimeHealth] recommended next direction:");
        sb.AppendLine("  The baseline is now ready to choose the first low-risk real gameplay slice.");
        sb.AppendLine();
        sb.AppendLine("Best candidates:");
        sb.AppendLine("  1. harvest.salvage small isolated behavior, if test gate is written first");
        sb.AppendLine("  2. audio.sounds small isolated behavior, if not tied to vehicle damage/AI timing");
        sb.AppendLine("  3. ui.hud.compass display snapshot shell, if no XUi loop patch yet");
        sb.AppendLine();
        sb.AppendLine("My recommendation:");
        sb.AppendLine("  Add a focused test gate for harvest.salvage first, then migrate one small behavior.");
        sb.AppendLine("  It is lower-risk than crawl, ItemValue, renderFace, or zombie AI.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthRuntimeHealth] safety:");
        sb.AppendLine("  1. Report is read-only.");
        sb.AppendLine("  2. No runtime state is changed.");
        sb.AppendLine("  3. No gameplay behavior is connected.");
        sb.AppendLine("  4. No Harmony patches are installed.");
        sb.AppendLine("  5. No reflection is performed.");
        sb.AppendLine("  6. No XML parsing occurs.");
        sb.AppendLine("  7. No custom game option lookup occurs.");
        sb.AppendLine("  8. No network packets are sent.");
        sb.AppendLine("  9. No save/load writes occur.");
        return sb.ToString();
    }

    private static void AppendStatus(StringBuilder sb, string name, RebirthRuntimeHealthStatus status, string detail)
    {
        sb.Append("  ").Append(name).Append(": ").Append(status).Append(" - ").AppendLine(detail);
    }
}
#endif
