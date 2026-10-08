using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Server-authoritative Trait-support runtime. Support changes temporary state, never immutable
/// Trait/Diet/Attribute identity. All timers use the Survivor active-play clock and therefore
/// naturally freeze while the character is offline.
/// </summary>
public static class RebirthTraitSupportService
{
    public const float FocusSkillLevelCeiling = 50f;
    public const float FocusGlobalMultiplierCap = 1.05f;

    public static bool Tick(EntityPlayer player, RebirthWorldCharacterRecord record, float elapsedActiveSeconds)
    {
        if (record == null || record.Origin == null || record.Support == null || elapsedActiveSeconds <= 0f)
            return false;
        bool changed = EnsureHabitGrace(record);
        foreach (KeyValuePair<string, RebirthTraitSupportRuntimeState> pair in record.Support.Entries)
        {
            RebirthTraitSupportRuntimeState state = pair.Value;
            if (state == null) continue;
            changed |= Decrement(ref state.GraceRemainingActiveSeconds, elapsedActiveSeconds);
            changed |= Decrement(ref state.ManagedRemainingActiveSeconds, elapsedActiveSeconds);
            changed |= Decrement(ref state.PositiveRemainingActiveSeconds, elapsedActiveSeconds);
            changed |= Decrement(ref state.CooldownRemainingActiveSeconds, elapsedActiveSeconds);
            if (state.PositiveRemainingActiveSeconds <= 0f && state.Stacks > 0)
            {
                state.Stacks = 0;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>
    /// Server-owned completion path for non-metabolism support consumables/medicines. The server
    /// re-finds the exact item type/seed, fires the normal primary-action-end event so the base
    /// item purpose remains intact, removes one use, and only then applies Survivor support state.
    /// </summary>
    public static bool ConsumeAndApplyMatchingInventoryItem(EntityPlayer player, int itemType, ushort seed, out string message)
    {
        message = string.Empty;
        if (player == null || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
        { message = "Not authoritative."; return false; }
        if (RebirthCharacterCreationHoldService.IsHeld(player))
        { message = "Finish Survivor creation before using support items."; return false; }

        if (RebirthBackpackLibraryReservation.BlocksResourceUse(player))
        { message = Localization.Get("xuiRebirthLibraryTransferPending"); return false; }

        bool inBackpack; int slot; ItemStack stack;
        if (!TryFindMatchingInventoryStack(player, itemType, seed, out inBackpack, out slot, out stack))
        { message = "Support item is no longer available."; return false; }
        if (stack.itemValue == null || stack.itemValue.ItemClass == null)
        { message = "Invalid support item."; return false; }
        string itemId = stack.itemValue.ItemClass.GetItemName();
        RebirthTraitSupportProfileDefinition profile;
        if (!RebirthSurvivorDefinitionRegistry.TryGetSupportByItem(itemId, out profile) || profile == null)
        { message = "Item is not bound to a Survivor support profile."; return false; }

        // New support-only consumables have no useful native purpose while their nonstacking
        // support is cooling down, so reject before inventory is consumed. Existing medicines and
        // metabolism drinks are intentionally still usable because their ordinary purpose remains.
        if (string.Equals(profile.Kind, "consumable", StringComparison.OrdinalIgnoreCase))
        {
            RebirthWorldCharacterRecord cooldownRecord;
            if (RebirthWorldCharacterService.TryGet(player, out cooldownRecord) && cooldownRecord != null && cooldownRecord.Support != null)
            {
                EnsureHabitGrace(cooldownRecord);
                RebirthTraitSupportRuntimeState cooldownState;
                if (cooldownRecord.Support.Entries.TryGetValue(profile.Id, out cooldownState) && cooldownState != null && cooldownState.CooldownRemainingActiveSeconds > 0.001f)
                {
                    bool canRefreshHabitManagement = profile.ManagedSeconds > 0f && OwnsTrait(cooldownRecord, profile.HabitTraitId) && cooldownState.ManagedRemainingActiveSeconds + 0.001f < profile.ManagedSeconds;
                    if (!canRefreshHabitManagement)
                    {
                        message = "Support is still on cooldown (" + Math.Ceiling(cooldownState.CooldownRemainingActiveSeconds).ToString("0") + "s).";
                        return false;
                    }
                }
            }
        }

        ItemStack originalStack = stack.Clone();
        ItemValue usedValue = stack.itemValue.Clone();
        float actualCharge = Mathf.Max(0f, EffectManager.GetValue(
            PassiveEffects.DegradationPerUse, stack.itemValue, 1f, player, tags: stack.itemValue.ItemClass.ItemTags));
        if (usedValue.MaxUseTimes > 0 && actualCharge > 0f && usedValue.UseTimes + actualCharge < usedValue.MaxUseTimes)
            stack.itemValue.UseTimes += actualCharge;
        else
        {
            stack.count--;
            if (stack.count <= 0) stack = ItemStack.Empty.Clone();
        }

        // Authoritative debit precedes native/support effects. Support-only items roll back
        // their debit if their Survivor state cannot be applied; ordinary medicines retain
        // their native purpose even when no support state is applicable.
        if (inBackpack) player.bag.SetSlot(slot, stack); else player.inventory.SetItem(slot, stack);
        bool supportApplied = TryApplyFromItem(player, itemId, "trait-support-server-consume");
        if (!supportApplied && string.Equals(profile.Kind, "consumable", StringComparison.OrdinalIgnoreCase))
        {
            if (inBackpack) player.bag.SetSlot(slot, originalStack); else player.inventory.SetItem(slot, originalStack);
            message = "Support state could not be applied; item was not consumed.";
            return false;
        }

        player.MinEventContext.ItemValue = usedValue;
        QuestEventManager.Current.UsedItem(usedValue);
        player.FireEvent(MinEventTypes.onSelfPrimaryActionEnd);
        message = "Support item used.";
        return true;
    }

    public static bool TryApplyFromItem(EntityPlayer player, ItemValue itemValue, string reason)
    {
        if (itemValue == null || itemValue.ItemClass == null) return false;
        return TryApplyFromItem(player, itemValue.ItemClass.GetItemName(), reason);
    }

    public static bool TryApplyFromItem(EntityPlayer player, string itemId, string reason)
    {
        // This infusion is applied by actual swallowed volume, not a full benefit per sip/event.
        if(itemId=="rebirthCookingFoodN38")return false;
        if (player == null || string.IsNullOrEmpty(itemId) || !RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        if (RebirthCharacterCreationHoldService.IsHeld(player)) return false;
        RebirthTraitSupportProfileDefinition profile;
        if (!RebirthSurvivorDefinitionRegistry.TryGetSupportByItem(itemId, out profile) || profile == null) return false;
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete || record.Support == null) return false;
        EnsureHabitGrace(record);
        RebirthTraitSupportRuntimeState state = GetOrCreate(record, profile.Id);
        bool changed = false;
        bool cooldownReady = state.CooldownRemainingActiveSeconds <= 0.001f;

        if (profile.ManagedSeconds > 0f && OwnsTrait(record, profile.HabitTraitId))
        {
            float next = Math.Max(state.ManagedRemainingActiveSeconds, profile.ManagedSeconds);
            if (Math.Abs(next - state.ManagedRemainingActiveSeconds) > 0.001f) { state.ManagedRemainingActiveSeconds = next; changed = true; }
            if (state.GraceRemainingActiveSeconds > 0f) { state.GraceRemainingActiveSeconds = 0f; changed = true; }
        }
        if (cooldownReady && profile.PositiveSeconds > 0f)
        {
            state.PositiveRemainingActiveSeconds = Math.Max(state.PositiveRemainingActiveSeconds, profile.PositiveSeconds);
            if (string.Equals(profile.RepeatMode, "next_meal", StringComparison.OrdinalIgnoreCase)) state.Stacks = 1;
            else state.Stacks = Math.Max(1, state.Stacks);
            changed = true;
        }
        if (cooldownReady && profile.CooldownSeconds > 0f)
        {
            state.CooldownRemainingActiveSeconds = profile.CooldownSeconds;
            changed = true;
        }

        if (!changed) return true; // valid use during a nonstacking cooldown: item keeps its ordinary purpose.
        RebirthWorldCharacterService.MarkDirty(record, string.IsNullOrEmpty(reason) ? "trait-support-item" : reason);
        RebirthSurvivorNetworkService.SendOwnerState(player, Math.Max(0L, record.Revision - 1L), false, string.IsNullOrEmpty(reason) ? "trait-support-item" : reason);
        return true;
    }

    /// <summary>Applies active timed/equipment support to the already Trait-resolved condition snapshot.</summary>
    public static float ApplyConditionEffects(EntityPlayer player, RebirthMetabolismState metabolism, RebirthWorldCharacterRecord record, RebirthConditionResolvedSnapshot result)
    {
        if (record == null || record.Origin == null || record.Support == null || result == null || RebirthSurvivorDefinitionRegistry.Bundle == null) return 0f;
        float mood = 0f;
        foreach (RebirthTraitSupportProfileDefinition profile in RebirthSurvivorDefinitionRegistry.Bundle.SupportProfiles)
        {
            RebirthTraitSupportRuntimeState state = null;
            record.Support.Entries.TryGetValue(profile.Id, out state);
            bool equipped = IsEquipmentActive(player, record, profile);
            for (int i = 0; i < profile.Effects.Count; i++)
            {
                RebirthTraitSupportEffectDefinition effect = profile.Effects[i];
                if (effect == null || !IsEffectStateActive(profile, state, equipped, record, effect.State) || !ScopeApplies(record, profile, effect.Scope)) continue;
                string target = (effect.Target ?? string.Empty).Trim().ToLowerInvariant();
                if (target == "mood.target") mood += effect.Value;
                else if (target == "energy.use" || (target == "energy.use.strenuous" && metabolism != null && metabolism.SmoothedActivity >= 1.5f) || (target == "energy.use.sprint_jump" && metabolism != null && metabolism.SmoothedActivity >= 2.5f) || (target == "energy.use.movement" && metabolism != null && metabolism.SmoothedActivity >= 0.5f)) Apply(ref result.EnergyUseMultiplier, effect.Operation, effect.Value);
                else if (target == "energy.recovery") Apply(ref result.EnergyRecoveryMultiplier, effect.Operation, effect.Value);
                else if (target == "hydration.demand.total") Apply(ref result.HydrationDemandMultiplier, effect.Operation, effect.Value);
                else if (target == "environment.heat") Apply(ref result.HeatPenaltyMultiplier, effect.Operation, effect.Value);
                else if (target == "environment.cold") Apply(ref result.ColdPenaltyMultiplier, effect.Operation, effect.Value);
                else if (target == "digestion.fluid_absorption") Apply(ref result.FluidAbsorptionMultiplier, effect.Operation, effect.Value);
                else if (target == "digestion.fluid_utilization") Apply(ref result.FluidUtilizationMultiplier, effect.Operation, effect.Value);
                else if (target == "digestion.gut_resilience") Apply(ref result.GutResilienceMultiplier, effect.Operation, effect.Value);
                else if (target == "digestion.recovery") Apply(ref result.DigestiveRecoveryMultiplier, effect.Operation, effect.Value);
                else if (target == "health.capacity.illness_loss") Apply(ref result.HealthCapacityIllnessLossMultiplier, effect.Operation, effect.Value);
                else if (target == "mood.injury") Apply(ref result.InjuryMoodMultiplier, effect.Operation, effect.Value);
            }
        }
        Clamp(result);
        return Mathf.Clamp(mood, -20f, 20f);
    }

    public static float GetSkillGainMultiplier(RebirthWorldCharacterRecord record, float currentSkillValue)
    {
        if (record == null || record.Support == null || currentSkillValue >= FocusSkillLevelCeiling || RebirthSurvivorDefinitionRegistry.Bundle == null) return 1f;
        float multiplier = 1f;
        foreach (RebirthTraitSupportProfileDefinition profile in RebirthSurvivorDefinitionRegistry.Bundle.SupportProfiles)
        {
            RebirthTraitSupportRuntimeState state;
            if (!record.Support.Entries.TryGetValue(profile.Id, out state) || state == null || state.PositiveRemainingActiveSeconds <= 0f) continue;
            for (int i = 0; i < profile.Effects.Count; i++)
            {
                RebirthTraitSupportEffectDefinition effect = profile.Effects[i];
                if (effect == null || !string.Equals(effect.Target, "skill.gain", StringComparison.OrdinalIgnoreCase) || !string.Equals(effect.State, "positive", StringComparison.OrdinalIgnoreCase) || !ScopeApplies(record, profile, effect.Scope)) continue;
                if (string.Equals(effect.Operation, "multiply", StringComparison.OrdinalIgnoreCase)) multiplier *= effect.Value;
            }
        }
        return Mathf.Clamp(multiplier, 1f, FocusGlobalMultiplierCap);
    }

    /// <summary>Consumes one armed seasoning state only when a real meaningful meal is being recorded.</summary>
    public static void ApplyNextMealSupport(RebirthWorldCharacterRecord record, RebirthFoodMoodDefinition food, ref float moodInfluence, ref float repetitionMultiplier)
    {
        if (record == null || record.Support == null || food == null || RebirthSurvivorDefinitionRegistry.Bundle == null) return;
        foreach (RebirthTraitSupportProfileDefinition profile in RebirthSurvivorDefinitionRegistry.Bundle.SupportProfiles)
        {
            if (!string.Equals(profile.RepeatMode, "next_meal", StringComparison.OrdinalIgnoreCase)) continue;
            RebirthTraitSupportRuntimeState state;
            if (!record.Support.Entries.TryGetValue(profile.Id, out state) || state == null || state.Stacks <= 0 || state.PositiveRemainingActiveSeconds <= 0f) continue;
            for (int i = 0; i < profile.Effects.Count; i++)
            {
                RebirthTraitSupportEffectDefinition effect = profile.Effects[i];
                if (effect == null || !string.Equals(effect.State, "armed", StringComparison.OrdinalIgnoreCase) || !ScopeApplies(record, profile, effect.Scope)) continue;
                if (string.Equals(effect.Target, "food.mood.next", StringComparison.OrdinalIgnoreCase)) Apply(ref moodInfluence, effect.Operation, effect.Value);
                else if (string.Equals(effect.Target, "food.repetition.next", StringComparison.OrdinalIgnoreCase)) Apply(ref repetitionMultiplier, effect.Operation, effect.Value);
            }
            state.Stacks = 0;
            state.PositiveRemainingActiveSeconds = 0f;
            repetitionMultiplier = Mathf.Clamp01(repetitionMultiplier);
            moodInfluence = Mathf.Clamp(moodInfluence, -20f, 20f);
        }
    }

    public static string BuildDebugSummary(RebirthWorldCharacterRecord record)
    {
        if (record == null || record.Support == null) return "support=<unavailable>";
        List<string> rows = new List<string>();
        foreach (KeyValuePair<string, RebirthTraitSupportRuntimeState> pair in record.Support.Entries)
        {
            RebirthTraitSupportRuntimeState s = pair.Value; if (s == null) continue;
            rows.Add(pair.Key+":grace="+s.GraceRemainingActiveSeconds.ToString("0")+",managed="+s.ManagedRemainingActiveSeconds.ToString("0")+",positive="+s.PositiveRemainingActiveSeconds.ToString("0")+",cooldown="+s.CooldownRemainingActiveSeconds.ToString("0")+",stacks="+s.Stacks);
        }
        rows.Sort(StringComparer.OrdinalIgnoreCase);
        List<string> gear = new List<string>(); foreach (KeyValuePair<string,string> pair in record.Support.EquippedGearBySlot) gear.Add(pair.Key+"="+(pair.Value??string.Empty)); gear.Sort(StringComparer.OrdinalIgnoreCase);
        string supportText = rows.Count == 0 ? "support=<none>" : "support="+string.Join(";", rows.ToArray());
        return supportText + " gear=" + (gear.Count == 0 ? "<none>" : string.Join(";", gear.ToArray()));
    }

    private static bool EnsureHabitGrace(RebirthWorldCharacterRecord record)
    {
        if (record == null || record.Origin == null || record.Support == null || RebirthSurvivorDefinitionRegistry.Bundle == null) return false;
        bool changed = false;
        foreach (RebirthTraitSupportProfileDefinition profile in RebirthSurvivorDefinitionRegistry.Bundle.SupportProfiles)
        {
            if (string.IsNullOrEmpty(profile.HabitTraitId) || !OwnsTrait(record, profile.HabitTraitId) || record.Support.Entries.ContainsKey(profile.Id)) continue;
            record.Support.Entries[profile.Id] = new RebirthTraitSupportRuntimeState { SupportProfileId = profile.Id, GraceRemainingActiveSeconds = profile.GraceSeconds };
            changed = true;
        }
        return changed;
    }

    private static RebirthTraitSupportRuntimeState GetOrCreate(RebirthWorldCharacterRecord record, string profileId)
    {
        RebirthTraitSupportRuntimeState state;
        if (!record.Support.Entries.TryGetValue(profileId, out state) || state == null)
        {
            state = new RebirthTraitSupportRuntimeState { SupportProfileId = profileId };
            record.Support.Entries[profileId] = state;
        }
        return state;
    }

    private static bool OwnsTrait(RebirthWorldCharacterRecord record, string traitId)
    {
        if (record == null || record.Origin == null || string.IsNullOrEmpty(traitId)) return false;
        for (int i = 0; i < record.Origin.TraitIds.Count; i++) if (string.Equals(record.Origin.TraitIds[i], traitId, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool OwnsAnySupportedTrait(RebirthWorldCharacterRecord record, RebirthTraitSupportProfileDefinition profile)
    {
        if (record == null || profile == null) return false;
        for (int i = 0; i < profile.SupportedTraitIds.Count; i++) if (OwnsTrait(record, profile.SupportedTraitIds[i])) return true;
        return false;
    }

    private static bool ScopeApplies(RebirthWorldCharacterRecord record, RebirthTraitSupportProfileDefinition profile, string scope)
    {
        scope = (scope ?? string.Empty).Trim();
        if (scope.Length == 0 || string.Equals(scope, "universal", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(scope, "supported_trait", StringComparison.OrdinalIgnoreCase)) return OwnsAnySupportedTrait(record, profile);
        if (string.Equals(scope, "habit_trait", StringComparison.OrdinalIgnoreCase)) return OwnsTrait(record, profile.HabitTraitId);
        if (string.Equals(scope, "non_habit_trait", StringComparison.OrdinalIgnoreCase)) return !OwnsTrait(record, profile.HabitTraitId);
        const string prefix = "trait:";
        return scope.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && OwnsTrait(record, scope.Substring(prefix.Length));
    }

    private static bool IsEffectStateActive(RebirthTraitSupportProfileDefinition profile, RebirthTraitSupportRuntimeState state, bool equipped, RebirthWorldCharacterRecord record, string requiredState)
    {
        requiredState = (requiredState ?? string.Empty).Trim().ToLowerInvariant();
        if (requiredState == "equipped") return equipped;
        if (requiredState == "unsatisfied") return OwnsTrait(record, profile.HabitTraitId) && state != null && state.GraceRemainingActiveSeconds <= 0f && state.ManagedRemainingActiveSeconds <= 0f;
        if (state == null) return false;
        if (requiredState == "managed") return state.ManagedRemainingActiveSeconds > 0f;
        if (requiredState == "positive") return state.PositiveRemainingActiveSeconds > 0f;
        if (requiredState == "armed") return state.PositiveRemainingActiveSeconds > 0f && state.Stacks > 0;
        return false;
    }

    private static bool IsEquipmentActive(EntityPlayer player, RebirthWorldCharacterRecord record, RebirthTraitSupportProfileDefinition profile)
    {
        if (profile == null) return false;
        if (!string.IsNullOrEmpty(profile.GearSlotId) && !string.IsNullOrEmpty(profile.GearItemId) && record != null && record.Support != null)
        {
            string equippedItem;
            if (record.Support.EquippedGearBySlot.TryGetValue(profile.GearSlotId, out equippedItem) && string.Equals(equippedItem, profile.GearItemId, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return player != null && player.Buffs != null && !string.IsNullOrEmpty(profile.EquipmentCVar) && player.Buffs.GetCustomVar(profile.EquipmentCVar) > 0f;
    }

    private static bool TryFindMatchingInventoryStack(EntityPlayer player, int itemType, ushort seed, out bool inBackpack, out int slot, out ItemStack stack)
    {
        inBackpack = false; slot = -1; stack = null;
        if (player == null || player.inventory == null || player.bag == null) return false;
        ItemStack[] tool = player.inventory.ItemGrid.items;
        int toolLimit = RebirthToolbeltCapacity.GetOwnedSlotCount(player, tool.Length);
        for (int i = 0; i < toolLimit; i++)
        {
            ItemStack value = tool[i];
            if (value == null || value.IsEmpty() || value.itemValue == null || value.itemValue.type != itemType || value.itemValue.Seed != seed) continue;
            slot = i; stack = value; return true;
        }
        ItemStack[] bag = player.bag.ItemGrid.items;
        for (int i = 0; i < bag.Length; i++)
        {
            ItemStack value = bag[i];
            if (value == null || value.IsEmpty() || value.itemValue == null || value.itemValue.type != itemType || value.itemValue.Seed != seed) continue;
            inBackpack = true; slot = i; stack = value; return true;
        }
        return false;
    }

    private static bool Decrement(ref float value, float elapsed)
    {
        if (value <= 0f) return false;
        float before = value; value = Math.Max(0f, value - elapsed);
        return before > 0f && value <= 0f;
    }

    private static void Apply(ref float current, string operation, float value)
    {
        if (string.Equals(operation, "multiply", StringComparison.OrdinalIgnoreCase)) current *= value;
        else if (string.Equals(operation, "add", StringComparison.OrdinalIgnoreCase)) current += value;
        else if (string.Equals(operation, "relieve", StringComparison.OrdinalIgnoreCase) && current > 1f) current = 1f + (current - 1f) * Mathf.Clamp01(1f - value);
    }

    private static void Clamp(RebirthConditionResolvedSnapshot r)
    {
        r.EnergyUseMultiplier=Mathf.Clamp(r.EnergyUseMultiplier,0.65f,1.50f); r.EnergyRecoveryMultiplier=Mathf.Clamp(r.EnergyRecoveryMultiplier,0.60f,1.50f);
        r.HydrationDemandMultiplier=Mathf.Clamp(r.HydrationDemandMultiplier,0.65f,1.50f); r.HeatPenaltyMultiplier=Mathf.Clamp(r.HeatPenaltyMultiplier,0.50f,1.50f); r.ColdPenaltyMultiplier=Mathf.Clamp(r.ColdPenaltyMultiplier,0.50f,1.50f);
        r.FluidAbsorptionMultiplier=Mathf.Clamp(r.FluidAbsorptionMultiplier,0.60f,1.50f); r.FluidUtilizationMultiplier=Mathf.Clamp(r.FluidUtilizationMultiplier,0.60f,1.50f); r.GutResilienceMultiplier=Mathf.Clamp(r.GutResilienceMultiplier,0.60f,1.50f);
        r.DigestiveRecoveryMultiplier=Mathf.Clamp(r.DigestiveRecoveryMultiplier,0.60f,1.50f); r.HealthCapacityIllnessLossMultiplier=Mathf.Clamp(r.HealthCapacityIllnessLossMultiplier,0.50f,1.60f); r.InjuryMoodMultiplier=Mathf.Clamp(r.InjuryMoodMultiplier,0.50f,1.60f);
    }
}
