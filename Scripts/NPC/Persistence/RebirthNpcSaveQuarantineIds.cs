using System;
using System.Collections.Generic;

// Key-only enumeration before expensive evidence snapshots. Each owning service
// contributes actual typed identities; no GUID-looking XML/coordinate inference.
internal static class RebirthNpcSaveQuarantineIds
{
    internal const int MaximumIds=8192;
    internal static void Add(HashSet<RebirthNpcStableId> ids,RebirthNpcStableId id,bool optional=false)
    {if(id.IsEmpty){if(optional)return;throw new InvalidOperationException("Original NPC identity unknown");}if(!ids.Contains(id)&&ids.Count>=MaximumIds)throw new InvalidOperationException("Original NPC identity retention bound");ids.Add(id);}
    internal static RebirthNpcStableId[] Collect()
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Original identity capture requires main thread");var ids=new HashSet<RebirthNpcStableId>();
        RebirthNpcRuntimeRegistry.AppendQuarantineIds(ids);RebirthNpcInventoryTransactionService.AppendQuarantineIds(ids);RebirthNpcEquipmentService.AppendQuarantineIds(ids);RebirthNpcSocialService.AppendQuarantineIds(ids);RebirthNpcProgressionService.AppendQuarantineIds(ids);RebirthNpcAdvancedProgressionService.AppendQuarantineIds(ids);RebirthNpcCombatEmergencyService.AppendQuarantineIds(ids);RebirthNpcSettlementSimulation.AppendQuarantineIds(ids);RebirthNpcStableIdentityStore.AppendQuarantineIds(ids);RebirthNpcAggregatePersistenceStore.AppendQuarantineIds(ids);RebirthNpcExecutionPersistenceStore.AppendQuarantineIds(ids);RebirthNpcExecutionLeaseRegistry.AppendQuarantineIds(ids);RebirthNpcWorldIntegrationService.AppendQuarantineIds(ids);RebirthNpcWorkOutcomeService.AppendQuarantineIds(ids);RebirthNpcLifecycleGameplayService.AppendQuarantineIds(ids);RebirthNpcIdentityAuditLedger.AppendQuarantineIds(ids);
        RebirthDogNavigationMarkerService.AppendQuarantineIds(ids);RebirthCompanionBehaviorService.AppendQuarantineIds(ids);RebirthCompanionInventoryLockService.AppendQuarantineIds(ids);RebirthDogRuntimeService.AppendQuarantineIds(ids);RebirthDogChunkObserverService.AppendQuarantineIds(ids);RebirthNpcTradeService.AppendQuarantineIds(ids);RebirthNpcDialogueService.AppendQuarantineIds(ids);RebirthNpcDecisionEngine.AppendQuarantineIds(ids);RebirthNpcDecisionSignalStore.AppendQuarantineIds(ids);RebirthNpcActivityRegistry.AppendQuarantineIds(ids);RebirthNpcActivityRecoveryRegistry.AppendQuarantineIds(ids);RebirthNpcCommandGateway.AppendQuarantineIds(ids);RebirthNpcProgressionClientCache.AppendQuarantineIds(ids);RebirthNpcProgressionProjectionRevisionService.AppendQuarantineIds(ids);
        if(!RebirthNpcNetworkBaselineService.HasNoPendingBaseline)throw new InvalidOperationException("Original native baseline jobs remain retained");
        RebirthNpcRetainedWorkPackages.AppendQuarantineIds(ids);
        RebirthNpcEquipmentProjectionReceipts.AppendQuarantineIds(ids);RebirthNpcWorkAssignmentService.AppendQuarantineIds(ids);RebirthNpcSpatialIndex.AppendQuarantineIds(ids);RebirthNpcReplicationPerformanceService.AppendQuarantineIds(ids);
        var result=new RebirthNpcStableId[ids.Count];ids.CopyTo(result);Array.Sort(result,(a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));return result;
    }
}
public static partial class RebirthNpcRuntimeRegistry
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in EntityByStableId.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
public static partial class RebirthNpcInventoryTransactionService
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in States.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
public static partial class RebirthNpcEquipmentService
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in States.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
public static partial class RebirthNpcSocialService
{
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {
        lock(Sync)
        {
            foreach(var id in Memories.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);
            foreach(var key in Relationships.Keys){int split=key.IndexOf('|');if(split<=0||!RebirthNpcStableId.TryParse(key.Substring(0,split),out var id))throw new InvalidOperationException("Social original owner key malformed");RebirthNpcSaveQuarantineIds.Add(ids,id);}
        }
    }
}
public static partial class RebirthNpcProgressionService
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in Records.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
public static partial class RebirthNpcProgressionClientCache
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){foreach(var id in Values.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(var id in Generations.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}}
public static partial class RebirthNpcProgressionProjectionRevisionService
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in Revisions.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
public static partial class RebirthNpcAdvancedProgressionService
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){foreach(var id in Records.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(var session in Mentorships.Values){if(session==null)throw new InvalidOperationException("Mentorship original missing");RebirthNpcSaveQuarantineIds.Add(ids,session.MentorId);RebirthNpcSaveQuarantineIds.Add(ids,session.TraineeId);}}}}
public static partial class RebirthNpcCombatEmergencyService
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in States.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
public static partial class RebirthNpcSettlementSimulation
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){foreach(var id in Needs.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(var id in Professions.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(var id in BusyNpcs)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(var id in ClaimedWorkByNpc.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(var row in Work.Values){if(row==null)throw new InvalidOperationException("Work original missing");RebirthNpcSaveQuarantineIds.Add(ids,row.ClaimedBy,true);}}}}
public static partial class RebirthNpcStableIdentityStore
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var id in Pending.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}
public static partial class RebirthNpcAggregatePersistenceStore
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){foreach(var id in Records.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(var id in QuarantinedRecords.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);}}}
public static partial class RebirthNpcExecutionPersistenceStore
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){foreach(var id in PendingByStableId.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);foreach(var row in LegacyPendingByEntity.Values){if(row==null)throw new InvalidOperationException("Legacy execution missing");RebirthNpcSaveQuarantineIds.Add(ids,row.StableId);}foreach(var row in WorkCustody.Values){if(row?.Snapshot==null)throw new InvalidOperationException("Work custody missing");RebirthNpcSaveQuarantineIds.Add(ids,row.Snapshot.StableId);}}}}
public static partial class RebirthNpcExecutionLeaseRegistry
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){foreach(var owner in NativeWorkOwners.Values){if(owner==null)throw new InvalidOperationException("Native work owner missing");RebirthNpcSaveQuarantineIds.Add(ids,owner.StableOwner);}foreach(var row in recoveryReserved.Values){if(row==null)throw new InvalidOperationException("Reserved execution missing");RebirthNpcSaveQuarantineIds.Add(ids,row.StableId);}}}}
public static partial class RebirthNpcWorldIntegrationService
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){foreach(var id in ByStable.Keys)RebirthNpcSaveQuarantineIds.Add(ids,id);if(PendingSpawns.Count!=0||worldSnapshotPublicationUncertain||SpawnAttempts.HasClaims)throw new InvalidOperationException("Spawn original pending typed custody");}}}
public static partial class RebirthNpcWorkOutcomeService
{internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids){lock(Sync){foreach(var row in Outcomes){if(row==null)throw new InvalidOperationException("Outcome original missing");RebirthNpcSaveQuarantineIds.Add(ids,row.NpcId);}foreach(var row in Events){if(row==null)throw new InvalidOperationException("Progression event missing");RebirthNpcSaveQuarantineIds.Add(ids,row.NpcId);}if(PendingDeliveries.Count!=0)throw new InvalidOperationException("Progression sink custody pending");}}}
