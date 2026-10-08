using System;
using System.Text;

#nullable disable

public enum RebirthFirstSliceGateDecision
{
    Unknown,
    Go,
    NoGo,
    ManualReviewRequired
}

public enum RebirthFirstSliceGateReasonKind
{
    Unknown,
    SourceEvidence,
    Scope,
    PatchTarget,
    Authority,
    Diagnostics,
    Tests,
    Rollback,
    ProjectConsolidation,
    Safety
}

public sealed class RebirthFirstSliceNoGoReason
{
    public string ReasonId;
    public RebirthFirstSliceGateReasonKind Kind;
    public bool BlocksGo;
    public string Reason;
    public string RequiredToClear;
    public string Notes;
}

/// <summary>
/// Read-only first slice no-go gate.
/// Current state is intentionally NO-GO because no actual audited source target has been recorded.
/// This does not inspect files, install Harmony, or migrate gameplay.
/// </summary>
public static class RebirthFirstSliceNoGoGate
{
    private static readonly RebirthFirstSliceNoGoReason[] s_reasons = new[]
    {
        new RebirthFirstSliceNoGoReason
        {
            ReasonId = "nogo.source.evidence.pending",
            Kind = RebirthFirstSliceGateReasonKind.SourceEvidence,
            BlocksGo = true,
            Reason = "The source evidence intake template is still pending actual audited source values.",
            RequiredToClear = "Fill FIRST_GAMEPLAY_SLICE_SOURCE_EVIDENCE_TEMPLATE.md from actual source audit.",
            Notes = "No guessed target is allowed."
        },
        new RebirthFirstSliceNoGoReason
        {
            ReasonId = "nogo.patch.target.missing",
            Kind = RebirthFirstSliceGateReasonKind.PatchTarget,
            BlocksGo = true,
            Reason = "No exact declaring type, method signature, or patch shape has been selected.",
            RequiredToClear = "Document exact target and typed Harmony parameter plan.",
            Notes = "Harmony patch must not be created before this."
        },
        new RebirthFirstSliceNoGoReason
        {
            ReasonId = "nogo.authority.unproven",
            Kind = RebirthFirstSliceGateReasonKind.Authority,
            BlocksGo = true,
            Reason = "Server/client authority for the future reward behavior has not been proven.",
            RequiredToClear = "Document server-authoritative ownership and dedicated-server test plan.",
            Notes = "Client-only reward truth is blocked."
        },
        new RebirthFirstSliceNoGoReason
        {
            ReasonId = "nogo.scope.not.selected",
            Kind = RebirthFirstSliceGateReasonKind.Scope,
            BlocksGo = true,
            Reason = "The exact one behavior for the first slice has not been named.",
            RequiredToClear = "Name one isolated behavior and explicitly list non-goals.",
            Notes = "Combined harvest/salvage/sound/vehicle/XP/UI remains blocked."
        },
        new RebirthFirstSliceNoGoReason
        {
            ReasonId = "nogo.phase3h.tests.not.mapped",
            Kind = RebirthFirstSliceGateReasonKind.Tests,
            BlocksGo = true,
            Reason = "Phase 3H test IDs have not been mapped to the chosen behavior.",
            RequiredToClear = "Map relevant Phase 3H test IDs in the package summary.",
            Notes = "Tests before behavior."
        },
        new RebirthFirstSliceNoGoReason
        {
            ReasonId = "nogo.rollback.not.named",
            Kind = RebirthFirstSliceGateReasonKind.Rollback,
            BlocksGo = true,
            Reason = "Rollback path has not been named.",
            RequiredToClear = "Name a single flag and/or one patch adapter file as rollback.",
            Notes = "Required for quick user testing."
        },
        new RebirthFirstSliceNoGoReason
        {
            ReasonId = "nogo.diagnostics.review.pending",
            Kind = RebirthFirstSliceGateReasonKind.Diagnostics,
            BlocksGo = true,
            Reason = "Diagnostic construction review has not been performed for any future patch.",
            RequiredToClear = "Confirm no interpolation/formatting/LINQ/reflection/helper calls before diagnostic gates.",
            Notes = "Debug overhead rule remains strict."
        },
        new RebirthFirstSliceNoGoReason
        {
            ReasonId = "nogo.consolidation.must.remain.single",
            Kind = RebirthFirstSliceGateReasonKind.ProjectConsolidation,
            BlocksGo = false,
            Reason = "Single-project consolidation target is established.",
            RequiredToClear = "Keep one project/package; do not reintroduce Core/Utils split.",
            Notes = "Not blocking because current package already follows this."
        }
    };

