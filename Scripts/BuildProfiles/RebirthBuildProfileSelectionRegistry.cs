using System;
using System.Text;

#nullable disable

public enum RebirthBuildProfileSelectionState
{
    Unknown,
    NoneSelected,
    Release,
    Profiling,
    StrippedComparison,
    DeveloperDiagnostics,
    CompatibilityAudit
}

public sealed class RebirthBuildProfileSelection
{
    public RebirthBuildProfileSelectionState SelectedState;
    public string ProfileId;
    public string Reason;
    public DateTime ChangedUtc;
}

/// <summary>
/// In-memory build profile selection registry.
/// This does not change compiler symbols, build output, csproj, Harmony behavior, or gameplay.
/// </summary>
public static class RebirthBuildProfileSelectionRegistry
{
    private static RebirthBuildProfileSelection s_selection = new RebirthBuildProfileSelection
    {
        SelectedState = RebirthBuildProfileSelectionState.NoneSelected,
        ProfileId = "none",
        Reason = "no in-memory build profile selected",
        ChangedUtc = DateTime.MinValue
    };

    public static string SetProfile(string profileId)
    {
        RebirthBuildProfileSelectionState state = ParseProfile(profileId);
        if (state == RebirthBuildProfileSelectionState.Unknown)
            return "[RebirthBuildProfileSelection] Unknown profile: " + profileId + ". Use release, profiling, stripped, devdiagnostics, or compataudit.";

        s_selection = new RebirthBuildProfileSelection
        {
            SelectedState = state,
            ProfileId = NormalizeProfileId(state),
            Reason = "manual in-memory profile selection",
            ChangedUtc = DateTime.UtcNow
        };

        return "[RebirthBuildProfileSelection] Selected in-memory build profile: " + s_selection.ProfileId
            + ". No compiler symbols, project files, build output, Harmony patches, or gameplay behavior were changed.";
    }

    public static string Reset()
    {
        s_selection = new RebirthBuildProfileSelection
        {
            SelectedState = RebirthBuildProfileSelectionState.NoneSelected,
            ProfileId = "none",
            Reason = "selection reset",
            ChangedUtc = DateTime.UtcNow
        };

        return "[RebirthBuildProfileSelection] Reset in-memory build profile selection. No build output was changed.";
    }

    public static RebirthBuildProfileSelection GetSelectionSnapshot()
    {
        return new RebirthBuildProfileSelection
        {
            SelectedState = s_selection.SelectedState,
            ProfileId = s_selection.ProfileId,
            Reason = s_selection.Reason,
            ChangedUtc = s_selection.ChangedUtc
        };
    }

    public static string GetStatusReport()
    {
        RebirthBuildProfileSelection s = GetSelectionSnapshot();
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[RebirthBuildProfileSelection] current in-memory selection.");
        sb.Append("  Selected: ").Append(s.ProfileId).Append(" / ").AppendLine(s.SelectedState.ToString());
        sb.Append("  Reason: ").AppendLine(s.Reason);
        sb.AppendLine("  This selection is not persisted and does not change compiler symbols or build output.");
        return sb.ToString();
    }

    public static string GetDryRunManifestReport(string profileFilter)
    {
        string f = (profileFilter ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
        {
            RebirthBuildProfileSelection s = GetSelectionSnapshot();
            f = s.ProfileId;
        }

        if (string.IsNullOrEmpty(f) || f == "none")
            return "[RebirthBuildProfileSelection] No profile selected. Use rbbuildfresh select release|profiling|stripped first, or pass a profile filter.";

        RebirthBuildProfileSelectionState state = ParseProfile(f);
        if (state == RebirthBuildProfileSelectionState.Unknown)
            return "[RebirthBuildProfileSelection] Unknown profile for manifest dry-run: " + f;

        string profileId = NormalizeProfileId(state);

        StringBuilder sb = new StringBuilder(16384);
        sb.Append("[RebirthBuildProfileSelection] dry-run build manifest for profile: ").AppendLine(profileId);
        sb.AppendLine("  This is a report only. It does not change compiler symbols or build output.");

        AppendProfilePolicy(sb, profileId);
        AppendPatchAdapterPlan(sb, profileId);
        AppendDiagnosticPlan(sb, profileId);
        AppendTogglePlan(sb, profileId);

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildProfileSelection] safety rules:");
        sb.AppendLine("  1. Profile selection is in-memory only.");
        sb.AppendLine("  2. Profile selection is not saved to disk.");
        sb.AppendLine("  3. Profile selection does not change compiler symbols.");
        sb.AppendLine("  4. Profile selection does not change csproj or build output.");
        sb.AppendLine("  5. Profile selection does not install/uninstall Harmony patches.");
        sb.AppendLine("  6. Profile selection does not change gameplay.");
        sb.AppendLine("  7. Real build scripts must be added in a later phase and must obey build profile contracts.");
        return sb.ToString();
    }

