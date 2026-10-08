using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcGuardNavigationActivityController : IRebirthNpcActivityController
{
    private const float GuardHoldRadius = 3f;

    public string ControllerId => "rebirth.navigation.guard";
    public int Priority => 195;

    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease)
    {
        return npc != null && lease != null && lease.Order == RebirthNpcOrderState.Guard &&
            npc.RebirthRuntimeState != null && npc.RebirthRuntimeState.HasGuardPosition &&
            (npc.position - npc.RebirthRuntimeState.GuardPosition).sqrMagnitude >
                GuardHoldRadius * GuardHoldRadius;
    }

    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 6;
    public void Start(RebirthNpcControllerContext context) => RebirthNpcGuardNavigationRuntime.Attach(context);
    public void Recover(RebirthNpcControllerContext context) => RebirthNpcGuardNavigationRuntime.Attach(context);
}

public static class RebirthNpcGuardNavigationRuntime
{
    private const float GuardHoldRadius = 3f;
    private const float RepathDistance = 1f;
    private const int HeartbeatTimeoutSeconds = 6;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcGuardNavigationSession> Sessions =
        new Dictionary<int, RebirthNpcGuardNavigationSession>();
    private static long nextTickUtcTicks;

    public static void Register()
    {
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcGuardNavigationActivityController());
    }

    public static void Attach(RebirthNpcControllerContext context)
    {
        if (context.Npc.RebirthRuntimeState == null ||
            !context.Npc.RebirthRuntimeState.HasGuardPosition)
        {
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.PermanentInvalid,
                "Guard navigation requires a persisted guard position.");
            return;
        }

        RebirthNpcGuardNavigationSession session = new RebirthNpcGuardNavigationSession
        {
            EntityId = context.Npc.entityId,
            LeaseId = context.Lease.LeaseId,
            ActivityId = context.ActivityId,
            AcceptedRevision = context.Lease.AcceptedRevision,
            GuardPosition = context.Npc.RebirthRuntimeState.GuardPosition,
            LastRequestedDestination = new Vector3(float.NaN, 0f, 0f)
        };
        lock (Sync) Sessions[session.EntityId] = session;
        RebirthNpcGuardNavigationDiagnostics.RecordAttached(context.IsRecovery);
    }

    public static void Tick()
    {
        long now = DateTime.UtcNow.Ticks;
        if (now < nextTickUtcTicks) return;
        nextTickUtcTicks = now + TimeSpan.FromSeconds(1).Ticks;

        RebirthNpcGuardNavigationSession[] snapshot;
        lock (Sync)
        {
            snapshot = new RebirthNpcGuardNavigationSession[Sessions.Count];
            Sessions.Values.CopyTo(snapshot, 0);
        }
        for (int i = 0; i < snapshot.Length; i++) TickSession(snapshot[i]);
    }

    private static void TickSession(RebirthNpcGuardNavigationSession session)
    {
        EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(session.EntityId) as EntityRebirthNPC;
        if (npc == null) { Remove(session.EntityId, session.ActivityId); return; }

        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(session.EntityId, out lease) ||
            lease.LeaseId != session.LeaseId || lease.Order != RebirthNpcOrderState.Guard)
        {
            Stop(npc, "Guard navigation lease ended.");
            Remove(session.EntityId, session.ActivityId);
            return;
        }

        if (npc.RebirthRuntimeState == null || !npc.RebirthRuntimeState.HasGuardPosition)
        {
            Report(session, RebirthNpcActivityOutcome.PermanentInvalid,
                "Guard position was removed while navigation was active.");
            return;
        }

        Vector3 guardPosition = npc.RebirthRuntimeState.GuardPosition;
        float distanceSquared = (npc.position - guardPosition).sqrMagnitude;
        IRebirthNpcMovementAdapter adapter = RebirthNpcMovementAdapterRegistry.Current;
        if (adapter == null)
        {
            Report(session, RebirthNpcActivityOutcome.TransientBlocked,
                "No movement adapter is registered for Guard navigation.");
            return;
        }

        if (distanceSquared <= GuardHoldRadius * GuardHoldRadius)
        {
            adapter.Stop(npc, "Guard position reached; yielding to stationary Guard hold.");
            if (RebirthNpcActivityRegistry.TryYield(session.EntityId, session.ActivityId,
                    session.LeaseId,
                    "Guard navigation reached the configured hold radius and yielded to the stationary controller."))
                RebirthNpcGuardNavigationDiagnostics.RecordHandoff();
            Remove(session.EntityId, session.ActivityId);
            return;
        }

        {
            string detail;
            RebirthNpcMovementRequestResult result = adapter.RequestMove(
                npc, guardPosition, GuardHoldRadius, out detail);
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
            session.GuardPosition = guardPosition;
            session.LastRequestedDestination = guardPosition;
            lock (Sync) Sessions[session.EntityId] = session;
            RebirthNpcGuardNavigationDiagnostics.RecordMoveRequest(result);
        }

        if (!RebirthNpcActivityRegistry.TryHeartbeat(session.EntityId, session.ActivityId,
                session.LeaseId, HeartbeatTimeoutSeconds, "Guard navigation active."))
            Remove(session.EntityId, session.ActivityId);
    }

    private static void Report(RebirthNpcGuardNavigationSession session,
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
            RebirthNpcGuardNavigationSession current;
            if (Sessions.TryGetValue(entityId, out current) && current.ActivityId == activityId)
                Sessions.Remove(entityId);
        }
    }

    public static RebirthNpcGuardNavigationSession[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcGuardNavigationSession[] result =
                new RebirthNpcGuardNavigationSession[Sessions.Count];
            Sessions.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => left.EntityId.CompareTo(right.EntityId));
            return result;
        }
    }
}

public struct RebirthNpcGuardNavigationSession
{
    public int EntityId;
    public ulong LeaseId;
    public ulong ActivityId;
    public uint AcceptedRevision;
    public Vector3 GuardPosition;
    public Vector3 LastRequestedDestination;
}

public static class RebirthNpcGuardNavigationDiagnostics
{
    private static long attached, recovered, moveRequests, alreadySatisfied, handoffs;

    internal static void RecordAttached(bool recovery)
    {
        if (recovery) recovered++; else attached++;
    }

    internal static void RecordMoveRequest(RebirthNpcMovementRequestResult result)
    {
        if (result == RebirthNpcMovementRequestResult.Accepted) moveRequests++;
        else if (result == RebirthNpcMovementRequestResult.AlreadySatisfied) alreadySatisfied++;
    }

    internal static void RecordHandoff() { handoffs++; }

    public static string GetReport()
    {
        RebirthNpcGuardNavigationSession[] sessions =
            RebirthNpcGuardNavigationRuntime.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] guard navigation active=").Append(sessions.Length)
            .Append(" attached=").Append(attached)
            .Append(" recovered=").Append(recovered)
            .Append(" moveRequests=").Append(moveRequests)
            .Append(" alreadySatisfied=").Append(alreadySatisfied)
            .Append(" stationaryHandoffs=").Append(handoffs).AppendLine();
        for (int i = 0; i < sessions.Length; i++)
        {
            RebirthNpcGuardNavigationSession session = sessions[i];
            builder.Append("  entity=").Append(session.EntityId)
                .Append(" activity=").Append(session.ActivityId)
                .Append(" lease=").Append(session.LeaseId)
                .Append(" guard=").Append(session.GuardPosition)
                .Append(" lastDestination=").Append(session.LastRequestedDestination).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
