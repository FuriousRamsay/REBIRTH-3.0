using System;
using System.Text;

#nullable disable

public enum RebirthToggleMode
{
    Unknown,
    DataOnly,
    CommandOnly,
    RuntimeGate,
    SchedulerJob,
    PatchInstall,
    PatchRuntimeGate,
    BuildProfileOnly
}

public enum RebirthToggleSafetyPolicy
{
    Unknown,
    CanToggleLive,
    RequiresWorldReload,
    RequiresMainMenu,
    RequiresRestart,
    ProfilingOnly,
    AlwaysOnStructural,
    ManualReviewRequired
}

public enum RebirthPerformanceImpactMode
{
    Unknown,
    NotPerformanceRelevant,
    RuntimeGateMeasurement,
    PatchInstallMeasurement,
    SchedulerCostMeasurement,
    DataIndexMeasurement,
    BuildComparisonOnly,
    ManualReviewRequired
}

public enum RebirthModuleToggleDefault
{
    Disabled,
    Enabled,
    InheritOption,
    ProfilingOnly,
    ManualReviewRequired
}

public sealed class RebirthModuleToggleDecl
{
    public string ModuleId;
    public string DisplayName;
    public RebirthToggleMode ToggleMode;
    public RebirthToggleSafetyPolicy SafetyPolicy;
    public RebirthPerformanceImpactMode ImpactMode;
    public RebirthModuleToggleDefault DefaultState;
    public bool IsHotPath;
    public bool CanMeasureImpact;
    public string HotMethod;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthToggleCommandPlanDecl
{
    public string Command;
    public string Purpose;
    public string Safety;
    public string Notes;
}

/// <summary>
/// Read-only toggle/performance impact contract registry.
/// This phase does not toggle any real gameplay module.
/// </summary>
public static class RebirthModuleToggleRegistry
{
    private static readonly RebirthModuleToggleDecl[] s_modules = new[]
    {
        new RebirthModuleToggleDecl
        {
            ModuleId = "player.movement",
            DisplayName = "Player movement adapter",
            ToggleMode = RebirthToggleMode.PatchRuntimeGate,
            SafetyPolicy = RebirthToggleSafetyPolicy.RequiresWorldReload,
            ImpactMode = RebirthPerformanceImpactMode.PatchInstallMeasurement,
            DefaultState = RebirthModuleToggleDefault.ManualReviewRequired,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "PlayerMoveController.Update",
            Evidence = "P2/P5/P6.5",
            Notes = "Future true patch-install measurement is important; runtime gate alone still measures patch entry overhead."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "player.crawl",
            DisplayName = "One-block crawl",
            ToggleMode = RebirthToggleMode.RuntimeGate,
            SafetyPolicy = RebirthToggleSafetyPolicy.CanToggleLive,
            ImpactMode = RebirthPerformanceImpactMode.RuntimeGateMeasurement,
            DefaultState = RebirthModuleToggleDefault.InheritOption,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "PlayerMoveController.Update",
            Evidence = "Blueprint v2 / prior crawl performance issues",
            Notes = "Should be measurable independently once movement adapter exists. Mounted/vehicle fast-exit must bypass it."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "combat.damage",
            DisplayName = "DamageEntity dispatcher",
            ToggleMode = RebirthToggleMode.PatchRuntimeGate,
            SafetyPolicy = RebirthToggleSafetyPolicy.RequiresWorldReload,
            ImpactMode = RebirthPerformanceImpactMode.PatchInstallMeasurement,
            DefaultState = RebirthModuleToggleDefault.ManualReviewRequired,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "EntityAlive.DamageEntity",
            Evidence = "P2",
            Notes = "Feature policies can runtime-gate; the dispatcher patch itself needs install/no-install profiling."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "pathing.vanilla",
            DisplayName = "Vanilla pathing adapter",
            ToggleMode = RebirthToggleMode.PatchRuntimeGate,
            SafetyPolicy = RebirthToggleSafetyPolicy.RequiresWorldReload,
            ImpactMode = RebirthPerformanceImpactMode.PatchInstallMeasurement,
            DefaultState = RebirthModuleToggleDefault.ManualReviewRequired,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "EntityMoveHelper.UpdateMoveHelper / EntityAlive.FindPath",
            Evidence = "P4/P5/V8",
            Notes = "Pathing toggles cannot casually change live behavior without controlled test scenario."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "pathing.localRoutes",
            DisplayName = "Local non-drop route guard",
            ToggleMode = RebirthToggleMode.SchedulerJob,
            SafetyPolicy = RebirthToggleSafetyPolicy.CanToggleLive,
            ImpactMode = RebirthPerformanceImpactMode.SchedulerCostMeasurement,
            DefaultState = RebirthModuleToggleDefault.InheritOption,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "local route search budget",
            Evidence = "P2.5",
            Notes = "Existing budget evidence is 1 route build per frame. Future scheduler can measure cost directly."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "items.stats",
            DisplayName = "ItemValue.ModifyValue dispatcher",
            ToggleMode = RebirthToggleMode.PatchRuntimeGate,
            SafetyPolicy = RebirthToggleSafetyPolicy.RequiresWorldReload,
            ImpactMode = RebirthPerformanceImpactMode.PatchInstallMeasurement,
            DefaultState = RebirthModuleToggleDefault.ManualReviewRequired,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "ItemValue.ModifyValue",
            Evidence = "Blueprint v2 / prior ItemValue performance findings",
            Notes = "True impact needs patch-install profiles; per-policy runtime gates still useful."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "paint.render",
            DisplayName = "Paint renderFace adapter",
            ToggleMode = RebirthToggleMode.PatchRuntimeGate,
            SafetyPolicy = RebirthToggleSafetyPolicy.RequiresWorldReload,
            ImpactMode = RebirthPerformanceImpactMode.PatchInstallMeasurement,
            DefaultState = RebirthModuleToggleDefault.ManualReviewRequired,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "BlockShapeNew.renderFace",
            Evidence = "Blueprint v2 / prior paint/render findings",
            Notes = "No-data fast path can be measured separately from patch-install overhead."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "world.caves",
            DisplayName = "Caves/worldgen",
            ToggleMode = RebirthToggleMode.DataOnly,
            SafetyPolicy = RebirthToggleSafetyPolicy.RequiresWorldReload,
            ImpactMode = RebirthPerformanceImpactMode.BuildComparisonOnly,
            DefaultState = RebirthModuleToggleDefault.ManualReviewRequired,
            IsHotPath = false,
            CanMeasureImpact = true,
            HotMethod = "WorldBuilder.GenerateData",
            Evidence = "P3/P5A2",
            Notes = "Generation impact is measured by worldgen/load tests, not normal frame toggles."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "ui.crosshair",
            DisplayName = "Crosshair rendering",
            ToggleMode = RebirthToggleMode.RuntimeGate,
            SafetyPolicy = RebirthToggleSafetyPolicy.CanToggleLive,
            ImpactMode = RebirthPerformanceImpactMode.RuntimeGateMeasurement,
            DefaultState = RebirthModuleToggleDefault.InheritOption,
            IsHotPath = false,
            CanMeasureImpact = true,
            HotMethod = "crosshair-specific draw",
            Evidence = "P4/P6",
            Notes = "Must not patch GUIUtils.DrawLine globally. Runtime toggle should be safe once native render path exists."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "deployables.drones",
            DisplayName = "Drone follow-lock",
            ToggleMode = RebirthToggleMode.RuntimeGate,
            SafetyPolicy = RebirthToggleSafetyPolicy.CanToggleLive,
            ImpactMode = RebirthPerformanceImpactMode.RuntimeGateMeasurement,
            DefaultState = RebirthModuleToggleDefault.InheritOption,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "drone-specific active set",
            Evidence = "P4/V8",
            Notes = "Must not patch Entity.OnUpdatePosition globally."
        },
        new RebirthModuleToggleDecl
        {
            ModuleId = "diagnostics.profiling",
            DisplayName = "Profiling diagnostics",
            ToggleMode = RebirthToggleMode.BuildProfileOnly,
            SafetyPolicy = RebirthToggleSafetyPolicy.ProfilingOnly,
            ImpactMode = RebirthPerformanceImpactMode.BuildComparisonOnly,
            DefaultState = RebirthModuleToggleDefault.ProfilingOnly,
            IsHotPath = true,
            CanMeasureImpact = true,
            HotMethod = "many",
            Evidence = "debug policy / prior profiler concern",
            Notes = "Release builds should not carry always-patched diagnostic Harmony wrappers."
        }
    };

