using System;

public enum RebirthBackpackLibraryViewStatus { Unavailable, NoBackpack, Ready }

// Display reads are owner scoped. Transfer dispatch requires the custody reservation handshake.
public static class RebirthBackpackLibraryServer
{
    private static bool Resolve(EntityPlayer player,ClientInfo sender,string creation,out RebirthWorldCharacterRecord record)
    {
        record=null;
        return player!=null&&player.world!=null&&GameManager.Instance!=null&&
            ReferenceEquals(player.world,GameManager.Instance.World)&&
            ReferenceEquals(player.world.GetEntity(player.entityId),player)&&
            sender?.InternalId!=null&&sender.entityId==player.entityId&&
            RebirthWorldCharacterRepository.IsServerAuthority&&RebirthSurvivorMode.IsEnabledForCurrentWorld()&&
            RebirthWorldCharacterService.TryGet(player,out record)&&record?.Support!=null&&record.Origin!=null&&record.IsComplete&&
            RebirthSurvivorRequestScope.Matches(creation,record.Origin.CreationId);
    }
    private static bool ResolveLocal(EntityPlayerLocal player,string creation,out RebirthWorldCharacterRecord record)
    {
        record=null;
        return player!=null&&player.world!=null&&!player.world.IsRemote()&&GameManager.Instance!=null&&
            ReferenceEquals(player.world,GameManager.Instance.World)&&ReferenceEquals(player.world.GetPrimaryPlayer(),player)&&
            ReferenceEquals(player.world.GetEntity(player.entityId),player)&&RebirthWorldCharacterRepository.IsServerAuthority&&
            RebirthSurvivorMode.IsEnabledForCurrentWorld()&&RebirthWorldCharacterService.TryGet(player,out record)&&
            record?.Support!=null&&record.Origin!=null&&record.IsComplete&&
            RebirthSurvivorRequestScope.Matches(creation,record.Origin.CreationId);
    }
    private static bool Save(EntityPlayer player,RebirthWorldCharacterRecord record)
    {
        if(!RebirthWorldCharacterService.TryGetIdentity(player,out var identity))return false;
        RebirthWorldCharacterService.MarkDirty(record,"backpack-library-transfer");
        return RebirthWorldCharacterRepository.SaveIfDirty(identity,"backpack-library-transfer");
    }
    // Read-only owner projection; no journal save, debit, reservation or revision change.
    public static bool GetView(EntityPlayer player,ClientInfo sender,string creation,out RebirthBackpackLibraryView view)
    {return GetViewStatus(player,sender,creation,out view,out _)==RebirthBackpackLibraryViewStatus.Ready;}
    public static RebirthBackpackLibraryViewStatus GetViewStatus(EntityPlayer player,ClientInfo sender,string creation,
        out RebirthBackpackLibraryView view,out long revision)
    {
        view=null;revision=-1;
        if(!Resolve(player,sender,creation,out var record)||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player))return RebirthBackpackLibraryViewStatus.Unavailable;
        return ProjectView(record,out view,out revision);
    }
    public static RebirthBackpackLibraryViewStatus GetLocalViewStatus(EntityPlayerLocal player,string creation,
        out RebirthBackpackLibraryView view,out long revision)
    {
        view=null;revision=-1;
        if(!ResolveLocal(player,creation,out var record)||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player))return RebirthBackpackLibraryViewStatus.Unavailable;
        return ProjectView(record,out view,out revision);
    }
    private static RebirthBackpackLibraryViewStatus ProjectView(RebirthWorldCharacterRecord record,
        out RebirthBackpackLibraryView view,out long revision)
    {
        view=null;revision=-1;
        var state=record.Support;
        if(state.GearRevision<0)return RebirthBackpackLibraryViewStatus.Unavailable;
        state.EquippedGearBySlot.TryGetValue("backpack",out var item);
        state.EquippedGearItemDataBySlot.TryGetValue("backpack",out var data);
        if(string.IsNullOrEmpty(item)&&string.IsNullOrEmpty(data)&&state.PendingLibraryTransfer==null)
        {revision=state.GearRevision;return RebirthBackpackLibraryViewStatus.NoBackpack;}
        if(string.IsNullOrEmpty(item)||!RebirthNativeItemCodec.TryDecode(data,out var backpack)||
            !string.Equals(item,backpack.ItemClass.GetItemName(),StringComparison.Ordinal))return RebirthBackpackLibraryViewStatus.Unavailable;
        bool pending=state.PendingLibraryTransfer!=null||state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null;
        if(!RebirthBackpackLibraryView.TryCreate(record.Origin.CreationId,state.GearRevision,backpack,pending,out view))return RebirthBackpackLibraryViewStatus.Unavailable;
        revision=state.GearRevision;return RebirthBackpackLibraryViewStatus.Ready;
    }
    public static RebirthBackpackLibraryViewStatus GetSellStashViewStatus(EntityPlayer player,ClientInfo sender,string creation,
        out RebirthBackpackSellStashView view,out long revision)
    {
        view=null;revision=-1;
        if(!Resolve(player,sender,creation,out var record)||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player))return RebirthBackpackLibraryViewStatus.Unavailable;
        return ProjectSellStashView(record,out view,out revision);
    }
    public static RebirthBackpackLibraryViewStatus GetLocalSellStashViewStatus(EntityPlayerLocal player,string creation,
        out RebirthBackpackSellStashView view,out long revision)
    {
        view=null;revision=-1;
        if(!ResolveLocal(player,creation,out var record)||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player))return RebirthBackpackLibraryViewStatus.Unavailable;
        return ProjectSellStashView(record,out view,out revision);
    }
    private static RebirthBackpackLibraryViewStatus ProjectSellStashView(RebirthWorldCharacterRecord record,
        out RebirthBackpackSellStashView view,out long revision)
    {
        view=null;revision=-1;
        var state=record.Support;
        if(state.GearRevision<0)return RebirthBackpackLibraryViewStatus.Unavailable;
        state.EquippedGearBySlot.TryGetValue("backpack",out var item);
        state.EquippedGearItemDataBySlot.TryGetValue("backpack",out var data);
        if(string.IsNullOrEmpty(item)&&string.IsNullOrEmpty(data)&&state.PendingLibraryTransfer==null)
        {revision=state.GearRevision;return RebirthBackpackLibraryViewStatus.NoBackpack;}
        if(string.IsNullOrEmpty(item)||!RebirthNativeItemCodec.TryDecode(data,out var backpack)||
            !string.Equals(item,backpack.ItemClass.GetItemName(),StringComparison.Ordinal))return RebirthBackpackLibraryViewStatus.Unavailable;
        bool pending=state.PendingLibraryTransfer!=null||state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null;
        if(!RebirthBackpackSellStashView.TryCreate(record.Origin.CreationId,state.GearRevision,backpack,pending,out view))return RebirthBackpackLibraryViewStatus.Unavailable;
        revision=state.GearRevision;return RebirthBackpackLibraryViewStatus.Ready;
    }
    public static bool Prepare(EntityPlayer player,ClientInfo sender,string creation,long revision,bool bag,
        int inventorySlot,int librarySlot,int quantity,bool deposit,out RebirthBackpackLibraryReceipt offer)
    {
        offer=null;
        if(!Resolve(player,sender,creation,out var record)||player.IsDead()||
            RebirthCharacterCreationHoldService.IsHeld(player))return false;
        var slots=bag?RebirthPlayerDataInventory.ReadSlots(sender.latestPlayerData,true):RebirthPlayerDataInventory.ReadSlots(sender.latestPlayerData,false);
        return PrepareRecord(player,record,creation,revision,bag,inventorySlot,librarySlot,quantity,deposit,slots,out offer);
    }
    public static bool PrepareLocal(EntityPlayerLocal player,string creation,long revision,bool bag,
        int inventorySlot,int librarySlot,int quantity,bool deposit,out RebirthBackpackLibraryReceipt offer)
    {
        offer=null;
        if(!ResolveLocal(player,creation,out var record)||player.IsDead()||!player.IsSpawned()||
            RebirthCharacterCreationHoldService.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player))return false;
        var xui=player.PlayerUI?.xui;
        if(player.inventory==null||player.inventory.IsHoldingItemActionRunning()||xui==null||xui.IsUsingItemActionEntryUse||
            xui.DragAndDropWindow?.CurrentStack==null||!xui.DragAndDropWindow.CurrentStack.IsEmpty())return false;
        var slots=bag?player.bag?.ItemGrid.items:player.inventory?.ItemGrid.items;
        return PrepareRecord(player,record,creation,revision,bag,inventorySlot,librarySlot,quantity,deposit,slots,out offer);
    }
    public static bool PrepareSellStash(EntityPlayer player,ClientInfo sender,string creation,long revision,bool bag,
        int inventorySlot,int librarySlot,int quantity,bool deposit,out RebirthBackpackLibraryReceipt offer)
    {
        offer=null;
        if(!Resolve(player,sender,creation,out var record)||player.IsDead()||
            RebirthCharacterCreationHoldService.IsHeld(player))return false;
        var slots=bag?RebirthPlayerDataInventory.ReadSlots(sender.latestPlayerData,true):RebirthPlayerDataInventory.ReadSlots(sender.latestPlayerData,false);
        return PrepareRecord(player,record,creation,revision,bag,inventorySlot,librarySlot,quantity,deposit,slots,out offer,true);
    }
    public static bool PrepareLocalSellStash(EntityPlayerLocal player,string creation,long revision,bool bag,
        int inventorySlot,int librarySlot,int quantity,bool deposit,out RebirthBackpackLibraryReceipt offer)
    {
        offer=null;
        if(!ResolveLocal(player,creation,out var record)||player.IsDead()||!player.IsSpawned()||
            RebirthCharacterCreationHoldService.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player))return false;
        var xui=player.PlayerUI?.xui;
        if(player.inventory==null||player.inventory.IsHoldingItemActionRunning()||xui==null||xui.IsUsingItemActionEntryUse||
            xui.DragAndDropWindow?.CurrentStack==null||!xui.DragAndDropWindow.CurrentStack.IsEmpty())return false;
        var slots=bag?player.bag?.ItemGrid.items:player.inventory?.ItemGrid.items;
        return PrepareRecord(player,record,creation,revision,bag,inventorySlot,librarySlot,quantity,deposit,slots,out offer,true);
    }
    public static bool PrepareCursor(EntityPlayer player,ClientInfo sender,string creation,long revision,
        int sectionSlot,int quantity,bool deposit,bool sell,out RebirthBackpackLibraryReceipt offer)
    {
        offer=null;
        if(!Resolve(player,sender,creation,out var record)||player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player))return false;
        var cursor=sender.latestPlayerData?.dragAndDropItem;
        if(cursor==null)return false;
        return PrepareRecord(player,record,creation,revision,false,0,sectionSlot,quantity,deposit,new[]{cursor},out offer,sell,true);
    }
    public static bool PrepareLocalCursor(EntityPlayerLocal player,string creation,long revision,
        int sectionSlot,int quantity,bool deposit,bool sell,out RebirthBackpackLibraryReceipt offer)
    {
        offer=null;
        if(!ResolveLocal(player,creation,out var record)||player.IsDead()||!player.IsSpawned()||
            RebirthCharacterCreationHoldService.IsHeld(player)||RebirthBackpackLibraryReservation.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player))return false;
        var ui=player.PlayerUI?.xui;
        if(player.inventory==null||player.inventory.IsHoldingItemActionRunning()||ui==null||ui.IsUsingItemActionEntryUse)return false;
        var cursor=ui.DragAndDropWindow?.CurrentStack;
        if(cursor==null)return false;
        return PrepareRecord(player,record,creation,revision,false,0,sectionSlot,quantity,deposit,new[]{cursor},out offer,sell,true);
    }
    private static bool PrepareRecord(EntityPlayer player,RebirthWorldCharacterRecord record,string creation,long revision,bool bag,
        int inventorySlot,int librarySlot,int quantity,bool deposit,ItemStack[] slots,out RebirthBackpackLibraryReceipt offer,bool sell=false,bool cursor=false)
    {
        offer=null;
        var state=record.Support;
        // Recovery is explicit; another UI request never changes an existing intent.
        if(state.PendingLibraryTransfer!=null||state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null||
            state.GearRevision!=revision||revision==long.MaxValue)return false;
        if(!state.EquippedGearBySlot.TryGetValue("backpack",out var item)||
            !state.EquippedGearItemDataBySlot.TryGetValue("backpack",out var data)||
            !RebirthNativeItemCodec.TryDecode(data,out var backpack)||
            !string.Equals(item,backpack.ItemClass.GetItemName(),StringComparison.Ordinal))return false;
        if(slots==null)return false;
        int owned=cursor?1:bag?slots.Length:RebirthToolbeltCapacity.GetOwnedSlotCount(player,slots.Length);
        if(inventorySlot<0||inventorySlot>=owned||slots[inventorySlot]==null)return false;
        RebirthBackpackLibraryReceipt receipt;
        bool planned=cursor?RebirthBackpackLibraryReceipt.TryCreateCursor(Guid.NewGuid(),record.Origin.CreationId,revision,
            librarySlot,quantity,deposit,backpack,slots[inventorySlot],sell,out receipt) :sell?RebirthBackpackLibraryReceipt.TryCreateSellStash(Guid.NewGuid(),record.Origin.CreationId,revision,
            bag,inventorySlot,librarySlot,quantity,deposit,backpack,slots[inventorySlot],out receipt)
            :RebirthBackpackLibraryReceipt.TryCreate(Guid.NewGuid(),record.Origin.CreationId,revision,
            bag,inventorySlot,librarySlot,quantity,deposit,backpack,slots[inventorySlot],out receipt);
        if(!planned||
            !RebirthBackpackLibraryJournal.Prepare(state,receipt,creation,()=>Save(player,record)))return false;
        offer=receipt;return true;
    }
    public static bool GetRecoveryOffer(EntityPlayer player,ClientInfo sender,string creation,
        out RebirthBackpackLibraryReceipt offer)
    {
        offer=null;
        if(!Resolve(player,sender,creation,out var record)||player.IsDead()||
            RebirthCharacterCreationHoldService.IsHeld(player)||
            !RebirthBackpackLibraryJournal.TryGetPending(record.Support,creation,out var pending)||
            !Save(player,record))return false;
        offer=pending;return true;
    }
    // Explicit owner action only. Normal native player saving supplies evidence;
    // an in-memory applied receipt never releases the reservation by itself.
    public static bool ApplyLocalPending(EntityPlayerLocal player,string creation,out RebirthBackpackLibraryOwnerResult result)
    {
        result=RebirthBackpackLibraryOwnerResult.Pending;
        if(!ResolveLocal(player,creation,out var record)||
            !RebirthBackpackLibraryJournal.TryGetPending(record.Support,creation,out var receipt)||
            !RebirthBackpackLibraryReservation.TryAcquire(player,creation,receipt))return false;
        var id=creation;
        result=RebirthBackpackLibraryOwnerTransfer.Apply(player,id,receipt);
        if(result==RebirthBackpackLibraryOwnerResult.Indeterminate&&
            RebirthBackpackLibraryOwnerTransfer.RecoverApplied(player,id,receipt))result=RebirthBackpackLibraryOwnerResult.Applied;
        if(result!=RebirthBackpackLibraryOwnerResult.Applied&&result!=RebirthBackpackLibraryOwnerResult.Rejected)return false;
        bool applied=result==RebirthBackpackLibraryOwnerResult.Applied;
        if(!AdvanceLocal(player,creation,receipt.TransactionId,applied))return false;
        return RebirthBackpackLibraryReservation.ReleaseSettled(player,id,receipt,r=>
            TryGetLocalSettlement(player,creation,r.TransactionId,out var terminal)&&terminal.Applied==applied&&
            terminal.GearRevision==r.ExpectedGearRevision+1);
    }
    // Explicit host interaction step. Native save success is confirmed by fresh readback,
    // never by the void SaveLocalPlayerData return or the in-memory receipt alone.
    public static bool AdvanceLocalInteraction(EntityPlayerLocal player,string creation,out RebirthBackpackLibraryOwnerResult result)
    {
        if(ApplyLocalPending(player,creation,out result))return true;
        if(result!=RebirthBackpackLibraryOwnerResult.Applied&&result!=RebirthBackpackLibraryOwnerResult.Rejected)return false;
        if(!ResolveLocal(player,creation,out _))return false;
        try{GameManager.Instance.SaveLocalPlayerData();}
        catch(Exception error){Log.Warning("[REBIRTH Library] Host player checkpoint deferred: "+error.GetType().Name);return false;}
        return ApplyLocalPending(player,creation,out result);
    }
    public static bool GetLocalRecoveryOffer(EntityPlayerLocal player,string creation,out RebirthBackpackLibraryReceipt offer)
    {
        offer=null;
        if(!ResolveLocal(player,creation,out var record)||player.IsDead()||!player.IsSpawned()||
            RebirthCharacterCreationHoldService.IsHeld(player)||
            !RebirthBackpackLibraryJournal.TryGetPending(record.Support,creation,out var pending)||
            !Save(player,record))return false;
        offer=pending;return true;
    }
    public static bool TryGetSettlement(EntityPlayer player,ClientInfo sender,string creation,string transaction,
        out RebirthBackpackLibrarySettlement settlement)
    {
        settlement=null;
        if(!Resolve(player,sender,creation,out var record)||!Guid.TryParse(transaction,out var requested)||requested==Guid.Empty)return false;
        var saved=record.Support.LastLibrarySettlement;
        if(saved==null||!RebirthSurvivorRequestScope.Matches(creation,saved.CreationId)||saved.TransactionId!=requested.ToString("N")||
            saved.GearRevision>record.Support.GearRevision||record.Support.PendingLibraryTransfer?.TransactionId==saved.TransactionId)return false;
        settlement=saved;return true; // Outcome was saved atomically when the pending journal was cleared.
    }
    public static bool TryGetLocalSettlement(EntityPlayerLocal player,string creation,string transaction,out RebirthBackpackLibrarySettlement settlement)
    {
        settlement=null;
        if(!ResolveLocal(player,creation,out var record)||!Guid.TryParse(transaction,out var requested)||requested==Guid.Empty)return false;
        var saved=record.Support.LastLibrarySettlement;
        if(saved==null||!RebirthSurvivorRequestScope.Matches(creation,saved.CreationId)||saved.TransactionId!=requested.ToString("N")||
            saved.GearRevision>record.Support.GearRevision||record.Support.PendingLibraryTransfer?.TransactionId==saved.TransactionId)return false;
        settlement=saved;return true;
    }
    public static bool AdvanceLocal(EntityPlayerLocal player,string creation,string transaction,bool applied)
    {
        if(!ResolveLocal(player,creation,out var record))return false;
        return AdvanceRecord(player,record,creation,transaction,applied,r=>RebirthBackpackLibrarySavedEvidence.TryVerifyLocalFromDisk(player,r,applied));
    }
    public static bool Advance(EntityPlayer player,ClientInfo sender,string creation,string transaction,bool applied)
    {
        if(!Resolve(player,sender,creation,out var record))return false;
        return AdvanceRecord(player,record,creation,transaction,applied,r=>RebirthBackpackLibrarySavedEvidence.TryVerifyFromDisk(sender,r,applied));
    }
    private static bool AdvanceRecord(EntityPlayer player,RebirthWorldCharacterRecord record,string creation,string transaction,bool applied,
        Func<RebirthBackpackLibraryReceipt,bool> evidence)
    {
        var state=record.Support;var receipt=state.PendingLibraryTransfer;
        if(receipt==null||receipt.TransactionId!=transaction||!RebirthSurvivorRequestScope.Matches(creation,receipt.CreationId))return false;
        Func<bool> save=()=>Save(player,record);
        if(!applied)return RebirthBackpackLibraryJournal.CancelRejected(state,transaction,creation,evidence,save);
        if(state.LibraryTransferPhase!=RebirthBackpackLibraryPhase.BackpackCommitted&&
            !RebirthBackpackLibraryJournal.MarkOwnerApplied(state,transaction,creation,evidence,save))return false;
        if(!RebirthBackpackLibraryJournal.CommitBackpack(state,transaction,creation,save))return false;
        return RebirthBackpackLibraryJournal.Finish(state,transaction,creation,evidence,save);
    }
}
