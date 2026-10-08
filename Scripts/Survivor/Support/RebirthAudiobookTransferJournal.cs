using System;

// Shared pending cassette reservation prevents competing music/audio custody.
// Caller must authenticate the durable owner receipt before Commit/CancelRejected.
public static class RebirthAudiobookTransferJournal
{
    public static bool Prepare(RebirthWorldSupportState state,RebirthMusicTransferState request,Func<bool> save)
    {
        if(state==null||request==null||save==null||state.PendingGearTransfer!=null||state.PendingLibraryTransfer!=null)return false;
        RebirthMusicTransferState validated;
        if(!RebirthMusicTransferState.TryRead(request.ToXml(),out validated)||!Matches(state,validated))return false;
        if(state.PendingMusicTransfer!=null)
            return state.PendingMusicTransfer.ToXml().ToString()==validated.ToXml().ToString()&&save();
        state.PendingMusicTransfer=validated;
        bool saved=false;
        try { saved=save();return saved; }
        finally { if(!saved)state.PendingMusicTransfer=null; }
    }
    public static bool Commit(RebirthWorldSupportState state,string transactionId,Func<bool> save)
    {
        var pending=state?.PendingMusicTransfer;
        if(pending==null||save==null||pending.TransactionId!=transactionId||!Matches(state,pending)||state.AudiobookRevision==long.MaxValue)return false;
        RebirthAudiobookCassetteState removed=null;
        if(pending.Operation==1)
            state.AudiobookCassettes.Add(new RebirthAudiobookCassetteState { SlotId=pending.AudiobookSlotId,ItemId=pending.ItemId,ItemData=pending.ItemData });
        else { removed=state.AudiobookCassettes[pending.LibraryIndex];state.AudiobookCassettes.RemoveAt(pending.LibraryIndex); }
        state.AudiobookRevision++;state.PendingMusicTransfer=null;
        bool saved=false;
        try { saved=save();return saved; }
        finally
        {
            if(!saved)
            {
                state.PendingMusicTransfer=pending;state.AudiobookRevision--;
                if(pending.Operation==1)state.AudiobookCassettes.RemoveAt(state.AudiobookCassettes.Count-1);
                else state.AudiobookCassettes.Insert(pending.LibraryIndex,removed);
            }
        }
    }
    public static bool CancelRejected(RebirthWorldSupportState state,string transactionId,Func<bool> save)
    {
        var pending=state?.PendingMusicTransfer;
        if(pending==null||save==null||pending.TransactionId!=transactionId||!Matches(state,pending)||state.AudiobookRevision==long.MaxValue)return false;
        state.PendingMusicTransfer=null;state.AudiobookRevision++;
        bool saved=false;
        try { saved=save();return saved; }
        finally { if(!saved){state.PendingMusicTransfer=pending;state.AudiobookRevision--;} }
    }
    private static bool Matches(RebirthWorldSupportState state,RebirthMusicTransferState pending)
    {
        if(!pending.IsAudiobook||state.AudiobookRevision!=pending.ExpectedRevision)return false;
        
        if(pending.Operation==1)
        {
            RebirthAudiobookDefinition definition;
            if(!RebirthProgressionRuntimeConfig.TryGetAudiobook(pending.ItemId,out definition)||definition==null)return false;
            if(state.AudiobookCassettes.Count>=RebirthAudiobookLibraryPersistence.Capacity||pending.LibraryIndex!=state.AudiobookCassettes.Count)return false;
            foreach(var entry in state.AudiobookCassettes)
                if(entry==null||string.Equals(entry.SlotId,pending.AudiobookSlotId,StringComparison.OrdinalIgnoreCase))return false;
            return true;
        }
        if(pending.Operation!=2||pending.LibraryIndex<0||pending.LibraryIndex>=state.AudiobookCassettes.Count)return false;
        var cassette=state.AudiobookCassettes[pending.LibraryIndex];
        return cassette!=null&&cassette.SlotId==pending.AudiobookSlotId&&cassette.ItemId==pending.ItemId&&cassette.ItemData==pending.ItemData;
    }
}