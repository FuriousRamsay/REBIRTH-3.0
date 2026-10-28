// AUTHORED_NOT_RUN. Native actor/PDS/codec/budget adapters are explicit.
class ItemStack{internal byte Value;}
class Recipe{}
class CraftingData{internal string Queue="after";}
class XUiC_WorkstationWindowGroup{}
class XUiC_RecipeStack{
 internal OwnerXui xui=new OwnerXui();internal int Count;internal Recipe CurrentRecipe;internal object Window;
 internal T GetParentByType<T>() where T:class=>Window as T;
 internal int GetRecipeCount()=>Count;internal Recipe GetRecipe()=>CurrentRecipe;
}
class XUiC_CraftingQueue{internal XUiC_RecipeStack[] Cards;internal XUiC_RecipeStack[] GetRecipesToCraft()=>Cards;}
class OwnerWindow{internal XUiC_CraftingQueue Queue;internal T GetChildByType<T>() where T:class=>Queue as T;}
class OwnerXui{internal OwnerWindow Group;internal OwnerPlayerUi playerUI;internal CraftingData Crafting=new CraftingData();internal OwnerWindow FindWindowGroupByName(string name)=>Group;internal CraftingData GetCraftingData()=>Crafting;}
class OwnerPlayerUi{internal OwnerXui xui;internal EntityPlayerLocal entityPlayer;}
static class RebirthRemoteResourceClientOriginal{internal static string QueueImage(CraftingData data)=>data.Queue;}
class World{internal string Guid="fixture-world";internal bool Remote;internal EntityPlayerLocal Actor;internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Actor;internal EntityPlayer GetEntity(int id)=>Actor;}
static class ModEvents{internal struct SGameUpdateData{}}
class OwnerBuffs{internal readonly Dictionary<string,float> Values=new Dictionary<string,float>();internal void SetCustomVar(string key,float value,bool sync){Values[key]=value;}internal float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0;internal void RemoveCustomVar(string key){Values.Remove(key);}}
class OwnerGrid{internal ItemStack[] items;}
class OwnerBag{internal OwnerGrid ItemGrid=new OwnerGrid{items=new[]{new ItemStack{Value=1}}};}
class OwnerInventory{internal OwnerGrid ItemGrid=new OwnerGrid{items=new[]{new ItemStack{Value=2}}};}
class EntityPlayer{internal World world;internal int entityId=1,Died=2;internal bool Dead;internal string Creation="creation";internal OwnerPlayerUi PlayerUI;internal OwnerBuffs Buffs=new OwnerBuffs();internal OwnerBag bag=new OwnerBag();internal OwnerInventory inventory=new OwnerInventory();internal byte[] NativeEquipment=new byte[]{3};internal bool IsDead()=>Dead;internal bool IsSpawned()=>true;}
class EntityPlayerLocal:EntityPlayer{}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal int SaveRequests;internal void SaveLocalPlayerData(){SaveRequests++;}}
namespace UnityEngine{static class Time{internal static float realtimeSinceStartup;}}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class GameIO{internal static string Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"native-player");internal static string GetPlayerDataDir()=>Root;internal static string GetSaveGameDir()=>Root;}
class RebirthStablePlayerIdentity{internal string StorageKey="owner",CanonicalId="account";internal static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer player)=>player.Creation;}
class PlayerDataFile{internal const string EXT="ttp";internal const byte cFileVersion=7;internal int id=1,deaths=2;internal bool bLoaded=true,bDead;internal float NativePrefix=1;internal MemoryStream buffData=new MemoryStream();internal CraftingData craftingData=new CraftingData();internal byte[] equipmentData=new byte[]{3},Native=new byte[]{7,8,9};internal ItemStack[] Bag=new[]{new ItemStack{Value=1}},Belt=new[]{new ItemStack{Value=2}};}
 // EXCLUDED_RECEIPT_BEGIN
// Explicit native marker decode adapter. Production uses its existing exact
// EntityBuffs framing reader; native byte serialization is not claimed here.
static class RebirthRemoteResourcePlacedStationSavedReceipt{internal static bool TryRead(PlayerDataFile data,string key,out float value,out bool present){value=data.NativePrefix;present=value!=0;return true;}}
 // EXCLUDED_RECEIPT_END
