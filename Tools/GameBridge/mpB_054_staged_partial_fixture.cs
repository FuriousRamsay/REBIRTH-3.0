using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Entire production partial successor and WAL are injected.
// Native station/budget/publication adapters below are explicit doubles.
// Staged parser admission is an explicit adapter; partial_cold extracts that parser separately.
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
// Native handoff and tool-rebase producers have independent complete cohorts.
internal static class RebirthProductiveEarnedRemoteHost {internal static bool Handoff;internal static bool IsNativeHandoffTick(TileEntityWorkstation station)=>Handoff;}
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
internal static class RebirthNativeItemConformanceReader
{
 internal static bool TryEncodeStackArrayV1(ItemStack[] tools,int binary,int text,out string encoded)
 {encoded=Convert.ToBase64String(BitConverter.GetBytes(tools[0].Wear));return true;}
}
internal static class RebirthProductiveEarnedArtifact
{
 internal sealed class CompletionRetention {internal bool TryRetain()=>true;}
 internal static bool SameTools(ItemStack[] tools,byte[] bytes)=>BitConverter.GetBytes(tools[0].Wear).SequenceEqual(bytes);
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
 internal sealed class Image {internal ItemStack[] Tools;}
 internal static bool TryRead(byte[] bytes,int offset,out Image image,out ulong elapsed){elapsed=0;image=new Image{Tools=new[]{new ItemStack{Wear=BitConverter.ToInt32(bytes,4)}}};return true;}
 internal static byte[] Body(float timer,int wear)=>BitConverter.GetBytes(timer).Concat(BitConverter.GetBytes(wear)).ToArray();
 internal static bool TryCapturePlan(TileEntityWorkstation s,ItemStack[] tools,int index,float timer,out byte[] before,out byte[] after,out int b,out int a)
 {before=Body(s.Queue[index].CraftingTimeLeft,s.Tools[0].Wear);after=Body(timer,tools[0].Wear);b=a=0;return true;}
 internal static bool SameExceptElapsed(byte[] a,int ao,byte[] b,int bo)=>a.SequenceEqual(b);
 internal static bool MatchesCurrent(TileEntityWorkstation s,byte[] expected,int offset)=>Body(s.Queue.Last().CraftingTimeLeft,s.Tools[0].Wear).SequenceEqual(expected);
 internal static bool TryMeasure(byte[] body,int offset,out long graph,out long scratch){graph=64;scratch=0;return true;}
}
internal sealed class Lease:IDisposable {public void Dispose(){}}
internal sealed class RebirthProductiveEarnedObservationBudget:IDisposable
{
 internal sealed class Observation:IDisposable {public void Dispose(){}}
 internal static RebirthProductiveEarnedObservationBudget TryOpen(RebirthProductiveEarnedArtifact.Prepared a,object f)=>new RebirthProductiveEarnedObservationBudget();
 internal IDisposable TryEnterCallbacks()=>new Lease();internal bool TryReserveAllocation(long n,out IDisposable lease){lease=new Lease();return true;}
 internal static bool TryRetainDetached(long n,out IDisposable lease){lease=new Lease();return true;}
 internal Observation TryObserveNative(byte[] body,int offset)=>new Observation();public void Dispose(){}
}
internal enum RemoteResourceSourceKind {WorkstationOutput}
internal static class RemoteResourceIdentity {internal static object Workstation(Position p)=>p;}
internal static class RebirthRemoteResourceNativeBinding
{internal static bool TryResolve(World w,object endpoint,RemoteResourceSourceKind kind,string birth,out object binding){binding=new object();return true;}}
internal static class RebirthRemoteResourceNativePublication
{
 internal sealed class Watch:IDisposable {public void Dispose(){}}
 internal static Watch BeginWithBudget(object binding,Func<bool> current,RebirthProductiveEarnedObservationBudget memory,string key)=>new Watch();
}
internal static class RebirthProductiveEarnedSavedNativeSource
{
 internal sealed class Evidence:IDisposable {internal byte[] Native,StagedPartialRecord;internal int NativeOffset;internal string PendingRecipeBinding,PendingExtras,Path;internal bool PrimaryCurrent()=>File.ReadAllBytes(Path).SequenceEqual(StagedPartialRecord);internal IDisposable RetainNativeLoad()=>new Lease();public void Dispose(){}}
 internal static Evidence Plan;
 internal static bool TryReadStagedNativePartial(Evidence parent,Func<IDisposable> callbacks,out Evidence plan){using(var scope=callbacks()){plan=Plan;return scope!=null&&plan!=null;}} // Parser is separately extracted in partial_cold.

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
 
