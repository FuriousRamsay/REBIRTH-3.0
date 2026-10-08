using System;
using UnityEngine.Scripting;

// Personal presentation preferences intentionally never enter the synchronized sandbox state.
public static class RebirthTraderVoicePreferences
{
    public static readonly string[] Traders = { "Rekt", "Hugh", "Jen", "Joel", "Bob" };
    public static readonly string[] Presets = { "ORIGINAL", "REKT", "HUGH", "JEN", "JOEL", "BOB", "POPPY" };
    public static readonly string[] Labels = { "Original", "Hostile", "Gruff", "Warm", "Smooth", "Practical", "Sardonic" };
    public static bool ValidTrader(string trader) => Array.IndexOf(Traders, trader) >= 0;
    public static int Get(string trader)
    {
        if (!ValidTrader(trader)) return 0;
        int value = SdPlayerPrefs.GetInt("Rebirth.TraderVoices." + trader, 0);
        return value >= 0 && value < Presets.Length ? value : 0;
    }
    public static void Set(string trader, int value)
    {
        if (!ValidTrader(trader)) return;
        SdPlayerPrefs.SetInt("Rebirth.TraderVoices." + trader, value >= 0 && value < Presets.Length ? value : 0);
        SdPlayerPrefs.Save();
    }
}

[Preserve]
public sealed class XUiC_RebirthTraderVoiceOption : XUiController
{
    private XUiC_ComboBoxList<string> selector;
    private string trader;
    private bool refreshing;
    private int baseline;
    private XUiC_RebirthSandboxOptions owner;
    public int PendingValue => selector?.SelectedIndex ?? baseline;
    public bool HasChanges => PendingValue != baseline;
    public RebirthSandboxOptionId LockId => (RebirthSandboxOptionId)(64 + Array.IndexOf(RebirthTraderVoicePreferences.Traders, trader));
    public override void Init()
    {
        base.Init();
        trader = ViewComponent.ID.Replace("traderVoice", "");
        owner = GetParentByType<XUiC_RebirthSandboxOptions>();
        owner?.RegisterVoiceOption(this);
        GetChildById("voiceLock").OnPress += (sender, button) => {
            if (button != 0 && button != -1) return;
            RebirthSandboxRandomizerLockState.CycleMode(LockId, PendingValue);
            RefreshBindings(); owner?.VoiceOptionChanged();
        };
        selector = GetChildById("voiceSelector") as XUiC_ComboBoxList<string>;
        if (selector != null) selector.OnValueChanged += Changed;
    }
    public override void OnOpen()
    {
        base.OnOpen();
        if (selector == null) return;
        refreshing = true;
        try
        {
            selector.Elements.Clear();
            foreach (var label in RebirthTraderVoicePreferences.Labels)
                selector.Elements.Add(Localization.Get("rebirthTraderVoice" + label));
            baseline = RebirthTraderVoicePreferences.Get(trader);
            selector.SelectedIndex = baseline;
        }
        finally { refreshing = false; }
    }
    private void Changed(XUiController sender, string oldValue, string newValue)
    {
        if (!refreshing) { RefreshBindings(); owner?.VoiceOptionChanged(); }
    }
    public void SetPending(int value) { if (selector != null) selector.SelectedIndex = value; RefreshBindings(); owner?.VoiceOptionChanged(); }
    public void Commit() { RebirthTraderVoicePreferences.Set(trader, PendingValue); baseline = PendingValue; RefreshBindings(); }
    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        var mode = RebirthSandboxRandomizerLockState.GetMode(LockId);
        switch (bindingName) {
            case "voice_lock_sprite": value = mode == RebirthSandboxOptionRandomizerMode.Unlocked ? "ui_game_symbol_unlock" : "ui_game_symbol_lock"; return true;
            case "voice_locked": value = (mode != RebirthSandboxOptionRandomizerMode.Unlocked).ToString(); return true;
            case "voice_minimum": value = (mode == RebirthSandboxOptionRandomizerMode.Minimum).ToString(); return true;
            case "voice_maximum": value = (mode == RebirthSandboxOptionRandomizerMode.Maximum).ToString(); return true;
            case "voice_changed": value = HasChanges.ToString(); return true;
            case "voice_default": value = (PendingValue == 0).ToString(); return true;
        }
        return base.GetBindingValueInternal(ref value,bindingName);
    }
}
