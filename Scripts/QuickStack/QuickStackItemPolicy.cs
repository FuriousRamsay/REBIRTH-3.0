// Direct slot writers must retain the restriction used by native container UI.
// Returning an already stored item to the player is intentionally unaffected.
internal static class QuickStackItemPolicy
{
    internal static bool CanDeposit(ItemValue value)
        => value != null && !value.IsEmpty() && value.ItemClass != null
            && value.ItemClass.CanPlaceInContainer();
}
