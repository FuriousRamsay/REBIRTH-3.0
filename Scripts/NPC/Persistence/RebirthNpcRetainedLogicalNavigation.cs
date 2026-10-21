using System;
using System.Collections.Generic;

public static partial class RebirthNpcWorkNavigationService
{
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids)
    {
        lock(Sync)foreach(var row in States.Values)
        {
            if(row.Intent==null||row.Actor==null||!ReferenceEquals(row.Intent.Actor,row.Actor))throw new InvalidOperationException("Logical native movement original missing");
            RebirthNpcSaveQuarantineIds.Add(ids,row.Intent.StableId);
        }
    }
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            var rows=States;var order=Order;long[] counts={requests,accepted,alreadySatisfied,temporaryFailures,permanentFailures};
            var keys=new RebirthNpcRetainedWorkPackages.Keys();
            foreach(var row in rows.Values)
            {
                if(row.Intent==null||row.Actor==null||row.Adapter==null||row.AssignmentId==0||!ReferenceEquals(row.Intent.Actor,row.Actor)||
                    !ReferenceEquals(row.Intent.OriginalWorld,scope.World)||!ReferenceEquals(row.Actor.world,scope.World))throw new InvalidOperationException("Logical movement native owner unavailable");
                keys.Owners.Add(row.Intent.StableId);keys.Assignments.Add(row.AssignmentId);keys.Executors.Add(row.Adapter);
            }
            string Intent(RebirthNpcEmbodiedWorkIntent value)=>value==null?null:RebirthNpcRetainedWorkPackages.Fields(value.NativeId,value.StableId,value.ProfileId,value.Generation,
                value.SaveFingerprint,value.Path,value.PrimaryId,value.SecondaryId,value.Revision,value.ExecutorId,value.ExecutorGeneration);
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(requests,accepted,alreadySatisfied,temporaryFailures,permanentFailures,RebirthNpcRetainedWorkPackages.Order(order.CopyOrder()),
                RebirthNpcRetainedWorkPackages.Rows(rows,row=>RebirthNpcRetainedWorkPackages.Fields(row.AssignmentId,row.LastRequestUtcTicks,row.ConsecutiveFailures,row.LastDetail,Intent(row.Intent),row.Adapter?.AdapterId)));
            var next=new Dictionary<ulong,State>();var nextOrder=new RebirthNpcFairRoundRobin<ulong>();
            return RebirthNpcRetainedWorkPackages.Capture("work-navigation-empty",scope,scope.World,rows,
                row=>new object[]{row,row.Actor,row.Adapter,row.Intent,row.Intent?.OriginalRuntime,row.Intent?.OriginalHoldingData,row.Intent?.OriginalHeldValue},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(States,rows)&&ReferenceEquals(Order,order);},
                ()=>{lock(Sync)return !ReferenceEquals(States,rows)&&!ReferenceEquals(Order,order);},
                ()=>{lock(Sync){States=next;Order=nextOrder;requests=accepted=alreadySatisfied=temporaryFailures=permanentFailures=0;}},
                ()=>{lock(Sync){States=rows;Order=order;requests=counts[0];accepted=counts[1];alreadySatisfied=counts[2];temporaryFailures=counts[3];permanentFailures=counts[4];}});
        }
    }
}
