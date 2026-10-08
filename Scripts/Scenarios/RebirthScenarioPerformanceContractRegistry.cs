using System;
using System.Text;

#nullable disable

public enum RebirthScenarioPerfScenarioSetKind
{
    Unknown,
    BaselineNone,
    PurgeOnly,
    HiveOnly,
    SurvivorOnly,
    PurgeHive,
    PurgeSurvivor,
    HiveSurvivor,
    AllKnown,
    FutureCustom
}

public enum RebirthScenarioPerfMetricKind
{
    Unknown,
    AverageFps,
    OnePercentLowFps,
    WorstFrameMs,
    GcAllocBytes,
    GcCollections,
    SchedulerCostMs,
    HotPathGateReads,
    NetworkPackageCount,
    MarkerUpdateCount,
    SpawnPolicyCostMs,
    UiUpdateCostMs,
    XmlRuntimeLookups,
    Notes
}

public enum RebirthScenarioPerfBudgetRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    BlockedUntilDisambiguated
}

public enum RebirthScenarioPerfMeasurementMode
{
    Unknown,
    ManualDryRunOnly,
    FutureRuntimeProbe,
    FutureProfilingBuild,
    FutureStrippedComparison,
    BlockedManualReview
}

public sealed class RebirthScenarioPerformanceSetDecl
{
    public string SetId;
    public RebirthScenarioPerfScenarioSetKind SetKind;
    public string ScenarioIds;
    public RebirthScenarioPerfMeasurementMode MeasurementMode;
    public RebirthScenarioPerfBudgetRisk Risk;
    public string RequiredSnapshotPolicy;
    public string RequiredTestScenes;
    public string ExpectedHotSystems;
    public string Notes;
}

public sealed class RebirthScenarioPerformanceMetricDecl
{
    public string MetricId;
    public RebirthScenarioPerfMetricKind MetricKind;
    public string Unit;
    public string AppliesTo;
    public RebirthScenarioPerfMeasurementMode MeasurementMode;
    public string Reason;
    public string Notes;
}

