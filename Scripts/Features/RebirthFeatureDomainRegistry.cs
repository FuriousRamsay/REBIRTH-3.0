using System;
using System.Text;

#nullable disable

public enum RebirthFeatureDomainKind
{
    Unknown,
    Scenario,
    Player,
    Combat,
    ZombieAi,
    NpcCompanion,
    Trader,
    Vehicle,
    ItemStats,
    Armor,
    Workstation,
    Crafting,
    HarvestSalvage,
    Paint,
    MapMarker,
    LootPersistence,
    Spawning,
    QuestEvent,
    WorldCaves,
    WeatherEnvironment,
    UiHud,
    Audio,
    Network,
    Persistence,
    DebugDiagnostics,
    Performance,
    ExternalCompatibility,
    MigrationDrift
}

public enum RebirthFeatureHotPathRisk
{
    Unknown,
    None,
    Low,
    Medium,
    High,
    Critical
}

public enum RebirthFeatureAuthorityRisk
{
    Unknown,
    None,
    ClientOnly,
    ServerOnly,
    ServerAuthoritativeClientDisplay,
    PeerToPeerRisk,
    DedicatedServerRisk,
    Mixed
}

public enum RebirthFeaturePersistenceRisk
{
    Unknown,
    None,
    Low,
    Medium,
    High,
    Critical
}

public enum RebirthFeatureMigrationStatus
{
    Unknown,
    LedgerOnly,
    NeedsSurfaceAudit,
    NeedsContract,
    NeedsDisambiguation,
    Blocked,
    ReadyForDependencyGraph,
    ReadyForMigrationLater
}

public sealed class RebirthFeatureDomainDecl
{
    public string DomainId;
    public string DisplayName;
    public RebirthFeatureDomainKind DomainKind;
    public string Current2_6Surfaces;
    public string XmlSurfaces;
    public string FutureOwnerModule;
    public string ScenarioDependency;
    public RebirthFeatureHotPathRisk HotPathRisk;
    public RebirthFeatureAuthorityRisk AuthorityRisk;
    public RebirthFeaturePersistenceRisk PersistenceRisk;
    public RebirthFeatureMigrationStatus MigrationStatus;
    public string TestRequirement;
    public string Notes;
}

