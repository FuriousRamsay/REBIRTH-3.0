using UnityEngine;

// Inventory-independent serving decisions shared by current player consumption
// and the companion-source transaction integration. No source/state is mutated.
internal readonly struct RebirthDrinkServing
{
    internal readonly float ConsumedMl;
    internal readonly bool StomachLimited;
    internal bool Accepted { get { return ConsumedMl > 0.5f; } }

    internal RebirthDrinkServing(float consumedMl, bool stomachLimited)
    {
        ConsumedMl = consumedMl;
        StomachLimited = stomachLimited;
    }
}

internal readonly struct RebirthFoodServing
{
    internal readonly bool Accepted;
    internal readonly bool OverfillsStomach;

    internal RebirthFoodServing(bool accepted, bool overfillsStomach)
    {
        Accepted = accepted;
        OverfillsStomach = overfillsStomach;
    }
}

internal static class RebirthConsumptionServing
{
    internal static RebirthDrinkServing Drink(float sipTarget, float remaining,
        float freeCapacity, float usefulMl)
    {
        if (freeCapacity <= 0.5f) return new RebirthDrinkServing(0f, false);
        float consumed = Mathf.Min(sipTarget, remaining, freeCapacity, usefulMl);
        if (consumed <= 0.5f) return new RebirthDrinkServing(0f, false);
        return new RebirthDrinkServing(consumed,
            freeCapacity + 0.5f < Mathf.Min(sipTarget, remaining, usefulMl));
    }

    internal static RebirthFoodServing Food(float solidMl, float foodWaterMl,
        float freeCapacity, float minimumFitFraction)
    {
        float occupied = Mathf.Max(0f, solidMl) + Mathf.Max(0f, foodWaterMl);
        float minimumFit = occupied * Mathf.Clamp01(minimumFitFraction);
        bool accepted = !(occupied > 0.5f && freeCapacity + 0.5f < minimumFit);
        return new RebirthFoodServing(accepted, accepted && occupied > freeCapacity + 0.5f);
    }
}
