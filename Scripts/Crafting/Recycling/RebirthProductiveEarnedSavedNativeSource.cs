using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// A saved marker is a candidate locator only. Evidence becomes an installed
// source exclusively through the region-load/Instantiate/Persistency callbacks
// in the other partial. No current tile lookup issues an earned-work original.
internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    private const int MaximumRecord=RebirthProductiveEarnedRowProof.MaximumArtifactBytes+
        2*RebirthProductiveEarnedNativeBody.MaximumBodyBytes+8192;
    internal sealed class Evidence:IDisposable
    {
        internal bool Active=true,Terminal,PendingNextWork;
        internal int PendingTimerIndex=-1;internal float PendingTimerBefore,PendingTimerTarget;internal bool PendingTimerAmbiguous;
        internal string Path,Directory,WorldGuid,MarkerKey;
        internal byte[] Image,Original,Marker,Native;
        internal int NativeOffset;
        internal RebirthProductiveEarnedRowProof.Frame Frame;
        internal Guid CustodyBirth;
        internal CachedLoadRequest Load;
        internal ChunkCustomData NativeMarker;
        internal NativeChildDependency[] Dependencies;
        internal string PendingRecipeBinding,PendingExtras;
        internal IDisposable DefinitionRetention;
        internal byte[] StagedPartialRecord;
        internal byte[] StagedChildLink;
        internal byte[] ToolsBefore;internal int ToolsBeforeOffset;internal bool ToolsRebased;
        private IDisposable memory;
        private int references=1;
        internal Evidence(IDisposable retained){memory=retained;}
        internal bool PrimaryCurrent()=>Active&&Matches(Path,Image)&&NativeChildDependenciesCurrent(Dependencies);
        internal bool CanEnterNativeLoad()=>CanLoadSavedCandidate(this);
        internal bool MarkerMatches(Chunk chunk)=>NativeMarker!=null&&MarkerBytesMatch(chunk)&&
            chunk.ChunkCustomData.dict.TryGetValue(MarkerKey,out var marker)&&ReferenceEquals(marker,NativeMarker);
        internal bool MarkerBytesMatch(Chunk chunk)=>Active&&chunk!=null&&chunk.ChunkCustomData.dict.TryGetValue(MarkerKey,out var marker)&&
            marker.TriggerWriteDataDelegate==null&&
            !marker.isSavedToNetwork&&marker.expiresInWorldTime==ulong.MaxValue&&marker.data!=null&&marker.data.SequenceEqual(Marker);
        internal bool CurrentIdentity(World world,TileEntityWorkstation station,Guid birth,Guid operation)
            =>Active&&Frame?.Birth==birth&&Frame.Operation==operation&&Load?.Loaded?.Installed(world)==true&&
                ReferenceEquals(Load.Loaded.Tile,station)&&NativeMarker!=null&&
                (CustodyBirth==Guid.Empty||RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var current)&&current==CustodyBirth.ToString("N"));
        internal IDisposable RetainNativeLoad()
        {lock(this){if(!Active||memory==null)return null;references++;return new Lease(this);}}
        private sealed class Lease:IDisposable
        {private Evidence value;internal Lease(Evidence source){value=source;}public void Dispose(){System.Threading.Interlocked.Exchange(ref value,null)?.Release();}}
        private void Release()
        {
            lock(this)
            {
                if(--references!=0)return;
                Image=Original=Marker=Native=ToolsBefore=null;Frame=null;NativeMarker=null;Dependencies=null;
                StagedPartialRecord=null;
                StagedChildLink=null;
                PendingRecipeBinding=PendingExtras=null;var definition=DefinitionRetention;DefinitionRetention=null;
                if(Load!=null){Load.Snapshot=Load.Tile=null;Load.Loaded=null;}Load=null;
                var retained=memory;memory=null;
                try{definition?.Dispose();}finally{retained?.Dispose();}
            }
        }
        public void Dispose(){lock(this){if(!Active)return;Active=false;Release();}}
    }
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;
        RebirthProductiveEarnedNativeBody.Install();
        RebirthCookingMillingPublication.InstallMobileNativeSourceObservers();
        RebirthProductiveEarnedCoordinator.InstallMobileToolSourceObserver();
        RebirthProductiveEarnedPendingToolGestures.Install();
        RebirthProductiveEarnedRemoteHost.InstallNativeUploadObserver();
        RebirthRemoteResourcePlacedStationOriginal.InstallCompletionCachedNativeLoadObservers();
        ModEvents.GameStarting.RegisterHandler(Starting);installed=true;
    }
    private static void Starting(ref ModEvents.SGameStartingData data)
    {
        lock(cachedLoadGate)
        {
            ResetCompletedNativeLoads();
            foreach(var request in cachedLoadRequests)request.Custody.Dispose();cachedLoadRequests.Clear();
            try
            {
                string save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),root=System.IO.Path.Combine(save,"RebirthProductiveEarned");
                ConfigureStartupRefill(save);
                if(!RebirthProductiveEarnedArtifact.SafePath(root)||!System.IO.Directory.Exists(root))return;
                ScanStartupCandidates(save,root);
            }
            catch{/* Unknown originals remain on disk and their markers block. */}
        }
    }
    private static void ReadCandidate(string path,string save,bool starting=false,bool replacePending=false,Evidence preserve=null)
    {
        Evidence source=null;
        try
        {
            if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return;
            long length=new FileInfo(path).Length;
            if(length<128||length>MaximumRecord||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*length+65536),out var allocation))return;
            source=new Evidence(allocation){Path=path,Directory=save};
            source.Image=Read(path,MaximumRecord);
            using(var stream=new MemoryStream(source.Image,false))
            using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadUInt32()!=0x324C4552)return; // REL2, exact saved native tile, no unrelated chunk copy.
                source.Terminal=reader.ReadBoolean();source.PendingNextWork=reader.ReadBoolean();source.WorldGuid=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                if(source.PendingNextWork&&!source.Terminal)return;
                source.PendingTimerIndex=reader.ReadInt32();source.PendingTimerBefore=reader.ReadSingle();source.PendingTimerTarget=reader.ReadSingle();source.PendingTimerAmbiguous=reader.ReadBoolean();
                if(source.PendingTimerIndex< -1||float.IsNaN(source.PendingTimerBefore)||float.IsInfinity(source.PendingTimerBefore)||
                    float.IsNaN(source.PendingTimerTarget)||float.IsInfinity(source.PendingTimerTarget)||
                    !source.PendingNextWork&&(source.PendingTimerIndex!=-1||source.PendingTimerBefore!=0||source.PendingTimerTarget!=0||source.PendingTimerAmbiguous))return;
                if(!Guid.TryParse(source.WorldGuid,out var rawWorld)||rawWorld==Guid.Empty)return;
                int x=reader.ReadInt32(),z=reader.ReadInt32();
                source.MarkerKey=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,128);
                source.Original=ReadBytes(reader,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                source.Frame=RebirthProductiveEarnedRowProof.Decode(source.Original);
                string suffix=source.Terminal&&path.EndsWith(".tools-load",StringComparison.Ordinal)?".tools-load":source.Terminal&&path.EndsWith(".successor-load",StringComparison.Ordinal)?".successor-load":
                    source.Terminal?".terminal-load":".earned-load";
                if(!source.Frame.HasNativeBodies||!source.Frame.HasRecipeDefinition||Path.GetFileName(path)!=source.Frame.Operation.ToString("N")+".rew"+suffix||
                    suffix==".successor-load"&&source.PendingNextWork||
                    Path.GetFileName(Path.GetDirectoryName(path))!=source.Frame.Birth.ToString("N"))return;
                source.Marker=ReadBytes(reader,256);
                if(!RebirthProductiveEarnedRowProof.MatchesMarker(source.Marker,source.Original))return;
                source.NativeOffset=reader.ReadInt32();source.Native=ReadBytes(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                string savedPath=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,1024);
                string payloadDigest=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
                long offset=reader.ReadInt64();var tile=ReadBytes(reader,RebirthProductiveEarnedNativeBody.MaximumBodyBytes+18);
                if(stream.Position!=stream.Length||offset<8||string.IsNullOrEmpty(savedPath)||!RebirthProductiveEarnedArtifact.SafePath(savedPath)||
                    payloadDigest.Length!=64||!payloadDigest.All(c=>c>='0'&&c<='9'||c>='A'&&c<='F')||
                    !RebirthProductiveEarnedNativeBody.TryRead(source.Native,source.NativeOffset,out _,out _))return;
                if(!source.Terminal)
                {
                    string original=path.Substring(0,path.Length-suffix.Length);
                    if(!TryInitialEarnedBody(original,source.Original,source.Frame,out var earned,out int earnedOffset,out var dependency)||
                        !RebirthProductiveEarnedNativeBody.SameExceptElapsed(source.Native,source.NativeOffset,earned,earnedOffset))return;
                    if(dependency!=null)source.Dependencies=(source.Dependencies??Array.Empty<NativeChildDependency>()).Concat(new[]{dependency}).ToArray();
                }
                source.Load=new CachedLoadRequest{Custody=source,Directory=save,X=x,Z=z,Snapshot=Array.Empty<byte>(),Tile=tile,Offset=offset};
            }
            if(source.Terminal&&!source.PendingNextWork)
            {
                // Later native saves after marker removal may have different
                // clocks. Complete terminals compare their full normalized
                // native body, under measured parser bounds, on the ACTUAL
                // returned read. This never qualifies an earned-row source.
                if(source.Load.Tile.Length!=source.Native.Length+10||
                    !RebirthProductiveEarnedNativeBody.TryMeasure(source.Native,source.NativeOffset,out long graph,out long scratch)||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(allocation,
                        checked(8L*length+4L*source.Native.Length+3*graph+scratch+65536)))return;
                source.Load.NormalizedTerminal=true;source.Load.MaximumGraph=graph;source.Load.MaximumScratch=scratch;
            }
            if(!TryReadToolRebase(source)||!source.PrimaryCurrent()||!TryReadPendingDefinition(source))return;
            if(starting&&TryReadInvokedNativePartial(source,out var partial)) {source.Dispose();source=partial;}
            else if(starting&&TryReadInvokedNativeChild(source,out var child)) {source.Dispose();source=child;}
            source.Load.StartupOrigin=path;
            if(replacePending&&(!source.Load.PredictedNative||!source.Terminal||source.PendingNextWork))return;
            if(!source.CanEnterNativeLoad())return;
            if(starting)
            {
                var same=cachedLoadRequests.Where(value=>value.Custody.Frame.Birth==source.Frame.Birth&&
                    value.Custody.Frame.Operation==source.Frame.Operation).ToArray();
                if(source.Load.PredictedNative&&same.Length>0)
                {
                    if(!replacePending||!source.Terminal||source.PendingNextWork||same.Length!=1||
                        !same[0].Custody.Terminal||!same[0].Custody.PendingNextWork||
                        !string.Equals(same[0].Custody.Path,source.Path,RebirthStationCompletionOriginalScope.PathComparison))return;
                    cachedLoadRequests.Remove(same[0]);if(!ReferenceEquals(same[0].Custody,preserve))same[0].Custody.Dispose();
                }
                foreach(var prior in same.Where(value=>value.PredictedNative))
                    if(cachedLoadRequests.Remove(prior)&&!ReferenceEquals(prior.Custody,preserve))prior.Custody.Dispose();
            }
            cachedLoadRequests.Add(source.Load);source=null;
        }
        catch{/* Parsing retained evidence never grants a live row. */}
        finally{source?.Dispose();}
    }
    internal static bool Publish(RebirthProductiveEarnedArtifact.Prepared artifact,
        RebirthProductiveEarnedObservationBudget memory,RebirthRemoteResourceNativePublication.Watch watch,
        byte[] native,int nativeOffset,bool terminal,Func<bool> current,bool pendingNextWork=false,
        int pendingTimerIndex=-1,float pendingTimerBefore=0,float pendingTimerTarget=0,bool pendingTimerAmbiguous=false,bool partialSuccessor=false,bool toolsRebase=false)
    {
        try
        {
            if(current?.Invoke()!=true||artifact?.Current()!=true||memory==null||watch==null||partialSuccessor&&(!terminal||pendingNextWork)||toolsRebase&&(!terminal||!pendingNextWork||partialSuccessor))return false;
            if(!terminal&&artifact.LoadedSource is Evidence loaded)
                return !loaded.Terminal&&loaded.PrimaryCurrent()&&loaded.CurrentIdentity(GameManager.Instance?.World,
                    watch.Tile as TileEntityWorkstation,artifact.Birth,artifact.Operation);
            using(var callbacks=memory.TryEnterCallbacks())
            {
                if(callbacks==null||!watch.TryReadPublishedTileDescriptor(out var descriptor,out _,out int tileLength,out _)||
                    descriptor.Path==null||Encoding.UTF8.GetByteCount(descriptor.Path)>1024||
                    !memory.TryReserveAllocation(checked(4L*(artifact.ImageLength+native.Length+tileLength+8192)+4096),out var workspace))return false;
                using(workspace)
                {
                if(!watch.TryCopyPublishedTileSpan(out var saved,out long savedOffset,out var tile,out var tileAllocation))return false;
                using(tileAllocation)
                {
                if(current()!=true)return false;
                var station=watch.Tile as TileEntityWorkstation;var chunk=station?.GetChunk();if(chunk==null)return false;
                var frame=artifact.ReadFrame();var p=station.localChunkPos;
                byte[] record;
                using(var buffer=new MemoryStream(checked(artifact.ImageLength+native.Length+tile.Length+8192)))using(var writer=new BinaryWriter(buffer,new UTF8Encoding(false,true)))
                {
                    writer.Write(0x324C4552u);writer.Write(terminal);writer.Write(pendingNextWork);writer.Write(RebirthWorkstationCompletionOutbox.CurrentGuid(GameManager.Instance.World));
                    writer.Write(pendingTimerIndex);writer.Write(pendingTimerBefore);writer.Write(pendingTimerTarget);writer.Write(pendingTimerAmbiguous);
                    writer.Write(chunk.X);writer.Write(chunk.Z);writer.Write("RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z);
                    Write(writer,artifact.CopyImage());Write(writer,artifact.Marker(frame));writer.Write(nativeOffset);Write(writer,native);
                    writer.Write(saved.Path);writer.Write(saved.PayloadDigest);writer.Write(savedOffset);Write(writer,tile);writer.Flush();
                    if(buffer.Length>MaximumRecord)return false;record=buffer.ToArray();
                }
                string path=artifact.OriginalPath+(toolsRebase?".tools-load":partialSuccessor?".successor-load":terminal?".terminal-load":".earned-load");
                if(!WriteNew(path,record)||!current())return false;
                // Register while the native tile is still resident. GameStarting
                // alone cannot observe an unload/reload within this same world.
                // Registration is only saved custody: Loaded stays null until
                // the actual region/factory/read chain returns.
                return (terminal?RegisterPublishedTerminal(path,GameIO.GetSaveGameDir(),record,artifact.LoadedSource):
                    RegisterPublishedEarned(path,GameIO.GetSaveGameDir(),record))&&current();
                }
                }
            }
        }
        catch{return false;}
    }
    internal static bool TryObserved(TileEntityWorkstation station,out Evidence source)
    {
        source=null;var world=GameManager.Instance?.World;if(world?.IsRemote()!=false)return false;
        lock(cachedLoadGate)
        {
            var matches=cachedLoadRequests.Where(request=>request.Loaded?.Installed(world)==true&&ReferenceEquals(request.Loaded.Tile,station)).ToArray();
            if(matches.Length!=1)return false;source=matches[0].Custody;return source.PrimaryCurrent();
        }
    }
    internal static void Release(Evidence source)
    {if(source==null)return;lock(cachedLoadGate){if(cachedLoadRequests.Remove(source.Load))MarkStartupRefillNeeded();source.Dispose();}}
    private static byte[] ReadBytes(BinaryReader reader,int maximum)
    {int length=reader.ReadInt32();if(length<1||length>maximum||length>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException();var result=reader.ReadBytes(length);if(result.Length!=length)throw new EndOfStreamException();return result;}
    private static void Write(BinaryWriter writer,byte[] bytes){if(bytes==null)throw new InvalidDataException();writer.Write(bytes.Length);writer.Write(bytes);}
    private static byte[] Read(string path,int maximum)
    {
        if(!RebirthProductiveEarnedArtifact.SafePath(path))throw new InvalidDataException();
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(file.Length<1||file.Length>maximum)throw new InvalidDataException();var result=new byte[(int)file.Length];int offset=0;while(offset<result.Length){int read=file.Read(result,offset,result.Length-offset);if(read<1)throw new EndOfStreamException();offset+=read;}if(file.ReadByte()!=-1||!RebirthProductiveEarnedArtifact.SafePath(path))throw new InvalidDataException();return result;}
    }
    private static bool Matches(string path,byte[] image)
    {
        try
        {
            if(image==null||!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return false;
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {if(file.Length!=image.Length)return false;var buffer=new byte[4096];int offset=0;while(offset<image.Length){int read=file.Read(buffer,0,Math.Min(buffer.Length,image.Length-offset));if(read<1)return false;for(int i=0;i<read;i++)if(buffer[i]!=image[offset+i])return false;offset+=read;}return file.ReadByte()==-1&&RebirthProductiveEarnedArtifact.SafePath(path);}
        }
        catch{return false;}
    }
    private static bool WriteNew(string path,byte[] image)
    {
        if(image==null||image.Length>MaximumRecord||!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return false;
        if(File.Exists(path))return Matches(path,image);
        using(var file=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(image,0,image.Length);file.Flush(true);}
        if(!Matches(path+".tmp",image))return false;File.Move(path+".tmp",path);return Matches(path,image);
    }
}
