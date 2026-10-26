using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

#nullable disable

public enum RebirthNpcWorkOutcomeState : byte { Completed=0, Failed=1, Cancelled=2, Suspended=3, Superseded=4 }
public enum RebirthNpcProgressionEventKind : byte
{
    WorkCompleted=0, WorkFailed=1, ResourceProduced=2, ResourceDelivered=3,
    RepairCompleted=4, CraftingCompleted=5, FarmingCompleted=6,
    CapabilityUsed=7, SettlementContribution=8
}

public sealed class RebirthNpcWorkOutcomeRecord
{
    public Guid OutcomeId; public ulong AssignmentId; public RebirthNpcStableId NpcId;
    // True only for a physically witnessed, checkpointed operation. Old rows
    // remain readable but are never retroactively delivered as new practice.
    public bool DurableCompletion;
    public string DurableWitness;
    public string DefinitionId; public string TargetId; public long StartedUtcTicks; public long CompletedUtcTicks;
    public RebirthNpcWorkOutcomeState State; public RebirthNpcWorkFailureCategory FailureCategory;
    public int RetryCount; public string CancellationActor; public string SettlementId; public string Detail;
    public Dictionary<string,int> Produced; public Dictionary<string,int> Consumed;
}

public sealed class RebirthNpcProgressionEvent
{
    public Guid EventId; public Guid OutcomeId; public RebirthNpcProgressionEventKind Kind;
    public RebirthNpcStableId NpcId; public ulong AssignmentId; public string DefinitionId;
    public string ResourceKey; public int Quantity; public string SettlementId; public long CreatedUtcTicks;
}

public interface IRebirthNpcWorkProgressionEventSink
{
    int Priority { get; }
    void Handle(RebirthNpcProgressionEvent progressionEvent);
}

public static partial class RebirthNpcWorkOutcomeService
{
    private const int MaxOutcomes = 512, MaxEvents = 1024, MaxReplay = 2048;
    private static readonly object Sync = new object();
    private static Queue<RebirthNpcWorkOutcomeRecord> Outcomes = new Queue<RebirthNpcWorkOutcomeRecord>();
    private static Queue<RebirthNpcProgressionEvent> Events = new Queue<RebirthNpcProgressionEvent>();
    private sealed class PendingDelivery{public RebirthNpcProgressionEvent Event;public readonly HashSet<string> Delivered=new HashSet<string>(StringComparer.Ordinal);}
    private static HashSet<Guid> Emitted = new HashSet<Guid>();
    private static Queue<Guid> EmittedOrder = new Queue<Guid>();
    private static readonly HashSet<Guid> Checkpointed = new HashSet<Guid>();
    private static readonly Dictionary<Guid,PendingDelivery> PendingDeliveries=new Dictionary<Guid,PendingDelivery>();
    private static readonly List<IRebirthNpcWorkProgressionEventSink> Sinks = new List<IRebirthNpcWorkProgressionEventSink>();
    private static long recorded, emitted, duplicates, sinkFaults;
    private static int persistenceVersion;
    private static int replayCursor;
    private static object recoveryEpoch=new object();
    internal static bool CanRetireEmptyBacking{get{lock(Sync)return Outcomes.Count==0&&Events.Count==0&&Emitted.Count==0&&EmittedOrder.Count==0&&PendingDeliveries.Count==0;}}

    public static int PersistenceVersion { get { return System.Threading.Volatile.Read(ref persistenceVersion); } }

    public static void RegisterSink(IRebirthNpcWorkProgressionEventSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        lock (Sync) { if (!Sinks.Contains(sink)) Sinks.Add(sink); Sinks.Sort(delegate(IRebirthNpcWorkProgressionEventSink a,IRebirthNpcWorkProgressionEventSink b){return b.Priority.CompareTo(a.Priority);}); }
    }

