using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcPatrolActivityController : IRebirthNpcActivityController
{
    public string ControllerId => "rebirth.navigation.patrol";
    public int Priority => 175;

    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease)
    {
        return npc != null && lease != null && lease.Order == RebirthNpcOrderState.Patrol &&
            lease.HasTargetPosition;
    }

    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 6;
    public void Start(RebirthNpcControllerContext context) => RebirthNpcPatrolRuntime.Attach(context);
    public void Recover(RebirthNpcControllerContext context) => RebirthNpcPatrolRuntime.Attach(context);
}

public static class RebirthNpcPatrolRuntime
{
    private const float ArrivalRadius = 2.5f;
    private const float MinimumLegDistance = 4f;
    private const int HeartbeatTimeoutSeconds = 6;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcPatrolSession> Sessions =
        new Dictionary<int, RebirthNpcPatrolSession>();
    private static long nextTickUtcTicks;

    public static void Register()
    {
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcPatrolActivityController());
    }

    public static void Attach(RebirthNpcControllerContext context)
    {
        if (!context.Lease.HasTargetPosition)
        {
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.PermanentInvalid,
                "Patrol execution lease has no target position.");
            return;
        }

        Vector3 origin = context.Npc.position;
        if (context.IsRecovery)
        {
            lock (Sync)
            {
                RebirthNpcPatrolSession prior;
                if (Sessions.TryGetValue(context.Npc.entityId, out prior) &&
                    prior.LeaseId == context.Lease.LeaseId && prior.ActivityId == context.ActivityId)
                    origin = prior.PointA;
            }
        }
        Vector3 destination = context.Lease.TargetPosition;
        if ((origin - destination).sqrMagnitude < MinimumLegDistance * MinimumLegDistance)
        {
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.PermanentInvalid,
                "Patrol target must be at least " + MinimumLegDistance + " metres from the NPC start position.");
            return;
        }

        RebirthNpcPatrolSession session = new RebirthNpcPatrolSession
        {
            EntityId = context.Npc.entityId,
            LeaseId = context.Lease.LeaseId,
            ActivityId = context.ActivityId,
            AcceptedRevision = context.Lease.AcceptedRevision,
            PointA = origin,
            PointB = destination,
            CurrentPointIndex = 1,
            LastRequestedPointIndex = -1,
            CompletedLegs = 0
        };
        lock (Sync) Sessions[session.EntityId] = session;
        RebirthNpcPatrolDiagnostics.RecordAttached(context.IsRecovery);
    }

    public static void Tick()
    {
        long now = DateTime.UtcNow.Ticks;
        if (now < nextTickUtcTicks) return;
        nextTickUtcTicks = now + TimeSpan.FromSeconds(1).Ticks;

        RebirthNpcPatrolSession[] snapshot;
        lock (Sync)
        {
            snapshot = new RebirthNpcPatrolSession[Sessions.Count];
            Sessions.Values.CopyTo(snapshot, 0);
        }
        for (int i = 0; i < snapshot.Length; i++) TickSession(snapshot[i]);
    }

    private static void TickSession(RebirthNpcPatrolSession session)
    {
        EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(session.EntityId) as EntityRebirthNPC;
        if (npc == null) { Remove(session.EntityId, session.ActivityId); return; }

        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(session.EntityId, out lease) ||
            lease.LeaseId != session.LeaseId || lease.Order != RebirthNpcOrderState.Patrol)
        {
            Stop(npc, "Patrol lease ended.");
            Remove(session.EntityId, session.ActivityId);
            return;
        }

        Vector3 target = session.CurrentPointIndex == 0 ? session.PointA : session.PointB;
        if ((npc.position - target).sqrMagnitude <= ArrivalRadius * ArrivalRadius)
        {
            session.CompletedLegs++;
            session.CurrentPointIndex = session.CurrentPointIndex == 0 ? 1 : 0;
            session.LastRequestedPointIndex = -1;
            target = session.CurrentPointIndex == 0 ? session.PointA : session.PointB;
            lock (Sync) Sessions[session.EntityId] = session;
            RebirthNpcPatrolDiagnostics.RecordLegCompleted();
        }

        IRebirthNpcMovementAdapter adapter = RebirthNpcMovementAdapterRegistry.Current;
        if (adapter == null)
        {
            Report(session, RebirthNpcActivityOutcome.TransientBlocked,
                "No movement adapter is registered for Patrol navigation.");
            return;
        }

        {
            string detail;
            RebirthNpcMovementRequestResult result = adapter.RequestMove(
                npc, target, ArrivalRadius, out detail);
            if (result == RebirthNpcMovementRequestResult.Unsupported ||
                result == RebirthNpcMovementRequestResult.Failed)
            {
                Report(session, RebirthNpcActivityOutcome.PermanentInvalid, detail);
                return;
            }
            if (result == RebirthNpcMovementRequestResult.TemporarilyUnavailable)
            {
                Report(session, RebirthNpcActivityOutcome.TransientBlocked, detail);
                return;
            }
            session.LastRequestedPointIndex = session.CurrentPointIndex;
            lock (Sync) Sessions[session.EntityId] = session;
            RebirthNpcPatrolDiagnostics.RecordMoveRequest(result);
        }

        if (!RebirthNpcActivityRegistry.TryHeartbeat(session.EntityId, session.ActivityId,
                session.LeaseId, HeartbeatTimeoutSeconds,
                "Patrol active; completed legs=" + session.CompletedLegs + "."))
            Remove(session.EntityId, session.ActivityId);
    }

    private static void Report(RebirthNpcPatrolSession session,
        RebirthNpcActivityOutcome outcome, string detail)
    {
        RebirthNpcActivityRegistry.TryReportResult(session.EntityId, session.ActivityId,
            session.LeaseId, session.AcceptedRevision, outcome, detail);
        Remove(session.EntityId, session.ActivityId);
    }

    private static void Stop(EntityRebirthNPC npc, string reason)
    {
        IRebirthNpcMovementAdapter adapter = RebirthNpcMovementAdapterRegistry.Current;
        if (adapter != null) adapter.Stop(npc, reason);
    }

    private static void Remove(int entityId, ulong activityId)
    {
        lock (Sync)
        {
            RebirthNpcPatrolSession current;
            if (Sessions.TryGetValue(entityId, out current) && current.ActivityId == activityId)
                Sessions.Remove(entityId);
        }
    }

    public static RebirthNpcPatrolSession[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcPatrolSession[] result = new RebirthNpcPatrolSession[Sessions.Count];
            Sessions.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => left.EntityId.CompareTo(right.EntityId));
            return result;
        }
    }
}

