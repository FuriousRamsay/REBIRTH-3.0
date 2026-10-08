using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Route planning inside buildings. The terrain-aware walker steers straight at its target and around drops, so it cannot get to a
/// container in another room: it never goes through an interior door, up the stairs or round a corner.
///
/// This plans on the game's own colliders. Every 1 m column is asked for its walkable floors (upward-facing surfaces where a player
/// capsule fits); neighbouring columns connect when the floors are within a step (up to ~1 m up, 3 m down) and a capsule can move
/// between them. A closed door in the way is not an obstacle but a waypoint where the door is opened. A* then finds the way, and
/// FollowPath walks it, jumping up steps and opening doors on the way.
/// </summary>
public static class RebirthGameBridgePath
{
    public struct Step
    {
        public Vector3 Pos;        // feet, game space
        public bool Door;          // a closed door sits between the previous step and this one
        public Vector3i DoorBlock;
    }

    private const float Radius = 0.3f, Height = 1.62f;   // a little under the real 1.8 m: door frames and stair soffits leave less than a block of headroom
    public static string LastFailure = "";

    private static int Mask(EntityPlayerLocal p)
    {
        int m = Physics.DefaultRaycastLayers;
        try { m &= ~(1 << p.PhysicsTransform.gameObject.layer); } catch { }
        return m;
    }

    private static Vector3 U(Vector3 gamePos) { return gamePos - Origin.position; }          // game -> unity space
    private static Vector3 G(Vector3 unityPos) { return unityPos + Origin.position; }

