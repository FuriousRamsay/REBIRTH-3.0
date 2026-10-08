using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// World knowledge for the Game Bridge: points of interest (POIs = placed prefabs) and travelling to them.
///
///   GET  /pois?radius=400&amp;limit=10&amp;maxTier=5   nearest real POIs (tiny decorations skipped): name, label, tier,
///                                                  size, distance to the edge, direction, visited, inside
///   POST /gotopoi?id=N | name=substring [&amp;run=1]    travel to a POI's edge with terrain-aware walking
///
/// Exploring on its own (agenda): when idle, head for the nearest unvisited low-tier POI.
/// </summary>
public static class RebirthGameBridgeWorld
{
    private static readonly HashSet<int> visited = new HashSet<int>();
    private static PrefabInstance lastInside;
    private static World storageWorld;
    private static readonly Dictionary<int, RebirthGameBridgePoiStorage> poiStorage = new Dictionary<int, RebirthGameBridgePoiStorage>();
    private static bool lastLootComplete;
    private static JObject lastLootReport;

    public static bool TryDispatch(BridgeRequest req)
    {
        switch (req.Path)
        {
            case "/pois": Pois(req); return true;
            case "/gotopoi": GoToPoi(req); return true;
            case "/clearpoi": RunOnPoi(req, "clear"); return true;
            case "/lootpoi": RunOnPoi(req, "loot"); return true;
            case "/raidpoi": RunOnPoi(req, "raid"); return true;
            case "/enterpoi": RunOnPoi(req, "enter"); return true;
            case "/routeto": RouteTo(req); return true;
            case "/breakdoor": BreakDoorEndpoint(req); return true;
            default: return false;
        }
    }

    private static EntityPlayerLocal Player(BridgeRequest req)
    {
        EntityPlayerLocal p = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
        if (p == null) req.Fail("no local player", 409);
        return p;
    }

