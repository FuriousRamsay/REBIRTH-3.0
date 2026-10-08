using System;
using System.Text;

#nullable disable

public enum RebirthFeatureMigrationWave
{
    Unknown,
    Wave0Readiness,
    Wave1LowRiskInfrastructure,
    Wave2ReadOnlyDisplay,
    Wave3StatefulInfrastructure,
    Wave4LowRiskGameplay,
    Wave5ServerAuthoritativeGameplay,
    Wave6HotPathGameplay,
    Wave7BlockedManualReview
}

public enum RebirthFeatureMigrationReadiness
{
    Unknown,
    ReadyForPlanning,
    NeedsContract,
    NeedsTestMatrix,
    NeedsNetworkPersistenceContract,
    NeedsHotPathStrategy,
    BlockedBySurvivorDisambiguation,
    BlockedByExternalCompatibility,
    DoNotMigrateYet
}

public enum RebirthFeatureMigrationRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthFeatureMigrationOrderDecl
{
    public string OrderId;
    public string DomainId;
    public RebirthFeatureMigrationWave Wave;
    public int Sequence;
    public RebirthFeatureMigrationReadiness Readiness;
    public RebirthFeatureMigrationRisk Risk;
    public string Prerequisites;
    public string WhyHere;
    public string MustNotStartBefore;
    public string TestGate;
    public string Notes;
}

