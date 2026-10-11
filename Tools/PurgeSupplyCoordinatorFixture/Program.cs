using System;using System.Collections.Generic;using System.Linq;using UnityEngine;
class Program
{
 static int checks;static readonly string Key=new string('a',64),Other=new string('b',64);
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static (World,EntityPlayer,AIDirectorAirDropComponent) Setup()
 {
  RebirthPurgeSupplyCoordinator.Reset();Time.realtimeSinceStartup=0;GameTimer.Instance.ticks=0;
  var world=new World{worldState=new WorldState{Guid=Guid.NewGuid().ToString("N")}};
  var director=new AIDirector{World=world};world.aiDirector=director;
  var component=new AIDirectorAirDropComponent{Director=director};director.Supply=component;director.Players=new AIDirectorPlayerManagementComponent();
  var player=new EntityPlayer{world=world,entityId=1,Owner=Key,position=new Vector3(14,61,-993)};
  world.Entities.Add(player);director.Players.trackedPlayers.list.Add(new Tracked{Player=player});
  GameManager.Instance=new GameManager{World=world};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true};
  RebirthPurgeReleasePolicy.Enabled=true;RebirthSandboxOptionManager.Current.IsPurge=true;ThreadManager.Main=true;LootContainer.NoLoot=false;
  RebirthPoiWorldLifecycle.Instance.Store=new RebirthPoiWorldStore{Published=new RebirthPoiWorldSnapshot{Binding=new RebirthPoiWorldBinding{WorldId=Guid.Parse(world.worldState.Guid),IsCurrent=true}}};
  var account=new RebirthPurgeSupplyAccount(Key,75,75,1);account.TryReserve(new RebirthPurgeSupplyDeliveryPlan(20,90,-993),out account);
  RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{Key,account}};RebirthPurgeSupplyService.Allow=true;RebirthPurgeSupplyService.Starts=0;
  RebirthPurgeSupplyCrateSerialization.Stamps.Clear();RebirthPurgeSupplySaveProof.Queues.Clear();RebirthPurgeSupplySaveProof.Ready.Clear();RebirthPurgeSupplySaveProof.Refresh=true;AIAirDrop.Planes=0;
  return(world,player,component);
 }
 static void Step(int n=1,bool paused=false){for(int i=0;i<n;i++){Time.realtimeSinceStartup+=1;if(!paused)GameTimer.Instance.ticks+=20;RebirthPurgeSupplyCoordinator.Pulse();}}
 static EntitySupplyCrate Recover(World world,string key,int id)
 {
  var guid=Guid.Parse(world.worldState.Guid);var account=new RebirthPurgeSupplyAccount(key,75,75,1);account.TryReserve(new RebirthPurgeSupplyDeliveryPlan(20,90,-993),out account);account.TryStart(guid,account.Token(guid),out account);account.TryBeginCrate(guid,account.Token(guid),out account);
  var accounts=new Dictionary<string,RebirthPurgeSupplyAccount>(RebirthPurgeSupplyService.Published);accounts[key]=account;RebirthPurgeSupplyService.Published=accounts;
  var crate=new EntitySupplyCrate{world=world,entityId=id,position=new Vector3(20,61,-993),onGround=true};world.Entities.Add(crate);
  RebirthPurgeSupplyCrateSerialization.Stamps[crate]=new RebirthPurgeSupplyCrateStamp(guid,key,1,account.Token(guid));return crate;
 }
 static void Main()
 {
  var (world,player,component)=Setup();bool result=true;
  Check(!RebirthPurgeSupplyCoordinator.BeforeScheduled(component,ref result)&&!result,"enabled Purge suppresses ordinary scheduled native drops");
  Check(!RebirthPurgeSupplyCoordinator.BeforeDirectorTick(component),"empty native director cannot start ordinary schedule in Purge");
  component.activeAirDrop=new AIAirDrop(component,world,new List<EntityPlayer>{player});
  Check(RebirthPurgeSupplyCoordinator.BeforeDirectorTick(component),"preexisting native flight is allowed to finish");
  component.activeAirDrop=null;RebirthSandboxOptionManager.Current.IsPurge=false;
  Check(RebirthPurgeSupplyCoordinator.BeforeScheduled(component,ref result)&&RebirthPurgeSupplyCoordinator.BeforeDirectorTick(component),"None keeps ordinary native scheduling");
  RebirthSandboxOptionManager.Current.IsPurge=true;SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
  Check(RebirthPurgeSupplyCoordinator.BeforeScheduled(component,ref result),"remote client cannot suppress server scheduling");
  (world,player,component)=Setup();Step(1);Step(20,true);
  Check(AIAirDrop.Planes==0&&component.Spawns==0,"paused game time cannot advance earned flight");
  Step(12);
  Check(AIAirDrop.Planes==1&&component.Spawns==1,"bounded coordinator produces one original native plane and crate");
  Check(RebirthPurgeSupplyService.Published[Key].Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.CrateStarted&&RebirthPurgeSupplyService.Published[Key].DeliveredDrops==0,"live crate alone cannot consume reward");
  Step(8);Check(component.Spawns==1,"unconfirmed crate cannot cause another launch");
  RebirthPurgeSupplySaveProof.Ready.Add(Key);Step(2);
  Check(RebirthPurgeSupplyService.Published[Key].DeliveredDrops==1,"positive typed saved receipt completes original account");
  Step(8);Check(component.Spawns==1,"completed original is not delivered twice");
  (world,player,component)=Setup();component.ThrowAfterRegistration=true;Step(14);
  Check(component.Spawns==1&&world.RemovedObservers==0&&RebirthPurgeSupplySaveProof.Queues.ContainsKey(Key),"registered original survives native exception and retains observer until proof");
  RebirthPurgeSupplySaveProof.Ready.Add(Key);Step(2);
  Check(RebirthPurgeSupplyService.Published[Key].DeliveredDrops==1&&world.RemovedObservers==1,"saved recovered original completes and releases retained observer");
  Step(8);RebirthPurgeSupplyCoordinator.Reset();Check(component.Spawns==1&&world.RemovedObservers==1,"recovered original neither respawns nor releases observer twice");
  (world,player,component)=Setup();component.ThrowAfterCache=true;Step(14);RebirthPurgeSupplySaveProof.Ready.Add(Key);Step(2);
  Check(RebirthPurgeSupplyService.Published[Key].DeliveredDrops==1&&world.RemovedObservers==0,"exception after native cache insertion preserves native observer ownership");
  (world,player,component)=Setup();component.ThrowAfterRegistration=true;Step(14);RebirthPurgeSupplyCoordinator.Reset();
  Check(world.RemovedObservers==1&&RebirthPurgeSupplyService.Published[Key].DeliveredDrops==0,"same-world reset releases retained observer without falsely completing delivery");
  (world,player,component)=Setup();Step(1);var cold=RebirthPurgeSupplyAccount.Read(RebirthPurgeSupplyService.Published[Key].Write());RebirthPurgeSupplyCoordinator.Reset();RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{Key,cold}};Step(12);
  Check(component.Spawns==1&&RebirthPurgeSupplyService.Starts==1,"cold pre-crate original resumes without re-earning or duplicate admission");
  (world,player,component)=Setup();var first=Recover(world,Key,20);Step(2);RebirthPurgeSupplySaveProof.Ready.Add(Key);Step(2);
  Check(component.Spawns==0&&RebirthPurgeSupplyService.Published[Key].DeliveredDrops==1,"cold owned crate recovery confirms existing original without launching");
  (world,player,component)=Setup();first=Recover(world,Key,20);var second=Recover(world,Other,21);RebirthPurgeSupplySaveProof.Ready.Add(Other);Step(8);
  Check(RebirthPurgeSupplyService.Published[Key].DeliveredDrops==0&&RebirthPurgeSupplyService.Published[Other].DeliveredDrops==1,"delayed original save cannot starve another owner confirmation");
  Check(RebirthPurgeSupplySaveProof.Queues[Key]<=2,"native save retries remain bounded for delayed original");
  (world,player,component)=Setup();first=Recover(world,Key,20);Step(3);RebirthPurgeSupplySaveProof.Refresh=false;int queued=RebirthPurgeSupplySaveProof.Queues[Key];Step(10);
  Check(RebirthPurgeSupplySaveProof.Queues[Key]==queued,"pending background proof is retained rather than abandoned and requeued");
  (world,player,component)=Setup();first=Recover(world,Key,20);first.Dead=true;Step(8);
  Check(component.Spawns==0&&RebirthPurgeSupplyService.Published[Key].DeliveredDrops==0,"dead recovered crate grants neither completion nor duplicate");
  (world,player,component)=Setup();for(int i=0;i<12;i++)component.supplyCrates.Add(i);Step(10);
  Check(component.Spawns==0&&RebirthPurgeSupplyService.Starts==0,"native full capacity cannot discard older crate for earned drop");
  (world,player,component)=Setup();LootContainer.NoLoot=true;Step(10);
  Check(component.Spawns==0&&RebirthPurgeSupplyService.Starts==0,"no-loot setting does not falsely admit delivery");
  (world,player,component)=Setup();RebirthPoiWorldLifecycle.Instance.Store.Published.Binding.IsCurrent=false;Step(10);
  Check(component.Spawns==0&&RebirthPurgeSupplyService.Starts==0,"replaced original save cannot begin native flight");
  (world,player,component)=Setup();RebirthPurgeReleasePolicy.Enabled=false;Step(10);
  Check(component.Spawns==0&&RebirthPurgeSupplyService.Starts==0,"disabled whole release performs no supply work");
  (world,player,component)=Setup();RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>();world.aiDirector.Players.trackedPlayers.list.Clear();
  for(int i=0;i<100;i++)world.aiDirector.Players.trackedPlayers.list.Add(new Tracked{Player=new EntityPlayer{world=world,entityId=i+2,Owner=Other}});
  RebirthPurgeKillContributor.Calls=0;Step(1);
  Check(RebirthPurgeKillContributor.Calls==16,"large player selection inspects at most sixteen authenticated candidates per second");
  (world,player,component)=Setup();first=Recover(world,Key,20);world.Entities.list.Remove(first);world.Entities.dict.Remove(first.entityId);world.Loaded=false;Step(1);
  Check(world.Observers==1&&component.Spawns==0,"cold original destination gets one native load observer without spawning a replacement");
  Step(10);Check(world.Observers==1&&world.RemovedObservers==0,"original recovery holds only one bounded observer while loading");
  Step(6);Check(world.RemovedObservers==1&&component.Spawns==0,"missing original expires observer without inferring loss or granting another crate");
  Step(6);Check(world.Observers==1,"failed original loading cannot retry every pulse");
  (world,player,component)=Setup();first=Recover(world,Key,20);world.Entities.list.Remove(first);world.Entities.dict.Remove(first.entityId);world.Loaded=false;Step(1);
  world.Loaded=true;world.Entities.Add(first);RebirthPurgeSupplySaveProof.Ready.Add(Key);Step(4);
  Check(RebirthPurgeSupplyService.Published[Key].DeliveredDrops==1&&component.Spawns==0,"original crate arriving from native chunk load completes without a replacement");
  Check(world.RemovedObservers==1,"confirmed original releases temporary recovery observer");
  (world,player,component)=Setup();first=Recover(world,Key,20);world.Entities.list.Remove(first);world.Entities.dict.Remove(first.entityId);world.Loaded=false;Step(1);RebirthPurgeSupplyCoordinator.Reset();
  Check(world.RemovedObservers==1,"same-world lifecycle reset releases recovery observer");
  RebirthPurgeSupplyCoordinator.Reset();Check(world.RemovedObservers==1,"lifecycle reset does not release an observer twice");
  (world,player,component)=Setup();first=Recover(world,Key,20);world.Entities.list.Remove(first);world.Entities.dict.Remove(first.entityId);world.Loaded=false;world.ThrowObserver=true;Step(1);
  Check(world.Observers==1&&component.Spawns==0,"native recovery observer failure does not escape update or spawn a replacement");
  Step(10);Check(world.Observers==1,"native observer failure backs off instead of retrying every pulse");
  (world,player,component)=Setup();first=Recover(world,Key,20);world.Entities.list.Remove(first);world.Entities.dict.Remove(first.entityId);
  world.UnloadedX.Add(160);component.supplyCrates.Add(new AIDirectorAirDropComponent.SupplyCrateCache{entityId=first.entityId,blockPos=new Vector3i(160,61,-993)});Step(1);
  Check(world.Observers==1&&world.LastObserver.x==160.5f&&component.Spawns==0,"native moved-crate cache supplies only an unloaded destination hint");
  Check(RebirthPurgeSupplyService.Published[Key].DeliveredDrops==0,"cached runtime ID and position cannot confirm ownership or delivery");
  world.UnloadedX.Clear();first.position=new Vector3(160.5f,61,-992.5f);world.Entities.Add(first);RebirthPurgeSupplySaveProof.Ready.Add(Key);Step(4);
  Check(RebirthPurgeSupplyService.Published[Key].DeliveredDrops==1&&component.Spawns==0,"positively stamped moved original completes after native chunk load without replacement");
  Console.WriteLine("RESULT "+checks+" PASS; production coordinator/flight/account/stamp/spawn scope; native world, authenticated identity, atomic service and typed saved receipt boundaries explicitly doubled. No actual native disk or live-game claim.");
 }
}
class Log{public static void Warning(string text){}}
class WorldState{public string Guid;}
class Entity{public World world;public int entityId;public Vector3 position;public int spawnById;public string spawnByName,EntityName;public bool spawnByAllowShare;}
class EntityPlayer:Entity{public string Owner;public bool IsDead()=>false;}
class EntitySupplyCrate:Entity{public bool onGround,Dead;public bool IsDead()=>Dead;}
class EntityList{public Dictionary<int,Entity> dict=new Dictionary<int,Entity>();public List<Entity> list=new List<Entity>();public void Add(Entity entity){dict[entity.entityId]=entity;list.Add(entity);}}
struct Vector3i{public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
class Chunk{}
class World{public WorldState worldState;public EntityList Entities=new EntityList();public AIDirector aiDirector;public bool Loaded=true;public int Observers,RemovedObservers;public bool ThrowObserver;public HashSet<int> UnloadedX=new HashSet<int>();public Vector3 LastObserver;
 public bool IsRemote()=>false;public static object worldToBlockPos(Vector3 pos)=>pos;public object GetChunkFromWorldPos(object pos)=>Loaded&&!UnloadedX.Contains((int)((Vector3)pos).x)?new Chunk():null;public World GetGameManager()=>this;
 public ChunkManager.ChunkObserver AddChunkObserver(Vector3 position,bool visual,int radius,int entity){Observers++;LastObserver=position;if(ThrowObserver)throw new InvalidOperationException("native observer unavailable");return new ChunkManager.ChunkObserver();}public void RemoveChunkObserver(ChunkManager.ChunkObserver observer){RemovedObservers++;}
 public Vector3 ClampToValidWorldPos(Vector3 pos)=>pos;public float GetHeight(int x,int z)=>61;}
class ChunkManager{public class ChunkObserver{}}
class Tracked{public EntityPlayer Player;}class TrackedList{public List<Tracked> list=new List<Tracked>();}
class AIDirectorPlayerManagementComponent{public TrackedList trackedPlayers=new TrackedList();}
class AIDirector{public World World;public AIDirectorAirDropComponent Supply;public AIDirectorPlayerManagementComponent Players;public T GetComponent<T>()where T:class=>typeof(T)==typeof(AIDirectorAirDropComponent)?Supply as T:Players as T;}
class RandomSource{public Vector2 RandomOnUnitCircle=>new Vector2(1,0);public float RandomFloat=>0;}
class AIDirectorAirDropComponent{public AIDirector Director;public RandomSource Random=new RandomSource();public AIAirDrop activeAirDrop;public class SupplyCrateCache{public ChunkManager.ChunkObserver ChunkObserver;public int entityId;public Vector3i blockPos;public static implicit operator SupplyCrateCache(int id)=>new SupplyCrateCache{entityId=id};}public List<SupplyCrateCache> supplyCrates=new List<SupplyCrateCache>();public int Spawns;public bool ThrowAfterRegistration,ThrowAfterCache;
 public EntitySupplyCrate SpawnSupplyCrate(Vector3 drop,ChunkManager.ChunkObserver observer){Spawns++;var crate=new EntitySupplyCrate{world=Director.World,entityId=100+Spawns,position=drop,onGround=true};RebirthPurgeSupplySpawnScope.BeforeSpawn(Director.World,crate);Director.World.Entities.Add(crate);if(ThrowAfterRegistration)throw new Exception("after registration");supplyCrates.Add(new SupplyCrateCache{entityId=crate.entityId,ChunkObserver=observer,blockPos=new Vector3i((int)drop.x,(int)drop.y,(int)drop.z)});if(ThrowAfterCache)throw new Exception("after cache");return crate;}}
class AIAirDrop{public const float cPlaneMetersPerSecond=120;public static int Planes;public Entity eSupplyPlane;readonly World world;public class FlightPath{public Vector3 Start,End;}
 public AIAirDrop(AIDirectorAirDropComponent component,World world,List<EntityPlayer> players){this.world=world;}public void SpawnPlane(FlightPath path){Planes++;eSupplyPlane=new Entity{world=world,entityId=1000+Planes};world.Entities.Add(eSupplyPlane);}}
class GameManager{public static GameManager Instance;public World World;}class ConnectionManager{public bool IsServer;}
class SingletonMonoBehaviour<T>{public static T Instance;}class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}class LootContainer{public static bool NoLoot;}
class GameTimer{public static readonly GameTimer Instance=new GameTimer();public ulong ticks;}
class RebirthPoiWorldBinding{public Guid WorldId;public bool IsCurrent;}
class RebirthPoiWorldSnapshot{public RebirthPoiWorldBinding Binding;}
class RebirthPoiWorldStore{public RebirthPoiWorldSnapshot Published;}
class RebirthPoiWorldLifecycle{public static readonly RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle();public RebirthPoiWorldStore Store;public bool TryGetStore(out RebirthPoiWorldStore store){store=Store;return store!=null;}}
static class RebirthPurgeReleasePolicy{internal static bool Enabled=true;}
class RebirthSandboxOptionManager{internal static readonly RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool IsPurge=true;}
static class RebirthPurgeKillContributor{internal static int Calls;internal static string Identify(EntityPlayer player,World world){Calls++;return player!=null&&ReferenceEquals(player.world,world)?player.Owner:null;}}
static class RebirthPurgeSupplyCrateSerialization{internal static Dictionary<EntitySupplyCrate,RebirthPurgeSupplyCrateStamp> Stamps=new Dictionary<EntitySupplyCrate,RebirthPurgeSupplyCrateStamp>();
 internal static bool TryAttachBeforeSpawn(EntitySupplyCrate crate,RebirthPurgeSupplyCrateStamp stamp,object owner,Func<bool> current)=>TryAttach(crate,stamp,owner,current);
 internal static bool TryAttach(EntitySupplyCrate crate,RebirthPurgeSupplyCrateStamp stamp,object owner,Func<bool> current){if(!current())return false;Stamps[crate]=stamp;return true;}
 internal static bool TryReadAuthoritative(EntitySupplyCrate crate,Guid world,out RebirthPurgeSupplyCrateStamp stamp){stamp=null;Entity registered;return ReferenceEquals(crate.world,GameManager.Instance.World)&&crate.world.Entities.dict.TryGetValue(crate.entityId,out registered)&&ReferenceEquals(registered,crate)&&Stamps.TryGetValue(crate,out stamp)&&stamp.World==world;}}
