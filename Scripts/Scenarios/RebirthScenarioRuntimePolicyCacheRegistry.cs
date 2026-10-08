using System;
using System.Text;

#nullable disable

[Flags]
public enum RebirthScenarioPolicyFlags
{
    None = 0,
    Baseline = 1 << 0,
    Common = 1 << 1,
    Purge = 1 << 2,
    Hive = 1 << 3,
    SurvivorScenario = 1 << 4,
    FutureCustom = 1 << 5,

    HasServerAuthority = 1 << 10,
    HasClientDisplay = 1 << 11,
    HasXmlOwnership = 1 << 12,
    HasHotPathConcern = 1 << 13,
    RequiresManualReview = 1 << 14,
    RequiresSurvivorDisambiguation = 1 << 15
}

public enum RebirthScenarioPolicyCacheSource
{
    Unknown,
    ContractRegistry,
    ModuleBindingRegistry,
    XmlOwnershipRegistry,
    FutureRuntimeResolver,
    ManualReview
}

public enum RebirthScenarioPolicyCacheState
{
    Unknown,
    Planned,
    ResolvedReadOnly,
    FutureLiveSnapshot,
    BlockedManualReview,
    BlockedDisambiguation
}

public sealed class RebirthScenarioPolicySnapshotDecl
{
    public string SnapshotId;
    public string ScenarioId;
    public RebirthScenarioPolicyFlags Flags;
    public RebirthScenarioPolicyCacheSource Source;
    public RebirthScenarioPolicyCacheState State;
    public string HotPathGateName;
    public string ServerPolicyName;
    public string ClientDisplayPolicyName;
    public string XmlPolicyName;
    public string Notes;
}

/// <summary>
/// Read-only scenario runtime policy cache plan.
/// This does not activate scenarios or build a live cache yet.
/// It defines the cheap snapshot shape future hot paths should consult.
/// </summary>
public static class RebirthScenarioRuntimePolicyCacheRegistry
{
    private static readonly RebirthScenarioPolicySnapshotDecl[] s_snapshots = new[]
    {
        new RebirthScenarioPolicySnapshotDecl
        {
            SnapshotId = "snapshot.none",
            ScenarioId = "none",
            Flags = RebirthScenarioPolicyFlags.Baseline,
            Source = RebirthScenarioPolicyCacheSource.ContractRegistry,
            State = RebirthScenarioPolicyCacheState.Planned,
            HotPathGateName = "ScenarioPolicy.NoneActive",
            ServerPolicyName = "ScenarioServerPolicy.None",
            ClientDisplayPolicyName = "ScenarioClientDisplay.None",
            XmlPolicyName = "ScenarioXmlPolicy.Baseline",
            Notes = "Baseline snapshot for comparing no-scenario performance."
        },
        new RebirthScenarioPolicySnapshotDecl
        {
            SnapshotId = "snapshot.common",
            ScenarioId = "common",
            Flags = RebirthScenarioPolicyFlags.Common
                | RebirthScenarioPolicyFlags.HasServerAuthority
                | RebirthScenarioPolicyFlags.HasClientDisplay
                | RebirthScenarioPolicyFlags.HasXmlOwnership
                | RebirthScenarioPolicyFlags.HasHotPathConcern,
            Source = RebirthScenarioPolicyCacheSource.ContractRegistry,
            State = RebirthScenarioPolicyCacheState.Planned,
            HotPathGateName = "ScenarioPolicy.CommonEnabled",
            ServerPolicyName = "ScenarioServerPolicy.Common",
            ClientDisplayPolicyName = "ScenarioClientDisplay.Common",
            XmlPolicyName = "ScenarioXmlPolicy.Common",
            Notes = "Common infrastructure snapshot. Must not become scenario-specific dumping ground."
        },
        new RebirthScenarioPolicySnapshotDecl
        {
            SnapshotId = "snapshot.purge",
            ScenarioId = "purge",
            Flags = RebirthScenarioPolicyFlags.Purge
                | RebirthScenarioPolicyFlags.HasServerAuthority
                | RebirthScenarioPolicyFlags.HasClientDisplay
                | RebirthScenarioPolicyFlags.HasXmlOwnership
                | RebirthScenarioPolicyFlags.HasHotPathConcern,
            Source = RebirthScenarioPolicyCacheSource.ContractRegistry,
            State = RebirthScenarioPolicyCacheState.Planned,
            HotPathGateName = "ScenarioPolicy.PurgeEnabled",
            ServerPolicyName = "PurgeServerPolicy.ProgressAndSpawns",
            ClientDisplayPolicyName = "PurgeClientDisplayPolicy.CachedHudAndMarkers",
            XmlPolicyName = "PurgeXmlPolicy.OwnedLayers",
            Notes = "Purge server truth and client display snapshots must be separated."
        },
        new RebirthScenarioPolicySnapshotDecl
        {
            SnapshotId = "snapshot.hive",
            ScenarioId = "hive",
            Flags = RebirthScenarioPolicyFlags.Hive
                | RebirthScenarioPolicyFlags.HasServerAuthority
                | RebirthScenarioPolicyFlags.HasXmlOwnership
                | RebirthScenarioPolicyFlags.HasHotPathConcern,
            Source = RebirthScenarioPolicyCacheSource.ContractRegistry,
            State = RebirthScenarioPolicyCacheState.Planned,
            HotPathGateName = "ScenarioPolicy.HiveEnabled",
            ServerPolicyName = "HiveServerPolicy.CavesAndSpawning",
            ClientDisplayPolicyName = "HiveClientDisplayPolicy.NoneOrMinimal",
            XmlPolicyName = "HiveXmlPolicy.CavePoiAndTunnelOwnership",
            Notes = "Hive policy must keep cave tunnel checks separate from cave POI tag checks."
        },
        new RebirthScenarioPolicySnapshotDecl
        {
            SnapshotId = "snapshot.survivor",
            ScenarioId = "survivor",
            Flags = RebirthScenarioPolicyFlags.SurvivorScenario
                | RebirthScenarioPolicyFlags.HasServerAuthority
                | RebirthScenarioPolicyFlags.HasClientDisplay
                | RebirthScenarioPolicyFlags.HasXmlOwnership
                | RebirthScenarioPolicyFlags.HasHotPathConcern
                | RebirthScenarioPolicyFlags.RequiresManualReview
                | RebirthScenarioPolicyFlags.RequiresSurvivorDisambiguation,
            Source = RebirthScenarioPolicyCacheSource.ManualReview,
            State = RebirthScenarioPolicyCacheState.BlockedDisambiguation,
            HotPathGateName = "ScenarioPolicy.SurvivorScenarioEnabled",
            ServerPolicyName = "SurvivorScenarioServerPolicy.BlockedUntilDisambiguated",
            ClientDisplayPolicyName = "SurvivorScenarioClientDisplay.BlockedUntilDisambiguated",
            XmlPolicyName = "SurvivorScenarioXmlPolicy.BlockedUntilDisambiguated",
            Notes = "Survivor scenario is blocked until survivor NPC/entity role/token usage is separated."
        },
        new RebirthScenarioPolicySnapshotDecl
        {
            SnapshotId = "snapshot.future.custom",
            ScenarioId = "future.custom",
            Flags = RebirthScenarioPolicyFlags.FutureCustom
                | RebirthScenarioPolicyFlags.HasServerAuthority
                | RebirthScenarioPolicyFlags.HasClientDisplay
                | RebirthScenarioPolicyFlags.HasXmlOwnership
                | RebirthScenarioPolicyFlags.HasHotPathConcern
                | RebirthScenarioPolicyFlags.RequiresManualReview,
            Source = RebirthScenarioPolicyCacheSource.ManualReview,
            State = RebirthScenarioPolicyCacheState.BlockedManualReview,
            HotPathGateName = "ScenarioPolicy.CustomScenarioEnabled",
            ServerPolicyName = "CustomScenarioServerPolicy.RequiresContract",
            ClientDisplayPolicyName = "CustomScenarioClientDisplay.RequiresContract",
            XmlPolicyName = "CustomScenarioXmlPolicy.RequiresContract",
            Notes = "Future scenarios must declare contracts/bindings/XML/perf budgets before behavior."
        }
    };

