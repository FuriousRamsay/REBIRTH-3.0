// AUTHORED_NOT_RUN. Full actual station queue source composed below. Native UI,
// native body/schema, incarnation and snapshot publication are explicit adapters.
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;
class ItemStack{internal int count;internal ItemStack(int n){count=n;}}
class Recipe{}
class RecipeQueueItem{internal int Count;}
class World{internal bool Remote;internal EntityPlayerLocal Actor;internal TileEntityWorkstation Tile;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>Actor;internal object GetTileEntity(object position)=>Tile;}
class EntityPlayerLocal{internal World world;internal int entityId=7,Died=2;internal bool Dead;internal bool IsDead()=>Dead;}
class TileEntityWorkstation{internal ItemStack[] Output={new ItemStack(4)};internal RecipeQueueItem[] Queue={new RecipeQueueItem{Count=1}};internal string Birth="birth";internal object ToWorldPos()=>this;}
class RebirthCamperStationState:TileEntityWorkstation{}
class Data{internal TileEntityWorkstation TileEntity;}
class QueueUi{internal XUiC_RecipeStack[] Cards;internal XUiC_RecipeStack[] GetRecipesToCraft()=>Cards;}
class XUiC_WorkstationWindowGroup{internal Data WorkstationData=new Data();internal QueueUi craftingQueue=new QueueUi();}
class Grid{internal ItemStack[] Cells={new ItemStack(4)};internal ItemStack[] GetSlots()=>Cells;}
class XUiC_RecipeStack{internal XUiC_WorkstationWindowGroup Window;internal Recipe Recipe=new Recipe();internal int Count=1;internal T GetParentByType<T>() where T:class=>Window as T;internal int GetRecipeCount()=>Count;internal Recipe GetRecipe()=>Recipe;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
class PlayerDataFile{internal const string EXT="ttp";}
class RebirthStablePlayerIdentity{internal string StorageKey="owner",CanonicalId="account";internal static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
static class GameIO{internal static string Save;internal static string GetSaveGameDir()=>Save;internal static string GetPlayerDataDir()=>Save;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal actor)=>"creation";}
static class RebirthLogisticsEndpointIdentity{internal static bool TryGetIncarnation(World world,TileEntityWorkstation tile,out string birth){birth=tile.Birth;return true;}}
static class RebirthRemoteResourcePlacedStationQueue{internal static string Encode(RecipeQueueItem[] rows)=>string.Join(",",rows.Select(row=>row.Count));}
static class RemoteResourceIdentity{internal static string Workstation(object position)=>"station";}
enum RemoteResourceSourceKind{WorkstationOutput}
class RebirthRemoteResourceNativeBinding{internal static bool TryResolve(World world,string id,RemoteResourceSourceKind kind,string birth,out RebirthRemoteResourceNativeBinding binding){binding=new RebirthRemoteResourceNativeBinding();return true;}}
static class RebirthProductiveEarnedNativeBody{
 internal static byte[] Image(TileEntityWorkstation tile)=>Encoding.UTF8.GetBytes(tile.Output[0].count+"|"+tile.Queue[0].Count);
 internal static bool TryCaptureCurrent(TileEntityWorkstation tile,out byte[] body,out int offset){body=Image(tile);offset=0;return true;}
 internal static bool MatchesCurrent(TileEntityWorkstation tile,byte[] body,int offset)=>body.SequenceEqual(Image(tile));}
static class RebirthProductiveEarnedObservationBudget{internal sealed class Scope:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long n,out IDisposable memory){memory=new Scope();return true;}}
static class RebirthRemoteResourceNativePublication{
 internal static bool Published;internal sealed class Publication{internal string Path="native-chunk",PayloadDigest="native-payload";}
 internal sealed class Watch:IDisposable{internal Func<bool> Original;internal bool Closed;internal bool Current()=>!Closed&&Original();internal bool TryReadPublishedTileDescriptor(out Publication publication,out long offset,out int length,out string digest){publication=new Publication();offset=8;length=12;digest="exact-span";return Published&&Current();}public void Dispose(){Closed=true;}}
 internal static Watch Begin(RebirthRemoteResourceNativeBinding binding,Func<bool> current)=>new Watch{Original=current};}
