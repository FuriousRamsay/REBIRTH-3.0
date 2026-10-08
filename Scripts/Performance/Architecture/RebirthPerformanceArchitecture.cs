using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

#nullable disable

public enum RebirthModuleCategory
{
    Core,
    Paint,
    Entities,
    AI,
    Vehicles,
    Player,
    UI,
    Items,
    World,
    Diagnostics,
    External
}

public enum RebirthToggleTier
{
    PlayerFacing,
    Profiling,
    DiagnosticBuildOnly
}

public enum RebirthModuleAuthority
{
    ServerOnly,
    ClientOnly,
    ServerAndClient,
    HostOnly
}

public enum RebirthToggleSyncMode
{
    LocalOnly,
    ServerAuthoritative,
    WorldLoadOnly,
    BuildOnly
}

public enum RebirthPatchKind
{
    Prefix,
    Postfix,
    Transpiler,
    Finalizer,
    TargetMethods
}

public enum RebirthPatchInstallMode
{
    Always,
    WorldLoadIfEnabled,
    ProfilingOnly,
    DiagnosticBuildOnly
}

public enum RebirthEnginePhase
{
    None,
    GameUpdateEvent,
    GameManagerUpdatePrefix,
    GameManagerUpdatePostfix,
    GameManagerGmUpdatePrefix,
    GameManagerGmUpdatePostfix,
    PlayerMoveUpdate,
    CameraLateUpdate,
    EntityAliveUpdate,
    EntityZombieUpdate,
    EaiCanExecute,
    EaiFindTarget,
    MeshRenderFace,
    UiUpdate,
    WorldUnload
}

public enum RebirthTickCadenceKind
{
    EventOnly,
    EveryFrame,
    Interval,
    BudgetedQueue,
    ScheduledOnly
}

public sealed class RebirthPatchDecl
{
    public string TargetTypeName;
    public string TargetMethodName;
    public RebirthPatchKind Kind;
    public RebirthPatchSafetyKind SafetyKind;
    public RebirthPatchInstallMode InstallMode;
    public RebirthEnginePhase Phase;
    public bool CanUnpatchWhileWorldLoaded;
    public bool ReplacesVanilla;
    public string VanillaDriftReviewNote;

    public string TargetDisplayName
    {
        get { return (TargetTypeName ?? "<unknown>") + "." + (TargetMethodName ?? "<unknown>"); }
    }
}

public sealed class RebirthTickDecl
{
    public RebirthEnginePhase Phase;
    public RebirthTickCadenceKind Cadence;
    public double IntervalSeconds;
    public int BudgetMicros;
    public bool RequiresWorld;
    public bool RequiresLocalPlayer;
    public bool ServerOnly;
    public bool ClientOnly;
}

public sealed class RebirthModuleManifest
{
    public string Id;
    public string DisplayName;
    public RebirthModuleCategory Category;
    public RebirthToggleTier Tier;
    public RebirthToggleSyncMode SyncMode;
    public RebirthModuleAuthority Authority;
    public bool DefaultEnabled;
    public bool CanToggleWhileWorldLoaded;
    public bool RequiresWorldReloadToChange;
    public bool ChangesGameplaySemantics;
    public RebirthPatchDecl[] Patches;
    public RebirthTickDecl[] Ticks;
    public RebirthEnginePhase[] EnginePhases;
    public string[] DependsOn;
    public string[] ConflictsWith;
    public double BudgetAvgMicrosPerFrame;
    public double BudgetWorstMicrosPerFrame;
    public int BudgetMaxCallsPerFrame;
    public string BudgetJustification;
    public string Owner;
    public string LastReviewedGameVersion;
    public string[] RequiredSmokeTests;
}

/// <summary>
/// Lightweight architecture registry introduced for the performance-first rewrite.
/// This first pass is intentionally declarative: it records module ownership,
/// toggle semantics, authority, budgets and patch surfaces without moving behavior yet.
/// </summary>
public static class RebirthModuleRegistry
{
    private static readonly List<RebirthModuleManifest> s_modules = new List<RebirthModuleManifest>(64);
    private static readonly Dictionary<string, RebirthModuleManifest> s_byId = new Dictionary<string, RebirthModuleManifest>(StringComparer.OrdinalIgnoreCase);
    private static bool s_registered;

    public static void EnsureRegistered()
    {
        if (s_registered)
            return;

        s_registered = true;
        RegisterDefaults();
    }

    public static RebirthModuleManifest[] GetModulesSnapshot()
    {
        EnsureRegistered();
        return s_modules.ToArray();
    }

