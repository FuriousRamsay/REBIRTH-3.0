#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public static partial class RebirthNpcWorkTargetRegistry
{
    // Uses only ACTUAL existing claims. Never inserts a fake owner/session/lease,
    // enables Work, invokes a provider, or treats an empty input as settlement.
    internal static void VerifyActualHeldTargets(string confirmation)
    {
        if(!RebirthNpcQualificationMutationGuard.Allows(confirmation))throw new InvalidOperationException(RebirthNpcQualificationMutationGuard.Refused);
        if(RebirthNpcWorkReleaseGate.Enabled||!RebirthNpcWorkCallbackObservation.IsIdle)throw new InvalidOperationException("Held Work precondition missing");
        lock(Sync)
        {
            if(Owners.Count>8192)throw new InvalidOperationException("Actual held claim observation exceeds fixture bound");
            var original=Owners;var rows=new List<KeyValuePair<RebirthNpcWorkTargetKey,Ownership>>(Owners);string before=HeldFixtureImage(rows);
            Reconcile(Array.Empty<RebirthNpcWorkSession>());
            if(rows.Count>0)
            {
                var row=rows[0];var owner=row.Value;if(owner==null)throw new InvalidOperationException("Actual original claim malformed; retained");var position=new Vector3(row.Key.X*0.5f,row.Key.Y*0.5f,row.Key.Z*0.5f);
                if(TryRenew(position,owner.EntityId,owner.ActivityId,owner.LeaseId,owner.AcceptedRevision,out _)||Release(position,owner.EntityId,owner.ActivityId,owner.LeaseId,owner.AcceptedRevision)||TryAcquire(position,owner.EntityId,owner.ActivityId,owner.LeaseId,owner.AcceptedRevision,out _))throw new InvalidOperationException("Held original claim admitted mutation");
            }
            if(!ReferenceEquals(Owners,original)||Owners.Count!=rows.Count||HeldFixtureImage(new List<KeyValuePair<RebirthNpcWorkTargetKey,Ownership>>(Owners))!=before)throw new InvalidOperationException("Held original target image changed");
            foreach(var row in rows)if(!Owners.TryGetValue(row.Key,out var current)||!ReferenceEquals(current,row.Value))throw new InvalidOperationException("Held original target reference changed");
        }
    }
    private static string HeldFixtureImage(List<KeyValuePair<RebirthNpcWorkTargetKey,Ownership>> rows)
    {
        rows.Sort((a,b)=>{int x=a.Key.X.CompareTo(b.Key.X);if(x!=0)return x;int y=a.Key.Y.CompareTo(b.Key.Y);return y!=0?y:a.Key.Z.CompareTo(b.Key.Z);});var result=new StringBuilder();
        foreach(var row in rows){var value=row.Value;if(value==null)throw new InvalidOperationException("Actual original claim malformed");result.Append(row.Key.X).Append(',').Append(row.Key.Y).Append(',').Append(row.Key.Z).Append(':').Append(value.EntityId).Append('/').Append(value.ActivityId).Append('/').Append(value.LeaseId).Append('/').Append(value.AcceptedRevision).Append('/').Append(value.AcquiredClockTicks).Append(';');}
        result.Append('|').Append(acquisitions).Append('/').Append(renewals).Append('/').Append(renewalFailures).Append('/').Append(conflicts).Append('/').Append(releases).Append('/').Append(releaseRejects).Append('/').Append(staleReclaims);return result.ToString();
    }
}
#endif
