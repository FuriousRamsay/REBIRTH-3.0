using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Map cyclers stacked at the map's top right, in this order: supply drops, owned vehicles, companions that are not
/// following the player (owner 2026-11-07). A cycler with nothing to show is hidden and the ones below move up.
/// Visibility is read from the previous frame, so the stack settles within one frame of a change.
/// </summary>
internal static class RebirthMapCyclerStack
{
    internal const int Gap = 8;
    private static readonly bool[] visible = new bool[3];

    internal static void SetVisible(int slot, bool shown) { if (slot >= 0 && slot < visible.Length) visible[slot] = shown; }

    /// <summary>Vertical offset (downward, in pixels) for the cycler in this slot.</summary>
    internal static int Offset(int slot)
    {
        int offset = 0;
        for (int i = 0; i < slot && i < visible.Length; i++)
            if (visible[i]) offset += XUiC_RebirthPurgeSupplyButton.ControlHeight + Gap;
        return offset;
    }
}

/// <summary>Owned vehicles or idle companions: click to centre the map on the next one; the counter shows which.</summary>
[Preserve]
public sealed class XUiC_RebirthMapTargetCycler : XUiController
{
    private struct Target { internal string Key, Name, Icon; internal Vector3 Position; }

    private static readonly System.Collections.Generic.List<XUiC_RebirthMapTargetCycler> Open = new System.Collections.Generic.List<XUiC_RebirthMapTargetCycler>();
    /// <summary>Same rule as the supply drop cycler: the map ignores clicks and hover under a shown cycler.</summary>
    internal static bool AnyBlocksMapInput { get { lock (Open) { foreach (var c in Open) if (c.ContainsPointer()) return true; } return false; } }
    private bool ContainsPointer()
    {
        if (map == null || !map.isOpen || ViewComponent?.IsVisible != true) return false;
        var camera = map.xui?.playerUI?.camera; var transform = ViewComponent.UiTransform;
        if (camera == null || transform == null) return false;
        var ray = camera.ScreenPointToRay(map.xui.playerUI.CursorController.GetScreenPosition());
        if (!new Plane(transform.forward, transform.position).Raycast(ray, out float distance)) return false;
        var point = transform.InverseTransformPoint(ray.GetPoint(distance));
        return point.x >= 0 && point.x <= XUiC_RebirthPurgeSupplyButton.ControlWidth && point.y <= 0 && point.y >= -XUiC_RebirthPurgeSupplyButton.ControlHeight;
    }
    private string kind = "vehicles";
    private int slot = 1;
    private XUiC_RebirthMapArea map;
    private XUiController button;
    private XUiV_Sprite icon, border;
    private XUiV_Label counter;
    private readonly List<Target> targets = new List<Target>();
    private string selected;
    private float next;
    private XUiView mapView;

    public override bool ParseAttribute(string name, string value)
    {
        if (name == "kind") { kind = value; slot = value == "companions" ? 2 : 1; return true; }
        return base.ParseAttribute(name, value);
    }

    public override void Init()
    {
        base.Init();
        button = GetChildById("cycleButton");
        icon = GetChildById("cycleIcon")?.ViewComponent as XUiV_Sprite;
        border = GetChildById("cycleBorder")?.ViewComponent as XUiV_Sprite;
        counter = GetChildById("cycleCounter")?.ViewComponent as XUiV_Label;
        if (button != null) button.OnPress += (s, mouse) => { if (mouse == 0 || mouse == -1) Cycle(); };
    }

    public override void OnOpen() { base.OnOpen(); map = GetParentByType<XUiC_RebirthMapArea>(); mapView = null; next = 0f; Refresh(); lock (Open) Open.Add(this); }

    public override void OnClose()
    {
        RebirthMapCyclerStack.SetVisible(slot, false);
        if (ViewComponent != null) ViewComponent.IsVisible = false;
        lock (Open) Open.Remove(this);
        map = null; mapView = null;
        base.OnClose();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (map == null || !map.isOpen || !map.bMapInitialized) { Show(false); return; }
        if (Time.realtimeSinceStartup >= next) { next = Time.realtimeSinceStartup + 1f; Refresh(); }
        bool show = targets.Count > 0 && Layout();
        Show(show);
        if (!show) return;
        int index = targets.FindIndex(t => t.Key == selected);
        string text = (index >= 0 ? (index + 1).ToString() : "-") + " / " + targets.Count;
        if (counter != null && counter.Text != text) counter.Text = text;
        string sprite = targets[Math.Max(0, index)].Icon;
        if (icon != null && !string.IsNullOrEmpty(sprite) && icon.SpriteName != sprite) icon.SpriteName = sprite;
        if (border != null) border.Color = button?.ViewComponent?.UiTransformIsHovered == true ? new Color32(219, 175, 250, 255) : new Color32(190, 149, 225, 255);
    }

