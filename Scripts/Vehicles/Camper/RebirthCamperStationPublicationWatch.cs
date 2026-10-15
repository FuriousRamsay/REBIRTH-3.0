using System;
using System.Collections.Generic;

// Authority-thread polling for original mobile publications. A timeout retains
// the attempted custody record; it never replays payment or treats absence as failure.
public static class RebirthCamperStationPublicationWatch
{
    private sealed class Entry
    {
        internal World World;
        internal EntityPlayer Player;
        internal RebirthStationGridAdmission Admission;
        internal RebirthCamperStationSaveEvidence.Request Request;
        internal RebirthCamperStationSaveEvidence.Publication Proof;
        internal float Deadline;
        internal bool Releasing,ReleaseWarning;
    }
    private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
    private static bool installed;
    private static float next;
    public static void Install()
    {
        if(installed)return;
        RebirthStationPreparationService.RemotePlayerPlanner=PlanRemote;
        RebirthStationPreparationService.RemotePlayerPayment=PayRemote;
        RebirthStationPreparationService.RemotePlayerPublished=ObserveRemotePayment;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Tick));
        installed=true;
    }
    internal static bool TryPublishPrepared(EntityPlayer player,RebirthCamperStationAddress address,string creation,Guid job)
    {
        if(!installed||!ThreadManager.IsMainThread()||entries.Count>=64||entries.ContainsKey(job.ToString("N")))return false;
        var entry=new Entry{World=player.world,Player=player,Deadline=UnityEngine.Time.realtimeSinceStartup+30f};
        entries.Add(job.ToString("N"),entry);
        try
        {
            if(!RebirthStationPreparationService.TryPublishMobilePrepared(player,address,creation,job,out var attempted,out var request))
            {Release(job.ToString("N"),entry);return false;}
            if(attempted==null||attempted.JobId!=job.ToString("N"))
            {Release(job.ToString("N"),entry);return false;}
            if(entry.Admission==null)entry.Admission=attempted.Clone();
            else if(entry.Admission.JobId!=attempted.JobId)
            {Release(job.ToString("N"),entry);return false;}
            if(request!=null)entry.Request=request;
            // A pending owner debit occupies the SAME original watcher entry.
            // Only saved owner callback attaches a native vehicle save request.
            return entry.Request!=null||attempted.IsPlayerPayment;
        }
        catch{Release(job.ToString("N"),entry);throw;}
    }
    private static bool PlanRemote(EntityPlayer player,RebirthCamperStationState station,Recipe recipe,int batches,int tier,
        out RebirthStationGridIngredients.Plan fragmentsPlan,out ItemStack[] fragments,out RebirthCamperStationPlayerPaymentImage payment)
    {
        fragmentsPlan=null;fragments=null;payment=null;
        if(player==null||player is EntityPlayerLocal||RebirthBackpackLibraryReservation.BlocksResourceUse(player)||
            !RebirthCamperRemoteStationTransport.TryGetCurrent(player,out var live,out var address,out var creation,out var lease)||
            !ReferenceEquals(live,station)||!RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null||
            !RebirthCamperStationPlayerSavedSource.TryCopySavedSource(identity,out var bag,out var belt,out var bagLocks))return false;
        Func<bool> current=()=>RebirthCamperRemoteStationTransport.IsCurrent(player,address,creation,lease)&&
            !RebirthBackpackLibraryReservation.BlocksResourceUse(player)&&
            RebirthWorldCharacterService.TryGetIdentity(player,out var now)&&now!=null&&now.StorageKey==identity.StorageKey&&now.CanonicalId==identity.CanonicalId;
        return current()&&RebirthCamperStationPlayerPayment.TryPlanFromImages(player,station,recipe,batches,tier,identity,bag,belt,bagLocks,null,
            RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length),out fragmentsPlan,out fragments,out payment)&&current();
    }
    private static bool PayRemote(EntityPlayer player,RebirthStationGridAdmission admission,Func<bool> current,Action<bool> completed)
    {
        if(admission==null||current==null||completed==null||!entries.TryGetValue(admission.JobId,out var entry)||
            !ReferenceEquals(entry.Player,player)||!ReferenceEquals(entry.World,player.world)||
            !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||!current()||
            !RebirthCamperStationPlayerDebitPayload.TryCreate(player,identity,admission,out var payload))return false;
        Func<bool> watched=()=>current()&&entries.TryGetValue(admission.JobId,out var same)&&ReferenceEquals(same,entry)&&!entry.Releasing&&
            UnityEngine.Time.realtimeSinceStartup<entry.Deadline;
        return watched()&&RebirthCamperRemoteOwnerApplication.SendOwnerApplication(player,RebirthCamperStationPlayerPayment.Operation(admission),
            RebirthCamperRemoteOwnerEffectKind.Debit,payload,paid=>
            {
                bool saved=paid&&watched()&&RebirthCamperStationPlayerPayment.HasSaved(player,admission);
                completed(saved);
                if(!saved&&entries.TryGetValue(admission.JobId,out var retained)&&ReferenceEquals(retained,entry))Release(admission.JobId,entry);
            });
    }
    private static void ObserveRemotePayment(EntityPlayer player,RebirthStationGridAdmission admission,RebirthCamperStationSaveEvidence.Request request)
    {
        if(!ThreadManager.IsMainThread()||admission==null||request==null||
            !entries.TryGetValue(admission.JobId,out var entry)||!ReferenceEquals(entry.Player,player)||
            !ReferenceEquals(entry.World,player.world)||entry.Releasing||entry.Request!=null)
        {request?.Dispose();return;}
        entry.Admission=admission.Clone();entry.Request=request;entry.Deadline=UnityEngine.Time.realtimeSinceStartup+30f;
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread()||entries.Count==0)return;
        float now=UnityEngine.Time.realtimeSinceStartup;
        if(now<next)return;next=now+.25f;
        var jobs=new List<string>(entries.Keys);
        foreach(var job in jobs)
        {
            if(!entries.TryGetValue(job,out var entry))continue;
            if(entry.Releasing){Release(job,entry);continue;}
            bool completed=false;
            try
            {
                if(!ReferenceEquals(entry.World,GameManager.Instance?.World)||entry.World==null||entry.World.IsRemote()||
                    entry.Player==null||entry.Player.IsDead()||!ReferenceEquals(entry.World.GetEntity(entry.Player.entityId),entry.Player)||
                    now>=entry.Deadline)
                {
                    Log.Warning("[Camper] Publication remains unconfirmed for job "+job+"; attempted custody retained.");
                    Release(job,entry);continue;
                }
                if(entry.Proof==null)
                {
                    if(entry.Request==null)continue; // original owner payment still awaiting its exact saved witness
                    if(RebirthCamperStationSaveEvidence.TryObserve(entry.Request,out var proof))entry.Proof=proof;
                    else if(entry.Request.Closed)
                    {
                        Log.Warning("[Camper] Native save observation expired or changed for job "+job+"; attempted custody retained.");
                        Release(job,entry);continue;
                    }
                }
                if(entry.Proof!=null)completed=RebirthCamperStationPublicationService.TryRecord(entry.Player,entry.Admission,entry.Proof)&&
                    RebirthCamperStationQueuedAuthority.TryRegister(entry.Player,entry.Admission,entry.Proof);
            }
            catch(Exception error)
            {
                Log.Warning("[Camper] Publication observation interrupted for job "+job+": "+error.GetType().Name);
                Release(job,entry);continue;
            }
            if(completed)Release(job,entry);
        }
    }
    private static void Release(string job,Entry entry)
    {
        entry.Releasing=true;
        try{entry.Request?.Dispose();entries.Remove(job);}
        catch(Exception error)
        {
            if(!entry.ReleaseWarning)Log.Warning("[Camper] Publication observation release for job "+job+": "+error.GetType().Name);
            entry.ReleaseWarning=true;
        }
    }
}
