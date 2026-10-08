using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

#nullable disable

public sealed class RebirthNpcNavigationRuntimeAdapter : IRebirthNpcMovementAdapter
{
    private sealed class State
    {
        public int EntityId;
        public EntityRebirthNPC Actor;
        public Vector3 Destination;
        public Vector3 LastObservedPosition;
        public long LastRequestTicks;
        public long LastProgressTicks;
        public int ConsecutiveNoProgress;
        public int RecoveryAttempts;
        public string LastReason;
    }

    private const int MaximumTrackedActors = 512;
    private const int MaximumNoProgressRequests = 4;
    private const int MaximumRecoveryAttempts = 3;
    private const float MinimumProgressDistance = 0.35f;
    private static readonly long MinimumRepathTicks = TimeSpan.FromMilliseconds(750).Ticks;
    private static readonly long UnchangedRouteRefreshTicks = TimeSpan.FromSeconds(3).Ticks;
    private static readonly long StuckWindowTicks = TimeSpan.FromSeconds(6).Ticks;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, State> States = new Dictionary<int, State>();
    private static readonly RebirthNpcFairRoundRobin<int> Order = new RebirthNpcFairRoundRobin<int>();
    private static long requests, accepted, satisfied, rateLimited, progressResets,
        stuckDetections, recoveryAttempts, terminalFailures, stops, evictions, budgetDeferred;

    private readonly IRebirthNpcMovementAdapter inner;

    public RebirthNpcNavigationRuntimeAdapter(IRebirthNpcMovementAdapter innerAdapter)
    {
        inner = innerAdapter ?? throw new ArgumentNullException(nameof(innerAdapter));
    }

    public string AdapterId => "rebirth.navigation.runtime+" + inner.AdapterId;

    public RebirthNpcMovementRequestResult RequestMove(EntityRebirthNPC npc, Vector3 destination,
        float stopDistance, out string detail)
    {
        detail = string.Empty;
        if (npc == null)
        {
            detail = "Navigation actor is unavailable.";
            return RebirthNpcMovementRequestResult.TemporarilyUnavailable;
        }

        RebirthNpcSpatialIndex.Upsert(npc.entityId, npc.position.x, npc.position.y, npc.position.z);
        State state = GetOrCreate(npc, npc.position, destination);
        long now = DateTime.UtcNow.Ticks;
        bool destinationChanged = (state.Destination - destination).sqrMagnitude > 1f;
        if (destinationChanged)
        {
            state.Destination = destination;
            state.LastObservedPosition = npc.position;
            state.LastProgressTicks = now;
            state.ConsecutiveNoProgress = 0;
            state.RecoveryAttempts = 0;
            progressResets++;
        }

        long requestAge = now - state.LastRequestTicks;
        if (requestAge < MinimumRepathTicks)
        {
            rateLimited++;
            detail = "Navigation repath request is rate limited by the shared runtime policy.";
            return RebirthNpcMovementRequestResult.Accepted;
        }
        if (!destinationChanged && state.LastRequestTicks > 0 && requestAge < UnchangedRouteRefreshTicks)
        {
            rateLimited++;
            detail = "Existing native route retained until the bounded refresh interval.";
            return RebirthNpcMovementRequestResult.Accepted;
        }

        float progressSq = (npc.position - state.LastObservedPosition).sqrMagnitude;
        if (progressSq >= MinimumProgressDistance * MinimumProgressDistance)
        {
            state.LastObservedPosition = npc.position;
            state.LastProgressTicks = now;
            state.ConsecutiveNoProgress = 0;
            state.RecoveryAttempts = 0;
            progressResets++;
        }
        else if (now - state.LastProgressTicks >= StuckWindowTicks)
        {
            state.ConsecutiveNoProgress++;
            if (state.ConsecutiveNoProgress >= MaximumNoProgressRequests)
            {
                stuckDetections++;
                state.ConsecutiveNoProgress = 0;
                state.RecoveryAttempts++;
                recoveryAttempts++;
                inner.Stop(npc, "Navigation stuck recovery reset.");
                state.LastRequestTicks = now;
                state.LastProgressTicks = now;
                state.LastObservedPosition = npc.position;
                if (state.RecoveryAttempts > MaximumRecoveryAttempts)
                {
                    terminalFailures++;
                    state.LastReason = "Navigation exhausted the bounded stuck-recovery budget.";
                    detail = state.LastReason;
                    return RebirthNpcMovementRequestResult.Failed;
                }
                state.LastReason = "Navigation detected no progress and reset the native path before retry " +
                    state.RecoveryAttempts + "/" + MaximumRecoveryAttempts + ".";
            }
        }

        if (!RebirthNpcPathBudgetCoordinator.TryAcquire(RebirthNpcPathBudgetClass.Background, 1))
        {
            budgetDeferred++;
            detail = "Navigation request deferred by the shared path budget.";
            return RebirthNpcMovementRequestResult.TemporarilyUnavailable;
        }

        state.LastRequestTicks = now;
        requests++;
        RebirthNpcMovementRequestResult result = inner.RequestMove(npc, destination, stopDistance, out detail);
        if (result == RebirthNpcMovementRequestResult.Accepted) accepted++;
        else if (result == RebirthNpcMovementRequestResult.AlreadySatisfied) satisfied++;
        state.LastReason = detail ?? string.Empty;
        return result;
    }

