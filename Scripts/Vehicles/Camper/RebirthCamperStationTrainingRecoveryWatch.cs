using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// One local authoritative window at a time. Save retries keep the original
// operation; a timed-out attempt is not restarted until a new window lease.
internal static class RebirthCamperStationTrainingRecoveryWatch
{
    private static bool installed;
    private static float next,deadline;
    private static object lease;
    private static readonly HashSet<string> attempted=new HashSet<string>(StringComparer.Ordinal);
    private static RebirthCamperStationTrainingRecovery active;
    private static string activeJob;
    private static bool releaseFailureLogged;
    private static bool archiving,activeDisposed,archived,releasing;
    private static RebirthCamperStationCompletionArchive archive;
    private sealed class RemoteEntry
    {
        internal EntityPlayer Player;
        internal RebirthCamperStationAddress Address;
        internal string Creation;
        internal object Lease;
        internal RebirthCamperStationTrainingRecovery Recovery;
        internal RebirthCamperStationCompletionArchive Archive;
        internal bool Archiving,RecoveryDisposed,Archived,Releasing;
        internal float Deadline;
    }
    private static readonly Dictionary<string,RemoteEntry> remote=new Dictionary<string,RemoteEntry>(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<object,HashSet<string>> remoteAttempts=new ConditionalWeakTable<object,HashSet<string>>();
    // Root remote open/poll dispatcher supplies only its authenticated owner's jobs.
    internal static bool TryObserveRemote(EntityPlayer player,string job)
    {
        if(!installed||!ThreadManager.IsMainThread()||player==null||player is EntityPlayerLocal||string.IsNullOrEmpty(job)||
            player.world==null||player.world.IsRemote()||RebirthCamperStationCompletionWatch.IsWatching(job)||
            !RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null)return false;
        RebirthStationGridAdmission admission=null;
        bool cached=owner.Progression.StationRefundArchives.TryGetValue(job,out var history)&&history!=null&&history.IsCompletion;
        if(!cached&&!owner.Progression.CamperStationTrainingPlans.ContainsKey(job))return false;
        if(cached)RebirthStationGridAdmission.TryReadStored(history.Write().Element("stationAdmission"),out admission);
        else owner.Progression.StationPreparations.TryGetValue(job,out admission);
        if(admission==null||!admission.TryGetMobileAddress(out var address)||
            !RebirthStationLiveAccess.TryGetMobileLease(player,address,admission.CreationId,out var station,out var currentLease)||currentLease==null)return false;
        if(remote.TryGetValue(job,out var existing))
            return ReferenceEquals(existing.Player,player)&&ReferenceEquals(existing.Lease,currentLease)&&!existing.Releasing;
        var attemptedJobs=remoteAttempts.GetValue(currentLease,_=>new HashSet<string>(StringComparer.Ordinal));
        if(attemptedJobs.Contains(job))return false;
        if(remote.Count>=64||RebirthCamperStationSaveEvidence.IsPending(station))return false;
        remote.Add(job,new RemoteEntry{Player=player,Address=address,Creation=admission.CreationId,Lease=currentLease,
            Archiving=cached,RecoveryDisposed=cached,Deadline=UnityEngine.Time.realtimeSinceStartup+30f});
        attemptedJobs.Add(job);
        return true;
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
        TickRemote(now);
        try
        {
            if(releasing&&!Release())return;
            var player=GameManager.Instance?.World?.GetPrimaryPlayer();
            RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var currentLease);
            if(!ReferenceEquals(lease,currentLease))
            {if(!Release())return;lease=currentLease;attempted.Clear();}
            if(archiving)
            {
                if(!activeDisposed){active?.Dispose();activeDisposed=true;}
                if(!archived)
                {
                    if(archive==null)
                    {
                        RebirthCamperStationCompletionArchive.TryBegin(player,activeJob,out var retained);
                        archive=retained;
                    }
                    if(archive==null||!archive.TryArchive())return;
                    archived=true;
                }
                Release();return;
            }
            if(active!=null)
            {
                if(now>=deadline||!active.CanContinue)
                {Log.Warning("[Camper] Training recovery remains held for job "+activeJob+"; lease/save observation expired or changed.");Release();}
                else if(active.TryGrant()&&active.HasRetired)archiving=true;
                return;
            }
            if(lease==null||!RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null)return;
            if(!RebirthCamperStationWindowSession.TryGetCurrent(player,out var station,out var address)||
                RebirthCamperStationSaveEvidence.IsPending(station))return;
            var jobs=new HashSet<string>(owner.Progression.CamperStationTrainingPlans.Keys,StringComparer.Ordinal);
            foreach(var history in owner.Progression.StationRefundArchives.Values)
                if(history!=null&&history.IsCompletion)jobs.Add(history.JobId);
            foreach(var job in jobs)
            {
                if(attempted.Contains(job)||RebirthCamperStationCompletionWatch.IsWatching(job))continue;
                RebirthStationGridAdmission admission=null;
                if(!owner.Progression.StationPreparations.TryGetValue(job,out admission)&&
                    owner.Progression.StationRefundArchives.TryGetValue(job,out var history))
                    RebirthStationGridAdmission.TryReadStored(history.Write().Element("stationAdmission"),out admission);
                if(admission==null||!admission.TryGetMobileAddress(out var expected)||!address.Equals(expected))continue;
                attempted.Add(job);
                if(owner.Progression.StationRefundArchives.ContainsKey(job))
                {activeJob=job;archiving=true;activeDisposed=true;break;}
                RebirthCamperStationTrainingRecovery.TryBegin(player,job,out var recovery);
                if(recovery!=null)
                {active=recovery;activeJob=job;releaseFailureLogged=false;deadline=now+30f;break;}
            }
        }
        catch(Exception error)
        {Log.Warning("[Camper] Training recovery interrupted: "+error.GetType().Name);Release();}
    }
    private static void TickRemote(float now)
    {
        foreach(var job in new List<string>(remote.Keys))
        {
            var entry=remote[job];
            try
            {
                if(entry.Releasing){if(ReleaseRemote(entry))remote.Remove(job);continue;}
                if(!RebirthStationLiveAccess.TryGetMobileLease(entry.Player,entry.Address,entry.Creation,out var station,out var currentLease)||
                    !ReferenceEquals(entry.Lease,currentLease)||now>=entry.Deadline)
                {entry.Releasing=true;if(ReleaseRemote(entry))remote.Remove(job);continue;}
                if(RebirthCamperStationCompletionWatch.IsWatching(job))continue;
                if(!entry.Archiving)
                {
                    if(entry.Recovery==null)
                    {
                        if(RebirthCamperStationSaveEvidence.IsPending(station))continue;
                        RebirthCamperStationTrainingRecovery.TryBegin(entry.Player,job,out var retained);
                        entry.Recovery=retained;
                    }
                    if(entry.Recovery==null)continue;
                    if(!entry.Recovery.CanContinue)
                    {entry.Releasing=true;if(ReleaseRemote(entry))remote.Remove(job);continue;}
                    if(!entry.Recovery.TryGrant()||!entry.Recovery.HasRetired)continue;
                    entry.Archiving=true;entry.Deadline=now+30f;
                }
                if(!entry.RecoveryDisposed){entry.Recovery?.Dispose();entry.RecoveryDisposed=true;}
                if(!entry.Archived)
                {
                    if(entry.Archive==null)
                    {
                        RebirthCamperStationCompletionArchive.TryBegin(entry.Player,job,out var retained);
                        entry.Archive=retained;
                    }
                    if(entry.Archive==null||!entry.Archive.TryArchive())continue;
                    entry.Archived=true;
                }
                entry.Releasing=true;if(ReleaseRemote(entry))remote.Remove(job);
            }
            catch(Exception error)
            {
                Log.Warning("[Camper] Remote completion observation retained for job "+job+": "+error.GetType().Name);
                // Cleanup retries are exclusive, including after archive success.
                if(entry.Archived)entry.Releasing=true;
            }
        }
    }
    private static bool ReleaseRemote(RemoteEntry entry)
    {
        try
        {
            entry.Archive?.Dispose();if(!entry.RecoveryDisposed)entry.Recovery?.Dispose();
            return true;
        }
        catch{return false;}
    }
    private static bool Release()
    {
        releasing=true;
        try
        {
            archive?.Dispose();if(!activeDisposed)active?.Dispose();
            active=null;activeJob=null;archive=null;archiving=false;activeDisposed=false;archived=false;
            releaseFailureLogged=false;releasing=false;return true;
        }
        catch(Exception error)
        {
            deadline=0;
            if(!releaseFailureLogged)Log.Warning("[Camper] Recovery save-request release retained for retry: "+error.GetType().Name);
            releaseFailureLogged=true;
            return false;
        }
    }
}
