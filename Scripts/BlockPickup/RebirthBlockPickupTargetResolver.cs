using System;
using System.Collections.Generic;

#nullable disable

public sealed class RebirthBlockPickupTargetDecision
{
    public readonly ItemValue TargetItem;
    public readonly BlockValue Target;
    public readonly int Count;
    public readonly string Reason;

    public RebirthBlockPickupTargetDecision(ItemValue targetItem, BlockValue targetBlock, int count, string reason)
    {
        TargetItem = targetItem;
        Target = targetBlock;
        Count = Math.Max(1, count);
        Reason = reason ?? string.Empty;
    }

    public bool IsValid
    {
        get { return TargetItem != null && TargetItem.type > 0 && TargetItem.ItemClass != null; }
    }

    public bool IsBlockTarget
    {
        get { return !Target.isair && Target.Block != null; }
    }

    public bool IsStorageContainer
    {
        get { return IsBlockTarget && RebirthBlockPickupClassifier.IsStorageContainer(Target.Block); }
    }

    public string TargetName
    {
        get
        {
            if (IsBlockTarget)
                return Target.Block.GetBlockName();
            return IsValid ? TargetItem.ItemClass.Name : "invalid";
        }
    }

    public ItemValue CreateItemValue()
    {
        return IsValid ? TargetItem.Clone() : ItemValue.None;
    }
}

/// <summary>
/// Resolves both the item and quantity produced by REBIRTH pickup independently
/// from source eligibility. This replaces the 2.6 source -> *_PickedUp XML layer
/// while retaining its ReplacementBlockName/isReplacementItem/numBlocks escape hatches.
/// </summary>
public static class RebirthBlockPickupTargetResolver
{
    public const string PropertyPickupTarget = "RebirthPickupTarget";
    public const string PropertyLegacyReplacementName = "ReplacementBlockName";
    public const string PropertyLegacyReplacementIsItem = "isReplacementItem";
    public const string PropertyReplacementCount = "numBlocks";
    public const string PropertyVanillaPickupTarget = "RebirthVanillaPickupTarget";

    private static readonly object Sync = new object();
    private static RebirthBlockPickupTargetDecision[] cache;
    private static Block[] cacheSources;
    private static object cacheRegistryIdentity;
    private static Dictionary<int, List<Block>> reverseDowngradeStorageBlocks;
    private static Dictionary<int, List<Block>> reverseSelectorHelpers;

    public static void ClearCache()
    {
        cache = null;
        cacheSources = null;
        cacheRegistryIdentity = null;
        lock (Sync)
        {
            reverseDowngradeStorageBlocks = null;
            reverseSelectorHelpers = null;
        }
    }

    public static bool HasExplicitResultOverride(Block source)
    {
        if (source == null)
            return false;

        if (!string.IsNullOrEmpty(GetProperty(source, PropertyPickupTarget)) ||
            !string.IsNullOrEmpty(GetProperty(source, PropertyLegacyReplacementName)))
            return true;

        int count;
        return int.TryParse(GetProperty(source, PropertyReplacementCount), out count) && count > 1;
    }

    public static RebirthBlockPickupTargetDecision Resolve(Block source)
    {
        if (source == null)
            return Invalid("missing source block");

        // Definition startup is not a stable cache generation. Resolve directly
        // and leave the miss retryable until Block.LateInitAll publishes the full registry.
        if (!Block.BlocksLoaded || Block.list == null || Block.list.Length == 0)
            return ResolveUncached(source);

        RebirthBlockPickupTargetDecision[] local = cache;
        Block[] localSources = cacheSources;
        object registryIdentity = Block.list;
        if (local == null
            || localSources == null
            || !ReferenceEquals(cacheRegistryIdentity, registryIdentity)
            || local.Length != Block.list.Length
            || localSources.Length != Block.list.Length)
        {
            lock (Sync)
            {
                if (cache == null
                    || cacheSources == null
                    || !ReferenceEquals(cacheRegistryIdentity, registryIdentity)
                    || cache.Length != Block.list.Length
                    || cacheSources.Length != Block.list.Length)
                {
                    cache = new RebirthBlockPickupTargetDecision[Block.list.Length];
                    cacheSources = new Block[Block.list.Length];
                    cacheRegistryIdentity = registryIdentity;
                    reverseDowngradeStorageBlocks = null;
                    reverseSelectorHelpers = null;
                }

                local = cache;
                localSources = cacheSources;
            }
        }

        int id = source.blockID;
        if (id >= 0 && id < local.Length
            && local[id] != null
            && ReferenceEquals(localSources[id], source))
        {
            return local[id];
        }

        RebirthBlockPickupTargetDecision decision = ResolveUncached(source);
        if (id >= 0 && id < local.Length)
        {
            localSources[id] = source;
            local[id] = decision;
        }
        return decision;
    }

