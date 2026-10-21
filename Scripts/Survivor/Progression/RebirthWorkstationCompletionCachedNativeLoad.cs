using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Requests are immutable saved ORIGINAL custody, registered before native
    // region loading. Parsed bytes alone never produce an Installed witness.
    internal sealed class CachedLoadRequest
    {
        internal CachedCustody Custody;internal string Directory;internal int X,Z;internal byte[] Snapshot,Tile;internal long Offset;
        internal CachedLoaded Loaded;
    }
    private sealed class CachedLoading
    {
        internal CachedLoadRequest Request;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;
        internal bool Loaded,Instantiating,Instantiated,ReadReturned;internal int Reads;
        internal long Start,ReadLength,ReadFence;
        internal IDisposable Retention;
    }
    internal sealed class CachedLoaded
    {
        internal CachedLoadRequest Request;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal Chunk Chunk;internal TileEntityWorkstation Tile;internal CraftCompleteData[] ActualReceiptRows;
        internal long ReadFence;
        internal bool Installed(World world)=>ReferenceEquals(Request.Custody.Load,Request)&&InstalledSource(world)&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(world,Tile,out var current)&&current==ReadFence;
        internal bool InstalledSource(World world)=>ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&
            ReferenceEquals(GameManager.Instance?.World,world)&&RebirthWorkstationCompletionOutbox.CurrentGuid(world)==Request.Custody.WorldGuid&&
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
                var matches=cachedLoadRequests.Where(value=>value.Custody.Active&&value.Directory==Path.GetFullPath(dir)&&
                    WorldChunkCache.extractX(key)==value.X&&WorldChunkCache.extractZ(key)==value.Z).ToArray();
                if(matches.Length<1||matches.Length>4)return null;
                var access=util?.chunkReader?.regionFileAccess;if(access==null)return null;
                foreach(var request in matches)
                {
                    var held=request.Custody.RetainNativeLoad();if(held==null)return null;
                    entries.Add(new CachedLoading{Request=request,Util=util,Access=access,Retention=held});
                }
                var result=new CachedLoadingBatch{Entries=entries.ToArray()};entries.Clear();return cachedLoadingBatch=result;
            }
        }
        catch{return null;}
        finally{foreach(var entry in entries)entry.Retention?.Dispose();}
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
            ReferenceEquals(reader,value.Reader)&&MatchesCachedTileSpan(reader.BaseStream,value.Request.Tile)).ToArray();
        if(matches.Length!=1)return null;var loading=matches[0];cachedLoading=loading;
        loading.Start=reader.BaseStream.Position;loading.Chunk=chunk;loading.Instantiating=true;return loading;
    }
    internal static object EnterCachedPersistencyRead(TileEntityWorkstation tile,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        var loading=cachedLoading;
        if(!run||loading==null||!loading.Instantiating||mode!=StreamModeRead.Persistency||!ReferenceEquals(reader,loading.Reader)||loading.Reads!=0||
            reader.BaseStream.Position!=loading.Start||!MatchesCachedTileSpan(reader.BaseStream,loading.Request.Tile)||!ReferenceEquals(tile.GetChunk(),loading.Chunk))return null;
        loading.Tile=tile;loading.Reads++;return loading;
    }
    internal static void ObserveCachedPersistencyRead(PooledBinaryReader reader,bool run,object state)
    {
        if(state is CachedLoading loading&&ReferenceEquals(loading,cachedLoading)&&run&&ReferenceEquals(reader,loading.Reader)&&
            loading.Reads==1&&reader.BaseStream.Position==loading.Start+loading.Request.Tile.Length&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeReadFence(loading.Tile,out loading.ReadFence))loading.ReadReturned=true;
    }
    internal static void ObserveCachedNativeInstantiate(TileEntity result,PooledBinaryReader reader,Chunk chunk,bool run,object state)
    {
        if(state is not CachedLoading loading||!run||!ReferenceEquals(loading,cachedLoading)||!loading.Instantiating||!loading.ReadReturned||
            loading.Reads!=1||!ReferenceEquals(loading.Reader,reader)||!ReferenceEquals(loading.Chunk,chunk)||!ReferenceEquals(loading.Tile,result)||
            !ReferenceEquals(result?.GetChunk(),chunk)||reader.BaseStream.Position!=loading.Start+loading.Request.Tile.Length)return;
        loading.Instantiated=true;
    }
    internal static void EndCachedNativeInstantiate(object state,Exception error)
    {if(state is CachedLoading loading){loading.Instantiating=false;if(error!=null)loading.Instantiated=false;if(ReferenceEquals(cachedLoading,loading))cachedLoading=null;}}
    internal static void ObserveCachedNativeLoad(Chunk result,bool run,object state)
    {
        if(state is not CachedLoadingBatch batch||!ReferenceEquals(batch,cachedLoadingBatch)||!run)return;
        foreach(var loading in batch.Entries)
        {
        if(!loading.Loaded||!loading.Instantiated||!loading.ReadReturned||
            loading.Reads!=1||!ReferenceEquals(result,loading.Chunk)||!ReferenceEquals(result?.GetTileEntity(loading.Tile.localChunkPos),loading.Tile)||
            loading.Reader.BaseStream.Position!=loading.ReadLength||!loading.Request.Custody.MarkerMatches(result))continue;
        lock(cachedLoadGate)loading.Request.Loaded=new CachedLoaded{Request=loading.Request,Util=loading.Util,Access=loading.Access,Chunk=result,Tile=loading.Tile,
            ReadFence=loading.ReadFence,ActualReceiptRows=loading.Tile.CraftCompleteList?.Count<=4?loading.Tile.CraftCompleteList.ToArray():null};
        }
    }
    internal static void EndCachedNativeLoad(object state)
    {
        if(state is not CachedLoadingBatch batch||batch.Entries==null)return;if(ReferenceEquals(cachedLoadingBatch,batch)){cachedLoadingBatch=null;cachedLoading=null;}
        foreach(var loading in batch.Entries)
        {
        loading.Request=null;loading.Util=null;loading.Access=null;loading.Reader=null;loading.Chunk=null;loading.Tile=null;
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
}
