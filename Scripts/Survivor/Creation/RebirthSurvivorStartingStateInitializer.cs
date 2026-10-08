using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Pure authoritative construction of the initial mutable world-character state from the
/// already-resolved creation result. It performs no inventory, network, disk, or native-stat work.
/// Keeping this pure makes creation replay/idempotence straightforward and prevents duplicated
/// starting grants on reconnect.
/// </summary>
public static class RebirthSurvivorStartingStateInitializer
{
    public const float InitialMood = 50f;
    public const float InitialDietSatisfaction = 50f;
    private const float FloatTolerance = 0.0001f;

    public static RebirthWorldProgressionState BuildProgression(RebirthSurvivorCreationResult result)
    {
        if (result == null || !result.IsValid)
            return null;

        RebirthWorldProgressionState state = new RebirthWorldProgressionState
        {
            HealthPotential = result.HealthPotential
        };

        for (int i = 0; i < result.Attributes.Count; i++)
        {
            RebirthResolvedAttributeStart a = result.Attributes[i];
            if (a == null || string.IsNullOrEmpty(a.AttributeId))
                continue;
            state.Attributes[a.AttributeId] = new RebirthAttributeRuntimeState
            {
                AttributeId = a.AttributeId,
                Current = a.Current,
                Potential = a.Potential
            };
        }

        foreach (KeyValuePair<string, float> pair in result.StartingSkills)
        {
            if (string.IsNullOrEmpty(pair.Key))
                continue;
            state.Skills[pair.Key] = new RebirthSkillRuntimeState
            {
                SkillId = pair.Key,
                Value = pair.Value,
                Progress = 0f
            };
        }

        foreach (KeyValuePair<string, float> pair in result.StartingSkillKnowledge)
        {
            if (string.IsNullOrEmpty(pair.Key))
                continue;
            state.SkillKnowledge[pair.Key] = new RebirthSkillKnowledgeRuntimeState
            {
                SkillId = pair.Key,
                Value = pair.Value
            };
        }

        for (int i = 0; i < result.StartingKnowledgeIds.Count; i++)
        {
            string id = result.StartingKnowledgeIds[i];
            if (!string.IsNullOrEmpty(id))
                state.KnowledgeIds.Add(id);
        }
        return state;
    }

    public static RebirthWorldConditionState BuildCondition(EntityPlayer player, RebirthSurvivorCreationResult result)
    {
        if (result == null || !result.IsValid)
            return null;

        return new RebirthWorldConditionState
        {
            MoodCurrent = InitialMood,
            MoodTarget = InitialMood,
            DietSatisfaction = InitialDietSatisfaction,
            HealthCapacity = result.HealthPotential,
            ActivePlaySeconds = 0d,
            // This is a resume/diagnostic anchor only. Condition timers are active-play based;
            // setting current world time prevents any later implementation from interpreting
            // creation-to-first-tick as offline catch-up.
            LastActiveWorldTime = player != null && player.world != null
                ? player.world.GetWorldTime()
                : 0UL
        };
    }

    public static RebirthWorldSupportState BuildSupport(RebirthSurvivorCreationResult result)
    {
        if (result == null || !result.IsValid)
            return null;

        // Support state is intentionally empty at creation. Chunk 11's generic support service
        // derives grace/managed requirements from immutable origin Traits and initializes them
        // once. This prevents this low-level creation layer from duplicating support policy.
        return new RebirthWorldSupportState();
    }

    public static RebirthWorldCharacterRecord BuildRecord(
        RebirthStablePlayerIdentity identity,
        RebirthWorldOriginSnapshot origin,
        EntityPlayer player,
        RebirthSurvivorCreationResult result)
    {
        if (identity == null || origin == null || result == null || !result.IsValid)
            return null;

        RebirthWorldProgressionState progression = BuildProgression(result);
        RebirthWorldConditionState condition = BuildCondition(player, result);
        RebirthWorldSupportState support = BuildSupport(result);
        if (progression == null || condition == null || support == null)
            return null;

        DateTime now = origin.CommittedAtUtc;
        return new RebirthWorldCharacterRecord(
            RebirthWorldCharacterRecord.CurrentSchemaVersion,
            identity.CanonicalId,
            identity.StorageKey,
            1L,
            now,
            now,
            origin,
            progression,
            condition,
            support);
    }

