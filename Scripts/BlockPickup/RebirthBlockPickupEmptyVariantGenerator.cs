using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Adds deterministic source-specific secure player-storage blocks.
///
/// Each loot/storage source receives its own generated player block. The block
/// can use the family's Empty downgrade model, but it retains the source's
/// effective LootList and therefore the source container's native size and
/// open/close sounds. Explicit RebirthContainerSize overrides are copied to the
/// generated block. POI block tile-entity schemas are never modified globally.
/// </summary>
public static class RebirthBlockPickupEmptyVariantGenerator
{
    public const string GeneratedSuffix = "RebirthPlayer";
    public const string PropertyGeneratedEmptyVisual = "RebirthGeneratedEmptyVisual";
    public const string PropertyGeneratedPlayerStorage = "RebirthGeneratedPlayerStorage";
    public const string PropertyGeneratedSourceBlock = "RebirthGeneratedSourceBlock";

    private const string FallbackPlayerStorageLootList = "playerWoodWritableStorage";
    private const string LegacyGeneratedDefaultContainerSize = "6,2";

    public static string GetGeneratedBlockName(string sourceBlockName)
    {
        return string.IsNullOrEmpty(sourceBlockName)
            ? string.Empty
            : sourceBlockName + GeneratedSuffix;
    }

