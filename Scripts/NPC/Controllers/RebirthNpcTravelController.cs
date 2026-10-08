using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcTravelActivityController : IRebirthNpcActivityController
{
    public string ControllerId => "rebirth.navigation.travel";
    public int Priority => 155;

    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease)
    {
        return npc != null && lease != null && lease.Order == RebirthNpcOrderState.Travel &&
            lease.HasTargetPosition;
    }

    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 6;
    public void Start(RebirthNpcControllerContext context) => RebirthNpcTravelRuntime.Attach(context);
    public void Recover(RebirthNpcControllerContext context) => RebirthNpcTravelRuntime.Attach(context);
}

public static class RebirthNpcTravelRuntime
{
    public const float ArrivalRadius = 2.5f;
    private const int HeartbeatTimeoutSeconds = 6;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcTravelSession> Sessions =
        new Dictionary<int, RebirthNpcTravelSession>();
    private static long nextTickUtcTicks;

    public static void Register()
    {
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcTravelActivityController());
    }

    public static void Attach(RebirthNpcControllerContext context)
    {
        if (!context.Lease.HasTargetPosition)
        {
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.PermanentInvalid,
                "Travel execution lease has no target position.");
            return;
        }

        RebirthNpcTravelSession session = new RebirthNpcTravelSession
        {
            EntityId = context.Npc.entityId,
            LeaseId = context.Lease.LeaseId,
            ActivityId = context.ActivityId,
            AcceptedRevision = context.Lease.AcceptedRevision,
            Destination = context.Lease.TargetPosition,
            MoveRequested = false
        };
        lock (Sync) Sessions[session.EntityId] = session;
        RebirthNpcTravelDiagnostics.RecordAttached(context.IsRecovery);
    }

    public static void Tick()
    {
        long now = DateTime.UtcNow.Ticks;
        if (now < nextTickUtcTicks) return;
        nextTickUtcTicks = now + TimeSpan.FromSeconds(1).Ticks;

        RebirthNpcTravelSession[] snapshot;
        lock (Sync)
        {
            snapshot = new RebirthNpcTravelSession[Sessions.Count];
            Sessions.Values.CopyTo(snapshot, 0);
        }
        for (int i = 0; i < snapshot.Length; i++) TickSession(snapshot[i]);
    }

    private static void TickSession(RebirthNpcTravelSession session)
    {
        EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(session.EntityId) as EntityRebirthNPC;
        if (npc == null)
        {
            Remove(session.EntityId, session.ActivityId);
            return;
        }

        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(session.EntityId, out lease) ||
            lease.LeaseId != session.LeaseId || lease.Order != RebirthNpcOrderState.Travel)
        {
            Stop(npc, "Travel lease ended.");
            Remove(session.EntityId, session.ActivityId);
            return;
        }

        if ((npc.position - session.Destination).sqrMagnitude <= ArrivalRadius * ArrivalRadius)
        {
            Stop(npc, "Travel destination reached.");
            if (RebirthNpcActivityRegistry.TryReportResult(session.EntityId, session.ActivityId,
                    session.LeaseId, session.AcceptedRevision,
                    RebirthNpcActivityOutcome.Succeeded,
                    "Travel destination reached within " + ArrivalRadius + " metres."))
                RebirthNpcTravelDiagnostics.RecordCompleted();
            Remove(session.EntityId, session.ActivityId);
            return;
        }

        IRebirthNpcMovementAdapter adapter = RebirthNpcMovementAdapterRegistry.Current;
        if (adapter == null)
        {
            Report(session, RebirthNpcActivityOutcome.TransientBlocked,
                "No movement adapter is registered for Travel navigation.");
            return;
        }

        {
            string detail;
            RebirthNpcMovementRequestResult result = adapter.RequestMove(
                npc, session.Destination, ArrivalRadius, out detail);
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
            session.MoveRequested = true;
            lock (Sync) Sessions[session.EntityId] = session;
            RebirthNpcTravelDiagnostics.RecordMoveRequest(result);
        }

        if (!RebirthNpcActivityRegistry.TryHeartbeat(session.EntityId, session.ActivityId,
                session.LeaseId, HeartbeatTimeoutSeconds,
                "Travel navigation active toward " + session.Destination + "."))
            Remove(session.EntityId, session.ActivityId);
    }

    private static void Report(RebirthNpcTravelSession session,
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
            RebirthNpcTravelSession current;
            if (Sessions.TryGetValue(entityId, out current) && current.ActivityId == activityId)
                Sessions.Remove(entityId);
        }
    }

    public static RebirthNpcTravelSession[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcTravelSession[] result = new RebirthNpcTravelSession[Sessions.Count];
            Sessions.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => left.EntityId.CompareTo(right.EntityId));
            return result;
        }
    }
}

public struct RebirthNpcTravelSession
{
    public int EntityId;
    public ulong LeaseId;
    public ulong ActivityId;
    public uint AcceptedRevision;
    public Vector3 Destination;
    public bool MoveRequested;
}

public static class RebirthNpcTravelDiagnostics
{
    private static long attached, recovered, moveRequests, alreadySatisfied, completed;

    internal static void RecordAttached(bool recovery)
    {
        if (recovery) recovered++; else attached++;
    }

    internal static void RecordMoveRequest(RebirthNpcMovementRequestResult result)
    {
        if (result == RebirthNpcMovementRequestResult.Accepted) moveRequests++;
        else if (result == RebirthNpcMovementRequestResult.AlreadySatisfied) alreadySatisfied++;
    }

    internal static void RecordCompleted() { completed++; }

    public static string GetReport()
    {
        RebirthNpcTravelSession[] sessions = RebirthNpcTravelRuntime.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] travel active=").Append(sessions.Length)
            .Append(" attached=").Append(attached)
            .Append(" recovered=").Append(recovered)
            .Append(" moveRequests=").Append(moveRequests)
            .Append(" alreadySatisfied=").Append(alreadySatisfied)
            .Append(" completed=").Append(completed).AppendLine();
        for (int i = 0; i < sessions.Length; i++)
        {
            RebirthNpcTravelSession session = sessions[i];
            builder.Append("  entity=").Append(session.EntityId)
                .Append(" activity=").Append(session.ActivityId)
                .Append(" lease=").Append(session.LeaseId)
                .Append(" destination=").Append(session.Destination)
                .Append(" moveRequested=").Append(session.MoveRequested).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
