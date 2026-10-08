using System;
using System.Text;

#nullable disable

public enum RebirthHarvestSalvageTestArea
{
    Unknown,
    HarvestBonus,
    SalvageBonus,
    VehicleInteraction,
    SoundStealth,
    ServerParity,
    Regression,
    Performance
}

public enum RebirthHarvestSalvageTestPriority
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}

public enum RebirthHarvestSalvageMigrationReadiness
{
    Unknown,
    RequiredBeforeMigration,
    RequiredBeforePatch,
    RequiredBeforeServerTest,
    OptionalAfterMigration
}

public sealed class RebirthHarvestSalvageTestGateDecl
{
    public string TestId;
    public RebirthHarvestSalvageTestArea Area;
    public RebirthHarvestSalvageTestPriority Priority;
    public RebirthHarvestSalvageMigrationReadiness Readiness;
    public string Environment;
    public string Steps;
    public string Expected;
    public string MustNotRegress;
    public string Notes;
}

/// <summary>
/// Read-only harvest/salvage test gate.
/// This defines tests before the first low-risk gameplay migration slice.
/// It does not patch behavior or migrate gameplay.
/// </summary>
public static class RebirthHarvestSalvageTestGateRegistry
{
    private static readonly RebirthHarvestSalvageTestGateDecl[] s_tests = new[]
    {
        new RebirthHarvestSalvageTestGateDecl
        {
            TestId = "harvest.test.compile.load",
            Area = RebirthHarvestSalvageTestArea.Regression,
            Priority = RebirthHarvestSalvageTestPriority.Critical,
            Readiness = RebirthHarvestSalvageMigrationReadiness.RequiredBeforeMigration,
            Environment = "Visual Studio + game load",
            Steps = "Compile, load world, run rbsysfresh runtimehealth and harvest gate commands.",
            Expected = "No compile warnings/errors from new harvest test gate code; commands print reports.",
            MustNotRegress = "architecture shell remains inert",
            Notes = "Baseline for all later harvest/salvage work."
        },
        new RebirthHarvestSalvageTestGateDecl
        {
            TestId = "harvest.test.wild.grown",
            Area = RebirthHarvestSalvageTestArea.HarvestBonus,
            Priority = RebirthHarvestSalvageTestPriority.Critical,
            Readiness = RebirthHarvestSalvageMigrationReadiness.RequiredBeforeMigration,
            Environment = "SP and dedicated server",
            Steps = "Compare harvesting wild crop/resource blocks and grown/placed equivalents with and without the future feature enabled.",
            Expected = "Future bonuses apply only to intended wild/grown categories according to feature contract.",
            MustNotRegress = "gloves wild/grown behavior and no double-apply",
            Notes = "Captured because prior harvest behavior had category-specific requirements."
        },
        new RebirthHarvestSalvageTestGateDecl
        {
            TestId = "salvage.test.per.block",
            Area = RebirthHarvestSalvageTestArea.SalvageBonus,
            Priority = RebirthHarvestSalvageTestPriority.Critical,
            Readiness = RebirthHarvestSalvageMigrationReadiness.RequiredBeforeMigration,
            Environment = "SP and dedicated server",
            Steps = "Salvage multiple valid blocks and compare expected per-block reward modification.",
            Expected = "Future salvage bonus applies per block and rounds down as intended.",
            MustNotRegress = "round-down behavior and no double-apply",
            Notes = "Per-block and rounding rules must be verified before migration."
        },
        new RebirthHarvestSalvageTestGateDecl
        {
            TestId = "salvage.test.vehicle.stump",
            Area = RebirthHarvestSalvageTestArea.VehicleInteraction,
            Priority = RebirthHarvestSalvageTestPriority.High,
            Readiness = RebirthHarvestSalvageMigrationReadiness.RequiredBeforeMigration,
            Environment = "SP and dedicated server",
            Steps = "Use vehicle impact/destruction cases such as tree stump honey interaction.",
            Expected = "Vehicle destruction does not trigger unintended harvest/salvage rewards.",
            MustNotRegress = "tree stump honey via vehicle remains disabled",
            Notes = "Prevents reintroducing vehicle harvest reward bugs."
        },
        new RebirthHarvestSalvageTestGateDecl
        {
            TestId = "salvage.test.stealth.sound",
            Area = RebirthHarvestSalvageTestArea.SoundStealth,
            Priority = RebirthHarvestSalvageTestPriority.High,
            Readiness = RebirthHarvestSalvageMigrationReadiness.RequiredBeforeMigration,
            Environment = "SP and dedicated server",
            Steps = "Use wrench/ratchet/salvage tool stealth cases, reset to menu/world, repeat.",
            Expected = "Future salvage sound changes do not permanently quiet or amplify tools after reset.",
            MustNotRegress = "stealth salvage sound reset behavior",
            Notes = "Sound interaction may be a separate migration slice, but tests belong here."
        },
        new RebirthHarvestSalvageTestGateDecl
        {
            TestId = "salvage.test.server.parity",
            Area = RebirthHarvestSalvageTestArea.ServerParity,
            Priority = RebirthHarvestSalvageTestPriority.Critical,
            Readiness = RebirthHarvestSalvageMigrationReadiness.RequiredBeforeMigration,
            Environment = "SP, dedicated server, P2P if available",
            Steps = "Compare harvested/salvaged outputs and XP/item rewards between SP and server modes.",
            Expected = "Server-authoritative reward truth matches displayed output.",
            MustNotRegress = "client does not decide authoritative rewards",
            Notes = "Prevents client-only or SP-only reward behavior."
        },
        new RebirthHarvestSalvageTestGateDecl
        {
            TestId = "salvage.test.performance",
            Area = RebirthHarvestSalvageTestArea.Performance,
            Priority = RebirthHarvestSalvageTestPriority.High,
            Readiness = RebirthHarvestSalvageMigrationReadiness.RequiredBeforePatch,
            Environment = "Release/profiling comparison",
            Steps = "Exercise repeated harvesting/salvaging while logging frame feel and checking no diagnostic construction in disabled mode.",
            Expected = "No per-hit allocations from diagnostics, no reflection, no repeated broad scans.",
            MustNotRegress = "debug gates before construction",
            Notes = "Even low-risk gameplay must follow hot-path-safe diagnostic rules."
        }
    };

