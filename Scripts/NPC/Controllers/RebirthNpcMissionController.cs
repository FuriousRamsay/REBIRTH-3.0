using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthNpcMissionStepResult : byte
{
    Running = 0,
    Succeeded = 1,
    TransientBlocked = 2,
    TransientTargetUnavailable = 3,
    PermanentUnreachable = 4,
    PermanentInvalid = 5,
    Abandoned = 6
}

public readonly struct RebirthNpcMissionContext
{
    public readonly EntityRebirthNPC Npc;
    public readonly RebirthNpcExecutionLease Lease;
    public readonly ulong ActivityId;
    public readonly bool IsRecovery;

    public RebirthNpcMissionContext(EntityRebirthNPC npc, RebirthNpcExecutionLease lease,
        ulong activityId, bool isRecovery)
    {
        Npc = npc;
        Lease = lease;
        ActivityId = activityId;
        IsRecovery = isRecovery;
    }
}

public interface IRebirthNpcMissionExecutor
{
    string ExecutorId { get; }
    int Priority { get; }
    bool CanExecute(EntityRebirthNPC npc, RebirthNpcExecutionLease lease);
    RebirthNpcMissionStepResult Begin(RebirthNpcMissionContext context, out string detail);
    RebirthNpcMissionStepResult Tick(RebirthNpcMissionContext context, out string detail);
    void End(RebirthNpcMissionContext context, string reason);
}

public static class RebirthNpcMissionExecutorRegistry
{
    private static readonly object Sync = new object();
    private static readonly List<IRebirthNpcMissionExecutor> Executors =
        new List<IRebirthNpcMissionExecutor>();

    public static bool Register(IRebirthNpcMissionExecutor executor)
    {
        if (executor == null || string.IsNullOrWhiteSpace(executor.ExecutorId)) return false;
        lock (Sync)
        {
            for (int i = 0; i < Executors.Count; i++)
                if (string.Equals(Executors[i].ExecutorId, executor.ExecutorId,
                        StringComparison.Ordinal))
                    return false;
            Executors.Add(executor);
            Executors.Sort((left, right) =>
            {
                int priority = right.Priority.CompareTo(left.Priority);
                return priority != 0 ? priority : string.Compare(left.ExecutorId,
                    right.ExecutorId, StringComparison.Ordinal);
            });
            return true;
        }
    }

    public static IRebirthNpcMissionExecutor Resolve(EntityRebirthNPC npc,
        RebirthNpcExecutionLease lease)
    {
        lock (Sync)
        {
            for (int i = 0; i < Executors.Count; i++)
            {
                try
                {
                    if (Executors[i].CanExecute(npc, lease)) return Executors[i];
                }
                catch (Exception ex)
                {
                    Log.Warning("[REBIRTH NPC] Mission executor " + Executors[i].ExecutorId +
                        " CanExecute failed: " + ex.Message);
                    RebirthNpcMissionDiagnostics.RecordExecutorFault();
                }
            }
        }
        return null;
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[REBIRTH NPC] mission executors registered=")
                .Append(Executors.Count).AppendLine();
            for (int i = 0; i < Executors.Count; i++)
                builder.Append("  id=").Append(Executors[i].ExecutorId)
                    .Append(" priority=").Append(Executors[i].Priority)
                    .Append(" type=").Append(Executors[i].GetType().FullName).AppendLine();
            return builder.ToString().TrimEnd();
        }
    }
}

public sealed class RebirthNpcMissionActivityController : IRebirthNpcActivityController
{
    public string ControllerId => "rebirth.execution.mission";
    public int Priority => 150;
    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease) =>
        npc != null && lease != null && lease.Order == RebirthNpcOrderState.Mission;
    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 6;
    public void Start(RebirthNpcControllerContext context) => RebirthNpcMissionRuntime.Attach(context);
    public void Recover(RebirthNpcControllerContext context) => RebirthNpcMissionRuntime.Attach(context);
}

public static class RebirthNpcMissionRuntime
{
    private const int HeartbeatTimeoutSeconds = 6;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcMissionSession> Sessions =
        new Dictionary<int, RebirthNpcMissionSession>();
    private static long nextTickUtcTicks;