    private static RebirthBlockPickupTargetDecision ResolveUncached(Block source)
    {
        string sourceName = source.GetBlockName() ?? string.Empty;
        int count = ParseCount(source);

        // Modern explicit override. ItemClass.GetItem resolves either an item or a
        // block item, so no companion type property is required for this form.
        string configuredTarget = FirstConfiguredName(GetProperty(source, PropertyPickupTarget));
        if (!string.IsNullOrEmpty(configuredTarget))
        {
            RebirthBlockPickupTargetDecision configured = ResolveNamedTarget(
                configuredTarget, count, "explicit RebirthPickupTarget", false);
            if (configured.IsValid)
                return FinalizeStorageTarget(configured);

            Log.Warning("[REBIRTH BlockPickup] Invalid RebirthPickupTarget '" + configuredTarget
                + "' on block " + sourceName + "; falling back to automatic resolution.");
        }

        // Exact 2.6 compatibility properties. isReplacementItem=true forces item
        // lookup; false/absent treats the name as a block first.
        string legacyReplacement = FirstConfiguredName(GetProperty(source, PropertyLegacyReplacementName));
        if (!string.IsNullOrEmpty(legacyReplacement))
        {
            bool isItem = ParseBool(GetProperty(source, PropertyLegacyReplacementIsItem));
            RebirthBlockPickupTargetDecision configured = ResolveNamedTarget(
                legacyReplacement, count, "legacy ReplacementBlockName", !isItem);
            if (configured.IsValid)
                return FinalizeStorageTarget(configured);

            Log.Warning("[REBIRTH BlockPickup] Invalid ReplacementBlockName '" + legacyReplacement
                + "' on block " + sourceName + ".");
            return Invalid("invalid ReplacementBlockName");
        }

        // Auto-shape families (frameShapes, woodShapes, etc.) are placed as a
        // concrete generated shape block, but the recoverable inventory item is
        // the selector helper. Preserve the exact current shape in ItemValue.Meta
        // so the recovered stack can still open the Shapes window and defaults to
        // the shape the player actually picked up.
        if (source.GetAutoShapeType() == EAutoShapeType.Shape)
        {
            Block autoShapeHelper = source.GetAutoShapeHelperBlock();
            if (IsSelectableVariantHelper(autoShapeHelper))
            {
                return CreateSelectorHelperDecision(
                    source, autoShapeHelper, count, "auto-shape selector helper");
            }
        }

        // Preserve the original vanilla CanPickup param1 result. Generated
        // auto-shapes deliberately point CanPickup param1 at their VariantHelper,
        // which is a player-facing selector item rather than an unsafe technical
        // helper. Honor that relationship instead of collapsing back to the
        // concrete placed block.
        string vanillaPickupTarget = FirstConfiguredName(
            GetProperty(source, PropertyVanillaPickupTarget));
        if (!string.IsNullOrEmpty(vanillaPickupTarget))
        {
            Block vanillaSelectorHelper;
            if (TryResolveSelectableVariantHelper(vanillaPickupTarget, out vanillaSelectorHelper))
            {
                return CreateSelectorHelperDecision(
                    source, vanillaSelectorHelper, count, "preserved base CanPickup selector target");
            }

            RebirthBlockPickupTargetDecision vanillaConfigured = ResolveNamedTarget(
                vanillaPickupTarget, count, "preserved base CanPickup target", false);
            // CanPickup param1 is item,quality. Do not lose its quality and cache
            // one randomly created tier for every subsequent pickup of this block.
            string[] pickupParts = GetProperty(source, PropertyVanillaPickupTarget).Split(',');
            int pickupQuality;
            if (vanillaConfigured.IsValid && !vanillaConfigured.IsBlockTarget &&
                pickupParts.Length > 1 && int.TryParse(pickupParts[1].Trim(), out pickupQuality))
                vanillaConfigured = new RebirthBlockPickupTargetDecision(
                    ItemClass.CreateItemValue(vanillaPickupTarget, Math.Max(1, pickupQuality)),
                    BlockValue.Air, count, vanillaConfigured.Reason);
            if (vanillaConfigured.IsValid)
                return FinalizeStorageTarget(vanillaConfigured);
        }

        // General empty-variant rule. Any block whose downgrade chain reaches an
        // Empty visual resolves to that visual. Storage sources use an existing or
        // code-generated secure player-storage copy of the Empty block, leaving the
        // POI Empty definition itself unchanged.
        BlockValue emptyVariantTarget;
        bool emptyVariantIsPlayerStorage;
        if (TryResolvePreferredEmptyVariant(
            source, out emptyVariantTarget, out emptyVariantIsPlayerStorage))
        {
            return FinalizeStorageTarget(CreateBlockDecision(
                emptyVariantTarget, count,
                emptyVariantIsPlayerStorage
                    ? "secure source-specific player-storage variant"
                    : "Empty variant from downgrade chain"));
        }

        string legacyTarget;
        if (RebirthBlockPickupLegacyTargets.TryGetTarget(sourceName, out legacyTarget))
        {
            BlockValue legacyValue;
            if (TryResolveStorageBlock(legacyTarget, out legacyValue))
                return FinalizeStorageTarget(CreateBlockDecision(
                    legacyValue, count, "custom 2.6 container mapping"));
        }

        // Respect base-game configured pickup results before automatic container
        // conversion. PickupTarget and PickedUpItemValue are inherited properties.
        if (!string.IsNullOrEmpty(source.PickupTarget))
        {
            RebirthBlockPickupTargetDecision pickupTarget = ResolveNamedTarget(
                FirstConfiguredName(source.PickupTarget), count, "base PickupTarget", false);
            if (pickupTarget.IsValid)
                return FinalizeStorageTarget(pickupTarget);
        }

        if (!string.IsNullOrEmpty(source.PickedUpItemValue))
        {
            string[] parts = source.PickedUpItemValue.Split(',');
            string itemName = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            int quality = 1;
            if (parts.Length >= 2)
                int.TryParse(parts[1].Trim(), out quality);
            if (quality < 1)
                quality = 1;

            ItemValue pickedItem = ItemClass.CreateItemValue(itemName, quality);
            if (pickedItem != null && pickedItem.type > 0 && pickedItem.ItemClass != null)
            {
                BlockValue pickedBlock = BlockValue.Air;
                if (pickedItem.ItemClass.IsBlock() && Block.list != null &&
                    pickedItem.type >= 0 && pickedItem.type < Block.list.Length &&
                    Block.list[pickedItem.type] != null)
                {
                    pickedBlock = Block.GetBlockValue(Block.list[pickedItem.type].GetBlockName());
                }

                return FinalizeStorageTarget(new RebirthBlockPickupTargetDecision(
                    pickedItem, pickedBlock, count, "base PickedUpItemValue"));
            }
        }

        bool sourceIsStorage = RebirthBlockPickupClassifier.IsStorageContainer(source);

        // Manual selector families use SelectAlternates + PlaceAltBlockValue rather
        // than the auto-shape system. farmPlotBlockVariantHelper is the canonical
        // base-game example. Reverse-resolve a placed alternate back to its helper
        // so pickup returns the selector-enabled item instead of a dead-end concrete
        // variant. Storage blocks deliberately stay on the secure storage path.
        if (!sourceIsStorage)
        {
            Block selectorHelper;
            if (TryResolvePreferredSelectorHelper(source, out selectorHelper))
            {
                return CreateSelectorHelperDecision(
                    source, selectorHelper, count, "SelectAlternates variant helper");
            }
        }

        if (!sourceIsStorage || LooksLikeOpenEmptyOrBrokenVariant(sourceName))
        {
            BlockValue reverseTarget;
            if (TryResolveReverseDowngradeStorage(source, out reverseTarget))
                return FinalizeStorageTarget(CreateBlockDecision(
                    reverseTarget, count, "3.0 reverse DowngradeBlock container relation"));
        }

        BlockValue sourceValue = Block.GetBlockValue(sourceName);
        if (sourceIsStorage)
            return FinalizeStorageTarget(CreateBlockDecision(
                sourceValue, count, "source is already a storage block"));

        return CreateBlockDecision(sourceValue, count, "same block");
    }

