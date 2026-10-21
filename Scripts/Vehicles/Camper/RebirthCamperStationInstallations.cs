using System;
using System.Collections.Generic;
using System.IO;

// Vehicle-owned installation model. Inventory transfers and authority belong to
// the vehicle coordinator; constructing this model does not consume an item.
public sealed class RebirthCamperStationInstallations
{
    public sealed class Installation
    {
        private readonly ItemStack item;
        public Guid Id { get { return State.InstallationId; } }
        public RebirthCamperStationState State { get; private set; }
        public ItemStack InstalledItem { get { return new ItemStack(item.itemValue.Clone(), 1); } }

        internal Installation(ItemStack source)
        {
            item = new ItemStack(source.itemValue.Clone(), 1);
            RebirthCamperStationState restored;
            State = RebirthCamperPortableStation.TryRestore(item, out restored)
                ? restored : new RebirthCamperStationState(Guid.NewGuid(), item);
        }

        internal Installation(ItemStack source, RebirthCamperStationState restored)
        {
            item = new ItemStack(source.itemValue.Clone(), 1);
            State = restored;
        }
    }

    private readonly Installation[] slots = new Installation[RebirthCamperCraftingStation.ModuleSlots];
    public Guid VehicleIdentity { get; private set; }
    public long Revision { get; private set; }

    public RebirthCamperStationInstallations(Guid vehicleIdentity)
    {
        if (vehicleIdentity == Guid.Empty) throw new ArgumentException("Vehicle identity is required.");
        VehicleIdentity = vehicleIdentity;
    }

    public Installation GetSlot(int slot)
    {
        CheckSlot(slot);
        return slots[slot];
    }

    public Installation Find(Guid installationId)
    {
        if (installationId == Guid.Empty) return null;
        foreach (Installation installation in slots)
            if (installation != null && installation.Id == installationId) return installation;
        return null;
    }

    // Snapshot decoding remains detached. Only a verified authoritative module
    // adoption may retain unchanged live objects: paid queue and terminal
    // operations bind those exact objects, not merely their serialized IDs.
    internal void RetainUnchangedStates(RebirthCamperStationInstallations current, bool adopt = true)
    {
        RequireGameThread();
        if (current == null || current.VehicleIdentity != VehicleIdentity)
            throw new InvalidOperationException("Matching current vehicle stations required.");
        var retained = new Installation[slots.Length];
        for (int slot = 0; slot < slots.Length; slot++)
        {
            var proposed = slots[slot];
            if (proposed == null) continue;
            var original = current.Find(proposed.Id);
            if (original == null) continue;
            if (!Equal(RebirthCamperInventorySnapshot.Capture(new[] { proposed.InstalledItem }),
                    RebirthCamperInventorySnapshot.Capture(new[] { original.InstalledItem })) ||
                !Equal(RebirthCamperStationSnapshot.Capture(proposed.Id, proposed.State),
                    RebirthCamperStationSnapshot.Capture(original.Id, original.State)))
                throw new InvalidOperationException("An existing station changed during module adoption.");
            retained[slot] = original;
        }
        // Validate every match before mutating the detached candidate.
        if (adopt)
            for (int slot = 0; slot < slots.Length; slot++)
                if (retained[slot] != null) slots[slot] = retained[slot];
    }
    private static bool Equal(byte[] left, byte[] right)
    {
        if (left.Length != right.Length) return false;
        for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
        return true;
    }

    // Only call after the authoritative coordinator validates the associated
    // inventory transfer. Slot validation and construction finish before mutation.
    public Installation Install(int slot, ItemStack item)
    {
        if(item?.itemValue?.Metadata?.ContainsKey(RebirthCamperStationRemovalContents.ContentsKey)==true)
            throw new InvalidOperationException("Return pending station contents before installing the carrier.");
        RequireGameThread();
        CheckSlot(slot);
        if (slots[slot] != null) throw new InvalidOperationException("Station position is occupied.");
        BlockValue block;
        WorkstationData data;
        if (!RebirthCamperCraftingStation.TryResolve(item, out block, out data))
            throw new ArgumentException("Install one actual crafting station item.");
        foreach (var existing in slots)
        {
            BlockValue installedBlock;
            WorkstationData installedData;
            if (existing != null && RebirthCamperCraftingStation.TryResolve(existing.InstalledItem, out installedBlock, out installedData)
                && installedBlock.type == block.type)
                throw new InvalidOperationException("This type of crafting station is already installed.");
        }
        var installation = new Installation(item);
        if (Find(installation.Id) != null) throw new InvalidOperationException("Station identity already installed.");
        long nextRevision = checked(Revision + 1);
        slots[slot] = installation;
        Revision = nextRevision;
        return installation;
    }

    // Returns a state-bearing item; server coordinator must transfer it atomically
    // and prevent ordinary terrain placement from dropping its portable contents.
    public ItemStack Remove(int slot, long expectedRevision)
    {
        RequireGameThread();
        CheckSlot(slot);
        if (Revision != expectedRevision || slots[slot] == null)
            throw new InvalidOperationException("Station inventory changed or position is empty.");
        var installation = slots[slot];
        if (RebirthCamperStationWindowSession.IsInUse(installation.State))
            throw new InvalidOperationException("Close the station window before removing its installation.");
        ItemStack portable = RebirthCamperStationRemovalContents.Prepare(VehicleIdentity,installation).Carrier;
        long next = checked(Revision + 1);
        slots[slot] = null;
        Revision = next;
        return portable;
    }

