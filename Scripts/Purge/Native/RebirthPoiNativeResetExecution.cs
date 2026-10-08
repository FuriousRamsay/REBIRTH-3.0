using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Executes the actual native World.ResetPOIS iterator under one original batch owner.
// Native hooks may observe this context; no global reset caller is intercepted here.
internal sealed class RebirthPoiNativeResetExecution:IEnumerator,IDisposable
{
    [ThreadStatic] private static RebirthPoiNativeResetExecution active;
    internal static RebirthPoiNativeResetExecution Active {get{return active;} }
    internal readonly RebirthPoiNativeResetEnvelope Envelope;
    internal readonly RebirthPoiResetBatchCallerProtocol Batch;
    private readonly Func<bool> ownerCurrent;
    private readonly Dictionary<RebirthPoiNativeResetEnvelope.Target,HashSet<int>> auxiliary=new Dictionary<RebirthPoiNativeResetEnvelope.Target,HashSet<int>>();
    private readonly Dictionary<RebirthPoiNativeResetEnvelope.Target,Dictionary<string,RebirthPoiAuthoredRuntimeBinding>> copyReceipts=new Dictionary<RebirthPoiNativeResetEnvelope.Target,Dictionary<string,RebirthPoiAuthoredRuntimeBinding>>();
    private object current;
    public object Current {get{return current;} }
    private RebirthPoiNativeResetExecution(RebirthPoiNativeResetEnvelope envelope,RebirthPoiResetBatchCallerProtocol batch,Func<bool> originalOwner)
    {Envelope=envelope;Batch=batch;ownerCurrent=originalOwner;foreach(var target in envelope.Targets)auxiliary.Add(target,new HashSet<int>());}
    public static bool TryCreateWorldReset(RebirthPoiWorldStore store,World world,List<PrefabInstance> prefabs,FastTags<TagGroup.Global> questTags,int playerId,int[] sharedWith,QuestClass questClass,RebirthPoiResetCaller caller,Guid transaction,Func<bool> originalOwner,Func<double> clock,out RebirthPoiNativeResetExecution execution)
    {
        execution=null;
        try
        {
            if(store==null||originalOwner==null||clock==null||!originalOwner()||!RebirthPoiNativeResetWitnesses.IsReady)return false;
            var snapshot=store.Published;RebirthPoiNativeResetEnvelope envelope;
            if(!RebirthPoiNativeResetEnvelope.TryCaptureAuthored(snapshot,world,prefabs,transaction,caller,out envelope))return false;
            // Alive old partial participants require the explicit survival/removal producer.
            // Do not silently discard their causality while that producer is being connected.
            foreach(var target in envelope.Targets)
            {RebirthPoiClearanceRecord record;if(!snapshot.TryGet(target.Entry.Identity,out record)||record.Observations!=null&&record.Observations.Volumes.Values.Any(v=>v.Actors.Values.Any(a=>!a.Dead)))return false;}
            // Native iterator factory is effect-free; this exact iterator is advanced once.
            IEnumerator native=world.ResetPOIS(prefabs,questTags,playerId,sharedWith,questClass);
            var batch=new RebirthPoiResetBatchCallerProtocol(store,snapshot,envelope.Targets.Select(t=>t.Entry),native,clock);
            execution=new RebirthPoiNativeResetExecution(envelope,batch,originalOwner);return execution.IsOriginalCurrent;
        }
        catch{execution=null;return false;}
    }
    internal bool IsOriginalCurrent {get{try{return Envelope.IsOriginalCurrent&&ownerCurrent();}catch{return false;}} }
    internal RebirthPoiNativeResetEnvelope.Target TargetFor(PrefabInstance prefab)
    {return IsOriginalCurrent?Envelope.Targets.FirstOrDefault(t=>ReferenceEquals(t.Prefab,prefab)):null;}
    internal RebirthPoiNativeCopyReceiptScope BeginCopyReceipts(RebirthPoiNativeResetEnvelope.Target target,object owner)
    {
        if(!IsOriginalCurrent||target==null||target.Authored==null||!Envelope.Targets.Contains(target))return null;
        Dictionary<string,RebirthPoiAuthoredRuntimeBinding> receipts;
        if(!copyReceipts.TryGetValue(target,out receipts)){receipts=new Dictionary<string,RebirthPoiAuthoredRuntimeBinding>(StringComparer.Ordinal);copyReceipts.Add(target,receipts);}
        var scope=new RebirthPoiNativeCopyReceiptScope(target.Authored,Envelope.Transaction,owner,()=>Envelope.IsBaseCurrent&&ownerCurrent(),receipts);target.PendingCopy=scope;return scope;
    }
    internal bool CompleteCopyReceipts(RebirthPoiNativeResetEnvelope.Target target,RebirthPoiNativeCopyReceiptScope scope)
    {
        IReadOnlyList<RebirthPoiAuthoredRuntimeBinding> receipts;
        if(scope==null||!IsOriginalCurrent||!scope.CopyCompleted(scope.CopyOwner,true,out receipts)||!target.CommitCopy(scope,receipts))return false;
        if(target.Entry.Plan.IsAuthored)foreach(var receipt in receipts)Batch.RuntimeBindingVerified(target.Entry.Identity,Envelope.World,Envelope.Transaction,receipt);
        return true;
    }
    private RebirthPoiAuthoredRuntimeBinding ResetBinding(RebirthPoiNativeResetEnvelope.Target target,RebirthPoiAuthoredResetKind kind,object runtime)
    {
        var slots=target.Authored.Slots.Where(s=>s.Expected.Kind==kind).Where(s=>{int id;object actual;return s.TryReadRuntime(out id,out actual)&&ReferenceEquals(actual,runtime)&&target.Accepts(s,id,actual);}).ToArray();
        if(slots.Length!=1)return null;var slot=slots[0];int nativeId;object value;if(!slot.TryReadRuntime(out nativeId,out value))return null;
        Dictionary<string,RebirthPoiAuthoredRuntimeBinding> receipts;
        if(!copyReceipts.TryGetValue(target,out receipts)){receipts=new Dictionary<string,RebirthPoiAuthoredRuntimeBinding>(StringComparer.Ordinal);copyReceipts.Add(target,receipts);}
        RebirthPoiAuthoredRuntimeBinding receipt;
        if(!receipts.TryGetValue(slot.Expected.Key,out receipt))
        {
            // Outside the copied intersection an existing original runtime can
            // still be positively reset by the original whole World.ResetPOIS.
            if(!slot.Expected.OriginalNativeId.HasValue)return null;
            receipt=new RebirthPoiAuthoredRuntimeBinding(slot.Expected.Key,slot.Expected.Descriptor,nativeId,Guid.NewGuid());receipts.Add(slot.Expected.Key,receipt);
        }
        return receipt.NativeId==nativeId&&receipt.Descriptor==slot.Expected.Descriptor?receipt:null;
    }
    private bool Advance(IEnumerator iterator,out object yielded)
    {
        yielded=null;
        if(!IsOriginalCurrent)
        {if(Batch.State==RebirthPoiResetProtocolState.PendingAdmission||Batch.State==RebirthPoiResetProtocolState.Ready)Batch.CancelBeforeMutation();else Batch.AbortUncertain();return true;}
        var previous=active;if(previous!=null&&!ReferenceEquals(previous,this)){Batch.AbortUncertain(new InvalidOperationException("Overlapping original native reset execution context."));return true;}
        active=this;
        try{bool more=iterator.MoveNext();if(more)yielded=Wrap(iterator.Current);return more;}
        catch(InvalidOperationException){if(Batch.State==RebirthPoiResetProtocolState.Refused)throw;Batch.AbortUncertain();return true;}
        catch(Exception error){Batch.AbortUncertain(error);return true;}
        finally{active=previous;}
    }
    private object Wrap(object value){var iterator=value as IEnumerator;return iterator==null?value:new Nested(this,iterator);}
    public bool MoveNext(){return Advance(Batch,out current);}
    private sealed class Nested:IEnumerator
    {private readonly RebirthPoiNativeResetExecution owner;private readonly IEnumerator iterator;
    private object current;public Nested(RebirthPoiNativeResetExecution execution,IEnumerator original){owner=execution;iterator=original;}public object Current {get{return current;} }public bool MoveNext(){return owner.Advance(iterator,out current);}public void Reset(){throw new NotSupportedException();}}
    internal void ChunkResult(RebirthPoiNativeResetEnvelope.Target target,long key,bool copy,bool verified)
    {if(!verified){Batch.AbortUncertain();return;}if(copy)Batch.ChunkCopied(target.Entry.Identity,Envelope.World,Envelope.Transaction,key,true);else Batch.ChunkRegenerated(target.Entry.Identity,Envelope.World,Envelope.Transaction,key,true);}
    internal void VolumeResult(SleeperVolume volume,World world,bool verified)
    {
        var target=TargetFor(volume==null?null:volume.prefabInstance);if(target==null||!ReferenceEquals(world,Envelope.World)||!verified){Batch.AbortUncertain();return;}
        if(target.Entry.Plan.IsAuthored){var receipt=ResetBinding(target,RebirthPoiAuthoredResetKind.Sleeper,volume);if(receipt==null)Batch.AbortUncertain();else Batch.AuthoredEffectReset(target.Entry.Identity,world,Envelope.Transaction,receipt);return;}
        var ids=target.Sleepers.Where(p=>ReferenceEquals(p.Value,volume)).Select(p=>p.Key).ToArray();if(ids.Length!=1){Batch.AbortUncertain();return;}int id=ids[0];
        if(target.AuxiliarySleepers.Contains(id))auxiliary[target].Add(id);else Batch.VolumeReset(target.Entry.Identity,world,Envelope.Transaction,id);
    }
    internal void TriggerResult(TriggerVolume volume,bool verified)
    {
        var target=TargetFor(volume==null?null:volume.prefabInstance);if(target==null||!verified){Batch.AbortUncertain();return;}
        if(target.Entry.Plan.IsAuthored){var receipt=ResetBinding(target,RebirthPoiAuthoredResetKind.Trigger,volume);if(receipt==null)Batch.AbortUncertain();else Batch.AuthoredEffectReset(target.Entry.Identity,Envelope.World,Envelope.Transaction,receipt);return;}
        var ids=target.Triggers.Where(p=>ReferenceEquals(p.Value,volume)).Select(p=>p.Key).ToArray();if(ids.Length!=1){Batch.AbortUncertain();return;}Batch.TriggerReset(target.Entry.Identity,Envelope.World,Envelope.Transaction,ids[0]);
    }
    internal void RefreshResult(PrefabInstance prefab,bool verified)
    {
        var target=TargetFor(prefab);if(target==null||!verified||!target.Entry.Plan.IsAuthored&&!target.AuxiliarySleepers.All(id=>auxiliary[target].Contains(id))){Batch.AbortUncertain();return;}
        // Every combat/native room is reset by a whole World.ResetPOIS. Original
        // observations may contain other rooms only when a separately qualified caller says so.
        Batch.TriggersRefreshed(target.Entry.Identity,Envelope.World,Envelope.Transaction,target.Entry.Plan.Manifest);
    }
    public void Dispose(){Batch.Dispose();}
    public void Reset(){throw new NotSupportedException("Original native reset cannot be recreated.");}
}
