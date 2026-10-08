using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Detached plans only: caller authenticates actor/session/access, persists custody,
// reserves the exact before snapshot and obtains saved owner receipts before apply.
public static class RebirthVehicleTransferPlanner
{
    public static RebirthVehicleTransferPlan Repair(Guid transactionId, Guid creationId,
        string actorId, string fingerprint, RebirthVehicleTransferTarget target,
        RebirthVehicleAssembly before, long expectedRevision, EntityPlayer authoritativePlayer,
        int nativeHealth, int nativeMaximumHealth, RebirthVehicleTransferItem repairKitDebit)
        =>Repair(transactionId,creationId.ToString("N"),actorId,fingerprint,target,before,expectedRevision,authoritativePlayer,nativeHealth,nativeMaximumHealth,repairKitDebit);
    public static RebirthVehicleTransferPlan Repair(Guid transactionId, string creationId,
        string actorId, string fingerprint, RebirthVehicleTransferTarget target,
        RebirthVehicleAssembly before, long expectedRevision, EntityPlayer authoritativePlayer,
        int nativeHealth, int nativeMaximumHealth, RebirthVehicleTransferItem repairKitDebit)
    {
        if (target == null || target.Carrier != RebirthVehicleAssemblyCarrier.VehicleEntity || before == null
            || before.Revision != expectedRevision || before.PendingInventoryTransferId != Guid.Empty
            || authoritativePlayer == null || nativeHealth <= 0 || nativeMaximumHealth <= nativeHealth
            || repairKitDebit == null || !repairKitDebit.Debit || repairKitDebit.Count != 1)
            throw new InvalidDataException("Repair requires an available entity revision, live health and one exact kit debit.");
        ItemValue kit;
        if (!RebirthNativeItemCodec.TryDecode(repairKitDebit.ItemData, out kit)
            || kit.ItemClass == null || kit.ItemClass.GetItemName() != "resourceRepairKit")
            throw new InvalidDataException("Repair debit is not a native repair kit.");
        int amount = RebirthServiceCraftSkillService.AdjustVehicleRepairAmount(authoritativePlayer, 2500);
        if (amount <= 0) throw new InvalidDataException("Authoritative repair gain is not positive.");
        int afterHealth = (int)Math.Min((long)nativeMaximumHealth, (long)nativeHealth + amount);
        var health = new RebirthVehicleTransferHealth(nativeHealth, afterHealth, nativeMaximumHealth);
        var after = before.DeepClone();
        if (after.Revision == long.MaxValue) throw new InvalidDataException("Vehicle revision is exhausted.");
        after.Revision++; after.LastAppliedInventoryTransferId = transactionId;
        return RebirthVehicleTransferPlan.Create(transactionId, creationId, actorId, fingerprint,
            before, after, new[] { repairKitDebit }, target, null, health);
    }

    // Exact debit slots come from an authenticated owner snapshot. The selector
    // must preserve native toolbelt-first, backpack-only-if-zero selection policy.
    public static RebirthVehicleTransferPlan Refuel(Guid transactionId, Guid creationId,
        string actorId, string fingerprint, RebirthVehicleTransferTarget target,
        RebirthVehicleAssembly before, long expectedRevision, string nativeFuelItem,
        float nativeFuelLevel, float nativeFuelCapacity, IEnumerable<RebirthVehicleTransferItem> selectedDebits)
        =>Refuel(transactionId,creationId.ToString("N"),actorId,fingerprint,target,before,expectedRevision,nativeFuelItem,nativeFuelLevel,nativeFuelCapacity,selectedDebits);
    public static RebirthVehicleTransferPlan Refuel(Guid transactionId, string creationId,
        string actorId, string fingerprint, RebirthVehicleTransferTarget target,
        RebirthVehicleAssembly before, long expectedRevision, string nativeFuelItem,
        float nativeFuelLevel, float nativeFuelCapacity, IEnumerable<RebirthVehicleTransferItem> selectedDebits)
    {
        if (target == null || target.Carrier != RebirthVehicleAssemblyCarrier.VehicleEntity
            || before == null || before.Revision != expectedRevision || before.PendingInventoryTransferId != Guid.Empty
            || string.IsNullOrEmpty(nativeFuelItem) || float.IsNaN(nativeFuelLevel) || float.IsInfinity(nativeFuelLevel)
            || float.IsNaN(nativeFuelCapacity) || float.IsInfinity(nativeFuelCapacity)
            || nativeFuelCapacity <= 0f || nativeFuelLevel < 0f || nativeFuelLevel >= nativeFuelCapacity
            || before.FuelPercent != nativeFuelLevel / nativeFuelCapacity)
            throw new InvalidDataException("Refuel requires an available entity revision and native tank observation.");
        var debits = (selectedDebits ?? Enumerable.Empty<RebirthVehicleTransferItem>()).Take(257).ToArray();
        if (debits.Length == 0 || debits.Length > 256 || debits.Any(i => i == null || !i.Debit)
            || debits.Select(i => i.Source).Distinct().Count() != 1
            || debits.Select(i => i.Slot).Distinct().Count() != debits.Length)
            throw new InvalidDataException("Refuel requires distinct debit slots in one native inventory domain.");
        long count = 0;
        foreach (var debit in debits)
        {
            ItemValue decoded;
            if (!RebirthNativeItemCodec.TryDecode(debit.ItemData, out decoded)
                || decoded.ItemClass == null || decoded.ItemClass.GetItemName() != nativeFuelItem)
                throw new InvalidDataException("Refuel debit does not contain the native fuel item.");
            count += debit.Count;
        }
        int requested = (int)Math.Ceiling((double)Math.Min(2500f, (nativeFuelCapacity - nativeFuelLevel) * 25f));
        if (count < 1 || count > requested) throw new InvalidDataException("Refuel debit exceeds the native refill request.");
        float afterLevel = Math.Min(nativeFuelCapacity, nativeFuelLevel + (float)count / 25f);
        var fuel = new RebirthVehicleTransferFuel(nativeFuelLevel, afterLevel, nativeFuelCapacity);
        var after = before.DeepClone();
        if (after.Revision == long.MaxValue) throw new InvalidDataException("Vehicle revision is exhausted.");
        after.FuelPercent = afterLevel / nativeFuelCapacity; after.Revision++; after.LastAppliedInventoryTransferId = transactionId;
        return RebirthVehicleTransferPlan.Create(transactionId, creationId, actorId, fingerprint,
            before, after, debits, target, fuel);
    }

