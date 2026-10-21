using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Full positive saved enqueue, not a matching row descriptor. Native game
    // readers consume their real region stream; these bytes only qualify it.
    private sealed class Source4SavedNative
    {
        internal byte[] Snapshot,Payload,Tile;internal long Offset;internal Vector3i Position;internal string CompletionDigest;
        internal int SnapshotLength,TileLength;internal long CompletionLength;internal string SnapshotDigest,TileDigest;
        internal void CopyFrom(Source4SavedNative source)
        {Snapshot=source.Snapshot;Payload=source.Payload;Tile=source.Tile;Offset=source.Offset;Position=source.Position;CompletionDigest=source.CompletionDigest;CompletionLength=source.CompletionLength;}
        internal void Compact()
        {SnapshotLength=Snapshot.Length;TileLength=Tile.Length;using(var sha=SHA256.Create()){SnapshotDigest=Convert.ToBase64String(sha.ComputeHash(Snapshot,8,Snapshot.Length-8));TileDigest=Convert.ToBase64String(sha.ComputeHash(Tile));}Snapshot=Payload=Tile=null;}
    }
    private sealed class Source4NativeOriginal:IDisposable
    {
        internal World World;internal string Save,Region,Proof;internal IRegionFileChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal int Phase;internal bool Used;
        internal RebirthRemoteResourcePlacedStationReference.Image Reference;internal Source4SavedNative Native;
        internal Chunk Chunk;internal TileEntityWorkstation Tile;internal RecipeQueueItem Row;internal int Slot,OriginalCount;internal string RowBinding;
        internal ChunkCustomData Marker;internal byte[] MarkerImage;internal string MarkerKey;
        internal IDisposable Memory;internal long Fence;internal bool Installed;internal volatile bool Closed;internal int Loads;
        internal bool LoadCurrent()=>!Closed&&ReferenceEquals(World,GameManager.Instance?.World)&&World?.IsRemote()==false&&
            Save==Path.GetFullPath(GameIO.GetSaveGameDir())&&
            World.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,Util)&&
            ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,Access);
        internal bool Current()=>ThreadManager.IsMainThread()&&LoadCurrent();
        internal bool LoadedCurrent()=>Current()&&Installed&&Loads==0&&Chunk!=null&&Tile!=null&&ReferenceEquals(World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&
            !Chunk.InProgressUnloading&&ReferenceEquals(Chunk.GetTileEntity(Tile.localChunkPos),Tile)&&ReferenceEquals(Tile.GetChunk(),Chunk)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Tile,out var birth)&&birth==Reference.Birth&&
            Chunk.ChunkCustomData.dict.TryGetValue(MarkerKey,out var marker)&&ReferenceEquals(marker,Marker)&&marker.data!=null&&marker.data.SequenceEqual(MarkerImage)&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Tile,out var fence)&&fence==Fence;
        internal bool OriginalRowCurrent()=>LoadedCurrent()&&Slot>=0&&Slot<Tile.Queue.Length&&ReferenceEquals(Tile.Queue[Slot],Row)&&
            Row?.Recipe!=null&&Row.Multiplier>0&&Row.Multiplier<=OriginalCount&&RebirthProductiveEarnedUiQueueOwner.NativeBinding(Row)==RowBinding;
        public void Dispose(){lock(source4LoadGate){Closed=true;Release();}}
        internal void Release(){if(!Closed||Loads!=0)return;Memory?.Dispose();Memory=null;Native=null;Chunk=null;Tile=null;Row=null;Marker=null;MarkerImage=null;}
    }
    private sealed class Source4NativeLoading
    {
        internal Source4NativeOriginal Original;internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;
        internal bool Stream,Factory,Returned,Instantiated;internal int Reads;internal long Fence;
    }
    private static readonly List<Source4NativeOriginal> source4NativeOriginals=new List<Source4NativeOriginal>();
    private static readonly object source4LoadGate=new object();
    [ThreadStatic]private static Source4NativeLoading[] source4Loading;
    [ThreadStatic]private static Source4NativeLoading source4Instantiating;
    private static bool Source4CompletionCurrent(Source4NativeOriginal source)
    {
        if(!source.LoadCurrent())return false;
        try{using(var stream=new FileStream(CompletionPath(source.Reference.Source),FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var sha=SHA256.Create())return stream.Length==source.Native.CompletionLength&&BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()==source.Native.CompletionDigest;}
        catch{return false;}
    }
    internal static void DiscoverSource4NativeOrigins(RegionFileManager manager)
    {
        if(!ThreadManager.IsMainThread()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||GameManager.Instance?.World is not World world||world.IsRemote()||
            manager?.snapshotUtil==null||manager.regionFileAccess is not RegionFileAccessSectorBased||
            Path.GetFullPath(manager.saveDirectory)!=Path.GetFullPath(GameIO.GetSaveGameRegionDir()))return;
        lock(source4LoadGate){foreach(var old in source4NativeOriginals.Where(value=>!ReferenceEquals(value.World,world)||!ReferenceEquals(value.Util,manager.snapshotUtil)).ToArray())
        {source4NativeOriginals.Remove(old);old.Dispose();}
        string directory=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedSavedAfter");CompletionAncestors(directory);if(!Directory.Exists(directory))return;
        foreach(string path in Directory.EnumerateFiles(directory,"*.rpe"))
        {
            RebirthRemoteResourcePlacedStationPreparation.ColdRead allocation=null;Source4NativeOriginal candidate=null;
            try
            {
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var operation)||source4NativeOriginals.Any(value=>value.Reference.Source==operation)||
                    CompletionPath(operation)!=path||!RebirthRemoteResourcePlacedStationArtifactStore.TryReadHeader(operation,out var header)||
                    !RebirthRemoteResourcePlacedStationPreparation.TryReserveColdRead(header,out allocation))continue;
                RebirthRemoteResourcePlacedStationReference.Image reference;
                using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var reader=new BinaryReader(input,new UTF8Encoding(false,true),true))
                    if(reader.ReadUInt32()!=CompletionMagic||!RebirthRemoteResourcePlacedStationReference.TryRead(CompletionText(reader,4096),out reference)||reference.Source!=operation)continue;
                if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.HistoricalSource,out var scratch))continue;
                RebirthRemoteResourceDurableJournal.Record root;
                using(scratch)if(!RebirthRemoteResourceDurableJournal.TryReadPlacedHistoryBounded(reference.Owner,operation,header,out root,out bool unknown)||unknown||root==null)continue;
                if(!IsMappedRoot(root)||!RebirthRemoteResourcePlacedStationPrepared.TryRead(root,out var artifact,out string digest,null,allocation)||digest!=reference.Artifact||
                    artifact.Original.Digest!=reference.Original||artifact.Birth!=reference.Birth||root.Binding!=reference.Binding||
                    root.Phase!=2&&root.Phase!=4||root.Phase==4&&(string)root.Copy().Attribute("disposition")!="used")continue;
                var native=new Source4SavedNative();
                if(!TryReadCompletion(root,artifact,reference,null,out string proof,false,allocation,native)||
                    root.Phase==4&&(string)root.Copy().Attribute("ownerProof")!=proof||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(65536,out var memory))continue;
                candidate=new Source4NativeOriginal{World=world,Save=Path.GetFullPath(GameIO.GetSaveGameDir()),Region=Path.GetFullPath(manager.saveDirectory),
                    Util=manager.snapshotUtil,Access=manager.regionFileAccess,Phase=root.Phase,Used=(string)root.Copy().Attribute("disposition")=="used",Reference=reference,Native=native,Proof=proof,Memory=memory};
                candidate.Slot=(int)artifact.Copy().Element("plan").Attribute("slot");
                candidate.MarkerKey="RebirthLogisticsStationV1:"+(native.Position.x&15)+":"+native.Position.y+":"+(native.Position.z&15);
                candidate.MarkerImage=Convert.FromBase64String(artifact.Copy().Element("savedBefore").Element("markers").Elements("marker")
                    .Single(value=>(string)value.Attribute("key")==candidate.MarkerKey).Element("data").Value);
                native.Compact();
                // Only fixed native digests/offsets/identity survive discovery.
                // Drop whole graphs and the unique cold-read lease; main-thread
                // owner issuance independently reopens the full typed original.
                allocation.Dispose();allocation=null;source4NativeOriginals.Add(candidate);candidate=null;
            }
            catch{/* An unqualified saved source is retained on disk, never minted as queue ownership. */}
            finally{allocation?.Dispose();candidate?.Dispose();}
        }}
    }
    internal static object EnterSource4NativeLoad(ChunkSnapshotUtil util,string dir,long key,bool ran)
    {
        if(!ran||source4Loading!=null)return null;
        lock(source4LoadGate){var matches=source4NativeOriginals.Where(value=>value.LoadCurrent()&&value.Loads==0&&ReferenceEquals(value.Util,util)&&value.Region==Path.GetFullPath(dir)&&
            (value.Native.Position.x>>4)==WorldChunkCache.extractX(key)&&(value.Native.Position.z>>4)==WorldChunkCache.extractZ(key)).ToArray();
        if(matches.Length<1)return null;
        foreach(var match in matches){match.Loads++;match.Installed=false;match.Row=null;}
        return source4Loading=matches.Select(value=>new Source4NativeLoading{Original=value}).ToArray();}
    }
    private static bool TryOpenSource4History(Source4NativeOriginal source,out RebirthRemoteResourcePlacedStationColdRead.Original original)
    {
        original=null;RebirthRemoteResourcePlacedStationPreparation.ColdRead allocation=null;
        try
        {
            var reference=source.Reference;
            if(!source.Current()||!RebirthRemoteResourcePlacedStationArtifactStore.TryReadHeader(reference.Source,out var header)||
                !RebirthRemoteResourcePlacedStationPreparation.TryReserveColdRead(header,out allocation)||
                !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.HistoricalSource,out var scratch))return false;
            RebirthRemoteResourceDurableJournal.Record root;
            using(scratch)if(!RebirthRemoteResourceDurableJournal.TryReadPlacedHistoryBounded(reference.Owner,reference.Source,header,out root,out bool unknown)||unknown||root==null)return false;
            if(root.Binding!=reference.Binding||!IsMappedRoot(root)||!RebirthRemoteResourcePlacedStationPrepared.TryRead(root,out var artifact,out string digest,null,allocation)||
                digest!=reference.Artifact||artifact.Original.Digest!=reference.Original||artifact.Birth!=reference.Birth||
                !TryReadCompletion(root,artifact,reference,null,out string proof,false,allocation)||proof!=source.Proof||
                root.Phase!=2&&root.Phase!=4||root.Phase==4&&((string)root.Copy().Attribute("disposition")!="used"||(string)root.Copy().Attribute("ownerProof")!=proof))return false;
            original=new RebirthRemoteResourcePlacedStationColdRead.Original(allocation,root,artifact,digest);allocation=null;return true;
        }
        catch{return false;}
        finally{allocation?.Dispose();}
    }
    internal static void ObserveSource4NativeLoadStream(RegionFileChunkReader source,PooledBinaryReader reader,string dir,int x,int z,string ext,uint version,bool ran)
    {
        if(!ran||source4Loading==null||reader?.BaseStream==null)return;
        string digest=null;
        foreach(var loading in source4Loading)
        {
            var original=loading.Original;var native=original.Native;
            if(!original.LoadCurrent()||original.Util is not ChunkSnapshotUtil nativeUtil||!ReferenceEquals(nativeUtil.chunkReader,source)||!ReferenceEquals(source.regionFileAccess,original.Access)||Path.GetFullPath(dir)!=original.Region||
                x!=(native.Position.x>>4)||z!=(native.Position.z>>4)||ext!="7rg"||version!=47||!ReferenceEquals(reader,source.loadChunkReader)||
                !ReferenceEquals(reader.BaseStream,source.loadChunkMemoryStream)||reader.BaseStream.Position!=0||reader.BaseStream.Length!=native.SnapshotLength-8)continue;
            if(digest==null){long position=reader.BaseStream.Position;
                try{using(var sha=SHA256.Create())digest=Convert.ToBase64String(sha.ComputeHash(reader.BaseStream));}finally{reader.BaseStream.Position=position;}}
            if(digest==native.SnapshotDigest){loading.Reader=reader;loading.Stream=true;}
        }
    }
    internal static object EnterSource4NativeInstantiate(PooledBinaryReader reader,StreamModeRead mode,TileEntityType type,Chunk chunk,bool ran)
    {
        if(!ran||mode!=StreamModeRead.Persistency||type!=TileEntityType.Workstation||chunk==null||source4Loading==null||source4Instantiating!=null)return null;
        var matches=source4Loading.Where(value=>value.Stream&&!value.Instantiated&&ReferenceEquals(value.Reader,reader)&&
            reader.BaseStream.Position==value.Original.Native.Offset-8&&chunk.X==(value.Original.Native.Position.x>>4)&&chunk.Z==(value.Original.Native.Position.z>>4)).ToArray();
        if(matches.Length!=1)return null;var loading=matches[0];loading.Chunk=chunk;loading.Factory=true;return source4Instantiating=loading;
    }
    internal static object EnterSource4PersistencyRead(TileEntityWorkstation tile,PooledBinaryReader reader,StreamModeRead mode,bool ran)
    {
        var loading=source4Instantiating;
        if(!ran||loading==null||!loading.Factory||loading.Reads!=0||mode!=StreamModeRead.Persistency||!ReferenceEquals(reader,loading.Reader)||
            reader.BaseStream.Position!=loading.Original.Native.Offset-8||!ReferenceEquals(tile.GetChunk(),loading.Chunk))return null;
        loading.Reads++;loading.Tile=tile;return loading;
    }
    internal static void ObserveSource4PersistencyRead(PooledBinaryReader reader,bool ran,object state)
    {
        if(state is not Source4NativeLoading loading||!ran||!ReferenceEquals(loading,source4Instantiating)||!ReferenceEquals(reader,loading.Reader)||loading.Reads!=1)return;
        if(reader.BaseStream.Position==loading.Original.Native.Offset-8+loading.Original.Native.TileLength&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeReadFence(loading.Tile,out loading.Fence))loading.Returned=true;
    }
    internal static void ObserveSource4NativeInstantiate(TileEntity result,PooledBinaryReader reader,Chunk chunk,bool ran,object state)
    {
        if(state is Source4NativeLoading loading&&ran&&ReferenceEquals(loading,source4Instantiating)&&loading.Returned&&loading.Reads==1&&
            ReferenceEquals(reader,loading.Reader)&&ReferenceEquals(result,loading.Tile)&&ReferenceEquals(chunk,loading.Chunk)&&ReferenceEquals(result?.GetChunk(),chunk)&&
            reader.BaseStream.Position==loading.Original.Native.Offset-8+loading.Original.Native.TileLength)loading.Instantiated=true;
    }
    internal static void EndSource4NativeInstantiate(object state,Exception error)
    {if(state is Source4NativeLoading loading){if(error!=null)loading.Instantiated=false;loading.Factory=false;if(ReferenceEquals(loading,source4Instantiating))source4Instantiating=null;}}
    internal static void ObserveSource4NativeLoad(Chunk result,bool ran,object state)
    {
        if(!ran||state is not Source4NativeLoading[] entries||!ReferenceEquals(entries,source4Loading))return;
        foreach(var loading in entries)
        {
            var original=loading.Original;
            if(!original.LoadCurrent()||!loading.Instantiated||!loading.Returned||loading.Reads!=1||!ReferenceEquals(result,loading.Chunk)||
                !ReferenceEquals(result?.GetTileEntity(loading.Tile.localChunkPos),loading.Tile)||!ReferenceEquals(loading.Tile.GetChunk(),result)||
                loading.Reader.BaseStream.Position!=original.Native.SnapshotLength-8||!Source4CompletionCurrent(original)||
                !result.ChunkCustomData.dict.TryGetValue(original.MarkerKey,out var marker)||marker.TriggerWriteDataDelegate!=null||marker.isSavedToNetwork||
                marker.expiresInWorldTime!=ulong.MaxValue||marker.data==null||!marker.data.SequenceEqual(original.MarkerImage))continue;
            int slot=original.Slot;
            if(slot<0||slot>=loading.Tile.Queue.Length||loading.Tile.Queue[slot]?.Recipe==null)continue;
            string binding=RebirthProductiveEarnedUiQueueOwner.NativeBinding(loading.Tile.Queue[slot]);
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(original.Memory,checked(65536+4L*binding.Length)))continue;
            original.Chunk=result;original.Tile=loading.Tile;original.Marker=marker;original.Fence=loading.Fence;original.Slot=slot;original.Row=loading.Tile.Queue[slot];
            original.OriginalCount=original.Row.Multiplier;original.RowBinding=binding;original.Installed=true;
        }
    }
    internal static void EndSource4NativeLoad(object state)
    {if(state is Source4NativeLoading[] entries){if(ReferenceEquals(entries,source4Loading))source4Loading=null;source4Instantiating=null;lock(source4LoadGate)foreach(var entry in entries){entry.Reader=null;entry.Tile=null;entry.Chunk=null;var original=entry.Original;entry.Original=null;if(original!=null){original.Loads--;original.Release();}}}}
}
