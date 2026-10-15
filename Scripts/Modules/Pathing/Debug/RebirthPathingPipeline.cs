#if DEBUG
using System;
using System.Text;

#nullable disable

public enum RebirthPathingTenantArea
{
    Unknown,
    VanillaEntityMoveHelper,
    VanillaFindPath,
    LocalRouteGuard,
    NonDropRouteSearch,
    AStarInternalRisk,
    ZombieAiDependency,
    Diagnostics
}

public enum RebirthPathingTenantStatus
{
    Planned,
    ScaffoldedNoBehavior,
    RoutedNoBehaviorChange,
    BehaviorMigrated,
    Disabled,
    ManualReviewRequired,
    ExcludedByDefault
}

public enum RebirthAStarRiskDecision
{
    Unknown,
    ExcludeByDefault,
    KeepVendoredAndGoverned,
    ManualReviewRequired,
    ReimplementBehaviorWithoutInternalPatch,
    AbsorbNative
}

public sealed class RebirthPathingTenantDecl
{
    public RebirthPathingTenantArea Area;
    public RebirthPathingTenantStatus Status;
    public string OwnerModuleId;
    public bool HotPath;
    public bool MayAllocate;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthLocalRouteBudgetDecl
{
    public string BudgetId;
    public int PreferredBuildsPerFrame;
    public string OwnerModuleId;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthAStarRiskDecl
{
    public string PatchTarget;
    public RebirthAStarRiskDecision Decision;
    public string OwnerModuleId;
    public string Evidence;
    public string Notes;
}

/// <summary>
/// No-behavior pathing tenant scaffold.
/// This does not patch EntityMoveHelper, EntityAlive.FindPath, or A* internals.
/// </summary>
public static class RebirthPathingPipeline
{
    private static readonly RebirthPathingTenantDecl[] s_tenants = new[]
    {
        new RebirthPathingTenantDecl
        {
            Area = RebirthPathingTenantArea.VanillaEntityMoveHelper,
            Status = RebirthPathingTenantStatus.BehaviorMigrated,
            OwnerModuleId = "pathing.vanilla",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / P2 / P4 / P5 / V8",
            Notes = "Owns the bounded hybrid shortcut postfix on EntityMoveHelper.UpdateMoveHelper; native path remains authoritative."
        },
        new RebirthPathingTenantDecl
        {
            Area = RebirthPathingTenantArea.VanillaFindPath,
            Status = RebirthPathingTenantStatus.Planned,
            OwnerModuleId = "pathing.vanilla",
            HotPath = true,
            MayAllocate = false,
            Evidence = "P4",
            Notes = "PathSmoothing patches EntityAlive.FindPath; future owner must be explicit."
        },
        new RebirthPathingTenantDecl
        {
            Area = RebirthPathingTenantArea.LocalRouteGuard,
            Status = RebirthPathingTenantStatus.Planned,
            OwnerModuleId = "pathing.localRoutes",
            HotPath = true,
            MayAllocate = false,
            Evidence = "P2.5",
            Notes = "ZombieVerticalDropObstaclePathGuard is bounded local route search, not general pathfinder."
        },
        new RebirthPathingTenantDecl
        {
            Area = RebirthPathingTenantArea.NonDropRouteSearch,
            Status = RebirthPathingTenantStatus.Planned,
            OwnerModuleId = "pathing.localRoutes",
            HotPath = true,
            MayAllocate = false,
            Evidence = "P2.5 / CustomZombieNonDropRouteMaxExtraBlocks",
            Notes = "Non-drop route search belongs under central scheduler/budget, not self-owned ad hoc budget only."
        },
        new RebirthPathingTenantDecl
        {
            Area = RebirthPathingTenantArea.AStarInternalRisk,
            Status = RebirthPathingTenantStatus.ExcludedByDefault,
            OwnerModuleId = "pathing.astarRisk",
            HotPath = true,
            MayAllocate = false,
            Evidence = "P4",
            Notes = "A* internals transpilers/reverse patches are excluded by default unless explicitly approved."
        },
        new RebirthPathingTenantDecl
        {
            Area = RebirthPathingTenantArea.ZombieAiDependency,
            Status = RebirthPathingTenantStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "zombie.ai",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / V8",
            Notes = "ZombieAI depends on Pathing. Pathing must not depend on ZombieAI."
        },
        new RebirthPathingTenantDecl
        {
            Area = RebirthPathingTenantArea.Diagnostics,
            Status = RebirthPathingTenantStatus.Planned,
            OwnerModuleId = "pathing.diagnostics",
            HotPath = true,
            MayAllocate = false,
            Evidence = "debug policy",
            Notes = "No pathing diagnostics string/log construction in release hot paths."
        }
    };

    private static readonly RebirthLocalRouteBudgetDecl[] s_budgets = new[]
    {
        new RebirthLocalRouteBudgetDecl
        {
            BudgetId = "preferred-non-drop-route-builds",
            PreferredBuildsPerFrame = 1,
            OwnerModuleId = "pathing.localRoutes",
            Evidence = "P2.5: existing PreferredNonDropRouteMaxBuildsPerFrame = 1",
            Notes = "Current route guard already has a bounded build budget; future phase moves it under RebirthFrameScheduler."
        }
    };

    private static readonly RebirthAStarRiskDecl[] s_astarRisks = new[]
    {
        new RebirthAStarRiskDecl
        {
            PatchTarget = "Pathfinding.GridGraph.Linecast",
            Decision = RebirthAStarRiskDecision.ExcludeByDefault,
            OwnerModuleId = "pathing.astarRisk",
            Evidence = "P4",
            Notes = "Do not absorb A* internal transpiler/reverse patch by default."
        },
        new RebirthAStarRiskDecl
        {
            PatchTarget = "Pathfinding.LayerGridGraph.GetNearest",
            Decision = RebirthAStarRiskDecision.ExcludeByDefault,
            OwnerModuleId = "pathing.astarRisk",
            Evidence = "P4",
            Notes = "Do not absorb A* internal transpiler/reverse patch by default."
        },
        new RebirthAStarRiskDecl
        {
            PatchTarget = "Pathfinding.RaycastModifier.ValidateLine",
            Decision = RebirthAStarRiskDecision.ExcludeByDefault,
            OwnerModuleId = "pathing.astarRisk",
            Evidence = "P4",
            Notes = "Do not absorb A* internal transpiler/reverse patch by default."
        }
    };

    public static RebirthPathingTenantDecl[] GetTenantsSnapshot()
    {
        RebirthPathingTenantDecl[] copy = new RebirthPathingTenantDecl[s_tenants.Length];
        Array.Copy(s_tenants, copy, copy.Length);
        return copy;
    }

    public static string GetTenantReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthPathing] governed EntityMoveHelper/pathing pipeline.");
        sb.AppendLine("  Hybrid smoothing installs one bounded EntityMoveHelper postfix.");
        sb.AppendLine("  A* internals remain untouched; native paths remain authoritative.");
        sb.AppendLine("area | status | owner | hot | alloc | evidence | notes");

        for (int i = 0; i < s_tenants.Length; i++)
        {
            RebirthPathingTenantDecl t = s_tenants[i];
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

    public static string GetBudgetReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPathing] local route budget ledger.");
        sb.AppendLine("budget | builds/frame | owner | evidence | notes");

        for (int i = 0; i < s_budgets.Length; i++)
        {
            RebirthLocalRouteBudgetDecl b = s_budgets[i];
            sb.Append(b.BudgetId).Append(" | ")
              .Append(b.PreferredBuildsPerFrame).Append(" | ")
              .Append(b.OwnerModuleId).Append(" | ")
              .Append(b.Evidence).Append(" | ")
              .AppendLine(b.Notes);
        }

        return sb.ToString();
    }

