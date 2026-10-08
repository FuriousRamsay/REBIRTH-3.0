using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthNpcActivityStatus : byte
{
    Running = 0,
    Completed = 1,
    Failed = 2,
    TimedOut = 3,
    Interrupted = 4,
    Released = 5,
    RecoveryPending = 6,
    Abandoned = 7,
    Yielded = 8
}

public sealed class RebirthNpcActivityRecord
{
    public ulong ActivityId { get; internal set; }
    public ulong LeaseId { get; internal set; }
    public int TargetEntityId { get; internal set; }
    public string ControllerId { get; internal set; }
    public RebirthNpcOrderState Order { get; internal set; }
    public uint AcceptedRevision { get; internal set; }
    public RebirthNpcActivityStatus Status { get; internal set; }
    public string Detail { get; internal set; }
    public long StartedUtcTicks { get; internal set; }
    public long LastHeartbeatUtcTicks { get; internal set; }
    public long HeartbeatDeadlineUtcTicks { get; internal set; }
    public long UpdatedUtcTicks { get; internal set; }

    internal RebirthNpcActivityRecord Clone()
    {
        return (RebirthNpcActivityRecord)MemberwiseClone();
    }
}

public readonly struct RebirthNpcActivityStartResult
{
    public readonly bool Succeeded;
    public readonly ulong ActivityId;
    public readonly string Error;

    private RebirthNpcActivityStartResult(bool succeeded, ulong activityId, string error)
    {
        Succeeded = succeeded;
        ActivityId = activityId;
        Error = error ?? string.Empty;
    }

    public static RebirthNpcActivityStartResult Success(ulong activityId) =>
        new RebirthNpcActivityStartResult(true, activityId, string.Empty);

    public static RebirthNpcActivityStartResult Failure(string error) =>
        new RebirthNpcActivityStartResult(false, 0UL, error);
}

public static class RebirthNpcActivityRegistry
{
    private const int MaxHistoryRecords = 256;
    private const int MinimumHeartbeatSeconds = 2;
    private const int MaximumHeartbeatSeconds = 60;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcActivityRecord> ActiveByEntity =
        new Dictionary<int, RebirthNpcActivityRecord>();
    private static readonly Dictionary<ulong, RebirthNpcActivityRecord> ByActivityId =
        new Dictionary<ulong, RebirthNpcActivityRecord>();
    private static readonly Queue<ulong> HistoryOrder = new Queue<ulong>();
    private static ulong nextActivityId = 1UL;
    private static long nextWatchdogUtcTicks;

    // Detach only a numeric lookup; original ByActivityId custody is retained.
    private static void QuarantineForeignWorkIndex(int entityId)
    {
        RebirthNpcActivityRecord original;
        lock (Sync) { if (!ActiveByEntity.TryGetValue(entityId, out original) || !RebirthNpcWorkReleaseGate.Hold(original.Order)) return; }
        // No Activity lock is held while consulting original lease identity.
        if (RebirthNpcExecutionLeaseRegistry.IsCurrentWorldWorkLeaseOwner(entityId, original.LeaseId)) return;
        lock (Sync) { RebirthNpcActivityRecord current; if (ActiveByEntity.TryGetValue(entityId, out current) && ReferenceEquals(current, original)) ActiveByEntity.Remove(entityId); }
    }
    internal static void QuarantineForeignWorkIndexes()
    {
        int[] ids;
        lock (Sync) { ids = new int[ActiveByEntity.Count]; ActiveByEntity.Keys.CopyTo(ids, 0); }
        foreach (int id in ids) QuarantineForeignWorkIndex(id);
    }
    public static RebirthNpcActivityStartResult TryStart(int entityId, ulong leaseId,
        string controllerId, int heartbeatTimeoutSeconds)
    {
        QuarantineForeignWorkIndex(entityId);
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return RebirthNpcActivityStartResult.Failure(RebirthNpcWorkReleaseGate.Detail);

        if (entityId <= 0) return RebirthNpcActivityStartResult.Failure("Entity id is invalid.");
        if (leaseId == 0UL) return RebirthNpcActivityStartResult.Failure("Lease id is required.");
        if (string.IsNullOrWhiteSpace(controllerId))
            return RebirthNpcActivityStartResult.Failure("Controller id is required.");

        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(entityId, out lease) || lease.LeaseId != leaseId)
            return RebirthNpcActivityStartResult.Failure("The execution lease is no longer active.");

