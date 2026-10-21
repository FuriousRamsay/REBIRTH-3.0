using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed class ColdStationCandidate:IDisposable
    {
        internal World World;internal string Save,RegionDirectory,Path,Stage,MarkerKey,WorldKey;
        internal Guid Operation;internal Vector3i Position;internal byte[] Receipt,Tile,Marker,MarkerSpan,Original;
        internal IDisposable Memory;internal bool Closed;
        public void Dispose(){if(Closed)return;Closed=true;Receipt=Tile=Marker=MarkerSpan=Original=null;Memory?.Dispose();Memory=null;}
    }
    internal sealed class LoadedStation:IDisposable
    {
        private readonly ColdStationCandidate candidate;
        internal readonly ChunkSnapshotUtil Util;internal readonly RegionFileAccessAbstract Access;
        internal readonly Chunk Chunk;internal readonly TileEntityWorkstation Station;private readonly long fence;
        private bool closed;
        private LoadedStation(ColdStationCandidate candidate,ColdStationRead read)
        {this.candidate=candidate;Util=read.Util;Access=read.Access;Chunk=read.Chunk;Station=read.Tile;fence=read.Fence;}
        internal Guid Operation=>candidate.Operation;internal string Stage=>candidate.Stage;
        internal World World=>candidate.World;
        internal bool MatchesOriginal(byte[] original)=>Current()&&original!=null&&candidate.Original.SequenceEqual(original);
        internal bool Adopted;internal long MeasureOriginal()=>candidate.Original?.LongLength??-1;
        internal bool Current()
        {
            try{return !closed&&!candidate.Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,candidate.World)&&
                !candidate.World.IsRemote()&&RebirthWorkstationCompletionOutbox.CurrentGuid(candidate.World)==candidate.WorldKey&&
                Path.GetFullPath(GameIO.GetSaveGameDir())==candidate.Save&&candidate.World.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&
                ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,Util)&&ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,Access)&&
                ReferenceEquals(candidate.World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&!Chunk.InProgressUnloading&&
                ReferenceEquals(candidate.World.GetTileEntity(candidate.Position),Station)&&ReferenceEquals(Station.GetChunk(),Chunk)&&
                ReferenceEquals(Chunk.GetTileEntity(Station.localChunkPos),Station)&&MarkerMatches(candidate,Chunk)&&
                RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(candidate.World,Station,out var current)&&current==fence&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(candidate.Path,candidate.Receipt);}
            catch{return false;}
        }
        internal bool Holds(World world,TileEntityWorkstation station)=>!Adopted&&!closed&&!candidate.Closed&&ReferenceEquals(candidate.World,world)&&ReferenceEquals(Station,station);
        internal bool SameWorld(World world)=>!closed&&!candidate.Closed&&ReferenceEquals(candidate.World,world);
        internal byte[] CopyOriginal()=>Current()?(byte[])candidate.Original.Clone():null;
        internal string ReceiptPath=>candidate.Path;internal byte[] CopyReceipt()=>Current()?(byte[])candidate.Receipt.Clone():null;
        internal string MarkerKey=>candidate.MarkerKey;
        internal byte[] MarkerImage=>candidate.Marker;
        internal static LoadedStation FromActualRead(ColdStationRead read)=>loadingRecyclerStations?.Entries?.Contains(read)==true&&read.StreamObserved&&read.MarkerObserved&&
            read.ReadReturned&&read.Instantiated&&read.Reads==1&&!read.Published?new LoadedStation(read.Candidate,read):null;
        public void Dispose(){if(closed)return;closed=true;loadedRecyclerStations.Remove(this);candidate.Dispose();}
    }
    internal sealed class ColdStationRead
    {
        internal ColdStationCandidate Candidate;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;
        internal bool StreamObserved,MarkerObserved,Instantiating,Instantiated,ReadReturned,Published;internal int Reads;
        internal long Start,Length,Fence;
    }
    private sealed class ColdStationBatch
    {internal ColdStationRead[] Entries;internal World World;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;internal string Directory;internal int X,Z;
        internal PooledBinaryReader CleanupReader;internal long CleanupLength;internal readonly List<CleanupNativeRead> CleanupReads=new List<CleanupNativeRead>();}
    private static readonly object coldStationGate=new object();
    private static readonly List<LoadedStation> loadedRecyclerStations=new List<LoadedStation>();
    [ThreadStatic]private static ColdStationBatch loadingRecyclerStations;
    [ThreadStatic]private static ColdStationRead instantiatingRecyclerStation;
    private static bool MarkerMatches(ColdStationCandidate source,Chunk chunk)=>chunk?.ChunkCustomData?.dict!=null&&
        chunk.ChunkCustomData.dict.TryGetValue(source.MarkerKey,out var marker)&&marker.TriggerWriteDataDelegate==null&&
        marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.data!=null&&marker.data.SequenceEqual(source.Marker);
    // A primary receipt merely selects the exact saved bytes to watch for. It
    // cannot produce LoadedStation without the entire actual native read chain.
    private static ColdStationCandidate ReadColdStationCandidate(string path,World world,int x,int z)
    {
        IDisposable memory=null;
        try
        {
            long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
            if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+64L*1024*1024),out memory))return null;
            using var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)return null;
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var original=record?.Element("before")?.Elements().SingleOrDefault();
            var saved=record?.Element("recyclerPlacedStationSaved");
            if(record?.Name!="workstationCompletionIntent"||original==null||original.Name!="placedRecyclerOriginal"&&original.Name!="recyclerClosedAuthorityOriginal"||saved==null||
                !Guid.TryParseExact((string)original.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                (string)record.Attribute("operation")!=operation.ToString("N")||(string)original.Attribute("world")!=RebirthWorkstationCompletionOutbox.CurrentGuid(world)||
                (string)record.Attribute("world")!=(string)original.Attribute("world"))return null;
            var position=new Vector3i((int)original.Attribute("x"),(int)original.Attribute("y"),(int)original.Attribute("z"));
            string stage=(string)saved.Attribute("stage");if((position.x>>4)!=x||(position.z>>4)!=z||stage!="before"&&stage!="after")return null;
            byte[] tile=ColdBytes(saved.Element("nativePersistency"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes+32),
                marker=ColdBytes(saved.Element("marker"),65536),span=ColdBytes(saved.Element("nativeMarkerSpan"),131072);
            if(tile==null||marker==null||span==null||tile.Length!=(int)saved.Attribute("tileLength")||
                RebirthStationPublicationRecord.Digest(tile)!=(string)saved.Attribute("tileDigest")||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(tile,out var native)||
                native.LocalPosition!=new Vector3i(position.x&15,position.y,position.z&15))return null;
            byte[] body=ColdBytes(original.Element(stage=="after"?"afterNative":"beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
            int offset=(int)original.Attribute(stage=="after"?"afterOffset":"beforeOffset");
            if(body==null||offset<13||offset>body.Length-8||
                !RebirthProductiveEarnedNativeBody.Encode(native,BitConverter.ToUInt64(body,offset),out int encodedOffset).SequenceEqual(body)||encodedOffset!=offset)return null;
            var header=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(marker),65536);
            byte[] originalBytes=Encoding.UTF8.GetBytes(original.ToString(SaveOptions.DisableFormatting));
            if((string)header.Attribute("operation")!=operation.ToString("N")||
                (string)header.Attribute("owner")!=(string)original.Attribute("owner")||(string)header.Attribute("creation")!=(string)original.Attribute("creation")||
                (int?)header.Attribute("deaths")!=(int?)original.Attribute("deaths")||
                !(header.Name=="recyclerPlacedNativeOriginal"&&original.Name=="placedRecyclerOriginal"&&
                    (string)header.Attribute("original")==RebirthWorkstationCompletionOriginal.Hash(Encoding.UTF8.GetString(originalBytes))||
                  MatchesClosedPendingMarker(header,original)))return null;
            var receipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(receipt==null||receipt.LongLength!=length)return null;
            var result=new ColdStationCandidate{World=world,Save=Path.GetFullPath(GameIO.GetSaveGameDir()),WorldKey=(string)original.Attribute("world"),
                Path=path,Stage=stage,Operation=operation,Position=position,Receipt=receipt,Tile=tile,Marker=marker,MarkerSpan=span,Original=originalBytes,
                MarkerKey="RebirthRecyclerPlacedV1:"+(position.x&15)+":"+position.y+":"+(position.z&15)+":"+operation.ToString("N"),Memory=memory};
            memory=null;return result;
        }
        catch{return null;}
        finally{memory?.Dispose();}
    }
    private static byte[] ColdBytes(XElement node,int maximum)
    {
        if(node==null||node.HasAttributes||node.HasElements||node.Value.Length>(long)maximum*4/3+4)return null;
        var bytes=Convert.FromBase64String(node.Value);return bytes.Length<=maximum&&Convert.ToBase64String(bytes)==node.Value?bytes:null;
    }
    internal static object EnterRecyclerNativeLoad(ChunkSnapshotUtil util,string dir,long key,bool run)
    {
        if(!authorityInstalled||!run||loadingRecyclerStations!=null)return null;
        try
        {
            var world=GameManager.Instance?.World;if(world==null||world.IsRemote()||util?.chunkReader?.regionFileAccess is not RegionFileAccessSectorBased||
                world.ChunkCache?.ChunkProvider is not ChunkProviderGenerateWorld provider||!ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,util)||
                !ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,util.chunkReader.regionFileAccess)||Path.GetFullPath(dir)!=Path.GetFullPath(provider.m_RegionFileManager.saveDirectory))return null;
            string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","Survivor","WorkstationCompletions","recyclerPlaced");if(!Directory.Exists(root))return null;
            int x=WorldChunkCache.extractX(key),z=WorldChunkCache.extractZ(key);
            var batch=new ColdStationBatch{Entries=new ColdStationRead[0],World=world,Util=util,Access=util.chunkReader.regionFileAccess,Directory=Path.GetFullPath(dir),X=x,Z=z};
            loadingRecyclerStations=batch;return batch;
        }
        catch{return null;}
    }
    private static bool HasUniqueNativeSpan(Stream stream,byte[] span)
    {
        if(stream==null||span==null||span.Length<1||!stream.CanRead||!stream.CanSeek)return false;
        long start=stream.Position;int found=0,matched=0;var fallback=new int[span.Length];
        for(int i=1,j=0;i<span.Length;i++){while(j>0&&span[i]!=span[j])j=fallback[j-1];if(span[i]==span[j])j++;fallback[i]=j;}
        try{stream.Position=0;int next;while((next=stream.ReadByte())>=0){while(matched>0&&next!=span[matched])matched=fallback[matched-1];
            if(next==span[matched])matched++;if(matched==span.Length){if(++found>1)return false;matched=fallback[matched-1];}}return found==1;}
        finally{stream.Position=start;}
    }
    internal static void ObserveRecyclerNativeLoadStream(RegionFileChunkReader util,PooledBinaryReader reader,string dir,int x,int z,string ext,uint version,bool run)
    {
        var batch=loadingRecyclerStations;if(!run||batch==null||reader==null)return;
        try
        {
            if(batch.Entries.Length!=0||!ReferenceEquals(batch.Util.chunkReader,util)||!ReferenceEquals(util.regionFileAccess,batch.Access)||Path.GetFullPath(dir)!=batch.Directory||
                x!=batch.X||z!=batch.Z||ext!="7rg"||version!=47||!ReferenceEquals(reader,util.loadChunkReader)||
                !ReferenceEquals(reader.BaseStream,util.loadChunkMemoryStream)||reader.BaseStream.Position!=0||reader.BaseStream.Length<1||reader.BaseStream.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)return;
            batch.CleanupReader=reader;batch.CleanupLength=reader.BaseStream.Length;
            var operations=ReadNativeRecyclerMarkerOperations(reader.BaseStream);if(operations==null||operations.Length>4)return;
            string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","Survivor","WorkstationCompletions","recyclerPlaced");
            foreach(var operation in operations)
            {
                var candidate=ReadColdStationCandidate(Path.Combine(root,operation.ToString("N")+".station-after.xml"),batch.World,x,z)??
                    ReadColdStationCandidate(Path.Combine(root,operation.ToString("N")+".station-before.xml"),batch.World,x,z)??
                    ReadClosedPendingCandidate(root,operation,batch.World,x,z,reader.BaseStream);
                if(candidate==null)continue;candidate.RegionDirectory=batch.Directory;
                batch.Entries=batch.Entries.Concat(new[]{new ColdStationRead{Candidate=candidate,Util=batch.Util,Access=batch.Access}}).ToArray();
            }
        }
        catch{return;}
        foreach(var entry in batch.Entries)try
        {
            if(!ReferenceEquals(entry.Util.chunkReader,util)||!ReferenceEquals(util.regionFileAccess,entry.Access)||Path.GetFullPath(dir)!=entry.Candidate.RegionDirectory||
                x!=(entry.Candidate.Position.x>>4)||z!=(entry.Candidate.Position.z>>4)||ext!="7rg"||version!=47||
                !ReferenceEquals(reader,util.loadChunkReader)||!ReferenceEquals(reader.BaseStream,util.loadChunkMemoryStream)||reader.BaseStream.Position!=0||
                reader.BaseStream.Length<1||reader.BaseStream.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes)continue;
            entry.Reader=reader;entry.Length=reader.BaseStream.Length;entry.MarkerObserved=HasUniqueNativeSpan(reader.BaseStream,entry.Candidate.MarkerSpan);entry.StreamObserved=entry.MarkerObserved;
        }catch{}
    }
    private static Guid[] ReadNativeRecyclerMarkerOperations(Stream stream)
    {
        byte[] prefix=Encoding.ASCII.GetBytes("RebirthRecyclerPlacedV1:");var result=new HashSet<Guid>();long position=stream.Position;int matched=0,hits=0;
        var fallback=new int[prefix.Length];
        for(int i=1,n=0;i<prefix.Length;i++){while(n>0&&prefix[i]!=prefix[n])n=fallback[n-1];if(prefix[i]==prefix[n])n++;fallback[i]=n;}
        try
        {
            stream.Position=0;int next;
            while((next=stream.ReadByte())>=0)
            {
                while(matched>0&&next!=prefix[matched])matched=fallback[matched-1];
                if(next==prefix[matched])matched++;
                if(matched!=prefix.Length)continue;matched=fallback[matched-1];if(++hits>64)return null;long start=stream.Position;
                try
                {
                    var text=new StringBuilder(80);int separators=0;
                    while(text.Length<96&&(next=stream.ReadByte())>=0)
                    {
                        char c=(char)next;if(separators<3){if(c==':')separators++;else if(c!='-'&&(c<'0'||c>'9'))break;text.Append(c);continue;}
                        if(!Uri.IsHexDigit(c))break;text.Append(c);if(text.ToString().Split(':').Last().Length==32)break;
                    }
                    var parts=text.ToString().Split(':');if(parts.Length==4&&int.TryParse(parts[0],out int lx)&&lx>=0&&lx<16&&
                        int.TryParse(parts[1],out _)&&int.TryParse(parts[2],out int lz)&&lz>=0&&lz<16&&Guid.TryParseExact(parts[3],"N",out var operation)&&operation!=Guid.Empty)result.Add(operation);
                }
                finally{stream.Position=start;}
            }
            return result.ToArray();
        }
        finally{stream.Position=position;}
    }
    private static bool MatchesNativeTileSpan(Stream stream,byte[] tile)
    {
        if(stream==null||tile==null||!stream.CanRead||!stream.CanSeek||stream.Position<0||stream.Position>stream.Length-tile.Length)return false;
        long start=stream.Position;try{for(int i=0;i<tile.Length;i++)if(stream.ReadByte()!=tile[i])return false;return true;}finally{stream.Position=start;}
    }
    internal static object EnterRecyclerNativeInstantiate(PooledBinaryReader reader,StreamModeRead mode,TileEntityType type,Chunk chunk,bool run)
    {
        if(!run||loadingRecyclerStations==null||instantiatingRecyclerStation!=null||mode!=StreamModeRead.Persistency||type!=TileEntityType.Workstation||chunk==null)return null;
        var matches=loadingRecyclerStations.Entries.Where(value=>value.StreamObserved&&!value.Instantiated&&ReferenceEquals(reader,value.Reader)&&MatchesNativeTileSpan(reader.BaseStream,value.Candidate.Tile)).ToArray();
        if(matches.Length!=1)return matches.Length==0?EnterCleanupNativeInstantiate(reader,chunk):null;var entry=matches[0];entry.Start=reader.BaseStream.Position;entry.Chunk=chunk;entry.Instantiating=true;instantiatingRecyclerStation=entry;return entry;
    }
    internal static object EnterRecyclerPersistencyRead(TileEntityWorkstation tile,PooledBinaryReader reader,StreamModeRead mode,bool run)
    {
        var entry=instantiatingRecyclerStation;if(entry==null)return EnterCleanupNativeRead(tile,reader,mode,run);
        if(!run||!entry.Instantiating||entry.Reads!=0||mode!=StreamModeRead.Persistency||
            !ReferenceEquals(reader,entry.Reader)||!ReferenceEquals(tile.GetChunk(),entry.Chunk)||reader.BaseStream.Position!=entry.Start||!MatchesNativeTileSpan(reader.BaseStream,entry.Candidate.Tile))return null;
        entry.Tile=tile;entry.Reads++;return entry;
    }
    internal static void ObserveRecyclerPersistencyRead(PooledBinaryReader reader,bool run,object state)
    {
        if(state is CleanupNativeRead cleanup){ObserveCleanupNativeRead(reader,run,cleanup);return;}
        if(state is ColdStationRead entry&&run&&ReferenceEquals(instantiatingRecyclerStation,entry)&&ReferenceEquals(reader,entry.Reader)&&entry.Reads==1&&
            reader.BaseStream.Position==entry.Start+entry.Candidate.Tile.Length&&RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeReadFence(entry.Tile,out entry.Fence))entry.ReadReturned=true;
    }
    internal static void ObserveRecyclerNativeInstantiate(TileEntity tile,PooledBinaryReader reader,Chunk chunk,bool run,object state)
    {
        if(state is CleanupNativeRead cleanup){ObserveCleanupNativeInstantiate(tile,reader,chunk,run,cleanup);return;}
        if(state is ColdStationRead entry&&run&&ReferenceEquals(instantiatingRecyclerStation,entry)&&entry.ReadReturned&&entry.Reads==1&&
            ReferenceEquals(tile,entry.Tile)&&ReferenceEquals(reader,entry.Reader)&&ReferenceEquals(chunk,entry.Chunk)&&
            ReferenceEquals(tile.GetChunk(),chunk)&&reader.BaseStream.Position==entry.Start+entry.Candidate.Tile.Length)entry.Instantiated=true;
    }
    internal static void EndRecyclerNativeInstantiate(object state,Exception error)
    {if(state is CleanupNativeRead cleanup){EndCleanupNativeInstantiate(cleanup,error);return;}
        if(state is ColdStationRead entry){entry.Instantiating=false;if(error!=null)entry.Instantiated=false;if(ReferenceEquals(instantiatingRecyclerStation,entry))instantiatingRecyclerStation=null;}}
    internal static void ObserveRecyclerNativeLoad(Chunk chunk,bool run,object state)
    {
        if(!run||state is not ColdStationBatch batch||!ReferenceEquals(loadingRecyclerStations,batch))return;
        ObserveCleanupNativeLoad(chunk,batch);
        lock(coldStationGate)foreach(var entry in batch.Entries)
        {
            if(!entry.StreamObserved||!entry.MarkerObserved||!entry.Instantiated||!entry.ReadReturned||entry.Reads!=1||
                !ReferenceEquals(chunk,entry.Chunk)||!ReferenceEquals(chunk.GetTileEntity(entry.Tile.localChunkPos),entry.Tile)||
                entry.Reader.BaseStream.Position!=entry.Length||!MarkerMatches(entry.Candidate,chunk))continue;
            var loaded=LoadedStation.FromActualRead(entry);if(loaded==null)continue;loadedRecyclerStations.Add(loaded);entry.Published=true;
        }
    }
    internal static void EndRecyclerNativeLoad(object state)
    {
        if(state is not ColdStationBatch batch||batch.Entries==null)return;
        if(ReferenceEquals(loadingRecyclerStations,batch)){loadingRecyclerStations=null;instantiatingRecyclerStation=null;}
        foreach(var entry in batch.Entries){if(!entry.Published)entry.Candidate.Dispose();entry.Reader=null;entry.Tile=null;entry.Chunk=null;entry.Candidate=null;entry.Util=null;entry.Access=null;}
        EndCleanupNativeLoad(batch);batch.CleanupReader=null;
        batch.Entries=null;
    }
    private static bool ColdStationHeld(TileEntityWorkstation station)
    {lock(coldStationGate)return HasNativeOriginalMarker(station)||loadedRecyclerStations.Any(value=>value.Holds(GameManager.Instance?.World,station));}
    private static bool HasNativeOriginalMarker(TileEntityWorkstation station)
    {
        if(!authorityInstalled||station==null||station is RebirthCamperStationState)return false;
        var world=GameManager.Instance?.World;if(world==null||!ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station))return false;
        string prefix="RebirthRecyclerPlacedV1:"+station.localChunkPos.x+":"+station.localChunkPos.y+":"+station.localChunkPos.z+":";
        // Metadata may refuse unknown custody, never restore or authorize it.
        return station.GetChunk()?.ChunkCustomData?.dict?.Keys.Any(key=>key.StartsWith(prefix,StringComparison.Ordinal))==true;
    }
    private static void SweepLoadedRecyclerStations(World world)
    {
        lock(coldStationGate)foreach(var source in loadedRecyclerStations.Where(value=>!value.SameWorld(world)).ToArray())source.Dispose();
    }
}
