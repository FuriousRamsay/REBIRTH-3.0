using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;

// SleeperEventData is retained by QuestEventManager while Update returns false.
// Its native reset-only first phase is admitted before any volume is changed.
internal static class RebirthPoiSleeperEventResetOwner
{
    private sealed class Group
    {
        internal RebirthPoiNativeAuthoredManifest Authored;
        internal RebirthPoiResetBatchEntry Entry;
        internal RebirthPoiClearanceRecord Original;
        internal RebirthPoiNativeAuthoredManifest.Slot[] Slots;
        internal readonly List<Func<bool>> Unchanged=new List<Func<bool>>();
        internal readonly List<Action<Lease>> Untouched=new List<Action<Lease>>();
    }
    private sealed class Lease
    {
        internal SleeperEventData Event;internal World World;internal SleeperVolume[] Volumes;internal int[] Players;internal Guid Transaction=Guid.NewGuid();
        internal Group[] Groups;internal RebirthPoiResetBatchCallerProtocol Batch;
        internal bool Current
        {
            get{try{return leases.TryGetValue(Event,out var original)&&ReferenceEquals(original,this)&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&!Event.hasRefreshed&&Event.SleeperVolumes.SequenceEqual(Volumes)&&Event.EntityList.SequenceEqual(Players)&&QuestEventManager.Current.SleeperVolumeUpdateDictionary.TryGetValue(Event.position,out var registered)&&ReferenceEquals(registered,Event)&&(Groups==null||Groups.All(g=>g.Authored.IsOriginalAuthoredCurrent));}catch{return false;}}
        }
    }
    private static readonly Dictionary<SleeperEventData,Lease> leases=new Dictionary<SleeperEventData,Lease>();
    private static readonly Dictionary<SleeperEventData,double> retries=new Dictionary<SleeperEventData,double>();
    [ThreadStatic] private static Lease active;
    private static double Now=>(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    internal static void Reset(){leases.Clear();retries.Clear();active=null;}
    internal static bool TryStart(SleeperEventData data)
    {
        if(leases.ContainsKey(data))return true;
        if(retries.TryGetValue(data,out var after)&&Now<after)return false;
        foreach(var expired in retries.Keys.Where(key=>!QuestEventManager.Current.SleeperVolumeUpdateDictionary.TryGetValue(key.position,out var original)||!ReferenceEquals(original,key)).ToArray())retries.Remove(expired);
        if(retries.Count>=128&&!retries.ContainsKey(data))return false;
        if(leases.Count>=128||data.SleeperVolumes.Count<1||data.SleeperVolumes.Count>4096||data.SleeperVolumes.Any(v=>v==null)||data.SleeperVolumes.Distinct().Count()!=data.SleeperVolumes.Count)return false;
        var lease=new Lease{Event=data,World=GameManager.Instance?.World,Volumes=data.SleeperVolumes.ToArray(),Players=data.EntityList.ToArray()};
        leases.Add(data,lease);
        if(!lease.Current){leases.Remove(data);return false;}
        try{ThreadManager.StartCoroutine(Run(lease));return true;}catch{leases.Remove(data);return false;}
    }
    private static bool Capture(Lease lease,RebirthPoiWorldStore store)
    {
        var snapshot=store.Published;var groups=new List<Group>();
        foreach(var byPrefab in lease.Volumes.GroupBy(v=>v.prefabInstance))
        {
            RebirthPoiNativeAuthoredManifest authored;RebirthPoiClearanceRecord record;
            if(!RebirthPoiNativeAuthoredManifest.TryCapture(snapshot.Binding,lease.World,byPrefab.Key,out authored)||!snapshot.TryGet(authored.Identity,out record)||record.State==RebirthPoiClearanceState.ResetPending)return false;
            var slots=authored.Slots.Where(s=>s.Expected.Kind==RebirthPoiAuthoredResetKind.Sleeper&&byPrefab.Any(v=>ReferenceEquals(v,s.OriginalRuntime))).ToArray();
            if(slots.Length!=byPrefab.Count()||slots.Any(s=>!s.Expected.OriginalNativeId.HasValue))return false;
            var plan=new RebirthPoiResetPlan(lease.Transaction,RebirthPoiResetCaller.Event,authored.Digest,Array.Empty<long>(),slots.Select(s=>s.Expected.OriginalNativeId.Value),Array.Empty<int>(),slots.Select(s=>s.Expected));
            var group=new Group{Authored=authored,Original=record,Slots=slots,Entry=new RebirthPoiResetBatchEntry(authored.Identity,plan)};
            if(record.Observations!=null)
            foreach(var old in record.Observations.Volumes.Values.Where(v=>!slots.Any(s=>s.Expected.Descriptor==v.Descriptor)))
            {
                var slot=authored.Slots.SingleOrDefault(s=>s.Expected.Kind==RebirthPoiAuthoredResetKind.Sleeper&&s.Expected.Descriptor==old.Descriptor&&s.Expected.OriginalNativeId==old.NativeVolumeId);
                if(slot==null||!(slot.OriginalRuntime is SleeperVolume volume))return false;
                int count=volume.numSpawned;bool spawning=volume.isSpawning,cleared=volume.wasCleared;var membership=volume.respawnMap.ToArray();var pending=volume.pendingSpawnMap.ToArray();var operations=volume.pendingSpawnOps.ToArray();
                group.Unchanged.Add(()=>ReferenceEquals(lease.World.GetSleeperVolume(old.NativeVolumeId),volume)&&volume.numSpawned==count&&volume.isSpawning==spawning&&volume.wasCleared==cleared&&volume.respawnMap.Count==membership.Length&&membership.All(p=>volume.respawnMap.TryGetValue(p.Key,out var value)&&value.className==p.Value.className&&value.spawnPointIndex==p.Value.spawnPointIndex)&&volume.pendingSpawnMap.OrderBy(i=>i).SequenceEqual(pending.OrderBy(i=>i))&&volume.pendingSpawnOps.SequenceEqual(operations));
                group.Untouched.Add(owner=>owner.Batch.UnaffectedVolumeVerified(authored.Identity,lease.World,lease.Transaction,old.NativeVolumeId,old.Descriptor,record.Observations.EffectiveGeneration(old)));
            }
            groups.Add(group);
        }
        lease.Groups=groups.ToArray();return lease.Current;
    }
    private static IEnumerator Run(Lease lease)
    {
        double deadline=Now+90;bool complete=false;
        try
        {
            RebirthPoiWorldStore store=null;
            while(lease.Current&&Now<deadline&&!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store))yield return null;
            if(store==null||!lease.Current)yield break;
            foreach(var prefab in lease.Volumes.Select(v=>v.prefabInstance).Distinct())
            {
                RebirthPoiNativeAuthoredManifest authored;
                if(!RebirthPoiNativeAuthoredManifest.TryCapture(store.Published.Binding,lease.World,prefab,out authored))yield break;
                while(lease.Current&&Now<deadline)
                {
                    var snapshot=store.Published;RebirthPoiClearanceRecord record;
                    if(snapshot.TryGet(authored.Identity,out record))break;
                    if(!store.HasPending)store.TryDiscover(snapshot,authored.Identity,!authored.Slots.Any(s=>s.Expected.Combat));
                    yield return null;
                }
                if(!lease.Current||Now>=deadline)yield break;
            }
            if(!Capture(lease,store))yield break;
            lease.Batch=new RebirthPoiResetBatchCallerProtocol(store,store.Published,lease.Groups.Select(g=>g.Entry),Native(lease),()=>Now);
            while(lease.Current&&Now<deadline&&!lease.Batch.TryAdmitBeforeMutation())
            {if(lease.Batch.State==RebirthPoiResetProtocolState.Unknown||lease.Batch.State==RebirthPoiResetProtocolState.Refused)yield break;yield return null;}
            while(lease.Current&&Now<deadline)
            {
                if(lease.Batch.State==RebirthPoiResetProtocolState.Unknown||lease.Batch.State==RebirthPoiResetProtocolState.Refused)yield break;
                var prior=active;active=lease;bool more;
                try{more=lease.Batch.MoveNext();}finally{active=prior;}
                if(!more)break;yield return lease.Batch.Current;
            }
            RebirthPoiResetBatchCompletion receipt;
            if(lease.Current&&lease.Batch.TryGetCompleted(out receipt)&&ReferenceEquals(receipt.Scope,lease.World)&&receipt.World==store.Published.Binding.WorldId&&receipt.Transaction==lease.Transaction){lease.Event.hasRefreshed=true;complete=true;}
        }
        finally
        {
            if(leases.TryGetValue(lease.Event,out var original)&&ReferenceEquals(original,lease))leases.Remove(lease.Event);
            if(lease.Batch!=null){lease.Batch.Dispose();ThreadManager.StartCoroutine(Drain(lease.Batch));}
            if(!complete)retries[lease.Event]=Now+10;else retries.Remove(lease.Event);
            if(!complete&&ReferenceEquals(GameManager.Instance?.World,lease.World))Log.Warning("[REBIRTH Purge] Sleeper event reset withheld; original event remains unfinished.");
        }
    }
    private static IEnumerator Drain(RebirthPoiResetBatchCallerProtocol batch)
    {double deadline=Now+30;while(Now<deadline&&!batch.PollCancellation())yield return null;}
    private static IEnumerator Native(Lease lease)
    {
        foreach(var group in lease.Groups)
        foreach(var slot in group.Slots)
        {
            if(!lease.Current||group.Unchanged.Any(check=>!check())){lease.Batch.AbortUncertain();yield break;}
            if(group.Original.Observations!=null)
            foreach(var old in group.Original.Observations.Volumes.Values.Where(v=>v.Descriptor==slot.Expected.Descriptor))
            foreach(var actor in old.Actors.Values.Where(a=>!a.Dead))
                lease.Batch.PriorActorOutcomeVerified(group.Authored.Identity,lease.World,lease.Transaction,new RebirthPoiResetActorOutcome(actor.Token,actor.EntityId,group.Original.Epoch,group.Original.Observations.EffectiveGeneration(old),old.Descriptor,actor.CausalDigest,RebirthPoiPriorActorDisposition.RetainedUnresolved,Guid.NewGuid()));
            ((SleeperVolume)slot.OriginalRuntime).DespawnAndReset(lease.World);
            yield return null;
        }
        foreach(var group in lease.Groups)
        {
            if(group.Unchanged.Any(check=>!check())){lease.Batch.AbortUncertain();yield break;}
            foreach(var receipt in group.Untouched)receipt(lease);
        }
    }
    internal static void Observed(SleeperVolume volume,World world,bool originalRan)
    {
        var lease=active;if(lease==null)return;
        try
        {
            var group=lease.Groups.SingleOrDefault(g=>g.Slots.Any(s=>ReferenceEquals(s.OriginalRuntime,volume)));var slot=group?.Slots.SingleOrDefault(s=>ReferenceEquals(s.OriginalRuntime,volume));
            int id;object runtime;
            bool reset=originalRan&&lease.Current&&ReferenceEquals(world,lease.World)&&slot!=null&&slot.TryReadRuntime(out id,out runtime)&&ReferenceEquals(runtime,volume)&&RebirthPoiNativeResetPostconditions.Volume(volume,originalRan);
            if(!reset){lease.Batch.AbortUncertain();return;}
            lease.Batch.AuthoredEffectReset(group.Authored.Identity,world,lease.Transaction,new RebirthPoiAuthoredRuntimeBinding(slot.Expected.Key,slot.Expected.Descriptor,slot.Expected.OriginalNativeId.Value,Guid.NewGuid()));
        }
        catch(Exception error){lease.Batch.AbortUncertain(error);}
    }
}
[HarmonyPatch(typeof(SleeperEventData),nameof(SleeperEventData.Update))]
internal static class RebirthPoiSleeperEventResetHook
{
    private static bool Prefix(SleeperEventData __instance,ref bool __result)
    {
        if(!RebirthPurgeReleasePolicy.Enabled||!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled||__instance.hasRefreshed||__instance.SleeperVolumes.Count==0)return true;
        RebirthPoiSleeperEventResetOwner.TryStart(__instance);__result=false;return false;
    }
}
[HarmonyPatch(typeof(SleeperVolume),nameof(SleeperVolume.DespawnAndReset))]
internal static class RebirthPoiSleeperEventVolumeReceiptHook
{internal static void Postfix(SleeperVolume __instance,World __0,bool __runOriginal){RebirthPoiSleeperEventResetOwner.Observed(__instance,__0,__runOriginal);}}