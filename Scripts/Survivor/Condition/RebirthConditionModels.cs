using System;
using System.Collections.Generic;

#nullable disable

public sealed class RebirthFoodMoodDefinition
{
    public string SourceItemId = string.Empty;
    public string MoodProfileId = string.Empty;
    public string VarietyFamilyId = string.Empty;
    public float BaseMoodInfluence;
    public readonly HashSet<string> DietTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class RebirthMealEvaluationResult
{
    public bool Valid;
    public string SourceItemId = string.Empty;
    public string DietId = string.Empty;
    public string DietRule = string.Empty;
    public string MoodProfileId = string.Empty;
    public string VarietyFamilyId = string.Empty;
    public float BaseMoodInfluence;
    public float TraitAdjustedMoodInfluence;
    public float RepetitionMultiplier = 1f;
    public float DietModifier;
    public float EffectiveMoodInfluence;
    public bool CompatibleWithDiet = true;
    public RebirthDietCompatibilityState CompatibilityState = RebirthDietCompatibilityState.Compatible;
    public string CompatibilityReason = string.Empty;
    public int PreviousMatchingMeals;
    public int RecentMealCount;
    public int RecentVarietyCount;
    public float DietSatisfactionBefore;
    public float DietSatisfactionAfter;
    public float MoodTargetAfter;
    public string Reason = string.Empty;
}

public sealed class RebirthConditionContributorSnapshot
{
    public string Id = string.Empty;
    public string Category = string.Empty;
    public string Source = string.Empty;
    public float MoodTargetDelta;
}

/// <summary>
/// Immutable-for-one-evaluation resolved condition view. Runtime consumers read this snapshot
/// rather than independently interpreting Traits, Mood, deprivation and environment.
/// </summary>
public sealed class RebirthConditionResolvedSnapshot
{
    public float MoodTarget;
    public float DietMoodTargetDelta;
    public string DominantPositiveCauseId = string.Empty;
    public float DominantPositiveCauseDelta;
    public string DominantNegativeCauseId = string.Empty;
    public float DominantNegativeCauseDelta;

    public float EnergyUseMultiplier = 1f;
    public float EnergyRecoveryMultiplier = 1f;
    public float HydrationDemandMultiplier = 1f;
    public float NutritionUseMultiplier = 1f;
    public float HeatPenaltyMultiplier = 1f;
    public float ColdPenaltyMultiplier = 1f;
    public float DigestionSpeedMultiplier = 1f;
    public float FluidAbsorptionMultiplier = 1f;
    public float FluidUtilizationMultiplier = 1f;
    public float NutrientUtilizationMultiplier = 1f;
    public float GutResilienceMultiplier = 1f;
    public float DigestiveRecoveryMultiplier = 1f;
    public float DigestiveBaselineOffset;
    public float HealthCapacityLossMultiplier = 1f;
    public float HealthCapacityInjuryLossMultiplier = 1f;
    public float HealthCapacityIllnessLossMultiplier = 1f;
    public float HealthCapacityRecoveryMultiplier = 1f;
    public float MoodRiseRateMultiplier = 1f;
    public float MoodFallRateMultiplier = 1f;
    public float InjuryMoodMultiplier = 1f;
    public float StaminaRecoveryMultiplier = 1f;

    public readonly List<RebirthConditionContributorSnapshot> Contributors = new List<RebirthConditionContributorSnapshot>();
}

public sealed class RebirthConditionStatusSnapshot
{
    public float MoodCurrent;
    public float MoodTarget;
    public float DietSatisfaction;
    public float HealthCapacity;
    public float HealthPotential;
    public int RecentMealCount;
    public int RecentVarietyCount;
    public bool HasRecentMeal;
    public bool LastMealCompatible;
    public string DominantPositiveCauseId = string.Empty;
    public float DominantPositiveCauseDelta;
    public string DominantNegativeCauseId = string.Empty;
    public float DominantNegativeCauseDelta;
    public float EnergyUseMultiplier = 1f;
    public float EnergyRecoveryMultiplier = 1f;
    public float HydrationDemandMultiplier = 1f;
    public float NutritionUseMultiplier = 1f;
    public float HealthCapacityLossMultiplier = 1f;
    public float HealthCapacityRecoveryMultiplier = 1f;
    public float SevereDehydrationActiveSeconds;
    public float SevereMalnutritionActiveSeconds;
}
