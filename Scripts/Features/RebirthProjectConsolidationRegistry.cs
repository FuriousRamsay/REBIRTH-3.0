using System;
using System.Text;

#nullable disable

public enum RebirthProjectConsolidationArea
{
    Unknown,
    SingleProject,
    FolderOwnership,
    NamespaceOwnership,
    ExternalCompatibility,
    XmlOwnership,
    RuntimeBoundaries,
    DeprecatedSplit,
    ManualReview
}

public enum RebirthProjectConsolidationDecision
{
    Unknown,
    KeepSingleProject,
    CollapseIntoRebirth,
    KeepAsFeatureFolder,
    KeepAsCompatibilityFolder,
    DeprecateOldSplit,
    ManualReviewRequired,
    Blocked
}

public enum RebirthProjectConsolidationRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthProjectConsolidationDecl
{
    public string ConsolidationId;
    public RebirthProjectConsolidationArea Area;
    public RebirthProjectConsolidationDecision Decision;
    public RebirthProjectConsolidationRisk Risk;
    public string CurrentOrHistoricSurface;
    public string TargetShape;
    public string Rule;
    public string MustNotDo;
    public string Notes;
}

/// <summary>
/// Read-only project consolidation plan.
/// The target architecture is one REBIRTH project, not separate Core and Utils projects.
/// This does not move files or change build output.
/// </summary>
public static class RebirthProjectConsolidationRegistry
{
    private static readonly RebirthProjectConsolidationDecl[] s_rows = new[]
    {
        new RebirthProjectConsolidationDecl
        {
            ConsolidationId = "consolidation.single.project",
            Area = RebirthProjectConsolidationArea.SingleProject,
            Decision = RebirthProjectConsolidationDecision.KeepSingleProject,
            Risk = RebirthProjectConsolidationRisk.Critical,
            CurrentOrHistoricSurface = "historic split between zzz_REBIRTH__Utils and zzz_REBIRTH__Core/Core_2_0",
            TargetShape = "one fresh REBIRTH project/package",
            Rule = "Do not recreate separate Core and Utils projects as the target architecture.",
            MustNotDo = "Do not split runtime ownership across two new projects unless there is a future explicit technical requirement.",
            Notes = "User goal is collapse/merge, not preserving the old split."
        },
        new RebirthProjectConsolidationDecl
        {
            ConsolidationId = "consolidation.feature.folders",
            Area = RebirthProjectConsolidationArea.FolderOwnership,
            Decision = RebirthProjectConsolidationDecision.KeepAsFeatureFolder,
            Risk = RebirthProjectConsolidationRisk.High,
            CurrentOrHistoricSurface = "mixed utility/core/helper classes",
            TargetShape = "Scripts/Rebirth/<Domain>/<Feature> folders inside one project",
            Rule = "Separate by feature/domain folder, not by Core-vs-Utils project split.",
            MustNotDo = "Do not create generic dumping grounds named Utils for migrated feature behavior.",
            Notes = "Shared helpers must have clear owner/domain."
        },
        new RebirthProjectConsolidationDecl
        {
            ConsolidationId = "consolidation.runtime.boundaries",
            Area = RebirthProjectConsolidationArea.RuntimeBoundaries,
            Decision = RebirthProjectConsolidationDecision.CollapseIntoRebirth,
            Risk = RebirthProjectConsolidationRisk.High,
            CurrentOrHistoricSurface = "manager/helper code spread across project boundaries",
            TargetShape = "single runtime boundary model: feature owner, authority owner, persistence owner, patch owner",
            Rule = "A module boundary is logical ownership, not a separate project.",
            MustNotDo = "Do not use project split to hide unclear ownership.",
            Notes = "Registries already express ownership without requiring separate assemblies."
        },
        new RebirthProjectConsolidationDecl
        {
            ConsolidationId = "consolidation.compat.external",
            Area = RebirthProjectConsolidationArea.ExternalCompatibility,
            Decision = RebirthProjectConsolidationDecision.KeepAsCompatibilityFolder,
            Risk = RebirthProjectConsolidationRisk.High,
            CurrentOrHistoricSurface = "Izayo/Sonja/third-party compatibility XML/code",
            TargetShape = "Scripts/Rebirth/Compatibility or Config/_Compatibility inside same project/package",
            Rule = "External compatibility stays separated by folder and ownership metadata, not by Core/Utils project split.",
            MustNotDo = "Do not absorb vendored/external behavior into core feature logic by default.",
            Notes = "Compatibility can still be isolated without a separate project."
        },
        new RebirthProjectConsolidationDecl
        {
            ConsolidationId = "consolidation.xml",
            Area = RebirthProjectConsolidationArea.XmlOwnership,
            Decision = RebirthProjectConsolidationDecision.CollapseIntoRebirth,
            Risk = RebirthProjectConsolidationRisk.High,
            CurrentOrHistoricSurface = "Config files previously distributed across mod folders",
            TargetShape = "one Config root with root XML include files calling grouped subfolders",
            Rule = "XML may be grouped under Config subfolders, but root Config XML include entry files remain.",
            MustNotDo = "Do not create separate Core/Utils Config ownership as the target.",
            Notes = "Matches earlier XML include rule."
        },
        new RebirthProjectConsolidationDecl
        {
            ConsolidationId = "consolidation.namespaces",
            Area = RebirthProjectConsolidationArea.NamespaceOwnership,
            Decision = RebirthProjectConsolidationDecision.CollapseIntoRebirth,
            Risk = RebirthProjectConsolidationRisk.Medium,
            CurrentOrHistoricSurface = "global namespace and old utility naming",
            TargetShape = "future namespace plan can still use Rebirth.<Domain> in one assembly",
            Rule = "Namespace/folder organization should describe domain ownership, not old project split.",
            MustNotDo = "Do not preserve old naming solely because it came from Utils/Core.",
            Notes = "Current fresh code remains global namespace for compile compatibility; future namespace work should be deliberate."
        },
        new RebirthProjectConsolidationDecl
        {
            ConsolidationId = "consolidation.deprecated.old.split",
            Area = RebirthProjectConsolidationArea.DeprecatedSplit,
            Decision = RebirthProjectConsolidationDecision.DeprecateOldSplit,
            Risk = RebirthProjectConsolidationRisk.Medium,
            CurrentOrHistoricSurface = "old zzz_REBIRTH__Utils plus zzz_REBIRTH__Core split",
            TargetShape = "deprecated migration reference only",
            Rule = "Old split may be referenced for source audit/history but not treated as desired architecture.",
            MustNotDo = "Do not migrate by copying old folder/project boundaries unchanged.",
            Notes = "Feature ownership beats historical package location."
        },
        new RebirthProjectConsolidationDecl
        {
            ConsolidationId = "consolidation.manual.review",
            Area = RebirthProjectConsolidationArea.ManualReview,
            Decision = RebirthProjectConsolidationDecision.ManualReviewRequired,
            Risk = RebirthProjectConsolidationRisk.High,
            CurrentOrHistoricSurface = "anything with build/load-order dependency on separate mod folders",
            TargetShape = "review before collapsing into single package",
            Rule = "If something truly depends on separate mod-folder load order, document it as an exception.",
            MustNotDo = "Do not silently collapse load-order-sensitive behavior without tests.",
            Notes = "Exception process only; target remains one project/package."
        }
    };

