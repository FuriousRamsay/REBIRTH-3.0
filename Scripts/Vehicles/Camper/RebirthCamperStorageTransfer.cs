using System;

// Complete candidate for one server-owned container transfer. The coordinator
// persists a transaction receipt before publishing either native inventory.
public sealed class RebirthCamperStorageTransfer
{
    public RebirthCamperStorageInstallations Storage { get; private set; }
    public ItemStack[] Backpack { get; private set; }
    public ItemStack[] Cargo { get; private set; }
    public long ExpectedRevision { get; private set; }

    // Cursor items have already left the backpack through native UI movement.
    // The coordinator must reserve/verify cursor custody and publish Backpack[0]
    // back to that cursor; never debit a matching backpack stack a second time.
    public static RebirthCamperStorageTransfer PrepareCursor(RebirthCamperStorageInstallations current,
        long expectedRevision, ItemStack reservedCursor, ItemStack[] cargo, int moduleSlot)
    {
        if (reservedCursor == null) throw new ArgumentNullException(nameof(reservedCursor));
        return Prepare(current, expectedRevision, new[] { reservedCursor }, 0, cargo,
            moduleSlot, !reservedCursor.IsEmpty(), false);
    }

    public static RebirthCamperStorageTransfer Prepare(RebirthCamperStorageInstallations current,
        long expectedRevision, ItemStack[] backpack, int playerSlot, ItemStack[] cargo,
        int moduleSlot, bool install, bool playerSlotLocked)
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Prepare transfers on the game thread.");
        if (current == null || current.Revision != expectedRevision)
            throw new InvalidOperationException("Container inventory changed.");
        if (backpack == null || playerSlot < 0 || playerSlot >= backpack.Length || playerSlotLocked)
            throw new ArgumentException("An unlocked backpack position is required.");
        if (cargo == null || cargo.Length != current.Capacity)
            throw new ArgumentException("Cargo dimensions do not match installed containers.");
        var nextBackpack = Copy(backpack);
        var nextStorage = RebirthCamperStorageInstallations.Restore(current.VehicleIdentity, current.Capture());
        ItemStack[] nextCargo;
        if (install)
        {
            ItemStack source = nextBackpack[playerSlot];
            if (source.IsEmpty() || source.count < 1) throw new ArgumentException("Backpack container is missing.");
            int offset=nextStorage.CargoOffset(moduleSlot);
            ItemStack[] packedContents;
            bool packed=RebirthContainerPackUp.ReadDetached(source,out packedContents);
            nextStorage.Install(moduleSlot, new ItemStack(source.itemValue.Clone(), 1));
            nextCargo = ItemStack.CreateArray(nextStorage.Capacity);
            ItemStack[] copied = Copy(cargo);
            int added=nextCargo.Length-copied.Length;
            Array.Copy(copied,0,nextCargo,0,offset);
            Array.Copy(copied,offset,nextCargo,offset+added,copied.Length-offset);
            if(packed)
            {
                if(packedContents.Length!=added)throw new InvalidOperationException("Packed capacity differs.");
                Array.Copy(Copy(packedContents),0,nextCargo,offset,added);
            }
            source.count--;
            if (source.count == 0) nextBackpack[playerSlot] = ItemStack.Empty.Clone();
        }
        else
        {
            // An empty explicit destination avoids guessed metadata equivalence or
            // accidental overstacking. The UI lets the player choose that position.
            if (!nextBackpack[playerSlot].IsEmpty())
                throw new InvalidOperationException("Choose an empty backpack position for the container.");
            nextBackpack[playerSlot] = nextStorage.RemoveAndCompact(moduleSlot, expectedRevision, cargo, out nextCargo);
        }
        return new RebirthCamperStorageTransfer {
            Storage = nextStorage, Backpack = nextBackpack, Cargo = nextCargo, ExpectedRevision = expectedRevision
        };
    }

    private static ItemStack[] Copy(ItemStack[] source)
    {
        var result = new ItemStack[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == null || source[index].count < 0)
                throw new ArgumentException("Malformed inventory stack.");
            result[index] = new ItemStack(source[index].itemValue.Clone(), source[index].count);
        }
        return result;
    }
}
