using System;
using System.IO;
using System.Text;
using UnityEngine.Scripting;

// Separate status protocol. A query never calls Execute/Handle or authorizes
// financial replay; v2 may reserve an exact never-received original as refused.
[Preserve]
public sealed class NetPackageRebirthCamperStationCommandStatusQuery:NetPackage,IMemoryPoolableObject
{
    private int player,vehicle;private Guid token,identity,installation;
    private RebirthCamperRemoteStationCommand command;
    private bool admissionProtocol;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthCamperStationCommandStatusQuery Setup(int p,int v,Guid t,Guid i,Guid station,RebirthCamperRemoteStationCommand original,bool declaredAdmission=false)
    {player=p;vehicle=v;token=t;identity=i;installation=station;command=original;admissionProtocol=declaredAdmission;return this;}
    public override void write(PooledBinaryWriter writer)
    {
        if(command==null||token==Guid.Empty||identity==Guid.Empty||installation==Guid.Empty)throw new InvalidDataException("Original station status identity required.");
        base.write(writer);writer.Write((byte)2);writer.Write(player);writer.Write(vehicle);writer.Write(token.ToByteArray());writer.Write(identity.ToByteArray());writer.Write(installation.ToByteArray());
        writer.Write((byte)command.Kind);writer.Write(command.Operation.ToByteArray());writer.Write(command.Job.ToByteArray());
        byte[] recipe=new UTF8Encoding(false,true).GetBytes(command.RecipeName);
        if(recipe.Length>1024)throw new InvalidDataException("Station status recipe exceeds bound.");
        writer.Write((ushort)recipe.Length);writer.Write(recipe);writer.Write(command.Batches);writer.Write(command.Tier);writer.Write(admissionProtocol);
    }
    public override void read(PooledBinaryReader reader)
    {
        command=null;admissionProtocol=false;
        byte version=reader.ReadByte();if(version!=1&&version!=2)throw new InvalidDataException("Unsupported station status query.");
        player=reader.ReadInt32();vehicle=reader.ReadInt32();token=new Guid(reader.ReadBytes(16));identity=new Guid(reader.ReadBytes(16));installation=new Guid(reader.ReadBytes(16));
        var kind=(RebirthCamperRemoteStationCommandKind)reader.ReadByte();var operation=new Guid(reader.ReadBytes(16));var job=new Guid(reader.ReadBytes(16));
        int length=reader.ReadUInt16();if(length>1024)throw new InvalidDataException("Station status recipe exceeds bound.");
        byte[] bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();
        var decoded=new RebirthCamperRemoteStationCommand(kind,operation,job,new UTF8Encoding(false,true).GetString(bytes),reader.ReadInt32(),reader.ReadInt32());
        byte declarationValue=version==2?reader.ReadByte():(byte)0;if(declarationValue>1)throw new InvalidDataException("Invalid station admission declaration.");
        bool declaration=declarationValue==1;
        if(token==Guid.Empty||identity==Guid.Empty||installation==Guid.Empty)throw new InvalidDataException("Original station status identity required.");
        command=decoded;admissionProtocol=declaration;
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {RebirthCamperRemoteStationTransport.QueryOriginalStatus(world,Sender,player,vehicle,token,identity,installation,command,admissionProtocol);}
    public int GetLength()=>0;
    public void Reset(){player=vehicle=0;token=identity=installation=Guid.Empty;command=null;admissionProtocol=false;Sender=null;}
    public void Cleanup()=>Reset();
}

[Preserve]
public sealed class NetPackageRebirthCamperStationCommandStatusResult:NetPackage,IMemoryPoolableObject
{
    private int vehicle;private Guid token,identity,installation,operation,job;
    private RebirthCamperRemoteStationCommandKind kind;
    private RebirthCamperRemoteStationCommandStatus.State status;private byte[] snapshot;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthCamperStationCommandStatusResult Setup(int v,Guid t,Guid i,Guid station,RebirthCamperRemoteStationCommand original,RebirthCamperRemoteStationCommandStatus.State result,byte[] state)
    {vehicle=v;token=t;identity=i;installation=station;operation=original.Operation;job=original.Job;kind=original.Kind;status=result;snapshot=state;return this;}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);writer.Write((byte)1);writer.Write(vehicle);writer.Write(token.ToByteArray());writer.Write(identity.ToByteArray());writer.Write(installation.ToByteArray());writer.Write(operation.ToByteArray());writer.Write(job.ToByteArray());writer.Write((byte)kind);writer.Write((byte)status);CamperFrame.Write(writer,snapshot);}
    public override void read(PooledBinaryReader reader)
    {
        snapshot=null;
        if(reader.ReadByte()!=1)throw new InvalidDataException("Unsupported station status result.");
        vehicle=reader.ReadInt32();token=new Guid(reader.ReadBytes(16));identity=new Guid(reader.ReadBytes(16));installation=new Guid(reader.ReadBytes(16));operation=new Guid(reader.ReadBytes(16));job=new Guid(reader.ReadBytes(16));
        kind=(RebirthCamperRemoteStationCommandKind)reader.ReadByte();status=(RebirthCamperRemoteStationCommandStatus.State)reader.ReadByte();
        if(token==Guid.Empty||identity==Guid.Empty||installation==Guid.Empty||operation==Guid.Empty||job==Guid.Empty||
            !Enum.IsDefined(typeof(RebirthCamperRemoteStationCommandKind),kind)||!Enum.IsDefined(typeof(RebirthCamperRemoteStationCommandStatus.State),status))throw new InvalidDataException("Invalid station status correlation.");
        snapshot=CamperFrame.Read(reader);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,world))return;
        var live=world.GetEntity(vehicle) as EntityVehicle;if(live==null)return;
        RebirthCamperTransferSnapshot state=null;
        if(status==RebirthCamperRemoteStationCommandStatus.State.Applied||status==RebirthCamperRemoteStationCommandStatus.State.Rejected)
            state=RebirthCamperTransferSnapshot.Restore(identity,snapshot);
        RebirthCamperRemoteStationClient.ReceiveOriginalStatus(live,token,identity,installation,operation,job,kind,status,state);
    }
    public int GetLength()=>0;
    public void Reset(){vehicle=0;token=identity=installation=operation=job=Guid.Empty;kind=default;status=default;snapshot=null;Sender=null;}
    public void Cleanup()=>Reset();
}

