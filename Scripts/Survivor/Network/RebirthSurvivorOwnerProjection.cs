using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

/// <summary>Local-only invalidation key. Never serialized or accepted as authority.</summary>
public struct RebirthSurvivorOwnerHeader : IEquatable<RebirthSurvivorOwnerHeader>
{
    public readonly long Generation;
    public readonly bool Available;
    public readonly long CharacterRevision;
    public readonly long GearRevision;
    public readonly bool RebirthModeEnabled;
    public readonly bool HasCharacter;
    public readonly RebirthSurvivorOwnerCreationState CreationState;
    private readonly object world, player, definitions;
    private readonly string definitionHash;

    internal RebirthSurvivorOwnerHeader(long generation, RebirthSurvivorOwnerStateSnapshot state,
        object currentWorld, object currentPlayer)
    {
        Generation = generation; Available = state != null;
        CharacterRevision = state != null ? state.CharacterRevision : 0L;
        GearRevision = state != null ? state.GearRevision : -1L;
        RebirthModeEnabled = state != null && state.RebirthModeEnabled;
        HasCharacter = state != null && state.HasCharacter;
        CreationState = state != null ? state.CreationState : RebirthSurvivorOwnerCreationState.NotApplicable;
        world = currentWorld; player = currentPlayer;
        definitions = RebirthSurvivorDefinitionRegistry.Bundle;
        definitionHash = RebirthSurvivorDefinitionRegistry.SemanticHash;
    }
    public bool Equals(RebirthSurvivorOwnerHeader other)
    {
        return Generation == other.Generation && Available == other.Available
            && GearRevision == other.GearRevision && CharacterRevision == other.CharacterRevision && RebirthModeEnabled == other.RebirthModeEnabled
            && HasCharacter == other.HasCharacter && CreationState == other.CreationState
            && ReferenceEquals(world, other.world) && ReferenceEquals(player, other.player)
            && ReferenceEquals(definitions, other.definitions)
            && string.Equals(definitionHash, other.definitionHash, StringComparison.Ordinal);
    }
    public override bool Equals(object obj) { return obj is RebirthSurvivorOwnerHeader && Equals((RebirthSurvivorOwnerHeader)obj); }
    public override int GetHashCode() { return Generation.GetHashCode() ^ CharacterRevision.GetHashCode() ^ Available.GetHashCode(); }
}

/// <summary>
/// Constructed once from an accepted owner packet. Private dictionaries and read-only copied
/// Traits make sharing safe; no caller can mutate the authoritative or UI snapshot through it.
/// Availability/owner/world/definition checks belong to ClientState.TryGetOwnerScalars.
/// </summary>
public sealed class RebirthSurvivorOwnerScalars
{
    private readonly Dictionary<string, float> skills = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> theory = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> attributes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> knowledge = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly ReadOnlyCollection<string> TraitIds;
    public readonly string BackgroundId;
    internal RebirthSurvivorOwnerScalars(RebirthSurvivorOwnerStateSnapshot state)
    {
        BackgroundId = state.BackgroundId ?? string.Empty;
        TraitIds = new List<string>(state.TraitIds ?? new List<string>()).AsReadOnly();
        if (state.Skills != null) foreach (RebirthSurvivorOwnerSkillSnapshot item in state.Skills)
            if (item != null && !string.IsNullOrEmpty(item.Id) && !skills.ContainsKey(item.Id)) skills.Add(item.Id, item.Value);
        if (state.SkillKnowledge != null) foreach (RebirthSurvivorOwnerSkillKnowledgeSnapshot item in state.SkillKnowledge)
            if (item != null && !string.IsNullOrEmpty(item.Id) && !theory.ContainsKey(item.Id)) theory.Add(item.Id, item.Value);
        if (state.Attributes != null) foreach (RebirthSurvivorOwnerAttributeSnapshot item in state.Attributes)
            if (item != null && !string.IsNullOrEmpty(item.Id) && !attributes.ContainsKey(item.Id)) attributes.Add(item.Id, item.Current);
        if (state.KnowledgeIds != null) foreach (string id in state.KnowledgeIds)
            if (!string.IsNullOrEmpty(id)) knowledge.Add(id);
    }
    public bool TryGetSkill(string id, out float value) { value = 0f; return !string.IsNullOrEmpty(id) && skills.TryGetValue(id, out value); }
    public bool TryGetTheory(string id, out float value) { value = 0f; return !string.IsNullOrEmpty(id) && theory.TryGetValue(id, out value); }
    public bool TryGetAttribute(string id, out float value) { value = 0f; return !string.IsNullOrEmpty(id) && attributes.TryGetValue(id, out value); }
    public bool HasKnowledge(string id) { return !string.IsNullOrEmpty(id) && knowledge.Contains(id); }
}
