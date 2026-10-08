using Platform;
using System;
using UnityEngine;

#nullable disable

public static class RebirthContainerRenameService
{
    public const int MaximumNameLength = 100;
    private const float MaximumRenameDistance = 6f;

    public static bool TryHandleActivation(
        string commandName,
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        EntityPlayerLocal player)
    {
        if (!string.Equals(commandName, TEFeatureRebirthContainerName.EditCommand, StringComparison.OrdinalIgnoreCase))
            return false;

        if (world == null || player == null)
            return true;

        ResolveParentBlock(world, ref blockPos, ref blockValue);
        TileEntityComposite composite = world.GetTileEntity(blockPos) as TileEntityComposite;

        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        PlatformUserIdentifierAbs userId = persistent != null
            ? persistent.PrimaryId
            : PlatformManager.InternalLocalUserIdentifier;

        if (!CanRename(composite, userId, world, player))
        {
            GameManager.ShowTooltip(player, Localization.Get("xuiRebirthContainerRenameDenied"), string.Empty, "ui_denied");
            return true;
        }

        player.AimingGun = false;
        XUiC_RebirthRenameContainer.Open(
            player.PlayerUI.xui,
            blockPos,
            GetCustomName(world, blockPos, composite),
            blockValue.Block != null ? blockValue.Block.GetLocalizedBlockName() : string.Empty);
        return true;
    }

    public static BlockActivationCommand[] AppendCommandIfNeeded(
        BlockActivationCommand[] commands,
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        EntityAlive entityFocusing)
    {
        BlockActivationCommand[] source = commands ?? BlockActivationCommand.Empty;
        if (world == null || !(entityFocusing is EntityPlayerLocal))
            return source;

        ResolveParentBlock(world, ref blockPos, ref blockValue);
        TileEntityComposite composite = world.GetTileEntity(blockPos) as TileEntityComposite;
        EntityPlayer player = entityFocusing as EntityPlayer;
        PlatformUserIdentifierAbs userId = ResolveUserId(player);
        if (!CanRename(composite, userId, world, player))
            return source;

        for (int i = 0; i < source.Length; i++)
        {
            if (string.Equals(source[i].text, TEFeatureRebirthContainerName.EditCommand,
                StringComparison.OrdinalIgnoreCase))
                return source;
        }

        BlockActivationCommand[] result = new BlockActivationCommand[source.Length + 1];
        Array.Copy(source, result, source.Length);
        result[source.Length] = new BlockActivationCommand(
            TEFeatureRebirthContainerName.EditCommand, "pen", true);
        return result;
    }

    public static void ConfigureCommands(
        BlockActivationCommand[] commands,
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        EntityAlive entityFocusing)
    {
        if (commands == null || world == null)
            return;

        ResolveParentBlock(world, ref blockPos, ref blockValue);
        TileEntityComposite composite = world.GetTileEntity(blockPos) as TileEntityComposite;
        EntityPlayer focusingPlayer = entityFocusing as EntityPlayer;
        PlatformUserIdentifierAbs userId = ResolveUserId(focusingPlayer);

        bool enabled = entityFocusing is EntityPlayerLocal &&
            CanRename(composite, userId, world, focusingPlayer);

        for (int i = 0; i < commands.Length; i++)
        {
            if (string.Equals(commands[i].text, TEFeatureRebirthContainerName.EditCommand, StringComparison.OrdinalIgnoreCase))
                commands[i].enabled = enabled;
        }
    }

    public static void ProcessServerRequest(
        World world,
        Vector3i blockPos,
        int playerId,
        PlatformUserIdentifierAbs requestedUserId,
        string requestedName)
    {
        if (world == null || GameManager.Instance == null || requestedUserId == null)
            return;

        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance.GetPersistentPlayerList()
            ?.GetPlayerDataFromEntityID(playerId);
        if (player == null || persistent == null || persistent.PrimaryId == null ||
            !persistent.PrimaryId.Equals(requestedUserId))
            return;

        BlockValue blockValue = world.GetBlock(blockPos);
        ResolveParentBlock(world, ref blockPos, ref blockValue);
        if (blockValue.isair)
            return;

        Vector3 center = blockPos.ToVector3() + Vector3.one * 0.5f;
        if ((player.position - center).sqrMagnitude > MaximumRenameDistance * MaximumRenameDistance)
            return;

        TileEntityComposite composite = world.GetTileEntity(blockPos) as TileEntityComposite;
        if (!CanRename(composite, persistent.PrimaryId, world, player))
            return;

        TEFeatureRebirthContainerName feature = composite != null
            ? composite.GetFeature<TEFeatureRebirthContainerName>()
            : null;

        // Player-owned/generated storage carries the name in its TE feature so the name
        // survives pickup/replacement.  Ordinary POI/world storage must keep its original
        // composite schema and therefore uses the external per-world name registry.
        if (feature != null && composite.Owner != null)
            feature.SetCustomName(requestedName);
        else
            RebirthContainerNameRegistry.SetServer(world, blockPos, requestedName);
    }

    public static bool CanRename(
        TileEntityComposite composite,
        PlatformUserIdentifierAbs userId,
        WorldBase world,
        EntityPlayer player = null)
    {
        if (composite == null || userId == null || world == null || player == null)
            return false;

        TEFeatureStorage loot;
        if (!composite.TryGetSelfOrFeature<TEFeatureStorage>(out loot) || loot == null ||
            !RemoteResourceSourcePolicy.IsSupportedStatic(composite, loot))
            return false;

        if (composite.Owner != null)
            return RebirthBlockPickupAccessService.CanAccessOwnedComposite(
                composite, userId, world, player);

        string reason;
        return RemoteResourceAccess.AuthorizeComposite(composite, player, false, out reason);
    }

