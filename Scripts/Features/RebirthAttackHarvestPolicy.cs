using System.Text;

#nullable disable

/// <summary>
/// Complete policy surface for the attack-harvest category.
/// Defaults disabled and vanilla-preserving.
/// </summary>
public static class RebirthAttackHarvestPolicy
{
    private static bool s_enabled;
    private static bool s_observeOnly = true;
    private static bool s_allowReplacementBehavior;
    private static bool s_replacementVanillaEquivalentOnly = true;

    public static bool Enabled { get { return s_enabled; } }
    public static bool ObserveOnly { get { return s_observeOnly; } }
    public static bool AllowReplacementBehavior { get { return s_allowReplacementBehavior; } }
    public static bool ReplacementVanillaEquivalentOnly { get { return s_replacementVanillaEquivalentOnly; } }

    public static void EnableObserveOnly()
    {
        s_enabled = true;
        s_observeOnly = true;
        s_allowReplacementBehavior = false;
        s_replacementVanillaEquivalentOnly = true;
    }

    public static void EnableReplacementMode(bool vanillaEquivalentOnly)
    {
        s_enabled = true;
        s_observeOnly = false;
        s_allowReplacementBehavior = true;
        s_replacementVanillaEquivalentOnly = vanillaEquivalentOnly;
    }

    public static void Disable()
    {
        s_enabled = false;
        s_observeOnly = true;
        s_allowReplacementBehavior = false;
        s_replacementVanillaEquivalentOnly = true;
    }

    public static bool ShouldPatchDoWork()
    {
        return s_enabled;
    }

    public static string GetSummaryReport()
    {
        return "[RebirthAttackHarvestPolicy] enabled: " + s_enabled
            + "; observeOnly: " + s_observeOnly
            + "; allowReplacementBehavior: " + s_allowReplacementBehavior
            + "; replacementVanillaEquivalentOnly: " + s_replacementVanillaEquivalentOnly;
    }

    public static string GetDetailReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine(GetSummaryReport());
        sb.AppendLine("  Category: attack harvest / GameUtils.HarvestOnAttack.");
        sb.AppendLine("  Observe-only mode returns true and preserves vanilla.");
        sb.AppendLine("  Replacement mode calls RebirthAttackHarvestReplacementService and skips vanilla only when replacement succeeds.");
        sb.AppendLine("  Current replacement is vanilla-equivalent by design.");
        return sb.ToString();
    }
}
