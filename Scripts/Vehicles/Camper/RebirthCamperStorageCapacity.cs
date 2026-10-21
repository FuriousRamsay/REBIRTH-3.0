using System;

// Pure planning: never moves items or changes a native bag. Commit belongs to the server transaction.
public static class RebirthCamperStorageCapacity
{
    public const int ModuleSlots = 20;
    public const int ModuleColumns = 5;
    public const int ModuleRows = 4;
    // Installed ItemStackGrid.Write serializes item-array length as signed Int16.
    public const int BaseSlots = 44;
    public const int MaximumNativeSlots = short.MaxValue;

    public static int Total(int[] capacities, int baseSlots = BaseSlots)
    {
        if (capacities == null || capacities.Length != ModuleSlots)
            throw new ArgumentException("Exactly twenty container positions are required.", nameof(capacities));
        if (baseSlots < 0 || baseSlots > MaximumNativeSlots) throw new ArgumentOutOfRangeException(nameof(baseSlots));
        long total = baseSlots;
        foreach (int capacity in capacities)
        {
            if (capacity < 0) throw new ArgumentException("Container capacities cannot be negative.", nameof(capacities));
            total += capacity;
        }
        if (total > MaximumNativeSlots) throw new ArgumentOutOfRangeException(nameof(capacities), "Native storage serialization limit exceeded.");
        return (int)total;
    }

    // One entry per occupied cargo slot, including its current position. Stack identity/count never changes.
    // Return a destination for each entry, retaining positions when possible and filling only vacant low slots.
    public static bool TryPlanCargoPositions(int capacity, int[] occupiedPositions, out int[] destinations)
    {
        destinations = null;
        if (capacity < 0 || capacity > MaximumNativeSlots || occupiedPositions == null)
            throw new ArgumentException("Invalid cargo capacity/positions.");
        var seen = new System.Collections.Generic.HashSet<int>();
        foreach (int position in occupiedPositions)
            if (position < 0 || !seen.Add(position)) throw new ArgumentException("Cargo positions must be unique and nonnegative.");
        if (occupiedPositions.Length > capacity) return false;
        var result = (int[])occupiedPositions.Clone();
        var used = new bool[capacity];
        foreach (int position in result) if (position < capacity) used[position] = true;
        int free = 0;
        for (int index = 0; index < result.Length; index++)
        {
            if (result[index] < capacity) continue;
            while (used[free]) free++;
            result[index] = free;
            used[free] = true;
        }
        destinations = result;
        return true;
    }
}
