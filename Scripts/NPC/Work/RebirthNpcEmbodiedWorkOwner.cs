using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// Single shared gate for lease and assignment work. No stop/release/rollback guesses or expiry takeover.
internal static class RebirthNpcEmbodiedWorkOwner
{
    internal const int MaximumOwners=1024;
    private sealed class Entry
    {
        internal readonly RebirthNpcEmbodiedWorkIntent Intent;
        internal bool Begun,Busy,Unresolved,Retired;
        internal Entry(RebirthNpcEmbodiedWorkIntent intent){Intent=intent;}
    }
    private static readonly object Sync=new object();
    private static readonly Dictionary<RebirthNpcStableId,Entry> Owners=new Dictionary<RebirthNpcStableId,Entry>();
    private static readonly ConditionalWeakTable<RebirthNpcEmbodiedWorkIntent,Entry> Tickets=new ConditionalWeakTable<RebirthNpcEmbodiedWorkIntent,Entry>();
    [ThreadStatic] private static RebirthNpcEmbodiedWorkIntent dispatch;
    [ThreadStatic] private static bool callbackSweep;
    private static int maintenanceCursor;
    internal static RebirthNpcEmbodiedWorkIntent CurrentDispatchIntent=>dispatch;
    internal static bool IsDispatching(RebirthNpcEmbodiedWorkIntent intent)
    {
        if(intent==null||!ReferenceEquals(dispatch,intent)||!intent.MatchesOriginal())return false;
        lock(Sync)return Owners.TryGetValue(intent.StableId,out var entry)&&ReferenceEquals(entry.Intent,intent)&&entry.Busy&&!entry.Unresolved&&!entry.Retired;
    }
    internal static bool TryStage(RebirthNpcEmbodiedWorkIntent intent,out string detail)
    {
        detail="Original work identity is unavailable.";
        if(intent==null||!intent.MatchesOriginal())return false;
        lock(Sync)
        {
            if(Owners.TryGetValue(intent.StableId,out var existing))
            {
                if(!existing.Unresolved&&!existing.Retired&&existing.Intent.SameKey(intent))
                {detail="Original staged callbacks retained; obtain original intent with TryGet.";return true;}
                detail="Original actor work ownership is retained; takeover refused.";return false;
            }
            if(Tickets.TryGetValue(intent,out _)){detail="Retired original intent cannot be replayed.";return false;}
            if(Owners.Count>=MaximumOwners){detail="Work owner capacity reached.";return false;}
            var entry=new Entry(intent);Tickets.Add(intent,entry);Owners.Add(intent.StableId,entry);
            detail="Original work staged; EAI activation remains unqualified.";return true;
        }
    }
    internal static bool TryGet(EntityRebirthNPC actor,string path,ulong primaryId,ulong secondaryId,uint revision,
        out RebirthNpcEmbodiedWorkIntent intent)
    {
        intent=null;var runtime=actor?.RebirthRuntimeState;if(runtime==null)return false;
        lock(Sync)
        {
            if(!Owners.TryGetValue(runtime.StableId,out var entry))return false;var candidate=entry.Intent;
            if(!ReferenceEquals(candidate.Actor,actor)||candidate.Path!=path||candidate.PrimaryId!=primaryId||
                candidate.SecondaryId!=secondaryId||candidate.Revision!=revision)return false;
            intent=candidate;return true; // Also returns unresolved exact originals for cancellation/diagnostics.
        }
    }
    internal static bool RequestCancel(RebirthNpcEmbodiedWorkIntent intent,out string detail)
    {
        detail="Original owner ticket missing; cancellation outcome unknown.";if(intent==null)return false;
        lock(Sync)
        {
            if(!Tickets.TryGetValue(intent,out var entry)||!ReferenceEquals(entry.Intent,intent))return false;
            if(entry.Retired){detail="Original never-dispatched intent already retired.";return true;}
            if(!Owners.TryGetValue(intent.StableId,out var current)||!ReferenceEquals(current,entry))return false;
            if(entry.Begun||entry.Busy||entry.Unresolved)
            {entry.Unresolved=true;detail="Original action cancellation/settlement unqualified; owner retained.";return false;}
            entry.Retired=true;Owners.Remove(intent.StableId);detail="Original never-dispatched intent retired without callbacks.";return true;
        }
    }
    internal static void Maintenance()
    {
        Entry[] entries;lock(Sync){entries=new Entry[Owners.Count];Owners.Values.CopyTo(entries,0);}
        if(entries.Length==0)return;
        int start;lock(Sync){start=maintenanceCursor%entries.Length;maintenanceCursor=(start+Math.Min(64,entries.Length))%entries.Length;}
        for(int i=0;i<Math.Min(64,entries.Length);i++)
        {
            var entry=entries[(start+i)%entries.Length];if(entry.Intent.MatchesOriginal())continue;
            lock(Sync)
            {
                if(!Owners.TryGetValue(entry.Intent.StableId,out var current)||!ReferenceEquals(current,entry))continue;
                if(entry.Begun||entry.Busy||entry.Unresolved){entry.Unresolved=true;continue;}
                entry.Retired=true;Owners.Remove(entry.Intent.StableId);
            }
        }
    }
    internal static bool TryDispatchFromEai(EAIRebirthWork task,EntityRebirthNPC actor,out string detail)
    {
        detail="Work EAI activation and typed native actuator proofs are missing.";
        if(!EAIRebirthWork.ActivationApproved||task==null||!task.IsOwnedUpdate||!ReferenceEquals(task.Actor,actor))return false;
        Entry entry;lock(Sync){if(actor?.RebirthRuntimeState==null||!Owners.TryGetValue(actor.RebirthRuntimeState.StableId,out entry))return false;}
        if(!entry.Intent.HasQualifiedActuator)return false;
        return DispatchOwnedCore(entry,out detail);
    }
    // Private callback sequencer; production reaches this only after the inactive native proof gates above.
    private static bool DispatchOwnedCore(Entry entry,out string detail)
    {
        detail="Original owned dispatch refused.";var intent=entry.Intent;
        if(callbackSweep||dispatch!=null||!intent.MatchesOriginal())return false;
        lock(Sync)
        {
            if(!Owners.TryGetValue(intent.StableId,out var current)||!ReferenceEquals(current,entry)||entry.Busy||entry.Unresolved||entry.Retired)return false;
            entry.Busy=true;
        }
        callbackSweep=true;
        try
        {
            // Producer predicate may reenter/cancel/replace world: no actuation authority while it runs.
            if(!intent.ProducerIsCurrent()||!StillOwned(entry)||!intent.MatchesOriginal())return Retain(entry,"Original predicate/identity changed.",out detail);
            dispatch=intent;
            bool begin;lock(Sync){if(!StillOwnedLocked(entry))return RetainLocked(entry,"Original entry changed.",out detail);begin=!entry.Begun;entry.Begun=true;}
            if(begin)
            {
                intent.BeginOwned();
                if(!StillOwned(entry)||!intent.MatchesOriginal())return Retain(entry,"Original Begin outcome changed/unknown.",out detail);
                dispatch=null;
                if(!intent.ProducerIsCurrent()||!StillOwned(entry)||!intent.MatchesOriginal())return Retain(entry,"Original identity changed after Begin.",out detail);
                dispatch=intent;
            }
            if(!StillOwned(entry)||!intent.MatchesOriginal())return Retain(entry,"Original pre-Tick guard failed.",out detail);
            intent.TickOwned();
            if(!StillOwned(entry)||!intent.MatchesOriginal())return Retain(entry,"Original Tick outcome changed/unknown.",out detail);
            detail="Owned callback invoked; no native outcome or cancellation settlement implied.";return true;
        }
        catch(Exception){return Retain(entry,"Original callback fault; outcome unknown.",out detail);}
        finally
        {
            if(ReferenceEquals(dispatch,intent))dispatch=null;
            callbackSweep=false;
            lock(Sync)entry.Busy=false;
        }
    }
    private static bool StillOwned(Entry entry){lock(Sync)return StillOwnedLocked(entry);}
    private static bool StillOwnedLocked(Entry entry)=>Owners.TryGetValue(entry.Intent.StableId,out var current)&&
        ReferenceEquals(current,entry)&&!entry.Unresolved&&!entry.Retired&&entry.Busy;
    private static bool Retain(Entry entry,string reason,out string detail){lock(Sync)return RetainLocked(entry,reason,out detail);}
    private static bool RetainLocked(Entry entry,string reason,out string detail){entry.Unresolved=true;detail=reason;return false;}
}