static class RebirthPurgeSupplyService{internal static IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> Published;internal static bool Allow;internal static int Starts;
 static bool Put(RebirthPurgeSupplyAccount original,RebirthPurgeSupplyAccount next){RebirthPurgeSupplyAccount current;if(!Allow||!Published.TryGetValue(original.Player,out current)||!ReferenceEquals(original,current))return false;var map=new Dictionary<string,RebirthPurgeSupplyAccount>(Published);map[original.Player]=next;Published=map;return true;}
 internal static bool TryStart(RebirthPurgeSupplyAccount original,Guid world,Guid token,out RebirthPurgeSupplyAccount next){next=null;if(!original.TryStart(world,token,out next)||!Put(original,next))return false;Starts++;return true;}
 internal static bool TryBeginCrate(RebirthPurgeSupplyAccount original,Guid world,Guid token,out RebirthPurgeSupplyAccount next){next=null;return original.TryBeginCrate(world,token,out next)&&Put(original,next);}
 internal static bool TryPrepare(RebirthPurgeSupplyAccount original,RebirthPurgeSupplyDeliveryPlan plan,out RebirthPurgeSupplyAccount next){next=null;return original.TryReserve(plan,out next)&&Put(original,next);}
 internal static bool TryComplete(RebirthPurgeSupplyDeliveryReceipt receipt,out RebirthPurgeSupplyAccount next){next=null;var stamp=receipt.Stamp;RebirthPurgeSupplyAccount original;return Published.TryGetValue(stamp.Player,out original)&&original.TryComplete(stamp.World,stamp.Token,out next)&&Put(original,next);}}
