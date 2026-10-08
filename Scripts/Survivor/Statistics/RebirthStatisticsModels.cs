using System;
using System.Collections.Generic;

#nullable disable

public static class RebirthStatisticsProtocol
{
    public const int Version = 2;
    public const int MaxWeaponEntries = 16;
    public const int MaxMilestones = 16;
    public const int MaxTrendDays = 7;
}

public sealed class RebirthStatisticsDailyBucket
{
    public int WorldDay;
    public double ActiveSeconds;
    public double CombatSeconds;
    public double SurvivalSeconds;
    public double CraftingSeconds;
    public double ExplorationSeconds;
    public double ManagementSeconds;
    public long ZombiesKilled;
    public long ItemsCrafted;
    public long ResourcesGathered;
    public double DistanceMeters;
    public double OnFootMeters;
    public double HealthPercentSum;
    public double StaminaPercentSum;
    public double NutritionPercentSum;
    public double HydrationPercentSum;
    public double EnergyPercentSum;
    public int SurvivalSamples;

    public RebirthStatisticsDailyBucket Clone()
    {
        return (RebirthStatisticsDailyBucket)MemberwiseClone();
    }
}

public sealed class RebirthStatisticsRecord
{
    public const int CurrentSchemaVersion = 2;
    public int SchemaVersion = CurrentSchemaVersion;
    public string StablePlayerId = string.Empty;
    public string StablePlayerKey = string.Empty;
    public long Epoch = 1L;
    public long Revision = 1L;
    public bool NativeBaselineInitialized;
    public long NativeZombieBaseline;
    public long NativePlayerBaseline;
    public long NativeDeathBaseline;
    public bool Dirty;
    public string LastDirtyReason = string.Empty;

    public double ActivePlaySeconds;
    public double CurrentLifeSeconds;
    public double LongestLifeSeconds;
    public int DistinctAliveWorldDays;
    public int FirstObservedWorldDay;
    public int LastObservedAliveWorldDay;

    public double DistanceMeters;
    public double OnFootMeters;
    public long ZombiesKilled;
    public long PlayerKills;
    public long Deaths;
    public long AnimalsKilled;
    public long HeadshotKills;
    public long MeleeKills;
    public long RangedKills;
    public long KnowledgeDiscovered;
    public double SkillProgressGained;
    public long ItemsCrafted;
    public long ResourcesGathered;
    public long PoisCleared;
    public double DamageDealt;
    public double DamageTaken;
    public double DamageBlocked;
    public bool HasAuthoritativeBlockedDamage;
    public double HighestDamageHit;
    public double HighestFallSurvived;
    public long BestZombiesInDay;
    public long BestItemsCraftedInDay;
    public long BestResourcesInDay;
    public double BestOnFootDistanceInDay;
    public double CombatSeconds;
    public double SurvivalSeconds;
    public double CraftingSeconds;
    public double ExplorationSeconds;
    public double ManagementSeconds;

    public readonly HashSet<string> LocationsDiscovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> TradersVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> BiomesVisited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, long> WeaponKills = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, int> MilestoneWorldDays = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public readonly List<RebirthStatisticsDailyBucket> Daily = new List<RebirthStatisticsDailyBucket>();

    public void Touch(string reason)
    {
        Revision = Revision < long.MaxValue ? Math.Max(1L, Revision + 1L) : long.MaxValue;
        Dirty = true;
        LastDirtyReason = reason ?? string.Empty;
    }

    public void MarkPersisted()
    {
        Dirty = false;
        LastDirtyReason = string.Empty;
    }

