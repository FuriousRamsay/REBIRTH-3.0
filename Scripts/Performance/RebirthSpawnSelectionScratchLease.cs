using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>Thread-local double-only scratch; nested and oversized calls use transient storage.</summary>
internal struct RebirthSpawnSelectionScratchLease : IDisposable
{
    private const int RetainedCapacityLimit = 256;
    private sealed class Slot
    {
        internal readonly List<double> EntityWeights;
        internal readonly List<double> LogWeights;
        internal readonly int OwnerThreadId;
        internal bool InUse;
        internal long Generation;
        internal Slot(int capacity)
        {
            EntityWeights = new List<double>(capacity);
            LogWeights = new List<double>(capacity);
            OwnerThreadId = Thread.CurrentThread.ManagedThreadId;
        }
    }
    [ThreadStatic] private static Slot retained;
    private readonly Slot slot;
    private readonly long generation;
    private RebirthSpawnSelectionScratchLease(Slot value)
    {
        slot = value;
        generation = value.Generation;
    }
    internal List<double> EntityWeights { get { return slot.EntityWeights; } }
    internal List<double> LogWeights { get { return slot.LogWeights; } }
    internal static RebirthSpawnSelectionScratchLease Acquire(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException("capacity");
        Slot value;
        if (capacity > RetainedCapacityLimit || (retained != null && retained.InUse))
            value = new Slot(capacity);
        else
        {
            if (retained == null) retained = new Slot(capacity);
            value = retained;
            if (value.EntityWeights.Capacity < capacity) value.EntityWeights.Capacity = capacity;
            if (value.LogWeights.Capacity < capacity) value.LogWeights.Capacity = capacity;
        }
        value.EntityWeights.Clear();
        value.LogWeights.Clear();
        unchecked { value.Generation++; }
        value.InUse = true;
        return new RebirthSpawnSelectionScratchLease(value);
    }
    public void Dispose()
    {
        if (slot == null || !slot.InUse || slot.Generation != generation) return;
        if (Thread.CurrentThread.ManagedThreadId != slot.OwnerThreadId)
            throw new InvalidOperationException("Selection scratch must be released on its acquiring thread.");
        slot.EntityWeights.Clear();
        slot.LogWeights.Clear();
        slot.InUse = false;
    }
}
