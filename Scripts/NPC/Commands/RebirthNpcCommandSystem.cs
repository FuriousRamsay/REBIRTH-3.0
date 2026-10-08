using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthNpcCommandKind : byte
{
    Clear = 0,
    Follow = 1,
    Stay = 2,
    Guard = 3,
    Patrol = 4,
    Work = 5,
    Travel = 6,
    Mission = 7
}

public enum RebirthNpcCommandOrigin : byte
{
    TrustedServer = 0,
    AuthenticatedClient = 1
}

public enum RebirthNpcCommandStatus : byte
{
    Submitted = 0,
    Validated = 1,
    Queued = 2,
    Executing = 3,
    Completed = 4,
    Rejected = 5,
    Cancelled = 6,
    Failed = 7,
    Expired = 8,
    Superseded = 9,
    RetryScheduled = 10
}

public enum RebirthNpcCommandPriority : byte
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum RebirthNpcCommandFailureKind : byte
{
    None = 0,
    TransientTargetUnavailable = 1,
    TransientPresence = 2,
    PermanentValidation = 3,
    PermanentTransaction = 4,
    UnexpectedException = 5
}

public readonly struct RebirthNpcCommandRequest
{
    public readonly ulong CommandId;
    public readonly int TargetEntityId;
    public readonly int SubjectEntityId;
    public readonly string IssuerId;
    public readonly RebirthNpcCommandKind Kind;
    public readonly Vector3 TargetPosition;
    public readonly bool HasTargetPosition;
    public readonly uint ExpectedRevision;
    public readonly RebirthNpcCommandOrigin Origin;
    public readonly RebirthNpcCommandPriority Priority;
    public readonly long ExpiresUtcTicks;

    public RebirthNpcCommandRequest(ulong commandId, int targetEntityId, int subjectEntityId, string issuerId,
        RebirthNpcCommandKind kind, Vector3 targetPosition, bool hasTargetPosition, uint expectedRevision,
        RebirthNpcCommandOrigin origin, RebirthNpcCommandPriority priority, long expiresUtcTicks)
    {
        CommandId = commandId;
        TargetEntityId = targetEntityId;
        SubjectEntityId = subjectEntityId;
        IssuerId = issuerId ?? string.Empty;
        Kind = kind;
        TargetPosition = targetPosition;
        HasTargetPosition = hasTargetPosition;
        ExpectedRevision = expectedRevision;
        Origin = origin;
        Priority = priority;
        ExpiresUtcTicks = expiresUtcTicks;
    }
}

public sealed class RebirthNpcCommandRecord
{
    public RebirthNpcCommandRequest Request { get; internal set; }
    public RebirthNpcCommandStatus Status { get; internal set; }
    public string Detail { get; internal set; }
    public long SubmittedUtcTicks { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }
    public int AttemptCount { get; internal set; }
    public long NextAttemptUtcTicks { get; internal set; }
    public RebirthNpcCommandFailureKind LastFailureKind { get; internal set; }

    internal RebirthNpcCommandRecord(RebirthNpcCommandRequest request)
    {
        Request = request;
        Status = RebirthNpcCommandStatus.Submitted;
        Detail = string.Empty;
        SubmittedUtcTicks = DateTime.UtcNow.Ticks;
        UpdatedUtcTicks = SubmittedUtcTicks;
        AttemptCount = 0;
        NextAttemptUtcTicks = 0L;
        LastFailureKind = RebirthNpcCommandFailureKind.None;
    }

    internal void SetStatus(RebirthNpcCommandStatus status, string detail)
    {
        Status = status;
        Detail = detail ?? string.Empty;
        UpdatedUtcTicks = DateTime.UtcNow.Ticks;
    }

    internal void ScheduleRetry(RebirthNpcCommandFailureKind failureKind, string detail, long nextAttemptUtcTicks)
    {
        LastFailureKind = failureKind;
        NextAttemptUtcTicks = nextAttemptUtcTicks;
        SetStatus(RebirthNpcCommandStatus.RetryScheduled, detail);
    }
}

public readonly struct RebirthNpcCommandSubmissionResult
{
    public readonly bool Accepted;
    public readonly ulong CommandId;
    public readonly string Error;

    private RebirthNpcCommandSubmissionResult(bool accepted, ulong commandId, string error)
    {
        Accepted = accepted;
        CommandId = commandId;
        Error = error ?? string.Empty;
    }

    public static RebirthNpcCommandSubmissionResult Success(ulong id) =>
        new RebirthNpcCommandSubmissionResult(true, id, string.Empty);
    public static RebirthNpcCommandSubmissionResult Failure(string error) =>
        new RebirthNpcCommandSubmissionResult(false, 0UL, error);
}