class RebirthPurgeSupplyDeliveryReceipt{public RebirthPurgeSupplyCrateStamp Stamp;}
class RebirthPurgeSupplySaveProof{public static Dictionary<string,int> Queues=new Dictionary<string,int>();public static HashSet<string> Ready=new HashSet<string>();public static bool Refresh=true;RebirthPurgeSupplyCrateStamp stamp;
 public bool MayRefresh=>Refresh;public static bool TryQueue(EntitySupplyCrate crate,RebirthPoiWorldSnapshot snapshot,out RebirthPurgeSupplySaveProof proof){var stamp=RebirthPurgeSupplyCrateSerialization.Stamps[crate];Queues.TryGetValue(stamp.Player,out int count);Queues[stamp.Player]=count+1;proof=new RebirthPurgeSupplySaveProof{stamp=stamp};return true;}
 public bool TryReceipt(out RebirthPurgeSupplyDeliveryReceipt receipt){receipt=null;if(!Ready.Contains(stamp.Player))return false;receipt=new RebirthPurgeSupplyDeliveryReceipt{Stamp=stamp};return true;}}
namespace UnityEngine{public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}public struct Vector2{public float x,y;public float sqrMagnitude=>x*x+y*y;public Vector2(float x,float y){this.x=x;this.y=y;}}public static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}public static class Time{public static float realtimeSinceStartup;}}