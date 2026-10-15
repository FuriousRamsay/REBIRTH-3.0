public static class RebirthCamperStorageContainer
{
    // A module is one actual placeable container item, never a currency token or arbitrary cargo stack.
    public static bool TryGetCapacity(ItemStack module, out int capacity)
    {
        Vector2i size;
        bool valid=TryGetSize(module,out size);
        capacity=valid?size.x*size.y:0;
        return valid;
    }
    internal static bool TryGetSize(ItemStack module,out Vector2i size)
    {
        size = default(Vector2i);
        if (module == null || module.IsEmpty() || module.count != 1 || module.itemValue == null) return false;
        ItemClassBlock item = module.itemValue.ItemClass as ItemClassBlock;
        if (item == null) return false;
        Block block = item.GetBlockValueFromItemValue(module.itemValue).Block;
        if (block == null) return false;
        // Modern safes/fridges are CompositeTileEntity blocks. Native storage
        // reads its own feature properties, not the block-root LootList.
        DynamicProperties composite, storage;
        // Open/decorative container variants can explicitly opt in using the
        // capacity of their native functional counterpart, without changing
        // their world-block class or converting arbitrary decoration to cargo.
        string lootList = block.Properties.GetString("RebirthCamperStorageLootList");
        if (string.IsNullOrEmpty(lootList))
        {
            if (block is BlockCompositeTileEntity compositeBlock
                && TileEntityCompositeData.FeaturesByBlock.TryGetValue(compositeBlock,out var data))
            {
                // Use the same compiled feature definition as the native container.
                // Feature names may be aliases or storage subclasses; the XML class
                // key alone does not identify the actual storage implementation.
                lootList=null;
                foreach(var feature in data.Features)
                    if(feature?.Type!=null && typeof(TEFeatureStorage).IsAssignableFrom(feature.Type))
                    { lootList=feature.Props.GetString(TEFeatureStorage.PropLootList); break; }
            }
            else if (block.Properties.Classes.TryGetValue("CompositeFeatures", out composite)
                && composite != null && composite.Classes.TryGetValue("TEFeatureStorage", out storage)
                && storage != null)
                lootList = storage.GetString(TEFeatureStorage.PropLootList);
            else
                lootList = block.Properties.GetString(TEFeatureStorage.PropLootList);
            }
        if (string.IsNullOrEmpty(lootList)) return false;
        LootContainer container = LootContainer.GetLootContainer(lootList, false);
        if (container == null || container.size.x <= 0 || container.size.y <= 0) return false;
        long total = (long)container.size.x * container.size.y;
        if (total > RebirthCamperStorageCapacity.MaximumNativeSlots) return false;
        size = container.size;
        return true;
    }
}
