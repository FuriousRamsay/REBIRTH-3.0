// Full actual query/result package classes. Native frame/package registry and
// endpoint services below are explicit adapters. AUTHORED_NOT_RUN.
using System;
using System.IO;
using System.Text;
namespace UnityEngine.Scripting {class PreserveAttribute:Attribute {}}
public interface IMemoryPoolableObject {void Reset();void Cleanup();}
public enum NetPackageDirection {ToServer,ToClient}
public class PooledBinaryWriter:BinaryWriter {internal PooledBinaryWriter(Stream s):base(s,Encoding.UTF8,true){}}
public class PooledBinaryReader:BinaryReader {internal PooledBinaryReader(Stream s):base(s,Encoding.UTF8,true){}}
public class ClientInfo {}
public abstract class NetPackage
{
    public ClientInfo Sender;public abstract NetPackageDirection PackageDirection{get;}
    public virtual void write(PooledBinaryWriter w){}public abstract void read(PooledBinaryReader r);public abstract void ProcessPackage(World w,GameManager g);
}
class EntityVehicle {}
class ItemStack {}
class RebirthCamperStationState {}
static class RebirthCamperInventorySnapshot {internal static ItemStack[] Restore(byte[] bytes)=>new[]{new ItemStack()};}
static class RebirthCamperStationSnapshot {internal static RebirthCamperStationState Restore(Guid installation,ItemStack item,byte[] bytes)=>new RebirthCamperStationState();}
public class World {internal bool Remote=true;internal EntityVehicle Vehicle=new EntityVehicle();internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Vehicle;}
public class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
static class ThreadManager {internal static bool IsMainThread()=>true;}
static class CamperFrame
{
    internal static void Write(BinaryWriter w,byte[] bytes){bytes=bytes??new byte[0];if(bytes.Length>1024)throw new InvalidDataException();w.Write(bytes.Length);w.Write(bytes);}
    internal static byte[] Read(BinaryReader r){int length=r.ReadInt32();if(length<0||length>1024)throw new InvalidDataException();var bytes=r.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return bytes;}
}
class RebirthCamperTransferSnapshot {internal static RebirthCamperTransferSnapshot Restore(Guid id,byte[] bytes)=>new RebirthCamperTransferSnapshot();}
static class RebirthCamperRemoteStationCommandStatus {internal enum State:byte {Unknown,Pending,Applied,Rejected}}
static class RebirthCamperRemoteStationClient
{
    internal static int Results;internal static Guid Operation;
    internal static void ReceiveOriginalStatus(EntityVehicle v,Guid t,Guid id,Guid station,Guid op,Guid job,RebirthCamperRemoteStationCommandKind kind,RebirthCamperRemoteStationCommandStatus.State status,RebirthCamperTransferSnapshot snapshot){Results++;Operation=op;}
}
static class RebirthCamperRemoteStationTransport
{
    internal static Action<EntityVehicle,Guid,Guid,ItemStack,RebirthCamperStationState> ReceiveUpdate;
    internal static int Queries;internal static bool Declaration;internal static RebirthCamperRemoteStationCommand Original;
    internal static void QueryOriginalStatus(World w,ClientInfo sender,int p,int v,Guid t,Guid id,Guid station,RebirthCamperRemoteStationCommand c,bool declaration=false){Queries++;Original=c;Declaration=declaration;}
}
// ACTUAL_COMMAND
// ACTUAL_QUERY
// ACTUAL_RESULT
// ACTUAL_UPDATE
static class Fixture
{
    private static void Need(bool value,string why){if(!value)throw new Exception(why);}
    private static byte[] Encode(NetPackage p){using(var s=new MemoryStream()){using(var w=new PooledBinaryWriter(s))p.write(w);return s.ToArray();}}
    private static void Decode(NetPackage p,byte[] bytes){using(var s=new MemoryStream(bytes))using(var r=new PooledBinaryReader(s))p.read(r);}
    private static void Reject(NetPackage p,byte[] bytes,string why){bool rejected=false;try{Decode(p,bytes);}catch(Exception e)when(e is InvalidDataException||e is IOException||e is ArgumentException||e is DecoderFallbackException){rejected=true;}Need(rejected,why);}
    public static void Main()
    {
        var token=Guid.NewGuid();var vehicle=Guid.NewGuid();var station=Guid.NewGuid();var job=Guid.NewGuid();
        foreach(var command in new[]{new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Craft,job,job,"rich-recipe",2,3),new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Cancel,Guid.NewGuid(),job,"",0,0),RebirthCamperRemoteStationCommand.Fuel(Guid.NewGuid(),true)})
        {
            var query=new NetPackageRebirthCamperStationCommandStatusQuery().Setup(1,2,token,vehicle,station,command);var bytes=Encode(query);var read=new NetPackageRebirthCamperStationCommandStatusQuery{Sender=new ClientInfo()};Decode(read,bytes);read.ProcessPackage(GameManager.Instance.World,GameManager.Instance);
            Need(RebirthCamperRemoteStationTransport.Original.Operation==command.Operation&&RebirthCamperRemoteStationTransport.Original.Job==command.Job&&RebirthCamperRemoteStationTransport.Original.Kind==command.Kind&&RebirthCamperRemoteStationTransport.Original.RecipeName==command.RecipeName,"query lost full original");
            Need(!RebirthCamperRemoteStationTransport.Declaration,"default status query invented admission protocol permission");
            var declared=Encode(new NetPackageRebirthCamperStationCommandStatusQuery().Setup(1,2,token,vehicle,station,command,true));Decode(read,declared);read.ProcessPackage(GameManager.Instance.World,GameManager.Instance);Need(RebirthCamperRemoteStationTransport.Declaration,"v2 query lost exact original admission declaration");
            var legacy=new byte[declared.Length-1];Array.Copy(declared,legacy,legacy.Length);legacy[0]=1;Decode(read,legacy);read.ProcessPackage(GameManager.Instance.World,GameManager.Instance);Need(!RebirthCamperRemoteStationTransport.Declaration,"legacy v1 query acquired absent-original refusal permission");
            var malformedDeclaration=(byte[])declared.Clone();malformedDeclaration[malformedDeclaration.Length-1]=2;Reject(read,malformedDeclaration,"noncanonical admission declaration accepted");
            for(int length=0;length<bytes.Length;length++){var cut=new byte[length];Array.Copy(bytes,cut,length);Reject(read,cut,"truncated query accepted");}
            var bad=(byte[])bytes.Clone();bad[0]=255;Reject(read,bad,"unknown query version accepted");bad=(byte[])bytes.Clone();Array.Clear(bad,9,16);Reject(read,bad,"empty query token accepted");
            read.Cleanup();Need(read.Sender==null,"query retained native sender");bool invalid=false;try{Encode(read);}catch(InvalidDataException){invalid=true;}Need(invalid,"query pool retained original command");
            foreach(RebirthCamperRemoteStationCommandStatus.State status in Enum.GetValues(typeof(RebirthCamperRemoteStationCommandStatus.State)))
            {
                var result=new NetPackageRebirthCamperStationCommandStatusResult().Setup(2,token,vehicle,station,command,status,new byte[]{1,2,3});var frame=Encode(result);var reply=new NetPackageRebirthCamperStationCommandStatusResult{Sender=new ClientInfo()};Decode(reply,frame);reply.ProcessPackage(GameManager.Instance.World,GameManager.Instance);Need(RebirthCamperRemoteStationClient.Operation==command.Operation,"result lost original operation");
                for(int length=0;length<frame.Length;length++){var cut=new byte[length];Array.Copy(frame,cut,length);Reject(reply,cut,"truncated status result accepted");}
                bad=(byte[])frame.Clone();bad[86]=255;Reject(reply,bad,"unknown outcome accepted");bad=(byte[])frame.Clone();bad[85]=255;Reject(reply,bad,"unknown command kind accepted");
                reply.Cleanup();Need(reply.Sender==null,"result retained native sender");int calls=RebirthCamperRemoteStationClient.Results;var other=new World();reply.ProcessPackage(other,GameManager.Instance);Need(RebirthCamperRemoteStationClient.Results==calls,"result crossed World scope");
            }
        }
        int updates=0;RebirthCamperRemoteStationTransport.ReceiveUpdate=(v,t,id,item,state)=>updates++;
        var update=new NetPackageRebirthCamperStationUpdate().Setup(2,token,vehicle,station,new byte[]{1},new byte[]{2});var updateBytes=Encode(update);var updateRead=new NetPackageRebirthCamperStationUpdate{Sender=new ClientInfo()};Decode(updateRead,updateBytes);updateRead.ProcessPackage(GameManager.Instance.World,GameManager.Instance);Need(updates==1,"current update not delivered");
        updateRead.ProcessPackage(new World(),GameManager.Instance);updateRead.ProcessPackage(GameManager.Instance.World,new GameManager());Need(updates==1,"update crossed World/callback scope");
        for(int length=0;length<updateBytes.Length;length++){var cut=new byte[length];Array.Copy(updateBytes,cut,length);Reject(updateRead,cut,"truncated update accepted");}
        updateRead.Cleanup();Need(updateRead.Sender==null,"update pool retained sender");updateRead.ProcessPackage(GameManager.Instance.World,GameManager.Instance);Need(updates==1,"pooled update retained payload");
        Console.WriteLine("MP071 actual status/update wire/pool scenarios: PASS (native registry/frame/endpoints are adapters)");
    }
}
