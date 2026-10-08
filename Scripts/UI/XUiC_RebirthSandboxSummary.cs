using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class XUiC_RebirthSandboxSummary : XUiController
{
    [XuiBindComponent("cbxRebirthPresetGroup", true)]
    public readonly XUiC_ComboBoxList<string> cbxPresetGroup;

    [XuiBindComponent("cbxRebirthPreset", true)]
    public readonly XUiC_ComboBoxList<string> cbxPreset;

    [XuiBindComponent("btnRebirthOptions", true)]
    public readonly XUiC_Button btnRebirthOptions;

    private readonly List<string> groupIds = new List<string>();
    private readonly List<RebirthSandboxPreset> visiblePresets = new List<RebirthSandboxPreset>();
    private bool suppressEvents;
    private bool comboEventsBound;
    private float contextPollTimer;
    private string description = string.Empty;
    private string changedOptions = string.Empty;
    private string presetName = string.Empty;

    [XuiXmlBinding("rebirth_description")]
    public string Description { get { return description; } }

    [XuiXmlBinding("rebirth_changed_options")]
    public string ChangedOptions { get { return changedOptions; } }

    [XuiXmlBinding("rebirth_preset_name")]
    public string PresetName { get { return presetName; } }

    public override void OnOpen()
    {
        base.OnOpen();
        BindComboEvents();
        RebirthSandboxUiSession.Changed -= OnSessionChanged;
        RebirthSandboxUiSession.Changed += OnSessionChanged;
        RebirthSandboxUiSession.EnsureContextCurrent();
        RefreshFromSession();
    }

    public override void OnClose()
    {
        UnbindComboEvents();
        RebirthSandboxUiSession.Changed -= OnSessionChanged;
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        contextPollTimer -= dt;
        if (contextPollTimer <= 0f)
        {
            contextPollTimer = 0.25f;
            RebirthSandboxUiSession.EnsureContextCurrent();
        }
        handleDirtyUpdateDefault();
    }

    public void PresetGroup_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents)
            return;

        int groupIndex = cbxPresetGroup.SelectedIndex;
        if (groupIndex < 0 || groupIndex >= groupIds.Count)
            return;

        PopulatePresets(groupIds[groupIndex], null, true);
    }

    public void Preset_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents)
            return;

        int index = cbxPreset.SelectedIndex;
        if (index < 0 || index >= visiblePresets.Count)
            return;

        RebirthSandboxPreset preset = visiblePresets[index];
        if (preset != null)
            RebirthSandboxUiSession.SetFromPreset(preset);
    }

    [XuiBindEvent("OnPress", "btnRebirthOptions")]
    public void BtnRebirthOptions_OnPressed(XUiController sender, int mouseButton)
    {
        XUiC_RebirthSandboxOptions.Open(xui, RefreshFromSession);
    }

    private void BindComboEvents()
    {
        if (comboEventsBound)
            return;

        if (cbxPresetGroup != null)
            cbxPresetGroup.OnValueChanged += PresetGroup_OnValueChanged;
        if (cbxPreset != null)
            cbxPreset.OnValueChanged += Preset_OnValueChanged;

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

        comboEventsBound = false;
    }

    private void OnSessionChanged()
    {
        RefreshFromSession();
    }

    private void RefreshFromSession()
    {
        RebirthSandboxOptionManager manager = RebirthSandboxOptionManager.Current;
        RebirthSandboxPreset selected = manager.ResolvePreset(RebirthSandboxUiSession.PresetName, RebirthSandboxUiSession.Code);
        if (selected == null)
            selected = manager.GetPreset(RebirthSandboxOptionManager.DefaultPresetName);

        suppressEvents = true;
        try
        {
            PopulateGroups(selected != null ? selected.Group : null);
            PopulatePresets(selected != null ? selected.Group : null, selected != null ? selected.Name : null, false);
        }
        finally
        {
            suppressEvents = false;
        }

        RebirthSandboxState state;
        if (!RebirthSandboxOptionManager.TryDecode(RebirthSandboxUiSession.Code, out state))
            state = new RebirthSandboxState();

        description = selected != null ? selected.DisplayDescription : string.Empty;
        presetName = selected != null ? selected.DisplayName : Localization.Get("xuiRebirthPresetStandard");

        RebirthSandboxState defaults = new RebirthSandboxState();
        List<string> changes = new List<string>();
        if (state.PlayerProgression != defaults.PlayerProgression)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthPlayerProgression") + ":[-] "
                + Localization.Get(state.PlayerProgression == RebirthPlayerProgressionMode.Rebirth
                    ? "xuiRebirthPlayerProgressionRebirth"
                    : "xuiRebirthPlayerProgressionBaseGame") + "[-]");

        if (state.SpawnProgression != defaults.SpawnProgression)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthSpawnProgression") + ":[-] "
                + "[E41215]"
                + Localization.Get(state.SpawnProgression == RebirthSpawnProgressionMode.Biome
                    ? "xuiRebirthSpawnProgressionBiome"
                    : "xuiRebirthSpawnProgressionGamestage") + "[-]");
        }
        if (state.HybridPathSmoothing != defaults.HybridPathSmoothing)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthHybridPathSmoothing") + ":[-] "
                + Localization.Get(state.HybridPathSmoothing ? "xuiOn" : "xuiOff") + "[-]");
        }

        if (state.ZombiesDestroyAreas != defaults.ZombiesDestroyAreas)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthZombiesDestroyAreas") + ":[-] "
                + Localization.Get(state.ZombiesDestroyAreas ? "xuiOn" : "xuiOff") + "[-]");
        }
        if (state.RancherRangedAttack != defaults.RancherRangedAttack)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthRancherRangedAttack") + ":[-] "
                + Localization.Get(state.RancherRangedAttack ? "xuiOn" : "xuiOff") + "[-]");
        }

        if (state.SleeperRespawns != defaults.SleeperRespawns)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthSleeperRespawns") + ":[-] "
                + Localization.Get(state.SleeperRespawns ? "xuiOn" : "xuiOff") + "[-]");
        }

        if (state.SleeperSpawnMultiplier != defaults.SleeperSpawnMultiplier)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthSleeperSpawnMultiplier") + ":[-] " + state.SleeperSpawnMultiplier + "x[-]");
        if (state.InfestedSleeperSpawnMultiplier != defaults.InfestedSleeperSpawnMultiplier)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthInfestedSleeperSpawnMultiplier") + ":[-] " + state.InfestedSleeperSpawnMultiplier + "x[-]");

        if (state.SuppressConsoleErrorPopups != defaults.SuppressConsoleErrorPopups)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthSuppressConsoleErrorPopups") + ":[-] "
                + Localization.Get(state.SuppressConsoleErrorPopups ? "xuiOn" : "xuiOff") + "[-]");
        }
        if (state.TreeDensityMultiplier != defaults.TreeDensityMultiplier)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthTreeDensityMultiplier") + ":[-] " + state.TreeDensityMultiplier + "%[-]");
        if (state.VehicleDensityMultiplier != defaults.VehicleDensityMultiplier)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthVehicleDensityMultiplier") + ":[-] " + state.VehicleDensityMultiplier + "%[-]");
        if (state.BlocksCatchFire != defaults.BlocksCatchFire)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthBlocksCatchFire") + ":[-] " + Localization.Get(state.BlocksCatchFire ? "xuiYes" : "xuiNo") + "[-]");
        if (state.FireAffectsHeatmap != defaults.FireAffectsHeatmap)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthFireAffectsHeatmap") + ":[-] " + Localization.Get(state.FireAffectsHeatmap ? "xuiYes" : "xuiNo") + "[-]");
        if (state.FireBlockDamageSpeed != defaults.FireBlockDamageSpeed)
        {
            string fireDamageSpeedKey = state.FireBlockDamageSpeed == RebirthFireBlockDamageSpeed.Slower
                ? "xuiRebirthFireBlockDamageSlower"
                : state.FireBlockDamageSpeed == RebirthFireBlockDamageSpeed.Faster
                    ? "xuiRebirthFireBlockDamageFaster"
                    : "xuiRebirthFireBlockDamageDefault";
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthFireBlockDamageSpeed") + ":[-] " + Localization.Get(fireDamageSpeedKey) + "[-]");
        }
        if (state.MaxJobs != defaults.MaxJobs)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthMaxJobs") + ":[-] " + state.MaxJobs + "[-]");
        if (state.JobsToNextTier != defaults.JobsToNextTier)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthJobsToNextTier") + ":[-] " + state.JobsToNextTier + "[-]");
        if (state.RepeatPoiJobs != defaults.RepeatPoiJobs)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthRepeatPoiJobs") + ":[-] " + Localization.Get(GetRepeatPoiPolicyKey(state.RepeatPoiJobs)) + "[-]");
        if (state.InfestedJobs != defaults.InfestedJobs)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthInfestedJobs") + ":[-] " + Localization.Get(GetInfestedJobsModeKey(state.InfestedJobs)) + "[-]");
        if (state.TraderJobList != defaults.TraderJobList)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthTraderJobList") + ":[-] " + Localization.Get(state.TraderJobList == RebirthTraderJobListMode.Fixed ? "xuiRebirthTraderJobListFixed" : "xuiRebirthTraderJobListRandom") + "[-]");
        if (state.PreventHealingOverlap != defaults.PreventHealingOverlap)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthPreventHealingOverlap") + ":[-] " + GetHealingOverlapThresholdLabel(state.PreventHealingOverlap) + "[-]");

        if (state.AlwaysStagger != defaults.AlwaysStagger)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthAlwaysStagger") + ":[-] " + Localization.Get(GetAlwaysStaggerModeKey(state.AlwaysStagger)) + "[-]");
        if (state.WeatherFogBehavior != defaults.WeatherFogBehavior)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthWeatherFogBehavior") + ":[-] " + Localization.Get(GetWeatherFogBehaviorKey(state.WeatherFogBehavior)) + "[-]");
        if (state.WeatherFogIntensity != defaults.WeatherFogIntensity)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthWeatherFogIntensity") + ":[-] " + Localization.Get(GetWeatherFogIntensityKey(state.WeatherFogIntensity)) + "[-]");
        if (state.UniformAtmosphere != defaults.UniformAtmosphere)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthUniformAtmosphere") + ":[-] " + Localization.Get(state.UniformAtmosphere ? "xuiYes" : "xuiNo") + "[-]");
        if (state.PitchBlack != defaults.PitchBlack)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthPitchBlack") + ":[-] " + Localization.Get(state.PitchBlack ? "xuiYes" : "xuiNo") + "[-]");
        if (state.SpawnProgression == RebirthSpawnProgressionMode.Gamestage
            && state.PoiRisk != defaults.PoiRisk)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthPoiRisk") + ":[-] " + Localization.Get(GetPoiRiskModeKey(state.PoiRisk)) + "[-]");
        if (state.PoiSenseSchedule != defaults.PoiSenseSchedule)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthPoiSenseSchedule") + ":[-] " + Localization.Get(GetPoiSenseScheduleKey(state.PoiSenseSchedule)) + "[-]");
        if (state.PoiSenseIntensity != defaults.PoiSenseIntensity)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthPoiSenseIntensity") + ":[-] " + Localization.Get(GetPoiSenseIntensityKey(state.PoiSenseIntensity)) + "[-]");

        if (state.LootTraderAreas != defaults.LootTraderAreas)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthLootTraderAreas") + ":[-] "
                + Localization.Get(state.LootTraderAreas ? "xuiOn" : "xuiOff") + "[-]");
        }
        if (state.AutoReplantTrees != defaults.AutoReplantTrees)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthAutoReplantTrees") + ":[-] "
                + Localization.Get(state.AutoReplantTrees ? "xuiOn" : "xuiOff") + "[-]");
        }
        if (state.VehicleBlockRespawnDays != defaults.VehicleBlockRespawnDays)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthVehicleBlockRespawn") + ":[-] "
                + (state.VehicleBlockRespawnDays == 0 ? Localization.Get("xuiOff") : state.VehicleBlockRespawnDays + " " + Localization.Get("xuiRebirthDays")) + "[-]");
        }

        if (state.ProtectCrate != defaults.ProtectCrate)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthProtectCrate") + ":[-] " + Localization.Get(state.ProtectCrate ? "xuiYes" : "xuiNo") + "[-]");

        if (state.AdvancedFarming != defaults.AdvancedFarming)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthAdvancedFarming") + ":[-] "
                + "[E41215]"
                + Localization.Get(state.AdvancedFarming ? "xuiYes" : "xuiNo") + "[-]");
        }
        if (state.RemoteResources != defaults.RemoteResources)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthRemoteResources") + ":[-] "
                + "[E41215]"
                + Localization.Get(state.RemoteResources ? "xuiYes" : "xuiNo") + "[-]");
        }
        if (state.RemoteResourcesDistance != defaults.RemoteResourcesDistance)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthRemoteResourcesDistance") + ":[-] " + state.RemoteResourcesDistance + " m[-]");
        if (state.QuickStackDistance != defaults.QuickStackDistance)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthQuickStackDistance") + ":[-] " + state.QuickStackDistance + " m[-]");
        if (state.GuardDistance != defaults.GuardDistance)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthGuardDistance") + ":[-] " + state.GuardDistance + " m[-]");
        if (state.FullControlDistance != defaults.FullControlDistance)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthFullControlDistance") + ":[-] " + state.FullControlDistance + " m[-]");
        if (state.HuntingDistance != defaults.HuntingDistance)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthHuntingDistance") + ":[-] " + state.HuntingDistance + " m[-]");
        if (state.CompanionCardStyle != defaults.CompanionCardStyle)
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthCompanionCardStyle") + ":[-] " + Localization.Get(state.CompanionCardStyle == RebirthCompanionCardStyle.Simple ? "xuiRebirthCompanionCardsSimple" : "xuiRebirthCompanionCardsDefault") + "[-]");
        if (state.TargetNameColorR != defaults.TargetNameColorR || state.TargetNameColorG != defaults.TargetNameColorG || state.TargetNameColorB != defaults.TargetNameColorB)
        {
            string hex = state.TargetNameColorR.ToString("X2") + state.TargetNameColorG.ToString("X2") + state.TargetNameColorB.ToString("X2");
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthTargetNameColor") + ":[-] [" + hex + "]#" + hex + "[-]");
        }
        if (state.InstantBlockPickup != defaults.InstantBlockPickup)
        {
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthInstantBlockPickup") + ":[-] "
                + "[E41215]"
                + Localization.Get(GetInstantBlockPickupLocalizationKey(state.InstantBlockPickup)) + "[-]");
        }
        if (changes.Count == 0 && !string.Equals(RebirthSandboxOptionManager.Encode(state), RebirthSandboxOptionManager.Encode(defaults), StringComparison.Ordinal))
            changes.Add("[A0A0A0]" + Localization.Get("xuiRebirthOptions") + ":[-] " + Localization.Get("xuiRebirthChanged") + "[-]");
        changedOptions = string.Join("\n", changes.ToArray());

        IsDirty = true;
    }

    private static string GetInstantBlockPickupLocalizationKey(RebirthInstantBlockPickupMode mode)
    {
        switch (mode)
        {
            case RebirthInstantBlockPickupMode.None: return "xuiRebirthInstantBlockPickupNone";
            case RebirthInstantBlockPickupMode.Always: return "xuiRebirthInstantBlockPickupAlways";
            default: return "xuiRebirthInstantBlockPickupDefault";
        }
    }

    private void PopulateGroups(string selectedGroup)
    {
        groupIds.Clear();
        cbxPresetGroup.Elements.Clear();

        List<string> groups = RebirthSandboxOptionManager.Current.GetAllPresetGroups();
        for (int i = 0; i < groups.Count; i++)
        {
            string group = groups[i];
            groupIds.Add(group);
            cbxPresetGroup.Elements.Add(RebirthSandboxOptionManager.GetGroupDisplayName(group));
        }

        int selectedIndex = groupIds.IndexOf(selectedGroup);
        cbxPresetGroup.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }

    private void PopulatePresets(string group, string selectedPresetName, bool selectFirstAndApply)
    {
        if (string.IsNullOrEmpty(group) && cbxPresetGroup.SelectedIndex >= 0 && cbxPresetGroup.SelectedIndex < groupIds.Count)
            group = groupIds[cbxPresetGroup.SelectedIndex];

        visiblePresets.Clear();
        cbxPreset.Elements.Clear();

        List<RebirthSandboxPreset> groupPresets = RebirthSandboxOptionManager.Current.GetPresetsForGroup(group);
        for (int i = 0; i < groupPresets.Count; i++)
        {
            visiblePresets.Add(groupPresets[i]);
            cbxPreset.Elements.Add(groupPresets[i].DisplayName);
        }

        int selectedIndex = 0;
        if (!string.IsNullOrEmpty(selectedPresetName))
        {
            for (int i = 0; i < visiblePresets.Count; i++)
            {
                if (string.Equals(visiblePresets[i].Name, selectedPresetName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }
        cbxPreset.SelectedIndex = visiblePresets.Count > 0 ? selectedIndex : -1;

        if (selectFirstAndApply && visiblePresets.Count > 0)
            RebirthSandboxUiSession.SetFromPreset(visiblePresets[cbxPreset.SelectedIndex]);
    }

    private static string GetInfestedJobsModeKey(RebirthInfestedJobsMode mode)
    {
        switch (mode)
        {
            case RebirthInfestedJobsMode.Default: return "xuiRebirthInfestedJobsDefault";
            case RebirthInfestedJobsMode.Hide: return "xuiRebirthInfestedJobsHide";
            default: return "xuiRebirthInfestedJobsSurprise";
        }
    }

    private static string GetRepeatPoiPolicyKey(RebirthRepeatPoiPolicy policy)
    {
        switch (policy)
        {
            case RebirthRepeatPoiPolicy.Low: return "xuiRebirthRepeatPoiLow";
            case RebirthRepeatPoiPolicy.Medium: return "xuiRebirthRepeatPoiMedium";
            case RebirthRepeatPoiPolicy.High: return "xuiRebirthRepeatPoiHigh";
            case RebirthRepeatPoiPolicy.Unlimited: return "xuiRebirthRepeatPoiUnlimited";
            default: return "xuiRebirthRepeatPoiNone";
        }
    }

    private static string GetHealingOverlapThresholdLabel(RebirthHealingOverlapThreshold threshold)
    {
        int health = RebirthHealingOverlapThresholdPolicy.ToHealth(threshold);
        return health <= 0
            ? Localization.Get("xuiOff")
            : health.ToString() + " " + Localization.Get("xuiRebirthHealth");
    }

    private static string GetAlwaysStaggerModeKey(RebirthAlwaysStaggerMode mode)
    {
        switch (mode)
        {
            case RebirthAlwaysStaggerMode.Disabled: return "xuiRebirthAlwaysStaggerDisabled";
            case RebirthAlwaysStaggerMode.AllQualifyingHits: return "xuiRebirthAlwaysStaggerAllHits";
            default: return "xuiRebirthAlwaysStaggerHeadshotsOnly";
        }
    }


    private static string GetWeatherFogBehaviorKey(RebirthWeatherFogBehavior behavior)
    {
        switch (behavior)
        {
            case RebirthWeatherFogBehavior.Static: return "xuiRebirthWeatherFogStatic";
            case RebirthWeatherFogBehavior.Disabled: return "xuiRebirthWeatherFogDisabled";
            default: return "xuiRebirthWeatherFogDynamic";
        }
    }

    private static string GetWeatherFogIntensityKey(RebirthWeatherFogIntensity intensity)
    {
        switch (intensity)
        {
            case RebirthWeatherFogIntensity.None: return "xuiRebirthWeatherFogNone";
            case RebirthWeatherFogIntensity.VeryLow: return "xuiRebirthWeatherFogVeryLow";
            case RebirthWeatherFogIntensity.Low: return "xuiRebirthWeatherFogLow";
            case RebirthWeatherFogIntensity.Heavy: return "xuiRebirthWeatherFogHeavy";
            case RebirthWeatherFogIntensity.VeryHeavy: return "xuiRebirthWeatherFogVeryHeavy";
            default: return "xuiRebirthWeatherFogNormal";
        }
    }
    private static string GetPoiRiskModeKey(RebirthPoiRiskMode mode)
    {
        switch (mode)
        {
            case RebirthPoiRiskMode.None: return "xuiRebirthPoiRiskNone";
            case RebirthPoiRiskMode.Low: return "xuiRebirthPoiRiskLow";
            case RebirthPoiRiskMode.High: return "xuiRebirthPoiRiskHigh";
            default: return "xuiRebirthPoiRiskMedium";
        }
    }

    private static string GetPoiSenseScheduleKey(RebirthPoiSenseSchedule value)
    {
        switch (value)
        {
            case RebirthPoiSenseSchedule.DayOnly: return "xuiRebirthPoiSenseDayOnly";
            case RebirthPoiSenseSchedule.NightOnly: return "xuiRebirthPoiSenseNightOnly";
            case RebirthPoiSenseSchedule.Always: return "xuiRebirthPoiSenseAlways";
            default: return "xuiRebirthPoiSenseNever";
        }
    }

    private static string GetPoiSenseIntensityKey(RebirthPoiSenseIntensity value)
    {
        switch (value)
        {
            case RebirthPoiSenseIntensity.Low: return "xuiRebirthPoiSenseLow";
            case RebirthPoiSenseIntensity.High: return "xuiRebirthPoiSenseHigh";
            default: return "xuiRebirthPoiSenseMedium";
        }
    }

}
