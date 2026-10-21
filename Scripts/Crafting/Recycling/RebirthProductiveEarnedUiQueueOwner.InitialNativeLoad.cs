using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;using System.Security.Cryptography;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private sealed class InitialReference{internal Guid Source;internal string Birth;}
    private sealed class InitialSavedNative
    {internal long Offset;internal Vector3i Position;internal int SnapshotLength,TileLength;internal string SnapshotDigest,TileDigest;}
    private sealed class InitialNativeOriginal:IDisposable
    {
        internal World World;internal string Save,Region,ReceiptPath,ReceiptDigest,OriginalPath,OriginalDigest,Owner,Creation,WorldKey;
        internal int Entity,Deaths;internal IRegionFileChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;internal InitialReference Reference;internal InitialSavedNative Native;
        internal Chunk Chunk;internal TileEntityWorkstation Tile;internal RecipeQueueItem Row;internal int Slot,OriginalCount;internal string RowBinding,RowBindingDigest;
        internal ChunkCustomData Marker;internal byte[] MarkerImage;internal string MarkerKey;internal IDisposable Memory;internal long Fence;internal bool Installed;internal volatile bool Closed;internal int Loads;
        internal bool LoadCurrent()=>!Closed&&ReferenceEquals(World,GameManager.Instance?.World)&&World?.IsRemote()==false&&Save==Path.GetFullPath(GameIO.GetSaveGameDir())&&
            World.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&ReferenceEquals(provider.m_RegionFileManager?.snapshotUtil,Util)&&ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,Access);
        internal bool Current()=>ThreadManager.IsMainThread()&&LoadCurrent();
        internal bool LoadedCurrent()=>Current()&&Installed&&Loads==0&&Chunk!=null&&Tile!=null&&ReferenceEquals(World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&
            !Chunk.InProgressUnloading&&ReferenceEquals(Chunk.GetTileEntity(Tile.localChunkPos),Tile)&&ReferenceEquals(Tile.GetChunk(),Chunk)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Tile,out var birth)&&birth==Reference.Birth&&
            Chunk.ChunkCustomData.dict.TryGetValue(MarkerKey,out var marker)&&ReferenceEquals(marker,Marker)&&marker.TriggerWriteDataDelegate==null&&!marker.isSavedToNetwork&&
            marker.expiresInWorldTime==ulong.MaxValue&&marker.data!=null&&marker.data.SequenceEqual(MarkerImage)&&
            RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Tile,out var fence)&&fence==Fence&&InitialDescriptorCurrent(this);
        internal bool OriginalRowCurrent()=>LoadedCurrent()&&Slot>=0&&Slot<Tile.Queue.Length&&ReferenceEquals(Tile.Queue[Slot],Row)&&Row?.Recipe!=null&&
            Row.Multiplier>0&&Row.Multiplier<=OriginalCount&&NativeBinding(Row)==RowBinding;
        public void Dispose(){lock(initialLoadGate){Closed=true;Release();}}
        internal void Release(){if(!Closed||Loads!=0)return;Memory?.Dispose();Memory=null;Native=null;Chunk=null;Tile=null;Row=null;Marker=null;MarkerImage=null;}
    }
    private sealed class InitialNativeLoading
    {internal InitialNativeOriginal Original;internal PooledBinaryReader Reader;internal Chunk Chunk;internal TileEntityWorkstation Tile;internal bool Stream,Factory,Returned,Instantiated;internal int Reads;internal long Fence;}
    private static readonly List<InitialNativeOriginal> initialNativeOriginals=new List<InitialNativeOriginal>();
    private static readonly object initialLoadGate=new object();
    [ThreadStatic]private static InitialNativeLoading[] initialLoading;
    [ThreadStatic]private static InitialNativeLoading initialInstantiating;
    private static bool InitialSourceCurrent(object value)=>value is InitialNativeOriginal original&&original.LoadedCurrent();
    private static string InitialFileHash(string path,long maximum,int prefix=0)
    {
        if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=System.Security.Cryptography.SHA256.Create())
        {if(stream.Length<=prefix||stream.Length>maximum)throw new InvalidDataException();stream.Position=prefix;return Convert.ToBase64String(sha.ComputeHash(stream));}
    }
    private static bool InitialReceiptCurrent(InitialNativeOriginal source)
    {
        try{return source.LoadCurrent()&&InitialFileHash(source.ReceiptPath,8192)==source.ReceiptDigest&&
            InitialFileHash(source.OriginalPath,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)==source.OriginalDigest;}
        catch{return false;}
    }
    private static bool InitialDescriptorCurrent(InitialNativeOriginal source)
    {try{return source.LoadCurrent()&&InitialFileHash(source.ReceiptPath,8192)==source.ReceiptDigest;}catch{return false;}}
    internal static void DiscoverInitialQueueNativeOrigins(RegionFileManager manager)
    {
        if(!installed||!ThreadManager.IsMainThread()||GameManager.Instance?.World is not World world||world.IsRemote()||manager?.snapshotUtil==null||
            manager.regionFileAccess is not RegionFileAccessSectorBased||Path.GetFullPath(manager.saveDirectory)!=Path.GetFullPath(GameIO.GetSaveGameRegionDir()))return;
        lock(initialLoadGate)
        {
            foreach(var old in initialNativeOriginals.Where(value=>!ReferenceEquals(value.World,world)||!ReferenceEquals(value.Util,manager.snapshotUtil)).ToArray()){initialNativeOriginals.Remove(old);old.Dispose();}
            string root=Path.Combine(GameIO.GetSaveGameDir(),"RebirthProductiveEarnedUi","queueOwners");if(!Directory.Exists(root)||!RebirthProductiveEarnedArtifact.SafePath(root))return;
            foreach(string ownerFolder in Directory.EnumerateDirectories(root))
            {
                if(!RebirthProductiveEarnedArtifact.SafePath(ownerFolder))continue;
                foreach(string path in Directory.EnumerateFiles(ownerFolder,"*.original.initial"))
                {
                    InitialNativeOriginal value=null;
                    try
                    {
                        if(!Guid.TryParseExact(Path.GetFileName(path).Substring(0,32),"N",out var operation)||initialNativeOriginals.Any(item=>item.Reference.Source==operation)||
                            Path.GetFileName(path)!=operation.ToString("N")+".original.initial"||!RebirthProductiveEarnedArtifact.SafePath(path)||new FileInfo(path).Length>8192||File.Exists(path+".tmp"))continue;
                        byte[] bytes=File.ReadAllBytes(path);var node=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(bytes),8192);
                        string save=Path.GetFullPath(GameIO.GetSaveGameDir()),owner=(string)node.Attribute("owner"),creation=(string)node.Attribute("creation"),birth=(string)node.Attribute("birth");
                        if(node.Name!="initialNativeQueueOwner"||node.Attributes().Count()!=20||node.Elements().Count()!=2||(int?)node.Attribute("version")!=1||
                            (string)node.Attribute("operation")!=operation.ToString("N")||!Guid.TryParseExact(birth,"N",out var born)||born==Guid.Empty||
                            !RebirthOriginalActionScope.Validate(owner,creation)||Path.GetFileName(ownerFolder)!=owner||(string)node.Attribute("save")!=save||
                            (string)node.Attribute("world")!=RebirthProductiveEarnedArtifact.WorldIdentity(world)||node.Element("nativeRowDigest")?.HasAttributes!=false||node.Element("nativeRowDigest").HasElements||
                            node.Element("marker")?.Attributes().Count()!=1||node.Element("marker").HasElements)continue;
                        var native=new InitialSavedNative{Position=new Vector3i((int)node.Attribute("x"),(int)node.Attribute("y"),(int)node.Attribute("z")),Offset=(long)node.Attribute("offset"),
                            TileLength=(int)node.Attribute("tileLength"),SnapshotLength=(int)node.Attribute("snapshotLength"),SnapshotDigest=(string)node.Attribute("snapshotDigest"),TileDigest=(string)node.Attribute("tileDigest")};
                        int slot=(int)node.Attribute("slot"),count=(int)node.Attribute("count"),entity=(int)node.Attribute("entity"),deaths=(int)node.Attribute("deaths");
                        string marker="RebirthLogisticsStationV1:"+(native.Position.x&15)+":"+native.Position.y+":"+(native.Position.z&15);
                        if(slot<0||slot>byte.MaxValue||count<1||count>short.MaxValue||entity<0||deaths<0||native.SnapshotLength<9||native.SnapshotLength>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||
                            native.TileLength<1||native.TileLength>RebirthProductiveEarnedNativeBody.MaximumBodyBytes+18||native.Offset<8||native.Offset>native.SnapshotLength-native.TileLength||
                            (string)node.Element("marker").Attribute("key")!=marker||node.Element("nativeRowDigest").Value.Length!=44||node.Element("marker").Value.Length>5464)continue;
                        string original=path.Substring(0,path.Length-".initial".Length),snapshot=path+".snapshot";
                        var markerBytes=Convert.FromBase64String(node.Element("marker").Value);if(markerBytes.Length>4096||Convert.ToBase64String(markerBytes)!=node.Element("marker").Value)continue;
                        bool hashes=true;foreach(string hash in new[]{native.SnapshotDigest,native.TileDigest,(string)node.Attribute("originalDigest"),node.Element("nativeRowDigest").Value})
                        {var decoded=Convert.FromBase64String(hash);if(decoded.Length!=32||Convert.ToBase64String(decoded)!=hash){hashes=false;break;}}if(!hashes)continue;
                        if(!RebirthProductiveEarnedArtifact.SafePath(snapshot)||!RebirthProductiveEarnedArtifact.SafePath(original)||File.Exists(snapshot+".tmp")||File.Exists(original+".tmp")||
                            new FileInfo(snapshot).Length!=native.SnapshotLength||new FileInfo(original).Length<1||new FileInfo(original).Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8192+4L*(path.Length+original.Length+save.Length+manager.saveDirectory.Length+owner.Length+creation.Length)),out var memory))continue;
                        value=new InitialNativeOriginal{World=world,Save=save,Region=Path.GetFullPath(manager.saveDirectory),Util=manager.snapshotUtil,Access=manager.regionFileAccess,
                            Reference=new InitialReference{Source=operation,Birth=birth},Native=native,ReceiptPath=path,ReceiptDigest=InitialHash(bytes),OriginalPath=original,OriginalDigest=(string)node.Attribute("originalDigest"),
                            Owner=owner,Creation=creation,WorldKey=(string)node.Attribute("world"),Entity=entity,Deaths=deaths,Slot=slot,OriginalCount=count,RowBindingDigest=node.Element("nativeRowDigest").Value,
                            MarkerKey=marker,MarkerImage=markerBytes,Memory=memory};
                        initialNativeOriginals.Add(value);value=null;
                    }
                    catch{/* Invalid historical metadata grants no native row source. */}
                    finally{value?.Dispose();}
                }
            }
        }
    }
        internal static object EnterInitialNativeLoad(ChunkSnapshotUtil util,string dir,long key,bool ran)
    {
        if(!ran||initialLoading!=null)return null;
        lock(initialLoadGate){var matches=initialNativeOriginals.Where(value=>value.LoadCurrent()&&value.Loads==0&&ReferenceEquals(value.Util,util)&&value.Region==Path.GetFullPath(dir)&&
            (value.Native.Position.x>>4)==WorldChunkCache.extractX(key)&&(value.Native.Position.z>>4)==WorldChunkCache.extractZ(key)).ToArray();
        if(matches.Length<1)return null;
        foreach(var match in matches){match.Loads++;match.Installed=false;match.Row=null;}
        return initialLoading=matches.Select(value=>new InitialNativeLoading{Original=value}).ToArray();}
    }
    internal static void ObserveInitialNativeLoadStream(RegionFileChunkReader source,PooledBinaryReader reader,string dir,int x,int z,string ext,uint version,bool ran)
    {
        if(!ran||initialLoading==null||reader?.BaseStream==null)return;
        string digest=null;
        foreach(var loading in initialLoading)
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
    internal static object EnterInitialNativeInstantiate(PooledBinaryReader reader,StreamModeRead mode,TileEntityType type,Chunk chunk,bool ran)
    {
        if(!ran||mode!=StreamModeRead.Persistency||type!=TileEntityType.Workstation||chunk==null||initialLoading==null||initialInstantiating!=null)return null;
        var matches=initialLoading.Where(value=>value.Stream&&!value.Instantiated&&ReferenceEquals(value.Reader,reader)&&
            reader.BaseStream.Position==value.Original.Native.Offset-8&&chunk.X==(value.Original.Native.Position.x>>4)&&chunk.Z==(value.Original.Native.Position.z>>4)).ToArray();
        if(matches.Length!=1)return null;var loading=matches[0];loading.Chunk=chunk;loading.Factory=true;return initialInstantiating=loading;
    }
    internal static object EnterInitialPersistencyRead(TileEntityWorkstation tile,PooledBinaryReader reader,StreamModeRead mode,bool ran)
    {
        var loading=initialInstantiating;
        if(!ran||loading==null||!loading.Factory||loading.Reads!=0||mode!=StreamModeRead.Persistency||!ReferenceEquals(reader,loading.Reader)||
            reader.BaseStream.Position!=loading.Original.Native.Offset-8||!ReferenceEquals(tile.GetChunk(),loading.Chunk))return null;
        loading.Reads++;loading.Tile=tile;return loading;
    }
    internal static void ObserveInitialPersistencyRead(PooledBinaryReader reader,bool ran,object state)
    {
        if(state is not InitialNativeLoading loading||!ran||!ReferenceEquals(loading,initialInstantiating)||!ReferenceEquals(reader,loading.Reader)||loading.Reads!=1)return;
        if(reader.BaseStream.Position==loading.Original.Native.Offset-8+loading.Original.Native.TileLength&&
            RebirthProductiveEarnedUiDeferredTransport.TryGetObservedNativeReadFence(loading.Tile,out loading.Fence))loading.Returned=true;
    }
    internal static void ObserveInitialNativeInstantiate(TileEntity result,PooledBinaryReader reader,Chunk chunk,bool ran,object state)
    {
        if(state is InitialNativeLoading loading&&ran&&ReferenceEquals(loading,initialInstantiating)&&loading.Returned&&loading.Reads==1&&
            ReferenceEquals(reader,loading.Reader)&&ReferenceEquals(result,loading.Tile)&&ReferenceEquals(chunk,loading.Chunk)&&ReferenceEquals(result?.GetChunk(),chunk)&&
            reader.BaseStream.Position==loading.Original.Native.Offset-8+loading.Original.Native.TileLength)loading.Instantiated=true;
    }
    internal static void EndInitialNativeInstantiate(object state,Exception error)
    {if(state is InitialNativeLoading loading){if(error!=null)loading.Instantiated=false;loading.Factory=false;if(ReferenceEquals(loading,initialInstantiating))initialInstantiating=null;}}
    internal static void ObserveInitialNativeLoad(Chunk result,bool ran,object state)
    {
        if(!ran||state is not InitialNativeLoading[] entries||!ReferenceEquals(entries,initialLoading))return;
        foreach(var loading in entries)
        {
            var original=loading.Original;
            if(!original.LoadCurrent()||!loading.Instantiated||!loading.Returned||loading.Reads!=1||!ReferenceEquals(result,loading.Chunk)||
                !ReferenceEquals(result?.GetTileEntity(loading.Tile.localChunkPos),loading.Tile)||!ReferenceEquals(loading.Tile.GetChunk(),result)||
                loading.Reader.BaseStream.Position!=original.Native.SnapshotLength-8||!InitialReceiptCurrent(original)||
                !result.ChunkCustomData.dict.TryGetValue(original.MarkerKey,out var marker)||marker.TriggerWriteDataDelegate!=null||marker.isSavedToNetwork||
                marker.expiresInWorldTime!=ulong.MaxValue||marker.data==null||!marker.data.SequenceEqual(original.MarkerImage))continue;
            int slot=original.Slot;
            if(loading.Tile.ToWorldPos()!=original.Native.Position||slot<0||slot>=loading.Tile.Queue.Length||loading.Tile.Queue[slot]?.Recipe==null)continue;
            string binding=RebirthProductiveEarnedUiQueueOwner.NativeBinding(loading.Tile.Queue[slot]);
            if(InitialHash(Encoding.UTF8.GetBytes(binding))!=original.RowBindingDigest||loading.Tile.Queue[slot].Multiplier!=original.OriginalCount||!OrdinaryInitial(loading.Tile.Queue[slot])||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(original.Memory,checked(8192+4L*binding.Length+4L*(original.ReceiptPath.Length+original.OriginalPath.Length+original.Save.Length+original.Region.Length+original.Owner.Length+original.Creation.Length))))continue;
            original.Chunk=result;original.Tile=loading.Tile;original.Marker=marker;original.Fence=loading.Fence;original.Slot=slot;original.Row=loading.Tile.Queue[slot];
            original.RowBinding=binding;original.Installed=true;
        }
    }
    internal static void EndInitialNativeLoad(object state,Exception error=null)
    {if(state is InitialNativeLoading[] entries){if(ReferenceEquals(entries,initialLoading))initialLoading=null;initialInstantiating=null;lock(initialLoadGate)foreach(var entry in entries){entry.Reader=null;entry.Tile=null;entry.Chunk=null;var original=entry.Original;entry.Original=null;if(original!=null){if(error!=null)original.Installed=false;original.Loads--;original.Release();}}}}

}
