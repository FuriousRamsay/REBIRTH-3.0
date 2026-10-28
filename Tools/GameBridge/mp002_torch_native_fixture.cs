// AUTHORED_NOT_RUN. Actual native-hit observer/receiver; native physics/transport doubled.
// ACTUAL_TORCH_NATIVE
namespace HarmonyLib{[System.AttributeUsage(System.AttributeTargets.Class)]class HarmonyPatch:System.Attribute{public HarmonyPatch(System.Type t,string method){}}}
class TagGroup{internal class Global{}}
struct FastTags<T>{internal static FastTags<T> Parse(string text)=>new FastTags<T>();public static FastTags<T> operator |(FastTags<T> a,FastTags<T> b)=>a;}
enum PassiveEffects{AttacksPerMinute}
static class EffectManager{internal static float Rate=60;internal static float GetValue(PassiveEffects effect,ItemValue item,float value,EntityPlayer actor,object unused,FastTags<TagGroup.Global> tags)=>Rate;}
class ItemActionData{}
class ItemActionDynamic{internal class ItemActionDynamicData:ItemActionData{}}
class ItemActionDynamicMelee:ItemActionDynamic{
 internal bool Throw;internal System.Action DuringTarget;internal int Rays;
 internal ItemActionData CreateModifierData(ItemInventoryData held,int index)=>new ItemActionDynamicData();
 internal WorldRayHitInfo GetExecuteActionTarget(ItemActionDynamicData data){Rays++;DuringTarget?.Invoke();if(Throw)throw new System.InvalidOperationException();return new WorldRayHitInfo();}
}
class ItemActionAttack{
 internal enum EnumAttackMode{RealNoHarvesting,Simulate}
 internal class AttackHitInfo{internal bool bBlockHit=true,bKilled;internal int damageGiven=1;}
 public static void Hit(){}internal static BlockValue GetBlockHit(World world,WorldRayHitInfo hit)=>new BlockValue();
}
struct BlockValue{internal bool isair;}
struct Vector3i{}
class WorldRayHitInfo{internal bool bHitValid=true;internal string tag;internal Hit hit=new Hit();internal WorldRayHitInfo Clone()=>this;}
class Hit{internal Vector3i blockPos;}
class ItemClass{internal string Name="meleeToolTorch";internal ItemActionDynamicMelee[] Actions={new ItemActionDynamicMelee()};internal FastTags<TagGroup.Global> ItemTags;internal string GetItemName()=>Name;}
class ItemValue{internal ItemClass ItemClass;}
class ItemInventoryData{internal ItemClass item=new ItemClass();internal ItemValue itemValue=new ItemValue();}
class Inventory{internal int holdingItemIdx;internal ItemInventoryData holdingItemData=new ItemInventoryData();}
class EntityAlive{internal World world;internal int entityId=7;}
class EntityPlayer:EntityAlive{internal Inventory inventory=new Inventory();internal int Died,Layer=4;internal bool Dead;internal bool IsDead()=>Dead;internal int GetModelLayer()=>Layer;internal void SetModelLayer(int value){Layer=value;}}
class EntityPlayerLocal:EntityPlayer{}
class WorldState{internal string Guid="world-A";}
class World{internal bool Remote;internal EntityPlayer Actor;internal WorldState worldState=new WorldState();internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Actor!=null&&Actor.entityId==id?Actor:null;internal EntityPlayer GetPrimaryPlayer()=>Actor;}
class MinEventParams{internal EntityAlive Self;internal ItemValue ItemValue;}
enum RebirthFireIgnitionCause{BlockEvent,Projectile}
static class RebirthFireMinEventHelpers{internal static bool IsCurrentEventWorld(MinEventParams context,World world)=>context?.Self!=null&&ReferenceEquals(context.Self.world,world)&&ReferenceEquals(GameManager.Instance.World,world);}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal bool IsPaused()=>false;}
class ClientInfo{internal int entityId=7;}
class Clients{internal ClientInfo Peer;internal ClientInfo ForEntityId(int id)=>Peer;}
class ConnectionManager{internal bool IsServer;internal Clients Clients=new Clients();internal int Sends;internal void SendToServer(NetPackageRebirthTorchNativeHit packet,bool flush){Sends++;}}
static class SingletonMonoBehaviour<T>where T:new(){internal static T Instance=new T();}
class NetPackageRebirthTorchNativeHit{internal ClientInfo Sender;internal bool Valid=true;internal bool ValidEntityIdForSender(int id)=>Valid;internal NetPackageRebirthTorchNativeHit Setup(int actor,int slot,int deaths,string world,long ordinal)=>this;}
static class NetPackageManager{internal static T GetPackage<T>()where T:new()=>new T();}
static class RebirthFireRuntimePolicy{internal static bool Enabled=true;}
class RebirthFireService{internal static RebirthFireService Instance=new RebirthFireService();internal int Fires;internal void ScheduleIgnition(World world,Vector3i[] positions,int actor,RebirthFireIgnitionCause cause,float delay){Fires++;}}
static class Log{internal static void Warning(string text){}}
class Program{
 static readonly System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
 static System.Reflection.MethodInfo Hook(string name)=>typeof(RebirthTorchNativeHit.NativeHit).GetMethod(name,Flags);
 static void Need(bool value,string message){if(!value)throw new System.Exception(message);}
 static World Server(out NetPackageRebirthTorchNativeHit packet){
  var actor=new EntityPlayer();var world=new World{Actor=actor};actor.world=world;GameManager.Instance.World=world;
  var manager=SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=true};
  packet=new NetPackageRebirthTorchNativeHit{Sender=new ClientInfo()};manager.Clients.Peer=packet.Sender;EffectManager.Rate=60;
  return world;
 }
 static void Main(){
  var local=new EntityPlayerLocal();var world=new World{Actor=local,Remote=true};local.world=world;GameManager.Instance.World=world;
  var manager=SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
  var context=new MinEventParams{Self=local,ItemValue=new ItemValue{ItemClass=local.inventory.holdingItemData.item}};
  Need(!RebirthTorchNativeHit.Capture(context,RebirthFireIgnitionCause.BlockEvent,0,false,0),"event without actual native Hit admitted");
  object[] args={7,new ItemActionAttack.AttackHitInfo(),ItemActionAttack.EnumAttackMode.RealNoHarvesting,true,null};Hook("Prefix").Invoke(null,args);
  Need(RebirthTorchNativeHit.Capture(context,RebirthFireIgnitionCause.BlockEvent,0,false,0),"actual configured native damage event missed");
  Hook("Postfix").Invoke(null,new[]{(object)true,args[4]});Hook("Finalizer").Invoke(null,new object[]{null,args[4]});Need(manager.Sends==1,"native return did not issue one courier");
  args=new object[]{7,new ItemActionAttack.AttackHitInfo{damageGiven=0},ItemActionAttack.EnumAttackMode.RealNoHarvesting,true,null};Hook("Prefix").Invoke(null,args);
  Need(RebirthTorchNativeHit.Capture(context,RebirthFireIgnitionCause.BlockEvent,0,false,0),"actual zero-damage onSelfDamagedBlock lost native event parity");
  Hook("Postfix").Invoke(null,new[]{(object)true,args[4]});Hook("Finalizer").Invoke(null,new object[]{null,args[4]});Need(manager.Sends==2,"native zero-damage event did not return through courier");
  world=Server(out var packet);int fires=RebirthFireService.Instance.Fires;
  RebirthTorchNativeHit.Receive(packet,world,7,0,0,"world-A",1);Need(RebirthFireService.Instance.Fires==fires+1,"actual server target missing");
  RebirthTorchNativeHit.Receive(packet,world,7,0,0,"world-A",1);Need(RebirthFireService.Instance.Fires==fires+1,"duplicate original replayed");
  world=Server(out packet);fires=RebirthFireService.Instance.Fires;
  RebirthTorchNativeHit.Receive(packet,world,7,0,1,"world-A",1);RebirthTorchNativeHit.Receive(packet,world,7,0,0,"world-B",1);
  packet.Valid=false;RebirthTorchNativeHit.Receive(packet,world,7,0,0,"world-A",1);packet.Valid=true;
  Need(RebirthFireService.Instance.Fires==fires,"wrong life/world/sender authorized fire");
  world=Server(out packet);var action=world.Actor.inventory.holdingItemData.item.Actions[0];action.Throw=true;
  try{RebirthTorchNativeHit.Receive(packet,world,7,0,0,"world-A",1);throw new System.Exception("missing native exception");}catch(System.InvalidOperationException){}
  Need(world.Actor.Layer==4,"native model layer not restored");action.Throw=false;
  RebirthTorchNativeHit.Receive(packet,world,7,0,0,"world-A",1);Need(RebirthFireService.Instance.Fires==fires,"unknown native target retried");
  world=Server(out packet);action=world.Actor.inventory.holdingItemData.item.Actions[0];action.DuringTarget=()=>world.Actor.inventory.holdingItemData.item.Actions[0]=new ItemActionDynamicMelee();
  RebirthTorchNativeHit.Receive(packet,world,7,0,0,"world-A",1);Need(RebirthFireService.Instance.Fires==fires,"replaced actual action authorized fire");
  world=Server(out packet);world.Actor.world=new World();
  RebirthTorchNativeHit.Receive(packet,world,7,0,0,"world-A",1);Need(RebirthFireService.Instance.Fires==fires,"foreign actor world borrowed registered numeric ID");
  System.Console.WriteLine("PASS actual torch action courier with explicit native doubles; native/gameplay NOT RUN");
 }
}