    public RebirthStatisticsRecord Clone()
    {
        RebirthStatisticsRecord c = new RebirthStatisticsRecord
        {
            SchemaVersion = SchemaVersion,
            StablePlayerId = StablePlayerId,
            StablePlayerKey = StablePlayerKey,
            Epoch = Epoch,
            Revision = Revision,
            NativeBaselineInitialized = NativeBaselineInitialized,
            NativeZombieBaseline = NativeZombieBaseline,
            NativePlayerBaseline = NativePlayerBaseline,
            NativeDeathBaseline = NativeDeathBaseline,
            Dirty = Dirty,
            LastDirtyReason = LastDirtyReason,
            ActivePlaySeconds = ActivePlaySeconds,
            CurrentLifeSeconds = CurrentLifeSeconds,
            LongestLifeSeconds = LongestLifeSeconds,
            DistinctAliveWorldDays = DistinctAliveWorldDays,
            FirstObservedWorldDay = FirstObservedWorldDay,
            LastObservedAliveWorldDay = LastObservedAliveWorldDay,
            DistanceMeters = DistanceMeters,
            OnFootMeters = OnFootMeters,
            ZombiesKilled = ZombiesKilled,
            PlayerKills = PlayerKills,
            Deaths = Deaths,
            AnimalsKilled = AnimalsKilled,
            HeadshotKills = HeadshotKills,
            MeleeKills = MeleeKills,
            RangedKills = RangedKills,
            KnowledgeDiscovered = KnowledgeDiscovered,
            SkillProgressGained = SkillProgressGained,
            ItemsCrafted = ItemsCrafted,
            ResourcesGathered = ResourcesGathered,
            PoisCleared = PoisCleared,
            DamageDealt = DamageDealt,
            DamageTaken = DamageTaken,
            DamageBlocked = DamageBlocked,
            HasAuthoritativeBlockedDamage = HasAuthoritativeBlockedDamage,
            HighestDamageHit = HighestDamageHit,
            HighestFallSurvived = HighestFallSurvived,
            BestZombiesInDay = BestZombiesInDay,
            BestItemsCraftedInDay = BestItemsCraftedInDay,
            BestResourcesInDay = BestResourcesInDay,
            BestOnFootDistanceInDay = BestOnFootDistanceInDay,
            CombatSeconds = CombatSeconds,
            SurvivalSeconds = SurvivalSeconds,
            CraftingSeconds = CraftingSeconds,
            ExplorationSeconds = ExplorationSeconds,
            ManagementSeconds = ManagementSeconds
        };
        foreach (string v in LocationsDiscovered) c.LocationsDiscovered.Add(v);
        foreach (string v in TradersVisited) c.TradersVisited.Add(v);
        foreach (string v in BiomesVisited) c.BiomesVisited.Add(v);
        foreach (KeyValuePair<string,long> v in WeaponKills) c.WeaponKills[v.Key] = v.Value;
        foreach (KeyValuePair<string,int> v in MilestoneWorldDays) c.MilestoneWorldDays[v.Key] = v.Value;
        for (int i = 0; i < Daily.Count; i++) if (Daily[i] != null) c.Daily.Add(Daily[i].Clone());
        return c;
    }
}

public sealed class RebirthStatisticsMilestoneDefinition
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string Metric = string.Empty;
    public double Threshold;
}

public sealed class RebirthStatisticsWeaponSnapshot
{
    public string ItemName = string.Empty;
    public long Kills;
    public RebirthStatisticsWeaponSnapshot Clone() { return new RebirthStatisticsWeaponSnapshot { ItemName = ItemName, Kills = Kills }; }
}

public sealed class RebirthStatisticsMilestoneSnapshot
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public int WorldDay;
    public RebirthStatisticsMilestoneSnapshot Clone() { return new RebirthStatisticsMilestoneSnapshot { Id = Id, Name = Name, WorldDay = WorldDay }; }
}

public sealed class RebirthStatisticsTrendSnapshot
{
    public int WorldDay;
    public float HealthPercent;
    public float StaminaPercent;
    public float NutritionPercent;
    public float HydrationPercent;
    public float EnergyPercent;
    public RebirthStatisticsTrendSnapshot Clone() { return (RebirthStatisticsTrendSnapshot)MemberwiseClone(); }
}