    public static void Prepare(XmlFile xmlFile)
    {
        if (xmlFile == null || xmlFile.XmlDoc == null || xmlFile.XmlDoc.Root == null)
            return;

        XElement root = xmlFile.XmlDoc.Root;
        ConvertVanillaCanPickupProperties(root);

        List<XElement> sourceElements = root.Elements("block").ToList();
        if (sourceElements.Count == 0)
            return;

        Dictionary<string, XElement> blocks =
            new Dictionary<string, XElement>(StringComparer.Ordinal);
        for (int i = 0; i < sourceElements.Count; i++)
        {
            string name = GetAttribute(sourceElements[i], "name");
            if (!string.IsNullOrEmpty(name))
                blocks[name] = sourceElements[i];
        }

        List<PlayerStorageVariantDefinition> definitions =
            new List<PlayerStorageVariantDefinition>(512);

        foreach (KeyValuePair<string, XElement> pair in blocks)
        {
            string sourceName = pair.Key;
            if (IsPlayerVariantName(sourceName) ||
                LooksLikeTechnicalHelper(sourceName) ||
                !HasEffectiveStorage(
                    sourceName,
                    blocks,
                    new HashSet<string>(StringComparer.Ordinal)))
            {
                continue;
            }

            string generatedName = GetGeneratedBlockName(sourceName);
            if (blocks.ContainsKey(generatedName))
                continue;

            string visualName = FindEmptyVariant(sourceName, blocks);
            bool usesEmptyVisual = !string.IsNullOrEmpty(visualName);

            if (!usesEmptyVisual)
            {
                string legacyTarget;
                if (RebirthBlockPickupLegacyTargets.TryGetTarget(
                        sourceName, out legacyTarget) &&
                    IsUsableVisualBlockName(legacyTarget, blocks))
                {
                    visualName = legacyTarget;
                }
                else
                {
                    visualName = sourceName;
                }
            }

            if (!IsUsableVisualBlockName(visualName, blocks))
                continue;

            string lootList = GetEffectiveStorageFeatureValue(
                sourceName,
                TEFeatureStorage.PropLootList,
                blocks,
                new HashSet<string>(StringComparer.Ordinal));
            if (string.IsNullOrEmpty(lootList))
            {
                lootList = GetEffectiveStorageFeatureValue(
                    visualName,
                    TEFeatureStorage.PropLootList,
                    blocks,
                    new HashSet<string>(StringComparer.Ordinal));
            }
            if (string.IsNullOrEmpty(lootList))
                lootList = FallbackPlayerStorageLootList;

            string explicitSize = ResolveExplicitContainerSize(
                sourceName, visualName, blocks);

            // Let Extends supply the visual parent's authored icon and tint.
            // Only name-based icons need an explicit fallback: a generated block
            // has a different name, which is not itself an atlas sprite.
            string iconName = GetNameBasedIconFallback(visualName, blocks);

            definitions.Add(new PlayerStorageVariantDefinition(
                sourceName,
                visualName,
                generatedName,
                lootList,
                explicitSize,
                iconName,
                usesEmptyVisual));
        }

        if (definitions.Count == 0){RefreshGeneratedStorageNames(blocks);return;}

        definitions.Sort(delegate(
            PlayerStorageVariantDefinition a,
            PlayerStorageVariantDefinition b)
        {
            return string.CompareOrdinal(a.GeneratedBlockName, b.GeneratedBlockName);
        });

        int added = 0;
        int emptyVisualCount = 0;
        int explicitSizeCount = 0;
        int legacyCompatibilityCount = 0;
        for (int i = 0; i < definitions.Count; i++)
        {
            PlayerStorageVariantDefinition definition = definitions[i];
            if (!blocks.ContainsKey(definition.GeneratedBlockName))
            {
                XElement generatedElement = CreateGeneratedBlock(definition);
                string intactVisual = FindIntactStorageVisual(definition.SourceBlockName, blocks);
                if (!string.IsNullOrEmpty(intactVisual))
                    generatedElement.Elements("property").First(e => GetAttribute(e, "name") == "Extends")
                        .SetAttributeValue("value", intactVisual);
                root.Add(generatedElement);
                blocks.Add(definition.GeneratedBlockName, generatedElement);
                CopyLocalization(ResolveStorageNameKey(definition.SourceBlockName,definition.VisualBlockName,blocks),definition.GeneratedBlockName);

                added++;
                if (definition.UsesEmptyVisual)
                    emptyVisualCount++;
                if (!string.IsNullOrEmpty(definition.ExplicitContainerSize))
                    explicitSizeCount++;
            }

            // Previous 3.0 builds generated one player block from the shared
            // Empty visual name (for example cnt...EmptyRebirthPlayer). Keep
            // those names defined so placed blocks and inventory items from
            // existing saves do not become missing blocks. New pickups never
            // select these compatibility definitions; they use the source-
            // specific block above so native LootList sizes are preserved.
            if (definition.UsesEmptyVisual)
            {
                string legacyGeneratedName =
                    GetGeneratedBlockName(definition.VisualBlockName);
                if (!string.Equals(
                        legacyGeneratedName,
                        definition.GeneratedBlockName,
                        StringComparison.Ordinal) &&
                    !blocks.ContainsKey(legacyGeneratedName))
                {
                    PlayerStorageVariantDefinition compatibility =
                        new PlayerStorageVariantDefinition(
                            definition.VisualBlockName,
                            definition.VisualBlockName,
                            legacyGeneratedName,
                            FallbackPlayerStorageLootList,
                            LegacyGeneratedDefaultContainerSize,
                            GetNameBasedIconFallback(definition.VisualBlockName, blocks),
                            true);
                    XElement compatibilityElement =
                        CreateGeneratedBlock(compatibility);
                    root.Add(compatibilityElement);
                    blocks.Add(legacyGeneratedName, compatibilityElement);
                    CopyLocalization(ResolveStorageNameKey(definition.VisualBlockName,definition.VisualBlockName,blocks),legacyGeneratedName);
                    legacyCompatibilityCount++;
                }
            }
        }

        RefreshGeneratedStorageNames(blocks);
        if (added > 0 || legacyCompatibilityCount > 0)
        {
            { if (RebirthLogSettings.BlockPickupLoggingEnabled) Log.Out("[REBIRTH BlockPickup] Generated " + added
                + " source-specific secure player-storage block definitions ("
                + emptyVisualCount + " using Empty downgrade visuals, "
                + explicitSizeCount + " with explicit size overrides) and "
                + legacyCompatibilityCount + " legacy Empty-variant compatibility definitions."
                + " Native source LootList sizes are preserved without altering POI schemas."); }
        }
    }

