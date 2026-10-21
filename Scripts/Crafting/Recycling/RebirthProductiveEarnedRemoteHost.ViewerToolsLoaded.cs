using System;
using System.IO;

internal static partial class RebirthProductiveEarnedRemoteHost
{
    // Actual owning tick + independently installed saved native tools source.
    // This completes only old payer metadata, including after owner detach or
    // death. No player/station setter, sync, output or new financial scope.
    private static bool TryReleaseLoadedViewerTools(TileEntityWorkstation station,RebirthProductiveEarnedSavedNativeSource.Evidence source)
    {
        try
        {
            if(!IsNativeHandoffTick(station)||source?.ToolsRebased!=true||!source.Terminal||!source.PendingNextWork||!source.PrimaryCurrent()||
                source.Frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(GameManager.Instance.World)||
                !string.Equals(Path.GetFullPath(source.Directory),Path.GetFullPath(GameIO.GetSaveGameDir()),RebirthStationCompletionOriginalScope.PathComparison)||
                !source.CurrentIdentity(GameManager.Instance.World,station,source.Frame.Birth,source.Frame.Operation)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset))return false;
            string releasedOriginal=null,header=null;byte[] releaseHold=null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!decoder.Admit(source.Frame,source.Original.Length))return false;
                string original=Path.Combine(source.Directory,"RebirthProductiveEarned",source.Frame.Birth.ToString("N"),source.Frame.Operation.ToString("N")+".rew");
                if(!File.Exists(original+".viewer-tools-hold")||
                    !RebirthProductiveEarnedRemoteToolRebaseRecord.TryRead(original,source.Original,source.Native,source.NativeOffset,out var history,out _))return false;
                using(history)
                {
                    if(history.ViewerSource==null||source.PendingTimerIndex!=history.Paid.PendingIndex||source.PendingTimerBefore!=history.Paid.PendingBefore||
                        source.PendingTimerTarget!=history.Paid.PendingTarget||!source.PrimaryCurrent())return false;
                    byte[] hold=System.Text.Encoding.UTF8.GetBytes(history.Header+"\n"+Digest(history.HeaderImage)+"\n"+Digest(history.ViewerSource));
                    if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original+".viewer-tools-hold",hold)||
                        !RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(history.Header+".nativeTools",history.PlanImage,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||
                        !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(history.Header+".nativeToolsSaved",System.Text.Encoding.ASCII.GetBytes(Digest(history.PlanImage))))return false;
                    releasedOriginal=original;header=history.Header;releaseHold=hold;
                }
            }
            if(!RebirthProductiveEarnedCoordinator.AdoptLoadedViewerTools(station,source)||!source.PrimaryCurrent()||
                !source.CurrentIdentity(GameManager.Instance.World,station,source.Frame.Birth,source.Frame.Operation)||
                !RebirthProductiveEarnedNativeBody.MatchesCurrent(station,source.Native,source.NativeOffset)||
                !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(releasedOriginal+".viewer-tools-released",System.Text.Encoding.ASCII.GetBytes(Digest(releaseHold))))return false;
            if(pending.TryGetValue(source.Frame.Operation,out var payer)&&payer.ViewerToolsOnly&&payer.HeaderPath==header)
            {
                payer.ViewerToolsReleased=true;if(payer.Tools!=null)payer.Tools.Saved=true;
                payer.ViewerToolsSource?.Dispose();payer.ViewerToolsSource=null;
            }
            return true;
        }
        catch{return false;}
    }
}
