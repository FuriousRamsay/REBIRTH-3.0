using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Canonical resolver for active Survivor condition contributors. Mood, Traits, deprivation,
/// environment and the existing metabolism state are resolved once into multipliers/cause data;
/// downstream systems do not each reinterpret the same conditions independently.
/// </summary>
public static class RebirthConditionResolver
{
    public static RebirthConditionResolvedSnapshot Resolve(RebirthWorldCharacterRecord record)
    {
        return Resolve(null, record, null);
    }

    public static RebirthConditionResolvedSnapshot Resolve(EntityPlayer player, RebirthWorldCharacterRecord record, RebirthMetabolismState metabolism)
    {
        RebirthConditionResolvedSnapshot result = new RebirthConditionResolvedSnapshot();
        float neutral = RebirthConditionRuntimeConfig.MoodNeutral;
        float totalMoodDelta = 0f;

        float satisfaction = record != null && record.Condition != null
            ? Mathf.Clamp(record.Condition.DietSatisfaction, 0f, 100f)
            : neutral;
        float normalized = (satisfaction - 50f) / 50f;
        float dietDelta = normalized * RebirthConditionRuntimeConfig.DietSatisfactionMaxTargetDelta;
        result.DietMoodTargetDelta = dietDelta;
        AddMoodContributor(result, "diet_satisfaction", "diet", "diet", dietDelta, ref totalMoodDelta);

        // Apply authored Trait runtime components before condition causes so category multipliers
        // such as Pain Tolerant and Heat Tolerant can scale the matching cause exactly once.
        RebirthConditionTraitModifierService.ApplyRuntimeComponents(player, metabolism, record, result);
        float directTraitMoodDelta = RebirthConditionTraitModifierService.GetDirectMoodTargetDelta(player, record);
        AddMoodContributor(result, "trait_mood", "trait", "runtime_components", directTraitMoodDelta, ref totalMoodDelta);
        float supportMoodDelta = RebirthTraitSupportService.ApplyConditionEffects(player, metabolism, record, result);
        AddMoodContributor(result, "trait_support", "support", "support_profiles", supportMoodDelta, ref totalMoodDelta);

        if (player != null && player.Stats != null)
        {
            float hydrationPercent = Percent(player.Stats.Water != null ? player.Stats.Water.Value : 100f,
                player.Stats.Water != null ? player.Stats.Water.ModifiedMax : 100f);
            float nutritionPercent = Percent(player.Stats.Food != null ? player.Stats.Food.Value : 100f,
                player.Stats.Food != null ? player.Stats.Food.ModifiedMax : 100f);

            if (hydrationPercent < 0.25f)
            {
                float severity = 1f - Mathf.Clamp01(hydrationPercent / 0.25f);
                AddMoodContributor(result, "severe_dehydration", "deprivation", "hydration",
                    Mathf.Lerp(-10f, -20f, severity), ref totalMoodDelta);
            }
            if (nutritionPercent < 0.25f)
            {
                float severity = 1f - Mathf.Clamp01(nutritionPercent / 0.25f);
                AddMoodContributor(result, "severe_malnutrition", "deprivation", "nutrition",
                    Mathf.Lerp(-8f, -18f, severity), ref totalMoodDelta);
            }

            if (metabolism != null && RebirthMetabolismConfig.EnergyMax > 0.001f)
            {
                float energyPercent = Mathf.Clamp01(metabolism.Energy / RebirthMetabolismConfig.EnergyMax);
                if (energyPercent < 0.25f)
                {
                    float severity = 1f - Mathf.Clamp01(energyPercent / 0.25f);
                    AddMoodContributor(result, "exhaustion", "energy", "energy",
                        Mathf.Lerp(-5f, -10f, severity), ref totalMoodDelta);
                }
            }

            float nativeHealthBlockage = Mathf.Max(0f, EffectManager.GetValue(PassiveEffects.HealthMaxBlockage, _entity: player));
            if (nativeHealthBlockage > 0.01f)
            {
                float baseMax = player.Stats.Health != null ? Mathf.Max(1f, player.Stats.Health.Max) : 100f;
                float severity = Mathf.Clamp01(nativeHealthBlockage / Mathf.Max(20f, baseMax * 0.25f));
                AddMoodContributor(result, "injury_pain", "injury", "native_health_blockage",
                    Mathf.Lerp(-5f, -15f, severity) * result.InjuryMoodMultiplier, ref totalMoodDelta);
            }

            if (RebirthConditionTraitModifierService.IsSeriousIllness(player))
                AddMoodContributor(result, "serious_illness", "illness", "buff",
                    -12f, ref totalMoodDelta);

            float core = player.PlayerStats.CoreTemp;
            if (core >= 75f)
            {
                float severity = Mathf.Clamp01((core - 75f) / 25f);
                AddMoodContributor(result, "heat_exposure", "environment", "temperature",
                    Mathf.Lerp(-3f, -10f, severity) * result.HeatPenaltyMultiplier, ref totalMoodDelta);
            }
            else if (core <= 62f)
            {
                float severity = Mathf.Clamp01((62f - core) / 27f);
                AddMoodContributor(result, "cold_exposure", "environment", "temperature",
                    Mathf.Lerp(-3f, -10f, severity) * result.ColdPenaltyMultiplier, ref totalMoodDelta);
            }
        }

        result.MoodTarget = Mathf.Clamp(neutral + totalMoodDelta,
            RebirthConditionRuntimeConfig.MoodMin, RebirthConditionRuntimeConfig.MoodMax);

        float currentMood = record != null && record.Condition != null
            ? Mathf.Clamp(record.Condition.MoodCurrent, RebirthConditionRuntimeConfig.MoodMin, RebirthConditionRuntimeConfig.MoodMax)
            : neutral;
        result.EnergyUseMultiplier *= RebirthConditionRuntimeConfig.GetMoodEnergyUseMultiplier(currentMood);
        result.EnergyRecoveryMultiplier *= RebirthConditionRuntimeConfig.GetMoodEnergyRecoveryMultiplier(currentMood);
        result.EnergyUseMultiplier = Mathf.Clamp(result.EnergyUseMultiplier, 0.65f, 1.50f);
        result.EnergyRecoveryMultiplier = Mathf.Clamp(result.EnergyRecoveryMultiplier, 0.60f, 1.50f);
        return result;
    }

    private static float Percent(float value, float max)
    {
        return max > 0.001f ? Mathf.Clamp01(value / max) : 0f;
    }

    private static void AddMoodContributor(RebirthConditionResolvedSnapshot result, string id, string category,
        string source, float delta, ref float total)
    {
        if (result == null || Math.Abs(delta) <= 0.001f)
            return;
        total += delta;
        result.Contributors.Add(new RebirthConditionContributorSnapshot
        {
            Id = id ?? string.Empty,
            Category = category ?? string.Empty,
            Source = source ?? string.Empty,
            MoodTargetDelta = delta
        });
        if (delta > result.DominantPositiveCauseDelta)
        {
            result.DominantPositiveCauseId = id ?? string.Empty;
            result.DominantPositiveCauseDelta = delta;
        }
        if (delta < result.DominantNegativeCauseDelta)
        {
            result.DominantNegativeCauseId = id ?? string.Empty;
            result.DominantNegativeCauseDelta = delta;
        }
    }
}
