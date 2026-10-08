using System;
using System.Collections.Generic;
using System.Linq;

// One native inventory replacement after ALL exact debits validate. Local success
// still requires native player save and independent saved-receipt verification.
public static class RebirthVehicleOwnerDebitBatch
{
    public static RebirthVehicleOwnerTransferResult Apply(EntityPlayerLocal player, Guid currentCreation,
        Guid offeredCreation, IEnumerable<RebirthVehicleTransferItem> operations, Func<bool> isCurrentSession)
        =>Apply(player,currentCreation.ToString("N"),offeredCreation.ToString("N"),operations,isCurrentSession);
    public static RebirthVehicleOwnerTransferResult Apply(EntityPlayerLocal player, string currentCreation,
        string offeredCreation, IEnumerable<RebirthVehicleTransferItem> operations, Func<bool> isCurrentSession)
    {
        if (player?.Buffs == null || !RebirthSurvivorRequestScope.Matches(offeredCreation,currentCreation)
            || operations == null || isCurrentSession == null || !isCurrentSession())
            return RebirthVehicleOwnerTransferResult.Pending;
        var items = operations.Take(257).ToArray();
        if (items.Length == 0 || items.Length > 256 || items.Any(i => i == null || !i.Debit)
            || items.Select(i => i.Source).Distinct().Count() != 1
            || items.Select(i => i.Slot).Distinct().Count() != items.Length
            || items.Select(i => i.ReceiptId).Distinct().Count() != items.Length)
            return RebirthVehicleOwnerTransferResult.Indeterminate;
        var keys = items.Select(i => "rbVehicle_" + i.ReceiptId.ToString("N")).ToArray();
        var previous = keys.Select(player.Buffs.GetCustomVar).ToArray();
        if (previous.All(v => v == 1f)) return RebirthVehicleOwnerTransferResult.Applied;
        if (previous.All(v => v == -1f)) return RebirthVehicleOwnerTransferResult.Rejected;
        if (previous.Any(v => v != 0f)) return RebirthVehicleOwnerTransferResult.Indeterminate;
        if (!player.IsSpawned() || player.IsDead() || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return RebirthVehicleOwnerTransferResult.Pending;
        bool bag = items[0].Source == RebirthVehiclePartSourceLocation.Backpack;
        var live = bag ? player.bag?.ItemGrid.items : player.inventory?.ItemGrid.items;
        if (live == null) return RebirthVehicleOwnerTransferResult.Pending;
        int owned = bag ? live.Length : RebirthToolbeltCapacity.GetOwnedSlotCount(player, live.Length);
        if (owned < 0 || owned > live.Length) return RebirthVehicleOwnerTransferResult.Pending;
        var locks = bag ? player.bag.LockedSlots : null;
        bool valid = true;
        foreach (var item in items)
        {
            int slot = item.Slot;
            if (slot < 0 || slot >= owned || (locks != null && slot < locks.Length && locks[slot])
                || live[slot] == null || live[slot].IsEmpty() || live[slot].count < item.Count
                || RebirthNativeItemCodec.Encode(live[slot].itemValue) != item.ItemData)
            { valid = false; break; }
        }
        ItemStack[] candidate = null;
        if (valid)
        {
            candidate = new ItemStack[live.Length];
            for (int i = 0; i < live.Length; i++) candidate[i] = live[i]?.Clone() ?? ItemStack.Empty.Clone();
            foreach (var item in items)
            {
                candidate[item.Slot].count -= item.Count;
                if (candidate[item.Slot].count == 0) candidate[item.Slot] = ItemStack.Empty.Clone();
            }
        }
        if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Pending;
        try
        {
            foreach (string key in keys) player.Buffs.SetCustomVar(key, valid ? 2f : -1f, true);
            if (!valid) return RebirthVehicleOwnerTransferResult.Rejected;
            if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Indeterminate;
            // Inventory.SetSlots can throw after partially applying toolbelt slots;
            // every receipt is already uncertain, so no automatic retry can debit again.
            if (bag) player.bag.SetSlots(candidate);
            else player.inventory.SetSlots(candidate);
            if (!isCurrentSession()) return RebirthVehicleOwnerTransferResult.Indeterminate;
            foreach (string key in keys) player.Buffs.SetCustomVar(key, 1f, true);
            return RebirthVehicleOwnerTransferResult.Applied;
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Vehicle] owner debit batch requires reconciliation " + ex.GetType().Name + ": " + ex.Message);
            return RebirthVehicleOwnerTransferResult.Indeterminate;
        }
    }
}