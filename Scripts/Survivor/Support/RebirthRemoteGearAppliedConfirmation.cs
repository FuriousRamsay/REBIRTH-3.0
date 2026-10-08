using System;

// Server journal acknowledgement only. A packet claiming "applied" is not custody
// evidence; exact native final-file inventory and receipt must independently agree.
public static class RebirthRemoteGearAppliedConfirmation
{
    public static bool TryConfirm(EntityPlayer player, ClientInfo sender, string creation, string transactionId)
    {
        if (!RebirthRemoteGearInventorySource.TryResolve(player, sender, creation, out var record) ||
            !RebirthWorldCharacterService.TryGetIdentity(player, out var identity) ||
            identity == null || record.StablePlayerKey != identity.StorageKey) return false;
        string key = identity.StorageKey;
        var pending = record.Support.PendingGearTransfer;
        var phase = record.Support.GearTransferPhase;
        if (pending == null || pending.TransactionId != transactionId ||
            (phase != RebirthGearTransferPhase.Prepared && phase != RebirthGearTransferPhase.OwnerApplied) ||
            !Current(player, sender, creation, record, key, pending, phase) ||
            !RebirthWorldCharacterRepository.HasSavedGearTransfer(identity, pending, phase) ||
            !RebirthGearPlayerFileWitness.HasApplied(identity, pending) ||
            !Current(player, sender, creation, record, key, pending, phase)) return false;
        return RebirthGearTransferJournal.MarkOwnerApplied(record.Support, transactionId, creation, () =>
        {
            if (!Current(player, sender, creation, record, key, pending, RebirthGearTransferPhase.OwnerApplied)) return false;
            RebirthWorldCharacterService.MarkDirty(record, "remote-gear-owner-applied");
            try { RebirthWorldCharacterRepository.SaveIfDirty(identity, "remote-gear-owner-applied"); }
            catch { /* Resolve an uncertain write through the exact final file. */ }
            return RebirthWorldCharacterRepository.HasSavedGearTransfer(identity, pending,
                RebirthGearTransferPhase.OwnerApplied) &&
                Current(player, sender, creation, record, key, pending, RebirthGearTransferPhase.OwnerApplied);
        });
    }

    // Commit the persisted exact ItemValue only after owner custody is established.
    // Pending recovery remains attached; this never creates drops or clears intent.
    public static bool TryCommit(EntityPlayer player, ClientInfo sender, string creation, string transactionId)
    {
        if (!RebirthRemoteGearInventorySource.TryResolve(player, sender, creation, out var record) ||
            !RebirthWorldCharacterService.TryGetIdentity(player, out var identity) ||
            identity == null || record.StablePlayerKey != identity.StorageKey) return false;
        string key = identity.StorageKey;
        var pending = record.Support.PendingGearTransfer;
        var phase = record.Support.GearTransferPhase;
        if (pending == null || pending.TransactionId != transactionId ||
            (phase != RebirthGearTransferPhase.OwnerApplied && phase != RebirthGearTransferPhase.GearCommitted) ||
            !Current(player, sender, creation, record, key, pending, phase) ||
            !RebirthWorldCharacterRepository.HasSavedGearTransfer(identity, pending, phase)) return false;
        // A committed retry already has a durable checkpoint; it must not demand
        // the old inventory layout after projection/recovery subsequently changes it.
        if (phase == RebirthGearTransferPhase.GearCommitted) return true;
        if (!RebirthGearPlayerFileWitness.HasApplied(identity, pending) ||
            !Current(player, sender, creation, record, key, pending, phase)) return false;
        return RebirthGearTransferJournal.CommitGear(record.Support, transactionId, creation,
            data => ResolveSlotItem(data, pending.SlotId), () =>
        {
            if (!Current(player, sender, creation, record, key, pending, RebirthGearTransferPhase.GearCommitted)) return false;
            RebirthWorldCharacterService.MarkDirty(record, "remote-gear-commit");
            try { RebirthWorldCharacterRepository.SaveIfDirty(identity, "remote-gear-commit"); }
            catch { /* Resolve uncertain writes from the exact final character file. */ }
            return RebirthWorldCharacterRepository.HasSavedGearTransfer(identity, pending,
                RebirthGearTransferPhase.GearCommitted) &&
                Current(player, sender, creation, record, key, pending, RebirthGearTransferPhase.GearCommitted);
        });
    }

