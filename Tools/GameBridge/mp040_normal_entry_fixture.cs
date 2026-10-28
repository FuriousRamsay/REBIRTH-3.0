// AUTHORED_NOT_RUN. Actual native input entry and service facade; explicit game/admission adapters.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public struct Vector3 {public Vector3 normalized=>this;public static Vector3 operator +(Vector3 a,Vector3 b)=>a;public static Vector3 operator *(Vector3 a,float b)=>a;}
 public struct Ray {public Vector3 origin,direction;}
}
struct Vector3i {internal int x,y,z;}
class Block {}
class BlockValue {internal Block Block=new Block();}
class Hit {internal Vector3i blockPos;}
class WorldRayHitInfo {internal bool bHitValid=true;internal Hit hit=new Hit();}
static class Voxel {internal static bool Hit=true;internal static WorldRayHitInfo voxelRayHitInfo=new WorldRayHitInfo();internal static bool Raycast(World world,Ray ray,float range,int layers,int mask,float radius)=>Hit;}
static class Constants {internal const float cDigAndBuildDistance=5;}
class World {
 internal bool Remote;internal EntityPlayerLocal Primary;internal EntityPlayer Registered;internal BlockValue Target=new BlockValue();
 internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;internal object GetEntity(int id)=>Registered;internal BlockValue GetBlock(Vector3i pos)=>Target;
}
class EntityPlayer {internal World world;internal int entityId=1;internal bool Dead;internal Ray GetLookRay()=>new Ray();internal bool IsDead()=>Dead;}
class EntityPlayerLocal:EntityPlayer {}
class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
class ItemInventoryData {internal EntityPlayer holdingEntity;internal World world;}
public class ItemActionData {internal ItemInventoryData invData;internal float lastUseTime;}
public class ItemActionExchangeItem {public virtual void ExecuteAction(ItemActionData data,bool released){}}
class ConnectionManager {internal bool IsServer=true;}
static class SingletonMonoBehaviour<T> where T:new() {internal static T Instance=new T();}
static class Localization {internal static string Get(string key)=>key;}
class RebirthElectricalServiceRecord {}
static class RebirthElectricalServiceUiFeedback {internal static readonly List<string> Messages=new List<string>();internal static void Receive(bool success,string message)=>Messages.Add(message);}
static class RemoteResourceTransactions {
 internal sealed class ActionPayment {}
 internal static int Requests;internal static bool Accept=true;
 internal static bool RequestOwnedElectricalService(EntityPlayerLocal actor,Vector3i pos){Requests++;return Accept;}
}
static class RebirthInfrastructureWorkService {
 internal static int Admissions;internal static bool Accept=true;
 internal static bool IsElectricalBlock(Block block)=>block!=null;
 static bool BeginOwnedService(EntityPlayer actor,int clr,Vector3i position,out RemoteResourceTransactions.ActionPayment original,out string message){Admissions++;original=Accept?new RemoteResourceTransactions.ActionPayment():null;message="refused before payment";return Accept;}
// ACTUAL_SERVICE_FACADE
}
// ACTUAL_NATIVE_ENTRY
static class Program {
 static void Need(bool value,string error){if(!value)throw new Exception(error);}
 static ItemActionData New(bool remote=false){
  ThreadManager.Main=true;Voxel.Hit=true;Voxel.voxelRayHitInfo=new WorldRayHitInfo();
  var world=new World{Remote=remote};var actor=new EntityPlayerLocal{world=world};world.Primary=actor;world.Registered=actor;GameManager.Instance.World=world;
  SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=!remote};
  RebirthInfrastructureWorkService.Admissions=RemoteResourceTransactions.Requests=0;RebirthInfrastructureWorkService.Accept=RemoteResourceTransactions.Accept=true;RebirthElectricalServiceUiFeedback.Messages.Clear();
  return new ItemActionData{invData=new ItemInventoryData{world=world,holdingEntity=actor}};
 }
 public static void Main(){
  var action=new ItemActionElectricalServiceRebirth();var data=New();action.ExecuteAction(data,true);
  Need(RebirthInfrastructureWorkService.Admissions==1&&RemoteResourceTransactions.Requests==0,"host input bypassed owning admission");
  Need(RebirthElectricalServiceUiFeedback.Messages.Count==1&&RebirthElectricalServiceUiFeedback.Messages[0].Contains("pending"),"host reported completion before settlement");
  data=New(true);action.ExecuteAction(data,true);
  Need(RemoteResourceTransactions.Requests==1&&RebirthInfrastructureWorkService.Admissions==0,"remote input bypassed native owner checkpoint request");
  Need(RebirthElectricalServiceUiFeedback.Messages[0].Contains("pending"),"remote reported completion before settlement");
  data=New();RebirthInfrastructureWorkService.Accept=false;action.ExecuteAction(data,true);
  Need(RebirthInfrastructureWorkService.Admissions==1&&RebirthElectricalServiceUiFeedback.Messages[0]=="refused before payment","admission refusal lost or fell back");
  data=New(true);RemoteResourceTransactions.Accept=false;action.ExecuteAction(data,true);
  Need(RemoteResourceTransactions.Requests==1&&RebirthInfrastructureWorkService.Admissions==0,"remote refused original fell back to authority inventory");
  for(int variant=0;variant<11;variant++){
   data=New(variant%2==0);var world=data.invData.world;var actor=(EntityPlayerLocal)data.invData.holdingEntity;
   if(variant==0)GameManager.Instance.World=new World();if(variant==1)data.invData.world=new World();if(variant==2)world.Primary=new EntityPlayerLocal();
   if(variant==3)world.Registered=new EntityPlayerLocal();if(variant==4)actor.Dead=true;if(variant==5)ThreadManager.Main=false;
   if(variant==6)Voxel.Hit=false;if(variant==7)Voxel.voxelRayHitInfo.bHitValid=false;if(variant==8)world.Target.Block=null;if(variant==9)data.lastUseTime=1;
   action.ExecuteAction(data,variant!=10);
   Need(RemoteResourceTransactions.Requests==0&&RebirthInfrastructureWorkService.Admissions==0,"invalid native source admitted variant "+variant);
  }
  Console.WriteLine("PASS actual electrical normal input/service routing with native/admission doubles; no game/payment proof");
 }
}
