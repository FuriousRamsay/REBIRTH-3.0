using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Single REBIRTH owner of the Survivor Health Capacity restriction. Deprivation uses sustained
/// active-play exposure; native HealthMaxBlockage is folded into the same target instead of being
/// applied as a second competing max-health writer. Capacity recovery never heals current Health.
/// </summary>
public static class RebirthHealthCapacityService
{
    public static float GetPotential(RebirthWorldCharacterRecord record)
    {
        if (record == null) return 0f;
        if (record.Progression != null && record.Progression.HealthPotential > 0f) return record.Progression.HealthPotential;
        return record.Origin != null ? Math.Max(0f, record.Origin.HealthPotential) : 0f;
    }

    public static bool Normalize(RebirthWorldCharacterRecord record)
    {
        if (record == null || record.Condition == null) return false;
        float potential = GetPotential(record);
        if (potential <= 0f) return false;
        float min = Math.Min(potential, RebirthConditionRuntimeConfig.MinimumHealthCapacity);
        float normalized = Mathf.Clamp(record.Condition.HealthCapacity, min, potential);
        if (Math.Abs(normalized - record.Condition.HealthCapacity) <= 0.0001f) return false;
        record.Condition.HealthCapacity = normalized;
        return true;
    }

    public static bool Tick(EntityPlayer player, RebirthWorldCharacterRecord record, float activeSeconds)
    {
        if (player == null || player.Stats == null || record == null || record.Condition == null || activeSeconds <= 0f)
            return false;
        float potential = GetPotential(record);
        if (potential <= 0f) return false;

        // Native god mode is a full-restoration/debug state. Survivor condition capacity is
        // intentionally preserved in the record so disabling god mode restores the real injury,
        // but it must not continue to clamp the live Health stat while god mode is active.
        if (player.IsGodMode.Value)
        {
            ApplyGodModeHealth(player.Stats);
            return false;
        }

        RebirthWorldConditionState condition = record.Condition;
        float min = Math.Min(potential, RebirthConditionRuntimeConfig.MinimumHealthCapacity);
        float beforeCapacity = condition.HealthCapacity;
        float beforeDehydration = condition.SevereDehydrationActiveSeconds;
        float beforeMalnutrition = condition.SevereMalnutritionActiveSeconds;
        condition.HealthCapacity = Mathf.Clamp(condition.HealthCapacity, min, potential);

        float hydrationPercent = Percent(player.Stats.Water != null ? player.Stats.Water.Value : 100f,
            player.Stats.Water != null ? player.Stats.Water.ModifiedMax : 100f);
        float nutritionPercent = Percent(player.Stats.Food != null ? player.Stats.Food.Value : 100f,
            player.Stats.Food != null ? player.Stats.Food.ModifiedMax : 100f);

        condition.SevereDehydrationActiveSeconds = hydrationPercent < RebirthConditionRuntimeConfig.DehydrationThresholdPercent
            ? Math.Max(0f, condition.SevereDehydrationActiveSeconds + activeSeconds) : 0f;
        condition.SevereMalnutritionActiveSeconds = nutritionPercent < RebirthConditionRuntimeConfig.MalnutritionThresholdPercent
            ? Math.Max(0f, condition.SevereMalnutritionActiveSeconds + activeSeconds) : 0f;

        RebirthMetabolismState metabolism;
        RebirthWorldCharacterService.TryGetMetabolism(player, out metabolism);
        RebirthConditionResolvedSnapshot resolved = RebirthConditionResolver.Resolve(player, record, metabolism);

        float loss = 0f;
        if (condition.SevereDehydrationActiveSeconds > RebirthConditionRuntimeConfig.DehydrationGraceRealSeconds)
            loss += ResolveDeprivationLossRate(hydrationPercent,
                RebirthConditionRuntimeConfig.DehydrationThresholdPercent,
                RebirthConditionRuntimeConfig.DehydrationCriticalPercent,
                RebirthConditionRuntimeConfig.DehydrationLossPerRealMinute,
                RebirthConditionRuntimeConfig.DehydrationCriticalLossPerRealMinute,
                RebirthConditionRuntimeConfig.DehydrationZeroLossPerRealMinute);
        if (condition.SevereMalnutritionActiveSeconds > RebirthConditionRuntimeConfig.MalnutritionGraceRealSeconds)
            loss += ResolveDeprivationLossRate(nutritionPercent,
                RebirthConditionRuntimeConfig.MalnutritionThresholdPercent,
                RebirthConditionRuntimeConfig.MalnutritionCriticalPercent,
                RebirthConditionRuntimeConfig.MalnutritionLossPerRealMinute,
                RebirthConditionRuntimeConfig.MalnutritionCriticalLossPerRealMinute,
                RebirthConditionRuntimeConfig.MalnutritionZeroLossPerRealMinute);

        if (loss > 0f)
        {
            float adjustedLoss = loss * resolved.HealthCapacityLossMultiplier * activeSeconds / 60f;
            condition.HealthCapacity = Mathf.Max(min, condition.HealthCapacity - adjustedLoss);
        }

        float nativeBlockage = Mathf.Max(0f, EffectManager.GetValue(PassiveEffects.HealthMaxBlockage, _entity: player));
        bool illness = RebirthConditionTraitModifierService.IsSeriousIllness(player);
        float nativeMultiplier = resolved.HealthCapacityLossMultiplier *
            (illness ? resolved.HealthCapacityIllnessLossMultiplier : resolved.HealthCapacityInjuryLossMultiplier);
        float authoredIllnessPenalty = illness ? RebirthConditionRuntimeConfig.DysenteryNativeCapacityPenalty : 0f;
        float nativeTarget = Mathf.Clamp(potential - nativeBlockage * nativeMultiplier - authoredIllnessPenalty, min, potential);

        // Injury/disease max-health restrictions remain responsive: a new native blockage can lower
        // Capacity immediately. Restoration is deliberately slower and never adds current Health.
        if (condition.HealthCapacity > nativeTarget)
            condition.HealthCapacity = nativeTarget;

        bool deprivationCurrentlySevere = hydrationPercent < RebirthConditionRuntimeConfig.DehydrationThresholdPercent ||
            nutritionPercent < RebirthConditionRuntimeConfig.MalnutritionThresholdPercent;
        if (!deprivationCurrentlySevere && condition.HealthCapacity < nativeTarget)
        {
            float recoveryRate = RebirthConditionRuntimeConfig.HealthCapacityRecoveryPerRealMinute * resolved.HealthCapacityRecoveryMultiplier;
            condition.HealthCapacity = Mathf.MoveTowards(condition.HealthCapacity, nativeTarget,
                Math.Max(0f, recoveryRate) * activeSeconds / 60f);
        }

        condition.HealthCapacity = Mathf.Clamp(condition.HealthCapacity, min, potential);
        ClampCurrentHealthToCapacity(player, condition.HealthCapacity);
        return Math.Abs(beforeCapacity - condition.HealthCapacity) > 0.0001f ||
            Math.Abs(beforeDehydration - condition.SevereDehydrationActiveSeconds) > 0.0001f ||
            Math.Abs(beforeMalnutrition - condition.SevereMalnutritionActiveSeconds) > 0.0001f;
    }

