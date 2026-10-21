using System;

internal static partial class RebirthHydrationOwnerHold
{
    internal static bool RetireHistoricalRemote(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        if(!RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,original.Operation,original.Digest,out _)||
            !RebirthHydrationTerminalSeenCheckpoint.HasSavedHistorical(player,original)||
            !RebirthHydrationRemoteClientGrant.RetireHistorical(player,original))return false;
        var remove=new System.Collections.Generic.List<EntityPlayerLocal>();
        foreach(var pair in entries)
            if(pair.Value.Original.Digest==original.Digest)
            {if(Current(pair.Value))return false;remove.Add(pair.Key);}
        if(!RebirthHydrationRemoteClientTerminal.TryGetAcknowledgment(player,original.Operation,original.Digest,out _))return false;
        foreach(var key in remove)entries.Remove(key);
        reenteredRemoteEntries.RemoveAll(entry=>entry.Original.Digest==original.Digest&&!Current(entry));
        return true;
    }
    // Purpose-specific reentry of the exact durable client original. This
    // freezes actual native inventory only; the authenticated host still issues
    // any financial grant. Old native objects remain held until joint terminal.
    internal static bool TryReenterRemote(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        try
        {
            if(player?.world==null||original==null||!Scope(player,original))return false;
            RebirthHydrationTransferOriginal saved;
            if(player.world.IsRemote())
            {if(!RebirthHydrationClientOriginals.TryRead(player,out saved)||saved?.Digest!=original.Digest)return false;}
            else if(!RebirthHydrationTransferJournal.TryReadPending(player,out saved,out _)||saved?.Digest!=original.Digest)return false;
            entries.TryGetValue(player,out var existing);
            if(existing!=null&&Current(existing))return existing.Original.Digest==original.Digest&&QualifyReenteredOwner(player,original);
            if(existing!=null&&existing.Original.Digest!=original.Digest)return false;
            if(entries.Count+reenteredRemoteEntries.Count>=64||
                existing==null&&!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player)||
                RebirthDogLifecycleService.HasHireInventoryHold(player)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(player)||
                player.inventory==null||player.inventory.IsHoldingItemActionRunning()||player.PlayerUI?.xui==null||player.PlayerUI.xui.IsUsingItemActionEntryUse||
                player.PlayerUI.xui.DragAndDropWindow?.CurrentStack==null||!player.PlayerUI.xui.DragAndDropWindow.CurrentStack.IsEmpty()||
                !original.TryGetPlan(out var plan)||!Capture(player,original,out var live))return false;
            foreach(var retained in entries.Values)
                if(retained.Original.Owner==original.Owner&&retained.Original.World==original.World&&
                    (retained.Original.Digest!=original.Digest||Current(retained)))return false;
            float gear=player.Buffs?.GetCustomVar("rbGear_"+original.Operation.ToString("N"))??0f;
            float marker=player.Buffs?.GetCustomVar(RebirthHydrationOwnerCheckpoint.Marker(original))??0f;
            if(gear!=marker||(gear!=0f&&gear!=1f&&gear!=2f)||player.Buffs.GetCustomVar("rbHydrationRefused_"+original.Operation.ToString("N")+"_"+original.Digest)!=0f||
                (gear==1f?!plan.MatchesAppliedInventory(live.Bag,live.Belt):
                    plan.InspectApplication(live.Bag,live.Belt,true)==RebirthGearInventoryPlan.ApplicationState.Conflict))return false;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var next=new Entry{Player=player,World=player.world,Game=GameManager.Instance,Manager=manager,Peer=player.world.IsRemote()?manager.connectionToServer[0]:null,
                Bag=player.bag,Belt=player.inventory,Original=original,Save=DirectoryPath(GameIO.GetSaveGameDir()),PlayerData=DirectoryPath(GameIO.GetPlayerDataDir())};
            if(!Current(next))return false;
            if(existing!=null)reenteredRemoteEntries.Add(existing);
            entries[player]=next; // Retain before the independent native read.
            return QualifyReenteredOwner(player,original)&&Current(next);
        }
        catch{return false;}
    }
    private static bool QualifyReenteredOwner(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        if(!Matches(player,original))return false;
        float stage=player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"));
        if(stage==0f)return RebirthHydrationOwnerCheckpoint.HasSavedUntouchedBefore(player,original)&&
            RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying);
        if(stage==1f)return RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied);
        if(stage!=2f)return false;
        if(RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original))return true;
        // A positively saved native After under the original Applying receipt
        // needs only a receipt checkpoint, never a second item setter.
        return RebirthHydrationOwnerCheckpoint.HasSavedApplyingAfter(player,original)&&
            RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applied);
    }
}