    public static string GetSummaryReport()
    {
        int singleProject = 0;
        int manual = 0;
        int high = 0;

        for (int i = 0; i < s_rows.Length; i++)
        {
            RebirthProjectConsolidationDecl r = s_rows[i];

            if (r.Decision == RebirthProjectConsolidationDecision.KeepSingleProject
                || r.Decision == RebirthProjectConsolidationDecision.CollapseIntoRebirth)
                singleProject++;

            if (r.Decision == RebirthProjectConsolidationDecision.ManualReviewRequired)
                manual++;

            if (r.Risk == RebirthProjectConsolidationRisk.High || r.Risk == RebirthProjectConsolidationRisk.Critical)
                high++;
        }

        return "[RebirthProjectConsolidation] Rows: " + s_rows.Length
            + "; single/collapse decisions: " + singleProject
            + "; manual review: " + manual
            + "; high/critical: " + high
            + ". Plan is read-only.";
    }

    public static string GetPlanReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthProjectConsolidation] single-project consolidation plan.");
        sb.AppendLine("  Read-only. This does not move files or change build output.");
        sb.AppendLine("id | area | decision | risk | historic/current surface | target shape | rule | must not do | notes");

        bool any = false;
        for (int i = 0; i < s_rows.Length; i++)
        {
            RebirthProjectConsolidationDecl r = s_rows[i];
            if (!Matches(r, f))
                continue;

            any = true;
            sb.Append(r.ConsolidationId).Append(" | ")
              .Append(r.Area).Append(" | ")
              .Append(r.Decision).Append(" | ")
              .Append(r.Risk).Append(" | ")
              .Append(r.CurrentOrHistoricSurface).Append(" | ")
              .Append(r.TargetShape).Append(" | ")
              .Append(r.Rule).Append(" | ")
              .Append(r.MustNotDo).Append(" | ")
              .AppendLine(r.Notes);
        }

        if (!any)
            sb.AppendLine("No consolidation row matched the filter.");

        return sb.ToString();
    }

    public static string GetTargetShapeReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthProjectConsolidation] target shape:");
        sb.AppendLine("  zzz_REBIRTH__Fresh/");
        sb.AppendLine("    ModInfo.xml");
        sb.AppendLine("    RebirthUtils.csproj");
        sb.AppendLine("    Scripts/");
        sb.AppendLine("      Rebirth/");
        sb.AppendLine("        Commands/");
        sb.AppendLine("        Diagnostics/");
        sb.AppendLine("        Features/");
        sb.AppendLine("        Scenarios/");
        sb.AppendLine("        Toggles/");
        sb.AppendLine("        Compatibility/");
        sb.AppendLine("        <Domain folders>");
        sb.AppendLine("    Config/");
        sb.AppendLine("      items.xml, buffs.xml, entityclasses.xml, etc. root include entry files");
        sb.AppendLine("      _Common/");
        sb.AppendLine("      _Features/");
        sb.AppendLine("      _Scenarios/");
        sb.AppendLine("      _Compatibility/");
        sb.AppendLine("      _Cleanup/");
        sb.AppendLine();
        sb.AppendLine("Rule:");
        sb.AppendLine("  One project/package. Domain folders express ownership. Root Config XML files remain include entry points.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthProjectConsolidation] safety:");
        sb.AppendLine("  1. Plan is read-only.");
        sb.AppendLine("  2. No files moved.");
        sb.AppendLine("  3. No project files changed for build behavior.");
        sb.AppendLine("  4. No gameplay behavior connected.");
        sb.AppendLine("  5. No Harmony patches installed.");
        sb.AppendLine("  6. No XML parsing.");
        sb.AppendLine("  7. Old Core/Utils split is source-history context, not target architecture.");
        sb.AppendLine("  8. Load-order-sensitive exceptions require manual review.");
        return sb.ToString();
    }

    private static bool Matches(RebirthProjectConsolidationDecl r, string filter)
    {
        if (r == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (r.ConsolidationId != null && r.ConsolidationId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.CurrentOrHistoricSurface != null && r.CurrentOrHistoricSurface.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.TargetShape != null && r.TargetShape.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Rule != null && r.Rule.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.MustNotDo != null && r.MustNotDo.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Area.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Decision.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