    public void Stop(EntityRebirthNPC npc, string reason)
    {
        if (npc == null) return;
        inner.Stop(npc, reason);
        lock (Sync)
        {
            State current;
            if (States.TryGetValue(npc.entityId, out current) && object.ReferenceEquals(current.Actor, npc))
            {
                States.Remove(npc.entityId);
                Order.Remove(npc.entityId);
            }
        }
        RebirthNpcSpatialIndex.Remove(npc.entityId);
        stops++;
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            States.Clear();
            Order.Clear();
            requests = accepted = satisfied = rateLimited = progressResets = stuckDetections =
                recoveryAttempts = terminalFailures = stops = evictions = budgetDeferred = 0;
        }
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC Navigation Runtime] active=").Append(States.Count)
                .Append(" requests=").Append(requests).Append(" accepted=").Append(accepted)
                .Append(" satisfied=").Append(satisfied).Append(" rateLimited=").Append(rateLimited)
                .Append(" progressResets=").Append(progressResets).Append(" stuckDetections=").Append(stuckDetections)
                .Append(" recoveryAttempts=").Append(recoveryAttempts).Append(" terminalFailures=").Append(terminalFailures)
                .Append(" stops=").Append(stops).Append(" evictions=").Append(evictions)
                .Append(" budgetDeferred=").Append(budgetDeferred)
                .Append(" repathMs=750 unchangedRefreshMs=3000 stuckWindowSeconds=6 noProgressRequests=4 maxRecoveries=3 teleport=disabled");
            foreach (KeyValuePair<int, State> pair in States)
                b.AppendLine().Append("  entity=").Append(pair.Key).Append(" destination=")
                    .Append(pair.Value.Destination).Append(" noProgress=").Append(pair.Value.ConsecutiveNoProgress)
                    .Append(" recoveries=").Append(pair.Value.RecoveryAttempts).Append(" detail=")
                    .Append(pair.Value.LastReason);
            return b.ToString();
        }
    }

    private static State GetOrCreate(EntityRebirthNPC npc, Vector3 position, Vector3 destination)
    {
        int entityId = npc.entityId;
        lock (Sync)
        {
            State state;
            if (States.TryGetValue(entityId, out state))
            {
                if (object.ReferenceEquals(state.Actor, npc)) return state;
                // Entity ids are reusable. Never let the prior body's path/recovery history
                // govern a newly-created actor with the same numeric id.
                States.Remove(entityId);
                Order.Remove(entityId);
                evictions++;
            }
            state = new State
            {
                EntityId = entityId,
                Actor = npc,
                Destination = destination,
                LastObservedPosition = position,
                LastRequestTicks = 0,
                LastProgressTicks = DateTime.UtcNow.Ticks,
                LastReason = string.Empty
            };
            States.Add(entityId, state);
            Order.Add(entityId);
            while (States.Count > MaximumTrackedActors && Order.Count > 0)
            {
                int oldest;
                if(!Order.TryTake(out oldest))break;
                if (States.Remove(oldest)) evictions++;
            }
            return state;
        }
    }
}
