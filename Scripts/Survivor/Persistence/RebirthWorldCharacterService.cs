using System;

#nullable disable

/// <summary>
/// Logical access layer for the authoritative Survivor character. It deliberately composes,
/// rather than duplicates, the existing metabolism repository for Energy/DigestiveHealth/gut state.
/// </summary>
public static class RebirthWorldCharacterService
{
    public static bool HasCharacter(ClientInfo clientInfo)
    {
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return false;
        RebirthStablePlayerIdentity identity;
        return RebirthStablePlayerIdentity.TryFromClientInfo(clientInfo, out identity) &&
            RebirthWorldCharacterRepository.Exists(identity);
    }

    public static bool TryGet(ClientInfo clientInfo, out RebirthWorldCharacterRecord record)
    {
        record = null;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return false;
        RebirthStablePlayerIdentity identity;
        return RebirthStablePlayerIdentity.TryFromClientInfo(clientInfo, out identity) &&
            RebirthWorldCharacterRepository.TryGet(identity, out record);
    }

    public static bool TryGet(EntityPlayer player, out RebirthWorldCharacterRecord record)
    {
        record = null;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return false;
        RebirthStablePlayerIdentity identity;
        return RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) &&
            RebirthWorldCharacterRepository.TryGet(identity, out record);
    }

    public static bool TryGetIdentity(EntityPlayer player, out RebirthStablePlayerIdentity identity)
    {
        identity = null;
        return player != null && RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity);
    }

    public static bool TryGetMetabolism(EntityPlayer player, out RebirthMetabolismState metabolism)
    {
        metabolism = null;
        if (player == null)
            return false;
        return RebirthMetabolismStateRepository.TryGet(player, out metabolism) && metabolism != null;
    }

    public static void MarkDirty(RebirthWorldCharacterRecord record, string reason)
    {
        if (record == null || !RebirthWorldCharacterRepository.IsServerAuthority)
            return;
        RebirthWorldCharacterRepository.MarkDirty(record, reason);
    }

    public static bool FlushPlayer(EntityPlayer player, string reason)
    {
        RebirthStablePlayerIdentity identity;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity))
            return false;
        bool saved = RebirthWorldCharacterRepository.SaveIfDirty(identity, reason);
        RebirthMetabolismStateRepository.SaveIfDirty(reason);
        return saved;
    }
}