/// <summary>
/// Read-only feature domain coverage ledger.
/// This ensures every major REBIRTH feature family has an explicit slot before gameplay migration.
/// </summary>
public static class RebirthFeatureDomainRegistry
{
    private static readonly RebirthFeatureDomainDecl[] s_domains = new[]
    {
        new RebirthFeatureDomainDecl
        {
            DomainId = "scenario.core",
            DisplayName = "Scenarios / Scenario Infrastructure",
            DomainKind = RebirthFeatureDomainKind.Scenario,
            Current2_6Surfaces = "customScenario, RebirthVariables, RebirthUtilities, RebirthManager, scenario-specific XML and patches",
            XmlSurfaces = "scenario options, prefab filters, entitygroups, spawning, progression, localization, cleanup",
            FutureOwnerModule = "scenario.runtime",
            ScenarioDependency = "all",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.Mixed,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Medium,
            MigrationStatus = RebirthFeatureMigrationStatus.ReadyForDependencyGraph,
            TestRequirement = "none, purge, hive, survivor, purge+hive; survivor blocked until disambiguated",
            Notes = "Scenario layer is a prerequisite, not a replacement for feature coverage."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "player.crawl",
            DisplayName = "Player Crawl / Crouch / Camera Transitions",
            DomainKind = RebirthFeatureDomainKind.Player,
            Current2_6Surfaces = "RebirthPlayerCrawlController, Harmony_PlayerCrawlController, Harmony_PlayerMoveController, camera/collision logic",
            XmlSurfaces = "custom game option/localization",
            FutureOwnerModule = "player.movement",
            ScenarioDependency = "common; possible cave/hive interactions",
            HotPathRisk = RebirthFeatureHotPathRisk.Critical,
            AuthorityRisk = RebirthFeatureAuthorityRisk.Mixed,
            PersistenceRisk = RebirthFeaturePersistenceRisk.High,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "thin plates, windows, corner blocks, vehicle/water, reload while crawling, third person, dedicated server",
            Notes = "Must avoid behavior regressions and debug/log overhead in movement hot paths."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "combat.damage",
            DisplayName = "Damage / Resistances / Stagger / Combat Rules",
            DomainKind = RebirthFeatureDomainKind.Combat,
            Current2_6Surfaces = "Harmony_EntityAlive, DamageEntity patches, armor resistance, headshot, explosive, friendly passthrough",
            XmlSurfaces = "items, buffs, progression, entityclasses, damage options",
            FutureOwnerModule = "combat.damage",
            ScenarioDependency = "common; scenario policies may register into damage but must not own patch",
            HotPathRisk = RebirthFeatureHotPathRisk.Critical,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Low,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "SP, dedicated server, P2P, melee/ranged/explosion, armor modifiers, friendly entities",
            Notes = "One owner for DamageEntity/EntityAlive hot adapters."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "zombie.ai.special",
            DisplayName = "Zombie AI Special Behaviors",
            DomainKind = RebirthFeatureDomainKind.ZombieAi,
            Current2_6Surfaces = "vehicle attack AI, block jump anticipation, fat zombie aura, spider climbing, crawl/repositioning, decoy aggro",
            XmlSurfaces = "entityclasses, entitygroups, buffs, items/hand items, spawning",
            FutureOwnerModule = "ai.zombie",
            ScenarioDependency = "common; hive/purge spawn contexts possible",
            HotPathRisk = RebirthFeatureHotPathRisk.Critical,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Medium,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsSurfaceAudit,
            TestRequirement = "SP/dedi, vehicle, door/hole/corner cases, horde night, stuck behavior, target switching",
            Notes = "AI must be split into policy modules instead of scattered patches."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "npc.companions",
            DisplayName = "NPC Companions / Followers / Dogs / Drones",
            DomainKind = RebirthFeatureDomainKind.NpcCompanion,
            Current2_6Surfaces = "EntityNPCRebirth, follower/dog/drone systems, silencer, dog respawn, friendly passthrough",
            XmlSurfaces = "entityclasses, entitygroups, items, buffs, dialogs, localization",
            FutureOwnerModule = "npc.companions",
            ScenarioDependency = "common; survivor token ambiguity requires caution",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.DedicatedServerRisk,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Critical,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "SP/dedi/P2P, chunk reload, owner persistence, respawn, target selection, silencer persistence",
            Notes = "Must not confuse survivor NPC/entity role with survivor scenario."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "decoys.aggro",
            DisplayName = "Decoys / Aggro Redirection",
            DomainKind = RebirthFeatureDomainKind.ZombieAi,
            Current2_6Surfaces = "decoy targeting code, zombie target switching, event zombie forced player target behavior",
            XmlSurfaces = "items, entityclasses, buffs",
            FutureOwnerModule = "ai.aggro",
            ScenarioDependency = "common; event spawns",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Medium,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "decoy placed before/after target acquired, player hit breaks cycle, decoy destroyed resumes target",
            Notes = "Target policy must be server-authoritative and event-based where possible."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "traders.behavior",
            DisplayName = "Trader Behavior / Attack / Death Rules",
            DomainKind = RebirthFeatureDomainKind.Trader,
            Current2_6Surfaces = "Harmony_EntityTrader, trader attackability, killall protection, respawn/flicker/disappear issues",
            XmlSurfaces = "entityclasses, spawning/prefab/trader definitions",
            FutureOwnerModule = "traders.policy",
            ScenarioDependency = "common; possible scenario-specific trader rules",
            HotPathRisk = RebirthFeatureHotPathRisk.Medium,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Critical,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "death, killall, static spawner, new world, different POI, zombie attack/retarget",
            Notes = "Trader state must not poison future worlds or new entities."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "vehicles.ai.interaction",
            DisplayName = "Vehicle Interaction / Vehicle Attack AI / Jump Anticipation",
            DomainKind = RebirthFeatureDomainKind.Vehicle,
            Current2_6Surfaces = "Harmony_FatZombieVehicleAI, zombie vehicle attack, jump anticipation, vehicle water/camera interactions",
            XmlSurfaces = "entityclasses, buffs/options/localization",
            FutureOwnerModule = "vehicles.ai",
            ScenarioDependency = "common; horde night policy",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.DedicatedServerRisk,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Low,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "SP/dedi, vehicle side/front, horde night, damage/sound/animation sync, client prediction",
            Notes = "Server/client calculation mismatches are a known risk."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "items.stats.randomization",
            DisplayName = "Item Stats / Quality Rolls / Vehicle Parts",
            DomainKind = RebirthFeatureDomainKind.ItemStats,
            Current2_6Surfaces = "ItemValue.ModifyValue, armor/item stat patches, vehicle part bonuses, rolled values",
            XmlSurfaces = "items, item_modifiers, progression, localization",
            FutureOwnerModule = "items.stats",
            ScenarioDependency = "common",
            HotPathRisk = RebirthFeatureHotPathRisk.Critical,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerAuthoritativeClientDisplay,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Critical,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "quality ranges, rolled persistence, tooltip/display, dedicated server, stat impact feel",
            Notes = "ItemValue.ModifyValue must avoid allocations and broad scans."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "armor.effects",
            DisplayName = "Armor Sets / Triggered Effects / Passives",
            DomainKind = RebirthFeatureDomainKind.Armor,
            Current2_6Surfaces = "armor triggered_effects, armor passives, resistance patches, scrapping mastery",
            XmlSurfaces = "items, buffs, progression, loot, localization",
            FutureOwnerModule = "armor.effects",
            ScenarioDependency = "common",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerAuthoritativeClientDisplay,
            PersistenceRisk = RebirthFeaturePersistenceRisk.High,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "passive placement, triggered effects, set changes, inventory changes, scrapping, server parity",
            Notes = "Effect ownership must be clear so item/stat hot paths stay bounded."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "workstations.crafting",
            DisplayName = "Workstations / Crafting / Remote Broadcast",
            DomainKind = RebirthFeatureDomainKind.Workstation,
            Current2_6Surfaces = "scrapping automation, recycler, remote crafting, broadcast containers, fuel/cancel animations",
            XmlSurfaces = "blocks, recipes, items, XUi, localization",
            FutureOwnerModule = "workstations",
            ScenarioDependency = "common",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.DedicatedServerRisk,
            PersistenceRisk = RebirthFeaturePersistenceRisk.High,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "queue empty hitch, short stacks, fuel, remote containers, chunk reload, server parity",
            Notes = "Known hitch area; automation must be carefully gated."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "harvest.salvage",
            DisplayName = "Harvest / Salvage / Vehicle Stump Rules",
            DomainKind = RebirthFeatureDomainKind.HarvestSalvage,
            Current2_6Surfaces = "harvest/salvage bonuses, wrench/ratchet, vehicle stump honey suppression, stealth salvage sound",
            XmlSurfaces = "blocks, items, buffs, loot",
            FutureOwnerModule = "harvest.salvage",
            ScenarioDependency = "common",
            HotPathRisk = RebirthFeatureHotPathRisk.Medium,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Low,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "wild/grown, per block, vehicle harvest, stealth sound reset, SP/dedi",
            Notes = "Must preserve round-down and avoid double-apply behavior."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "paint.render",
            DisplayName = "Paint / Render / Remote Paint",
            DomainKind = RebirthFeatureDomainKind.Paint,
            Current2_6Surfaces = "paint persistence, remote paint, render face/tint/drain cache behavior",
            XmlSurfaces = "blocks, items, XUi/localization",
            FutureOwnerModule = "paint.render",
            ScenarioDependency = "common",
            HotPathRisk = RebirthFeatureHotPathRisk.Critical,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerAuthoritativeClientDisplay,
            PersistenceRisk = RebirthFeaturePersistenceRisk.High,
            MigrationStatus = RebirthFeatureMigrationStatus.ReadyForDependencyGraph,
            TestRequirement = "chunk reload, server/client, render hot path, remote containers, persistence",
            Notes = "BlockShapeNew.renderFace must have a single owner and avoid per-face overhead."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "map.markers.reservations",
            DisplayName = "Map Markers / Nav Objects / Reserved Loot",
            DomainKind = RebirthFeatureDomainKind.MapMarker,
            Current2_6Surfaces = "boss loot markers, supply crate markers, reserved loot ownership, nav object setup, marker flicker",
            XmlSurfaces = "entityclasses, loot, localization",
            FutureOwnerModule = "map.markers (owned by RebirthBossEventNativeMapMarkerService for Boss Events) + loot.reservations",
            ScenarioDependency = "purge, common",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerAuthoritativeClientDisplay,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Critical,
            MigrationStatus = RebirthFeatureMigrationStatus.ReadyForDependencyGraph,
            TestRequirement = "spawn/destroy/login/relog, owner display, non-owner blocked, marker persistence, no flicker",
            Notes = "Boss Events now owns a bounded client marker adapter that updates existing NavObjects and only creates or removes them when synchronized event state changes."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "loot.persistence",
            DisplayName = "Loot Containers / Boss Loot / Supply Crates",
            DomainKind = RebirthFeatureDomainKind.LootPersistence,
            Current2_6Surfaces = "reserved loot containers, boss event loot, purge supply crates, restart persistence",
            XmlSurfaces = "loot, entityclasses, blocks, localization",
            FutureOwnerModule = "loot.persistence",
            ScenarioDependency = "purge, event systems",
            HotPathRisk = RebirthFeatureHotPathRisk.Medium,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Critical,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "owner/non-owner, relog, server restart, destroy cleanup, marker cleanup",
            Notes = "Persistence and marker cleanup must be designed together."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "spawning.events",
            DisplayName = "Entity Spawning / Event Spawning / Sleepers",
            DomainKind = RebirthFeatureDomainKind.Spawning,
            Current2_6Surfaces = "Harmony_EntityGroups, SleeperVolume, DynamicPrefabDecorator, event spawns, seeker/mercenary spawns",
            XmlSurfaces = "entitygroups, entityclasses, spawning, gamestages, sleeper volumes, prefabs",
            FutureOwnerModule = "spawning",
            ScenarioDependency = "purge, hive, common",
            HotPathRisk = RebirthFeatureHotPathRisk.Critical,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.High,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "SP/dedi/P2P, sleepers, blood moon, caves, event zombies, nav objects, forced targets",
            Notes = "Scenario policies feed spawning; spawning owns execution."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "quests.events.progression",
            DisplayName = "Quests / Game Events / Progression",
            DomainKind = RebirthFeatureDomainKind.QuestEvent,
            Current2_6Surfaces = "player entity spawn events, purge progression, seeker notifications, classes/genetics/perks",
            XmlSurfaces = "quests, progression, buffs, entitygroups, localization",
            FutureOwnerModule = "quests.events",
            ScenarioDependency = "purge, hive, survivor maybe; common",
            HotPathRisk = RebirthFeatureHotPathRisk.Medium,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerAuthoritativeClientDisplay,
            PersistenceRisk = RebirthFeaturePersistenceRisk.High,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsSurfaceAudit,
            TestRequirement = "quest state, relog, cave exclusion, scenario options, server parity",
            Notes = "Must keep cave tunnel and cave POI exclusion rules distinct."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "world.caves.descent",
            DisplayName = "Caves / The Descent / Worldgen",
            DomainKind = RebirthFeatureDomainKind.WorldCaves,
            Current2_6Surfaces = "cave tunnel helper, cave POI tags, DynamicPrefabDecorator, worldgen/prefab spawn rules",
            XmlSurfaces = "prefabs, spawning, entitygroups, biome rules, tags",
            FutureOwnerModule = "world.caves",
            ScenarioDependency = "hive",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Medium,
            MigrationStatus = RebirthFeatureMigrationStatus.ReadyForDependencyGraph,
            TestRequirement = "cave tunnel, cave POI, underground POI, blood moon, spawnmanagerbiomes, player events",
            Notes = "Do not reuse tunnel-only helper for cave POI checks."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "weather.environment",
            DisplayName = "Storms / Weather / Lighting / Environment",
            DomainKind = RebirthFeatureDomainKind.WeatherEnvironment,
            Current2_6Surfaces = "Harmony_WorldEnvironment, storms, daylight/nightfall/moon brightness, FSR/OBS concern",
            XmlSurfaces = "weather/environment options/localization",
            FutureOwnerModule = "environment.weather",
            ScenarioDependency = "common",
            HotPathRisk = RebirthFeatureHotPathRisk.Medium,
            AuthorityRisk = RebirthFeatureAuthorityRisk.Mixed,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Low,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "storm start/stop, dedicated server, host/client, visual settings, OBS/FSR edge case handoff",
            Notes = "Environment patches must remain isolated and reversible."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "ui.hud.compass",
            DisplayName = "UI / HUD / Compass / Crosshair / Healthbars",
            DomainKind = RebirthFeatureDomainKind.UiHud,
            Current2_6Surfaces = "XUiC_ClassHUDRebirth, XUiC_CompassWindowRebirth, crosshair commands, healthbars 3.0 drift, walkman/heatmap HUD",
            XmlSurfaces = "XUi_Common, XUi_InGame, XUi_Menu, templates, localization",
            FutureOwnerModule = "ui.hud",
            ScenarioDependency = "purge display, common, external compatibility",
            HotPathRisk = RebirthFeatureHotPathRisk.Critical,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ClientOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Low,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "idle updates, compass smoothness, scenario display snapshots, 3.0 XUi split, healthbars option",
            Notes = "UI should consume cached state; no heavy scenario calculations in update loops."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "audio.sounds",
            DisplayName = "Audio / Silencer / Hit Sounds / Vehicle Sounds",
            DomainKind = RebirthFeatureDomainKind.Audio,
            Current2_6Surfaces = "NPC silencer, vehicle hit sounds, dog hurt sound suppression, LoadAudioPatch loop cache",
            XmlSurfaces = "sounds, items, entityclasses, dialogs",
            FutureOwnerModule = "audio",
            ScenarioDependency = "common",
            HotPathRisk = RebirthFeatureHotPathRisk.Medium,
            AuthorityRisk = RebirthFeatureAuthorityRisk.Mixed,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Medium,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "SP/dedi, chunk reload, NPC equipped states, hit animation sync, double sound prevention",
            Notes = "Audio correctness and network timing need separate tests."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "network.authority",
            DisplayName = "Networking / Dedicated Server Authority",
            DomainKind = RebirthFeatureDomainKind.Network,
            Current2_6Surfaces = "NetPackage* purge/sleepers/display/progress, server/client mismatches, P2P XP/authority issues",
            XmlSurfaces = "none directly; affected by scenario and feature XML",
            FutureOwnerModule = "network.authority",
            ScenarioDependency = "all server-authoritative systems",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.DedicatedServerRisk,
            PersistenceRisk = RebirthFeaturePersistenceRisk.High,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "SP/dedi/P2P matrix for every stateful feature",
            Notes = "Network authority should become a cross-cutting contract before migration."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "persistence.saveLoad",
            DisplayName = "Persistence / Save-Load / Relog / Restart",
            DomainKind = RebirthFeatureDomainKind.Persistence,
            Current2_6Surfaces = "reserved loot, markers, NPC followers, rolled item values, paint, trader state, crawl state",
            XmlSurfaces = "feature-dependent",
            FutureOwnerModule = "persistence",
            ScenarioDependency = "all stateful systems",
            HotPathRisk = RebirthFeatureHotPathRisk.Medium,
            AuthorityRisk = RebirthFeatureAuthorityRisk.ServerOnly,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Critical,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "relog, server restart, chunk unload/reload, new game, different world",
            Notes = "Persistence contracts should exist before migrating any stateful feature."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "debug.diagnostics",
            DisplayName = "Debug / Diagnostics / Profiling",
            DomainKind = RebirthFeatureDomainKind.DebugDiagnostics,
            Current2_6Surfaces = "profilers, diagnostic Harmony wrappers, debug logging, stack traces, object[] postfix concerns",
            XmlSurfaces = "options/localization maybe",
            FutureOwnerModule = "diagnostics",
            ScenarioDependency = "all",
            HotPathRisk = RebirthFeatureHotPathRisk.Critical,
            AuthorityRisk = RebirthFeatureAuthorityRisk.Mixed,
            PersistenceRisk = RebirthFeaturePersistenceRisk.None,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "release vs profiling vs stripped comparison, hot-path no allocation checks",
            Notes = "All debug string/work construction must be gated before evaluation."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "performance.harness",
            DisplayName = "Performance Harness / Build Profiles / Stripped Comparisons",
            DomainKind = RebirthFeatureDomainKind.Performance,
            Current2_6Surfaces = "build_profiling, perf toggles, CSV summaries, stripped comparison desire",
            XmlSurfaces = "none directly",
            FutureOwnerModule = "performance.testing",
            ScenarioDependency = "all scenario sets",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.Mixed,
            PersistenceRisk = RebirthFeaturePersistenceRisk.None,
            MigrationStatus = RebirthFeatureMigrationStatus.ReadyForDependencyGraph,
            TestRequirement = "standing, running, driving, chunk area hitch, scenario matrix, release/profiling/stripped",
            Notes = "Must validate that diagnostics do not affect release builds."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "external.compatibility",
            DisplayName = "External Compatibility / Vendored Projects",
            DomainKind = RebirthFeatureDomainKind.ExternalCompatibility,
            Current2_6Surfaces = "SCore/Score, Dayuppy, TormentedEmu, Vandracon, IzayoGuns, SonjaArmor, Cleanup external entries",
            XmlSurfaces = "external compatibility XML and cleanup layers",
            FutureOwnerModule = "external.compat",
            ScenarioDependency = "common; feature-specific",
            HotPathRisk = RebirthFeatureHotPathRisk.High,
            AuthorityRisk = RebirthFeatureAuthorityRisk.Mixed,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Medium,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "compat on/off, external XML, ownership boundaries, 3.0 drift",
            Notes = "Vendored governance is separate from third-party compatibility XML."
        },
        new RebirthFeatureDomainDecl
        {
            DomainId = "migration.3_0_drift",
            DisplayName = "3.0 Migration Drift / API Changes",
            DomainKind = RebirthFeatureDomainKind.MigrationDrift,
            Current2_6Surfaces = "BlockDoorSecure, FastTags changes, ConsoleCmd overrides, healthbars game option, XUi split",
            XmlSurfaces = "XUi_Common/XUi_InGame/XUi_Menu/templates.xml and version-dependent XML",
            FutureOwnerModule = "migration.drift",
            ScenarioDependency = "all",
            HotPathRisk = RebirthFeatureHotPathRisk.Medium,
            AuthorityRisk = RebirthFeatureAuthorityRisk.Mixed,
            PersistenceRisk = RebirthFeaturePersistenceRisk.Medium,
            MigrationStatus = RebirthFeatureMigrationStatus.NeedsContract,
            TestRequirement = "compile, runtime load, UI open, commands, healthbars, XUi",
            Notes = "Version drift must be tracked explicitly so fixes are not mixed into feature logic."
        }
    };