    public static string GetSummaryReport()
    {
        int critical = 0;
        int server = 0;
        int perf = 0;

        for (int i = 0; i < s_tests.Length; i++)
        {
            RebirthHarvestSalvageTestGateDecl t = s_tests[i];

            if (t.Priority == RebirthHarvestSalvageTestPriority.Critical)
                critical++;

            if (t.Area == RebirthHarvestSalvageTestArea.ServerParity)
                server++;

            if (t.Area == RebirthHarvestSalvageTestArea.Performance)
                perf++;
        }

        return "[RebirthHarvestSalvageGate] Tests: " + s_tests.Length
            + "; critical: " + critical
            + "; server parity: " + server
            + "; performance: " + perf
            + ". Gate is read-only.";
    }

    public static string GetTestReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthHarvestSalvageGate] harvest/salvage test gate.");
        sb.AppendLine("  Read-only. This does not migrate gameplay.");
        sb.AppendLine("id | area | priority | readiness | environment | steps | expected | must not regress | notes");

        bool any = false;
        for (int i = 0; i < s_tests.Length; i++)
        {
            RebirthHarvestSalvageTestGateDecl t = s_tests[i];
            if (!Matches(t, f))
                continue;

            any = true;
            sb.Append(t.TestId).Append(" | ")
              .Append(t.Area).Append(" | ")
              .Append(t.Priority).Append(" | ")
              .Append(t.Readiness).Append(" | ")
              .Append(t.Environment).Append(" | ")
              .Append(t.Steps).Append(" | ")
              .Append(t.Expected).Append(" | ")
              .Append(t.MustNotRegress).Append(" | ")
              .AppendLine(t.Notes);
        }

        if (!any)
            sb.AppendLine("No harvest/salvage test matched the filter.");

        return sb.ToString();
    }

    public static string GetCriticalReport()
    {
        return GetTestReport("Critical");
    }

    public static string GetServerReport()
    {
        return GetTestReport("ServerParity");
    }

    public static string GetPerformanceReport()
    {
        return GetTestReport("Performance");
    }

    public static string GetMigrationReadinessReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthHarvestSalvageGate] migration readiness:");
        sb.AppendLine("  This is a candidate first low-risk gameplay slice only after tests exist.");
        sb.AppendLine("  First real implementation should be one isolated behavior.");
        sb.AppendLine("  Do not combine harvest, salvage, sound, vehicle interactions, XP, and UI all at once.");
        sb.AppendLine();
        sb.AppendLine("Recommended first behavior after this gate compiles:");
        sb.AppendLine("  1. pick one tiny server-authoritative reward modifier");
        sb.AppendLine("  2. add no diagnostics unless gated before construction");
        sb.AppendLine("  3. avoid Harmony unless the exact target and side effects are documented");
        sb.AppendLine("  4. keep feature flag false until manual test is ready");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvageGate] safety:");
        sb.AppendLine("  1. Gate is read-only.");
        sb.AppendLine("  2. No gameplay behavior is migrated.");
        sb.AppendLine("  3. No Harmony patches are installed.");
        sb.AppendLine("  4. No XML parsing occurs.");
        sb.AppendLine("  5. No custom game option lookup occurs.");
        sb.AppendLine("  6. No network packets are sent.");
        sb.AppendLine("  7. No save/load writes occur.");
        return sb.ToString();
    }

    private static bool Matches(RebirthHarvestSalvageTestGateDecl t, string filter)
    {
        if (t == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (t.TestId != null && t.TestId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Environment != null && t.Environment.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Steps != null && t.Steps.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Expected != null && t.Expected.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.MustNotRegress != null && t.MustNotRegress.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Area.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Priority.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Readiness.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
