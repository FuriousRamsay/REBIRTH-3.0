using System;
using System.Text;

#nullable disable

public enum RebirthScenarioKind
{
    Unknown,
    Common,
    NoneScenario,
    Purge,
    Hive,
    SurvivorScenario,
    SurvivorEntityRole,
    FutureCustom,
    Mixed
}

public enum RebirthScenarioSurfaceKind
{
    Unknown,
    CoreVariable,
    Manager,
    HarmonyPatch,
    Entity,
    SpawnPolicy,
    SleeperVolume,
    PrefabDecorator,
    Trader,
    Airdrop,
    SupplyCrate,
    MapMarker,
    CompassHud,
    XUi,
    NetworkPackage,
    XmlEntityClasses,
    XmlEntityGroups,
    XmlSpawning,
    XmlBuffs,
    XmlItems,
    XmlLoot,
    XmlQuests,
    XmlProgression,
    XmlLocalization,
    XmlPrefabLists,
    XmlCleanup,
    CustomGameOption,
    QuestEvent,
    RuntimeCache
}

public enum RebirthScenarioHotPathRisk
{
    Unknown,
    None,
    Low,
    Medium,
    High,
    Critical
}

public enum RebirthScenarioAuthority
{
    Unknown,
    Client,
    Server,
    Shared,
    ServerAuthoritativeClientDisplay,
    XmlLoadTime,
    BuildTime
}

public enum RebirthScenarioMigrationRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    MustDisambiguate
}

public sealed class RebirthScenarioSurfaceDecl
{
    public string SurfaceId;
    public RebirthScenarioKind Scenario;
    public RebirthScenarioSurfaceKind SurfaceKind;
    public string SourcePath;
    public string CurrentConcern;
    public string FutureOwnerModule;
    public RebirthScenarioHotPathRisk HotPathRisk;
    public RebirthScenarioAuthority Authority;
    public RebirthScenarioMigrationRisk MigrationRisk;
    public string Notes;
}

