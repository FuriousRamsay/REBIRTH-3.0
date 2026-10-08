using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Private server-side last-send evidence. Does not change the wire format or trust a client
/// revision on its own. Only a previously sent complete character with identical authority,
/// definition and auxiliary inputs may avoid construction of the large owner payload.
/// Small copies are made on actual sends; unchanged probes allocate no projection collections.
/// </summary>
internal sealed class RebirthSurvivorOwnerSendHeader
{
    private readonly object world, definitions, resolvedCondition;
    private readonly string definitionHash, definitionVersion;
    private readonly RebirthWorldCharacterRecord owner;
    private readonly object origin;
    private readonly long revision;
    private readonly RebirthWorldConditionState condition;
    private readonly RebirthWorldSupportState support;
    private readonly int physicalSlots, mealHistorySize;
    private readonly bool comparable;

    internal RebirthSurvivorOwnerSendHeader(RebirthWorldCharacterRecord record)
    {
        owner = record; origin = record != null ? record.Origin : null;
        revision = record != null ? record.Revision : 0L;
        world = GameManager.Instance != null ? GameManager.Instance.World : null;
        definitions = RebirthSurvivorDefinitionRegistry.Bundle;
        definitionHash = RebirthSurvivorDefinitionRegistry.SemanticHash;
        definitionVersion = RebirthSurvivorDefinitionRegistry.DefinitionVersion;
        if (record == null || !record.IsComplete) return;
        if (record.Condition != null && record.Condition.RecentMeals.Count > 128) return;
        if (record.Support != null && (record.Support.Entries.Count > 256 || record.Support.EquippedGearBySlot.Count > 64)) return;
        physicalSlots = RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(record);
        mealHistorySize = RebirthConditionRuntimeConfig.MealHistorySize;
        resolvedCondition = RebirthSurvivorConditionService.GetResolvedProjectionToken(record);
        if (record.Condition != null)
        {
            // No resolved cache means BuildStatus will resolve live dependencies. Do not guess
            // their equivalence; take the conservative body-build/send path until a token exists.
            if (resolvedCondition == null) return;
            condition = new RebirthWorldConditionState
            {
                MoodCurrent = record.Condition.MoodCurrent, MoodTarget = record.Condition.MoodTarget,
                DietSatisfaction = record.Condition.DietSatisfaction, HealthCapacity = record.Condition.HealthCapacity,
                ActivePlaySeconds = record.Condition.ActivePlaySeconds
            };
            foreach (RebirthRecentMealState meal in record.Condition.RecentMeals)
                condition.RecentMeals.Add(meal != null ? meal.Clone() : null);
        }
        support = record.Support != null ? record.Support.Clone() : null;
        comparable = true;
    }

    internal bool Matches(RebirthWorldCharacterRecord record, long knownRevision)
    {
        if (!comparable || record == null || !record.IsComplete || !ReferenceEquals(owner, record)
            || !ReferenceEquals(origin, record.Origin) || revision <= 0L || revision != knownRevision || revision != record.Revision
            || !ReferenceEquals(world, GameManager.Instance != null ? GameManager.Instance.World : null)
            || !ReferenceEquals(definitions, RebirthSurvivorDefinitionRegistry.Bundle)
            || !string.Equals(definitionHash, RebirthSurvivorDefinitionRegistry.SemanticHash, StringComparison.Ordinal)
            || !string.Equals(definitionVersion, RebirthSurvivorDefinitionRegistry.DefinitionVersion, StringComparison.Ordinal)
            || mealHistorySize != RebirthConditionRuntimeConfig.MealHistorySize
            || physicalSlots != RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(record)) return false;
        if (!ReferenceEquals(resolvedCondition, RebirthSurvivorConditionService.GetResolvedProjectionToken(record))) return false;
        RebirthWorldConditionState c = record.Condition;
        if ((condition == null) != (c == null)) return false;
        if (c != null)
        {
            if (condition.MoodCurrent != c.MoodCurrent || condition.MoodTarget != c.MoodTarget
                || condition.DietSatisfaction != c.DietSatisfaction || condition.HealthCapacity != c.HealthCapacity
                || condition.ActivePlaySeconds != c.ActivePlaySeconds || condition.RecentMeals.Count != c.RecentMeals.Count) return false;
            for (int i = 0; i < c.RecentMeals.Count; i++)
            {
                RebirthRecentMealState a = condition.RecentMeals[i], b = c.RecentMeals[i];
                if ((a == null) != (b == null)) return false;
                if (a != null && (!string.Equals(a.SourceItemId, b.SourceItemId, StringComparison.Ordinal)
                    || !string.Equals(a.VarietyFamilyId, b.VarietyFamilyId, StringComparison.Ordinal)
                    || a.MoodQuality != b.MoodQuality || a.CompatibleWithDiet != b.CompatibleWithDiet
                    || a.AgeActiveSeconds != b.AgeActiveSeconds)) return false;
            }
        }
        RebirthWorldSupportState s = record.Support;
        if ((support == null) != (s == null)) return false;
        if (s != null)
        {
            if (support.GearRevision != s.GearRevision || support.Entries.Count != s.Entries.Count || support.EquippedGearBySlot.Count != s.EquippedGearBySlot.Count) return false;
            foreach (KeyValuePair<string, RebirthTraitSupportRuntimeState> pair in support.Entries)
            {
                RebirthTraitSupportRuntimeState b;
                RebirthTraitSupportRuntimeState a = pair.Value;
                if (!s.Entries.TryGetValue(pair.Key, out b) || a == null || b == null
                    || !string.Equals(a.SupportProfileId, b.SupportProfileId, StringComparison.Ordinal)
                    || a.GraceRemainingActiveSeconds != b.GraceRemainingActiveSeconds
                    || a.ManagedRemainingActiveSeconds != b.ManagedRemainingActiveSeconds
                    || a.PositiveRemainingActiveSeconds != b.PositiveRemainingActiveSeconds
                    || a.CooldownRemainingActiveSeconds != b.CooldownRemainingActiveSeconds || a.Stacks != b.Stacks) return false;
            }
            foreach (KeyValuePair<string, string> pair in support.EquippedGearBySlot)
            {
                string value;
                if (!s.EquippedGearBySlot.TryGetValue(pair.Key, out value) || !string.Equals(pair.Value, value, StringComparison.Ordinal)) return false;
            }
        }
        return true;
    }
}
