using System;
using System.Text;

#nullable disable

public enum RebirthPatchAdapterKind
{
    Unknown,
    NoPatch,
    PrefixRuntimeGate,
    PostfixRuntimeGate,
    TranspilerExcluded,
    ReversePatchExcluded,
    BuildProfileOnlyPatch,
    StructuralPatch
}

public enum RebirthPatchAdapterStatus
{
    Planned,
    ScaffoldedNoBehavior,
    Disabled,
    ExcludedByDefault,
    ManualReviewRequired,
    ProfilingOnly
}

public enum RebirthPatchAdapterInstallPolicy
{
    Unknown,
    NeverInstall,
    RuntimeGateOnly,
    MainMenuOnly,
    WorldReloadRequired,
    RestartRequired,
    ProfilingBuildOnly,
    ManualReviewRequired
}

public sealed class RebirthPatchAdapterDecl
{
    public string AdapterId;
    public string ModuleId;
    public string TargetMethod;
    public RebirthPatchAdapterKind AdapterKind;
    public RebirthPatchAdapterStatus Status;
    public RebirthPatchAdapterInstallPolicy InstallPolicy;
    public bool IsHotPath;
    public bool AllowsPatchInstallMeasurement;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// Read-only patch adapter plan registry.
/// This does not call Harmony.Patch, Harmony.Unpatch, or PatchAll.
/// </summary>
public static class RebirthPatchAdapterPlanRegistry
{
    private static readonly RebirthPatchAdapterDecl[] s_adapters = new[]
    {
        new RebirthPatchAdapterDecl
        {
            AdapterId = "adapter.player.move.update",
            ModuleId = "player.movement",
            TargetMethod = "PlayerMoveController.Update",
            AdapterKind = RebirthPatchAdapterKind.PrefixRuntimeGate,
            Status = RebirthPatchAdapterStatus.Planned,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.WorldReloadRequired,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = true,
            Evidence = "P2/P5/P6.5",
            Notes = "Future single PlayerMoveController adapter. Runtime gate alone does not measure patch-entry overhead."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "adapter.combat.damage",
            ModuleId = "combat.damage",
            TargetMethod = "EntityAlive.DamageEntity",
            AdapterKind = RebirthPatchAdapterKind.PrefixRuntimeGate,
            Status = RebirthPatchAdapterStatus.Planned,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.WorldReloadRequired,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = true,
            Evidence = "P2",
            Notes = "Future single damage dispatcher. Individual features register policies."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "adapter.pathing.movehelper",
            ModuleId = "pathing.vanilla",
            TargetMethod = "EntityMoveHelper.UpdateMoveHelper",
            AdapterKind = RebirthPatchAdapterKind.PrefixRuntimeGate,
            Status = RebirthPatchAdapterStatus.ManualReviewRequired,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.WorldReloadRequired,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = true,
            Evidence = "P4/P5/V8",
            Notes = "Pathing is high-risk. No target reacquisition behavior belongs in adapter scaffold."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "adapter.pathing.findpath",
            ModuleId = "pathing.vanilla",
            TargetMethod = "EntityAlive.FindPath",
            AdapterKind = RebirthPatchAdapterKind.PrefixRuntimeGate,
            Status = RebirthPatchAdapterStatus.ManualReviewRequired,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.WorldReloadRequired,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = true,
            Evidence = "P4",
            Notes = "Must account for PathSmoothing collision without absorbing A* internals by default."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "adapter.items.modifyvalue",
            ModuleId = "items.stats",
            TargetMethod = "ItemValue.ModifyValue",
            AdapterKind = RebirthPatchAdapterKind.PostfixRuntimeGate,
            Status = RebirthPatchAdapterStatus.Planned,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.WorldReloadRequired,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = true,
            Evidence = "Blueprint v2 / prior ItemValue perf findings",
            Notes = "Future adapter must avoid object[] args, scans, XML lookups, and tooltip construction."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "adapter.paint.renderface",
            ModuleId = "paint.render",
            TargetMethod = "BlockShapeNew.renderFace",
            AdapterKind = RebirthPatchAdapterKind.PrefixRuntimeGate,
            Status = RebirthPatchAdapterStatus.Planned,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.WorldReloadRequired,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = true,
            Evidence = "Blueprint v2",
            Notes = "Future adapter must have no-data fast path before coordinate/dictionary work."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "adapter.worldgen.generatedata",
            ModuleId = "world.caves",
            TargetMethod = "WorldBuilder.GenerateData",
            AdapterKind = RebirthPatchAdapterKind.StructuralPatch,
            Status = RebirthPatchAdapterStatus.ManualReviewRequired,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.RestartRequired,
            IsHotPath = false,
            AllowsPatchInstallMeasurement = false,
            Evidence = "P5A2 severe drift",
            Notes = "Do not mechanically port 2.6 TheDescent worldgen hooks to 3.0."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "excluded.astar.gridgraph.linecast",
            ModuleId = "pathing.astarRisk",
            TargetMethod = "Pathfinding.GridGraph.Linecast",
            AdapterKind = RebirthPatchAdapterKind.TranspilerExcluded,
            Status = RebirthPatchAdapterStatus.ExcludedByDefault,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.NeverInstall,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = false,
            Evidence = "P4",
            Notes = "A* internal patch excluded by default."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "excluded.astar.layergridgraph.getnearest",
            ModuleId = "pathing.astarRisk",
            TargetMethod = "Pathfinding.LayerGridGraph.GetNearest",
            AdapterKind = RebirthPatchAdapterKind.TranspilerExcluded,
            Status = RebirthPatchAdapterStatus.ExcludedByDefault,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.NeverInstall,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = false,
            Evidence = "P4",
            Notes = "A* internal patch excluded by default."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "excluded.astar.raycastmodifier.validateline",
            ModuleId = "pathing.astarRisk",
            TargetMethod = "Pathfinding.RaycastModifier.ValidateLine",
            AdapterKind = RebirthPatchAdapterKind.TranspilerExcluded,
            Status = RebirthPatchAdapterStatus.ExcludedByDefault,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.NeverInstall,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = false,
            Evidence = "P4",
            Notes = "A* internal patch excluded by default."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "excluded.global.entity.onupdateposition",
            ModuleId = "deployables.drones",
            TargetMethod = "Entity.OnUpdatePosition",
            AdapterKind = RebirthPatchAdapterKind.NoPatch,
            Status = RebirthPatchAdapterStatus.ExcludedByDefault,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.NeverInstall,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = false,
            Evidence = "P4/V8",
            Notes = "Drone follow-lock must not patch Entity.OnUpdatePosition globally."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "excluded.global.guiutils.drawline",
            ModuleId = "ui.crosshair",
            TargetMethod = "GUIUtils.DrawLine",
            AdapterKind = RebirthPatchAdapterKind.NoPatch,
            Status = RebirthPatchAdapterStatus.ExcludedByDefault,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.NeverInstall,
            IsHotPath = false,
            AllowsPatchInstallMeasurement = false,
            Evidence = "P4/V8",
            Notes = "Crosshair work must not patch GUIUtils.DrawLine globally."
        },
        new RebirthPatchAdapterDecl
        {
            AdapterId = "profiling.diagnostic.wrappers",
            ModuleId = "diagnostics.profiling",
            TargetMethod = "various hot methods",
            AdapterKind = RebirthPatchAdapterKind.BuildProfileOnlyPatch,
            Status = RebirthPatchAdapterStatus.ProfilingOnly,
            InstallPolicy = RebirthPatchAdapterInstallPolicy.ProfilingBuildOnly,
            IsHotPath = true,
            AllowsPatchInstallMeasurement = false,
            Evidence = "debug policy / prior profiler concern",
            Notes = "Diagnostic Harmony wrappers must never be always-patched in release."
        }
    };

