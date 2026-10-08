using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>Thread-local double-only scratch; nested and oversized calls use transient storage.</summary>
internal struct RebirthThemeScratchLease : IDisposable
{
    private const int RetainedCapacityLimit = 256;
    private sealed class Slot
    {
        internal readonly List<double> Multipliers;
        internal readonly List<double> Weights;
        internal readonly int OwnerThreadId;
        internal bool InUse;
        internal long Generation;
        internal Slot(int capacity)
        {
            Multipliers = new List<double>(capacity);
            Weights = new List<double>(capacity);
            OwnerThreadId = Thread.CurrentThread.ManagedThreadId;
        }
    }
    [ThreadStatic] private static Slot retained;
    private readonly Slot slot;
    private readonly long generation;
    private RebirthThemeScratchLease(Slot value)
    {
        slot = value;
        generation = value.Generation;
    }
    internal List<double> Multipliers { get { return slot.Multipliers; } }
    internal List<double> Weights { get { return slot.Weights; } }
    internal static RebirthThemeScratchLease Acquire(int capacity)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException("capacity");
        Slot value;
        if (capacity > RetainedCapacityLimit || (retained != null && retained.InUse))
            value = new Slot(capacity);
        else
        {
            if (retained == null) retained = new Slot(capacity);
            value = retained;
            if (value.Multipliers.Capacity < capacity) value.Multipliers.Capacity = capacity;
            if (value.Weights.Capacity < capacity) value.Weights.Capacity = capacity;
        }
        value.Multipliers.Clear();
        value.Weights.Clear();
        unchecked { value.Generation++; }
        value.InUse = true;
        return new RebirthThemeScratchLease(value);
    }
    public void Dispose()
    {
        if (slot == null || !slot.InUse || slot.Generation != generation) return;
        if (Thread.CurrentThread.ManagedThreadId != slot.OwnerThreadId)
            throw new InvalidOperationException("Theme scratch must be released on its acquiring thread.");
        slot.Multipliers.Clear();
        slot.Weights.Clear();
        slot.InUse = false;
    }
}