    private static bool TryResolvePreferredEmptyVariant(
        Block source,
        out BlockValue target,
        out bool playerStorage)
    {
        target = BlockValue.Air;
        playerStorage = false;
        if (source == null)
            return false;

        bool sourceIsStorage = RebirthBlockPickupClassifier.IsStorageContainer(source);

        // Every storage source receives a deterministic source-specific secure
        // variant. Prefer it before walking the downgrade chain so the recovered
        // block keeps the source LootList (and therefore its native size/sounds)
        // while the generated definition independently selects the Empty visual.
        if (sourceIsStorage)
        {
            string sourceGeneratedName =
                RebirthBlockPickupEmptyVariantGenerator.GetGeneratedBlockName(
                    source.GetBlockName());
            if (TryResolveStorageBlock(sourceGeneratedName, out target))
            {
                playerStorage = true;
                return true;
            }
        }

        HashSet<int> visited = new HashSet<int>();
        Block current = source;

        for (int depth = 0; depth < 16 && current != null; depth++)
        {
            string currentName = current.GetBlockName() ?? string.Empty;
            if (currentName.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // A raw Empty block can be punched directly even though it has no
                // storage feature. Trace it back to the best storage member of its
                // downgrade family, then return that source's generated secure copy.
                // This keeps the Empty model instead of returning decoration.
                if (!sourceIsStorage)
                {
                    BlockValue familyStorage;
                    if (TryResolveReverseDowngradeStorage(current, out familyStorage))
                    {
                        BlockValue familyPlayerStorage;
                        if (TryResolvePlayerStorageVariant(
                                familyStorage, out familyPlayerStorage))
                        {
                            target = familyPlayerStorage;
                            playerStorage = true;
                            return true;
                        }
                    }
                }

                string[] storageCandidates =
                {
                    RebirthBlockPickupEmptyVariantGenerator.GetGeneratedBlockName(currentName),
                    currentName + "_Player",
                    currentName + "Player"
                };

                for (int i = 0; i < storageCandidates.Length; i++)
                {
                    if (TryResolveStorageBlock(storageCandidates[i], out target))
                    {
                        playerStorage = true;
                        return true;
                    }
                }

                // An Empty block can already be a player-storage definition.
                if (RebirthBlockPickupClassifier.IsStorageContainer(current) &&
                    (currentName.EndsWith("_Player", StringComparison.OrdinalIgnoreCase) ||
                     currentName.EndsWith("Player", StringComparison.OrdinalIgnoreCase)))
                {
                    target = Block.GetBlockValue(currentName);
                    playerStorage = true;
                    return !target.isair;
                }

                if (sourceIsStorage)
                {
                    Log.Warning("[REBIRTH BlockPickup] Storage block "
                        + source.GetBlockName() + " reaches Empty variant " + currentName
                        + " but no secure player-storage variant was generated or defined.");
                    return false;
                }

                target = Block.GetBlockValue(currentName);
                return !target.isair && target.Block != null &&
                    !RebirthBlockPickupClassifier.IsTechnicalHelper(target.Block);
            }

            BlockValue next = current.DowngradeBlock;
            if (next.isair || next.Block == null || next.type == current.blockID ||
                !visited.Add(next.type))
                return false;

            current = next.Block;
        }

        return false;
    }

