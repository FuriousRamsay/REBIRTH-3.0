using System;
using System.IO;

// Vehicle-owned module inventory, not a player inventory transfer endpoint.
// The authoritative coordinator must commit player transfers and bag resizing.
public sealed class RebirthCamperStorageInstallations
{
    private readonly ItemStack[] modules = new ItemStack[RebirthCamperStorageCapacity.ModuleSlots];
    public Guid VehicleIdentity { get; private set; }
    public long Revision { get; private set; }
    private bool baseFirst = true;
    public int Capacity { get { return RebirthCamperStorageCapacity.Total(GetCapacities()); } }

    public RebirthCamperStorageInstallations(Guid identity)
    {
        if (identity == Guid.Empty) throw new ArgumentException("Vehicle identity is required.");
        VehicleIdentity = identity;
    }

    public ItemStack GetSlot(int slot)
    {
        CheckSlot(slot);
        return modules[slot] == null ? null : new ItemStack(modules[slot].itemValue.Clone(), 1);
    }
    internal int CargoOffset(int slot)
    {
        CheckSlot(slot);int offset=baseFirst?RebirthCamperStorageCapacity.BaseSlots:0;var capacities=GetCapacities();
        for(int index=0;index<slot;index++)offset+=capacities[index];
        return offset;
    }
    // Reorder only a transaction candidate; the existing receipt still owns the
    // complete pre-migration manifest and cargo until the normal atomic commit.
    internal ItemStack[] WithBaseFirst(ItemStack[] cargo)
    {
        if(baseFirst)return cargo;
        if(cargo==null||cargo.Length!=Capacity)throw new InvalidDataException("Legacy cargo dimensions differ.");
        int added=Capacity-RebirthCamperStorageCapacity.BaseSlots;
        var reordered=ItemStack.CreateArray(cargo.Length);
        Array.Copy(cargo,added,reordered,0,RebirthCamperStorageCapacity.BaseSlots);
        Array.Copy(cargo,0,reordered,RebirthCamperStorageCapacity.BaseSlots,added);
        baseFirst=true;
        return reordered;
    }

    public void Install(int slot, ItemStack source)
    {
        RequireGameThread();
        CheckSlot(slot);
        if (modules[slot] != null) throw new InvalidOperationException("Container position is occupied.");
        int capacity;
        if (!RebirthCamperStorageContainer.TryGetCapacity(source, out capacity))
            throw new ArgumentException("Install one actual configured storage container item.");
        int[] capacities = GetCapacities();
        capacities[slot] = capacity;
        RebirthCamperStorageCapacity.Total(capacities); // Preflight native bag limit before mutation.
        var copy = new ItemStack(source.itemValue.Clone(), 1);
        long next = checked(Revision + 1);
        modules[slot] = copy;
        Revision = next;
    }

    public void Move(int from, int to)
    {
        RequireGameThread();
        CheckSlot(from); CheckSlot(to);
        if (modules[from] == null) throw new InvalidOperationException("Container position is empty.");
        if (from == to) return;
        if (modules[to] != null) throw new InvalidOperationException("Destination position is occupied.");
        long next = checked(Revision + 1);
        modules[to] = modules[from];
        modules[from] = null;
        Revision = next;
    }

    // Plan only. A future transaction must recheck Revision, transfer the module,
    // and resize/move real cargo atomically before removing anything from here.
    public bool TryPlanRemoval(int slot, int[] occupiedCargo, out int capacity, out int[] destinations)
    {
        RequireGameThread();
        CheckSlot(slot);
        if (modules[slot] == null) throw new InvalidOperationException("Container position is empty.");
        int[] capacities = GetCapacities();
        capacities[slot] = 0;
        capacity = RebirthCamperStorageCapacity.Total(capacities);
        return RebirthCamperStorageCapacity.TryPlanCargoPositions(capacity, occupiedCargo, out destinations);
    }

