using Platform;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class RemoteResourceToggleCommand
{
    public const string CommandName = "remoteresourcetoggle";

    public static BlockActivationCommand[] Append(
        BlockActivationCommand[] source, WorldBase worldBase, Vector3i position, EntityAlive focusing)
    {
        BlockActivationCommand[] original = source ?? BlockActivationCommand.Empty;
        World world = worldBase as World;
        EntityPlayer player = focusing as EntityPlayer;
        TileEntity tileEntity = world != null ? world.GetTileEntity(position) : null;
        IRemoteResourceSource resourceSource = CreateSource(world, tileEntity);
        if (resourceSource == null || !resourceSource.IsLoaded) return original;
        string accessReason;
        bool enabled = player != null && resourceSource.IsAuthorized(player, false, out accessReason);

        for (int i = 0; i < original.Length; i++)
        {
            if (!string.Equals(original[i].text, CommandName, StringComparison.OrdinalIgnoreCase)) continue;
            original[i].enabled = enabled;
            return original;
        }

        BlockActivationCommand[] result = new BlockActivationCommand[original.Length + 1];
        Array.Copy(original, result, original.Length);
        result[original.Length] = new BlockActivationCommand(
            CommandName, "resource", enabled);
        return result;
    }

    public static void RequestToggle(Vector3i position)
    {
        EntityPlayerLocal player = GameManager.Instance != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        if (player == null || persistent == null || persistent.PrimaryId == null) return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection.IsServer)
        {
            bool excluded;
            string reason;
            bool success = TryToggle(
                GameManager.Instance.World,
                player.entityId,
                persistent.PrimaryId,
                position,
                out excluded,
                out reason);
            ShowResult(success && excluded, reason);
            XUiC_RebirthRemoteResourceButton.ReceiveState(
                position, success, excluded, reason);
        }
        else
        {
            connection.SendToServer(
                NetPackageManager.GetPackage<NetPackageRemoteResourceToggleRequest>()
                    .Setup(player.entityId, persistent, position));
        }
    }

    public static void RequestState(Vector3i position)
    {
        EntityPlayerLocal player = GameManager.Instance != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        if (player == null || persistent == null || persistent.PrimaryId == null) return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection.IsServer)
        {
            bool excluded;
            string reason;
            bool manageable = TryReadState(
                GameManager.Instance.World,
                player.entityId,
                persistent.PrimaryId,
                position,
                out excluded,
                out reason);
            XUiC_RebirthRemoteResourceButton.ReceiveState(
                position, manageable, excluded, reason);
        }
        else
        {
            connection.SendToServer(
                NetPackageManager.GetPackage<NetPackageRemoteResourceStateRequest>()
                    .Setup(player.entityId, persistent, position));
        }
    }

    public static bool TryReadState(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        Vector3i position,
        out bool excluded,
        out string reason)
    {
        excluded = false;
        reason = string.Empty;
        EntityPlayer player = world != null ? world.GetEntity(playerId) as EntityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId)
            : null;
        TileEntity tileEntity = world != null ? world.GetTileEntity(position) : null;
        if (player == null || persistent == null || persistent.PrimaryId == null ||
            userId == null || !persistent.PrimaryId.Equals(userId) || tileEntity == null ||
            tileEntity is TileEntityWorkstation)
        {
            reason = "invalid player or static container";
            return false;
        }
        if ((tileEntity.ToWorldPos().ToVector3() - player.position).sqrMagnitude > 64f)
        {
            reason = "source is too far away to configure";
            return false;
        }

        StaticContainerResourceSource resourceSource =
            new StaticContainerResourceSource(world, position);
        if (!resourceSource.IsLoaded ||
            !resourceSource.IsAuthorized(player, false, out reason))
        {
            return false;
        }

        excluded = RemoteResourceStateStore.IsExcluded(resourceSource.StableId, userId);
        // The loot-window control presents Remote Resources as On by default. Persist that
        // state the first time an authorized container is actually opened/configured so an
        // untouched POI container is not silently enrolled merely because it is nearby.
        if (!excluded && RemoteResourceStateStore.Activate(resourceSource.StableId))
            RemoteResourceLiveSync.NotifySourceChanged(resourceSource.StableId, resourceSource.Position, "container enrolled from default-on Remote Resources state");
        return true;
    }

    public static bool TryHandle(
        string command, WorldBase world, Vector3i position, EntityPlayerLocal player)
    {
        if (!string.Equals(command, CommandName, StringComparison.OrdinalIgnoreCase)) return false;
        RequestToggle(position);
        return true;
    }

    public static bool TryToggle(
        World world,
        int playerId,
        PlatformUserIdentifierAbs userId,
        Vector3i position,
        out bool excluded,
        out string reason)
    {
        excluded = false;
        reason = string.Empty;
        EntityPlayer player = world != null ? world.GetEntity(playerId) as EntityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId)
            : null;
        TileEntity tileEntity = world != null ? world.GetTileEntity(position) : null;
        if (player == null || persistent == null || persistent.PrimaryId == null ||
            userId == null || !persistent.PrimaryId.Equals(userId) || tileEntity == null)
        {
            reason = "invalid player or source";
            return false;
        }
        if ((tileEntity.ToWorldPos().ToVector3() - player.position).sqrMagnitude > 64f)
        {
            reason = "source is too far away to configure";
            return false;
        }

        IRemoteResourceSource resourceSource;
        if (tileEntity is TileEntityWorkstation)
            resourceSource = new WorkstationOutputResourceSource(world, position);
        else
            resourceSource = new StaticContainerResourceSource(world, position);

        if (!resourceSource.IsLoaded || !resourceSource.IsAuthorized(player, false, out reason)) return false;
        excluded = !RemoteResourceStateStore.IsExcluded(resourceSource.StableId, userId);
        RemoteResourceStateStore.SetExcluded(resourceSource.StableId, userId, excluded);
        if (!excluded) RemoteResourceStateStore.Activate(resourceSource.StableId);
        RemoteResourceLiveSync.NotifySourceChanged(resourceSource.StableId, resourceSource.Position, excluded ? "player disabled source" : "player enabled source");
        reason = excluded
            ? Localization.Get("xuiRebirthRemoteResourcesDisabled")
            : Localization.Get("xuiRebirthRemoteResourcesEnabled");
        return true;
    }

    public static bool CanManage(World world, EntityPlayer player, TileEntity tileEntity)
    {
        IRemoteResourceSource source = CreateSource(world, tileEntity);
        if (source == null || player == null || !source.IsLoaded) return false;
        string reason;
        // The client does not own the persistent activation/exclusion store on a
        // dedicated server. Keep the command selectable after typed access checks and
        // let the server validate activation before changing state.
        return source.IsAuthorized(player, false, out reason);
    }

    private static IRemoteResourceSource CreateSource(World world, TileEntity tileEntity)
    {
        if (world == null || tileEntity == null) return null;
        if (tileEntity is TileEntityWorkstation)
            return new WorkstationOutputResourceSource(world, tileEntity.ToWorldPos());
        StaticContainerResourceSource source = new StaticContainerResourceSource(world, tileEntity.ToWorldPos());
        return source.IsLoaded ? (IRemoteResourceSource)source : null;
    }

    public static void ShowResult(bool excluded, string reason)
    {
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (player == null) return;
        string text = !string.IsNullOrEmpty(reason)
            ? reason
            : Localization.Get(excluded ? "xuiRebirthRemoteResourcesDisabled" : "xuiRebirthRemoteResourcesEnabled");
        GameManager.ShowTooltip(player, text);
    }
}

