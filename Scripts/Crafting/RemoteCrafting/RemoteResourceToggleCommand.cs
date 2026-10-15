using Platform;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public static class RemoteResourceToggleCommand
{
    public const string CommandName = "remoteresourcetoggle";
    private static int sequence, pending, pendingUiScope;
    private static World pendingWorld;
    private static EntityPlayerLocal pendingPlayer;
    private static TileEntity pendingTile;
    private static Vector3i pendingPosition;
    internal static void ClearReplyScope()
    {
        pending = 0; pendingWorld = null; pendingPlayer = null; pendingTile = null;
    }

    private static int BeginReplyScope(World world, EntityPlayerLocal player, Vector3i position)
    {
        if (sequence == int.MaxValue || world == null || player == null ||
            !ReferenceEquals(world, GameManager.Instance?.World) ||
            !ReferenceEquals(world.GetPrimaryPlayer(), player) || !ReferenceEquals(player.world, world)) return 0;
        TileEntity tile = world.GetTileEntity(position);
        if (tile == null) return 0;
        pendingWorld = world; pendingPlayer = player; pendingTile = tile; pendingPosition = position;
        pendingUiScope = XUiC_RebirthRemoteResourceButton.ReplyScope;
        return pending = ++sequence;
    }

    internal static bool AdmitReply(World world, int request, Vector3i position)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || !world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) || connection == null ||
            !connection.IsClient || connection.IsServer || request <= 0 || request != pending ||
            !ReferenceEquals(world, pendingWorld) || !ReferenceEquals(world.GetPrimaryPlayer(), pendingPlayer) ||
            !ReferenceEquals(pendingPlayer?.world, world) || pendingUiScope != XUiC_RebirthRemoteResourceButton.ReplyScope ||
            !position.Equals(pendingPosition) || !ReferenceEquals(world.GetTileEntity(position), pendingTile)) return false;
        pending = 0; pendingWorld = null; pendingPlayer = null; pendingTile = null;
        return true;
    }

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
            ? GameManager.Instance.World?.GetPrimaryPlayer()
            : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        if (!ThreadManager.IsMainThread() || player == null || persistent == null || persistent.PrimaryId == null) return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return;
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
            int request = BeginReplyScope(GameManager.Instance.World, player, position);
            if (!connection.IsClient || request == 0) return;
            connection.SendToServer(
                NetPackageManager.GetPackage<NetPackageRemoteResourceToggleRequestScoped>()
                    .Setup(player.entityId, persistent, position, request));
        }
    }

    public static void RequestState(Vector3i position)
    {
        EntityPlayerLocal player = GameManager.Instance != null
            ? GameManager.Instance.World?.GetPrimaryPlayer()
            : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentLocalPlayer()
            : null;
        if (!ThreadManager.IsMainThread() || player == null || persistent == null || persistent.PrimaryId == null) return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return;
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
            int request = BeginReplyScope(GameManager.Instance.World, player, position);
            if (!connection.IsClient || request == 0) return;
            connection.SendToServer(
                NetPackageManager.GetPackage<NetPackageRemoteResourceStateRequestScoped>()
                    .Setup(player.entityId, persistent, position, request));
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
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World)) return false;
        EntityPlayer player = world != null ? world.GetEntity(playerId) as EntityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId)
            : null;
        TileEntity tileEntity = world != null ? world.GetTileEntity(position) : null;
        if (player == null || !ReferenceEquals(player.world, world) || persistent == null || persistent.PrimaryId == null ||
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
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World)) return false;
        EntityPlayer player = world != null ? world.GetEntity(playerId) as EntityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId)
            : null;
        TileEntity tileEntity = world != null ? world.GetTileEntity(position) : null;
        if (player == null || !ReferenceEquals(player.world, world) || persistent == null || persistent.PrimaryId == null ||
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
        EntityPlayerLocal player = GameManager.Instance != null ? GameManager.Instance.World?.GetPrimaryPlayer() : null;
        if (player == null) return;
        string text = !string.IsNullOrEmpty(reason)
            ? reason
            : Localization.Get(excluded ? "xuiRebirthRemoteResourcesDisabled" : "xuiRebirthRemoteResourcesEnabled");
        GameManager.ShowTooltip(player, text);
    }
}

