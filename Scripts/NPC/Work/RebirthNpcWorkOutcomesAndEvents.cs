using System;
using System.Collections.Generic;
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

public static class RebirthNpcWorkOutcomeService
{
    private const int MaxOutcomes = 512, MaxEvents = 1024, MaxReplay = 2048;
    private static readonly object Sync = new object();
    private static readonly Queue<RebirthNpcWorkOutcomeRecord> Outcomes = new Queue<RebirthNpcWorkOutcomeRecord>();
    private static readonly Queue<RebirthNpcProgressionEvent> Events = new Queue<RebirthNpcProgressionEvent>();
    private sealed class PendingDelivery{public RebirthNpcProgressionEvent Event;public readonly HashSet<string> Delivered=new HashSet<string>(StringComparer.Ordinal);}
    private static readonly HashSet<Guid> Emitted = new HashSet<Guid>();
    private static readonly Queue<Guid> EmittedOrder = new Queue<Guid>();
    private static readonly Dictionary<Guid,PendingDelivery> PendingDeliveries=new Dictionary<Guid,PendingDelivery>();
    private static readonly List<IRebirthNpcWorkProgressionEventSink> Sinks = new List<IRebirthNpcWorkProgressionEventSink>();
    private static long recorded, emitted, duplicates, sinkFaults;
    private static int persistenceVersion;

    public static int PersistenceVersion { get { return System.Threading.Volatile.Read(ref persistenceVersion); } }

    public static void RegisterSink(IRebirthNpcWorkProgressionEventSink sink)
    {
        if (sink == null) throw new ArgumentNullException(nameof(sink));
        lock (Sync) { if (!Sinks.Contains(sink)) Sinks.Add(sink); Sinks.Sort(delegate(IRebirthNpcWorkProgressionEventSink a,IRebirthNpcWorkProgressionEventSink b){return b.Priority.CompareTo(a.Priority);}); }
    }

    public static void Record(RebirthNpcWorkOutcomeRecord outcome)
    {
        if (outcome == null || outcome.AssignmentId == 0 || outcome.NpcId.IsEmpty) return;
        if (outcome.OutcomeId == Guid.Empty) outcome.OutcomeId = DeriveGuid(outcome.AssignmentId, 0);
        if (outcome.CompletedUtcTicks <= 0) outcome.CompletedUtcTicks = DateTime.UtcNow.Ticks;
        lock (Sync) { Outcomes.Enqueue(outcome); while (Outcomes.Count > MaxOutcomes) Outcomes.Dequeue(); }
        Interlocked.Increment(ref persistenceVersion);
        Interlocked.Increment(ref recorded);
        EmitForOutcome(outcome);
    }

    private static void EmitForOutcome(RebirthNpcWorkOutcomeRecord o)
    {
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
        foreach (KeyValuePair<string,int> p in values) if (p.Value>0) Emit(Create(o,kind,p.Key,p.Value,salt+(i++)));
    }

    private static RebirthNpcProgressionEvent Create(RebirthNpcWorkOutcomeRecord o, RebirthNpcProgressionEventKind kind,string key,int quantity,int salt)
    { return new RebirthNpcProgressionEvent { EventId=DeriveGuid(o.AssignmentId,salt),OutcomeId=o.OutcomeId,Kind=kind,NpcId=o.NpcId,
        AssignmentId=o.AssignmentId,DefinitionId=o.DefinitionId??string.Empty,ResourceKey=key??string.Empty,Quantity=quantity,
        SettlementId=o.SettlementId??string.Empty,CreatedUtcTicks=DateTime.UtcNow.Ticks}; }

    public static bool Emit(RebirthNpcProgressionEvent e)
    {
        if(e==null||e.EventId==Guid.Empty||e.NpcId.IsEmpty)return false;PendingDelivery delivery;
        lock(Sync)
        {
            if(Emitted.Contains(e.EventId)){Interlocked.Increment(ref duplicates);return false;}
            if(!PendingDeliveries.TryGetValue(e.EventId,out delivery)){delivery=new PendingDelivery{Event=e};PendingDeliveries[e.EventId]=delivery;Events.Enqueue(e);while(Events.Count>MaxEvents)Events.Dequeue();}
        }
        bool complete=Deliver(delivery);if(complete)CompleteDelivery(e.EventId);return complete;
    }

    public static void TickPendingDeliveries(int maximumEvents)
    {
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
        lock(Sync){if(!PendingDeliveries.Remove(eventId))return;if(!Emitted.Add(eventId))return;EmittedOrder.Enqueue(eventId);while(Emitted.Count>MaxReplay&&EmittedOrder.Count>0)Emitted.Remove(EmittedOrder.Dequeue());}
        Interlocked.Increment(ref emitted);
    }
    private static string SinkKey(IRebirthNpcWorkProgressionEventSink sink){return sink.GetType().FullName+"|"+sink.Priority;}


    public static void ImportPersisted(RebirthNpcWorkOutcomeRecord[] records)
    {
        if (records == null || records.Length == 0) return;
        lock (Sync)
        {
            Outcomes.Clear();
            int start = Math.Max(0, records.Length - MaxOutcomes);
            for (int i = start; i < records.Length; i++)
                if (records[i] != null && records[i].AssignmentId != 0 && !records[i].NpcId.IsEmpty)
                    Outcomes.Enqueue(records[i]);
        }
        Interlocked.Increment(ref persistenceVersion);
    }

    public static RebirthNpcWorkOutcomeRecord[] GetOutcomes(){lock(Sync)return Outcomes.ToArray();}
    public static void ResetForWorldChange(){
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled)return;
lock(Sync){Outcomes.Clear();Events.Clear();Emitted.Clear();EmittedOrder.Clear();PendingDeliveries.Clear();}Interlocked.Increment(ref persistenceVersion);}
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

    private static Guid DeriveGuid(ulong assignmentId,int salt)
    { byte[] b=new byte[16]; BitConverter.GetBytes(assignmentId).CopyTo(b,0); BitConverter.GetBytes(salt).CopyTo(b,8); BitConverter.GetBytes(unchecked((int)0x52424E50)).CopyTo(b,12); return new Guid(b); }
}
