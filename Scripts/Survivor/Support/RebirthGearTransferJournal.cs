using System;
using System.Xml.Linq;

// Save callback must perform the durable character write, including MarkDirty.
// This journal does not authenticate packets or mutate native inventory.
public static class RebirthGearTransferJournal
{
    public static bool Prepare(RebirthWorldSupportState state, RebirthGearTransferState request,
        string currentCreationId, Func<bool> save)
    {
        if (save == null || !Matches(state, request, currentCreationId) || (state.PendingMusicTransfer != null||state.PendingLibraryTransfer!=null||state.PendingGearPreparationRefusal!=null)
            || state.GearTransferPhase != RebirthGearTransferPhase.Prepared || state.LastGearSettlement?.TransactionId == request.TransactionId) return false;
        if (state.PendingGearTransfer != null)
            return XNode.DeepEquals(state.PendingGearTransfer.ToXml(), request.ToXml()) && save();
        state.PendingGearTransfer = request;
        // False/exception may follow a durable write or a failed readback. Retain
        // the original intent so a retry cannot mint another transaction. Saved
        // witness remains mandatory for delivery; retention is not permission.
        return save();
    }

    // Call only after verifying a persistent rejection receipt from the owner.
    // Full backpack, timeout, disconnect or missing ACK are NOT rejections.
    public static bool CancelRejected(RebirthWorldSupportState state, string transactionId,
        string currentCreationId, Func<bool> save)
    {
        var pending = state == null ? null : state.PendingGearTransfer;
        if (save == null || pending == null || pending.TransactionId != transactionId
            || !Matches(state, pending, currentCreationId)
            || state.GearTransferPhase != RebirthGearTransferPhase.Prepared) return false;
        var oldSettlement = state.LastGearSettlement;
        var oldOriginal = state.LastGearSettlementOriginal;
        state.LastGearSettlement = RebirthGearSettlement.Create(pending, state.GearTransferPhase == RebirthGearTransferPhase.GearCommitted);
        state.LastGearSettlementOriginal = pending.PreparationRequestDigest==null?null:pending;
        state.PendingGearTransfer = null;
        state.GearRevision++;
        bool saved = false;
        try { saved = save(); return saved; }
        finally
        {
            if (!saved) { state.LastGearSettlement = oldSettlement; state.LastGearSettlementOriginal = oldOriginal; state.GearRevision--; state.PendingGearTransfer = pending; }
        }
    }

    // Caller has verified the durable owner receipt against this transaction.
    public static bool MarkOwnerApplied(RebirthWorldSupportState state, string transactionId,
        string creationId, Func<bool> save)
    {
        var pending = state == null ? null : state.PendingGearTransfer;
        if (save == null || pending == null || pending.TransactionId != transactionId
            || !Matches(state, pending, creationId)
            || state.GearTransferPhase == RebirthGearTransferPhase.GearCommitted) return false;
        if (state.GearTransferPhase == RebirthGearTransferPhase.OwnerApplied) return save();
        if (state.GearTransferPhase != RebirthGearTransferPhase.Prepared) return false;
        state.GearTransferPhase = RebirthGearTransferPhase.OwnerApplied;
        bool saved = false;
        try { saved = save(); return saved; }
        finally { if (!saved) state.GearTransferPhase = RebirthGearTransferPhase.Prepared; }
    }

    // Resolver must decode the exact ItemValue and validate its gear-slot profile.
    // No recovery items are spawned here; custody remains pending after commit.
    public static bool CommitGear(RebirthWorldSupportState state, string transactionId,
        string creationId, Func<string, string> resolveGearItemId, Func<bool> save)
    {
        var pending = state == null ? null : state.PendingGearTransfer;
        if (save == null || pending == null || pending.TransactionId != transactionId
            || !Matches(state, pending, creationId)) return false;
        if (state.GearTransferPhase == RebirthGearTransferPhase.GearCommitted) return save();
        if (state.GearTransferPhase != RebirthGearTransferPhase.OwnerApplied) return false;
        RebirthGearInventoryPlan plan;
        if (pending.HasRecoveryAttempts&&state.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted) return false;
        if (!pending.TryGetPlan(out plan)) return false;
        string newId = plan.GearAfter.Count == 0 ? null : resolveGearItemId == null ? null : resolveGearItemId(plan.GearAfter.ItemData);
        if (plan.GearAfter.Count != 0 && string.IsNullOrEmpty(newId)) return false;
        string oldId, oldData;
        bool hadId = state.EquippedGearBySlot.TryGetValue(pending.SlotId, out oldId);
        bool hadData = state.EquippedGearItemDataBySlot.TryGetValue(pending.SlotId, out oldData);
        if (plan.GearAfter.Count == 0)
        {
            state.EquippedGearBySlot.Remove(pending.SlotId);
            state.EquippedGearItemDataBySlot.Remove(pending.SlotId);
        }
        else
        {
            state.EquippedGearBySlot[pending.SlotId] = newId;
            state.EquippedGearItemDataBySlot[pending.SlotId] = plan.GearAfter.ItemData;
        }
        state.GearRevision++;
        state.GearTransferPhase = RebirthGearTransferPhase.GearCommitted;
        bool saved = false;
        try { saved = save(); return saved; }
        finally
        {
            if (!saved)
            {
                state.GearRevision--;
                state.GearTransferPhase = RebirthGearTransferPhase.OwnerApplied;
                if (hadId) state.EquippedGearBySlot[pending.SlotId] = oldId; else state.EquippedGearBySlot.Remove(pending.SlotId);
                if (hadData) state.EquippedGearItemDataBySlot[pending.SlotId] = oldData; else state.EquippedGearItemDataBySlot.Remove(pending.SlotId);
            }
        }
    }

