using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public partial class XUiC_RebirthSandboxOptions : XUiController
{
    private const string LockedSprite = "ui_game_symbol_lock";
    private const string UnlockedSprite = "ui_game_symbol_unlock";
    private static readonly System.Random Randomizer = new System.Random();

    [XuiBindComponent("cbxRebirthEditorPresetGroup", true)]
    public readonly XUiC_ComboBoxList<string> cbxPresetGroup;

    [XuiBindComponent("cbxRebirthEditorPreset", true)]
    public readonly XUiC_ComboBoxList<string> cbxPreset;

    [XuiBindComponent("cbxPlayerProgression", true)]
    public readonly XUiC_ComboBoxList<string> cbxPlayerProgression;

    [XuiBindComponent("cbxScrollbarMode", false)] public readonly XUiC_ComboBoxList<string> cbxScrollbarMode;
    [XuiBindComponent("cbxRequireTimedReading", true)] public readonly XUiC_ComboBoxList<string> cbxRequireTimedReading;
    [XuiBindComponent("cbxLiteratureStudyTime", true)] public readonly XUiC_ComboBoxList<string> cbxLiteratureStudyTime;

    [XuiBindComponent("cbxSpawnProgression", true)]
    public readonly XUiC_ComboBoxList<string> cbxSpawnProgression;

    [XuiBindComponent("btnSpawnProgressionRandomizerLock", true)]
    public readonly XUiC_Button btnSpawnProgressionRandomizerLock;

    [XuiBindComponent("cbxAdvancedFarming", true)]
    public readonly XUiC_ComboBoxList<string> cbxAdvancedFarming;

    [XuiBindComponent("cbxGuardDistance", true)] public readonly XUiC_ComboBoxList<string> cbxGuardDistance;
    [XuiBindComponent("cbxFullControlDistance", true)] public readonly XUiC_ComboBoxList<string> cbxFullControlDistance;
    [XuiBindComponent("cbxHuntingDistance", true)] public readonly XUiC_ComboBoxList<string> cbxHuntingDistance;
    [XuiBindComponent("cbxCompanionCardStyle", true)] public readonly XUiC_ComboBoxList<string> cbxCompanionCardStyle;
    [XuiBindComponent("btnTargetNameColor", true)] public readonly XUiC_Button btnTargetNameColor;

    [XuiBindComponent("btnAdvancedFarmingRandomizerLock", true)]
    public readonly XUiC_Button btnAdvancedFarmingRandomizerLock;

    [XuiBindComponent("cbxAutoReplantTrees", true)]
    public readonly XUiC_ComboBoxList<string> cbxAutoReplantTrees;

    [XuiBindComponent("cbxVehicleBlockRespawn", true)]
    public readonly XUiC_ComboBoxList<string> cbxVehicleBlockRespawn;

    [XuiBindComponent("cbxHybridPathSmoothing", true)]
    public readonly XUiC_ComboBoxList<string> cbxHybridPathSmoothing;

    [XuiBindComponent("cbxZombiesDestroyAreas", true)]
    public readonly XUiC_ComboBoxList<string> cbxZombiesDestroyAreas;

    [XuiBindComponent("cbxRancherRangedAttack", true)]
    public readonly XUiC_ComboBoxList<string> cbxRancherRangedAttack;

    [XuiBindComponent("cbxSleeperRespawns", true)]
    public readonly XUiC_ComboBoxList<string> cbxSleeperRespawns;

    [XuiBindComponent("cbxSleeperSpawnMultiplier", true)]
    public readonly XUiC_ComboBoxList<string> cbxSleeperSpawnMultiplier;

    [XuiBindComponent("cbxInfestedSleeperSpawnMultiplier", true)]
    public readonly XUiC_ComboBoxList<string> cbxInfestedSleeperSpawnMultiplier;

    [XuiBindComponent("cbxSuppressConsoleErrorPopups", true)]
    public readonly XUiC_ComboBoxList<string> cbxSuppressConsoleErrorPopups;

    [XuiBindComponent("cbxTreeDensityMultiplier", true)]
    public readonly XUiC_ComboBoxList<string> cbxTreeDensityMultiplier;

    [XuiBindComponent("cbxVehicleDensityMultiplier", true)]
    public readonly XUiC_ComboBoxList<string> cbxVehicleDensityMultiplier;

    [XuiBindComponent("cbxBlocksCatchFire", true)]
    public readonly XUiC_ComboBoxList<string> cbxBlocksCatchFire;

    [XuiBindComponent("cbxFireAffectsHeatmap", true)]
    public readonly XUiC_ComboBoxList<string> cbxFireAffectsHeatmap;

    [XuiBindComponent("cbxFireBlockDamageSpeed", true)]
    public readonly XUiC_ComboBoxList<string> cbxFireBlockDamageSpeed;

    [XuiBindComponent("cbxMaxJobs", true)]
    public readonly XUiC_ComboBoxList<string> cbxMaxJobs;

    [XuiBindComponent("cbxJobsToNextTier", true)]
    public readonly XUiC_ComboBoxList<string> cbxJobsToNextTier;

    [XuiBindComponent("cbxRepeatPoiJobs", true)]
    public readonly XUiC_ComboBoxList<string> cbxRepeatPoiJobs;

    [XuiBindComponent("cbxInfestedJobs", true)]
    public readonly XUiC_ComboBoxList<string> cbxInfestedJobs;

    [XuiBindComponent("cbxTraderJobList", true)]
    public readonly XUiC_ComboBoxList<string> cbxTraderJobList;

    [XuiBindComponent("cbxTraderJobRecoveryGrace", true)]
    public readonly XUiC_ComboBoxList<string> cbxTraderJobRecoveryGrace;

    [XuiBindComponent("cbxTraderJobRecoveryGraceDuration", true)]
    public readonly XUiC_ComboBoxList<string> cbxTraderJobRecoveryGraceDuration;

    [XuiBindComponent("cbxPreventHealingOverlap", true)]
    public readonly XUiC_ComboBoxList<string> cbxPreventHealingOverlap;

    [XuiBindComponent("cbxAlwaysStagger", true)]
    public readonly XUiC_ComboBoxList<string> cbxAlwaysStagger;

    [XuiBindComponent("cbxWeatherFogBehavior", true)]
    public readonly XUiC_ComboBoxList<string> cbxWeatherFogBehavior;

    [XuiBindComponent("cbxWeatherFogIntensity", true)]
    public readonly XUiC_ComboBoxList<string> cbxWeatherFogIntensity;

    [XuiBindComponent("cbxUniformAtmosphere", true)]
    public readonly XUiC_ComboBoxList<string> cbxUniformAtmosphere;

    [XuiBindComponent("cbxPitchBlack", true)]
    public readonly XUiC_ComboBoxList<string> cbxPitchBlack;

    [XuiBindComponent("cbxPoiRisk", true)]
    public readonly XUiC_ComboBoxList<string> cbxPoiRisk;

    [XuiBindComponent("cbxPoiSenseSchedule", true)]
    public readonly XUiC_ComboBoxList<string> cbxPoiSenseSchedule;

    [XuiBindComponent("cbxPoiSenseIntensity", true)]
    public readonly XUiC_ComboBoxList<string> cbxPoiSenseIntensity;

    [XuiBindComponent("cbxLootTraderAreas", true)]
    public readonly XUiC_ComboBoxList<string> cbxLootTraderAreas;

    [XuiBindComponent("cbxInstantBlockPickup", true)]
    public readonly XUiC_ComboBoxList<string> cbxInstantBlockPickup;

    [XuiBindComponent("btnInstantBlockPickupRandomizerLock", true)]
    public readonly XUiC_Button btnInstantBlockPickupRandomizerLock;

    [XuiBindComponent("cbxRemoteResources", true)]
    public readonly XUiC_ComboBoxList<string> cbxRemoteResources;

    [XuiBindComponent("btnRemoteResourcesRandomizerLock", true)]
    public readonly XUiC_Button btnRemoteResourcesRandomizerLock;

    [XuiBindComponent("cbxRemoteResourcesDistance", true)]
    public readonly XUiC_ComboBoxList<string> cbxRemoteResourcesDistance;

    [XuiBindComponent("btnRemoteResourcesDistanceRandomizerLock", true)]
    public readonly XUiC_Button btnRemoteResourcesDistanceRandomizerLock;

    [XuiBindComponent("cbxSecureAccessSharing", true)]
    public readonly XUiC_ComboBoxList<string> cbxSecureAccessSharing;

    [XuiBindComponent("cbxHornActivatedDoors", true)]
    public readonly XUiC_ComboBoxList<string> cbxHornActivatedDoors;

    [XuiBindComponent("cbxQuickStack", true)]
    public readonly XUiC_ComboBoxList<string> cbxQuickStack;

    [XuiBindComponent("btnQuickStackRandomizerLock", true)]
    public readonly XUiC_Button btnQuickStackRandomizerLock;

    [XuiBindComponent("cbxQuickStackDistance", true)]
    public readonly XUiC_ComboBoxList<string> cbxQuickStackDistance;

    [XuiBindComponent("btnQuickStackDistanceRandomizerLock", true)]
    public readonly XUiC_Button btnQuickStackDistanceRandomizerLock;


    [XuiBindComponent("cbxProtectCrate", true)] public readonly XUiC_ComboBoxList<string> cbxProtectCrate;
    [XuiBindComponent("cbxBossEvents", true)] public readonly XUiC_ComboBoxList<string> cbxBossEvents;
    [XuiBindComponent("cbxBossEventFrequency", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventFrequency;
    [XuiBindComponent("cbxBossEventMaximumPerDay", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventMaximumPerDay;
    [XuiBindComponent("cbxBossEventSize", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventSize;
    [XuiBindComponent("cbxBossEventDifficulty", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventDifficulty;
    [XuiBindComponent("cbxBossEventTime", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventTime;
    [XuiBindComponent("cbxBossEventBloodMoonDay", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventBloodMoonDay;
    [XuiBindComponent("cbxBossEventRestriction", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventRestriction;
    [XuiBindComponent("cbxBossEventRewards", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventRewards;
    [XuiBindComponent("cbxBossEventNotifications", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventNotifications;

    [XuiBindComponent("btnBack", true)]
    public readonly XUiC_Button btnBack;

    [XuiBindComponent("btnDefaults", true)]
    public readonly XUiC_Button btnDefaults;

    [XuiBindComponent("btnSave", true)]
    public readonly XUiC_Button btnSave;

    [XuiBindComponent("btnCopyCode", true)]
    public readonly XUiC_Button btnCopyCode;

    [XuiBindComponent("btnPasteCode", true)]
    public readonly XUiC_Button btnPasteCode;

    [XuiBindComponent("btnRandomize", true)]
    public readonly XUiC_Button btnRandomize;

    [XuiBindComponent("btnCreatePreset", true)]
    public readonly XUiC_Button btnCreatePreset;

    [XuiBindComponent("btnCopy", true)]
    public readonly XUiC_Button btnCopy;

    [XuiBindComponent("btnDelete", true)]
    public readonly XUiC_Button btnDelete;

    [XuiBindComponent(false)]
    public readonly XUiC_TabSelector tabSelector;

    private readonly List<string> groupIds = new List<string>();
    private readonly List<RebirthSandboxPreset> visiblePresets = new List<RebirthSandboxPreset>();
    private bool suppressEvents;
    private bool comboEventsBound;
    private RebirthPlayerProgressionMode workingPlayerProgression = RebirthPlayerProgressionMode.Rebirth;
    private RebirthSpawnProgressionMode workingSpawnProgression = RebirthSpawnProgressionMode.Gamestage;
    private bool workingAdvancedFarming = true;
    private int workingGuardDistance = RebirthCompanionDistancePolicy.GuardDefault;
    private int workingFullControlDistance = RebirthCompanionDistancePolicy.FullControlDefault;
    private int workingHuntingDistance = RebirthCompanionDistancePolicy.HuntingDefault;
    private RebirthCompanionCardStyle workingCompanionCardStyle = RebirthCompanionCardStyle.Simple;
    private RebirthScrollbarMode workingScrollbarMode = RebirthScrollbarMode.Smooth;
    private bool workingRequireTimedReading = true;
    private RebirthLiteratureStudyTime workingLiteratureStudyTime = RebirthLiteratureStudyTimePolicy.Default;
    private int workingTargetNameColorR = RebirthTargetNameColorPolicy.DefaultR;
    private int workingTargetNameColorG = RebirthTargetNameColorPolicy.DefaultG;
    private int workingTargetNameColorB = RebirthTargetNameColorPolicy.DefaultB;
    private bool workingAutoReplantTrees;
    private int workingVehicleBlockRespawnDays;
    private bool workingHybridPathSmoothing = true;
    private bool workingZombiesDestroyAreas;
    private bool workingRancherRangedAttack;
    private bool workingSleeperRespawns;
    private int workingSleeperSpawnMultiplier = 1;
    private int workingInfestedSleeperSpawnMultiplier = 2;
    private bool workingSuppressConsoleErrorPopups;
    private int workingTreeDensityMultiplier = 100;
    private int workingVehicleDensityMultiplier = 100;
    private bool workingBlocksCatchFire = false;
    private bool workingFireAffectsHeatmap = false;
    private RebirthFireBlockDamageSpeed workingFireBlockDamageSpeed = RebirthFireBlockDamageSpeed.Default;
    private int workingMaxJobs = 11;
    private int workingJobsToNextTier = RebirthTraderJobPolicy.DefaultJobsToNextTier;
    private RebirthRepeatPoiPolicy workingRepeatPoiJobs = RebirthRepeatPoiPolicy.Medium;
    private RebirthInfestedJobsMode workingInfestedJobs = RebirthInfestedJobsMode.Default;
    private RebirthTraderJobListMode workingTraderJobList = RebirthTraderJobListMode.Fixed;
    private bool workingTraderJobRecoveryGrace = true;
    private RebirthTraderJobRecoveryGraceDuration workingTraderJobRecoveryGraceDuration =
        RebirthTraderJobRecoveryGraceDuration.FiveMinutes;
    private RebirthHealingOverlapThreshold workingPreventHealingOverlap = RebirthHealingOverlapThreshold.Health10;
    private RebirthAlwaysStaggerMode workingAlwaysStagger = RebirthAlwaysStaggerMode.AllQualifyingHits;
    private RebirthWeatherFogBehavior workingWeatherFogBehavior = RebirthWeatherFogBehavior.Dynamic;
    private RebirthWeatherFogIntensity workingWeatherFogIntensity = RebirthWeatherFogIntensity.Normal;
    private bool workingUniformAtmosphere = true;
    private bool workingPitchBlack = true;
    private RebirthPoiRiskMode workingPoiRisk = RebirthPoiRiskMode.Medium;
    private RebirthPoiSenseSchedule workingPoiSenseSchedule = RebirthPoiSenseSchedule.Never;
    private RebirthPoiSenseIntensity workingPoiSenseIntensity = RebirthPoiSenseIntensity.Medium;
    private bool workingLootTraderAreas = true;
    private RebirthInstantBlockPickupMode workingInstantBlockPickup = RebirthInstantBlockPickupMode.Default;
    private RebirthQuickStackMode workingQuickStack = RebirthQuickStackMode.Full;
    private int workingQuickStackDistance = RebirthResourceDistancePolicy.QuickStackDefault;
    private bool workingRemoteResources = true;
    private int workingRemoteResourcesDistance = RebirthResourceDistancePolicy.RemoteResourcesDefault;
    private RebirthSecureAccessMode workingSecureAccessSharing = RebirthSecureAccessMode.PinAccessList;
    private RebirthHornActivatedDoorsMode workingHornActivatedDoors = RebirthHornActivatedDoorsMode.All;

    private bool workingProtectCrate = true;
    private RebirthBossEventStartLevel workingBossEventStartLevel = RebirthBossEventStartLevel.PlayerLevel15;
    private RebirthBossEventFrequency workingBossEventFrequency = RebirthBossEventFrequency.Normal;
    private int workingBossEventMaximumPerDay = 2;
    private RebirthBossEventSize workingBossEventSize = RebirthBossEventSize.Normal;
    private RebirthBossEventDifficulty workingBossEventDifficulty = RebirthBossEventDifficulty.Normal;
    private RebirthBossEventTime workingBossEventTime = RebirthBossEventTime.DayAndNight;
    private bool workingBossEventBloodMoonDay = true;
    private RebirthBossEventRestriction workingBossEventRestriction = RebirthBossEventRestriction.Anywhere;
    private bool workingBossEventRewards = true;
    private bool workingBossEventNotifications = true;
    private bool workingAsNewPreset;
    private string workingPresetName = RebirthSandboxOptionManager.DefaultPresetName;
    private string selectedPresetBaselineCode = RebirthSandboxOptionManager.Encode(new RebirthSandboxState());
    private string sessionRollbackPresetName = RebirthSandboxOptionManager.DefaultPresetName;
    private string sessionRollbackCode = RebirthSandboxOptionManager.Encode(new RebirthSandboxState());
    private Action onClose;
    private string editContextIdentity = string.Empty;
    private int workingProjectionRevision;
    private int cachedWorkingCodeRevision = -1;
    private string cachedWorkingCode = string.Empty;
    private string cachedBaselineCode = null;
    private RebirthSandboxState cachedBaselineState = null;

    private float playerProgressionDebugPollTimer;
    private int playerProgressionDebugLastIndex = int.MinValue;
    private int playerProgressionDebugLastElementCount = int.MinValue;
    private bool playerProgressionDebugLastEnabled;
    private bool playerProgressionDebugLastLocked;
    private RebirthPlayerProgressionMode playerProgressionDebugLastWorking = (RebirthPlayerProgressionMode)(-1);
    private string playerProgressionDebugLastContext = null;

    [XuiXmlBinding("rebirth_code")]
    public string RebirthCode
    {
        get
        {
            if (cachedWorkingCodeRevision != workingProjectionRevision || string.IsNullOrEmpty(cachedWorkingCode))
            {
                cachedWorkingCode = RebirthSandboxOptionManager.Encode(BuildWorkingState());
                cachedWorkingCodeRevision = workingProjectionRevision;
            }
            return cachedWorkingCode;
        }
    }

    [XuiXmlBinding("rebirth_is_user_preset")]
    public bool IsUserPreset
    {
        get
        {
            RebirthSandboxPreset preset = RebirthSandboxOptionManager.Current.GetPreset(workingPresetName);
            return preset != null && preset.IsUserPreset;
        }
    }

    [XuiXmlBinding("rebirth_is_draft")]
    public bool IsDraftPreset
    {
        get { return workingAsNewPreset; }
    }

    [XuiXmlBinding("rebirth_has_changes")]
    public bool HasChanges
    {
        get { return !AreCodesSemanticallyEquivalent(RebirthCode, selectedPresetBaselineCode); }
    }

    [XuiXmlBinding("rebirth_player_progression_default")]
    public bool PlayerProgressionIsDefault { get { return workingPlayerProgression == RebirthPlayerProgressionMode.Rebirth; } }

    [XuiXmlBinding("rebirth_player_progression_changed")]
    public bool PlayerProgressionChanged { get { return GetBaselineState().PlayerProgression != workingPlayerProgression; } }

    [XuiXmlBinding("rebirth_spawn_progression_default")]
    public bool SpawnProgressionIsDefault { get { return workingSpawnProgression == RebirthSpawnProgressionMode.Gamestage; } }

    [XuiXmlBinding("rebirth_spawn_progression_changed")]
    public bool SpawnProgressionChanged { get { return GetBaselineState().SpawnProgression != workingSpawnProgression; } }

    [XuiXmlBinding("rebirth_spawn_progression_randomizer_locked")]
    public bool SpawnProgressionRandomizerLocked
    {
        get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.SpawnProgression) != RebirthSandboxOptionRandomizerMode.Unlocked; }
    }

    [XuiXmlBinding("rebirth_spawn_progression_randomizer_lock_sprite")]
    public string SpawnProgressionRandomizerLockSprite { get { return SpawnProgressionRandomizerLocked ? LockedSprite : UnlockedSprite; } }

    [XuiXmlBinding("rebirth_spawn_progression_randomizer_lock_is_minimum")]
    public bool SpawnProgressionRandomizerLockIsMinimum
    {
        get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.SpawnProgression) == RebirthSandboxOptionRandomizerMode.Minimum; }
    }

    [XuiXmlBinding("rebirth_spawn_progression_randomizer_lock_is_maximum")]
    public bool SpawnProgressionRandomizerLockIsMaximum
    {
        get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.SpawnProgression) == RebirthSandboxOptionRandomizerMode.Maximum; }
    }

    [XuiXmlBinding("rebirth_advanced_farming_default")]
    public bool AdvancedFarmingIsDefault { get { return workingAdvancedFarming; } }

    [XuiXmlBinding("rebirth_advanced_farming_changed")]
    public bool AdvancedFarmingChanged { get { return GetBaselineState().AdvancedFarming != workingAdvancedFarming; } }

    [XuiXmlBinding("rebirth_guard_distance_default")] public bool GuardDistanceIsDefault { get { return workingGuardDistance == RebirthCompanionDistancePolicy.GuardDefault; } }
    [XuiXmlBinding("rebirth_guard_distance_changed")] public bool GuardDistanceChanged { get { return GetBaselineState().GuardDistance != workingGuardDistance; } }
    [XuiXmlBinding("rebirth_full_control_distance_default")] public bool FullControlDistanceIsDefault { get { return workingFullControlDistance == RebirthCompanionDistancePolicy.FullControlDefault; } }
    [XuiXmlBinding("rebirth_full_control_distance_changed")] public bool FullControlDistanceChanged { get { return GetBaselineState().FullControlDistance != workingFullControlDistance; } }
    [XuiXmlBinding("rebirth_hunting_distance_default")] public bool HuntingDistanceIsDefault { get { return workingHuntingDistance == RebirthCompanionDistancePolicy.HuntingDefault; } }
    [XuiXmlBinding("rebirth_hunting_distance_changed")] public bool HuntingDistanceChanged { get { return GetBaselineState().HuntingDistance != workingHuntingDistance; } }
    [XuiXmlBinding("rebirth_companion_card_style_default")] public bool CompanionCardStyleIsDefault { get { return workingCompanionCardStyle == RebirthCompanionCardStyle.Simple; } }
    [XuiXmlBinding("rebirth_companion_card_style_changed")] public bool CompanionCardStyleChanged { get { return GetBaselineState().CompanionCardStyle != workingCompanionCardStyle; } }
    [XuiXmlBinding("rebirth_target_name_color")] public string TargetNameColor { get { return workingTargetNameColorR + "," + workingTargetNameColorG + "," + workingTargetNameColorB + ",255"; } }
    [XuiXmlBinding("rebirth_target_name_color_default")] public bool TargetNameColorIsDefault { get { return workingTargetNameColorR == RebirthTargetNameColorPolicy.DefaultR && workingTargetNameColorG == RebirthTargetNameColorPolicy.DefaultG && workingTargetNameColorB == RebirthTargetNameColorPolicy.DefaultB; } }
    [XuiXmlBinding("rebirth_target_name_color_changed")] public bool TargetNameColorChanged
    {
        get
        {
            RebirthSandboxState baseline = GetBaselineState();
            return baseline.TargetNameColorR != workingTargetNameColorR || baseline.TargetNameColorG != workingTargetNameColorG || baseline.TargetNameColorB != workingTargetNameColorB;
        }
    }

    [XuiXmlBinding("rebirth_advanced_farming_randomizer_locked")]
    public bool AdvancedFarmingRandomizerLocked
    {
        get
        {
            return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.AdvancedFarming)
                != RebirthSandboxOptionRandomizerMode.Unlocked;
        }
    }

    [XuiXmlBinding("rebirth_advanced_farming_randomizer_lock_sprite")]
    public string AdvancedFarmingRandomizerLockSprite
    {
        get { return AdvancedFarmingRandomizerLocked ? LockedSprite : UnlockedSprite; }
    }

    [XuiXmlBinding("rebirth_advanced_farming_randomizer_lock_is_minimum")]
    public bool AdvancedFarmingRandomizerLockIsMinimum
    {
        get
        {
            return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.AdvancedFarming)
                == RebirthSandboxOptionRandomizerMode.Minimum;
        }
    }

    [XuiXmlBinding("rebirth_advanced_farming_randomizer_lock_is_maximum")]
    public bool AdvancedFarmingRandomizerLockIsMaximum
    {
        get
        {
            return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.AdvancedFarming)
                == RebirthSandboxOptionRandomizerMode.Maximum;
        }
    }


    [XuiXmlBinding("rebirth_auto_replant_trees_default")]
    public bool AutoReplantTreesIsDefault { get { return !workingAutoReplantTrees; } }

    [XuiXmlBinding("rebirth_auto_replant_trees_changed")]
    public bool AutoReplantTreesChanged { get { return GetBaselineState().AutoReplantTrees != workingAutoReplantTrees; } }

    [XuiXmlBinding("rebirth_vehicle_block_respawn_default")]
    public bool VehicleBlockRespawnIsDefault { get { return workingVehicleBlockRespawnDays == 0; } }

    [XuiXmlBinding("rebirth_vehicle_block_respawn_changed")]
    public bool VehicleBlockRespawnChanged { get { return GetBaselineState().VehicleBlockRespawnDays != workingVehicleBlockRespawnDays; } }

    [XuiXmlBinding("rebirth_hybrid_path_smoothing_default")]
    public bool HybridPathSmoothingIsDefault { get { return workingHybridPathSmoothing; } }

    [XuiXmlBinding("rebirth_hybrid_path_smoothing_changed")]
    public bool HybridPathSmoothingChanged { get { return GetBaselineState().HybridPathSmoothing != workingHybridPathSmoothing; } }

    [XuiXmlBinding("rebirth_zombies_destroy_areas_default")]
    public bool ZombiesDestroyAreasIsDefault { get { return !workingZombiesDestroyAreas; } }

    [XuiXmlBinding("rebirth_zombies_destroy_areas_changed")]
    public bool ZombiesDestroyAreasChanged { get { return GetBaselineState().ZombiesDestroyAreas != workingZombiesDestroyAreas; } }

    [XuiXmlBinding("rebirth_rancher_ranged_attack_default")]
    public bool RancherRangedAttackIsDefault { get { return !workingRancherRangedAttack; } }

    [XuiXmlBinding("rebirth_rancher_ranged_attack_changed")]
    public bool RancherRangedAttackChanged { get { return GetBaselineState().RancherRangedAttack != workingRancherRangedAttack; } }

    [XuiXmlBinding("rebirth_sleeper_respawns_default")]
    public bool SleeperRespawnsIsDefault { get { return !workingSleeperRespawns; } }

    [XuiXmlBinding("rebirth_sleeper_respawns_changed")]
    public bool SleeperRespawnsChanged { get { return GetBaselineState().SleeperRespawns != workingSleeperRespawns; } }

[XuiXmlBinding("rebirth_sleeper_spawn_multiplier_default")]
    public bool SleeperSpawnMultiplierIsDefault { get { return workingSleeperSpawnMultiplier == 1; } }

    [XuiXmlBinding("rebirth_sleeper_spawn_multiplier_changed")]
    public bool SleeperSpawnMultiplierChanged { get { return GetBaselineState().SleeperSpawnMultiplier != workingSleeperSpawnMultiplier; } }

    [XuiXmlBinding("rebirth_infested_sleeper_spawn_multiplier_default")]
    public bool InfestedSleeperSpawnMultiplierIsDefault { get { return workingInfestedSleeperSpawnMultiplier == 2; } }

    [XuiXmlBinding("rebirth_infested_sleeper_spawn_multiplier_changed")]
    public bool InfestedSleeperSpawnMultiplierChanged { get { return GetBaselineState().InfestedSleeperSpawnMultiplier != workingInfestedSleeperSpawnMultiplier; } }

    [XuiXmlBinding("rebirth_suppress_console_error_popups_default")]
    public bool SuppressConsoleErrorPopupsIsDefault { get { return !workingSuppressConsoleErrorPopups; } }

    [XuiXmlBinding("rebirth_suppress_console_error_popups_changed")]
    public bool SuppressConsoleErrorPopupsChanged { get { return GetBaselineState().SuppressConsoleErrorPopups != workingSuppressConsoleErrorPopups; } }

    [XuiXmlBinding("rebirth_tree_density_multiplier_default")]
    public bool TreeDensityMultiplierIsDefault { get { return workingTreeDensityMultiplier == 100; } }
    [XuiXmlBinding("rebirth_tree_density_multiplier_changed")]
    public bool TreeDensityMultiplierChanged { get { return GetBaselineState().TreeDensityMultiplier != workingTreeDensityMultiplier; } }
    [XuiXmlBinding("rebirth_vehicle_density_multiplier_default")]
    public bool VehicleDensityMultiplierIsDefault { get { return workingVehicleDensityMultiplier == 100; } }
    [XuiXmlBinding("rebirth_vehicle_density_multiplier_changed")]
    public bool VehicleDensityMultiplierChanged { get { return GetBaselineState().VehicleDensityMultiplier != workingVehicleDensityMultiplier; } }

    [XuiXmlBinding("rebirth_blocks_catch_fire_default")]
    public bool BlocksCatchFireIsDefault { get { return !workingBlocksCatchFire; } }
    [XuiXmlBinding("rebirth_blocks_catch_fire_changed")]
    public bool BlocksCatchFireChanged { get { return GetBaselineState().BlocksCatchFire != workingBlocksCatchFire; } }

    [XuiXmlBinding("rebirth_fire_affects_heatmap_default")]
    public bool FireAffectsHeatmapIsDefault { get { return !workingFireAffectsHeatmap; } }
    [XuiXmlBinding("rebirth_fire_affects_heatmap_changed")]
    public bool FireAffectsHeatmapChanged { get { return GetBaselineState().FireAffectsHeatmap != workingFireAffectsHeatmap; } }
    [XuiXmlBinding("rebirth_fire_affects_heatmap_enabled")]
    public bool FireAffectsHeatmapEnabled { get { return workingBlocksCatchFire; } }

    [XuiXmlBinding("rebirth_fire_block_damage_speed_default")]
    public bool FireBlockDamageSpeedIsDefault { get { return workingFireBlockDamageSpeed == RebirthFireBlockDamageSpeed.Default; } }
    [XuiXmlBinding("rebirth_fire_block_damage_speed_changed")]
    public bool FireBlockDamageSpeedChanged { get { return GetBaselineState().FireBlockDamageSpeed != workingFireBlockDamageSpeed; } }
    [XuiXmlBinding("rebirth_fire_block_damage_speed_enabled")]
    public bool FireBlockDamageSpeedEnabled { get { return workingBlocksCatchFire; } }

    [XuiXmlBinding("rebirth_max_jobs_default")]
    public bool MaxJobsIsDefault { get { return workingMaxJobs == 11; } }
    [XuiXmlBinding("rebirth_max_jobs_changed")]
    public bool MaxJobsChanged { get { return GetBaselineState().MaxJobs != workingMaxJobs; } }

    [XuiXmlBinding("rebirth_jobs_to_next_tier_default")]
    public bool JobsToNextTierIsDefault { get { return workingJobsToNextTier == RebirthTraderJobPolicy.DefaultJobsToNextTier; } }
    [XuiXmlBinding("rebirth_jobs_to_next_tier_changed")]
    public bool JobsToNextTierChanged { get { return GetBaselineState().JobsToNextTier != workingJobsToNextTier; } }
    [XuiXmlBinding("rebirth_repeat_poi_jobs_default")]
    public bool RepeatPoiJobsIsDefault { get { return workingRepeatPoiJobs == RebirthRepeatPoiPolicy.Medium; } }
    [XuiXmlBinding("rebirth_repeat_poi_jobs_changed")]
    public bool RepeatPoiJobsChanged { get { return GetBaselineState().RepeatPoiJobs != workingRepeatPoiJobs; } }

    [XuiXmlBinding("rebirth_infested_jobs_default")]
    public bool InfestedJobsIsDefault { get { return workingInfestedJobs == RebirthInfestedJobsMode.Default; } }
    [XuiXmlBinding("rebirth_infested_jobs_changed")]
    public bool InfestedJobsChanged { get { return GetBaselineState().InfestedJobs != workingInfestedJobs; } }

    [XuiXmlBinding("rebirth_trader_job_list_default")]
    public bool TraderJobListIsDefault { get { return workingTraderJobList == RebirthTraderJobListMode.Fixed; } }
    [XuiXmlBinding("rebirth_trader_job_list_changed")]
    public bool TraderJobListChanged { get { return GetBaselineState().TraderJobList != workingTraderJobList; } }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_default")]
    public bool TraderJobRecoveryGraceIsDefault { get { return workingTraderJobRecoveryGrace; } }
    [XuiXmlBinding("rebirth_trader_job_recovery_grace_changed")]
    public bool TraderJobRecoveryGraceChanged
    {
        get { return GetBaselineState().TraderJobRecoveryGrace != workingTraderJobRecoveryGrace; }
    }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_duration_default")]
    public bool TraderJobRecoveryGraceDurationIsDefault
    {
        get
        {
            return workingTraderJobRecoveryGraceDuration ==
                   RebirthTraderJobRecoveryGraceDuration.FiveMinutes;
        }
    }
    [XuiXmlBinding("rebirth_trader_job_recovery_grace_duration_changed")]
    public bool TraderJobRecoveryGraceDurationChanged
    {
        get
        {
            return GetBaselineState().TraderJobRecoveryGraceDuration !=
                   workingTraderJobRecoveryGraceDuration;
        }
    }
    [XuiXmlBinding("rebirth_trader_job_recovery_grace_duration_enabled")]
    public bool TraderJobRecoveryGraceDurationEnabled
    {
        get { return workingTraderJobRecoveryGrace; }
    }

    [XuiXmlBinding("rebirth_prevent_healing_overlap_default")]
    public bool PreventHealingOverlapIsDefault { get { return workingPreventHealingOverlap == RebirthHealingOverlapThreshold.Health10; } }
    [XuiXmlBinding("rebirth_prevent_healing_overlap_changed")]
    public bool PreventHealingOverlapChanged { get { return GetBaselineState().PreventHealingOverlap != workingPreventHealingOverlap; } }

    [XuiXmlBinding("rebirth_always_stagger_default")]
    public bool AlwaysStaggerIsDefault { get { return workingAlwaysStagger == RebirthAlwaysStaggerMode.AllQualifyingHits; } }
    [XuiXmlBinding("rebirth_always_stagger_changed")]
    public bool AlwaysStaggerChanged { get { return GetBaselineState().AlwaysStagger != workingAlwaysStagger; } }

    [XuiXmlBinding("rebirth_weather_fog_behavior_default")]
    public bool WeatherFogBehaviorIsDefault { get { return workingWeatherFogBehavior == RebirthWeatherFogBehavior.Dynamic; } }
    [XuiXmlBinding("rebirth_weather_fog_behavior_changed")]
    public bool WeatherFogBehaviorChanged { get { return GetBaselineState().WeatherFogBehavior != workingWeatherFogBehavior; } }
    [XuiXmlBinding("rebirth_weather_fog_intensity_default")]
    public bool WeatherFogIntensityIsDefault { get { return workingWeatherFogIntensity == RebirthWeatherFogIntensity.Normal; } }
    [XuiXmlBinding("rebirth_weather_fog_intensity_changed")]
    public bool WeatherFogIntensityChanged { get { return GetBaselineState().WeatherFogIntensity != workingWeatherFogIntensity; } }
    [XuiXmlBinding("rebirth_weather_fog_intensity_enabled")]
    public bool WeatherFogIntensityEnabled
    {
        get { return RebirthSandboxOptionDependencyPolicy.IsWeatherFogIntensityEnabled(workingWeatherFogBehavior); }
    }
    [XuiXmlBinding("rebirth_uniform_atmosphere_default")]
    public bool UniformAtmosphereIsDefault { get { return workingUniformAtmosphere; } }
    [XuiXmlBinding("rebirth_uniform_atmosphere_changed")]
    public bool UniformAtmosphereChanged { get { return GetBaselineState().UniformAtmosphere != workingUniformAtmosphere; } }

    [XuiXmlBinding("rebirth_pitch_black_default")]
    public bool PitchBlackIsDefault { get { return workingPitchBlack; } }
    [XuiXmlBinding("rebirth_pitch_black_changed")]
    public bool PitchBlackChanged { get { return GetBaselineState().PitchBlack != workingPitchBlack; } }
    [XuiXmlBinding("rebirth_poi_risk_enabled")]
    public bool PoiRiskEnabled
    {
        get { return RebirthSandboxOptionDependencyPolicy.IsPoiRiskEnabled(workingSpawnProgression); }
    }
    [XuiXmlBinding("rebirth_poi_risk_default")]
    public bool PoiRiskIsDefault
    {
        get
        {
            return GetEffectiveWorkingPoiRisk() == (PoiRiskEnabled
                ? RebirthPoiRiskMode.Medium
                : RebirthPoiRiskMode.None);
        }
    }
    [XuiXmlBinding("rebirth_poi_risk_changed")]
    public bool PoiRiskChanged
    {
        get
        {
            return workingSpawnProgression == RebirthSpawnProgressionMode.Gamestage
                && GetBaselineState().PoiRisk != workingPoiRisk;
        }
    }
    [XuiXmlBinding("rebirth_poi_sense_schedule_default")]
    public bool PoiSenseScheduleIsDefault { get { return workingPoiSenseSchedule == RebirthPoiSenseSchedule.Never; } }
    [XuiXmlBinding("rebirth_poi_sense_schedule_changed")]
    public bool PoiSenseScheduleChanged { get { return GetBaselineState().PoiSenseSchedule != workingPoiSenseSchedule; } }
    [XuiXmlBinding("rebirth_poi_sense_intensity_default")]
    public bool PoiSenseIntensityIsDefault { get { return workingPoiSenseIntensity == RebirthPoiSenseIntensity.Medium; } }
    [XuiXmlBinding("rebirth_poi_sense_intensity_changed")]
    public bool PoiSenseIntensityChanged { get { return GetBaselineState().PoiSenseIntensity != workingPoiSenseIntensity; } }
    [XuiXmlBinding("rebirth_poi_sense_intensity_enabled")]
    public bool PoiSenseIntensityEnabled
    {
        get { return RebirthSandboxOptionDependencyPolicy.IsPoiSenseIntensityEnabled(workingPoiSenseSchedule); }
    }

    [XuiXmlBinding("rebirth_loot_trader_areas_default")]
    public bool LootTraderAreasIsDefault { get { return workingLootTraderAreas; } }

    [XuiXmlBinding("rebirth_loot_trader_areas_changed")]
    public bool LootTraderAreasChanged { get { return GetBaselineState().LootTraderAreas != workingLootTraderAreas; } }

    [XuiXmlBinding("rebirth_instant_block_pickup_default")]
    public bool InstantBlockPickupIsDefault
    {
        get { return workingInstantBlockPickup == RebirthInstantBlockPickupMode.Default; }
    }

    [XuiXmlBinding("rebirth_instant_block_pickup_changed")]
    public bool InstantBlockPickupChanged
    {
        get { return GetBaselineState().InstantBlockPickup != workingInstantBlockPickup; }
    }

    [XuiXmlBinding("rebirth_instant_block_pickup_randomizer_locked")]
    public bool InstantBlockPickupRandomizerLocked
    {
        get
        {
            return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.InstantBlockPickup)
                != RebirthSandboxOptionRandomizerMode.Unlocked;
        }
    }

    [XuiXmlBinding("rebirth_instant_block_pickup_randomizer_lock_sprite")]
    public string InstantBlockPickupRandomizerLockSprite
    {
        get { return InstantBlockPickupRandomizerLocked ? LockedSprite : UnlockedSprite; }
    }

    [XuiXmlBinding("rebirth_instant_block_pickup_randomizer_lock_is_minimum")]
    public bool InstantBlockPickupRandomizerLockIsMinimum
    {
        get
        {
            return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.InstantBlockPickup)
                == RebirthSandboxOptionRandomizerMode.Minimum;
        }
    }

    [XuiXmlBinding("rebirth_instant_block_pickup_randomizer_lock_is_maximum")]
    public bool InstantBlockPickupRandomizerLockIsMaximum
    {
        get
        {
            return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.InstantBlockPickup)
                == RebirthSandboxOptionRandomizerMode.Maximum;
        }
    }

    [XuiXmlBinding("rebirth_remote_resources_enabled")]
    public bool RemoteResourcesEnabled { get { return workingRemoteResources; } }

    [XuiXmlBinding("rebirth_remote_resources_default")]
    public bool RemoteResourcesIsDefault { get { return workingRemoteResources; } }

    [XuiXmlBinding("rebirth_remote_resources_changed")]
    public bool RemoteResourcesChanged { get { return GetBaselineState().RemoteResources != workingRemoteResources; } }

    [XuiXmlBinding("rebirth_remote_resources_randomizer_locked")]
    public bool RemoteResourcesRandomizerLocked { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.RemoteResources) != RebirthSandboxOptionRandomizerMode.Unlocked; } }

    [XuiXmlBinding("rebirth_remote_resources_randomizer_lock_sprite")]
    public string RemoteResourcesRandomizerLockSprite { get { return RemoteResourcesRandomizerLocked ? LockedSprite : UnlockedSprite; } }

    [XuiXmlBinding("rebirth_remote_resources_randomizer_lock_is_minimum")]
    public bool RemoteResourcesRandomizerLockIsMinimum { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.RemoteResources) == RebirthSandboxOptionRandomizerMode.Minimum; } }

    [XuiXmlBinding("rebirth_remote_resources_randomizer_lock_is_maximum")]
    public bool RemoteResourcesRandomizerLockIsMaximum { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.RemoteResources) == RebirthSandboxOptionRandomizerMode.Maximum; } }

    [XuiXmlBinding("rebirth_remote_resources_distance_default")]
    public bool RemoteResourcesDistanceIsDefault { get { return workingRemoteResourcesDistance == RebirthResourceDistancePolicy.RemoteResourcesDefault; } }
    [XuiXmlBinding("rebirth_remote_resources_distance_changed")]
    public bool RemoteResourcesDistanceChanged { get { return GetBaselineState().RemoteResourcesDistance != workingRemoteResourcesDistance; } }
    [XuiXmlBinding("rebirth_remote_resources_distance_randomizer_locked")]
    public bool RemoteResourcesDistanceRandomizerLocked { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.RemoteResourcesDistance) != RebirthSandboxOptionRandomizerMode.Unlocked; } }
    [XuiXmlBinding("rebirth_remote_resources_distance_randomizer_lock_sprite")]
    public string RemoteResourcesDistanceRandomizerLockSprite { get { return RemoteResourcesDistanceRandomizerLocked ? LockedSprite : UnlockedSprite; } }
    [XuiXmlBinding("rebirth_remote_resources_distance_randomizer_lock_is_minimum")]
    public bool RemoteResourcesDistanceRandomizerLockIsMinimum { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.RemoteResourcesDistance) == RebirthSandboxOptionRandomizerMode.Minimum; } }
    [XuiXmlBinding("rebirth_remote_resources_distance_randomizer_lock_is_maximum")]
    public bool RemoteResourcesDistanceRandomizerLockIsMaximum { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.RemoteResourcesDistance) == RebirthSandboxOptionRandomizerMode.Maximum; } }

    [XuiXmlBinding("rebirth_secure_access_sharing_default")]
    public bool SecureAccessSharingIsDefault { get { return workingSecureAccessSharing == RebirthSecureAccessMode.PinAccessList; } }

    [XuiXmlBinding("rebirth_secure_access_sharing_changed")]
    public bool SecureAccessSharingChanged { get { return GetBaselineState().SecureAccessSharing != workingSecureAccessSharing; } }

    [XuiXmlBinding("rebirth_horn_activated_doors_default")]
    public bool HornActivatedDoorsIsDefault { get { return workingHornActivatedDoors == RebirthHornActivatedDoorsMode.All; } }

    [XuiXmlBinding("rebirth_horn_activated_doors_changed")]
    public bool HornActivatedDoorsChanged { get { return GetBaselineState().HornActivatedDoors != workingHornActivatedDoors; } }

    [XuiXmlBinding("rebirth_quick_stack_enabled")]
    public bool QuickStackEnabled { get { return workingQuickStack != RebirthQuickStackMode.Off; } }

    [XuiXmlBinding("rebirth_quick_stack_default")]
    public bool QuickStackIsDefault { get { return workingQuickStack == RebirthQuickStackMode.Full; } }

    [XuiXmlBinding("rebirth_quick_stack_changed")]
    public bool QuickStackChanged { get { return GetBaselineState().QuickStack != workingQuickStack; } }

    [XuiXmlBinding("rebirth_quick_stack_randomizer_locked")]
    public bool QuickStackRandomizerLocked
    {
        get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.QuickStack) != RebirthSandboxOptionRandomizerMode.Unlocked; }
    }

    [XuiXmlBinding("rebirth_quick_stack_randomizer_lock_sprite")]
    public string QuickStackRandomizerLockSprite { get { return QuickStackRandomizerLocked ? LockedSprite : UnlockedSprite; } }

    [XuiXmlBinding("rebirth_quick_stack_randomizer_lock_is_minimum")]
    public bool QuickStackRandomizerLockIsMinimum
    {
        get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.QuickStack) == RebirthSandboxOptionRandomizerMode.Minimum; }
    }

    [XuiXmlBinding("rebirth_quick_stack_randomizer_lock_is_maximum")]
    public bool QuickStackRandomizerLockIsMaximum
    {
        get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.QuickStack) == RebirthSandboxOptionRandomizerMode.Maximum; }
    }

    [XuiXmlBinding("rebirth_quick_stack_distance_default")]
    public bool QuickStackDistanceIsDefault { get { return workingQuickStackDistance == RebirthResourceDistancePolicy.QuickStackDefault; } }
    [XuiXmlBinding("rebirth_quick_stack_distance_changed")]
    public bool QuickStackDistanceChanged { get { return GetBaselineState().QuickStackDistance != workingQuickStackDistance; } }
    [XuiXmlBinding("rebirth_quick_stack_distance_randomizer_locked")]
    public bool QuickStackDistanceRandomizerLocked { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.QuickStackDistance) != RebirthSandboxOptionRandomizerMode.Unlocked; } }
    [XuiXmlBinding("rebirth_quick_stack_distance_randomizer_lock_sprite")]
    public string QuickStackDistanceRandomizerLockSprite { get { return QuickStackDistanceRandomizerLocked ? LockedSprite : UnlockedSprite; } }
    [XuiXmlBinding("rebirth_quick_stack_distance_randomizer_lock_is_minimum")]
    public bool QuickStackDistanceRandomizerLockIsMinimum { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.QuickStackDistance) == RebirthSandboxOptionRandomizerMode.Minimum; } }
    [XuiXmlBinding("rebirth_quick_stack_distance_randomizer_lock_is_maximum")]
    public bool QuickStackDistanceRandomizerLockIsMaximum { get { return RebirthSandboxRandomizerLockState.GetMode(RebirthSandboxOptionId.QuickStackDistance) == RebirthSandboxOptionRandomizerMode.Maximum; } }


    [XuiXmlBinding("rebirth_protect_crate_default")] public bool ProtectCrateIsDefault { get { return workingProtectCrate; } }
    [XuiXmlBinding("rebirth_protect_crate_changed")] public bool ProtectCrateChanged { get { return GetBaselineState().ProtectCrate != workingProtectCrate; } }

    [XuiXmlBinding("rebirth_boss_events_default")] public bool BossEventsIsDefault { get { return workingBossEventStartLevel == RebirthBossEventStartLevel.PlayerLevel15; } }
    [XuiXmlBinding("rebirth_boss_events_changed")] public bool BossEventsChanged { get { return GetBaselineState().BossEventStartLevel != workingBossEventStartLevel; } }
    [XuiXmlBinding("rebirth_boss_events_enabled")] public bool BossEventsEnabled { get { return RebirthBossEventStartLevelPolicy.IsEnabled(workingBossEventStartLevel); } }
    [XuiXmlBinding("rebirth_boss_event_frequency_default")] public bool BossEventFrequencyIsDefault { get { return workingBossEventFrequency == RebirthBossEventFrequency.Normal; } }
    [XuiXmlBinding("rebirth_boss_event_frequency_changed")] public bool BossEventFrequencyChanged { get { return GetBaselineState().BossEventFrequency != workingBossEventFrequency; } }
    [XuiXmlBinding("rebirth_boss_event_maximum_default")] public bool BossEventMaximumIsDefault { get { return workingBossEventMaximumPerDay == 2; } }
    [XuiXmlBinding("rebirth_boss_event_maximum_changed")] public bool BossEventMaximumChanged { get { return GetBaselineState().BossEventMaximumPerDay != workingBossEventMaximumPerDay; } }
    [XuiXmlBinding("rebirth_boss_event_size_default")] public bool BossEventSizeIsDefault { get { return workingBossEventSize == RebirthBossEventSize.Normal; } }
    [XuiXmlBinding("rebirth_boss_event_size_changed")] public bool BossEventSizeChanged { get { return GetBaselineState().BossEventSize != workingBossEventSize; } }
    [XuiXmlBinding("rebirth_boss_event_difficulty_default")] public bool BossEventDifficultyIsDefault { get { return workingBossEventDifficulty == RebirthBossEventDifficulty.Normal; } }
    [XuiXmlBinding("rebirth_boss_event_difficulty_changed")] public bool BossEventDifficultyChanged { get { return GetBaselineState().BossEventDifficulty != workingBossEventDifficulty; } }
    [XuiXmlBinding("rebirth_boss_event_time_default")] public bool BossEventTimeIsDefault { get { return workingBossEventTime == RebirthBossEventTime.DayAndNight; } }
    [XuiXmlBinding("rebirth_boss_event_time_changed")] public bool BossEventTimeChanged { get { return GetBaselineState().BossEventTime != workingBossEventTime; } }
    [XuiXmlBinding("rebirth_boss_event_blood_moon_default")] public bool BossEventBloodMoonIsDefault { get { return workingBossEventBloodMoonDay; } }
    [XuiXmlBinding("rebirth_boss_event_blood_moon_changed")] public bool BossEventBloodMoonChanged { get { return GetBaselineState().BossEventBloodMoonDay != workingBossEventBloodMoonDay; } }
    [XuiXmlBinding("rebirth_boss_event_restriction_default")] public bool BossEventRestrictionIsDefault { get { return workingBossEventRestriction == RebirthBossEventRestriction.Anywhere; } }
    [XuiXmlBinding("rebirth_boss_event_restriction_changed")] public bool BossEventRestrictionChanged { get { return GetBaselineState().BossEventRestriction != workingBossEventRestriction; } }
    [XuiXmlBinding("rebirth_boss_event_rewards_default")] public bool BossEventRewardsIsDefault { get { return workingBossEventRewards; } }
    [XuiXmlBinding("rebirth_boss_event_rewards_changed")] public bool BossEventRewardsChanged { get { return GetBaselineState().BossEventRewards != workingBossEventRewards; } }
    [XuiXmlBinding("rebirth_literature_study_time_default")] public bool LiteratureStudyTimeIsDefault { get { return workingLiteratureStudyTime==RebirthLiteratureStudyTimePolicy.Default; } }
    [XuiXmlBinding("rebirth_literature_study_time_changed")] public bool LiteratureStudyTimeChanged { get { return GetBaselineState().LiteratureStudyTime!=workingLiteratureStudyTime; } }

    [XuiXmlBinding("rebirth_boss_event_notifications_default")] public bool BossEventNotificationsIsDefault { get { return workingBossEventNotifications; } }
    [XuiXmlBinding("rebirth_boss_event_notifications_changed")] public bool BossEventNotificationsChanged { get { return GetBaselineState().BossEventNotifications != workingBossEventNotifications; } }

    private RebirthSandboxState BuildWorkingState()
    {
        return new RebirthSandboxState
        {
            PlayerProgression = workingPlayerProgression,
            ScrollbarMode = workingScrollbarMode,
            RequireTimedReading = workingRequireTimedReading,
            LiteratureStudyTime = workingLiteratureStudyTime,
            AdvancedFarming = workingAdvancedFarming,
            InstantBlockPickup = workingInstantBlockPickup,
            QuickStack = workingQuickStack,
            QuickStackDistance = workingQuickStackDistance,
            RemoteResources = workingRemoteResources,
            RemoteResourcesDistance = workingRemoteResourcesDistance,
            GuardDistance = workingGuardDistance,
            FullControlDistance = workingFullControlDistance,
            HuntingDistance = workingHuntingDistance,
            CompanionCardStyle = workingCompanionCardStyle,
            TargetNameColorR = workingTargetNameColorR,
            TargetNameColorG = workingTargetNameColorG,
            TargetNameColorB = workingTargetNameColorB,
            SecureAccessSharing = workingSecureAccessSharing,
            HornActivatedDoors = workingHornActivatedDoors,
            SpawnProgression = workingSpawnProgression,
            AutoReplantTrees = workingAutoReplantTrees,
            VehicleBlockRespawnDays = workingVehicleBlockRespawnDays,
            HybridPathSmoothing = workingHybridPathSmoothing,
            ZombiesDestroyAreas = workingZombiesDestroyAreas,
            RancherRangedAttack = workingRancherRangedAttack,
            SleeperRespawns = workingSleeperRespawns,
            LootTraderAreas = workingLootTraderAreas,
            SleeperSpawnMultiplier = workingSleeperSpawnMultiplier,
            InfestedSleeperSpawnMultiplier = workingInfestedSleeperSpawnMultiplier,
            SuppressConsoleErrorPopups = workingSuppressConsoleErrorPopups,
            TreeDensityMultiplier = workingTreeDensityMultiplier,
            VehicleDensityMultiplier = workingVehicleDensityMultiplier,
            BlocksCatchFire = workingBlocksCatchFire,
            FireAffectsHeatmap = workingFireAffectsHeatmap,
            FireBlockDamageSpeed = workingFireBlockDamageSpeed,
            MaxJobs = workingMaxJobs,
            JobsToNextTier = workingJobsToNextTier,
            RepeatPoiJobs = workingRepeatPoiJobs,
            InfestedJobs = workingInfestedJobs,
            TraderJobList = workingTraderJobList,
            TraderJobRecoveryGrace = workingTraderJobRecoveryGrace,
            TraderJobRecoveryGraceDuration = workingTraderJobRecoveryGraceDuration,
            PreventHealingOverlap = workingPreventHealingOverlap,
            AlwaysStagger = workingAlwaysStagger,
            WeatherFogBehavior = workingWeatherFogBehavior,
            WeatherFogIntensity = workingWeatherFogIntensity,
            UniformAtmosphere = workingUniformAtmosphere,
            PoiRisk = workingPoiRisk,
            PoiSenseSchedule = workingPoiSenseSchedule,
            PoiSenseIntensity = workingPoiSenseIntensity,
            PitchBlack = workingPitchBlack,
            BossEventStartLevel = workingBossEventStartLevel,
            BossEventFrequency = workingBossEventFrequency,
            BossEventMaximumPerDay = workingBossEventMaximumPerDay,
            BossEventSize = workingBossEventSize,
            BossEventDifficulty = workingBossEventDifficulty,
            BossEventTime = workingBossEventTime,
            BossEventBloodMoonDay = workingBossEventBloodMoonDay,
            BossEventRestriction = workingBossEventRestriction,
            BossEventRewards = workingBossEventRewards,
            BossEventNotifications = workingBossEventNotifications,
            ProtectCrate = workingProtectCrate
        };
    }

    private void ApplyStateToWorking(RebirthSandboxState state)
    {
        if (state == null) state = new RebirthSandboxState();
        workingPlayerProgression = state.PlayerProgression;
        workingScrollbarMode = state.ScrollbarMode;
        workingRequireTimedReading = state.RequireTimedReading;
        workingLiteratureStudyTime = state.LiteratureStudyTime;
        workingAdvancedFarming = state.AdvancedFarming;
        workingInstantBlockPickup = state.InstantBlockPickup;
        workingQuickStack = state.QuickStack;
        workingQuickStackDistance = state.QuickStackDistance;
        workingRemoteResources = state.RemoteResources;
        workingRemoteResourcesDistance = state.RemoteResourcesDistance;
        workingGuardDistance = state.GuardDistance;
        workingFullControlDistance = state.FullControlDistance;
        workingHuntingDistance = state.HuntingDistance;
        workingCompanionCardStyle = state.CompanionCardStyle;
        workingTargetNameColorR = state.TargetNameColorR;
        workingTargetNameColorG = state.TargetNameColorG;
        workingTargetNameColorB = state.TargetNameColorB;
        workingSecureAccessSharing = state.SecureAccessSharing;
        workingHornActivatedDoors = state.HornActivatedDoors;
        workingSpawnProgression = state.SpawnProgression;
        workingAutoReplantTrees = state.AutoReplantTrees;
        workingVehicleBlockRespawnDays = state.VehicleBlockRespawnDays;
        workingHybridPathSmoothing = state.HybridPathSmoothing;
        workingZombiesDestroyAreas = state.ZombiesDestroyAreas;
        workingRancherRangedAttack = state.RancherRangedAttack;
        workingSleeperRespawns = state.SleeperRespawns;
        workingLootTraderAreas = state.LootTraderAreas;
        workingSleeperSpawnMultiplier = state.SleeperSpawnMultiplier;
        workingInfestedSleeperSpawnMultiplier = state.InfestedSleeperSpawnMultiplier;
        workingSuppressConsoleErrorPopups = state.SuppressConsoleErrorPopups;
        workingTreeDensityMultiplier = state.TreeDensityMultiplier;
        workingVehicleDensityMultiplier = state.VehicleDensityMultiplier;
        workingBlocksCatchFire = state.BlocksCatchFire;
        workingFireAffectsHeatmap = state.FireAffectsHeatmap;
        workingFireBlockDamageSpeed = state.FireBlockDamageSpeed;
        workingMaxJobs = state.MaxJobs;
        workingJobsToNextTier = state.JobsToNextTier;
        workingRepeatPoiJobs = state.RepeatPoiJobs;
        workingInfestedJobs = state.InfestedJobs;
        workingTraderJobList = state.TraderJobList;
        workingTraderJobRecoveryGrace = state.TraderJobRecoveryGrace;
        workingTraderJobRecoveryGraceDuration = state.TraderJobRecoveryGraceDuration;
        workingPreventHealingOverlap = state.PreventHealingOverlap;
        workingAlwaysStagger = state.AlwaysStagger;
        workingWeatherFogBehavior = state.WeatherFogBehavior;
        workingWeatherFogIntensity = state.WeatherFogIntensity;
        workingUniformAtmosphere = state.UniformAtmosphere;
        workingPoiRisk = state.PoiRisk;
        workingPoiSenseSchedule = state.PoiSenseSchedule;
        workingPoiSenseIntensity = state.PoiSenseIntensity;
        workingPitchBlack = state.PitchBlack;
        workingBossEventStartLevel = state.BossEventStartLevel;
        workingBossEventFrequency = state.BossEventFrequency;
        workingBossEventMaximumPerDay = state.BossEventMaximumPerDay;
        workingBossEventSize = state.BossEventSize;
        workingBossEventDifficulty = state.BossEventDifficulty;
        workingBossEventTime = state.BossEventTime;
        workingBossEventBloodMoonDay = state.BossEventBloodMoonDay;
        workingBossEventRestriction = state.BossEventRestriction;
        workingBossEventRewards = state.BossEventRewards;
        workingBossEventNotifications = state.BossEventNotifications;
        workingProtectCrate = state.ProtectCrate;
        InvalidateWorkingProjection();
    }

    private void InvalidateWorkingProjection()
    {
        if (workingProjectionRevision < int.MaxValue) workingProjectionRevision++;
        cachedWorkingCodeRevision = -1;
        cachedWorkingCode = string.Empty;
    }

    private bool IsEditContextCurrent()
    {
        RebirthSandboxUiSession.EnsureContextCurrent();
        return string.Equals(editContextIdentity, RebirthSandboxUiSession.ContextIdentity, StringComparison.Ordinal);
    }

    public override void OnOpen()
    {
        windowGroup.isEscClosable = false;
        base.OnOpen();
        BindComboEvents();

        RebirthSandboxOptionManager.Current.ReloadPresets();
        RebirthSandboxState state;
        if (!RebirthSandboxOptionManager.TryDecode(RebirthSandboxUiSession.Code, out state))
            state = new RebirthSandboxState();

        ApplyStateToWorking(state);
        editContextIdentity = RebirthSandboxUiSession.ContextIdentity;
        RebirthSandboxPreset resolved = RebirthSandboxOptionManager.Current.ResolvePreset(
            RebirthSandboxUiSession.PresetName,
            RebirthSandboxUiSession.Code);
        if (resolved == null)
            resolved = RebirthSandboxOptionManager.Current.GetPreset(RebirthSandboxOptionManager.DefaultPresetName);

        workingPresetName = resolved != null ? resolved.Name : RebirthSandboxOptionManager.DefaultPresetName;
        selectedPresetBaselineCode = resolved != null
            ? resolved.Code
            : RebirthSandboxOptionManager.Encode(new RebirthSandboxState());
        workingAsNewPreset = resolved == null
            || (!resolved.IsUserPreset
                && !AreCodesSemanticallyEquivalent(RebirthSandboxUiSession.Code, selectedPresetBaselineCode));
        sessionRollbackPresetName = workingPresetName;
        sessionRollbackCode = selectedPresetBaselineCode;

        PopulateOptionValues();
        TracePlayerProgressionState("on-open", true);
        RefreshPresetSelectors();
        if (tabSelector != null)
            tabSelector.SelectedTabIndex = 0;
        IsDirty = true;
    }

    public override void OnClose()
    {
        UnbindComboEvents();
        RestoreSessionRollbackPoint();
        base.OnClose();
        Action callback = onClose;
        onClose = null;
        if (callback != null)
            callback();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (XUiUtils.HotkeysAllowedFor(viewComponent))
        {
            if (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
                BtnBack_OnPressed(this, -1);
            if (xui.playerUI.playerInput.GUIActions.Apply.WasReleased)
                BtnSave_OnPressed(this, 0);
        }

        if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled)
        {
            playerProgressionDebugPollTimer -= dt;
            if (playerProgressionDebugPollTimer <= 0f)
            {
                playerProgressionDebugPollTimer = 0.20f;
                TracePlayerProgressionState("poll", false);
            }
        }
        handleDirtyUpdateDefault();
    }

    public void PresetGroup_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents)
            return;

        int index = cbxPresetGroup.SelectedIndex;
        if (index < 0 || index >= groupIds.Count)
            return;

        PopulatePresets(groupIds[index], null, true);
    }

    public void Preset_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents)
            return;

        int index = cbxPreset.SelectedIndex;
        if (index < 0 || index >= visiblePresets.Count)
            return;

        ApplyPreset(visiblePresets[index]);
    }

    public void PlayerProgression_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        int eventIndex = cbxPlayerProgression != null ? cbxPlayerProgression.SelectedIndex : -999;
        { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
            "VALUE-CHANGED begin suppressEvents=" + suppressEvents
            + " old='" + (oldValue ?? string.Empty) + "' new='" + (newValue ?? string.Empty) + "'"
            + " selectedIndex=" + eventIndex + " working=" + workingPlayerProgression); }

        if (suppressEvents || cbxPlayerProgression == null)
        {
            TracePlayerProgressionState("value-changed-ignored", true);
            return;
        }

        RebirthPlayerProgressionMode lockedMode;
        if (RebirthSandboxUiSession.TryGetLockedPlayerProgression(out lockedMode))
        {
            { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
                "VALUE-CHANGED LOCKED requestedIndex=" + eventIndex + " lockedMode=" + lockedMode
                + " " + RebirthSandboxUiSession.CharacterProgressionDebugContext); }
            workingPlayerProgression = lockedMode;
            suppressEvents = true;
            try { cbxPlayerProgression.SelectedIndex = (int)lockedMode; } finally { suppressEvents = false; }
            TracePlayerProgressionState("value-changed-reverted-by-lock", true);
            return;
        }

        int selectedIndex = cbxPlayerProgression.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex > (int)RebirthPlayerProgressionMode.Rebirth)
        {
            { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression("VALUE-CHANGED invalid selectedIndex=" + selectedIndex); }
            TracePlayerProgressionState("value-changed-invalid-index", true);
            return;
        }

        RebirthPlayerProgressionMode before = workingPlayerProgression;
        workingPlayerProgression = (RebirthPlayerProgressionMode)selectedIndex;
        { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
            "VALUE-CHANGED accepted working=" + before + "->" + workingPlayerProgression); }
        MarkWorkingStateChanged();
        TracePlayerProgressionState("value-changed-after-publish", true);
    }

    public void SpawnProgression_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents)
            return;
        int selectedIndex = cbxSpawnProgression.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex > (int)RebirthSpawnProgressionMode.Gamestage)
            return;
        // Changing the parent option changes only POI Risk's effective value. The
        // configured value is retained and returns automatically with Gamestage.
        workingSpawnProgression = (RebirthSpawnProgressionMode)selectedIndex;

        RefreshOptionValue();
        MarkWorkingStateChanged();
    }

    [XuiBindEvent("OnPress", "btnSpawnProgressionRandomizerLock")]
    public void SpawnProgressionRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        int selectedIndex = cbxSpawnProgression != null ? cbxSpawnProgression.SelectedIndex : (int)workingSpawnProgression;
        RebirthSandboxRandomizerLockState.CycleMode(RebirthSandboxOptionId.SpawnProgression, selectedIndex);
        if (sender != null) sender.IsDirty = true;
        IsDirty = true;
    }


    public void AutoReplantTrees_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxAutoReplantTrees == null) return;
        workingAutoReplantTrees = cbxAutoReplantTrees.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }

    public void VehicleBlockRespawn_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxVehicleBlockRespawn == null) return;
        int[] values = { 0, 3, 7, 14, 21 };
        int index = cbxVehicleBlockRespawn.SelectedIndex;
        workingVehicleBlockRespawnDays = index >= 0 && index < values.Length ? values[index] : 0;
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
        RefreshBindings();
    }

    public void HybridPathSmoothing_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxHybridPathSmoothing == null) return;
        workingHybridPathSmoothing = cbxHybridPathSmoothing.SelectedIndex == 1;
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
        RefreshBindings();
    }

    public void ZombiesDestroyAreas_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxZombiesDestroyAreas == null) return;
        workingZombiesDestroyAreas = cbxZombiesDestroyAreas.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }

    public void RancherRangedAttack_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxRancherRangedAttack == null) return;
        workingRancherRangedAttack = cbxRancherRangedAttack.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }

    public void SleeperSpawnMultiplier_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSleeperSpawnMultiplier == null) return;
        workingSleeperSpawnMultiplier = cbxSleeperSpawnMultiplier.SelectedIndex + 1;
        MarkAsNewPresetWhenEditingBuiltIn(); PublishWorkingState(); IsDirty = true; RefreshBindings();
    }

    public void InfestedSleeperSpawnMultiplier_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxInfestedSleeperSpawnMultiplier == null) return;
        workingInfestedSleeperSpawnMultiplier = cbxInfestedSleeperSpawnMultiplier.SelectedIndex + 2;
        MarkAsNewPresetWhenEditingBuiltIn(); PublishWorkingState(); IsDirty = true; RefreshBindings();
    }

    public void SuppressConsoleErrorPopups_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSuppressConsoleErrorPopups == null) return;
        workingSuppressConsoleErrorPopups = cbxSuppressConsoleErrorPopups.SelectedIndex == 1;
        MarkAsNewPresetWhenEditingBuiltIn(); PublishWorkingState(); IsDirty = true; RefreshBindings();
    }

    public void TreeDensityMultiplier_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxTreeDensityMultiplier == null) return;
        int index = cbxTreeDensityMultiplier.SelectedIndex;
        if (index < 0 || index >= RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers.Length) return;
        workingTreeDensityMultiplier = RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers[index];
        MarkAsNewPresetWhenEditingBuiltIn(); PublishWorkingState(); IsDirty = true; RefreshBindings();
    }

    public void VehicleDensityMultiplier_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxVehicleDensityMultiplier == null) return;
        int index = cbxVehicleDensityMultiplier.SelectedIndex;
        if (index < 0 || index >= RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers.Length) return;
        workingVehicleDensityMultiplier = RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers[index];
        MarkAsNewPresetWhenEditingBuiltIn(); PublishWorkingState(); IsDirty = true; RefreshBindings();
    }

    public void LootTraderAreas_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxLootTraderAreas == null) return;
        workingLootTraderAreas = cbxLootTraderAreas.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }

    public void SleeperRespawns_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSleeperRespawns == null) return;
        workingSleeperRespawns = cbxSleeperRespawns.SelectedIndex == 1;
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
        RefreshBindings();
    }

    public void AdvancedFarming_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents)
            return;

        workingAdvancedFarming = cbxAdvancedFarming.SelectedIndex == 1;
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
    }


    public void GuardDistance_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxGuardDistance == null) return; int i=cbxGuardDistance.SelectedIndex; if(i>=0&&i<RebirthCompanionDistancePolicy.GuardAllowedDistances.Length){workingGuardDistance=RebirthCompanionDistancePolicy.GuardAllowedDistances[i]; MarkWorkingStateChanged();} }
    public void FullControlDistance_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxFullControlDistance == null) return; int i=cbxFullControlDistance.SelectedIndex; if(i>=0&&i<RebirthCompanionDistancePolicy.FullControlAllowedDistances.Length){workingFullControlDistance=RebirthCompanionDistancePolicy.FullControlAllowedDistances[i]; MarkWorkingStateChanged();} }
    public void HuntingDistance_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxHuntingDistance == null) return; int i=cbxHuntingDistance.SelectedIndex; if(i>=0&&i<RebirthCompanionDistancePolicy.HuntingAllowedDistances.Length){workingHuntingDistance=RebirthCompanionDistancePolicy.HuntingAllowedDistances[i]; MarkWorkingStateChanged();} }
    [XuiXmlBinding("rebirth_scrollbar_mode_tooltip")] public string ScrollbarModeTooltip { get { return Localization.Get("xuiRebirthScrollbarModeDesc")+(RebirthScrollbarPagingInstaller.Available?string.Empty:" "+Localization.Get("xuiRebirthScrollbarUnavailable")); } }
    [XuiXmlBinding("rebirth_scrollbar_mode_default")] public bool ScrollbarModeDefault { get { return workingScrollbarMode==RebirthScrollbarMode.Smooth; } }
    [XuiXmlBinding("rebirth_scrollbar_mode_changed")] public bool ScrollbarModeChanged { get { return GetBaselineState().ScrollbarMode!=workingScrollbarMode; } }
    public void ScrollbarMode_OnValueChanged(XUiController sender,string oldValue,string newValue) { if(suppressEvents||cbxScrollbarMode==null)return;workingScrollbarMode=cbxScrollbarMode.SelectedIndex==1?RebirthScrollbarMode.Paged:RebirthScrollbarMode.Smooth;MarkWorkingStateChanged(); }
    public void RequireTimedReading_OnValueChanged(XUiController sender,string oldValue,string newValue) { if(suppressEvents||cbxRequireTimedReading==null)return;workingRequireTimedReading=cbxRequireTimedReading.SelectedIndex==1;MarkWorkingStateChanged(); }
    [XuiXmlBinding("rebirth_require_timed_reading_default")] public bool RequireTimedReadingDefault { get { return workingRequireTimedReading; } }
    [XuiXmlBinding("rebirth_require_timed_reading_changed")] public bool RequireTimedReadingChanged { get { return GetBaselineState().RequireTimedReading!=workingRequireTimedReading; } }
    public void LiteratureStudyTime_OnValueChanged(XUiController sender,string oldValue,string newValue)
    {
        if(suppressEvents||cbxLiteratureStudyTime==null)return;
        workingLiteratureStudyTime=(RebirthLiteratureStudyTime)Math.Max(0,Math.Min((int)RebirthLiteratureStudyTime.Double,cbxLiteratureStudyTime.SelectedIndex));
        MarkWorkingStateChanged();
    }

    public void CompanionCardStyle_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxCompanionCardStyle == null) return;
        workingCompanionCardStyle = cbxCompanionCardStyle.SelectedIndex == 1 ? RebirthCompanionCardStyle.Simple : RebirthCompanionCardStyle.Default;
        MarkWorkingStateChanged();
    }

    public void RepeatPoiJobs_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxRepeatPoiJobs == null) return;
        workingRepeatPoiJobs = (RebirthRepeatPoiPolicy)Math.Max(0, Math.Min((int)RebirthRepeatPoiPolicy.Unlimited, cbxRepeatPoiJobs.SelectedIndex));
        HandleManualOptionChange();
    }

    public void InfestedJobs_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxInfestedJobs == null) return;
        workingInfestedJobs = (RebirthInfestedJobsMode)Math.Max(0, Math.Min((int)RebirthInfestedJobsMode.Surprise, cbxInfestedJobs.SelectedIndex));
        MarkWorkingStateChanged();
    }

    public void TraderJobList_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxTraderJobList == null) return;
        workingTraderJobList = cbxTraderJobList.SelectedIndex == 0 ? RebirthTraderJobListMode.Fixed : RebirthTraderJobListMode.Random;
        HandleManualOptionChange();
    }

    public void TraderJobRecoveryGrace_OnValueChanged(
        XUiController sender,
        string oldValue,
        string newValue)
    {
        if (suppressEvents || cbxTraderJobRecoveryGrace == null) return;

        workingTraderJobRecoveryGrace =
            cbxTraderJobRecoveryGrace.SelectedIndex == 1;

        PopulateOptionControls();
        HandleManualOptionChange();
    }

    public void TraderJobRecoveryGraceDuration_OnValueChanged(
        XUiController sender,
        string oldValue,
        string newValue)
    {
        if (suppressEvents ||
            cbxTraderJobRecoveryGraceDuration == null ||
            !TraderJobRecoveryGraceDurationEnabled)
            return;

        workingTraderJobRecoveryGraceDuration =
            (RebirthTraderJobRecoveryGraceDuration)Math.Max(
                0,
                Math.Min(
                    (int)RebirthTraderJobRecoveryGraceDuration.FifteenMinutes,
                    cbxTraderJobRecoveryGraceDuration.SelectedIndex));

        HandleManualOptionChange();
    }

    public void PreventHealingOverlap_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxPreventHealingOverlap == null) return;
        workingPreventHealingOverlap = (RebirthHealingOverlapThreshold)Math.Max(0, Math.Min((int)RebirthHealingOverlapThreshold.Health30, cbxPreventHealingOverlap.SelectedIndex));
        HandleManualOptionChange();
    }

    public void AlwaysStagger_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxAlwaysStagger == null) return;
        workingAlwaysStagger = (RebirthAlwaysStaggerMode)cbxAlwaysStagger.SelectedIndex;
        HandleManualOptionChange();
    }

    public void WeatherFogBehavior_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWeatherFogBehavior == null) return;
        workingWeatherFogBehavior = (RebirthWeatherFogBehavior)cbxWeatherFogBehavior.SelectedIndex;
        PopulateOptionControls();
        HandleManualOptionChange();
    }

    public void WeatherFogIntensity_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWeatherFogIntensity == null || !WeatherFogIntensityEnabled) return;
        workingWeatherFogIntensity = (RebirthWeatherFogIntensity)cbxWeatherFogIntensity.SelectedIndex;
        HandleManualOptionChange();
    }

    public void PitchBlack_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxPitchBlack == null) return;
        workingPitchBlack = cbxPitchBlack.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }

    public void UniformAtmosphere_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxUniformAtmosphere == null) return;
        workingUniformAtmosphere = cbxUniformAtmosphere.SelectedIndex == 1;
        HandleManualOptionChange();
    }

    public void PoiRisk_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxPoiRisk == null || !PoiRiskEnabled) return;
        workingPoiRisk = (RebirthPoiRiskMode)cbxPoiRisk.SelectedIndex;
        HandleManualOptionChange();
    }

    public void PoiSenseSchedule_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxPoiSenseSchedule == null) return;
        workingPoiSenseSchedule = (RebirthPoiSenseSchedule)cbxPoiSenseSchedule.SelectedIndex;
        HandleManualOptionChange();
    }

    public void PoiSenseIntensity_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxPoiSenseIntensity == null || !PoiSenseIntensityEnabled) return;
        workingPoiSenseIntensity = (RebirthPoiSenseIntensity)cbxPoiSenseIntensity.SelectedIndex;
        HandleManualOptionChange();
    }

    public void MaxJobs_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxMaxJobs == null) return;
        int index = cbxMaxJobs.SelectedIndex;
        if (index < 0 || index >= RebirthTraderJobPolicy.AllowedMaxJobs.Length) return;
        workingMaxJobs = RebirthTraderJobPolicy.AllowedMaxJobs[index];
        HandleManualOptionChange();
    }

    public void JobsToNextTier_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxJobsToNextTier == null) return;
        int index = cbxJobsToNextTier.SelectedIndex;
        if (index < 0 || index >= RebirthTraderJobPolicy.AllowedJobsToNextTier.Length) return;
        workingJobsToNextTier = RebirthTraderJobPolicy.AllowedJobsToNextTier[index];
        HandleManualOptionChange();
    }

    public void BlocksCatchFire_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxBlocksCatchFire == null) return;
        workingBlocksCatchFire = cbxBlocksCatchFire.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }

    public void FireAffectsHeatmap_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxFireAffectsHeatmap == null) return;
        workingFireAffectsHeatmap = cbxFireAffectsHeatmap.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }

    public void FireBlockDamageSpeed_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxFireBlockDamageSpeed == null) return;
        int index = cbxFireBlockDamageSpeed.SelectedIndex;
        if (index < (int)RebirthFireBlockDamageSpeed.Slower ||
            index > (int)RebirthFireBlockDamageSpeed.Faster)
            index = (int)RebirthFireBlockDamageSpeed.Default;
        workingFireBlockDamageSpeed = (RebirthFireBlockDamageSpeed)index;
        MarkWorkingStateChanged();
    }

    [XuiBindEvent("OnPress", "btnAdvancedFarmingRandomizerLock")]
    public void AdvancedFarmingRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        int selectedIndex = cbxAdvancedFarming != null ? cbxAdvancedFarming.SelectedIndex : -1;
        if (selectedIndex < 0 || selectedIndex > 1)
            selectedIndex = workingAdvancedFarming ? 1 : 0;

        RebirthSandboxRandomizerLockState.CycleMode(
            RebirthSandboxOptionId.AdvancedFarming,
            selectedIndex);

        if (sender != null)
            sender.IsDirty = true;

        IsDirty = true;
    }

    public void InstantBlockPickup_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents)
            return;

        int selectedIndex = cbxInstantBlockPickup.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex > (int)RebirthInstantBlockPickupMode.Always)
            return;

        workingInstantBlockPickup = (RebirthInstantBlockPickupMode)selectedIndex;
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
    }

    [XuiBindEvent("OnPress", "btnInstantBlockPickupRandomizerLock")]
    public void InstantBlockPickupRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        int selectedIndex = cbxInstantBlockPickup != null ? cbxInstantBlockPickup.SelectedIndex : -1;
        if (selectedIndex < 0 || selectedIndex > (int)RebirthInstantBlockPickupMode.Always)
            selectedIndex = (int)workingInstantBlockPickup;

        RebirthSandboxRandomizerLockState.CycleMode(
            RebirthSandboxOptionId.InstantBlockPickup,
            selectedIndex);

        if (sender != null)
            sender.IsDirty = true;

        IsDirty = true;
    }

    public void RemoteResources_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents) return;
        workingRemoteResources = cbxRemoteResources.SelectedIndex == 1;
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
        RefreshBindings();
    }

    [XuiBindEvent("OnPress", "btnRemoteResourcesRandomizerLock")]
    public void RemoteResourcesRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        int selectedIndex = cbxRemoteResources != null ? cbxRemoteResources.SelectedIndex : -1;
        if (selectedIndex < 0 || selectedIndex > 1) selectedIndex = workingRemoteResources ? 1 : 0;
        RebirthSandboxRandomizerLockState.CycleMode(RebirthSandboxOptionId.RemoteResources, selectedIndex);
        if (sender != null) sender.IsDirty = true;
        IsDirty = true;
    }

    public void RemoteResourcesDistance_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxRemoteResourcesDistance == null) return;
        int index = cbxRemoteResourcesDistance.SelectedIndex;
        if (index < 0 || index >= RebirthResourceDistancePolicy.RemoteResourcesAllowedDistances.Length) return;
        workingRemoteResourcesDistance = RebirthResourceDistancePolicy.RemoteResourcesAllowedDistances[index];
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
        RefreshBindings();
    }

    [XuiBindEvent("OnPress", "btnRemoteResourcesDistanceRandomizerLock")]
    public void RemoteResourcesDistanceRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        int selectedIndex = cbxRemoteResourcesDistance != null ? cbxRemoteResourcesDistance.SelectedIndex : -1;
        if (selectedIndex < 0 || selectedIndex >= RebirthResourceDistancePolicy.RemoteResourcesAllowedDistances.Length)
            selectedIndex = RebirthResourceDistancePolicy.RemoteResourcesDistanceToIndex(workingRemoteResourcesDistance);
        RebirthSandboxRandomizerLockState.CycleMode(RebirthSandboxOptionId.RemoteResourcesDistance, selectedIndex);
        if (sender != null) sender.IsDirty = true;
        IsDirty = true;
    }

    public void SecureAccessSharing_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSecureAccessSharing == null) return;
        workingSecureAccessSharing = (RebirthSecureAccessMode)Mathf.Clamp(cbxSecureAccessSharing.SelectedIndex, 0, 2);
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
    }

    public void HornActivatedDoors_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxHornActivatedDoors == null) return;
        workingHornActivatedDoors = (RebirthHornActivatedDoorsMode)Mathf.Clamp(cbxHornActivatedDoors.SelectedIndex, 0, 2);
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
    }

    public void QuickStack_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents)
            return;
        int index = cbxQuickStack.SelectedIndex;
        if (index < 0 || index > (int)RebirthQuickStackMode.Full)
            return;
        workingQuickStack = (RebirthQuickStackMode)index;
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
        RefreshBindings();
    }

    [XuiBindEvent("OnPress", "btnQuickStackRandomizerLock")]
    public void QuickStackRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        int selectedIndex = cbxQuickStack != null ? cbxQuickStack.SelectedIndex : -1;
        if (selectedIndex < 0 || selectedIndex > (int)RebirthQuickStackMode.Full)
            selectedIndex = (int)workingQuickStack;
        RebirthSandboxRandomizerLockState.CycleMode(RebirthSandboxOptionId.QuickStack, selectedIndex);
        if (sender != null)
            sender.IsDirty = true;
        IsDirty = true;
    }

    public void QuickStackDistance_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxQuickStackDistance == null) return;
        int index = cbxQuickStackDistance.SelectedIndex;
        if (index < 0 || index >= RebirthResourceDistancePolicy.QuickStackAllowedDistances.Length) return;
        workingQuickStackDistance = RebirthResourceDistancePolicy.QuickStackAllowedDistances[index];
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
        RefreshBindings();
    }

    [XuiBindEvent("OnPress", "btnQuickStackDistanceRandomizerLock")]
    public void QuickStackDistanceRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        int selectedIndex = cbxQuickStackDistance != null ? cbxQuickStackDistance.SelectedIndex : -1;
        if (selectedIndex < 0 || selectedIndex >= RebirthResourceDistancePolicy.QuickStackAllowedDistances.Length)
            selectedIndex = RebirthResourceDistancePolicy.QuickStackDistanceToIndex(workingQuickStackDistance);
        RebirthSandboxRandomizerLockState.CycleMode(RebirthSandboxOptionId.QuickStackDistance, selectedIndex);
        if (sender != null) sender.IsDirty = true;
        IsDirty = true;
    }

    [XuiBindEvent("OnPress", "btnBack")]
    public void BtnBack_OnPressed(XUiController sender, int mouseButton)
    {
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }


    public void ProtectCrate_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxProtectCrate == null) return; workingProtectCrate = cbxProtectCrate.SelectedIndex == 1; MarkWorkingStateChanged(); }
    public void BossEvents_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEvents == null) return; workingBossEventStartLevel = (RebirthBossEventStartLevel)Mathf.Clamp(cbxBossEvents.SelectedIndex, 0, 5); MarkWorkingStateChanged(); }
    public void BossEventFrequency_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventFrequency == null) return; workingBossEventFrequency = (RebirthBossEventFrequency)Mathf.Clamp(cbxBossEventFrequency.SelectedIndex, 0, 4); MarkWorkingStateChanged(); }
    public void BossEventMaximumPerDay_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventMaximumPerDay == null) return; int i=cbxBossEventMaximumPerDay.SelectedIndex; workingBossEventMaximumPerDay = i == 5 ? 0 : i + 1; MarkWorkingStateChanged(); }
    public void BossEventSize_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventSize == null) return; workingBossEventSize = (RebirthBossEventSize)Mathf.Clamp(cbxBossEventSize.SelectedIndex, 0, 3); MarkWorkingStateChanged(); }
    public void BossEventDifficulty_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventDifficulty == null) return; workingBossEventDifficulty = (RebirthBossEventDifficulty)Mathf.Clamp(cbxBossEventDifficulty.SelectedIndex, 0, 3); MarkWorkingStateChanged(); }
    public void BossEventTime_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventTime == null) return; workingBossEventTime = (RebirthBossEventTime)Mathf.Clamp(cbxBossEventTime.SelectedIndex, 0, 2); MarkWorkingStateChanged(); }
    public void BossEventBloodMoonDay_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventBloodMoonDay == null) return; workingBossEventBloodMoonDay = cbxBossEventBloodMoonDay.SelectedIndex == 1; MarkWorkingStateChanged(); }
    public void BossEventRestriction_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventRestriction == null) return; workingBossEventRestriction = (RebirthBossEventRestriction)Mathf.Clamp(cbxBossEventRestriction.SelectedIndex, 0, 4); MarkWorkingStateChanged(); }
    public void BossEventRewards_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventRewards == null) return; workingBossEventRewards = cbxBossEventRewards.SelectedIndex == 1; MarkWorkingStateChanged(); }
    public void BossEventNotifications_OnValueChanged(XUiController sender, string oldValue, string newValue) { if (suppressEvents || cbxBossEventNotifications == null) return; workingBossEventNotifications = cbxBossEventNotifications.SelectedIndex == 1; MarkWorkingStateChanged(); }

    [XuiBindEvent("OnPress", "btnTargetNameColor")]
    public void BtnTargetNameColor_OnPressed(XUiController sender, int mouseButton)
    {
        Color32 initial = new Color32((byte)workingTargetNameColorR, (byte)workingTargetNameColorG, (byte)workingTargetNameColorB, 255);
        XUiC_RebirthTargetNameColorPicker.Open(xui, initial, delegate(Color32 selected)
        {
            workingTargetNameColorR = selected.r;
            workingTargetNameColorG = selected.g;
            workingTargetNameColorB = selected.b;
            MarkWorkingStateChanged();
        });
    }

    [XuiBindEvent("OnPress", "btnDefaults")]
    public void BtnDefaults_OnPressed(XUiController sender, int mouseButton)
    {
        DefaultVoiceOptions();
        ApplyStateToWorking(new RebirthSandboxState());
        MarkAsNewPresetWhenEditingBuiltIn();
        RefreshOptionValue();
        PublishWorkingState();
        IsDirty = true;
    }

    [XuiBindEvent("OnPress", "btnSave")]
    public void BtnSave_OnPressed(XUiController sender, int mouseButton)
    {
        // Personal audio preferences are independent of the world/preset context.
        // Remote server settings may be represented as a new local preset draft.
        // Saving or cancelling that draft must not gate the player's voice choice.
        bool savedVoicePreferences = VoiceHasChanges;
        if (savedVoicePreferences) SaveVoiceOptions();
        if (!IsEditContextCurrent()) { Log.Warning("[RebirthSandbox] Save rejected because the world/preset edit context changed while the editor was open."); return; }
        if (savedVoicePreferences && !HasChanges) return;
        RebirthSandboxPreset selected = RebirthSandboxOptionManager.Current.GetPreset(workingPresetName);
        if (selected == null)
            return;

        if (workingAsNewPreset)
        {
            XUiC_RebirthSandboxSaveAsPreset.Open(xui, RebirthCode, string.Empty, delegate(string presetName)
            {
                RebirthSandboxOptionManager.Current.ReloadPresets();
                RebirthSandboxPreset saved = RebirthSandboxOptionManager.Current.GetPreset(presetName);
                if (saved == null)
                    return;

                workingPresetName = saved.Name;
                SaveVoiceOptions();
                selectedPresetBaselineCode = saved.Code;
                workingAsNewPreset = false;
                RefreshPresetSelectors();
                RebirthSandboxUiSession.SetFromPreset(saved);
                CaptureSessionRollbackPoint();
                IsDirty = true;
            });
            return;
        }

        if (!selected.IsUserPreset || !HasChanges)
            return;

        RebirthSandboxPreset updated = RebirthSandboxOptionManager.Current.UpdateUserPreset(
            selected.Name,
            selected.Description,
            RebirthCode);
        if (updated == null)
            return;

        workingPresetName = updated.Name;
        SaveVoiceOptions();
        selectedPresetBaselineCode = updated.Code;
        workingAsNewPreset = false;
        RefreshPresetSelectors();
        RebirthSandboxUiSession.SetFromPreset(updated);
        CaptureSessionRollbackPoint();
        IsDirty = true;
    }

    [XuiBindEvent("OnPress", "btnCopyCode")]
    public void BtnCopyCode_OnPressed(XUiController sender, int mouseButton)
    {
        GUIUtility.systemCopyBuffer = RebirthCode;
    }

    [XuiBindEvent("OnPress", "btnPasteCode")]
    public void BtnPasteCode_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthSandboxState state;
        if (!RebirthSandboxOptionManager.TryDecode(GUIUtility.systemCopyBuffer, out state))
            return;

        ApplyStateToWorking(state);
        MarkAsNewPresetWhenEditingBuiltIn();
        RefreshOptionValue();
        RefreshPresetSelectors();
        PublishWorkingState();
        IsDirty = true;
    }

    [XuiBindEvent("OnPress", "btnRandomize")]
    public void BtnRandomize_OnPressed(XUiController sender, int mouseButton)
    {
        RandomizeVoiceOptions();
        bool changed = false;

        changed |= RandomizeEnumOption(RebirthSandboxOptionId.SpawnProgression, ref workingSpawnProgression,
            RebirthSpawnProgressionMode.Biome, RebirthSpawnProgressionMode.Gamestage);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.AdvancedFarming, ref workingAdvancedFarming);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.AutoReplantTrees, ref workingAutoReplantTrees);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.VehicleBlockRespawn,
            ref workingVehicleBlockRespawnDays, VehicleBlockRespawnRandomizerValues);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.HybridPathSmoothing, ref workingHybridPathSmoothing);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.ZombiesDestroyAreas, ref workingZombiesDestroyAreas);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.RancherRangedAttack, ref workingRancherRangedAttack);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.SleeperRespawns, ref workingSleeperRespawns);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.SleeperSpawnMultiplier,
            ref workingSleeperSpawnMultiplier, SleeperSpawnMultiplierRandomizerValues);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.InfestedSleeperSpawnMultiplier,
            ref workingInfestedSleeperSpawnMultiplier, InfestedSleeperSpawnMultiplierRandomizerValues);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.SuppressConsoleErrorPopups,
            ref workingSuppressConsoleErrorPopups);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.TreeDensityMultiplier,
            ref workingTreeDensityMultiplier, RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.VehicleDensityMultiplier,
            ref workingVehicleDensityMultiplier, RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.BlocksCatchFire, ref workingBlocksCatchFire);
        if (workingBlocksCatchFire)
        {
            changed |= RandomizeBooleanOption(
                RebirthSandboxOptionId.FireAffectsHeatmap,
                ref workingFireAffectsHeatmap);
            changed |= RandomizeEnumOption(
                RebirthSandboxOptionId.FireBlockDamageSpeed,
                ref workingFireBlockDamageSpeed,
                RebirthFireBlockDamageSpeed.Slower,
                RebirthFireBlockDamageSpeed.Faster);
        }
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.MaxJobs,
            ref workingMaxJobs, RebirthTraderJobPolicy.AllowedMaxJobs);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.JobsToNextTier,
            ref workingJobsToNextTier, RebirthTraderJobPolicy.AllowedJobsToNextTier);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.RepeatPoiJobs, ref workingRepeatPoiJobs,
            RebirthRepeatPoiPolicy.None, RebirthRepeatPoiPolicy.Unlimited);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.InfestedJobs, ref workingInfestedJobs,
            RebirthInfestedJobsMode.Default, RebirthInfestedJobsMode.Surprise);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.TraderJobList, ref workingTraderJobList,
            RebirthTraderJobListMode.Fixed, RebirthTraderJobListMode.Random);
        changed |= RandomizeBooleanOption(
            RebirthSandboxOptionId.TraderJobRecoveryGrace,
            ref workingTraderJobRecoveryGrace);
        if (workingTraderJobRecoveryGrace)
        {
            changed |= RandomizeEnumOption(
                RebirthSandboxOptionId.TraderJobRecoveryGraceDuration,
                ref workingTraderJobRecoveryGraceDuration,
                RebirthTraderJobRecoveryGraceDuration.FiveMinutes,
                RebirthTraderJobRecoveryGraceDuration.FifteenMinutes);
        }
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.PreventHealingOverlap, ref workingPreventHealingOverlap,
            RebirthHealingOverlapThreshold.Off, RebirthHealingOverlapThreshold.Health30);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.AlwaysStagger, ref workingAlwaysStagger,
            RebirthAlwaysStaggerMode.Disabled, RebirthAlwaysStaggerMode.AllQualifyingHits);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.WeatherFogBehavior, ref workingWeatherFogBehavior,
            RebirthWeatherFogBehavior.Dynamic, RebirthWeatherFogBehavior.Disabled);
        if (RebirthSandboxOptionDependencyPolicy.IsWeatherFogIntensityEnabled(workingWeatherFogBehavior))
            changed |= RandomizeEnumOption(RebirthSandboxOptionId.WeatherFogIntensity, ref workingWeatherFogIntensity,
                RebirthWeatherFogIntensity.None, RebirthWeatherFogIntensity.VeryHeavy);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.UniformAtmosphere, ref workingUniformAtmosphere);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.PitchBlack, ref workingPitchBlack);
        // Disabled dependent options are not randomized. Their configured values stay
        // untouched and become active again when the parent constraint is removed.
        if (RebirthSandboxOptionDependencyPolicy.IsPoiRiskEnabled(workingSpawnProgression))
            changed |= RandomizeEnumOption(RebirthSandboxOptionId.PoiRisk, ref workingPoiRisk,
                RebirthPoiRiskMode.None, RebirthPoiRiskMode.High);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.PoiSenseSchedule, ref workingPoiSenseSchedule,
            RebirthPoiSenseSchedule.Never, RebirthPoiSenseSchedule.Always);
        if (RebirthSandboxOptionDependencyPolicy.IsPoiSenseIntensityEnabled(workingPoiSenseSchedule))
            changed |= RandomizeEnumOption(RebirthSandboxOptionId.PoiSenseIntensity, ref workingPoiSenseIntensity,
                RebirthPoiSenseIntensity.Low, RebirthPoiSenseIntensity.High);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.LootTraderAreas, ref workingLootTraderAreas);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.InstantBlockPickup, ref workingInstantBlockPickup,
            RebirthInstantBlockPickupMode.None, RebirthInstantBlockPickupMode.Always);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.GuardDistance, ref workingGuardDistance, RebirthCompanionDistancePolicy.GuardAllowedDistances);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.FullControlDistance, ref workingFullControlDistance, RebirthCompanionDistancePolicy.FullControlAllowedDistances);
        changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.HuntingDistance, ref workingHuntingDistance, RebirthCompanionDistancePolicy.HuntingAllowedDistances);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.QuickStack, ref workingQuickStack,
            RebirthQuickStackMode.Off, RebirthQuickStackMode.Full);
        if (workingQuickStack != RebirthQuickStackMode.Off)
            changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.QuickStackDistance,
                ref workingQuickStackDistance, RebirthResourceDistancePolicy.QuickStackAllowedDistances);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.RemoteResources, ref workingRemoteResources);
        if (workingRemoteResources)
            changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.RemoteResourcesDistance,
                ref workingRemoteResourcesDistance, RebirthResourceDistancePolicy.RemoteResourcesAllowedDistances);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.SecureAccessSharing, ref workingSecureAccessSharing,
            RebirthSecureAccessMode.PinAccessList, RebirthSecureAccessMode.Party);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.HornActivatedDoors, ref workingHornActivatedDoors,
            RebirthHornActivatedDoorsMode.None, RebirthHornActivatedDoorsMode.All);
        changed |= RandomizeBooleanOption(RebirthSandboxOptionId.ProtectCrate, ref workingProtectCrate);
        changed |= RandomizeEnumOption(RebirthSandboxOptionId.BossEvents, ref workingBossEventStartLevel, RebirthBossEventStartLevel.Off, RebirthBossEventStartLevel.PlayerLevel30);
        if (RebirthBossEventStartLevelPolicy.IsEnabled(workingBossEventStartLevel))
        {
            changed |= RandomizeEnumOption(RebirthSandboxOptionId.BossEventFrequency, ref workingBossEventFrequency,
                RebirthBossEventFrequency.Rare, RebirthBossEventFrequency.VeryHigh);
            changed |= RandomizeIndexedIntegerOption(RebirthSandboxOptionId.BossEventMaximumPerDay,
                ref workingBossEventMaximumPerDay, BossEventMaximumPerDayRandomizerValues);
            changed |= RandomizeEnumOption(RebirthSandboxOptionId.BossEventSize, ref workingBossEventSize,
                RebirthBossEventSize.Small, RebirthBossEventSize.Random);
            changed |= RandomizeEnumOption(RebirthSandboxOptionId.BossEventDifficulty, ref workingBossEventDifficulty,
                RebirthBossEventDifficulty.Low, RebirthBossEventDifficulty.Random);
            changed |= RandomizeEnumOption(RebirthSandboxOptionId.BossEventTime, ref workingBossEventTime,
                RebirthBossEventTime.DayOnly, RebirthBossEventTime.NightOnly);
            changed |= RandomizeBooleanOption(RebirthSandboxOptionId.BossEventBloodMoonDay,
                ref workingBossEventBloodMoonDay);
            changed |= RandomizeEnumOption(RebirthSandboxOptionId.BossEventRestriction, ref workingBossEventRestriction,
                RebirthBossEventRestriction.Anywhere, RebirthBossEventRestriction.OutdoorsAndNotQuesting);
            changed |= RandomizeBooleanOption(RebirthSandboxOptionId.BossEventRewards, ref workingBossEventRewards);
            changed |= RandomizeBooleanOption(RebirthSandboxOptionId.BossEventNotifications,
                ref workingBossEventNotifications);
        }

        if (!changed)
            return;

        MarkAsNewPresetWhenEditingBuiltIn();
        RefreshOptionValue();
        PublishWorkingState();
        RefreshBindings();
        IsDirty = true;
    }

    [XuiBindEvent("OnPress", "btnCreatePreset")]
    public void BtnCreatePreset_OnPressed(XUiController sender, int mouseButton)
    {
        // There is intentionally no visible Custom pseudo-preset. Open the naming
        // dialog immediately and create a real User preset from REBIRTH defaults.
        string defaultCode = RebirthSandboxOptionManager.Encode(new RebirthSandboxState());
        OpenSaveAsPreset(defaultCode);
    }

    [XuiBindEvent("OnPress", "btnCopy")]
    public void BtnCopy_OnPressed(XUiController sender, int mouseButton)
    {
        // Duplicate also creates a real named User preset immediately. Entering an
        // invisible draft state here would give the user no visible feedback.
        OpenSaveAsPreset(RebirthCode);
    }

    [XuiBindEvent("OnPress", "btnDelete")]
    public void BtnDelete_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthSandboxPreset preset = RebirthSandboxOptionManager.Current.GetPreset(workingPresetName);
        if (preset == null || !preset.IsUserPreset)
            return;

        if (!RebirthSandboxOptionManager.Current.DeleteUserPreset(preset.Name))
            return;

        List<RebirthSandboxPreset> remainingUserPresets =
            RebirthSandboxOptionManager.Current.GetPresetsForGroup(RebirthSandboxOptionManager.UserGroupName);
        RebirthSandboxPreset nextPreset = remainingUserPresets.Count > 0
            ? remainingUserPresets[0]
            : RebirthSandboxOptionManager.Current.GetPreset(RebirthSandboxOptionManager.DefaultPresetName);
        ApplyPreset(nextPreset);
        RefreshPresetSelectors();
    }

    private void BindComboEvents()
    {
        if (comboEventsBound)
            return;

        if (cbxPresetGroup != null)
            cbxPresetGroup.OnValueChanged += PresetGroup_OnValueChanged;
        if (cbxPreset != null)
            cbxPreset.OnValueChanged += Preset_OnValueChanged;
        if (cbxPlayerProgression != null)
            cbxPlayerProgression.OnValueChanged += PlayerProgression_OnValueChanged;
        if (cbxSpawnProgression != null)
            cbxSpawnProgression.OnValueChanged += SpawnProgression_OnValueChanged;
        if (cbxAdvancedFarming != null)
            cbxAdvancedFarming.OnValueChanged += AdvancedFarming_OnValueChanged;
        if (cbxGuardDistance != null) cbxGuardDistance.OnValueChanged += GuardDistance_OnValueChanged;
        if (cbxFullControlDistance != null) cbxFullControlDistance.OnValueChanged += FullControlDistance_OnValueChanged;
        if (cbxHuntingDistance != null) cbxHuntingDistance.OnValueChanged += HuntingDistance_OnValueChanged;
        if (cbxCompanionCardStyle != null) cbxCompanionCardStyle.OnValueChanged += CompanionCardStyle_OnValueChanged;
        if(cbxScrollbarMode!=null)cbxScrollbarMode.OnValueChanged+=ScrollbarMode_OnValueChanged;
        if(cbxRequireTimedReading!=null)cbxRequireTimedReading.OnValueChanged+=RequireTimedReading_OnValueChanged;
        if (cbxLiteratureStudyTime != null) cbxLiteratureStudyTime.OnValueChanged += LiteratureStudyTime_OnValueChanged;
        if (cbxBlocksCatchFire != null)
            cbxBlocksCatchFire.OnValueChanged += BlocksCatchFire_OnValueChanged;
        if (cbxFireAffectsHeatmap != null)
            cbxFireAffectsHeatmap.OnValueChanged += FireAffectsHeatmap_OnValueChanged;
        if (cbxFireBlockDamageSpeed != null)
            cbxFireBlockDamageSpeed.OnValueChanged += FireBlockDamageSpeed_OnValueChanged;
        if (cbxMaxJobs != null) cbxMaxJobs.OnValueChanged += MaxJobs_OnValueChanged;
        if (cbxJobsToNextTier != null) cbxJobsToNextTier.OnValueChanged += JobsToNextTier_OnValueChanged;
        if (cbxRepeatPoiJobs != null) cbxRepeatPoiJobs.OnValueChanged += RepeatPoiJobs_OnValueChanged;
        if (cbxInfestedJobs != null) cbxInfestedJobs.OnValueChanged += InfestedJobs_OnValueChanged;
        if (cbxTraderJobList != null) cbxTraderJobList.OnValueChanged += TraderJobList_OnValueChanged;
        if (cbxTraderJobRecoveryGrace != null) cbxTraderJobRecoveryGrace.OnValueChanged += TraderJobRecoveryGrace_OnValueChanged;
        if (cbxTraderJobRecoveryGraceDuration != null) cbxTraderJobRecoveryGraceDuration.OnValueChanged += TraderJobRecoveryGraceDuration_OnValueChanged;
        if (cbxPreventHealingOverlap != null) cbxPreventHealingOverlap.OnValueChanged += PreventHealingOverlap_OnValueChanged;
        if (cbxAlwaysStagger != null) cbxAlwaysStagger.OnValueChanged += AlwaysStagger_OnValueChanged;
        if (cbxWeatherFogBehavior != null) cbxWeatherFogBehavior.OnValueChanged += WeatherFogBehavior_OnValueChanged;
        if (cbxWeatherFogIntensity != null) cbxWeatherFogIntensity.OnValueChanged += WeatherFogIntensity_OnValueChanged;
        if (cbxUniformAtmosphere != null) cbxUniformAtmosphere.OnValueChanged += UniformAtmosphere_OnValueChanged;
        if (cbxPitchBlack != null) cbxPitchBlack.OnValueChanged += PitchBlack_OnValueChanged;
        if (cbxPoiRisk != null) cbxPoiRisk.OnValueChanged += PoiRisk_OnValueChanged;
        if (cbxPoiSenseSchedule != null) cbxPoiSenseSchedule.OnValueChanged += PoiSenseSchedule_OnValueChanged;
        if (cbxPoiSenseIntensity != null) cbxPoiSenseIntensity.OnValueChanged += PoiSenseIntensity_OnValueChanged;
        if (cbxAutoReplantTrees != null)
            cbxAutoReplantTrees.OnValueChanged += AutoReplantTrees_OnValueChanged;
        if (cbxVehicleBlockRespawn != null) cbxVehicleBlockRespawn.OnValueChanged += VehicleBlockRespawn_OnValueChanged;
        if (cbxHybridPathSmoothing != null) cbxHybridPathSmoothing.OnValueChanged += HybridPathSmoothing_OnValueChanged;
        if (cbxZombiesDestroyAreas != null) cbxZombiesDestroyAreas.OnValueChanged += ZombiesDestroyAreas_OnValueChanged;
        if (cbxRancherRangedAttack != null) cbxRancherRangedAttack.OnValueChanged += RancherRangedAttack_OnValueChanged;
        if (cbxSleeperRespawns != null) cbxSleeperRespawns.OnValueChanged += SleeperRespawns_OnValueChanged;
        if (cbxSleeperSpawnMultiplier != null) cbxSleeperSpawnMultiplier.OnValueChanged += SleeperSpawnMultiplier_OnValueChanged;
        if (cbxInfestedSleeperSpawnMultiplier != null) cbxInfestedSleeperSpawnMultiplier.OnValueChanged += InfestedSleeperSpawnMultiplier_OnValueChanged;
        if (cbxSuppressConsoleErrorPopups != null) cbxSuppressConsoleErrorPopups.OnValueChanged += SuppressConsoleErrorPopups_OnValueChanged;
        if (cbxTreeDensityMultiplier != null) cbxTreeDensityMultiplier.OnValueChanged += TreeDensityMultiplier_OnValueChanged;
        if (cbxVehicleDensityMultiplier != null) cbxVehicleDensityMultiplier.OnValueChanged += VehicleDensityMultiplier_OnValueChanged;
        if (cbxLootTraderAreas != null) cbxLootTraderAreas.OnValueChanged += LootTraderAreas_OnValueChanged;
        if (cbxInstantBlockPickup != null)
            cbxInstantBlockPickup.OnValueChanged += InstantBlockPickup_OnValueChanged;
        if (cbxRemoteResources != null)
            cbxRemoteResources.OnValueChanged += RemoteResources_OnValueChanged;
        if (cbxRemoteResourcesDistance != null)
            cbxRemoteResourcesDistance.OnValueChanged += RemoteResourcesDistance_OnValueChanged;
        if (cbxSecureAccessSharing != null)
            cbxSecureAccessSharing.OnValueChanged += SecureAccessSharing_OnValueChanged;
        if (cbxHornActivatedDoors != null)
            cbxHornActivatedDoors.OnValueChanged += HornActivatedDoors_OnValueChanged;
        if (cbxQuickStack != null)
            cbxQuickStack.OnValueChanged += QuickStack_OnValueChanged;
        if (cbxQuickStackDistance != null)
            cbxQuickStackDistance.OnValueChanged += QuickStackDistance_OnValueChanged;

        if (cbxProtectCrate != null) cbxProtectCrate.OnValueChanged += ProtectCrate_OnValueChanged;
        if (cbxBossEvents != null) cbxBossEvents.OnValueChanged += BossEvents_OnValueChanged;
        if (cbxBossEventFrequency != null) cbxBossEventFrequency.OnValueChanged += BossEventFrequency_OnValueChanged;
        if (cbxBossEventMaximumPerDay != null) cbxBossEventMaximumPerDay.OnValueChanged += BossEventMaximumPerDay_OnValueChanged;
        if (cbxBossEventSize != null) cbxBossEventSize.OnValueChanged += BossEventSize_OnValueChanged;
        if (cbxBossEventDifficulty != null) cbxBossEventDifficulty.OnValueChanged += BossEventDifficulty_OnValueChanged;
        if (cbxBossEventTime != null) cbxBossEventTime.OnValueChanged += BossEventTime_OnValueChanged;
        if (cbxBossEventBloodMoonDay != null) cbxBossEventBloodMoonDay.OnValueChanged += BossEventBloodMoonDay_OnValueChanged;
        if (cbxBossEventRestriction != null) cbxBossEventRestriction.OnValueChanged += BossEventRestriction_OnValueChanged;
        if (cbxBossEventRewards != null) cbxBossEventRewards.OnValueChanged += BossEventRewards_OnValueChanged;
        if (cbxBossEventNotifications != null) cbxBossEventNotifications.OnValueChanged += BossEventNotifications_OnValueChanged;

        comboEventsBound = true;
    }

    private void UnbindComboEvents()
    {
        if (!comboEventsBound)
            return;

        if (cbxPresetGroup != null)
            cbxPresetGroup.OnValueChanged -= PresetGroup_OnValueChanged;
        if (cbxPreset != null)
            cbxPreset.OnValueChanged -= Preset_OnValueChanged;
        if (cbxPlayerProgression != null)
            cbxPlayerProgression.OnValueChanged -= PlayerProgression_OnValueChanged;
        if (cbxSpawnProgression != null)
            cbxSpawnProgression.OnValueChanged -= SpawnProgression_OnValueChanged;
        if (cbxAdvancedFarming != null)
            cbxAdvancedFarming.OnValueChanged -= AdvancedFarming_OnValueChanged;
        if (cbxGuardDistance != null) cbxGuardDistance.OnValueChanged -= GuardDistance_OnValueChanged;
        if (cbxFullControlDistance != null) cbxFullControlDistance.OnValueChanged -= FullControlDistance_OnValueChanged;
        if (cbxHuntingDistance != null) cbxHuntingDistance.OnValueChanged -= HuntingDistance_OnValueChanged;
        if (cbxCompanionCardStyle != null) cbxCompanionCardStyle.OnValueChanged -= CompanionCardStyle_OnValueChanged;
        if(cbxScrollbarMode!=null)cbxScrollbarMode.OnValueChanged-=ScrollbarMode_OnValueChanged;
        if(cbxRequireTimedReading!=null)cbxRequireTimedReading.OnValueChanged-=RequireTimedReading_OnValueChanged;
        if (cbxLiteratureStudyTime != null) cbxLiteratureStudyTime.OnValueChanged -= LiteratureStudyTime_OnValueChanged;
        if (cbxBlocksCatchFire != null)
            cbxBlocksCatchFire.OnValueChanged -= BlocksCatchFire_OnValueChanged;
        if (cbxFireAffectsHeatmap != null)
            cbxFireAffectsHeatmap.OnValueChanged -= FireAffectsHeatmap_OnValueChanged;
        if (cbxFireBlockDamageSpeed != null)
            cbxFireBlockDamageSpeed.OnValueChanged -= FireBlockDamageSpeed_OnValueChanged;
        if (cbxMaxJobs != null) cbxMaxJobs.OnValueChanged -= MaxJobs_OnValueChanged;
        if (cbxJobsToNextTier != null) cbxJobsToNextTier.OnValueChanged -= JobsToNextTier_OnValueChanged;
        if (cbxRepeatPoiJobs != null) cbxRepeatPoiJobs.OnValueChanged -= RepeatPoiJobs_OnValueChanged;
        if (cbxInfestedJobs != null) cbxInfestedJobs.OnValueChanged -= InfestedJobs_OnValueChanged;
        if (cbxTraderJobList != null) cbxTraderJobList.OnValueChanged -= TraderJobList_OnValueChanged;
        if (cbxTraderJobRecoveryGrace != null) cbxTraderJobRecoveryGrace.OnValueChanged -= TraderJobRecoveryGrace_OnValueChanged;
        if (cbxTraderJobRecoveryGraceDuration != null) cbxTraderJobRecoveryGraceDuration.OnValueChanged -= TraderJobRecoveryGraceDuration_OnValueChanged;
        if (cbxPreventHealingOverlap != null) cbxPreventHealingOverlap.OnValueChanged -= PreventHealingOverlap_OnValueChanged;
        if (cbxAlwaysStagger != null) cbxAlwaysStagger.OnValueChanged -= AlwaysStagger_OnValueChanged;
        if (cbxWeatherFogBehavior != null) cbxWeatherFogBehavior.OnValueChanged -= WeatherFogBehavior_OnValueChanged;
        if (cbxWeatherFogIntensity != null) cbxWeatherFogIntensity.OnValueChanged -= WeatherFogIntensity_OnValueChanged;
        if (cbxUniformAtmosphere != null) cbxUniformAtmosphere.OnValueChanged -= UniformAtmosphere_OnValueChanged;
        if (cbxPitchBlack != null) cbxPitchBlack.OnValueChanged -= PitchBlack_OnValueChanged;
        if (cbxPoiRisk != null) cbxPoiRisk.OnValueChanged -= PoiRisk_OnValueChanged;
        if (cbxPoiSenseSchedule != null) cbxPoiSenseSchedule.OnValueChanged -= PoiSenseSchedule_OnValueChanged;
        if (cbxPoiSenseIntensity != null) cbxPoiSenseIntensity.OnValueChanged -= PoiSenseIntensity_OnValueChanged;
        if (cbxAutoReplantTrees != null)
            cbxAutoReplantTrees.OnValueChanged -= AutoReplantTrees_OnValueChanged;
        if (cbxVehicleBlockRespawn != null) cbxVehicleBlockRespawn.OnValueChanged -= VehicleBlockRespawn_OnValueChanged;
        if (cbxHybridPathSmoothing != null) cbxHybridPathSmoothing.OnValueChanged -= HybridPathSmoothing_OnValueChanged;
        if (cbxZombiesDestroyAreas != null) cbxZombiesDestroyAreas.OnValueChanged -= ZombiesDestroyAreas_OnValueChanged;
        if (cbxRancherRangedAttack != null) cbxRancherRangedAttack.OnValueChanged -= RancherRangedAttack_OnValueChanged;
        if (cbxSleeperRespawns != null) cbxSleeperRespawns.OnValueChanged -= SleeperRespawns_OnValueChanged;
        if (cbxSleeperSpawnMultiplier != null) cbxSleeperSpawnMultiplier.OnValueChanged -= SleeperSpawnMultiplier_OnValueChanged;
        if (cbxInfestedSleeperSpawnMultiplier != null) cbxInfestedSleeperSpawnMultiplier.OnValueChanged -= InfestedSleeperSpawnMultiplier_OnValueChanged;
        if (cbxSuppressConsoleErrorPopups != null) cbxSuppressConsoleErrorPopups.OnValueChanged -= SuppressConsoleErrorPopups_OnValueChanged;
        if (cbxTreeDensityMultiplier != null) cbxTreeDensityMultiplier.OnValueChanged -= TreeDensityMultiplier_OnValueChanged;
        if (cbxVehicleDensityMultiplier != null) cbxVehicleDensityMultiplier.OnValueChanged -= VehicleDensityMultiplier_OnValueChanged;
        if (cbxLootTraderAreas != null) cbxLootTraderAreas.OnValueChanged -= LootTraderAreas_OnValueChanged;
        if (cbxInstantBlockPickup != null)
            cbxInstantBlockPickup.OnValueChanged -= InstantBlockPickup_OnValueChanged;
        if (cbxRemoteResources != null)
            cbxRemoteResources.OnValueChanged -= RemoteResources_OnValueChanged;
        if (cbxRemoteResourcesDistance != null)
            cbxRemoteResourcesDistance.OnValueChanged -= RemoteResourcesDistance_OnValueChanged;
        if (cbxSecureAccessSharing != null)
            cbxSecureAccessSharing.OnValueChanged -= SecureAccessSharing_OnValueChanged;
        if (cbxHornActivatedDoors != null)
            cbxHornActivatedDoors.OnValueChanged -= HornActivatedDoors_OnValueChanged;
        if (cbxQuickStack != null)
            cbxQuickStack.OnValueChanged -= QuickStack_OnValueChanged;
        if (cbxQuickStackDistance != null)
            cbxQuickStackDistance.OnValueChanged -= QuickStackDistance_OnValueChanged;

        if (cbxProtectCrate != null) cbxProtectCrate.OnValueChanged -= ProtectCrate_OnValueChanged;
        if (cbxBossEvents != null) cbxBossEvents.OnValueChanged -= BossEvents_OnValueChanged;
        if (cbxBossEventFrequency != null) cbxBossEventFrequency.OnValueChanged -= BossEventFrequency_OnValueChanged;
        if (cbxBossEventMaximumPerDay != null) cbxBossEventMaximumPerDay.OnValueChanged -= BossEventMaximumPerDay_OnValueChanged;
        if (cbxBossEventSize != null) cbxBossEventSize.OnValueChanged -= BossEventSize_OnValueChanged;
        if (cbxBossEventDifficulty != null) cbxBossEventDifficulty.OnValueChanged -= BossEventDifficulty_OnValueChanged;
        if (cbxBossEventTime != null) cbxBossEventTime.OnValueChanged -= BossEventTime_OnValueChanged;
        if (cbxBossEventBloodMoonDay != null) cbxBossEventBloodMoonDay.OnValueChanged -= BossEventBloodMoonDay_OnValueChanged;
        if (cbxBossEventRestriction != null) cbxBossEventRestriction.OnValueChanged -= BossEventRestriction_OnValueChanged;
        if (cbxBossEventRewards != null) cbxBossEventRewards.OnValueChanged -= BossEventRewards_OnValueChanged;
        if (cbxBossEventNotifications != null) cbxBossEventNotifications.OnValueChanged -= BossEventNotifications_OnValueChanged;

        comboEventsBound = false;
    }

    private void PopulateOptionValues()
    {
        suppressEvents = true;
        try
        {
            if (cbxPlayerProgression != null)
            {
                cbxPlayerProgression.Elements.Clear();
                cbxPlayerProgression.Elements.Add(Localization.Get("xuiRebirthPlayerProgressionBaseGame"));
                cbxPlayerProgression.Elements.Add(Localization.Get("xuiRebirthPlayerProgressionRebirth"));
                cbxPlayerProgression.SelectedIndex = (int)workingPlayerProgression;
                { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
                    "populate elements=" + cbxPlayerProgression.Elements.Count
                    + " index=" + cbxPlayerProgression.SelectedIndex
                    + " working=" + workingPlayerProgression
                    + " values=" + FormatPlayerProgressionElements()); }
            }

            if(cbxScrollbarMode!=null){cbxScrollbarMode.Elements.Clear();cbxScrollbarMode.Elements.Add(Localization.Get("xuiRebirthScrollbarSmooth"));cbxScrollbarMode.Elements.Add(Localization.Get("xuiRebirthScrollbarPaged"));cbxScrollbarMode.SelectedIndex=(int)workingScrollbarMode;}
            if(cbxRequireTimedReading!=null){cbxRequireTimedReading.Elements.Clear();cbxRequireTimedReading.Elements.Add(Localization.Get("xuiRebirthQuickReading"));cbxRequireTimedReading.Elements.Add(Localization.Get("xuiRebirthTimedReading"));cbxRequireTimedReading.SelectedIndex=workingRequireTimedReading?1:0;}
            if(cbxLiteratureStudyTime!=null)
            {
                cbxLiteratureStudyTime.Elements.Clear();
                cbxLiteratureStudyTime.Elements.Add(Localization.Get("xuiRebirthStudyDurationQuarter"));
                cbxLiteratureStudyTime.Elements.Add(Localization.Get("xuiRebirthStudyDurationHalf"));
                cbxLiteratureStudyTime.Elements.Add(Localization.Get("xuiRebirthStudyDurationThreeQuarters"));
                cbxLiteratureStudyTime.Elements.Add(Localization.Get("xuiRebirthStudyDurationNormal"));
                cbxLiteratureStudyTime.Elements.Add(Localization.Get("xuiRebirthStudyDurationOneAndHalf"));
                cbxLiteratureStudyTime.Elements.Add(Localization.Get("xuiRebirthStudyDurationDouble"));
                cbxLiteratureStudyTime.SelectedIndex=(int)workingLiteratureStudyTime;
            }

            cbxSpawnProgression.Elements.Clear();
            cbxSpawnProgression.Elements.Add(Localization.Get("xuiRebirthSpawnProgressionBiome"));
            cbxSpawnProgression.Elements.Add(Localization.Get("xuiRebirthSpawnProgressionGamestage"));
            cbxSpawnProgression.SelectedIndex = (int)workingSpawnProgression;

            cbxAdvancedFarming.Elements.Clear();
            cbxAdvancedFarming.Elements.Add(Localization.Get("xuiNo"));
            cbxAdvancedFarming.Elements.Add(Localization.Get("xuiYes"));
            cbxAdvancedFarming.SelectedIndex = workingAdvancedFarming ? 1 : 0;
            PopulateDistance(cbxGuardDistance, workingGuardDistance, RebirthCompanionDistancePolicy.GuardAllowedDistances, RebirthCompanionDistancePolicy.GuardDistanceToIndex);
            PopulateDistance(cbxFullControlDistance, workingFullControlDistance, RebirthCompanionDistancePolicy.FullControlAllowedDistances, RebirthCompanionDistancePolicy.FullControlDistanceToIndex);
            PopulateDistance(cbxHuntingDistance, workingHuntingDistance, RebirthCompanionDistancePolicy.HuntingAllowedDistances, RebirthCompanionDistancePolicy.HuntingDistanceToIndex);
            if (cbxCompanionCardStyle != null)
            {
                cbxCompanionCardStyle.Elements.Clear();
                cbxCompanionCardStyle.Elements.Add(Localization.Get("xuiRebirthCompanionCardsDefault"));
                cbxCompanionCardStyle.Elements.Add(Localization.Get("xuiRebirthCompanionCardsSimple"));
                cbxCompanionCardStyle.SelectedIndex = workingCompanionCardStyle == RebirthCompanionCardStyle.Simple ? 1 : 0;
            }
            if (cbxBlocksCatchFire != null)
            {
                cbxBlocksCatchFire.Elements.Clear();
                cbxBlocksCatchFire.Elements.Add(Localization.Get("xuiNo"));
                cbxBlocksCatchFire.Elements.Add(Localization.Get("xuiYes"));
                cbxBlocksCatchFire.SelectedIndex = workingBlocksCatchFire ? 1 : 0;
            }
            if (cbxFireAffectsHeatmap != null)
            {
                cbxFireAffectsHeatmap.Elements.Clear();
                cbxFireAffectsHeatmap.Elements.Add(Localization.Get("xuiNo"));
                cbxFireAffectsHeatmap.Elements.Add(Localization.Get("xuiYes"));
                cbxFireAffectsHeatmap.SelectedIndex = workingFireAffectsHeatmap ? 1 : 0;
            }
            if (cbxFireBlockDamageSpeed != null)
            {
                cbxFireBlockDamageSpeed.Elements.Clear();
                cbxFireBlockDamageSpeed.Elements.Add(Localization.Get("xuiRebirthFireBlockDamageSlower"));
                cbxFireBlockDamageSpeed.Elements.Add(Localization.Get("xuiRebirthFireBlockDamageDefault"));
                cbxFireBlockDamageSpeed.Elements.Add(Localization.Get("xuiRebirthFireBlockDamageFaster"));
                cbxFireBlockDamageSpeed.SelectedIndex = (int)workingFireBlockDamageSpeed;
            }
            if (cbxMaxJobs != null)
            {
                cbxMaxJobs.Elements.Clear();
                for (int i = 0; i < RebirthTraderJobPolicy.AllowedMaxJobs.Length; i++)
                    cbxMaxJobs.Elements.Add(RebirthTraderJobPolicy.AllowedMaxJobs[i].ToString());
                cbxMaxJobs.SelectedIndex = RebirthTraderJobPolicy.MaxJobsToIndex(workingMaxJobs);
            }
            if (cbxJobsToNextTier != null)
            {
                cbxJobsToNextTier.Elements.Clear();
                for (int i = 0; i < RebirthTraderJobPolicy.AllowedJobsToNextTier.Length; i++)
                    cbxJobsToNextTier.Elements.Add(RebirthTraderJobPolicy.AllowedJobsToNextTier[i].ToString());
                cbxJobsToNextTier.SelectedIndex = RebirthTraderJobPolicy.JobsToNextTierToIndex(workingJobsToNextTier);
            }
            if (cbxRepeatPoiJobs != null)
            {
                cbxRepeatPoiJobs.Elements.Clear();
                cbxRepeatPoiJobs.Elements.Add(Localization.Get("xuiRebirthRepeatPoiNone"));
                cbxRepeatPoiJobs.Elements.Add(Localization.Get("xuiRebirthRepeatPoiLow"));
                cbxRepeatPoiJobs.Elements.Add(Localization.Get("xuiRebirthRepeatPoiMedium"));
                cbxRepeatPoiJobs.Elements.Add(Localization.Get("xuiRebirthRepeatPoiHigh"));
                cbxRepeatPoiJobs.Elements.Add(Localization.Get("xuiRebirthRepeatPoiUnlimited"));
                cbxRepeatPoiJobs.SelectedIndex = (int)workingRepeatPoiJobs;
            }
            if (cbxInfestedJobs != null)
            {
                cbxInfestedJobs.Elements.Clear();
                cbxInfestedJobs.Elements.Add(Localization.Get("xuiRebirthInfestedJobsDefault"));
                cbxInfestedJobs.Elements.Add(Localization.Get("xuiRebirthInfestedJobsHide"));
                cbxInfestedJobs.Elements.Add(Localization.Get("xuiRebirthInfestedJobsSurprise"));
                cbxInfestedJobs.SelectedIndex = (int)workingInfestedJobs;
            }
            if (cbxTraderJobList != null)
            {
                cbxTraderJobList.Elements.Clear();
                cbxTraderJobList.Elements.Add(Localization.Get("xuiRebirthTraderJobListFixed"));
                cbxTraderJobList.Elements.Add(Localization.Get("xuiRebirthTraderJobListRandom"));
                cbxTraderJobList.SelectedIndex = (int)workingTraderJobList;
            }
            if (cbxTraderJobRecoveryGrace != null)
            {
                cbxTraderJobRecoveryGrace.Elements.Clear();
                cbxTraderJobRecoveryGrace.Elements.Add(Localization.Get("xuiNo"));
                cbxTraderJobRecoveryGrace.Elements.Add(Localization.Get("xuiYes"));
                cbxTraderJobRecoveryGrace.SelectedIndex =
                    workingTraderJobRecoveryGrace ? 1 : 0;
            }
            if (cbxTraderJobRecoveryGraceDuration != null)
            {
                cbxTraderJobRecoveryGraceDuration.Elements.Clear();
                cbxTraderJobRecoveryGraceDuration.Elements.Add(
                    Localization.Get("xuiRebirthTraderJobRecoveryGrace5"));
                cbxTraderJobRecoveryGraceDuration.Elements.Add(
                    Localization.Get("xuiRebirthTraderJobRecoveryGrace10"));
                cbxTraderJobRecoveryGraceDuration.Elements.Add(
                    Localization.Get("xuiRebirthTraderJobRecoveryGrace15"));
                cbxTraderJobRecoveryGraceDuration.SelectedIndex =
                    (int)workingTraderJobRecoveryGraceDuration;
            }
            if (cbxPreventHealingOverlap != null)
            {
                cbxPreventHealingOverlap.Elements.Clear();
                cbxPreventHealingOverlap.Elements.Add(Localization.Get("xuiOff"));
                cbxPreventHealingOverlap.Elements.Add("10 " + Localization.Get("xuiRebirthHealth"));
                cbxPreventHealingOverlap.Elements.Add("20 " + Localization.Get("xuiRebirthHealth"));
                cbxPreventHealingOverlap.Elements.Add("30 " + Localization.Get("xuiRebirthHealth"));
                cbxPreventHealingOverlap.SelectedIndex = (int)workingPreventHealingOverlap;
            }
            if (cbxAlwaysStagger != null)
            {
                cbxAlwaysStagger.Elements.Clear();
                cbxAlwaysStagger.Elements.Add(Localization.Get("xuiRebirthAlwaysStaggerDisabled"));
                cbxAlwaysStagger.Elements.Add(Localization.Get("xuiRebirthAlwaysStaggerHeadshotsOnly"));
                cbxAlwaysStagger.Elements.Add(Localization.Get("xuiRebirthAlwaysStaggerAllHits"));
                cbxAlwaysStagger.SelectedIndex = (int)workingAlwaysStagger;
            }
            if (cbxWeatherFogBehavior != null)
            {
                cbxWeatherFogBehavior.Elements.Clear();
                cbxWeatherFogBehavior.Elements.Add(Localization.Get("xuiRebirthWeatherFogDynamic"));
                cbxWeatherFogBehavior.Elements.Add(Localization.Get("xuiRebirthWeatherFogStatic"));
                cbxWeatherFogBehavior.Elements.Add(Localization.Get("xuiRebirthWeatherFogDisabled"));
                cbxWeatherFogBehavior.SelectedIndex = (int)workingWeatherFogBehavior;
            }
            if (cbxWeatherFogIntensity != null)
            {
                cbxWeatherFogIntensity.Elements.Clear();
                cbxWeatherFogIntensity.Elements.Add(Localization.Get("xuiRebirthWeatherFogNone"));
                cbxWeatherFogIntensity.Elements.Add(Localization.Get("xuiRebirthWeatherFogVeryLow"));
                cbxWeatherFogIntensity.Elements.Add(Localization.Get("xuiRebirthWeatherFogLow"));
                cbxWeatherFogIntensity.Elements.Add(Localization.Get("xuiRebirthWeatherFogNormal"));
                cbxWeatherFogIntensity.Elements.Add(Localization.Get("xuiRebirthWeatherFogHeavy"));
                cbxWeatherFogIntensity.Elements.Add(Localization.Get("xuiRebirthWeatherFogVeryHeavy"));
                cbxWeatherFogIntensity.SelectedIndex = (int)workingWeatherFogIntensity;
            }
            if (cbxUniformAtmosphere != null)
            {
                cbxUniformAtmosphere.Elements.Clear();
                cbxUniformAtmosphere.Elements.Add(Localization.Get("xuiNo"));
                cbxUniformAtmosphere.Elements.Add(Localization.Get("xuiYes"));
                cbxUniformAtmosphere.SelectedIndex = workingUniformAtmosphere ? 1 : 0;
            }
            if (cbxPitchBlack != null)
            {
                cbxPitchBlack.Elements.Clear();
                cbxPitchBlack.Elements.Add(Localization.Get("xuiNo"));
                cbxPitchBlack.Elements.Add(Localization.Get("xuiYes"));
                cbxPitchBlack.SelectedIndex = workingPitchBlack ? 1 : 0;
            }
            if (cbxPoiRisk != null)
            {
                cbxPoiRisk.Elements.Clear();
                cbxPoiRisk.Elements.Add(Localization.Get("xuiRebirthPoiRiskNone"));
                cbxPoiRisk.Elements.Add(Localization.Get("xuiRebirthPoiRiskLow"));
                cbxPoiRisk.Elements.Add(Localization.Get("xuiRebirthPoiRiskMedium"));
                cbxPoiRisk.Elements.Add(Localization.Get("xuiRebirthPoiRiskHigh"));
                cbxPoiRisk.SelectedIndex = (int)GetEffectiveWorkingPoiRisk();
            }
            if (cbxPoiSenseSchedule != null)
            {
                cbxPoiSenseSchedule.Elements.Clear();
                cbxPoiSenseSchedule.Elements.Add(Localization.Get("xuiRebirthPoiSenseNever"));
                cbxPoiSenseSchedule.Elements.Add(Localization.Get("xuiRebirthPoiSenseDayOnly"));
                cbxPoiSenseSchedule.Elements.Add(Localization.Get("xuiRebirthPoiSenseNightOnly"));
                cbxPoiSenseSchedule.Elements.Add(Localization.Get("xuiRebirthPoiSenseAlways"));
                cbxPoiSenseSchedule.SelectedIndex = (int)workingPoiSenseSchedule;
            }
            if (cbxPoiSenseIntensity != null)
            {
                cbxPoiSenseIntensity.Elements.Clear();
                cbxPoiSenseIntensity.Elements.Add(Localization.Get("xuiRebirthPoiSenseLow"));
                cbxPoiSenseIntensity.Elements.Add(Localization.Get("xuiRebirthPoiSenseMedium"));
                cbxPoiSenseIntensity.Elements.Add(Localization.Get("xuiRebirthPoiSenseHigh"));
                cbxPoiSenseIntensity.SelectedIndex = (int)workingPoiSenseIntensity;
            }
            if (cbxAutoReplantTrees != null) cbxAutoReplantTrees.SelectedIndex = workingAutoReplantTrees ? 1 : 0;
            if (cbxHybridPathSmoothing != null) cbxHybridPathSmoothing.SelectedIndex = workingHybridPathSmoothing ? 1 : 0;
            if (cbxZombiesDestroyAreas != null) cbxZombiesDestroyAreas.SelectedIndex = workingZombiesDestroyAreas ? 1 : 0;
            if (cbxRancherRangedAttack != null) cbxRancherRangedAttack.SelectedIndex = workingRancherRangedAttack ? 1 : 0;
            if (cbxSleeperRespawns != null) cbxSleeperRespawns.SelectedIndex = workingSleeperRespawns ? 1 : 0;
            if (cbxSuppressConsoleErrorPopups != null) cbxSuppressConsoleErrorPopups.SelectedIndex = workingSuppressConsoleErrorPopups ? 1 : 0;
            if (cbxLootTraderAreas != null) cbxLootTraderAreas.SelectedIndex = workingLootTraderAreas ? 1 : 0;
            if (cbxVehicleBlockRespawn != null) cbxVehicleBlockRespawn.SelectedIndex = workingVehicleBlockRespawnDays == 3 ? 1 : workingVehicleBlockRespawnDays == 7 ? 2 : workingVehicleBlockRespawnDays == 14 ? 3 : workingVehicleBlockRespawnDays == 21 ? 4 : 0;
        if (cbxAutoReplantTrees != null)
        {
            cbxAutoReplantTrees.Elements.Clear();
            cbxAutoReplantTrees.Elements.Add(Localization.Get("xuiOff"));
            cbxAutoReplantTrees.Elements.Add(Localization.Get("xuiOn"));
            cbxAutoReplantTrees.SelectedIndex = workingAutoReplantTrees ? 1 : 0;
        }
        if (cbxHybridPathSmoothing != null)
        {
            cbxHybridPathSmoothing.Elements.Clear();
            cbxHybridPathSmoothing.Elements.Add(Localization.Get("xuiOff"));
            cbxHybridPathSmoothing.Elements.Add(Localization.Get("xuiOn"));
            cbxHybridPathSmoothing.SelectedIndex = workingHybridPathSmoothing ? 1 : 0;
        }
        if (cbxZombiesDestroyAreas != null)
        {
            cbxZombiesDestroyAreas.Elements.Clear();
            cbxZombiesDestroyAreas.Elements.Add(Localization.Get("xuiOff"));
            cbxZombiesDestroyAreas.Elements.Add(Localization.Get("xuiOn"));
            cbxZombiesDestroyAreas.SelectedIndex = workingZombiesDestroyAreas ? 1 : 0;
        }
        if (cbxRancherRangedAttack != null)
        {
            cbxRancherRangedAttack.Elements.Clear();
            cbxRancherRangedAttack.Elements.Add(Localization.Get("xuiOff"));
            cbxRancherRangedAttack.Elements.Add(Localization.Get("xuiOn"));
            cbxRancherRangedAttack.SelectedIndex = workingRancherRangedAttack ? 1 : 0;
        }
        if (cbxSleeperRespawns != null)
        {
            cbxSleeperRespawns.Elements.Clear();
            cbxSleeperRespawns.Elements.Add(Localization.Get("xuiOff"));
            cbxSleeperRespawns.Elements.Add(Localization.Get("xuiOn"));
            cbxSleeperRespawns.SelectedIndex = workingSleeperRespawns ? 1 : 0;
        }
        if (cbxSleeperSpawnMultiplier != null)
        {
            cbxSleeperSpawnMultiplier.Elements.Clear();
            for (int i = 1; i <= 4; i++) cbxSleeperSpawnMultiplier.Elements.Add(i + "x");
            cbxSleeperSpawnMultiplier.SelectedIndex = workingSleeperSpawnMultiplier - 1;
        }
        if (cbxInfestedSleeperSpawnMultiplier != null)
        {
            cbxInfestedSleeperSpawnMultiplier.Elements.Clear();
            for (int i = 2; i <= 6; i++) cbxInfestedSleeperSpawnMultiplier.Elements.Add(i + "x");
            cbxInfestedSleeperSpawnMultiplier.SelectedIndex = workingInfestedSleeperSpawnMultiplier - 2;
        }
        if (cbxSuppressConsoleErrorPopups != null)
        {
            cbxSuppressConsoleErrorPopups.Elements.Clear();
            cbxSuppressConsoleErrorPopups.Elements.Add(Localization.Get("xuiOff"));
            cbxSuppressConsoleErrorPopups.Elements.Add(Localization.Get("xuiOn"));
            cbxSuppressConsoleErrorPopups.SelectedIndex = workingSuppressConsoleErrorPopups ? 1 : 0;
        }
        PopulateDensityMultiplier(cbxTreeDensityMultiplier, workingTreeDensityMultiplier);
        PopulateDensityMultiplier(cbxVehicleDensityMultiplier, workingVehicleDensityMultiplier);
        if (cbxLootTraderAreas != null)
        {
            cbxLootTraderAreas.Elements.Clear();
            cbxLootTraderAreas.Elements.Add(Localization.Get("xuiOff"));
            cbxLootTraderAreas.Elements.Add(Localization.Get("xuiOn"));
            cbxLootTraderAreas.SelectedIndex = workingLootTraderAreas ? 1 : 0;
        }
        if (cbxVehicleBlockRespawn != null)
        {
            cbxVehicleBlockRespawn.Elements.Clear();
            cbxVehicleBlockRespawn.Elements.Add(Localization.Get("xuiOff"));
            cbxVehicleBlockRespawn.Elements.Add("3 " + Localization.Get("xuiRebirthDays"));
            cbxVehicleBlockRespawn.Elements.Add("7 " + Localization.Get("xuiRebirthDays"));
            cbxVehicleBlockRespawn.Elements.Add("14 " + Localization.Get("xuiRebirthDays"));
            cbxVehicleBlockRespawn.Elements.Add("21 " + Localization.Get("xuiRebirthDays"));
            cbxVehicleBlockRespawn.SelectedIndex = workingVehicleBlockRespawnDays == 3 ? 1 : workingVehicleBlockRespawnDays == 7 ? 2 : workingVehicleBlockRespawnDays == 14 ? 3 : workingVehicleBlockRespawnDays == 21 ? 4 : 0;
        }

            cbxInstantBlockPickup.Elements.Clear();
            cbxInstantBlockPickup.Elements.Add(Localization.Get("xuiRebirthInstantBlockPickupNone"));
            cbxInstantBlockPickup.Elements.Add(Localization.Get("xuiRebirthInstantBlockPickupDefault"));
            cbxInstantBlockPickup.Elements.Add(Localization.Get("xuiRebirthInstantBlockPickupAlways"));
            cbxInstantBlockPickup.SelectedIndex = (int)workingInstantBlockPickup;

            if (cbxRemoteResources != null)
            {
                cbxRemoteResources.Elements.Clear();
                cbxRemoteResources.Elements.Add(Localization.Get("xuiNo"));
                cbxRemoteResources.Elements.Add(Localization.Get("xuiYes"));
                cbxRemoteResources.SelectedIndex = workingRemoteResources ? 1 : 0;
            }

            PopulateDistance(cbxRemoteResourcesDistance, workingRemoteResourcesDistance, RebirthResourceDistancePolicy.RemoteResourcesAllowedDistances, RebirthResourceDistancePolicy.RemoteResourcesDistanceToIndex);

            if (cbxSecureAccessSharing != null)
            {
                cbxSecureAccessSharing.Elements.Clear();
                cbxSecureAccessSharing.Elements.Add(Localization.Get("xuiRebirthSecureAccessPin"));
                cbxSecureAccessSharing.Elements.Add(Localization.Get("xuiRebirthSecureAccessAllies"));
                cbxSecureAccessSharing.Elements.Add(Localization.Get("xuiRebirthSecureAccessParty"));
                cbxSecureAccessSharing.SelectedIndex = (int)workingSecureAccessSharing;
            }

            if (cbxHornActivatedDoors != null)
            {
                cbxHornActivatedDoors.Elements.Clear();
                cbxHornActivatedDoors.Elements.Add(Localization.Get("xuiRebirthHornDoorsNone"));
                cbxHornActivatedDoors.Elements.Add(Localization.Get("xuiRebirthHornDoorsTrader"));
                cbxHornActivatedDoors.Elements.Add(Localization.Get("xuiRebirthHornDoorsAll"));
                cbxHornActivatedDoors.SelectedIndex = (int)workingHornActivatedDoors;
            }

            if (cbxQuickStack != null)
            {
                cbxQuickStack.Elements.Clear();
                cbxQuickStack.Elements.Add(Localization.Get("xuiRebirthQuickStackOff"));
                cbxQuickStack.Elements.Add(Localization.Get("xuiRebirthQuickStackStrict"));
                cbxQuickStack.Elements.Add(Localization.Get("xuiRebirthQuickStackFull"));
                cbxQuickStack.SelectedIndex = (int)workingQuickStack;
            }

            PopulateDistance(cbxQuickStackDistance, workingQuickStackDistance, RebirthResourceDistancePolicy.QuickStackAllowedDistances, RebirthResourceDistancePolicy.QuickStackDistanceToIndex);

            PopulateBoolean(cbxProtectCrate, workingProtectCrate);
            if (cbxBossEvents != null)
            {
                cbxBossEvents.Elements.Clear();
                cbxBossEvents.Elements.Add(Localization.Get("xuiRebirthBossEventsOff"));
                cbxBossEvents.Elements.Add(Localization.Get("xuiRebirthBossEventsLevel10"));
                cbxBossEvents.Elements.Add(Localization.Get("xuiRebirthBossEventsLevel15"));
                cbxBossEvents.Elements.Add(Localization.Get("xuiRebirthBossEventsLevel20"));
                cbxBossEvents.Elements.Add(Localization.Get("xuiRebirthBossEventsLevel25"));
                cbxBossEvents.Elements.Add(Localization.Get("xuiRebirthBossEventsLevel30"));
                cbxBossEvents.SelectedIndex = (int)workingBossEventStartLevel;
            }
            PopulateEnum(cbxBossEventFrequency, workingBossEventFrequency, "xuiRebirthBossFrequencyRare", "xuiRebirthBossFrequencyLow", "xuiRebirthBossFrequencyNormal", "xuiRebirthBossFrequencyHigh", "xuiRebirthBossFrequencyVeryHigh");
            if (cbxBossEventMaximumPerDay != null) { cbxBossEventMaximumPerDay.Elements.Clear(); for (int i=1;i<=5;i++) cbxBossEventMaximumPerDay.Elements.Add(i.ToString()); cbxBossEventMaximumPerDay.Elements.Add(Localization.Get("xuiRebirthUnlimited")); cbxBossEventMaximumPerDay.SelectedIndex = workingBossEventMaximumPerDay == 0 ? 5 : Mathf.Clamp(workingBossEventMaximumPerDay - 1, 0, 4); }
            PopulateEnum(cbxBossEventSize, workingBossEventSize, "xuiRebirthBossSizeSmall", "xuiRebirthBossSizeNormal", "xuiRebirthBossSizeLarge", "xuiRebirthRandom");
            PopulateEnum(cbxBossEventDifficulty, workingBossEventDifficulty, "xuiRebirthBossDifficultyLow", "xuiRebirthBossDifficultyNormal", "xuiRebirthBossDifficultyHigh", "xuiRebirthRandom");
            PopulateEnum(cbxBossEventTime, workingBossEventTime, "xuiRebirthBossTimeDay", "xuiRebirthBossTimeAny", "xuiRebirthBossTimeNight");
            PopulateBoolean(cbxBossEventBloodMoonDay, workingBossEventBloodMoonDay);
            PopulateEnum(cbxBossEventRestriction, workingBossEventRestriction, "xuiRebirthBossRestrictionAnywhere", "xuiRebirthBossRestrictionOutdoors", "xuiRebirthBossRestrictionQuesting", "xuiRebirthBossRestrictionNotQuesting", "xuiRebirthBossRestrictionOutdoorsNotQuesting");
            PopulateBoolean(cbxBossEventRewards, workingBossEventRewards);
            PopulateBoolean(cbxBossEventNotifications, workingBossEventNotifications);
        }
        finally
        {
            suppressEvents = false;
        }
    }


    private static void PopulateBoolean(XUiC_ComboBoxList<string> combo, bool value) { if (combo == null) return; combo.Elements.Clear(); combo.Elements.Add(Localization.Get("xuiNo")); combo.Elements.Add(Localization.Get("xuiYes")); combo.SelectedIndex = value ? 1 : 0; }
    private static void PopulateEnum<T>(XUiC_ComboBoxList<string> combo, T value, params string[] keys) where T : struct { if (combo == null) return; combo.Elements.Clear(); for (int i=0;i<keys.Length;i++) combo.Elements.Add(Localization.Get(keys[i])); combo.SelectedIndex = Convert.ToInt32(value, CultureInfo.InvariantCulture); }

    private static void PopulateDistance(XUiC_ComboBoxList<string> combo, int distance, int[] allowed, Func<int, int> toIndex)
    {
        if (combo == null) return;
        combo.Elements.Clear();
        for (int i = 0; i < allowed.Length; i++)
            combo.Elements.Add(allowed[i].ToString(CultureInfo.InvariantCulture) + " m");
        combo.SelectedIndex = toIndex(distance);
    }

    private static void PopulateDensityMultiplier(XUiC_ComboBoxList<string> combo, int multiplier)
    {
        if (combo == null) return;
        combo.Elements.Clear();
        int[] values = RebirthWorldDecorationDensityRuntimePolicy.AllowedMultipliers;
        for (int i = 0; i < values.Length; i++) combo.Elements.Add(values[i] + "%");
        combo.SelectedIndex = RebirthWorldDecorationDensityRuntimePolicy.MultiplierToIndex(multiplier);
    }

    private string FormatPlayerProgressionElements()
    {
        if (cbxPlayerProgression == null) return "<null>";
        try
        {
            string result = "[";
            for (int i = 0; i < cbxPlayerProgression.Elements.Count; i++)
            {
                if (i > 0) result += ",";
                result += i + ":'" + (cbxPlayerProgression.Elements[i] ?? string.Empty) + "'";
            }
            return result + "]";
        }
        catch (Exception ex)
        {
            return "<elements-error:" + ex.GetType().Name + ">";
        }
    }

    private void TracePlayerProgressionState(string source, bool force)
    {
        if (!RebirthLogSettings.CharacterProgressionUiLoggingEnabled) return;

        int index = cbxPlayerProgression != null ? cbxPlayerProgression.SelectedIndex : -999;
        int count = cbxPlayerProgression != null ? cbxPlayerProgression.Elements.Count : -1;
        bool enabled = cbxPlayerProgression != null && cbxPlayerProgression.ViewComponent != null
            && cbxPlayerProgression.ViewComponent.Enabled;
        RebirthPlayerProgressionMode lockedMode;
        bool locked = RebirthSandboxUiSession.TryGetLockedPlayerProgression(out lockedMode);
        string context = RebirthSandboxUiSession.CharacterProgressionDebugContext;

        bool changed = force
            || index != playerProgressionDebugLastIndex
            || count != playerProgressionDebugLastElementCount
            || enabled != playerProgressionDebugLastEnabled
            || locked != playerProgressionDebugLastLocked
            || workingPlayerProgression != playerProgressionDebugLastWorking
            || !string.Equals(context, playerProgressionDebugLastContext, StringComparison.Ordinal);
        if (!changed) return;

        playerProgressionDebugLastIndex = index;
        playerProgressionDebugLastElementCount = count;
        playerProgressionDebugLastEnabled = enabled;
        playerProgressionDebugLastLocked = locked;
        playerProgressionDebugLastWorking = workingPlayerProgression;
        playerProgressionDebugLastContext = context;

        { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
            "STATE source=" + source
            + " selectedIndex=" + index + " elements=" + count
            + " working=" + workingPlayerProgression
            + " locked=" + locked + " lockedMode=" + lockedMode
            + " viewEnabled=" + enabled
            + " values=" + FormatPlayerProgressionElements()
            + " " + context); }
    }

    private RebirthPoiRiskMode GetEffectiveWorkingPoiRisk()
    {
        return RebirthSandboxOptionDependencyPolicy.GetEffectivePoiRisk(
            workingSpawnProgression,
            workingPoiRisk);
    }

    private void MarkWorkingStateChanged()
    {
        MarkAsNewPresetWhenEditingBuiltIn();
        PublishWorkingState();
        IsDirty = true;
        RefreshBindings();
    }

    private void HandleManualOptionChange()
    {
        MarkWorkingStateChanged();
    }

    private void PopulateOptionControls()
    {
        RefreshOptionValue();
    }

    private void RefreshOptionValue()
    {
        suppressEvents = true;
        try
        {
            RebirthPlayerProgressionMode lockedProgression;
            bool progressionLocked = RebirthSandboxUiSession.TryGetLockedPlayerProgression(out lockedProgression);
            if (progressionLocked) workingPlayerProgression = lockedProgression;
            if (cbxPlayerProgression != null)
            {
                cbxPlayerProgression.SelectedIndex = (int)workingPlayerProgression;
                if (cbxPlayerProgression.ViewComponent != null) cbxPlayerProgression.ViewComponent.Enabled = !progressionLocked;
                { if (RebirthLogSettings.CharacterProgressionUiLoggingEnabled) RebirthLogSettings.TraceCharacterProgression(
                    "refresh progressionLocked=" + progressionLocked
                    + " lockedProgression=" + lockedProgression
                    + " index=" + cbxPlayerProgression.SelectedIndex
                    + " working=" + workingPlayerProgression
                    + " enabled=" + (cbxPlayerProgression.ViewComponent == null ? "<no-view>" : cbxPlayerProgression.ViewComponent.Enabled.ToString())
                    + " " + RebirthSandboxUiSession.CharacterProgressionDebugContext); }
            }
            if (cbxSpawnProgression != null) cbxSpawnProgression.SelectedIndex = (int)workingSpawnProgression;
            if (cbxAdvancedFarming != null) cbxAdvancedFarming.SelectedIndex = workingAdvancedFarming ? 1 : 0;
            if (cbxGuardDistance != null) cbxGuardDistance.SelectedIndex = RebirthCompanionDistancePolicy.GuardDistanceToIndex(workingGuardDistance);
            if (cbxFullControlDistance != null) cbxFullControlDistance.SelectedIndex = RebirthCompanionDistancePolicy.FullControlDistanceToIndex(workingFullControlDistance);
            if (cbxHuntingDistance != null) cbxHuntingDistance.SelectedIndex = RebirthCompanionDistancePolicy.HuntingDistanceToIndex(workingHuntingDistance);
            if (cbxCompanionCardStyle != null) cbxCompanionCardStyle.SelectedIndex = workingCompanionCardStyle == RebirthCompanionCardStyle.Simple ? 1 : 0;
            if (cbxAutoReplantTrees != null) cbxAutoReplantTrees.SelectedIndex = workingAutoReplantTrees ? 1 : 0;
            if (cbxVehicleBlockRespawn != null) cbxVehicleBlockRespawn.SelectedIndex = GetValueIndex(VehicleBlockRespawnRandomizerValues, workingVehicleBlockRespawnDays, 0);
            if (cbxHybridPathSmoothing != null) cbxHybridPathSmoothing.SelectedIndex = workingHybridPathSmoothing ? 1 : 0;
            if (cbxZombiesDestroyAreas != null) cbxZombiesDestroyAreas.SelectedIndex = workingZombiesDestroyAreas ? 1 : 0;
            if (cbxRancherRangedAttack != null) cbxRancherRangedAttack.SelectedIndex = workingRancherRangedAttack ? 1 : 0;
            if (cbxSleeperRespawns != null) cbxSleeperRespawns.SelectedIndex = workingSleeperRespawns ? 1 : 0;
            if (cbxSleeperSpawnMultiplier != null) cbxSleeperSpawnMultiplier.SelectedIndex = GetValueIndex(SleeperSpawnMultiplierRandomizerValues, workingSleeperSpawnMultiplier, 0);
            if (cbxInfestedSleeperSpawnMultiplier != null) cbxInfestedSleeperSpawnMultiplier.SelectedIndex = GetValueIndex(InfestedSleeperSpawnMultiplierRandomizerValues, workingInfestedSleeperSpawnMultiplier, 0);
            if (cbxSuppressConsoleErrorPopups != null) cbxSuppressConsoleErrorPopups.SelectedIndex = workingSuppressConsoleErrorPopups ? 1 : 0;
            if (cbxTreeDensityMultiplier != null) cbxTreeDensityMultiplier.SelectedIndex = RebirthWorldDecorationDensityRuntimePolicy.MultiplierToIndex(workingTreeDensityMultiplier);
            if (cbxVehicleDensityMultiplier != null) cbxVehicleDensityMultiplier.SelectedIndex = RebirthWorldDecorationDensityRuntimePolicy.MultiplierToIndex(workingVehicleDensityMultiplier);
            if (cbxBlocksCatchFire != null) cbxBlocksCatchFire.SelectedIndex = workingBlocksCatchFire ? 1 : 0;
            if (cbxFireAffectsHeatmap != null) cbxFireAffectsHeatmap.SelectedIndex = workingFireAffectsHeatmap ? 1 : 0;
            if (cbxFireBlockDamageSpeed != null) cbxFireBlockDamageSpeed.SelectedIndex = (int)workingFireBlockDamageSpeed;
            if (cbxMaxJobs != null) cbxMaxJobs.SelectedIndex = RebirthTraderJobPolicy.MaxJobsToIndex(workingMaxJobs);
            if (cbxJobsToNextTier != null) cbxJobsToNextTier.SelectedIndex = RebirthTraderJobPolicy.JobsToNextTierToIndex(workingJobsToNextTier);
            if (cbxRepeatPoiJobs != null) cbxRepeatPoiJobs.SelectedIndex = (int)workingRepeatPoiJobs;
            if (cbxInfestedJobs != null) cbxInfestedJobs.SelectedIndex = (int)workingInfestedJobs;
            if (cbxTraderJobList != null) cbxTraderJobList.SelectedIndex = (int)workingTraderJobList;
            if (cbxTraderJobRecoveryGrace != null) cbxTraderJobRecoveryGrace.SelectedIndex = workingTraderJobRecoveryGrace ? 1 : 0;
            if (cbxTraderJobRecoveryGraceDuration != null) cbxTraderJobRecoveryGraceDuration.SelectedIndex = (int)workingTraderJobRecoveryGraceDuration;
            if (cbxPreventHealingOverlap != null) cbxPreventHealingOverlap.SelectedIndex = (int)workingPreventHealingOverlap;
            if (cbxAlwaysStagger != null) cbxAlwaysStagger.SelectedIndex = (int)workingAlwaysStagger;
            if (cbxWeatherFogBehavior != null) cbxWeatherFogBehavior.SelectedIndex = (int)workingWeatherFogBehavior;
            if (cbxWeatherFogIntensity != null) cbxWeatherFogIntensity.SelectedIndex = (int)workingWeatherFogIntensity;
            if (cbxUniformAtmosphere != null) cbxUniformAtmosphere.SelectedIndex = workingUniformAtmosphere ? 1 : 0;
            if (cbxPitchBlack != null) cbxPitchBlack.SelectedIndex = workingPitchBlack ? 1 : 0;
            if (cbxPoiRisk != null) cbxPoiRisk.SelectedIndex = (int)GetEffectiveWorkingPoiRisk();
            if (cbxPoiSenseSchedule != null) cbxPoiSenseSchedule.SelectedIndex = (int)workingPoiSenseSchedule;
            if (cbxPoiSenseIntensity != null) cbxPoiSenseIntensity.SelectedIndex = (int)workingPoiSenseIntensity;
            if (cbxLootTraderAreas != null) cbxLootTraderAreas.SelectedIndex = workingLootTraderAreas ? 1 : 0;
            if (cbxInstantBlockPickup != null) cbxInstantBlockPickup.SelectedIndex = (int)workingInstantBlockPickup;
            if (cbxRemoteResources != null) cbxRemoteResources.SelectedIndex = workingRemoteResources ? 1 : 0;
            if (cbxRemoteResourcesDistance != null) cbxRemoteResourcesDistance.SelectedIndex = RebirthResourceDistancePolicy.RemoteResourcesDistanceToIndex(workingRemoteResourcesDistance);
            if (cbxSecureAccessSharing != null) cbxSecureAccessSharing.SelectedIndex = (int)workingSecureAccessSharing;
            if (cbxHornActivatedDoors != null) cbxHornActivatedDoors.SelectedIndex = (int)workingHornActivatedDoors;
            if (cbxQuickStack != null) cbxQuickStack.SelectedIndex = (int)workingQuickStack;
            if (cbxQuickStackDistance != null) cbxQuickStackDistance.SelectedIndex = RebirthResourceDistancePolicy.QuickStackDistanceToIndex(workingQuickStackDistance);
            if (cbxProtectCrate != null) cbxProtectCrate.SelectedIndex = workingProtectCrate ? 1 : 0;
            if (cbxBossEvents != null) cbxBossEvents.SelectedIndex = (int)workingBossEventStartLevel;
            if (cbxBossEventFrequency != null) cbxBossEventFrequency.SelectedIndex = (int)workingBossEventFrequency;
            if (cbxBossEventMaximumPerDay != null) cbxBossEventMaximumPerDay.SelectedIndex = GetValueIndex(BossEventMaximumPerDayRandomizerValues, workingBossEventMaximumPerDay, 1);
            if (cbxBossEventSize != null) cbxBossEventSize.SelectedIndex = (int)workingBossEventSize;
            if (cbxBossEventDifficulty != null) cbxBossEventDifficulty.SelectedIndex = (int)workingBossEventDifficulty;
            if (cbxBossEventTime != null) cbxBossEventTime.SelectedIndex = (int)workingBossEventTime;
            if (cbxBossEventBloodMoonDay != null) cbxBossEventBloodMoonDay.SelectedIndex = workingBossEventBloodMoonDay ? 1 : 0;
            if (cbxBossEventRestriction != null) cbxBossEventRestriction.SelectedIndex = (int)workingBossEventRestriction;
            if (cbxBossEventRewards != null) cbxBossEventRewards.SelectedIndex = workingBossEventRewards ? 1 : 0;
            if (cbxBossEventNotifications != null) cbxBossEventNotifications.SelectedIndex = workingBossEventNotifications ? 1 : 0;
        }
        finally
        {
            suppressEvents = false;
        }
    }

    private void RefreshPresetSelectors()
    {
        RebirthSandboxPreset selected = RebirthSandboxOptionManager.Current.GetPreset(workingPresetName);
        if (selected == null)
            selected = RebirthSandboxOptionManager.Current.ResolvePreset(workingPresetName, RebirthCode);
        if (selected == null)
            selected = RebirthSandboxOptionManager.Current.GetPreset(RebirthSandboxOptionManager.DefaultPresetName);

        suppressEvents = true;
        try
        {
            groupIds.Clear();
            cbxPresetGroup.Elements.Clear();
            List<string> groups = RebirthSandboxOptionManager.Current.GetAllPresetGroups();
            for (int i = 0; i < groups.Count; i++)
            {
                groupIds.Add(groups[i]);
                cbxPresetGroup.Elements.Add(RebirthSandboxOptionManager.GetGroupDisplayName(groups[i]));
            }

            int groupIndex = selected != null ? groupIds.IndexOf(selected.Group) : 0;
            cbxPresetGroup.SelectedIndex = groupIds.Count > 0 ? (groupIndex >= 0 ? groupIndex : 0) : -1;
            string group = selected != null ? selected.Group : (groupIds.Count > 0 ? groupIds[0] : null);
            PopulatePresets(group, selected != null ? selected.Name : null, false);
        }
        finally
        {
            suppressEvents = false;
        }

    }

    private void PopulatePresets(string group, string selectedName, bool applyFirst)
    {
        visiblePresets.Clear();
        cbxPreset.Elements.Clear();
        List<RebirthSandboxPreset> presets = RebirthSandboxOptionManager.Current.GetPresetsForGroup(group);
        for (int i = 0; i < presets.Count; i++)
        {
            visiblePresets.Add(presets[i]);
            cbxPreset.Elements.Add(presets[i].DisplayName);
        }

        int selectedIndex = 0;
        if (!string.IsNullOrEmpty(selectedName))
        {
            for (int i = 0; i < visiblePresets.Count; i++)
            {
                if (string.Equals(visiblePresets[i].Name, selectedName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }
        cbxPreset.SelectedIndex = visiblePresets.Count > 0 ? selectedIndex : -1;

        if (applyFirst && visiblePresets.Count > 0)
            ApplyPreset(visiblePresets[cbxPreset.SelectedIndex]);
    }

    private void ApplyPreset(RebirthSandboxPreset preset)
    {
        if (preset == null)
            return;

        string code = preset.Code;
        RebirthSandboxState state;
        if (!RebirthSandboxOptionManager.TryDecode(code, out state))
            return;

        ApplyStateToWorking(state);
        workingPresetName = preset.Name;
        selectedPresetBaselineCode = code;
        workingAsNewPreset = false;
        RefreshOptionValue();
        PublishWorkingState();
        IsDirty = true;
    }

    private void OpenSaveAsPreset(string code)
    {
        string saveContextIdentity = editContextIdentity;
        XUiC_RebirthSandboxSaveAsPreset.Open(xui, code, string.Empty, delegate(string presetName)
        {
            RebirthSandboxUiSession.EnsureContextCurrent();
            if (!string.Equals(saveContextIdentity, RebirthSandboxUiSession.ContextIdentity, StringComparison.Ordinal)) { Log.Warning("[RebirthSandbox] Save As result ignored because the edit context changed."); return; }
            RebirthSandboxOptionManager.Current.ReloadPresets();
            RebirthSandboxPreset saved = RebirthSandboxOptionManager.Current.GetPreset(presetName);
            if (saved == null)
                return;

            RebirthSandboxState state;
            if (!RebirthSandboxOptionManager.TryDecode(saved.Code, out state))
                return;

            ApplyStateToWorking(state);
            workingPresetName = saved.Name;
            selectedPresetBaselineCode = saved.Code;
            workingAsNewPreset = false;
            RefreshOptionValue();
            RefreshPresetSelectors();
            RebirthSandboxUiSession.SetFromPreset(saved);
            CaptureSessionRollbackPoint();
            IsDirty = true;
        });
    }

    private static bool AreCodesSemanticallyEquivalent(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
            return true;

        RebirthSandboxState leftState;
        RebirthSandboxState rightState;
        if (!RebirthSandboxOptionManager.TryDecode(left, out leftState)
            || !RebirthSandboxOptionManager.TryDecode(right, out rightState))
            return false;

        return string.Equals(
            RebirthSandboxOptionManager.Encode(leftState),
            RebirthSandboxOptionManager.Encode(rightState),
            StringComparison.Ordinal);
    }

    private RebirthSandboxState GetBaselineState()
    {
        if (cachedBaselineState == null || !string.Equals(cachedBaselineCode, selectedPresetBaselineCode, StringComparison.Ordinal))
        {
            RebirthSandboxState baseline;
            if (!RebirthSandboxOptionManager.TryDecode(selectedPresetBaselineCode, out baseline) || baseline == null)
                baseline = new RebirthSandboxState();
            cachedBaselineState = baseline;
            cachedBaselineCode = selectedPresetBaselineCode;
        }
        return cachedBaselineState;
    }

    private static int GetRandomizedOptionIndex(
        RebirthSandboxOptionId option,
        int currentIndex,
        int minimumAllowedIndex,
        int maximumAllowedIndex)
    {
        RebirthSandboxOptionRandomizerMode mode =
            RebirthSandboxRandomizerLockState.GetMode(option);
        if (mode == RebirthSandboxOptionRandomizerMode.Locked)
            return currentIndex;

        int minIndex = minimumAllowedIndex;
        int maxIndex = maximumAllowedIndex;
        bool includeCurrentValue = false;

        if (mode == RebirthSandboxOptionRandomizerMode.Minimum)
        {
            int anchorIndex = RebirthSandboxRandomizerLockState.GetAnchorIndex(option, currentIndex);
            minIndex = ClampIndex(anchorIndex, minIndex, maxIndex);
            includeCurrentValue = true;
        }
        else if (mode == RebirthSandboxOptionRandomizerMode.Maximum)
        {
            int anchorIndex = RebirthSandboxRandomizerLockState.GetAnchorIndex(option, currentIndex);
            maxIndex = ClampIndex(anchorIndex, minIndex, maxIndex);
            includeCurrentValue = true;
        }

        return GetRandomIndex(minIndex, maxIndex, currentIndex, includeCurrentValue);
    }

    private static int ClampIndex(int index, int minIndex, int maxIndex)
    {
        if (index < minIndex)
            return minIndex;
        if (index > maxIndex)
            return maxIndex;
        return index;
    }

    private static int GetRandomIndex(int minIndex, int maxIndex, int currentIndex, bool allowCurrent)
    {
        int range = maxIndex - minIndex + 1;
        if (range <= 1)
            return minIndex;

        lock (Randomizer)
        {
            if (allowCurrent || currentIndex < minIndex || currentIndex > maxIndex)
                return minIndex + Randomizer.Next(range);

            int offset = Randomizer.Next(range - 1) + 1;
            return minIndex + ((currentIndex - minIndex + offset) % range);
        }
    }


    private void CaptureSessionRollbackPoint()
    {
        RebirthSandboxPreset saved = RebirthSandboxOptionManager.Current.GetPreset(RebirthSandboxUiSession.PresetName);
        if (saved != null)
        {
            sessionRollbackPresetName = saved.Name;
            sessionRollbackCode = saved.Code;
            return;
        }

        sessionRollbackPresetName = RebirthSandboxUiSession.PresetName;
        sessionRollbackCode = RebirthSandboxUiSession.Code;
    }

    private void RestoreSessionRollbackPoint()
    {
        if (string.Equals(RebirthSandboxUiSession.PresetName, sessionRollbackPresetName, StringComparison.Ordinal)
            && string.Equals(RebirthSandboxUiSession.Code, sessionRollbackCode, StringComparison.Ordinal))
            return;

        RebirthSandboxUiSession.SetCode(sessionRollbackPresetName, sessionRollbackCode);
    }

    private void PublishWorkingState()
    {
        InvalidateWorkingProjection();
        RebirthSandboxUiSession.SetCode(workingPresetName, RebirthCode);
    }

    private void MarkAsNewPresetWhenEditingBuiltIn()
    {
        RebirthSandboxPreset selected = RebirthSandboxOptionManager.Current.GetPreset(workingPresetName);
        if (selected == null || !selected.IsUserPreset)
            workingAsNewPreset = true;
    }

    public static void Open(XUi targetXui, Action closedCallback)
    {
        XUiC_RebirthSandboxOptions controller = targetXui.GetChildByType<XUiC_RebirthSandboxOptions>();
        if (controller == null)
        {
            Log.Error("[RebirthSandbox] rebirthSandboxOptions window controller was not found.");
            return;
        }

        controller.onClose = closedCallback;
        targetXui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup, false);
    }
}
