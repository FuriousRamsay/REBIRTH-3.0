using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public enum RebirthNpcWorkNavigationDisposition : byte
{
    InRange = 0, Moving = 1, TemporarilyUnavailable = 2, PermanentlyUnavailable = 3
}

public static class RebirthNpcWorkNavigationService
{
    private sealed class State
    {
        public ulong AssignmentId;
        public long LastRequestUtcTicks;
        public int ConsecutiveFailures;
        public string LastDetail;
        public RebirthNpcEmbodiedWorkIntent Intent;
        public EntityRebirthNPC Actor;
        public IRebirthNpcMovementAdapter Adapter;
    }

    private const int MaximumStates = 512;
    private static readonly object Sync = new object();
    private static readonly Dictionary<ulong, State> States = new Dictionary<ulong, State>();
    private static readonly RebirthNpcFairRoundRobin<ulong> Order = new RebirthNpcFairRoundRobin<ulong>();
    private static long requests, accepted, alreadySatisfied, temporaryFailures, permanentFailures;

    public static RebirthNpcWorkNavigationDisposition Request(RebirthNpcConcreteWorkContext context,
        out string detail)
    {
        detail = "Navigation refused: immutable original target witness and typed native movement ownership are unqualified.";
        return RebirthNpcWorkNavigationDisposition.TemporarilyUnavailable;
    }

    public static bool TryResolveNpc(RebirthNpcStableId stableId, out EntityRebirthNPC npc)
    {
        npc = null;
        int entityId;
        if (stableId.IsEmpty || !RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out entityId)) return false;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        npc = world != null ? world.GetEntity(entityId) as EntityRebirthNPC : null;
        return npc != null;
    }

    public static void Complete(ulong assignmentId, string reason)
    {
        // Neither an exact token nor void Stop proves typed original movement cancellation.
        // Retain navigation state; do not stop a possibly combat-owned route.
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder b = new StringBuilder("[REBIRTH NPC Work Navigation] active=").Append(States.Count)
                .Append(" requests=").Append(requests).Append(" accepted=").Append(accepted)
                .Append(" satisfied=").Append(alreadySatisfied).Append(" temporaryFailures=").Append(temporaryFailures)
                .Append(" permanentFailures=").Append(permanentFailures);
            foreach (KeyValuePair<ulong, State> pair in States)
                b.AppendLine().Append("  assignment=").Append(pair.Key).Append(" failures=")
                    .Append(pair.Value.ConsecutiveFailures).Append(" detail=").Append(pair.Value.LastDetail);
            return b.ToString();
        }
    }

    private static State GetOrCreate(ulong assignmentId)
    {
        lock (Sync)
        {
            State state;
            if (States.TryGetValue(assignmentId, out state)) return state;
            state = new State { AssignmentId = assignmentId };
            States.Add(assignmentId, state);
            Order.Add(assignmentId);
            while (States.Count > MaximumStates && Order.Count > 0)
            {
                ulong oldest;
                if(!Order.TryTake(out oldest))break;
                States.Remove(oldest);
            }
            return state;
        }
    }

    public static void ResetForWorldChange()
    {
        // Release hold: preserve original work custody; no native effects.
        if(!RebirthNpcWorkReleaseGate.Enabled)return;

        lock(Sync)
        {
            States.Clear();
            Order.Clear();
        }
    }

    private static void Record(ulong assignmentId, string detail, bool failure)
    {
        State state = GetOrCreate(assignmentId);
        state.LastDetail = detail ?? string.Empty;
        if (failure) state.ConsecutiveFailures++;
    }
}