public static class RebirthNpcCommandGateway
{
    private const int MaxQueuedCommands = 2048;
    private const int MaxHistoryRecords = 512;
    private const int MaxCommandsPerFrame = 8;
    private static readonly object Sync = new object();
    private static readonly Queue<ulong>[] PendingByPriority =
    {
        new Queue<ulong>(),
        new Queue<ulong>(),
        new Queue<ulong>(),
        new Queue<ulong>()
    };
    private static int pendingCount;
    private static readonly SortedDictionary<long, Queue<ulong>> RetryByDue =
        new SortedDictionary<long, Queue<ulong>>();
    private static readonly Dictionary<ulong, RebirthNpcCommandRecord> Records =
        new Dictionary<ulong, RebirthNpcCommandRecord>();
    private static readonly Queue<ulong> HistoryOrder = new Queue<ulong>();
    private static ulong nextCommandId = 1UL;

    public static RebirthNpcCommandSubmissionResult Submit(EntityRebirthNPC npc, string issuerId,
        RebirthNpcCommandKind kind, Vector3 targetPosition, bool hasTargetPosition, uint expectedRevision)
    {
        return SubmitCore(npc, issuerId, kind, targetPosition, hasTargetPosition, expectedRevision,
            RebirthNpcCommandOrigin.TrustedServer, -1);
    }

    public static RebirthNpcCommandSubmissionResult SubmitForSubject(EntityRebirthNPC npc, string issuerId,
        int subjectEntityId, RebirthNpcCommandKind kind, Vector3 targetPosition,
        bool hasTargetPosition, uint expectedRevision)
    {
        return SubmitCore(npc, issuerId, kind, targetPosition, hasTargetPosition, expectedRevision,
            RebirthNpcCommandOrigin.TrustedServer, subjectEntityId);
    }

    public static RebirthNpcCommandSubmissionResult SubmitAuthenticated(EntityRebirthNPC npc, string authenticatedIssuerId,
        int authenticatedPlayerEntityId, RebirthNpcCommandKind kind, Vector3 targetPosition,
        bool hasTargetPosition, uint expectedRevision)
    {
        return SubmitCore(npc, authenticatedIssuerId, kind, targetPosition, hasTargetPosition, expectedRevision,
            RebirthNpcCommandOrigin.AuthenticatedClient, authenticatedPlayerEntityId);
    }

    private static RebirthNpcCommandSubmissionResult SubmitCore(EntityRebirthNPC npc, string issuerId,
        RebirthNpcCommandKind kind, Vector3 targetPosition, bool hasTargetPosition, uint expectedRevision,
        RebirthNpcCommandOrigin origin, int subjectEntityId)
    {
        // Release hold: preserve original work custody; no native effects.
        if (RebirthNpcWorkReleaseGate.Hold(kind) || (npc!=null && RebirthNpcWorkReleaseGate.HoldEntity(npc.entityId))) return RebirthNpcCommandSubmissionResult.Failure(RebirthNpcWorkReleaseGate.Detail);

        if (!IsServer()) return RebirthNpcCommandSubmissionResult.Failure("Commands may only be submitted to the authoritative server.");
        if (npc == null || npc.RebirthRuntimeState == null)
            return RebirthNpcCommandSubmissionResult.Failure("Target NPC is unavailable.");

        ulong id;
        lock (Sync)
        {
            if (pendingCount >= MaxQueuedCommands)
                return RebirthNpcCommandSubmissionResult.Failure("NPC command queue is full.");
            id = nextCommandId++;
            if (id == 0UL) id = nextCommandId++;
        }

        RebirthNpcCommandPriority priority = RebirthNpcCommandPolicy.GetPriority(kind);
        long expiresUtcTicks = DateTime.UtcNow.AddSeconds(RebirthNpcCommandPolicy.GetLifetimeSeconds(kind)).Ticks;
        RebirthNpcCommandRequest request = new RebirthNpcCommandRequest(id, npc.entityId, subjectEntityId, issuerId,
            kind, targetPosition, hasTargetPosition, expectedRevision, origin, priority, expiresUtcTicks);
        RebirthNpcCommandRecord record = new RebirthNpcCommandRecord(request);
        string validationError = Validate(npc, request);
        if (!string.IsNullOrEmpty(validationError))
        {
            record.SetStatus(RebirthNpcCommandStatus.Rejected, validationError);
            StoreTerminal(record);
            RebirthNpcCommandDiagnostics.RecordRejected();
            return RebirthNpcCommandSubmissionResult.Failure(validationError);
        }

        record.SetStatus(RebirthNpcCommandStatus.Validated, "Validated against authoritative NPC state.");
        lock (Sync)
        {
            string arbitrationError = ApplyQueueArbitrationLocked(record);
            if (!string.IsNullOrEmpty(arbitrationError))
            {
                TerminalizeLocked(record, RebirthNpcCommandStatus.Rejected, arbitrationError);
                RebirthNpcCommandDiagnostics.RecordRejected();
                return RebirthNpcCommandSubmissionResult.Failure(arbitrationError);
            }
            record.SetStatus(RebirthNpcCommandStatus.Queued,
                "Queued at " + priority + " priority for the next safe scheduler boundary.");
            Records[id] = record;
            PendingByPriority[(int)priority].Enqueue(id);
            pendingCount++;
        }
        RebirthNpcCommandDiagnostics.RecordSubmitted();
        return RebirthNpcCommandSubmissionResult.Success(id);
    }

