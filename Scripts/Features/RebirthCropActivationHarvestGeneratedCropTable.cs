using System;
using System.Text;

#nullable disable

/// <summary>
/// Generated during Milestone 4H from old REBIRTH v2.6 Harmony_CropsGrown.cs,
/// validated against ConfigsDump(50).zip / blocks.xml.
/// 
/// This table intentionally avoids broad heuristic config matches such as master/helper/dead/growth-stage blocks.
/// It is static source data; no XML/config lookup occurs at runtime.
/// </summary>
public static class RebirthCropActivationHarvestGeneratedCropTable
{
    public const string Source = "zzz_REBIRTH__Utils v2.6 / Harmony_CropsGrown.cs + ConfigsDump(50).zip / blocks.xml";
    public const int WildOrNaturalCount = 19;
    public const int PlayerGrownCount = 14;

    private static readonly string[] s_wildOrNaturalCropBlocks = new string[]
    {
        "mushroomBiome2",
        "mushroomBiome4",
        "mushroomRadiated02",
        "mushroomRadiated04",
        "plantedAloe3Harvest",
        "plantedBlueberry3Harvest",
        "plantedChrysanthemum3Harvest",
        "plantedCoffee3Harvest",
        "plantedCorn2Deco",
        "plantedCorn3Harvest",
        "plantedCotton3Harvest",
        "plantedGoldenrod3Harvest",
        "plantedGraceCorn3Harvest",
        "plantedHop3Harvest",
        "plantedMushroom3Harvest",
        "plantedPotato3Harvest",
        "plantedPumpkin3Harvest",
        "plantedSnowberry3Harvest",
        "plantedYucca3Harvest"
    };

    private static readonly string[] s_playerGrownCropBlocks = new string[]
    {
        "plantedAloe3HarvestPlayer",
        "plantedBlueberry3HarvestPlayer",
        "plantedChrysanthemum3HarvestPlayer",
        "plantedCoffee3HarvestPlayer",
        "plantedCorn3HarvestPlayer",
        "plantedCotton3HarvestPlayer",
        "plantedGoldenrod3HarvestPlayer",
        "plantedGraceCorn3HarvestPlayer",
        "plantedHop3HarvestPlayer",
        "plantedMushroom3HarvestPlayer",
        "plantedPotato3HarvestPlayer",
        "plantedPumpkin3HarvestPlayer",
        "plantedSnowberry3HarvestPlayer",
        "plantedYucca3HarvestPlayer"
    };

    public static bool IsWildOrNatural(string blockName)
    {
        return ContainsOrdinal(s_wildOrNaturalCropBlocks, blockName);
    }

    public static bool IsPlayerGrown(string blockName)
    {
        return ContainsOrdinal(s_playerGrownCropBlocks, blockName);
    }

    public static RebirthCropActivationKind Classify(string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return RebirthCropActivationKind.Unknown;

        if (IsPlayerGrown(blockName))
            return RebirthCropActivationKind.PlayerGrown;

        if (IsWildOrNatural(blockName))
            return RebirthCropActivationKind.WildOrNatural;

        return RebirthCropActivationKind.Unknown;
    }

    public static string GetSummaryReport()
    {
        return "[RebirthCropActivationHarvestGeneratedCropTable] source: " + Source
            + "; wildOrNaturalCount: " + WildOrNaturalCount
            + "; playerGrownCount: " + PlayerGrownCount
            + "; generation: Milestone 4H exact old-patch list validated against config";
    }

    public static string GetListReport(string filter)
    {
        StringBuilder sb = new StringBuilder(12000);
        string f = filter ?? string.Empty;

        sb.AppendLine(GetSummaryReport());

        sb.AppendLine("Player-grown crop blocks:");
        AppendMatching(sb, s_playerGrownCropBlocks, f);

        sb.AppendLine("Wild/natural crop blocks:");
        AppendMatching(sb, s_wildOrNaturalCropBlocks, f);

        return sb.ToString();
    }

    private static void AppendMatching(StringBuilder sb, string[] values, string filter)
    {
        bool any = false;

        for (int i = 0; i < values.Length; i++)
        {
            string value = values[i];
            if (!string.IsNullOrEmpty(filter) && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            any = true;
            sb.Append("  ").AppendLine(value);
        }

        if (!any)
            sb.AppendLine("  <none>");
    }

    private static bool ContainsOrdinal(string[] values, string blockName)
    {
        if (string.IsNullOrEmpty(blockName))
            return false;

        for (int i = 0; i < values.Length; i++)
        {
            if (string.Equals(values[i], blockName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
