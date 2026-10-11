using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

// Native map icons, owned by stable POI keys. Spatial buckets keep the native
// navigation manager limited to the current viewport rather than all-world icons.
internal static class RebirthPoiMapPresentation
{
    private static readonly Dictionary<string, NavObject> owned = new Dictionary<string, NavObject>(StringComparer.Ordinal);
    private static Dictionary<long, List<RebirthPoiMapRecord>> cells = new Dictionary<long, List<RebirthPoiMapRecord>>();
    private static Dictionary<long, List<RebirthPoiMapRecord>> building;
    private static IEnumerator<RebirthPoiMapRecord> scan;
    private static IReadOnlyDictionary<string, RebirthPoiMapRecord> original, indexed;
    private static double next;
    private const int CellSize = 512;
    private static long Cell(int x, int z) => ((long)x << 32) | (uint)z;
    internal static void Reset()
    {
        if (NavObjectManager.HasInstance) foreach (var icon in owned.Values.Distinct()) NavObjectManager.Instance.UnRegisterNavObject(icon);
        owned.Clear(); scan?.Dispose(); scan = null; cells.Clear(); building = null; original = null; indexed = null; next = 0;
    }
    private static void Release(string key, NavObjectManager manager)
    {
        NavObject icon;
        if (!owned.TryGetValue(key, out icon)) return;
        owned.Remove(key);
        // Native registration deduplicates identical class/position. Two distinct
        // POIs may own the same native icon; retain it until its final owner leaves.
        if (!owned.Values.Any(other => ReferenceEquals(other, icon))) manager.UnRegisterNavObject(icon);
    }
    internal static void Pulse()
    {
        var current = RebirthPoiMapSync.LocalMap;
        if (current == null) return;
        if (scan == null && !ReferenceEquals(indexed, current))
        {
            original = current; building = new Dictionary<long, List<RebirthPoiMapRecord>>();
            scan = original.Values.GetEnumerator();
        }
        if (scan == null) return;
        for (int budget = 0; budget < 256; budget++)
        {
            if (!scan.MoveNext())
            {
                scan.Dispose(); scan = null; cells = building; indexed = original; building = null; original = null;
                next = 0; break;
            }
            var record = scan.Current; var id = record.Identity;
            int x = (int)Math.Floor((id.X + id.SizeX * .5) / CellSize), z = (int)Math.Floor((id.Z + id.SizeZ * .5) / CellSize);
            long key = Cell(x, z); List<RebirthPoiMapRecord> bucket;
            if (!building.TryGetValue(key, out bucket)) { bucket = new List<RebirthPoiMapRecord>(); building.Add(key, bucket); }
            bucket.Add(record);
        }
    }
    internal static void Render(XUiC_MapArea area)
    {
        if (!RebirthSandboxOptionManager.Current.PoiClearTrackingEnabled || GameManager.IsDedicatedServer || area == null) return;
        if (Time.realtimeSinceStartup < next) return; next = Time.realtimeSinceStartup + .25;
        var manager = NavObjectManager.Instance;
        var wanted = new Dictionary<string, RebirthPoiMapRecord>(StringComparer.Ordinal);
        double radius = Math.Max(.7, area.zoomScale) * 168 + 32;
        double cx = area.mapMiddlePosPixel.x, cz = area.mapMiddlePosPixel.y;
        int xmin = (int)Math.Floor((cx - radius) / CellSize), xmax = (int)Math.Floor((cx + radius) / CellSize);
        int zmin = (int)Math.Floor((cz - radius) / CellSize), zmax = (int)Math.Floor((cz + radius) / CellSize);
        bool purge = RebirthSandboxOptionManager.Current.IsPurge;
        for (int x = xmin; x <= xmax; x++) for (int z = zmin; z <= zmax; z++)
        {
            List<RebirthPoiMapRecord> bucket; if (!cells.TryGetValue(Cell(x, z), out bucket)) continue;
            foreach (var candidate in bucket)
            {
                RebirthPoiMapRecord record; var current = RebirthPoiMapSync.LocalMap;
                if (current == null || !current.TryGetValue(candidate.Identity.Key, out record)) continue;
                var id = record.Identity; double px = id.X + id.SizeX * .5, pz = id.Z + id.SizeZ * .5;
                if (Math.Abs(px - cx) > radius || Math.Abs(pz - cz) > radius || record.State == RebirthPoiClearanceState.ResetPending
                    || !purge && record.State != RebirthPoiClearanceState.Cleared) continue;
                wanted[id.Key] = record;
            }
        }
        foreach (string key in owned.Keys.Where(k => !wanted.ContainsKey(k)).ToArray())
        { Release(key, manager); }
        foreach (var pair in wanted)
        {
            var record = pair.Value;
            string type = record.State == RebirthPoiClearanceState.Cleared ? "rebirth_poi_cleared" : "rebirth_poi_discovered";
            NavObject icon;
            if (owned.TryGetValue(pair.Key, out icon) && (icon.NavObjectClass == null || icon.NavObjectClass.NavObjectClassName != type))
            { Release(pair.Key, manager); icon = null; }
            if (icon != null) continue;
            var id = record.Identity;
            icon = manager.RegisterNavObject(type, new Vector3(id.X + id.SizeX * .5f, id.Y, id.Z + id.SizeZ * .5f), hiddenOnCompass: true);
            icon.name = Localization.Get(id.Prefab) + " - " + Localization.Get(record.State == RebirthPoiClearanceState.Cleared
                ? "xuiRebirthPoiCleared" : "xuiRebirthPoiDiscovered");
            icon.usingLocalizationId = false; owned.Add(pair.Key, icon);
        }
    }
}
[HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.Update))]
internal static class RebirthPoiMapRenderHook
{
    private static void Postfix(XUiC_MapArea __instance) { RebirthPoiMapPresentation.Render(__instance); }
}

