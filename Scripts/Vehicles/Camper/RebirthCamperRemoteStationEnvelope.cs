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
    internal static Action<EntityVehicle,Guid,RebirthCamperTransferSnapshot,byte,Guid,bool> ReceiveCommandSnapshot;

    internal static void HandleSession(World world,ClientInfo sender,int playerId,int vehicleId,
        Guid token,Guid vehicleIdentity,Guid installation,byte action,RebirthCamperRemoteStationCommand request,bool burning=false,Guid actionOperation=default(Guid))
    {
        var manager=GameManager.Instance;var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread() || !IsRegistered || world==null || world.IsRemote()
            || !ReferenceEquals(manager?.World,world)||connection?.IsServer!=true
            || sender?.loginDone!=true||sender.disconnecting||sender.entityId!=playerId||token==Guid.Empty
            || !ReferenceEquals(connection.Clients?.ForEntityId(playerId),sender)
            || sender.netConnection==null||sender.netConnection.Length==0||sender.netConnection[0]==null||sender.netConnection[0].IsDisconnected()
            || action>FuelSwitch||action==StationCommand&&request==null||action==FuelSwitch&&actionOperation==Guid.Empty)return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        var vehicle=world.GetEntity(vehicleId) as EntityVehicle;
        var native=sender.netConnection[0];var worldState=world.worldState;string worldGuid=world.worldState?.Guid;
        int deaths=player?.Died??-1;string creation=null;
        bool CurrentRequest()
        {
            try{return ReferenceEquals(GameManager.Instance,manager)&&ReferenceEquals(manager.World,world)
                &&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,connection)&&connection.IsServer
                &&ReferenceEquals(world.worldState,worldState)&&world.worldState?.Guid==worldGuid
                &&player!=null&&!player.IsDead()&&player.Died==deaths&&ReferenceEquals(player.world,world)
                &&ReferenceEquals(world.GetEntity(playerId),player)&&vehicle!=null&&ReferenceEquals(vehicle.world,world)&&ReferenceEquals(world.GetEntity(vehicleId),vehicle)
                &&sender.loginDone&&!sender.disconnecting&&ReferenceEquals(connection.Clients?.ForEntityId(playerId),sender)
                &&sender.netConnection!=null&&sender.netConnection.Length>0&&ReferenceEquals(sender.netConnection[0],native)&&!native.IsDisconnected()
                &&creation!=null&&creationProvider(player)==creation;}
            catch{return false;}
        }
        try
        {
            creation=player==null?null:creationProvider(player);
            if(string.IsNullOrWhiteSpace(creation) || !CurrentRequest()
                || !RebirthCamperRemoteAdmission.CanAccess(vehicle,player,sender,()=>creationProvider(player)==creation))
                throw new InvalidOperationException("Current remote station access required.");
            var runtime=RebirthCamperModuleRuntime.For(vehicle);
            if(runtime.TransferPending)throw new InvalidOperationException("Resolve the pending camper transfer first.");
            bool accepted=true;
            if(action==OpenSession)Admit(vehicle,player,sender,new RebirthCamperStationAddress(vehicleIdentity,installation),token);
            else if(action==StationCommand)
            {
                if(!leases.TryGetValue(playerId,out var commandLease)||!ReferenceEquals(commandLease.Player,player)
                    ||!ReferenceEquals(commandLease.Vehicle,vehicle)||commandLease.Token!=token
                    ||commandLease.Address.Vehicle!=vehicleIdentity||commandLease.Address.Installation!=installation||!Current(commandLease))
                    throw new InvalidOperationException("Current exact station command session required.");
                accepted=Execute(player,token,request);
            }
            else if(action==CloseSession)
            {
                if(leases.TryGetValue(playerId,out var closing) && closing.Token==token)
                {
                    if(!ReferenceEquals(closing.Player,player)||!ReferenceEquals(closing.Vehicle,vehicle)
                        ||closing.Address.Vehicle!=vehicleIdentity||closing.Address.Installation!=installation||!Current(closing))
                        throw new InvalidOperationException("Current exact station close session required.");
                    var state=runtime.Stations.Find(closing.Address.Installation)?.State;
                    if(RebirthCamperStationGridCustody.IsHeld(state))
                        throw new InvalidOperationException("Station save custody is pending.");
                    // Flush metadata before presentation release. This reply
                    // never settles paid originals: their independent saved
                    // evidence and custody remain authoritative.
                    runtime.Persist(true,4);
                }
                Release(player,token);
            }
            else if(action==FuelSwitch)
            {
                if(!leases.TryGetValue(playerId,out var lease) || !ReferenceEquals(lease.Player,player)
                    || !ReferenceEquals(lease.Vehicle,vehicle)||lease.Address.Vehicle!=vehicleIdentity||lease.Address.Installation!=installation
                    || lease.Token!=token || !Current(lease))throw new InvalidOperationException("Current station fuel session required.");
                accepted=Execute(player,token,RebirthCamperRemoteStationCommand.Fuel(actionOperation,burning));
            }
            else if(action!=QuerySession)throw new InvalidOperationException("Unknown station session action.");
            Guid snapshotIdentity=runtime.Identity;
            if(!CurrentRequest())return;
            byte[] snapshot=RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,vehicle.bag.ItemGrid.items,ItemStack.Empty);
            RebirthCamperRemoteTransport.AssertFrame(snapshot);
            if(!CurrentRequest()||runtime.Identity!=snapshotIdentity||!ReferenceEquals(RebirthCamperModuleRuntime.For(vehicle),runtime))return;
            SendSessionReply(playerId,vehicleId,token,runtime.Identity,action,request,actionOperation,accepted,snapshot);
        }
        catch(Exception error)
        {
            Log.Warning("[CamperStation] Remote session retained: "+error.Message);
            if(CurrentRequest())SendSessionReply(playerId,vehicleId,token,Guid.Empty,action,request,actionOperation,false,Array.Empty<byte>());
        }
    }
    private static void SendSessionReply(int player,int vehicle,Guid token,Guid identity,byte action,RebirthCamperRemoteStationCommand command,Guid actionOperation,bool accepted,byte[] snapshot)
    {
        Guid operation=action==StationCommand&&command!=null?command.Operation:actionOperation;
        NetPackage reply=operation!=Guid.Empty&&(action==StationCommand||action==FuelSwitch)
            ?(NetPackage)NetPackageManager.GetPackage<NetPackageRebirthCamperStationCommandResultV2>().Setup(vehicle,token,identity,operation,accepted,snapshot,action)
            :NetPackageManager.GetPackage<NetPackageRebirthCamperStationSessionResult>().Setup(vehicle,token,identity,action,accepted,snapshot);
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(reply,_attachedToEntityId:player);
    }
}

