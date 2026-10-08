using System;
public class EntityPlayerLocal:EntityPlayer{}
public class ItemClass{public string GetItemName()=>"daypack";}
public class ItemValue{public string Data="full-native-bytes";public int type=1;public ushort Seed=2;public bool Empty;public ItemClass ItemClass=new();public bool IsEmpty()=>Empty;}
public class Profile{public string Kind="survivor_gear";}
public static class RebirthSurvivorDefinitionRegistry{public static Profile Profile=new();public static bool Known=true;public static bool TryGetSupportByGearItem(string key,out Profile profile){profile=Profile;return Known;}}
public class ConnectionManager{public bool IsServer;public int Sends;public void SendToServer(object request){Sends++;}public object[] connectionToServer={new object()};}
public static class SingletonMonoBehaviour<T>{public static T Instance;}
public static class RebirthGearOwnerReservation{public static bool Match;public static object Expected;public static bool MatchesIntent(EntityPlayerLocal p,object peer,object original)=>Match&&ReferenceEquals(Expected,original);}
public static class RebirthGearPreparationClient{
 public static bool Capture=true,Queued;public static bool Throw;public static int Captures,Requests,Index;public static bool Bag;public static Guid Transaction;public static object Original;
 public static bool TryCreateOriginal(EntityPlayerLocal p,ItemValue item,bool bag,int index,Guid tx,out object original){Captures++;Bag=bag;Index=index;Transaction=tx;Original=new();original=Original;return Capture;}
 public static string UnequipSlot;public static bool TryCreateUnequipOriginal(EntityPlayerLocal p,string slot,Guid tx,out RebirthGearPreparationIntent original){Captures++;UnequipSlot=slot;Transaction=tx;original=new(){TransactionId=tx};Original=original;return Capture&&p!=null&&!string.IsNullOrEmpty(slot);}
 public static bool TryRequestOriginal(EntityPlayerLocal p,object original){Requests++;if(Throw)throw new Exception();RebirthGearOwnerReservation.Expected=original;return Queued;}
}
static class Program{
 static int n;static EntityPlayerLocal player=new();static ItemValue item=new();
 static void Check(bool yes,string label){if(!yes)throw new Exception(label);n++;}
 static void Reset(){player.inventory=new();player.bag=new();RebirthGearPreparationClient.Capture=true;RebirthGearPreparationClient.Queued=false;RebirthGearPreparationClient.Throw=false;RebirthGearPreparationClient.Captures=RebirthGearPreparationClient.Requests=0;RebirthGearOwnerReservation.Match=false;RebirthGearOwnerReservation.Expected=null;RebirthSurvivorDefinitionRegistry.Known=true;RebirthSurvivorDefinitionRegistry.Profile=new();SingletonMonoBehaviour<ConnectionManager>.Instance=new();}
 static void Main(){
 Reset();Check(!RebirthGearInitialEquipDispatcher.TryBegin(null,item,true,3)&&RebirthGearPreparationClient.Captures==0,"null owner inert");
 Reset();Check(!RebirthGearInitialEquipDispatcher.TryBegin(player,null,true,3),"null item inert");
 Reset();Check(!RebirthGearInitialEquipDispatcher.TryBegin(player,new ItemValue{Empty=true},true,3),"empty item inert");
 Reset();RebirthSurvivorDefinitionRegistry.Known=false;Check(!RebirthGearInitialEquipDispatcher.TryBegin(player,item,true,3)&&RebirthGearPreparationClient.Captures==0,"unknown item not gear");
 Reset();RebirthSurvivorDefinitionRegistry.Profile.Kind="water";Check(!RebirthGearInitialEquipDispatcher.TryBegin(player,item,true,3),"water stays separate");
 Reset();RebirthGearPreparationClient.Capture=false;Check(!RebirthGearInitialEquipDispatcher.TryBegin(player,item,true,3)&&RebirthGearPreparationClient.Requests==0,"failed exact candidate never uploads");
 Reset();RebirthGearPreparationClient.Queued=true;Check(RebirthGearInitialEquipDispatcher.TryBegin(player,item,true,12),"queued request accepted");Check(RebirthGearPreparationClient.Bag&&RebirthGearPreparationClient.Index==12&&RebirthGearPreparationClient.Transaction!=Guid.Empty,"explicit bag index and new original identity");Check(ReferenceEquals(RebirthGearOwnerReservation.Expected,RebirthGearPreparationClient.Original)&&RebirthGearPreparationClient.Requests==1,"same captured original submitted once");
 Reset();RebirthGearOwnerReservation.Match=true;Check(RebirthGearInitialEquipDispatcher.TryBegin(player,item,false,4)&&!RebirthGearPreparationClient.Bag&&RebirthGearPreparationClient.Index==4,"uncertain upload retains exact held original for pump");
 Reset();Check(!RebirthGearInitialEquipDispatcher.TryBegin(player,item,true,1),"unheld failed request reports failure");
 Reset();SingletonMonoBehaviour<ConnectionManager>.Instance=null;Check(!RebirthGearInitialEquipDispatcher.TryBegin(player,item,true,1),"missing peer cannot claim admission");
 Reset();RebirthGearPreparationClient.Throw=true;Check(!RebirthGearInitialEquipDispatcher.TryBegin(player,item,true,1),"native failure contained");
 Reset();RebirthGearPreparationClient.Queued=true;var action=new ItemActionEquipSurvivorGearRebirth();action.ExecuteInstantAction(player,new ItemStack{itemValue=item},false,new XUiC_ItemStack{StackLocation=XUiC_ItemStack.StackLocationTypes.Backpack,SlotNumber=9});Check(RebirthGearPreparationClient.Bag&&RebirthGearPreparationClient.Index==9&&RebirthGearPreparationClient.Requests==1,"actual clicked backpack action uses selected slot");Check(SingletonMonoBehaviour<ConnectionManager>.Instance.Sends==0,"selected remote action never sends legacy support request");
 Reset();RebirthGearPreparationClient.Queued=true;action.ExecuteInstantAction(player,new ItemStack{itemValue=item},true,null);Check(!RebirthGearPreparationClient.Bag&&RebirthGearPreparationClient.Index==5,"actual held instant action uses current toolbelt slot");
 Reset();RebirthGearPreparationClient.Queued=true;var data=new ItemActionEat.MyInventoryData{bEatingStarted=true,invData=new ItemInventoryData{holdingEntity=player,slotIdx=2,itemStack=new ItemStack{itemValue=item}}};action.OnHoldingUpdate(data);Check(!data.bEatingStarted&&!RebirthGearPreparationClient.Bag&&RebirthGearPreparationClient.Index==2,"actual held update binds original action inventory slot");
 Reset();RebirthGearPreparationClient.Capture=false;action.ExecuteInstantAction(player,new ItemStack{itemValue=item},false,new XUiC_ItemStack{StackLocation=XUiC_ItemStack.StackLocationTypes.Backpack,SlotNumber=9});Check(RebirthGearPreparationClient.Requests==0&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sends==0,"refused selected original does not fall back to legacy remote mutation");
 Reset();RebirthGearPreparationClient.Queued=true;Check(RebirthGearInitialEquipDispatcher.TryBeginUnequip(player,"backpack")&&RebirthGearPreparationClient.UnequipSlot=="backpack"&&RebirthGearPreparationClient.Requests==1,"unequip initial action submits same explicit slot original");
 Reset();RebirthGearOwnerReservation.Match=true;Check(RebirthGearInitialEquipDispatcher.TryBeginUnequip(player,"belt")&&RebirthGearPreparationClient.Requests==1,"uncertain unequip retains exact initial hold for pump");
 Reset();RebirthGearPreparationClient.Capture=false;Check(!RebirthGearInitialEquipDispatcher.TryBeginUnequip(player,"backpack")&&RebirthGearPreparationClient.Requests==0,"unequip failed candidate never uploads");
 Reset();Check(!RebirthGearInitialEquipDispatcher.TryBeginUnequip(null,"belt")&&RebirthGearPreparationClient.Requests==0,"unequip missing owner refused");
 Reset();RebirthGearPreparationClient.Queued=true;Check(RebirthGearInitialEquipDispatcher.TryBeginUnequip(player,"belt",out var exactTransaction)&&exactTransaction==RebirthGearPreparationClient.Transaction&&exactTransaction!=Guid.Empty,"unequip exposes admitted same original transaction");
 Reset();RebirthGearPreparationClient.Capture=false;Check(!RebirthGearInitialEquipDispatcher.TryBeginUnequip(player,"belt",out exactTransaction)&&exactTransaction==Guid.Empty,"refused unequip exposes no pickup transaction");
 Reset();RebirthGearPreparationClient.Queued=true;player.bag.ItemGrid.items[6]=new ItemStack{itemValue=item,count=1};ItemActionEquipSurvivorGearRebirth.Dispatch(player,item);Check(RebirthGearPreparationClient.Requests==1&&RebirthGearPreparationClient.Bag&&RebirthGearPreparationClient.Index==6&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sends==0,"actual generic remote dispatch resolves unique full-data bag cell through original request");
 Reset();RebirthGearPreparationClient.Queued=true;player.inventory.ItemGrid.items[1]=new ItemStack{itemValue=item,count=1};ItemActionEquipSurvivorGearRebirth.Dispatch(player,item);Check(RebirthGearPreparationClient.Requests==1&&!RebirthGearPreparationClient.Bag&&RebirthGearPreparationClient.Index==1&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sends==0,"actual generic remote dispatch resolves unique owned belt cell");
 Reset();player.bag.ItemGrid.items[6]=new ItemStack{itemValue=item,count=1};player.bag.ItemGrid.items[7]=new ItemStack{itemValue=item,count=1};ItemActionEquipSurvivorGearRebirth.Dispatch(player,item);Check(RebirthGearPreparationClient.Requests==0&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sends==0,"actual ambiguous generic dispatch never sends legacy mutation");
 Reset();ItemActionEquipSurvivorGearRebirth.Dispatch(player,item);Check(RebirthGearPreparationClient.Requests==0&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sends==0,"actual missing generic source never sends legacy mutation");
 Reset();player.bag.ItemGrid.items[6]=new ItemStack{itemValue=new ItemValue{Data="different-metadata"},count=1};ItemActionEquipSurvivorGearRebirth.Dispatch(player,item);Check(RebirthGearPreparationClient.Requests==0&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sends==0,"same type seed with different full metadata does not substitute");
 Console.WriteLine($"PASS {n} actual initial dispatcher checks; candidate, network and native boundaries doubled");
 }
}
public class World{public bool Remote=true;public bool IsRemote()=>Remote;}
public class EntityAlive{}
public class EntityPlayer:EntityAlive{public int entityId=7;public World world=new();public Inventory inventory=new(),bag=new();}
public class Inventory{public Grid ItemGrid=new();public int holdingItemIdx=5;}
public class ItemStack{public int count;public ItemValue itemValue=new();public bool IsEmpty()=>itemValue==null||itemValue.IsEmpty();}
public class DynamicProperties{}
public class ItemInventoryData{public ItemStack itemStack=new();public EntityAlive holdingEntity;public int slotIdx;}
public class ItemActionData{public ItemInventoryData invData;}
public class ItemActionEat{
 public bool Consume,UseAnimation;public class MyInventoryData:ItemActionData{public bool bEatingStarted;}
 public virtual void ReadFrom(DynamicProperties p){}public virtual bool ExecuteInstantAction(EntityAlive e,ItemStack s,bool held,XUiC_ItemStack controller)=>false;public virtual void OnHoldingUpdate(ItemActionData d){}public virtual void StopHolding(ItemActionData d){}
}
public class XUiC_ItemStack{public enum StackLocationTypes{Backpack,ToolBelt,LootContainer}public StackLocationTypes StackLocation;public int SlotNumber;}
public static class RebirthSurvivorSupportUiFeedback{public static void Receive(bool ok,string message){}}
public static class Localization{public static string Get(string key)=>key;}
public static class RebirthSurvivorGearService{public static bool TryEquipMatchingInventoryItem(EntityPlayer p,int type,ushort seed,out string message){message="";return true;}}
public static class RebirthMusicLibraryClient{public static bool CanSend(ConnectionManager c,object r)=>true;}
public enum RebirthSurvivorSupportAction{EquipGearItem}
public class NetPackageRebirthSurvivorSupportActionRequest{public object Setup(int id,RebirthSurvivorSupportAction action,ItemValue value,string slot)=>this;}
public static class NetPackageManager{public static T GetPackage<T>()where T:new()=>new();}
public class RebirthGearPreparationIntent{public Guid TransactionId;}public class Grid{public ItemStack[] items=new ItemStack[52];}
public static class RebirthToolbeltCapacity{public static int GetOwnedSlotCount(EntityPlayerLocal p,int length)=>4;}
public static class RebirthNativeItemCodec{public static string Encode(ItemValue item)=>item.Data;}
public class RebirthGearInventoryPlan{public class Stack{public string ItemData;public int Count;}}
public class RebirthGearInventorySnapshot{
 public RebirthGearInventoryPlan.Stack[] Bag,Belt;public int OwnedBeltSlots;
 public static bool TryCapture(ItemStack[] bag,ItemStack[] belt,int owned,out RebirthGearInventorySnapshot snapshot){snapshot=null;if(bag==null||belt==null)return false; snapshot=new(){Bag=Copy(bag),Belt=Copy(belt),OwnedBeltSlots=owned};return true;}
 static RebirthGearInventoryPlan.Stack[] Copy(ItemStack[] input){var output=new RebirthGearInventoryPlan.Stack[input.Length];for(int i=0;i<input.Length;i++)output[i]=new(){ItemData=input[i]?.itemValue?.Data,Count=input[i]?.count??0};return output;}
}