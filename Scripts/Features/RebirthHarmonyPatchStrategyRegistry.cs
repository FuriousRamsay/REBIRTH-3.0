using System;
using System.Text;

#nullable disable

public enum RebirthHarmonyPatchKind
{
    Unknown,
    Prefix,
    Postfix,
    Transpiler,
    Finalizer,
    ReversePatch,
    ReplacementPrefixSkipsOriginal,
    NoPatchPreferred,
    ManualReviewRequired
}

public enum RebirthHarmonyTargetHeat
{
    Unknown,
    Cold,
    Warm,
    Hot,
    CriticalHotPath
}

public enum RebirthReflectionPolicyKind
{
    Unknown,
    NotAllowed,
    AllowedInitOnlyCached,
    AllowedColdPathCached,
    ManualReviewRequired,
    MustAvoid
}

public enum RebirthPatchFailurePolicy
{
    Unknown,
    FailLoudDisableFeature,
    FailLoudAbortPatch,
    SoftDisableOptionalFeature,
    CompatibilityWarningOnly,
    ManualReviewRequired
}

public enum RebirthPatchStrategyRisk
{
    Unknown,
    Low,
    Medium,
    High,
    Critical,
    Blocked
}

public sealed class RebirthHarmonyPatchStrategyDecl
{
    public string StrategyId;
    public string TargetOrArea;
    public string OwnerModule;
    public RebirthHarmonyPatchKind PreferredPatchKind;
    public RebirthHarmonyTargetHeat TargetHeat;
    public RebirthReflectionPolicyKind ReflectionPolicy;
    public RebirthPatchFailurePolicy FailurePolicy;
    public RebirthPatchStrategyRisk Risk;
    public string Allowed;
    public string Forbidden;
    public string TestGate;
    public string Notes;
}

