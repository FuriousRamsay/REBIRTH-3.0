using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
// AUTHORED_NOT_RUN. Complete production invoked-child candidate partial.
// Parent REL2 admission and native frame/codec/budget are explicit doubles.
// Actual native loading and saved item mutation are not simulated here.
// ACTUAL_CHILD_COLD_SOURCE
internal sealed class Lease:IDisposable {public void Dispose(){}}
internal static class ThreadManager {internal static bool IsMainThread()=>true;}
internal sealed class Position {internal int x=7,y=8,z=9;}
internal sealed class Chunk {internal int X=1,Z=2;}
internal class TileEntityWorkstation
{internal Position localChunkPos=new Position();internal Chunk Chunk=new Chunk();internal Chunk GetChunk()=>Chunk;internal Position ToWorldPos()=>localChunkPos;}
internal sealed class RebirthCamperStationState:TileEntityWorkstation {}
internal sealed class World {internal string Guid;internal TileEntityWorkstation Tile;internal bool IsRemote()=>false;internal TileEntityWorkstation GetTileEntity(Position ignored)=>Tile;}
internal sealed class GameManager {internal static GameManager Instance=new GameManager();internal World World;}
internal static class RebirthWorkstationCompletionOutbox {internal static string CurrentGuid(World world)=>world.Guid;}
internal static class RebirthStationCompletionOriginalScope {internal static readonly StringComparison PathComparison=StringComparison.OrdinalIgnoreCase;}
internal static class RebirthProductiveEarnedObservationBudget
{
 internal static bool Resize=true;internal static IDisposable TryEnterDecoder()=>new Lease();internal static IDisposable TryEnterCandidateDecoder()=>new Lease();
 internal static bool TryRetainDetached(long count,out IDisposable retained){retained=new Lease();return true;}
 internal static bool TryResizeDetached(IDisposable retained,long count)=>Resize;
}
internal sealed class RecipeQueueItem {internal float CraftingTimeLeft,OneItemCraftTime=5;}
internal static class RebirthProductiveEarnedRowProof
{
 internal const int MaximumArtifactBytes=6*1024*1024;
 internal const int MaximumFrameBytes=262144;
 internal sealed class Frame
 {
  internal string FullRecipeBinding,ExtrasDefinition;
  internal bool HasNativeBodies=true,HasRecipeDefinition=true;internal Guid Birth,Operation;internal string World="world";
  internal int RowIndex;internal float WorkedSeconds=5;internal byte[] BeforeNative,AfterNative;internal int BeforeElapsedOffset,AfterElapsedOffset;
 }
 internal static Frame Decoded;
 internal static Frame Decode(byte[] image)=>Decoded;
 internal static bool TryDecodeNative(Frame frame,out RecipeQueueItem before,out RecipeQueueItem after,out object beforeTools,out object afterTools)
 {before=new RecipeQueueItem{CraftingTimeLeft=3};after=new RecipeQueueItem{CraftingTimeLeft=-2};beforeTools=afterTools=null;return true;}
 internal static byte[] Marker(Frame frame,byte[] image){using(var sha=SHA256.Create())return sha.ComputeHash(image);}
}
internal static class RebirthProductiveEarnedNativeBody
{
 internal const int MaximumBodyBytes=2097088;internal static bool NativeMatch=true;internal static Position LoadedPosition=new Position();
 internal sealed class NativeImage {internal Position LocalPosition;}
 internal static bool TryRead(byte[] native,int offset,out NativeImage image,out ulong elapsed){image=new NativeImage{LocalPosition=LoadedPosition};elapsed=0;return true;}
 internal static bool MatchesCurrent(TileEntityWorkstation station,byte[] native,int offset)=>NativeMatch;
 internal static byte[] ComparisonDigest(byte[] bytes,int offset){using(var sha=SHA256.Create())return sha.ComputeHash(bytes);}
 internal static bool SameExceptElapsed(byte[] a,int ao,byte[] b,int bo)=>a.SequenceEqual(b);
 internal static bool TryMeasure(byte[] bytes,int offset,out long graph,out long scratch){graph=256;scratch=64;return true;}
}
internal static partial class RebirthProductiveEarnedCoordinator
{
 internal static bool HasMarker;internal static bool TryNativeMarker(TileEntityWorkstation station,out byte[] marker){marker=new byte[]{1};return HasMarker;}
 internal static bool TryEarnedNativeChildWork(float before,float target,float unit,out float work,out float normalized)
 {work=unit;normalized=target;return before==-12&&target==-7&&unit==5;}
}
internal static class RebirthProductiveEarnedArtifact
{
 internal static string Root;
 internal static bool SafePath(string path)=>Path.GetFullPath(path).StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
 internal sealed class Prepared
 {
  internal string OriginalPath;internal byte[] Image;internal bool Current()=>File.Exists(OriginalPath)&&File.ReadAllBytes(OriginalPath).SequenceEqual(Image);
  internal byte[] CopyImage()=>(byte[])Image.Clone();
 }
}
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
 private static readonly object cachedLoadGate=new object();private static readonly List<CachedLoadRequest> cachedLoadRequests=new List<CachedLoadRequest>();
 private const int MaximumRecord=12*1024*1024;
 internal sealed class Evidence:IDisposable
 {
  internal bool ToolsRebased;
  internal byte[] StagedChildLink;
  internal byte[] StagedPartialRecord;
  internal string PendingRecipeBinding,PendingExtras;
  internal Evidence(IDisposable retained){}
  internal string Path,Directory,WorldGuid,MarkerKey;internal bool Terminal,PendingNextWork,PendingTimerAmbiguous,Active=true;
  internal byte[] Image,Original,Native,Marker;internal int NativeOffset,PendingTimerIndex;internal float PendingTimerBefore,PendingTimerTarget;
  internal RebirthProductiveEarnedRowProof.Frame Frame;internal NativeChildDependency[] Dependencies;internal CachedLoadRequest Load;
  internal bool PrimaryCurrent()=>Active&&File.Exists(Path)&&File.ReadAllBytes(Path).SequenceEqual(Image)&&NativeChildDependenciesCurrent(Dependencies);
  internal bool CanEnterNativeLoad()=>PrimaryCurrent();
  public void Dispose(){Active=false;}
 }
 internal sealed class CachedLoadRequest
 {
  internal Evidence Custody;internal string Directory,StartupOrigin;internal int X,Z;internal byte[] Snapshot,Tile;internal bool PredictedNative;
  internal long MaximumGraph,MaximumScratch;internal object Loaded;
 }
 private static byte[] Read(string path,int maximum)
 {if(!RebirthProductiveEarnedArtifact.SafePath(path))throw new Exception("scope");var bytes=File.ReadAllBytes(path);if(bytes.Length<1||bytes.Length>maximum)throw new Exception("bound");return bytes;}
 internal static bool ReadChild(Evidence parent,out Evidence child)=>TryReadInvokedNativeChild(parent,out child);
 internal static Evidence First=>cachedLoadRequests[0].Custody;
 internal static int Count=>cachedLoadRequests.Count;
 internal static void Clear(){foreach(var request in cachedLoadRequests)request.Custody.Dispose();cachedLoadRequests.Clear();}
}
internal static class Program
{
 private static void Need(bool value,string text){if(!value)throw new Exception(text);}
 public static void Main()
 {
  string root=Path.Combine(Path.GetTempPath(),"rb-original-cold-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);RebirthProductiveEarnedArtifact.Root=root;
  try
  {
   Guid birth=Guid.NewGuid(),op=Guid.NewGuid(),guid=Guid.NewGuid();
   string folder=Path.Combine(root,"RebirthProductiveEarned",birth.ToString("N"));Directory.CreateDirectory(folder);
   string path=Path.Combine(folder,op.ToString("N")+".rew");var bytes=Enumerable.Repeat((byte)4,100).ToArray();File.WriteAllBytes(path,bytes);
   string world;
   using(var sha=SHA256.Create())world=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(guid.ToString("N"))));
   var frame=new RebirthProductiveEarnedRowProof.Frame{Birth=birth,Operation=op,World=world,WorkedSeconds=3,BeforeNative=new byte[64],AfterNative=new byte[64]};
   RebirthProductiveEarnedRowProof.Decoded=frame;
   var station=new TileEntityWorkstation();GameManager.Instance.World=new World{Guid=guid.ToString("D"),Tile=station};
   var artifact=new RebirthProductiveEarnedArtifact.Prepared{OriginalPath=path,Image=bytes};
   Need(RebirthProductiveEarnedSavedNativeSource.PublishOriginalLocation(artifact,station,frame),"actual preparation location not published");
   Need(new FileInfo(path+".native-location").Length==105,"location wire framing changed");
   Need(RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(path,root)&&RebirthProductiveEarnedSavedNativeSource.Count==1,"initial original not registered");
   var evidence=RebirthProductiveEarnedSavedNativeSource.First;
   Need(evidence.Load.Loaded==null&&evidence.Load.PredictedNative&&evidence.Load.Tile.Length==74&&evidence.WorldGuid==guid.ToString("D"),"locator fabricated loaded evidence or wrong native span");
   Need(RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(path,root)&&RebirthProductiveEarnedSavedNativeSource.Count==1,"initial registration duplicated");
   byte[] location=File.ReadAllBytes(path+".native-location");File.WriteAllBytes(path+".native-location",new byte[]{0});
   Need(!evidence.PrimaryCurrent(),"changed location dependency still current");File.WriteAllBytes(path+".native-location",location);
   RebirthProductiveEarnedSavedNativeSource.Clear();
   location[4]=1;File.WriteAllBytes(path+".native-location",location);
   Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(path,root),"child original bypassed predecessor protocol");location[4]=0;File.WriteAllBytes(path+".native-location",location);
   frame.World="wrong-world";Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(path,root),"foreign world hash accepted");frame.World=world;
   frame.WorkedSeconds=2;Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(path,root),"incomplete original work accepted");frame.WorkedSeconds=3;
   RebirthProductiveEarnedNativeBody.LoadedPosition=new Position{x=99};Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(path,root),"wrong original native position accepted");RebirthProductiveEarnedNativeBody.LoadedPosition=new Position();
   File.WriteAllBytes(path+".native-location.tmp",location);Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(path,root),"unknown locator candidate accepted");File.Delete(path+".native-location.tmp");
   bytes[0]=99;File.WriteAllBytes(path,bytes);Need(!RebirthProductiveEarnedSavedNativeSource.RegisterPreparedOriginal(path,root),"changed immutable original accepted");
   Console.WriteLine("PASS extracted initial locator/candidate with disclosed native codec/world/budget doubles");
  }
  finally
  {
   foreach(string file in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))File.Delete(file);
   foreach(string directory in Directory.EnumerateDirectories(root,"*",SearchOption.AllDirectories).OrderByDescending(value=>value.Length))Directory.Delete(directory);
   Directory.Delete(root);
  }
 }
}
