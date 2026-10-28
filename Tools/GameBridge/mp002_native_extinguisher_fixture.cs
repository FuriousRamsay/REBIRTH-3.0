// No native physics/real packet/save result is simulated as proof.
// ACTUAL_NATIVE_SHOT

namespace HarmonyLib{[System.AttributeUsage(System.AttributeTargets.Class)]class HarmonyPatch:System.Attribute{public HarmonyPatch(System.Type t,string name){}}}
class ItemAction{}
class ItemActionData{}
class ItemClass{internal ItemAction[] Actions;}
class ItemInventoryData{internal World world;internal EntityPlayer holdingEntity;internal ItemClass item;internal System.Collections.Generic.List<ItemActionRanged.ItemActionDataRanged> actionData;}
class ItemActionRanged:ItemAction{internal class ItemActionDataRanged:ItemActionData{internal ItemInventoryData invData;internal int indexInEntityOfAction;internal float Delay=.15f;}}
class ItemActionRangedRebirthExtinguisher:ItemActionRanged{internal bool UsesNativeInfiniteSpray=true;internal int Hits;internal float NativeShotDelay(ItemActionDataRanged data)=>data.Delay;internal void ApplyReplicatedNativeHit(ItemActionDataRanged data){Hits++;}}
class Inventory{internal int holdingItemIdx;internal ItemInventoryData holdingItemData;}
class EntityPlayer{internal World world;internal int entityId=7,Died;internal bool Dead;internal Inventory inventory=new Inventory();internal bool IsDead()=>Dead;}
class EntityPlayerLocal:EntityPlayer{}
class ClientInfo{internal int entityId=7;}
class Clients{internal ClientInfo Peer;internal ClientInfo ForEntityId(int id)=>Peer!=null&&Peer.entityId==id?Peer:null;}
class ConnectionManager{internal bool IsServer=true;internal Clients Clients=new Clients();}
static class SingletonMonoBehaviour<T>where T:new(){internal static T Instance=new T();}
class World{internal EntityPlayer Actor;internal bool Remote;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Actor!=null&&Actor.entityId==id?Actor:null;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal bool Paused;internal bool IsPaused()=>Paused;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class Log{internal static void Warning(string message){}}
class NetPackageItemActionEffects
{
 internal int entityId=7,slotIdx,actionIdx,firingState=1,userData=1;internal ClientInfo Sender;internal bool Valid=true;
 internal bool ValidEntityIdForSender(int id)=>Valid&&Sender!=null&&Sender.entityId==id;
 public void ProcessPackage(World world,GameManager manager){}
}
static class Program
{
 static readonly System.Reflection.MethodInfo Prefix=typeof(RebirthExtinguisherNativeShot.NativeEffects).GetMethod("Prefix",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
 static readonly System.Reflection.MethodInfo Postfix=typeof(RebirthExtinguisherNativeShot.NativeEffects).GetMethod("Postfix",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
 static readonly System.Reflection.MethodInfo Finalizer=typeof(RebirthExtinguisherNativeShot.NativeEffects).GetMethod("Finalizer",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
 static ItemActionRangedRebirthExtinguisher action;static World world;static ClientInfo peer;
 static NetPackageItemActionEffects New()
 {
  action=new ItemActionRangedRebirthExtinguisher();peer=new ClientInfo();var actor=new EntityPlayer();world=new World{Actor=actor};actor.world=world;GameManager.Instance.World=world;GameManager.Instance.Paused=false;
  SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=true;SingletonMonoBehaviour<ConnectionManager>.Instance.Clients=new Clients{Peer=peer};
  var held=new ItemInventoryData{holdingEntity=actor,world=world,item=new ItemClass{Actions=new ItemAction[]{action}}};
  held.actionData=new System.Collections.Generic.List<ItemActionRanged.ItemActionDataRanged>{new ItemActionRanged.ItemActionDataRanged{invData=held}};actor.inventory.holdingItemData=held;
  return new NetPackageItemActionEffects{Sender=peer};
 }
 static bool Begin(NetPackageItemActionEffects packet,out object state){var args=new object[]{packet,world,GameManager.Instance,true,null};bool result=(bool)Prefix.Invoke(null,args);state=args[4];return result;}
 static void End(object state,bool ran=true,bool native=true)
 {try{if(ran&&native)RebirthExtinguisherNativeShot.ObserveActualNativeEffects(action,world.Actor.inventory.holdingItemData.actionData[0],1);Postfix.Invoke(null,new object[]{ran,state});}finally{Finalizer.Invoke(null,new object[]{null,state});}}
 static void Need(bool value,string name){if(!value)throw new System.Exception(name);}
 static void Main()
 {
  var packet=New();Need(Begin(packet,out var state)&&state!=null,"actual native invocation admitted");End(state);Need(action.Hits==1,"one native hit");Need(!Begin(packet,out state),"duplicate ordinal refused");
  packet=New();world.Actor.inventory.holdingItemData.actionData[0].Delay=1000000f;Need(Begin(packet,out state),"native pacing precondition");End(state);packet.userData=2;Need(!Begin(packet,out state),"native firing cadence gates new ordinal");Need(!Begin(packet,out state),"paced refusal cannot later reuse its ordinal");
  packet=New();packet.Sender=new ClientInfo{entityId=8};Need(!Begin(packet,out state)&&state==null,"foreign sender refused");
  packet=New();packet.Valid=false;Need(!Begin(packet,out state),"native sender validation required");
  packet=New();packet.slotIdx=1;Need(!Begin(packet,out state),"unheld slot refused");
  packet=New();packet.userData=0;Need(!Begin(packet,out state),"old unmapped shot ordinal cannot acquire hit");
  packet=New();action.UsesNativeInfiniteSpray=false;Need(!Begin(packet,out state),"finite ammunition needs its own payment evidence");
  packet=New();packet.firingState=0;Need(Begin(packet,out state)&&state==null,"native release passes without hit");End(state);Need(action.Hits==0,"release never extinguishes");
  packet=New();Need(Begin(packet,out state),"actual native callback precondition");End(state,true,false);Need(action.Hits==0,"normal Process return alone is not an actual native action witness");
  packet=New();Need(Begin(packet,out state),"native precondition");End(state,false);Need(action.Hits==0,"skipped original cannot supply hit");Need(!Begin(packet,out state),"skipped/unknown attempt not replayed");
  packet=New();Need(Begin(packet,out state),"held precondition");world.Actor.inventory.holdingItemData.item.Actions[0]=new ItemAction();End(state);Need(action.Hits==0,"changed actual action source refused");
  packet=New();Need(Begin(packet,out state),"item retirement precondition");world.Actor.inventory.holdingItemData.item=null;End(state);Need(action.Hits==0,"retired held item must refuse without throwing into native callback");
  packet=New();Need(Begin(packet,out state),"action list retirement precondition");world.Actor.inventory.holdingItemData.item.Actions=null;End(state);Need(action.Hits==0,"retired action array must refuse without observer exception");
  packet=New();Need(Begin(packet,out state),"peer collection retirement precondition");SingletonMonoBehaviour<ConnectionManager>.Instance.Clients=null;End(state);Need(action.Hits==0,"retired peer collection must refuse without observer exception");
  packet=New();Need(Begin(packet,out state),"world precondition");GameManager.Instance.World=new World();End(state);Need(action.Hits==0,"world replacement cannot use old source");
  packet=New();Need(Begin(packet,out state),"actor-world precondition");world.Actor.world=new World();End(state);Need(action.Hits==0,"retargeted actor world authorized hit");
  packet=New();Need(Begin(packet,out state),"item-world precondition");world.Actor.inventory.holdingItemData.world=new World();End(state);Need(action.Hits==0,"retargeted native item world authorized hit");
  packet=New();world.Actor.inventory.holdingItemData.actionData[0].Delay=float.PositiveInfinity;Need(!Begin(packet,out state),"nonfinite native delay refused");
  packet=New();packet.actionIdx=1;Need(Begin(packet,out state)&&state==null,"unrelated action path has no custom hit authority");
  packet=New();SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
  Need(Begin(packet,out state)&&state==null,"non-authority native process cannot issue custom shot source");
  packet=New();Need(Begin(packet,out state),"authority retirement precondition");SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;
  End(state);Need(action.Hits==0,"retired authority cannot apply native hit");
  System.Console.WriteLine("PASS actual native-shot dispatcher with explicit transport/Harmony doubles; physics/gameplay NOT RUN");
 }
}
