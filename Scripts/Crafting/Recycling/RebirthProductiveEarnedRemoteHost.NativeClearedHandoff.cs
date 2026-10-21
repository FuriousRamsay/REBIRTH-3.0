using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal static bool NeedsRemoteNativeClearCleanup(Guid birth,Guid operation)
    {
        try
        {
            if(!ThreadManager.IsMainThread())return false;ObserveEarly();
            return coldEarly.Any(hold=>hold.Birth==birth&&hold.Operation==operation&&
                File.Exists(hold.Path.Substring(0,hold.Path.Length-5)+".header.nativeStationClearInvoking"));
        }
        catch{return false;}
    }
    private static bool TryFinishNativeClearedHandoffs(TileEntityWorkstation station)
    {
        if(RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out _))return false;
        foreach(var hold in coldEarly.Where(value=>value.Position.Equals(station.ToWorldPos())).ToArray())
        {
            IDisposable workspace=null;RebirthProductiveEarnedObservationBudget.Decoder decoder=null;
            RebirthProductiveEarnedSavedNativeSource.CompletedNativeLoad loaded=null;
            try
            {
                string root=hold.Path.Substring(0,hold.Path.Length-5)+".header";
                if(!File.Exists(root+".nativeStationClearInvoking")||!Manifest(hold.Manifest,out _,out _,out _,out _,out var originalDigest))continue;
                byte[] adopted=ReadNativeOutputMetadata(root+".nativeStationAdopted",1024);
                if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(root+".nativeStationClearInvoking",adopted))continue;
                string original=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthProductiveEarned",hold.Birth.ToString("N"),hold.Operation.ToString("N")+".rew");
                long size=new FileInfo(original).Length;
                long toolSize=File.Exists(root+".nativeTools")?new FileInfo(root+".nativeTools").Length:0;
                if(size<100||size>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||toolSize<0||toolSize>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(32L*size+16L*toolSize+8L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes+65536),out workspace))continue;
                decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)continue;
                byte[] image=ReadNativeOutputMetadata(original,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);var frame=RebirthProductiveEarnedRowProof.Decode(image);
                byte[] header=Read(root);
                if(frame.Operation!=hold.Operation||frame.Birth!=hold.Birth||frame.World!=hold.World||hold.World!=RebirthProductiveEarnedArtifact.WorldIdentity(GameManager.Instance?.World)||
                    Digest(image)!=originalDigest||!decoder.Admit(frame,image.Length)||!TryReadHeader(header,hold.Owner,hold.Creation,hold.Deaths,station,frame,originalDigest,out var nonce)||nonce!=hold.Nonce||
                    !HasNativeStationAdoption(root,originalDigest,Digest(header)))continue;
                var context=new Context{Header=header,HeaderPath=root};
                byte[] outputBytes=ReadNativeOutputMetadata(root+".nativeOutput",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                if(!RebirthProductiveEarnedNativeOutputPlan.TryRead(outputBytes,frame,nonce,originalDigest,out var output))continue;
                context.NativeOutputRecord=outputBytes;
                byte[] outputSaved=ReadNativeOutputMetadata(root+".nativeOutputSaved",8192);
                if(!ValidateNativeOutputSaved(context,outputSaved))continue;
                if(File.Exists(root+".nativeTools"))
                {
                    byte[] toolBytes=ReadNativeOutputMetadata(root+".nativeTools",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                    if(!RebirthProductiveEarnedRemoteToolPlan.TryRead(toolBytes,frame.Operation,frame.Birth,nonce,originalDigest,output.Native,output.Offset,out var tools))continue;
                    using(var source=ReadRemoteToolSource(original,image,tools))if(source==null)continue;
                    output=EffectiveNativeOutput(output,tools);
                }
                byte kind=1;byte[] final=output.Native;int offset=output.Offset;byte[] terminal=outputSaved;
                if(File.Exists(root+".nativePending"))
                {
                    byte[] bytes=ReadNativeOutputMetadata(root+".nativePending",RebirthProductiveEarnedRowProof.MaximumArtifactBytes);
                    if(!RebirthProductiveEarnedNativePendingPlan.TryRead(bytes,frame,nonce,originalDigest,output,station.block,out var plan)||plan.Child!=null)continue;
                    terminal=ReadNativeOutputMetadata(root+".nativePendingSaved",8192);
                    if(!terminal.SequenceEqual(System.Text.Encoding.ASCII.GetBytes(Digest(bytes))))continue;
                    byte[] partial=RebirthProductiveEarnedNativePendingPlan.PartialRecord(plan);
                    if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original+".partial-successor",partial)||
                        !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original+".partial-successor.invoking",partial))continue;
                    kind=2;final=plan.After;offset=plan.AfterOffset;
                }
                else if(output.PendingIndex>=0)continue;
                if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,final,offset))continue;
                bool returned=pending.TryGetValue(hold.Operation,out var warm)&&ReferenceEquals(warm.Station,station)&&warm.NativeMarkerReleased&&
                    warm.Artifact.NativeRetirementIdentityCurrent();
                if(!returned)
                {
                    byte[] digest;using(var sha=SHA256.Create())digest=sha.ComputeHash(image);
                    if(!RebirthProductiveEarnedSavedNativeSource.TryObservedCompletedNativeLoad(hold.Birth,hold.Operation,digest,final,offset,out loaded)||!loaded.Current())continue;
                }
                var closed=new RebirthProductiveEarnedRemoteClosedRecord.Description{Operation=hold.Operation,Birth=hold.Birth,Nonce=nonce,World=hold.World,Owner=hold.Owner,
                    Creation=hold.Creation,Deaths=hold.Deaths,Position=hold.Position,Kind=kind,OriginalDigest=originalDigest,HeaderDigest=Digest(header),HoldDigest=Digest(hold.Bytes),TerminalDigest=Digest(terminal)};
                byte[] closedBytes=RebirthProductiveEarnedRemoteClosedRecord.Encode(closed);
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".closed",closedBytes)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(root+".nativeReleased",System.Text.Encoding.ASCII.GetBytes(Digest(closedBytes))))continue;
                if(warm!=null)DisposeAdoptedRemoteRoot(warm);pending.Remove(hold.Operation);early.Remove(hold.Operation);coldEarly.RemoveAll(value=>value.Operation==hold.Operation);
                loaded?.Dispose();loaded=null;RefreshEarlyAfterNativeClosure();return true;
            }
            catch{/* Exact native absence/immutable metadata must remain positively qualified. */}
            finally{decoder?.Dispose();workspace?.Dispose();}
        }
        return false;
    }
}