    public static void Record(RebirthNpcWorkOutcomeRecord outcome)
    {
        TryRecordCommitted(outcome);
    }
    // Read-only pre-admission check. Numeric assignment IDs are process-local;
    // an earlier saved outcome must not be discovered only after native debit.
    internal static bool CanAdmitExactOutcomeIdentity(RebirthNpcWorkAssignment assignment)
    {
        if(assignment==null||assignment.AssignmentId==0||assignment.NpcId.IsEmpty||assignment.CreatedUtcTicks<=0)return false;
        try
        {
            RebirthNpcWorkOutcomePersistenceStore.EnsureLoaded();
            if(!RebirthNpcWorkOutcomePersistenceStore.IsReadyForNativeAdmission)return false;
            Guid operation=RebirthNpcConcreteWorkExecutorBase.DeriveOperationId(assignment.AssignmentId);
            // A brand-new native original cannot use even an identical prior
            // completion as permission for another effect. Retained-original
            // recovery bypasses admission and reads its own saved proof instead.
            lock(Sync)foreach(var prior in Outcomes)if(prior.OutcomeId==operation)return false;
            return true;
        }
        catch{return false;}
    }

    // The caller must supply an operation-specific stable GUID after observing
    // its own saved native completion witness. Publication precedes delivery.
    public static bool TryRecordCommitted(RebirthNpcWorkOutcomeRecord outcome)
    {
        if((!RebirthNpcWorkReleaseGate.Enabled&&!RebirthNpcQualifiedWorkGate.AllowsOutcome(outcome)&&
            !(outcome?.DefinitionId==RebirthNpcFarmingWorkPackageService.HarvestDefinitionId&&RebirthNpcFarmOriginal.AllowsOutcome(outcome)))||
           outcome==null||outcome.OutcomeId==Guid.Empty||
           outcome.AssignmentId==0||outcome.NpcId.IsEmpty||
           outcome.State!=RebirthNpcWorkOutcomeState.Completed||
           string.IsNullOrWhiteSpace(outcome.DurableWitness)||
           BlocksRetainedEvent(outcome.OutcomeId,outcome.AssignmentId))return false;
        try{RebirthNpcWorkOutcomePersistenceStore.EnsureLoaded();}
        catch(Exception ex){Log.Warning("[REBIRTH NPC Work] Outcome backing unavailable; no event delivered: "+ex.Message);return false;}
        if(outcome.CompletedUtcTicks<=0)outcome.CompletedUtcTicks=DateTime.UtcNow.Ticks;
        outcome.DurableCompletion=true;
        RebirthNpcWorkOutcomeRecord priorOutcome=null;
        bool atCapacity;lock(Sync)atCapacity=Outcomes.Count>=MaxOutcomes;
        HashSet<Guid> pendingOwnerAwards=null;
        if(atCapacity&&!RebirthP1SharedWorkAward.TryGetPendingOwnerOutcomeIds(out pendingOwnerAwards))
            return false; // An unavailable award journal is never treated as empty.
        if(atCapacity)pendingOwnerAwards.UnionWith(RebirthP6B13Bridge.PendingUnavailableOutcomeIds());
        lock(Sync)
        {
            foreach(var prior in Outcomes)
                if(prior.OutcomeId==outcome.OutcomeId)
                {
                    // Definitions that still mint a fresh wall-clock time per retry replay onto the recorded time.
                    if(prior.CompletedUtcTicks!=outcome.CompletedUtcTicks&&MintsFreshCompletionTime(outcome.DefinitionId))
                        outcome.CompletedUtcTicks=prior.CompletedUtcTicks;
                    if(!SameCompletionWitness(prior,outcome))return false;
                    priorOutcome=prior;break;
                }
            if(priorOutcome==null)
            {
                if(Outcomes.Count>=MaxOutcomes)
                {
                    if(pendingOwnerAwards==null)return false;
                    var rows=Outcomes.ToArray();int removable=-1;
                    // Keep every outcome named by a saved owner-award intent.
                    // Evict only a checkpointed row whose replay cursor already
                    // passed it; otherwise backpressure the physical publisher.
                    for(int i=0;i<rows.Length&&i<replayCursor;i++)
                        if(Checkpointed.Contains(rows[i].OutcomeId)&&
                           !pendingOwnerAwards.Contains(rows[i].OutcomeId))
                        {removable=i;break;}
                    if(removable<0)return false;
                    Outcomes=new Queue<RebirthNpcWorkOutcomeRecord>(rows.Where((_,i)=>i!=removable));
                    Checkpointed.Remove(rows[removable].OutcomeId);
                    replayCursor--;
                }
                recoveryEpoch=new object();Outcomes.Enqueue(outcome);
                Interlocked.Increment(ref persistenceVersion);
            }
        }
        try{RebirthNpcWorkOutcomePersistenceStore.SaveAndVerifyCommitted(priorOutcome??outcome);}
        catch(Exception ex){Log.Warning("[REBIRTH NPC Work] Retained unverified outcome; no event delivered: "+ex.Message);return false;}
        lock(Sync)Checkpointed.Add(outcome.OutcomeId);
        if(priorOutcome==null)Interlocked.Increment(ref recorded);
        EmitForOutcome(priorOutcome??outcome);
        return true;
    }
    internal static bool HasCommittedLiterature(ulong assignmentId,RebirthNpcStableId npcId)
    {
        RebirthNpcWorkOutcomeRecord match=null;
        lock(Sync)foreach(var row in Outcomes)
            if(row.AssignmentId==assignmentId&&row.NpcId==npcId&&row.DurableCompletion&&
                row.State==RebirthNpcWorkOutcomeState.Completed&&
                RebirthNpcQualifiedWorkGate.AllowsDefinition(row.DefinitionId)&&Checkpointed.Contains(row.OutcomeId))
            {match=row;break;}
        try{return match!=null&&RebirthNpcQualifiedWorkGate.AllowsOutcome(match)&&
            RebirthNpcWorkOutcomePersistenceStore.IsCurrentPrimaryCommitted(match);}
        catch{return false;}
    }