    // Server coordinator prepares the complete replacement cargo before committing.
    // This method never debits a player or publishes a bag; those are one transaction
    // under the coordinator's inventory/session lock, on the same game thread.
    public ItemStack RemoveAndCompact(int slot, long expectedRevision, ItemStack[] cargo, out ItemStack[] compacted)
    {
        RequireGameThread();
        CheckSlot(slot);
        compacted = null;
        if (Revision != expectedRevision) throw new InvalidOperationException("Container inventory changed; refresh before removal.");
        if (cargo == null || cargo.Length != Capacity) throw new ArgumentException("Cargo must match current installed capacity.");
        for (int index = 0; index < cargo.Length; index++)
        {
            if (cargo[index] == null || cargo[index].count < 0) throw new ArgumentException("Malformed cargo stack.");
        }
        var source=GetSlot(slot);Vector2i size;
        if(source==null||!RebirthCamperStorageContainer.TryGetSize(source,out size))
            throw new InvalidOperationException("Installed container missing.");
        int offset=CargoOffset(slot),removedCapacity=size.x*size.y;
        var contents=ItemStack.CreateArray(removedCapacity);
        for(int index=0;index<contents.Length;index++)contents[index]=cargo[offset+index].Clone();
        ItemStack removed=RebirthContainerPackUp.PrepareDetached(source,contents,size);
        var replacement=ItemStack.CreateArray(cargo.Length-removedCapacity);
        for(int index=0;index<replacement.Length;index++)
            replacement[index]=cargo[index<offset?index:index+removedCapacity].Clone();
        long next = checked(Revision + 1);
        // All validation/allocation finishes before either output or module mutation.
        modules[slot] = null;
        Revision = next;
        compacted = replacement;
        return removed;
    }

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
        int[] capacities = GetCapacities();
        RebirthCamperStorageCapacity.Total(capacities);
        using (var header = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            header.Write(baseFirst?0x32564352u:0x31564352u); // RCV2 records base-first cargo
            header.Write(VehicleIdentity.ToByteArray());
            header.Write(Revision);
            header.Write(modules.Length);
            for (int slot = 0; slot < modules.Length; slot++)
            {
                ItemStack item = modules[slot];
                header.Write(item != null);
                if (item == null) continue;
                header.Write(capacities[slot]);
                header.Flush();
                using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
                {
                    writer.SetBaseStream(stream);
                    item.itemValue.Write(writer);
                    writer.Flush();
                }
            }
            header.Flush();

        }
    }

    public static RebirthCamperStorageInstallations Restore(Guid identity, byte[] payload)
    {
        RequireGameThread();
        if (payload == null) throw new ArgumentNullException("payload");
        using (var stream = new MemoryStream(payload, false))
        using (var header = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
        {
            uint version=header.ReadUInt32();
            if (version!=0x31564352u&&version!=0x32564352u) throw new InvalidDataException("Unsupported container manifest.");
            if (identity == Guid.Empty || new Guid(header.ReadBytes(16)) != identity)
                throw new InvalidDataException("Vehicle identity mismatch.");
            long revision = header.ReadInt64();
            int savedSlots = header.ReadInt32();
            // Keep the original ten installations in their original positions;
            // newly added positions default to empty without changing cargo.
            if (revision < 0 || (savedSlots != 10 && savedSlots != RebirthCamperStorageCapacity.ModuleSlots)
                || savedSlots > RebirthCamperStorageCapacity.ModuleSlots)
                throw new InvalidDataException("Invalid container manifest revision/dimensions.");
            var restored = new RebirthCamperStorageInstallations(identity);
            restored.baseFirst=version==0x32564352u;
            for (int slot = 0; slot < savedSlots; slot++)
            {
                byte occupied = header.ReadByte();
                if (occupied > 1) throw new InvalidDataException("Invalid container occupancy flag.");
                if (occupied == 0) continue;
                int savedCapacity = header.ReadInt32();
                if (savedCapacity <= 0 || savedCapacity > RebirthCamperStorageCapacity.MaximumNativeSlots)
                    throw new InvalidDataException("Invalid saved container capacity.");
                var item = new ItemValue();
                using (var reader = MemoryPools.poolBinaryReader.AllocSync(true))
                {
                    reader.SetBaseStream(stream);
                    item.Read(reader);
                }
                var stack = new ItemStack(item, 1);
                int capacity;
                if (!RebirthCamperStorageContainer.TryGetCapacity(stack, out capacity))
                    throw new InvalidDataException("Installed container definition is missing/invalid; retain recovery payload.");
                if (capacity != savedCapacity)
                    throw new InvalidDataException("Installed container capacity changed; retain recovery payload and reconcile real cargo before adoption.");
                restored.modules[slot] = stack;
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected container manifest trailing data.");
            RebirthCamperStorageCapacity.Total(restored.GetCapacities());
            restored.Revision = revision;
            return restored;
        }
    }

    private int[] GetCapacities()
    {
        var result = new int[modules.Length];
        for (int slot = 0; slot < modules.Length; slot++)
            if (modules[slot] != null && !RebirthCamperStorageContainer.TryGetCapacity(modules[slot], out result[slot]))
                throw new InvalidOperationException("Installed container definition changed; preserve module inventory for recovery.");
        return result;
    }
    private static void CheckSlot(int slot)
    {
        if (slot < 0 || slot >= RebirthCamperStorageCapacity.ModuleSlots) throw new ArgumentOutOfRangeException("slot");
    }
    private static void RequireGameThread()
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Access container installations on the game thread.");
    }
}