/// <summary>
/// Read-only feature migration order plan.
/// This does not migrate code. It defines a safe staged order for future implementation.
/// </summary>
public static class RebirthFeatureMigrationOrderRegistry
{
    private static readonly RebirthFeatureMigrationOrderDecl[] s_order = new[]
    {
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.000.readiness",
            DomainId = "architecture.readiness",
            Wave = RebirthFeatureMigrationWave.Wave0Readiness,
            Sequence = 0,
            Readiness = RebirthFeatureMigrationReadiness.ReadyForPlanning,
            Risk = RebirthFeatureMigrationRisk.Low,
            Prerequisites = "current ledgers/contracts",
            WhyHere = "Confirm architecture, plan, XML cleanup, dependency graph, and safety rules before real migration.",
            MustNotStartBefore = "nothing",
            TestGate = "compile and command visibility",
            Notes = "This is still no-gameplay scaffolding."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.010.debug.policy",
            DomainId = "debug.diagnostics",
            Wave = RebirthFeatureMigrationWave.Wave1LowRiskInfrastructure,
            Sequence = 10,
            Readiness = RebirthFeatureMigrationReadiness.NeedsHotPathStrategy,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "Phase 2K Harmony/reflection policy",
            WhyHere = "Debug/profiling rules must exist before any migrated feature adds diagnostics.",
            MustNotStartBefore = "Harmony patch strategy and reflection policy",
            TestGate = "release/profiling/stripped behavior separation",
            Notes = "No debug work construction before bool gate."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.020.performance.harness",
            DomainId = "performance.harness",
            Wave = RebirthFeatureMigrationWave.Wave1LowRiskInfrastructure,
            Sequence = 20,
            Readiness = RebirthFeatureMigrationReadiness.NeedsTestMatrix,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "feature test matrix and build verification readiness",
            WhyHere = "Performance testing must exist before hot-path gameplay migration.",
            MustNotStartBefore = "Phase 2H/2J",
            TestGate = "standing/running/driving baseline plan",
            Notes = "Actual metrics can come later, but harness shape must be planned."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.030.network.authority",
            DomainId = "network.authority",
            Wave = RebirthFeatureMigrationWave.Wave3StatefulInfrastructure,
            Sequence = 30,
            Readiness = RebirthFeatureMigrationReadiness.NeedsNetworkPersistenceContract,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "Phase 2I network authority contracts",
            WhyHere = "Stateful features need SP/dedi/P2P authority rules before migration.",
            MustNotStartBefore = "Phase 2I",
            TestGate = "SP/dedi/P2P authority matrix",
            Notes = "Prevents client-owned truth and sync drift."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.040.persistence",
            DomainId = "persistence.saveLoad",
            Wave = RebirthFeatureMigrationWave.Wave3StatefulInfrastructure,
            Sequence = 40,
            Readiness = RebirthFeatureMigrationReadiness.NeedsNetworkPersistenceContract,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "Phase 2I persistence contracts",
            WhyHere = "Stateful features need relog/restart/chunk reload rules.",
            MustNotStartBefore = "Phase 2I",
            TestGate = "relog, server restart, chunk unload/reload",
            Notes = "Required before loot, NPC, item rolls, paint persistence, traders."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.050.ui.display",
            DomainId = "ui.hud.compass",
            Wave = RebirthFeatureMigrationWave.Wave2ReadOnlyDisplay,
            Sequence = 50,
            Readiness = RebirthFeatureMigrationReadiness.NeedsTestMatrix,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "cached display snapshot contracts",
            WhyHere = "Read-only UI display can migrate before gameplay if it consumes cached state only.",
            MustNotStartBefore = "test matrix for UI update/idling",
            TestGate = "idle HUD/compass update, scenario snapshot display, no scans",
            Notes = "No server truth or scenario calculation inside UI loops."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.060.xml.cleanup",
            DomainId = "xml.cleanup.reorganization",
            Wave = RebirthFeatureMigrationWave.Wave1LowRiskInfrastructure,
            Sequence = 60,
            Readiness = RebirthFeatureMigrationReadiness.NeedsContract,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "XML cleanup plan and include root rule",
            WhyHere = "XML organization can be prepared before gameplay but should not be physically moved until ownership is stable.",
            MustNotStartBefore = "root Config include convention and XML ownership review",
            TestGate = "XML load order and include path validation",
            Notes = "Root Config XML files remain include entry points."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.100.paint.render",
            DomainId = "paint.render",
            Wave = RebirthFeatureMigrationWave.Wave6HotPathGameplay,
            Sequence = 100,
            Readiness = RebirthFeatureMigrationReadiness.NeedsHotPathStrategy,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "Harmony/reflection policy, perf harness, persistence contract",
            WhyHere = "Render hot path needs a single owner and strict performance rules.",
            MustNotStartBefore = "Phase 2K and render test matrix",
            TestGate = "render hot path, chunk reload, client/server paint persistence",
            Notes = "BlockShapeNew.renderFace must avoid per-face overhead."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.110.items.stats",
            DomainId = "items.stats.randomization",
            Wave = RebirthFeatureMigrationWave.Wave6HotPathGameplay,
            Sequence = 110,
            Readiness = RebirthFeatureMigrationReadiness.NeedsHotPathStrategy,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "Item stat contract, persistence contract, Harmony/reflection policy",
            WhyHere = "ItemValue.ModifyValue is hot and affects armor/vehicle parts.",
            MustNotStartBefore = "persistence + hot-path patch policy",
            TestGate = "quality rolls, tooltips, server sync, no per-call allocations",
            Notes = "Migrate before armor and vehicle-part stat behavior."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.120.armor.effects",
            DomainId = "armor.effects",
            Wave = RebirthFeatureMigrationWave.Wave5ServerAuthoritativeGameplay,
            Sequence = 120,
            Readiness = RebirthFeatureMigrationReadiness.NeedsContract,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "items.stats.randomization",
            WhyHere = "Armor effects depend on stat ownership and item-value behavior.",
            MustNotStartBefore = "item stat migration order/test plan",
            TestGate = "triggered effects, passives, set swaps, server parity",
            Notes = "Avoid scattering armor logic into generic item hot paths."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.130.map.loot",
            DomainId = "map.markers.reservations + loot.persistence",
            Wave = RebirthFeatureMigrationWave.Wave5ServerAuthoritativeGameplay,
            Sequence = 130,
            Readiness = RebirthFeatureMigrationReadiness.NeedsNetworkPersistenceContract,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "network.authority and persistence.saveLoad",
            WhyHere = "Markers and reserved loot must migrate together to avoid stale/flickering markers.",
            MustNotStartBefore = "network/persistence contracts",
            TestGate = "spawn/destroy/login/relog/server restart/non-owner blocked",
            Notes = "Event-based markers only."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.140.workstations",
            DomainId = "workstations.crafting",
            Wave = RebirthFeatureMigrationWave.Wave5ServerAuthoritativeGameplay,
            Sequence = 140,
            Readiness = RebirthFeatureMigrationReadiness.NeedsNetworkPersistenceContract,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "network/persistence contracts and performance tests",
            WhyHere = "Known hitch areas need performance gates before migration.",
            MustNotStartBefore = "performance test matrix",
            TestGate = "queue empty hitch, remote containers, chunk reload, server parity",
            Notes = "Automation must be gated and event-driven."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.150.harvest.salvage",
            DomainId = "harvest.salvage",
            Wave = RebirthFeatureMigrationWave.Wave4LowRiskGameplay,
            Sequence = 150,
            Readiness = RebirthFeatureMigrationReadiness.NeedsContract,
            Risk = RebirthFeatureMigrationRisk.Medium,
            Prerequisites = "basic server authority rules",
            WhyHere = "Lower-risk gameplay area after core contracts, but still needs parity tests.",
            MustNotStartBefore = "feature test matrix",
            TestGate = "wild/grown, per block, vehicle stump, stealth sound reset",
            Notes = "Preserve round-down and avoid double-apply."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.160.world.caves",
            DomainId = "world.caves.descent",
            Wave = RebirthFeatureMigrationWave.Wave5ServerAuthoritativeGameplay,
            Sequence = 160,
            Readiness = RebirthFeatureMigrationReadiness.NeedsContract,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "scenario.hive contracts and spawning policy contracts",
            WhyHere = "Cave/hive policy must be correct before hive spawning and player event exclusions.",
            MustNotStartBefore = "cave tunnel vs cave POI contract",
            TestGate = "cave tunnel, cave POI, underground POI, blood moon spawn",
            Notes = "Do not merge tunnel helper with cave POI tags."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.170.spawning",
            DomainId = "spawning.events",
            Wave = RebirthFeatureMigrationWave.Wave5ServerAuthoritativeGameplay,
            Sequence = 170,
            Readiness = RebirthFeatureMigrationReadiness.NeedsNetworkPersistenceContract,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "world.caves, scenario policies, network authority",
            WhyHere = "Spawning executes central policy after scenario/world rules exist.",
            MustNotStartBefore = "spawning test matrix and authority contracts",
            TestGate = "sleepers, event spawns, blood moon, cave exclusions, dedicated server",
            Notes = "Scenarios provide policy; spawning owns execution."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.180.quests.events",
            DomainId = "quests.events.progression",
            Wave = RebirthFeatureMigrationWave.Wave5ServerAuthoritativeGameplay,
            Sequence = 180,
            Readiness = RebirthFeatureMigrationReadiness.NeedsContract,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "scenario, spawning, persistence, authority",
            WhyHere = "Quest/event behavior depends on scenario/spawn/persistence decisions.",
            MustNotStartBefore = "feature test matrix",
            TestGate = "quest state, relog, cave exclusions, server parity",
            Notes = "Cave tunnel and cave POI exclusions stay distinct."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.190.npc.companions",
            DomainId = "npc.companions",
            Wave = RebirthFeatureMigrationWave.Wave5ServerAuthoritativeGameplay,
            Sequence = 190,
            Readiness = RebirthFeatureMigrationReadiness.NeedsNetworkPersistenceContract,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "network/persistence and survivor disambiguation guardrail",
            WhyHere = "NPC companions are stateful and server-sensitive.",
            MustNotStartBefore = "persistence + authority contracts",
            TestGate = "SP/dedi/P2P, chunk reload, owner persistence, silencer persistence",
            Notes = "Do not absorb survivor NPC/entity role into survivor scenario."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.200.traders",
            DomainId = "traders.behavior",
            Wave = RebirthFeatureMigrationWave.Wave5ServerAuthoritativeGameplay,
            Sequence = 200,
            Readiness = RebirthFeatureMigrationReadiness.NeedsNetworkPersistenceContract,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "network/persistence contracts",
            WhyHere = "Trader death/spawn state can poison future state if wrong.",
            MustNotStartBefore = "trader persistence test matrix",
            TestGate = "killall, death, new world, different POI, static spawner, server restart",
            Notes = "Isolate from generic entity death rules."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.210.zombie.ai",
            DomainId = "zombie.ai.special",
            Wave = RebirthFeatureMigrationWave.Wave6HotPathGameplay,
            Sequence = 210,
            Readiness = RebirthFeatureMigrationReadiness.NeedsHotPathStrategy,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "AI policy contracts, network authority, Harmony/reflection policy",
            WhyHere = "Zombie AI is hot, complex, and regression-prone.",
            MustNotStartBefore = "Phase 2K and AI test matrix",
            TestGate = "vehicle, block jump, spider, decoy, horde night, SP/dedi",
            Notes = "Split into smaller AI policies later."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.220.vehicles",
            DomainId = "vehicles.ai.interaction",
            Wave = RebirthFeatureMigrationWave.Wave6HotPathGameplay,
            Sequence = 220,
            Readiness = RebirthFeatureMigrationReadiness.NeedsHotPathStrategy,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "zombie.ai.special, network authority, audio contract",
            WhyHere = "Vehicle AI interaction depends on AI and audio/sync rules.",
            MustNotStartBefore = "vehicle + audio test matrix",
            TestGate = "SP/dedi, hit sound sync, animation sync, damage, horde night",
            Notes = "Avoid fixing damage while breaking sound/facing."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.230.player.crawl",
            DomainId = "player.crawl",
            Wave = RebirthFeatureMigrationWave.Wave6HotPathGameplay,
            Sequence = 230,
            Readiness = RebirthFeatureMigrationReadiness.NeedsHotPathStrategy,
            Risk = RebirthFeatureMigrationRisk.Critical,
            Prerequisites = "movement patch strategy, persistence, test matrix",
            WhyHere = "Player crawl is hot and has many known edge cases.",
            MustNotStartBefore = "Phase 2K and crawl test matrix",
            TestGate = "thin plates/windows/corners/vehicle/water/reload/third person/dedi",
            Notes = "No behavior change without targeted tests."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.240.audio",
            DomainId = "audio.sounds",
            Wave = RebirthFeatureMigrationWave.Wave4LowRiskGameplay,
            Sequence = 240,
            Readiness = RebirthFeatureMigrationReadiness.NeedsContract,
            Risk = RebirthFeatureMigrationRisk.Medium,
            Prerequisites = "network timing awareness",
            WhyHere = "Audio can be migrated in focused slices, but vehicle/NPC audio needs sync tests.",
            MustNotStartBefore = "feature test matrix",
            TestGate = "NPC silencer, dog hurt, vehicle hit sound sync, no double sounds",
            Notes = "Keep sound timing separate from damage logic."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.250.weather",
            DomainId = "weather.environment",
            Wave = RebirthFeatureMigrationWave.Wave4LowRiskGameplay,
            Sequence = 250,
            Readiness = RebirthFeatureMigrationReadiness.NeedsContract,
            Risk = RebirthFeatureMigrationRisk.Medium,
            Prerequisites = "environment patch ownership",
            WhyHere = "Environment can migrate after patch policy and basic tests.",
            MustNotStartBefore = "feature test matrix",
            TestGate = "storm start/stop, server/host/client, lighting settings",
            Notes = "FSR/OBS crash remains handoff/compat concern."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.900.survivor",
            DomainId = "survivor scenario-related features",
            Wave = RebirthFeatureMigrationWave.Wave7BlockedManualReview,
            Sequence = 900,
            Readiness = RebirthFeatureMigrationReadiness.BlockedBySurvivorDisambiguation,
            Risk = RebirthFeatureMigrationRisk.Blocked,
            Prerequisites = "survivor token disambiguation",
            WhyHere = "survivor token overlaps scenario and NPC/entity role concepts.",
            MustNotStartBefore = "manual classification of survivor C#/XML surfaces",
            TestGate = "survivor scenario vs survivor NPC/entity role tests",
            Notes = "Do not migrate by keyword."
        },
        new RebirthFeatureMigrationOrderDecl
        {
            OrderId = "order.910.external",
            DomainId = "external.compatibility",
            Wave = RebirthFeatureMigrationWave.Wave7BlockedManualReview,
            Sequence = 910,
            Readiness = RebirthFeatureMigrationReadiness.BlockedByExternalCompatibility,
            Risk = RebirthFeatureMigrationRisk.High,
            Prerequisites = "external compatibility contracts",
            WhyHere = "External/vendored/compat XML must be separated from core feature ownership.",
            MustNotStartBefore = "external governance and compatibility test matrix",
            TestGate = "compat on/off and external XML tests",
            Notes = "Do not absorb external projects into core migration."
        }
    };

    public static string GetSummaryReport()
    {
        int total = s_order.Length;
        int blocked = 0;
        int critical = 0;
        int hot = 0;

        for (int i = 0; i < s_order.Length; i++)
        {
            RebirthFeatureMigrationOrderDecl o = s_order[i];

            if (o.Risk == RebirthFeatureMigrationRisk.Blocked || o.Wave == RebirthFeatureMigrationWave.Wave7BlockedManualReview)
                blocked++;
            if (o.Risk == RebirthFeatureMigrationRisk.Critical)
                critical++;
            if (o.Wave == RebirthFeatureMigrationWave.Wave6HotPathGameplay)
                hot++;
        }

        return "[RebirthMigrationOrder] Entries: " + total
            + "; critical: " + critical
            + "; hot-path wave: " + hot
            + "; blocked/manual-review: " + blocked
            + ". Plan is read-only.";
    }

    public static string GetOrderReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(32768);
        sb.AppendLine("[RebirthMigrationOrder] feature migration order.");
        sb.AppendLine("  Read-only. This does not migrate code.");
        sb.AppendLine("sequence | domain | wave | readiness | risk | prerequisites | why | must not start before | test gate | notes");

        bool any = false;
        for (int i = 0; i < s_order.Length; i++)
        {
            RebirthFeatureMigrationOrderDecl o = s_order[i];
            if (!Matches(o, f))
                continue;

            any = true;
            sb.Append(o.Sequence).Append(" | ")
              .Append(o.DomainId).Append(" | ")
              .Append(o.Wave).Append(" | ")
              .Append(o.Readiness).Append(" | ")
              .Append(o.Risk).Append(" | ")
              .Append(o.Prerequisites).Append(" | ")
              .Append(o.WhyHere).Append(" | ")
              .Append(o.MustNotStartBefore).Append(" | ")
              .Append(o.TestGate).Append(" | ")
              .AppendLine(o.Notes);
        }

        if (!any)
            sb.AppendLine("No migration order entry matched the filter.");

        return sb.ToString();
    }

    public static string GetWaveReport(string waveFilter)
    {
        string f = (waveFilter ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
            return GetOrderReport(null);

        return GetOrderReport(f);
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthMigrationOrder] blocked/manual-review entries.");
        sb.AppendLine("sequence | domain | readiness | risk | must not start before | notes");

        for (int i = 0; i < s_order.Length; i++)
        {
            RebirthFeatureMigrationOrderDecl o = s_order[i];
            if (o.Risk != RebirthFeatureMigrationRisk.Blocked
                && o.Wave != RebirthFeatureMigrationWave.Wave7BlockedManualReview
                && o.Readiness != RebirthFeatureMigrationReadiness.BlockedBySurvivorDisambiguation
                && o.Readiness != RebirthFeatureMigrationReadiness.BlockedByExternalCompatibility)
                continue;

            sb.Append(o.Sequence).Append(" | ")
              .Append(o.DomainId).Append(" | ")
              .Append(o.Readiness).Append(" | ")
              .Append(o.Risk).Append(" | ")
              .Append(o.MustNotStartBefore).Append(" | ")
              .AppendLine(o.Notes);
        }

        return sb.ToString();
    }

    public static string GetNextPlanningReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthMigrationOrder] next planning prerequisites before gameplay migration:");
        sb.AppendLine("  1. Phase 2H Feature Test Matrix");
        sb.AppendLine("  2. Phase 2I Network Authority and Persistence Contracts");
        sb.AppendLine("  3. Phase 2J Build Verification / Stripped-vs-Profiling readiness");
        sb.AppendLine("  4. Phase 2K Harmony Patch Strategy and Reflection Policy");
        sb.AppendLine("  5. Then choose first low-risk gameplay slice from the migration order.");
        sb.AppendLine();
        sb.AppendLine("Do not start hot-path gameplay migration before Phase 2K.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthMigrationOrder] safety rules:");
        sb.AppendLine("  1. Plan is read-only.");
        sb.AppendLine("  2. No gameplay behavior is connected.");
        sb.AppendLine("  3. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  4. No runtime XML parsing occurs.");
        sb.AppendLine("  5. No files are migrated in this phase.");
        sb.AppendLine("  6. Hot-path migration waits for Harmony/reflection policy.");
        sb.AppendLine("  7. Stateful migration waits for network/persistence contracts.");
        sb.AppendLine("  8. Survivor scenario migration remains blocked until disambiguation.");
        return sb.ToString();
    }

    private static bool Matches(RebirthFeatureMigrationOrderDecl o, string filter)
    {
        if (o == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (o.OrderId != null && o.OrderId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (o.DomainId != null && o.DomainId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (o.Prerequisites != null && o.Prerequisites.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (o.WhyHere != null && o.WhyHere.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (o.MustNotStartBefore != null && o.MustNotStartBefore.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (o.TestGate != null && o.TestGate.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (o.Wave.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (o.Readiness.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (o.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
