using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;
namespace UnityEngine {public static class Time {public static float realtimeSinceStartup;}}
namespace UnityEngine.Scripting {public class PreserveAttribute:Attribute {}}
public enum NetPackageDirection {ToClient,ToServer}
public class NetPackage {public ClientInfo Sender;public virtual NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){}public virtual void ProcessPackage(World w,GameManager g){}protected bool ValidEntityIdForSender(int id)=>Sender!=null&&Sender.entityId==id;}
public class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){}}
public class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream s):base(s){}}
public static class NetPackageManager {public static T GetPackage<T>()where T:NetPackage,new()=>new T();}
public class ClientInfo {public int entityId;public bool loginDone=true;public List<NetPackage> Sent=new List<NetPackage>();public void SendPackage(NetPackage p)=>Sent.Add(p);}
public class Clients {public List<ClientInfo> List=new List<ClientInfo>();}
public class ConnectionManager {public bool IsServer=true;public Clients Clients=new Clients();public List<NetPackage> Sent=new List<NetPackage>();public void SendToServer(NetPackage p)=>Sent.Add(p);}
public class SingletonMonoBehaviour<T>where T:new(){public static T Instance=new T();}
public class Player {public int entityId=1;}
public class World {public bool Remote;public Player Player=new Player();public bool IsRemote()=>Remote;public Player GetPrimaryPlayer()=>Player;}
public class GameManager {public static GameManager Instance=new GameManager();public World World=new World();public bool IsStartingGame;}
static class RebirthPurgeReleasePolicy {internal static bool Enabled=true;}
static class RebirthSandboxOptionManager {internal static Options Current=new Options();}
class Options {internal bool PoiClearTrackingEnabled=true,IsPurge=true;}
static class Log {public static void Warning(string s)=>Console.WriteLine(s);}
static class RebirthPoiMapPresentation {internal static void Reset(){}}
internal enum RebirthPoiClearanceState {Discovered,Cleared,ResetPending}
internal class RebirthPoiClearanceRecord {internal bool ResetOnly;internal RebirthPoiIdentity Identity;internal long Epoch;internal RebirthPoiClearanceState State;}
internal class RebirthPoiClearanceLedger {internal Dictionary<string,RebirthPoiClearanceRecord> Records=new Dictionary<string,RebirthPoiClearanceRecord>();}
internal class RebirthPoiWorldBinding {internal bool IsCurrent=true;}
internal class RebirthPoiWorldSnapshot {internal Guid WorldId=Guid.NewGuid();internal long Revision=1;internal RebirthPoiWorldBinding Binding=new RebirthPoiWorldBinding();internal Dictionary<string,RebirthPoiClearanceLedger> Shards=new Dictionary<string,RebirthPoiClearanceLedger>();}
internal class RebirthPoiWorldStore {internal RebirthPoiWorldSnapshot Published=new RebirthPoiWorldSnapshot();}
internal class RebirthPoiWorldLifecycle {internal static RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle();internal RebirthPoiWorldStore Store=new RebirthPoiWorldStore();internal bool TryGetEvidenceStore(out RebirthPoiWorldStore s){s=Store;return true;}}
internal class RebirthPurgePoiCensus {internal static RebirthPurgePoiCensus Instance=new RebirthPurgePoiCensus();internal long Generation=1;}
internal class RebirthPurgeObjectiveProgress {internal static RebirthPurgeObjectiveProgress Instance=new RebirthPurgeObjectiveProgress();internal Guid WorldId;internal long Revision;internal int TargetPercentage=75;internal IReadOnlyDictionary<string,BiomeProgress> Published;internal class BiomeProgress {internal string Biome="forest";internal int Eligible=10,Discovered=6,Cleared=4;internal IReadOnlyDictionary<int,int> EligibleByTier=new Dictionary<int,int>{{1,10}},ClearedByTier=new Dictionary<int,int>{{1,4}};}}
class Program
{
 static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static void Main()
 {
 var cm=SingletonMonoBehaviour<ConnectionManager>.Instance;var world=GameManager.Instance.World;var snapshot=RebirthPoiWorldLifecycle.Instance.Store.Published;var progress=RebirthPurgeObjectiveProgress.Instance;progress.WorldId=snapshot.WorldId;progress.Revision=snapshot.Revision;progress.Published=new Dictionary<string,RebirthPurgeObjectiveProgress.BiomeProgress>{{"forest",new RebirthPurgeObjectiveProgress.BiomeProgress()}};
 var custodyIdentity=new RebirthPoiIdentity("noncombat",99,0,0,0,10,10,10,"forest");var custodyShard=new RebirthPoiClearanceLedger();custodyShard.Records.Add(custodyIdentity.Key,new RebirthPoiClearanceRecord{Identity=custodyIdentity,ResetOnly=true,State=RebirthPoiClearanceState.Discovered});snapshot.Shards.Add("custody",custodyShard);
 RebirthPoiMapSync.Reset();var peers=Enumerable.Range(1,3).Select(n=>new ClientInfo{entityId=n}).ToArray();cm.Clients.List.AddRange(peers);var nonce=Guid.NewGuid();foreach(var peer in peers)RebirthPoiMapSync.Request(peer,nonce);
 RebirthPoiMapSync.Pulse();Check(peers.Sum(p=>p.Sent.Count)==4,"combined objective/map traffic respects one global four-packet budget");
 Check(peers.All(p=>p.Sent.OfType<NetPackageRebirthPurgeObjectives>().Count()==1),"initial ledger binding retains waiting objective requests for every peer");
 var package=peers[0].Sent.OfType<NetPackageRebirthPurgeObjectives>().First();RebirthPurgeObjectiveFrame objective;Check(RebirthPurgeObjectiveFrame.TryDecode(Field<byte[]>(package,"bytes"),out objective)&&objective.Request==nonce&&objective.World==snapshot.WorldId&&objective.Known&&objective.Biomes["forest"].Cleared==4,"production transport builds scoped shared counts");
 UnityEngine.Time.realtimeSinceStartup=0.2f;RebirthPoiMapSync.Pulse();Check(peers.All(p=>p.Sent.OfType<NetPackageRebirthPoiMapFrame>().Count()==1),"map pages follow objective summary without starvation");
 var mapPackage=peers[0].Sent.OfType<NetPackageRebirthPoiMapFrame>().Single();RebirthPoiMapFrame custodyFrame;Check(RebirthPoiMapFrame.TryDecode(Field<byte[]>(mapPackage,"bytes"),out custodyFrame)&&custodyFrame.Records.Single().State==RebirthPoiClearanceState.ResetPending,"reset-only custody is hidden by existing map marker state without new wire fields");
 int sent=peers.Sum(p=>p.Sent.Count);UnityEngine.Time.realtimeSinceStartup=0.4f;RebirthPoiMapSync.Pulse();Check(peers.Sum(p=>p.Sent.Count)==sent,"unchanged summaries do not resend or rescan per peer");
 snapshot.Revision=2;UnityEngine.Time.realtimeSinceStartup=0.6f;RebirthPoiMapSync.Pulse();var invalid=peers[0].Sent.OfType<NetPackageRebirthPurgeObjectives>().Last();RebirthPurgeObjectiveFrame unknown;Check(RebirthPurgeObjectiveFrame.TryDecode(Field<byte[]>(invalid,"bytes"),out unknown)&&!unknown.Known&&unknown.Revision==2&&unknown.Sequence>objective.Sequence,"changed durable snapshot with unfinished projection withdraws counts");
 progress.Revision=2;progress.Published=new Dictionary<string,RebirthPurgeObjectiveProgress.BiomeProgress>{{"forest",new RebirthPurgeObjectiveProgress.BiomeProgress()}};UnityEngine.Time.realtimeSinceStartup=0.8f;RebirthPoiMapSync.Pulse();Check(peers.All(p=>p.Sent.OfType<NetPackageRebirthPurgeObjectives>().Count()==3),"new committed progress republishes once to each peer");
 RebirthPoiMapSync.Reset();cm.IsServer=false;world.Remote=true;RebirthPoiMapSync.Pulse();var request=cm.Sent.OfType<NetPackageRebirthPoiMapRequest>().Last();var clientNonce=Field<Guid>(request,"request");
 var clientFrame=new RebirthPurgeObjectiveFrame(clientNonce,snapshot.WorldId,objective.Session,1,2,1,true,new[]{new RebirthPurgeObjectiveFrame.Biome("forest",10,6,4,new Dictionary<int,int>{{1,10}},new Dictionary<int,int>{{1,4}})});
 RebirthPoiMapSync.ReceiveObjectives(world,clientFrame);Check(RebirthPoiMapSync.LocalObjectives==null,"production client stages early summary before map publication");
 RebirthPoiMapSync.Receive(world,new RebirthPoiMapFrame(clientNonce,snapshot.WorldId,objective.Session,Guid.NewGuid(),1,0,true,0,1,0,new RebirthPoiMapRecord[0]));Check(RebirthPoiMapSync.LocalObjectives==clientFrame,"production map publication exposes only matching objective scope");
 RebirthPoiMapSync.Reset();Check(RebirthPoiMapSync.LocalObjectives==null,"world shutdown clears visible objective summary");
 Console.WriteLine("RESULT "+checks+" PASS; production map/objective sync linked; native connection/send/store are doubles.");
 }
}