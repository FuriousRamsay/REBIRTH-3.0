using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// Shared original execution for scripted and administrative whole-POI resets.
internal static class RebirthPoiOwnedWorldReset
{
    private static double Now => (double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;
    internal static IEnumerator Run(World world,List<PrefabInstance> targets,FastTags<TagGroup.Global> tags,int player,QuestClass questClass,RebirthPoiResetCaller caller,Func<bool> current,Action<bool> finish)
    {
        RebirthPoiNativeResetExecution execution=null;bool completed=false;double deadline=Now+90;
        try
        {
            if(world==null||world.IsRemote()||targets==null||targets.Count<1||targets.Count>64||!current())yield break;
            RebirthPoiWorldStore store=null;
            while(current()&&Now<deadline&&!RebirthPoiWorldLifecycle.Instance.TryGetStore(out store))yield return null;
            if(store==null||!current())yield break;
            foreach(var target in targets)
            {
                RebirthPoiNativeAuthoredManifest authored;
                if(!RebirthPoiNativeAuthoredManifest.TryCapture(store.Published.Binding,world,target,out authored))yield break;
                while(current()&&Now<deadline)
                {
                    var snapshot=store.Published;RebirthPoiClearanceRecord record;
                    if(snapshot.TryGet(authored.Identity,out record))break;
                    if(!store.HasPending)store.TryDiscover(snapshot,authored.Identity,!authored.Slots.Any(s=>s.Expected.Combat));
                    yield return null;
                }
                if(!current()||Now>=deadline)yield break;
            }
            Guid transaction=Guid.NewGuid();
            if(!RebirthPoiNativeResetExecution.TryCreateWorldReset(store,world,targets,tags,player,null,questClass,caller,transaction,current,()=>Now,out execution))yield break;
            while(current()&&Now<deadline&&!execution.TryAdmitBeforeMutation())
            {
                if(execution.Batch.State==RebirthPoiResetProtocolState.Unknown||execution.Batch.State==RebirthPoiResetProtocolState.Refused)yield break;
                yield return null;
            }
            while(current()&&Now<deadline)
            {
                if(execution.Batch.State==RebirthPoiResetProtocolState.Unknown||execution.Batch.State==RebirthPoiResetProtocolState.Refused)yield break;
                if(!execution.MoveNext())break;
                yield return execution.Current;
            }
            RebirthPoiResetBatchCompletion receipt;
            if(current()&&execution.Batch.TryGetCompleted(out receipt)&&ReferenceEquals(receipt.Scope,world)&&receipt.World==store.Published.Binding.WorldId&&receipt.Transaction==transaction)
            {completed=true;}
        }
        finally
        {
            try { finish(completed); }
            finally { if(execution!=null){execution.Dispose();ThreadManager.StartCoroutine(Drain(execution));} }
        }
    }
    private static IEnumerator Drain(RebirthPoiNativeResetExecution execution)
    {double deadline=Now+30;while(Now<deadline&&!execution.Batch.PollCancellation())yield return null;}
}
