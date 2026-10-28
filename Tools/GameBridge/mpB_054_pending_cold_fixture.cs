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
internal static class RebirthProductiveEarnedRowProof
{
 internal const int MaximumFrameBytes=262144;
 internal sealed class Frame
 {
  internal Guid Birth,Operation;internal string World="world",FullRecipeBinding,ExtrasDefinition;
  internal bool HasRecipeDefinition=true;
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
internal static partial class RebirthProductiveEarnedArtifact
{
 internal sealed class CompletionRetention {internal bool TryRetain()=>true;}
 internal static bool FileMatches(string path,byte[] bytes)=>File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes);
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
  internal Prepared(){}
  internal Prepared(World world,TileEntityWorkstation station,string save,string path,byte[] image,Guid birth,Guid operation)
  {OriginalPath=path;Birth=birth;Operation=operation;Frame=RebirthProductiveEarnedSavedNativeSource.Observed.Frame;}
  internal RebirthProductiveEarnedRowProof.Frame ReadFrame()=>Frame;
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
 internal sealed class Evidence
 {
  internal bool ToolsRebased;
  internal string PendingRecipeBinding,PendingExtras;
  internal bool Terminal=true,PendingNextWork=true,PendingTimerAmbiguous,Loaded=true;
  internal string Path;internal byte[] Original,Native,Image;internal int PendingTimerIndex,NativeOffset;internal float PendingTimerBefore,PendingTimerTarget;
  internal Guid CustodyBirth;internal RebirthProductiveEarnedRowProof.Frame Frame;internal TileEntityWorkstation Station;
  internal bool CurrentIdentity(World world,TileEntityWorkstation station,Guid birth,Guid operation)=>Loaded&&
   ReferenceEquals(world,GameManager.Instance.World)&&ReferenceEquals(station,Station)&&birth==Frame.Birth&&operation==Frame.Operation&&
   File.Exists(Path)&&File.ReadAllBytes(Path).SequenceEqual(Image)&&
   station.Chunk.ChunkCustomData.dict["RebirthProductiveEarnedV1:0:0:0"].data.SequenceEqual(Frame.Operation.ToByteArray());
 }
 internal static Evidence Observed;
 internal static bool TryObserved(TileEntityWorkstation station,out Evidence source){source=Observed;return source!=null&&source.Loaded;}
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
 internal sealed class NativePartialSuccessor {}
 internal sealed class NativeEarnedChild {}
 internal static bool Enabled=true;private static void ObserveWorld(){}
 private static void ParkSavedPendingTerminal(NativeTerminal terminal){terminal.Allocation?.Dispose();terminal.Allocation=null;} // Actual phase helper has a separate fixture.
 private static bool MatchesPendingDefinition(Attempt value,RecipeQueueItem row)=>true; // Explicit definition adapter.
 internal sealed class Attempt
 {
  internal bool NativeTickCustody;
  internal bool ClearedNativeMarker,NativeMetadataClosed;internal RebirthProductiveEarnedArtifact.CompletionRetention CompletionRetention;
  internal string PendingRecipeBinding,PendingExtras;internal IDisposable PendingDefinitionRetention;
  internal TileEntityWorkstation Station;internal NativeTerminal Terminal;internal NativePartialSuccessor PartialSuccessor;
  internal bool PendingTimerAmbiguous,PendingTimerObserved=true;internal int PendingTimerIndex;
  internal float PendingTimerBefore,PendingTimerTarget;internal RebirthProductiveEarnedArtifact.Prepared Artifact;
  internal RebirthProductiveEarnedRowProof.Frame Frame=new RebirthProductiveEarnedRowProof.Frame();internal ItemStack[] Tools;internal IDisposable Retention;
  internal NativeEarnedChild EarnedChild;
  internal RebirthProductiveEarnedPublication.Observation Observation;
  internal RecipeQueueItem Row;internal Recipe Recipe;internal int Multiplier;internal float Target;
  internal bool OutputTimerObserved;internal object NativeOutput;
 }
 internal sealed class NativeTerminal:IDisposable
 {
  internal Attempt Original;internal byte[] Body;internal int ElapsedOffset;internal bool Saved=true;
  internal World World;internal bool PendingNextWork;internal RebirthProductiveEarnedSavedNativeSource.Evidence SavedOrigin;
  internal RebirthProductiveEarnedObservationBudget Memory=new RebirthProductiveEarnedObservationBudget();
  internal RebirthProductiveEarnedObservationBudget.Observation Allocation=new RebirthProductiveEarnedObservationBudget.Observation();
  internal RebirthRemoteResourceNativePublication.Watch Watch=new RebirthRemoteResourceNativePublication.Watch();
  internal bool Current()=>Saved&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Original.Station,Body,0)&&
   (SavedOrigin==null||SavedOrigin.CurrentIdentity(World,Original.Station,Original.Artifact.Birth,Original.Artifact.Operation))&&
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
 
 
 
 
 
