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
    // Owner-requested tier filters hide native map icons without erasing discovery.
    private static readonly bool[] dimmed = new bool[7];
    internal static bool Dimmed(int filter)=>filter>=0&&filter<dimmed.Length&&dimmed[filter];
    internal static bool Owns(NavObject icon) => icon != null && owned.Values.Contains(icon);
    internal static RebirthPoiMapRecord Record(NavObject icon)
    {
        if(icon==null)return null;
        foreach(var pair in owned)if(ReferenceEquals(pair.Value,icon)){RebirthPoiMapRecord record;return RebirthPoiMapSync.LocalMap!=null&&RebirthPoiMapSync.LocalMap.TryGetValue(pair.Key,out record)?record:null;}
        return null;
    }
    internal static void Toggle(int filter)
    {if(filter<0||filter>=dimmed.Length)return;dimmed[filter]=!dimmed[filter];next=0;}
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
            var id = record.Identity;
            if (icon == null)
            {
                icon = manager.RegisterNavObject(type, new Vector3(id.X + id.SizeX * .5f, id.Y, id.Z + id.SizeZ * .5f), hiddenOnCompass: true);
                if(icon==null)continue;
                owned.Add(pair.Key, icon);
            }
            // Refresh existing icons too: the census can finish after discovery.
            // Only these six tier sprites are supplied by the original atlas.
            icon.OverrideSpriteName = record.State == RebirthPoiClearanceState.Cleared
                ? "ui_game_symbol_purged"
                : record.Tier >= 0 ? "ui_game_symbol_Skull" + Math.Min(5, record.Tier)
                : "ui_game_symbol_twitch_boss_overhead";
            bool filtered=purge && (record.State==RebirthPoiClearanceState.Cleared?Dimmed(0):record.Tier>=0&&Dimmed(Math.Min(5,record.Tier)+1));
            icon.hiddenOnMap=filtered;
            icon.hiddenOnCompass=filtered;
            string label = Localization.Get(id.Prefab);
            if(!string.Equals(icon.name,label,StringComparison.Ordinal))
            {
                icon.name=label;
                icon.localizedName=null;
            }
            icon.usingLocalizationId = false;
        }
    }

    // Native map labels use DisplayName regardless of text_type. Keep the name
    // for native hover tooltips, but hide its persistent label on both maps.
    internal static void RefreshMapSprites(XUiC_MapArea area)
    {
        if (area == null || owned.Count == 0) return;
        foreach (var icon in owned.Values)
        {
            GameObject view;
            if (!area.keyToNavSprite.TryGetValue(icon.Key, out view) || view == null) continue;
            view.SetActive(!icon.hiddenOnMap);
            if(icon.hiddenOnMap)continue;
            var name = view.transform.Find("Name");
            if (name != null) name.gameObject.SetActive(false);
            var sprite = view.transform.Find("Sprite")?.GetComponent<UISprite>();
            if (sprite == null) continue;
            // Native map rendering caches the sprite at creation. Census tier
            // updates must refresh existing markers as well as newly added ones.
            string artwork = icon.OverrideSpriteName;
            string atlasName = artwork != null && (artwork.StartsWith("ui_game_symbol_Skull",StringComparison.Ordinal) || artwork == "ui_game_symbol_purged") ? "PurgeUIAtlas" : "UIAtlas";
            if (!string.IsNullOrEmpty(artwork) && (sprite.spriteName != artwork || sprite.atlas == null || ((UnityEngine.Object)sprite.atlas).name!=atlasName))
            {
                sprite.atlas = area.xui.GetAtlasByName(atlasName, artwork);
                sprite.spriteName = artwork;
            }
        }
    }
}
[HarmonyPatch(typeof(XUiC_MapArea), "updateNavObjectList")]
internal static class RebirthPoiMapSpriteHook
{
    private static void Postfix(XUiC_MapArea __instance) { RebirthPoiMapPresentation.RefreshMapSprites(__instance); }
}
[HarmonyPatch(typeof(XUiC_MapArea), "UpdateWaypointSelection")]
internal static class RebirthPoiMapHoverHook
{
    private sealed class Hover { internal string Text; }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_MapArea,Hover> hovered = new System.Runtime.CompilerServices.ConditionalWeakTable<XUiC_MapArea,Hover>();
    private static void Postfix(XUiC_MapArea __instance)
    {
        var icon = __instance.closestMouseOverNavObject;
        var previous=hovered.GetValue(__instance, _ => new Hover());
        var popup=__instance.GetChildByType<XUiC_RebirthPurgePoiPopup>();
        if(popup!=null&&RebirthSandboxOptionManager.Current.IsPurge)
        {
            popup.Show(RebirthPoiMapPresentation.Record(icon));
            if(RebirthPoiMapPresentation.Owns(icon)||previous.Text!=null&&__instance.xui.ToolTipWindow.ToolTip==previous.Text)__instance.xui.ToolTipWindow.ToolTip=string.Empty;
            previous.Text=null;return;
        }
        popup?.Show(null);
        if (RebirthPoiMapPresentation.Owns(icon))
        {previous.Text=icon.DisplayName;__instance.xui.ToolTipWindow.ToolTip=previous.Text;}
        else
        {
            if(previous.Text!=null&&__instance.xui.ToolTipWindow.ToolTip==previous.Text)__instance.xui.ToolTipWindow.ToolTip=string.Empty;
            previous.Text=null;
        }
    }
}
[HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.Update))]
internal static class RebirthPoiMapRenderHook
{
    private static void Postfix(XUiC_MapArea __instance) { RebirthPoiMapPresentation.Render(__instance); }
}

