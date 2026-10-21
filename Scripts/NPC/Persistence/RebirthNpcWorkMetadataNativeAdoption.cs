using System;
using System.Collections.Generic;

public static partial class RebirthNpcWorkAssignmentService
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            try{RebirthNpcAncillaryOriginalScope.RequireScope(scope,assignmentRecordScope);}catch{return null;}
            var original=ById;var owners=ByNpc;var history=HistoryOrder;var binding=assignmentRecordScope;
            ulong allocator=nextAssignmentId;string image;
            try{image=AssignmentImage().OuterXml;}catch{return null;}
            bool Current(){lock(Sync)return ReferenceEquals(ById,original)&&ReferenceEquals(ByNpc,owners)&&ReferenceEquals(HistoryOrder,history)&&
                ReferenceEquals(assignmentRecordScope,binding)&&nextAssignmentId==allocator&&AssignmentImage().OuterXml==image;}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&assignmentRecordScope?.Current()==true;
                    if(ReferenceEquals(ById,original)||ReferenceEquals(ByNpc,owners)||ReferenceEquals(HistoryOrder,history)||
                        assignmentRecordScope!=null||ById.Count!=0||ByNpc.Count!=0||HistoryOrder.Count!=0||!RebirthNpcWorkCallbackObservation.IsIdle)return false;
                    // Nonterminal originals stay with the registered retained
                    // work owner. A fresh generation never reissues those jobs
                    // or recycles one of their process-local assignment IDs.
                    foreach(var row in original.Values)if(!IsTerminal(row.Status)&&
                        (!RebirthNpcRetainedWorkCustody.BlocksOwner(row.NpcId)||!RebirthNpcRetainedWorkCustody.BlocksAssignment(row.AssignmentId)))return false;
                    if(!RebirthNpcAncillaryOriginalScope.Use(ref assignmentRecordScope,true))return false;
                    if(allocator==0)nextAssignmentId=0;
                    else if(nextAssignmentId!=0&&nextAssignmentId<allocator)nextAssignmentId=allocator;
                    adopted=incoming;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}

public static partial class RebirthNpcWorkExecutorHealthRegistry
{
    internal static RebirthNpcRuntimeContinuationAdoption CaptureNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||healthWorld!=null&&!ReferenceEquals(healthWorld,scope.World)||!RebirthNpcWorkCallbackObservation.IsIdle)return null;
            var original=Entries;var world=healthWorld;string image;
            try{image=HealthImage().OuterXml;}catch{return null;}
            var copy=new Dictionary<HealthKey,Health>();
            foreach(var pair in original)
            {
                var row=pair.Value;
                copy.Add(pair.Key,new Health{FaultsInWindow=row.FaultsInWindow,WindowStartedClockTicks=row.WindowStartedClockTicks,
                    QuarantineUntilClockTicks=row.QuarantineUntilClockTicks,TotalFaults=row.TotalFaults,Successes=row.Successes,
                    Quarantines=row.Quarantines,Recoveries=row.Recoveries,LastFaultStage=row.LastFaultStage,LastFaultDetail=row.LastFaultDetail});
            }
            bool Current(){lock(Sync)return ReferenceEquals(Entries,original)&&ReferenceEquals(healthWorld,world)&&
                RebirthNpcWorkCallbackObservation.IsIdle&&HealthImage().OuterXml==image;}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> native)
            {
                lock(Sync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming)&&ReferenceEquals(healthWorld,incoming)&&ReferenceEquals(Entries,copy);
                    if(ReferenceEquals(Entries,original)||healthWorld!=null||Entries.Count!=0||!RebirthNpcWorkCallbackObservation.IsIdle)return false;
                    // Executor registration generations are process-local. Keep
                    // their fault quarantine/cooldowns across the native world
                    // transition without moving any executor or callback.
                    Entries=copy;healthWorld=incoming;adopted=incoming;return true;
                }
            }
            return new RebirthNpcRuntimeContinuationAdoption(scope,new HashSet<RebirthNpcStableId>(),Current,Adopt);
        }
    }
}
