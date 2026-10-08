using System.Text;

#nullable disable

/// <summary>
/// Lightweight counters for manually installed attack-harvest patch.
/// No string construction is performed in the patch.
/// </summary>
public static class RebirthAttackHarvestCounters
{
    private static int s_prefixCalls;
    private static int s_vanillaPassThrough;
    private static int s_policyDisabledPassThrough;
    private static int s_observeOnlyPassThrough;
    private static int s_replacementModePassThrough;
    private static int s_replacementSuccess;
    private static int s_replacementFallback;
    private static int s_destroyDropEvents;
    private static int s_harvestDropEvents;
    private static int s_blockHarvestEvents;
    private static int s_otherHarvestEvents;
    private static int s_bonusAdjustedStacks;
    private static int s_bonusAddedItems;

    public static int PrefixCalls { get { return s_prefixCalls; } }
    public static int VanillaPassThrough { get { return s_vanillaPassThrough; } }
    public static int PolicyDisabledPassThrough { get { return s_policyDisabledPassThrough; } }
    public static int ObserveOnlyPassThrough { get { return s_observeOnlyPassThrough; } }
    public static int ReplacementModePassThrough { get { return s_replacementModePassThrough; } }
    public static int ReplacementSuccess { get { return s_replacementSuccess; } }
    public static int ReplacementFallback { get { return s_replacementFallback; } }

    public static void Reset()
    {
        s_prefixCalls = 0;
        s_vanillaPassThrough = 0;
        s_policyDisabledPassThrough = 0;
        s_observeOnlyPassThrough = 0;
        s_replacementModePassThrough = 0;
        s_replacementSuccess = 0;
        s_replacementFallback = 0;
        s_destroyDropEvents = 0;
        s_harvestDropEvents = 0;
        s_blockHarvestEvents = 0;
        s_otherHarvestEvents = 0;
        s_bonusAdjustedStacks = 0;
        s_bonusAddedItems = 0;
    }

    public static void RecordPolicyDisabled()
    {
        s_prefixCalls++;
        s_policyDisabledPassThrough++;
        s_vanillaPassThrough++;
    }

    public static void RecordObserveOnly()
    {
        s_prefixCalls++;
        s_observeOnlyPassThrough++;
        s_vanillaPassThrough++;
    }

    public static void RecordReplacementModePassthrough()
    {
        s_prefixCalls++;
        s_replacementModePassThrough++;
        s_vanillaPassThrough++;
    }

    public static void RecordBonusAdjustment(int vanillaCount, int adjustedCount)
    {
        if (adjustedCount == vanillaCount)
            return;

        s_bonusAdjustedStacks++;
        s_bonusAddedItems += adjustedCount - vanillaCount;
    }

    public static void RecordReplacementSuccess(int destroyDropEvents, int harvestDropEvents, bool blockHit)
    {
        s_prefixCalls++;
        s_replacementSuccess++;
        s_destroyDropEvents += destroyDropEvents;
        s_harvestDropEvents += harvestDropEvents;
        if (blockHit)
            s_blockHarvestEvents++;
        else
            s_otherHarvestEvents++;
    }

    public static void RecordReplacementFallback()
    {
        s_prefixCalls++;
        s_replacementFallback++;
        s_vanillaPassThrough++;
    }

    public static string GetSummaryReport()
    {
        return "[RebirthAttackHarvestCounters] prefixCalls: " + s_prefixCalls
            + "; vanillaPassThrough: " + s_vanillaPassThrough
            + "; policyDisabledPassThrough: " + s_policyDisabledPassThrough
            + "; observeOnlyPassThrough: " + s_observeOnlyPassThrough
            + "; replacementModePassThrough: " + s_replacementModePassThrough
            + "; replacementSuccess: " + s_replacementSuccess
            + "; replacementFallback: " + s_replacementFallback
            + "; destroyDropEvents: " + s_destroyDropEvents
            + "; harvestDropEvents: " + s_harvestDropEvents
            + "; blockHarvestEvents: " + s_blockHarvestEvents
            + "; otherHarvestEvents: " + s_otherHarvestEvents
            + "; bonusAdjustedStacks: " + s_bonusAdjustedStacks
            + "; bonusAddedItems: " + s_bonusAddedItems;
    }

    public static string GetDetailReport()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine(GetSummaryReport());
        sb.AppendLine("  Counters only increment when the manual attack-harvest patch is installed.");
        sb.AppendLine("  Observe-only preserves vanilla by returning true.");
        sb.AppendLine("  Replacement success skips vanilla after running vanilla-equivalent replacement service.");
        sb.AppendLine("  Replacement fallback returns true to vanilla.");
        return sb.ToString();
    }
}
