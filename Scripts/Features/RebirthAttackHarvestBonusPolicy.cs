using System.Text;

#nullable disable

/// <summary>
/// Complete attack-harvest count bonus category policy.
/// Defaults disabled and vanilla-equivalent.
/// 
/// This applies only inside the manual attack-harvest replacement service.
/// It does not perform XML/config lookups in the patch path.
/// </summary>
public static class RebirthAttackHarvestBonusPolicy
{
    private static bool s_enabled;
    private static int s_destroyBonusPercent;
    private static int s_harvestBonusPercent;
    private static bool s_applyToDestroyDrops = true;
    private static bool s_applyToHarvestDrops = true;
    private static bool s_keepAtLeastOneWhenVanillaCountPositive = true;
    private static bool s_roundDown = true;

    public static bool Enabled { get { return s_enabled; } }
    public static int DestroyBonusPercent { get { return s_destroyBonusPercent; } }
    public static int HarvestBonusPercent { get { return s_harvestBonusPercent; } }
    public static bool ApplyToDestroyDrops { get { return s_applyToDestroyDrops; } }
    public static bool ApplyToHarvestDrops { get { return s_applyToHarvestDrops; } }
    public static bool KeepAtLeastOneWhenVanillaCountPositive { get { return s_keepAtLeastOneWhenVanillaCountPositive; } }
    public static bool RoundDown { get { return s_roundDown; } }

    public static void EnableManualTest(int destroyBonusPercent, int harvestBonusPercent, bool applyToDestroyDrops, bool applyToHarvestDrops)
    {
        s_enabled = true;
        s_destroyBonusPercent = ClampPercent(destroyBonusPercent);
        s_harvestBonusPercent = ClampPercent(harvestBonusPercent);
        s_applyToDestroyDrops = applyToDestroyDrops;
        s_applyToHarvestDrops = applyToHarvestDrops;
        s_keepAtLeastOneWhenVanillaCountPositive = true;
        s_roundDown = true;
    }

    public static void Disable()
    {
        s_enabled = false;
        s_destroyBonusPercent = 0;
        s_harvestBonusPercent = 0;
        s_applyToDestroyDrops = true;
        s_applyToHarvestDrops = true;
        s_keepAtLeastOneWhenVanillaCountPositive = true;
        s_roundDown = true;
    }

    public static int ApplyDestroyBonus(int vanillaCount)
    {
        if (!s_enabled || !s_applyToDestroyDrops)
            return vanillaCount;

        return ApplyBonus(vanillaCount, s_destroyBonusPercent);
    }

    public static int ApplyHarvestBonus(int vanillaCount)
    {
        if (!s_enabled || !s_applyToHarvestDrops)
            return vanillaCount;

        return ApplyBonus(vanillaCount, s_harvestBonusPercent);
    }

    public static int PreviewDestroyBonus(int vanillaCount)
    {
        return ApplyBonus(vanillaCount, s_destroyBonusPercent);
    }

    public static int PreviewHarvestBonus(int vanillaCount)
    {
        return ApplyBonus(vanillaCount, s_harvestBonusPercent);
    }

    public static string GetSummaryReport()
    {
        return "[RebirthAttackHarvestBonusPolicy] enabled: " + s_enabled
            + "; destroyBonusPercent: " + s_destroyBonusPercent
            + "; harvestBonusPercent: " + s_harvestBonusPercent
            + "; applyToDestroyDrops: " + s_applyToDestroyDrops
            + "; applyToHarvestDrops: " + s_applyToHarvestDrops
            + "; keepAtLeastOneWhenVanillaCountPositive: " + s_keepAtLeastOneWhenVanillaCountPositive
            + "; roundDown: " + s_roundDown;
    }

    public static string GetPreviewReport(int sampleCount)
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine(GetSummaryReport());
        sb.AppendLine("  sampleVanillaCount: " + sampleCount);
        sb.AppendLine("  sampleDestroyCount: " + PreviewDestroyBonus(sampleCount));
        sb.AppendLine("  sampleHarvestCount: " + PreviewHarvestBonus(sampleCount));
        return sb.ToString();
    }

    public static string GetDetailReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine(GetSummaryReport());
        sb.AppendLine("  Counts are modified after vanilla HarvestCount/effect calculations.");
        sb.AppendLine("  Destroy drops and harvest drops can be independently enabled.");
        sb.AppendLine("  Defaults disabled, so replacement remains vanilla-equivalent until enabled.");
        sb.AppendLine("  Counts are rounded down to preserve prior REBIRTH harvest/salvage policy.");
        return sb.ToString();
    }

    private static int ApplyBonus(int vanillaCount, int bonusPercent)
    {
        if (vanillaCount <= 0)
            return vanillaCount;

        if (bonusPercent == 0)
            return vanillaCount;

        long scaled = (long)vanillaCount * (long)(100 + bonusPercent);
        long result = scaled / 100L;

        if (s_keepAtLeastOneWhenVanillaCountPositive && result < 1)
            result = 1;

        if (result > 10000)
            result = 10000;

        return (int)result;
    }

    private static int ClampPercent(int percent)
    {
        if (percent < -90)
            return -90;

        if (percent > 500)
            return 500;

        return percent;
    }
}
