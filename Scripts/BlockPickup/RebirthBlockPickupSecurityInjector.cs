using System;

#nullable disable

/// <summary>
/// Adds REBIRTH rename/security features only to player-storage definitions.
///
/// Earlier revisions injected two features into every TEFeatureStorage block,
/// including bird nests, trash piles, POI loot crates and vehicles. Existing
/// saves then logged one schema-migration warning for every loaded container.
/// Recovered containers now use distinct generated player-storage block types,
/// so ordinary world loot definitions must remain byte-for-byte unchanged.
/// </summary>
public static class RebirthBlockPickupSecurityInjector
{
    private const string CompositeFeaturesClass = "CompositeFeatures";
    private const string StorageFeatureClass = "TEFeatureStorage";
    private const string LockableFeatureClass = "TEFeatureLockable";
    private const string RenameFeatureClass = TEFeatureRebirthContainerName.FeatureName;

    public static void Prepare(BlockCompositeTileEntity block)
    {
        if (block == null || block.Properties == null)
            return;

        DynamicProperties compositeFeatures;
        if (!block.Properties.Classes.TryGetValue(CompositeFeaturesClass, out compositeFeatures) ||
            compositeFeatures == null ||
            !compositeFeatures.Classes.ContainsKey(StorageFeatureClass))
            return;

        if (IsExcludedStorageDefinition(block, compositeFeatures.Classes[StorageFeatureClass]))
            return;

        bool generatedPlayerStorage = HasTrueOrNonEmptyProperty(
            block, RebirthBlockPickupEmptyVariantGenerator.PropertyGeneratedPlayerStorage);
        bool explicitPlayerStorage = IsPlayerStorageDefinition(block);

        // Lockable is a capability, not ownership. Native/POI/quest storage can
        // already be lockable; that alone must never opt it into REBIRTH's rename
        // schema. Only generated or explicitly player-storage definitions qualify.
        if (!generatedPlayerStorage && !explicitPlayerStorage)
            return;

        if (!compositeFeatures.Classes.ContainsKey(LockableFeatureClass))
            compositeFeatures.Classes.Add(LockableFeatureClass, new DynamicProperties());

        if (!compositeFeatures.Classes.ContainsKey(RenameFeatureClass))
            compositeFeatures.Classes.Add(RenameFeatureClass, new DynamicProperties());
    }

    private static bool IsPlayerStorageDefinition(BlockCompositeTileEntity block)
    {
        string name = block.GetBlockName() ?? string.Empty;
        if (name.EndsWith("_Player", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith("Player", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(RebirthBlockPickupEmptyVariantGenerator.GeneratedSuffix,
                StringComparison.OrdinalIgnoreCase))
            return true;

        return RebirthBlockPickupClassifier.HasFilterTag(block, "MC_playerBlocks");
    }

    private static bool HasTrueOrNonEmptyProperty(Block block, string propertyName)
    {
        string value;
        if (block == null || block.Properties == null ||
            !block.Properties.Values.TryGetValue(propertyName, out value))
            return false;

        if (string.IsNullOrEmpty(value))
            return false;

        bool parsed;
        return !bool.TryParse(value, out parsed) || parsed;
    }

    private static bool IsExcludedStorageDefinition(
        BlockCompositeTileEntity block,
        DynamicProperties storageProperties)
    {
        string filterTags;
        if (block.Properties.Values.TryGetValue("FilterTags", out filterTags) &&
            (ContainsCsvToken(filterTags, "SC_questblocks") ||
             ContainsCsvToken(filterTags, "SC_playerHelpers")))
            return true;

        string isQuestLoot;
        if (storageProperties != null &&
            storageProperties.Values.TryGetValue("IsQuestLoot", out isQuestLoot) &&
            string.Equals(isQuestLoot, "true", StringComparison.OrdinalIgnoreCase))
            return true;

        string name = block.GetBlockName() ?? string.Empty;
        return name.IndexOf("fetchquest", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("questsatchel", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsCsvToken(string csv, string token)
    {
        if (string.IsNullOrEmpty(csv))
            return false;

        string[] parts = csv.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i].Trim(), token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
