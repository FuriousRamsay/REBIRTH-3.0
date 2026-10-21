using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedRemoteClient
{
    private static bool NativeFinalCurrent(Original source)
    {
        using(var callbacks=source?.Memory?.TryEnterCallbacks())
        {
            if(callbacks==null||!source.Current()||source.Output?.Returned!=true||!source.Output.Saved)return false;
            if(source.Output.Plan.PendingIndex<0)return RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.Output.Plan.Native,source.Output.Plan.Offset);
            return source.Pending?.Saved==true&&source.Pending.Returned&&MatchesSavedStageDescription(source,"nativePendingUpload",source.Nonce)&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.Path+".nativePending",source.Pending.Record,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(source.Station,source.Pending.Plan.After,source.Pending.Plan.AfterOffset);
        }
    }
    private static bool TryNativeFinalDigest(Original source,out byte kind,out string digest)
    {
        kind=0;digest=null;if(!NativeFinalCurrent(source))return false;
        kind=(byte)(source.Pending==null?1:2);string suffix=kind==1?"nativeOutputSaved":"nativePendingSaved";
        if(!MatchesSavedStageDescription(source,suffix,source.Nonce))return false;digest=Hash(Read(source.Path+"."+suffix,8192));return true;
    }
    private static bool ReleaseNativeFinal(Original source)
    {
        if(source==null)return false;string path=source.Path+".closed";
        if(!Safe(path)||File.Exists(path+".tmp")||!File.Exists(path))return false;
        byte[] proof=System.Text.Encoding.ASCII.GetBytes(Hash(Read(path,4096)));
        bool already=File.Exists(source.Path+".nativeReleased");
        if(already&&!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.Path+".nativeReleased",proof))return false;
        if(!already)
        {
        bool adopted=RebirthProductiveEarnedRemoteClosedRecord.TryRead(Read(path,4096),out var closed)&&
            closed.Operation==source.Frame.Operation&&closed.Birth==source.Frame.Birth&&closed.Nonce==source.Nonce&&closed.OriginalDigest==source.Digest&&
            SavedNativeAdoption(source.Path,closed);
        if(source.Pending?.Plan.Child!=null&&!adopted)
        {
            var pending=source.Pending;var frame=pending.Plan.Child;
            if(!pending.Saved||!pending.Returned||pending.ChildNonce==Guid.Empty||!MatchesSavedStageDescription(source,"nativePendingUpload",source.Nonce))return false;
            Original child=null;
            if(originals.TryGetValue(source.Station,out var retained)&&!ReferenceEquals(retained,source)&&retained.Frame.Operation==frame.Operation&&
                retained.Image.SequenceEqual(pending.Plan.ChildImage)&&retained.Nonce==pending.ChildNonce)child=retained;
            if(child==null)
            {
            child=new Original{Actor=source.Actor,Station=source.Station,World=source.World,Frame=frame,Image=pending.Plan.ChildImage,Owner=source.Owner,Creation=source.Creation,
                WorldKey=source.WorldKey,Deaths=source.Deaths,Root=source.Root,Path=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(source.Path),frame.Operation.ToString("N")+".rew"),
                Digest=Hash(pending.Plan.ChildImage),Nonce=pending.ChildNonce,Attempted=true,Applied=true,BeforeSent=true,AfterSent=true,Published=true,Recovered=true,
                HasReadFence=source.HasReadFence,ReadFence=source.ReadFence};
            if(!source.Identity()||frame.RowIndex<0||frame.RowIndex>=source.Station.Queue.Length)return false;
            child.Row=source.Station.Queue[frame.RowIndex];child.Recipe=child.Row?.Recipe;
            if(child.Recipe==null||!WriteExact(child.Path,child.Image,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||!WriteExact(child.Path+".scope",Scope(child),2048))return false;
            // These historical stages describe this very paid-parent native
            // invocation and its positively saved upload; no AddRecipe replay.
            foreach(string stage in new[]{"accepted","attempt","applied","afterUpload","published"})
                if(!WriteExact(child.Path+"."+stage,StageBytes(child,stage,child.Nonce),2048))return false;
            child.Memory=RebirthProductiveEarnedObservationBudget.TryOpenOwner(child.Actor,frame,child.Image);if(child.Memory==null)return false;
            if(originals.TryGetValue(child.Station,out var old)&&!ReferenceEquals(old,source)&&old.Frame.Operation!=frame.Operation){child.Memory.Dispose();return false;}
            originals[child.Station]=child;
            }
        }
        if(!WriteExact(source.Path+".nativeReleased",proof,128))return false;
        }
        source.Tools?.Dispose();source.Tools=null;source.Pending?.Dispose();source.Pending=null;source.Output?.Dispose();source.Output=null;source.Memory?.Dispose();source.Memory=null;return true;
    }
    private static bool ReleaseRecoveredNativeFinal(EntityPlayerLocal actor,string originalPath,RebirthProductiveEarnedRemoteClosedRecord.Description closed,byte[] closedBytes)
    {
        if(actor==null||closed==null||!Safe(originalPath)||File.Exists(originalPath+".closed.tmp")||
            !Read(originalPath+".closed",4096).SequenceEqual(closedBytes))return false;
        byte[] release=System.Text.Encoding.ASCII.GetBytes(Hash(closedBytes));
        if(File.Exists(originalPath+".nativeReleased"))return RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(originalPath+".nativeReleased",release);
        if(closed.Kind==1||SavedNativeAdoption(originalPath,closed))return WriteExact(originalPath+".nativeReleased",release,128);
        var world=actor.world;var station=world?.GetTileEntity(closed.Position) as TileEntityWorkstation;
        if(world?.IsRemote()!=true||!ReferenceEquals(world,GameManager.Instance?.World)||station==null||actor.Died!=closed.Deaths||
            RebirthCamperCharacterAccess.CurrentCreation(actor)!=closed.Creation||RebirthProductiveEarnedArtifact.WorldIdentity(world)!=closed.World||
            !RebirthStablePlayerIdentity.TryFromLocalPlatform(actor,out var identity)||identity.StorageKey!=closed.Owner)return false;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return false;
            byte[] image=Read(originalPath,RebirthProductiveEarnedRowProof.MaximumArtifactBytes);var frame=RebirthProductiveEarnedRowProof.Decode(image);
            if(Hash(image)!=closed.OriginalDigest||frame.Operation!=closed.Operation||frame.Birth!=closed.Birth||frame.World!=closed.World||!decoder.Admit(frame,image.Length))return false;
            var source=new Original{Actor=actor,World=world,Station=station,Frame=frame,Image=image,Owner=closed.Owner,Creation=closed.Creation,WorldKey=closed.World,
                Deaths=closed.Deaths,Root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()),Path=originalPath,Digest=closed.OriginalDigest,Nonce=closed.Nonce,Published=true};
            // Restore only immutable descriptions. The adopted child has no
            // native-read fence until its own authenticated original echo arrives.
            if(!LoadNativeOutputDescription(source,decoder)||!LoadNativeToolsDescription(source,decoder)||!LoadNativePendingDescription(source,decoder)||source.Pending?.Saved!=true)return false;
            try{return ReleaseNativeFinal(source);}
            finally{source.Tools?.Dispose();source.Pending?.Dispose();source.Output?.Dispose();source.Memory?.Dispose();}
        }
    }
}
