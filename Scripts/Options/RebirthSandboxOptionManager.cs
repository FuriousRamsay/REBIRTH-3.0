using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

#nullable disable

public enum RebirthSandboxOptionId
{
    AdvancedFarming = 0,
    InstantBlockPickup = 1,
    QuickStack = 2,
    RemoteResources = 3,
    SecureAccessSharing = 4,
    HornActivatedDoors = 5,
    SpawnProgression = 6,
    AutoReplantTrees = 7,
    VehicleBlockRespawn = 8,
    HybridPathSmoothing = 9,
    SleeperRespawns = 10,
    LootTraderAreas = 11,
    SleeperSpawnMultiplier = 12,
    InfestedSleeperSpawnMultiplier = 13,
    SuppressConsoleErrorPopups = 14,
    TreeDensityMultiplier = 15,
    VehicleDensityMultiplier = 16,
    BlocksCatchFire = 17,
    MaxJobs = 18,
    RepeatPoiJobs = 19,
    InfestedJobs = 20,
    TraderJobList = 21,
    PreventHealingOverlap = 22,
    AlwaysStagger = 23,
    WeatherFogBehavior = 24,
    WeatherFogIntensity = 25,
    UniformAtmosphere = 26,
    PoiRisk = 27,
    PoiSenseSchedule = 28,
    PoiSenseIntensity = 29,
    PitchBlack = 30,
    BossEvents = 31,
    BossEventFrequency = 32,
    BossEventMaximumPerDay = 33,
    BossEventSize = 34,
    BossEventDifficulty = 35,
    BossEventTime = 36,
    BossEventBloodMoonDay = 37,
    BossEventRestriction = 38,
    BossEventRewards = 39,
    BossEventNotifications = 40,
    ProtectCrate = 41,
    ZombiesDestroyAreas = 42,
    RancherRangedAttack = 43,
    FireAffectsHeatmap = 44,
    FireBlockDamageSpeed = 45,
    TraderJobRecoveryGrace = 46,
    TraderJobRecoveryGraceDuration = 47,
    QuickStackDistance = 48,
    RemoteResourcesDistance = 49,
    ReservedCompanionInventory = 50,
    GuardDistance = 51,
    FullControlDistance = 52,
    HuntingDistance = 53,
    CompanionCardStyle = 54,
    TargetNameColorRHigh = 55,
    TargetNameColorRLow = 56,
    TargetNameColorGHigh = 57,
    TargetNameColorGLow = 58,
    TargetNameColorBHigh = 59,
    TargetNameColorBLow = 60,
    PlayerProgression = 61,
    LiteratureStudyTime = 62,
    JobsToNextTier = 63,
    // Local presentation lock identities only; not serialized into world/preset codes.
    TraderVoiceRekt = 64,
    TraderVoiceHugh = 65,
    TraderVoiceJen = 66,
    TraderVoiceJoel = 67,
    TraderVoiceBob = 68,
    RequireTimedReading = 69,
    ScrollbarMode = 70,
    Theme = 71,
    ShowClearedPois = 72
}




public enum RebirthWorldTheme { None = 0, Purge = 1 }

public enum RebirthScrollbarMode { Smooth = 0, Paged = 1 }

public enum RebirthLiteratureStudyTime
{
    Quarter = 0,
    Half = 1,
    ThreeQuarters = 2,
    Normal = 3,
    OneAndHalf = 4,
    Double = 5
}

public static class RebirthLiteratureStudyTimePolicy
{
    public const RebirthLiteratureStudyTime Default = RebirthLiteratureStudyTime.Normal;
    public static float ToMultiplier(RebirthLiteratureStudyTime value)
    {
        switch(value)
        {
            case RebirthLiteratureStudyTime.Quarter: return 0.25f;
            case RebirthLiteratureStudyTime.Half: return 0.50f;
            case RebirthLiteratureStudyTime.ThreeQuarters: return 0.75f;
            case RebirthLiteratureStudyTime.OneAndHalf: return 1.50f;
            case RebirthLiteratureStudyTime.Double: return 2.00f;
            default: return 1.00f;
        }
    }
}

public enum RebirthCompanionCardStyle
{
    Default = 0,
    Simple = 1
}

public static class RebirthTargetNameColorPolicy
{
    // Muted warm yellow: readable over the world without looking neon.
    public const int DefaultR = 235;
    public const int DefaultG = 196;
    public const int DefaultB = 74;

    public static int ClampChannel(int value)
    {
        return Math.Max(0, Math.Min(255, value));
    }
}

public static class RebirthCompanionDistancePolicy
{
    public const int GuardDefault = 20;
    public const int FullControlDefault = 20;
    public const int HuntingDefault = 40;
    public static readonly int[] GuardAllowedDistances = { 5,10,15,20,25,30,35,40,45,50 };
    public static readonly int[] FullControlAllowedDistances = { 5,10,15,20,25,30,35,40 };
    public static readonly int[] HuntingAllowedDistances = { 20,25,30,35,40,45,50,55,60 };

    public static int NormalizeGuard(int value) { return Normalize(value, GuardDefault, GuardAllowedDistances); }
    public static int NormalizeFullControl(int value) { return Normalize(value, FullControlDefault, FullControlAllowedDistances); }
    public static int NormalizeHunting(int value) { return Normalize(value, HuntingDefault, HuntingAllowedDistances); }
    public static int GuardDistanceToIndex(int value) { return ToIndex(NormalizeGuard(value), GuardAllowedDistances); }
    public static int FullControlDistanceToIndex(int value) { return ToIndex(NormalizeFullControl(value), FullControlAllowedDistances); }
    public static int HuntingDistanceToIndex(int value) { return ToIndex(NormalizeHunting(value), HuntingAllowedDistances); }
    public static bool TryDecodeGuard(int index, out int distance) { return TryDecode(index, GuardAllowedDistances, out distance); }
    public static bool TryDecodeFullControl(int index, out int distance) { return TryDecode(index, FullControlAllowedDistances, out distance); }
    public static bool TryDecodeHunting(int index, out int distance) { return TryDecode(index, HuntingAllowedDistances, out distance); }
    private static int Normalize(int value, int fallback, int[] allowed) { int best=fallback,delta=int.MaxValue; for(int i=0;i<allowed.Length;i++){int d=Math.Abs(allowed[i]-value);if(d<delta){delta=d;best=allowed[i];}} return best; }
    private static int ToIndex(int value, int[] allowed) { for(int i=0;i<allowed.Length;i++) if(allowed[i]==value) return i; return 0; }
    private static bool TryDecode(int index, int[] allowed, out int distance) { distance=0; if(index<0||index>=allowed.Length)return false; distance=allowed[index]; return true; }
}

public static class RebirthResourceDistancePolicy
{
    public const int QuickStackDefault = 30;
    public const int QuickStackMaximum = 50;
    public const int RemoteResourcesDefault = 50;
    public const int RemoteResourcesMaximum = 80;

    public static readonly int[] QuickStackAllowedDistances =
    {
        10, 20, 30, 40, 50
    };

    public static readonly int[] RemoteResourcesAllowedDistances =
    {
        10, 20, 30, 40, 50, 60, 70, 80
    };

    // v148 encoded both options against 10..200. Keep accepting those legacy
    // indices and clamp them to the new per-feature maximum instead of rejecting
    // an existing sandbox code.
    private const int LegacyDistanceValueCount = 20;

    public static int NormalizeQuickStack(int value)
    {
        return Normalize(value, QuickStackDefault, QuickStackAllowedDistances);
    }

    public static int NormalizeRemoteResources(int value)
    {
        return Normalize(value, RemoteResourcesDefault, RemoteResourcesAllowedDistances);
    }

    public static int QuickStackDistanceToIndex(int value)
    {
        return DistanceToIndex(value, QuickStackDefault, QuickStackAllowedDistances);
    }

    public static int RemoteResourcesDistanceToIndex(int value)
    {
        return DistanceToIndex(value, RemoteResourcesDefault, RemoteResourcesAllowedDistances);
    }

    public static bool TryDecodeQuickStackDistanceIndex(int valueIndex, out int distance)
    {
        return TryDecodeLegacyDistanceIndex(valueIndex, QuickStackMaximum, out distance);
    }

    public static bool TryDecodeRemoteResourcesDistanceIndex(int valueIndex, out int distance)
    {
        return TryDecodeLegacyDistanceIndex(valueIndex, RemoteResourcesMaximum, out distance);
    }

    private static bool TryDecodeLegacyDistanceIndex(int valueIndex, int maximum, out int distance)
    {
        distance = 0;
        if (valueIndex < 0 || valueIndex >= LegacyDistanceValueCount) return false;
        distance = Math.Min(maximum, 10 + valueIndex * 10);
        return true;
    }

    private static int Normalize(int value, int fallback, int[] allowed)
    {
        int best = fallback;
        int bestDelta = int.MaxValue;
        for (int i = 0; i < allowed.Length; i++)
        {
            int delta = Math.Abs(allowed[i] - value);
            if (delta < bestDelta)
            {
                best = allowed[i];
                bestDelta = delta;
            }
        }
        return best;
    }

    private static int DistanceToIndex(int value, int fallback, int[] allowed)
    {
        int normalized = Normalize(value, fallback, allowed);
        for (int i = 0; i < allowed.Length; i++)
            if (allowed[i] == normalized) return i;
        return 0;
    }
}



public enum RebirthHornActivatedDoorsMode { None = 0, Trader = 1, All = 2 }

public enum RebirthInfestedJobsMode { Default = 0, Hide = 1, Surprise = 2 }

public enum RebirthTraderJobListMode { Fixed = 0, Random = 1 }

public enum RebirthTraderJobRecoveryGraceDuration
{
    FiveMinutes = 0,
    TenMinutes = 1,
    FifteenMinutes = 2
}

public enum RebirthAlwaysStaggerMode { Disabled = 0, HeadshotsOnly = 1, AllQualifyingHits = 2 }

public enum RebirthWeatherFogBehavior { Dynamic = 0, Static = 1, Disabled = 2 }

public enum RebirthWeatherFogIntensity
{
    None = 0,
    VeryLow = 1,
    Low = 2,
    Normal = 3,
    Heavy = 4,
    VeryHeavy = 5
}

public enum RebirthPoiSenseSchedule { Never = 0, DayOnly = 1, NightOnly = 2, Always = 3 }

public enum RebirthPoiSenseIntensity { Low = 0, Medium = 1, High = 2 }

public enum RebirthInstantBlockPickupMode
{
    None = 0,
    Default = 1,
    Always = 2
}

public enum RebirthFireBlockDamageSpeed
{
    Slower = 0,
    Default = 1,
    Faster = 2
}

public enum RebirthHealingOverlapThreshold
{
    Off = 0,
    Health10 = 1,
    Health20 = 2,
    Health30 = 3
}

public static class RebirthHealingOverlapThresholdPolicy
{
    public const int DefaultHealth = 10;

    public static bool IsEnabled(RebirthHealingOverlapThreshold value)
    {
        return value != RebirthHealingOverlapThreshold.Off;
    }

    public static int ToHealth(RebirthHealingOverlapThreshold value)
    {
        switch (value)
        {
            case RebirthHealingOverlapThreshold.Health10: return 10;
            case RebirthHealingOverlapThreshold.Health20: return 20;
            case RebirthHealingOverlapThreshold.Health30: return 30;
            default: return 0;
        }
    }

    public static RebirthHealingOverlapThreshold Normalize(RebirthHealingOverlapThreshold value)
    {
        if (value < RebirthHealingOverlapThreshold.Off ||
            value > RebirthHealingOverlapThreshold.Health30)
            return RebirthHealingOverlapThreshold.Health10;
        return value;
    }
}

public enum RebirthBossEventStartLevel
{
    Off = 0,
    PlayerLevel10 = 1,
    PlayerLevel15 = 2,
    PlayerLevel20 = 3,
    PlayerLevel25 = 4,
    PlayerLevel30 = 5
}

public static class RebirthBossEventStartLevelPolicy
{
    public const int DefaultPlayerLevel = 15;

    public static bool IsEnabled(RebirthBossEventStartLevel value)
    {
        return value != RebirthBossEventStartLevel.Off;
    }

