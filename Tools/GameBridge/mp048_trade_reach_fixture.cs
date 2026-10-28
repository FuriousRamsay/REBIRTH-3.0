using System;using System.Collections.Generic;
// Whole actual fresh reach policy. Geometry query is an explicit native-world
// adapter; this does not certify native collider/raycast runtime behavior.
namespace UnityEngine
{
 struct Vector3{internal float x,y,z;internal Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 struct Bounds{internal Vector3 center,size;internal Bounds(Vector3 center,Vector3 size){this.center=center;this.size=size;}}
}
struct RebirthNpcStableId{internal int Value;internal bool IsEmpty=>Value==0;public static bool operator ==(RebirthNpcStableId a,RebirthNpcStableId b)=>a.Value==b.Value;public static bool operator !=(RebirthNpcStableId a,RebirthNpcStableId b)=>a.Value!=b.Value;public override bool Equals(object o)=>o is RebirthNpcStableId id&&id==this;public override int GetHashCode()=>Value;}
class Entity{internal World world;internal int entityId;internal bool Dead,Spawned=true;internal UnityEngine.Vector3 position;internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;}
class EntityPlayer:Entity{}
class EntityRebirthNPC:Entity{internal State RebirthRuntimeState;internal class State{internal RebirthNpcStableId StableId;}}
class World
{
 internal EntityPlayer Player;internal EntityRebirthNPC Npc;internal bool Remote,Include=true;internal Action AfterQuery;internal UnityEngine.Bounds Query;
 internal bool IsRemote()=>Remote;internal Entity GetEntity(int id)=>id==Player.entityId?Player:id==Npc.entityId?Npc:null;
 internal void GetEntitiesInBounds(Type type,UnityEngine.Bounds bounds,List<Entity> entities){Query=bounds;if(Include&&Math.Abs(Npc.position.x-bounds.center.x)<=bounds.size.x/2+.5f&&Math.Abs(Npc.position.y-bounds.center.y)<=bounds.size.y/2+.5f&&Math.Abs(Npc.position.z-bounds.center.z)<=bounds.size.z/2+.5f)entities.Add(Npc);AfterQuery?.Invoke();}
}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
// Retained custody authority is an independent input, never inferred from reach.
static class RebirthNpcSaveQuarantineBundle{internal static bool Held;internal static bool BlocksRetainedTrade(RebirthNpcStableId npc)=>Held;}
class ConnectionManager{internal bool IsServer=true;}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcRuntimeRegistry{internal static int Target=2;internal static bool TryGetEntityId(RebirthNpcStableId npc,out int id){id=Target;return npc.Value==7;}}
static class RebirthNpcLiveRecoveryRuntime{internal static bool Deferred;internal static bool IsNativeActivationDeferred(EntityRebirthNPC npc)=>Deferred;}
static class RebirthNpcPlayerInventoryEndpointService{internal static string Endpoint="player:1";internal static bool TryGet(string actor,out string endpoint){endpoint=Endpoint;return actor=="actor";}}
class Program
{
 static void Main()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Trade reach assertion "+checks);}
  var id=new RebirthNpcStableId{Value=7};var world=new World();world.Player=new EntityPlayer{entityId=1,world=world};world.Npc=new EntityRebirthNPC{entityId=2,world=world,RebirthRuntimeState=new EntityRebirthNPC.State{StableId=id},position=new UnityEngine.Vector3(1,0,0)};GameManager.Instance.World=world;
  Check(RebirthNpcTradeReach.CanBegin("actor",id));Check(world.Query.size.x==10&&world.Query.size.y==10&&world.Query.size.z==10);
  RebirthNpcSaveQuarantineBundle.Held=true;Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));RebirthNpcSaveQuarantineBundle.Held=false;
  world.Npc.position=new UnityEngine.Vector3(5,5,5);Check(RebirthNpcTradeReach.CanBegin(world.Player,id));
  world.Npc.position=new UnityEngine.Vector3(50,0,0);Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));world.Npc.position=new UnityEngine.Vector3(1,0,0);
  world.Include=false;Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));world.Include=true;
  world.Player.Dead=true;Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));world.Player.Dead=false;
  world.Npc.Dead=true;Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));world.Npc.Dead=false;
  world.Npc.RebirthRuntimeState.StableId=new RebirthNpcStableId{Value=8};Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));world.Npc.RebirthRuntimeState.StableId=id;
  RebirthNpcLiveRecoveryRuntime.Deferred=true;Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));RebirthNpcLiveRecoveryRuntime.Deferred=false;
  world.Player.position=new UnityEngine.Vector3(float.NaN,0,0);Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));world.Player.position=new UnityEngine.Vector3();
  Check(!RebirthNpcTradeReach.CanBegin(new EntityPlayer{world=world,entityId=1},id));
  RebirthNpcPlayerInventoryEndpointService.Endpoint="player:bad";Check(!RebirthNpcTradeReach.CanBegin("actor",id));RebirthNpcPlayerInventoryEndpointService.Endpoint="player:1";
  world.Remote=true;Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));world.Remote=false;
  ThreadManager.Main=false;Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));ThreadManager.Main=true;
  world.AfterQuery=()=>GameManager.Instance.World=new World();Check(!RebirthNpcTradeReach.CanBegin(world.Player,id));
  Console.WriteLine(checks+" actual fresh trade reach assertions passed; native bounds query adapter NOT certified");
 }
}