[Preserve]
public sealed class NetPackageRemoteResourceToggleRequest : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private Vector3i position;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRemoteResourceToggleRequest Setup(int id, PersistentPlayerData persistent, Vector3i pos)
    {
        playerId = id;
        userId = persistent.PrimaryId;
        position = pos;
        return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        position = new Vector3i(binary.ReadInt32(), binary.ReadInt32(), binary.ReadInt32());
    }
    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId);
        userId.ToStream(binary);
        binary.Write(position.x);
        binary.Write(position.y);
        binary.Write(position.z);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;
        bool excluded;
        string reason;
        bool success = RemoteResourceToggleCommand.TryToggle(world, playerId, userId, position, out excluded, out reason);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRemoteResourceToggleResult>().Setup(position, success, excluded, reason),
            _attachedToEntityId: playerId);
    }
    public int GetLength() { return 48; }
}

[Preserve]
public sealed class NetPackageRemoteResourceToggleResult : NetPackage
{
    private Vector3i position;
    private bool success;
    private bool excluded;
    private string reason;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRemoteResourceToggleResult Setup(
        Vector3i pos, bool ok, bool isExcluded, string message)
    {
        position = pos;
        success = ok;
        excluded = isExcluded;
        reason = message ?? string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        success = reader.ReadBoolean();
        excluded = reader.ReadBoolean();
        reason = reader.ReadString();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(position.x);
        binary.Write(position.y);
        binary.Write(position.z);
        binary.Write(success);
        binary.Write(excluded);
        binary.Write(reason ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RemoteResourceToggleCommand.ShowResult(success && excluded, reason);
        XUiC_RebirthRemoteResourceButton.ReceiveState(
            position, success, excluded, reason);
    }

    public int GetLength() { return 0; }
}

[Preserve]
public sealed class NetPackageRemoteResourceStateRequest : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private Vector3i position;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRemoteResourceStateRequest Setup(
        int id, PersistentPlayerData persistent, Vector3i pos)
    {
        playerId = id;
        userId = persistent.PrimaryId;
        position = pos;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        position = new Vector3i(binary.ReadInt32(), binary.ReadInt32(), binary.ReadInt32());
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId);
        userId.ToStream(binary);
        binary.Write(position.x);
        binary.Write(position.y);
        binary.Write(position.z);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;

