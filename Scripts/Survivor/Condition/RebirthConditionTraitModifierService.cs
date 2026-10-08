using System;
using System.Globalization;

#nullable disable

/// <summary>
/// Interprets runtime <component> entries from the authoritative Trait modifier profiles.
/// This keeps condition/metabolism Traits data-driven and prevents parallel hard-coded Trait lists
/// from appearing in Mood, Energy, digestion and Health Capacity services.
/// </summary>
public static class RebirthConditionTraitModifierService
{
    public static void ApplyRuntimeComponents(EntityPlayer player, RebirthMetabolismState metabolism,
        RebirthWorldCharacterRecord record, RebirthConditionResolvedSnapshot result)
    {
        if (record == null || record.Origin == null || result == null)
            return;

        bool strenuous = metabolism != null && metabolism.SmoothedActivity >= 1.5f;
        bool sprintLike = metabolism != null && metabolism.SmoothedActivity >= 2.5f;
        bool moving = metabolism != null && metabolism.SmoothedActivity >= 0.5f;
        bool injured = player != null && player.Stats != null &&
            EffectManager.GetValue(PassiveEffects.HealthMaxBlockage, _entity: player) > 0.01f;
        bool ill = IsSeriousIllness(player);
        bool encumbered = IsActuallyEncumbered(player);

        for (int i = 0; i < record.Origin.TraitIds.Count; i++)
        {
            RebirthTraitDefinition trait;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(record.Origin.TraitIds[i], out trait) || trait == null)
                continue;
            // Aptitudes have already been baked into the immutable starting Skill snapshot.
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) continue;
            RebirthConditionModifierProfileDefinition profile;
            if (!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile) || profile == null)
                continue;

            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "runtime", StringComparison.OrdinalIgnoreCase))
                    continue;
                float value;
                if (!float.TryParse(component.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    continue;

                string target = (component.Target ?? string.Empty).Trim().ToLowerInvariant();
                if (target == "energy.use" || (target == "energy.use.strenuous" && strenuous) ||
                    (target == "energy.use.sprint_jump" && sprintLike) || (target == "energy.use.movement" && moving) ||
                    (target == "energy.use.injured" && injured) || (target == "energy.use.encumbered" && encumbered))
                    ApplyMultiplyOrAdd(ref result.EnergyUseMultiplier, component.Operation, value);
                else if (target == "energy.recovery")
                    ApplyMultiplyOrAdd(ref result.EnergyRecoveryMultiplier, component.Operation, value);
                else if (target == "hydration.demand.total")
                    ApplyMultiplyOrAdd(ref result.HydrationDemandMultiplier, component.Operation, value);
                else if (target == "nutrition.demand.total")
                    ApplyMultiplyOrAdd(ref result.NutritionUseMultiplier, component.Operation, value);
                else if (target == "environment.heat")
                    ApplyMultiplyOrAdd(ref result.HeatPenaltyMultiplier, component.Operation, value);
                else if (target == "environment.cold")
                    ApplyMultiplyOrAdd(ref result.ColdPenaltyMultiplier, component.Operation, value);
                else if (target == "digestion.speed")
                    ApplyMultiplyOrAdd(ref result.DigestionSpeedMultiplier, component.Operation, value);
                else if (target == "digestion.fluid_absorption")
                    ApplyMultiplyOrAdd(ref result.FluidAbsorptionMultiplier, component.Operation, value);
                else if (target == "digestion.nutrient_utilization")
                    ApplyMultiplyOrAdd(ref result.NutrientUtilizationMultiplier, component.Operation, value);
                else if (target == "digestion.gut_resilience")
                    ApplyMultiplyOrAdd(ref result.GutResilienceMultiplier, component.Operation, value);
                else if (target == "digestion.recovery")
                    ApplyMultiplyOrAdd(ref result.DigestiveRecoveryMultiplier, component.Operation, value);
                else if (target == "digestion.baseline")
                    ApplyMultiplyOrAdd(ref result.DigestiveBaselineOffset, component.Operation, value);
                else if (target == "health.capacity.loss")
                    ApplyMultiplyOrAdd(ref result.HealthCapacityLossMultiplier, component.Operation, value);
                else if (target == "health.capacity.injury_loss")
                    ApplyMultiplyOrAdd(ref result.HealthCapacityInjuryLossMultiplier, component.Operation, value);
                else if (target == "health.capacity.illness_loss")
                    ApplyMultiplyOrAdd(ref result.HealthCapacityIllnessLossMultiplier, component.Operation, value);
                else if (target == "health.capacity.recovery" || (target == "health.capacity.illness_recovery" && ill))
                    ApplyMultiplyOrAdd(ref result.HealthCapacityRecoveryMultiplier, component.Operation, value);
                else if (target == "mood.recovery.negative")
                    ApplyMultiplyOrAdd(ref result.MoodRiseRateMultiplier, component.Operation, value);
                else if (target == "mood.injury")
                    ApplyMultiplyOrAdd(ref result.InjuryMoodMultiplier, component.Operation, value);
                else if (target == "stamina.recovery.energy")
                    ApplyMultiplyOrAdd(ref result.StaminaRecoveryMultiplier, component.Operation, value);
            }
        }

        result.EnergyUseMultiplier = ClampMultiplier(result.EnergyUseMultiplier, 0.65f, 1.50f);
        result.EnergyRecoveryMultiplier = ClampMultiplier(result.EnergyRecoveryMultiplier, 0.60f, 1.50f);
        result.HydrationDemandMultiplier = ClampMultiplier(result.HydrationDemandMultiplier, 0.65f, 1.50f);
        result.NutritionUseMultiplier = ClampMultiplier(result.NutritionUseMultiplier, 0.65f, 1.50f);
        result.HeatPenaltyMultiplier = ClampMultiplier(result.HeatPenaltyMultiplier, 0.50f, 1.50f);
        result.ColdPenaltyMultiplier = ClampMultiplier(result.ColdPenaltyMultiplier, 0.50f, 1.50f);
        result.DigestionSpeedMultiplier = ClampMultiplier(result.DigestionSpeedMultiplier, 0.60f, 1.50f);
        result.FluidAbsorptionMultiplier = ClampMultiplier(result.FluidAbsorptionMultiplier, 0.60f, 1.50f);
        result.NutrientUtilizationMultiplier = ClampMultiplier(result.NutrientUtilizationMultiplier, 0.60f, 1.50f);
        result.GutResilienceMultiplier = ClampMultiplier(result.GutResilienceMultiplier, 0.60f, 1.50f);
        result.DigestiveRecoveryMultiplier = ClampMultiplier(result.DigestiveRecoveryMultiplier, 0.60f, 1.50f);
        result.HealthCapacityLossMultiplier = ClampMultiplier(result.HealthCapacityLossMultiplier, 0.50f, 1.60f);
        result.HealthCapacityInjuryLossMultiplier = ClampMultiplier(result.HealthCapacityInjuryLossMultiplier, 0.50f, 1.60f);
        result.HealthCapacityIllnessLossMultiplier = ClampMultiplier(result.HealthCapacityIllnessLossMultiplier, 0.50f, 1.60f);
        result.HealthCapacityRecoveryMultiplier = ClampMultiplier(result.HealthCapacityRecoveryMultiplier, 0.50f, 1.60f);
        result.MoodRiseRateMultiplier = ClampMultiplier(result.MoodRiseRateMultiplier, 0.60f, 1.50f);
        result.MoodFallRateMultiplier = ClampMultiplier(result.MoodFallRateMultiplier, 0.60f, 1.50f);
        result.InjuryMoodMultiplier = ClampMultiplier(result.InjuryMoodMultiplier, 0.50f, 1.60f);
        result.StaminaRecoveryMultiplier = ClampMultiplier(result.StaminaRecoveryMultiplier, 0.60f, 1.30f);
    }

    public static float AdjustFoodMood(RebirthWorldCharacterRecord record, RebirthFoodMoodDefinition food, float value)
    {
        if (record == null || record.Origin == null || food == null)
            return value;
        float adjusted = value;
        for (int i = 0; i < record.Origin.TraitIds.Count; i++)
        {
            RebirthTraitDefinition trait;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(record.Origin.TraitIds[i], out trait) || trait == null)
                continue;
            // Aptitudes have already been baked into the immutable starting Skill snapshot.
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) continue;
            RebirthConditionModifierProfileDefinition profile;
            if (!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile) || profile == null)
                continue;
            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "runtime", StringComparison.OrdinalIgnoreCase))
                    continue;
                float componentValue;
                if (!float.TryParse(component.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out componentValue))
                    continue;
                string target = (component.Target ?? string.Empty).Trim().ToLowerInvariant();
                bool applies = target == "food.mood.all" ||
                    (target == "food.mood.positive" && adjusted > 0f) ||
                    (target == "food.mood.negative" && adjusted < 0f) ||
                    (target == "food.mood.good_comfort" && adjusted > 0f &&
                        (string.Equals(food.MoodProfileId, "good", StringComparison.OrdinalIgnoreCase) || string.Equals(food.MoodProfileId, "comfort", StringComparison.OrdinalIgnoreCase))) ||
                    (target == "food.mood.plant_positive" && adjusted > 0f && IsPlantOnly(food)) ||
                    (target == "food.mood.meat_positive" && adjusted > 0f && food.DietTags.Contains("Meat")) ||
                    (target == "food.mood.sweet" && food.DietTags.Contains("Sweet"));
                if (applies)
                    ApplyMultiplyOrAdd(ref adjusted, component.Operation, componentValue);
            }
        }
        return Math.Max(-20f, Math.Min(20f, adjusted));
    }

    public static float AdjustFoodRepetitionMultiplier(RebirthWorldCharacterRecord record, RebirthFoodMoodDefinition food, float value)
    {
        if (record == null || record.Origin == null || food == null)
            return value;
        float adjusted = value;
        for (int i = 0; i < record.Origin.TraitIds.Count; i++)
        {
            RebirthTraitDefinition trait;
            RebirthConditionModifierProfileDefinition profile;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(record.Origin.TraitIds[i], out trait) || trait == null)
                continue;
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) continue;
            if (!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile) || profile == null)
                continue;
            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "runtime", StringComparison.OrdinalIgnoreCase))
                    continue;
                float componentValue;
                if (!float.TryParse(component.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out componentValue))
                    continue;
                string target = (component.Target ?? string.Empty).Trim().ToLowerInvariant();
                bool applies = (target == "food.repetition.simple" && string.Equals(food.MoodProfileId, "simple", StringComparison.OrdinalIgnoreCase)) ||
                    (target == "food.repetition.sweet" && food.DietTags.Contains("Sweet"));
                if (applies)
                    ApplyMultiplyOrAdd(ref adjusted, component.Operation, componentValue);
            }
        }
        return Math.Max(0f, Math.Min(1f, adjusted));
    }

    public static float GetDirectMoodTargetDelta(EntityPlayer player, RebirthWorldCharacterRecord record)
    {
        if (record == null || record.Origin == null) return 0f;
        bool needsCompanionContext = HasCompanionMoodComponent(record);
        bool nearbyCompanion = needsCompanionContext && HasNearbyOwnedCompanion(player, 15f);
        float result = 0f;
        for (int i = 0; i < record.Origin.TraitIds.Count; i++)
        {
            RebirthTraitDefinition trait; RebirthConditionModifierProfileDefinition profile;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(record.Origin.TraitIds[i], out trait) || trait == null) continue;
            if (RebirthSkillAptitudeTraitFactory.IsAptitude(trait)) continue;
            if (!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile) || profile == null) continue;
            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "runtime", StringComparison.OrdinalIgnoreCase)) continue;
                float value; if (!float.TryParse(component.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
                string target = (component.Target ?? string.Empty).Trim().ToLowerInvariant();
                bool applies = target == "mood.baseline" ||
                    (target == "mood.companion.nearby" && nearbyCompanion) ||
                    (target == "mood.companion.solo" && !nearbyCompanion);
                if (applies) ApplyMultiplyOrAdd(ref result, component.Operation, value);
            }
        }
        return Math.Max(-15f, Math.Min(15f, result));
    }

    private static bool HasCompanionMoodComponent(RebirthWorldCharacterRecord record)
    {
        if (record == null || record.Origin == null) return false;
        for (int i = 0; i < record.Origin.TraitIds.Count; i++)
        {
            RebirthTraitDefinition trait; RebirthConditionModifierProfileDefinition profile;
            if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(record.Origin.TraitIds[i], out trait) || trait == null) continue;
            if (!RebirthSurvivorDefinitionRegistry.TryGetModifier(trait.ModifierId, out profile) || profile == null) continue;
            for (int c = 0; c < profile.Components.Count; c++)
            {
                RebirthConditionModifierComponent component = profile.Components[c];
                if (component == null || !string.Equals(component.Phase, "runtime", StringComparison.OrdinalIgnoreCase)) continue;
                string target = (component.Target ?? string.Empty).Trim();
                if (string.Equals(target, "mood.companion.nearby", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(target, "mood.companion.solo", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    public static bool HasNearbyOwnedCompanion(EntityPlayer player, float radius)
    {
        if (player == null || player.world == null || player.world.Entities == null) return false;
        float radiusSq = Math.Max(1f, radius) * Math.Max(1f, radius);
        for (int i = 0; i < player.world.Entities.list.Count; i++)
        {
            Entity entity = player.world.Entities.list[i];
            if (entity == null || entity == player || !player.HasOwnedEntity(entity.entityId)) continue;
            if ((entity.position - player.position).sqrMagnitude <= radiusSq) return true;
        }
        return false;
    }

    private static bool IsActuallyEncumbered(EntityPlayer player)
    {
        if (player == null || player.Buffs == null) return false;
        // Vanilla V3 computes slot encumbrance into _encumberedslots and drives
        // buffEncumberedInv from that value. Use the native state instead of
        // assuming a Bag capacity member that does not exist in the current API.
        return player.Buffs.GetCustomVar("_encumberedslots") > 0.001f ||
            player.Buffs.HasBuff("buffEncumberedInv");
    }

    public static bool IsSeriousIllness(EntityPlayer player)
    {
        if (player == null || player.Buffs == null)
            return false;
        return player.Buffs.HasBuff("buffDysenteryMain") || player.Buffs.HasBuff("buffDysentery01Untreated") ||
            player.Buffs.HasBuff("buffDysentery01GetBetter");
    }

    public static bool SupportsFoodMoodTarget(string target)
    {
        string t=(target??string.Empty).Trim().ToLowerInvariant();
        return t=="food.mood.all" || t=="food.mood.positive" || t=="food.mood.negative" ||
            t=="food.mood.good_comfort" || t=="food.mood.plant_positive" ||
            t=="food.mood.meat_positive" || t=="food.mood.sweet";
    }

    public static bool SupportsConditionTraitTarget(string target)
    {
        string t=(target??string.Empty).Trim().ToLowerInvariant();
        return SupportsFoodMoodTarget(t) ||
            t=="energy.use" || t=="energy.use.strenuous" || t=="energy.use.sprint_jump" ||
            t=="energy.use.movement" || t=="energy.use.injured" || t=="energy.use.encumbered" ||
            t=="environment.heat" || t=="environment.cold" ||
            t=="mood.recovery.negative" || t=="mood.injury" ||
            t=="mood.baseline" || t=="mood.companion.nearby" || t=="mood.companion.solo";
    }

    public static bool SupportsHealthCapacityTraitTarget(string target)
    {
        string t=(target??string.Empty).Trim().ToLowerInvariant();
        return t=="health.capacity.loss" ||
            t=="health.capacity.injury_loss" ||
            t=="health.capacity.illness_loss" ||
            t=="health.capacity.recovery" ||
            t=="health.capacity.illness_recovery";
    }

    private static bool IsPlantOnly(RebirthFoodMoodDefinition food)
    {
        if (food == null || !food.DietTags.Contains("Plant"))
            return false;
        return !food.DietTags.Contains("Meat") && !food.DietTags.Contains("Fish") && !food.DietTags.Contains("Egg") &&
            !food.DietTags.Contains("Dairy") && !food.DietTags.Contains("AnimalProduct");
    }

    private static void ApplyMultiplyOrAdd(ref float current, string operation, float value)
    {
        if (string.Equals(operation, "multiply", StringComparison.OrdinalIgnoreCase)) current *= value;
        else if (string.Equals(operation, "add", StringComparison.OrdinalIgnoreCase)) current += value;
    }

    private static float ClampMultiplier(float value, float min, float max)
    {
        if (value < min) return min;
        return value > max ? max : value;
    }
}
