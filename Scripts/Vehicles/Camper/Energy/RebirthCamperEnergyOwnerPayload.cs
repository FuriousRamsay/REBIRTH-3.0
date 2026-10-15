using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal static class RebirthCamperEnergyOwnerPayload
{
    internal enum Stage:byte {Prepare=0,Apply=1,Retire=2}
    private const uint Magic=0x31504E45;
    internal static Guid Effect(Guid original,Stage stage)
    {
        if(original==Guid.Empty||!Enum.IsDefined(typeof(Stage),stage))throw new ArgumentException("Exact energy operation required.");
        if(stage==Stage.Apply)return original;
        using(var hash=SHA256.Create())
        {var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes("REBIRTH.energy.owner."+stage+"."+original.ToString("N")));var id=new byte[16];Array.Copy(bytes,id,16);return new Guid(id);}
    }
    internal static byte[] Write(RebirthCamperEnergyTransferFrame frame,Stage stage,Guid scope)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {writer.Write(Magic);writer.Write((byte)stage);writer.Write(scope.ToByteArray());CamperFrame.Write(writer,frame.Write(false));CamperFrame.Write(writer,frame.Write(true));writer.Flush();return stream.ToArray();}
    }
    private static bool Read(byte[] payload,Guid effect,out RebirthCamperEnergyTransferFrame frame,out Stage stage,out Guid scope)
    {
        frame=null;stage=Stage.Prepare;scope=Guid.Empty;
        try
        {
            if(effect==Guid.Empty||payload==null||payload.Length>8*1024*1024)return false;
            using(var stream=new MemoryStream(payload,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
            {
                if(reader.ReadUInt32()!=Magic)return false;stage=(Stage)reader.ReadByte();if(!Enum.IsDefined(typeof(Stage),stage))return false;
                scope=new Guid(reader.ReadBytes(16));if(scope==Guid.Empty)return false;
                byte[] before=CamperFrame.Read(reader),after=CamperFrame.Read(reader);if(stream.Position!=stream.Length)return false;
                // Read the bounded immutable header solely to reconstruct the
                // RCJ parser input; its parser validates both complete images.
                Guid operation,vehicle;string creation;long revision;
                using(var side=new MemoryStream(before,false))using(var header=new BinaryReader(side,Encoding.UTF8,true))
                {
                    header.ReadUInt32();operation=new Guid(header.ReadBytes(16));vehicle=new Guid(header.ReadBytes(16));header.ReadInt32();
                    header.ReadString();header.ReadString();creation=header.ReadString();header.ReadBoolean();revision=header.ReadInt64();
                }
                if(Effect(operation,stage)!=effect)return false;
                frame=RebirthCamperEnergyTransferFrame.Read(new RebirthCamperTransferJournal.Record(operation,vehicle,creation,revision,
                    RebirthCamperTransferJournal.Phase.Prepared,before,after));return true;
            }
        }
        catch{return false;}
    }
    internal static bool TryApply(EntityPlayerLocal player,Guid operation,byte[] payload)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player?.world==null||!player.world.IsRemote()||player.IsDead()||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                !Read(payload,operation,out var frame,out var stage,out var scope)||frame.Actor!=player.entityId||
                frame.Survivor!=RebirthSurvivorMode.IsEnabledForCurrentWorld()||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var owner)||frame.Owner!=owner.CanonicalId||frame.OwnerKey!=owner.StorageKey||
                RebirthCamperCharacterAccess.CurrentCreation(player)!=frame.Creation)return false;
            var vehicle=player.PlayerUI?.xui?.Vehicle?.CurrentVehicle;
            string vehicleId;
            if(stage!=Stage.Retire&&(!RebirthCamperEnergyAccess.IsCurrentOwnerScope(scope,frame.Vehicle,player)||vehicle==null||!RebirthCamperModuleInteraction.Current(vehicle,player)||
                !vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperIdentity",out vehicleId)||
                !Guid.TryParse(vehicleId,out var id)||id!=frame.Vehicle))return false;
            string key="rbEnergy_"+frame.Operation.ToString("N");float phase=player.Buffs.GetCustomVar(key);
            if(stage==Stage.Prepare)
            {
                if((phase!=0f&&phase!=2f)||!RebirthCamperEnergyOwnerHold.Prepare(player,frame)||
                    !frame.Before.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
                player.Buffs.SetCustomVar(key,2f,true);return true;
            }
            if(stage==Stage.Apply)
            {
                if(phase==1f)return frame.After.Matches(RebirthCamperEnergyActorImage.Capture(player));
                if(phase!=2f||!RebirthCamperEnergyOwnerHold.Apply(player,frame))return false;
                player.Buffs.SetCustomVar(key,1f,true);return true;
            }
            if(phase==3f)return true;
            if(phase!=1f||!RebirthCamperEnergyOwnerHold.Retire(player,frame))return false;
            player.Buffs.SetCustomVar(key,3f,true);return true;
        }
        catch{return false;}
    }
    internal static bool VerifySaved(EntityPlayer player,Guid operation,byte[] payload,Func<bool> current)
    {
        try
        {
            return ThreadManager.IsMainThread()&&current!=null&&current()&&Read(payload,operation,out var frame,out var stage,out _)&&
                player?.entityId==frame.Actor&&frame.Survivor==RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
                RebirthStablePlayerIdentity.TryResolveServerEntity(player,out var owner)&&owner.CanonicalId==frame.Owner&&owner.StorageKey==frame.OwnerKey&&
                RebirthCamperCharacterAccess.CurrentCreation(player)==frame.Creation&&
                RebirthCamperEnergySavedOwner.HasSaved(owner,frame.Operation,stage==Stage.Prepare?2:stage==Stage.Apply?1:3,
                    stage==Stage.Prepare?frame.Before:stage==Stage.Apply?frame.After:null)&&current();
        }
        catch{return false;}
    }
}
