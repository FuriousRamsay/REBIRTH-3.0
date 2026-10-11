using System;
using System.Text;

#nullable disable

public enum RebirthOptionKind
{
    Unknown,
    Bool,
    Int,
    Float,
    String,
    EnumLike
}

public enum RebirthOptionMigrationPolicy
{
    RebirthExclusive,
    Native30WinsMigrateValue,
    Native30WinsDoNotMigrate,
    RebirthOverridesNative,
    ManualReviewRequired
}

public enum RebirthOptionVisibilityPolicy
{
    Visible,
    Hidden,
    ReadOnly,
    Removed
}

public sealed class RebirthOptionMigrationDecl
{
    public string RebirthName;
    public string CurrentDefault26;
    public RebirthOptionKind Kind;
    public string CurrentValues;
    public string OwnerModuleId;

    public string VanillaEquivalent30;
    public string SandboxOptionCategoryHint30;
    public string SandboxOptionNameHint30;

    public RebirthOptionMigrationPolicy MigrationPolicy;
    public RebirthOptionVisibilityPolicy Visibility26;
    public RebirthOptionVisibilityPolicy Visibility30;

    public bool ServerAuthoritative;
    public bool RequiresWorldReload;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// Read-only seed ledger for the 2.6 -> 3.0 option migration model.
/// This does not read or write GamePrefs and does not alter gameplay options.
/// </summary>
public static class RebirthOptionMigrationRegistry
{
    private static readonly RebirthOptionMigrationDecl[] s_options = new[]
    {
        new RebirthOptionMigrationDecl
        {
            RebirthName = "Theme", CurrentDefault26 = "None", Kind = RebirthOptionKind.EnumLike,
            CurrentValues = "None/Purge", OwnerModuleId = "purge.theme", VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "World", SandboxOptionNameHint30 = "World Theme",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible, Visibility30 = RebirthOptionVisibilityPolicy.Hidden,
            ServerAuthoritative = true, RequiresWorldReload = true,
            Evidence = "PurgeTheme/RESEARCH_AND_IMPLEMENTATION_PLAN_20261005.txt",
            Notes = "Append-only ID71. No inferred import of 2.6 scenario files. Purge selected for a new save is retained across saved-world options, presets, pasted codes and dedicated configuration overrides. Release gate hides unfinished theme."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "ShowClearedPois", CurrentDefault26 = "Off", Kind = RebirthOptionKind.Bool,
            CurrentValues = "Off/On", OwnerModuleId = "purge.theme", VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Quality of Life", SandboxOptionNameHint30 = "Show Cleared POIs",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Removed, Visibility30 = RebirthOptionVisibilityPolicy.Hidden,
            ServerAuthoritative = true, RequiresWorldReload = true,
            Evidence = "PurgeTheme/RESEARCH_AND_IMPLEMENTATION_PLAN_20261005.txt",
            Notes = "Append-only ID72. Configured preference survives Purge forcing tracking On. No native respawn override under None."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "ScrollbarMode",
            CurrentDefault26 = "Smooth",
            Kind = RebirthOptionKind.EnumLike,
            CurrentValues = "Smooth/Paged",
            OwnerModuleId = "ui.scrollbar.paging",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Quality of Life",
            SandboxOptionNameHint30 = "Scrollbar movement",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Removed,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "ScrollbarPaging source/codec qualification",
            Notes = "Append-only ID 70; old snapshots stay Smooth. UI-only paging is carried by the normal world sandbox code."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomHordeNight",
            CurrentDefault26 = "<existing 2.6 value>",
            Kind = RebirthOptionKind.EnumLike,
            CurrentValues = "existing REBIRTH values",
            OwnerModuleId = "horde.bloodmoon",
            VanillaEquivalent30 = "BloodMoon* cluster",
            SandboxOptionCategoryHint30 = "Blood Moon",
            SandboxOptionNameHint30 = "BloodMoon*",
            MigrationPolicy = RebirthOptionMigrationPolicy.ManualReviewRequired,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.ReadOnly,
            ServerAuthoritative = true,
            RequiresWorldReload = true,
            Evidence = "P6",
            Notes = "Critical semantic mapping trap: HordeNight names conceptually map to 3.0 BloodMoon options. Do not rely on token matching."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomHordeNightTurbo",
            CurrentDefault26 = "<existing 2.6 value>",
            Kind = RebirthOptionKind.Bool,
            OwnerModuleId = "horde.bloodmoon",
            VanillaEquivalent30 = "BloodMoon* cluster",
            SandboxOptionCategoryHint30 = "Blood Moon",
            SandboxOptionNameHint30 = "BloodMoon*",
            MigrationPolicy = RebirthOptionMigrationPolicy.ManualReviewRequired,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.ReadOnly,
            ServerAuthoritative = true,
            RequiresWorldReload = true,
            Evidence = "P6",
            Notes = "Must be manually mapped to native 3.0 blood-moon behavior if an equivalent exists."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomHeadshotMultiplier",
            CurrentDefault26 = "<existing 2.6 value>",
            Kind = RebirthOptionKind.Float,
            OwnerModuleId = "combat.damage",
            VanillaEquivalent30 = "HeadshotMultiplier",
            SandboxOptionCategoryHint30 = "Combat",
            SandboxOptionNameHint30 = "HeadshotMultiplier",
            MigrationPolicy = RebirthOptionMigrationPolicy.Native30WinsMigrateValue,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Hidden,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "P6",
            Notes = "High-confidence native 3.0 equivalent from option triage."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomFeralSense",
            CurrentDefault26 = "<existing 2.6 value>",
            Kind = RebirthOptionKind.EnumLike,
            OwnerModuleId = "entity.sense",
            VanillaEquivalent30 = "FeralSense",
            SandboxOptionCategoryHint30 = "Zombie",
            SandboxOptionNameHint30 = "FeralSense",
            MigrationPolicy = RebirthOptionMigrationPolicy.Native30WinsMigrateValue,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Hidden,
            ServerAuthoritative = true,
            RequiresWorldReload = true,
            Evidence = "P6",
            Notes = "High-confidence native 3.0 equivalent from option triage."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomZombieDigging",
            CurrentDefault26 = "<existing 2.6 value>",
            Kind = RebirthOptionKind.Bool,
            OwnerModuleId = "zombie.ai",
            VanillaEquivalent30 = "ZombieDigging",
            SandboxOptionCategoryHint30 = "Zombie",
            SandboxOptionNameHint30 = "ZombieDigging",
            MigrationPolicy = RebirthOptionMigrationPolicy.Native30WinsMigrateValue,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Hidden,
            ServerAuthoritative = true,
            RequiresWorldReload = true,
            Evidence = "P6",
            Notes = "High-confidence native 3.0 equivalent from option triage."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomZombiesDestroyAreas",
            CurrentDefault26 = "false",
            Kind = RebirthOptionKind.Bool,
            CurrentValues = "false,true",
            OwnerModuleId = "zombie.ai.destroyarea",
            VanillaEquivalent30 = "EAIDestroyArea",
            SandboxOptionCategoryHint30 = "Entities",
            SandboxOptionNameHint30 = "ZombiesDestroyAreas",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 Harmony_EAIDestroyArea and 3.1 EAIDestroyArea",
            Notes = "Option ID 42. False suppresses broad EAIDestroyArea target selection while preserving direct obstruction breaking."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomArmorLootProgression",
            CurrentDefault26 = "<existing 2.6 value>",
            Kind = RebirthOptionKind.EnumLike,
            OwnerModuleId = "loot.armor",
            VanillaEquivalent30 = "ArmorLootCount",
            SandboxOptionCategoryHint30 = "Loot",
            SandboxOptionNameHint30 = "ArmorLootCount",
            MigrationPolicy = RebirthOptionMigrationPolicy.ManualReviewRequired,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.ReadOnly,
            ServerAuthoritative = true,
            RequiresWorldReload = true,
            Evidence = "P6",
            Notes = "High-confidence conceptual area, but value semantics need manual confirmation before migration."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomCrosshairHUD",
            CurrentDefault26 = "<existing 2.6 value>",
            Kind = RebirthOptionKind.Bool,
            OwnerModuleId = "ui.crosshair",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Modded",
            SandboxOptionNameHint30 = "CustomCrosshairHUD",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = false,
            RequiresWorldReload = false,
            Evidence = "P6/P4",
            Notes = "Morecrosshairs is player-facing, but implementation must not patch GUIUtils.DrawLine globally."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomRepeatPOI",
            CurrentDefault26 = "unconfirmed",
            Kind = RebirthOptionKind.Bool,
            CurrentValues = "false,true",
            OwnerModuleId = "trader.jobs.repeatpoi",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "World Generation",
            SandboxOptionNameHint30 = "RepeatPoiJobs",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 trader-job source and Repeat POI design",
            Notes = "Legacy Boolean import maps false to None and true to Unlimited. Option ID 19 remains stable while the 3.0 value domain expands to five policies."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "TraderJobList",
            CurrentDefault26 = "Random (historical unconditional regeneration)",
            Kind = RebirthOptionKind.EnumLike,
            CurrentValues = "Fixed,Random",
            OwnerModuleId = "trader.jobs.listrefresh",
            VanillaEquivalent30 = "QuestEventManager trader-job cache",
            SandboxOptionCategoryHint30 = "Trader Jobs",
            SandboxOptionNameHint30 = "TraderJobList",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 GetQuestList patch and 3.0 b259 NetPackageNPCQuestList/QuestEventManager",
            Notes = "Option ID 21. Fixed leaves vanilla cache behavior untouched. Random invalidates only the requesting player/trader cache entry on the server when the FetchList event is processed."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomRestrictiveHealing",
            CurrentDefault26 = "true",
            Kind = RebirthOptionKind.Bool,
            CurrentValues = "false,true",
            OwnerModuleId = "player.healing.overlap",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Player",
            SandboxOptionNameHint30 = "PreventHealingOverlap",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 restrictive-healing source and approved 3.0 design",
            Notes = "Option ID 22. The first 3.0 implementation preserves the legacy >10 pending-medical-healing threshold and permits bleed-stopping treatments while actively bleeding."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomAlwaysStagger",
            CurrentDefault26 = "headshotsonly (later intended default; adjacent snapshots retained true)",
            Kind = RebirthOptionKind.EnumLike,
            CurrentValues = "false,headshotsonly,true",
            OwnerModuleId = "combat.stagger",
            VanillaEquivalent30 = "EntityAlive pain response and pain resistance",
            SandboxOptionCategoryHint30 = "Combat",
            SandboxOptionNameHint30 = "AlwaysStagger",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 Always Stagger source-level analysis and 3.0 b259 damage-response path",
            Notes = "Option ID 23. Disabled preserves vanilla. Headshots Only is the 3.0 default. All Qualifying Hits forces nonfatal positive enemy hits into the pain path. Animals are excluded."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomWeatherFog",
            CurrentDefault26 = "default",
            Kind = RebirthOptionKind.EnumLike,
            CurrentValues = "none,lower,default,higher,highest",
            OwnerModuleId = "environment.weatherfog",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Environment",
            SandboxOptionNameHint30 = "WeatherFogBehavior / WeatherFogIntensity",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 Weather Fog source analysis and 3.0 b259 WorldEnvironment.SpectrumsFrameUpdate",
            Notes = "Option IDs 24 and 25. The legacy 2.6 none value maps to Fog Behavior Disabled. Fog Intensity also provides a separate None value for zero ordinary above-water fog while retaining Dynamic or Static behavior. Existing persisted intensity indices remain compatible: old High and Very High slots now display as Heavy and Very Heavy. Rendering is applied locally while the world-authored policy is synchronized through the REBIRTH sandbox code."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomUniformAtmosphere",
            CurrentDefault26 = "false",
            Kind = RebirthOptionKind.Bool,
            CurrentValues = "false,true",
            OwnerModuleId = "environment.uniformatmosphere",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Environment",
            SandboxOptionNameHint30 = "UniformAtmosphere",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 Uniform Atmosphere source analysis and 3.0 b259 WorldEnvironment.SpectrumsFrameUpdate",
            Notes = "Option ID 26. Disabled preserves active-biome spectra. Enabled substitutes a cached 100% pine-forest BiomeIntensity for Sky, Sun, Moon, Fog, and FogFade only. Time, weather, ambient derivation, underwater fog, and gameplay biome identity remain base-game controlled."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomPOIRisk",
            CurrentDefault26 = "default",
            Kind = RebirthOptionKind.String,
            CurrentValues = "none,low,medium,high",
            OwnerModuleId = "spawning.sleepers.poirisk",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Gameplay",
            SandboxOptionNameHint30 = "PoiRisk",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 POI Risk code and REBIRTH 3.0 b259 SleeperVolume.UpdateSpawn integration",
            Notes = "Option ID 27. The 3.0 display values are None/Low/Medium/High and Medium is the default.  Adds only a fading POI-tier bonus because native 3.1 SleeperVolume gamestage already includes biome scaling. Available only with Gamestage zombie progression; Biome progression forces None. Restores the native volume gamestage after the authoritative spawn call and never alters global player gamestage."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomPOISense",
            CurrentDefault26 = "never",
            Kind = RebirthOptionKind.String,
            CurrentValues = "never,dayonly,nightonly,always",
            OwnerModuleId = "spawning.sleepers.poisense",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Gameplay",
            SandboxOptionNameHint30 = "PoiSenseSchedule / PoiSenseIntensity",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = false,
            Evidence = "REBIRTH 2.6 EntityAlive sleeper sight/hearing patches and 3.0 b259 SleeperVolume.CheckTouching",
            Notes = "Option IDs 28 and 29. Schedule preserves Never/Day Only/Night Only/Always. Intensity is Low/Medium/High. Server wakes only already-instantiated sleepers within the same prefab under 8/12/16 active caps and 2/4/6 wakes per second."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomRancherRangedAttack",
            CurrentDefault26 = "false",
            Kind = RebirthOptionKind.Bool,
            CurrentValues = "false,true",
            OwnerModuleId = "entities.rancher.rangedattack",
            VanillaEquivalent30 = "zombieRancher AITask RangedAttackTarget",
            SandboxOptionCategoryHint30 = "Entities",
            SandboxOptionNameHint30 = "RancherRangedAttack",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthOverridesNative,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = true,
            Evidence = "REBIRTH 2.6 XmlPatchConditionEvaluator isoption patch and 3.1 zombieRancher AITask",
            Notes = "Option ID 43. Default Off applies the standard zombieTemplateMale AITask to Rancher variants. On restores the native Rancher RangedAttackTarget bee-swarm task through a conditional entityclasses.xml patch. Plague Spitter aliases receive an explicit ranged-task override in the Off branch so their separate ranged identity is not removed by Rancher inheritance."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "PlayerProgression",
            CurrentDefault26 = "Base Game (implicit before Survivor feature)",
            Kind = RebirthOptionKind.EnumLike,
            CurrentValues = "BaseGame,Rebirth",
            OwnerModuleId = "survivor.progression",
            VanillaEquivalent30 = "native perk/magazine progression",
            SandboxOptionCategoryHint30 = "Character",
            SandboxOptionNameHint30 = "Player Progression",
            MigrationPolicy = RebirthOptionMigrationPolicy.RebirthExclusive,
            Visibility26 = RebirthOptionVisibilityPolicy.Removed,
            Visibility30 = RebirthOptionVisibilityPolicy.Visible,
            ServerAuthoritative = true,
            RequiresWorldReload = true,
            Evidence = "Survivor Chunk 01 source audit + CHAR-020",
            Notes = "Option ID 61. New worlds default to Rebirth. Every U-and-earlier sandbox code and every legacy world without a RebirthSandboxOptions.xml snapshot resolves to Base Game so existing saves never silently acquire the Survivor progression replacement."
        },
        new RebirthOptionMigrationDecl
        {
            RebirthName = "CustomPathSmoothing",
            CurrentDefault26 = "true",
            Kind = RebirthOptionKind.Bool,
            OwnerModuleId = "pathing.smoothing",
            VanillaEquivalent30 = "",
            SandboxOptionCategoryHint30 = "Modded",
            SandboxOptionNameHint30 = "CustomPathSmoothing",
            MigrationPolicy = RebirthOptionMigrationPolicy.ManualReviewRequired,
            Visibility26 = RebirthOptionVisibilityPolicy.Visible,
            Visibility30 = RebirthOptionVisibilityPolicy.ReadOnly,
            ServerAuthoritative = true,
            RequiresWorldReload = true,
            Evidence = "P6/P4",
            Notes = "Currently live/enabled, but A* internals are excluded from native absorption by default."
        }
    };

