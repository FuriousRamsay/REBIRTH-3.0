using System;

// Authoritative local publication. Remote transport must obtain server-owned
// cursor custody and native-save confirmation before using the same protocol.
public static class RebirthCamperTransferCommit
{
    public static RebirthCamperTransferJournal.Phase Publish(RebirthCamperTransferJournal journal,
        Guid operation, string authenticatedCreation, EntityVehicle vehicle, EntityPlayerLocal player,
        XUiC_DragAndDropWindow cursor, RebirthCamperTransferSnapshot before,
        RebirthCamperTransferSnapshot after, Func<bool> authenticatedSession,
        Func<Guid,ItemStack,bool> verifyDurableNativeSave,
        Func<bool> authenticatedCharacter)
    {
        if (!ThreadManager.IsMainThread() || journal == null || before == null || after == null
            || cursor == null || authenticatedSession == null || authenticatedCharacter == null || verifyDurableNativeSave == null
            || vehicle == null || !RebirthCamperBodyAttachment.HasBody(vehicle) || vehicle.world == null || vehicle.world.IsRemote() || player == null
            || !ReferenceEquals(vehicle.world, player.world) || player.IsDead()
            || (vehicle.position-player.position).sqrMagnitude > 64f
            || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)
            || !RebirthCamperModuleInteraction.Current(vehicle,player)
            || !vehicle.AllowActivationCommand("service",player) || !authenticatedSession())
            throw new InvalidOperationException("Authenticated authoritative transfer required.");
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        if(journal.Read(operation)==null&&RebirthRemoteResourceSourceCustody.BlocksVehicle(vehicle))
            throw new InvalidOperationException("Vehicle cargo belongs to an original resource transfer.");
        journal.AssertAvailable(runtime.Identity,operation);
        if (before.Storage.VehicleIdentity!=runtime.Identity || after.Storage.VehicleIdentity!=runtime.Identity)
            throw new InvalidOperationException("Transfer belongs to another vehicle.");
        // Storage-container transfers leave station installations untouched.
        // Only a station replacement can invalidate a window or grid owner.
        if(before.Stations.Revision!=after.Stations.Revision)
            for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
                if(RebirthCamperStationWindowSession.IsInUse(runtime.Stations.GetSlot(slot)?.State)||
                    RebirthCamperStationGridCustody.IsHeld(runtime.Stations.GetSlot(slot)?.State))
                    throw new InvalidOperationException("Station window still owns an installation.");
        if(before.BackpackSlot!=after.BackpackSlot)throw new InvalidOperationException("Transfer inventory owner changed.");
        byte[] prior=before.Capture();
        byte[] next=after.Capture();
        // Decode once before callbacks: callers cannot mutate the prepared
        // candidate while native inventory listeners execute during publication.
        after=RebirthCamperTransferSnapshot.Restore(runtime.Identity,next);
        var ownerStack=before.BackpackSlot<0?cursor.CurrentStack:player.bag.ItemGrid.items[before.BackpackSlot];
        if(before.BackpackSlot>=0&&!cursor.IsEmpty())throw new InvalidOperationException("Quick transfer requires an empty cursor.");
        byte[] live=RebirthCamperTransferSnapshot.Capture(runtime.Storage,runtime.Stations,vehicle.bag.ItemGrid.items,ownerStack,before.BackpackSlot);
        if(!Equal(prior,live)) throw new InvalidOperationException("Transfer source changed.");
        // Native Resize truncates slot locks, and SetSlots does not remap saved
        // preference positions. Refuse any candidate that would silently move
        // a protected cell or discard a lock/preference during compaction.
        AssertCargoPolicy(vehicle.bag.ItemGrid, before.Cargo, after.Cargo);
        after.Stations.RetainUnchangedStates(runtime.Stations,false);
        RebirthCamperStoragePackedTransfer.Prepare(before,after);
        var receipt=journal.Prepare(operation,runtime.Identity,authenticatedCreation,
            before.Storage.Revision,prior,next);
        if(receipt.State!=RebirthCamperTransferJournal.Phase.Prepared) return receipt.State;
        if(!authenticatedSession()) return receipt.State;
        journal.Advance(operation,RebirthCamperTransferJournal.Phase.Prepared,RebirthCamperTransferJournal.Phase.Uncertain);
        runtime.SetTransferPending(true);
        RebirthCamperTrace.Write("publish.begin",vehicle,operation.ToString("N"));
        try
        {
            // All preparation and exact-source comparisons precede native writes.
            // Listener exceptions may happen after mutation; never retry blindly.
            string playerReceipt="rbCamper_"+operation.ToString("N");
            player.Buffs.SetCustomVar(playerReceipt,2f,true);
            if(after.BackpackSlot<0)cursor.SetCurrentStack(after.Cursor.Clone(),false);
            else player.bag.SetSlot(after.BackpackSlot,after.Cursor.Clone());
            vehicle.bag.SetSlots(Clone(after.Cargo),true);
            after.Stations.RetainUnchangedStates(runtime.Stations);
            runtime.Storage=after.Storage; runtime.Stations=after.Stations;
            runtime.Persist(true);
            vehicle.SetBagModified();
            // 1 means locally published. Only the exact receipt in the primary
            // native player save can acknowledge durable owner publication.
            player.Buffs.SetCustomVar(playerReceipt,1f,true);
            if(!authenticatedSession() || !(after.BackpackSlot<0
                ? verifyDurableNativeSave(operation,after.Cursor.Clone())
                : RebirthCamperSavedCursor.SaveAndVerifyPublishedLocal(player,operation,authenticatedCharacter)))
            {
                RebirthCamperTrace.Write("publish.owner-save-unconfirmed",vehicle,operation.ToString("N"));
                if(authenticatedCharacter())
                    RebirthCamperOwnerSaveRetry.Start(journal,operation,vehicle,player,authenticatedCharacter);
                return RebirthCamperTransferJournal.Phase.Uncertain;
            }
            if(!authenticatedSession())return RebirthCamperTransferJournal.Phase.Uncertain;
            // The native vehicle inventory and module metadata are published.
            // Let VehicleManager save them on its normal single-writer path;
            // waiting for a disk read here delayed every ordinary UI transfer.
            var manager=VehicleManager.Instance;
            if(manager==null)return RebirthCamperTransferJournal.Phase.Uncertain;
            manager.TriggerSave();
            manager.saveTime=0f;
            if(!authenticatedSession())return RebirthCamperTransferJournal.Phase.Uncertain;
            RebirthCamperStoragePackedTransfer.Applied(before,after);
            journal.Advance(operation,RebirthCamperTransferJournal.Phase.Uncertain,RebirthCamperTransferJournal.Phase.Applied);
            runtime.SetTransferPending(false);
            RebirthCamperPreferredRemoval.Confirm(player,vehicle,before,after);
            return RebirthCamperTransferJournal.Phase.Applied;
        }
        catch(Exception error)
        {
            Log.Error("[CamperTransfer] Reconcile receipt "+operation.ToString("N")+": "+error);
            return RebirthCamperTransferJournal.Phase.Uncertain;
        }
    }
    private static ItemStack[] Clone(ItemStack[] source)
    { var result=new ItemStack[source.Length];for(int i=0;i<source.Length;i++)result[i]=source[i].Clone();return result; }
    private static bool Equal(byte[] left,byte[] right)
    { if(left.Length!=right.Length)return false;for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false;return true; }
    internal static void AssertCargoPolicy(ItemStackGrid grid, ItemStack[] before, ItemStack[] after)
    {
        if (grid == null || before == null || after == null || grid.Length != before.Length)
            throw new InvalidOperationException("Native cargo policy is unavailable.");
        var locks = grid.SlotLocks;
        if (locks != null)
        {
            if (locks.Length != before.Length)
                throw new InvalidOperationException("Cargo slot locks do not match the native inventory.");
            for (int slot = 0; slot < locks.Length; slot++)
                if (locks[slot] && (slot >= after.Length || !Equal(
                    RebirthCamperInventorySnapshot.Capture(new[] { before[slot] }),
                    RebirthCamperInventorySnapshot.Capture(new[] { after[slot] }))))
                    throw new InvalidOperationException("Cargo slot "+(slot+1)+" is locked and would move or leave the retained capacity. Unlock that affected slot before changing storage.");
        }
        // Native AnyPreferences means any non-null array, including an entirely
        // empty saved bag image and unrelated equipment/toolbelt preferences.
        // Resize keeps this tracker unchanged. Protect actual cargo positions,
        // not the mere presence of a tracker or another owner's preference area.
        var preferences=grid.Preferences?.bag;
        if(before.Length!=after.Length&&preferences!=null)
            for(int slot=0;slot<preferences.Length;slot++)
            {
                var preferred=preferences[slot];
                if(preferred==null)throw new InvalidOperationException("Cargo position preference is malformed.");
                if(preferred.IsEmpty())continue;
                if(slot>=before.Length||slot>=after.Length||!Equal(
                    RebirthCamperInventorySnapshot.Capture(new[]{before[slot]}),
                    RebirthCamperInventorySnapshot.Capture(new[]{after[slot]})))
                    throw new InvalidOperationException("Cargo slot "+(slot+1)+" has an item preference and would move or leave the retained capacity. Clear that affected preference before changing storage.");
            }
    }
}
