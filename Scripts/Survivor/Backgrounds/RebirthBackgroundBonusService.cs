using System;

#nullable disable

/// <summary>
/// Read-only Signature Bonus ownership queries. Ownership is always derived from the existing
/// selected Background; there is no second entitlement flag and no client-authored mutation path.
/// </summary>
public static class RebirthBackgroundBonusService
{
    public static RebirthBackgroundBonusDefinition GetSignatureBonus(EntityPlayer player)
    {
        RebirthBackgroundBonusDefinition result;
        return TryGetSignatureBonus(player, out result) ? result : null;
    }

    public static RebirthBackgroundBonusDefinition GetSignatureBonus(RebirthSurvivorProfile profile)
    {
        RebirthBackgroundBonusDefinition result;
        return TryGetSignatureBonus(profile, out result) ? result : null;
    }

    public static bool TryGetSignatureBonus(EntityPlayer player, out RebirthBackgroundBonusDefinition definition)
    {
        definition = null;
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld() || !RebirthBackgroundBonusRegistry.IsReady) return false;

        World world = player.world;
        if (world != null && world.IsRemote())
        {
            // A background lookup needs one immutable string, not a clone of every skill,
            // knowledge title and gear entry. The scalar projection also checks owner/world
            // identity and definition compatibility before exposing that value.
            RebirthSurvivorOwnerScalars owner;
            return RebirthSurvivorClientState.TryGetOwnerScalars(player, out owner)
                && RebirthBackgroundBonusRegistry.TryGetByBackground(owner.BackgroundId, out definition);
        }

        RebirthWorldCharacterRecord record;
        return RebirthWorldCharacterService.TryGet(player, out record) && TryGetSignatureBonus(record, out definition);
    }

    public static bool TryGetSignatureBonus(RebirthWorldCharacterRecord record, out RebirthBackgroundBonusDefinition definition)
    {
        definition = null;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || !RebirthBackgroundBonusRegistry.IsReady || record == null || !record.IsComplete || record.Origin == null) return false;
        return RebirthBackgroundBonusRegistry.TryGetByBackground(record.Origin.BackgroundId, out definition);
    }

    public static bool TryGetSignatureBonus(RebirthSurvivorOwnerStateSnapshot owner, out RebirthBackgroundBonusDefinition definition)
    {
        definition = null;
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() || !RebirthBackgroundBonusRegistry.IsReady || owner == null || !owner.RebirthModeEnabled || !owner.HasCharacter) return false;
        return RebirthBackgroundBonusRegistry.TryGetByBackground(owner.BackgroundId, out definition);
    }

    public static bool TryGetSignatureBonus(RebirthSurvivorProfile profile, out RebirthBackgroundBonusDefinition definition)
    {
        definition = null;
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth || !RebirthBackgroundBonusRegistry.IsReady || profile == null) return false;
        return RebirthBackgroundBonusRegistry.TryGetByBackground(profile.BackgroundId, out definition);
    }

    public static bool TryGetSignatureBonus(RebirthSurvivorCreationSelection selection, out RebirthBackgroundBonusDefinition definition)
    {
        definition = null;
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth || !RebirthBackgroundBonusRegistry.IsReady || selection == null) return false;
        return RebirthBackgroundBonusRegistry.TryGetByBackground(selection.BackgroundId, out definition);
    }

    public static bool HasBonus(EntityPlayer player, string bonusId)
    {
        RebirthBackgroundBonusDefinition definition;
        return TryGetSignatureBonus(player, out definition) && definition != null && string.Equals(definition.Id, bonusId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
