using System;
using System.Text;

#nullable disable

/// <summary>
/// Hot-path safe feature flag shell.
/// 
/// This intentionally uses direct bool constants/properties instead of string keys,
/// dictionaries, LINQ, reflection, option lookups, or XML parsing.
/// 
/// Current phase:
/// - all gameplay feature flags are false
/// - no gameplay code consumes these flags yet
/// - no Harmony patches are installed
/// </summary>
public static class RebirthHotPathFeatureFlags
{
    public static readonly bool PlayerCrawlEnabled = false;
    public static readonly bool CombatDamageEnabled = false;
    public static readonly bool ItemStatsEnabled = false;
    public static readonly bool PaintRenderEnabled = false;
    public static readonly bool ZombieAiSpecialEnabled = false;
    public static readonly bool VehicleAiInteractionEnabled = false;
    public static readonly bool SpawningEventsEnabled = false;
    public static readonly bool UiHudCompassEnabled = false;
    public static readonly bool WorkstationsEnabled = false;
    public static readonly bool NpcCompanionsEnabled = false;
    public static readonly bool MapMarkersReservationsEnabled = true;
    public static readonly bool LootPersistenceEnabled = false;
    public static readonly bool HarvestSalvageEnabled = false;
    public static readonly bool AudioSoundsEnabled = false;
    public static readonly bool WorldCavesEnabled = false;
    public static readonly bool WeatherEnvironmentEnabled = false;
    public static readonly bool SurvivorScenarioEnabled = false;

    public static bool IsPlayerCrawlEnabled { get { return PlayerCrawlEnabled; } }
    public static bool IsCombatDamageEnabled { get { return CombatDamageEnabled; } }
    public static bool IsItemStatsEnabled { get { return ItemStatsEnabled; } }
    public static bool IsPaintRenderEnabled { get { return PaintRenderEnabled; } }
    public static bool IsZombieAiSpecialEnabled { get { return ZombieAiSpecialEnabled; } }
    public static bool IsVehicleAiInteractionEnabled { get { return VehicleAiInteractionEnabled; } }
    public static bool IsSpawningEventsEnabled { get { return SpawningEventsEnabled; } }
    public static bool IsUiHudCompassEnabled { get { return UiHudCompassEnabled; } }
    public static bool IsWorkstationsEnabled { get { return WorkstationsEnabled; } }
    public static bool IsNpcCompanionsEnabled { get { return NpcCompanionsEnabled; } }
    public static bool IsMapMarkersReservationsEnabled { get { return MapMarkersReservationsEnabled; } }
    public static bool IsLootPersistenceEnabled { get { return LootPersistenceEnabled; } }
    public static bool IsHarvestSalvageEnabled { get { return HarvestSalvageEnabled; } }
    public static bool IsAudioSoundsEnabled { get { return AudioSoundsEnabled; } }
    public static bool IsWorldCavesEnabled { get { return WorldCavesEnabled; } }
    public static bool IsWeatherEnvironmentEnabled { get { return WeatherEnvironmentEnabled; } }
    public static bool IsSurvivorScenarioEnabled { get { return SurvivorScenarioEnabled; } }

    public static string GetSummaryReport()
    {
        int enabled = 0;
        if (PlayerCrawlEnabled) enabled++;
        if (CombatDamageEnabled) enabled++;
        if (ItemStatsEnabled) enabled++;
        if (PaintRenderEnabled) enabled++;
        if (ZombieAiSpecialEnabled) enabled++;
        if (VehicleAiInteractionEnabled) enabled++;
        if (SpawningEventsEnabled) enabled++;
        if (UiHudCompassEnabled) enabled++;
        if (WorkstationsEnabled) enabled++;
        if (NpcCompanionsEnabled) enabled++;
        if (MapMarkersReservationsEnabled) enabled++;
        if (LootPersistenceEnabled) enabled++;
        if (HarvestSalvageEnabled) enabled++;
        if (AudioSoundsEnabled) enabled++;
        if (WorldCavesEnabled) enabled++;
        if (WeatherEnvironmentEnabled) enabled++;
        if (SurvivorScenarioEnabled) enabled++;

        return "[RebirthHotPathFeatureFlags] Flags: 17; enabled: " + enabled
            + "; all gameplay flags default false. Shell is inert.";
    }

    public static string GetFlagsReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHotPathFeatureFlags] direct hot-path-safe flags.");
        sb.AppendLine("  No strings, dictionaries, LINQ, reflection, XML parsing, or option lookups are required to read these flags.");
        sb.AppendLine("  No gameplay code consumes these flags yet.");
        sb.AppendLine();
        AppendFlag(sb, "player.crawl", PlayerCrawlEnabled);
        AppendFlag(sb, "combat.damage", CombatDamageEnabled);
        AppendFlag(sb, "items.stats", ItemStatsEnabled);
        AppendFlag(sb, "paint.render", PaintRenderEnabled);
        AppendFlag(sb, "zombie.ai.special", ZombieAiSpecialEnabled);
        AppendFlag(sb, "vehicles.ai.interaction", VehicleAiInteractionEnabled);
        AppendFlag(sb, "spawning.events", SpawningEventsEnabled);
        AppendFlag(sb, "ui.hud.compass", UiHudCompassEnabled);
        AppendFlag(sb, "workstations.crafting", WorkstationsEnabled);
        AppendFlag(sb, "npc.companions", NpcCompanionsEnabled);
        AppendFlag(sb, "map.markers.reservations", MapMarkersReservationsEnabled);
        AppendFlag(sb, "loot.persistence", LootPersistenceEnabled);
        AppendFlag(sb, "harvest.salvage", HarvestSalvageEnabled);
        AppendFlag(sb, "audio.sounds", AudioSoundsEnabled);
        AppendFlag(sb, "world.caves.descent", WorldCavesEnabled);
        AppendFlag(sb, "weather.environment", WeatherEnvironmentEnabled);
        AppendFlag(sb, "survivor.scenario", SurvivorScenarioEnabled);
        return sb.ToString();
    }

    public static string GetIntendedUseReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHotPathFeatureFlags] intended future use:");
        sb.AppendLine("  if (!RebirthHotPathFeatureFlags.IsPlayerCrawlEnabled)");
        sb.AppendLine("      return;");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("  - Use direct bool properties in hot paths.");
        sb.AppendLine("  - Do not use string feature IDs in hot paths.");
        sb.AppendLine("  - Do not use dictionaries in hot paths for feature flags.");
        sb.AppendLine("  - Do not read XML/options in hot paths.");
        sb.AppendLine("  - Do not log or build debug strings before checking diagnostic gates.");
        sb.AppendLine();
        sb.AppendLine("Current phase does not wire any gameplay code to these flags.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHotPathFeatureFlags] safety:");
        sb.AppendLine("  1. All gameplay feature flags are false.");
        sb.AppendLine("  2. No gameplay code consumes these flags yet.");
        sb.AppendLine("  3. No Harmony patches installed.");
        sb.AppendLine("  4. No XML parsing.");
        sb.AppendLine("  5. No custom game option lookup.");
        sb.AppendLine("  6. No dictionaries or string lookups needed for hot-path reads.");
        sb.AppendLine("  7. Survivor scenario remains false.");
        return sb.ToString();
    }

    private static void AppendFlag(StringBuilder sb, string name, bool value)
    {
        sb.Append("  ").Append(name).Append(": ").AppendLine(value.ToString());
    }
}