    public static bool TryGet(ulong commandId, out RebirthNpcCommandRecord record)
    {
        lock (Sync) return Records.TryGetValue(commandId, out record);
    }

    public static RebirthNpcCommandRecord[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcCommandRecord[] result = new RebirthNpcCommandRecord[Records.Count];
            Records.Values.CopyTo(result, 0);
            Array.Sort(result, (a, b) => a.Request.CommandId.CompareTo(b.Request.CommandId));
            return result;
        }
    }

    public static void Tick()
    {
        if (!IsServer()) return;
        int executed = 0;
        while (executed < MaxCommandsPerFrame)
        {
            RebirthNpcCommandRecord record = null;
            RebirthNpcCommandRecord terminal = null;
            lock (Sync)
            {
                long nowTicks = DateTime.UtcNow.Ticks;
                MoveDueRetriesLocked(nowTicks);
                ulong id;
                if (!TryDequeueNextLocked(out id)) return;
                if (!Records.TryGetValue(id, out record)) continue;
                if (record.Status != RebirthNpcCommandStatus.Queued &&
                    record.Status != RebirthNpcCommandStatus.RetryScheduled) continue;
                if (record.Status == RebirthNpcCommandStatus.RetryScheduled &&
                    nowTicks < record.NextAttemptUtcTicks)
                {
                    ScheduleRetryTokenLocked(record.Request.CommandId, record.NextAttemptUtcTicks);
                    pendingCount++;
                    continue;
                }
                if (nowTicks > record.Request.ExpiresUtcTicks)
                {
                    if (TerminalizeLocked(record, RebirthNpcCommandStatus.Expired,
                        "Command expired before execution."))
                    {
                        RebirthNpcCommandDiagnostics.RecordExpired();
                        terminal = record;
                    }
                    record = null;
                }
                else
                {
                    record.AttemptCount++;
                    record.LastFailureKind = RebirthNpcCommandFailureKind.None;
                    record.SetStatus(RebirthNpcCommandStatus.Executing,
                        "Applying normalized command attempt " + record.AttemptCount + ".");
                }
            }
            if (terminal != null) RebirthNpcRemoteCommandService.OnTerminal(terminal);
            if (record == null) continue;
            executed++;
            Execute(record);
        }
    }

    public static void CancelForEntity(int entityId, string reason)
    {
        List<RebirthNpcCommandRecord> terminal = new List<RebirthNpcCommandRecord>();
        lock (Sync)
        {
            foreach (RebirthNpcCommandRecord record in Records.Values)
            {
                if (record.Request.TargetEntityId != entityId) continue;
                if (record.Status != RebirthNpcCommandStatus.Queued &&
                    record.Status != RebirthNpcCommandStatus.Validated &&
                    record.Status != RebirthNpcCommandStatus.RetryScheduled) continue;
                if (TerminalizeLocked(record, RebirthNpcCommandStatus.Cancelled,
                    reason ?? "Target left runtime."))
                {
                    terminal.Add(record);
                    RebirthNpcCommandDiagnostics.RecordCancelled();
                }
            }
        }
        for (int i = 0; i < terminal.Count; i++)
            RebirthNpcRemoteCommandService.OnTerminal(terminal[i]);
    }

    public static void ResetForWorldChange(string reason)
    {
        List<RebirthNpcCommandRecord> terminal = new List<RebirthNpcCommandRecord>();
        lock (Sync)
        {
            foreach (RebirthNpcCommandRecord record in Records.Values)
            {
                if (record.Status != RebirthNpcCommandStatus.Queued &&
                    record.Status != RebirthNpcCommandStatus.Validated &&
                    record.Status != RebirthNpcCommandStatus.RetryScheduled &&
                    record.Status != RebirthNpcCommandStatus.Executing) continue;
                if (TerminalizeLocked(record, RebirthNpcCommandStatus.Cancelled,
                    reason ?? "World runtime changed."))
                    terminal.Add(record);
            }
            for (int i=0;i<PendingByPriority.Length;i++) RetainHeldQueue(PendingByPriority[i]);
            var retryKeys=new List<long>(RetryByDue.Keys);foreach(long due in retryKeys){RetainHeldQueue(RetryByDue[due]);if(RetryByDue[due].Count==0)RetryByDue.Remove(due);}
            pendingCount=0;foreach(var queue in PendingByPriority)pendingCount+=queue.Count;foreach(var pair in RetryByDue)pendingCount+=pair.Value.Count;
        }
        for (int i = 0; i < terminal.Count; i++)
            RebirthNpcRemoteCommandService.OnTerminal(terminal[i]);
    }

    private static void RetainHeldQueue(Queue<ulong> queue)
    {
        int count=queue.Count;for(int i=0;i<count;i++){ulong id=queue.Dequeue();RebirthNpcCommandRecord record;if(Records.TryGetValue(id,out record)&&RebirthNpcWorkReleaseGate.Hold(record.Request.Kind))queue.Enqueue(id);}
    }
    private static bool TryDequeueNextLocked(out ulong id)
    {
        for(int priority=PendingByPriority.Length-1;priority>=0;priority--)
        {
            var queue=PendingByPriority[priority];int count=queue.Count; int eligible=-1,index=0;
            foreach(ulong token in queue){RebirthNpcCommandRecord held;if(!Records.TryGetValue(token,out held)||!RebirthNpcWorkReleaseGate.Hold(held.Request.Kind)){eligible=index;break;}index++;}
            if(eligible<0)continue;
            id=0;for(int i=0;i<count;i++)
            {
                ulong candidate=queue.Dequeue();if(i==eligible)id=candidate;else queue.Enqueue(candidate);
            }
            if(pendingCount>0)pendingCount--;return true;
        }
        id=0;return false;
    }

    private static void MoveDueRetriesLocked(long nowTicks)
    {
        var dueKeys=new List<long>();foreach(var pair in RetryByDue)if(pair.Key<=nowTicks)dueKeys.Add(pair.Key);
        foreach(long due in dueKeys)
        {
            var ids=RetryByDue[due];var retained=new Queue<ulong>();
            while(ids.Count>0)
            {
                ulong id=ids.Dequeue();RebirthNpcCommandRecord record;
                if(Records.TryGetValue(id,out record)&&RebirthNpcWorkReleaseGate.Hold(record.Request.Kind)){retained.Enqueue(id);continue;}
                if(record==null||record.Status!=RebirthNpcCommandStatus.RetryScheduled||record.NextAttemptUtcTicks!=due){if(pendingCount>0)pendingCount--;continue;}
                PendingByPriority[(int)record.Request.Priority].Enqueue(id);
            }
            if(retained.Count==0)RetryByDue.Remove(due);else RetryByDue[due]=retained;
        }
    }

    private static void ScheduleRetryTokenLocked(ulong id, long dueTicks)
    {
        Queue<ulong> queue;
        if (!RetryByDue.TryGetValue(dueTicks, out queue))
            RetryByDue[dueTicks] = queue = new Queue<ulong>();
        queue.Enqueue(id);
    }

    private static string ApplyQueueArbitrationLocked(RebirthNpcCommandRecord incoming)
    {
        foreach (RebirthNpcCommandRecord existing in Records.Values)
        {
            if (existing.Status != RebirthNpcCommandStatus.Queued &&
                existing.Status != RebirthNpcCommandStatus.Validated &&
                existing.Status != RebirthNpcCommandStatus.RetryScheduled)
                continue;
            if (existing.Request.TargetEntityId != incoming.Request.TargetEntityId) continue;
            if (!string.Equals(existing.Request.IssuerId, incoming.Request.IssuerId,
                    StringComparison.OrdinalIgnoreCase)) continue;

            if (existing.Request.Priority > incoming.Request.Priority)
                return "A higher-priority " + existing.Request.Kind + " command is already queued.";

            if (TerminalizeLocked(existing, RebirthNpcCommandStatus.Superseded,
                "Superseded by command " + incoming.Request.CommandId + " (" + incoming.Request.Kind + ")."))
            {
                RebirthNpcCommandDiagnostics.RecordSuperseded();
                RebirthNpcRemoteCommandService.OnTerminal(existing);
            }
        }
        TrimHistory();
        return string.Empty;
    }

    private static void Execute(RebirthNpcCommandRecord record)
    {
        // Release hold: preserve original work custody; no native effects.
        if (record!=null && RebirthNpcWorkReleaseGate.Hold(record.Request.Kind)) return;

        try
        {
            World world = GameManager.Instance?.World;
            EntityRebirthNPC npc = world?.GetEntity(record.Request.TargetEntityId) as EntityRebirthNPC;
            if (npc == null)
            {
                if (TryScheduleRetry(record, RebirthNpcCommandFailureKind.TransientTargetUnavailable,
                        "Target NPC is temporarily unavailable.")) return;
                Complete(record, RebirthNpcCommandStatus.Cancelled,
                    "Target NPC remained unavailable after retry policy was exhausted.");
                RebirthNpcCommandDiagnostics.RecordCancelled();
                return;
            }

            RebirthNpcCommandFailureKind validationFailureKind;
            string validationError = Validate(npc, record.Request, out validationFailureKind);
            if (!string.IsNullOrEmpty(validationError))
            {
                if ((validationFailureKind == RebirthNpcCommandFailureKind.TransientPresence ||
                     validationFailureKind == RebirthNpcCommandFailureKind.TransientTargetUnavailable) &&
                    TryScheduleRetry(record, validationFailureKind, validationError)) return;
                Complete(record, RebirthNpcCommandStatus.Rejected, validationError);
                RebirthNpcCommandDiagnostics.RecordRejected();
                return;
            }

            RebirthNpcOrderState order = NormalizeOrder(record.Request.Kind);
            bool needsPosition = order == RebirthNpcOrderState.Guard || order == RebirthNpcOrderState.Patrol ||
                                 order == RebirthNpcOrderState.Work || order == RebirthNpcOrderState.Travel ||
                                 order == RebirthNpcOrderState.Mission;
            RebirthNpcTransactionResult result = npc.SetRebirthOrder(order,
                record.Request.TargetPosition, needsPosition && record.Request.HasTargetPosition);
            if (!result.Succeeded)
            {
                Complete(record, RebirthNpcCommandStatus.Failed, result.Error);
                record.LastFailureKind = RebirthNpcCommandFailureKind.PermanentTransaction;
                RebirthNpcCommandDiagnostics.RecordFailed();
                return;
            }

            RebirthNpcExecutionLeaseRegistry.BeginOrReplace(npc, record, result.Revision);
            Complete(record, RebirthNpcCommandStatus.Completed,
                "Order accepted at revision " + result.Revision +
                "; execution lease updated.");
            RebirthNpcCommandDiagnostics.RecordCompleted();
        }
        catch (Exception ex)
        {
            record.LastFailureKind = RebirthNpcCommandFailureKind.UnexpectedException;
            Complete(record, RebirthNpcCommandStatus.Failed, ex.GetType().Name + ": " + ex.Message);
            RebirthNpcCommandDiagnostics.RecordFailed();
            Log.Warning("[REBIRTH NPC] command " + record.Request.CommandId + " failed: " + ex);
        }
    }

    private static string Validate(EntityRebirthNPC npc, RebirthNpcCommandRequest request)
    {
        RebirthNpcCommandFailureKind ignored;
        return Validate(npc, request, out ignored);
    }

    private static string Validate(EntityRebirthNPC npc, RebirthNpcCommandRequest request,
        out RebirthNpcCommandFailureKind failureKind)
    {
        failureKind = RebirthNpcCommandFailureKind.PermanentValidation;
        RebirthNpcRuntimeState state = npc.RebirthRuntimeState;
        RebirthNpcProfile profile = npc.RebirthProfile;
        if (state.Presence == RebirthNpcPresenceState.Removed)
            return "Target NPC has been permanently removed.";
        if (state.Presence != RebirthNpcPresenceState.Active && state.Presence != RebirthNpcPresenceState.Suspended)
        {
            if (state.Presence == RebirthNpcPresenceState.InVehicleTransit ||
                state.Presence == RebirthNpcPresenceState.MissionAway ||
                state.Presence == RebirthNpcPresenceState.AwaitingRespawn ||
                state.Presence == RebirthNpcPresenceState.UnloadedPersistent)
                failureKind = RebirthNpcCommandFailureKind.TransientPresence;
            return "Target NPC cannot accept commands while presence is " + state.Presence + ".";
        }
        if (!profile.Has(RebirthNpcCapabilities.Orders))
            return "Target profile does not support orders.";
        if (request.ExpectedRevision != 0U && request.ExpectedRevision != state.Revision)
            return "NPC state changed before command validation; expected revision " + request.ExpectedRevision +
                   " but authoritative revision is " + state.Revision + ".";
        if (request.Origin == RebirthNpcCommandOrigin.AuthenticatedClient)
        {
            if (state.OwnershipKind != RebirthNpcOwnershipKind.Player)
                return "Remote commands require direct player ownership.";
            if (string.IsNullOrEmpty(state.OwnerId) ||
                !string.Equals(state.OwnerId, request.IssuerId, StringComparison.OrdinalIgnoreCase))
                return "Authenticated player does not own the target NPC.";
        }
        else if (state.OwnershipKind != RebirthNpcOwnershipKind.System &&
                 !string.IsNullOrEmpty(state.OwnerId) &&
                 !string.Equals(state.OwnerId, request.IssuerId, StringComparison.OrdinalIgnoreCase))
        {
            return "Issuer does not own or control the target NPC.";
        }
        RebirthNpcOrderState order = NormalizeOrder(request.Kind);
        if (order == RebirthNpcOrderState.Follow && request.SubjectEntityId < 0)
            return "Follow requires an authenticated or trusted subject entity.";
        if (order == RebirthNpcOrderState.Travel && !profile.Has(RebirthNpcCapabilities.Travel))
            return "Target profile does not support travel commands.";
        if (order == RebirthNpcOrderState.Mission && !profile.Has(RebirthNpcCapabilities.Mission))
            return "Target profile does not support mission commands.";
        if ((order == RebirthNpcOrderState.Guard || order == RebirthNpcOrderState.Patrol ||
             order == RebirthNpcOrderState.Work || order == RebirthNpcOrderState.Travel ||
             order == RebirthNpcOrderState.Mission) && !request.HasTargetPosition)
            return order + " requires a target position.";
        return string.Empty;
    }


    private static bool TryScheduleRetry(RebirthNpcCommandRecord record,
        RebirthNpcCommandFailureKind failureKind, string reason)
    {
        int maxAttempts = RebirthNpcCommandPolicy.GetMaxAttempts(record.Request.Kind);
        if (record.AttemptCount >= maxAttempts) return false;

        int delayMilliseconds = RebirthNpcCommandPolicy.GetRetryDelayMilliseconds(record.AttemptCount);
        long nextAttempt = DateTime.UtcNow.AddMilliseconds(delayMilliseconds).Ticks;
        if (nextAttempt >= record.Request.ExpiresUtcTicks) return false;

        lock (Sync)
        {
            record.ScheduleRetry(failureKind, reason + " Retry " + (record.AttemptCount + 1) +
                " of " + maxAttempts + " in " + delayMilliseconds + " ms.", nextAttempt);
            ScheduleRetryTokenLocked(record.Request.CommandId, nextAttempt);
            pendingCount++;
        }
        RebirthNpcCommandDiagnostics.RecordRetryScheduled();
        return true;
    }

    private static RebirthNpcOrderState NormalizeOrder(RebirthNpcCommandKind kind)
    {
        switch (kind)
        {
            case RebirthNpcCommandKind.Follow: return RebirthNpcOrderState.Follow;
            case RebirthNpcCommandKind.Stay: return RebirthNpcOrderState.Stay;
            case RebirthNpcCommandKind.Guard: return RebirthNpcOrderState.Guard;
            case RebirthNpcCommandKind.Patrol: return RebirthNpcOrderState.Patrol;
            case RebirthNpcCommandKind.Work: return RebirthNpcOrderState.Work;
            case RebirthNpcCommandKind.Travel: return RebirthNpcOrderState.Travel;
            case RebirthNpcCommandKind.Mission: return RebirthNpcOrderState.Mission;
            default: return RebirthNpcOrderState.None;
        }
    }

    private static void Complete(RebirthNpcCommandRecord record, RebirthNpcCommandStatus status, string detail)
    {
        bool changed;
        lock (Sync) changed = TerminalizeLocked(record, status, detail);
        if (changed) RebirthNpcRemoteCommandService.OnTerminal(record);
    }

    private static void StoreTerminal(RebirthNpcCommandRecord record)
    {
        lock (Sync)
        {
            Records[record.Request.CommandId] = record;
            HistoryOrder.Enqueue(record.Request.CommandId);
            TrimHistory();
        }
    }

    private static bool TerminalizeLocked(RebirthNpcCommandRecord record,
        RebirthNpcCommandStatus status, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if (record!=null && RebirthNpcWorkReleaseGate.Hold(record.Request.Kind)) return false;

        if (record == null || IsTerminal(record.Status)) return false;
        record.SetStatus(status, detail);
        Records[record.Request.CommandId] = record;
        HistoryOrder.Enqueue(record.Request.CommandId);
        TrimHistory();
        return true;
    }

    private static bool IsTerminal(RebirthNpcCommandStatus status)
    {
        return status == RebirthNpcCommandStatus.Completed ||
            status == RebirthNpcCommandStatus.Rejected ||
            status == RebirthNpcCommandStatus.Cancelled ||
            status == RebirthNpcCommandStatus.Failed ||
            status == RebirthNpcCommandStatus.Expired ||
            status == RebirthNpcCommandStatus.Superseded;
    }

    private static void TrimHistory()
    {
        int scanBudget = HistoryOrder.Count;
        while (scanBudget-- > 0 && HistoryOrder.Count > MaxHistoryRecords)
        {
            ulong oldest = HistoryOrder.Dequeue();
            RebirthNpcCommandRecord record;
            if (Records.TryGetValue(oldest, out record) && RebirthNpcWorkReleaseGate.Hold(record.Request.Kind)) { HistoryOrder.Enqueue(oldest); continue; }
            if (Records.TryGetValue(oldest, out record) &&
                record.Status != RebirthNpcCommandStatus.Queued &&
                record.Status != RebirthNpcCommandStatus.Executing)
                Records.Remove(oldest);
        }
    }

    private static bool IsServer()
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return manager != null && manager.IsServer;
    }
}

