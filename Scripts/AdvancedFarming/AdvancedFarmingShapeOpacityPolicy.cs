using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Advanced Farming runtime opacity policy for generated shape blocks.
///
/// 7DTD generated shape blocks are exposed as material-family plus shape id, for example
/// woodShapes:cubeFrame. Vanilla shapes.xml mostly uses LightOpacity=0 for fully transparent
/// generated shapes and omits the property for opaque/default shapes. Advanced Farming needs a
/// middle layer for greenhouse behavior: many visually perforated/generated shapes should attenuate
/// SUN light without sealing temperature like a full cube.
///
/// This policy is intentionally runtime-only. It does not modify Config/shapes.xml, so it affects
/// only Advanced Farming light/temperature evaluation and patched SUN propagation.
///
/// v100: results are cached per blockID. This method is called from SUN rebuild and patched vanilla
/// LightProcessor paths, so parsing the generated shape id and allocating normalized strings on every
/// edge/cell is too expensive.
/// </summary>
public static class AdvancedFarmingShapeOpacityPolicy
{
    public const int FullCubeOpacity = 255;

    private struct CachedPolicy
    {
        public bool Initialized;
        public bool HasPolicy;
        public int LightOpacity;
        public bool ThermalPassable;
        public string Category;
        public string ShapeId;
    }

    private static readonly Dictionary<int, CachedPolicy> s_policyByBlockId = new Dictionary<int, CachedPolicy>(512);

    public static bool TryGetGeneratedShapePolicy(Block block, out int lightOpacity, out bool thermalPassable, out string category)
    {
        lightOpacity = 0;
        thermalPassable = true;
        category = string.Empty;

        CachedPolicy cached = GetCachedPolicy(block);
        if (!cached.HasPolicy)
            return false;

        lightOpacity = cached.LightOpacity;
        thermalPassable = cached.ThermalPassable;
        category = cached.Category ?? string.Empty;
        return true;
    }

    public static bool TryGetGeneratedShapeId(Block block, out string shapeId)
    {
        shapeId = string.Empty;
        if (block == null)
            return false;

        CachedPolicy cached = GetCachedPolicy(block);
        if (string.IsNullOrEmpty(cached.ShapeId))
            return false;

        shapeId = cached.ShapeId;
        return true;
    }

    public static string BuildDebugSummary(Block block)
    {
        CachedPolicy cached = GetCachedPolicy(block);
        if (!cached.HasPolicy)
            return "none";

        return (cached.Category ?? string.Empty) + ":shape=" + (cached.ShapeId ?? string.Empty)
            + ":lightOpacity=" + cached.LightOpacity + ":thermalPass=" + cached.ThermalPassable;
    }

    public static void ClearCacheForDiagnostics()
    {
        s_policyByBlockId.Clear();
    }

    private static CachedPolicy GetCachedPolicy(Block block)
    {
        if (block == null)
            return default(CachedPolicy);

        int blockId = block.blockID;
        CachedPolicy cached;
        if (s_policyByBlockId.TryGetValue(blockId, out cached) && cached.Initialized)
            return cached;

        cached = BuildPolicy(block);
        cached.Initialized = true;
        s_policyByBlockId[blockId] = cached;
        return cached;
    }

