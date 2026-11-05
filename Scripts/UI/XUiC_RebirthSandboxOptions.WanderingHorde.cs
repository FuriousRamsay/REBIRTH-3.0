using UnityEngine;

#nullable disable

public partial class XUiC_RebirthSandboxOptions
{
    [XuiBindComponent("cbxWanderingHordeFrequency", true)] public readonly XUiC_ComboBoxList<string> cbxWanderingHordeFrequency;
    [XuiBindComponent("btnWanderingHordeFrequencyRandomizerLock", true)] public readonly XUiC_Button btnWanderingHordeFrequencyRandomizerLock;
    private RebirthWanderingHordeFrequency workingWanderingHordeFrequency = RebirthWanderingHordeFrequency.Random;
    [XuiXmlBinding("rebirth_wandering_horde_frequency_default")] public bool WanderingHordeFrequencyDefault => workingWanderingHordeFrequency == RebirthWanderingHordeFrequency.Random;
    [XuiXmlBinding("rebirth_wandering_horde_frequency_changed")] public bool WanderingHordeFrequencyChanged => GetBaselineState().WanderingHordeFrequency != workingWanderingHordeFrequency;
    [XuiXmlBinding("rebirth_wandering_horde_frequency_randomizer_locked")] public bool WanderingHordeFrequencyLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.WanderingHordeFrequency);
    [XuiXmlBinding("rebirth_wandering_horde_frequency_randomizer_lock_sprite")] public string WanderingHordeFrequencyLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WanderingHordeFrequency);
    [XuiXmlBinding("rebirth_wandering_horde_frequency_randomizer_lock_is_minimum")] public bool WanderingHordeFrequencyMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WanderingHordeFrequency);
    [XuiXmlBinding("rebirth_wandering_horde_frequency_randomizer_lock_is_maximum")] public bool WanderingHordeFrequencyMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WanderingHordeFrequency);
    [XuiBindEvent("OnPress", "btnWanderingHordeFrequencyRandomizerLock")]
    public void WanderingHordeFrequencyLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.WanderingHordeFrequency, cbxWanderingHordeFrequency, (int)workingWanderingHordeFrequency, sender); }
    public void WanderingHordeFrequency_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWanderingHordeFrequency == null) return;
        workingWanderingHordeFrequency = (RebirthWanderingHordeFrequency)Mathf.Clamp(cbxWanderingHordeFrequency.SelectedIndex, 0, 8);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxWanderingHordeSize", true)] public readonly XUiC_ComboBoxList<string> cbxWanderingHordeSize;
    [XuiBindComponent("btnWanderingHordeSizeRandomizerLock", true)] public readonly XUiC_Button btnWanderingHordeSizeRandomizerLock;
    private RebirthWanderingHordeSize workingWanderingHordeSize = RebirthWanderingHordeSize.Random;
    [XuiXmlBinding("rebirth_wandering_horde_size_default")] public bool WanderingHordeSizeDefault => workingWanderingHordeSize == RebirthWanderingHordeSize.Random;
    [XuiXmlBinding("rebirth_wandering_horde_size_changed")] public bool WanderingHordeSizeChanged => GetBaselineState().WanderingHordeSize != workingWanderingHordeSize;
    [XuiXmlBinding("rebirth_wandering_horde_size_randomizer_locked")] public bool WanderingHordeSizeLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.WanderingHordeSize);
    [XuiXmlBinding("rebirth_wandering_horde_size_randomizer_lock_sprite")] public string WanderingHordeSizeLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WanderingHordeSize);
    [XuiXmlBinding("rebirth_wandering_horde_size_randomizer_lock_is_minimum")] public bool WanderingHordeSizeMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WanderingHordeSize);
    [XuiXmlBinding("rebirth_wandering_horde_size_randomizer_lock_is_maximum")] public bool WanderingHordeSizeMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WanderingHordeSize);
    [XuiBindEvent("OnPress", "btnWanderingHordeSizeRandomizerLock")]
    public void WanderingHordeSizeLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.WanderingHordeSize, cbxWanderingHordeSize, (int)workingWanderingHordeSize, sender); }
    public void WanderingHordeSize_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWanderingHordeSize == null) return;
        workingWanderingHordeSize = (RebirthWanderingHordeSize)Mathf.Clamp(cbxWanderingHordeSize.SelectedIndex, 0, 9);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxWanderingHerdRestriction", true)] public readonly XUiC_ComboBoxList<string> cbxWanderingHerdRestriction;
    [XuiBindComponent("btnWanderingHerdRestrictionRandomizerLock", true)] public readonly XUiC_Button btnWanderingHerdRestrictionRandomizerLock;
    private RebirthWanderingHerdRestriction workingWanderingHerdRestriction = RebirthWanderingHerdRestriction.Highest;
    [XuiXmlBinding("rebirth_wandering_herd_restriction_default")] public bool WanderingHerdRestrictionDefault => workingWanderingHerdRestriction == RebirthWanderingHerdRestriction.Highest;
    [XuiXmlBinding("rebirth_wandering_herd_restriction_changed")] public bool WanderingHerdRestrictionChanged => GetBaselineState().WanderingHerdRestriction != workingWanderingHerdRestriction;
    [XuiXmlBinding("rebirth_wandering_herd_restriction_randomizer_locked")] public bool WanderingHerdRestrictionLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.WanderingHerdRestriction);
    [XuiXmlBinding("rebirth_wandering_herd_restriction_randomizer_lock_sprite")] public string WanderingHerdRestrictionLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WanderingHerdRestriction);
    [XuiXmlBinding("rebirth_wandering_herd_restriction_randomizer_lock_is_minimum")] public bool WanderingHerdRestrictionMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WanderingHerdRestriction);
    [XuiXmlBinding("rebirth_wandering_herd_restriction_randomizer_lock_is_maximum")] public bool WanderingHerdRestrictionMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WanderingHerdRestriction);
    [XuiBindEvent("OnPress", "btnWanderingHerdRestrictionRandomizerLock")]
    public void WanderingHerdRestrictionLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.WanderingHerdRestriction, cbxWanderingHerdRestriction, (int)workingWanderingHerdRestriction, sender); }
    public void WanderingHerdRestriction_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWanderingHerdRestriction == null) return;
        workingWanderingHerdRestriction = (RebirthWanderingHerdRestriction)Mathf.Clamp(cbxWanderingHerdRestriction.SelectedIndex, 0, 3);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxDemolisherMode", true)] public readonly XUiC_ComboBoxList<string> cbxDemolisherMode;
    [XuiBindComponent("btnDemolisherModeRandomizerLock", true)] public readonly XUiC_Button btnDemolisherModeRandomizerLock;
    private RebirthDemolisherMode workingDemolisherMode = RebirthDemolisherMode.ReducedNotInPois;
    [XuiXmlBinding("rebirth_demolisher_mode_default")] public bool DemolisherModeDefault => workingDemolisherMode == RebirthDemolisherMode.ReducedNotInPois;
    [XuiXmlBinding("rebirth_demolisher_mode_changed")] public bool DemolisherModeChanged => GetBaselineState().DemolisherMode != workingDemolisherMode;
    [XuiXmlBinding("rebirth_demolisher_mode_randomizer_locked")] public bool DemolisherModeLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.DemolisherMode);
    [XuiXmlBinding("rebirth_demolisher_mode_randomizer_lock_sprite")] public string DemolisherModeLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.DemolisherMode);
    [XuiXmlBinding("rebirth_demolisher_mode_randomizer_lock_is_minimum")] public bool DemolisherModeMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.DemolisherMode);
    [XuiXmlBinding("rebirth_demolisher_mode_randomizer_lock_is_maximum")] public bool DemolisherModeMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.DemolisherMode);
    [XuiBindEvent("OnPress", "btnDemolisherModeRandomizerLock")]
    public void DemolisherModeLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.DemolisherMode, cbxDemolisherMode, (int)workingDemolisherMode, sender); }
    public void DemolisherMode_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxDemolisherMode == null) return;
        workingDemolisherMode = (RebirthDemolisherMode)Mathf.Clamp(cbxDemolisherMode.SelectedIndex, 0, 5);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxWanderingHerdChance", true)] public readonly XUiC_ComboBoxList<string> cbxWanderingHerdChance;
    [XuiBindComponent("btnWanderingHerdChanceRandomizerLock", true)] public readonly XUiC_Button btnWanderingHerdChanceRandomizerLock;
    private static readonly int[] WanderingHerdChanceValues = new[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
    private int workingWanderingHerdChance = 30;
    [XuiXmlBinding("rebirth_wandering_herd_chance_default")] public bool WanderingHerdChanceDefault => workingWanderingHerdChance == 30;
    [XuiXmlBinding("rebirth_wandering_herd_chance_changed")] public bool WanderingHerdChanceChanged => GetBaselineState().WanderingHerdChance != workingWanderingHerdChance;
    [XuiXmlBinding("rebirth_wandering_herd_chance_randomizer_locked")] public bool WanderingHerdChanceLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.WanderingHerdChance);
    [XuiXmlBinding("rebirth_wandering_herd_chance_randomizer_lock_sprite")] public string WanderingHerdChanceLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WanderingHerdChance);
    [XuiXmlBinding("rebirth_wandering_herd_chance_randomizer_lock_is_minimum")] public bool WanderingHerdChanceMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WanderingHerdChance);
    [XuiXmlBinding("rebirth_wandering_herd_chance_randomizer_lock_is_maximum")] public bool WanderingHerdChanceMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WanderingHerdChance);
    [XuiBindEvent("OnPress", "btnWanderingHerdChanceRandomizerLock")]
    public void WanderingHerdChanceLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.WanderingHerdChance, cbxWanderingHerdChance, workingWanderingHerdChance / 10, sender); }
    public void WanderingHerdChance_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWanderingHerdChance == null) return;
        workingWanderingHerdChance = Mathf.Clamp(cbxWanderingHerdChance.SelectedIndex, 0, 10) * 10;
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxWanderingHerdAfterHordeNight", true)] public readonly XUiC_ComboBoxList<string> cbxWanderingHerdAfterHordeNight;
    [XuiBindComponent("btnWanderingHerdAfterHordeNightRandomizerLock", true)] public readonly XUiC_Button btnWanderingHerdAfterHordeNightRandomizerLock;
    private static readonly int[] WanderingHerdAfterHordeNightValues = new[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
    private int workingWanderingHerdAfterHordeNight = 50;
    [XuiXmlBinding("rebirth_wandering_herd_after_horde_night_default")] public bool WanderingHerdAfterHordeNightDefault => workingWanderingHerdAfterHordeNight == 50;
    [XuiXmlBinding("rebirth_wandering_herd_after_horde_night_changed")] public bool WanderingHerdAfterHordeNightChanged => GetBaselineState().WanderingHerdAfterHordeNight != workingWanderingHerdAfterHordeNight;
    [XuiXmlBinding("rebirth_wandering_herd_after_horde_night_randomizer_locked")] public bool WanderingHerdAfterHordeNightLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.WanderingHerdAfterHordeNight);
    [XuiXmlBinding("rebirth_wandering_herd_after_horde_night_randomizer_lock_sprite")] public string WanderingHerdAfterHordeNightLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WanderingHerdAfterHordeNight);
    [XuiXmlBinding("rebirth_wandering_herd_after_horde_night_randomizer_lock_is_minimum")] public bool WanderingHerdAfterHordeNightMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WanderingHerdAfterHordeNight);
    [XuiXmlBinding("rebirth_wandering_herd_after_horde_night_randomizer_lock_is_maximum")] public bool WanderingHerdAfterHordeNightMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WanderingHerdAfterHordeNight);
    [XuiBindEvent("OnPress", "btnWanderingHerdAfterHordeNightRandomizerLock")]
    public void WanderingHerdAfterHordeNightLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.WanderingHerdAfterHordeNight, cbxWanderingHerdAfterHordeNight, workingWanderingHerdAfterHordeNight / 10, sender); }
    public void WanderingHerdAfterHordeNight_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWanderingHerdAfterHordeNight == null) return;
        workingWanderingHerdAfterHordeNight = Mathf.Clamp(cbxWanderingHerdAfterHordeNight.SelectedIndex, 0, 10) * 10;
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxWanderingHerdDuration", true)] public readonly XUiC_ComboBoxList<string> cbxWanderingHerdDuration;
    [XuiBindComponent("btnWanderingHerdDurationRandomizerLock", true)] public readonly XUiC_Button btnWanderingHerdDurationRandomizerLock;
    private static readonly int[] WanderingHerdDurationValues = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
    private int workingWanderingHerdDuration = 5;
    [XuiXmlBinding("rebirth_wandering_herd_duration_default")] public bool WanderingHerdDurationDefault => workingWanderingHerdDuration == 5;
    [XuiXmlBinding("rebirth_wandering_herd_duration_changed")] public bool WanderingHerdDurationChanged => GetBaselineState().WanderingHerdDuration != workingWanderingHerdDuration;
    [XuiXmlBinding("rebirth_wandering_herd_duration_randomizer_locked")] public bool WanderingHerdDurationLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.WanderingHerdDuration);
    [XuiXmlBinding("rebirth_wandering_herd_duration_randomizer_lock_sprite")] public string WanderingHerdDurationLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WanderingHerdDuration);
    [XuiXmlBinding("rebirth_wandering_herd_duration_randomizer_lock_is_minimum")] public bool WanderingHerdDurationMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WanderingHerdDuration);
    [XuiXmlBinding("rebirth_wandering_herd_duration_randomizer_lock_is_maximum")] public bool WanderingHerdDurationMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WanderingHerdDuration);
    [XuiBindEvent("OnPress", "btnWanderingHerdDurationRandomizerLock")]
    public void WanderingHerdDurationLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.WanderingHerdDuration, cbxWanderingHerdDuration, workingWanderingHerdDuration - 1, sender); }
    public void WanderingHerdDuration_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWanderingHerdDuration == null) return;
        workingWanderingHerdDuration = 1 + Mathf.Clamp(cbxWanderingHerdDuration.SelectedIndex, 0, 7);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxWanderingHordeDespawn", true)] public readonly XUiC_ComboBoxList<string> cbxWanderingHordeDespawn;
    [XuiBindComponent("btnWanderingHordeDespawnRandomizerLock", true)] public readonly XUiC_Button btnWanderingHordeDespawnRandomizerLock;
    private bool workingWanderingHordeDespawn = false;
    [XuiXmlBinding("rebirth_wandering_horde_despawn_default")] public bool WanderingHordeDespawnDefault => workingWanderingHordeDespawn == false;
    [XuiXmlBinding("rebirth_wandering_horde_despawn_changed")] public bool WanderingHordeDespawnChanged => GetBaselineState().WanderingHordeDespawn != workingWanderingHordeDespawn;
    [XuiXmlBinding("rebirth_wandering_horde_despawn_randomizer_locked")] public bool WanderingHordeDespawnLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.WanderingHordeDespawn);
    [XuiXmlBinding("rebirth_wandering_horde_despawn_randomizer_lock_sprite")] public string WanderingHordeDespawnLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.WanderingHordeDespawn);
    [XuiXmlBinding("rebirth_wandering_horde_despawn_randomizer_lock_is_minimum")] public bool WanderingHordeDespawnMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.WanderingHordeDespawn);
    [XuiXmlBinding("rebirth_wandering_horde_despawn_randomizer_lock_is_maximum")] public bool WanderingHordeDespawnMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.WanderingHordeDespawn);
    [XuiBindEvent("OnPress", "btnWanderingHordeDespawnRandomizerLock")]
    public void WanderingHordeDespawnLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.WanderingHordeDespawn, cbxWanderingHordeDespawn, workingWanderingHordeDespawn ? 1 : 0, sender); }
    public void WanderingHordeDespawn_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxWanderingHordeDespawn == null) return;
        workingWanderingHordeDespawn = cbxWanderingHordeDespawn.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }
    private void PopulateWanderingHordeOptions()
    {
        PopulateEnum(cbxWanderingHordeFrequency, workingWanderingHordeFrequency, "xuiRebirthWanderingHordeFrequencyOff", "xuiRebirthWanderingHordeFrequencyDefault", "xuiRebirthWanderingHordeFrequencyHigh", "xuiRebirthWanderingHordeFrequencyHigher", "xuiRebirthWanderingHordeFrequencyHighest", "xuiRebirthWanderingHordeFrequencyGradualSlower", "xuiRebirthWanderingHordeFrequencyGradual", "xuiRebirthWanderingHordeFrequencyGradualFaster", "xuiRebirthWanderingHordeFrequencyRandom");
        PopulateEnum(cbxWanderingHordeSize, workingWanderingHordeSize, "xuiRebirthWanderingHordeSizeX1", "xuiRebirthWanderingHordeSizeX2", "xuiRebirthWanderingHordeSizeX3", "xuiRebirthWanderingHordeSizeX4", "xuiRebirthWanderingHordeSizeX5", "xuiRebirthWanderingHordeSizeX6", "xuiRebirthWanderingHordeSizeGradualSlower", "xuiRebirthWanderingHordeSizeGradual", "xuiRebirthWanderingHordeSizeGradualFaster", "xuiRebirthWanderingHordeSizeRandom");
        PopulateEnum(cbxWanderingHerdRestriction, workingWanderingHerdRestriction, "xuiRebirthWanderingHerdRestrictionLow", "xuiRebirthWanderingHerdRestrictionNormal", "xuiRebirthWanderingHerdRestrictionHigh", "xuiRebirthWanderingHerdRestrictionHighest");
        PopulateEnum(cbxDemolisherMode, workingDemolisherMode, "xuiRebirthDemolisherModeNever", "xuiRebirthDemolisherModeReduced", "xuiRebirthDemolisherModeReducedNotInPois", "xuiRebirthDemolisherModeNotInPois", "xuiRebirthDemolisherModeNotHordeNight", "xuiRebirthDemolisherModeAlways");
        if (cbxWanderingHerdChance != null) { cbxWanderingHerdChance.Elements.Clear(); foreach (int value in WanderingHerdChanceValues) cbxWanderingHerdChance.Elements.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"); cbxWanderingHerdChance.SelectedIndex = workingWanderingHerdChance / 10; }
        if (cbxWanderingHerdAfterHordeNight != null) { cbxWanderingHerdAfterHordeNight.Elements.Clear(); foreach (int value in WanderingHerdAfterHordeNightValues) cbxWanderingHerdAfterHordeNight.Elements.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"); cbxWanderingHerdAfterHordeNight.SelectedIndex = workingWanderingHerdAfterHordeNight / 10; }
        if (cbxWanderingHerdDuration != null) { cbxWanderingHerdDuration.Elements.Clear(); foreach (int value in WanderingHerdDurationValues) cbxWanderingHerdDuration.Elements.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture) + " min"); cbxWanderingHerdDuration.SelectedIndex = workingWanderingHerdDuration - 1; }
        PopulateBoolean(cbxWanderingHordeDespawn, workingWanderingHordeDespawn);
    }
}
