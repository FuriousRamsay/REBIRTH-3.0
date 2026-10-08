using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

public enum RebirthNpcMovementRequestResult : byte
{
    Accepted = 0,
    AlreadySatisfied = 1,
    TemporarilyUnavailable = 2,
    Unsupported = 3,
    Failed = 4
}

public interface IRebirthNpcMovementAdapter
{
    string AdapterId { get; }
    RebirthNpcMovementRequestResult RequestMove(EntityRebirthNPC npc, Vector3 destination,
        float stopDistance, out string detail);
    void Stop(EntityRebirthNPC npc, string reason);
}

public static class RebirthNpcMovementAdapterRegistry
{
    private static readonly object Sync = new object();
    private static IRebirthNpcMovementAdapter current;

    public static void Register(IRebirthNpcMovementAdapter adapter)
    {
        if (adapter == null) throw new ArgumentNullException(nameof(adapter));
        lock (Sync) current = adapter;
    }

    public static IRebirthNpcMovementAdapter Current
    {
        get { lock (Sync) return current; }
    }
}

/// <summary>
/// Direct 3.1 bridge to EntityAlive.FindPath and PathNavigate.clearPath.
/// No runtime member discovery or reflective invocation is used.
/// </summary>
public sealed class RebirthNpcNativeNavigatorAdapter : IRebirthNpcMovementAdapter
{
    public string AdapterId => "rebirth.native.navigator.3_1";

    public RebirthNpcMovementRequestResult RequestMove(EntityRebirthNPC npc, Vector3 destination,
        float stopDistance, out string detail)
    {
        detail = string.Empty;
        if (npc == null || npc.IsDead())
        {
            detail = "NPC is unavailable.";
            return RebirthNpcMovementRequestResult.TemporarilyUnavailable;
        }

        if ((npc.position - destination).sqrMagnitude <= stopDistance * stopDistance)
        {
            detail = "Destination already inside the follow stop distance.";
            return RebirthNpcMovementRequestResult.AlreadySatisfied;
        }

        if (npc.getNavigator() == null)
        {
            detail = "Native PathNavigate instance is unavailable.";
            return RebirthNpcMovementRequestResult.TemporarilyUnavailable;
        }

        try
        {
            float requestedSpeed = npc.moveSpeed > 0f ? npc.moveSpeed : 0.2f;
            npc.FindPath(destination, requestedSpeed, true, null);
            detail = "EntityAlive.FindPath accepted the 3.1 navigation request.";
            return RebirthNpcMovementRequestResult.Accepted;
        }
        catch (Exception ex)
        {
            detail = "Native navigation request failed: " + ex.GetBaseException().Message;
            return RebirthNpcMovementRequestResult.Failed;
        }
    }

    public void Stop(EntityRebirthNPC npc, string reason)
    {
        if (npc == null || npc.getNavigator() == null)
            return;

        npc.getNavigator().clearPath();
    }
}

public sealed class RebirthNpcFollowActivityController : IRebirthNpcActivityController
{
    public string ControllerId => "rebirth.navigation.follow";
    public int Priority => 180;
    public bool CanHandle(EntityRebirthNPC npc, RebirthNpcExecutionLease lease) =>
        npc != null && lease != null && lease.Order == RebirthNpcOrderState.Follow && lease.SubjectEntityId >= 0;
    public int GetHeartbeatTimeoutSeconds(RebirthNpcExecutionLease lease) => 6;
    public void Start(RebirthNpcControllerContext context) => RebirthNpcFollowRuntime.Attach(context);
    public void Recover(RebirthNpcControllerContext context) => RebirthNpcFollowRuntime.Attach(context);
}

public static class RebirthNpcFollowRuntime
{
    private const float DesiredDistance = 3.5f;
    private const float RepathDistance = 2f;
    private static readonly Dictionary<int, RebirthNpcFollowSession> Sessions = new Dictionary<int, RebirthNpcFollowSession>();
    private static readonly object Sync = new object();
    private static long nextTick;

    public static void Register()
    {
        RebirthNpcMovementAdapterRegistry.Register(new RebirthNpcNavigationRuntimeAdapter(new RebirthNpcNativeNavigatorAdapter()));
        RebirthNpcActivityControllerRegistry.Register(new RebirthNpcFollowActivityController());
    }

    public static void Attach(RebirthNpcControllerContext context)
    {
        if (context.Lease.SubjectEntityId < 0)
        {
            RebirthNpcActivityRegistry.TryReportResult(context.Npc.entityId, context.ActivityId,
                context.Lease.LeaseId, context.Lease.AcceptedRevision,
                RebirthNpcActivityOutcome.PermanentInvalid, "Follow lease has no authenticated subject entity.");
            return;
        }
        lock (Sync) Sessions[context.Npc.entityId] = new RebirthNpcFollowSession
        {
            EntityId = context.Npc.entityId, TargetEntityId = context.Lease.SubjectEntityId,
            LeaseId = context.Lease.LeaseId, ActivityId = context.ActivityId,
            AcceptedRevision = context.Lease.AcceptedRevision, LastDestination = new Vector3(float.NaN, 0f, 0f)
        };
    }

    public static void Tick()
    {
        long now = DateTime.UtcNow.Ticks;
        if (now < nextTick) return;
        nextTick = now + TimeSpan.FromSeconds(1).Ticks;
        RebirthNpcFollowSession[] snapshot;
        lock (Sync) { snapshot = new RebirthNpcFollowSession[Sessions.Count]; Sessions.Values.CopyTo(snapshot, 0); }
        for (int i = 0; i < snapshot.Length; i++) TickSession(snapshot[i]);
    }

