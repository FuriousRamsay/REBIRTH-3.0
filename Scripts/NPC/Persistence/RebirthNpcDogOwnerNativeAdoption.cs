using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthDogLifecycleService
{
    internal static RebirthNpcOwnerNativeContinuationAdoption CaptureGhostNativeAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(GhostRepairSync)
        {
            if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal())return null;
            var original=GhostRepairRetries;string image=GhostRetryImage(scope).OuterXml;
            var rows=new List<Tuple<GhostRepairRetry,string,string,int>>();
            foreach(var value in original.Values)
            {
                if(!RebirthStablePlayerIdentity.TryResolveServerEntity(value.OriginalActor,out var owner))return null;
                string creation=RebirthCamperCharacterAccess.CurrentCreation(value.OriginalActor);if(string.IsNullOrWhiteSpace(creation))return null;
                rows.Add(Tuple.Create(value,owner.CanonicalId,creation,value.OriginalActor.Died));
            }
            bool Current(){lock(GhostRepairSync)return ReferenceEquals(GhostRepairRetries,original)&&GhostRetryImage(scope).OuterXml==image;}
            World adopted=null;
            bool Adopt(World incoming,Dictionary<string,NativeOwnerReentry> native)
            {
                lock(GhostRepairSync)
                {
                    if(adopted!=null)return ReferenceEquals(adopted,incoming);
                    if(GhostRepairRetries.Count!=0||!RebirthNpcRecoveryGeneration.TryReadObservedGeneration(incoming,out ulong generation))return false;
                    var next=new Dictionary<int,GhostRepairRetry>();
                    foreach(var row in rows)
                    {
                        if(!native.TryGetValue(row.Item2,out var source)||!source.Current()||source.Creation!=row.Item3||source.Deaths!=row.Item4||
                            !TryResolveOwnerId(source.Actor,out var owner)||!string.IsNullOrEmpty(row.Item1.OriginalOwner)&&row.Item1.OriginalOwner!=owner||next.ContainsKey(source.Actor.entityId))return false;
                        next.Add(source.Actor.entityId,new GhostRepairRetry{OriginalWorld=incoming,OriginalActor=source.Actor,OriginalOwner=owner,
                            RecoveryGeneration=generation,PlayerEntityId=source.Actor.entityId,AttemptsRemaining=row.Item1.AttemptsRemaining,
                            NextAttemptUtcTicks=row.Item1.NextAttemptUtcTicks,OwnerProjectionReconciled=false});
                    }
                    GhostRepairRetries=next;adopted=incoming;return true;
                }
            }
            return Current()?new RebirthNpcOwnerNativeContinuationAdoption(scope,new HashSet<string>(rows.Select(value=>value.Item2),StringComparer.Ordinal),Current,Adopt):null;
        }
    }
}

public static partial class RebirthCompanionSnapshotService
{
    internal static RebirthNpcOwnerNativeContinuationAdoption CaptureNativeOwnerAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        if(!ThreadManager.IsMainThread()||scope==null||!scope.IsUnchangedOriginal())return null;
        var original=Pending;ulong epoch=requestEpoch;
        var rows=new List<Tuple<PendingRequest,string,string,int>>();
        foreach(var value in original.Values)
        {
            if(value?.Callback==null||!ReferenceEquals(value.World,scope.World)||!ReferenceEquals(value.Actor?.world,scope.World)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(value.Actor,out var owner))return null;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(value.Actor);if(string.IsNullOrWhiteSpace(creation))return null;
            rows.Add(Tuple.Create(value,owner.CanonicalId,creation,value.Actor.Died));
        }
        bool Current()=>ReferenceEquals(Pending,original)&&requestEpoch==epoch&&original.Count==rows.Count&&
            rows.All(row=>row.Item1.Callback!=null&&original.Values.Any(value=>ReferenceEquals(value,row.Item1)));
        World adopted=null;
        bool Adopt(World incoming,Dictionary<string,RebirthDogLifecycleService.NativeOwnerReentry> native)
        {
            if(adopted!=null)return ReferenceEquals(adopted,incoming);
            if(ReferenceEquals(Pending,original)||requestEpoch==epoch)return false;
            foreach(var row in rows)if(!native.TryGetValue(row.Item2,out var source)||!source.Current()||source.Creation!=row.Item3||source.Deaths!=row.Item4||
                ReferenceEquals(row.Item1.World,incoming))return false;
            // The exact original UI owner now has a genuine new native session.
            // Close only old projection callbacks, never invoke them in the new
            // World or consume any new request from the replacement dictionary.
            foreach(var row in rows)row.Item1.Callback=null;
            original.Clear();adopted=incoming;return true;
        }
        return Current()?new RebirthNpcOwnerNativeContinuationAdoption(scope,new HashSet<string>(rows.Select(value=>value.Item2),StringComparer.Ordinal),Current,Adopt):null;
    }
}
