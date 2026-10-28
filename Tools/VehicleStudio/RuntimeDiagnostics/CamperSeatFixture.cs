using System;
using System.Collections.Generic;
using Platform;

namespace Rebirth.VehicleStudio.Diagnostics
{
    public sealed class ConsoleCmdCamperSeatFixture : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperseat" };
        public override string getDescription() => "Seat the local test player in an existing camper slot using native attachment.";
        public override string getHelp() => "camperseat ENTITY_ID SLOT: CodexTest only, stopped camper, free configured slot. Capture camperdiag and screenshots afterward.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                if (GameManager.IsDedicatedServer || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest" || args.Count != 2)
                    throw new Exception(getHelp());
                var world = GameManager.Instance.World;
                var player = world.GetPrimaryPlayer();
                var camper = world.GetEntity(int.Parse(args[0])) as EntityVehicle;
                int slot = int.Parse(args[1]);
                if (player == null || camper == null || EntityClass.list[camper.entityClass].entityClassName != "rebirthCamper80s")
                    throw new Exception("Local player and exact camper required.");
                if (slot < 0 || slot >= camper.GetAttachMaxCount() || camper.vehicleRB == null || camper.vehicleRB.velocity.magnitude > .1f)
                    throw new Exception("A stopped camper and configured slot are required.");
                if (player.AttachedToEntity != camper && (player.GetPosition() - camper.GetPosition()).sqrMagnitude > 144f)
                    throw new Exception("Approach the grounded camper first; remote or airborne seating is refused.");
                if (camper.GetAttached(slot) != null && camper.GetAttached(slot) != player)
                    throw new Exception("The requested seat is occupied.");
                if (player.AttachedToEntity != null && player.AttachedToEntity != camper)
                    throw new Exception("Leave the other vehicle first.");
                if (camper.GetAttached(slot) != player)
                {
                    if (player.AttachedToEntity == camper) player.Detach();
                    int actual = player.AttachToEntity(camper, slot);
                    if (actual != slot || camper.GetAttached(slot) != player)
                        throw new Exception("Native attachment did not confirm the requested seat; inspect current state.");
                }
                SdtdConsole.Instance.Output("[CamperSeat] ATTACHED slot=" + slot + "; nativePose=" + camper.vehicle.GetSeatPose(slot)
                    + "; fixture only, visual fit remains unqualified.");
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperSeat] " + error.Message); }
        }
    }
}
