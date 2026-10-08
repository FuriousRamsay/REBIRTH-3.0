// REBIRTH 3.0 Randomizer feature
// Source: adapted from zzz_RebirthModsLight sandbox option randomizer.
// Provides the Randomize button on the sandbox options screen.

using SandboxOptions;
using System.Collections.Generic;
using UnityEngine.Scripting;

using SandboxOptionId = SandboxOptions.SandboxOptions;

[Preserve]
public class XUiC_SandboxOptionsRandomizer : XUiC_SandboxOptions
{
    private static readonly System.Random Random = new System.Random();

    [XuiBindComponent("btnRandomize", true)]
    public readonly XUiC_Button btnRandomize;

    [XuiBindEvent("OnPress", "btnRandomize")]
    public void BtnRandomize_OnPressed(XUiController _sender, int _mouseButton)
    {
        this.RandomizeUnlockedOptions();
    }

    private void RandomizeUnlockedOptions()
    {
        if (this.sandboxOptionDict == null || this.sandboxOptionDict.Count == 0)
            return;

        Dictionary<SandboxOptionId, int> selectedIndices = new Dictionary<SandboxOptionId, int>();

        foreach (KeyValuePair<SandboxOptionId, XUiC_SandBoxOptionEntry> pair in this.sandboxOptionDict)
        {
            XUiC_SandBoxOptionEntry entry = pair.Value;
            if (!TryGetValidSelectedIndex(entry, out int selectedIndex))
                continue;

            selectedIndices[pair.Key] = selectedIndex;
        }

        bool changedAny = false;

        foreach (KeyValuePair<SandboxOptionId, XUiC_SandBoxOptionEntry> pair in this.sandboxOptionDict)
        {
            SandboxOptionId option = pair.Key;
            XUiC_SandBoxOptionEntry entry = pair.Value;

            if (!CanRandomizeEntry(option, entry))
                continue;

            int minIndex = entry.controlCombo.MinIndex;
            int maxIndex = entry.controlCombo.MaxIndex;
            if (minIndex < 0)
                minIndex = 0;
            if (maxIndex >= entry.controlCombo.Elements.Count)
                maxIndex = entry.controlCombo.Elements.Count - 1;
            if (maxIndex < minIndex)
                continue;

            int currentIndex = entry.controlCombo.SelectedIndex;
            RebirthSandboxRandomizerMode randomizerMode = XUiC_SandBoxOptionEntryRandomizer.GetMode(option);
            bool includeCurrentValue = false;

            if (randomizerMode == RebirthSandboxRandomizerMode.Minimum)
            {
                int anchorIndex = XUiC_SandBoxOptionEntryRandomizer.GetAnchorIndex(option, entry, currentIndex);
                anchorIndex = ClampIndex(anchorIndex, minIndex, maxIndex);
                if (anchorIndex > minIndex)
                    minIndex = anchorIndex;
                includeCurrentValue = true;
            }
            else if (randomizerMode == RebirthSandboxRandomizerMode.Maximum)
            {
                int anchorIndex = XUiC_SandBoxOptionEntryRandomizer.GetAnchorIndex(option, entry, currentIndex);
                anchorIndex = ClampIndex(anchorIndex, minIndex, maxIndex);
                if (anchorIndex < maxIndex)
                    maxIndex = anchorIndex;
                includeCurrentValue = true;
            }

            if (maxIndex < minIndex)
                continue;

            int randomizedIndex = GetRandomIndex(minIndex, maxIndex, currentIndex, includeCurrentValue);

            selectedIndices[option] = randomizedIndex;
            if (randomizedIndex != currentIndex)
                changedAny = true;
        }

        if (!changedAny)
            return;

        string randomizedCode;
        if (!TryBuildSandboxCode(selectedIndices, out randomizedCode))
            return;
        if (!this.sandboxManager.LoadOptionsFromCode(randomizedCode, SandboxOptionManager.CustomPreset))
            return;

        this.ApplyCustomPresetToUi();
    }

    private static bool TryGetValidSelectedIndex(XUiC_SandBoxOptionEntry entry, out int selectedIndex)
    {
        selectedIndex = -1;

        if (entry == null || !entry.HasEntry || entry.Option == null || entry.controlCombo == null)
            return false;

        int count = entry.controlCombo.Elements.Count;
        if (count <= 0)
            return false;

        selectedIndex = entry.controlCombo.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= count)
        {
            selectedIndex = entry.Option.GetDefaultIndex();
            if (selectedIndex < 0 || selectedIndex >= count)
                return false;
        }

        SandboxOptionValueSet valueSet = entry.Option.GetValueSet();
        return valueSet != null && valueSet.IsValidIndex(selectedIndex);
    }

    private static bool CanRandomizeEntry(SandboxOptionId option, XUiC_SandBoxOptionEntry entry)
    {
        if (option == SandboxOptionId.Max || entry == null || !entry.HasEntry || entry.IsSeparator || entry.Option == null)
            return false;

        if (entry.controlCombo == null || entry.controlCombo.Elements.Count <= 1)
            return false;

        if (!entry.Enabled)
            return false;

        if (XUiC_SandBoxOptionEntryRandomizer.IsLocked(option))
            return false;

        return true;
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

        lock (Random)
        {
            if (allowCurrent || currentIndex < minIndex || currentIndex > maxIndex)
                return minIndex + Random.Next(range);

            int offset = Random.Next(range - 1) + 1;
            return minIndex + ((currentIndex - minIndex + offset) % range);
        }
    }

    private bool TryBuildSandboxCode(Dictionary<SandboxOptionId, int> selectedIndices, out string code)
    {
        code = string.Empty;
        SandboxOptionPreset preset = new SandboxOptionPreset();

        foreach (KeyValuePair<string, List<BaseSandboxOption>> category in this.sandboxManager.OptionsByCategory.dict)
        {
            List<BaseSandboxOption> options = category.Value;
            if (options == null)
                continue;

            for (int i = 0; i < options.Count; i++)
            {
                BaseSandboxOption option = options[i];
                if (option == null)
                    continue;

                if (option.Option == SandboxOptionId.Max)
                    continue;
                if (!selectedIndices.TryGetValue(option.Option, out int selectedIndex))
                {
                    Log.Warning("[Rebirth] Sandbox randomizer aborted because option " + option.Option + " is not represented by the current UI projection; existing value was not reset.");
                    return false;
                }

                SandboxOptionValueSet valueSet = option.GetValueSet();
                if (valueSet == null || !valueSet.IsValidIndex(selectedIndex))
                    continue;

                int defaultIndex = option.GetDefaultIndex();
                if (selectedIndex == defaultIndex)
                    continue;

                if (!preset.PresetValues.ContainsKey(option.Option))
                    preset.PresetValues.Add(option.Option, selectedIndex);
            }
        }

        code = preset.SandboxCode;
        return !string.IsNullOrEmpty(code);
    }

    private void ApplyCustomPresetToUi()
    {
        SandboxOptionPreset customPreset = SandboxOptionManager.CustomPreset;

        if (this.currentPreset.IsCustomPreset)
        {
            this.updateOptionsToPreset(customPreset);
            this.currentPreset = new SandboxPresetInfo(customPreset);
            this.currentPreset.SandboxCode = customPreset.SandboxCode;
            this.applyPresetSettings();
            this.setEntriesDirty();
            this.IsDirty = true;
            return;
        }

        this.presetSelector.SelectPresetByName("Custom", null, true);
        this.IsDirty = true;
    }
}
