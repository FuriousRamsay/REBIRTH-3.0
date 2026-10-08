using System;
using System.Text;

#nullable disable

[Flags]
public enum RebirthRuntimeScenarioFlags
{
    None = 0,
    Common = 1 << 0,
    Purge = 1 << 1,
    Hive = 1 << 2,
    SurvivorScenario = 1 << 3,
    FutureCustom = 1 << 4,
    BlockedManualReview = 1 << 5
}

public enum RebirthRuntimeScenarioSource
{
    Unknown,
    ExplicitDefaults,
    ManualCommandPreview,
    FutureOptionsCache,
    FutureServerSnapshot,
    FutureXmlResolvedPolicy
}

public sealed class RebirthRuntimeScenarioPolicySnapshot
{
    public RebirthRuntimeScenarioFlags Flags;
    public RebirthRuntimeScenarioSource Source;
    public bool PurgeEnabled;
    public bool HiveEnabled;
    public bool SurvivorScenarioEnabled;
    public bool FutureCustomEnabled;
    public bool IsServerAuthoritative;
    public bool IsClientDisplayOnly;
    public bool BlocksSurvivorMigration;
    public int Version;
    public string Reason;
}

/// <summary>
/// First real runtime shell for scenario policy state.
/// This is intentionally inert:
/// - no XML parsing
/// - no option lookup
/// - no Harmony hook
/// - no gameplay consumer
/// - explicit defaults only
/// </summary>
public static class RebirthRuntimeScenarioPolicyCache
{
    private static readonly object s_lock = new object();

    private static RebirthRuntimeScenarioPolicySnapshot s_snapshot = CreateDefaultSnapshot(0, "static initialization");

    public static RebirthRuntimeScenarioPolicySnapshot Snapshot
    {
        get
        {
            lock (s_lock)
            {
                return CloneSnapshot(s_snapshot);
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
        RebirthRuntimeScenarioPolicySnapshot s = Snapshot;

        return "[RebirthRuntimeScenarioPolicy] Version: " + s.Version
            + "; source: " + s.Source
            + "; flags: " + s.Flags
            + "; purge: " + s.PurgeEnabled
            + "; hive: " + s.HiveEnabled
            + "; survivorScenario: " + s.SurvivorScenarioEnabled
            + "; futureCustom: " + s.FutureCustomEnabled
            + "; serverAuthoritative: " + s.IsServerAuthoritative
            + "; clientDisplayOnly: " + s.IsClientDisplayOnly
            + "; survivorBlocked: " + s.BlocksSurvivorMigration
            + "; reason: " + s.Reason;
    }

    public static string GetDetailReport()
    {
        RebirthRuntimeScenarioPolicySnapshot s = Snapshot;

        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthRuntimeScenarioPolicy] runtime policy cache shell.");
        sb.AppendLine("  This is an inert runtime shell using explicit defaults only.");
        sb.AppendLine("  It is not connected to gameplay, XML, Harmony, networking, or options.");
        sb.AppendLine();
        sb.AppendLine("Fields:");
        sb.Append("  Version: ").AppendLine(s.Version.ToString());
        sb.Append("  Source: ").AppendLine(s.Source.ToString());
        sb.Append("  Flags: ").AppendLine(s.Flags.ToString());
        sb.Append("  PurgeEnabled: ").AppendLine(s.PurgeEnabled.ToString());
        sb.Append("  HiveEnabled: ").AppendLine(s.HiveEnabled.ToString());
        sb.Append("  SurvivorScenarioEnabled: ").AppendLine(s.SurvivorScenarioEnabled.ToString());
        sb.Append("  FutureCustomEnabled: ").AppendLine(s.FutureCustomEnabled.ToString());
        sb.Append("  IsServerAuthoritative: ").AppendLine(s.IsServerAuthoritative.ToString());
        sb.Append("  IsClientDisplayOnly: ").AppendLine(s.IsClientDisplayOnly.ToString());
        sb.Append("  BlocksSurvivorMigration: ").AppendLine(s.BlocksSurvivorMigration.ToString());
        sb.Append("  Reason: ").AppendLine(s.Reason);
        sb.AppendLine();
        sb.AppendLine("Current default intent:");
        sb.AppendLine("  - common infrastructure allowed");
        sb.AppendLine("  - purge disabled");
        sb.AppendLine("  - hive disabled");
        sb.AppendLine("  - survivor scenario disabled and blocked");
        sb.AppendLine("  - future custom disabled");
        sb.AppendLine("  - no gameplay reads this cache yet");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthRuntimeScenarioPolicy] safety rules:");
        sb.AppendLine("  1. Explicit defaults only.");
        sb.AppendLine("  2. No XML parsing.");
        sb.AppendLine("  3. No custom game option lookup.");
        sb.AppendLine("  4. No Harmony patch install/uninstall.");
        sb.AppendLine("  5. No gameplay consumer reads this yet.");
        sb.AppendLine("  6. No network packets are sent.");
        sb.AppendLine("  7. No save/load state is written.");
        sb.AppendLine("  8. Survivor scenario remains blocked until disambiguation.");
        sb.AppendLine("  9. Future phases may populate this from validated option/server/XML policy, but not in this phase.");
        return sb.ToString();
    }

    private static RebirthRuntimeScenarioPolicySnapshot CreateDefaultSnapshot(int version, string reason)
    {
        return new RebirthRuntimeScenarioPolicySnapshot
        {
            Flags = RebirthRuntimeScenarioFlags.Common | RebirthRuntimeScenarioFlags.BlockedManualReview,
            Source = RebirthRuntimeScenarioSource.ExplicitDefaults,
            PurgeEnabled = false,
            HiveEnabled = false,
            SurvivorScenarioEnabled = false,
            FutureCustomEnabled = false,
            IsServerAuthoritative = true,
            IsClientDisplayOnly = false,
            BlocksSurvivorMigration = true,
            Version = version,
            Reason = string.IsNullOrEmpty(reason) ? "explicit defaults" : reason
        };
    }

    private static RebirthRuntimeScenarioPolicySnapshot CloneSnapshot(RebirthRuntimeScenarioPolicySnapshot source)
    {
        if (source == null)
            return CreateDefaultSnapshot(0, "null source fallback");

        return new RebirthRuntimeScenarioPolicySnapshot
        {
            Flags = source.Flags,
            Source = source.Source,
            PurgeEnabled = source.PurgeEnabled,
            HiveEnabled = source.HiveEnabled,
            SurvivorScenarioEnabled = source.SurvivorScenarioEnabled,
            FutureCustomEnabled = source.FutureCustomEnabled,
            IsServerAuthoritative = source.IsServerAuthoritative,
            IsClientDisplayOnly = source.IsClientDisplayOnly,
            BlocksSurvivorMigration = source.BlocksSurvivorMigration,
            Version = source.Version,
            Reason = source.Reason
        };
    }
}