public static class RebirthNpcCommandPolicy
{
    public static RebirthNpcCommandPriority GetPriority(RebirthNpcCommandKind kind)
    {
        switch (kind)
        {
            case RebirthNpcCommandKind.Clear: return RebirthNpcCommandPriority.Critical;
            case RebirthNpcCommandKind.Stay:
            case RebirthNpcCommandKind.Follow:
            case RebirthNpcCommandKind.Guard:
                return RebirthNpcCommandPriority.High;
            case RebirthNpcCommandKind.Patrol:
            case RebirthNpcCommandKind.Work:
                return RebirthNpcCommandPriority.Normal;
            default:
                return RebirthNpcCommandPriority.Low;
        }
    }

    public static string GetReport()
    {
        StringBuilder builder = new StringBuilder();
        foreach (RebirthNpcCommandKind kind in Enum.GetValues(typeof(RebirthNpcCommandKind)))
        {
            builder.Append("  ").Append(kind)
                .Append(" priority=").Append(GetPriority(kind))
                .Append(" lifetimeSeconds=").Append(GetLifetimeSeconds(kind))
                .Append(" maxAttempts=").Append(GetMaxAttempts(kind)).AppendLine();
        }
        return "[REBIRTH NPC] command arbitration policy\n" + builder.ToString().TrimEnd();
    }

