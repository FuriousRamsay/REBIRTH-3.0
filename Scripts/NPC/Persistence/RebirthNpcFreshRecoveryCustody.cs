using System;

// Read-only admission limitation, not cancellation or generation rebasing.
// Existing durable installed-generation continuation is deliberately separate:
// legitimate same-G actions while remote ACK is pending must remain intact.
internal static class RebirthNpcFreshRecoveryCustody
{
    internal static bool Check(World world,out string error)
    {
        error="Pending original command/activity/decision/trade/native-edit custody is not generation-qualified for fresh recovery";
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World))return false;
        if(!RebirthNpcWorkAssignmentService.CanBeginFreshAssignmentGeneration(world)||!RebirthNpcCommandGateway.CanBeginFreshGeneration(world)||!RebirthNpcActivityRegistry.CanBeginFreshGeneration(world)||!RebirthNpcActivityRecoveryRegistry.CanBeginFreshGeneration(world)||!RebirthNpcDecisionEngine.CanBeginFreshGeneration(world)||!RebirthNpcTradeService.CanBeginFreshGeneration(world)||!RebirthDogStorageService.CanRetireEmptyStorage||!RebirthDogChunkObserverService.CanBeginFreshGeneration(world)||!RebirthDogRuntimeService.CanBeginFreshGeneration(world)||!RebirthDogLifecycleService.CanRetireEmptyGhostRetries||!RebirthDogAutoLootBridge.CanRetireEmptyAutoLoot||!RebirthCompanionBehaviorService.CanRetireEmptyBehavior||!RebirthCompanionInventoryLockService.CanRetireEmptyItemLocks||!RebirthCompanionSnapshotService.CanRetireEmptySnapshots||!RebirthNpcFollowRuntime.CanRetireEmptyController||!RebirthNpcTravelRuntime.CanRetireEmptyController||!RebirthNpcGuardNavigationRuntime.CanRetireEmptyController||!RebirthNpcPatrolRuntime.CanRetireEmptyController||!RebirthNpcStationaryActivityRuntime.CanRetireEmptyController||!RebirthNpcMissionRuntime.CanRetireEmptyController||!RebirthNpcNetworkBaselineService.HasNoPendingBaseline||!RebirthNpcWorkReservationService.CanRetireEmptyReservations||!RebirthNpcWorkAssignmentCoordinator.CanRetireEmptyAssignmentCoordinator||!RebirthNpcWorkstationExecutionService.CanRetireEmptyWorkstations||!RebirthNpcNavigationRuntimeAdapter.CanRetireEmptyNavigationRuntime||!RebirthNpcWorkRuntime.CanRetireEmptyController||!RebirthNpcWorkExecutorRegistry.CanRetireEmptyResolution||!RebirthNpcWorkTargetRegistry.CanRetireEmptyTargets)return false;
        if(!(RebirthNpcGuardDutyWorkPackageService.CanRetireEmptyGuardPackage&&RebirthNpcMedicalTreatmentWorkPackageService.CanRetireEmptyMedicalPackage&&RebirthNpcGuardPostWorkAdapter.CanRetireEmptyNative&&RebirthNpcMedicalTreatmentAdapter.CanRetireEmptyNative))return false;
        if(!RebirthNpcRecoveryPrimitiveCaches.CanPrepareFresh(world,out error))return false;
        error=string.Empty;return true;
    }
}
public static partial class RebirthNpcCommandGateway
{
    internal static bool CanBeginFreshGeneration(World world)
    {lock(Sync){if(commandWorld!=null&&!ReferenceEquals(commandWorld,world))return false;if(pendingCount!=0||RetryByDue.Count!=0)return false;foreach(var queue in PendingByPriority)if(queue.Count!=0)return false;foreach(var record in Records.Values)if(record==null||!IsTerminal(record.Status))return false;return true;}}
}
public static partial class RebirthNpcActivityRegistry
{
    internal static bool CanBeginFreshGeneration(World world)
    {lock(Sync){if(activityWorld!=null&&!ReferenceEquals(activityWorld,world))return false;if(ActiveByEntity.Count!=0)return false;foreach(var record in ByActivityId.Values)if(record==null||record.Status==RebirthNpcActivityStatus.Running)return false;return true;}}
}
public static partial class RebirthNpcActivityRecoveryRegistry
{
    internal static bool CanBeginFreshGeneration(World world)
    {lock(Sync){if(activityRecoveryWorld!=null&&!ReferenceEquals(activityRecoveryWorld,world))return false;if(PendingByEntity.Count!=0||PendingOrder.Count!=0||PendingNodes.Count!=0||AttemptsByLease.Count!=0)return false;foreach(var ticket in ByRecoveryId.Values)if(ticket==null||ticket.Status==RebirthNpcActivityRecoveryStatus.Pending)return false;return true;}}
}
public static partial class RebirthNpcDecisionEngine
{
    internal static bool CanBeginFreshGeneration(World world)
    {lock(Sync){if(decisionWorld!=null&&!ReferenceEquals(decisionWorld,world))return false;foreach(var state in States.Values)if(state==null||state.Active!=null)return false;return true;}}
}
