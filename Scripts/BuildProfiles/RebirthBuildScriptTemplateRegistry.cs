using System;
using System.Text;

#nullable disable

public enum RebirthBuildScriptKind
{
    Unknown,
    Release,
    Profiling,
    StrippedComparison,
    DeveloperDiagnostics,
    CompatibilityAudit
}

public enum RebirthBuildScriptActionKind
{
    Unknown,
    Restore,
    Clean,
    Build,
    CopyOutput,
    Package,
    VerifyNoDiagnostics,
    VerifyProfilingSymbols,
    VerifyStrippedHotPatches,
    EmitManifest
}

public sealed class RebirthBuildScriptActionDecl
{
    public RebirthBuildScriptKind ScriptKind;
    public RebirthBuildScriptActionKind ActionKind;
    public int Order;
    public string ActionId;
    public string Description;
    public string Safety;
    public string Notes;
}

/// <summary>
/// Read-only build script template registry.
/// This does not create scripts, change csproj, change compiler symbols, or build anything.
/// </summary>
public static class RebirthBuildScriptTemplateRegistry
{
    private static readonly RebirthBuildScriptActionDecl[] s_actions = new[]
    {
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.Release,
            ActionKind = RebirthBuildScriptActionKind.Clean,
            Order = 10,
            ActionId = "release.clean",
            Description = "Clean previous release build artifacts.",
            Safety = "Template only.",
            Notes = "Future script should remove bin/obj/package output before build."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.Release,
            ActionKind = RebirthBuildScriptActionKind.Build,
            Order = 20,
            ActionId = "release.build",
            Description = "Build release profile without diagnostic wrappers.",
            Safety = "Template only.",
            Notes = "Future compiler symbols must exclude profiling-only diagnostics."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.Release,
            ActionKind = RebirthBuildScriptActionKind.VerifyNoDiagnostics,
            Order = 30,
            ActionId = "release.verify.noDiagnostics",
            Description = "Verify release build excludes always-patched diagnostic wrappers.",
            Safety = "Template only.",
            Notes = "Should fail future build if profiling-only adapter is compiled into release."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.Release,
            ActionKind = RebirthBuildScriptActionKind.Package,
            Order = 40,
            ActionId = "release.package",
            Description = "Package release mod without bin/obj/.vs.",
            Safety = "Template only.",
            Notes = "Final package must include project/source when requested, but no build artifacts unless explicitly requested."
        },

        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.Profiling,
            ActionKind = RebirthBuildScriptActionKind.Clean,
            Order = 10,
            ActionId = "profiling.clean",
            Description = "Clean previous profiling build artifacts.",
            Safety = "Template only.",
            Notes = "Keeps profiling output separate from release."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.Profiling,
            ActionKind = RebirthBuildScriptActionKind.Build,
            Order = 20,
            ActionId = "profiling.build",
            Description = "Build profiling profile with explicit profiling diagnostics.",
            Safety = "Template only.",
            Notes = "Future symbols may enable profiling adapters, but reports must clearly state overhead is intentional."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.Profiling,
            ActionKind = RebirthBuildScriptActionKind.VerifyProfilingSymbols,
            Order = 30,
            ActionId = "profiling.verify.symbols",
            Description = "Verify profiling symbols/manifest are present.",
            Safety = "Template only.",
            Notes = "Prevents accidentally comparing a release build as a profiling build."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.Profiling,
            ActionKind = RebirthBuildScriptActionKind.EmitManifest,
            Order = 40,
            ActionId = "profiling.emit.manifest",
            Description = "Emit profiling manifest with enabled diagnostics and patch adapters.",
            Safety = "Template only.",
            Notes = "Future comparison logs should include this manifest."
        },

        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.StrippedComparison,
            ActionKind = RebirthBuildScriptActionKind.Clean,
            Order = 10,
            ActionId = "stripped.clean",
            Description = "Clean previous stripped comparison artifacts.",
            Safety = "Template only.",
            Notes = "Keeps stripped output separate from release/profiling."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.StrippedComparison,
            ActionKind = RebirthBuildScriptActionKind.Build,
            Order = 20,
            ActionId = "stripped.build",
            Description = "Build stripped comparison profile with hot structural patches excluded where possible.",
            Safety = "Template only.",
            Notes = "Used to compare feature cost versus patch-install/adapter overhead."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.StrippedComparison,
            ActionKind = RebirthBuildScriptActionKind.VerifyStrippedHotPatches,
            Order = 30,
            ActionId = "stripped.verify.hotpatches",
            Description = "Verify hot structural adapters are excluded where possible.",
            Safety = "Template only.",
            Notes = "Future verification should compare against RebirthPatchAdapterPlanRegistry measurement candidates."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.StrippedComparison,
            ActionKind = RebirthBuildScriptActionKind.EmitManifest,
            Order = 40,
            ActionId = "stripped.emit.manifest",
            Description = "Emit stripped comparison manifest.",
            Safety = "Template only.",
            Notes = "Manifest should state exactly which adapters/features were excluded."
        },

        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.DeveloperDiagnostics,
            ActionKind = RebirthBuildScriptActionKind.Build,
            Order = 20,
            ActionId = "devdiagnostics.build",
            Description = "Build targeted developer diagnostics profile.",
            Safety = "Template only.",
            Notes = "Diagnostics must still be gated before construction."
        },
        new RebirthBuildScriptActionDecl
        {
            ScriptKind = RebirthBuildScriptKind.CompatibilityAudit,
            ActionKind = RebirthBuildScriptActionKind.EmitManifest,
            Order = 20,
            ActionId = "compataudit.emit.manifest",
            Description = "Emit compatibility audit manifest.",
            Safety = "Template only.",
            Notes = "For migration/external-project inspection, not runtime performance comparison."
        }
    };

    public static string GetTemplateReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthBuildScripts] read-only build script templates.");
        sb.AppendLine("  This does not create scripts, change csproj, change compiler symbols, or build anything.");
        sb.AppendLine("script | order | action | description | safety | notes");

        bool any = false;
        for (int i = 0; i < s_actions.Length; i++)
        {
            RebirthBuildScriptActionDecl a = s_actions[i];
            if (!Matches(a, f))
                continue;

            any = true;
            sb.Append(a.ScriptKind).Append(" | ")
              .Append(a.Order).Append(" | ")
              .Append(a.ActionId).Append(" | ")
              .Append(a.Description).Append(" | ")
              .Append(a.Safety).Append(" | ")
              .AppendLine(a.Notes);
        }

        if (!any)
            sb.AppendLine("No build script template action matched the filter.");

        return sb.ToString();
    }

    public static string GetProfileTemplateReport(string profile)
    {
        string f = (profile ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
        {
            RebirthBuildProfileSelection selected = RebirthBuildProfileSelectionRegistry.GetSelectionSnapshot();
            f = selected.ProfileId;
        }

        if (string.IsNullOrEmpty(f) || f == "none")
            return "[RebirthBuildScripts] No build profile selected. Use rbbuildfresh select release|profiling|stripped or pass a profile filter.";

        StringBuilder sb = new StringBuilder(16384);
        sb.Append("[RebirthBuildScripts] dry-run script template for profile: ").AppendLine(f);
        sb.AppendLine("  Template only. No script file is generated in this phase.");
        sb.AppendLine();
        sb.Append(RebirthBuildProfileSelectionRegistry.GetDryRunManifestReport(f));
        sb.AppendLine();
        sb.AppendLine("Script action sequence:");
        sb.Append(GetTemplateReport(f));
        return sb.ToString();
    }

    public static string GetFutureFileNameReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildScripts] future script filename plan.");
        sb.AppendLine("profile | future script | purpose");
        sb.AppendLine("release | build_release.bat | Release build without diagnostic wrappers.");
        sb.AppendLine("profiling | build_profiling.bat | Profiling build with explicit diagnostic/profile symbols.");
        sb.AppendLine("stripped | build_stripped_comparison.bat | Hot-patch/feature stripped comparison build.");
        sb.AppendLine("devdiagnostics | build_dev_diagnostics.bat | Targeted local diagnostics only.");
        sb.AppendLine("compataudit | build_compat_audit.bat | Migration/external compatibility audit manifest.");
        sb.AppendLine("No files are generated in Phase 1U.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthBuildScripts] safety rules:");
        sb.AppendLine("  1. This registry is read-only.");
        sb.AppendLine("  2. Phase 1U does not create .bat/.ps1 files.");
        sb.AppendLine("  3. Phase 1U does not change csproj.");
        sb.AppendLine("  4. Phase 1U does not change compiler symbols.");
        sb.AppendLine("  5. Phase 1U does not build or package output.");
        sb.AppendLine("  6. Future scripts must fail release builds containing profiling-only diagnostic wrappers.");
        sb.AppendLine("  7. Future scripts must keep release/profiling/stripped outputs separate.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthBuildScripts] Template actions: " + s_actions.Length
            + ". Build script registry is read-only and generates no files in this phase.";
    }

    private static bool Matches(RebirthBuildScriptActionDecl a, string filter)
    {
        if (a == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (a.ScriptKind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (a.ActionKind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (a.ActionId != null && a.ActionId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (a.Description != null && a.Description.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
