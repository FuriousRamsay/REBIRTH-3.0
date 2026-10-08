using HarmonyLib;
using Platform;
using System;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Persistent custom drone naming. The name is stored on EntityDrone.OriginalItemValue,
/// the same entity-owned item value that 3.1 serializes to disk, syncs with flag 128,
/// and preserves through pickup/redeployment.
/// </summary>
public static class RebirthDroneRenameService
{
    internal const string MetadataKey = "RebirthDroneName";
    private const int MaxNameLength = 32;

    public static string Normalize(string value)
    {
        string result = (value ?? string.Empty).Trim();
        if (result.Length > MaxNameLength) result = result.Substring(0, MaxNameLength).Trim();
        return result;
    }

    public static bool TryGetPersistedName(EntityDrone drone, out string name)
    {
        name = string.Empty;
        if (drone == null || drone.OriginalItemValue == null) return false;
        string stored;
        if (!drone.OriginalItemValue.TryGetMetadata(MetadataKey, out stored)) return false;
        stored = Normalize(stored);
        if (string.IsNullOrEmpty(stored)) return false;
        name = stored;
        return true;
    }

    public static string GetDisplayName(EntityDrone drone)
    {
        string name;
        return TryGetPersistedName(drone, out name) ? name : Localization.Get("xuiRebirthDrone");
    }

    public static void ApplyPersistedName(EntityDrone drone)
    {
        string name;
        if (drone == null || !TryGetPersistedName(drone, out name)) return;
        if (!string.Equals(drone.EntityName, name, StringComparison.Ordinal))
            drone.SetEntityName(name);
    }

    public static void Request(EntityPlayerLocal player, int droneEntityId, string requestedName)
    {
        if (player == null) return;
        string name = Normalize(requestedName);
        if (string.IsNullOrEmpty(name)) return;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentLocalPlayer();
        if (persistent?.PrimaryId == null) return;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return;
        if (connection.IsServer)
            ProcessServerRequest(GameManager.Instance?.World, player.entityId, persistent.PrimaryId, droneEntityId, name);
        else
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthDroneRenameRequest>()
                .Setup(player.entityId, persistent.PrimaryId, droneEntityId, name));
    }

    internal static bool ProcessServerRequest(
        World world,
        int playerEntityId,
        PlatformUserIdentifierAbs userId,
        int droneEntityId,
        string requestedName)
    {
        if (world == null || world.IsRemote() || userId == null) return false;
        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(playerEntityId);
        EntityDrone drone = world.GetEntity(droneEntityId) as EntityDrone;
        string name = Normalize(requestedName);
        if (player == null || persistent?.PrimaryId == null || !persistent.PrimaryId.Equals(userId) ||
            drone == null || drone.IsDead() || drone.belongsPlayerId != playerEntityId ||
            drone.OriginalItemValue == null || string.IsNullOrEmpty(name))
            return false;

        drone.OriginalItemValue.SetMetadata(MetadataKey, name);
        drone.SetEntityName(name);
        drone.SendSyncData((ushort)128);
        return true;
    }
}

[Preserve]
public sealed class NetPackageRebirthDroneRenameRequest : NetPackage
{
    private int playerEntityId;
    private PlatformUserIdentifierAbs userId;
    private int droneEntityId;
    private string requestedName;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthDroneRenameRequest Setup(
        int playerId,
        PlatformUserIdentifierAbs persistentUserId,
        int droneId,
        string name)
    {
        playerEntityId = playerId;
        userId = persistentUserId;
        droneEntityId = droneId;
        requestedName = name ?? string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader br = (BinaryReader)reader;
        playerEntityId = br.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(br);
        droneEntityId = br.ReadInt32();
        requestedName = br.ReadString();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter bw = (BinaryWriter)writer;
        bw.Write(playerEntityId);
        userId.ToStream(bw);
        bw.Write(droneEntityId);
        bw.Write(requestedName ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || userId == null ||
            !ValidEntityIdForSender(playerEntityId) || !ValidUserIdForSender(userId))
            return;
        RebirthDroneRenameService.ProcessServerRequest(world, playerEntityId, userId, droneEntityId, requestedName);
    }

    public int GetLength() => 0;
}

/// <summary>Add Rename to the focused owner's normal drone radial menu.</summary>
[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.InitLocalActivationCommands))]
internal static class RebirthDroneRenameActivationCommandsPatch
{
    private static void Postfix(EntityDrone __instance, Action<EntityActivationCommand> _addCallback)
    {
        EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (__instance == null || _addCallback == null || player == null ||
            __instance.IsDead() || __instance.belongsPlayerId != player.entityId)
            return;
        _addCallback(new EntityActivationCommand("rebirth_drone_rename", "pen", _customCommandText: "rebirth_drone_rename"));
    }
}

/// <summary>
/// Open the Companions Stats/Rename UI directly instead of entering vanilla drone
/// LockManager interaction, so Rename does not tear down the management window.
/// </summary>
[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.OnEntityActivated))]
internal static class RebirthDroneRenameActivationPatch
{
    private static bool Prefix(EntityDrone __instance, EntityActivationCommand _command, EntityPlayerLocal _playerFocusing)
    {
        if (!string.Equals(_command.commandId, "rebirth_drone_rename", StringComparison.Ordinal)) return true;
        if (__instance == null || _playerFocusing == null || __instance.IsDead() ||
            __instance.belongsPlayerId != _playerFocusing.entityId || _playerFocusing.PlayerUI?.xui == null)
            return false;

        RebirthCompanionUiService.Open(
            _playerFocusing.PlayerUI.xui,
            "D:" + __instance.entityId,
            "Rename");
        return false;
    }
}

[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.OnAddedToWorld))]
internal static class RebirthDronePersistedNameAddedPatch
{
    private static void Postfix(EntityDrone __instance)
    {
        RebirthDroneRenameService.ApplyPersistedName(__instance);
    }
}

[HarmonyPatch(typeof(EntityDrone), nameof(EntityDrone.ReadSyncData))]
internal static class RebirthDronePersistedNameSyncPatch
{
    private static void Postfix(EntityDrone __instance, ushort syncFlags)
    {
        if (__instance != null && (syncFlags & 128) != 0)
            RebirthDroneRenameService.ApplyPersistedName(__instance);
    }
}
