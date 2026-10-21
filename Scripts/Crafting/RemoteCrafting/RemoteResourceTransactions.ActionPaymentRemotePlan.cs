using System;
using System.Linq;

public static partial class RemoteResourceTransactions
{
    private static int PlanActionRemoteOwner(EntityPlayer actor,ActionRemoteHostOriginal owner,ItemValue required,int needed,Plan plan)
    {
        if(!RemoteActionOriginalCurrent(owner)||!ReferenceEquals(owner.Actor,actor))return needed;
        foreach(bool belt in new[]{false,true})
        {
            var slots=belt?owner.BeltBefore:owner.BagBefore;
            int limit=belt?RebirthToolbeltCapacity.GetOwnedSlotCount(actor,slots.Length):slots.Length;
            for(int slot=0;slot<limit&&needed>0;slot++)
            {
                var stack=slots[slot];if(!MatchesConsumable(stack,required))continue;
                byte[] item=RebirthResourceItemFingerprint.Capture(stack.itemValue);if(item==null)return needed;
                int count=Math.Min(needed,stack.count);plan.Local.Add(new LocalTake{Toolbelt=belt,Slot=slot,Count=count,ItemType=stack.itemValue.type,OriginalCount=stack.count,ItemPayload=item});needed-=count;
            }
        }
        return needed;
    }
    private static bool ValidateActionPlan(EntityPlayer actor,Plan plan,ActionRemoteHostOriginal owner,out string failure)
    {
        if(owner==null)return Validate(actor,plan,out failure);
        failure="Actual saved remote owner original is unavailable.";
        if(!RemoteActionOriginalCurrent(owner)||!ReferenceEquals(owner.Actor,actor))return false;
        foreach(var take in plan.Local)
        {
            var slots=take.Toolbelt?owner.BeltBefore:owner.BagBefore;
            if(take.Slot<0||take.Slot>=slots.Length||take.Count<1||take.Count>slots[take.Slot].count||slots[take.Slot].count!=take.OriginalCount||
                slots[take.Slot].itemValue.type!=take.ItemType||!RebirthResourceItemFingerprint.Matches(slots[take.Slot].itemValue,take.ItemPayload))return false;
        }
        // Existing native non-owner validation remains exact. Its local branch
        // cannot run against a remote player's server-side inventory replica.
        var native=new Plan();native.Remote.AddRange(plan.Remote);foreach(var id in plan.SourceIds)native.SourceIds.Add(id);
        return Validate(actor,native,out failure);
    }
}
