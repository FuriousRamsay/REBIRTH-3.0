using System;
using System.IO;

// Detached authenticated native snapshot data. Never reads a remote EntityPlayer fallback.
public static class RebirthPlayerDataInventory
{
    public static ItemStack[] ReadSlots(PlayerDataFile data, bool bag)
    {
        MemoryStream blob = bag ? data?.bagData : data?.inventoryData;
        if (blob == null || blob.Length == 0 || blob.Length > 4194304) return null;
        try
        {
            using (var stream = new MemoryStream(blob.ToArray(), false))
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(false))
            {
                reader.SetBaseStream(stream);
                if (reader.ReadByte() != (bag ? 2 : 1)) return null;
                long gridStart = stream.Position;
                ushort gridVersion = reader.ReadUInt16();
                Vector2i size = StreamUtils.ReadVector2i(reader);
                int count = reader.ReadInt16();
                int maximum = bag ? 169 : 20;
                if (gridVersion < 1 || gridVersion > 2 || size.x < 0 || size.y < 0
                    || size.x > maximum || size.y > maximum || count < 0 || count > maximum
                    || (long)size.x * size.y != count) return null;
                stream.Position = gridStart;
                var grid = ItemStackGrid.Read(reader, StreamModeRead.Persistency);
                if (!bag) reader.ReadByte(); // Native selected slot follows the toolbelt grid.
                if (stream.Position != stream.Length) return null;
                return grid.CloneItems();
            }
        }
        catch (Exception) { return null; }
    }
}