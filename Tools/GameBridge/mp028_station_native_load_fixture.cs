// AUTHORED_NOT_RUN. Actual complete StationNativeLoad. Native region/factory/
// Read execution, schema span comparison/body/fence/incarnation are adapters.
using System;using System.IO;using System.Linq;using System.Collections.Generic;
struct Vector3i{internal int x,y,z;internal Vector3i(int a,int b,int c){x=a;y=b;z=c;}public static bool operator==(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator!=(Vector3i a,Vector3i b)=>!(a==b);public override bool Equals(object other)=>other is Vector3i v&&v==this;public override int GetHashCode()=>x^y^z;}
enum StreamModeRead{Persistency,FromClient}enum TileEntityType{Workstation}
class PooledBinaryReader{internal Stream BaseStream;}
class RegionFileAccessAbstract{}
class RegionFileChunkReader{internal RegionFileAccessAbstract regionFileAccess;internal PooledBinaryReader loadChunkReader;internal Stream loadChunkMemoryStream;}
class ChunkSnapshotUtil{internal RegionFileChunkReader chunkReader;}
class Manager{internal ChunkSnapshotUtil snapshotUtil;internal RegionFileAccessAbstract regionFileAccess;}
class ChunkProviderGenerateWorld{internal Manager m_RegionFileManager;}
class Cache{internal object ChunkProvider;}
class Chunk{internal int X=1,Z=2;internal bool InProgressUnloading;internal TileEntityWorkstation Tile;internal object GetTileEntity(object local)=>Tile;}
class TileEntity{internal Chunk Chunk;internal Chunk GetChunk()=>Chunk;}
class TileEntityWorkstation:TileEntity{internal object localChunkPos=new object();internal Vector3i Position=new Vector3i(17,3,33);internal Vector3i ToWorldPos()=>Position;internal string Birth="birth";internal long Fence=9;internal byte[] Body;}
class World{internal Cache ChunkCache=new Cache();internal Chunk Chunk;internal bool IsRemote()=>false;internal object GetChunkSync(int x,int z)=>Chunk;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class GameIO{internal static string Root;internal static string GetSaveGameDir()=>Root;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class RebirthCookingMillingPublication{internal class StaticSourcePurpose{}}
static class RebirthWorkstationCompletionPipeline{internal class RemoteOutputHostSource{}}
// Purpose attachment is not a native-load issuer in this isolated cohort.
static partial class RebirthProductiveEarnedUiDeferredTransport{internal class AuthorityLocalLoadedSource{}}
static class WorldChunkCache{internal static int extractX(long key)=>1;internal static int extractZ(long key)=>2;}
static class RebirthStationSnapshotEvidence{internal const int MaximumSnapshotBytes=1024;}
static class RebirthLogisticsEndpointIdentity{internal static bool TryGetIncarnation(World world,TileEntityWorkstation tile,out string birth){birth=tile.Birth;return true;}}
static partial class RebirthProductiveEarnedUiDeferredTransport{internal static bool TryReadNativeObservationFence(World world,TileEntityWorkstation tile,out long fence){fence=tile.Fence;return true;}internal static bool TryGetObservedNativeReadFence(TileEntityWorkstation tile,out long fence){fence=tile.Fence;return true;}}
static class RebirthProductiveEarnedNativeBody{internal static bool MatchesCurrent(TileEntityWorkstation tile,byte[] body,int elapsed)=>tile.Body.SequenceEqual(body);}
static class RebirthProductiveEarnedLoadedNativeSpan{
 internal static bool MatchesStart(Stream stream,byte[] native,int length)=>stream.Position==0&&stream.Length==length;
 internal static bool CaptureReturned(Stream stream,long start,byte[] destination,byte[] body,int elapsed,long graph,long scratch){long end=stream.Position;stream.Position=start;int n=stream.Read(destination,0,destination.Length);stream.Position=end;return n==destination.Length&&destination.Skip(10).SequenceEqual(body);}}
// ACTUAL_NATIVE_LOAD
public static partial class RebirthRecipeOutputIntegration{
 private class UiOutputLoadedOwner{internal bool Closed;}
 private class UiOutputOriginal{}
 private class Hold{internal Guid Operation;internal UiOutputLoadedOwner Owner;}
 private static readonly List<Hold> stationOutputColdHolds=new List<Hold>();
 static StationOutputNativeRequest request;static ChunkSnapshotUtil util;static RegionFileChunkReader region;static PooledBinaryReader reader;static Chunk chunk;static TileEntityWorkstation tile;static int checks;
 static void Need(bool value,string name){checks++;if(!value)throw new Exception(name);}
 static void Fresh(){foreach(var previous in stationNativeLoadRequests)previous.Dispose();stationNativeLoadRequests.Clear();stationNativeLoadingBatch=null;stationNativeLoading=null;GameIO.Root=Path.GetFullPath(".");var access=new RegionFileAccessAbstract();region=new RegionFileChunkReader{regionFileAccess=access};util=new ChunkSnapshotUtil{chunkReader=region};chunk=new Chunk();tile=new TileEntityWorkstation{Chunk=chunk,Body=Enumerable.Range(0,32).Select(n=>(byte)n).ToArray()};chunk.Tile=tile;var world=new World{Chunk=chunk};world.ChunkCache.ChunkProvider=new ChunkProviderGenerateWorld{m_RegionFileManager=new Manager{snapshotUtil=util,regionFileAccess=access}};GameManager.Instance.World=world;reader=new PooledBinaryReader{BaseStream=new MemoryStream(new byte[10].Concat(tile.Body).ToArray())};region.loadChunkReader=reader;region.loadChunkMemoryStream=reader.BaseStream;request=new StationOutputNativeRequest{Operation=Guid.NewGuid(),Owner=new UiOutputLoadedOwner(),World=world,Save=GameIO.Root,Birth="birth",X=1,Z=2,Position=tile.Position,Body=tile.Body.ToArray(),ReturnedSpan=new byte[42],Elapsed=13,Graph=1024,Scratch=1024};stationNativeLoadRequests.Add(request);}
 static void Load(bool stream=true,bool read=true,bool factory=true,bool complete=true,Exception error=null){var batch=EnterRecipeOutputNativeLoad(util,GameIO.Root,0,true);ObserveRecipeOutputNativeLoadStream(region,reader,GameIO.Root,1,2,"7rg",47,stream);var entered=EnterRecipeOutputNativeInstantiate(reader,StreamModeRead.Persistency,TileEntityType.Workstation,chunk,true);var reading=EnterRecipeOutputPersistencyRead(tile,reader,StreamModeRead.Persistency,true);reader.BaseStream.Position=42;ObserveRecipeOutputPersistencyRead(reader,read,reading);ObserveRecipeOutputNativeInstantiate(tile,reader,chunk,factory,entered);EndRecipeOutputNativeInstantiate(entered,error);ObserveRecipeOutputNativeLoad(chunk,complete,batch);EndRecipeOutputNativeLoad(batch);}
 public static void Main(){Fresh();request.Owner=null;Load();Need(request.Loaded?.Current()==true,"actual native region load before owner did not retain source");var priorNative=request.Loaded;request.Owner=new UiOutputLoadedOwner();Need(ReferenceEquals(request.Loaded,priorNative)&&request.Loaded.Current(),"joining later actual owner replaced native source lineage");Fresh();Load();Need(request.Loaded?.Current()==true,"actual returned stream/factory/read/load did not bind installed native tile");tile.Fence++;Need(!request.Loaded.Current(),"new native read fence inherited original");Fresh();Load(stream:false);Need(request.Loaded==null,"skipped native stream qualified");Fresh();Load(read:false);Need(request.Loaded==null,"skipped actual read qualified");Fresh();Load(factory:false);Need(request.Loaded==null,"skipped actual factory qualified");Fresh();Load(error:new Exception("native factory"));Need(request.Loaded==null,"factory error qualified");Fresh();Load(complete:false);Need(request.Loaded==null,"skipped outer load qualified");Fresh();tile.Position=new Vector3i(18,3,33);Load();Need(request.Loaded==null,"different exact tile position qualified");Fresh();var raw=((MemoryStream)reader.BaseStream).ToArray();raw[41]++;reader.BaseStream=new MemoryStream(raw);region.loadChunkMemoryStream=reader.BaseStream;Load();Need(request.Loaded==null,"changed full native body qualified");Fresh();Load();((ChunkProviderGenerateWorld)GameManager.Instance.World.ChunkCache.ChunkProvider).m_RegionFileManager.snapshotUtil=new ChunkSnapshotUtil();Need(!request.Loaded.Current(),"replacement native provider inherited source");Fresh();Load();tile.Birth="replacement";Need(!request.Loaded.Current(),"replacement incarnation inherited source");Console.WriteLine("PASS "+checks+" actual native load-lineage assertions; native execution/schema are adapters.");}
}
