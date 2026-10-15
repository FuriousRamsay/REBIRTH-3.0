using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Scripting;
using Rebirth.Vehicles.Camper;

public static class RebirthVehicleSeatSwitch
{
    private static float nextLocalRequest;
    private sealed class RequestGate { internal float Next; }
    private static readonly ConditionalWeakTable<EntityPlayer, RequestGate> ServerGates = new ConditionalWeakTable<EntityPlayer, RequestGate>();

    public static void Poll(PlayerMoveController controller)
    {
        if (controller == null || !controller.bAllowPlayerInput) return;
        Poll(controller.entityPlayerLocal);
    }

    // Called by the existing native REBIRTH controls dispatcher; no extra frame patch.
    public static void Poll(EntityPlayerLocal player)
    {
        var actions = RebirthNativeControls.Actions;
        if (GameManager.IsDedicatedServer || actions == null || !actions.Enabled
            || actions.SwitchVehicleSeat == null || !actions.SwitchVehicleSeat.WasPressed
            || player == null || player.IsDead() || player.PlayerUI?.windowManager == null
            || player.PlayerUI.windowManager.IsInputActive() || player.PlayerUI.windowManager.IsModalWindowOpen()
            || Time.unscaledTime < nextLocalRequest) return;
        var vehicle = player.AttachedToEntity as EntityVehicle;
        if (!Member(vehicle, player)) return;
        nextLocalRequest = Time.unscaledTime + .4f;
        int from = vehicle.FindAttachSlot(player);
        if (vehicle.world.IsRemote())
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                NetPackageManager.GetPackage<NetPackageRebirthVehicleSeatRequest>().Setup(player.entityId, vehicle.entityId, from), false);
        else AuthoritativeRequest(vehicle, player, from, Platform.PlatformManager.InternalLocalUserIdentifier);
    }

    internal static bool Member(EntityVehicle vehicle, EntityPlayer player)
        => ThreadManager.IsMainThread() && vehicle?.world != null && vehicle.vehicle != null
            && player != null && !player.IsDead() && ReferenceEquals(player.world, vehicle.world)
            && ReferenceEquals(player.AttachedToEntity, vehicle) && vehicle.FindAttachSlot(player) >= 0
            && ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId), vehicle)
            && ReferenceEquals(vehicle.world.GetEntity(player.entityId), player);

    internal static bool AuthoritativeRequest(EntityVehicle vehicle, EntityPlayer player, int expectedSlot, PlatformUserIdentifierAbs user)
    {
        if (!Member(vehicle, player) || vehicle.world.IsRemote()
            || vehicle.FindAttachSlot(player) != expectedSlot) return false;
        var gate = ServerGates.GetValue(player, ignored => new RequestGate());
        if (Time.unscaledTime < gate.Next) return false;
        // Native drive permission protects a locked/owned driver's seat; existing
        // passengers can still move among free passenger positions.
        bool mayDrive = vehicle.isDriveable() && (!vehicle.IsLocked() || !vehicle.hasLock()
            || (user != null && ((vehicle.vehicle.OwnerId != null && vehicle.vehicle.OwnerId.Equals(user))
                || vehicle.isAllowedUser(user))));
        int destination = RebirthVehicleSeatCycle.Next(vehicle.GetAttachMaxCount(), expectedSlot,
            slot => vehicle.GetAttached(slot) != null, mayDrive);
        if (destination < 0 || !Transfer(vehicle, player, expectedSlot, destination)) return false;
        gate.Next = Time.unscaledTime + .4f;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthVehicleSeatResult>().Setup(player.entityId, vehicle.entityId, expectedSlot, destination),
            false, -1, -1, -1, null, 192, false);
        vehicle.SendSyncData(1); // native attachment snapshot for later observers/save replication
        return true;
    }

    internal static bool Transfer(EntityVehicle vehicle, EntityPlayer player, int expectedSlot, int destination)
    {
        if (!Member(vehicle, player) || destination < 0 || destination >= vehicle.GetAttachMaxCount()) return false;
        int current = vehicle.FindAttachSlot(player);
        if (current == destination)
        { RebirthCamperBedPoseService.Refresh(player); RebirthCamperCampSeatReturn.NotifySeatChanged(vehicle, player, destination); return true; } // native attach or earlier result already positioned it
        if (current != expectedSlot || vehicle.GetAttached(destination) != null
            || player.RootTransform == null || player.ModelTransform == null || player.m_characterController == null
            || vehicle.vehicleRB == null) return false;
        var target = vehicle.GetAttachedToInfo(destination);
        if (target == null || target.enterParentTransform == null) return false;

        // Native internal reassignment removes only the old seat membership, runs
        // DriverRemoved/SetVehicleDriven, and reinstalls pose, IK, controls and camera.
        // It never calls player.Detach(): AttachedToEntity and the driving activity
        // remain intact, and no exit position or reboarding interaction is involved.
        if (vehicle.AttachEntityToSelf(player, destination) != destination) return false;
        // Exact transform portion of Entity.AttachToEntity; its public entry point
        // refuses an already attached rider and therefore cannot transfer seats.
        player.RootTransform.SetParent(target.enterParentTransform, false);
        player.RootTransform.localPosition = Vector3.zero;
        player.RootTransform.localEulerAngles = Vector3.zero;
        player.ModelTransform.localPosition = target.enterPosition;
        player.ModelTransform.localEulerAngles = target.enterRotation;
        player.rotation = target.enterRotation;
        RebirthCamperBedPoseService.Refresh(player);
        RebirthCamperCampSeatReturn.NotifySeatChanged(vehicle, player, destination);
        return true;
    }
}

[Preserve]
public sealed class NetPackageRebirthVehicleSeatRequest : NetPackage
{
    private int player, vehicle, from;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;
    public NetPackageRebirthVehicleSeatRequest Setup(int playerId, int vehicleId, int expectedSlot)
    { player = playerId; vehicle = vehicleId; from = expectedSlot; return this; }
    public override void read(PooledBinaryReader reader)
    { player = reader.ReadInt32(); vehicle = reader.ReadInt32(); from = reader.ReadInt32(); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); writer.Write(player); writer.Write(vehicle); writer.Write(from); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || !ValidEntityIdForSender(player)
            || Sender == null || !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(player), Sender)) return;
        RebirthVehicleSeatSwitch.AuthoritativeRequest(world.GetEntity(vehicle) as EntityVehicle, world.GetEntity(player) as EntityPlayer, from, Sender.InternalId);
    }
    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthVehicleSeatResult : NetPackage
{
    private int player, vehicle, from, destination;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthVehicleSeatResult Setup(int playerId, int vehicleId, int expectedSlot, int seat)
    { player = playerId; vehicle = vehicleId; from = expectedSlot; destination = seat; return this; }
    public override void read(PooledBinaryReader reader)
    { player = reader.ReadInt32(); vehicle = reader.ReadInt32(); from = reader.ReadInt32(); destination = reader.ReadInt32(); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); writer.Write(player); writer.Write(vehicle); writer.Write(from); writer.Write(destination); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!ThreadManager.IsMainThread() || world == null || !world.IsRemote()) return;
        RebirthVehicleSeatSwitch.Transfer(world.GetEntity(vehicle) as EntityVehicle, world.GetEntity(player) as EntityPlayer, from, destination);
    }
    public int GetLength() => 0;
}