    public static int ToPlayerLevel(RebirthBossEventStartLevel value)
    {
        switch (value)
        {
            case RebirthBossEventStartLevel.PlayerLevel10: return 10;
            case RebirthBossEventStartLevel.PlayerLevel15: return 15;
            case RebirthBossEventStartLevel.PlayerLevel20: return 20;
            case RebirthBossEventStartLevel.PlayerLevel25: return 25;
            case RebirthBossEventStartLevel.PlayerLevel30: return 30;
            default: return 0;
        }
    }

    public static RebirthBossEventStartLevel Normalize(RebirthBossEventStartLevel value)
    {
        if (value < RebirthBossEventStartLevel.Off ||
            value > RebirthBossEventStartLevel.PlayerLevel30)
            return RebirthBossEventStartLevel.PlayerLevel15;
        return value;
    }
}

/// <summary>
/// Central policy for dependencies between Rebirth sandbox options.
///
/// A dependency must never destroy the configured value of the dependent option.
/// It may disable the control and substitute an effective runtime value while the
/// parent condition is active. When the condition is removed, the configured value
/// becomes effective again without requiring the player to re-enter it.
/// </summary>
public static class RebirthSandboxOptionDependencyPolicy
{
    public static bool IsWeatherFogIntensityEnabled(RebirthWeatherFogBehavior behavior)
    {
        return behavior != RebirthWeatherFogBehavior.Disabled;
    }

    public static bool IsPoiRiskEnabled(RebirthSpawnProgressionMode spawnProgression)
    {
        return spawnProgression == RebirthSpawnProgressionMode.Gamestage;
    }

    public static bool IsPoiSenseIntensityEnabled(RebirthPoiSenseSchedule schedule)
    {
        return schedule != RebirthPoiSenseSchedule.Never;
    }

    public static RebirthPoiRiskMode GetEffectivePoiRisk(
        RebirthSpawnProgressionMode spawnProgression,
        RebirthPoiRiskMode configuredValue)
    {
        return IsPoiRiskEnabled(spawnProgression)
            ? configuredValue
            : RebirthPoiRiskMode.None;
    }
}

public sealed class RebirthSandboxState
{
    public RebirthPlayerProgressionMode PlayerProgression = RebirthPlayerProgressionMode.Rebirth;
    public RebirthWorldTheme Theme = RebirthWorldTheme.None;
    public bool ShowClearedPois;
    public RebirthScrollbarMode ScrollbarMode = RebirthScrollbarMode.Smooth;
    public bool RequireTimedReading = true;
    public RebirthLiteratureStudyTime LiteratureStudyTime = RebirthLiteratureStudyTimePolicy.Default;
    public bool AdvancedFarming = true;
    public RebirthInstantBlockPickupMode InstantBlockPickup = RebirthInstantBlockPickupMode.Default;
    public RebirthQuickStackMode QuickStack = RebirthQuickStackMode.Full;
    public int QuickStackDistance = RebirthResourceDistancePolicy.QuickStackDefault;
    public bool RemoteResources = true;
    public int RemoteResourcesDistance = RebirthResourceDistancePolicy.RemoteResourcesDefault;
    public int GuardDistance = RebirthCompanionDistancePolicy.GuardDefault;
    public int FullControlDistance = RebirthCompanionDistancePolicy.FullControlDefault;
    public int HuntingDistance = RebirthCompanionDistancePolicy.HuntingDefault;
    public RebirthCompanionCardStyle CompanionCardStyle = RebirthCompanionCardStyle.Simple;
    public int TargetNameColorR = RebirthTargetNameColorPolicy.DefaultR;
    public int TargetNameColorG = RebirthTargetNameColorPolicy.DefaultG;
    public int TargetNameColorB = RebirthTargetNameColorPolicy.DefaultB;
    public RebirthSecureAccessMode SecureAccessSharing = RebirthSecureAccessMode.PinAccessList;
    public RebirthHornActivatedDoorsMode HornActivatedDoors = RebirthHornActivatedDoorsMode.All;
    public RebirthSpawnProgressionMode SpawnProgression = RebirthSpawnProgressionMode.Biome;
    public bool AutoReplantTrees = false;
    public int VehicleBlockRespawnDays = 0;
    public bool HybridPathSmoothing = true;
    public bool ZombiesDestroyAreas = false;
    public bool RancherRangedAttack = false;
    public bool SleeperRespawns = false;
    public bool LootTraderAreas = true;
    public int SleeperSpawnMultiplier = 1;
    public int InfestedSleeperSpawnMultiplier = 2;
    public bool SuppressConsoleErrorPopups = false;
    public int TreeDensityMultiplier = 100;
    public int VehicleDensityMultiplier = 100;
    public bool BlocksCatchFire = false;
    public bool FireAffectsHeatmap = false;
    public RebirthFireBlockDamageSpeed FireBlockDamageSpeed = RebirthFireBlockDamageSpeed.Default;
    public int MaxJobs = 11;
    public int JobsToNextTier = RebirthTraderJobPolicy.DefaultJobsToNextTier;
    public RebirthRepeatPoiPolicy RepeatPoiJobs = RebirthRepeatPoiPolicy.Medium;
    public RebirthInfestedJobsMode InfestedJobs = RebirthInfestedJobsMode.Default;
    public RebirthTraderJobListMode TraderJobList = RebirthTraderJobListMode.Fixed;
    public bool TraderJobRecoveryGrace = true;
    public RebirthTraderJobRecoveryGraceDuration TraderJobRecoveryGraceDuration =
        RebirthTraderJobRecoveryGraceDuration.FiveMinutes;
    public RebirthHealingOverlapThreshold PreventHealingOverlap = RebirthHealingOverlapThreshold.Health10;
    public RebirthAlwaysStaggerMode AlwaysStagger = RebirthAlwaysStaggerMode.AllQualifyingHits;
    public RebirthWeatherFogBehavior WeatherFogBehavior = RebirthWeatherFogBehavior.Dynamic;
    public RebirthWeatherFogIntensity WeatherFogIntensity = RebirthWeatherFogIntensity.Normal;
    public bool UniformAtmosphere = true;
    public RebirthPoiRiskMode PoiRisk = RebirthPoiRiskMode.Medium;
    public RebirthPoiSenseSchedule PoiSenseSchedule = RebirthPoiSenseSchedule.Never;
    public RebirthPoiSenseIntensity PoiSenseIntensity = RebirthPoiSenseIntensity.Medium;
    public bool PitchBlack = true;
    public RebirthBossEventStartLevel BossEventStartLevel = RebirthBossEventStartLevel.PlayerLevel15;
    public RebirthBossEventFrequency BossEventFrequency = RebirthBossEventFrequency.Normal;
    public int BossEventMaximumPerDay = 2;
    public RebirthBossEventSize BossEventSize = RebirthBossEventSize.Normal;
    public RebirthBossEventDifficulty BossEventDifficulty = RebirthBossEventDifficulty.Normal;
    public RebirthBossEventTime BossEventTime = RebirthBossEventTime.DayAndNight;
    public bool BossEventBloodMoonDay = true;
    public RebirthBossEventRestriction BossEventRestriction = RebirthBossEventRestriction.Anywhere;
    public bool BossEventRewards = true;
    public bool BossEventNotifications = true;
    public bool ProtectCrate = true;

    public RebirthSandboxState Clone()
    {
        return new RebirthSandboxState
        {
            PlayerProgression = PlayerProgression,
            Theme = Theme,
            ShowClearedPois = ShowClearedPois,
            ScrollbarMode = ScrollbarMode,
            RequireTimedReading = RequireTimedReading,
            LiteratureStudyTime = LiteratureStudyTime,
            AdvancedFarming = AdvancedFarming,
            InstantBlockPickup = InstantBlockPickup,
            QuickStack = QuickStack,
            QuickStackDistance = QuickStackDistance,
            RemoteResources = RemoteResources,
            RemoteResourcesDistance = RemoteResourcesDistance,
            GuardDistance = GuardDistance,
            FullControlDistance = FullControlDistance,
            HuntingDistance = HuntingDistance,
            CompanionCardStyle = CompanionCardStyle,
            TargetNameColorR = TargetNameColorR,
            TargetNameColorG = TargetNameColorG,
            TargetNameColorB = TargetNameColorB,
            SecureAccessSharing = SecureAccessSharing,
            HornActivatedDoors = HornActivatedDoors,
            SpawnProgression = SpawnProgression,
            AutoReplantTrees = AutoReplantTrees,
            VehicleBlockRespawnDays = VehicleBlockRespawnDays,
            HybridPathSmoothing = HybridPathSmoothing,
            ZombiesDestroyAreas = ZombiesDestroyAreas,
            RancherRangedAttack = RancherRangedAttack,
            SleeperRespawns = SleeperRespawns,
            LootTraderAreas = LootTraderAreas,
            SleeperSpawnMultiplier = SleeperSpawnMultiplier,
            InfestedSleeperSpawnMultiplier = InfestedSleeperSpawnMultiplier,
            SuppressConsoleErrorPopups = SuppressConsoleErrorPopups,
            TreeDensityMultiplier = TreeDensityMultiplier,
            VehicleDensityMultiplier = VehicleDensityMultiplier,
            BlocksCatchFire = BlocksCatchFire,
            FireAffectsHeatmap = FireAffectsHeatmap,
            FireBlockDamageSpeed = FireBlockDamageSpeed,
            MaxJobs = MaxJobs,
            JobsToNextTier = JobsToNextTier,
            RepeatPoiJobs = RepeatPoiJobs,
            InfestedJobs = InfestedJobs,
            TraderJobList = TraderJobList,
            TraderJobRecoveryGrace = TraderJobRecoveryGrace,
            TraderJobRecoveryGraceDuration = TraderJobRecoveryGraceDuration,
            PreventHealingOverlap = PreventHealingOverlap,
            AlwaysStagger = AlwaysStagger,
            WeatherFogBehavior = WeatherFogBehavior,
            WeatherFogIntensity = WeatherFogIntensity,
            UniformAtmosphere = UniformAtmosphere,
            PoiRisk = PoiRisk,
            PoiSenseSchedule = PoiSenseSchedule,
            PoiSenseIntensity = PoiSenseIntensity,
            PitchBlack = PitchBlack,
            BossEventStartLevel = BossEventStartLevel,
            BossEventFrequency = BossEventFrequency,
            BossEventMaximumPerDay = BossEventMaximumPerDay,
            BossEventSize = BossEventSize,
            BossEventDifficulty = BossEventDifficulty,
            BossEventTime = BossEventTime,
            BossEventBloodMoonDay = BossEventBloodMoonDay,
            BossEventRestriction = BossEventRestriction,
            BossEventRewards = BossEventRewards,
            BossEventNotifications = BossEventNotifications,
            ProtectCrate = ProtectCrate
        };
    }
}

public sealed class RebirthSandboxPreset
{
    public string Name;
    public string LocalizedNameKey;
    public string DescriptionKey;
    public string Description;
    public string Group;
    public bool IsDefault;
    public bool IsUserPreset;
    public string Code;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(LocalizedNameKey))
                return Localization.Get(LocalizedNameKey);
            return Name ?? string.Empty;
        }
    }

    public string DisplayDescription
    {
        get
        {
            if (!string.IsNullOrEmpty(DescriptionKey))
                return Localization.Get(DescriptionKey);
            return Description ?? string.Empty;
        }
    }
}

/// <summary>
/// Independent REBIRTH sandbox option registry and code codec.
/// This never reads or writes the base game's SandboxCode.
/// </summary>
public sealed class RebirthSandboxOptionManager
{
    public const string CodePrefix = "RB";
    public const char CurrentVersion = 'X';
    public const char PreviousVersionW = 'W';
    public const char PreviousVersion000000000000 = 'V';
    public const char PreviousVersion00000000000 = 'U';
    public const char PreviousVersion0000000000 = 'T';
    public const char PreviousVersion000000000 = 'S';
    public const char PreviousVersion00000000 = 'R';
    public const char PreviousVersion0000000 = 'Q';
    public const char PreviousVersion000000 = 'P';
    public const char PreviousVersion00000 = 'O';
    public const char PreviousVersion0000 = 'N';
    public const char PreviousVersion000 = 'M';
    public const char PreviousVersion00 = 'L';
    public const char PreviousVersion0 = 'K';
    public const char PreviousVersion = 'J';
    public const char PreviousVersion2 = 'I';
    public const char OlderVersion4 = 'H';
    public const char OlderVersion3 = 'G';
    public const char OlderVersion2 = 'F';
    public const char OlderVersion = 'E';
    public const char LegacyVersion = 'D';
    public const char OldestVersion = 'C';
    public const char AncientVersion = 'B';
    public const char EarliestVersion = 'A';
    public const string DefaultPresetName = "RebirthStandard";
    public const string RecommendedGroupName = "RebirthRecommended";
    public const string UserGroupName = "RebirthUser";