    public static RebirthPatchAdapterDecl[] GetSnapshot()
    {
        RebirthPatchAdapterDecl[] copy = new RebirthPatchAdapterDecl[s_adapters.Length];
        Array.Copy(s_adapters, copy, copy.Length);
        return copy;
    }

    public static string GetReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthPatchAdapters] read-only patch adapter plan.");
        sb.AppendLine("  This does not install, uninstall, or enumerate live Harmony patches.");
        sb.AppendLine("adapter | module | target | kind | status | install policy | hot | patch-measure | evidence | notes");

        bool any = false;
        for (int i = 0; i < s_adapters.Length; i++)
        {
            RebirthPatchAdapterDecl a = s_adapters[i];
            if (!Matches(a, f))
                continue;

            any = true;
            sb.Append(a.AdapterId).Append(" | ")
              .Append(a.ModuleId).Append(" | ")
              .Append(a.TargetMethod).Append(" | ")
              .Append(a.AdapterKind).Append(" | ")
              .Append(a.Status).Append(" | ")
              .Append(a.InstallPolicy).Append(" | ")
              .Append(a.IsHotPath).Append(" | ")
              .Append(a.AllowsPatchInstallMeasurement).Append(" | ")
              .Append(a.Evidence).Append(" | ")
              .AppendLine(a.Notes);
        }

