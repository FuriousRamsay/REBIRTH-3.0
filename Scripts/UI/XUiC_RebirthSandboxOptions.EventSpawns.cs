using UnityEngine;

#nullable disable

public partial class XUiC_RebirthSandboxOptions
{
    [XuiBindComponent("cbxKamikazeMode", true)] public readonly XUiC_ComboBoxList<string> cbxKamikazeMode;
    [XuiBindComponent("btnKamikazeModeRandomizerLock", true)] public readonly XUiC_Button btnKamikazeModeRandomizerLock;
    private RebirthKamikazeMode workingKamikazeMode = RebirthKamikazeMode.NotInPois;
    [XuiXmlBinding("rebirth_kamikaze_mode_default")] public bool KamikazeModeDefault => workingKamikazeMode == RebirthKamikazeMode.NotInPois;
    [XuiXmlBinding("rebirth_kamikaze_mode_changed")] public bool KamikazeModeChanged => GetBaselineState().KamikazeMode != workingKamikazeMode;
    [XuiXmlBinding("rebirth_kamikaze_mode_randomizer_locked")] public bool KamikazeModeLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.KamikazeMode);
    [XuiXmlBinding("rebirth_kamikaze_mode_randomizer_lock_sprite")] public string KamikazeModeLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.KamikazeMode);
    [XuiXmlBinding("rebirth_kamikaze_mode_randomizer_lock_is_minimum")] public bool KamikazeModeMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.KamikazeMode);
    [XuiXmlBinding("rebirth_kamikaze_mode_randomizer_lock_is_maximum")] public bool KamikazeModeMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.KamikazeMode);
    [XuiBindEvent("OnPress", "btnKamikazeModeRandomizerLock")]
    public void KamikazeModeLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.KamikazeMode, cbxKamikazeMode, (int)workingKamikazeMode, sender); }
    public void KamikazeMode_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxKamikazeMode == null) return;
        workingKamikazeMode = (RebirthKamikazeMode)Mathf.Clamp(cbxKamikazeMode.SelectedIndex, 0, 4);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxTornadoMode", true)] public readonly XUiC_ComboBoxList<string> cbxTornadoMode;
    [XuiBindComponent("btnTornadoModeRandomizerLock", true)] public readonly XUiC_Button btnTornadoModeRandomizerLock;
    private RebirthTornadoMode workingTornadoMode = RebirthTornadoMode.HordeNightOnly;
    [XuiXmlBinding("rebirth_tornado_mode_default")] public bool TornadoModeDefault => workingTornadoMode == RebirthTornadoMode.HordeNightOnly;
    [XuiXmlBinding("rebirth_tornado_mode_changed")] public bool TornadoModeChanged => GetBaselineState().TornadoMode != workingTornadoMode;
    [XuiXmlBinding("rebirth_tornado_mode_randomizer_locked")] public bool TornadoModeLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.TornadoMode);
    [XuiXmlBinding("rebirth_tornado_mode_randomizer_lock_sprite")] public string TornadoModeLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.TornadoMode);
    [XuiXmlBinding("rebirth_tornado_mode_randomizer_lock_is_minimum")] public bool TornadoModeMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.TornadoMode);
    [XuiXmlBinding("rebirth_tornado_mode_randomizer_lock_is_maximum")] public bool TornadoModeMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.TornadoMode);
    [XuiBindEvent("OnPress", "btnTornadoModeRandomizerLock")]
    public void TornadoModeLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.TornadoMode, cbxTornadoMode, (int)workingTornadoMode, sender); }
    public void TornadoMode_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxTornadoMode == null) return;
        workingTornadoMode = (RebirthTornadoMode)Mathf.Clamp(cbxTornadoMode.SelectedIndex, 0, 3);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxScreamerNights", true)] public readonly XUiC_ComboBoxList<string> cbxScreamerNights;
    [XuiBindComponent("btnScreamerNightsRandomizerLock", true)] public readonly XUiC_Button btnScreamerNightsRandomizerLock;
    private RebirthScreamerNightMode workingScreamerNights = RebirthScreamerNightMode.None;
    [XuiXmlBinding("rebirth_screamer_nights_default")] public bool ScreamerNightsDefault => workingScreamerNights == RebirthScreamerNightMode.None;
    [XuiXmlBinding("rebirth_screamer_nights_changed")] public bool ScreamerNightsChanged => GetBaselineState().ScreamerNights != workingScreamerNights;
    [XuiXmlBinding("rebirth_screamer_nights_randomizer_locked")] public bool ScreamerNightsLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.ScreamerNights);
    [XuiXmlBinding("rebirth_screamer_nights_randomizer_lock_sprite")] public string ScreamerNightsLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.ScreamerNights);
    [XuiXmlBinding("rebirth_screamer_nights_randomizer_lock_is_minimum")] public bool ScreamerNightsMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.ScreamerNights);
    [XuiXmlBinding("rebirth_screamer_nights_randomizer_lock_is_maximum")] public bool ScreamerNightsMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.ScreamerNights);
    [XuiBindEvent("OnPress", "btnScreamerNightsRandomizerLock")]
    public void ScreamerNightsLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.ScreamerNights, cbxScreamerNights, (int)workingScreamerNights, sender); }
    public void ScreamerNights_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxScreamerNights == null) return;
        workingScreamerNights = (RebirthScreamerNightMode)Mathf.Clamp(cbxScreamerNights.SelectedIndex, 0, 3);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxTornadoMultiplier", true)] public readonly XUiC_ComboBoxList<string> cbxTornadoMultiplier;
    [XuiBindComponent("btnTornadoMultiplierRandomizerLock", true)] public readonly XUiC_Button btnTornadoMultiplierRandomizerLock;
    private static readonly int[] TornadoMultiplierValues = new[] { 25, 50, 75, 100, 150, 200, 250, 300 };
    private int workingTornadoMultiplier = 100;
    [XuiXmlBinding("rebirth_tornado_multiplier_default")] public bool TornadoMultiplierDefault => workingTornadoMultiplier == 100;
    [XuiXmlBinding("rebirth_tornado_multiplier_changed")] public bool TornadoMultiplierChanged => GetBaselineState().TornadoMultiplier != workingTornadoMultiplier;
    [XuiXmlBinding("rebirth_tornado_multiplier_randomizer_locked")] public bool TornadoMultiplierLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.TornadoMultiplier);
    [XuiXmlBinding("rebirth_tornado_multiplier_randomizer_lock_sprite")] public string TornadoMultiplierLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.TornadoMultiplier);
    [XuiXmlBinding("rebirth_tornado_multiplier_randomizer_lock_is_minimum")] public bool TornadoMultiplierMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.TornadoMultiplier);
    [XuiXmlBinding("rebirth_tornado_multiplier_randomizer_lock_is_maximum")] public bool TornadoMultiplierMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.TornadoMultiplier);
    [XuiBindEvent("OnPress", "btnTornadoMultiplierRandomizerLock")]
    public void TornadoMultiplierLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.TornadoMultiplier, cbxTornadoMultiplier, System.Array.IndexOf(RebirthEventSpawnPolicy.TornadoSteps, workingTornadoMultiplier), sender); }
    public void TornadoMultiplier_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxTornadoMultiplier == null) return;
        workingTornadoMultiplier = RebirthEventSpawnPolicy.TornadoSteps[Mathf.Clamp(cbxTornadoMultiplier.SelectedIndex, 0, 7)];
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxScreamerMax", true)] public readonly XUiC_ComboBoxList<string> cbxScreamerMax;
    [XuiBindComponent("btnScreamerMaxRandomizerLock", true)] public readonly XUiC_Button btnScreamerMaxRandomizerLock;
    private static readonly int[] ScreamerMaxValues = new[] { 3, 4, 5, 6, 7, 8, 9, 10 };
    private int workingScreamerMax = 6;
    [XuiXmlBinding("rebirth_screamer_max_default")] public bool ScreamerMaxDefault => workingScreamerMax == 6;
    [XuiXmlBinding("rebirth_screamer_max_changed")] public bool ScreamerMaxChanged => GetBaselineState().ScreamerMax != workingScreamerMax;
    [XuiXmlBinding("rebirth_screamer_max_randomizer_locked")] public bool ScreamerMaxLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.ScreamerMax);
    [XuiXmlBinding("rebirth_screamer_max_randomizer_lock_sprite")] public string ScreamerMaxLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.ScreamerMax);
    [XuiXmlBinding("rebirth_screamer_max_randomizer_lock_is_minimum")] public bool ScreamerMaxMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.ScreamerMax);
    [XuiXmlBinding("rebirth_screamer_max_randomizer_lock_is_maximum")] public bool ScreamerMaxMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.ScreamerMax);
    [XuiBindEvent("OnPress", "btnScreamerMaxRandomizerLock")]
    public void ScreamerMaxLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.ScreamerMax, cbxScreamerMax, workingScreamerMax - 3, sender); }
    public void ScreamerMax_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxScreamerMax == null) return;
        workingScreamerMax = 3 + Mathf.Clamp(cbxScreamerMax.SelectedIndex, 0, 7);
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxGamestageMultiplier", true)] public readonly XUiC_ComboBoxList<string> cbxGamestageMultiplier;
    [XuiBindComponent("btnGamestageMultiplierRandomizerLock", true)] public readonly XUiC_Button btnGamestageMultiplierRandomizerLock;
    private static readonly int[] GamestageMultiplierValues = new[] { 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200 };
    private int workingGamestageMultiplier = 100;
    [XuiXmlBinding("rebirth_gamestage_multiplier_default")] public bool GamestageMultiplierDefault => workingGamestageMultiplier == 100;
    [XuiXmlBinding("rebirth_gamestage_multiplier_changed")] public bool GamestageMultiplierChanged => GetBaselineState().GamestageMultiplier != workingGamestageMultiplier;
    [XuiXmlBinding("rebirth_gamestage_multiplier_randomizer_locked")] public bool GamestageMultiplierLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.GamestageMultiplier);
    [XuiXmlBinding("rebirth_gamestage_multiplier_randomizer_lock_sprite")] public string GamestageMultiplierLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.GamestageMultiplier);
    [XuiXmlBinding("rebirth_gamestage_multiplier_randomizer_lock_is_minimum")] public bool GamestageMultiplierMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.GamestageMultiplier);
    [XuiXmlBinding("rebirth_gamestage_multiplier_randomizer_lock_is_maximum")] public bool GamestageMultiplierMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.GamestageMultiplier);
    [XuiBindEvent("OnPress", "btnGamestageMultiplierRandomizerLock")]
    public void GamestageMultiplierLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.GamestageMultiplier, cbxGamestageMultiplier, (workingGamestageMultiplier - 50) / 10, sender); }
    public void GamestageMultiplier_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxGamestageMultiplier == null) return;
        workingGamestageMultiplier = 50 + Mathf.Clamp(cbxGamestageMultiplier.SelectedIndex, 0, 15) * 10;
        MarkWorkingStateChanged();
    }
    [XuiBindComponent("cbxBossEventZombieAnimals", true)] public readonly XUiC_ComboBoxList<string> cbxBossEventZombieAnimals;
    [XuiBindComponent("btnBossEventZombieAnimalsRandomizerLock", true)] public readonly XUiC_Button btnBossEventZombieAnimalsRandomizerLock;
    private bool workingBossEventZombieAnimals = true;
    [XuiXmlBinding("rebirth_boss_event_zombie_animals_default")] public bool BossEventZombieAnimalsDefault => workingBossEventZombieAnimals == true;
    [XuiXmlBinding("rebirth_boss_event_zombie_animals_changed")] public bool BossEventZombieAnimalsChanged => GetBaselineState().BossEventZombieAnimals != workingBossEventZombieAnimals;
    [XuiXmlBinding("rebirth_boss_event_zombie_animals_randomizer_locked")] public bool BossEventZombieAnimalsLocked => IsRebirthRandomizerLocked(RebirthSandboxOptionId.BossEventZombieAnimals);
    [XuiXmlBinding("rebirth_boss_event_zombie_animals_randomizer_lock_sprite")] public string BossEventZombieAnimalsLockSprite => GetRebirthRandomizerLockSprite(RebirthSandboxOptionId.BossEventZombieAnimals);
    [XuiXmlBinding("rebirth_boss_event_zombie_animals_randomizer_lock_is_minimum")] public bool BossEventZombieAnimalsMinimum => IsRebirthRandomizerMinimum(RebirthSandboxOptionId.BossEventZombieAnimals);
    [XuiXmlBinding("rebirth_boss_event_zombie_animals_randomizer_lock_is_maximum")] public bool BossEventZombieAnimalsMaximum => IsRebirthRandomizerMaximum(RebirthSandboxOptionId.BossEventZombieAnimals);
    [XuiBindEvent("OnPress", "btnBossEventZombieAnimalsRandomizerLock")]
    public void BossEventZombieAnimalsLock_OnPressed(XUiController sender, int mouseButton)
    { CycleRebirthRandomizerLock(RebirthSandboxOptionId.BossEventZombieAnimals, cbxBossEventZombieAnimals, workingBossEventZombieAnimals ? 1 : 0, sender); }
    public void BossEventZombieAnimals_OnValueChanged(XUiController sender, string oldValue, string newValue)
    {
        if (suppressEvents || cbxBossEventZombieAnimals == null) return;
        workingBossEventZombieAnimals = cbxBossEventZombieAnimals.SelectedIndex == 1;
        MarkWorkingStateChanged();
    }
    private void PopulateEventSpawnOptions()
    {
        PopulateEnum(cbxKamikazeMode, workingKamikazeMode, "xuiRebirthKamikazeModeNever", "xuiRebirthKamikazeModeNotInPois", "xuiRebirthKamikazeModeNotHordeNight", "xuiRebirthKamikazeModeNotHordeNightOrPois", "xuiRebirthKamikazeModeAlways");
        PopulateEnum(cbxTornadoMode, workingTornadoMode, "xuiRebirthTornadoModeBiomesOnly", "xuiRebirthTornadoModeHordeNightOnly", "xuiRebirthTornadoModeAlways", "xuiRebirthTornadoModeNever");
        PopulateEnum(cbxScreamerNights, workingScreamerNights, "xuiRebirthScreamerNightsNone", "xuiRebirthScreamerNightsLower", "xuiRebirthScreamerNightsDefault", "xuiRebirthScreamerNightsHigher");
        if (cbxTornadoMultiplier != null) { cbxTornadoMultiplier.Elements.Clear(); foreach (int value in TornadoMultiplierValues) cbxTornadoMultiplier.Elements.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"); cbxTornadoMultiplier.SelectedIndex = System.Array.IndexOf(RebirthEventSpawnPolicy.TornadoSteps, workingTornadoMultiplier); }
        if (cbxScreamerMax != null) { cbxScreamerMax.Elements.Clear(); foreach (int value in ScreamerMaxValues) cbxScreamerMax.Elements.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ""); cbxScreamerMax.SelectedIndex = workingScreamerMax - 3; }
        if (cbxGamestageMultiplier != null) { cbxGamestageMultiplier.Elements.Clear(); foreach (int value in GamestageMultiplierValues) cbxGamestageMultiplier.Elements.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"); cbxGamestageMultiplier.SelectedIndex = (workingGamestageMultiplier - 50) / 10; }
        PopulateBoolean(cbxBossEventZombieAnimals, workingBossEventZombieAnimals);
    }
}
