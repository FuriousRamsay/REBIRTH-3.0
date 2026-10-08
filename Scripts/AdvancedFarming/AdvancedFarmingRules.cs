using System;
using System.Collections.Generic;

#nullable disable

public sealed class AdvancedFarmingRuleSet
{
    public readonly string Name;
    public readonly int DewChanceMin;
    public readonly int DewChanceMax;

    public AdvancedFarmingRuleSet(string name, int dewChanceMin, int dewChanceMax)
    {
        Name = name;
        DewChanceMin = dewChanceMin;
        DewChanceMax = dewChanceMax;
    }
}

public static class AdvancedFarmingRuleResolver
{
    private static readonly Dictionary<string, AdvancedFarmingRuleSet> s_rules =
        new Dictionary<string, AdvancedFarmingRuleSet>(StringComparer.OrdinalIgnoreCase)
        {
            { "forest", new AdvancedFarmingRuleSet("forest", 80, 100) },
            { "pine_forest", new AdvancedFarmingRuleSet("pine_forest", 80, 100) },
            { "burnt_forest", new AdvancedFarmingRuleSet("burnt_forest", 60, 80) },
            { "desert", new AdvancedFarmingRuleSet("desert", 40, 60) },
            { "snow", new AdvancedFarmingRuleSet("snow", 40, 60) },
            { "wasteland", new AdvancedFarmingRuleSet("wasteland", 40, 60) },
        };

    private static readonly Dictionary<string, string> s_biomeOverrides =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static readonly AdvancedFarmingRuleSet s_fallback =
        new AdvancedFarmingRuleSet("fallback", 40, 60);

    public static void SetBiomeOverride(string biomeName, string ruleName)
    {
        if (string.IsNullOrEmpty(biomeName))
            return;

        if (string.IsNullOrEmpty(ruleName))
        {
            s_biomeOverrides.Remove(biomeName);
            return;
        }

        s_biomeOverrides[biomeName] = ruleName;
    }

    public static AdvancedFarmingRuleSet Resolve(string biomeName)
    {
        if (!string.IsNullOrEmpty(biomeName) && s_biomeOverrides.TryGetValue(biomeName, out string mapped))
            biomeName = mapped;

        if (!string.IsNullOrEmpty(biomeName) && s_rules.TryGetValue(biomeName, out AdvancedFarmingRuleSet rule))
            return rule;

        return s_fallback;
    }

    public static string Describe()
    {
        return "[AdvancedFarmingRules] dew defaults: forest/pine 80-100, burnt 60-80, desert/snow/wasteland 40-60; farm-plot rain uses measured rainfall exposure rather than biome chance simulation; overrides=" + s_biomeOverrides.Count;
    }

    public static int DeterministicRollPercent(Vector3i pos, int cycleIndex, int salt)
    {
        unchecked
        {
            int h = pos.x * 73856093 ^ pos.y * 19349663 ^ pos.z * 83492791 ^ cycleIndex * 1103515245 ^ salt;
            // Preserve every legacy result except the one value that signed negation
            // cannot normalize. int.MinValue becomes magnitude 2^31 instead of
            // remaining negative and producing a negative remainder.
            uint magnitude = h == int.MinValue
                ? 0x80000000u
                : (uint)(h < 0 ? -h : h);
            return (int)(magnitude % 101u);
        }
    }
}