    public static bool TryGetModule(string id, out RebirthModuleManifest module)
    {
        EnsureRegistered();
        return s_byId.TryGetValue(id ?? string.Empty, out module);
    }

    public static string GetSummary()
    {
        EnsureRegistered();
        int player = 0;
        int profiling = 0;
        int diagnostic = 0;
        int structuralPatches = 0;
        int behavioralPatches = 0;
        int diagnosticPatches = 0;

        for (int i = 0; i < s_modules.Count; i++)
        {
            RebirthModuleManifest m = s_modules[i];
            if (m.Tier == RebirthToggleTier.PlayerFacing) player++;
            else if (m.Tier == RebirthToggleTier.Profiling) profiling++;
            else diagnostic++;

            if (m.Patches == null) continue;
            for (int p = 0; p < m.Patches.Length; p++)
            {
                switch (m.Patches[p].SafetyKind)
                {
                    case RebirthPatchSafetyKind.Structural: structuralPatches++; break;
                    case RebirthPatchSafetyKind.Behavioral: behavioralPatches++; break;
                    case RebirthPatchSafetyKind.Diagnostic: diagnosticPatches++; break;
                }
            }
        }

        return "modules=" + s_modules.Count
            + " playerFacing=" + player
            + " profiling=" + profiling
            + " diagnostic=" + diagnostic
            + " declaredPatches(structural/behavioral/diagnostic)=" + structuralPatches + "/" + behavioralPatches + "/" + diagnosticPatches;
    }

