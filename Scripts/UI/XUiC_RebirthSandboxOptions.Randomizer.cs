using System;

#nullable disable

public partial class XUiC_RebirthSandboxOptions
{
    private static readonly int[] VehicleBlockRespawnRandomizerValues = { 0, 3, 7, 14, 21 };
    private static readonly int[] SleeperSpawnMultiplierRandomizerValues = { 1, 2, 3, 4 };
    private static readonly int[] InfestedSleeperSpawnMultiplierRandomizerValues = { 2, 3, 4, 5, 6 };
    private static readonly int[] BossEventMaximumPerDayRandomizerValues = { 1, 2, 3, 4, 5, 0 };

    [XuiBindComponent("btnSleeperRespawnsRandomizerLock", true)]
    public readonly XUiC_Button btnSleeperRespawnsRandomizerLock;

    [XuiBindComponent("btnSleeperSpawnMultiplierRandomizerLock", true)]
    public readonly XUiC_Button btnSleeperSpawnMultiplierRandomizerLock;

    [XuiBindComponent("btnInfestedSleeperSpawnMultiplierRandomizerLock", true)]
    public readonly XUiC_Button btnInfestedSleeperSpawnMultiplierRandomizerLock;

    [XuiBindComponent("btnHybridPathSmoothingRandomizerLock", true)]
    public readonly XUiC_Button btnHybridPathSmoothingRandomizerLock;

    [XuiBindComponent("btnZombiesDestroyAreasRandomizerLock", true)]
    public readonly XUiC_Button btnZombiesDestroyAreasRandomizerLock;

    [XuiBindComponent("btnRancherRangedAttackRandomizerLock", true)]
    public readonly XUiC_Button btnRancherRangedAttackRandomizerLock;

    [XuiBindComponent("btnAlwaysStaggerRandomizerLock", true)]
    public readonly XUiC_Button btnAlwaysStaggerRandomizerLock;

    [XuiBindComponent("btnProtectCrateRandomizerLock", true)]
    public readonly XUiC_Button btnProtectCrateRandomizerLock;

    [XuiBindComponent("btnPoiRiskRandomizerLock", true)]
    public readonly XUiC_Button btnPoiRiskRandomizerLock;

    [XuiBindComponent("btnPoiSenseScheduleRandomizerLock", true)]
    public readonly XUiC_Button btnPoiSenseScheduleRandomizerLock;

    [XuiBindComponent("btnPoiSenseIntensityRandomizerLock", true)]
    public readonly XUiC_Button btnPoiSenseIntensityRandomizerLock;

    [XuiBindComponent("btnTreeDensityMultiplierRandomizerLock", true)]
    public readonly XUiC_Button btnTreeDensityMultiplierRandomizerLock;

    [XuiBindComponent("btnVehicleDensityMultiplierRandomizerLock", true)]
    public readonly XUiC_Button btnVehicleDensityMultiplierRandomizerLock;

    [XuiBindComponent("btnBlocksCatchFireRandomizerLock", true)]
    public readonly XUiC_Button btnBlocksCatchFireRandomizerLock;

    [XuiBindComponent("btnFireAffectsHeatmapRandomizerLock", true)]
    public readonly XUiC_Button btnFireAffectsHeatmapRandomizerLock;

    [XuiBindComponent("btnFireBlockDamageSpeedRandomizerLock", true)]
    public readonly XUiC_Button btnFireBlockDamageSpeedRandomizerLock;

    [XuiBindComponent("btnWeatherFogBehaviorRandomizerLock", true)]
    public readonly XUiC_Button btnWeatherFogBehaviorRandomizerLock;

    [XuiBindComponent("btnWeatherFogIntensityRandomizerLock", true)]
    public readonly XUiC_Button btnWeatherFogIntensityRandomizerLock;

    [XuiBindComponent("btnUniformAtmosphereRandomizerLock", true)]
    public readonly XUiC_Button btnUniformAtmosphereRandomizerLock;

    [XuiBindComponent("btnPitchBlackRandomizerLock", true)]
    public readonly XUiC_Button btnPitchBlackRandomizerLock;

    [XuiBindComponent("btnAutoReplantTreesRandomizerLock", true)]
    public readonly XUiC_Button btnAutoReplantTreesRandomizerLock;

    [XuiBindComponent("btnVehicleBlockRespawnRandomizerLock", true)]
    public readonly XUiC_Button btnVehicleBlockRespawnRandomizerLock;

    [XuiBindComponent("btnMaxJobsRandomizerLock", true)]
    public readonly XUiC_Button btnMaxJobsRandomizerLock;

    [XuiBindComponent("btnJobsToNextTierRandomizerLock", true)]
    public readonly XUiC_Button btnJobsToNextTierRandomizerLock;

    [XuiBindComponent("btnRepeatPoiJobsRandomizerLock", true)]
    public readonly XUiC_Button btnRepeatPoiJobsRandomizerLock;

    [XuiBindComponent("btnInfestedJobsRandomizerLock", true)]
    public readonly XUiC_Button btnInfestedJobsRandomizerLock;

    [XuiBindComponent("btnTraderJobListRandomizerLock", true)]
    public readonly XUiC_Button btnTraderJobListRandomizerLock;

    [XuiBindComponent("btnTraderJobRecoveryGraceRandomizerLock", true)]
    public readonly XUiC_Button btnTraderJobRecoveryGraceRandomizerLock;

    [XuiBindComponent("btnTraderJobRecoveryGraceDurationRandomizerLock", true)]
    public readonly XUiC_Button btnTraderJobRecoveryGraceDurationRandomizerLock;

    [XuiBindComponent("btnLootTraderAreasRandomizerLock", true)]
    public readonly XUiC_Button btnLootTraderAreasRandomizerLock;