[Preserve]
public sealed class NetPackageRemoteResourceToggleRequestScoped : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private Vector3i position;
    private int requestId;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRemoteResourceToggleRequestScoped Setup(int id, PersistentPlayerData persistent, Vector3i pos, int sequence)
    {
        playerId = id;
        userId = persistent.PrimaryId;
        position = pos;
        requestId = sequence;
        return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        position = new Vector3i(binary.ReadInt32(), binary.ReadInt32(), binary.ReadInt32());
        requestId = binary.ReadInt32();
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
        binary.Write(requestId);
    }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) || connection == null || !connection.IsServer ||
            Sender == null || !ReferenceEquals(connection.Clients?.ForEntityId(playerId), Sender) || requestId <= 0 || !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;
        bool excluded;
        string reason;
        bool success = RemoteResourceToggleCommand.TryToggle(world, playerId, userId, position, out excluded, out reason);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRemoteResourceToggleResultScoped>().Setup(position, success, excluded, reason, requestId),
            _attachedToEntityId: playerId);
    }
    public int GetLength() { return 52; }
}

[Preserve]
public sealed class NetPackageRemoteResourceToggleResultScoped : NetPackage
{
    private Vector3i position;
    private int requestId;
    private bool success;
    private bool excluded;
    private string reason;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRemoteResourceToggleResultScoped Setup(
        Vector3i pos, bool ok, bool isExcluded, string message, int sequence)
    {
        position = pos;
        requestId = sequence;
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
        requestId = reader.ReadInt32();
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
        binary.Write(requestId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!RemoteResourceToggleCommand.AdmitReply(world, requestId, position)) return;
        RemoteResourceToggleCommand.ShowResult(success && excluded, reason);
        XUiC_RebirthRemoteResourceButton.ReceiveState(
            position, success, excluded, reason);
    }

    public int GetLength() { return 0; }
}

[Preserve]
public sealed class NetPackageRemoteResourceStateRequestScoped : NetPackage
{
    private int playerId;
    private PlatformUserIdentifierAbs userId;
    private Vector3i position;
    private int requestId;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRemoteResourceStateRequestScoped Setup(
        int id, PersistentPlayerData persistent, Vector3i pos, int sequence)
    {
        playerId = id;
        userId = persistent.PrimaryId;
        position = pos;
        requestId = sequence;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        position = new Vector3i(binary.ReadInt32(), binary.ReadInt32(), binary.ReadInt32());
        requestId = binary.ReadInt32();
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
        binary.Write(requestId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) || connection == null || !connection.IsServer ||
            Sender == null || !ReferenceEquals(connection.Clients?.ForEntityId(playerId), Sender) || requestId <= 0 ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;

        bool excluded;
        string reason;
        bool manageable = RemoteResourceToggleCommand.TryReadState(
            world, playerId, userId, position, out excluded, out reason);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRemoteResourceStateResultScoped>()
                .Setup(position, manageable, excluded, reason, requestId),
            _attachedToEntityId: playerId);
    }

    public int GetLength() { return 52; }
}

[Preserve]
public sealed class NetPackageRemoteResourceStateResultScoped : NetPackage
{
    private Vector3i position;
    private int requestId;
    private bool manageable;
    private bool excluded;
    private string reason;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRemoteResourceStateResultScoped Setup(
        Vector3i pos, bool canManage, bool isExcluded, string message, int sequence)
    {
        position = pos;
        requestId = sequence;
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
        requestId = reader.ReadInt32();
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
        binary.Write(requestId);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!RemoteResourceToggleCommand.AdmitReply(world, requestId, position)) return;
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
    internal static int ReplyScope { get; private set; }
    private static XUiC_RebirthRemoteResourceButton active;

    private XUiV_Button button;
    private XUiC_LootWindow lootWindow;
    private TileEntity scopedTile;
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
        ReplyScope++;
        RemoteResourceToggleCommand.ClearReplyScope();
        active = this;
        lootWindow = GetParentByType<XUiC_LootWindow>();
        hasPosition = false;
        scopedTile = null;
        nextRefreshAt = 0f;
        SetHidden();
        Refresh(true);
    }

    public override void OnClose()
    {
        ReplyScope++;
        RemoteResourceToggleCommand.ClearReplyScope();
        if (active == this) active = null;
        hasPosition = false;
        scopedTile = null;
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
            !ReferenceEquals(staticLoot, loot) ||
            !RemoteResourceSourcePolicy.IsSupportedStatic(tileEntity, staticLoot))
        {
            hasPosition = false;
            SetHidden();
            return;
        }

        bool changed = !hasPosition || !SamePosition(position, current) || !ReferenceEquals(scopedTile, tileEntity);
        if (changed)
        {
            ReplyScope++;
            RemoteResourceToggleCommand.ClearReplyScope();
        }
        position = current;
        scopedTile = tileEntity;
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
