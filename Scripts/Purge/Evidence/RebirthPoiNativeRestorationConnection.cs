using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// One native UpdateSpawn restoration and its exact AddEnemyToWorld callback own this
// receipt. Ordinary spawns cannot claim it; expiry/unknown publication withholds proof.
internal static class RebirthPoiNativeRestorationConnection
{
    private sealed class Pending
    {
        internal World World;internal SleeperVolume Volume;internal RebirthPoiWorldStore Store;
        internal RebirthPoiNativeManifest Manifest;internal RebirthPoiNativeRestorationWitness Witness;
        internal RebirthPoiPartialObservation Observations;internal EntityAlive Entity;
    }
    private sealed class OriginalCall
    {
        internal World World;internal SleeperVolume Volume;internal RebirthPoiNativeRestorationWitness Witness;
        internal OriginalCall Previous;internal readonly List<EntityAlive> Callbacks=new List<EntityAlive>();
    }
    [ThreadStatic]private static OriginalCall active;
    private static readonly Dictionary<int,Pending> pending=new Dictionary<int,Pending>();
    private static readonly Queue<int> retry=new Queue<int>();
    private static double nextPulse;
    internal static void Reset(){pending.Clear();retry.Clear();nextPulse=0;active=null;}
    internal static bool HasPending(RebirthPoiIdentity identity){return pending.Values.Any(p=>p.Manifest.Identity.Equals(identity));}
    internal static RebirthPoiNativeRestorationWitness BeforeUpdate(World world,SleeperVolume volume)
    {
        // Match cheap native restoration prerequisites before resolving any authored POI.
        if(world==null || volume==null || volume.spawnDelay>1 || volume.spawnsAvailable==null ||
            volume.respawnList==null || volume.respawnList.Count==0)return null;
        int originalId=volume.respawnList[volume.respawnList.Count-1];
        if(world.GetEntity(originalId)!=null || volume.pendingSpawnMap.Contains(originalId))return null;
        if(!RebirthPurgeReleasePolicy.Enabled || pending.Count>=4096 || retry.Count>=8192 || !RebirthPoiWorldLifecycle.Instance.TryGetStore(out var store) ||
            !RebirthPoiNativeManifest.TryResolve(world,volume?.prefabInstance,out var manifest))return null;
        int id=world.FindSleeperVolume(volume.BoxMin,volume.BoxMax);
                if(!RebirthPoiNativeRestorationWitness.TryBegin(store,manifest,volume,id,out var witness))return null;
        active=new OriginalCall {World=world,Volume=volume,Witness=witness,Previous=active};return witness;
    }
    internal static void AfterUpdate(World world,SleeperVolume volume,RebirthPoiNativeRestorationWitness witness,Exception failure)
    {
        OriginalCall call=null;
        if(active!=null && ReferenceEquals(active.Witness,witness)){call=active;active=active.Previous;}
        if(witness==null || failure!=null || !witness.FinishOriginalUpdate() ||
            !RebirthPoiWorldLifecycle.Instance.TryGetStore(out var store) ||
            !RebirthPoiNativeManifest.TryResolve(world,volume.prefabInstance,out var manifest))return;
        int id=witness.SuccessorEntityId;if(id<0 || pending.ContainsKey(id))return;
        pending.Add(id,new Pending {World=world,Volume=volume,Store=store,Manifest=manifest,Witness=witness});retry.Enqueue(id);
        if(call!=null)foreach(var entity in call.Callbacks)
            if(entity.entityId==id)ActualSpawn(world,volume,entity);
    }
    // True means this callback belongs to an original restoration, even if publication
    // is temporarily pending. It must never also be enrolled as a fresh participant.
    internal static bool ActualSpawn(World world,SleeperVolume volume,EntityAlive entity)
    {
        if(entity==null)return false;
        if(!pending.TryGetValue(entity.entityId,out var original))
        {
            if(active!=null && ReferenceEquals(active.World,world) && ReferenceEquals(active.Volume,volume))
            {if(active.Callbacks.Count<4)active.Callbacks.Add(entity);return true;}
            return false;
        }
        if(!ReferenceEquals(original.World,world) || !ReferenceEquals(original.Volume,volume))return true;
        if(original.Observations==null)
        {
            if(!original.Witness.TryCompleteActualSpawn(entity,out var observations)){pending.Remove(entity.entityId);return true;}
            original.Observations=observations;original.Entity=entity;
            RebirthPoiNativeEvidence.Instance.NativeRestored(world,volume,entity,observations);
        }
        Publish(entity.entityId,original);return true;
    }
    private static void Publish(int id,Pending original)
    {
        var snapshot=original.Store.Published;
        if(snapshot==null || !snapshot.Binding.IsCurrent){pending.Remove(id);return;}
        if(original.Store.HasPending)return;
        var result=original.Store.TryObservePartial(snapshot,original.Manifest.Identity,original.Observations.Epoch,original.Observations);
        if(result==RebirthPoiStoreResult.Published || result==RebirthPoiStoreResult.Duplicate)
        {
            pending.Remove(id);
        }
        else if(result!=RebirthPoiStoreResult.Uncertain && result!=RebirthPoiStoreResult.IoFailure)pending.Remove(id);
    }
    internal static void Pulse()
    {
        double now=(double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
        if(now<nextPulse)return;nextPulse=now+0.5;
        int budget=Math.Min(4,retry.Count);
        while(budget-->0)
        {
            int id=retry.Dequeue();if(!pending.TryGetValue(id,out var original))continue;
            if(!ReferenceEquals(GameManager.Instance?.World,original.World) || original.Store.Published==null ||
                original.Observations==null && !original.Volume.respawnMap.ContainsKey(id)){pending.Remove(id);continue;}
            if(original.Observations!=null)Publish(id,original);
            if(pending.ContainsKey(id))retry.Enqueue(id);
        }
    }
}