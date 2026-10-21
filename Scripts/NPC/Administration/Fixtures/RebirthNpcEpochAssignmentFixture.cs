#if DEBUG
using System;
using System.Collections.Generic;

internal static class RebirthNpcEpochAllocatorFixture
{
    // Only the actual pure allocator on local scalars. No epoch installed,
    // connection manufactured, native World rebound or packet dispatched.
    internal static void Verify()
    {
        uint next=1;if(RebirthNpcNetworkEpoch.TakeEpoch(ref next)!=1||next!=2)throw new InvalidOperationException("Actual epoch allocation failed");
        next=uint.MaxValue;if(RebirthNpcNetworkEpoch.TakeEpoch(ref next)!=uint.MaxValue||next!=0||RebirthNpcNetworkEpoch.TakeEpoch(ref next)!=0||next!=0)throw new InvalidOperationException("Native epoch token wrapped");
    }
}
public static partial class RebirthNpcWorkAssignmentService
{
    internal static void VerifyActualHeldAssignmentRecords(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation)||RebirthNpcWorkReleaseGate.Enabled)throw new InvalidOperationException("Actual held disposable fixture required");
        lock(Sync)
        {
            var records=ById;var owners=ByNpc;var history=HistoryOrder;var original=assignmentRecordScope;string image=AssignmentImage().OuterXml;var references=new Dictionary<ulong,RebirthNpcWorkAssignment>(ById);
            if(Create(null,out _,out _)!=RebirthNpcWorkAssignmentResult.PermissionDenied||Cancel(null,0,0,out _,out _)!=RebirthNpcWorkAssignmentResult.PermissionDenied)throw new InvalidOperationException("Held assignment admission changed");
            foreach(var pair in records){if(EvaluateSchedule(pair.Key,3,640,out _)||TryTransition(pair.Key,pair.Value.Revision,RebirthNpcWorkAssignmentStatus.Ready,"fixture",out _))throw new InvalidOperationException("Held actual assignment changed");}
            Reset();GetReport();
            if(!ReferenceEquals(records,ById)||!ReferenceEquals(owners,ByNpc)||!ReferenceEquals(history,HistoryOrder)||!ReferenceEquals(original,assignmentRecordScope)||AssignmentImage().OuterXml!=image)throw new InvalidOperationException("Actual held assignment image changed");
            foreach(var pair in references)if(!ReferenceEquals(pair.Value,ById[pair.Key]))throw new InvalidOperationException("Actual held assignment object replaced");
        }
    }
}
#endif
