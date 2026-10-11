using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// Original native SetDead receipts for already saved inherited participants.
// No enrollment, lookup-based death, lazy hydration, marker or reset inference.
internal sealed class RebirthPoiInheritedActorOutcomes
{
    internal static readonly RebirthPoiInheritedActorOutcomes Instance=new RebirthPoiInheritedActorOutcomes();
    private readonly Func<RebirthPoiWorldStore> resolve;
    private readonly Func<double> clock;
    private readonly Func<long> ownerGeneration;
    private readonly Queue<Death> pending=new Queue<Death>();
    private double nextPulse;
    internal sealed class Obligation
    {internal RebirthPoiIdentity Identity;internal long Epoch;internal Guid Generation,Reset;internal int Volume;internal string Descriptor,Digest;}
    internal sealed class Death
    {internal RebirthPoiWorldStore Store;internal RebirthPoiWorldBinding Binding;internal World World;internal EntityAlive Entity;internal Guid Token,Receipt;internal int EntityId;internal string Class,Contributor;internal ulong Time;internal List<Obligation> Obligations;internal long OwnerGeneration;internal bool Completed;internal RebirthPoiActorTerminalKind Kind;}
    internal RebirthPoiInheritedActorOutcomes(Func<RebirthPoiWorldStore> storeResolver=null,Func<double> monotonic=null,Func<long> lifecycleGeneration=null)
    {resolve=storeResolver??(()=>{RebirthPoiWorldStore store;return RebirthPoiWorldLifecycle.Instance.TryGetEvidenceStore(out store)?store:null;});ownerGeneration=lifecycleGeneration??(storeResolver==null?(()=>RebirthPoiWorldLifecycle.Instance.Generation):(()=>0L));clock=monotonic??(()=>((double)Stopwatch.GetTimestamp()/Stopwatch.Frequency));}
    private static bool NativeCurrent(World world,EntityAlive entity,int id,Guid token,string name,bool present=true)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||GameManager.Instance==null||!ReferenceEquals(GameManager.Instance.World,world)||entity==null||!ReferenceEquals(entity.world,world)||entity.entityId!=id)return false;
        // GetEntity would call EnsureEntity and hydrate an async participant.
        bool exists=world.Entities.dict.TryGetValue(id,out var native);if(present?(!exists||!ReferenceEquals(native,entity)):exists)return false;
        Guid actual;EntityClass type;return RebirthPoiNativeActorStamp.TryRead(entity,out actual)&&actual==token&&EntityClass.list.TryGetValue(entity.entityClass,out type)&&type.entityClassName==name;
    }
    internal Death BeforeSetDead(EntityAlive entity)
    {
        try
        {
            if(entity==null||pending.Count>=128)return null;Guid token;EntityClass type;
            if(!RebirthPoiNativeActorStamp.TryRead(entity,out token)||!EntityClass.list.TryGetValue(entity.entityClass,out type))return null;
            var world=entity.world as World;if(!NativeCurrent(world,entity,entity.entityId,token,type.entityClassName))return null;
            var store=resolve();var snapshot=store==null?null:store.Published;
            if(snapshot==null||!snapshot.Binding.IsCurrent||!ReferenceEquals(snapshot.Binding.Scope,world))return null;
            var obligations=new List<Obligation>();
            foreach(var shard in snapshot.Shards.Values)foreach(var record in shard.Records.Values)
            {
                if((record.State!=RebirthPoiClearanceState.Discovered&&record.State!=RebirthPoiClearanceState.ResetPending)||record.Observations==null)continue;
                foreach(var volume in record.Observations.Volumes.Values)
                {
                    RebirthPoiActorObservation actor;if(!volume.Actors.TryGetValue(token,out actor)||actor.Dead||(actor.Inheritance==null&&record.State!=RebirthPoiClearanceState.ResetPending)||actor.EntityId!=entity.entityId||actor.ClassName!=type.entityClassName)continue;
                    if(obligations.Count>=8192)return null;
                    obligations.Add(new Obligation{Identity=record.Identity,Epoch=record.Epoch,Reset=record.ResetId,Generation=record.Observations.EffectiveGeneration(volume),Volume=volume.NativeVolumeId,Descriptor=volume.Descriptor,Digest=actor.CausalDigest});
                }
            }
            if(obligations.Count==0)return null;
            return new Death{OwnerGeneration=ownerGeneration(),Store=store,Binding=snapshot.Binding,World=world,Entity=entity,Token=token,Receipt=Guid.NewGuid(),EntityId=entity.entityId,Class=type.entityClassName,Obligations=obligations,Contributor=RebirthPurgeKillContributor.Capture(entity),Kind=RebirthPoiActorTerminalKind.Death};
        }
        catch{return null;}
    }
    internal Death BeforeUnload(World world,EntityAlive entity,EnumRemoveEntityReason reason)
    {
        if(reason!=EnumRemoveEntityReason.Despawned||entity==null||!ReferenceEquals(entity.world,world)||!entity.IsDespawned||entity.bWillRespawn||!entity.IsMarkedForUnload())return null;
        var receipt=BeforeSetDead(entity);if(receipt!=null){receipt.Kind=RebirthPoiActorTerminalKind.NativeDespawn;receipt.Contributor=null;}return receipt;
    }
    internal bool AfterSetDead(Death original,bool originalRan)
    {
        try{return original!=null&&original.Kind==RebirthPoiActorTerminalKind.Death&&original.Entity.bDead&&Finish(original,originalRan,true);}catch{return false;}
    }
    internal bool AfterUnload(Death original,bool originalRan,EnumRemoveEntityReason reason)
    {
        try{return original!=null&&reason==EnumRemoveEntityReason.Despawned&&original.Kind==RebirthPoiActorTerminalKind.NativeDespawn&&original.Entity.IsDespawned&&!original.Entity.bWillRespawn&&Finish(original,originalRan,false);}catch{return false;}
    }
    private bool Finish(Death original,bool originalRan,bool present)
    {
        if(original.OwnerGeneration!=ownerGeneration()||original.Completed||!originalRan||!original.Binding.IsCurrent||!NativeCurrent(original.World,original.Entity,original.EntityId,original.Token,original.Class,present))return false;
        original.Completed=true;original.Time=original.World.worldTime;
        bool applied;try{applied=Apply(original);}catch{applied=false;}
        if(!applied){if(pending.Count>=128)return false;pending.Enqueue(original);}
        return true;
    }
    // Retry retains the original proof/world/epoch/generation; no transplant into
    // a replacement world, restored successor or later reset generation.
    private bool Apply(Death original)
    {
        if(!original.Completed||original.OwnerGeneration!=ownerGeneration()||!original.Binding.IsCurrent)return true;
        foreach(var obligation in original.Obligations)
        {
            var snapshot=original.Store.Published;if(snapshot==null||!ReferenceEquals(snapshot.Binding,original.Binding))return true;
            if(original.Store.HasPending)
            {var retry=original.Store.TryRetryPending();if(retry!=RebirthPoiStoreResult.Published&&retry!=RebirthPoiStoreResult.Duplicate)return false;snapshot=original.Store.Published;}
            RebirthPoiClearanceRecord record;RebirthPoiVolumeObservation volume;RebirthPoiActorObservation actor;
            if(!snapshot.TryGet(obligation.Identity,out record)||record.Observations==null)continue;
            volume=null;actor=null;
            if(record.Epoch==obligation.Epoch)
            {
                if(!record.Observations.Volumes.TryGetValue(obligation.Volume,out volume)||volume.Descriptor!=obligation.Descriptor||record.Observations.EffectiveGeneration(volume)!=obligation.Generation||!volume.Actors.TryGetValue(original.Token,out actor)||actor.EntityId!=original.EntityId||actor.ClassName!=original.Class||actor.CausalDigest!=obligation.Digest)continue;
            }
            else
            {
                // Native death/unload can finish between reset intent and durable
                // successor. Cross exactly that saved transaction using its original
                // actor receipt, not a later matching ID or guessed generation.
                RebirthPoiResetActorOutcome prior;
                if(obligation.Reset==Guid.Empty||obligation.Epoch==long.MaxValue||record.Epoch!=obligation.Epoch+1||record.LastResetId!=obligation.Reset||record.LastAuthoredReset==null||!record.LastAuthoredReset.PriorActors.TryGetValue(original.Token,out prior)||prior.SourceEpoch!=obligation.Epoch||prior.SourceGeneration!=obligation.Generation||prior.Descriptor!=obligation.Descriptor||prior.ActorDigest!=obligation.Digest||prior.EntityId!=original.EntityId)continue;
                var matches=record.Observations.Volumes.Values.Where(v=>v.Descriptor==obligation.Descriptor&&v.Actors.ContainsKey(original.Token)).ToArray();
                if(matches.Length!=1)continue;volume=matches[0];actor=volume.Actors[original.Token];
                if(actor.EntityId!=original.EntityId||actor.ClassName!=original.Class||actor.Inheritance==null||actor.Inheritance.ResetTransaction!=obligation.Reset||actor.Inheritance.SurvivalReceipt!=prior.Receipt)continue;
            }
            if(actor.Dead)continue;
            if(record.State==RebirthPoiClearanceState.ResetPending)return false;
            if(record.State!=RebirthPoiClearanceState.Discovered||record.Observations.Revision==long.MaxValue)continue;
            var terminal=new RebirthPoiActorObservation(actor.Token,actor.EntityId,actor.ClassName,actor.SpawnPoint,original.Receipt,original.Time,actor.Restorations,actor.Checkpoint,actor.Inheritance,original.Kind,original.Contributor);
            var successor=new RebirthPoiPartialObservation(record.Observations.Generation,record.Epoch,record.Observations.Revision+1,record.Observations.Volumes.Values.Select(v=>v.NativeVolumeId==volume.NativeVolumeId?new RebirthPoiVolumeObservation(v.NativeVolumeId,v.Descriptor,v.Actors.Values.Select(a=>a.Token==original.Token?terminal:a),v.Generation):v));
            var result=original.Store.TryObservePartial(snapshot,record.Identity,record.Epoch,successor);
            if(result==RebirthPoiStoreResult.Uncertain||result==RebirthPoiStoreResult.IoFailure||result==RebirthPoiStoreResult.Conflict)return false;
        }
        return true;
    }
    internal int PendingCount {get{return pending.Count;} }
    internal void Pulse()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        double now=clock();if(now<nextPulse)return;nextPulse=now+1;
        int count=Math.Min(4,pending.Count);for(int i=0;i<count;i++){var receipt=pending.Dequeue();try{if(!Apply(receipt))pending.Enqueue(receipt);}catch{pending.Enqueue(receipt);}}
    }
}