    private static XElement CreateGeneratedBlock(
        PlayerStorageVariantDefinition definition)
    {
        XElement storageFeature = new XElement(
            "property",
            new XAttribute("class", "TEFeatureStorage"),
            Property(TEFeatureStorage.PropLootList, definition.LootList));

        XElement generated = new XElement(
            "block",
            new XAttribute("name", definition.GeneratedBlockName),
            new XElement(
                "property",
                new XAttribute("name", "Extends"),
                new XAttribute("value", definition.VisualBlockName),
                new XAttribute("param1", "Downgrade")),
            Property("Class", "CompositeTileEntity"),
            new XElement(
                "property",
                new XAttribute("class", "CompositeFeatures"),
                storageFeature,
                new XElement(
                    "property",
                    new XAttribute("class", "TEFeatureLockable")),
                new XElement(
                    "property",
                    new XAttribute(
                        "class",
                        TEFeatureRebirthContainerName.FeatureName))),
            Property("Stacknumber", "1"),
            Property("CreativeMode", "None"),
            // Generated secure player storage is terminal. Its Extends definition above excludes
            // inherited Downgrade data, preventing circular trader-shelf downgrade chains in 3.2.
            Property("ItemTypeIcon", "lock"),
            Property("FilterTags", "MC_playerBlocks,SC_decor"),
            Property("RebirthPickup", "Allow"),
            Property(
                PropertyGeneratedPlayerStorage,
                definition.VisualBlockName),
            Property(
                PropertyGeneratedSourceBlock,
                definition.SourceBlockName));

        if (!string.IsNullOrEmpty(definition.IconName))
            generated.Add(Property("CustomIcon", definition.IconName));

        if (!string.IsNullOrEmpty(definition.ExplicitContainerSize))
        {
            generated.Add(Property(
                RebirthContainerSizeService.PropertyContainerSize,
                definition.ExplicitContainerSize));
        }

        if (definition.UsesEmptyVisual)
        {
            generated.Add(Property(
                PropertyGeneratedEmptyVisual,
                definition.VisualBlockName));
        }

        return generated;
    }

    private static void ConvertVanillaCanPickupProperties(XElement root)
    {
        if (root == null)
            return;

        int converted = 0;
        int removed = 0;
        List<XElement> blocks = root.Elements("block").ToList();
        for (int i = 0; i < blocks.Count; i++)
        {
            XElement block = blocks[i];
            List<XElement> properties = block.Elements("property")
                .Where(e => string.Equals(
                    GetAttribute(e, "name"),
                    "CanPickup",
                    StringComparison.Ordinal))
                .ToList();
            if (properties.Count == 0)
                continue;

            XElement effective = properties[properties.Count - 1];
            string rawValue = GetAttribute(effective, "value");
            bool enabled;
            if (bool.TryParse(rawValue, out enabled))
            {
                SetDirectProperty(
                    block,
                    RebirthBlockPickupClassifier.PropertyVanillaCanPickup,
                    enabled ? "true" : "false");

                string pickupTarget = GetAttribute(effective, "param1");
                if (!string.IsNullOrEmpty(pickupTarget))
                {
                    SetDirectProperty(
                        block,
                        RebirthBlockPickupTargetResolver.PropertyVanillaPickupTarget,
                        pickupTarget.Trim());
                }

                converted++;
            }

            for (int j = 0; j < properties.Count; j++)
            {
                properties[j].Remove();
                removed++;
            }
        }

        if (removed > 0)
        {
            { if (RebirthLogSettings.BlockPickupLoggingEnabled) Log.Out("[REBIRTH BlockPickup] Converted " + converted
                + " vanilla CanPickup definitions to inherited REBIRTH hold-E metadata and removed "
                + removed + " vanilla prompt properties."); }
        }
    }

    private static void SetDirectProperty(
        XElement block,
        string propertyName,
        string propertyValue)
    {
        XElement existing = block.Elements("property")
            .LastOrDefault(e => string.Equals(
                GetAttribute(e, "name"),
                propertyName,
                StringComparison.Ordinal));
        if (existing == null)
        {
            block.Add(Property(propertyName, propertyValue));
            return;
        }

        existing.SetAttributeValue("value", propertyValue ?? string.Empty);
    }

    public static bool TryFindExistingPlayerStorageVariant(
        string sourceBlockName,
        Dictionary<string, XElement> blocks,
        out string variantName)
    {
        variantName = string.Empty;
        if (string.IsNullOrEmpty(sourceBlockName) || blocks == null)
            return false;

        string[] candidates =
        {
            GetGeneratedBlockName(sourceBlockName),
            sourceBlockName + "_Player",
            sourceBlockName + "Player"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (blocks.ContainsKey(candidate) &&
                HasEffectiveStorage(
                    candidate,
                    blocks,
                    new HashSet<string>(StringComparer.Ordinal)))
            {
                variantName = candidate;
                return true;
            }
        }

        return false;
    }

