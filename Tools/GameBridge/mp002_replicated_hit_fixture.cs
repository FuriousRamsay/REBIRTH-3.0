// AUTHORED_NOT_RUN. Actual replicated hit body; native physics/event APIs doubled.
using System;
class Vector3 {}
struct Ray {}
class BlockValue { internal static readonly BlockValue Air=new BlockValue(); }
enum MinEventTypes { onSelfPrimaryActionRayHit,onSelfSecondaryActionRayHit }
class EventContext { internal object ItemValue,ItemActionData;internal bool IsLocal; }
class EntityAlive {
 internal EventContext MinEventContext=new EventContext();internal bool Dead;
 internal bool IsDead()=>Dead;internal Ray GetLookRay()=>new Ray();
}
class World {internal bool Remote;internal bool IsRemote()=>Remote;}
class InventoryData {internal World world;internal EntityAlive holdingEntity;internal object itemValue;}
class ItemActionDataRanged {internal InventoryData invData;internal int indexInEntityOfAction;}
class Hit {internal float distanceSq;internal Vector3 pos=new Vector3();}
class WorldRayHitInfo {internal string tag;internal Hit hit=new Hit();internal WorldRayHitInfo Clone()=>this;}
static class Voxel {
 internal static bool Found=true;internal static WorldRayHitInfo voxelRayHitInfo=new WorldRayHitInfo();
 internal static bool Raycast(World world,Ray ray,float range,int layer,int mask,float radius)=>Found;
}
static class ItemActionAttack {
 internal static object FindHitEntityNoTagCheck(WorldRayHitInfo info,out string name){name="";return Target;}
 internal static EntityAlive Target;internal static BlockValue GetBlockHit(World world,WorldRayHitInfo hit)=>BlockValue.Air;
}
static class RebirthExtinguisherHitContext {
 internal static int Calls;internal static bool Throw;internal static EntityAlive Expected;
 internal static void Fire(EntityAlive actor,Vector3 position,EntityAlive target,BlockValue block,MinEventTypes type){
  if(!actor.MinEventContext.IsLocal||actor.MinEventContext.ItemValue==null||actor.MinEventContext.ItemActionData==null||!ReferenceEquals(target,Expected))throw new Exception("wrong actual event context");
  Calls++;if(Throw)throw new InvalidOperationException("event failure");
 }
}
class ActualAction {
 internal float Range=6;internal int hitmaskOverride;internal float GetRange(ItemActionDataRanged data)=>Range;
 // ACTUAL_REPLICATED_HIT
}
class Program {
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(){
  var actor=new EntityAlive();var world=new World();var item=new object();var oldItem=new object();var oldAction=new object();
  actor.MinEventContext.ItemValue=oldItem;actor.MinEventContext.ItemActionData=oldAction;
  var data=new ItemActionDataRanged{invData=new InventoryData{world=world,holdingEntity=actor,itemValue=item}};
  var action=new ActualAction();var target=new EntityAlive();ItemActionAttack.Target=target;RebirthExtinguisherHitContext.Expected=target;
  Voxel.voxelRayHitInfo.tag="E_target";Voxel.voxelRayHitInfo.hit.distanceSq=4;
  action.ApplyReplicatedNativeHit(data);
  Require(RebirthExtinguisherHitContext.Calls==1,"valid authority hit missing");
  Require(!actor.MinEventContext.IsLocal&&ReferenceEquals(actor.MinEventContext.ItemValue,oldItem)&&ReferenceEquals(actor.MinEventContext.ItemActionData,oldAction),"context leaked");
  actor.MinEventContext.IsLocal=true;RebirthExtinguisherHitContext.Throw=true;
  try{action.ApplyReplicatedNativeHit(data);throw new Exception("missing event exception");}catch(InvalidOperationException){}
  Require(actor.MinEventContext.IsLocal&&ReferenceEquals(actor.MinEventContext.ItemValue,oldItem)&&ReferenceEquals(actor.MinEventContext.ItemActionData,oldAction),"throw lost prior context");
  RebirthExtinguisherHitContext.Throw=false;int calls=RebirthExtinguisherHitContext.Calls;
  world.Remote=true;action.ApplyReplicatedNativeHit(data);world.Remote=false;
  actor.Dead=true;action.ApplyReplicatedNativeHit(data);actor.Dead=false;
  action.Range=float.NaN;action.ApplyReplicatedNativeHit(data);action.Range=6;
  Voxel.Found=false;action.ApplyReplicatedNativeHit(data);Voxel.Found=true;
  Voxel.voxelRayHitInfo.hit.distanceSq=37;action.ApplyReplicatedNativeHit(data);
  Require(RebirthExtinguisherHitContext.Calls==calls,"ineligible ray produced event");
  Console.WriteLine("PASS actual replicated hit context against physics/event doubles; not native gameplay");
 }
}
