using System;
using System.Text;

#nullable disable

public enum RebirthHarvestSalvageAdapterDecision
{
    Unknown,
    NoOp,
    BlockedByNoGoGate,
    WouldApplyFutureBehavior
}

public sealed class RebirthHarvestSalvageAdapterResult
{
    public RebirthHarvestSalvageAdapterDecision Decision;
    public int OriginalAmount;
    public int FinalAmount;
    public string Reason;
}

/// <summary>
/// Disabled/no-op harvest/salvage implementation adapter stub.
/// 
/// This is not wired to Harmony or gameplay.
/// It exists only to define the future call shape safely.
/// </summary>
public static class RebirthHarvestSalvageImplementationAdapter
{
    public static RebirthHarvestSalvageAdapterResult PreviewHarvestAmount(int originalAmount)
    {
        return BuildNoOpResult(originalAmount, "harvest adapter stub is not connected; policy is disabled");
    }

    public static RebirthHarvestSalvageAdapterResult PreviewSalvageAmount(int originalAmount)
    {
        return BuildNoOpResult(originalAmount, "salvage adapter stub is not connected; policy is disabled");
    }

    public static bool IsRuntimeUseAllowed()
    {
        RebirthHarvestSalvagePolicySnapshot policy = RebirthHarvestSalvageRuntimePolicy.Snapshot;

        if (!policy.RuntimeConsumerAllowed)
            return false;

        if (!policy.HotPathUseAllowed)
            return false;

        if (RebirthFirstSliceNoGoGate.CurrentDecision != RebirthFirstSliceGateDecision.Go)
            return false;

        return true;
    }

    public static string GetSummaryReport()
    {
        RebirthHarvestSalvagePolicySnapshot policy = RebirthHarvestSalvageRuntimePolicy.Snapshot;

        return "[RebirthHarvestSalvageAdapter] connectedToGameplay: false"
            + "; runtimeUseAllowed: " + IsRuntimeUseAllowed()
            + "; noGoDecision: " + RebirthFirstSliceNoGoGate.CurrentDecision
            + "; harvestEnabled: " + policy.HarvestFeatureEnabled
            + "; salvageEnabled: " + policy.SalvageFeatureEnabled
            + "; runtimeConsumerAllowed: " + policy.RuntimeConsumerAllowed
            + "; hotPathUseAllowed: " + policy.HotPathUseAllowed
            + ". Adapter is no-op.";
    }

    public static string GetPreviewReport(int amount)
    {
        RebirthHarvestSalvageAdapterResult harvest = PreviewHarvestAmount(amount);
        RebirthHarvestSalvageAdapterResult salvage = PreviewSalvageAmount(amount);

        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvageAdapter] preview report.");
        sb.AppendLine("  Not connected to gameplay. No Harmony patches installed.");
        sb.AppendLine();
        AppendResult(sb, "harvest", harvest);
        AppendResult(sb, "salvage", salvage);
        return sb.ToString();
    }

    public static string GetFutureUseReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvageAdapter] future allowed use:");
        sb.AppendLine("  1. Exact source evidence must be filled.");
        sb.AppendLine("  2. First slice no-go gate must become GO.");
        sb.AppendLine("  3. Runtime policy must explicitly allow consumer and hot-path use.");
        sb.AppendLine("  4. Hot-path feature flag must remain direct bool, not string/dictionary lookup.");
        sb.AppendLine("  5. Harmony patch must call a tiny adapter method after a direct guard.");
        sb.AppendLine("  6. Adapter must not log or build diagnostics unless gated before construction.");
        sb.AppendLine();
        sb.AppendLine("Current phase intentionally keeps all of that disabled.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvageAdapter] safety:");
        sb.AppendLine("  1. Adapter is not connected to gameplay.");
        sb.AppendLine("  2. No Harmony patches installed.");
        sb.AppendLine("  3. No XML parsing.");
        sb.AppendLine("  4. No custom game option lookup.");
        sb.AppendLine("  5. No network packets.");
        sb.AppendLine("  6. No save/load writes.");
        sb.AppendLine("  7. Preview methods return original amounts unchanged.");
        sb.AppendLine("  8. Runtime use is false while no-go gate is NoGo.");
        return sb.ToString();
    }

    private static RebirthHarvestSalvageAdapterResult BuildNoOpResult(int originalAmount, string reason)
    {
        return new RebirthHarvestSalvageAdapterResult
        {
            Decision = RebirthHarvestSalvageAdapterDecision.NoOp,
            OriginalAmount = originalAmount,
            FinalAmount = originalAmount,
            Reason = reason
        };
    }

    private static void AppendResult(StringBuilder sb, string name, RebirthHarvestSalvageAdapterResult result)
    {
        sb.Append("  ").Append(name).Append(": ")
          .Append("decision=").Append(result.Decision)
          .Append("; original=").Append(result.OriginalAmount)
          .Append("; final=").Append(result.FinalAmount)
          .Append("; reason=").AppendLine(result.Reason);
    }
}
