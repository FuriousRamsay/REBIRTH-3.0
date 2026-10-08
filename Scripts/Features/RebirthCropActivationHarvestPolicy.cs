using System;
using System.Text;

#nullable disable

public sealed class RebirthCropActivationHarvestPolicySnapshot
{
    public bool Enabled;
    public bool RuntimeConsumerAllowed;
    public bool HotPathUseAllowed;
    public bool ManualTestOverrideNoGoGate;
    public bool PlayerGrownDowngradeEnabled;
    public int WildBonusPercent;
    public int GrownBonusPercent;
    public bool PreserveVanillaWhenDisabled;
    public string Source;
}

/// <summary>
/// Runtime policy for the crop activation harvest count patch.
/// Defaults are disabled.
/// 
/// Milestone 4E adds player-grown downgrade behavior behind explicit manual-test policy.
/// </summary>
public static class RebirthCropActivationHarvestPolicy
{
    private static readonly object s_lock = new object();

    private static bool s_enabled;
    private static bool s_runtimeConsumerAllowed;
    private static bool s_hotPathUseAllowed;
    private static bool s_manualTestOverrideNoGoGate;
    private static bool s_playerGrownDowngradeEnabled;
    private static int s_wildBonusPercent;
    private static int s_grownBonusPercent;

    public static bool Enabled { get { return s_enabled; } }
    public static bool RuntimeConsumerAllowed { get { return s_runtimeConsumerAllowed; } }
    public static bool HotPathUseAllowed { get { return s_hotPathUseAllowed; } }
    public static bool ManualTestOverrideNoGoGate { get { return s_manualTestOverrideNoGoGate; } }
    public static bool PlayerGrownDowngradeEnabled { get { return s_playerGrownDowngradeEnabled; } }
    public static int WildBonusPercent { get { return s_wildBonusPercent; } }
    public static int GrownBonusPercent { get { return s_grownBonusPercent; } }

    public static RebirthCropActivationHarvestPolicySnapshot Snapshot
    {
        get
        {
            return new RebirthCropActivationHarvestPolicySnapshot
            {
                Enabled = s_enabled,
                RuntimeConsumerAllowed = s_runtimeConsumerAllowed,
                HotPathUseAllowed = s_hotPathUseAllowed,
                ManualTestOverrideNoGoGate = s_manualTestOverrideNoGoGate,
                PlayerGrownDowngradeEnabled = s_playerGrownDowngradeEnabled,
                WildBonusPercent = s_wildBonusPercent,
                GrownBonusPercent = s_grownBonusPercent,
                PreserveVanillaWhenDisabled = true,
                Source = "Milestone 4E manual-test policy"
            };
        }
    }

    public static bool CanUseInPatch()
    {
        if (!s_enabled)
            return false;

        if (!s_runtimeConsumerAllowed)
            return false;

        if (!s_hotPathUseAllowed)
            return false;

        if (!s_manualTestOverrideNoGoGate && RebirthFirstSliceNoGoGate.CurrentDecision != RebirthFirstSliceGateDecision.Go)
            return false;

        return true;
    }

    public static int GetBonusPercent(bool isGrownCrop)
    {
        return isGrownCrop ? s_grownBonusPercent : s_wildBonusPercent;
    }

    public static int PreviewAdjustedCount(int originalCount, bool isGrownCrop)
    {
        if (originalCount <= 0)
            return originalCount;

        if (!CanUseInPatch())
            return originalCount;

        return ApplyBonusPercent(originalCount, GetBonusPercent(isGrownCrop));
    }

    public static int ApplyBonusPercent(int originalCount, int percent)
    {
        if (originalCount <= 0)
            return originalCount;

        if (percent <= 0)
            return originalCount;

        long adjusted = originalCount + ((long)originalCount * percent / 100L);

        if (adjusted > int.MaxValue)
            return int.MaxValue;

        return (int)adjusted;
    }

    public static void EnableManualTest(int wildBonusPercent, int grownBonusPercent)
    {
        EnableManualTest(wildBonusPercent, grownBonusPercent, true);
    }