static class RebirthGearPlayerDataInventory{internal static ItemStack[] ReadSlots(PlayerDataFile data,bool bag)=>bag?data.Bag:data.Belt;}
static class RebirthProductiveEarnedObservationBudget{
 internal sealed class Permit:IDisposable{internal bool Disposed;public void Dispose(){Disposed=true;}}
 internal static bool TryRetainDetached(long bytes,out IDisposable memory){memory=new Permit();return true;}
 internal static IDisposable TryEnterDecoder()=>new Permit();
}
static class RebirthProductiveEarnedUiOwnerFile{
 internal static byte[] DeferredNativeOwnerImage(PlayerDataFile data)=>data.Native.ToArray();
 internal static bool DeferredNativeOwnerFileMatches(string path,byte[] native)=>File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(native);
 internal static bool DeferredNativeOwnerPathSafe(string path)=>Path.GetFullPath(path).StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),StringComparison.OrdinalIgnoreCase);
}
static class RebirthWorkstationCompletionPublication{
 internal static void RequireNativeSaveFile(string path){if(!Path.GetFullPath(path).StartsWith(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("fixture path escaped temporary root");}
}
public static partial class RebirthRecipeOutputIntegration{
 // Distinct final quest/cosmetic owner PDS witness belongs to the remote
 // callback cohort; this fixture covers insertion-prefix saved evidence only.
 private static void ObserveRemoteOutputCallbackOwnerSaved(RebirthWorkstationCompletionPipeline.AdditionalOutputOwnerSaved saved){}
 // EXCLUDED_NATIVE_LOAD_BEGIN
 // Full native load lineage is separately composed; no load/adoption evidence
 // is supplied by the saved queue or prefix-only cohorts.
 private static void SweepUiOutputLoadedOwners(World world){}
 private static void PumpPersonalOutputColdTerminals(){}
 // EXCLUDED_NATIVE_LOAD_END
 // Separate station-ready owner purpose is not exercised by personal cohorts.
 private static void ObserveStationOutputBeforeOwnerFromPlayer(PlayerDataFile data,EntityPlayerLocal actor){}
 private static void EnterStationOutputBeforeOwnerSave(PlayerDataFile data,string directory,string name,bool ran,out object state){state=null;}
 private static void ObserveStationOutputBeforeOwnerSave(bool ran,object state){}
 private static int PrepareStationOutputNativeLoads(object owner)=>0;
 private static void DisposeStationOutputNativeLoads(object owner){}
 // EXCLUDED_PERSONAL_QUEUE_BEGIN
 // Personal native queue qualification is separately composed; this prefix
 // cohort deliberately supplies no queue-save evidence or release authority.
 private static void ObserveUiOutputPersonalQueueFromPlayer(PlayerDataFile data,EntityPlayerLocal actor){}
 private static void EnterUiOutputPersonalQueueSave(PlayerDataFile data,string directory,string name,bool ran,out object state){state=null;}
 private static void ObserveUiOutputPersonalQueueSave(bool ran,object state){}
 private static bool IsUiOutputPersonalCard(UiOutputOriginal source)=>false;
 private static bool HasUiOutputPersonalPrefix(PlayerDataFile data,Guid operation,int prefix)=>false;
 // EXCLUDED_PERSONAL_QUEUE_END
 internal sealed class OwningObservation{
  internal World World;internal EntityPlayerLocal Viewer;internal XUiC_RecipeStack Card;internal Recipe Recipe;
  internal Guid Operation=Guid.NewGuid();internal int ViewerDeaths=2,BeforeCount=1;internal long NativeVersionBefore;
  internal string ViewerCreation="creation",ViewerOwnerKey="owner";internal byte[] BagBefore=new byte[]{1},BeltBefore=new byte[]{2},EquipmentBefore=new byte[]{3};
 }
 private sealed class UiScope{internal XUiC_RecipeStack Card=new XUiC_RecipeStack();internal object Grid;internal IReadOnlyList<ItemStack> Outputs=new ItemStack[0];}
 private sealed class UiOutputOriginal{
  internal World World;internal EntityPlayerLocal Actor;internal Guid Operation=Guid.NewGuid();internal int Deaths=2;
  internal string Owner="owner",Creation="creation",Path;internal byte[] Original=new byte[]{4,5};internal bool Live=true;
  internal UiScope Scope=new UiScope();internal Recipe Recipe=new Recipe();internal int Count=1;internal long NativeVersionBefore;
  internal byte[] OwnerBeforeBag=new byte[]{1},OwnerBeforeBelt=new byte[]{2},OwnerBeforeEquipment=new byte[]{3};
  internal string Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());internal bool OutputEntered,OwnerCurrent=true,Disposed;internal int OutputPrefix=1;
  internal byte[] AcceptedBag=new byte[]{1},AcceptedBelt=new byte[]{2},AcceptedEquipment=new byte[]{3};
  // Explicit native live-owner comparison adapter, not saved-file authority.
  internal bool AcceptedOwnerCurrent()=>OwnerCurrent;
  public void Dispose(){Disposed=true;}
  internal bool Current()=>Live;
 }
 private static byte[] OwningGrid(ItemStack[] cells)=>cells.Select(cell=>cell.Value).ToArray();
 // Native equipment serialization adapter for the load composition.
 private static byte[] OwningEquipmentNative(EntityPlayerLocal actor)=>actor.NativeEquipment.ToArray();
 // ACTUAL_PUBLICATION
 private static void Need(bool value,string text){if(!value)throw new Exception(text);}
 private static UiOutputOriginal Fresh(out PlayerDataFile data){
  var world=new World();var actor=new EntityPlayerLocal{world=world};world.Actor=actor;GameManager.Instance.World=world;
  var original=new UiOutputOriginal{World=world,Actor=actor,Path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"originals",Guid.NewGuid().ToString("N")+".xml")};
  actor.PlayerUI=new OwnerPlayerUi{xui=original.Scope.Card.xui,entityPlayer=actor};original.Scope.Card.xui.playerUI=actor.PlayerUI;original.Scope.Card.xui.Group=new OwnerWindow{Queue=new XUiC_CraftingQueue{Cards=new[]{original.Scope.Card}}};
  data=new PlayerDataFile();RegisterUiOutputOwnerPrefix(original,0,new byte[]{1},new byte[]{2},new byte[]{3},"accepted-hash");return original;
 }
 private static string Proof(UiOutputOriginal original)=>original.Path+".output-0.owner-saved";
 private static object Capture(UiOutputOriginal original,PlayerDataFile data,bool ran=true){ObserveRecipeOutputOwnerFromPlayer(data,original.Actor,ran);EnterRecipeOutputOwnerSave(data,GameIO.Root,"account",true,out var state);return state;}
 private static void WriteNative(PlayerDataFile data){Directory.CreateDirectory(GameIO.Root);File.WriteAllBytes(System.IO.Path.Combine(GameIO.Root,"account.ttp"),data.Native);}
 public static void Main(){
  var original=Fresh(out var data);var state=Capture(original,data);Need(state!=null,"exact native capture did not retain save observation");WriteNative(data);ObserveRecipeOutputOwnerSave(true,state);
  Need(File.Exists(Proof(original)),"actual matching completed native save did not publish prefix");
  var proof=XElement.Load(Proof(original));Need((string)proof.Attribute("original")==UiOriginalHash(original.Original)&&(string)proof.Attribute("accepted")=="accepted-hash"&&(string)proof.Attribute("nativeDigest")==UiOriginalHash(data.Native),"saved prefix lost original/accepted/full-native binding");
  original=Fresh(out data);data.equipmentData=new byte[]{99};Need(Capture(original,data)==null&&!File.Exists(Proof(original)),"different native equipment qualified accepted prefix");
  original=Fresh(out data);data.Bag[0].Value=99;Need(Capture(original,data)==null,"different native bag qualified prefix");
  original=Fresh(out data);data.Belt[0].Value=99;Need(Capture(original,data)==null,"different native belt qualified prefix");
  original=Fresh(out data);Need(Capture(original,data,false)==null,"skipped native FromPlayer created save token");
  original=Fresh(out data);ObserveRecipeOutputOwnerFromPlayer(data,original.Actor,true);data.Native[0]=42;EnterRecipeOutputOwnerSave(data,GameIO.Root,"account",true,out state);Need(state==null,"changed full PDS after capture qualified save entry");
  original=Fresh(out data);ObserveRecipeOutputOwnerFromPlayer(data,original.Actor,true);EnterRecipeOutputOwnerSave(data,GameIO.Root,"foreign",true,out state);Need(state==null,"foreign native account filename qualified save");
  original=Fresh(out data);ObserveRecipeOutputOwnerFromPlayer(data,original.Actor,true);EnterRecipeOutputOwnerSave(data,System.IO.Path.Combine(GameIO.Root,"foreign"),"account",true,out state);Need(state==null,"different player directory qualified save");
  original=Fresh(out data);state=Capture(original,data);WriteNative(data);ObserveRecipeOutputOwnerSave(false,state);Need(!File.Exists(Proof(original)),"skipped native Save published prefix");
  original=Fresh(out data);state=Capture(original,data);File.WriteAllBytes(System.IO.Path.Combine(GameIO.Root,"account.ttp"),new byte[]{0});ObserveRecipeOutputOwnerSave(true,state);Need(!File.Exists(Proof(original)),"different final native file published prefix");
  original=Fresh(out data);state=Capture(original,data);WriteNative(data);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Proof(original)));File.WriteAllText(Proof(original)+".tmp","foreign stage");ObserveRecipeOutputOwnerSave(true,state);Need(!File.Exists(Proof(original))&&File.ReadAllText(Proof(original)+".tmp")=="foreign stage","staged proof was replaced or adopted");
  original=Fresh(out data);original.Actor.Died++;Need(Capture(original,data)==null,"changed native life qualified old accepted prefix");
  original=Fresh(out data);original.World.Remote=true;Need(Capture(original,data)==null,"remote native replica qualified local saved prefix");
  foreach(int variant in Enumerable.Range(0,9)){
   original=Fresh(out data);original.World.Remote=true;
   RegisterUiOutputOwnerPrefix(original,1,new byte[]{8},new byte[]{9},new byte[]{10},"remote-accepted");
   var observation=new OwningObservation{World=original.World,Viewer=original.Actor,Card=original.Scope.Card,Recipe=original.Recipe};
   if(variant==1)observation.Card=new XUiC_RecipeStack();
   if(variant==2)observation.NativeVersionBefore++;
   if(variant==3)observation.BagBefore=new byte[]{99};
   var request=new XElement("nativeViewer",new XAttribute("stage","after"),new XAttribute("generation","generation"),new XAttribute("pdsDigest","native-wire"),new XAttribute("pdsLength",3),
    new XElement("nativeOwnerFrame",new XElement("bag",Convert.ToBase64String(new byte[]{8})),new XElement("belt",Convert.ToBase64String(new byte[]{9})),new XElement("equipment",Convert.ToBase64String(new byte[]{(byte)(variant==4?99:10)}))),
    new XElement("physicalDelivery",new XElement("delivery",new XAttribute("sink",variant==5?"Output":"Inventory"))));
   var saved=RebirthWorkstationCompletionPipeline.FixtureSaved(observation,request,variant);
   ObserveUiOutputRemoteOwnerSaved(saved);
   string remoteProof=original.Path+".output-1.owner-saved";
   Need(File.Exists(remoteProof)==(variant==0),"remote saved prefix accepted foreign/unsaved/wrong native source variant "+variant);
  }
  Console.WriteLine("MP028 owner-prefix save: 24 assertions authored; actual observer/token/publication, explicit native producer/serialization/save adapters.");
 }
}
internal static partial class RebirthWorkstationCompletionPipeline{
 private sealed class Frame{internal RebirthRecipeOutputIntegration.OwningObservation Observation;internal Guid RemoteNativeNonce=Guid.NewGuid();internal string RemoteNativeSourceHash="native-source";}
 private sealed class NativeViewerClient{internal Frame Frame;internal ViewerClientStage After;internal bool Current()=>true;}
 private sealed class ViewerClientStage{internal bool Saved=true,Sent=true,Captured=true;internal XElement Request;internal string Digest="request-digest";}
 // Explicit adapter for the already authenticated native Saved producer. The
 // actual capability still checks private stage state and exact ack lineage.
 internal static AdditionalOutputOwnerSaved FixtureSaved(RebirthRecipeOutputIntegration.OwningObservation observation,XElement request,int variant){
  var source=new NativeViewerClient{Frame=new Frame{Observation=observation},After=new ViewerClientStage{Request=request}};
  var ack=new XElement("ack",new XAttribute("stage",variant==8?"nativeViewerAfterArmed":"nativeViewerAfterSaved"),new XAttribute("digest","request-digest"),new XAttribute("nativeNonce",source.Frame.RemoteNativeNonce.ToString("N")),new XAttribute("nativeSource","native-source"));
  if(variant==6)source.After.Saved=false;if(variant==7)ack.SetAttributeValue("nativeNonce",Guid.NewGuid().ToString("N"));
  return AdditionalOutputOwnerSaved.Capture(source,source.After,ack);
 }
}
