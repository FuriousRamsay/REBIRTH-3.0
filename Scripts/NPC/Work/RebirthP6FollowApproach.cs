using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

// P6 follow-compatible approach (B11 Smart Looting). A following NPC may be asked to walk to ONE block
// position near its owner, then hand control back to Follow. Follow is not an EAI task (it is the 1 s
// RebirthNpcFollowRuntime controller tick), so arbitration is explicit: while a request is Moving the Follow
// tick yields (YieldsFollow) and the EAI task EAIRebirthP6FollowApproach owns the path. Follow only PAUSES
// (D-25): its lease, order and session are never touched, and a request that ends for any reason is gone.
// A new attempt gets a NEW operation id; nothing resumes under an old id. Nothing is persisted.
internal static class RebirthP6FollowApproach
{
    // Closed until the Follow yield hook is installed (see the package doc) and the bridge proves it.
    internal const bool FollowApproachQualified = false;
    internal const bool FollowYieldHookInstalled = false;

    internal const float ArriveMeters = 2.5f;      // inside RebirthP11FollowLootDispatcher.ReachMeters (3 m)
    internal const float MaxFromOwnerMeters = 10f; // container to owner
    internal const float MaxFromNpcMeters = 16f;   // container to NPC at request time
    internal const float OwnerKeepUpMeters = 12f;  // same keep-up bound as the dispatcher
    internal const float TimeoutSeconds = 15f;
    internal const float StallSeconds = 4f;
    internal const float StallMeters = 0.35f;
    internal const int MaxRequests = 64;

    internal enum Phase : byte { None = 0, Moving = 1, Arrived = 2, Failed = 3 }

    private sealed class Request
    {
        internal ulong Id; internal Vector3i Block; internal Vector3 Goal; internal int OwnerEntityId;
        internal Phase Phase; internal float StartedAt, ProgressAt; internal Vector3 ProgressPos;
        internal string Detail;
    }

    private static readonly Dictionary<int, Request> requests = new Dictionary<int, Request>();
    private static World requestWorld;
    private static ulong nextId;

    internal static bool Active =>
        FollowApproachQualified && FollowYieldHookInstalled && RebirthNpcQualifiedWorkGate.InfrastructureEnabled;

    private static bool CurrentWorld()
    {
        World world = GameManager.Instance?.World;
        if (!ReferenceEquals(world, requestWorld)) { requestWorld = world; requests.Clear(); }
        return world != null && !world.IsRemote();
    }

    // Every reader (YieldsFollow/WantsEai/Step/Cancel/Consume) must drop a previous world's table first, otherwise an
    // entity id reused in a new world would inherit a stale Moving request and Follow would yield to nothing.
    private static bool Live() { return CurrentWorld() && requests.Count != 0; }

    private const float FinishedKeepSeconds = 60f;
    private const float SweepSeconds = 5f;
    private static float nextSweep;

    // A Moving request is owned by the EAI task. If that task never runs (mutex bit held by another task, NPC unloaded,
    // task list changed) nothing else would end the request and Follow would yield forever, so every reader enforces
    // the deadline (timeout + stall window) itself.
    private static bool Expired(Request r) { return Time.time - r.StartedAt > TimeoutSeconds + StallSeconds; }

    // Bounded housekeeping (at most MaxRequests entries, every 5 s): NPC dead/unloaded/removed -> entry dropped;
    // overdue Moving -> Failed; unconsumed terminal entries older than 60 s -> dropped. Other NPCs' live requests untouched.
    private static void Sweep()
    {
        if (Time.time < nextSweep || requests.Count == 0) return;
        nextSweep = Time.time + SweepSeconds;
        World w = GameManager.Instance?.World;
        List<int> drop = null;
        foreach (var kv in requests)
        {
            var alive = w?.GetEntity(kv.Key) as EntityAlive;
            if (alive == null || alive.IsDead()) { (drop ?? (drop = new List<int>())).Add(kv.Key); continue; }
            var r = kv.Value;
            if (r.Phase == Phase.Moving) { if (Expired(r)) { r.Phase = Phase.Failed; r.Detail = "Approach expired."; } }
            else if (Time.time - r.StartedAt > FinishedKeepSeconds) (drop ?? (drop = new List<int>())).Add(kv.Key);
        }
        if (drop != null) for (int i = 0; i < drop.Count; i++) requests.Remove(drop[i]);
    }

    // Server/main thread. Returns the new operation id; 0 when refused.
    internal static ulong TryRequest(EntityPlayer owner, EntityRebirthNPC npc, Vector3i block, out string detail)
    {
        detail = "Follow approach is closed.";
        if (!Active || !CurrentWorld() || !ThreadManager.IsMainThread()) return 0;
        if (npc == null || owner == null || npc.IsDead() || owner.IsDead() || !RebirthP6WorkFollowState.IsFollowing(npc))
        { detail = "NPC is not following a live owner."; return 0; }
        if (RebirthHumanAiPolicy.HasCombat(npc) || npc.GetRevengeTarget() != null)
        { detail = "Combat has priority."; return 0; }
        Vector3 goal = new Vector3(block.x + 0.5f, block.y, block.z + 0.5f);
        if ((owner.position - goal).sqrMagnitude > MaxFromOwnerMeters * MaxFromOwnerMeters ||
            (npc.position - goal).sqrMagnitude > MaxFromNpcMeters * MaxFromNpcMeters ||
            (owner.position - npc.position).sqrMagnitude > OwnerKeepUpMeters * OwnerKeepUpMeters)
        { detail = "Container is outside the bounded approach distance."; return 0; }
        if (requests.TryGetValue(npc.entityId, out var existing) && existing.Phase == Phase.Moving)
        { detail = "An approach is already running."; return 0; }
        if (requests.Count >= MaxRequests && !requests.ContainsKey(npc.entityId))
        {
            // Never clear other NPCs requests: retire only entries whose NPC is gone, otherwise refuse.
            World w = GameManager.Instance?.World;
            List<int> gone = null;
            foreach (var kv in requests)
                if (!(w?.GetEntity(kv.Key) is EntityAlive alive) || alive.IsDead()) (gone ?? (gone = new List<int>())).Add(kv.Key);
            if (gone != null) for (int i = 0; i < gone.Count; i++) requests.Remove(gone[i]);
            if (requests.Count >= MaxRequests) { detail = "Too many active approaches."; return 0; }
        }
        var request = new Request
        {
            Id = ++nextId, Block = block, Goal = goal, OwnerEntityId = owner.entityId, Phase = Phase.Moving,
            StartedAt = Time.time, ProgressAt = Time.time, ProgressPos = npc.position
        };
        requests[npc.entityId] = request;
        detail = string.Empty;
        return request.Id;
    }