    private static bool CapsuleFree(Vector3 feetUnity, int mask)
    {
        return !Physics.CheckCapsule(feetUnity + Vector3.up * (Radius + 0.12f), feetUnity + Vector3.up * (Height - Radius), Radius - 0.02f, mask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Walkable floor heights (game space y) in the column at (x, z), between yMin and yMax.</summary>
    private static List<float> Floors(EntityPlayerLocal p, int x, int z, float yMin, float yMax, int mask, Dictionary<long, List<float>> cache)
    {
        long key = ((long)x << 32) ^ (uint)z;
        List<float> list;
        if (cache.TryGetValue(key, out list)) return list;
        list = new List<float>();
        Vector3 top = U(new Vector3(x + 0.5f, yMax, z + 0.5f));
        RaycastHit[] hits = Physics.RaycastAll(top, Vector3.down, yMax - yMin + 1f, mask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
        float lastY = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            if (h.normal.y < 0.6f) continue;                            // not a floor
            float gy = h.point.y + Origin.position.y;
            if (lastY - gy < 1.7f) continue;                            // too close under the previous floor: no room to stand
            if (!CapsuleFree(new Vector3(h.point.x, h.point.y + 0.02f, h.point.z), mask)) continue;
            list.Add(gy); lastY = gy;
        }
        cache[key] = list;
        return list;
    }

    private static bool IsClosedDoorAt(EntityPlayerLocal p, Vector3 gamePoint, out Vector3i block)
    {
        block = new Vector3i(Mathf.FloorToInt(gamePoint.x), Mathf.FloorToInt(gamePoint.y), Mathf.FloorToInt(gamePoint.z));
        for (int dy = 0; dy <= 1; dy++)
        {
            Vector3i b = new Vector3i(block.x, block.y + dy, block.z);
            BlockValue bv = p.world.GetBlock(b.x, b.y, b.z);
            if (bv.isair || bv.Block == null) continue;
            if (bv.ischild) continue;
            string n = bv.Block.GetBlockName();
            if (n.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("trim", StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("shapes:", StringComparison.OrdinalIgnoreCase) < 0) { block = b; return true; }
        }
        return false;
    }

    /// <summary>Can a player walk from floor A to floor B (neighbouring columns)? doorBlock is set when a closed door is the only thing in the way.</summary>
    private static bool Connects(EntityPlayerLocal p, Vector3 a, Vector3 b, int mask, out bool door, out Vector3i doorBlock)
    {
        door = false; doorBlock = default(Vector3i);
        float dy = b.y - a.y;
        if (dy > 1.1f || dy < -3.2f) return false;
        // A real step (more than a hand's breadth) is swept along the slope, so a staircase does not clip the capsule; level ground is swept flat.
        Vector3 from, to;
        if (Mathf.Abs(dy) > 0.3f) { from = U(new Vector3(a.x, a.y + 0.22f, a.z)); to = U(new Vector3(b.x, b.y + 0.22f, b.z)); }
        else { float y = Mathf.Max(a.y, b.y) + 0.12f; from = U(new Vector3(a.x, y, a.z)); to = U(new Vector3(b.x, y, b.z)); }
        Vector3 dir = to - from; float dist = dir.magnitude; dir /= Mathf.Max(0.001f, dist);
        RaycastHit hit;
        if (!Physics.CapsuleCast(from + Vector3.up * Radius, from + Vector3.up * (Height - Radius - 0.12f), Radius - 0.02f, dir, out hit, dist, mask, QueryTriggerInteraction.Ignore))
            return true;
        Vector3i blk;
        if (IsClosedDoorAt(p, G(hit.point) + dir * 0.2f, out blk) || IsClosedDoorAt(p, G(hit.point), out blk)) { door = true; doorBlock = blk; return true; }
        return false;
    }

    /// <summary>Plan a walk from `from` to `to` (feet, game space). Null when no route exists within the search budget.</summary>
    public static List<Step> Plan(EntityPlayerLocal p, Vector3 from, Vector3 to, int maxNodes = 7000)
    {
        int mask = Mask(p);
        var cache = new Dictionary<long, List<float>>();
        float yMin = Mathf.Min(from.y, to.y) - 6f, yMax = Mathf.Max(from.y, to.y) + 12f;
        // start/goal floors: nearest floor to the given height in their column
        Func<Vector3, Vector3?> snap = pt =>
        {
            int cx = Mathf.FloorToInt(pt.x), cz = Mathf.FloorToInt(pt.z);
            Vector3? best = null; float bd = 99f;
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    foreach (float fy in Floors(p, cx + dx, cz + dz, yMin, yMax, mask, cache))
                    {
                        float d = Mathf.Abs(fy - pt.y) * 2f + new Vector2(cx + dx + 0.5f - pt.x, cz + dz + 0.5f - pt.z).magnitude;
                        if (d < bd) { bd = d; best = new Vector3(cx + dx + 0.5f, fy, cz + dz + 0.5f); }
                    }
            return best;
        };
        Vector3? s = snap(from), g = snap(to);
        if (!s.HasValue || !g.HasValue) { LastFailure = (!s.HasValue ? "no floor near the start " + from : "") + (!g.HasValue ? " no floor near the goal " + to : ""); return null; }

        var open = new List<Vector3>();
        var cost = new Dictionary<Vector3, float>();
        var prev = new Dictionary<Vector3, Vector3>();
        var doorAt = new Dictionary<Vector3, Vector3i>();
        var closed = new HashSet<Vector3>();
        open.Add(s.Value); cost[s.Value] = 0f;
        int expanded = 0;
        Vector3 goal = g.Value, found = default(Vector3); bool done = false;
        Vector3 goalForDebug = goal, closestAt = s.Value; float closestD = float.MaxValue;
        while (open.Count > 0 && expanded < maxNodes)
        {
            int bi = 0; float bf = float.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                float f = cost[open[i]] + (open[i] - goal).magnitude;
                if (f < bf) { bf = f; bi = i; }
            }
            Vector3 cur = open[bi]; open.RemoveAt(bi);
            if (!closed.Add(cur)) continue;
            expanded++;
            if ((cur - goalForDebug).sqrMagnitude < closestD) { closestD = (cur - goalForDebug).sqrMagnitude; closestAt = cur; }
            if (new Vector2(cur.x - goal.x, cur.z - goal.z).magnitude < 0.6f && Mathf.Abs(cur.y - goal.y) < 1.2f) { found = cur; done = true; break; }
            int cx = Mathf.FloorToInt(cur.x), cz = Mathf.FloorToInt(cur.z);
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    foreach (float fy in Floors(p, cx + dx, cz + dz, yMin, yMax, mask, cache))
                    {
                        var nb = new Vector3(cx + dx + 0.5f, fy, cz + dz + 0.5f);
                        if (closed.Contains(nb)) continue;
                        if (dx != 0 && dz != 0)
                        {
                            // diagonal: both orthogonal neighbours must be passable too (no cutting corners)
                            bool d1, d2; Vector3i b1, b2;
                            bool ok1 = false, ok2 = false;
                            foreach (float f1 in Floors(p, cx + dx, cz, yMin, yMax, mask, cache))
                                if (Mathf.Abs(f1 - cur.y) < 1.2f && Connects(p, cur, new Vector3(cx + dx + 0.5f, f1, cz + 0.5f), mask, out d1, out b1) && !d1) { ok1 = true; break; }
                            foreach (float f2 in Floors(p, cx, cz + dz, yMin, yMax, mask, cache))
                                if (Mathf.Abs(f2 - cur.y) < 1.2f && Connects(p, cur, new Vector3(cx + 0.5f, f2, cz + dz + 0.5f), mask, out d2, out b2) && !d2) { ok2 = true; break; }
                            if (!ok1 || !ok2) continue;
                        }
                        bool door; Vector3i db;
                        if (!Connects(p, cur, nb, mask, out door, out db)) continue;
                        float step = (nb - cur).magnitude + (door ? 4f : 0f) + (nb.y > cur.y + 0.6f ? 0.6f : 0f);
                        float nc = cost[cur] + step;
                        float oldc;
                        if (cost.TryGetValue(nb, out oldc) && oldc <= nc) continue;
                        cost[nb] = nc; prev[nb] = cur; if (door) doorAt[nb] = db; else doorAt.Remove(nb);
                        open.Add(nb);
                    }
                }
        }
        if (!done) { LastFailure = "no route: searched " + expanded + " spots (" + (expanded >= maxNodes ? "budget used up" : "everything reachable explored") + ") from " + s.Value + " towards " + goal + "; closest reached " + closestAt + " (" + Mathf.Sqrt(closestD).ToString("0.0") + " m from the goal); explored: " + string.Join(" ", new List<string>(System.Linq.Enumerable.Select(closed, c2 => Mathf.FloorToInt(c2.x) + "," + Mathf.FloorToInt(c2.y) + "," + Mathf.FloorToInt(c2.z))).ToArray()); return null; }
        var rev = new List<Step>();
        Vector3 c = found;
        while (true)
        {
            Vector3i dblk; bool isDoor = doorAt.TryGetValue(c, out dblk);
            rev.Add(new Step { Pos = c, Door = isDoor, DoorBlock = dblk });
            Vector3 pv;
            if (!prev.TryGetValue(c, out pv)) break;
            c = pv;
        }
        rev.Reverse();
        return rev;
    }

    /// <summary>Walk a planned route: keep moving, jump up steps, open doors where the plan says so. Returns true on arrival.</summary>
    public static IEnumerator FollowPath(EntityPlayerLocal p, List<Step> path, float finalRadius, float maxSeconds, Action<string> log, Action<bool> done)
    {
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        float end = Time.realtimeSinceStartup + maxSeconds;
        int i = 1;
        float lastProgress = Time.realtimeSinceStartup; Vector3 lastPos = p.position;
        bool ok = false;
        while (Time.realtimeSinceStartup < end && !p.IsDead() && i < path.Count)
        {
            Step st = path[i];
            if (st.Door && !RebirthGameBridgePlayer.DoorIsOpen(p, st.DoorBlock))
            {
                string r = null;
                yield return RebirthGameBridgePlayer.DoorRoutine(p, st.DoorBlock, m => r = m);
                if (r != null && r.StartsWith("couldn't")) log("door on the way: " + r);
            }
            Vector3 to = st.Pos - p.position; to.y = 0f;
            float reach = i == path.Count - 1 ? finalRadius : 0.55f;
            if (to.magnitude <= reach) { i++; lastProgress = Time.realtimeSinceStartup; continue; }
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            RebirthGameBridgePlayer.SmoothTurn(p, yaw, 0f, 0.08f);
            if (Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yaw)) < 30f) RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
            if (st.Pos.y - p.position.y > 0.45f && p.onGround && to.magnitude < 1.4f) RebirthGameBridgeInput.HoldFrames(a.Jump, 4);
            if ((p.position - lastPos).sqrMagnitude > 0.04f) { lastPos = p.position; lastProgress = Time.realtimeSinceStartup; }
            else if (Time.realtimeSinceStartup - lastProgress > 1.2f)
            {
                RebirthGameBridgeInput.HoldFrames(a.Jump, 5);
                if (Time.realtimeSinceStartup - lastProgress > 3.5f) { log("stuck on the way to " + st.Pos); break; }
            }
            yield return null;
        }
        ok = i >= path.Count;
        RebirthGameBridgeInput.Release(a.MoveForward);
        done(ok);
    }

    /// <summary>A floor spot to stand on to use a block: close (1-1.6 m), at about the block's own floor level (not on top of the counter it sits on).</summary>
    public static Vector3? StandSpot(EntityPlayerLocal p, Vector3i block)
    {
        int mask = Mask(p);
        var cache = new Dictionary<long, List<float>>();
        Vector3? best = null; float bs = float.MaxValue;
        for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                foreach (float fy in Floors(p, block.x + dx, block.z + dz, block.y - 3f, block.y + 3f, mask, cache))
                {
                    if (fy > block.y + 0.45f || fy < block.y - 1.4f) continue;
                    var spot = new Vector3(block.x + dx + 0.5f, fy, block.z + dz + 0.5f);
                    float d = new Vector2(spot.x - (block.x + 0.5f), spot.z - (block.z + 0.5f)).magnitude;
                    float score = Mathf.Abs(d - 1.2f) + Mathf.Abs(fy - block.y) * 0.5f;
                    if (score < bs) { bs = score; best = spot; }
                }
            }
        return best;
    }

    /// <summary>Plan and walk to a spot. Returns false when there is no route or the walk fails.</summary>
    public static IEnumerator GoTo(EntityPlayerLocal p, Vector3 target, float radius, float maxSeconds, Action<string> log, Action<bool> done)
    {
        List<Step> path = Plan(p, p.position, target);
        if (path == null) { log("no route to " + target + ": " + LastFailure); done(false); yield break; }
        bool ok = false;
        yield return FollowPath(p, path, radius, maxSeconds, log, r => ok = r);
        done(ok);
    }
    private static IEnumerator FollowPathOwned(EntityPlayerLocal p, List<Step> path, float finalRadius, float maxSeconds, Action<string> log, Action<bool> done, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        float end = Time.realtimeSinceStartup + maxSeconds;
        int i = 1;
        float lastProgress = Time.realtimeSinceStartup; Vector3 lastPos = p.position;
        bool ok = false;
        while (scope.Admitted && Time.realtimeSinceStartup < end && !p.IsDead() && i < path.Count)
        {
            Step st = path[i];
            if (st.Door && !RebirthGameBridgePlayer.DoorIsOpen(p, st.DoorBlock))
            {
                string r = null;
                yield return RebirthGameBridgePlayer.DoorRoutine(p, st.DoorBlock, m => r = m, scope);
                if (r != null && r.StartsWith("couldn't")) log("door on the way: " + r);
            }
            Vector3 to = st.Pos - p.position; to.y = 0f;
            float reach = i == path.Count - 1 ? finalRadius : 0.55f;
            if (to.magnitude <= reach) { i++; lastProgress = Time.realtimeSinceStartup; continue; }
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            if (!scope.Admitted) yield break;
            RebirthGameBridgePlayer.SmoothTurn(p, yaw, 0f, 0.08f);
            if (Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yaw)) < 30f) scope.TryHoldFrames(a.MoveForward, 1);
            if (st.Pos.y - p.position.y > 0.45f && p.onGround && to.magnitude < 1.4f) scope.TryHoldFrames(a.Jump, 4);
            if ((p.position - lastPos).sqrMagnitude > 0.04f) { lastPos = p.position; lastProgress = Time.realtimeSinceStartup; }
            else if (Time.realtimeSinceStartup - lastProgress > 1.2f)
            {
                scope.TryHoldFrames(a.Jump, 5);
                if (Time.realtimeSinceStartup - lastProgress > 3.5f) { log("stuck on the way to " + st.Pos); break; }
            }
            yield return null;
        }
        ok = i >= path.Count;
        scope.Release(a.MoveForward);
        done(ok);
    }
    private static IEnumerator GoToOwned(EntityPlayerLocal p, Vector3 target, float radius, float maxSeconds, Action<string> log, Action<bool> done, RebirthGameBridgeInput.OwnedInputScope scope)
    {
        if (!scope.Admitted) yield break;
        List<Step> path = Plan(p, p.position, target);
        if (path == null) { log("no route to " + target + ": " + LastFailure); done(false); yield break; }
        bool ok = false;
        yield return FollowPath(p, path, radius, maxSeconds, log, r => ok = r, scope);
        done(ok);
    }

    public static IEnumerator FollowPath(EntityPlayerLocal p, List<Step> path, float finalRadius, float maxSeconds, Action<string> log, Action<bool> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(FollowPathOwned(p, path, finalRadius, maxSeconds, log, ok => { completed = true; done(ok); }, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done(false); }
        }
    }
    public static IEnumerator GoTo(EntityPlayerLocal p, Vector3 target, float radius, float maxSeconds, Action<string> log, Action<bool> done, RebirthGameBridgeInput.OwnedInputScope parent)
    {
        bool completed = false;
        using (var scope = new RebirthGameBridgeInput.OwnedInputScope(p, () => parent != null && parent.Admitted, parent))
        {
            try { yield return RebirthGameBridgeUi.RunGuarded(GoToOwned(p, target, radius, maxSeconds, log, ok => { completed = true; done(ok); }, scope), scope); }
            finally { if (scope.CursorCustodyPending) parent?.MarkCursorCustodyPending(); else if (scope.Failure != null) parent?.Refuse(scope.Failure); if (!completed) done(false); }
        }
    }
}