    private static RebirthBlockPickupTargetDecision ResolveNamedTarget(
        string targetName,
        int count,
        string reason,
        bool blockFirst)
    {
        if (string.IsNullOrEmpty(targetName))
            return Invalid("empty target");

        if (blockFirst)
        {
            BlockValue blockValue;
            if (TryResolveBlock(targetName, out blockValue))
                return CreateBlockDecision(blockValue, count, reason + " (block)");
        }

        ItemValue itemValue = ItemClass.GetItem(targetName);
        if (itemValue != null && itemValue.type > 0 && itemValue.ItemClass != null)
        {
            BlockValue blockValue = BlockValue.Air;
            if (itemValue.ItemClass.IsBlock() && Block.list != null &&
                itemValue.type >= 0 && itemValue.type < Block.list.Length &&
                Block.list[itemValue.type] != null)
            {
                blockValue = Block.GetBlockValue(Block.list[itemValue.type].GetBlockName());
                if (!blockValue.isair && blockValue.Block != null &&
                    RebirthBlockPickupClassifier.IsTechnicalHelper(blockValue.Block))
                {
                    return Invalid(reason + " (technical helper item target rejected)");
                }
            }

            return new RebirthBlockPickupTargetDecision(itemValue, blockValue, count, reason + " (item)");
        }

        if (!blockFirst)
        {
            BlockValue blockValue;
            if (TryResolveBlock(targetName, out blockValue))
                return CreateBlockDecision(blockValue, count, reason + " (block)");
        }

        return Invalid("target not found: " + targetName);
    }

