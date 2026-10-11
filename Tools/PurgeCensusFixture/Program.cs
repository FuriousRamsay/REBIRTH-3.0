using System;using System.Collections.Generic;using System.IO;using System.Threading;
class World { public Cache ChunkCache=new Cache();public bool Remote;public bool IsRemote()=>Remote;public Biome GetBiome(int x,int z)=>new Biome(); }
class Cache {public object ChunkProvider;}
class Biome {public string m_sBiomeName="forest";}
class ChunkProviderGenerateWorld {public DynamicPrefabDecorator prefabDecorator;public Location worldLocation=new Location();}
class Location {public string FullPath;}
class DynamicPrefabDecorator {public object listsLock=new object();public List<PrefabInstance> allPrefabs=new List<PrefabInstance>();public event Action<PrefabInstance> OnPrefabLoaded,OnPrefabChanged,OnPrefabRemoved;public void Change()=>OnPrefabChanged?.Invoke(null);}
class PrefabInstance {public Prefab prefab=new Prefab();public V boundingBoxPosition,boundingBoxSize=new V(20,20,20);public int rotation;}
class Prefab {public int yOffset;public bool bTraderArea;public string PrefabName="house";public List<Room> SleeperVolumeList=new List<Room>{new Room()};public byte DifficultyTier=2;}
class Room {public int spawnCountMax=1;public string minScript;}
struct V {public int x,y,z;public V(int a,int b,int c){x=a;y=b;z=c;}}
static class ThreadManager {public static bool IsMainThread()=>true;}
struct Vector3i {public int x,y,z;public static Vector3i Parse(string s){var p=s.Split(',');return new Vector3i{x=int.Parse(p[0]),y=int.Parse(p[1]),z=int.Parse(p[2])};}}
class GameManager {public static GameManager Instance=new GameManager();public World World;public bool IsStartingGame;public DynamicPrefabDecorator Native;public DynamicPrefabDecorator GetDynamicPrefabDecorator()=>Native;}
static class RebirthPurgeReleasePolicy {public static bool Enabled=true;}
static class RebirthSandboxOptionManager {public static Options Current=new Options();}
class Options {public bool IsPurge=true;}
internal enum RebirthPoiClearanceState {Discovered,Cleared,ResetPending}
internal class RebirthPoiClearanceRecord {public RebirthPoiClearanceState State;public bool ResetOnly;}
internal class RebirthPoiWorldBinding {public bool IsCurrent=true;}
internal class RebirthPoiWorldSnapshot {public RebirthPoiWorldBinding Binding=new RebirthPoiWorldBinding();public Guid WorldId=Guid.NewGuid();public long Revision;public Dictionary<string,RebirthPoiClearanceRecord> Records=new Dictionary<string,RebirthPoiClearanceRecord>();public bool TryGet(RebirthPoiIdentity id,out RebirthPoiClearanceRecord record)=>Records.TryGetValue(id.Key,out record);}
internal class RebirthPoiWorldStore {public RebirthPoiWorldSnapshot Published=new RebirthPoiWorldSnapshot();public event Action<RebirthPoiWorldSnapshot,RebirthPoiWorldSnapshot,IReadOnlyList<RebirthPoiIdentity>> PublicationChanged;public void Publish(RebirthPoiWorldSnapshot next,params RebirthPoiIdentity[] ids){var old=Published;Published=next;PublicationChanged?.Invoke(old,next,ids);}}
class RebirthPoiWorldLifecycle {public static RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle();internal RebirthPoiWorldStore Store=new RebirthPoiWorldStore();public bool TryGetStore(out RebirthPoiWorldStore s){s=Store;return true;}}
class Program {
 static int checks;static string dir;
 static void Check(bool pass,string label){if(!pass)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static RebirthPurgePoiCensus Setup(int count,int fileCount=-1){var d=new DynamicPrefabDecorator();for(int n=0;n<count;n++)d.allPrefabs.Add(new PrefabInstance{boundingBoxPosition=new V(n*30,40,0)});var w=new World();w.ChunkCache.ChunkProvider=new ChunkProviderGenerateWorld{prefabDecorator=d,worldLocation=new Location{FullPath=dir}};GameManager.Instance.World=w;GameManager.Instance.Native=d;File.WriteAllText(Path.Combine(dir,"prefabs.xml"),"<prefabs>"+string.Concat(System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0,fileCount<0?count:fileCount),n=>"<decoration name='house' position='"+(n*30)+",40,0'/>"))+"</prefabs>");RebirthPurgePoiCensus.Instance.Reset();return RebirthPurgePoiCensus.Instance;}
 static void Tick(RebirthPurgePoiCensus c){Thread.Sleep(110);c.Pulse();}
 static void Main(){dir=Path.Combine(Path.GetTempPath(),"RebirthCensus_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{
 var c=Setup(130);Tick(c);Check(!c.Ready,"source reading cannot publish a partial denominator");Tick(c);Check(!c.Ready,"first census slice remains unknown");Tick(c);Check(!c.Ready,"second census slice remains unknown");Tick(c);Tick(c);Check(c.Ready&&c.Published.Count==130,"complete bounded census publishes all distinct instances");
 Check(c.Published.Values.GetEnumerator().MoveNext(),"authored entries available");GameManager.Instance.Native.Change();Check(!c.Ready&&c.Published==null,"same-count native replacement immediately invalidates publication");
 c=Setup(1,2);Tick(c);Check(!c.Ready&&c.Unresolved==1,"skipped native source definition cannot reduce denominator");
 c=Setup(1);GameManager.Instance.Native.allPrefabs[0].prefab.bTraderArea=true;Tick(c);Check(c.Ready&&c.Published.Count==0,"trader definition explicitly excluded");
 c=Setup(1);GameManager.Instance.Native.allPrefabs[0].prefab.SleeperVolumeList.Clear();Tick(c);Check(c.Ready&&c.Published.Count==0,"zero-combat definition excluded without fake clear");
 c=Setup(1);GameManager.Instance.Native.allPrefabs[0].prefab=null;Tick(c);Check(!c.Ready&&c.Unresolved==1,"unresolved authored definition remains unknown");
 c=Setup(0);Tick(c);Check(!c.Ready,"empty source is unknown rather than complete");
 c=Setup(1);Tick(c);RebirthSandboxOptionManager.Current.IsPurge=false;Tick(c);Check(!c.Ready&&c.Published==null,"None removes Purge objective state");RebirthSandboxOptionManager.Current.IsPurge=true;
 c=Setup(1);GameManager.Instance.World.Remote=true;Tick(c);Check(!c.Ready,"remote metadata cannot authorize denominator");
 c=Setup(1);File.WriteAllText(Path.Combine(dir,"prefabs.xml"),"<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///nothing'>]><prefabs>&a;</prefabs>");Tick(c);Check(!c.Ready&&c.Unresolved==1,"external entity document refused");
 c=Setup(1);GameManager.Instance.Native.allPrefabs[0].prefab.PrefabName="replacement";Tick(c);Check(!c.Ready&&c.Unresolved==1,"equal-count substituted prefab cannot qualify");
c=Setup(1);GameManager.Instance.Native.allPrefabs[0].boundingBoxPosition=new V(99,40,0);Tick(c);Check(!c.Ready&&c.Unresolved==1,"equal-count moved prefab cannot qualify");
c=Setup(1);GameManager.Instance.Native.allPrefabs[0].prefab.yOffset=3;GameManager.Instance.Native.allPrefabs[0].boundingBoxPosition=new V(0,43,0);File.WriteAllText(Path.Combine(dir,"prefabs.xml"),"<prefabs><decoration name='house' position='0,40,0' y_is_groundlevel='true'/></prefabs>");Tick(c);Check(c.Ready&&c.Published.Count==1,"native ground-level offset matches original authored placement");
c=Setup(1);File.WriteAllText(Path.Combine(dir,"prefabs.xml"),"<prefabs><group><decoration name='house' position='0,40,0'/></group></prefabs>");Tick(c);Check(!c.Ready&&c.Unresolved==1,"nested decoration absent from native loader cannot qualify census");
c=Setup(1);File.WriteAllText(Path.Combine(dir,"prefabs.xml"),"<wrong><decoration name='house' position='0,40,0'/></wrong>");Tick(c);Check(!c.Ready&&c.Unresolved==1,"unexpected source root cannot qualify census");
c=Setup(130);Tick(c);File.AppendAllText(Path.Combine(dir,"prefabs.xml")," ");Tick(c);Check(!c.Ready&&c.Unresolved==1,"source replacement during scan cannot publish a mixed denominator");
c=Setup(2);Tick(c);var progress=new RebirthPurgeObjectiveProgress();var store=RebirthPoiWorldLifecycle.Instance.Store;store.Published=new RebirthPoiWorldSnapshot();progress.Pulse();Check(progress.Published["forest"].Eligible==2&&progress.Published["forest"].Cleared==0,"undiscovered authored POIs contribute to eligible total");
var id=System.Linq.Enumerable.First(c.Published.Values).Identity;store.Published.Records[id.Key]=new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.Cleared};store.Published=new RebirthPoiWorldSnapshot{Revision=1,Records=store.Published.Records};progress.Pulse();Check(progress.Published["forest"].Cleared==1&&progress.Published["forest"].Discovered==1,"only committed matching instances contribute to clearance");
Check(progress.Published["forest"].RemainingFor(75)==1,"milestone remaining count rounds upward");Check(progress.Published["forest"].ClearedByTier[2]==1,"tier progress derives from authored tier");
store.Published=new RebirthPoiWorldSnapshot{Revision=2,Records=new Dictionary<string,RebirthPoiClearanceRecord>{{id.Key,new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.ResetPending}}}};progress.Pulse();Check(progress.Published["forest"].Cleared==0,"reset intent immediately removes current cleared count");
c.Reset();progress.Pulse();Check(progress.Published==null&&progress.WorldId==Guid.Empty,"invalidated denominator withdraws progress projection");
c=Setup(130);for(int n=0;n<12&&!c.Ready;n++)Tick(c);Check(c.Ready&&c.Published.Count==130,"large original authored census qualified for progress starvation regression");
store=RebirthPoiWorldLifecycle.Instance.Store;store.Published=new RebirthPoiWorldSnapshot();progress=new RebirthPurgeObjectiveProgress();
var ordered=System.Linq.Enumerable.ToArray(c.Published.Values);
var binding=store.Published.Binding;var guid=store.Published.WorldId;
for(int n=0;n<5;n++){
 var records=new Dictionary<string,RebirthPoiClearanceRecord>(store.Published.Records);
 records[ordered[n].Identity.Key]=new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.Cleared};
 store.Publish(new RebirthPoiWorldSnapshot{Binding=binding,WorldId=guid,Revision=n+1,Records=records},ordered[n].Identity);
 progress.Pulse();
}
Check(progress.Published!=null&&progress.Published["forest"].Eligible==130&&progress.Published["forest"].Cleared==5&&progress.Revision==5,"continuous committed updates cannot restart or starve original census progress");
var detached=progress.Published["forest"];
var changedRecords=new Dictionary<string,RebirthPoiClearanceRecord>(store.Published.Records);changedRecords[ordered[0].Identity.Key]=new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.ResetPending};
store.Publish(new RebirthPoiWorldSnapshot{Binding=binding,WorldId=guid,Revision=6,Records=changedRecords},ordered[0].Identity);progress.Pulse();
Check(progress.Published["forest"].Cleared==4&&detached.Cleared==5&&detached.ClearedByTier[2]==5,"incremental reset cannot mutate previously published milestone counters");
changedRecords=new Dictionary<string,RebirthPoiClearanceRecord>(store.Published.Records);changedRecords[ordered[0].Identity.Key]=new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.ResetPending,ResetOnly=true};
store.Publish(new RebirthPoiWorldSnapshot{Binding=binding,WorldId=guid,Revision=7,Records=changedRecords},ordered[0].Identity);progress.Pulse();
Check(progress.Published["forest"].Discovered==4,"reset-only undiscovered obligation is not falsely counted as discovery");
for(int n=0;n<100;n++){
 changedRecords=new Dictionary<string,RebirthPoiClearanceRecord>(store.Published.Records);changedRecords[ordered[n].Identity.Key]=new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.Cleared};
 store.Publish(new RebirthPoiWorldSnapshot{Binding=binding,WorldId=guid,Revision=n+8,Records=changedRecords},ordered[n].Identity);
}
long predecessor=progress.Revision;progress.Pulse();
Check(progress.Revision==predecessor,"more than sixty-four dirty identities cannot publish a partial mixed summary");
progress.Pulse();
Check(progress.Revision==107&&progress.Published["forest"].Cleared==100,"bounded continuation publishes exact current snapshot after all dirty identities settle");
changedRecords=new Dictionary<string,RebirthPoiClearanceRecord>(store.Published.Records);changedRecords[ordered[0].Identity.Key]=new RebirthPoiClearanceRecord{State=RebirthPoiClearanceState.ResetPending};
store.Published=new RebirthPoiWorldSnapshot{Binding=binding,WorldId=guid,Revision=108,Records=changedRecords};progress.Pulse();
Check(progress.Published==null,"missing original publication notification withdraws projection and rebuilds conservatively");
progress.Pulse();progress.Pulse();
Check(progress.Revision==108&&progress.Published["forest"].Cleared==99,"notification gap rebuild preserves exact committed current truth");
progress.Reset();c.Reset();
Console.WriteLine("RESULT "+checks+" PASS");
 }finally{RebirthPurgePoiCensus.Instance.Reset();Directory.Delete(dir,true);}}
}
class Log{public static void Warning(string text){}}
