using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
// AUTHORED_NOT_RUN. Entire production partial successor and WAL are injected.
// Native station/budget/publication adapters below are explicit doubles.
// ACTUAL_PARTIAL_SUCCESSOR_SOURCE
internal sealed class Recipe {internal string Name="recipe";}
internal sealed class ItemStack {internal int Wear;}
internal sealed class RecipeQueueItem
{internal Recipe Recipe=new Recipe();internal int Multiplier=1,AmountToRepair,Quality,StartingEntityId;internal bool IsCrafting;internal object RepairItem;internal float CraftingTimeLeft,OneItemCraftTime=5;}
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
internal static class RebirthProductiveEarnedRowProof
{
 internal const int MaximumFrameBytes=262144;
 internal sealed class Frame
 {
  internal Guid Birth,Operation;internal string World="world",FullRecipeBinding,ExtrasDefinition;
  internal int RowIndex,Multiplier,BeforeElapsedOffset,AfterElapsedOffset;internal float WorkedSeconds;
  internal byte[] BeforeRow,AfterRow,BeforeTools,AfterTools,BeforeNative,AfterNative;
 }
 internal static byte[] CaptureRow(RecipeQueueItem r)=>BitConverter.GetBytes(r.CraftingTimeLeft).Concat(BitConverter.GetBytes(r.Multiplier)).Concat(BitConverter.GetBytes(r.OneItemCraftTime)).ToArray();
 internal static byte[] Encode(Frame f)=>f.Operation.ToByteArray().Concat(f.BeforeRow).Concat(f.AfterRow).Concat(f.BeforeTools).Concat(f.AfterTools).ToArray();
}
internal static class RebirthNativeItemConformanceReader
{
 internal static bool TryEncodeStackArrayV1(ItemStack[] tools,int binary,int text,out string value)
 {value=Convert.ToBase64String(BitConverter.GetBytes(tools[0].Wear));return true;}
}
internal static class RebirthProductiveEarnedArtifact
{
 internal sealed class CompletionRetention {internal bool TryRetain()=>true;}
 internal static string Root;
 internal static int Prepares;internal static bool PrepareAllowed=true;
 internal static bool SameTools(ItemStack[] tools,byte[] bytes)=>BitConverter.GetBytes(tools[0].Wear).SequenceEqual(bytes);
 internal static bool TryPrepare(TileEntityWorkstation station,RebirthProductiveEarnedRowProof.Frame frame,out Prepared artifact)
 {
  Prepares++;artifact=null;if(!PrepareAllowed)return false;
  artifact=new Prepared{Birth=frame.Birth,Operation=frame.Operation,OriginalPath=Path.Combine(Root,frame.Operation.ToString("N")+".rew")};
  File.WriteAllBytes(artifact.OriginalPath,RebirthProductiveEarnedRowProof.Encode(frame));return true;
 }
 internal static bool SafePath(string path)=>System.IO.Path.GetFullPath(path).StartsWith(Root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 internal sealed class Prepared
 {
  
  internal CompletionRetention CaptureCompletionRetention(byte[] marker)=>new CompletionRetention(); // Actual retention partial has its own fixture.
  internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal Guid CustodyBirth=>Birth;
  internal string OriginalPath;internal int ImageLength=128;internal object LoadedSource;
  internal byte[] CopyImage()=>new byte[128];
  internal bool Current()=>true;internal bool MatchesMarker(byte[] bytes)=>bytes.SequenceEqual(new byte[]{9});
  internal byte[] Marker(object frame)=>new byte[]{9};internal void RetainCompleted(){}
 }
}
internal static class RebirthProductiveEarnedNativeBody
{
 internal const int MaximumBodyBytes=2097088;
 internal static byte[] Body(float timer,int wear)=>BitConverter.GetBytes(timer).Concat(BitConverter.GetBytes(wear)).ToArray();
 internal static byte[] ComparisonDigest(byte[] body,int offset){using(var sha=SHA256.Create())return sha.ComputeHash(body);}
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
 internal static bool TryMeasureTools(byte[] bytes,out long graph,out long scratch){graph=64;scratch=0;return true;}
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
 private static bool TryRestoreStagedNativeChild(Attempt value)=>false; // Separate staged fixture.
 private static bool MatchesPendingDefinition(Attempt value,RecipeQueueItem row)=>true; // Explicit definition adapter.
 internal sealed class Attempt
 {
  internal bool NativeTickCustody;internal Func<bool> NativeAdoptionReleaseReady;
  internal bool ClearedNativeMarker,NativeMetadataClosed;internal RebirthProductiveEarnedArtifact.CompletionRetention CompletionRetention;
  internal string PendingRecipeBinding,PendingExtras;internal IDisposable PendingDefinitionRetention;
  internal TileEntityWorkstation Station;internal NativeTerminal Terminal;internal NativePartialSuccessor PartialSuccessor;
  internal bool PendingTimerAmbiguous,PendingTimerObserved=true;internal int PendingTimerIndex;
  internal float PendingTimerBefore,PendingTimerTarget;internal RebirthProductiveEarnedArtifact.Prepared Artifact;
  internal RebirthProductiveEarnedRowProof.Frame Frame=new RebirthProductiveEarnedRowProof.Frame();internal ItemStack[] Tools;internal IDisposable Retention;
  internal NativeEarnedChild EarnedChild;
  internal RebirthRemoteResourceNativePublication.Watch Observation;
 }
 internal sealed class NativeTerminal:IDisposable
 {
  internal bool ToolsRebased;internal bool ToolRebaseCurrent()=>ToolsRebased;
  internal object SavedOrigin;
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
 internal static bool PrepareChild(Attempt a)=>TryPrepareEarnedNativeChild(a);
}
internal static class Program
{
 private static void Need(bool value,string text){if(!value)throw new Exception(text);}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-earned-child-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   Need(RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(-12,-7,5,out float work,out float timer)&&work==5&&timer==-7,"same-row exact earned child work");
   Need(RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(3,-7,5,out work,out timer)&&work==3&&timer==-7,"cycled earned child work");
   Need(RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(-5,0,5,out work,out timer)&&work==5&&timer==-float.Epsilon,"native exact-zero normalization");
   Need(!RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(-2,3,5,out work,out timer),"partial child misclassified earned");
   Need(!RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(-12,-6,5,out work,out timer),"non-native increase acquired work");
   Need(!RebirthProductiveEarnedCoordinator.TryEarnedNativeChildWork(0,-7,5,out work,out timer),"uninitialized zero acquired work");
   var parent=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"parent.rew"),-12,-7);
   RebirthProductiveEarnedArtifact.Prepares=0;RebirthProductiveEarnedArtifact.PrepareAllowed=false;
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(parent)&&parent.EarnedChild!=null&&parent.Station.Writes==0,"refused child preparation lost retained original or mutated native state");
   Guid operation=parent.EarnedChild.Frame.Operation;
   RebirthProductiveEarnedArtifact.PrepareAllowed=true;
   Need(RebirthProductiveEarnedCoordinator.PrepareChild(parent)&&parent.EarnedChild.Frame.Operation==operation&&parent.EarnedChild.Frame.WorkedSeconds==5,"retry replaced child operation/work");
   Need(parent.Station.Writes==0&&parent.Station.Queue[0].CraftingTimeLeft==-12&&parent.Station.Tools[0].Wear==0&&RebirthProductiveEarnedCoordinator.Clears==0,"preparation performed native work or marker cleanup");
   Need(File.Exists(parent.EarnedChild.Artifact.OriginalPath)&&File.Exists(parent.Artifact.OriginalPath+".earned-child"),"child original/link not durably published");
   int prepares=RebirthProductiveEarnedArtifact.Prepares;
   Need(RebirthProductiveEarnedCoordinator.PrepareChild(parent)&&RebirthProductiveEarnedArtifact.Prepares==prepares,"exact retry prepared another child");
   File.WriteAllBytes(parent.Artifact.OriginalPath+".earned-child",new byte[]{9});
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(parent)&&parent.Station.Writes==0,"conflicting lineage link overwritten");
   parent=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"unsaved.rew"),3,-7);parent.Terminal.Saved=false;
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(parent)&&parent.EarnedChild==null,"unsaved predecessor prepared child");
   parent=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"tools.rew"),3,-7);RebirthProductiveToolWork.Capacity=2;
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(parent)&&parent.EarnedChild==null&&parent.Station.Writes==0,"insufficient tools minted earned child");
   parent=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"changed.rew"),3,-7);Need(RebirthProductiveEarnedCoordinator.PrepareChild(parent),"initial cycled child preparation");
   Guid prior=parent.EarnedChild.Frame.Operation;parent.Station.Queue[0].Multiplier++;
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(parent)&&parent.EarnedChild.Frame.Operation==prior,"changed native row adopted child or minted replacement");
   Console.WriteLine("PASS extracted child preparation with disclosed codec/native/publication doubles; no earned permission qualification");
  }
  finally{foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}
