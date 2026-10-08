using System;

public enum RebirthVehicleInventoryPlanResult { Ready, Pending, Rejected }

// Pure preparation: live slots are never edited, even when a multi-stack credit
// cannot fit. Owner application/receipt publication belongs to the coordinator.
public static class RebirthVehicleOwnerInventoryPlan
{
    public static RebirthVehicleInventoryPlanResult Prepare(ItemStack[] live,
        RebirthVehicleTransferItem operation, int ownedSlots, Func<int, bool> isLocked, out ItemStack[] candidate)
    {
        candidate = null;
        if (live == null || operation == null || ownedSlots < 0 || ownedSlots > live.Length)
            return RebirthVehicleInventoryPlanResult.Pending;
        ItemValue value;
        if (!RebirthNativeItemCodec.TryDecode(operation.ItemData, out value))
            return RebirthVehicleInventoryPlanResult.Rejected;
        if (operation.Debit)
        {
            int slot = operation.Slot;
            if (slot < 0 || slot >= ownedSlots || (isLocked != null && isLocked(slot)))
                return RebirthVehicleInventoryPlanResult.Rejected;
            var source = live[slot];
            if (source == null || source.IsEmpty() || source.count < operation.Count
                || RebirthNativeItemCodec.Encode(source.itemValue) != operation.ItemData)
                return RebirthVehicleInventoryPlanResult.Rejected;
            var copy = Clone(live);
            copy[slot].count -= operation.Count;
            if (copy[slot].count == 0) copy[slot] = ItemStack.Empty.Clone();
            candidate = copy;
            return RebirthVehicleInventoryPlanResult.Ready;
        }
        var incoming = new ItemStack(value, operation.Count);
        if (!incoming.CanMoveTo(XUiC_ItemStack.StackLocationTypes.Backpack))
            return RebirthVehicleInventoryPlanResult.Rejected;
        int maximum = Math.Min(ushort.MaxValue, value.ItemClass.MaxCount);
        if (maximum <= 0) return RebirthVehicleInventoryPlanResult.Rejected;
        var proposed = Clone(live);
        int remaining = operation.Count;
        for (int i = 0; i < ownedSlots && remaining > 0; i++)
        {
            var slot = proposed[i];
            if (slot.IsEmpty() || RebirthNativeItemCodec.Encode(slot.itemValue) != operation.ItemData) continue;
            int available;
            if (!slot.CanStackPartlyWith(incoming, out available)) continue;
            int added = Math.Min(remaining, Math.Min(available, Math.Max(0, maximum - slot.count)));
            slot.count += added;
            remaining -= added;
        }
        for (int i = 0; i < ownedSlots && remaining > 0; i++)
        {
            if (!proposed[i].IsEmpty()) continue;
            int added = Math.Min(remaining, maximum);
            proposed[i] = new ItemStack(value.Clone(), added);
            remaining -= added;
        }
        if (remaining > 0) return RebirthVehicleInventoryPlanResult.Pending;
        candidate = proposed;
        return RebirthVehicleInventoryPlanResult.Ready;
    }

    private static ItemStack[] Clone(ItemStack[] source)
    {
        var result = new ItemStack[source.Length];
        for (int i = 0; i < source.Length; i++) result[i] = source[i]?.Clone() ?? ItemStack.Empty.Clone();
        return result;
    }
}
