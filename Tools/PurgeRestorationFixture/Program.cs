using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
class Vec { public int x,y,z; public Vec(int a=0,int b=0,int c=0){x=a;y=b;z=c;} public static Vec operator +(Vec a,Vec b)=>new Vec(a.x+b.x,a.y+b.y,a.z+b.z);public override bool Equals(object o)=>o is Vec v&&x==v.x&&y==v.y&&z==v.z;public override int GetHashCode()=>x; }
class Definition { public Vec startPos=new Vec(),size=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags;public string minScript; }
class DefinitionList {private readonly List<Definition> items=new List<Definition>{new Definition()};public int Count=>items.Count;public Definition this[int index]=>items[index];public void Add(Definition value)=>items.Add(value); }
class Prefab {public bool bTraderArea;public string PrefabName="house";public DefinitionList SleeperVolumeList=new DefinitionList();}
class PrefabInstance {public Prefab prefab=new Prefab();public Vec boundingBoxPosition=new Vec(),boundingBoxSize=new Vec(10,10,10);public byte rotation;public List<SleeperVolume> sleeperVolumes=new List<SleeperVolume>();}
class Biome {public string m_sBiomeName="forest";}
class WorldState {public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant();}
class World {public WorldState worldState=new WorldState();public ulong worldTime=1;public Dictionary<int,EntityAlive> Entities=new Dictionary<int,EntityAlive>();public List<SleeperVolume> Volumes=new List<SleeperVolume>(); public bool IsRemote()=>false;public EntityAlive GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;public int FindSleeperVolume(Vec min,Vec max)=>Volumes.FindIndex(v=>v.BoxMin.Equals(min)&&v.BoxMax.Equals(max));public SleeperVolume GetSleeperVolume(int id)=>id>=0&&id<Volumes.Count?Volumes[id]:null;public Biome GetBiome(int x,int z)=>new Biome();}
class EntityBuffs {public Dictionary<string,float> Vars=new Dictionary<string,float>();public bool HasCustomVar(string key)=>Vars.ContainsKey(key);public float GetCustomVar(string key)=>Vars[key];public void SetCustomVar(string key,float value,bool netSync){Vars[key]=value;} }
class EntityAlive {public EntityBuffs Buffs=new EntityBuffs();public World world;public int entityId;public int entityClass=1;public bool Dead;public bool IsDead()=>Dead;}
class EntityPlayer:EntityAlive {public bool IsSpectator;public bool IsAlive()=>!Dead;public Vec GetBlockPosition()=>new Vec();}
class Script {public bool Running;public bool IsRunning()=>Running;}
class Handle {public bool IsCompleted=true;public Action onComplete;}
class EntityClass {public string entityClassName="zombieArlene";public static Dictionary<int,EntityClass> list=new Dictionary<int,EntityClass>{{1,new EntityClass()}};}
class SleeperVolume {public struct RespawnData {public string className;public int spawnPointIndex;}public int spawnDelay=0;public List<int> spawnsAvailable=new List<int>{0,1};public List<int> respawnList;public static int TickSpawnCount;public PrefabInstance prefabInstance;public Vec BoxMin=new Vec(),BoxMax=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags,numSpawned;public bool isSpawning,wasCleared;public Script minScript;public Dictionary<int,RespawnData> respawnMap=new Dictionary<int,RespawnData>();public HashSet<int> pendingSpawnMap=new HashSet<int>();public Queue<Handle> pendingSpawnOps=new Queue<Handle>();}
class Decorator {public PrefabInstance Prefab;public PrefabInstance GetPrefabFromWorldPos(int x,int z)=>Prefab;}
class GameStateManager {public bool IsGameStarted()=>true;}
class GameManager {public static GameManager Instance;public World World;public bool IsStartingGame;public GameStateManager gameStateManager=new GameStateManager();public bool IsEditMode()=>false;public Decorator Decorator=new Decorator();public Decorator GetDynamicPrefabDecorator()=>Decorator;}
class ConnectionManager {public bool IsServer=true;}
class SingletonMonoBehaviour<T> {public static T Instance;}
class ThreadManager {public static bool IsMainThread()=>true;}
class GameIO {public static string GetSaveGameDir()=>"unused";}
class Log {public static void Warning(string s){} }
static class Program
{
 static int checks;static void Check(bool value,string label){if(!value)throw new Exception("FAIL "+label);checks++;Console.WriteLine("PASS "+label);}
 sealed class Fixture
 {
  public World World;public SleeperVolume Volume;public RebirthPoiWorldStore Store;public RebirthPoiNativeManifest Manifest;public RebirthPoiNativeRestorationWitness Witness;public Guid Token;
  public Fixture()
  {
   World=new World();var prefab=new PrefabInstance();Volume=new SleeperVolume{prefabInstance=prefab,numSpawned=1,respawnList=new List<int>{1}};World.Volumes.Add(Volume);prefab.sleeperVolumes.Add(Volume);GameManager.Instance=new GameManager{World=World};GameManager.Instance.Decorator.Prefab=prefab;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
   if(!RebirthPoiNativeManifest.TryResolve(World,prefab,out Manifest))throw new Exception("mapping setup");
   var dir=Path.Combine(Path.GetTempPath(),"rebirth-purge-restoration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var binding=new RebirthPoiWorldBinding(World,World.worldState.Guid,dir,()=>ReferenceEquals(GameManager.Instance.World,World));RebirthPoiWorldStore.TryOpen(binding,out Store);Store.TryDiscover(Store.Published,Manifest.Identity);
   Token=Guid.NewGuid();Manifest.TryGetDescriptor(Volume,out var descriptor);var journal=new RebirthPoiPartialObservation(Guid.NewGuid(),0,1,new[]{new RebirthPoiVolumeObservation(0,descriptor,new[]{new RebirthPoiActorObservation(Token,1,"zombieArlene",0)})});Store.TryObservePartial(Store.Published,Manifest.Identity,0,journal);Volume.respawnMap.Add(1,new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0});
  }
  public bool Begin()=>RebirthPoiNativeRestorationWitness.TryBegin(Store,Manifest,Volume,0,out Witness);
  public void NativeRestore(){Volume.respawnList.Remove(1);Volume.respawnMap.Remove(1);Volume.respawnMap.Add(2,new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0});Volume.pendingSpawnMap.Add(2);SleeperVolume.TickSpawnCount++;}
  public EntityAlive ActualActor(){var e=new EntityAlive{world=World,entityId=2};World.Entities[2]=e;return e;}
 }
 static void Main()
 {
  var f=new Fixture();Check(f.Begin(),"exact missing native respawn member binds original durable actor");f.NativeRestore();Check(f.Witness.FinishOriginalUpdate(),"actual old removal and one pending replacement prove original restoration branch");var pendingEntity=new EntityAlive{world=f.World,entityId=2};Check(!f.Witness.TryCompleteActualSpawn(pendingEntity,out _),"enqueued pending ID without actual world spawn is not entity proof");var actual=f.ActualActor();Check(f.Witness.TryCompleteActualSpawn(actual,out var successor),"actual successful native callback produces causal successor");Check(successor.Volumes[0].Actors[f.Token].EntityId==2&&successor.Volumes[0].Actors.Count==1,"restoration preserves single logical participant rather than enrolling another");Check(RebirthPoiNativeActorStamp.TryRead(actual,out var token)&&token==f.Token,"actual verified restoration inherits original persisted CVar token");Check(f.Witness.TryCompleteActualSpawn(actual,out var retry)&&retry.Canonical==successor.Canonical,"original restoration callback retry preserves original receipt");Check(f.Store.TryObservePartial(f.Store.Published,f.Manifest.Identity,0,successor)==RebirthPoiStoreResult.Published,"actual production store durably publishes exact restoration lineage");Check(!f.Witness.TryCompleteActualSpawn(actual,out _),"published predecessor cannot produce another replacement transition");Check(RebirthPoiWorldStore.TryOpen(f.Store.Published.Binding,out var cold)==RebirthPoiStoreResult.Published&&cold.Published.TryGet(f.Manifest.Identity,out var reloaded)&&reloaded.Observations.Volumes[0].Actors[f.Token].EntityId==2,"strict cold file retains native successor under original logical participant");
  f=new Fixture();f.Begin();f.Volume.respawnMap.Remove(1);Check(!f.Witness.FinishOriginalUpdate(),"disabled or missing class old-map removal cannot count as restoration or death");
  f=new Fixture();f.Volume.spawnDelay=2;Check(!f.Begin(),"native delayed update cannot capture speculative restoration");
  f=new Fixture();f.World.Entities[1]=new EntityAlive{world=f.World,entityId=1};Check(!f.Begin(),"existing original actor bypasses missing-entity restoration proof");
  f=new Fixture();f.Begin();f.NativeRestore();f.Volume.numSpawned++;Check(!f.Witness.FinishOriginalUpdate(),"regular new enrollment counter increment cannot impersonate restoration");
  f=new Fixture();f.Begin();f.NativeRestore();f.Witness.FinishOriginalUpdate();actual=f.ActualActor();actual.entityClass=99;Check(!f.Witness.TryCompleteActualSpawn(actual,out _),"actual callback class must match original durable participant");
  f=new Fixture();f.Begin();f.NativeRestore();f.Witness.FinishOriginalUpdate();actual=f.ActualActor();actual.Buffs.SetCustomVar("_rbPoiActorGuid0",5,false);Check(!f.Witness.TryCompleteActualSpawn(actual,out _)&&actual.Buffs.Vars.Count==1,"partial existing identity is refused without repair");
  f=new Fixture();f.Begin();f.NativeRestore();GameManager.Instance.World=new World();Check(!f.Witness.FinishOriginalUpdate(),"late original restoration cannot publish into replacement world");
  f=new Fixture();f.Begin();f.NativeRestore();f.Witness.FinishOriginalUpdate();actual=f.ActualActor();f.Store.TryBeginReset(f.Store.Published,f.Manifest.Identity,0,Guid.NewGuid());Check(!f.Witness.TryCompleteActualSpawn(actual,out _),"original pending reset invalidates restoration callback before identity mutation");
  f=new Fixture();f.Begin();f.NativeRestore();f.Volume.prefabInstance.prefab.SleeperVolumeList[0].minScript="changed-script";Check(!f.Witness.FinishOriginalUpdate(),"changed authored volume descriptor refuses original native ID reuse");
  f=new Fixture();f.Volume.respawnMap[1]=new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=-1};Check(!f.Begin(),"legacy unknown spawn point is not guessed into durable lineage");
  Console.WriteLine("RESULT "+checks+" PASS; actual production restoration witness/model/stamp/store; exact native branch effects doubled, real files; no native hooks or reload authorization installed.");
 }
}