    public static int GetMaxAttempts(RebirthNpcCommandKind kind)
    {
        switch (kind)
        {
            case RebirthNpcCommandKind.Clear:
            case RebirthNpcCommandKind.Stay:
                return 1;
            case RebirthNpcCommandKind.Follow:
            case RebirthNpcCommandKind.Guard:
                return 2;
            default:
                return 3;
        }
    }

    public static int GetRetryDelayMilliseconds(int completedAttempts)
    {
        switch (completedAttempts)
        {
            case 0: return 250;
            case 1: return 1000;
            default: return 3000;
        }
    }

    public static int GetLifetimeSeconds(RebirthNpcCommandKind kind)
    {
        switch (kind)
        {
            case RebirthNpcCommandKind.Clear:
            case RebirthNpcCommandKind.Stay:
            case RebirthNpcCommandKind.Follow:
                return 10;
            case RebirthNpcCommandKind.Guard:
            case RebirthNpcCommandKind.Patrol:
            case RebirthNpcCommandKind.Work:
                return 30;
            default:
                return 60;
        }
    }
}

public static class RebirthNpcCommandDiagnostics
{
    private static long submitted, completed, rejected, cancelled, failed, expired, superseded, retriesScheduled;
    internal static void RecordSubmitted() { submitted++; }
    internal static void RecordCompleted() { completed++; }
    internal static void RecordRejected() { rejected++; }
    internal static void RecordCancelled() { cancelled++; }
    internal static void RecordFailed() { failed++; }
    internal static void RecordExpired() { expired++; }
    internal static void RecordSuperseded() { superseded++; }
    internal static void RecordRetryScheduled() { retriesScheduled++; }

