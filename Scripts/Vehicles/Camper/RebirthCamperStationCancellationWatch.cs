using System;
using System.Collections.Generic;

// Keeps original operation objects, including a failed begin which acquired custody.
// Timeouts report a pending cancellation; they never unlock or repeat payment.
internal static class RebirthCamperStationCancellationWatch
{
    private sealed class Entry
    {
        internal RebirthCamperStationCancellationCheckpoint Checkpoint;
        internal RebirthCamperStationCancellationSave Save;
        internal RebirthCamperStationCancellationRemoval Removal;
        internal RebirthCamperStationRefundDelivery Delivery;
        internal RebirthCamperStationRefundDeliveryRecord Saved;
        internal RebirthCamperStationRefundRecovery Recovery;
        internal bool Released,Archived;
        internal object Lease;
        internal bool HandingOff;
        internal float WarningAt;
        internal bool Warned;
    }
    private static readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
    private static bool installed;
    private static float next;
    internal static bool IsWatching(string job)
        =>ThreadManager.IsMainThread()&&!string.IsNullOrEmpty(job)&&entries.ContainsKey(job);
    internal static void Install()
    {
        if(installed)return;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Tick));
        installed=true;
    }
    internal static bool TryWatch(RebirthCamperStationQueuedAuthority.Binding binding)
    {
        if(!installed||!ThreadManager.IsMainThread()||binding==null||entries.Count>=64)return false;
        string job=binding.Admission.JobId;
        if(entries.ContainsKey(job))return false;
        foreach(var entry in entries.Values)
            if(ReferenceEquals(entry.Checkpoint.Binding.Station,binding.Station))return false;
        if(!RebirthCamperStationCancellationCheckpoint.TryCreate(binding,out var checkpoint)||!HasRoom(checkpoint))return false;
        // Retain before any checkpoint/queue/inventory side effect.
        RebirthCamperStationWindowSession.TryGetLeaseIdentity(binding.Player,out var lease);
        entries.Add(job,new Entry{Checkpoint=checkpoint,Lease=lease,WarningAt=UnityEngine.Time.realtimeSinceStartup+30f});
        return true;
    }
    private static bool HasRoom(RebirthCamperStationCancellationCheckpoint checkpoint)
    {
        if(!(checkpoint.Binding.Player is EntityPlayerLocal player)||!ReferenceEquals(player.world?.GetPrimaryPlayer(),player))return false;
        var bag=player.bag;var grid=bag?.ItemGrid;var slots=grid?.items;
        if(slots==null)return false;
        Func<int,bool> locked=slot=>bag.LockedSlots!=null&&slot<bag.LockedSlots.Length&&bag.LockedSlots[slot];
        return RebirthStationRefundBackpackPlan.TryCreate(slots,slots.Length,locked,checkpoint.CopyRefund(),out var plan)&&
            ReferenceEquals(player.bag,bag)&&ReferenceEquals(bag.ItemGrid,grid)&&ReferenceEquals(grid.items,slots)&&
            plan.MatchesBefore(slots,slots.Length,locked);
    }
    private static void Tick(ref ModEvents.SGameUpdateData data)
    {
        if(!ThreadManager.IsMainThread()||entries.Count==0)return;
        float now=UnityEngine.Time.realtimeSinceStartup;if(now<next)return;next=now+.25f;
        foreach(var job in new List<string>(entries.Keys))
        {
            var entry=entries[job];
            try
            {
                if(Advance(entry)){entries.Remove(job);continue;}
            }
            catch(Exception error)
            {
                if(!entry.Warned)Log.Warning("[Camper] Cancellation pending for job "+job+": "+error.GetType().Name);
                entry.Warned=true;
            }
            if(now>=entry.WarningAt&&!entry.Warned)
            {
                Log.Warning("[Camper] Cancellation pending for job "+job+"; original custody retained.");
                entry.Warned=true;
            }
        }
    }
    private static bool Advance(Entry entry)
    {
        if(entry.Released)
        {
            if(!entry.Archived&&!entry.HandingOff)
            {
                RebirthCamperStationWindowSession.TryGetLeaseIdentity(entry.Checkpoint.Binding.Player,out var currentLease);
                entry.HandingOff=entry.Lease==null||!ReferenceEquals(entry.Lease,currentLease)||
                    entry.Recovery!=null&&!entry.Recovery.CanContinue&&
                    !entry.Checkpoint.Binding.Progression.StationRefundArchives.ContainsKey(entry.Checkpoint.Binding.Admission.JobId);
            }
            if(entry.HandingOff){entry.Recovery?.Dispose();return true;}
            if(!entry.Archived)
            {
                if(entry.Recovery==null)
                {
                    RebirthCamperStationRefundRecovery.TryBegin(entry.Checkpoint.Binding.Player,
                        entry.Checkpoint.Binding.Admission.JobId,out var recovery);
                    entry.Recovery=recovery;
                }
                if(entry.Recovery!=null&&!entry.Recovery.CanContinue&&
                    !entry.Checkpoint.Binding.Progression.StationRefundArchives.ContainsKey(entry.Checkpoint.Binding.Admission.JobId))
                {entry.HandingOff=true;return false;}
                // The archive service also resumes its own retained cached archive
                // after a failed flush; no new credit or reconstructed permit.
                entry.Archived=RebirthCamperStationRefundArchiveService.TryArchive(entry.Checkpoint.Binding.Player,
                    entry.Checkpoint.Binding.Admission.JobId,entry.Recovery);
                if(!entry.Archived)return false;
            }
            entry.Recovery?.Dispose();return true;
        }
        if(entry.Save==null)
        {
            if(!HasRoom(entry.Checkpoint))return false;
            RebirthCamperStationCancellationSave.TryBegin(entry.Checkpoint,out var saved);
            entry.Save=saved;
        }
        if(entry.Save==null)return false;
        if(entry.Removal==null)
        {
            if(!entry.Save.TrySave())return false;
            // Full backpack before queue removal preserves the paid queue.
            if(!HasRoom(entry.Checkpoint))return false;
            RebirthCamperStationCancellationRemoval.TryBegin(entry.Checkpoint,entry.Save,out var removal);
            entry.Removal=removal;
        }
        if(entry.Saved!=null)
        {entry.Released=entry.Removal.TryReleaseSettled(entry.Delivery);return false;}
        if(entry.Removal==null||!entry.Removal.TryConfirm()||!entry.Removal.TrySavePublication())return false;
        if(entry.Delivery==null)
        {
            RebirthCamperStationRefundDelivery.TryBegin(entry.Checkpoint,entry.Removal,out var delivery);
            entry.Delivery=delivery;
        }
        if(entry.Delivery==null)return false;
        if(entry.Saved==null)
        {
            entry.Delivery.TryApply(); // original delivery internally consumes at most one application permit
            if(!entry.Delivery.TryGetSaved(out var saved))return false;
            entry.Saved=saved;
        }
        // Terminal release makes delivery.Current false; do not reconfirm afterwards.
        entry.Released=entry.Removal.TryReleaseSettled(entry.Delivery);return false;
    }
}
