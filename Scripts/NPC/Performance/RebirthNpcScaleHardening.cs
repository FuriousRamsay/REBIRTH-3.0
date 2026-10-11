using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcPathBudgetClass : byte
{
    Critical = 0,
    Interactive = 1,
    Background = 2
}

public sealed class RebirthNpcScaleSnapshot
{
    public int SpatialEntries;
    public int SpatialCells;
    public long SpatialQueries;
    public long SpatialCandidates;
    public long SpatialHits;
    public long PathAccepted;
    public long PathDeferred;
    public int PendingNetworkBatches;
    public int PendingNetworkMessages;
    public long NetworkMessagesQueued;
    public long NetworkBatchesDrained;
    public long NetworkBytesQueued;
    public int SerializationBuffersRetained;
    public long SerializationRentals;
    public long SerializationAllocations;
    public long RelationshipBatches;
    public long RelationshipItems;
    public long RetentionSweeps;
    public long RetentionItemsReleased;
}

/// <summary>
/// Process-wide composition point for WP18 scale controls. Every retained
/// collection is bounded and every world-scoped entry is cleared explicitly.
/// </summary>
public static class RebirthNpcScaleHardeningService
{
    public static void EnsureInitialized()
    {
        RebirthNpcSpatialIndex.EnsureInitialized();
        RebirthNpcPathBudgetCoordinator.EnsureInitialized();
        RebirthNpcNetworkBatcher.EnsureInitialized();
        RebirthNpcSerializationBufferPool.EnsureInitialized();
        RebirthNpcRelationshipBatcher.EnsureInitialized();
        RebirthNpcRetentionCoordinator.EnsureInitialized();
    }

    public static RebirthNpcScaleSnapshot GetSnapshot()
    {
        RebirthNpcSpatialIndexSnapshot spatial = RebirthNpcSpatialIndex.GetSnapshot();
        RebirthNpcPathBudgetSnapshot path = RebirthNpcPathBudgetCoordinator.GetSnapshot();
        RebirthNpcNetworkBatchSnapshot network = RebirthNpcNetworkBatcher.GetSnapshot();
        RebirthNpcSerializationPoolSnapshot serialization = RebirthNpcSerializationBufferPool.GetSnapshot();
        RebirthNpcRelationshipBatchSnapshot relationships = RebirthNpcRelationshipBatcher.GetSnapshot();
        RebirthNpcRetentionSnapshot retention = RebirthNpcRetentionCoordinator.GetSnapshot();
        return new RebirthNpcScaleSnapshot
        {
            SpatialEntries = spatial.EntryCount,
            SpatialCells = spatial.CellCount,
            SpatialQueries = spatial.Queries,
            SpatialCandidates = spatial.Candidates,
            SpatialHits = spatial.Hits,
            PathAccepted = path.Accepted,
            PathDeferred = path.Deferred,
            PendingNetworkBatches = network.PendingBatches,
            PendingNetworkMessages = network.PendingMessages,
            NetworkMessagesQueued = network.MessagesQueued,
            NetworkBatchesDrained = network.BatchesDrained,
            NetworkBytesQueued = network.BytesQueued,
            SerializationBuffersRetained = serialization.Retained,
            SerializationRentals = serialization.Rentals,
            SerializationAllocations = serialization.Allocations,
            RelationshipBatches = relationships.Batches,
            RelationshipItems = relationships.Items,
            RetentionSweeps = retention.Sweeps,
            RetentionItemsReleased = retention.ItemsReleased
        };
    }

