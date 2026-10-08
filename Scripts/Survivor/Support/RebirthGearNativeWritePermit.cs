using System;

// One exact native setter entry, not an ambient bypass for callbacks. Dispatcher
// owns authentication/hold; adapter prepares exact payloads before opening this scope.
internal static class RebirthGearNativeWritePermit
{
    internal enum Kind { BagSlot, BagBacking, BeltSlot }
    private sealed class Entry
    {
        internal Kind Operation; internal object Inventory, Payload;
        internal int Index, Count; internal bool Consumed; internal Func<bool> Current;
    }
    [ThreadStatic] private static Entry active;
    internal static void Execute(Kind kind, object inventory, int index, object payload, int count,
        Func<bool> current, Action write)
    {
        if (active != null || inventory == null || payload == null || current == null || write == null ||
            !current()) throw new InvalidOperationException("Gear write permission unavailable.");
        var entry = new Entry { Operation = kind, Inventory = inventory, Index = index,
            Payload = payload, Count = count, Current = current };
        active = entry;
        try
        {
            write();
            if (!entry.Consumed) throw new InvalidOperationException("Gear native setter did not consume its permission.");
        }
        finally { active = null; }
    }
    internal static bool TryConsume(Kind kind, object inventory, int index, object payload, int count)
    {
        var entry = active;
        if (entry == null || entry.Consumed || entry.Operation != kind || entry.Index != index ||
            entry.Count != count || !ReferenceEquals(entry.Inventory, inventory) ||
            !ReferenceEquals(entry.Payload, payload)) return false;
        try
        {
            if (!entry.Current()) return false;
            entry.Consumed = true;
            return true;
        }
        catch { return false; }
    }
}