using System;
using System.Text;

#nullable disable

public enum RebirthCommandSmokeTestArea
{
    Unknown,
    Compile,
    Load,
    Architecture,
    Scenario,
    Diagnostics,
    BuildProfiles,
    RuntimeToggles,
    HarvestSalvage,
    Consolidation,
    FirstSlice,
    Evidence,
    Safety
}

public enum RebirthCommandSmokeTestPriority
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}

public sealed class RebirthCommandSmokeTestDecl
{
    public string TestId;
    public RebirthCommandSmokeTestArea Area;
    public RebirthCommandSmokeTestPriority Priority;
    public string Command;
    public string Expected;
    public string FailureMeaning;
    public string Notes;
}

/// <summary>
/// Read-only command smoke test matrix.
/// This does not execute commands. It prints what should be run manually after compile/load.
/// </summary>
public static class RebirthCommandSmokeTestMatrix
{
    private static readonly RebirthCommandSmokeTestDecl[] s_tests = new[]
    {
        Row("smoke.compile.vs", RebirthCommandSmokeTestArea.Compile, RebirthCommandSmokeTestPriority.Critical,
            "Visual Studio Rebuild",
            "0 errors and ideally 0 warnings from fresh project sources.",
            "Source/package regression.",
            "Run before loading game."),

        Row("smoke.load.game", RebirthCommandSmokeTestArea.Load, RebirthCommandSmokeTestPriority.Critical,
            "load world",
            "No command registration/load exceptions.",
            "Constructor/static init/command registration problem.",
            "Then run command checks."),

        Row("smoke.runtime.health", RebirthCommandSmokeTestArea.Architecture, RebirthCommandSmokeTestPriority.Critical,
            "rbruntimehealthfresh summary",
            "Status is Green and gameplay connected is false.",
            "Architecture shell is no longer inert or command failed.",
            "Primary aggregate check."),

        Row("smoke.nogo", RebirthCommandSmokeTestArea.FirstSlice, RebirthCommandSmokeTestPriority.Critical,
            "rbfirstslicenogofresh summary",
            "Decision is NoGo until source evidence is filled.",
            "Real patching may have become allowed without evidence.",
            "Intentional current state."),

        Row("smoke.source.intake", RebirthCommandSmokeTestArea.Evidence, RebirthCommandSmokeTestPriority.High,
            "rbsourceintakefresh summary",
            "Required fields are pending actual source audit.",
            "Evidence template command failed or was accidentally filled without audit.",
            "Should remain pending right now."),

        Row("smoke.patch.evidence", RebirthCommandSmokeTestArea.Evidence, RebirthCommandSmokeTestPriority.High,
            "rbpatchevidencefresh required",
            "Required patch target evidence prints.",
            "Patch evidence ledger broken.",
            "No actual patch target chosen."),

        Row("smoke.firstslice.audit", RebirthCommandSmokeTestArea.FirstSlice, RebirthCommandSmokeTestPriority.High,
            "rbfirstsliceauditfresh blockers",
            "Blockers print source evidence, target, authority, diagnostics, tests.",
            "Audit checklist broken or missing blockers.",
            "Keeps first slice gated."),

        Row("smoke.harvest.gate", RebirthCommandSmokeTestArea.HarvestSalvage, RebirthCommandSmokeTestPriority.High,
            "rbharvestgatefresh summary",
            "Harvest/salvage test count prints.",
            "Test gate registry broken.",
            "Read-only tests."),

        Row("smoke.harvest.policy", RebirthCommandSmokeTestArea.HarvestSalvage, RebirthCommandSmokeTestPriority.High,
            "rbharvestpolicyfresh summary",
            "Harvest/salvage disabled, runtime consumer false, hot-path false.",
            "Policy no longer safely disabled.",
            "Must remain inert."),

        Row("smoke.harvest.adapter", RebirthCommandSmokeTestArea.HarvestSalvage, RebirthCommandSmokeTestPriority.High,
            "rbharvestadapterfresh preview 10",
            "Harvest and salvage final amounts both remain 10.",
            "No-op adapter changed behavior.",
            "Adapter must stay no-op while no-go gate blocks."),

        Row("smoke.diag.gates", RebirthCommandSmokeTestArea.Diagnostics, RebirthCommandSmokeTestPriority.High,
            "rbdiaggatesfresh summary",
            "All diagnostic gates false.",
            "Diagnostics may be enabled unexpectedly.",
            "Release-safe default."),

        Row("smoke.hot.flags", RebirthCommandSmokeTestArea.RuntimeToggles, RebirthCommandSmokeTestPriority.High,
            "rbhotflagsfresh summary",
            "All gameplay flags false.",
            "A feature flag was enabled unexpectedly.",
            "No gameplay consumers yet."),

        Row("smoke.runtime.toggles", RebirthCommandSmokeTestArea.RuntimeToggles, RebirthCommandSmokeTestPriority.Medium,
            "rbruntimetogglesfresh summary",
            "Runtime consumers disabled and hot-path use disabled.",
            "Runtime toggle shell drifted.",
            "Still inert."),

        Row("smoke.scenario.policy", RebirthCommandSmokeTestArea.Scenario, RebirthCommandSmokeTestPriority.Medium,
            "rbruntimepolicyfresh summary",
            "Explicit scenario defaults only.",
            "Scenario policy shell drifted.",
            "No gameplay consumer."),

        Row("smoke.profile.compare", RebirthCommandSmokeTestArea.BuildProfiles, RebirthCommandSmokeTestPriority.Medium,
            "rbprofilecomparefresh summary",
            "Dry-run build/profile comparison prints.",
            "Build profile report broken.",
            "Does not build or alter symbols."),

        Row("smoke.consolidation", RebirthCommandSmokeTestArea.Consolidation, RebirthCommandSmokeTestPriority.High,
            "rbconsolidationfresh target",
            "Single project/package target prints and no Core/Utils split target.",
            "Consolidation rule missing.",
            "Matches user goal."),

        Row("smoke.safety.aggregate", RebirthCommandSmokeTestArea.Safety, RebirthCommandSmokeTestPriority.Critical,
            "rbsysfresh runtimehealthfull",
            "No gameplay connected, no Harmony patches installed by fresh migrated systems, no XML parsing.",
            "Fresh system became active unexpectedly.",
            "Use after any new phase.")
    };

