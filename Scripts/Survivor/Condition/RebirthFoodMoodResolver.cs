using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

/// <summary>
/// Reads generic item authoring only. Chunk 12 assigns these properties to the food catalog.
/// No item name is hard-coded here.
/// </summary>
public static class RebirthFoodMoodResolver
{
    public const string MoodProfileProperty = "RebirthMoodFoodProfile";
    public const string MoodOverrideProperty = "RebirthMoodInfluence";
    public const string DietTagsProperty = "RebirthDietTags";
    public const string VarietyFamilyProperty = "RebirthMoodVarietyFamily";

    public static bool TryResolve(ItemValue itemValue, out RebirthFoodMoodDefinition result)
    {
        result = null;
        if (itemValue == null || itemValue.IsEmpty() || itemValue.ItemClass == null) return false;
        bool resolved = TryResolve(itemValue.ItemClass, out result);
        if (resolved) RebirthCookingBatch.ApplyMood(itemValue, result);
        return resolved;
    }

    public static bool TryResolve(ItemClass itemClass, out RebirthFoodMoodDefinition result)
    {
        result = null;
        if (itemClass == null || itemClass.Properties == null) return false;
        string profile = RebirthConsumableResolver.Get(itemClass, MoodProfileProperty, string.Empty).Trim();
        if (profile.Length == 0) return false;
        float influence;
        if (!RebirthConditionRuntimeConfig.TryGetFoodMoodInfluence(profile, out influence)) return false;

        string rawOverride = RebirthConsumableResolver.Get(itemClass, MoodOverrideProperty, null);
        float overrideValue;
        if (rawOverride != null && float.TryParse(rawOverride, NumberStyles.Float, CultureInfo.InvariantCulture, out overrideValue))
            influence = overrideValue;

        RebirthFoodMoodDefinition d = new RebirthFoodMoodDefinition();
        d.SourceItemId = itemClass.GetItemName() ?? string.Empty;
        d.MoodProfileId = profile;
        d.BaseMoodInfluence = influence;
        d.VarietyFamilyId = RebirthConsumableResolver.Get(itemClass, VarietyFamilyProperty, d.SourceItemId).Trim();
        string rawTags = RebirthConsumableResolver.Get(itemClass, DietTagsProperty, string.Empty);
        string[] parts = rawTags.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            string tag = (parts[i] ?? string.Empty).Trim();
            if (tag.Length > 0) d.DietTags.Add(tag);
        }
        result = d;
        return true;
    }
}
