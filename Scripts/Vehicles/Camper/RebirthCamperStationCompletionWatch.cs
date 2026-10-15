using System;
using System.Collections.Generic;

// Original event polling only. Unconfirmed completions remain held by their
// callsite and durable custody; no timeout unlocks output or issues rewards.
internal static class RebirthCamperStationCompletionWatch
{
    private sealed class Entry
    {
        internal RebirthCamperStationCompletionCapture.SuccessfulOutput Completed;
        internal RebirthCamperStationSaveEvidence.Request Request;
        internal RebirthCamperStationSaveEvidence.Publication Proof;
        internal bool CompletionSaved;
        internal RebirthCamperStationTrainingPlan Training;
        internal bool TrainingGranted;
        internal bool IntentSaved;
        internal RebirthCamperStationNativeRewardDelivery NativeReward;
        internal RebirthCamperStationReceiptRetirement Retirement;
        internal bool NativeRetired,RetirementDisposed,CompletionRequestDisposed,Archived;
        internal RebirthCamperStationCompletionArchive Archive;
        internal object Lease;
        internal bool HandingOff;
        internal float Deadline;
        internal bool Releasing,ReleaseWarning;
    }
    private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
    private static bool installed;private static float next;
    internal static bool IsWatching(string job)
        =>ThreadManager.IsMainThread()&&!string.IsNullOrEmpty(job)&&entries.ContainsKey(job);
    internal static void Install()
    {
        if(installed)return;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Tick));
        installed=true;
    }
    internal static bool TryWatch(RebirthCamperStationCompletionCapture.SuccessfulOutput completed)
    {
        if(!installed||!ThreadManager.IsMainThread()||completed==null||!completed.MatchesLiveOutput())return false;
        var job=completed.Binding.Admission.JobId;
        if(entries.TryGetValue(job,out var existing))return ReferenceEquals(existing.Completed,completed);
        if(entries.Count>=64)return false;
        try
        {
            var request=completed.Binding.IsCurrent()?RebirthCamperStationSaveEvidence.BeginCompletion(completed):null;
            RebirthStationLiveAccess.TryGetMobileLease(completed.Binding.Player,completed.Binding.Address,
                completed.Binding.Admission.CreationId,out _,out var lease);
            entries.Add(job,new Entry{Completed=completed,Request=request,Lease=lease,Deadline=UnityEngine.Time.realtimeSinceStartup+30f});
            return true;
        }
        catch{return false;}
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread()||entries.Count==0)return;
        float now=UnityEngine.Time.realtimeSinceStartup;if(now<next)return;next=now+.25f;
        foreach(var job in new List<string>(entries.Keys))
        {
            if(!entries.TryGetValue(job,out var entry))continue;
            if(entry.Releasing){Release(job,entry);continue;}
            try
            {
                // Output custody continues without an online owner. Native/Survivor
                // effects wait for the same authenticated owner to return.
                if(!entry.Completed.Binding.TryRefreshOnlinePlayer()){entry.Deadline=now+30f;continue;}
                if(entry.Request==null||entry.Request.Closed&&entry.Proof==null)
                {
                    entry.Request?.Dispose();
                    entry.Request=RebirthCamperStationSaveEvidence.BeginCompletion(entry.Completed);
                    entry.Deadline=now+30f;
                    RebirthStationLiveAccess.TryGetMobileLease(entry.Completed.Binding.Player,entry.Completed.Binding.Address,
                        entry.Completed.Binding.Admission.CreationId,out _,out entry.Lease);
                }
                if(entry.NativeRetired)
                {
                    if(!entry.RetirementDisposed){entry.Retirement.Dispose();entry.RetirementDisposed=true;}
                    if(!entry.CompletionRequestDisposed){entry.Request?.Dispose();entry.CompletionRequestDisposed=true;}
                    if(!entry.Archived&&!entry.HandingOff)
                    {
                        RebirthStationLiveAccess.TryGetMobileLease(entry.Completed.Binding.Player,entry.Completed.Binding.Address,
                            entry.Completed.Binding.Admission.CreationId,out _,out var currentLease);
                        entry.HandingOff=entry.Lease==null||!ReferenceEquals(entry.Lease,currentLease)||
                            entry.Archive!=null&&!entry.Archive.CanContinue;
                    }
                    if(entry.HandingOff)
                    {
                        entry.Archive?.Dispose();entries.Remove(job);continue;
                    }
                    if(!entry.Archived)
                    {
                        if(entry.Archive==null)
                        {
                            RebirthCamperStationCompletionArchive.TryBegin(entry.Completed.Binding.Player,job,out var archive);
                            entry.Archive=archive;
                        }
                        if(entry.Archive!=null&&!entry.Archive.CanContinue){entry.HandingOff=true;continue;}
                        if(entry.Archive==null||!entry.Archive.TryArchive())continue;
                        entry.Archived=true;
                    }
                    entry.Archive.Dispose();entries.Remove(job);continue;
                }
                if(entry.Retirement!=null)
                {
                    if(now>=entry.Deadline||!entry.Retirement.IsCurrent())
                    {Log.Warning("[Camper] Receipt retirement remains unconfirmed for job "+job);Release(job,entry);}
                    else if(entry.Retirement.TryConfirm())entry.NativeRetired=true;
                    continue;
                }
                if(now>=entry.Deadline||!entry.Completed.MatchesLiveOutput())
                {Log.Warning("[Camper] Completion remains held for job "+job+"; save observation expired or changed.");Release(job,entry);continue;}
                if(entry.Proof==null)
                {
                    if(RebirthCamperStationSaveEvidence.TryObserve(entry.Request,out var proof))entry.Proof=proof;
                    else if(entry.Request.Closed)
                    {Log.Warning("[Camper] Native completion save unconfirmed for job "+job+"; custody retained.");Release(job,entry);continue;}
                }
                if(entry.Proof!=null&&!entry.CompletionSaved)
                    entry.CompletionSaved=RebirthCamperStationCompletionService.TryRecord(entry.Completed,entry.Proof);
                if(!entry.CompletionSaved)continue;
                if(entry.Training==null)
                {
                    if(!RebirthCamperStationTrainingPlan.TryCreate(entry.Completed,out var plan))continue;
                    entry.Training=plan;
                }
                if(!entry.TrainingGranted)entry.TrainingGranted=RebirthCamperStationTrainingService.TryGrant(entry.Training);
                if(entry.TrainingGranted&&!entry.IntentSaved)
                    entry.IntentSaved=RebirthCamperStationCompletionIntentService.TrySave(entry.Training);
                if(!entry.IntentSaved)continue;
                if(entry.NativeReward==null)
                {
                    if(!RebirthCamperStationNativeRewardDelivery.TryBegin(entry.Training,out var delivery))continue;
                    entry.NativeReward=delivery;
                    entry.Deadline=now+30f;
                }
                if(entry.NativeReward.TryDeliver())
                {
                    RebirthCamperStationReceiptRetirement.TryBegin(entry.Training,entry.NativeReward,out var retirement);
                    entry.Retirement=retirement;
                    if(retirement!=null)entry.Deadline=now+30f;
                }
            }
            catch(Exception error)
            {
                Log.Warning("[Camper] Completion observation interrupted for job "+job+": "+error.GetType().Name);
                if(!entry.NativeRetired)Release(job,entry);
            }
        }
    }
    private static void Release(string job,Entry entry)
    {
        entry.Releasing=true;
        try{entry.Retirement?.Dispose();entry.Request?.Dispose();entries.Remove(job);}
        catch(Exception error)
        {
            if(!entry.ReleaseWarning)Log.Warning("[Camper] Completion observation release for job "+job+": "+error.GetType().Name);
            entry.ReleaseWarning=true;
        }
    }
}