    public static RebirthScenarioPolicySnapshotDecl[] GetSnapshot()
    {
        RebirthScenarioPolicySnapshotDecl[] copy = new RebirthScenarioPolicySnapshotDecl[s_snapshots.Length];
        for (int i=0;i<s_snapshots.Length;i++)
        {
            RebirthScenarioPolicySnapshotDecl source=s_snapshots[i];
            copy[i]=source==null?null:new RebirthScenarioPolicySnapshotDecl { SnapshotId = source.SnapshotId, ScenarioId = source.ScenarioId, Flags = source.Flags, Source = source.Source, State = source.State, HotPathGateName = source.HotPathGateName, ServerPolicyName = source.ServerPolicyName, ClientDisplayPolicyName = source.ClientDisplayPolicyName, XmlPolicyName = source.XmlPolicyName, Notes = source.Notes };
        }
        return copy;
    }

    public static string GetSummaryReport()
    {
        int planned = 0;
        int blocked = 0;
        int server = 0;
        int client = 0;
        int hot = 0;

        for (int i = 0; i < s_snapshots.Length; i++)
        {
            RebirthScenarioPolicySnapshotDecl s = s_snapshots[i];

            if (s.State == RebirthScenarioPolicyCacheState.Planned)
                planned++;
            if (s.State == RebirthScenarioPolicyCacheState.BlockedManualReview || s.State == RebirthScenarioPolicyCacheState.BlockedDisambiguation)
                blocked++;
            if ((s.Flags & RebirthScenarioPolicyFlags.HasServerAuthority) != 0)
                server++;
            if ((s.Flags & RebirthScenarioPolicyFlags.HasClientDisplay) != 0)
                client++;
            if ((s.Flags & RebirthScenarioPolicyFlags.HasHotPathConcern) != 0)
                hot++;
        }

        return "[RebirthScenarioPolicyCache] Snapshots: " + s_snapshots.Length
            + "; planned: " + planned
            + "; blocked: " + blocked
            + "; server-policy: " + server
            + "; client-display: " + client
            + "; hot-path concerns: " + hot
            + ". Registry is read-only.";
    }

