using System;

#nullable disable

/// <summary>
/// One ownership rule shared by metabolism and creation: in Rebirth progression mode a player
/// with no committed world character has no live metabolism clock yet. The entity can exist for
/// the creator/hold flow, but digestion, reserve drain and fresh-state migration must wait until
/// the authoritative origin commit succeeds.
/// </summary>
public static class RebirthSurvivorMetabolismCreationGate
{
    public static bool ShouldDefer(EntityPlayer player)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return false;
        if (!RebirthWorldCharacterRepository.IsServerAuthority)
            return false;
        if (player == null)
            return true;

        RebirthStablePlayerIdentity identity;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) || identity == null)
            return true; // fail closed until authenticated server identity is available

        RebirthWorldCharacterRecord record;
        return !RebirthWorldCharacterRepository.TryGet(identity, out record) || record == null || !record.IsComplete;
    }

    public static bool IsLinkedToCommittedOrigin(EntityPlayer player, RebirthMetabolismState state)
    {
        if (player == null || state == null)
            return false;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return true;

        RebirthStablePlayerIdentity identity;
        RebirthWorldCharacterRecord record;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) ||
            !RebirthWorldCharacterRepository.TryGet(identity, out record) ||
            record == null || record.Origin == null)
            return false;

        // An empty marker is accepted only for a record that was explicitly migrated from the
        // pre-creation-generation world schema. New Chunk-05+ records always have a random
        // server creationId and therefore fail closed if their metabolism marker disappears.
        if (string.IsNullOrEmpty(state.SurvivorCreationId))
            return !string.IsNullOrEmpty(record.Origin.CreationId)
                && record.Origin.CreationId.StartsWith("legacy-", StringComparison.Ordinal);
        if (record.Origin.CreationId?.StartsWith("legacy-", StringComparison.Ordinal) == true)
            return string.Equals(state.SurvivorCreationId, record.Origin.CreationId, StringComparison.Ordinal);
        return RebirthSurvivorRequestScope.Matches(state.SurvivorCreationId, record.Origin.CreationId);
    }
}
