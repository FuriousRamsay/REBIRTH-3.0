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
 {before=new RecipeQueueItem();after=new RecipeQueueItem{CraftingTimeLeft=BitConverter.ToSingle(f.AfterRow,0)};beforeTools=new[]{new ItemStack{Wear=BitConverter.ToInt32(f.BeforeTools,0)}};afterTools=new[]{new ItemStack{Wear=BitConverter.ToInt32(f.AfterTools,0)}};return true;}
}
internal static class RebirthNativeItemConformanceReader
{
 internal static bool TryEncodeStackArrayV1(ItemStack[] tools,int binary,int text,out string value)
 {value=Convert.ToBase64String(BitConverter.GetBytes(tools[0].Wear));return true;}
}
internal static partial class RebirthProductiveEarnedArtifact
{
 internal sealed class CompletionRetention {internal bool TryRetain()=>true;}
 internal static string Root;
 internal static bool NativeAllowed=true;internal static Guid ExpectedCustody;
 internal static string WorldIdentity(World world)=>"world";
 private static bool NativeCurrent(World world,TileEntityWorkstation station,Guid birth)=>NativeAllowed&&birth==ExpectedCustody;
 private static bool FileMatches(string path,byte[] bytes)=>File.ReadAllBytes(path).SequenceEqual(bytes);
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
  internal Prepared(){}
  internal Prepared(World world,TileEntityWorkstation station,string save,string path,byte[] image,Guid birth,Guid operation,Guid rebound){Birth=birth;Operation=operation;Rebound=rebound;OriginalPath=path;Frame=RebirthProductiveEarnedSavedNativeSource.Plan.Frame;}
  internal bool IsTarget(TileEntityWorkstation station)=>true;
  internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();internal Guid Rebound;internal Guid CustodyBirth=>LoadedSource?.CustodyBirth??(Rebound==Guid.Empty?Birth:Rebound);
  internal string OriginalPath;internal int ImageLength=128;internal RebirthProductiveEarnedSavedNativeSource.Evidence LoadedSource;
  internal RebirthProductiveEarnedRowProof.Frame Frame;
  internal byte[] CopyImage()=>new byte[128];
  internal bool Current()=>NativeAllowed;internal bool MatchesMarker(byte[] bytes)=>bytes.SequenceEqual(Operation.ToByteArray());
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
internal sealed class Lease:IDisposable {public void Dispose(){}}
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
 internal sealed class Evidence:IDisposable {internal byte[] Native,Image,StagedChildLink;internal int NativeOffset,PendingTimerIndex;internal bool Terminal=true,PendingNextWork=true,Valid=true;internal Guid CustodyBirth;internal string Path;internal TileEntityWorkstation Station;internal RebirthProductiveEarnedRowProof.Frame Frame;internal CachedLoad Load;internal bool PrimaryCurrent()=>Valid&&(Path==null||File.ReadAllBytes(Path).SequenceEqual(Image));internal bool CurrentIdentity(World world,TileEntityWorkstation station,Guid birth,Guid operation)=>Valid&&ReferenceEquals(Station,station);internal IDisposable RetainNativeLoad()=>new Lease();public void Dispose(){}}
 internal sealed class CachedLoad {internal object Loaded;internal bool PredictedNative=true;internal Evidence Custody;}
 internal static Evidence Plan;
 internal static bool TryReadStagedNativeChild(Evidence parent,Func<IDisposable> callbacks,out Evidence plan){using(var scope=callbacks()){plan=Plan;return scope!=null&&plan!=null;}} // Actual parser has a separate fixture.

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
  internal TileEntityWorkstation Station;internal RebirthProductiveEarnedArtifact.Prepared Artifact;
  internal RebirthProductiveEarnedObservationBudget Memory=new RebirthProductiveEarnedObservationBudget();
  internal RebirthRemoteResourceNativePublication.Watch Placed=new RebirthRemoteResourceNativePublication.Watch();
  internal bool Current()=>RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Artifact.Frame.AfterNative,0)&&
   Artifact.MatchesMarker(Station.Chunk.ChunkCustomData.dict["RebirthProductiveEarnedV1:0:0:0"].data);
  internal bool Saved()=>Current()&&RebirthProductiveEarnedSavedNativeSource.Saved;
  public void Dispose(){}
 }
 internal static Observation BeginAfterNativeWork(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
 {var value=new Observation{Station=station,Artifact=artifact};return value.Current()?value:null;}
}
internal static partial class RebirthProductiveEarnedCoordinator
{
 private static bool TryApplyStagedNativePartial(Attempt value)=>false; // Staged recovery has a separate extracted fixture.
 
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
  internal RebirthProductiveEarnedSavedNativeSource.Evidence SavedOrigin;
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
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthProductiveEarnedCoordinator.Attempt Setup(string root,string name)
 {
  RebirthProductiveEarnedArtifact.NativeAllowed=true;
  var p=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,name+".rew"),-12,-7);p.PendingRecipeBinding="recipe";p.PendingExtras="recipe-extras";
  Need(RebirthProductiveEarnedCoordinator.PrepareChild(p),"warm source plan preparation");var child=p.EarnedChild;
  var plan=new RebirthProductiveEarnedSavedNativeSource.Evidence{Path=child.Artifact.OriginalPath,Image=child.Image,Frame=child.Frame,StagedChildLink=child.Link,
   Native=child.Frame.AfterNative,NativeOffset=child.Frame.AfterElapsedOffset};
  plan.Load=new RebirthProductiveEarnedSavedNativeSource.CachedLoad{Custody=plan};
  var source=new RebirthProductiveEarnedSavedNativeSource.Evidence{Station=p.Station,Native=p.Terminal.Body,Frame=p.Frame,CustodyBirth=Guid.NewGuid()};
  p.Terminal.SavedOrigin=source;p.Artifact.LoadedSource=source;RebirthProductiveEarnedArtifact.ExpectedCustody=source.CustodyBirth;
  RebirthProductiveEarnedSavedNativeSource.Plan=plan;p.EarnedChild=null;child.Dispose();return p;
 }
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-staged-child-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   var p=Setup(root,"normal");var plan=RebirthProductiveEarnedSavedNativeSource.Plan;int prepares=RebirthProductiveEarnedArtifact.Prepares;
   Need(RebirthProductiveEarnedCoordinator.PrepareChild(p)&&p.Station.Writes==0,"staged recovery mutated native state or failed");
   Need(RebirthProductiveEarnedArtifact.Prepares==prepares&&p.EarnedChild.Frame.Operation==plan.Frame.Operation&&p.EarnedChild.Frame.Birth==plan.Frame.Birth,
    "staged recovery replaced historical child identity");
   Need(p.EarnedChild.Artifact.CustodyBirth==RebirthProductiveEarnedArtifact.ExpectedCustody&&p.EarnedChild.Artifact.Birth!=p.EarnedChild.Artifact.CustodyBirth,"historical birth substituted for current native incarnation");
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.EarnedChild.Returned&&p.Station.Writes==1,"first staged application did not retain unsaved child");
   Need(!RebirthProductiveEarnedCoordinator.ApplyChild(p)&&p.Station.Writes==1,"staged warm application replayed");
   RebirthProductiveEarnedSavedNativeSource.Saved=true;Need(RebirthProductiveEarnedCoordinator.FinishChild(p)&&p.Station.Writes==1,"saved staged child not adopted");
   p=Setup(root,"changed-definition");RebirthProductiveEarnedSavedNativeSource.Plan.Frame.FullRecipeBinding="changed";
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(p)&&p.Station.Writes==0,"changed staged child definition accepted");
   p=Setup(root,"wrong-wear");RebirthProductiveEarnedSavedNativeSource.Plan.Frame.AfterTools=BitConverter.GetBytes(1);
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(p)&&p.Station.Writes==0,"unpaid child wear accepted");
   p=Setup(root,"wrong-incarnation");RebirthProductiveEarnedArtifact.ExpectedCustody=Guid.NewGuid();
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(p)&&p.Station.Writes==0,"different native incarnation rebound child");
   p=Setup(root,"missing-loaded-source");p.Artifact.LoadedSource=null;
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(p)&&p.Station.Writes==0,"unqualified predecessor rebound child");
   p=Setup(root,"entered");File.WriteAllBytes(p.Artifact.OriginalPath+".earned-child.invoking",new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(p)&&p.Station.Writes==0,"entered child reauthorized");
   p=Setup(root,"uncertain");File.WriteAllBytes(p.Artifact.OriginalPath+".earned-child.invoking.tmp",new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(p)&&p.Station.Writes==0,"uncertain invocation reauthorized");
   p=Setup(root,"outside");RebirthRecyclerNativeLoop.Running=false;
   Need(!RebirthProductiveEarnedCoordinator.PrepareChild(p)&&p.Station.Writes==0,"child rebound outside native invocation");
   Console.WriteLine("PASS extracted staged child restoration/binding and existing native application; disclosed native/parser/artifact/budget adapters");
  }
  finally{foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}
