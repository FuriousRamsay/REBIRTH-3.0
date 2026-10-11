using System;using System.Collections.Generic;using System.Xml.Linq;using UnityEngine;
class Program
{
 static int checks;static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static readonly string Key=new string('a',64);
 static (World,EntityPlayer,AIDirectorAirDropComponent,RebirthPurgeSupplyAccount) Setup()
 {
  var world=new World{worldState=new WorldState{Guid=Guid.NewGuid().ToString("N")}};var player=new EntityPlayer{world=world,entityId=1,position=new Vector3(14,61,-993)};world.Entities.dict[1]=player;
  GameManager.Instance=new GameManager{World=world};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true};
  var component=new AIDirectorAirDropComponent{Director=new AIDirector{World=world}};
  var account=new RebirthPurgeSupplyAccount(Key,75,75,1);account.TryReserve(new RebirthPurgeSupplyDeliveryPlan(20,90,-993),out account);
  RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{Key,account}};RebirthPurgeSupplyService.Allow=true;RebirthPurgeSupplyService.Starts=0;
  RebirthPurgeKillContributor.Key=Key;RebirthPurgeSupplyCrateSerialization.Allow=true;RebirthPurgeSupplyCrateSerialization.Stamp=null;AIAirDrop.Planes=0;AIAirDrop.Throw=false;LootContainer.NoLoot=false;
  return(world,player,component,account);
 }
 static void Main()
 {
  var (world,player,component,prepared)=Setup();
  RebirthPurgeSupplyService.Allow=false;Check(!RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out _)&&AIAirDrop.Planes==0&&world.Observers==0,"failed durable start performs no plane or observer mutation");RebirthPurgeSupplyService.Allow=true;
  Check(RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out var owner)&&RebirthPurgeSupplyService.Starts==1,"original prepared account starts once before native work");
  Check(!RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out _)&&RebirthPurgeSupplyService.Starts==1,"stale original preparation cannot create another owner");
  world.Loaded=false;owner.Advance(1);Check(world.Observers==1&&AIAirDrop.Planes==0&&component.Spawns==0,"native observer loads original destination before flight effects");
  world.Loaded=true;for(int n=0;n<12;n++)component.supplyCrates.Add(n);
  owner.Advance(1);Check(AIAirDrop.Planes==0&&component.Spawns==0&&component.supplyCrates.Count==12,"native full crate capacity waits without removing older crates");
  component.supplyCrates.Clear();owner.Advance(float.NaN);Check(AIAirDrop.Planes==0,"invalid tick duration cannot launch plane");
  owner.Advance(1);Check(AIAirDrop.Planes==1&&component.Spawns==0,"one registered native plane begins original flight");
  for(int n=0;n<12;n++)owner.Advance(1);
  Check(component.Spawns==1&&owner.Crate!=null&&!owner.Uncertain,"one native crate is positively returned and owned");
  Check(owner.Crate.spawnById==player.entityId&&!owner.Crate.spawnByAllowShare,"native owner projection precedes original crate registration and broadcast");
  Check(RebirthPurgeSupplyCrateSerialization.Stamp!=null&&RebirthPurgeSupplyCrateSerialization.Stamp.Token==prepared.Token(Guid.Parse(world.worldState.Guid)),"returned original crate uses durable world player sequence stamp");
  owner.ReleaseObserver();Check(world.RemovedObservers==0,"native cache retains its observer");owner.Advance(1);Check(AIAirDrop.Planes==1&&component.Spawns==1,"completed original native calls cannot be repeated");
  Check(RebirthPurgeSupplyService.Published[Key].DeliveredDrops==0,"plane and live crate do not falsely consume undelivered entitlement");
  (world,player,component,prepared)=Setup();RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out owner);component.Throw=true;
  for(int n=0;n<12;n++)owner.Advance(1);Check(owner.Uncertain&&component.Spawns==1,"native crate exception retains uncertain original");
  owner.Advance(1);Check(component.Spawns==1,"uncertain crate call cannot retry and duplicate reward");
  (world,player,component,prepared)=Setup();RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out owner);component.ThrowBeforeRegistration=true;
  for(int n=0;n<12;n++)owner.Advance(1);
  Check(owner.Uncertain&&owner.Crate==null&&!world.Entities.dict.ContainsKey(100),"attached but unregistered crate cannot become positive recovery evidence");
  owner.ReleaseObserver();Check(world.RemovedObservers==1,"failed registration releases unowned temporary observer without retry");
  (world,player,component,prepared)=Setup();RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out owner);component.ThrowAfterRegistration=true;
  for(int n=0;n<12;n++)owner.Advance(1);
  Check(!owner.Uncertain&&ReferenceEquals(owner.Crate,world.Entities.dict[100])&&world.Entities.dict.ContainsKey(100)&&RebirthPurgeSupplyCrateSerialization.Stamp.Token==prepared.Token(Guid.Parse(world.worldState.Guid)),"registered crate remains originally tagged after native spawn throws before reply");
  owner.ReleaseObserver();Check(world.RemovedObservers==1,"registered original without native cache releases retained observer once");owner.ReleaseObserver();Check(world.RemovedObservers==1,"recovered observer cannot be released twice");
  var normalCrate=new EntitySupplyCrate{world=world,entityId=102};RebirthPurgeSupplyCrateSerialization.Stamp=null;RebirthPurgeSupplySpawnScope.BeforeSpawn(world,normalCrate);
  Check(RebirthPurgeSupplyCrateSerialization.Stamp==null,"exception disposes original spawn scope so unrelated later crate cannot inherit it");
  (world,player,component,prepared)=Setup();RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out owner);AIAirDrop.Throw=true;owner.Advance(1);
  Check(owner.Uncertain&&AIAirDrop.Planes==1,"uncertain original plane spawn is retained");owner.Advance(1);Check(AIAirDrop.Planes==1,"uncertain plane call cannot be repeated");
  owner.ReleaseObserver();Check(world.RemovedObservers==1,"observer retained before crate attempt is released explicitly");
  (world,player,component,prepared)=Setup();RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out owner);RebirthPurgeKillContributor.Key=null;owner.Advance(1);
  Check(world.Observers==0&&AIAirDrop.Planes==0,"changed authenticated owner withholds original flight");
  (world,player,component,prepared)=Setup();RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out owner);GameManager.Instance.World=new World();owner.Advance(1);
  Check(world.Observers==0&&AIAirDrop.Planes==0,"replaced world cannot advance original flight");
  (world,player,component,prepared)=Setup();component.activeAirDrop=new AIAirDrop(component,world,new List<EntityPlayer>{player});
  Check(!RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out _)&&RebirthPurgeSupplyService.Starts==0,"existing native flight is not overwritten");
  (world,player,component,prepared)=Setup();LootContainer.NoLoot=true;
  Check(!RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out _)&&RebirthPurgeSupplyService.Starts==0,"no-loot world cannot falsely start supply delivery");
  (world,player,component,prepared)=Setup();RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out owner);
  var coldStarted=RebirthPurgeSupplyAccount.Read(RebirthPurgeSupplyService.Published[Key].Write());
  RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{Key,coldStarted}};
  Check(RebirthPurgeSupplyFlightOwner.TryCreate(component,player,coldStarted,out var resumed)&&RebirthPurgeSupplyService.Starts==1,"cold pre-crate original resumes without another entitlement or start");
  for(int n=0;n<12;n++)resumed.Advance(1);
  Check(component.Spawns==1&&RebirthPurgeSupplyService.Published[Key].Delivery.Phase==RebirthPurgeSupplyDeliveryPhase.CrateStarted,"resumed flight durably admits one original crate");
  Check(!RebirthPurgeSupplyFlightOwner.TryCreate(component,player,RebirthPurgeSupplyService.Published[Key],out _),"cold crate-started original cannot launch duplicate");
  (world,player,component,prepared)=Setup();RebirthPurgeSupplyFlightOwner.TryCreate(component,player,prepared,out owner);
  RebirthPurgeSupplyService.Allow=false;for(int n=0;n<12;n++)owner.Advance(1);
  Check(owner.Uncertain&&component.Spawns==0,"failed crate phase acknowledgement performs no native crate effect");
  owner.ReleaseObserver();Check(world.RemovedObservers==1,"failed durable crate admission releases observer because native crate was never attempted");
  owner.ReleaseObserver();Check(world.RemovedObservers==1,"released pre-spawn observer cannot be removed twice");
  (world,player,component,prepared)=Setup();
  prepared.TryStart(Guid.Parse(world.worldState.Guid),prepared.Token(Guid.Parse(world.worldState.Guid)),out var oldStarted);
  var oldXml=oldStarted.Write();oldXml.Element("delivery").SetAttributeValue("version",1);oldStarted=RebirthPurgeSupplyAccount.Read(oldXml);
  RebirthPurgeSupplyService.Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{Key,oldStarted}};
  Check(!RebirthPurgeSupplyFlightOwner.TryCreate(component,player,oldStarted,out _),"legacy started ambiguity is preserved instead of retried");
  Console.WriteLine("RESULT "+checks+" PASS; production owned flight/account/stamp; native aircraft, world, authenticated identity and atomic service boundary explicitly doubled. No live plane, native save commit or delivery completion proof.");
 }
}
class Log{public static void Warning(string text){}}
class WorldState{public string Guid;}class EntityList{public Dictionary<int,Entity> dict=new Dictionary<int,Entity>();}
class Entity{public World world;public int entityId;public Vector3 position;public int spawnById;public string spawnByName,EntityName;public bool spawnByAllowShare;}class EntityPlayer:Entity{public bool IsDead()=>false;}
class EntitySupplyCrate:Entity{}class Chunk{}
class World{public WorldState worldState;public EntityList Entities=new EntityList();public bool Loaded=true;public int Observers,RemovedObservers;
 public bool IsRemote()=>false;public static object worldToBlockPos(Vector3 pos)=>pos;public object GetChunkFromWorldPos(object pos)=>Loaded?new Chunk():null;public World GetGameManager()=>this;
 public ChunkManager.ChunkObserver AddChunkObserver(Vector3 position,bool visual,int radius,int entity){Observers++;return new ChunkManager.ChunkObserver();}
 public void RemoveChunkObserver(ChunkManager.ChunkObserver observer){RemovedObservers++;}}