    public static RebirthVehicleTransferPlan Siphon(Guid transactionId, Guid creationId,
        string actorId, string fingerprint, RebirthVehicleTransferTarget target,
        RebirthVehicleAssembly before, long expectedRevision, float? nativeFuelLevel = null, float? nativeFuelCapacity = null)
        =>Siphon(transactionId,creationId.ToString("N"),actorId,fingerprint,target,before,expectedRevision,nativeFuelLevel,nativeFuelCapacity);
    public static RebirthVehicleTransferPlan Siphon(Guid transactionId, string creationId,
        string actorId, string fingerprint, RebirthVehicleTransferTarget target,
        RebirthVehicleAssembly before, long expectedRevision, float? nativeFuelLevel = null, float? nativeFuelCapacity = null)
    {
        if (target == null || before == null || before.Revision != expectedRevision
            || before.PendingInventoryTransferId != Guid.Empty || before.Revision == long.MaxValue)
            throw new InvalidDataException("Siphon requires an available exact revision and target.");
        RebirthVehicleTransferFuel fuel = null;
        float gasAmount;
        if (target.Carrier == RebirthVehicleAssemblyCarrier.VehicleEntity)
        {
            if (!nativeFuelLevel.HasValue || !nativeFuelCapacity.HasValue)
                throw new InvalidDataException("Entity siphon requires native fuel level and capacity.");
            fuel = new RebirthVehicleTransferFuel(nativeFuelLevel.Value, 0f, nativeFuelCapacity.Value);
            if (before.FuelPercent != fuel.BeforeLevel / fuel.Capacity)
                throw new InvalidDataException("Native fuel and assembly observation disagree.");
            gasAmount = fuel.BeforeLevel * 25f;
        }
        else
        {
            if (nativeFuelLevel.HasValue || nativeFuelCapacity.HasValue || float.IsNaN(before.FuelPercent)
                || float.IsInfinity(before.FuelPercent) || before.FuelPercent < 0f || before.FuelPercent > 1f)
                throw new InvalidDataException("Invalid repairable block fuel observation.");
            gasAmount = before.FuelPercent * 500f;
        }
        // Match existing Mathf.RoundToInt's nearest-even tie behavior, with a
        // checked limit before conversion instead of overflowing a credit count.
        double rounded = Math.Round((double)gasAmount, MidpointRounding.ToEven);
        if (double.IsNaN(rounded) || double.IsInfinity(rounded) || rounded < 1d || rounded > int.MaxValue)
            throw new InvalidDataException("Siphon does not produce a valid gas count.");
        var gas = ItemClass.GetItemClass("ammoGasCan", false);
        if (gas == null) throw new InvalidDataException("Native gas item is unavailable.");
        var credit = new RebirthVehicleTransferItem(Guid.NewGuid(), false,
            RebirthNativeItemCodec.Encode(new ItemValue(gas.Id)), (int)rounded, RebirthVehiclePartSourceLocation.None, -1);
        var after = before.DeepClone();
        after.FuelPercent = 0f; after.Revision++; after.LastAppliedInventoryTransferId = transactionId;
        return RebirthVehicleTransferPlan.Create(transactionId, creationId, actorId, fingerprint,
            before, after, new[] { credit }, target, fuel);
    }

