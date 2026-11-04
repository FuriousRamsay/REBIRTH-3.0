using UnityEngine;

#nullable disable

public partial class XUiC_RebirthSandboxOptions
{
    [XuiBindComponent("cbxSeekerMode", true)] public readonly XUiC_ComboBoxList<string> cbxSeekerMode;
    [XuiBindComponent("btnSeekerModeRandomizerLock", true)] public readonly XUiC_Button btnSeekerModeRandomizerLock;
    private RebirthSeekerMode workingSeekerMode = RebirthSeekerMode.Always;
    [XuiXmlBinding("rebirth_seeker_mode_default")] public bool SeekerModeDefault => workingSeekerMode == RebirthSeekerMode.Always;
    [XuiXmlBinding("rebirth_seeker_mode_changed")] public bool SeekerModeChanged => GetBaselineState().SeekerMode != workingSeekerMode;
    [XuiXmlBinding("rebirth_seeker_mode_randomizer_locked")] public bool SeekerModeLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.SeekerMode);
    [XuiXmlBinding("rebirth_seeker_mode_randomizer_lock_sprite")] public string SeekerModeLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SeekerMode);
    [XuiXmlBinding("rebirth_seeker_mode_randomizer_lock_is_minimum")] public bool SeekerModeMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SeekerMode);
    [XuiXmlBinding("rebirth_seeker_mode_randomizer_lock_is_maximum")] public bool SeekerModeMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SeekerMode);
    [XuiBindEvent("OnPress", "btnSeekerModeRandomizerLock")]
    public void SeekerModeLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.SeekerMode, cbxSeekerMode, (int)workingSeekerMode, sender); }
    public void SeekerMode_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSeekerMode == null) return;
        workingSeekerMode = (RebirthSeekerMode)Mathf.Clamp(cbxSeekerMode.SelectedIndex, 0, 3);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxSeekerRewards", true)] public readonly XUiC_ComboBoxList<string> cbxSeekerRewards;
    [XuiBindComponent("btnSeekerRewardsRandomizerLock", true)] public readonly XUiC_Button btnSeekerRewardsRandomizerLock;
    private bool workingSeekerRewards = true;
    [XuiXmlBinding("rebirth_seeker_rewards_default")] public bool SeekerRewardsDefault => workingSeekerRewards == true;
    [XuiXmlBinding("rebirth_seeker_rewards_changed")] public bool SeekerRewardsChanged => GetBaselineState().SeekerRewards != workingSeekerRewards;
    [XuiXmlBinding("rebirth_seeker_rewards_randomizer_locked")] public bool SeekerRewardsLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.SeekerRewards);
    [XuiXmlBinding("rebirth_seeker_rewards_randomizer_lock_sprite")] public string SeekerRewardsLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SeekerRewards);
    [XuiXmlBinding("rebirth_seeker_rewards_randomizer_lock_is_minimum")] public bool SeekerRewardsMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SeekerRewards);
    [XuiXmlBinding("rebirth_seeker_rewards_randomizer_lock_is_maximum")] public bool SeekerRewardsMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SeekerRewards);
    [XuiBindEvent("OnPress", "btnSeekerRewardsRandomizerLock")]
    public void SeekerRewardsLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.SeekerRewards, cbxSeekerRewards, workingSeekerRewards ? 1 : 0, sender); }
    public void SeekerRewards_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSeekerRewards == null) return;
        workingSeekerRewards = cbxSeekerRewards.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxSeekerNotifications", true)] public readonly XUiC_ComboBoxList<string> cbxSeekerNotifications;
    [XuiBindComponent("btnSeekerNotificationsRandomizerLock", true)] public readonly XUiC_Button btnSeekerNotificationsRandomizerLock;
    private bool workingSeekerNotifications = true;
    [XuiXmlBinding("rebirth_seeker_notifications_default")] public bool SeekerNotificationsDefault => workingSeekerNotifications == true;
    [XuiXmlBinding("rebirth_seeker_notifications_changed")] public bool SeekerNotificationsChanged => GetBaselineState().SeekerNotifications != workingSeekerNotifications;
    [XuiXmlBinding("rebirth_seeker_notifications_randomizer_locked")] public bool SeekerNotificationsLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.SeekerNotifications);
    [XuiXmlBinding("rebirth_seeker_notifications_randomizer_lock_sprite")] public string SeekerNotificationsLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SeekerNotifications);
    [XuiXmlBinding("rebirth_seeker_notifications_randomizer_lock_is_minimum")] public bool SeekerNotificationsMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SeekerNotifications);
    [XuiXmlBinding("rebirth_seeker_notifications_randomizer_lock_is_maximum")] public bool SeekerNotificationsMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SeekerNotifications);
    [XuiBindEvent("OnPress", "btnSeekerNotificationsRandomizerLock")]
    public void SeekerNotificationsLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.SeekerNotifications, cbxSeekerNotifications, workingSeekerNotifications ? 1 : 0, sender); }
    public void SeekerNotifications_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSeekerNotifications == null) return;
        workingSeekerNotifications = cbxSeekerNotifications.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }
    private static readonly int[] SeekerFrequencyValues = new[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 };
    [XuiBindComponent("cbxSeekerFrequency", true)] public readonly XUiC_ComboBoxList<string> cbxSeekerFrequency;
    [XuiBindComponent("btnSeekerFrequencyRandomizerLock", true)] public readonly XUiC_Button btnSeekerFrequencyRandomizerLock;
    private int workingSeekerFrequency = 100;
    [XuiXmlBinding("rebirth_seeker_frequency_default")] public bool SeekerFrequencyDefault => workingSeekerFrequency == 100;
    [XuiXmlBinding("rebirth_seeker_frequency_changed")] public bool SeekerFrequencyChanged => GetBaselineState().SeekerFrequency != workingSeekerFrequency;
    [XuiXmlBinding("rebirth_seeker_frequency_randomizer_locked")] public bool SeekerFrequencyLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.SeekerFrequency);
    [XuiXmlBinding("rebirth_seeker_frequency_randomizer_lock_sprite")] public string SeekerFrequencyLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SeekerFrequency);
    [XuiXmlBinding("rebirth_seeker_frequency_randomizer_lock_is_minimum")] public bool SeekerFrequencyMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SeekerFrequency);
    [XuiXmlBinding("rebirth_seeker_frequency_randomizer_lock_is_maximum")] public bool SeekerFrequencyMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SeekerFrequency);
    [XuiBindEvent("OnPress", "btnSeekerFrequencyRandomizerLock")]
    public void SeekerFrequencyLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.SeekerFrequency, cbxSeekerFrequency, workingSeekerFrequency / 10, sender); }
    public void SeekerFrequency_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSeekerFrequency == null) return;
        workingSeekerFrequency = Mathf.Clamp(cbxSeekerFrequency.SelectedIndex, 0, 20) * 10;
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxSeekerRestriction", true)] public readonly XUiC_ComboBoxList<string> cbxSeekerRestriction;
    [XuiBindComponent("btnSeekerRestrictionRandomizerLock", true)] public readonly XUiC_Button btnSeekerRestrictionRandomizerLock;
    private RebirthSeekerRestriction workingSeekerRestriction = RebirthSeekerRestriction.Anywhere;
    [XuiXmlBinding("rebirth_seeker_restriction_default")] public bool SeekerRestrictionDefault => workingSeekerRestriction == RebirthSeekerRestriction.Anywhere;
    [XuiXmlBinding("rebirth_seeker_restriction_changed")] public bool SeekerRestrictionChanged => GetBaselineState().SeekerRestriction != workingSeekerRestriction;
    [XuiXmlBinding("rebirth_seeker_restriction_randomizer_locked")] public bool SeekerRestrictionLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.SeekerRestriction);
    [XuiXmlBinding("rebirth_seeker_restriction_randomizer_lock_sprite")] public string SeekerRestrictionLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SeekerRestriction);
    [XuiXmlBinding("rebirth_seeker_restriction_randomizer_lock_is_minimum")] public bool SeekerRestrictionMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SeekerRestriction);
    [XuiXmlBinding("rebirth_seeker_restriction_randomizer_lock_is_maximum")] public bool SeekerRestrictionMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SeekerRestriction);
    [XuiBindEvent("OnPress", "btnSeekerRestrictionRandomizerLock")]
    public void SeekerRestrictionLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.SeekerRestriction, cbxSeekerRestriction, (int)workingSeekerRestriction, sender); }
    public void SeekerRestriction_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSeekerRestriction == null) return;
        workingSeekerRestriction = (RebirthSeekerRestriction)Mathf.Clamp(cbxSeekerRestriction.SelectedIndex, 0, 5);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxSeekerHordeDay", true)] public readonly XUiC_ComboBoxList<string> cbxSeekerHordeDay;
    [XuiBindComponent("btnSeekerHordeDayRandomizerLock", true)] public readonly XUiC_Button btnSeekerHordeDayRandomizerLock;
    private bool workingSeekerHordeDay = true;
    [XuiXmlBinding("rebirth_seeker_horde_day_default")] public bool SeekerHordeDayDefault => workingSeekerHordeDay == true;
    [XuiXmlBinding("rebirth_seeker_horde_day_changed")] public bool SeekerHordeDayChanged => GetBaselineState().SeekerHordeDay != workingSeekerHordeDay;
    [XuiXmlBinding("rebirth_seeker_horde_day_randomizer_locked")] public bool SeekerHordeDayLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.SeekerHordeDay);
    [XuiXmlBinding("rebirth_seeker_horde_day_randomizer_lock_sprite")] public string SeekerHordeDayLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.SeekerHordeDay);
    [XuiXmlBinding("rebirth_seeker_horde_day_randomizer_lock_is_minimum")] public bool SeekerHordeDayMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.SeekerHordeDay);
    [XuiXmlBinding("rebirth_seeker_horde_day_randomizer_lock_is_maximum")] public bool SeekerHordeDayMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.SeekerHordeDay);
    [XuiBindEvent("OnPress", "btnSeekerHordeDayRandomizerLock")]
    public void SeekerHordeDayLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.SeekerHordeDay, cbxSeekerHordeDay, workingSeekerHordeDay ? 1 : 0, sender); }
    public void SeekerHordeDay_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxSeekerHordeDay == null) return;
        workingSeekerHordeDay = cbxSeekerHordeDay.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }
    private void PopulateSeekerOptions()
    {
        PopulateEnum(cbxSeekerMode, workingSeekerMode, "xuiSeekerNever", "xuiSeekerNotQuesting", "xuiSeekerNotHordeNight", "xuiSeekerAlways");
        PopulateBoolean(cbxSeekerRewards, workingSeekerRewards);
        PopulateBoolean(cbxSeekerNotifications, workingSeekerNotifications);
        if (cbxSeekerFrequency != null) { cbxSeekerFrequency.Elements.Clear(); foreach (int value in SeekerFrequencyValues) cbxSeekerFrequency.Elements.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"); cbxSeekerFrequency.SelectedIndex = workingSeekerFrequency / 10; }
        PopulateEnum(cbxSeekerRestriction, workingSeekerRestriction, "xuiSeekerRestrictionAnywhere", "xuiSeekerRestrictionQuesting", "xuiSeekerRestrictionNotQuesting", "xuiSeekerRestrictionOutdoors", "xuiSeekerRestrictionOutdoorsNotQuesting", "xuiSeekerRestrictionOff");
        PopulateBoolean(cbxSeekerHordeDay, workingSeekerHordeDay);
    }
}