    /// <summary>A real POI (building, site), not a decoration or a tiny prefab.</summary>
    private static bool IsRealPoi(PrefabInstance pi)
    {
        if (pi == null || pi.prefab == null) return false;
        Vector3i s = pi.boundingBoxSize;
        if (s.x < 8 || s.z < 8) return false;
        string n = pi.prefab.PrefabName ?? pi.name ?? "";
        if (n.StartsWith("part_", StringComparison.OrdinalIgnoreCase) || n.StartsWith("deco", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("rwg_tile", StringComparison.OrdinalIgnoreCase) || n.IndexOf("filler", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    /// <summary>Flat distance from a point to a POI's bounding box (0 when inside).</summary>
    public static float DistanceTo(PrefabInstance pi, Vector3 pos)
    {
        Vector3 min = pi.boundingBoxPosition.ToVector3(), size = pi.boundingBoxSize.ToVector3();
        float dx = Mathf.Max(min.x - pos.x, 0f, pos.x - (min.x + size.x));
        float dz = Mathf.Max(min.z - pos.z, 0f, pos.z - (min.z + size.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>The point on the POI's bounds edge nearest to `pos`, just outside it, at ground level.</summary>
    public static Vector3 EdgePoint(PrefabInstance pi, Vector3 pos)
    {
        Vector3 min = pi.boundingBoxPosition.ToVector3(), size = pi.boundingBoxSize.ToVector3();
        float x = Mathf.Clamp(pos.x, min.x - 1f, min.x + size.x + 1f);
        float z = Mathf.Clamp(pos.z, min.z - 1f, min.z + size.z + 1f);
        return new Vector3(x, pos.y, z);
    }

    public static List<PrefabInstance> NearbyPois(Vector3 pos, float radius, int maxTier)
    {
        var list = new List<KeyValuePair<float, PrefabInstance>>();
        DynamicPrefabDecorator dec = GameManager.Instance.GetDynamicPrefabDecorator();
        if (dec == null || dec.allPrefabs == null) return new List<PrefabInstance>();
        foreach (PrefabInstance pi in dec.allPrefabs)
        {
            if (!IsRealPoi(pi) || pi.prefab.DifficultyTier > maxTier) continue;
            float d = DistanceTo(pi, pos);
            if (d <= radius) list.Add(new KeyValuePair<float, PrefabInstance>(d, pi));
        }
        list.Sort((a, b) => a.Key.CompareTo(b.Key));
        var result = new List<PrefabInstance>();
        foreach (var kv in list) result.Add(kv.Value);
        return result;
    }

    private static string Direction(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float yaw = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
        string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        return names[Mathf.RoundToInt(yaw / 45f) % 8];
    }

    public static JObject Describe(PrefabInstance pi, Vector3 pos)
    {
        Vector2 c = pi.GetCenterXZ();
        string label = null;
        try { label = pi.prefab.LocalizedName; } catch { }
        return new JObject
        {
            ["id"] = pi.id,
            ["name"] = pi.prefab.PrefabName,
            ["label"] = label,
            ["tier"] = pi.prefab.DifficultyTier,
            ["size"] = new JArray(pi.boundingBoxSize.x, pi.boundingBoxSize.y, pi.boundingBoxSize.z),
            ["center"] = new JArray(Mathf.Round(c.x), Mathf.Round(c.y)),
            ["distance"] = Mathf.Round(DistanceTo(pi, pos)),
            ["direction"] = Direction(pos, new Vector3(c.x, pos.y, c.y)),
            ["visited"] = visited.Contains(pi.id)
        };
    }

    private static void Pois(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        List<PrefabInstance> pois = NearbyPois(p.position, req.QueryFloat("radius", 400f), req.QueryInt("maxTier", 6));
        var arr = new JArray();
        int limit = req.QueryInt("limit", 10);
        for (int i = 0; i < pois.Count && i < limit; i++) arr.Add(Describe(pois[i], p.position));
        PrefabInstance inside = null;
        try { inside = GameManager.Instance.GetDynamicPrefabDecorator().GetPrefabFromWorldPosInside(Mathf.FloorToInt(p.position.x), Mathf.FloorToInt(p.position.z)); } catch { }
        req.Complete(new JObject { ["count"] = pois.Count, ["pois"] = arr, ["inside"] = inside != null && IsRealPoi(inside) ? Describe(inside, p.position) : null });
    }

    private static PrefabInstance Resolve(BridgeRequest req, EntityPlayerLocal p)
    {
        int id;
        List<PrefabInstance> near = NearbyPois(p.position, req.QueryFloat("radius", 1000f), 6);
        if (int.TryParse(req.QueryString("id"), out id)) return near.Find(pi => pi.id == id);
        string name = req.QueryString("name");
        if (name != null) return near.Find(pi => pi.prefab.PrefabName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
            || (pi.prefab.LocalizedName ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
        return near.Count > 0 ? near[0] : null;
    }

    private static void GoToPoi(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        PrefabInstance pi = Resolve(req, p);
        if (pi == null) { req.Fail("no matching POI nearby", 404); return; }
        lastLootReport = null;
        RebirthGameBridgePump.Instance.Run(GoToPoiRoutine(req, p, pi, req.QueryBool("run", true), msg => { }));
    }

    /// <summary>Travel to the POI's edge (terrain-aware walking, sprinting in bursts), then report.</summary>
    public static IEnumerator GoToPoiRoutine(BridgeRequest req, EntityPlayerLocal p, PrefabInstance pi, bool run, Action<string> log)
    {
        float start = Time.realtimeSinceStartup;
        string result = null;
        int interruptions = 0;
        for (int leg = 0; leg < 8 && result == null; leg++)
        {
            if (DistanceTo(pi, p.position) <= 3f) { result = "arrived"; break; }
            Vector3 edge = EdgePoint(pi, p.position);
            // Long trips in legs of ~60 m so progress (and trouble) is re-evaluated along the way.
            Vector3 to = edge - p.position; to.y = 0f;
            Vector3 waypoint = to.magnitude > 60f ? p.position + to.normalized * 60f : edge;
            string legResult = null;
            yield return RebirthGameBridgePlayer.WalkTo(p, waypoint, 2.5f, run, 45f, r => legResult = r);
            if (legResult == "cancelled" && interruptions < 10 && !p.IsDead())
            {
                // An instinct took over (a zombie, food about to burn): let it finish, then carry on.
                interruptions++;
                log("trip interrupted - dealing with it, then carrying on");
                yield return WaitForInstincts(p, 180f);
                leg--;
                continue;
            }
            if (legResult != "arrived") log("leg " + (leg + 1) + ": " + legResult + " (" + Mathf.Round(DistanceTo(pi, p.position)) + " m to go)");
            if (legResult != null && legResult != "arrived" && legResult.StartsWith("blocked") == false && legResult != "timeout") { result = legResult; break; }
            if (legResult != null && (legResult.StartsWith("blocked") || legResult == "stuck"))
            {
                // Detour: try a sideways waypoint, like a player going around an obstacle.
                Vector3 side = Quaternion.Euler(0f, leg % 2 == 0 ? 60f : -60f, 0f) * (to.sqrMagnitude > 0.01f ? to.normalized : Vector3.forward);
                string detour = null;
                yield return RebirthGameBridgePlayer.WalkTo(p, p.position + side * 15f, 2.5f, run, 20f, r => detour = r);
            }
        }
        if (result == null) result = DistanceTo(pi, p.position) <= 3f ? "arrived" : "could not reach it";
        if (result == "arrived") { visited.Add(pi.id); lastInside = pi; }
        log((result == "arrived" ? "arrived at " : "couldn't reach ") + (pi.prefab.LocalizedName ?? pi.prefab.PrefabName) + " (tier " + pi.prefab.DifficultyTier + ")");
        req.Complete(new JObject
        {
            ["result"] = result, ["poi"] = Describe(pi, p.position),
            ["seconds"] = Mathf.Round(Time.realtimeSinceStartup - start)
        }, result == "arrived" ? 200 : 409);
    }

    // ------------------------------------------------------------------ clear / loot / raid

    private static bool InsidePoi(PrefabInstance pi, Vector3 pos, float margin)
    {
        Vector3 min = pi.boundingBoxPosition.ToVector3(), size = pi.boundingBoxSize.ToVector3();
        return pos.x >= min.x - margin && pos.x <= min.x + size.x + margin && pos.z >= min.z - margin && pos.z <= min.z + size.z + margin
            && pos.y >= min.y - 4f && pos.y <= min.y + size.y + 4f;
    }

    private static List<EntityAlive> EnemiesInPoi(EntityPlayerLocal p, PrefabInstance pi)
    {
        var list = new List<EntityAlive>();
        foreach (Entity e in p.world.Entities.list)
        {
            var a = e as EntityAlive;
            if (a == null || a == p || a.IsDead() || a.Health <= 0 || !(a is EntityEnemy)) continue;
            if (InsidePoi(pi, a.position, 3f)) list.Add(a);
        }
        list.Sort((x, y) => (x.position - p.position).sqrMagnitude.CompareTo((y.position - p.position).sqrMagnitude));
        return list;
    }

    private static void RunOnPoi(BridgeRequest req, string what)
    {
        lastLootReport = null;
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        PrefabInstance pi = Resolve(req, p);
        if (pi == null) { req.Fail("no matching POI nearby", 404); return; }
        var events = new JArray();
        Action<string> log = m => { events.Add(DateTime.Now.ToString("HH:mm:ss") + " " + m); Log.Out("[REBIRTH GameBridge] raid: " + m); };
        IEnumerator routine = what == "enter" ? EnterPoiRoutine(p, pi, log) : what == "clear" ? ClearPoiRoutine(p, pi, log) : what == "loot" ? LootPoiRoutine(p, pi, log) : RaidPoiRoutine(p, pi, log);
        RebirthGameBridgePump.Instance.Run(Finish(req, routine, pi, p, events));
    }

    /// <summary>Get to the POI and walk into the building through a door (opening or breaking it).</summary>
    public static IEnumerator EnterPoiRoutine(EntityPlayerLocal p, PrefabInstance pi, Action<string> log)
    {
        yield return WaitForInstincts(p, 120f);
        if (DistanceTo(pi, p.position) > 3f)
            yield return GoToPoiRoutine(new BridgeRequest { Method = "POST", Path = "/gotopoi" }, p, pi, true, log);
        bool ok = false;
        yield return RebirthGameBridgeEntry.EnterRoutine(p, pi, log, r => ok = r);
        log(ok ? "I'm inside" : "still outside");
    }

    /// <summary>POST /routeto?x=&y=&z= : plan a route on the game's colliders (doors, stairs) and walk it. Reports the result and why a plan failed.</summary>
    private static void RouteTo(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        var target = new Vector3(req.QueryFloat("x", p.position.x), req.QueryFloat("y", p.position.y), req.QueryFloat("z", p.position.z));
        var events = new JArray();
        Action<string> log = m => { events.Add(DateTime.Now.ToString("HH:mm:ss") + " " + m); Log.Out("[REBIRTH GameBridge] route: " + m); };
        RebirthGameBridgePump.Instance.Run(RouteRoutine(req, p, target, events, log));
    }

    private static IEnumerator RouteRoutine(BridgeRequest req, EntityPlayerLocal p, Vector3 target, JArray events, Action<string> log)
    {
        float t0 = Time.realtimeSinceStartup;
        var path = RebirthGameBridgePath.Plan(p, p.position, target);
        float planMs = (Time.realtimeSinceStartup - t0) * 1000f;
        if (path == null) { req.Complete(new JObject { ["ok"] = false, ["planMs"] = planMs, ["failure"] = RebirthGameBridgePath.LastFailure, ["events"] = events }, 409); yield break; }
        int doors = 0; foreach (var s in path) if (s.Door) doors++;
        bool ok = false;
        yield return RebirthGameBridgePath.FollowPath(p, path, 0.5f, req.QueryFloat("maxSeconds", 60f), log, r => ok = r);
        req.Complete(new JObject { ["ok"] = ok, ["steps"] = path.Count, ["doors"] = doors, ["planMs"] = planMs, ["position"] = new JArray(p.position.x, p.position.y, p.position.z), ["events"] = events }, ok ? 200 : 409);
    }

    /// <summary>POST /breakdoor?x=&y=&z= : stand in front of that door and break it down (limit 1500 hp; pickaxe for metal, fireaxe for wood). For testing.</summary>
    private static void BreakDoorEndpoint(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        var pos = new Vector3i(Mathf.FloorToInt(req.QueryFloat("x", 0f)), Mathf.FloorToInt(req.QueryFloat("y", 0f)), Mathf.FloorToInt(req.QueryFloat("z", 0f)));
        RebirthGameBridgePump.Instance.Run(BreakDoorRoutineRequest(req, p, pos));
    }

    private static IEnumerator BreakDoorRoutineRequest(BridgeRequest req, EntityPlayerLocal p, Vector3i pos)
    {
        string msg = null;
        float t0 = Time.realtimeSinceStartup;
        string walked = null;
        yield return RebirthGameBridgePlayer.WalkTo(p, new Vector3(pos.x + 0.5f, p.position.y, pos.z + 0.5f), 1.3f, false, 40f, m => walked = m);
        yield return RebirthGameBridgePlayer.BreakDoorRoutine(p, pos, m => msg = m);
        req.Complete(new JObject { ["result"] = msg, ["hp"] = RebirthGameBridgePlayer.BlockHitPoints(p, pos), ["seconds"] = Time.realtimeSinceStartup - t0, ["gone"] = p.world.GetBlock(pos.x, pos.y, pos.z).isair });
    }

    private static IEnumerator Finish(BridgeRequest req, IEnumerator routine, PrefabInstance pi, EntityPlayerLocal p, JArray events)
    {
        yield return routine;
        req.Complete(new JObject { ["poi"] = Describe(pi, p.position), ["remainingEnemies"] = EnemiesInPoi(p, pi).Count,
            ["clearVerified"] = NativeClearVerified(p, pi), ["sleeperVolumes"] = SleeperStatus(pi),
            ["loot"] = lastLootReport, ["events"] = events });
    }

    private static JArray SleeperStatus(PrefabInstance pi)
    {
        var result = new JArray();
        foreach (var volume in pi.sleeperVolumes)
            result.Add(new JObject { ["cleared"] = volume.wasCleared, ["spawned"] = volume.isSpawned,
                ["spawning"] = volume.isSpawning, ["remaining"] = volume.respawnMap.Count });
        return result;
    }

    // An empty entity scan outside a building says nothing about untriggered sleeper volumes.
    // Require every authored volume to be registered and explicitly cleared by native gameplay.
    private static bool NativeClearVerified(EntityPlayerLocal p, PrefabInstance pi)
    {
        int authored = pi.prefab.SleeperVolumeList.Count;
        if (authored == 0 || pi.sleeperVolumes.Count < authored || EnemiesInPoi(p, pi).Count != 0) return false;
        foreach (var volume in pi.sleeperVolumes)
            if (!volume.wasCleared || volume.isSpawning || volume.pendingSpawnMap.Count != 0) return false;
        return true;
    }

    /// <summary>
    /// A raid is in charge right now: it deals with zombies itself (one at a time), so the idle "turn to watch"
    /// instinct stays out of its way instead of fighting it for the camera. Heartbeat-based so a stopped
    /// routine can't leave it stuck on.
    /// </summary>
    public static bool RaidActive { get { return Time.realtimeSinceStartup - raidHeartbeat < 5f; } }
    private static float raidHeartbeat = -100f;
    private static void Beat() { raidHeartbeat = Time.realtimeSinceStartup; }

    /// <summary>Wait until no instinct is running (eating, a fight, taking cooked food) before starting something.</summary>
    public static IEnumerator WaitForInstincts(EntityPlayerLocal p, float maxSeconds)
    {
        float end = Time.realtimeSinceStartup + maxSeconds;
        float calm = 0f;
        while (Time.realtimeSinceStartup < end && !p.IsDead())
        {
            Beat();
            bool busy = RebirthGameBridgeCombat.InstinctBusy || RebirthGameBridgePlayer.IsMoving;
            calm = busy ? 0f : calm + Time.unscaledDeltaTime;
            if (calm > 0.4f) yield break;
            yield return null;
        }
    }

    /// <summary>
    /// Awake zombies coming for us are handled first, one at a time (nearest first). When several are close at
    /// once, pull back first so they string out and arrive one by one - that's how a player draws them out.
    /// </summary>
    private static IEnumerator DealWithThreats(EntityPlayerLocal p, Action<string> log)
    {
        for (int round = 0; round < 8 && !p.IsDead(); round++)
        {
            yield return WaitForInstincts(p, 120f);
            List<EntityAlive> coming = RebirthGameBridgeCombat.AwakeThreats(p, 25f, true);
            if (coming.Count == 0) yield break;
            int close = coming.FindAll(e => (e.position - p.position).magnitude < 10f).Count;
            if (close >= 3 || (close >= 2 && p.Health < p.GetMaxHealth() * 0.6f))
            {
                log(close + " zombies close at once - pulling back so they come one at a time");
                yield return RebirthGameBridgeCombat.PullBack(p, 12f);
                coming = RebirthGameBridgeCombat.AwakeThreats(p, 25f, true);
                if (coming.Count == 0) yield break;
            }
            EntityAlive t = coming[0];
            log("dealing with " + Cls(t) + " first (" + Mathf.Round((t.position - p.position).magnitude) + " m" + (coming.Count > 1 ? ", " + (coming.Count - 1) + " more around" : "") + ")");
            string r = null;
            yield return RebirthGameBridgeCombat.FightOne(p, t, x => r = x);
            Beat();
            log(Cls(t) + ": " + r);
            if (p.IsDead()) yield break;
        }
    }

    private static string Cls(EntityAlive e) { return e != null && e.EntityClass != null ? e.EntityClass.entityClassName : "zombie"; }

    /// <summary>Kill every zombie in the POI: awake ones coming at us first, then sleepers (sneaking up on them).</summary>
    public static IEnumerator ClearPoiRoutine(EntityPlayerLocal p, PrefabInstance pi, Action<string> log)
    {
        float end = Time.realtimeSinceStartup + 60f;
        var unreachable = new HashSet<int>();
        int before = EnemiesInPoi(p, pi).Count;
        while (Time.realtimeSinceStartup < end && !p.IsDead())
        {
            Beat();
            yield return DealWithThreats(p, log);
            if (p.IsDead()) yield break;
            EntityAlive target = EnemiesInPoi(p, pi).Find(e => !unreachable.Contains(e.entityId));
            if (target == null) break;
            bool asleep = target.IsSleeping;
            log("going for " + Cls(target) + (asleep ? " (asleep - sneaking up)" : "") + ", " + Mathf.Round((target.position - p.position).magnitude) + " m");
            PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
            // A sleeper is sniped from range when we have a bow (sneak up crouched to ~14 m, shoot from there, melee only if it gets close).
            float stopAt = RebirthGameBridgeCombat.IsRangedPublic(p.inventory.holdingItem) ? 12f
                : asleep && RebirthGameBridgeCombat.HasBow(p) ? 14f : 2.2f;
            if (asleep) RebirthGameBridgeInput.HoldSeconds(a.Crouch, 60f);
            string walk = null;
            if ((target.position - p.position).magnitude > stopAt + 0.3f)
                yield return RebirthGameBridgePlayer.WalkTo(p, target.position, stopAt, !asleep, 15f, r => walk = r);
            RebirthGameBridgeInput.Release(a.Crouch);
            Beat();
            if (walk == "cancelled") continue; // an instinct took over (it woke up / something came at us): re-plan
            if (target.IsDead()) continue;
            if ((target.position - p.position).magnitude > stopAt + 2f)
            {
                log("couldn't get to " + Cls(target) + " (" + walk + ") - leaving it for now");
                unreachable.Add(target.entityId);
                continue;
            }
            string fight = null;
            yield return RebirthGameBridgeCombat.FightOne(p, target, r => fight = r);
            log(Cls(target) + ": " + fight);
            if (!target.IsDead() && fight == "obstructed") unreachable.Add(target.entityId);
        }
        int left = EnemiesInPoi(p, pi).Count;
        log(NativeClearVerified(p, pi) ? "POI cleared: native sleeper volumes verified"
            : "clear not verified: " + left + " loaded enemies left (" + unreachable.Count + " unreachable); untriggered sleeper volumes may remain");
    }

    private struct LootSpot { public Vector3i Pos; public TEFeatureStorage Loot; public string Name; }

    private static List<LootSpot> LootInPoi(EntityPlayerLocal p, PrefabInstance pi)
    {
        var list = new List<LootSpot>();
        Vector3i min = pi.boundingBoxPosition, size = pi.boundingBoxSize;
        for (int cx = World.toChunkXZ(min.x - 1); cx <= World.toChunkXZ(min.x + size.x + 1); cx++)
            for (int cz = World.toChunkXZ(min.z - 1); cz <= World.toChunkXZ(min.z + size.z + 1); cz++)
            {
                var chunk = p.world.GetChunkSync(cx, cz) as Chunk;
                if (chunk == null) continue;
                foreach (TileEntity te in chunk.GetTileEntities().list)
                {
                    TEFeatureStorage loot;
                    if (te == null || !te.TryGetSelfOrFeature(out loot) || loot == null) continue;
                    Vector3i pos = te.ToWorldPos();
                    if (!InsidePoi(pi, pos.ToVector3(), 1f)) continue;
                    RebirthGameBridgePoiStorage storage;
                    if (storageWorld == p.world && poiStorage.TryGetValue(pi.id, out storage) && storage.Contains(p, pos)) continue;
                    if (loot.ItemGrid.Touched && loot.IsEmpty()) continue; // already looted
                    BlockValue bv = p.world.GetBlock(pos.x, pos.y, pos.z);
                    list.Add(new LootSpot { Pos = pos, Loot = loot, Name = bv.Block != null ? bv.Block.GetBlockName() : "container" });
                }
            }
        return list;
    }

    private static bool IsSealedLootCover(BlockValue block)
    {
        if (block.isair || block.ischild || block.Block == null || block.Block.DowngradeBlock.isair) return false;
        string name = block.Block.GetBlockName();
        return name.StartsWith("cntShippingCrate", System.StringComparison.Ordinal)
            || name.StartsWith("hiddenSafePictureFrame_", System.StringComparison.Ordinal)
            || name.StartsWith("hiddenSafePainting", System.StringComparison.Ordinal)
            || name == "cntLootWeapons" || name == "cntLootTools";
    }

    // Wrappers have no loot tile entity. Yield while scanning so large POIs do not freeze a frame.
    private static IEnumerator FindSealedCrates(EntityPlayerLocal p, PrefabInstance pi, List<LootSpot> spots)
    {
        Vector3i min = pi.boundingBoxPosition, size = pi.boundingBoxSize;
        var known = new HashSet<Vector3i>();
        foreach (LootSpot spot in spots) known.Add(spot.Pos);
        int work = 0;
        for (int x = min.x; x < min.x + size.x; x++)
            for (int z = min.z; z < min.z + size.z; z++)
            {
                if (!(p.world.GetChunkSync(World.toChunkXZ(x), World.toChunkXZ(z)) is Chunk)) continue;
                for (int y = System.Math.Max(0, min.y); y < System.Math.Min(256, min.y + size.y); y++)
                {
                    if (p.IsDead()) yield break;
                    var pos = new Vector3i(x, y, z);
                    BlockValue block = p.world.GetBlock(x, y, z);
                    if (IsSealedLootCover(block) && known.Add(pos))
                        spots.Add(new LootSpot { Pos = pos, Name = block.Block.GetBlockName() });
                    if (++work % 2048 == 0) { Beat(); yield return null; }
                }
            }
    }

    private static IEnumerator OpenLootCover(EntityPlayerLocal p, Vector3i pos, Action<string> log)
    {
        BlockValue original = p.world.GetBlock(pos.x, pos.y, pos.z);
        if (!IsSealedLootCover(original)) yield break;
        int previous = p.inventory.holdingItemIdx;
        System.Func<ItemClass, bool> axe = ic => ic != null &&
            (ic.GetItemName() == "meleeToolRepairT0StoneAxe" || ic.GetItemName() == "meleeToolRepairT0TazasStoneAxe"
            || ic.GetItemName() == "meleeToolAxeT1IronFireaxe" || ic.GetItemName() == "meleeToolAxeT2SteelAxe");
        yield return RebirthGameBridgeNeeds.Prepare(p, axe, null, "an axe to open the loot cover", log);
        yield return RebirthGameBridgeUi.CloseMenus();
        string name;
        int slot = RebirthGameBridgeNeeds.ToolbeltSlot(p, axe, out name);
        if (slot < 0) yield break;
        bool equipped = false;
        yield return RebirthGameBridgeNeeds.Equip(p, slot, name, ok => equipped = ok);
        if (!equipped) yield break;
        var action = RebirthGameBridgeInput.LocalActions().Primary;
        float end = Time.realtimeSinceStartup + 40f;
        try
        {
            while (Time.realtimeSinceStartup < end && !p.IsDead()
                && p.world.GetBlock(pos.x, pos.y, pos.z).type == original.type)
            {
                Beat();
                if (RebirthGameBridgeCombat.AwakeThreats(p, 3.5f, false).Count > 0) break;
                RebirthGameBridgePlayer.SmoothAimAt(p, pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f), 0.1f);
                var hit = p.HitInfo;
                if (hit != null && hit.bHitValid && hit.hit.blockPos == pos && !RebirthGameBridgeNeeds.HandsBusy(p))
                    RebirthGameBridgeInput.HoldFrames(action, 1);
                else RebirthGameBridgeInput.Release(action);
                yield return null;
            }
        }
        finally { RebirthGameBridgeInput.Release(action); }
        if (!p.IsDead() && previous >= 0 && previous != slot)
            yield return RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { });
    }

    private static IEnumerator PickLootLock(EntityPlayerLocal p, Vector3i pos, Action<string> log)
    {
        float deadline = Time.realtimeSinceStartup + 180f;
        int attempts = 0;
        while (!p.IsDead() && Time.realtimeSinceStartup < deadline)
        {
            Beat();
            if (RebirthGameBridgeCombat.AwakeThreats(p, 3.5f, false).Count > 0)
            {
                yield return RebirthGameBridgeUi.CloseMenus();
                yield break;
            }
            // E cancels the native timer: never reactivate while a pick is in progress.
            if (RebirthGameBridgeUi.IsWindowOpen("timer")) { yield return null; continue; }
            var composite = p.world.GetTileEntity(pos) as TileEntityComposite;
            var feature = composite != null ? composite.GetFeature<TEFeatureLockPickable>() : null;
            if (feature == null || !feature.NeedsLockpicking()) yield break;
            var ui = LocalPlayerUI.GetUIForPlayer(p);
            ItemValue pick = ItemClass.GetItem(feature.lockPickItem);
            if (pick == null || pick.IsEmpty() || ui == null || ui.xui == null
                || ui.xui.PlayerInventory.GetItemCount(pick) <= 0)
            { log("locked container needs " + feature.lockPickItem); yield break; }
            if (++attempts > 15) { log("lockpick retry limit reached; container remains incomplete"); yield break; }
            yield return RebirthGameBridgePlayer.ApproachAndActivate(p, pos, log, 1.2f, 5f);
            // Allow server lock acknowledgement and native window creation before another activation.
            float wait = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < wait && !RebirthGameBridgeUi.IsWindowOpen("timer")) yield return null;
        }
        if (RebirthGameBridgeUi.IsWindowOpen("timer")) yield return RebirthGameBridgeUi.CloseMenus();
    }

    /// <summary>Resolve the currently open target by position: opening can replace its tile entity.</summary>
    private static TEFeatureStorage OpenLoot(EntityPlayerLocal p, Vector3i expectedPosition)
    {
        try
        {
            var ui = LocalPlayerUI.GetUIForPlayer(p);
            var loot = ui != null && ui.xui != null ? ui.xui.LootContainer : null;
            if (LootWindowOpen() && loot != null && loot.ToWorldPos().Equals(expectedPosition))
            {
                var tile = p?.world?.GetTileEntity(expectedPosition);
                TEFeatureStorage current;
                if (tile != null && tile.TryGetSelfOrFeature(out current)
                    && ReferenceEquals(loot, current)) return loot;
            }
        }
        catch { }
        // A stale scan snapshot or a different open container cannot verify this target.
        return null;
    }

    private static bool CanTakeInspectedLoot(EntityPlayerLocal player, Vector3i position, TEFeatureStorage inspected)
    {
        return player != null && !player.IsDead() && inspected != null
            && RebirthGameBridgeCombat.AwakeThreats(player, 3.5f, false).Count == 0
            && ReferenceEquals(inspected, OpenLoot(player, position));
    }
    private static bool IsLootRunComplete(bool dead, int skipped, int remaining, bool chunksLoaded, string storageFailure)
    {
        return !dead && skipped == 0 && remaining == 0 && chunksLoaded && storageFailure == null;
    }

    private static int CountItems(TEFeatureStorage loot, List<string> names)
    {
        int n = 0;
        try
        {
            foreach (ItemStack s in loot.ItemGrid.items)
                if (s != null && !s.IsEmpty()) { n += s.count; if (names != null && s.itemValue.ItemClass != null) names.Add(s.itemValue.ItemClass.GetLocalizedItemName() + (s.count > 1 ? " x" + s.count : "")); }
        }
        catch { return -1; } // An unreadable container is not evidence that it is empty.
        return n;
    }

    private static bool LootWindowOpen() { return RebirthGameBridgeUi.IsWindowOpen("bagStorage") || RebirthGameBridgeUi.IsWindowOpen("looting") || RebirthGameBridgeUi.IsWindowOpen("windowLooting"); }

    /// <summary>
    /// Loot every container in the POI, nearest first: walk up, E, wait out the search, LOOK at what's inside
    /// (at least 2 s, even when empty), take it all with the loot window's Take All, verify, close.
    /// A zombie right next to us interrupts: close, deal with it, come back to that container later.
    /// </summary>
    public static IEnumerator LootPoiRoutine(EntityPlayerLocal p, PrefabInstance pi, Action<string> log)
    {
        lastLootComplete = false;
        lastLootReport = null;
        if (storageWorld != p.world) { poiStorage.Clear(); storageWorld = p.world; }
        RebirthGameBridgePoiStorage storage;
        if (!poiStorage.TryGetValue(pi.id, out storage)) poiStorage[pi.id] = storage = new RebirthGameBridgePoiStorage();
        yield return storage.RefreshExpectations(p, pi);
        bool storageChunksLoaded = storage.Recover(p, pi);
        List<LootSpot> spots = LootInPoi(p, pi);
        yield return FindSealedCrates(p, pi, spots);
        log(spots.Count + " container(s) to loot");
        int looted = 0, skipped = 0, empty = 0;
        int storageThreatRetries = 0;
        string storageFailure = null; // This run only: a prior attempt may have failed before recovery.
        var done = new HashSet<Vector3i>();
        var interruptionCount = new Dictionary<Vector3i, int>();
        var stalledTransferCount = new Dictionary<Vector3i, int>();
        float end = Time.realtimeSinceStartup + 1500f;
        while (Time.realtimeSinceStartup < end && !p.IsDead())
        {
            Beat();
            yield return DealWithThreats(p, log);
            if (p.IsDead()) yield break;
            storageFailure = storage.CustodyFailure(p);
            if (storageFailure != null) break;
            if (RebirthGameBridgeLootBags.Encumbered(p))
            {
                bool stored = false;
                yield return storage.Deposit(p, pi, log, ok => stored = ok);
                if (!stored)
                {
                    if (storage.RetryAfterThreat && ++storageThreatRetries <= 3)
                    {
                        log("storage interrupted by an awake threat - resolving threats before retry " + storageThreatRetries);
                        continue; // Existing DealWithThreats at the loop top; retain this container's progress.
                    }
                    storageFailure = storage.Failure ?? "storage deposit did not complete";
                    break;
                }
            }
            LootSpot next = default(LootSpot); float best = float.MaxValue; bool any = false;
            foreach (LootSpot s in spots)
            {
                if (done.Contains(s.Pos)) continue;
                float d = (s.Pos.ToVector3() - p.position).sqrMagnitude;
                if (d < best) { best = d; next = s; any = true; }
            }
            if (!any)
            {
                // Movement may load another POI chunk or reveal a replaced loot container.
                // Refresh before declaring there is nothing left to visit.
                List<LootSpot> discovered = LootInPoi(p, pi);
                yield return FindSealedCrates(p, pi, discovered);
                bool foundNew = false;
                foreach (LootSpot candidate in discovered)
                    if (!done.Contains(candidate.Pos)) { foundNew = true; break; }
                if (!foundNew) break;
                spots = discovered;
                continue;
            }
            bool needsStorage = false;
            bool transferProgress = false;

            // Activation range is shorter than the prompt range: stand close (1.2 m, then closer) and press E again if nothing opens.
            // Plan a route (doors, stairs, corners) to a spot in front of it, then the final close approach.
            Vector3? spot = RebirthGameBridgePath.StandSpot(p, next.Pos);
            if (spot.HasValue && new Vector2(spot.Value.x - p.position.x, spot.Value.z - p.position.z).magnitude > 0.9f)
            {
                bool routed = false;
                yield return RebirthGameBridgePath.GoTo(p, spot.Value, 0.5f, 45f, log, ok => routed = ok);
                if (!routed) log("no clean route to " + next.Name + " - trying the plain walk");
            }
            yield return RebirthGameBridgePlayer.ApproachAndActivate(p, next.Pos, log, 1.2f, 15f);
            Beat();
            if (!LootWindowOpen() && Vector3.Distance(p.position, next.Pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f)) > 3.2f)
            {
                log("can't get to " + next.Name + " @" + next.Pos.x + "," + next.Pos.y + "," + next.Pos.z + " (walk: " + RebirthGameBridgePlayer.LastWalkResult + ") - skipping it");
                done.Add(next.Pos); skipped++;
                continue;
            }
            if (!LootWindowOpen() && IsSealedLootCover(p.world.GetBlock(next.Pos.x, next.Pos.y, next.Pos.z)))
            {
                yield return OpenLootCover(p, next.Pos, log);
                yield return RebirthGameBridgePlayer.ApproachAndActivate(p, next.Pos, log, 1.2f, 10f);
            }
            if (!LootWindowOpen())
            {
                yield return PickLootLock(p, next.Pos, log);
                if (!p.IsDead() && !LootWindowOpen() && RebirthGameBridgeCombat.AwakeThreats(p, 3.5f, false).Count == 0)
                    yield return RebirthGameBridgePlayer.ApproachAndActivate(p, next.Pos, log, 1.2f, 5f);
            }
            for (int retry = 0; retry < 2 && !LootWindowOpen(); retry++)
            {
                float w = Time.realtimeSinceStartup + 2.5f;
                while (Time.realtimeSinceStartup < w && !LootWindowOpen()) yield return null;
                if (LootWindowOpen() || RebirthGameBridgeCombat.AwakeThreats(p, 3.5f, false).Count > 0) break;
                yield return RebirthGameBridgePlayer.ApproachAndActivate(p, next.Pos, log, 0.7f - 0.3f * retry);
            }
            // Untouched containers are searched first (a timer), then the loot window opens.
            float until = Time.realtimeSinceStartup + 10f;
            bool interrupted = false;
            while (Time.realtimeSinceStartup < until && !LootWindowOpen())
            {
                if (RebirthGameBridgeCombat.AwakeThreats(p, 3.5f, false).Count > 0) { interrupted = true; break; }
                yield return null;
            }
            if (!interrupted && !LootWindowOpen())
            {
                string prompt = "";
                try { BlockValue bv = p.world.GetBlock(next.Pos.x, next.Pos.y, next.Pos.z); prompt = RebirthGameBridgePlayer.StripColors(bv.Block.GetActivationText(p.world, bv, next.Pos, p)); } catch { }
                yield return RebirthGameBridgeUi.CloseMenus();
                log("couldn't open " + next.Name + (prompt.Length > 0 ? " (\"" + prompt.Replace("\n", " ").Trim() + "\")" : "") + " - leaving it incomplete (unreachable, still locked, or unopened)");
                done.Add(next.Pos); skipped++;
                continue;
            }

            if (!interrupted)
            {
                // Look at what's in there before grabbing anything (at least 2 s, even if it's empty).
                var names = new List<string>();
                TEFeatureStorage inspectedLoot = OpenLoot(p, next.Pos);
                int inside = CountItems(inspectedLoot, names);
                if (inside < 0)
                {
                    log("could not read " + next.Name + " inventory; leaving incomplete");
                    yield return RebirthGameBridgeUi.CloseMenus();
                    done.Add(next.Pos); skipped++;
                    continue;
                }
                float lookUntil = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < lookUntil)
                {
                    if (RebirthGameBridgeCombat.AwakeThreats(p, 3.5f, false).Count > 0) { interrupted = true; break; }
                    Beat();
                    yield return null;
                }
                if (!interrupted)
                {
                    log("opened " + next.Name + ": " + inside + " item(s) inside" + (names.Count > 0 ? " (" + string.Join(", ", names.ToArray()) + ")" : ""));
                    if (inside == 0) { log(next.Name + ": empty"); empty++; }
                    else
                    {
                        if (!CanTakeInspectedLoot(p, next.Pos, inspectedLoot))
                        {
                            log("loot target changed before Take All; leaving incomplete");
                            yield return RebirthGameBridgeUi.CloseMenus();
                            skipped++;
                            break;
                        }
                        Vector2 pt;
                        if (RebirthGameBridgeUi.TryFindById("windowLooting", "btnMoveAll", out pt) || RebirthGameBridgeUi.TryFindById("looting", "btnMoveAll", out pt))
                        {
                            bool refused = false;
                            yield return RebirthGameBridgeUi.ClickAt(pt, () =>
                            {
                                Vector2 currentPoint;
                                bool sameButton = (RebirthGameBridgeUi.TryFindById("windowLooting", "btnMoveAll", out currentPoint)
                                    || RebirthGameBridgeUi.TryFindById("looting", "btnMoveAll", out currentPoint))
                                    && Mathf.Abs(currentPoint.x - pt.x) <= .5f && Mathf.Abs(currentPoint.y - pt.y) <= .5f;
                                refused = !CanTakeInspectedLoot(p, next.Pos, inspectedLoot) || !sameButton;
                                return !refused;
                            });
                            if (refused)
                            {
                                log("Take All target, button or safety state changed before input; leaving incomplete");
                                yield return RebirthGameBridgeUi.CloseMenus();
                                skipped++;
                                break;
                            }
                            yield return Wait(0.8f); // see it land in the backpack
                        }
                        else
                        {
                            // The loot window shows "[R] to take everything from a container": press it like a player.
                            foreach (var ra in RebirthGameBridgeInput.FindByKey("R")) RebirthGameBridgeInput.HoldFrames(ra, 3);   // every action bound to R (the loot window listens on its own action set)
                            yield return Wait(1.0f);
                        }
                        int leftIn = ReferenceEquals(inspectedLoot, OpenLoot(p, next.Pos)) ? CountItems(inspectedLoot, null) : -1;
                        log("  after taking: " + leftIn + " left in " + next.Name);
                        if (leftIn < 0)
                        {
                            log("container inventory became unreadable; transfer not verified");
                            yield return RebirthGameBridgeUi.CloseMenus();
                            skipped++;
                            break;
                        }
                        if (leftIn == 0) { log("looted " + next.Name + ": " + string.Join(", ", names.ToArray())); looted++; }
                        else if (leftIn < inside) { log("took what fit from " + next.Name + " (" + leftIn + " left - returning after storage)"); needsStorage = true; transferProgress = true; }
                        else { log("couldn't take anything from " + next.Name + "; attempting a storage trip"); needsStorage = true; }
                    }
                }
            }
            RebirthGameBridgeInput.ReleaseCursor();
            yield return RebirthGameBridgeUi.CloseMenus();
            if (interrupted)
            {
                int n;
                interruptionCount.TryGetValue(next.Pos, out n);
                interruptionCount[next.Pos] = n + 1;
                if (n + 1 >= 3) { done.Add(next.Pos); skipped++; }
                log("zombie right next to me - leaving " + next.Name + " for later");
                continue; // DealWithThreats at the top of the loop, then back to it (nearest first)
            }
            if (needsStorage)
            {
                // Successful partial transfers may legitimately need several storage trips.
                // Only consecutive no-progress transfers consume this retry budget;
                // combat interruptions have their own independent allowance.
                int n; stalledTransferCount.TryGetValue(next.Pos, out n);
                n = transferProgress ? 0 : n + 1;
                stalledTransferCount[next.Pos] = n;
                if (n >= 4) { log("container still not transferring after storage; leaving incomplete"); skipped++; break; }
                bool stored = false;
                yield return storage.Deposit(p, pi, log, ok => stored = ok);
                if (!stored)
                {
                    if (storage.RetryAfterThreat && ++storageThreatRetries <= 3)
                    {
                        log("storage interrupted by an awake threat - resolving threats before retry " + storageThreatRetries);
                        continue; // Existing DealWithThreats at the loop top; retain this container's progress.
                    }
                    storageFailure = storage.Failure ?? "storage deposit did not complete";
                    break;
                }
                continue; // Do not mark a partially emptied container done.
            }
            done.Add(next.Pos);
        }
        yield return storage.RefreshExpectations(p, pi);
        storageChunksLoaded = storage.Recover(p, pi);
        List<LootSpot> remaining = LootInPoi(p, pi);
        yield return FindSealedCrates(p, pi, remaining);
        bool chunksLoaded = true;
        Vector3i min = pi.boundingBoxPosition, size = pi.boundingBoxSize;
        for (int cx = World.toChunkXZ(min.x - 1); cx <= World.toChunkXZ(min.x + size.x + 1); cx++)
            for (int cz = World.toChunkXZ(min.z - 1); cz <= World.toChunkXZ(min.z + size.z + 1); cz++)
                if (!(p.world.GetChunkSync(cx, cz) is Chunk)) chunksLoaded = false;
        chunksLoaded = chunksLoaded && storageChunksLoaded;
        if (storageFailure == null) storageFailure = storage.CustodyFailure(p);
        lastLootComplete = IsLootRunComplete(p.IsDead(), skipped, remaining.Count, chunksLoaded, storageFailure);
        var cratePositions = new JArray();
        foreach (Vector3i pos in storage.Crates) cratePositions.Add(new JArray(pos.x, pos.y, pos.z));
        lastLootReport = new JObject { ["poiId"] = pi.id, ["complete"] = lastLootComplete,
            ["emptied"] = looted, ["alreadyEmpty"] = empty, ["skipped"] = skipped,
            ["remainingContainers"] = remaining.Count, ["allChunksLoaded"] = chunksLoaded, ["storageFailure"] = storageFailure,
            ["storageCrates"] = cratePositions, ["allStorageChunksLoaded"] = storageChunksLoaded };
        log((lastLootComplete ? "looting verified complete: " : "looting incomplete: ") + looted + " looted, " + empty + " empty, " + skipped + " skipped, " + remaining.Count + " remaining");
    }

    private static IEnumerator Wait(float s)
    {
        float end = Time.realtimeSinceStartup + s;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    /// <summary>Go to the POI, clear it, loot it.</summary>
    public static IEnumerator RaidPoiRoutine(EntityPlayerLocal p, PrefabInstance pi, Action<string> log)
    {
        if (RebirthGameBridgeCombat.InstinctBusy) log("finishing what I'm doing first");
        yield return WaitForInstincts(p, 180f);
        yield return RebirthGameBridgeNeeds.WaitHandsFree(p, 3f);
        string label = pi.prefab.LocalizedName ?? pi.prefab.PrefabName;
        log("raiding " + label + " (tier " + pi.prefab.DifficultyTier + ")");
        if (DistanceTo(pi, p.position) > 3f)
            yield return GoToPoiRoutine(new BridgeRequest { Method = "POST", Path = "/gotopoi" }, p, pi, true, log);
        if (p.IsDead()) yield break;
        if (DistanceTo(pi, p.position) > 6f) { log("raid called off: never got there"); yield break; }
        bool inside = false;
        yield return RebirthGameBridgeEntry.EnterRoutine(p, pi, log, r => inside = r);
        if (p.IsDead()) yield break;
        yield return ClearPoiRoutine(p, pi, log);
        if (p.IsDead()) yield break;
        yield return LootPoiRoutine(p, pi, log);
        if (lastLootComplete && NativeClearVerified(p, pi)) visited.Add(pi.id);
        log(lastLootComplete && NativeClearVerified(p, pi) ? "finished with " + label : "raid incomplete: " + label);
    }

    // ------------------------------------------------------------------ explore (agenda)

    public static bool ExploreEnabled = false; // opt-in: guard explore=1 (would wander off during scenarios)
    private static float nextExploreAt;

    /// <summary>Idle with nothing else to do: head for the nearest unvisited low-tier POI.</summary>
    public static IEnumerator ExploreTick(EntityPlayerLocal p, Action<string> log)
    {
        if (!ExploreEnabled || Time.realtimeSinceStartup < nextExploreAt) return null;
        nextExploreAt = Time.realtimeSinceStartup + 20f;
        PrefabInstance target = null;
        foreach (PrefabInstance pi in NearbyPois(p.position, 400f, 1))
            if (!visited.Contains(pi.id) && DistanceTo(pi, p.position) > 3f) { target = pi; break; }
        if (target == null) return null;
        log("nothing else to do - heading to " + (target.prefab.LocalizedName ?? target.prefab.PrefabName) + " (tier " + target.prefab.DifficultyTier + ", "
            + Mathf.Round(DistanceTo(target, p.position)) + " m " + Direction(p.position, new Vector3(target.GetCenterXZ().x, p.position.y, target.GetCenterXZ().y)) + ")");
        return RaidPoiRoutine(p, target, log);
    }
}
