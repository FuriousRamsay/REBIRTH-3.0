using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal enum RebirthPoiResetProtocolState { PendingAdmission, Ready, Running, PendingCompletion, Completed, Refused, Unknown }
// Caller-owned protocol. Native installation is deliberately separate: callers must await
// this exact operation and consume Completed, never turn child refusal into vanilla success.
internal sealed class RebirthPoiResetCallerProtocol : IEnumerator,IDisposable
{
    private readonly RebirthPoiWorldStore store;
    private readonly RebirthPoiWorldSnapshot original;
    private readonly RebirthPoiClearanceRecord originalRecord;
    private readonly RebirthPoiResetPlan plan;
    private readonly IEnumerator native;
    private readonly Func<double> clock;
    private readonly HashSet<long> copied=new HashSet<long>(),regenerated=new HashSet<long>();
    private readonly HashSet<int> resetVolumes=new HashSet<int>(),resetTriggers=new HashSet<int>();
    private readonly HashSet<int> unchangedVolumes=new HashSet<int>();
    private RebirthPoiPartialObservation retainedObservation;
    private RebirthPoiAuthoredResetBindings terminalBindings;
    private readonly Dictionary<string,RebirthPoiAuthoredRuntimeBinding> runtimeBindings=new Dictionary<string,RebirthPoiAuthoredRuntimeBinding>(StringComparer.Ordinal);
    private readonly Dictionary<Guid,RebirthPoiResetActorOutcome> priorActors=new Dictionary<Guid,RebirthPoiResetActorOutcome>();
    private readonly HashSet<string> resetAuthored=new HashSet<string>(StringComparer.Ordinal);
    private bool advanced,ended,failed,cancelled,triggersRefreshed;
    private double nextAttempt;
    private object current;
    public RebirthPoiResetProtocolState State {get;private set;}
    public Exception NativeFailure {get;private set;}
    public object Current {get{return current;} }
    public RebirthPoiResetCallerProtocol(RebirthPoiWorldStore originalStore,RebirthPoiWorldSnapshot predecessor,RebirthPoiIdentity identity,RebirthPoiResetPlan envelope,IEnumerator originalNative,Func<double> monotonicClock)
    {
        if(originalStore==null||predecessor==null||identity==null||envelope==null||originalNative==null||monotonicClock==null||!ReferenceEquals(originalStore.Published,predecessor)||!predecessor.TryGet(identity,out originalRecord)||originalRecord.State==RebirthPoiClearanceState.ResetPending)throw new ArgumentException("Invalid original reset caller.");
        store=originalStore;original=predecessor;plan=envelope;native=originalNative;clock=monotonicClock;State=RebirthPoiResetProtocolState.PendingAdmission;
    }
    private bool Scope(){return original.Binding.IsCurrent&&ReferenceEquals(original.Binding,store.Published==null?null:store.Published.Binding);}
    private bool Receipt(RebirthPoiClearanceRecord record){return record.Epoch==originalRecord.Epoch&&record.State==RebirthPoiClearanceState.ResetPending&&record.ResetId==plan.Transaction&&record.ResetPlan!=null&&record.ResetPlan.Canonical==plan.Canonical;}
    public void Poll()
    {
        if(State==RebirthPoiResetProtocolState.Completed||State==RebirthPoiResetProtocolState.Refused||State==RebirthPoiResetProtocolState.Unknown)return;
        if(!Scope()){State=advanced?RebirthPoiResetProtocolState.Unknown:RebirthPoiResetProtocolState.Refused;return;}
        if(failed){State=RebirthPoiResetProtocolState.Unknown;return;}
        double now=clock();if(now<nextAttempt)return;nextAttempt=now+1;
        if(store.HasPending)return;
        var snapshot=store.Published;RebirthPoiClearanceRecord record;if(snapshot==null||!snapshot.TryGet(originalRecord.Identity,out record)){State=advanced?RebirthPoiResetProtocolState.Unknown:RebirthPoiResetProtocolState.Refused;return;}
        if(State==RebirthPoiResetProtocolState.PendingAdmission)
        {
            if(Receipt(record)){State=RebirthPoiResetProtocolState.Ready;}
            else
            {
            if(!ReferenceEquals(record,originalRecord)){State=RebirthPoiResetProtocolState.Refused;return;}
            var result=store.TryBeginReset(snapshot,originalRecord.Identity,originalRecord.Epoch,plan.Transaction,plan);
            if(result==RebirthPoiStoreResult.Published||result==RebirthPoiStoreResult.Duplicate)State=RebirthPoiResetProtocolState.Ready;
            else if(result!=RebirthPoiStoreResult.Uncertain&&result!=RebirthPoiStoreResult.IoFailure)State=RebirthPoiResetProtocolState.Refused;
            }
        }
        if(cancelled&&State==RebirthPoiResetProtocolState.Ready)
        {
            snapshot=store.Published;
            if(snapshot!=null&&snapshot.TryGet(originalRecord.Identity,out record)&&record.Epoch==originalRecord.Epoch&&record.LastResetId==plan.Transaction&&record.LastResetDisposition==RebirthPoiResetDisposition.NoMutation){State=RebirthPoiResetProtocolState.Refused;return;}
            if(snapshot==null||!snapshot.TryGet(originalRecord.Identity,out record)||!Receipt(record)){State=RebirthPoiResetProtocolState.Unknown;return;}
            var result=store.TryFinishReset(snapshot,originalRecord.Identity,originalRecord.Epoch,plan.Transaction,RebirthPoiResetDisposition.NoMutation);
            if(result==RebirthPoiStoreResult.Published||result==RebirthPoiStoreResult.Duplicate)State=RebirthPoiResetProtocolState.Refused;
            return;
        }
        if(State==RebirthPoiResetProtocolState.PendingCompletion)
        {
            // Recover an exact terminal receipt after lifecycle resolved a lost response.
            if(record.Epoch==originalRecord.Epoch+1&&record.LastResetId==plan.Transaction&&record.LastResetDisposition==RebirthPoiResetDisposition.Completed){if(originalRecord.Observations!=null&&(retainedObservation==null||record.Observations==null||record.Observations.Canonical!=retainedObservation.Canonical)){State=RebirthPoiResetProtocolState.Unknown;return;}if(terminalBindings!=null&&(record.LastAuthoredReset==null||record.LastAuthoredReset.Canonical!=terminalBindings.Canonical)){State=RebirthPoiResetProtocolState.Unknown;return;}State=RebirthPoiResetProtocolState.Completed;return;}
            if(!Receipt(record)){State=RebirthPoiResetProtocolState.Unknown;return;}
            if(!ended||!copied.SetEquals(plan.Chunks)||!regenerated.SetEquals(plan.Chunks)||(plan.IsAuthored?!resetAuthored.SetEquals(plan.Authored.Select(e=>e.Key)):!resetVolumes.SetEquals(plan.Volumes)||!resetTriggers.SetEquals(plan.Triggers))||plan.RequiresTriggerRefresh&&!triggersRefreshed){State=RebirthPoiResetProtocolState.Unknown;return;}
            if(terminalBindings==null&&(plan.IsAuthored||runtimeBindings.Count>0||priorActors.Count>0)){try{terminalBindings=new RebirthPoiAuthoredResetBindings(plan,runtimeBindings.Values,originalRecord.Epoch,priorActors.Values);}catch(ArgumentException){State=RebirthPoiResetProtocolState.Unknown;return;}}
            if(originalRecord.Observations!=null&&retainedObservation==null){
                var required=originalRecord.Observations.Volumes.Values.Where(v=>plan.IsAuthored?!plan.Authored.Any(e=>e.Kind==RebirthPoiAuthoredResetKind.Sleeper&&e.Descriptor==v.Descriptor):!plan.Volumes.Contains(v.NativeVolumeId)).Select(v=>v.NativeVolumeId);if(!unchangedVolumes.SetEquals(required)){State=RebirthPoiResetProtocolState.Unknown;return;}
                retainedObservation=originalRecord.Observations.RollOverVerifiedReset(plan,terminalBindings);if(retainedObservation==null){State=RebirthPoiResetProtocolState.Unknown;return;}
            }
            var result=store.TryFinishReset(snapshot,originalRecord.Identity,originalRecord.Epoch,plan.Transaction,RebirthPoiResetDisposition.Completed,retainedObservation,terminalBindings);
            if(result==RebirthPoiStoreResult.Published||result==RebirthPoiStoreResult.Duplicate)State=RebirthPoiResetProtocolState.Completed;
            else if(result!=RebirthPoiStoreResult.Uncertain&&result!=RebirthPoiStoreResult.IoFailure)State=RebirthPoiResetProtocolState.Unknown;
        }
    }
    // Hooks must supply original native scope/transaction and positive native result. Unknown
    // or foreign effects cannot satisfy the envelope, even if native iterator ends normally.
    public void ChunkCopied(object scope,Guid transaction,long key,bool success){ObserveChunk(scope,transaction,key,success,copied);}
    public void ChunkRegenerated(object scope,Guid transaction,long key,bool success){ObserveChunk(scope,transaction,key,success,regenerated);}
    private void ObserveChunk(object scope,Guid transaction,long key,bool success,HashSet<long> target)
    {if(!WitnessScope(scope,transaction))return;if(!plan.Chunks.Contains(key)){failed=true;return;}if(!success){failed=true;return;}target.Add(key);}
    public void VolumeReset(object scope,Guid transaction,int id){if(!WitnessScope(scope,transaction))return;if(!plan.Volumes.Contains(id)){failed=true;return;}resetVolumes.Add(id);}
    public void TriggerReset(object scope,Guid transaction,int id){if(!WitnessScope(scope,transaction))return;if(!plan.Triggers.Contains(id)){failed=true;return;}resetTriggers.Add(id);}
    public void RuntimeBindingVerified(object scope,Guid transaction,RebirthPoiAuthoredRuntimeBinding binding)
    {
        if(!WitnessScope(scope,transaction))return;
        if(binding==null){failed=true;return;}
        RebirthPoiAuthoredRuntimeBinding originalBinding;
        if(runtimeBindings.TryGetValue(binding.ExpectationKey,out originalBinding))
        {if(originalBinding.Descriptor!=binding.Descriptor||originalBinding.NativeId!=binding.NativeId||originalBinding.Receipt!=binding.Receipt)failed=true;return;}
        if(runtimeBindings.Count>=8192){failed=true;return;}runtimeBindings.Add(binding.ExpectationKey,binding);
    }
    public void PriorActorOutcomeVerified(object scope,Guid transaction,RebirthPoiResetActorOutcome outcome)
    {
        if(!WitnessScope(scope,transaction))return;if(outcome==null||originalRecord.Observations==null){failed=true;return;}
        var matches=originalRecord.Observations.Volumes.Values.Where(v=>v.Actors.ContainsKey(outcome.Token)&&outcome.Matches(originalRecord.Observations,v,v.Actors[outcome.Token])).ToArray();
        if(matches.Length!=1){failed=true;return;}
        RebirthPoiResetActorOutcome prior;if(priorActors.TryGetValue(outcome.Token,out prior)){if(prior.Write().ToString()!=outcome.Write().ToString())failed=true;return;}if(priorActors.Count>=8192){failed=true;return;}priorActors.Add(outcome.Token,outcome);
    }
    public void AuthoredEffectReset(object scope,Guid transaction,RebirthPoiAuthoredRuntimeBinding binding)
    {
        if(!WitnessScope(scope,transaction)||!plan.IsAuthored)return;
        RuntimeBindingVerified(scope,transaction,binding);if(failed)return;
        var expected=plan.Authored.SingleOrDefault(e=>e.Key==binding.ExpectationKey);
        if(expected==null||expected.Descriptor!=binding.Descriptor||expected.OriginalNativeId.HasValue&&expected.OriginalNativeId.Value!=binding.NativeId){failed=true;return;}resetAuthored.Add(expected.Key);
    }
    public void TriggersRefreshed(object scope,Guid transaction,string manifest){if(WitnessScope(scope,transaction)&&manifest==plan.Manifest)triggersRefreshed=true;}
    // Native adapter must positively prove the original unchanged object/manifest/generation;
    // this domain receipt is not produced by elapsed time or by merely omitting a reset ID.
    public void UnaffectedVolumeVerified(object scope,Guid transaction,int id,string descriptor,Guid generation)
    {
        if(!WitnessScope(scope,transaction)||originalRecord.Observations==null)return;
        RebirthPoiVolumeObservation affected;if(originalRecord.Observations.Volumes.TryGetValue(id,out affected)&&(plan.IsAuthored?plan.Authored.Any(e=>e.Kind==RebirthPoiAuthoredResetKind.Sleeper&&e.Descriptor==affected.Descriptor):plan.Volumes.Contains(id)))return;
        RebirthPoiVolumeObservation volume;if(!originalRecord.Observations.Volumes.TryGetValue(id,out volume)||volume.Descriptor!=descriptor||originalRecord.Observations.EffectiveGeneration(volume)!=generation){failed=true;return;}
        unchangedVolumes.Add(id);
    }
    private bool WitnessScope(object scope,Guid transaction){return advanced&&!ended&&!failed&&ReferenceEquals(scope,original.Binding.Scope)&&transaction==plan.Transaction&&Scope();}
    public bool MoveNext()
    {
        current=null;Poll();
        if(State==RebirthPoiResetProtocolState.Completed)return false;
        if(State==RebirthPoiResetProtocolState.Refused)throw new InvalidOperationException("Original POI reset refused before native mutation; owning caller must report refusal.");
        if(State==RebirthPoiResetProtocolState.Unknown)return true;
        if(cancelled||State==RebirthPoiResetProtocolState.PendingAdmission||State==RebirthPoiResetProtocolState.PendingCompletion)return true;
        State=RebirthPoiResetProtocolState.Running;advanced=true;
        try
        {
            if(native.MoveNext()){current=WrapYield(native.Current);return true;}
            ended=true;State=RebirthPoiResetProtocolState.PendingCompletion;nextAttempt=0;Poll();return State!=RebirthPoiResetProtocolState.Completed;
        }
        catch(Exception error){NativeFailure=error;failed=true;State=RebirthPoiResetProtocolState.Unknown;return true;}
    }
    private object WrapYield(object yielded){var iterator=yielded as IEnumerator;return iterator==null?yielded:new Nested(this,iterator);}
    // Unity may drive a yielded nested coroutine without polling its parent. Every nested
    // advancement therefore carries the SAME original scope/operation guard recursively.
    private sealed class Nested:IEnumerator,IDisposable
    {
        private readonly RebirthPoiResetCallerProtocol owner;private readonly IEnumerator originalIterator;private object yielded;private bool disposed;
        public Nested(RebirthPoiResetCallerProtocol protocol,IEnumerator iterator){owner=protocol;originalIterator=iterator;}
        public object Current {get{return yielded;} }
        public bool MoveNext()
        {
            yielded=null;
            if(disposed||!owner.Scope()||owner.State!=RebirthPoiResetProtocolState.Running||owner.failed)
            {owner.State=RebirthPoiResetProtocolState.Unknown;return true;}
            try{if(!originalIterator.MoveNext())return false;yielded=owner.WrapYield(originalIterator.Current);return true;}
            catch(Exception error){owner.NativeFailure=error;owner.failed=true;owner.State=RebirthPoiResetProtocolState.Unknown;return true;}
        }
        public void Reset(){throw new NotSupportedException("Original nested reset cannot be rewound.");}
        public void Dispose(){if(disposed)return;disposed=true;var item=originalIterator as IDisposable;if(item!=null)item.Dispose();}
    }
    public void CancelBeforeMutation(){if(advanced){State=RebirthPoiResetProtocolState.Unknown;return;}cancelled=true;nextAttempt=0;Poll();}
    public void Dispose(){if(advanced&&State!=RebirthPoiResetProtocolState.Completed)State=RebirthPoiResetProtocolState.Unknown;else if(!advanced)CancelBeforeMutation();var disposable=native as IDisposable;if(disposable!=null)disposable.Dispose();}
    public void Reset(){throw new NotSupportedException("Original native reset iterator must never be recreated or rewound.");}
}




