using System.Collections.Generic;

#nullable disable

/// <summary>
/// Terrain awareness for the Game Bridge: every bridge movement (walking, fleeing, retreating, backing
/// off, approaching) looks at the ground ahead before stepping, the way a player watches where they go.
///
/// Probe() walks a line over the block grid and classifies it: flat, step up (jumpable, up to 1 block),
/// safe drop (up to SafeDrop blocks), dangerous drop, wall, water. BestDirection() picks the safe direction
/// closest to the one wanted. Flatness()/FlatSpotNear() find decent ground to fight on.
///
/// Fall damage in this game starts after a 2-block fall and scales with impact speed (EntityAlive
/// .fallHitGround): about 3-4 blocks is harmless, 5+ hurts, ~8+ can kill, and falls can sprain or break
/// legs. SafeDrop is therefore 3.
/// </summary>
public static class RebirthGameBridgeTerrain
{
    public const float SafeDrop = 3f;
    public const float MaxClimb = 1.1f; // one block, with a jump
    private const int ScanDown = 14;

    public struct Line
    {
        public bool Safe;          // the whole line is walkable without a dangerous drop, wall or deep water
        public float SafeLength;   // how far along it is walkable
        public float MaxDrop;      // largest single drop (blocks)
        public float MaxRise;      // largest single rise (blocks)
        public float Roughness;    // sum of height changes (bumpy vs flat)
        public bool Wall, Cliff, Water;
        public bool JumpNeeded;    // a 1-block step up within the first metre
        public string Why;
    }

    private static World W { get { return GameManager.Instance.World; } }

    private static bool Solid(BlockValue bv)
    {
        return !bv.isair && bv.Block != null && bv.Block.IsCollideMovement && !bv.Block.IsTerrainDecoration;
    }

    /// <summary>Standing height (top surface) of the column at x,z at or below fromY; NaN if nothing within ScanDown blocks.</summary>
    public static float GroundAt(int x, int z, int fromY, out bool water)
    {
        bool terrain;
        return GroundAt(x, z, fromY, out water, out terrain);
    }

    /// <summary>As GroundAt, also telling whether the top block is natural terrain (walkable slope, no jump needed).</summary>
    public static float GroundAt(int x, int z, int fromY, out bool water, out bool terrain)
    {
        water = false; terrain = false;
        World w = W;
        for (int y = fromY; y >= fromY - ScanDown && y > 0; y--)
        {
            BlockValue bv = w.GetBlock(x, y, z);
            if (bv.isWater) { water = true; continue; }
            if (Solid(bv))
            {
                terrain = bv.Block.shape != null && bv.Block.shape.IsTerrain();
                return y + 1;
            }
        }
        return float.NaN;
    }

    /// <summary>Is there room for the player's body (2 blocks) standing on `groundY` at x,z?</summary>
    private static bool Headroom(int x, int z, float groundY)
    {
        int y = Mathf.RoundToInt(groundY);
        return !Solid(W.GetBlock(x, y, z)) && !Solid(W.GetBlock(x, y + 1, z));
    }

