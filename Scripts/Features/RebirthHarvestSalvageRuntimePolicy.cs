using System;
using System.Text;

#nullable disable

public enum RebirthHarvestSalvagePolicySource
{
    Unknown,
    ExplicitDefaults,
    FutureGameOption,
    FutureServerSnapshot,
    FutureBuildProfile,
    ManualReviewRequired
}

public sealed class RebirthHarvestSalvagePolicySnapshot
{
    public bool HarvestFeatureEnabled;
    public bool SalvageFeatureEnabled;
    public bool VehicleHarvestRewardsAllowed;
    public bool StealthSoundChangesEnabled;
    public bool ServerAuthoritativeRewards;
    public bool RuntimeConsumerAllowed;
    public bool HotPathUseAllowed;
    public int HarvestBonusPercent;
    public int SalvageBonusPercent;
    public int Version;
    public RebirthHarvestSalvagePolicySource Source;
    public string Reason;
}

/// <summary>
/// Runtime harvest/salvage policy shell.
/// This is intentionally inert:
/// - explicit defaults only
/// - no Harmony patch
/// - no XML parsing
/// - no custom game option lookup
/// - no gameplay consumer
/// - no network packets
/// - no save/load writes
/// </summary>
public static class RebirthHarvestSalvageRuntimePolicy
{
    private static readonly object s_lock = new object();

    private static RebirthHarvestSalvagePolicySnapshot s_snapshot = CreateDefaultSnapshot(0, "static initialization");

    public static RebirthHarvestSalvagePolicySnapshot Snapshot
    {
        get
        {
            lock (s_lock)
            {
                return Clone(s_snapshot);
            }
        }
    }

    public static void ResetToExplicitDefaults(string reason)
    {
        lock (s_lock)
        {
            int nextVersion = s_snapshot == null ? 1 : s_snapshot.Version + 1;
            s_snapshot = CreateDefaultSnapshot(nextVersion, reason);
        }
    }

    public static string GetSummaryReport()
    {
        RebirthHarvestSalvagePolicySnapshot s = Snapshot;

        return "[RebirthHarvestSalvagePolicy] Version: " + s.Version
            + "; source: " + s.Source
            + "; harvestEnabled: " + s.HarvestFeatureEnabled
            + "; salvageEnabled: " + s.SalvageFeatureEnabled
            + "; vehicleRewardsAllowed: " + s.VehicleHarvestRewardsAllowed
            + "; stealthSoundChanges: " + s.StealthSoundChangesEnabled
            + "; serverAuthoritativeRewards: " + s.ServerAuthoritativeRewards
            + "; runtimeConsumerAllowed: " + s.RuntimeConsumerAllowed
            + "; hotPathUseAllowed: " + s.HotPathUseAllowed
            + "; harvestBonusPercent: " + s.HarvestBonusPercent
            + "; salvageBonusPercent: " + s.SalvageBonusPercent
            + "; reason: " + s.Reason;
    }

