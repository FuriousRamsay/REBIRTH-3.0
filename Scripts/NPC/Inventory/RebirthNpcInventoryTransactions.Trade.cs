using System;
using System.Collections.Generic;
using System.Threading;

public static partial class RebirthNpcInventoryTransactionService
{
    // This entry cannot be called with a quote, image or runtime phase alone.
    // Its observation comes from the actual owner transport and native save.
    internal static RebirthNpcInventoryTransactionResult ApplyTradeOwnerOriginal(
        RebirthNpcTradeService.OwnerAdmission.SavedAfter observation,out uint revision)
    {
        revision=0;
        if(!ThreadManager.IsMainThread()||observation==null||RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||!observation.Current())
            return RebirthNpcInventoryTransactionResult.AuthorityDenied;
        var prepared=observation.Grant.Prepared;var original=prepared.Original;
        var plan=prepared.NpcPlan;var npc=original.CopyQuote().NpcId;
        if(!HasInventoryCapability(npc)||!RebirthNpcTradeService.BlocksOwnerPreparedNpc(npc))
            return RebirthNpcInventoryTransactionResult.AuthorityDenied;
        lock(Sync)
        {
            if(!States.TryGetValue(npc,out var state))return RebirthNpcInventoryTransactionResult.NotFound;
            revision=state.Revision;
            if(state.AppliedTransactions.Contains(original.Operation))
                return plan.Matches(GetSnapshot(npc),true)?RebirthNpcInventoryTransactionResult.Replayed:
                    RebirthNpcInventoryTransactionResult.RevisionConflict;
            if(!plan.Matches(GetSnapshot(npc),false)||state.Reservations.Count!=0||
                RebirthNpcInventoryPersistenceStore.CaptureTradeSaved(original,plan,false)==null||!observation.Current())
                return RebirthNpcInventoryTransactionResult.RevisionConflict;
            var after=plan.CopySnapshot(true);
            // Assemble the complete replacement before publication. Both trade
            // legs, native rich-stock revision and replay ID publish together.
            var next=new InventoryState{Revision=after.Revision,NativeStacks=after.NativeStacks};
            foreach(var pair in after.Quantities)next.Quantities.Add(pair.Key,pair.Value);
            foreach(var pair in after.Reservations)next.Reservations.Add(pair.Key,pair.Value);
            if(after.LogisticsEffects!=null)foreach(var pair in after.LogisticsEffects)next.LogisticsEffects.Add(pair.Key,pair.Value);
            foreach(var id in state.AppliedTransactionOrder)
            {next.AppliedTransactionOrder.Enqueue(id);next.AppliedTransactions.Add(id);}
            RememberTransaction(next,original.Operation);
            if(!observation.Current()||!plan.Matches(GetSnapshot(npc),false))return RebirthNpcInventoryTransactionResult.RevisionConflict;
            generationEpoch=new object();States[npc]=next;
            RebirthNpcInventoryPersistenceStore.MarkDirty();revision=next.Revision;
            Interlocked.Increment(ref applied);
            return RebirthNpcInventoryTransactionResult.Applied;
        }
    }
}