    public static string GetSnapshotReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthScenarioPolicyCache] planned runtime policy snapshots.");
        sb.AppendLine("  Read-only. This does not build live cache state or activate scenarios.");
        sb.AppendLine("snapshot | scenario | flags | source | state | hot gate | server policy | client display | xml policy | notes");

        bool any = false;
        for (int i = 0; i < s_snapshots.Length; i++)
        {
            RebirthScenarioPolicySnapshotDecl s = s_snapshots[i];
            if (!Matches(s, f))
                continue;

            any = true;
            sb.Append(s.SnapshotId).Append(" | ")
              .Append(s.ScenarioId).Append(" | ")
              .Append(s.Flags).Append(" | ")
              .Append(s.Source).Append(" | ")
              .Append(s.State).Append(" | ")
              .Append(s.HotPathGateName).Append(" | ")
              .Append(s.ServerPolicyName).Append(" | ")
              .Append(s.ClientDisplayPolicyName).Append(" | ")
              .Append(s.XmlPolicyName).Append(" | ")
              .AppendLine(s.Notes);
        }

        if (!any)
            sb.AppendLine("No policy snapshot matched the filter.");

        return sb.ToString();
    }

    public static string GetHotPathGateReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioPolicyCache] future hot-path gates.");
        sb.AppendLine("scenario | gate | state | rule | notes");

        for (int i = 0; i < s_snapshots.Length; i++)
        {
            RebirthScenarioPolicySnapshotDecl s = s_snapshots[i];
            if ((s.Flags & RebirthScenarioPolicyFlags.HasHotPathConcern) == 0)
                continue;

            sb.Append(s.ScenarioId).Append(" | ")
              .Append(s.HotPathGateName).Append(" | ")
              .Append(s.State).Append(" | ")
              .Append("hot paths read cached policy only").Append(" | ")
              .AppendLine(s.Notes);
        }

        sb.AppendLine("Rule: no XML/option/string scans inside hot paths.");
        return sb.ToString();
    }

    public static string GetAuthorityReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioPolicyCache] server/client policy split.");
        sb.AppendLine("scenario | server policy | client display policy | notes");

        for (int i = 0; i < s_snapshots.Length; i++)
        {
            RebirthScenarioPolicySnapshotDecl s = s_snapshots[i];
            if ((s.Flags & RebirthScenarioPolicyFlags.HasServerAuthority) == 0
                && (s.Flags & RebirthScenarioPolicyFlags.HasClientDisplay) == 0)
                continue;

            sb.Append(s.ScenarioId).Append(" | ")
              .Append(s.ServerPolicyName).Append(" | ")
              .Append(s.ClientDisplayPolicyName).Append(" | ")
              .AppendLine(s.Notes);
        }

        return sb.ToString();
    }

    public static string GetInvalidationReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioPolicyCache] future invalidation rules:");
        sb.AppendLine("  - World load: resolve active scenario set and XML ownership.");
        sb.AppendLine("  - Custom game option change: rebuild scenario policy snapshot.");
        sb.AppendLine("  - Server join: server sends authoritative scenario policy summary to client.");
        sb.AppendLine("  - Player login: send cached display state, not full recomputation from UI.");
        sb.AppendLine("  - POI discovery/progress event: update purge display snapshot/event markers.");
        sb.AppendLine("  - Spawn policy reload: rebuild cached spawning restrictions.");
        sb.AppendLine("  - XML reload/mod reload: rebuild XML ownership and scenario policy references.");
        sb.AppendLine("  - Survivor disambiguation unresolved: survivor scenario policy remains blocked.");
        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthScenarioPolicyCache] safety rules:");
        sb.AppendLine("  1. Registry is read-only.");
        sb.AppendLine("  2. No live scenario cache is built yet.");
        sb.AppendLine("  3. No scenario activation/deactivation occurs.");
        sb.AppendLine("  4. No runtime XML parsing occurs.");
        sb.AppendLine("  5. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  6. No gameplay behavior is connected.");
        sb.AppendLine("  7. Future hot paths must read cached booleans/enums only.");
        sb.AppendLine("  8. Survivor scenario remains blocked until disambiguation is complete.");
        return sb.ToString();
    }

    private static bool Matches(RebirthScenarioPolicySnapshotDecl s, string filter)
    {
        if (s == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (s.SnapshotId != null && s.SnapshotId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.ScenarioId != null && s.ScenarioId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.HotPathGateName != null && s.HotPathGateName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.ServerPolicyName != null && s.ServerPolicyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.ClientDisplayPolicyName != null && s.ClientDisplayPolicyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.XmlPolicyName != null && s.XmlPolicyName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.State.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.Flags.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
