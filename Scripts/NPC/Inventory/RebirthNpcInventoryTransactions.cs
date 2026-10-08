using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcInventoryTransactionResult : byte
{
    Applied = 0,
    Replayed = 1,
    InvalidRequest = 2,
    CapabilityDenied = 3,
    RevisionConflict = 4,
    InsufficientQuantity = 5,
    ReservationConflict = 6,
    NotFound = 7,
    AuthorityDenied = 8
}



public enum RebirthNpcInventoryTransferResult : byte
{
    Applied = 0,
    Replayed = 1,
    InvalidRequest = 2,
    CapabilityDenied = 3,
    RevisionConflict = 4,
    InsufficientQuantity = 5,
    ReservationConflict = 6,
    NotFound = 7,
    SameInventory = 8,
    AuthorityDenied = 9
}

public sealed class RebirthNpcInventoryTransfer
{
    public Guid TransactionId { get; }
    public RebirthNpcStableId SourceNpcId { get; }
    public RebirthNpcStableId DestinationNpcId { get; }
    public uint ExpectedSourceRevision { get; }
    public uint ExpectedDestinationRevision { get; }
    public string ItemKey { get; }
    public int Quantity { get; }
    public string AuthorityKey { get; }

    public RebirthNpcInventoryTransfer(Guid transactionId, RebirthNpcStableId sourceNpcId,
        RebirthNpcStableId destinationNpcId, uint expectedSourceRevision, uint expectedDestinationRevision,
        string itemKey, int quantity, string authorityKey)
    {
        TransactionId = transactionId;
        SourceNpcId = sourceNpcId;
        DestinationNpcId = destinationNpcId;
        ExpectedSourceRevision = expectedSourceRevision;
        ExpectedDestinationRevision = expectedDestinationRevision;
        ItemKey = (itemKey ?? string.Empty).Trim();
        Quantity = quantity;
        AuthorityKey = (authorityKey ?? string.Empty).Trim();
    }
}

public sealed class RebirthNpcInventoryMutation
{
    public string ItemKey { get; }
    public int QuantityDelta { get; }

    public RebirthNpcInventoryMutation(string itemKey, int quantityDelta)
    {
        ItemKey = (itemKey ?? string.Empty).Trim();
        QuantityDelta = quantityDelta;
    }
}

public sealed class RebirthNpcInventoryTransaction
{
    public Guid TransactionId { get; }
    public RebirthNpcStableId NpcId { get; }
    public uint ExpectedRevision { get; }
    public string AuthorityKey { get; }
    public IReadOnlyList<RebirthNpcInventoryMutation> Mutations { get; }

    public RebirthNpcInventoryTransaction(Guid transactionId, RebirthNpcStableId npcId, uint expectedRevision,
        string authorityKey, IReadOnlyList<RebirthNpcInventoryMutation> mutations)
    {
        TransactionId = transactionId;
        NpcId = npcId;
        ExpectedRevision = expectedRevision;
        AuthorityKey = (authorityKey ?? string.Empty).Trim();
        Mutations = mutations;
    }
}

public sealed class RebirthNpcInventorySnapshot
{
    internal RebirthNpcNativeStackSet NativeStacks;
    public RebirthNpcStableId NpcId { get; internal set; }
    public uint Revision { get; internal set; }
    public Dictionary<string, int> Quantities { get; internal set; }
    public Dictionary<string, int> Reservations { get; internal set; }
}

public sealed class RebirthNpcInventoryPersistentRecord
{
    internal RebirthNpcNativeStackSet NativeStacks;
    public RebirthNpcStableId NpcId { get; internal set; }
    public uint Revision { get; internal set; }
    public Dictionary<string, int> Quantities { get; internal set; }
    public Dictionary<string, int> Reservations { get; internal set; }
    public Guid[] ReplayJournal { get; internal set; }
}

public static class RebirthNpcInventoryTransactionService
{
    private sealed class InventoryState
    {
        internal RebirthNpcNativeStackSet NativeStacks;
        public uint Revision;
        public readonly Dictionary<string, int> Quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> Reservations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<Guid> AppliedTransactions = new HashSet<Guid>();
        public readonly Queue<Guid> AppliedTransactionOrder = new Queue<Guid>();
    }

