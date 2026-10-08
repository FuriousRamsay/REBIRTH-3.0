using System;
using UnityEngine;

#nullable disable

public static class RebirthMoodService
{
    public static bool Tick(EntityPlayer player, RebirthWorldCharacterRecord record, RebirthMetabolismState metabolism, float activeSeconds)
    {
        if (record == null || record.Condition == null || activeSeconds <= 0f) return false;
        RebirthWorldConditionState condition = record.Condition;
        RebirthConditionResolvedSnapshot resolved = RebirthConditionResolver.Resolve(player, record, metabolism);
        float oldTarget = condition.MoodTarget;
        float oldCurrent = condition.MoodCurrent;
        condition.MoodTarget = resolved.MoodTarget;
        float ratePerMinute;
        if (condition.MoodTarget >= condition.MoodCurrent)
        {
            ratePerMinute = RebirthConditionRuntimeConfig.MoodRisePerRealMinute;
            // Optimistic/Pessimistic recovery only changes upward recovery from a sub-neutral state.
            if (condition.MoodCurrent < RebirthConditionRuntimeConfig.MoodNeutral)
                ratePerMinute *= resolved.MoodRiseRateMultiplier;
        }
        else
        {
            ratePerMinute = RebirthConditionRuntimeConfig.MoodFallPerRealMinute * resolved.MoodFallRateMultiplier;
        }
        float maxDelta = Math.Max(0f, ratePerMinute) * activeSeconds / 60f;
        condition.MoodCurrent = Mathf.MoveTowards(condition.MoodCurrent, condition.MoodTarget, maxDelta);
        condition.MoodCurrent = Mathf.Clamp(condition.MoodCurrent, RebirthConditionRuntimeConfig.MoodMin, RebirthConditionRuntimeConfig.MoodMax);
        return Math.Abs(oldTarget - condition.MoodTarget) > 0.0001f || Math.Abs(oldCurrent - condition.MoodCurrent) > 0.0001f;
    }

    public static bool Tick(RebirthWorldCharacterRecord record, float activeSeconds)
    {
        return Tick(null, record, null, activeSeconds);
    }

    public static string GetStateLabel(float mood)
    {
        if (mood >= 80f) return "Excellent";
        if (mood >= 60f) return "Good";
        if (mood >= 40f) return "Neutral";
        if (mood >= 20f) return "Low";
        return "Miserable";
    }
}