    // A true result is a recorded intent, NOT a spawn acknowledgment. Caller must
    // verify the exact final-file witness before the first native spawn effect.
    public static bool RecordRecoveryAttempt(RebirthWorldSupportState state,string transactionId,string creationId,RebirthGearRecoveryAttempt attempt,Func<bool> save)
    {
        var previous=state?.PendingGearTransfer;
        if(previous==null||save==null||previous.TransactionId!=transactionId||state.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted||!Matches(state,previous,creationId)||state.PendingMusicTransfer!=null||state.PendingLibraryTransfer!=null||!previous.TryAppendRecoveryAttempt(attempt,out var updated))return false;
        state.PendingGearTransfer=updated;bool saved=false;
        try{saved=save();return saved&&ReferenceEquals(state.PendingGearTransfer,updated)&&state.GearTransferPhase==RebirthGearTransferPhase.GearCommitted&&Matches(state,updated,creationId);}
        finally{if(!saved&&ReferenceEquals(state.PendingGearTransfer,updated))state.PendingGearTransfer=previous;}
    }
    // Caller must independently verify the original publication on disk first.
    public static bool RecordRecoveryPublicationReceipt(RebirthWorldSupportState state,string transactionId,string creationId,Guid publicationId,Func<bool> save)
    {
        var previous=state?.PendingGearTransfer;
        if(previous==null||save==null||previous.TransactionId!=transactionId||state.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted||!Matches(state,previous,creationId)||state.PendingMusicTransfer!=null||state.PendingLibraryTransfer!=null||!previous.TryAppendRecoveryPublicationReceipt(publicationId,out var updated))return false;
        state.PendingGearTransfer=updated;bool saved=false;
        try{saved=save();return saved&&ReferenceEquals(state.PendingGearTransfer,updated)&&state.GearTransferPhase==RebirthGearTransferPhase.GearCommitted&&Matches(state,updated,creationId);}
        finally{if(!saved&&ReferenceEquals(state.PendingGearTransfer,updated))state.PendingGearTransfer=previous;}
    }
    // Only after durable recovery reconciliation proves all planned drops delivered.
    public static bool FinishRecovery(RebirthWorldSupportState state, string transactionId,
        string creationId, Func<bool> save)
    {
        var pending = state == null ? null : state.PendingGearTransfer;
        if (save == null || pending == null || pending.TransactionId != transactionId
            || state.GearTransferPhase != RebirthGearTransferPhase.GearCommitted
            || !Matches(state, pending, creationId)||!pending.HasAllRecoveryPublicationReceipts) return false;
        var oldSettlement = state.LastGearSettlement;
        var oldOriginal = state.LastGearSettlementOriginal;
        state.LastGearSettlement = RebirthGearSettlement.Create(pending, state.GearTransferPhase == RebirthGearTransferPhase.GearCommitted);
        state.LastGearSettlementOriginal = pending.PreparationRequestDigest==null?null:pending;
        state.PendingGearTransfer = null;
        state.GearTransferPhase = RebirthGearTransferPhase.Prepared;
        bool saved = false;
        try { saved = save(); return saved; }
        finally
        {
            if (!saved) { state.LastGearSettlement = oldSettlement; state.LastGearSettlementOriginal = oldOriginal; state.PendingGearTransfer = pending; state.GearTransferPhase = RebirthGearTransferPhase.GearCommitted; }
        }
    }

    internal static bool Matches(RebirthWorldSupportState state, RebirthGearTransferState pending, string creationId)
    {

        if (state == null || pending == null || state.GearRevision != pending.ExpectedRevision + (state.GearTransferPhase == RebirthGearTransferPhase.GearCommitted ? 1 : 0)
            || state.GearTransferPhase < RebirthGearTransferPhase.Prepared || state.GearTransferPhase > RebirthGearTransferPhase.GearCommitted
            || !RebirthSurvivorRequestScope.Matches(creationId,pending.CreationId)) return false;
        RebirthGearInventoryPlan plan;
        if (pending.HasRecoveryAttempts&&state.GearTransferPhase!=RebirthGearTransferPhase.GearCommitted) return false;
        if (!pending.TryGetPlan(out plan)) return false;
        string itemId, itemData;
        state.EquippedGearBySlot.TryGetValue(pending.SlotId, out itemId);
        state.EquippedGearItemDataBySlot.TryGetValue(pending.SlotId, out itemData);
        var expected = state.GearTransferPhase == RebirthGearTransferPhase.GearCommitted ? plan.GearAfter : plan.GearBefore;
        if (expected.Count == 0) return string.IsNullOrEmpty(itemId) && string.IsNullOrEmpty(itemData);
        // Legacy name-only gear must be resolved and persisted to exact native
        // data before preparing custody. Never guess its metadata in a retry.
        return !string.IsNullOrEmpty(itemId) && itemData == expected.ItemData;
    }
}

