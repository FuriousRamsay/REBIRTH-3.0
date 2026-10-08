using System;
using System.Text;

#nullable disable

public enum RebirthPatchTargetEvidenceArea
{
    Unknown,
    SourceLocation,
    MethodSignature,
    PatchType,
    ParameterPlan,
    Authority,
    SideEffects,
    Performance,
    Rollback,
    Blocked
}

public enum RebirthPatchTargetEvidenceStatus
{
    Unknown,
    Missing,
    Required,
    Candidate,
    Accepted,
    Blocked,
    ManualReview
}

public enum RebirthPatchTargetEvidenceRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthPatchTargetEvidenceRequirement
{
    public string EvidenceId;
    public RebirthPatchTargetEvidenceArea Area;
    public RebirthPatchTargetEvidenceStatus Status;
    public RebirthPatchTargetEvidenceRisk Risk;
    public string RequiredEvidence;
    public string AcceptedExample;
    public string RejectedExample;
    public string Verification;
    public string Notes;
}

/// <summary>
/// Read-only patch target evidence ledger.
/// This does not choose a method target, install Harmony, or migrate gameplay.
/// It defines what evidence must exist before the first real patch.
/// </summary>
public static class RebirthPatchTargetEvidenceLedger
{
    private static readonly RebirthPatchTargetEvidenceRequirement[] s_rows = new[]
    {
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.source.file",
            Area = RebirthPatchTargetEvidenceArea.SourceLocation,
            Status = RebirthPatchTargetEvidenceStatus.Required,
            Risk = RebirthPatchTargetEvidenceRisk.Critical,
            RequiredEvidence = "Exact source file path from the current base/source package.",
            AcceptedExample = "Assembly-CSharp/BaseClass.cs or Scripts/<owner>/<file>.cs with line/context.",
            RejectedExample = "I remember this method exists.",
            Verification = "Patch summary cites file path and method context.",
            Notes = "No blind patching."
        },
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.method.signature",
            Area = RebirthPatchTargetEvidenceArea.MethodSignature,
            Status = RebirthPatchTargetEvidenceStatus.Required,
            Risk = RebirthPatchTargetEvidenceRisk.Critical,
            RequiredEvidence = "Exact declaring type, method name, return type, and parameter list.",
            AcceptedExample = "DeclaringType.MethodName(TypeA a, TypeB b) : ReturnType.",
            RejectedExample = "Patch a harvest function somewhere.",
            Verification = "Harmony target can be written from the cited signature without guessing.",
            Notes = "Required before creating patch file."
        },
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.patch.type",
            Area = RebirthPatchTargetEvidenceArea.PatchType,
            Status = RebirthPatchTargetEvidenceStatus.Required,
            Risk = RebirthPatchTargetEvidenceRisk.High,
            RequiredEvidence = "Chosen patch type with reason: Prefix, Postfix, Transpiler, or no Harmony patch.",
            AcceptedExample = "Postfix only reads final reward and applies server-authoritative adjustment.",
            RejectedExample = "Prefix return false without side-effect map.",
            Verification = "Patch contract states why patch type is minimal.",
            Notes = "Transpiler is manual-review only."
        },
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.typed.parameters",
            Area = RebirthPatchTargetEvidenceArea.ParameterPlan,
            Status = RebirthPatchTargetEvidenceStatus.Required,
            Risk = RebirthPatchTargetEvidenceRisk.Critical,
            RequiredEvidence = "Typed Harmony parameters needed by the patch.",
            AcceptedExample = "__instance, ref int __result, EntityPlayer player as typed parameters if verified.",
            RejectedExample = "object[] __args or broad reflection access.",
            Verification = "Patch compiles without object[] __args.",
            Notes = "Hot or warm methods cannot use object[] __args."
        },
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.authority",
            Area = RebirthPatchTargetEvidenceArea.Authority,
            Status = RebirthPatchTargetEvidenceStatus.Required,
            Risk = RebirthPatchTargetEvidenceRisk.Critical,
            RequiredEvidence = "Proof whether method runs server, client, or both, and which side owns reward truth.",
            AcceptedExample = "Server authoritative branch only modifies reward truth; client only displays.",
            RejectedExample = "Client calculates item reward and assumes server follows.",
            Verification = "Dedicated server test included.",
            Notes = "Required for harvest/salvage rewards."
        },
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.side.effects",
            Area = RebirthPatchTargetEvidenceArea.SideEffects,
            Status = RebirthPatchTargetEvidenceStatus.Required,
            Risk = RebirthPatchTargetEvidenceRisk.High,
            RequiredEvidence = "List of vanilla side effects that must remain untouched.",
            AcceptedExample = "durability, XP, tool action, block destruction, sound, heat, quest progress remain vanilla unless explicitly in scope.",
            RejectedExample = "Assume postfix cannot affect anything.",
            Verification = "Patch summary lists side effects and non-goals.",
            Notes = "Keeps first slice narrow."
        },
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.performance",
            Area = RebirthPatchTargetEvidenceArea.Performance,
            Status = RebirthPatchTargetEvidenceStatus.Required,
            Risk = RebirthPatchTargetEvidenceRisk.High,
            RequiredEvidence = "Hot/warm/cold classification and disabled-overhead plan.",
            AcceptedExample = "First statement is direct disabled flag return; no logging/reflection/string lookup.",
            RejectedExample = "Feature disabled but still builds descriptions, scans collections, or logs.",
            Verification = "Source review plus Phase 3H performance gate.",
            Notes = "Disabled mode must be near-zero overhead."
        },
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.rollback",
            Area = RebirthPatchTargetEvidenceArea.Rollback,
            Status = RebirthPatchTargetEvidenceStatus.Required,
            Risk = RebirthPatchTargetEvidenceRisk.Medium,
            RequiredEvidence = "Single rollback path.",
            AcceptedExample = "Disable one flag or remove one patch adapter file.",
            RejectedExample = "Rollback requires undoing multiple feature systems.",
            Verification = "Package summary names rollback step.",
            Notes = "Essential for user testing."
        },
        new RebirthPatchTargetEvidenceRequirement
        {
            EvidenceId = "evidence.blocked.no.target",
            Area = RebirthPatchTargetEvidenceArea.Blocked,
            Status = RebirthPatchTargetEvidenceStatus.Blocked,
            Risk = RebirthPatchTargetEvidenceRisk.Blocked,
            RequiredEvidence = "No real patch may be added until the exact source target is available.",
            AcceptedExample = "User provides base/source package or current project contains target.",
            RejectedExample = "Generate patch from assumed 7DTD internals.",
            Verification = "If source target is not present, next package remains ledger/policy/test only.",
            Notes = "This is why this phase is still read-only."
        }
    };

    public static string GetSummaryReport()
    {
        int required = 0;
        int critical = 0;
        int blocked = 0;

        for (int i = 0; i < s_rows.Length; i++)
        {
            RebirthPatchTargetEvidenceRequirement r = s_rows[i];

            if (r.Status == RebirthPatchTargetEvidenceStatus.Required)
                required++;

            if (r.Risk == RebirthPatchTargetEvidenceRisk.Critical)
                critical++;

            if (r.Status == RebirthPatchTargetEvidenceStatus.Blocked || r.Risk == RebirthPatchTargetEvidenceRisk.Blocked)
                blocked++;
        }

        return "[RebirthPatchEvidence] Rows: " + s_rows.Length
            + "; required: " + required
            + "; critical: " + critical
            + "; blocked: " + blocked
            + ". Ledger is read-only.";
    }

    public static string GetEvidenceReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthPatchEvidence] patch target evidence ledger.");
        sb.AppendLine("  Read-only. This does not choose a method target or install Harmony.");
        sb.AppendLine("id | area | status | risk | required evidence | accepted | rejected | verification | notes");

        bool any = false;
        for (int i = 0; i < s_rows.Length; i++)
        {
            RebirthPatchTargetEvidenceRequirement r = s_rows[i];
            if (!Matches(r, f))
                continue;

            any = true;
            sb.Append(r.EvidenceId).Append(" | ")
              .Append(r.Area).Append(" | ")
              .Append(r.Status).Append(" | ")
              .Append(r.Risk).Append(" | ")
              .Append(r.RequiredEvidence).Append(" | ")
              .Append(r.AcceptedExample).Append(" | ")
              .Append(r.RejectedExample).Append(" | ")
              .Append(r.Verification).Append(" | ")
              .AppendLine(r.Notes);
        }

        if (!any)
            sb.AppendLine("No patch evidence row matched the filter.");

        return sb.ToString();
    }

    public static string GetRequiredReport()
    {
        return GetEvidenceReport("Required");
    }

    public static string GetBlockedReport()
    {
        return GetEvidenceReport("Blocked");
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPatchEvidence] safety:");
        sb.AppendLine("  1. Ledger is read-only.");
        sb.AppendLine("  2. No method target is chosen.");
        sb.AppendLine("  3. No Harmony patches are installed.");
        sb.AppendLine("  4. No gameplay behavior is migrated.");
        sb.AppendLine("  5. No XML parsing.");
        sb.AppendLine("  6. No custom game option lookup.");
        sb.AppendLine("  7. No network packets.");
        sb.AppendLine("  8. No save/load writes.");
        return sb.ToString();
    }

    private static bool Matches(RebirthPatchTargetEvidenceRequirement r, string filter)
    {
        if (r == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (r.EvidenceId != null && r.EvidenceId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.RequiredEvidence != null && r.RequiredEvidence.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.AcceptedExample != null && r.AcceptedExample.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.RejectedExample != null && r.RejectedExample.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Area.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Status.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
