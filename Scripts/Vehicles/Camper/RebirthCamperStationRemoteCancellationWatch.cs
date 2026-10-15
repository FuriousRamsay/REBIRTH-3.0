using System;
using System.Collections.Generic;

// Original authenticated remote cancellation operations, retained before effects.
internal static class RebirthCamperStationRemoteCancellationWatch
{
    private sealed class Entry
    {
        internal RebirthCamperStationCancellationCheckpoint Checkpoint;
        internal RebirthCamperStationCancellationSave Save;
        internal RebirthCamperStationCancellationRemoval Removal;
        internal RebirthCamperStationRemoteRefundDelivery Delivery;
        internal object Lease;
        internal bool Released;
        internal float WarningAt;
        internal bool Warned;
    }
    private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
    private static bool installed;
    private static float next;
    internal static bool IsWatching(string job)=>ThreadManager.IsMainThread()&&!string.IsNullOrEmpty(job)&&entries.ContainsKey(job);
    internal static void Install()
    {
        if(installed)return;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Tick));installed=true;
    }
    internal static bool Handle(EntityPlayer player,RebirthCamperStationAddress address,string creation,object lease,Guid job)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||player is EntityPlayerLocal||job==Guid.Empty||
                !RebirthCamperRemoteStationTransport.IsCurrent(player,address,creation,lease)||
                !RebirthCamperRemoteStationTransport.TryResolve(player,address,creation,out var station,out var currentLease)||
                !ReferenceEquals(currentLease,lease))return false;
            string key=job.ToString("N");
            if(entries.TryGetValue(key,out var prior))
                return ReferenceEquals(prior.Lease,lease)&&ReferenceEquals(prior.Checkpoint.Binding.Player,player)&&prior.Checkpoint.Binding.Address.Equals(address)&&
                    prior.Checkpoint.Binding.Admission.CreationId==creation;
            if(entries.Count>=64||station.Queue==null)return false;
            foreach(var entry in entries.Values)if(ReferenceEquals(entry.Checkpoint.Binding.Station,station))return false;
            RebirthCamperStationQueuedAuthority.Binding binding=null;
            foreach(var row in station.Queue)
            {
                if(row==null)return false;
                if(RebirthStationGridQueue.IsMarked(row.Recipe)&&RebirthStationGridQueue.TryGetJobId(row.Recipe,out var rowJob)&&rowJob==key)
                {
                    if(binding!=null||!RebirthCamperStationQueuedAuthority.TryGetCurrent(station,row,out binding))return false;
                }
            }
            if(binding==null||!ReferenceEquals(binding.Player,player)||!binding.Address.Equals(address)||binding.Admission.CreationId!=creation||
                !RebirthCamperStationCancellationCheckpoint.TryCreate(binding,out var checkpoint)||
                !RebirthCamperStationRemoteRefundDelivery.HasRoom(checkpoint))return false;
            Install();entries.Add(key,new Entry{Checkpoint=checkpoint,Lease=lease,WarningAt=UnityEngine.Time.realtimeSinceStartup+30f});return true;
        }
        catch{return false;}
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread()||entries.Count==0)return;
        float now=UnityEngine.Time.realtimeSinceStartup;if(now<next)return;next=now+.25f;
        foreach(var job in new List<string>(entries.Keys))
        {
            var entry=entries[job];
            try{if(Advance(entry)){entries.Remove(job);continue;}}
            catch(Exception error){if(!entry.Warned)Log.Warning("[Camper] Remote cancellation retained for "+job+": "+error.GetType().Name);entry.Warned=true;}
            if(now>=entry.WarningAt&&!entry.Warned){Log.Warning("[Camper] Remote cancellation pending for "+job+"; custody retained.");entry.Warned=true;}
        }
    }
    private static bool Advance(Entry entry)
    {
        if(entry.Released)return entry.Delivery!=null&&entry.Delivery.TryArchive();
        if(entry.Save==null)
        {
            if(!RebirthCamperStationRemoteRefundDelivery.HasRoom(entry.Checkpoint))return false;
            RebirthCamperStationCancellationSave.TryBegin(entry.Checkpoint,out var save);entry.Save=save;
        }
        if(entry.Save==null)return false;
        if(entry.Removal==null)
        {
            if(!entry.Save.TrySave()||!RebirthCamperStationRemoteRefundDelivery.HasRoom(entry.Checkpoint))return false;
            RebirthCamperStationCancellationRemoval.TryBegin(entry.Checkpoint,entry.Save,out var removal);entry.Removal=removal;
        }
        if(entry.Removal==null||!entry.Removal.TryConfirm()||!entry.Removal.TrySavePublication())return false;
        if(entry.Delivery==null)
        {
            RebirthCamperStationRemoteRefundDelivery.TryBegin(entry.Checkpoint,entry.Removal,out var delivery);entry.Delivery=delivery;
        }
        if(entry.Delivery==null)return false;
        entry.Delivery.TryApply();
        if(!entry.Delivery.TryGetSaved(out _))return false;
        entry.Released=entry.Removal.TryReleaseSettled(entry.Delivery);return false;
    }
}