 internal static Attempt Active(TileEntityWorkstation station)=>retained[station];
 internal static void Forget(TileEntityWorkstation station)=>retained.Remove(station);
 internal static bool Restore(TileEntityWorkstation station)=>TryRestorePendingNativeTerminal(station);
}
internal static class RebirthLogisticsEndpointIdentity
{
 internal static bool Known=true;internal static string Identity=Guid.NewGuid().ToString("N");
 internal static bool TryGetIncarnation(World world,TileEntityWorkstation station,out string identity){identity=Identity;return Known;}
 internal sealed class Admission:IDisposable {internal bool Current()=>false;public void Dispose(){}}
 internal static Admission Begin(World world,TileEntityWorkstation station,Func<bool> current)=>new Admission();
}
internal static class Program
{
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 private static RebirthProductiveEarnedCoordinator.Attempt Prepare(string root,string name)
 {
  var p=RebirthProductiveEarnedCoordinator.Setup(Path.Combine(root,name+".rew"),-12,-7);RebirthProductiveEarnedCoordinator.Forget(p.Station);
  p.Frame.Birth=p.Artifact.Birth;p.Frame.Operation=p.Artifact.Operation;p.Frame.Multiplier=2;
  p.Frame.FullRecipeBinding=RebirthProductiveEarnedUiQueueOwner.Binding(p.Station.Queue[0]);p.Frame.ExtrasDefinition=RebirthRecipeOutputs.DefinitionIdentity(p.Station.Queue[0].Recipe);
  p.Artifact.Frame=p.Frame;byte[] original=p.Artifact.CopyImage(),saved=new byte[200];
  File.WriteAllBytes(p.Artifact.OriginalPath,original);File.WriteAllBytes(p.Artifact.OriginalPath+".terminal-load",saved);
  RebirthProductiveEarnedSavedNativeSource.Observed=new RebirthProductiveEarnedSavedNativeSource.Evidence{Path=p.Artifact.OriginalPath+".terminal-load",
   Original=original,Image=saved,Frame=p.Frame,Station=p.Station,Native=RebirthProductiveEarnedNativeBody.Body(-12,0),PendingTimerBefore=-12,PendingTimerTarget=-7};
  RebirthLogisticsEndpointIdentity.Known=true;return p;
 }
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-pending-cold-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   var p=Prepare(root,"normal");var source=RebirthProductiveEarnedSavedNativeSource.Observed;
   Need(RebirthProductiveEarnedCoordinator.Restore(p.Station),"actual saved pending terminal not restored");
   var restored=RebirthProductiveEarnedCoordinator.Active(p.Station);
   Need(restored.OutputTimerObserved&&restored.NativeOutput==null&&restored.Terminal.SavedOrigin==source&&restored.PendingTimerTarget==-7,"restoration invented old invocation or lost exact pending debt");
   Need(restored.Terminal.Allocation==null,"restored saved pending terminal monopolized observation phase");
   Need(p.Station.Writes==0&&p.Station.Queue[0].CraftingTimeLeft==-12&&RebirthProductiveEarnedCoordinator.Clears==0&&restored.Observation==null,"terminal restore acted as earned/output/setter permit");
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"duplicate retained restoration");
   p=Prepare(root,"unloaded");RebirthProductiveEarnedSavedNativeSource.Observed.Loaded=false;
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"parsed bytes without actual Loaded admitted");
   p=Prepare(root,"attempted");File.WriteAllBytes(p.Artifact.OriginalPath+".partial-successor.invoking",new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station)&&p.Station.Writes==0,"old native prefix replayed after invoking record");
   foreach(string suffix in new[]{".tool-gesture",".tool-gesture.tmp"})
   {p=Prepare(root,"gesture-"+Guid.NewGuid().ToString("N"));File.WriteAllBytes(p.Artifact.OriginalPath+suffix,new byte[]{1});Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station)&&p.Station.Writes==0,"gesture custody bypassed by cold pending restore: "+suffix);}
   p=Prepare(root,"staged-partial");File.WriteAllBytes(p.Artifact.OriginalPath+".partial-successor",new byte[]{1});
   Need(RebirthProductiveEarnedCoordinator.Restore(p.Station)&&p.Station.Writes==0,"staged plan blocked terminal-only restore or granted setters");
   p=Prepare(root,"uncertain-staged-partial");File.WriteAllBytes(p.Artifact.OriginalPath+".partial-successor.tmp",new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"uncertain staged record admitted");
   p=Prepare(root,"staged-child");File.WriteAllBytes(p.Artifact.OriginalPath+".earned-child",new byte[]{1});
   Need(RebirthProductiveEarnedCoordinator.Restore(p.Station)&&p.Station.Writes==0,"staged child blocked terminal-only restore or granted setters");
   p=Prepare(root,"uncertain-staged-child");File.WriteAllBytes(p.Artifact.OriginalPath+".earned-child.tmp",new byte[]{1});
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"uncertain child plan admitted");
   p=Prepare(root,"different-recipe");p.Frame.FullRecipeBinding="other";
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"legacy different recipe substituted current getter for saved definition");
   p=Prepare(root,"saved-cycled-definition");source=RebirthProductiveEarnedSavedNativeSource.Observed;
   source.PendingRecipeBinding=p.Frame.FullRecipeBinding;source.PendingExtras=p.Frame.ExtrasDefinition;
   p.Frame.FullRecipeBinding="predecessor-recipe";p.Frame.ExtrasDefinition="predecessor-extras";
   Need(RebirthProductiveEarnedCoordinator.Restore(p.Station),"qualified saved successor definition replaced by predecessor recipe");
   restored=RebirthProductiveEarnedCoordinator.Active(p.Station);
   Need(restored.PendingRecipeBinding==source.PendingRecipeBinding&&restored.PendingExtras==source.PendingExtras&&p.Station.Writes==0,
    "saved successor definition lost or restoration mutated native state");
   p=Prepare(root,"saved-definition-mismatch");source=RebirthProductiveEarnedSavedNativeSource.Observed;
   source.PendingRecipeBinding="changed-binding";source.PendingExtras=p.Frame.ExtrasDefinition;
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"changed definition fell back to predecessor binding");
   p=Prepare(root,"body");p.Station.Queue[0].CraftingTimeLeft=-11;
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"changed full native body admitted");
   p=Prepare(root,"identity");RebirthLogisticsEndpointIdentity.Known=false;
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"missing native incarnation admitted");
   p=Prepare(root,"outside");RebirthRecyclerNativeLoop.Running=false;
   Need(!RebirthProductiveEarnedCoordinator.Restore(p.Station),"getter outside actual native invocation admitted");
   Console.WriteLine("PASS extracted pending terminal restore and distinct artifact loader; disclosed native/budget/identity doubles");
  }
  finally{foreach(string file in Directory.EnumerateFiles(root))File.Delete(file);Directory.Delete(root);}
 }
}
