using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    private static bool RestoreNativeOutputDescription(Context source)
    {
        if(source.NativeOutput!=null)return true;
        string path=source.HeaderPath+".nativeOutput";if(!File.Exists(path))return !File.Exists(path+".tmp");
        if(!source.Current()||!Safe(path)||File.Exists(path+".tmp"))return false;
        long length=new FileInfo(path).Length;
        if(length<100||length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var held))return false;
        try
        {
            byte[] bytes=ReadNativeOutputMetadata(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
                if(!decoder.Admit(frame,source.Image.Length)||!RebirthProductiveEarnedNativeOutputPlan.TryRead(bytes,frame,source.Nonce,Digest(source.Image),out var plan)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path,bytes)||!source.Current()||
                    !RebirthProductiveEarnedNativeOutputPlan.TryMeasureRetention(plan,bytes.Length,out long retainedBytes)||
                    !RebirthProductiveEarnedObservationBudget.TryResizeDetached(held,retainedBytes))return false;
                string after=source.HeaderPath+".afterSaved";
                if(!Safe(after)||!File.Exists(after)||File.Exists(after+".tmp")||!ValidateAfterReceipt(source,ReadBeforeReceipt(after)))return false;
                source.NativeOutput=plan;source.NativeOutputRecord=bytes;source.NativeOutputRetention=held;held=null;source.AfterPublished=true;
                string saved=source.HeaderPath+".nativeOutputSaved";
                if(File.Exists(saved)&&!File.Exists(saved+".tmp")&&ValidateNativeOutputSaved(source,ReadNativeOutputMetadata(saved,8192))){source.NativeOutputSaved=true;source.Memory.RetainNativeTerminalAllocation();}
            }
            // Metadata may be restored without issuing an upload or setter.
            // A fresh native publication remains required for live continuation.
            if(RebirthProductiveEarnedSavedNativeSource.TryObserved(source.Station,out var loaded)&&loaded.Terminal&&
                loaded.Frame.Birth==source.NativeOutput.Birth&&loaded.Frame.Operation==source.Operation&&loaded.Original.SequenceEqual(source.Image)&&
                loaded.PrimaryCurrent()&&loaded.CurrentIdentity(source.World,source.Station,loaded.Frame.Birth,loaded.Frame.Operation))
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                    if(decoder!=null&&RebirthProductiveEarnedNativeBody.SameExceptElapsed(loaded.Native,loaded.NativeOffset,source.NativeOutput.Native,source.NativeOutput.Offset)&&
                        RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,loaded.Native,loaded.NativeOffset))
                    {source.Artifact.LoadedSource=loaded;source.NativeOutputAccepted=true;}
            }
            if(source.NativeOutputAccepted)ObserveAcceptedNativeOutput(source);
            return true;
        }
        finally{held?.Dispose();}
    }
    internal static bool QueryNativeOutput(EntityPlayer actor,ClientInfo peer,Guid operation,Guid nonce,string digest,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        if(!pending.TryGetValue(operation,out var source)||!ReferenceEquals(source.Actor,actor)||!ReferenceEquals(source.Sender,peer)||source.Nonce!=nonce||
            Digest(source.Image)!=digest||!source.Current()||!RestoreNativeOutputDescription(source)||source.NativeOutput==null)return false;
        if(!RestoreNativeToolsDescription(source)||!RestoreNativePendingDescription(source))return false;
        if(source.NativePending!=null)
        {
            reply=NativeOutputReply(source,false);reply.State=TrySaveNativePending(source)?RebirthProductiveEarnedWire.Status.NativePendingSaved:RebirthProductiveEarnedWire.Status.NativePendingHeader;
            if(source.NativeChild!=null)reply.QueueOwner=source.NativeChild.Nonce.ToString("N")+":"+Digest(source.NativeChild.Image);
            return true;
        }
        if(source.Tools!=null)
        {TrySaveNativeTools(source);reply=NativeOutputReply(source,false);reply.State=source.Tools.Saved?RebirthProductiveEarnedWire.Status.NativeToolSaved:RebirthProductiveEarnedWire.Status.NativeToolHeader;reply.QueueOwner=source.Tools.OwnerSaved?"ownerSaved":"ownerBefore";return true;}
        reply=NativeOutputReply(source,TrySaveNativeOutput(source));return true;
    }
    internal static bool RegisterNativeOutput(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,byte[] bytes,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        if(!TryRebindNativeManifest(actor,peer,station,creation,operation,nonce)||!pending.TryGetValue(operation,out var source)||!ReferenceEquals(source.Actor,actor)||!ReferenceEquals(source.Sender,peer)||
            !ReferenceEquals(source.Station,station)||source.Creation!=creation||source.Nonce!=nonce||!source.Current())return false;
        if(!RestoreNativeOutputDescription(source))return false;
        if(source.NativeOutput==null)
        {
            if(!TryRetainAfterPublication(source)||!source.AfterCurrent()||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*bytes.Length+65536),out var held))return false;
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null)return false;
                    var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
                    if(!decoder.Admit(frame,source.Image.Length)||!RebirthProductiveEarnedNativeOutputPlan.TryRead(bytes,frame,nonce,Digest(source.Image),out var plan)||
                        !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".nativeOutput",bytes)||!source.Current()||
                        !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,frame.AfterNative,frame.AfterElapsedOffset)||
                        !RebirthProductiveEarnedNativeOutputPlan.TryMeasureRetention(plan,bytes.Length,out long retainedBytes)||
                        !RebirthProductiveEarnedObservationBudget.TryResizeDetached(held,retainedBytes))return false;
                    source.NativeOutput=plan;source.NativeOutputRecord=(byte[])bytes.Clone();source.NativeOutputRetention=held;held=null;
                }
            }
            finally{held?.Dispose();}
        }
        if(!source.NativeOutputRecord.SequenceEqual(bytes)||!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeOutput",bytes))return false;
        reply=NativeOutputReply(source,TrySaveNativeOutput(source));return true;
    }
    private static RebirthProductiveEarnedWire.Reply NativeOutputReply(Context source,bool saved)=>new RebirthProductiveEarnedWire.Reply{
        Operation=source.Operation,Birth=RebirthProductiveEarnedRowProof.Decode(source.Image).Birth,Nonce=source.Nonce,Digest=Digest(source.Image),World=source.WorldKey,
        Owner=source.Owner,Creation=source.Creation,Deaths=source.Deaths,Position=source.Station.ToWorldPos(),
        State=saved?RebirthProductiveEarnedWire.Status.NativeOutputSaved:RebirthProductiveEarnedWire.Status.NativeOutputHeader};
    private static bool NativeOutputCurrent(Context source)
    {
        if(source?.Tools?.Saved==true)return NativeToolsCurrent(source);
        using(var callbacks=source?.Memory?.TryEnterCallbacks())
            return callbacks!=null&&source.Current()&&source.NativeOutput!=null&&
                RebirthProductiveEarnedCoordinator.TryNativeMarker(source.Station,out var marker)&&source.Artifact.MatchesMarker(marker)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativeOutput",source.NativeOutputRecord)&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.NativeOutput.Native,source.NativeOutput.Offset);
    }
    private static void ObserveAcceptedNativeOutput(Context source)
    {
        if(!NativeOutputCurrent(source))return;source.NativeOutputAccepted=true;
        if(source.NativeOutputWatch==null&&RebirthRemoteResourceNativeBinding.TryResolve(source.World,RemoteResourceIdentity.Workstation(source.Station.ToWorldPos()),
            RemoteResourceSourceKind.WorkstationOutput,source.Artifact.CustodyBirth.ToString("N"),out var binding))
        {
            var p=source.Station.localChunkPos;source.NativeOutputWatch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,()=>NativeOutputCurrent(source),source.Memory,
                "RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z);
        }
    }
    private static bool TrySaveNativeOutput(Context source)
    {
        if(source?.Tools!=null)return source.NativeOutputSaved&&source.Tools.Saved&&NativeToolsCurrent(source);
        if(source?.NativeOutputSaved==true&&source.NativeOutputAccepted&&NativeOutputCurrent(source)&&
            ValidateNativeOutputSaved(source,ReadNativeOutputMetadata(source.HeaderPath+".nativeOutputSaved",8192)))
        {source.NativeOutputWatch?.Dispose();source.NativeOutputWatch=null;return true;}
        if(!source.NativeOutputAccepted||!NativeOutputCurrent(source)||source.NativeOutputWatch==null||
            !source.NativeOutputWatch.TryReadPublishedTileDescriptor(out var publication,out long offset,out int length,out string digest))return false;
        if(!RebirthProductiveEarnedCoordinator.PublishRemoteNativeTerminalSource(source.Artifact,RebirthProductiveEarnedRowProof.Decode(source.Image),
            source.NativeOutput,source.Memory,source.NativeOutputWatch,()=>source.NativeOutputAccepted&&NativeOutputCurrent(source)))return false;
        byte[] record;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {writer.Write(0x31534F4Eu);writer.Write(Digest(source.Header));writer.Write(Digest(source.NativeOutputRecord));writer.Write(publication.Path);writer.Write(publication.PayloadDigest);
         writer.Write(offset);writer.Write(length);writer.Write(digest);writer.Flush();record=stream.ToArray();}
        string path=source.HeaderPath+".nativeOutputSaved";
        if(File.Exists(path))
        {if(!ValidateNativeOutputSaved(source,ReadNativeOutputMetadata(path,4096)))return false;}
        else if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,record))return false;
        if(!NativeOutputCurrent(source))return false;
        source.NativeOutputSaved=true;source.Memory.RetainNativeTerminalAllocation();source.NativeOutputWatch.Dispose();source.NativeOutputWatch=null;return true;
    }
    private static bool ValidateNativeOutputSaved(Context source,byte[] bytes)
    {
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadUInt32()!=0x31534F4Eu||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(source.Header)||
                RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=Digest(source.NativeOutputRecord))return false;
            string path=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,1024),payload=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
            long offset=reader.ReadInt64();int length=reader.ReadInt32();string tile=RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64);
            return stream.Position==stream.Length&&Safe(path)&&HashText(payload)&&HashText(tile)&&offset>=8&&length>0&&length<=RebirthProductiveEarnedNativeBody.MaximumBodyBytes+18;
        }
    }
    private static byte[] ReadNativeOutputMetadata(string path,int maximum)
    {
        if(!Safe(path)||File.Exists(path+".tmp"))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<1||stream.Length>maximum)throw new InvalidDataException();var bytes=new byte[(int)stream.Length];int count=0;
            while(count<bytes.Length){int read=stream.Read(bytes,count,bytes.Length-count);if(read<1)throw new EndOfStreamException();count+=read;}
            if(stream.ReadByte()!=-1||!Safe(path))throw new InvalidDataException();return bytes;
        }
    }
}