/// <summary>
/// Read-only scenario performance contract registry.
/// This does not measure FPS or run tests.
/// It defines what scenario combinations must be tested later.
/// </summary>
public static class RebirthScenarioPerformanceContractRegistry
{
    private static readonly RebirthScenarioPerformanceSetDecl[] s_sets = new[]
    {
        new RebirthScenarioPerformanceSetDecl
        {
            SetId = "perf.none.baseline",
            SetKind = RebirthScenarioPerfScenarioSetKind.BaselineNone,
            ScenarioIds = "none",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.ManualDryRunOnly,
            Risk = RebirthScenarioPerfBudgetRisk.Low,
            RequiredSnapshotPolicy = "snapshot.none",
            RequiredTestScenes = "standing, running, driving, inventory, chunk-boundary",
            ExpectedHotSystems = "core modules only",
            Notes = "Baseline is required before attributing cost to any scenario."
        },
        new RebirthScenarioPerformanceSetDecl
        {
            SetId = "perf.purge.only",
            SetKind = RebirthScenarioPerfScenarioSetKind.PurgeOnly,
            ScenarioIds = "purge",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Risk = RebirthScenarioPerfBudgetRisk.High,
            RequiredSnapshotPolicy = "snapshot.purge",
            RequiredTestScenes = "POI discovery, purge progress update, map/compass markers, supply crate, sleeper volume, driving through POIs",
            ExpectedHotSystems = "ui.hud, ui.compass, map.markers, spawning.groups, sleeper volumes, network packages",
            Notes = "Purge should be measured event-heavy and idle; UI must not recompute purge state every tick."
        },
        new RebirthScenarioPerformanceSetDecl
        {
            SetId = "perf.hive.only",
            SetKind = RebirthScenarioPerfScenarioSetKind.HiveOnly,
            ScenarioIds = "hive",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Risk = RebirthScenarioPerfBudgetRisk.High,
            RequiredSnapshotPolicy = "snapshot.hive",
            RequiredTestScenes = "cave tunnel, cave POI, underground POI, blood moon spawn check, biome spawn check",
            ExpectedHotSystems = "world.caves, spawning.biomes, spawning.groups, prefab/tag policy",
            Notes = "Hive tests must separate cave tunnel behavior from cave POI tag behavior."
        },
        new RebirthScenarioPerformanceSetDecl
        {
            SetId = "perf.survivor.only",
            SetKind = RebirthScenarioPerfScenarioSetKind.SurvivorOnly,
            ScenarioIds = "survivor",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.BlockedManualReview,
            Risk = RebirthScenarioPerfBudgetRisk.BlockedUntilDisambiguated,
            RequiredSnapshotPolicy = "snapshot.survivor",
            RequiredTestScenes = "blocked until survivor scenario vs survivor entity role is classified",
            ExpectedHotSystems = "unknown until disambiguated",
            Notes = "Do not measure survivor scenario costs until survivor token ownership is resolved."
        },
        new RebirthScenarioPerformanceSetDecl
        {
            SetId = "perf.purge.hive",
            SetKind = RebirthScenarioPerfScenarioSetKind.PurgeHive,
            ScenarioIds = "purge+hive",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Risk = RebirthScenarioPerfBudgetRisk.Critical,
            RequiredSnapshotPolicy = "snapshot.purge + snapshot.hive",
            RequiredTestScenes = "purge POI discovery inside cave/underground POI, sleeper volume, cave spawn, map markers, driving into cave POI area",
            ExpectedHotSystems = "scenario.purge, world.caves, spawning, ui.compass, map.markers, network packages",
            Notes = "Combination test is required because purge POI/sleeper systems and hive cave/POI systems overlap."
        },
        new RebirthScenarioPerformanceSetDecl
        {
            SetId = "perf.purge.survivor",
            SetKind = RebirthScenarioPerfScenarioSetKind.PurgeSurvivor,
            ScenarioIds = "purge+survivor",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.BlockedManualReview,
            Risk = RebirthScenarioPerfBudgetRisk.BlockedUntilDisambiguated,
            RequiredSnapshotPolicy = "snapshot.purge + snapshot.survivor",
            RequiredTestScenes = "blocked until survivor disambiguation",
            ExpectedHotSystems = "purge known; survivor unknown until disambiguated",
            Notes = "Purge can be measured alone; purge+survivor is blocked until survivor scenario meaning is explicit."
        },
        new RebirthScenarioPerformanceSetDecl
        {
            SetId = "perf.all.known",
            SetKind = RebirthScenarioPerfScenarioSetKind.AllKnown,
            ScenarioIds = "purge+hive+survivor",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.BlockedManualReview,
            Risk = RebirthScenarioPerfBudgetRisk.BlockedUntilDisambiguated,
            RequiredSnapshotPolicy = "all known snapshots",
            RequiredTestScenes = "blocked until survivor disambiguation and combination policy review",
            ExpectedHotSystems = "all scenario systems",
            Notes = "All-known scenario measurement must not happen until every scenario has contracts/bindings/runtime policy."
        },
        new RebirthScenarioPerformanceSetDecl
        {
            SetId = "perf.future.custom",
            SetKind = RebirthScenarioPerfScenarioSetKind.FutureCustom,
            ScenarioIds = "future.custom",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.BlockedManualReview,
            Risk = RebirthScenarioPerfBudgetRisk.High,
            RequiredSnapshotPolicy = "custom scenario snapshot required",
            RequiredTestScenes = "must be declared by the custom scenario contract",
            ExpectedHotSystems = "must be declared by custom scenario bindings",
            Notes = "Future scenarios must bring their own performance contract before behavior."
        }
    };

