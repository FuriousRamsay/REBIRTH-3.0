using System;
using System.Collections.Generic;

public sealed partial class RebirthNpcGuardPostWorkAdapter
{
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var row in States.Values)RebirthNpcSaveQuarantineIds.Add(ids,row.NpcId);}
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,nativeScope);
            var rows=States;var original=nativeScope;long[] counts={begun,ticks,completed,cancelled,restored,orderFailures,driftCorrections};
            var keys=new RebirthNpcRetainedWorkPackages.Keys();foreach(var pair in rows){if(pair.Value.NpcId.IsEmpty)throw new InvalidOperationException("Actual duty native owner unknown");keys.Owners.Add(pair.Value.NpcId);keys.Assignments.Add(pair.Key);}
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(begun,ticks,completed,cancelled,restored,orderFailures,driftCorrections,
                RebirthNpcRetainedWorkPackages.Rows(rows,row=>RebirthNpcRetainedWorkPackages.Fields(row.InitialTicks,row.RemainingTicks,row.Radius,row.Post,row.NpcId,row.PreviousOrder,
                    row.PreviousGuard,row.PreviousHasGuard,row.Restored,row.PendingRestore)));
            var next=new Dictionary<ulong,DutyState>();
            return RebirthNpcRetainedWorkPackages.Capture("guard-native-empty",scope,original?.World,rows,
                row=>new object[]{row,RebirthNpcRetainedWorkPackages.NativeActor(scope.World,row.NpcId)},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(States,rows)&&ReferenceEquals(nativeScope,original);},
                ()=>{lock(Sync)return !ReferenceEquals(States,rows)&&(original==null||!ReferenceEquals(nativeScope,original));},
                ()=>{lock(Sync){States=next;nativeScope=null;begun=ticks=completed=cancelled=restored=orderFailures=driftCorrections=0;}},
                ()=>{lock(Sync){States=rows;nativeScope=original;begun=counts[0];ticks=counts[1];completed=counts[2];cancelled=counts[3];restored=counts[4];orderFailures=counts[5];driftCorrections=counts[6];}});
        }
    }
}

public sealed partial class RebirthNpcMedicalTreatmentAdapter
{
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids){lock(sync)foreach(var pair in states){RebirthNpcSaveQuarantineIds.Add(ids,pair.Value.PatientId);RebirthNpcWorkAssignmentService.AppendRetainedAssignmentOwner(ids,pair.Key);}}
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,nativeScope);
            var rows=states;var original=nativeScope;long[] counts={begun,ticks,treatments,cancelled,overlapDenied,supplyFailures,patientFailures,refunds};
            var keys=new RebirthNpcRetainedWorkPackages.Keys();foreach(var pair in rows){keys.Assignment(scope,pair.Key);if(pair.Value.PatientId.IsEmpty)throw new InvalidOperationException("Actual patient owner unknown");keys.Owners.Add(pair.Value.PatientId);keys.Targets.Add("medical:treat:"+pair.Value.PatientId);}
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(begun,ticks,treatments,cancelled,overlapDenied,supplyFailures,patientFailures,refunds,
                RebirthNpcRetainedWorkPackages.Rows(rows,row=>RebirthNpcRetainedWorkPackages.Fields(row.PatientId,row.SettlementId,row.ItemKey,row.ItemQuantity,row.HealAmount,row.RemainingTicks,row.TotalTicks,row.Committed)));
            var next=new Dictionary<ulong,State>();
            return RebirthNpcRetainedWorkPackages.Capture("medical-native-empty",scope,original?.World,rows,
                row=>new object[]{row,RebirthNpcRetainedWorkPackages.NativeActor(scope.World,row.PatientId)},Image,keys,
                ()=>{lock(sync)return ReferenceEquals(states,rows)&&ReferenceEquals(nativeScope,original);},
                ()=>{lock(sync)return !ReferenceEquals(states,rows)&&(original==null||!ReferenceEquals(nativeScope,original));},
                ()=>{lock(sync){states=next;nativeScope=null;begun=ticks=treatments=cancelled=overlapDenied=supplyFailures=patientFailures=refunds=0;}},
                ()=>{lock(sync){states=rows;nativeScope=original;begun=counts[0];ticks=counts[1];treatments=counts[2];cancelled=counts[3];overlapDenied=counts[4];supplyFailures=counts[5];patientFailures=counts[6];refunds=counts[7];}});
        }
    }
}
