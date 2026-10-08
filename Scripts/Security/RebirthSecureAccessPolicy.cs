using Platform;

#nullable disable

public enum RebirthSecureAccessMode
{
    PinAccessList = 0,
    Allies = 1,
    Party = 2
}

public enum RebirthSecureAccessPurpose
{
    DirectInteraction,
    RemoteResourceRead,
    RemoteResourceConsume,
    QuickStackDeposit,
    QuickStackWithdraw,
    HornDoorActivation,
    WorkstationAccess,
    Automation
}

/// <summary>
/// Single server-authoritative policy for REBIRTH access to locked secure objects.
/// Native owner/access-list authorization is always evaluated before cumulative
/// Allies and Party expansion. PIN values are never read, compared, or transmitted.
/// </summary>
public static class RebirthSecureAccessPolicy
{
    public static bool CanAccess(
        EntityPlayer requester,
        ILockable target,
        RebirthSecureAccessPurpose purpose,
        out string reason)
    {
        reason = string.Empty;
        if (requester == null || target == null)
        {
            reason = "missing requester or target";
            return false;
        }

        RebirthWorkstationLockable workstation = target as RebirthWorkstationLockable;
        if (workstation != null) return workstation.CanAccess(requester, purpose, out reason);

        // Insecure and unlocked secure objects retain normal game behavior.
        if (!target.IsLocked())
            return true;

        PlatformUserIdentifierAbs requesterId;
        if (!RemoteResourceAccess.TryGetPersistentId(requester, out requesterId))
        {
            reason = "missing persistent requester identity";
            return false;
        }

        // Native authorization is authoritative for owner and explicit access-list/PIN grants.
        if (target.IsOwner(requesterId) || target.IsUserAllowed(requesterId))
            return true;

        PlatformUserIdentifierAbs ownerId = target.GetOwner();
        if (ownerId == null)
        {
            reason = "locked secure object has no owner";
            return false;
        }

        if (CanExpandAccess(requesterId, ownerId, purpose, out reason))
            return true;

        reason = "requester is not owner, natively authorized, allied, or in the owner's party";
        return false;
    }


    /// <summary>
    /// Expands a failed native owner/access-list authorization according to the
    /// configured REBIRTH sharing mode. This method deliberately does not call
    /// ILockable.IsUserAllowed, so it is safe for the global native postfix.
    /// </summary>
    public static bool CanExpandAccess(
        PlatformUserIdentifierAbs requesterId,
        PlatformUserIdentifierAbs ownerId,
        RebirthSecureAccessPurpose purpose,
        out string reason)
    {
        reason = string.Empty;
        if (requesterId == null || ownerId == null)
        {
            reason = "missing requester or owner identity";
            return false;
        }

        if (purpose == RebirthSecureAccessPurpose.WorkstationAccess)
        {
            reason = "workstations require owner or explicit allow-list access";
            return false;
        }
        RebirthSecureAccessMode mode = RebirthSandboxOptionManager.Current.SecureAccessSharing;
        if (mode >= RebirthSecureAccessMode.Allies && AreAllies(requesterId, ownerId))
            return true;

        if (mode >= RebirthSecureAccessMode.Party)
        {
            EntityPlayer requester = FindOnlinePlayer(requesterId);
            if (requester != null && ArePartyMembers(requester, ownerId))
                return true;
        }

        reason = "requester is not allied with or in the owner's party";
        return false;
    }

    public static bool CanAccess(
        EntityPlayer requester,
        TileEntityComposite target,
        RebirthSecureAccessPurpose purpose,
        out string reason)
    {
        reason = string.Empty;
        if (target == null)
        {
            reason = "missing target";
            return false;
        }

        TEFeatureLockable lockable = target.GetFeature<TEFeatureLockable>();
        if (lockable == null || !lockable.IsLocked())
            return true;

        return CanAccess(requester, lockable, purpose, out reason);
    }

    private static bool AreAllies(PlatformUserIdentifierAbs requesterId, PlatformUserIdentifierAbs ownerId)
    {
        PersistentPlayerList list = GameManager.Instance?.GetPersistentPlayerList();
        if (list == null)
            return false;

        return list.Allies != null && list.Allies.IsAlly(requesterId, ownerId);
    }

    private static EntityPlayer FindOnlinePlayer(PlatformUserIdentifierAbs userId)
    {
        World world = GameManager.Instance?.World;
        if (world == null || userId == null)
            return null;

        foreach (EntityPlayer player in world.Players.list)
        {
            PlatformUserIdentifierAbs playerId;
            if (RemoteResourceAccess.TryGetPersistentId(player, out playerId) && userId.Equals(playerId))
                return player;
        }

        return null;
    }

    private static bool ArePartyMembers(EntityPlayer requester, PlatformUserIdentifierAbs ownerId)
    {
        if (requester == null || requester.Party == null || requester.world == null)
            return false;

        EntityPlayer owner = null;
        foreach (EntityPlayer player in requester.world.Players.list)
        {
            PlatformUserIdentifierAbs playerId;
            if (RemoteResourceAccess.TryGetPersistentId(player, out playerId) && ownerId.Equals(playerId))
            {
                owner = player;
                break;
            }
        }

        // Party access is intentionally current-party only; an offline owner has no current party.
        return owner != null && owner.Party != null && object.ReferenceEquals(requester.Party, owner.Party);
    }
}
