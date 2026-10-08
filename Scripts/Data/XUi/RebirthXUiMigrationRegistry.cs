using System;
using System.Text;

#nullable disable

public enum RebirthXUiMigrationStatus
{
    Unknown,
    SameModel,
    MovedFolder,
    SplitAcrossFolders,
    TemplateIntroduced,
    RemovedOrGutted,
    ManualReviewRequired
}

public sealed class RebirthXUiMigrationDecl
{
    public string Area;
    public string Source26;
    public string Target30;
    public RebirthXUiMigrationStatus Status;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// Read-only XUi migration ledger for 2.6 -> 3.0.
/// This does not generate XML and does not bind runtime controllers.
/// </summary>
public static class RebirthXUiMigrationRegistry
{
    private static readonly RebirthXUiMigrationDecl[] s_entries = new[]
    {
        new RebirthXUiMigrationDecl
        {
            Area = "Global XUi content model",
            Source26 = "Config/XUi",
            Target30 = "XUi_Common + XUi_InGame + XUi_Menu + templates.xml",
            Status = RebirthXUiMigrationStatus.SplitAcrossFolders,
            Evidence = "P6",
            Notes = "XUi migration is an authoring-model change, not just a windows.xml diff."
        },
        new RebirthXUiMigrationDecl
        {
            Area = "Templates",
            Source26 = "mostly inline windows/controls",
            Target30 = "templates.xml",
            Status = RebirthXUiMigrationStatus.TemplateIntroduced,
            Evidence = "P6",
            Notes = "Future rbxml xui-diff26to30 must identify template opportunities without generating XML."
        },
        new RebirthXUiMigrationDecl
        {
            Area = "World generation UI",
            Source26 = "XUiC_WorldGenerationWindowGroup",
            Target30 = "3.0 changed/gutted worldgen UI flow",
            Status = RebirthXUiMigrationStatus.RemovedOrGutted,
            Evidence = "P5",
            Notes = "TheDescent worldgen UI cannot be mechanically ported."
        },
        new RebirthXUiMigrationDecl
        {
            Area = "Workstation UI",
            Source26 = "XUiC_WorkstationWindowGroup cluster",
            Target30 = "manual review required",
            Status = RebirthXUiMigrationStatus.ManualReviewRequired,
            Evidence = "Open gap from ordered ledger",
            Notes = "Full body read of workstation/XUi cluster remains undone."
        },
        new RebirthXUiMigrationDecl
        {
            Area = "Crosshair UI",
            Source26 = "Morecrosshairs / CustomCrosshairHUD",
            Target30 = "native REBIRTH crosshair module",
            Status = RebirthXUiMigrationStatus.ManualReviewRequired,
            Evidence = "P4/P6",
            Notes = "Do not use GUIUtils.DrawLine global patch. Prefer crosshair-specific render ownership."
        }
    };

    public static RebirthXUiMigrationDecl[] GetSnapshot()
    {
        RebirthXUiMigrationDecl[] copy = new RebirthXUiMigrationDecl[s_entries.Length];
        for (int i=0;i<s_entries.Length;i++)
        {
            RebirthXUiMigrationDecl source=s_entries[i];
            copy[i]=source==null?null:new RebirthXUiMigrationDecl { Area = source.Area, Source26 = source.Source26, Target30 = source.Target30, Status = source.Status, Evidence = source.Evidence, Notes = source.Notes };
        }
        return copy;
    }

    public static string GetReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthXUi] read-only 2.6 -> 3.0 XUi migration ledger.");
        sb.AppendLine("  Rule: XUi XML remains hand-authored. C# does not generate windows.xml/xui.xml.");
        sb.AppendLine("area | 2.6 source | 3.0 target | status | evidence | notes");

        for (int i = 0; i < s_entries.Length; i++)
        {
            RebirthXUiMigrationDecl e = s_entries[i];
            sb.Append(e.Area).Append(" | ")
              .Append(e.Source26).Append(" | ")
              .Append(e.Target30).Append(" | ")
              .Append(e.Status).Append(" | ")
              .Append(e.Evidence).Append(" | ")
              .AppendLine(e.Notes);
        }

        return sb.ToString();
    }
}