    private static void TickSession(RebirthNpcFollowSession s)
    {
        EntityRebirthNPC npc = GameManager.Instance.World.GetEntity(s.EntityId) as EntityRebirthNPC;
        Entity target = GameManager.Instance.World.GetEntity(s.TargetEntityId);
        if (npc == null) { Remove(s.EntityId, s.ActivityId); return; }
        if (target == null)
        {
            Report(s, RebirthNpcActivityOutcome.TransientTargetUnavailable, "Follow target is temporarily unavailable."); return;
        }
        RebirthNpcExecutionLease lease;
        if (!RebirthNpcExecutionLeaseRegistry.TryGetActive(s.EntityId, out lease) || lease.LeaseId != s.LeaseId)
        { Stop(npc, "Follow lease ended."); Remove(s.EntityId, s.ActivityId); return; }

        Vector3 destination = target.position;
        float distanceSq = (npc.position - destination).sqrMagnitude;
        IRebirthNpcMovementAdapter adapter = RebirthNpcMovementAdapterRegistry.Current;

        // Keep normal followers close without continuously holding distant chunks open.
        // This is an automatic Recall, not an order change: once separation exceeds 35 m
        // the follower is placed at the owner/subject's exact position and continues Follow.
        float autoRecall = RebirthCompanionService.AutomaticFollowRecallDistance;
        if (distanceSq > autoRecall * autoRecall && target.AttachedToEntity == null)
        {
            npc.SetAttackTarget(null, 0);
            npc.SetRevengeTarget(null);
            npc.SetPosition(destination, true);
            npc.position = destination;
            if (adapter != null) adapter.Stop(npc, "Automatic follow Recall (>35 m).");
            s.LastDestination = destination;
            lock (Sync) Sessions[s.EntityId] = s;
            if (!RebirthNpcActivityRegistry.TryHeartbeat(s.EntityId, s.ActivityId, s.LeaseId, 6,
                "Follower automatically Recalled after exceeding 35 m.")) Remove(s.EntityId, s.ActivityId);
            return;
        }
        if (adapter == null) { Report(s, RebirthNpcActivityOutcome.TransientBlocked, "No movement adapter is registered."); return; }
        if (distanceSq <= DesiredDistance * DesiredDistance)
        {
            adapter.Stop(npc, "Follow distance satisfied.");
            if (!RebirthNpcActivityRegistry.TryHeartbeat(s.EntityId, s.ActivityId, s.LeaseId, 6,
                "Following target inside desired distance.")) Remove(s.EntityId, s.ActivityId);
            return;
        }
        {
            string detail;
            RebirthNpcMovementRequestResult result = adapter.RequestMove(npc, destination, DesiredDistance, out detail);
            if (result == RebirthNpcMovementRequestResult.Unsupported || result == RebirthNpcMovementRequestResult.Failed)
            { Report(s, RebirthNpcActivityOutcome.PermanentInvalid, detail); return; }
            if (result == RebirthNpcMovementRequestResult.TemporarilyUnavailable)
            { Report(s, RebirthNpcActivityOutcome.TransientBlocked, detail); return; }
            s.LastDestination = destination;
            lock (Sync) Sessions[s.EntityId] = s;
            RebirthNpcFollowDiagnostics.RecordMoveRequest(result);
        }
        if (!RebirthNpcActivityRegistry.TryHeartbeat(s.EntityId, s.ActivityId, s.LeaseId, 6,
            "Follow navigation active.")) Remove(s.EntityId, s.ActivityId);
    }

    private static void Report(RebirthNpcFollowSession s, RebirthNpcActivityOutcome outcome, string detail)
    {
        RebirthNpcActivityRegistry.TryReportResult(s.EntityId, s.ActivityId, s.LeaseId,
            s.AcceptedRevision, outcome, detail); Remove(s.EntityId, s.ActivityId);
    }
    private static void Stop(EntityRebirthNPC npc, string reason) { RebirthNpcMovementAdapterRegistry.Current?.Stop(npc, reason); }
    private static void Remove(int entityId, ulong activityId) { lock (Sync) { RebirthNpcFollowSession c; if (Sessions.TryGetValue(entityId, out c) && c.ActivityId == activityId) Sessions.Remove(entityId); } }
    public static RebirthNpcFollowSession[] GetSnapshot() { lock (Sync) { var r=new RebirthNpcFollowSession[Sessions.Count]; Sessions.Values.CopyTo(r,0); return r; } }
}

public struct RebirthNpcFollowSession
{
    public int EntityId, TargetEntityId; public ulong LeaseId, ActivityId; public uint AcceptedRevision; public Vector3 LastDestination;
}

public static class RebirthNpcFollowDiagnostics
{
    private static long accepted, satisfied;
    internal static void RecordMoveRequest(RebirthNpcMovementRequestResult result) { if (result == RebirthNpcMovementRequestResult.Accepted) accepted++; else satisfied++; }
    public static string GetReport()
    {
        var sessions=RebirthNpcFollowRuntime.GetSnapshot(); var b=new StringBuilder();
        b.Append("[REBIRTH NPC] follow active=").Append(sessions.Length).Append(" moveRequests=").Append(accepted).Append(" alreadySatisfied=").Append(satisfied).AppendLine();
        for(int i=0;i<sessions.Length;i++) b.Append("  entity=").Append(sessions[i].EntityId).Append(" target=").Append(sessions[i].TargetEntityId).Append(" activity=").Append(sessions[i].ActivityId).Append(" lease=").Append(sessions[i].LeaseId).AppendLine();
        return b.ToString().TrimEnd();
    }
}
