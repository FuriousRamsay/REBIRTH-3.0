#if DEBUG
using System;
using System.Text;

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
/// Read-only performance toggle dry-run planner.
/// This phase does not collect FPS, toggle modules, patch/unpatch Harmony, or write state.
/// </summary>
public static class RebirthPerformanceDryRunPlanner
{
    public static string GetSummaryReport()
    {
        return "[RebirthPerfDryRun] Dry-run planner ready. It validates future toggle/perf actions and includes shadow toggle state without changing gameplay.";
    }

    public static string GetBaselinePlanReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPerfDryRun] future baseline capture plan.");
        sb.AppendLine("  Phase 1N does not capture FPS/frame/GC yet.");
        sb.AppendLine("  Future baseline should record:");
        sb.AppendLine("    - scene/world identifier");
        sb.AppendLine("    - player position and mounted state");
        sb.AppendLine("    - module toggle snapshot");
        sb.AppendLine("    - FPS / frame time");
        sb.AppendLine("    - GC allocation and collection count");
        sb.AppendLine("    - scheduler cost by job");
        sb.AppendLine("    - hot-method call counters where profiling build allows it");
        sb.AppendLine("    - active Harmony patch owner summary");
        return sb.ToString();
    }

    public static string GetDryRunReport(bool enable, string moduleFilter)
    {
        string filter = (moduleFilter ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(filter))
            return "[RebirthPerfDryRun] Missing module id/filter. Example: rbperffresh dryrun off player.crawl";

        RebirthModuleToggleDecl[] modules = RebirthModuleToggleRegistry.GetSnapshot();

        StringBuilder sb = new StringBuilder(8192);
        sb.Append("[RebirthPerfDryRun] dry-run ")
          .Append(enable ? "enable" : "disable")
          .Append(" for filter: ")
          .AppendLine(filter);
        sb.AppendLine("module | shadow state | action | safety | mode | impact | hot method | message");

        bool any = false;
        for (int i = 0; i < modules.Length; i++)
        {
            RebirthModuleToggleDecl m = modules[i];
            if (!Matches(m, filter))
                continue;

            any = true;
            RebirthPerfDryRunResult r = BuildResult(m, enable);

            RebirthModuleShadowToggle shadow = RebirthModuleToggleStateRegistry.GetShadowStateSnapshot(m.ModuleId);

            sb.Append(r.ModuleId).Append(" | ")
              .Append(shadow.State).Append(" | ")
              .Append(r.Action).Append(" | ")
              .Append(r.Safety).Append(" | ")
              .Append(r.ToggleMode).Append(" | ")
              .Append(r.ImpactMode).Append(" | ")
              .Append(r.HotMethod).Append(" | ")
              .AppendLine(r.Message);
        }

        if (!any)
            sb.AppendLine("No module matched the filter.");

        sb.AppendLine("No state was changed. This is a planning report only.");
        return sb.ToString();
    }

    public static string GetSweepPlanReport()
    {
        RebirthModuleToggleDecl[] modules = RebirthModuleToggleRegistry.GetSnapshot();

        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthPerfDryRun] future sweep plan.");
        sb.AppendLine("  Phase 1N does not execute the sweep.");
        sb.AppendLine("  Suggested ordering keeps safer runtime-gated tests before reload/restart/structural tests.");
        sb.AppendLine("order | module | shadow state | mode | safety | impact | hot method | notes");

        int order = 1;
        for (int pass = 0; pass < 4; pass++)
        {
            for (int i = 0; i < modules.Length; i++)
            {
                RebirthModuleToggleDecl m = modules[i];
                if (!BelongsToSweepPass(m, pass))
                    continue;

                RebirthModuleShadowToggle shadow = RebirthModuleToggleStateRegistry.GetShadowStateSnapshot(m.ModuleId);

                sb.Append(order++).Append(" | ")
                  .Append(m.ModuleId).Append(" | ")
                  .Append(shadow.State).Append(" | ")
                  .Append(m.ToggleMode).Append(" | ")
                  .Append(m.SafetyPolicy).Append(" | ")
                  .Append(m.ImpactMode).Append(" | ")
                  .Append(m.HotMethod).Append(" | ")
                  .AppendLine(m.Notes);
            }
        }

        sb.AppendLine("Future sweep output should include: module, requested state, actual state, fps, frame time, GC alloc, scheduler cost, hot counters, and notes.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPerfDryRun] safety rules:");
        sb.AppendLine("  1. Phase 1N never changes module state.");
        sb.AppendLine("  2. Phase 1N never patches or unpatches Harmony.");
        sb.AppendLine("  3. RuntimeGate modules are safest future live-toggle candidates.");
        sb.AppendLine("  4. PatchRuntimeGate and PatchInstall modules require reload/restart/main-menu policy before true measurement.");
        sb.AppendLine("  5. ProfilingOnly modules must not ship as release always-patched diagnostic wrappers.");
        sb.AppendLine("  6. ManualReviewRequired modules must be blocked until explicitly reviewed.");
        sb.AppendLine("  7. Future performance capture must record enough context to compare runs fairly.");
        sb.AppendLine("  8. Shadow toggle state is report context only until real module toggles are connected.");
        return sb.ToString();
    }

    private static RebirthPerfDryRunResult BuildResult(RebirthModuleToggleDecl m, bool enable)
    {
        RebirthPerfDryRunResult r = new RebirthPerfDryRunResult();
        r.ModuleId = m.ModuleId;
        r.Action = enable ? RebirthPerfDryRunAction.EnableModule : RebirthPerfDryRunAction.DisableModule;
        r.ToggleMode = m.ToggleMode.ToString();
        r.ImpactMode = m.ImpactMode.ToString();
        r.HotMethod = m.HotMethod;

        switch (m.SafetyPolicy)
        {
            case RebirthToggleSafetyPolicy.CanToggleLive:
                r.Safety = RebirthPerfDryRunSafety.SafeLiveDryRunOnly;
                r.Message = "Future live toggle candidate. Runtime-gate measurement can be meaningful once behavior exists.";
                break;

            case RebirthToggleSafetyPolicy.RequiresWorldReload:
                r.Safety = RebirthPerfDryRunSafety.WouldRequireWorldReload;
                r.Message = "Future action should require a world reload or controlled test reset.";
                break;

            case RebirthToggleSafetyPolicy.RequiresMainMenu:
                r.Safety = RebirthPerfDryRunSafety.WouldRequireMainMenu;
                r.Message = "Future action should only be allowed from main menu.";
                break;

            case RebirthToggleSafetyPolicy.RequiresRestart:
                r.Safety = RebirthPerfDryRunSafety.WouldRequireRestart;
                r.Message = "Future action should require process restart/build-profile comparison.";
                break;

            case RebirthToggleSafetyPolicy.ProfilingOnly:
                r.Safety = RebirthPerfDryRunSafety.ProfilingOnly;
                r.Message = "Future action belongs to profiling builds only, not release.";
                break;

            case RebirthToggleSafetyPolicy.AlwaysOnStructural:
                r.Safety = RebirthPerfDryRunSafety.BlockedStructural;
                r.Message = "Future live toggle should be blocked because this is structural.";
                break;

            case RebirthToggleSafetyPolicy.ManualReviewRequired:
            default:
                r.Safety = RebirthPerfDryRunSafety.BlockedManualReview;
                r.Message = "Future action must be blocked until the module receives explicit migration review.";
                break;
        }

        if (m.DefaultState == RebirthModuleToggleDefault.ManualReviewRequired)
            r.Message += " Default state is manual-review.";

        if (m.ToggleMode == RebirthToggleMode.PatchRuntimeGate || m.ToggleMode == RebirthToggleMode.PatchInstall)
            r.Message += " Runtime gate will not measure true patch-install overhead.";

        return r;
    }

    private static bool Matches(RebirthModuleToggleDecl m, string filter)
    {
        if (m == null)
            return false;

        if (m.ModuleId != null && m.ModuleId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.DisplayName != null && m.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.HotMethod != null && m.HotMethod.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private static bool BelongsToSweepPass(RebirthModuleToggleDecl m, int pass)
    {
        if (m == null)
            return false;

        if (pass == 0)
            return m.SafetyPolicy == RebirthToggleSafetyPolicy.CanToggleLive;

        if (pass == 1)
            return m.SafetyPolicy == RebirthToggleSafetyPolicy.RequiresWorldReload;

        if (pass == 2)
            return m.SafetyPolicy == RebirthToggleSafetyPolicy.RequiresMainMenu
                || m.SafetyPolicy == RebirthToggleSafetyPolicy.RequiresRestart;

        return m.SafetyPolicy == RebirthToggleSafetyPolicy.ProfilingOnly
            || m.SafetyPolicy == RebirthToggleSafetyPolicy.ManualReviewRequired
            || m.SafetyPolicy == RebirthToggleSafetyPolicy.AlwaysOnStructural;
    }
}
#endif
