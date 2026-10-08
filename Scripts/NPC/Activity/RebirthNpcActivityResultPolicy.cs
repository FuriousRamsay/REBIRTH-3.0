using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthNpcActivityOutcome : byte
{
    Succeeded = 0,
    TransientBlocked = 1,
    TransientTargetUnavailable = 2,
    PermanentUnreachable = 3,
    PermanentInvalid = 4,
    Abandoned = 5,
    ControllerFault = 6
}

public enum RebirthNpcActivityDisposition : byte
{
    CompleteLease = 0,
    ScheduleRecovery = 1,
    FailLease = 2,
    ReleaseLease = 3
}

public enum RebirthNpcActivityRecoveryStatus : byte
{
    Pending = 0,
    Claimed = 1,
    Exhausted = 2,
    Cancelled = 3
}

public sealed class RebirthNpcActivityRecoveryTicket
{
    public ulong RecoveryId { get; internal set; }
    public ulong LeaseId { get; internal set; }
    public int TargetEntityId { get; internal set; }
    public RebirthNpcOrderState Order { get; internal set; }
    public RebirthNpcActivityOutcome Outcome { get; internal set; }
    public RebirthNpcActivityRecoveryStatus Status { get; internal set; }
    public int Attempt { get; internal set; }
    public int MaximumAttempts { get; internal set; }
    public string ControllerId { get; internal set; }
    public string Detail { get; internal set; }
    public long CreatedUtcTicks { get; internal set; }
    public long NotBeforeUtcTicks { get; internal set; }
    public long ExpiresUtcTicks { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }

    internal RebirthNpcActivityRecoveryTicket Clone()
    {
        return (RebirthNpcActivityRecoveryTicket)MemberwiseClone();
    }
}

public static class RebirthNpcActivityResultPolicy
{
    public static RebirthNpcActivityDisposition GetDisposition(RebirthNpcActivityOutcome outcome)
    {
        switch (outcome)
        {
            case RebirthNpcActivityOutcome.Succeeded:
                return RebirthNpcActivityDisposition.CompleteLease;
            case RebirthNpcActivityOutcome.TransientBlocked:
            case RebirthNpcActivityOutcome.TransientTargetUnavailable:
                return RebirthNpcActivityDisposition.ScheduleRecovery;
            case RebirthNpcActivityOutcome.Abandoned:
                return RebirthNpcActivityDisposition.ReleaseLease;
            default:
                return RebirthNpcActivityDisposition.FailLease;
        }
    }

    public static int GetMaximumRecoveryAttempts(RebirthNpcOrderState order)
    {
        switch (order)
        {
            case RebirthNpcOrderState.Follow:
            case RebirthNpcOrderState.Stay:
            case RebirthNpcOrderState.Guard:
                return 2;
            case RebirthNpcOrderState.Patrol:
            case RebirthNpcOrderState.Work:
            case RebirthNpcOrderState.Travel:
            case RebirthNpcOrderState.Mission:
                return 3;
            default:
                return 1;
        }
    }

    public static TimeSpan GetRecoveryDelay(int attempt)
    {
        if (attempt <= 1) return TimeSpan.FromSeconds(1);
        if (attempt == 2) return TimeSpan.FromSeconds(5);
        return TimeSpan.FromSeconds(15);
    }

    public static string GetReport()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[REBIRTH NPC] activity result policy");
        foreach (RebirthNpcActivityOutcome outcome in Enum.GetValues(typeof(RebirthNpcActivityOutcome)))
            builder.Append("  outcome=").Append(outcome)
                .Append(" disposition=").Append(GetDisposition(outcome)).AppendLine();
        builder.AppendLine("  recovery attempts: Follow/Stay/Guard=2, Patrol/Work/Travel/Mission=3");
        builder.Append("  recovery delays: attempt1=1s, attempt2=5s, later=15s");
        return builder.ToString();
    }
}

public static class RebirthNpcActivityRecoveryRegistry
{
    private const int MaxHistoryRecords = 256;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcActivityRecoveryTicket> PendingByEntity =
        new Dictionary<int, RebirthNpcActivityRecoveryTicket>();
    private static readonly Dictionary<ulong, RebirthNpcActivityRecoveryTicket> ByRecoveryId =
        new Dictionary<ulong, RebirthNpcActivityRecoveryTicket>();
    private static readonly Queue<ulong> HistoryOrder = new Queue<ulong>();
    private static readonly Dictionary<ulong, int> AttemptsByLease = new Dictionary<ulong, int>();
    private static readonly LinkedList<ulong> PendingOrder = new LinkedList<ulong>();
    private static readonly Dictionary<ulong, LinkedListNode<ulong>> PendingNodes =
        new Dictionary<ulong, LinkedListNode<ulong>>();
    private const int RecoverySweepBudget = 16;
    private static ulong nextRecoveryId = 1UL;