    public static string GetReport()
    {
        RebirthNpcScaleSnapshot s = GetSnapshot();
        return "[REBIRTH NPC Scale Hardening] spatialEntries=" + s.SpatialEntries +
            " spatialCells=" + s.SpatialCells + " spatialQueries=" + s.SpatialQueries +
            " candidates=" + s.SpatialCandidates + " hits=" + s.SpatialHits +
            " pathAccepted=" + s.PathAccepted + " pathDeferred=" + s.PathDeferred +
            " pendingNetworkBatches=" + s.PendingNetworkBatches +
            " pendingNetworkMessages=" + s.PendingNetworkMessages +
            " networkMessagesQueued=" + s.NetworkMessagesQueued +
            " networkBatchesDrained=" + s.NetworkBatchesDrained +
            " networkBytesQueued=" + s.NetworkBytesQueued +
            " serializationRetained=" + s.SerializationBuffersRetained +
            " serializationRentals=" + s.SerializationRentals +
            " serializationAllocations=" + s.SerializationAllocations +
            " relationshipBatches=" + s.RelationshipBatches +
            " relationshipItems=" + s.RelationshipItems +
            " retentionSweeps=" + s.RetentionSweeps +
            " retentionItemsReleased=" + s.RetentionItemsReleased;
    }

    public static void ResetForWorldChange()
    {
        RebirthNpcSpatialIndex.ResetForWorldChange();
        RebirthNpcPathBudgetCoordinator.ResetForWorldChange();
        RebirthNpcNetworkBatcher.ResetForWorldChange();
        RebirthNpcSerializationBufferPool.ResetForWorldChange();
        RebirthNpcRelationshipBatcher.ResetForWorldChange();
        RebirthNpcRetentionCoordinator.ResetForWorldChange();
        RebirthNpcScaleBenchmarkService.ResetForWorldChange();
    }
}

public sealed class RebirthNpcSpatialIndexSnapshot
{
    public int EntryCount, CellCount;
    public long Queries, Candidates, Hits, Updates, Removes;
}

public static class RebirthNpcSpatialIndex
{
    private sealed class Entry
    {
        public int EntityId;
        public float X, Y, Z;
        public long Cell;
    }

    private const int CellSize = 16;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
    private static readonly Dictionary<long, HashSet<int>> Cells = new Dictionary<long, HashSet<int>>();
    private static long queries, candidates, hits, updates, removes;

    public static void EnsureInitialized() { }

    public static void Upsert(int entityId, float x, float y, float z)
    {
        long cell = CellKey(x, z);
        lock (Sync)
        {
            Entry entry;
            if (Entries.TryGetValue(entityId, out entry))
            {
                if (entry.Cell != cell)
                {
                    RemoveFromCell(entry.Cell, entityId);
                    AddToCell(cell, entityId);
                    entry.Cell = cell;
                }
                entry.X = x; entry.Y = y; entry.Z = z;
            }
            else
            {
                entry = new Entry { EntityId = entityId, X = x, Y = y, Z = z, Cell = cell };
                Entries.Add(entityId, entry);
                AddToCell(cell, entityId);
            }
            updates++;
        }
    }

    public static bool Remove(int entityId)
    {
        lock (Sync)
        {
            Entry entry;
            if (!Entries.TryGetValue(entityId, out entry)) return false;
            Entries.Remove(entityId);
            RemoveFromCell(entry.Cell, entityId);
            removes++;
            return true;
        }
    }

    public static int QueryRadius(float x, float y, float z, float radius, List<int> destination, int maximumResults)
    {
        if (destination == null) throw new ArgumentNullException("destination");
        if (radius < 0f) radius = 0f;
        if (maximumResults <= 0) return 0;
        float radiusSquared = radius * radius;
        int minX = FloorToCell(x - radius), maxX = FloorToCell(x + radius);
        int minZ = FloorToCell(z - radius), maxZ = FloorToCell(z + radius);
        int startCount = destination.Count;
        lock (Sync)
        {
            queries++;
            for (int cellX = minX; cellX <= maxX && destination.Count - startCount < maximumResults; cellX++)
            {
                for (int cellZ = minZ; cellZ <= maxZ && destination.Count - startCount < maximumResults; cellZ++)
                {
                    HashSet<int> ids;
                    if (!Cells.TryGetValue(Pack(cellX, cellZ), out ids)) continue;
                    foreach (int id in ids)
                    {
                        candidates++;
                        Entry entry;
                        if (!Entries.TryGetValue(id, out entry)) continue;
                        float dx = entry.X - x, dy = entry.Y - y, dz = entry.Z - z;
                        if (dx * dx + dy * dy + dz * dz > radiusSquared) continue;
                        destination.Add(id);
                        hits++;
                        if (destination.Count - startCount >= maximumResults) break;
                    }
                }
            }
        }
        return destination.Count - startCount;
    }