public struct RebirthNpcPatrolSession
{
    public int EntityId;
    public ulong LeaseId;
    public ulong ActivityId;
    public uint AcceptedRevision;
    public Vector3 PointA;
    public Vector3 PointB;
    public int CurrentPointIndex;
    public int LastRequestedPointIndex;
    public int CompletedLegs;
}

public static class RebirthNpcPatrolDiagnostics
{
    private static long attached, recovered, moveRequests, alreadySatisfied, completedLegs;

    internal static void RecordAttached(bool recovery)
    {
        if (recovery) recovered++; else attached++;
    }

    internal static void RecordMoveRequest(RebirthNpcMovementRequestResult result)
    {
        if (result == RebirthNpcMovementRequestResult.Accepted) moveRequests++;
        else if (result == RebirthNpcMovementRequestResult.AlreadySatisfied) alreadySatisfied++;
    }

    internal static void RecordLegCompleted() { completedLegs++; }

    public static string GetReport()
    {
        RebirthNpcPatrolSession[] sessions = RebirthNpcPatrolRuntime.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] patrol active=").Append(sessions.Length)
            .Append(" attached=").Append(attached)
            .Append(" recovered=").Append(recovered)
            .Append(" moveRequests=").Append(moveRequests)
            .Append(" alreadySatisfied=").Append(alreadySatisfied)
            .Append(" completedLegs=").Append(completedLegs).AppendLine();
        for (int i = 0; i < sessions.Length; i++)
        {
            RebirthNpcPatrolSession session = sessions[i];
            builder.Append("  entity=").Append(session.EntityId)
                .Append(" activity=").Append(session.ActivityId)
                .Append(" lease=").Append(session.LeaseId)
                .Append(" pointA=").Append(session.PointA)
                .Append(" pointB=").Append(session.PointB)
                .Append(" next=").Append(session.CurrentPointIndex == 0 ? "A" : "B")
                .Append(" legs=").Append(session.CompletedLegs).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
