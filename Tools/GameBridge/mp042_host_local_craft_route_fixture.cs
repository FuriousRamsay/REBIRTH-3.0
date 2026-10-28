// AUTHORED_NOT_RUN. Actual owning action prefix and shortfall selector; source/native original services are explicit adapters.
using System;using System.Linq;using System.Collections.Generic;
class ItemValue{internal int type=1;internal ItemValue Clone()=>new ItemValue{type=type};}
class ItemStack{internal ItemValue itemValue=new ItemValue();internal int count;internal ItemStack(){}internal ItemStack(ItemValue value,int amount){itemValue=value;count=amount;}}
class World{internal bool Remote;internal bool IsRemote()=>Remote;}
class EntityPlayerLocal{internal World world=new World();}
class PlayerUI{internal EntityPlayerLocal entityPlayer=new EntityPlayerLocal();}
class XUi{internal PlayerUI playerUI=new PlayerUI();}
class Controller{internal XUi xui=new XUi();internal Controller Parent;internal virtual T GetChildByType<T>()where T:class=>null;internal T GetParentByType<T>()where T:class=>Parent as T;}
class Recipe{internal bool IsScrap;}
class RebirthCamperStationState{}
class WorkstationData{internal object TileEntity;}
class XUiC_WorkstationWindowGroup:Controller{internal WorkstationData WorkstationData=new WorkstationData();}
// Managed station producers are outside this personal-action cohort.
static class RebirthCookingHeat{internal static bool Managed(Recipe recipe)=>false;}
static class RebirthRecyclerRuntime{internal static bool IsMarked(Recipe recipe)=>false;}
static class RebirthStationGridQueue{internal static bool IsMarked(Recipe recipe)=>false;}
class XUiC_RecipeEntry:Controller{internal Recipe Recipe=new Recipe();}
class WindowGroup{internal Controller Controller=new Controller();}
class CraftCount{internal int Count=2;internal WindowGroup WindowGroup=new WindowGroup();}
class ItemActionEntryCraft{internal Controller ItemController=new XUiC_RecipeEntry();internal CraftCount craftCountControl=new CraftCount();internal List<ItemStack> tempIngredientList=new List<ItemStack>{new ItemStack(new ItemValue(),2)};internal bool HasItems=true;internal bool hasItems(XUi ui,Recipe recipe)=>HasItems;}
class XUiC_WorkstationInputGrid{internal bool HasItems(List<ItemStack> items,int multiplier)=>false;}
enum RemoteResourceClientOperation{Craft}
static class RemoteResourcesRuntimePolicy{internal static bool Enabled=true;}
static class RemoteResourceDiagnostics{internal static void Write(string text){}}
static class RemoteResourceSnapshotCache{internal static int Rebuilds;internal static void ForceRebuild(EntityPlayerLocal player){Rebuilds++;}}
static class RemoteResourceTransactions{internal static int Count,Reads;internal static int CountRemote(EntityPlayerLocal player,ItemValue value){Reads++;return Count;}}
static class RemoteResourceClientAvailability{internal static int Count,Reads;internal static int GetCount(EntityPlayerLocal player,ItemValue value){Reads++;return Count;}internal static void InvalidateAndRequest(EntityPlayerLocal player){}}
static class RebirthRemoteResourceCraftOriginal{internal static bool Personal=true;internal static bool IsPersonalEntry(ItemActionEntryCraft entry)=>Personal;}
static class RebirthRemoteResourceCraftProtocol{internal static int Begins;internal static bool Accept=true;internal static bool TryAttach(ItemActionEntryCraft entry)=>false;internal static bool Begin(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> requirements,List<ItemStack> remote){Begins++;return Accept;}}
static class RebirthRemoteResourcePlacedStationOriginal{internal static bool ProtocolActive;internal static bool IsEntryCandidate(EntityPlayerLocal player,ItemActionEntryCraft entry)=>false;internal static void BeginClient(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> requirements,List<ItemStack> remote){throw new Exception("personal action entered placed station path");}}
static class RebirthRemoteResourceFetchedStationOriginal{internal static bool ProtocolActive;internal static bool IsEntryCandidate(EntityPlayerLocal player,ItemActionEntryCraft entry)=>false;internal static void BeginClient(EntityPlayerLocal player,ItemActionEntryCraft entry,ulong epoch,ulong request,List<ItemStack> requirements,List<ItemStack> remote){throw new Exception("personal action entered fetched station path");}}
static class RebirthRemoteResourcePlacedStationScope{internal enum PaymentSource{StationInput,Player}internal static bool TryObserveWindow(EntityPlayerLocal player,ItemActionEntryCraft entry,out object ignored,out PaymentSource payment){ignored=null;payment=PaymentSource.Player;return false;}}
static class RebirthRemoteResourcePlacedStationProjection{internal static bool TryCurrentShortfall(ItemActionEntryCraft entry,out List<ItemStack> needs,out List<ItemStack> remote){needs=remote=null;return false;}}
class Caller
{
 static bool IsDedicatedClient;static ItemActionEntryCraft resumeCraft;static readonly HashSet<ItemActionEntryCraft> craftWaiting=new HashSet<ItemActionEntryCraft>();
 class PendingCraft{internal ulong RequestId;internal ItemActionEntryCraft Entry;internal Recipe Recipe;internal int CraftCount;internal List<ItemStack> FullRequirements;}
 static readonly Dictionary<ulong,PendingCraft> pendingCraft=new Dictionary<ulong,PendingCraft>();static ulong clientSessionEpoch=1;static bool LocalOnly;static int NativeLeases;
 static ulong NextRequestId()=>5;static List<ItemStack> CloneRequirements(List<ItemStack> items,int count)=>items.Select(item=>new ItemStack(item.itemValue.Clone(),item.count*count)).ToList();
 static int CountConsumableLocal(EntityPlayerLocal player,ItemValue value)=>LocalOnly?10:0;static bool HasAllLocal(EntityPlayerLocal player,List<ItemStack> requirements)=>LocalOnly;
 static bool BeginLease(ulong request,RemoteResourceClientOperation operation){NativeLeases++;return false;}static bool SendRequest(EntityPlayerLocal player,ulong request,RemoteResourceClientOperation kind,List<ItemStack> needs)=>false;static void AbandonLease(ulong request){}
 // ACTUAL_PREFIX
 // ACTUAL_SHORTFALL
 static void Need(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Main(){foreach(bool remote in new[]{false,true})for(int variant=0;variant<7;variant++)
 {
  var entry=new ItemActionEntryCraft();entry.ItemController.xui.playerUI.entityPlayer.world.Remote=remote;IsDedicatedClient=remote;resumeCraft=null;LocalOnly=false;NativeLeases=0;RemoteResourcesRuntimePolicy.Enabled=true;RebirthRemoteResourceCraftOriginal.Personal=true;RebirthRemoteResourceCraftProtocol.Begins=0;RebirthRemoteResourceCraftProtocol.Accept=true;RemoteResourceSnapshotCache.Rebuilds=RemoteResourceTransactions.Reads=RemoteResourceClientAvailability.Reads=0;RemoteResourceTransactions.Count=RemoteResourceClientAvailability.Count=10;
  if(variant==1)resumeCraft=entry;if(variant==2)LocalOnly=true;if(variant==3)entry.HasItems=false;if(variant==4)RemoteResourceTransactions.Count=RemoteResourceClientAvailability.Count=0;if(variant==5)RebirthRemoteResourceCraftProtocol.Accept=false;if(variant==6)RemoteResourcesRuntimePolicy.Enabled=false;
  bool native=BeforeCraftActivated(entry);Need(native==(variant==1||variant==2||variant==3||variant==6),"owning prefix permitted unpaid personal queue "+remote+"/"+variant);Need(RebirthRemoteResourceCraftProtocol.Begins==(variant==0||variant==5?1:0)&&NativeLeases==0,"personal action abandoned saved protocol on refusal");
  if(variant==0)Need(remote?RemoteResourceClientAvailability.Reads>0&&RemoteResourceTransactions.Reads==0:RemoteResourceTransactions.Reads>0&&RemoteResourceClientAvailability.Reads==0,"shortfall trusted wrong topology projection");
 }
 Console.WriteLine("MP042 host-local/remote personal crafting actual action prefix and shortfall selector authored; durable source/native queue services remain separately qualified.");}
}
