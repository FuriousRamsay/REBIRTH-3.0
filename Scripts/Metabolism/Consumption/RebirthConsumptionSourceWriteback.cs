// Compare the captured source immediately before writing. This is an in-memory
// source commit; it does not claim cross-repository crash durability.
internal static class RebirthConsumptionSourceWriteback
{
    internal static bool TryCommit(EntityPlayer player, RebirthMetabolismSourceKind kind,
        int slot, ItemStack expected, ItemStack replacement, RebirthMetabolismState state)
    {
        if (player == null || expected == null || expected.IsEmpty() || expected.itemValue == null || RebirthBackpackLibraryReservation.BlocksResourceUse(player))
            return false;
        ItemStack current;
        switch (kind)
        {
            case RebirthMetabolismSourceKind.HydrationSlot:
                if (state == null) return false;
                current = state.HydrationSlotItem;
                break;
            case RebirthMetabolismSourceKind.Backpack:
                var bag = player.bag != null ? player.bag.ItemGrid.items : null;
                if (bag == null || slot < 0 || slot >= bag.Length) return false;
                current = bag[slot];
                break;
            case RebirthMetabolismSourceKind.HeldToolbelt:
                var belt = player.inventory != null ? player.inventory.ItemGrid.items : null;
                if (belt == null || slot < 0 || slot >= RebirthToolbeltCapacity.GetOwnedSlotCount(player, belt.Length)) return false;
                current = belt[slot];
                break;
            default:
                return false; // New source kinds must supply an explicit adapter.
        }
        if (current == null || current.IsEmpty() || current.count != expected.count ||
            current.itemValue == null || !current.itemValue.Equals(expected.itemValue)) return false;

        ItemStack value = replacement != null ? replacement.Clone() : ItemStack.Empty.Clone();
        ItemStack intended = value.Clone();
        switch (kind)
        {
            case RebirthMetabolismSourceKind.HydrationSlot: state.HydrationSlotItem = value; break;
            case RebirthMetabolismSourceKind.Backpack: player.bag.SetSlot(slot, value); break;
            case RebirthMetabolismSourceKind.HeldToolbelt: player.inventory.SetItem(slot, value); break;
        }
        // Native setters return void and may be refused by an inventory guard.
        // Only the exact observed postimage permits the caller to add ingestion.
        ItemStack observed = null;
        switch (kind)
        {
            case RebirthMetabolismSourceKind.HydrationSlot: observed = state.HydrationSlotItem; break;
            case RebirthMetabolismSourceKind.Backpack: observed = player.bag.ItemGrid.items[slot]; break;
            case RebirthMetabolismSourceKind.HeldToolbelt: observed = player.inventory.ItemGrid.items[slot]; break;
        }
        return observed != null && observed.count == intended.count &&
            (observed.IsEmpty() && intended.IsEmpty() || !observed.IsEmpty() && !intended.IsEmpty() &&
                observed.itemValue != null && observed.itemValue.Equals(intended.itemValue));
    }
}