    public static string GetSummaryReport()
    {
        int total = s_domains.Length;
        int criticalHot = 0;
        int networkRisk = 0;
        int persistenceHigh = 0;
        int blockedOrNeedsDisambiguation = 0;

        for (int i = 0; i < s_domains.Length; i++)
        {
            RebirthFeatureDomainDecl d = s_domains[i];

            if (d.HotPathRisk == RebirthFeatureHotPathRisk.Critical)
                criticalHot++;

            if (d.AuthorityRisk == RebirthFeatureAuthorityRisk.DedicatedServerRisk
                || d.AuthorityRisk == RebirthFeatureAuthorityRisk.PeerToPeerRisk
                || d.AuthorityRisk == RebirthFeatureAuthorityRisk.Mixed
                || d.AuthorityRisk == RebirthFeatureAuthorityRisk.ServerAuthoritativeClientDisplay)
                networkRisk++;

            if (d.PersistenceRisk == RebirthFeaturePersistenceRisk.High
                || d.PersistenceRisk == RebirthFeaturePersistenceRisk.Critical)
                persistenceHigh++;

            if (d.MigrationStatus == RebirthFeatureMigrationStatus.Blocked
                || d.MigrationStatus == RebirthFeatureMigrationStatus.NeedsDisambiguation)
                blockedOrNeedsDisambiguation++;
        }

        return "[RebirthFeatures] Domains: " + total
            + "; critical hot-path: " + criticalHot
            + "; network/authority risk: " + networkRisk
            + "; high/critical persistence risk: " + persistenceHigh
            + "; blocked/disambiguation: " + blockedOrNeedsDisambiguation
            + ". Ledger is read-only.";
    }

