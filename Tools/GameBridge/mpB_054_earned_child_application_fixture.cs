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
internal class TileEntityWorkstation
{
 internal RecipeQueueItem[] Queue;internal Block block=new Block();internal Position localChunkPos=new Position();internal int Writes;
 internal static bool ThrowWrites;
 private ItemStack[] tools=new[]{new ItemStack()};internal ItemStack[] Tools {get=>tools;set{Writes++;if(ThrowWrites)throw new Exception("native setter fault");tools=value;}}
 internal Chunk Chunk=new Chunk();internal Chunk GetChunk()=>Chunk;
 internal void setModified(){}internal Position ToWorldPos()=>localChunkPos;
}
internal sealed class RebirthCamperStationState:TileEntityWorkstation {}
internal sealed class ChunkCustomData
{internal object TriggerWriteDataDelegate;internal bool isSavedToNetwork;internal ulong expiresInWorldTime=ulong.MaxValue;internal byte[] data;}
internal sealed class ChunkData {internal Dictionary<string,ChunkCustomData> dict=new Dictionary<string,ChunkCustomData>();}
internal sealed class Chunk {internal ChunkData ChunkCustomData=new ChunkData();internal bool isModified;}
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
 internal static bool TryDecodeNative(Frame f,out RecipeQueueItem before,out RecipeQueueItem after,out ItemStack[] beforeTools,out ItemStack[] afterTools)
 {before=new RecipeQueueItem();after=new RecipeQueueItem{CraftingTimeLeft=BitConverter.ToSingle(f.AfterRow,0)};beforeTools=afterTools=null;return true;}
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
  artifact=new Prepared{Birth=frame.Birth,Operation=frame.Operation,Frame=frame,OriginalPath=Path.Combine(Root,frame.Operation.ToString("N")+".rew")};
  File.WriteAllBytes(artifact.OriginalPath,RebirthProductiveEarnedRowProof.Encode(frame));return true;
 }
 internal static bool SafePath(string path)=>System.IO.Path.GetFullPath(path).StartsWith(Root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 internal sealed class Prepared
 {
  
  internal CompletionRetention CaptureCompletionRetention(byte[] marker)=>new CompletionRetention(); // Actual retention partial has its own fixture.
  internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal Guid CustodyBirth=>Birth;
  internal string OriginalPath;internal int ImageLength=128;internal object LoadedSource;
  internal RebirthProductiveEarnedRowProof.Frame Frame;
  internal byte[] CopyImage()=>new byte[128];
  internal bool Current()=>true;internal bool MatchesMarker(byte[] bytes)=>bytes.SequenceEqual(Operation.ToByteArray());
  internal byte[] Marker(object frame)=>Operation.ToByteArray();internal void RetainCompleted(){}
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
internal sealed class Lease:IDisposable {internal int Disposals;public void Dispose(){Disposals++;}}
internal sealed class RebirthProductiveEarnedObservationBudget:IDisposable
{
 internal sealed class Observation:IDisposable {public void Dispose(){}}
 internal static RebirthProductiveEarnedObservationBudget TryOpen(RebirthProductiveEarnedArtifact.Prepared a,object f)=>new RebirthProductiveEarnedObservationBudget();
 internal IDisposable TryEnterCallbacks()=>new Lease();internal bool TryReserveAllocation(long n,out IDisposable lease){lease=new Lease();return true;}
 internal static bool TryRetainDetached(long n,out IDisposable lease){lease=new Lease();return true;}
 internal static IDisposable TryEnterDecoder()=>new Lease();
 internal static bool TryMeasureTools(byte[] bytes,out long graph,out long scratch){graph=64;scratch=0;return true;}
 internal Observation TryObserveNative(byte[] body,int offset)=>new Observation();public void Dispose(){}
}
internal enum RemoteResourceSourceKind {WorkstationOutput}
internal static class RemoteResourceIdentity {internal static object Workstation(Position p)=>p;}
internal static class RebirthRemoteResourceNativeBinding
{internal static bool TryResolve(World w,object endpoint,RemoteResourceSourceKind kind,string birth,out object binding){binding=new object();return true;}}
internal static class RebirthRemoteResourceNativePublication
{
 internal sealed class Watch:IDisposable
 {internal static bool ThrowOnce;public void Dispose(){if(ThrowOnce){ThrowOnce=false;throw new Exception("watch cleanup fault");}}}
 internal static Watch BeginWithBudget(object binding,Func<bool> current,RebirthProductiveEarnedObservationBudget memory,string key)=>new Watch();
}
internal static class RebirthProductiveEarnedSavedNativeSource
{
 internal static bool RegisterInvokedPartial(string path,string save,object preserve)=>true;
 internal static bool RegisterInvokedChild(string parent,string child,string save,byte[] image)=>true;
 internal static bool Saved;internal static int Publications;
 internal static bool Publish(RebirthProductiveEarnedArtifact.Prepared artifact,RebirthProductiveEarnedObservationBudget memory,
  RebirthRemoteResourceNativePublication.Watch watch,byte[] body,int offset,bool terminal,Func<bool> current,bool partialSuccessor=false)
 {if(!current())throw new Exception("Wrong publication arm");Publications++;return Saved;}
 internal static void Release(object source){}
 internal static void ReleaseRegisteredTerminal(Guid birth,Guid operation){}
}
internal static class RebirthProductiveEarnedPublication
{
 internal sealed class Observation:IDisposable
 {
  internal bool ThrowDisposeOnce;internal int Disposals;
  internal TileEntityWorkstation Station;internal RebirthProductiveEarnedArtifact.Prepared Artifact;
  internal RebirthProductiveEarnedObservationBudget Memory=new RebirthProductiveEarnedObservationBudget();
  internal RebirthRemoteResourceNativePublication.Watch Placed=new RebirthRemoteResourceNativePublication.Watch();
  internal bool Current()=>RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Artifact.Frame.AfterNative,0)&&
   Artifact.MatchesMarker(Station.Chunk.ChunkCustomData.dict["RebirthProductiveEarnedV1:0:0:0"].data);
  internal bool Saved()=>Current()&&RebirthProductiveEarnedSavedNativeSource.Saved;
  public void Dispose(){if(ThrowDisposeOnce){ThrowDisposeOnce=false;throw new Exception("retryable observation disposal");}Disposals++;}
 }
 internal static Observation BeginAfterNativeWork(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
 {var value=new Observation{Station=station,Artifact=artifact};return value.Current()?value:null;}
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
  internal RebirthProductiveEarnedPublication.Observation Observation;
  internal RecipeQueueItem Row;internal Recipe Recipe;internal int Multiplier;internal float Target;
 }
 internal sealed class NativeTerminal:IDisposable
 {
  internal bool ToolsRebased;internal bool ToolRebaseCurrent()=>ToolsRebased;
  internal object SavedOrigin;
  internal Attempt Original;internal byte[] Body;internal int ElapsedOffset;internal bool Saved=true;
  internal RebirthProductiveEarnedObservationBudget Memory=new RebirthProductiveEarnedObservationBudget();
  internal RebirthProductiveEarnedObservationBudget.Observation Allocation=new RebirthProductiveEarnedObservationBudget.Observation();
  internal RebirthRemoteResourceNativePublication.Watch Watch=new RebirthRemoteResourceNativePublication.Watch();
  internal bool Current()=>Saved&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Body,0)&&
   Original.Artifact.MatchesMarker(Original.Station.Chunk.ChunkCustomData.dict["RebirthProductiveEarnedV1:0:0:0"].data);
  public void Dispose(){}
 }
 private static readonly Dictionary<TileEntityWorkstation,Attempt> retained=new Dictionary<TileEntityWorkstation,Attempt>();
 private static bool marker=true;internal static int Clears;
 internal static bool TryNativeMarker(TileEntityWorkstation station,out byte[] bytes){bytes=station.Chunk.ChunkCustomData.dict["RebirthProductiveEarnedV1:0:0:0"].data;return marker;}
 private static bool ClearExactMarker(TileEntityWorkstation station,byte[] bytes){if(!marker)return false;marker=false;Clears++;return true;}
 internal static Attempt Setup(string path,float before,float target)
 {
  marker=true;Clears=0;retained.Clear();RebirthRecyclerNativeLoop.Running=true;RebirthProductiveToolWork.Capacity=100;TileEntityWorkstation.ThrowWrites=false;
  RebirthRemoteResourceNativePublication.Watch.ThrowOnce=false;
  RebirthProductiveEarnedSavedNativeSource.Saved=false;RebirthProductiveEarnedSavedNativeSource.Publications=0;
  var station=new TileEntityWorkstation{Queue=new[]{new RecipeQueueItem{CraftingTimeLeft=before}}};
  var a=new Attempt{Station=station,Artifact=new RebirthProductiveEarnedArtifact.Prepared{OriginalPath=path},PendingTimerBefore=before,PendingTimerTarget=target};
  station.Chunk.ChunkCustomData.dict["RebirthProductiveEarnedV1:0:0:0"]=new ChunkCustomData{data=a.Artifact.Marker(a.Frame)};
  a.Terminal=new NativeTerminal{Original=a,Body=RebirthProductiveEarnedNativeBody.Body(before,0)};retained.Add(station,a);return a;
 }
 internal static bool Apply(Attempt a)=>TryApplyPartialNativeSuccessor(a);
 internal static bool Finish(Attempt a)=>TryFinishPartialNativeSuccessor(a);
 internal static bool PrepareChild(Attempt a)=>TryPrepareEarnedNativeChild(a);
 internal static bool ApplyChild(Attempt a)=>TryApplyEarnedNativeChild(a);
 internal static bool FinishChild(Attempt a)=>TryFinishEarnedNativeChild(a);
 internal static Attempt Active(TileEntityWorkstation station)=>retained[station];
}
internal static class Program
{
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-earned-child-apply-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   var p=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"normal.rew"),-12,-7);
   Need(RebirthProductiveEarnedCoordinator.PrepareChild(p),"child prepare");var child=p.EarnedChild;Guid operation=child.Frame.Operation;
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.Station.Writes==1&&child.Returned,"native child not applied exactly once while unsaved");
   Need(p.Station.Tools[0].Wear==5&&p.Station.Queue[0].CraftingTimeLeft==-7,"frozen child tool/timer target lost");
   Need(File.Exists(p.Artifact.OriginalPath+".earned-child.invoking"),"native setter without durable invoking record");
   Need(ReferenceEquals(RebirthProductiveEarnedCoordinator.Active(p.Station),p)&&RebirthProductiveEarnedCoordinator.Clears==0,"unsaved child adopted or cleared marker");
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.Station.Writes==1,"warm application repeated native setter");
   RebirthProductiveEarnedSavedNativeSource.Saved=true;
   Need(RebirthProductiveEarnedCoordinator.FinishChild(p)&&p.Station.Writes==1,"saved child not adopted");
   var active=RebirthProductiveEarnedCoordinator.Active(p.Station);
   Need(!ReferenceEquals(active,p)&&active.Artifact.Operation==operation&&active.Observation.Saved()&&RebirthProductiveEarnedCoordinator.Clears==0,"adoption lost exact original/saved observation or cleared child marker");
   p=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"unknown.rew"),3,-7);Need(RebirthProductiveEarnedCoordinator.PrepareChild(p),"unknown prepare");
   string invoking=p.Artifact.OriginalPath+".earned-child.invoking";File.WriteAllBytes(invoking,new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.Station.Writes==0,"unknown invoking record permitted native setter");File.Delete(invoking);
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.Station.Writes==0,"unknown warm attempt revived after missing record");
   p=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"throw.rew"),3,-7);Need(RebirthProductiveEarnedCoordinator.PrepareChild(p),"throw prepare");
   TileEntityWorkstation.ThrowWrites=true;Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.EarnedChild.Entered&&!p.EarnedChild.Returned,"throwing native setter lost unknown state");TileEntityWorkstation.ThrowWrites=false;
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.Station.Writes==1&&!RebirthProductiveEarnedCoordinator.FinishChild(p),"throwing setter replayed/adopted");
   p=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"cleanup.rew"),3,-7);Need(RebirthProductiveEarnedCoordinator.PrepareChild(p),"cleanup prepare");
   RebirthRemoteResourceNativePublication.Watch.ThrowOnce=true;
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.EarnedChild.Returned&&p.Station.Writes==1,"cleanup fault lost returned native work");
   RebirthProductiveEarnedSavedNativeSource.Saved=true;
   Need(RebirthProductiveEarnedCoordinator.FinishChild(p)&&p.Station.Writes==1,"cleanup retry reran native work or failed adoption");
   p=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"foreign-marker.rew"),3,-7);Need(RebirthProductiveEarnedCoordinator.PrepareChild(p),"marker prepare");
   p.Station.Chunk.ChunkCustomData.dict["RebirthProductiveEarnedV1:0:0:0"].data=new byte[]{1};
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.Station.Writes==0,"foreign marker overwritten after prepare");
   p=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,"altered.rew"),3,-7);Need(RebirthProductiveEarnedCoordinator.PrepareChild(p),"altered prepare");RebirthProductiveEarnedCoordinator.ApplyChild(p);
   File.WriteAllBytes(p.Artifact.OriginalPath+".earned-child.invoking",new byte[]{2});RebirthProductiveEarnedSavedNativeSource.Saved=true;
   Need(!RebirthProductiveEarnedCoordinator.FinishChild(p)&&p.Station.Writes==1&&ReferenceEquals(RebirthProductiveEarnedCoordinator.Active(p.Station),p),"changed invoking record permitted adoption");
   var lease=new Lease();var observation=new RebirthProductiveEarnedPublication.Observation{ThrowDisposeOnce=true};
   var pendingNext=new RebirthProductiveEarnedCoordinator.Attempt{Observation=observation};
   var disposing=new RebirthProductiveEarnedCoordinator.NativeEarnedChild{Next=pendingNext,Retention=lease,Image=new byte[]{1}};
   bool threw=false;try{disposing.Dispose();}catch{threw=true;}
   Need(threw&&!disposing.Closed&&ReferenceEquals(disposing.Next,pendingNext)&&disposing.Retention==lease&&lease.Disposals==0,"retryable observation failure lost exact child Next or charged retention");
   disposing.Dispose();Need(disposing.Closed&&disposing.Next==null&&disposing.Image==null&&lease.Disposals==1&&observation.Disposals==1,"child cleanup retry did not release exact original resources");
   disposing.Dispose();Need(lease.Disposals==1&&observation.Disposals==1,"child cleanup repeated after completion");
   Console.WriteLine("PASS extracted child application with native/codec/budget/save doubles; no native gameplay qualification");
  }
  finally{foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}