    public static void Register()
    {
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcMissionActivityController());
    }

    public static void Attach(RebirthNpcControllerContext context)
    {
        IRebirthNpcMissionExecutor executor =
            RebirthNpcMissionExecutorRegistry.Resolve(context.Npc, context.Lease);
        if (executor == null)
        {
            RebirthNpcMissionDiagnostics.RecordMissingExecutor();
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.PermanentInvalid,
                "No compatible Mission executor is registered for command " +
                context.Lease.CommandId + ".");
            return;
        }

        RebirthNpcMissionSession session = new RebirthNpcMissionSession
        {
            EntityId = context.Npc.entityId,
            LeaseId = context.Lease.LeaseId,
            ActivityId = context.ActivityId,
            AcceptedRevision = context.Lease.AcceptedRevision,
            IsRecovery = context.IsRecovery,
            CapturedLease = context.Lease.Clone(),
            Executor = executor
        };
        Store(session);
        RebirthNpcMissionDiagnostics.RecordAttached(context.IsRecovery);

        string detail;
        RebirthNpcMissionStepResult result;
        try
        {
            result = executor.Begin(new RebirthNpcMissionContext(context.Npc, context.Lease,
                context.ActivityId, context.IsRecovery), out detail);
        }
        catch (Exception ex)
        {
            RebirthNpcMissionDiagnostics.RecordExecutorFault();
            Report(session, RebirthNpcActivityOutcome.ControllerFault,
                "Mission executor " + executor.ExecutorId + " Begin failed: " + ex.Message);
            return;
        }
        ApplyResult(session, result, detail, "Mission executor began.");
    }

    private static void Store(RebirthNpcMissionSession session)
    {
        lock (Sync) Sessions[session.EntityId] = session;
    }

    public static void Tick()
    {
        long now = DateTime.UtcNow.Ticks;
        if (now < nextTickUtcTicks) return;
        nextTickUtcTicks = now + TimeSpan.FromSeconds(1).Ticks;
        RebirthNpcMissionSession[] snapshot;
        lock (Sync)
        {
            snapshot = new RebirthNpcMissionSession[Sessions.Count];
            Sessions.Values.CopyTo(snapshot, 0);
        }
        for (int i = 0; i < snapshot.Length; i++) TickSession(snapshot[i]);
    }

    private static void TickSession(RebirthNpcMissionSession session)
    {
        EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(session.EntityId) as EntityRebirthNPC;
        if (npc == null) { Remove(session, null, null, "NPC unavailable."); return; }

        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(session.EntityId, out lease) ||
            lease.LeaseId != session.LeaseId || lease.Order != RebirthNpcOrderState.Mission)
        {
            Remove(session, npc, session.CapturedLease, "Mission lease ended.");
            return;
        }

        if (!RebirthNpcActivityRegistry.TryHeartbeat(session.EntityId, session.ActivityId,
                session.LeaseId, HeartbeatTimeoutSeconds,
                "Mission executor " + session.Executor.ExecutorId + " is active."))
        {
            Remove(session, npc, session.CapturedLease, "Mission heartbeat rejected.");
            return;
        }

        string detail;
        RebirthNpcMissionStepResult result;
        try
        {
            result = session.Executor.Tick(new RebirthNpcMissionContext(npc, lease,
                session.ActivityId, session.IsRecovery), out detail);
            RebirthNpcMissionDiagnostics.RecordTick();
        }
        catch (Exception ex)
        {
            RebirthNpcMissionDiagnostics.RecordExecutorFault();
            Report(session, RebirthNpcActivityOutcome.ControllerFault,
                "Mission executor " + session.Executor.ExecutorId + " Tick failed: " + ex.Message);
            return;
        }
        ApplyResult(session, result, detail, "Mission executor remains active.");
    }

    private static void ApplyResult(RebirthNpcMissionSession session,
        RebirthNpcMissionStepResult result, string detail, string runningDetail)
    {
        if (result == RebirthNpcMissionStepResult.Running)
        {
            if (!string.IsNullOrEmpty(detail))
                RebirthNpcActivityRegistry.TryHeartbeat(session.EntityId, session.ActivityId,
                    session.LeaseId, HeartbeatTimeoutSeconds, detail);
            return;
        }

        RebirthNpcActivityOutcome outcome;
        switch (result)
        {
            case RebirthNpcMissionStepResult.Succeeded:
                outcome = RebirthNpcActivityOutcome.Succeeded; break;
            case RebirthNpcMissionStepResult.TransientBlocked:
                outcome = RebirthNpcActivityOutcome.TransientBlocked; break;
            case RebirthNpcMissionStepResult.TransientTargetUnavailable:
                outcome = RebirthNpcActivityOutcome.TransientTargetUnavailable; break;
            case RebirthNpcMissionStepResult.PermanentUnreachable:
                outcome = RebirthNpcActivityOutcome.PermanentUnreachable; break;
            case RebirthNpcMissionStepResult.Abandoned:
                outcome = RebirthNpcActivityOutcome.Abandoned; break;
            default:
                outcome = RebirthNpcActivityOutcome.PermanentInvalid; break;
        }
        Report(session, outcome, string.IsNullOrEmpty(detail) ?
            (runningDetail + " Result=" + result + ".") : detail);
    }

    private static void Report(RebirthNpcMissionSession session,
        RebirthNpcActivityOutcome outcome, string detail)
    {
        EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(session.EntityId) as EntityRebirthNPC;
        Remove(session, npc, session.CapturedLease, detail);
        if (RebirthNpcActivityRegistry.TryReportResult(session.EntityId, session.ActivityId,
                session.LeaseId, session.AcceptedRevision, outcome, detail))
            RebirthNpcMissionDiagnostics.RecordOutcome(outcome);
    }

    private static void Remove(RebirthNpcMissionSession session, EntityRebirthNPC npc,
        RebirthNpcExecutionLease lease, string reason)
    {
        lock (Sync)
        {
            RebirthNpcMissionSession current;
            if (!Sessions.TryGetValue(session.EntityId, out current) ||
                current.ActivityId != session.ActivityId) return;
            Sessions.Remove(session.EntityId);
        }
        try
        {
            if (session.Executor != null && npc != null && lease != null)
                session.Executor.End(new RebirthNpcMissionContext(npc, lease,
                    session.ActivityId, session.IsRecovery), reason ?? string.Empty);
        }
        catch (Exception ex)
        {
            RebirthNpcMissionDiagnostics.RecordExecutorFault();
            Log.Warning("[REBIRTH NPC] Mission executor " + session.Executor.ExecutorId +
                " End failed: " + ex.Message);
        }
    }

    public static RebirthNpcMissionSession[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcMissionSession[] result = new RebirthNpcMissionSession[Sessions.Count];
            Sessions.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => left.EntityId.CompareTo(right.EntityId));
            return result;
        }
    }
}

