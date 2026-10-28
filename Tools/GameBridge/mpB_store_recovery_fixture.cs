// Source methods inserted from current production. Native world, naming and bounded codec are doubles.
using System;using System.Text;using System.Collections.Generic;using System.IO;using System.Xml;using System.Linq;using System.Globalization;using System.Runtime.CompilerServices;
public struct Vector3i {public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}public static bool operator ==(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator !=(Vector3i a,Vector3i b)=>!(a==b);public override bool Equals(object value)=>value is Vector3i other&&this==other;public override int GetHashCode()=>x^(y<<8)^(z<<16);}
public class Block{public string GetBlockName()=>"crate";}
public struct BlockValue{public Block Block;}
public class WorldBase{public bool Remote=false;public bool IsRemote()=>Remote;public BlockValue GetBlock(Vector3i p)=>new BlockValue{Block=new Block()};}
public class World:WorldBase{}
class GameManager{public static readonly GameManager Instance=new GameManager();public WorldBase World=new World();}
static class GameIO{public static string Root;public static string GetSaveGameDir()=>Root;}
static class Log{public static void Warning(string s){}}
static class Time{public static float realtimeSinceStartup;}
static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
static class RebirthContainerRenameService{public static string NormalizeName(string s)=>(s??"").Trim();}
// DURABLE_HELPER
static class Names {
 const string FileName="RebirthContainerNames.xml";static readonly Dictionary<Vector3i,Entry> names=new Dictionary<Vector3i,Entry>();static bool loaded,serverAuthority,loadFailed;
 static Vector3i Canonicalize(WorldBase w,Vector3i p)=>p;static void BroadcastUpdate(Vector3i p,string b,string n){}
 // NAME_MEMBERS
 public static int Count=>names.Count;
}
// GRACE_RECORD
class QuestJournal{}class Quest{}class GraceProjection{}class QuestProjectionSignature{}
static class Grace {
 const int OfflineFormatVersion=1;const string OfflineFileName="RebirthTraderQuestGrace.xml";
 static readonly object Sync=new object();static readonly List<RebirthTraderOfflineGraceRecord> Offline=new List<RebirthTraderOfflineGraceRecord>();
 static bool offlineLoaded,offlineDirty,offlineLoadFailed;static string offlineScopePath;
 static readonly HashSet<string> PartyCompletionSignals=new HashSet<string>();static readonly Dictionary<int,long> ArmedDeathDeadlineByPlayer=new Dictionary<int,long>();
 static ConditionalWeakTable<QuestJournal,GraceProjection> GraceByJournal=new ConditionalWeakTable<QuestJournal,GraceProjection>();
 static ConditionalWeakTable<Quest,QuestProjectionSignature> QuestSignatures=new ConditionalWeakTable<Quest,QuestProjectionSignature>();
 // GRACE_MEMBERS
 public static bool Open()=>EnsureOfflineLoaded();public static void Save()=>SaveOfflineLocked();public static int Count=>Offline.Count;public static bool Dirty=>offlineDirty;
 public static void Seed(string player){Offline.Add(new RebirthTraderOfflineGraceRecord{PlayerId=player,QuestId="quest",QuestCode=7,DeadlineUtcTicks=DateTime.MaxValue.Ticks});offlineDirty=true;}
 public static void ProcessRestart(){Offline.Clear();offlineLoaded=false;offlineDirty=false;offlineLoadFailed=false;offlineScopePath=null;}
}
static class ModEvents{public struct SGameStartingData{}public struct SGameStartDoneData{}}
static class RebirthSurvivorNetworkCodec {
 public static void WriteString(BinaryWriter w,string s,int max){if(s.Length>max)throw new Exception("bound");w.Write(s);}
 public static string ReadString(BinaryReader r,int max){var s=r.ReadString();if(s.Length>max)throw new Exception("bound");return s;}
}
// PLACED_RECORDS
static class Placed {
 const uint Magic=0x50574252U;const ushort Version=1;const int MaxRecords=250000;const float SaveInterval=20f;
 static readonly Dictionary<string,RebirthPlacedWorkmanshipRecord> records=new Dictionary<string,RebirthPlacedWorkmanshipRecord>();static bool loaded,dirty,loadFailed;static string loadedPath;static WorldBase loadedWorld;static float nextSave;
 static bool awaitingStart;static object mutation=new object(),pendingMutation;static string pendingTemporary,pendingDigest;
 static bool IsServer()=>true;
 // PLACED_MEMBERS
 public static void Begin(){var d=new ModEvents.SGameStartingData();OnGameStarting(ref d);}
 public static void Done(){var done=new ModEvents.SGameStartDoneData();OnGameStartDone(ref done);}
 public static void Start(){Begin();Done();}
 public static void ProcessRestart(){records.Clear();loaded=false;dirty=false;loadFailed=false;loadedPath=null;loadedWorld=null;awaitingStart=false;mutation=new object();pendingMutation=null;pendingTemporary=pendingDigest=null;Start();}
 public static void Seed(string owner,float workmanship=.8f){records["0|1,2,3"]=new RebirthPlacedWorkmanshipRecord{Position=new Vector3i(1,2,3),BlockName="wood",CreatorStableId=owner,Kind=RebirthPlacedProvenanceKind.ConstructionPlacement,Workmanship=workmanship};MarkDirty();}
 public static int Count=>records.Count;public static bool Dirty=>dirty;public static bool Blocked=>loadFailed;public static bool Current=>HasCurrentStore;public static string PathName=>PersistencePath;
 public static void Stop()=>Reset(true);
}
static class NativeProvenanceFixture {
 // PROVENANCE_NATIVE_DECODER
 public static void Exercise(){var id=Guid.NewGuid().ToString("N");byte[] payload;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write(Guid.ParseExact(id,"N").ToByteArray());writer.Write((ushort)7);writer.Write(true);writer.Write(0);writer.Write("wood");writer.Write((byte)1);writer.Write("exact-author");writer.Write("background");writer.Write("bonus");writer.Write("skill.construction");writer.Write(25f);writer.Write(.75f);writer.Write(500);writer.Write((ulong)321);writer.Flush();payload=stream.ToArray();}
 var record=Decode(payload,id,new Vector3i(1,2,3),"wood");if(record==null||record.SourceIncarnation!=id||record.CreatorStableId!="exact-author"||record.Workmanship!=.75f)throw new Exception("native provenance payload did not preserve original");if(Decode(payload,id,new Vector3i(1,2,3),"replacement")!=null)throw new Exception("different current block accepted old native record");Array.Resize(ref payload,payload.Length-1);if(Decode(payload,id,new Vector3i(1,2,3),"wood")!=null)throw new Exception("truncated native provenance accepted");Console.WriteLine("PASS actual bounded native provenance decoder preserves author and refuses replacement/truncation");}
}
static class Program {
 static void Need(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Folder(string root,string name){GameIO.Root=Path.Combine(root,name);Directory.CreateDirectory(GameIO.Root);GameManager.Instance.World=new World();}
 static string GraceFile=>Path.Combine(GameIO.Root,"RebirthTraderQuestGrace.xml");
 static void Main(string[] args){NativeProvenanceFixture.Exercise();string root=args[0];var p=new Vector3i(1,2,3);var w=new World();
  Folder(root,"names-new");Names.Reset(true);Need(Names.SetServer(w,p,"original"),"MP011 new store writable");var file=Path.Combine(GameIO.Root,"RebirthContainerNames.xml");File.Copy(file,file+".bak");File.WriteAllText(file,"bad");Names.Reset(true);Need(Names.SetServer(w,p,"recovered"),"MP011 valid backup allows rename");
  File.WriteAllText(file,"bad-primary");File.WriteAllText(file+".bak","bad-backup");Names.Reset(true);Need(!Names.SetServer(w,p,"replacement")&&File.ReadAllText(file)=="bad-primary"&&File.ReadAllText(file+".bak")=="bad-backup","MP011 failed recovery refuses rename and preserves files");
  Folder(root,"grace-new");Grace.ProcessRestart();Need(Grace.Open(),"MP012 new store opens");Grace.Seed("owner-a");Grace.Save();Need(!Grace.Dirty,"MP012 validated atomic save");var graceA=GraceFile;File.Copy(graceA,graceA+".bak");File.Delete(graceA);Grace.ProcessRestart();Need(Grace.Open()&&Grace.Count==1,"MP012 missing primary recovers backup");
  File.WriteAllText(graceA,"bad");File.WriteAllText(graceA+".bak","bad");Grace.ProcessRestart();Need(!Grace.Open(),"MP012 corrupt candidates are quarantined");
  File.WriteAllText(graceA,"<rebirthTraderQuestGrace format='999'/>");File.Delete(graceA+".bak");Grace.ProcessRestart();Need(!Grace.Open(),"MP012 unsupported format is not empty writable store");
  Folder(root,"grace-retain-a");Grace.ProcessRestart();Need(Grace.Open(),"MP012 scope A begins");Grace.Seed("held-owner");var originalGrace=GraceFile;Directory.CreateDirectory(originalGrace);Grace.Save();Need(Grace.Dirty,"MP012 publication failure keeps dirty state");Grace.ResetOffline(true);Need(Grace.Dirty&&Grace.Count==1,"MP012 reset cannot discard failed write");
  Folder(root,"grace-retain-b");var graceB=GraceFile;Need(!Grace.Open()&&!File.Exists(graceB),"MP012 A failure cannot publish into B");Directory.Delete(originalGrace);Need(Grace.Open()&&Grace.Count==0&&File.Exists(originalGrace)&&!File.Exists(graceB),"MP012 old scope flushes before fresh B initializes");
  Folder(root,"placed-lifecycle");Placed.ProcessRestart();Placed.Begin();Need(!Placed.Current,"MP036 GameStarting cannot bind provisional old world");GameManager.Instance.World=new World();Placed.Done();Need(Placed.Current,"MP036 GameStartDone binds actual replacement world");Placed.Stop();
  Folder(root,"placed-a");Placed.ProcessRestart();Need(Placed.Current,"MP036 new bound provenance store");Placed.Seed("owner-a");Placed.Save();Need(!Placed.Dirty,"MP036 durable provenance save");file=Placed.PathName;File.Copy(file,file+".bak");File.Delete(file);Placed.ProcessRestart();Need(Placed.Count==1&&!Placed.Blocked,"MP036 missing primary recovers backup");
  File.WriteAllBytes(file,new byte[]{1});File.WriteAllBytes(file+".bak",new byte[]{2});Placed.ProcessRestart();Need(Placed.Blocked&&!Placed.Current&&!Placed.TryFlushForShutdown(),"MP036 failed recovery quarantines mutation and flush");Placed.Stop();Need(Placed.Blocked&&!Placed.CanReset,"MP036 shutdown retains failed-load quarantine even when not dirty");Folder(root,"placed-blocked-new-world");Placed.Start();Need(Placed.Blocked&&!Placed.Current,"MP036 unresolved old scope cannot be replaced by clean new world");
  Folder(root,"placed-invalid");Placed.ProcessRestart();Placed.Seed("owner",float.NaN);Placed.Save();file=Placed.PathName;File.Copy(file+".tmp",file);Placed.ProcessRestart();Need(Placed.Blocked,"MP036 parseable nonfinite record rejected");
  Folder(root,"placed-retain-a");Placed.ProcessRestart();Placed.Seed("retained-owner");var placedA=Placed.PathName;Directory.CreateDirectory(Path.GetDirectoryName(placedA));Directory.CreateDirectory(placedA);Placed.Save();Placed.Stop();Need(Placed.Dirty&&Placed.Count==1,"MP036 shutdown retains failed provenance image");
  Folder(root,"placed-retain-b");Placed.Start();Need(Placed.Dirty&&!Placed.Current,"MP036 replacement world cannot use old bound records");Directory.Delete(placedA);Placed.Start();Need(Placed.Current&&Placed.Count==0&&File.Exists(placedA),"MP036 old path saved before new store opened");
  Console.WriteLine("MPB second-wave production-method persistence cases complete; native identity/network/game lifecycle are doubled.");
 }
}