class ChunkManager{public class ChunkObserver{}}
class AIDirector{public World World;}
class AIDirectorAirDropComponent{public AIDirector Director;public AIAirDrop activeAirDrop;public class SupplyCrateCache{public ChunkManager.ChunkObserver ChunkObserver;public static implicit operator SupplyCrateCache(int id)=>new SupplyCrateCache();}public List<SupplyCrateCache> supplyCrates=new List<SupplyCrateCache>();public int Spawns;public bool Throw,ThrowBeforeRegistration,ThrowAfterRegistration;
 public EntitySupplyCrate SpawnSupplyCrate(Vector3 drop,ChunkManager.ChunkObserver observer){Spawns++;if(Throw)throw new Exception("native crate uncertainty");var crate=new EntitySupplyCrate{world=Director.World,entityId=100,position=drop};RebirthPurgeSupplySpawnScope.BeforeSpawn(Director.World,crate);if(ThrowBeforeRegistration)throw new Exception("before registration");Director.World.Entities.dict[100]=crate;if(ThrowAfterRegistration)throw new Exception("native registered crate uncertainty");supplyCrates.Add(new SupplyCrateCache{ChunkObserver=observer});return crate;}}
class AIAirDrop{public const float cPlaneMetersPerSecond=120;public static int Planes;public static bool Throw;public Entity eSupplyPlane;readonly World world;
 public class FlightPath{public Vector3 Start,End;}public AIAirDrop(AIDirectorAirDropComponent component,World world,List<EntityPlayer> players){this.world=world;}
 public void SpawnPlane(FlightPath path){Planes++;if(Throw)throw new Exception("native plane uncertainty");eSupplyPlane=new Entity{world=world,entityId=101};world.Entities.dict[101]=eSupplyPlane;}}