    private static readonly RebirthScenarioPerformanceMetricDecl[] s_metrics = new[]
    {
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.averageFps",
            MetricKind = RebirthScenarioPerfMetricKind.AverageFps,
            Unit = "fps",
            AppliesTo = "all scenario sets",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Reason = "Basic comparison across scenario combinations.",
            Notes = "Must be recorded with same world/position/test path."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.onePercentLowFps",
            MetricKind = RebirthScenarioPerfMetricKind.OnePercentLowFps,
            Unit = "fps",
            AppliesTo = "all scenario sets",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Reason = "Captures stutter/hitch impact better than average FPS alone.",
            Notes = "Important for driving/chunk-boundary/purge marker/sleeper updates."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.worstFrameMs",
            MetricKind = RebirthScenarioPerfMetricKind.WorstFrameMs,
            Unit = "ms",
            AppliesTo = "all scenario sets",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Reason = "Detects large hitches caused by scenario events.",
            Notes = "Must be paired with event notes."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.gcAllocBytes",
            MetricKind = RebirthScenarioPerfMetricKind.GcAllocBytes,
            Unit = "bytes",
            AppliesTo = "hot-path scenario tests",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureProfilingBuild,
            Reason = "Scenario checks must not allocate in hot paths.",
            Notes = "String building, LINQ, collections, stack traces, and XML/option lookups are suspect."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.schedulerCost",
            MetricKind = RebirthScenarioPerfMetricKind.SchedulerCostMs,
            Unit = "ms",
            AppliesTo = "scheduler-driven scenario managers",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Reason = "Scenario manager work should be scheduled and bounded.",
            Notes = "Useful for purge scanning and hive spawn policy updates."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.hotPathGateReads",
            MetricKind = RebirthScenarioPerfMetricKind.HotPathGateReads,
            Unit = "count",
            AppliesTo = "hot-path adapters",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureProfilingBuild,
            Reason = "Hot paths should read cached flags only.",
            Notes = "Counts should not imply scans or expensive policy resolution."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.networkPackages",
            MetricKind = RebirthScenarioPerfMetricKind.NetworkPackageCount,
            Unit = "count",
            AppliesTo = "purge/progress/sleeper/display scenarios",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Reason = "Scenario display/progress must not spam network packages.",
            Notes = "Purge progress and marker display should be event-driven."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.markerUpdates",
            MetricKind = RebirthScenarioPerfMetricKind.MarkerUpdateCount,
            Unit = "count",
            AppliesTo = "purge map/compass marker scenarios",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe,
            Reason = "Marker flicker/repeated updates are both correctness and performance risks.",
            Notes = "Should update on spawn/destroy/login/progress changes only."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.spawnPolicyCost",
            MetricKind = RebirthScenarioPerfMetricKind.SpawnPolicyCostMs,
            Unit = "ms",
            AppliesTo = "purge/hive spawning tests",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureProfilingBuild,
            Reason = "Spawn policy lookup must be cached and server-authoritative.",
            Notes = "Relevant to sleepers, entitygroups, caves, blood moon spawn restrictions."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.uiUpdateCost",
            MetricKind = RebirthScenarioPerfMetricKind.UiUpdateCostMs,
            Unit = "ms",
            AppliesTo = "purge/hud/compass tests",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureProfilingBuild,
            Reason = "UI should consume cached display state.",
            Notes = "No purge discovery/progress scans from HUD update loops."
        },
        new RebirthScenarioPerformanceMetricDecl
        {
            MetricId = "metric.xmlRuntimeLookups",
            MetricKind = RebirthScenarioPerfMetricKind.XmlRuntimeLookups,
            Unit = "count",
            AppliesTo = "all scenarios",
            MeasurementMode = RebirthScenarioPerfMeasurementMode.FutureProfilingBuild,
            Reason = "Runtime XML lookups in hot paths should be zero.",
            Notes = "Scenario XML must resolve at load/init into policies."
        }
    };

    public static string GetSummaryReport()
    {
        int blocked = 0;
        int critical = 0;
        int runtimeProbe = 0;

        for (int i = 0; i < s_sets.Length; i++)
        {
            RebirthScenarioPerformanceSetDecl s = s_sets[i];

            if (s.MeasurementMode == RebirthScenarioPerfMeasurementMode.BlockedManualReview)
                blocked++;
            if (s.Risk == RebirthScenarioPerfBudgetRisk.Critical)
                critical++;
            if (s.MeasurementMode == RebirthScenarioPerfMeasurementMode.FutureRuntimeProbe)
                runtimeProbe++;
        }

        return "[RebirthScenarioPerf] Sets: " + s_sets.Length
            + "; metrics: " + s_metrics.Length
            + "; future runtime probe sets: " + runtimeProbe
            + "; critical sets: " + critical
            + "; blocked sets: " + blocked
            + ". Contracts are read-only; declarations only, measurement=NotRun.";
    }

    public static string GetSetReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthScenarioPerf] scenario performance test sets.");
        sb.AppendLine("  Read-only. This does not run tests or measure FPS.");
        sb.AppendLine("set | scenarios | mode | risk | snapshot | scenes | hot systems | notes");

        bool any = false;
        for (int i = 0; i < s_sets.Length; i++)
        {
            RebirthScenarioPerformanceSetDecl s = s_sets[i];
            if (!MatchesSet(s, f))
                continue;

            any = true;
            sb.Append(s.SetId).Append(" | ")
              .Append(s.ScenarioIds).Append(" | ")
              .Append(s.MeasurementMode).Append(" | ")
              .Append(s.Risk).Append(" | ")
              .Append(s.RequiredSnapshotPolicy).Append(" | ")
              .Append(s.RequiredTestScenes).Append(" | ")
              .Append(s.ExpectedHotSystems).Append(" | ")
              .AppendLine(s.Notes);
        }

        if (!any)
            sb.AppendLine("No scenario performance set matched the filter.");

        return sb.ToString();
    }

    public static string GetMetricReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(16384);
        sb.AppendLine("[RebirthScenarioPerf] scenario performance metrics.");
        sb.AppendLine("metric | kind | unit | applies to | mode | reason | notes");

        bool any = false;
        for (int i = 0; i < s_metrics.Length; i++)
        {
            RebirthScenarioPerformanceMetricDecl m = s_metrics[i];
            if (!MatchesMetric(m, f))
                continue;

            any = true;
            sb.Append(m.MetricId).Append(" | ")
              .Append(m.MetricKind).Append(" | ")
              .Append(m.Unit).Append(" | ")
              .Append(m.AppliesTo).Append(" | ")
              .Append(m.MeasurementMode).Append(" | ")
              .Append(m.Reason).Append(" | ")
              .AppendLine(m.Notes);
        }

        if (!any)
            sb.AppendLine("No scenario performance metric matched the filter.");

        return sb.ToString();
    }

    public static string GetMatrixReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioPerf] required scenario comparison matrix:");
        sb.AppendLine("  1. none baseline");
        sb.AppendLine("  2. purge only");
        sb.AppendLine("  3. hive only");
        sb.AppendLine("  4. survivor only - blocked until disambiguation");
        sb.AppendLine("  5. purge + hive");
        sb.AppendLine("  6. purge + survivor - blocked until disambiguation");
        sb.AppendLine("  7. hive + survivor - blocked until disambiguation");
        sb.AppendLine("  8. all known - blocked until disambiguation and combination review");
        sb.AppendLine("  9. future.custom - must declare its own contract first");
        sb.AppendLine();
        sb.AppendLine("Rule: a scenario combination cannot be performance-tested as a single black box unless each scenario's module/XML/runtime policy is known.");
        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthScenarioPerf] blocked scenario performance sets.");
        sb.AppendLine("set | scenarios | reason | notes");

        for (int i = 0; i < s_sets.Length; i++)
        {
            RebirthScenarioPerformanceSetDecl s = s_sets[i];
            if (s.MeasurementMode != RebirthScenarioPerfMeasurementMode.BlockedManualReview
                && s.Risk != RebirthScenarioPerfBudgetRisk.BlockedUntilDisambiguated)
                continue;

            sb.Append(s.SetId).Append(" | ")
              .Append(s.ScenarioIds).Append(" | ")
              .Append(s.Risk).Append(" | ")
              .AppendLine(s.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthScenarioPerf] safety rules:");
        sb.AppendLine("  1. Contracts are read-only.");
        sb.AppendLine("  2. No tests are executed.");
        sb.AppendLine("  3. No FPS/frame/GC metrics are captured yet.");
        sb.AppendLine("  4. No scenario activation/deactivation occurs.");
        sb.AppendLine("  5. No runtime XML parsing occurs.");
        sb.AppendLine("  6. No Harmony patch install/uninstall occurs.");
        sb.AppendLine("  7. Survivor scenario measurements stay blocked until disambiguation.");
        sb.AppendLine("  8. Future/custom scenarios must declare performance contracts before behavior.");
        return sb.ToString();
    }

    private static bool MatchesSet(RebirthScenarioPerformanceSetDecl s, string filter)
    {
        if (s == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (s.SetId != null && s.SetId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.ScenarioIds != null && s.ScenarioIds.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.RequiredTestScenes != null && s.RequiredTestScenes.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.ExpectedHotSystems != null && s.ExpectedHotSystems.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.MeasurementMode.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private static bool MatchesMetric(RebirthScenarioPerformanceMetricDecl m, string filter)
    {
        if (m == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (m.MetricId != null && m.MetricId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.AppliesTo != null && m.AppliesTo.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.Reason != null && m.Reason.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.MetricKind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (m.MeasurementMode.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