        if (RebirthNpcWorkReleaseGate.Hold(lease.Order)) return RebirthNpcActivityStartResult.Failure(RebirthNpcWorkReleaseGate.Detail);

        int timeout = Math.Max(MinimumHeartbeatSeconds,
            Math.Min(MaximumHeartbeatSeconds, heartbeatTimeoutSeconds));
        lock (Sync)
        {
            RebirthNpcActivityRecord existing;
            if (ActiveByEntity.TryGetValue(entityId, out existing))
            {
                if (RebirthNpcWorkReleaseGate.Hold(existing.Order)) return RebirthNpcActivityStartResult.Failure(RebirthNpcWorkReleaseGate.Detail);
                if (existing.LeaseId == leaseId &&
                    string.Equals(existing.ControllerId, controllerId, StringComparison.Ordinal))
                    return RebirthNpcActivityStartResult.Success(existing.ActivityId);
                TerminalizeLocked(existing, RebirthNpcActivityStatus.Interrupted,
                    "Replaced by activity controller " + controllerId + " for lease " + leaseId + ".");
                RebirthNpcActivityDiagnostics.RecordInterrupted();
            }

            ulong activityId = nextActivityId++;
            if (activityId == 0UL) activityId = nextActivityId++;
           long now = DateTime.UtcNow.Ticks;
            RebirthNpcActivityRecord record = new RebirthNpcActivityRecord
            {
                ActivityId = activityId,
                LeaseId = leaseId,
                TargetEntityId = entityId,
                ControllerId = controllerId.Trim(),
                Order = lease.Order,
                AcceptedRevision = lease.AcceptedRevision,
                Status = RebirthNpcActivityStatus.Running,
                Detail = "Execution controller attached to active lease.",
                StartedUtcTicks = now,
                LastHeartbeatUtcTicks = now,
                HeartbeatDeadlineUtcTicks = now + TimeSpan.FromSeconds(timeout).Ticks,
                UpdatedUtcTicks = now
            };
            ActiveByEntity[entityId] = record;
            ByActivityId[activityId] = record;
            RebirthNpcActivityDiagnostics.RecordStarted();
            RebirthNpcWorldIntegrationService.OnActivityTransition(entityId, lease.Order);
            return RebirthNpcActivityStartResult.Success(activityId);
        }
    }

    public static bool TryHeartbeat(int entityId, ulong activityId, ulong leaseId,
        int heartbeatTimeoutSeconds, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return false;

        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(entityId, out lease) || lease.LeaseId != leaseId)
            return false;

        int timeout = Math.Max(MinimumHeartbeatSeconds,
            Math.Min(MaximumHeartbeatSeconds, heartbeatTimeoutSeconds));
        lock (Sync)
        {
            RebirthNpcActivityRecord record;
            if (!ActiveByEntity.TryGetValue(entityId, out record) ||
                record.ActivityId != activityId || record.LeaseId != leaseId ||
                record.Status != RebirthNpcActivityStatus.Running)
                return false;
            if(RebirthNpcWorkReleaseGate.Hold(record.Order))return false;
            long now = DateTime.UtcNow.Ticks;
            record.LastHeartbeatUtcTicks = now;
            record.HeartbeatDeadlineUtcTicks = now + TimeSpan.FromSeconds(timeout).Ticks;
            record.UpdatedUtcTicks = now;
            if (!string.IsNullOrEmpty(detail)) record.Detail = detail;
            RebirthNpcActivityDiagnostics.RecordHeartbeat();
            return true;
        }
    }

    public static bool TryComplete(int entityId, ulong activityId, ulong leaseId,
        uint observedRevision, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return false;

        lock (Sync)
        {
            RebirthNpcActivityRecord record;
            if (!TryResolveRunningLocked(entityId, activityId, leaseId, out record)) return false;
            if(RebirthNpcWorkReleaseGate.Hold(record.Order))return false;
            if (!RebirthNpcExecutionLeaseRegistry.TryComplete(entityId, leaseId, observedRevision,
                    detail ?? "Activity controller reported completion."))
                return false;
            TerminalizeLocked(record, RebirthNpcActivityStatus.Completed,
                detail ?? "Activity controller reported completion.");
            RebirthNpcActivityRecoveryRegistry.ReleaseForLease(leaseId);
            RebirthNpcActivityDiagnostics.RecordCompleted();
            return true;
        }
    }

    public static bool TryFail(int entityId, ulong activityId, ulong leaseId,
        uint observedRevision, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return false;

        return TryReportResult(entityId, activityId, leaseId, observedRevision,
            RebirthNpcActivityOutcome.ControllerFault, detail);
    }

    public static bool TryReportResult(int entityId, ulong activityId, ulong leaseId,
        uint observedRevision, RebirthNpcActivityOutcome outcome, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return false;

        lock (Sync)
        {
            RebirthNpcActivityRecord record;
            if (!TryResolveRunningLocked(entityId, activityId, leaseId, out record)) return false;
            if(RebirthNpcWorkReleaseGate.Hold(record.Order))return false;
            string resultDetail = detail ?? ("Activity controller reported " + outcome + ".");
            switch (RebirthNpcActivityResultPolicy.GetDisposition(outcome))
            {
                case RebirthNpcActivityDisposition.CompleteLease:
                    if (!RebirthNpcExecutionLeaseRegistry.TryComplete(entityId, leaseId,
                            observedRevision, resultDetail)) return false;
                    TerminalizeLocked(record, RebirthNpcActivityStatus.Completed, resultDetail);
                    RebirthNpcActivityRecoveryRegistry.ReleaseForLease(leaseId);
                    RebirthNpcActivityDiagnostics.RecordCompleted();
                    return true;
                case RebirthNpcActivityDisposition.ScheduleRecovery:
                    ulong recoveryId;
                    if (!RebirthNpcActivityRecoveryRegistry.TrySchedule(record, outcome,
                            resultDetail, out recoveryId))
                    {
                        TerminalizeLocked(record, RebirthNpcActivityStatus.Failed,
                            "Recovery could not be scheduled: " + resultDetail);
                        RebirthNpcActivityDiagnostics.RecordFailed();
                        return true;
                    }
                    TerminalizeLocked(record, RebirthNpcActivityStatus.RecoveryPending,
                        "Recovery " + recoveryId + " scheduled: " + resultDetail);
                    RebirthNpcActivityDiagnostics.RecordRecoveryPending();
                    return true;
                case RebirthNpcActivityDisposition.ReleaseLease:
                    if (!RebirthNpcExecutionLeaseRegistry.TryRelease(entityId, leaseId,
                            observedRevision, resultDetail)) return false;
                    TerminalizeLocked(record, RebirthNpcActivityStatus.Abandoned, resultDetail);
                    RebirthNpcActivityRecoveryRegistry.ReleaseForLease(leaseId);
                    RebirthNpcActivityDiagnostics.RecordAbandoned();
                    return true;
                default:
                    if (!RebirthNpcExecutionLeaseRegistry.TryFail(entityId, leaseId,
                            observedRevision, resultDetail)) return false;
                    TerminalizeLocked(record, RebirthNpcActivityStatus.Failed, resultDetail);
                    RebirthNpcActivityRecoveryRegistry.ReleaseForLease(leaseId);
                    RebirthNpcActivityDiagnostics.RecordFailed();
                    return true;
            }
        }
    }


    public static bool TryYield(int entityId, ulong activityId, ulong leaseId,
        string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.HoldEntity(entityId))return false;

        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(entityId, out lease) ||
            lease.LeaseId != leaseId) return false;
        lock (Sync)
        {
            RebirthNpcActivityRecord record;
            if (!TryResolveRunningLocked(entityId, activityId, leaseId, out record)) return false;
            if(RebirthNpcWorkReleaseGate.Hold(record.Order))return false;
            TerminalizeLocked(record, RebirthNpcActivityStatus.Yielded,
                detail ?? "Activity yielded the active execution lease.");
            RebirthNpcActivityDiagnostics.RecordYielded();
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
            RebirthNpcActivityRecord record;
            if (!ActiveByEntity.TryGetValue(entityId, out record)) return;
            if(RebirthNpcWorkReleaseGate.Hold(record.Order))return;
            TerminalizeLocked(record, RebirthNpcActivityStatus.Released,
                reason ?? "Target left the active runtime.");
            RebirthNpcActivityRecoveryRegistry.ReleaseForLease(record.LeaseId);
            RebirthNpcActivityDiagnostics.RecordReleased();
        }
    }

    public static void Tick()
    {
        long now = DateTime.UtcNow.Ticks;
        if (now < nextWatchdogUtcTicks) return;
        nextWatchdogUtcTicks = now + TimeSpan.FromSeconds(1).Ticks;

        List<RebirthNpcActivityRecord> timedOut = new List<RebirthNpcActivityRecord>();
        lock (Sync)
        {
            foreach (RebirthNpcActivityRecord record in ActiveByEntity.Values)
            {
                if(RebirthNpcWorkReleaseGate.Hold(record.Order))continue;
                RebirthNpcExecutionLease lease;
                if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(record.TargetEntityId, out lease) ||
                    lease.LeaseId != record.LeaseId)
                {
                    timedOut.Add(record);
                    continue;
                }
                if (now > record.HeartbeatDeadlineUtcTicks) timedOut.Add(record);
            }

            for (int i = 0; i < timedOut.Count; i++)
            {
                RebirthNpcActivityRecord record = timedOut[i];
                if(RebirthNpcWorkReleaseGate.Hold(record.Order))continue;
                RebirthNpcExecutionLease lease;
                bool leaseStillActive = RebirthNpcExecutionLeaseRegistry.TryGetActive(
                    record.TargetEntityId, out lease) && lease.LeaseId == record.LeaseId;
                if (leaseStillActive)
                {
                    RebirthNpcExecutionLeaseRegistry.TryFail(record.TargetEntityId, record.LeaseId,
                        record.AcceptedRevision, "Activity heartbeat watchdog expired.");
                    TerminalizeLocked(record, RebirthNpcActivityStatus.TimedOut,
                        "Activity heartbeat watchdog expired.");
                    RebirthNpcActivityRecoveryRegistry.ReleaseForLease(record.LeaseId);
                    RebirthNpcActivityDiagnostics.RecordTimedOut();
                }
                else
                {
                    TerminalizeLocked(record, RebirthNpcActivityStatus.Interrupted,
                        "Execution lease ended before activity completion.");
                    RebirthNpcActivityRecoveryRegistry.ReleaseForLease(record.LeaseId);
                    RebirthNpcActivityDiagnostics.RecordInterrupted();
                }
            }
        }
    }

    public static bool HasRunningActivity(int entityId, ulong leaseId)
    {
        QuarantineForeignWorkIndex(entityId);
        lock (Sync)
        {
            RebirthNpcActivityRecord record;
            return ActiveByEntity.TryGetValue(entityId, out record) &&
                   record.LeaseId == leaseId &&
                   record.Status == RebirthNpcActivityStatus.Running;
        }
    }

    public static RebirthNpcActivityRecord[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcActivityRecord[] result = new RebirthNpcActivityRecord[ByActivityId.Count];
            int index = 0;
            foreach (RebirthNpcActivityRecord record in ByActivityId.Values)
                result[index++] = record.Clone();
            Array.Sort(result, (left, right) => left.ActivityId.CompareTo(right.ActivityId));
            return result;
        }
    }

    private static bool TryResolveRunningLocked(int entityId, ulong activityId, ulong leaseId,
        out RebirthNpcActivityRecord record)
    {
        if (!ActiveByEntity.TryGetValue(entityId, out record)) return false;
        return record.ActivityId == activityId && record.LeaseId == leaseId &&
               record.Status == RebirthNpcActivityStatus.Running;
    }

    private static void TerminalizeLocked(RebirthNpcActivityRecord record,
        RebirthNpcActivityStatus status, string detail)
    {
        // Release hold: preserve original work custody; no native effects.
        if(RebirthNpcWorkReleaseGate.Hold(record.Order))return;

        record.Status = status;
        record.Detail = detail ?? string.Empty;
        record.UpdatedUtcTicks = DateTime.UtcNow.Ticks;
        ActiveByEntity.Remove(record.TargetEntityId);
        HistoryOrder.Enqueue(record.ActivityId);
        TrimHistoryLocked();
    }

    private static void TrimHistoryLocked()
    {
        int scanBudget = HistoryOrder.Count;
        while (scanBudget-- > 0 && HistoryOrder.Count > MaxHistoryRecords)
        {
            ulong oldest = HistoryOrder.Dequeue();
            RebirthNpcActivityRecord record;
            if (ByActivityId.TryGetValue(oldest, out record) && RebirthNpcWorkReleaseGate.Hold(record.Order)) { HistoryOrder.Enqueue(oldest); continue; }
            if (ByActivityId.TryGetValue(oldest, out record) &&
                record.Status != RebirthNpcActivityStatus.Running)
                ByActivityId.Remove(oldest);
        }
    }
}

