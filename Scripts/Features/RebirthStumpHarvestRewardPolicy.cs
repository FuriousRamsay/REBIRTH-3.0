using System.Text;

#nullable disable

/// <summary>
/// Manual policy for consuming stored stump/vehicle harvest context and granting a controlled reward.
/// Defaults disabled. No automatic reward path is connected unless explicitly enabled.
/// </summary>
public static class RebirthStumpHarvestRewardPolicy
{
    private static bool s_enabled;
    private static bool s_consumeContextOnReward = true;
    private static bool s_requireVehicleAttachedContext = true;
    private static bool s_requireContext = true;
    private static string s_rewardItemName = "resourceHoney";
    private static int s_rewardCount = 1;

    public static bool Enabled { get { return s_enabled; } }
    public static bool ConsumeContextOnReward { get { return s_consumeContextOnReward; } }
    public static bool RequireVehicleAttachedContext { get { return s_requireVehicleAttachedContext; } }
    public static bool RequireContext { get { return s_requireContext; } }
    public static string RewardItemName { get { return s_rewardItemName; } }
    public static int RewardCount { get { return s_rewardCount; } }

    public static void EnableManualTest(
        string rewardItemName,
        int rewardCount,
        bool consumeContextOnReward,
        bool requireVehicleAttachedContext,
        bool requireContext)
    {
        s_enabled = true;
        s_rewardItemName = string.IsNullOrEmpty(rewardItemName) ? "resourceHoney" : rewardItemName;
        s_rewardCount = ClampCount(rewardCount);
        s_consumeContextOnReward = consumeContextOnReward;
        s_requireVehicleAttachedContext = requireVehicleAttachedContext;
        s_requireContext = requireContext;
    }

    public static void EnablePreviewOnly(bool consumeContextOnPreview, bool requireVehicleAttachedContext)
    {
        s_enabled = true;
        s_consumeContextOnReward = consumeContextOnPreview;
        s_requireVehicleAttachedContext = requireVehicleAttachedContext;
        s_requireContext = true;
        s_rewardItemName = "resourceHoney";
        s_rewardCount = 0;
    }

    public static void Disable()
    {
        s_enabled = false;
        s_consumeContextOnReward = true;
        s_requireVehicleAttachedContext = true;
        s_requireContext = true;
        s_rewardItemName = "resourceHoney";
        s_rewardCount = 1;
    }

    public static bool CanConsume()
    {
        return s_enabled;
    }

    public static string GetSummaryReport()
    {
        return "[RebirthStumpHarvestRewardPolicy] enabled: " + s_enabled
            + "; rewardItemName: " + s_rewardItemName
            + "; rewardCount: " + s_rewardCount
            + "; consumeContextOnReward: " + s_consumeContextOnReward
            + "; requireVehicleAttachedContext: " + s_requireVehicleAttachedContext
            + "; requireContext: " + s_requireContext;
    }

    public static string GetDetailReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine(GetSummaryReport());
        sb.AppendLine("  Purpose: gated stump/vehicle context reward path.");
        sb.AppendLine("  Defaults disabled.");
        sb.AppendLine("  This policy is command/manual only.");
        sb.AppendLine("  A future automatic reward hook must reuse this policy and context guards.");
        return sb.ToString();
    }

    private static int ClampCount(int count)
    {
        if (count < 0)
            return 0;

        if (count > 100)
            return 100;

        return count;
    }
}
