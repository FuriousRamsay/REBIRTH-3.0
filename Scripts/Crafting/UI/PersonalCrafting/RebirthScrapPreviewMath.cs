using System;

/// <summary>Read-only projection of V3.2 ItemActionEntryScrap.OnActivated's output calculation.</summary>
public static class RebirthScrapPreviewMath
{
    public static int Output(int sourceWeight, int stackCount, int outputWeight, float recovery, float modifier)
    {
        if (sourceWeight <= 0 || stackCount <= 0 || outputWeight <= 0 || modifier <= 0) return 0;
        int units = sourceWeight * stackCount / outputWeight;
        if (units <= 0) return 0;
        return Math.Max(1, (int)((double)units * ((double)recovery * modifier)));
    }
}
