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
class World {public AIDirector aiDirector=new AIDirector();public WorldState worldState=new WorldState();public ulong worldTime=1;public Dictionary<int,EntityAlive> Entities=new Dictionary<int,EntityAlive>();public List<SleeperVolume> Volumes=new List<SleeperVolume>(); public bool IsRemote()=>false;public EntityAlive GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;public int FindSleeperVolume(Vec min,Vec max)=>Volumes.FindIndex(v=>v.BoxMin.Equals(min)&&v.BoxMax.Equals(max));public SleeperVolume GetSleeperVolume(int id)=>id>=0&&id<Volumes.Count?Volumes[id]:null;public Biome GetBiome(int x,int z)=>new Biome();}
class EntityBuffs {public Dictionary<string,float> Vars=new Dictionary<string,float>();public bool HasCustomVar(string key)=>Vars.ContainsKey(key);public float GetCustomVar(string key)=>Vars[key];public void SetCustomVar(string key,float value,bool netSync){Vars[key]=value;} }
class EntityAlive {public Vec position=new Vec();public EntityBuffs Buffs=new EntityBuffs();public World world;public int entityId;public bool Dead;public bool IsDead()=>Dead;}
class Flag { public bool Value; }
class EntityPlayer:EntityAlive {public bool Spawned=true; public bool IsSpawned()=>Spawned; public object AttachedToEntity; public Flag IsFlyMode=new Flag();public bool IsSpectator;public bool IsAlive()=>!Dead;public Vec GetBlockPosition()=>new Vec();}
class Script {public bool Running;public bool IsRunning()=>Running;}
class Handle {public bool IsCompleted=true;public Action onComplete;}
class SleeperVolume {public PrefabInstance prefabInstance;public Vec BoxMin=new Vec(),BoxMax=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags,numSpawned;public bool isSpawning,wasCleared;public Script minScript;public Dictionary<int,object> respawnMap=new Dictionary<int,object>();public HashSet<int> pendingSpawnMap=new HashSet<int>();public Queue<Handle> pendingSpawnOps=new Queue<Handle>();}
class Decorator {public PrefabInstance Prefab;public PrefabInstance GetPrefabFromWorldPosInside(int x,int z)=>Prefab;}
class GameStateManager {public bool IsGameStarted()=>true;}
class GameManager {public static GameManager Instance;public World World;public bool IsStartingGame;public GameStateManager gameStateManager=new GameStateManager();public bool IsEditMode()=>false;public Decorator Decorator=new Decorator();public Decorator GetDynamicPrefabDecorator()=>Decorator;}
class ConnectionManager {public bool IsServer=true;}
class SingletonMonoBehaviour<T> {public static T Instance;}
class ThreadManager {public static bool IsMainThread()=>true;}
class GameIO {public static string GetSaveGameDir()=>"unused";}
class Log {public static void Warning(string s){} }

