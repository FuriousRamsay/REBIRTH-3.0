using System;
using System.Collections.Generic;

public static partial class RebirthNpcWorkExecutorRegistry
{
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var row in ResolutionCache.Values)RebirthNpcSaveQuarantineIds.Add(ids,RebirthNpcRetainedWorkPackages.NativeOwner(resolutionWorld,row.EntityId));}
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var rows=ResolutionCache;var world=resolutionWorld;ulong generation=resolutionGeneration;long timer=nextResolutionCachePruneClockTicks;
            long[] counts={staleBindingRejects,resolutionCacheHits,resolutionCacheMisses,resolutionCacheInvalidations,resolutionCacheHealthInvalidations,resolutionCacheExpiredPrunes,resolutionRaces};
            var keys=new RebirthNpcRetainedWorkPackages.Keys();foreach(var row in rows.Values){keys.Actor(world,row.EntityId);keys.Leases.Add(row.LeaseId);if(row.Binding.Executor!=null)keys.Executors.Add(row.Binding.Executor);}
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(ResolutionEmptyImage().OuterXml,RebirthNpcRetainedWorkPackages.Rows(rows,row=>
                RebirthNpcRetainedWorkPackages.Fields(row.EntityId,row.LeaseId,row.AcceptedRevision,row.Binding.Executor?.ExecutorId,row.Binding.Generation,row.ExpiresClockTicks)));
            var next=new Dictionary<int,ResolutionCacheEntry>();
            return RebirthNpcRetainedWorkPackages.Capture("work-resolution-empty",scope,world,rows,row=>new object[]{row,row.Binding.Executor,world?.GetEntity(row.EntityId)},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(ResolutionCache,rows)&&ReferenceEquals(resolutionWorld,world)&&resolutionGeneration==generation&&nextResolutionCachePruneClockTicks==timer;},
                ()=>{lock(Sync)return !ReferenceEquals(ResolutionCache,rows)&&(world==null||!ReferenceEquals(resolutionWorld,world));},
                ()=>{lock(Sync){ResolutionCache=next;resolutionWorld=null;resolutionGeneration=0;nextResolutionCachePruneClockTicks=0;staleBindingRejects=resolutionCacheHits=resolutionCacheMisses=resolutionCacheInvalidations=resolutionCacheHealthInvalidations=resolutionCacheExpiredPrunes=resolutionRaces=0;}},
                ()=>{lock(Sync){ResolutionCache=rows;resolutionWorld=world;resolutionGeneration=generation;nextResolutionCachePruneClockTicks=timer;staleBindingRejects=counts[0];resolutionCacheHits=counts[1];resolutionCacheMisses=counts[2];resolutionCacheInvalidations=counts[3];resolutionCacheHealthInvalidations=counts[4];resolutionCacheExpiredPrunes=counts[5];resolutionRaces=counts[6];}});
        }
    }
}

public static partial class RebirthNpcWorkTargetRegistry
{
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var row in Owners.Values)RebirthNpcSaveQuarantineIds.Add(ids,RebirthNpcRetainedWorkPackages.NativeOwner(targetWorld,row.EntityId));}
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var rows=Owners;var world=targetWorld;ulong generation=targetGeneration;
            long[] counts={acquisitions,renewals,renewalFailures,conflicts,releases,releaseRejects,staleReclaims};
            var keys=new RebirthNpcRetainedWorkPackages.Keys();foreach(var pair in rows){keys.Actor(world,pair.Value.EntityId);keys.Leases.Add(pair.Value.LeaseId);keys.Activities.Add(pair.Value.ActivityId);keys.Targets.Add(pair.Key.ToString());}
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(TargetEmptyImage().OuterXml,RebirthNpcRetainedWorkPackages.Rows(rows,row=>
                RebirthNpcRetainedWorkPackages.Fields(row.EntityId,row.ActivityId,row.LeaseId,row.AcceptedRevision,row.AcquiredClockTicks)));
            var next=new Dictionary<RebirthNpcWorkTargetKey,Ownership>();
            return RebirthNpcRetainedWorkPackages.Capture("work-targets-empty",scope,world,rows,row=>new object[]{row,world?.GetEntity(row.EntityId)},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(Owners,rows)&&ReferenceEquals(targetWorld,world)&&targetGeneration==generation;},
                ()=>{lock(Sync)return !ReferenceEquals(Owners,rows)&&(world==null||!ReferenceEquals(targetWorld,world));},
                ()=>{lock(Sync){Owners=next;targetWorld=null;targetGeneration=0;acquisitions=renewals=renewalFailures=conflicts=releases=releaseRejects=staleReclaims=0;}},
                ()=>{lock(Sync){Owners=rows;targetWorld=world;targetGeneration=generation;acquisitions=counts[0];renewals=counts[1];renewalFailures=counts[2];conflicts=counts[3];releases=counts[4];releaseRejects=counts[5];staleReclaims=counts[6];}});
        }
    }
}

public static partial class RebirthNpcWorkReservationService
{
    internal static string RetainedReservationFields(RebirthNpcWorkReservation row)=>row==null?null:RebirthNpcRetainedWorkPackages.Fields(row.ReservationId,row.AssignmentId,row.NpcId,row.TargetKey,row.Slot,row.ExpiresUtcTicks,row.AssignmentRevision);
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var row in ById.Values)RebirthNpcSaveQuarantineIds.Add(ids,row.NpcId);}
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,reservationScope);
            var rows=ById;var assignments=ByAssignment;var targets=ByTarget;var original=reservationScope;ulong nextId=nextReservationId;
            long[] counts={granted,denied,renewed,released,expired};var keys=new RebirthNpcRetainedWorkPackages.Keys();
            foreach(var row in rows.Values){if(row.NpcId.IsEmpty)throw new InvalidOperationException("Reservation owner missing");keys.Owners.Add(row.NpcId);keys.Assignments.Add(row.AssignmentId);keys.Targets.Add(row.TargetKey);}
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(nextReservationId,granted,denied,renewed,released,expired,
                RebirthNpcRetainedWorkPackages.Rows(rows,RetainedReservationFields),RebirthNpcRetainedWorkPackages.Rows(assignments,value=>RebirthNpcRetainedWorkPackages.Fields(value)),
                RebirthNpcRetainedWorkPackages.Rows(targets,value=>RebirthNpcRetainedWorkPackages.Order(value)));
            var next=new Dictionary<ulong,RebirthNpcWorkReservation>();var nextAssignments=new Dictionary<ulong,ulong>();var nextTargets=new Dictionary<string,List<ulong>>(StringComparer.OrdinalIgnoreCase);
            return RebirthNpcRetainedWorkPackages.Capture("work-reservations-empty",scope,original?.World,rows,row=>new object[]{row},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(ById,rows)&&ReferenceEquals(ByAssignment,assignments)&&ReferenceEquals(ByTarget,targets)&&ReferenceEquals(reservationScope,original);},
                ()=>{lock(Sync)return !ReferenceEquals(ById,rows)&&!ReferenceEquals(ByAssignment,assignments)&&!ReferenceEquals(ByTarget,targets)&&(original==null||!ReferenceEquals(reservationScope,original));},
                ()=>{lock(Sync){ById=next;ByAssignment=nextAssignments;ByTarget=nextTargets;reservationScope=null;granted=denied=renewed=released=expired=0;}},
                ()=>{lock(Sync){ById=rows;ByAssignment=assignments;ByTarget=targets;reservationScope=original;nextReservationId=nextId;granted=counts[0];denied=counts[1];renewed=counts[2];released=counts[3];expired=counts[4];}});
        }
    }
}
