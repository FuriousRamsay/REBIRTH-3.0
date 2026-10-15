using System;
using System.Collections.Generic;

#nullable disable

// Owned by the existing character statistics record. WorldDay is the evening
// on which the native Blood Moon started, so midnight does not split a run.
public sealed class RebirthHordeNightStatistics
{
    public int WorldDay;
    public bool Completed;
    public long Kills;
    public long HeadshotKills;
    public long HeadDecapitations;
    public long Deaths;
    public readonly Dictionary<string, long> WeaponCategories = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, long> ZombieTypes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, long> ZombieVariants = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    public RebirthHordeNightStatistics Clone()
    {
        var copy = new RebirthHordeNightStatistics
        {
            WorldDay = WorldDay, Completed = Completed, Kills = Kills,
            HeadshotKills = HeadshotKills, HeadDecapitations = HeadDecapitations, Deaths = Deaths
        };
        foreach (var entry in WeaponCategories) copy.WeaponCategories.Add(entry.Key, entry.Value);
        foreach (var entry in ZombieTypes) copy.ZombieTypes.Add(entry.Key, entry.Value);
        foreach (var entry in ZombieVariants) copy.ZombieVariants.Add(entry.Key, entry.Value);
        return copy;
    }
}

// Keep lifetime totals separately from pages of night history. Never derive
// totals from the subset displayed by the UI or sent in a history page.
public sealed class RebirthHordeNightLifetimeStatistics
{
    public long NightsParticipated;
    public long Kills;
    public long HeadshotKills;
    public long HeadDecapitations;
    public long Deaths;
    public long BestNightKills;
    public int BestNightWorldDay;
    public readonly Dictionary<string, long> WeaponCategories = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, long> ZombieTypes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, long> ZombieVariants = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

    public RebirthHordeNightLifetimeStatistics Clone()
    {
        var copy = new RebirthHordeNightLifetimeStatistics
        {
            NightsParticipated = NightsParticipated, Kills = Kills, HeadshotKills = HeadshotKills,
            HeadDecapitations = HeadDecapitations, Deaths = Deaths,
            BestNightKills = BestNightKills, BestNightWorldDay = BestNightWorldDay
        };
        foreach (var entry in WeaponCategories) copy.WeaponCategories.Add(entry.Key, entry.Value);
        foreach (var entry in ZombieTypes) copy.ZombieTypes.Add(entry.Key, entry.Value);
        foreach (var entry in ZombieVariants) copy.ZombieVariants.Add(entry.Key, entry.Value);
        return copy;
    }
}
