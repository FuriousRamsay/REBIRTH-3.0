using System;
using System.Text;

#nullable disable

public enum RebirthFirstGameplaySliceStatus
{
    Unknown,
    Candidate,
    Required,
    Blocked,
    ManualReview
}

public enum RebirthFirstGameplaySliceRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public enum RebirthFirstGameplaySliceArea
{
    Unknown,
    PatchTarget,
    Authority,
    FeatureFlag,
    Diagnostics,
    TestGate,
    ScopeControl,
    Rollback,
    Performance
}

public sealed class RebirthFirstGameplaySliceContractDecl
{
    public string ContractId;
    public RebirthFirstGameplaySliceArea Area;
    public RebirthFirstGameplaySliceStatus Status;
    public RebirthFirstGameplaySliceRisk Risk;
    public string Requirement;
    public string Allowed;
    public string Blocked;
    public string Verification;
    public string Notes;
}

/// <summary>
/// Read-only first gameplay slice contract.
/// This defines the entry criteria before implementing the first harvest/salvage behavior.
/// It does not patch behavior or migrate gameplay.
/// </summary>
public static class RebirthFirstGameplaySliceContractRegistry
{
    private static readonly RebirthFirstGameplaySliceContractDecl[] s_rows = new[]
    {
        new RebirthFirstGameplaySliceContractDecl
        {
            ContractId = "firstslice.scope.one.behavior",
            Area = RebirthFirstGameplaySliceArea.ScopeControl,
            Status = RebirthFirstGameplaySliceStatus.Required,
            Risk = RebirthFirstGameplaySliceRisk.Critical,
            Requirement = "First gameplay slice must implement exactly one small harvest/salvage behavior.",
            Allowed = "One isolated server-authoritative reward modifier or one isolated display/report behavior.",
            Blocked = "Combining harvest, salvage, vehicle rewards, stealth sound, XP, UI, and network behavior in one patch.",
            Verification = "Command report and package summary must name the single behavior.",
            Notes = "Prevents repeating broad risky patches."
        },
        new RebirthFirstGameplaySliceContractDecl
        {
            ContractId = "firstslice.patch.exact.target",
            Area = RebirthFirstGameplaySliceArea.PatchTarget,
            Status = RebirthFirstGameplaySliceStatus.Required,
            Risk = RebirthFirstGameplaySliceRisk.Critical,
            Requirement = "Exact vanilla/base method target must be identified before any Harmony patch is added.",
            Allowed = "Typed Prefix/Postfix on a documented method after source audit.",
            Blocked = "Adding an unaudited class to production PatchAll, guessed method names, object[] __args, or unowned global patches.",
            Verification = "Patch contract states declaring type, method name, patch type, parameters, and side effects.",
            Notes = "No blind patching from memory."
        },
        new RebirthFirstGameplaySliceContractDecl
        {
            ContractId = "firstslice.authority.server",
            Area = RebirthFirstGameplaySliceArea.Authority,
            Status = RebirthFirstGameplaySliceStatus.Required,
            Risk = RebirthFirstGameplaySliceRisk.Critical,
            Requirement = "Reward truth must be server-authoritative if it affects items, XP, loot, or persistent state.",
            Allowed = "Client display may mirror server truth only.",
            Blocked = "Client-only reward calculation for authoritative outputs.",
            Verification = "Dedicated server test from Phase 3H must pass before enabling.",
            Notes = "Prevents SP-only or client-side divergence."
        },
        new RebirthFirstGameplaySliceContractDecl
        {
            ContractId = "firstslice.flag.default.false",
            Area = RebirthFirstGameplaySliceArea.FeatureFlag,
            Status = RebirthFirstGameplaySliceStatus.Required,
            Risk = RebirthFirstGameplaySliceRisk.High,
            Requirement = "Feature flag must remain false by default until tests pass.",
            Allowed = "Direct bool flag read after exact target is chosen.",
            Blocked = "String/dictionary/XML/option lookup in a hot path.",
            Verification = "rbhotflagsfresh list and harvest policy commands show disabled defaults.",
            Notes = "Keeps runtime inert until intentionally enabled."
        },
        new RebirthFirstGameplaySliceContractDecl
        {
            ContractId = "firstslice.diagnostics.gated",
            Area = RebirthFirstGameplaySliceArea.Diagnostics,
            Status = RebirthFirstGameplaySliceStatus.Required,
            Risk = RebirthFirstGameplaySliceRisk.High,
            Requirement = "All diagnostics must be gated before string construction or expensive work.",
            Allowed = "if (RebirthDiagnosticGates.CanBuildDeveloperMessage()) before message construction.",
            Blocked = "Interpolation, concatenation, LINQ, reflection, stack traces, helper calls, lambdas before gate.",
            Verification = "Manual source review and disabled-diagnostic release/profiling comparison.",
            Notes = "Matches strict debug/performance rule."
        },
        new RebirthFirstGameplaySliceContractDecl
        {
            ContractId = "firstslice.phase3h.tests",
            Area = RebirthFirstGameplaySliceArea.TestGate,
            Status = RebirthFirstGameplaySliceStatus.Required,
            Risk = RebirthFirstGameplaySliceRisk.High,
            Requirement = "Relevant Phase 3H test gate rows must be acknowledged before implementation.",
            Allowed = "Implement only behavior covered by the named test rows.",
            Blocked = "Implementing behavior without a matching test row.",
            Verification = "Package summary lists test IDs used for the slice.",
            Notes = "Tests come before behavior."
        },
        new RebirthFirstGameplaySliceContractDecl
        {
            ContractId = "firstslice.rollback.simple",
            Area = RebirthFirstGameplaySliceArea.Rollback,
            Status = RebirthFirstGameplaySliceStatus.Required,
            Risk = RebirthFirstGameplaySliceRisk.Medium,
            Requirement = "Rollback must be simple: disable one flag or remove one patch file.",
            Allowed = "One feature flag, one patch adapter, one policy consumer.",
            Blocked = "Cross-cutting changes that require reverting many systems.",
            Verification = "Package summary names rollback file(s).",
            Notes = "Important for fast testing with the user."
        },
        new RebirthFirstGameplaySliceContractDecl
        {
            ContractId = "firstslice.performance.no.alloc",
            Area = RebirthFirstGameplaySliceArea.Performance,
            Status = RebirthFirstGameplaySliceStatus.Required,
            Risk = RebirthFirstGameplaySliceRisk.High,
            Requirement = "Disabled feature must be near-zero overhead in hot paths.",
            Allowed = "One direct bool check and return.",
            Blocked = "Per-call allocations, collection scans, reflection, logging, option lookup, string feature lookup.",
            Verification = "Source review and profiling/stripped comparison when available.",
            Notes = "Avoids hidden performance loss."
        }
    };

