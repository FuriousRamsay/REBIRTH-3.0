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
    private readonly Queue<DeathWitness> pendingDeaths=new Queue<DeathWitness>();
    private sealed class RespawnMiss { internal long Revision=-1; internal double Until; }
    private ConditionalWeakTable<PrefabInstance,RespawnMiss> respawnMisses=new ConditionalWeakTable<PrefabInstance,RespawnMiss>();
    private double nextPulse;
    private long generation;
    private sealed class Actor { public EntityAlive Entity; public bool Dead; public RebirthPoiActorObservation Observation; public readonly List<RebirthPoiActorObservation> Versions=new List<RebirthPoiActorObservation>(); }
    private sealed class RoomBinding { internal int Id; internal string Descriptor; }
    private readonly Dictionary<PrefabInstance,Candidate> quarantined=new Dictionary<PrefabInstance,Candidate>();
    private sealed class Candidate
    {
        public RebirthPoiNativeManifest Manifest;
        public RebirthPoiWorldStore Store;
        public RebirthPoiNativeManifest.RoomCertificate Root;
        public World World=>Root.World; public PrefabInstance Prefab=>Root.Prefab; public RebirthPoiIdentity Identity=>Root.Identity;
        public readonly Dictionary<SleeperVolume,RoomBinding> Bindings=new Dictionary<SleeperVolume,RoomBinding>();
        public readonly Dictionary<int,RebirthPoiVolumeObservation> Unbound=new Dictionary<int,RebirthPoiVolumeObservation>();
        public bool Restored; public double NextManifest; public Guid Quarantine; public long QuarantineEpoch;
        public EntityPlayer Touched;
        public readonly Dictionary<SleeperVolume,Dictionary<int,Actor>> Actors=new Dictionary<SleeperVolume,Dictionary<int,Actor>>();
        public bool Queued,Fault,ResetObserved,JournalDirty;
        public long RespawnGateRevision=-1; public double RespawnGateUntil; public bool RespawnGateValid;
        public long? Epoch;
        public Guid BoundReset;
        public long Generation;
        public Guid Proof=Guid.NewGuid(),Repopulation=Guid.NewGuid(),JournalGeneration;
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
        resolveStore=storeResolver??(()=>{RebirthPoiWorldStore store;return RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)?store:null;});
        clock=monotonicClock??(()=>((double)Stopwatch.GetTimestamp()/Stopwatch.Frequency));
    }
    public void Reset() { generation++; shutdownPrepared=false;candidates.Clear();quarantined.Clear();dirty.Clear();pendingDeaths.Clear();nextPulse=0; respawnMisses=new ConditionalWeakTable<PrefabInstance,RespawnMiss>(); }
    private Candidate Get(World world,PrefabInstance prefab)
    {
        if(!CurrentWorld(world)||prefab==null)return null;
        Candidate c;
        if(candidates.TryGetValue(prefab,out c))
        {
            var currentSnapshot=resolveStore()?.Published;
            if(c.Epoch!=null&&c.Identity!=null&&currentSnapshot!=null&&currentSnapshot.Binding.IsCurrent&&
                currentSnapshot.TryGet(c.Identity,out var terminal)&&CompletedSuccessor(c,terminal))
                return Transition(c,terminal);
            if(!c.ResetObserved)Promote(c);return c;
        }
        RebirthPoiNativeManifest manifest;RebirthPoiNativeManifest.RoomCertificate root;
        if(candidates.Count>=MaximumCandidates||!RebirthPoiNativeManifest.TryResolve(world,prefab,out manifest)||
            !RebirthPoiNativeManifest.RoomCertificate.TryCapture(world,prefab,out root)||!root.BindIdentity())return null;
        c=new Candidate{Manifest=manifest,Root=root,Generation=generation,JournalGeneration=root.Seal};
        candidates.Add(prefab,c);
        foreach(var volume in manifest.Volumes)Attach(c,volume);
        BindJournal(c);return c;
    }
    private void Promote(Candidate c)
    {
        if(c.Fault||c.ResetObserved||clock()<c.NextManifest)return;c.NextManifest=clock()+0.5;
        if(!c.Root.AuthoredCurrent()){c.Fault=true;return;}
        if(c.Manifest==null&&RebirthPoiNativeManifest.TryResolve(c.World,c.Prefab,out var manifest))
        {
            c.Manifest=manifest;
            foreach(var volume in manifest.Volumes)if(!Attach(c,volume)){c.Fault=true;return;}
            c.JournalDirty=true;
        }
    }
    private Candidate CaptureCandidate(World world,SleeperVolume volume)
    {
        var c=Get(world,volume.prefabInstance);
        if(c==null)
        {
            if(candidates.Count>=MaximumCandidates||!RebirthPoiNativeManifest.RoomCertificate.TryCapture(world,volume.prefabInstance,out var root)||
                !root.TryRoom(volume,out _,out _))return null;
            c=new Candidate{Root=root,Generation=generation,JournalGeneration=root.Seal};candidates.Add(volume.prefabInstance,c);
        }
        // A reset can replace authored/runtime objects before its durable terminal
        // receipt. Capture the new positive scope for quarantine, never reuse the old root.
        var captureRoot=c.Root;
        if(!captureRoot.AuthoredCurrent()&&RebirthPoiNativeManifest.RoomCertificate.TryCapture(world,volume.prefabInstance,out var replacement))captureRoot=replacement;
        if(captureRoot.BindIdentity())
        {
            var evidenceStore=resolveStore();var snapshot=evidenceStore?.Published;RebirthPoiClearanceRecord record;
            if(snapshot!=null&&snapshot.Binding.IsCurrent&&ReferenceEquals(snapshot.Binding.Scope,world)&&snapshot.TryGet(captureRoot.Identity,out record)&&record.State==RebirthPoiClearanceState.ResetPending)
            {
                Candidate waiting;
                if(!quarantined.TryGetValue(volume.prefabInstance,out waiting))
                {
                    if(!RebirthPoiNativeManifest.RoomCertificate.TryCapture(world,volume.prefabInstance,out var root))return null;
                    waiting=new Candidate{Root=root,Store=evidenceStore,Generation=generation,Quarantine=record.ResetId,QuarantineEpoch=record.Epoch};quarantined.Add(volume.prefabInstance,waiting);
                }
                if(waiting.Quarantine!=record.ResetId||waiting.QuarantineEpoch!=record.Epoch){waiting.Fault=true;return waiting;}
                c=waiting;
            }
        }
        if(!c.Fault&&!Attach(c,volume))c.Fault=true;
        BindJournal(c);return c;
    }
    private bool Attach(Candidate c,SleeperVolume volume)
    {
        if(c.Bindings.ContainsKey(volume))return c.Root.TryRoom(volume,out var retained,out _)&&c.Bindings[volume].Id==retained;
        if(!c.Root.TryRoom(volume,out var id,out var descriptor)||c.Bindings.Values.Any(b=>b.Id==id)||
            descriptor!=null&&c.Bindings.Values.Any(b=>b.Descriptor==descriptor)||
            descriptor!=null&&c.Unbound.Any(p=>p.Value.Descriptor==descriptor&&p.Key!=id))return false;
        var actors=new Dictionary<int,Actor>();c.Actors.Add(volume,actors);c.Bindings.Add(volume,new RoomBinding{Id=id,Descriptor=descriptor});c.JournalDirty=true;
        if(c.Unbound.TryGetValue(id,out var saved))
        {
            if(descriptor==null||descriptor!=saved.Descriptor)return false;
            foreach(var proof in saved.Actors.Values)actors.Add(proof.EntityId,RestoreActor(c,volume,proof));
            c.Unbound.Remove(id);
        }
        return true;
    }
    private void BindJournal(Candidate c)
    {
        if(c.Fault||c.Restored||c.Quarantine!=Guid.Empty||!c.Root.BindIdentity())return;
        if(c.JournalGeneration==Guid.Empty)c.JournalGeneration=c.Root.Seal;
        var owner=resolveStore();var snapshot=owner?.Published;RebirthPoiClearanceRecord record;
        if(snapshot==null||!snapshot.Binding.IsCurrent||!ReferenceEquals(snapshot.Binding.Scope,c.World))return;
        if(c.Store!=null&&!ReferenceEquals(c.Store,owner)){c.Fault=true;return;}c.Store=owner;
        if(snapshot.TryGet(c.Identity,out record))
        {
            if(record.State==RebirthPoiClearanceState.ResetPending)return;
            c.Epoch=record.Epoch;c.BoundReset=record.LastResetId;RestoreJournal(c,record);
        }
        else {c.Epoch=0;c.JournalGeneration=c.Root.Seal;}
        c.Restored=true;
    }
    private static bool CurrentWorld(World world)
    { return ThreadManager.IsMainThread() && world!=null && !world.IsRemote() && GameManager.Instance!=null && ReferenceEquals(GameManager.Instance.World,world); }
    private void Dirty(Candidate c) { if(c!=null && !c.Queued && c.Generation==generation) {c.Queued=true;dirty.Enqueue(c);} }
    // Suppression never declares a POI clear: native cleared-room state and
    // published POI/reset custody only prevent automatic repopulation.
    // Exact manifest revalidation is bounded per candidate, not repeated per native hook.
    internal bool SuppressPurgeRespawn(World world,SleeperVolume volume,bool includeClearedRoom=false)
    {
        if (!RebirthSandboxOptionManager.Current.IsPurge || !CurrentWorld(world) || volume == null) return false;
        // Native cleared-room state survives unload/reload and precedes whole-POI
        // clearance. Do not let an unfinished POI naturally repopulate its cleared
        // rooms while waiting for the remaining rooms or ledger readiness.
        // Explicit administrative/quest Reset calls retain their separate owner.
        if(includeClearedRoom && volume.wasCleared)return true;
        if(volume.prefabInstance==null)return false;
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
                || !snapshot.TryGet(candidate.Identity,out record)
                || record.State!=RebirthPoiClearanceState.Cleared && record.State!=RebirthPoiClearanceState.ResetPending) return false;
            var identity=candidate.Identity;var prefab=volume.prefabInstance;
            if(prefab.boundingBoxPosition.x!=identity.X || prefab.boundingBoxPosition.y!=identity.Y || prefab.boundingBoxPosition.z!=identity.Z
                || prefab.boundingBoxSize.x!=identity.SizeX || prefab.boundingBoxSize.y!=identity.SizeY || prefab.boundingBoxSize.z!=identity.SizeZ
                || prefab.rotation!=identity.Rotation || prefab.prefab==null
                || !string.Equals(prefab.prefab.PrefabName,identity.Prefab,StringComparison.OrdinalIgnoreCase))return false;
            if(candidate.RespawnGateRevision!=snapshot.Revision || now>=candidate.RespawnGateUntil)
            {
                candidate.RespawnGateValid=candidate.Manifest!=null&&candidate.Manifest.StillMatches();
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
            if(RebirthSandboxOptionManager.Current.IsPurge && (!player.IsSpawned() || player.AttachedToEntity!=null || player.IsFlyMode.Value)) return;
            var position=player.GetBlockPosition(); var decorator=GameManager.Instance.GetDynamicPrefabDecorator();
            var prefab=RebirthSandboxOptionManager.Current.IsPurge
                ? RebirthPurgeScannerPoi.Resolve(world,player.position)
                : decorator==null?null:decorator.GetPrefabFromWorldPosInside(position.x,position.z);
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
            var candidate=CaptureCandidate(world,volume); if(candidate==null || candidate.Fault) return;
            Dictionary<int,Actor> actors;
            if(!candidate.Actors.TryGetValue(volume,out actors)) return;
            Actor previous;
            if(actors.TryGetValue(entity.entityId,out previous))
                        {
                if(previous.Entity==null && !previous.Dead && MatchesEnrollment(previous.Observation,volume,entity)) previous.Entity=entity;
                if(previous.Dead || !ReferenceEquals(previous.Entity,entity)) candidate.Fault=true;
                Dirty(candidate);return;
            }
            if(candidate.Actors.Values.Sum(a=>a.Count)+candidate.Unbound.Values.Sum(v=>v.Actors.Count)>=MaximumActorsPerPoi) { candidate.Fault=true;return; }
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
            if(store?.Published!=null && store.Published.TryGet(candidate.Identity,out current) &&
                current.State==RebirthPoiClearanceState.Cleared && candidate.PendingRepopulation==null)
                candidate.PendingRepopulation=new RebirthPoiRepopulationEvidence(candidate.Repopulation,1,world.worldTime);
            candidate.JournalDirty=true; Dirty(candidate);
        }
        catch { }
    }
    internal bool Owns(SleeperVolume volume,RebirthPoiIdentity identity)
    {return volume!=null&&identity!=null&&(candidates.TryGetValue(volume.prefabInstance,out var c)&&identity.Equals(c.Identity)||quarantined.TryGetValue(volume.prefabInstance,out var q)&&identity.Equals(q.Identity));}
    internal void Withhold(World world,SleeperVolume volume)
    {if(volume==null||!CurrentWorld(world))return;var c=CaptureCandidate(world,volume);if(c!=null)c.Fault=true;}
    internal RebirthPoiNativeRestorationWitness BeginRestoration(World world,SleeperVolume volume,int id,SleeperVolume.RespawnData native)
    {
        var c=CaptureCandidate(world,volume);Actor actor=null;
        if(c!=null&&!c.Fault&&c.Actors.TryGetValue(volume,out var actors))actors.TryGetValue(id,out actor);
        var original=actor?.Observation;
        return new RebirthPoiNativeRestorationWitness(world,volume,id,native,original,()=>
            c!=null&&actor!=null&&RestorationScope(c)&&
            c.Actors.TryGetValue(volume,out var retained)&&retained.TryGetValue(id,out var participant)&&ReferenceEquals(participant,actor)&&ReferenceEquals(actor.Observation,original));
    }
    private bool RestorationScope(Candidate c)
    {
        if(c.Fault||c.ResetObserved||c.Generation!=generation||!c.Root.AuthoredCurrent())return false;
        bool owned=candidates.TryGetValue(c.Prefab,out var current)&&ReferenceEquals(current,c)||
            quarantined.TryGetValue(c.Prefab,out var waiting)&&ReferenceEquals(waiting,c);
        if(!owned)return false;
        var snapshot=resolveStore()?.Published;
        if(snapshot==null)return c.Epoch==null;
        if(!snapshot.Binding.IsCurrent||!ReferenceEquals(snapshot.Binding.Scope,c.World))return false;
        if(c.Identity==null)return false;
        if(!snapshot.TryGet(c.Identity,out var record))return c.Epoch==null||c.Epoch==0;
        if(c.Quarantine!=Guid.Empty)return record.State==RebirthPoiClearanceState.ResetPending&&record.ResetId==c.Quarantine&&record.Epoch==c.QuarantineEpoch;
        return record.State!=RebirthPoiClearanceState.ResetPending&&(c.Epoch==null||record.Epoch==c.Epoch);
    }
    internal bool NativeRestored(World world,SleeperVolume volume,EntityAlive entity,RebirthPoiActorObservation original,RebirthPoiActorObservation restored)
    {
        var candidate=CaptureCandidate(world,volume);
        if(candidate==null||candidate.Fault||original==null||restored==null||!candidate.Actors.TryGetValue(volume,out var actors)||
            !actors.TryGetValue(original.EntityId,out var actor)||!ReferenceEquals(actor.Observation,original)||
            !MatchesEnrollment(restored,volume,entity)||actors.ContainsKey(entity.entityId)||!Advance(candidate,actor,restored))return false;
        actors.Remove(original.EntityId);actors.Add(entity.entityId,actor);actor.Entity=entity;Dirty(candidate);return true;
    }
    internal bool IsAcknowledged(SleeperVolume volume,RebirthPoiActorObservation observation)
    {
        var store=resolveStore();var snapshot=store?.Published;
        if(snapshot==null||!snapshot.Binding.IsCurrent||volume==null||!candidates.TryGetValue(volume.prefabInstance,out var c)||
            c.Identity==null||!ReferenceEquals(c.Store,store)||!ReferenceEquals(snapshot.Binding.Scope,c.World)||!snapshot.TryGet(c.Identity,out var record)||
            record.Epoch!=c.Epoch||record.Observations==null||!c.Bindings.TryGetValue(volume,out var room)||
            !record.Observations.Volumes.TryGetValue(room.Id,out var saved)||!saved.Actors.TryGetValue(observation.Token,out var actor))return false;
        return SameActor(actor,observation)||actor.IsCausalSuccessorOf(observation);
    }
    public DeathWitness BeforeDeath(SleeperVolume volume,EntityAlive entity)
    {
        try
        {
            if(volume==null || entity==null || !CurrentWorld(entity.world as World) || !volume.respawnMap.ContainsKey(entity.entityId)) return null;
            Candidate c; Dictionary<int,Actor> actors; Actor actor;
            if((!quarantined.TryGetValue(volume.prefabInstance,out c)||!c.Actors.TryGetValue(volume,out var pendingActors)||!pendingActors.ContainsKey(entity.entityId))&&!candidates.TryGetValue(volume.prefabInstance,out c))return null;
            if(!CurrentDeathCandidate(c,volume,entity) ||
                !c.Actors.TryGetValue(volume,out actors) || !actors.TryGetValue(entity.entityId,out actor) || actor.Dead) return null;
            if(!MatchesEnrollment(actor.Observation,volume,entity))return null;
            if(actor.Entity==null && MatchesEnrollment(actor.Observation,volume,entity)) actor.Entity=entity;
            if(!ReferenceEquals(actor.Entity,entity))return null;
            return new DeathWitness { Candidate=c,Actor=actor,Volume=volume,Entity=entity,Generation=generation,Contributor=RebirthPurgeKillContributor.Capture(entity) };
        }
        catch { return null; }
    }
    public void AfterDeath(DeathWitness witness)
    {
        // Native Kill notifies sleeper volumes before setting death/attribution.
        // Keep the exact enrollment witness; confirm death on the next bounded pulse.
        if(witness!=null && witness.Generation==generation && pendingDeaths.Count<MaximumActorsPerPoi)
            pendingDeaths.Enqueue(witness);
        else if(witness!=null&&witness.Generation==generation&&witness.Candidate is Candidate exhausted)exhausted.Fault=true;
    }
    private void CompleteDeath(DeathWitness witness)
    {
        try
        {
            if(witness==null || witness.Generation!=generation || !CurrentWorld(witness.Entity.world as World) || !witness.Entity.IsDead() || witness.Volume.respawnMap.ContainsKey(witness.Entity.entityId)) return;
            var c=(Candidate)witness.Candidate; var actor=(Actor)witness.Actor;
            if(actor.Dead || actor.Observation?.Dead==true)return; // Duplicate native callback retains the original receipt.
            if(!CurrentDeathCandidate(c,witness.Volume,witness.Entity) ||
                !c.Actors.TryGetValue(witness.Volume,out var actors) ||
                !actors.TryGetValue(witness.Entity.entityId,out var enrolled) || !ReferenceEquals(enrolled,actor) ||
                !ReferenceEquals(actor.Entity,witness.Entity)) return;
                        var original=actor.Observation;
            if(original==null)return;
            var death=new RebirthPoiActorObservation(original.Token,original.EntityId,original.ClassName,original.SpawnPoint,
                Guid.NewGuid(),c.World.worldTime,original.Restorations,original.Checkpoint,original.Inheritance,contributor:RebirthPurgeKillContributor.Capture(witness.Entity)??witness.Contributor);
            if(!Advance(c,actor,death))return;actor.Dead=true; actor.Entity=null; c.JournalDirty=true; Dirty(c);
        }
        catch { }
    }
    // A death belongs to an already enrolled actor and exact native room. Unrelated
    // room readiness must not erase that event; whole-POI publication still requires
    // StillMatches in Pulse. Reset/replaced candidates can never consume this receipt.
    private bool CurrentDeathCandidate(Candidate c,SleeperVolume volume,EntityAlive entity)
    {
        return c!=null && !c.Fault && !c.ResetObserved && c.Generation==generation &&
            ReferenceEquals(c.World,entity.world) && ReferenceEquals(c.Prefab,volume.prefabInstance) &&
            (candidates.TryGetValue(c.Prefab,out var current)&&ReferenceEquals(current,c)||quarantined.TryGetValue(c.Prefab,out var waiting)&&ReferenceEquals(waiting,c));
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
            if(candidate==null||candidate.Fault||candidate.ResetObserved||snapshot==null||!snapshot.Binding.IsCurrent||!snapshot.TryGet(candidate.Identity,out record)||record.State!=RebirthPoiClearanceState.Cleared||record.Clear==null||!candidate.Actors.TryGetValue(volume,out var actors)||actors.Values.Any(a=>!a.Dead))return;
            candidate.Epoch=record.Epoch;actors.Clear();candidate.PendingClear=null;
        }
        catch{ }
    }
    public void NativeReset(World world,SleeperVolume volume)
    {
        try
        {
            Candidate c=null;if(volume!=null&&!candidates.TryGetValue(volume.prefabInstance,out c))c=CaptureCandidate(world,volume);
            if(c==null || volume.wasCleared || volume.isSpawning || volume.numSpawned!=0 || volume.respawnMap.Count!=0) return;
            if(c.Epoch==null){var snapshot=resolveStore()?.Published;RebirthPoiClearanceRecord record;if(snapshot!=null&&snapshot.TryGet(c.Identity,out record))c.Epoch=record.Epoch;}
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
    private static Actor RestoreActor(Candidate candidate,SleeperVolume volume,RebirthPoiActorObservation proof)
    {
        var entity=candidate.World.GetEntity(proof.EntityId) as EntityAlive;
        return new Actor {Observation=proof,Dead=proof.Dead&&proof.TerminalKind==RebirthPoiActorTerminalKind.Death,
            Entity=entity!=null&&!entity.IsDead()&&MatchesEnrollment(proof,volume,entity)?entity:null};
    }
    private static bool SameActor(RebirthPoiActorObservation left,RebirthPoiActorObservation right)
    {return left!=null&&right!=null&&left.CausalDigest==right.CausalDigest&&left.DeathReceipt==right.DeathReceipt&&
        left.DeathTime==right.DeathTime&&left.TerminalKind==right.TerminalKind&&left.Contributor==right.Contributor;}
    private static bool Advance(Candidate candidate,Actor actor,RebirthPoiActorObservation observation)
    {
        if(actor.Observation==null||!observation.IsCausalSuccessorOf(actor.Observation)||actor.Versions.Count>=64)
        {candidate.Fault=true;return false;}
        actor.Versions.Add(actor.Observation);actor.Observation=observation;candidate.JournalDirty=true;return true;
    }
    private static RebirthPoiActorObservation NextObservation(Actor actor,RebirthPoiActorObservation published)
    {
        if(published==null||SameActor(actor.Observation,published)||actor.Observation.IsCausalSuccessorOf(published))return actor.Observation;
        for(int i=actor.Versions.Count-1;i>=0;i--)
            if(!SameActor(actor.Versions[i],published)&&actor.Versions[i].IsCausalSuccessorOf(published))return actor.Versions[i];
        return null;
    }
    private static void RestoreJournal(Candidate candidate,RebirthPoiClearanceRecord record)
    {
        var saved=record.Observations;if(saved==null)return;
        if(saved.Epoch!=record.Epoch || !candidate.Root.ValidateGeneration(saved)||saved.Volumes.Values.Select(v=>v.Descriptor).Distinct().Count()!=saved.Volumes.Count){candidate.Fault=true;return;}
        candidate.JournalGeneration=saved.Generation;
        foreach(var proof in saved.Volumes.Values)
        {
            if(!candidate.Root.KnownDescriptor(proof.Descriptor)){candidate.Fault=true;return;}
            var volume=candidate.World.GetSleeperVolume(proof.NativeVolumeId);
            if(volume==null)
            {
                if(candidate.Bindings.Values.Any(b=>b.Id==proof.NativeVolumeId||b.Descriptor==proof.Descriptor)){candidate.Fault=true;return;}
                candidate.Unbound.Add(proof.NativeVolumeId,proof);continue;
            }
            if(!candidate.Root.TryRoom(volume,out var id,out var descriptor)||id!=proof.NativeVolumeId||descriptor!=proof.Descriptor)
            {candidate.Fault=true;return;}
            if(!candidate.Actors.TryGetValue(volume,out var actors))
            {actors=new Dictionary<int,Actor>();candidate.Actors.Add(volume,actors);candidate.Bindings.Add(volume,new RoomBinding{Id=id,Descriptor=descriptor});}
            foreach(var actor in proof.Actors.Values)
            {
                // Captured fresh actors may predate store readiness. They cannot adopt a saved token.
                if(actors.TryGetValue(actor.EntityId,out var captured))
                {
                    if(!SameActor(captured.Observation,actor)&&NextObservation(captured,actor)==null){candidate.Fault=true;return;}
                }
                else actors.Add(actor.EntityId,RestoreActor(candidate,volume,actor));
            }
        }
    }
    private static RebirthPoiPartialObservation BuildJournal(Candidate candidate,RebirthPoiClearanceRecord record)
    {
        try
        {
            var previous=record.Observations;
            var volumes=new List<RebirthPoiVolumeObservation>(candidate.Unbound.Values);bool changed=previous==null;
            if(previous!=null&&candidate.Unbound.Values.Any(v=>!previous.Volumes.TryGetValue(v.NativeVolumeId,out var old)||
                old.Descriptor!=v.Descriptor||old.Actors.Count!=v.Actors.Count||v.Actors.Values.Any(a=>!old.Actors.TryGetValue(a.Token,out var p)||!SameActor(a,p))))changed=true;
            foreach(var pair in candidate.Bindings)
            {
                var volume=pair.Key;int id=pair.Value.Id;string descriptor=pair.Value.Descriptor;
                if(descriptor==null)
                {
                    if(!candidate.Root.TryCapturedDescriptor(volume,out descriptor)||descriptor==null)return null;
                    pair.Value.Descriptor=descriptor;
                }
                if(id<0||!candidate.Root.KnownDescriptor(descriptor))return null;
                RebirthPoiVolumeObservation old=null;
                if(previous!=null&&previous.Volumes.TryGetValue(id,out old)&&old.Descriptor!=descriptor)return null;
                var actors=candidate.Actors[volume].Values.Select(a=>NextObservation(a,old!=null&&old.Actors.TryGetValue(a.Observation.Token,out var p)?p:null)).ToArray();
                if(actors.Any(a=>a==null))return null;
                if(old==null || actors.Length!=old.Actors.Count || actors.Any(a=>!old.Actors.TryGetValue(a.Token,out var p)||!SameActor(a,p)))changed=true;
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
    { return identity!=null && (candidates.Values.Any(c=>identity.Equals(c.Identity) && (c.Fault || c.ResetObserved))||quarantined.Values.Any(c=>identity.Equals(c.Identity))); }
    internal string ClearObserverName(RebirthPoiIdentity identity)
    {
        if(identity==null)return string.Empty;
        foreach(var candidate in candidates.Values)
        {
            if(!identity.Equals(candidate.Identity))continue;
            var player=candidate.Touched;
            if(player!=null && ReferenceEquals(player.world,candidate.World) &&
                ReferenceEquals(player.world.GetEntity(player.entityId),player))
                return RebirthPurgeDiscoveryNotification.DisplayName(player);
        }
        return string.Empty;
    }
    internal bool TryScannerView(World world, EntityPlayer player, out RebirthPoiNativeManifest manifest, out EntityAlive[] actors)
    {
        manifest=null; actors=null;
        if(!CurrentWorld(world)||player==null||!player.IsSpawned()||!player.IsAlive()||player.IsSpectator||
            player.AttachedToEntity!=null||player.IsFlyMode.Value)return false;
        var prefab=world.GetPOIAtPosition(player.position);
        Candidate candidate;
        if(prefab==null||!candidates.TryGetValue(prefab,out candidate)||candidate.Fault||candidate.ResetObserved||
            (candidate.Manifest==null||!candidate.Manifest.StillMatches()))return false;
        RebirthPoiClearanceRecord record;
        var snapshot=resolveStore()?.Published;
        if(snapshot==null||!snapshot.TryGet(candidate.Identity,out record)||record.ResetOnly||
            record.State!=RebirthPoiClearanceState.Discovered)return false;
        manifest=candidate.Manifest;
        actors=candidate.Actors.Values.SelectMany(room=>room.Values)
            .Where(actor=>!actor.Dead&&actor.Entity!=null&&actor.Entity.IsAlive()&&
                ReferenceEquals(world.GetEntity(actor.Entity.entityId),actor.Entity))
            .Select(actor=>actor.Entity).Distinct().ToArray();
        return true;
    }
    internal string ScannerStatus(World world,EntityPlayer player)
    {
        if(!CurrentWorld(world)||player==null)return "Scanner: no current world/player";
        var prefab=world.GetPOIAtPosition(player.position);
        if(prefab==null)return "Scanner: outside a POI";
        Candidate candidate;
        if(!candidates.TryGetValue(prefab,out candidate))return "Scanner: "+prefab.prefab.PrefabName+", no native observation";
        RebirthPoiClearanceRecord record;
        var snapshot=resolveStore()?.Published;
        string state=snapshot!=null&&snapshot.TryGet(candidate.Identity,out record)
            ?record.State+", resetOnly="+record.ResetOnly:"no clearance record";
        return "Scanner: "+(candidate.Identity?.Prefab??candidate.Prefab.prefab.PrefabName)+", "+state+
            ", fault="+candidate.Fault+", resetObserved="+candidate.ResetObserved+
            ", manifestCurrent="+(candidate.Manifest!=null&&candidate.Manifest.StillMatches())+
            ", spawned="+player.IsSpawned()+", alive="+player.IsAlive()+
            ", spectator="+player.IsSpectator+", attached="+(player.AttachedToEntity!=null)+
            ", fly="+player.IsFlyMode.Value;
    }
    private static bool AcknowledgedLatest(Candidate c,RebirthPoiPartialObservation published)
    {
        if(published==null||published.Volumes.Count!=c.Bindings.Count+c.Unbound.Count)return false;
        foreach(var pair in c.Bindings)
        {
            if(!published.Volumes.TryGetValue(pair.Value.Id,out var room)||room.Descriptor!=pair.Value.Descriptor||room.Actors.Count!=c.Actors[pair.Key].Count)return false;
            foreach(var actor in c.Actors[pair.Key].Values)
            {
                if(!room.Actors.TryGetValue(actor.Observation.Token,out var proof))return false;
                int acknowledged=actor.Versions.FindIndex(v=>SameActor(v,proof));
                if(acknowledged>=0)actor.Versions.RemoveRange(0,acknowledged+1);
                if(!SameActor(proof,actor.Observation))return false;
                actor.Versions.Clear();
            }
        }
        return true;
    }
    private void ConfirmDeaths(int budget=128)
    {for(int n=0,limit=Math.Min(budget,pendingDeaths.Count);n<limit;n++)CompleteDeath(pendingDeaths.Dequeue());}
    private bool HasPendingDeaths(RebirthPoiIdentity identity)
    {return pendingDeaths.Any(w=>w.Generation==generation&&w.Candidate is Candidate c&&identity.Equals(c.Identity));}
    // Bounded offer to the original store, before native reset pins its predecessor.
    // False means no reset may begin; pending exact store payload is retained for retry.
    internal bool DrainForReset(RebirthPoiWorldStore store,World world,IEnumerable<PrefabInstance> prefabs)
    {
        if(store==null||!ReferenceEquals(store,resolveStore())||!CurrentWorld(world)||prefabs==null)return false;
        ConfirmDeaths();
        var targets=new HashSet<PrefabInstance>(prefabs);
        if(pendingDeaths.Any(w=>w.Generation==generation&&w.Candidate is Candidate queued&&targets.Contains(queued.Prefab)))return false;
        foreach(var prefab in targets)
        {
            if(quarantined.ContainsKey(prefab))return false;
            if(!candidates.TryGetValue(prefab,out var c))continue;
            for(int step=0;step<66;step++)
            {
                if(store.HasPending)store.TryRetryPending();
                if(store.HasPending||c.Fault||c.ResetObserved||!c.Root.BindIdentity())return false;
                ProcessCandidate(c,true);
                if(!c.JournalDirty&&c.Restored&&!RebirthPoiNativeRestorationConnection.HasPending(c.Identity))break;
                if(step==65)return false;
                RebirthPoiNativeRestorationConnection.Pulse(true);
            }
        }
        return !store.HasPending;
    }
    internal bool ResetPredecessorCurrent(RebirthPoiWorldStore store,RebirthPoiWorldSnapshot snapshot,RebirthPoiIdentity identity)
    {
        if(store==null||snapshot==null||!ReferenceEquals(store,resolveStore())||!ReferenceEquals(store.Published,snapshot)||
            !snapshot.Binding.IsCurrent||store.HasPending||identity==null||HasPendingDeaths(identity)||RebirthPoiNativeRestorationConnection.HasPending(identity))return false;
        foreach(var c in candidates.Values.Where(c=>identity.Equals(c.Identity)))
            if(!ReferenceEquals(c.Store,store)||c.Fault||c.ResetObserved||!c.Restored||c.JournalDirty||!c.Root.AuthoredCurrent()||
                !snapshot.TryGet(identity,out var record)||record.Epoch!=c.Epoch||!AcknowledgedLatest(c,record.Observations))return false;
        return !quarantined.Values.Any(c=>identity.Equals(c.Identity));
    }
    private static bool CompletedSuccessor(Candidate c,RebirthPoiClearanceRecord record)
    {return c.Epoch!=null&&record.Epoch==c.Epoch+1&&record.LastResetDisposition==RebirthPoiResetDisposition.Completed&&
        record.LastResetId!=Guid.Empty&&record.LastResetId!=c.BoundReset;}
    private Candidate Transition(Candidate old,RebirthPoiClearanceRecord record)
    {
        if(!CompletedSuccessor(old,record))return old;
        Candidate next;
        if(quarantined.TryGetValue(old.Prefab,out next))
        {
            if(next.Quarantine!=record.LastResetId||next.QuarantineEpoch!=old.Epoch){next.Fault=true;return old;}
        }
        else
        {
            if(!RebirthPoiNativeManifest.RoomCertificate.TryCapture(old.World,old.Prefab,out var root))return old;
            next=new Candidate{Root=root,Store=old.Store,Generation=generation};
        }
        next.Quarantine=Guid.Empty;BindJournal(next);
        if(!next.Restored||next.Fault||!MergePendingDeaths(old,next,record)){next.Fault=true;old.Fault=true;return old;}
        candidates[old.Prefab]=next;quarantined.Remove(old.Prefab);next.JournalDirty=true;Dirty(next);return next;
    }
    private static bool MergePendingDeaths(Candidate old,Candidate next,RebirthPoiClearanceRecord record)
    {
        foreach(var room in old.Actors)
        foreach(var actor in room.Value.Values.Where(a=>a.Dead&&a.Versions.Any(v=>!v.Dead)))
        {
            var death=actor.Observation;Actor target=null;RebirthPoiVolumeObservation unloaded=null;
            foreach(var map in next.Actors.Values)
                foreach(var value in map.Values)if(value.Observation.Token==death.Token)target=value;
            if(target==null)unloaded=next.Unbound.Values.SingleOrDefault(v=>v.Actors.ContainsKey(death.Token));
            var original=target?.Observation??unloaded?.Actors[death.Token];
            var outcome=record.LastAuthoredReset!=null&&record.LastAuthoredReset.PriorActors.TryGetValue(death.Token,out var retained)?retained:null;
            if(original==null)
            {
                // Only a positively recorded native removal can resolve this old actor.
                if(outcome!=null&&outcome.Disposition==RebirthPoiPriorActorDisposition.NativeRemoved&&outcome.SourceEpoch==old.Epoch&&outcome.ActorDigest==death.CausalDigest)continue;
                return false;
            }
            if(SameActor(original,death))continue;
            if(original.Dead||original.EntityId!=death.EntityId||original.ClassName!=death.ClassName||original.SpawnPoint!=death.SpawnPoint||original.LineageCanonical!=death.LineageCanonical)return false;
            if(original.CausalDigest!=death.CausalDigest&&
                (outcome==null||outcome.SourceEpoch!=old.Epoch||outcome.ActorDigest!=death.CausalDigest||
                original.Inheritance==null||original.Inheritance.ResetTransaction!=record.LastResetId||original.Inheritance.SurvivalReceipt!=outcome.Receipt))return false;
            var inherited=new RebirthPoiActorObservation(death.Token,death.EntityId,death.ClassName,death.SpawnPoint,death.DeathReceipt,death.DeathTime,
                death.Restorations,death.Checkpoint,original.Inheritance,death.TerminalKind,death.Contributor);
            if(!inherited.IsCausalSuccessorOf(original))return false;
            if(target!=null){if(!Advance(next,target,inherited))return false;target.Dead=true;target.Entity=null;}
            else next.Unbound[unloaded.NativeVolumeId]=new RebirthPoiVolumeObservation(unloaded.NativeVolumeId,unloaded.Descriptor,
                unloaded.Actors.Values.Select(a=>a.Token==death.Token?inherited:a),unloaded.Generation);
            next.JournalDirty=true;
        }
        return true;
    }
    private void ReleaseQuarantine()
    {
        var snapshot=resolveStore()?.Published;
        if(snapshot==null||!snapshot.Binding.IsCurrent)return;
        foreach(var pair in quarantined.Take(4).ToArray())
        {
            var q=pair.Value;
            if(q.Fault||!q.Root.BindIdentity()||!ReferenceEquals(snapshot.Binding.Scope,q.World)||!snapshot.TryGet(q.Identity,out var record)||
                record.State==RebirthPoiClearanceState.ResetPending)continue;
            bool completed=record.Epoch==q.QuarantineEpoch+1&&record.LastResetDisposition==RebirthPoiResetDisposition.Completed;
            bool unchanged=record.Epoch==q.QuarantineEpoch&&record.LastResetDisposition==RebirthPoiResetDisposition.NoMutation;
            if(record.LastResetId!=q.Quarantine||!completed&&!unchanged){q.Fault=true;continue;}
            if(unchanged&&candidates.TryGetValue(pair.Key,out var contradicted)&&contradicted.ResetObserved){q.Fault=true;continue;}
            if(completed&&candidates.TryGetValue(pair.Key,out var predecessor)){Transition(predecessor,record);continue;}
            // The terminal published journal owns all prior obligations and room bindings.
            // Merge only under the recomputed authoring seal / legacy full-map validation.
            q.Quarantine=Guid.Empty;q.Restored=false;q.Epoch=record.Epoch;
            BindJournal(q);
            if(candidates.TryGetValue(pair.Key,out var original)&&!MergePendingDeaths(original,q,record))q.Fault=true;
            if(q.Fault)continue;
            candidates[pair.Key]=q;quarantined.Remove(pair.Key);q.JournalDirty=true;Dirty(q);
        }
    }
    internal enum ShutdownDrainResult { Complete, Incomplete, IoPending, StaleScope }
    private bool shutdownPrepared;
    internal ShutdownDrainResult LastShutdownResult {get;private set;}
    internal ShutdownDrainResult PrepareShutdown()
    {
        if(shutdownPrepared)return LastShutdownResult;
        shutdownPrepared=true;LastShutdownResult=ShutdownDrainResult.Complete;
        var store=resolveStore();
        if(store?.Published==null)
        {
            if(candidates.Count!=0||quarantined.Count!=0||pendingDeaths.Count!=0)return ShutdownFailed(ShutdownDrainResult.Incomplete);
            return LastShutdownResult;
        }
        ConfirmDeaths(pendingDeaths.Count);ReleaseQuarantine();
        // Finite retained-work drain, not a global publication cutoff. Each candidate
        // has at most64 pre-ack actor transitions; each retry must acknowledge progress.
        foreach(var captured in candidates.Values.ToArray())
        {
            var c=captured;
            int maximum=6+c.Actors.Values.SelectMany(m=>m.Values).Select(a=>a.Versions.Count).DefaultIfEmpty(0).Max();
            bool complete=false;
            for(int step=0;step<maximum;step++)
            {
                if(!ReferenceEquals(store,resolveStore())||!store.Published.Binding.IsCurrent)
                    return ShutdownFailed(ShutdownDrainResult.StaleScope);
                if(store.HasPending)store.TryRetryPending();
                if(store.HasPending)return ShutdownFailed(ShutdownDrainResult.IoPending);
                long before=store.Published.Revision;
                ProcessCandidate(c,true);RebirthPoiNativeRestorationConnection.Pulse(true);
                if(candidates.TryGetValue(c.Prefab,out var current))c=current;
                if(store.HasPending)return ShutdownFailed(ShutdownDrainResult.IoPending);
                if(!c.Fault&&!c.ResetObserved&&c.Restored&&!c.JournalDirty&&c.Identity!=null&&
                    !quarantined.ContainsKey(c.Prefab)&&!RebirthPoiNativeRestorationConnection.HasUnresolved(c.Identity)&&
                    store.Published.TryGet(c.Identity,out var record)&&record.Epoch==c.Epoch&&AcknowledgedLatest(c,record.Observations))
                {complete=true;break;}
                if(store.Published.Revision==before)break;
            }
            if(!complete)LastShutdownResult=ShutdownDrainResult.Incomplete;
        }
        if(quarantined.Count!=0||pendingDeaths.Count!=0)LastShutdownResult=ShutdownDrainResult.Incomplete;
        if(LastShutdownResult!=ShutdownDrainResult.Complete)return ShutdownFailed(LastShutdownResult);
        return LastShutdownResult;
    }
    private ShutdownDrainResult ShutdownFailed(ShutdownDrainResult result)
    {
        LastShutdownResult=result;
        Log.Warning("[REBIRTH Purge] Final native custody drain "+result+"; unacknowledged callbacks are not durable.");
        return result;
    }    public void Pulse()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        ConfirmDeaths();ReleaseQuarantine();
        double now=clock();if(now<nextPulse)return;nextPulse=now+0.5;
        if(dirty.Count==0)return;
        var c=dirty.Dequeue();c.Queued=false;
        if(c.Generation!=generation || !candidates.TryGetValue(c.Prefab,out var currentCandidate) || !ReferenceEquals(currentCandidate,c) || !CurrentWorld(c.World))return;
        ProcessCandidate(c,false);
    }
    private void ProcessCandidate(Candidate c,bool journalOnly)
    {
        // Reset terminal acknowledgement precedes retirement of the old volatile
        // candidate, including deaths captured between intent and native effects.
        var transitionSnapshot=resolveStore()?.Published;
        if(c.Epoch!=null&&c.Identity!=null&&transitionSnapshot!=null&&transitionSnapshot.Binding.IsCurrent&&
            ReferenceEquals(c.Store,resolveStore())&&transitionSnapshot.TryGet(c.Identity,out var terminal))
        {
            if(terminal.State==RebirthPoiClearanceState.ResetPending){Dirty(c);return;}
            if(CompletedSuccessor(c,terminal))
            {var next=Transition(c,terminal);if(!ReferenceEquals(next,c))ProcessCandidate(next,journalOnly);return;}
        }
        // Retain pending journal work across temporary room/metadata readiness loss.
        // Reuse the bounded dirty queue; every retry revalidates the exact candidate.
        if(c.Fault||c.ResetObserved||!c.Root.AuthoredCurrent())return;
        Promote(c);BindJournal(c);
        if(c.Identity==null||!c.Restored){Dirty(c);return;}
        var store=resolveStore();if(c.Store!=null&&!ReferenceEquals(store,c.Store)){c.Fault=true;return;}
        var snapshot=store==null?null:store.Published;
        if(snapshot==null) {Dirty(c);return;}
        RebirthPoiClearanceRecord record;
        if(!snapshot.TryGet(c.Identity,out record))
        {
            var discovery=store.TryDiscover(snapshot,c.Identity);
            if(discovery!=RebirthPoiStoreResult.Published && discovery!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}
            snapshot=store.Published;if(snapshot==null || !snapshot.TryGet(c.Identity,out record)){Dirty(c);return;}
            if(discovery==RebirthPoiStoreResult.Published)RebirthPurgeDiscoveryNotification.Send(c.Identity,c.Touched);
        }
        if(record.State==RebirthPoiClearanceState.ResetPending)return;
        if(CompletedSuccessor(c,record))
        {Transition(c,record);return;}
        if(record.ResetOnly)
        {
            var promotion=store.TryDiscover(snapshot,c.Identity);
            if(promotion!=RebirthPoiStoreResult.Published&&promotion!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}
            snapshot=store.Published;
            if(snapshot==null||!snapshot.TryGet(c.Identity,out record)||record.ResetOnly)return;
            // A reset placeholder was never a player discovery. Its first live
            // promotion has the same feedback as first discovery of a new POI.
            if(promotion==RebirthPoiStoreResult.Published)RebirthPurgeDiscoveryNotification.Send(c.Identity,c.Touched);
        }
        // Earned map reveals do not establish a discoverer. Capture the first
        // authenticated on-foot native visit even when a marker already exists.
        if(record.State==RebirthPoiClearanceState.Discovered && c.Touched!=null && RebirthPurgeKillContributor.Identify(c.Touched,c.World)!=null
            && c.Touched.IsSpawned()&&!c.Touched.IsDead()&&!c.Touched.IsSpectator
            && c.Touched.AttachedToEntity==null&&!c.Touched.IsFlyMode.Value)
            RebirthPurgePoiHistory.Discover(c.Identity,RebirthPurgeDiscoveryNotification.DisplayName(c.Touched));
        if(c.PendingRepopulation!=null && c.Epoch!=null && record.Epoch==c.Epoch.Value+1 && record.LastRepopulation!=null &&
            record.LastRepopulation.GenerationId==c.PendingRepopulation.GenerationId &&
            record.LastRepopulation.LiveNativeParticipants==c.PendingRepopulation.LiveNativeParticipants &&
            record.LastRepopulation.WorldTime==c.PendingRepopulation.WorldTime)
        { c.Epoch=record.Epoch;c.PendingRepopulation=null;c.PendingClear=null;c.Proof=Guid.NewGuid(); c.JournalDirty=true; }
        if(c.Epoch==null)c.Epoch=record.Epoch;
        if(c.Epoch!=record.Epoch || c.Fault)return;
        int live=c.Actors.Values.Sum(a=>a.Values.Count(actor=>!actor.Dead && actor.Entity!=null && !actor.Entity.IsDead()));
        if(record.State==RebirthPoiClearanceState.Cleared)
        {
            if(live==0 && c.PendingRepopulation==null)return;
            var evidence=c.PendingRepopulation??(c.PendingRepopulation=new RebirthPoiRepopulationEvidence(c.Repopulation,live,c.World.worldTime));
            var result=store.TryObserveRepopulation(snapshot,c.Identity,record.Epoch,evidence);
            if(result!=RebirthPoiStoreResult.Published && result!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}
            c.Epoch=record.Epoch+1; c.Proof=Guid.NewGuid(); c.JournalDirty=true; c.PendingRepopulation=null; c.PendingClear=null; Dirty(c); return;
        }
        // Persist native enrollment and deaths before publishing a clear. Failed writes are
        // retried by the existing exact-candidate store; new observations never replace it.
        if(c.JournalDirty)
        {
            if(store.HasPending || RebirthPoiNativeRestorationConnection.HasUnresolved(c.Identity)){Dirty(c);return;}
            var observations=BuildJournal(c,record);
            if(observations==null){c.Fault=true;return;}
            if(record.Observations==null || record.Observations.Canonical!=observations.Canonical)
            {
                var publication=store.TryObservePartial(snapshot,c.Identity,record.Epoch,observations);
                if(publication!=RebirthPoiStoreResult.Published && publication!=RebirthPoiStoreResult.Duplicate){Dirty(c);return;}
                snapshot=store.Published;
                if(snapshot==null || !snapshot.TryGet(c.Identity,out record)){Dirty(c);return;}
            }
            c.JournalDirty=!AcknowledgedLatest(c,record.Observations);
            if(c.JournalDirty){Dirty(c);return;}
        }
        if(journalOnly||quarantined.ContainsKey(c.Prefab)||c.Manifest==null||!c.Manifest.StillMatches()||c.Unbound.Count!=0||RebirthPoiNativeRestorationConnection.HasPending(c.Identity))return;
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
        var proof=c.PendingClear??(c.PendingClear=new RebirthPoiClearEvidence(c.Proof,c.Manifest.Volumes.Length,c.Manifest.Volumes.Length,spawned,0,0,false,c.World.worldTime));
        var clear=store.TryClear(snapshot,c.Identity,record.Epoch,proof);
        if(clear!=RebirthPoiStoreResult.Published && clear!=RebirthPoiStoreResult.Duplicate)Dirty(c);
    }
}










