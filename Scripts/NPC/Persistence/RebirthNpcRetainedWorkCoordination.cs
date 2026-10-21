using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthNpcWorkAssignmentService
{
    internal static string RetainedAssignmentFields(RebirthNpcWorkAssignment row)=>row==null?null:RebirthNpcRetainedWorkPackages.Fields(row.AssignmentId,row.NpcId,row.DefinitionId,
        row.ActorId,row.TargetKey,row.TargetPosition,row.HasTargetPosition,row.Status,row.Revision,row.Priority,row.Detail,row.CreatedUtcTicks,row.UpdatedUtcTicks,
        row.Schedule==null?null:RebirthNpcRetainedWorkPackages.Fields(row.Schedule.StartMinuteInclusive,row.Schedule.EndMinuteExclusive,row.Schedule.DayMask));
    internal static void AppendRetainedAssignmentOwner(HashSet<RebirthNpcStableId> ids,ulong assignment)
    {lock(Sync){if(!ById.TryGetValue(assignment,out var row)||row.NpcId.IsEmpty)throw new InvalidOperationException("Actual package assignment owner unavailable");RebirthNpcSaveQuarantineIds.Add(ids,row.NpcId);}}
}

public static partial class RebirthNpcWorkAssignmentCoordinator
{
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var row in Sessions.Values){if(row.Assignment==null)throw new InvalidOperationException("Actual embodied assignment missing");RebirthNpcSaveQuarantineIds.Add(ids,row.Assignment.NpcId);}}
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,assignmentCoordinatorScope);
            var rows=Sessions;var order=SessionSchedule;var original=assignmentCoordinatorScope;
            long[] counts={started,completed,failed,suspended,noExecutor,executorFaults};var keys=new RebirthNpcRetainedWorkPackages.Keys();
            foreach(var row in rows.Values)
            {
                if(row.Assignment==null||row.Reservation==null||row.Assignment.NpcId.IsEmpty||row.Assignment.AssignmentId!=row.AssignmentId||
                    row.Reservation.ReservationId!=row.ReservationId||row.Intent!=null&&!ReferenceEquals(row.Intent.OriginalWorld,scope.World))throw new InvalidOperationException("Actual embodied callback owner missing");
                keys.Assignments.Add(row.AssignmentId);keys.Owners.Add(row.Assignment.NpcId);keys.Targets.Add(row.Assignment.TargetKey);if(row.Executor!=null)keys.Executors.Add(row.Executor);
            }
            string Intent(RebirthNpcEmbodiedWorkIntent intent)=>intent==null?null:RebirthNpcRetainedWorkPackages.Fields(intent.NativeId,intent.StableId,intent.ProfileId,intent.Generation,
                intent.SaveFingerprint,intent.Path,intent.PrimaryId,intent.SecondaryId,intent.Revision,intent.ExecutorId,intent.ExecutorGeneration);
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(started,completed,failed,suspended,noExecutor,executorFaults,RebirthNpcRetainedWorkPackages.Order(order.CopyOrder()),
                RebirthNpcRetainedWorkPackages.Rows(rows,row=>RebirthNpcRetainedWorkPackages.Fields(row.AssignmentId,row.ReservationId,row.Executor?.ExecutorId,row.StartedUtcTicks,
                    RebirthNpcWorkAssignmentService.RetainedAssignmentFields(row.Assignment),RebirthNpcWorkReservationService.RetainedReservationFields(row.Reservation),Intent(row.Intent))));
            var next=new Dictionary<ulong,Session>();var nextOrder=new RebirthNpcFairRoundRobin<ulong>();
            return RebirthNpcRetainedWorkPackages.Capture("work-assignment-controller-empty",scope,original?.World,rows,
                row=>new object[]{row,row.Executor,row.Intent,row.Assignment,row.Reservation,row.Assignment?.Schedule,row.Intent?.Actor,row.Intent?.OriginalRuntime,row.Intent?.OriginalHoldingData,row.Intent?.OriginalHeldValue},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(Sessions,rows)&&ReferenceEquals(SessionSchedule,order)&&ReferenceEquals(assignmentCoordinatorScope,original);},
                ()=>{lock(Sync)return !ReferenceEquals(Sessions,rows)&&!ReferenceEquals(SessionSchedule,order)&&(original==null||!ReferenceEquals(assignmentCoordinatorScope,original));},
                ()=>{lock(Sync){Sessions=next;SessionSchedule=nextOrder;assignmentCoordinatorScope=null;started=completed=failed=suspended=noExecutor=executorFaults=0;}},
                ()=>{lock(Sync){Sessions=rows;SessionSchedule=order;assignmentCoordinatorScope=original;started=counts[0];completed=counts[1];failed=counts[2];suspended=counts[3];noExecutor=counts[4];executorFaults=counts[5];}});
        }
    }
}

