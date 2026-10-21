using System;
using System.IO;
using System.Text;

// Full native values plus Int32 counts: ItemStack's compact network count must
// not truncate recovery snapshots for molten stock or expanded vehicle cargo.
public static class RebirthCamperInventorySnapshot
{
    private const uint Magic = 0x31534352; // RCS1 inventory frame
    private const int MaximumItemBytes = 8 * 1024 * 1024;
    public static byte[] Capture(ItemStack[] slots)
    {
        RequireThread();
        if (slots == null || slots.Length > short.MaxValue) throw new ArgumentException("Valid native inventory dimensions required.");
        using (var memory = new MemoryStream())
        using (var header = new BinaryWriter(memory, Encoding.UTF8, true))
        {
            header.Write(Magic); header.Write(slots.Length);
            foreach (ItemStack stack in slots)
            {
                if (stack == null || stack.itemValue == null || stack.count < 0)
                    throw new InvalidDataException("Incomplete inventory stack; retain original inventory.");
                if (stack.itemValue.Metadata != null)
                {
                    if (stack.itemValue.Metadata.Count > byte.MaxValue) throw new InvalidDataException("Too many native item metadata entries.");
                    foreach (var entry in stack.itemValue.Metadata)
                        if (entry.Value == null) throw new InvalidDataException("Incomplete native item metadata.");
                }
                byte[] item;
                using (var native = new MemoryStream())
                using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
                {
                    Encoding previous=writer.Encoding;
                    try{writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(native);stack.itemValue.Write(writer);writer.Flush();item=native.ToArray();}
                    finally{writer.Encoding=previous;}
                }
                if (item.Length == 0 || item.Length > MaximumItemBytes) throw new InvalidDataException("Native item frame is too large.");
                header.Write(stack.count); header.Write(item.Length); header.Write(item);
            }
            header.Flush(); return memory.ToArray();
        }
    }

    public static ItemStack[] Restore(byte[] payload)
    {
        RequireThread();
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        using (var memory = new MemoryStream(payload, false))
        using (var header = new BinaryReader(memory, Encoding.UTF8, true))
        {
            if (header.ReadUInt32() != Magic) throw new InvalidDataException("Unsupported native inventory snapshot.");
            int length = header.ReadInt32();
            if (length < 0 || length > short.MaxValue || length > (memory.Length - memory.Position) / 8)
                throw new InvalidDataException("Invalid snapshot dimensions.");
            var result = new ItemStack[length];
            for (int slot = 0; slot < length; slot++)
            {
                int count = header.ReadInt32(), size = header.ReadInt32();
                if (count < 0 || size <= 0 || size > MaximumItemBytes || size > memory.Length - memory.Position)
                    throw new InvalidDataException("Invalid item count or frame.");
                byte[] item = header.ReadBytes(size);
                ItemValue value;
                if (!RebirthNativeItemCodec.TryDecode(Convert.ToBase64String(item), out value))
                {
                    // The canonical item reader intentionally rejects the empty sentinel.
                    // Accept only its exact current native encoding, never a failed decode
                    // of a populated item (including malformed/unsupported metadata).
                    value = new ItemValue();
                    using (var empty = new MemoryStream())
                    using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
                    {
                        Encoding previous=writer.Encoding;
                        try{writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(empty);value.Write(writer);writer.Flush();}
                        finally{writer.Encoding=previous;}
                        byte[] expected = empty.ToArray();
                        if (count != 0 || expected.Length != item.Length)
                            throw new InvalidDataException("Unsupported canonical native item snapshot; retain original custody.");
                        for (int i = 0; i < expected.Length; i++)
                            if (expected[i] != item[i]) throw new InvalidDataException("Invalid empty native item snapshot.");
                    }
                }
                result[slot] = new ItemStack(value, count);
            }
            if (memory.Position != memory.Length) throw new InvalidDataException("Unexpected inventory trailing bytes.");
            return result;
        }
    }
    private static void RequireThread()
    { if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Native inventory snapshots require the game thread."); }
}
