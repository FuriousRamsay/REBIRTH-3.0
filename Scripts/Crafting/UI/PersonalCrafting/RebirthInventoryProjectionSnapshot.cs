using System;

// Owns only a pending UI image. Never retain native inventory references or
// reuse this array after the controller has handed it to the presenter.
internal static class RebirthInventoryProjectionSnapshot
{
    internal static ItemStack[] Update(ItemStack[] pending, ItemStack[] source)
    {
        if (source == null) return Array.Empty<ItemStack>();
        if (pending == null || pending.Length != source.Length)
            pending = new ItemStack[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            ItemStack incoming = source[i] ?? ItemStack.Empty;
            ItemStack previous = pending[i];
            if (previous == null || previous.count != incoming.count ||
                !SameValue(previous.itemValue, incoming.itemValue))
                pending[i] = incoming.Clone();
        }
        return pending;
    }

    private static bool SameValue(ItemValue previous, ItemValue incoming)
    {
        if (previous == null || incoming == null) return false;
        // Native equality includes wear, quality, seed, ammo selection, stats,
        // metadata and nested mods. It omits the complete Flags and paint data.
        // Check those on nested mods too, since Clone retains them recursively.
        if (HasNullMetadata(previous) || HasNullMetadata(incoming) ||
            !previous.Equals(incoming) || previous.Flags != incoming.Flags ||
            !previous.TextureFullArray.Equals(incoming.TextureFullArray)) return false;
        return SameExtras(previous.modifications, incoming.modifications) &&
               SameExtras(previous.cosmeticMods, incoming.cosmeticMods);
    }

    private static bool SameExtras(ItemValue[] previous, ItemValue[] incoming)
    {
        // Native equality already verified lengths and null positions.
        for (int i = 0; incoming != null && i < incoming.Length; i++)
            if (incoming[i] != null && !SameValue(previous[i], incoming[i])) return false;
        return true;
    }

    private static bool HasNullMetadata(ItemValue value)
    {
        // Native Equals dereferences metadata values. Such input can still be
        // cloned; retain that existing behavior rather than throwing here.
        if (value.Metadata != null)
            foreach (var entry in value.Metadata) if (entry.Value == null) return true;
        foreach (var item in value.modifications ?? Array.Empty<ItemValue>())
            if (item != null && HasNullMetadata(item)) return true;
        foreach (var item in value.cosmeticMods ?? Array.Empty<ItemValue>())
            if (item != null && HasNullMetadata(item)) return true;
        return false;
    }
}
