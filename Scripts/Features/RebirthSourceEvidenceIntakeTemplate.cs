using System;
using System.Text;

#nullable disable

public enum RebirthSourceEvidenceIntakeFieldStatus
{
    Unknown,
    EmptyTemplate,
    Required,
    Optional,
    FilledByFutureAudit,
    BlockedUntilSourceProvided
}

public enum RebirthSourceEvidenceIntakeFieldKind
{
    Unknown,
    SourcePackage,
    SourceFile,
    DeclaringType,
    MethodSignature,
    MethodBodyContext,
    AuthorityEvidence,
    SideEffectEvidence,
    PatchShape,
    TestMapping,
    RollbackPlan,
    ManualNotes
}

public sealed class RebirthSourceEvidenceIntakeField
{
    public string FieldId;
    public RebirthSourceEvidenceIntakeFieldKind Kind;
    public RebirthSourceEvidenceIntakeFieldStatus Status;
    public string Prompt;
    public string RequiredFormat;
    public string CurrentValue;
    public string Notes;
}

/// <summary>
/// Read-only source evidence intake template.
/// It is a blank structured form for the first real patch target.
/// This does not inspect files, choose a method, install Harmony, or migrate gameplay.
/// </summary>
public static class RebirthSourceEvidenceIntakeTemplate
{
    private static readonly RebirthSourceEvidenceIntakeField[] s_fields = new[]
    {
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.source.package",
            Kind = RebirthSourceEvidenceIntakeFieldKind.SourcePackage,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "Which source package/project was audited?",
            RequiredFormat = "Exact zip/project/source package name and version.",
            CurrentValue = "<pending actual source audit>",
            Notes = "Do not rely on memory."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.source.file",
            Kind = RebirthSourceEvidenceIntakeFieldKind.SourceFile,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "What exact source file contains the target?",
            RequiredFormat = "Relative file path plus nearby class/method context.",
            CurrentValue = "<pending actual source audit>",
            Notes = "Needed before Harmony target selection."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.declaring.type",
            Kind = RebirthSourceEvidenceIntakeFieldKind.DeclaringType,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "What exact declaring type owns the method?",
            RequiredFormat = "Full class/type name exactly as in source.",
            CurrentValue = "<pending actual source audit>",
            Notes = "Needed for AccessTools.Method or HarmonyPatch target."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.method.signature",
            Kind = RebirthSourceEvidenceIntakeFieldKind.MethodSignature,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "What exact method signature is targeted?",
            RequiredFormat = "ReturnType MethodName(ParameterType name, ...).",
            CurrentValue = "<pending actual source audit>",
            Notes = "No guessed method signatures."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.method.context",
            Kind = RebirthSourceEvidenceIntakeFieldKind.MethodBodyContext,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "What does the relevant method body do around the hook point?",
            RequiredFormat = "Short paraphrase of local context and hook point.",
            CurrentValue = "<pending actual source audit>",
            Notes = "Avoid long copied source; summarize only."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.authority",
            Kind = RebirthSourceEvidenceIntakeFieldKind.AuthorityEvidence,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "Does the method run server-side, client-side, or both?",
            RequiredFormat = "Server/client/both plus evidence and ownership decision.",
            CurrentValue = "<pending actual source audit>",
            Notes = "Harvest/salvage rewards must remain server-authoritative."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.side.effects",
            Kind = RebirthSourceEvidenceIntakeFieldKind.SideEffectEvidence,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "Which vanilla side effects must not change?",
            RequiredFormat = "Durability, XP, sound, heat, quest, block destruction, item output, etc.",
            CurrentValue = "<pending actual source audit>",
            Notes = "First slice must stay narrow."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.patch.shape",
            Kind = RebirthSourceEvidenceIntakeFieldKind.PatchShape,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "What minimal patch shape is allowed?",
            RequiredFormat = "No patch / Prefix / Postfix / Transpiler with typed parameter plan.",
            CurrentValue = "<pending actual source audit>",
            Notes = "object[] __args is not allowed for hot/warm patches."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.test.mapping",
            Kind = RebirthSourceEvidenceIntakeFieldKind.TestMapping,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "Which Phase 3H test IDs cover the target behavior?",
            RequiredFormat = "Comma-separated test IDs.",
            CurrentValue = "<pending actual source audit>",
            Notes = "No behavior without test gate mapping."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.rollback",
            Kind = RebirthSourceEvidenceIntakeFieldKind.RollbackPlan,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Required,
            Prompt = "How is this first slice rolled back?",
            RequiredFormat = "One flag and/or one patch adapter file.",
            CurrentValue = "<pending actual source audit>",
            Notes = "Rollback must be simple."
        },
        new RebirthSourceEvidenceIntakeField
        {
            FieldId = "intake.notes",
            Kind = RebirthSourceEvidenceIntakeFieldKind.ManualNotes,
            Status = RebirthSourceEvidenceIntakeFieldStatus.Optional,
            Prompt = "Any manual notes from the source audit?",
            RequiredFormat = "Short notes only.",
            CurrentValue = "<empty>",
            Notes = "Use for uncertainty or follow-up evidence."
        }
    };

    public static string GetSummaryReport()
    {
        int required = 0;
        int pending = 0;
        int optional = 0;

        for (int i = 0; i < s_fields.Length; i++)
        {
            RebirthSourceEvidenceIntakeField f = s_fields[i];

            if (f.Status == RebirthSourceEvidenceIntakeFieldStatus.Required)
                required++;

            if (f.CurrentValue != null && f.CurrentValue.IndexOf("<pending", StringComparison.OrdinalIgnoreCase) >= 0)
                pending++;

            if (f.Status == RebirthSourceEvidenceIntakeFieldStatus.Optional)
                optional++;
        }

        return "[RebirthSourceEvidenceIntake] Fields: " + s_fields.Length
            + "; required: " + required
            + "; pending: " + pending
            + "; optional: " + optional
            + ". Template is read-only.";
    }

    public static string GetTemplateReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthSourceEvidenceIntake] first patch source evidence intake template.");
        sb.AppendLine("  Read-only. Blank until an actual source audit fills it.");
        sb.AppendLine("id | kind | status | prompt | required format | current value | notes");

        bool any = false;
        for (int i = 0; i < s_fields.Length; i++)
        {
            RebirthSourceEvidenceIntakeField row = s_fields[i];
            if (!Matches(row, f))
                continue;

            any = true;
            sb.Append(row.FieldId).Append(" | ")
              .Append(row.Kind).Append(" | ")
              .Append(row.Status).Append(" | ")
              .Append(row.Prompt).Append(" | ")
              .Append(row.RequiredFormat).Append(" | ")
              .Append(row.CurrentValue).Append(" | ")
              .AppendLine(row.Notes);
        }

        if (!any)
            sb.AppendLine("No source evidence intake field matched the filter.");

        return sb.ToString();
    }

    public static string GetBlankMarkdownTemplate()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("# First Gameplay Slice Source Evidence");
        sb.AppendLine();
        sb.AppendLine("- Source package/version: ");
        sb.AppendLine("- Source file path: ");
        sb.AppendLine("- Declaring type: ");
        sb.AppendLine("- Method signature: ");
        sb.AppendLine("- Method body context: ");
        sb.AppendLine("- Authority evidence: ");
        sb.AppendLine("- Side effects that must not change: ");
        sb.AppendLine("- Minimal patch shape: ");
        sb.AppendLine("- Typed Harmony parameters: ");
        sb.AppendLine("- Phase 3H test IDs: ");
        sb.AppendLine("- Rollback plan: ");
        sb.AppendLine("- Manual notes/uncertainty: ");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Do not guess the target.");
        sb.AppendLine("- Do not use object[] __args in hot/warm patches.");
        sb.AppendLine("- Do not build diagnostics before gates.");
        sb.AppendLine("- Keep first slice to one behavior.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthSourceEvidenceIntake] safety:");
        sb.AppendLine("  1. Template is read-only.");
        sb.AppendLine("  2. No files inspected by this command.");
        sb.AppendLine("  3. No method target chosen.");
        sb.AppendLine("  4. No Harmony patches installed.");
        sb.AppendLine("  5. No gameplay behavior migrated.");
        sb.AppendLine("  6. No XML parsing.");
        sb.AppendLine("  7. No custom game option lookup.");
        sb.AppendLine("  8. No network packets.");
        sb.AppendLine("  9. No save/load writes.");
        return sb.ToString();
    }

    private static bool Matches(RebirthSourceEvidenceIntakeField row, string filter)
    {
        if (row == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (row.FieldId != null && row.FieldId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (row.Prompt != null && row.Prompt.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (row.RequiredFormat != null && row.RequiredFormat.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (row.CurrentValue != null && row.CurrentValue.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (row.Kind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (row.Status.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