    private static readonly RebirthSandboxOptionManager s_current = new RebirthSandboxOptionManager();
    private readonly List<RebirthSandboxPreset> presets = new List<RebirthSandboxPreset>();
    private RebirthSandboxState currentState = new RebirthSandboxState();
    private int revision;

    public static RebirthSandboxOptionManager Current { get { return s_current; } }
    public IList<RebirthSandboxPreset> Presets { get { return presets.AsReadOnly(); } }
    public RebirthPlayerProgressionMode PlayerProgression { get { return currentState.PlayerProgression; } }
    public RebirthWorldTheme Theme { get { return currentState.Theme; } }
    public bool ShowClearedPois { get { return currentState.ShowClearedPois; } }
    public bool IsPurge { get { return RebirthPurgeReleasePolicy.Enabled && RebirthThemePolicy.IsPurge(currentState); } }
    public bool PoiClearTrackingEnabled { get { return RebirthPurgeReleasePolicy.Enabled && RebirthThemePolicy.TrackingEnabled(currentState); } }
    public RebirthSpawnProgressionMode EffectiveSpawnProgression { get { return IsPurge ? RebirthSpawnProgressionMode.Biome : currentState.SpawnProgression; } }
    public RebirthScrollbarMode ScrollbarMode { get { return currentState.ScrollbarMode; } }
    public bool RequireTimedReading { get { return currentState.RequireTimedReading; } }
    public RebirthLiteratureStudyTime LiteratureStudyTime { get { return currentState.LiteratureStudyTime; } }
    public float LiteratureStudyTimeMultiplier { get { return RebirthLiteratureStudyTimePolicy.ToMultiplier(currentState.LiteratureStudyTime); } }
    public bool AdvancedFarming { get { return currentState.AdvancedFarming; } }
    public RebirthInstantBlockPickupMode InstantBlockPickup { get { return currentState.InstantBlockPickup; } }
    public RebirthQuickStackMode QuickStack { get { return currentState.QuickStack; } }
    public int QuickStackDistance { get { return currentState.QuickStackDistance; } }
    public bool RemoteResources { get { return currentState.RemoteResources; } }
    public int RemoteResourcesDistance { get { return currentState.RemoteResourcesDistance; } }
    public int GuardDistance { get { return currentState.GuardDistance; } }
    public int FullControlDistance { get { return currentState.FullControlDistance; } }
    public int HuntingDistance { get { return currentState.HuntingDistance; } }
    public RebirthCompanionCardStyle CompanionCardStyle { get { return currentState.CompanionCardStyle; } }
    public int TargetNameColorR { get { return currentState.TargetNameColorR; } }
    public int TargetNameColorG { get { return currentState.TargetNameColorG; } }
    public int TargetNameColorB { get { return currentState.TargetNameColorB; } }
    public RebirthSecureAccessMode SecureAccessSharing { get { return currentState.SecureAccessSharing; } }
    public RebirthHornActivatedDoorsMode HornActivatedDoors { get { return currentState.HornActivatedDoors; } }
    public RebirthSpawnProgressionMode SpawnProgression { get { return currentState.SpawnProgression; } }
    public bool AutoReplantTrees { get { return currentState.AutoReplantTrees; } }
    public int VehicleBlockRespawnDays { get { return currentState.VehicleBlockRespawnDays; } }
    public bool HybridPathSmoothing { get { return currentState.HybridPathSmoothing; } }
    public bool ZombiesDestroyAreas { get { return currentState.ZombiesDestroyAreas; } }
    public bool RancherRangedAttack { get { return currentState.RancherRangedAttack; } }
    public bool SleeperRespawns { get { return currentState.SleeperRespawns; } }
    public bool LootTraderAreas { get { return currentState.LootTraderAreas; } }
    public int SleeperSpawnMultiplier { get { return currentState.SleeperSpawnMultiplier; } }
    public int InfestedSleeperSpawnMultiplier { get { return currentState.InfestedSleeperSpawnMultiplier; } }
    public bool SuppressConsoleErrorPopups { get { return currentState.SuppressConsoleErrorPopups; } }
    public int TreeDensityMultiplier { get { return currentState.TreeDensityMultiplier; } }
    public int VehicleDensityMultiplier { get { return currentState.VehicleDensityMultiplier; } }
    public bool BlocksCatchFire { get { return currentState.BlocksCatchFire; } }
    public bool FireAffectsHeatmap { get { return currentState.FireAffectsHeatmap; } }
    public RebirthFireBlockDamageSpeed FireBlockDamageSpeed { get { return currentState.FireBlockDamageSpeed; } }
    public int MaxJobs { get { return currentState.MaxJobs; } }
    public int JobsToNextTier { get { return currentState.JobsToNextTier; } }
    public RebirthRepeatPoiPolicy RepeatPoiJobs { get { return currentState.RepeatPoiJobs; } }
    public RebirthInfestedJobsMode InfestedJobs { get { return currentState.InfestedJobs; } }
    public RebirthTraderJobListMode TraderJobList { get { return currentState.TraderJobList; } }
    public bool TraderJobRecoveryGrace { get { return currentState.TraderJobRecoveryGrace; } }
    public RebirthTraderJobRecoveryGraceDuration TraderJobRecoveryGraceDuration
    {
        get { return currentState.TraderJobRecoveryGraceDuration; }
    }
    public RebirthHealingOverlapThreshold PreventHealingOverlap { get { return currentState.PreventHealingOverlap; } }
    public RebirthAlwaysStaggerMode AlwaysStagger { get { return currentState.AlwaysStagger; } }
    public RebirthWeatherFogBehavior WeatherFogBehavior { get { return currentState.WeatherFogBehavior; } }
    public RebirthWeatherFogIntensity WeatherFogIntensity { get { return currentState.WeatherFogIntensity; } }
    public bool UniformAtmosphere { get { return currentState.UniformAtmosphere; } }
    public RebirthPoiRiskMode PoiRisk { get { return currentState.PoiRisk; } }
    public RebirthPoiSenseSchedule PoiSenseSchedule { get { return currentState.PoiSenseSchedule; } }
    public RebirthPoiSenseIntensity PoiSenseIntensity { get { return currentState.PoiSenseIntensity; } }
    public bool PitchBlack { get { return currentState.PitchBlack; } }
    public bool BossEvents { get { return RebirthBossEventStartLevelPolicy.IsEnabled(currentState.BossEventStartLevel); } }
    public RebirthBossEventStartLevel BossEventStartLevel { get { return currentState.BossEventStartLevel; } }
    public RebirthBossEventFrequency BossEventFrequency { get { return currentState.BossEventFrequency; } }
    public int BossEventMaximumPerDay { get { return currentState.BossEventMaximumPerDay; } }
    public RebirthBossEventSize BossEventSize { get { return currentState.BossEventSize; } }
    public RebirthBossEventDifficulty BossEventDifficulty { get { return currentState.BossEventDifficulty; } }
    public RebirthBossEventTime BossEventTime { get { return currentState.BossEventTime; } }
    public bool BossEventBloodMoonDay { get { return currentState.BossEventBloodMoonDay; } }
    public RebirthBossEventRestriction BossEventRestriction { get { return currentState.BossEventRestriction; } }
    public bool BossEventRewards { get { return currentState.BossEventRewards; } }
    public bool BossEventNotifications { get { return currentState.BossEventNotifications; } }
    public bool ProtectCrate { get { return currentState.ProtectCrate; } }
    public int Revision { get { return revision; } }
    public string CurrentCode { get { return Encode(currentState); } }

    private RebirthSandboxOptionManager()
    {
        ReloadPresets();
    }

    public void ReloadPresets()
    {
        presets.Clear();

        presets.Add(new RebirthSandboxPreset
        {
            Name = DefaultPresetName,
            LocalizedNameKey = "xuiRebirthPresetStandard",
            DescriptionKey = "xuiRebirthPresetStandardDesc",
            Group = RecommendedGroupName,
            IsDefault = true,
            Code = Encode(new RebirthSandboxState())
        });


        LoadUserPresets();
    }

    public List<string> GetAllPresetGroups()
    {
        List<string> result = new List<string>();
        for (int i = 0; i < presets.Count; i++)
        {
            RebirthSandboxPreset preset = presets[i];
            if (!result.Contains(preset.Group))
                result.Add(preset.Group);
        }
        return result;
    }

    public List<RebirthSandboxPreset> GetPresetsForGroup(string group)
    {
        List<RebirthSandboxPreset> result = new List<RebirthSandboxPreset>();
        for (int i = 0; i < presets.Count; i++)
        {
            if (string.Equals(presets[i].Group, group, StringComparison.Ordinal))
                result.Add(presets[i]);
        }
        return result;
    }

