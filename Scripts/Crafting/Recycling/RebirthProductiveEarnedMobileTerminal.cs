using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    internal sealed class MobileTerminal:IDisposable
    {
        internal Attempt Original;
        internal RebirthCamperStationState Station;
        internal ItemStack Installed;
        internal byte[] Body,Record,Plan;
        internal byte[] ToolRecord;internal IDisposable ToolRetention;
        internal readonly System.Collections.Generic.List<MobileRemoteTools> RemoteTools=new System.Collections.Generic.List<MobileRemoteTools>();
        internal MobileRemoteTools PendingRemoteTools;
        internal MobileLocalTools PendingLocalTools;
        internal RebirthProductiveEarnedObservationBudget Memory;
        internal RebirthProductiveEarnedObservationBudget.Observation Allocation;
        internal RebirthCamperStationSaveEvidence.Request Save;
        internal RebirthCamperStationSaveEvidence.Publication Saved;
        internal IDisposable Retention;
        internal bool Positive,Clearing,Closed;
        internal bool Current()
        {
            try
            {
                using(var scope=Memory?.TryEnterCallbacks())
                    return scope!=null&&!Closed&&Original?.Artifact?.Current()==true&&Station?.BoundVehicle!=null&&
                        RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Original.Artifact.OriginalPath+".mobile-output-invoking",Original.Artifact.OriginalDigest())&&
                        (Plan==null||RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Original.Artifact.OriginalPath+".mobile-terminal-plan",Plan,
                            RebirthProductiveEarnedMobileOrigin.MaximumBytes))&&
                        (!Positive||Record!=null&&RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Original.Artifact.OriginalPath+".mobile-terminal",Record,
                            RebirthProductiveEarnedMobileOrigin.MaximumBytes))&&
                        (ToolRecord==null||RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(Original.Artifact.OriginalPath+".mobile-tools-plan",ToolRecord,
                            RebirthProductiveEarnedMobileOrigin.MaximumBytes)&&RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(
                            Original.Artifact.OriginalPath+".mobile-tools-rebased",RebirthProductiveEarnedMobileOrigin.Digest(ToolRecord)))&&
                        RemoteTools.All(source=>source.Current())&&
                        RebirthProductiveEarnedMobileOrigin.Canonical(Station,Installed).SequenceEqual(Body)&&
                        (Clearing?Station.CopyProductiveEarnedMarker().Length==0:
                            Original.Artifact.MatchesMarker(Station.CopyProductiveEarnedMarker()));
            }
            catch{return false;}
        }
        public void Dispose()
        {
            if(Closed)return;
            Save?.Dispose();Saved?.Dispose();Allocation?.Dispose();Memory?.Dispose();Retention?.Dispose();ToolRetention?.Dispose();ToolRetention=null;ToolRecord=null;
            PendingRemoteTools?.Dispose();PendingRemoteTools=null;foreach(var source in RemoteTools)source.Dispose();RemoteTools.Clear();
            PendingLocalTools?.Dispose();PendingLocalTools=null;
            Closed=true;Save=null;Saved=null;Allocation=null;Memory=null;Retention=null;Original=null;Station=null;Installed=null;Body=Record=Plan=null;
        }
    }
    private static void CaptureReturnedMobileTerminal(Attempt original)
    {
        MobileTerminal value=null;
        try
        {
            if(original.MobileTerminal!=null||!original.MobileOutputReturned||original.MobileReturnUnknown||original.MobileReturnedImage==null||
                original.Station is not RebirthCamperStationState station||original.Artifact?.Current()!=true)return;
            original.Observation?.Dispose();original.Observation=null;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return;
                var installed=RebirthCamperModuleRuntime.For(station.BoundVehicle).Stations.Find(station.InstallationId);
                if(!ReferenceEquals(installed?.State,station))return;
                var frozen=RebirthCamperStationSnapshot.Restore(station.InstallationId,installed.InstalledItem,original.MobileReturnedImage);
                byte[] body=RebirthProductiveEarnedMobileOrigin.Canonical(frozen,installed.InstalledItem);
                if(!original.Artifact.MatchesMarker(frozen.CopyProductiveEarnedMarker())||
                    !RebirthProductiveEarnedMobileOrigin.Canonical(station,installed.InstalledItem).SequenceEqual(body))return;
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(4L*body.Length+262144),out var retention))return;
                value=new MobileTerminal{Original=original,Station=station,Installed=installed.InstalledItem,Body=body,Retention=retention};
            }
            value.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(original.Artifact,original.Frame);
            if(value.Memory==null)return;
            value.Allocation=value.Memory.TryObserveMobile(value.Station);
            if(value.Allocation==null||!value.Current())return;
            value.Plan=EncodeMobileTerminalPlan(original,value.Body);
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(original.Artifact.OriginalPath+".mobile-terminal-plan",value.Plan,
                RebirthProductiveEarnedMobileOrigin.MaximumBytes))return;
            original.MobileTerminal=value;value=null;
            PumpMobileTerminal(original);
        }
        catch{/* Exact attempted original remains held; output is never replayed. */}
        finally{value?.Dispose();}
    }
    private static bool PumpMobileTerminal(Attempt original)
    {
        var value=original?.MobileTerminal;
        try
        {
            if(value!=null&&!EnsureMobileTerminalObservation(value))return false;
            if(value?.PendingRemoteTools!=null&&!(value.PendingRemoteTools.Reentry?PumpMobileRemoteReentry(original):RetryMobileRemoteTools(original)))return false;
            if(value?.PendingLocalTools!=null&&!PumpMobileLocalTools(original))return false;
            if(MobileSuccessorNeedsFinish(original))return FinishMobileSuccessor(original);
            if(value?.Current()!=true)return false;
            if(value.Positive&&!value.Clearing&&original.PendingTimerObserved)
            {
                bool continued=ContinueMobilePending(original);
                if(original.MobileSuccessor==null)
                {
                    value.Save?.Dispose();value.Save=null;value.Saved?.Dispose();value.Saved=null;
                    value.Allocation?.Dispose();value.Allocation=null;
                }
                return continued;
            }
            var runtime=RebirthCamperModuleRuntime.For(value.Station.BoundVehicle);
            if(value.Save==null&&value.Saved==null)
                value.Save=RebirthCamperStationSaveEvidence.BeginProcessingRecovery(value.Station,
                    new RebirthCamperStationAddress(runtime.Identity,value.Station.InstallationId),value.Current,value.Memory);
            if(value.Saved==null)
            {
                if(value.Save==null||!RebirthCamperStationSaveEvidence.TryObserve(value.Save,out var saved))return false;
                value.Saved=saved;
            }
            using(var callback=value.Memory.TryEnterCallbacks())
            {
                if(callback==null||!value.Saved.ReadStationSnapshot(state=>
                    RebirthProductiveEarnedMobileOrigin.Canonical(state,value.Installed).SequenceEqual(value.Body)&&
                    (value.Clearing?state.CopyProductiveEarnedMarker().Length==0:
                        original.Artifact.MatchesMarker(state.CopyProductiveEarnedMarker())))||!value.Current())return false;
                if(value.Clearing)
                {
                    if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(original.Artifact.OriginalPath+".mobile-released",
                        RebirthProductiveEarnedMobileOrigin.Digest(value.Record)))return false;
                    RecordMobileReleasedStatus(original);
                    value.Dispose();original.MobileTerminal=null;
                    original.MobileSuccessor?.Dispose();original.MobileSuccessor=null;
                    original.Observation?.Dispose();original.PendingDefinitionRetention?.Dispose();original.Retention?.Dispose();
                    retained.Remove(original.Station);return true;
                }
                if(!value.Positive)
                {
                    value.Record=EncodeMobileTerminal(original,value.Body,value.Saved);
                    if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(original.Artifact.OriginalPath+".mobile-terminal",
                        value.Record,RebirthProductiveEarnedMobileOrigin.MaximumBytes))return false;
                    value.Positive=true;
                }
            }
            if(original.PendingTimerAmbiguous)return false;
            if(original.PendingTimerObserved)return ContinueMobilePending(original);
            var next=value.Station.Queue?.LastOrDefault();
            if(next?.Recipe!=null&&next.Multiplier>0&&next.CraftingTimeLeft<=0)return false;
            return BeginMobileClear(original);
        }
        catch{return false;}
    }
    private static bool EnsureMobileTerminalObservation(MobileTerminal value)
    {
        if(value==null||value.Closed)return false;
        if(value.Memory==null)value.Memory=RebirthProductiveEarnedObservationBudget.TryOpen(value.Original.Artifact,value.Original.Frame);
        if(value.Memory==null)return false;
        if(value.Allocation==null)value.Allocation=value.Memory.TryObserveMobile(value.Station);
        return value.Allocation!=null;
    }
    private static bool MobileSuccessorNeedsFinish(Attempt original)
        =>original?.MobileSuccessor?.Returned==true&&original.MobileTerminal?.Clearing!=true;
    private static byte[] EncodeMobileTerminal(Attempt original,byte[] body,RebirthCamperStationSaveEvidence.Publication saved)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x31544d52);writer.Write(original.Artifact.OriginalDigest());
            writer.Write(original.PendingTimerObserved);writer.Write(original.PendingTimerAmbiguous);
            writer.Write(original.PendingTimerIndex);writer.Write(original.PendingTimerBefore);writer.Write(original.PendingTimerTarget);
            writer.Write(original.PendingRecipeBinding??"");writer.Write(original.PendingExtras??"");
            writer.Write(body.Length);writer.Write(body);
            writer.Write(saved.SaveDirectory);writer.Write(saved.VehicleEntityId);writer.Write(saved.VehicleClass);
            writer.Write(saved.VehicleEntityDigest);writer.Write(saved.VehicleBagDigest);writer.Write(saved.StationDigest);
            writer.Flush();return stream.ToArray();
        }
    }
    private static byte[] EncodeMobileTerminalPlan(Attempt original,byte[] body)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            writer.Write(0x31544d52);writer.Write(original.Artifact.OriginalDigest());
            writer.Write(original.PendingTimerObserved);writer.Write(original.PendingTimerAmbiguous);
            writer.Write(original.PendingTimerIndex);writer.Write(original.PendingTimerBefore);writer.Write(original.PendingTimerTarget);
            writer.Write(original.PendingRecipeBinding??"");writer.Write(original.PendingExtras??"");
            writer.Write(body.Length);writer.Write(body);writer.Flush();return stream.ToArray();
        }
    }
    private static bool BeginMobileClear(Attempt original)
    {
        var value=original.MobileTerminal;
        if(value?.Positive!=true||!value.Current())return false;
        string path=original.Artifact.OriginalPath+".mobile-clearing";
        if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(path,RebirthProductiveEarnedMobileOrigin.Digest(value.Record)))return false;
        value.Save?.Dispose();value.Save=null;value.Saved?.Dispose();value.Saved=null;
        if(!value.Current())return false;
        value.Station.RestoreProductiveEarnedMarker(true,Array.Empty<byte>());value.Station.setModified();value.Clearing=true;
        return PumpMobileTerminal(original);
    }
    // The actual mobile owner invokes this before its fuel/work gate. Saved
    // evidence pumping is read-only; pending settlement has its own finite WAL.
    internal static bool HoldMobileTick(RebirthCamperStationState station)
    {
        if(!Enabled)return false;ObserveWorld();
        if(!retained.TryGetValue(station,out var original))
        {
            TryRestoreMobileTerminal(station);
            if(!retained.TryGetValue(station,out original))
            {
                if(TryNativeMarker(station,out _))
                {
                    var row=station.Queue?.LastOrDefault();
                    if(row?.Recipe==null||!TryBindCold(station,row,out original))return true;
                }
                else return MobileColdScanHeld(station);
            }
        }
        var prior=mobileSettlementStation;mobileSettlementStation=station;
        try
        {
            if(original.MobileReturnedImage!=null&&original.MobileTerminal==null)RetryMobileReturnedPublication(original);
            if(original.MobileTerminal!=null)PumpMobileTerminal(original);
            else if(!original.OutputTimerObserved&&!original.MobileAwaitingReturn&&!original.MobileReturnUnknown)PumpMobileEarned(original);
        }
        catch{/* A read/publication refusal keeps this exact original held. */}
        finally{mobileSettlementStation=prior;}
        return retained.ContainsKey(station);
    }
    [ThreadStatic]private static Attempt mobilePaidZero;
    internal static bool IsExecutingMobilePaidZeroCompletion(TileEntityWorkstation station)
        =>ReferenceEquals(mobileSettlementStation,station)&&mobilePaidZero!=null&&ReferenceEquals(mobilePaidZero.Station,station)&&
            retained.TryGetValue(station,out var original)&&ReferenceEquals(original,mobilePaidZero)&&
            !original.OutputTimerObserved&&!original.MobileReturnUnknown&&original.Observation?.Saved()==true&&original.Artifact.MatchesAfter(station);
    private static void PumpMobileEarned(Attempt original)
    {
        if(original.Station is not RebirthCamperStationState station||mobilePaidZero!=null||
            !ReferenceEquals(mobileSettlementStation,station)||original.Artifact?.MatchesAfter(station)!=true)return;
        if(original.Observation==null||!original.Observation.Current())
        {
            original.Observation?.Dispose();original.MobileReturnedPublication=true;
            try{original.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(station,original.Artifact);}
            finally{original.MobileReturnedPublication=false;}
        }
        if(original.Observation?.Saved()!=true)return;
        original.MobileInitialSaved=true;
        // The existing mobile owning window wrapper commits its native grids
        // before work and refreshes the actual output even after an exception.
        using(var window=RebirthCamperStationOpenQueueProcessing.Begin(station))
        {
            if(!original.Artifact.MatchesAfter(station))return;
            if(original.MobileOutputEntered&&!original.MobileNoEffectReturn)return;
            string entered=original.Artifact.OriginalPath+".mobile-output-invoking";
            if(!original.MobileOutputEntered&&(System.IO.File.Exists(entered)||System.IO.File.Exists(entered+".tmp")))return;
            original.MobileOutputEntered=true;original.MobileNoEffectReturn=false;
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(entered,original.Artifact.OriginalDigest()))return;
            mobilePaidZero=original;
            bool returned=false;
            try{station.HandleRecipeQueue(0f);returned=true;}
            finally
            {
                mobilePaidZero=null;
                if(!returned)original.MobileReturnUnknown=true;
            }
            original.MobileNoEffectReturn=!original.OutputTimerObserved&&original.Artifact.MatchesAfter(station);
        }
        if(original.MobileOutputReturned)
        {
            original.MobileReturnedImage=null;
            if(FreezeMobileReturned(original))CaptureReturnedMobileTerminal(original);
        }
    }
}
