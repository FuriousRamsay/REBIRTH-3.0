using System;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal static bool AdoptLoadedViewerTools(TileEntityWorkstation station,RebirthProductiveEarnedSavedNativeSource.Evidence source)
    {
        if(!RebirthProductiveEarnedRemoteHost.IsNativeHandoffTick(station)||source?.ToolsRebased!=true||!source.Terminal||!source.PendingNextWork||
            source.Frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(GameManager.Instance.World)||
            !source.PrimaryCurrent()||!source.CurrentIdentity(GameManager.Instance.World,station,source.Frame.Birth,source.Frame.Operation)||
            !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset))return false;
        if(!retained.TryGetValue(station,out var original))return true; // The existing cold native restore owns its graph creation.
        if(original.Frame.Operation!=source.Frame.Operation||original.Frame.Birth!=source.Frame.Birth||!original.OutputTimerObserved||
            original.Terminal==null||original.PartialSuccessor!=null||original.EarnedChild!=null||original.ClearedNativeMarker||
            original.PendingTimerIndex!=source.PendingTimerIndex||original.PendingTimerBefore!=source.PendingTimerBefore||original.PendingTimerTarget!=source.PendingTimerTarget)return false;
        if(HasAdoptedRemoteViewerTools(station,source.Frame.Operation,source.Native,source.NativeOffset))return true;
        RebirthProductiveEarnedArtifact.Prepared artifact;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||!decoder.Admit(source.Frame,source.Original.Length)||
                !RebirthProductiveEarnedArtifact.TryReadRemoteObservedOriginal(station,source.Frame.Birth,source.Frame.Operation,out artifact,out _))return false;
        }
        var allocation=original.Terminal.Memory.TryObserveNative(source.Native,source.NativeOffset);if(allocation==null)return false;
        if(!source.PrimaryCurrent()||!artifact.Current()||!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset))
        {allocation.Dispose();return false;}
        var terminal=original.Terminal;var previous=terminal.Allocation;terminal.Allocation=allocation;
        original.Artifact=artifact;original.NativeTickCustody=true;terminal.Body=(byte[])source.Native.Clone();terminal.ElapsedOffset=source.NativeOffset;
        terminal.SavedOrigin=source;terminal.RemoteToolsRebased=true;terminal.RemoteSavedSource=()=>source.PrimaryCurrent()&&artifact.Current();
        previous?.Dispose();return true;
    }
}
