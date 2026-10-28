using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
// AUTHORED_NOT_RUN. The complete production Load partial is extracted below.
// Native region/factory/read objects and immutable saved-record admission are
// explicit doubles. This qualifies callback linkage, not native save I/O.
// ACTUAL_EARNED_LOAD_SOURCE
public enum StreamModeRead {Persistency,FromServer}
public enum TileEntityType {Workstation}
public struct LocalPosition {public int x,y,z;}
public class TileEntity {public Chunk Chunk;public LocalPosition localChunkPos;public Chunk GetChunk()=>Chunk;}
public sealed class TileEntityWorkstation:TileEntity {public List<CraftCompleteData> CraftCompleteList=new List<CraftCompleteData>();}
public sealed class CraftCompleteData {}
public sealed class ChunkCustomData {public Dictionary<string,object> dict=new Dictionary<string,object>();}
public sealed class Chunk {public int X,Z;public bool InProgressUnloading;public ChunkCustomData ChunkCustomData=new ChunkCustomData();public TileEntity Tile;public TileEntity GetTileEntity(LocalPosition ignored)=>Tile;}
public sealed class PooledBinaryReader:BinaryReader {public PooledBinaryReader(byte[] bytes):base(new MemoryStream(bytes,false)){} }
public sealed class RegionFileAccessAbstract {}
public sealed class RegionFileChunkReader {public RegionFileAccessAbstract regionFileAccess=new RegionFileAccessAbstract();public PooledBinaryReader loadChunkReader;public Stream loadChunkMemoryStream;}
public sealed class ChunkSnapshotUtil {public RegionFileChunkReader chunkReader=new RegionFileChunkReader();}
public sealed class RegionManager {public ChunkSnapshotUtil snapshotUtil;public RegionFileAccessAbstract regionFileAccess;}
public sealed class ChunkProviderGenerateWorld {public RegionManager m_RegionFileManager=new RegionManager();}
public sealed class Cache {public object ChunkProvider;}
public sealed class World {public Cache ChunkCache=new Cache();public Chunk Chunk;public bool IsRemote()=>false;public Chunk GetChunkSync(int x,int z)=>Chunk;}
public sealed class GameManager {public static GameManager Instance=new GameManager();public World World;}
public static class GameIO {public static string Save=Path.GetFullPath(Path.GetTempPath());public static string GetSaveGameDir()=>Save;}
public static class ThreadManager {public static bool IsMainThread()=>true;}
public static class WorldChunkCache {public static int extractX(long value)=>(int)value;public static int extractZ(long value)=>0;}
public static class RebirthWorkstationCompletionOutbox {public static string CurrentGuid(World w)=>"00000000-0000-0000-0000-000000000001";}
public static class RebirthProductiveEarnedArtifact {public static string WorldIdentity(World w)=>"world-key";}
internal static class RebirthProductiveEarnedObservationBudget
{
 internal static bool Available=true;
 internal static bool TryRetainDetached(long bytes,out IDisposable retained){retained=Available?new Lease():null;return retained!=null;}
 private sealed class Lease:IDisposable {public void Dispose(){}}
}
internal static class RebirthProductiveEarnedCoordinator
{internal static bool Needed;internal static bool NeedsLoadedNativeCleanup(Guid birth,Guid operation)=>Needed;}
internal static class RebirthProductiveEarnedRemoteHost
{internal static bool Needed;internal static bool NeedsRemoteNativeClearCleanup(Guid birth,Guid operation)=>Needed;}
internal static class RebirthProductiveEarnedNativeBody
{internal static byte[] ComparisonDigest(byte[] body,int offset){using(var sha=SHA256.Create())return sha.ComputeHash(body);}}
public static class RebirthStationSnapshotEvidence {public const int MaximumSnapshotBytes=8*1024*1024;}
public static class RebirthStationCompletionOriginalScope {public static readonly StringComparison PathComparison=StringComparison.OrdinalIgnoreCase;}
public static class RebirthProductiveEarnedLoadedNativeSpan
{
 // Explicit span adapter for grouped callback tests. Actual grammar has its own extracted fixture.
 public static bool MatchesStart(Stream stream,byte[] native,int length)
 {long start=stream.Position;try{return stream.ReadByte()==native[0];}finally{stream.Position=start;}}
 public static bool CaptureReturned(Stream stream,long start,byte[] destination,byte[] native,int offset,long graph,long scratch)
 {long end=stream.Position;try{stream.Position=start;for(int i=0;i<native.Length;i++)if(stream.ReadByte()!=native[i])return false;return stream.Position==end;}finally{stream.Position=end;}}
}
public static class RebirthProductiveEarnedUiDeferredTransport
{
 private static readonly Dictionary<TileEntityWorkstation,long> reads=new Dictionary<TileEntityWorkstation,long>();
 public static void ActualNativeRead(TileEntityWorkstation tile){reads.TryGetValue(tile,out long value);reads[tile]=value+1;}
 public static bool TryGetObservedNativeReadFence(TileEntityWorkstation tile,out long value)=>reads.TryGetValue(tile,out value);
 public static bool TryReadNativeObservationFence(World world,TileEntityWorkstation tile,out long value)=>reads.TryGetValue(tile,out value);
}
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
 internal static int Remaining,Disposed;
 private static void MarkStartupRefillNeeded(){} // Separate actual refill fixture.
 internal sealed class Frame {internal string World="world-key";internal Guid Birth=Guid.NewGuid(),Operation=Guid.NewGuid();}
 private static Guid lastBirth,lastOperation;
 internal static bool ObserveProof(out CompletedNativeLoad proof,bool wrongBody=false,bool wrongOriginal=false)
 {
  byte[] digest;using(var sha=SHA256.Create())digest=sha.ComputeHash(wrongOriginal?new byte[]{8}:new byte[]{7});
  return TryObservedCompletedNativeLoad(lastBirth,lastOperation,digest,wrongBody?new byte[]{1,3,5,9}:new byte[]{1,3,5,7},0,out proof);
 }
 internal sealed class Evidence
 {
  internal bool Active=true;internal string WorldGuid="00000000-0000-0000-0000-000000000001",MarkerKey="marker";internal object NativeMarker;internal Guid CustodyBirth;internal Frame Frame=new Frame();internal CachedLoadRequest Load;
  internal byte[] Native,Original=new byte[]{7};internal int NativeOffset;
  internal bool Terminal,PendingNextWork;
  internal void Dispose(){Active=false;Disposed++;}
  internal bool PrimaryCurrent()=>Active;
  internal bool Superseded;
  internal bool CanEnterNativeLoad()=>Active&&!Superseded;
  internal bool MarkerBytesMatch(Chunk chunk)=>chunk.ChunkCustomData.dict.ContainsKey(MarkerKey);
  internal bool MarkerMatches(Chunk chunk)=>MarkerBytesMatch(chunk)&&ReferenceEquals(chunk.ChunkCustomData.dict[MarkerKey],NativeMarker);
  internal IDisposable RetainNativeLoad()=>new Lease();private sealed class Lease:IDisposable {public void Dispose(){}}
 }
 internal static bool Exercise(bool readRan,bool factoryRan,bool wrongReader,bool marker,out World world,out CachedLoaded loaded,bool supersedeWhileLoading=false,int rival=0,bool factoryError=false,int cleanup=0,bool needCleanup=false,bool wrongChunk=false)
 {
  lock(cachedLoadGate){ResetCompletedNativeLoads();cachedLoadRequests.Clear();}Disposed=0;RebirthProductiveEarnedCoordinator.Needed=needCleanup;
  var bytes=new byte[]{1,3,5,7};var reader=new PooledBinaryReader(bytes);var util=new ChunkSnapshotUtil();util.chunkReader.loadChunkReader=reader;util.chunkReader.loadChunkMemoryStream=reader.BaseStream;
  var chunk=new Chunk{X=wrongChunk?1:0};var tile=new TileEntityWorkstation{Chunk=chunk};chunk.Tile=tile;if(marker)chunk.ChunkCustomData.dict["marker"]=new object();
  world=new World{Chunk=chunk};var provider=new ChunkProviderGenerateWorld();provider.m_RegionFileManager.snapshotUtil=util;provider.m_RegionFileManager.regionFileAccess=util.chunkReader.regionFileAccess;world.ChunkCache.ChunkProvider=provider;GameManager.Instance.World=world;
  var evidence=new Evidence();var request=new CachedLoadRequest{Custody=evidence,Directory=GameIO.Save,X=0,Z=0,Snapshot=(byte[])bytes.Clone(),Tile=(byte[])bytes.Clone(),Offset=0};evidence.Load=request;cachedLoadRequests.Add(request);
  lastBirth=evidence.Frame.Birth;lastOperation=evidence.Frame.Operation;
  evidence.Terminal=cleanup>0;evidence.PendingNextWork=cleanup==2;
  if(cleanup>0&&cleanup!=2){request.NormalizedTerminal=true;evidence.Native=(byte[])bytes.Clone();request.Tile=new byte[bytes.Length];}
  if(cleanup==3){request.PredictedNative=true;evidence.Native=new byte[]{1,3,5,9};request.Tile=new byte[4];}
  CachedLoadRequest other=null;
  if(rival!=0)
  {
   request.PredictedNative=true;evidence.Native=(byte[])bytes.Clone();request.Tile=new byte[bytes.Length];
   var otherEvidence=new Evidence{MarkerKey=rival==1||rival>=6?"different-marker":"marker",Native=rival==3?new byte[]{1,3,5,9}:(byte[])bytes.Clone(),Superseded=rival==5};
   other=new CachedLoadRequest{Custody=otherEvidence,Directory=GameIO.Save,X=0,Z=0,Tile=new byte[rival==4?5:4],PredictedNative=true};otherEvidence.Load=other;cachedLoadRequests.Add(other);
   if(rival>=6)for(int i=2;i<(rival==6?4:5);i++)
   {
    var extra=new Evidence{MarkerKey="different-marker-"+i,Native=(byte[])bytes.Clone()};
    var extraRequest=new CachedLoadRequest{Custody=extra,Directory=GameIO.Save,X=0,Z=0,Tile=new byte[4],PredictedNative=true};extra.Load=extraRequest;cachedLoadRequests.Add(extraRequest);
   }
  }
  // Equal current objects before a real native load cannot issue Loaded.
  if(request.Loaded!=null)throw new Exception("Getter issued load proof");
  object load=EnterCachedNativeLoad(util,GameIO.Save,0,true);
  ObserveCachedNativeLoadStream(util.chunkReader,reader,GameIO.Save,0,0,"7rg",47,true);
  object instantiate=EnterCachedNativeInstantiate(reader,StreamModeRead.Persistency,TileEntityType.Workstation,chunk,factoryRan);
  var actualReader=wrongReader?new PooledBinaryReader(bytes):reader;
  object read=EnterCachedPersistencyRead(tile,actualReader,StreamModeRead.Persistency,readRan);
  if(readRan){actualReader.ReadBytes(bytes.Length);RebirthProductiveEarnedUiDeferredTransport.ActualNativeRead(tile);}
  ObserveCachedPersistencyRead(actualReader,readRan,read);
  ObserveCachedNativeInstantiate(tile,reader,chunk,factoryRan,instantiate);EndCachedNativeInstantiate(instantiate,factoryError?new Exception("native factory failed"):null);
  evidence.Superseded=supersedeWhileLoading;
  ObserveCachedNativeLoad(chunk,true,load);loaded=request.Loaded;
  if(other?.Loaded!=null)throw new Exception("rival candidate adopted native source");
  EndCachedNativeLoad(load);
  Remaining=cachedLoadRequests.Count;
  return loaded?.Installed(world)==true;
 }
}
internal static class Program
{
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 public static void Main()
 {
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out var supersededWorld,out var supersededLoad,true),"in-flight earned load survived terminal supersession");
  Need(RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out var world,out var loaded),"actual complete load/read/factory not linked");
  RebirthProductiveEarnedUiDeferredTransport.ActualNativeRead(loaded.Tile);Need(!loaded.Installed(world),"later equal native read adopted old earned source");
  Need(RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded),"second actual source");world.ChunkCache.ChunkProvider=new ChunkProviderGenerateWorld();Need(!loaded.Installed(world),"provider replacement adopted original");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(false,true,false,true,out world,out loaded),"skipped native read issued source");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,false,false,true,out world,out loaded),"skipped native factory issued source");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,true,true,out world,out loaded),"equal wrong stream issued source");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded),"missing saved original marker issued source");
  Need(RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,false,1),"shared location header prevented actual saved-marker selection");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,false,2),"ambiguous full body and marker chose an arbitrary operation");
  Need(RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,false,3),"returned full body did not distinguish same-header candidates");
  Need(RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,false,4),"returned span length did not distinguish candidate");
  Need(RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,false,5),"superseded rival blocked eligible source");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,false,1,true),"factory exception left grouped candidate installed");
  Need(RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,false,6),"bounded four-candidate group did not select saved marker");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,false,7),"over-capacity group admitted a source");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:1)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==0&&RebirthProductiveEarnedSavedNativeSource.Disposed==1,"actual loaded completed marker absence did not release lookup custody");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:2)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1&&RebirthProductiveEarnedSavedNativeSource.Disposed==0,"pending obligation retired from marker absence");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1,"earned original retired from marker absence");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:3)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1,"different native body retired terminal lookup");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(false,true,false,false,out world,out loaded,cleanup:1)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1,"skipped native read retired terminal lookup");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,true,false,out world,out loaded,cleanup:1)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1,"wrong reader retired terminal lookup");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,factoryError:true,cleanup:1)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1,"factory exception retired terminal lookup");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,wrongChunk:true),"same native body in different chunk issued source");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:1,needCleanup:true,wrongChunk:true)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1&&!RebirthProductiveEarnedSavedNativeSource.ObserveProof(out _),"different chunk retired original or issued cleanup receipt");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,supersedeWhileLoading:true,cleanup:1)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1,"uncertain/superseded terminal retired lookup");
  Need(RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,true,out world,out loaded,cleanup:1)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1&&RebirthProductiveEarnedSavedNativeSource.Disposed==0,"present terminal marker lost source qualification");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,rival:2,cleanup:1)&&
   RebirthProductiveEarnedSavedNativeSource.Remaining==1&&RebirthProductiveEarnedSavedNativeSource.Disposed==1,"completed and earned matching histories were retired together");
  Need(!RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:1,needCleanup:true),"cleanup source unexpectedly retired without its receipt owner");
  Need(RebirthProductiveEarnedSavedNativeSource.ObserveProof(out var completion)&&completion.Current(),"actual completed load did not issue metadata-only receipt");
  Need(!RebirthProductiveEarnedSavedNativeSource.ObserveProof(out _,true),"completion receipt accepted different native body");
  Need(!RebirthProductiveEarnedSavedNativeSource.ObserveProof(out _,wrongOriginal:true),"same operation/body adopted different immutable original");
  completion.Dispose();Need(!RebirthProductiveEarnedSavedNativeSource.ObserveProof(out _),"disposed completion receipt remained available");
  RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:1,needCleanup:true);
  Need(RebirthProductiveEarnedSavedNativeSource.ObserveProof(out completion),"scope receipt setup");world.ChunkCache.ChunkProvider=new ChunkProviderGenerateWorld();Need(!completion.Current(),"changed native manager retained receipt");
  RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:1,needCleanup:true);
  Need(RebirthProductiveEarnedSavedNativeSource.ObserveProof(out completion),"world receipt setup");GameManager.Instance.World=new World();Need(!completion.Current(),"replacement World adopted receipt");
  RebirthProductiveEarnedObservationBudget.Available=false;
  RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:1,needCleanup:true);
  Need(RebirthProductiveEarnedSavedNativeSource.Remaining==1&&!RebirthProductiveEarnedSavedNativeSource.ObserveProof(out _),"receipt budget failure lost retained registration");
  RebirthProductiveEarnedSavedNativeSource.Exercise(true,true,false,false,out world,out loaded,cleanup:1);
  Need(RebirthProductiveEarnedSavedNativeSource.Remaining==0,"history without a retained attempt consumed receipt capacity");
  RebirthProductiveEarnedObservationBudget.Available=true;
  Console.WriteLine("PASS extracted ordinary-earned actual load callback linkage; disclosed native/record doubles; not game qualification");
 }
}
