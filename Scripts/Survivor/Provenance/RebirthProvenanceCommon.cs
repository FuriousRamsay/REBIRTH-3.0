using System;

#nullable disable

public sealed class RebirthProvenanceAuthorSnapshot
{
    public string StablePlayerId = string.Empty;
    public string BackgroundId = string.Empty;
    public string BonusId = string.Empty;

    public bool IsValid { get { return !string.IsNullOrEmpty(StablePlayerId); } }
}

/// <summary>
/// Shared server-derived identity capture for provenance records. No client-supplied identity or
/// Background name is accepted. Clean Slate and other no-bonus states are represented by an empty
/// BonusId, while BackgroundId remains the immutable Survivor origin.
/// </summary>
public static class RebirthProvenanceIdentity
{
    public static bool TryCapture(EntityPlayer player, out RebirthProvenanceAuthorSnapshot snapshot)
    {
        snapshot = null;
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        World world = player.world;
        if (world != null && world.IsRemote()) return false;

        RebirthStablePlayerIdentity identity;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity) || identity == null || string.IsNullOrEmpty(identity.CanonicalId))
            return false;

        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record == null || !record.IsComplete || record.Origin == null)
            return false;

        RebirthBackgroundBonusDefinition bonus;
        RebirthBackgroundBonusService.TryGetSignatureBonus(record, out bonus);
        snapshot = new RebirthProvenanceAuthorSnapshot
        {
            StablePlayerId = identity.CanonicalId,
            BackgroundId = record.Origin.BackgroundId ?? string.Empty,
            BonusId = bonus != null ? (bonus.Id ?? string.Empty) : string.Empty
        };
        return true;
    }
}