    public static string GetAStarRiskReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPathing] A* internal patch risk ledger.");
        sb.AppendLine("target | decision | owner | evidence | notes");

        for (int i = 0; i < s_astarRisks.Length; i++)
        {
            RebirthAStarRiskDecl r = s_astarRisks[i];
            sb.Append(r.PatchTarget).Append(" | ")
              .Append(r.Decision).Append(" | ")
              .Append(r.OwnerModuleId).Append(" | ")
              .Append(r.Evidence).Append(" | ")
              .AppendLine(r.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPathing] safety rules:");
        sb.AppendLine("  1. EntityMoveHelper integration must remain one bounded, allocation-free owner patch.");
        sb.AppendLine("  2. Do not absorb A* internals by default.");
        sb.AppendLine("  3. ZombieAI depends on Pathing; Pathing must not depend on ZombieAI.");
        sb.AppendLine("  4. Local route guards must be centrally budgeted.");
        sb.AppendLine("  5. No pathing debug string construction in release hot paths.");
        sb.AppendLine("  6. Hybrid smoothing may skip only existing native path nodes after body-clearance validation.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthPathing] Pathing tenant entries: " + s_tenants.Length
            + "; local route budgets: " + s_budgets.Length
            + "; A* risk entries: " + s_astarRisks.Length
            + ". Hybrid path-guided shortcut smoothing is installed; A* internals remain excluded.";
    }
}
#endif