    /// <summary>
    /// Independent invariant barrier between the canonical validator and persistence. A programming
    /// error in initialization must fail before either metabolism or world-character storage is
    /// touched. This deliberately validates resolved server output, never client-provided numbers.
    /// </summary>
    public static bool ValidateCandidateAgainstResult(
        RebirthWorldCharacterRecord candidate,
        RebirthStablePlayerIdentity identity,
        RebirthSurvivorCreationResult result,
        out string error)
    {
        error = string.Empty;
        if (candidate == null || identity == null || result == null || !result.IsValid)
            return Fail("candidate-validation-context-invalid", out error);
        if (candidate.SchemaVersion != RebirthWorldCharacterRecord.CurrentSchemaVersion)
            return Fail("candidate-schema-not-current", out error);
        if (!candidate.IsComplete || candidate.Origin == null || candidate.Progression == null || candidate.Condition == null || candidate.Support == null)
            return Fail("candidate-sections-incomplete", out error);
        if (!string.Equals(candidate.StablePlayerId, identity.CanonicalId, StringComparison.Ordinal)
            || !string.Equals(candidate.StablePlayerKey, identity.StorageKey, StringComparison.Ordinal))
            return Fail("candidate-identity-mismatch", out error);

        RebirthWorldOriginSnapshot origin = candidate.Origin;
        if (!string.Equals(origin.DefinitionHash, result.DefinitionHash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(origin.DefinitionVersion, result.DefinitionVersion, StringComparison.Ordinal)
            || !string.Equals(origin.BackgroundId, result.BackgroundId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(origin.DietId, result.DietId, StringComparison.OrdinalIgnoreCase)
            || origin.RemainingCreationPoints != result.RemainingCreationPoints
            || origin.UnencumberedSlotDelta != result.UnencumberedSlotDelta
            || !Same(origin.HealthPotential, result.HealthPotential))
            return Fail("candidate-origin-scalar-mismatch", out error);

        if (origin.TraitIds.Count != result.TraitIds.Count)
            return Fail("candidate-origin-trait-count-mismatch", out error);
        for (int i = 0; i < result.TraitIds.Count; i++)
            if (!string.Equals(origin.TraitIds[i], result.TraitIds[i], StringComparison.OrdinalIgnoreCase))
                return Fail("candidate-origin-trait-mismatch", out error);

        RebirthSurvivorDefinitionBundle bundle = RebirthSurvivorDefinitionRegistry.Bundle;
        if (bundle == null || bundle.Progression == null)
            return Fail("candidate-definition-bundle-unavailable", out error);
        if (origin.Attributes.Count != result.Attributes.Count
            || candidate.Progression.Attributes.Count != result.Attributes.Count
            || result.Attributes.Count != bundle.Progression.Attributes.Count)
            return Fail("candidate-attribute-count-mismatch", out error);

        for (int i = 0; i < result.Attributes.Count; i++)
        {
            RebirthResolvedAttributeStart resolved = result.Attributes[i];
            if (resolved == null || string.IsNullOrEmpty(resolved.AttributeId) || resolved.Current > resolved.Potential)
                return Fail("candidate-attribute-invalid", out error);

            RebirthAttributeDefinition definition = FindAttributeDefinition(bundle.Progression, resolved.AttributeId);
            if (definition == null || resolved.Current < definition.Min || resolved.Current > definition.Max
                || resolved.Potential < definition.Min || resolved.Potential > definition.Max)
                return Fail("candidate-attribute-out-of-definition-range", out error);

            RebirthWorldOriginAttribute originAttribute = FindOriginAttribute(origin, resolved.AttributeId);
            RebirthAttributeRuntimeState runtimeAttribute;
            if (originAttribute == null
                || !candidate.Progression.Attributes.TryGetValue(resolved.AttributeId, out runtimeAttribute)
                || runtimeAttribute == null
                || !Same(originAttribute.Current, resolved.Current)
                || !Same(originAttribute.Potential, resolved.Potential)
                || !Same(runtimeAttribute.Current, resolved.Current)
                || !Same(runtimeAttribute.Potential, resolved.Potential))
                return Fail("candidate-attribute-value-mismatch", out error);
        }

        if (!Same(candidate.Progression.HealthPotential, result.HealthPotential)
            || !Same(candidate.Condition.HealthCapacity, result.HealthPotential))
            return Fail("candidate-health-potential-mismatch", out error);
        if (!Same(candidate.Condition.MoodCurrent, InitialMood)
            || !Same(candidate.Condition.MoodTarget, InitialMood)
            || !Same(candidate.Condition.DietSatisfaction, InitialDietSatisfaction)
            || candidate.Condition.ActivePlaySeconds != 0d
            || candidate.Condition.RecentMeals.Count != 0)
            return Fail("candidate-condition-default-mismatch", out error);

        if (origin.StartingSkills.Count != result.StartingSkills.Count
            || candidate.Progression.Skills.Count != result.StartingSkills.Count)
            return Fail("candidate-skill-count-mismatch", out error);
        foreach (KeyValuePair<string, float> pair in result.StartingSkills)
        {
            RebirthSkillDefinition definition;
            RebirthSkillRuntimeState runtime;
            float originValue;
            if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(pair.Key, out definition)
                || definition == null
                || pair.Value < definition.Min || pair.Value > definition.Max
                || !origin.StartingSkills.TryGetValue(pair.Key, out originValue)
                || !candidate.Progression.Skills.TryGetValue(pair.Key, out runtime)
                || runtime == null
                || !Same(originValue, pair.Value)
                || !Same(runtime.Value, pair.Value)
                || !Same(runtime.Progress, 0f))
                return Fail("candidate-skill-mismatch", out error);
        }

        if (origin.StartingSkillKnowledge.Count != result.StartingSkillKnowledge.Count
            || candidate.Progression.SkillKnowledge.Count != result.StartingSkillKnowledge.Count)
            return Fail("candidate-skill-knowledge-count-mismatch", out error);
        foreach (KeyValuePair<string, float> pair in result.StartingSkillKnowledge)
        {
            RebirthSkillDefinition skillDefinition;
            RebirthSkillKnowledgeRuntimeState runtimeKnowledge;
            float originKnowledge;
            if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(pair.Key, out skillDefinition)
                || skillDefinition == null
                || pair.Value < bundle.Progression.SkillKnowledgeMin
                || pair.Value > bundle.Progression.SkillKnowledgeMax
                || !origin.StartingSkillKnowledge.TryGetValue(pair.Key, out originKnowledge)
                || !candidate.Progression.SkillKnowledge.TryGetValue(pair.Key, out runtimeKnowledge)
                || runtimeKnowledge == null
                || !Same(originKnowledge, pair.Value)
                || !Same(runtimeKnowledge.Value, pair.Value))
                return Fail("candidate-skill-knowledge-mismatch", out error);
        }

        if (origin.StartingKnowledgeIds.Count != result.StartingKnowledgeIds.Count
            || candidate.Progression.KnowledgeIds.Count != result.StartingKnowledgeIds.Count)
            return Fail("candidate-knowledge-count-mismatch", out error);
        for (int i = 0; i < result.StartingKnowledgeIds.Count; i++)
        {
            string id = result.StartingKnowledgeIds[i];
            RebirthKnowledgeDefinition definition;
            if (string.IsNullOrEmpty(id)
                || !RebirthSurvivorDefinitionRegistry.TryGetKnowledge(id, out definition)
                || definition == null
                || !candidate.Progression.KnowledgeIds.Contains(id)
                || !ContainsIgnoreCase(origin.StartingKnowledgeIds, id))
                return Fail("candidate-knowledge-mismatch", out error);
        }

        if (candidate.Support.Entries.Count != 0)
            return Fail("candidate-support-not-empty-at-origin", out error);
        return true;
    }

    private static RebirthAttributeDefinition FindAttributeDefinition(RebirthProgressionDefinition progression, string id)
    {
        if (progression == null) return null;
        for (int i = 0; i < progression.Attributes.Count; i++)
        {
            RebirthAttributeDefinition value = progression.Attributes[i];
            if (value != null && string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        return null;
    }

    private static RebirthWorldOriginAttribute FindOriginAttribute(RebirthWorldOriginSnapshot origin, string id)
    {
        if (origin == null) return null;
        for (int i = 0; i < origin.Attributes.Count; i++)
        {
            RebirthWorldOriginAttribute value = origin.Attributes[i];
            if (value != null && string.Equals(value.AttributeId, id, StringComparison.OrdinalIgnoreCase))
                return value;
        }
        return null;
    }

    private static bool ContainsIgnoreCase(IList<string> values, string wanted)
    {
        if (values == null) return false;
        for (int i = 0; i < values.Count; i++)
            if (string.Equals(values[i], wanted, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool Same(float a, float b)
    {
        return Math.Abs(a - b) <= FloatTolerance;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