    internal static bool SameCompletionWitness(RebirthNpcWorkOutcomeRecord a,RebirthNpcWorkOutcomeRecord b)
    {
        if(a==null||b==null||!a.DurableCompletion||!b.DurableCompletion||
           a.OutcomeId!=b.OutcomeId||a.AssignmentId!=b.AssignmentId||!a.NpcId.Equals(b.NpcId)||
           (a.DefinitionId??string.Empty)!=(b.DefinitionId??string.Empty)||
           (a.TargetId??string.Empty)!=(b.TargetId??string.Empty)||
           a.DurableWitness!=b.DurableWitness||a.StartedUtcTicks!=b.StartedUtcTicks||
           a.CompletedUtcTicks!=b.CompletedUtcTicks||
           a.State!=b.State||a.FailureCategory!=b.FailureCategory||
           (a.SettlementId??string.Empty)!=(b.SettlementId??string.Empty)||
           (a.Detail??string.Empty)!=(b.Detail??string.Empty))return false;
        return SameResources(a.Produced,b.Produced)&&SameResources(a.Consumed,b.Consumed);
    }
    // Literature (RebirthNpcLiteratureWorkAdapter) and recipe staging (RebirthNpcRecipeStagingWork) stamp
    // DateTime.UtcNow on every attempt; all other executors derive CompletedUtcTicks from a saved row/stamp.
    private static bool MintsFreshCompletionTime(string definitionId)=>
        definitionId==RebirthNpcLiteratureWorkAdapter.DefinitionId||RebirthNpcRecipeStagingWork.IsDefinition(definitionId);
    private static bool SameResources(Dictionary<string,int> a,Dictionary<string,int> b)
    {
        if(a==null||a.Count==0)return b==null||b.Count==0;
        if(b==null||a.Count!=b.Count)return false;
        foreach(var pair in a)if(!b.TryGetValue(pair.Key,out int value)||value!=pair.Value)return false;
        return true;
    }