/// <summary>
/// Read-only Harmony patch strategy and reflection policy registry.
/// This does not install patches, run Harmony, or perform reflection.
/// </summary>
public static class RebirthHarmonyPatchStrategyRegistry
{
    private static readonly RebirthHarmonyPatchStrategyDecl[] s_strategies = new[]
    {
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.rule.default",
            TargetOrArea = "default patch policy",
            OwnerModule = "patching.core",
            PreferredPatchKind = RebirthHarmonyPatchKind.NoPatchPreferred,
            TargetHeat = RebirthHarmonyTargetHeat.Unknown,
            ReflectionPolicy = RebirthReflectionPolicyKind.MustAvoid,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.High,
            Allowed = "Patch only when a module owns the target and has tests.",
            Forbidden = "Unaudited production patch classes, hidden diagnostics, unowned hot patches, silent reflection failure.",
            TestGate = "owner, hot-path risk, authority, compatibility, and failure behavior documented",
            Notes = "No patch is the default until ownership and tests exist."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.rule.diagnostics",
            TargetOrArea = "diagnostic/profiling wrappers",
            OwnerModule = "diagnostics",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.Hot,
            ReflectionPolicy = RebirthReflectionPolicyKind.MustAvoid,
            FailurePolicy = RebirthPatchFailurePolicy.SoftDisableOptionalFeature,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Profiling/dev profile explicit registration only.",
            Forbidden = "Release PatchAll of diagnostic wrappers; object[] __args; debug string construction before gate.",
            TestGate = "release/profiling/stripped comparison",
            Notes = "Addresses prior always-patched diagnostic Harmony wrapper risk."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.rule.object_args",
            TargetOrArea = "object[] __args usage",
            OwnerModule = "patching.core",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.CriticalHotPath,
            ReflectionPolicy = RebirthReflectionPolicyKind.NotAllowed,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudAbortPatch,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Cold/manual diagnostics only after review.",
            Forbidden = "object[] __args in any hot/critical hot patch.",
            TestGate = "build verification hot patch scan",
            Notes = "Harmony may need to build argument arrays each call."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.rule.reflection",
            TargetOrArea = "reflection access",
            OwnerModule = "reflection.accessors",
            PreferredPatchKind = RebirthHarmonyPatchKind.NoPatchPreferred,
            TargetHeat = RebirthHarmonyTargetHeat.Hot,
            ReflectionPolicy = RebirthReflectionPolicyKind.AllowedInitOnlyCached,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Resolve once, cache once, validate once, expose IsAvailable, fail loudly, safe fallback.",
            Forbidden = "GetField/GetMethod/Invoke/GetValue/SetValue lookup per hot-path call; silent fallback.",
            TestGate = "missing member simulation and hot-path no-reflection gate",
            Notes = "Reflection is not banned globally, but runtime hot-path reflection is."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.rule.prefix",
            TargetOrArea = "simple Prefix",
            OwnerModule = "patching.core",
            PreferredPatchKind = RebirthHarmonyPatchKind.Prefix,
            TargetHeat = RebirthHarmonyTargetHeat.Warm,
            ReflectionPolicy = RebirthReflectionPolicyKind.MustAvoid,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.Medium,
            Allowed = "Small guards, cached flag checks, typed parameters, no allocation.",
            Forbidden = "LINQ, string building, reflection lookup, broad scans, object[] args.",
            TestGate = "method-specific behavior and perf test",
            Notes = "Simple prefixes are acceptable when target heat and ownership allow."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.rule.postfix",
            TargetOrArea = "simple Postfix",
            OwnerModule = "patching.core",
            PreferredPatchKind = RebirthHarmonyPatchKind.Postfix,
            TargetHeat = RebirthHarmonyTargetHeat.Warm,
            ReflectionPolicy = RebirthReflectionPolicyKind.MustAvoid,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.Medium,
            Allowed = "Small post-observation or cached state update after original.",
            Forbidden = "object[] args on hot targets, expensive post-processing, hidden logging.",
            TestGate = "method-specific behavior and perf test",
            Notes = "Postfixes still run every target invocation."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.rule.transpiler",
            TargetOrArea = "Transpiler",
            OwnerModule = "patching.core",
            PreferredPatchKind = RebirthHarmonyPatchKind.Transpiler,
            TargetHeat = RebirthHarmonyTargetHeat.Hot,
            ReflectionPolicy = RebirthReflectionPolicyKind.ManualReviewRequired,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudAbortPatch,
            Risk = RebirthPatchStrategyRisk.High,
            Allowed = "Only when it reduces runtime overhead or avoids broader wrapper cost; document injected IL location and fallback.",
            Forbidden = "Default use, complex helper calls in loops, untested IL assumptions, silent failure.",
            TestGate = "IL match test, runtime behavior test, compatibility failure path",
            Notes = "Patch-time cost is less important than injected runtime cost."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.rule.skip_original",
            TargetOrArea = "Prefix return false / replacement behavior",
            OwnerModule = "patching.core",
            PreferredPatchKind = RebirthHarmonyPatchKind.ReplacementPrefixSkipsOriginal,
            TargetHeat = RebirthHarmonyTargetHeat.Hot,
            ReflectionPolicy = RebirthReflectionPolicyKind.ManualReviewRequired,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudAbortPatch,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Only when replacement ownership is explicit and vanilla side effects are mapped.",
            Forbidden = "Convenience overrides, partial side-effect preservation, untested network/server drift.",
            TestGate = "side-effect map, compatibility test, server/client parity test",
            Notes = "High bar because vanilla and other mod behavior is bypassed."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.target.player_move",
            TargetOrArea = "PlayerMoveController.Update / player movement flow",
            OwnerModule = "player.movement",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.CriticalHotPath,
            ReflectionPolicy = RebirthReflectionPolicyKind.NotAllowed,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Cached flags, fast exits, exact typed parameters, no diagnostics in release.",
            Forbidden = "Reflection, object[] args, string/log construction, scenario scans, broad replacement.",
            TestGate = "crawl/vehicle/water/third-person/reload/dedi matrix",
            Notes = "Known crawl/camera regression surface."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.target.damage",
            TargetOrArea = "EntityAlive.DamageEntity / combat damage",
            OwnerModule = "combat.damage",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.CriticalHotPath,
            ReflectionPolicy = RebirthReflectionPolicyKind.NotAllowed,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Single owner, cached policy, typed args, server-authoritative calculations.",
            Forbidden = "Scenario-owned damage patches, reflection, object[] args, repeated item/buff scans.",
            TestGate = "SP/dedi/P2P damage matrix",
            Notes = "Damage must remain server-authoritative."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.target.itemvalue",
            TargetOrArea = "ItemValue.ModifyValue",
            OwnerModule = "items.stats",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.CriticalHotPath,
            ReflectionPolicy = RebirthReflectionPolicyKind.NotAllowed,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Cached stat rules and persisted rolled values.",
            Forbidden = "LINQ, broad armor scans, reflection, string/log construction, object[] args.",
            TestGate = "quality rolls, armor stats, tooltip, server sync, no allocations",
            Notes = "Prior item/stat patches affected performance."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.target.renderface",
            TargetOrArea = "BlockShapeNew.renderFace",
            OwnerModule = "paint.render",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.CriticalHotPath,
            ReflectionPolicy = RebirthReflectionPolicyKind.NotAllowed,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Single paint/render owner, cached block flags, no per-face allocation.",
            Forbidden = "Debug logging, string/block-name classification per face, reflection, object[] args.",
            TestGate = "paint persistence, chunk reload, render perf",
            Notes = "Render path must be extremely small."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.target.ai",
            TargetOrArea = "EntityMoveHelper / EntityAlive.FindPath / zombie AI loops",
            OwnerModule = "ai.zombie/pathing",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.CriticalHotPath,
            ReflectionPolicy = RebirthReflectionPolicyKind.NotAllowed,
            FailurePolicy = RebirthPatchFailurePolicy.FailLoudDisableFeature,
            Risk = RebirthPatchStrategyRisk.Critical,
            Allowed = "Policy-driven small checks, server-only truth, cached route/path flags.",
            Forbidden = "client-owned target truth, reflection, debug scans, object[] args, pathing replacement without side-effect map.",
            TestGate = "vehicle, decoy, spider, block jump, horde night, dedi",
            Notes = "AI migration must split into smaller policies."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.target.ui",
            TargetOrArea = "XUi/HUD/Compass update loops",
            OwnerModule = "ui.hud",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.Hot,
            ReflectionPolicy = RebirthReflectionPolicyKind.MustAvoid,
            FailurePolicy = RebirthPatchFailurePolicy.SoftDisableOptionalFeature,
            Risk = RebirthPatchStrategyRisk.High,
            Allowed = "Cached display snapshot consumption.",
            Forbidden = "POI/scenario/progress scans, network truth, logging construction, runtime XML parsing.",
            TestGate = "idle HUD/compass and purge display matrix",
            Notes = "UI is display only."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.external.astar",
            TargetOrArea = "A* internals / path smoothing internals",
            OwnerModule = "external.compatibility",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.CriticalHotPath,
            ReflectionPolicy = RebirthReflectionPolicyKind.ManualReviewRequired,
            FailurePolicy = RebirthPatchFailurePolicy.ManualReviewRequired,
            Risk = RebirthPatchStrategyRisk.Blocked,
            Allowed = "Manual compatibility review only.",
            Forbidden = "Absorb external A* transpilers/reverse patches into core by default.",
            TestGate = "external compatibility audit",
            Notes = "External pathing patches are not core migration defaults."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.external.gui_drawline",
            TargetOrArea = "GUIUtils.DrawLine",
            OwnerModule = "external.compatibility/ui",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.Hot,
            ReflectionPolicy = RebirthReflectionPolicyKind.ManualReviewRequired,
            FailurePolicy = RebirthPatchFailurePolicy.ManualReviewRequired,
            Risk = RebirthPatchStrategyRisk.Blocked,
            Allowed = "Manual review only if crosshair/UI ownership requires it.",
            Forbidden = "Global DrawLine patch by default.",
            TestGate = "UI/crosshair compatibility audit",
            Notes = "Do not absorb external broad UI patches into core."
        },
        new RebirthHarmonyPatchStrategyDecl
        {
            StrategyId = "harmony.external.entity_position",
            TargetOrArea = "Entity.OnUpdatePosition",
            OwnerModule = "external.compatibility/entities",
            PreferredPatchKind = RebirthHarmonyPatchKind.ManualReviewRequired,
            TargetHeat = RebirthHarmonyTargetHeat.CriticalHotPath,
            ReflectionPolicy = RebirthReflectionPolicyKind.ManualReviewRequired,
            FailurePolicy = RebirthPatchFailurePolicy.ManualReviewRequired,
            Risk = RebirthPatchStrategyRisk.Blocked,
            Allowed = "Manual review only for specific entity ownership need.",
            Forbidden = "Global Entity.OnUpdatePosition patch by default.",
            TestGate = "entity/drone/follower compatibility audit",
            Notes = "Too broad for default fresh migration."
        }
    };

    public static string GetSummaryReport()
    {
        int critical = 0;
        int blocked = 0;
        int criticalHot = 0;
        int reflectionBlocked = 0;

        for (int i = 0; i < s_strategies.Length; i++)
        {
            RebirthHarmonyPatchStrategyDecl s = s_strategies[i];

            if (s.Risk == RebirthPatchStrategyRisk.Critical)
                critical++;
            if (s.Risk == RebirthPatchStrategyRisk.Blocked)
                blocked++;
            if (s.TargetHeat == RebirthHarmonyTargetHeat.CriticalHotPath)
                criticalHot++;
            if (s.ReflectionPolicy == RebirthReflectionPolicyKind.NotAllowed
                || s.ReflectionPolicy == RebirthReflectionPolicyKind.MustAvoid)
                reflectionBlocked++;
        }

        return "[RebirthHarmonyStrategy] Strategies: " + s_strategies.Length
            + "; critical: " + critical
            + "; blocked: " + blocked
            + "; critical-hot targets: " + criticalHot
            + "; reflection not allowed/must avoid: " + reflectionBlocked
            + ". Registry is read-only.";
    }

    public static string GetStrategyReport(string filter)
    {
        string f = (filter ?? string.Empty).Trim();

        StringBuilder sb = new StringBuilder(32768);
        sb.AppendLine("[RebirthHarmonyStrategy] Harmony patch strategy and reflection policy.");
        sb.AppendLine("  Read-only. This does not install Harmony patches or perform reflection.");
        sb.AppendLine("id | target/area | owner | patch kind | heat | reflection | failure | risk | allowed | forbidden | test gate | notes");

        bool any = false;
        for (int i = 0; i < s_strategies.Length; i++)
        {
            RebirthHarmonyPatchStrategyDecl s = s_strategies[i];
            if (!Matches(s, f))
                continue;

            any = true;
            sb.Append(s.StrategyId).Append(" | ")
              .Append(s.TargetOrArea).Append(" | ")
              .Append(s.OwnerModule).Append(" | ")
              .Append(s.PreferredPatchKind).Append(" | ")
              .Append(s.TargetHeat).Append(" | ")
              .Append(s.ReflectionPolicy).Append(" | ")
              .Append(s.FailurePolicy).Append(" | ")
              .Append(s.Risk).Append(" | ")
              .Append(s.Allowed).Append(" | ")
              .Append(s.Forbidden).Append(" | ")
              .Append(s.TestGate).Append(" | ")
              .AppendLine(s.Notes);
        }

        if (!any)
            sb.AppendLine("No Harmony strategy matched the filter.");

        return sb.ToString();
    }

    public static string GetHotPathReport()
    {
        return GetStrategyReport("CriticalHotPath");
    }

    public static string GetReflectionReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthHarmonyStrategy] reflection policy:");
        sb.AppendLine("  - Reflection lookup in hot paths is forbidden.");
        sb.AppendLine("  - MethodInfo.Invoke/FieldInfo.GetValue/SetValue per hot-path call is forbidden.");
        sb.AppendLine("  - Allowed reflection must resolve once, cache once, validate once, expose IsAvailable, fail loudly, and provide a safe fallback.");
        sb.AppendLine("  - Silent reflection failure is not acceptable.");
        sb.AppendLine();
        sb.AppendLine(GetStrategyReport("reflection"));
        return sb.ToString();
    }

    public static string GetPatchTypeReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthHarmonyStrategy] patch type rules:");
        sb.AppendLine("  Prefix: acceptable for small typed guards/cached flags when owner/test exists.");
        sb.AppendLine("  Postfix: acceptable for small post-observation/cached state updates when owner/test exists.");
        sb.AppendLine("  Transpiler: only when it reduces runtime work or avoids broader wrapper cost; document IL location and fallback.");
        sb.AppendLine("  Prefix return false / replacement: high bar; requires explicit replacement ownership and side-effect map.");
        sb.AppendLine("  No patch: default until ownership and tests exist.");
        sb.AppendLine();
        sb.AppendLine(GetStrategyReport("rule."));
        return sb.ToString();
    }

    public static string GetBlockedReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthHarmonyStrategy] blocked/manual-review patch areas.");
        sb.AppendLine("id | target/area | owner | reason | notes");

        for (int i = 0; i < s_strategies.Length; i++)
        {
            RebirthHarmonyPatchStrategyDecl s = s_strategies[i];
            if (s.Risk != RebirthPatchStrategyRisk.Blocked
                && s.PreferredPatchKind != RebirthHarmonyPatchKind.ManualReviewRequired)
                continue;

            sb.Append(s.StrategyId).Append(" | ")
              .Append(s.TargetOrArea).Append(" | ")
              .Append(s.OwnerModule).Append(" | ")
              .Append(s.Forbidden).Append(" | ")
              .AppendLine(s.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthHarmonyStrategy] safety rules:");
        sb.AppendLine("  1. Registry is read-only.");
        sb.AppendLine("  2. No Harmony patches are installed.");
        sb.AppendLine("  3. No reflection is performed.");
        sb.AppendLine("  4. Central production PatchAll excludes manual diagnostic/profiling patch classes.");
        sb.AppendLine("  5. No object[] __args on hot patches.");
        sb.AppendLine("  6. No reflection lookup or Invoke/GetValue/SetValue in hot paths.");
        sb.AppendLine("  7. No debug/log construction before debug gate.");
        sb.AppendLine("  8. No Prefix return false without explicit replacement ownership and side-effect map.");
        sb.AppendLine("  9. No transpiler without documented injected behavior and fallback.");
        sb.AppendLine("  10. No scenario-owned hot-method patches.");
        return sb.ToString();
    }

    private static bool Matches(RebirthHarmonyPatchStrategyDecl s, string filter)
    {
        if (s == null)
            return false;

        if (string.IsNullOrEmpty(filter))
            return true;

        if (s.StrategyId != null && s.StrategyId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.TargetOrArea != null && s.TargetOrArea.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.OwnerModule != null && s.OwnerModule.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.Allowed != null && s.Allowed.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.Forbidden != null && s.Forbidden.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.TestGate != null && s.TestGate.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.PreferredPatchKind.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.TargetHeat.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.ReflectionPolicy.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.FailurePolicy.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (s.Risk.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
