// Native game/clock/identity types below are disclosed doubles. Persistence bytes use real files.
// Production methods are inserted by mpB_firstwave.mjs; this fixture has NOT RUN.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
public struct Vector3i {
 public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}
 public static bool operator==(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;
 public static bool operator!=(Vector3i a,Vector3i b)=>!(a==b);
 public override bool Equals(object o)=>o is Vector3i p&&this==p;
 public override int GetHashCode()=>x*397^y*31^z;
}
public interface IChunk {int GetHeight(int x,int z);}
public class Chunk:IChunk {public bool InProgressUnloading;public int Height;public int GetHeight(int x,int z)=>Height;}
public class TileEntity {public bool IsRemoving;public Chunk Owner;public Chunk GetChunk()=>Owner;}
public class WorldBase {
 public readonly Dictionary<string,IChunk> Chunks=new Dictionary<string,IChunk>();
 public readonly Dictionary<Vector3i,TileEntity> Tiles=new Dictionary<Vector3i,TileEntity>();
 public int Reads;public IChunk GetChunkSync(int x,int z){Chunks.TryGetValue(x+":"+z,out var c);return c;}
 public TileEntity GetTileEntity(Vector3i p){Tiles.TryGetValue(p,out var t);return t;}
}
public class World:WorldBase {public bool Remote=false;public bool IsRemote()=>Remote;public NativeWorldState worldState=new NativeWorldState();public static int toChunkXZ(int v)=>v>>4;public static int toBlockXZ(int v)=>v&15;
 public EnumLandClaimOwner GetLandClaimOwner(Vector3i p,PersistentPlayerData d)=>EnumLandClaimOwner.Self;}
