using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Getting INTO a building. Looting a POI from outside the walls goes nowhere: the containers are inside and the walker only steers
/// around holes and walls, it does not plan a route through a door. This finds the doors of a prefab, works out which side of each is
/// outside and which is inside, and walks outside -> door -> inside (opening the door, or breaking through when it is locked), trying
/// the next entrance when one fails.
/// </summary>
public static class RebirthGameBridgeEntry
{
    public struct Entrance
    {
        public Vector3i Door;
        public Vector3 Outside, Inside;
        public string Name;
        public bool Roofed;      // the inside point has a roof over it
    }

    private static bool Solid(EntityPlayerLocal p, int x, int y, int z)
    {
        BlockValue bv = p.world.GetBlock(x, y, z);
        return !bv.isair;
    }

    /// <summary>Is there a roof over this point (a solid block within 12 blocks straight up)?</summary>
    public static bool Roofed(EntityPlayerLocal p, Vector3 pt)
    {
        int x = Mathf.FloorToInt(pt.x), y = Mathf.FloorToInt(pt.y), z = Mathf.FloorToInt(pt.z);
        for (int dy = 2; dy <= 12; dy++) if (Solid(p, x, y + dy, z)) return true;
        return false;
    }

    /// <summary>Indoors: roofed, and inside the prefab's footprint.</summary>
    public static bool IsIndoors(EntityPlayerLocal p, PrefabInstance pi)
    {
        Vector3i bp = pi.boundingBoxPosition, bs = pi.boundingBoxSize;
        Vector3 q = p.position;
        bool inside = q.x >= bp.x && q.x <= bp.x + bs.x && q.z >= bp.z && q.z <= bp.z + bs.z;
        return inside && Roofed(p, q + Vector3.up);
    }

    private static bool Standable(EntityPlayerLocal p, int x, int y, int z)
    {
        return !Solid(p, x, y, z) && !Solid(p, x, y + 1, z) && Solid(p, x, y - 1, z);
    }

