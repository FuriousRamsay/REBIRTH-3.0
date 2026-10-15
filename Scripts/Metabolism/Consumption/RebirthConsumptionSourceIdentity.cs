internal static class RebirthConsumptionSourceIdentity
{
    // Requests identify the used stack by type and native seed. Stack size is
    // not permission to consume a different stack of the same food.
    internal static bool Matches(ItemStack stack, int itemType, ushort seed)
        => stack != null && !stack.IsEmpty() && stack.itemValue != null
            && stack.itemValue.type == itemType && stack.itemValue.Seed == seed;
}