    private static void EmitForOutcome(RebirthNpcWorkOutcomeRecord o)
    {
        // Management (B13) awards the directing owner once per committed, physically produced/consumed outcome.
        // Independent of the worker's own progression events below; the helper's receipt prevents repeats.
        try{RebirthP6B13Bridge.Offer(o);}catch{}
        // Reading itself saves KnowledgeId and the read marker through the
        // player's literature progression path. It does not award a generic
        // profession or crafting skill in the player implementation.
        if(string.Equals(o.DefinitionId,RebirthNpcLiteratureWorkAdapter.DefinitionId,StringComparison.Ordinal))return;
        // Staging moves an existing ingredient, never recipe production.
        if(RebirthNpcRecipeStagingWork.IsDefinition(o.DefinitionId))return;
        // Existing items were relocated. No new production or undecided
        // Management/Leadership practice formula is inferred from the move.
        if(RebirthNpcStorageOrganizationWork.IsDefinition(o.DefinitionId))return;
        // Saved crop source + native NPC item credit award the shared Farming
        // skill through the exact farm original receipt, not legacy work XP.
        if(string.Equals(o.DefinitionId,RebirthNpcFarmingWorkPackageService.HarvestDefinitionId,StringComparison.Ordinal))return;
        if(RebirthNpcSingleCropWork.IsDefinition(o.DefinitionId))return;
        // Actual material-use receipt only; repair practice/savings are unreleased.
        if(RebirthNpcSingleRepairWork.IsDefinition(o.DefinitionId))return;
        // Queue admission is an input transfer, not a native cooked-output receipt.
        if(RebirthNpcRecipeQueueWork.IsDefinition(o.DefinitionId))return;
        RebirthNpcProgressionEventKind primary = o.State == RebirthNpcWorkOutcomeState.Completed ?
            RebirthNpcProgressionEventKind.WorkCompleted : RebirthNpcProgressionEventKind.WorkFailed;
        Emit(Create(o, primary, string.Empty, 0, 1));
        if (o.State != RebirthNpcWorkOutcomeState.Completed) return;
        string d=(o.DefinitionId??string.Empty).ToLowerInvariant();
        if (d.Contains("farm") || d.Contains("harvest")) Emit(Create(o,RebirthNpcProgressionEventKind.FarmingCompleted,string.Empty,0,2));
        if (d.Contains("repair")) Emit(Create(o,RebirthNpcProgressionEventKind.RepairCompleted,string.Empty,0,3));
        if (d.Contains("craft") || d.Contains("workstation")) Emit(Create(o,RebirthNpcProgressionEventKind.CraftingCompleted,string.Empty,0,4));
        Emit(Create(o,RebirthNpcProgressionEventKind.CapabilityUsed,string.Empty,0,5));
        if (!string.IsNullOrEmpty(o.SettlementId)) Emit(Create(o,RebirthNpcProgressionEventKind.SettlementContribution,string.Empty,0,6));
        EmitResources(o, o.Produced, RebirthNpcProgressionEventKind.ResourceProduced, 100);
    }

    private static void EmitResources(RebirthNpcWorkOutcomeRecord o, Dictionary<string,int> values, RebirthNpcProgressionEventKind kind, int salt)
    {
        if (values == null) return; int i=0;
        var keys=new List<string>(values.Keys);keys.Sort(StringComparer.OrdinalIgnoreCase);
        foreach(string key in keys)if(values[key]>0)Emit(Create(o,kind,key,values[key],salt+(i++)));
    }

    private static RebirthNpcProgressionEvent Create(RebirthNpcWorkOutcomeRecord o, RebirthNpcProgressionEventKind kind,string key,int quantity,int salt)
    { return new RebirthNpcProgressionEvent { EventId=DeriveGuid(o.OutcomeId,salt),OutcomeId=o.OutcomeId,Kind=kind,NpcId=o.NpcId,
        AssignmentId=o.AssignmentId,DefinitionId=o.DefinitionId??string.Empty,ResourceKey=key??string.Empty,Quantity=quantity,
        SettlementId=o.SettlementId??string.Empty,CreatedUtcTicks=DateTime.UtcNow.Ticks}; }

    public static bool Emit(RebirthNpcProgressionEvent e)
    {
        if(e==null||e.EventId==Guid.Empty||e.NpcId.IsEmpty)return false;PendingDelivery delivery;
        if(BlocksRetainedEvent(e.EventId,e.AssignmentId))return false;
        lock(Sync)
        {
            if(Emitted.Contains(e.EventId)){Interlocked.Increment(ref duplicates);return false;}
            if(!PendingDeliveries.TryGetValue(e.EventId,out delivery)){recoveryEpoch=new object();delivery=new PendingDelivery{Event=e};PendingDeliveries[e.EventId]=delivery;Events.Enqueue(e);while(Events.Count>MaxEvents)Events.Dequeue();}
        }
        bool complete=Deliver(delivery);if(complete)CompleteDelivery(e.EventId);return complete;
    }

