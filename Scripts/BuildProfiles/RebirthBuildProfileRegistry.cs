using System;
using System.Text;

#nullable disable

public enum RebirthBuildProfileKind
{
    Unknown,
    Release,
    Profiling,
    StrippedComparison,
    DeveloperDiagnostics,
    CompatibilityAudit
}

public enum RebirthBuildProfilePatchPolicy
{
    Unknown,
    NoDiagnosticPatches,
    ProfilingPatchesAllowed,
    StructuralPatchesExcluded,
    PatchInstallComparison,
    ManualReviewRequired
}

public enum RebirthBuildProfileDiagnosticPolicy
{
    Unknown,
    CompileOut,
    BoolGatedBeforeConstruction,
    ProfilingOnly,
    DeveloperOnly,
    ManualReviewRequired
}

public sealed class RebirthBuildProfileDecl
{
    public RebirthBuildProfileKind Profile;
    public string ProfileId;
    public RebirthBuildProfilePatchPolicy PatchPolicy;
    public RebirthBuildProfileDiagnosticPolicy DiagnosticPolicy;
    public bool AllowsAlwaysPatchedDiagnostics;
    public bool AllowsPatchInstallMeasurement;
    public bool AllowsRuntimeFeatureSweep;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthBuildProfileRuleDecl
{
    public string RuleId;
    public RebirthBuildProfileKind Profile;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// Read-only build profile contract registry.
/// This does not change compiler symbols, build output, or patch behavior.
/// </summary>
public static class RebirthBuildProfileRegistry
{
    private static readonly RebirthBuildProfileDecl[] s_profiles = new[]
    {
        new RebirthBuildProfileDecl
        {
            Profile = RebirthBuildProfileKind.Release,
            ProfileId = "release",
            PatchPolicy = RebirthBuildProfilePatchPolicy.NoDiagnosticPatches,
            DiagnosticPolicy = RebirthBuildProfileDiagnosticPolicy.CompileOut,
            AllowsAlwaysPatchedDiagnostics = false,
            AllowsPatchInstallMeasurement = false,
            AllowsRuntimeFeatureSweep = false,
            Evidence = "debug policy / prior profiler concern",
            Notes = "Release must not carry diagnostic Harmony wrappers, object[] postfixes, or diagnostic string construction."
        },
        new RebirthBuildProfileDecl
        {
            Profile = RebirthBuildProfileKind.Profiling,
            ProfileId = "profiling",
            PatchPolicy = RebirthBuildProfilePatchPolicy.ProfilingPatchesAllowed,
            DiagnosticPolicy = RebirthBuildProfileDiagnosticPolicy.ProfilingOnly,
            AllowsAlwaysPatchedDiagnostics = true,
            AllowsPatchInstallMeasurement = true,
            AllowsRuntimeFeatureSweep = true,
            Evidence = "performance comparison requirement",
            Notes = "Profiling build may install diagnostic adapters, but all overhead must be intentional and reported."
        },
        new RebirthBuildProfileDecl
        {
            Profile = RebirthBuildProfileKind.StrippedComparison,
            ProfileId = "stripped",
            PatchPolicy = RebirthBuildProfilePatchPolicy.StructuralPatchesExcluded,
            DiagnosticPolicy = RebirthBuildProfileDiagnosticPolicy.CompileOut,
            AllowsAlwaysPatchedDiagnostics = false,
            AllowsPatchInstallMeasurement = true,
            AllowsRuntimeFeatureSweep = false,
            Evidence = "user requested stripped comparison build",
            Notes = "Used to compare baseline/no-REBIRTH-hot-patches against release/profiling."
        },
        new RebirthBuildProfileDecl
        {
            Profile = RebirthBuildProfileKind.DeveloperDiagnostics,
            ProfileId = "devdiagnostics",
            PatchPolicy = RebirthBuildProfilePatchPolicy.ManualReviewRequired,
            DiagnosticPolicy = RebirthBuildProfileDiagnosticPolicy.DeveloperOnly,
            AllowsAlwaysPatchedDiagnostics = false,
            AllowsPatchInstallMeasurement = false,
            AllowsRuntimeFeatureSweep = true,
            Evidence = "debug policy",
            Notes = "For local targeted diagnostics only. Must not become normal release behavior."
        },
        new RebirthBuildProfileDecl
        {
            Profile = RebirthBuildProfileKind.CompatibilityAudit,
            ProfileId = "compataudit",
            PatchPolicy = RebirthBuildProfilePatchPolicy.ManualReviewRequired,
            DiagnosticPolicy = RebirthBuildProfileDiagnosticPolicy.BoolGatedBeforeConstruction,
            AllowsAlwaysPatchedDiagnostics = false,
            AllowsPatchInstallMeasurement = false,
            AllowsRuntimeFeatureSweep = false,
            Evidence = "3.0 migration / external project governance",
            Notes = "Used to inspect compatibility and migration ledgers, not gameplay performance."
        }
    };

    private static readonly RebirthBuildProfileRuleDecl[] s_rules = new[]
    {
        new RebirthBuildProfileRuleDecl
        {
            RuleId = "release-no-diagnostic-wrappers",
            Profile = RebirthBuildProfileKind.Release,
            Evidence = "prior profiling/debug wrapper concern",
            Notes = "Release must not patch diagnostic wrappers into hot methods."
        },
        new RebirthBuildProfileRuleDecl
        {
            RuleId = "release-no-object-array-postfix",
            Profile = RebirthBuildProfileKind.Release,
            Evidence = "prior object[] Harmony warning",
            Notes = "Avoid Postfix(object[] __args, ...) on hot methods in release."
        },
        new RebirthBuildProfileRuleDecl
        {
            RuleId = "diagnostics-gated-before-construction",
            Profile = RebirthBuildProfileKind.DeveloperDiagnostics,
            Evidence = "debug policy",
            Notes = "No string interpolation, concatenation, formatting, LINQ, helper calls, or stack traces before the bool gate."
        },
        new RebirthBuildProfileRuleDecl
        {
            RuleId = "profiling-overhead-explicit",
            Profile = RebirthBuildProfileKind.Profiling,
            Evidence = "performance comparison requirement",
            Notes = "Profiling overhead is allowed only when the profile name and reports make it obvious."
        },
        new RebirthBuildProfileRuleDecl
        {
            RuleId = "stripped-structural-hotpatches-excluded",
            Profile = RebirthBuildProfileKind.StrippedComparison,
            Evidence = "performance comparison requirement",
            Notes = "Stripped comparison builds should exclude hot-method REBIRTH structural adapters when possible."
        }
    };

    public static string GetProfileReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthBuildProfiles] read-only build profile contracts.");
        sb.AppendLine("profile | patch policy | diagnostics | always diag | patch measure | sweep | evidence | notes");

        bool any = false;
        for (int i = 0; i < s_profiles.Length; i++)
        {
            RebirthBuildProfileDecl p = s_profiles[i];
            if (!string.IsNullOrEmpty(f)
                && (p.ProfileId == null || p.ProfileId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && p.Profile.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            any = true;
            sb.Append(p.ProfileId).Append(" | ")
              .Append(p.PatchPolicy).Append(" | ")
              .Append(p.DiagnosticPolicy).Append(" | ")
              .Append(p.AllowsAlwaysPatchedDiagnostics).Append(" | ")
              .Append(p.AllowsPatchInstallMeasurement).Append(" | ")
              .Append(p.AllowsRuntimeFeatureSweep).Append(" | ")
              .Append(p.Evidence).Append(" | ")
              .AppendLine(p.Notes);
        }

        if (!any)
            sb.AppendLine("No build profile matched the filter.");

        return sb.ToString();
    }

    public static string GetRuleReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildProfiles] build profile rules.");
        sb.AppendLine("rule | profile | evidence | notes");

        for (int i = 0; i < s_rules.Length; i++)
        {
            RebirthBuildProfileRuleDecl r = s_rules[i];
            sb.Append(r.RuleId).Append(" | ")
              .Append(r.Profile).Append(" | ")
              .Append(r.Evidence).Append(" | ")
              .AppendLine(r.Notes);
        }

        return sb.ToString();
    }