    private static CachedPolicy BuildPolicy(Block block)
    {
        CachedPolicy result = new CachedPolicy
        {
            Initialized = true,
            HasPolicy = false,
            LightOpacity = 0,
            ThermalPassable = true,
            Category = string.Empty,
            ShapeId = string.Empty
        };

        string shapeId;
        if (!TryGetGeneratedShapeIdUncached(block, out shapeId))
            return result;

        result.ShapeId = shapeId;
        string normalized = Normalize(shapeId);
        if (normalized.Length == 0)
            return result;

        // Explicit visual-hole/empty states should stay very open even if their shape name also
        // contains another category token such as cube, wall, arch, or plate.
        if (Contains(normalized, "broken") || Contains(normalized, "empty"))
            return SetPolicy(result, 3, true, "brokenOrEmpty");

        // Exact cube is the only generated shape category in this policy that remains a true
        // thermal enclosure blocker. Other cube-derived shapes such as cubeFrame are partial.
        if (string.Equals(normalized, "cube", StringComparison.Ordinal))
            return SetPolicy(result, FullCubeOpacity, false, "cube");

        if (Contains(normalized, "platediagonal") || Contains(normalized, "diagonalplate") || Contains(normalized, "platediag") || Contains(normalized, "diagplate"))
            return SetPolicy(result, 3, true, "plateDiagonal");

        if (Contains(normalized, "platecorner") || Contains(normalized, "cornerplate"))
            return SetPolicy(result, 7, true, "plateCorner");

        if (Contains(normalized, "arrowslit"))
            return SetPolicy(result, 7, true, "arrowSlit");

        if (Contains(normalized, "chairrail"))
            return SetPolicy(result, 7, true, "chairRail");

        if (Contains(normalized, "baseboard"))
            return SetPolicy(result, 6, true, "baseboard");

        if (Contains(normalized, "catwalk"))
            return SetPolicy(result, 4, true, "catwalk");

        if (Contains(normalized, "railing") || Contains(normalized, "handrail"))
            return SetPolicy(result, 3, true, "railing");

        if (Contains(normalized, "ramp"))
            return SetPolicy(result, 6, true, "ramp");

        if (Contains(normalized, "pillar") || Contains(normalized, "column"))
            return SetPolicy(result, 7, true, "pillar");

        if (Contains(normalized, "pipe"))
            return SetPolicy(result, 6, true, "pipe");

        if (Contains(normalized, "slope"))
            return SetPolicy(result, 3, true, "slope");

        if (Contains(normalized, "stair"))
            return SetPolicy(result, 7, true, "stairs");

        if (Contains(normalized, "wedge"))
            return SetPolicy(result, 5, true, "wedge");

        if (Contains(normalized, "arch"))
            return SetPolicy(result, 6, true, "arch");

        if (Contains(normalized, "trim"))
            return SetPolicy(result, 3, true, "trim");

        if (Contains(normalized, "beam"))
            return SetPolicy(result, 5, true, "beam");

        if (Contains(normalized, "duct"))
            return SetPolicy(result, 6, true, "duct");

        if (Contains(normalized, "grate"))
            return SetPolicy(result, 7, true, "grate");

        if (Contains(normalized, "table"))
            return SetPolicy(result, 5, true, "table");

        if (Contains(normalized, "ladder"))
            return SetPolicy(result, 3, true, "ladder");

        if (Contains(normalized, "fence"))
            return SetPolicy(result, 4, true, "fence");

        if (ContainsSignToken(normalized))
            return SetPolicy(result, 6, true, "sign");

        if (Contains(normalized, "wall"))
            return SetPolicy(result, 6, true, "wall");

        if (Contains(normalized, "cube"))
            return SetPolicy(result, 7, true, "cubeType");

        return result;
    }

    private static bool TryGetGeneratedShapeIdUncached(Block block, out string shapeId)
    {
        shapeId = string.Empty;
        if (block == null)
            return false;

        string name = block.GetBlockName() ?? string.Empty;
        int colon = name.IndexOf(':');
        if (colon <= 0 || colon + 1 >= name.Length)
            return false;

        string family = name.Substring(0, colon);
        if (!family.EndsWith("Shapes", StringComparison.OrdinalIgnoreCase))
            return false;

        shapeId = name.Substring(colon + 1);
        return shapeId.Length > 0;
    }

    private static CachedPolicy SetPolicy(CachedPolicy policy, int opacity, bool passTemperature, string policyCategory)
    {
        policy.HasPolicy = true;
        policy.LightOpacity = opacity;
        policy.ThermalPassable = passTemperature;
        policy.Category = policyCategory;
        return policy;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }

    private static bool Contains(string value, string token)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf(token, StringComparison.Ordinal) >= 0;
    }

    private static bool ContainsSignToken(string normalized)
    {
        if (string.IsNullOrEmpty(normalized))
            return false;

        // Avoid matching words like "design" while still catching sign/signage/signpost shapes.
        return normalized.StartsWith("sign", StringComparison.Ordinal)
            || normalized.EndsWith("sign", StringComparison.Ordinal)
            || Contains(normalized, "signage")
            || Contains(normalized, "signpost");
    }
}
