using System;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal sealed class MobileLocalTools:IDisposable
    {
        internal byte[] Record,Before,After;
        internal bool Reentry,Entered,ProjectionReady;
        internal IDisposable Retention;
        internal RebirthCamperStationSaveEvidence.Request Save;
        internal RebirthCamperStationSaveEvidence.Publication Saved;
        public void Dispose(){Save?.Dispose();Saved?.Dispose();Retention?.Dispose();Save=null;Saved=null;Retention=null;Record=Before=After=null;}
    }
    private static bool PumpMobileLocalTools(Attempt original)
    {
        var terminal=original.MobileTerminal;var source=terminal?.PendingLocalTools;
        if(source==null)return true;
        try
        {
            if(source.Reentry&&!source.Entered)
            {
                // Same original native station-sync grant: actual saved owner
                // debit/returned gesture and actual installed native Before
                // were jointly authenticated by the cold source reader. Only
                // its tools sink is still owed; no owner inventory is replayed.
                if(!IsMobileSettlementSource(original.Station)||!terminal.Current()||
                    !RebirthProductiveEarnedMobileOrigin.Canonical(terminal.Station,terminal.Installed).SequenceEqual(source.Before))return false;
                byte[] receipt=RebirthProductiveEarnedMobileOrigin.Digest(source.Record);
                string path=original.Artifact.OriginalPath+".mobile-tools-reentry";
                if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,receipt)||
                    !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path+".invoking",receipt)||!terminal.Current())return false;
                source.Entered=true;
                var after=RebirthCamperStationSnapshot.Restore(original.Artifact.Birth,terminal.Installed,source.After);
                terminal.Station.Tools=ItemStack.Clone(after.Tools);terminal.Station.setModified();
            }
            Func<bool> current=()=>original.Artifact.Current()&&original.Artifact.MatchesMarker(terminal.Station.CopyProductiveEarnedMarker())&&
                RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(original.Artifact.OriginalPath+".mobile-tools-plan",source.Record,
                    RebirthProductiveEarnedMobileOrigin.MaximumBytes)&&
                RebirthProductiveEarnedMobileOrigin.Canonical(terminal.Station,terminal.Installed).SequenceEqual(source.After);
            if(!current())return false;
            if(source.Reentry&&!source.ProjectionReady)
            {
                if(!IsMobileSettlementSource(original.Station)||
                    !RebirthCamperStationOpenQueueProcessing.RefreshAfterOriginal(terminal.Station)||!current())return false;
                source.ProjectionReady=true;
                source.Reentry=false;
            }
            if(source.Save==null&&source.Saved==null)
            {
                var runtime=RebirthCamperModuleRuntime.For(terminal.Station.BoundVehicle);
                source.Save=RebirthCamperStationSaveEvidence.BeginProcessingRecovery(terminal.Station,
                    new RebirthCamperStationAddress(runtime.Identity,terminal.Station.InstallationId),current,terminal.Memory);
            }
            if(source.Saved==null)
            {if(!RebirthCamperStationSaveEvidence.TryObserve(source.Save,out var saved))return false;source.Saved=saved;}
            using(var scope=terminal.Memory.TryEnterCallbacks())
                if(scope==null||!source.Saved.ReadStationSnapshot(state=>
                    original.Artifact.MatchesMarker(state.CopyProductiveEarnedMarker())&&
                    RebirthProductiveEarnedMobileOrigin.Canonical(state,terminal.Installed).SequenceEqual(source.After))||!current()||
                    !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(original.Artifact.OriginalPath+".mobile-tools-rebased",
                        RebirthProductiveEarnedMobileOrigin.Digest(source.Record)))return false;
            terminal.Body=source.After;source.After=null;terminal.ToolRecord=source.Record;source.Record=null;
            terminal.ToolRetention=source.Retention;source.Retention=null;
            source.Dispose();terminal.PendingLocalTools=null;return true;
        }
        catch{return false;}
    }
}