    public static string GetDetailReport()
    {
        RebirthHarvestSalvagePolicySnapshot s = Snapshot;

        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvagePolicy] runtime policy shell.");
        sb.AppendLine("  This is inert and not connected to gameplay.");
        sb.AppendLine();
        sb.Append("  Version: ").AppendLine(s.Version.ToString());
        sb.Append("  Source: ").AppendLine(s.Source.ToString());
        sb.Append("  HarvestFeatureEnabled: ").AppendLine(s.HarvestFeatureEnabled.ToString());
        sb.Append("  SalvageFeatureEnabled: ").AppendLine(s.SalvageFeatureEnabled.ToString());
        sb.Append("  VehicleHarvestRewardsAllowed: ").AppendLine(s.VehicleHarvestRewardsAllowed.ToString());
        sb.Append("  StealthSoundChangesEnabled: ").AppendLine(s.StealthSoundChangesEnabled.ToString());
        sb.Append("  ServerAuthoritativeRewards: ").AppendLine(s.ServerAuthoritativeRewards.ToString());
        sb.Append("  RuntimeConsumerAllowed: ").AppendLine(s.RuntimeConsumerAllowed.ToString());
        sb.Append("  HotPathUseAllowed: ").AppendLine(s.HotPathUseAllowed.ToString());
        sb.Append("  HarvestBonusPercent: ").AppendLine(s.HarvestBonusPercent.ToString());
        sb.Append("  SalvageBonusPercent: ").AppendLine(s.SalvageBonusPercent.ToString());
        sb.Append("  Reason: ").AppendLine(s.Reason);
        sb.AppendLine();
        sb.AppendLine("Default intent:");
        sb.AppendLine("  - no harvest reward modification");
        sb.AppendLine("  - no salvage reward modification");
        sb.AppendLine("  - no vehicle-triggered harvest rewards");
        sb.AppendLine("  - no stealth sound change");
        sb.AppendLine("  - future rewards must be server-authoritative");
        sb.AppendLine("  - no hot-path consumer may read this until exact patch/test is chosen");
        return sb.ToString();
    }

    public static string GetFuturePatchRequirementsReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvagePolicy] future patch requirements:");
        sb.AppendLine("  1. Choose one exact behavior first; do not combine harvest, salvage, sound, vehicle, XP, and UI.");
        sb.AppendLine("  2. Identify exact vanilla method target before patching.");
        sb.AppendLine("  3. Document whether target is hot/warm/cold.");
        sb.AppendLine("  4. Use typed Harmony parameters only; no object[] __args.");
        sb.AppendLine("  5. Check direct bool feature flag before any expensive work.");
        sb.AppendLine("  6. Build no debug strings unless RebirthDiagnosticGates gate is true.");
        sb.AppendLine("  7. Keep reward truth server-authoritative.");
        sb.AppendLine("  8. Run Phase 3H tests before enabling feature flag.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarvestSalvagePolicy] safety:");
        sb.AppendLine("  1. Explicit defaults only.");
        sb.AppendLine("  2. Harvest feature disabled.");
        sb.AppendLine("  3. Salvage feature disabled.");
        sb.AppendLine("  4. Runtime consumer disabled.");
        sb.AppendLine("  5. Hot-path use disabled.");
        sb.AppendLine("  6. No Harmony patches installed.");
        sb.AppendLine("  7. No XML parsing.");
        sb.AppendLine("  8. No custom game option lookup.");
        sb.AppendLine("  9. No gameplay behavior connected.");
        sb.AppendLine("  10. No network packets.");
        sb.AppendLine("  11. No save/load writes.");
        return sb.ToString();
    }

    private static RebirthHarvestSalvagePolicySnapshot CreateDefaultSnapshot(int version, string reason)
    {
        return new RebirthHarvestSalvagePolicySnapshot
        {
            HarvestFeatureEnabled = false,
            SalvageFeatureEnabled = false,
            VehicleHarvestRewardsAllowed = false,
            StealthSoundChangesEnabled = false,
            ServerAuthoritativeRewards = true,
            RuntimeConsumerAllowed = false,
            HotPathUseAllowed = false,
            HarvestBonusPercent = 0,
            SalvageBonusPercent = 0,
            Version = version,
            Source = RebirthHarvestSalvagePolicySource.ExplicitDefaults,
            Reason = string.IsNullOrEmpty(reason) ? "explicit defaults" : reason
        };
    }

    private static RebirthHarvestSalvagePolicySnapshot Clone(RebirthHarvestSalvagePolicySnapshot s)
    {
        if (s == null)
            return CreateDefaultSnapshot(0, "null source fallback");

        return new RebirthHarvestSalvagePolicySnapshot
        {
            HarvestFeatureEnabled = s.HarvestFeatureEnabled,
            SalvageFeatureEnabled = s.SalvageFeatureEnabled,
            VehicleHarvestRewardsAllowed = s.VehicleHarvestRewardsAllowed,
            StealthSoundChangesEnabled = s.StealthSoundChangesEnabled,
            ServerAuthoritativeRewards = s.ServerAuthoritativeRewards,
            RuntimeConsumerAllowed = s.RuntimeConsumerAllowed,
            HotPathUseAllowed = s.HotPathUseAllowed,
            HarvestBonusPercent = s.HarvestBonusPercent,
            SalvageBonusPercent = s.SalvageBonusPercent,
            Version = s.Version,
            Source = s.Source,
            Reason = s.Reason
        };
    }
}