    public static string GetPatchAdapterCompatibilityReport()
    {
        RebirthPatchAdapterDecl[] adapters = RebirthPatchAdapterPlanRegistry.GetSnapshot();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthBuildProfiles] patch adapter/profile compatibility.");
        sb.AppendLine("adapter | target | profile guidance | notes");

        for (int i = 0; i < adapters.Length; i++)
        {
            RebirthPatchAdapterDecl a = adapters[i];

            string guidance;
            if (a.InstallPolicy == RebirthPatchAdapterInstallPolicy.NeverInstall)
                guidance = "excluded in all profiles unless explicitly redesigned";
            else if (a.InstallPolicy == RebirthPatchAdapterInstallPolicy.ProfilingBuildOnly)
                guidance = "profiling only; never release";
            else if (a.AllowsPatchInstallMeasurement)
                guidance = "release/runtime-gate plus profiling/stripped comparison candidates";
            else if (a.InstallPolicy == RebirthPatchAdapterInstallPolicy.RestartRequired)
                guidance = "restart/build-profile comparison only";
            else
                guidance = "manual review";

            sb.Append(a.AdapterId).Append(" | ")
              .Append(a.TargetMethod).Append(" | ")
              .Append(guidance).Append(" | ")
              .AppendLine(a.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildProfiles] safety rules:");
        sb.AppendLine("  1. This registry is read-only.");
        sb.AppendLine("  2. Release profile must not include diagnostic Harmony wrappers.");
        sb.AppendLine("  3. Release profile must not use object[] Harmony postfixes on hot paths.");
        sb.AppendLine("  4. Diagnostics must be compile-out or gated before construction.");
        sb.AppendLine("  5. Profiling build overhead must be explicit and reportable.");
        sb.AppendLine("  6. Stripped comparison builds are for patch-install overhead comparison.");
        sb.AppendLine("  7. This phase does not change compiler symbols or build output.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthBuildProfiles] Build profiles: " + s_profiles.Length
            + "; rules: " + s_rules.Length
            + ". Contracts are read-only and do not alter builds.";
    }
}
