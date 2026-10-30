using System;using System.Collections.Generic;using UnityEngine;
public static partial class RebirthMetabolismService {
private static bool IsGodModeActive(EntityPlayer player)
    {
        return player != null && player.IsGodMode.Value;
    }
public static float GetEnergyReserveCeiling(EntityPlayer player)
    {
        if (player == null || player.Stats == null || player.Stats.Water == null || player.Stats.Food == null)
            return RebirthMetabolismConfig.EnergyMax;

        // God mode is a native "fully restored" state. Keep the custom Energy reserve in the same
        // state instead of only allowing the sustainable cap to return to 100.
        if (IsGodModeActive(player))
            return RebirthMetabolismConfig.EnergyMax;

        float hydrationPercent = player.Stats.Water.ModifiedMax > 0.001f
            ? Mathf.Clamp01(player.Stats.Water.Value / player.Stats.Water.ModifiedMax)
            : 0f;
        float nutritionPercent = player.Stats.Food.ModifiedMax > 0.001f
            ? Mathf.Clamp01(player.Stats.Food.Value / player.Stats.Food.ModifiedMax)
            : 0f;

        return RebirthMetabolismConfig.GetEnergyReserveCeiling(hydrationPercent, nutritionPercent);
    }
public static void ClampEnergyToReserveCeiling(EntityPlayer player, RebirthMetabolismState state)
    {
        if (state == null) return;
        float ceiling = GetEnergyReserveCeiling(player);
        state.Energy = Mathf.Clamp(state.Energy, 0f, Mathf.Max(0f, ceiling));
    }
private static float ProcessEnergy(EntityPlayer player, RebirthMetabolismState state, RebirthMetabolismModifiers mods, float usePerRealMinute, float deltaRealMinutes)
    {
        if (state != null)
            state.LastEnergyRecoveryNutritionUsePerRealMinute = 0f;
        if (state == null || player == null || player.Stats == null || deltaRealMinutes <= 0f)
            return 0f;

        if (IsGodModeActive(player))
        {
            state.Energy = RebirthMetabolismConfig.EnergyMax;
            return 0f;
        }

        float ceilingBeforeWork = GetEnergyReserveCeiling(player);
        state.Energy = Mathf.Clamp(state.Energy, 0f, ceilingBeforeWork);
        state.Energy = Mathf.Clamp(state.Energy - Mathf.Max(0f, usePerRealMinute) * Mathf.Max(0f, mods.EnergyUse) * deltaRealMinutes,
            0f, ceilingBeforeWork);

        float activity = Mathf.Max(0f, state.SmoothedActivity);
        float recoveryFactor = GetEnergyRecoveryFactor(activity);
        float foodFloor = player.Stats.Food.ModifiedMax * RebirthMetabolismConfig.EnergyRecoveryFoodFloorPercent;
        if (recoveryFactor <= 0f || player.Stats.Food.Value <= foodFloor || state.Energy >= ceilingBeforeWork - 0.001f)
            return 0f;

        // Professional Cooking is digestion-time efficiency, never instant Energy/Nutrition. The
        // preparation snapshot only becomes active after that prepared meal has physically reached
        // the intestinal nutrition compartment. Multiple meals do not multiply; the best active
        // prepared-meal snapshot applies while its nutrients are present.
        float preparedEfficiency = GetPreparedMealEnergyEfficiencyMultiplier(state);
        float hydrationRecoveryMultiplier = GetEnergyRecoveryHydrationMultiplier(player);
        float requested = RebirthMetabolismConfig.EnergyRecoveryPerRealMinute * recoveryFactor * hydrationRecoveryMultiplier
            * Mathf.Max(0f, mods.EnergyRecovery) * preparedEfficiency * deltaRealMinutes;
        requested = Mathf.Min(requested, Mathf.Max(0f, ceilingBeforeWork - state.Energy));
        float foodCostPerEnergy = Mathf.Max(0f, RebirthMetabolismConfig.FoodUnitsPerEnergyRecovered) / Mathf.Max(1f, preparedEfficiency);
        if (foodCostPerEnergy > 0f)
        {
            float availableFood = Mathf.Max(0f, player.Stats.Food.Value - foodFloor);
            requested = Mathf.Min(requested, availableFood / foodCostPerEnergy);
        }

        float beforeRecovery = state.Energy;
        float foodSpent = 0f;
        if (requested > 0f)
        {
            state.Energy += requested;
            if (foodCostPerEnergy > 0f)
            {
                foodSpent = requested * foodCostPerEnergy;
                player.Stats.Food.Value -= foodSpent;
            }
            ClampEnergyToReserveCeiling(player, state);
        }

        state.LastEnergyRecoveryNutritionUsePerRealMinute = deltaRealMinutes > 0.00001f ? foodSpent / deltaRealMinutes : 0f;
        float actuallyRecovered = Mathf.Max(0f, state.Energy - beforeRecovery);
        return deltaRealMinutes > 0.00001f ? actuallyRecovered / deltaRealMinutes : 0f;
    }
private static float GetPreparedMealEnergyEfficiencyMultiplier(RebirthMetabolismState state)
    {
        if (state == null || state.IngestionEntries == null) return 1f;
        float best = 1f;
        for (int i = 0; i < state.IngestionEntries.Count; i++)
        {
            RebirthIngestionEntry entry = state.IngestionEntries[i];
            if (entry == null || entry.Kind != RebirthIngestionKind.Food || entry.IntestinalNutritionUnits <= 0.001f) continue;
            best = Mathf.Max(best, Mathf.Clamp(entry.PreparedMealEnergyEfficiencyMultiplier, 1f, 2f));
        }
        return best;
    }
private static float GetEnergyRecoveryFactor(float activity)
    {
        activity = Mathf.Max(0f, activity);
        if (activity < 0.5f) return 1f;       // idle: full recovery
        if (activity < 1.5f) return 0.15f;    // walking/light work: only a little recovery
        if (activity < 2.5f) return 0.05f;    // moderate work: effectively no recovery
        return 0f;                            // running/extreme work: no recovery
    }
private static float EstimateEnergyRecoveryPerRealMinute(EntityPlayer player, RebirthMetabolismState state, RebirthMetabolismModifiers mods)
    {
        if (player == null || player.Stats == null || state == null)
            return 0f;
        float ceiling = GetEnergyReserveCeiling(player);
        float foodFloor = player.Stats.Food.ModifiedMax * RebirthMetabolismConfig.EnergyRecoveryFoodFloorPercent;
        if (player.Stats.Food.Value <= foodFloor || state.Energy >= ceiling - 0.001f)
            return 0f;
        return RebirthMetabolismConfig.EnergyRecoveryPerRealMinute
            * GetPreparedMealEnergyEfficiencyMultiplier(state)
            * GetEnergyRecoveryFactor(state.SmoothedActivity)
            * GetEnergyRecoveryHydrationMultiplier(player)
            * Mathf.Max(0f, mods.EnergyRecovery);
    }
public static float GetEnergyRecoveryHydrationMultiplier(EntityPlayer player)
    {
        if (player == null || player.Stats == null || player.Stats.Water == null)
            return 1f;

        float max = Mathf.Max(1f, player.Stats.Water.ModifiedMax);
        float hydrationPercent = Mathf.Clamp01(player.Stats.Water.Value / max);
        return RebirthMetabolismConfig.GetEnergyHydrationEfficiency(hydrationPercent);
    }
private static void ResolveActivity(EntityPlayer player, RebirthMetabolismState state, float deltaRealMinutes,
        out float hydrationMlPerReal60Minutes, out float energyUsePerRealMinute)
    {
        float target = 0f;
        FastTags<TagGroup.Global> tag = player.CurrentMovementTag;
        if (tag.Test_AnySet(TagSwimmingRun)) target = 4f;
        else if (tag.Test_AnySet(TagRunning)) target = 3f;
        else if (tag.Test_AnySet(TagSwimming) || tag.Test_AnySet(TagClimbing)) target = 2.5f;
        else if (tag.Test_AnySet(TagJumping)) target = 2f;
        else if (tag.Test_AnySet(TagWalking)) target = 1f;

        // Movement tags capture sustained exertion. Net Stamina expenditure captures short,
        // expensive actions (repeated attacks, mining, jumping, etc.) that can happen between
        // metabolism ticks. It both raises the activity tier immediately and carries a small
        // direct Energy cost so a player repeatedly emptying Stamina cannot remain near 100 Energy.
        float stamina = player.Stats.Stamina.Value;
        float staminaSpent = 0f;
        if (state.LastStamina >= 0f && stamina < state.LastStamina - 0.05f)
        {
            staminaSpent = Mathf.Max(0f, state.LastStamina - stamina);
            float staminaTarget = Mathf.Clamp(1f + staminaSpent * 0.60f, 1f, 4f);
            target = Mathf.Max(target, staminaTarget);
        }
        state.LastStamina = stamina;

        float smoothed = Mathf.Lerp(state.SmoothedActivity, target, 0.35f);
        // Do not smooth away a real Stamina-spend spike on the tick where it happened.
        state.SmoothedActivity = staminaSpent > 0.05f ? Mathf.Max(smoothed, target) : smoothed;

        float a = state.SmoothedActivity;
        if (a < 0.5f)
        {
            hydrationMlPerReal60Minutes = 0f;
            energyUsePerRealMinute = 0f;
        }
        else if (a < 1.5f)
        {
            hydrationMlPerReal60Minutes = 25f;
            energyUsePerRealMinute = RebirthMetabolismConfig.EnergyLightUsePerRealMinute;
        }
        else if (a < 2.5f)
        {
            hydrationMlPerReal60Minutes = 100f;
            energyUsePerRealMinute = RebirthMetabolismConfig.EnergyModerateUsePerRealMinute;
        }
        else if (a < 3.5f)
        {
            hydrationMlPerReal60Minutes = 250f;
            energyUsePerRealMinute = RebirthMetabolismConfig.EnergyHighUsePerRealMinute;
        }
        else
        {
            hydrationMlPerReal60Minutes = 400f;
            energyUsePerRealMinute = RebirthMetabolismConfig.EnergyExtremeUsePerRealMinute;
        }

        energyUsePerRealMinute = RebirthEfficientMovementService.Apply(player, energyUsePerRealMinute);

        if (staminaSpent > 0f && deltaRealMinutes > 0.00001f)
        {
            float directStaminaCost = staminaSpent * Mathf.Max(0f, RebirthMetabolismConfig.EnergyPerStaminaSpent);
            energyUsePerRealMinute += directStaminaCost / deltaRealMinutes;
        }
    }
public static RebirthMetabolismSnapshot BuildSnapshot(EntityPlayer player)
    {
        RebirthMetabolismState state = RebirthMetabolismStateRepository.GetOrCreate(player);
        if (IsServerAuthority && state != null && IsGodModeActive(player))
            state.Energy = RebirthMetabolismConfig.EnergyMax;
        RebirthMetabolismModifiers mods = RebirthMetabolismModifierResolver.Resolve(player, state);
        float aH, aF, hH, cF, iH, iF, d;
        ResolveActivityNoMutation(player, state, out aH, out aF);
        ResolveTemperature(player, mods, out hH, out cF);
        ResolveIllness(player, out iH, out iF, out d);
        float hyd = (RebirthMetabolismConfig.BaseFluidNeedMlPerReal60Minutes * mods.HydrationRequirement + aH + hH + iH)
            * mods.TotalHydrationDemand;
        float food = (RebirthMetabolismConfig.BaseFoodNeedUnitsPerReal60Minutes * mods.FoodRequirement + cF + iF)
            * mods.TotalNutritionDemand;
        float capacity = Mathf.Max(100f, mods.StomachCapacity);
        float energyUse = RebirthEfficientMovementService.Apply(player, aF) * mods.EnergyUse;
        float energyRecovery = EstimateEnergyRecoveryPerRealMinute(player, state, mods);
        return BuildSnapshot(player, state, mods, hyd, food, energyUse, energyRecovery,
            RebirthMetabolismConfig.BaseIntestinalFluidAbsorptionMlPerRealMinute * mods.FluidAbsorption,
            RebirthMetabolismConfig.BaseSolidGastricEmptyingMlPerRealMinute * mods.DigestionSpeed,
            RebirthMetabolismConfig.BaseIntestinalNutrientAbsorptionUnitsPerRealMinute * mods.DigestionSpeed,
            EstimateCurrentGastricEmptyingMlPerRealMinute(state, mods, capacity));
    }
private static void ResolveActivityNoMutation(EntityPlayer player, RebirthMetabolismState state, out float hydration, out float energyUsePerRealMinute)
    {
        float a = state != null ? state.SmoothedActivity : 0f;
        if (a < 0.5f) { hydration = 0f; energyUsePerRealMinute = 0f; }
        else if (a < 1.5f) { hydration = 25f; energyUsePerRealMinute = RebirthMetabolismConfig.EnergyLightUsePerRealMinute; }
        else if (a < 2.5f) { hydration = 100f; energyUsePerRealMinute = RebirthMetabolismConfig.EnergyModerateUsePerRealMinute; }
        else if (a < 3.5f) { hydration = 250f; energyUsePerRealMinute = RebirthMetabolismConfig.EnergyHighUsePerRealMinute; }
        else { hydration = 400f; energyUsePerRealMinute = RebirthMetabolismConfig.EnergyExtremeUsePerRealMinute; }
    }
public static void Step(EntityPlayer player,RebirthMetabolismState state,RebirthMetabolismModifiers mods,float seconds){
ResolveActivity(player,state,seconds/60f,out _,out float energyUsePerRealMinute);
float energyRecoveryPerRealMinute=ProcessEnergy(player,state,mods,energyUsePerRealMinute,seconds/60f);

}
}
public static class RebirthMetabolismConfig{
public static float UpdateRealSeconds = 1f;
public static float MlPerHydrationPoint = 25f;
public static float BaseStomachCapacityMl = 1500f;
public static float BaseIntestinalCapacityMl = 1000f;
public static float ComfortableFullness = 0.70f;
public static float FullThreshold = 0.85f;
public static float HardFullness = 1.00f;
public static float FoodMinimumFitFraction = 0.80f;
public static float BaseFluidNeedMlPerReal60Minutes = 900f;
public static float ClearLiquidHoldRealSeconds = 20f;
public static float ClearLiquidGastricHalfTimeRealSeconds = 120f;
public static float ClearLiquidWithFoodHalfTimeMultiplier = 1.60f;
public static float MealFluidHoldRealSeconds = 45f;
public static float MealFluidGastricHalfTimeRealSeconds = 300f;
public static float IntestinalFluidResidenceRealSeconds = 30f;
public static float BaseIntestinalFluidAbsorptionMlPerRealMinute = 112.5f;
public static float AutoSipStartProjectedPercent = 0.72f;
public static float AutoSipStopProjectedPercent = 0.85f;
public static float AutoSipDefaultMl = 50f;
public static float AutoSipCooldownRealSeconds = 25f;
public static float CriticalDehydrationHealthLossPerRealMinute = 0.067f;
public static float RequirementMinMultiplier = 0.70f;
public static float RequirementMaxMultiplier = 1.40f;
public static float UtilizationMinMultiplier = 0.70f;
public static float UtilizationMaxMultiplier = 1.30f;
public static float AbsorptionMinMultiplier = 0.50f;
public static float AbsorptionMaxMultiplier = 1.75f;
public static float BaseFoodNeedUnitsPerReal60Minutes = 30f;
public static float EnergyMax = 100f;
public static float EnergyLightUsePerRealMinute = 1.50f;
public static float EnergyModerateUsePerRealMinute = 4.00f;
public static float EnergyHighUsePerRealMinute = 8.00f;
public static float EnergyExtremeUsePerRealMinute = 12.00f;
public static float EnergyRecoveryPerRealMinute = 4.00f;
public static float EnergyPerStaminaSpent = 0.05f;
public static float FoodUnitsPerEnergyRecovered = 0.20f;
public static float EnergyRecoveryFoodFloorPercent = 0.00f;
public static float EnergyMinimumSustainableCap = 10f;
public static float EnergyHydrationFullEfficiencyPercent = 0.75f;
public static float EnergyHydrationMediumEfficiencyPercent = 0.50f;
public static float EnergyHydrationCriticalEfficiencyPercent = 0.25f;
public static float EnergyHydrationEfficiencyAtMedium = 0.85f;
public static float EnergyHydrationEfficiencyAtCritical = 0.60f;
public static float EnergyHydrationEfficiencyAtZero = 0.25f;
public static float EnergyLowThreshold = 75f;
public static float EnergyCriticalThreshold = 25f;
public static float StaminaRegenAtLowEnergy = 1.00f;
public static float StaminaRegenAtCriticalEnergy = 0.50f;
public static float StaminaRegenAtZeroEnergy = 0.20f;
public static float FoodRequirementMinMultiplier = 0.70f;
public static float FoodRequirementMaxMultiplier = 1.40f;
public static float BaseDigestiveHealth = 100f;
public static float BaseSolidGastricEmptyingMlPerRealMinute = 45f;
public static float BaseIntestinalNutrientAbsorptionUnitsPerRealMinute = 2.25f;
public static float IntestinalNutritionResidenceRealSeconds = 60f;
public static float LightFoodGastricLagRealSeconds = 60f;
public static float NormalFoodGastricLagRealSeconds = 90f;
public static float DenseFoodGastricLagRealSeconds = 120f;
public static float HeavyFoodGastricLagRealSeconds = 180f;
public static float DigestionSpeedMinMultiplier = 0.50f;
public static float DigestionSpeedMaxMultiplier = 1.75f;
public static float GutResilienceMinMultiplier = 0.50f;
public static float GutResilienceMaxMultiplier = 1.75f;
public static float DigestiveEnzymesRealSeconds = 300f;
public static float ElectrolytesRealSeconds = 900f;
public static float ProbioticsRealSeconds = 3600f;
public static float PrebioticRealSeconds = 1800f;
public static float GetEnergyHydrationEfficiency(float hydrationPercent)
    {
        float p = Mathf.Clamp01(hydrationPercent);
        float full = Mathf.Clamp01(EnergyHydrationFullEfficiencyPercent);
        float medium = Mathf.Clamp(EnergyHydrationMediumEfficiencyPercent, 0f, full);
        float critical = Mathf.Clamp(EnergyHydrationCriticalEfficiencyPercent, 0f, medium);

        if (p >= full)
            return 1f;
        if (p >= medium)
        {
            float t = full > medium ? Mathf.InverseLerp(medium, full, p) : 1f;
            return Mathf.Lerp(EnergyHydrationEfficiencyAtMedium, 1f, t);
        }
        if (p >= critical)
        {
            float t = medium > critical ? Mathf.InverseLerp(critical, medium, p) : 1f;
            return Mathf.Lerp(EnergyHydrationEfficiencyAtCritical, EnergyHydrationEfficiencyAtMedium, t);
        }

        float c = critical > 0.001f ? p / critical : 0f;
        return Mathf.Lerp(EnergyHydrationEfficiencyAtZero, EnergyHydrationEfficiencyAtCritical, c);
    }
public static float GetEnergyReserveCeiling(float hydrationPercent, float nutritionPercent)
    {
        float nutrition = Mathf.Clamp01(nutritionPercent);
        float hydrationEfficiency = GetEnergyHydrationEfficiency(hydrationPercent);
        float raw = EnergyMax * nutrition * hydrationEfficiency;
        float minimum = Mathf.Clamp(EnergyMinimumSustainableCap, 0f, EnergyMax);
        return Mathf.Clamp(raw, minimum, EnergyMax);
    }
}