    public static RebirthVehicleTransferPlan PartChange(Guid transactionId, Guid creationId,
        string actorId, string fingerprint, RebirthVehicleTransferTarget target,
        RebirthVehicleAssembly before, long expectedRevision, RebirthVehicleAssemblyAction action,
        string slotId, RebirthInstalledVehiclePart incoming, RebirthVehiclePartSourceLocation source, int sourceSlot)
        =>PartChange(transactionId,creationId.ToString("N"),actorId,fingerprint,target,before,expectedRevision,action,slotId,incoming,source,sourceSlot);
    public static RebirthVehicleTransferPlan PartChange(Guid transactionId, string creationId,
        string actorId, string fingerprint, RebirthVehicleTransferTarget target,
        RebirthVehicleAssembly before, long expectedRevision, RebirthVehicleAssemblyAction action,
        string slotId, RebirthInstalledVehiclePart incoming, RebirthVehiclePartSourceLocation source, int sourceSlot)
    {
        if (target == null || before == null || before.Revision != expectedRevision
            || before.PendingInventoryTransferId != Guid.Empty)
            throw new InvalidDataException("Vehicle part planning requires an available exact revision and target.");
        bool install = action == RebirthVehicleAssemblyAction.Install || action == RebirthVehicleAssemblyAction.Replace;
        bool remove = action == RebirthVehicleAssemblyAction.Remove || action == RebirthVehicleAssemblyAction.Pickup;
        if (!install && !remove) throw new InvalidDataException("Action is not a vehicle part change.");
        var family = RebirthVehicleDefinitionRegistry.GetFamily(before.FamilyId);
        var slot = family?.Slots.SingleOrDefault(s => s != null && string.Equals(s.SlotId, slotId, StringComparison.Ordinal));
        if (slot == null) throw new InvalidDataException("Vehicle part slot is not defined by this family.");
        var after = before.DeepClone();
        var items = new List<RebirthVehicleTransferItem>();
        RebirthInstalledVehiclePart previous;
        bool occupied = after.Parts.TryGetValue(slot.SlotId, out previous);
        if (occupied && previous == null) throw new InvalidDataException("Vehicle part slot has invalid saved contents.");
        if (install)
        {
            // Cursor contents must first enter a saved owned slot; the offer is not
            // proof of ownership. Owner exact-payload debit supplies that proof.
            if (incoming == null || string.IsNullOrEmpty(incoming.SerializedItemValue))
                throw new InvalidDataException("Incoming part requires a complete native item payload.");
            var normalized = RebirthVehicleInventoryTransactions.SanitizeOfferedPart(incoming, slot.ItemName);
            if (normalized == null || normalized.SerializedItemValue != incoming.SerializedItemValue)
                throw new InvalidDataException("Incoming part does not preserve the exact offered native payload.");
            items.Add(new RebirthVehicleTransferItem(Guid.NewGuid(), true, incoming.SerializedItemValue, 1, source, sourceSlot));
            normalized.SlotId = slot.SlotId;
            after.Parts[slot.SlotId] = normalized;
        }
        else
        {
            if (incoming != null || source != RebirthVehiclePartSourceLocation.None || sourceSlot != -1)
                throw new InvalidDataException("Removal cannot supply an incoming inventory source.");
            if (!occupied) throw new InvalidDataException("Vehicle part slot is empty.");
            after.Parts.Remove(slot.SlotId);
        }
        if (occupied)
        {
            // Includes legacy part reconstruction through the existing native helper.
            // Pickup now plans durable owner credit rather than memory-only cursor delivery.
            var outgoing = RebirthVehicleInventoryTransactions.CreateItemStack(previous);
            if (outgoing == null || outgoing.IsEmpty() || outgoing.count != 1)
                throw new InvalidDataException("Saved vehicle part cannot be reconstructed for delivery.");
            items.Add(new RebirthVehicleTransferItem(Guid.NewGuid(), false,
                RebirthNativeItemCodec.Encode(outgoing.itemValue), 1, RebirthVehiclePartSourceLocation.None, -1));
        }
        if (after.Revision == long.MaxValue) throw new InvalidDataException("Vehicle revision is exhausted.");
        after.Revision++;
        after.LastAppliedInventoryTransferId = transactionId;
        return RebirthVehicleTransferPlan.Create(transactionId, creationId, actorId, fingerprint, before, after, items, target);
    }
}