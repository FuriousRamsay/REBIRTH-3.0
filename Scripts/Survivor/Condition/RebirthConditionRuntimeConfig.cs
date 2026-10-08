using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Data-driven active-play tuning for Mood, Diet Satisfaction and Health Capacity.
/// It remains outside immutable origin authoring so balance can be adjusted without inventing
/// per-item/per-Trait C# branches.
/// </summary>
public static class RebirthConditionRuntimeConfig
{
    private static readonly Dictionary<string, float> FoodMoodProfiles =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public static float MoodMin = 0f;
    public static float MoodMax = 100f;
    public static float MoodNeutral = 50f;
    public static float TickSeconds = 1f;
    public static float OwnerSyncSeconds = 5f;
    public static float MoodRisePerRealMinute = 0.75f;
    public static float MoodFallPerRealMinute = 1f;
    public static float DietSatisfactionMaxTargetDelta = 8f;

    public static float[] MoodEnergyRecovery = new float[] { 0.65f, 0.85f, 1f, 1.05f, 1.10f, 1.10f };
    public static float[] MoodEnergyUse = new float[] { 1.10f, 1.05f, 1f, 0.98f, 0.95f, 0.95f };

    public static int MealHistorySize = 6;
    public static float MealSampleScale = 3f;
    public static float DietSatisfactionUpdateAlpha = 0.35f;
    public static float VegetarianViolation = -8f;
    public static float VeganViolation = -10f;
    public static float CarnivoreViolation = -2f;
    public static float[] RepetitionMultipliers = new float[] { 1f, 0.75f, 0.5f, 0.25f, 0f };

    public static float MinimumHealthCapacity = 1f;
    public static float HealthCapacityRecoveryPerRealMinute = 0.50f;
    public static float DehydrationThresholdPercent = 0.25f;
    public static float DehydrationCriticalPercent = 0.10f;
    public static float DehydrationGraceRealSeconds = 600f;
    public static float DehydrationLossPerRealMinute = 0.12f;
    public static float DehydrationCriticalLossPerRealMinute = 0.35f;
    public static float DehydrationZeroLossPerRealMinute = 0.75f;
    public static float MalnutritionThresholdPercent = 0.25f;
    public static float MalnutritionCriticalPercent = 0.10f;
    public static float MalnutritionGraceRealSeconds = 1800f;
    public static float MalnutritionLossPerRealMinute = 0.05f;
    public static float MalnutritionCriticalLossPerRealMinute = 0.15f;
    public static float MalnutritionZeroLossPerRealMinute = 0.35f;
    public static float DysenteryNativeCapacityPenalty = 0f;

