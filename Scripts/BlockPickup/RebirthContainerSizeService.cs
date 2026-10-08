using System;

#nullable disable

/// <summary>
/// Applies an optional inherited block property to newly placed player-owned
/// storage. XML inheritance is already flattened into Block.Properties by 3.0.
/// </summary>
public static class RebirthContainerSizeService
{
    public const string PropertyContainerSize = "RebirthContainerSize";
    public const string PropertyLegacyContainerSize = "ContainerSize";
    public const string PropertyLootContainerSize = "LootContainerSize";

    public static void ApplyToNewPlayerStorage(
        TEFeatureStorage storage,
        BlockValue blockValue)
    {
        if (storage == null || storage.ItemGrid == null || !storage.ItemGrid.PlayerOwned ||
            !SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return;

        Block block = blockValue.Block;
        Vector2i configured;
        if (block == null || !TryGetConfiguredSize(block, out configured))
            return;

        if (storage.ItemGrid.ContainerSize.x == configured.x &&
            storage.ItemGrid.ContainerSize.y == configured.y)
            return;

        // Resize returns removed items in the installed API. This placement-only policy
        // must never discard an occupied trailing slot if called on a populated grid.
        int capacity = checked(configured.x * configured.y);
        for (int i = capacity; i < storage.ItemGrid.Length; i++)
            if (storage.ItemGrid[i] != null && !storage.ItemGrid[i].IsEmpty()) return;
        storage.ItemGrid.Resize(configured);
        storage.SetModified();
    }

    public static bool TryGetConfiguredSize(Block block, out Vector2i size)
    {
        size = new Vector2i();
        if (block == null || block.Properties == null)
            return false;

        // The REBIRTH-specific property is the explicit final override. Legacy
        // names remain supported only when no RebirthContainerSize is present.
        string raw = GetProperty(block, PropertyContainerSize);
        if (string.IsNullOrEmpty(raw))
            raw = GetProperty(block, PropertyLegacyContainerSize);
        if (string.IsNullOrEmpty(raw))
            raw = GetProperty(block, PropertyLootContainerSize);
        if (string.IsNullOrEmpty(raw))
            return false;

        string[] parts = raw.Split(',');
        int width;
        int height;
        if (parts.Length != 2 ||
            !int.TryParse(parts[0].Trim(), out width) ||
            !int.TryParse(parts[1].Trim(), out height) ||
            width < 1 || height < 1 || width > 20 || height > 20)
        {
            Log.Warning("[REBIRTH BlockPickup] Invalid container size '" + raw
                + "' on block " + block.GetBlockName()
                + ". Expected width,height with each value from 1 to 20.");
            return false;
        }

        size = new Vector2i(width, height);
        return true;
    }

    private static string GetProperty(Block block, string key)
    {
        string value;
        if (block.Properties.Values.TryGetValue(key, out value))
            return value;

        DynamicProperties compositeFeatures;
        DynamicProperties storageProperties;
        if (block.Properties.Classes.TryGetValue("CompositeFeatures", out compositeFeatures) &&
            compositeFeatures != null &&
            compositeFeatures.Classes.TryGetValue("TEFeatureStorage", out storageProperties) &&
            storageProperties != null &&
            storageProperties.Values.TryGetValue(key, out value))
            return value;

        return string.Empty;
    }
}