    // Reordering retains the very same native inventory/queue/timer object.
    public void Move(int from, int to)
    {
        RequireGameThread();
        CheckSlot(from);
        CheckSlot(to);
        if (slots[from] == null) throw new InvalidOperationException("Station position is empty.");
        if (from == to) return;
        if (slots[to] != null) throw new InvalidOperationException("Destination position is occupied.");
        long nextRevision = checked(Revision + 1);
        slots[to] = slots[from];
        slots[from] = null;
        Revision = nextRevision;
    }

    // Portable manifest only: the vehicle coordinator must own persistence and
    // access. Never accept a client manifest as an inventory transfer request.
    public byte[] Capture()
    {using(var stream=new MemoryStream()){WriteTo(stream);return stream.ToArray();}}
    internal byte[] CaptureCharged(IRebirthStationSnapshotByteBudget budget,out IDisposable allocation)
    {
        allocation=null;if(budget==null)return Capture();IDisposable scratch=null;
        try
        {
            if(!budget.TryReserveAllocation(512*1024,out scratch))throw new InvalidOperationException("Module serialization workspace pressure.");
            long length;
            using(var count=new RebirthCamperSavedVehicle.CountStream(budget.MaximumSnapshotBytes)){WriteTo(count);length=count.Length;}
            if(length<1||length>budget.MaximumSnapshotBytes||!budget.TryReserveAllocation(checked(length+4096),out allocation))throw new InvalidOperationException("Module manifest allocation pressure.");
            var bytes=new byte[(int)length];using(var stream=new MemoryStream(bytes,0,bytes.Length,true,true))
            {WriteTo(stream);if(stream.Position!=length)throw new InvalidDataException("Module manifest length changed.");}
            return bytes;
        }
        catch{allocation?.Dispose();allocation=null;throw;}
        finally{scratch?.Dispose();}
    }
    private void WriteTo(Stream stream)
    {
        RequireGameThread();
        using (var header = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            header.Write(0x314D4352u); // RCM1
            header.Write(VehicleIdentity.ToByteArray());
            header.Write(Revision);
            header.Write(slots.Length);
            foreach (Installation installation in slots)
            {
                header.Write(installation != null);
                if (installation == null) continue;
                header.Write(installation.Id.ToByteArray());
                header.Flush();
                using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
                {
                    writer.SetBaseStream(stream);
                    installation.InstalledItem.itemValue.Write(writer);
                    writer.Flush();
                }
                int length=RebirthCamperStationSnapshot.MeasureLength(installation.Id,installation.State,int.MaxValue);
                header.Write(length);header.Flush();
                RebirthCamperStationSnapshot.WriteTo(installation.Id,installation.State,stream,stream.CanRead);
            }
            header.Flush();

        }
    }

    // Decode completely into a new model before adoption; failed reads cannot
    // mutate live inventory, and the caller retains the original recovery bytes.
    public static RebirthCamperStationInstallations Restore(Guid vehicleIdentity, byte[] payload)
    {
        RequireGameThread();
        if (payload == null) throw new ArgumentNullException("payload");
        using (var stream = new MemoryStream(payload, false))
        using (var header = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
        {
            if (header.ReadUInt32() != 0x314D4352u) throw new InvalidDataException("Unsupported station manifest.");
            if (vehicleIdentity == Guid.Empty || new Guid(header.ReadBytes(16)) != vehicleIdentity)
                throw new InvalidDataException("Vehicle identity mismatch.");
            long revision = header.ReadInt64();
            int savedSlots = header.ReadInt32();
            // Original manifests have ten positions. Preserve their indices and
            // station identities when the current capacity is expanded.
            if (revision < 0 || (savedSlots != 10 && savedSlots != RebirthCamperCraftingStation.ModuleSlots)
                || savedSlots > RebirthCamperCraftingStation.ModuleSlots)
                throw new InvalidDataException("Invalid station manifest dimensions or revision.");
            var restored = new RebirthCamperStationInstallations(vehicleIdentity);
            var identities = new HashSet<Guid>();
            for (int slot = 0; slot < savedSlots; slot++)
            {
                byte occupied = header.ReadByte();
                if (occupied > 1) throw new InvalidDataException("Invalid station occupancy flag.");
                if (occupied == 0) continue;
                Guid id = new Guid(header.ReadBytes(16));
                if (id == Guid.Empty || !identities.Add(id))
                    throw new InvalidDataException("Missing or duplicate station identity.");
                var value = new ItemValue();
                using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    reader.SetBaseStream(stream);
                    value.Read(reader);
                }
                var item = new ItemStack(value, 1);
                int length = header.ReadInt32();
                if (length <= 0 || length > stream.Length - stream.Position)
                    throw new InvalidDataException("Incomplete station snapshot.");
                byte[] snapshot = header.ReadBytes(length);
                var state = RebirthCamperStationSnapshot.Restore(id, item, snapshot);
                restored.slots[slot] = new Installation(item, state);
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected manifest trailing data.");
            restored.Revision = revision;
            return restored;
        }
    }

    private static void CheckSlot(int slot)
    {
        if (slot < 0 || slot >= RebirthCamperCraftingStation.ModuleSlots)
            throw new ArgumentOutOfRangeException("slot");
    }

    private static void RequireGameThread()
    {
        if (!ThreadManager.IsMainThread())
            throw new InvalidOperationException("Change station installations on the game thread.");
    }
}
