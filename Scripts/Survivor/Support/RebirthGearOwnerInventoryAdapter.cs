using System;
using System.Collections.Generic;
using System.IO;

// Only the authenticated owner dispatcher may call this with an active inventory
// interaction hold. No offer reception, hold acquisition or network ACK occurs here.
internal static class RebirthGearOwnerInventoryAdapter
{
    internal static RebirthGearOwnerApplySequence.Result Apply(EntityPlayerLocal player,
        RebirthGearTransferState offer, object authenticatedSession, Func<bool> isCurrent)
    {
        if (player == null || player.Buffs == null || isCurrent == null || !isCurrent() ||
            offer == null || !RebirthGearTransferState.TryRead(offer.ToXml(), out var validated) ||
            !validated.TryGetPlan(out var plan)) return RebirthGearOwnerApplySequence.Result.Conflict;
        offer = validated;
        var externalCurrent = isCurrent;
        isCurrent = () => externalCurrent() && RebirthGearOwnerReservation.Matches(player, authenticatedSession, validated);
        if (!isCurrent()) return RebirthGearOwnerApplySequence.Result.NeedsReconciliation;
        string receipt = "rbGear_" + offer.TransactionId;
        float previous = player.Buffs.GetCustomVar(receipt);
        if (previous == 1f)
            return RebirthGearOwnerSaveCheckpoint.TryPersist(player, offer, RebirthGearOwnerReceipt.Applied, isCurrent)
                ? RebirthGearOwnerApplySequence.Result.Applied : RebirthGearOwnerApplySequence.Result.Pending;
        if (previous != 0f && previous != 2f) return RebirthGearOwnerApplySequence.Result.NeedsReconciliation;
        bool applying = previous == 2f;
        try
        {
            // Decode every planned write before acquiring durable intent. An invalid
            // native item must never leave the inventory partially overwritten.
            var writes = new Dictionary<int, ItemStack>();
            foreach (var change in plan.Changes)
            {
                ItemStack stack;
                if (change.After.Count == 0) stack = ItemStack.Empty.Clone();
                else
                {
                    if (!RebirthNativeItemCodec.TryDecode(change.After.ItemData, out var value) ||
                        value == null || value.IsEmpty() || value.ItemClass == null)
                        return applying ? RebirthGearOwnerApplySequence.Result.NeedsReconciliation :
                            RebirthGearOwnerApplySequence.Result.Conflict;
                    stack = new ItemStack(value, change.After.Count);
                }
                writes.Add(change.Index + (change.IsBag ? 0 : 169), stack);
            }
            Func<RebirthGearInventorySnapshot> read = () =>
            {
                if (!isCurrent() || !RebirthGearInventorySnapshot.TryCapture(player.bag?.ItemGrid?.items,
                    player.inventory?.ItemGrid?.items, 4, out var snapshot)) throw new IOException("Gear inventory binding changed.");
                return snapshot;
            };
            return RebirthGearOwnerApplySequence.Execute(plan, applying, isCurrent,
                () => read().Bag, () => read().Belt,
                () => RebirthGearOwnerSaveCheckpoint.TryPersist(player, offer, RebirthGearOwnerReceipt.Applying, isCurrent),
                () =>
                {
                    if (!isCurrent()) throw new IOException("Gear owner changed before expansion.");
                    var existing = player.bag.ItemGrid.items;
                    int maximum = Math.Max(plan.BagSlotsBefore, plan.BagSlotsAfter);
                    if (existing.Length == maximum) return;
                    if (existing.Length != plan.BagSlotsBefore || existing.Length > maximum)
                        throw new IOException("Unexpected gear backpack backing.");
                    var expanded = new ItemStack[maximum];
                    for (int i = 0; i < expanded.Length; i++)
                        expanded[i] = i < existing.Length ? existing[i]?.Clone() ?? ItemStack.Empty.Clone() : ItemStack.Empty.Clone();
                    RebirthGearNativeWritePermit.Execute(RebirthGearNativeWritePermit.Kind.BagBacking,
                        player.bag, -1, expanded, expanded.Length, isCurrent, () => player.bag.SetSlots(expanded));
                    // Belt backing remains native; usable capacity is projected later.
                },
                change =>
                {
                    if (!isCurrent()) throw new IOException("Gear owner changed before slot write.");
                    var stack = writes[change.Index + (change.IsBag ? 0 : 169)].Clone();
                    if (change.IsBag)
                        RebirthGearNativeWritePermit.Execute(RebirthGearNativeWritePermit.Kind.BagSlot,
                            player.bag, change.Index, stack, stack.count, isCurrent,
                            () => player.bag.SetSlot(change.Index, stack));
                    else
                        RebirthGearNativeWritePermit.Execute(RebirthGearNativeWritePermit.Kind.BeltSlot,
                            player.inventory, change.Index, stack.itemValue, stack.count, isCurrent,
                            () => player.inventory.SetItem(change.Index, stack.itemValue, stack.count));
                },
                () => RebirthGearOwnerSaveCheckpoint.TryPersist(player, offer, RebirthGearOwnerReceipt.Applied, isCurrent));
        }
        catch { return RebirthGearOwnerApplySequence.Result.Pending; }
    }
}