using System;
using System.Collections.Generic;

public static partial class RebirthNpcGuardDutyWorkPackageService
{
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var row in Active.Values)RebirthNpcWorkAssignmentService.AppendRetainedAssignmentOwner(ids,row.AssignmentId);}
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,packageScope);
            var rows=Active;var original=packageScope;var order=Schedule;
            long[] counts={requests,created,started,rejected,reconciliations,completed,failed};var keys=new RebirthNpcRetainedWorkPackages.Keys();
            foreach(var row in rows.Values)keys.Assignment(scope,row.AssignmentId);
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(requests,created,started,rejected,reconciliations,completed,failed,RebirthNpcRetainedWorkPackages.Order(order.CopyOrder()),
                RebirthNpcRetainedWorkPackages.Rows(rows,row=>RebirthNpcRetainedWorkPackages.Fields(row.Id,row.AssignmentId,row.UpdatedUtcTicks,row.Detail)));
            var next=new Dictionary<Guid,ActivePackage>();var nextOrder=new RebirthNpcFairRoundRobin<Guid>();
            return RebirthNpcRetainedWorkPackages.Capture("guard-packages-empty",scope,original?.World,rows,row=>new object[]{row},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(Active,rows)&&ReferenceEquals(Schedule,order)&&ReferenceEquals(packageScope,original);},
                ()=>{lock(Sync)return !ReferenceEquals(Active,rows)&&!ReferenceEquals(Schedule,order)&&(original==null||!ReferenceEquals(packageScope,original));},
                ()=>{lock(Sync){Active=next;Schedule=nextOrder;packageScope=null;requests=created=started=rejected=reconciliations=completed=failed=0;}},
                ()=>{lock(Sync){Active=rows;Schedule=order;packageScope=original;requests=counts[0];created=counts[1];started=counts[2];rejected=counts[3];reconciliations=counts[4];completed=counts[5];failed=counts[6];}});
        }
    }
}

public static partial class RebirthNpcMedicalTreatmentWorkPackageService
{
    internal static void AppendPackageIds(HashSet<RebirthNpcStableId> ids){lock(Sync)foreach(var row in Active.Values)RebirthNpcWorkAssignmentService.AppendRetainedAssignmentOwner(ids,row.AssignmentId);}
    internal static RebirthNpcRetainedWorkPackages.Member CapturePackageCustody(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            RebirthNpcAncillaryOriginalScope.RequireScope(scope,packageScope);
            var rows=Active;var original=packageScope;var order=Schedule;
            long[] counts={requests,created,started,rejected,reconciliations,completed,failed};var keys=new RebirthNpcRetainedWorkPackages.Keys();
            foreach(var row in rows.Values)keys.Assignment(scope,row.AssignmentId);
            string Image()=>RebirthNpcRetainedWorkPackages.Fields(requests,created,started,rejected,reconciliations,completed,failed,RebirthNpcRetainedWorkPackages.Order(order.CopyOrder()),
                RebirthNpcRetainedWorkPackages.Rows(rows,row=>RebirthNpcRetainedWorkPackages.Fields(row.Id,row.AssignmentId,row.UpdatedUtcTicks,row.Detail)));
            var next=new Dictionary<Guid,ActivePackage>();var nextOrder=new RebirthNpcFairRoundRobin<Guid>();
            return RebirthNpcRetainedWorkPackages.Capture("medical-packages-empty",scope,original?.World,rows,row=>new object[]{row},Image,keys,
                ()=>{lock(Sync)return ReferenceEquals(Active,rows)&&ReferenceEquals(Schedule,order)&&ReferenceEquals(packageScope,original);},
                ()=>{lock(Sync)return !ReferenceEquals(Active,rows)&&!ReferenceEquals(Schedule,order)&&(original==null||!ReferenceEquals(packageScope,original));},
                ()=>{lock(Sync){Active=next;Schedule=nextOrder;packageScope=null;requests=created=started=rejected=reconciliations=completed=failed=0;}},
                ()=>{lock(Sync){Active=rows;Schedule=order;packageScope=original;requests=counts[0];created=counts[1];started=counts[2];rejected=counts[3];reconciliations=counts[4];completed=counts[5];failed=counts[6];}});
        }
    }
}