    [XuiBindComponent("btnBossEventsRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventsRandomizerLock;

    [XuiBindComponent("btnBossEventFrequencyRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventFrequencyRandomizerLock;

    [XuiBindComponent("btnBossEventMaximumPerDayRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventMaximumPerDayRandomizerLock;

    [XuiBindComponent("btnBossEventSizeRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventSizeRandomizerLock;

    [XuiBindComponent("btnBossEventDifficultyRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventDifficultyRandomizerLock;

    [XuiBindComponent("btnBossEventTimeRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventTimeRandomizerLock;

    [XuiBindComponent("btnBossEventBloodMoonDayRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventBloodMoonDayRandomizerLock;

    [XuiBindComponent("btnBossEventRestrictionRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventRestrictionRandomizerLock;

    [XuiBindComponent("btnBossEventRewardsRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventRewardsRandomizerLock;

    [XuiBindComponent("btnBossEventNotificationsRandomizerLock", true)]
    public readonly XUiC_Button btnBossEventNotificationsRandomizerLock;

    [XuiBindComponent("btnSecureAccessSharingRandomizerLock", true)]
    public readonly XUiC_Button btnSecureAccessSharingRandomizerLock;

    [XuiBindComponent("btnHornActivatedDoorsRandomizerLock", true)]
    public readonly XUiC_Button btnHornActivatedDoorsRandomizerLock;

    [XuiBindComponent("btnSuppressConsoleErrorPopupsRandomizerLock", true)]
    public readonly XUiC_Button btnSuppressConsoleErrorPopupsRandomizerLock;

    [XuiBindComponent("btnPreventHealingOverlapRandomizerLock", true)]
    public readonly XUiC_Button btnPreventHealingOverlapRandomizerLock;

    private static bool IsRebirthRandomizerLocked(RebirthSandboxOptionId option)
    {
        return RebirthSandboxRandomizerLockState.GetMode(option) != RebirthSandboxOptionRandomizerMode.Unlocked;
    }

    private static string GetRebirthRandomizerLockSprite(RebirthSandboxOptionId option)
    {
        return IsRebirthRandomizerLocked(option) ? LockedSprite : UnlockedSprite;
    }

    private static bool IsRebirthRandomizerMinimum(RebirthSandboxOptionId option)
    {
        return RebirthSandboxRandomizerLockState.GetMode(option) == RebirthSandboxOptionRandomizerMode.Minimum;
    }

    private static bool IsRebirthRandomizerMaximum(RebirthSandboxOptionId option)
    {
        return RebirthSandboxRandomizerLockState.GetMode(option) == RebirthSandboxOptionRandomizerMode.Maximum;
    }

    [XuiXmlBinding("rebirth_sleeper_respawns_randomizer_locked")]
    public bool SleeperRespawnsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.SleeperRespawns); } }

    [XuiXmlBinding("rebirth_sleeper_respawns_randomizer_lock_sprite")]
    public string SleeperRespawnsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SleeperRespawns); } }

    [XuiXmlBinding("rebirth_sleeper_respawns_randomizer_lock_is_minimum")]
    public bool SleeperRespawnsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SleeperRespawns); } }

    [XuiXmlBinding("rebirth_sleeper_respawns_randomizer_lock_is_maximum")]
    public bool SleeperRespawnsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SleeperRespawns); } }

    [XuiXmlBinding("rebirth_sleeper_spawn_multiplier_randomizer_locked")]
    public bool SleeperSpawnMultiplierRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.SleeperSpawnMultiplier); } }

    [XuiXmlBinding("rebirth_sleeper_spawn_multiplier_randomizer_lock_sprite")]
    public string SleeperSpawnMultiplierRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SleeperSpawnMultiplier); } }

