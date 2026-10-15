using System;
using System.IO;

// State-bearing module items require the mobile-station placement adapter.
// Never strip this metadata or treat a decode failure as a new empty station.
public static class RebirthCamperPortableStation
{
    public const string MetadataKey = "RebirthCamperStation";
    private const uint Magic = 0x31505352; // RSP1
    private const uint AddressedMagic = 0x32505352; // RSP2: source vehicle correlation, never authority

    public static ItemStack Capture(ItemStack installed, RebirthCamperStationState state)
        => CaptureCore(installed,state,default(RebirthCamperStationAddress));

    public static ItemStack Capture(ItemStack installed, RebirthCamperStationState state,
        RebirthCamperStationAddress sourceAddress)
    {
        if(!sourceAddress.IsValid || state==null || sourceAddress.Installation!=state.InstallationId)
            throw new ArgumentException("Exact source installation address required.");
        return CaptureCore(installed,state,sourceAddress);
    }

    private static ItemStack CaptureCore(ItemStack installed, RebirthCamperStationState state,
        RebirthCamperStationAddress sourceAddress)
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Capture stations on the game thread.");
        if (installed == null || state == null) throw new ArgumentNullException();
        if (installed.itemValue == null || installed.count != 1)
            throw new ArgumentException("Single native station item required.");
        byte[] snapshot = RebirthCamperStationSnapshot.Capture(state.InstallationId, state);
        var value = installed.itemValue.Clone();
        if (value.Metadata != null && !value.Metadata.ContainsKey(MetadataKey) && value.Metadata.Count >= byte.MaxValue)
            throw new InvalidDataException("Native item metadata entry limit exceeded.");
        if (value.Metadata != null)
            foreach (var entry in value.Metadata)
                if (entry.Value == null)
                    throw new InvalidDataException("Native station metadata is incomplete; retain installed station.");
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(sourceAddress.IsValid ? AddressedMagic : Magic);
            writer.Write(state.InstallationId.ToByteArray());
            if(sourceAddress.IsValid)writer.Write(sourceAddress.Vehicle.ToByteArray());
            writer.Write(snapshot.Length);
            writer.Write(snapshot);
            writer.Flush();
            value.SetMetadata(MetadataKey, Convert.ToBase64String(stream.ToArray()));
        }
        return new ItemStack(value, 1);
    }

    public static bool TryRestore(ItemStack item, out RebirthCamperStationState state)
    {
        state = null;
        Guid id; RebirthCamperStationAddress source; byte[] snapshot;
        if(!TryReadFrame(item,out id,out source,out snapshot))return false;
        state = RebirthCamperStationSnapshot.Restore(id,item,snapshot);
        // An item does not process crafting. Preserve remaining work/fuel/melt
        // timers, but make the first live tick establish a new clock baseline.
        state.lastTickTime = 0;
        return true;
    }

    // Correlation only. Caller must independently authenticate the original
    // owner/publication and destination lease before authorizing relocation.
    // RSP1 remains readable but cannot supply a source vehicle address.
    public static bool TryGetSourceAddress(ItemStack item,out RebirthCamperStationAddress sourceAddress)
    {
        Guid id; byte[] snapshot;
        return TryReadFrame(item,out id,out sourceAddress,out snapshot) && sourceAddress.IsValid;
    }

    private static bool TryReadFrame(ItemStack item,out Guid id,
        out RebirthCamperStationAddress sourceAddress,out byte[] snapshot)
    {
        id=Guid.Empty;sourceAddress=default(RebirthCamperStationAddress);snapshot=null;
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Restore stations on the game thread.");
        if (item == null || item.count != 1 || item.itemValue == null) throw new ArgumentException("Single station item required.");
        string encoded;
        if (!item.itemValue.TryGetMetadata(MetadataKey, out encoded)) return false;
        if (string.IsNullOrEmpty(encoded)) throw new InvalidDataException("Empty portable station state; retain item.");
        using (var stream = new MemoryStream(Convert.FromBase64String(encoded), false))
        using (var reader = new BinaryReader(stream))
        {
            uint magic=reader.ReadUInt32();
            if (magic != Magic && magic != AddressedMagic) throw new InvalidDataException("Unsupported portable station.");
            id = new Guid(reader.ReadBytes(16));
            if(magic==AddressedMagic)
            {
                Guid vehicle=new Guid(reader.ReadBytes(16));
                if(vehicle==Guid.Empty || id==Guid.Empty)throw new InvalidDataException("Invalid portable source address.");
                sourceAddress=new RebirthCamperStationAddress(vehicle,id);
            }
            int length = reader.ReadInt32();
            if (id == Guid.Empty || length <= 0 || length != stream.Length - stream.Position)
                throw new InvalidDataException("Invalid portable station identity or frame.");
            snapshot=reader.ReadBytes(length);
            return true;
        }
    }
}
