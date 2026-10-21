using System;
using System.IO;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static bool ClearRemoteNativeFinal(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
    {
        if(artifact?.Current()!=true||!artifact.IsTarget(station))return false;
        byte[] marker=artifact.Marker(artifact.ReadFrame());
        return artifact.CaptureCompletionRetention(marker)!=null&&ClearExactMarker(station,marker);
    }
    internal static bool AcceptRemoteNativeChildMarker(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared parent,RebirthProductiveEarnedArtifact.Prepared child,RebirthProductiveEarnedRowProof.Frame frame)
    {
        if(parent?.Current()!=true||child?.Current()!=true||!parent.IsTarget(station)||!child.IsTarget(station)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,frame.AfterNative,frame.AfterElapsedOffset))return false;
        var parentFrame=RebirthProductiveEarnedRowProof.Decode(parent.CopyImage());
        if(TryNativeMarker(station,out var current)&&child.MatchesMarker(current))return true;
        return ReplaceExactNativeMarker(station,parent.Marker(parentFrame),child.Marker(frame));
    }
    internal static bool PublishRemoteNativePartial(RebirthProductiveEarnedArtifact.Prepared artifact,RebirthProductiveEarnedObservationBudget memory,
        RebirthRemoteResourceNativePublication.Watch watch,RebirthProductiveEarnedNativePendingPlan.Description plan,Func<bool> current,bool toolsRebased=false)
    {
        if(artifact?.Current()!=true||plan?.Child!=null||watch==null||current?.Invoke()!=true)return false;
        byte[] record=RebirthProductiveEarnedNativePendingPlan.PartialRecord(plan);
        if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(artifact.OriginalPath+".partial-successor",record)||
            !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(artifact.OriginalPath+".partial-successor.invoking",record))return false;
        return RebirthProductiveEarnedSavedNativeSource.RegisterInvokedPartial(artifact.OriginalPath+(toolsRebased?".tools-load":".terminal-load"),GameIO.GetSaveGameDir(),artifact.LoadedSource)&&
            RebirthProductiveEarnedSavedNativeSource.Publish(artifact,memory,watch,plan.After,plan.AfterOffset,true,current,partialSuccessor:true);
    }
}
