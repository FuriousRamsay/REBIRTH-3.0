using System;

// Apply only a privately issued warm grant under its exact original hold.
// Inventory outcome remains separate from joint saved metabolism settlement.
internal static partial class RebirthHydrationOwnerApply
{
    internal static RebirthGearOwnerApplySequence.Result Apply(RebirthHydrationTransferGrant grant)
    {
        if(grant==null)return RebirthGearOwnerApplySequence.Result.NeedsReconciliation;
        return ApplyHeld(grant.Player,grant.Original,grant.Current);
    }
    private static RebirthGearOwnerApplySequence.Result ApplyHeld(EntityPlayerLocal player,RebirthHydrationTransferOriginal original,Func<bool> current)
    {
        if(!current()||!original.TryGetPlan(out var plan))return RebirthGearOwnerApplySequence.Result.NeedsReconciliation;
        try
        {
            float receipt=player.Buffs.GetCustomVar("rbGear_"+original.Operation.ToString("N"));
            if(receipt==1f)return RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applied)?
                RebirthGearOwnerApplySequence.Result.Applied:RebirthGearOwnerApplySequence.Result.Pending;
            // The grant was issued only from actual saved applying/Before. A
            // missing or malformed live receipt is unknown, never fresh work.
            if(receipt!=2f)return RebirthGearOwnerApplySequence.Result.NeedsReconciliation;
            var change=plan.Changes[0];ItemStack desired;
            if(change.After.Count==0)desired=ItemStack.Empty.Clone();
            else
            {
                if(!RebirthNativeItemCodec.TryDecode(change.After.ItemData,out var item)||item==null||item.IsEmpty()||item.ItemClass==null)
                    return RebirthGearOwnerApplySequence.Result.NeedsReconciliation;
                desired=new ItemStack(item,change.After.Count);
            }
            Func<RebirthGearInventorySnapshot> read=()=>
            {
                if(!current()||!RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,player.inventory?.ItemGrid?.items,
                    original.OwnedBelt,out var snapshot)||!current())throw new InvalidOperationException("Original hydration native scope changed");
                return snapshot;
            };
            return RebirthGearOwnerApplySequence.Execute(plan,true,current,()=>read().Bag,()=>read().Belt,
                ()=>RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying),
                ()=>{if(!current()||player.bag.ItemGrid.items.Length!=plan.BagSlotsBefore||player.inventory.ItemGrid.items.Length!=plan.BeltSlotsBefore)
                    throw new InvalidOperationException("Hydration never changes native backing capacity");},
                write=>
                {
                    if(!current())throw new InvalidOperationException("Hydration original lost current permission");
                    var stack=desired.Clone();
                    if(write.IsBag)RebirthGearNativeWritePermit.Execute(RebirthGearNativeWritePermit.Kind.BagSlot,player.bag,write.Index,stack,stack.count,
                        current,()=>player.bag.SetSlot(write.Index,stack));
                    else RebirthGearNativeWritePermit.Execute(RebirthGearNativeWritePermit.Kind.BeltSlot,player.inventory,write.Index,stack.itemValue,stack.count,
                        current,()=>player.inventory.SetItem(write.Index,stack.itemValue,stack.count));
                },()=>RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applied));
        }
        catch{return RebirthGearOwnerApplySequence.Result.Pending;}
    }
}

