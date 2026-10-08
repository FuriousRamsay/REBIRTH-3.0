using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public static class RebirthDietSatisfactionService
{
    public static bool TryRecordMeaningfulMeal(EntityPlayer player, ItemValue itemValue, out RebirthMealEvaluationResult evaluation)
    {
        evaluation = null;
        if (player == null || itemValue == null || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        if (RebirthCharacterCreationHoldService.IsHeld(player)) return false;
        RebirthFoodMoodDefinition food;
        if (!RebirthFoodMoodResolver.TryResolve(itemValue, out food) || food == null) return false;
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || record.Origin == null) return false;
        evaluation = RecordMeaningfulMeal(record, food);
        if (evaluation == null || !evaluation.Valid) return false;
        RebirthWorldCharacterService.MarkDirty(record, "condition-meaningful-meal");
        RebirthSurvivorNetworkService.SendOwnerState(player, Math.Max(0L, record.Revision - 1L), false, "condition-meaningful-meal");
        return true;
    }

    public static RebirthMealEvaluationResult RecordMeaningfulMeal(RebirthWorldCharacterRecord record, RebirthFoodMoodDefinition food)
    {
        RebirthMealEvaluationResult r = new RebirthMealEvaluationResult();
        if (record == null || record.Condition == null || record.Origin == null || food == null)
        { r.Reason = "missing-character-or-food"; return r; }

        RebirthDietDefinition diet;
        if (!RebirthSurvivorDefinitionRegistry.TryGetDiet(record.Origin.DietId, out diet) || diet == null)
        { r.Reason = "unknown-diet"; return r; }

        r.SourceItemId = food.SourceItemId ?? string.Empty;
        r.DietId = diet.Id;
        r.DietRule = diet.CompositionRule ?? string.Empty;
        r.MoodProfileId = food.MoodProfileId ?? string.Empty;
        r.VarietyFamilyId = string.IsNullOrEmpty(food.VarietyFamilyId) ? r.SourceItemId : food.VarietyFamilyId;
        r.BaseMoodInfluence = food.BaseMoodInfluence;
        r.TraitAdjustedMoodInfluence = RebirthConditionTraitModifierService.AdjustFoodMood(record, food, r.BaseMoodInfluence);
        r.DietSatisfactionBefore = record.Condition.DietSatisfaction;

        RebirthDietCompatibilityResult compatibility = RebirthDietCompatibility.Evaluate(diet.CompositionRule, food.DietTags);
        r.CompatibleWithDiet = compatibility.Compatible;
        r.CompatibilityState = compatibility.State;
        r.CompatibilityReason = compatibility.Reason;
        r.DietModifier = compatibility.DietModifier;
        r.PreviousMatchingMeals = CountPreviousMatchingCompatibleMeals(record.Condition, r.VarietyFamilyId);
        r.RepetitionMultiplier = RebirthConditionRuntimeConfig.GetRepetitionMultiplier(r.PreviousMatchingMeals);
        r.RepetitionMultiplier = RebirthConditionTraitModifierService.AdjustFoodRepetitionMultiplier(record, food, r.RepetitionMultiplier);
        RebirthTraitSupportService.ApplyNextMealSupport(record, food, ref r.TraitAdjustedMoodInfluence, ref r.RepetitionMultiplier);

        // Positive enjoyment is suppressed for dietary violations. Negative food quality remains
        // negative and can stack with a violation, preserving the objective food-quality axis.
        float quality = r.TraitAdjustedMoodInfluence;
        if (quality > 0f)
            quality = r.CompatibleWithDiet ? quality * r.RepetitionMultiplier : 0f;
        r.EffectiveMoodInfluence = Mathf.Clamp(quality + r.DietModifier, -20f, 20f);

        AddMeal(record.Condition, new RebirthRecentMealState
        {
            SourceItemId = r.SourceItemId,
            VarietyFamilyId = r.VarietyFamilyId,
            MoodQuality = r.BaseMoodInfluence,
            CompatibleWithDiet = r.CompatibleWithDiet,
            AgeActiveSeconds = 0f
        });

        float sample = Mathf.Clamp(50f + r.EffectiveMoodInfluence * RebirthConditionRuntimeConfig.MealSampleScale, 0f, 100f);
        float alpha = RebirthConditionRuntimeConfig.DietSatisfactionUpdateAlpha;
        record.Condition.DietSatisfaction = Mathf.Clamp(Mathf.Lerp(record.Condition.DietSatisfaction, sample, alpha), 0f, 100f);
        RebirthConditionResolvedSnapshot resolved = RebirthConditionResolver.Resolve(record);
        record.Condition.MoodTarget = resolved.MoodTarget;

        r.DietSatisfactionAfter = record.Condition.DietSatisfaction;
        r.MoodTargetAfter = record.Condition.MoodTarget;
        r.RecentMealCount = Math.Min(record.Condition.RecentMeals.Count, RebirthConditionRuntimeConfig.MealHistorySize);
        r.RecentVarietyCount = CountRecentCompatibleVarieties(record.Condition);
        r.Valid = true;
        r.Reason = "ok";
        return r;
    }

    public static int CountRecentCompatibleVarieties(RebirthWorldConditionState condition)
    {
        if (condition == null) return 0;
        HashSet<string> families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int start = Math.Max(0, condition.RecentMeals.Count - RebirthConditionRuntimeConfig.MealHistorySize);
        for (int i = start; i < condition.RecentMeals.Count; i++)
        {
            RebirthRecentMealState meal = condition.RecentMeals[i];
            if (meal == null || !meal.CompatibleWithDiet) continue;
            string id = string.IsNullOrEmpty(meal.VarietyFamilyId) ? meal.SourceItemId : meal.VarietyFamilyId;
            if (!string.IsNullOrEmpty(id)) families.Add(id);
        }
        return families.Count;
    }

    public static bool IsCompatible(string compositionRule, IEnumerable<string> tags, out float dietModifier)
    {
        RebirthDietCompatibilityResult result = RebirthDietCompatibility.Evaluate(compositionRule, tags);
        dietModifier = result.DietModifier;
        return result.Compatible;
    }

    public static RebirthDietCompatibilityResult EvaluateCompatibility(string compositionRule, IEnumerable<string> tags)
    {
        return RebirthDietCompatibility.Evaluate(compositionRule, tags);
    }

    private static int CountPreviousMatchingCompatibleMeals(RebirthWorldConditionState condition, string family)
    {
        if (condition == null || string.IsNullOrEmpty(family)) return 0;
        int count = 0;
        int start = Math.Max(0, condition.RecentMeals.Count - RebirthConditionRuntimeConfig.MealHistorySize);
        for (int i = start; i < condition.RecentMeals.Count; i++)
        {
            RebirthRecentMealState meal = condition.RecentMeals[i];
            if (meal == null || !meal.CompatibleWithDiet) continue;
            string id = string.IsNullOrEmpty(meal.VarietyFamilyId) ? meal.SourceItemId : meal.VarietyFamilyId;
            if (string.Equals(id, family, StringComparison.OrdinalIgnoreCase)) count++;
        }
        return count;
    }

    private static void AddMeal(RebirthWorldConditionState condition, RebirthRecentMealState meal)
    {
        condition.RecentMeals.Add(meal);
        int max = RebirthConditionRuntimeConfig.MealHistorySize;
        while (condition.RecentMeals.Count > max) condition.RecentMeals.RemoveAt(0);
    }
}