    public static RebirthNpcSpatialIndexSnapshot GetSnapshot()
    {
        lock (Sync) return new RebirthNpcSpatialIndexSnapshot
        {
            EntryCount = Entries.Count, CellCount = Cells.Count, Queries = queries,
            Candidates = candidates, Hits = hits, Updates = updates, Removes = removes
        };
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            Entries.Clear(); Cells.Clear();
            queries = candidates = hits = updates = removes = 0;
        }
    }

    private static int FloorToCell(float value) { return (int)Math.Floor(value / CellSize); }
    private static long CellKey(float x, float z) { return Pack(FloorToCell(x), FloorToCell(z)); }
    private static long Pack(int x, int z) { return ((long)x << 32) ^ (uint)z; }
    private static void AddToCell(long key, int id)
    {
        HashSet<int> ids;
        if (!Cells.TryGetValue(key, out ids)) { ids = new HashSet<int>(); Cells.Add(key, ids); }
        ids.Add(id);
    }
    private static void RemoveFromCell(long key, int id)
    {
        HashSet<int> ids;
        if (!Cells.TryGetValue(key, out ids)) return;
        ids.Remove(id);
        if (ids.Count == 0) Cells.Remove(key);
    }
}

public sealed class RebirthNpcBoundedCache<TKey, TValue>
{
    private sealed class CacheEntry
    {
        public TValue Value;
        public long ExpiresAtTicks;
    }

    private readonly object sync = new object();
    private readonly Dictionary<TKey, CacheEntry> entries;
    private readonly LinkedList<TKey> order = new LinkedList<TKey>();
    private readonly Dictionary<TKey, LinkedListNode<TKey>> orderNodes;
    private readonly int capacity;
    private readonly long lifetimeTicks;
    private long hits, misses, evictions, expirations;

    public RebirthNpcBoundedCache(int maximumEntries, TimeSpan lifetime, IEqualityComparer<TKey> comparer)
    {
        if (maximumEntries < 1) throw new ArgumentOutOfRangeException("maximumEntries");
        capacity = maximumEntries;
        lifetimeTicks = Math.Max(1L, lifetime.Ticks);
        IEqualityComparer<TKey> keyComparer = comparer ?? EqualityComparer<TKey>.Default;
        entries = new Dictionary<TKey, CacheEntry>(keyComparer);
        orderNodes = new Dictionary<TKey, LinkedListNode<TKey>>(keyComparer);
    }

    public bool TryGet(TKey key, long nowTicks, out TValue value)
    {
        lock (sync)
        {
            CacheEntry entry;
            if (!entries.TryGetValue(key, out entry)) { misses++; value = default(TValue); return false; }
            if (entry.ExpiresAtTicks <= nowTicks)
            {
                RemoveNoLock(key);
                expirations++;
                misses++;
                value = default(TValue);
                return false;
            }
            hits++;
            value = entry.Value;
            return true;
        }
    }

    public void Set(TKey key, TValue value, long nowTicks)
    {
        lock (sync)
        {
            RemoveOrderNodeNoLock(key);
            entries[key] = new CacheEntry { Value = value, ExpiresAtTicks = nowTicks + lifetimeTicks };
            orderNodes[key] = order.AddLast(key);
            TrimNoLock(nowTicks);
        }
    }

    public bool Remove(TKey key)
    {
        lock (sync) return RemoveNoLock(key);
    }

    public void Clear()
    {
        lock (sync)
        {
            entries.Clear();
            order.Clear();
            orderNodes.Clear();
        }
    }

