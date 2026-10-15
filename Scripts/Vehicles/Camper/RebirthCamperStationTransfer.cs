using System;

// Prepare the entire installation/player change before the server commit.
public sealed class RebirthCamperStationTransfer
{
    public RebirthCamperStationInstallations Stations { get; private set; }
    public ItemStack[] Backpack { get; private set; }
    public long ExpectedRevision { get; private set; }
    public int ModuleSlot { get; private set; }
    public bool Installing { get; private set; }

    // This single-position inventory is authenticated cursor escrow, not an
    // actual backpack. Commit its returned value only to the matching cursor.
    public static RebirthCamperStationTransfer PrepareCursor(RebirthCamperStationInstallations current,
        long expectedRevision, ItemStack reservedCursor, int moduleSlot)
    {
        if (reservedCursor == null) throw new ArgumentNullException(nameof(reservedCursor));
        return Prepare(current, expectedRevision, new[] { reservedCursor }, 0, moduleSlot,
            !reservedCursor.IsEmpty(), false);
    }

    public static RebirthCamperStationTransfer Prepare(RebirthCamperStationInstallations current,
        long expectedRevision, ItemStack[] backpack, int playerSlot, int moduleSlot, bool install, bool playerSlotLocked)
    {
        if (!ThreadManager.IsMainThread()) throw new InvalidOperationException("Game thread required.");
        if (current == null || current.Revision != expectedRevision)
            throw new InvalidOperationException("Station inventory changed.");
        // This transaction prepares a replacement of the complete station model.
        // Any live window still refers to its original state object, including
        // windows for slots other than the one being changed.
        for (int slot = 0; slot < RebirthCamperCraftingStation.ModuleSlots; slot++)
            if (RebirthCamperStationWindowSession.IsInUse(current.GetSlot(slot)?.State))
                throw new InvalidOperationException("Close station windows before changing installations.");
        if (backpack == null || playerSlot < 0 || playerSlot >= backpack.Length || playerSlotLocked)
            throw new ArgumentException("An unlocked backpack position is required.");
        var candidate = new ItemStack[backpack.Length];
        for (int index = 0; index < backpack.Length; index++)
        {
            if (backpack[index] == null || backpack[index].count < 0)
                throw new ArgumentException("Malformed backpack stack.");
            candidate[index] = new ItemStack(backpack[index].itemValue.Clone(), backpack[index].count);
        }
        var stations = RebirthCamperStationInstallations.Restore(current.VehicleIdentity, current.Capture());
        if (install)
        {
            ItemStack source = candidate[playerSlot];
            if (source.IsEmpty()) throw new ArgumentException("Station item is missing.");
            string portable;
            if (source.itemValue.TryGetMetadata(RebirthCamperPortableStation.MetadataKey, out portable) && source.count != 1)
                throw new InvalidOperationException("State-bearing station items must remain individual items.");
            stations.Install(moduleSlot, new ItemStack(source.itemValue.Clone(), 1));
            source.count--;
            if (source.count == 0) candidate[playerSlot] = ItemStack.Empty.Clone();
        }
        else
        {
            if (!candidate[playerSlot].IsEmpty()) throw new InvalidOperationException("Choose an empty backpack position.");
            candidate[playerSlot] = stations.Remove(moduleSlot, expectedRevision);
        }
        return new RebirthCamperStationTransfer { Stations = stations, Backpack = candidate,
            ExpectedRevision = expectedRevision, ModuleSlot = moduleSlot, Installing = install };
    }
}