    private static readonly RebirthToggleCommandPlanDecl[] s_commands = new[]
    {
        new RebirthToggleCommandPlanDecl
        {
            Command = "rbtogglefresh list",
            Purpose = "List planned toggle contracts.",
            Safety = "Read-only in Phase 1M.",
            Notes = "No feature state is changed yet."
        },
        new RebirthToggleCommandPlanDecl
        {
            Command = "rbtogglefresh impact",
            Purpose = "List how each module should be measured.",
            Safety = "Read-only in Phase 1M.",
            Notes = "Distinguishes runtime gate vs patch install vs scheduler vs build comparison."
        },
        new RebirthToggleCommandPlanDecl
        {
            Command = "rbtogglefresh policy <module>",
            Purpose = "Explain live/reload/restart/profiling-only safety for a module.",
            Safety = "Read-only in Phase 1M.",
            Notes = "Future real command must refuse unsafe live toggles."
        },
        new RebirthToggleCommandPlanDecl
        {
            Command = "rbsys perf baseline",
            Purpose = "Future controlled baseline capture.",
            Safety = "Not implemented yet.",
            Notes = "Needs profiler/FPS capture integration."
        },
        new RebirthToggleCommandPlanDecl
        {
            Command = "rbsys perf off <module>",
            Purpose = "Future performance impact test for a module.",
            Safety = "Not implemented yet.",
            Notes = "Must obey each module's SafetyPolicy and ToggleMode."
        },
        new RebirthToggleCommandPlanDecl
        {
            Command = "rbsys sweep",
            Purpose = "Future controlled toggle sweep.",
            Safety = "Not implemented yet.",
            Notes = "Should output module, enabled/disabled, fps, frame time, GC alloc, hot method counts, scheduler cost, notes."
        }
    };

