using System;
using System.Text;

#nullable disable

public enum RebirthSourceAuditWorkspaceStatus
{
    Unknown,
    ReadyForFiles,
    MissingRequiredFiles,
    EvidencePending,
    NoGo,
    GoAfterManualAudit
}

public enum RebirthSourceAuditWorkspaceFileRole
{
    Unknown,
    LatestFreshProject,
    BaseGameSource,
    OldRebirthSource,
    MergedConfigDump,
    OptionalLogs,
    OptionalFeatureSpecific
}

public sealed class RebirthSourceAuditRequiredFile
{
    public string FileId;
    public RebirthSourceAuditWorkspaceFileRole Role;
    public bool RequiredForGameplayPatch;
    public string PreferredName;
    public string WhyNeeded;
    public string WhenRequired;
}

/// <summary>
/// Read-only source audit workspace for Milestone 4A.
/// This does not inspect uploaded files, choose method targets, install Harmony, or migrate gameplay.
/// </summary>
public static class RebirthSourceAuditWorkspaceRegistry
{
    private static readonly RebirthSourceAuditRequiredFile[] s_files = new[]
    {
        Row("file.latest.fresh.project", RebirthSourceAuditWorkspaceFileRole.LatestFreshProject, true,
            "zzz_REBIRTH__Fresh_Phase3R_HANDOFF_BLUEPRINT_FIX_VSProject.zip or newer",
            "Base project to continue from.",
            "Always required."),

        Row("file.base.2_6.source", RebirthSourceAuditWorkspaceFileRole.BaseGameSource, true,
            "_2.6 exp b10.tar.gz or current 2.6 base source/decompiled source package",
            "Needed to identify exact vanilla/base method targets and signatures.",
            "Required before any real Harmony patch."),

        Row("file.old.rebirth.source", RebirthSourceAuditWorkspaceFileRole.OldRebirthSource, true,
            "zzz_REBIRTH__Utils v2.6.zip or latest old/current REBIRTH 2.6 source package",
            "Needed to compare intended old behavior without copying old tangled boundaries.",
            "Required before porting old behavior."),

        Row("file.config.dump", RebirthSourceAuditWorkspaceFileRole.MergedConfigDump, false,
            "ConfigsDump(50).zip, Config(9).zip, or current merged Config dump",
            "Needed when behavior depends on XML items, buffs, loot, quests, progression, entityclasses, blocks, or tags.",
            "Required if the chosen slice touches XML-defined behavior."),

        Row("file.logs.optional", RebirthSourceAuditWorkspaceFileRole.OptionalLogs, false,
            "Player*.zip and output_log_client/output_log_dedi logs",
            "Useful only when reproducing or validating a specific old bug.",
            "Optional unless a bug reproduction is being audited."),

        Row("file.feature.specific.optional", RebirthSourceAuditWorkspaceFileRole.OptionalFeatureSpecific, false,
            "Feature-specific files such as RebirthPlayerCrawlController.cs, items(41).xml, loot.xml variants",
            "Needed only for that exact feature area.",
            "Optional unless the next milestone targets that file/feature.")
    };

    public static RebirthSourceAuditWorkspaceStatus CurrentStatus
    {
        get { return RebirthSourceAuditWorkspaceStatus.MissingRequiredFiles; }
    }

    public static string GetSummaryReport()
    {
        int required = 0;
        int optional = 0;

        for (int i = 0; i < s_files.Length; i++)
        {
            if (s_files[i].RequiredForGameplayPatch)
                required++;
            else
                optional++;
        }

        return "[RebirthSourceAuditWorkspace] Status: " + CurrentStatus
            + "; required file roles: " + required
            + "; optional file roles: " + optional
            + "; real patching remains NoGo.";
    }

    public static string GetRequiredFilesReport()
    {
        return GetFilesReport("true");
    }

    public static string GetFilesReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(12000);
        sb.AppendLine("[RebirthSourceAuditWorkspace] required/recommended files for Milestone 4A.");
        sb.AppendLine("id | role | required for gameplay patch | preferred name | why needed | when required");

        bool any = false;
        for (int i = 0; i < s_files.Length; i++)
        {
            RebirthSourceAuditRequiredFile row = s_files[i];
            if (!Matches(row, f))
                continue;

            any = true;
            sb.Append(row.FileId).Append(" | ")
              .Append(row.Role).Append(" | ")
              .Append(row.RequiredForGameplayPatch).Append(" | ")
              .Append(row.PreferredName).Append(" | ")
              .Append(row.WhyNeeded).Append(" | ")
              .AppendLine(row.WhenRequired);
        }

        if (!any)
            sb.AppendLine("No source-audit file row matched the filter.");

        return sb.ToString();
    }

    public static string GetNoGoReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthSourceAuditWorkspace] current NoGo reasons:");
        sb.AppendLine("  - Base 2.6 source/decompiled source is not available in this package.");
        sb.AppendLine("  - Old/current REBIRTH 2.6 source is not available in this package.");
        sb.AppendLine("  - Exact source file, declaring type, method signature, and authority evidence are not filled.");
        sb.AppendLine("  - Therefore no gameplay Harmony patch may be created yet.");
        return sb.ToString();
    }

    public static string GetNextReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthSourceAuditWorkspace] next milestone after files are supplied:");
        sb.AppendLine("  Milestone 4B — Actual Source Audit + Target Selection");
        sb.AppendLine();
        sb.AppendLine("  In one package:");
        sb.AppendLine("    1. Inspect base 2.6 source for exact harvest/salvage method targets.");
        sb.AppendLine("    2. Inspect old REBIRTH source for intended behavior.");
        sb.AppendLine("    3. Fill FIRST_GAMEPLAY_SLICE_SOURCE_EVIDENCE_TEMPLATE.md.");
        sb.AppendLine("    4. Choose one behavior only if evidence supports it.");
        sb.AppendLine("    5. Add target-specific patch contract.");
        sb.AppendLine("    6. Keep behavior disabled/no-op unless all gates pass.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthSourceAuditWorkspace] safety:");
        sb.AppendLine("  1. Workspace is read-only.");
        sb.AppendLine("  2. No uploaded files are inspected by this command.");
        sb.AppendLine("  3. No method target is chosen.");
        sb.AppendLine("  4. No Harmony patches are installed.");
        sb.AppendLine("  5. No gameplay behavior is migrated.");
        sb.AppendLine("  6. No XML parsing occurs.");
        sb.AppendLine("  7. No custom game option lookup occurs.");
        return sb.ToString();
    }

    private static RebirthSourceAuditRequiredFile Row(
        string id,
        RebirthSourceAuditWorkspaceFileRole role,
        bool required,
        string preferred,
        string why,
        string when)
    {
        return new RebirthSourceAuditRequiredFile
        {
            FileId = id,
            Role = role,
            RequiredForGameplayPatch = required,
            PreferredName = preferred,
            WhyNeeded = why,
            WhenRequired = when
        };
    }

    private static bool Matches(RebirthSourceAuditRequiredFile row, string filter)
    {
        if (row == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (filter.Equals("true", StringComparison.OrdinalIgnoreCase))
            return row.RequiredForGameplayPatch;

        if (filter.Equals("false", StringComparison.OrdinalIgnoreCase))
            return !row.RequiredForGameplayPatch;

        if (row.FileId != null && row.FileId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (row.PreferredName != null && row.PreferredName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (row.WhyNeeded != null && row.WhyNeeded.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (row.Role.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