internal class RebirthPurgeObjectiveFrame {internal const int MaximumEligible=200000;}
namespace UnityEngine { internal static class Time {internal static float realtimeSinceStartup;} }
internal static class RebirthPurgeReleasePolicy {internal static bool Enabled=true;}
internal static class RebirthPoiMapSync {internal static Guid LocalWorld,LocalSession;}
internal sealed class RebirthSandboxOptionManager {internal static readonly RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool IsPurge=true;internal int PurgeProgression=0;internal int PurgeCompletionPercentage=75;}
internal sealed class RebirthPoiWorldLifecycle {internal static readonly RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle();internal RebirthPoiWorldStore Store;internal bool TryGetStore(out RebirthPoiWorldStore store){store=Store;return store!=null;}}
internal sealed class RebirthPurgePoiCensus {internal static readonly RebirthPurgePoiCensus Instance=new RebirthPurgePoiCensus();internal bool Ready=true;internal IReadOnlyDictionary<string,Entry> Published;internal sealed class Entry {internal RebirthPoiIdentity Identity;internal int Tier;}}
internal sealed class RebirthPurgeObjectiveProgress
{
    internal static readonly RebirthPurgeObjectiveProgress Instance=new RebirthPurgeObjectiveProgress();
    internal IReadOnlyDictionary<string,BiomeProgress> Published;internal Guid WorldId;internal long Revision,CensusGeneration=1;internal int TargetPercentage=75;internal void Pulse(){}
    internal sealed class BiomeProgress {internal string Biome;internal int Eligible,Cleared;internal int WeightedEligible,WeightedCleared;internal int EligibleUnits=>RebirthSandboxOptionManager.Current.PurgeProgression==1?WeightedEligible:Eligible;internal int ClearedUnits=>RebirthSandboxOptionManager.Current.PurgeProgression==1?WeightedCleared:Cleared;internal int RemainingFor(int percentage)=>Math.Max(0,RebirthPurgeProgression.Required(EligibleUnits,percentage*75)-ClearedUnits);}
}
class Program
{
    static int checks;static void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
    static RebirthPoiIdentity Poi(int n,string biome="forest")=>new RebirthPoiIdentity("house",n*16,30,0,0,10,10,10,biome);
    static void Step(){UnityEngine.Time.realtimeSinceStartup+=1;RebirthPurgeDiscoveryService.Pulse();}
    static void Main()
    {
        GameManager.Instance=new GameManager{World=new World()};
        var world=GameManager.Instance.World;
        var registeredPlayer=new EntityPlayer{world=world,entityId=1};
        world.Entities.Add(registeredPlayer.entityId,registeredPlayer);
        world.aiDirector.Players.trackedPlayers.list.Add(new Tracked{Player=registeredPlayer});
        string dir=Path.Combine(Path.GetTempPath(),"rebirth-discovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);bool live=true;
        try
        {
            Directory.CreateDirectory(Path.Combine(dir,"Config"));File.WriteAllText(Path.Combine(dir,"Config","_purge_discovery.xml"),"<purge_discovery version='1'><milestone id='discover_tier_0' percent='15' tier='0'/></purge_discovery>");RebirthPurgeDiscoveryPolicy.Load(dir);
            var binding=new RebirthPoiWorldBinding(new object(),Guid.NewGuid().ToString("N").ToUpperInvariant(),dir,()=>live);RebirthPoiWorldStore store;
            Check(RebirthPoiWorldStore.TryOpen(binding,out store)==RebirthPoiStoreResult.Published,"original world opened");RebirthPoiWorldLifecycle.Instance.Store=store;
            Check(store.TryDiscover(store.Published,Poi(100))==RebirthPoiStoreResult.Published,"initial ordinary discovery persisted");
            var progress=RebirthPurgeObjectiveProgress.Instance;progress.WorldId=binding.WorldId;progress.Revision=store.Published.Revision;
            progress.Published=new Dictionary<string,RebirthPurgeObjectiveProgress.BiomeProgress>{{"forest",new RebirthPurgeObjectiveProgress.BiomeProgress{Biome="forest",Eligible=20,Cleared=3}}};
            UnityEngine.Time.realtimeSinceStartup=1;RebirthPurgeMilestoneService.Pulse();
            Check(RebirthPurgeMilestoneService.PublishedAll==null,"startup without map identity cannot persist a milestone");
            RebirthPoiMapSync.LocalWorld=binding.WorldId;RebirthPoiMapSync.LocalSession=Guid.NewGuid();
            UnityEngine.Time.realtimeSinceStartup=2;RebirthPurgeMilestoneService.Pulse();
            Check(RebirthPurgeMilestoneService.PublishedAll.Count==1&&RebirthPurgeMilestoneService.Published.Count==0,"15 percent crossing earns discovery without false completion");
            var entries=new Dictionary<string,RebirthPurgePoiCensus.Entry>();
            for(int n=0;n<65;n++){var identity=Poi(n);entries.Add(identity.Key,new RebirthPurgePoiCensus.Entry{Identity=identity,Tier=0});}
            var wrongBiome=Poi(200,"desert");var wrongTier=Poi(201);entries.Add(wrongBiome.Key,new RebirthPurgePoiCensus.Entry{Identity=wrongBiome,Tier=0});entries.Add(wrongTier.Key,new RebirthPurgePoiCensus.Entry{Identity=wrongTier,Tier=1});
            RebirthPurgePoiCensus.Instance.Published=entries;Step();
            Check(store.Published.Count<=33&&store.Published.Revision<=2,"one service slice publishes at most 32 markers and one revision");
            for(int n=0;n<20;n++)Step();
            Check(store.Published.Count==66,"complete earned tier delivered incrementally");
            Check(!store.Published.TryGet(wrongBiome,out _)&&!store.Published.TryGet(wrongTier,out _),"reward stays in earned biome and tier");
            var final=store.Published;for(int n=0;n<10;n++)Step();Check(ReferenceEquals(final,store.Published),"idle scan performs no duplicate publication");
            RebirthPurgeDiscoveryService.Reset();Step();for(int n=0;n<10;n++)Step();Check(ReferenceEquals(final,store.Published),"restarting scan retains idempotent marker publication");
            RebirthPurgePoiCensus.Instance.Ready=false;var extra=Poi(300);entries.Add(extra.Key,new RebirthPurgePoiCensus.Entry{Identity=extra,Tier=0});Step();Check(!store.Published.TryGet(extra,out _),"incomplete census cannot reveal unqualified marker");
            Check(RebirthPurgeDiscoveryPolicy.ScaledPercent(15,75)==15&&RebirthPurgeDiscoveryPolicy.ScaledPercent(65,75)==65,"legacy default discovery bands retained");
            Check(RebirthPurgeDiscoveryPolicy.ScaledPercent(15,100)==20&&RebirthPurgeDiscoveryPolicy.ScaledPercent(65,50)==44,"discovery bands follow configured completion target");
            int fractional=RebirthPurgeDiscoveryPolicy.ScaledNumerator(25,50);
            Check(fractional==1250&&RebirthPurgeDiscoveryPolicy.Required(3000,fractional)==500,"fractional band admits the historical50/300 boundary without a whole-percent delay");
            Check(RebirthPurgeDiscoveryPolicy.Required(301,fractional)==51,"fractional required count rounds only the final POI count upward");
            Check(RebirthPurgeDiscoveryPolicy.PercentText(fractional)=="16.67"&&RebirthPurgeDiscoveryPolicy.PercentText(1125)=="15","fractional and integer help values use invariant readable formatting");
            var nearby=typeof(RebirthPurgeDiscoveryService).GetMethod("NearPlayer",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var positionPlayer=GameManager.Instance.World.aiDirector.Players.trackedPlayers.list[0].Player;positionPlayer.position=new Vec(5,0,5);
            Check((bool)nearby.Invoke(null,new object[]{new RebirthPoiIdentity("edge",15000,0,0,0,10,10,10,"forest")}),"discovery includes exact 15000 metre boundary");
            Check(!(bool)nearby.Invoke(null,new object[]{new RebirthPoiIdentity("far",15001,0,0,0,10,10,10,"forest")}),"discovery excludes beyond 15000 metres");
            var near = new RebirthPoiIdentity("near",0,0,0,0,10,10,10,"forest");
            world.Entities.Remove(positionPlayer.entityId);Check(!(bool)nearby.Invoke(null,new object[]{near}),"unregistered player cannot drive discovery");world.Entities.Add(positionPlayer.entityId,positionPlayer);
            positionPlayer.AttachedToEntity=new object();Check(!(bool)nearby.Invoke(null,new object[]{near}),"mounted player cannot drive discovery");positionPlayer.AttachedToEntity=null;
            positionPlayer.IsFlyMode.Value=true;Check(!(bool)nearby.Invoke(null,new object[]{near}),"flying player cannot drive discovery");positionPlayer.IsFlyMode.Value=false;
            positionPlayer.Spawned=false;Check(!(bool)nearby.Invoke(null,new object[]{near}),"unspawned player cannot drive discovery");positionPlayer.Spawned=true;
            Check((bool)nearby.Invoke(null,new object[]{near}),"on-foot discovery resumes without rebuilding census");
            progress.Revision=store.Published.Revision;
            progress.Published=new Dictionary<string,RebirthPurgeObjectiveProgress.BiomeProgress>{{"weighted",new RebirthPurgeObjectiveProgress.BiomeProgress{Biome="weighted",Eligible=5,Cleared=2,WeightedEligible=48,WeightedCleared=36}}};
            UnityEngine.Time.realtimeSinceStartup+=1;RebirthPurgeMilestoneService.Pulse();
            Check(!RebirthPurgeMilestoneService.Published.ContainsKey("weighted"),"count mode with40percent rawclear does not complete");
            RebirthSandboxOptionManager.Current.PurgeProgression=1;
            UnityEngine.Time.realtimeSinceStartup+=1;RebirthPurgeMilestoneService.Pulse();
            Check(RebirthPurgeMilestoneService.Published.ContainsKey("weighted")&&RebirthPurgeMilestoneService.Published["weighted"].Mode==1,"same published census reevaluates onmode change and awards75percent weighted completion");
            RebirthSandboxOptionManager.Current.PurgeProgression=0;
            UnityEngine.Time.realtimeSinceStartup+=1;RebirthPurgeMilestoneService.Pulse();
            Check(RebirthPurgeMilestoneService.Published.ContainsKey("weighted"),"switchback preserves earned completion");
            live=false;RebirthPurgePoiCensus.Instance.Ready=true;Step();Check(store.Published==null,"replaced world withholds old markers");
            Console.WriteLine("RESULT "+checks+" PASS; production discovery/milestone/store/domain with real temporary save files; clock, census and engine are doubles; no terrain or gameplay claim.");
        }
        finally{RebirthPurgeDiscoveryService.Reset();RebirthPurgeMilestoneService.Reset();Directory.Delete(dir,true);}
    }
}
class Tracked{public EntityPlayer Player;}class TrackedList{public List<Tracked> list=new List<Tracked>();}
class AIDirectorPlayerManagementComponent{public TrackedList trackedPlayers=new TrackedList();}
class AIDirector{public AIDirectorPlayerManagementComponent Players=new AIDirectorPlayerManagementComponent();public T GetComponent<T>()where T:class=>Players as T;}

// Notification transport is outside this discovery/store fixture.
internal static class RebirthPurgeMilestoneNotification { internal static void Send(RebirthPurgeMilestoneStore.Receipt receipt) {} }
