using System;
using System.Text;

#nullable disable

public enum RebirthMilestonePlanArea
{
    Unknown,
    Packaging,
    Handoff,
    SourceAudit,
    FirstSlice,
    Testing,
    Consolidation,
    Safety,
    FutureWork
}

public enum RebirthMilestonePlanDecision
{
    Unknown,
    Adopted,
    Required,
    Recommended,
    BlockedUntilEvidence,
    ManualReview
}

public sealed class RebirthMilestonePlanItem
{
    public string MilestoneId;
    public RebirthMilestonePlanArea Area;
    public RebirthMilestonePlanDecision Decision;
    public string Plan;
    public string Why;
    public string NextAction;
}

/// <summary>
/// Read-only milestone plan that replaces tiny phase increments with larger bundled milestones.
/// </summary>
public static class RebirthMilestonePlanRegistry
{
    private static readonly RebirthMilestonePlanItem[] s_items = new[]
    {
        Row("milestone.cadence.larger", RebirthMilestonePlanArea.Packaging, RebirthMilestonePlanDecision.Adopted,
            "Use larger bundled milestone packages instead of many tiny increments.",
            "User requested fewer requests and larger chunks.",
            "Next packages should bundle related evidence, contracts, tests, and implementation stubs where safe."),

        Row("milestone.handoff.required", RebirthMilestonePlanArea.Handoff, RebirthMilestonePlanDecision.Required,
            "Update _Documentation/HANDOFF_CURRENT_STATE.md in every package.",
            "Allows another conversation to resume cleanly.",
            "Keep latest package, compile status, blockers, commands, and next steps current."),

        Row("milestone.single.project", RebirthMilestonePlanArea.Consolidation, RebirthMilestonePlanDecision.Adopted,
            "Target one REBIRTH project/package, not separate Core and Utils projects.",
            "User explicitly said the goal is collapsing and merging.",
            "Keep domain ownership by folder/metadata inside the single project."),

        Row("milestone.source.audit.required", RebirthMilestonePlanArea.SourceAudit, RebirthMilestonePlanDecision.BlockedUntilEvidence,
            "Do not add a real gameplay patch until exact source evidence is filled.",
            "Prevents guessed Harmony targets.",
            "Inspect actual source and fill FIRST_GAMEPLAY_SLICE_SOURCE_EVIDENCE_TEMPLATE.md before patching."),

        Row("milestone.first.slice.one.behavior", RebirthMilestonePlanArea.FirstSlice, RebirthMilestonePlanDecision.Required,
            "First gameplay slice must be exactly one behavior.",
            "Avoids broad regression-prone patches.",
            "Name the single behavior, non-goals, source target, tests, and rollback path."),

        Row("milestone.tests.before.behavior", RebirthMilestonePlanArea.Testing, RebirthMilestonePlanDecision.Required,
            "Map Phase 3H tests before enabling behavior.",
            "Keeps changes testable in SP/dedi.",
            "Package summary must list relevant test IDs."),

        Row("milestone.disabled.default", RebirthMilestonePlanArea.Safety, RebirthMilestonePlanDecision.Required,
            "New gameplay behavior remains disabled by default until tested.",
            "Protects compile/load baseline.",
            "Use direct bool/policy guards and keep no-go gate active until evidence is complete."),

        Row("milestone.next.4a", RebirthMilestonePlanArea.FutureWork, RebirthMilestonePlanDecision.Recommended,
            "Next larger package should be Milestone 4A: Source Audit + First Real Slice Preparation Bundle.",
            "The architecture is ready to move beyond tiny ledgers, but not to guessed patching.",
            "If source is available, audit it and fill evidence. If not, stop before gameplay patching.")
    };

    public static string GetSummaryReport()
    {
        int required = 0;
        int adopted = 0;
        int blocked = 0;

        for (int i = 0; i < s_items.Length; i++)
        {
            if (s_items[i].Decision == RebirthMilestonePlanDecision.Required)
                required++;

            if (s_items[i].Decision == RebirthMilestonePlanDecision.Adopted)
                adopted++;

            if (s_items[i].Decision == RebirthMilestonePlanDecision.BlockedUntilEvidence)
                blocked++;
        }

        return "[RebirthMilestonePlan] Items: " + s_items.Length
            + "; adopted: " + adopted
            + "; required: " + required
            + "; blocked-until-evidence: " + blocked
            + ". Larger milestone cadence is adopted.";
    }

    public static string GetPlanReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(12000);
        sb.AppendLine("[RebirthMilestonePlan] consolidated milestone plan.");
        sb.AppendLine("id | area | decision | plan | why | next action");

        bool any = false;
        for (int i = 0; i < s_items.Length; i++)
        {
            RebirthMilestonePlanItem item = s_items[i];
            if (!Matches(item, f))
                continue;

            any = true;
            sb.Append(item.MilestoneId).Append(" | ")
              .Append(item.Area).Append(" | ")
              .Append(item.Decision).Append(" | ")
              .Append(item.Plan).Append(" | ")
              .Append(item.Why).Append(" | ")
              .AppendLine(item.NextAction);
        }

        if (!any)
            sb.AppendLine("No milestone plan row matched the filter.");

        return sb.ToString();
    }

    public static string GetNextReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthMilestonePlan] next recommended larger package:");
        sb.AppendLine("  Milestone 4A — Source Audit + First Real Slice Preparation Bundle");
        sb.AppendLine();
        sb.AppendLine("Bundle together:");
        sb.AppendLine("  1. Update handoff document.");
        sb.AppendLine("  2. Inspect actual source for the harvest/salvage target.");
        sb.AppendLine("  3. Fill source evidence template.");
        sb.AppendLine("  4. Choose one exact behavior only if evidence supports it.");
        sb.AppendLine("  5. Add target-specific patch contract.");
        sb.AppendLine("  6. Keep behavior disabled by default.");
        sb.AppendLine("  7. Add exact manual test checklist.");
        sb.AppendLine();
        sb.AppendLine("If exact source evidence is not available, do not add a gameplay patch.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthMilestonePlan] safety:");
        sb.AppendLine("  1. Plan is read-only.");
        sb.AppendLine("  2. No gameplay behavior migrated.");
        sb.AppendLine("  3. No Harmony patches installed.");
        sb.AppendLine("  4. No XML parsing.");
        sb.AppendLine("  5. No custom game option lookup.");
        sb.AppendLine("  6. Handoff document is now required for future packages.");
        return sb.ToString();
    }

    private static RebirthMilestonePlanItem Row(
        string id,
        RebirthMilestonePlanArea area,
        RebirthMilestonePlanDecision decision,
        string plan,
        string why,
        string nextAction)
    {
        return new RebirthMilestonePlanItem
        {
            MilestoneId = id,
            Area = area,
            Decision = decision,
            Plan = plan,
            Why = why,
            NextAction = nextAction
        };
    }

    private static bool Matches(RebirthMilestonePlanItem item, string filter)
    {
        if (item == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (item.MilestoneId != null && item.MilestoneId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.Plan != null && item.Plan.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.Why != null && item.Why.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.NextAction != null && item.NextAction.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.Area.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (item.Decision.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