public sealed class RebirthStatisticsSnapshot
{
    public int ProtocolVersion = RebirthStatisticsProtocol.Version;
    public long Epoch;
    public long Revision;
    public int PlayerLevel;
    public double ActivePlaySeconds;
    public double CurrentLifeSeconds;
    public double LongestLifeSeconds;
    public int DaysSurvived;
    public double DistanceMeters;
    public double OnFootMeters;
    public long ZombiesKilled;
    public long PlayerKills;
    public long Deaths;
    public long AnimalsKilled;
    public long HeadshotKills;
    public long MeleeKills;
    public long RangedKills;
    public long KnowledgeDiscovered;
    public double SkillProgressGained;
    public long ItemsCrafted;
    public long ResourcesGathered;
    public long PoisCleared;
    public int LocationsDiscovered;
    public int TradersVisited;
    public int BiomesVisited;
    public double DamageDealt;
    public double DamageTaken;
    public double DamageBlocked;
    public bool HasAuthoritativeBlockedDamage;
    public double HighestDamageHit;
    public double HighestFallSurvived;
    public long BestZombiesInDay;
    public long BestItemsCraftedInDay;
    public long BestResourcesInDay;
    public double BestOnFootDistanceInDay;
    public double CombatSeconds;
    public double SurvivalSeconds;
    public double CraftingSeconds;
    public double ExplorationSeconds;
    public double ManagementSeconds;
    public readonly List<RebirthStatisticsWeaponSnapshot> Weapons = new List<RebirthStatisticsWeaponSnapshot>();
    public readonly List<RebirthStatisticsMilestoneSnapshot> Milestones = new List<RebirthStatisticsMilestoneSnapshot>();
    public readonly List<RebirthStatisticsTrendSnapshot> Trend = new List<RebirthStatisticsTrendSnapshot>();

    public RebirthStatisticsSnapshot Clone()
    {
        RebirthStatisticsSnapshot c = new RebirthStatisticsSnapshot
        {
            ProtocolVersion=ProtocolVersion, Epoch=Epoch, Revision=Revision, PlayerLevel=PlayerLevel, ActivePlaySeconds=ActivePlaySeconds,
            CurrentLifeSeconds=CurrentLifeSeconds, LongestLifeSeconds=LongestLifeSeconds, DaysSurvived=DaysSurvived,
            DistanceMeters=DistanceMeters, OnFootMeters=OnFootMeters, ZombiesKilled=ZombiesKilled, PlayerKills=PlayerKills,
            Deaths=Deaths, AnimalsKilled=AnimalsKilled, HeadshotKills=HeadshotKills, MeleeKills=MeleeKills, RangedKills=RangedKills,
            KnowledgeDiscovered=KnowledgeDiscovered, SkillProgressGained=SkillProgressGained, ItemsCrafted=ItemsCrafted,
            ResourcesGathered=ResourcesGathered, PoisCleared=PoisCleared, LocationsDiscovered=LocationsDiscovered,
            TradersVisited=TradersVisited, BiomesVisited=BiomesVisited, DamageDealt=DamageDealt, DamageTaken=DamageTaken,
            DamageBlocked=DamageBlocked, HasAuthoritativeBlockedDamage=HasAuthoritativeBlockedDamage, HighestDamageHit=HighestDamageHit,
            HighestFallSurvived=HighestFallSurvived, BestZombiesInDay=BestZombiesInDay, BestItemsCraftedInDay=BestItemsCraftedInDay,
            BestResourcesInDay=BestResourcesInDay, BestOnFootDistanceInDay=BestOnFootDistanceInDay, CombatSeconds=CombatSeconds,
            SurvivalSeconds=SurvivalSeconds, CraftingSeconds=CraftingSeconds, ExplorationSeconds=ExplorationSeconds, ManagementSeconds=ManagementSeconds
        };
        for (int i=0;i<Weapons.Count;i++) if (Weapons[i]!=null) c.Weapons.Add(Weapons[i].Clone());
        for (int i=0;i<Milestones.Count;i++) if (Milestones[i]!=null) c.Milestones.Add(Milestones[i].Clone());
        for (int i=0;i<Trend.Count;i++) if (Trend[i]!=null) c.Trend.Add(Trend[i].Clone());
        return c;
    }
}
