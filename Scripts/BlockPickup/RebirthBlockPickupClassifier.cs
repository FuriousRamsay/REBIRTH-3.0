using System;
using System.Collections.Generic;

#nullable disable

public enum RebirthBlockPickupClassification
{
    Denied = 0,
    BaseGamePickup = 1,
    StorageContainer = 2,
    ObjectBlock = 3,
    ExplicitAllow = 4
}

public sealed class RebirthBlockPickupDecision
{
    public static readonly RebirthBlockPickupDecision DeniedUnknown =
        new RebirthBlockPickupDecision(false, RebirthBlockPickupClassification.Denied, "unknown block");

    public readonly bool Allowed;
    public readonly RebirthBlockPickupClassification Classification;
    public readonly string Reason;

    public RebirthBlockPickupDecision(
        bool allowed,
        RebirthBlockPickupClassification classification,
        string reason)
    {
        Allowed = allowed;
        Classification = classification;
        Reason = reason ?? string.Empty;
    }

    public override string ToString()
    {
        return "allowed=" + Allowed + " classification=" + Classification +
            " reason=" + Reason;
    }
}

/// <summary>
/// Cached code-driven pickup eligibility. Absolute safety exclusions are applied
/// before XML opt-ins. Technical/helper blocks are rejected before storage
/// capability so a helper can never become a recoverable player block.
/// </summary>
public static class RebirthBlockPickupClassifier
{
    public const string PropertyPickup = "RebirthPickup";
    public const string PropertyVanillaCanPickup = "RebirthVanillaCanPickup";

    private static RebirthBlockPickupDecision[] cache;