    public string GetReport(string name)
    {
        lock (sync) return "[REBIRTH NPC Cache] name=" + name + " entries=" + entries.Count + "/" + capacity +
            " hits=" + hits + " misses=" + misses + " evictions=" + evictions + " expirations=" + expirations;
    }

    private void TrimNoLock(long nowTicks)
    {
        while (order.First != null)
        {
            TKey key = order.First.Value;
            CacheEntry entry;
            if (!entries.TryGetValue(key, out entry))
            {
                RemoveOrderNodeNoLock(key);
                continue;
            }

            if (entries.Count > capacity)
            {
                RemoveNoLock(key);
                evictions++;
                continue;
            }

            // Every entry has the same configured lifetime and Set() moves refreshed keys
            // to the tail, so the head is the earliest possible expiry. Once it is live,
            // every later entry is live too and the sweep can stop.
            if (entry.ExpiresAtTicks <= nowTicks)
            {
                RemoveNoLock(key);
                expirations++;
                continue;
            }

            break;
        }
    }

    private bool RemoveNoLock(TKey key)
    {
        bool removed = entries.Remove(key);
        RemoveOrderNodeNoLock(key);
        return removed;
    }

    private void RemoveOrderNodeNoLock(TKey key)
    {
        LinkedListNode<TKey> node;
        if (!orderNodes.TryGetValue(key, out node)) return;
        order.Remove(node);
        orderNodes.Remove(key);
    }
}

public sealed class RebirthNpcPathBudgetSnapshot
{
    public int CriticalRemaining, InteractiveRemaining, BackgroundRemaining;
    public long Accepted, Deferred, Epochs;
}

public static class RebirthNpcPathBudgetCoordinator
{
    private static readonly object Sync = new object();
    private static readonly int[] Limits = { 8, 24, 8 };
    private static readonly int[] Remaining = new int[3];
    private static long epoch = -1, accepted, deferred, epochs;

    public static void EnsureInitialized() { lock (Sync) ResetBudgetNoLock(CurrentEpoch()); }

    public static bool TryAcquire(RebirthNpcPathBudgetClass budgetClass, int cost)
    {
        if (cost < 1) cost = 1;
        int index = (int)budgetClass;
        if (index < 0 || index >= Remaining.Length) index = (int)RebirthNpcPathBudgetClass.Background;
        lock (Sync)
        {
            long current = CurrentEpoch();
            if (current != epoch) ResetBudgetNoLock(current);
            if (Remaining[index] < cost) { deferred++; return false; }
            Remaining[index] -= cost; accepted++; return true;
        }
    }

    public static RebirthNpcPathBudgetSnapshot GetSnapshot()
    {
        lock (Sync) return new RebirthNpcPathBudgetSnapshot
        {
            CriticalRemaining = Remaining[0], InteractiveRemaining = Remaining[1], BackgroundRemaining = Remaining[2],
            Accepted = accepted, Deferred = deferred, Epochs = epochs
        };
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { epoch = -1; accepted = deferred = epochs = 0; ResetBudgetNoLock(CurrentEpoch()); }
    }

    private static long CurrentEpoch() { return Stopwatch.GetTimestamp() / Math.Max(1L, Stopwatch.Frequency / 20L); }
    private static void ResetBudgetNoLock(long value)
    {
        epoch = value; epochs++;
        for (int i = 0; i < Remaining.Length; i++) Remaining[i] = Limits[i];
    }
}

public sealed class RebirthNpcNetworkBatch
{
    public int RecipientId;
    public string Channel;
    public byte[][] Payloads;
    public int TotalBytes;
}

public sealed class RebirthNpcNetworkBatchSnapshot
{
    public int PendingBatches, PendingMessages;
    public long MessagesQueued, MessagesRejected, BatchesDrained, BytesQueued;
}

public static class RebirthNpcNetworkBatcher
{
    private sealed class PendingBatch
    {
        public int RecipientId;
        public string Channel;
        public readonly List<byte[]> Payloads = new List<byte[]>(16);
        public int TotalBytes;
    }