    // Terminal states stay readable until the caller consumes them (Consume) or a new request replaces them.
    internal static Phase Status(EntityRebirthNPC npc, out ulong id, out Vector3i block)
    {
        id = 0; block = default;
        if (npc == null || !CurrentWorld() || !requests.TryGetValue(npc.entityId, out var r)) return Phase.None;
        id = r.Id; block = r.Block; return r.Phase;
    }

    internal static void Consume(EntityRebirthNPC npc, ulong id)
    {
        if (npc != null && Live() && requests.TryGetValue(npc.entityId, out var r) && r.Id == id && r.Phase != Phase.Moving)
            requests.Remove(npc.entityId);
    }

    // Cancels only a running request (looting started elsewhere, owner left, gate closed).
    internal static void Cancel(EntityRebirthNPC npc, string reason)
    {
        if (npc != null && Live() && requests.TryGetValue(npc.entityId, out var r) && r.Phase == Phase.Moving)
        { r.Phase = Phase.Failed; r.Detail = reason; }
    }

    // Read by the Follow tick: true while an approach owns this NPC's path.
    internal static bool YieldsFollow(EntityRebirthNPC npc)
    {
        if (npc == null || !Live()) return false;
        Sweep();
        if (!requests.TryGetValue(npc.entityId, out var r) || r.Phase != Phase.Moving) return false;
        if (npc.IsDead() || Expired(r)) { r.Phase = Phase.Failed; r.Detail = "Approach expired or NPC gone."; return false; }
        return Active;
    }

    internal static bool WantsEai(EntityRebirthNPC npc)
    {
        if (npc == null || !Live()) return false;
        if (!requests.TryGetValue(npc.entityId, out var r) || r.Phase != Phase.Moving) return false;
        if (npc.IsDead() || Expired(r)) { r.Phase = Phase.Failed; r.Detail = "Approach expired or NPC gone."; return false; }
        if (!Active) return false;
        if (!RebirthP6WorkFollowState.IsFollowing(npc) || RebirthHumanAiPolicy.HasCombat(npc) || npc.GetRevengeTarget() != null)
        { r.Phase = Phase.Failed; r.Detail = "Combat or order change took priority."; return false; }
        return true;
    }

    // One EAI step (called from EAIRebirthP6FollowApproach.Update at ~0.75 s). Returns the phase after the step.
    internal static Phase Step(EntityRebirthNPC npc)
    {
        if (npc == null || !Live() || !requests.TryGetValue(npc.entityId, out var r) || r.Phase != Phase.Moving) return Phase.None;
        float now = Time.time;
        var owner = GameManager.Instance.World.GetEntity(r.OwnerEntityId) as EntityPlayer;
        if (owner == null || owner.IsDead() || (owner.position - r.Goal).sqrMagnitude > MaxFromOwnerMeters * MaxFromOwnerMeters ||
            now - r.StartedAt > TimeoutSeconds)
        { Fail(npc, r, "Owner moved away or approach timed out."); return r.Phase; }
        if ((npc.position - r.ProgressPos).sqrMagnitude > StallMeters * StallMeters) { r.ProgressPos = npc.position; r.ProgressAt = now; }
        else if (now - r.ProgressAt > StallSeconds) { Fail(npc, r, "Approach made no progress."); return r.Phase; }
        var adapter = RebirthNpcMovementAdapterRegistry.Current;
        if (adapter == null) { Fail(npc, r, "No movement adapter."); return r.Phase; }
        RebirthNpcMovementRequestResult result;
        try { result = adapter.RequestMove(npc, r.Goal, ArriveMeters, out string detail); r.Detail = detail; }
        catch (Exception ex) { Fail(npc, r, "Movement request failed: " + ex.Message); return r.Phase; }
        if (result == RebirthNpcMovementRequestResult.AlreadySatisfied)
        {
            try { adapter.Stop(npc, "Follow approach arrived."); } catch { }
            r.Phase = Phase.Arrived; return r.Phase;
        }
        if (result == RebirthNpcMovementRequestResult.Unsupported || result == RebirthNpcMovementRequestResult.Failed)
            Fail(npc, r, r.Detail);
        return r.Phase;
    }

    private static void Fail(EntityRebirthNPC npc, Request r, string detail)
    {
        r.Phase = Phase.Failed; r.Detail = detail;
        // Follow's own tick re-paths within a second; only drop the approach path when no combat route is live.
        if (!RebirthHumanAiPolicy.HasCombat(npc))
            try { RebirthNpcMovementAdapterRegistry.Current?.Stop(npc, "Follow approach ended."); } catch { }
    }

    internal static string GetReport() => "[REBIRTH P6 follow approach] requests=" + requests.Count + " qualified=" + FollowApproachQualified;
}
