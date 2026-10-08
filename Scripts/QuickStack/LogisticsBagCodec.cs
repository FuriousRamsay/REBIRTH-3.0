using System;
using System.IO;

// Request-only consistency witness, not native Bag persistence. Never import client
// locks/preferences/ownership. Matched REBIRTH builds use the explicit v3 envelope.
internal static class LogisticsBagCodec
{
    internal const int MaximumSlots = 256;
    internal const byte SnapshotVersion = 3;

    internal static Bag Capture(Bag source)
    {
        if (source == null) return null;
        int count = ValidateCount(source.ItemGrid.Length);
        Bag copy = Create(count);
        for (int i = 0; i < count; i++)
            copy.ItemGrid[i] = source.ItemGrid[i]; // native setter clones full ItemValue
        return copy;
    }

    internal static void Write(PooledBinaryWriter writer, Bag snapshot)
    {
        if (writer == null || snapshot == null) throw new ArgumentNullException();
        int count = ValidateCount(snapshot.ItemGrid.Length);
        writer.Write(SnapshotVersion);
        writer.Write((ushort)count);
        for (int i = 0; i < count; i++) snapshot.ItemGrid[i].Write(writer);
    }

    internal static Bag Read(PooledBinaryReader reader)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));
        if (reader.ReadByte() != SnapshotVersion)
            throw new InvalidDataException("Unsupported logistics snapshot version; matching REBIRTH builds required.");
        int count = ValidateCount(reader.ReadUInt16()); // bound before grid allocation
        Bag snapshot = Create(count);
        for (int i = 0; i < count; i++) snapshot.ItemGrid[i].Read(reader);
        return snapshot;
    }

    private static int ValidateCount(int count)
    {
        if (count < 0 || count > MaximumSlots)
            throw new InvalidDataException("Logistics inventory exceeds slot limit.");
        return count;
    }

    private static Bag Create(int count)
    {
        return new Bag(new Vector2i(count, 1), XUiC_ItemStack.StackLocationTypes.Backpack, null);
    }
}