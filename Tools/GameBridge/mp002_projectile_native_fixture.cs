// AUTHORED_NOT_RUN. Actual observer classes; physics/transport/native returns doubled.
// ACTUAL_FLAMING_PROJECTILE
namespace HarmonyLib{[System.AttributeUsage(System.AttributeTargets.Class)]class HarmonyPatch:System.Attribute{public HarmonyPatch(System.Type type,string name){}}}
namespace UnityEngine{struct Vector3{internal float x,y,z;internal float sqrMagnitude=>x*x+y*y+z*z;}}
class ItemAction{}
class ItemActionData{internal ItemInventoryData invData;internal int indexInEntityOfAction;}
class ItemActionLauncher:ItemAction{internal class ItemActionDataLauncher:ItemActionData{}}
class ItemClass{internal string Name="ammoArrowFlaming";internal ItemAction[] Actions;internal string GetItemName()=>Name;}
class ItemValue{internal ItemClass ItemClass;}
class ItemInventoryData{internal ItemClass item;internal System.Collections.Generic.List<ItemActionData> actionData;}
class Inventory{internal int holdingItemIdx;internal ItemInventoryData holdingItemData;}
class Entity{internal int entityId=7;internal World world;}
class EntityPlayer:Entity{internal Inventory inventory=new Inventory();internal int Died;internal bool Dead;internal bool IsDead()=>Dead;}
class EntityPlayerLocal:EntityPlayer{}
class ClientInfo{internal int entityId=7;}
class Clients{internal ClientInfo Peer;internal ClientInfo ForEntityId(int id)=>Peer;}
class ConnectionManager{internal bool IsServer=true;internal Clients Clients=new Clients();}
static class SingletonMonoBehaviour<T>where T:new(){internal static T Instance=new T();}
class World{internal EntityPlayer Actor;internal bool Remote;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Actor?.entityId==id?Actor:null;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal bool IsPaused()=>false;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class NetPackageItemActionEffects{internal int entityId=7,slotIdx,actionIdx,firingState=1;internal ClientInfo Sender;internal bool Valid=true;internal UnityEngine.Vector3 startPos,direction=new UnityEngine.Vector3{x=1};internal bool ValidEntityIdForSender(int id)=>Valid;public void ProcessPackage(){}}
class ProjectileMoveScript{
 internal enum State{Idle,Active,Dead}
 internal Entity firingEntity;internal ItemActionLauncher.ItemActionDataLauncher actionData;internal int ProjectileOwnerID=7;
 internal ItemClass itemProjectile;internal ItemValue itemValueProjectile;internal State state;
 public void Fire(){}public void checkCollision(){}public void SetState(){}
}
struct Vector3i{internal int x;}
class Hit{internal UnityEngine.Vector3 pos;internal Vector3i blockPos;}
class WorldRayHitInfo{
 internal bool bHitValid=true;internal string tag="Block";internal Hit hit=new Hit{blockPos=new Vector3i{x=9}};
 internal WorldRayHitInfo Clone()=>new WorldRayHitInfo{tag=tag,bHitValid=bHitValid,hit=new Hit{pos=hit.pos,blockPos=hit.blockPos}};
}
static class Voxel{internal static WorldRayHitInfo voxelRayHitInfo=new WorldRayHitInfo();}
static class GameUtils{internal static bool IsBlockOrTerrain(string tag)=>tag=="Block";}
struct BlockValue{internal bool isair;}
static class ItemActionAttack{internal static BlockValue GetBlockHit(World world,WorldRayHitInfo hit)=>new BlockValue();}
enum RebirthFireIgnitionCause{Projectile}
static class RebirthFireRuntimePolicy{internal static bool Enabled=true;}
class RebirthFireService{internal static RebirthFireService Instance=new RebirthFireService();internal int Fires,Last;internal void ScheduleIgnition(World world,Vector3i[] pos,int actor,RebirthFireIgnitionCause cause,float delay){Fires++;Last=pos[0].x;}}
class Program{
 static readonly System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
 static object Invoke(System.Type type,string name,object[] args)=>type.GetMethod(name,Flags).Invoke(null,args);
 static System.Type Effects=>typeof(RebirthFlamingProjectileNative.NativeEffects);
 static System.Type Fire=>typeof(RebirthFlamingProjectileNative.NativeFire);
 static System.Type Collision=>typeof(RebirthFlamingProjectileNative.NativeCollision);
 static System.Type State=>typeof(RebirthFlamingProjectileNative.NativeState);
 static void Need(bool value,string message){if(!value)throw new System.Exception(message);}
 static ProjectileMoveScript Launch(bool returned=true,bool actualFire=true,ProjectileMoveScript reused=null){
  var actor=new EntityPlayer();var world=new World{Actor=actor};actor.world=world;GameManager.Instance.World=world;
  var peer=new ClientInfo();SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=peer;
  var launcher=new ItemActionLauncher();var held=new ItemInventoryData{item=new ItemClass{Actions=new ItemAction[]{launcher}}};
  var data=new ItemActionLauncher.ItemActionDataLauncher{invData=held};held.actionData=new System.Collections.Generic.List<ItemActionData>{data};actor.inventory.holdingItemData=held;
  var packet=new NetPackageItemActionEffects{Sender=peer};object[] prefix={packet,world,GameManager.Instance,true,null};
  Need((bool)Invoke(Effects,"Prefix",prefix),"valid native launch rejected");
  var ammo=new ItemClass();var projectile=reused??new ProjectileMoveScript();projectile.actionData=data;projectile.itemProjectile=ammo;projectile.itemValueProjectile=new ItemValue{ItemClass=ammo};
  if(actualFire){object[] before={projectile,actor,new UnityEngine.Vector3(),new UnityEngine.Vector3{x=1},false,true,null};Invoke(Fire,"Prefix",before);projectile.firingEntity=actor;projectile.state=ProjectileMoveScript.State.Active;Invoke(Fire,"Postfix",new[]{(object)true,before[6]});}
  Invoke(Effects,"Postfix",new[]{(object)returned,prefix[4]});Invoke(Effects,"Finalizer",new object[]{null,prefix[4]});
  return projectile;
 }
 static void Impact(ProjectileMoveScript projectile,bool actualState=true){
  object[] collision={projectile,true,null};Invoke(Collision,"Prefix",collision);
  if(actualState){object[] state={projectile,ProjectileMoveScript.State.Dead,true,null};Invoke(State,"Prefix",state);projectile.state=ProjectileMoveScript.State.Dead;Invoke(State,"Postfix",new[]{(object)projectile,true,state[3]});}
  Invoke(Collision,"Postfix",new[]{(object)true,collision[2]});Invoke(Collision,"Finalizer",new object[]{null,collision[2]});
 }
 static void Main(){
  var projectile=Launch();Impact(projectile);Need(RebirthFireService.Instance.Fires==1&&RebirthFireService.Instance.Last==9,"actual native flight/impact not applied once");Impact(projectile);Need(RebirthFireService.Instance.Fires==1,"projectile replayed");
  projectile=Launch(false);Impact(projectile);Need(RebirthFireService.Instance.Fires==1,"nonreturned native launch created permission");
  projectile=Launch(true,false);projectile.state=ProjectileMoveScript.State.Active;Impact(projectile);Need(RebirthFireService.Instance.Fires==1,"launch return without native Fire created permission");
  projectile=Launch();Impact(projectile,false);Need(RebirthFireService.Instance.Fires==1,"collision without actual native state transition created fire");
  projectile=Launch();GameManager.Instance.World=new World();Impact(projectile);Need(RebirthFireService.Instance.Fires==1,"old-world projectile created fire");
  projectile=Launch();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=new ClientInfo();Impact(projectile);Need(RebirthFireService.Instance.Fires==1,"replacement native peer adopted flight");
  projectile=Launch();Voxel.voxelRayHitInfo.tag="E_target";Impact(projectile);Need(RebirthFireService.Instance.Fires==1,"entity impact treated as block");Voxel.voxelRayHitInfo.tag="Block";
  projectile=Launch();object[] collision={projectile,true,null};Invoke(Collision,"Prefix",collision);object[] state={projectile,ProjectileMoveScript.State.Dead,true,null};Invoke(State,"Prefix",state);projectile.state=ProjectileMoveScript.State.Dead;Invoke(State,"Postfix",new[]{(object)projectile,true,state[3]});Voxel.voxelRayHitInfo.hit.blockPos=new Vector3i{x=77};Invoke(Collision,"Postfix",new[]{(object)true,collision[2]});Invoke(Collision,"Finalizer",new object[]{null,collision[2]});
  Need(RebirthFireService.Instance.Fires==2&&RebirthFireService.Instance.Last==9,"later global ray overwrote actual captured native impact");
  Voxel.voxelRayHitInfo.hit.blockPos=new Vector3i{x=9};
  projectile=Launch(reused:projectile);Impact(projectile);
  Need(RebirthFireService.Instance.Fires==3,"fresh authenticated native launch could not reuse pooled projectile");
  Impact(projectile);Need(RebirthFireService.Instance.Fires==3,"pooled projectile launch replayed");
  projectile=Launch();((EntityPlayer)projectile.firingEntity).Died++;Impact(projectile);
  Need(RebirthFireService.Instance.Fires==3,"old-life projectile adopted respawned shooter");
  projectile=Launch();((EntityPlayer)projectile.firingEntity).inventory.holdingItemData=new ItemInventoryData();Impact(projectile);
  Need(RebirthFireService.Instance.Fires==4,"switching held weapon invalidated genuine same-life flight");
  System.Console.WriteLine("PASS actual flaming projectile observer with explicit native/physics doubles; genuine gameplay NOT RUN");
 }
}
