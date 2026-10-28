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
    private static readonly Dictionary<NavObject,HashSet<string>> iconOwners=new Dictionary<NavObject,HashSet<string>>();
    private static Dictionary<long, List<RebirthPoiMapRecord>> cells = new Dictionary<long, List<RebirthPoiMapRecord>>();
    private static Dictionary<long, List<RebirthPoiMapRecord>> building;
    private static IEnumerator<RebirthPoiMapRecord> scan;
    private static IReadOnlyDictionary<string, RebirthPoiMapRecord> original, indexed;
    private static double next;
    // Owner-requested tier filters hide native map icons without erasing discovery.
    private static readonly bool[] dimmed = new bool[7];
    // Native per-marker compass toggles survive ordinary viewport eviction.
    // A temporary legend filter must not turn its forced-hidden state into that preference.
    private static readonly Dictionary<string,bool> compassHidden = new Dictionary<string,bool>(StringComparer.Ordinal);
    private static readonly HashSet<string> compassFiltered = new HashSet<string>(StringComparer.Ordinal);
    // Native registration can share one icon across POI keys. This snapshot is
    // active-ownership only and is removed before native unregister/pool reuse.
    private static readonly Dictionary<NavObject,bool> compassBeforeFilter = new Dictionary<NavObject,bool>();
    internal static bool Dimmed(int filter)=>filter>=0&&filter<dimmed.Length&&dimmed[filter];
    internal static bool Owns(NavObject icon) => icon != null && owned.Values.Contains(icon);
    internal static RebirthPoiMapRecord Record(NavObject icon)
    {
        if(icon==null)return null;
        HashSet<string> owners;if(!iconOwners.TryGetValue(icon,out owners))return null;
        var current=RebirthPoiMapSync.LocalMap;if(current==null)return null;
        foreach(var key in owners)if(!compassFiltered.Contains(key))
        {RebirthPoiMapRecord record;if(current.TryGetValue(key,out record)&&record.State!=RebirthPoiClearanceState.ResetPending)return record;}
        return null;
    }
    internal static void Toggle(int filter)
    {if(filter<0||filter>=dimmed.Length)return;dimmed[filter]=!dimmed[filter];next=0;}
    private const int CellSize = 512;
    private static long Cell(int x, int z) => ((long)x << 32) | (uint)z;
    internal static void Reset()
    {
        if (NavObjectManager.HasInstance) foreach (var icon in owned.Values.Distinct()) { compassBeforeFilter.Remove(icon); NavObjectManager.Instance.UnRegisterNavObject(icon); }
        owned.Clear(); iconOwners.Clear(); compassHidden.Clear(); compassFiltered.Clear(); compassBeforeFilter.Clear(); scan?.Dispose(); scan = null; cells.Clear(); building = null; original = null; indexed = null; next = 0;
    }
    private static void AddOwner(string key,NavObject icon)
    {
        owned.Add(key,icon);
        HashSet<string> owners;
        if(!iconOwners.TryGetValue(icon,out owners))
        {owners=new HashSet<string>(StringComparer.Ordinal);iconOwners.Add(icon,owners);}
        owners.Add(key);
    }
    private static void Release(string key, NavObjectManager manager)
    {
        NavObject icon;
        if (!owned.TryGetValue(key, out icon)) return;
        bool hidden;compassHidden[key]=compassBeforeFilter.TryGetValue(icon,out hidden)?hidden:icon.hiddenOnCompass;
        compassFiltered.Remove(key);
        owned.Remove(key);
        var owners=iconOwners[icon];owners.Remove(key);
        // Native registration deduplicates identical class/position. Two distinct
        // POIs may own the same native icon; retain it until its final owner leaves.
        if (owners.Count==0)
        { iconOwners.Remove(icon); compassBeforeFilter.Remove(icon); manager.UnRegisterNavObject(icon); }
        else RefreshFilter(icon);
    }
    private static void ApplyFilter(string key,NavObject icon,bool filtered)
    {
        bool hidden;
        compassHidden[key]=compassBeforeFilter.TryGetValue(icon,out hidden)?hidden:icon.hiddenOnCompass;
        if(filtered)compassFiltered.Add(key);else compassFiltered.Remove(key);
        RefreshFilter(icon);
    }
    private static void RefreshFilter(NavObject icon)
    {
        // Native icons can be shared by distinct POIs. Hide only when every
        // owner is filtered, independent of enumeration order. The popup must
        // likewise resolve to an owner that is still visible.
        bool filtered=iconOwners[icon].All(key=>compassFiltered.Contains(key));
        icon.hiddenOnMap=filtered;
        bool hidden;
        if(filtered)
        {
            if(!compassBeforeFilter.ContainsKey(icon))compassBeforeFilter.Add(icon,icon.hiddenOnCompass);
            icon.hiddenOnCompass=true;
        }
        else if(compassBeforeFilter.TryGetValue(icon,out hidden))
        {icon.hiddenOnCompass=hidden;compassBeforeFilter.Remove(icon);}
    }
    private static void ViewportBounds(XUiC_MapArea area,out double left,out double right,out double bottom,out double top)
    {
        // Exact native worldPosToScreenPos uses 2.1190476/zoom even when the
        // REBIRTH map canvas is wider than the original 712x712 view.
        var view=area.mapView?.ViewComponent;
        double width=view==null?712:Math.Max(1,view.Size.x),height=view==null?712:Math.Max(1,view.Size.y);
        double unit=Math.Max(.01,area.zoomScale)/2.1190476;
        double cx=area.mapMiddlePosPixel.x,cz=area.mapMiddlePosPixel.y;
        left=cx-area.cTexMiddle.x*unit-32;
        right=cx+(width-area.cTexMiddle.x)*unit+32;
        bottom=cz+(area.cTexMiddle.y-height)*unit-32;
        top=cz+area.cTexMiddle.y*unit+32;
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
        double left,right,bottom,top;ViewportBounds(area,out left,out right,out bottom,out top);
        int xmin = (int)Math.Floor(left / CellSize), xmax = (int)Math.Floor(right / CellSize);
        int zmin = (int)Math.Floor(bottom / CellSize), zmax = (int)Math.Floor(top / CellSize);
        bool purge = RebirthSandboxOptionManager.Current.IsPurge;
        for (int x = xmin; x <= xmax; x++) for (int z = zmin; z <= zmax; z++)
        {
            List<RebirthPoiMapRecord> bucket; if (!cells.TryGetValue(Cell(x, z), out bucket)) continue;
            foreach (var candidate in bucket)
            {
                RebirthPoiMapRecord record; var current = RebirthPoiMapSync.LocalMap;
                if (current == null || !current.TryGetValue(candidate.Identity.Key, out record)) continue;
                var id = record.Identity; double px = id.X + id.SizeX * .5, pz = id.Z + id.SizeZ * .5;
                if (px < left || px > right || pz < bottom || pz > top || record.State == RebirthPoiClearanceState.ResetPending
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
                bool hidden;compassHidden.TryGetValue(pair.Key,out hidden);
                icon = manager.RegisterNavObject(type, new Vector3(id.X + id.SizeX * .5f, id.Y, id.Z + id.SizeZ * .5f), hiddenOnCompass: hidden);
                if(icon==null)continue;
                AddOwner(pair.Key,icon);
            }
            bool filtered=purge && (record.State==RebirthPoiClearanceState.Cleared?Dimmed(0):record.Tier>=0&&Dimmed(Math.Min(5,record.Tier)+1));
            ApplyFilter(pair.Key,icon,filtered);
        }
        // Resolve artwork and name after all owners' filter states are known,
        // using the same visible owner as the hover card.
        foreach(var icon in iconOwners.Keys)
        {
            var record=Record(icon);if(record==null)continue;
            icon.OverrideSpriteName = record.State == RebirthPoiClearanceState.Cleared
                ? "ui_game_symbol_purged"
                : record.Tier >= 0 ? "ui_game_symbol_Skull" + Math.Min(5, record.Tier)
                : "ui_game_symbol_twitch_boss_overhead";
            string label=Localization.Get(record.Identity.Prefab);
            if(!string.Equals(icon.name,label,StringComparison.Ordinal))
            {icon.name=label;icon.localizedName=null;}
            icon.usingLocalizationId=false;
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
        if(__instance is XUiC_RebirthMapArea full && full.SuppressMapHover)
        {
            // Native selection is also gated, but Harmony postfixes still run
            // when a prefix skips it. Hide an already visible card as well.
            __instance.GetChildByType<XUiC_RebirthPurgePoiPopup>()?.Show(null);
            full.ClearMapHover();
            hovered.GetValue(__instance, _ => new Hover()).Text=null;
            return;
        }
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