class TileEntityWorkstation {public bool IsPlayerPlaced=true;}
public class NativeWorldState{public string Guid=System.Guid.NewGuid().ToString("D");}
class GameManager{public static GameManager Instance=new GameManager();public World World;}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
class ConnectionManager{public bool IsServer=true;}
static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public enum EnumLandClaimOwner{Self,Other}
public class PlatformUserIdentifierAbs {
 public string CombinedString;public override bool Equals(object x)=>x is PlatformUserIdentifierAbs p&&p.CombinedString==CombinedString;
 public override int GetHashCode()=>CombinedString.GetHashCode();
}
public class PersistentPlayerData{public PlatformUserIdentifierAbs PrimaryId=new PlatformUserIdentifierAbs{CombinedString="owner"};}
static class ProviderChecks {
 // PROVIDER_MEMBER
 public static bool Live(WorldBase w,Vector3i p,TileEntity t)=>IsTileEntityLive(w,p,t);
}
struct BlockValue{public bool isair;public object Block;}
static class LightChecks {
 static readonly int[] s_sourceMemoStamp=new int[100],s_visitedStamp=new int[100];
 static readonly Vector3i[] s_queue=new Vector3i[100];
 static readonly bool[] s_sourceMemoOpen=new bool[100];
 enum CellVerdict{Pass,StructureBlocked,CoverBlocked}
 static int MakeLocalIndex(Vector3i p,int a,int b,int c,int d,int e,int f)=>0;
 static BlockValue GetBlockMemoized(WorldBase w,Vector3i p,int a,int b,int c,int d,int e,int f,int stamp){w.Reads++;return new BlockValue{isair=true};}
 static CellVerdict ClassifyEdge(WorldBase w,Vector3i a,BlockValue av,Vector3i b,BlockValue bv,int x,int y,int z)=>CellVerdict.Pass;
 static string GetBlockName(BlockValue v)=>"air";
 static void EnqueueCell(Vector3i p,int a,int b,int c,int d,int e,int f,int stamp,ref int tail){tail++;}
 // LIGHT_MEMBERS
 public static bool Column(WorldBase w,Vector3i p,out bool incomplete){Array.Clear(s_sourceMemoStamp,0,100);var r=new NaturalLightExposure();bool b=IsOpenSkyColumnMemoized(w,p,0,0,0,64,64,64,1,ref r);incomplete=r.IncompleteGeometry;return b;}
 public static bool Neighbor(WorldBase w,Vector3i p){var r=new NaturalLightExposure();int tail=0;
  TryEnqueueNeighbor(w,new Vector3i(15,1,0),new BlockValue(),p.x,p.y,p.z,1,0,0,0,64,0,64,0,64,64,64,64,1,ref tail,ref r);
  return r.IncompleteGeometry&&tail==0&&w.Reads==0;}
 public static bool Area(WorldBase w,Vector3i p)=>IsExposureAreaLoaded(w,p);
}
static class GameIO{public static string Directory;public static string GetSaveGameDir()=>Directory;}
static class Time{public static float realtimeSinceStartup;}
static class Log{public static void Warning(string s){}public static void Error(string s){}public static void Out(string s){}}
static class RebirthFireRuntimePolicy{public static bool Enabled=true;}
static class RebirthFireDefaults{public const int PersistenceAutosaveSeconds=30,MaxSnapshotFires=100,MaxSnapshotCooldowns=100;}
class RebirthFireState {public Vector3i Position;public ulong IgnitedWorldTime,NextProcessWorldTime;public int IgniterEntityId,SourceBlockType;public uint ScheduleToken;public byte Flags;}
class RebirthFireService {
 public static readonly RebirthFireService Instance=new RebirthFireService();public int Revision;
 public List<RebirthFireState> SnapshotFires()=>new List<RebirthFireState>();
 public Dictionary<Vector3i,ulong> SnapshotCooldowns()=>new Dictionary<Vector3i,ulong>();
 public void Restore(World w,List<RebirthFireState> f,Dictionary<Vector3i,ulong> c){Revision++;}
}
// DURABLE_HELPER
// FIRE_CLASS
static class SaveDataUtils{public static bool Managed;public static bool TryGetManagedPath(string p,out object value){value=null;return Managed;}}
static class SdFile {
 public static bool FailPrimaryCopy;
 public static bool Exists(string p)=>File.Exists(p);public static Stream Create(string p)=>File.Create(p);public static Stream OpenRead(string p)=>File.OpenRead(p);
 public static void Copy(string from,string to,bool overwrite){if(FailPrimaryCopy&&to.EndsWith(".dat")){File.WriteAllBytes(to,new byte[]{1});throw new IOException("injected managed copy failure");}File.Copy(from,to,overwrite);}
}
static class RebirthWorkstationCredentials {public const string ResetRequired="reset";public static bool IsCurrentVerifier(string s)=>s.StartsWith("v4:");}
static class RebirthLogSettings{public static bool BlockPickupLoggingEnabled;}
static class SecurityChecks {
 const int Signature=0x53574252;const byte FileVersion=4;const string FileName="rebirth_workstation_owners.dat";
 static readonly object Sync=new object();static readonly Dictionary<Vector3i,SecurityState> States=new Dictionary<Vector3i,SecurityState>();
 static readonly Dictionary<Vector3i,float> NextStateRequestTime=new Dictionary<Vector3i,float>();
 static bool serverWorld=true,dirty,loadFailed;static long accessRevision;
 static void AccessChanged(Vector3i p){accessRevision++;}
 static bool TryParseUser(string s,out PlatformUserIdentifierAbs u){u=string.IsNullOrEmpty(s)?null:new PlatformUserIdentifierAbs{CombinedString=s};return u!=null;}
 // SECURITY_MEMBERS
 public static void Start(){States.Clear();dirty=false;loadFailed=false;serverWorld=true;Load();}
 public static void Seed(string owner){States[new Vector3i(1,2,3)]=new SecurityState{Owner=owner};dirty=true;}
 public static void Save()=>SaveCurrent();public static bool Blocked=>loadFailed;
 public static string Owner=>States.Count==0?null:States.Values.First().Owner;
 public static bool CanMigrate()=>GetOrMigrateState(new World(),new Vector3i(2,2,2),new TileEntityWorkstation(),new PersistentPlayerData(),true)!=null;
}
static class Program {
 static void LoadFire(){var current=new World();GameManager.Instance.World=current;RebirthFirePersistenceScope.Bind(current);RebirthFirePersistence.Load(current);}
 static void Need(bool x,string label){if(!x)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static void Folder(string root,string name){GameIO.Directory=Path.Combine(root,name);Directory.CreateDirectory(GameIO.Directory);}
 static void Main(string[] args){string root=args[0];
  var pos=new Vector3i(15,1,0);var world=new World();var chunk=new Chunk();world.Chunks["0:0"]=chunk;var tile=new TileEntity{Owner=chunk};world.Tiles[pos]=tile;
  Need(ProviderChecks.Live(world,pos,tile),"MP088 live current provider");chunk.InProgressUnloading=true;
  Need(!ProviderChecks.Live(world,pos,tile),"MP088 native OnUnload leaves old chunk but refuses provider");chunk.InProgressUnloading=false;world.Chunks.Remove("0:0");
  Need(!ProviderChecks.Live(world,pos,tile),"MP088 detached cached chunk refused");world.Chunks["0:0"]=new Chunk();
  Need(!ProviderChecks.Live(world,pos,tile),"MP088 replacement chunk cannot reuse cached provider");world.Chunks["0:0"]=chunk;world.Tiles[pos]=new TileEntity{Owner=chunk};
  Need(!ProviderChecks.Live(world,pos,tile),"MP088 replaced tile incarnation refused");
  bool incomplete;Need(!LightChecks.Column(world,new Vector3i(16,1,0),out incomplete)&&incomplete,"MP034 missing column cannot become sky");
  world.Reads=0;Need(LightChecks.Neighbor(world,new Vector3i(16,1,0)),"MP034 missing neighbor not read as passable air");
  world.Chunks["1:0"]=new Chunk{Height=0};Need(LightChecks.Column(world,new Vector3i(16,1,0),out incomplete)&&!incomplete,"MP034 loaded open column control");
  Need(!LightChecks.Area(world,new Vector3i(15,1,0)),"MP034 incomplete neighborhood cannot retain cached geometry");
  Folder(root,"fire-bad");var fire=Path.Combine(GameIO.Directory,"RebirthFireManager.dat");File.WriteAllBytes(fire,new byte[]{1,2});File.WriteAllBytes(fire+".bak",new byte[]{3,4});
  RebirthFirePersistence.ResetSession();LoadFire();Need(!RebirthFirePersistence.SaveSafely("fixture"),"MP021 failed primary and backup block save");
  bool denied=false;try{RebirthFirePersistence.ClearStoredState();}catch(InvalidOperationException){denied=true;}Need(denied&&File.ReadAllBytes(fire).SequenceEqual(new byte[]{1,2})&&File.ReadAllBytes(fire+".bak").SequenceEqual(new byte[]{3,4}),"MP021 failed candidates survive cleanup and save");
  RebirthFirePersistence.ResetSession();RebirthFireRuntimePolicy.Enabled=false;Need(!RebirthFirePersistence.SaveSafely("disabled-new-session")&&File.ReadAllBytes(fire).SequenceEqual(new byte[]{1,2}),"MP021 disabled startup cannot silently clear unreadable store");RebirthFireRuntimePolicy.Enabled=true;
  Folder(root,"fire-good");RebirthFirePersistence.ResetSession();LoadFire();Need(RebirthFirePersistence.SaveSafely("new"),"MP021 genuinely new store writable");fire=Path.Combine(GameIO.Directory,"RebirthFireManager.dat");File.Copy(fire,fire+".bak");File.WriteAllBytes(fire,new byte[]{1});RebirthFirePersistence.ResetSession();LoadFire();Need(RebirthFirePersistence.SaveSafely("recovered"),"MP021 valid backup permits save");
  var validFire=File.ReadAllBytes(fire);Folder(root,"fire-version");fire=Path.Combine(GameIO.Directory,"RebirthFireManager.dat");var unsupported=(byte[])validFire.Clone();unsupported[4]=255;unsupported[5]=255;File.WriteAllBytes(fire,unsupported);
  RebirthFirePersistence.ResetSession();LoadFire();Need(!RebirthFirePersistence.SaveSafely("unsupported")&&File.ReadAllBytes(fire).SequenceEqual(unsupported),"MP021 unsupported version is quarantined");
  Folder(root,"fire-trailing");fire=Path.Combine(GameIO.Directory,"RebirthFireManager.dat");File.WriteAllBytes(fire,validFire.Concat(new byte[]{1}).ToArray());RebirthFirePersistence.ResetSession();LoadFire();Need(!RebirthFirePersistence.SaveSafely("trailing"),"MP021 trailing-data store is quarantined");
  Folder(root,"fire-missing");fire=Path.Combine(GameIO.Directory,"RebirthFireManager.dat");File.WriteAllBytes(fire+".bak",validFire);RebirthFirePersistence.ResetSession();LoadFire();Need(RebirthFirePersistence.SaveSafely("backup-only"),"MP021 missing primary tries valid backup");
  Folder(root,"security-local");SaveDataUtils.Managed=false;SecurityChecks.Start();SecurityChecks.Seed("owner-a");SecurityChecks.Save();var file=Path.Combine(GameIO.Directory,"rebirth_workstation_owners.dat");
  SecurityChecks.Seed("owner-b");SecurityChecks.Save();SecurityChecks.Start();Need(SecurityChecks.Owner=="owner-b","MP023 complete atomic candidate roundtrip");File.Delete(file);SecurityChecks.Start();Need(SecurityChecks.Owner=="owner-a","MP023 missing primary recovers backup");
  Folder(root,"security-bad");file=Path.Combine(GameIO.Directory,"rebirth_workstation_owners.dat");File.WriteAllBytes(file,new byte[]{1});File.WriteAllBytes(file+".bak",new byte[]{2});SecurityChecks.Start();Need(SecurityChecks.Blocked&&!SecurityChecks.CanMigrate(),"MP023 unresolved load blocks ownership migration");SecurityChecks.Seed("new-owner");SecurityChecks.Save();Need(File.ReadAllBytes(file).SequenceEqual(new byte[]{1}),"MP023 unresolved load cannot replace primary");
  Folder(root,"security-managed");SaveDataUtils.Managed=true;SdFile.FailPrimaryCopy=false;SecurityChecks.Start();SecurityChecks.Seed("owner-a");SecurityChecks.Save();SecurityChecks.Seed("owner-b");SdFile.FailPrimaryCopy=true;SecurityChecks.Save();
  file=Path.Combine(GameIO.Directory,"rebirth_workstation_owners.dat");Need(File.Exists(file+".tmp")&&File.Exists(file+".bak"),"MP023 managed failed copy retains validated recovery candidates");SdFile.FailPrimaryCopy=false;SecurityChecks.Start();Need(SecurityChecks.Owner=="owner-b"&&!SecurityChecks.Blocked,"MP023 managed failed publication recovers complete candidate");
  Folder(root,"security-managed-new-failure");SdFile.FailPrimaryCopy=true;SecurityChecks.Start();SecurityChecks.Seed("first-owner");SecurityChecks.Save();file=Path.Combine(GameIO.Directory,"rebirth_workstation_owners.dat");var retained=File.ReadAllBytes(file+".tmp");
  SecurityChecks.Seed("later-owner");SecurityChecks.Save();Need(File.ReadAllBytes(file+".tmp").SequenceEqual(retained),"MP023 retry cannot truncate sole valid managed candidate");SdFile.FailPrimaryCopy=false;SecurityChecks.Start();Need(SecurityChecks.Owner=="first-owner","MP023 sole candidate recovers after repeated publication failure");
  Console.WriteLine("MPB offline production-method cases complete; native callbacks/transport/topology are doubled, not gameplay proof.");
 }
}