        if (!any)
            sb.AppendLine("No adapter matched the filter.");

        return sb.ToString();
    }

    public static string GetExcludedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthPatchAdapters] excluded patch targets.");
        sb.AppendLine("target | adapter | module | evidence | notes");

        for (int i = 0; i < s_adapters.Length; i++)
        {
            RebirthPatchAdapterDecl a = s_adapters[i];
            if (a.Status != RebirthPatchAdapterStatus.ExcludedByDefault)
                continue;

            sb.Append(a.TargetMethod).Append(" | ")
              .Append(a.AdapterId).Append(" | ")
              .Append(a.ModuleId).Append(" | ")
              .Append(a.Evidence).Append(" | ")
              .AppendLine(a.Notes);
        }

        return sb.ToString();
    }

    public static string GetMeasurementReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthPatchAdapters] patch-install measurement candidates.");
        sb.AppendLine("adapter | module | target | policy | notes");

        for (int i = 0; i < s_adapters.Length; i++)
        {
            RebirthPatchAdapterDecl a = s_adapters[i];
            if (!a.AllowsPatchInstallMeasurement)
                continue;

            sb.Append(a.AdapterId).Append(" | ")
              .Append(a.ModuleId).Append(" | ")
              .Append(a.TargetMethod).Append(" | ")
              .Append(a.InstallPolicy).Append(" | ")
              .AppendLine(a.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPatchAdapters] safety rules:");
        sb.AppendLine("  1. This registry is read-only.");
        sb.AppendLine("  2. No Harmony.PatchAll is called.");
        sb.AppendLine("  3. No Harmony.Patch or Harmony.Unpatch is called.");
        sb.AppendLine("  4. Future hot-method adapter ownership must be singular.");
        sb.AppendLine("  5. Runtime gates do not measure true patch-entry overhead.");
        sb.AppendLine("  6. Patch-install measurements require reload/restart/profiling policies.");
        sb.AppendLine("  7. A* internals, Entity.OnUpdatePosition, and GUIUtils.DrawLine are excluded by default.");
        sb.AppendLine("  8. Diagnostic wrappers are profiling-build only.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        int excluded = 0;
        int measurable = 0;
        int hot = 0;

        for (int i = 0; i < s_adapters.Length; i++)
        {
            if (s_adapters[i].Status == RebirthPatchAdapterStatus.ExcludedByDefault)
                excluded++;
            if (s_adapters[i].AllowsPatchInstallMeasurement)
                measurable++;
            if (s_adapters[i].IsHotPath)
                hot++;
        }

        return "[RebirthPatchAdapters] Adapters: " + s_adapters.Length
            + "; excluded: " + excluded
            + "; patch-install measurement candidates: " + measurable
            + "; hot-path targets: " + hot
            + ". Registry is read-only.";
    }

    private static bool Matches(RebirthPatchAdapterDecl a, string filter)
    {
        if (a == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (a.AdapterId != null && a.AdapterId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (a.ModuleId != null && a.ModuleId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (a.TargetMethod != null && a.TargetMethod.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
