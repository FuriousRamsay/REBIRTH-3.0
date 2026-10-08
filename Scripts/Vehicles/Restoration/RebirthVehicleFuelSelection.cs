using System;
using System.Collections.Generic;
using System.IO;

// Planned owner-transfer producer; not called by the live restoration Refuel path.
// Detached selection over a caller-authenticated owner snapshot. No live player
// references or changes; apply still validates exact payload, count, slot and locks.
public static class RebirthVehicleFuelSelection
{
    public static IReadOnlyList<RebirthVehicleTransferItem> Select(ItemStack[] toolbelt, int ownedToolbeltSlots,
        ItemStack[] backpack, Func<int, bool> isBackpackLocked, string nativeFuelItem, int requestedCount)
    {
        if (toolbelt == null || backpack == null || ownedToolbeltSlots < 0 || ownedToolbeltSlots > toolbelt.Length
            || string.IsNullOrEmpty(nativeFuelItem) || requestedCount < 1 || requestedCount > 2500)
            throw new InvalidDataException("Invalid owner fuel inventory observation.");
        var selected = new List<RebirthVehicleTransferItem>();
        Collect(toolbelt, ownedToolbeltSlots, null, nativeFuelItem, requestedCount,
            RebirthVehiclePartSourceLocation.Toolbelt, selected);
        // Matches the previously audited 3.2 takeFuel contract; installed-version parity
        // still requires revalidation. Any eligible toolbelt fuel prevents backpack use,
        // even if the toolbelt supplies less than the requested amount.
        if (selected.Count == 0)
            Collect(backpack, backpack.Length, isBackpackLocked, nativeFuelItem, requestedCount,
                RebirthVehiclePartSourceLocation.Backpack, selected);
        return selected.AsReadOnly();
    }

    private static void Collect(ItemStack[] slots, int owned, Func<int, bool> isLocked, string fuelItem,
        int requested, RebirthVehiclePartSourceLocation source, List<RebirthVehicleTransferItem> selected)
    {
        int remaining = requested;
        for (int i = 0; i < owned && remaining > 0; i++)
        {
            var stack = slots[i];
            if ((isLocked != null && isLocked(i)) || stack == null || stack.IsEmpty()
                || stack.itemValue?.ItemClass == null || stack.itemValue.ItemClass.GetItemName() != fuelItem) continue;
            int count = Math.Min(remaining, stack.count);
            selected.Add(new RebirthVehicleTransferItem(Guid.NewGuid(), true,
                RebirthNativeItemCodec.Encode(stack.itemValue), count, source, i));
            remaining -= count;
            if (selected.Count > 256) throw new InvalidDataException("Fuel selection exceeds transfer operation limit.");
        }
    }
}