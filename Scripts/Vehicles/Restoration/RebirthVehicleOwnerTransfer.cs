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
        if (player == null || player.Buffs == null || operation == null || !RebirthSurvivorRequestScope.Matches(offeredCreation,currentCreation)
            || isCurrentSession == null || !isCurrentSession()) return RebirthVehicleOwnerTransferResult.Pending;
        string receipt = "rbVehicle_" + operation.ReceiptId.ToString("N");
        float previous = player.Buffs.GetCustomVar(receipt);
        if (previous == 1f) return RebirthVehicleOwnerTransferResult.Applied;
        if (previous == -1f) return RebirthVehicleOwnerTransferResult.Rejected;
        if (previous != 0f) return RebirthVehicleOwnerTransferResult.Indeterminate;
        if (!player.IsSpawned() || player.IsDead() || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return RebirthVehicleOwnerTransferResult.Pending;

        bool bag = !operation.Debit || operation.Source == RebirthVehiclePartSourceLocation.Backpack;
        var slots = bag ? player.bag?.ItemGrid.items : player.inventory?.ItemGrid.items;
        if (slots == null) return RebirthVehicleOwnerTransferResult.Pending;
        int owned = bag ? slots.Length : RebirthToolbeltCapacity.GetOwnedSlotCount(player, slots.Length);
        var locks = bag ? player.bag.LockedSlots : null;
        ItemStack[] candidate;
        var prepared = RebirthVehicleOwnerInventoryPlan.Prepare(slots, operation, owned,
            i => locks != null && i < locks.Length && locks[i], out candidate);
        if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Pending;
        if (prepared == RebirthVehicleInventoryPlanResult.Pending) return RebirthVehicleOwnerTransferResult.Pending;
        try
        {
            if (prepared == RebirthVehicleInventoryPlanResult.Rejected)
            {
                player.Buffs.SetCustomVar(receipt, -1f, true);
                return RebirthVehicleOwnerTransferResult.Rejected;
            }
            // Native setters call listeners after changing inventory. If a listener
            // throws, repeating this operation could duplicate/debit items twice.
            player.Buffs.SetCustomVar(receipt, 2f, true);
            if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Indeterminate;
            if (bag) player.bag.SetSlots(candidate);
            else player.inventory.SetItem(operation.Slot, candidate[operation.Slot]);
            if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Indeterminate;
            player.Buffs.SetCustomVar(receipt, 1f, true);
            return RebirthVehicleOwnerTransferResult.Applied;
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Vehicle] owner transfer requires reconciliation receipt=" + operation.ReceiptId.ToString("N")
                + " " + ex.GetType().Name + ": " + ex.Message);
            return RebirthVehicleOwnerTransferResult.Indeterminate;
        }
    }
}
