using System.Text;

#nullable disable

public sealed class RebirthHarvestSystemsSnapshot
{
    public bool CropPatchInstalled;
    public bool AttackHarvestPatchInstalled;
    public bool StumpHarvestContextPatchInstalled;
    public bool AnyPatchInstalled;
    public bool CropManualPolicyEnabled;
}

/// <summary>
/// Shared status/policy surface for the first bundled harvest/salvage implementation milestone.
/// Defaults do not install or enable anything.
/// </summary>
public static class RebirthHarvestSystemsPolicy
{
    public static RebirthHarvestSystemsSnapshot Snapshot
    {
        get
        {
            return new RebirthHarvestSystemsSnapshot
            {
                CropPatchInstalled = RebirthHarvestSystemsManualPatchInstaller.IsCropPatchInstalled,
                AttackHarvestPatchInstalled = RebirthHarvestSystemsManualPatchInstaller.IsAttackHarvestPatchInstalled,
                StumpHarvestContextPatchInstalled = RebirthHarvestSystemsManualPatchInstaller.IsStumpHarvestContextPatchInstalled,
                AnyPatchInstalled = RebirthHarvestSystemsManualPatchInstaller.AnyInstalled,
                CropManualPolicyEnabled = RebirthCropActivationHarvestPolicy.Enabled
            };
        }
    }

    public static string GetSummaryReport()
    {
        RebirthHarvestSystemsSnapshot s = Snapshot;

        return "[RebirthHarvestSystemsPolicy] cropPatchInstalled: " + s.CropPatchInstalled
            + "; attackHarvestPatchInstalled: " + s.AttackHarvestPatchInstalled
            + "; stumpHarvestContextPatchInstalled: " + s.StumpHarvestContextPatchInstalled
            + "; anyPatchInstalled: " + s.AnyPatchInstalled
            + "; cropManualPolicyEnabled: " + s.CropManualPolicyEnabled
            + "; stumpContextEnabled: " + RebirthStumpHarvestContextPolicy.Enabled
            + "; stumpContextCount: " + RebirthStumpHarvestContextStore.Count
            + "; stumpRewardPreviewEnabled: " + RebirthStumpHarvestRewardPolicy.Enabled
            + "; stumpRewardItem: " + RebirthStumpHarvestRewardPolicy.RewardItemName
            + "; stumpRewardCount: " + RebirthStumpHarvestRewardPolicy.RewardCount
            + "; attackPolicyEnabled: " + RebirthAttackHarvestPolicy.Enabled
            + "; attackObserveOnly: " + RebirthAttackHarvestPolicy.ObserveOnly
            + "; attackReplacementAllowed: " + RebirthAttackHarvestPolicy.AllowReplacementBehavior
            + "; attackPrefixCalls: " + RebirthAttackHarvestCounters.PrefixCalls
            + "; attackBonusEnabled: " + RebirthAttackHarvestBonusPolicy.Enabled
            + "; attackDestroyBonusPercent: " + RebirthAttackHarvestBonusPolicy.DestroyBonusPercent
            + "; attackHarvestBonusPercent: " + RebirthAttackHarvestBonusPolicy.HarvestBonusPercent;
    }

    public static string GetDetailReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSystemsPolicy] detail:");
        sb.AppendLine("  Patches are not auto-installed by bootstrap.");
        sb.AppendLine("  Manual installer avoids broad PatchAll.");
        sb.AppendLine("  Crop patch has manual-test behavior behind RebirthCropActivationHarvestPolicy.");
        sb.AppendLine("  Attack harvest patch can observe or run replacement behind manual policy.");
        sb.AppendLine("  Attack harvest bonus policy modifies replacement counts only when explicitly enabled.");
        sb.AppendLine("  Stump harvest context patch stores gated context when explicitly enabled.");
        sb.AppendLine("  Stump reward service can preview or manually grant configured reward after context validation.");
        sb.AppendLine("  Use rbharvestsystemsfresh installcrop for crop only.");
        sb.AppendLine("  Use rbharvestsystemsfresh installall only for explicit manual testing.");
        sb.AppendLine("  Use rbharvestsystemsfresh uninstallall for rollback.");
        sb.AppendLine(GetSummaryReport());
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSystemsPolicy] safety:");
        sb.AppendLine("  1. No automatic PatchAll.");
        sb.AppendLine("  2. Patches install only through explicit command.");
        sb.AppendLine("  3. Uninstall command removes this Harmony id.");
        sb.AppendLine("  4. Attack harvest is no-op; stump context is gated/manual and preserves vanilla.");
        sb.AppendLine("  5. Crop gameplay still requires explicit manual policy enable.");
        sb.AppendLine("  6. No object[] __args.");
        sb.AppendLine("  7. No runtime XML lookup.");
        sb.AppendLine("  8. No debug construction inside patch guards.");
        return sb.ToString();
    }
}
