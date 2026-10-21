using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    // Requests are immutable saved ORIGINAL custody, registered before native
    // region loading. Parsed bytes alone never produce an Installed witness.
    internal sealed class CachedLoadRequest
    {
        internal Evidence Custody;internal string Directory,StartupOrigin;internal int X,Z;internal byte[] Snapshot,Tile;internal long Offset;
        internal CachedLoaded Loaded;
        internal bool PredictedNative,NormalizedTerminal,Loading;
        internal long MaximumGraph,MaximumScratch;
    }
    private sealed class CachedLoading
    {
        internal CachedLoadRequest Request;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;
        internal bool Loaded,Instantiating,Instantiated,ReadReturned;internal int Reads;
        internal long Start,ReadLength,ReadFence;
        internal IDisposable Retention;
        internal CachedLoading[] Alternatives;
    }
    internal sealed class CachedLoaded
    {
        internal CachedLoadRequest Request;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal Chunk Chunk;internal TileEntityWorkstation Tile;internal CraftCompleteData[] ActualReceiptRows;
        internal long ReadFence;
        internal bool Installed(World world)=>ReferenceEquals(Request.Custody.Load,Request)&&InstalledSource(world)&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(world,Tile,out var current)&&current==ReadFence;
        internal bool InstalledSource(World world)=>ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&
            ReferenceEquals(GameManager.Instance?.World,world)&&
            Guid.TryParse(RebirthWorkstationCompletionOutbox.CurrentGuid(world),out var currentGuid)&&currentGuid!=Guid.Empty&&
            Guid.TryParse(Request.Custody.WorldGuid,out var sourceGuid)&&currentGuid==sourceGuid&&
            RebirthProductiveEarnedArtifact.WorldIdentity(world)==Request.Custody.Frame.World&&
            string.Equals(Path.GetFullPath(GameIO.GetSaveGameDir()),Request.Directory,RebirthStationCompletionOriginalScope.PathComparison)&&
            world.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,Util)&&
            ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,Access)&&ReferenceEquals(world.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&
            !Chunk.InProgressUnloading&&ReferenceEquals(Chunk.GetTileEntity(Tile.localChunkPos),Tile)&&ReferenceEquals(Tile.GetChunk(),Chunk)&&
            Request.Custody.MarkerMatches(Chunk)&&Request.Custody.PrimaryCurrent();
    }
    private static readonly object cachedLoadGate=new object();
    private static readonly List<CachedLoadRequest> cachedLoadRequests=new List<CachedLoadRequest>();
    [ThreadStatic]private static CachedLoading cachedLoading;
    private sealed class CachedLoadingBatch{internal CachedLoading[] Entries;}
    [ThreadStatic]private static CachedLoadingBatch cachedLoadingBatch;
    internal static object EnterCachedNativeLoad(ChunkSnapshotUtil util,string dir,long key,bool run)
    {
        if(!run||cachedLoadingBatch!=null)return null;
        var entries=new List<CachedLoading>();
        try
        {
            lock(cachedLoadGate)
            {
                var matches=cachedLoadRequests.Where(value=>value.Custody.CanEnterNativeLoad()&&value.Directory==Path.GetFullPath(dir)&&
                    WorldChunkCache.extractX(key)==value.X&&WorldChunkCache.extractZ(key)==value.Z).ToArray();
                if(matches.Length<1||matches.Length>4)return null;
                var access=util?.chunkReader?.regionFileAccess;if(access==null)return null;
                foreach(var request in matches)
                {
                    if(request.Loading)return null;
                    var held=request.Custody.RetainNativeLoad();if(held==null)return null;
                    entries.Add(new CachedLoading{Request=request,Util=util,Access=access,Retention=held});
                }
                foreach(var entry in entries){entry.Request.Loading=true;entry.Request.Loaded=null;}
                var result=new CachedLoadingBatch{Entries=entries.ToArray()};entries.Clear();return cachedLoadingBatch=result;
            }
        }
        catch{return null;}
        finally{foreach(var entry in entries){lock(cachedLoadGate)entry.Request.Loading=false;entry.Retention?.Dispose();}}
    }
    internal static void ObserveCachedNativeLoadStream(RegionFileChunkReader source,PooledBinaryReader reader,string dir,int x,int z,string ext,uint version,bool run)
    {
        try
        {
            if(!run||cachedLoadingBatch==null||reader==null)return;
            foreach(var loading in cachedLoadingBatch.Entries)
            {
            var request=loading.Request;
            if(!request.Custody.Active||!ReferenceEquals(loading.Util.chunkReader,source)||!ReferenceEquals(source.regionFileAccess,loading.Access)||
                Path.GetFullPath(dir)!=request.Directory||x!=request.X||z!=request.Z||ext!="7rg"||version!=47||
                !ReferenceEquals(reader,source.loadChunkReader)||!ReferenceEquals(reader.BaseStream,source.loadChunkMemoryStream)||
                reader.BaseStream.Position!=0||reader.BaseStream.Length<1||reader.BaseStream.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)continue;
            loading.Reader=reader;loading.ReadLength=reader.BaseStream.Length;loading.Loaded=true;
            }
        }
        catch{}
    }
    internal static object EnterCachedNativeInstantiate(PooledBinaryReader reader,StreamModeRead mode,TileEntityType type,Chunk chunk,bool run)
    {
        if(!run||cachedLoadingBatch==null||cachedLoading!=null||mode!=StreamModeRead.Persistency||type!=TileEntityType.Workstation||chunk==null)return null;
        var matches=cachedLoadingBatch.Entries.Where(value=>value.Loaded&&!value.Instantiating&&!value.Instantiated&&
            ReferenceEquals(reader,value.Reader)&&chunk.X==value.Request.X&&chunk.Z==value.Request.Z&&
            MatchesCachedLoadStart(reader.BaseStream,value.Request)).ToArray();
        if(matches.Length<1||matches.Length>4)return null;
        // A predicted native header locates a tile, not an operation. Carry all
        // bounded candidates through the SAME real factory/read invocation;
        // the returned full body and saved marker select the source later.
        var loading=matches[0];cachedLoading=loading;loading.Alternatives=matches;
        foreach(var candidate in matches)
        {candidate.Start=reader.BaseStream.Position;candidate.Chunk=chunk;candidate.Instantiating=true;}
        return loading;
    }
    internal static object EnterCachedPersistencyRead(TileEntityWorkstation tile,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        var loading=cachedLoading;
        if(!run||loading==null||!loading.Instantiating||mode!=StreamModeRead.Persistency||!ReferenceEquals(reader,loading.Reader)||
            reader.BaseStream.Position!=loading.Start||!ReferenceEquals(tile.GetChunk(),loading.Chunk)||
            loading.Alternatives.Any(candidate=>candidate.Reads!=0))return null;
        foreach(var candidate in loading.Alternatives)
            if(candidate.Reads==0&&MatchesCachedLoadStart(reader.BaseStream,candidate.Request))
            {candidate.Tile=tile;candidate.Reads++;}
        return loading;
    }
    internal static void ObserveCachedPersistencyRead(PooledBinaryReader reader,bool run,object state)
    {
        if(state is not CachedLoading loading||!ReferenceEquals(loading,cachedLoading)||!run||!ReferenceEquals(reader,loading.Reader))return;
        foreach(var candidate in loading.Alternatives)
            if(candidate.Reads==1&&reader.BaseStream.Position==candidate.Start+candidate.Request.Tile.Length&&
                MatchesCachedReadReturn(reader.BaseStream,candidate.Start,candidate.Request)&&
                RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeReadFence(candidate.Tile,out candidate.ReadFence))candidate.ReadReturned=true;
    }
    internal static void ObserveCachedNativeInstantiate(TileEntity result,PooledBinaryReader reader,Chunk chunk,bool run,object state)
    {
        if(state is not CachedLoading loading||!run||!ReferenceEquals(loading,cachedLoading)||!loading.Instantiating||
            !ReferenceEquals(loading.Reader,reader)||!ReferenceEquals(loading.Chunk,chunk)||!ReferenceEquals(result?.GetChunk(),chunk))return;
        foreach(var candidate in loading.Alternatives)
            if(candidate.ReadReturned&&candidate.Reads==1&&ReferenceEquals(candidate.Tile,result)&&
                reader.BaseStream.Position==candidate.Start+candidate.Request.Tile.Length)candidate.Instantiated=true;
    }
    internal static void EndCachedNativeInstantiate(object state,Exception error)
    {
        if(state is not CachedLoading loading)return;
        foreach(var candidate in loading.Alternatives)
        {candidate.Instantiating=false;if(error!=null)candidate.Instantiated=false;}
        if(ReferenceEquals(cachedLoading,loading))cachedLoading=null;
    }
    internal static void ObserveCachedNativeLoad(Chunk result,bool run,object state)
    {
        if(state is not CachedLoadingBatch batch||!ReferenceEquals(batch,cachedLoadingBatch)||!run)return;
        lock(cachedLoadGate)
        {
            // Publication can supersede an earned request while a background
            // region load is in flight. Recheck before adopting its returned
            // native objects, not only when entering the region load.
            var returned=batch.Entries.Where(loading=>loading.Loaded&&loading.Instantiated&&loading.ReadReturned&&
                loading.Reads==1&&ReferenceEquals(result,loading.Chunk)&&ReferenceEquals(result?.GetTileEntity(loading.Tile.localChunkPos),loading.Tile)&&
                result.X==loading.Request.X&&result.Z==loading.Request.Z&&
                loading.Reader.BaseStream.Position==loading.ReadLength).ToArray();
            // A completed terminal may be saved again after its marker clear.
            // Its exact returned body plus the actual loaded chunk's absent
            // marker retires lookup memory only, never historical disk custody.
            // Pending/earned/uncertain candidates cannot use this branch.
            foreach(var loading in returned)
            {
                var source=loading.Request.Custody;
                if(!source.Terminal||source.PendingNextWork||!source.CanEnterNativeLoad()||
                    result.ChunkCustomData.dict.ContainsKey(source.MarkerKey))continue;
                if(!RecordCompletedNativeLoad(loading))continue;
                if(cachedLoadRequests.Remove(loading.Request)){MarkStartupRefillNeeded();source.Dispose();}
            }
            var matching=returned.Where(loading=>loading.Request.Custody.MarkerBytesMatch(result)&&
                loading.Request.Custody.CanEnterNativeLoad()).ToArray();
            foreach(var loading in matching)
            {
            if(matching.Count(candidate=>ReferenceEquals(candidate.Tile,loading.Tile))!=1)continue;
            if(!result.ChunkCustomData.dict.TryGetValue(loading.Request.Custody.MarkerKey,out var marker))continue;
            loading.Request.Custody.NativeMarker=marker;
            loading.Request.Custody.CustodyBirth=Guid.Empty;
            loading.Request.Loaded=new CachedLoaded{Request=loading.Request,Util=loading.Util,Access=loading.Access,Chunk=result,Tile=loading.Tile,
                ReadFence=loading.ReadFence,ActualReceiptRows=loading.Tile.CraftCompleteList?.Count<=4?loading.Tile.CraftCompleteList.ToArray():null};
            }
        }
    }
    internal static void EndCachedNativeLoad(object state)
    {
        if(state is not CachedLoadingBatch batch||batch.Entries==null)return;if(ReferenceEquals(cachedLoadingBatch,batch)){cachedLoadingBatch=null;cachedLoading=null;}
        foreach(var loading in batch.Entries)
        {
        lock(cachedLoadGate)if(loading.Request!=null)loading.Request.Loading=false;
        loading.Request=null;loading.Util=null;loading.Access=null;loading.Reader=null;loading.Chunk=null;loading.Tile=null;
        loading.Alternatives=null;
        loading.Retention?.Dispose();loading.Retention=null;
        }
        batch.Entries=null;
    }
    private static bool MatchesCachedTileSpan(Stream stream,byte[] expected)
    {
        if(stream==null||expected==null||!stream.CanRead||!stream.CanSeek||stream.Position<0||stream.Position>stream.Length-expected.Length)return false;
        long start=stream.Position;
        try{for(int i=0;i<expected.Length;i++)if(stream.ReadByte()!=expected[i])return false;return true;}
        finally{stream.Position=start;}
    }
    private static bool MatchesCachedLoadStart(Stream stream,CachedLoadRequest request)
    {
        if(!request.PredictedNative&&!request.NormalizedTerminal)return MatchesCachedTileSpan(stream,request.Tile);
        return RebirthProductiveEarnedLoadedNativeSpan.MatchesStart(stream,request.Custody.Native,request.Tile.Length);
    }
    private static bool MatchesCachedReadReturn(Stream stream,long start,CachedLoadRequest request)
    {
        if(request.PredictedNative||request.NormalizedTerminal)return RebirthProductiveEarnedLoadedNativeSpan.CaptureReturned(stream,start,
            request.Tile,request.Custody.Native,request.Custody.NativeOffset,request.MaximumGraph,request.MaximumScratch);
        long position=stream.Position;
        try{stream.Position=start;return MatchesCachedTileSpan(stream,request.Tile);}
        finally{stream.Position=position;}
    }
}