    private void Show(bool show)
    {
        RebirthMapCyclerStack.SetVisible(slot, show);
        if (ViewComponent != null && ViewComponent.IsVisible != show) ViewComponent.IsVisible = show;
    }

    /// <summary>Same right inset as the supply drop cycler, moved down past the visible cyclers above it.</summary>
    private bool Layout()
    {
        if (mapView == null) mapView = map.GetChildById("mapView")?.ViewComponent; // cached: a per-frame tree search on the map is costly
        var view = mapView; var parent = Parent?.ViewComponent?.UiTransform;
        if (view?.UiTransform == null || parent == null || ViewComponent == null) return false;
        int width = XUiC_RebirthPurgeSupplyButton.ControlWidth, inset = XUiC_RebirthPurgeSupplyButton.Inset;
        if (view.Size.x < width + inset * 2) return false;
        var point = parent.InverseTransformPoint(view.UiTransform.TransformPoint(new Vector3(view.Size.x, 0, 0)));
        var position = new Vector2i(Mathf.RoundToInt(point.x) - width - inset, Mathf.RoundToInt(point.y) - inset - RebirthMapCyclerStack.Offset(slot));
        if (ViewComponent.Position != position) ViewComponent.Position = position;
        return true;
    }

    private void Refresh()
    {
        targets.Clear();
        var player = xui?.playerUI?.entityPlayer;
        var world = GameManager.Instance?.World;
        if (player == null || world == null) return;
        if (kind == "vehicles")
        {
            // Owned vehicles are the vehicle waypoints the game keeps at their last known position.
            foreach (Waypoint waypoint in player.Waypoints.Collection.list)
            {
                if (waypoint == null || waypoint.lastKnownPositionEntityType != eLastKnownPositionEntityType.Vehicle) continue;
                Vector3 position = waypoint.pos.ToVector3();
                if (waypoint.lastKnownPositionEntityId > 0 && world.GetEntity(waypoint.lastKnownPositionEntityId) is EntityVehicle live) position = live.position;
                targets.Add(new Target { Key = "v" + waypoint.lastKnownPositionEntityId + ":" + waypoint.pos, Name = waypoint.name?.Text, Icon = waypoint.icon, Position = position });
            }
        }
        else
        {
            // Live companion registry only (owner 2026-11-08: the full roster lookup scanned remote resources and the
            // companion save store every second and lagged the map).
            string owner = GameManager.Instance?.GetPersistentLocalPlayer()?.PrimaryId?.ToString();
            if (string.IsNullOrEmpty(owner)) return;
            foreach (var state in RebirthNpcRuntimeRegistry.GetSnapshot())
            {
                if (state == null || state.Presence != RebirthNpcPresenceState.Active || state.OwnershipKind != RebirthNpcOwnershipKind.Player
                    || !string.Equals(state.OwnerId ?? string.Empty, owner, StringComparison.OrdinalIgnoreCase) || state.Order == RebirthNpcOrderState.Follow) continue;
                if (!RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out int entityId) || !(world.GetEntity(entityId) is EntityAlive npc) || npc.IsDead()) continue;
                targets.Add(new Target { Key = "c" + entityId, Name = npc.EntityName, Icon = null, Position = npc.position });
            }
        }
        targets.Sort((a, b) => (a.Position - player.position).sqrMagnitude.CompareTo((b.Position - player.position).sqrMagnitude));
    }

    private void Cycle()
    {
        Refresh();
        if (map == null || targets.Count == 0) return;
        int index = targets.FindIndex(t => t.Key == selected);
        var target = targets[(index + 1) % targets.Count];
        selected = target.Key;
        map.closeAllPopups(); map.ClearMapHover(); map.Interaction.Pan();
        map.zoomScale = Math.Min(map.zoomScale, 2f); map.targetZoomScale = map.zoomScale;
        map.PositionMapAt(target.Position); map.FitPlayableView();
    }
}
