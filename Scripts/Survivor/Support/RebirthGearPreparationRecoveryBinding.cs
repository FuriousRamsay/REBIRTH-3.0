using System;
using Stack=RebirthGearInventoryPlan.Stack;

// Pure comparison only. Caller must independently authenticate the original
// final-file receipt/world/owner and continuously hold inventory interactions.
public static class RebirthGearPreparationRecoveryBinding
{
    public static bool TryRecoverOriginal(RebirthGearPreparationIntent intent,RebirthGearTransferState offer,
        RebirthGearInventorySnapshot current,Guid savedWorld,int originalOwned,float savedReceipt,
        out RebirthGearInventorySnapshot original)
    {
        original=null;
        if(intent==null||offer==null||current==null||
            (savedReceipt!=0f&&savedReceipt!=-1f&&savedReceipt!=1f&&savedReceipt!=2f)||
            !RebirthGearTransferState.TryRead(offer.ToXml(),out var validated)||!validated.TryGetPlan(out var plan)||
            (validated.HasRecoveryAttempts&&savedReceipt!=1f))return false;
        try
        {
            int maximumBag=Math.Max(plan.BagSlotsBefore,plan.BagSlotsAfter),maximumBelt=Math.Max(plan.BeltSlotsBefore,plan.BeltSlotsAfter);
            if((current.Bag.Length!=plan.BagSlotsBefore&&current.Bag.Length!=maximumBag)||(current.Belt.Length!=plan.BeltSlotsBefore&&current.Belt.Length!=maximumBelt)||
                ((savedReceipt==0f||savedReceipt==-1f)&&(current.Bag.Length!=plan.BagSlotsBefore||current.Belt.Length!=plan.BeltSlotsBefore))||
                !RebirthGearEncodedSnapshot.TryCopy(current.Bag,current.Belt,originalOwned,out var bag,out var belt))return false;
            var expanded=new Stack[maximumBag];
            for(int i=0;i<expanded.Length;i++)expanded[i]=i<bag.Length?bag[i]:new Stack();
            var expandedBelt=new Stack[maximumBelt];
            for(int i=0;i<expandedBelt.Length;i++)expandedBelt[i]=i<belt.Length?belt[i]:new Stack();
            bool applying=savedReceipt==1f||savedReceipt==2f;
            if(plan.InspectApplication(expanded,expandedBelt,applying)==RebirthGearInventoryPlan.ApplicationState.Conflict||
                (savedReceipt==1f&&!plan.MatchesAppliedInventory(expanded,expandedBelt)))return false;
            var beforeBag=new Stack[plan.BagSlotsBefore];var beforeBelt=new Stack[plan.BeltSlotsBefore];
            Array.Copy(bag,beforeBag,beforeBag.Length);Array.Copy(belt,beforeBelt,beforeBelt.Length);
            foreach(var change in plan.Changes)
            {
                var slots=change.IsBag?beforeBag:beforeBelt;
                if(change.Index<slots.Length)slots[change.Index]=new Stack{ItemData=change.Before.ItemData,Count=change.Before.Count};
            }
            if(!RebirthGearInventorySnapshot.TryCaptureEncoded(beforeBag,beforeBelt,originalOwned,out var recovered))return false;
            // Compare the original operation without erasing/replacing retained
            // recovery provenance on the caller's immutable offer.
            if(!RebirthGearTransferState.TryCreate(validated.TransactionId,validated.CreationId,validated.SlotId,
                validated.ExpectedRevision,plan,out var operation)||
                !operation.TryBindPreparationRequest(validated.PreparationRequestDigest,out var bound)||
                !RebirthGearPreparationOfferBinding.MatchesBound(intent,recovered,bound,savedWorld))return false;
            original=recovered;return true;
        }
        catch{return false;}
    }
}