    private static void AppendProfilePolicy(StringBuilder sb, string profileId)
    {
        sb.AppendLine();
        sb.AppendLine("Profile policy:");
        sb.Append(RebirthBuildProfileRegistry.GetProfileReport(profileId));
    }

    private static void AppendPatchAdapterPlan(StringBuilder sb, string profileId)
    {
        RebirthPatchAdapterDecl[] adapters = RebirthPatchAdapterPlanRegistry.GetSnapshot();

        sb.AppendLine();
        sb.AppendLine("Patch adapter dry-run inclusion:");
        sb.AppendLine("adapter | target | dry-run disposition | reason");

        for (int i = 0; i < adapters.Length; i++)
        {
            RebirthPatchAdapterDecl a = adapters[i];
            string disposition = GetAdapterDisposition(profileId, a);
            string reason = GetAdapterReason(profileId, a);

            sb.Append(a.AdapterId).Append(" | ")
              .Append(a.TargetMethod).Append(" | ")
              .Append(disposition).Append(" | ")
              .AppendLine(reason);
        }
    }

    private static void AppendDiagnosticPlan(StringBuilder sb, string profileId)
    {
        sb.AppendLine();
        sb.AppendLine("Diagnostic dry-run policy:");

        if (profileId == "release")
        {
            sb.AppendLine("  Diagnostics: compile out or disabled.");
            sb.AppendLine("  Always-patched diagnostic wrappers: forbidden.");
            sb.AppendLine("  object[] Harmony postfix diagnostics on hot paths: forbidden.");
        }
        else if (profileId == "profiling")
        {
            sb.AppendLine("  Diagnostics: allowed when explicitly profiling.");
            sb.AppendLine("  Diagnostic overhead must be reportable.");
            sb.AppendLine("  Profiling-only wrappers may exist in this build profile.");
        }
        else if (profileId == "stripped")
        {
            sb.AppendLine("  Diagnostics: compile out.");
            sb.AppendLine("  Hot-method structural adapters: excluded where possible.");
            sb.AppendLine("  Used for patch-install overhead comparison.");
        }
        else if (profileId == "devdiagnostics")
        {
            sb.AppendLine("  Diagnostics: targeted developer-only diagnostics.");
            sb.AppendLine("  Must be bool-gated before construction.");
            sb.AppendLine("  Not a release/profile comparison baseline.");
        }
        else if (profileId == "compataudit")
        {
            sb.AppendLine("  Diagnostics: compatibility/audit reports only.");
            sb.AppendLine("  No gameplay performance conclusions should be drawn from this profile.");
        }
    }

    private static void AppendTogglePlan(StringBuilder sb, string profileId)
    {
        RebirthModuleToggleDecl[] modules = RebirthModuleToggleRegistry.GetSnapshot();

        sb.AppendLine();
        sb.AppendLine("Module toggle dry-run policy:");
        sb.AppendLine("module | default | shadow | profile disposition | notes");

        for (int i = 0; i < modules.Length; i++)
        {
            RebirthModuleToggleDecl m = modules[i];
            RebirthModuleShadowToggle shadow = RebirthModuleToggleStateRegistry.GetShadowStateSnapshot(m.ModuleId);

            sb.Append(m.ModuleId).Append(" | ")
              .Append(m.DefaultState).Append(" | ")
              .Append(shadow.State).Append(" | ")
              .Append(GetModuleDisposition(profileId, m)).Append(" | ")
              .AppendLine(m.Notes);
        }
    }