[Preserve]
public sealed class NetPackageRebirthCamperStationSession:NetPackage,IMemoryPoolableObject
{
    private int player,vehicle;private Guid token,vehicleIdentity,installation;private byte action;
    private RebirthCamperRemoteStationCommand command;private bool burning;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthCamperStationSession Setup(int p,int v,Guid t,Guid vi,Guid si,byte a,RebirthCamperRemoteStationCommand c=null,bool burn=false)
    {player=p;vehicle=v;token=t;vehicleIdentity=vi;installation=si;action=a;command=c;burning=burn;return this;}
    public override void read(PooledBinaryReader r)
    {
        command=null; // A pooled non-command frame must not retain an earlier recipe.
        player=r.ReadInt32();vehicle=r.ReadInt32();token=new Guid(r.ReadBytes(16));vehicleIdentity=new Guid(r.ReadBytes(16));
        installation=new Guid(r.ReadBytes(16));action=r.ReadByte();burning=r.ReadBoolean();
        if(action>RebirthCamperRemoteStationTransport.CloseSession)throw new InvalidDataException("Correlated fuel requests require V2.");
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
    {if(ThreadManager.IsMainThread()&&ReferenceEquals(callbacks,GameManager.Instance)&&ReferenceEquals(world,callbacks?.World)
        &&world!=null && !world.IsRemote() && ValidEntityIdForSender(player))
        RebirthCamperRemoteStationTransport.HandleSession(world,Sender,player,vehicle,token,vehicleIdentity,installation,action,command,burning);}
    public int GetLength()=>0;
    public void Reset(){player=vehicle=0;token=vehicleIdentity=installation=Guid.Empty;action=0;command=null;burning=false;Sender=null;}
    public void Cleanup()=>Reset();
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
