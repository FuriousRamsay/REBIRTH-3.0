using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    private sealed class RespawnMiss { internal long Revision=-1; internal double Until; }
    private ConditionalWeakTable<PrefabInstance,RespawnMiss> respawnMisses=new ConditionalWeakTable<PrefabInstance,RespawnMiss>();
    private double nextPulse;
    private long generation;
    private sealed class Actor { public EntityAlive Entity; public bool Dead; public RebirthPoiActorObservation Observation; }
    private sealed class Candidate
    {
        public RebirthPoiNativeManifest Manifest;
        public EntityPlayer Touched;
        public readonly Dictionary<SleeperVolume,Dictionary<int,Actor>> Actors=new Dictionary<SleeperVolume,Dictionary<int,Actor>>();
        public bool Queued,Fault,ResetObserved,JournalDirty;
        public long RespawnGateRevision=-1; public double RespawnGateUntil; public bool RespawnGateValid;
        public long? Epoch;
        public long Generation;
        public Guid Proof=Guid.NewGuid(),Repopulation=Guid.NewGuid(),JournalGeneration=Guid.NewGuid();
        public RebirthPoiClearEvidence PendingClear; public RebirthPoiRepopulationEvidence PendingRepopulation;
    }
    internal sealed class DeathWitness
    {
        internal object Candidate,Actor;
        internal SleeperVolume Volume;
        internal EntityAlive Entity;
        internal string Contributor;
        internal long Generation;
    }
    internal RebirthPoiNativeEvidence(Func<RebirthPoiWorldStore> storeResolver=null,Func<double> monotonicClock=null)
    {
        resolveStore=storeResolver??(()=>{RebirthPoiWorldStore store;return RebirthPoiWorldLifecycle.Instance.TryGetStore(out store)?store:null;});
        clock=monotonicClock??(()=>((double)Stopwatch.GetTimestamp()/Stopwatch.Frequency));
    }
    public void Reset() { generation++; candidates.Clear();dirty.Clear();nextPulse=0; respawnMisses=new ConditionalWeakTable<PrefabInstance,RespawnMiss>(); }
    private Candidate Get(World world,PrefabInstance prefab)
    {
        if(!CurrentWorld(world) || prefab==null) return null;
        Candidate candidate;
        if(candidates.TryGetValue(prefab,out candidate))
        {
            if(!candidate.ResetObserved)return candidate; // Ordinary native dirty hooks remain O(1).
            var resetSnapshot=resolveStore()?.Published;RebirthPoiClearanceRecord successor;
            if(candidate.Epoch==null||resetSnapshot==null||!resetSnapshot.Binding.IsCurrent||!resetSnapshot.TryGet(candidate.Manifest.Identity,out successor)||successor.State==RebirthPoiClearanceState.ResetPending||successor.Epoch<=candidate.Epoch.Value||successor.LastResetDisposition!=RebirthPoiResetDisposition.Completed)return candidate;
            // Only a durable completed original reset may retire old volatile
            // causality. New enrollment restores the saved successor generation.
            candidates.Remove(prefab);
        }
        RebirthPoiNativeManifest manifest;
        if(candidates.Count>=MaximumCandidates || !RebirthPoiNativeManifest.TryResolve(world,prefab,out manifest)) return null;
        candidate=new Candidate { Manifest=manifest,Generation=generation };
        foreach(var volume in manifest.Volumes) candidate.Actors.Add(volume,new Dictionary<int,Actor>());
        var store=resolveStore(); RebirthPoiClearanceRecord record;
        if(store!=null && store.Published!=null && store.Published.TryGet(manifest.Identity,out record)) { candidate.Epoch=record.Epoch; RestoreJournal(candidate,record); }
        candidates.Add(prefab,candidate); return candidate;
    }
    private static bool CurrentWorld(World world)
    { return ThreadManager.IsMainThread() && world!=null && !world.IsRemote() && GameManager.Instance!=null && ReferenceEquals(GameManager.Instance.World,world); }
    private void Dirty(Candidate c) { if(c!=null && !c.Queued && c.Generation==generation) {c.Queued=true;dirty.Enqueue(c);} }
    // Suppression never clears a POI: only already committed world truth can do so.
    // Exact manifest revalidation is bounded per candidate, not repeated per native hook.
    internal bool SuppressPurgeRespawn(World world,SleeperVolume volume)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || !CurrentWorld(world) || volume?.prefabInstance == null) return false;
        try
        {
            var snapshot=resolveStore()?.Published;
            if(snapshot==null || snapshot.Count==0 || !snapshot.Binding.IsCurrent || !ReferenceEquals(snapshot.Binding.Scope,world))return false;
            double now=clock();
            Candidate candidate;
            if(!candidates.TryGetValue(volume.prefabInstance,out candidate))
            {
                var miss=respawnMisses.GetValue(volume.prefabInstance,_=>new RespawnMiss());
                if(miss.Revision==snapshot.Revision && now<miss.Until)return false;
                candidate=Get(world,volume.prefabInstance);
                if(candidate==null){miss.Revision=snapshot.Revision;miss.Until=now+.5;return false;}
                respawnMisses.Remove(volume.prefabInstance);
            }
            RebirthPoiClearanceRecord record;
            if (candidate.Fault || !candidate.Actors.ContainsKey(volume)
                || !snapshot.TryGet(candidate.Manifest.Identity,out record)
                || record.State!=RebirthPoiClearanceState.Cleared && record.State!=RebirthPoiClearanceState.ResetPending) return false;
            var identity=candidate.Manifest.Identity;var prefab=volume.prefabInstance;
            if(prefab.boundingBoxPosition.x!=identity.X || prefab.boundingBoxPosition.y!=identity.Y || prefab.boundingBoxPosition.z!=identity.Z
                || prefab.boundingBoxSize.x!=identity.SizeX || prefab.boundingBoxSize.y!=identity.SizeY || prefab.boundingBoxSize.z!=identity.SizeZ
                || prefab.rotation!=identity.Rotation || prefab.prefab==null
                || !string.Equals(prefab.prefab.PrefabName,identity.Prefab,StringComparison.OrdinalIgnoreCase))return false;
            if(candidate.RespawnGateRevision!=snapshot.Revision || now>=candidate.RespawnGateUntil)
            {
                candidate.RespawnGateValid=candidate.Manifest.StillMatches();
                candidate.RespawnGateRevision=snapshot.Revision;candidate.RespawnGateUntil=now+.5;
            }
            return candidate.RespawnGateValid;
        }
        catch{return false;}
    }
    public void PlayerTouch(World world,EntityPlayer player)
    {
        try
        {
            if(!CurrentWorld(world) || player==null || !ReferenceEquals(player.world,world) || !player.IsAlive() || player.IsSpectator) return;
            var position=player.GetBlockPosition(); var decorator=GameManager.Instance.GetDynamicPrefabDecorator();
            var prefab=decorator==null?null:decorator.GetPrefabFromWorldPos(position.x,position.z);
            var candidate=Get(world,prefab);if(candidate!=null)candidate.Touched=player;Dirty(candidate);
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
                        {
                if(previous.Entity==null && !previous.Dead && MatchesEnrollment(previous.Observation,volume,entity)) previous.Entity=entity;
                if(previous.Dead || !ReferenceEquals(previous.Entity,entity)) candidate.Fault=true;
                Dirty(candidate);return;
            }
            if(candidate.Actors.Values.Sum(a=>a.Count)>=MaximumActorsPerPoi) { candidate.Fault=true;return; }
                        SleeperVolume.RespawnData membership;
            EntityClass nativeClass;
            Guid token;
            if(!volume.respawnMap.TryGetValue(entity.entityId,out membership) ||
                !EntityClass.list.TryGetValue(entity.entityClass,out nativeClass) || nativeClass.entityClassName!=membership.className ||
                !RebirthPoiNativeActorStamp.TryAssignForVerifiedEnrollment(entity,out token)) {candidate.Fault=true;return;}
            if(candidate.Actors.Values.Any(map=>map.Values.Any(a=>a.Observation!=null && a.Observation.Token==token))) {candidate.Fault=true;return;}
            actors.Add(entity.entityId,new Actor { Entity=entity,
                Observation=new RebirthPoiActorObservation(token,entity.entityId,membership.className,membership.spawnPointIndex) });
                        var store=resolveStore();RebirthPoiClearanceRecord current;
            if(store?.Published!=null && store.Published.TryGet(candidate.Manifest.Identity,out current) &&
                current.State==RebirthPoiClearanceState.Cleared && candidate.PendingRepopulation==null)
                candidate.PendingRepopulation=new RebirthPoiRepopulationEvidence(candidate.Repopulation,1,world.worldTime);
            candidate.JournalDirty=true; Dirty(candidate);
        }
        catch { }
    }
    internal void NativeRestored(World world,SleeperVolume volume,EntityAlive entity,RebirthPoiPartialObservation observations)
    {
        var candidate=Get(world,volume?.prefabInstance);if(candidate==null || candidate.Fault || observations==null)return;
        int id=world.FindSleeperVolume(volume.BoxMin,volume.BoxMax);RebirthPoiVolumeObservation proof;
        if(!observations.Volumes.TryGetValue(id,out proof) || !candidate.Actors.TryGetValue(volume,out var actors))return;
        Guid token;if(!RebirthPoiNativeActorStamp.TryRead(entity,out token) || !proof.Actors.TryGetValue(token,out var restored) ||
            !MatchesEnrollment(restored,volume,entity))return;
        var originals=actors.Where(a=>a.Value.Observation?.Token==token).ToArray();
        if(originals.Length>1 || originals.Length==1 && !restored.IsCausalSuccessorOf(originals[0].Value.Observation)){candidate.Fault=true;return;}
        if(originals.Length==1)actors.Remove(originals[0].Key);
        actors[entity.entityId]=new Actor {Entity=entity,Observation=restored};
        candidate.JournalGeneration=observations.Generation;Dirty(candidate);
    }
    public DeathWitness BeforeDeath(SleeperVolume volume,EntityAlive entity)
    {
        try
        {
            if(volume==null || entity==null || !CurrentWorld(entity.world as World) || !entity.IsDead() || !volume.respawnMap.ContainsKey(entity.entityId)) return null;
            Candidate c; Dictionary<int,Actor> actors; Actor actor;
            if(!candidates.TryGetValue(volume.prefabInstance,out c) || c.Fault || !c.Manifest.StillMatches() ||
                !c.Actors.TryGetValue(volume,out actors) || !actors.TryGetValue(entity.entityId,out actor) || actor.Dead) return null;
            if(actor.Entity==null && MatchesEnrollment(actor.Observation,volume,entity)) actor.Entity=entity;
            if(!ReferenceEquals(actor.Entity,entity))return null;
            return new DeathWitness { Candidate=c,Actor=actor,Volume=volume,Entity=entity,Generation=generation,Contributor=RebirthPurgeKillContributor.Capture(entity) };
        }
        catch { return null; }
    }
    public void AfterDeath(DeathWitness witness)
    {
        try
        {
            if(witness==null || witness.Generation!=generation || !CurrentWorld(witness.Entity.world as World) || !witness.Entity.IsDead() || witness.Volume.respawnMap.ContainsKey(witness.Entity.entityId)) return;
            var c=(Candidate)witness.Candidate; var actor=(Actor)witness.Actor;
            if(actor.Dead || actor.Observation?.Dead==true)return; // Duplicate native callback retains the original receipt.
            if(c.Generation!=generation || !c.Manifest.StillMatches() || !ReferenceEquals(actor.Entity,witness.Entity)) return;
                        var original=actor.Observation;
            if(original==null)return;
            actor.Observation=new RebirthPoiActorObservation(original.Token,original.EntityId,original.ClassName,original.SpawnPoint,
                Guid.NewGuid(),c.Manifest.World.worldTime,original.Restorations,original.Checkpoint,original.Inheritance,contributor:witness.Contributor);
            actor.Dead=true; actor.Entity=null; c.JournalDirty=true; Dirty(c);
        }
        catch { }
    }
    public void NativeDirty(World world,SleeperVolume volume)
    { try { if(volume!=null && (volume.isSpawning || volume.wasCleared || volume.numSpawned>0)) Dirty(Get(world,volume.prefabInstance)); } catch { } }
    // Positive native Reset observation invalidates volatile causality only. Durable reset
    // intent/rebuild/trigger witnesses are a required next connection, not guessed here.
    // None/QoL natural respawn resets a previously cleared room before spawning.
    // Retire only its already positively dead volatile actors. World clearance
    // stays unchanged until actual native enrollment proves live repopulation.
    internal void NativeNaturalReset(World world,SleeperVolume volume)
    {
        try
        {
            if(RebirthSandboxOptionManager.Current.IsPurge||!CurrentWorld(world))return;
            var candidate=Get(world,volume?.prefabInstance);var snapshot=resolveStore()?.Published;RebirthPoiClearanceRecord record;
            if(candidate==null||candidate.Fault||candidate.ResetObserved||snapshot==null||!snapshot.Binding.IsCurrent||!snapshot.TryGet(candidate.Manifest.Identity,out record)||record.State!=RebirthPoiClearanceState.Cleared||record.Clear==null||!candidate.Actors.TryGetValue(volume,out var actors)||actors.Values.Any(a=>!a.Dead))return;
            candidate.Epoch=record.Epoch;actors.Clear();candidate.PendingClear=null;
        }
        catch{ }
    }
    public void NativeReset(World world,SleeperVolume volume)
    {
        try
        {
            var c=volume==null?null:Get(world,volume.prefabInstance);
            if(c==null || volume.wasCleared || volume.isSpawning || volume.numSpawned!=0 || volume.respawnMap.Count!=0) return;
            if(c.Epoch==null){var snapshot=resolveStore()?.Published;RebirthPoiClearanceRecord record;if(snapshot!=null&&snapshot.TryGet(c.Manifest.Identity,out record))c.Epoch=record.Epoch;}
            c.ResetObserved=true; c.Fault=true; Dirty(c);
        }
        catch { }
    }
    private static bool MatchesEnrollment(RebirthPoiActorObservation actor,SleeperVolume volume,EntityAlive entity)
    {
        Guid token;SleeperVolume.RespawnData membership;EntityClass nativeClass;
        return actor!=null && !actor.Dead && actor.EntityId==entity.entityId &&
            RebirthPoiNativeActorStamp.TryRead(entity,out token) && token==actor.Token &&
            EntityClass.list.TryGetValue(entity.entityClass,out nativeClass) && nativeClass.entityClassName==actor.ClassName &&
            volume.respawnMap.TryGetValue(entity.entityId,out membership) && membership.className==actor.ClassName && membership.spawnPointIndex==actor.SpawnPoint;
    }
    private static void RestoreJournal(Candidate candidate,RebirthPoiClearanceRecord record)
    {
        var saved=record.Observations;if(saved==null)return;
        if(saved.Epoch!=record.Epoch || saved.Volumes.Count!=candidate.Manifest.Volumes.Length){candidate.Fault=true;return;}
        candidate.JournalGeneration=saved.Generation;
        foreach(var volume in candidate.Manifest.Volumes)
        {
            int id=candidate.Manifest.World.FindSleeperVolume(volume.BoxMin,volume.BoxMax);
            string descriptor;RebirthPoiVolumeObservation proof;
            if(!candidate.Manifest.TryGetDescriptor(volume,out descriptor) || !saved.Volumes.TryGetValue(id,out proof) || proof.Descriptor!=descriptor)
            {candidate.Fault=true;return;}
            foreach(var actor in proof.Actors.Values)
            {
                var entity=candidate.Manifest.World.GetEntity(actor.EntityId) as EntityAlive;
                Guid token;SleeperVolume.RespawnData membership;EntityClass nativeClass;
                bool bound=!actor.Dead && entity!=null && !entity.IsDead() &&
                    RebirthPoiNativeActorStamp.TryRead(entity,out token) && token==actor.Token &&
                    EntityClass.list.TryGetValue(entity.entityClass,out nativeClass) && nativeClass.entityClassName==actor.ClassName &&
                    volume.respawnMap.TryGetValue(actor.EntityId,out membership) && membership.className==actor.ClassName && membership.spawnPointIndex==actor.SpawnPoint;
                // Unloaded live actors retain their obligation. Native despawn is not a kill.
                candidate.Actors[volume].Add(actor.EntityId,new Actor {Observation=actor,
                    Dead=actor.Dead && actor.TerminalKind==RebirthPoiActorTerminalKind.Death,Entity=bound?entity:null});
            }
        }
    }
    private static RebirthPoiPartialObservation BuildJournal(Candidate candidate,RebirthPoiClearanceRecord record)
    {
        try
        {
            var previous=record.Observations;
            var volumes=new List<RebirthPoiVolumeObservation>();bool changed=previous==null;
            foreach(var volume in candidate.Manifest.Volumes)
            {
                int id=candidate.Manifest.World.FindSleeperVolume(volume.BoxMin,volume.BoxMax);string descriptor;
                if(id<0 || !candidate.Manifest.TryGetDescriptor(volume,out descriptor))return null;
                RebirthPoiVolumeObservation old=null;
                if(previous!=null && (!previous.Volumes.TryGetValue(id,out old) || old.Descriptor!=descriptor))return null;
                var actors=candidate.Actors[volume].Values.Select(a=>a.Observation).ToArray();
                if(actors.Any(a=>a==null))return null;
                if(old==null || actors.Length!=old.Actors.Count || actors.Any(a=>!old.Actors.TryGetValue(a.Token,out var p) ||
                    p.CausalDigest!=a.CausalDigest || p.DeathReceipt!=a.DeathReceipt || p.DeathTime!=a.DeathTime || p.TerminalKind!=a.TerminalKind || p.Contributor!=a.Contributor))changed=true;
                volumes.Add(new RebirthPoiVolumeObservation(id,descriptor,actors,old==null?candidate.JournalGeneration:previous.EffectiveGeneration(old)));
            }
            if(!changed)return previous;
            var successor=new RebirthPoiPartialObservation(previous==null?candidate.JournalGeneration:previous.Generation,
                record.Epoch,previous==null?1:checked(previous.Revision+1),volumes);
            return successor.IsSuccessorOf(previous)?successor:null;
        }
        catch(ArgumentException){return null;}catch(OverflowException){return null;}
    }
    public bool IsWithheld(RebirthPoiIdentity identity)
    { return identity!=null && candidates.Values.Any(c=>c.Manifest.Identity.Equals(identity) && (c.Fault || c.ResetObserved)); }
    public void Pulse()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        double now=clock();if(now<nextPulse)return;nextPulse=now+0.5;
        if(dirty.Count==0)return;
        var c=dirty.Dequeue();c.Queued=false;
        if(c.Generation!=generation || !candidates.TryGetValue(c.Manifest.Prefab,out var currentCandidate) || !ReferenceEquals(currentCandidate,c) || !CurrentWorld(c.Manifest.World) || !c.Manifest.StillMatches())return;
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
        if(record.ResetOnly){var promotion=store.TryDiscover(snapshot,c.Manifest.Identity);if(promotion!=RebirthPoiStoreResult.Published&&promotion!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}snapshot=store.Published;if(!snapshot.TryGet(c.Manifest.Identity,out record)||record.ResetOnly)return;}
        if(c.PendingRepopulation!=null && c.Epoch!=null && record.Epoch==c.Epoch.Value+1 && record.LastRepopulation!=null &&
            record.LastRepopulation.GenerationId==c.PendingRepopulation.GenerationId &&
            record.LastRepopulation.LiveNativeParticipants==c.PendingRepopulation.LiveNativeParticipants &&
            record.LastRepopulation.WorldTime==c.PendingRepopulation.WorldTime)
        { c.Epoch=record.Epoch;c.PendingRepopulation=null;c.PendingClear=null;c.Proof=Guid.NewGuid(); c.JournalGeneration=Guid.NewGuid(); c.JournalDirty=true; }
        if(c.Epoch==null)c.Epoch=record.Epoch;
        if(c.Epoch!=record.Epoch || c.Fault)return;
        int live=c.Actors.Values.Sum(a=>a.Values.Count(actor=>!actor.Dead && actor.Entity!=null && !actor.Entity.IsDead()));
        if(record.State==RebirthPoiClearanceState.Cleared)
        {
            if(live==0 && c.PendingRepopulation==null)return;
            var evidence=c.PendingRepopulation??(c.PendingRepopulation=new RebirthPoiRepopulationEvidence(c.Repopulation,live,c.Manifest.World.worldTime));
            var result=store.TryObserveRepopulation(snapshot,c.Manifest.Identity,record.Epoch,evidence);
            if(result!=RebirthPoiStoreResult.Published && result!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}
            c.Epoch=record.Epoch+1; c.Proof=Guid.NewGuid(); c.JournalGeneration=Guid.NewGuid(); c.JournalDirty=true; c.PendingRepopulation=null; c.PendingClear=null; Dirty(c); return;
        }
        // Persist native enrollment and deaths before publishing a clear. Failed writes are
        // retried by the existing exact-candidate store; new observations never replace it.
        if(c.JournalDirty)
        {
            if(store.HasPending || RebirthPoiNativeRestorationConnection.HasPending(c.Manifest.Identity)){Dirty(c);return;}
            var observations=BuildJournal(c,record);
            if(observations==null){c.Fault=true;return;}
            if(record.Observations==null || record.Observations.Canonical!=observations.Canonical)
            {
                var publication=store.TryObservePartial(snapshot,c.Manifest.Identity,record.Epoch,observations);
                if(publication!=RebirthPoiStoreResult.Published && publication!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}
                snapshot=store.Published;
                if(snapshot==null || !snapshot.TryGet(c.Manifest.Identity,out record)){Dirty(c);return;}
            }
            c.JournalDirty=false;
        }
        if(RebirthPoiRemainingRoomActivation.TryActivateOne(c.Manifest,c.Touched)){Dirty(c);return;}
        int spawned=0;
        foreach(var volume in c.Manifest.Volumes)
        {
            var actors=c.Actors[volume];
            // Native async queues retain completed handles; count actual unfinished/callback
            // failures, not pendingSpawnMap.Count or queue length as active work.
            if(volume.isSpawning || !volume.wasCleared || volume.respawnMap.Count!=0 ||
                volume.minScript!=null && volume.minScript.IsRunning() ||
                volume.pendingSpawnOps.Any(h=>h==null || !h.IsCompleted || h.onComplete!=null) ||
                volume.numSpawned!=actors.Count || actors.Values.Any(a=>!a.Dead)) return;
            // Stale pending IDs are allowed only when this exact enrolled actor was proven dead.
            if(volume.pendingSpawnMap.Any(id=>!actors.ContainsKey(id) || !actors[id].Dead))return;
            spawned+=actors.Count;
        }
        if(spawned==0)return; // An entirely empty POI is not a combat clear.
        var proof=c.PendingClear??(c.PendingClear=new RebirthPoiClearEvidence(c.Proof,c.Manifest.Volumes.Length,c.Manifest.Volumes.Length,spawned,0,0,false,c.Manifest.World.worldTime));
        var clear=store.TryClear(snapshot,c.Manifest.Identity,record.Epoch,proof);
        if(clear!=RebirthPoiStoreResult.Published && clear!=RebirthPoiStoreResult.Duplicate)Dirty(c);
    }
}


