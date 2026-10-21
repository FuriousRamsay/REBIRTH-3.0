using System;

public enum RebirthVehicleOwnerTransferResult { Pending, Applied, Rejected, Indeterminate }

// Only invoke for an authenticated server offer and matching live character/session.
// Applied means locally applied; server must verify saved native player data before
// committing its assembly. Receipt 2 deliberately prevents retrying an uncertain write.
public static class RebirthVehicleOwnerTransfer
{
    public static RebirthVehicleOwnerTransferResult Apply(EntityPlayerLocal player, Guid currentCreation,
        Guid offeredCreation, RebirthVehicleTransferItem operation, Func<bool> isCurrentSession)
        =>Apply(player,currentCreation.ToString("N"),offeredCreation.ToString("N"),operation,isCurrentSession);
    public static RebirthVehicleOwnerTransferResult Apply(EntityPlayerLocal player, string currentCreation,
        string offeredCreation, RebirthVehicleTransferItem operation, Func<bool> isCurrentSession)
    {
        if (player == null || player.Buffs == null || operation == null || !RebirthVehicleOwnerCustody.CreationMatches(player,currentCreation,offeredCreation,new[]{operation})
            || isCurrentSession == null || !isCurrentSession()) return RebirthVehicleOwnerTransferResult.Pending;
        string receipt = "rbVehicle_" + operation.ReceiptId.ToString("N");
        float previous = player.Buffs.GetCustomVar(receipt);
        RebirthVehicleOwnerCustody.TryGetPlan(player,new[]{operation},out var boundPlan);
        if(previous==2f&&boundPlan!=null)
        {
            if(!RebirthVehicleOwnerOriginal.ResolveApplying(player,boundPlan,new[]{operation},out var retry))return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!retry)return RebirthVehicleOwnerTransferResult.Applied;
            previous=0f; // Exact positive saved/live original Before, never a timeout.
        }
        if(previous==1f&&boundPlan!=null)return RebirthVehicleOwnerOriginal.ResolveApplying(player,boundPlan,new[]{operation},out var completedRetry)&&!completedRetry?
            RebirthVehicleOwnerTransferResult.Applied:RebirthVehicleOwnerTransferResult.Indeterminate;
        if (previous == 1f) return RebirthVehicleOwnerCheckpoint.Persist(player,new[]{operation.ReceiptId},1f,isCurrentSession)?RebirthVehicleOwnerTransferResult.Applied:RebirthVehicleOwnerTransferResult.Indeterminate;
        if (previous == -1f) return RebirthVehicleOwnerCheckpoint.Persist(player,new[]{operation.ReceiptId},-1f,isCurrentSession)?RebirthVehicleOwnerTransferResult.Rejected:RebirthVehicleOwnerTransferResult.Indeterminate;
        if (previous != 0f) return RebirthVehicleOwnerTransferResult.Indeterminate;
        if (!player.IsSpawned() || player.IsDead() || !RebirthSurvivorMode.IsEnabledForCurrentWorld()&&boundPlan?.NativeDeaths!=player.Died)
            return RebirthVehicleOwnerTransferResult.Pending;

        bool bag = !operation.Debit || operation.Source == RebirthVehiclePartSourceLocation.Backpack;
        if(!RebirthVehicleInventoryWrite.CanMutate(player,bag))return RebirthVehicleOwnerTransferResult.Pending;
        var slots = bag ? player.bag?.ItemGrid.items : player.inventory?.ItemGrid.items;
        if (slots == null) return RebirthVehicleOwnerTransferResult.Pending;
        var beforeBag=RebirthVehicleInventoryWrite.Snapshot(player.bag.ItemGrid.items);var beforeBelt=RebirthVehicleInventoryWrite.Snapshot(player.inventory.ItemGrid.items);
        bool FullBefore()=>RebirthVehicleInventoryWrite.SameSlots(beforeBag,player.bag?.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(beforeBelt,player.inventory?.ItemGrid.items);
        int owned = bag ? slots.Length : RebirthToolbeltCapacity.GetOwnedSlotCount(player, slots.Length);
        ItemStack[] candidate;
        var prepared = RebirthVehicleOwnerInventoryPlan.Prepare(slots, operation, owned,
            i => RebirthVehicleNativePolicy.IsLocked(player,bag,i), out candidate);
        if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Pending;
        if (prepared == RebirthVehicleInventoryPlanResult.Pending) return RebirthVehicleOwnerTransferResult.Pending;
        try
        {
            if(!FullBefore())return RebirthVehicleOwnerTransferResult.Indeterminate;
            if (prepared == RebirthVehicleInventoryPlanResult.Rejected)
            {
                player.Buffs.SetCustomVar(receipt, -1f, true);
                return FullBefore()&&player.Buffs.GetCustomVar(receipt)==-1f&&RebirthVehicleOwnerCheckpoint.Persist(player,new[]{operation.ReceiptId},-1f,isCurrentSession) ? RebirthVehicleOwnerTransferResult.Rejected : RebirthVehicleOwnerTransferResult.Indeterminate;
            }
            // Native setters call listeners after changing inventory. If a listener
            // throws, repeating this operation could duplicate/debit items twice.
            var expected=RebirthVehicleInventoryWrite.Snapshot(candidate);
            var expectedBag=bag?expected:beforeBag;var expectedBelt=bag?beforeBelt:expected;
            if(!FullBefore())return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(boundPlan!=null&&!RebirthVehicleOwnerOriginal.Prepare(player,boundPlan,new[]{operation},bag,candidate))return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!FullBefore()||!isCurrentSession())return RebirthVehicleOwnerTransferResult.Indeterminate;
            player.Buffs.SetCustomVar(receipt, 2f, true);
            if(!RebirthVehicleOwnerCheckpoint.Persist(player,new[]{operation.ReceiptId},2f,isCurrentSession))return RebirthVehicleOwnerTransferResult.Indeterminate;
            if (!isCurrentSession() || player.Buffs.GetCustomVar(receipt)!=2f || !RebirthVehicleInventoryWrite.CanMutate(player,bag) ||
                !FullBefore())
                return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(RebirthVehicleOwnerCustody.NativeCanEnter(player,bag))RebirthVehicleOwnerCustody.WriteSlots(player,bag,candidate,new[]{operation},isCurrentSession);
            else if (bag) player.bag.SetSlots(candidate);
            else player.inventory.SetItem(operation.Slot, candidate[operation.Slot]);
            if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Indeterminate;
            if(!RebirthVehicleInventoryWrite.SameSlots(expectedBag,player.bag?.ItemGrid.items)||!RebirthVehicleInventoryWrite.SameSlots(expectedBelt,player.inventory?.ItemGrid.items))return RebirthVehicleOwnerTransferResult.Indeterminate;
            player.Buffs.SetCustomVar(receipt, 1f, true);
            return RebirthVehicleInventoryWrite.SameSlots(expectedBag,player.bag?.ItemGrid.items)&&RebirthVehicleInventoryWrite.SameSlots(expectedBelt,player.inventory?.ItemGrid.items)&&
                player.Buffs.GetCustomVar(receipt)==1f&&RebirthVehicleOwnerCheckpoint.Persist(player,new[]{operation.ReceiptId},1f,isCurrentSession) ? RebirthVehicleOwnerTransferResult.Applied : RebirthVehicleOwnerTransferResult.Indeterminate;
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Vehicle] owner transfer requires reconciliation receipt=" + operation.ReceiptId.ToString("N")
                + " " + ex.GetType().Name + ": " + ex.Message);
            return RebirthVehicleOwnerTransferResult.Indeterminate;
        }
    }
}