    [XuiXmlBinding("rebirth_sleeper_spawn_multiplier_randomizer_lock_is_minimum")]
    public bool SleeperSpawnMultiplierRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SleeperSpawnMultiplier); } }

    [XuiXmlBinding("rebirth_sleeper_spawn_multiplier_randomizer_lock_is_maximum")]
    public bool SleeperSpawnMultiplierRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SleeperSpawnMultiplier); } }

    [XuiXmlBinding("rebirth_infested_sleeper_spawn_multiplier_randomizer_locked")]
    public bool InfestedSleeperSpawnMultiplierRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.InfestedSleeperSpawnMultiplier); } }

    [XuiXmlBinding("rebirth_infested_sleeper_spawn_multiplier_randomizer_lock_sprite")]
    public string InfestedSleeperSpawnMultiplierRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.InfestedSleeperSpawnMultiplier); } }

    [XuiXmlBinding("rebirth_infested_sleeper_spawn_multiplier_randomizer_lock_is_minimum")]
    public bool InfestedSleeperSpawnMultiplierRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.InfestedSleeperSpawnMultiplier); } }

    [XuiXmlBinding("rebirth_infested_sleeper_spawn_multiplier_randomizer_lock_is_maximum")]
    public bool InfestedSleeperSpawnMultiplierRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.InfestedSleeperSpawnMultiplier); } }

    [XuiXmlBinding("rebirth_hybrid_path_smoothing_randomizer_locked")]
    public bool HybridPathSmoothingRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.HybridPathSmoothing); } }

    [XuiXmlBinding("rebirth_hybrid_path_smoothing_randomizer_lock_sprite")]
    public string HybridPathSmoothingRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.HybridPathSmoothing); } }

    [XuiXmlBinding("rebirth_hybrid_path_smoothing_randomizer_lock_is_minimum")]
    public bool HybridPathSmoothingRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.HybridPathSmoothing); } }

    [XuiXmlBinding("rebirth_hybrid_path_smoothing_randomizer_lock_is_maximum")]
    public bool HybridPathSmoothingRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.HybridPathSmoothing); } }

    [XuiXmlBinding("rebirth_zombies_destroy_areas_randomizer_locked")]
    public bool ZombiesDestroyAreasRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.ZombiesDestroyAreas); } }

    [XuiXmlBinding("rebirth_zombies_destroy_areas_randomizer_lock_sprite")]
    public string ZombiesDestroyAreasRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.ZombiesDestroyAreas); } }

    [XuiXmlBinding("rebirth_zombies_destroy_areas_randomizer_lock_is_minimum")]
    public bool ZombiesDestroyAreasRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.ZombiesDestroyAreas); } }

    [XuiXmlBinding("rebirth_zombies_destroy_areas_randomizer_lock_is_maximum")]
    public bool ZombiesDestroyAreasRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.ZombiesDestroyAreas); } }

    [XuiXmlBinding("rebirth_rancher_ranged_attack_randomizer_locked")]
    public bool RancherRangedAttackRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.RancherRangedAttack); } }

    [XuiXmlBinding("rebirth_rancher_ranged_attack_randomizer_lock_sprite")]
    public string RancherRangedAttackRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.RancherRangedAttack); } }

    [XuiXmlBinding("rebirth_rancher_ranged_attack_randomizer_lock_is_minimum")]
    public bool RancherRangedAttackRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.RancherRangedAttack); } }

    [XuiXmlBinding("rebirth_rancher_ranged_attack_randomizer_lock_is_maximum")]
    public bool RancherRangedAttackRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.RancherRangedAttack); } }

    [XuiXmlBinding("rebirth_always_stagger_randomizer_locked")]
    public bool AlwaysStaggerRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.AlwaysStagger); } }

    [XuiXmlBinding("rebirth_always_stagger_randomizer_lock_sprite")]
    public string AlwaysStaggerRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.AlwaysStagger); } }

    [XuiXmlBinding("rebirth_always_stagger_randomizer_lock_is_minimum")]
    public bool AlwaysStaggerRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.AlwaysStagger); } }

    [XuiXmlBinding("rebirth_always_stagger_randomizer_lock_is_maximum")]
    public bool AlwaysStaggerRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.AlwaysStagger); } }

    [XuiXmlBinding("rebirth_protect_crate_randomizer_locked")]
    public bool ProtectCrateRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.ProtectCrate); } }

    [XuiXmlBinding("rebirth_protect_crate_randomizer_lock_sprite")]
    public string ProtectCrateRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.ProtectCrate); } }

    [XuiXmlBinding("rebirth_protect_crate_randomizer_lock_is_minimum")]
    public bool ProtectCrateRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.ProtectCrate); } }

    [XuiXmlBinding("rebirth_protect_crate_randomizer_lock_is_maximum")]
    public bool ProtectCrateRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.ProtectCrate); } }

    [XuiXmlBinding("rebirth_poi_risk_randomizer_locked")]
    public bool PoiRiskRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.PoiRisk); } }

    [XuiXmlBinding("rebirth_poi_risk_randomizer_lock_sprite")]
    public string PoiRiskRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.PoiRisk); } }

    [XuiXmlBinding("rebirth_poi_risk_randomizer_lock_is_minimum")]
    public bool PoiRiskRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.PoiRisk); } }

    [XuiXmlBinding("rebirth_poi_risk_randomizer_lock_is_maximum")]
    public bool PoiRiskRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.PoiRisk); } }

    [XuiXmlBinding("rebirth_poi_sense_schedule_randomizer_locked")]
    public bool PoiSenseScheduleRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.PoiSenseSchedule); } }

    [XuiXmlBinding("rebirth_poi_sense_schedule_randomizer_lock_sprite")]
    public string PoiSenseScheduleRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.PoiSenseSchedule); } }

    [XuiXmlBinding("rebirth_poi_sense_schedule_randomizer_lock_is_minimum")]
    public bool PoiSenseScheduleRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.PoiSenseSchedule); } }

    [XuiXmlBinding("rebirth_poi_sense_schedule_randomizer_lock_is_maximum")]
    public bool PoiSenseScheduleRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.PoiSenseSchedule); } }

    [XuiXmlBinding("rebirth_poi_sense_intensity_randomizer_locked")]
    public bool PoiSenseIntensityRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.PoiSenseIntensity); } }

    [XuiXmlBinding("rebirth_poi_sense_intensity_randomizer_lock_sprite")]
    public string PoiSenseIntensityRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.PoiSenseIntensity); } }

    [XuiXmlBinding("rebirth_poi_sense_intensity_randomizer_lock_is_minimum")]
    public bool PoiSenseIntensityRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.PoiSenseIntensity); } }

    [XuiXmlBinding("rebirth_poi_sense_intensity_randomizer_lock_is_maximum")]
    public bool PoiSenseIntensityRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.PoiSenseIntensity); } }

    [XuiXmlBinding("rebirth_tree_density_multiplier_randomizer_locked")]
    public bool TreeDensityMultiplierRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.TreeDensityMultiplier); } }

    [XuiXmlBinding("rebirth_tree_density_multiplier_randomizer_lock_sprite")]
    public string TreeDensityMultiplierRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.TreeDensityMultiplier); } }

    [XuiXmlBinding("rebirth_tree_density_multiplier_randomizer_lock_is_minimum")]
    public bool TreeDensityMultiplierRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.TreeDensityMultiplier); } }

    [XuiXmlBinding("rebirth_tree_density_multiplier_randomizer_lock_is_maximum")]
    public bool TreeDensityMultiplierRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.TreeDensityMultiplier); } }

    [XuiXmlBinding("rebirth_vehicle_density_multiplier_randomizer_locked")]
    public bool VehicleDensityMultiplierRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.VehicleDensityMultiplier); } }

    [XuiXmlBinding("rebirth_vehicle_density_multiplier_randomizer_lock_sprite")]
    public string VehicleDensityMultiplierRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.VehicleDensityMultiplier); } }

    [XuiXmlBinding("rebirth_vehicle_density_multiplier_randomizer_lock_is_minimum")]
    public bool VehicleDensityMultiplierRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.VehicleDensityMultiplier); } }

    [XuiXmlBinding("rebirth_vehicle_density_multiplier_randomizer_lock_is_maximum")]
    public bool VehicleDensityMultiplierRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.VehicleDensityMultiplier); } }

    [XuiXmlBinding("rebirth_blocks_catch_fire_randomizer_locked")]
    public bool BlocksCatchFireRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BlocksCatchFire); } }

    [XuiXmlBinding("rebirth_blocks_catch_fire_randomizer_lock_sprite")]
    public string BlocksCatchFireRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BlocksCatchFire); } }

    [XuiXmlBinding("rebirth_blocks_catch_fire_randomizer_lock_is_minimum")]
    public bool BlocksCatchFireRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BlocksCatchFire); } }

    [XuiXmlBinding("rebirth_blocks_catch_fire_randomizer_lock_is_maximum")]
    public bool BlocksCatchFireRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BlocksCatchFire); } }

    [XuiXmlBinding("rebirth_fire_affects_heatmap_randomizer_locked")]
    public bool FireAffectsHeatmapRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.FireAffectsHeatmap); } }

    [XuiXmlBinding("rebirth_fire_affects_heatmap_randomizer_lock_sprite")]
    public string FireAffectsHeatmapRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.FireAffectsHeatmap); } }

    [XuiXmlBinding("rebirth_fire_affects_heatmap_randomizer_lock_is_minimum")]
    public bool FireAffectsHeatmapRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.FireAffectsHeatmap); } }

    [XuiXmlBinding("rebirth_fire_affects_heatmap_randomizer_lock_is_maximum")]
    public bool FireAffectsHeatmapRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.FireAffectsHeatmap); } }

    [XuiXmlBinding("rebirth_fire_block_damage_speed_randomizer_locked")]
    public bool FireBlockDamageSpeedRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.FireBlockDamageSpeed); } }

    [XuiXmlBinding("rebirth_fire_block_damage_speed_randomizer_lock_sprite")]
    public string FireBlockDamageSpeedRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.FireBlockDamageSpeed); } }

    [XuiXmlBinding("rebirth_fire_block_damage_speed_randomizer_lock_is_minimum")]
    public bool FireBlockDamageSpeedRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.FireBlockDamageSpeed); } }

    [XuiXmlBinding("rebirth_fire_block_damage_speed_randomizer_lock_is_maximum")]
    public bool FireBlockDamageSpeedRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.FireBlockDamageSpeed); } }

    [XuiXmlBinding("rebirth_weather_fog_behavior_randomizer_locked")]
    public bool WeatherFogBehaviorRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.WeatherFogBehavior); } }

    [XuiXmlBinding("rebirth_weather_fog_behavior_randomizer_lock_sprite")]
    public string WeatherFogBehaviorRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WeatherFogBehavior); } }

    [XuiXmlBinding("rebirth_weather_fog_behavior_randomizer_lock_is_minimum")]
    public bool WeatherFogBehaviorRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WeatherFogBehavior); } }

    [XuiXmlBinding("rebirth_weather_fog_behavior_randomizer_lock_is_maximum")]
    public bool WeatherFogBehaviorRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WeatherFogBehavior); } }

    [XuiXmlBinding("rebirth_weather_fog_intensity_randomizer_locked")]
    public bool WeatherFogIntensityRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.WeatherFogIntensity); } }

    [XuiXmlBinding("rebirth_weather_fog_intensity_randomizer_lock_sprite")]
    public string WeatherFogIntensityRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WeatherFogIntensity); } }

    [XuiXmlBinding("rebirth_weather_fog_intensity_randomizer_lock_is_minimum")]
    public bool WeatherFogIntensityRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WeatherFogIntensity); } }

    [XuiXmlBinding("rebirth_weather_fog_intensity_randomizer_lock_is_maximum")]
    public bool WeatherFogIntensityRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WeatherFogIntensity); } }

    [XuiXmlBinding("rebirth_uniform_atmosphere_randomizer_locked")]
    public bool UniformAtmosphereRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.UniformAtmosphere); } }

    [XuiXmlBinding("rebirth_uniform_atmosphere_randomizer_lock_sprite")]
    public string UniformAtmosphereRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.UniformAtmosphere); } }

    [XuiXmlBinding("rebirth_uniform_atmosphere_randomizer_lock_is_minimum")]
    public bool UniformAtmosphereRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.UniformAtmosphere); } }

    [XuiXmlBinding("rebirth_uniform_atmosphere_randomizer_lock_is_maximum")]
    public bool UniformAtmosphereRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.UniformAtmosphere); } }

    [XuiXmlBinding("rebirth_pitch_black_randomizer_locked")]
    public bool PitchBlackRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.PitchBlack); } }

    [XuiXmlBinding("rebirth_pitch_black_randomizer_lock_sprite")]
    public string PitchBlackRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.PitchBlack); } }

    [XuiXmlBinding("rebirth_pitch_black_randomizer_lock_is_minimum")]
    public bool PitchBlackRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.PitchBlack); } }

    [XuiXmlBinding("rebirth_pitch_black_randomizer_lock_is_maximum")]
    public bool PitchBlackRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.PitchBlack); } }

    [XuiXmlBinding("rebirth_auto_replant_trees_randomizer_locked")]
    public bool AutoReplantTreesRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.AutoReplantTrees); } }

    [XuiXmlBinding("rebirth_auto_replant_trees_randomizer_lock_sprite")]
    public string AutoReplantTreesRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.AutoReplantTrees); } }

    [XuiXmlBinding("rebirth_auto_replant_trees_randomizer_lock_is_minimum")]
    public bool AutoReplantTreesRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.AutoReplantTrees); } }

    [XuiXmlBinding("rebirth_auto_replant_trees_randomizer_lock_is_maximum")]
    public bool AutoReplantTreesRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.AutoReplantTrees); } }

    [XuiXmlBinding("rebirth_vehicle_block_respawn_randomizer_locked")]
    public bool VehicleBlockRespawnRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.VehicleBlockRespawn); } }

    [XuiXmlBinding("rebirth_vehicle_block_respawn_randomizer_lock_sprite")]
    public string VehicleBlockRespawnRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.VehicleBlockRespawn); } }

    [XuiXmlBinding("rebirth_vehicle_block_respawn_randomizer_lock_is_minimum")]
    public bool VehicleBlockRespawnRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.VehicleBlockRespawn); } }

    [XuiXmlBinding("rebirth_vehicle_block_respawn_randomizer_lock_is_maximum")]
    public bool VehicleBlockRespawnRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.VehicleBlockRespawn); } }

    [XuiXmlBinding("rebirth_max_jobs_randomizer_locked")]
    public bool MaxJobsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.MaxJobs); } }

    [XuiXmlBinding("rebirth_max_jobs_randomizer_lock_sprite")]
    public string MaxJobsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.MaxJobs); } }

    [XuiXmlBinding("rebirth_max_jobs_randomizer_lock_is_minimum")]
    public bool MaxJobsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.MaxJobs); } }

    [XuiXmlBinding("rebirth_max_jobs_randomizer_lock_is_maximum")]
    public bool MaxJobsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.MaxJobs); } }

    [XuiXmlBinding("rebirth_jobs_to_next_tier_randomizer_locked")]
    public bool JobsToNextTierRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.JobsToNextTier); } }

    [XuiXmlBinding("rebirth_jobs_to_next_tier_randomizer_lock_sprite")]
    public string JobsToNextTierRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.JobsToNextTier); } }

    [XuiXmlBinding("rebirth_jobs_to_next_tier_randomizer_lock_is_minimum")]
    public bool JobsToNextTierRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.JobsToNextTier); } }

    [XuiXmlBinding("rebirth_jobs_to_next_tier_randomizer_lock_is_maximum")]
    public bool JobsToNextTierRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.JobsToNextTier); } }

    [XuiXmlBinding("rebirth_repeat_poi_jobs_randomizer_locked")]
    public bool RepeatPoiJobsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.RepeatPoiJobs); } }

    [XuiXmlBinding("rebirth_repeat_poi_jobs_randomizer_lock_sprite")]
    public string RepeatPoiJobsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.RepeatPoiJobs); } }

    [XuiXmlBinding("rebirth_repeat_poi_jobs_randomizer_lock_is_minimum")]
    public bool RepeatPoiJobsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.RepeatPoiJobs); } }

    [XuiXmlBinding("rebirth_repeat_poi_jobs_randomizer_lock_is_maximum")]
    public bool RepeatPoiJobsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.RepeatPoiJobs); } }

    [XuiXmlBinding("rebirth_infested_jobs_randomizer_locked")]
    public bool InfestedJobsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.InfestedJobs); } }

    [XuiXmlBinding("rebirth_infested_jobs_randomizer_lock_sprite")]
    public string InfestedJobsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.InfestedJobs); } }

    [XuiXmlBinding("rebirth_infested_jobs_randomizer_lock_is_minimum")]
    public bool InfestedJobsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.InfestedJobs); } }

    [XuiXmlBinding("rebirth_infested_jobs_randomizer_lock_is_maximum")]
    public bool InfestedJobsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.InfestedJobs); } }

    [XuiXmlBinding("rebirth_trader_job_list_randomizer_locked")]
    public bool TraderJobListRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.TraderJobList); } }

    [XuiXmlBinding("rebirth_trader_job_list_randomizer_lock_sprite")]
    public string TraderJobListRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.TraderJobList); } }

    [XuiXmlBinding("rebirth_trader_job_list_randomizer_lock_is_minimum")]
    public bool TraderJobListRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.TraderJobList); } }

    [XuiXmlBinding("rebirth_trader_job_list_randomizer_lock_is_maximum")]
    public bool TraderJobListRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.TraderJobList); } }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_randomizer_locked")]
    public bool TraderJobRecoveryGraceRandomizerLocked
    {
        get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.TraderJobRecoveryGrace); }
    }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_randomizer_lock_sprite")]
    public string TraderJobRecoveryGraceRandomizerLockSprite
    {
        get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.TraderJobRecoveryGrace); }
    }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_randomizer_lock_is_minimum")]
    public bool TraderJobRecoveryGraceRandomizerLockIsMinimum
    {
        get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.TraderJobRecoveryGrace); }
    }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_randomizer_lock_is_maximum")]
    public bool TraderJobRecoveryGraceRandomizerLockIsMaximum
    {
        get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.TraderJobRecoveryGrace); }
    }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_duration_randomizer_locked")]
    public bool TraderJobRecoveryGraceDurationRandomizerLocked
    {
        get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.TraderJobRecoveryGraceDuration); }
    }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_duration_randomizer_lock_sprite")]
    public string TraderJobRecoveryGraceDurationRandomizerLockSprite
    {
        get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.TraderJobRecoveryGraceDuration); }
    }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_duration_randomizer_lock_is_minimum")]
    public bool TraderJobRecoveryGraceDurationRandomizerLockIsMinimum
    {
        get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.TraderJobRecoveryGraceDuration); }
    }

    [XuiXmlBinding("rebirth_trader_job_recovery_grace_duration_randomizer_lock_is_maximum")]
    public bool TraderJobRecoveryGraceDurationRandomizerLockIsMaximum
    {
        get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.TraderJobRecoveryGraceDuration); }
    }

    [XuiXmlBinding("rebirth_loot_trader_areas_randomizer_locked")]
    public bool LootTraderAreasRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.LootTraderAreas); } }

    [XuiXmlBinding("rebirth_loot_trader_areas_randomizer_lock_sprite")]
    public string LootTraderAreasRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.LootTraderAreas); } }

    [XuiXmlBinding("rebirth_loot_trader_areas_randomizer_lock_is_minimum")]
    public bool LootTraderAreasRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.LootTraderAreas); } }

    [XuiXmlBinding("rebirth_loot_trader_areas_randomizer_lock_is_maximum")]
    public bool LootTraderAreasRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.LootTraderAreas); } }

    [XuiXmlBinding("rebirth_boss_events_randomizer_locked")]
    public bool BossEventsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEvents); } }

    [XuiXmlBinding("rebirth_boss_events_randomizer_lock_sprite")]
    public string BossEventsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEvents); } }

    [XuiXmlBinding("rebirth_boss_events_randomizer_lock_is_minimum")]
    public bool BossEventsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEvents); } }

    [XuiXmlBinding("rebirth_boss_events_randomizer_lock_is_maximum")]
    public bool BossEventsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEvents); } }

    [XuiXmlBinding("rebirth_boss_event_frequency_randomizer_locked")]
    public bool BossEventFrequencyRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventFrequency); } }

    [XuiXmlBinding("rebirth_boss_event_frequency_randomizer_lock_sprite")]
    public string BossEventFrequencyRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventFrequency); } }

    [XuiXmlBinding("rebirth_boss_event_frequency_randomizer_lock_is_minimum")]
    public bool BossEventFrequencyRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventFrequency); } }

    [XuiXmlBinding("rebirth_boss_event_frequency_randomizer_lock_is_maximum")]
    public bool BossEventFrequencyRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventFrequency); } }

    [XuiXmlBinding("rebirth_boss_event_maximum_randomizer_locked")]
    public bool BossEventMaximumPerDayRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventMaximumPerDay); } }

    [XuiXmlBinding("rebirth_boss_event_maximum_randomizer_lock_sprite")]
    public string BossEventMaximumPerDayRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventMaximumPerDay); } }

    [XuiXmlBinding("rebirth_boss_event_maximum_randomizer_lock_is_minimum")]
    public bool BossEventMaximumPerDayRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventMaximumPerDay); } }

    [XuiXmlBinding("rebirth_boss_event_maximum_randomizer_lock_is_maximum")]
    public bool BossEventMaximumPerDayRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventMaximumPerDay); } }

    [XuiXmlBinding("rebirth_boss_event_size_randomizer_locked")]
    public bool BossEventSizeRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventSize); } }

    [XuiXmlBinding("rebirth_boss_event_size_randomizer_lock_sprite")]
    public string BossEventSizeRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventSize); } }

    [XuiXmlBinding("rebirth_boss_event_size_randomizer_lock_is_minimum")]
    public bool BossEventSizeRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventSize); } }

    [XuiXmlBinding("rebirth_boss_event_size_randomizer_lock_is_maximum")]
    public bool BossEventSizeRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventSize); } }

    [XuiXmlBinding("rebirth_boss_event_difficulty_randomizer_locked")]
    public bool BossEventDifficultyRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventDifficulty); } }

    [XuiXmlBinding("rebirth_boss_event_difficulty_randomizer_lock_sprite")]
    public string BossEventDifficultyRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventDifficulty); } }

    [XuiXmlBinding("rebirth_boss_event_difficulty_randomizer_lock_is_minimum")]
    public bool BossEventDifficultyRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventDifficulty); } }

    [XuiXmlBinding("rebirth_boss_event_difficulty_randomizer_lock_is_maximum")]
    public bool BossEventDifficultyRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventDifficulty); } }

    [XuiXmlBinding("rebirth_boss_event_time_randomizer_locked")]
    public bool BossEventTimeRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventTime); } }

    [XuiXmlBinding("rebirth_boss_event_time_randomizer_lock_sprite")]
    public string BossEventTimeRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventTime); } }

    [XuiXmlBinding("rebirth_boss_event_time_randomizer_lock_is_minimum")]
    public bool BossEventTimeRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventTime); } }

    [XuiXmlBinding("rebirth_boss_event_time_randomizer_lock_is_maximum")]
    public bool BossEventTimeRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventTime); } }

    [XuiXmlBinding("rebirth_boss_event_blood_moon_randomizer_locked")]
    public bool BossEventBloodMoonDayRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventBloodMoonDay); } }

    [XuiXmlBinding("rebirth_boss_event_blood_moon_randomizer_lock_sprite")]
    public string BossEventBloodMoonDayRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventBloodMoonDay); } }

    [XuiXmlBinding("rebirth_boss_event_blood_moon_randomizer_lock_is_minimum")]
    public bool BossEventBloodMoonDayRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventBloodMoonDay); } }

    [XuiXmlBinding("rebirth_boss_event_blood_moon_randomizer_lock_is_maximum")]
    public bool BossEventBloodMoonDayRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventBloodMoonDay); } }

    [XuiXmlBinding("rebirth_boss_event_restriction_randomizer_locked")]
    public bool BossEventRestrictionRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventRestriction); } }

    [XuiXmlBinding("rebirth_boss_event_restriction_randomizer_lock_sprite")]
    public string BossEventRestrictionRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventRestriction); } }

    [XuiXmlBinding("rebirth_boss_event_restriction_randomizer_lock_is_minimum")]
    public bool BossEventRestrictionRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventRestriction); } }

    [XuiXmlBinding("rebirth_boss_event_restriction_randomizer_lock_is_maximum")]
    public bool BossEventRestrictionRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventRestriction); } }

    [XuiXmlBinding("rebirth_boss_event_rewards_randomizer_locked")]
    public bool BossEventRewardsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventRewards); } }

    [XuiXmlBinding("rebirth_boss_event_rewards_randomizer_lock_sprite")]
    public string BossEventRewardsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventRewards); } }

    [XuiXmlBinding("rebirth_boss_event_rewards_randomizer_lock_is_minimum")]
    public bool BossEventRewardsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventRewards); } }

    [XuiXmlBinding("rebirth_boss_event_rewards_randomizer_lock_is_maximum")]
    public bool BossEventRewardsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventRewards); } }

    [XuiXmlBinding("rebirth_boss_event_notifications_randomizer_locked")]
    public bool BossEventNotificationsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventNotifications); } }

    [XuiXmlBinding("rebirth_boss_event_notifications_randomizer_lock_sprite")]
    public string BossEventNotificationsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventNotifications); } }

    [XuiXmlBinding("rebirth_boss_event_notifications_randomizer_lock_is_minimum")]
    public bool BossEventNotificationsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventNotifications); } }

    [XuiXmlBinding("rebirth_boss_event_notifications_randomizer_lock_is_maximum")]
    public bool BossEventNotificationsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventNotifications); } }

    [XuiXmlBinding("rebirth_secure_access_sharing_randomizer_locked")]
    public bool SecureAccessSharingRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.SecureAccessSharing); } }

    [XuiXmlBinding("rebirth_secure_access_sharing_randomizer_lock_sprite")]
    public string SecureAccessSharingRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SecureAccessSharing); } }

    [XuiXmlBinding("rebirth_secure_access_sharing_randomizer_lock_is_minimum")]
    public bool SecureAccessSharingRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SecureAccessSharing); } }

    [XuiXmlBinding("rebirth_secure_access_sharing_randomizer_lock_is_maximum")]
    public bool SecureAccessSharingRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SecureAccessSharing); } }

    [XuiXmlBinding("rebirth_horn_activated_doors_randomizer_locked")]
    public bool HornActivatedDoorsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.HornActivatedDoors); } }

    [XuiXmlBinding("rebirth_horn_activated_doors_randomizer_lock_sprite")]
    public string HornActivatedDoorsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.HornActivatedDoors); } }

    [XuiXmlBinding("rebirth_horn_activated_doors_randomizer_lock_is_minimum")]
    public bool HornActivatedDoorsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.HornActivatedDoors); } }

    [XuiXmlBinding("rebirth_horn_activated_doors_randomizer_lock_is_maximum")]
    public bool HornActivatedDoorsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.HornActivatedDoors); } }

    [XuiXmlBinding("rebirth_suppress_console_error_popups_randomizer_locked")]
    public bool SuppressConsoleErrorPopupsRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.SuppressConsoleErrorPopups); } }

    [XuiXmlBinding("rebirth_suppress_console_error_popups_randomizer_lock_sprite")]
    public string SuppressConsoleErrorPopupsRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SuppressConsoleErrorPopups); } }

    [XuiXmlBinding("rebirth_suppress_console_error_popups_randomizer_lock_is_minimum")]
    public bool SuppressConsoleErrorPopupsRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SuppressConsoleErrorPopups); } }

    [XuiXmlBinding("rebirth_suppress_console_error_popups_randomizer_lock_is_maximum")]
    public bool SuppressConsoleErrorPopupsRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SuppressConsoleErrorPopups); } }

    [XuiXmlBinding("rebirth_prevent_healing_overlap_randomizer_locked")]
    public bool PreventHealingOverlapRandomizerLocked { get { return IsRebirthRandomizerLocked(RebirthSandboxOptionId.PreventHealingOverlap); } }

    [XuiXmlBinding("rebirth_prevent_healing_overlap_randomizer_lock_sprite")]
    public string PreventHealingOverlapRandomizerLockSprite { get { return GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.PreventHealingOverlap); } }

    [XuiXmlBinding("rebirth_prevent_healing_overlap_randomizer_lock_is_minimum")]
    public bool PreventHealingOverlapRandomizerLockIsMinimum { get { return IsRebirthRandomizerMinimum(RebirthSandboxOptionId.PreventHealingOverlap); } }

    [XuiXmlBinding("rebirth_prevent_healing_overlap_randomizer_lock_is_maximum")]
    public bool PreventHealingOverlapRandomizerLockIsMaximum { get { return IsRebirthRandomizerMaximum(RebirthSandboxOptionId.PreventHealingOverlap); } }

    private void CycleRebirthRandomizerLock(
        RebirthSandboxOptionId option,
        XUiC_ComboBoxList<string> combo,
        int fallbackIndex,
        XUiController sender)
    {
        int selectedIndex = combo != null && combo.SelectedIndex >= 0
            ? combo.SelectedIndex
            : fallbackIndex;
        RebirthSandboxRandomizerLockState.CycleMode(option, selectedIndex);
        if (sender != null)
            sender.IsDirty = true;
        RefreshBindings();
        IsDirty = true;
    }

    [XuiBindEvent("OnPress", "btnSleeperRespawnsRandomizerLock")]
    public void SleeperRespawnsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.SleeperRespawns, cbxSleeperRespawns, workingSleeperRespawns ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnSleeperSpawnMultiplierRandomizerLock")]
    public void SleeperSpawnMultiplierRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.SleeperSpawnMultiplier, cbxSleeperSpawnMultiplier, GetValueIndex(SleeperSpawnMultiplierRandomizerValues, workingSleeperSpawnMultiplier, 0), sender);
    }

    [XuiBindEvent("OnPress", "btnInfestedSleeperSpawnMultiplierRandomizerLock")]
    public void InfestedSleeperSpawnMultiplierRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.InfestedSleeperSpawnMultiplier, cbxInfestedSleeperSpawnMultiplier, GetValueIndex(InfestedSleeperSpawnMultiplierRandomizerValues, workingInfestedSleeperSpawnMultiplier, 0), sender);
    }

    [XuiBindEvent("OnPress", "btnHybridPathSmoothingRandomizerLock")]
    public void HybridPathSmoothingRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.HybridPathSmoothing, cbxHybridPathSmoothing, workingHybridPathSmoothing ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnZombiesDestroyAreasRandomizerLock")]
    public void ZombiesDestroyAreasRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.ZombiesDestroyAreas, cbxZombiesDestroyAreas, workingZombiesDestroyAreas ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnRancherRangedAttackRandomizerLock")]
    public void RancherRangedAttackRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.RancherRangedAttack, cbxRancherRangedAttack, workingRancherRangedAttack ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnAlwaysStaggerRandomizerLock")]
    public void AlwaysStaggerRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.AlwaysStagger, cbxAlwaysStagger, (int)workingAlwaysStagger, sender);
    }

    [XuiBindEvent("OnPress", "btnProtectCrateRandomizerLock")]
    public void ProtectCrateRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.ProtectCrate, cbxProtectCrate, workingProtectCrate ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnPoiRiskRandomizerLock")]
    public void PoiRiskRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.PoiRisk, cbxPoiRisk, (int)workingPoiRisk, sender);
    }

    [XuiBindEvent("OnPress", "btnPoiSenseScheduleRandomizerLock")]
    public void PoiSenseScheduleRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.PoiSenseSchedule, cbxPoiSenseSchedule, (int)workingPoiSenseSchedule, sender);
    }

    [XuiBindEvent("OnPress", "btnPoiSenseIntensityRandomizerLock")]
    public void PoiSenseIntensityRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.PoiSenseIntensity, cbxPoiSenseIntensity, (int)workingPoiSenseIntensity, sender);
    }

    [XuiBindEvent("OnPress", "btnTreeDensityMultiplierRandomizerLock")]
    public void TreeDensityMultiplierRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.TreeDensityMultiplier, cbxTreeDensityMultiplier, RebirthWorldDecorationDensityRuntimePolicy.MultiplierToIndex(workingTreeDensityMultiplier), sender);
    }

    [XuiBindEvent("OnPress", "btnVehicleDensityMultiplierRandomizerLock")]
    public void VehicleDensityMultiplierRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.VehicleDensityMultiplier, cbxVehicleDensityMultiplier, RebirthWorldDecorationDensityRuntimePolicy.MultiplierToIndex(workingVehicleDensityMultiplier), sender);
    }

    [XuiBindEvent("OnPress", "btnBlocksCatchFireRandomizerLock")]
    public void BlocksCatchFireRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BlocksCatchFire, cbxBlocksCatchFire, workingBlocksCatchFire ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnFireAffectsHeatmapRandomizerLock")]
    public void FireAffectsHeatmapRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(
            RebirthSandboxOptionId.FireAffectsHeatmap,
            cbxFireAffectsHeatmap,
            workingFireAffectsHeatmap ? 1 : 0,
            sender);
    }

    [XuiBindEvent("OnPress", "btnFireBlockDamageSpeedRandomizerLock")]
    public void FireBlockDamageSpeedRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(
            RebirthSandboxOptionId.FireBlockDamageSpeed,
            cbxFireBlockDamageSpeed,
            (int)workingFireBlockDamageSpeed,
            sender);
    }

    [XuiBindEvent("OnPress", "btnWeatherFogBehaviorRandomizerLock")]
    public void WeatherFogBehaviorRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.WeatherFogBehavior, cbxWeatherFogBehavior, (int)workingWeatherFogBehavior, sender);
    }

    [XuiBindEvent("OnPress", "btnWeatherFogIntensityRandomizerLock")]
    public void WeatherFogIntensityRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.WeatherFogIntensity, cbxWeatherFogIntensity, (int)workingWeatherFogIntensity, sender);
    }

    [XuiBindEvent("OnPress", "btnUniformAtmosphereRandomizerLock")]
    public void UniformAtmosphereRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.UniformAtmosphere, cbxUniformAtmosphere, workingUniformAtmosphere ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnPitchBlackRandomizerLock")]
    public void PitchBlackRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.PitchBlack, cbxPitchBlack, workingPitchBlack ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnAutoReplantTreesRandomizerLock")]
    public void AutoReplantTreesRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.AutoReplantTrees, cbxAutoReplantTrees, workingAutoReplantTrees ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnVehicleBlockRespawnRandomizerLock")]
    public void VehicleBlockRespawnRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.VehicleBlockRespawn, cbxVehicleBlockRespawn, GetValueIndex(VehicleBlockRespawnRandomizerValues, workingVehicleBlockRespawnDays, 0), sender);
    }

    [XuiBindEvent("OnPress", "btnMaxJobsRandomizerLock")]
    public void MaxJobsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.MaxJobs, cbxMaxJobs, RebirthTraderJobPolicy.MaxJobsToIndex(workingMaxJobs), sender);
    }

    [XuiBindEvent("OnPress", "btnJobsToNextTierRandomizerLock")]
    public void JobsToNextTierRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.JobsToNextTier, cbxJobsToNextTier, RebirthTraderJobPolicy.JobsToNextTierToIndex(workingJobsToNextTier), sender);
    }

    [XuiBindEvent("OnPress", "btnRepeatPoiJobsRandomizerLock")]
    public void RepeatPoiJobsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.RepeatPoiJobs, cbxRepeatPoiJobs, (int)workingRepeatPoiJobs, sender);
    }

    [XuiBindEvent("OnPress", "btnInfestedJobsRandomizerLock")]
    public void InfestedJobsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.InfestedJobs, cbxInfestedJobs, (int)workingInfestedJobs, sender);
    }

    [XuiBindEvent("OnPress", "btnTraderJobListRandomizerLock")]
    public void TraderJobListRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.TraderJobList, cbxTraderJobList, (int)workingTraderJobList, sender);
    }

    [XuiBindEvent("OnPress", "btnTraderJobRecoveryGraceRandomizerLock")]
    public void TraderJobRecoveryGraceRandomizerLock_OnPressed(
        XUiController sender,
        int mouseButton)
    {
        CycleRebirthRandomizerLock(
            RebirthSandboxOptionId.TraderJobRecoveryGrace,
            cbxTraderJobRecoveryGrace,
            workingTraderJobRecoveryGrace ? 1 : 0,
            sender);
    }

    [XuiBindEvent("OnPress", "btnTraderJobRecoveryGraceDurationRandomizerLock")]
    public void TraderJobRecoveryGraceDurationRandomizerLock_OnPressed(
        XUiController sender,
        int mouseButton)
    {
        CycleRebirthRandomizerLock(
            RebirthSandboxOptionId.TraderJobRecoveryGraceDuration,
            cbxTraderJobRecoveryGraceDuration,
            (int)workingTraderJobRecoveryGraceDuration,
            sender);
    }

    [XuiBindEvent("OnPress", "btnLootTraderAreasRandomizerLock")]
    public void LootTraderAreasRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.LootTraderAreas, cbxLootTraderAreas, workingLootTraderAreas ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventsRandomizerLock")]
    public void BossEventsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEvents, cbxBossEvents, (int)workingBossEventStartLevel, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventFrequencyRandomizerLock")]
    public void BossEventFrequencyRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventFrequency, cbxBossEventFrequency, (int)workingBossEventFrequency, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventMaximumPerDayRandomizerLock")]
    public void BossEventMaximumPerDayRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventMaximumPerDay, cbxBossEventMaximumPerDay, GetValueIndex(BossEventMaximumPerDayRandomizerValues, workingBossEventMaximumPerDay, 1), sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventSizeRandomizerLock")]
    public void BossEventSizeRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventSize, cbxBossEventSize, (int)workingBossEventSize, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventDifficultyRandomizerLock")]
    public void BossEventDifficultyRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventDifficulty, cbxBossEventDifficulty, (int)workingBossEventDifficulty, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventTimeRandomizerLock")]
    public void BossEventTimeRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventTime, cbxBossEventTime, (int)workingBossEventTime, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventBloodMoonDayRandomizerLock")]
    public void BossEventBloodMoonDayRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventBloodMoonDay, cbxBossEventBloodMoonDay, workingBossEventBloodMoonDay ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventRestrictionRandomizerLock")]
    public void BossEventRestrictionRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventRestriction, cbxBossEventRestriction, (int)workingBossEventRestriction, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventRewardsRandomizerLock")]
    public void BossEventRewardsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventRewards, cbxBossEventRewards, workingBossEventRewards ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnBossEventNotificationsRandomizerLock")]
    public void BossEventNotificationsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventNotifications, cbxBossEventNotifications, workingBossEventNotifications ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnSecureAccessSharingRandomizerLock")]
    public void SecureAccessSharingRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.SecureAccessSharing, cbxSecureAccessSharing, (int)workingSecureAccessSharing, sender);
    }

    [XuiBindEvent("OnPress", "btnHornActivatedDoorsRandomizerLock")]
    public void HornActivatedDoorsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.HornActivatedDoors, cbxHornActivatedDoors, (int)workingHornActivatedDoors, sender);
    }

    [XuiBindEvent("OnPress", "btnSuppressConsoleErrorPopupsRandomizerLock")]
    public void SuppressConsoleErrorPopupsRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.SuppressConsoleErrorPopups, cbxSuppressConsoleErrorPopups, workingSuppressConsoleErrorPopups ? 1 : 0, sender);
    }

    [XuiBindEvent("OnPress", "btnPreventHealingOverlapRandomizerLock")]
    public void PreventHealingOverlapRandomizerLock_OnPressed(XUiController sender, int mouseButton)
    {
        CycleRebirthRandomizerLock(RebirthSandboxOptionId.PreventHealingOverlap, cbxPreventHealingOverlap, (int)workingPreventHealingOverlap, sender);
    }

    private static bool RandomizeBooleanOption(RebirthSandboxOptionId option, ref bool value)
    {
        int currentIndex = value ? 1 : 0;
        int randomizedIndex = GetRandomizedOptionIndex(option, currentIndex, 0, 1);
        if (randomizedIndex == currentIndex)
            return false;

        value = randomizedIndex == 1;
        return true;
    }

    private static bool RandomizeEnumOption<T>(
        RebirthSandboxOptionId option,
        ref T value,
        T minimumValue,
        T maximumValue) where T : struct
    {
        int currentIndex = Convert.ToInt32(value);
        int minimumIndex = Convert.ToInt32(minimumValue);
        int maximumIndex = Convert.ToInt32(maximumValue);
        int randomizedIndex = GetRandomizedOptionIndex(option, currentIndex, minimumIndex, maximumIndex);
        if (randomizedIndex == currentIndex)
            return false;

        value = (T)Enum.ToObject(typeof(T), randomizedIndex);
        return true;
    }

    private static bool RandomizeIndexedIntegerOption(
        RebirthSandboxOptionId option,
        ref int value,
        int[] allowedValues)
    {
        if (allowedValues == null || allowedValues.Length == 0)
            return false;

        int currentIndex = GetValueIndex(allowedValues, value, 0);
        int randomizedIndex = GetRandomizedOptionIndex(option, currentIndex, 0, allowedValues.Length - 1);
        if (randomizedIndex == currentIndex)
            return false;

        value = allowedValues[randomizedIndex];
        return true;
    }

    private static int GetValueIndex(int[] values, int value, int fallbackIndex)
    {
        if (values != null)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == value)
                    return i;
            }
        }

        return fallbackIndex;
    }
}
