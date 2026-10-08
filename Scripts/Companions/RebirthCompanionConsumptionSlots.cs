// Builds a complete replacement inventory without mutating the observed source.
// Used containers and untouched stacked siblings stay in the same companion storage.
internal static class RebirthCompanionConsumptionSlots
{
    internal static bool TryPlan(ItemStack[] observed, PackedBoolArray locks, int sourceSlot,
        ItemStack expected, ItemStack replacement, ItemStack siblings, out ItemStack[] planned)
    {
        planned = null;
        if (observed == null || sourceSlot < 0 || sourceSlot >= observed.Length || expected == null || expected.IsEmpty()) return false;
        ItemStack source = observed[sourceSlot];
        if (source == null || source.IsEmpty() || source.count != expected.count ||
            source.itemValue == null || !source.itemValue.Equals(expected.itemValue)) return false;
        var working = new ItemStack[observed.Length];
        for (int i = 0; i < working.Length; i++) working[i] = observed[i] != null ? observed[i].Clone() : ItemStack.Empty.Clone();
        working[sourceSlot] = replacement != null ? replacement.Clone() : ItemStack.Empty.Clone();
        ItemStack remaining = siblings != null ? siblings.Clone() : ItemStack.Empty.Clone();
        for (int i = 0; i < working.Length && !remaining.IsEmpty(); i++)
        {
            ItemStack target = working[i];
            if (target.IsEmpty()) continue;
            int amount;
            if (!target.CanStackPartlyWith(remaining, out amount) || amount <= 0) continue;
            amount = System.Math.Min(amount, remaining.count);
            target.count += amount;
            remaining.count -= amount;
        }
        for (int i = 0; i < working.Length && !remaining.IsEmpty(); i++)
        {
            if (!working[i].IsEmpty() || (i != sourceSlot && locks != null && i < locks.Length && locks[i])) continue;
            if (remaining.itemValue.ItemClass == null) return false;
            int amount = System.Math.Min(remaining.count, System.Math.Max(1, remaining.itemValue.ItemClass.Stacknumber.Value));
            working[i] = new ItemStack(remaining.itemValue.Clone(), amount);
            remaining.count -= amount;
        }
        if (!remaining.IsEmpty()) return false; // No partial source write or player-inventory spill.
        planned = working;
        return true;
    }
}