    private static string GetAdapterDisposition(string profileId, RebirthPatchAdapterDecl a)
    {
        if (a == null)
            return "unknown";

        if (a.InstallPolicy == RebirthPatchAdapterInstallPolicy.NeverInstall)
            return "excluded";

        if (profileId == "release")
        {
            if (a.InstallPolicy == RebirthPatchAdapterInstallPolicy.ProfilingBuildOnly)
                return "excluded";
            if (a.Status == RebirthPatchAdapterStatus.ExcludedByDefault)
                return "excluded";
            return "eligible-runtime-gate-or-normal-adapter";
        }

        if (profileId == "profiling")
        {
            if (a.Status == RebirthPatchAdapterStatus.ExcludedByDefault)
                return "excluded-by-default";
            return "eligible-for-profiling-dry-run";
        }

        if (profileId == "stripped")
        {
            if (a.IsHotPath)
                return "exclude-hot-structural-adapter-if-possible";
            return "manual-review";
        }

        if (profileId == "devdiagnostics")
            return "manual-targeted-diagnostics-only";

        if (profileId == "compataudit")
            return "report-only";

        return "manual-review";
    }

    private static string GetAdapterReason(string profileId, RebirthPatchAdapterDecl a)
    {
        if (a == null)
            return "missing adapter";

        if (a.InstallPolicy == RebirthPatchAdapterInstallPolicy.NeverInstall)
            return "install policy is NeverInstall";

        if (profileId == "release" && a.InstallPolicy == RebirthPatchAdapterInstallPolicy.ProfilingBuildOnly)
            return "profiling-only adapter forbidden in release";

        if (profileId == "stripped" && a.IsHotPath)
            return "stripped comparison excludes hot adapters where possible";

        return a.Notes;
    }

    private static string GetModuleDisposition(string profileId, RebirthModuleToggleDecl m)
    {
        if (m == null)
            return "unknown";

        if (profileId == "release")
        {
            if (m.ModuleId == "diagnostics.profiling")
                return "disabled/excluded";
            return "use contract default when behavior exists";
        }

        if (profileId == "profiling")
            return "eligible for profiling/sweep if migrated";

        if (profileId == "stripped")
        {
            if (m.IsHotPath)
                return "exclude or disable for comparison where possible";
            return "manual comparison policy";
        }

        if (profileId == "devdiagnostics")
            return "manual targeted diagnostics only";

        if (profileId == "compataudit")
            return "report-only";

        return "manual review";
    }

    private static RebirthBuildProfileSelectionState ParseProfile(string profileId)
    {
        string p = (profileId ?? string.Empty).Trim().ToLowerInvariant();

        if (p == "release")
            return RebirthBuildProfileSelectionState.Release;
        if (p == "profiling" || p == "profile")
            return RebirthBuildProfileSelectionState.Profiling;
        if (p == "stripped" || p == "strippedcomparison" || p == "strip")
            return RebirthBuildProfileSelectionState.StrippedComparison;
        if (p == "devdiagnostics" || p == "dev" || p == "diagnostics")
            return RebirthBuildProfileSelectionState.DeveloperDiagnostics;
        if (p == "compataudit" || p == "compat" || p == "audit")
            return RebirthBuildProfileSelectionState.CompatibilityAudit;

        return RebirthBuildProfileSelectionState.Unknown;
    }

    private static string NormalizeProfileId(RebirthBuildProfileSelectionState state)
    {
        switch (state)
        {
            case RebirthBuildProfileSelectionState.Release:
                return "release";
            case RebirthBuildProfileSelectionState.Profiling:
                return "profiling";
            case RebirthBuildProfileSelectionState.StrippedComparison:
                return "stripped";
            case RebirthBuildProfileSelectionState.DeveloperDiagnostics:
                return "devdiagnostics";
            case RebirthBuildProfileSelectionState.CompatibilityAudit:
                return "compataudit";
            case RebirthBuildProfileSelectionState.NoneSelected:
                return "none";
            default:
                return "unknown";
        }
    }
}
