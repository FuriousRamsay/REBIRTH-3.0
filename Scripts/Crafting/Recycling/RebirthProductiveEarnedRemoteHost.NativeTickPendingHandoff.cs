using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    private static bool TryAdoptRemoteSavedPending(Context source,RebirthProductiveEarnedSavedNativeSource.Evidence loaded)
    {
        IDisposable workspace=null;RebirthProductiveEarnedObservationBudget.Decoder ownedDecoder=null;
        try
        {
            if(OriginalStillOwnsNativeCard(source)||!ReferenceEquals(nativeHandoffTick,source.Station)||
                source.Artifact?.Current()!=true&&!NativeParentCustodyCurrent(source))return false;
            string root=source.HeaderPath,original=source.Artifact.OriginalPath;
            if(!File.Exists(root+".nativePending")||!File.Exists(root+".nativePendingSaved"))return false;
            long size=new FileInfo(root+".nativePending").Length+new FileInfo(root+".nativeOutput").Length;
            if(File.Exists(root+".nativeTools"))size=checked(size+new FileInfo(root+".nativeTools").Length);
            if(size<100||size>3L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*size+65536),out workspace))return false;
            if(loaded==null&&(ownedDecoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())==null)return false;
            var frame=RebirthProductiveEarnedRowProof.Decode(source.Image);
            if(ownedDecoder!=null&&!ownedDecoder.Admit(frame,source.Image.Length))return false;
            byte[] outputBytes=ReadNativeOutputMetadata(root+".nativeOutput",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            byte[] outputSaved=ReadNativeOutputMetadata(root+".nativeOutputSaved",8192);
            if(!RebirthProductiveEarnedNativeOutputPlan.TryRead(outputBytes,frame,source.Nonce,Digest(source.Image),out var output))return false;
            source.NativeOutput=output;source.NativeOutputRecord=outputBytes;
            if(!ValidateNativeOutputSaved(source,outputSaved))return false;
            bool tools=File.Exists(root+".nativeTools");
            RebirthProductiveEarnedSavedNativeSource.NativeChildDependency[] toolDependencies=null;
            if(tools)
            {
                byte[] toolBytes=ReadNativeOutputMetadata(root+".nativeTools",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(!RebirthProductiveEarnedRemoteToolPlan.TryRead(toolBytes,frame.Operation,frame.Birth,source.Nonce,Digest(source.Image),output.Native,output.Offset,out var toolPlan))return false;
                using(var remote=ReadRemoteToolSource(original,source.Image,toolPlan))
                    if(remote==null)return false;
                output=EffectiveNativeOutput(output,toolPlan);
                if(!RebirthProductiveEarnedSavedNativeSource.TryRemoteHandoffToolDependencies(original,source.Image,toolPlan.After,toolPlan.AfterOffset,out toolDependencies))return false;
            }
            byte[] pendingBytes=ReadNativeOutputMetadata(root+".nativePending",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
            byte[] pendingSaved=ReadNativeOutputMetadata(root+".nativePendingSaved",8192);
            if(!pendingSaved.SequenceEqual(System.Text.Encoding.ASCII.GetBytes(Digest(pendingBytes)))||
                !RebirthProductiveEarnedNativePendingPlan.TryRead(pendingBytes,frame,source.Nonce,Digest(source.Image),output,source.Station.block,out var plan)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,plan.After,plan.AfterOffset))return false;
            if(loaded==null)
            {
                if(!source.NativePendingSaved||!source.NativePendingAccepted||plan.Child!=null&&
                    (source.NativeChild?.Accepted!=true||!source.NativeChild.AfterPublished))return false;
            }
            else if(!loaded.PrimaryCurrent()||!loaded.CurrentIdentity(source.World,source.Station,loaded.Frame.Birth,loaded.Frame.Operation)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(loaded.Native,loaded.NativeOffset,plan.After,plan.AfterOffset)||
                (plan.Child==null? !loaded.Terminal||loaded.PendingNextWork||loaded.Frame.Operation!=frame.Operation||!loaded.Original.SequenceEqual(source.Image):
                    loaded.Terminal||loaded.Frame.Operation!=plan.Child.Operation||!loaded.Original.SequenceEqual(plan.ChildImage)))return false;
            var target=source.Artifact;
            byte[] link=plan.Child==null?RebirthProductiveEarnedNativePendingPlan.PartialRecord(plan):RebirthProductiveEarnedNativePendingPlan.ChildLink(source.Image,plan);
            string linkPath=original+(plan.Child==null?".partial-successor":".earned-child");
            if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(linkPath,link)||!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(linkPath+".invoking",link))return false;
            if(plan.Child!=null)
            {
                target=source.NativeChild?.Artifact;
                if(target==null&&(!RebirthProductiveEarnedArtifact.TryReadRemoteObservedOriginal(source.Station,plan.Child.Birth,plan.Child.Operation,out target,out var childImage)||!childImage.SequenceEqual(plan.ChildImage)))return false;
                if(!target.MatchesAfter(source.Station))return false;
            }
            var hold=ReadEarly(root.Substring(0,root.Length-7)+".hold");
            if(hold.Operation!=source.Operation||hold.Birth!=frame.Birth||hold.Nonce!=source.Nonce||hold.Owner!=source.Owner||hold.Creation!=source.Creation||
                hold.Deaths!=source.Deaths||hold.World!=source.WorldKey||!hold.Position.Equals(source.Station.ToWorldPos()))return false;
            byte[] adopted;
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {writer.Write(0x31414E52u);writer.Write(Digest(source.Image));writer.Write(Digest(source.Header));writer.Write(Digest(outputBytes));writer.Write(Digest(outputSaved));
             writer.Write(tools?Digest(ReadNativeOutputMetadata(root+".nativeToolsSaved",8192)):"");writer.Write(Digest(pendingBytes));writer.Write(Digest(pendingSaved));writer.Flush();adopted=stream.ToArray();}
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".nativeStationAdopted",adopted))return false;
            byte[] originalImage=source.Image,header=source.Header;
            Func<bool> qualified=()=>RebirthProductiveEarnedArtifact.FileMatches(original,originalImage)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(root,header)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(root+".nativeStationAdopted",adopted)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(root+".nativePending",pendingBytes,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(root+".nativePendingSaved",pendingSaved)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(linkPath,link)&&RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(linkPath+".invoking",link)&&
                RebirthProductiveEarnedSavedNativeSource.RemoteHandoffDependenciesCurrent(toolDependencies);
            string originalDigest=Digest(originalImage),headerDigest=Digest(header),terminalDigest=Digest(pendingSaved);
            Func<bool> releaseReady=()=>NativeAdoptionReleased(hold,2,originalDigest,headerDigest,terminalDigest);
            if(plan.Child!=null)
            {
                if(!RebirthProductiveEarnedCoordinator.AdoptRemoteSavedEarnedChild(source.Station,target,plan.Child,qualified,
                    checked((long)originalImage.Length+header.Length+adopted.Length+pendingBytes.Length+pendingSaved.Length+link.Length+2048),releaseReady))return false;
                if(!PublishNativeInheritedCustody(source,plan.Child,plan.ChildImage))return false;
            }
            else if(!source.NativeMarkerReleased)
            {
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".nativeStationClearInvoking",adopted)||
                    !RebirthProductiveEarnedCoordinator.ClearRemoteNativeFinal(source.Station,target))return false;
                source.NativeMarkerReleased=true;
            }
            var closed=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=source.Operation,Birth=frame.Birth,Nonce=source.Nonce,World=source.WorldKey,
                Owner=source.Owner,Creation=source.Creation,Deaths=source.Deaths,Position=source.Station.ToWorldPos(),Kind=2,OriginalDigest=Digest(source.Image),
                HeaderDigest=Digest(source.Header),HoldDigest=Digest(hold.Bytes),TerminalDigest=Digest(pendingSaved)};
            byte[] closedBytes=RebirthProductiveEarnedRemoteClosedRecord.Encode(closed);
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".closed",closedBytes)||
                !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".nativeReleased",System.Text.Encoding.ASCII.GetBytes(Digest(closedBytes))))return false;
            if(source.NativeChild!=null){DisposeAdoptedRemoteRoot(source.NativeChild);source.NativeChild=null;}
            if(plan.Child!=null){if(pending.TryGetValue(plan.Child.Operation,out var oldChild))DisposeAdoptedRemoteRoot(oldChild);pending.Remove(plan.Child.Operation);early.Remove(plan.Child.Operation);coldEarly.RemoveAll(value=>value.Operation==plan.Child.Operation);}
            DisposeAdoptedRemoteRoot(source);pending.Remove(source.Operation);early.Remove(source.Operation);coldEarly.RemoveAll(value=>value.Operation==source.Operation);
            RefreshEarlyAfterNativeClosure();return true;
        }
        catch{return false;}
        finally{ownedDecoder?.Dispose();workspace?.Dispose();}
    }
    private static RebirthProductiveEarnedRemoteToolRebaseRecord.Description ReadRemoteToolSource(string path,byte[] image,RebirthProductiveEarnedRemoteToolPlan.Description tools)
        =>RebirthProductiveEarnedRemoteToolRebaseRecord.TryRead(path,image,tools.After,tools.AfterOffset,out var source,out _)?source:null;
}
