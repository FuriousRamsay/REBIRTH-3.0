using System;using System.IO;
namespace UnityEngine {public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}}
namespace UnityEngine.Scripting {public class PreserveAttribute:Attribute{}}
struct SpawnPosition {public UnityEngine.Vector3 position;public float heading;public SpawnPosition(UnityEngine.Vector3 p,float h){position=p;heading=h;}}
public enum NetPackageDirection{ToClient,ToServer}public class ClientInfo{}public class GameManager{}public class World{public bool remote;public bool IsRemote()=>remote;}
public class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s){}}public class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s){}}
public abstract class NetPackage {protected ClientInfo Sender;public virtual NetPackageDirection PackageDirection=>default;public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){}public virtual void ProcessPackage(World w,GameManager g){}protected bool ValidEntityIdForSender(int a)=>true;}
static class RebirthPurgeTruckStart {public static int Seen,Revision;public static Guid Id;public static UnityEngine.Vector3 Position;public static void Query(World w,ClientInfo s,int a,Guid id){}public static void Status(World w,int a,Guid id,byte state,int vehicle){}public static void RelocateClient(World w,int a,Guid id,int revision,UnityEngine.Vector3 p,float h){Seen++;Revision=revision;Id=id;Position=p;}}
class Program {
 static int checks;static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
 static byte[] Encode(NetPackage p){var stream=new MemoryStream();using(var w=new PooledBinaryWriter(stream)){p.write(w);w.Flush();return stream.ToArray();}}
 static void Main(){
  foreach(string name in new[]{"rock01","rock02","rock04","rock05","rock06","rock07","goreBlockAnimal","goreBlockHumanCorpse","goreHumanSkull","treeDeadPineLeaf","treeSnowyDeadBush"})Check(RebirthPurgeStartPlacementPolicy.Removable(name,true,"ModelEntity"),name+" must clear");
  Check(RebirthPurgeStartPlacementPolicy.Removable("grassTall",true,"BillboardPlant"),"foliage");
  foreach(string name in new[]{"rockResource","rock01Painted","concreteMaster","destroyedCar","treePine42m","lootChest","terrainAsphalt","gorePainting"})Check(!RebirthPurgeStartPlacementPolicy.Removable(name,true,"ModelEntity"),name+" must remain");
  Check(!RebirthPurgeStartPlacementPolicy.Removable("rock01",false,"ModelEntity"),"non-decoration protected");
  Check(!RebirthPurgeStartPlacementPolicy.Relocate(5,29.99),"bounded wait before limit");Check(RebirthPurgeStartPlacementPolicy.Relocate(6,1),"retry limit");Check(RebirthPurgeStartPlacementPolicy.Relocate(0,30),"unloaded site deadline");
  Check(!RebirthPurgeStartPlacementPolicy.FarEnough(64,0),"nearby is not a new site");Check(!RebirthPurgeStartPlacementPolicy.FarEnough(255.99,0),"boundary");Check(RebirthPurgeStartPlacementPolicy.FarEnough(256,0),"256m admitted");Check(RebirthPurgeStartPlacementPolicy.FarEnough(200,200),"diagonal admitted");
  var id=Guid.NewGuid();var random=new Random(27);for(int i=1;i<=200;i++){
   var point=new UnityEngine.Vector3((float)random.NextDouble()*4000,50,(float)random.NextDouble()*4000);var packet=new NetPackageRebirthPurgeStartRelocateV1().Setup(171,id,i,new SpawnPosition(point,90));var bytes=Encode(packet);Check(bytes.Length==packet.GetLength(),"41-byte exact length");var decoded=new NetPackageRebirthPurgeStartRelocateV1();using(var r=new PooledBinaryReader(new MemoryStream(bytes)))decoded.read(r);decoded.ProcessPackage(new World{remote=true},new GameManager());Check(RebirthPurgeTruckStart.Revision==i&&RebirthPurgeTruckStart.Id==id&&RebirthPurgeTruckStart.Position.x==point.x,"wire roundtrip");int seen=RebirthPurgeTruckStart.Seen;decoded.ProcessPackage(new World{remote=false},new GameManager());Check(RebirthPurgeTruckStart.Seen==seen,"server rejects client-only apply");
  }
  var valid=Encode(new NetPackageRebirthPurgeStartRelocateV1().Setup(171,id,1,new SpawnPosition(new UnityEngine.Vector3(1,50,1),0)));valid[0]=2;try{new NetPackageRebirthPurgeStartRelocateV1().read(new PooledBinaryReader(new MemoryStream(valid)));throw new Exception("bad version admitted");}catch(InvalidDataException){checks++;}
  Check(Encode(new NetPackageRebirthPurgeStartQueryV1().Setup(171,id)).Length==21,"query V1 unchanged");Check(Encode(new NetPackageRebirthPurgeStartStatusV1().Setup(171,id,2,0)).Length==26,"status V1 unchanged");
  Console.WriteLine($"PASS {checks} actual production debris/retry/distance policy and relocation-packet checks. Native cleanup/streaming/seat handoff not executed.");
 }
}