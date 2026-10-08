#nullable disable

/// <summary>
/// Marks a recovered container item as the secure/player-owned form without
/// requiring a separate XML block type solely to obtain the lock overlay.
/// ItemValue metadata is serialized and also prevents the secure item from
/// stacking with an otherwise identical ordinary POI-container item.
/// </summary>
public static class RebirthSecureContainerItem
{
    public const string MetadataKey = "RebirthSecureContainer";
    public const string ItemTypeIcon = "lock";

    public static void Mark(ItemValue itemValue)
    {
        if (itemValue != null && itemValue.type > 0)
            itemValue.SetMetadata(MetadataKey, 1);
    }

    public static bool IsMarked(ItemValue itemValue)
    {
        int marked;
        return itemValue != null && itemValue.TryGetMetadata(MetadataKey, out marked) && marked != 0;
    }

    public static void ApplyItemStackBinding(
        XUiC_ItemStack controller,
        ref string value,
        string bindingName,
        ref bool handled)
    {
        if (controller == null || controller.itemStack == null ||
            controller.itemStack.IsEmpty() ||
            !IsMarked(controller.itemStack.itemValue))
            return;

        if (bindingName == "hasitemtypeicon")
        {
            // Match the base controller's behavior: a slot-lock sprite takes
            // precedence over an item-type icon in the same corner.
            value = string.IsNullOrEmpty(controller.lockSprite) ? "true" : "false";
            handled = true;
        }
        else if (bindingName == "itemtypeicon")
        {
            value = ItemTypeIcon;
            handled = true;
        }
    }
}
