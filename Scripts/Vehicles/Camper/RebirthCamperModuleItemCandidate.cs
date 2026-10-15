using System;
using System.IO;

// Prepare a portable native item candidate. No live vehicle/player inventory is
// mutated here; the server transfer coordinator must commit it atomically.
public static class RebirthCamperModuleItemCandidate
{
    private const string MetadataKey = "RebirthCamperModules";
    private const uint Magic = 0x314D5652; // RVM1

    public static ItemValue Prepare(ItemValue source, RebirthCamperStorageInstallations storage,
        RebirthCamperStationInstallations stations)
    {
        RequireCamper(source);
        if (source.Metadata != null)
        {
            int nextCount = source.Metadata.Count + (source.Metadata.ContainsKey(MetadataKey) ? 0 : 1);
            if (nextCount > byte.MaxValue)
                throw new InvalidDataException("Native item metadata entry limit exceeded; preserve source item.");
            foreach (var entry in source.Metadata)
                if (entry.Value == null)
                    throw new InvalidDataException("Native item metadata contains an incomplete entry; preserve source item.");
        }
        if (storage == null || stations == null || storage.VehicleIdentity != stations.VehicleIdentity)
            throw new ArgumentException("Matching vehicle-owned container and station installations required.");
        byte[] containers = storage.Capture();
        byte[] crafting = stations.Capture();
        byte[] payload;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write(storage.VehicleIdentity.ToByteArray());
            writer.Write(containers.Length); writer.Write(containers);
            writer.Write(crafting.Length); writer.Write(crafting);
            writer.Flush();
            payload = stream.ToArray();
        }
        var candidate = source.Clone();
        candidate.SetMetadata(MetadataKey, Convert.ToBase64String(payload));
        return candidate;
    }

    // Caller supplies trusted vehicle identity. Decode both manifests before
    // adoption, preserving the source item/recovery bytes when either fails.
    public static bool TryRestore(Guid identity, ItemValue item,
        out RebirthCamperStorageInstallations storage, out RebirthCamperStationInstallations stations)
    {
        storage = null; stations = null;
        RequireCamper(item);
        if (identity == Guid.Empty) throw new ArgumentException("Trusted vehicle identity required.");
        string encoded;
        if (!item.TryGetMetadata(MetadataKey, out encoded)) return false;
        if (string.IsNullOrEmpty(encoded)) throw new InvalidDataException("Empty camper module metadata; retain source item.");
        byte[] payload = Convert.FromBase64String(encoded);
        using (var stream = new MemoryStream(payload, false))
        using (var reader = new BinaryReader(stream))
        {
            if (reader.ReadUInt32() != Magic || new Guid(reader.ReadBytes(16)) != identity)
                throw new InvalidDataException("Camper module frame/vehicle identity mismatch.");
            byte[] containers = ReadFrame(reader);
            byte[] crafting = ReadFrame(reader);
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected camper module trailing data.");
            var restoredStorage = RebirthCamperStorageInstallations.Restore(identity, containers);
            var restoredStations = RebirthCamperStationInstallations.Restore(identity, crafting);
            storage = restoredStorage;
            stations = restoredStations;
            return true;
        }
    }

    private static byte[] ReadFrame(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length <= 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Incomplete camper module manifest.");
        return reader.ReadBytes(length);
    }
    private static void RequireCamper(ItemValue item)
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Prepare/restore module items on the game thread.");
        if (item == null || item.IsEmpty() || item.ItemClass == null
            || !item.ItemClass.GetItemName().Equals("rebirthCamper80sPlaceable", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Exact native camper item required.");
    }
}
