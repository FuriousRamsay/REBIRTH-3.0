using System;
using System.Text;

#nullable disable

public enum RebirthProfileComparisonProfile
{
    Unknown,
    Release,
    Profiling,
    StrippedComparison,
    DeveloperDiagnostics,
    CompatibilityAudit
}

public enum RebirthProfileComparisonArea
{
    Unknown,
    Diagnostics,
    HarmonyPatches,
    Reflection,
    RuntimeReports,
    GameplaySystems,
    BuildArtifacts,
    Symbols,
    Safety
}

public sealed class RebirthProfileComparisonDecl
{
    public string ComparisonId;
    public RebirthProfileComparisonProfile Profile;
    public RebirthProfileComparisonArea Area;
    public string Included;
    public string Excluded;
    public string IntendedPurpose;
    public string MustRemainTrue;
    public string Notes;
}

/// <summary>
/// Dry-run build/profile comparison report.
/// This does not build, alter compiler symbols, or change packaging.
/// </summary>
public static class RebirthBuildProfileComparisonReport
{
    private static readonly RebirthProfileComparisonDecl[] s_rows = new[]
    {
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.release.diagnostics",
            Profile = RebirthProfileComparisonProfile.Release,
            Area = RebirthProfileComparisonArea.Diagnostics,
            Included = "required runtime only",
            Excluded = "profiling wrappers, developer diagnostic wrappers, expensive logging helpers",
            IntendedPurpose = "normal player/server build",
            MustRemainTrue = "disabled diagnostics must not construct messages or allocate in hot paths",
            Notes = "ReleaseDiagnosticsEnabled is currently false."
        },
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.release.harmony",
            Profile = RebirthProfileComparisonProfile.Release,
            Area = RebirthProfileComparisonArea.HarmonyPatches,
            Included = "owned gameplay patches only after migration",
            Excluded = "diagnostic PatchAll wrappers, broad external/global patches by default",
            IntendedPurpose = "minimum runtime patch surface",
            MustRemainTrue = "no object[] __args on hot patches",
            Notes = "Current fresh shell does not install gameplay Harmony patches."
        },
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.release.reflection",
            Profile = RebirthProfileComparisonProfile.Release,
            Area = RebirthProfileComparisonArea.Reflection,
            Included = "future init-only cached adapters if required",
            Excluded = "reflection lookup or Invoke/GetValue/SetValue in hot paths",
            IntendedPurpose = "stable runtime without silent reflection failures",
            MustRemainTrue = "reflection resolves once, caches once, validates once, fails loudly",
            Notes = "Current shell performs no reflection."
        },
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.profiling.diagnostics",
            Profile = RebirthProfileComparisonProfile.Profiling,
            Area = RebirthProfileComparisonArea.Diagnostics,
            Included = "explicit profiling measurements only",
            Excluded = "behavior-changing diagnostics",
            IntendedPurpose = "measure costs without changing gameplay semantics",
            MustRemainTrue = "profiling build can be compared fairly to release",
            Notes = "Profiling must still gate message construction."
        },
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.profiling.harmony",
            Profile = RebirthProfileComparisonProfile.Profiling,
            Area = RebirthProfileComparisonArea.HarmonyPatches,
            Included = "explicit profiling patches only when requested",
            Excluded = "implicit PatchAll diagnostics",
            IntendedPurpose = "instrument specific systems intentionally",
            MustRemainTrue = "profiling wrappers must be opt-in and removable",
            Notes = "No profiling wrappers are installed in this phase."
        },
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.stripped.diagnostics",
            Profile = RebirthProfileComparisonProfile.StrippedComparison,
            Area = RebirthProfileComparisonArea.Diagnostics,
            Included = "minimum required runtime shell",
            Excluded = "diagnostics, profiling reports, optional console report systems if desired",
            IntendedPurpose = "compare runtime cost without diagnostics/planning overhead",
            MustRemainTrue = "stripped build should isolate diagnostic and optional feature costs",
            Notes = "This phase is only a dry-run report."
        },
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.dev.reports",
            Profile = RebirthProfileComparisonProfile.DeveloperDiagnostics,
            Area = RebirthProfileComparisonArea.RuntimeReports,
            Included = "architecture/reporting commands and registries",
            Excluded = "none by default",
            IntendedPurpose = "developer visibility while migrating",
            MustRemainTrue = "reports are not called from gameplay hot paths",
            Notes = "Current many registries belong in this kind of profile eventually."
        },
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.compat.audit",
            Profile = RebirthProfileComparisonProfile.CompatibilityAudit,
            Area = RebirthProfileComparisonArea.Safety,
            Included = "external compatibility surfaces, version drift reports",
            Excluded = "automatic absorption into core gameplay",
            IntendedPurpose = "manual compatibility/version drift review",
            MustRemainTrue = "external/vendored logic remains separated from core feature ownership",
            Notes = "A* internals, GUIUtils.DrawLine, Entity.OnUpdatePosition stay manual-review."
        },
        new RebirthProfileComparisonDecl
        {
            ComparisonId = "profile.artifacts",
            Profile = RebirthProfileComparisonProfile.Release,
            Area = RebirthProfileComparisonArea.BuildArtifacts,
            Included = "source project files and docs",
            Excluded = ".vs, obj, bin, dll, pdb unless explicitly requested",
            IntendedPurpose = "clean source package handoff",
            MustRemainTrue = "packages should not carry stale build artifacts",
            Notes = "Matches current packaging convention."
        }
    };

    public static string GetSummaryReport()
    {
        int release = 0;
        int profiling = 0;
        int stripped = 0;
        int dev = 0;

        for (int i = 0; i < s_rows.Length; i++)
        {
            RebirthProfileComparisonDecl r = s_rows[i];

            if (r.Profile == RebirthProfileComparisonProfile.Release)
                release++;
            if (r.Profile == RebirthProfileComparisonProfile.Profiling)
                profiling++;
            if (r.Profile == RebirthProfileComparisonProfile.StrippedComparison)
                stripped++;
            if (r.Profile == RebirthProfileComparisonProfile.DeveloperDiagnostics)
                dev++;
        }

        return "[RebirthBuildProfileCompare] Rows: " + s_rows.Length
            + "; release: " + release
            + "; profiling: " + profiling
            + "; stripped: " + stripped
            + "; developer diagnostics: " + dev
            + ". Dry-run only.";
    }

    public static string GetComparisonReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthBuildProfileCompare] dry-run build/profile comparison.");
        sb.AppendLine("  This does not build, change compiler symbols, or alter packaging.");
        sb.AppendLine("id | profile | area | included | excluded | purpose | must remain true | notes");

        bool any = false;
        for (int i = 0; i < s_rows.Length; i++)
        {
            RebirthProfileComparisonDecl r = s_rows[i];
            if (!Matches(r, f))
                continue;

            any = true;
            sb.Append(r.ComparisonId).Append(" | ")
              .Append(r.Profile).Append(" | ")
              .Append(r.Area).Append(" | ")
              .Append(r.Included).Append(" | ")
              .Append(r.Excluded).Append(" | ")
              .Append(r.IntendedPurpose).Append(" | ")
              .Append(r.MustRemainTrue).Append(" | ")
              .AppendLine(r.Notes);
        }

        if (!any)
            sb.AppendLine("No build/profile comparison row matched the filter.");

        return sb.ToString();
    }

    public static string GetReleaseReport()
    {
        return GetComparisonReport("Release");
    }

    public static string GetProfilingReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine(GetComparisonReport("Profiling"));
        sb.AppendLine(GetComparisonReport("StrippedComparison"));
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildProfileCompare] safety:");
        sb.AppendLine("  1. Dry-run report only.");
        sb.AppendLine("  2. No build executed.");
        sb.AppendLine("  3. No compiler symbols changed.");
        sb.AppendLine("  4. No files copied or packaged differently.");
        sb.AppendLine("  5. No Harmony patches installed.");
        sb.AppendLine("  6. No gameplay behavior connected.");
        sb.AppendLine("  7. Future release/profiling/stripped profiles must be comparable.");
        return sb.ToString();
    }

    private static bool Matches(RebirthProfileComparisonDecl r, string filter)
    {
        if (r == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (r.ComparisonId != null && r.ComparisonId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Included != null && r.Included.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Excluded != null && r.Excluded.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.IntendedPurpose != null && r.IntendedPurpose.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.MustRemainTrue != null && r.MustRemainTrue.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Profile.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (r.Area.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
