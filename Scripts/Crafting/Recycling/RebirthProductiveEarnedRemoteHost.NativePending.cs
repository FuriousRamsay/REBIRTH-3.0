using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal static bool RegisterNativePending(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,byte[] bytes,out RebirthProductiveEarnedWire.Reply reply)
    {
        reply=null;
        if(bytes==null||!TryRebindNativeManifest(actor,peer,station,creation,operation,nonce)||!pending.TryGetValue(operation,out var source)||!ReferenceEquals(source.Actor,actor)||!ReferenceEquals(source.Sender,peer)||
            !ReferenceEquals(source.Station,station)||source.Creation!=creation||source.Nonce!=nonce||!source.Current()||!RestoreNativeOutputDescription(source)||!RestoreNativeToolsDescription(source))return false;
        if(source.NativePending==null&&File.Exists(source.HeaderPath+".nativePending")&&!RestoreNativePendingDescription(source))return false;
        if(source.NativePending==null)
        {
            if(!TrySaveNativeOutput(source)||!NativeOutputCurrent(source)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*bytes.Length+65536),out var held))return false;
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||!RebirthProductiveEarnedNativePendingPlan.TryRead(bytes,RebirthProductiveEarnedRowProof.Decode(source.Image),nonce,Digest(source.Image),EffectiveNativeOutput(source),station.block,out var plan)||
                        !RebirthProductiveEarnedNativePendingPlan.TryMeasureRetention(plan,bytes.Length,out long retainedBytes)||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(held,retainedBytes)||
                        !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".nativePending",bytes,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||!NativeOutputCurrent(source))return false;
                    source.NativePending=plan;source.NativePendingRecord=(byte[])bytes.Clone();source.NativePendingRetention=held;held=null;
                }
            }
            finally{held?.Dispose();}
        }
        if(!source.NativePendingRecord.SequenceEqual(bytes)||!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativePending",bytes,RebirthProductiveEarnedRowProof.MaximumArtifactBytes))return false;
        if(!PrepareNativePendingGrant(source))return false;
        bool saved=TrySaveNativePending(source);
        reply=NativeOutputReply(source,false);reply.State=saved?RebirthProductiveEarnedWire.Status.NativePendingSaved:RebirthProductiveEarnedWire.Status.NativePendingHeader;
        if(source.NativeChild!=null)reply.QueueOwner=source.NativeChild.Nonce.ToString("N")+":"+Digest(source.NativeChild.Image);
        return true;
    }
    private static bool PrepareNativePendingGrant(Context source)
    {
        var plan=source.NativePending;
        if(plan==null||!source.Current())return false;
        string path=source.Artifact.OriginalPath+(plan.Child==null?".partial-successor":".earned-child");
        if(plan.Child==null)
        {
            byte[] record=RebirthProductiveEarnedNativePendingPlan.PartialRecord(plan);
            return RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,record)&&
                RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path+".invoking",record);
        }
        if(source.NativeChild==null)
        {
            RebirthProductiveEarnedArtifact.Prepared artifact;
            if(!RebirthProductiveEarnedArtifact.TryReadPrepared(source.Station,plan.Child,out artifact)&&
                (!RebirthProductiveEarnedArtifact.TryReadRemoteObservedOriginal(source.Station,plan.Child.Birth,plan.Child.Operation,out artifact,out var observedImage)||
                    !observedImage.SequenceEqual(plan.ChildImage))&&
                (!NativeOutputCurrent(source)||!RebirthProductiveEarnedArtifact.TryPrepareRemote(source.Station,plan.Child,source.Actor,source.Sender,source.Creation,out artifact)))return false;
            string headerPath=Path.Combine(Path.GetDirectoryName(source.HeaderPath),plan.Child.Operation.ToString("N")+".header");
            Guid nonce;byte[] header;
            if(File.Exists(headerPath))
            {header=Read(headerPath);if(!TryReadHeader(header,source.Owner,source.Creation,source.Deaths,source.Station,plan.Child,Digest(plan.ChildImage),out nonce))return false;}
            else{nonce=Guid.NewGuid();header=Header(source.Owner,source.Creation,source.Deaths,source.Station,plan.Child,Digest(plan.ChildImage),nonce);if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(headerPath,header))return false;}
            // Paid-parent admission. This is not an owning-card AddRecipe event:
            // the exact full Before is the predecessor's positively saved native terminal.
            var item=new Early{Operation=plan.Child.Operation,Birth=plan.Child.Birth,Nonce=nonce,Owner=source.Owner,Creation=source.Creation,Deaths=source.Deaths,
                World=source.WorldKey,Position=source.Station.ToWorldPos(),Manifest=Convert.FromBase64String(EarlyManifest(plan.Child,plan.ChildImage)),
                Path=Path.Combine(Path.GetDirectoryName(headerPath),plan.Child.Operation.ToString("N")+".hold")};
            item.Bytes=EarlyBytes(item);if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(item.Path,item.Bytes))return false;
            var memory=RebirthProductiveEarnedObservationBudget.TryOpen(artifact,plan.Child);if(memory==null)return false;
            var child=new Context{World=source.World,Actor=source.Actor,Sender=source.Sender,Station=source.Station,Artifact=artifact,Operation=plan.Child.Operation,Nonce=nonce,
                Owner=source.Owner,Creation=source.Creation,Deaths=source.Deaths,Directory=source.Directory,HeaderPath=headerPath,Header=header,Image=plan.ChildImage,WorldKey=source.WorldKey,Memory=memory};
            if(!child.Current()){memory.Dispose();return false;}
            source.NativeChild=child;
            byte[] link=RebirthProductiveEarnedNativePendingPlan.ChildLink(source.Image,plan);
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,link)||!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path+".invoking",link))return false;
            early[item.Operation]=item;if(!coldEarly.Any(value=>value.Operation==item.Operation))coldEarly.Add(item);
        }
        byte[] expected=RebirthProductiveEarnedNativePendingPlan.ChildLink(source.Image,plan);
        return source.NativeChild.Current()&&RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path,expected)&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(path+".invoking",expected)&&PublishRemotePaidParentLocator(source,source.NativeChild);
    }
    private static bool NativePendingCurrent(Context source)
    {
        using(var callbacks=source?.Memory?.TryEnterCallbacks())
            return callbacks!=null&&source.Current()&&source.NativePending!=null&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativePending",source.NativePendingRecord,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)&&
                RebirthProductiveEarnedCoordinator.TryNativeMarker(source.Station,out var marker)&&
                (source.NativeChild??source).Artifact.MatchesMarker(marker)&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.NativePending.After,source.NativePending.AfterOffset);
    }
    private static bool NativeParentCustodyCurrent(Context source)
    {
        var child=source.NativeChild;var plan=source.NativePending;
        if(child?.Artifact?.Current()!=true||plan?.Child==null||child.Operation!=plan.Child.Operation||!child.Image.SequenceEqual(plan.ChildImage)||
            !source.Artifact.IsTarget(source.Station)||!RebirthProductiveEarnedArtifact.FileMatches(source.Artifact.OriginalPath,source.Image))return false;
        byte[] link=RebirthProductiveEarnedNativePendingPlan.ChildLink(source.Image,plan);
        return RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativePending",source.NativePendingRecord,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.Artifact.OriginalPath+".earned-child",link)&&
            RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.Artifact.OriginalPath+".earned-child.invoking",link);
    }
    private static void ObserveAcceptedNativePending(Context source)
    {
        var plan=source.NativePending;if(plan==null||!source.Current())return;
        if(plan.Child!=null)
        {
            if(source.NativeChild==null||!RebirthProductiveEarnedCoordinator.AcceptRemoteNativeChildMarker(source.Station,source.Artifact,source.NativeChild.Artifact,plan.Child))return;
            source.NativeChild.Accepted=true;
        }
        if(!NativePendingCurrent(source))return;
        source.NativePendingAccepted=true;source.NativeOutputWatch?.Dispose();source.NativeOutputWatch=null;
        if(plan.Child!=null)
        {
            RebirthProductiveEarnedSavedNativeSource.ReleaseRegisteredTerminal(source.Artifact.Birth,source.Artifact.Operation);
            if(!RebirthProductiveEarnedSavedNativeSource.RegisterInvokedChild(source.Artifact.OriginalPath+(source.Tools?.Saved==true?".tools-load":".terminal-load"),source.NativeChild.Artifact.OriginalPath,source.Directory,source.NativeChild.Image))return;
            TryRetainAfterPublication(source.NativeChild);return;
        }
        if(source.NativePendingWatch==null&&RebirthRemoteResourceNativeBinding.TryResolve(source.World,RemoteResourceIdentity.Workstation(source.Station.ToWorldPos()),
            RemoteResourceSourceKind.WorkstationOutput,source.Artifact.CustodyBirth.ToString("N"),out var binding))
        {
            var p=source.Station.localChunkPos;source.NativePendingWatch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,()=>NativePendingCurrent(source),source.Memory,
                "RebirthProductiveEarnedV1:"+p.x+":"+p.y+":"+p.z);
        }
    }
    private static bool TrySaveNativePending(Context source)
    {
        if(!source.NativePendingAccepted||!NativePendingCurrent(source))return false;
        if(source.NativePendingSaved&&RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.HeaderPath+".nativePendingSaved",System.Text.Encoding.ASCII.GetBytes(Digest(source.NativePendingRecord))))
        {source.NativePendingWatch?.Dispose();source.NativePendingWatch=null;return true;}
        if(source.NativeChild!=null)
        {if(!TryRetainAfterPublication(source.NativeChild))return false;}
        else
        {
            var loaded=source.Artifact.LoadedSource;
            bool savedLoaded=loaded?.Terminal==true&&!loaded.PendingNextWork&&loaded.PrimaryCurrent()&&loaded.CurrentIdentity(source.World,source.Station,loaded.Frame.Birth,loaded.Frame.Operation)&&
                RebirthProductiveEarnedNativeBody.SameExceptElapsed(loaded.Native,loaded.NativeOffset,source.NativePending.After,source.NativePending.AfterOffset);
            if(!savedLoaded&&(source.NativePendingWatch==null||!RebirthProductiveEarnedCoordinator.PublishRemoteNativePartial(source.Artifact,source.Memory,source.NativePendingWatch,source.NativePending,()=>NativePendingCurrent(source),source.Tools?.Saved==true)))return false;
        }
        byte[] record=System.Text.Encoding.ASCII.GetBytes(Digest(source.NativePendingRecord));
        if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.HeaderPath+".nativePendingSaved",record)||!NativePendingCurrent(source))return false;
        source.NativePendingSaved=true;source.NativePendingWatch?.Dispose();source.NativePendingWatch=null;return true;
    }
    private static bool RestoreNativePendingDescription(Context source)
    {
        if(source.NativePending!=null)return true;
        string path=source.HeaderPath+".nativePending";
        if(!File.Exists(path))return !File.Exists(path+".tmp");
        if(source.NativeOutput==null||!source.Current()||!source.NativeOutputSaved||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*new FileInfo(path).Length+65536),out var held))return false;
        try
        {
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                byte[] bytes=ReadNativeOutputMetadata(path,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(decoder==null||!RebirthProductiveEarnedNativePendingPlan.TryRead(bytes,RebirthProductiveEarnedRowProof.Decode(source.Image),source.Nonce,Digest(source.Image),EffectiveNativeOutput(source),source.Station.block,out var plan)||
                    !RebirthProductiveEarnedNativePendingPlan.TryMeasureRetention(plan,bytes.Length,out long retainedBytes)||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(held,retainedBytes))return false;
                source.NativePending=plan;source.NativePendingRecord=bytes;source.NativePendingRetention=held;held=null;
            }
            if(!PrepareNativePendingGrant(source))return false;
            // Only a native source installed by the actual saved-chunk reader
            // can renew an already-entered continuation after process replacement.
            if(RebirthProductiveEarnedSavedNativeSource.TryObserved(source.Station,out var loaded)&&loaded.PrimaryCurrent()&&
                loaded.CurrentIdentity(source.World,source.Station,loaded.Frame.Birth,loaded.Frame.Operation)&&
                RebirthProductiveEarnedNativeBody.SameExceptElapsed(loaded.Native,loaded.NativeOffset,source.NativePending.After,source.NativePending.AfterOffset)&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,loaded.Native,loaded.NativeOffset)&&
                (source.NativeChild==null?loaded.Terminal&&!loaded.PendingNextWork&&loaded.Frame.Operation==source.Operation&&loaded.Original.SequenceEqual(source.Image):
                    !loaded.Terminal&&loaded.Frame.Operation==source.NativeChild.Operation&&loaded.Original.SequenceEqual(source.NativeChild.Image)))
            {
                (source.NativeChild??source).Artifact.LoadedSource=loaded;source.NativePendingAccepted=true;
                if(source.NativeChild!=null)source.NativeChild.Accepted=true;
                ObserveAcceptedNativePending(source);
            }
            return true;
        }
        finally{held?.Dispose();}
    }
}