    public static string GetReport()
    {
        RebirthNpcCommandRecord[] records = RebirthNpcCommandGateway.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] commands tracked=").Append(records.Length)
            .Append(" submitted=").Append(submitted)
            .Append(" completed=").Append(completed)
            .Append(" rejected=").Append(rejected)
            .Append(" cancelled=").Append(cancelled)
            .Append(" failed=").Append(failed)
            .Append(" expired=").Append(expired)
            .Append(" superseded=").Append(superseded)
            .Append(" retriesScheduled=").Append(retriesScheduled).AppendLine();
        for (int i = Math.Max(0, records.Length - 25); i < records.Length; i++)
        {
            RebirthNpcCommandRecord r = records[i];
            builder.Append("  id=").Append(r.Request.CommandId)
                .Append(" entity=").Append(r.Request.TargetEntityId)
                .Append(" kind=").Append(r.Request.Kind)
                .Append(" origin=").Append(r.Request.Origin)
                .Append(" priority=").Append(r.Request.Priority)
                .Append(" status=").Append(r.Status)
                .Append(" attempts=").Append(r.AttemptCount)
                .Append(" failure=").Append(r.LastFailureKind)
                .Append(" detail=").Append(r.Detail).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}

[Preserve]
public sealed class RebirthNpcCommandModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        RebirthNpcStationaryActivityRuntime.RegisterControllers();
        RebirthNpcFollowRuntime.Register();
        RebirthNpcGuardNavigationRuntime.Register();
        RebirthNpcPatrolRuntime.Register();
        if(RebirthNpcWorkReleaseGate.Enabled) RebirthNpcWorkRuntime.Register();
        if(RebirthNpcWorkReleaseGate.Enabled) RebirthNpcConcreteWorkRuntime.Register();
        RebirthNpcSettlementOperations.Register();
        RebirthNpcTravelRuntime.Register();
        RebirthNpcMissionRuntime.Register();
        ModEvents.GameUpdate.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        RebirthNpcActivityRegistry.QuarantineForeignWorkIndexes();
        RebirthNpcActivityRecoveryRegistry.QuarantineForeignWorkIndexes();
        RebirthNpcCommandGateway.Tick();
        RebirthNpcActivityRegistry.Tick();
        RebirthNpcActivityRecoveryRegistry.Tick();
        RebirthNpcActivityDispatcher.Tick();
        RebirthNpcStationaryActivityRuntime.Tick();
        RebirthNpcFollowRuntime.Tick();
        RebirthNpcGuardNavigationRuntime.Tick();
        RebirthNpcPatrolRuntime.Tick();
        // Concrete work stages owned intents; lease-path migration remains pending. EAIRebirthWork is inactive.
        if(RebirthNpcWorkReleaseGate.Enabled) RebirthNpcWorkRuntime.Tick();
        if(RebirthNpcWorkReleaseGate.Enabled) RebirthNpcConcreteWorkRuntime.Tick();
        if(RebirthNpcWorkReleaseGate.Enabled) RebirthNpcEmbodiedWorkOwner.Maintenance();
        RebirthNpcTravelRuntime.Tick();
        RebirthNpcMissionRuntime.Tick();
        RebirthNpcExecutionPersistenceStore.Tick();
        RebirthNpcInventoryPersistenceStore.Tick();
        RebirthNpcEquipmentPersistenceStore.Tick();
        RebirthNpcDecisionEngine.Tick();
        RebirthNpcSocialService.Tick();
        RebirthNpcSettlementPersistenceStore.Tick();
        RebirthNpcSettlementSimulation.Tick();
    }
}
