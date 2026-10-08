using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcEquipmentSlot : byte
{
    Head = 0, Face = 1, Chest = 2, Hands = 3, Legs = 4, Feet = 5,
    PrimaryWeapon = 6, SecondaryWeapon = 7, Utility = 8
}

public enum RebirthNpcEquipmentResult : byte
{
    Applied = 0, Replayed = 1, InvalidRequest = 2, CapabilityDenied = 3,
    RevisionConflict = 4, ItemUnavailable = 5, NotFound = 6
}

public sealed class RebirthNpcEquipmentPersistentRecord
{
    public RebirthNpcStableId NpcId { get; internal set; }
    public uint Revision { get; internal set; }
    public Dictionary<RebirthNpcEquipmentSlot, string> Slots { get; internal set; }
    public Guid[] ReplayJournal { get; internal set; }
}

public sealed class RebirthNpcEquipmentSnapshot
{
    public RebirthNpcStableId NpcId { get; internal set; }
    public uint Revision { get; internal set; }
    public Dictionary<RebirthNpcEquipmentSlot, string> Slots { get; internal set; }
}

public static class RebirthNpcEquipmentService
{
    private sealed class State
    {
        public uint Revision;
        public readonly Dictionary<RebirthNpcEquipmentSlot, string> Slots = new Dictionary<RebirthNpcEquipmentSlot, string>();
        public readonly HashSet<Guid> Applied = new HashSet<Guid>();
        public readonly Queue<Guid> Journal = new Queue<Guid>();
    }

    private const int MaxJournal = 128;
    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId, State> States = new Dictionary<RebirthNpcStableId, State>();
    private static long equipped, unequipped, replayed, rejected;

    public static RebirthNpcEquipmentResult Equip(Guid transactionId, RebirthNpcStableId npcId,
        uint expectedRevision, RebirthNpcEquipmentSlot slot, string itemKey, out uint resultingRevision)
    {
        resultingRevision = 0; itemKey = (itemKey ?? string.Empty).Trim();
        RebirthNpcEquipmentPersistenceStore.EnsureLoaded();
        if (transactionId == Guid.Empty || npcId.IsEmpty || itemKey.Length == 0 || !Enum.IsDefined(typeof(RebirthNpcEquipmentSlot), slot))
            return Reject(RebirthNpcEquipmentResult.InvalidRequest);
        if (!HasCapability(npcId)) return Reject(RebirthNpcEquipmentResult.CapabilityDenied);
        lock (Sync)
        {
            State state = GetOrCreate(npcId); resultingRevision = state.Revision;
            if (state.Applied.Contains(transactionId)) { Interlocked.Increment(ref replayed); return RebirthNpcEquipmentResult.Replayed; }
            if (state.Revision != expectedRevision) return Reject(RebirthNpcEquipmentResult.RevisionConflict);
            string current; state.Slots.TryGetValue(slot, out current);
            if (string.Equals(current, itemKey, StringComparison.OrdinalIgnoreCase))
            { Remember(state, transactionId); return RebirthNpcEquipmentResult.Replayed; }
            if (!RebirthNpcInventoryTransactionService.TryReserve(npcId, itemKey, 1))
                return Reject(RebirthNpcEquipmentResult.ItemUnavailable);
            if (!string.IsNullOrEmpty(current)) RebirthNpcInventoryTransactionService.ReleaseReservation(npcId, current, 1);
            state.Slots[slot] = itemKey; state.Revision++; resultingRevision = state.Revision; Remember(state, transactionId);
            Interlocked.Increment(ref equipped); RebirthNpcEquipmentPersistenceStore.MarkDirty();
            PublishProjection(npcId, state);
            return RebirthNpcEquipmentResult.Applied;
        }
    }

    public static RebirthNpcEquipmentResult Unequip(Guid transactionId, RebirthNpcStableId npcId,
        uint expectedRevision, RebirthNpcEquipmentSlot slot, out uint resultingRevision)
    {
        resultingRevision = 0; RebirthNpcEquipmentPersistenceStore.EnsureLoaded();
        if (transactionId == Guid.Empty || npcId.IsEmpty || !Enum.IsDefined(typeof(RebirthNpcEquipmentSlot), slot))
            return Reject(RebirthNpcEquipmentResult.InvalidRequest);
        if (!HasCapability(npcId)) return Reject(RebirthNpcEquipmentResult.CapabilityDenied);
        lock (Sync)
        {
            State state = GetOrCreate(npcId); resultingRevision = state.Revision;
            if (state.Applied.Contains(transactionId)) { Interlocked.Increment(ref replayed); return RebirthNpcEquipmentResult.Replayed; }
            if (state.Revision != expectedRevision) return Reject(RebirthNpcEquipmentResult.RevisionConflict);
            string current;
            if (!state.Slots.TryGetValue(slot, out current)) { Remember(state, transactionId); return RebirthNpcEquipmentResult.Replayed; }
            state.Slots.Remove(slot); RebirthNpcInventoryTransactionService.ReleaseReservation(npcId, current, 1);
            state.Revision++; resultingRevision = state.Revision; Remember(state, transactionId);
            Interlocked.Increment(ref unequipped); RebirthNpcEquipmentPersistenceStore.MarkDirty();
            PublishProjection(npcId, state);
            return RebirthNpcEquipmentResult.Applied;
        }
    }

