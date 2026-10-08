using System;
using System.Text;

#nullable disable

public enum RebirthScenarioManagerKind
{
    Unknown,
    CommonInfrastructure,
    PurgeProgress,
    PurgeDiscovery,
    PurgeDisplay,
    PurgeMarkers,
    HiveCavePolicy,
    HiveSpawnPolicy,
    SurvivorScenario,
    CustomScenario,
    SharedSpawning,
    SharedUi,
    SharedXml,
    SharedNetwork,
    SharedRuntimePolicy
}

public enum RebirthScenarioManagerLifecycle
{
    Unknown,
    ModLoad,
    WorldLoad,
    WorldUnload,
    PlayerLogin,
    PlayerLogout,
    OptionChanged,
    ScenarioPolicyRebuilt,
    EventDrivenOnly,
    SchedulerAllowed,
    ManualReviewRequired
}

public enum RebirthScenarioManagerAuthority
{
    Unknown,
    ClientOnly,
    ServerOnly,
    ServerAuthoritativeClientDisplay,
    SharedReadOnly,
    XmlLoadTimeOnly,
    ManualReviewRequired
}

public enum RebirthScenarioManagerBoundaryRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    BlockedUntilDisambiguated
}

public sealed class RebirthScenarioManagerBoundaryDecl
{
    public string ManagerId;
    public string ScenarioId;
    public RebirthScenarioManagerKind ManagerKind;
    public RebirthScenarioManagerLifecycle Lifecycle;
    public RebirthScenarioManagerAuthority Authority;
    public RebirthScenarioManagerBoundaryRisk Risk;
    public string Owns;
    public string MustNotOwn;
    public string TalksTo;
    public string Notes;
}

