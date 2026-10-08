using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public sealed class RebirthNpcItemIntegritySnapshot
{
    public int Inventories;
    public int EquipmentLoadouts;
    public int QuantityEntries;
    public int ReservationEntries;
    public int EquippedSlots;
    public int MissingInventoryReservations;
    public int ExcessInventoryReservations;
    public int ReservationQuantityViolations;
    public int InvalidEquipmentEntries;
    public long AuditPasses;
    public long FailedAuditPasses;
    public string LastError;

    public bool IsValid => MissingInventoryReservations == 0 &&
        ReservationQuantityViolations == 0 && InvalidEquipmentEntries == 0 && string.IsNullOrEmpty(LastError);
}

/// <summary>
/// Read-only cross-store invariant audit for inventory reservations and semantic equipment.
/// Equipment consumes one reservation per occupied slot; persisted reservations may exceed
/// equipment requirements only when another subsystem has an active reservation.
/// </summary>
public static class RebirthNpcItemIntegrityService
{
    private static readonly object Sync = new object();
    private static long auditPasses;
    private static long failedAuditPasses;
    private static RebirthNpcItemIntegritySnapshot last = new RebirthNpcItemIntegritySnapshot { LastError = string.Empty };

    public static RebirthNpcItemIntegritySnapshot Audit()
    {
        RebirthNpcPersistenceCoordinator.EnsureLoaded();
        RebirthNpcInventoryPersistentRecord[] inventories = RebirthNpcInventoryTransactionService.CapturePersistentRecords();
        RebirthNpcEquipmentPersistentRecord[] equipment = RebirthNpcEquipmentService.CapturePersistentRecords();
        RebirthNpcItemIntegritySnapshot snapshot = new RebirthNpcItemIntegritySnapshot
        {
            Inventories = inventories.Length,
            EquipmentLoadouts = equipment.Length,
            LastError = string.Empty
        };

        try
        {
            Dictionary<RebirthNpcStableId, RebirthNpcInventoryPersistentRecord> inventoryByNpc =
                new Dictionary<RebirthNpcStableId, RebirthNpcInventoryPersistentRecord>();
            for (int i = 0; i < inventories.Length; i++)
            {
                RebirthNpcInventoryPersistentRecord record = inventories[i];
                inventoryByNpc[record.NpcId] = record;
                snapshot.QuantityEntries += record.Quantities.Count;
                snapshot.ReservationEntries += record.Reservations.Count;
                foreach (KeyValuePair<string, int> reservation in record.Reservations)
                {
                    int quantity;
                    if (!record.Quantities.TryGetValue(reservation.Key, out quantity) || reservation.Value <= 0 || reservation.Value > quantity)
                        snapshot.ReservationQuantityViolations++;
                }
            }

            Dictionary<RebirthNpcStableId, Dictionary<string, int>> required =
                new Dictionary<RebirthNpcStableId, Dictionary<string, int>>();
            for (int i = 0; i < equipment.Length; i++)
            {
                RebirthNpcEquipmentPersistentRecord record = equipment[i];
                Dictionary<string, int> perItem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                required[record.NpcId] = perItem;
                foreach (KeyValuePair<RebirthNpcEquipmentSlot, string> slot in record.Slots)
                {
                    snapshot.EquippedSlots++;
                    string itemKey = (slot.Value ?? string.Empty).Trim();
                    if (!Enum.IsDefined(typeof(RebirthNpcEquipmentSlot), slot.Key) || itemKey.Length == 0)
                    {
                        snapshot.InvalidEquipmentEntries++;
                        continue;
                    }
                    int count;
                    perItem.TryGetValue(itemKey, out count);
                    perItem[itemKey] = count + 1;
                }
            }

            foreach (KeyValuePair<RebirthNpcStableId, Dictionary<string, int>> npc in required)
            {
                RebirthNpcInventoryPersistentRecord inventory;
                if (!inventoryByNpc.TryGetValue(npc.Key, out inventory))
                {
                    foreach (int count in npc.Value.Values) snapshot.MissingInventoryReservations += count;
                    continue;
                }
                foreach (KeyValuePair<string, int> item in npc.Value)
                {
                    int reserved;
                    inventory.Reservations.TryGetValue(item.Key, out reserved);
                    if (reserved < item.Value) snapshot.MissingInventoryReservations += item.Value - reserved;
                }
            }

            // Excess is informational because work/logistics may own reservations too.
            foreach (RebirthNpcInventoryPersistentRecord inventory in inventories)
            {
                Dictionary<string, int> equipmentRequired;
                required.TryGetValue(inventory.NpcId, out equipmentRequired);
                foreach (KeyValuePair<string, int> reservation in inventory.Reservations)
                {
                    int needed = 0;
                    if (equipmentRequired != null) equipmentRequired.TryGetValue(reservation.Key, out needed);
                    if (reservation.Value > needed) snapshot.ExcessInventoryReservations += reservation.Value - needed;
                }
            }
        }
        catch (Exception ex)
        {
            snapshot.LastError = ex.GetType().Name + ": " + ex.Message;
        }

        snapshot.AuditPasses = Interlocked.Increment(ref auditPasses);
        if (!snapshot.IsValid) snapshot.FailedAuditPasses = Interlocked.Increment(ref failedAuditPasses);
        else snapshot.FailedAuditPasses = Interlocked.Read(ref failedAuditPasses);
        lock (Sync) last = snapshot;
        return snapshot;
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) last = new RebirthNpcItemIntegritySnapshot { LastError = string.Empty };
    }

    public static string GetReport()
    {
        RebirthNpcItemIntegritySnapshot snapshot = Audit();
        StringBuilder b = new StringBuilder();
        b.Append("[REBIRTH NPC Item Integrity] valid=").Append(snapshot.IsValid)
         .Append(" inventories=").Append(snapshot.Inventories)
         .Append(" loadouts=").Append(snapshot.EquipmentLoadouts)
         .Append(" quantityEntries=").Append(snapshot.QuantityEntries)
         .Append(" reservationEntries=").Append(snapshot.ReservationEntries)
         .Append(" equippedSlots=").Append(snapshot.EquippedSlots)
         .Append(" missingEquipmentReservations=").Append(snapshot.MissingInventoryReservations)
         .Append(" excessReservations=").Append(snapshot.ExcessInventoryReservations)
         .Append(" reservationQuantityViolations=").Append(snapshot.ReservationQuantityViolations)
         .Append(" invalidEquipmentEntries=").Append(snapshot.InvalidEquipmentEntries)
         .Append(" auditPasses=").Append(snapshot.AuditPasses)
         .Append(" failedAuditPasses=").Append(snapshot.FailedAuditPasses)
         .Append(" lastError=").Append(string.IsNullOrEmpty(snapshot.LastError) ? "none" : snapshot.LastError);
        return b.ToString();
    }
}
