using System;
using System.IO;
using System.Text;
using UnityEngine.Scripting;

public static partial class RebirthCamperRemoteStationTransport
{
    internal const byte QuerySession=0,OpenSession=1,StationCommand=2,CloseSession=3,FuelSwitch=4;
    // Presentation subscription belongs to the remote picker/window coordinator.
    // Installing command handlers alone never exposes an incomplete grid UI.
    internal static Action<EntityVehicle,Guid,RebirthCamperTransferSnapshot,byte,bool> ReceiveSnapshot;

    internal static void HandleSession(World world,ClientInfo sender,int playerId,int vehicleId,
        Guid token,Guid vehicleIdentity,Guid installation,byte action,RebirthCamperRemoteStationCommand request,bool burning=false)
    {
        if(!ThreadManager.IsMainThread() || !IsRegistered || world==null || world.IsRemote()
            || sender==null || sender.entityId!=playerId || token==Guid.Empty)return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        var vehicle=world.GetEntity(vehicleId) as EntityVehicle;
        try
        {
            string creation=player==null?null:creationProvider(player);
            if(string.IsNullOrWhiteSpace(creation) || !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.ForEntityId(playerId),sender)
                || !RebirthCamperRemoteAdmission.CanAccess(vehicle,player,sender,()=>creationProvider(player)==creation))
                throw new InvalidOperationException("Current remote station access required.");
            var runtime=RebirthCamperModuleRuntime.For(vehicle);
            if(runtime.TransferPending)throw new InvalidOperationException("Resolve the pending camper transfer first.");
            bool accepted=true;
            if(action==OpenSession)Admit(vehicle,player,sender,new RebirthCamperStationAddress(vehicleIdentity,installation),token);
            else if(action==StationCommand)accepted=Execute(player,token,request);
            else if(action==CloseSession)
            {
                if(leases.TryGetValue(playerId,out var closing) && closing.Token==token)
                {
                    var state=runtime.Stations.Find(closing.Address.Installation)?.State;
                    if(RebirthCamperStationGridCustody.IsHeld(state))
                        throw new InvalidOperationException("Station save custody is pending.");
                    // Confirm native vehicle publication before acknowledging
                    // release. A failed publication keeps this exact lease.
                    runtime.Persist(true,4);
                }
                Release(player,token);
            }
            else if(action==FuelSwitch)
            {
                if(!leases.TryGetValue(playerId,out var lease) || !ReferenceEquals(lease.Player,player)
                    || lease.Token!=token || !Current(lease))throw new InvalidOperationException("Current station fuel session required.");
                var state=runtime.Stations.Find(lease.Address.Installation).State;
                if(RebirthCamperStationGridCustody.IsHeld(state))throw new InvalidOperationException("Station save custody is pending.");
                state.IsBurning=burning;state.ResetTickTime();runtime.Persist(true,4);
            }
            else if(action!=QuerySession)throw new InvalidOperationException("Unknown station session action.");
            byte[] snapshot=RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,vehicle.bag.ItemGrid.items,ItemStack.Empty);
            RebirthCamperRemoteTransport.AssertFrame(snapshot);
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                NetPackageManager.GetPackage<NetPackageRebirthCamperStationSessionResult>().Setup(vehicleId,token,runtime.Identity,action,accepted,snapshot),
                _attachedToEntityId:playerId);
        }
        catch(Exception error)
        {
            Log.Warning("[CamperStation] Remote session retained: "+error.Message);
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                NetPackageManager.GetPackage<NetPackageRebirthCamperStationSessionResult>().Setup(vehicleId,token,Guid.Empty,action,false,Array.Empty<byte>()),
                _attachedToEntityId:playerId);
        }
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperStationSession:NetPackage
{
    private int player,vehicle;private Guid token,vehicleIdentity,installation;private byte action;
    private RebirthCamperRemoteStationCommand command;private bool burning;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthCamperStationSession Setup(int p,int v,Guid t,Guid vi,Guid si,byte a,RebirthCamperRemoteStationCommand c=null,bool burn=false)
    {player=p;vehicle=v;token=t;vehicleIdentity=vi;installation=si;action=a;command=c;burning=burn;return this;}
    public override void read(PooledBinaryReader r)
    {
        player=r.ReadInt32();vehicle=r.ReadInt32();token=new Guid(r.ReadBytes(16));vehicleIdentity=new Guid(r.ReadBytes(16));
        installation=new Guid(r.ReadBytes(16));action=r.ReadByte();burning=r.ReadBoolean();
        if(action==RebirthCamperRemoteStationTransport.StationCommand)
        {
            var kind=(RebirthCamperRemoteStationCommandKind)r.ReadByte();var op=new Guid(r.ReadBytes(16));var job=new Guid(r.ReadBytes(16));
            int length=r.ReadUInt16();if(length>1024)throw new InvalidDataException("Oversized recipe name.");
            byte[] bytes=r.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
            string recipe=new UTF8Encoding(false,true).GetString(bytes);
            command=new RebirthCamperRemoteStationCommand(kind,op,job,recipe,r.ReadInt32(),r.ReadInt32());
        }
    }
    public override void write(PooledBinaryWriter w)
    {
        base.write(w);w.Write(player);w.Write(vehicle);w.Write(token.ToByteArray());w.Write(vehicleIdentity.ToByteArray());w.Write(installation.ToByteArray());w.Write(action);w.Write(burning);
        if(action==RebirthCamperRemoteStationTransport.StationCommand)
        {
            if(command==null)throw new InvalidOperationException("Station command required.");
            byte[] recipe=Encoding.UTF8.GetBytes(command.RecipeName);
            w.Write((byte)command.Kind);w.Write(command.Operation.ToByteArray());w.Write(command.Job.ToByteArray());
            w.Write((ushort)recipe.Length);w.Write(recipe);w.Write(command.Batches);w.Write(command.Tier);
        }
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world!=null && !world.IsRemote() && ValidEntityIdForSender(player))
        RebirthCamperRemoteStationTransport.HandleSession(world,Sender,player,vehicle,token,vehicleIdentity,installation,action,command,burning);}
    public int GetLength()=>0;
}

[Preserve]
public sealed class NetPackageRebirthCamperStationSessionResult:NetPackage
{
    private int vehicle;private Guid token,identity;private byte action;private bool accepted;private byte[] snapshot;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthCamperStationSessionResult Setup(int v,Guid t,Guid i,byte a,bool ok,byte[] state)
    {vehicle=v;token=t;identity=i;action=a;accepted=ok;snapshot=state;return this;}
    public override void read(PooledBinaryReader r)
    {vehicle=r.ReadInt32();token=new Guid(r.ReadBytes(16));identity=new Guid(r.ReadBytes(16));action=r.ReadByte();accepted=r.ReadBoolean();snapshot=CamperFrame.Read(r);}
    public override void write(PooledBinaryWriter w)
    {base.write(w);w.Write(vehicle);w.Write(token.ToByteArray());w.Write(identity.ToByteArray());w.Write(action);w.Write(accepted);CamperFrame.Write(w,snapshot);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread() || world==null || !world.IsRemote())return;
        var live=world.GetEntity(vehicle) as EntityVehicle;
        if(live==null)return;
        var state=identity==Guid.Empty?null:RebirthCamperTransferSnapshot.Restore(identity,snapshot);
        RebirthCamperRemoteStationTransport.ReceiveSnapshot?.Invoke(live,token,state,action,accepted);
    }
    public int GetLength()=>0;
}
