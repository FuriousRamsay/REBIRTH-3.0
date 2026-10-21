using System;
using System.Collections.Generic;

// Observational checks only. Pending originals, sessions and leases are never
// cancelled, released or reset to make an administrative recovery admissible.
internal static class RebirthNpcRecoveryPreflight
{
    internal static bool Check(World world,RebirthNpcRecoveryCandidates.Candidate candidate,out string error,bool verifyCurrentInventory=true,RebirthNpcRecoveryPrimitiveCaches caches=null)
    {
        error=string.Empty;
        if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||
            candidate?.Prepared==null||!candidate.Prepared.IsCurrent){error="recovery candidate/world epoch changed";return false;}
        foreach(var related in candidate.Related)if(!related.IsCurrent){error="related recovery store generation changed";return false;}
        if(candidate.Runtime!=null&&!candidate.Runtime.IsCurrent){error="native runtime recovery projection changed";return false;}
        if(candidate.ExecutionProjection!=null&&!candidate.ExecutionProjection.IsCurrent){error="native execution recovery projection changed";return false;}
        if(caches==null||!caches.Frozen){error="Exact frozen derived cache transition required";return false;}
        if(!RebirthNpcExecutionPersistenceStore.CanRecoverQuiescent(candidate.ExecutionProjection!=null,out error))return false;
        if(!RebirthNpcOriginalEffectAdmission.CanRecoverNpcStores(world,candidate.Affected,out error)||
            !RebirthNpcExternalInventoryTransferCoordinator.CanRecoverNpcStores(out error)||
            !RebirthNpcInteractionSessionService.CanRecoverNpcStores(candidate.Affected,out error)||
            !RebirthNpcWorldIntegrationService.CanRecoverNpcStores(out error)||
            !RebirthNpcReplicationPerformanceService.CanRecoverNpcStores(out error,true))return false;
        if(verifyCurrentInventory&&candidate.Inventories!=null&&!RebirthNpcInventoryPersistenceStore.MatchesCurrentRecoveryCustody(candidate.Inventories,out error))return false;
        var currentItems=RebirthNpcItemIntegrityService.AuditRecords(RebirthNpcInventoryTransactionService.CapturePersistentRecords(),RebirthNpcEquipmentService.CapturePersistentRecords());
        if(currentItems.ExcessInventoryReservations!=0)
        {error="non-equipment NPC inventory reservations retain unresolved original custody";return false;}
        foreach(var lease in RebirthNpcExecutionLeaseRegistry.GetSnapshot())
            if(lease.Status==RebirthNpcExecutionLeaseStatus.Active)
            {error="active native execution lease must settle before store recovery";return false;}
        foreach(var command in RebirthNpcCommandGateway.GetSnapshot())
            if(command.Status==RebirthNpcCommandStatus.Submitted||command.Status==RebirthNpcCommandStatus.Validated||command.Status==RebirthNpcCommandStatus.Queued||
                command.Status==RebirthNpcCommandStatus.Executing||command.Status==RebirthNpcCommandStatus.RetryScheduled)
            {error="pending command still references the current generation";return false;}
        var seen=new HashSet<int>();
        foreach(var id in candidate.Affected)
        {
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(id,out int entity))continue;
            if(!seen.Add(entity)||world.GetEntity(entity) is not EntityRebirthNPC npc||npc.RebirthRuntimeState?.StableId!=id)
            {error="live NPC stable/native incarnation mapping is inconsistent";return false;}
            if(npc.RebirthRuntimeState.PreparedRestorationPending){error="prepared original native body retains custody";return false;}
            if(!npc.NativeIdentityReadable){error="native identity payload failed decoding; current body remains retained";return false;}
            // A recovery is never native death resurrection or replacement body
            // adoption. Live condition changes require their own actual action.
            if(npc.bDead||npc.IsDead()){error="native dead NPC remains outside live recovery admission";return false;}
        }
        return candidate.Prepared.IsCurrent&&ReferenceEquals(GameManager.Instance?.World,world);
    }
}
