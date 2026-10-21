// Keep an equipped choice between thresholds; avoid repeated swaps around one distance.
internal static class RebirthHumanWeaponRules
{
    internal static int Choose(float distance, int held, int melee, int ranged)
    {
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f) return held;
        if (melee < 0) return ranged;
        if (ranged < 0) return melee;
        if (distance <= 3f) return melee;
        if (distance >= 6f) return ranged;
        return held == melee || held == ranged ? held : melee;
    }
}
