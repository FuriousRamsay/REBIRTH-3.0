using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
class Vec { public int x,y,z; public Vec(int a=0,int b=0,int c=0){x=a;y=b;z=c;} public static Vec operator +(Vec a,Vec b)=>new Vec(a.x+b.x,a.y+b.y,a.z+b.z);public override bool Equals(object o)=>o is Vec v&&x==v.x&&y==v.y&&z==v.z;public override int GetHashCode()=>x; }
class Definition { public Vec startPos=new Vec(),size=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags;public string minScript; }
class Prefab {public bool bTraderArea;public string PrefabName="house";public List<Definition> SleeperVolumeList=new List<Definition>{new Definition()};}
class PrefabInstance {public Prefab prefab=new Prefab();public Vec boundingBoxPosition=new Vec(),boundingBoxSize=new Vec(10,10,10);public byte rotation;public List<SleeperVolume> sleeperVolumes=new List<SleeperVolume>();}
class Biome {public string m_sBiomeName="forest";}
class WorldState {public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant();}
class World {public WorldState worldState=new WorldState();public ulong worldTime=1;public Dictionary<int,EntityAlive> Entities=new Dictionary<int,EntityAlive>();public List<SleeperVolume> Volumes=new List<SleeperVolume>(); public bool IsRemote()=>false;public EntityAlive GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;public int FindSleeperVolume(Vec min,Vec max)=>Volumes.FindIndex(v=>v.BoxMin.Equals(min)&&v.BoxMax.Equals(max));public SleeperVolume GetSleeperVolume(int id)=>id>=0&&id<Volumes.Count?Volumes[id]:null;public Biome GetBiome(int x,int z)=>new Biome();}
class EntityBuffs {public Dictionary<string,float> Vars=new Dictionary<string,float>();public bool HasCustomVar(string key)=>Vars.ContainsKey(key);public float GetCustomVar(string key)=>Vars[key];public void SetCustomVar(string key,float value,bool netSync){Vars[key]=value;} }
class EntityAlive {public EntityBuffs Buffs=new EntityBuffs();public World world;public int entityId;public bool Dead;public bool IsDead()=>Dead;}
class EntityPlayer:EntityAlive {public bool IsSpectator;public bool IsAlive()=>!Dead;public Vec GetBlockPosition()=>new Vec();}
class Script {public bool Running;public bool IsRunning()=>Running;}
class Handle {public bool IsCompleted=true;public Action onComplete;}
class SleeperVolume {public PrefabInstance prefabInstance;public Vec BoxMin=new Vec(),BoxMax=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags,numSpawned;public bool isSpawning,wasCleared;public Script minScript;public Dictionary<int,object> respawnMap=new Dictionary<int,object>();public HashSet<int> pendingSpawnMap=new HashSet<int>();public Queue<Handle> pendingSpawnOps=new Queue<Handle>();}
class Decorator {public PrefabInstance Prefab;public PrefabInstance GetPrefabFromWorldPos(int x,int z)=>Prefab;}
class GameStateManager {public bool IsGameStarted()=>true;}
class GameManager {public static GameManager Instance;public World World;public bool IsStartingGame;public GameStateManager gameStateManager=new GameStateManager();public bool IsEditMode()=>false;public Decorator Decorator=new Decorator();public Decorator GetDynamicPrefabDecorator()=>Decorator;}
class ConnectionManager {public bool IsServer=true;}
class SingletonMonoBehaviour<T> {public static T Instance;}
class ThreadManager {public static bool IsMainThread()=>true;}
class GameIO {public static string GetSaveGameDir()=>"unused";}
class Log {public static void Warning(string s){} }