    /// <summary>
    /// Canonical max-health application used by the PlayerEntityStats health patch. When no
    /// committed Rebirth character exists, native HealthMaxBlockage behavior is preserved.
    /// </summary>
    public static void ApplyNativeHealthCapacity(PlayerEntityStats stats, EntityPlayer player)
    {
        if (stats == null || player == null || stats.Health == null)
            return;

        // Do not let persisted Survivor injury/deprivation capacity override native god mode.
        // This is a live-stat override only; the stored condition remains untouched and returns
        // when god mode is disabled.
        if (player.IsGodMode.Value)
        {
            ApplyGodModeHealth(stats);
            return;
        }

        RebirthWorldCharacterRecord record;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || !RebirthWorldCharacterService.TryGet(player, out record) ||
            record == null || !record.IsComplete || record.Condition == null)
        {
            stats.Health.MaxModifier = -EffectManager.GetValue(PassiveEffects.HealthMaxBlockage, _entity: player);
            return;
        }

        Normalize(record);
        float capacity = Mathf.Max(RebirthConditionRuntimeConfig.MinimumHealthCapacity, record.Condition.HealthCapacity);
        // Stat.Max is the native healthy base. Express the Survivor capacity as the one canonical
        // blockage/bonus modifier rather than repeatedly rewriting Stat.Max itself.
        stats.Health.MaxModifier = capacity - stats.Health.Max;
    }


    private static void ApplyGodModeHealth(EntityStats stats)
    {
        if (stats == null || stats.Health == null)
            return;

        stats.Health.MaxModifier = 0f;
        stats.Health.Value = stats.Health.ModifiedMax;
    }

    public static void ClampCurrentHealthToCapacity(EntityPlayer player, float capacity)
    {
        if (player == null || player.Stats == null || player.Stats.Health == null)
            return;
        float safeCapacity = Math.Max(RebirthConditionRuntimeConfig.MinimumHealthCapacity, capacity);
        if (player.Stats.Health.Value > safeCapacity + 0.001f)
            player.Stats.Health.Value = safeCapacity;
    }

    /// <summary>Compatibility surface retained for other condition contributors.</summary>
    public static bool MoveToward(RebirthWorldCharacterRecord record, float target, float lossPerMinute, float recoveryPerMinute, float activeSeconds)
    {
        if (record == null || record.Condition == null || activeSeconds <= 0f) return false;
        float potential = GetPotential(record);
        if (potential <= 0f) return false;
        float min = Math.Min(potential, RebirthConditionRuntimeConfig.MinimumHealthCapacity);
        target = Mathf.Clamp(target, min, potential);
        float current = Mathf.Clamp(record.Condition.HealthCapacity, min, potential);
        float rate = target < current ? Math.Max(0f, lossPerMinute) : Math.Max(0f, recoveryPerMinute);
        float next = Mathf.MoveTowards(current, target, rate * activeSeconds / 60f);
        if (Math.Abs(next - record.Condition.HealthCapacity) <= 0.0001f) return false;
        record.Condition.HealthCapacity = next;
        return true;
    }

    private static float ResolveDeprivationLossRate(float percent, float threshold, float critical,
        float ordinaryRate, float criticalRate, float zeroRate)
    {
        if (percent >= threshold) return 0f;
        if (percent > critical)
        {
            float t = 1f - Mathf.InverseLerp(critical, threshold, percent);
            return Mathf.Lerp(ordinaryRate, criticalRate, t);
        }
        float criticalT = critical > 0.0001f ? 1f - Mathf.Clamp01(percent / critical) : 1f;
        return Mathf.Lerp(criticalRate, zeroRate, criticalT);
    }

    private static float Percent(float value, float max)
    {
        return max > 0.001f ? Mathf.Clamp01(value / max) : 0f;
    }
}
