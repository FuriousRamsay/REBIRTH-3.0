using System.Collections.Generic;

public partial class XUiC_RebirthSandboxOptions
{
    private readonly List<XUiC_RebirthTraderVoiceOption> voiceOptions = new List<XUiC_RebirthTraderVoiceOption>();
    public void RegisterVoiceOption(XUiC_RebirthTraderVoiceOption option) { if (!voiceOptions.Contains(option)) voiceOptions.Add(option); }
    [XuiXmlBinding("rebirth_voice_has_changes")]
    public bool VoiceHasChanges { get { foreach (var option in voiceOptions) if (option.HasChanges) return true; return false; } }
    public void VoiceOptionChanged() { IsDirty = true; RefreshBindings(); }
    private void SaveVoiceOptions() { foreach (var option in voiceOptions) option.Commit(); VoiceOptionChanged(); }
    private void DefaultVoiceOptions() { foreach (var option in voiceOptions) option.SetPending(0); }
    private void RandomizeVoiceOptions() {
        foreach (var option in voiceOptions)
            option.SetPending(GetRandomizedOptionIndex(option.LockId,option.PendingValue,0,RebirthTraderVoicePreferences.Presets.Length-1));
    }
}
