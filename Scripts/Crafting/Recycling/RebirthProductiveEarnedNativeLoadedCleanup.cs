using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static bool NeedsLoadedNativeCleanup(Guid birth,Guid operation)
        =>ThreadManager.IsMainThread()&&retained.Values.Any(value=>!value.ClearedNativeMarker&&value.Artifact!=null&&value.OutputTimerObserved&&
            value.Artifact.Birth==birth&&value.Artifact.Operation==operation&&value.EarnedChild==null&&
            (value.LoadedCleanup!=null||value.PartialSuccessor?.Returned==true||value.PartialSuccessor==null&&value.Terminal!=null&&!value.Terminal.PendingNextWork));
    private static void PumpLoadedNativeCleanup()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var original in retained.Values.ToArray())
        {
            try
            {
                // Child preparation/application is its own operation custody.
                // Only ordinary complete terminals or returned partial work
                // can retire through this actual loaded terminal receipt.
                if(original.ClearedNativeMarker||original.Artifact==null||!original.OutputTimerObserved||original.EarnedChild!=null)continue;
                var proof=original.LoadedCleanup;
                if(proof==null)
                {
                    byte[] body;int offset;
                    if(original.PartialSuccessor!=null)
                    {
                        if(!original.PartialSuccessor.Returned)continue;
                        body=original.PartialSuccessor.After;offset=original.PartialSuccessor.AfterOffset;
                    }
                    else
                    {
                        if(original.Terminal==null||original.Terminal.PendingNextWork)continue;
                        body=original.Terminal.Body;offset=original.Terminal.ElapsedOffset;
                    }
                    if(!RebirthProductiveEarnedSavedNativeSource.TryObservedCompletedNativeLoad(original.Artifact.Birth,
                        original.Artifact.Operation,original.Artifact.OriginalDigest(),body,offset,out proof)||!proof.Current())continue;
                    original.LoadedCleanup=proof;
                }
                if(!proof.Current())continue;
                // No world or file mutation. A later failed disposal retains
                // the receipt; it cannot authorize another native attempt.
                original.PartialSuccessor?.Dispose();original.PartialSuccessor=null;
                original.Terminal?.Dispose();original.Terminal=null;
                original.Observation?.Dispose();original.Observation=null;
                RebirthProductiveEarnedSavedNativeSource.Release(original.Artifact.LoadedSource);
                original.PendingDefinitionRetention?.Dispose();original.PendingDefinitionRetention=null;
                original.Retention?.Dispose();original.Retention=null;original.Tools=null;original.Frame=null;
                retained.Remove(original.Station);original.LoadedCleanup=null;proof.Dispose();
            }
            catch{/* Preserve the receipt and remaining custody on cleanup failure. */}
        }
    }
}