    public static void ApplyCustomNameToActivationText(
        WorldBase world,
        Vector3i blockPos,
        BlockValue blockValue,
        ref string activationText)
    {
        if (world == null || blockValue.Block == null)
            return;

        ResolveParentBlock(world, ref blockPos, ref blockValue);
        TileEntityComposite composite = world.GetTileEntity(blockPos) as TileEntityComposite;
        if (composite == null)
            return;

        string localizedName = blockValue.Block.GetLocalizedBlockName();
        string customName = GetCustomName(world, blockPos, composite);
        string displayName = !string.IsNullOrEmpty(customName)
            ? customName
            : localizedName;

        if (!string.IsNullOrEmpty(activationText) && !string.IsNullOrEmpty(localizedName) &&
            !string.Equals(displayName, localizedName, StringComparison.Ordinal))
            activationText = activationText.Replace(localizedName, displayName);

        TEFeatureStorage storage;
        bool hasStorage = composite.TryGetSelfOrFeature<TEFeatureStorage>(out storage) && storage != null;
        if (composite.Owner != null && hasStorage && storage.IsEmpty() &&
            CanShowEmptyState(composite))
        {
            // Base 3.0 renders this as "Empty Name". REBIRTH deliberately
            // renders the state after the display name so renames remain natural.
            activationText = displayName + " ([D6C978]" +
                Localization.Get("xuiRebirthEmpty") + "[-])";
        }
        else if (string.IsNullOrEmpty(activationText))
        {
            activationText = displayName;
        }

        string ownerLabel = Localization.Get("xuiRebirthOwnedBy") + ":";
        if (composite.Owner == null ||
            activationText.IndexOf("\n" + ownerLabel, StringComparison.Ordinal) >= 0)
            return;

        string ownerName = RebirthBlockPickupAccessService.GetOwnerDisplayName(composite.Owner);
        if (string.IsNullOrEmpty(ownerName))
            return;

        activationText += "\n" + ownerLabel + " [B58CFF]" + ownerName + "[-]";
    }

    public static string GetCustomName(
        WorldBase world, Vector3i blockPos, TileEntityComposite composite = null)
    {
        if (world == null)
            return string.Empty;

        BlockValue blockValue = world.GetBlock(blockPos);
        ResolveParentBlock(world, ref blockPos, ref blockValue);
        if (composite == null)
            composite = world.GetTileEntity(blockPos) as TileEntityComposite;
        if (composite == null)
            return string.Empty;

        TEFeatureRebirthContainerName feature =
            composite.GetFeature<TEFeatureRebirthContainerName>();
        if (feature != null && !string.IsNullOrEmpty(feature.CustomName))
            return feature.CustomName;

        return RebirthContainerNameRegistry.Get(world, blockPos);
    }

    internal static void ApplyCustomNameToLootWindow(XUiC_LootWindow lootWindow, RebirthLootTitleProjection titleState)
    {
        if (lootWindow == null || titleState == null || lootWindow.te == null || GameManager.Instance == null ||
            GameManager.Instance.World == null)
            return;

        World world = GameManager.Instance.World;
        Vector3i position = lootWindow.te.ToWorldPos();
        string customName = GetCustomName(world, position);
        string title = titleState.Resolve(lootWindow.te, lootWindow.lootContainerName, customName);
        if (string.Equals(lootWindow.lootContainerName, title, StringComparison.Ordinal))
            return;

        lootWindow.lootContainerName = title;
        lootWindow.RefreshBindings();
    }

    private static PlatformUserIdentifierAbs ResolveUserId(EntityPlayer player)
    {
        PersistentPlayerData persistent = player != null && GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId)
            : GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (persistent != null && persistent.PrimaryId != null)
            return persistent.PrimaryId;
        return PlatformManager.InternalLocalUserIdentifier;
    }

    private static bool CanShowEmptyState(TileEntityComposite composite)
    {
        TEFeatureLockPickable lockPickable = composite.GetFeature<TEFeatureLockPickable>();
        if (lockPickable != null && lockPickable.NeedsLockpicking())
            return false;

        TEFeatureLockable lockable = composite.GetFeature<TEFeatureLockable>();
        return lockable == null || !lockable.IsLocked() ||
            lockable.IsUserAllowed(PlatformManager.InternalLocalUserIdentifier);
    }

    public static string NormalizeName(string value)
    {
        string normalized = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();
        if (normalized.Length > MaximumNameLength)
            normalized = normalized.Substring(0, MaximumNameLength);
        return normalized;
    }

    private static void ResolveParentBlock(
        WorldBase world,
        ref Vector3i blockPos,
        ref BlockValue blockValue)
    {
        if (world == null || !blockValue.ischild || blockValue.Block == null ||
            blockValue.Block.multiBlockPos == null)
            return;

        Vector3i parentPos = blockValue.Block.multiBlockPos.GetParentPos(blockPos, blockValue);
        BlockValue parentValue = world.GetBlock(parentPos);
        if (parentValue.isair || parentValue.ischild)
            return;

        blockPos = parentPos;
        blockValue = parentValue;
    }
}
