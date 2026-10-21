using System;
using System.Collections.Generic;

// Distinct private custody for completing only the unresolved metabolism leg.
// This is never an inventory-application grant and cannot replay an owner setter.
internal sealed partial class RebirthHydrationColdRecoveryLease
{
    private static readonly Dictionary<Guid,RebirthHydrationColdRecoveryLease> retained=new Dictionary<Guid,RebirthHydrationColdRecoveryLease>();
    internal readonly EntityPlayerLocal Player;internal readonly RebirthHydrationTransferOriginal Original;
    internal readonly RebirthMetabolismState State;private readonly RebirthHydrationColdOwnerWitness owner;
    private RebirthHydrationColdRecoveryLease(RebirthHydrationColdOwnerWitness owner,RebirthMetabolismState state)
    {this.owner=owner;Player=owner.Player;Original=owner.Original;State=state;}
    internal bool Owns(RebirthHydrationColdOwnerWitness witness)=>ReferenceEquals(owner,witness)&&
        retained.TryGetValue(Original.Operation,out var current)&&ReferenceEquals(current,this)&&
        RebirthHydrationOwnerHold.Matches(Player,Original)&&RebirthMetabolismStateRepository.TryGet(Player,out var state)&&ReferenceEquals(state,State);
    internal bool Current()
    {
        try{return Owns(owner)&&owner.CurrentForRecovery(this)&&SourceMatches(State,Original)&&
            RebirthHydrationTransferJournal.HasAttempted(Player,Original)&&Owns(owner)&&SourceMatches(State,Original);}
        catch{return false;}
    }
    internal static bool TryAcquire(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,out RebirthHydrationColdRecoveryLease lease)
    {
        lease=null;
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||original==null)return false;
            if(retained.TryGetValue(original.Operation,out var existing))
            {if(!ReferenceEquals(existing.Player,player)||existing.Original.Digest!=original.Digest||!existing.Current())return false;lease=existing;return true;}
            if(retained.Count>=64||!RebirthHydrationTransferJournal.HasAttempted(player,original)||
                !RebirthHydrationColdOwnerWitness.TryObserve(player,original,out var observed)||
                !RebirthMetabolismStateRepository.TryGet(player,out var state)||!SourceMatches(state,original))return false;
            var next=new RebirthHydrationColdRecoveryLease(observed,state);
            if(!RebirthHydrationOwnerHold.TryAcquireColdApplied(observed))return false;
            // Retain before any subsequent saved-leg IO can become uncertain.
            retained.Add(original.Operation,next);
            if(!next.Current())return false;lease=next;return true;
        }
        catch{return false;}
    }
    private static bool SourceMatches(RebirthMetabolismState state,RebirthHydrationTransferOriginal original)
    {
        if(state==null||!RebirthHydrationTransferScope.MatchesState(original.Creation,state)||!original.TryGetPlan(out var plan))return false;
        var receipt=state.HydrationTransferReceipt;
        if(receipt?.Operation==original.Operation&&receipt.Digest!=original.Digest)return false;
        var expected=receipt?.Digest==original.Digest?plan.GearAfter:plan.GearBefore;
        var item=state.HydrationSlotItem;int count=item==null||item.IsEmpty()?0:item.count;
        return count==expected.Count&&(count==0?string.Empty:RebirthNativeItemCodec.Encode(item.itemValue))==expected.ItemData;
    }
}
