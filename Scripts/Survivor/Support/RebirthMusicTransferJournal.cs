using System;

// The caller supplies a durable character save (including MarkDirty). No owner
// inventory action may be dispatched unless Prepare returns true.
public static class RebirthMusicTransferJournal
{
    public static bool Prepare(RebirthWorldSupportState state, RebirthMusicTransferState request, Func<bool> save)
    {
        if (state == null || request == null || save == null || (state.PendingGearTransfer != null||state.PendingLibraryTransfer!=null)) return false;
        RebirthMusicTransferState validated;
        if (!RebirthMusicTransferState.TryRead(request.ToXml(), out validated)
            || !MatchesLibrary(state, validated)) return false;
        if (state.PendingMusicTransfer != null)
        {
            // Never replace custody, including when the caller reuses an ID with
            // different contents. A retry must first confirm the durable write.
            return state.PendingMusicTransfer.ToXml().ToString() == validated.ToXml().ToString() && save();
        }
        state.PendingMusicTransfer = validated;
        try
        {
            if (save()) return true;
        }
        catch
        {
            state.PendingMusicTransfer = null;
            throw;
        }
        state.PendingMusicTransfer = null;
        return false;
    }

    public static bool Commit(RebirthWorldSupportState state, string transactionId, Func<bool> save)
    {
        var pending = state == null ? null : state.PendingMusicTransfer;
        if (pending == null || save == null || pending.TransactionId != transactionId
            || !MatchesLibrary(state, pending) || state.MusicRevision == long.MaxValue) return false;
        RebirthMusicCassetteState removed = null;
        if (pending.Operation == 1)
            state.MusicCassettes.Add(new RebirthMusicCassetteState { ItemId = pending.ItemId, ItemData = pending.ItemData });
        else
        {
            removed = state.MusicCassettes[pending.LibraryIndex];
            state.MusicCassettes.RemoveAt(pending.LibraryIndex);
        }
        state.MusicRevision++;
        state.PendingMusicTransfer = null;
        bool saved = false;
        try
        {
            saved = save();
            return saved;
        }
        finally
        {
            // Keep the pending operation available for a repeated owner ACK if
            // persistence fails. Never request a second inventory mutation.
            if (!saved)
            {
                state.PendingMusicTransfer = pending;
                state.MusicRevision--;
                if (pending.Operation == 1) state.MusicCassettes.RemoveAt(state.MusicCassettes.Count - 1);
                else state.MusicCassettes.Insert(pending.LibraryIndex, removed);
            }
        }
    }

    // Only call after the authenticated owner has recorded a permanent rejection.
    // A timeout or full backpack is not evidence that an operation was rejected.
    public static bool CancelRejected(RebirthWorldSupportState state, string transactionId, Func<bool> save)
    {
        var pending = state == null ? null : state.PendingMusicTransfer;
        if (pending == null || save == null || pending.TransactionId != transactionId
            || !MatchesLibrary(state, pending) || state.MusicRevision == long.MaxValue) return false;
        state.PendingMusicTransfer = null;
        // Invalidate delayed UI requests for the operation that was cancelled.
        state.MusicRevision++;
        bool saved = false;
        try
        {
            saved = save();
            return saved;
        }
        finally
        {
            if (!saved)
            {
                state.PendingMusicTransfer = pending;
                state.MusicRevision--;
            }
        }
    }

    private static bool MatchesLibrary(RebirthWorldSupportState state, RebirthMusicTransferState pending)
    {
        if (pending.IsAudiobook || state.MusicRevision != pending.ExpectedRevision || !RebirthMusicLibraryService.IsMusicCassette(pending.ItemId)) return false;
        if (pending.Operation == 1)
            return state.MusicCassettes.Count < RebirthMusicLibraryService.Capacity && pending.LibraryIndex == state.MusicCassettes.Count;
        if (pending.Operation != 2 || pending.LibraryIndex < 0 || pending.LibraryIndex >= state.MusicCassettes.Count) return false;
        var cassette = state.MusicCassettes[pending.LibraryIndex];
        return cassette != null && cassette.ItemId == pending.ItemId && cassette.ItemData == pending.ItemData;
    }
}