    public static string GetDomainReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(32768);
        sb.AppendLine("[RebirthFeatures] feature domain coverage ledger.");
        sb.AppendLine("  Read-only. Every major feature family should have a slot before migration.");
        sb.AppendLine("domain | display | kind | owner | scenario | hot | authority | persistence | status | tests | 2.6 surfaces | xml | notes");

        bool any = false;
        for (int i = 0; i < s_domains.Length; i++)
        {
            RebirthFeatureDomainDecl d = s_domains[i];
            if (!Matches(d, f))
                continue;

            any = true;
            sb.Append(d.DomainId).Append(" | ")
              .Append(d.DisplayName).Append(" | ")
              .Append(d.DomainKind).Append(" | ")
              .Append(d.FutureOwnerModule).Append(" | ")
              .Append(d.ScenarioDependency).Append(" | ")
              .Append(d.HotPathRisk).Append(" | ")
              .Append(d.AuthorityRisk).Append(" | ")
              .Append(d.PersistenceRisk).Append(" | ")
              .Append(d.MigrationStatus).Append(" | ")
              .Append(d.TestRequirement).Append(" | ")
              .Append(d.Current2_6Surfaces).Append(" | ")
              .Append(d.XmlSurfaces).Append(" | ")
              .AppendLine(d.Notes);
        }

