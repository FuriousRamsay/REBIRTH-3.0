using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// Actual native enrollment/death causality. Partial observations are session-owned until
// the separately required durable observation journal is connected; never infer reload proof.
internal sealed class RebirthPoiNativeEvidence
{
    public static readonly RebirthPoiNativeEvidence Instance=new RebirthPoiNativeEvidence();
    private const int MaximumCandidates=4096,MaximumActorsPerPoi=8192;
    private readonly Func<RebirthPoiWorldStore> resolveStore;
    private readonly Func<double> clock;
    private readonly Dictionary<PrefabInstance,Candidate> candidates=new Dictionary<PrefabInstance,Candidate>();
    private readonly Queue<Candidate> dirty=new Queue<Candidate>();
    private double nextPulse;
    private long generation;
    private sealed class Actor { public EntityAlive Entity; public bool Dead; }
    private sealed class Candidate
    {
        public RebirthPoiNativeManifest Manifest;
        public readonly Dictionary<SleeperVolume,Dictionary<int,Actor>> Actors=new Dictionary<SleeperVolume,Dictionary<int,Actor>>();
        public bool Queued,Fault,ResetObserved;
        public long? Epoch;
        public long Generation;
        public Guid Proof=Guid.NewGuid(),Repopulation=Guid.NewGuid();
        public RebirthPoiClearEvidence PendingClear; public RebirthPoiRepopulationEvidence PendingRepopulation;
    }
    internal sealed class DeathWitness
    {
        internal object Candidate,Actor;
        internal SleeperVolume Volume;
        internal EntityAlive Entity;
        internal long Generation;
    }
    internal RebirthPoiNativeEvidence(Func<RebirthPoiWorldStore> storeResolver=null,Func<double> monotonicClock=null)
    {
        resolveStore=storeResolver??(()=>{RebirthPoiWorldStore store;return RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)?store:null;});
        clock=monotonicClock??(()=>((double)Stopwatch.GetTimestamp()/Stopwatch.Frequency));
    }
    public void Reset() { generation++; candidates.Clear();dirty.Clear();nextPulse=0; }
    private Candidate Get(World world,PrefabInstance prefab)
    {
        if(!CurrentWorld(world) || prefab==null) return null;
        Candidate candidate;
        if(candidates.TryGetValue(prefab,out candidate)) return candidate.Manifest.StillMatches()?candidate:null;
        RebirthPoiNativeManifest manifest;
        if(candidates.Count>=MaximumCandidates || !RebirthPoiNativeManifest.TryResolve(world,prefab,out manifest)) return null;
        candidate=new Candidate { Manifest=manifest,Generation=generation };
        foreach(var volume in manifest.Volumes) candidate.Actors.Add(volume,new Dictionary<int,Actor>());
        var store=resolveStore(); RebirthPoiClearanceRecord record;
        if(store!=null && store.Published!=null && store.Published.TryGet(manifest.Identity,out record)) candidate.Epoch=record.Epoch;
        candidates.Add(prefab,candidate); return candidate;
    }
    private static bool CurrentWorld(World world)
    { return ThreadManager.IsMainThread() && world!=null && !world.IsRemote() && GameManager.Instance!=null && ReferenceEquals(GameManager.Instance.World,world); }
    private void Dirty(Candidate c) { if(c!=null && !c.Queued && c.Generation==generation) {c.Queued=true;dirty.Enqueue(c);} }
    public void PlayerTouch(World world,EntityPlayer player)
    {
        try
        {
            if(!CurrentWorld(world) || player==null || !ReferenceEquals(player.world,world) || !player.IsAlive() || player.IsSpectator) return;
            var position=player.GetBlockPosition(); var decorator=GameManager.Instance.GetDynamicPrefabDecorator();
            var prefab=decorator==null?null:decorator.GetPrefabFromWorldPos(position.x,position.z);
            Dirty(Get(world,prefab));
        }
        catch { }
    }
    public void NativeSpawn(World world,SleeperVolume volume,EntityAlive entity)
    {
        try
        {
            if(!CurrentWorld(world) || volume==null || entity==null || entity.IsDead() || !ReferenceEquals(entity.world,world) ||
                !ReferenceEquals(world.GetEntity(entity.entityId),entity) || !volume.respawnMap.ContainsKey(entity.entityId)) return;
            var candidate=Get(world,volume.prefabInstance); if(candidate==null || candidate.Fault) return;
            Dictionary<int,Actor> actors;
            if(!candidate.Actors.TryGetValue(volume,out actors)) return;
            Actor previous;
            if(actors.TryGetValue(entity.entityId,out previous))
            { if(previous.Dead || !ReferenceEquals(previous.Entity,entity)) candidate.Fault=true; Dirty(candidate);return; }
            if(candidate.Actors.Values.Sum(a=>a.Count)>=MaximumActorsPerPoi) { candidate.Fault=true;return; }
            actors.Add(entity.entityId,new Actor { Entity=entity }); Dirty(candidate);
        }
        catch { }
    }
    public DeathWitness BeforeDeath(SleeperVolume volume,EntityAlive entity)
    {
        try
        {
            if(volume==null || entity==null || !CurrentWorld(entity.world as World) || !entity.IsDead() || !volume.respawnMap.ContainsKey(entity.entityId)) return null;
            Candidate c; Dictionary<int,Actor> actors; Actor actor;
            if(!candidates.TryGetValue(volume.prefabInstance,out c) || c.Fault || !c.Manifest.StillMatches() ||
                !c.Actors.TryGetValue(volume,out actors) || !actors.TryGetValue(entity.entityId,out actor) || actor.Dead || !ReferenceEquals(actor.Entity,entity)) return null;
            return new DeathWitness { Candidate=c,Actor=actor,Volume=volume,Entity=entity,Generation=generation };
        }
        catch { return null; }
    }
    public void AfterDeath(DeathWitness witness)
    {
        try
        {
            if(witness==null || witness.Generation!=generation || !CurrentWorld(witness.Entity.world as World) || !witness.Entity.IsDead() || witness.Volume.respawnMap.ContainsKey(witness.Entity.entityId)) return;
            var c=(Candidate)witness.Candidate; var actor=(Actor)witness.Actor;
            if(c.Generation!=generation || !c.Manifest.StillMatches() || !ReferenceEquals(actor.Entity,witness.Entity)) return;
            actor.Dead=true; actor.Entity=null; Dirty(c);
        }
        catch { }
    }
    public void NativeDirty(World world,SleeperVolume volume)
    { try { if(volume!=null) Dirty(Get(world,volume.prefabInstance)); } catch { } }
    // Positive native Reset observation invalidates volatile causality only. Durable reset
    // intent/rebuild/trigger witnesses are a required next connection, not guessed here.
    public void NativeReset(World world,SleeperVolume volume)
    {
        try
        {
            var c=volume==null?null:Get(world,volume.prefabInstance);
            if(c==null || volume.wasCleared || volume.isSpawning || volume.numSpawned!=0 || volume.respawnMap.Count!=0) return;
            c.ResetObserved=true; c.Fault=true; Dirty(c);
        }
        catch { }
    }
    public bool IsWithheld(RebirthPoiIdentity identity)
    { return identity!=null && candidates.Values.Any(c=>c.Manifest.Identity.Equals(identity) && (c.Fault || c.ResetObserved)); }
    public void Pulse()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        double now=clock();if(now<nextPulse)return;nextPulse=now+0.5;
        if(dirty.Count==0)return;
        var c=dirty.Dequeue();c.Queued=false;
        if(c.Generation!=generation || !CurrentWorld(c.Manifest.World) || !c.Manifest.StillMatches())return;
        var store=resolveStore();var snapshot=store==null?null:store.Published;
        if(snapshot==null) {Dirty(c);return;}
        RebirthPoiClearanceRecord record;
        if(!snapshot.TryGet(c.Manifest.Identity,out record))
        {
            var discovery=store.TryDiscover(snapshot,c.Manifest.Identity);
            if(discovery!=RebirthPoiStoreResult.Published && discovery!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}
            snapshot=store.Published;if(snapshot==null || !snapshot.TryGet(c.Manifest.Identity,out record)){Dirty(c);return;}
        }
        if(record.State==RebirthPoiClearanceState.ResetPending)return;
        if(c.PendingRepopulation!=null && c.Epoch!=null && record.Epoch==c.Epoch.Value+1 && record.LastRepopulation!=null &&
            record.LastRepopulation.GenerationId==c.PendingRepopulation.GenerationId &&
            record.LastRepopulation.LiveNativeParticipants==c.PendingRepopulation.LiveNativeParticipants &&
            record.LastRepopulation.WorldTime==c.PendingRepopulation.WorldTime)
        { c.Epoch=record.Epoch;c.PendingRepopulation=null;c.PendingClear=null;c.Proof=Guid.NewGuid(); }
        if(c.Epoch==null)c.Epoch=record.Epoch;
        if(c.Epoch!=record.Epoch || c.Fault)return;
        int live=c.Actors.Values.Sum(a=>a.Values.Count(actor=>!actor.Dead && actor.Entity!=null && !actor.Entity.IsDead()));
        if(record.State==RebirthPoiClearanceState.Cleared)
        {
            if(live==0)return;
            var evidence=c.PendingRepopulation??(c.PendingRepopulation=new RebirthPoiRepopulationEvidence(c.Repopulation,live,c.Manifest.World.worldTime));
            var result=store.TryObserveRepopulation(snapshot,c.Manifest.Identity,record.Epoch,evidence);
            if(result!=RebirthPoiStoreResult.Published && result!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}
            c.Epoch=record.Epoch+1; c.Proof=Guid.NewGuid(); c.PendingRepopulation=null; c.PendingClear=null; Dirty(c); return;
        }
        int spawned=0;
        foreach(var volume in c.Manifest.Volumes)
        {
            var actors=c.Actors[volume];
            // Native async queues retain completed handles; count actual unfinished/callback
            // failures, not pendingSpawnMap.Count or queue length as active work.
            if(volume.isSpawning || !volume.wasCleared || volume.respawnMap.Count!=0 ||
                volume.minScript!=null && volume.minScript.IsRunning() ||
                volume.pendingSpawnOps.Any(h=>h==null || !h.IsCompleted || h.onComplete!=null) ||
                actors.Count==0 || volume.numSpawned!=actors.Count || actors.Values.Any(a=>!a.Dead)) return;
            // Stale pending IDs are allowed only when this exact enrolled actor was proven dead.
            if(volume.pendingSpawnMap.Any(id=>!actors.ContainsKey(id) || !actors[id].Dead))return;
            spawned+=actors.Count;
        }
        var proof=c.PendingClear??(c.PendingClear=new RebirthPoiClearEvidence(c.Proof,c.Manifest.Volumes.Length,c.Manifest.Volumes.Length,spawned,0,0,false,c.Manifest.World.worldTime));
        var clear=store.TryClear(snapshot,c.Manifest.Identity,record.Epoch,proof);
        if(clear!=RebirthPoiStoreResult.Published && clear!=RebirthPoiStoreResult.Duplicate)Dirty(c);
    }
}


