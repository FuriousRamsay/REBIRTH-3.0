using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
internal sealed class RebirthSandboxOptionManager{internal static readonly RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool IsPurge;}
internal static class RebirthPoiNativeRestorationConnection{internal static bool HasPending(RebirthPoiIdentity identity)=>false;}
internal static class RebirthPurgeReleasePolicy{internal static bool Enabled=>true;}
class EntityClass {public string entityClassName="zombieArlene";public static Dictionary<int,EntityClass> list=new Dictionary<int,EntityClass>{{1,new EntityClass()}};}
class Vec { public int x,y,z; public Vec(int a=0,int b=0,int c=0){x=a;y=b;z=c;} public static Vec operator +(Vec a,Vec b)=>new Vec(a.x+b.x,a.y+b.y,a.z+b.z);public override bool Equals(object o)=>o is Vec v&&x==v.x&&y==v.y&&z==v.z;public override int GetHashCode()=>x; }
class Definition { public Vec startPos=new Vec(),size=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags;public string minScript; }
class Prefab {public byte DifficultyTier;public bool bTraderArea;public string PrefabName="house";public List<Definition> SleeperVolumeList=new List<Definition>{new Definition()};}
class PrefabInstance {public Prefab prefab=new Prefab();public Vec boundingBoxPosition=new Vec(),boundingBoxSize=new Vec(10,10,10);public byte rotation;public List<SleeperVolume> sleeperVolumes=new List<SleeperVolume>();}
class Biome {public string m_sBiomeName="forest";}
class WorldState {public string Guid=System.Guid.NewGuid().ToString("N").ToUpperInvariant();}
class World {public WorldState worldState=new WorldState();public ulong worldTime=1;public Dictionary<int,EntityAlive> Entities=new Dictionary<int,EntityAlive>();public List<SleeperVolume> Volumes=new List<SleeperVolume>(); public bool IsRemote()=>false;public EntityAlive GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;public int ManifestLookups;public int FindSleeperVolume(Vec min,Vec max){ManifestLookups++;return Volumes.FindIndex(v=>v.BoxMin.Equals(min)&&v.BoxMax.Equals(max));}public SleeperVolume GetSleeperVolume(int id)=>id>=0&&id<Volumes.Count?Volumes[id]:null;public Biome GetBiome(int x,int z)=>new Biome();}
class EntityBuffs {public Dictionary<string,float> Vars=new Dictionary<string,float>();public bool HasCustomVar(string key)=>Vars.ContainsKey(key);public float GetCustomVar(string key)=>Vars[key];public void SetCustomVar(string key,float value,bool netSync){Vars[key]=value;} }
class EntityAlive {public EntityBuffs Buffs=new EntityBuffs();public World world;public int entityId;public int entityClass=1;public bool Dead;public bool IsDead()=>Dead;}
class EntityPlayer:EntityAlive {public bool IsSpectator;public bool IsAlive()=>!Dead;public Vec GetBlockPosition()=>new Vec();}
class Script {public bool Running;public bool IsRunning()=>Running;}
class Handle {public bool IsCompleted=true;public Action onComplete;}
class SleeperVolume {public struct RespawnData {public string className;public int spawnPointIndex;}public PrefabInstance prefabInstance;public Vec BoxMin=new Vec(),BoxMax=new Vec(10,10,10);public short spawnCountMin=1,spawnCountMax=2;public int flags,numSpawned;public bool isSpawning,isSpawned,wasCleared;public int Activated;public void UpdatePlayerTouched(World world,EntityPlayer player){Activated++;isSpawned=isSpawning=true;}public Script minScript;public Dictionary<int,RespawnData> respawnMap=new Dictionary<int,RespawnData>();public HashSet<int> pendingSpawnMap=new HashSet<int>();public Queue<Handle> pendingSpawnOps=new Queue<Handle>();}
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
 static int count;static void Check(bool yes,string name){if(!yes)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static void Main()
 {
  var world=new World();GameManager.Instance=new GameManager{World=world};var prefab=new PrefabInstance();var volume=new SleeperVolume{prefabInstance=prefab};prefab.sleeperVolumes.Add(volume);world.Volumes.Add(volume);GameManager.Instance.Decorator.Prefab=prefab;
  string dir=Path.Combine(Path.GetTempPath(),"rebirth-purge-evidence-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var binding=new RebirthPoiWorldBinding(world,world.worldState.Guid,dir,()=>ReferenceEquals(GameManager.Instance.World,world));
  Check(RebirthPoiWorldStore.TryOpen(binding,out var store)==RebirthPoiStoreResult.Published,"real store opens");double time=0;var producer=new RebirthPoiNativeEvidence(()=>store,()=>time);Action pulse=()=>{time++;producer.Pulse();};
  RebirthPoiNativeManifest.TryResolve(world,prefab,out var manifest);Check(manifest!=null,"complete authored native mapping");producer.PlayerTouch(world,new EntityPlayer{world=world});pulse();Check(store.Published.Count==1,"passive discovery saves");
  volume.wasCleared=true;volume.numSpawned=1;producer.NativeDirty(world,volume);pulse();store.Published.TryGet(manifest.Identity,out var record);Check(record.State==RebirthPoiClearanceState.Discovered,"wasCleared and numSpawned alone never proof");
  var entity=new EntityAlive{world=world,entityId=42};world.Entities.Add(42,entity);volume.respawnMap.Add(42,new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0});volume.pendingSpawnMap.Add(42);volume.pendingSpawnOps.Enqueue(new Handle());producer.NativeSpawn(world,volume,entity);entity.Dead=true;var witness=producer.BeforeDeath(volume,entity);Check(witness!=null,"successful native entity plus original membership death witness");volume.respawnMap.Remove(42);producer.AfterDeath(witness);pulse();store.Published.TryGet(manifest.Identity,out record);Check(record.State==RebirthPoiClearanceState.Cleared,"retained finished async handles and historical IDs permit verified clear");producer.AfterDeath(witness);pulse();Check(store.Published.Revision==3,"duplicate death does not republish");Check(!producer.IsWithheld(manifest.Identity),"duplicate death does not replace its receipt or fault original candidate");
  Check(!producer.SuppressPurgeRespawn(world,volume),"None tracking retains native respawns after clear");
  RebirthSandboxOptionManager.Current.IsPurge=true;
  Check(producer.SuppressPurgeRespawn(world,volume),"Purge committed clear suppresses native respawn");
  Check(!producer.SuppressPurgeRespawn(new World(),volume),"replacement world cannot use old clear gate");
  Check(!producer.SuppressPurgeRespawn(world,new SleeperVolume{prefabInstance=prefab}),"unregistered replacement room not suppressed by old proof");
  var clearedReload=new RebirthPoiNativeEvidence(()=>store,()=>time);
  Check(clearedReload.SuppressPurgeRespawn(world,volume),"cold producer restores permanent clear gate from durable ledger");
  var missingPrefab=new PrefabInstance{boundingBoxPosition=new Vec(999)};
  var missingRoom=new SleeperVolume{prefabInstance=missingPrefab};int beforeMiss=world.ManifestLookups;
  Check(!producer.SuppressPurgeRespawn(world,missingRoom)&&world.ManifestLookups>beforeMiss,"unsupported room manifest refusal is positively checked");
  int afterMiss=world.ManifestLookups;
  Check(!producer.SuppressPurgeRespawn(world,missingRoom)&&world.ManifestLookups==afterMiss,"unsupported room repeated gate does not rescan every tick");
  time+=1;
  Check(!producer.SuppressPurgeRespawn(world,missingRoom)&&world.ManifestLookups>afterMiss,"unsupported room retry stays time-bounded and can recover");
  RebirthSandboxOptionManager.Current.IsPurge=false;
  var cold=new RebirthPoiNativeEvidence(()=>store,()=>time);cold.NativeDirty(world,volume);time++;cold.Pulse();Check(store.Published.Revision==3,"cold producer does not invent reload causality");
  var second=new EntityAlive{world=world,entityId=43};world.Entities.Add(43,second);volume.respawnMap.Add(43,new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0});producer.NativeSpawn(world,volume,second);pulse();store.Published.TryGet(manifest.Identity,out record);Check(record.Epoch==1 && record.State==RebirthPoiClearanceState.Discovered,"actual live repopulation invalidates clear with epoch");
  second.Dead=true;var secondWitness=producer.BeforeDeath(volume,second);volume.respawnMap.Remove(43);producer.AfterDeath(secondWitness);volume.numSpawned=2;var unfinished=new Handle{IsCompleted=false};volume.pendingSpawnOps.Enqueue(unfinished);producer.NativeDirty(world,volume);pulse();store.Published.TryGet(manifest.Identity,out record);Check(record.State==RebirthPoiClearanceState.Discovered,"unfinished callback blocks clear");unfinished.IsCompleted=true;unfinished.onComplete=()=>{};producer.NativeDirty(world,volume);pulse();store.Published.TryGet(manifest.Identity,out record);Check(record.State==RebirthPoiClearanceState.Discovered,"failed completion callback blocks clear");unfinished.onComplete=null;producer.NativeDirty(world,volume);pulse();store.Published.TryGet(manifest.Identity,out record);Check(record.State==RebirthPoiClearanceState.Cleared,"completed native actors re-clear new epoch");
  volume.wasCleared=false;volume.numSpawned=0;producer.NativeReset(world,volume);Check(producer.IsWithheld(manifest.Identity),"observed native reset withholds volatile marker authority");
  var resetTx=Guid.NewGuid();Check(store.TryBeginReset(store.Published,manifest.Identity,1,resetTx)==RebirthPoiStoreResult.Published,"original reset intent published for enrollment renewal");
  var withheldActor=new EntityAlive{world=world,entityId=44};world.Entities[44]=withheldActor;volume.respawnMap[44]=new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0};producer.NativeSpawn(world,volume,withheldActor);Check(producer.IsWithheld(manifest.Identity),"pending reset cannot retire original fault or enroll new generation");volume.respawnMap.Clear();
  Check(store.TryFinishReset(store.Published,manifest.Identity,1,resetTx,RebirthPoiResetDisposition.Completed)==RebirthPoiStoreResult.Published,"positive original terminal receipt advances saved generation");
  volume.numSpawned=1;volume.pendingSpawnMap.Clear();volume.pendingSpawnOps.Clear();volume.respawnMap[44]=new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0};producer.NativeSpawn(world,volume,withheldActor);pulse();pulse();store.Published.TryGet(manifest.Identity,out var renewed);
  Check(!producer.IsWithheld(manifest.Identity)&&renewed.Epoch==2&&renewed.Observations.Volumes.Values.Single().Actors.Count==1,"durable completed reset restores enrollment with new exact generation");
  producer.AfterDeath(secondWitness);pulse();Check(!producer.IsWithheld(manifest.Identity)&&store.Published.TryGet(manifest.Identity,out renewed)&&renewed.State==RebirthPoiClearanceState.Discovered,"late old generation death cannot clear renewed room");
  withheldActor.Dead=true;var renewedDeath=producer.BeforeDeath(volume,withheldActor);volume.respawnMap.Remove(44);volume.wasCleared=true;producer.AfterDeath(renewedDeath);pulse();store.Published.TryGet(manifest.Identity,out renewed);Check(renewed.State==RebirthPoiClearanceState.Cleared&&renewed.Epoch==2,"new positively witnessed death can reclear completed reset generation");
  volume.wasCleared=false;volume.numSpawned=0;producer.NativeNaturalReset(world,volume);store.Published.TryGet(manifest.Identity,out renewed);Check(renewed.State==RebirthPoiClearanceState.Cleared&&renewed.Epoch==2,"natural reset alone cannot assert repopulation or alter saved clear");
  var naturalActor=new EntityAlive{world=world,entityId=45};world.Entities[45]=naturalActor;volume.respawnMap[45]=new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0};volume.numSpawned=1;producer.NativeSpawn(world,volume,naturalActor);pulse();pulse();store.Published.TryGet(manifest.Identity,out renewed);Check(renewed.Epoch==3&&renewed.State==RebirthPoiClearanceState.Discovered&&renewed.Observations.Volumes.Values.Single().Actors.Count==1,"actual live enrollment after native natural reset begins exact new generation");
  producer.NativeNaturalReset(world,volume);naturalActor.Dead=true;var naturalDeath=producer.BeforeDeath(volume,naturalActor);Check(naturalDeath!=null,"natural observer cannot erase a living current actor obligation");volume.respawnMap.Remove(45);volume.wasCleared=true;producer.AfterDeath(naturalDeath);pulse();store.Published.TryGet(manifest.Identity,out renewed);Check(renewed.Epoch==3&&renewed.State==RebirthPoiClearanceState.Cleared,"natural generation reclears from new native count and positive death");
  world.Volumes.Clear();Check(!RebirthPoiNativeManifest.TryResolve(world,prefab,out _),"missing authored native volume refuses manifest");world.Volumes.Add(volume);var extra=new SleeperVolume{prefabInstance=prefab,BoxMin=new Vec(20),BoxMax=new Vec(30)};prefab.sleeperVolumes.Add(extra);Check(!RebirthPoiNativeManifest.TryResolve(world,prefab,out _),"extra combat runtime volume refuses manifest");prefab.sleeperVolumes.Remove(extra);prefab.prefab.bTraderArea=true;Check(!RebirthPoiNativeManifest.TryResolve(world,prefab,out _),"trader exclusion explicit");prefab.prefab.bTraderArea=false;GameManager.Instance.World=new World();Check(!RebirthPoiNativeManifest.TryResolve(world,prefab,out _),"replacement world rejects late native evidence");
  GameManager.Instance.World=world;var stamped=new EntityAlive{world=world,entityId=90};world.Entities.Add(90,stamped);Check(RebirthPoiNativeActorStamp.TryAssignForVerifiedEnrollment(stamped,out var stamp),"verified native actor stamp assigns exact128bit token");Check(stamped.Buffs.Vars.Count==8&&stamped.Buffs.Vars.Values.All(v=>v>=1&&v<=65536),"stamp segments exact nonzero float save range");Check(RebirthPoiNativeActorStamp.TryAssignForVerifiedEnrollment(stamped,out var again)&&stamp==again,"stamp assignment retry immutable");var loaded=new EntityAlive{world=world,entityId=90};loaded.Buffs.Vars=new Dictionary<string,float>(stamped.Buffs.Vars);Check(RebirthPoiNativeActorStamp.TryRead(loaded,out var restored)&&restored==stamp,"restored native CVar values retain exact token");loaded.Buffs.Vars.Remove("_rbPoiActorGuid0");Check(!RebirthPoiNativeActorStamp.TryRead(loaded,out _),"partial native stamp refused");world.Entities[90]=loaded;Check(!RebirthPoiNativeActorStamp.TryAssignForVerifiedEnrollment(loaded,out _),"partial native stamp never repaired by minting");loaded.Buffs.Vars["_rbPoiActorGuid0"]=float.NaN;Check(!RebirthPoiNativeActorStamp.TryRead(loaded,out _),"NaN stamp refuses");loaded.Buffs.Vars["_rbPoiActorGuid0"]=1.5f;Check(!RebirthPoiNativeActorStamp.TryRead(loaded,out _),"fractional stamp refuses");
  // Independent fault fixture preserves the original proof receipt/world time on retry.
  var faultWorld=new World();GameManager.Instance.World=faultWorld;var faultPrefab=new PrefabInstance();var fv=new SleeperVolume{prefabInstance=faultPrefab};faultPrefab.sleeperVolumes.Add(fv);faultWorld.Volumes.Add(fv);GameManager.Instance.Decorator.Prefab=faultPrefab;
  var faultDir=Path.Combine(Path.GetTempPath(),"rebirth-purge-evidence-fault-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(faultDir);bool armed=false;var faultBinding=new RebirthPoiWorldBinding(faultWorld,faultWorld.worldState.Guid,faultDir,()=>ReferenceEquals(GameManager.Instance.World,faultWorld));RebirthPoiWorldStore.TryOpen(faultBinding,out var faultStore,stage=>{if(armed&&stage=="afterManifestCandidateWritten")throw new IOException("fixture fault");});
  var fp=new RebirthPoiNativeEvidence(()=>faultStore,()=>time);fp.PlayerTouch(faultWorld,new EntityPlayer{world=faultWorld});time++;fp.Pulse();RebirthPoiNativeManifest.TryResolve(faultWorld,faultPrefab,out var fm);
  var fe=new EntityAlive{world=faultWorld,entityId=70};faultWorld.Entities.Add(70,fe);fv.respawnMap.Add(70,new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0});fp.NativeSpawn(faultWorld,fv,fe);fe.Dead=true;var fw=fp.BeforeDeath(fv,fe);fv.respawnMap.Remove(70);fp.AfterDeath(fw);fv.wasCleared=true;fv.numSpawned=2;time++;fp.Pulse();faultStore.Published.TryGet(fm.Identity,out var fr);Check(fr.State==RebirthPoiClearanceState.Discovered,"missing class count cannot stand in for successful actor");fv.numSpawned=1;
  armed=true;fp.NativeDirty(faultWorld,fv);time++;fp.Pulse();Check(faultStore.HasPending,"failed clear publication retains durable original candidate");string candidate=File.ReadAllText(Path.Combine(faultBinding.Directory,"world.xml.candidate"));faultWorld.worldTime=400;armed=false;Check(faultStore.TryRetryPending()==RebirthPoiStoreResult.Published,"original clear candidate retry publishes");time++;fp.Pulse();faultStore.Published.TryGet(fm.Identity,out fr);Check(fr.State==RebirthPoiClearanceState.Cleared&&faultStore.Published.Manifest==candidate,"producer does not replace successful pending proof after time advances");
  var re=new EntityAlive{world=faultWorld,entityId=71};faultWorld.Entities.Add(71,re);fv.respawnMap.Add(71,new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0});fp.NativeSpawn(faultWorld,fv,re);armed=true;time++;fp.Pulse();Check(faultStore.HasPending,"uncertain repopulation retains original generation candidate");
  re.Dead=true;var rw=fp.BeforeDeath(fv,re);fv.respawnMap.Remove(71);fp.AfterDeath(rw);fv.numSpawned=2;faultWorld.worldTime=800;armed=false;Check(faultStore.TryRetryPending()==RebirthPoiStoreResult.Published,"repopulation original receipt publishes after actor death");time++;fp.Pulse();faultStore.Published.TryGet(fm.Identity,out fr);Check(fr.Epoch==1&&fr.State==RebirthPoiClearanceState.Cleared,"exact persisted repopulation receipt reconciles epoch and allows subsequent verified clear");
  fp.Reset();var late=fp.BeforeDeath(fv,fe);Check(late==null,"reset session cannot re-enroll dead entity");
  // Missing a second authored combat volume is never waived by clearing the first.
  var secondDef=new Definition{startPos=new Vec(20)};faultPrefab.prefab.SleeperVolumeList.Add(secondDef);Check(!RebirthPoiNativeManifest.TryResolve(faultWorld,faultPrefab,out _),"unloaded second authored room withholds full clearance");var sv=new SleeperVolume{prefabInstance=faultPrefab,BoxMin=new Vec(20),BoxMax=new Vec(30,10,10)};faultWorld.Volumes.Add(sv);faultPrefab.sleeperVolumes.Add(sv);Check(RebirthPoiNativeManifest.TryResolve(faultWorld,faultPrefab,out var two)&&two.Volumes.Length==2,"complete second room mapping includes both combat volumes");
  // Cold partial-room continuation uses saved positive actor proof, never counters alone.
  var reloadWorld=new World();GameManager.Instance.World=reloadWorld;
  var reloadPrefab=new PrefabInstance();reloadPrefab.prefab.SleeperVolumeList.Add(new Definition{startPos=new Vec(20)});
  var roomA=new SleeperVolume{prefabInstance=reloadPrefab,numSpawned=1};
  var roomB=new SleeperVolume{prefabInstance=reloadPrefab,BoxMin=new Vec(20),BoxMax=new Vec(30,10,10)};
  reloadWorld.Volumes.Add(roomA);reloadWorld.Volumes.Add(roomB);reloadPrefab.sleeperVolumes.Add(roomA);reloadPrefab.sleeperVolumes.Add(roomB);
  GameManager.Instance.Decorator.Prefab=reloadPrefab;
  var reloadDir=Path.Combine(Path.GetTempPath(),"rebirth-purge-partial-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(reloadDir);
  var reloadBinding=new RebirthPoiWorldBinding(reloadWorld,reloadWorld.worldState.Guid,reloadDir,()=>ReferenceEquals(GameManager.Instance.World,reloadWorld));
  RebirthPoiWorldStore.TryOpen(reloadBinding,out var reloadStore);
  var partial=new RebirthPoiNativeEvidence(()=>reloadStore,()=>time);
  var ra=new EntityAlive{world=reloadWorld,entityId=101};reloadWorld.Entities[101]=ra;
  roomA.respawnMap[101]=new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0};
  partial.NativeSpawn(reloadWorld,roomA,ra);time++;partial.Pulse();
  RebirthPoiNativeManifest.TryResolve(reloadWorld,reloadPrefab,out var reloadManifest);
  reloadStore.Published.TryGet(reloadManifest.Identity,out var partialRecord);
  Check(partialRecord.Observations!=null && partialRecord.Observations.Volumes.Count==2 &&
    partialRecord.Observations.Volumes[0].Actors.Count==1 && partialRecord.Observations.Volumes[1].Actors.Count==0,"partial journal includes unopened authored room");
  var tokenA=partialRecord.Observations.Volumes[0].Actors.Values.Single().Token;
  partial.Reset();reloadWorld.Entities.Remove(101);
  Check(RebirthPoiWorldStore.TryOpen(reloadBinding,out var reopened)==RebirthPoiStoreResult.Published,"partial journal reopens real persisted files");
  var continuation=new RebirthPoiNativeEvidence(()=>reopened,()=>time);
  continuation.NativeDirty(reloadWorld,roomA);time++;continuation.Pulse();
  reopened.Published.TryGet(reloadManifest.Identity,out partialRecord);
  Check(partialRecord.State==RebirthPoiClearanceState.Discovered && !partialRecord.Observations.Volumes[0].Actors[tokenA].Dead,"unloaded actor remains living obligation");
  reloadWorld.Entities[101]=ra;ra.Dead=true;var resumedDeath=continuation.BeforeDeath(roomA,ra);
  Check(resumedDeath!=null,"cold restored stamp and exact membership authorize original death witness");
  roomA.respawnMap.Remove(101);roomA.wasCleared=true;continuation.AfterDeath(resumedDeath);time++;continuation.Pulse();
  reopened.Published.TryGet(reloadManifest.Identity,out partialRecord);
  Check(partialRecord.State==RebirthPoiClearanceState.Discovered && partialRecord.Observations.Volumes[0].Actors[tokenA].Dead,"saved first-room death never clears unopened second room");
  continuation.Reset();var finalRoom=new RebirthPoiNativeEvidence(()=>reopened,()=>time);
  var rb=new EntityAlive{world=reloadWorld,entityId=102};reloadWorld.Entities[102]=rb;roomB.numSpawned=1;
  roomB.respawnMap[102]=new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0};
  finalRoom.NativeSpawn(reloadWorld,roomB,rb);rb.Dead=true;var finalDeath=finalRoom.BeforeDeath(roomB,rb);
  roomB.respawnMap.Remove(102);roomB.wasCleared=true;finalRoom.AfterDeath(finalDeath);time++;finalRoom.Pulse();
  reopened.Published.TryGet(reloadManifest.Identity,out partialRecord);
  Check(partialRecord.State==RebirthPoiClearanceState.Cleared && partialRecord.Observations.Volumes.Values.All(v=>v.Actors.Values.All(a=>a.Dead)),"both rooms clear across cold producer reload using durable positive deaths");  Check(RebirthPoiRemainingRoomActivation.Eligible(3,1,1)&&!RebirthPoiRemainingRoomActivation.Eligible(3,0,1)&&!RebirthPoiRemainingRoomActivation.Eligible(3,1,3),"legacy tier and remaining-room activation thresholds");
  RebirthSandboxOptionManager.Current.IsPurge=true;roomB.wasCleared=false;roomB.isSpawned=false;roomB.isSpawning=false;
  Check(RebirthPoiRemainingRoomActivation.TryActivateOne(reloadManifest,new EntityPlayer{world=reloadWorld})&&roomB.Activated==1,"native remaining-room touch is invoked once");
  Check(!RebirthPoiRemainingRoomActivation.TryActivateOne(reloadManifest,new EntityPlayer{world=reloadWorld}),"already spawning room is not retriggered");
  RebirthSandboxOptionManager.Current.IsPurge=false;
  // An optional room that genuinely spawned zero actors must not block a combat POI.
  var optionalWorld=new World();GameManager.Instance.World=optionalWorld;
  var optionalPrefab=new PrefabInstance();optionalPrefab.prefab.SleeperVolumeList.Add(new Definition{startPos=new Vec(20)});
  var combatRoom=new SleeperVolume{prefabInstance=optionalPrefab,numSpawned=1};
  var emptyRoom=new SleeperVolume{prefabInstance=optionalPrefab,BoxMin=new Vec(20),BoxMax=new Vec(30,10,10),numSpawned=0,wasCleared=true};
  optionalWorld.Volumes.Add(combatRoom);optionalWorld.Volumes.Add(emptyRoom);optionalPrefab.sleeperVolumes.Add(combatRoom);optionalPrefab.sleeperVolumes.Add(emptyRoom);
  GameManager.Instance.Decorator.Prefab=optionalPrefab;
  var optionalDir=Path.Combine(Path.GetTempPath(),"rebirth-purge-optional-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(optionalDir);
  var optionalBinding=new RebirthPoiWorldBinding(optionalWorld,optionalWorld.worldState.Guid,optionalDir,()=>ReferenceEquals(GameManager.Instance.World,optionalWorld));
  RebirthPoiWorldStore.TryOpen(optionalBinding,out var optionalStore);var optionalProducer=new RebirthPoiNativeEvidence(()=>optionalStore,()=>time);
  var enemy=new EntityAlive{world=optionalWorld,entityId=201};optionalWorld.Entities[201]=enemy;
  combatRoom.respawnMap[201]=new SleeperVolume.RespawnData{className="zombieArlene",spawnPointIndex=0};
  optionalProducer.NativeSpawn(optionalWorld,combatRoom,enemy);enemy.Dead=true;var optionalDeath=optionalProducer.BeforeDeath(combatRoom,enemy);
  combatRoom.respawnMap.Clear();combatRoom.wasCleared=true;optionalProducer.AfterDeath(optionalDeath);time++;optionalProducer.Pulse();
  RebirthPoiNativeManifest.TryResolve(optionalWorld,optionalPrefab,out var optionalManifest);optionalStore.Published.TryGet(optionalManifest.Identity,out var optionalRecord);
  Check(optionalRecord.State==RebirthPoiClearanceState.Cleared,"verified combat clear accepts genuinely empty completed optional room");
  Console.WriteLine("RESULT "+count+" PASS; actual production manifest/evidence/store; native methods and events doubled, real temp files. Harmony installation separately parent compiled.");
 }
}




internal static class RebirthPurgeKillContributor {internal static Func<EntityAlive,string> Resolve;internal static string Capture(EntityAlive entity)=>Resolve?.Invoke(entity);}
