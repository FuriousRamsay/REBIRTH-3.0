using System;

// Detached prediction only. Never writes target, drag cursor, or persistence.
internal static class RebirthVehicleCursorDropPlan
{
    internal static bool TryPlan(XUiC_ItemStack target, ItemStack cursor, bool dropOne,
        out ItemStack afterTarget, out ItemStack afterCursor)
    {
        afterTarget = null;
        afterCursor = null;
        if (target == null || cursor == null || target.ItemStack == null || !target.AllowDropping ||
            cursor.IsEmpty() || cursor.count <= 0 || target.ItemStack.count < 0) return false;
        ItemStack incoming = cursor.Clone();
        ItemStack stored = target.ItemStack.Clone();
        ItemClass incomingClass = incoming.itemValue.ItemClassOrMissing;
        ItemClass storedClass = stored.IsEmpty() ? null : stored.itemValue.ItemClassOrMissing;
        if (incomingClass == null || (!stored.IsEmpty() && storedClass == null)) return false;
        int capacity = target.OverrideStackCount == -1 ? incomingClass.MaxCount :
            Math.Min(incomingClass.MaxCount, target.OverrideStackCount);
        if (capacity <= 0) return false;

        if (dropOne)
        {
            // Native HandleDropOne uses location-only admission and type-only merge.
            if (!incoming.CanMoveTo(target.StackLocation)) return false;
            if (stored.IsEmpty())
            {
                stored = incoming.Clone(); stored.count = 1; incoming.count--;
            }
            else if (incoming.itemValue.type == stored.itemValue.type && stored.count < capacity)
            {
                stored.count++; incoming.count--;
            }
        }
        else
        {
            bool splitEmpty = stored.IsEmpty() && capacity < incoming.count;
            bool swap = !splitEmpty && (stored.IsEmpty() ||
                !storedClass.CanStack() || !incomingClass.CanStack());
            bool merge = !swap && incoming.itemValue.type == stored.itemValue.type &&
                !incoming.itemValue.HasQuality && !stored.itemValue.HasQuality;
            if (merge)
            {
                long total = (long)incoming.count + stored.count;
                if (total > int.MaxValue || stored.count > capacity) return false;
                if (total > capacity)
                {
                    stored.count = capacity; incoming.count = (int)total - capacity;
                }
                else { stored.count = (int)total; incoming = ItemStack.Empty.Clone(); }
            }
            else if (splitEmpty)
            {
                stored = incoming.Clone(); stored.count = capacity; incoming.count -= capacity;
            }
            else
            {
                // Native SwapItem has location/slot admission and additional side effects.
                if (!target.CanSwap(incoming.Clone()) || !incoming.CanMoveTo(target.StackLocation, target.SlotNumber) ||
                    target.StackLocation == XUiC_ItemStack.StackLocationTypes.CollectorCatalysts ||
                    target.StackLocation == XUiC_ItemStack.StackLocationTypes.ToolBelt ||
                    (target.StackLocation == XUiC_ItemStack.StackLocationTypes.LootContainer &&
                     !incomingClass.CanPlaceInContainer())) return false;
                ItemStack previous = stored; stored = incoming; incoming = previous;
            }
        }
        afterTarget = stored;
        afterCursor = incoming.count == 0 ? ItemStack.Empty.Clone() : incoming;
        return true;
    }
}
