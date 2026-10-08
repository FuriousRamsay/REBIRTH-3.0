using System;
using System.Text;

#nullable disable

public enum RebirthMigrationSliceKind
{
    Unknown,
    ReadOnlyRuntime,
    ConsoleCommand,
    DataCache,
    DisplayOnly,
    LowRiskGameplay,
    StatefulGameplay,
    HotPathGameplay,
    BlockedManualReview
}

public enum RebirthMigrationSliceDecision
{
    Unknown,
    Candidate,
    GoodFirstSlice,
    NeedsMoreContract,
    NeedsTestsFirst,
    NeedsAuthorityPersistenceFirst,
    NeedsHarmonyPolicyApplied,
    Blocked,
    DoNotStartYet
}

public enum RebirthMigrationSliceRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthMigrationFirstSliceDecl
{
    public string SliceId;
    public string DomainId;
    public RebirthMigrationSliceKind SliceKind;
    public RebirthMigrationSliceDecision Decision;
    public RebirthMigrationSliceRisk Risk;
    public string WhyCandidate;
    public string WhyNotYet;
    public string RequiredBeforeStart;
    public string FirstImplementationShape;
    public string TestGate;
    public string Notes;
}

/// <summary>
/// Read-only migration readiness and first-slice selection registry.
/// This does not migrate gameplay. It identifies safe starting candidates.
/// </summary>
public static class RebirthMigrationReadinessRegistry
{
    private static readonly RebirthMigrationFirstSliceDecl[] s_slices = new[]
    {
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.001.runtime.policy.cache.real",
            DomainId = "scenario.core",
            SliceKind = RebirthMigrationSliceKind.DataCache,
            Decision = RebirthMigrationSliceDecision.GoodFirstSlice,
            Risk = RebirthMigrationSliceRisk.Low,
            WhyCandidate = "It can be implemented as cached flags/data without gameplay behavior.",
            WhyNotYet = "Must not be read by hot gameplay until tests exist.",
            RequiredBeforeStart = "compile/load and command smoke test",
            FirstImplementationShape = "Create real cache holder populated from explicit defaults only; no XML parsing, no hot-path use.",
            TestGate = "commands show cache state; no gameplay changes",
            Notes = "Good bridge from planning to runtime infrastructure."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.002.debug.gate.helpers",
            DomainId = "debug.diagnostics",
            SliceKind = RebirthMigrationSliceKind.ReadOnlyRuntime,
            Decision = RebirthMigrationSliceDecision.GoodFirstSlice,
            Risk = RebirthMigrationSliceRisk.Low,
            WhyCandidate = "It enforces future debug gating without touching gameplay.",
            WhyNotYet = "Must not add lazy lambdas that capture ref/out/in params or allocate in hot paths.",
            RequiredBeforeStart = "Harmony/reflection policy accepted",
            FirstImplementationShape = "Add tiny static gate helpers/constants only; no Harmony patches, no logging calls inserted.",
            TestGate = "compile; verify helpers are inert unless called",
            Notes = "Supports performance discipline before feature migration."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.003.build.profile.symbols.dry",
            DomainId = "performance.harness",
            SliceKind = RebirthMigrationSliceKind.ConsoleCommand,
            Decision = RebirthMigrationSliceDecision.Candidate,
            Risk = RebirthMigrationSliceRisk.Low,
            WhyCandidate = "Build profile selection already exists as dry-run; can tighten reporting without runtime gameplay.",
            WhyNotYet = "Actual build scripts should not alter output until tested.",
            RequiredBeforeStart = "build verification gates",
            FirstImplementationShape = "Add dry-run report that compares intended Release/Profiling/Stripped symbols and exclusions.",
            TestGate = "command output only",
            Notes = "Prepares stripped/profiling comparison."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.004.xml.root.include.validation",
            DomainId = "xml.cleanup.reorganization",
            SliceKind = RebirthMigrationSliceKind.ConsoleCommand,
            Decision = RebirthMigrationSliceDecision.Candidate,
            Risk = RebirthMigrationSliceRisk.Low,
            WhyCandidate = "Can validate conventions without moving XML.",
            WhyNotYet = "Do not parse/rewrite XML at runtime in gameplay; validation command only.",
            RequiredBeforeStart = "XML cleanup plan",
            FirstImplementationShape = "Optional command to list expected root Config files and include convention; no XML movement.",
            TestGate = "command output only",
            Notes = "Physical XML split comes later."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.005.ui.cached.snapshot.shell",
            DomainId = "ui.hud.compass",
            SliceKind = RebirthMigrationSliceKind.DisplayOnly,
            Decision = RebirthMigrationSliceDecision.NeedsTestsFirst,
            Risk = RebirthMigrationSliceRisk.Medium,
            WhyCandidate = "Display-only state can be low risk if it does not calculate gameplay truth.",
            WhyNotYet = "UI update tests and display snapshot contract must be specific.",
            RequiredBeforeStart = "UI idle/compass test matrix and no-scan rule",
            FirstImplementationShape = "Create display snapshot structs only; no XUi patches yet.",
            TestGate = "no UI loop integration until specific tests exist",
            Notes = "Potential early migration after read-only runtime slices."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.006.harvest.salvage.small",
            DomainId = "harvest.salvage",
            SliceKind = RebirthMigrationSliceKind.LowRiskGameplay,
            Decision = RebirthMigrationSliceDecision.NeedsTestsFirst,
            Risk = RebirthMigrationSliceRisk.Medium,
            WhyCandidate = "Lower-risk gameplay compared to crawl/AI/render/ItemValue.",
            WhyNotYet = "Still needs server parity and double-apply tests.",
            RequiredBeforeStart = "harvest/salvage test matrix and patch target review",
            FirstImplementationShape = "One isolated behavior with explicit owner and no diagnostics in hot path.",
            TestGate = "wild/grown, per-block, vehicle stump, SP/dedi",
            Notes = "Possible first real gameplay feature if tests are ready."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.007.audio.small",
            DomainId = "audio.sounds",
            SliceKind = RebirthMigrationSliceKind.LowRiskGameplay,
            Decision = RebirthMigrationSliceDecision.NeedsTestsFirst,
            Risk = RebirthMigrationSliceRisk.Medium,
            WhyCandidate = "Small audio fixes may be isolated.",
            WhyNotYet = "Vehicle/NPC sound sync can interact with network/AI timing.",
            RequiredBeforeStart = "audio sync test matrix and authority review",
            FirstImplementationShape = "One narrowly scoped audio behavior only; no vehicle damage/AI coupling.",
            TestGate = "no double sound/no missing sound/SP/dedi",
            Notes = "Choose only if target is cold/warm, not broad hot loop."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.008.paint.render",
            DomainId = "paint.render",
            SliceKind = RebirthMigrationSliceKind.HotPathGameplay,
            Decision = RebirthMigrationSliceDecision.DoNotStartYet,
            Risk = RebirthMigrationSliceRisk.Critical,
            WhyCandidate = "Important feature, but not first.",
            WhyNotYet = "renderFace is critical hot path and needs perf harness first.",
            RequiredBeforeStart = "hot-path tests, Harmony strategy applied, persistence contract",
            FirstImplementationShape = "Not first slice.",
            TestGate = "render hot path, chunk reload, paint persistence",
            Notes = "Delay until after simpler migration succeeds."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.009.itemvalue.stats",
            DomainId = "items.stats.randomization",
            SliceKind = RebirthMigrationSliceKind.HotPathGameplay,
            Decision = RebirthMigrationSliceDecision.DoNotStartYet,
            Risk = RebirthMigrationSliceRisk.Critical,
            WhyCandidate = "Central to armor/vehicle parts, but high risk.",
            WhyNotYet = "ItemValue.ModifyValue is critical hot path and prior patches hurt performance.",
            RequiredBeforeStart = "item roll persistence tests, no-allocation proof, hot patch strategy",
            FirstImplementationShape = "Not first slice.",
            TestGate = "quality rolls, tooltip/server sync, no allocations",
            Notes = "Do not begin real migration here."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.010.player.crawl",
            DomainId = "player.crawl",
            SliceKind = RebirthMigrationSliceKind.HotPathGameplay,
            Decision = RebirthMigrationSliceDecision.DoNotStartYet,
            Risk = RebirthMigrationSliceRisk.Critical,
            WhyCandidate = "Important but regression-prone.",
            WhyNotYet = "Known edge cases and PlayerMoveController hot path make it a bad first migration slice.",
            RequiredBeforeStart = "crawl-specific test matrix, movement patch strategy, persistence/vehicle/water tests",
            FirstImplementationShape = "Not first slice.",
            TestGate = "thin plates/windows/corners/vehicle/water/reload/third-person/dedi",
            Notes = "Avoid early behavior churn."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.011.zombie.ai",
            DomainId = "zombie.ai.special",
            SliceKind = RebirthMigrationSliceKind.HotPathGameplay,
            Decision = RebirthMigrationSliceDecision.DoNotStartYet,
            Risk = RebirthMigrationSliceRisk.Critical,
            WhyCandidate = "Important but too broad.",
            WhyNotYet = "AI/target/pathing/vehicle/spider/decoy are high-risk and server-sensitive.",
            RequiredBeforeStart = "split into smaller policies, hot-path strategy, SP/dedi tests",
            FirstImplementationShape = "Not first slice.",
            TestGate = "vehicle, decoy, spider, block jump, horde night",
            Notes = "Requires sub-slicing."
        },
        new RebirthMigrationFirstSliceDecl
        {
            SliceId = "slice.012.survivor",
            DomainId = "survivor scenario-related features",
            SliceKind = RebirthMigrationSliceKind.BlockedManualReview,
            Decision = RebirthMigrationSliceDecision.Blocked,
            Risk = RebirthMigrationSliceRisk.Blocked,
            WhyCandidate = "Not a candidate.",
            WhyNotYet = "survivor token is ambiguous across scenario and NPC/entity role concepts.",
            RequiredBeforeStart = "manual survivor disambiguation",
            FirstImplementationShape = "No migration by keyword.",
            TestGate = "classification report",
            Notes = "Blocked."
        }
    };

    public static string GetSummaryReport()
    {
        int good = 0;
        int candidate = 0;
        int blocked = 0;
        int doNotStart = 0;

        for (int i = 0; i < s_slices.Length; i++)
        {
            RebirthMigrationFirstSliceDecl s = s_slices[i];

            if (s.Decision == RebirthMigrationSliceDecision.GoodFirstSlice)
                good++;
            if (s.Decision == RebirthMigrationSliceDecision.Candidate)
                candidate++;
            if (s.Decision == RebirthMigrationSliceDecision.Blocked)
                blocked++;
            if (s.Decision == RebirthMigrationSliceDecision.DoNotStartYet)
                doNotStart++;
        }

        return "[RebirthMigrationReady] Slices: " + s_slices.Length
            + "; good first slices: " + good
            + "; candidates: " + candidate
            + "; do-not-start-yet: " + doNotStart
            + "; blocked: " + blocked
            + ". Registry is read-only.";
    }

    public static string GetSliceReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(32768);
        sb.AppendLine("[RebirthMigrationReady] migration readiness and first-slice selection.");
        sb.AppendLine("  Read-only. This does not migrate gameplay.");
        sb.AppendLine("id | domain | kind | decision | risk | why candidate | why not yet | required before start | implementation shape | test gate | notes");

        bool any = false;
        for (int i = 0; i < s_slices.Length; i++)
        {
            RebirthMigrationFirstSliceDecl s = s_slices[i];
            if (!Matches(s, f))
                continue;

            any = true;
            sb.Append(s.SliceId).Append(" | ")
              .Append(s.DomainId).Append(" | ")
              .Append(s.SliceKind).Append(" | ")
              .Append(s.Decision).Append(" | ")
              .Append(s.Risk).Append(" | ")
              .Append(s.WhyCandidate).Append(" | ")
              .Append(s.WhyNotYet).Append(" | ")
              .Append(s.RequiredBeforeStart).Append(" | ")
              .Append(s.FirstImplementationShape).Append(" | ")
              .Append(s.TestGate).Append(" | ")
              .AppendLine(s.Notes);
        }

        if (!any)
            sb.AppendLine("No migration readiness slice matched the filter.");

        return sb.ToString();
    }

    public static string GetRecommendedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthMigrationReady] recommended first real steps:");
        sb.AppendLine("  1. Implement real scenario runtime policy cache shell using explicit defaults only.");
        sb.AppendLine("  2. Implement debug gate helper shell with no Harmony patches and no gameplay calls.");
        sb.AppendLine("  3. Optionally improve dry-run build/profile comparison reporting.");
        sb.AppendLine("  4. Only after that, choose a small low-risk gameplay slice with tests.");
        sb.AppendLine();
        sb.AppendLine("Avoid as first real migration:");
        sb.AppendLine("  - Player crawl");
        sb.AppendLine("  - ItemValue/stat randomization");
        sb.AppendLine("  - paint/renderFace");
        sb.AppendLine("  - zombie AI/pathing");
        sb.AppendLine("  - survivor scenario-related behavior");
        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthMigrationReady] blocked/do-not-start-yet slices.");
        sb.AppendLine("id | domain | decision | risk | why not yet | notes");

        for (int i = 0; i < s_slices.Length; i++)
        {
            RebirthMigrationFirstSliceDecl s = s_slices[i];
            if (s.Decision != RebirthMigrationSliceDecision.Blocked
                && s.Decision != RebirthMigrationSliceDecision.DoNotStartYet)
                continue;

            sb.Append(s.SliceId).Append(" | ")
              .Append(s.DomainId).Append(" | ")
              .Append(s.Decision).Append(" | ")
              .Append(s.Risk).Append(" | ")
              .Append(s.WhyNotYet).Append(" | ")
              .AppendLine(s.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthMigrationReady] safety rules:");
        sb.AppendLine("  1. Registry is read-only.");
        sb.AppendLine("  2. No gameplay behavior is connected.");
        sb.AppendLine("  3. No Harmony patches are installed.");
        sb.AppendLine("  4. No reflection is performed.");
        sb.AppendLine("  5. Good first slices must be inert or read-only.");
        sb.AppendLine("  6. Hot-path features are explicitly not first slices.");
        sb.AppendLine("  7. Stateful gameplay waits for network/persistence tests.");
        sb.AppendLine("  8. Survivor scenario remains blocked until disambiguation.");
        return sb.ToString();
    }

    private static bool Matches(RebirthMigrationFirstSliceDecl s, string filter)
    {
        if (s == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (s.SliceId != null && s.SliceId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.DomainId != null && s.DomainId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.WhyCandidate != null && s.WhyCandidate.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.WhyNotYet != null && s.WhyNotYet.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.RequiredBeforeStart != null && s.RequiredBeforeStart.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.FirstImplementationShape != null && s.FirstImplementationShape.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.TestGate != null && s.TestGate.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.SliceKind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.Decision.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
