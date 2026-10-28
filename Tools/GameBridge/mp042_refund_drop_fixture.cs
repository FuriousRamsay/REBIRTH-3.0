using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
// AUTHORED_NOT_RUN. Full production observer; native spawn/codec/budget adapters.
namespace UnityEngine {internal struct Vector3 {internal static Vector3 zero=>new Vector3();}}
namespace HarmonyLib {internal sealed class Harmony {internal Harmony(string id){}}internal sealed class HarmonyPatch:Attribute {internal HarmonyPatch(Type t,string name,Type p){}}}
internal static class RebirthHarmonyBootstrap {internal static void PatchClassOnce(Harmony h,Type type){}}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal class Entity {internal World world;internal int entityId;}
internal sealed class EntityItem:Entity {internal int belongsPlayerId;internal ItemStack itemStack;internal bool Unloaded;internal bool IsMarkedForUnload()=>Unloaded;}
internal class EntityPlayer:Entity {internal int Died=2;internal string Creation="creation";internal bool Dead;internal Vector3 position;internal bool IsSpawned()=>true;internal bool IsDead()=>Dead;}
internal sealed class EntityPlayerLocal:EntityPlayer {}
internal sealed class World {
 internal bool Remote;internal EntityPlayerLocal Primary;internal Dictionary<int,Entity> Entities=new Dictionary<int,Entity>();
 internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Primary;internal Entity GetEntity(int id)=>Entities.TryGetValue(id,out var e)?e:null;
 internal void SpawnEntityInWorld(Entity entity){Entities[entity.entityId]=entity;Notify(entity);}
 internal void Notify(Entity entity){typeof(RebirthCookingRefundDropObservation).GetNestedType("SpawnObserver",BindingFlags.NonPublic).GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{this,entity,true});}
}
internal sealed class ItemStack {internal string Rich="rich-original";internal int count=3;internal bool IsEmpty()=>count==0;internal ItemStack Clone()=>new ItemStack{Rich=Rich,count=count};}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayer a)=>a.Creation;}
internal static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.Rich==b.Rich&&a.count==b.count;}
internal static class RemoteResourceTransactions {
 internal static byte[] CaptureOriginalNativeFrame(ItemStack[] items,ref long remaining)=>Encoding.UTF8.GetBytes(items[0].Rich+"|"+items[0].count);
 internal static long MeasureOriginalNativeFrame(byte[] image)=>image.Length;
}
internal static class RebirthCamperInventorySnapshot {internal static ItemStack[] Restore(byte[] image){var parts=Encoding.UTF8.GetString(image).Split('|');return new[]{new ItemStack{Rich=parts[0],count=int.Parse(parts[1])}};}}
internal static class RebirthProductiveEarnedObservationBudget {
 internal static bool Available=true;internal static int Live;
 internal sealed class Hold:IDisposable {public void Dispose(){Live--;}}
 internal static bool TryRetainDetached(long size,out IDisposable allocation){allocation=null;if(!Available)return false;Live++;allocation=new Hold();return true;}
 internal static bool TryResizeDetached(IDisposable allocation,long size)=>Available;
}
internal sealed class GameManager {
 internal static GameManager Instance=new GameManager();internal World World;internal int Mode,Calls;
 internal void ItemDropServer(ItemStack item,Vector3 p,Vector3 jitter,int owner,float time,bool option){
  Calls++;if(Mode==1)return;
  var entity=new EntityItem{world=World,entityId=100,belongsPlayerId=owner,itemStack=item.Clone()};
  if(Mode==2)entity.itemStack.Rich="other-rich";if(Mode==3)entity.belongsPlayerId++;
  World.SpawnEntityInWorld(entity);
  if(Mode==4)World.SpawnEntityInWorld(new EntityItem{world=World,entityId=101,belongsPlayerId=owner,itemStack=item.Clone()});
  if(Mode==5)entity.Unloaded=true;if(Mode==6)World.Primary.Died++;if(Mode==7)throw new Exception("native threw after spawn");
  if(Mode==8)item.Rich="native-mutated-argument";if(Mode==9)World=new World();
 }
}
// ACTUAL_DROP
internal static class Program {
 static void Need(bool b,string m){if(!b)throw new Exception(m);}
 public static void Main(){
  for(int mode=0;mode<10;mode++){
   var w=new World();var p=new EntityPlayerLocal{world=w,entityId=1};w.Primary=p;w.Entities[1]=p;GameManager.Instance=new GameManager{World=w,Mode=mode};
   var offered=new ItemStack();bool attempted=false,success=false,threw=false;
   try{success=RebirthCookingRefundDropObservation.TryDrop(p,offered,out attempted);}catch{threw=true;}
   Need(attempted&&GameManager.Instance.Calls==1,"native invocation missing "+mode);
   Need(success==(mode==0||mode==8)&&threw==(mode==7),"unknown or unrelated spawn accepted "+mode);
   Need(offered.Rich=="rich-original"&&offered.count==3,"native drop changed retained offer");
   Need(RebirthProductiveEarnedObservationBudget.Live==0,"drop allocation leaked");
  }
  for(int variant=0;variant<4;variant++){
   var w=new World();var p=new EntityPlayerLocal{world=w,entityId=1};w.Primary=p;w.Entities[1]=p;GameManager.Instance=new GameManager{World=w};
   if(variant==0)w.Remote=true;if(variant==1)p.Dead=true;if(variant==2)w.Primary=new EntityPlayerLocal();if(variant==3)RebirthProductiveEarnedObservationBudget.Available=false;
   Need(!RebirthCookingRefundDropObservation.TryDrop(p,new ItemStack(),out bool attempted)&&!attempted&&GameManager.Instance.Calls==0,"unavailable drop invoked native action");
   RebirthProductiveEarnedObservationBudget.Available=true;
  }
  {
   var w=new World();var local=new EntityPlayerLocal{world=w,entityId=1};w.Primary=local;w.Entities[1]=local;
   var remote=new EntityPlayer{world=w,entityId=2};w.Entities[2]=remote;GameManager.Instance=new GameManager{World=w};
   Need(RebirthCookingRefundDropObservation.TryDrop(remote,new ItemStack(),()=>true,out bool attempted,out var accepted)&&attempted&&accepted!=null&&accepted.belongsPlayerId==2,"authenticated remote-owner native action must expose exact entity");
   int calls=GameManager.Instance.Calls;
   Need(!RebirthCookingRefundDropObservation.TryDrop(remote,new ItemStack(),()=>false,out attempted,out accepted)&&!attempted&&accepted==null&&GameManager.Instance.Calls==calls,"revoked remote action must not invoke drop");
   Need(!RebirthCookingRefundDropObservation.TryDrop(remote,new ItemStack(),()=>throw new Exception("scope unavailable"),out attempted,out accepted)&&!attempted,"throwing original scope cannot enter native drop");
   Need(!RebirthCookingRefundDropObservation.TryDrop(remote,new ItemStack(),()=>true,out attempted,out accepted,()=>false)&&!attempted&&accepted==null&&GameManager.Instance.Calls==calls,"refused durable invocation fence cannot enter native call");
   bool threw=false;try{RebirthCookingRefundDropObservation.TryDrop(remote,new ItemStack(),()=>true,out attempted,out accepted,()=>throw new IOException("uncertain fence publication"));}catch(IOException){threw=true;}
   Need(threw&&!attempted&&GameManager.Instance.Calls==calls,"fence publication exception must preserve unknown original without native invocation");
   Need(RebirthProductiveEarnedObservationBudget.Live==0,"remote observer allocation leaked");
  }
  Console.WriteLine("PASS actual native cooking drop publication observer; durable save and genuine remote NOT RUN");
 }
}
