using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;
using HarmonyLib;
namespace HarmonyLib {class Harmony{}}
namespace UnityEngine {struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>new Vector3();public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);public static float SqrMagnitude(Vector3 v)=>v.x*v.x+v.y*v.y+v.z*v.z;}static class Mathf {public static int FloorToInt(float v)=>(int)Math.Floor(v);}class Rigidbody {public bool isKinematic;}}
struct Vector3i {public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
class ItemStack {public string Id;public int count;public ItemStack(string id,int n){Id=id;count=n;}public ItemStack Clone()=>new ItemStack(Id,count);}
class Entity {public World world;public int entityId;public ulong WorldTimeBorn;}
class EntityPlayer:Entity{}
class EntityItem:Entity {public ItemStack itemStack;public UnityEngine.Vector3 position;public UnityEngine.Rigidbody itemRB=new UnityEngine.Rigidbody();}
class ChunkCustomData {public byte[] data;public ChunkCustomData(string key,ulong ticks,bool network){}}
class Data {public Dictionary<string,ChunkCustomData> dict=new Dictionary<string,ChunkCustomData>();public void Set(string key,ChunkCustomData value){dict[key]=value;}public void Remove(string key){dict.Remove(key);}}
class Stub {public int id;}
class Chunk {public int X,Z;public bool InProgressUnloading,IsLocked,isModified;public HashSet<int> pendingEntityCreateOps=new HashSet<int>();public List<Stub> entityStubs=new List<Stub>();public Data ChunkCustomData=new Data();}
class WorldState {public string Guid;}
class Cache {public object ChunkProvider=new ChunkProviderGenerateWorld();}
class World {public WorldState worldState=new WorldState();public Chunk Chunk=new Chunk();public Cache ChunkCache=new Cache();public Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();public float RandomRange(float a,float b)=>a;public bool IsRemote()=>false;public static int toChunkXZ(int value)=>value>>4;public object GetChunkSync(int x,int z)=>Chunk;public Entity GetEntity(int id)=>Entities.TryGetValue(id,out var entity)?entity:null;}
class RegionFileAccessSectorBased{}
class Manager {public string saveDirectory;public object regionFileAccess=new RegionFileAccessSectorBased();}
class ChunkProviderGenerateWorld {public Manager m_RegionFileManager=new Manager();}
static class RegionFile {public static string ConstructFullFilePath(string directory,int x,int z,string extension)=>Path.Combine(directory,"fake.7rg");}
static class SdFile {public static Stream Open(string path,FileMode mode,FileAccess access,FileShare share)=>File.Open(path,mode,access,share);}
static class RebirthStationRegionPayload {public static bool TryRead(Stream stream,int x,int z,out byte[] bytes){bytes=null;return false;}}
static class RebirthStationChunkInflate {public static bool TryDecode(byte[] bytes,int maximum,out byte[] body){body=null;return false;}}
static class RebirthStationSnapshotEvidence {public const int MaximumSnapshotBytes=8388608;}
static class AdvancedFarmingHarvestPublication {
 internal sealed class Proof {internal string Digest;internal long Generation;}
 internal static bool After;static Guid before;
 internal static bool Observe(Guid operation,Guid source,World world,Vector3i position,Func<bool> current,out Proof proof){proof=null;if(!current())return false;if(before==Guid.Empty)before=operation;if(operation!=before&&!After)return false;proof=new Proof{Digest=new string(operation==before?'a':'b',64),Generation=42};return true;}
 internal static void Complete(Guid operation){}
}
static class GameIO {public static string Directory;public static string GetSaveGameDir()=>Directory;}
static class ThreadManager {public static bool IsMainThread()=>true;}
static class Log {public static void Warning(string text){}}
class GameManager {
 public static GameManager Instance=new GameManager();public World World=new World();public int Spawns;
 public void ItemDropServer(ItemStack stack,UnityEngine.Vector3 position,UnityEngine.Vector3 motion,int owner,float lifetime,bool relative){var item=new EntityItem{world=World,entityId=100+Spawns,WorldTimeBorn=123,position=position,itemStack=stack};if(AdvancedFarmingHarvestDrops.Capture(item)){Spawns++;World.Entities.Add(item.entityId,item);}}
}
static class RebirthHarmonyBootstrap {public static void PatchClassOnce(Harmony h,Type t){}}
class AdvancedFarmingHarvestDropSpawn{}class AdvancedFarmingHarvestDropCollect{}class AdvancedFarmingHarvestDropNativeCollect{}class AdvancedFarmingHarvestDropUpdate{}class AdvancedFarmingHarvestDropUnload{}
static class AdvancedFarmingHarvestReceipt {
 internal enum Phase {OwnerPublished=2,Retired=3}
 internal sealed class Record {public Guid Operation=Guid.NewGuid(),Source=Guid.NewGuid();public string World;public Phase State=Phase.OwnerPublished;public float DropX=1,DropY=60,DropZ=1;}
 public static Record Current;
 public static ItemStack[] Outputs=new[]{new ItemStack("corn",2)};
 public static ItemStack[] Remaining(Record r)=>Outputs.Select(item=>item.Clone()).ToArray();
 public static string OutputImage(ItemStack[] items){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(items.Length);foreach(var item in items){w.Write(item.Id);w.Write(item.count);}return Convert.ToBase64String(s.ToArray());}}
 public static ItemStack[] ReadOutputs(string image){using(var s=new MemoryStream(Convert.FromBase64String(image)))using(var r=new BinaryReader(s)){int count=r.ReadInt32();var result=new ItemStack[count];for(int i=0;i<count;i++)result[i]=new ItemStack(r.ReadString(),r.ReadInt32());return result;}}
 public static string OutputDigest(string image){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(image))).Replace("-","").ToLowerInvariant();}
 public static bool TryRead(string world,Guid operation,out Record record){record=Current;return record.Operation==operation&&record.World==world;}
}
static class AdvancedFarmingHarvestTerminalPlot {internal static UnityEngine.Vector3 DropPosition(AdvancedFarmingHarvestReceipt.Record r,ItemStack item,out UnityEngine.Vector3 motion){motion=UnityEngine.Vector3.zero;return new UnityEngine.Vector3(r.DropX,r.DropY,r.DropZ);}}
// PRODUCTION_DURABLE
// PRODUCTION_DROPS
static class Fixture {
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static void Main(string[] args){GameIO.Directory=args[0];var world=GameManager.Instance.World;world.worldState.Guid=Guid.NewGuid().ToString();var record=new AdvancedFarmingHarvestReceipt.Record{World=world.worldState.Guid};AdvancedFarmingHarvestReceipt.Current=record;var owner=new EntityPlayer{world=world,entityId=1};
  Check(!AdvancedFarmingHarvestDrops.Observe(world,owner,record,out _),"unpublished native item settled");Check(GameManager.Instance.Spawns==1,"native item not spawned once");var item=(EntityItem)world.GetEntity(100);
  Check(AdvancedFarmingHarvestDrops.Held(item)&&item.itemRB.isKinematic,"native item not fenced before publication");
  Check(item.itemStack.Id=="corn"&&item.itemStack.count==2,"original output mutated");
  Check(!AdvancedFarmingHarvestDrops.Observe(world,owner,record,out _)&&GameManager.Instance.Spawns==1,"pending native save duplicated drop");
  AdvancedFarmingHarvestPublication.After=true;Check(AdvancedFarmingHarvestDrops.Observe(world,owner,record,out var proof)&&proof.Generation==42,"native publication did not settle");
  record.State=AdvancedFarmingHarvestReceipt.Phase.Retired;AdvancedFarmingHarvestDrops.Complete(record);Check(!AdvancedFarmingHarvestDrops.Held(item)&&!item.itemRB.isKinematic,"retirement did not restore native physics");
  record.State=AdvancedFarmingHarvestReceipt.Phase.OwnerPublished;Check(AdvancedFarmingHarvestDrops.Observe(world,owner,record,out _)&&GameManager.Instance.Spawns==1,"durable published drop replayed");
  string path=Path.Combine(GameIO.Directory,"rebirth","farming-harvest","v1","drops",record.Operation.ToString("N")+".json");AdvancedFarmingHarvestDrops.RetireWorld(world);File.WriteAllText(path,"broken-original");Check(!AdvancedFarmingHarvestDrops.Observe(world,owner,record,out _)&&GameManager.Instance.Spawns==1&&File.ReadAllText(path)=="broken-original","corrupt original drop overwritten or replayed");
    AdvancedFarmingHarvestDrops.RetireWorld(world);AdvancedFarmingHarvestReceipt.Outputs=new[]{new ItemStack("corn",2),new ItemStack("plot",1)};
  record=new AdvancedFarmingHarvestReceipt.Record{World=world.worldState.Guid};AdvancedFarmingHarvestReceipt.Current=record;int start=GameManager.Instance.Spawns;
  Check(AdvancedFarmingHarvestDrops.Observe(world,owner,record,out var combined),"two original outputs failed publication");Check(GameManager.Instance.Spawns==start+2,"combined output did not spawn each native stack once");
  Check(combined.OutputDigest==AdvancedFarmingHarvestReceipt.OutputDigest(AdvancedFarmingHarvestReceipt.OutputImage(AdvancedFarmingHarvestReceipt.Outputs)),"combined output proof changed original stacks");
  Check(AdvancedFarmingHarvestDrops.Observe(world,owner,record,out _)&&GameManager.Instance.Spawns==start+2,"combined replay duplicated native output");
  Console.WriteLine("PASS actual native-drop coordinator: pending fence, once-only spawn, proof, release, replay, corruption; native publication doubled");
 }
}