 private static bool MatchesPendingDefinition(Attempt value,RecipeQueueItem row)=>value.PendingRecipeBinding==RebirthProductiveEarnedUiQueueOwner.Binding(row)&&value.PendingExtras==RebirthRecipeOutputs.DefinitionIdentity(row.Recipe);
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
  internal RebirthProductiveEarnedSavedNativeSource.Evidence SavedOrigin;
  internal Attempt Original;internal byte[] Body;internal int ElapsedOffset;internal bool Saved=true;
  internal RebirthProductiveEarnedObservationBudget Memory=new RebirthProductiveEarnedObservationBudget();
  internal RebirthProductiveEarnedObservationBudget.Observation Allocation=new RebirthProductiveEarnedObservationBudget.Observation();
  internal RebirthRemoteResourceNativePublication.Watch Watch=new RebirthRemoteResourceNativePublication.Watch();
  internal bool Current()=>Saved&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Body,0);
  public void Dispose(){}
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
}
internal static class Program
{
 private static void Need(bool value,string text){if(!value)throw new Exception(text);}
 private static RebirthProductiveEarnedCoordinator.Attempt Setup(string root,string name)
 {
  var a=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,name+".rew"),-2,3);
  a.PendingRecipeBinding="recipe";a.PendingExtras="recipe-extras";
  string path=a.Artifact.OriginalPath+".partial-successor";byte[] record=Enumerable.Repeat((byte)7,180).ToArray();File.WriteAllBytes(path,record);
  var plan=new RebirthProductiveEarnedSavedNativeSource.Evidence{Path=path,Native=RebirthProductiveEarnedNativeBody.Body(3,2),StagedPartialRecord=record,
   PendingRecipeBinding="recipe",PendingExtras="recipe-extras"};
  RebirthProductiveEarnedSavedNativeSource.Plan=plan;a.Terminal.SavedOrigin=new RebirthProductiveEarnedSavedNativeSource.Evidence();return a;
 }
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-staged-partial-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   var a=Setup(root,"normal");Need(!RebirthProductiveEarnedCoordinator.Apply(a),"unsaved successor reported complete");
   Need(a.PartialSuccessor?.Returned==true&&a.Station.Writes==1&&a.Station.Queue[0].CraftingTimeLeft==3&&a.Station.Tools[0].Wear==2,
    "frozen unentered partial did not apply exactly once");
   Need(File.Exists(a.Artifact.OriginalPath+".partial-successor.invoking")&&RebirthProductiveEarnedCoordinator.Clears==0,"application lacked WAL or cleared before save");
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==1,"warm retry repeated staged setters");
   RebirthProductiveEarnedSavedNativeSource.Saved=true;Need(RebirthProductiveEarnedCoordinator.Finish(a)&&a.Station.Writes==1&&RebirthProductiveEarnedCoordinator.Clears==1,"saved partial cleanup did not finish without replay");
   a=Setup(root,"changed-definition");RebirthProductiveEarnedSavedNativeSource.Plan.PendingRecipeBinding="changed";
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0,"changed staged definition applied");
   a=Setup(root,"wrong-wear");RebirthProductiveEarnedSavedNativeSource.Plan.Native=RebirthProductiveEarnedNativeBody.Body(3,1);
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0,"unpaid staged tool wear applied");
   a=Setup(root,"entered");File.WriteAllBytes(a.Artifact.OriginalPath+".partial-successor.invoking",new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0,"entered plan replayed");
   a=Setup(root,"uncertain");File.WriteAllBytes(a.Artifact.OriginalPath+".partial-successor.invoking.tmp",new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0,"uncertain invocation replayed");
   a=Setup(root,"insufficient-tools");RebirthProductiveToolWork.Capacity=1;
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0,"partial debt clamped to insufficient tools");
   a=Setup(root,"outside");RebirthRecyclerNativeLoop.Running=false;
   Need(!RebirthProductiveEarnedCoordinator.Apply(a)&&a.Station.Writes==0,"staged application outside real invocation");
   Console.WriteLine("PASS extracted staged partial application/WAL/publication flow; disclosed native/parser/budget doubles");
  }
  finally{foreach(string path in Directory.EnumerateFiles(root))File.Delete(path);Directory.Delete(root);}
 }
}
