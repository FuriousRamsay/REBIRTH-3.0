using System;
using System.Text;

#nullable disable

public enum RebirthHarvestSalvageAuditCandidateStatus
{
    Unknown,
    SelectedFutureSlice,
    RejectedForFirstSlice,
    Blocked,
    ManualReview
}

public enum RebirthHarvestSalvageAuditRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}

public sealed class RebirthHarvestSalvageAuditCandidate
{
    public string CandidateId;
    public RebirthHarvestSalvageAuditCandidateStatus Status;
    public RebirthHarvestSalvageAuditRisk Risk;
    public string BaseSource;
    public string OldSource;
    public string Target;
    public string Finding;
    public string NextStep;
}

/// <summary>
/// Milestone 4B actual source audit registry.
/// Evidence was taken from the active sandbox source packages:
/// _2.6 exp b10.tar(96).gz, zzz_REBIRTH__Utils v2.6.zip, and ConfigsDump(50).zip.
/// This registry is read-only and does not install patches.
/// </summary>
public static class RebirthHarvestSalvageActualSourceAuditRegistry
{
    private static readonly RebirthHarvestSalvageAuditCandidate[] s_candidates = new[]
    {
        new RebirthHarvestSalvageAuditCandidate
        {
            CandidateId = "crop.activation.harvest-count-bonus",
            Status = RebirthHarvestSalvageAuditCandidateStatus.SelectedFutureSlice,
            Risk = RebirthHarvestSalvageAuditRisk.High,
            BaseSource = "BlockCropsGrown.cs lines 57-115",
            OldSource = "Harmony/Harmony_CropsGrown.cs lines 16-283",
            Target = "BlockCropsGrown.OnBlockActivated(WorldBase, int, Vector3i, BlockValue, EntityPlayerLocal)",
            Finding = "Exact base method and old REBIRTH Prefix behavior found. Prefix replacement is likely required because vanilla awards the ItemStack internally.",
            NextStep = "Milestone 4C may add a disabled-by-default patch bundle after accepting this target."
        },
        new RebirthHarvestSalvageAuditCandidate
        {
            CandidateId = "attack.harvest.collectHarvestedItem",
            Status = RebirthHarvestSalvageAuditCandidateStatus.RejectedForFirstSlice,
            Risk = RebirthHarvestSalvageAuditRisk.Critical,
            BaseSource = "GameUtils.cs lines 721-873",
            OldSource = "Not selected for first slice",
            Target = "GameUtils.HarvestOnAttack / collectHarvestedItem",
            Finding = "Too broad: handles attack harvesting, inventory, quest harvested event, drops, and XP.",
            NextStep = "Defer to separate attack-harvest milestone."
        },
        new RebirthHarvestSalvageAuditCandidate
        {
            CandidateId = "stump.vehicle.harvest-context",
            Status = RebirthHarvestSalvageAuditCandidateStatus.RejectedForFirstSlice,
            Risk = RebirthHarvestSalvageAuditRisk.Critical,
            BaseSource = "Block.cs OnBlockDestroyedBy / OnBlockDamaged flow",
            OldSource = "Harmony/Harmony_Block_HarvestContext.cs lines 7-76",
            Target = "Block.OnBlockDestroyedBy",
            Finding = "Broad base block destruction hook used by old REBIRTH for stump vehicle context.",
            NextStep = "Defer to separate stump/vehicle harvest milestone."
        },
        new RebirthHarvestSalvageAuditCandidate
        {
            CandidateId = "vehicle.salvage.storage-protection",
            Status = RebirthHarvestSalvageAuditCandidateStatus.RejectedForFirstSlice,
            Risk = RebirthHarvestSalvageAuditRisk.High,
            BaseSource = "Base vehicle/block damage flow not selected",
            OldSource = "Dayuppy/Scripts/Blocks/BlockVehicleRebirth.cs lines 40-67",
            Target = "BlockVehicleRebirth.OnBlockDamaged",
            Finding = "Custom vehicle block override prevents salvage damage to player-storage repairable vehicles.",
            NextStep = "Defer to separate vehicle/salvage milestone."
        }
    };

    public static string GetSummaryReport()
    {
        int selected = 0;
        int rejected = 0;

        for (int i = 0; i < s_candidates.Length; i++)
        {
            if (s_candidates[i].Status == RebirthHarvestSalvageAuditCandidateStatus.SelectedFutureSlice)
                selected++;

            if (s_candidates[i].Status == RebirthHarvestSalvageAuditCandidateStatus.RejectedForFirstSlice)
                rejected++;
        }

        return "[RebirthHarvestSalvageActualAudit] Candidates: " + s_candidates.Length
            + "; selected future slice: " + selected
            + "; rejected for first slice: " + rejected
            + ". No patch installed.";
    }

    public static string GetCandidateReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16000);
        sb.AppendLine("[RebirthHarvestSalvageActualAudit] Milestone 4B candidate report.");
        sb.AppendLine("id | status | risk | base source | old source | target | finding | next step");

        bool any = false;
        for (int i = 0; i < s_candidates.Length; i++)
        {
            RebirthHarvestSalvageAuditCandidate c = s_candidates[i];
            if (!Matches(c, f))
                continue;

            any = true;
            sb.Append(c.CandidateId).Append(" | ")
              .Append(c.Status).Append(" | ")
              .Append(c.Risk).Append(" | ")
              .Append(c.BaseSource).Append(" | ")
              .Append(c.OldSource).Append(" | ")
              .Append(c.Target).Append(" | ")
              .Append(c.Finding).Append(" | ")
              .AppendLine(c.NextStep);
        }

        if (!any)
            sb.AppendLine("No Milestone 4B candidate matched the filter.");

        return sb.ToString();
    }

    public static string GetSelectedReport()
    {
        return GetCandidateReport("SelectedFutureSlice");
    }

    public static string GetRejectedReport()
    {
        return GetCandidateReport("RejectedForFirstSlice");
    }

    public static string GetNextReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvageActualAudit] next milestone:");
        sb.AppendLine("  Milestone 4C — Disabled Crop Activation Harvest Count Patch Bundle");
        sb.AppendLine();
        sb.AppendLine("Required constraints:");
        sb.AppendLine("  - disabled by default");
        sb.AppendLine("  - direct guard at top of Prefix");
        sb.AppendLine("  - typed Harmony parameters only");
        sb.AppendLine("  - no object[] __args");
        sb.AppendLine("  - no debug construction before gate");
        sb.AppendLine("  - no XML/custom option lookup in patch");
        sb.AppendLine("  - SP and dedicated-server client tests");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvageActualAudit] safety:");
        sb.AppendLine("  1. Actual source files were inspected outside this registry.");
        sb.AppendLine("  2. Registry is read-only.");
        sb.AppendLine("  3. No Harmony patch is installed.");
        sb.AppendLine("  4. No gameplay behavior is migrated.");
        sb.AppendLine("  5. No XML parsing occurs at runtime.");
        sb.AppendLine("  6. Selected target is for a future disabled patch, not active behavior.");
        return sb.ToString();
    }

    private static bool Matches(RebirthHarvestSalvageAuditCandidate c, string filter)
    {
        if (c == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (c.CandidateId != null && c.CandidateId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.BaseSource != null && c.BaseSource.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.OldSource != null && c.OldSource.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.Target != null && c.Target.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.Finding != null && c.Finding.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.Status.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (c.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
