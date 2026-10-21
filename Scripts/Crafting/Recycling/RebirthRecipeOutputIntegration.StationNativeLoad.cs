using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static partial class RebirthRecipeOutputIntegration
{
    // A recipe-output purpose owns its own actual region/factory/read lineage.
    // Shared native parsers are detached grammar helpers, never earned custody.
    private sealed class StationOutputNativeRequest:IDisposable
    {
        internal Guid Operation;internal World World;internal string Save,Birth,OriginalHash;
        internal UiOutputLoadedOwner Owner;internal byte[] Original,Grid;internal System.Xml.Linq.XElement Image;
        internal bool CallbackReturned,Adopted;
        internal UiOutputOriginal ActiveOriginal;
        internal RebirthCookingMillingPublication.StaticSourcePurpose MillingPurpose;
        internal RebirthWorkstationCompletionPipeline.RemoteOutputHostSource RemoteOutputPurpose;
        internal RebirthProductiveEarnedUiDeferredTransport.AuthorityLocalLoadedSource AuthorityLocalPurpose;
        internal int X,Z,Elapsed;internal Vector3i Position;internal byte[] Body,ReturnedSpan;
        internal long Graph,Scratch;internal IDisposable Memory,ExtraMemory;
        internal bool Loading,Closed;internal StationOutputNativeLoaded Loaded;
        public void Dispose(){Closed=true;Loaded=null;ActiveOriginal=null;if(Loading)return;stationOutputColdHolds.RemoveAll(value=>value.Operation==Operation&&ReferenceEquals(value.Owner,Owner));Memory?.Dispose();Memory=null;ExtraMemory?.Dispose();ExtraMemory=null;Body=ReturnedSpan=null;MillingPurpose=null;RemoteOutputPurpose=null;AuthorityLocalPurpose=null;}
    }
    private sealed class StationOutputNativeLoading
    {
        internal StationOutputNativeRequest Request;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;
        internal long Start,Length,Fence;internal int Reads;
        internal bool StreamReturned,FactoryEntered,ReadReturned,FactoryReturned;
        internal StationOutputNativeLoading[] Alternatives;
    }
    private sealed class StationOutputNativeBatch{internal StationOutputNativeLoading[] Values;}
    private sealed class StationOutputNativeLoaded
    {
        internal StationOutputNativeRequest Request;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal Chunk Chunk;internal TileEntityWorkstation Tile;internal long Fence;
        internal bool Current()=>!Request.Closed&&ReferenceEquals(Request.Loaded,this)&&ThreadManager.IsMainThread()&&
            ReferenceEquals(GameManager.Instance?.World,Request.World)&&!Request.World.IsRemote()&&
            Path.GetFullPath(GameIO.GetSaveGameDir())==Request.Save&&
            Request.World.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&
            ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,Util)&&ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,Access)&&
            ReferenceEquals(Request.World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&!Chunk.InProgressUnloading&&
            ReferenceEquals(Chunk.GetTileEntity(Tile.localChunkPos),Tile)&&ReferenceEquals(Tile.GetChunk(),Chunk)&&
            Tile.ToWorldPos()==Request.Position&&RebirthLogisticsEndpointIdentity.TryGetIncarnation(Request.World,Tile,out var birth)&&birth==Request.Birth&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(Request.World,Tile,out var fence)&&fence==Fence&&
            RebirthProductiveEarnedNativeBody.MatchesCurrent(Tile,Request.Body,Request.Elapsed);
    }
    private static readonly object stationNativeLoadGate=new object();
    private static readonly List<StationOutputNativeRequest> stationNativeLoadRequests=new List<StationOutputNativeRequest>();
    [ThreadStatic]private static StationOutputNativeBatch stationNativeLoadingBatch;
    [ThreadStatic]private static StationOutputNativeLoading stationNativeLoading;
    internal static object EnterRecipeOutputNativeLoad(ChunkSnapshotUtil util,string directory,long key,bool ran)
    {
        if(!ran||stationNativeLoadingBatch!=null||util?.chunkReader?.regionFileAccess==null)return null;
        lock(stationNativeLoadGate)
        {
            var requests=stationNativeLoadRequests.Where(value=>!value.Closed&&value.Owner?.Closed!=true&&!value.Loading&&value.Save==Path.GetFullPath(directory)&&
                value.X==WorldChunkCache.extractX(key)&&value.Z==WorldChunkCache.extractZ(key)).ToArray();
            if(requests.Length==0)return null;
            var batch=new StationOutputNativeBatch{Values=requests.Select(value=>new StationOutputNativeLoading{Request=value,Util=util,Access=util.chunkReader.regionFileAccess}).ToArray()};
            foreach(var value in requests){value.Loading=true;value.Loaded=null;}
            return stationNativeLoadingBatch=batch;
        }
    }
    internal static void ObserveRecipeOutputNativeLoadStream(RegionFileChunkReader source,PooledBinaryReader reader,string directory,int x,int z,string extension,uint version,bool ran)
    {
        if(!ran||stationNativeLoadingBatch==null||reader==null)return;
        foreach(var loading in stationNativeLoadingBatch.Values)
        {
            var request=loading.Request;
            if(request.Closed||!ReferenceEquals(loading.Util.chunkReader,source)||!ReferenceEquals(source.regionFileAccess,loading.Access)||
                Path.GetFullPath(directory)!=request.Save||x!=request.X||z!=request.Z||extension!="7rg"||version!=47||
                !ReferenceEquals(reader,source.loadChunkReader)||!ReferenceEquals(reader.BaseStream,source.loadChunkMemoryStream)||
                reader.BaseStream.Position!=0||reader.BaseStream.Length<1||reader.BaseStream.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)continue;
            loading.Reader=reader;loading.Length=reader.BaseStream.Length;loading.StreamReturned=true;
        }
    }
    internal static object EnterRecipeOutputNativeInstantiate(PooledBinaryReader reader,StreamModeRead mode,TileEntityType type,Chunk chunk,bool ran)
    {
        if(!ran||stationNativeLoadingBatch==null||stationNativeLoading!=null||mode!=StreamModeRead.Persistency||type!=TileEntityType.Workstation||chunk==null)return null;
        var matches=stationNativeLoadingBatch.Values.Where(value=>!value.Request.Closed&&value.StreamReturned&&!value.FactoryEntered&&!value.FactoryReturned&&
            ReferenceEquals(reader,value.Reader)&&chunk.X==value.Request.X&&chunk.Z==value.Request.Z&&
            RebirthProductiveEarnedLoadedNativeSpan.MatchesStart(reader.BaseStream,value.Request.Body,value.Request.ReturnedSpan.Length)).ToArray();
        if(matches.Length==0)return null;
        var first=matches[0];first.Alternatives=matches;stationNativeLoading=first;
        foreach(var value in matches){value.Start=reader.BaseStream.Position;value.Chunk=chunk;value.FactoryEntered=true;}
        return first;
    }
    internal static object EnterRecipeOutputPersistencyRead(TileEntityWorkstation tile,PooledBinaryReader reader,StreamModeRead mode,bool ran)
    {
        var loading=stationNativeLoading;
        if(!ran||loading==null||mode!=StreamModeRead.Persistency||!ReferenceEquals(reader,loading.Reader)||reader.BaseStream.Position!=loading.Start||
            !ReferenceEquals(tile.GetChunk(),loading.Chunk)||loading.Alternatives.Any(value=>value.Reads!=0))return null;
        foreach(var value in loading.Alternatives){value.Reads=1;value.Tile=tile;}
        return loading;
    }
    internal static void ObserveRecipeOutputPersistencyRead(PooledBinaryReader reader,bool ran,object state)
    {
        if(state is not StationOutputNativeLoading loading||!ReferenceEquals(loading,stationNativeLoading)||!ran||!ReferenceEquals(reader,loading.Reader))return;
        foreach(var value in loading.Alternatives)
        {
            var request=value.Request;
            if(!request.Closed&&value.Reads==1&&reader.BaseStream.Position==value.Start+request.ReturnedSpan.Length&&
                value.Tile.ToWorldPos()==request.Position&&
                RebirthProductiveEarnedLoadedNativeSpan.CaptureReturned(reader.BaseStream,value.Start,request.ReturnedSpan,request.Body,request.Elapsed,request.Graph,request.Scratch)&&
                RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeReadFence(value.Tile,out value.Fence))value.ReadReturned=true;
        }
    }
    internal static void ObserveRecipeOutputNativeInstantiate(TileEntity result,PooledBinaryReader reader,Chunk chunk,bool ran,object state)
    {
        if(state is not StationOutputNativeLoading loading||!ran||!ReferenceEquals(loading,stationNativeLoading)||
            !ReferenceEquals(reader,loading.Reader)||!ReferenceEquals(chunk,loading.Chunk)||!ReferenceEquals(result?.GetChunk(),chunk))return;
        foreach(var value in loading.Alternatives)
            if(!value.Request.Closed&&value.ReadReturned&&value.Reads==1&&ReferenceEquals(value.Tile,result)&&
                reader.BaseStream.Position==value.Start+value.Request.ReturnedSpan.Length)value.FactoryReturned=true;
    }
    internal static void EndRecipeOutputNativeInstantiate(object state,Exception error)
    {
        if(state is not StationOutputNativeLoading loading)return;
        foreach(var value in loading.Alternatives){value.FactoryEntered=false;if(error!=null)value.FactoryReturned=false;}
        if(ReferenceEquals(stationNativeLoading,loading))stationNativeLoading=null;
    }
    internal static void ObserveRecipeOutputNativeLoad(Chunk result,bool ran,object state)
    {
        if(state is not StationOutputNativeBatch batch||!ReferenceEquals(batch,stationNativeLoadingBatch)||!ran)return;
        lock(stationNativeLoadGate)foreach(var value in batch.Values)
        {
            var request=value.Request;
            if(request.Closed||!value.StreamReturned||!value.FactoryReturned||!value.ReadReturned||value.Reads!=1||
                !ReferenceEquals(value.Chunk,result)||!ReferenceEquals(result?.GetTileEntity(value.Tile.localChunkPos),value.Tile)||
                result.X!=request.X||result.Z!=request.Z||value.Reader.BaseStream.Position!=value.Length)continue;
            if(batch.Values.Count(other=>other.FactoryReturned&&ReferenceEquals(other.Tile,value.Tile))!=1)continue;
            request.Loaded=new StationOutputNativeLoaded{Request=request,Util=value.Util,Access=value.Access,Chunk=result,Tile=value.Tile,Fence=value.Fence};
        }
    }
    internal static void EndRecipeOutputNativeLoad(object state,Exception error=null)
    {
        if(state is not StationOutputNativeBatch batch)return;
        lock(stationNativeLoadGate)foreach(var value in batch.Values)
        {value.Request.Loading=false;if(error!=null)value.Request.Loaded=null;if(value.Request.Closed){value.Request.Dispose();stationNativeLoadRequests.Remove(value.Request);}value.Reader=null;value.Tile=null;value.Chunk=null;value.Alternatives=null;}
        if(ReferenceEquals(stationNativeLoadingBatch,batch)){stationNativeLoadingBatch=null;stationNativeLoading=null;}
    }
}