    private static RebirthBlockPickupTargetDecision CreateSelectorHelperDecision(
        Block source,
        Block helper,
        int count,
        string reason)
    {
        if (source == null || !IsSelectableVariantHelper(helper))
            return Invalid((reason ?? "selector helper") + " (invalid selector helper)");

        BlockValue helperValue = Block.GetBlockValue(helper.GetBlockName());
        if (helperValue.isair || helperValue.Block == null)
            return Invalid((reason ?? "selector helper") + " (helper block missing)");

        ItemValue itemValue = helperValue.ToItemValue();
        int selectedIndex = helper.GetAlternateBlockIndex(source.GetBlockName());
        if (selectedIndex >= 0)
            itemValue.Meta = selectedIndex;

        return new RebirthBlockPickupTargetDecision(
            itemValue, helperValue, count,
            (reason ?? "selector helper") +
            (selectedIndex >= 0 ? " meta=" + selectedIndex : ""));
    }

    private static bool TryResolveSelectableVariantHelper(
        string helperName,
        out Block helper)
    {
        helper = string.IsNullOrEmpty(helperName)
            ? null
            : Block.GetBlockByName(helperName);
        return IsSelectableVariantHelper(helper);
    }

    private static bool IsSelectableVariantHelper(Block block)
    {
        if (block == null || !block.SelectAlternates)
            return false;

        string[] alternates = block.GetAltBlockNames();
        return alternates != null && alternates.Length > 0;
    }

