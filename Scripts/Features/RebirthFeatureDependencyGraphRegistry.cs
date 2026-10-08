using System;
using System.Text;

#nullable disable

public enum RebirthFeatureDependencyKind
{
    Unknown,
    RequiresBeforeMigration,
    RequiresBeforeRuntime,
    ProvidesPolicyTo,
    ProvidesDisplayStateTo,
    ProvidesPersistenceTo,
    ProvidesAuthorityTo,
    SharesHotPathWith,
    MustNotDependOn,
    BlockedByDisambiguation,
    ExternalCompatibility
}

public enum RebirthFeatureDependencyDirection
{
    Unknown,
    Upstream,
    Downstream,
    Bidirectional,
    Guardrail
}

public enum RebirthFeatureDependencyRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthFeatureDependencyDecl
{
    public string DependencyId;
    public string FromDomainId;
    public string ToDomainId;
    public RebirthFeatureDependencyKind DependencyKind;
    public RebirthFeatureDependencyDirection Direction;
    public RebirthFeatureDependencyRisk Risk;
    public string Reason;
    public string MigrationRule;
    public string TestImpact;
    public string Notes;
}

/// <summary>
/// Read-only feature dependency graph.
/// This records prerequisite relationships before choosing migration order.
/// </summary>
public static class RebirthFeatureDependencyGraphRegistry
{
    private static readonly RebirthFeatureDependencyDecl[] s_dependencies = new[]
    {
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.scenario.before.feature",
            FromDomainId = "scenario.core",
            ToDomainId = "all scenario-dependent domains",
            DependencyKind = RebirthFeatureDependencyKind.RequiresBeforeMigration,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Scenario state currently cuts across many features and must be resolved before feature migration.",
            MigrationRule = "Scenario contracts/runtime policy must exist before migrating scenario-dependent behavior.",
            TestImpact = "Scenario matrix decides which feature tests are valid.",
            Notes = "Scenario layer is prerequisite, not replacement."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.policy.before.hotpaths",
            FromDomainId = "scenario.core",
            ToDomainId = "combat.damage, player.crawl, zombie.ai.special, spawning.events, ui.hud.compass",
            DependencyKind = RebirthFeatureDependencyKind.ProvidesPolicyTo,
            Direction = RebirthFeatureDependencyDirection.Downstream,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Hot paths must read cached scenario flags instead of options/XML/string checks.",
            MigrationRule = "No hot-path adapter migration until policy cache shape is stable.",
            TestImpact = "Hot-path tests must confirm zero scenario scans/allocations.",
            Notes = "Protects performance before gameplay migration."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.network.before.stateful",
            FromDomainId = "network.authority",
            ToDomainId = "map.markers.reservations, loot.persistence, npc.companions, items.stats.randomization, spawning.events, quests.events.progression",
            DependencyKind = RebirthFeatureDependencyKind.ProvidesAuthorityTo,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Stateful/server-authoritative systems need authority rules before migration.",
            MigrationRule = "Dedicated server authority contract must be known before stateful feature implementation.",
            TestImpact = "SP/dedi/P2P test matrix required.",
            Notes = "Prevents client-owned truth and sync drift."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.persistence.before.stateful",
            FromDomainId = "persistence.saveLoad",
            ToDomainId = "map.markers.reservations, loot.persistence, npc.companions, items.stats.randomization, paint.render, traders.behavior, player.crawl",
            DependencyKind = RebirthFeatureDependencyKind.ProvidesPersistenceTo,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Stateful features must define relog/restart/chunk-reload behavior before migration.",
            MigrationRule = "No persistent feature migration without save/load contract.",
            TestImpact = "Relog, chunk unload/reload, server restart tests required.",
            Notes = "Avoids repeating reserved loot/NPC/trader persistence failures."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.debug.before.hotfeatures",
            FromDomainId = "debug.diagnostics",
            ToDomainId = "combat.damage, player.crawl, zombie.ai.special, items.stats.randomization, ui.hud.compass, paint.render",
            DependencyKind = RebirthFeatureDependencyKind.RequiresBeforeRuntime,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Debug/profiling/logging can create hot-path cost if not gated before construction.",
            MigrationRule = "Debug policy must be established before adding diagnostics to migrated features.",
            TestImpact = "Release vs profiling vs stripped comparison tests.",
            Notes = "All debug work must be gated before string/collection/helper evaluation."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.performance.before_migration",
            FromDomainId = "performance.harness",
            ToDomainId = "all high/critical hot-path domains",
            DependencyKind = RebirthFeatureDependencyKind.RequiresBeforeMigration,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.High,
            Reason = "Performance-sensitive migration needs baseline/profiling/stripped comparison capability.",
            MigrationRule = "At minimum, dry-run performance contracts must exist before risky hot-path migration.",
            TestImpact = "Standing/running/driving/chunk-boundary tests and scenario matrix.",
            Notes = "Full harness can be implemented later, but contracts come first."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.ui.depends.display_snapshots",
            FromDomainId = "ui.hud.compass",
            ToDomainId = "scenario.core, map.markers.reservations, quests.events.progression",
            DependencyKind = RebirthFeatureDependencyKind.MustNotDependOn,
            Direction = RebirthFeatureDependencyDirection.Guardrail,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "UI must not recompute scenario/map/quest truth inside update loops.",
            MigrationRule = "UI consumes cached display snapshots only.",
            TestImpact = "Compass/HUD smoothness and idle update tests.",
            Notes = "Prevents purge display and marker costs in UI hot paths."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.spawning.consumes.scenario",
            FromDomainId = "spawning.events",
            ToDomainId = "scenario.core, world.caves.descent, quests.events.progression",
            DependencyKind = RebirthFeatureDependencyKind.ProvidesPolicyTo,
            Direction = RebirthFeatureDependencyDirection.Bidirectional,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Spawning executes central policy while scenarios/caves/events provide restrictions.",
            MigrationRule = "Spawning owns execution; scenarios/world/quests provide cached policy.",
            TestImpact = "Sleepers, blood moon, event spawns, cave exclusions, POI discovery.",
            Notes = "Avoid scattering spawn logic across scenario managers."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.worldcaves.before_hive_spawning",
            FromDomainId = "world.caves.descent",
            ToDomainId = "spawning.events",
            DependencyKind = RebirthFeatureDependencyKind.ProvidesPolicyTo,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Hive/cave spawn rules need clear cave tunnel vs cave POI semantics.",
            MigrationRule = "World.caves policy must be migrated before hive spawn restrictions.",
            TestImpact = "Cave tunnel, cave POI, underground POI, blood moon spawn tests.",
            Notes = "Do not merge tunnel helper and cave POI tags."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.survivor.blocks_related_domains",
            FromDomainId = "scenario.core",
            ToDomainId = "npc.companions, spawning.events, quests.events.progression, external.compatibility",
            DependencyKind = RebirthFeatureDependencyKind.BlockedByDisambiguation,
            Direction = RebirthFeatureDependencyDirection.Guardrail,
            Risk = RebirthFeatureDependencyRisk.Blocked,
            Reason = "survivor token overlaps scenario, NPC/entity roles, tags, groups, progression.",
            MigrationRule = "No survivor-scenario migration by keyword. Classify usage first.",
            TestImpact = "Survivor scenario tests blocked until disambiguation.",
            Notes = "Protects companion/NPC systems from accidental scenario absorption."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.items.before_armor_vehicle_parts",
            FromDomainId = "items.stats.randomization",
            ToDomainId = "armor.effects, vehicles.ai.interaction",
            DependencyKind = RebirthFeatureDependencyKind.RequiresBeforeRuntime,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.High,
            Reason = "Armor effects and vehicle part bonuses depend on item stat/quality/roll architecture.",
            MigrationRule = "Item stat persistence/display rules must exist before armor and vehicle-part stat features.",
            TestImpact = "Quality rolls, tooltip display, server sync, armor/vehicle stat feel.",
            Notes = "ItemValue hot path is a central risk."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.loot_markers_bidirectional",
            FromDomainId = "loot.persistence",
            ToDomainId = "map.markers.reservations",
            DependencyKind = RebirthFeatureDependencyKind.ProvidesPersistenceTo,
            Direction = RebirthFeatureDependencyDirection.Bidirectional,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Reserved loot containers and their markers must save/load/cleanup together.",
            MigrationRule = "Do not migrate markers separately from container ownership and cleanup contracts.",
            TestImpact = "Spawn/destroy/login/relog/server restart/non-owner blocked.",
            Notes = "Prevents marker flicker and stale marker bugs."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.traders.persistence_authority",
            FromDomainId = "traders.behavior",
            ToDomainId = "persistence.saveLoad, network.authority",
            DependencyKind = RebirthFeatureDependencyKind.RequiresBeforeMigration,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.Critical,
            Reason = "Trader death/spawn state can persist incorrectly and affect future worlds/entities.",
            MigrationRule = "Trader migration requires persistence and authority contract first.",
            TestImpact = "killall, death, new world, different POI, static spawner, server restart.",
            Notes = "Trader behavior should be isolated from general entity death logic."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.vehicles_ai_depends_network_audio",
            FromDomainId = "vehicles.ai.interaction",
            ToDomainId = "network.authority, audio.sounds, zombie.ai.special",
            DependencyKind = RebirthFeatureDependencyKind.RequiresBeforeRuntime,
            Direction = RebirthFeatureDependencyDirection.Bidirectional,
            Risk = RebirthFeatureDependencyRisk.High,
            Reason = "Vehicle attack AI has known client/server calculation and sound/animation sync risks.",
            MigrationRule = "Vehicle AI migration must include authority and audio timing contracts.",
            TestImpact = "SP/dedi vehicle side/front, damage, hit sounds, animations, horde night.",
            Notes = "Avoid solving damage while regressing sound or target facing."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.external_guardrails",
            FromDomainId = "external.compatibility",
            ToDomainId = "items.stats.randomization, armor.effects, ui.hud.compass, migration.3_0_drift",
            DependencyKind = RebirthFeatureDependencyKind.ExternalCompatibility,
            Direction = RebirthFeatureDependencyDirection.Guardrail,
            Risk = RebirthFeatureDependencyRisk.High,
            Reason = "External projects and compatibility XML must not be absorbed into core feature ownership.",
            MigrationRule = "Separate vendored governance from compatibility XML and feature ownership.",
            TestImpact = "compat on/off, external XML, build drift, third-party item/armor behavior.",
            Notes = "Avoid mixing external support with core scenario/feature contracts."
        },
        new RebirthFeatureDependencyDecl
        {
            DependencyId = "dep.migration_drift_before_ui_xml",
            FromDomainId = "migration.3_0_drift",
            ToDomainId = "ui.hud.compass, external.compatibility, scenario.core",
            DependencyKind = RebirthFeatureDependencyKind.RequiresBeforeMigration,
            Direction = RebirthFeatureDependencyDirection.Upstream,
            Risk = RebirthFeatureDependencyRisk.High,
            Reason = "3.0 API/XUi/option drift affects compile/runtime shape.",
            MigrationRule = "Version drift contracts must be checked before migrating UI/XML-heavy features.",
            TestImpact = "compile, runtime load, XUi open, commands, healthbars/crosshair.",
            Notes = "Prevents version fixes from being hidden in gameplay migration."
        }
    };

    public static string GetSummaryReport()
    {
        int total = s_dependencies.Length;
        int critical = 0;
        int blocked = 0;
        int guardrails = 0;
        int upstream = 0;

        for (int i = 0; i < s_dependencies.Length; i++)
        {
            RebirthFeatureDependencyDecl d = s_dependencies[i];

            if (d.Risk == RebirthFeatureDependencyRisk.Critical)
                critical++;
            if (d.Risk == RebirthFeatureDependencyRisk.Blocked)
                blocked++;
            if (d.Direction == RebirthFeatureDependencyDirection.Guardrail
                || d.DependencyKind == RebirthFeatureDependencyKind.MustNotDependOn)
                guardrails++;
            if (d.Direction == RebirthFeatureDependencyDirection.Upstream)
                upstream++;
        }

        return "[RebirthFeatureDeps] Dependencies: " + total
            + "; critical: " + critical
            + "; blocked: " + blocked
            + "; guardrails: " + guardrails
            + "; upstream prerequisites: " + upstream
            + ". Graph is read-only.";
    }

    public static string GetDependencyReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(32768);
        sb.AppendLine("[RebirthFeatureDeps] feature dependency graph.");
        sb.AppendLine("  Read-only. This records migration prerequisites and guardrails.");
        sb.AppendLine("id | from | to | kind | direction | risk | reason | migration rule | test impact | notes");

        bool any = false;
        for (int i = 0; i < s_dependencies.Length; i++)
        {
            RebirthFeatureDependencyDecl d = s_dependencies[i];
            if (!Matches(d, f))
                continue;

            any = true;
            sb.Append(d.DependencyId).Append(" | ")
              .Append(d.FromDomainId).Append(" | ")
              .Append(d.ToDomainId).Append(" | ")
              .Append(d.DependencyKind).Append(" | ")
              .Append(d.Direction).Append(" | ")
              .Append(d.Risk).Append(" | ")
              .Append(d.Reason).Append(" | ")
              .Append(d.MigrationRule).Append(" | ")
              .Append(d.TestImpact).Append(" | ")
              .AppendLine(d.Notes);
        }

        if (!any)
            sb.AppendLine("No feature dependency matched the filter.");

        return sb.ToString();
    }

    public static string GetUpstreamReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFeatureDeps] upstream prerequisites.");
        sb.AppendLine("from | to | risk | migration rule");

        for (int i = 0; i < s_dependencies.Length; i++)
        {
            RebirthFeatureDependencyDecl d = s_dependencies[i];
            if (d.Direction != RebirthFeatureDependencyDirection.Upstream)
                continue;

            sb.Append(d.FromDomainId).Append(" | ")
              .Append(d.ToDomainId).Append(" | ")
              .Append(d.Risk).Append(" | ")
              .AppendLine(d.MigrationRule);
        }

        return sb.ToString();
    }

    public static string GetGuardrailReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFeatureDeps] dependency guardrails.");
        sb.AppendLine("id | from | to | kind | risk | rule | notes");

        for (int i = 0; i < s_dependencies.Length; i++)
        {
            RebirthFeatureDependencyDecl d = s_dependencies[i];
            if (d.Direction != RebirthFeatureDependencyDirection.Guardrail
                && d.DependencyKind != RebirthFeatureDependencyKind.MustNotDependOn
                && d.DependencyKind != RebirthFeatureDependencyKind.BlockedByDisambiguation)
                continue;

            sb.Append(d.DependencyId).Append(" | ")
              .Append(d.FromDomainId).Append(" | ")
              .Append(d.ToDomainId).Append(" | ")
              .Append(d.DependencyKind).Append(" | ")
              .Append(d.Risk).Append(" | ")
              .Append(d.MigrationRule).Append(" | ")
              .AppendLine(d.Notes);
        }

        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthFeatureDeps] blocked dependencies.");
        sb.AppendLine("id | from | to | reason | rule");

        for (int i = 0; i < s_dependencies.Length; i++)
        {
            RebirthFeatureDependencyDecl d = s_dependencies[i];
            if (d.Risk != RebirthFeatureDependencyRisk.Blocked)
                continue;

            sb.Append(d.DependencyId).Append(" | ")
              .Append(d.FromDomainId).Append(" | ")
              .Append(d.ToDomainId).Append(" | ")
              .Append(d.Reason).Append(" | ")
              .AppendLine(d.MigrationRule);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthFeatureDeps] safety rules:");
        sb.AppendLine("  1. Graph is read-only.");
        sb.AppendLine("  2. No gameplay behavior is connected.");
        sb.AppendLine("  3. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  4. No runtime XML parsing occurs.");
        sb.AppendLine("  5. No migration order is executed here.");
        sb.AppendLine("  6. Dependency graph informs migration order but does not implement features.");
        sb.AppendLine("  7. Guardrails prevent hot-path/UI/scenario ownership mistakes.");
        return sb.ToString();
    }

    private static bool Matches(RebirthFeatureDependencyDecl d, string filter)
    {
        if (d == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (d.DependencyId != null && d.DependencyId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.FromDomainId != null && d.FromDomainId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.ToDomainId != null && d.ToDomainId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.Reason != null && d.Reason.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.MigrationRule != null && d.MigrationRule.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.TestImpact != null && d.TestImpact.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.DependencyKind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.Direction.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