internal class RebirthPurgeObjectiveFrame {internal const int MaximumEligible=200000;}
static class Program {
static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}static void Main(){
string dir=Path.Combine(Path.GetTempPath(),"rebirth-purge-milestone-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);bool live=true,armed=false;string stage="afterCandidate";var binding=new RebirthPoiWorldBinding(new object(),Guid.NewGuid().ToString("N").ToUpperInvariant(),dir,()=>live);var store=new RebirthPurgeMilestoneStore(binding,s=>{if(armed&&s==stage)throw new IOException("fixture");});var forest=new RebirthPurgeMilestoneStore.Receipt("forest",75,4,3,1,1);var desert=new RebirthPurgeMilestoneStore.Receipt("desert",75,8,6,2,1);
try {
Check(store.TryOpen()&&store.Published.Count==0,"new world starts with no fabricated achievement");Check(store.TryRecord(new[]{forest})&&store.Published.Count==1,"confirmed original achievement publishes once");string path=Path.Combine(dir,"RebirthData","Purge","Milestones","world.xml");string first=File.ReadAllText(path);
Check(store.TryRecord(new[]{forest})&&File.ReadAllText(path)==first,"duplicate achievement does not rewrite or mint another receipt");Check(store.TryRecord(new RebirthPurgeMilestoneStore.Receipt[0])&&store.Published.ContainsKey("forest")&&File.ReadAllText(path)==first,"falling current progress cannot erase historical achievement");Check(store.TryRecord(new[]{new RebirthPurgeMilestoneStore.Receipt("forest",90,10,9,5,2)})&&File.ReadAllText(path)==first,"changed threshold and reclear cannot farm original biome achievement");
var cold=new RebirthPurgeMilestoneStore(binding);Check(cold.TryOpen()&&cold.Published["forest"].Revision==1&&cold.Published["forest"].Percent==75,"original receipt survives cold load with earned context");
armed=true;Check(!store.TryRecord(new[]{desert})&&store.Published.Count==1&&File.Exists(path+".candidate"),"failed prepublication write leaves visible history unchanged and retains exact candidate");armed=false;cold=new RebirthPurgeMilestoneStore(binding);Check(cold.TryOpen()&&cold.Published.Count==2&&cold.Published.ContainsKey("desert")&&!File.Exists(path+".candidate"),"cold load reconciles validated original pending achievement exactly once");Check(!store.TryRecord(new[]{desert}),"stale publisher cannot overwrite reconciled history");Check(store.TryOpen()&&store.Published.Count==2,"original publisher reopens positively confirmed successor");
var snow=new RebirthPurgeMilestoneStore.Receipt("snow",75,4,3,3,1);stage="afterPublished";armed=true;Check(!store.TryRecord(new[]{snow})&&store.Published.Count==2,"lost final-write response is withheld until final is re-read");armed=false;Check(store.TryOpen()&&store.Published.Count==3&&store.Published["snow"].Revision==3,"lost final-write response reconciles durable receipt without replay");
live=false;Check(!store.TryRecord(new[]{new RebirthPurgeMilestoneStore.Receipt("wasteland",75,4,3,4,1)})&&store.Published==null,"replaced native world cannot publish or expose previous history");live=true;
var foreign=new RebirthPoiWorldBinding(new object(),Guid.NewGuid().ToString("N").ToUpperInvariant(),dir,()=>true);Check(!new RebirthPurgeMilestoneStore(foreign).TryOpen(),"another saved world identity cannot adopt receipts");
string saved=File.ReadAllText(path);File.WriteAllText(path,"corrupt");Check(!store.TryOpen()&&File.ReadAllText(path)=="corrupt","corrupt saved history is withheld rather than overwritten");File.WriteAllText(path,saved);Check(store.TryOpen(),"restored exact final history can reopen");
File.WriteAllText(path+".candidate",saved.Replace("biome=\"forest\"","biome=\"renamed\""));Check(!store.TryOpen()&&File.ReadAllText(path)==saved,"foreign predecessor or edited candidate cannot replace earned history");File.Delete(path+".candidate");
bool rejected=false;try{new RebirthPurgeMilestoneStore.Receipt("forest",75,0,0,1,1);}catch(ArgumentException){rejected=true;}Check(rejected,"absent biome is not an earned completion");rejected=false;try{new RebirthPurgeMilestoneStore.Receipt("forest",75,4,2,1,1);}catch(ArgumentException){rejected=true;}Check(rejected,"below-target progress cannot create historical completion");
IReadOnlyList<RebirthPurgeDiscoveryPolicy.Definition> definitions;
Check(RebirthPurgeDiscoveryPolicy.TryParse("<purge_discovery version='1'><milestone id='tier_one' percent='25' tier='1'/></purge_discovery>",out definitions)&&definitions.Count==1&&definitions[0].Tier==1,"typed discovery policy accepts bounded authored reward");
Check(!RebirthPurgeDiscoveryPolicy.TryParse("<purge_discovery version='1'><milestone id='completion' percent='25' tier='1'/></purge_discovery>",out definitions),"discovery cannot impersonate completion receipt");
Check(!RebirthPurgeDiscoveryPolicy.TryParse("<purge_discovery version='1'><milestone id='tier_one' percent='25' tier='1'/><milestone id='tier_one' percent='30' tier='2'/></purge_discovery>",out definitions),"duplicate authored discovery identity refused");
Check(store.TryOpen(),"exact original history reopened before discovery");
var discovery=new RebirthPurgeMilestoneStore.Receipt("forest",15,10,2,7,3,"discover_tier_0",0);
Check(store.TryRecord(new[]{discovery})&&store.Published.Count==3&&store.PublishedAll.Count==4,"discovery coexists with old completion API");
string discoveryFile=File.ReadAllText(path);Check(discoveryFile.Contains("version=\"2\"")&&discoveryFile.Contains("discover_tier_0"),"discovery upgrades saved schema explicitly");
var discoveryView=store.PublishedAll;
Check(store.TryRecord(new[]{new RebirthPurgeMilestoneStore.Receipt("forest",25,10,3,8,4,"discover_tier_0",5)})&&ReferenceEquals(discoveryView,store.PublishedAll)&&File.ReadAllText(path)==discoveryFile,"earned discovery tier and threshold survive retuning without rewrite");
var discoveryCold=new RebirthPurgeMilestoneStore(binding);Check(discoveryCold.TryOpen()&&discoveryCold.Published.Count==3&&discoveryCold.PublishedAll.Values.Single(r=>r.Id=="discover_tier_0").Tier==0,"discovery context survives cold load beside v1 achievements");
Console.WriteLine("RESULT "+checks+" PASS; production milestone/domain/binding/codecs and real temporary files; native world doubled; no reward delivery claim.");
}finally{Directory.Delete(dir,true);}
}}