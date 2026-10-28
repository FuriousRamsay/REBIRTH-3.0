// AUTHORED_NOT_RUN. Native Load capability, UI/scope, codec and receipt lookup
// are explicit adapters; source sidecars/publication/constructor are actual.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic;
class ItemClass{internal string Name="cosmetic";internal string GetItemName()=>Name;}
class ItemValue{internal ItemClass ItemClass=new ItemClass();internal int type=1;internal string Rich="raw";internal bool IsEmpty()=>type==0;internal ItemValue Clone()=>new ItemValue{type=type,Rich=Rich,ItemClass=ItemClass};}
class ItemStack{internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue value,int amount){itemValue=value;count=amount;}internal bool IsEmpty()=>count<=0;internal ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);}
static class RebirthNativeItemCodec{internal static string Encode(ItemValue value)=>value.type+":"+value.Rich;internal static bool TryDecode(string text,out ItemValue value){value=null;var parts=text.Split(':');if(parts.Length!=2)return false;value=new ItemValue{type=int.Parse(parts[0]),Rich=parts[1]};return true;}}
static class RebirthCamperInventorySnapshot{internal static byte[] Capture(ItemStack[] rows)=>Encoding.UTF8.GetBytes(string.Join(";",rows.Select(row=>row.count+","+RebirthNativeItemCodec.Encode(row.itemValue))));internal static ItemStack[] Restore(byte[] bytes)=>Encoding.UTF8.GetString(bytes).Split(';').Select(row=>{var pair=row.Split(',');RebirthNativeItemCodec.TryDecode(pair[1],out var item);return new ItemStack(item,int.Parse(pair[0]));}).ToArray();}
static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&RebirthNativeItemCodec.Encode(a.itemValue)==RebirthNativeItemCodec.Encode(b.itemValue);}
class World{internal string Guid="world";internal bool IsRemote()=>false;}
class GameManager{internal static GameManager Instance=new GameManager();internal void SaveLocalPlayerData(){}}
class Recipe{internal int count=1,itemValueType=1;internal bool IsScrap;internal List<ItemStack> ingredients=new List<ItemStack>();}
// Quest dispatch adapter invokes the actual production callback admission and
// positive-return observer; no native quest effect is claimed by this cohort.
class QuestEventManager{internal int ScrapCalls;internal ItemStack LastScrap;internal void ScrappedItem(ItemStack item){var state=RebirthRecipeOutputIntegration.EnterPersonalOutputScrappedItem(item,true);ScrapCalls++;LastScrap=item;RebirthRecipeOutputIntegration.ObservePersonalOutputCraftedItem(state,true,null);}}
class Buffs{internal Dictionary<string,float> Values=new Dictionary<string,float>();internal float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0;}
class ItemGrid{internal ItemStack[] items=new[]{new ItemStack(new ItemValue(),0)};}
class NativeInventory{internal ItemGrid ItemGrid=new ItemGrid();}
class Equipment{internal EntityPlayerLocal Actor;internal int State,UnlockCalls;internal bool FailUnlock;internal void UnlockCosmeticItem(ItemClass item){var state=RebirthRecipeOutputIntegration.EnterOutputScrapUnlock(this,item,true);UnlockCalls++;State=1;var error=FailUnlock?new Exception("native cosmetic callback"):null;RebirthRecipeOutputIntegration.ObserveOutputScrapUnlock(state,true,error);if(error!=null)throw error;}}
class EntityPlayerLocal{internal Equipment equipment;internal EntityPlayerLocal(){equipment=new Equipment{Actor=this};}internal World world=new World();internal Buffs Buffs=new Buffs();internal PlayerUI PlayerUI=new PlayerUI();internal NativeInventory bag=new NativeInventory(),inventory=new NativeInventory();}
class PlayerUI{internal XUi xui=new XUi();}
class XUiM_PlayerInventory{}
class XUi{internal XUiM_PlayerInventory PlayerInventory=new XUiM_PlayerInventory();internal PlayerUI playerUI;internal XUiC_CraftingQueue Queue=new XUiC_CraftingQueue();internal string NativeQueue="before-queue";internal XUi FindWindowGroupByName(string name)=>this;internal T GetChildByType<T>() where T:class=>Queue as T;internal string GetCraftingData()=>NativeQueue;}
class XUiC_CraftingQueue{internal XUiC_RecipeStack[] Cards;internal XUiC_RecipeStack[] GetRecipesToCraft()=>Cards;}
class XUiC_WorkstationWindowGroup{}
class XUiC_WorkstationOutputGrid{}
class XUiC_RecipeStack{internal XUi xui;internal Recipe Recipe=new Recipe();internal int Count=2,AmountToRepair,StartingEntityId=8,NativeHelperCalls;internal bool isInventoryFull=true,FailQuest;internal ItemValue outputItemValue,OriginalItem;internal Recipe GetRecipe()=>Recipe;internal int GetRecipeCount()=>Count;internal T GetParentByType<T>() where T:class=>null;internal bool AddItemToInventory(){NativeHelperCalls++;return RebirthRecipeOutputIntegration.NativeGridHelperAdapter(this);}}
class RecipeQueueItem{internal Recipe Recipe;}
static class RebirthRemoteResourcePlacedStationQueue{internal static Recipe SavedRecipe;internal static string Encode(object[] rows)=>"exact-card";internal static bool TryDecode(string image,out RecipeQueueItem[] rows){rows=new[]{new RecipeQueueItem{Recipe=SavedRecipe}};return image=="exact-card";}}
static class RebirthRemoteResourceClientOriginal{internal static string QueueImage(string data)=>data;}
static class RebirthRemoteResourcePlacedStationSavedReceipt{internal static int Prefix=1;internal static bool TryRead(byte[] data,string key,out float prefix,out bool present){prefix=Prefix;present=true;return true;}}
static class RebirthWorkstationCompletionPublication{internal static void RequireNativeSaveFile(string path){}}
static class RebirthProductiveEarnedObservationBudget{private sealed class Scope:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long bytes,out IDisposable memory){memory=new Scope();return true;}internal static IDisposable TryEnterDecoder()=>new Scope();}
static class RebirthRecipeOutputInventoryPrediction{internal static bool Fits=true;internal static bool TryPlan(EntityPlayerLocal actor,ItemStack[] outputs,out ItemStack[] bag,out ItemStack[] belt,out ItemValue[] equipment){bag=belt=null;equipment=null;return Fits;}}
// ACTUAL_COLD
public static partial class RebirthRecipeOutputIntegration
{
    // Independent remote final callback owner source is covered by its own
    // actual-member cohort; this composed fixture covers personal native load.
    private static void RegisterRemoteCallbackOwner(UiOutputOriginal original,byte[] returned){}
 private sealed class UiScope{internal XUiC_RecipeStack Card;internal IReadOnlyList<ItemStack> Outputs;internal bool Cached,CachedContinuation,CachedPrimaryConsumed;internal XUiC_WorkstationOutputGrid Grid;internal UiOutputOriginal Original;}
 private static UiScope ui;
 private sealed class UiOutputLoadedOwner{internal EntityPlayerLocal Actor;internal World World;internal string Save,Owner="owner",Creation="creation",Path="native-file",Queue="before-queue";internal int Deaths=2;internal byte[] Native=new byte[]{1,2,3,4,5,6},Bag,Belt,Equipment,Buffs=new byte[1];internal Guid[] Operations;internal bool Live=true;internal bool Current()=>Live;}
 private sealed class UiOutputPending{internal Guid Operation;internal UiOutputOriginal Source;internal bool NativeContinuationEntered,NativeCallbackEntered,NativeCosmeticEntered,Returned,QueueObserved;}
 private sealed class UiOutputOriginal:IDisposable
 {
  internal readonly Guid Operation;internal readonly UiScope Scope;internal readonly EntityPlayerLocal Actor;internal readonly World World;private readonly object NativeUi,NativeWindow,NativeStation,NativeGrid;
  internal readonly Recipe Recipe;internal readonly ItemValue Primary;internal readonly int Deaths,Count,PersonalQueueIndex;internal readonly long NativeVersionBefore;internal readonly string Creation,Save,WorldId,Owner,Path,RecipeImage,PersonalQueueBefore;internal readonly byte[] Original,OwnerBeforeBag,OwnerBeforeBelt,OwnerBeforeEquipment;
  internal byte[] AcceptedBag,AcceptedBelt,AcceptedEquipment,CallbackEquipmentAfter;internal bool Entered,Returned,OutputEntered;internal int OutputPrefix,OutputAttempt;internal IDisposable Memory;
  // ACTUAL_CONSTRUCTOR
  internal bool Current()=>ReferenceEquals(ui,Scope)&&ReferenceEquals(Scope.Card.GetRecipe(),Recipe)&&Scope.Card.Count==Count&&ReferenceEquals(Scope.Card.outputItemValue,Primary);
  internal bool GridCallbackFence,RemoteCallbackFence,GridPhysical=true,GridCurrent=true;
  internal bool HasStationGrid=>Scope.Grid!=null&&(GridCallbackFence||Scope.Cached);
  internal bool CallbackPhysicalComplete=>HasStationGrid?GridPhysical:OutputPrefix==Scope.Outputs.Count+1;
  internal int CallbackPrefix=>HasStationGrid?Scope.Outputs.Count+1:OutputPrefix;
  internal bool CallbackOwnerCurrent()=>HasStationGrid?GridCurrent:AcceptedOwnerCurrent();
  internal bool AcceptedOwnerCurrent()=>true;
  public void Dispose(){Memory?.Dispose();}
 }
 private static Dictionary<XUiC_RecipeStack,UiOutputPending> pendingUiOutputs=new Dictionary<XUiC_RecipeStack,UiOutputPending>();
 private static bool HasPendingUiOutput(XUiC_RecipeStack card)=>pendingUiOutputs.ContainsKey(card);
 private static void RetainUiOutput(UiOutputOriginal original)=>pendingUiOutputs.Add(original.Scope.Card,new UiOutputPending{Operation=original.Operation,Source=original});
 private static bool BindPersonalOutputRecoverySlot(EntityPlayerLocal actor,Guid operation,int slot)=>slot==0;
 private static bool CanInsertOutputs(EntityPlayerLocal actor)=>true;
 private static bool IsUiOutputPersonalCard(UiOutputOriginal source)=>!source.HasStationGrid;
 // Owning native file observers are independently composed by save cohorts.
 private static void RegisterUiOutputOwnerPrefix(UiOutputOriginal source,int index,byte[] bag,byte[] belt,byte[] equipment,string accepted){}
 private static void SaveUiOutputPersonalPrefix(UiOutputOriginal source,int index){}
 private static byte[] OwningGrid(ItemStack[] rows)=>RebirthCamperInventorySnapshot.Capture(rows);
 private static byte[] OwningEquipmentNative(EntityPlayerLocal actor)=>Frame(actor.equipment.State);
 private static object ActualCardRow(XUiC_RecipeStack card)=>card;
 private static string PersonalOutputReceipt(Guid operation)=>"marker"+operation;
 // ACTUAL_READ
 // ACTUAL_PUBLICATION
 // ACTUAL_PRIMARY_ACK
 internal static bool NativeGridHelperAdapter(XUiC_RecipeStack card)
 {
  var source=ui.Original;bool accepted=false;ConsumeUiOutputCachedPrimary(card.xui.PlayerInventory,new ItemStack(source.Primary,source.Recipe.count),ref accepted);
  if(!accepted)return false;
  if(RedirectPersonalOutputScrap(new QuestEventManager(),new ItemStack(source.Primary,source.Recipe.count),true))return true;
  var state=EnterPersonalOutputCraftedItem(new ItemStack(source.Primary,source.Recipe.count),true);ObservePersonalOutputCraftedItem(state,true,card.FailQuest?new Exception("native quest callback"):null);return !card.FailQuest;
 }
 static int checks;static string root;static Guid operation;static UiOutputLoadedOwner loaded;static XUiC_RecipeStack card;static string path,hash;static byte[] originalBytes;
 static void Need(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static byte[] Frame(int count,string rich="item")=>RebirthCamperInventorySnapshot.Capture(new[]{new ItemStack(new ItemValue{Rich=rich},count)});
 static XElement Cell(string name,byte[] bytes)=>new XElement(name,Convert.ToBase64String(bytes));
 static void Write(string suffix,XElement image)=>File.WriteAllBytes(path+suffix,UiOriginalBytes(image));
 static XElement Stage(string name,params object[] body)=>new XElement(name,new XAttribute("operation",operation.ToString("N")),new XAttribute("original",hash),body);
 static void Fresh(bool callbackFence=true)
 {
  pendingUiOutputs.Clear();ui=null;RebirthRecipeOutputInventoryPrediction.Fits=true;operation=Guid.NewGuid();var actor=new EntityPlayerLocal();var xui=actor.PlayerUI.xui;card=new XUiC_RecipeStack{xui=xui};xui.Queue.Cards=new[]{card};
  RebirthRemoteResourcePlacedStationQueue.SavedRecipe=card.Recipe;loaded=new UiOutputLoadedOwner{Actor=actor,World=actor.world,Save=root,Operations=new[]{operation},Bag=Frame(11),Belt=Frame(3),Equipment=Frame(0)};actor.Buffs.Values[PersonalOutputReceipt(operation)]=1;RebirthRemoteResourcePlacedStationSavedReceipt.Prefix=1;
  path=Path.Combine(root,"rebirth","recipe-outputs","ui",operation.ToString("N")+".xml");Directory.CreateDirectory(Path.GetDirectoryName(path));
  var image=new XElement("recipeOutputOriginal",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("world","world"),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("deaths",2),new XAttribute("entity",8),new XAttribute("beforeCount",2),new XAttribute("startingOwner",8),new XAttribute("cached",false),new XAttribute("nativeVersion",9),new XAttribute("sink","Inventory"),new XAttribute("personalQueueIndex",0),new XElement("recipe","exact-card"),new XElement("primaryBeforeEffects","1:raw"),Cell("outputs",new[]{new ItemStack(new ItemValue{Rich="raw"},1),new ItemStack(new ItemValue{Rich="extra"},1)}),Cell("bagBefore",Frame(10)),Cell("beltBefore",loaded.Belt),Cell("equipmentBefore",loaded.Equipment),new XElement("personalQueueBefore","before-queue"));
  if(callbackFence)image.Add(new XAttribute("callbackFence",1));originalBytes=UiOriginalBytes(image);hash=UiOriginalHash(originalBytes);File.WriteAllBytes(path,originalBytes);
  Write(".effect-entered",Stage("recipeOutputEffectEntered",new XAttribute("effect","giveExp")));Write(".effect-observed",Stage("recipeOutputEffectObserved",new XAttribute("nativeRan",true),new XElement("primaryAfterEffects","1:decorated")));
  Write(".output-0.entered",Stage("recipeOutputEntered",new XAttribute("index",0),new XAttribute("attempt",0),new XAttribute("equipmentCallbacks",false),Cell("incoming",Frame(1,"decorated")),Cell("bag",Frame(10)),Cell("belt",loaded.Belt),Cell("equipment",loaded.Equipment),Cell("expectedBag",loaded.Bag),Cell("expectedBelt",loaded.Belt),Cell("expectedEquipmentItems",loaded.Equipment)));
  var accepted=Stage("recipeOutputAccepted",new XAttribute("index",0),new XAttribute("attempt",0),Cell("bag",loaded.Bag),Cell("belt",loaded.Belt),Cell("equipment",loaded.Equipment));Write(".output-0.accepted",accepted);
  Write(".output-0.owner-saved",Stage("recipeOutputOwnerPrefixSaved",new XAttribute("index",0),new XAttribute("accepted",UiOriginalHash(UiOriginalBytes(accepted))),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("deaths",2),new XAttribute("entity",8),new XAttribute("playerFile","native-file"),new XAttribute("nativeDigest",UiOriginalHash(loaded.Native)),new XAttribute("nativeLength",6),new XAttribute("personalPrefix",1)));
 }
 static XElement Cell(string name,ItemStack[] items)=>Cell(name,RebirthCamperInventorySnapshot.Capture(items));
 static void CompletePrefix()
 {
  byte[] before=loaded.Bag;loaded.Bag=Frame(12);loaded.Actor.Buffs.Values[PersonalOutputReceipt(operation)]=2;RebirthRemoteResourcePlacedStationSavedReceipt.Prefix=2;
  Write(".output-1.entered",Stage("recipeOutputEntered",new XAttribute("index",1),new XAttribute("attempt",0),new XAttribute("equipmentCallbacks",false),Cell("incoming",Frame(1,"extra")),Cell("bag",before),Cell("belt",loaded.Belt),Cell("equipment",loaded.Equipment),Cell("expectedBag",loaded.Bag),Cell("expectedBelt",loaded.Belt),Cell("expectedEquipmentItems",loaded.Equipment)));
  var accepted=Stage("recipeOutputAccepted",new XAttribute("index",1),new XAttribute("attempt",0),Cell("bag",loaded.Bag),Cell("belt",loaded.Belt),Cell("equipment",loaded.Equipment));Write(".output-1.accepted",accepted);
  Write(".output-1.owner-saved",Stage("recipeOutputOwnerPrefixSaved",new XAttribute("index",1),new XAttribute("accepted",UiOriginalHash(UiOriginalBytes(accepted))),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("deaths",2),new XAttribute("entity",8),new XAttribute("playerFile","native-file"),new XAttribute("nativeDigest",UiOriginalHash(loaded.Native)),new XAttribute("nativeLength",6),new XAttribute("personalPrefix",2)));
 }
 public static void Main(string[] args)
 {
  root=args[0];Fresh();Need(TryAdoptPersonalOutputCold(loaded,operation),"actual saved prefix did not bind restored native card");var original=pendingUiOutputs[card].Source;Need(original.Operation==operation&&original.Original.SequenceEqual(originalBytes)&&original.OutputPrefix==1&&original.Primary.Rich=="decorated"&&original.Scope.Outputs.Single().itemValue.Rich=="extra","cold adoption regenerated or changed immutable outputs");Need(!card.isInventoryFull&&ReferenceEquals(card.outputItemValue,original.Primary),"exact saved primary was not bound before intercepted native continuation");Need(!TryAdoptPersonalOutputCold(loaded,operation),"same card adopted a second original");
  Need(CanAdvancePendingPersonalOutput(card),"qualified remaining original could not enter native Update");RebirthRecipeOutputInventoryPrediction.Fits=false;Need(!CanAdvancePendingPersonalOutput(card),"withheld capacity allowed native Update to alter original timer/row");
  Fresh();loaded.Live=false;Need(!TryAdoptPersonalOutputCold(loaded,operation),"unqualified native Load granted cold adoption");
  Fresh();loaded.Queue="unrestored";Need(!TryAdoptPersonalOutputCold(loaded,operation),"async queue not restored allowed adoption");
  Fresh();card.Count=3;Need(!TryAdoptPersonalOutputCold(loaded,operation),"different native card count adopted");
  Fresh();loaded.Bag=Frame(12);Need(!TryAdoptPersonalOutputCold(loaded,operation),"third native owner image adopted");
  Fresh();File.Delete(path+".output-0.owner-saved");Need(!TryAdoptPersonalOutputCold(loaded,operation),"accepted runtime prefix substituted for saved owner prefix");
  Fresh();File.WriteAllText(path+".output-1.entered","unknown");Need(!TryAdoptPersonalOutputCold(loaded,operation),"entered next output replayed");
  Fresh();Write(".output-1.entered",Stage("recipeOutputEntered",new XAttribute("index",1),new XAttribute("attempt",0),new XAttribute("equipmentCallbacks",false),Cell("incoming",Frame(1,"extra")),Cell("bag",loaded.Bag),Cell("belt",loaded.Belt),Cell("equipment",loaded.Equipment),Cell("expectedBag",Frame(12)),Cell("expectedBelt",loaded.Belt),Cell("expectedEquipmentItems",loaded.Equipment)));Write(".output-1.entered.refused",Stage("recipeOutputRefused",new XAttribute("index",1),new XAttribute("attempt",0),Cell("bag",loaded.Bag),Cell("belt",loaded.Belt),Cell("equipment",loaded.Equipment)));Need(TryAdoptPersonalOutputCold(loaded,operation)&&pendingUiOutputs[card].Source.OutputAttempt==1,"positive exact no-effect attempt did not retain next create-only attempt");
  Fresh();Write(".native-continuation-entered",Stage("recipeOutputNativeContinuationEntered",new XAttribute("outputPrefix",2)));Need(!TryAdoptPersonalOutputCold(loaded,operation),"unknown entered native callback replayed");
  Fresh();File.WriteAllText(path+".tmp","foreign");Need(!TryAdoptPersonalOutputCold(loaded,operation),"staged original adopted");
  Fresh();var saved=XElement.Load(path+".output-0.owner-saved");saved.SetAttributeValue("accepted","foreign");Write(".output-0.owner-saved",saved);Need(!TryAdoptPersonalOutputCold(loaded,operation),"foreign accepted prefix binding adopted");
  Fresh();CompletePrefix();Need(TryAdoptPersonalOutputCold(loaded,operation)&&pendingUiOutputs[card].Source.OutputPrefix==2&&!pendingUiOutputs[card].Returned,"complete saved physical prefix could not retain pending native callback");
  original=pendingUiOutputs[card].Source;ui=original.Scope;var callback=EnterPersonalOutputCraftedItem(new ItemStack(original.Primary,1),true);Need(callback!=null&&pendingUiOutputs[card].NativeContinuationEntered&&!pendingUiOutputs[card].Returned,"actual native callback did not enter its immutable fence");bool reentered=false;try{EnterPersonalOutputCraftedItem(new ItemStack(original.Primary,1),true);}catch(InvalidOperationException){reentered=true;}Need(reentered,"same native callback entered twice");ObservePersonalOutputCraftedItem(callback,true,null);ui=null;Need(CompleteUiOutputReturnedNative(card),"positive actual callback return did not permit queue-only handoff");
  Fresh();CompletePrefix();File.Delete(path+".output-0.owner-saved");Need(TryAdoptPersonalOutputCold(loaded,operation),"later complete saved native prefix required a superseded earlier PDS save observer");
  Fresh(false);CompletePrefix();Need(!TryAdoptPersonalOutputCold(loaded,operation),"legacy complete physical prefix inferred an unentered callback from absent old fence");
  Fresh();CompletePrefix();card.Recipe.IsScrap=true;card.Recipe.ingredients.Add(new ItemStack(new ItemValue{Rich="scrap-input"},4));Need(TryAdoptPersonalOutputCold(loaded,operation),"saved scrap original did not bind exact restored card");original=pendingUiOutputs[card].Source;ui=original.Scope;ui.CachedContinuation=ui.CachedPrimaryConsumed=true;pendingUiOutputs[card].NativeContinuationEntered=true;var events=new QuestEventManager();Need(RedirectPersonalOutputScrap(events,new ItemStack(original.Primary,1),true)&&events.ScrapCalls==1&&ReferenceEquals(events.LastScrap,card.Recipe.ingredients[0])&&loaded.Actor.equipment.UnlockCalls==1,"private native helper did not preserve actual scrap ingredient and cosmetic callbacks");ui.CachedContinuation=false;Need(!RedirectPersonalOutputScrap(events,new ItemStack(original.Primary,1),true)&&events.ScrapCalls==1,"unrelated native CraftedItem was redirected");ui=null;Need(CompleteUiOutputReturnedNative(card)&&original.AcceptedEquipment.SequenceEqual(Frame(1)),"positive full native scrap callback did not permit queue-only handoff");pendingUiOutputs.Clear();original.Dispose();loaded.Equipment=Frame(1);loaded.Native=new byte[]{1,2,3,4,5,8};Need(TryAdoptPersonalOutputCold(loaded,operation)&&CompleteUiOutputReturnedNative(card),"positive saved cosmetic After was not bound through exact cold callback proof");
  Fresh();CompletePrefix();card.Recipe.IsScrap=true;card.Recipe.ingredients.Add(new ItemStack(new ItemValue{Rich="scrap-input"},4));Need(TryAdoptPersonalOutputCold(loaded,operation),"unknown cosmetic setup");original=pendingUiOutputs[card].Source;ui=original.Scope;ui.CachedContinuation=ui.CachedPrimaryConsumed=true;pendingUiOutputs[card].NativeContinuationEntered=true;loaded.Actor.equipment.FailUnlock=true;bool cosmeticError=false;try{RedirectPersonalOutputScrap(new QuestEventManager(),new ItemStack(original.Primary,1),true);}catch(Exception){cosmeticError=true;}ui=null;Need(cosmeticError&&!pendingUiOutputs[card].Returned&&File.Exists(path+".scrap-quest-returned")&&!File.Exists(path+".native-continuation-returned")&&!CompleteUiOutputReturnedNative(card),"quest return alone completed an unknown cosmetic unlock");
  Fresh();CompletePrefix();Need(TryAdoptPersonalOutputCold(loaded,operation),"unknown callback setup");original=pendingUiOutputs[card].Source;ui=original.Scope;callback=EnterPersonalOutputCraftedItem(new ItemStack(original.Primary,1),true);ObservePersonalOutputCraftedItem(callback,true,new InvalidOperationException("native callback"));ui=null;Need(!CompleteUiOutputReturnedNative(card),"unknown native callback acquired positive returned proof");
  Fresh();CompletePrefix();Write(".native-continuation-entered",Stage("recipeOutputNativeContinuationEntered",new XAttribute("outputPrefix",2)));Write(".native-continuation-returned",Stage("recipeOutputNativeContinuationReturned",new XAttribute("outputPrefix",2)));Need(TryAdoptPersonalOutputCold(loaded,operation)&&CompleteUiOutputReturnedNative(card),"positive returned native callback did not permit queue-only continuation");
  Fresh();CompletePrefix();loaded.Queue=loaded.Actor.PlayerUI.xui.NativeQueue="after-queue";Write(".queue-observed",Stage("recipeOutputQueueObserved",new XAttribute("beforeCount",2),new XAttribute("afterCount",1),new XElement("personalQueueAfter","after-queue")));Need(TryCompletePersonalOutputLoadedQueue(loaded,operation)&&File.Exists(path+".queue-owner-saved"),"actual loaded complete owner/observed queue did not publish missing saved terminal metadata");
  var terminal=XElement.Load(path+".queue-owner-saved");Need((string)terminal.Attribute("nativeDigest")==UiOriginalHash(loaded.Native)&&(int)terminal.Attribute("outputPrefix")==2,"loaded queue terminal changed original saved owner binding");
  Fresh();CompletePrefix();loaded.Queue=loaded.Actor.PlayerUI.xui.NativeQueue="after-queue";Write(".queue-observed",Stage("recipeOutputQueueObserved",new XAttribute("beforeCount",2),new XAttribute("afterCount",1),new XElement("personalQueueAfter","foreign-queue")));Need(!TryCompletePersonalOutputLoadedQueue(loaded,operation),"different full saved queue inferred terminal");
  Fresh();CompletePrefix();Need(TryAdoptPersonalOutputCold(loaded,operation),"grid callback setup");original=pendingUiOutputs[card].Source;original.GridCallbackFence=true;original.Scope.Grid=new XUiC_WorkstationOutputGrid();original.GridPhysical=false;Need(!CanAdvancePendingStationOutput(card)&&card.NativeHelperCalls==0,"unknown grid setter replayed");original.GridPhysical=true;Need(CanAdvancePendingStationOutput(card)&&card.NativeHelperCalls==1&&pendingUiOutputs[card].Returned,"accepted combined grid did not finish unentered native callback");Need(CanAdvancePendingStationOutput(card)&&card.NativeHelperCalls==1&&CompleteUiOutputReturnedNative(card),"returned grid callback repeated or blocked queue-only native handoff");
  Fresh();CompletePrefix();Need(TryAdoptPersonalOutputCold(loaded,operation),"grid unknown callback setup");original=pendingUiOutputs[card].Source;original.GridCallbackFence=true;original.Scope.Grid=new XUiC_WorkstationOutputGrid();card.FailQuest=true;Need(!CanAdvancePendingStationOutput(card)&&pendingUiOutputs[card].NativeContinuationEntered&&!pendingUiOutputs[card].Returned,"failed native grid callback became returned");card.FailQuest=false;Need(!CanAdvancePendingStationOutput(card)&&card.NativeHelperCalls==1,"entered unknown grid callback replayed");
  Fresh();CompletePrefix();Need(TryAdoptPersonalOutputCold(loaded,operation),"cached grid setup");original=pendingUiOutputs[card].Source;original.Scope.Cached=true;original.Scope.Grid=new XUiC_WorkstationOutputGrid();card.isInventoryFull=true;Need(CanAdvancePendingStationOutput(card)&&!card.isInventoryFull&&card.NativeHelperCalls==0&&CompleteUiOutputReturnedNative(card),"accepted native cached grid did not hand queue bookkeeping back without an extra callback");Need(CanAdvancePendingStationOutput(card)&&card.NativeHelperCalls==0,"cached grid repeated helper quest/sound");
  Console.WriteLine("PASS "+checks+" actual personal cold admission/binding assertions; native Load/UI/codec/scope remain explicit adapters.");
 }
}