    private static string FindIntactStorageVisual(string source, Dictionary<string, XElement> blocks)
    {
        Func<string, bool> intact = name => !IsPlayerVariantName(name) &&
            !LooksLikeTechnicalHelper(name) && name.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) < 0 &&
            name.IndexOf("Open", StringComparison.OrdinalIgnoreCase) < 0 &&
            name.IndexOf("Broken", StringComparison.OrdinalIgnoreCase) < 0 &&
            HasEffectiveStorage(name, blocks, new HashSet<string>(StringComparer.Ordinal));
        if (intact(source)) return source;
        foreach (string name in blocks.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            if (!intact(name)) continue;
            string current = name;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (int depth = 0; depth < 16 && !string.IsNullOrEmpty(current) && visited.Add(current); depth++)
            {
                if (current == source) return name;
                current = FirstConfiguredName(GetEffectiveValue(current, "DowngradeBlock", blocks,
                    new HashSet<string>(StringComparer.Ordinal)));
            }
        }
        return string.Empty;
    }
    private static string FindEmptyVariant(
        string sourceName,
        Dictionary<string, XElement> blocks)
    {
        string current = sourceName;
        HashSet<string> visited =
            new HashSet<string>(StringComparer.Ordinal);

        for (int depth = 0; depth < 16; depth++)
        {
            if (IsEmptyVariantName(current))
                return current;
            if (!visited.Add(current) || !blocks.ContainsKey(current))
                return string.Empty;

            string downgrade = FirstConfiguredName(GetEffectiveValue(
                current,
                "DowngradeBlock",
                blocks,
                new HashSet<string>(StringComparer.Ordinal)));
            if (string.IsNullOrEmpty(downgrade) ||
                string.Equals(
                    downgrade,
                    "air",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(downgrade, current, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            current = downgrade;
        }

        return string.Empty;
    }

    private static bool HasEffectiveStorage(
        string blockName,
        Dictionary<string, XElement> blocks,
        HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(blockName) || !visited.Add(blockName))
            return false;

        XElement block;
        if (!blocks.TryGetValue(blockName, out block) || block == null)
            return false;

        IEnumerable<XElement> featureGroups = block.Elements("property")
            .Where(e => string.Equals(
                GetAttribute(e, "class"),
                "CompositeFeatures",
                StringComparison.Ordinal));
        foreach (XElement group in featureGroups)
        {
            if (group.Elements("property").Any(e => string.Equals(
                GetAttribute(e, "class"),
                "TEFeatureStorage",
                StringComparison.Ordinal)))
            {
                return true;
            }
        }

        string parent = GetDirectValue(block, "Extends");
        return !string.IsNullOrEmpty(parent) &&
            HasEffectiveStorage(parent, blocks, visited);
    }

    private static string GetEffectiveStorageFeatureValue(
        string blockName,
        string propertyName,
        Dictionary<string, XElement> blocks,
        HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(blockName) || !visited.Add(blockName))
            return string.Empty;

        XElement block;
        if (!blocks.TryGetValue(blockName, out block) || block == null)
            return string.Empty;

        string direct = GetDirectStorageFeatureValue(block, propertyName);
        if (!string.IsNullOrEmpty(direct))
            return direct;

        string parent = GetDirectValue(block, "Extends");
        return string.IsNullOrEmpty(parent)
            ? string.Empty
            : GetEffectiveStorageFeatureValue(
                parent,
                propertyName,
                blocks,
                visited);
    }

    private static string GetDirectStorageFeatureValue(
        XElement block,
        string propertyName)
    {
        if (block == null)
            return string.Empty;

        string value = string.Empty;
        foreach (XElement featureGroup in block.Elements("property"))
        {
            if (!string.Equals(
                    GetAttribute(featureGroup, "class"),
                    "CompositeFeatures",
                    StringComparison.Ordinal))
            {
                continue;
            }

            foreach (XElement feature in featureGroup.Elements("property"))
            {
                if (!string.Equals(
                        GetAttribute(feature, "class"),
                        "TEFeatureStorage",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (XElement property in feature.Elements("property"))
                {
                    if (string.Equals(
                        GetAttribute(property, "name"),
                        propertyName,
                        StringComparison.Ordinal))
                    {
                        value = GetAttribute(property, "value");
                    }
                }
            }
        }

        return value;
    }

    private static string ResolveExplicitContainerSize(
        string sourceName,
        string visualName,
        Dictionary<string, XElement> blocks)
    {
        string[] propertyNames =
        {
            RebirthContainerSizeService.PropertyContainerSize,
            RebirthContainerSizeService.PropertyLegacyContainerSize,
            RebirthContainerSizeService.PropertyLootContainerSize
        };

        string value = ResolveFirstSizeProperty(
            sourceName,
            propertyNames,
            blocks);
        if (IsContainerSize(value))
            return value.Trim();

        value = ResolveFirstSizeProperty(
            visualName,
            propertyNames,
            blocks);
        return IsContainerSize(value) ? value.Trim() : string.Empty;
    }

    private static string ResolveFirstSizeProperty(
        string blockName,
        string[] propertyNames,
        Dictionary<string, XElement> blocks)
    {
        for (int i = 0; i < propertyNames.Length; i++)
        {
            string propertyName = propertyNames[i];
            string value = GetEffectiveValue(
                blockName,
                propertyName,
                blocks,
                new HashSet<string>(StringComparer.Ordinal));
            if (IsContainerSize(value))
                return value;

            value = GetEffectiveStorageFeatureValue(
                blockName,
                propertyName,
                blocks,
                new HashSet<string>(StringComparer.Ordinal));
            if (IsContainerSize(value))
                return value;
        }

        return string.Empty;
    }

    private static string GetEffectiveValue(
        string blockName,
        string propertyName,
        Dictionary<string, XElement> blocks,
        HashSet<string> visited)
    {
        if (string.IsNullOrEmpty(blockName) || !visited.Add(blockName))
            return string.Empty;

        XElement block;
        if (!blocks.TryGetValue(blockName, out block) || block == null)
            return string.Empty;

        string direct = GetDirectValue(block, propertyName);
        if (!string.IsNullOrEmpty(direct))
            return direct;

        string parent = GetDirectValue(block, "Extends");
        return string.IsNullOrEmpty(parent)
            ? string.Empty
            : GetEffectiveValue(parent, propertyName, blocks, visited);
    }

    private static string GetNameBasedIconFallback(
        string visualName,
        Dictionary<string, XElement> blocks)
    {
        string current = visualName;
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        XElement block;
        while (!string.IsNullOrEmpty(current) && visited.Add(current) &&
            blocks.TryGetValue(current, out block))
        {
            XElement icon = block.Elements("property").LastOrDefault(e =>
                GetAttribute(e, "name") == "CustomIcon");
            if (icon != null)
                return string.IsNullOrEmpty(GetAttribute(icon, "value"))
                    ? visualName : string.Empty;

            XElement parent = block.Elements("property").LastOrDefault(e =>
                GetAttribute(e, "name") == "Extends");
            if (parent == null || GetAttribute(parent, "param1").Split(',')
                .Any(p => p.Trim() == "CustomIcon"))
                break;
            current = GetAttribute(parent, "value");
        }
        return visualName;
    }

    private static string GetDirectValue(
        XElement block,
        string propertyName)
    {
        if (block == null)
            return string.Empty;

        string value = string.Empty;
        foreach (XElement property in block.Elements("property"))
        {
            if (string.Equals(
                GetAttribute(property, "name"),
                propertyName,
                StringComparison.Ordinal))
            {
                value = GetAttribute(property, "value");
            }
        }

        return value;
    }

    private static bool IsUsableVisualBlockName(
        string blockName,
        Dictionary<string, XElement> blocks)
    {
        return !string.IsNullOrEmpty(blockName) &&
            !IsPlayerVariantName(blockName) &&
            !LooksLikeTechnicalHelper(blockName) &&
            blocks.ContainsKey(blockName);
    }

    private static XElement Property(string name, string value)
    {
        return new XElement(
            "property",
            new XAttribute("name", name),
            new XAttribute("value", value ?? string.Empty));
    }

    private static string ResolveStorageNameKey(string sourceName, string visualName, Dictionary<string,XElement> blocks)
    {
        if(!IsEmptyVariantName(sourceName)&&!LooksLikeTechnicalHelper(sourceName)&&Localization.Exists(sourceName))return sourceName;
        string empty=IsEmptyVariantName(sourceName)?sourceName:visualName;
        var candidates=blocks.Keys.Where(name=>!IsPlayerVariantName(name)&&!IsEmptyVariantName(name)&&!LooksLikeTechnicalHelper(name)&&
            Localization.Exists(name)&&FindEmptyVariant(name,blocks)==empty).OrderBy(name=>name,StringComparer.Ordinal).ToArray();
        if(candidates.Length>0)
        {
            if(Localization.Dictionary.TryGetValue(candidates[0],out var first)&&first!=null&&
                candidates.All(name=>Localization.Dictionary.TryGetValue(name,out var row)&&row!=null&&first.SequenceEqual(row)))return candidates[0];
        }
        return Localization.Exists("xuiStorage")?"xuiStorage":string.Empty;
    }

    private static void RefreshGeneratedStorageNames(Dictionary<string,XElement> blocks)
    {
        foreach(var pair in blocks)
        {
            string source=GetDirectValue(pair.Value,PropertyGeneratedSourceBlock);
            string visual=GetDirectValue(pair.Value,PropertyGeneratedPlayerStorage);
            if(string.IsNullOrEmpty(source)||string.IsNullOrEmpty(visual)||IsPlayerVariantName(source)||
                pair.Key!=GetGeneratedBlockName(source)||!blocks.ContainsKey(source)||!blocks.ContainsKey(visual)||
                GetDirectValue(pair.Value,"Extends")!=visual)continue;
            CopyLocalization(ResolveStorageNameKey(source,visual,blocks),pair.Key,true);
        }
    }

    private static void CopyLocalization(string sourceKey,string generatedKey,bool refreshGenerated=false)
    {
        try
        {
            if(string.IsNullOrEmpty(sourceKey)||!Localization.Exists(sourceKey)||!refreshGenerated&&Localization.Exists(generatedKey))return;
            if(Localization.Dictionary.TryGetValue(sourceKey,out var source)&&source!=null)
                Localization.Dictionary[generatedKey]=(string[])source.Clone();
        }
        catch(Exception ex){Log.Warning("[REBIRTH BlockPickup] Could not copy localization from "+sourceKey+" to "+generatedKey+": "+ex.Message);}
    }
    private static bool IsContainerSize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        string[] parts = value.Split(',');
        int width;
        int height;
        return parts.Length == 2 &&
            int.TryParse(parts[0].Trim(), out width) && width >= 1 && width <= 20 &&
            int.TryParse(parts[1].Trim(), out height) && height >= 1 && height <= 20;
    }

    private static bool IsEmptyVariantName(string name)
    {
        return !string.IsNullOrEmpty(name) &&
            name.IndexOf(
                "Empty",
                StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsPlayerVariantName(string name)
    {
        return !string.IsNullOrEmpty(name) &&
            (name.EndsWith(
                 "_Player",
                 StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(
                 "Player",
                 StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeTechnicalHelper(string name)
    {
        return !string.IsNullOrEmpty(name) &&
            (name.IndexOf(
                 "RandomLootHelper",
                 StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf(
                 "LootHelper",
                 StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf(
                 "VariantHelper",
                 StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf(
                 "placeholder",
                 StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string FirstConfiguredName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        int comma = raw.IndexOf(',');
        return (comma >= 0 ? raw.Substring(0, comma) : raw).Trim();
    }

    private static string GetAttribute(XElement element, string name)
    {
        if (element == null)
            return string.Empty;

        XAttribute attribute = element.Attribute(name);
        return attribute != null ? attribute.Value : string.Empty;
    }

    private sealed class PlayerStorageVariantDefinition
    {
        public readonly string SourceBlockName;
        public readonly string VisualBlockName;
        public readonly string GeneratedBlockName;
        public readonly string LootList;
        public readonly string ExplicitContainerSize;
        public readonly string IconName;
        public readonly bool UsesEmptyVisual;

        public PlayerStorageVariantDefinition(
            string sourceBlockName,
            string visualBlockName,
            string generatedBlockName,
            string lootList,
            string explicitContainerSize,
            string iconName,
            bool usesEmptyVisual)
        {
            SourceBlockName = sourceBlockName;
            VisualBlockName = visualBlockName;
            GeneratedBlockName = generatedBlockName;
            LootList = string.IsNullOrEmpty(lootList)
                ? FallbackPlayerStorageLootList
                : lootList;
            ExplicitContainerSize = explicitContainerSize ?? string.Empty;
            IconName = iconName ?? string.Empty;
            UsesEmptyVisual = usesEmptyVisual;
        }
    }
}