    public static void EnableManualTest(int wildBonusPercent, int grownBonusPercent, bool playerGrownDowngradeEnabled)
    {
        lock (s_lock)
        {
            s_enabled = true;
            s_runtimeConsumerAllowed = true;
            s_hotPathUseAllowed = true;
            s_manualTestOverrideNoGoGate = true;
            s_playerGrownDowngradeEnabled = playerGrownDowngradeEnabled;
            s_wildBonusPercent = ClampPercent(wildBonusPercent);
            s_grownBonusPercent = ClampPercent(grownBonusPercent);
        }
    }

    public static void Disable()
    {
        Reset();
    }

    public static void Reset()
    {
        lock (s_lock)
        {
            s_enabled = false;
            s_runtimeConsumerAllowed = false;
            s_hotPathUseAllowed = false;
            s_manualTestOverrideNoGoGate = false;
            s_playerGrownDowngradeEnabled = false;
            s_wildBonusPercent = 0;
            s_grownBonusPercent = 0;
        }
    }

    public static string GetSummaryReport()
    {
        RebirthCropActivationHarvestPolicySnapshot s = Snapshot;

        return "[RebirthCropActivationHarvestPolicy] enabled: " + s.Enabled
            + "; runtimeConsumerAllowed: " + s.RuntimeConsumerAllowed
            + "; hotPathUseAllowed: " + s.HotPathUseAllowed
            + "; manualTestOverrideNoGoGate: " + s.ManualTestOverrideNoGoGate
            + "; playerGrownDowngradeEnabled: " + s.PlayerGrownDowngradeEnabled
            + "; noGoDecision: " + RebirthFirstSliceNoGoGate.CurrentDecision
            + "; wildBonusPercent: " + s.WildBonusPercent
            + "; grownBonusPercent: " + s.GrownBonusPercent
            + "; preserveVanillaWhenDisabled: " + s.PreserveVanillaWhenDisabled;
    }

    public static string GetDetailReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthCropActivationHarvestPolicy] detail:");
        sb.AppendLine("  enabled: " + s_enabled);
        sb.AppendLine("  runtimeConsumerAllowed: " + s_runtimeConsumerAllowed);
        sb.AppendLine("  hotPathUseAllowed: " + s_hotPathUseAllowed);
        sb.AppendLine("  manualTestOverrideNoGoGate: " + s_manualTestOverrideNoGoGate);
        sb.AppendLine("  playerGrownDowngradeEnabled: " + s_playerGrownDowngradeEnabled);
        sb.AppendLine("  noGoDecision: " + RebirthFirstSliceNoGoGate.CurrentDecision);
        sb.AppendLine("  wildBonusPercent: " + s_wildBonusPercent);
        sb.AppendLine("  grownBonusPercent: " + s_grownBonusPercent);
        sb.AppendLine("  patch target: BlockCropsGrown.OnBlockActivated(WorldBase, int, Vector3i, BlockValue, EntityPlayerLocal)");
        sb.AppendLine("  disabled behavior: Prefix returns true to vanilla before doing work");
        sb.AppendLine("  enabled manual-test behavior: count bonus plus optional player-grown downgrade path");
        return sb.ToString();
    }

    public static string GetPreviewReport(int count)
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[RebirthCropActivationHarvestPolicy] preview:");
        sb.AppendLine("  original: " + count);
        sb.AppendLine("  wild result: " + PreviewAdjustedCount(count, false));
        sb.AppendLine("  grown result: " + PreviewAdjustedCount(count, true));
        sb.AppendLine("  canUseInPatch: " + CanUseInPatch());
        sb.AppendLine("  manualTestOverrideNoGoGate: " + s_manualTestOverrideNoGoGate);
        sb.AppendLine("  playerGrownDowngradeEnabled: " + s_playerGrownDowngradeEnabled);
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthCropActivationHarvestPolicy] safety:");
        sb.AppendLine("  1. Defaults are disabled.");
        sb.AppendLine("  2. Disabled patch returns true to vanilla immediately.");
        sb.AppendLine("  3. Manual test enable is explicit command-only state.");
        sb.AppendLine("  4. Manual test state is not saved.");
        sb.AppendLine("  5. No runtime XML lookup.");
        sb.AppendLine("  6. No custom game option lookup.");
        sb.AppendLine("  7. No object[] __args.");
        sb.AppendLine("  8. No diagnostics are built by the patch.");
        sb.AppendLine("  9. No network or save/load behavior.");
        return sb.ToString();
    }

    private static int ClampPercent(int value)
    {
        if (value < 0)
            return 0;

        if (value > 500)
            return 500;

        return value;
    }
}