/// <summary>
/// Read-only scenario manager boundary registry.
/// This defines manager ownership/lifecycle boundaries before gameplay migration.
/// It does not instantiate managers or register scheduler jobs.
/// </summary>
public static class RebirthScenarioManagerBoundaryRegistry
{
    private static readonly RebirthScenarioManagerBoundaryDecl[] s_boundaries = new[]
    {
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.scenario.runtime",
            ScenarioId = "common",
            ManagerKind = RebirthScenarioManagerKind.SharedRuntimePolicy,
            Lifecycle = RebirthScenarioManagerLifecycle.ScenarioPolicyRebuilt,
            Authority = RebirthScenarioManagerAuthority.SharedReadOnly,
            Risk = RebirthScenarioManagerBoundaryRisk.High,
            Owns = "resolved scenario policy snapshots and cheap hot-path flags",
            MustNotOwn = "scenario-specific progress, worldgen, spawning execution, UI drawing",
            TalksTo = "options.cache, scenario contracts, module bindings, XML ownership",
            Notes = "This is the central cache owner; future hot paths read from it."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.scenario.xml",
            ScenarioId = "common",
            ManagerKind = RebirthScenarioManagerKind.SharedXml,
            Lifecycle = RebirthScenarioManagerLifecycle.WorldLoad,
            Authority = RebirthScenarioManagerAuthority.XmlLoadTimeOnly,
            Risk = RebirthScenarioManagerBoundaryRisk.High,
            Owns = "scenario XML ownership map and load-time resolution",
            MustNotOwn = "runtime XML parsing, gameplay decisions in hot paths",
            TalksTo = "scenario runtime policy, spawning, world.caves, purge progress",
            Notes = "Physical XML reorganization can happen later; ownership is declared first."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.purge.progress",
            ScenarioId = "purge",
            ManagerKind = RebirthScenarioManagerKind.PurgeProgress,
            Lifecycle = RebirthScenarioManagerLifecycle.EventDrivenOnly,
            Authority = RebirthScenarioManagerAuthority.ServerAuthoritativeClientDisplay,
            Risk = RebirthScenarioManagerBoundaryRisk.High,
            Owns = "purge progress truth, percentage changes, server-side validation",
            MustNotOwn = "HUD rendering, compass drawing, raw POI scans from UI",
            TalksTo = "network packages, purge display, purge discovery, map markers",
            Notes = "Server owns progress; clients receive snapshots."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.purge.discovery",
            ScenarioId = "purge",
            ManagerKind = RebirthScenarioManagerKind.PurgeDiscovery,
            Lifecycle = RebirthScenarioManagerLifecycle.EventDrivenOnly,
            Authority = RebirthScenarioManagerAuthority.ServerAuthoritativeClientDisplay,
            Risk = RebirthScenarioManagerBoundaryRisk.High,
            Owns = "POI discovery events and purge POI tier eligibility",
            MustNotOwn = "continuous player/HUD/compass scanning",
            TalksTo = "purge progress, prefab policy, sleeper policy",
            Notes = "Discovery should update on relevant events only."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.purge.display",
            ScenarioId = "purge",
            ManagerKind = RebirthScenarioManagerKind.PurgeDisplay,
            Lifecycle = RebirthScenarioManagerLifecycle.EventDrivenOnly,
            Authority = RebirthScenarioManagerAuthority.ClientOnly,
            Risk = RebirthScenarioManagerBoundaryRisk.Medium,
            Owns = "client display snapshot consumption for purge messages/HUD",
            MustNotOwn = "authoritative progress calculation",
            TalksTo = "purge progress net snapshots, UI HUD, localization",
            Notes = "Client display is a consumer, not scenario truth."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.purge.markers",
            ScenarioId = "purge",
            ManagerKind = RebirthScenarioManagerKind.PurgeMarkers,
            Lifecycle = RebirthScenarioManagerLifecycle.EventDrivenOnly,
            Authority = RebirthScenarioManagerAuthority.ServerAuthoritativeClientDisplay,
            Risk = RebirthScenarioManagerBoundaryRisk.Medium,
            Owns = "event-based map/compass marker state for purge crates/skulls/progress",
            MustNotOwn = "per-frame marker recreation or compass recomputation",
            TalksTo = "map.markers, ui.compass, network packages",
            Notes = "Markers update on spawn/destroy/login/progress changes."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.hive.cavePolicy",
            ScenarioId = "hive",
            ManagerKind = RebirthScenarioManagerKind.HiveCavePolicy,
            Lifecycle = RebirthScenarioManagerLifecycle.ScenarioPolicyRebuilt,
            Authority = RebirthScenarioManagerAuthority.ServerOnly,
            Risk = RebirthScenarioManagerBoundaryRisk.Critical,
            Owns = "resolved cave tunnel and cave POI policy distinction",
            MustNotOwn = "generic prefab decoration or generic spawn execution",
            TalksTo = "world.caves, spawning.biomes, prefab/tag policy",
            Notes = "Cave tunnel helper remains separate from cave POI tag checks."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.hive.spawnPolicy",
            ScenarioId = "hive",
            ManagerKind = RebirthScenarioManagerKind.HiveSpawnPolicy,
            Lifecycle = RebirthScenarioManagerLifecycle.ScenarioPolicyRebuilt,
            Authority = RebirthScenarioManagerAuthority.ServerOnly,
            Risk = RebirthScenarioManagerBoundaryRisk.High,
            Owns = "hive/cave spawn restrictions and blood moon spawn policy data",
            MustNotOwn = "spawn execution loops or EntityGroups patch ownership",
            TalksTo = "spawning.biomes, spawning.groups, world.caves",
            Notes = "Spawning modules execute; hive manager provides cached restrictions."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.survivor.scenario",
            ScenarioId = "survivor",
            ManagerKind = RebirthScenarioManagerKind.SurvivorScenario,
            Lifecycle = RebirthScenarioManagerLifecycle.ManualReviewRequired,
            Authority = RebirthScenarioManagerAuthority.ManualReviewRequired,
            Risk = RebirthScenarioManagerBoundaryRisk.BlockedUntilDisambiguated,
            Owns = "nothing yet",
            MustNotOwn = "survivor NPC/entity role systems until explicitly classified",
            TalksTo = "entities.roles, spawning.groups, scenario XML ownership",
            Notes = "Blocked until ScenarioId.Survivor is separated from EntityRole.Survivor and EntityTag.survivor."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.shared.spawning",
            ScenarioId = "common",
            ManagerKind = RebirthScenarioManagerKind.SharedSpawning,
            Lifecycle = RebirthScenarioManagerLifecycle.ScenarioPolicyRebuilt,
            Authority = RebirthScenarioManagerAuthority.ServerOnly,
            Risk = RebirthScenarioManagerBoundaryRisk.Critical,
            Owns = "spawn execution and central spawn policy dispatch",
            MustNotOwn = "scenario-specific progress/POI logic",
            TalksTo = "purge spawn policy, hive spawn policy, entitygroups",
            Notes = "Scenarios register policy; shared spawning owns execution and hot-path adapter."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.shared.ui",
            ScenarioId = "common",
            ManagerKind = RebirthScenarioManagerKind.SharedUi,
            Lifecycle = RebirthScenarioManagerLifecycle.EventDrivenOnly,
            Authority = RebirthScenarioManagerAuthority.ClientOnly,
            Risk = RebirthScenarioManagerBoundaryRisk.High,
            Owns = "HUD/compass display rendering and cached display state consumption",
            MustNotOwn = "scenario calculations, server truth, POI scans, spawn rules",
            TalksTo = "purge display snapshots, map markers, localization",
            Notes = "UI must consume cached display state only."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.shared.network",
            ScenarioId = "common",
            ManagerKind = RebirthScenarioManagerKind.SharedNetwork,
            Lifecycle = RebirthScenarioManagerLifecycle.EventDrivenOnly,
            Authority = RebirthScenarioManagerAuthority.ServerAuthoritativeClientDisplay,
            Risk = RebirthScenarioManagerBoundaryRisk.High,
            Owns = "network envelope and authority validation patterns",
            MustNotOwn = "scenario-specific business logic",
            TalksTo = "purge progress/display/sleeper packages and future scenario packages",
            Notes = "Network packages should carry snapshots/events, not trigger full client recomputation."
        },
        new RebirthScenarioManagerBoundaryDecl
        {
            ManagerId = "manager.custom.scenario",
            ScenarioId = "future.custom",
            ManagerKind = RebirthScenarioManagerKind.CustomScenario,
            Lifecycle = RebirthScenarioManagerLifecycle.ManualReviewRequired,
            Authority = RebirthScenarioManagerAuthority.ManualReviewRequired,
            Risk = RebirthScenarioManagerBoundaryRisk.High,
            Owns = "nothing until custom scenario declares contracts",
            MustNotOwn = "shared hot-path patches or implicit XML layers",
            TalksTo = "scenario contracts, bindings, XML ownership, performance contracts",
            Notes = "Future custom scenarios must declare manager boundaries before behavior."
        }
    };

    public static string GetSummaryReport()
    {
        int server = 0;
        int client = 0;
        int blocked = 0;
        int critical = 0;

        for (int i = 0; i < s_boundaries.Length; i++)
        {
            RebirthScenarioManagerBoundaryDecl b = s_boundaries[i];

            if (b.Authority == RebirthScenarioManagerAuthority.ServerOnly
                || b.Authority == RebirthScenarioManagerAuthority.ServerAuthoritativeClientDisplay)
                server++;
            if (b.Authority == RebirthScenarioManagerAuthority.ClientOnly
                || b.Authority == RebirthScenarioManagerAuthority.ServerAuthoritativeClientDisplay)
                client++;
            if (b.Risk == RebirthScenarioManagerBoundaryRisk.BlockedUntilDisambiguated
                || b.Lifecycle == RebirthScenarioManagerLifecycle.ManualReviewRequired)
                blocked++;
            if (b.Risk == RebirthScenarioManagerBoundaryRisk.Critical)
                critical++;
        }

        return "[RebirthScenarioManagers] Boundaries: " + s_boundaries.Length
            + "; server-authoritative/server-only: " + server
            + "; client-display/client-only: " + client
            + "; critical: " + critical
            + "; blocked/manual-review: " + blocked
            + ". Registry is read-only.";
    }

    public static string GetBoundaryReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthScenarioManagers] scenario manager boundaries.");
        sb.AppendLine("  Read-only. This does not instantiate managers or register scheduler jobs.");
        sb.AppendLine("manager | scenario | kind | lifecycle | authority | risk | owns | must not own | talks to | notes");

        bool any = false;
        for (int i = 0; i < s_boundaries.Length; i++)
        {
            RebirthScenarioManagerBoundaryDecl b = s_boundaries[i];
            if (!Matches(b, f))
                continue;

            any = true;
            sb.Append(b.ManagerId).Append(" | ")
              .Append(b.ScenarioId).Append(" | ")
              .Append(b.ManagerKind).Append(" | ")
              .Append(b.Lifecycle).Append(" | ")
              .Append(b.Authority).Append(" | ")
              .Append(b.Risk).Append(" | ")
              .Append(b.Owns).Append(" | ")
              .Append(b.MustNotOwn).Append(" | ")
              .Append(b.TalksTo).Append(" | ")
              .AppendLine(b.Notes);
        }

        if (!any)
            sb.AppendLine("No manager boundary matched the filter.");

        return sb.ToString();
    }

    public static string GetScenarioReport(string scenario)
    {
        string f = (scenario ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(f))
            return "[RebirthScenarioManagers] Missing scenario id.";

        return GetBoundaryReport("scenario:" + f);
    }

    public static string GetAuthorityReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioManagers] authority boundaries.");
        sb.AppendLine("manager | scenario | authority | owns | notes");

        for (int i = 0; i < s_boundaries.Length; i++)
        {
            RebirthScenarioManagerBoundaryDecl b = s_boundaries[i];

            sb.Append(b.ManagerId).Append(" | ")
              .Append(b.ScenarioId).Append(" | ")
              .Append(b.Authority).Append(" | ")
              .Append(b.Owns).Append(" | ")
              .AppendLine(b.Notes);
        }

        return sb.ToString();
    }

    public static string GetLifecycleReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioManagers] lifecycle boundaries.");
        sb.AppendLine("manager | lifecycle | authority | scheduler allowed? | notes");

        for (int i = 0; i < s_boundaries.Length; i++)
        {
            RebirthScenarioManagerBoundaryDecl b = s_boundaries[i];
            bool schedulerAllowed = b.Lifecycle == RebirthScenarioManagerLifecycle.SchedulerAllowed;

            sb.Append(b.ManagerId).Append(" | ")
              .Append(b.Lifecycle).Append(" | ")
              .Append(b.Authority).Append(" | ")
              .Append(schedulerAllowed).Append(" | ")
              .AppendLine(b.Notes);
        }

        sb.AppendLine("Rule: event-driven is preferred; scheduler jobs must be declared and budgeted before implementation.");
        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioManagers] blocked/manual-review managers.");
        sb.AppendLine("manager | scenario | reason | notes");

        for (int i = 0; i < s_boundaries.Length; i++)
        {
            RebirthScenarioManagerBoundaryDecl b = s_boundaries[i];
            if (b.Risk != RebirthScenarioManagerBoundaryRisk.BlockedUntilDisambiguated
                && b.Lifecycle != RebirthScenarioManagerLifecycle.ManualReviewRequired
                && b.Authority != RebirthScenarioManagerAuthority.ManualReviewRequired)
                continue;

            sb.Append(b.ManagerId).Append(" | ")
              .Append(b.ScenarioId).Append(" | ")
              .Append(b.Risk).Append(" / ").Append(b.Lifecycle).Append(" | ")
              .AppendLine(b.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthScenarioManagers] safety rules:");
        sb.AppendLine("  1. Registry is read-only.");
        sb.AppendLine("  2. No managers are instantiated.");
        sb.AppendLine("  3. No scheduler jobs are registered.");
        sb.AppendLine("  4. No scenario activation/deactivation occurs.");
        sb.AppendLine("  5. No runtime XML parsing occurs.");
        sb.AppendLine("  6. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  7. Shared managers own execution; scenarios provide policies/data.");
        sb.AppendLine("  8. UI managers consume display snapshots only.");
        sb.AppendLine("  9. Survivor scenario manager remains blocked until disambiguation.");
        return sb.ToString();
    }

    private static bool Matches(RebirthScenarioManagerBoundaryDecl b, string filter)
    {
        if (b == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        string f = filter;

        if (f.StartsWith("scenario:", StringComparison.OrdinalIgnoreCase))
        {
            string scenario = f.Substring("scenario:".Length);
            return b.ScenarioId != null && b.ScenarioId.IndexOf(scenario, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        if (b.ManagerId != null && b.ManagerId.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.ScenarioId != null && b.ScenarioId.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.Owns != null && b.Owns.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.MustNotOwn != null && b.MustNotOwn.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.TalksTo != null && b.TalksTo.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.ManagerKind.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.Authority.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (b.Risk.ToString().IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
