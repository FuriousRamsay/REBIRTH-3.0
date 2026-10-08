using System;
using System.Text;

#nullable disable

public enum RebirthBuildVerificationProfile
{
    Unknown,
    Release,
    Profiling,
    StrippedComparison,
    DeveloperDiagnostics,
    CompatibilityAudit
}

public enum RebirthBuildVerificationGate
{
    Unknown,
    Compile,
    LoadGame,
    CommandSmokeTest,
    NoDiagnosticPatchAll,
    NoHotPathDebugConstruction,
    NoReflectionHotPath,
    NoObjectArgsHotPatch,
    ProfileDeltaReady,
    ArtifactPackaging,
    ManualReview
}

public enum RebirthBuildVerificationRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthBuildVerificationDecl
{
    public string VerificationId;
    public RebirthBuildVerificationProfile Profile;
    public RebirthBuildVerificationGate Gate;
    public RebirthBuildVerificationRisk Risk;
    public string AppliesTo;
    public string Requirement;
    public string FailureMeaning;
    public string NextAction;
    public string Notes;
}

/// <summary>
/// Read-only build verification and stripped-vs-profiling readiness registry.
/// This does not build, run MSBuild, or alter compiler symbols.
/// </summary>
public static class RebirthBuildVerificationRegistry
{
    private static readonly RebirthBuildVerificationDecl[] s_gates = new[]
    {
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.release.compile",
            Profile = RebirthBuildVerificationProfile.Release,
            Gate = RebirthBuildVerificationGate.Compile,
            Risk = RebirthBuildVerificationRisk.Critical,
            AppliesTo = "every package",
            Requirement = "Release project compiles cleanly with no stale generated references.",
            FailureMeaning = "Architecture package is not safe to continue.",
            NextAction = "Fix compile before adding more phases.",
            Notes = "User convention: if they say continue, assume previous package compiled."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.load.commands",
            Profile = RebirthBuildVerificationProfile.Release,
            Gate = RebirthBuildVerificationGate.CommandSmokeTest,
            Risk = RebirthBuildVerificationRisk.High,
            AppliesTo = "all console command registries",
            Requirement = "Game loads and rbsysfresh plus direct commands print reports without errors.",
            FailureMeaning = "Command registration or dependency reference issue.",
            NextAction = "Fix command/report code before gameplay migration.",
            Notes = "All current commands remain read-only."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.no.diagnostic.patchall",
            Profile = RebirthBuildVerificationProfile.Release,
            Gate = RebirthBuildVerificationGate.NoDiagnosticPatchAll,
            Risk = RebirthBuildVerificationRisk.Critical,
            AppliesTo = "Harmony patch registry and future diagnostics",
            Requirement = "Release build must not PatchAll diagnostic/profiling wrappers.",
            FailureMeaning = "Release hot paths may carry diagnostic wrapper overhead.",
            NextAction = "Move diagnostics behind explicit profiling/dev profile registration.",
            Notes = "Addresses prior object[]/diagnostic Harmony wrapper concerns."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.no.hot.debug.construction",
            Profile = RebirthBuildVerificationProfile.Release,
            Gate = RebirthBuildVerificationGate.NoHotPathDebugConstruction,
            Risk = RebirthBuildVerificationRisk.Critical,
            AppliesTo = "all hot-path domains",
            Requirement = "No debug string interpolation, concatenation, LINQ, collection scan, stack trace, reflection, helper call, or lambda capture before debug gate.",
            FailureMeaning = "Release performance can be affected even when debug is disabled.",
            NextAction = "Gate debug work before construction.",
            Notes = "Strict directive for all migrated feature code."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.no.hot.reflection",
            Profile = RebirthBuildVerificationProfile.Release,
            Gate = RebirthBuildVerificationGate.NoReflectionHotPath,
            Risk = RebirthBuildVerificationRisk.Critical,
            AppliesTo = "hot patches and runtime loops",
            Requirement = "No reflection lookup or MethodInfo.Invoke/FieldInfo.GetValue per hot-path call.",
            FailureMeaning = "Hot-path runtime cost and silent failure risk.",
            NextAction = "Use explicit adapters that resolve once, cache once, validate once, and fail loudly.",
            Notes = "Reflection policy is formalized in Phase 2K."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.no.object.args.hotpatch",
            Profile = RebirthBuildVerificationProfile.Release,
            Gate = RebirthBuildVerificationGate.NoObjectArgsHotPatch,
            Risk = RebirthBuildVerificationRisk.Critical,
            AppliesTo = "Harmony prefixes/postfixes on hot methods",
            Requirement = "No object[] __args signatures on hot patches.",
            FailureMeaning = "Harmony may allocate/build argument arrays every call.",
            NextAction = "Use exact typed parameters or avoid patch.",
            Notes = "Especially important for Update/Damage/Render/ItemValue/AI paths."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.profiling.delta",
            Profile = RebirthBuildVerificationProfile.Profiling,
            Gate = RebirthBuildVerificationGate.ProfileDeltaReady,
            Risk = RebirthBuildVerificationRisk.High,
            AppliesTo = "future performance harness",
            Requirement = "Profiling profile can enable measurements without changing gameplay semantics.",
            FailureMeaning = "Profiling build cannot be compared fairly to release/stripped.",
            NextAction = "Separate measurement from behavior and ensure debug construction is gated.",
            Notes = "Profiling build may collect data; release must stay clean."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.stripped.delta",
            Profile = RebirthBuildVerificationProfile.StrippedComparison,
            Gate = RebirthBuildVerificationGate.ProfileDeltaReady,
            Risk = RebirthBuildVerificationRisk.High,
            AppliesTo = "future stripped comparison package",
            Requirement = "Stripped build removes diagnostics/profiling and optional feature tenants for comparison.",
            FailureMeaning = "Cannot isolate cost of migrated systems.",
            NextAction = "Define build/profile switches before performance claims.",
            Notes = "Current build scripts are still dry-run."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.package.clean",
            Profile = RebirthBuildVerificationProfile.Release,
            Gate = RebirthBuildVerificationGate.ArtifactPackaging,
            Risk = RebirthBuildVerificationRisk.Medium,
            AppliesTo = "generated VS project packages",
            Requirement = "Packages exclude .vs, obj, bin, compiled dll/pdb unless explicitly requested.",
            FailureMeaning = "Package may contain stale build artifacts.",
            NextAction = "Regenerate clean source package.",
            Notes = "Matches current packaging convention."
        },
        new RebirthBuildVerificationDecl
        {
            VerificationId = "verify.compat.audit",
            Profile = RebirthBuildVerificationProfile.CompatibilityAudit,
            Gate = RebirthBuildVerificationGate.ManualReview,
            Risk = RebirthBuildVerificationRisk.High,
            AppliesTo = "external compatibility and 3.0 migration drift",
            Requirement = "Compatibility audit profile identifies external/vendored/version drift before core migration.",
            FailureMeaning = "Core migration may absorb external/version-specific fixes accidentally.",
            NextAction = "Separate compatibility fixes from feature logic.",
            Notes = "Vendored governance and compatibility XML stay separate."
        }
    };

    public static string GetSummaryReport()
    {
        int critical = 0;
        int release = 0;
        int profileDelta = 0;

        for (int i = 0; i < s_gates.Length; i++)
        {
            RebirthBuildVerificationDecl g = s_gates[i];

            if (g.Risk == RebirthBuildVerificationRisk.Critical)
                critical++;
            if (g.Profile == RebirthBuildVerificationProfile.Release)
                release++;
            if (g.Gate == RebirthBuildVerificationGate.ProfileDeltaReady)
                profileDelta++;
        }

        return "[RebirthBuildVerify] Gates: " + s_gates.Length
            + "; release gates: " + release
            + "; profile-delta gates: " + profileDelta
            + "; critical: " + critical
            + ". Registry is read-only; declarations only, execution=NotRun.";
    }

    public static string GetGateReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthBuildVerify] build verification and stripped-vs-profiling readiness gates.");
        sb.AppendLine("  Read-only. This does not build, run MSBuild, or change compiler symbols.");
        sb.AppendLine("id | profile | gate | risk | applies to | requirement | failure meaning | next action | notes");

        bool any = false;
        for (int i = 0; i < s_gates.Length; i++)
        {
            RebirthBuildVerificationDecl g = s_gates[i];
            if (!Matches(g, f))
                continue;

            any = true;
            sb.Append(g.VerificationId).Append(" | ")
              .Append(g.Profile).Append(" | ")
              .Append(g.Gate).Append(" | ")
              .Append(g.Risk).Append(" | ")
              .Append(g.AppliesTo).Append(" | ")
              .Append(g.Requirement).Append(" | ")
              .Append(g.FailureMeaning).Append(" | ")
              .Append(g.NextAction).Append(" | ")
              .AppendLine(g.Notes);
        }

        if (!any)
            sb.AppendLine("No build verification gate matched the filter.");

        return sb.ToString();
    }

    public static string GetReleaseReport()
    {
        return GetGateReport("Release");
    }

    public static string GetProfilingReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthBuildVerify] profiling/stripped comparison readiness:");
        sb.AppendLine(GetGateReport("Profiling"));
        sb.AppendLine(GetGateReport("StrippedComparison"));
        return sb.ToString();
    }

    public static string GetHotPathReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthBuildVerify] hot-path build verification gates:");
        sb.AppendLine(GetGateReport("hot"));
        sb.AppendLine(GetGateReport("object"));
        sb.AppendLine(GetGateReport("reflection"));
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildVerify] safety rules:");
        sb.AppendLine("  1. Registry is read-only.");
        sb.AppendLine("  2. No build is executed.");
        sb.AppendLine("  3. No compiler symbols are changed.");
        sb.AppendLine("  4. No gameplay behavior is connected.");
        sb.AppendLine("  5. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  6. Release must not carry diagnostic hot-path wrappers.");
        sb.AppendLine("  7. Profiling must measure without changing gameplay semantics.");
        sb.AppendLine("  8. Stripped comparison must isolate diagnostic/feature costs.");
        return sb.ToString();
    }

    private static bool Matches(RebirthBuildVerificationDecl g, string filter)
    {
        if (g == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (g.VerificationId != null && g.VerificationId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (g.AppliesTo != null && g.AppliesTo.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (g.Requirement != null && g.Requirement.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (g.FailureMeaning != null && g.FailureMeaning.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (g.NextAction != null && g.NextAction.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (g.Profile.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (g.Gate.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (g.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