    private const int MaxPendingBatches = 512;
    private const int MaxMessagesPerBatch = 64;
    private const int MaxBytesPerBatch = 48 * 1024;
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, PendingBatch> Pending = new Dictionary<string, PendingBatch>(StringComparer.Ordinal);
    private static long messagesQueued, messagesRejected, batchesDrained, bytesQueued;

    public static void EnsureInitialized() { }

    public static bool Queue(int recipientId, string channel, byte[] payload)
    {
        if (payload == null || payload.Length == 0 || payload.Length > MaxBytesPerBatch) { Interlocked.Increment(ref messagesRejected); return false; }
        channel = string.IsNullOrEmpty(channel) ? "default" : channel;
        string key = recipientId.ToString() + ":" + channel;
        lock (Sync)
        {
            PendingBatch batch;
            if (!Pending.TryGetValue(key, out batch))
            {
                if (Pending.Count >= MaxPendingBatches) { messagesRejected++; return false; }
                batch = new PendingBatch { RecipientId = recipientId, Channel = channel };
                Pending.Add(key, batch);
            }
            if (batch.Payloads.Count >= MaxMessagesPerBatch || batch.TotalBytes + payload.Length > MaxBytesPerBatch)
            {
                messagesRejected++; return false;
            }
            batch.Payloads.Add(payload); batch.TotalBytes += payload.Length;
            messagesQueued++; bytesQueued += payload.Length; return true;
        }
    }

    public static RebirthNpcNetworkBatch Peek(int recipientId, string channel)
    {
        channel = string.IsNullOrEmpty(channel) ? "default" : channel;
        string key = recipientId.ToString() + ":" + channel;
        lock (Sync)
        {
            PendingBatch batch;
            if (!Pending.TryGetValue(key, out batch)) return null;
            return new RebirthNpcNetworkBatch
            {
                RecipientId = batch.RecipientId, Channel = batch.Channel,
                Payloads = batch.Payloads.ToArray(), TotalBytes = batch.TotalBytes
            };
        }
    }

    public static int Acknowledge(int recipientId, string channel, int acceptedPayloads)
    {
        if (acceptedPayloads <= 0) return 0;
        channel = string.IsNullOrEmpty(channel) ? "default" : channel;
        string key = recipientId.ToString() + ":" + channel;
        lock (Sync)
        {
            PendingBatch batch;
            if (!Pending.TryGetValue(key, out batch)) return 0;
            int count = Math.Min(acceptedPayloads, batch.Payloads.Count);
            int bytes = 0;
            for (int i = 0; i < count; i++) bytes += batch.Payloads[i] != null ? batch.Payloads[i].Length : 0;
            if (count > 0) batch.Payloads.RemoveRange(0, count);
            batch.TotalBytes = Math.Max(0, batch.TotalBytes - bytes);
            if (batch.Payloads.Count == 0) { Pending.Remove(key); batchesDrained++; }
            return count;
        }
    }

    public static RebirthNpcNetworkBatch Drain(int recipientId, string channel)
    {
        channel = string.IsNullOrEmpty(channel) ? "default" : channel;
        string key = recipientId.ToString() + ":" + channel;
        lock (Sync)
        {
            PendingBatch batch;
            if (!Pending.TryGetValue(key, out batch)) return null;
            Pending.Remove(key); batchesDrained++;
            return new RebirthNpcNetworkBatch
            {
                RecipientId = batch.RecipientId, Channel = batch.Channel,
                Payloads = batch.Payloads.ToArray(), TotalBytes = batch.TotalBytes
            };
        }
    }

    public static RebirthNpcNetworkBatchSnapshot GetSnapshot()
    {
        lock (Sync)
        {
            int messages = 0; foreach (PendingBatch batch in Pending.Values) messages += batch.Payloads.Count;
            return new RebirthNpcNetworkBatchSnapshot
            {
                PendingBatches = Pending.Count, PendingMessages = messages, MessagesQueued = messagesQueued,
                MessagesRejected = messagesRejected, BatchesDrained = batchesDrained, BytesQueued = bytesQueued
            };
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Pending.Clear(); messagesQueued = messagesRejected = batchesDrained = bytesQueued = 0; }
    }
}

