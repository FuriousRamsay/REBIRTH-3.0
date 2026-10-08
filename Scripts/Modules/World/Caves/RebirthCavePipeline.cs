using System;
using System.Text;

#nullable disable

public enum RebirthCaveTenantArea
{
    Unknown,
    CaveGeneration,
    CaveRuntimeState,
    CaveSpawnPolicy,
    CaveXmlIndex,
    CavePoiTagPolicy,
    CaveTunnelPolicy,
    WorldBuilderGenerateData,
    WorldGenerationUi,
    Diagnostics
}

public enum RebirthCaveTenantStatus
{
    Planned,
    ScaffoldedNoBehavior,
    RoutedNoBehaviorChange,
    BehaviorMigrated,
    Disabled,
    ManualReviewRequired,
    SevereDrift
}

public enum RebirthCaveRule
{
    Unknown,
    DoNotMergeTheDescentMonolith,
    SeparateTunnelAndPoiChecks,
    UsePoiTagsForPoiCaves,
    WorldBuilderGenerateDataSevereDrift,
    NoWorldGenUiMechanicalPort,
    SpawnPolicySeparateFromGeneration,
    DiagnosticsCompileOut
}

public sealed class RebirthCaveTenantDecl
{
    public RebirthCaveTenantArea Area;
    public RebirthCaveTenantStatus Status;
    public string OwnerModuleId;
    public bool HotPath;
    public bool MayAllocate;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthCaveRuleDecl
{
    public RebirthCaveRule Rule;
    public string OwnerModuleId;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// No-behavior world/caves tenant scaffold.
/// This does not run cave generation, change spawn policy, parse cave XML, or patch world generation.
/// </summary>
public static class RebirthCavePipeline
{
    private static readonly RebirthCaveTenantDecl[] s_tenants = new[]
    {
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.CaveGeneration,
            Status = RebirthCaveTenantStatus.ManualReviewRequired,
            OwnerModuleId = "world.caves.generation",
            HotPath = false,
            MayAllocate = true,
            Evidence = "P3",
            Notes = "TheDescent contains real cave generation pipeline: generation, graph, Delaunay, prefab placement. Must not be absorbed as monolith."
        },
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.CaveRuntimeState,
            Status = RebirthCaveTenantStatus.Planned,
            OwnerModuleId = "world.caves.runtime",
            HotPath = true,
            MayAllocate = false,
            Evidence = "P3 / Blueprint v2",
            Notes = "Runtime cave/tunnel state should be indexed/cached and separate from generation code."
        },
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.CaveSpawnPolicy,
            Status = RebirthCaveTenantStatus.Planned,
            OwnerModuleId = "world.caves.spawns",
            HotPath = true,
            MayAllocate = false,
            Evidence = "P3 / prior cave spawn restrictions",
            Notes = "Cave spawn policies must distinguish cave tunnel runtime from cave POI tags."
        },
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.CaveXmlIndex,
            Status = RebirthCaveTenantStatus.Planned,
            OwnerModuleId = "world.caves.xml",
            HotPath = false,
            MayAllocate = false,
            Evidence = "P3 / XML ledger",
            Notes = "Cave XML should be indexed at load/reload, not read dynamically in spawn/update paths."
        },
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.CavePoiTagPolicy,
            Status = RebirthCaveTenantStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "world.caves.poi",
            HotPath = false,
            MayAllocate = false,
            Evidence = "prior explicit user directive",
            Notes = "Cave POI checks use TAG_POI_CAVE and TAG_POI_UNDERGROUND, not the cave tunnel helper."
        },
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.CaveTunnelPolicy,
            Status = RebirthCaveTenantStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "world.caves.tunnel",
            HotPath = true,
            MayAllocate = false,
            Evidence = "prior explicit user directive",
            Notes = "Tunnel helper remains tunnel-only because other code relies on that exact meaning."
        },
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.WorldBuilderGenerateData,
            Status = RebirthCaveTenantStatus.SevereDrift,
            OwnerModuleId = "world.caves.worldgen",
            HotPath = false,
            MayAllocate = true,
            Evidence = "P5A2",
            Notes = "WorldBuilder.GenerateData has severe 2.6->3.0 drift. Do not mechanically port 2.6 TheDescent hooks."
        },
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.WorldGenerationUi,
            Status = RebirthCaveTenantStatus.ManualReviewRequired,
            OwnerModuleId = "world.caves.ui",
            HotPath = false,
            MayAllocate = true,
            Evidence = "P5 / P6",
            Notes = "XUiC_WorldGenerationWindowGroup is effectively gutted in 3.0. UI migration must be reauthored."
        },
        new RebirthCaveTenantDecl
        {
            Area = RebirthCaveTenantArea.Diagnostics,
            Status = RebirthCaveTenantStatus.Planned,
            OwnerModuleId = "world.caves.diagnostics",
            HotPath = true,
            MayAllocate = false,
            Evidence = "debug policy",
            Notes = "Diagnostics must compile out or be gated before string/log construction."
        }
    };

    private static readonly RebirthCaveRuleDecl[] s_rules = new[]
    {
        new RebirthCaveRuleDecl
        {
            Rule = RebirthCaveRule.DoNotMergeTheDescentMonolith,
            OwnerModuleId = "world.caves",
            Evidence = "P3 / Blueprint v2",
            Notes = "Split into generation, runtime, spawn policy, XML index, and worldgen compatibility."
        },
        new RebirthCaveRuleDecl
        {
            Rule = RebirthCaveRule.SeparateTunnelAndPoiChecks,
            OwnerModuleId = "world.caves",
            Evidence = "prior user directive / Blueprint v2",
            Notes = "Cave tunnel helper must not be reused for cave POI checks."
        },
        new RebirthCaveRuleDecl
        {
            Rule = RebirthCaveRule.UsePoiTagsForPoiCaves,
            OwnerModuleId = "world.caves.poi",
            Evidence = "prior user directive",
            Notes = "Use TAG_POI_CAVE and TAG_POI_UNDERGROUND for cave POI logic."
        },
        new RebirthCaveRuleDecl
        {
            Rule = RebirthCaveRule.WorldBuilderGenerateDataSevereDrift,
            OwnerModuleId = "world.caves.worldgen",
            Evidence = "P5A2",
            Notes = "3.0 worldgen changes require reauthored adapter, not mechanical 2.6 patch carryover."
        },
        new RebirthCaveRuleDecl
        {
            Rule = RebirthCaveRule.NoWorldGenUiMechanicalPort,
            OwnerModuleId = "world.caves.ui",
            Evidence = "P5/P6",
            Notes = "3.0 XUi/worldgen UI model is different; do not port old UI directly."
        },
        new RebirthCaveRuleDecl
        {
            Rule = RebirthCaveRule.SpawnPolicySeparateFromGeneration,
            OwnerModuleId = "world.caves.spawns",
            Evidence = "Blueprint v2",
            Notes = "Spawn restrictions can use runtime/indexed cave state without owning generation internals."
        },
        new RebirthCaveRuleDecl
        {
            Rule = RebirthCaveRule.DiagnosticsCompileOut,
            OwnerModuleId = "world.caves.diagnostics",
            Evidence = "debug policy",
            Notes = "No cave spawn/worldgen diagnostics string construction in release hot paths."
        }
    };

    public static string GetTenantReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthCaves] no-behavior world/caves tenant scaffold.");
        sb.AppendLine("  No worldgen patch is installed in this phase.");
        sb.AppendLine("  No cave generation/runtime/spawn behavior is changed in this phase.");
        sb.AppendLine("area | status | owner | hot | alloc | evidence | notes");

        for (int i = 0; i < s_tenants.Length; i++)
        {
            RebirthCaveTenantDecl t = s_tenants[i];
            sb.Append(t.Area).Append(" | ")
              .Append(t.Status).Append(" | ")
              .Append(t.OwnerModuleId).Append(" | ")
              .Append(t.HotPath).Append(" | ")
              .Append(t.MayAllocate).Append(" | ")
              .Append(t.Evidence).Append(" | ")
              .AppendLine(t.Notes);
        }

        return sb.ToString();
    }

    public static string GetRuleReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthCaves] cave/worldgen safety rules.");
        sb.AppendLine("rule | owner | evidence | notes");

        for (int i = 0; i < s_rules.Length; i++)
        {
            RebirthCaveRuleDecl r = s_rules[i];
            sb.Append(r.Rule).Append(" | ")
              .Append(r.OwnerModuleId).Append(" | ")
              .Append(r.Evidence).Append(" | ")
              .AppendLine(r.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthCaves] safety rules:");
        sb.AppendLine("  1. Do not merge TheDescent as a monolith.");
        sb.AppendLine("  2. Keep cave tunnel helper tunnel-only.");
        sb.AppendLine("  3. Use POI tags for cave POI checks.");
        sb.AppendLine("  4. Do not mechanically port WorldBuilder.GenerateData hooks to 3.0.");
        sb.AppendLine("  5. Do not mechanically port 2.6 worldgen XUi to 3.0.");
        sb.AppendLine("  6. Cave spawn policy is separate from cave generation.");
        sb.AppendLine("  7. Diagnostics must compile out or be bool-gated before construction.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthCaves] Cave/worldgen tenant entries: " + s_tenants.Length
            + "; safety rules: " + s_rules.Length
            + ". This phase is no-behavior and installs no Harmony/worldgen patches.";
    }
}