    public static string GetList(string categoryFilter)
    {
        EnsureRegistered();
        StringBuilder sb = new StringBuilder(4096);
        string filter = (categoryFilter ?? string.Empty).Trim();
        sb.AppendLine("[RebirthModules] declared performance architecture modules:");
        sb.AppendLine("  NOTE: this registry is DECLARATIVE in this pass — 'default' is the manifest value,");
        sb.AppendLine("  not live state. Live enable state remains in the rbsys toggles ('rbsys status').");
        sb.AppendLine("  Manifest<->toggle convergence is scheduled for the patch-registry milestone (M8).");
        for (int i = 0; i < s_modules.Count; i++)
        {
            RebirthModuleManifest m = s_modules[i];
            if (!string.IsNullOrEmpty(filter)
                && !string.Equals(filter, m.Category.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(filter, m.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            sb.Append("  ").Append(m.Id)
              .Append(" tier=").Append(m.Tier)
              .Append(" authority=").Append(m.Authority)
              .Append(" sync=").Append(m.SyncMode)
              .Append(" default=").Append(m.DefaultEnabled ? "on" : "off")
              .Append(" liveToggle=").Append(m.CanToggleWhileWorldLoaded ? "yes" : "no")
              .Append(" gameplay=").Append(m.ChangesGameplaySemantics ? "changes" : "safe")
              .Append(" budgetAvgUs=").Append(m.BudgetAvgMicrosPerFrame.ToString("0.##"))
              .Append(" - ").AppendLine(m.DisplayName ?? string.Empty);
        }
        return sb.ToString();
    }

    public static string GetBudgetReport()
    {
        EnsureRegistered();
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthModules] declared budgets. Runtime cost attribution is incremental; unmeasured modules show current stats as 0.");
        sb.AppendLine("module | category | avgBudgetUs | worstBudgetUs | currentAvgUs | calls | active | justification");
        for (int i = 0; i < s_modules.Count; i++)
        {
            RebirthModuleManifest m = s_modules[i];
            RebirthModuleCostStats.StatsSnapshot stats = RebirthModuleCostStats.GetSnapshot(m.Id);
            sb.Append(m.Id).Append(" | ")
              .Append(m.Category).Append(" | ")
              .Append(m.BudgetAvgMicrosPerFrame.ToString("0.##")).Append(" | ")
              .Append(m.BudgetWorstMicrosPerFrame.ToString("0.##")).Append(" | ")
              .Append(stats.AvgMicros.ToString("0.##")).Append(" | ")
              .Append(stats.Calls).Append(" | ")
              .Append(stats.ActiveCount).Append(" | ")
              .AppendLine(m.BudgetJustification ?? string.Empty);
        }
        return sb.ToString();
    }

    public static string GetLeakReport()
    {
        EnsureRegistered();
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[RebirthModules] active-set leak report scaffold:");
        sb.AppendLine("  No migrated active-set modules are registered yet. This command is reserved for M4+ modules keyed by entityId/creationToken.");
        sb.AppendLine("  Required invariant for future modules: after world unload and entity despawn, active entries must be zero.");
        return sb.ToString();
    }

    private static void RegisterDefaults()
    {
        Register(new RebirthModuleManifest
        {
            Id = "core.patchtoggle",
            DisplayName = "Harmony patch toggle service / structural safety",
            Category = RebirthModuleCategory.Core,
            Tier = RebirthToggleTier.Profiling,
            SyncMode = RebirthToggleSyncMode.LocalOnly,
            Authority = RebirthModuleAuthority.ServerAndClient,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = true,
            BudgetAvgMicrosPerFrame = 50,
            BudgetJustification = "Only active when toggles are changed or status is requested.",
            Owner = "PerformanceArchitecture",
            LastReviewedGameVersion = "2.6",
            Patches = new RebirthPatchDecl[0]
        });

        Register(new RebirthModuleManifest
        {
            Id = "entityfactory.typeResolver",
            DisplayName = "EntityFactory.GetEntityType resolver replacement",
            Category = RebirthModuleCategory.Core,
            Tier = RebirthToggleTier.PlayerFacing,
            SyncMode = RebirthToggleSyncMode.WorldLoadOnly,
            Authority = RebirthModuleAuthority.ServerAndClient,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = false,
            RequiresWorldReloadToChange = true,
            ChangesGameplaySemantics = false,
            BudgetAvgMicrosPerFrame = 0,
            BudgetJustification = "Structural resolver used by XML-instantiated resident content; must remain installed while a world is live.",
            Owner = "Core",
            LastReviewedGameVersion = "2.6",
            Patches = new [] { new RebirthPatchDecl { TargetTypeName = "EntityFactory", TargetMethodName = "GetEntityType", Kind = RebirthPatchKind.Prefix, SafetyKind = RebirthPatchSafetyKind.Structural, InstallMode = RebirthPatchInstallMode.Always, Phase = RebirthEnginePhase.None, CanUnpatchWhileWorldLoaded = false, ReplacesVanilla = true, VanillaDriftReviewNote = "Full replacement prefix. Review every game update." } }
        });

        Register(new RebirthModuleManifest
        {
            Id = "paint.maintenance",
            DisplayName = "Paint/tint per-frame maintenance, drains, store flushes",
            Category = RebirthModuleCategory.Paint,
            Tier = RebirthToggleTier.PlayerFacing,
            SyncMode = RebirthToggleSyncMode.WorldLoadOnly,
            Authority = RebirthModuleAuthority.ServerAndClient,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = false,
            BudgetAvgMicrosPerFrame = 250,
            BudgetWorstMicrosPerFrame = 2000,
            BudgetJustification = "Should be almost idle when no paint/tint data is dirty; budgeted drains only when work exists.",
            Owner = "Paint",
            LastReviewedGameVersion = "2.6",
            Ticks = new [] { new RebirthTickDecl { Phase = RebirthEnginePhase.GameManagerUpdatePostfix, Cadence = RebirthTickCadenceKind.BudgetedQueue, RequiresWorld = true } }
        });

        Register(new RebirthModuleManifest
        {
            Id = "paint.render",
            DisplayName = "Custom paint/tint renderFace resolver",
            Category = RebirthModuleCategory.Paint,
            Tier = RebirthToggleTier.PlayerFacing,
            SyncMode = RebirthToggleSyncMode.WorldLoadOnly,
            Authority = RebirthModuleAuthority.ClientOnly,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = false,
            BudgetAvgMicrosPerFrame = 250,
            BudgetWorstMicrosPerFrame = 4000,
            BudgetJustification = "Expected near-zero on worlds/chunks with no custom paint sidecar data; expensive only during chunk mesh generation with paint/tint sidecars.",
            Owner = "Paint",
            LastReviewedGameVersion = "2.6",
            Patches = new [] { new RebirthPatchDecl { TargetTypeName = "BlockShapeNew", TargetMethodName = "renderFace", Kind = RebirthPatchKind.Transpiler, SafetyKind = RebirthPatchSafetyKind.Behavioral, InstallMode = RebirthPatchInstallMode.WorldLoadIfEnabled, Phase = RebirthEnginePhase.MeshRenderFace, CanUnpatchWhileWorldLoaded = true, ReplacesVanilla = false } }
        });

        Register(new RebirthModuleManifest
        {
            Id = "zombie.resident",
            DisplayName = "Resident zombie subsystem fan-out inside EntityZombieSDX",
            Category = RebirthModuleCategory.Entities,
            Tier = RebirthToggleTier.Profiling,
            SyncMode = RebirthToggleSyncMode.LocalOnly,
            Authority = RebirthModuleAuthority.ServerOnly,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = true,
            BudgetAvgMicrosPerFrame = 500,
            BudgetWorstMicrosPerFrame = 2000,
            BudgetJustification = "Profiling-only gate. Should migrate to active-set systems so idle zombies pay zero resident Rebirth cost.",
            Owner = "Entities",
            LastReviewedGameVersion = "2.6"
        });

        Register(new RebirthModuleManifest
        {
            Id = "zombie.eai.resident",
            DisplayName = "Custom resident Rebirth EAI tasks",
            Category = RebirthModuleCategory.AI,
            Tier = RebirthToggleTier.Profiling,
            SyncMode = RebirthToggleSyncMode.LocalOnly,
            Authority = RebirthModuleAuthority.ServerOnly,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = true,
            BudgetAvgMicrosPerFrame = 750,
            BudgetWorstMicrosPerFrame = 3000,
            BudgetJustification = "Profiling-only. Disabling may make zombies lose Rebirth targeting behavior.",
            Owner = "AI",
            LastReviewedGameVersion = "2.6"
        });

        Register(new RebirthModuleManifest
        {
            Id = "zombie.eai.candidateCache",
            DisplayName = "Shared broad EAI target candidate discovery cache",
            Category = RebirthModuleCategory.AI,
            Tier = RebirthToggleTier.Profiling,
            SyncMode = RebirthToggleSyncMode.ServerAuthoritative,
            Authority = RebirthModuleAuthority.ServerOnly,
            DefaultEnabled = false,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = true,
            BudgetAvgMicrosPerFrame = 500,
            BudgetWorstMicrosPerFrame = 2000,
            BudgetJustification = "Target architecture placeholder. Cache broad candidates only; final target decision remains per-zombie.",
            Owner = "AI",
            LastReviewedGameVersion = "2.6",
            Ticks = new [] { new RebirthTickDecl { Phase = RebirthEnginePhase.GameUpdateEvent, Cadence = RebirthTickCadenceKind.Interval, IntervalSeconds = 0.25, RequiresWorld = true, ServerOnly = true } }
        });

        Register(new RebirthModuleManifest
        {
            Id = "itemstats.modifyvalue",
            DisplayName = "ItemValue.ModifyValue / equipment / armor stat pipeline",
            Category = RebirthModuleCategory.Items,
            Tier = RebirthToggleTier.PlayerFacing,
            SyncMode = RebirthToggleSyncMode.WorldLoadOnly,
            Authority = RebirthModuleAuthority.ServerAndClient,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = true,
            BudgetAvgMicrosPerFrame = 250,
            BudgetWorstMicrosPerFrame = 1000,
            BudgetJustification = "Hot stat path; should use relevance tables and avoid trace locals when tracing is off.",
            Owner = "Bonuses",
            LastReviewedGameVersion = "2.6",
            Patches = new [] { new RebirthPatchDecl { TargetTypeName = "ItemValue", TargetMethodName = "ModifyValue", Kind = RebirthPatchKind.Prefix, SafetyKind = RebirthPatchSafetyKind.Behavioral, InstallMode = RebirthPatchInstallMode.WorldLoadIfEnabled, Phase = RebirthEnginePhase.None, CanUnpatchWhileWorldLoaded = true } }
        });

        Register(new RebirthModuleManifest
        {
            Id = "crawl.player",
            DisplayName = "One-block crawl movement and camera controller",
            Category = RebirthModuleCategory.Player,
            Tier = RebirthToggleTier.PlayerFacing,
            SyncMode = RebirthToggleSyncMode.ServerAuthoritative,
            Authority = RebirthModuleAuthority.ServerAndClient,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = true,
            BudgetAvgMicrosPerFrame = 100,
            BudgetWorstMicrosPerFrame = 1000,
            BudgetJustification = "Must hard-gate vehicle, third-person, non-crouch and non-transition states before geometry probes.",
            Owner = "Player",
            LastReviewedGameVersion = "2.6",
            Patches = new [] { new RebirthPatchDecl { TargetTypeName = "PlayerMoveController", TargetMethodName = "Update", Kind = RebirthPatchKind.Postfix, SafetyKind = RebirthPatchSafetyKind.Behavioral, InstallMode = RebirthPatchInstallMode.WorldLoadIfEnabled, Phase = RebirthEnginePhase.PlayerMoveUpdate, CanUnpatchWhileWorldLoaded = true } }
        });

        Register(new RebirthModuleManifest
        {
            Id = "vehicle.streaming",
            DisplayName = "Vehicle streaming, VML headroom, copy-chunk budget",
            Category = RebirthModuleCategory.Vehicles,
            Tier = RebirthToggleTier.PlayerFacing,
            SyncMode = RebirthToggleSyncMode.LocalOnly,
            Authority = RebirthModuleAuthority.ClientOnly,
            DefaultEnabled = true,
            CanToggleWhileWorldLoaded = true,
            ChangesGameplaySemantics = false,
            BudgetAvgMicrosPerFrame = 200,
            BudgetWorstMicrosPerFrame = 1500,
            BudgetJustification = "May trade hitch reduction against average FPS; should be measured while driving.",
            Owner = "Vehicles",
            LastReviewedGameVersion = "2.6"
        });

        Register(new RebirthModuleManifest
        {
            Id = "diagnostics.chunkProfiler",
            DisplayName = "Chunk diagnostic Harmony profiler hooks",
            Category = RebirthModuleCategory.Diagnostics,
            Tier = RebirthToggleTier.DiagnosticBuildOnly,
            SyncMode = RebirthToggleSyncMode.BuildOnly,
            Authority = RebirthModuleAuthority.ServerAndClient,
            DefaultEnabled = false,
            CanToggleWhileWorldLoaded = false,
            ChangesGameplaySemantics = false,
            BudgetAvgMicrosPerFrame = 0,
            BudgetJustification = "Excluded from release builds by REBIRTH_DIAGNOSTIC_HARMONY.",
            Owner = "Diagnostics",
            LastReviewedGameVersion = "2.6",
            Patches = new [] { new RebirthPatchDecl { TargetTypeName = "DynamicPrefabDecorator", TargetMethodName = "DecorateChunk", Kind = RebirthPatchKind.TargetMethods, SafetyKind = RebirthPatchSafetyKind.Diagnostic, InstallMode = RebirthPatchInstallMode.DiagnosticBuildOnly, Phase = RebirthEnginePhase.GameManagerGmUpdatePostfix, CanUnpatchWhileWorldLoaded = true } }
        });
    }

    private static void Register(RebirthModuleManifest manifest)
    {
        if (manifest == null || string.IsNullOrEmpty(manifest.Id))
            return;
        if (s_byId.ContainsKey(manifest.Id))
            return;

        if (manifest.Patches == null) manifest.Patches = new RebirthPatchDecl[0];
        if (manifest.Ticks == null) manifest.Ticks = new RebirthTickDecl[0];
        if (manifest.EnginePhases == null) manifest.EnginePhases = new RebirthEnginePhase[0];
        if (manifest.DependsOn == null) manifest.DependsOn = new string[0];
        if (manifest.ConflictsWith == null) manifest.ConflictsWith = new string[0];
        if (manifest.RequiredSmokeTests == null) manifest.RequiredSmokeTests = new string[0];

        s_modules.Add(manifest);
        s_byId.Add(manifest.Id, manifest);
    }
}

/// <summary>
/// Low-overhead module attribution sink. First pass is command/report scaffolding and manual recording.
/// Future scheduler/dispatcher calls should record sampled ticks here from one central site.
/// </summary>
public static class RebirthModuleCostStats
{
    public struct StatsSnapshot
    {
        public readonly string ModuleId;
        public readonly long Calls;
        public readonly double AvgMicros;
        public readonly double WorstMicros;
        public readonly int ActiveCount;
        public readonly bool Measured;

        public StatsSnapshot(string moduleId, long calls, double avgMicros, double worstMicros, int activeCount, bool measured)
        {
            ModuleId = moduleId;
            Calls = calls;
            AvgMicros = avgMicros;
            WorstMicros = worstMicros;
            ActiveCount = activeCount;
            Measured = measured;
        }
    }

    private sealed class MutableStats
    {
        public long Calls;
        public long TotalTicks;
        public long WorstTicks;
        public int ActiveCount;
    }

    private static readonly Dictionary<string, MutableStats> s_stats = new Dictionary<string, MutableStats>(StringComparer.OrdinalIgnoreCase);
    private static readonly object s_lock = new object();

    public static bool Enabled = true;
    public static int SampleMask = 31; // selects one in (mask+1) eligible invocations by default.
    private static long s_sampleChecks;
    private static long s_sampleSelections;

    public static bool ShouldSampleThisFrame()
    {
        if (!Enabled) return false;
        long check = System.Threading.Interlocked.Increment(ref s_sampleChecks);
        int mask = SampleMask;
        bool selected = mask <= 0 || ((check - 1L) & mask) == 0L;
        if (selected) System.Threading.Interlocked.Increment(ref s_sampleSelections);
        return selected;
    }

    public static long BeginSample()
    {
        return Stopwatch.GetTimestamp();
    }

    public static void EndSample(string moduleId, long startTicks, int activeCount)
    {
        if (string.IsNullOrEmpty(moduleId))
            return;
        long elapsed = Stopwatch.GetTimestamp() - startTicks;
        RecordTicks(moduleId, elapsed, activeCount);
    }

    public static void RecordTicks(string moduleId, long elapsedTicks, int activeCount)
    {
        if (string.IsNullOrEmpty(moduleId))
            return;
        if (elapsedTicks < 0)
            elapsedTicks = 0;

        lock (s_lock)
        {
            MutableStats stats;
            if (!s_stats.TryGetValue(moduleId, out stats))
            {
                stats = new MutableStats();
                s_stats.Add(moduleId, stats);
            }

            stats.Calls++;
            stats.TotalTicks += elapsedTicks;
            if (elapsedTicks > stats.WorstTicks)
                stats.WorstTicks = elapsedTicks;
            stats.ActiveCount = activeCount;
        }
    }

    public static StatsSnapshot GetSnapshot(string moduleId)
    {
        if (string.IsNullOrEmpty(moduleId))
            return new StatsSnapshot(moduleId ?? string.Empty, 0, 0, 0, 0, false);

        lock (s_lock)
        {
            MutableStats stats;
            if (!s_stats.TryGetValue(moduleId, out stats) || stats.Calls <= 0)
                return new StatsSnapshot(moduleId, 0, 0, 0, 0, false);

            double tickToUs = 1000000.0 / Stopwatch.Frequency;
            return new StatsSnapshot(moduleId, stats.Calls, (stats.TotalTicks / (double)stats.Calls) * tickToUs, stats.WorstTicks * tickToUs, stats.ActiveCount, true);
        }
    }

    public static void Reset()
    {
        lock (s_lock) s_stats.Clear();
        System.Threading.Interlocked.Exchange(ref s_sampleChecks, 0L);
        System.Threading.Interlocked.Exchange(ref s_sampleSelections, 0L);
    }

    public static string GetCostReport(int maxRows)
    {
        RebirthModuleManifest[] modules = RebirthModuleRegistry.GetModulesSnapshot();
        List<StatsSnapshot> rows = new List<StatsSnapshot>(modules.Length);
        HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < modules.Length; i++) { known.Add(modules[i].Id); rows.Add(GetSnapshot(modules[i].Id)); }
        lock (s_lock)
        {
            foreach (string observedId in s_stats.Keys) if (!known.Contains(observedId)) rows.Add(GetSnapshot(observedId));
        }

        rows.Sort((a, b) => { int c=b.Measured.CompareTo(a.Measured); return c!=0?c:b.AvgMicros.CompareTo(a.AvgMicros); });
        if (maxRows <= 0 || maxRows > rows.Count) maxRows = rows.Count;

        StringBuilder sb = new StringBuilder(4096);
        long checks=System.Threading.Interlocked.Read(ref s_sampleChecks), selections=System.Threading.Interlocked.Read(ref s_sampleSelections);
        sb.AppendLine("[RebirthCost] sampled/module-attribution report; values are microseconds per sampled invocation, not per-frame costs.");
        sb.Append("coverage sampleChecks=").Append(checks).Append(" selected=").Append(selections).Append(" sampleMask=").Append(SampleMask).AppendLine("; modules without samples are NotMeasured.");
        sb.AppendLine("module | status | avgUsPerSample | worstUsPerSample | sampledCalls | activeLast");
        for (int i = 0; i < maxRows; i++)
        {
            StatsSnapshot s = rows[i];
            sb.Append(s.ModuleId).Append(" | ").Append(s.Measured?"Measured":"NotMeasured").Append(" | ")
              .Append(s.Measured?s.AvgMicros.ToString("0.##"):"-").Append(" | ")
              .Append(s.Measured?s.WorstMicros.ToString("0.##"):"-").Append(" | ")
              .Append(s.Calls).Append(" | ")
              .AppendLine(s.Measured?s.ActiveCount.ToString():"-");
        }
        return sb.ToString();
    }
}