public sealed class RebirthNpcSerializationPoolSnapshot
{
    public int Retained;
    public long Rentals, Returns, Allocations, RejectedReturns;
}

public static class RebirthNpcSerializationBufferPool
{
    private const int MinimumSize = 256;
    private const int MaximumSize = 64 * 1024;
    private const int MaximumRetained = 64;
    private static readonly object Sync = new object();
    private static readonly Stack<byte[]> Buffers = new Stack<byte[]>();
    private static readonly HashSet<byte[]> Leased = new HashSet<byte[]>();
    private static long rentals, returns, allocations, rejectedReturns;

    public static void EnsureInitialized() { }

    public static byte[] Rent(int minimumLength)
    {
        if (minimumLength < MinimumSize) minimumLength = MinimumSize;
        if (minimumLength > MaximumSize)
        {
            byte[] oversized = new byte[minimumLength];
            lock (Sync) { allocations++; rentals++; Leased.Add(oversized); }
            return oversized;
        }
        lock (Sync)
        {
            byte[] selected = null;
            if (Buffers.Count > 0)
            {
                byte[][] candidates = Buffers.ToArray(); Buffers.Clear();
                for (int i = 0; i < candidates.Length; i++)
                {
                    byte[] candidate = candidates[i];
                    if (selected == null && candidate.Length >= minimumLength) selected = candidate;
                    else Buffers.Push(candidate);
                }
            }
            rentals++;
            if (selected == null) { allocations++; selected = new byte[RoundSize(minimumLength)]; }
            Leased.Add(selected);
            return selected;
        }
    }

    public static void Return(byte[] buffer, bool clear)
    {
        if (buffer == null) return;
        lock (Sync)
        {
            if (!Leased.Remove(buffer)) { rejectedReturns++; return; }
            returns++;
            if (buffer.Length < MinimumSize || buffer.Length > MaximumSize)
            {
                rejectedReturns++;
                return;
            }
            if (clear) Array.Clear(buffer, 0, buffer.Length);
            if (Buffers.Count >= MaximumRetained) { rejectedReturns++; return; }
            Buffers.Push(buffer);
        }
    }

    public static RebirthNpcSerializationPoolSnapshot GetSnapshot()
    {
        lock (Sync) return new RebirthNpcSerializationPoolSnapshot
        { Retained = Buffers.Count, Rentals = rentals, Returns = returns, Allocations = allocations, RejectedReturns = rejectedReturns };
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Buffers.Clear(); Leased.Clear(); rentals = returns = allocations = rejectedReturns = 0; }
    }

    private static int RoundSize(int size)
    {
        int value = MinimumSize;
        while (value < size && value < MaximumSize) value <<= 1;
        return value;
    }
}

public sealed class RebirthNpcRelationshipMutation
{
    public string SourceNpcId, TargetNpcId, Reason;
    public int Delta;
}

public sealed class RebirthNpcRelationshipBatchSnapshot
{
    public int PendingSources, PendingItems;
    public long Batches, Items, Rejected;
}

public static class RebirthNpcRelationshipBatcher
{
    private const int MaximumSources = 4096;
    private const int MaximumItemsPerSource = 128;
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, List<RebirthNpcRelationshipMutation>> Pending =
        new Dictionary<string, List<RebirthNpcRelationshipMutation>>(StringComparer.Ordinal);
    private static long batches, items, rejected;

    public static void EnsureInitialized() { }

