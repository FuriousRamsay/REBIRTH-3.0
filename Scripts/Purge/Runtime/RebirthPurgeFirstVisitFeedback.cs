using System;

// Called after the evidence owner has authenticated an on-foot live visitor.
// A first-generation record can be published before its visitor is available.
// Do not fill its history silently: proximity discovery uses that timestamp to
// avoid replay, and would otherwise mistake a saved name for delivered feedback.
internal static class RebirthPurgeFirstVisitFeedback
{
    internal static void Observe(RebirthPoiClearanceRecord record,EntityPlayer visitor)
    {
        var history=RebirthPurgePoiHistory.Get(record);
        if(record.Epoch==0 && record.LastResetId==Guid.Empty &&
            (history==null || history.DiscoveredAt==0))
        {
#if PURGE_QUALIFICATION
            try{RecordAttempt(record.Identity.Prefab);}catch{/* Observation must not prevent feedback. */}
#endif
            RebirthPurgeDiscoveryNotification.Send(record.Identity,visitor);
        }
        else
            // Preserve established attribution and the existing quiet reset path.
            RebirthPurgePoiHistory.Discover(record.Identity,RebirthPurgeDiscoveryNotification.DisplayName(visitor));
    }
#if PURGE_QUALIFICATION
    private static WeakReference<World> diagnosticOwner;
    private static long attemptedSends;
    private static string lastPrefab;
    private static void RecordAttempt(string prefab)
    {
        if(!RebirthPurgeFeedbackTrace.Allowed)return;
        var world=GameManager.Instance?.World;if(world==null)return;
        if(diagnosticOwner==null||!diagnosticOwner.TryGetTarget(out var prior)||!ReferenceEquals(prior,world))
        {diagnosticOwner=new WeakReference<World>(world);attemptedSends=0;}
        attemptedSends++;lastPrefab=prefab;
    }
    internal static Newtonsoft.Json.Linq.JObject Diagnostic()
    {
        bool current=diagnosticOwner!=null&&diagnosticOwner.TryGetTarget(out var prior)&&ReferenceEquals(prior,GameManager.Instance?.World);
        return new Newtonsoft.Json.Linq.JObject {
            ["observationOnly"]=true,["attemptedSends"]=current?attemptedSends:0,
            ["lastPrefab"]=current?lastPrefab:null,
            ["scope"]="Authenticated evidence-owner first-visit send attempts; use completed tooltip/audio trace to verify dispatch."
        };
    }
#endif
}