public sealed class RebirthNpcMissionSession
{
    public int EntityId;
    public ulong LeaseId;
    public ulong ActivityId;
    public uint AcceptedRevision;
    public bool IsRecovery;
    public RebirthNpcExecutionLease CapturedLease;
    public IRebirthNpcMissionExecutor Executor;
}

public static class RebirthNpcMissionDiagnostics
{
    private static long attached, recoveryAttached, ticks, missingExecutor, executorFaults;
    private static long succeeded, transient, permanent, abandoned;

    internal static void RecordAttached(bool recovery)
    {
        attached++;
        if (recovery) recoveryAttached++;
    }
    internal static void RecordTick() { ticks++; }
    internal static void RecordMissingExecutor() { missingExecutor++; }
    internal static void RecordExecutorFault() { executorFaults++; }
    internal static void RecordOutcome(RebirthNpcActivityOutcome outcome)
    {
        if (outcome == RebirthNpcActivityOutcome.Succeeded) succeeded++;
        else if (outcome == RebirthNpcActivityOutcome.TransientBlocked ||
                 outcome == RebirthNpcActivityOutcome.TransientTargetUnavailable) transient++;
        else if (outcome == RebirthNpcActivityOutcome.Abandoned) abandoned++;
        else permanent++;
    }

    public static string GetReport()
    {
        RebirthNpcMissionSession[] sessions = RebirthNpcMissionRuntime.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append(RebirthNpcMissionExecutorRegistry.GetReport()).AppendLine();
        builder.Append("[REBIRTH NPC] mission sessions active=").Append(sessions.Length)
            .Append(" attached=").Append(attached)
            .Append(" recoveryAttached=").Append(recoveryAttached)
            .Append(" ticks=").Append(ticks)
            .Append(" missingExecutor=").Append(missingExecutor)
            .Append(" executorFaults=").Append(executorFaults)
            .Append(" succeeded=").Append(succeeded)
            .Append(" transient=").Append(transient)
            .Append(" permanent=").Append(permanent)
            .Append(" abandoned=").Append(abandoned).AppendLine();
        for (int i = 0; i < sessions.Length; i++)
        {
            RebirthNpcMissionSession session = sessions[i];
            builder.Append("  entity=").Append(session.EntityId)
                .Append(" activity=").Append(session.ActivityId)
                .Append(" lease=").Append(session.LeaseId)
                .Append(" executor=").Append(session.Executor == null ? "<none>" :
                    session.Executor.ExecutorId)
                .Append(" recovery=").Append(session.IsRecovery).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