    public static RebirthModuleToggleDecl[] GetSnapshot()
    {
        RebirthModuleToggleDecl[] copy = new RebirthModuleToggleDecl[s_modules.Length];
        Array.Copy(s_modules, copy, copy.Length);
        return copy;
    }

    public static string GetListReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthToggles] read-only module toggle contract ledger.");
        sb.AppendLine("module | mode | safety | impact | default | hot | measurable | hot method | evidence | notes");

        for (int i = 0; i < s_modules.Length; i++)
        {
            RebirthModuleToggleDecl m = s_modules[i];
            if (!string.IsNullOrEmpty(f)
                && (m.ModuleId == null || m.ModuleId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && (m.DisplayName == null || m.DisplayName.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0))
                continue;

            sb.Append(m.ModuleId).Append(" | ")
              .Append(m.ToggleMode).Append(" | ")
              .Append(m.SafetyPolicy).Append(" | ")
              .Append(m.ImpactMode).Append(" | ")
              .Append(m.DefaultState).Append(" | ")
              .Append(m.IsHotPath).Append(" | ")
              .Append(m.CanMeasureImpact).Append(" | ")
              .Append(m.HotMethod).Append(" | ")
              .Append(m.Evidence).Append(" | ")
              .AppendLine(m.Notes);
        }

        return sb.ToString();
    }

    public static string GetImpactReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthToggles] performance impact measurement plan.");
        sb.AppendLine("module | impact mode | toggle mode | safety | hot method | notes");

        for (int i = 0; i < s_modules.Length; i++)
        {
            RebirthModuleToggleDecl m = s_modules[i];
            if (!m.CanMeasureImpact)
                continue;

            sb.Append(m.ModuleId).Append(" | ")
              .Append(m.ImpactMode).Append(" | ")
              .Append(m.ToggleMode).Append(" | ")
              .Append(m.SafetyPolicy).Append(" | ")
              .Append(m.HotMethod).Append(" | ")
              .AppendLine(m.Notes);
        }

        return sb.ToString();
    }

    public static string GetPolicyReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthToggles] toggle safety policy report.");

        for (int i = 0; i < s_modules.Length; i++)
        {
            RebirthModuleToggleDecl m = s_modules[i];
            if (!string.IsNullOrEmpty(f)
                && (m.ModuleId == null || m.ModuleId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && (m.DisplayName == null || m.DisplayName.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0))
                continue;

            sb.Append("  ").Append(m.ModuleId)
              .Append(": mode=").Append(m.ToggleMode)
              .Append(", safety=").Append(m.SafetyPolicy)
              .Append(", impact=").Append(m.ImpactMode)
              .Append(", default=").Append(m.DefaultState)
              .Append(", hot=").Append(m.IsHotPath)
              .Append(", note=").AppendLine(m.Notes);
        }

        return sb.ToString();
    }

    public static string GetCommandPlanReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthToggles] future command plan.");
        sb.AppendLine("command | purpose | safety | notes");

        for (int i = 0; i < s_commands.Length; i++)
        {
            RebirthToggleCommandPlanDecl c = s_commands[i];
            sb.Append(c.Command).Append(" | ")
              .Append(c.Purpose).Append(" | ")
              .Append(c.Safety).Append(" | ")
              .AppendLine(c.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthToggles] safety rules:");
        sb.AppendLine("  1. Phase 1M is read-only; it does not change feature state.");
        sb.AppendLine("  2. RuntimeGate toggles can measure feature behavior but not patch-entry overhead.");
        sb.AppendLine("  3. PatchInstall measurements require reload/restart/main-menu policy per module.");
        sb.AppendLine("  4. SchedulerJob measurements require jobs to report cost through RebirthFrameScheduler.");
        sb.AppendLine("  5. Profiling diagnostics must not ship as always-patched release wrappers.");
        sb.AppendLine("  6. Future commands must refuse unsafe live toggles.");
        sb.AppendLine("  7. Every real module must declare ToggleMode, SafetyPolicy, ImpactMode, and DefaultState before migration.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthToggles] Toggle contract entries: " + s_modules.Length
            + "; future command plans: " + s_commands.Length
            + ". This phase is read-only and does not toggle gameplay.";
    }
}