    // Retain original ticket, pending node and attempt history; detach numeric lookup only.
    private static void QuarantineForeignWorkIndex(int entityId)
    {
        RebirthNpcActivityRecoveryTicket original;
        lock (Sync) { if (!PendingByEntity.TryGetValue(entityId, out original) || !RebirthNpcWorkReleaseGate.Hold(original.Order)) return; }
        if (RebirthNpcExecutionLeaseRegistry.IsCurrentWorldWorkLeaseOwner(entityId, original.LeaseId)) return;
        lock (Sync) { RebirthNpcActivityRecoveryTicket current; if (PendingByEntity.TryGetValue(entityId, out current) && ReferenceEquals(current, original)) PendingByEntity.Remove(entityId); }
    }
    internal static void QuarantineForeignWorkIndexes()
    {
        int[] ids;
        lock (Sync) { ids = new int[PendingByEntity.Count]; PendingByEntity.Keys.CopyTo(ids, 0); }
        foreach (int id in ids) QuarantineForeignWorkIndex(id);
    }
    public static bool TrySchedule(RebirthNpcActivityRecord activity,
        RebirthNpcActivityOutcome outcome, string detail, out ulong recoveryId)
    {
        if (activity != null) QuarantineForeignWorkIndex(activity.TargetEntityId);
        // Release hold: preserve original work custody; no native effects.
        if(activity!=null&&RebirthNpcWorkReleaseGate.Hold(activity.Order)){recoveryId=0;return false;}

        recoveryId = 0UL;
        if (activity == null) return false;
        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(activity.TargetEntityId, out lease) ||
            lease.LeaseId != activity.LeaseId)
            return false;

