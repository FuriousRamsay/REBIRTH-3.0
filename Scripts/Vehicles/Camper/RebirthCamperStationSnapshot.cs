using System;
using System.IO;

// Captures native persistency bytes, including queue attribution and station timers.
// This is a snapshot primitive, not a moving station tick or a network protocol.
public static class RebirthCamperStationSnapshot
{
    private const uint Magic = 0x34534352; // RCS4; provisional earlier frames were never deployed.

    // Decode into a new detached model only. Never overwrite a live station on a failed read.
    public static RebirthCamperStationState Restore(Guid installationId, ItemStack installedItem, byte[] payload)
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Restore station state on the game thread.");
        if (payload == null || payload.Length < 22) throw new InvalidDataException("Incomplete station frame.");
        using (var stream = new MemoryStream(payload, false))
        {
            int[][] counts = new int[6][];
            long revision;
            using (var header = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                if (header.ReadUInt32() != Magic) throw new InvalidDataException("Unsupported station frame.");
                if (installationId == Guid.Empty || new Guid(header.ReadBytes(16)) != installationId)
                    throw new InvalidDataException("Station identity mismatch.");
                BlockValue installedBlock;
                WorkstationData configuration;
                if (!RebirthCamperCraftingStation.TryResolve(installedItem, out installedBlock, out configuration)
                    || header.ReadString() != installedBlock.Block.GetBlockName())
                    throw new InvalidDataException("Installed station type mismatch.");
                string configuredMaterials = installedBlock.Block.Properties.GetString("Workstation", "InputMaterials") ?? "";
                if (header.ReadString() != configuredMaterials)
                    throw new InvalidDataException("Station material definitions changed; retain the original payload.");
                if (header.ReadString() != (installedBlock.Block.Properties.GetString("Workstation", "Modules") ?? ""))
                    throw new InvalidDataException("Station module definitions changed; retain the original payload.");
                if (header.ReadString() != (installedBlock.Block.Properties.GetString("RebirthCraftingStation") ?? ""))
                    throw new InvalidDataException("Station recipe-family alias changed; retain the original payload.");
                revision = header.ReadInt64();
                if (revision < 0) throw new InvalidDataException("Invalid station revision.");
                for (int group = 0; group < counts.Length; group++)
                {
                    int length = header.ReadInt32();
                    int maximum = group == 5 ? short.MaxValue : byte.MaxValue;
                    if (length < 0 || length > maximum || length > (stream.Length - stream.Position) / 4)
                        throw new InvalidDataException("Invalid station count table.");
                    counts[group] = new int[length];
                    for (int slot = 0; slot < length; slot++)
                    {
                        counts[group][slot] = header.ReadInt32();
                        if (counts[group][slot] < 0) throw new InvalidDataException("Negative station stock.");
                    }
                }
                ValidateNativeHeader(header);
            }
            var restored = new RebirthCamperStationState(installationId, installedItem);
            var materialValues = new ItemValue[restored.MaterialNames.Length];
            for (int index = 0; index < materialValues.Length; index++)
                materialValues[index] = restored.Input[index + 3].itemValue.Clone();
            using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                restored.read(reader, StreamModeRead.Persistency);
                if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected station frame trailing data.");
            }
            if (restored.Input.Length != 3 + restored.MaterialNames.Length)
                throw new InvalidDataException("Station input configuration changed; retain the original payload for recovery.");
            ValidateInputLayout(restored);
            var groups = CountGroups(restored);
            for (int group = 0; group < groups.Length; group++)
            {
                if (groups[group].Length != counts[group].Length)
                    throw new InvalidDataException("Station stock table dimensions disagree with native state.");
                for (int slot = 0; slot < groups[group].Length; slot++)
                {
                    ItemStack stack = groups[group][slot];
                    int exact = counts[group][slot];
                    if (stack == null || stack.count != Math.Min(exact, ushort.MaxValue))
                        throw new InvalidDataException("Station stock table disagrees with native state.");
                    stack.count = exact;
                }
            }
            // Native zero-count stacks omit ItemValue; restore the configured molten-unit
            // identity so later smelting starts in the correct slot without inventing stock.
            for (int index = 0; index < materialValues.Length; index++)
            {
                ItemStack material = restored.Input[index + 3];
                if (material.count == 0) material.itemValue = materialValues[index];
                else if (material.itemValue == null || material.itemValue.type != materialValues[index].type)
                    throw new InvalidDataException("Molten stock belongs to a different material definition.");
            }
            restored.RestoreRevision(revision);
            return restored;
        }
    }

    public static byte[] Capture(Guid installationId, TileEntityWorkstation station)
    {
        if (installationId == Guid.Empty) throw new ArgumentException("Station installation identity is required.");
        if (station == null) throw new ArgumentNullException(nameof(station));
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Capture station state on the game thread.");
        var detached = station as RebirthCamperStationState;
        if (detached == null || detached.InstallationId != installationId)
            throw new InvalidOperationException("Capture requires the matching vehicle-owned station model.");
        ValidateInputLayout(station);
        // Preserve full counts in the envelope; native bytes still carry actual ItemValues.
        var groups = CountGroups(station);
        if (station.Queue == null || station.Queue.Length > byte.MaxValue
            || station.currentMeltTimesLeft == null || station.currentMeltTimesLeft.Length > byte.MaxValue)
            throw new InvalidDataException("Unsupported native station queue/timer dimensions.");
        for (int group = 0; group < groups.Length; group++)
        {
            var slots = groups[group];
            int maximum = group == 5 ? short.MaxValue : byte.MaxValue;
            if (slots == null || slots.Length > maximum) throw new InvalidDataException("Unsupported native station array size.");
            foreach (var stack in slots)
                if (stack == null || stack.count < 0 || stack.itemValue == null)
                    throw new InvalidDataException("Invalid station stock.");
        }
        using (var stream = new MemoryStream())
        {
            using (var header = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                header.Write(Magic);
                header.Write(installationId.ToByteArray());
                header.Write(detached.InstalledBlock.Block.GetBlockName());
                header.Write(detached.InstalledBlock.Block.Properties.GetString("Workstation", "InputMaterials") ?? "");
                header.Write(detached.InstalledBlock.Block.Properties.GetString("Workstation", "Modules") ?? "");
                header.Write(detached.InstalledBlock.Block.Properties.GetString("RebirthCraftingStation") ?? "");
                header.Write(detached.Revision);
                foreach (var slots in groups)
                {
                    header.Write(slots.Length);
                    foreach (var stack in slots) header.Write(stack.count);
                }
                header.Flush();
            }
            long nativeBodyStart = stream.Position;
            using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(stream);
                station.write(writer, StreamModeWrite.Persistency);
                writer.Flush();
                long end = stream.Position;
                stream.Position = nativeBodyStart;
                using (var header = new BinaryReader(stream, System.Text.Encoding.UTF8, true)) ValidateNativeHeader(header);
                stream.Position = end;
                return stream.ToArray();
            }
        }
    }

    private static void ValidateInputLayout(TileEntityWorkstation station)
    {
        int physical=station.InputSlotCount;
        if(physical!=3 || station.MaterialNames==null || station.Input==null
            || station.Input.Length!=physical+station.MaterialNames.Length
            || station.lastInput==null || station.lastInput.Length!=physical
            || station.currentMeltTimesLeft==null || station.currentMeltTimesLeft.Length!=physical)
            throw new InvalidDataException("Unqualified mobile station input/timer layout; retain state for recovery.");
        foreach(float timer in station.currentMeltTimesLeft)
            if(float.IsNaN(timer)||float.IsInfinity(timer))
                throw new InvalidDataException("Invalid station melt timer.");
    }

    private static void ValidateNativeHeader(BinaryReader reader)
    {
        long start = reader.BaseStream.Position;
        try
        {
            if (reader.ReadUInt16() != 19) throw new InvalidDataException("Unqualified native tile-entity persistence version.");
            // Audited TileEntity.write: three Int32 chunk coordinates, then UInt64 heap timestamp.
            reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32(); reader.ReadUInt64();
            if (reader.ReadByte() != 50) throw new InvalidDataException("Unqualified native workstation persistence version.");
        }
        finally { reader.BaseStream.Position = start; }
    }


    // Native persistence also writes previous input and pending completion stacks.
    // They use the same UInt16 ItemStack count codec as the visible inventories.
    private static ItemStack[][] CountGroups(TileEntityWorkstation station)
    {
        var receipts = station.CraftCompleteList;
        var completed = new ItemStack[receipts == null ? 0 : receipts.Count];
        for (int index = 0; index < completed.Length; index++)
        {
            if (receipts[index] == null) throw new InvalidDataException("Missing completion receipt.");
            completed[index] = receipts[index].CraftedItemStack;
        }
        return new[] { station.Tools, station.Fuel, station.Input, station.Output, station.lastInput, completed };
    }
}