    public static void TickPendingDeliveries(int maximumEvents)
    {
        // Replay only checkpointed physical outcomes after cold recovery. Award
        // sinks have their own persistent EventId receipts for exactly-once XP.
        RebirthNpcWorkOutcomeRecord[] replay;int start;lock(Sync)
        {
            var rows=Outcomes.ToArray();start=replayCursor;
            int take=Math.Min(16,Math.Max(0,rows.Length-start));
            replay=new RebirthNpcWorkOutcomeRecord[take];Array.Copy(rows,start,replay,0,take);
        }
        int processed=0;
        foreach(var row in replay)
        {
            bool checkpointed;lock(Sync)checkpointed=Checkpointed.Contains(row.OutcomeId);
            if(row.DurableCompletion)
            {
                if(!checkpointed||!RebirthNpcWorkOutcomePersistenceStore.IsCurrentPrimaryCommitted(row))break;
                EmitForOutcome(row);
            }
            processed++;
        }
        lock(Sync)if(replayCursor==start)replayCursor+=processed;
        PendingDelivery[] pending;lock(Sync){pending=new List<PendingDelivery>(PendingDeliveries.Values).ToArray();}
        int budget=maximumEvents<=0?pending.Length:Math.Min(maximumEvents,pending.Length);for(int i=0;i<budget;i++)if(Deliver(pending[i]))CompleteDelivery(pending[i].Event.EventId);
    }

    private static bool Deliver(PendingDelivery delivery)
    {
        IRebirthNpcWorkProgressionEventSink[] sinks;lock(Sync)sinks=Sinks.ToArray();bool complete=true;
        for(int i=0;i<sinks.Length;i++)
        {
            string key=SinkKey(sinks[i]);lock(Sync)if(delivery.Delivered.Contains(key))continue;
            try{sinks[i].Handle(delivery.Event);lock(Sync)delivery.Delivered.Add(key);}
            catch(Exception ex){complete=false;Interlocked.Increment(ref sinkFaults);Log.Warning("[REBIRTH NPC Work] Progression sink failed: "+ex.Message);}
        }
        return complete;
    }
    private static void CompleteDelivery(Guid eventId)
    {
        lock(Sync){if(!PendingDeliveries.Remove(eventId))return;recoveryEpoch=new object();if(!Emitted.Add(eventId))return;EmittedOrder.Enqueue(eventId);while(Emitted.Count>MaxReplay&&EmittedOrder.Count>0)Emitted.Remove(EmittedOrder.Dequeue());}
        Interlocked.Increment(ref emitted);
    }
    private static string SinkKey(IRebirthNpcWorkProgressionEventSink sink){return sink.GetType().FullName+"|"+sink.Priority;}


