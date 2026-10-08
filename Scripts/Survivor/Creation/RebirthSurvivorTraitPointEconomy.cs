using System;

#nullable disable

/// <summary>
/// Shared Survivor-creation Trait-point accounting.
/// maxNegativeTraitRefund &lt; 0 means unlimited: every selected negative Trait refunds
/// its authored point value. Keeping this logic in one place prevents the Creator UI,
/// validator and debug diagnostics from drifting apart.
/// </summary>
public static class RebirthSurvivorTraitPointEconomy
{
    public static int ApplyNegativeRefund(int maxNegativeTraitRefund, int requested)
    {
        requested = Math.Max(0, requested);
        return maxNegativeTraitRefund < 0 ? requested : Math.Min(maxNegativeTraitRefund, requested);
    }

    public static bool HasRefundCap(int maxNegativeTraitRefund)
    {
        return maxNegativeTraitRefund >= 0;
    }
}