    public static string Load()
    {
        FoodMoodProfiles.Clear();
        string root = RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
        string path = Path.Combine(root, "condition_runtime.xml");
        XDocument doc = XDocument.Load(path);
        XElement node = doc.Root;
        if (node == null || (string)node.Attribute("schema_version") != "1")
            throw new InvalidDataException("condition_runtime.xml schema_version must be 1");

        XElement mood = node.Element("mood");
        if (mood == null) throw new InvalidDataException("condition_runtime.xml is missing <mood>");
        MoodMin = F(mood, "min", MoodMin);
        MoodMax = F(mood, "max", MoodMax);
        MoodNeutral = F(mood, "neutral", MoodNeutral);
        TickSeconds = Math.Max(0.25f, F(mood, "tick_seconds", TickSeconds));
        OwnerSyncSeconds = Math.Max(TickSeconds, F(mood, "owner_sync_seconds", OwnerSyncSeconds));
        MoodRisePerRealMinute = Math.Max(0f, F(mood, "rise_per_real_minute", MoodRisePerRealMinute));
        MoodFallPerRealMinute = Math.Max(0f, F(mood, "fall_per_real_minute", MoodFallPerRealMinute));
        DietSatisfactionMaxTargetDelta = Math.Max(0f, F(mood, "diet_satisfaction_max_target_delta", DietSatisfactionMaxTargetDelta));
        if (MoodMax <= MoodMin || MoodNeutral < MoodMin || MoodNeutral > MoodMax)
            throw new InvalidDataException("condition_runtime.xml Mood bounds are invalid");

        XElement moodEnergy = node.Element("mood_energy");
        if (moodEnergy == null) throw new InvalidDataException("condition_runtime.xml is missing <mood_energy>");
        MoodEnergyRecovery = new float[] {
            Positive(moodEnergy,"recovery_at_0",0.65f), Positive(moodEnergy,"recovery_at_20",0.85f),
            Positive(moodEnergy,"recovery_at_40",1f), Positive(moodEnergy,"recovery_at_60",1.05f),
            Positive(moodEnergy,"recovery_at_80",1.10f), Positive(moodEnergy,"recovery_at_100",1.10f) };
        MoodEnergyUse = new float[] {
            Positive(moodEnergy,"use_at_0",1.10f), Positive(moodEnergy,"use_at_20",1.05f),
            Positive(moodEnergy,"use_at_40",1f), Positive(moodEnergy,"use_at_60",0.98f),
            Positive(moodEnergy,"use_at_80",0.95f), Positive(moodEnergy,"use_at_100",0.95f) };

        XElement diet = node.Element("diet");
        if (diet == null) throw new InvalidDataException("condition_runtime.xml is missing <diet>");
        MealHistorySize = Math.Max(1, Math.Min(16, I(diet, "history_size", MealHistorySize)));
        MealSampleScale = Math.Max(0f, F(diet, "meal_sample_scale", MealSampleScale));
        DietSatisfactionUpdateAlpha = Clamp01(F(diet, "satisfaction_update_alpha", DietSatisfactionUpdateAlpha));
        VegetarianViolation = Math.Min(0f, F(diet, "vegetarian_violation", VegetarianViolation));
        VeganViolation = Math.Min(0f, F(diet, "vegan_violation", VeganViolation));
        CarnivoreViolation = Math.Min(0f, F(diet, "carnivore_violation", CarnivoreViolation));
        XElement repetition = diet.Element("repetition");
        if (repetition == null) throw new InvalidDataException("condition_runtime.xml is missing <repetition>");
        RepetitionMultipliers = new float[]
        {
            Clamp01(F(repetition,"first",1f)), Clamp01(F(repetition,"second",0.75f)),
            Clamp01(F(repetition,"third",0.50f)), Clamp01(F(repetition,"fourth",0.25f)),
            Clamp01(F(repetition,"further",0f))
        };

        XElement profiles = node.Element("food_profiles");
        if (profiles == null) throw new InvalidDataException("condition_runtime.xml is missing <food_profiles>");
        foreach (XElement profile in profiles.Elements("profile"))
        {
            string id = ((string)profile.Attribute("id") ?? string.Empty).Trim();
            if (id.Length == 0 || FoodMoodProfiles.ContainsKey(id))
                throw new InvalidDataException("condition_runtime.xml contains an invalid/duplicate food profile: " + id);
            FoodMoodProfiles.Add(id, F(profile, "mood_influence", 0f));
        }
        string[] required = new string[] { "comfort", "good", "simple", "neutral", "unpleasant", "bad", "disgusting", "revolting" };
        for (int i = 0; i < required.Length; i++)
            if (!FoodMoodProfiles.ContainsKey(required[i])) throw new InvalidDataException("missing food Mood profile: " + required[i]);

        XElement health = node.Element("health_capacity");
        if (health == null) throw new InvalidDataException("condition_runtime.xml is missing <health_capacity>");
        MinimumHealthCapacity = Math.Max(0f, F(health, "minimum", MinimumHealthCapacity));
        HealthCapacityRecoveryPerRealMinute = Math.Max(0f, F(health, "recovery_per_real_minute", HealthCapacityRecoveryPerRealMinute));
        DehydrationThresholdPercent = Clamp01(F(health, "dehydration_threshold_percent", DehydrationThresholdPercent));
        DehydrationCriticalPercent = Clamp01(F(health, "dehydration_critical_percent", DehydrationCriticalPercent));
        DehydrationGraceRealSeconds = Math.Max(0f, F(health, "dehydration_grace_real_seconds", DehydrationGraceRealSeconds));
        DehydrationLossPerRealMinute = Math.Max(0f, F(health, "dehydration_loss_per_real_minute", DehydrationLossPerRealMinute));
        DehydrationCriticalLossPerRealMinute = Math.Max(DehydrationLossPerRealMinute, F(health, "dehydration_critical_loss_per_real_minute", DehydrationCriticalLossPerRealMinute));
        DehydrationZeroLossPerRealMinute = Math.Max(DehydrationCriticalLossPerRealMinute, F(health, "dehydration_zero_loss_per_real_minute", DehydrationZeroLossPerRealMinute));
        MalnutritionThresholdPercent = Clamp01(F(health, "malnutrition_threshold_percent", MalnutritionThresholdPercent));
        MalnutritionCriticalPercent = Clamp01(F(health, "malnutrition_critical_percent", MalnutritionCriticalPercent));
        MalnutritionGraceRealSeconds = Math.Max(0f, F(health, "malnutrition_grace_real_seconds", MalnutritionGraceRealSeconds));
        MalnutritionLossPerRealMinute = Math.Max(0f, F(health, "malnutrition_loss_per_real_minute", MalnutritionLossPerRealMinute));
        MalnutritionCriticalLossPerRealMinute = Math.Max(MalnutritionLossPerRealMinute, F(health, "malnutrition_critical_loss_per_real_minute", MalnutritionCriticalLossPerRealMinute));
        MalnutritionZeroLossPerRealMinute = Math.Max(MalnutritionCriticalLossPerRealMinute, F(health, "malnutrition_zero_loss_per_real_minute", MalnutritionZeroLossPerRealMinute));
        DysenteryNativeCapacityPenalty = Math.Max(0f, F(health, "dysentery_native_capacity_penalty", DysenteryNativeCapacityPenalty));
        if (DehydrationCriticalPercent > DehydrationThresholdPercent || MalnutritionCriticalPercent > MalnutritionThresholdPercent)
            throw new InvalidDataException("condition_runtime.xml Health Capacity critical thresholds must be <= ordinary severe thresholds");

        return "foodProfiles=" + FoodMoodProfiles.Count + " mealHistory=" + MealHistorySize + " tick=" + TickSeconds.ToString("0.##", CultureInfo.InvariantCulture) + "s healthCapacity=integrated";
    }

