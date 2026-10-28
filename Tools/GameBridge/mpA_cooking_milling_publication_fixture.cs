// AUTHORED_NOT_RUN. Extracted actual methods; native serialization, UI events,
// account source and filesystem below are explicit doubles, not gameplay proof.
using System;using System.Linq;using System.Text;using System.Collections.Generic;using System.Xml.Linq;
class World {}
class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
class ItemStack {internal int Value;internal ItemStack(int value){Value=value;}}
class Recipe {internal string Name="same";}
class RecipeQueueItem {internal Recipe Recipe;internal int Count,Owner;}
class Grid {internal ItemStack[] Slots={new ItemStack(1)};internal ItemStack[] GetSlots()=>Slots;}
class XUiC_WorkstationInputGrid:Grid {}class XUiC_WorkstationToolGrid:Grid {}class XUiC_WorkstationFuelGrid:Grid {}
class XUiC_WorkstationWindowGroup
{
 internal WorkstationData WorkstationData=new WorkstationData();
 internal Grid outputWindow=new Grid();internal object craftingQueue=new object();internal RecipeQueueItem[] Rows;
 internal XUiC_WorkstationInputGrid Input=new XUiC_WorkstationInputGrid();internal XUiC_WorkstationToolGrid Tools=new XUiC_WorkstationToolGrid();internal XUiC_WorkstationFuelGrid Fuel=new XUiC_WorkstationFuelGrid();
 internal T GetChildByType<T>() where T:class=>typeof(T)==typeof(XUiC_WorkstationInputGrid)?Input as T:typeof(T)==typeof(XUiC_WorkstationToolGrid)?Tools as T:Fuel as T;
}
class TileEntityWorkstation {internal ItemStack[] Input,Output,Tools,Fuel;internal RecipeQueueItem[] Queue;}
class WorkstationData{internal TileEntityWorkstation TileEntity;}
class RebirthCamperStationState:TileEntityWorkstation {}
static class RebirthCamperStationNativeActorPayment{internal static bool IsHeld(RebirthCamperStationState station)=>false;}
// Independent mobile custody owners are excluded, never granted, in this
// existing warm milling-original cohort.
static class RebirthCamperStationWindowSession{internal static bool HasOriginalProjection(RebirthCamperStationState station)=>false;}
static class RebirthCamperStationFuelOriginal{internal static bool IsHeld(RebirthCamperStationState station)=>false;}
static class RebirthProductiveEarnedCoordinator{internal static bool BlocksMobileGrid(RebirthCamperStationState station)=>false;}
static class RebirthRemoteResourceFetchedStationOriginal{internal static bool BlocksStation(RebirthCamperStationState station)=>false;}
static class RebirthCamperStationSaveEvidence{internal static bool IsPending(RebirthCamperStationState station)=>false;}
static class RebirthCamperStationCompletionCallsite{internal static bool IsHeld(RebirthCamperStationState station)=>false;}
static class RebirthCamperStationCancellationRemoval{internal static bool IsHeld(RebirthCamperStationState station)=>false;}
static class RebirthCamperStationGridCustody
{
 // PRODUCTION_GRID_HELD
}
class ItemGrid {internal ItemStack[] items={new ItemStack(2)};}class Bag {internal ItemGrid ItemGrid=new ItemGrid();}
class Cursor {internal bool Empty=true;internal bool IsEmpty()=>Empty;}class Xui {internal Cursor DragAndDropWindow=new Cursor();}class PlayerUi {internal Xui xui=new Xui();}
class EntityPlayerLocal {internal Bag bag=new Bag(),inventory=new Bag();internal PlayerUi PlayerUI=new PlayerUi();}
class Identity {internal string StorageKey="owner";}
static class RebirthStablePlayerIdentity {internal static Identity Owner=new Identity();internal static bool TryFromLocalPlatform(out Identity owner){owner=Owner;return owner!=null;}}
static class RebirthCamperInventorySnapshot {internal static byte[] Capture(ItemStack[] items)=>Encoding.UTF8.GetBytes(string.Join(",",items.Select(x=>x.Value)));}
static class RebirthWorkstationCompletionOutbox {internal static string CurrentGuid(World world)=>"world";}
static class RebirthProductiveEarnedUiDeferredTransport
{
 internal static bool Showing=true;internal static long Version=1;
 internal static bool TryGetCurrentNativeUiSession(XUiC_WorkstationWindowGroup window,out long version){version=Version;return Showing;}
 internal static bool TryGetObservedNativeUiSession(XUiC_WorkstationWindowGroup window,out long version){version=Version;return true;}
}
static class RebirthWorkstationCompletionPublication
{
 internal static bool Refuse,Throw,CopyRefuse;internal static Dictionary<string,XElement> Files=new Dictionary<string,XElement>();
 internal static bool Write(string path,XElement image){if(Throw)throw new InvalidOperationException();if(Refuse)return false;Files[path]=new XElement(image);return true;}
 internal static XElement ReadPrimary(string path)=>Files.TryGetValue(path,out var value)?new XElement(value):null;
 internal static byte[] CopyPublishedBytes(string path)=>CopyRefuse?null:Encoding.UTF8.GetBytes(Files[path].ToString(SaveOptions.DisableFormatting));
 internal static bool MatchesPublishedBytes(string path,byte[] bytes)=>bytes!=null&&Files.ContainsKey(path)&&CopyPublishedBytes(path).SequenceEqual(bytes);
}
static class RebirthCookingMillingPublication
{
 private sealed class Original
 {
  internal World World=GameManager.Instance.World;internal string Path="original",Owner="owner";internal Guid Operation=Guid.NewGuid();internal XElement OriginalImage=new XElement("original");
  internal TileEntityWorkstation Station=new TileEntityWorkstation();internal XUiC_WorkstationWindowGroup Window=new XUiC_WorkstationWindowGroup();internal EntityPlayerLocal Actor=new EntityPlayerLocal();internal Recipe Recipe;
  internal ItemStack[] Output={new ItemStack(7)},UiInput={new ItemStack(1)},UiTools={new ItemStack(1)},UiFuel={new ItemStack(1)};
  internal byte[] QueueAfter,BagBefore,BeltBefore,Intent;internal long Session=1;
  internal bool Mutated=true,Cleared=true,NativeAccepted,NativeRan,NativeUncertain,UnusedAdmission,Finished,CurrentIdentity=true,NativeBeforeCurrent=true;
  internal bool Current()=>CurrentIdentity;
  internal bool MetadataCurrent()=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Intent);
  internal bool NativeUnchanged()=>NativeBeforeCurrent;
  // ACTUAL_NATIVE_TERMINAL
  // PRODUCTION_TERMINAL
  // PRODUCTION_LIVE_WINDOW
 }
 private sealed class MillingClose {internal Original Original;}
 private static MillingClose closingMilling;
 private static bool HasMobileColdHold(TileEntityWorkstation station)=>false;
 private static bool HasStaticMillingSource(TileEntityWorkstation station)=>false;
 private static readonly List<Original> pending=new List<Original>();
 private static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Select((x,i)=>x.Value==b[i].Value).All(x=>x);
 private static byte[] Queue(RecipeQueueItem[] rows)=>Encoding.UTF8.GetBytes(string.Join(";",rows.Select(x=>(x.Recipe?.Name??"empty")+":"+x.Count+":"+x.Owner)));
 private static RecipeQueueItem[] Rows(XUiC_WorkstationWindowGroup window)=>window.Rows;
 private static void Finish(Original original){original.Finished=true;pending.Remove(original);}
 // PRODUCTION_HELD
 // PRODUCTION_SYNC_GATE
 // PRODUCTION_RETAIN_UNUSED
 // PRODUCTION_FINISH_UNUSED
 // PRODUCTION_ENVELOPE
 private static int assertions;
 private static void Check(bool condition,string name){assertions++;if(!condition)throw new Exception(name);}
 private static Original New()
 {
  pending.Clear();closingMilling=null;RebirthProductiveEarnedUiDeferredTransport.Showing=true;RebirthProductiveEarnedUiDeferredTransport.Version=1;
  RebirthStablePlayerIdentity.Owner=new Identity();var original=new Original();var shared=new Recipe();original.Recipe=shared;
  original.Window.Rows=new[]{new RecipeQueueItem(),new RecipeQueueItem{Recipe=shared,Count=3,Owner=9}};
  original.Station.Queue=original.Window.Rows;original.Window.outputWindow.Slots=original.Output;original.Station.Output=original.Output;
  original.Window.WorkstationData.TileEntity=original.Station;
  original.QueueAfter=Queue(original.Window.Rows);original.BagBefore=RebirthCamperInventorySnapshot.Capture(original.Actor.bag.ItemGrid.items);original.BeltBefore=RebirthCamperInventorySnapshot.Capture(original.Actor.inventory.ItemGrid.items);
  pending.Add(original);return original;
 }
 static void Main()
 {
  var original=New();Check(original.Terminal(),"exact first row retirement leaves legitimate same Recipe successor");
  var mobile=new RebirthCamperStationState();original.Station=mobile;
  Check(RebirthCamperStationGridCustody.IsHeld(mobile),"mobile grid gestures and fuel switch respect the milling original");
  Check(!RebirthCamperStationGridCustody.IsHeld(new RebirthCamperStationState()),"unrelated mobile grid stays available");
  original.UnusedAdmission=true;Check(!RebirthCamperStationGridCustody.IsHeld(mobile),"unused pre-setter metadata does not hold mobile gameplay");
  original=New();
  original.Station.Queue=new[]{new RecipeQueueItem(),new RecipeQueueItem{Recipe=original.Window.Rows[1].Recipe,Count=2,Owner=9}};
  Check(!original.Terminal(),"other same Recipe row quantity cannot change");
  original=New();Check(CanSync(original.Window),"exact live original may perform its native projection");
  original.NativeUncertain=true;Check(!CanSync(original.Window),"uncertain native result never reprojects");original.NativeUncertain=false;
  original.NativeRan=true;Check(!CanSync(original.Window),"observed native invocation cannot reproject");original.NativeRan=false;
  original.NativeAccepted=true;Check(!CanSync(original.Window),"accepted result metadata only");original.NativeAccepted=false;
  RebirthProductiveEarnedUiDeferredTransport.Version=2;Check(!CanSync(original.Window),"same reused window with new session refuses");
  RebirthProductiveEarnedUiDeferredTransport.Version=1;RebirthProductiveEarnedUiDeferredTransport.Showing=false;Check(!CanSync(original.Window),"closed window has no pump projection authority");
  closingMilling=new MillingClose{Original=original};Check(original.LiveWindowCurrent(),"exact actual closing scope retains original session");closingMilling=null;
  foreach(string area in new[]{"input","tools","fuel","bag","belt","owner","cursor","source"})
  {
   original=New();switch(area){case "input":original.Window.Input.Slots[0].Value++;break;case "tools":original.Window.Tools.Slots[0].Value++;break;
    case "fuel":original.Window.Fuel.Slots[0].Value++;break;case "bag":original.Actor.bag.ItemGrid.items[0].Value++;break;case "belt":original.Actor.inventory.ItemGrid.items[0].Value++;break;
    case "owner":RebirthStablePlayerIdentity.Owner.StorageKey="other";break;case "cursor":original.Actor.PlayerUI.xui.DragAndDropWindow.Empty=false;break;case "source":original.NativeBeforeCurrent=false;break;}
   Check(!CanSync(original.Window),"full original drift refuses: "+area);
  }
  foreach(bool throwing in new[]{false,true})
  {
   original=New();pending.Clear();RebirthWorkstationCompletionPublication.Files.Clear();RebirthWorkstationCompletionPublication.Refuse=!throwing;RebirthWorkstationCompletionPublication.Throw=throwing;
   RetainUnusedAdmission(original);Check(pending.Contains(original)&&original.UnusedAdmission&&!original.Finished,"failed unused classification retains original before I/O");
   RebirthWorkstationCompletionPublication.Refuse=RebirthWorkstationCompletionPublication.Throw=false;
   TryFinishUnusedAdmission(original);Check(original.Finished&&!pending.Contains(original),"same actual pre-setter unused event completes metadata retry");
   Check(RebirthWorkstationCompletionPublication.Files.ContainsKey(original.Path+".unused"),"unused receipt exists before resource release");
  }
  Console.WriteLine("PASS "+assertions+" extracted production assertions; native/UI/durable adapters are doubles");
 }
}
