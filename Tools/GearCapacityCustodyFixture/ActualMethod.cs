using System; public static class RebirthSurvivorGearService { public const int BasePhysicalBagSlots=52,MaxPhysicalBagSlots=169;    public static bool ReconcilePhysicalBagCapacity(EntityPlayer player, int desiredSlots, bool notify)
    {
        if (player == null || player.bag == null) return false;
        desiredSlots = Mathf.Clamp(desiredSlots, BasePhysicalBagSlots, MaxPhysicalBagSlots);
        ItemStack[] slots = player.bag.ItemGrid.items;
        if (slots == null) slots = ItemStack.CreateArray(desiredSlots);
        if (slots.Length == desiredSlots) return true;
        // The original transaction owns backing geometry while custody is held.
        // Native SetSlots may be refused by its guard; do not resize lock metadata
        // or report a successful projection resize after that refusal.
        var localOwner = player as EntityPlayerLocal;
        if (localOwner != null && (RebirthGearOwnerReservation.IsHeld(localOwner)
            || RebirthBackpackLibraryReservation.IsHeld(localOwner))) return false;
        if (slots.Length > desiredSlots)
        {
            for (int i = desiredSlots; i < slots.Length; i++)
                if (slots[i] != null && !slots[i].IsEmpty()) return false;
        }
        ItemStack[] resized = ItemStack.CreateArray(desiredSlots);
        int copy = Math.Min(slots.Length, resized.Length);
        for (int i = 0; i < copy; i++) resized[i] = slots[i] != null ? slots[i].Clone() : ItemStack.Empty.Clone();
        player.bag.SetSlots(resized);
        if (player.bag.ItemGrid.items == null || player.bag.ItemGrid.items.Length != desiredSlots) return false;
        PackedBoolArray locks = player.bag.LockedSlots;
        if (locks != null && locks.Length != desiredSlots) locks.Length = desiredSlots;
        if (notify) { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor] physical backpack capacity entity=" + player.entityId + " slots=" + desiredSlots); }
        return true;
    }

}