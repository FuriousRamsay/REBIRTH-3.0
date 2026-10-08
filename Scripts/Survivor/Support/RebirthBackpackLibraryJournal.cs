using System;
using System.Xml.Linq;

// Durable callbacks must mark/save character state; checkpoint callback must inspect
// authenticated saved native owner data for this exact receipt, never a client ACK alone.
public static class RebirthBackpackLibraryJournal
{
    private static bool Save(Func<bool> save){try{return save!=null&&save();}catch{return false;}}
    private static bool Evidence(Func<RebirthBackpackLibraryReceipt,bool> check,RebirthBackpackLibraryReceipt receipt)
    {try{return check!=null&&check(receipt);}catch{return false;}}
    private static bool Matches(RebirthWorldSupportState state,RebirthBackpackLibraryReceipt receipt,string creation)
    {
        if(state==null||receipt==null||state.PendingGearTransfer!=null||state.PendingMusicTransfer!=null||
            !RebirthSurvivorRequestScope.Matches(creation,receipt.CreationId)||
            state.LibraryTransferPhase<RebirthBackpackLibraryPhase.Prepared||state.LibraryTransferPhase>RebirthBackpackLibraryPhase.BackpackCommitted||
            state.GearRevision!=receipt.ExpectedGearRevision+(state.LibraryTransferPhase==RebirthBackpackLibraryPhase.BackpackCommitted?1:0))return false;
        if(!receipt.TryGetImages(out var before,out var after,out _,out _))return false;
        var expected=state.LibraryTransferPhase==RebirthBackpackLibraryPhase.BackpackCommitted?after:before;
        string item,data;
        return state.EquippedGearBySlot.TryGetValue("backpack",out item)&&
            state.EquippedGearItemDataBySlot.TryGetValue("backpack",out data)&&
            string.Equals(item,expected.ItemClass.GetItemName(),StringComparison.Ordinal)&&
            string.Equals(data,RebirthNativeItemCodec.Encode(expected),StringComparison.Ordinal);
    }
    // Reconnect offers reuse the immutable pending intent; never create another transaction.
    public static bool TryGetPending(RebirthWorldSupportState state,string creation,out RebirthBackpackLibraryReceipt receipt)
    {
        receipt=null;
        var pending=state?.PendingLibraryTransfer;
        if(!Matches(state,pending,creation))return false;
        receipt=pending;return true;
    }
    public static bool Prepare(RebirthWorldSupportState state,RebirthBackpackLibraryReceipt receipt,string creation,Func<bool> save)
    {
        if(save==null||!Matches(state,receipt,creation)||state.LibraryTransferPhase!=RebirthBackpackLibraryPhase.Prepared)return false;
        if(state.PendingLibraryTransfer!=null)return XNode.DeepEquals(state.PendingLibraryTransfer.ToXml(),receipt.ToXml())&&Save(save);
        state.PendingLibraryTransfer=receipt;
        if(Save(save))return true;
        state.PendingLibraryTransfer=null;return false;
    }
    private static RebirthBackpackLibraryReceipt Pending(RebirthWorldSupportState state,string transaction,string creation)
    {
        var receipt=state?.PendingLibraryTransfer;
        return receipt!=null&&receipt.TransactionId==transaction&&Matches(state,receipt,creation)?receipt:null;
    }
    public static bool MarkOwnerApplied(RebirthWorldSupportState state,string transaction,string creation,
        Func<RebirthBackpackLibraryReceipt,bool> verifySavedOwner,Func<bool> save)
    {
        var receipt=Pending(state,transaction,creation);
        if(receipt==null||save==null||state.LibraryTransferPhase==RebirthBackpackLibraryPhase.BackpackCommitted||!Evidence(verifySavedOwner,receipt))return false;
        if(state.LibraryTransferPhase==RebirthBackpackLibraryPhase.OwnerApplied)return Save(save);
        state.LibraryTransferPhase=RebirthBackpackLibraryPhase.OwnerApplied;
        if(Save(save))return true;
        state.LibraryTransferPhase=RebirthBackpackLibraryPhase.Prepared;return false;
    }
    public static bool CommitBackpack(RebirthWorldSupportState state,string transaction,string creation,Func<bool> save)
    {
        var receipt=Pending(state,transaction,creation);if(receipt==null||save==null)return false;
        if(state.LibraryTransferPhase==RebirthBackpackLibraryPhase.BackpackCommitted)return Save(save);
        if(state.LibraryTransferPhase!=RebirthBackpackLibraryPhase.OwnerApplied||!receipt.TryGetImages(out _,out var after,out _,out _))return false;
        string prior=state.EquippedGearItemDataBySlot["backpack"];
        state.EquippedGearItemDataBySlot["backpack"]=RebirthNativeItemCodec.Encode(after);
        state.GearRevision++;state.LibraryTransferPhase=RebirthBackpackLibraryPhase.BackpackCommitted;
        if(Save(save))return true;
        state.EquippedGearItemDataBySlot["backpack"]=prior;state.GearRevision--;state.LibraryTransferPhase=RebirthBackpackLibraryPhase.OwnerApplied;return false;
    }
    public static bool Finish(RebirthWorldSupportState state,string transaction,string creation,
        Func<RebirthBackpackLibraryReceipt,bool> verifySavedOwner,Func<bool> save)
    {
        var receipt=Pending(state,transaction,creation);
        if(receipt==null||save==null||state.LibraryTransferPhase!=RebirthBackpackLibraryPhase.BackpackCommitted||!Evidence(verifySavedOwner,receipt))return false;
        var oldSettlement=state.LastLibrarySettlement;
        state.LastLibrarySettlement=RebirthBackpackLibrarySettlement.Create(receipt,true);
        state.PendingLibraryTransfer=null;state.LibraryTransferPhase=RebirthBackpackLibraryPhase.Prepared;
        if(Save(save))return true;
        state.LastLibrarySettlement=oldSettlement;
        state.PendingLibraryTransfer=receipt;state.LibraryTransferPhase=RebirthBackpackLibraryPhase.BackpackCommitted;return false;
    }
    // Only an authenticated durable rejection permits cancellation. Timeout/full bag is pending.
    public static bool CancelRejected(RebirthWorldSupportState state,string transaction,string creation,
        Func<RebirthBackpackLibraryReceipt,bool> verifySavedRejection,Func<bool> save)
    {
        var receipt=Pending(state,transaction,creation);
        if(receipt==null||save==null||state.LibraryTransferPhase!=RebirthBackpackLibraryPhase.Prepared||!Evidence(verifySavedRejection,receipt))return false;
        var oldSettlement=state.LastLibrarySettlement;
        state.LastLibrarySettlement=RebirthBackpackLibrarySettlement.Create(receipt,false);
        state.PendingLibraryTransfer=null;state.GearRevision++;
        if(Save(save))return true;
        state.LastLibrarySettlement=oldSettlement;
        state.PendingLibraryTransfer=receipt;state.GearRevision--;return false;
    }
}
