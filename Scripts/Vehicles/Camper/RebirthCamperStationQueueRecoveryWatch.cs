using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// One local authoritative window at a time. Save retries keep the original
// operation; a timed-out attempt is not restarted until a new window lease.
internal static class RebirthCamperStationQueueRecoveryWatch
{
    private static bool installed;
    private static float next,deadline;
    private static object lease;
    private static readonly HashSet<string> attempted=new HashSet<string>(StringComparer.Ordinal);
    private static RebirthCamperStationQueueRecovery active;
    private static string activeJob;
    private static bool releaseFailureLogged;
    private static bool releasing;
    private sealed class RemoteEntry
    {
        internal EntityPlayer Player;
        internal RebirthCamperStationAddress Address;
        internal string Creation;
        internal object Lease;
        internal RebirthCamperStationQueueRecovery Recovery;
        internal float Deadline;
        internal bool Releasing;
    }
    private static readonly Dictionary<string,RemoteEntry> remote=new Dictionary<string,RemoteEntry>(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<object,HashSet<string>> remoteAttempts=new ConditionalWeakTable<object,HashSet<string>>();
    internal static bool TryObserveRemote(EntityPlayer player,string job)
    {
        if(!installed||!ThreadManager.IsMainThread()||player==null||player is EntityPlayerLocal||
            player.world==null||player.world.IsRemote()||string.IsNullOrEmpty(job)||
            !RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null||
            !owner.Progression.CamperStationPublications.ContainsKey(job)||
            !owner.Progression.StationPreparations.TryGetValue(job,out var admission)||admission==null||
            !RebirthCamperStationQueueRecovery.TryGetRecoveryLease(player,admission,out var station,out var address,out var currentLease)||currentLease==null)return false;
        if(remote.TryGetValue(job,out var retained))return ReferenceEquals(retained.Player,player)&&ReferenceEquals(retained.Lease,currentLease)&&!retained.Releasing;
        var attempts=remoteAttempts.GetValue(currentLease,_=>new HashSet<string>(StringComparer.Ordinal));
        if(attempts.Contains(job)||remote.Count>=64||RebirthCamperStationSaveEvidence.IsPending(station)||RebirthCamperStationCompletionCallsite.IsHeld(station))return false;
        attempts.Add(job);
        var entry=new RemoteEntry{Player=player,Address=address,Creation=admission.CreationId,Lease=currentLease,Deadline=UnityEngine.Time.realtimeSinceStartup+30f};
        remote.Add(job,entry); // Retain before native request creation, including false TryBegin with an original.
        RebirthCamperStationQueueRecovery.TryBegin(player,job,out entry.Recovery);
        if(entry.Recovery==null)entry.Releasing=true;
        return entry.Recovery!=null;
    }
    private static void TickRemote(float now)
    {
        foreach(var job in new List<string>(remote.Keys))
        {
            var entry=remote[job];
            try
            {
                if(!entry.Releasing)
                {
                    if(now>=entry.Deadline||!RebirthStationLiveAccess.TryGetMobileLease(entry.Player,entry.Address,entry.Creation,out _,out var current)||
                        !ReferenceEquals(current,entry.Lease)||entry.Recovery==null||!entry.Recovery.CanContinue)entry.Releasing=true;
                    else if(entry.Recovery.TryRecover())entry.Releasing=true;
                }
                if(entry.Releasing){entry.Recovery?.Dispose();remote.Remove(job);}
            }
            catch(Exception error){entry.Releasing=true;Log.Warning("[Camper] Remote queue recovery retained cleanup: "+error.GetType().Name);}
        }
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
            if(active!=null)
            {
                if(now>=deadline||!active.CanContinue)
                {Log.Warning("[Camper] Queue recovery remains held for job "+activeJob+"; lease/save observation expired or changed.");Release();}
                else if(active.TryRecover())Release();
                return;
            }
            if(lease==null||!RebirthWorldCharacterService.TryGet(player,out var owner)||owner?.Progression==null)return;
            // Another save/completion operation may temporarily own this station.
            // Waiting is not a failed recovery attempt for every queued job.
            if(!RebirthCamperStationWindowSession.TryGetCurrent(player,out var station,out _)||
                RebirthCamperStationSaveEvidence.IsPending(station)||RebirthCamperStationCompletionCallsite.IsHeld(station))return;
            foreach(var job in new List<string>(owner.Progression.CamperStationPublications.Keys))
            {
                if(attempted.Contains(job))continue;
                attempted.Add(job);
                RebirthCamperStationQueueRecovery.TryBegin(player,job,out var recovery);
                if(recovery!=null)
                {active=recovery;activeJob=job;releaseFailureLogged=false;deadline=now+30f;break;}
            }
        }
        catch(Exception error)
        {Log.Warning("[Camper] Queue recovery interrupted: "+error.GetType().Name);Release();}
    }
    private static bool Release()
    {
        try
        {active?.Dispose();active=null;activeJob=null;releaseFailureLogged=false;releasing=false;return true;}
        catch(Exception error)
        {
            deadline=0;
            releasing=true;
            if(!releaseFailureLogged)Log.Warning("[Camper] Recovery save-request release retained for retry: "+error.GetType().Name);
            releaseFailureLogged=true;
            return false;
        }
    }
}