    public RebirthSandboxPreset GetPreset(string name)
    {
        if (string.IsNullOrEmpty(name))
            name = DefaultPresetName;

        for (int i = 0; i < presets.Count; i++)
        {
            if (string.Equals(presets[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return presets[i];
        }
        return null;
    }

    public RebirthSandboxPreset GetPresetByCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return null;

        for (int i = 0; i < presets.Count; i++)
        {
            RebirthSandboxPreset preset = presets[i];
            if (string.Equals(preset.Code, code, StringComparison.OrdinalIgnoreCase))
                return preset;
        }
        return null;
    }

    public RebirthSandboxPreset ResolvePreset(string requestedName, string code)
    {
        // Preserve the selected source preset while its working values are being edited.
        // The authoritative code may differ without creating a visible Custom preset.
        RebirthSandboxPreset requested = GetPreset(requestedName);
        if (requested != null)
            return requested;

        RebirthSandboxPreset exact = GetPresetByCode(code);
        if (exact != null)
            return exact;

        return GetPreset(DefaultPresetName);
    }

    public bool LoadCode(string code, bool applyRuntime, bool incrementRevision)
    {
        RebirthSandboxState decoded;
        if (!TryDecode(code, out decoded))
            return false;

        currentState = decoded;
        if (incrementRevision)
            revision++;

        if (applyRuntime)
            ApplyRuntimeState();
        return true;
    }

    public bool LoadAuthoritativeSnapshot(string code, int authoritativeRevision, bool applyRuntime)
    {
        RebirthSandboxState decoded;
        if (!TryDecode(code, out decoded))
            return false;

        currentState = decoded;
        revision = Math.Max(0, authoritativeRevision);
        if (applyRuntime)
            ApplyRuntimeState();
        return true;
    }

    public bool ApplyNetworkSnapshot(string code, int incomingRevision)
    {
        if (incomingRevision < revision)
            return false;

        return LoadAuthoritativeSnapshot(code, incomingRevision, true);
    }

    public void ResetToDefaults(bool applyRuntime)
    {
        ResetToDefaultsAtRevision(revision + 1, applyRuntime);
    }

    public void ResetToDefaultsAtRevision(int authoritativeRevision, bool applyRuntime)
    {
        currentState = new RebirthSandboxState();
        revision = Math.Max(0, authoritativeRevision);
        if (applyRuntime)
            ApplyRuntimeState();
    }

    public void ApplyRuntimeState()
    {
        RebirthSurvivorMode.ApplyFromSandbox(currentState.PlayerProgression);
        AdvancedFarmingRuntimePolicy.SetEnabledFromRebirthSandbox(currentState.AdvancedFarming);
        QuickStackRuntimePolicy.SetMode(currentState.QuickStack);
        QuickStackRuntimePolicy.SetDistance(currentState.QuickStackDistance);
        RemoteResourcesRuntimePolicy.SetEnabled(currentState.RemoteResources);
        RemoteResourcesRuntimePolicy.SetDistance(currentState.RemoteResourcesDistance);
        RebirthAutoReplantTreesRuntimePolicy.SetEnabled(currentState.AutoReplantTrees);
        RebirthVehicleBlockRespawnRuntimePolicy.SetDays(currentState.VehicleBlockRespawnDays);
        RebirthHybridPathSmoothing.Enabled = currentState.HybridPathSmoothing;
        RebirthZombieDestroyAreaRuntimePolicy.SetEnabled(currentState.ZombiesDestroyAreas);
        RebirthSleeperRespawnRuntimePolicy.SetEnabled(currentState.SleeperRespawns);
        RebirthTraderLootPolicy.Enabled = currentState.LootTraderAreas;
        RebirthSleeperSpawnMultiplierRuntimePolicy.SetMultipliers(currentState.SleeperSpawnMultiplier, currentState.InfestedSleeperSpawnMultiplier);
        RebirthWorldDecorationDensityRuntimePolicy.SetMultipliers(currentState.TreeDensityMultiplier, currentState.VehicleDensityMultiplier);
        RebirthConsolePopupRuntimePolicy.SetSuppressAutomaticErrorPopups(currentState.SuppressConsoleErrorPopups);
        RebirthFireRuntimePolicy.SetEnabled(currentState.BlocksCatchFire);
        RebirthFireRuntimePolicy.SetAffectsHeatmap(
            currentState.BlocksCatchFire && currentState.FireAffectsHeatmap);
        RebirthFireRuntimePolicy.SetBlockDamageSpeed(currentState.FireBlockDamageSpeed);
        RebirthTraderJobPolicy.SetMaxJobs(currentState.MaxJobs);
        RebirthTraderJobPolicy.SetJobsToNextTier(currentState.JobsToNextTier);
        RebirthTraderJobPolicy.SetRepeatPoiPolicy(currentState.RepeatPoiJobs);
        RebirthInfestedJobsRuntimePolicy.SetMode(currentState.InfestedJobs);
        RebirthHealingOverlapRuntimePolicy.SetThreshold(currentState.PreventHealingOverlap);
        RebirthAlwaysStaggerRuntimePolicy.SetMode(currentState.AlwaysStagger);
        RebirthWeatherFogPolicy.SetOptions(currentState.WeatherFogBehavior, currentState.WeatherFogIntensity);
        RebirthUniformAtmospherePolicy.SetEnabled(currentState.UniformAtmosphere);
        RebirthPoiRiskRuntimePolicy.SetMode(
            RebirthSandboxOptionDependencyPolicy.GetEffectivePoiRisk(
                EffectiveSpawnProgression,
                currentState.PoiRisk));
        RebirthPoiSenseRuntimePolicy.SetOptions(currentState.PoiSenseSchedule, currentState.PoiSenseIntensity);
        RebirthPitchBlackPolicy.SetEnabled(currentState.PitchBlack);
        RebirthBossEventOptions.Apply(new RebirthBossEventOptionSnapshot
        {
            Enabled = RebirthBossEventStartLevelPolicy.IsEnabled(currentState.BossEventStartLevel),
            MinimumPlayerLevel = RebirthBossEventStartLevelPolicy.ToPlayerLevel(currentState.BossEventStartLevel),
            Frequency = currentState.BossEventFrequency,
            MaximumPerDay = currentState.BossEventMaximumPerDay <= 0 ? int.MaxValue : currentState.BossEventMaximumPerDay,
            Size = currentState.BossEventSize,
            Difficulty = currentState.BossEventDifficulty,
            Time = currentState.BossEventTime,
            BloodMoonDayEvents = currentState.BossEventBloodMoonDay,
            Restriction = currentState.BossEventRestriction,
            Rewards = currentState.BossEventRewards,
            Notifications = currentState.BossEventNotifications
        });
        RebirthProtectCrateRuntimePolicy.SetEnabled(currentState.ProtectCrate);
    }

    private void OnStateChanged(bool applyRuntime)
    {
        revision++;
        if (applyRuntime)
            ApplyRuntimeState();
    }

    private void MarkChanged(bool applyRuntime)
    {
        OnStateChanged(applyRuntime);
    }

    public void SetPlayerProgression(RebirthPlayerProgressionMode mode, bool applyRuntime)
    {
        if (mode != RebirthPlayerProgressionMode.BaseGame && mode != RebirthPlayerProgressionMode.Rebirth)
            mode = RebirthPlayerProgressionMode.Rebirth;
        if (currentState.PlayerProgression == mode)
            return;
        currentState.PlayerProgression = mode;
        OnStateChanged(applyRuntime);
    }

    public void SetAdvancedFarming(bool enabled, bool applyRuntime)
    {
        if (currentState.AdvancedFarming == enabled)
            return;

        currentState.AdvancedFarming = enabled;
        revision++;
        if (applyRuntime)
            ApplyRuntimeState();
    }

    public void SetQuickStack(RebirthQuickStackMode mode, bool applyRuntime)
    {
        if (currentState.QuickStack == mode)
            return;
        currentState.QuickStack = mode;
        revision++;
        if (applyRuntime)
            ApplyRuntimeState();
    }

    public void SetRemoteResources(bool enabled, bool applyRuntime)
    {
        if (currentState.RemoteResources == enabled)
            return;

        currentState.RemoteResources = enabled;
        revision++;
        if (applyRuntime)
            ApplyRuntimeState();
    }

    public void SetTheme(RebirthWorldTheme value, bool applyRuntime)
    {
        if (value != RebirthWorldTheme.None && value != RebirthWorldTheme.Purge)
        {
            Log.Warning("[RebirthSandbox] Unsupported Theme; using None.");
            value = RebirthWorldTheme.None;
        }
        if (currentState.Theme == value) return;
        currentState.Theme = value;
        OnStateChanged(applyRuntime);
    }
    public void SetShowClearedPois(bool value, bool applyRuntime)
    {
        if (currentState.ShowClearedPois == value) return;
        currentState.ShowClearedPois = value;
        OnStateChanged(applyRuntime);
    }
    public void SetScrollbarMode(RebirthScrollbarMode value, bool applyRuntime)
    {
        if(value!=RebirthScrollbarMode.Smooth&&value!=RebirthScrollbarMode.Paged)value=RebirthScrollbarMode.Smooth;
        if(currentState.ScrollbarMode==value)return;
        currentState.ScrollbarMode=value;revision++;if(applyRuntime)ApplyRuntimeState();
    }

    public void SetLiteratureStudyTime(RebirthLiteratureStudyTime value, bool applyRuntime)
    {
        if(value<RebirthLiteratureStudyTime.Quarter||value>RebirthLiteratureStudyTime.Double)value=RebirthLiteratureStudyTimePolicy.Default;
        if(currentState.LiteratureStudyTime==value)return;
        currentState.LiteratureStudyTime=value;revision++;if(applyRuntime)ApplyRuntimeState();
    }

    public void SetQuickStackDistance(int distance, bool applyRuntime)
    {
        distance = RebirthResourceDistancePolicy.NormalizeQuickStack(distance);
        if (currentState.QuickStackDistance == distance) return;
        currentState.QuickStackDistance = distance;
        OnStateChanged(applyRuntime);
    }

    public void SetRemoteResourcesDistance(int distance, bool applyRuntime)
    {
        distance = RebirthResourceDistancePolicy.NormalizeRemoteResources(distance);
        if (currentState.RemoteResourcesDistance == distance) return;
        currentState.RemoteResourcesDistance = distance;
        OnStateChanged(applyRuntime);
    }


    public void SetSecureAccessSharing(RebirthSecureAccessMode mode, bool applyRuntime) { if (currentState.SecureAccessSharing == mode) return; currentState.SecureAccessSharing = mode; revision++; if (applyRuntime) ApplyRuntimeState(); }

    public void SetHornActivatedDoors(RebirthHornActivatedDoorsMode mode, bool applyRuntime) { if (currentState.HornActivatedDoors == mode) return; currentState.HornActivatedDoors = mode; revision++; if (applyRuntime) ApplyRuntimeState(); }

    public void SetSpawnProgression(RebirthSpawnProgressionMode mode, bool applyRuntime)
    {
        if ((int)mode < (int)RebirthSpawnProgressionMode.Biome || (int)mode > (int)RebirthSpawnProgressionMode.Gamestage)
            mode = RebirthSpawnProgressionMode.Gamestage;

        if (currentState.SpawnProgression == mode)
            return;

        // Do not overwrite PoiRisk here. Biome progression makes its effective value
        // None, while the configured value remains available for restoration when the
        // player switches back to Gamestage progression.
        currentState.SpawnProgression = mode;
        revision++;
        if (applyRuntime)
            ApplyRuntimeState();
    }

    public void SetAutoReplantTrees(bool enabled, bool applyRuntime)
    {
        if (currentState.AutoReplantTrees == enabled) return;
        currentState.AutoReplantTrees = enabled;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }

    public void SetVehicleBlockRespawnDays(int days, bool applyRuntime)
    {
        if (days != 0 && days != 3 && days != 7 && days != 14 && days != 21) days = 0;
        if (currentState.VehicleBlockRespawnDays == days) return;
        currentState.VehicleBlockRespawnDays = days;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }

    public void SetHybridPathSmoothing(bool enabled, bool applyRuntime)
    {
        if (currentState.HybridPathSmoothing == enabled) return;
        currentState.HybridPathSmoothing = enabled;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }

    public void SetZombiesDestroyAreas(bool enabled, bool applyRuntime)
    {
        if (currentState.ZombiesDestroyAreas == enabled) return;
        currentState.ZombiesDestroyAreas = enabled;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }

    public void SetRancherRangedAttack(bool enabled, bool applyRuntime)
    {
        if (currentState.RancherRangedAttack == enabled) return;
        currentState.RancherRangedAttack = enabled;
        // This option is consumed while entity XML is patched. Keep it in the
        // authoritative state/code even though there is no hot runtime mutation.
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }

    public void SetLootTraderAreas(bool enabled, bool applyRuntime)
    {
        if (currentState.LootTraderAreas == enabled) return;
        currentState.LootTraderAreas = enabled;
        OnStateChanged(applyRuntime);
    }


    public void SetSleeperSpawnMultiplier(int multiplier, bool applyRuntime)
    {
        multiplier = Math.Max(1, Math.Min(4, multiplier));
        if (currentState.SleeperSpawnMultiplier == multiplier) return;
        currentState.SleeperSpawnMultiplier = multiplier;
        OnStateChanged(applyRuntime);
    }

    public void SetInfestedSleeperSpawnMultiplier(int multiplier, bool applyRuntime)
    {
        multiplier = Math.Max(2, Math.Min(6, multiplier));
        if (currentState.InfestedSleeperSpawnMultiplier == multiplier) return;
        currentState.InfestedSleeperSpawnMultiplier = multiplier;
        OnStateChanged(applyRuntime);
    }

    public void SetSuppressConsoleErrorPopups(bool enabled, bool applyRuntime)
    {
        if (currentState.SuppressConsoleErrorPopups == enabled) return;
        currentState.SuppressConsoleErrorPopups = enabled;
        OnStateChanged(applyRuntime);
    }

    public void SetSleeperRespawns(bool enabled, bool applyRuntime)
    {
        if (currentState.SleeperRespawns == enabled) return;
        currentState.SleeperRespawns = enabled;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }

    public void SetInstantBlockPickup(RebirthInstantBlockPickupMode mode, bool applyRuntime)
    {
        if (currentState.InstantBlockPickup == mode)
            return;

        currentState.InstantBlockPickup = mode;
        revision++;
        if (applyRuntime)
            ApplyRuntimeState();
    }

    public void SetTreeDensityMultiplier(int multiplier, bool applyRuntime)
    {
        multiplier = RebirthWorldDecorationDensityRuntimePolicy.NormalizeMultiplier(multiplier, 100);
        if (currentState.TreeDensityMultiplier == multiplier) return;
        currentState.TreeDensityMultiplier = multiplier;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }

    public void SetVehicleDensityMultiplier(int multiplier, bool applyRuntime)
    {
        multiplier = RebirthWorldDecorationDensityRuntimePolicy.NormalizeMultiplier(multiplier, 200);
        if (currentState.VehicleDensityMultiplier == multiplier) return;
        currentState.VehicleDensityMultiplier = multiplier;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }

    public void SetBlocksCatchFire(bool enabled, bool applyRuntime)
    {
        if (currentState.BlocksCatchFire == enabled) return;
        currentState.BlocksCatchFire = enabled;
        OnStateChanged(applyRuntime);
    }

    public void SetFireAffectsHeatmap(bool enabled, bool applyRuntime)
    {
        if (currentState.FireAffectsHeatmap == enabled) return;
        currentState.FireAffectsHeatmap = enabled;
        OnStateChanged(applyRuntime);
    }

    public void SetFireBlockDamageSpeed(RebirthFireBlockDamageSpeed speed, bool applyRuntime)
    {
        if ((int)speed < (int)RebirthFireBlockDamageSpeed.Slower ||
            (int)speed > (int)RebirthFireBlockDamageSpeed.Faster)
            speed = RebirthFireBlockDamageSpeed.Default;
        if (currentState.FireBlockDamageSpeed == speed) return;
        currentState.FireBlockDamageSpeed = speed;
        OnStateChanged(applyRuntime);
    }

    public void SetMaxJobs(int maxJobs, bool applyRuntime)
    {
        maxJobs = RebirthTraderJobPolicy.NormalizeMaxJobs(maxJobs);
        if (currentState.MaxJobs == maxJobs) return;
        currentState.MaxJobs = maxJobs;
        OnStateChanged(applyRuntime);
    }

    public void SetJobsToNextTier(int jobsToNextTier, bool applyRuntime)
    {
        jobsToNextTier = RebirthTraderJobPolicy.NormalizeJobsToNextTier(jobsToNextTier);
        if (currentState.JobsToNextTier == jobsToNextTier) return;
        currentState.JobsToNextTier = jobsToNextTier;
        OnStateChanged(applyRuntime);
    }

    public void SetRepeatPoiJobs(RebirthRepeatPoiPolicy policy, bool applyRuntime)
    {
        if (policy < RebirthRepeatPoiPolicy.None || policy > RebirthRepeatPoiPolicy.Unlimited) policy = RebirthRepeatPoiPolicy.Medium;
        if (currentState.RepeatPoiJobs == policy) return;
        currentState.RepeatPoiJobs = policy;
        OnStateChanged(applyRuntime);
    }

    public void SetInfestedJobs(RebirthInfestedJobsMode mode, bool applyRuntime)
    {
        if (mode < RebirthInfestedJobsMode.Default || mode > RebirthInfestedJobsMode.Surprise) mode = RebirthInfestedJobsMode.Default;
        if (currentState.InfestedJobs == mode) return;
        currentState.InfestedJobs = mode;
        OnStateChanged(applyRuntime);
    }

    public void SetTraderJobList(RebirthTraderJobListMode mode, bool applyRuntime)
    {
        if (mode < RebirthTraderJobListMode.Fixed || mode > RebirthTraderJobListMode.Random) mode = RebirthTraderJobListMode.Fixed;
        if (currentState.TraderJobList == mode) return;
        currentState.TraderJobList = mode;
        OnStateChanged(applyRuntime);
    }

    public void SetTraderJobRecoveryGrace(bool enabled, bool applyRuntime)
    {
        if (currentState.TraderJobRecoveryGrace == enabled) return;
        currentState.TraderJobRecoveryGrace = enabled;
        OnStateChanged(applyRuntime);
    }

    public void SetTraderJobRecoveryGraceDuration(
        RebirthTraderJobRecoveryGraceDuration duration,
        bool applyRuntime)
    {
        if (duration < RebirthTraderJobRecoveryGraceDuration.FiveMinutes ||
            duration > RebirthTraderJobRecoveryGraceDuration.FifteenMinutes)
            duration = RebirthTraderJobRecoveryGraceDuration.FiveMinutes;

        if (currentState.TraderJobRecoveryGraceDuration == duration) return;
        currentState.TraderJobRecoveryGraceDuration = duration;
        OnStateChanged(applyRuntime);
    }

    public void SetPreventHealingOverlap(RebirthHealingOverlapThreshold threshold, bool applyRuntime)
    {
        threshold = RebirthHealingOverlapThresholdPolicy.Normalize(threshold);
        if (currentState.PreventHealingOverlap == threshold) return;
        currentState.PreventHealingOverlap = threshold;
        OnStateChanged(applyRuntime);
    }

    public void SetAlwaysStagger(RebirthAlwaysStaggerMode mode, bool applyRuntime)
    {
        if (mode < RebirthAlwaysStaggerMode.Disabled || mode > RebirthAlwaysStaggerMode.AllQualifyingHits) mode = RebirthAlwaysStaggerMode.AllQualifyingHits;
        if (currentState.AlwaysStagger == mode) return;
        currentState.AlwaysStagger = mode;
        OnStateChanged(applyRuntime);
    }


    public void SetWeatherFogBehavior(RebirthWeatherFogBehavior behavior, bool applyRuntime)
    {
        if (behavior < RebirthWeatherFogBehavior.Dynamic || behavior > RebirthWeatherFogBehavior.Disabled)
            behavior = RebirthWeatherFogBehavior.Dynamic;
        if (currentState.WeatherFogBehavior == behavior) return;
        currentState.WeatherFogBehavior = behavior;
        OnStateChanged(applyRuntime);
    }

    public void SetWeatherFogIntensity(RebirthWeatherFogIntensity intensity, bool applyRuntime)
    {
        if (!RebirthSandboxOptionDependencyPolicy.IsWeatherFogIntensityEnabled(currentState.WeatherFogBehavior))
            return;

        if (intensity < RebirthWeatherFogIntensity.None || intensity > RebirthWeatherFogIntensity.VeryHeavy)
            intensity = RebirthWeatherFogIntensity.Normal;
        if (currentState.WeatherFogIntensity == intensity) return;
        currentState.WeatherFogIntensity = intensity;
        OnStateChanged(applyRuntime);
    }
    public void SetProtectCrate(bool value, bool applyRuntime) { if (currentState.ProtectCrate == value) return; currentState.ProtectCrate = value; revision++; if (applyRuntime) ApplyRuntimeState(); }

    public void SetCompanionCardStyle(RebirthCompanionCardStyle value, bool applyRuntime)
    {
        if ((int)value < (int)RebirthCompanionCardStyle.Default || (int)value > (int)RebirthCompanionCardStyle.Simple)
            value = RebirthCompanionCardStyle.Simple;
        if (currentState.CompanionCardStyle == value) return;
        currentState.CompanionCardStyle = value;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }
    public void SetTargetNameColor(int r, int g, int b, bool applyRuntime)
    {
        r = RebirthTargetNameColorPolicy.ClampChannel(r);
        g = RebirthTargetNameColorPolicy.ClampChannel(g);
        b = RebirthTargetNameColorPolicy.ClampChannel(b);
        if (currentState.TargetNameColorR == r &&
            currentState.TargetNameColorG == g &&
            currentState.TargetNameColorB == b)
            return;
        currentState.TargetNameColorR = r;
        currentState.TargetNameColorG = g;
        currentState.TargetNameColorB = b;
        OnStateChanged(applyRuntime);
    }

    public void SetBossEvents(bool value, bool applyRuntime)
    {
        SetBossEventStartLevel(value ? RebirthBossEventStartLevel.PlayerLevel15 : RebirthBossEventStartLevel.Off, applyRuntime);
    }
    public void SetBossEventStartLevel(RebirthBossEventStartLevel value, bool applyRuntime)
    {
        value = RebirthBossEventStartLevelPolicy.Normalize(value);
        if (currentState.BossEventStartLevel == value) return;
        currentState.BossEventStartLevel = value;
        revision++;
        if (applyRuntime) ApplyRuntimeState();
    }
    public void SetBossEventFrequency(RebirthBossEventFrequency value, bool applyRuntime) { if (value < RebirthBossEventFrequency.Rare || value > RebirthBossEventFrequency.VeryHigh) value = RebirthBossEventFrequency.Normal; if (currentState.BossEventFrequency == value) return; currentState.BossEventFrequency = value; revision++; if (applyRuntime) ApplyRuntimeState(); }
    public void SetBossEventMaximumPerDay(int value, bool applyRuntime) { if (value != 0 && (value < 1 || value > 5)) value = 2; if (currentState.BossEventMaximumPerDay == value) return; currentState.BossEventMaximumPerDay = value; revision++; if (applyRuntime) ApplyRuntimeState(); }
    public void SetBossEventSize(RebirthBossEventSize value, bool applyRuntime) { if (value < RebirthBossEventSize.Small || value > RebirthBossEventSize.Random) value = RebirthBossEventSize.Normal; if (currentState.BossEventSize == value) return; currentState.BossEventSize = value; revision++; if (applyRuntime) ApplyRuntimeState(); }
    public void SetBossEventDifficulty(RebirthBossEventDifficulty value, bool applyRuntime) { if (value < RebirthBossEventDifficulty.Low || value > RebirthBossEventDifficulty.Random) value = RebirthBossEventDifficulty.Normal; if (currentState.BossEventDifficulty == value) return; currentState.BossEventDifficulty = value; revision++; if (applyRuntime) ApplyRuntimeState(); }
    public void SetBossEventTime(RebirthBossEventTime value, bool applyRuntime) { if (value < RebirthBossEventTime.DayOnly || value > RebirthBossEventTime.NightOnly) value = RebirthBossEventTime.DayAndNight; if (currentState.BossEventTime == value) return; currentState.BossEventTime = value; revision++; if (applyRuntime) ApplyRuntimeState(); }
    public void SetBossEventBloodMoonDay(bool value, bool applyRuntime) { if (currentState.BossEventBloodMoonDay == value) return; currentState.BossEventBloodMoonDay = value; revision++; if (applyRuntime) ApplyRuntimeState(); }
    public void SetBossEventRestriction(RebirthBossEventRestriction value, bool applyRuntime) { if (value < RebirthBossEventRestriction.Anywhere || value > RebirthBossEventRestriction.OutdoorsAndNotQuesting) value = RebirthBossEventRestriction.Anywhere; if (currentState.BossEventRestriction == value) return; currentState.BossEventRestriction = value; revision++; if (applyRuntime) ApplyRuntimeState(); }
    public void SetBossEventRewards(bool value, bool applyRuntime) { if (currentState.BossEventRewards == value) return; currentState.BossEventRewards = value; revision++; if (applyRuntime) ApplyRuntimeState(); }
    public void SetBossEventNotifications(bool value, bool applyRuntime) { if (currentState.BossEventNotifications == value) return; currentState.BossEventNotifications = value; revision++; if (applyRuntime) ApplyRuntimeState(); }

    public void SetPitchBlack(bool enabled, bool applyRuntime)
    {
        if (currentState.PitchBlack == enabled) return;
        currentState.PitchBlack = enabled;
        MarkChanged(applyRuntime);
    }

    public void SetUniformAtmosphere(bool enabled, bool applyRuntime)
    {
        if (currentState.UniformAtmosphere == enabled) return;
        currentState.UniformAtmosphere = enabled;
        OnStateChanged(applyRuntime);
    }

    public void SetPoiRisk(RebirthPoiRiskMode mode, bool applyRuntime)
    {
        // A disabled dependent option cannot be edited, but its configured value is
        // intentionally retained so removing the parent constraint restores it.
        if (!RebirthSandboxOptionDependencyPolicy.IsPoiRiskEnabled(currentState.SpawnProgression))
            return;

        if (mode < RebirthPoiRiskMode.None || mode > RebirthPoiRiskMode.High)
            mode = RebirthPoiRiskMode.Medium;

        if (currentState.PoiRisk == mode) return;
        currentState.PoiRisk = mode;
        OnStateChanged(applyRuntime);
    }


    public void SetPoiSenseSchedule(RebirthPoiSenseSchedule value, bool applyRuntime)
    {
        if (value < RebirthPoiSenseSchedule.Never || value > RebirthPoiSenseSchedule.Always) value = RebirthPoiSenseSchedule.Never;
        if (currentState.PoiSenseSchedule == value) return;
        currentState.PoiSenseSchedule = value;
        OnStateChanged(applyRuntime);
    }

    public void SetPoiSenseIntensity(RebirthPoiSenseIntensity value, bool applyRuntime)
    {
        if (!RebirthSandboxOptionDependencyPolicy.IsPoiSenseIntensityEnabled(currentState.PoiSenseSchedule))
            return;

        if (value < RebirthPoiSenseIntensity.Low || value > RebirthPoiSenseIntensity.High) value = RebirthPoiSenseIntensity.Medium;
        if (currentState.PoiSenseIntensity == value) return;
        currentState.PoiSenseIntensity = value;
        OnStateChanged(applyRuntime);
    }

    private static void AppendColorChannel(ref string result, RebirthSandboxOptionId highId, RebirthSandboxOptionId lowId, int value, int defaultValue)
    {
        int high = value / 26;
        int low = value % 26;
        int defaultHigh = defaultValue / 26;
        int defaultLow = defaultValue % 26;
        if (high != defaultHigh)
            result += IndexToAlpha2((int)highId) + IndexToAlpha(high);
        if (low != defaultLow)
            result += IndexToAlpha2((int)lowId) + IndexToAlpha(low);
    }

    public static string Encode(RebirthSandboxState state)
    {
        if (state == null)
            state = new RebirthSandboxState();

        string result = CodePrefix + CurrentVersion;
        if (state.Theme == RebirthWorldTheme.Purge)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.Theme) + IndexToAlpha(1);
        if (state.ShowClearedPois)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.ShowClearedPois) + IndexToAlpha(1);
        if (state.PlayerProgression != RebirthPlayerProgressionMode.Rebirth)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.PlayerProgression) + IndexToAlpha((int)state.PlayerProgression);
        if (!state.AdvancedFarming)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.AdvancedFarming) + IndexToAlpha(0);
        if (state.InstantBlockPickup != RebirthInstantBlockPickupMode.Default)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.InstantBlockPickup)
                + IndexToAlpha((int)state.InstantBlockPickup);
        if (state.QuickStack != RebirthQuickStackMode.Full)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.QuickStack)
                + IndexToAlpha((int)state.QuickStack);
        if (state.QuickStackDistance != RebirthResourceDistancePolicy.QuickStackDefault)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.QuickStackDistance)
                + IndexToAlpha(RebirthResourceDistancePolicy.QuickStackDistanceToIndex(state.QuickStackDistance));
        if (!state.RemoteResources)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.RemoteResources) + IndexToAlpha(0);
        if (state.RemoteResourcesDistance != RebirthResourceDistancePolicy.RemoteResourcesDefault)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.RemoteResourcesDistance)
                + IndexToAlpha(RebirthResourceDistancePolicy.RemoteResourcesDistanceToIndex(state.RemoteResourcesDistance));
        if (state.SecureAccessSharing != RebirthSecureAccessMode.PinAccessList)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.SecureAccessSharing) + IndexToAlpha((int)state.SecureAccessSharing);
        if (state.HornActivatedDoors != RebirthHornActivatedDoorsMode.All)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.HornActivatedDoors) + IndexToAlpha((int)state.HornActivatedDoors);
        result += IndexToAlpha2((int)RebirthSandboxOptionId.SpawnProgression) + IndexToAlpha((int)state.SpawnProgression);
        if (state.AutoReplantTrees)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.AutoReplantTrees) + IndexToAlpha(1);
        if (state.VehicleBlockRespawnDays != 0)
        {
            int vehicleRespawnIndex = state.VehicleBlockRespawnDays == 3 ? 1 : state.VehicleBlockRespawnDays == 7 ? 2 : state.VehicleBlockRespawnDays == 14 ? 3 : 4;
            result += IndexToAlpha2((int)RebirthSandboxOptionId.VehicleBlockRespawn) + IndexToAlpha(vehicleRespawnIndex);
        }
        if (!state.HybridPathSmoothing)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.HybridPathSmoothing) + IndexToAlpha(0);
        if (state.ZombiesDestroyAreas)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.ZombiesDestroyAreas) + IndexToAlpha(1);
        if (state.RancherRangedAttack)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.RancherRangedAttack) + IndexToAlpha(1);
        if (state.SleeperRespawns)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.SleeperRespawns) + IndexToAlpha(1);
        if (!state.LootTraderAreas)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.LootTraderAreas) + IndexToAlpha(0);
        if (state.SleeperSpawnMultiplier != 1)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.SleeperSpawnMultiplier) + IndexToAlpha(state.SleeperSpawnMultiplier - 1);
        if (state.InfestedSleeperSpawnMultiplier != 2)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.InfestedSleeperSpawnMultiplier) + IndexToAlpha(state.InfestedSleeperSpawnMultiplier - 2);
        if (state.SuppressConsoleErrorPopups)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.SuppressConsoleErrorPopups) + IndexToAlpha(1);
        if (state.TreeDensityMultiplier != 100)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.TreeDensityMultiplier) + IndexToAlpha(RebirthWorldDecorationDensityRuntimePolicy.MultiplierToIndex(state.TreeDensityMultiplier));
        if (state.VehicleDensityMultiplier != 100)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.VehicleDensityMultiplier) + IndexToAlpha(RebirthWorldDecorationDensityRuntimePolicy.MultiplierToIndex(state.VehicleDensityMultiplier));
        if (state.BlocksCatchFire)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.BlocksCatchFire) + IndexToAlpha(1);
        if (state.FireAffectsHeatmap)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.FireAffectsHeatmap) + IndexToAlpha(1);
        if (state.FireBlockDamageSpeed != RebirthFireBlockDamageSpeed.Default)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.FireBlockDamageSpeed) + IndexToAlpha((int)state.FireBlockDamageSpeed);
        if (state.MaxJobs != 11)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.MaxJobs) + IndexToAlpha(RebirthTraderJobPolicy.MaxJobsToPersistedIndex(state.MaxJobs));
        if (state.JobsToNextTier != RebirthTraderJobPolicy.DefaultJobsToNextTier)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.JobsToNextTier) + IndexToAlpha(RebirthTraderJobPolicy.JobsToNextTierToIndex(state.JobsToNextTier));
        // v96 defaults changed from None/Surprise/Random to Medium/Default/Fixed.
        // Always encode these three values. Older R-and-earlier codes often omitted
        // their historical defaults, so TryDecode intentionally seeds those old
        // defaults before applying any explicit entries.
        result += IndexToAlpha2((int)RebirthSandboxOptionId.RepeatPoiJobs) + IndexToAlpha((int)state.RepeatPoiJobs);
        result += IndexToAlpha2((int)RebirthSandboxOptionId.InfestedJobs) + IndexToAlpha((int)state.InfestedJobs);
        result += IndexToAlpha2((int)RebirthSandboxOptionId.TraderJobList) + IndexToAlpha((int)state.TraderJobList);
        if(state.ScrollbarMode!=RebirthScrollbarMode.Smooth)result += IndexToAlpha2((int)RebirthSandboxOptionId.ScrollbarMode)+IndexToAlpha((int)state.ScrollbarMode);
        if (!state.RequireTimedReading) result += IndexToAlpha2((int)RebirthSandboxOptionId.RequireTimedReading) + IndexToAlpha(0);
        if (state.LiteratureStudyTime != RebirthLiteratureStudyTimePolicy.Default)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.LiteratureStudyTime) + IndexToAlpha((int)state.LiteratureStudyTime);

        if (!state.TraderJobRecoveryGrace)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.TraderJobRecoveryGrace) + IndexToAlpha(0);

        if (state.TraderJobRecoveryGraceDuration !=
            RebirthTraderJobRecoveryGraceDuration.FiveMinutes)
        {
            result += IndexToAlpha2(
                (int)RebirthSandboxOptionId.TraderJobRecoveryGraceDuration) +
                IndexToAlpha((int)state.TraderJobRecoveryGraceDuration);
        }

        if (state.PreventHealingOverlap != RebirthHealingOverlapThreshold.Health10)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.PreventHealingOverlap) + IndexToAlpha((int)state.PreventHealingOverlap);
        if (state.AlwaysStagger != RebirthAlwaysStaggerMode.AllQualifyingHits)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.AlwaysStagger) + IndexToAlpha((int)state.AlwaysStagger);
        if (state.WeatherFogBehavior != RebirthWeatherFogBehavior.Dynamic)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.WeatherFogBehavior) + IndexToAlpha((int)state.WeatherFogBehavior);
        if (state.WeatherFogIntensity != RebirthWeatherFogIntensity.Normal)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.WeatherFogIntensity) + IndexToAlpha(WeatherFogIntensityToPersistedIndex(state.WeatherFogIntensity));
        // Uniform Atmosphere defaults to enabled from code version Q onward. Encode
        // the disabled state explicitly so new saves can opt out while older codes
        // retain their original omitted=false behavior.
        if (!state.UniformAtmosphere)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.UniformAtmosphere) + IndexToAlpha(0);
        // Encode the configured value even while a parent option temporarily disables
        // POI Risk. This allows save/load and network snapshots to restore the setting
        // when Gamestage progression becomes active again.
        if (state.PoiRisk != RebirthPoiRiskMode.Medium)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.PoiRisk) + IndexToAlpha((int)state.PoiRisk);
        if (state.PoiSenseSchedule != RebirthPoiSenseSchedule.Never)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.PoiSenseSchedule) + IndexToAlpha((int)state.PoiSenseSchedule);
        if (state.PoiSenseIntensity != RebirthPoiSenseIntensity.Medium)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.PoiSenseIntensity) + IndexToAlpha((int)state.PoiSenseIntensity);
        // Pitch Black defaults to enabled from code version R onward. Encode the
        // disabled state explicitly so new saves can opt out while Q-and-older codes
        // retain their original omitted=false behavior.
        if (!state.PitchBlack)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.PitchBlack) + IndexToAlpha(0);
        if (state.BossEventStartLevel != RebirthBossEventStartLevel.PlayerLevel15) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEvents) + IndexToAlpha((int)state.BossEventStartLevel);
        if (state.BossEventFrequency != RebirthBossEventFrequency.Normal) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventFrequency) + IndexToAlpha((int)state.BossEventFrequency);
        if (state.BossEventMaximumPerDay != 2) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventMaximumPerDay) + IndexToAlpha(state.BossEventMaximumPerDay == 0 ? 5 : state.BossEventMaximumPerDay - 1);
        if (state.BossEventSize != RebirthBossEventSize.Normal) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventSize) + IndexToAlpha((int)state.BossEventSize);
        if (state.BossEventDifficulty != RebirthBossEventDifficulty.Normal) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventDifficulty) + IndexToAlpha((int)state.BossEventDifficulty);
        if (state.BossEventTime != RebirthBossEventTime.DayAndNight) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventTime) + IndexToAlpha((int)state.BossEventTime);
        if (!state.BossEventBloodMoonDay) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventBloodMoonDay) + IndexToAlpha(0);
        if (state.BossEventRestriction != RebirthBossEventRestriction.Anywhere) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventRestriction) + IndexToAlpha((int)state.BossEventRestriction);
        if (!state.BossEventRewards) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventRewards) + IndexToAlpha(0);
        if (!state.BossEventNotifications) result += IndexToAlpha2((int)RebirthSandboxOptionId.BossEventNotifications) + IndexToAlpha(0);
        if (!state.ProtectCrate) result += IndexToAlpha2((int)RebirthSandboxOptionId.ProtectCrate) + IndexToAlpha(0);
        if (state.GuardDistance != RebirthCompanionDistancePolicy.GuardDefault)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.GuardDistance) + IndexToAlpha(RebirthCompanionDistancePolicy.GuardDistanceToIndex(state.GuardDistance));
        if (state.FullControlDistance != RebirthCompanionDistancePolicy.FullControlDefault)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.FullControlDistance) + IndexToAlpha(RebirthCompanionDistancePolicy.FullControlDistanceToIndex(state.FullControlDistance));
        if (state.HuntingDistance != RebirthCompanionDistancePolicy.HuntingDefault)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.HuntingDistance) + IndexToAlpha(RebirthCompanionDistancePolicy.HuntingDistanceToIndex(state.HuntingDistance));
        // Simple Cards are the REBIRTH default. Only encode this option when the
        // player explicitly selects the alternate Default Cards presentation.
        if (state.CompanionCardStyle != RebirthCompanionCardStyle.Simple)
            result += IndexToAlpha2((int)RebirthSandboxOptionId.CompanionCardStyle) + IndexToAlpha((int)state.CompanionCardStyle);

        AppendColorChannel(ref result, RebirthSandboxOptionId.TargetNameColorRHigh, RebirthSandboxOptionId.TargetNameColorRLow,
            RebirthTargetNameColorPolicy.ClampChannel(state.TargetNameColorR), RebirthTargetNameColorPolicy.DefaultR);
        AppendColorChannel(ref result, RebirthSandboxOptionId.TargetNameColorGHigh, RebirthSandboxOptionId.TargetNameColorGLow,
            RebirthTargetNameColorPolicy.ClampChannel(state.TargetNameColorG), RebirthTargetNameColorPolicy.DefaultG);
        AppendColorChannel(ref result, RebirthSandboxOptionId.TargetNameColorBHigh, RebirthSandboxOptionId.TargetNameColorBLow,
            RebirthTargetNameColorPolicy.ClampChannel(state.TargetNameColorB), RebirthTargetNameColorPolicy.DefaultB);
        return result;
    }

    public static bool TryDecode(string code, out RebirthSandboxState state)
    {
        state = null;
        if (string.IsNullOrEmpty(code))
            return false;
        if (code.Length < 3 || !code.StartsWith(CodePrefix, StringComparison.Ordinal) ||
            (code[2] != CurrentVersion && code[2] != PreviousVersionW && code[2] != PreviousVersion000000000000 && code[2] != PreviousVersion00000000000 && code[2] != PreviousVersion0000000000 && code[2] != PreviousVersion000000000 && code[2] != PreviousVersion00000000 && code[2] != PreviousVersion0000000 && code[2] != PreviousVersion000000 && code[2] != PreviousVersion00000 && code[2] != PreviousVersion0000 && code[2] != PreviousVersion000 && code[2] != PreviousVersion00 && code[2] != PreviousVersion0 && code[2] != PreviousVersion && code[2] != PreviousVersion2 && code[2] != OlderVersion4 && code[2] != OlderVersion3 && code[2] != OlderVersion2 && code[2] != OlderVersion && code[2] != LegacyVersion && code[2] != OldestVersion && code[2] != AncientVersion && code[2] != EarliestVersion))
            return false;
        char sourceVersion = code[2];
        bool legacyBooleanRepeatPoi = sourceVersion == EarliestVersion;
        if ((code.Length - 3) % 3 != 0)
            return false;

        RebirthSandboxState decoded = new RebirthSandboxState();
        // Older codes omit the original Gamestage default. Preserve that saved choice.
        decoded.SpawnProgression = RebirthSpawnProgressionMode.Gamestage;
        // V introduces Player Progression. All U-and-earlier codes preserve their pre-Survivor meaning.
        if (sourceVersion < PreviousVersion000000000000)
            decoded.PlayerProgression = RebirthPlayerProgressionMode.BaseGame;

        // v96 changed the NEW/default values for these three options. All existing
        // R-and-earlier sandbox codes were produced before that change and could omit
        // the historical default values entirely. Seed the historical values first;
        // any explicitly encoded option below still overrides them. New v96 codes
        // always encode all three values, so they decode to the new defaults exactly.
        decoded.RepeatPoiJobs = RebirthRepeatPoiPolicy.None;
        decoded.InfestedJobs = RebirthInfestedJobsMode.Surprise;
        decoded.TraderJobList = RebirthTraderJobListMode.Random;

        // Uniform Atmosphere defaulted to disabled through version P. Preserve an
        // omitted setting in those older codes as Off; versions Q and later default On.
        if (sourceVersion <= PreviousVersion000000) // P and earlier
            decoded.UniformAtmosphere = false;

        // Pitch Black defaults to enabled from version R onward. All Q-and-older
        // codes omitted the option when it was Off, so preserve that old default.
        if (sourceVersion <= PreviousVersion0000000) // Q and earlier
            decoded.PitchBlack = false;

        // Codes written before version N treated Blocks Catch Fire as enabled when the
        // option was omitted. Preserve those existing-world values while keeping N/O/P/Q/R default off.
        if (sourceVersion <= PreviousVersion000) // M and earlier
            decoded.BlocksCatchFire = true;

        int targetNameRHigh = decoded.TargetNameColorR / 26;
        int targetNameRLow = decoded.TargetNameColorR % 26;
        int targetNameGHigh = decoded.TargetNameColorG / 26;
        int targetNameGLow = decoded.TargetNameColorG % 26;
        int targetNameBHigh = decoded.TargetNameColorB / 26;
        int targetNameBLow = decoded.TargetNameColorB % 26;

        for (int i = 3; i < code.Length; i += 3)
        {
            int optionIndex = Alpha2ToIndex(code[i], code[i + 1]);
            int valueIndex = AlphaToIndex(code[i + 2]);
            if (optionIndex < 0 || valueIndex < 0)
                return false;

            if (optionIndex == (int)RebirthSandboxOptionId.PlayerProgression)
            {
                if (valueIndex > 1)
                    return false;
                decoded.PlayerProgression = (RebirthPlayerProgressionMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.Theme)
            {
                // W and earlier treated these IDs as unknown. Preserve that behavior.
                if (sourceVersion >= 'X')
                {
                    if (valueIndex > 1) return false;
                    decoded.Theme = (RebirthWorldTheme)valueIndex;
                }
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.ShowClearedPois)
            {
                if (sourceVersion >= 'X')
                {
                    if (valueIndex > 1) return false;
                    decoded.ShowClearedPois = valueIndex == 1;
                }
            }
            else if(optionIndex==(int)RebirthSandboxOptionId.ScrollbarMode)
            {
                if(valueIndex>1)return false; decoded.ScrollbarMode=(RebirthScrollbarMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.RequireTimedReading)
            {
                if(valueIndex>1)return false; decoded.RequireTimedReading=valueIndex==1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.LiteratureStudyTime)
            {
                if(valueIndex>(int)RebirthLiteratureStudyTime.Double)return false;
                decoded.LiteratureStudyTime=(RebirthLiteratureStudyTime)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.AdvancedFarming)
            {
                if (valueIndex > 1)
                    return false;
                decoded.AdvancedFarming = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.InstantBlockPickup)
            {
                if (valueIndex > (int)RebirthInstantBlockPickupMode.Always)
                    return false;
                decoded.InstantBlockPickup = (RebirthInstantBlockPickupMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.QuickStack)
            {
                if (valueIndex > (int)RebirthQuickStackMode.Full)
                    return false;
                decoded.QuickStack = (RebirthQuickStackMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.RemoteResources)
            {
                if (valueIndex > 1)
                    return false;
                decoded.RemoteResources = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.QuickStackDistance)
            {
                int distance;
                if (!RebirthResourceDistancePolicy.TryDecodeQuickStackDistanceIndex(valueIndex, out distance)) return false;
                decoded.QuickStackDistance = distance;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.RemoteResourcesDistance)
            {
                int distance;
                if (!RebirthResourceDistancePolicy.TryDecodeRemoteResourcesDistanceIndex(valueIndex, out distance)) return false;
                decoded.RemoteResourcesDistance = distance;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.SecureAccessSharing) { if (valueIndex > 2) return false; decoded.SecureAccessSharing = (RebirthSecureAccessMode)valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.HornActivatedDoors) { if (valueIndex > 2) return false; decoded.HornActivatedDoors = (RebirthHornActivatedDoorsMode)valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.SpawnProgression)
            {
                if (valueIndex > (int)RebirthSpawnProgressionMode.Gamestage)
                    return false;
                decoded.SpawnProgression = (RebirthSpawnProgressionMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.AutoReplantTrees)
            {
                if (valueIndex > 1) return false;
                decoded.AutoReplantTrees = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.VehicleBlockRespawn)
            {
                if (valueIndex > 4) return false;
                decoded.VehicleBlockRespawnDays = valueIndex == 1 ? 3 : valueIndex == 2 ? 7 : valueIndex == 3 ? 14 : valueIndex == 4 ? 21 : 0;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.HybridPathSmoothing)
            {
                if (valueIndex > 1) return false;
                decoded.HybridPathSmoothing = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.ZombiesDestroyAreas)
            {
                if (valueIndex > 1) return false;
                decoded.ZombiesDestroyAreas = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.RancherRangedAttack)
            {
                if (valueIndex > 1) return false;
                decoded.RancherRangedAttack = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.SleeperRespawns)
            {
                if (valueIndex > 1) return false;
                decoded.SleeperRespawns = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.LootTraderAreas)
            {
                if (valueIndex > 1) return false;
                decoded.LootTraderAreas = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.SleeperSpawnMultiplier)
            {
                if (valueIndex > 3) return false;
                decoded.SleeperSpawnMultiplier = valueIndex + 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.InfestedSleeperSpawnMultiplier)
            {
                if (valueIndex > 4) return false;
                decoded.InfestedSleeperSpawnMultiplier = valueIndex + 2;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.SuppressConsoleErrorPopups)
            {
                if (valueIndex > 1) return false;
                decoded.SuppressConsoleErrorPopups = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.TreeDensityMultiplier)
            {
                if (valueIndex >= RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers.Length) return false;
                decoded.TreeDensityMultiplier = RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers[valueIndex];
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.VehicleDensityMultiplier)
            {
                if (valueIndex >= RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers.Length) return false;
                decoded.VehicleDensityMultiplier = RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers[valueIndex];
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.BlocksCatchFire)
            {
                if (valueIndex > 1) return false;
                decoded.BlocksCatchFire = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.FireAffectsHeatmap)
            {
                if (valueIndex > 1) return false;
                decoded.FireAffectsHeatmap = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.FireBlockDamageSpeed)
            {
                if (valueIndex > (int)RebirthFireBlockDamageSpeed.Faster) return false;
                decoded.FireBlockDamageSpeed = (RebirthFireBlockDamageSpeed)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.MaxJobs)
            {
                int decodedMaxJobs;
                if (!RebirthTraderJobPolicy.TryMaxJobsFromPersistedIndex(valueIndex, out decodedMaxJobs)) return false;
                decoded.MaxJobs = decodedMaxJobs;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.JobsToNextTier)
            {
                int decodedJobsToNextTier;
                if (!RebirthTraderJobPolicy.TryJobsToNextTierFromIndex(valueIndex, out decodedJobsToNextTier)) return false;
                decoded.JobsToNextTier = decodedJobsToNextTier;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.RepeatPoiJobs)
            {
                if (legacyBooleanRepeatPoi)
                {
                    if (valueIndex > 1) return false;
                    decoded.RepeatPoiJobs = valueIndex == 1 ? RebirthRepeatPoiPolicy.Unlimited : RebirthRepeatPoiPolicy.None;
                }
                else
                {
                    if (valueIndex > (int)RebirthRepeatPoiPolicy.Unlimited) return false;
                    decoded.RepeatPoiJobs = (RebirthRepeatPoiPolicy)valueIndex;
                }
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.InfestedJobs)
            {
                if (valueIndex > (int)RebirthInfestedJobsMode.Surprise) return false;
                decoded.InfestedJobs = (RebirthInfestedJobsMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.TraderJobList)
            {
                if (valueIndex > (int)RebirthTraderJobListMode.Random) return false;
                decoded.TraderJobList = (RebirthTraderJobListMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.TraderJobRecoveryGrace)
            {
                if (valueIndex > 1) return false;
                decoded.TraderJobRecoveryGrace = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.TraderJobRecoveryGraceDuration)
            {
                if (valueIndex > (int)RebirthTraderJobRecoveryGraceDuration.FifteenMinutes)
                    return false;
                decoded.TraderJobRecoveryGraceDuration =
                    (RebirthTraderJobRecoveryGraceDuration)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.PreventHealingOverlap)
            {
                if (sourceVersion >= PreviousVersion00000000000) // U and later
                {
                    if (valueIndex > (int)RebirthHealingOverlapThreshold.Health30) return false;
                    decoded.PreventHealingOverlap = (RebirthHealingOverlapThreshold)valueIndex;
                }
                else
                {
                    // U introduced the four-state threshold selector. V preserves it; T-and-earlier
                    // encoded this option as a boolean: Off=0, On=1. Preserve On as
                    // the original 2.6-compatible 10-health threshold.
                    if (valueIndex > 1) return false;
                    decoded.PreventHealingOverlap = valueIndex == 1
                        ? RebirthHealingOverlapThreshold.Health10
                        : RebirthHealingOverlapThreshold.Off;
                }
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.AlwaysStagger)
            {
                if (valueIndex > (int)RebirthAlwaysStaggerMode.AllQualifyingHits) return false;
                decoded.AlwaysStagger = (RebirthAlwaysStaggerMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.WeatherFogBehavior)
            {
                if (valueIndex > (int)RebirthWeatherFogBehavior.Disabled) return false;
                decoded.WeatherFogBehavior = (RebirthWeatherFogBehavior)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.WeatherFogIntensity)
            {
                RebirthWeatherFogIntensity decodedFogIntensity;
                if (!TryWeatherFogIntensityFromPersistedIndex(valueIndex, out decodedFogIntensity)) return false;
                decoded.WeatherFogIntensity = decodedFogIntensity;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.UniformAtmosphere)
            {
                if (valueIndex > 1) return false;
                decoded.UniformAtmosphere = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.PoiRisk)
            {
                if (valueIndex > (int)RebirthPoiRiskMode.High) return false;
                decoded.PoiRisk = (RebirthPoiRiskMode)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.PoiSenseSchedule)
            {
                if (valueIndex > (int)RebirthPoiSenseSchedule.Always) return false;
                decoded.PoiSenseSchedule = (RebirthPoiSenseSchedule)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.PoiSenseIntensity)
            {
                if (valueIndex > (int)RebirthPoiSenseIntensity.High) return false;
                decoded.PoiSenseIntensity = (RebirthPoiSenseIntensity)valueIndex;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.PitchBlack)
            {
                if (valueIndex > 1) return false;
                decoded.PitchBlack = valueIndex == 1;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEvents)
            {
                if (sourceVersion >= PreviousVersion0000000000) // T and later
                {
                    if (valueIndex > (int)RebirthBossEventStartLevel.PlayerLevel30) return false;
                    decoded.BossEventStartLevel = (RebirthBossEventStartLevel)valueIndex;
                }
                else
                {
                    // Pre-v189 codes stored this option as a boolean: 0=Off, 1=On.
                    // Preserve old On as the historical level-15 start threshold.
                    if (valueIndex > 1) return false;
                    decoded.BossEventStartLevel = valueIndex == 1
                        ? RebirthBossEventStartLevel.PlayerLevel15
                        : RebirthBossEventStartLevel.Off;
                }
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventFrequency) { if (valueIndex > (int)RebirthBossEventFrequency.VeryHigh) return false; decoded.BossEventFrequency = (RebirthBossEventFrequency)valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventMaximumPerDay) { if (valueIndex > 5) return false; decoded.BossEventMaximumPerDay = valueIndex == 5 ? 0 : valueIndex + 1; }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventSize) { if (valueIndex > (int)RebirthBossEventSize.Random) return false; decoded.BossEventSize = (RebirthBossEventSize)valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventDifficulty) { if (valueIndex > (int)RebirthBossEventDifficulty.Random) return false; decoded.BossEventDifficulty = (RebirthBossEventDifficulty)valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventTime) { if (valueIndex > (int)RebirthBossEventTime.NightOnly) return false; decoded.BossEventTime = (RebirthBossEventTime)valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventBloodMoonDay) { if (valueIndex > 1) return false; decoded.BossEventBloodMoonDay = valueIndex == 1; }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventRestriction) { if (valueIndex > (int)RebirthBossEventRestriction.OutdoorsAndNotQuesting) return false; decoded.BossEventRestriction = (RebirthBossEventRestriction)valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventRewards) { if (valueIndex > 1) return false; decoded.BossEventRewards = valueIndex == 1; }
            else if (optionIndex == (int)RebirthSandboxOptionId.BossEventNotifications) { if (valueIndex > 1) return false; decoded.BossEventNotifications = valueIndex == 1; }
            else if (optionIndex == (int)RebirthSandboxOptionId.ProtectCrate) { if (valueIndex > 1) return false; decoded.ProtectCrate = valueIndex == 1; }
            else if (optionIndex == (int)RebirthSandboxOptionId.ReservedCompanionInventory)
            {
                // v244 briefly exposed Companion Inventory as values 0..2. The option has
                // been removed, but keep accepting its old code slot so previously copied
                // sandbox codes still decode. The retired value is intentionally ignored.
                if (valueIndex > 2) return false;
            }
            else if (optionIndex == (int)RebirthSandboxOptionId.GuardDistance)
            { int distance; if (!RebirthCompanionDistancePolicy.TryDecodeGuard(valueIndex, out distance)) return false; decoded.GuardDistance = distance; }
            else if (optionIndex == (int)RebirthSandboxOptionId.FullControlDistance)
            { int distance; if (!RebirthCompanionDistancePolicy.TryDecodeFullControl(valueIndex, out distance)) return false; decoded.FullControlDistance = distance; }
            else if (optionIndex == (int)RebirthSandboxOptionId.HuntingDistance)
            { int distance; if (!RebirthCompanionDistancePolicy.TryDecodeHunting(valueIndex, out distance)) return false; decoded.HuntingDistance = distance; }
            else if (optionIndex == (int)RebirthSandboxOptionId.CompanionCardStyle)
            { if (valueIndex > (int)RebirthCompanionCardStyle.Simple) return false; decoded.CompanionCardStyle = (RebirthCompanionCardStyle)valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.TargetNameColorRHigh) { if (valueIndex > 9) return false; targetNameRHigh = valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.TargetNameColorRLow) { targetNameRLow = valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.TargetNameColorGHigh) { if (valueIndex > 9) return false; targetNameGHigh = valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.TargetNameColorGLow) { targetNameGLow = valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.TargetNameColorBHigh) { if (valueIndex > 9) return false; targetNameBHigh = valueIndex; }
            else if (optionIndex == (int)RebirthSandboxOptionId.TargetNameColorBLow) { targetNameBLow = valueIndex; }
            // Unknown future option IDs are ignored so an older client can reject only
            // structurally invalid data while preserving known values.
        }

        int targetNameR = targetNameRHigh * 26 + targetNameRLow;
        int targetNameG = targetNameGHigh * 26 + targetNameGLow;
        int targetNameB = targetNameBHigh * 26 + targetNameBLow;
        if (targetNameR > 255 || targetNameG > 255 || targetNameB > 255)
            return false;
        decoded.TargetNameColorR = targetNameR;
        decoded.TargetNameColorG = targetNameG;
        decoded.TargetNameColorB = targetNameB;

        // Keep configured dependent values intact. Runtime and UI use the dependency
        // policy to derive an effective value without destructively normalizing state.
        state = decoded;
        return true;
    }

    public static string GetGroupDisplayName(string group)
    {
        switch (group)
        {
            case RecommendedGroupName: return Localization.Get("xuiRebirthPresetGroupRecommended");
            case UserGroupName: return Localization.Get("xuiRebirthPresetGroupUser");
            default: return group ?? string.Empty;
        }
    }

    public RebirthSandboxPreset SaveUserPreset(string name, string description, string code)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0 || GetPreset(name) != null)
            return null;

        RebirthSandboxState ignored;
        if (!TryDecode(code, out ignored))
            return null;

        string directory = GetUserPresetDirectory();
        Directory.CreateDirectory(directory);
        string safeName = MakeSafeFileName(name);
        string path = Path.Combine(directory, safeName + "_" + StablePresetNameSuffix(name) + ".xml");
        if (File.Exists(path)) return null;

        XmlDocument document = new XmlDocument();
        XmlElement root = document.CreateElement("rebirthPreset");
        root.SetAttribute("name", name);
        root.SetAttribute("description", description ?? string.Empty);
        root.SetAttribute("code", code);
        document.AppendChild(root);
        SaveXmlAtomic(document, path);

        ReloadPresets();
        return GetPreset(name);
    }

    public RebirthSandboxPreset UpdateUserPreset(string name, string description, string code)
    {
        RebirthSandboxPreset preset = GetPreset(name);
        if (preset == null || !preset.IsUserPreset)
            return null;

        RebirthSandboxState ignored;
        if (!TryDecode(code, out ignored))
            return null;

        string directory = GetUserPresetDirectory();
        string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.xml") : new string[0];
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(files[i]);
                XmlElement root = document.DocumentElement;
                if (root == null || !string.Equals(root.GetAttribute("name"), name, StringComparison.OrdinalIgnoreCase))
                    continue;

                root.SetAttribute("description", description ?? string.Empty);
                root.SetAttribute("code", code);
                SaveXmlAtomic(document, files[i]);
                ReloadPresets();
                return GetPreset(name);
            }
            catch
            {
                // A malformed unrelated sibling must not prevent locating/updating the named preset.
                continue;
            }
        }

        return null;
    }

    public bool DeleteUserPreset(string name)
    {
        RebirthSandboxPreset preset = GetPreset(name);
        if (preset == null || !preset.IsUserPreset)
            return false;

        string directory = GetUserPresetDirectory();
        string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.xml") : new string[0];
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(files[i]);
                XmlElement root = document.DocumentElement;
                if (root != null && string.Equals(root.GetAttribute("name"), name, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(files[i]);
                    ReloadPresets();
                    return true;
                }
            }
            catch
            {
                // Ignore malformed unrelated user preset files.
            }
        }
        return false;
    }

    private void LoadUserPresets()
    {
        string directory;
        try
        {
            directory = GetUserPresetDirectory();
        }
        catch
        {
            return;
        }

        if (!Directory.Exists(directory))
            return;

        string[] files = Directory.GetFiles(directory, "*.xml");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(files[i]);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "rebirthPreset")
                    continue;

                string name = root.GetAttribute("name");
                string code = root.GetAttribute("code");
                RebirthSandboxState ignored;
                if (string.IsNullOrWhiteSpace(name) || !TryDecode(code, out ignored))
                    continue;

                presets.Add(new RebirthSandboxPreset
                {
                    Name = name,
                    Description = root.GetAttribute("description"),
                    Group = UserGroupName,
                    IsUserPreset = true,
                    Code = code
                });
            }
            catch
            {
                // One malformed user preset must not prevent other presets from loading.
            }
        }
    }

    private static string GetUserPresetDirectory()
    {
        return Path.Combine(GameIO.GetUserGameDataDir(), "Presets", "Rebirth");
    }

    private static string MakeSafeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            for (int j = 0; j < invalid.Length; j++)
            {
                if (chars[i] == invalid[j])
                {
                    chars[i] = '_';
                    break;
                }
            }
        }
        return new string(chars);
    }

    private static string StablePresetNameSuffix(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            for (int i = 0; i < normalized.Length; i++) { hash ^= normalized[i]; hash *= 16777619u; }
            return hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static void SaveXmlAtomic(XmlDocument document, string path)
    {
        string temporary = path + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            document.Save(stream);
            stream.Flush();
        }
        XmlDocument verify = new XmlDocument();
        verify.Load(temporary);
        if (verify.DocumentElement == null || verify.DocumentElement.Name != "rebirthPreset")
            throw new InvalidDataException("staged preset verification failed");
        string error;
        if (!RebirthDurableFileCommit.TryPublish(temporary, path, out error))
            throw new IOException("durable preset publication failed: " + error);
    }


    // Preserve the v68 persisted indices for existing worlds while allowing None
    // to appear first in the UI and enum ordering. Old codes 0..4 continue to mean
    // Very Low..Very Heavy; the new None value is stored as index 5.
    private static int WeatherFogIntensityToPersistedIndex(RebirthWeatherFogIntensity value)
    {
        switch (value)
        {
            case RebirthWeatherFogIntensity.VeryLow: return 0;
            case RebirthWeatherFogIntensity.Low: return 1;
            case RebirthWeatherFogIntensity.Normal: return 2;
            case RebirthWeatherFogIntensity.Heavy: return 3;
            case RebirthWeatherFogIntensity.VeryHeavy: return 4;
            case RebirthWeatherFogIntensity.None: return 5;
            default: return 2;
        }
    }

    private static bool TryWeatherFogIntensityFromPersistedIndex(
        int index,
        out RebirthWeatherFogIntensity value)
    {
        switch (index)
        {
            case 0: value = RebirthWeatherFogIntensity.VeryLow; return true;
            case 1: value = RebirthWeatherFogIntensity.Low; return true;
            case 2: value = RebirthWeatherFogIntensity.Normal; return true;
            case 3: value = RebirthWeatherFogIntensity.Heavy; return true;
            case 4: value = RebirthWeatherFogIntensity.VeryHeavy; return true;
            case 5: value = RebirthWeatherFogIntensity.None; return true;
            default:
                value = RebirthWeatherFogIntensity.Normal;
                return false;
        }
    }

    private static char IndexToAlpha(int index)
    {
        if (index < 0 || index > 25)
            throw new ArgumentOutOfRangeException("index");
        return (char)('A' + index);
    }

    private static string IndexToAlpha2(int index)
    {
        if (index < 0 || index > 675)
            throw new ArgumentOutOfRangeException("index");
        return new string(new[] { IndexToAlpha(index / 26), IndexToAlpha(index % 26) });
    }

    private static int AlphaToIndex(char value)
    {
        if (value < 'A' || value > 'Z')
            return -1;
        return value - 'A';
    }

    private static int Alpha2ToIndex(char first, char second)
    {
        int high = AlphaToIndex(first);
        int low = AlphaToIndex(second);
        if (high < 0 || low < 0)
            return -1;
        return high * 26 + low;
    }
}