    public static string GetSummaryReport()
    {
        int critical = 0;
        int high = 0;

        for (int i = 0; i < s_tests.Length; i++)
        {
            if (s_tests[i].Priority == RebirthCommandSmokeTestPriority.Critical)
                critical++;

            if (s_tests[i].Priority == RebirthCommandSmokeTestPriority.High)
                high++;
        }

        return "[RebirthCommandSmokeTests] Tests: " + s_tests.Length
            + "; critical: " + critical
            + "; high: " + high
            + ". Matrix is read-only.";
    }

    public static string GetMatrixReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(20000);
        sb.AppendLine("[RebirthCommandSmokeTests] command smoke test matrix.");
        sb.AppendLine("  Read-only. This command does not execute other commands.");
        sb.AppendLine("id | area | priority | command | expected | failure meaning | notes");

        bool any = false;
        for (int i = 0; i < s_tests.Length; i++)
        {
            RebirthCommandSmokeTestDecl t = s_tests[i];
            if (!Matches(t, f))
                continue;

            any = true;
            sb.Append(t.TestId).Append(" | ")
              .Append(t.Area).Append(" | ")
              .Append(t.Priority).Append(" | ")
              .Append(t.Command).Append(" | ")
              .Append(t.Expected).Append(" | ")
              .Append(t.FailureMeaning).Append(" | ")
              .AppendLine(t.Notes);
        }

        if (!any)
            sb.AppendLine("No smoke test row matched the filter.");

        return sb.ToString();
    }

    public static string GetCriticalReport()
    {
        return GetMatrixReport("Critical");
    }

    public static string GetRecommendedOrderReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthCommandSmokeTests] recommended manual order:");
        sb.AppendLine("  1. Visual Studio Rebuild");
        sb.AppendLine("  2. Load world");
        sb.AppendLine("  3. rbruntimehealthfresh summary");
        sb.AppendLine("  4. rbfirstslicenogofresh summary");
        sb.AppendLine("  5. rbsourceintakefresh summary");
        sb.AppendLine("  6. rbharvestpolicyfresh summary");
        sb.AppendLine("  7. rbharvestadapterfresh preview 10");
        sb.AppendLine("  8. rbhotflagsfresh summary");
        sb.AppendLine("  9. rbdiaggatesfresh summary");
        sb.AppendLine("  10. rbconsolidationfresh target");
        sb.AppendLine();
        sb.AppendLine("Expected current state:");
        sb.AppendLine("  project compiles, commands load, gameplay remains disconnected, first slice remains NoGo.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthCommandSmokeTests] safety:");
        sb.AppendLine("  1. Matrix is read-only.");
        sb.AppendLine("  2. It does not execute commands.");
        sb.AppendLine("  3. It does not inspect files.");
        sb.AppendLine("  4. It does not choose method targets.");
        sb.AppendLine("  5. It does not install Harmony patches.");
        sb.AppendLine("  6. It does not migrate gameplay.");
        sb.AppendLine("  7. It does not parse XML or options.");
        return sb.ToString();
    }

    private static RebirthCommandSmokeTestDecl Row(
        string id,
        RebirthCommandSmokeTestArea area,
        RebirthCommandSmokeTestPriority priority,
        string command,
        string expected,
        string failureMeaning,
        string notes)
    {
        return new RebirthCommandSmokeTestDecl
        {
            TestId = id,
            Area = area,
            Priority = priority,
            Command = command,
            Expected = expected,
            FailureMeaning = failureMeaning,
            Notes = notes
        };
    }

    private static bool Matches(RebirthCommandSmokeTestDecl t, string filter)
    {
        if (t == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (t.TestId != null && t.TestId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Command != null && t.Command.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Expected != null && t.Expected.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.FailureMeaning != null && t.FailureMeaning.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Area.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Priority.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
