using System;
using System.Text;

#nullable disable

public enum RebirthFeatureTestCategory
{
    Unknown,
    CompileLoad,
    SinglePlayer,
    DedicatedServer,
    PeerToPeer,
    NetworkAuthority,
    Persistence,
    HotPathPerformance,
    ScenarioMatrix,
    XmlLoadOrder,
    UiDisplay,
    AudioSync,
    ExternalCompatibility,
    Regression
}

public enum RebirthFeatureTestPriority
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public enum RebirthFeatureTestReadiness
{
    Unknown,
    Planned,
    RequiredBeforeMigration,
    RequiredBeforeHotPathMigration,
    RequiredBeforeStatefulMigration,
    BlockedByDisambiguation,
    FutureAutomation
}

public sealed class RebirthFeatureTestCaseDecl
{
    public string TestId;
    public string DomainId;
    public RebirthFeatureTestCategory Category;
    public RebirthFeatureTestPriority Priority;
    public RebirthFeatureTestReadiness Readiness;
    public string ScenarioSet;
    public string Environment;
    public string Steps;
    public string Expected;
    public string Notes;
}

/// <summary>
/// Read-only feature test matrix.
/// This does not execute tests. It defines gates required before migration.
/// </summary>
public static class RebirthFeatureTestMatrixRegistry
{
    private static readonly RebirthFeatureTestCaseDecl[] s_tests = new[]
    {
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.compile.load",
            DomainId = "architecture.readiness",
            Category = RebirthFeatureTestCategory.CompileLoad,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeMigration,
            ScenarioSet = "all",
            Environment = "Visual Studio + game load",
            Steps = "Compile, install mod, load main menu/world, run rbsysfresh help/summary commands.",
            Expected = "No compile errors, no load errors, commands visible.",
            Notes = "Baseline gate for every package."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.xml.root.includes",
            DomainId = "xml.cleanup.reorganization",
            Category = RebirthFeatureTestCategory.XmlLoadOrder,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeMigration,
            ScenarioSet = "all",
            Environment = "SP and dedicated server",
            Steps = "Validate root Config XML files remain present and include subfolder fragments in documented order.",
            Expected = "Game/mod loader sees root Config entries; subfolder fragments load through include calls.",
            Notes = "Required before any physical XML reorganization."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.scenario.none.baseline",
            DomainId = "scenario.core",
            Category = RebirthFeatureTestCategory.ScenarioMatrix,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeMigration,
            ScenarioSet = "none",
            Environment = "SP/dedi/P2P",
            Steps = "Run baseline world with no scenario behavior enabled.",
            Expected = "No purge/hive/survivor-specific behavior leaks into baseline.",
            Notes = "Baseline is required before attributing costs/regressions to scenarios."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.scenario.purge",
            DomainId = "scenario.core",
            Category = RebirthFeatureTestCategory.ScenarioMatrix,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeMigration,
            ScenarioSet = "purge",
            Environment = "SP/dedi/P2P",
            Steps = "Trigger POI discovery/progress/display/markers/supply crate paths.",
            Expected = "Server owns progress; client receives display snapshots; no marker flicker.",
            Notes = "Purge is event/display/network sensitive."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.scenario.hive",
            DomainId = "scenario.core",
            Category = RebirthFeatureTestCategory.ScenarioMatrix,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeMigration,
            ScenarioSet = "hive",
            Environment = "SP/dedi",
            Steps = "Test cave tunnel, cave POI, underground POI, blood moon/cave spawn conditions.",
            Expected = "Cave tunnel helper and cave POI tag checks remain distinct.",
            Notes = "Hive policy depends on cave semantic separation."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.scenario.survivor.blocked",
            DomainId = "scenario.core",
            Category = RebirthFeatureTestCategory.ScenarioMatrix,
            Priority = RebirthFeatureTestPriority.Blocked,
            Readiness = RebirthFeatureTestReadiness.BlockedByDisambiguation,
            ScenarioSet = "survivor",
            Environment = "manual audit",
            Steps = "Classify survivor token references as scenario, NPC/entity role, tag, or unrelated.",
            Expected = "No survivor scenario migration by keyword.",
            Notes = "Blocked until disambiguation is complete."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.hotpath.no.alloc",
            DomainId = "debug.diagnostics",
            Category = RebirthFeatureTestCategory.HotPathPerformance,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeHotPathMigration,
            ScenarioSet = "all",
            Environment = "release/profiling/stripped",
            Steps = "Exercise hot paths with debug disabled and compare release vs stripped/profiling builds.",
            Expected = "No debug string construction, LINQ, reflection, stack trace, object[] args, or avoidable allocations in release hot paths.",
            Notes = "Required before PlayerMoveController, DamageEntity, ItemValue, renderFace, AI hot-path migration."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.network.authority.matrix",
            DomainId = "network.authority",
            Category = RebirthFeatureTestCategory.NetworkAuthority,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeStatefulMigration,
            ScenarioSet = "all stateful features",
            Environment = "SP/dedi/P2P",
            Steps = "Verify which side owns truth, which side displays state, and how corrections sync.",
            Expected = "No client-owned server truth; no dedicated-server-only regressions.",
            Notes = "Required before loot, NPCs, items rolls, traders, spawning, quests."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.persistence.matrix",
            DomainId = "persistence.saveLoad",
            Category = RebirthFeatureTestCategory.Persistence,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeStatefulMigration,
            ScenarioSet = "all stateful features",
            Environment = "SP/dedi",
            Steps = "Test relog, chunk unload/reload, server restart, new game, different world where relevant.",
            Expected = "Stateful features persist only intended data and clean up destroyed/invalid state.",
            Notes = "Required before reserved loot, markers, NPCs, rolled values, paint, traders."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.ui.cached.display",
            DomainId = "ui.hud.compass",
            Category = RebirthFeatureTestCategory.UiDisplay,
            Priority = RebirthFeatureTestPriority.High,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeMigration,
            ScenarioSet = "purge/common",
            Environment = "client/SP",
            Steps = "Idle in world, move, drive, trigger purge display/marker changes.",
            Expected = "HUD/compass consume cached display state; no scenario progress/POI scans in UI updates.",
            Notes = "Protects compass smoothness and HUD idle cost."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.player.crawl.regression",
            DomainId = "player.crawl",
            Category = RebirthFeatureTestCategory.Regression,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeHotPathMigration,
            ScenarioSet = "common/hive interactions",
            Environment = "SP/dedi",
            Steps = "Thin plates, window blocks, corner blocks, vehicle/water, reload while crawling, third person, side/back entry.",
            Expected = "Only valid crawl spaces trigger; no vehicle/water camera regression; no reload stuck state.",
            Notes = "Do not migrate without narrow behavior tests."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.paint.persistence.render",
            DomainId = "paint.render",
            Category = RebirthFeatureTestCategory.HotPathPerformance,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeHotPathMigration,
            ScenarioSet = "common",
            Environment = "SP/dedi",
            Steps = "Paint blocks remotely/locally, reload chunks, watch render hot path.",
            Expected = "Paint persists and renderFace path avoids per-face overhead.",
            Notes = "Single render owner required."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.items.rolls",
            DomainId = "items.stats.randomization",
            Category = RebirthFeatureTestCategory.Persistence,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeHotPathMigration,
            ScenarioSet = "common",
            Environment = "SP/dedi/P2P",
            Steps = "Create quality rolls, compare ranges, relog/restart, inspect tooltip/server sync.",
            Expected = "Rolled values persist; lower quality max does not exceed higher quality low; display matches server.",
            Notes = "Required before armor/vehicle part stat migration."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.map.loot.markers",
            DomainId = "map.markers.reservations + loot.persistence",
            Category = RebirthFeatureTestCategory.Persistence,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeStatefulMigration,
            ScenarioSet = "purge/common",
            Environment = "SP/dedi",
            Steps = "Spawn loot/crate, relog, server restart, destroy, verify owner/non-owner and marker cleanup.",
            Expected = "No stale/flickering markers; ownership persists; cleanup is event-based.",
            Notes = "Markers and loot persistence migrate together."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.spawning.caves.events",
            DomainId = "spawning.events",
            Category = RebirthFeatureTestCategory.DedicatedServer,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeStatefulMigration,
            ScenarioSet = "purge/hive/common",
            Environment = "SP/dedi/P2P",
            Steps = "Sleepers, entitygroups, event spawns, blood moon, cave/POI exclusions.",
            Expected = "Server owns spawn execution; scenarios/world/quests provide cached policy.",
            Notes = "No scattered scenario-owned spawn hot patches."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.npc.companions.persistence",
            DomainId = "npc.companions",
            Category = RebirthFeatureTestCategory.Persistence,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeStatefulMigration,
            ScenarioSet = "common/survivor-token caution",
            Environment = "SP/dedi/P2P",
            Steps = "Follower/dog/drone owner persistence, chunk reload, respawn, silencer persistence, target selection.",
            Expected = "Owner/state persists correctly; no survivor scenario token confusion.",
            Notes = "Stateful and network-sensitive."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.zombie.ai.special",
            DomainId = "zombie.ai.special",
            Category = RebirthFeatureTestCategory.HotPathPerformance,
            Priority = RebirthFeatureTestPriority.Critical,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeHotPathMigration,
            ScenarioSet = "common/hive/purge contexts",
            Environment = "SP/dedi",
            Steps = "Vehicle attacks, block jump, fat aura, spider climbing, decoys, horde night.",
            Expected = "Correct target/position behavior without hot-path overhead or dedi mismatch.",
            Notes = "Split into smaller test slices before migration."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.audio.sync",
            DomainId = "audio.sounds",
            Category = RebirthFeatureTestCategory.AudioSync,
            Priority = RebirthFeatureTestPriority.High,
            Readiness = RebirthFeatureTestReadiness.RequiredBeforeMigration,
            ScenarioSet = "common",
            Environment = "SP/dedi",
            Steps = "NPC silencer, dog hurt suppression, vehicle hit damage/sound/animation timing.",
            Expected = "No double sounds, no missing sounds, timing aligned with authoritative action.",
            Notes = "Audio must not be fixed by breaking damage/AI timing."
        },
        new RebirthFeatureTestCaseDecl
        {
            TestId = "test.external.compat",
            DomainId = "external.compatibility",
            Category = RebirthFeatureTestCategory.ExternalCompatibility,
            Priority = RebirthFeatureTestPriority.High,
            Readiness = RebirthFeatureTestReadiness.FutureAutomation,
            ScenarioSet = "common",
            Environment = "compat on/off",
            Steps = "Test external XML/project support with compatibility enabled/disabled.",
            Expected = "Core feature ownership remains separate from external compatibility.",
            Notes = "Vendored governance and compatibility XML are separate."
        }
    };

    public static string GetSummaryReport()
    {
        int critical = 0;
        int blocked = 0;
        int hot = 0;
        int stateful = 0;

        for (int i = 0; i < s_tests.Length; i++)
        {
            RebirthFeatureTestCaseDecl t = s_tests[i];

            if (t.Priority == RebirthFeatureTestPriority.Critical)
                critical++;
            if (t.Priority == RebirthFeatureTestPriority.Blocked || t.Readiness == RebirthFeatureTestReadiness.BlockedByDisambiguation)
                blocked++;
            if (t.Category == RebirthFeatureTestCategory.HotPathPerformance)
                hot++;
            if (t.Category == RebirthFeatureTestCategory.Persistence || t.Category == RebirthFeatureTestCategory.NetworkAuthority)
                stateful++;
        }

        return "[RebirthFeatureTests] Tests: " + s_tests.Length
            + "; critical: " + critical
            + "; blocked: " + blocked
            + "; hot-path/perf: " + hot
            + "; stateful/network: " + stateful
            + ". Matrix is read-only; declarations only, execution=NotRun.";
    }

    public static string GetTestReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(32768);
        sb.AppendLine("[RebirthFeatureTests] feature test matrix.");
        sb.AppendLine("  Read-only. This does not execute tests.");
        sb.AppendLine("id | domain | category | priority | readiness | scenarios | environment | steps | expected | notes");

        bool any = false;
        for (int i = 0; i < s_tests.Length; i++)
        {
            RebirthFeatureTestCaseDecl t = s_tests[i];
            if (!Matches(t, f))
                continue;

            any = true;
            sb.Append(t.TestId).Append(" | ")
              .Append(t.DomainId).Append(" | ")
              .Append(t.Category).Append(" | ")
              .Append(t.Priority).Append(" | ")
              .Append(t.Readiness).Append(" | ")
              .Append(t.ScenarioSet).Append(" | ")
              .Append(t.Environment).Append(" | ")
              .Append(t.Steps).Append(" | ")
              .Append(t.Expected).Append(" | ")
              .AppendLine(t.Notes);
        }

        if (!any)
            sb.AppendLine("No feature test matched the filter.");

        return sb.ToString();
    }

    public static string GetCriticalReport()
    {
        return GetTestReport("Critical");
    }

    public static string GetHotPathReport()
    {
        return GetTestReport("HotPathPerformance");
    }

    public static string GetStatefulReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFeatureTests] stateful/network test gates.");
        sb.AppendLine("id | domain | category | environment | expected | notes");

        for (int i = 0; i < s_tests.Length; i++)
        {
            RebirthFeatureTestCaseDecl t = s_tests[i];
            if (t.Category != RebirthFeatureTestCategory.Persistence
                && t.Category != RebirthFeatureTestCategory.NetworkAuthority
                && t.Category != RebirthFeatureTestCategory.DedicatedServer
                && t.Category != RebirthFeatureTestCategory.PeerToPeer)
                continue;

            sb.Append(t.TestId).Append(" | ")
              .Append(t.DomainId).Append(" | ")
              .Append(t.Category).Append(" | ")
              .Append(t.Environment).Append(" | ")
              .Append(t.Expected).Append(" | ")
              .AppendLine(t.Notes);
        }

        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthFeatureTests] blocked tests.");
        sb.AppendLine("id | domain | reason | notes");

        for (int i = 0; i < s_tests.Length; i++)
        {
            RebirthFeatureTestCaseDecl t = s_tests[i];
            if (t.Priority != RebirthFeatureTestPriority.Blocked
                && t.Readiness != RebirthFeatureTestReadiness.BlockedByDisambiguation)
                continue;

            sb.Append(t.TestId).Append(" | ")
              .Append(t.DomainId).Append(" | ")
              .Append(t.Readiness).Append(" | ")
              .AppendLine(t.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthFeatureTests] safety rules:");
        sb.AppendLine("  1. Matrix is read-only.");
        sb.AppendLine("  2. No tests are executed.");
        sb.AppendLine("  3. No gameplay behavior is connected.");
        sb.AppendLine("  4. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  5. No runtime XML parsing occurs.");
        sb.AppendLine("  6. Hot-path migration requires hot-path test gates.");
        sb.AppendLine("  7. Stateful migration requires network/persistence test gates.");
        sb.AppendLine("  8. Survivor scenario tests remain blocked until disambiguation.");
        return sb.ToString();
    }

    private static bool Matches(RebirthFeatureTestCaseDecl t, string filter)
    {
        if (t == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (t.TestId != null && t.TestId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.DomainId != null && t.DomainId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.ScenarioSet != null && t.ScenarioSet.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Environment != null && t.Environment.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Steps != null && t.Steps.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Expected != null && t.Expected.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Category.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Priority.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (t.Readiness.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