    public static RebirthFirstSliceGateDecision CurrentDecision
    {
        get
        {
            for (int i = 0; i < s_reasons.Length; i++)
            {
                if (s_reasons[i].BlocksGo)
                    return RebirthFirstSliceGateDecision.NoGo;
            }

            return RebirthFirstSliceGateDecision.Go;
        }
    }

    public static string GetSummaryReport()
    {
        int blockers = 0;
        int nonBlockers = 0;

        for (int i = 0; i < s_reasons.Length; i++)
        {
            if (s_reasons[i].BlocksGo)
                blockers++;
            else
                nonBlockers++;
        }

        return "[RebirthFirstSliceNoGoGate] Decision: " + CurrentDecision
            + "; blockers: " + blockers
            + "; non-blocking notes: " + nonBlockers
            + ". Gate is read-only.";
    }

    public static string GetReasonsReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFirstSliceNoGoGate] first gameplay slice no-go gate.");
        sb.AppendLine("  Current expected decision is NO-GO until actual source evidence is filled.");
        sb.AppendLine("id | kind | blocks go | reason | required to clear | notes");

        bool any = false;
        for (int i = 0; i < s_reasons.Length; i++)
        {
            RebirthFirstSliceNoGoReason r = s_reasons[i];
            if (!Matches(r, f))
                continue;

            any = true;
            sb.Append(r.ReasonId).Append(" | ")
              .Append(r.Kind).Append(" | ")
              .Append(r.BlocksGo).Append(" | ")
              .Append(r.Reason).Append(" | ")
              .Append(r.RequiredToClear).Append(" | ")
              .AppendLine(r.Notes);
        }

        if (!any)
            sb.AppendLine("No no-go reason matched the filter.");

        return sb.ToString();
    }

    public static string GetBlockersReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthFirstSliceNoGoGate] blocking reasons:");

        bool any = false;
        for (int i = 0; i < s_reasons.Length; i++)
        {
            RebirthFirstSliceNoGoReason r = s_reasons[i];
            if (!r.BlocksGo)
                continue;

            any = true;
            sb.Append("  - ").Append(r.ReasonId).Append(": ").Append(r.RequiredToClear).AppendLine();
        }

        if (!any)
            sb.AppendLine("  none");

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthFirstSliceNoGoGate] safety:");
        sb.AppendLine("  1. Gate is read-only.");
        sb.AppendLine("  2. Current decision is intentionally NoGo.");
        sb.AppendLine("  3. No source files inspected.");
        sb.AppendLine("  4. No method target chosen.");
        sb.AppendLine("  5. No Harmony patches installed.");
        sb.AppendLine("  6. No gameplay behavior migrated.");
        sb.AppendLine("  7. No XML parsing.");
        sb.AppendLine("  8. No custom game option lookup.");
        sb.AppendLine("  9. No network packets.");
        sb.AppendLine("  10. No save/load writes.");
        return sb.ToString();
    }

    private static bool Matches(RebirthFirstSliceNoGoReason r, string filter)
    {
        if (r == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (r.ReasonId != null && r.ReasonId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Reason != null && r.Reason.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.RequiredToClear != null && r.RequiredToClear.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Kind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
