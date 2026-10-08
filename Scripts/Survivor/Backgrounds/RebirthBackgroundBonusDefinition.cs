using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public sealed class RebirthBackgroundBonusTuningValue
{
    public string Key { get; private set; }
    public string Value { get; private set; }
    public bool Locked { get; private set; }

    public RebirthBackgroundBonusTuningValue(string key, string value, bool locked)
    {
        Key = (key ?? string.Empty).Trim();
        Value = (value ?? string.Empty).Trim();
        Locked = locked;
    }
}

/// <summary>
/// Immutable authoring record for one Background Signature Bonus. Handler/Profile are stable
/// data tokens consumed by the owning implementation chunks; they are deliberately not class names.
/// </summary>
public sealed class RebirthBackgroundBonusDefinition
{
    public string Id { get; private set; }
    public string BackgroundId { get; private set; }
    public string NameKey { get; private set; }
    public string DescriptionKey { get; private set; }
    public string IconKey { get; private set; }
    public string IconState { get; private set; }
    public string Category { get; private set; }
    public string Handler { get; private set; }
    public string Profile { get; private set; }
    public ReadOnlyCollection<RebirthBackgroundBonusTuningValue> Tuning { get; private set; }

    public RebirthBackgroundBonusDefinition(string id, string backgroundId, string nameKey, string descriptionKey,
        string iconKey, string iconState, string category, string handler, string profile,
        IEnumerable<RebirthBackgroundBonusTuningValue> tuning)
    {
        Id = Clean(id);
        BackgroundId = Clean(backgroundId);
        NameKey = Clean(nameKey);
        DescriptionKey = Clean(descriptionKey);
        IconKey = Clean(iconKey);
        IconState = Clean(iconState);
        Category = Clean(category);
        Handler = Clean(handler);
        Profile = Clean(profile);
        Tuning = new ReadOnlyCollection<RebirthBackgroundBonusTuningValue>(new List<RebirthBackgroundBonusTuningValue>(tuning ?? new RebirthBackgroundBonusTuningValue[0]));
    }

    public bool TryGetTuning(string key, out RebirthBackgroundBonusTuningValue value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(key)) return false;
        for (int i = 0; i < Tuning.Count; i++)
        {
            RebirthBackgroundBonusTuningValue candidate = Tuning[i];
            if (candidate != null && string.Equals(candidate.Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }
        return false;
    }

    private static string Clean(string value) { return (value ?? string.Empty).Trim(); }
}