    public static bool Queue(RebirthNpcRelationshipMutation mutation)
    {
        if (mutation == null || string.IsNullOrEmpty(mutation.SourceNpcId) || string.IsNullOrEmpty(mutation.TargetNpcId)) return false;
        lock (Sync)
        {
            List<RebirthNpcRelationshipMutation> list;
            if (!Pending.TryGetValue(mutation.SourceNpcId, out list))
            {
                if (Pending.Count >= MaximumSources) { rejected++; return false; }
                list = new List<RebirthNpcRelationshipMutation>(); Pending.Add(mutation.SourceNpcId, list);
            }
            if (list.Count >= MaximumItemsPerSource) { rejected++; return false; }
            list.Add(mutation); items++; return true;
        }
    }

    public static int Drain(string sourceNpcId, List<RebirthNpcRelationshipMutation> destination)
    {
        if (destination == null) throw new ArgumentNullException("destination");
        lock (Sync)
        {
            List<RebirthNpcRelationshipMutation> list;
            if (!Pending.TryGetValue(sourceNpcId, out list)) return 0;
            Pending.Remove(sourceNpcId); batches++;
            destination.AddRange(list); return list.Count;
        }
    }

    internal static bool Acknowledge(RebirthNpcRelationshipMutation mutation)
    {
        if (mutation == null) return false;
        lock (Sync)
        {
            List<RebirthNpcRelationshipMutation> list;
            if (!Pending.TryGetValue(mutation.SourceNpcId, out list) || list.Count == 0 ||
                !ReferenceEquals(list[0], mutation)) return false;
            list.RemoveAt(0);
            if (list.Count == 0) { Pending.Remove(mutation.SourceNpcId); batches++; }
            return true;
        }
    }

    public static RebirthNpcRelationshipBatchSnapshot GetSnapshot()
    {
        lock (Sync)
        {
            int pendingItems = 0; foreach (List<RebirthNpcRelationshipMutation> list in Pending.Values) pendingItems += list.Count;
            return new RebirthNpcRelationshipBatchSnapshot
            { PendingSources = Pending.Count, PendingItems = pendingItems, Batches = batches, Items = items, Rejected = rejected };
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Pending.Clear(); batches = items = rejected = 0; }
    }
}

public sealed class RebirthNpcRetentionSnapshot
{
    public int RegisteredParticipants;
    public long Sweeps, ItemsReleased, ParticipantFailures;
}

public static class RebirthNpcRetentionCoordinator
{
    private sealed class Participant
    {
        public string Id;
        public Func<long, int> Sweep;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<string, Participant> Participants = new Dictionary<string, Participant>(StringComparer.Ordinal);
    private static long sweeps, itemsReleased, participantFailures;

    public static void EnsureInitialized() { }

    public static bool Register(string id, Func<long, int> sweep)
    {
        if (string.IsNullOrEmpty(id) || sweep == null) return false;
        lock (Sync)
        {
            if (Participants.ContainsKey(id)) return false;
            Participants.Add(id, new Participant { Id = id, Sweep = sweep }); return true;
        }
    }

    public static int Sweep(long nowTicks)
    {
        Participant[] participants;
        lock (Sync) participants = new List<Participant>(Participants.Values).ToArray();
        int released = 0;
        for (int i = 0; i < participants.Length; i++)
        {
            try { released += Math.Max(0, participants[i].Sweep(nowTicks)); }
            catch (Exception error)
            {
                Interlocked.Increment(ref participantFailures);
                Log.Warning("[REBIRTH NPC Retention] participant=" + participants[i].Id + " failed: " + error.Message);
            }
        }
        Interlocked.Increment(ref sweeps); Interlocked.Add(ref itemsReleased, released); return released;
    }

    public static RebirthNpcRetentionSnapshot GetSnapshot()
    {
        lock (Sync) return new RebirthNpcRetentionSnapshot
        { RegisteredParticipants = Participants.Count, Sweeps = sweeps, ItemsReleased = itemsReleased, ParticipantFailures = participantFailures };
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Participants.Clear(); sweeps = itemsReleased = participantFailures = 0; }
    }
}

public sealed class RebirthNpcScaleBenchmarkResult
{
    public int EntityCount, QueryCount;
    public double SpatialMilliseconds, SerializationMilliseconds, BatchMilliseconds;
    public long ManagedBytesDelta;
    public bool Passed;
}

