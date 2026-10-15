using System;
using System.Collections.Generic;

// Observe applied refunds after reload; never reconstruct an inventory permit.
internal static class RebirthCamperStationRefundRecoveryWatch
{
    private static bool installed;
    private static float next,deadline;
    private static object lease;
    private static string activeJob;
    private static RebirthCamperStationRefundRecovery active;
    private static bool terminal,releaseWarning,releasing;
    private static readonly HashSet<string> attempted=new HashSet<string>(StringComparer.Ordinal);
    private sealed class RemoteEntry
    {
        internal EntityPlayer Player;internal object Lease;internal RebirthCamperStationRefundRecovery Recovery;
        internal bool Terminal;
    }
    private static readonly Dictionary<string,RemoteEntry> remote=new Dictionary<string,RemoteEntry>(StringComparer.Ordinal);
    // Called by the authenticated server lease poll. No server EntityPlayer.bag
    // or host primary player participates in remote inventory authority.
    internal static bool TryObserveRemote(EntityPlayer player,string job)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||player is EntityPlayerLocal||string.IsNullOrEmpty(job)||
                RebirthCamperStationRemoteCancellationWatch.IsWatching(job)||
                !RebirthCamperRemoteStationTransport.TryGetCurrent(player,out var station,out var address,out var creation,out var currentLease)||
                !RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
                !RebirthWorldCharacterService.TryGetIdentity(player,out var identity)||identity==null)return false;
            RebirthStationGridAdmission admission=null;
            if(!owner.Progression.StationPreparations.TryGetValue(job,out admission))
            {
                if(!owner.Progression.StationRefundArchives.TryGetValue(job,out var history)||history==null||!history.IsMobile||history.IsCompletion||
                    !RebirthStationGridAdmission.TryReadStored(history.Write().Element("stationAdmission"),out admission))return false;
            }
            if(admission==null||admission.JobId!=job||admission.CreationId!=creation||
                !admission.TryGetMobileAddress(out var expected)||!address.Equals(expected))return false;
            string key=identity.StorageKey+":"+creation+":"+job;
            if(remote.TryGetValue(key,out var entry)&&(!ReferenceEquals(entry.Player,player)||!ReferenceEquals(entry.Lease,currentLease)))
            {
                entry.Recovery?.Dispose();remote.Remove(key);entry=null;
            }
            if(entry==null)
            {
                if(remote.Count>=64||RebirthCamperStationSaveEvidence.IsPending(station))return false;
                entry=new RemoteEntry{Player=player,Lease=currentLease};remote.Add(key,entry);
                if(!owner.Progression.StationRefundArchives.ContainsKey(job))
                {
                    RebirthCamperStationRefundRecovery.TryBegin(player,job,out var recovery);entry.Recovery=recovery;
                    if(recovery==null){remote.Remove(key);return false;}
                }
            }
            if(entry.Terminal){entry.Recovery?.Dispose();remote.Remove(key);return true;}
            entry.Recovery?.TryApply();
            if(!RebirthCamperStationRefundArchiveService.TryArchive(player,job,entry.Recovery))return false;
            entry.Terminal=true;entry.Recovery?.Dispose();remote.Remove(key);return true;
        }
        catch{return false;} // Retained custody or disposal can be retried by the same poll.
    }
    internal static void Install()
    {
        if(installed)return;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Tick));
        installed=true;
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread())return;
        float now=UnityEngine.Time.realtimeSinceStartup;if(now<next)return;next=now+.25f;
        try
        {
            if(releasing&&!Release())return;
            var player=GameManager.Instance?.World?.GetPrimaryPlayer();
            RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var currentLease);
            if(!ReferenceEquals(lease,currentLease))
            {
                if(!Release())return;
                lease=currentLease;attempted.Clear();
            }
            if(activeJob!=null)
            {
                if(terminal){Release();return;}
                if(RebirthCamperStationCancellationWatch.IsWatching(activeJob))return;
                // Only the recovery operation can authorize an unapplied refund
                // from explicit native before-image evidence. It refuses 1/2.
                active?.TryApply();
                // Archive retries remain valid after active records were retired.
                if(RebirthCamperStationRefundArchiveService.TryArchive(player,activeJob,active))
                {terminal=true;Release();return;}
                bool archived=RebirthWorldCharacterService.TryGet(player,out var owner)&&
                    owner?.Progression?.StationRefundArchives.ContainsKey(activeJob)==true;
                if(now>=deadline||!archived&&(active==null||!active.CanContinue))
                {
                    Log.Warning("[Camper] Refund recovery remains pending for job "+activeJob+"; current observation unavailable.");
                    Release();
                }
                return;
            }
            if(lease==null||!RebirthWorldCharacterService.TryGet(player,out var record)||record?.Progression==null)return;
            if(!RebirthCamperStationWindowSession.TryGetCurrent(player,out var station,out var address)||
                RebirthCamperStationSaveEvidence.IsPending(station))return;
            var jobs=new HashSet<string>(record.Progression.CamperStationRefundDeliveries.Keys,StringComparer.Ordinal);
            foreach(var archive in record.Progression.StationRefundArchives.Values)
                if(archive!=null&&archive.IsMobile&&!archive.IsCompletion)jobs.Add(archive.JobId);
            foreach(var job in jobs)
            {
                if(attempted.Contains(job)||RebirthCamperStationCancellationWatch.IsWatching(job))continue;
                RebirthStationGridAdmission admission=null;
                if(!record.Progression.StationPreparations.TryGetValue(job,out admission)&&
                    record.Progression.StationRefundArchives.TryGetValue(job,out var historical))
                    RebirthStationGridAdmission.TryReadStored(historical.Write().Element("stationAdmission"),out admission);
                if(admission==null||!admission.TryGetMobileAddress(out var expected)||!address.Equals(expected))continue;
                attempted.Add(job);
                if(record.Progression.StationRefundArchives.ContainsKey(job))
                {activeJob=job;deadline=now+30f;break;}
                RebirthCamperStationRefundRecovery.TryBegin(player,job,out var recovery);
                if(recovery!=null)
                {
                    active=recovery;activeJob=job;deadline=now+30f;attempted.Add(job);break;
                }
            }
        }
        catch(Exception error)
        {
            Log.Warning("[Camper] Refund recovery interrupted: "+error.GetType().Name);Release();
        }
    }
    private static bool Release()
    {
        releasing=true;
        try
        {
            active?.Dispose();active=null;activeJob=null;terminal=false;releaseWarning=false;releasing=false;return true;
        }
        catch(Exception error)
        {
            if(!releaseWarning)Log.Warning("[Camper] Refund observation release pending: "+error.GetType().Name);
            releaseWarning=true;return false;
        }
    }
}