    public static string GetSummaryReport()
    {
        int required = 0;
        int critical = 0;
        int blocked = 0;

        for (int i = 0; i < s_rows.Length; i++)
        {
            RebirthFirstGameplaySliceContractDecl r = s_rows[i];

            if (r.Status == RebirthFirstGameplaySliceStatus.Required)
                required++;

            if (r.Risk == RebirthFirstGameplaySliceRisk.Critical)
                critical++;

            if (r.Status == RebirthFirstGameplaySliceStatus.Blocked || r.Risk == RebirthFirstGameplaySliceRisk.Blocked)
                blocked++;
        }

        return "[RebirthFirstSliceContract] Rows: " + s_rows.Length
            + "; required: " + required
            + "; critical: " + critical
            + "; blocked: " + blocked
            + ". Contract is read-only.";
    }

    public static string GetContractReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFirstSliceContract] first gameplay slice target contract.");
        sb.AppendLine("  Read-only. This does not patch or migrate gameplay.");
        sb.AppendLine("id | area | status | risk | requirement | allowed | blocked | verification | notes");

        bool any = false;
        for (int i = 0; i < s_rows.Length; i++)
        {
            RebirthFirstGameplaySliceContractDecl r = s_rows[i];
            if (!Matches(r, f))
                continue;

            any = true;
            sb.Append(r.ContractId).Append(" | ")
              .Append(r.Area).Append(" | ")
              .Append(r.Status).Append(" | ")
              .Append(r.Risk).Append(" | ")
              .Append(r.Requirement).Append(" | ")
              .Append(r.Allowed).Append(" | ")
              .Append(r.Blocked).Append(" | ")
              .Append(r.Verification).Append(" | ")
              .AppendLine(r.Notes);
        }

        if (!any)
            sb.AppendLine("No first-slice contract row matched the filter.");

        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthFirstSliceContract] blocked for first gameplay slice:");
        sb.AppendLine("  - player crawl");
        sb.AppendLine("  - ItemValue/stat randomization");
        sb.AppendLine("  - paint/renderFace");
        sb.AppendLine("  - zombie AI/pathing");
        sb.AppendLine("  - survivor scenario behavior");
        sb.AppendLine("  - combined harvest + salvage + sound + vehicle + XP + UI migration");
        sb.AppendLine("  - diagnostic/profiling classes entering production PatchAll");
        sb.AppendLine("  - object[] __args on hot patches");
        sb.AppendLine("  - hot-path reflection or string/dictionary option lookup");
        return sb.ToString();
    }

    public static string GetRecommendedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthFirstSliceContract] recommended implementation shape:");
        sb.AppendLine("  1. Audit exact base method target from provided source.");
        sb.AppendLine("  2. Add one patch adapter file only after target is known.");
        sb.AppendLine("  3. Patch typed parameters only.");
        sb.AppendLine("  4. First line should be a direct bool/policy guard when possible.");
        sb.AppendLine("  5. Implement one behavior.");
        sb.AppendLine("  6. Keep diagnostics absent or gated before construction.");
        sb.AppendLine("  7. Keep default disabled until test command and manual test are ready.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthFirstSliceContract] safety:");
        sb.AppendLine("  1. Contract is read-only.");
        sb.AppendLine("  2. No gameplay behavior is migrated.");
        sb.AppendLine("  3. No Harmony patches installed.");
        sb.AppendLine("  4. No XML parsing.");
        sb.AppendLine("  5. No custom game option lookup.");
        sb.AppendLine("  6. No network packets.");
        sb.AppendLine("  7. No save/load writes.");
        sb.AppendLine("  8. No file movement or project consolidation performed.");
        return sb.ToString();
    }

    private static bool Matches(RebirthFirstGameplaySliceContractDecl r, string filter)
    {
        if (r == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (r.ContractId != null && r.ContractId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Requirement != null && r.Requirement.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Allowed != null && r.Allowed.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Blocked != null && r.Blocked.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Verification != null && r.Verification.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
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
