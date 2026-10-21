using System;
using System.Collections.Generic;

public static partial class RebirthNpcInventoryTransactionService
{
    // Explicit Bridge observation only: never loads or creates an inventory,
    // flushes a store, reserves stock or manufactures a trade receipt.
    internal static object CaptureTradeAuditState(RebirthNpcStableId npc,Guid operation)
    {
        if(!ThreadManager.IsMainThread()||npc.IsEmpty||operation==Guid.Empty||
            !RebirthNpcInventoryPersistenceStore.IsTransferLoaded)return new{schema=1,available=false};
        object epoch;lock(Sync)epoch=generationEpoch;
        bool saved=RebirthNpcInventoryPersistenceStore.CanRetireBacking;
        if(!RebirthNpcInventoryPersistenceStore.IsTransferLoaded)return new{schema=1,available=false};
        lock(Sync)
        {
            if(!ReferenceEquals(epoch,generationEpoch)||!States.TryGetValue(npc,out var state))return new{schema=1,available=false};
            return new{schema=1,available=true,npc=npc.ToString(),operation=operation.ToString("N"),revision=state.Revision,
                quantities=new Dictionary<string,int>(state.Quantities,StringComparer.OrdinalIgnoreCase),
                reservations=new Dictionary<string,int>(state.Reservations,StringComparer.OrdinalIgnoreCase),
                applied=state.AppliedTransactions.Contains(operation),savedBacking=saved};
        }
    }
}