        lock (Sync)
        {
            RebirthNpcActivityRecoveryTicket prior;
            int previousAttempt;
            AttemptsByLease.TryGetValue(activity.LeaseId, out previousAttempt);
            int attempt = previousAttempt + 1;
            AttemptsByLease[activity.LeaseId] = attempt;
            if (PendingByEntity.TryGetValue(activity.TargetEntityId, out prior) &&
                prior.LeaseId == activity.LeaseId)
            {
                TerminalizeLocked(prior, RebirthNpcActivityRecoveryStatus.Cancelled,
                    "Replaced by a newer recovery result for the same lease.");
            }

            int maximum = RebirthNpcActivityResultPolicy.GetMaximumRecoveryAttempts(activity.Order);
            ulong id = nextRecoveryId++;
            if (id == 0UL) id = nextRecoveryId++;
            long now = DateTime.UtcNow.Ticks;
            RebirthNpcActivityRecoveryTicket ticket = new RebirthNpcActivityRecoveryTicket
            {
                RecoveryId = id,
                LeaseId = activity.LeaseId,
                TargetEntityId = activity.TargetEntityId,
                Order = activity.Order,
                Outcome = outcome,
                Status = attempt <= maximum ? RebirthNpcActivityRecoveryStatus.Pending :
                    RebirthNpcActivityRecoveryStatus.Exhausted,
                Attempt = attempt,
                MaximumAttempts = maximum,
                ControllerId = activity.ControllerId ?? string.Empty,
                Detail = detail ?? string.Empty,
                CreatedUtcTicks = now,
                NotBeforeUtcTicks = now + RebirthNpcActivityResultPolicy.GetRecoveryDelay(attempt).Ticks,
                ExpiresUtcTicks = now + TimeSpan.FromMinutes(2).Ticks,
                UpdatedUtcTicks = now
            };
            ByRecoveryId[id] = ticket;
            if (ticket.Status == RebirthNpcActivityRecoveryStatus.Pending)
            {
                PendingByEntity[ticket.TargetEntityId] = ticket;
                LinkedListNode<ulong> node=PendingOrder.AddLast(ticket.RecoveryId);
                PendingNodes[ticket.RecoveryId]=node;
                RebirthNpcActivityRecoveryDiagnostics.RecordScheduled();
                recoveryId = id;
                return true;
            }

            HistoryOrder.Enqueue(id);
            TrimHistoryLocked();
            RebirthNpcActivityRecoveryDiagnostics.RecordExhausted();
            RebirthNpcExecutionLeaseRegistry.TryFail(activity.TargetEntityId, activity.LeaseId,
                activity.AcceptedRevision, "Activity recovery attempts exhausted: " + detail);
            AttemptsByLease.Remove(activity.LeaseId);
            return false;
        }
    }

    public static bool HasPending(int entityId, ulong leaseId)
    {
        QuarantineForeignWorkIndex(entityId);
        lock (Sync)
        {
            RebirthNpcActivityRecoveryTicket current;
            return PendingByEntity.TryGetValue(entityId, out current) &&
                current.LeaseId == leaseId &&
                current.Status == RebirthNpcActivityRecoveryStatus.Pending;
        }
    }

    public static bool TryClaim(int entityId, ulong leaseId, string controllerId,
        out RebirthNpcActivityRecoveryTicket ticket)
    {
        QuarantineForeignWorkIndex(entityId);
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldLease(leaseId)){ticket=null;return false;}

        ticket = null;
        lock (Sync)
        {
            RebirthNpcActivityRecoveryTicket current;
            if (!PendingByEntity.TryGetValue(entityId, out current) || current.LeaseId != leaseId ||
                current.Status != RebirthNpcActivityRecoveryStatus.Pending ||
                DateTime.UtcNow.Ticks < current.NotBeforeUtcTicks)
                return false;
            if(RebirthNpcWorkReleaseGate.Hold(current.Order))return false;
            current.ControllerId = controllerId ?? string.Empty;
            TerminalizeLocked(current, RebirthNpcActivityRecoveryStatus.Claimed,
                "Recovery claimed by controller " + (controllerId ?? string.Empty) + ".");
            RebirthNpcActivityRecoveryDiagnostics.RecordClaimed();
            ticket = current.Clone();
            return true;
        }
    }

    public static void ReleaseForEntity(int entityId, string reason)
    {
        QuarantineForeignWorkIndex(entityId);
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return;

        lock (Sync)
        {
            RebirthNpcActivityRecoveryTicket ticket;
            if (!PendingByEntity.TryGetValue(entityId, out ticket)) return;
            if(RebirthNpcWorkReleaseGate.Hold(ticket.Order))return;
            TerminalizeLocked(ticket, RebirthNpcActivityRecoveryStatus.Cancelled,
                reason ?? "Target left the active runtime.");
            AttemptsByLease.Remove(ticket.LeaseId);
            RebirthNpcActivityRecoveryDiagnostics.RecordCancelled();
        }
    }

    public static void Tick()
    {
        long now = DateTime.UtcNow.Ticks;
        lock (Sync)
        {
            int processed=0;
            while(processed<RecoverySweepBudget && PendingOrder.First!=null)
            {
                LinkedListNode<ulong> node=PendingOrder.First;
                RebirthNpcActivityRecoveryTicket held;
                while(node!=null&&ByRecoveryId.TryGetValue(node.Value,out held)&&RebirthNpcWorkReleaseGate.Hold(held.Order))node=node.Next;
                if(node==null)break;
                PendingOrder.Remove(node);
                PendingNodes.Remove(node.Value);
                processed++;

                RebirthNpcActivityRecoveryTicket ticket;
                if(!ByRecoveryId.TryGetValue(node.Value,out ticket) ||
                    ticket.Status!=RebirthNpcActivityRecoveryStatus.Pending) continue;

                RebirthNpcExecutionLease lease;
                bool leaseStillActive=RebirthNpcExecutionLeaseRegistry.TryGetActive(
                    ticket.TargetEntityId,out lease) && lease.LeaseId==ticket.LeaseId;
                if(now>=ticket.ExpiresUtcTicks || !leaseStillActive)
                {
                    TerminalizeLocked(ticket,RebirthNpcActivityRecoveryStatus.Cancelled,
                        "Recovery expired or its execution lease ended.");
                    if(leaseStillActive && now>=ticket.ExpiresUtcTicks)
                        RebirthNpcExecutionLeaseRegistry.TryFail(ticket.TargetEntityId,ticket.LeaseId,
                            lease.AcceptedRevision,"Activity recovery window expired.");
                    if(!leaseStillActive || now>=ticket.ExpiresUtcTicks)AttemptsByLease.Remove(ticket.LeaseId);
                    RebirthNpcActivityRecoveryDiagnostics.RecordCancelled();
                    continue;
                }

                LinkedListNode<ulong> next=PendingOrder.AddLast(ticket.RecoveryId);
                PendingNodes[ticket.RecoveryId]=next;
            }
        }
    }

    public static void ReleaseForLease(ulong leaseId)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldLease(leaseId))return;

        if (leaseId == 0UL) return;
        lock (Sync)
        {
            AttemptsByLease.Remove(leaseId);
            RebirthNpcActivityRecoveryTicket pending = null;
            foreach (RebirthNpcActivityRecoveryTicket ticket in PendingByEntity.Values)
            {
                if (ticket != null && ticket.LeaseId == leaseId) { pending = ticket; break; }
            }
            if (pending != null)
            {
                TerminalizeLocked(pending, RebirthNpcActivityRecoveryStatus.Cancelled,
                    "Execution lease reached a terminal state.");
                RebirthNpcActivityRecoveryDiagnostics.RecordCancelled();
            }
        }
    }

    public static void ResetForWorldChange()
    {
        lock(Sync)
        {
            var recoveryKeys=new List<ulong>(ByRecoveryId.Keys);
            foreach(ulong id in recoveryKeys){var ticket=ByRecoveryId[id];if(RebirthNpcWorkReleaseGate.Hold(ticket.Order))continue;ByRecoveryId.Remove(id);}
            var entityKeys=new List<int>(PendingByEntity.Keys);foreach(int id in entityKeys)if(!RebirthNpcWorkReleaseGate.Hold(PendingByEntity[id].Order))PendingByEntity.Remove(id);
            var attemptKeys=new List<ulong>(AttemptsByLease.Keys);foreach(ulong id in attemptKeys)if(!RebirthNpcWorkReleaseGate.HoldLease(id))AttemptsByLease.Remove(id);
            var node=PendingOrder.First;while(node!=null){var next=node.Next;if(!ByRecoveryId.ContainsKey(node.Value)){PendingNodes.Remove(node.Value);PendingOrder.Remove(node);}node=next;}
            int count=HistoryOrder.Count;for(int i=0;i<count;i++){ulong id=HistoryOrder.Dequeue();if(ByRecoveryId.ContainsKey(id))HistoryOrder.Enqueue(id);}
            // nextRecoveryId remains monotonic while held original records exist.
            if(ByRecoveryId.Count==0)nextRecoveryId=1UL;
        }
    }


    public static RebirthNpcActivityRecoveryTicket[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcActivityRecoveryTicket[] result =
                new RebirthNpcActivityRecoveryTicket[ByRecoveryId.Count];
            int index = 0;
            foreach (RebirthNpcActivityRecoveryTicket ticket in ByRecoveryId.Values)
                result[index++] = ticket.Clone();
            Array.Sort(result, (left, right) => left.RecoveryId.CompareTo(right.RecoveryId));
            return result;
        }
    }

    private static void TerminalizeLocked(RebirthNpcActivityRecoveryTicket ticket,
        RebirthNpcActivityRecoveryStatus status, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.Hold(ticket.Order))return;

        ticket.Status = status;
        ticket.Detail = detail ?? string.Empty;
        ticket.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
        PendingByEntity.Remove(ticket.TargetEntityId);
        LinkedListNode<ulong> node;
        if(PendingNodes.TryGetValue(ticket.RecoveryId,out node))
        {
            PendingOrder.Remove(node);
            PendingNodes.Remove(ticket.RecoveryId);
        }
        HistoryOrder.Enqueue(ticket.RecoveryId);
        TrimHistoryLocked();
    }

    private static void TrimHistoryLocked()
    {
        int scanBudget = HistoryOrder.Count;
        while (scanBudget-- > 0 && HistoryOrder.Count > MaxHistoryRecords)
        {
            ulong oldest = HistoryOrder.Dequeue();
            RebirthNpcActivityRecoveryTicket ticket;
            if (ByRecoveryId.TryGetValue(oldest, out ticket) && RebirthNpcWorkReleaseGate.Hold(ticket.Order)) { HistoryOrder.Enqueue(oldest); continue; }
            if (ByRecoveryId.TryGetValue(oldest, out ticket) &&
                ticket.Status != RebirthNpcActivityRecoveryStatus.Pending)
                ByRecoveryId.Remove(oldest);
        }
    }
}