    // Server-only intent checkpoint. A retry with an existing attempt refuses;
    // reconcile that saved native identity rather than spawning another entity.
    public static bool TryRecordRecoveryAttempt(EntityPlayer player,ClientInfo sender,string creation,string transactionId,RebirthGearRecoveryAttempt attempt,out RebirthGearTransferState checkpoint)
    {
        checkpoint=null;
        if(player==null||attempt==null||attempt.OriginalOwnerEntityId<=0||attempt.OriginalOwnerEntityId!=player.entityId||!RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||record.StablePlayerKey!=identity.StorageKey)return false;
        int originalOwnerEntityId=attempt.OriginalOwnerEntityId;
        string key=identity.StorageKey;var pending=record.Support.PendingGearTransfer;
        if(pending==null||pending.TransactionId!=transactionId||
            (player.entityId!=originalOwnerEntityId)||!Current(player,sender,creation,record,key,pending,RebirthGearTransferPhase.GearCommitted)||
            !RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,pending,RebirthGearTransferPhase.GearCommitted)||
            (player.entityId!=originalOwnerEntityId)||!Current(player,sender,creation,record,key,pending,RebirthGearTransferPhase.GearCommitted))return false;
        RebirthGearTransferState saved=null;
        bool recorded=RebirthGearTransferJournal.RecordRecoveryAttempt(record.Support,transactionId,creation,attempt,()=>
        {
            var updated=record.Support.PendingGearTransfer;
            if(updated==null||!updated.TryGetRecoveryAttempt(attempt.PublicationId,out var retained)||
                !System.Xml.Linq.XNode.DeepEquals(retained.ToXml(),attempt.ToXml())||
                (player.entityId!=originalOwnerEntityId)||!Current(player,sender,creation,record,key,updated,RebirthGearTransferPhase.GearCommitted))return false;
            RebirthWorldCharacterService.MarkDirty(record,"remote-gear-recovery-attempt");
            try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"remote-gear-recovery-attempt");}catch{}
            if(!RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,updated,RebirthGearTransferPhase.GearCommitted)||
                (player.entityId!=originalOwnerEntityId)||!Current(player,sender,creation,record,key,updated,RebirthGearTransferPhase.GearCommitted))return false;
            saved=updated;return true;
        });
        if(!recorded||saved==null||(player.entityId!=originalOwnerEntityId)||!Current(player,sender,creation,record,key,saved,RebirthGearTransferPhase.GearCommitted))return false;
        checkpoint=saved;return true;
    }
    // Empty recovery is proven by the persisted conserved plan, not a client claim.
    public static bool TryFinishEmptyRecovery(EntityPlayer player,ClientInfo sender,string creation,string transactionId)
    {
        return TryFinishRecovery(player,sender,creation,transactionId,true);
    }
    // Nonempty recovery requires every original publication receipt in the final saved file.
    public static bool TryFinishRecovery(EntityPlayer player,ClientInfo sender,string creation,string transactionId)
    {
        return TryFinishRecovery(player,sender,creation,transactionId,false);
    }
    private static bool TryFinishRecovery(EntityPlayer player,ClientInfo sender,string creation,string transactionId,bool requireEmpty)
    {
        if(!RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||record.StablePlayerKey!=identity.StorageKey)return false;
        var pending=record.Support.PendingGearTransfer;
        if(pending==null||pending.TransactionId!=transactionId||!pending.TryGetPlan(out var plan)||(requireEmpty&&plan.Recovery.Count!=0)||!pending.HasAllRecoveryPublicationReceipts||record.Support.PendingMusicTransfer!=null||record.Support.PendingLibraryTransfer!=null||
            !Current(player,sender,creation,record,identity.StorageKey,pending,RebirthGearTransferPhase.GearCommitted)||
            !RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,pending,RebirthGearTransferPhase.GearCommitted))return false;
        if(!Current(player,sender,creation,record,identity.StorageKey,pending,RebirthGearTransferPhase.GearCommitted))return false;
        return RebirthGearTransferJournal.FinishRecovery(record.Support,transactionId,creation,()=>
        {
            var terminal=record.Support.LastGearSettlement;
            Func<bool> current=()=>RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var live)&&ReferenceEquals(live,record)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)&&
                liveIdentity!=null&&liveIdentity.StorageKey==identity.StorageKey&&record.StablePlayerKey==identity.StorageKey&&
                record.Support.PendingGearTransfer==null&&record.Support.GearTransferPhase==RebirthGearTransferPhase.Prepared&&
                terminal!=null&&ReferenceEquals(record.Support.LastGearSettlement,terminal)&&terminal.TransactionId==transactionId&&terminal.GearRevision==record.Support.GearRevision&&RebirthSurvivorRequestScope.Matches(creation,terminal.CreationId)&&record.Support.PendingMusicTransfer==null&&record.Support.PendingLibraryTransfer==null;
            if(!current())return false;

            RebirthWorldCharacterService.MarkDirty(record,"remote-gear-finish-recovery");
            try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"remote-gear-finish-recovery");}catch{}
            return current()&&RebirthWorldCharacterRepository.HasSavedGearSettlement(identity,terminal)&&current();
        });
    }
    // Only an independently saved owner rejection with untouched inventory may
    // cancel Prepared custody. Missing ACK, disconnect and elapsed time cannot.
    public static bool TryCancelRejected(EntityPlayer player,ClientInfo sender,string creation,string transactionId)
    {
        if(!RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||record.StablePlayerKey!=identity.StorageKey)return false;
        string key=identity.StorageKey;var pending=record.Support.PendingGearTransfer;
        if(pending==null||pending.TransactionId!=transactionId||record.Support.PendingMusicTransfer!=null||record.Support.PendingLibraryTransfer!=null||
            !Current(player,sender,creation,record,key,pending,RebirthGearTransferPhase.Prepared)||
            !RebirthWorldCharacterRepository.HasSavedGearTransfer(identity,pending,RebirthGearTransferPhase.Prepared)||
            !RebirthGearPlayerFileWitness.HasRejected(identity,pending)||
            !Current(player,sender,creation,record,key,pending,RebirthGearTransferPhase.Prepared))return false;
        return RebirthGearTransferJournal.CancelRejected(record.Support,transactionId,creation,()=>
        {
            var terminal=record.Support.LastGearSettlement;
            Func<bool> current=()=>RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var live)&&ReferenceEquals(live,record)&&
                RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)&&RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)&&
                liveIdentity!=null&&liveIdentity.StorageKey==key&&record.StablePlayerKey==key&&record.Support.PendingGearTransfer==null&&
                record.Support.GearTransferPhase==RebirthGearTransferPhase.Prepared&&terminal!=null&&!terminal.Applied&&
                ReferenceEquals(record.Support.LastGearSettlement,terminal)&&terminal.TransactionId==transactionId&&
                terminal.GearRevision==pending.ExpectedRevision+1&&record.Support.GearRevision==terminal.GearRevision&&
                RebirthSurvivorRequestScope.Matches(creation,terminal.CreationId)&&record.Support.PendingMusicTransfer==null&&record.Support.PendingLibraryTransfer==null;
            if(!current())return false;
            RebirthWorldCharacterService.MarkDirty(record,"remote-gear-rejected");
            try{RebirthWorldCharacterRepository.SaveIfDirty(identity,"remote-gear-rejected");}catch{}
            return current()&&RebirthWorldCharacterRepository.HasSavedGearSettlement(identity,terminal)&&current();
        });
    }
    public static bool TryGetSettlement(EntityPlayer player,ClientInfo sender,string creation,string transactionId,out RebirthGearSettlement settlement)
    {
        settlement=null;
        if(!RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var record)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||record.StablePlayerKey!=identity.StorageKey)return false;
        string key=identity.StorageKey;var terminal=record.Support.LastGearSettlement;
        if(terminal==null||terminal.TransactionId!=transactionId||!RebirthSurvivorRequestScope.Matches(creation,terminal.CreationId)||
            record.Support.PendingGearTransfer!=null||record.Support.GearRevision!=terminal.GearRevision||
            !RebirthWorldCharacterRepository.HasSavedGearSettlement(identity,terminal)||
            !RebirthRemoteGearInventorySource.TryResolve(player,sender,creation,out var live)||!ReferenceEquals(live,record)||
            !RebirthWorldCharacterRepository.IsCurrentCachedRecord(record)||!ReferenceEquals(record.Support.LastGearSettlement,terminal)||
            record.Support.PendingGearTransfer!=null||record.Support.GearRevision!=terminal.GearRevision||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var liveIdentity)||liveIdentity==null||liveIdentity.StorageKey!=key)return false;
        settlement=terminal;return true;
    }
    // Exact retained terminal request binding. No pending plan or original
    // preimage is required after completion; no permission to repeat effects.
    public static bool TryGetBoundSettlement(EntityPlayer player,ClientInfo sender,string originalMarker,
        out RebirthGearSettlement settlement)
    {
        settlement=null;
        try
        {
            if(player==null||sender==null||!ThreadManager.IsMainThread()||
                !RebirthGearPreparationMarker.TryRead(originalMarker,1f,out var savedWorld,out _,out var intent))return false;
            var game=GameManager.Instance;var world=game?.World;var state=world?.worldState;
            var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(world==null||state==null||manager==null)return false;
            Func<bool> current=()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&
                ReferenceEquals(game.World,world)&&!world.IsRemote()&&ReferenceEquals(world.worldState,state)&&
                Guid.TryParse(state.Guid,out var guid)&&guid==savedWorld&&
                ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)&&manager.IsServer&&manager.Clients!=null&&
                ReferenceEquals(manager.Clients.ForEntityId(player.entityId),sender)&&
                RebirthWorldCharacterService.TryGetIdentity(player,out var owner)&&owner!=null&&
                RebirthStablePlayerIdentity.TryFromClientInfo(sender,out var authenticated)&&authenticated!=null&&
                owner.CanonicalId==authenticated.CanonicalId&&owner.StorageKey==authenticated.StorageKey;
            if(!current()||!TryGetSettlement(player,sender,intent.CreationId,intent.TransactionId.ToString("N"),out var terminal)||
                !terminal.MatchesOriginalMarker(originalMarker)||!current()||
                !TryGetSettlement(player,sender,intent.CreationId,intent.TransactionId.ToString("N"),out var final)||
                !ReferenceEquals(final,terminal)||!current())return false;
            settlement=terminal;return true;
        }
        catch{return false;}
    }
    // Original terminal plan for cold owner verification only. Pending effects
    // remain absent; receipt delivery is not permission to reapply inventory.
    public static bool TryGetBoundTerminalOriginal(EntityPlayer player,ClientInfo sender,string marker,
        out RebirthGearTransferState original,out RebirthGearSettlement settlement)
    {
        original=null;settlement=null;
        try
        {
            if(!TryGetBoundSettlement(player,sender,marker,out var terminal)||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,terminal.CreationId,out var record))return false;
            var retained=record.Support.LastGearSettlementOriginal;
            if(!RebirthGearTerminalOriginalPersistence.Matches(terminal,retained)||
                !RebirthGearTransferState.TryRead(retained.ToXml(),out var copy)||
                !TryGetBoundSettlement(player,sender,marker,out var final)||!ReferenceEquals(final,terminal)||
                !RebirthRemoteGearInventorySource.TryResolve(player,sender,terminal.CreationId,out var current)||
                !ReferenceEquals(current,record)||!ReferenceEquals(record.Support.LastGearSettlementOriginal,retained)||
                record.Support.PendingGearTransfer!=null)return false;
            original=copy;settlement=terminal;return true;
        }
        catch{return false;}
    }
    private static string ResolveSlotItem(string data, string slot)
    {
        if (!RebirthNativeItemCodec.TryDecode(data, out var value) || value?.ItemClass == null) return null;
        string id = value.ItemClass.GetItemName();
        if (!RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(id, out var profile) ||
            profile == null || !string.Equals(profile.Kind, "survivor_gear", StringComparison.OrdinalIgnoreCase) ||
            profile.GearSlotId != slot) return null;
        return id;
    }
    private static bool Current(EntityPlayer player, ClientInfo sender, string creation,
        RebirthWorldCharacterRecord expected, string key, RebirthGearTransferState pending,
        RebirthGearTransferPhase phase)
    {
        return expected != null && expected.StablePlayerKey == key &&
            RebirthWorldCharacterRepository.IsCurrentCachedRecord(expected) &&
            RebirthRemoteGearInventorySource.TryResolve(player, sender, creation, out var current) &&
            ReferenceEquals(current, expected) &&
            RebirthWorldCharacterService.TryGetIdentity(player, out var identity) &&
            identity != null && identity.StorageKey == key &&
            ReferenceEquals(expected.Support?.PendingGearTransfer, pending) &&
            RebirthGearTransferSavedWitness.Matches(expected.Support, creation, pending, phase);
    }
}