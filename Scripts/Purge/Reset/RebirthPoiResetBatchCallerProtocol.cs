using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal sealed class RebirthPoiResetBatchEntry
{
    public readonly RebirthPoiIdentity Identity;
    public readonly RebirthPoiResetPlan Plan;
    public RebirthPoiResetBatchEntry(RebirthPoiIdentity identity,RebirthPoiResetPlan plan)
    {Identity=identity??throw new ArgumentNullException("identity");Plan=plan??throw new ArgumentNullException("plan");}
}
// One original native iterator can reset several overlapping prefabs. Every POI must
// be admitted before that iterator advances, and every terminal receipt must be durable.
internal sealed class RebirthPoiResetBatchCallerProtocol:IEnumerator,IDisposable
{
    private readonly RebirthPoiWorldStore store;
    private readonly RebirthPoiWorldSnapshot predecessor;
    private readonly IEnumerator native;
    private readonly Dictionary<string,RebirthPoiResetCallerProtocol> children=new Dictionary<string,RebirthPoiResetCallerProtocol>(StringComparer.Ordinal);
    private readonly Dictionary<string,RebirthPoiResetBatchEntry> entries;
    private readonly Dictionary<string,long> epochs=new Dictionary<string,long>(StringComparer.Ordinal);
    private readonly HashSet<RebirthPoiResetCallerProtocol> cancellationSent=new HashSet<RebirthPoiResetCallerProtocol>();
    private bool advanced,ended,cancelled,failed;
    private object current;
    private RebirthPoiResetBatchCompletion completion;
    public RebirthPoiResetProtocolState State {get;private set;}
    public Exception NativeFailure {get;private set;}
    public object Current {get{return current;} }
    private sealed class ChildIterator:IEnumerator
    {private readonly RebirthPoiResetBatchCallerProtocol owner;public ChildIterator(RebirthPoiResetBatchCallerProtocol batch){owner=batch;}public object Current {get{return null;} }public bool MoveNext(){return !owner.ended;}public void Reset(){throw new NotSupportedException();}}
    public RebirthPoiResetBatchCallerProtocol(RebirthPoiWorldStore originalStore,RebirthPoiWorldSnapshot snapshot,IEnumerable<RebirthPoiResetBatchEntry> affected,IEnumerator originalNative,Func<double> clock)
    {
        if(originalStore==null||snapshot==null||affected==null||originalNative==null||clock==null||!ReferenceEquals(originalStore.Published,snapshot))throw new ArgumentException("Invalid original reset batch.");
        var list=affected.ToArray();if(list.Length==0||list.Length>RebirthPoiClearanceLedger.MaximumRecords||list.Any(e=>e==null)||list.Select(e=>e.Identity.Key).Distinct(StringComparer.Ordinal).Count()!=list.Length||list.Select(e=>e.Plan.Transaction).Distinct().Count()!=1)throw new ArgumentException("Invalid complete original reset list.");
        store=originalStore;predecessor=snapshot;native=originalNative;entries=list.ToDictionary(e=>e.Identity.Key,StringComparer.Ordinal);
        foreach(var entry in list){RebirthPoiClearanceRecord record;if(!snapshot.TryGet(entry.Identity,out record))throw new ArgumentException("Original batch POI missing.");epochs.Add(entry.Identity.Key,record.Epoch);children.Add(entry.Identity.Key,new RebirthPoiResetCallerProtocol(store,snapshot,entry.Identity,entry.Plan,new ChildIterator(this),clock));}
        State=RebirthPoiResetProtocolState.PendingAdmission;
    }
    private bool CurrentScope(){return predecessor.Binding.IsCurrent&&store.Published!=null&&ReferenceEquals(predecessor.Binding,store.Published.Binding);}
    private bool OriginalIntents()
    {
        if(!CurrentScope())return false;
        foreach(var entry in entries.Values){RebirthPoiClearanceRecord record;if(!store.Published.TryGet(entry.Identity,out record)||record.Epoch!=epochs[entry.Identity.Key]||record.State!=RebirthPoiClearanceState.ResetPending||record.ResetId!=entry.Plan.Transaction||record.ResetPlan==null||record.ResetPlan.Canonical!=entry.Plan.Canonical)return false;}return true;
    }
    private void Unknown(Exception error=null){failed=true;NativeFailure=error;State=RebirthPoiResetProtocolState.Unknown;}
    private void PollChildren()
    {
        foreach(var child in children.Values)child.Poll();
        if(children.Values.Any(c=>c.State==RebirthPoiResetProtocolState.Unknown)){Unknown();return;}
        if(!advanced&&(cancelled||children.Values.Any(c=>c.State==RebirthPoiResetProtocolState.Refused)))
        {
            cancelled=true;foreach(var child in children.Values)if(cancellationSent.Add(child))child.CancelBeforeMutation();
            State=children.Values.All(c=>c.State==RebirthPoiResetProtocolState.Refused)?RebirthPoiResetProtocolState.Refused:RebirthPoiResetProtocolState.PendingAdmission;return;
        }
        if(!advanced){State=children.Values.All(c=>c.State==RebirthPoiResetProtocolState.Ready)?RebirthPoiResetProtocolState.Ready:RebirthPoiResetProtocolState.PendingAdmission;return;}
        if(ended){State=children.Values.All(c=>c.State==RebirthPoiResetProtocolState.Completed)?RebirthPoiResetProtocolState.Completed:RebirthPoiResetProtocolState.PendingCompletion;}
    }
    public bool MoveNext()
    {
        current=null;if(failed||State==RebirthPoiResetProtocolState.Unknown)return true;if(State==RebirthPoiResetProtocolState.Completed)return false;
        PollChildren();if(State==RebirthPoiResetProtocolState.Refused)throw new InvalidOperationException("Whole original POI reset refused before native mutation; owning caller must report refusal.");
        if(State==RebirthPoiResetProtocolState.Unknown||cancelled||State==RebirthPoiResetProtocolState.PendingAdmission)return true;
        if(ended){foreach(var child in children.Values)if(child.State==RebirthPoiResetProtocolState.Running)child.MoveNext();PollChildren();return State!=RebirthPoiResetProtocolState.Completed;}
        if(!OriginalIntents()){if(advanced)Unknown();else{cancelled=true;PollChildren();}return true;}
        if(!advanced){foreach(var child in children.Values)child.MoveNext();if(children.Values.Any(c=>c.State!=RebirthPoiResetProtocolState.Running)){Unknown();return true;}advanced=true;}
        State=RebirthPoiResetProtocolState.Running;
        try{if(native.MoveNext()){current=Wrap(native.Current);return true;}ended=true;foreach(var child in children.Values)child.MoveNext();PollChildren();return State!=RebirthPoiResetProtocolState.Completed;}
        catch(Exception error){Unknown(error);return true;}
    }
    private object Wrap(object value){var iterator=value as IEnumerator;return iterator==null?value:new Nested(this,iterator);}
    private sealed class Nested:IEnumerator
    {
        private readonly RebirthPoiResetBatchCallerProtocol owner;private readonly IEnumerator native;private object current;
        public Nested(RebirthPoiResetBatchCallerProtocol batch,IEnumerator original){owner=batch;native=original;}public object Current {get{return current;} }
        public bool MoveNext(){current=null;owner.PollChildren();if(owner.failed||owner.ended||!owner.OriginalIntents()){owner.Unknown();return true;}try{if(!native.MoveNext())return false;current=owner.Wrap(native.Current);return true;}catch(Exception error){owner.Unknown(error);return true;}}
        public void Reset(){throw new NotSupportedException();}
    }
    private RebirthPoiResetCallerProtocol For(RebirthPoiIdentity identity,object scope,Guid tx)
    {if(!ReferenceEquals(scope,predecessor.Binding.Scope)||tx!=entries.Values.First().Plan.Transaction)return null;RebirthPoiResetCallerProtocol child;if(!advanced||ended||failed)return null;if(identity==null||!children.TryGetValue(identity.Key,out child)){Unknown();return null;}return child;}
    public void ChunkCopied(RebirthPoiIdentity identity,object scope,Guid tx,long key,bool success){var child=For(identity,scope,tx);if(child!=null)child.ChunkCopied(scope,tx,key,success);}
    public void ChunkRegenerated(RebirthPoiIdentity identity,object scope,Guid tx,long key,bool success){var child=For(identity,scope,tx);if(child!=null)child.ChunkRegenerated(scope,tx,key,success);}
    public void VolumeReset(RebirthPoiIdentity identity,object scope,Guid tx,int id){var child=For(identity,scope,tx);if(child!=null)child.VolumeReset(scope,tx,id);}
    public void TriggerReset(RebirthPoiIdentity identity,object scope,Guid tx,int id){var child=For(identity,scope,tx);if(child!=null)child.TriggerReset(scope,tx,id);}
    public void RuntimeBindingVerified(RebirthPoiIdentity identity,object scope,Guid transaction,RebirthPoiAuthoredRuntimeBinding binding){var child=For(identity,scope,transaction);if(child!=null)child.RuntimeBindingVerified(scope,transaction,binding);}
    public void PriorActorOutcomeVerified(RebirthPoiIdentity identity,object scope,Guid transaction,RebirthPoiResetActorOutcome outcome){var child=For(identity,scope,transaction);if(child!=null)child.PriorActorOutcomeVerified(scope,transaction,outcome);}
    public void AuthoredEffectReset(RebirthPoiIdentity identity,object scope,Guid transaction,RebirthPoiAuthoredRuntimeBinding binding){var child=For(identity,scope,transaction);if(child!=null)child.AuthoredEffectReset(scope,transaction,binding);}
    public void TriggersRefreshed(RebirthPoiIdentity identity,object scope,Guid tx,string manifest){var child=For(identity,scope,tx);if(child!=null)child.TriggersRefreshed(scope,tx,manifest);}
    public void UnaffectedVolumeVerified(RebirthPoiIdentity identity,object scope,Guid tx,int id,string descriptor,Guid generation){var child=For(identity,scope,tx);if(child!=null)child.UnaffectedVolumeVerified(scope,tx,id,descriptor,generation);}
    // Only the original owner may expose an outcome after every native effect and
    // exact final-file publication has completed. Later POI generations cannot replay it.
    public bool TryGetCompleted(out RebirthPoiResetBatchCompletion outcome)
    {
        outcome=null;
        if(State!=RebirthPoiResetProtocolState.Completed||!CurrentScope())return false;
        var published=store.Published;
        foreach(var entry in entries.Values)
        {
            RebirthPoiClearanceRecord record;
            if(!published.TryGet(entry.Identity,out record)||record.Epoch!=epochs[entry.Identity.Key]+1||
                record.LastResetId!=entry.Plan.Transaction||record.LastResetDisposition!=RebirthPoiResetDisposition.Completed||
                record.State==RebirthPoiClearanceState.ResetPending)return false;
        }
        if(completion==null)completion=new RebirthPoiResetBatchCompletion(predecessor.Binding.Scope,published.WorldId,
            entries.Values.First().Plan.Transaction,published.Revision,entries.Values);
        outcome=completion;return true;
    }
    internal void AbortUncertain(Exception error=null){Unknown(error);}
    public void CancelBeforeMutation(){if(advanced){Unknown();return;}cancelled=true;PollChildren();}
    public void Dispose(){if(advanced&&State!=RebirthPoiResetProtocolState.Completed)Unknown();else if(!advanced)CancelBeforeMutation();var item=native as IDisposable;if(item!=null)item.Dispose();}
    public void Reset(){throw new NotSupportedException("Original reset batch cannot be recreated.");}
}
