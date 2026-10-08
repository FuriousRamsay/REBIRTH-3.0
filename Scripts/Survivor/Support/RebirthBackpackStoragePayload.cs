using System;
using System.IO;

// Measure the complete physical backpack, including both sections and all other
// metadata, against the exact gear transaction item budget. Never trim contents.
public static class RebirthBackpackStoragePayload
{
    public const int MaxItemBytes = 196608;

    public static bool Fits(ItemValue backpack)
    {
        if (backpack == null || backpack.IsEmpty() || backpack.ItemClass == null
            || backpack.Metadata != null && backpack.Metadata.Count > byte.MaxValue) return false;
        try
        {
            using (var stream = new MemoryStream())
            using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);
                ItemValue.Write(backpack, writer);
                writer.Flush();
                return stream.Length > 0 && stream.Length <= MaxItemBytes;
            }
        }
        catch (Exception) { return false; }
    }
}