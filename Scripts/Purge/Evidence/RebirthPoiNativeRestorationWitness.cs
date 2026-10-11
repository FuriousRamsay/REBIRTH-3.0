using System;
using System.Collections.Generic;
using System.Linq;

// Passive observer of the exact qualified native restoration branch. It does not
// activate sleepers, mutate native spawn membership, or authorize reload clearing.
internal sealed class RebirthPoiNativeRestorationWitness
{
    private readonly RebirthPoiWorldStore store;
    private readonly RebirthPoiWorldSnapshot predecessor;
    private readonly RebirthPoiClearanceRecord record;
    private readonly RebirthPoiNativeManifest manifest;
    private readonly SleeperVolume volume;
    private readonly RebirthPoiActorObservation actor;
    private readonly HashSet<int> initialIds;
    private readonly int volumeId,initialSpawned,initialTick,initialPending;
    private readonly Guid receipt=Guid.NewGuid();
    private int successor=-1;
    private bool completedUpdate,failed;
    private RebirthPoiNativeRestorationWitness(RebirthPoiWorldStore owner,RebirthPoiWorldSnapshot snapshot,RebirthPoiClearanceRecord original,RebirthPoiNativeManifest mapping,SleeperVolume nativeVolume,int nativeId,RebirthPoiActorObservation participant)
    {store=owner;predecessor=snapshot;record=original;manifest=mapping;volume=nativeVolume;volumeId=nativeId;actor=participant;initialIds=new HashSet<int>(volume.respawnMap.Keys);initialSpawned=volume.numSpawned;initialTick=SleeperVolume.TickSpawnCount;initialPending=volume.pendingSpawnMap.Count;}
    private bool Current()
    {
        RebirthPoiClearanceRecord current;string descriptor;
        return !failed&&ThreadManager.IsMainThread()&&predecessor.Binding.IsCurrent&&ReferenceEquals(predecessor.Binding.Scope,manifest.World)&&manifest.StillMatches()&&
            store.Published!=null&&store.Published.TryGet(record.Identity,out current)&&current.Epoch==record.Epoch&&
            current.State==RebirthPoiClearanceState.Discovered&&current.Observations!=null&&current.Observations.Canonical==record.Observations.Canonical&&
            ReferenceEquals(manifest.World.GetSleeperVolume(volumeId),volume)&&manifest.TryGetDescriptor(volume,out descriptor)&&descriptor==record.Observations.Volumes[volumeId].Descriptor;
    }
    public static bool TryBegin(RebirthPoiWorldStore store,RebirthPoiNativeManifest manifest,SleeperVolume volume,int volumeId,out RebirthPoiNativeRestorationWitness witness)
    {
        witness=null;
        try
        {
            if(!ThreadManager.IsMainThread()||store==null||store.HasPending||store.Published==null||!store.Published.Binding.IsCurrent||manifest==null||!ReferenceEquals(store.Published.Binding.Scope,manifest.World)||!manifest.StillMatches()||volume==null||!manifest.Volumes.Contains(volume)||!ReferenceEquals(manifest.World.GetSleeperVolume(volumeId),volume)||volume.spawnDelay>1||volume.spawnsAvailable==null||volume.respawnList==null||volume.respawnList.Count==0)return false;
            RebirthPoiClearanceRecord record;RebirthPoiVolumeObservation observations;
            if(!store.Published.TryGet(manifest.Identity,out record)||record.State!=RebirthPoiClearanceState.Discovered||record.Observations==null||!record.Observations.Volumes.TryGetValue(volumeId,out observations))return false;
            string descriptor;if(!manifest.TryGetDescriptor(volume,out descriptor)||descriptor!=observations.Descriptor)return false;
            int oldId=volume.respawnList[volume.respawnList.Count-1];SleeperVolume.RespawnData native;
            if(manifest.World.GetEntity(oldId)!=null||volume.pendingSpawnMap.Contains(oldId)||!volume.respawnMap.TryGetValue(oldId,out native)||native.spawnPointIndex<0)return false;
            var matches=observations.Actors.Values.Where(a=>a.EntityId==oldId&&!a.Dead&&a.ClassName==native.className&&a.SpawnPoint==native.spawnPointIndex).ToArray();if(matches.Length!=1)return false;
            witness=new RebirthPoiNativeRestorationWitness(store,store.Published,record,manifest,volume,volumeId,matches[0]);return true;
        }
        catch{return false;}
    }
    // Called after THIS original UpdateSpawn returns successfully, never from a timer scan.
    public bool FinishOriginalUpdate()
    {
        if(completedUpdate||!Current()){failed=true;return false;}
        completedUpdate=true;
        var added=volume.respawnMap.Keys.Where(id=>!initialIds.Contains(id)).ToArray();SleeperVolume.RespawnData native;
        if(volume.respawnMap.ContainsKey(actor.EntityId)||added.Length!=1||initialIds.Any(id=>id!=actor.EntityId&&!volume.respawnMap.ContainsKey(id))||
            volume.numSpawned!=initialSpawned||SleeperVolume.TickSpawnCount!=initialTick+1||volume.pendingSpawnMap.Count!=initialPending+1||
            !volume.pendingSpawnMap.Contains(added[0])||!volume.respawnMap.TryGetValue(added[0],out native)||native.className!=actor.ClassName||native.spawnPointIndex!=actor.SpawnPoint||added[0]<=actor.EntityId)
        {failed=true;return false;}
        successor=added[0];return true;
    }
    internal int SuccessorEntityId {get{return successor;}}
    // Actual AddEnemyToWorld postfix must supply the exact successfully world-added actor.
    public bool TryCompleteActualSpawn(EntityAlive entity,out RebirthPoiPartialObservation successorObservation)
    {
        successorObservation=null;
        try
        {
            if(!completedUpdate||successor<0||!Current()||entity==null||entity.IsDead()||entity.entityId!=successor||!ReferenceEquals(entity.world,manifest.World)||!ReferenceEquals(manifest.World.GetEntity(successor),entity)||!volume.respawnMap.ContainsKey(successor))return false;
            EntityClass nativeClass;if(!EntityClass.list.TryGetValue(entity.entityClass,out nativeClass)||nativeClass.entityClassName!=actor.ClassName)return false;
            if(!RebirthPoiNativeActorStamp.TryAssignVerifiedRestoration(entity,actor.Token,out var assigned)||assigned!=actor.Token)return false;
            var replacement=actor.RestoreVerifiedNativeParticipant(successor,receipt);var original=record.Observations;
            if(original.Revision==long.MaxValue)return false;
            successorObservation=new RebirthPoiPartialObservation(original.Generation,original.Epoch,original.Revision+1,original.Volumes.Values.Select(v=>v.NativeVolumeId!=volumeId?v:new RebirthPoiVolumeObservation(v.NativeVolumeId,v.Descriptor,v.Actors.Values.Select(a=>a.Token==actor.Token?replacement:a),original.EffectiveGeneration(v))));
            return successorObservation.IsSuccessorOf(original)&&Current();
        }
        catch{failed=true;successorObservation=null;return false;}
    }
}