    internal static bool TryPrepareRecovery(RebirthNpcWorkOutcomeRecord[] records,out RebirthNpcPreparedRecovery candidate,out string error)
    {
        candidate=null;error=string.Empty;if(records==null||records.Length>MaxOutcomes){error="outcomes exceed lossless recovery bound";return false;}
        object expected;int version;string before;lock(Sync){if(PendingDeliveries.Count!=0){error="work outcome deliveries remain unsettled";return false;}expected=recoveryEpoch;version=persistenceVersion;before=RebirthNpcWorkOutcomePersistenceStore.RecoveryRecordsImage(Outcomes.ToArray());}
        var next=new Queue<RebirthNpcWorkOutcomeRecord>();var ids=new HashSet<Guid>();
        foreach(var row in records)
        {
            if(row==null||row.OutcomeId==Guid.Empty||row.AssignmentId==0||row.NpcId.IsEmpty||!ids.Add(row.OutcomeId)){error="invalid/duplicate outcome recovery row";return false;}
            next.Enqueue(new RebirthNpcWorkOutcomeRecord{OutcomeId=row.OutcomeId,AssignmentId=row.AssignmentId,NpcId=row.NpcId,DurableCompletion=row.DurableCompletion,DurableWitness=row.DurableWitness,DefinitionId=row.DefinitionId,TargetId=row.TargetId,StartedUtcTicks=row.StartedUtcTicks,CompletedUtcTicks=row.CompletedUtcTicks,State=row.State,FailureCategory=row.FailureCategory,RetryCount=row.RetryCount,CancellationActor=row.CancellationActor,SettlementId=row.SettlementId,Detail=row.Detail,Produced=row.Produced==null?null:new Dictionary<string,int>(row.Produced,StringComparer.OrdinalIgnoreCase),Consumed=row.Consumed==null?null:new Dictionary<string,int>(row.Consumed,StringComparer.OrdinalIgnoreCase)});
        }
        Func<bool> current=()=>{lock(Sync)return ReferenceEquals(expected,recoveryEpoch)&&persistenceVersion==version&&PendingDeliveries.Count==0&&RebirthNpcWorkOutcomePersistenceStore.RecoveryRecordsImage(Outcomes.ToArray())==before;};
        candidate=new RebirthNpcPreparedRecovery("RebirthNpcWorkOutcomes.xml",current,()=>{lock(Sync){if(!current())return false;Outcomes=next;Checkpointed.Clear();foreach(var row in next)if(row.DurableCompletion)Checkpointed.Add(row.OutcomeId);replayCursor=0;unchecked{persistenceVersion++;}recoveryEpoch=new object();return true;}});return true;
    }
    public static void ImportPersisted(RebirthNpcWorkOutcomeRecord[] records)
    {
        if (records == null || records.Length == 0) return;
        lock (Sync)
        {
            recoveryEpoch=new object();Outcomes.Clear();Checkpointed.Clear();replayCursor=0;
            int start = Math.Max(0, records.Length - MaxOutcomes);
            for (int i = start; i < records.Length; i++)
                if (records[i] != null && records[i].AssignmentId != 0 && !records[i].NpcId.IsEmpty)
                {
                    Outcomes.Enqueue(records[i]);
                    if(records[i].DurableCompletion)Checkpointed.Add(records[i].OutcomeId);
                }
        }
        Interlocked.Increment(ref persistenceVersion);
    }

    public static RebirthNpcWorkOutcomeRecord[] GetOutcomes(){lock(Sync)return Outcomes.ToArray();}
    public static void ResetForWorldChange(){
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled)return;
lock(Sync){recoveryEpoch=new object();Outcomes.Clear();Checkpointed.Clear();replayCursor=0;Events.Clear();Emitted.Clear();EmittedOrder.Clear();PendingDeliveries.Clear();}Interlocked.Increment(ref persistenceVersion);}
    public static RebirthNpcProgressionEvent[] GetEvents(){lock(Sync)return Events.ToArray();}
    public static string GetReport()
    {
        lock(Sync)
        {
            StringBuilder b=new StringBuilder("[REBIRTH NPC Work Outcomes] outcomes=").Append(Outcomes.Count).Append(" events=").Append(Events.Count)
                .Append(" recorded=").Append(Interlocked.Read(ref recorded)).Append(" emitted=").Append(Interlocked.Read(ref emitted))
                .Append(" duplicateEvents=").Append(Interlocked.Read(ref duplicates)).Append(" sinkFaults=").Append(Interlocked.Read(ref sinkFaults)).Append(" pendingDeliveries=").Append(PendingDeliveries.Count);
            RebirthNpcWorkOutcomeRecord[] a=Outcomes.ToArray(); for(int i=Math.Max(0,a.Length-20);i<a.Length;i++) b.AppendLine().Append("  assignment=").Append(a[i].AssignmentId)
                .Append(" npc=").Append(a[i].NpcId).Append(" definition=").Append(a[i].DefinitionId).Append(" state=").Append(a[i].State)
                .Append(" failure=").Append(a[i].FailureCategory).Append(" retries=").Append(a[i].RetryCount).Append(" detail=").Append(a[i].Detail);
            return b.ToString();
        }
    }

    private static Guid DeriveGuid(Guid operationId,int salt)
    { byte[] b=operationId.ToByteArray();byte[] s=BitConverter.GetBytes(salt);for(int i=0;i<4;i++)b[i+8]^=s[i];b[15]^=0x5A;return new Guid(b); }
}
