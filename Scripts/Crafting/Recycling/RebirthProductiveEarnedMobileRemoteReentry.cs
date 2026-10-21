using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedCoordinator
{
    // The actual vehicle station tick calls this before its global transfer
    // hold. Only the exact already-paid native tool transfer can progress here;
    // other camper transfers retain their original admission and owning caller.
    internal static void ObserveMobileVehicleTick(EntityVehicle vehicle,RebirthCamperModuleRuntime runtime)
    {
        if(!Enabled||!ThreadManager.IsMainThread()||vehicle?.world?.IsRemote()!=false||
            !ReferenceEquals(vehicle.world,GameManager.Instance?.World)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle))return;
        ObserveWorld();
        if(!runtime.TransferPending)return;
        for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
        {
            var station=runtime.Stations.GetSlot(slot)?.State;
            if(station==null||!ReferenceEquals(station.BoundVehicle,vehicle))continue;
            if(!retained.ContainsKey(station)&&TryNativeMarker(station,out _))TryRestoreMobileTerminal(station);
            if(!retained.TryGetValue(station,out var original)||original.MobileTerminal==null)continue;
            var prior=mobileSettlementStation;mobileSettlementStation=station;
            try
            {
                if(original.MobileTerminal.PendingRemoteTools!=null)PumpMobileTerminal(original);
                else FinishLoadedMobileToolReceipt(original,runtime);
            }
            catch{/* Exact native original remains held. No owner mutation occurs here. */}
            finally{mobileSettlementStation=prior;}
        }
    }
    private static bool PumpMobileRemoteReentry(Attempt original)
    {
        var terminal=original.MobileTerminal;var source=terminal?.PendingRemoteTools;
        if(source?.Reentry!=true||!IsMobileSettlementSource(original.Station))return false;
        var runtime=RebirthCamperModuleRuntime.For(terminal.Station.BoundVehicle);
        var journal=new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers"));
        var receipt=journal.Read(source.Operation);
        if(receipt?.State==RebirthCamperTransferJournal.Phase.Applied)return RetryMobileRemoteTools(original);
        if(receipt?.State!=RebirthCamperTransferJournal.Phase.Uncertain||!MatchesMobileRemoteToolReceipt(source,receipt,runtime.Identity)||
            !MobileRemoteOwnerCurrent(source)||!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(source.Path,source.Record,
                RebirthProductiveEarnedMobileOrigin.MaximumBytes))return false;
        if(!source.Entered)
        {
            if(!terminal.Current()||!RebirthProductiveEarnedMobileOrigin.Canonical(terminal.Station,terminal.Installed).SequenceEqual(source.Before))return false;
            var after=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
            if(!RebirthCamperTransferRecovery.CanFinish(receipt,runtime.Storage,runtime.Stations,
                terminal.Station.BoundVehicle.bag.ItemGrid.items,after.Cursor,true,out _))return false;
            byte[] grant=RebirthProductiveEarnedMobileOrigin.Digest(source.Record);
            if(!RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.Path+".reentry",grant)||
                !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.Path+".reentry.invoking",grant)||!terminal.Current())return false;
            source.Entered=true;
            // Original owner debit is already native-saved and immutable. The
            // existing server-owned transfer owes only this exact tools sink.
            var candidate=RebirthCamperStationSnapshot.Restore(original.Artifact.Birth,terminal.Installed,source.After);
            terminal.Station.Tools=ItemStack.Clone(candidate.Tools);terminal.Station.setModified();
        }
        Func<bool> current=()=>original.Artifact.Current()&&original.Artifact.MatchesMarker(terminal.Station.CopyProductiveEarnedMarker())&&
            RebirthProductiveEarnedMobileOrigin.Canonical(terminal.Station,terminal.Installed).SequenceEqual(source.After)&&MobileRemoteOwnerCurrent(source);
        if(!current())return false;
        // A failed native window refresh must retry before saving, even when
        // the exact original tools setter already returned on a prior tick.
        // Otherwise that stale window could later overwrite the paid sink.
        if(!source.ProjectionReady)
        {
            if(!RebirthCamperStationOpenQueueProcessing.RefreshAfterOriginal(terminal.Station)||!current())return false;
            source.ProjectionReady=true;
        }
        if(source.Save==null&&source.Saved==null)
            source.Save=RebirthCamperStationSaveEvidence.BeginProcessingRecovery(terminal.Station,
                new RebirthCamperStationAddress(runtime.Identity,terminal.Station.InstallationId),current,terminal.Memory);
        if(source.Saved==null)
        {if(!RebirthCamperStationSaveEvidence.TryObserve(source.Save,out var saved))return false;source.Saved=saved;}
        using(var callbacks=terminal.Memory.TryEnterCallbacks())
            if(callbacks==null||!source.Saved.ReadStationSnapshot(state=>
                original.Artifact.MatchesMarker(state.CopyProductiveEarnedMarker())&&
                RebirthProductiveEarnedMobileOrigin.Canonical(state,terminal.Installed).SequenceEqual(source.After))||!current()||
                !RebirthProductiveEarnedNativeSuccessorRecord.WriteNew(source.Path+".saved",RebirthProductiveEarnedMobileOrigin.Digest(source.Record)))return false;
        source.Save?.Dispose();source.Saved?.Dispose();source.Save=null;source.Saved=null;
        var applied=journal.Advance(source.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
        // Actual journal publication normally performs the same metadata
        // adoption synchronously; retry the positive terminal if it did not.
        if(terminal.PendingRemoteTools!=null&&!TryAdoptMobileRemoteTools(original,source,applied))return false;
        journal.AssertAvailable(runtime.Identity,source.Operation);
        runtime.SetTransferPending(false);runtime.Persist(true,12);return true;
    }
    private static bool FinishLoadedMobileToolReceipt(Attempt original,RebirthCamperModuleRuntime runtime)
    {
        var terminal=original.MobileTerminal;
        if(terminal==null||!EnsureMobileTerminalObservation(terminal)||!terminal.Current())return false;
        foreach(var source in terminal.RemoteTools)
        {
            var journal=new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CamperTransfers"));
            var receipt=journal.Read(source.Operation);
            if(receipt?.State!=RebirthCamperTransferJournal.Phase.Uncertain)continue;
            if(!source.Current()||!MatchesMobileRemoteToolReceipt(source,receipt,runtime.Identity))return false;
            var after=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
            if(!RebirthCamperTransferRecovery.CanFinish(receipt,runtime.Storage,runtime.Stations,
                terminal.Station.BoundVehicle.bag.ItemGrid.items,after.Cursor,true,out _))return false;
            // This source was attached only from the genuine loaded full After
            // plus copied actual saved owner original. Close metadata only.
            journal.Advance(source.Operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            journal.AssertAvailable(runtime.Identity,source.Operation);
            runtime.SetTransferPending(false);runtime.Persist(true,12);return true;
        }
        return false;
    }
}
