using System;
using System.Diagnostics;

internal enum RebirthPoiLifecycleState { Off, AwaitingStartDone, WaitingForBinding, Ready, PendingRetry, Refused }
internal delegate RebirthPoiStoreResult RebirthPoiStoreOpen(RebirthPoiWorldBinding binding,out RebirthPoiWorldStore store);

// Store lifecycle only. No sleeper activation, inferred kill, POI reset, marker or theme policy.
internal sealed class RebirthPoiWorldLifecycle
{
    public static readonly RebirthPoiWorldLifecycle Instance=new RebirthPoiWorldLifecycle();
    private readonly Func<RebirthPoiWorldBinding> resolve;
    private readonly Func<double> clock;
    private readonly RebirthPoiStoreOpen open;
    private readonly Action<string> diagnostic;
    private RebirthPoiWorldBinding binding;
    private RebirthPoiWorldStore store;
    private long generation;
    private double nextTick,nextAttempt,readinessDeadline;
    private int failures;
    public RebirthPoiLifecycleState State { get; private set; }
    public RebirthPoiStoreResult LastResult { get; private set; }
    public string LastDiagnostic { get; private set; }
    public long Generation { get { return generation; } }
    internal RebirthPoiWorldLifecycle(Func<RebirthPoiWorldBinding> resolver=null,Func<double> monotonicClock=null,
        RebirthPoiStoreOpen opener=null,Action<string> report=null)
    {
        resolve=resolver??NativeReadyBinding;
        clock=monotonicClock??(()=>((double)Stopwatch.GetTimestamp()/Stopwatch.Frequency));
        open=opener??((RebirthPoiWorldBinding b,out RebirthPoiWorldStore s)=>RebirthPoiWorldStore.TryOpen(b,out s));
        diagnostic=report??(message=>Log.Warning("[REBIRTH Purge] "+message));
        State=RebirthPoiLifecycleState.Off; LastDiagnostic=string.Empty;
    }
    private static RebirthPoiWorldBinding NativeReadyBinding()
    {
        var game=GameManager.Instance;
        if(!ThreadManager.IsMainThread() || game==null || game.IsStartingGame || game.IsEditMode() ||
            game.gameStateManager==null || !game.gameStateManager.IsGameStarted()) return null;
        RebirthPoiWorldBinding binding;
        return RebirthPoiWorldBinding.TryCapture(game.World,out binding)?binding:null;
    }
    public void Begin(bool asServer)
    {
        // A previous session's store must never be retargeted to another native world.
        generation++; binding=null; store=null; failures=0; nextTick=0; nextAttempt=0;
        LastDiagnostic=string.Empty; LastResult=RebirthPoiStoreResult.StaleScope;
        State=asServer?RebirthPoiLifecycleState.AwaitingStartDone:RebirthPoiLifecycleState.Off;
    }
    public void StartDone()
    {
        if(State!=RebirthPoiLifecycleState.AwaitingStartDone) return;
        double now=clock(); readinessDeadline=now+120; nextAttempt=now; nextTick=now;
        State=RebirthPoiLifecycleState.WaitingForBinding;
        // Native GameStartDone fires before IsStartingGame becomes false. Tick will wait.
    }
    public bool TryGetStore(out RebirthPoiWorldStore current)
    {
        current=null;
        if(State!=RebirthPoiLifecycleState.Ready || binding==null || !binding.IsCurrent || store==null || store.HasPending || store.Published==null) return false;
        current=store; return true;
    }
    // Original passive evidence may arrive while one exact publication is pending.
    // This accessor does not authorize ordinary mutation or clear producers.
    internal bool TryGetEvidenceStore(out RebirthPoiWorldStore current)
    {
        current=null;
        if((State!=RebirthPoiLifecycleState.Ready && State!=RebirthPoiLifecycleState.PendingRetry) || binding==null || !binding.IsCurrent || store==null || store.Published==null) return false;
        current=store; return true;
    }
    public void Tick()
    { if(!RebirthPurgeReleasePolicy.Enabled) return; 
        if(State==RebirthPoiLifecycleState.Off || State==RebirthPoiLifecycleState.AwaitingStartDone || State==RebirthPoiLifecycleState.Refused) return;
        double now=clock(); if(now<nextTick) return; nextTick=now+0.25;
        long originalGeneration=generation;
        try
        {
            if(binding!=null && !binding.IsCurrent) { Refuse(RebirthPoiStoreResult.StaleScope,"Original world binding changed; waiting for a new GameStarting session."); return; }
            if(State==RebirthPoiLifecycleState.Ready)
            {
                if(store==null || !store.HasPending) return;
                State=RebirthPoiLifecycleState.PendingRetry; nextAttempt=now+2; failures=0;
            }
            if(now<nextAttempt) return;
            if(State==RebirthPoiLifecycleState.WaitingForBinding)
            {
                if(binding==null)
                {
                    var candidate=resolve();
                    if(generation!=originalGeneration) return;
                    if(candidate==null || !candidate.IsCurrent)
                    {
                        if(now>=readinessDeadline) Refuse(RebirthPoiStoreResult.StaleScope,"Server world/save identity did not become ready within120seconds; no empty ledger substituted.");
                        else nextAttempt=now+1;
                        return;
                    }
                    binding=candidate;
                }
                RebirthPoiWorldStore candidateStore;
                var result=open(binding,out candidateStore);
                if(generation!=originalGeneration || binding==null || !binding.IsCurrent) return;
                LastResult=result;
                if(result==RebirthPoiStoreResult.Published && candidateStore!=null && candidateStore.Published!=null)
                { store=candidateStore; State=RebirthPoiLifecycleState.Ready; failures=0; return; }
                if(result==RebirthPoiStoreResult.Uncertain && candidateStore!=null && candidateStore.HasPending)
                { store=candidateStore; State=RebirthPoiLifecycleState.PendingRetry; Schedule(now); return; }
                if(result==RebirthPoiStoreResult.IoFailure) { Schedule(now); Report("World ledger I/O unavailable; retry is time-gated and original files retained."); return; }
                Refuse(result,"World ledger load refused ("+result+"); original files retained."); return;
            }
            if(State==RebirthPoiLifecycleState.PendingRetry)
            {
                var originalStore=store;
                if(originalStore==null || !originalStore.HasPending) { Refuse(RebirthPoiStoreResult.Conflict,"Pending world publication is unavailable; no replacement transaction created."); return; }
                var result=originalStore.TryRetryPending();
                if(generation!=originalGeneration || !ReferenceEquals(store,originalStore) || binding==null || !binding.IsCurrent) return;
                LastResult=result;
                if(result==RebirthPoiStoreResult.Published && !originalStore.HasPending && originalStore.Published!=null)
                { State=RebirthPoiLifecycleState.Ready; failures=0; return; }
                if(result==RebirthPoiStoreResult.Uncertain || result==RebirthPoiStoreResult.IoFailure) { Schedule(now); return; }
                Refuse(result,"Retained world publication refused ("+result+"); candidate preserved.");
            }
        }
        catch(Exception ex)
        {
            if(generation!=originalGeneration) return;
            LastResult=RebirthPoiStoreResult.IoFailure; Schedule(now);
            Report("World ledger I/O will retry: "+ex.GetType().Name);
        }
    }
    private void Schedule(double now)
    { failures=Math.Min(failures+1,6); nextAttempt=now+Math.Min(60,2*Math.Pow(2,failures-1)); }
    private void Refuse(RebirthPoiStoreResult result,string message)
    { LastResult=result; State=RebirthPoiLifecycleState.Refused; Report(message); }
    private void Report(string message)
    { if(LastDiagnostic==message) return; LastDiagnostic=message; try { diagnostic(message); } catch { } }
    public void Stop()
    {
        // Stop admission first. One pending retry is allowed only within the original scope.
        var original=store; var originalBinding=binding;
        generation++; State=RebirthPoiLifecycleState.Off; binding=null; store=null;
        if(original!=null && original.HasPending && originalBinding!=null && originalBinding.IsCurrent)
        {
            try { LastResult=original.TryRetryPending(); }
            catch { LastResult=RebirthPoiStoreResult.Uncertain; }
            // Persisted candidate is never deleted even when this last attempt fails.
        }
    }
}