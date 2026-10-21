using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    internal static bool HasRegisteredEarnedOriginal(RebirthProductiveEarnedArtifact.Prepared artifact)
    {
        if(artifact?.Current()!=true)return false;
        lock(cachedLoadGate)
            return cachedLoadRequests.Any(request=>!request.Custody.Terminal&&request.Custody.Path==artifact.OriginalPath+".earned-load"&&
                request.Custody.Frame.Birth==artifact.Birth&&request.Custody.Frame.Operation==artifact.Operation&&request.Custody.PrimaryCurrent()&&
                RebirthProductiveEarnedArtifact.FileMatches(artifact.OriginalPath,request.Custody.Original));
    }
    internal static bool ToolsPublicationCurrent(Guid birth,Guid operation)
    {
        lock(cachedLoadGate)
        {
            var matches=cachedLoadRequests.Where(request=>request.Custody.ToolsRebased&&request.Custody.PendingNextWork&&
                request.Custody.Frame.Birth==birth&&request.Custody.Frame.Operation==operation).ToArray();
            return matches.Length==1&&matches[0].Custody.PrimaryCurrent();
        }
    }
    private static bool TryReadToolRebase(Evidence source)
    {
        const string suffix=".tools-load";
        if(!source.Path.EndsWith(suffix,StringComparison.Ordinal))return true;
        if(!source.Terminal||!source.PendingNextWork||source.PendingTimerAmbiguous)return false;
        string original=source.Path.Substring(0,source.Path.Length-suffix.Length);
        if(File.Exists(original+".remote-tools-rebased"))
        {
            if(!RebirthProductiveEarnedRemoteToolRebaseRecord.TryRead(original,source.Original,source.Native,source.NativeOffset,out var remote,out var remoteRecord))return false;
            using(remote)
            {
            if(source.PendingTimerIndex!=remote.Paid.PendingIndex||source.PendingTimerBefore!=remote.Paid.PendingBefore||source.PendingTimerTarget!=remote.Paid.PendingTarget)return false;
            source.ToolsRebased=true;source.ToolsBefore=remote.Plan.Before;source.ToolsBeforeOffset=remote.Plan.BeforeOffset;
            source.Dependencies=RemoteToolDependencies(original,remote,remoteRecord);
            return true;
            }
        }
        if(!RebirthProductiveEarnedPendingToolRebaseRecord.TryRead(original,source.Original,source.Native,source.NativeOffset,out var rebase,out var record)||
            rebase.Birth!=source.Frame.Birth||rebase.Source!=source.Frame.Operation||rebase.Index!=source.PendingTimerIndex||
            rebase.BeforeTimer!=source.PendingTimerBefore||rebase.TargetTimer!=source.PendingTimerTarget)return false;
        if(!RebirthProductiveEarnedPendingToolGestures.TryInspectGestureJournal(original,out var journal))return false;
        using(journal)
            if(journal.WorldKey!=source.Frame.World||!string.Equals(Path.GetFullPath(journal.SaveRoot),Path.GetFullPath(source.Directory),RebirthStationCompletionOriginalScope.PathComparison))return false;
        source.ToolsRebased=true;source.ToolsBefore=rebase.Before;source.ToolsBeforeOffset=rebase.BeforeOffset;
        source.Dependencies=new[]{NativeChildDependencyFor(original+".tools-rebased",record)};
        return true;
    }
}
