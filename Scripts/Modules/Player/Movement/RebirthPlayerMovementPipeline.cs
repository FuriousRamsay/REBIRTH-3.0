using System;
using System.Text;

#nullable disable

public enum RebirthPlayerMovementStage
{
    Unknown,
    ResolveContext,
    WorldEligibility,
    MountedVehicleFastExit,
    ThirdPersonPolicy,
    CrawlOptionGate,
    CrawlEligibility,
    CrawlGeometryProbe,
    CrawlTransition,
    CameraPolicy,
    MovementPressureAttribution,
    Diagnostics
}

public enum RebirthPlayerMovementStageStatus
{
    Planned,
    ScaffoldedNoBehavior,
    RoutedNoBehaviorChange,
    BehaviorMigrated,
    Disabled,
    ManualReviewRequired
}

public sealed class RebirthPlayerMovementStageDecl
{
    public RebirthPlayerMovementStage Stage;
    public RebirthPlayerMovementStageStatus Status;
    public string OwnerModuleId;
    public bool HotPath;
    public bool MayAllocate;
    public string Evidence;
    public string Notes;
}

public sealed class RebirthPlayerMovementContext
{
    public PlayerMoveController Controller;
    public EntityPlayerLocal LocalPlayer;
    public bool HasController;
    public bool HasLocalPlayer;
    public bool IsMountedOrInVehicle;
    public bool ShouldFastExit;
    public string Reason;

    public static RebirthPlayerMovementContext Empty(string reason)
    {
        return new RebirthPlayerMovementContext
        {
            Controller = null,
            LocalPlayer = null,
            HasController = false,
            HasLocalPlayer = false,
            IsMountedOrInVehicle = false,
            ShouldFastExit = true,
            Reason = reason
        };
    }
}