    private static bool IsEntranceName(string n)
    {
        if (n.IndexOf("door", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("gate", StringComparison.OrdinalIgnoreCase) < 0
            && n.IndexOf("hatch", StringComparison.OrdinalIgnoreCase) < 0) return false;
        if (n.IndexOf("trim", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("frame", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("interior", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("cabinet", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("shapes:", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("vault", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        return true;
    }

    /// <summary>All doors of the prefab with a standable spot on both sides, best entrances first (exterior, roofed inside, close to us).</summary>
    public static List<Entrance> FindEntrances(EntityPlayerLocal p, PrefabInstance pi)
    {
        var list = new List<Entrance>();
        Vector3i bp = pi.boundingBoxPosition, bs = pi.boundingBoxSize;
        Vector3 center = new Vector3(bp.x + bs.x * 0.5f, 0f, bp.z + bs.z * 0.5f);
        for (int x = bp.x; x < bp.x + bs.x; x++)
            for (int z = bp.z; z < bp.z + bs.z; z++)
                for (int y = bp.y; y < bp.y + bs.y; y++)
                {
                    BlockValue bv = p.world.GetBlock(x, y, z);
                    if (bv.isair || bv.ischild || bv.Block == null) continue;
                    string name = bv.Block.GetBlockName();
                    if (!IsEntranceName(name)) continue;
                    // The lower half of a door only (the upper half is a child of it); skip a door whose block below is the same door.
                    BlockValue below = p.world.GetBlock(x, y - 1, z);
                    if (!below.isair && below.Block != null && below.Block.GetBlockName() == name) continue;
                    foreach (Vector3i axis in new[] { new Vector3i(1, 0, 0), new Vector3i(0, 0, 1) })
                    {
                        for (int reach = 2; reach <= 3; reach++)
                        {
                            Vector3i a = new Vector3i(x + axis.x * reach, y, z + axis.z * reach), b = new Vector3i(x - axis.x * reach, y, z - axis.z * reach);
                            if (!Standable(p, a.x, a.y, a.z) || !Standable(p, b.x, b.y, b.z)) continue;
                            Vector3 pa = new Vector3(a.x + 0.5f, a.y, a.z + 0.5f), pb = new Vector3(b.x + 0.5f, b.y, b.z + 0.5f);
                            // Outside = the side further from the middle of the prefab, unless one side is under a roof and the other is not.
                            bool roofA = Roofed(p, pa + Vector3.up), roofB = Roofed(p, pb + Vector3.up);
                            Vector3 outside, inside; bool roofedInside;
                            if (roofA != roofB) { outside = roofA ? pb : pa; inside = roofA ? pa : pb; roofedInside = true; }
                            else
                            {
                                float da = (new Vector3(pa.x, 0f, pa.z) - center).sqrMagnitude, db = (new Vector3(pb.x, 0f, pb.z) - center).sqrMagnitude;
                                outside = da > db ? pa : pb; inside = da > db ? pb : pa; roofedInside = roofA;
                            }
                            list.Add(new Entrance { Door = new Vector3i(x, y, z), Outside = outside, Inside = inside, Name = name, Roofed = roofedInside });
                            goto nextDoor;
                        }
                    }
                nextDoor:;
                }
        Vector3 me = p.position;
        int minY = int.MaxValue; foreach (Entrance e in list) minY = Math.Min(minY, e.Door.y);
        list.Sort((l, r) =>
        {
            float sl = (l.Roofed ? 0f : 40f) + (l.Name.IndexOf("exterior", StringComparison.OrdinalIgnoreCase) >= 0 ? 0f : 15f) + (l.Outside - me).magnitude * 0.3f + (l.Door.y - minY) * 6f;
            float sr = (r.Roofed ? 0f : 40f) + (r.Name.IndexOf("exterior", StringComparison.OrdinalIgnoreCase) >= 0 ? 0f : 15f) + (r.Outside - me).magnitude * 0.3f + (r.Door.y - minY) * 6f;
            return sl.CompareTo(sr);
        });
        return list;
    }

    /// <summary>
    /// Walk in a straight line (no steering around walls): for the last metres through a doorway, where the terrain-aware walker
    /// goes looking for a way around the frame instead of through the gap. Jumps when it stops making progress.
    /// </summary>
    public static IEnumerator WalkStraight(EntityPlayerLocal p, Vector3 target, float radius, float maxSeconds, Action<bool> done)
    {
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        float end = Time.realtimeSinceStartup + maxSeconds, lastMoveT = Time.realtimeSinceStartup;
        Vector3 lastPos = p.position;
        bool ok = false;
        while (Time.realtimeSinceStartup < end && !p.IsDead())
        {
            Vector3 to = target - p.position; to.y = 0f;
            if (to.magnitude <= radius) { ok = true; break; }
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            RebirthGameBridgePlayer.SmoothTurn(p, yaw, 0f, 0.1f);
            if (Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yaw)) < 25f) RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
            if ((p.position - lastPos).sqrMagnitude > 0.04f) { lastPos = p.position; lastMoveT = Time.realtimeSinceStartup; }
            else if (Time.realtimeSinceStartup - lastMoveT > 0.7f) { RebirthGameBridgeInput.HoldFrames(a.Jump, 5); lastMoveT = Time.realtimeSinceStartup; }
            yield return null;
        }
        RebirthGameBridgeInput.Release(a.MoveForward);
        done(ok);
    }

    /// <summary>Walk into the building through a door. Returns when indoors or when every entrance has been tried.</summary>
    public static IEnumerator EnterRoutine(EntityPlayerLocal p, PrefabInstance pi, Action<string> log, Action<bool> done)
    {
        if (IsIndoors(p, pi)) { done(true); yield break; }
        List<Entrance> entrances = FindEntrances(p, pi);
        log(entrances.Count + " way(s) in found: " + string.Join(", ", entrances.ConvertAll(e => e.Name + " @" + e.Door.x + "," + e.Door.y + "," + e.Door.z).ToArray()));
        int tried = 0;
        foreach (Entrance e in entrances)
        {
            if (++tried > 5 || p.IsDead()) break;
            string r = null;
            r = null;
            for (int tryWalk = 0; tryWalk < 3 && r != "arrived"; tryWalk++)
                yield return RebirthGameBridgePlayer.WalkTo(p, e.Outside, 0.8f, false, 40f, m => r = m);
            if (r != "arrived") { log("couldn't get to the outside of " + e.Name + " (" + r + ")"); continue; }
            // Doors only answer E from close by (about 1.5 m): step up to the door first.
            Vector3 doorCenter = new Vector3(e.Door.x + 0.5f, e.Door.y, e.Door.z + 0.5f);
            Vector3 away = e.Outside - doorCenter; away.y = 0f;
            Vector3 doorFront = doorCenter + away.normalized * 1.0f; doorFront.y = e.Outside.y;
            bool closeOk = false;
            yield return WalkStraight(p, doorFront, 0.25f, 6f, ok => closeOk = ok);
            string doorMsg = null;
            yield return RebirthGameBridgePlayer.DoorRoutine(p, e.Door, m => doorMsg = m);
            log(e.Name + ": " + doorMsg);
            bool straightOk = false;
            Vector3 doorC = new Vector3(e.Door.x + 0.5f, e.Door.y, e.Door.z + 0.5f);
            Vector3 lineUp = e.Outside; lineUp.x = Mathf.Abs(e.Outside.x - doorC.x) > Mathf.Abs(e.Outside.z - doorC.z) ? e.Outside.x : doorC.x; lineUp.z = Mathf.Abs(e.Outside.x - doorC.x) > Mathf.Abs(e.Outside.z - doorC.z) ? doorC.z : e.Outside.z;
            yield return WalkStraight(p, lineUp, 0.25f, 8f, ok => straightOk = ok);     // exactly in front of the gap
            yield return WalkStraight(p, doorC, 0.35f, 8f, ok => straightOk = ok);      // through it
            yield return WalkStraight(p, e.Inside, 0.5f, 8f, ok => straightOk = ok);
            r = straightOk ? "arrived" : "timeout";
            if (r == "arrived" && IsIndoors(p, pi)) { log("inside through " + e.Name); done(true); yield break; }
            log("walked to the inside of " + e.Name + " but I'm not indoors (" + r + ")");
        }
        // Nothing opened after looking around: break a door down (not over 1500 hp; pickaxe for metal, fireaxe for wood).
        log("looked around - no door opened; breaking through a locked one");
        foreach (Entrance e in entrances)
        {
            if (p.IsDead()) break;
            string doorPrompt = "";
            try { BlockValue db = p.world.GetBlock(e.Door.x, e.Door.y, e.Door.z); doorPrompt = RebirthGameBridgePlayer.StripColors(db.Block.GetActivationText(p.world, db, e.Door, p)); } catch { }
            bool locked = doorPrompt.IndexOf("Locked", StringComparison.Ordinal) >= 0 && doorPrompt.IndexOf("Unlocked", StringComparison.Ordinal) < 0;
            if (!locked) { log(e.Name + ": not locked - breaking it down is only for locked doors (the problem is getting to it)"); continue; }
            int hp = RebirthGameBridgePlayer.BlockHitPoints(p, e.Door);
            if (hp <= 0 || hp > 1500) { log(e.Name + ": " + (hp <= 0 ? "no strength data" : "too sturdy (" + hp + " hp)") + " - not breaking it"); continue; }
            string r = null;
            for (int tryWalk = 0; tryWalk < 2 && r != "arrived"; tryWalk++)
                yield return RebirthGameBridgePlayer.WalkTo(p, e.Outside, 0.8f, false, 40f, m => r = m);
            if (r != "arrived") continue;
            Vector3 dc = new Vector3(e.Door.x + 0.5f, e.Door.y, e.Door.z + 0.5f);
            Vector3 aw = e.Outside - dc; aw.y = 0f;
            Vector3 front = dc + aw.normalized * 1.0f; front.y = e.Outside.y;
            bool ok0 = false;
            yield return WalkStraight(p, front, 0.25f, 6f, ok => ok0 = ok);
            string msg = null;
            yield return RebirthGameBridgePlayer.BreakDoorRoutine(p, e.Door, m => msg = m);
            log(e.Name + ": " + msg);
            yield return WalkStraight(p, dc, 0.35f, 8f, ok => ok0 = ok);
            yield return WalkStraight(p, e.Inside, 0.5f, 8f, ok => ok0 = ok);
            if (ok0 && IsIndoors(p, pi)) { log("inside after breaking through " + e.Name); done(true); yield break; }
        }
        log("couldn't find a way in");
        done(false);
    }
}
