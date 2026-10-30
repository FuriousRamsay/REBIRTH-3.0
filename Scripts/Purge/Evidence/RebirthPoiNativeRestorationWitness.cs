using System;
using System.Collections.Generic;
using System.Linq;

// One original native replacement. Missing ledger readiness never creates a new participant.
internal sealed class RebirthPoiNativeRestorationWitness
{
    internal readonly World World;
    internal readonly SleeperVolume Volume;
    internal readonly RebirthPoiActorObservation Actor;
    private readonly Func<bool> current;
    private readonly HashSet<int> initialIds;
    private readonly int originalId,initialSpawned,initialTick,initialPending,initialRespawnCount,point;
    private readonly string className;
    private readonly Guid receipt=Guid.NewGuid();
    private int successor=-1;
    private bool completed,failed;
    internal bool NativeDeferred { get; private set; }
    internal RebirthPoiNativeRestorationWitness(World world,SleeperVolume volume,int id,SleeperVolume.RespawnData native,RebirthPoiActorObservation actor,Func<bool> isCurrent)
    {
        World=world;Volume=volume;originalId=id;className=native.className;point=native.spawnPointIndex;Actor=actor;current=isCurrent;
        initialIds=new HashSet<int>(volume.respawnMap.Keys);initialSpawned=volume.numSpawned;
        initialTick=SleeperVolume.TickSpawnCount;initialPending=volume.pendingSpawnMap.Count;
        initialRespawnCount=volume.respawnList?.Count??0;
    }
    private bool Scope()=>!failed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote();
    public bool FinishOriginalUpdate()
    {
        if(completed||!Scope())return false;completed=true;
        // Native CheckSpawnPos may reject a temporarily unavailable position and
        // put the untouched original back on the queue. This is not a replacement.
        if(point>=0&&initialRespawnCount>0&&Volume.spawnDelay==2&&
            Volume.respawnList!=null&&Volume.respawnList.Count==initialRespawnCount&&
            Volume.respawnList[initialRespawnCount-1]==originalId&&
            initialIds.SetEquals(Volume.respawnMap.Keys)&&
            Volume.respawnMap.TryGetValue(originalId,out var retained)&&
            retained.className==className&&retained.spawnPointIndex==point&&
            Volume.numSpawned==initialSpawned&&SleeperVolume.TickSpawnCount==initialTick&&
            Volume.pendingSpawnMap.Count==initialPending&&!Volume.pendingSpawnMap.Contains(originalId)&&
            Volume.spawnsAvailable!=null&&Volume.spawnsAvailable.Contains(point)&&World.GetEntity(originalId)==null)
        { NativeDeferred=true;return false; }
        var added=Volume.respawnMap.Keys.Where(id=>!initialIds.Contains(id)).ToArray();
        if(Volume.respawnMap.ContainsKey(originalId)||added.Length!=1||initialIds.Any(id=>id!=originalId&&!Volume.respawnMap.ContainsKey(id))||
            Volume.numSpawned!=initialSpawned||SleeperVolume.TickSpawnCount!=initialTick+1||Volume.pendingSpawnMap.Count!=initialPending+1||
            !Volume.pendingSpawnMap.Contains(added[0])||!Volume.respawnMap.TryGetValue(added[0],out var native)||
            native.className!=className||native.spawnPointIndex!=point||added[0]<=originalId)
        {failed=true;return false;}
        successor=added[0];return true;
    }
    internal int SuccessorEntityId=>successor;
    internal bool TryCompleteActualSpawn(EntityAlive entity,out RebirthPoiActorObservation observation)
    {
        observation=null;
        try
        {
            if(!completed||!Scope()||Actor==null||Actor.Dead||Actor.EntityId!=originalId||Actor.ClassName!=className||Actor.SpawnPoint!=point||
                current==null||!current()||entity==null||entity.IsDead()||entity.entityId!=successor||!ReferenceEquals(entity.world,World)||
                !ReferenceEquals(World.GetEntity(successor),entity)||!Volume.respawnMap.TryGetValue(successor,out var native)||native.className!=className||
                native.spawnPointIndex!=point||!EntityClass.list.TryGetValue(entity.entityClass,out var nativeClass)||nativeClass.entityClassName!=className)return false;
            if(!RebirthPoiNativeActorStamp.TryAssignVerifiedRestoration(entity,Actor.Token,out var assigned)||assigned!=Actor.Token)return false;
            observation=Actor.RestoreVerifiedNativeParticipant(successor,receipt);return current();
        }
        catch{failed=true;return false;}
    }
}