    /// <summary>Walk a straight line of `length` metres from the player's feet along the flat direction `dir`.</summary>
    public static Line Probe(Vector3 from, Vector3 dir, float length)
    {
        var r = new Line { Safe = true };
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) { r.Why = "no direction"; r.Safe = false; return r; }
        dir.Normalize();
        float cur = Mathf.Floor(from.y + 0.05f); // current standing height (blocks)
        int lastX = int.MinValue, lastZ = int.MinValue;
        for (float s = 0.5f; s <= length + 0.01f; s += 0.5f)
        {
            Vector3 pt = from + dir * s;
            int x = Mathf.FloorToInt(pt.x), z = Mathf.FloorToInt(pt.z);
            if (x == lastX && z == lastZ) continue;
            lastX = x; lastZ = z;
            bool water, terrain;
            float g = GroundAt(x, z, Mathf.FloorToInt(cur) + 2, out water, out terrain);
            if (float.IsNaN(g)) { r.Cliff = true; r.Safe = false; r.Why = "cliff/deep drop at " + s.ToString("0.0") + "m"; break; }
            float delta = g - cur;
            if (delta > MaxClimb) { r.Wall = true; r.Safe = false; r.Why = "wall at " + s.ToString("0.0") + "m"; break; }
            if (!Headroom(x, z, g)) { r.Wall = true; r.Safe = false; r.Why = "no headroom at " + s.ToString("0.0") + "m"; break; }
            if (-delta > SafeDrop) { r.Cliff = true; r.Safe = false; r.MaxDrop = -delta; r.Why = "drop of " + (-delta).ToString("0") + " blocks at " + s.ToString("0.0") + "m"; break; }
            if (water) { r.Water = true; }
            // Natural terrain is smoothed: the player walks up it without jumping. Only a real block edge (placed
            // block, rock, fence, step) a full block high needs a jump - and jumps cost stamina.
            if (delta >= 0.9f && s <= 1.01f && !terrain) r.JumpNeeded = true;
            r.MaxRise = Mathf.Max(r.MaxRise, delta);
            r.MaxDrop = Mathf.Max(r.MaxDrop, -delta);
            r.Roughness += Mathf.Abs(delta);
            r.SafeLength = s;
            cur = g;
        }
        return r;
    }

    /// <summary>
    /// Safe direction closest to `desired` (flat). Samples 16 directions; unsafe ones are skipped; among safe
    /// ones prefers alignment with `desired`, then smooth ground. Returns `desired` itself when it is safe.
    /// `avoid` (optional) scores extra penalties, e.g. heading toward other threats.
    /// </summary>
    public static Vector3 BestDirection(Vector3 from, Vector3 desired, float length, out Line chosen, Func<Vector3, float> avoid = null)
    {
        desired.y = 0f;
        desired = desired.sqrMagnitude > 1e-6f ? desired.normalized : Vector3.forward;
        chosen = Probe(from, desired, length);
        if (chosen.Safe && (avoid == null || avoid(desired) < 0.5f)) return desired;

        float bestScore = float.MinValue;
        Vector3 best = desired;
        Line bestLine = chosen;
        for (int i = 0; i < 16; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
            Line l = Probe(from, d, length);
            float score = Vector3.Dot(d, desired) * 3f - l.Roughness * 0.3f - (l.Water ? 1f : 0f) + l.SafeLength * (l.Safe ? 0.2f : 0.05f);
            if (!l.Safe) score -= 20f;
            if (avoid != null) score -= avoid(d) * 4f;
            if (score > bestScore) { bestScore = score; best = d; bestLine = l; }
        }
        chosen = bestLine;
        return best;
    }

    /// <summary>Height spread (blocks) of the ground within `radius` metres: 0 = flat.</summary>
    public static float Flatness(Vector3 at, float radius)
    {
        float min = float.MaxValue, max = float.MinValue;
        int r = Mathf.CeilToInt(radius);
        int fromY = Mathf.FloorToInt(at.y) + 3;
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                if (dx * dx + dz * dz > radius * radius) continue;
                bool water;
                float g = GroundAt(Mathf.FloorToInt(at.x) + dx, Mathf.FloorToInt(at.z) + dz, fromY, out water);
                if (float.IsNaN(g)) return 99f;
                min = Mathf.Min(min, g); max = Mathf.Max(max, g);
            }
        return max - min;
    }

    private static bool IsClutterBlock(BlockValue bv)
    {
        if (bv.isair || bv.Block == null) return false;
        if (bv.Block.IsTerrainDecoration) return true;
        string n = bv.Block.GetBlockName();
        return n.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Plant", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Flower", StringComparison.OrdinalIgnoreCase) >= 0 || n.StartsWith("tree", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Grass, plants, bushes and trees in the two block layers above the ground within radius: they block melee swings and arrows.</summary>
    public static int Clutter(Vector3 at, float radius)
    {
        int n = 0, r = Mathf.CeilToInt(radius), fromY = Mathf.FloorToInt(at.y) + 4;
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                if (dx * dx + dz * dz > radius * radius) continue;
                int x = Mathf.FloorToInt(at.x) + dx, z = Mathf.FloorToInt(at.z) + dz;
                bool water;
                float g = GroundAt(x, z, fromY, out water);
                if (float.IsNaN(g)) continue;
                int y = Mathf.FloorToInt(g);
                for (int dy = 0; dy <= 1; dy++) if (IsClutterBlock(W.GetBlock(x, y + dy, z))) n++;
            }
        return n;
    }

    /// <summary>The best place within maxDist to fight on: flat, bare (no grass/bushes/trees), dry. Null when nothing is loaded around.</summary>
    public static Vector3? OpenGroundNear(Vector3 from, float maxDist, out float bestScore)
    {
        Vector3? best = null; bestScore = float.MaxValue;
        for (float x = -maxDist; x <= maxDist; x += 4f)
            for (float z = -maxDist; z <= maxDist; z += 4f)
            {
                if (x * x + z * z > maxDist * maxDist) continue;
                Vector3 spot = new Vector3(from.x + x, from.y, from.z + z);
                bool water;
                float g = GroundAt(Mathf.FloorToInt(spot.x), Mathf.FloorToInt(spot.z), Mathf.FloorToInt(from.y) + 9, out water);
                if (float.IsNaN(g) || water) continue;
                spot.y = g;
                float score = Clutter(spot, 6f) * 1f + Flatness(spot, 4f) * 6f + Mathf.Sqrt(x * x + z * z) * 0.02f;
                if (score < bestScore) { bestScore = score; best = spot; }
            }
        return best;
    }
    /// <summary>
    /// A big flat patch: a (2*half) m square whose ground varies by only a few centimetres - a road, a parking lot, a field - for fights that need room to
    /// hold a distance and retreat 8 m. Slopes ruin the footwork (the zombie sits above or below you), so test here first. range = max-min height.
    /// </summary>
    /// <summary>Is this point inside (or within `margin` m of) any prefab - a POI, a trader compound, a rural building?</summary>
    public static bool NearAnyPrefab(float x, float z, float margin)
    {
        try
        {
            var dec = GameManager.Instance.GetDynamicPrefabDecorator();
            if (dec == null || dec.allPrefabs == null) return false;
            foreach (PrefabInstance pi in dec.allPrefabs)
            {
                Vector3i bp = pi.boundingBoxPosition, bs = pi.boundingBoxSize;
                if (x >= bp.x - margin && x <= bp.x + bs.x + margin && z >= bp.z - margin && z <= bp.z + bs.z + margin) return true;
            }
        }
        catch { }
        return false;
    }

    public static string SurfaceName(int x, int z)
    {
        try { int h = Mathf.FloorToInt(W.GetTerrainHeight(x, z)); BlockValue bv = W.GetBlock(x, h, z); if (bv.isair) bv = W.GetBlock(x, h - 1, z); return bv.Block != null ? bv.Block.GetBlockName() : ""; } catch { return ""; }
    }

    public static Vector3? FlatZoneNear(Vector3 from, float maxDist, float half, out float range, out float clutterAtCentre)
    {
        Vector3? best = null; float bestScore = float.MaxValue; range = 99f; clutterAtCentre = 99f;
        for (float x = -maxDist; x <= maxDist; x += 6f)
            for (float z = -maxDist; z <= maxDist; z += 6f)
            {
                if (x * x + z * z > maxDist * maxDist) continue;
                float cx = from.x + x, cz = from.z + z, min = float.MaxValue, max = float.MinValue; bool bad = false; float centreY = 0f, centreD = float.MaxValue;
                for (float dx = -half; dx <= half && !bad; dx += 4f)
                    for (float dz = -half; dz <= half; dz += 4f)
                    {
                        bool water;
                        // The game's own terrain height: independent of where we stand (scanning down from just above us returns our own level on higher ground = a fake "flat").
                        float g = float.NaN;
                        try { g = W.GetTerrainHeight(Mathf.FloorToInt(cx + dx), Mathf.FloorToInt(cz + dz)); } catch { }
                        if (float.IsNaN(g) || g <= 1f) { bad = true; break; }
                        min = Mathf.Min(min, g); max = Mathf.Max(max, g);
                        if (Mathf.Abs(dx) + Mathf.Abs(dz) < centreD) { centreD = Mathf.Abs(dx) + Mathf.Abs(dz); centreY = g; }
                    }
                if (bad) continue;
                float r = max - min;
                if (r > 1.2f) continue;
                if (NearAnyPrefab(cx, cz, half + 22f)) continue;       // never inside a POI or trader compound (stuck zombies, walls, clutter)
                var spot = new Vector3(cx, centreY, cz);
                float cl = Clutter(spot, 8f);
                string sn = SurfaceName(Mathf.FloorToInt(cx), Mathf.FloorToInt(cz));
                bool road = sn.IndexOf("sphalt", StringComparison.OrdinalIgnoreCase) >= 0 || sn.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0 || sn.IndexOf("ravel", StringComparison.OrdinalIgnoreCase) >= 0;
                float score = r * 12f + cl * 0.15f + Mathf.Sqrt(x * x + z * z) * 0.01f - (road ? 3f : 0f);
                if (score < bestScore) { bestScore = score; best = spot; range = r; clutterAtCentre = cl; }
            }
        return best;
    }

    /// <summary>Nearest reachable, flatter spot within maxDist (null if the current spot is already fine or none found).</summary>
    public static Vector3? FlatSpotNear(Vector3 from, float maxDist, float goodEnough, Func<Vector3, float> penalty = null)
    {
        float here = Flatness(from, 2.5f);
        if (here <= goodEnough) return null;
        Vector3? best = null;
        float bestScore = float.MaxValue;
        for (float dist = 3f; dist <= maxDist; dist += 2.5f)
            for (int i = 0; i < 12; i++)
            {
                Vector3 d = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
                Line l = Probe(from, d, dist);
                if (!l.Safe) continue;
                Vector3 spot = from + d * dist;
                bool water;
                float g = GroundAt(Mathf.FloorToInt(spot.x), Mathf.FloorToInt(spot.z), Mathf.FloorToInt(from.y) + 3, out water);
                if (float.IsNaN(g) || water) continue;
                spot.y = g;
                float flat = Flatness(spot, 2.5f);
                float score = flat * 3f + dist * 0.3f + (penalty != null ? penalty(spot) : 0f);
                if (flat < here && score < bestScore) { bestScore = score; best = spot; }
            }
        return best;
    }
}