/// <summary>
/// Static 2.6 scenario surface ledger.
/// This is based on the 2.6 source/XML audit and is read-only.
/// It does not activate scenarios, parse XML, or connect gameplay behavior.
/// </summary>
public static class RebirthScenarioSurfaceLedger
{
    private static readonly RebirthScenarioSurfaceDecl[] s_surfaces = new[]
    {
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "core.variables.customScenario",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.CoreVariable,
            SourcePath = "Scripts/Core/RebirthVariables.cs",
            CurrentConcern = "Scenario state/options are globally reachable and used by many systems.",
            FutureOwnerModule = "scenario.runtime",
            HotPathRisk = RebirthScenarioHotPathRisk.Medium,
            Authority = RebirthScenarioAuthority.Shared,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Must become a cached scenario policy snapshot, not repeated option/string checks."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "core.utilities.scenarioHelpers",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.RuntimeCache,
            SourcePath = "Scripts/Core/RebirthUtilities.cs",
            CurrentConcern = "Scenario/helper logic is centralized but broad and mixed with unrelated utilities.",
            FutureOwnerModule = "scenario.runtime",
            HotPathRisk = RebirthScenarioHotPathRisk.High,
            Authority = RebirthScenarioAuthority.Shared,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Split stable cached policy helpers from one-off migration/utility helpers."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "manager.rebirth.scenarioInit",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.Manager,
            SourcePath = "Scripts/Manager/RebirthManager.cs",
            CurrentConcern = "Scenario initialization and manager orchestration are intertwined.",
            FutureOwnerModule = "scenario.manager",
            HotPathRisk = RebirthScenarioHotPathRisk.Medium,
            Authority = RebirthScenarioAuthority.ServerAuthoritativeClientDisplay,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Scenario managers should register lifecycle hooks, scheduler jobs, and authority boundaries explicitly."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "playerlocal.scenarioEvents",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.HarmonyPatch,
            SourcePath = "Harmony/Harmony_EntityPlayerLocal.cs",
            CurrentConcern = "Player-local scenario checks/events are patched into player flow.",
            FutureOwnerModule = "player.movement + scenario.events",
            HotPathRisk = RebirthScenarioHotPathRisk.High,
            Authority = RebirthScenarioAuthority.Client,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Player update hooks must remain centrally owned; scenarios register policies/events."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "entityalive.scenarioDamage",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.HarmonyPatch,
            SourcePath = "Harmony/Harmony_EntityAlive.cs",
            CurrentConcern = "EntityAlive scenario-specific behavior appears in a hot combat/entity patch.",
            FutureOwnerModule = "combat.damage",
            HotPathRisk = RebirthScenarioHotPathRisk.Critical,
            Authority = RebirthScenarioAuthority.Server,
            MigrationRisk = RebirthScenarioMigrationRisk.Critical,
            Notes = "Scenarios must not own DamageEntity/EntityAlive patches; register data/policies into combat.damage."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "prefabdecorator.purgeHive",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.PrefabDecorator,
            SourcePath = "Harmony/Harmony_DynamicPrefabDecorator.cs",
            CurrentConcern = "Scenario-specific prefab decoration affects world/prefab flow.",
            FutureOwnerModule = "world.prefabs + scenario.xml",
            HotPathRisk = RebirthScenarioHotPathRisk.Medium,
            Authority = RebirthScenarioAuthority.Server,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Move to scenario prefab policy; avoid runtime string scans after load."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "entitygroups.scenarioSpawns",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.SpawnPolicy,
            SourcePath = "Harmony/Harmony_EntityGroups.cs",
            CurrentConcern = "Scenario-specific entity group behavior is patched around group selection.",
            FutureOwnerModule = "spawning.groups",
            HotPathRisk = RebirthScenarioHotPathRisk.High,
            Authority = RebirthScenarioAuthority.Server,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Scenario spawn contribution should be cached group policies."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "sleepers.scenarioVolumes",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.SleeperVolume,
            SourcePath = "Harmony/Harmony_SleeperVolume.cs",
            CurrentConcern = "Scenario behavior affects sleeper volume checks.",
            FutureOwnerModule = "spawning.sleepers",
            HotPathRisk = RebirthScenarioHotPathRisk.High,
            Authority = RebirthScenarioAuthority.Server,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Must separate purge/hive/survivor policies from base sleeper lifecycle."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "trader.scenarioRules",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.Trader,
            SourcePath = "Harmony/Harmony_EntityTrader.cs",
            CurrentConcern = "Scenario-specific trader behavior exists in trader patches.",
            FutureOwnerModule = "traders.policy",
            HotPathRisk = RebirthScenarioHotPathRisk.Medium,
            Authority = RebirthScenarioAuthority.ServerAuthoritativeClientDisplay,
            MigrationRisk = RebirthScenarioMigrationRisk.Medium,
            Notes = "Trader policies should be scenario-bound, not global conditional checks."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "xui.classHud.scenario",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.XUi,
            SourcePath = "Scripts/XUIC/XUiC_ClassHUDRebirth.cs",
            CurrentConcern = "Scenario/class HUD logic is mixed with UI updates.",
            FutureOwnerModule = "ui.hud",
            HotPathRisk = RebirthScenarioHotPathRisk.High,
            Authority = RebirthScenarioAuthority.Client,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "UI should consume cached display state; scenario managers should not run expensive UI scans."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "xui.compass.scenario",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.CompassHud,
            SourcePath = "Scripts/XUIC/XUiC_CompassWindowRebirth.cs",
            CurrentConcern = "Scenario compass/marker display is mixed with compass HUD updates.",
            FutureOwnerModule = "ui.compass + map.markers",
            HotPathRisk = RebirthScenarioHotPathRisk.High,
            Authority = RebirthScenarioAuthority.Client,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Compass markers must be event-based or cached, not repeatedly recomputed."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "network.purgeProgress",
            Scenario = RebirthScenarioKind.Purge,
            SurfaceKind = RebirthScenarioSurfaceKind.NetworkPackage,
            SourcePath = "Scripts/Network/NetPackageCheckPurgeProgress.cs",
            CurrentConcern = "Purge progress has dedicated network package flow.",
            FutureOwnerModule = "scenario.purge.progress",
            HotPathRisk = RebirthScenarioHotPathRisk.Low,
            Authority = RebirthScenarioAuthority.ServerAuthoritativeClientDisplay,
            MigrationRisk = RebirthScenarioMigrationRisk.Medium,
            Notes = "Keep server authoritative; client receives display cache only."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "network.purgeDisplay",
            Scenario = RebirthScenarioKind.Purge,
            SurfaceKind = RebirthScenarioSurfaceKind.NetworkPackage,
            SourcePath = "Scripts/Network/NetPackageUpdatePurgeDisplay.cs",
            CurrentConcern = "Purge display updates use dedicated network package.",
            FutureOwnerModule = "scenario.purge.ui",
            HotPathRisk = RebirthScenarioHotPathRisk.Low,
            Authority = RebirthScenarioAuthority.ServerAuthoritativeClientDisplay,
            MigrationRisk = RebirthScenarioMigrationRisk.Medium,
            Notes = "Display updates should remain event-driven."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "network.purgeSetProgress",
            Scenario = RebirthScenarioKind.Purge,
            SurfaceKind = RebirthScenarioSurfaceKind.NetworkPackage,
            SourcePath = "Scripts/Network/NetPackageSetPurgeProgress.cs",
            CurrentConcern = "Purge progress setting/sync exists as direct package behavior.",
            FutureOwnerModule = "scenario.purge.progress",
            HotPathRisk = RebirthScenarioHotPathRisk.Low,
            Authority = RebirthScenarioAuthority.Server,
            MigrationRisk = RebirthScenarioMigrationRisk.Medium,
            Notes = "Must validate authority and avoid client-owned progress."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "network.sleeperVolumes",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.NetworkPackage,
            SourcePath = "Scripts/Network/NetPackageCheckSleeperVolumes.cs",
            CurrentConcern = "Sleeper volume checks are networked and scenario-sensitive.",
            FutureOwnerModule = "spawning.sleepers",
            HotPathRisk = RebirthScenarioHotPathRisk.Medium,
            Authority = RebirthScenarioAuthority.ServerAuthoritativeClientDisplay,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Must preserve dedicated server behavior and avoid client-only scenario truth."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "xml.prefab.includeExclude",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.XmlPrefabLists,
            SourcePath = "Config/Prefabs*.xml / IgnorePrefabsScenario.xml",
            CurrentConcern = "Scenario-specific prefab include/exclude logic exists in XML lists.",
            FutureOwnerModule = "scenario.xml.prefabs",
            HotPathRisk = RebirthScenarioHotPathRisk.None,
            Authority = RebirthScenarioAuthority.XmlLoadTime,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Must model prefab ownership and cleanup separately from runtime scenario checks."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "xml.entityclasses.scenario",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.XmlEntityClasses,
            SourcePath = "Config/entityclasses.xml",
            CurrentConcern = "Scenario entities and survivor-role entities share naming/tag space.",
            FutureOwnerModule = "scenario.xml.entities",
            HotPathRisk = RebirthScenarioHotPathRisk.None,
            Authority = RebirthScenarioAuthority.XmlLoadTime,
            MigrationRisk = RebirthScenarioMigrationRisk.MustDisambiguate,
            Notes = "Survivor scenario must be separated from survivor NPC/entity role concepts."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "xml.entitygroups.scenario",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.XmlEntityGroups,
            SourcePath = "Config/entitygroups.xml",
            CurrentConcern = "Scenario spawn/entity groups are XML-defined and runtime-consumed.",
            FutureOwnerModule = "spawning.groups + scenario.xml",
            HotPathRisk = RebirthScenarioHotPathRisk.None,
            Authority = RebirthScenarioAuthority.XmlLoadTime,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Resolve to cached spawn group policies at load/init time."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "xml.spawning.scenario",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.XmlSpawning,
            SourcePath = "Config/spawning.xml",
            CurrentConcern = "Scenario spawn behavior exists in XML and C# manager/patch logic.",
            FutureOwnerModule = "spawning.biomes + scenario.xml",
            HotPathRisk = RebirthScenarioHotPathRisk.None,
            Authority = RebirthScenarioAuthority.XmlLoadTime,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Must map XML spawn definitions to scenario policy caches."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "xml.items.buffs.loot.quests",
            Scenario = RebirthScenarioKind.Mixed,
            SurfaceKind = RebirthScenarioSurfaceKind.XmlCleanup,
            SourcePath = "Config/items.xml / buffs.xml / loot.xml / quests.xml / progression.xml",
            CurrentConcern = "Scenario features cross many XML files and cleanup layers.",
            FutureOwnerModule = "scenario.xml",
            HotPathRisk = RebirthScenarioHotPathRisk.None,
            Authority = RebirthScenarioAuthority.XmlLoadTime,
            MigrationRisk = RebirthScenarioMigrationRisk.High,
            Notes = "Need scenario XML ownership ledger before physical XML reorganization."
        },
        new RebirthScenarioSurfaceDecl
        {
            SurfaceId = "survivor.term.disambiguation",
            Scenario = RebirthScenarioKind.SurvivorEntityRole,
            SurfaceKind = RebirthScenarioSurfaceKind.Entity,
            SourcePath = "Multiple C# and XML files",
            CurrentConcern = "The token survivor is used heavily for NPC/entity roles and must not be assumed to mean scenario.",
            FutureOwnerModule = "entities.roles + scenario.survivor",
            HotPathRisk = RebirthScenarioHotPathRisk.Medium,
            Authority = RebirthScenarioAuthority.Shared,
            MigrationRisk = RebirthScenarioMigrationRisk.MustDisambiguate,
            Notes = "Introduce distinct concepts: ScenarioId.Survivor, EntityRole.Survivor, EntityTag.survivor."
        }
    };

    public static RebirthScenarioSurfaceDecl[] GetSnapshot()
    {
        RebirthScenarioSurfaceDecl[] copy = new RebirthScenarioSurfaceDecl[s_surfaces.Length];
        Array.Copy(s_surfaces, copy, copy.Length);
        return copy;
    }

    public static string GetSummaryReport()
    {
        int purge = 0;
        int hive = 0;
        int survivorScenario = 0;
        int survivorRole = 0;
        int mixed = 0;
        int hotHighOrWorse = 0;
        int mustDisambiguate = 0;

        for (int i = 0; i < s_surfaces.Length; i++)
        {
            RebirthScenarioSurfaceDecl s = s_surfaces[i];

            if (s.Scenario == RebirthScenarioKind.Purge)
                purge++;
            else if (s.Scenario == RebirthScenarioKind.Hive)
                hive++;
            else if (s.Scenario == RebirthScenarioKind.SurvivorScenario)
                survivorScenario++;
            else if (s.Scenario == RebirthScenarioKind.SurvivorEntityRole)
                survivorRole++;
            else if (s.Scenario == RebirthScenarioKind.Mixed)
                mixed++;

            if (s.HotPathRisk == RebirthScenarioHotPathRisk.High || s.HotPathRisk == RebirthScenarioHotPathRisk.Critical)
                hotHighOrWorse++;

            if (s.MigrationRisk == RebirthScenarioMigrationRisk.MustDisambiguate)
                mustDisambiguate++;
        }

        return "[RebirthScenarioLedger] Surfaces: " + s_surfaces.Length
            + "; purge-specific: " + purge
            + "; hive-specific: " + hive
            + "; survivor-scenario-specific: " + survivorScenario
            + "; survivor-entity-role: " + survivorRole
            + "; mixed: " + mixed
            + "; high/critical hot-path risk: " + hotHighOrWorse
            + "; must-disambiguate: " + mustDisambiguate
            + ". Ledger is read-only.";
    }

    public static string GetSurfaceReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthScenarioLedger] 2.6 scenario surface ledger.");
        sb.AppendLine("  Read-only. This records audited 2.6 scenario surfaces before scenario contracts are created.");
        sb.AppendLine("surface | scenario | kind | source | owner | hot risk | authority | migration risk | concern | notes");

        bool any = false;
        for (int i = 0; i < s_surfaces.Length; i++)
        {
            RebirthScenarioSurfaceDecl s = s_surfaces[i];
            if (!Matches(s, f))
                continue;

            any = true;
            sb.Append(s.SurfaceId).Append(" | ")
              .Append(s.Scenario).Append(" | ")
              .Append(s.SurfaceKind).Append(" | ")
              .Append(s.SourcePath).Append(" | ")
              .Append(s.FutureOwnerModule).Append(" | ")
              .Append(s.HotPathRisk).Append(" | ")
              .Append(s.Authority).Append(" | ")
              .Append(s.MigrationRisk).Append(" | ")
              .Append(s.CurrentConcern).Append(" | ")
              .AppendLine(s.Notes);
        }

        if (!any)
            sb.AppendLine("No surface matched the filter.");

        return sb.ToString();
    }

    public static string GetHotPathReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioLedger] hot-path scenario surfaces.");
        sb.AppendLine("surface | scenario | source | owner | hot risk | notes");

        for (int i = 0; i < s_surfaces.Length; i++)
        {
            RebirthScenarioSurfaceDecl s = s_surfaces[i];
            if (s.HotPathRisk != RebirthScenarioHotPathRisk.High && s.HotPathRisk != RebirthScenarioHotPathRisk.Critical)
                continue;

            sb.Append(s.SurfaceId).Append(" | ")
              .Append(s.Scenario).Append(" | ")
              .Append(s.SourcePath).Append(" | ")
              .Append(s.FutureOwnerModule).Append(" | ")
              .Append(s.HotPathRisk).Append(" | ")
              .AppendLine(s.Notes);
        }

        return sb.ToString();
    }

    public static string GetDisambiguationReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioLedger] required disambiguation.");
        sb.AppendLine("surface | source | risk | concern | notes");

        for (int i = 0; i < s_surfaces.Length; i++)
        {
            RebirthScenarioSurfaceDecl s = s_surfaces[i];
            if (s.MigrationRisk != RebirthScenarioMigrationRisk.MustDisambiguate)
                continue;

            sb.Append(s.SurfaceId).Append(" | ")
              .Append(s.SourcePath).Append(" | ")
              .Append(s.MigrationRisk).Append(" | ")
              .Append(s.CurrentConcern).Append(" | ")
              .AppendLine(s.Notes);
        }

        sb.AppendLine("Required distinct concepts:");
        sb.AppendLine("  - ScenarioId.Survivor");
        sb.AppendLine("  - EntityRole.Survivor");
        sb.AppendLine("  - EntityTag.survivor");
        sb.AppendLine("  - Survivor NPC systems");
        sb.AppendLine("  - Survivor scenario systems");
        return sb.ToString();
    }

    public static string GetArchitectureImpactReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioLedger] architecture impact from 2.6 audit:");
        sb.AppendLine("  1. Scenario layer must be first-class before gameplay migration.");
        sb.AppendLine("  2. Scenario contracts alone are not enough; surface ownership must be recorded first.");
        sb.AppendLine("  3. Scenarios must register policies/data into central module owners.");
        sb.AppendLine("  4. Scenarios must not directly own hot-method Harmony patches.");
        sb.AppendLine("  5. Scenario XML ownership must be tracked before physical XML reorganization.");
        sb.AppendLine("  6. Survivor scenario must be disambiguated from survivor NPC/entity concepts.");
        sb.AppendLine("  7. Runtime scenario state must become cached policy snapshots.");
        sb.AppendLine("  8. Dedicated server authority must be explicit for purge/progress/sleepers/spawns.");
        sb.AppendLine();
        sb.AppendLine("Updated phase order:");
        sb.AppendLine("  Phase 1W: Scenario Surface Ledger");
        sb.AppendLine("  Phase 1X: Scenario Contracts");
        sb.AppendLine("  Phase 1Y: Scenario Module Bindings");
        sb.AppendLine("  Phase 1Z: Scenario XML Ownership");
        sb.AppendLine("  Phase 2A: Scenario Runtime Policy Cache");
        sb.AppendLine("  Phase 2B: Scenario Performance Contracts");
        sb.AppendLine("  Phase 2C: Scenario Manager Boundaries");
        sb.AppendLine("  Phase 2D: Begin real scenario-aware migration");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthScenarioLedger] safety rules:");
        sb.AppendLine("  1. This ledger is read-only.");
        sb.AppendLine("  2. It does not activate/deactivate scenarios.");
        sb.AppendLine("  3. It does not parse XML at runtime.");
        sb.AppendLine("  4. It does not install/uninstall Harmony patches.");
        sb.AppendLine("  5. It does not change gameplay behavior.");
        sb.AppendLine("  6. It does not register scheduler jobs.");
        sb.AppendLine("  7. It exists to prevent guessing before scenario contracts are built.");
        return sb.ToString();
    }

    private static bool Matches(RebirthScenarioSurfaceDecl s, string filter)
    {
        if (s == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (s.SurfaceId != null && s.SurfaceId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.SourcePath != null && s.SourcePath.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.FutureOwnerModule != null && s.FutureOwnerModule.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.CurrentConcern != null && s.CurrentConcern.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.Scenario.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.SurfaceKind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