    private const int MaxReplayTransactionsPerInventory = 256;
    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId, InventoryState> States = new Dictionary<RebirthNpcStableId, InventoryState>();
    private static long applied;
    private static long replayed;
    private static long rejected;

    public static RebirthNpcInventoryTransactionResult Apply(RebirthNpcInventoryTransaction transaction, out uint resultingRevision)
    {
        resultingRevision = 0;
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        if (!Validate(transaction))
        {
            Interlocked.Increment(ref rejected);
            return RebirthNpcInventoryTransactionResult.InvalidRequest;
        }
        if (!RebirthNpcInventoryAuthorityService.Validate(transaction.AuthorityKey,
            RebirthNpcInventoryAuthorityOperations.Mutate, transaction.NpcId))
        {
            Interlocked.Increment(ref rejected);
            return RebirthNpcInventoryTransactionResult.AuthorityDenied;
        }

        int entityId;
        RebirthNpcRuntimeState runtime;
        if (!RebirthNpcRuntimeRegistry.TryGetEntityId(transaction.NpcId, out entityId) ||
            !RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime))
        {
            Interlocked.Increment(ref rejected);
            return RebirthNpcInventoryTransactionResult.NotFound;
        }
        RebirthNpcProfile profile;
        if (!RebirthNpcProfileRegistry.TryResolve(runtime.ProfileId, out profile) || !profile.Has(RebirthNpcCapabilities.Inventory))
        {
            Interlocked.Increment(ref rejected);
            return RebirthNpcInventoryTransactionResult.CapabilityDenied;
        }

