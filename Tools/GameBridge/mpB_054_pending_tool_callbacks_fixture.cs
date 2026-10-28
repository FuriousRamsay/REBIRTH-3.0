using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using HarmonyLib;
// AUTHORED_NOT_RUN. Both whole production gesture/owner partials injected.
// Explicit UI, codec, file IO, conservation, source-token and accounting
// adapters provide no game evidence.
// ACTUAL_PENDING_TOOL_CALLBACK_SOURCE
namespace HarmonyLib {
 internal sealed class Harmony {internal Harmony(string id){}}
 internal sealed class HarmonyPatch:Attribute {internal HarmonyPatch(){}internal HarmonyPatch(Type t,string n){}}
 internal sealed class HarmonyPriority:Attribute {internal HarmonyPriority(int n){}}
 internal static class Priority {internal const int Last=0;}
 internal static class AccessTools {
  internal static MethodInfo Method(Type t,string n)=>t.GetMethod(n);
  internal static MethodInfo DeclaredMethod(Type t,string n)=>t.GetMethod(n,BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
 }
}
internal static class RebirthHarmonyBootstrap {internal static void PatchClassOnce(object h,Type t){}}
internal sealed class ItemStack {internal static readonly ItemStack Empty=new ItemStack();internal int Id;internal ItemStack(int id=0){Id=id;}}
internal sealed class Grid {internal ItemStack[] items=new[]{new ItemStack()};}
internal sealed class Container {internal Grid ItemGrid=new Grid();}
internal class TileEntityWorkstation {internal ItemStack[] Tools=new[]{new ItemStack(7)};}
internal sealed class Recipe {}
internal sealed class RecipeQueueItem {internal Recipe Recipe;internal short Multiplier;internal float CraftingTimeLeft,OneItemCraftTime;internal bool IsCrafting;internal byte Quality;internal int StartingEntityId;}
internal sealed class XUiC_RecipeStack {internal bool IsCrafting;internal int OutputQuality,StartingEntityId;internal Recipe GetRecipe()=>new Recipe();internal int GetRecipeCount()=>1;internal float GetRecipeCraftingTimeLeft()=>-5;internal float GetOneItemCraftTime()=>4;}
internal sealed class XUiC_CraftingQueue {internal XUiC_RecipeStack[] GetRecipesToCraft()=>new[]{new XUiC_RecipeStack()};}
internal sealed class RebirthCamperStationState:TileEntityWorkstation {}
// This cohort exercises static local gestures. Remote/mobile admission is
// excluded; these adapters cannot grant a remote or mobile gesture capability.
internal static class RebirthCamperStationWindowSession {internal static bool Admitted(XUiC_WorkstationWindowGroup window,RebirthCamperStationState station)=>false;}
internal static class RebirthProductiveEarnedRemoteClient {internal static object EnterRemoteToolGesture(XUiC_ItemStack controller,bool ran)=>null;internal static void ObserveRemoteToolGesture(bool ran,object state){}internal static void EndRemoteToolGesture(Exception error,object state){}}
internal class EntityPlayer {internal int entityId=1,Died;}
internal sealed class EntityPlayerLocal:EntityPlayer {
 internal Container bag=new Container(),inventory=new Container();internal World world;
 internal bool Dead;internal string Creation="creation";internal bool IsDead()=>Dead;
}
internal sealed class World {
 internal bool Remote;internal EntityPlayerLocal Actor;internal bool IsRemote()=>Remote;
 internal EntityPlayerLocal GetPrimaryPlayer()=>Actor;internal object GetEntity(int id)=>Actor;
}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;internal int SaveRequests;internal void SaveLocalPlayerData(){SaveRequests++;}}
internal static class ThreadManager {internal static bool Main=true;internal static bool IsMainThread()=>Main;}
internal sealed class DragWindow {internal ItemStack CurrentStack=new ItemStack();}
internal sealed class PlayerUI {internal EntityPlayerLocal entityPlayer;}
internal sealed class XUi {internal DragWindow DragAndDropWindow=new DragWindow();internal PlayerUI playerUI=new PlayerUI();}
internal sealed class WorkstationData {internal TileEntityWorkstation TileEntity=new TileEntityWorkstation();}
internal sealed class XUiC_WorkstationWindowGroup {
 internal XUi xui=new XUi();internal WorkstationData WorkstationData=new WorkstationData();internal XUiC_WorkstationToolGrid Tools;
 internal bool Open=true;internal long Session=1;internal T GetChildByType<T>() where T:class=>Tools as T;
 internal XUiC_WorkstationToolGrid toolWindow=>Tools;
 internal XUiC_WorkstationToolGrid inputWindow,outputWindow,fuelWindow;
 internal XUiC_CraftingQueue craftingQueue=new XUiC_CraftingQueue();public void syncTEfromUI(){}
}
internal sealed class XUiC_WorkstationToolGrid {
 internal XUi xui;internal ItemStack[] Slots=new[]{new ItemStack(7)};internal XUiC_ItemStack[] Controllers;
 internal ItemStack[] GetSlots()=>Slots;internal XUiC_ItemStack[] GetItemStackControllers()=>Controllers;
}
internal class XUiC_ItemStack {
 internal enum StackLocationTypes {Backpack,ToolBelt,Workstation}
 internal StackLocationTypes StackLocation;internal int SlotNumber;internal ItemStack ItemStack=new ItemStack();internal XUi xui;internal XUiC_WorkstationWindowGroup Parent;
 internal T GetParentByType<T>() where T:class=>Parent as T;
 public void HandleStackSwap(){} public void SwapItem(){} public void HandlePartialStackPickup(){} public virtual void HandleDropOne(){} public void HandleMoveToPreferredLocation(){}
}
internal sealed class XUiC_RequiredItemStack:XUiC_ItemStack {public override void HandleDropOne(){}}
internal static class XUiC_RebirthStationWorkspace {internal static XUiC_WorkstationWindowGroup ActiveInstance;}
internal sealed class LockManager {internal static LockManager Instance=new LockManager();internal bool Locked=true;internal bool IsLockedByLocalPlayer(object s,int i)=>Locked;}
internal static class RebirthCamperCharacterAccess {internal static string CurrentCreation(EntityPlayerLocal a)=>a.Creation;}
internal sealed class Identity {internal string StorageKey;internal string CanonicalId="canonical";}
internal static class RebirthStablePlayerIdentity {
 internal static string Owner="owner";internal static bool TryFromLocalPlatform(out Identity i){i=new Identity{StorageKey=Owner};return Owner!=null;}
}
internal static class RebirthProductiveEarnedUiDeferredTransport {
 internal static void InstallRecipientWindowObserver(){}
 internal static bool TryGetCurrentNativeUiSession(XUiC_WorkstationWindowGroup w,out long s){s=w.Session;return w.Open;}
}
internal static class RebirthNativeItemConformanceReader {
 internal static bool TryEncodeStackArrayV1(ItemStack[] a,int bytes,int chars,out string s){s=null;if(a==null||a.Any(v=>v==null))return false;s=Convert.ToBase64String(a.Select(v=>(byte)v.Id).ToArray());return true;}
 internal static bool TryDecodeStackArrayV1(string s,int count,int bytes,int chars,out ItemStack[] a){a=Convert.FromBase64String(s).Select(v=>new ItemStack(v)).ToArray();return a.Length==count;}
}
internal static class RebirthProductiveEarnedPendingToolTransfer {
 internal static bool ConservedGesture(ItemStack[] b,ItemStack[] l,ItemStack[] t,ItemStack c,ItemStack[] nb,ItemStack[] nl,ItemStack[] nt,ItemStack nc)
 =>b.Concat(l).Concat(t).Concat(new[]{c}).Select(v=>v.Id).OrderBy(v=>v).SequenceEqual(nb.Concat(nl).Concat(nt).Concat(new[]{nc}).Select(v=>v.Id).OrderBy(v=>v));
}
internal sealed class RebirthProductiveEarnedObservationBudget:IDisposable {
 public void Dispose(){}
 internal static bool Available=true;internal static int Live;
 internal sealed class Decoder:IDisposable {public void Dispose(){}}
 internal sealed class Lease:IDisposable {internal Lease(){Live++;}public void Dispose(){Live--;}}
 internal static Decoder TryEnterDecoder()=>Available?new Decoder():null;
 internal static IDisposable TryEnterCandidateDecoder()=>TryEnterDecoder();
 internal static bool TryRetainDetached(long n,out IDisposable lease){lease=Available?new Lease():null;return lease!=null;}
 internal static bool TryResizeDetached(IDisposable l,long n)=>Available;
 internal static bool TryMeasureTools(byte[] b,out long g,out long s){g=b.Length;s=32;return true;}
}
internal static class RebirthProductiveEarnedCoordinator {
 internal static bool Enabled=true,Admit=true;internal static int Sources;
 internal sealed class SavedPendingToolsObservation:IDisposable {
  internal TileEntityWorkstation Station;internal bool IsMobile=>false;
  internal IDisposable EnterNativeCallbacks()=>RebirthProductiveEarnedObservationBudget.TryEnterDecoder();
  internal bool MatchesNativeAfter(byte[] body,int offset)=>CurrentContext()&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,body,offset);
  internal bool TryProjectMobileTools(RecipeQueueItem[] queue,ItemStack[] input,ItemStack[] output,ItemStack[] fuel,ItemStack[] tools,out byte[] before,out byte[] after){before=after=null;return false;}
  // Publication is outside this static gesture cohort. Never grant rebase authority.
  internal bool TryPublishToolRebase(RebirthProductiveEarnedPendingToolGestures.SavedToolPublication publication)=>false;
  internal bool Valid=true;private bool disposed;internal bool RetainedContext()=>Valid&&!disposed;internal bool CurrentContext()=>RetainedContext();
  internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal int Index,Offset;internal float BeforeTimer=-5,TargetTimer=-1;internal string Binding="binding",Extras="extras";
  internal string OriginalPath=>System.IO.Path.Combine(Program.Root,Program.SourceName);
  internal byte[] CopyNativeBefore()=>new byte[]{7};internal byte[] CopyOriginalDigest()=>new byte[32];
  internal bool MatchesBeforeTools(byte[] b,RebirthProductiveEarnedObservationBudget.Decoder d)=>b.SequenceEqual(new byte[]{7});
  public void Dispose(){if(disposed)return;disposed=true;Sources--;}
 }
 internal static bool TryObserveSavedPendingTools(TileEntityWorkstation s,RebirthProductiveEarnedObservationBudget.Decoder d,out SavedPendingToolsObservation o){o=null;if(!Admit)return false;Sources++;o=new SavedPendingToolsObservation{Station=s};return true;}
}
internal static class RebirthRemoteResourceNativePublication {internal sealed class Watch:IDisposable {public void Dispose(){}}}
internal sealed class PlayerDataFile {
 internal const string EXT="ttp";internal int id=1,deaths;internal byte[] Image=new byte[]{1,2,3};
 internal ItemStack[] Bag=new[]{new ItemStack()},Belt=new[]{new ItemStack()};public void FromPlayer(){}public void Save(){}
}
internal static class GameIO {internal static string GetPlayerDataDir()=>System.IO.Path.GetFullPath("fixture-players");internal static string GetSaveGameDir()=>System.IO.Path.GetFullPath("fixture-save");}
internal static class RebirthGearPlayerDataInventory {internal static ItemStack[] ReadSlots(PlayerDataFile data,bool bag)=>bag?data.Bag:data.Belt;}
internal static class RebirthProductiveEarnedArtifact {internal static bool SameTools(ItemStack[] a,byte[] b)=>a.Select(v=>(byte)v.Id).SequenceEqual(b);internal static string WorldIdentity(World w)=>"fixture-world";}
internal static class RebirthProductiveEarnedUiOwnerFile {
 internal static byte[] Saved;internal static bool Safe=true;
 internal static byte[] DeferredNativeOwnerImage(PlayerDataFile data)=>(byte[])data.Image.Clone();
 internal static bool DeferredNativeOwnerPathSafe(string path)=>Safe;
 internal static bool DeferredNativeOwnerFileMatches(string path,byte[] image)=>Safe&&Saved!=null&&Saved.SequenceEqual(image);
}
// Explicit native projection/body adapters; actual shape/conformance has its own fixture.
internal static class RebirthProductiveEarnedNativeBody {
 internal sealed class Image {internal ItemStack[] Tools;}
 internal static bool MatchesCurrent(TileEntityWorkstation s,byte[] bytes,int offset)=>bytes.SequenceEqual(s.Tools.Select(v=>(byte)v.Id));
 internal static bool TryCaptureUiProjection(TileEntityWorkstation s,RecipeQueueItem[] rows,ItemStack[] input,ItemStack[] output,ItemStack[] fuel,ItemStack[] tools,out byte[] before,out byte[] after,out int a,out int b){before=s.Tools.Select(v=>(byte)v.Id).ToArray();after=tools.Select(v=>(byte)v.Id).ToArray();a=b=0;return true;}
 internal static bool ValidPendingToolReplacement(byte[] before,int a,byte[] after,int b,int index,float timer)=>before.Length==after.Length&&!before.SequenceEqual(after);
 internal static bool TryRead(byte[] bytes,int offset,out Image image,out ulong elapsed){image=new Image{Tools=bytes.Select(v=>new ItemStack(v)).ToArray()};elapsed=0;return true;}
}
internal static class Program {
 internal static string Root,SourceName="original.rew";
 static readonly Type Hooks=typeof(RebirthProductiveEarnedPendingToolGestures).GetNestedType("Gesture",BindingFlags.NonPublic);
 static object Invoke(string n,params object[] args)=>Hooks.GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
 static object Enter(XUiC_ItemStack c,bool run=true){object[] a={c,run,null};Invoke("Prefix",a);return a[2];}
 static void Return(object call){Invoke("Postfix",true,call);Invoke("Finalizer",null,call);}
 static void Need(bool b,string m){if(!b)throw new Exception(m);}
 static void Capture(PlayerDataFile data,Setup setup)=>typeof(RebirthProductiveEarnedPendingToolGestures).GetMethod("ObserveOwnerCapture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{data,setup.Actor,true});
 static object SaveEnter(PlayerDataFile data,bool ran=true){var type=typeof(RebirthProductiveEarnedPendingToolGestures).GetNestedType("OwnerSave",BindingFlags.NonPublic);object[] args={data,GameIO.GetPlayerDataDir(),"canonical",ran,null};type.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);return args[4];}
 static void SaveReturn(object state,bool ran=true){var type=typeof(RebirthProductiveEarnedPendingToolGestures).GetNestedType("OwnerSave",BindingFlags.NonPublic);type.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{ran,state});type.GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{null,state});}
 sealed class Setup {
  internal World World=new World();internal EntityPlayerLocal Actor=new EntityPlayerLocal();internal XUiC_WorkstationWindowGroup Window=new XUiC_WorkstationWindowGroup();internal XUiC_ItemStack Controller;
  internal XUiC_WorkstationToolGrid Tools=>Window.Tools;internal TileEntityWorkstation Station=>Window.WorkstationData.TileEntity;
  internal Setup(){Actor.world=World;World.Actor=Actor;GameManager.Instance.World=World;Window.xui.playerUI.entityPlayer=Actor;Window.Tools=new XUiC_WorkstationToolGrid{xui=Window.xui};Controller=new XUiC_ItemStack{Parent=Window,xui=Window.xui};Tools.Controllers=new[]{Controller};}
  internal bool Observe(out RebirthProductiveEarnedPendingToolGestures.Observation o)=>RebirthProductiveEarnedPendingToolGestures.TryObserveReturnedGesture(Actor,Station,out o);
  internal void Swap(){var old=Tools.Slots[0];Tools.Slots[0]=Window.xui.DragAndDropWindow.CurrentStack;Window.xui.DragAndDropWindow.CurrentStack=old;}
  internal void Close(){Window.Open=false;RebirthProductiveEarnedPendingToolGestures.Pump();Need(RebirthProductiveEarnedCoordinator.Sources==0&&RebirthProductiveEarnedObservationBudget.Live==0,"scope cleanup leaked");}
 }
 public static void Main(){
  Root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"rb-gesture-callbacks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Root);
  try{
  Need(((IEnumerable<MethodBase>)Invoke("TargetMethods")).Count()==6,"native callback target set changed");
  var initial=new Setup();RebirthProductiveEarnedPendingToolGestures.ObserveWindowOpened(initial.Window);
  Need(!RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureBefore(initial.Actor,initial.Station,out _),"window-open snapshot became saved before");
  int priorRequests=GameManager.Instance.SaveRequests;RebirthProductiveEarnedPendingToolGestures.Pump();RebirthProductiveEarnedPendingToolGestures.Pump();Need(GameManager.Instance.SaveRequests==priorRequests+1,"before save requested more than once per opening");
  var baseline=new PlayerDataFile();Capture(baseline,initial);var beforeSave=SaveEnter(baseline);Need(beforeSave!=null,"unentered baseline native capture not admitted");RebirthProductiveEarnedUiOwnerFile.Saved=(byte[])baseline.Image.Clone();SaveReturn(beforeSave);
  Need(RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureBefore(initial.Actor,initial.Station,out var beforeCapture)&&beforeCapture.Before,"native saved-before not positively observed");
  var firstGesture=Enter(initial.Controller);string journal=System.IO.Path.Combine(Root,SourceName)+".tool-gesture";
  Need(firstGesture!=null&&File.Exists(journal)&&File.Exists(journal+".01.invoking")&&!File.Exists(journal+".01.returned"),"native entry did not durably precede action");
  initial.Swap();Return(firstGesture);Need(File.Exists(journal+".01.returned"),"native positive return not recorded");Need(!RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureBefore(initial.Actor,initial.Station,out _),"saved before retrospectively issued after action");
  var settle=Enter(initial.Controller);initial.Actor.bag.ItemGrid.items[0]=initial.Window.xui.DragAndDropWindow.CurrentStack;initial.Window.xui.DragAndDropWindow.CurrentStack=new ItemStack();Return(settle);
  baseline.Bag=new[]{new ItemStack(7)};Capture(baseline,initial);RebirthProductiveEarnedPendingToolGestures.Pump();Need(!File.Exists(journal+".02.owner-after"),"capture-only minted saved after journal");
  beforeSave=SaveEnter(baseline);RebirthProductiveEarnedUiOwnerFile.Saved=(byte[])baseline.Image.Clone();SaveReturn(beforeSave);RebirthProductiveEarnedPendingToolGestures.Pump();Need(File.Exists(journal+".02.owner-after"),"actual saved after owner not linked to returned journal");
  byte[] publishedOwner=File.ReadAllBytes(journal+".02.owner-after");RebirthProductiveEarnedPendingToolGestures.Pump();Need(publishedOwner.SequenceEqual(File.ReadAllBytes(journal+".02.owner-after")),"repeat publication rewrote immutable owner event");
  var stationHooks=typeof(RebirthProductiveEarnedPendingToolGestures).GetNestedType("StationSyncObserver",BindingFlags.NonPublic);object[] stationArgs={initial.Window,true,null};stationHooks.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,stationArgs);Need(stationArgs[2]!=null&&File.Exists(journal+".02.station-invoking")&&!File.Exists(journal+".02.station-returned"),"actual sync entry missing durable original");
  initial.Station.Tools=initial.Tools.Slots.Select(v=>new ItemStack(v.Id)).ToArray();stationHooks.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{initial.Window,true,stationArgs[2]});stationHooks.GetMethod("Finalizer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{null,stationArgs[2]});Need(File.Exists(journal+".02.station-returned"),"actual native tool sync return not observed");
  Need(RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(Path.Combine(Root,SourceName),out var parsed)&&parsed.Returned==2&&parsed.OwnerStep==2&&parsed.StationStep==2&&!parsed.StationPending&&!parsed.PendingInvocation&&parsed.Before.Cells[2]!=parsed.After.Cells[2],"bounded native journal chain not described");parsed.Dispose();
  byte[] rootBytes=File.ReadAllBytes(journal);File.AppendAllText(journal,"trailing");Need(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(Path.Combine(Root,SourceName),out _),"trailing journal bytes accepted");File.WriteAllBytes(journal,rootBytes);
  File.Copy(journal+".02.returned",journal+".03.returned");Need(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(Path.Combine(Root,SourceName),out _),"orphan return accepted");File.Delete(journal+".03.returned");
  File.WriteAllBytes(journal+".03.invoking.tmp",new byte[]{1});Need(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(Path.Combine(Root,SourceName),out _),"uncertain journal temporary accepted");File.Delete(journal+".03.invoking.tmp");
  File.WriteAllBytes(journal+".65.invoking",new byte[]{1});Need(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(Path.Combine(Root,SourceName),out _),"out-of-bound journal step accepted");File.Delete(journal+".65.invoking");
  initial.Close();Need(File.Exists(journal)&&File.Exists(journal+".02.owner-after"),"scope close deleted durable custody");
  SourceName="interrupted.rew";initial=new Setup();RebirthProductiveEarnedPendingToolGestures.ObserveWindowOpened(initial.Window);baseline=new PlayerDataFile();Capture(baseline,initial);beforeSave=SaveEnter(baseline);RebirthProductiveEarnedUiOwnerFile.Saved=(byte[])baseline.Image.Clone();SaveReturn(beforeSave);firstGesture=Enter(initial.Controller);journal=System.IO.Path.Combine(Root,SourceName)+".tool-gesture";Need(firstGesture!=null&&File.Exists(journal+".01.invoking"),"interrupted setup missing invocation");Invoke("Finalizer",new Exception("native interrupted"),firstGesture);initial.Close();Need(!File.Exists(journal+".01.returned")&&File.Exists(journal+".01.invoking"),"interrupted journal replayed or deleted");
  Need(RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(Path.Combine(Root,SourceName),out parsed)&&parsed.PendingInvocation&&parsed.Returned==0&&parsed.OwnerStep==0,"interrupted journal converted to returned work");parsed.Dispose();
  initial=new Setup();RebirthProductiveEarnedPendingToolGestures.ObserveWindowOpened(initial.Window);baseline=new PlayerDataFile();Capture(baseline,initial);beforeSave=SaveEnter(baseline);SaveReturn(beforeSave);Need(Enter(initial.Controller)==null,"retained journal conflict was bypassed");initial.Close();SourceName="unprepared.rew";
  initial=new Setup();RebirthProductiveEarnedPendingToolGestures.ObserveWindowOpened(initial.Window);initial.Window.Session++;RebirthProductiveEarnedPendingToolGestures.ObserveWindowOpened(initial.Window);Need(RebirthProductiveEarnedCoordinator.Sources==1,"actual reopen retained prior session or duplicated source");initial.Close();
  var s=new Setup();Need(Enter(s.Controller,false)==null,"skipped original captured");
  var call=Enter(s.Controller);Need(call!=null,"real native gesture not observed");Need(Enter(s.Controller)==null,"nested callback counted twice");
  s.Swap();Return(call);Need(s.Observe(out var observation)&&observation.Steps==1,"returned cursor swap missing");
  call=Enter(s.Controller);s.Actor.bag.ItemGrid.items[0]=s.Window.xui.DragAndDropWindow.CurrentStack;s.Window.xui.DragAndDropWindow.CurrentStack=new ItemStack();Return(call);
  Need(s.Observe(out observation)&&observation.Steps==2,"same-cohort native cursor return missing");
  var data=new PlayerDataFile{Bag=new[]{new ItemStack(7)}};Capture(data,s);Need(observation.OwnerAfter!=null,"actual FromPlayer matching frame not retained");
  Need(!RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out _),"capture alone became saved owner");
  var save=SaveEnter(data);Need(save!=null,"actual matching native Save not observed");RebirthProductiveEarnedUiOwnerFile.Saved=(byte[])data.Image.Clone();SaveReturn(save);
  Need(RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out var ownerCapture),"positive native final file not observed");
  var firstWriter=SaveEnter(data);var secondWriter=SaveEnter(data);Need(firstWriter!=null&&secondWriter!=null,"overlap setup failed");
  Need(!RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out _),"active native save reused old positive observation");
  SaveReturn(firstWriter);Need(!RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out _),"first overlapping writer granted while another remained");
  SaveReturn(secondWriter);Need(RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out _),"last matching returned writer not observed");
  data.Image[0]=99;Need(SaveEnter(data)==null,"mutated native captured object adopted");data.Image[0]=1;
  Need(!RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out _),"mismatched newer native Save reused earlier receipt");
  RebirthProductiveEarnedUiOwnerFile.Saved=new byte[]{9};Need(!RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out _),"replaced final file accepted");
  // A worker scope keeps accounting alive when the UI closes, then releases once.
  save=SaveEnter(data);Need(save!=null,"worker lease setup");s.Window.Open=false;RebirthProductiveEarnedPendingToolGestures.Pump();Need(RebirthProductiveEarnedObservationBudget.Live==1,"worker lost pinned allocation");SaveReturn(save,false);Need(RebirthProductiveEarnedObservationBudget.Live==0,"worker allocation leaked");
  s=new Setup();call=Enter(s.Controller);s.Swap();Return(call);Capture(new PlayerDataFile(),s);Need(s.Observe(out observation)&&observation.OwnerAfter==null,"cursor-held custody omitted from owner capture");s.Close();
  s=new Setup();call=Enter(s.Controller);Return(call);data=new PlayerDataFile();Capture(data,s);save=SaveEnter(data);RebirthProductiveEarnedUiOwnerFile.Saved=(byte[])data.Image.Clone();SaveReturn(save);Need(RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out _),"recapture setup failed");
  data.Bag=new[]{new ItemStack(99)};Capture(data,s);Need(SaveEnter(data)==null&&!RebirthProductiveEarnedPendingToolGestures.TryObserveSavedGestureOwner(s.Actor,s.Station,out _),"failed reused FromPlayer retained old capture");s.Close();
  s=new Setup();call=Enter(s.Controller);s.Swap();Return(call);call=Enter(s.Controller);s.Actor.bag.ItemGrid.items[0]=s.Window.xui.DragAndDropWindow.CurrentStack;s.Window.xui.DragAndDropWindow.CurrentStack=new ItemStack();Return(call);Need(s.Observe(out observation),"gap setup failed");
  s.Actor.bag.ItemGrid.items[0]=new ItemStack(9);Need(!s.Observe(out _),"live gap admitted");Need(Enter(s.Controller)==null&&observation.Unknown,"gap not quarantined");s.Close();
  s=new Setup();call=Enter(s.Controller);var error=new Exception("native");Need(ReferenceEquals(Invoke("Finalizer",error,call),error),"original native exception replaced");Need(!s.Observe(out _),"throwing callback granted result");s.Close();
  s=new Setup();call=Enter(s.Controller);s.Tools.Slots[0]=new ItemStack(9);Return(call);Need(!s.Observe(out _),"nonconserved native callback granted result");s.Close();
  s=new Setup();call=Enter(s.Controller);Invoke("Postfix",false,call);Invoke("Finalizer",null,call);Need(!s.Observe(out _),"skipped native callback granted return");s.Close();
  s=new Setup();call=Enter(s.Controller);RebirthProductiveEarnedObservationBudget.Available=false;Return(call);RebirthProductiveEarnedObservationBudget.Available=true;Need(!s.Observe(out _),"return decoder failure granted result");s.Close();
  for(int variant=0;variant<7;variant++){
   s=new Setup();call=Enter(s.Controller);s.Swap();Return(call);Need(s.Observe(out observation),"scope setup failed");
   if(variant==0)s.Actor.Died++;if(variant==1)s.Actor.Creation="other";if(variant==2)s.Window.Session++;if(variant==3)GameManager.Instance.World=new World();if(variant==4)observation.Source.Valid=false;if(variant==5)RebirthStablePlayerIdentity.Owner="other";
   if(variant==6)s.Window.Tools=new XUiC_WorkstationToolGrid{xui=s.Window.xui};
   Need(!s.Observe(out _),"changed identity/scope accepted: "+variant);RebirthProductiveEarnedPendingToolGestures.Pump();Need(RebirthProductiveEarnedCoordinator.Sources==0&&RebirthProductiveEarnedObservationBudget.Live==0,"scope prune leaked: "+variant);RebirthStablePlayerIdentity.Owner="owner";
  }
  s=new Setup();RebirthProductiveEarnedObservationBudget.Available=false;Need(Enter(s.Controller)==null,"decoder pressure admitted");RebirthProductiveEarnedObservationBudget.Available=true;
  RebirthProductiveEarnedCoordinator.Enabled=false;Need(Enter(s.Controller)==null,"disabled observer captured");RebirthProductiveEarnedCoordinator.Enabled=true;
  RebirthProductiveEarnedCoordinator.Admit=false;Need(Enter(s.Controller)==null,"missing saved pending source admitted");RebirthProductiveEarnedCoordinator.Admit=true;
  var spoof=new XUiC_ItemStack{Parent=s.Window,xui=s.Window.xui,StackLocation=XUiC_ItemStack.StackLocationTypes.Backpack,ItemStack=new ItemStack(99)};Need(Enter(spoof)==null,"physical source slot spoof accepted");
  call=Enter(s.Controller);Return(call);Need(s.Observe(out observation),"no-op native return unavailable");observation.Steps=64;Need(Enter(s.Controller)==null,"step bound exceeded");s.Close();
  Need(RebirthProductiveEarnedCoordinator.Sources==0&&RebirthProductiveEarnedObservationBudget.Live==0,"final leases retained");
  Console.WriteLine("PASS whole production gesture callbacks/lifecycle with explicit UI/codec/source/conservation/budget adapters; no owner-save or rebase evidence");
  }finally{
   if(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Root))!=System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar)||!System.IO.Path.GetFileName(Root).StartsWith("rb-gesture-callbacks-"))throw new Exception("unsafe fixture cleanup");
   foreach(string file in Directory.EnumerateFiles(Root))File.Delete(file);Directory.Delete(Root);
  }
 }
}
