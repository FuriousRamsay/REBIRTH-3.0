using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Rebirth.Vehicles.Authoring;

public sealed class ConsoleCmdSeatPoseEditor : ConsoleCmdAbstract
{
    public override string[] getCommands() => new[] { "rbseatpose" };
    public override string getDescription() => "Open the local vehicle seat pose authoring panel.";
    public override string getHelp() => "rbseatpose open [vehicleEntityId] [seat]\nrbseatpose close|status\nDebug authoring only; local host/single-player. Saved profiles do not change production vehicle definitions.";
    public override void Execute(List<string> args, CommandSenderInfo senderInfo)
    {
        try
        {
            if (GameManager.IsDedicatedServer || senderInfo.RemoteClientInfo != null) throw new InvalidOperationException("Run locally on the host client.");
            var world = GameManager.Instance?.World;
            var player = world?.GetPrimaryPlayer() as EntityPlayerLocal;
            if (!player || player.IsDead() || world.IsRemote()) throw new InvalidOperationException("This candidate requires a live local host player; remote authoring is not implemented.");
            var old = player.GetComponent<SeatPoseSession>();
            if(args.Count>0&&args[0]=="status")
            {if(!old||!old.IsReady)throw new InvalidOperationException("No ready authoring session.");SdtdConsole.Instance.Output("Seat editor: READY");return;}
            if (args.Count > 0 && args[0] == "close") { if (old) old.Close(); return; }
            if(args.Count==0||args[0]!="open")throw new ArgumentException(getHelp());
            var vehicle = player.AttachedToEntity as EntityVehicle;
            if (args.Count > 1) vehicle = world.GetEntity(int.Parse(args[1])) as EntityVehicle;
            if (!vehicle || !vehicle.vehicleRB || !vehicle.ModelTransform || vehicle.IsDead()) throw new InvalidOperationException("Select a valid vehicle entity or enter a vehicle first.");
            if (vehicle.vehicleRB.velocity.sqrMagnitude > .01f) throw new InvalidOperationException("Park the vehicle before editing.");
            if ((vehicle.ModelTransform.position - player.position).sqrMagnitude > 100) throw new InvalidOperationException("Vehicle must be within 10 metres.");
            var user = Platform.PlatformManager.InternalLocalUserIdentifier;
            bool allowed = !vehicle.IsLocked() || !vehicle.hasLock() || (user != null && (vehicle.isAllowedUser(user) || vehicle.vehicle.OwnerId != null && vehicle.vehicle.OwnerId.Equals(user)));
            if (!allowed) throw new InvalidOperationException("Vehicle access denied.");
            int seat = args.Count > 2 ? int.Parse(args[2]) : vehicle.FindAttachSlot(player);
            if (seat < 0) seat = 0;
            if (old) old.Close();
            Open(player, vehicle, seat);
        }
        catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR: Seat editor: " + error.Message); }
    }
    static void Open(EntityPlayerLocal player, EntityVehicle vehicle, int seat)
    {
        if (seat < 0 || seat >= vehicle.GetAttachMaxCount()) throw new InvalidOperationException("Seat index out of range.");
        var occupied = vehicle.GetAttached(seat);
        if (occupied != null && occupied != player) throw new InvalidOperationException("That seat is occupied.");
        if (player.AttachedToEntity != null && player.AttachedToEntity != vehicle) throw new InvalidOperationException("Exit the other vehicle first.");
        if (player.AttachedToEntity == vehicle && vehicle.FindAttachSlot(player) != seat)
        {
            var info = vehicle.GetAttachedToInfo(seat);
            if (info?.enterParentTransform == null) throw new InvalidOperationException("Native seat anchor unavailable.");
            if (vehicle.AttachEntityToSelf(player, seat) != seat) throw new InvalidOperationException("Native seat transfer failed.");
            player.RootTransform.SetParent(info.enterParentTransform, false);
            player.RootTransform.localPosition = Vector3.zero; player.RootTransform.localRotation = Quaternion.identity;
            player.ModelTransform.localPosition = info.enterPosition; player.ModelTransform.localEulerAngles = info.enterRotation; player.rotation = info.enterRotation;
        }
        else if (player.AttachedToEntity != vehicle && player.AttachToEntity(vehicle, seat) != seat) throw new InvalidOperationException("Native attachment failed.");
        vehicle.SendSyncData(1);
        var session = player.gameObject.AddComponent<SeatPoseSession>();
        session.RequestSeat = destination =>
        {
            if (vehicle.GetAttached(destination) != null && vehicle.GetAttached(destination) != player) { SdtdConsole.Instance.Output("Seat editor: seat occupied."); return; }
            session.Close();
            try { Open(player, vehicle, destination); } catch (Exception error) { SdtdConsole.Instance.Output("Seat editor: " + error.Message); }
        };
        try { session.Open(player, vehicle, Path.Combine(Application.persistentDataPath, "REBIRTH", "SeatPoseProfiles")); }
        catch { session.Close(); throw; }
    }
}