    public static void ReleaseAll(RebirthNpcStableId npcId)
    {
        RebirthNpcEquipmentPersistenceStore.EnsureLoaded();
        lock (Sync)
        {
            State state; if (!States.TryGetValue(npcId, out state)) return;
            foreach (string itemKey in state.Slots.Values)
                RebirthNpcInventoryTransactionService.ReleaseReservation(npcId, itemKey, 1);
            state.Slots.Clear(); state.Revision++;
            PublishProjection(npcId, state);
            States.Remove(npcId); RebirthNpcEquipmentPersistenceStore.MarkDirty();
        }
    }

    public static RebirthNpcEquipmentSnapshot GetSnapshot(RebirthNpcStableId npcId)
    {
        RebirthNpcEquipmentPersistenceStore.EnsureLoaded();
        lock (Sync) { State state = GetOrCreate(npcId); return new RebirthNpcEquipmentSnapshot
        { NpcId = npcId, Revision = state.Revision, Slots = new Dictionary<RebirthNpcEquipmentSlot, string>(state.Slots) }; }
    }

    public static RebirthNpcEquipmentPersistentRecord[] CapturePersistentRecords()
    {
        lock (Sync)
        {
            var records = new RebirthNpcEquipmentPersistentRecord[States.Count]; int i = 0;
            foreach (var pair in States) records[i++] = new RebirthNpcEquipmentPersistentRecord
            { NpcId = pair.Key, Revision = pair.Value.Revision,
              Slots = new Dictionary<RebirthNpcEquipmentSlot, string>(pair.Value.Slots), ReplayJournal = pair.Value.Journal.ToArray() };
            Array.Sort(records, (a,b) => string.CompareOrdinal(a.NpcId.ToString(), b.NpcId.ToString())); return records;
        }
    }

    public static bool ValidatePersistentRecord(RebirthNpcEquipmentPersistentRecord record, out string error)
    {
        error = string.Empty;
        if (record == null || record.NpcId.IsEmpty || record.Slots == null || record.ReplayJournal == null)
        { error = "missing equipment state"; return false; }
        if (record.ReplayJournal.Length > MaxJournal) { error = "replay journal exceeds bound"; return false; }
        var seen = new HashSet<Guid>();
        foreach (var pair in record.Slots)
            if (!Enum.IsDefined(typeof(RebirthNpcEquipmentSlot), pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            { error = "invalid equipment slot"; return false; }
        foreach (Guid id in record.ReplayJournal) if (id == Guid.Empty || !seen.Add(id)) { error = "invalid replay journal"; return false; }
        return true;
    }

    public static bool TryRestorePersistentRecord(RebirthNpcEquipmentPersistentRecord record, out string error)
    {
        if (!ValidatePersistentRecord(record, out error)) return false;
        lock (Sync)
        {
            State state = new State { Revision = record.Revision };
            foreach (var pair in record.Slots) state.Slots[pair.Key] = pair.Value;
            foreach (Guid id in record.ReplayJournal) Remember(state, id);
            States[record.NpcId] = state;
        }
        return true;
    }

    public static void ClearRuntimeState() { lock (Sync) States.Clear(); }
    public static string GetReport()
    {
        lock (Sync)
        {
            int slots = 0; foreach (State state in States.Values) slots += state.Slots.Count;
            return "[REBIRTH NPC Equipment] loadouts=" + States.Count + " occupiedSlots=" + slots +
                " equipped=" + equipped + " unequipped=" + unequipped + " replayed=" + replayed + " rejected=" + rejected;
        }
    }


    public static void ProjectAndBroadcast(RebirthNpcStableId npcId)
    {
        RebirthNpcEquipmentPersistenceStore.EnsureLoaded();
        lock (Sync)
        {
            State state = GetOrCreate(npcId);
            PublishProjection(npcId, state);
        }
    }

    private static void PublishProjection(RebirthNpcStableId npcId, State state)
    {
        var snapshot = new RebirthNpcEquipmentSnapshot
        {
            NpcId = npcId,
            Revision = state.Revision,
            Slots = new Dictionary<RebirthNpcEquipmentSlot, string>(state.Slots)
        };
        RebirthNpcNativeEquipmentBridge.ApplyAuthoritativeSnapshot(snapshot);
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
        int entityId;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId)) return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityRebirthHumanoidNPC npc = world != null ? world.GetEntity(entityId) as EntityRebirthHumanoidNPC : null;
        if (npc == null) return;
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthNpcSemanticEquipment>().Setup(npc, snapshot),
            _onlyClientsAttachedToAnEntity: false, -1, entityId);
    }
    private static RebirthNpcEquipmentResult Reject(RebirthNpcEquipmentResult result) { Interlocked.Increment(ref rejected); return result; }
    private static State GetOrCreate(RebirthNpcStableId id) { State s; if (!States.TryGetValue(id, out s)) { s = new State(); States.Add(id, s); } return s; }
    private static void Remember(State state, Guid id)
    { if (!state.Applied.Add(id)) return; state.Journal.Enqueue(id); while (state.Journal.Count > MaxJournal) state.Applied.Remove(state.Journal.Dequeue()); }
    private static bool HasCapability(RebirthNpcStableId id)
    {
        int entityId; RebirthNpcRuntimeState runtime; RebirthNpcProfile profile;
        return RebirthNpcRuntimeRegistry.TryGetEntityId(id, out entityId) && RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime) &&
            RebirthNpcProfileRegistry.TryResolve(runtime.ProfileId, out profile) && profile.Has(RebirthNpcCapabilities.Inventory | RebirthNpcCapabilities.Equipment);
    }
}
