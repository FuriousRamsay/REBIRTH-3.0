using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    private static void RetireReplacedNativeGraph(TileEntityWorkstation station,RebirthProductiveEarnedSavedNativeSource.Evidence source)
    {
        if(!RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station)||!source.PrimaryCurrent()||
            !source.CurrentIdentity(GameManager.Instance?.World,station,source.Frame.Birth,source.Frame.Operation)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset))return;
        foreach(var old in retained.Values.Where(value=>!ReferenceEquals(value.Station,station)&&
            value.Station.ToWorldPos().Equals(station.ToWorldPos())).ToArray())
        {
            if(old.Artifact==null||old.Artifact.NativeRetirementIdentityCurrent())continue;
            bool original=old.Artifact.Birth==source.Frame.Birth&&old.Artifact.Operation==source.Frame.Operation&&
                RebirthProductiveEarnedArtifact.FileMatches(old.Artifact.OriginalPath,source.Original);
            bool child=old.EarnedChild?.Frame?.Birth==source.Frame.Birth&&old.EarnedChild.Frame.Operation==source.Frame.Operation&&
                source.Original.SequenceEqual(old.EarnedChild.Image)&&!source.Terminal&&
                RebirthProductiveEarnedNativeBody.SameExceptElapsed(source.Native,source.NativeOffset,old.EarnedChild.Frame.AfterNative,old.EarnedChild.Frame.AfterElapsedOffset);
            if(!child)
            {
                if(!original||old.EarnedChild?.Entered==true)continue;
                if(old.PartialSuccessor!=null)
                {
                    if(!source.Terminal||source.PendingNextWork||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(source.Native,source.NativeOffset,
                        old.PartialSuccessor.After,old.PartialSuccessor.AfterOffset))continue;
                }
                else if(source.Terminal)
                {if(source.PendingNextWork&&old.OutputTimerObserved&&!old.PendingTimerObserved)continue;}
                else if(old.NativeOutput!=null||old.OutputTimerObserved)continue; // A saved earned prefix cannot settle an entered output.
            }
            old.EarnedChild?.Dispose();old.PartialSuccessor?.Dispose();old.Terminal?.Dispose();old.Observation?.Dispose();
            old.PendingDefinitionRetention?.Dispose();old.Retention?.Dispose();old.LoadedCleanup?.Dispose();
            if(!ReferenceEquals(old.Artifact.LoadedSource,source))RebirthProductiveEarnedSavedNativeSource.Release(old.Artifact.LoadedSource);
            retained.Remove(old.Station);
        }
    }
}