/// <summary>
/// No-behavior player movement tenant scaffold.
/// This class intentionally does not alter PlayerMoveController behavior.
/// It exists so the future PlayerMoveController.Update adapter has a clean target.
/// </summary>
public static class RebirthPlayerMovementPipeline
{
    private static readonly RebirthPlayerMovementStageDecl[] s_stages = new[]
    {
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.ResolveContext,
            Status = RebirthPlayerMovementStageStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "player.movement",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / P2 / P5 / P6.5",
            Notes = "Future adapter should build a context with hard null/world/menu exits."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.WorldEligibility,
            Status = RebirthPlayerMovementStageStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "player.movement",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2",
            Notes = "World inactive/menu/dead/unloaded checks must occur before feature systems."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.MountedVehicleFastExit,
            Status = RebirthPlayerMovementStageStatus.ScaffoldedNoBehavior,
            OwnerModuleId = "player.mounted",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior vehicle water camera issue",
            Notes = "Mounted/in-vehicle state must skip crawl geometry and camera crouch correction entirely."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.ThirdPersonPolicy,
            Status = RebirthPlayerMovementStageStatus.Planned,
            OwnerModuleId = "player.camera",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior crawl third-person rule",
            Notes = "Third person should block crawl state and prevent switching while crawling."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.CrawlOptionGate,
            Status = RebirthPlayerMovementStageStatus.Planned,
            OwnerModuleId = "player.crawl",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / CustomOneBlockCrouch",
            Notes = "If crawl option is disabled, no crawl checks or geometry probes run."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.CrawlEligibility,
            Status = RebirthPlayerMovementStageStatus.Planned,
            OwnerModuleId = "player.crawl",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / crawl canonical cases",
            Notes = "Crouch/facing/stance/space eligibility before any expensive probe."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.CrawlGeometryProbe,
            Status = RebirthPlayerMovementStageStatus.Planned,
            OwnerModuleId = "player.crawl",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior crawl performance findings",
            Notes = "Only runs while crawl state requires it; block classifications must be indexed."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.CrawlTransition,
            Status = RebirthPlayerMovementStageStatus.Planned,
            OwnerModuleId = "player.crawl",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior jitter/flicker regressions",
            Notes = "No time-based forced crawl loops. Transition must be state-driven."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.CameraPolicy,
            Status = RebirthPlayerMovementStageStatus.Planned,
            OwnerModuleId = "player.camera",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / prior vehicle camera issue",
            Notes = "Camera policy must be separate from crawl geometry and vehicle-safe."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.MovementPressureAttribution,
            Status = RebirthPlayerMovementStageStatus.ManualReviewRequired,
            OwnerModuleId = "player.movementPressure",
            HotPath = true,
            MayAllocate = false,
            Evidence = "P5A open gap",
            Notes = "Harmony_RebirthMovementPressureAttribution.cs needs full review before migration."
        },
        new RebirthPlayerMovementStageDecl
        {
            Stage = RebirthPlayerMovementStage.Diagnostics,
            Status = RebirthPlayerMovementStageStatus.Planned,
            OwnerModuleId = "player.movement.diagnostics",
            HotPath = true,
            MayAllocate = false,
            Evidence = "Blueprint v2 / debug policy",
            Notes = "Section timing/log construction must be profiling/debug build only."
        }
    };

    public static RebirthPlayerMovementStageDecl[] GetStagesSnapshot()
    {
        RebirthPlayerMovementStageDecl[] copy = new RebirthPlayerMovementStageDecl[s_stages.Length];
        Array.Copy(s_stages, copy, copy.Length);
        return copy;
    }

    public static RebirthPlayerMovementContext BuildNoBehaviorContext(PlayerMoveController controller)
    {
        if (controller == null)
            return RebirthPlayerMovementContext.Empty("controller is null");

        return new RebirthPlayerMovementContext
        {
            Controller = controller,
            LocalPlayer = null,
            HasController = true,
            HasLocalPlayer = false,
            IsMountedOrInVehicle = false,
            ShouldFastExit = false,
            Reason = "scaffold only; no gameplay context resolution yet"
        };
    }

    public static void OnUpdateNoBehavior(PlayerMoveController controller)
    {
        // Intentional no-op.
        // This method exists only as the future adapter target.
        // It must not be called from Harmony until the no-behavior adapter phase.
        BuildNoBehaviorContext(controller);
    }

    public static string GetStageReport()
    {
        StringBuilder sb = new StringBuilder(8192);
        sb.AppendLine("[RebirthPlayerMovement] no-behavior PlayerMoveController tenant scaffold.");
        sb.AppendLine("  No Harmony patch is installed in this phase.");
        sb.AppendLine("  No movement/crawl/camera behavior is changed in this phase.");
        sb.AppendLine("stage | status | owner | hot | alloc | evidence | notes");

        for (int i = 0; i < s_stages.Length; i++)
        {
            RebirthPlayerMovementStageDecl s = s_stages[i];
            sb.Append(s.Stage).Append(" | ")
              .Append(s.Status).Append(" | ")
              .Append(s.OwnerModuleId).Append(" | ")
              .Append(s.HotPath).Append(" | ")
              .Append(s.MayAllocate).Append(" | ")
              .Append(s.Evidence).Append(" | ")
              .AppendLine(s.Notes);
        }

        return sb.ToString();
    }

    public static string GetSafetyReport()
    {
        StringBuilder sb = new StringBuilder(4096);
        sb.AppendLine("[RebirthPlayerMovement] safety rules:");
        sb.AppendLine("  1. Do not run crawl geometry while mounted/in vehicle.");
        sb.AppendLine("  2. Do not run camera crawl correction while mounted/in vehicle.");
        sb.AppendLine("  3. Do not build movement diagnostics strings in release.");
        sb.AppendLine("  4. Do not migrate crawl behavior before a no-behavior PlayerMoveController adapter compiles and loads.");
        sb.AppendLine("  5. Do not merge MovementPressureAttribution until that file is fully reviewed.");
        sb.AppendLine("  6. Future adapter must be the single REBIRTH owner of PlayerMoveController.Update.");
        return sb.ToString();
    }

    public static string GetSummaryReport()
    {
        return "[RebirthPlayerMovement] PlayerMoveController tenant scaffold stages: " + s_stages.Length
            + ". This phase is no-behavior and installs no Harmony patches.";
    }
}
