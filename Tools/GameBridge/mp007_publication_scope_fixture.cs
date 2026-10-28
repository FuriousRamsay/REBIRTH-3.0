// AUTHORED_NOT_RUN. Actual consume request handler, publication builder and
// complete owner publication class. Native consumption/identity/codec are adapters.
using System;using System.IO;using System.Collections.Generic;
enum RebirthMetabolismSourceKind:byte{HeldToolbelt,Backpack,HydrationSlot}
enum RebirthMetabolismConsumeFailure:byte{None,ItemChanged}
struct RebirthConsumeResult{internal bool Success;internal RebirthMetabolismConsumeFailure Failure;}
class ItemValue{internal int type=1;internal ushort Seed=1;internal string Meta="item",Texture="native-texture";internal ItemValue Clone()=>(ItemValue)MemberwiseClone();public bool Equals(ItemValue other)=>other!=null&&type==other.type&&Meta==other.Meta;}
class ItemStack{internal int count=3;internal ItemValue itemValue=new ItemValue();internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack{count=count,itemValue=itemValue.Clone()};}
class Slots{internal Slots ItemGrid=>this;internal ItemStack[] items=new[]{new ItemStack(),new ItemStack(),new ItemStack()};internal int Writes;internal Action During;internal void SetSlot(int slot,ItemStack stack){Writes++;items[slot]=stack;During?.Invoke();}internal void SetItem(int slot,ItemStack stack)=>SetSlot(slot,stack);}
class WorldState{internal string Guid="world";}
class World{internal bool Remote;internal WorldState worldState=new WorldState();internal EntityPlayer Player;internal bool IsRemote()=>Remote;internal EntityPlayer GetPrimaryPlayer()=>Player;internal object GetEntity(int id)=>Player?.entityId==id?Player:null;}
class EntityPlayer{internal int entityId=7,Died;internal bool Dead,Held;internal string Creation="creation";internal World world;internal Slots bag=new Slots(),inventory=new Slots();internal bool IsDead()=>Dead;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class ClientInfo{internal int entityId=7;internal bool loginDone=true,disconnecting;}
class Clients{internal ClientInfo Peer;internal ClientInfo ForEntityId(int id)=>Peer?.entityId==id?Peer:null;}
class ConnectionManager{internal bool IsServer=true;internal Clients Clients=new Clients();internal List<NetPackageRebirthMetabolismConsumeResult> Replies=new List<NetPackageRebirthMetabolismConsumeResult>();internal void SendPackage(NetPackageRebirthMetabolismConsumeResult packet,int _attachedToEntityId){Replies.Add(packet);}}
static class SingletonMonoBehaviour<T>where T:new(){internal static T Instance=new T();}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthSurvivorMode{internal static bool IsEnabledForCurrentWorld()=>true;}
static class RebirthCharacterCreationHoldService{internal static bool IsHeld(EntityPlayer p)=>p.Held;}
static class RebirthSurvivorClientState{internal static string GetProjectedCreationId(EntityPlayer p)=>p.Creation;}
static class RebirthSurvivorRequestScope{internal static bool TryNormalize(string input,out string value){value=input;return !string.IsNullOrEmpty(input);}}
class Origin{internal string CreationId;}
class RebirthWorldCharacterRecord{internal bool IsComplete=true;internal Origin Origin;}
static class RebirthWorldCharacterService{internal static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord record){record=new RebirthWorldCharacterRecord{Origin=new Origin{CreationId=p.Creation}};return true;}}
static class RebirthWorldCharacterRepository{internal static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>true;}
static class RebirthToolbeltCapacity{internal static int GetOwnedSlotCount(EntityPlayer p,int length)=>length-1;}
static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&a.itemValue.type==b.itemValue.type&&a.itemValue.Seed==b.itemValue.Seed&&a.itemValue.Meta==b.itemValue.Meta&&a.itemValue.Texture==b.itemValue.Texture;}
static class RebirthNativeItemCodec{internal static string Encode(ItemValue value)=>value.type+"|"+value.Seed+"|"+value.Meta+"|"+value.Texture;}
static class RebirthConsumptionTrace{internal static IDisposable Begin()=>null;internal static void Note(string stage,EntityPlayer player=null,Guid publication=default(Guid),RebirthMetabolismSourceKind kind=default(RebirthMetabolismSourceKind),int slot=-1,ItemStack before=null,ItemStack after=null,string reason=null,bool? success=null){}}
static class RebirthMetabolismService{
 internal static int Calls;internal static Action During;
 internal static bool TryFindMatchingStack(EntityPlayer p,int type,ushort seed,out RebirthMetabolismSourceKind kind,out int slot,out ItemStack stack,byte[] expectedFingerprint=null){kind=RebirthMetabolismSourceKind.Backpack;slot=0;stack=p.bag.items[0];return true;}
 internal static RebirthConsumeResult ConsumeInventorySource(EntityPlayer p,RebirthMetabolismSourceKind kind,int slot,ItemStack expected,bool autoSip){Calls++;var value=p.bag.items[0].Clone();value.count--;p.bag.SetSlot(0,value);var action=During;During=null;action?.Invoke();return new RebirthConsumeResult{Success=true};}
}
class NetPackage{internal ClientInfo Sender;internal bool ValidEntityIdForSender(int id)=>Sender?.entityId==id;public virtual void ProcessPackage(World world,GameManager callbacks){}}
static class NetPackageManager{internal static T GetPackage<T>()where T:new()=>new T();}
class NetPackageRebirthMetabolismConsumeResult:NetPackage{
 private RebirthConsumeResult result;private Guid publication;private int ownerId,sourceSlot,ownerDeaths;private RebirthMetabolismSourceKind sourceKind;private ItemStack before,after;private string beforeItem,afterItem,ownerCreation;
 // ACTUAL_SETUP
 // ACTUAL_WITH_INVENTORY
 // ACTUAL_IDENTITY
}
static class RebirthConsumptionSourceIdentity{internal static bool MatchesScope(string world,string creation,int deaths,byte[] expected)=>true;}
// This cohort retains the legacy publication branch. The player native original
// route is separately composed; its transport methods are explicit inactive adapters.
static class RebirthConsumptionDurableDelivery{internal static bool InventoryProtocolActive=>false;internal static void ReceiveInventoryRequest(EntityPlayer player,Guid operation,RebirthMetabolismSourceKind kind,int slot,int count,byte[] fingerprint,bool autoSip){throw new Exception("Native original route belongs to separate cohort");}}
static class RebirthRemoteResourceNetworkScope{internal static bool Receive(NetPackage packet,World world,GameManager callbacks,bool server,out object connection){connection=null;return false;}}
class ActualRequest:NetPackage{private int playerId=7,itemType=1;private ushort seed=1;private bool autoSip;private Guid operation;private RebirthMetabolismSourceKind requestedKind;private int requestedSlot,requestedCount;private byte[] sourceFingerprint=new byte[32],requestScope=new byte[32];
 // ACTUAL_REQUEST
}
// ACTUAL_OWNER_PUBLICATION
static class Program{
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 static EntityPlayer Owner(out World world,bool remote=false){world=new World{Remote=remote};var player=new EntityPlayer{world=world};world.Player=player;GameManager.Instance=new GameManager{World=world};ThreadManager.Main=true;return player;}
 static void Main(){
  for(int variant=0;variant<13;variant++){
   var player=Owner(out var world);var peer=new ClientInfo();var manager=SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();manager.Clients.Peer=peer;var request=new ActualRequest{Sender=peer};
   RebirthMetabolismService.Calls=0;RebirthMetabolismService.During=()=>{switch(variant){case 1:player.Died++;break;case 2:player.Creation="other";break;case 3:GameManager.Instance.World=new World();break;case 4:world.worldState=new WorldState();break;case 5:world.worldState.Guid="other";break;case 6:player.bag.items=new ItemStack[0];break;case 7:peer.disconnecting=true;break;case 8:peer.loginDone=false;break;case 9:SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();break;case 10:manager.Clients.Peer=new ClientInfo();break;case 11:world.Player=new EntityPlayer{world=world};break;case 12:GameManager.Instance=new GameManager{World=world};break;}};
   request.ProcessPackage(world,GameManager.Instance);Need(RebirthMetabolismService.Calls==1&&manager.Replies.Count==(variant==0?1:0),"stale/ended native action published a financial reply "+variant);
  }
  for(int variant=0;variant<8;variant++){
   var player=Owner(out var world,true);var before=player.bag.items[0].Clone();var after=before.Clone();after.count--;var id=Guid.NewGuid();
   player.bag.During=()=>{switch(variant){case 1:player.Died++;break;case 2:player.Creation="other";break;case 3:GameManager.Instance.World=new World();break;case 4:player.bag.items=new ItemStack[0];break;case 5:player.bag.items[0].itemValue.Texture="changed";break;case 6:world.worldState=new WorldState();break;case 7:world.Player=new EntityPlayer{world=world};break;}};
   bool applied=RebirthConsumptionOwnerPublication.Apply(world,7,id,RebirthMetabolismSourceKind.Backpack,0,before,after,"creation");
   Need(applied==(variant==0),"owner callback changed original scope or native postimage "+variant);
   if(variant==0)Need(RebirthConsumptionOwnerPublication.WasApplied(world,7,id,"creation"),"positive owner publication not retained");
  }
  var current=Owner(out var currentWorld,true);var expected=current.bag.items[0].Clone();var replacement=expected.Clone();replacement.count--;
  current.bag.items[0].itemValue.Texture="changed";
  Need(!RebirthConsumptionOwnerPublication.Apply(currentWorld,7,Guid.NewGuid(),RebirthMetabolismSourceKind.Backpack,0,expected,replacement,"creation")&&current.bag.Writes==0,"texture-only source difference overwritten");
  Need(!RebirthConsumptionOwnerPublication.Apply(currentWorld,7,Guid.NewGuid(),RebirthMetabolismSourceKind.HeldToolbelt,2,expected,replacement,"creation")&&current.inventory.Writes==0,"hidden belt slot written");
  var textureAfter=expected.Clone();textureAfter.itemValue.Texture="different";
  var packet=new NetPackageRebirthMetabolismConsumeResult().Setup(new RebirthConsumeResult{Success=true}).WithInventory(7,RebirthMetabolismSourceKind.Backpack,0,expected,textureAfter,0,"creation");
  Need(packet.TryGetPublicationIdentity(out _,out var publication)&&publication!=Guid.Empty,"full native change omitted by publication builder");
  Console.WriteLine("PASS actual consumption request/post-effect and owner publication scope/full-item routing; native consumption/identity/codec are adapters, persistence/reconnect NOT_RUN");
 }
}