    public static RebirthOptionMigrationDecl[] GetSnapshot()
    {
        RebirthOptionMigrationDecl[] copy = new RebirthOptionMigrationDecl[s_options.Length];
        Array.Copy(s_options, copy, copy.Length);
        return copy;
    }

    public static string GetReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthOptions] read-only 2.6 -> 3.0 option migration seed ledger.");
        sb.AppendLine("  NOTE: This is not the full 127-option triage yet. It seeds the high-risk/manual mappings first.");
        sb.AppendLine("option | owner | native30 | policy | 26vis -> 30vis | evidence | notes");

        for (int i = 0; i < s_options.Length; i++)
        {
            RebirthOptionMigrationDecl o = s_options[i];
            if (!string.IsNullOrEmpty(f)
                && (o.RebirthName == null || o.RebirthName.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                && (o.OwnerModuleId == null || o.OwnerModuleId.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0))
                continue;

            sb.Append(o.RebirthName).Append(" | ")
              .Append(o.OwnerModuleId).Append(" | ")
              .Append(string.IsNullOrEmpty(o.VanillaEquivalent30) ? "<rebirth-only>" : o.VanillaEquivalent30).Append(" | ")
              .Append(o.MigrationPolicy).Append(" | ")
              .Append(o.Visibility26).Append(" -> ").Append(o.Visibility30).Append(" | ")
              .Append(o.Evidence).Append(" | ")
              .AppendLine(o.Notes);
        }

        return sb.ToString();
    }
}
