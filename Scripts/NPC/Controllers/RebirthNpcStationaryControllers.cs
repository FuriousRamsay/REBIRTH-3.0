using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcStayActivityController : IRebirthNpcActivityController
{
    public string ControllerId => "rebirth.stationary.stay";
    public int Priority => 200;

    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease)
    {
        return npc != null && lease != null && lease.Order == RebirthNpcOrderState.Stay;
    }

    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 5;

    public void Start(RebirthNpcControllerContext context)
    {
        RebirthNpcStationaryActivityRuntime.Attach(context, context.Npc.position, 2.5f,
            "Stay activity anchored at the NPC's accepted position.");
    }

    public void Recover(RebirthNpcControllerContext context)
    {
        RebirthNpcStationaryActivityRuntime.Attach(context, context.Npc.position, 2.5f,
            "Stay activity recovered with a new local anchor.");
    }
}

public sealed class RebirthNpcGuardActivityController : IRebirthNpcActivityController
{
    private const float GuardHoldRadius = 3f;

    public string ControllerId => "rebirth.stationary.guard";
    public int Priority => 190;

    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease)
    {
        return npc != null && lease != null && lease.Order == RebirthNpcOrderState.Guard &&
            npc.RebirthRuntimeState != null && npc.RebirthRuntimeState.HasGuardPosition &&
            (npc.position - npc.RebirthRuntimeState.GuardPosition).sqrMagnitude <=
                GuardHoldRadius * GuardHoldRadius;
    }

    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 5;

    public void Start(RebirthNpcControllerContext context)
    {
        StartOrRecover(context, false);
    }

    public void Recover(RebirthNpcControllerContext context)
    {
        StartOrRecover(context, true);
    }

    private static void StartOrRecover(RebirthNpcControllerContext context, bool recovery)
    {
        Vector3 guardPosition = context.Npc.RebirthRuntimeState.GuardPosition;
        float distanceSquared = (context.Npc.position - guardPosition).sqrMagnitude;
        if (distanceSquared > GuardHoldRadius * GuardHoldRadius)
        {
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.TransientBlocked,
                "Guard hold requires navigation: NPC is outside the configured guard radius.");
            RebirthNpcStationaryActivityDiagnostics.RecordGuardNeedsNavigation();
            return;
        }

        RebirthNpcStationaryActivityRuntime.Attach(context, guardPosition, GuardHoldRadius,
            recovery ? "Guard hold recovered inside the configured radius." :
                "Guard hold started inside the configured radius.");
    }
}

public static class RebirthNpcStationaryActivityRuntime
{
    private const int HeartbeatTimeoutSeconds = 5;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, RebirthNpcStationaryActivitySession> Sessions =
        new Dictionary<int, RebirthNpcStationaryActivitySession>();
    private static long nextTickUtcTicks;