// ACTUAL_STATION_QUEUE
public static partial class RebirthRecipeOutputIntegration{
 private class UiScope{internal XUiC_RecipeStack Card;internal Grid Grid=new Grid();}
 private class UiOutputOriginal:IDisposable{internal UiScope Scope;internal World World;internal EntityPlayerLocal Actor;internal string Save,StationBirth="birth",Path,Owner="owner",Creation="creation";internal int Deaths=2;internal int CallbackPrefix=>2;internal Guid Operation=Guid.NewGuid();internal byte[] Original={1,2},GridExpected=Encoding.UTF8.GetBytes("4");internal bool GridCallbackFence,GridAccepted=true,Disposed;public void Dispose(){Disposed=true;}}
 private class UiOutputPending{internal UiOutputOriginal Source;internal EntityPlayerLocal Actor;internal World World;internal int Deaths=2,Count=2;internal string Creation="creation";internal Recipe Recipe;internal bool Returned=true,QueueObserved=true,QueueSaved;}
 private static Dictionary<XUiC_RecipeStack,UiOutputPending> pendingUiOutputs=new Dictionary<XUiC_RecipeStack,UiOutputPending>();
 private static byte[] OwningGrid(ItemStack[] cells)=>Encoding.UTF8.GetBytes(string.Join(",",cells.Select(cell=>cell.count)));
 private static RecipeQueueItem ActualCardRow(XUiC_RecipeStack card)=>new RecipeQueueItem{Count=card.Count};
 private static string UiOriginalHash(byte[] bytes)=>Convert.ToBase64String(bytes);
 private static byte[] UiOriginalBytes(XElement image)=>Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting));
 // Personal sidecar schema is composed by the personal cold cohort.
 private static bool ReadPersonalOutputSidecar(string path,out byte[] bytes,out XElement image){bytes=null;image=null;if(!File.Exists(path))return false;bytes=File.ReadAllBytes(path);image=XElement.Load(path);return true;}
 private static bool PersonalColdIdentity(XElement image,string name,string attributes,string elements,Guid operation,string hash)=>image.Name==name&&(string)image.Attribute("operation")==operation.ToString("N")&&(string)image.Attribute("original")==hash;
 // Create-only sidecar adapter; this cohort asserts the exact actual proof data.
 private static bool PublishPersonalColdBinding(string path,byte[] bytes){if(File.Exists(path))return File.ReadAllBytes(path).SequenceEqual(bytes);File.WriteAllBytes(path,bytes);return true;}
 static UiOutputPending Fresh(string root){foreach(var item in stationOutputQueues)item.Dispose();stationOutputQueues.Clear();pendingUiOutputs.Clear();RebirthRemoteResourceNativePublication.Published=false;
  var world=new World();var actor=new EntityPlayerLocal{world=world};world.Actor=actor;GameManager.Instance.World=world;GameIO.Save=root;
  var tile=new TileEntityWorkstation();world.Tile=tile;var window=new XUiC_WorkstationWindowGroup();window.WorkstationData.TileEntity=tile;var card=new XUiC_RecipeStack{Window=window};window.craftingQueue.Cards=new[]{card};
  var source=new UiOutputOriginal{World=world,Actor=actor,Save=Path.GetFullPath(root),Path=Path.Combine(root,Guid.NewGuid().ToString("N")),Scope=new UiScope{Card=card}};
  var pending=new UiOutputPending{World=world,Actor=actor,Source=source,Recipe=card.Recipe};pendingUiOutputs.Add(card,pending);return pending;}
 static int checks;static void Need(bool value,string label){checks++;if(!value)throw new Exception(label);}
 public static void Main(string[] args){var p=Fresh(args[0]);var source=p.Source;Need(RegisterUiOutputStationQueue(p)&&HoldsUiOutputStationQueue(source.Scope.Card),"actual station producer did not retain whole window");PumpStationOutputQueues();Need(p.Source==source&&!p.QueueSaved&&!File.Exists(source.Path+".station-queue-saved"),"runtime tile postimage substituted for native saved publication");
  RebirthRemoteResourceNativePublication.Published=true;PumpStationOutputQueues();Need(p.QueueSaved&&p.Source==null&&source.Disposed&&!HoldsUiOutputStationQueue(source.Scope.Card),"positive exact native publication did not release applied original");var proof=XElement.Load(source.Path+".station-queue-saved");Need((string)proof.Attribute("original")==UiOriginalHash(source.Original)&&(string)proof.Element("queueAfter")=="1"&&(string)proof.Element("outputAfter")==Convert.ToBase64String(source.GridExpected),"saved terminal lost immutable original/output/full queue");
  p=Fresh(args[0]);source=p.Source;RegisterUiOutputStationQueue(p);var saved=stationOutputQueues.Single();saved.Tile.Queue[0].Count=2;RebirthRemoteResourceNativePublication.Published=true;PumpStationOutputQueues();Need(!p.QueueSaved&&p.Source==source,"unsynchronized native queue minted saved terminal");
  p=Fresh(args[0]);source=p.Source;RegisterUiOutputStationQueue(p);saved=stationOutputQueues.Single();saved.Tile.Birth="replacement";RebirthRemoteResourceNativePublication.Published=true;PumpStationOutputQueues();Need(!p.QueueSaved,"replacement incarnation inherited output original");
  p=Fresh(args[0]);source=p.Source;RegisterUiOutputStationQueue(p);saved=stationOutputQueues.Single();PumpStationOutputQueues();saved.Tile.Output[0].count++;RebirthRemoteResourceNativePublication.Published=true;PumpStationOutputQueues();Need(!p.QueueSaved&&p.Source==source,"different output/native body minted terminal");
  p=Fresh(args[0]);source=p.Source;source.World.Remote=true;Need(!RegisterUiOutputStationQueue(p),"remote client borrowed host native tile publication");
  p=Fresh(args[0]);source=p.Source;RegisterUiOutputStationQueue(p);p.Actor.Died++;RebirthRemoteResourceNativePublication.Published=true;PumpStationOutputQueues();Need(!p.QueueSaved,"different life adopted current station original");
  p=Fresh(args[0]);source=p.Source;source.GridCallbackFence=true;RegisterUiOutputStationQueue(p);RebirthRemoteResourceNativePublication.Published=true;PumpStationOutputQueues();Need(!p.QueueSaved,"saved tile alone completed an unsaved native callback owner");var callback=new XElement("recipeOutputNativeContinuationReturned",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("original",UiOriginalHash(source.Original)),new XAttribute("outputPrefix",2));var callbackBytes=UiOriginalBytes(callback);File.WriteAllBytes(source.Path+".native-continuation-returned",callbackBytes);var owner=new XElement("recipeOutputOwnerPrefixSaved",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("original",UiOriginalHash(source.Original)),new XAttribute("index",1),new XAttribute("accepted","foreign"),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("deaths",2),new XAttribute("entity",7),new XAttribute("playerFile",Path.Combine(GameIO.GetPlayerDataDir(),"account.ttp")),new XAttribute("nativeDigest",Convert.ToBase64String(new byte[32])),new XAttribute("nativeLength",6));File.WriteAllBytes(source.Path+".output-1.owner-saved",UiOriginalBytes(owner));PumpStationOutputQueues();Need(!p.QueueSaved,"different callback owner purpose completed source");owner.SetAttributeValue("accepted",UiOriginalHash(callbackBytes));File.WriteAllBytes(source.Path+".output-1.owner-saved",UiOriginalBytes(owner));PumpStationOutputQueues();Need(p.QueueSaved&&XElement.Load(source.Path+".station-queue-saved").Element("callbackOwnerSaved")!=null,"matching positive callback owner+native tile did not close saved source");
  p=Fresh(args[0]);source=p.Source;RegisterUiOutputStationQueue(p);saved=stationOutputQueues.Single();ObserveStationOutputQueueSync(saved.Window,false,null);Need(!saved.NativeReturned,"skipped native sync qualified detached terminal");ObserveStationOutputQueueSync(saved.Window,true,null);Need(saved.NativeReturned&&!CanAdvanceStationOutputQueueNative(saved.Tile),"actual returned native postimage did not freeze source and clock");saved.Window.WorkstationData.TileEntity=new TileEntityWorkstation();source.Scope.Card.Count=7;source.Scope.Grid.Cells[0].count=99;p.Actor.Died++;Need(!HoldsUiOutputStationQueue(source.Scope.Card),"reused native window held unrelated station forever");RebirthRemoteResourceNativePublication.Published=true;PumpStationOutputQueues();Need(p.QueueSaved&&source.Disposed&&(int)XElement.Load(source.Path+".station-queue-saved").Attribute("afterCount")==1,"positive native source failed metadata-only close after UI reuse/life change or used a new card count");
  Console.WriteLine("PASS "+checks+" actual station saved queue assertions; native body/UI/incarnation/publication are explicit adapters.");}
}
