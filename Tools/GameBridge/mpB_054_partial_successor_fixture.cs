using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Entire production partial successor and WAL are injected.
// Native station/budget/publication adapters below are explicit doubles.
// ACTUAL_PARTIAL_SUCCESSOR_SOURCE
internal sealed class Recipe {internal string Name="recipe";}
internal sealed class ItemStack {internal int Wear;}
internal sealed class RecipeQueueItem
{internal Recipe Recipe=new Recipe();internal int Multiplier=1,AmountToRepair;internal object RepairItem;internal float CraftingTimeLeft,OneItemCraftTime=5;}
internal sealed class Position {internal int x,y,z;}
internal sealed class Block {}
internal sealed class TileEntityWorkstation
{
 internal RecipeQueueItem[] Queue;internal Block block=new Block();internal Position localChunkPos=new Position();internal int Writes;
 private ItemStack[] tools=new[]{new ItemStack()};internal ItemStack[] Tools {get=>tools;set{Writes++;tools=value;}}
 internal void setModified(){}internal Position ToWorldPos()=>localChunkPos;
}
internal sealed class World {}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World=new World();}
internal static class GameIO {internal static string GetSaveGameDir()=>RebirthProductiveEarnedArtifact.Root;}
internal static class RebirthRecyclerNativeLoop {internal static bool Running=true;internal static bool IsCurrentInvocation(TileEntityWorkstation s)=>Running;}
internal static class RebirthProductiveEarnedRemoteHost{internal static bool Handoff;internal static bool IsNativeHandoffTick(TileEntityWorkstation station)=>Handoff;}
internal static class RebirthRecyclerRuntime {internal static bool IsMarked(Recipe r)=>false;}
internal static class RebirthCookingHeat {internal static bool Managed(Recipe r)=>false;}
internal static class RebirthProductiveToolWork
{
 internal static float Capacity=100;internal static float Limit(ItemStack[] tools,Recipe recipe,Block block,float requested)=>Math.Min(Capacity,requested);
 internal static ItemStack[] Wear(ItemStack[] tools,Recipe recipe,Block block,float seconds)=>new[]{new ItemStack{Wear=tools[0].Wear+(int)seconds}};
}
internal static class RebirthProductiveEarnedUiQueueOwner {internal static string Binding(RecipeQueueItem row)=>row.Recipe.Name;}
internal static class RebirthRecipeOutputs {internal static string DefinitionIdentity(Recipe recipe)=>recipe.Name+"-extras";}
internal static class RebirthProductiveEarnedRowProof {internal const int MaximumFrameBytes=262144;}
internal static class RebirthProductiveEarnedArtifact
{
 internal sealed class CompletionRetention {internal bool TryRetain()=>true;}
 internal static string Root;
 internal static bool SafePath(string path)=>System.IO.Path.GetFullPath(path).StartsWith(Root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 internal sealed class Prepared
 {
  
  internal CompletionRetention CaptureCompletionRetention(byte[] marker)=>new CompletionRetention(); // Actual retention partial has its own fixture.
  internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal Guid CustodyBirth=>Birth;
  internal string OriginalPath;internal int ImageLength=128;internal object LoadedSource;
  internal bool Current()=>true;internal bool MatchesMarker(byte[] bytes)=>bytes.SequenceEqual(new byte[]{9});
  internal byte[] Marker(object frame)=>new byte[]{9};internal void RetainCompleted(){}
 }
}
internal static class RebirthProductiveEarnedNativeBody
{
 internal const int MaximumBodyBytes=2097088;
 internal static byte[] Body(float timer,int wear)=>BitConverter.GetBytes(timer).Concat(BitConverter.GetBytes(wear)).ToArray();
 internal static bool TryCapturePlan(TileEntityWorkstation s,ItemStack[] tools,int index,float timer,out byte[] before,out byte[] after,out int b,out int a)
 {before=Body(s.Queue[index].CraftingTimeLeft,s.Tools[0].Wear);after=Body(timer,tools[0].Wear);b=a=0;return true;}
 internal static bool SameExceptElapsed(byte[] a,int ao,byte[] b,int bo)=>a.SequenceEqual(b);
 internal static bool MatchesCurrent(TileEntityWorkstation s,byte[] expected,int offset)=>Body(s.Queue.Last().CraftingTimeLeft,s.Tools[0].Wear).SequenceEqual(expected);
 internal static bool TryMeasure(byte[] body,int offset,out long graph,out long scratch){graph=64;scratch=0;return true;}
}
internal sealed class Lease:IDisposable {internal int Disposals;public void Dispose(){Disposals++;}}
internal sealed class RebirthProductiveEarnedObservationBudget:IDisposable
{
 internal int Disposals;
 internal sealed class Observation:IDisposable {internal int Disposals;public void Dispose(){Disposals++;}}
 internal static RebirthProductiveEarnedObservationBudget TryOpen(RebirthProductiveEarnedArtifact.Prepared a,object f)=>new RebirthProductiveEarnedObservationBudget();
 internal IDisposable TryEnterCallbacks()=>new Lease();internal bool TryReserveAllocation(long n,out IDisposable lease){lease=new Lease();return true;}
 internal static bool TryRetainDetached(long n,out IDisposable lease){lease=new Lease();return true;}
 internal Observation TryObserveNative(byte[] body,int offset)=>new Observation();public void Dispose(){Disposals++;}
}
internal enum RemoteResourceSourceKind {WorkstationOutput}
internal static class RemoteResourceIdentity {internal static object Workstation(Position p)=>p;}
internal static class RebirthRemoteResourceNativeBinding
{internal static bool TryResolve(World w,object endpoint,RemoteResourceSourceKind kind,string birth,out object binding){binding=new object();return true;}}
internal static class RebirthRemoteResourceNativePublication
{
 internal sealed class Watch:IDisposable {internal static bool ThrowOnce;public void Dispose(){if(ThrowOnce){ThrowOnce=false;throw new Exception("watch disposal fault");}}}
 internal static Watch BeginWithBudget(object binding,Func<bool> current,RebirthProductiveEarnedObservationBudget memory,string key)=>new Watch();
}
internal static class RebirthProductiveEarnedSavedNativeSource
{
 internal static bool RegisterInvokedPartial(string path,string save,object preserve)=>true;
 internal static bool Saved;internal static int Publications;
 internal static bool Publish(RebirthProductiveEarnedArtifact.Prepared artifact,RebirthProductiveEarnedObservationBudget memory,
  RebirthRemoteResourceNativePublication.Watch watch,byte[] body,int offset,bool terminal,Func<bool> current,bool partialSuccessor=false)
 {if(!terminal||!partialSuccessor||!current())throw new Exception("Wrong publication arm");Publications++;return Saved;}
 internal static void Release(object source){}
 internal static void ReleaseRegisteredTerminal(Guid birth,Guid operation){}
}
internal static partial class RebirthProductiveEarnedCoordinator
{
 private static bool TryApplyStagedNativePartial(Attempt value)=>false; // Staged recovery has a separate extracted fixture.
 private static bool MatchesPendingDefinition(Attempt value,RecipeQueueItem row)=>true; // Explicit definition adapter.
 internal sealed class Attempt
 {
  internal bool NativeTickCustody;internal Func<bool> NativeAdoptionReleaseReady;
  internal object EarnedChild;
  internal bool ClearedNativeMarker,NativeMetadataClosed;internal RebirthProductiveEarnedArtifact.CompletionRetention CompletionRetention;
  internal string PendingRecipeBinding,PendingExtras;internal IDisposable PendingDefinitionRetention;
  internal TileEntityWorkstation Station;internal NativeTerminal Terminal;internal NativePartialSuccessor PartialSuccessor;
  internal bool PendingTimerAmbiguous,PendingTimerObserved=true;internal int PendingTimerIndex;
  internal float PendingTimerBefore,PendingTimerTarget;internal RebirthProductiveEarnedArtifact.Prepared Artifact;
  internal object Frame=new object();internal ItemStack[] Tools;internal IDisposable Retention;
  internal RebirthRemoteResourceNativePublication.Watch Observation;
 }
 internal sealed class NativeTerminal:IDisposable
 {
  internal bool ToolsRebased;internal bool ToolRebaseCurrent()=>ToolsRebased;
  internal IDisposable ToolRebaseRetention;internal byte[] ToolRebaseRecord;internal Func<bool> RemoteSavedSource;
  internal bool Closed;
  internal object SavedOrigin;
  internal Attempt Original;internal byte[] Body;internal int ElapsedOffset;internal bool Saved=true;
  internal RebirthProductiveEarnedObservationBudget Memory=new RebirthProductiveEarnedObservationBudget();
  internal RebirthProductiveEarnedObservationBudget.Observation Allocation=new RebirthProductiveEarnedObservationBudget.Observation();
  internal RebirthRemoteResourceNativePublication.Watch Watch=new RebirthRemoteResourceNativePublication.Watch();
  internal bool Current()=>Saved&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Body,0);
  // ACTUAL_NATIVE_TERMINAL_DISPOSE
 }
 private static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();
 private static bool marker=true;internal static int Clears;
 internal static bool TryNativeMarker(TileEntityWorkstation station,out byte[] bytes){bytes=new byte[]{9};return marker;}
 private static bool ClearExactMarker(TileEntityWorkstation station,byte[] bytes){if(!marker)return false;marker=false;Clears++;return true;}
 internal static Attempt Setup(string path,float before,float target)
 {
  marker=true;Clears=0;retained.Clear();RebirthRecyclerNativeLoop.Running=true;RebirthProductiveToolWork.Capacity=100;
  RebirthProductiveEarnedSavedNativeSource.Saved=false;RebirthProductiveEarnedSavedNativeSource.Publications=0;
  var station=new TileEntityWorkstation{Queue=new[]{new RecipeQueueItem{CraftingTimeLeft=before}}};
  var a=new Attempt{Station=station,Artifact=new RebirthProductiveEarnedArtifact.Prepared{OriginalPath=path},PendingTimerBefore=before,PendingTimerTarget=target};
  a.Terminal=new NativeTerminal{Original=a,Body=RebirthProductiveEarnedNativeBody.Body(before,0)};retained.Add(station,a);return a;
 }
 internal static bool Apply(Attempt a)=>TryApplyPartialNativeSuccessor(a);
 internal static bool Finish(Attempt a)=>TryFinishPartialNativeSuccessor(a);
 internal static bool Held(Attempt a)=>retained.TryGetValue(a.Station,out var current)&&ReferenceEquals(a,current);
}
internal static class Program
{
 private static void Need(bool value,string text){if(!value)throw new Exception(text);}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-partial-successor-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   Need(RebirthProductiveEarnedCoordinator.TryPartialNativeWork(-2,3,5,out float work,out float timer)&&work==2&&timer==3,"same-row partial work arithmetic");
   Need(RebirthProductiveEarnedCoordinator.TryPartialNativeWork(8,3,5,out work,out timer)&&work==5&&timer==3,"cycled partial work arithmetic");
   Need(!RebirthProductiveEarnedCoordinator.TryPartialNativeWork(-12,-7,5,out work,out timer),"fully earned child misclassified partial");
   Need(!RebirthProductiveEarnedCoordinator.TryPartialNativeWork(8,8,5,out work,out timer),"zero carryover invented work");
   var a=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"normal.rew"),-2,3);
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==1&&a.Station.Tools[0].Wear==2&&a.Station.Queue[0].CraftingTimeLeft==3,"actual partial work not applied once");
   Need(File.Exists(a.Artifact.OriginalPath+".partial-successor")&&File.Exists(a.Artifact.OriginalPath+".partial-successor.invoking"),"work entered without durable WAL");
   Need(RebirthProductiveEarnedCoordinator.Clears==0,"unsaved successor released marker");
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==1,"warm retry repeated work");
   RebirthProductiveEarnedSavedNativeSource.Saved=true;Need(RebirthProductiveEarnedCoordinator.Finish(a)&&a.Station.Writes==1&&RebirthProductiveEarnedCoordinator.Clears==1,"saved successor not retired without replay");
   a=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"unsaved.rew"),8,3);a.Terminal.Saved=false;
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0,"unsaved predecessor issued work");
   a=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"unknown.rew"),8,3);File.WriteAllBytes(a.Artifact.OriginalPath+".partial-successor.invoking",new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0,"unknown invoking file issued replay");
   a=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"capacity.rew"),8,3);RebirthProductiveToolWork.Capacity=2;
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0&&a.Station.Queue[0].CraftingTimeLeft==8,"tool limit discarded carryover");
   a=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"changed.rew"),8,3);RebirthProductiveEarnedCoordinator.Apply(a);a.Station.Queue[0].Recipe.Name="changed";RebirthProductiveEarnedSavedNativeSource.Saved=true;
   Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&RebirthProductiveEarnedCoordinator.Clears==0,"changed full recipe retired successor");
   a=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"wal-changed.rew"),8,3);RebirthProductiveEarnedCoordinator.Apply(a);
   File.WriteAllBytes(a.Artifact.OriginalPath+".partial-successor.invoking",new byte[]{0});RebirthProductiveEarnedSavedNativeSource.Saved=true;
   Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&a.Station.Writes==1&&RebirthProductiveEarnedCoordinator.Clears==0,"changed invoking evidence retired or replayed successor");
   string candidate=Path.Combine(root,"wal");Need(RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(candidate,new byte[]{1,2}),"WAL initial publish");
   Need(RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(candidate,new byte[]{1,2}),"WAL exact retry");
   Need(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(candidate,new byte[]{2,1}),"WAL replacement accepted");
   File.WriteAllBytes(candidate+".tmp",new byte[]{9});Need(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(candidate,new byte[]{1,2}),"unknown candidate consumed");
   var memory=new RebirthProductiveEarnedObservationBudget();var graph=new RebirthProductiveEarnedObservationBudget.Observation();var lease=new Lease();
   var disposing=new RebirthProductiveEarnedCoordinator.NativePartialSuccessor{Memory=memory,Allocation=graph,Retention=lease,Watch=new RebirthRemoteResourceNativePublication.Watch(),After=new byte[]{1}};
   RebirthRemoteResourceNativePublication.Watch.ThrowOnce=true;bool threw=false;try{disposing.Dispose();}catch{threw=true;}
   Need(threw&&graph.Disposals==1&&memory.Disposals==1&&lease.Disposals==1&&disposing.After==null,"throwing watch stranded actual partial graph/memory/retention");
   disposing.Dispose();Need(graph.Disposals==1&&memory.Disposals==1&&lease.Disposals==1,"disposed partial released resources twice");
   memory=new RebirthProductiveEarnedObservationBudget();graph=new RebirthProductiveEarnedObservationBudget.Observation();
   var terminal=new RebirthProductiveEarnedCoordinator.NativeTerminal{Memory=memory,Allocation=graph,Body=new byte[]{1},Watch=new RebirthRemoteResourceNativePublication.Watch()};
   RebirthRemoteResourceNativePublication.Watch.ThrowOnce=true;threw=false;try{terminal.Dispose();}catch{threw=true;}
   Need(threw&&graph.Disposals==1&&memory.Disposals==1&&terminal.Body==null&&terminal.Closed,"throwing watch stranded actual terminal graph/memory");
   terminal.Dispose();Need(graph.Disposals==1&&memory.Disposals==1,"disposed terminal released resources twice");
   a=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"post-clear-fault.rew"),-2,3);RebirthProductiveEarnedCoordinator.Apply(a);
   RebirthProductiveEarnedSavedNativeSource.Saved=true;RebirthRemoteResourceNativePublication.Watch.ThrowOnce=true;
   Need(!RebirthProductiveEarnedCoordinator.Finish(a)&&a.ClearedNativeMarker&&RebirthProductiveEarnedCoordinator.Held(a)&&RebirthProductiveEarnedCoordinator.Clears==1&&a.Station.Writes==1,
    "post-clear disposal fault lost retained latch or repeated work");
   int publications=RebirthProductiveEarnedSavedNativeSource.Publications;
   Need(RebirthProductiveEarnedCoordinator.Finish(a)&&a.NativeMetadataClosed&&!RebirthProductiveEarnedCoordinator.Held(a)&&RebirthProductiveEarnedCoordinator.Clears==1&&a.Station.Writes==1&&RebirthProductiveEarnedSavedNativeSource.Publications==publications,
    "latched retry republished native state, cleared marker again, or failed metadata retirement");
   Console.WriteLine("PASS extracted partial successor/WAL with disclosed native and publication doubles");
  }
  finally{foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}