    private static readonly HashSet<string> AllowedObjectSubcategories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SC_decor",
            "SC_lighting",
            "SC_plumbing",
            "SC_electrical",
            "SC_signs",
            "SC_commercial",
            "SC_residential",
            "SC_industrial",
            "SC_traps",
            "SC_loot"
        };

    private static readonly HashSet<string> DeniedSubcategories =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SC_terrain",
            "SC_crops",
            "SC_trees",
            "SC_shrubbery",
            "SC_questblocks",
            "SC_destruction",
            "SC_construction",
            "SC_doors",
            "SC_windows",
            "SC_fences"
        };

    public static void ClearCache()
    {
        cache = null;
    }

    public static RebirthBlockPickupDecision Evaluate(Block block)
    {
        if (block == null)
            return RebirthBlockPickupDecision.DeniedUnknown;

        RebirthBlockPickupDecision[] local = cache;
        if (local == null || Block.list == null || local.Length != Block.list.Length)
        {
            local = new RebirthBlockPickupDecision[Block.list != null ? Block.list.Length : 0];
            cache = local;
        }

        int id = block.blockID;
        if (id >= 0 && id < local.Length && local[id] != null)
            return local[id];

        RebirthBlockPickupDecision decision = EvaluateUncached(block);
        if (id >= 0 && id < local.Length)
            local[id] = decision;
        return decision;
    }

    private static RebirthBlockPickupDecision EvaluateUncached(Block block)
    {
        string explicitRule = GetProperty(block, PropertyPickup);
        if (string.Equals(explicitRule, "Deny", StringComparison.OrdinalIgnoreCase))
            return Deny("explicit RebirthPickup=Deny");

        if (block.blockID == 0 ||
            string.Equals(block.GetBlockName(), "air", StringComparison.OrdinalIgnoreCase))
            return Deny("air");

        if (block.shape == null)
            return Deny("missing shape");

        if (block.shape.IsTerrain())
            return Deny("terrain shape");

        string name = block.GetBlockName() ?? string.Empty;

        // Absolute denials: XML is not allowed to re-enable these categories.
        if (IsVehicleBlock(block))
            return Deny("vehicle or automotive block");
        if (IsStructuralInfrastructureFamily(name))
            return Deny("structural infrastructure block family");
        if (IsGoreOrGarbageDebrisFamily(name))
            return Deny("gore or garbage debris block family");
        if (IsSwitchOrControlBlock(block, name))
            return Deny("switch, trigger or activation-control block");

        // Propane tanks remain an absolute denial. Some legitimate loot containers
        // (notably the rusty/gas barrel families) also expose TEFeatureExplodable,
        // so explodability alone must not reject a real storage container. Their
        // emptiness, touched state, lock state and ownership are validated later.
        if (IsPropaneTank(name))
            return Deny("propane tank");
        if (IsExplodableBlock(block) && !IsStorageContainer(block))
            return Deny("non-storage explodable block");
        if (IsTechnicalHelper(block, name))
            return Deny("technical helper or placeholder block");
        if (DowngradesIntoStorageContainer(block))
            return Deny("sealed shell downgrades into a loot container");

        if (string.Equals(explicitRule, "Allow", StringComparison.OrdinalIgnoreCase))
            return Allow(RebirthBlockPickupClassification.ExplicitAllow,
                "explicit RebirthPickup=Allow");

        string vanillaCanPickup = GetProperty(block, PropertyVanillaCanPickup);
        bool vanillaCanPickupValue;
        if (bool.TryParse(vanillaCanPickup, out vanillaCanPickupValue))
        {
            if (!vanillaCanPickupValue)
                return Deny("base CanPickup=false");

            return Allow(RebirthBlockPickupClassification.BaseGamePickup,
                "base CanPickup=true preserved as REBIRTH hold-E pickup");
        }

        if (block is BlockWorkstation)
            return Allow(RebirthBlockPickupClassification.ObjectBlock,
                "workstation validated at runtime");

        // The single base shape block remains recoverable. All generated/non-base
        // shape variants remain denied below.
        if (string.Equals(name, "frameShapes", StringComparison.OrdinalIgnoreCase))
            return Allow(RebirthBlockPickupClassification.BaseGamePickup,
                "base frameShapes block");

        if (block.GetAutoShapeType() != EAutoShapeType.None)
            return Deny("non-base auto-shape block");

        if (HasFilterTag(block, "MC_Shapes"))
            return Deny("shape-family block");

        if (IsStorageContainer(block))
        {
            if (HasFilterTag(block, "SC_questblocks"))
                return Deny("quest storage");
            if (block is BlockVendingMachine)
                return Deny("vending machine");
            return Allow(RebirthBlockPickupClassification.StorageContainer,
                "composite storage feature");
        }

        if (HasAnyFilterTag(block, AllowedObjectSubcategories))
            return Allow(RebirthBlockPickupClassification.ObjectBlock,
                "approved object subcategory");

        if (HasAnyFilterTag(block, DeniedSubcategories))
            return Deny("structural, terrain, vegetation or quest subcategory");

        if (block.IsProp && block.shape is BlockShapeModelEntity)
            return Allow(RebirthBlockPickupClassification.ObjectBlock,
                "independent ModelEntity prop");

        return Deny("not classified as an independent object");
    }

    public static bool IsStorageContainer(Block block)
    {
        BlockCompositeTileEntity composite = block as BlockCompositeTileEntity;
        return composite != null && composite.CompositeData != null &&
            composite.CompositeData.HasFeature<TEFeatureStorage>();
    }

    public static bool IsTechnicalHelper(Block block)
    {
        return block != null && IsTechnicalHelper(
            block, block.GetBlockName() ?? string.Empty);
    }

    public static bool HasFilterTag(Block block, string tag)
    {
        if (block == null || block.FilterTags == null || string.IsNullOrEmpty(tag))
            return false;

        for (int i = 0; i < block.FilterTags.Length; i++)
        {
            if (string.Equals(block.FilterTags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsVehicleBlock(Block block)
    {
        if (HasFilterTag(block, "SC_automotive"))
            return true;

        string model = GetProperty(block, "Model");
        return model.IndexOf("/Vehicles/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            model.IndexOf("\\Vehicles\\", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsStructuralInfrastructureFamily(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // These are level-design/structural families, not portable decor.  iBeam
        // also carries SC_industrial, which previously caused it to bypass the
        // structural intent of its SC_construction tag.
        return name.StartsWith("iBeam", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("guardRail", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGoreOrGarbageDebrisFamily(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // World dressing/debris is never a portable object, even when a model or
        // loot feature would otherwise make it look like an approved prop/storage
        // block to the generic classifier. Keep real trash cans/bins outside this
        // rule; the environmental pile/decor families are the forbidden cases.
        return name.IndexOf("gore", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.StartsWith("garbage_decor", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("rubbishDecor", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("cntTrashPile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSwitchOrControlBlock(Block block, string name)
    {
        if (block is BlockActivateSingle || block is BlockActivateSwitch ||
            block is BlockSwitch)
            return true;

        string typeName = block.GetType().Name;
        return typeName.IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("Trigger", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("switch", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsExplodableBlock(Block block)
    {
        BlockCompositeTileEntity composite = block as BlockCompositeTileEntity;
        return composite != null && composite.CompositeData != null &&
            composite.CompositeData.HasFeature<TEFeatureExplodable>();
    }

    private static bool IsPropaneTank(string name)
    {
        return name.IndexOf("propane", StringComparison.OrdinalIgnoreCase) >= 0 &&
            name.IndexOf("tank", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool DowngradesIntoStorageContainer(Block block)
    {
        if (block == null || IsStorageContainer(block))
            return false;

        HashSet<int> visited = new HashSet<int>();
        Block current = block;
        for (int depth = 0; depth < 8; depth++)
        {
            BlockValue nextValue = current.DowngradeBlock;
            if (nextValue.isair || nextValue.Block == null ||
                nextValue.type == current.blockID || !visited.Add(nextValue.type))
                return false;

            Block next = nextValue.Block;
            if (IsStorageContainer(next))
                return true;

            current = next;
        }

        return false;
    }

    private static bool HasAnyFilterTag(Block block, HashSet<string> tags)
    {
        if (block == null || block.FilterTags == null)
            return false;
        for (int i = 0; i < block.FilterTags.Length; i++)
        {
            if (tags.Contains(block.FilterTags[i]))
                return true;
        }
        return false;
    }

    private static bool IsTechnicalHelper(Block block, string name)
    {
        return HasFilterTag(block, "SC_playerHelpers") ||
            name.IndexOf("RandomLootHelper", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("LootHelper", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.EndsWith("VariantHelper", StringComparison.OrdinalIgnoreCase) ||
            name.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetProperty(Block block, string propertyName)
    {
        string value;
        if (block != null && block.Properties != null &&
            block.Properties.Values.TryGetValue(propertyName, out value))
            return value ?? string.Empty;
        return string.Empty;
    }

    private static RebirthBlockPickupDecision Allow(
        RebirthBlockPickupClassification classification,
        string reason)
    {
        return new RebirthBlockPickupDecision(true, classification, reason);
    }

    private static RebirthBlockPickupDecision Deny(string reason)
    {
        return new RebirthBlockPickupDecision(
            false, RebirthBlockPickupClassification.Denied, reason);
    }
}