    private static bool TryResolvePreferredSelectorHelper(
        Block source,
        out Block helper)
    {
        helper = null;
        if (source == null)
            return false;

        EnsureReverseSelectorHelperIndex();

        List<Block> candidates;
        lock (Sync)
        {
            if (reverseSelectorHelpers == null ||
                !reverseSelectorHelpers.TryGetValue(source.blockID, out candidates) ||
                candidates == null || candidates.Count == 0)
            {
                return false;
            }

            Block best = null;
            int bestScore = int.MinValue;
            string sourceName = source.GetBlockName() ?? string.Empty;
            for (int i = 0; i < candidates.Count; i++)
            {
                Block candidate = candidates[i];
                if (!IsSelectableVariantHelper(candidate))
                    continue;

                int score = CommonPrefixLength(sourceName, candidate.GetBlockName() ?? string.Empty);
                if (candidate.GetAutoShapeType() == EAutoShapeType.Helper)
                    score += 1000;
                if (RebirthBlockPickupClassifier.HasFilterTag(candidate, "SC_playerHelpers"))
                    score += 100;
                if ((candidate.GetBlockName() ?? string.Empty).IndexOf(
                        "VariantHelper", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 50;

                if (best == null || score > bestScore ||
                    (score == bestScore && string.CompareOrdinal(
                        candidate.GetBlockName(), best.GetBlockName()) < 0))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            helper = best;
            return helper != null;
        }
    }

    private static void EnsureReverseSelectorHelperIndex()
    {
        lock (Sync)
        {
            if (reverseSelectorHelpers != null)
                return;
            if (!Block.BlocksLoaded || Block.list == null || Block.list.Length == 0)
                return;

            Dictionary<int, List<Block>> index = new Dictionary<int, List<Block>>();
            if (Block.list != null)
            {
                for (int i = 0; i < Block.list.Length; i++)
                {
                    Block candidate = Block.list[i];
                    if (!IsSelectableVariantHelper(candidate))
                        continue;

                    Block[] alternates = candidate.GetAltBlocks();
                    if (alternates == null)
                        continue;

                    for (int j = 0; j < alternates.Length; j++)
                    {
                        Block alternate = alternates[j];
                        if (alternate == null)
                            continue;

                        List<Block> list;
                        if (!index.TryGetValue(alternate.blockID, out list))
                        {
                            list = new List<Block>();
                            index.Add(alternate.blockID, list);
                        }
                        if (!list.Contains(candidate))
                            list.Add(candidate);
                    }
                }
            }

            reverseSelectorHelpers = index;
        }
    }

    private static RebirthBlockPickupTargetDecision FinalizeStorageTarget(
        RebirthBlockPickupTargetDecision decision)
    {
        if (!decision.IsBlockTarget || !RebirthBlockPickupClassifier.IsStorageContainer(decision.Target.Block))
            return decision;

        BlockValue playerStorage;
        if (TryResolvePlayerStorageVariant(decision.Target, out playerStorage))
        {
            return CreateBlockDecision(
                playerStorage,
                decision.Count,
                decision.Reason + " -> secure player-storage variant");
        }

        return decision;
    }

    private static RebirthBlockPickupTargetDecision CreateBlockDecision(
        BlockValue blockValue,
        int count,
        string reason)
    {
        if (blockValue.isair || blockValue.Block == null)
            return Invalid(reason + " (invalid block)");
        if (RebirthBlockPickupClassifier.IsTechnicalHelper(blockValue.Block))
            return Invalid(reason + " (technical helper target rejected)");

        return new RebirthBlockPickupTargetDecision(
            blockValue.ToItemValue(), blockValue, count, reason);
    }

    private static RebirthBlockPickupTargetDecision Invalid(string reason)
    {
        return new RebirthBlockPickupTargetDecision(ItemValue.None, BlockValue.Air, 1, reason);
    }

    private static int ParseCount(Block source)
    {
        string raw = GetProperty(source, PropertyReplacementCount);
        int count;
        return int.TryParse(raw, out count) && count > 0 ? count : 1;
    }

    private static bool ParseBool(string raw)
    {
        bool value;
        return bool.TryParse(raw, out value) && value;
    }

    private static string FirstConfiguredName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        int comma = raw.IndexOf(',');
        return (comma >= 0 ? raw.Substring(0, comma) : raw).Trim();
    }

    private static bool TryResolvePlayerStorageVariant(
        BlockValue canonicalStorage,
        out BlockValue playerStorage)
    {
        playerStorage = BlockValue.Air;
        if (canonicalStorage.isair || canonicalStorage.Block == null ||
            !RebirthBlockPickupClassifier.IsStorageContainer(canonicalStorage.Block))
            return false;

        string canonicalName = canonicalStorage.Block.GetBlockName() ?? string.Empty;
        if (canonicalName.EndsWith("_Player", StringComparison.OrdinalIgnoreCase) ||
            canonicalName.EndsWith("Player", StringComparison.OrdinalIgnoreCase))
        {
            playerStorage = canonicalStorage;
            return true;
        }

        string[] candidates =
        {
            RebirthBlockPickupEmptyVariantGenerator.GetGeneratedBlockName(canonicalName),
            canonicalName + "_Player",
            canonicalName + "Player"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            BlockValue candidate;
            if (TryResolveStorageBlock(candidates[i], out candidate))
            {
                playerStorage = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveReverseDowngradeStorage(Block source, out BlockValue target)
    {
        EnsureReverseDowngradeIndex();

        List<Block> candidates;
        lock (Sync)
        {
            if (reverseDowngradeStorageBlocks == null ||
                !reverseDowngradeStorageBlocks.TryGetValue(source.blockID, out candidates) ||
                candidates == null || candidates.Count == 0)
            {
                target = BlockValue.Air;
                return false;
            }

            Block best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                Block candidate = candidates[i];
                int score = ScoreCandidate(source, candidate);
                if (best == null || score > bestScore ||
                    (score == bestScore && string.CompareOrdinal(candidate.GetBlockName(), best.GetBlockName()) < 0))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            if (best == null)
            {
                target = BlockValue.Air;
                return false;
            }

            target = Block.GetBlockValue(best.GetBlockName());
            return !target.isair;
        }
    }

    private static void EnsureReverseDowngradeIndex()
    {
        lock (Sync)
        {
            if (reverseDowngradeStorageBlocks != null)
                return;
            if (!Block.BlocksLoaded || Block.list == null || Block.list.Length == 0)
                return;

            Dictionary<int, List<Block>> index = new Dictionary<int, List<Block>>();
            if (Block.list != null)
            {
                for (int i = 0; i < Block.list.Length; i++)
                {
                    Block candidate = Block.list[i];
                    if (candidate == null ||
                        !RebirthBlockPickupClassifier.IsStorageContainer(candidate) ||
                        IsPlayerStorageVariantName(candidate.GetBlockName()))
                    {
                        continue;
                    }

                    // Index the complete downgrade chain, not only the direct
                    // child. Some container families pass through closed/open or
                    // damaged states before reaching their reusable Empty model.
                    // A raw Empty block must still be able to find the original
                    // storage source whose generated player copy preserves size.
                    HashSet<int> visited = new HashSet<int>();
                    Block current = candidate;
                    for (int depth = 0; depth < 16 && current != null; depth++)
                    {
                        BlockValue downgrade = current.DowngradeBlock;
                        if (downgrade.isair ||
                            downgrade.type == current.blockID ||
                            !visited.Add(downgrade.type))
                        {
                            break;
                        }

                        List<Block> list;
                        if (!index.TryGetValue(downgrade.type, out list))
                        {
                            list = new List<Block>();
                            index.Add(downgrade.type, list);
                        }
                        if (!list.Contains(candidate))
                            list.Add(candidate);

                        current = downgrade.Block;
                    }
                }
            }

            reverseDowngradeStorageBlocks = index;
        }
    }

    private static bool IsPlayerStorageVariantName(string name)
    {
        return !string.IsNullOrEmpty(name) &&
            (name.EndsWith("_Player", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith("Player", StringComparison.OrdinalIgnoreCase));
    }

    private static int ScoreCandidate(Block source, Block candidate)
    {
        if (candidate == null)
            return int.MinValue;

        string sourceName = source.GetBlockName() ?? string.Empty;
        string candidateName = candidate.GetBlockName() ?? string.Empty;
        int score = CommonPrefixLength(sourceName, candidateName);

        if (candidateName.IndexOf("Closed", StringComparison.OrdinalIgnoreCase) >= 0)
            score += 80;
        if (candidateName.IndexOf("Full", StringComparison.OrdinalIgnoreCase) >= 0)
            score += 50;
        if (candidateName.IndexOf("Open", StringComparison.OrdinalIgnoreCase) < 0 &&
            candidateName.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) < 0 &&
            candidateName.IndexOf("Broken", StringComparison.OrdinalIgnoreCase) < 0)
            score += 30;
        if (!LooksLikeTechnicalHelper(candidateName, candidate))
            score += 20;
        if (RebirthBlockPickupClassifier.HasFilterTag(candidate, "MC_playerBlocks"))
            score += 10;

        return score;
    }

    private static bool LooksLikeOpenEmptyOrBrokenVariant(string name)
    {
        return name.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Broken", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool LooksLikeTechnicalHelper(string name, Block block)
    {
        return RebirthBlockPickupClassifier.HasFilterTag(block, "SC_playerHelpers") ||
            name.IndexOf("RandomLootHelper", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("VariantHelper", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CommonPrefixLength(string left, string right)
    {
        int length = Math.Min(left.Length, right.Length);
        int count = 0;
        while (count < length && char.ToUpperInvariant(left[count]) == char.ToUpperInvariant(right[count]))
            count++;
        return count;
    }

    private static bool TryResolveStorageBlock(string blockName, out BlockValue value)
    {
        if (!TryResolveBlock(blockName, out value))
            return false;
        return RebirthBlockPickupClassifier.IsStorageContainer(value.Block);
    }

    private static bool TryResolveBlock(string blockName, out BlockValue value)
    {
        value = Block.GetBlockValue(blockName);
        return !value.isair && value.Block != null;
    }

    private static string GetProperty(Block block, string propertyName)
    {
        string value;
        if (block != null && block.Properties != null &&
            block.Properties.Values.TryGetValue(propertyName, out value))
            return value;
        return string.Empty;
    }
}
