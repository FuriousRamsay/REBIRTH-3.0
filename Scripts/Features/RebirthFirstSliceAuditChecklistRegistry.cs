using System;
using System.Text;

#nullable disable

public enum RebirthFirstSliceAuditChecklistArea
{
    Unknown,
    SourceEvidence,
    Scope,
    PatchTarget,
    Authority,
    Diagnostics,
    Performance,
    Tests,
    Rollback,
    Packaging
}

public enum RebirthFirstSliceAuditChecklistStatus
{
    Unknown,
    Required,
    Recommended,
    BlockedIfMissing,
    ManualReview
}

public enum RebirthFirstSliceAuditChecklistRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthFirstSliceAuditChecklistItem
{
    public string ChecklistId;
    public RebirthFirstSliceAuditChecklistArea Area;
    public RebirthFirstSliceAuditChecklistStatus Status;
    public RebirthFirstSliceAuditChecklistRisk Risk;
    public string Check;
    public string PassCondition;
    public string FailCondition;
    public string Notes;
}

/// <summary>
/// Read-only audit checklist for the first real gameplay slice.
/// This does not inspect source, choose a target, install Harmony, or migrate gameplay.
/// </summary>
public static class RebirthFirstSliceAuditChecklistRegistry
{
    private static readonly RebirthFirstSliceAuditChecklistItem[] s_items = new[]
    {
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.source.template.filled",
            Area = RebirthFirstSliceAuditChecklistArea.SourceEvidence,
            Status = RebirthFirstSliceAuditChecklistStatus.BlockedIfMissing,
            Risk = RebirthFirstSliceAuditChecklistRisk.Critical,
            Check = "FIRST_GAMEPLAY_SLICE_SOURCE_EVIDENCE_TEMPLATE.md has been filled from actual audited source.",
            PassCondition = "All required source package, file, type, signature, context, authority, side effects, patch shape, tests, and rollback fields are filled.",
            FailCondition = "Any required field is pending, guessed, or copied from memory.",
            Notes = "Blocks first patch if incomplete."
        },
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.scope.one.behavior",
            Area = RebirthFirstSliceAuditChecklistArea.Scope,
            Status = RebirthFirstSliceAuditChecklistStatus.BlockedIfMissing,
            Risk = RebirthFirstSliceAuditChecklistRisk.Critical,
            Check = "Package summary names exactly one gameplay behavior.",
            PassCondition = "Only one isolated behavior is implemented.",
            FailCondition = "Multiple behaviors are bundled together.",
            Notes = "No combined harvest/salvage/sound/vehicle/XP/UI patch."
        },
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.patch.target.exact",
            Area = RebirthFirstSliceAuditChecklistArea.PatchTarget,
            Status = RebirthFirstSliceAuditChecklistStatus.BlockedIfMissing,
            Risk = RebirthFirstSliceAuditChecklistRisk.Critical,
            Check = "Exact patch target is documented.",
            PassCondition = "Declaring type, method, signature, patch type, and typed parameter plan are documented.",
            FailCondition = "Patch target is inferred or unspecified.",
            Notes = "Required before any Harmony patch file."
        },
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.authority.server.truth",
            Area = RebirthFirstSliceAuditChecklistArea.Authority,
            Status = RebirthFirstSliceAuditChecklistStatus.BlockedIfMissing,
            Risk = RebirthFirstSliceAuditChecklistRisk.Critical,
            Check = "Reward truth ownership is server-authoritative.",
            PassCondition = "Server owns item/XP/reward truth; client only mirrors or displays.",
            FailCondition = "Client computes authoritative rewards.",
            Notes = "Required for harvest/salvage outputs."
        },
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.diagnostics.gated",
            Area = RebirthFirstSliceAuditChecklistArea.Diagnostics,
            Status = RebirthFirstSliceAuditChecklistStatus.BlockedIfMissing,
            Risk = RebirthFirstSliceAuditChecklistRisk.High,
            Check = "All diagnostics are gated before construction.",
            PassCondition = "No string interpolation/formatting/LINQ/reflection/helper call/stack trace before a diagnostic gate.",
            FailCondition = "Disabled diagnostics still build messages or evaluate expensive arguments.",
            Notes = "No lazy lambdas that capture ref/out/in."
        },
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.performance.disabled.fast",
            Area = RebirthFirstSliceAuditChecklistArea.Performance,
            Status = RebirthFirstSliceAuditChecklistStatus.BlockedIfMissing,
            Risk = RebirthFirstSliceAuditChecklistRisk.High,
            Check = "Disabled feature overhead is near-zero.",
            PassCondition = "Direct bool/policy guard returns before allocations, logging, reflection, scans, or lookups.",
            FailCondition = "Disabled feature still performs work.",
            Notes = "Important if target is hot or warm."
        },
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.tests.phase3h.mapped",
            Area = RebirthFirstSliceAuditChecklistArea.Tests,
            Status = RebirthFirstSliceAuditChecklistStatus.BlockedIfMissing,
            Risk = RebirthFirstSliceAuditChecklistRisk.High,
            Check = "Phase 3H test IDs are mapped to the behavior.",
            PassCondition = "Package summary lists the relevant test gate IDs.",
            FailCondition = "No test mapping or tests do not cover the behavior.",
            Notes = "Tests before behavior."
        },
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.rollback.simple",
            Area = RebirthFirstSliceAuditChecklistArea.Rollback,
            Status = RebirthFirstSliceAuditChecklistStatus.Required,
            Risk = RebirthFirstSliceAuditChecklistRisk.Medium,
            Check = "Rollback is simple.",
            PassCondition = "Disable one flag or remove one patch adapter file.",
            FailCondition = "Rollback requires undoing broad code movement or multiple systems.",
            Notes = "Required for fast user testing."
        },
        new RebirthFirstSliceAuditChecklistItem
        {
            ChecklistId = "audit.packaging.single.project",
            Area = RebirthFirstSliceAuditChecklistArea.Packaging,
            Status = RebirthFirstSliceAuditChecklistStatus.Required,
            Risk = RebirthFirstSliceAuditChecklistRisk.Medium,
            Check = "Package remains single-project consolidated target.",
            PassCondition = "No Core/Utils split reintroduced; docs remain under _Documentation.",
            FailCondition = "Separate Core/Utils project/folder target returns.",
            Notes = "Matches Phase 3J consolidation rule."
        }
    };

    public static string GetSummaryReport()
    {
        int blockers = 0;
        int critical = 0;

        for (int i = 0; i < s_items.Length; i++)
        {
            RebirthFirstSliceAuditChecklistItem item = s_items[i];

            if (item.Status == RebirthFirstSliceAuditChecklistStatus.BlockedIfMissing)
                blockers++;

            if (item.Risk == RebirthFirstSliceAuditChecklistRisk.Critical)
                critical++;
        }

        return "[RebirthFirstSliceAuditChecklist] Items: " + s_items.Length
            + "; blocked-if-missing: " + blockers
            + "; critical: " + critical
            + ". Checklist is read-only.";
    }

    public static string GetChecklistReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFirstSliceAuditChecklist] first gameplay slice audit checklist.");
        sb.AppendLine("  Read-only. This does not inspect source, patch, or migrate gameplay.");
        sb.AppendLine("id | area | status | risk | check | pass | fail | notes");

        bool any = false;
        for (int i = 0; i < s_items.Length; i++)
        {
            RebirthFirstSliceAuditChecklistItem item = s_items[i];
            if (!Matches(item, f))
                continue;

            any = true;
            sb.Append(item.ChecklistId).Append(" | ")
              .Append(item.Area).Append(" | ")
              .Append(item.Status).Append(" | ")
              .Append(item.Risk).Append(" | ")
              .Append(item.Check).Append(" | ")
              .Append(item.PassCondition).Append(" | ")
              .Append(item.FailCondition).Append(" | ")
              .AppendLine(item.Notes);
        }

        if (!any)
            sb.AppendLine("No checklist item matched the filter.");

        return sb.ToString();
    }

    public static string GetBlockersReport()
    {
        return GetChecklistReport("BlockedIfMissing");
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthFirstSliceAuditChecklist] safety:");
        sb.AppendLine("  1. Checklist is read-only.");
        sb.AppendLine("  2. No source files inspected.");
        sb.AppendLine("  3. No method target chosen.");
        sb.AppendLine("  4. No Harmony patches installed.");
        sb.AppendLine("  5. No gameplay behavior migrated.");
        sb.AppendLine("  6. No XML parsing.");
        sb.AppendLine("  7. No custom game option lookup.");
        sb.AppendLine("  8. No network packets.");
        sb.AppendLine("  9. No save/load writes.");
        return sb.ToString();
    }

    private static bool Matches(RebirthFirstSliceAuditChecklistItem item, string filter)
    {
        if (item == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (item.ChecklistId != null && item.ChecklistId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.Check != null && item.Check.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.PassCondition != null && item.PassCondition.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.FailCondition != null && item.FailCondition.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.Area.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.Status.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