    public static void RegisterControllers()
    {
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcStayActivityController());
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcGuardActivityController());
    }

    public static void Attach(RebirthNpcControllerContext context, Vector3 anchor,
        float allowedRadius, string detail)
    {
        RebirthNpcStationaryActivitySession session = new RebirthNpcStationaryActivitySession
        {
            EntityId = context.Npc.entityId,
            LeaseId = context.Lease.LeaseId,
            ActivityId = context.ActivityId,
            AcceptedRevision = context.Lease.AcceptedRevision,
            Order = context.Lease.Order,
            Anchor = anchor,
            AllowedRadiusSquared = allowedRadius * allowedRadius,
            Detail = detail ?? string.Empty
        };
        lock (Sync) Sessions[session.EntityId] = session;
        RebirthNpcActivityRegistry.TryHeartbeat(session.EntityId, session.ActivityId,
            session.LeaseId, HeartbeatTimeoutSeconds, session.Detail);
        RebirthNpcStationaryActivityDiagnostics.RecordAttached(session.Order);
    }

    public static void Tick()
    {
        long now = DateTime.UtcNow.Ticks;
        if (now < nextTickUtcTicks) return;
        nextTickUtcTicks = now + TimeSpan.FromSeconds(1).Ticks;

        RebirthNpcStationaryActivitySession[] snapshot;
        lock (Sync)
        {
            snapshot = new RebirthNpcStationaryActivitySession[Sessions.Count];
            Sessions.Values.CopyTo(snapshot, 0);
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            RebirthNpcStationaryActivitySession session = snapshot[i];
            EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(session.EntityId) as EntityRebirthNPC;
            if (npc == null)
            {
                Remove(session.EntityId, session.ActivityId);
                continue;
            }

            RebirthNpcExecutionLease lease;
            if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(session.EntityId, out lease) ||
                lease.LeaseId != session.LeaseId || lease.Order != session.Order)
            {
                Remove(session.EntityId, session.ActivityId);
                continue;
            }

            if ((npc.position - session.Anchor).sqrMagnitude > session.AllowedRadiusSquared)
            {
                bool reported = RebirthNpcActivityRegistry.TryReportResult(session.EntityId,
                    session.ActivityId, session.LeaseId, session.AcceptedRevision,
                    RebirthNpcActivityOutcome.TransientBlocked,
                    session.Order + " hold radius was exceeded; navigation or repositioning is required.");
                if (reported) RebirthNpcStationaryActivityDiagnostics.RecordDrift(session.Order);
                Remove(session.EntityId, session.ActivityId);
                continue;
            }

            if (!RebirthNpcActivityRegistry.TryHeartbeat(session.EntityId, session.ActivityId,
                    session.LeaseId, HeartbeatTimeoutSeconds,
                    session.Order + " hold active within configured radius."))
                Remove(session.EntityId, session.ActivityId);
            else
                RebirthNpcStationaryActivityDiagnostics.RecordHeartbeat();
        }
    }

    private static void Remove(int entityId, ulong activityId)
    {
        lock (Sync)
        {
            RebirthNpcStationaryActivitySession current;
            if (Sessions.TryGetValue(entityId, out current) && current.ActivityId == activityId)
                Sessions.Remove(entityId);
        }
    }

    public static RebirthNpcStationaryActivitySession[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcStationaryActivitySession[] result =
                new RebirthNpcStationaryActivitySession[Sessions.Count];
            Sessions.Values.CopyTo(result, 0);
            Array.Sort(result, (left, right) => left.EntityId.CompareTo(right.EntityId));
            return result;
        }
    }
}

public sealed class RebirthNpcStationaryActivitySession
{
    public int EntityId;
    public ulong LeaseId;
    public ulong ActivityId;
    public uint AcceptedRevision;
    public RebirthNpcOrderState Order;
    public Vector3 Anchor;
    public float AllowedRadiusSquared;
    public string Detail;
}

public static class RebirthNpcStationaryActivityDiagnostics
{
    private static long stayAttached, guardAttached, heartbeats, drifted, guardNeedsNavigation;

    internal static void RecordAttached(RebirthNpcOrderState order)
    {
        if (order == RebirthNpcOrderState.Stay) stayAttached++;
        else if (order == RebirthNpcOrderState.Guard) guardAttached++;
    }

    internal static void RecordHeartbeat() { heartbeats++; }
    internal static void RecordDrift(RebirthNpcOrderState order) { drifted++; }
    internal static void RecordGuardNeedsNavigation() { guardNeedsNavigation++; }

    public static string GetReport()
    {
        RebirthNpcStationaryActivitySession[] sessions =
            RebirthNpcStationaryActivityRuntime.GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC] stationary activities active=").Append(sessions.Length)
            .Append(" stayAttached=").Append(stayAttached)
            .Append(" guardAttached=").Append(guardAttached)
            .Append(" heartbeats=").Append(heartbeats)
            .Append(" drifted=").Append(drifted)
            .Append(" guardNeedsNavigation=").Append(guardNeedsNavigation).AppendLine();
        for (int i = 0; i < sessions.Length; i++)
        {
            RebirthNpcStationaryActivitySession session = sessions[i];
            builder.Append("  entity=").Append(session.EntityId)
                .Append(" activity=").Append(session.ActivityId)
                .Append(" lease=").Append(session.LeaseId)
                .Append(" order=").Append(session.Order)
                .Append(" anchor=").Append(session.Anchor)
                .Append(" radius=").Append(Math.Sqrt(session.AllowedRadiusSquared))
                .Append(" detail=").Append(session.Detail).AppendLine();
        }
        return builder.ToString().TrimEnd();
    }
}
