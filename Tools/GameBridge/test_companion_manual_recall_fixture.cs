using System;using System.Collections.Generic;
class Entity{}
class EntityPlayer:Entity{public int entityId=1;}
class EntityDrone:Entity{public int belongsPlayerId=1;public bool Dead;public string OrderState="Stay";public bool IsDead()=>Dead;}
class EntityList{public List<Entity> list=new List<Entity>();}
class World{public EntityList Entities=new EntityList();}
enum RebirthNpcPresenceState{Active,Inactive}
enum RebirthNpcOwnershipKind{Player,Other}
enum RebirthNpcOrderState{Follow,Stay,Guard}
enum RebirthDogLifecycleKind{Active,Inactive}
class RebirthNpcRuntimeState{public RebirthNpcPresenceState Presence;public RebirthNpcOwnershipKind OwnershipKind;public RebirthNpcOrderState Order;public string OwnerId="owner";}
static class RebirthNpcRuntimeRegistry{public static RebirthNpcRuntimeState[] Rows=new RebirthNpcRuntimeState[0];public static RebirthNpcRuntimeState[] GetSnapshot()=>Rows;}
class Identity{}
class Ownership{public string OwnerPlatformIdOrPersistentPlayerId="owner";}
class Dog{public RebirthDogLifecycleKind Lifecycle;}
class RebirthNpcPersistentRecordView{public Identity Identity=new Identity();public Ownership Ownership=new Ownership();public Dog Dog=new Dog();public bool IsDog=true;}
static class RebirthDogStateService{public static bool IsDog(RebirthNpcPersistentRecordView row)=>row.IsDog;}
static class RebirthNpcAggregatePersistenceStore{public static RebirthNpcPersistentRecordView[] Rows=new RebirthNpcPersistentRecordView[0];public static RebirthNpcPersistentRecordView[] SnapshotViews()=>Rows;}
static class Subject{
// PRODUCTION_METHOD
}
public static class ManualRecallChecks
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static void Run()
 {
  var world=new World();var player=new EntityPlayer();
  Check(!Subject.HasTeleportTarget(world,player,"owner"),"empty roster admitted");
  var drone=new EntityDrone();world.Entities.list.Add(drone);Check(Subject.HasTeleportTarget(world,player,"owner"),"Stay drone excluded");
  drone.Dead=true;Check(!Subject.HasTeleportTarget(world,player,"owner"),"dead drone admitted");world.Entities.list.Clear();
  foreach(var order in new[]{RebirthNpcOrderState.Stay,RebirthNpcOrderState.Guard,RebirthNpcOrderState.Follow}){
   var row=new RebirthNpcRuntimeState{Order=order};RebirthNpcRuntimeRegistry.Rows=new[]{row};
   Check(Subject.HasTeleportTarget(world,player,"owner"),"active order excluded");row.OwnerId="other";
   Check(!Subject.HasTeleportTarget(world,player,"owner"),"foreign owner admitted");
  }
  RebirthNpcRuntimeRegistry.Rows=new RebirthNpcRuntimeState[0];var dog=new RebirthNpcPersistentRecordView();RebirthNpcAggregatePersistenceStore.Rows=new[]{dog};
  Check(Subject.HasTeleportTarget(world,player,"owner"),"unloaded active dog excluded");dog.Dog.Lifecycle=RebirthDogLifecycleKind.Inactive;
  Check(!Subject.HasTeleportTarget(world,player,"owner"),"inactive dog admitted");
  Console.WriteLine("PASS actual manual recall admission: Stay/Guard/Follow, foreign/dead/empty, durable dog; native movement/authority NOT covered.");
 }
}