public static partial class RebirthCamperRemoteStationTransport
{
    internal static void QueryOriginalStatus(World world,ClientInfo sender,int playerId,int vehicleId,Guid token,Guid identity,Guid installation,RebirthCamperRemoteStationCommand request,bool admissionProtocol=false)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||sender==null||
            !RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(request)||!leases.TryGetValue(playerId,out var lease)||
            !ReferenceEquals(lease.Sender,sender)||!ReferenceEquals(lease.Player,world.GetEntity(playerId))||!ReferenceEquals(lease.Vehicle,world.GetEntity(vehicleId))||
            lease.Token!=token||lease.Address.Vehicle!=identity||lease.Address.Installation!=installation||!Current(lease))return;
        try
        {
            if(admissionProtocol)RebirthCamperRemoteStationCommandStatusStore.TryRefuseUnreceived(lease.Player,lease.Address,lease.Creation,lease,request);
            var status=RebirthCamperRemoteStationCommandStatus.ReadStatus(lease.Player,lease.Address,lease.Creation,lease,request,out var observed);
            var runtime=RebirthCamperModuleRuntime.For(lease.Vehicle);
            byte[] bytes=Array.Empty<byte>();
            if(status==RebirthCamperRemoteStationCommandStatus.State.Applied||status==RebirthCamperRemoteStationCommandStatus.State.Rejected)
            {
                if(observed?.AuthorizesCommandRetirement!=true)return;
                bytes=RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,lease.Vehicle.bag.ItemGrid.items,ItemStack.Empty);
                RebirthCamperRemoteTransport.AssertFrame(bytes);
            }
            if(!Current(lease)||!ReferenceEquals(RebirthCamperModuleRuntime.For(lease.Vehicle),runtime)||runtime.Identity!=identity)return;
            sender.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthCamperStationCommandStatusResult>().Setup(vehicleId,token,identity,installation,request,status,bytes));
        }
        catch(Exception error){Log.Warning("[CamperStation] Original status retained: "+error.Message);}
    }
}
