using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using System.Security.Cryptography;

// Actual native saved-effect producer only. It never writes native inventory,
// reconstructs a tile, or obtains mutation permission from an absent receipt.
internal static class RebirthWorkstationReagentPublication
{
    private sealed class SnapshotBudget:IRebirthStationSnapshotByteBudget,IDisposable
    {
        private IDisposable root;private long used;private bool closed;private readonly object gate=new object();
        internal SnapshotBudget(IDisposable root){this.root=root;}
        public int MaximumSnapshotBytes=>RebirthStationSnapshotEvidence.MaximumSnapshotBytes;
        public bool TryReserveAllocation(long bytes,out IDisposable allocation)
        {allocation=null;lock(gate){if(closed||bytes<1||used>WorkspaceMaximum-bytes)return false;used+=bytes;allocation=new Charge(this,bytes);return true;}}
        private void Release(long bytes){lock(gate){used-=bytes;if(closed&&used==0){root?.Dispose();root=null;}}}
        public void Dispose(){lock(gate){closed=true;if(used==0){root?.Dispose();root=null;}}}
        private sealed class Charge:IDisposable
        {private SnapshotBudget owner;private readonly long bytes;internal Charge(SnapshotBudget owner,long bytes){this.owner=owner;this.bytes=bytes;}
         public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref owner,null);value?.Release(bytes);}}
    }
    // Body, frozen XML/base64 graphs and four bounded native spans survive
    // snapshot Reset; their reservation belongs to Pending, not Request.
    private const long RetainedMaximum=8L*RebirthProductiveEarnedNativeBody.MaximumBodyBytes+16L*4*256*1024+256*1024;
    private const long SnapshotWorkspace=12L*RebirthStationSnapshotEvidence.MaximumSnapshotBytes+4L*RebirthStationRegionPayload.MaximumPayloadBytes+1024*1024;
    private const long WorkspaceMaximum=RebirthProductiveEarnedObservationBudget.DecoderMaximum>SnapshotWorkspace?
        RebirthProductiveEarnedObservationBudget.DecoderMaximum:SnapshotWorkspace;
    private sealed class Pending:IDisposable
    {
        internal RebirthWorkstationCompletionOutbox.State State;
        internal World World;internal object WorldState;internal string WorldStateGuid,Root;
        internal TileEntityWorkstation Tile;internal Chunk Chunk;
        internal int ChunkX,ChunkZ;
        internal RebirthStationNativeInputSnapshot Input;internal RebirthStationNativeQueueSnapshot Queue;
        internal RebirthStationTerminalSnapshot Terminal;internal byte[] Body;internal int BodyOffset;
        internal RebirthWorkstationReagentOriginal.NativeObserved Observed;
        internal RebirthStationSnapshotEvidence.Request Request;internal DateTime Expires;
        internal volatile XElement Proof;
        internal IDisposable Retention;
        internal bool Current()
        {
            try{return ThreadManager.IsMainThread()&&DateTime.UtcNow<Expires&&ReferenceEquals(GameManager.Instance?.World,World)&&
                ReferenceEquals(World.worldState,WorldState)&&World.worldState.Guid==WorldStateGuid&&
                string.Equals(Path.GetFullPath(GameIO.GetSaveGameDir()),Root,RebirthStationCompletionOriginalScope.PathComparison)&&
                ReferenceEquals(State.NativeWorld,World)&&ReferenceEquals(State.NativeStation,Tile)&&
                ReferenceEquals(World.GetTileEntity(State.Original.Position),Tile)&&Tile.block.GetBlockName()==State.Original.Block&&
                !Tile.IsUserAccessing()&&
                ReferenceEquals(Tile.GetChunk(),Chunk)&&ReferenceEquals(World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&!Chunk.InProgressUnloading&&
                State.ReagentObserved&&State.ReagentAttempted&&Observed?.Matches(State)==true&&
                RebirthWorkstationCompletionPipeline.Grid(Tile.Output)==State.ReagentAfter&&Input.Matches(Tile.Input)&&Queue.Matches(Tile.Queue)&&
                Terminal.Matches(Tile)&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Tile,Body,BodyOffset);}
            catch{return false;}
        }
        public void Dispose()
        {var request=Request;Request=null;lock(requestGate)if(request!=null)requests.Remove(request);request?.Dispose();pending.Remove(State);
            Proof=null;Body=null;Input=null;Queue=null;Terminal=null;Retention?.Dispose();Retention=null;}
    }
    private static readonly object requestGate=new object();
    private static readonly Dictionary<RebirthStationSnapshotEvidence.Request,Pending> requests=new Dictionary<RebirthStationSnapshotEvidence.Request,Pending>();
    private sealed class NativeWriteWitness
    {internal NativeWriteWitness Previous;internal string Path,Digest;internal int X,Z;internal bool Ran;}
    [ThreadStatic]private static NativeWriteWitness nativeWrite;
    private static readonly Dictionary<RebirthWorkstationCompletionOutbox.State,Pending> pending=new Dictionary<RebirthWorkstationCompletionOutbox.State,Pending>();
    internal static void Install(Harmony harmony)
    {
        RebirthProductiveEarnedNativeBody.Install();
        foreach(var type in new[]{typeof(RebirthStationInputSerializedPatch),typeof(RebirthStationQueueSerializedPatch),typeof(RebirthStationOutputSerializedPatch),typeof(RebirthStationCompletionSerializedPatch),
            typeof(RebirthStationSnapshotUpdatePatch),typeof(RebirthStationChunkSerializedPatch),typeof(RebirthStationSnapshotWritePatch),typeof(RebirthStationRegionPublishedPatch),typeof(RebirthStationSnapshotResetPatch),typeof(NativePublished),typeof(NativeWrite)})
            RebirthHarmonyBootstrap.PatchClassOnce(harmony,type);
    }
    private static string ProofPath(RebirthWorkstationCompletionOutbox.State state)=>Path.Combine(state.Root,"nativeReagent",state.Original.Operation+".xml");
    internal static void Sweep()
    {foreach(var value in pending.Values.ToArray())if(value.Proof==null&&!value.Current())value.Dispose();}
    // Positive persisted native publication may retire an already-delivered
    // effect after cold restart. No current tile lookup or setter is involved.
    internal static bool TryRecover(RebirthWorkstationCompletionOutbox.State state)
    {
        if(state==null||!state.Saved||!state.ReagentAttempted||state.Reagent.Length==0)return false;
        string path=ProofPath(state);if(!File.Exists(path))return false;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RetainedMaximum+WorkspaceMaximum,out var coldWorkspace))return false;
        using(coldWorkspace)
        {
        try
        {
            var envelope=RebirthWorkstationCompletionPublication.ReadPrimary(path);
            var proof=envelope.Element("before")?.Elements().SingleOrDefault();
            if(envelope.Name!="workstationCompletionIntent"||envelope.Attributes().Count()!=2||envelope.Elements().Count()!=1||envelope.Element("before")?.Elements().Count()!=1||
                (string)envelope.Attribute("operation")!=state.Original.Operation||
                (string)envelope.Attribute("world")!=state.Original.WorldGuid||!Validate(state,proof,true))return false;
            if(RebirthWorkstationCompletionFixture.Hold("reagentPublication",state.Original.Operation))return false;
            state.ReagentObserved=true;state.ReagentPublished=true;
            return RebirthWorkstationCompletionOutbox.Save(state);
        }
        catch{return false;}
        }
    }
    internal static bool ObserveSaved(RebirthWorkstationCompletionOutbox.State state)
    {
        if(state.ReagentPublished)return state.Saved||RebirthWorkstationCompletionOutbox.Save(state);
        if(TryRecover(state))return true;
        Sweep();
        if(pending.TryGetValue(state,out var frozen)&&frozen.Proof!=null)return PublishProof(frozen);
        if(!pending.TryGetValue(state,out var value))
        {
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RetainedMaximum+WorkspaceMaximum,out var retention))return false;
            try
            {
            var world=state.NativeWorld;var tile=state.NativeStation;
            if(pending.Count>=4||world==null||tile==null||!state.ReagentObserved||state.NativeReagentObserved?.Matches(state)!=true||
                !ReferenceEquals(GameManager.Instance?.World,world)||world.IsRemote()||world.worldState==null||string.IsNullOrEmpty(world.worldState.Guid)||
                !RebirthStationNativeInputSnapshot.TryCapture(tile.Input,out var input)||!RebirthStationNativeQueueSnapshot.TryCapture(tile.Queue,out var queue)||
                !RebirthStationTerminalSnapshot.TryCapture(tile,out var terminal)||
                !RebirthProductiveEarnedNativeBody.TryCaptureCurrent(tile,out var body,out int offset))return false;
            var chunk=tile.GetChunk();var provider=world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;var regions=provider?.m_RegionFileManager;
            if(chunk==null||regions==null||!(regions.regionFileAccess is RegionFileAccessSectorBased))return false;
            value=new Pending{State=state,World=world,WorldState=world.worldState,WorldStateGuid=world.worldState.Guid,Root=Path.GetFullPath(GameIO.GetSaveGameDir()),
                Tile=tile,Chunk=chunk,ChunkX=chunk.X,ChunkZ=chunk.Z,Input=input,Queue=queue,Terminal=terminal,Body=body,BodyOffset=offset,Expires=DateTime.UtcNow.AddSeconds(30)};
            value.Retention=new SnapshotBudget(retention);retention=null;
            value.Observed=state.NativeReagentObserved;
            if(!value.Current()){value.Dispose();return false;}
            value.Request=RebirthStationSnapshotEvidence.Watch(world,chunk,chunk.X,chunk.Z,value.Current,tile,
                (nativeInput,nativeQueue)=>input.MatchesSerializedInput(nativeInput)&&queue.MatchesSerializedQueue(nativeQueue),terminal.MatchesSerializedTerminal,(SnapshotBudget)value.Retention);
            if(value.Request==null){value.Dispose();return false;}pending.Add(state,value);lock(requestGate)requests.Add(value.Request,value);
            try
            {
                lock(chunk)
                {
                    if(!value.Current()||chunk.IsLocked||chunk.InProgressSaving||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)
                    {value.Dispose();return false;}
                    chunk.InProgressSaving=true;
                }
                try{if(!value.Current()){value.Dispose();return false;}regions.SaveChunkSnapshot(chunk,true);}
                finally{lock(chunk)chunk.InProgressSaving=false;}
            }
            catch{value.Dispose();return false;}
            }
            finally{retention?.Dispose();}
        }
        // The actual publication callback detaches before pooled snapshot Reset.
        return value.Proof!=null&&PublishProof(value);
    }
    [HarmonyPatch(typeof(RebirthStationSnapshotEvidence),nameof(RebirthStationSnapshotEvidence.MarkPublished))]
    private static class NativePublished
    {
        private static void Postfix(RebirthStationSnapshotEvidence.Request request,long generation)
        {CaptureNativePublication(request,generation);}
    }
    private static void CaptureNativePublication(RebirthStationSnapshotEvidence.Request request,long generation)
    {
        try
        {
            Pending value;lock(requestGate)if(!requests.TryGetValue(request,out value)||value.Proof!=null)return;
            if(!RebirthStationSnapshotEvidence.TryGetPublished(request,out var publication)||
                !RebirthStationSnapshotEvidence.TryGetPublishedSnapshot(request,out var snapshotProof)||snapshotProof.Generation!=generation||
                !RebirthStationSnapshotEvidence.TryCopy(request,out var bytes,out var copyAllocation))return;
            using(copyAllocation)
            {
            if(!RebirthStationSnapshotEvidence.TryGetPublishedSnapshot(request,out var confirmed)||!ReferenceEquals(snapshotProof,confirmed))return;
            var native=nativeWrite;
            if(native?.Ran!=true||native.X!=publication.ChunkX||native.Z!=publication.ChunkZ||native.Digest!=publication.PayloadDigest||
                !string.Equals(native.Path,Path.GetFullPath(publication.Path),RebirthStationCompletionOriginalScope.PathComparison))return;
            var proof=Freeze(value,publication,snapshotProof,bytes);if(proof==null)return;
            // Worker callback reads only detached immutable original data and
            // this exact Request/generation. No live World/actor/tile query.
            lock(requestGate)if(requests.TryGetValue(request,out var current)&&ReferenceEquals(current,value)&&value.Proof==null)value.Proof=proof;
            }
        }
        catch{/* Missing callback evidence stays unknown and never authorizes replay. */}
    }
    // Observation only: final native __runOriginal is required separately from
    // the shared readback observer. A skipped or throwing native write cannot
    // mint this purpose's saved-effect proof from coincidentally equal old bytes.
    [HarmonyPatch(typeof(RegionFileV2),nameof(RegionFileV2.WriteData))]
    private static class NativeWrite
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(RegionFileV2 __instance,int _cX,int _cZ,int _dataLength,byte[] _data,bool _saveHeaderToFile,out NativeWriteWitness __state)
        {
            __state=new NativeWriteWitness{Previous=nativeWrite};nativeWrite=__state;
            try
            {
                if(!_saveHeaderToFile||_data==null||_dataLength<8||_dataLength>_data.Length||_dataLength>RebirthStationRegionPayload.MaximumPayloadBytes)return;
                __state.Path=Path.GetFullPath(__instance.fullFilePath);__state.X=_cX;__state.Z=_cZ;
                string nativePath=__state.Path;
                lock(requestGate)if(!requests.Values.Any(value=>value.Proof==null&&value.ChunkX==_cX&&value.ChunkZ==_cZ&&
                    nativePath.StartsWith(value.Root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,RebirthStationCompletionOriginalScope.PathComparison)))return;
                using(var hash=SHA256.Create())__state.Digest=BitConverter.ToString(hash.ComputeHash(_data,0,_dataLength)).Replace("-","");
            }
            catch{/* No observation, no native behavior change. */}
        }
        [HarmonyPriority(Priority.First)]
        private static void Postfix(bool __runOriginal,NativeWriteWitness __state)
        {if(__state!=null)__state.Ran=__runOriginal&&__state.Digest!=null;}
        [HarmonyPriority(Priority.Last)]
        private static Exception Finalizer(Exception __exception,NativeWriteWitness __state)
        {if(ReferenceEquals(nativeWrite,__state))nativeWrite=__state?.Previous;return __exception;}
    }
    private static XElement Freeze(Pending value,RebirthStationSnapshotEvidence.Publication publication,
        RebirthStationSnapshotEvidence.PublishedSnapshot snapshotProof,byte[] bytes)
    {
        var state=value.State;
        var original=state.Original;
        string root=value.Root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,path=Path.GetFullPath(publication.Path);
        if(!path.StartsWith(root,RebirthStationCompletionOriginalScope.PathComparison))return null;
        var proofImage=new XElement("nativeReagentPublication",new XAttribute("version",1),new XAttribute("operation",original.Operation),
            new XAttribute("world",original.WorldGuid),new XAttribute("worldState",value.WorldStateGuid),new XAttribute("witness",original.Digest),
            new XAttribute("owner",original.OwnerKey),new XAttribute("creation",original.Creation),new XAttribute("reagent",state.Reagent),
            new XAttribute("after",RebirthWorkstationCompletionOriginal.Hash(state.ReagentAfter)),new XAttribute("region",path.Substring(root.Length).Replace('\\','/')),
            new XAttribute("payload",publication.PayloadDigest),new XAttribute("chunkX",publication.ChunkX),new XAttribute("chunkZ",publication.ChunkZ),
            new XAttribute("generation",snapshotProof.Generation),new XAttribute("bodyOffset",value.BodyOffset),new XElement("nativeBody",Convert.ToBase64String(value.Body)));
        int[] offsets={publication.InputStart,publication.QueueStart,publication.OutputStart,publication.CompletionStart};
        int[] lengths={publication.InputLength,publication.QueueLength,publication.OutputLength,publication.CompletionLength};
        string[] names={"input","queue","output","completion"};
        for(int i=0;i<names.Length;i++)
        {
            if(offsets[i]<8||lengths[i]<1||lengths[i]>256*1024||offsets[i]>bytes.Length-lengths[i])return null;
            var span=new byte[lengths[i]];Buffer.BlockCopy(bytes,offsets[i],span,0,span.Length);
            proofImage.Add(new XElement(names[i],new XAttribute("offset",offsets[i]),Convert.ToBase64String(span)));
        }
        // Actual native publication/readback happened above. Persist that proof
        // independently BEFORE updating the outbox observed/published stage.
        return proofImage;
    }
    private static bool PublishProof(Pending value)
    {
        var state=value.State;var original=state.Original;
        if(!Validate(state,value.Proof)||!RebirthWorkstationCompletionPublication.Write(ProofPath(state),
            new XElement("workstationCompletionIntent",new XAttribute("operation",original.Operation),new XAttribute("world",original.WorldGuid),new XElement("before",new XElement(value.Proof)))))return false;
        if(RebirthWorkstationCompletionFixture.Hold("reagentPublication",original.Operation))return false;
        value.Dispose();state.ReagentPublished=true;return RebirthWorkstationCompletionOutbox.Save(state);
    }
    private static bool Validate(RebirthWorkstationCompletionOutbox.State state,XElement proof,bool reserved=false)
    {
        IDisposable workspace=null;
        try
        {
            // Warm proofs retain their fixed combined account. Cold reads
            // reserve the same bounded region/inflate/XML workspace first.
            if(!reserved&&!pending.ContainsKey(state)&&!RebirthProductiveEarnedObservationBudget.TryRetainDetached(RetainedMaximum+WorkspaceMaximum,out workspace))return false;
            if(pending.TryGetValue(state,out var retained)&&
                !((SnapshotBudget)retained.Retention).TryReserveAllocation(WorkspaceMaximum,out workspace))return false;
            var original=state.Original;
            if(proof?.Name!="nativeReagentPublication"||proof.Attributes().Count()!=15||proof.Elements().Count()!=5||
                (string)proof.Attribute("version")!="1"||(string)proof.Attribute("operation")!=original.Operation||
                (string)proof.Attribute("world")!=original.WorldGuid||(string)proof.Attribute("worldState")!=GameManager.Instance?.World?.worldState?.Guid||
                (string)proof.Attribute("witness")!=original.Digest||(string)proof.Attribute("owner")!=original.OwnerKey||
                !RebirthSurvivorRequestScope.Matches((string)proof.Attribute("creation"),original.Creation)||
                (string)proof.Attribute("reagent")!=state.Reagent||(string)proof.Attribute("after")!=RebirthWorkstationCompletionOriginal.Hash(state.ReagentAfter)||
                (long)proof.Attribute("generation")<1)return false;
            string relative=(string)proof.Attribute("region");
            if(string.IsNullOrEmpty(relative)||relative.Length>1024||Path.IsPathRooted(relative)||relative.Contains(":")||relative.Split('/','\\').Any(part=>part==".."||part=="."||part.Length==0))return false;
            int x=(int)proof.Attribute("chunkX"),z=(int)proof.Attribute("chunkZ");if(x!=(original.Position.x>>4)||z!=(original.Position.z>>4))return false;
            string root=Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));if(!path.StartsWith(root,RebirthStationCompletionOriginalScope.PathComparison))return false;
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            byte[] payload;using(var file=SdFile.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                if(!RebirthStationRegionPayload.TryRead(file,x&31,z&31,out payload))return false;
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            if(RebirthStationPublicationRecord.Digest(payload)!=(string)proof.Attribute("payload")||
                !RebirthStationChunkInflate.TryDecode(payload,RebirthStationSnapshotEvidence.MaximumSnapshotBytes,out var decoded))return false;
            var ranges=new List<Tuple<int,int>>();
            foreach(string name in new[]{"input","queue","output","completion"})
            {
                var node=proof.Element(name);if(node==null||proof.Elements(name).Count()!=1||node.Attributes().Count()!=1||node.HasElements)return false;
                int offset=(int)node.Attribute("offset")-8;string text=(string)node;
                if(text.Length>349528)return false;byte[] span=Convert.FromBase64String(text);
                if(span.Length<1||span.Length>256*1024||offset<0||offset>decoded.Length-span.Length||Convert.ToBase64String(span)!=text)return false;
                if(ranges.Any(range=>offset<range.Item1+range.Item2&&range.Item1<offset+span.Length))return false;
                for(int i=0;i<span.Length;i++)if(decoded[offset+i]!=span[i])return false;ranges.Add(Tuple.Create(offset,span.Length));
            }
            string native=(string)proof.Element("nativeBody");if(native==null||native.Length>3*1024*1024)return false;
            if(!RebirthProductiveEarnedNativeBody.TryRead(Convert.FromBase64String(native),(int)proof.Attribute("bodyOffset"),out var image,out _)||
                RebirthWorkstationCompletionPipeline.Grid(image.Output)!=state.ReagentAfter||
                !RebirthStationNativeInputSnapshot.TryCapture(image.Input,out var input)||
                !input.MatchesSerializedInput(Convert.FromBase64String((string)proof.Element("input")))||
                !RebirthStationNativeQueueSnapshot.TryCapture(image.Queue,out var queue)||
                !queue.MatchesSerializedQueue(Convert.FromBase64String((string)proof.Element("queue"))))return false;
            return NativeSpan(writer=>{writer.Write((byte)image.Output.Length);foreach(var stack in image.Output)stack.Write(writer);})==(string)proof.Element("output")&&
                NativeSpan(writer=>{writer.Write((short)image.Completions.Length);foreach(var receipt in image.Completions)receipt.Write(writer);})==(string)proof.Element("completion");
        }
        catch{return false;}
        finally{workspace?.Dispose();}
    }
    private static string NativeSpan(Action<PooledBinaryWriter> write)
    {
        using(var stream=new SpanStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {writer.SetBaseStream(stream);write(writer);writer.Flush();return Convert.ToBase64String(stream.ToArray());}
    }
    private sealed class SpanStream:MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {if(count<0||Position>256*1024-count)throw new InvalidDataException();base.Write(buffer,offset,count);}
        public override void WriteByte(byte value){if(Position>=256*1024)throw new InvalidDataException();base.WriteByte(value);}
    }
}