public static class RebirthNpcActivityDiagnostics
{
    private static long started, heartbeats, completed, failed, timedOut, interrupted, released, recoveryPending, abandoned, yielded;
    internal static void RecordStarted() { started++; }
    internal static void RecordHeartbeat() { heartbeats++; }
    internal static void RecordCompleted() { completed++; }
    internal static void RecordFailed() { failed++; }
    internal static void RecordTimedOut() { timedOut++; }
    internal static void RecordInterrupted() { interrupted++; }
    internal static void RecordReleased() { released++; }
    internal static void RecordRecoveryPending() { recoveryPending++; }
    internal static void RecordAbandoned() { abandoned++; }
    internal static void RecordYielded() { yielded++; }

    public static string GetReport()
    {
        RebirthNpcActivityRecord[] records = RebirthNpcActivityRegistry.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] activities tracked=").Append(records.Length)
            .Append(" started=").Append(started)
            .Append(" heartbeats=").Append(heartbeats)
            .Append(" completed=").Append(completed)
            .Append(" failed=").Append(failed)
            .Append(" timedOut=").Append(timedOut)
            .Append(" interrupted=").Append(interrupted)
            .Append(" released=").Append(released)
            .Append(" recoveryPending=").Append(recoveryPending)
            .Append(" abandoned=").Append(abandoned)
            .Append(" yielded=").Append(yielded).AppendLine();
        for (int i = Math.Max(0, records.Length - 25); i < records.Length; i++)
        {
            RebirthNpcActivityRecord record = records[i];
            builder.Append("  activity=").Append(record.ActivityId)
                .Append(" lease=").Append(record.LeaseId)
                .Append(" entity=").Append(record.TargetEntityId)
                .Append(" controller=").Append(record.ControllerId)
                .Append(" order=").Append(record.Order)
                .Append(" revision=").Append(record.AcceptedRevision)
                .Append(" status=").Append(record.Status)
                .Append(" detail=").Append(record.Detail).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
