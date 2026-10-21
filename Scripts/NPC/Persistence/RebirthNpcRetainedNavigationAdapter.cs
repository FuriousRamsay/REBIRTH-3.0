using System;
using System.Collections.Generic;

public sealed partial class RebirthNpcNavigationRuntimeAdapter
{
    internal static RebirthNpcRetainedControllerCustody.Member CaptureControllerCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(callbacks!=0)throw new InvalidOperationException("Actual native navigation callback must return before detach");
            var records=States;var order=Order;var world=navigationWorld;ulong generation=navigationGeneration;
            long[] counts={requests,accepted,satisfied,rateLimited,progressResets,stuckDetections,recoveryAttempts,terminalFailures,stops,evictions,budgetDeferred};
            var originals=new Dictionary<int,RebirthNpcControllerOriginalScope>();
            foreach(var pair in records)
            {
                var state=pair.Value;if(state==null||state.EntityId!=pair.Key||state.StableId.IsEmpty||state.Runtime?.StableId!=state.StableId)throw new InvalidOperationException("Actual native navigation state unavailable");
                var source=RebirthNpcControllerOriginalScope.CaptureRetainedNavigation(scope,state.Actor,state.Runtime);
                if(source==null)throw new InvalidOperationException("Actual original navigation actor lineage unavailable");originals.Add(pair.Key,source);
            }
            var next=new Dictionary<int,State>();var nextOrder=new RebirthNpcFairRoundRobin<int>();
            var extra=new System.Text.StringBuilder();foreach(var id in order.CopyOrder())extra.Append(RebirthNpcRetainedControllerCustody.Fields(id));
            foreach(var count in counts)extra.Append(RebirthNpcRetainedControllerCustody.Fields(count));
            return RebirthNpcRetainedControllerCustody.Capture("navigation-runtime-empty",scope,world,generation,0,records,
                s=>originals[s.EntityId],s=>0,s=>0,
                s=>RebirthNpcRetainedControllerCustody.Fields(s.EntityId,s.StableId,s.Destination,s.LastObservedPosition,s.LastRequestTicks,s.LastProgressTicks,s.ConsecutiveNoProgress,s.RecoveryAttempts,s.LastReason),
                s=>new object[]{s.Actor,s.Runtime},
                ()=>{lock(Sync)return callbacks==0&&ReferenceEquals(States,records)&&ReferenceEquals(Order,order)&&ReferenceEquals(navigationWorld,world)&&navigationGeneration==generation&&SameCounts(counts);},
                ()=>{lock(Sync)return !ReferenceEquals(States,records)&&!ReferenceEquals(Order,order)&&(world==null||!ReferenceEquals(navigationWorld,world));},
                ()=>{lock(Sync){States=next;Order=nextOrder;navigationWorld=null;navigationGeneration=0;SetCounts(new long[11]);}},
                ()=>{lock(Sync){States=records;Order=order;navigationWorld=world;navigationGeneration=generation;SetCounts(counts);}},null,extra.ToString());
        }
    }
}
