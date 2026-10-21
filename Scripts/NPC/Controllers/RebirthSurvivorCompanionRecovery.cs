using System;
using System.Collections.Generic;
using UnityEngine;

// Rebind saved Follow intent to the current stable owner's live incarnation.
// Movement still belongs exclusively to the existing command/activity controller.
internal static class RebirthSurvivorCompanionRecovery
{
    private static long next;
    internal static void Tick()
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||DateTime.UtcNow.Ticks<next||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling||RebirthNpcPersistenceTimelineService.IsAlignmentFailed)return;
        next=DateTime.UtcNow.AddSeconds(2).Ticks;
        var queued=new HashSet<int>();
        foreach(var command in RebirthNpcCommandGateway.GetSnapshot())
            if(command.Status==RebirthNpcCommandStatus.Queued||command.Status==RebirthNpcCommandStatus.Validated||
                command.Status==RebirthNpcCommandStatus.Executing||command.Status==RebirthNpcCommandStatus.RetryScheduled)
                queued.Add(command.Request.TargetEntityId);
        int budget=16;
        foreach(var state in RebirthNpcRuntimeRegistry.GetSnapshot())
        {
            if(state.ProfileId!="survivor.persistent"||state.Order!=RebirthNpcOrderState.Follow||
                state.OwnershipKind!=RebirthNpcOwnershipKind.Player||string.IsNullOrEmpty(state.OwnerId)||
                state.PreparedRestorationPending||state.Presence!=RebirthNpcPresenceState.Active||
                !RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out int id)||queued.Contains(id)||
                world.GetEntity(id) is not EntityRebirthNPC npc||npc.IsDead()||!npc.IsSpawned()||
                RebirthNpcLiveRecoveryRuntime.IsNativeActivationDeferred(npc))continue;
            var owner=RebirthDogRuntimeService.ResolveOwnerPublic(world,state.OwnerId);
            if(owner==null||owner.IsDead()||!owner.IsSpawned())continue;
            if(RebirthNpcExecutionLeaseRegistry.TryGetActive(id,out var lease)&&
                (lease.Order!=RebirthNpcOrderState.Follow||lease.SubjectEntityId==owner.entityId))continue;
            RebirthNpcCommandGateway.SubmitForSubject(npc,state.OwnerId,owner.entityId,
                RebirthNpcCommandKind.Follow,Vector3.zero,false,state.Revision);
            if(--budget==0)break;
        }
    }
}