        lock (Sync)
        {
            InventoryState state = GetOrCreate(transaction.NpcId);
            resultingRevision = state.Revision;
            if (state.AppliedTransactions.Contains(transaction.TransactionId))
            {
                Interlocked.Increment(ref replayed);
                return RebirthNpcInventoryTransactionResult.Replayed;
            }
            if (transaction.ExpectedRevision != state.Revision || state.Revision == uint.MaxValue)
            {
                Interlocked.Increment(ref rejected);
                return RebirthNpcInventoryTransactionResult.RevisionConflict;
            }

            Dictionary<string, int> proposed = new Dictionary<string, int>(state.Quantities, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < transaction.Mutations.Count; i++)
            {
                RebirthNpcInventoryMutation mutation = transaction.Mutations[i];
                int current;
                proposed.TryGetValue(mutation.ItemKey, out current);
                long nextLong = (long)current + mutation.QuantityDelta;
                if (nextLong < 0 || nextLong > int.MaxValue)
                {
                    Interlocked.Increment(ref rejected);
                    return RebirthNpcInventoryTransactionResult.InsufficientQuantity;
                }
                int reserved;
                state.Reservations.TryGetValue(mutation.ItemKey, out reserved);
                if (nextLong < (long)reserved+(state.NativeStacks?.GetQuantity(mutation.ItemKey)??0))
                {
                    Interlocked.Increment(ref rejected);
                    return RebirthNpcInventoryTransactionResult.ReservationConflict;
                }
                proposed[mutation.ItemKey] = (int)nextLong;
            }

            if(state.NativeStacks!=null&&!state.NativeStacks.FitsQuantities(proposed))return RebirthNpcInventoryTransactionResult.ReservationConflict;
            state.Quantities.Clear();
            foreach (KeyValuePair<string, int> pair in proposed)
                if (pair.Value > 0) state.Quantities[pair.Key] = pair.Value;
            RememberTransaction(state, transaction.TransactionId);
            state.Revision++;
            if(state.NativeStacks!=null)state.NativeStacks=state.NativeStacks.WithRevision(state.Revision);
            resultingRevision = state.Revision;
            Interlocked.Increment(ref applied);
            RebirthNpcInventoryPersistenceStore.MarkDirty();
            return RebirthNpcInventoryTransactionResult.Applied;
        }
    }

    public static RebirthNpcInventoryTransferResult ApplyTransfer(RebirthNpcInventoryTransfer transfer,
        out uint sourceRevision, out uint destinationRevision)
    {
        sourceRevision = 0; destinationRevision = 0;
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        if (transfer == null || transfer.TransactionId == Guid.Empty || transfer.SourceNpcId.IsEmpty ||
            transfer.DestinationNpcId.IsEmpty || transfer.ItemKey.Length == 0 || transfer.Quantity <= 0 ||
            transfer.AuthorityKey.Length == 0)
            return RebirthNpcInventoryTransferResult.InvalidRequest;
        if (transfer.SourceNpcId == transfer.DestinationNpcId)
            return RebirthNpcInventoryTransferResult.SameInventory;
        if (!RebirthNpcInventoryAuthorityService.Validate(transfer.AuthorityKey,
            RebirthNpcInventoryAuthorityOperations.Transfer, transfer.SourceNpcId, transfer.DestinationNpcId))
            return RebirthNpcInventoryTransferResult.AuthorityDenied;
        if (!HasInventoryCapability(transfer.SourceNpcId) || !HasInventoryCapability(transfer.DestinationNpcId))
            return RebirthNpcInventoryTransferResult.CapabilityDenied;

        lock (Sync)
        {
            InventoryState source = GetOrCreate(transfer.SourceNpcId);
            InventoryState destination = GetOrCreate(transfer.DestinationNpcId);
            sourceRevision = source.Revision; destinationRevision = destination.Revision;
            if (source.AppliedTransactions.Contains(transfer.TransactionId) &&
                destination.AppliedTransactions.Contains(transfer.TransactionId))
                return RebirthNpcInventoryTransferResult.Replayed;
            if (source.Revision != transfer.ExpectedSourceRevision || destination.Revision != transfer.ExpectedDestinationRevision ||
                source.Revision == uint.MaxValue || destination.Revision == uint.MaxValue)
                return RebirthNpcInventoryTransferResult.RevisionConflict;
            int sourceQuantity; source.Quantities.TryGetValue(transfer.ItemKey, out sourceQuantity);
            int reserved; source.Reservations.TryGetValue(transfer.ItemKey, out reserved);
            if (sourceQuantity < transfer.Quantity) return RebirthNpcInventoryTransferResult.InsufficientQuantity;
            if ((long)sourceQuantity - transfer.Quantity < (long)reserved+(source.NativeStacks?.GetQuantity(transfer.ItemKey)??0)) return RebirthNpcInventoryTransferResult.ReservationConflict;
            int destinationQuantity; destination.Quantities.TryGetValue(transfer.ItemKey, out destinationQuantity);
            if ((long)destinationQuantity + transfer.Quantity > int.MaxValue)
                return RebirthNpcInventoryTransferResult.InvalidRequest;

            int remaining = sourceQuantity - transfer.Quantity;
            if(source.NativeStacks!=null&&remaining<source.NativeStacks.GetQuantity(transfer.ItemKey))return RebirthNpcInventoryTransferResult.ReservationConflict;
            if (remaining == 0) source.Quantities.Remove(transfer.ItemKey);
            else source.Quantities[transfer.ItemKey] = remaining;
            destination.Quantities[transfer.ItemKey] = destinationQuantity + transfer.Quantity;
            RememberTransaction(source, transfer.TransactionId);
            RememberTransaction(destination, transfer.TransactionId);
            source.Revision++; destination.Revision++;
            if(source.NativeStacks!=null)source.NativeStacks=source.NativeStacks.WithRevision(source.Revision);
            if(destination.NativeStacks!=null)destination.NativeStacks=destination.NativeStacks.WithRevision(destination.Revision);
            sourceRevision = source.Revision; destinationRevision = destination.Revision;
            Interlocked.Increment(ref applied);
            RebirthNpcInventoryPersistenceStore.MarkDirty();
            return RebirthNpcInventoryTransferResult.Applied;
        }
    }

    public static bool TryReserve(RebirthNpcStableId npcId, string itemKey, int quantity)
    {
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        itemKey = (itemKey ?? string.Empty).Trim();
        if (npcId.IsEmpty || itemKey.Length == 0 || quantity <= 0) return false;
        lock (Sync)
        {
            InventoryState state = GetOrCreate(npcId);
            int current;
            state.Quantities.TryGetValue(itemKey, out current);
            int reserved;
            state.Reservations.TryGetValue(itemKey, out reserved);
            if ((long)reserved + quantity > current-(state.NativeStacks?.GetQuantity(itemKey)??0)) return false;
            state.Reservations[itemKey] = reserved + quantity;
            RebirthNpcInventoryPersistenceStore.MarkDirty();
            return true;
        }
    }

    public static void ReleaseReservation(RebirthNpcStableId npcId, string itemKey, int quantity)
    {
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        itemKey = (itemKey ?? string.Empty).Trim();
        if (npcId.IsEmpty || itemKey.Length == 0 || quantity <= 0) return;
        lock (Sync)
        {
            InventoryState state;
            if (!States.TryGetValue(npcId, out state)) return;
            int reserved;
            if (!state.Reservations.TryGetValue(itemKey, out reserved)) return;
            reserved -= quantity;
            if (reserved > 0) state.Reservations[itemKey] = reserved;
            else state.Reservations.Remove(itemKey);
            RebirthNpcInventoryPersistenceStore.MarkDirty();
        }
    }

    // Revision preflight does not need copies of quantities or reservations.
    public static uint GetRevision(RebirthNpcStableId npcId)
    {
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        lock (Sync)
        {
            InventoryState state;
            return States.TryGetValue(npcId, out state) ? state.Revision : 0U;
        }
    }

    // Cached read only: checkpoint reconstruction must not create an inventory owner.
    internal static bool TryGetExistingNativeCustody(RebirthNpcStableId npcId, out RebirthNpcNativeStackSet custody, out uint revision)
    {
        lock (Sync)
        {
            custody=null; revision=0;
            if(!States.TryGetValue(npcId,out var state)) return false;
            custody=state.NativeStacks; revision=state.Revision; return true;
        }
    }
    public static RebirthNpcInventorySnapshot GetSnapshot(RebirthNpcStableId npcId)
    {
        RebirthNpcInventoryPersistenceStore.EnsureLoaded();
        lock (Sync)
        {
            InventoryState state = GetOrCreate(npcId);
            return new RebirthNpcInventorySnapshot
            {
                NativeStacks = state.NativeStacks,
                NpcId = npcId,
                Revision = state.Revision,
                Quantities = new Dictionary<string, int>(state.Quantities, StringComparer.OrdinalIgnoreCase),
                Reservations = new Dictionary<string, int>(state.Reservations, StringComparer.OrdinalIgnoreCase)
            };
        }
    }


    public static RebirthNpcInventoryPersistentRecord[] CapturePersistentRecords()
    {
        lock (Sync)
        {
            RebirthNpcInventoryPersistentRecord[] records = new RebirthNpcInventoryPersistentRecord[States.Count];
            int index = 0;
            foreach (KeyValuePair<RebirthNpcStableId, InventoryState> pair in States)
            {
                records[index++] = new RebirthNpcInventoryPersistentRecord
                {
                    NativeStacks = pair.Value.NativeStacks,
                    NpcId = pair.Key,
                    Revision = pair.Value.Revision,
                    Quantities = new Dictionary<string, int>(pair.Value.Quantities, StringComparer.OrdinalIgnoreCase),
                    Reservations = new Dictionary<string, int>(pair.Value.Reservations, StringComparer.OrdinalIgnoreCase),
                    ReplayJournal = pair.Value.AppliedTransactionOrder.ToArray()
                };
            }
            Array.Sort(records, (left, right) => string.CompareOrdinal(left.NpcId.ToString(), right.NpcId.ToString()));
            return records;
        }
    }

    public static bool ValidatePersistentRecord(RebirthNpcInventoryPersistentRecord record, out string error)
    {
        error = string.Empty;
        if (record == null || record.NpcId.IsEmpty)
        {
            error = "missing stable identity";
            return false;
        }
        if (record.Quantities == null || record.Reservations == null || record.ReplayJournal == null)
        {
            error = "missing inventory collections";
            return false;
        }
        if(record.NativeStacks!=null&&(record.NativeStacks.Owner!=record.NpcId.ToString()||
            record.NativeStacks.Revision!=record.Revision||!record.NativeStacks.FitsQuantities(record.Quantities)))
        {error="native stack ownership/revision/quantity mismatch";return false;}
        foreach (KeyValuePair<string, int> pair in record.Quantities)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
            {
                error = "invalid quantity entry";
                return false;
            }
        }
        foreach (KeyValuePair<string, int> pair in record.Reservations)
        {
            int quantity;
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0 ||
                !record.Quantities.TryGetValue(pair.Key, out quantity) || pair.Value > quantity-(record.NativeStacks?.GetQuantity(pair.Key)??0))
            {
                error = "invalid reservation entry";
                return false;
            }
        }
        if (record.ReplayJournal.Length > MaxReplayTransactionsPerInventory)
        {
            error = "replay journal exceeds bound";
            return false;
        }
        HashSet<Guid> replayIds = new HashSet<Guid>();
        for (int i = 0; i < record.ReplayJournal.Length; i++)
            if (record.ReplayJournal[i] == Guid.Empty || !replayIds.Add(record.ReplayJournal[i]))
            {
                error = "invalid replay journal";
                return false;
            }

        return true;
    }

    public static bool TryRestorePersistentRecord(RebirthNpcInventoryPersistentRecord record, out string error)
    {
        if (!ValidatePersistentRecord(record, out error)) return false;
        lock (Sync)
        {
            InventoryState state = new InventoryState { Revision = record.Revision,NativeStacks=record.NativeStacks };
            if(record.NativeStacks!=null&&(record.NativeStacks.Owner!=record.NpcId.ToString()||
            record.NativeStacks.Revision!=record.Revision||!record.NativeStacks.FitsQuantities(record.Quantities)))
        {error="native stack ownership/revision/quantity mismatch";return false;}
        foreach (KeyValuePair<string, int> pair in record.Quantities) state.Quantities[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, int> pair in record.Reservations) state.Reservations[pair.Key] = pair.Value;
            for (int i = 0; i < record.ReplayJournal.Length; i++) RememberTransaction(state, record.ReplayJournal[i]);
            States[record.NpcId] = state;
        }
        return true;
    }

    public static void ClearRuntimeState()
    {
        lock (Sync) States.Clear();
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            int itemKinds = 0;
            int reservations = 0;
            foreach (InventoryState state in States.Values)
            {
                itemKinds += state.Quantities.Count;
                reservations += state.Reservations.Count;
            }
            return "[REBIRTH NPC Inventory] inventories=" + States.Count
                + " itemKinds=" + itemKinds
                + " reservations=" + reservations
                + " applied=" + applied
                + " replayed=" + replayed
                + " rejected=" + rejected;
        }
    }



    private static bool HasInventoryCapability(RebirthNpcStableId npcId)
    {
        int entityId; RebirthNpcRuntimeState runtime; RebirthNpcProfile profile;
        return RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId) &&
            RebirthNpcRuntimeRegistry.TryGet(entityId, out runtime) &&
            RebirthNpcProfileRegistry.TryResolve(runtime.ProfileId, out profile) &&
            profile.Has(RebirthNpcCapabilities.Inventory);
    }

    private static void RememberTransaction(InventoryState state, Guid transactionId)
    {
        if (!state.AppliedTransactions.Add(transactionId)) return;
        state.AppliedTransactionOrder.Enqueue(transactionId);
        while (state.AppliedTransactionOrder.Count > MaxReplayTransactionsPerInventory)
        {
            Guid expired = state.AppliedTransactionOrder.Dequeue();
            state.AppliedTransactions.Remove(expired);
        }
    }

    private static InventoryState GetOrCreate(RebirthNpcStableId npcId)
    {
        InventoryState state;
        if (!States.TryGetValue(npcId, out state))
        {
            state = new InventoryState();
            States.Add(npcId, state);
        }
        return state;
    }

    private static bool Validate(RebirthNpcInventoryTransaction transaction)
    {
        if (transaction == null || transaction.TransactionId == Guid.Empty || transaction.NpcId.IsEmpty ||
            string.IsNullOrWhiteSpace(transaction.AuthorityKey) || transaction.Mutations == null || transaction.Mutations.Count == 0)
            return false;
        HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < transaction.Mutations.Count; i++)
        {
            RebirthNpcInventoryMutation mutation = transaction.Mutations[i];
            if (mutation == null || string.IsNullOrWhiteSpace(mutation.ItemKey) || mutation.QuantityDelta == 0 || !keys.Add(mutation.ItemKey))
                return false;
        }
        return true;
    }
}