public static class RebirthNpcActivityRecoveryDiagnostics
{
    private static long scheduled, claimed, exhausted, cancelled;
    internal static void RecordScheduled() { scheduled++; }
    internal static void RecordClaimed() { claimed++; }
    internal static void RecordExhausted() { exhausted++; }
    internal static void RecordCancelled() { cancelled++; }

    public static string GetReport()
    {
        RebirthNpcActivityRecoveryTicket[] tickets = RebirthNpcActivityRecoveryRegistry.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] activity recoveries tracked=").Append(tickets.Length)
            .Append(" scheduled=").Append(scheduled)
            .Append(" claimed=").Append(claimed)
            .Append(" exhausted=").Append(exhausted)
            .Append(" cancelled=").Append(cancelled).AppendLine();
        for (int i = Math.Max(0, tickets.Length - 25); i < tickets.Length; i++)
        {
            RebirthNpcActivityRecoveryTicket ticket = tickets[i];
            builder.Append("  recovery=").Append(ticket.RecoveryId)
                .Append(" lease=").Append(ticket.LeaseId)
                .Append(" entity=").Append(ticket.TargetEntityId)
                .Append(" order=").Append(ticket.Order)
                .Append(" outcome=").Append(ticket.Outcome)
                .Append(" attempt=").Append(ticket.Attempt).Append('/').Append(ticket.MaximumAttempts)
                .Append(" status=").Append(ticket.Status)
                .Append(" detail=").Append(ticket.Detail).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
