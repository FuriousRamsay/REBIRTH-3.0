using Platform;

#nullable disable

/// <summary>
/// Shared ownership and access-list checks used by pickup and rename.
/// The server always performs the final authorization.
/// </summary>
public static class RebirthBlockPickupAccessService
{
    public static bool IsAdminOrEditor(WorldBase world, EntityPlayer player)
    {
        return (world != null && world.IsEditor()) || (player != null && player.IsAdmin);
    }

    public static bool CanAccessOwnedComposite(
        TileEntityComposite composite,
        PlatformUserIdentifierAbs userId,
        WorldBase world,
        EntityPlayer player)
    {
        if (composite == null || composite.Owner == null)
            return true;

        if (IsAdminOrEditor(world, player))
            return true;

        if (userId == null)
            return false;

        if (composite.Owner.Equals(userId))
            return true;

        TEFeatureLockable lockable = composite.GetFeature<TEFeatureLockable>();
        return lockable != null && lockable.IsUserAllowed(userId);
    }

    public static string GetOwnerDisplayName(PlatformUserIdentifierAbs owner)
    {
        if (owner == null)
            return string.Empty;

        PersistentPlayerList players = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList()
            : null;
        PersistentPlayerData data = players != null ? players.GetPlayerData(owner) : null;
        if (data != null && data.PlayerName != null && !string.IsNullOrEmpty(data.PlayerName.DisplayName))
            return EscapeDisplayName(data.PlayerName.DisplayName);

        return EscapeDisplayName(
            owner.ReadablePlatformUserIdentifier ?? owner.CombinedString ?? string.Empty);
    }

    private static string EscapeDisplayName(string value)
    {
        return Utils.EscapeBbCodes(GameUtils.SafeStringFormat(value ?? string.Empty));
    }
}