        if (!any)
            sb.AppendLine("No feature domain matched the filter.");

        return sb.ToString();
    }

    public static string GetHotPathReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFeatures] high/critical hot-path feature domains.");
        sb.AppendLine("domain | owner | hot risk | tests | notes");

        for (int i = 0; i < s_domains.Length; i++)
        {
            RebirthFeatureDomainDecl d = s_domains[i];
            if (d.HotPathRisk != RebirthFeatureHotPathRisk.High
                && d.HotPathRisk != RebirthFeatureHotPathRisk.Critical)
                continue;

            sb.Append(d.DomainId).Append(" | ")
              .Append(d.FutureOwnerModule).Append(" | ")
              .Append(d.HotPathRisk).Append(" | ")
              .Append(d.TestRequirement).Append(" | ")
              .AppendLine(d.Notes);
        }

        return sb.ToString();
    }

    public static string GetNetworkReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFeatures] network/authority-sensitive feature domains.");
        sb.AppendLine("domain | owner | authority risk | tests | notes");

        for (int i = 0; i < s_domains.Length; i++)
        {
            RebirthFeatureDomainDecl d = s_domains[i];
            if (d.AuthorityRisk == RebirthFeatureAuthorityRisk.None
                || d.AuthorityRisk == RebirthFeatureAuthorityRisk.ClientOnly)
                continue;

            sb.Append(d.DomainId).Append(" | ")
              .Append(d.FutureOwnerModule).Append(" | ")
              .Append(d.AuthorityRisk).Append(" | ")
              .Append(d.TestRequirement).Append(" | ")
              .AppendLine(d.Notes);
        }

        return sb.ToString();
    }

    public static string GetPersistenceReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFeatures] persistence-sensitive feature domains.");
        sb.AppendLine("domain | owner | persistence risk | tests | notes");

        for (int i = 0; i < s_domains.Length; i++)
        {
            RebirthFeatureDomainDecl d = s_domains[i];
            if (d.PersistenceRisk != RebirthFeaturePersistenceRisk.High
                && d.PersistenceRisk != RebirthFeaturePersistenceRisk.Critical)
                continue;

            sb.Append(d.DomainId).Append(" | ")
              .Append(d.FutureOwnerModule).Append(" | ")
              .Append(d.PersistenceRisk).Append(" | ")
              .Append(d.TestRequirement).Append(" | ")
              .AppendLine(d.Notes);
        }

        return sb.ToString();
    }

    public static string GetScenarioReport()
    {
        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthFeatures] scenario-dependent feature domains.");
        sb.AppendLine("domain | scenario dependency | owner | notes");

        for (int i = 0; i < s_domains.Length; i++)
        {
            RebirthFeatureDomainDecl d = s_domains[i];
            if (string.IsNullOrEmpty(d.ScenarioDependency)
                || d.ScenarioDependency.Equals("common", StringComparison.OrdinalIgnoreCase))
                continue;

            sb.Append(d.DomainId).Append(" | ")
              .Append(d.ScenarioDependency).Append(" | ")
              .Append(d.FutureOwnerModule).Append(" | ")
              .AppendLine(d.Notes);
        }

        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthFeatures] blocked/disambiguation feature domains.");
        sb.AppendLine("domain | status | reason | notes");

        for (int i = 0; i < s_domains.Length; i++)
        {
            RebirthFeatureDomainDecl d = s_domains[i];
            if (d.MigrationStatus != RebirthFeatureMigrationStatus.Blocked
                && d.MigrationStatus != RebirthFeatureMigrationStatus.NeedsDisambiguation)
                continue;

            sb.Append(d.DomainId).Append(" | ")
              .Append(d.MigrationStatus).Append(" | ")
              .Append(d.TestRequirement).Append(" | ")
              .AppendLine(d.Notes);
        }

        sb.AppendLine("Note: domains with NeedsContract/NeedsSurfaceAudit are not blocked, but still cannot migrate yet.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthFeatures] safety rules:");
        sb.AppendLine("  1. Ledger is read-only.");
        sb.AppendLine("  2. No gameplay behavior is connected.");
        sb.AppendLine("  3. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  4. No runtime XML parsing occurs.");
        sb.AppendLine("  5. No scenario activation/deactivation occurs.");
        sb.AppendLine("  6. No scheduler jobs are registered.");
        sb.AppendLine("  7. Every feature domain needs dependency/test/authority/persistence coverage before migration.");
        sb.AppendLine("  8. This ledger prevents feature categories from being accidentally omitted.");
        return sb.ToString();
    }

    private static bool Matches(RebirthFeatureDomainDecl d, string filter)
    {
        if (d == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (d.DomainId != null && d.DomainId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.DisplayName != null && d.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.Current2_6Surfaces != null && d.Current2_6Surfaces.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.XmlSurfaces != null && d.XmlSurfaces.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.FutureOwnerModule != null && d.FutureOwnerModule.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.ScenarioDependency != null && d.ScenarioDependency.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.TestRequirement != null && d.TestRequirement.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.DomainKind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.HotPathRisk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.AuthorityRisk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.PersistenceRisk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (d.MigrationStatus.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