    public static bool TryGetFoodMoodInfluence(string profileId, out float value)
    { return FoodMoodProfiles.TryGetValue(profileId ?? string.Empty, out value); }

    public static float GetRepetitionMultiplier(int previousMatchingMeals)
    {
        if (previousMatchingMeals <= 0) return RepetitionMultipliers[0];
        int index = Math.Min(previousMatchingMeals, RepetitionMultipliers.Length - 1);
        return RepetitionMultipliers[index];
    }

    public static float GetMoodEnergyRecoveryMultiplier(float mood)
    { return InterpolateMoodAnchors(mood, MoodEnergyRecovery); }

    public static float GetMoodEnergyUseMultiplier(float mood)
    { return InterpolateMoodAnchors(mood, MoodEnergyUse); }

    private static float InterpolateMoodAnchors(float mood, float[] values)
    {
        if (values == null || values.Length < 6) return 1f;
        float clamped = Math.Max(0f, Math.Min(100f, mood));
        if (clamped >= 100f) return values[5];
        int lowerIndex = Math.Min(4, (int)(clamped / 20f));
        float lowerMood = lowerIndex * 20f;
        float t = (clamped - lowerMood) / 20f;
        return values[lowerIndex] + (values[lowerIndex + 1] - values[lowerIndex]) * t;
    }

    private static float F(XElement e, string name, float fallback)
    { XAttribute a=e.Attribute(name); float v; return a!=null && float.TryParse(a.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:fallback; }
    private static float Positive(XElement e, string name, float fallback) { return Math.Max(0.01f, F(e,name,fallback)); }
    private static int I(XElement e, string name, int fallback)
    { XAttribute a=e.Attribute(name); int v; return a!=null && int.TryParse(a.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:fallback; }
    private static float Clamp01(float value) { return value < 0f ? 0f : value > 1f ? 1f : value; }
}
