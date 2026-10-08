using Platform;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// ILockable adapter used by the base 3.0 keypad window. All persistent changes
/// are submitted to RebirthWorkstationSecurityService and revalidated by server.
/// </summary>
public sealed class RebirthWorkstationLockable : ILockable
{
    private readonly Vector3i blockPos;
    private readonly EntityPlayerLocal player;

    public RebirthWorkstationLockable(Vector3i position, EntityPlayerLocal localPlayer)
    {
        blockPos = position;
        player = localPlayer;
    }

    internal bool CanAccess(EntityPlayer requester, RebirthSecureAccessPurpose purpose, out string reason)
    {
        PlatformUserIdentifierAbs user;
        if (!RemoteResourceAccess.TryGetPersistentId(requester, out user))
        { reason = "missing persistent requester"; return false; }
        return RebirthWorkstationSecurityService.CanAccessWorkstation(requester.world, blockPos,
            requester, user, false, purpose, out reason);
    }

    public bool IsLocked()
    {
        return RebirthWorkstationSecurityService.IsLocked(blockPos);
    }

    public void SetLocked(bool isLocked)
    {
        // Lock/unlock is performed through radial commands, not the keypad.
    }

    public PlatformUserIdentifierAbs GetOwner()
    {
        PlatformUserIdentifierAbs owner;
        RebirthWorkstationSecurityService.TryGetOwner(blockPos, out owner);
        return owner;
    }

    public void SetOwner(PlatformUserIdentifierAbs userIdentifier)
    {
        // Ownership is established only by authoritative placement/migration.
    }

    public bool IsUserAllowed(PlatformUserIdentifierAbs userIdentifier)
    {
        return RebirthWorkstationSecurityService.IsUserAllowed(
            blockPos, NormalizeLocalIdentifier(userIdentifier));
    }

    public List<PlatformUserIdentifierAbs> GetUsers()
    {
        return RebirthWorkstationSecurityService.GetAllowedUsers(blockPos);
    }

    public bool LocalPlayerIsOwner()
    {
        return RebirthWorkstationSecurityService.IsOwner(
            blockPos, GetPersistentLocalIdentifier());
    }

    public bool IsOwner(PlatformUserIdentifierAbs userIdentifier)
    {
        return RebirthWorkstationSecurityService.IsOwner(
            blockPos, NormalizeLocalIdentifier(userIdentifier));
    }

    public bool HasPassword()
    {
        return RebirthWorkstationSecurityService.HasPassword(blockPos);
    }

    public bool SetPasswordHash(
        string passwordHash,
        PlatformUserIdentifierAbs userIdentifier)
    {
        return RebirthWorkstationSecurityService.SetPasswordHash(
            blockPos, player, passwordHash, NormalizeLocalIdentifier(userIdentifier));
    }

    public bool CheckPasswordHash(
        string passwordHash,
        PlatformUserIdentifierAbs userIdentifier)
    {
        return RebirthWorkstationSecurityService.CheckPasswordHash(
            blockPos, player, passwordHash, NormalizeLocalIdentifier(userIdentifier));
    }

    public string GetPasswordHash()
    {
        return RebirthWorkstationSecurityService.GetPasswordHash(blockPos);
    }

    public string GetHashForPassword(string password)
    {
        return RebirthWorkstationCredentials.ClientCredential(password);
    }

    private static PlatformUserIdentifierAbs NormalizeLocalIdentifier(
        PlatformUserIdentifierAbs userIdentifier)
    {
        if (userIdentifier != null &&
            PlatformManager.InternalLocalUserIdentifier != null &&
            userIdentifier.Equals(PlatformManager.InternalLocalUserIdentifier))
        {
            PlatformUserIdentifierAbs persistent = GetPersistentLocalIdentifier();
            if (persistent != null)
                return persistent;
        }
        return userIdentifier;
    }

    private static PlatformUserIdentifierAbs GetPersistentLocalIdentifier()
    {
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        return persistent != null && persistent.PrimaryId != null
            ? persistent.PrimaryId
            : PlatformManager.InternalLocalUserIdentifier;
    }
}