class GameManager{public static GameManager Instance;public World World;}class ConnectionManager{public bool IsServer;}
class SingletonMonoBehaviour<T>{public static T Instance;}class ThreadManager{public static bool IsMainThread()=>true;}class LootContainer{public static bool NoLoot;}
internal static class RebirthPurgeReleasePolicy{internal static bool Enabled=true;}internal class RebirthSandboxOptionManager{internal static readonly RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool IsPurge=true;}
internal static class RebirthPurgeKillContributor{internal static string Key;internal static string Identify(EntityPlayer player,World world)=>Key;}
internal static class RebirthPurgeSupplyCrateSerialization{internal static bool Allow;internal static RebirthPurgeSupplyCrateStamp Stamp;internal static bool TryAttachBeforeSpawn(EntitySupplyCrate crate,RebirthPurgeSupplyCrateStamp stamp,object owner,Func<bool> current)=>TryAttach(crate,stamp,owner,current);internal static bool TryAttach(EntitySupplyCrate crate,RebirthPurgeSupplyCrateStamp stamp,object owner,Func<bool> current){if(!Allow||!current())return false;Stamp=stamp;return true;}}
internal static class RebirthPurgeSupplyService{internal static IReadOnlyDictionary<string,RebirthPurgeSupplyAccount> Published;internal static bool Allow;internal static int Starts;
 internal static bool TryBeginCrate(RebirthPurgeSupplyAccount original,Guid world,Guid token,out RebirthPurgeSupplyAccount next){next=null;RebirthPurgeSupplyAccount actual;if(!Allow||!Published.TryGetValue(original.Player,out actual)||!ReferenceEquals(original,actual)||!original.TryBeginCrate(world,token,out next))return false;Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{original.Player,next}};return true;}
 internal static bool TryStart(RebirthPurgeSupplyAccount original,Guid world,Guid token,out RebirthPurgeSupplyAccount next){next=null;RebirthPurgeSupplyAccount actual;if(!Allow||!Published.TryGetValue(original.Player,out actual)||!ReferenceEquals(original,actual)||!original.TryStart(world,token,out next))return false;Starts++;Published=new Dictionary<string,RebirthPurgeSupplyAccount>{{original.Player,next}};return true;}}
namespace UnityEngine{public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}public static class Mathf{public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}}