public static partial class RebirthNpcWorkstationExecutionService
{
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,workstationScope);
            var rows=Jobs;var history=TerminalHistory;var original=workstationScope;var counts=JobCounts();var keys=new RebirthNpcRetainedWorkPackages.Keys();
            foreach(var row in rows.Values)
            {
                if(row.Request==null)throw new InvalidOperationException("Actual workstation job request missing");keys.Stations.Add(row.Request.WorkstationPosition.ToString());
                foreach(var assignment in row.CollectionAssignmentIds??Array.Empty<ulong>())keys.Assignments.Add(assignment);
            }
            string Request(RebirthNpcWorkstationJobRequest request)=>RebirthNpcRetainedWorkPackages.Fields(request.SessionKey,request.SettlementId,request.WorkstationPosition,
                request.RecipeName,request.CraftCount,request.Priority,request.StartWorkstation,request.CollectOutput,request.AllowPartialSupply,
                request.Materials==null?null:RebirthNpcRetainedWorkPackages.Order(request.Materials.Select(value=>RebirthNpcRetainedWorkPackages.Fields(value.ItemKey,value.Quantity,value.Fuel))));
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(RebirthNpcRetainedWorkPackages.Order(JobCounts()),RebirthNpcRetainedWorkPackages.Order(history),
                RebirthNpcRetainedWorkPackages.Rows(rows,row=>RebirthNpcRetainedWorkPackages.Fields(row.Id,Request(row.Request),row.State,row.ResumeState,row.SupplyPackageId,row.CollectionPackageId,
                    row.QueueAttempts,row.MissingWorkstationTicks,row.StableIdleTicks,row.CreatedUtcTicks,row.UpdatedUtcTicks,row.Detail,row.SupplyCommitted,row.QueueCommitted,
                    row.CollectionAssignmentIds==null?null:RebirthNpcRetainedWorkPackages.Order(row.CollectionAssignmentIds),
                    row.BaselineOutput==null?null:RebirthNpcRetainedWorkPackages.Rows(row.BaselineOutput,value=>RebirthNpcRetainedWorkPackages.Fields(value)))));
            var next=new Dictionary<Guid,Job>();var nextHistory=new Queue<Guid>();
            return RebirthNpcRetainedWorkPackages.Capture("workstation-jobs-empty",scope,original?.World,rows,
                row=>new object[]{row,row.Request,row.Request?.Materials,row.CollectionAssignmentIds,row.BaselineOutput,scope.World.GetTileEntity(row.Request.WorkstationPosition)},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(Jobs,rows)&&ReferenceEquals(TerminalHistory,history)&&ReferenceEquals(workstationScope,original);},
                ()=>{lock(Sync)return !ReferenceEquals(Jobs,rows)&&!ReferenceEquals(TerminalHistory,history)&&(original==null||!ReferenceEquals(workstationScope,original));},
                ()=>{lock(Sync){Jobs=next;TerminalHistory=nextHistory;workstationScope=null;SetJobCounts(new long[14]);}},
                ()=>{lock(Sync){Jobs=rows;TerminalHistory=history;workstationScope=original;SetJobCounts(counts);}});
        }
    }
}