        bool excluded;
        string reason;
        bool manageable = RemoteResourceToggleCommand.TryReadState(
            world, playerId, userId, position, out excluded, out reason);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRemoteResourceStateResult>()
                .Setup(position, manageable, excluded, reason),
            _attachedToEntityId: playerId);
    }

    public int GetLength() { return 48; }
}

[Preserve]
public sealed class NetPackageRemoteResourceStateResult : NetPackage
{
    private Vector3i position;
    private bool manageable;
    private bool excluded;
    private string reason;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRemoteResourceStateResult Setup(
        Vector3i pos, bool canManage, bool isExcluded, string message)
    {
        position = pos;
        manageable = canManage;
        excluded = isExcluded;
        reason = message ?? string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        manageable = reader.ReadBoolean();
        excluded = reader.ReadBoolean();
        reason = reader.ReadString();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(position.x);
        binary.Write(position.y);
        binary.Write(position.z);
        binary.Write(manageable);
        binary.Write(excluded);
        binary.Write(reason ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        XUiC_RebirthRemoteResourceButton.ReceiveState(
            position, manageable, excluded, reason);
    }

    public int GetLength() { return 0; }
}

/// <summary>
/// 2.6-style Wi-Fi toggle shown in the normal loot window for static containers.
/// White/unselected means included in Remote Resources (default).
/// Gray/selected means explicitly excluded. Server state remains authoritative.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthRemoteResourceButton : XUiController
{
    private static XUiC_RebirthRemoteResourceButton active;

    private XUiV_Button button;
    private XUiC_LootWindow lootWindow;
    private Vector3i position;
    private bool hasPosition;
    private float nextRefreshAt;

    public override void Init()
    {
        base.Init();
        button = viewComponent as XUiV_Button;
        OnPress += OnButtonPressed;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        active = this;
        lootWindow = GetParentByType<XUiC_LootWindow>();
        hasPosition = false;
        nextRefreshAt = 0f;
        SetHidden();
        Refresh(true);
    }

    public override void OnClose()
    {
        if (active == this) active = null;
        hasPosition = false;
        SetHidden();
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (Time.time < nextRefreshAt) return;
        nextRefreshAt = Time.time + 0.5f;
        Refresh(false);
    }

    private void Refresh(bool force)
    {
        if (button == null) return;
        if (lootWindow == null) lootWindow = GetParentByType<XUiC_LootWindow>();
        TEFeatureStorage loot = lootWindow != null ? lootWindow.te : null;
        if (loot == null || GameManager.Instance == null || GameManager.Instance.World == null)
        {
            hasPosition = false;
            SetHidden();
            return;
        }

        Vector3i current = loot.ToWorldPos();
        TileEntity tileEntity = GameManager.Instance.World.GetTileEntity(current);
        TEFeatureStorage staticLoot = null;
        if (tileEntity == null || tileEntity is TileEntityWorkstation ||
            !tileEntity.TryGetSelfOrFeature<TEFeatureStorage>(out staticLoot) ||
            !RemoteResourceSourcePolicy.IsSupportedStatic(tileEntity, staticLoot))
        {
            hasPosition = false;
            SetHidden();
            return;
        }

        bool changed = !hasPosition || !SamePosition(position, current);
        position = current;
        hasPosition = true;
        button.IsVisible = true;
        button.Enabled = true;

        // Default is Remote Resources On until authoritative state says otherwise.
        if (changed) button.Selected = false;
        if (force || changed) RemoteResourceToggleCommand.RequestState(position);
    }

    private void OnButtonPressed(XUiController sender, int mouseButton)
    {
        if (!hasPosition || button == null || !button.Enabled) return;
        RemoteResourceToggleCommand.RequestToggle(position);
    }

    private void SetHidden()
    {
        if (button == null) return;
        button.IsVisible = false;
        button.Enabled = false;
        button.Selected = false;
    }

    private void ApplyState(Vector3i pos, bool manageable, bool excluded)
    {
        if (!hasPosition || !SamePosition(position, pos) || button == null) return;
        button.IsVisible = manageable;
        button.Enabled = manageable;
        button.Selected = manageable && excluded;
    }

    public static void ReceiveState(
        Vector3i pos, bool manageable, bool excluded, string reason)
    {
        XUiC_RebirthRemoteResourceButton current = active;
        if (current != null) current.ApplyState(pos, manageable, excluded);
    }

    private static bool SamePosition(Vector3i a, Vector3i b)
    {
        return a.x == b.x && a.y == b.y && a.z == b.z;
    }
}
