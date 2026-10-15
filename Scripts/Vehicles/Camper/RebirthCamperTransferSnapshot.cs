using System;
using System.IO;

// One recovery frame for both sides of a management operation. It is server
// data, never a client request or permission to publish inventory.
public sealed class RebirthCamperTransferSnapshot
{
    public RebirthCamperStorageInstallations Storage { get; private set; }
    public RebirthCamperStationInstallations Stations { get; private set; }
    public ItemStack[] Cargo { get; private set; }
    public ItemStack Cursor { get; private set; }
    // -1 is native cursor custody; otherwise Cursor is the exact backpack cell
    // participating in a quick transfer. Other backpack cells are never replayed.
    public int BackpackSlot { get; private set; } = -1;
    private const uint Magic = 0x31544352; // RCT1
    private const int MaximumFrame = 64 * 1024 * 1024;

    public static byte[] Capture(RebirthCamperStorageInstallations storage,
        RebirthCamperStationInstallations stations, ItemStack[] cargo, ItemStack cursor, int backpackSlot = -1)
    {
        if (storage == null || stations == null || storage.VehicleIdentity != stations.VehicleIdentity
            || cargo == null || cargo.Length != storage.Capacity || cursor == null)
            throw new ArgumentException("Matching module, cargo and cursor state required.");
        byte[][] frames = { storage.Capture(), stations.Capture(),
            RebirthCamperInventorySnapshot.Capture(cargo), RebirthCamperInventorySnapshot.Capture(new[] { cursor }) };
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Magic); writer.Write(storage.VehicleIdentity.ToByteArray());
            foreach (byte[] frame in frames)
            {
                if (frame.Length == 0 || frame.Length > MaximumFrame) throw new InvalidDataException("Transfer recovery frame is too large.");
                writer.Write(frame.Length); writer.Write(frame);
            }
            if(backpackSlot < -1)throw new ArgumentOutOfRangeException(nameof(backpackSlot));
            if(backpackSlot >= 0)writer.Write(backpackSlot);
            writer.Flush();
            if (stream.Length > MaximumFrame) throw new InvalidDataException("Transfer recovery payload exceeds journal limit.");
            return stream.ToArray();
        }
    }

    public static RebirthCamperTransferSnapshot Restore(Guid vehicleIdentity, byte[] payload)
    {
        if (vehicleIdentity == Guid.Empty || payload == null || payload.Length > MaximumFrame)
            throw new ArgumentException("Trusted vehicle identity and bounded recovery data required.");
        using (var stream = new MemoryStream(payload, false))
        using (var reader = new BinaryReader(stream))
        {
            if (reader.ReadUInt32() != Magic || new Guid(reader.ReadBytes(16)) != vehicleIdentity)
                throw new InvalidDataException("Transfer snapshot identity mismatch.");
            byte[] containers = ReadFrame(reader), crafting = ReadFrame(reader), cargoBytes = ReadFrame(reader), cursorBytes = ReadFrame(reader);
            int backpackSlot=-1;
            if(stream.Length-stream.Position==sizeof(int))
            {backpackSlot=reader.ReadInt32();if(backpackSlot<0)throw new InvalidDataException("Invalid quick-transfer backpack cell.");}
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected transfer snapshot trailing data.");
            var storage = RebirthCamperStorageInstallations.Restore(vehicleIdentity, containers);
            var stations = RebirthCamperStationInstallations.Restore(vehicleIdentity, crafting);
            var cargo = RebirthCamperInventorySnapshot.Restore(cargoBytes);
            var cursor = RebirthCamperInventorySnapshot.Restore(cursorBytes);
            if (cargo.Length != storage.Capacity || cursor.Length != 1)
                throw new InvalidDataException("Transfer recovery inventory dimensions disagree.");
            return new RebirthCamperTransferSnapshot { Storage=storage, Stations=stations, Cargo=cargo, Cursor=cursor[0], BackpackSlot=backpackSlot };
        }
    }
    public byte[] Capture() => Capture(Storage,Stations,Cargo,Cursor,BackpackSlot);
    private static byte[] ReadFrame(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length <= 0 || length > MaximumFrame || length > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException("Incomplete transfer recovery frame.");
        return reader.ReadBytes(length);
    }
}
