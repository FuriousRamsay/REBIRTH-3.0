using System;

// Recovery admission only. Never rerun a player debit or blindly roll back a
// listener failure. Publishing a recovery still requires authenticated authority.
public static class RebirthCamperTransferRecovery
{
    public static bool FinishLocal(RebirthCamperTransferJournal journal,Guid operation,
        string authenticatedCreation,EntityVehicle vehicle,EntityPlayerLocal player,
        XUiC_DragAndDropWindow cursor,Func<bool> currentSession,Func<bool> verifyDurableVehicleSave)
    {
        if(!ThreadManager.IsMainThread() || journal==null || operation==Guid.Empty || cursor==null
            || currentSession==null || verifyDurableVehicleSave==null || !currentSession()
            || vehicle==null || vehicle.world==null || vehicle.world.IsRemote() || player==null || player.IsDead()
            || !ReferenceEquals(vehicle.world,player.world) || !ReferenceEquals(vehicle.world.GetPrimaryPlayer(),player)
            || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)
            || (vehicle.position-player.position).sqrMagnitude>64f || !vehicle.AllowActivationCommand("service",player))return false;
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        journal.AssertAvailable(runtime.Identity,operation);
        var receipt=journal.Read(operation);
        if(receipt==null || receipt.Vehicle!=runtime.Identity || receipt.OwnerCreation!=authenticatedCreation
            || receipt.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
        for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
            if(RebirthCamperStationWindowSession.IsInUse(runtime.Stations.GetSlot(slot)?.State))return false;
        var candidate=RebirthCamperTransferSnapshot.Restore(runtime.Identity,receipt.After);
        var identity=GameManager.Instance.getPersistentPlayerID(null);
        if(identity==null || !(candidate.BackpackSlot<0
            ? RebirthCamperSavedCursor.Verify(identity.CombinedString,operation,candidate.Cursor,currentSession)
            : RebirthCamperSavedCursor.VerifyPublishedReceipt(identity.CombinedString,operation,currentSession)))return false;
        RebirthCamperTransferSnapshot recovered;
        if(!CanFinish(receipt,runtime.Storage,runtime.Stations,vehicle.bag.ItemGrid.items,
            candidate.BackpackSlot<0?cursor.CurrentStack:candidate.Cursor,true,out recovered)
            || !currentSession())return false;
        try
        {
            // Owner inventory is already acknowledged and is never written here.
            recovered.Stations.RetainUnchangedStates(runtime.Stations,false);
            vehicle.bag.SetSlots(recovered.Cargo,true);
            recovered.Stations.RetainUnchangedStates(runtime.Stations);
            runtime.Storage=recovered.Storage;runtime.Stations=recovered.Stations;
            runtime.Persist(true);vehicle.SetBagModified();
            if(!currentSession() || !verifyDurableVehicleSave())return false;
            RebirthCamperStoragePackedTransfer.Applied(RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.Before),recovered);
            journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            runtime.SetTransferPending(false);
            return true;
        }
        catch(Exception error)
        { Log.Error("[CamperTransfer] Recovery remains uncertain "+operation.ToString("N")+": "+error);return false; }
    }

    public static bool CanFinish(RebirthCamperTransferJournal.Record receipt,
        RebirthCamperTransferSnapshot live, bool exactOwnerSaveVerified,
        out RebirthCamperTransferSnapshot recovered)
    {
        return CanFinish(receipt,live?.Storage,live?.Stations,live?.Cargo,live?.Cursor,
            exactOwnerSaveVerified,out recovered);
    }

    public static bool CanFinish(RebirthCamperTransferJournal.Record receipt,
        RebirthCamperStorageInstallations storage,RebirthCamperStationInstallations stations,
        ItemStack[] cargo,ItemStack cursor,bool exactOwnerSaveVerified,out RebirthCamperTransferSnapshot recovered)
    {
        recovered=null;
        if(!ThreadManager.IsMainThread() || receipt==null || storage==null || stations==null || cargo==null || cursor==null || !exactOwnerSaveVerified
            || receipt.State!=RebirthCamperTransferJournal.Phase.Uncertain
            || storage.VehicleIdentity!=receipt.Vehicle || stations.VehicleIdentity!=receipt.Vehicle)
            return false;
        var before=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.Before);
        var after=RebirthCamperTransferSnapshot.Restore(receipt.Vehicle,receipt.After);
        // Once the owner's save has the exact receipt and resulting held item,
        // owner publication is not repeated. Only recognized server components
        // may advance to the journal's candidate.
        if(!Equal(RebirthCamperInventorySnapshot.Capture(new[]{cursor}),
            RebirthCamperInventorySnapshot.Capture(new[]{after.Cursor})))return false;
        if(!Either(storage.Capture(),before.Storage.Capture(),after.Storage.Capture())
            || !Either(stations.Capture(),before.Stations.Capture(),after.Stations.Capture())
            || !Either(RebirthCamperInventorySnapshot.Capture(cargo),
                RebirthCamperInventorySnapshot.Capture(before.Cargo),RebirthCamperInventorySnapshot.Capture(after.Cargo)))return false;
        recovered=after;
        return true;
    }
    private static bool Either(byte[] live,byte[] before,byte[] after)=>Equal(live,before)||Equal(live,after);
    private static bool Equal(byte[] left,byte[] right)
    {if(left.Length!=right.Length)return false;for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false;return true;}
}
