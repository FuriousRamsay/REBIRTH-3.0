using System;
using System.IO;

internal static partial class RebirthProductiveEarnedArtifact
{
    internal static bool TryBindStagedChild(TileEntityWorkstation station,Prepared parent,
        RebirthProductiveEarnedSavedNativeSource.Evidence plan,out Prepared prepared)
    {
        prepared=null;
        try
        {
            var world=GameManager.Instance?.World;var source=parent?.LoadedSource;
            if(!RebirthRecyclerNativeLoop.IsCurrentInvocation(station)||source?.Terminal!=true||!source.PendingNextWork||
                !parent.IsTarget(station)||!parent.Current()||!source.CurrentIdentity(world,station,parent.Birth,parent.Operation)||
                plan?.StagedChildLink==null||!plan.PrimaryCurrent()||plan.Load?.PredictedNative!=true||
                !ReferenceEquals(plan.Load.Custody,plan)||plan.Load.Loaded!=null||
                plan.Frame.World!=WorldIdentity(world)||plan.Frame.RowIndex!=source.PendingTimerIndex||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(plan.Frame.BeforeNative,plan.Frame.BeforeElapsedOffset,source.Native,source.NativeOffset)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset)||
                !NativeCurrent(world,station,parent.CustodyBirth)||!FileMatches(plan.Path,plan.Image))return false;
            string invoking=parent.OriginalPath+".earned-child.invoking";
            if(File.Exists(invoking)||File.Exists(invoking+".tmp")||
                !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(parent.OriginalPath+".earned-child",plan.StagedChildLink))return false;
            // Historical child Birth/Operation and immutable bytes stay exact.
            // Only native object custody is rebound, from the actual qualified
            // Loaded predecessor, and remains pinned to this live incarnation.
            var result=new Prepared(world,station,Path.GetFullPath(GameIO.GetSaveGameDir()),plan.Path,plan.Image,
                plan.Frame.Birth,plan.Frame.Operation,parent.CustodyBirth);
            if(!result.Current()||!parent.Current()||!plan.PrimaryCurrent())return false;
            prepared=result;return true;
        }
        catch{return false;}
    }
}
