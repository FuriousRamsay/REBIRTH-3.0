using System;
using System.Text;

#nullable disable

public enum RebirthXmlCleanupArea
{
    Unknown,
    RootConfigInclude,
    Common,
    Scenario,
    Feature,
    Compatibility,
    Cleanup,
    Deprecated,
    GeneratedFuture,
    ManualReview
}

public enum RebirthXmlCleanupAction
{
    Unknown,
    KeepInRoot,
    RootIncludeOnly,
    MoveToSubfolderLater,
    SplitByOwnerLater,
    ReconcileInCleanup,
    DeprecateLater,
    ManualReviewRequired,
    MustNotMoveYet
}

public enum RebirthXmlCleanupRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthXmlCleanupPlanDecl
{
    public string PlanId;
    public string XmlFileOrGroup;
    public RebirthXmlCleanupArea Area;
    public RebirthXmlCleanupAction Action;
    public RebirthXmlCleanupRisk Risk;
    public string ProposedRootEntry;
    public string ProposedSubfolder;
    public string Owner;
    public string IncludeRule;
    public string Notes;
}

/// <summary>
/// Read-only XML cleanup/reorganization plan.
/// This does not move XML, parse XML, or generate XML.
/// It defines the future convention that root Config XML files remain include entry points.
/// </summary>
public static class RebirthXmlCleanupPlanRegistry
{
    private static readonly RebirthXmlCleanupPlanDecl[] s_plans = new[]
    {
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.root.include.rule",
            XmlFileOrGroup = "Config/*.xml root files",
            Area = RebirthXmlCleanupArea.RootConfigInclude,
            Action = RebirthXmlCleanupAction.RootIncludeOnly,
            Risk = RebirthXmlCleanupRisk.Critical,
            ProposedRootEntry = "Config/<vanilla-file>.xml",
            ProposedSubfolder = "Config/_Common, Config/_Features, Config/_Scenarios, Config/_Compatibility, Config/_Cleanup",
            Owner = "xml.cleanup.plan",
            IncludeRule = "Root Config XML files stay in Config and issue include calls to grouped subfolder files.",
            Notes = "Subfolder split must not remove the root Config entry files the game/mod loader expects."
        },
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.common.shared",
            XmlFileOrGroup = "common shared XML",
            Area = RebirthXmlCleanupArea.Common,
            Action = RebirthXmlCleanupAction.SplitByOwnerLater,
            Risk = RebirthXmlCleanupRisk.High,
            ProposedRootEntry = "Config/items.xml, Config/buffs.xml, Config/entityclasses.xml, etc.",
            ProposedSubfolder = "Config/_Common/<xml-family>/",
            Owner = "common XML owners",
            IncludeRule = "Root file includes common fragments before feature/scenario-specific fragments when ordering requires it.",
            Notes = "Common is shared infrastructure, not a dumping ground."
        },
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.features",
            XmlFileOrGroup = "feature-owned XML",
            Area = RebirthXmlCleanupArea.Feature,
            Action = RebirthXmlCleanupAction.SplitByOwnerLater,
            Risk = RebirthXmlCleanupRisk.High,
            ProposedRootEntry = "Config/<vanilla-file>.xml",
            ProposedSubfolder = "Config/_Features/<FeatureName>/",
            Owner = "feature domain owners",
            IncludeRule = "Root XML file includes feature fragments that target the same vanilla XML family.",
            Notes = "Feature ownership must be declared before moving XML physically."
        },
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.scenarios",
            XmlFileOrGroup = "scenario-owned XML",
            Area = RebirthXmlCleanupArea.Scenario,
            Action = RebirthXmlCleanupAction.SplitByOwnerLater,
            Risk = RebirthXmlCleanupRisk.High,
            ProposedRootEntry = "Config/<vanilla-file>.xml",
            ProposedSubfolder = "Config/_Scenarios/Purge, Config/_Scenarios/Hive, Config/_Scenarios/Survivor",
            Owner = "scenario XML owners",
            IncludeRule = "Root XML file includes scenario fragments only after common prerequisites and before final cleanup.",
            Notes = "Survivor scenario XML remains blocked until survivor NPC/entity-role disambiguation."
        },
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.compatibility",
            XmlFileOrGroup = "external compatibility XML",
            Area = RebirthXmlCleanupArea.Compatibility,
            Action = RebirthXmlCleanupAction.SplitByOwnerLater,
            Risk = RebirthXmlCleanupRisk.High,
            ProposedRootEntry = "Config/<vanilla-file>.xml",
            ProposedSubfolder = "Config/_Compatibility/<ExternalProject>/",
            Owner = "external.compat",
            IncludeRule = "Root XML file includes compatibility fragments in a documented order after core ownership layers.",
            Notes = "Compatibility XML must remain separate from core feature/scenario ownership."
        },
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.cleanup.final",
            XmlFileOrGroup = "final cleanup/reconciliation XML",
            Area = RebirthXmlCleanupArea.Cleanup,
            Action = RebirthXmlCleanupAction.ReconcileInCleanup,
            Risk = RebirthXmlCleanupRisk.High,
            ProposedRootEntry = "Config/<vanilla-file>.xml",
            ProposedSubfolder = "Config/_Cleanup/",
            Owner = "xml.cleanup",
            IncludeRule = "Root XML file includes cleanup fragments last when ordering requires final reconciliation.",
            Notes = "Cleanup reconciles ownership but must not hide ownership."
        },
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.deprecated",
            XmlFileOrGroup = "deprecated/obsolete XML",
            Area = RebirthXmlCleanupArea.Deprecated,
            Action = RebirthXmlCleanupAction.ManualReviewRequired,
            Risk = RebirthXmlCleanupRisk.Medium,
            ProposedRootEntry = "none until reviewed",
            ProposedSubfolder = "Config/_Deprecated/",
            Owner = "migration.drift",
            IncludeRule = "Deprecated XML should not be included unless a compatibility bridge explicitly requires it.",
            Notes = "Do not delete until feature tests confirm no current dependency."
        },
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.generated.future",
            XmlFileOrGroup = "future generated XML manifests",
            Area = RebirthXmlCleanupArea.GeneratedFuture,
            Action = RebirthXmlCleanupAction.ManualReviewRequired,
            Risk = RebirthXmlCleanupRisk.Medium,
            ProposedRootEntry = "Config/<vanilla-file>.xml",
            ProposedSubfolder = "Config/_Generated/",
            Owner = "build.xml.future",
            IncludeRule = "Generated XML, if ever used, still flows through root Config include entry files.",
            Notes = "No generated XML exists in this phase."
        },
        new RebirthXmlCleanupPlanDecl
        {
            PlanId = "xml.must.not.move.hot",
            XmlFileOrGroup = "XML depended on by hot-path runtime code",
            Area = RebirthXmlCleanupArea.ManualReview,
            Action = RebirthXmlCleanupAction.MustNotMoveYet,
            Risk = RebirthXmlCleanupRisk.Critical,
            ProposedRootEntry = "current Config file until cached policy exists",
            ProposedSubfolder = "none yet",
            Owner = "performance + feature owners",
            IncludeRule = "Do not reorganize until runtime code no longer performs XML/option/string lookups in hot paths.",
            Notes = "XML cleanup must not create hidden runtime lookup costs."
        }
    };

    public static string GetSummaryReport()
    {
        int critical = 0;
        int rootInclude = 0;
        int manual = 0;
        int splitLater = 0;

        for (int i = 0; i < s_plans.Length; i++)
        {
            RebirthXmlCleanupPlanDecl p = s_plans[i];

            if (p.Risk == RebirthXmlCleanupRisk.Critical)
                critical++;
            if (p.Area == RebirthXmlCleanupArea.RootConfigInclude)
                rootInclude++;
            if (p.Action == RebirthXmlCleanupAction.ManualReviewRequired || p.Action == RebirthXmlCleanupAction.MustNotMoveYet)
                manual++;
            if (p.Action == RebirthXmlCleanupAction.SplitByOwnerLater)
                splitLater++;
        }

        return "[RebirthXmlCleanupPlan] Entries: " + s_plans.Length
            + "; root include rules: " + rootInclude
            + "; split later: " + splitLater
            + "; manual/must-not-move: " + manual
            + "; critical: " + critical
            + ". Plan is read-only.";
    }

    public static string GetPlanReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthXmlCleanupPlan] XML cleanup/reorganization plan.");
        sb.AppendLine("  Read-only. Root Config XML files remain include entry points.");
        sb.AppendLine("id | group | area | action | risk | root entry | subfolder | owner | include rule | notes");

        bool any = false;
        for (int i = 0; i < s_plans.Length; i++)
        {
            RebirthXmlCleanupPlanDecl p = s_plans[i];
            if (!Matches(p, f))
                continue;

            any = true;
            sb.Append(p.PlanId).Append(" | ")
              .Append(p.XmlFileOrGroup).Append(" | ")
              .Append(p.Area).Append(" | ")
              .Append(p.Action).Append(" | ")
              .Append(p.Risk).Append(" | ")
              .Append(p.ProposedRootEntry).Append(" | ")
              .Append(p.ProposedSubfolder).Append(" | ")
              .Append(p.Owner).Append(" | ")
              .Append(p.IncludeRule).Append(" | ")
              .AppendLine(p.Notes);
        }

        if (!any)
            sb.AppendLine("No XML cleanup plan entry matched the filter.");

        return sb.ToString();
    }

    public static string GetRootIncludeReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthXmlCleanupPlan] root Config include convention:");
        sb.AppendLine("  - Keep vanilla-family entry files under Config/.");
        sb.AppendLine("  - Those root files issue include calls to grouped subfolder fragments.");
        sb.AppendLine("  - Subfolders may organize by _Common, _Features, _Scenarios, _Compatibility, _Cleanup.");
        sb.AppendLine("  - Final cleanup fragments load last when required.");
        sb.AppendLine("  - Physical reorganization is future work, not done in this phase.");
        sb.AppendLine();
        sb.AppendLine("Conceptual shape:");
        sb.AppendLine("  Config/items.xml");
        sb.AppendLine("    -> include Config/_Common/Items/*.xml");
        sb.AppendLine("    -> include Config/_Features/Armor/items.xml");
        sb.AppendLine("    -> include Config/_Features/Vehicles/items.xml");
        sb.AppendLine("    -> include Config/_Scenarios/Purge/items.xml");
        sb.AppendLine("    -> include Config/_Compatibility/<External>/items.xml");
        sb.AppendLine("    -> include Config/_Cleanup/items.xml");
        sb.AppendLine();
        sb.AppendLine("Rule: do not remove the root Config XML entry files.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthXmlCleanupPlan] safety rules:");
        sb.AppendLine("  1. Plan is read-only.");
        sb.AppendLine("  2. No XML files are moved.");
        sb.AppendLine("  3. No XML files are generated.");
        sb.AppendLine("  4. No XML files are parsed at runtime.");
        sb.AppendLine("  5. Root Config XML files remain include entry points.");
        sb.AppendLine("  6. Cleanup reconciles ownership but must not hide ownership.");
        sb.AppendLine("  7. Survivor XML cannot be reorganized by keyword until disambiguated.");
        sb.AppendLine("  8. XML depended on by hot paths must not move until cached policy exists.");
        return sb.ToString();
    }

    private static bool Matches(RebirthXmlCleanupPlanDecl p, string filter)
    {
        if (p == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (p.PlanId != null && p.PlanId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (p.XmlFileOrGroup != null && p.XmlFileOrGroup.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (p.ProposedRootEntry != null && p.ProposedRootEntry.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (p.ProposedSubfolder != null && p.ProposedSubfolder.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (p.Owner != null && p.Owner.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (p.IncludeRule != null && p.IncludeRule.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (p.Area.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (p.Action.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (p.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