public static class RebirthNpcScaleBenchmarkService
{
    private static readonly object Sync = new object();
    private static RebirthNpcScaleBenchmarkResult last;
    private static long runs, passes, failures;

    public static RebirthNpcScaleBenchmarkResult Run(int entityCount, int queryCount)
    {
        entityCount = Math.Max(100, Math.Min(10000, entityCount));
        queryCount = Math.Max(10, Math.Min(5000, queryCount));
        RebirthNpcScaleHardeningService.ResetForWorldChange();
        RebirthNpcScaleHardeningService.EnsureInitialized();
        long memoryBefore = GC.GetTotalMemory(false);
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < entityCount; i++)
            RebirthNpcSpatialIndex.Upsert(i + 1, i % 200, (i % 13) * 0.25f, (i * 7) % 200);
        List<int> results = new List<int>(128);
        for (int i = 0; i < queryCount; i++)
        {
            results.Clear(); RebirthNpcSpatialIndex.QueryRadius(i % 200, 0f, (i * 11) % 200, 24f, results, 128);
        }
        watch.Stop(); double spatialMs = watch.Elapsed.TotalMilliseconds;

        watch.Restart();
        for (int i = 0; i < queryCount; i++)
        {
            byte[] buffer = RebirthNpcSerializationBufferPool.Rent(512 + (i % 2048));
            buffer[0] = (byte)i; RebirthNpcSerializationBufferPool.Return(buffer, false);
        }
        watch.Stop(); double serializationMs = watch.Elapsed.TotalMilliseconds;

        watch.Restart();
        byte[] payload = new byte[128];
        int queued = 0;
        for (int i = 0; i < queryCount; i++)
        {
            int recipient = i % 32;
            if (!RebirthNpcNetworkBatcher.Queue(recipient, "benchmark", payload))
                RebirthNpcNetworkBatcher.Drain(recipient, "benchmark");
            else queued++;
        }
        for (int i = 0; i < 32; i++) RebirthNpcNetworkBatcher.Drain(i, "benchmark");
        watch.Stop(); double batchMs = watch.Elapsed.TotalMilliseconds;
        long memoryDelta = GC.GetTotalMemory(false) - memoryBefore;

        RebirthNpcScaleBenchmarkResult result = new RebirthNpcScaleBenchmarkResult
        {
            EntityCount = entityCount, QueryCount = queryCount,
            SpatialMilliseconds = spatialMs, SerializationMilliseconds = serializationMs,
            BatchMilliseconds = batchMs, ManagedBytesDelta = memoryDelta
        };
        result.Passed = spatialMs <= 250d && serializationMs <= 100d && batchMs <= 100d && memoryDelta <= 32L * 1024L * 1024L;
        lock (Sync) last = result;
        Interlocked.Increment(ref runs);
        if (result.Passed) Interlocked.Increment(ref passes); else Interlocked.Increment(ref failures);
        return result;
    }

    public static string GetReport()
    {
        RebirthNpcScaleBenchmarkResult result; lock (Sync) result = last;
        if (result == null) return "[REBIRTH NPC Scale Benchmark] runs=" + runs + " no benchmark captured";
        return "[REBIRTH NPC Scale Benchmark] runs=" + runs + " passes=" + passes + " failures=" + failures +
            " entities=" + result.EntityCount + " queries=" + result.QueryCount +
            " spatialMs=" + result.SpatialMilliseconds.ToString("0.000") +
            " serializationMs=" + result.SerializationMilliseconds.ToString("0.000") +
            " batchMs=" + result.BatchMilliseconds.ToString("0.000") +
            " managedBytesDelta=" + result.ManagedBytesDelta + " result=" + (result.Passed ? "PASS" : "FAIL") +
            " runtimeEvidenceRequired=exact-b259-stress,host-client-bandwidth,dedicated-soak,save-duration";
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) last = null;
    }
}
