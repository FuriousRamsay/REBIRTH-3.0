internal static partial class RebirthHydrationOwnerHold
{
    // Invoked only by the authenticated Rejected receiver. The saved refusal
    // proves native Before was retained; no grant or item write is created.
    internal static bool RetireHistoricalRefusal(EntityPlayerLocal player,RebirthHydrationTransferOriginal original)
    {
        if(!RebirthHydrationRefusedCheckpoint.HasSavedHistorical(player,original)||
            RebirthHydrationRemoteClientGrant.HasIssued(original.Operation))return false;
        var remove=new System.Collections.Generic.List<EntityPlayerLocal>();
        foreach(var pair in entries)
            if(pair.Value.Original.Digest==original.Digest)
            {if(Current(pair.Value))return false;remove.Add(pair.Key);}
        if(!RebirthHydrationRefusedCheckpoint.HasSavedHistorical(player,original))return false;
        foreach(var key in remove)entries.Remove(key);
        reenteredRemoteEntries.RemoveAll(entry=>entry.Original.Digest==original.Digest&&!Current(entry));
        return true;
    }
    internal static bool TryReleasePreparedRefusal(RebirthHydrationClientPreparedRejection rejection)
    {
        if(rejection==null||!rejection.Current()||!RebirthHydrationRefusedCheckpoint.HasSaved(rejection)||
            !entries.TryGetValue(rejection.Player,out var original)||original.Original.Digest!=rejection.Original.Digest||!Current(original))return false;
        return entries.Remove(rejection.Player);
    }
}
