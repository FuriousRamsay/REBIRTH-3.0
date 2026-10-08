using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Explicit Character-only clipped list. Bounds come from data, never from hidden row-pool
/// rectangles. Whole content translates smoothly; pool rebinding is needed only beyond its range.
/// Uses the same 16/12 track/thumb dimensions and native button artwork as Personal Crafting.
/// </summary>
[UnityEngine.Scripting.Preserve]
public sealed class XUiC_RebirthCharacterOverviewList : XUiController
{
    private XUiController viewport, content, track, thumb;
    private XUiV_Label emptyLabel;
    private XUiC_RebirthSurvivorCharacter owner;
    private int itemCount, stride = 38, capacity, rowHeight = 36;
    private float pixels, target;
    private int lastScrollFrame = -1;
    private bool dragging;
    private bool pagingMode;
    private XUiController nativeHost;
    private XUiV_ScrollBar nativeBar;
    private float lastNativeValue;
    private float dragStartOffset, dragAccumulated;
    public int FirstDataIndex { get; private set; }
    public int Capacity { get { return capacity; } }
    public float ScrollOffset => pixels;
    private float PagingStep => RebirthScrollbarPagingPolicy.PageStep(Height + Math.Max(0, stride - rowHeight), stride);
    public void RestoreScrollOffset(float offset)
    {
        pixels = target = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.EnsureVisible(pixels, offset, offset + rowHeight, MaxOffset, Height, PagingStep) : Mathf.Clamp(offset, 0f, MaxOffset);
        UpdateRange(true); ApplyPosition(); UpdateBar();
    }
    public bool IsReady { get { return content != null && viewport != null && capacity > 0; } }
    public event Action DataRangeChanged;

    private int Height { get { return viewport != null && viewport.ViewComponent != null ? Math.Max(1, viewport.ViewComponent.Size.y) : 1; } }
    private int ContentHeight { get { return itemCount == 0 ? 0 : (itemCount - 1) * stride + rowHeight; } }
    private float MaxOffset { get { return Math.Max(0, ContentHeight - Height); } }
    private bool Live { get { return ((owner != null && owner.IsCharacterWindowOpen) || GetParentByType<XUiC_RebirthItemEditorHeader>()?.IsEditorOpen == true || GetParentByType<XUiC_RebirthBackpackLibrary>()?.IsOpen == true || GetParentByType<XUiC_RebirthBackpackSellStash>()?.IsOpen == true || GetParentByType<XUiC_RebirthJournal>() != null || GetParentByType<XUiC_QuestListWindow>() != null || GetParentByType<XUiC_RebirthPlayersList>() != null || GetChildByType<XUiC_RebirthWaypointList>() != null || GetChildByType<XUiC_RebirthInviteList>() != null || XUiC_RebirthCreativeWorkspace.ActiveInstance?.IsWorkspaceOpen == true) && ViewComponent != null && ViewComponent.UiTransform != null && ViewComponent.UiTransform.gameObject.activeInHierarchy; } }

    public override void Init()
    {
        base.Init();
        owner = GetParentByType<XUiC_RebirthSurvivorCharacter>();
        viewport = GetChildById("listViewport");
        content = GetChildById("listContent") ?? GetChildById("waypointList") ?? GetChildById("invitesList");
        track = GetChildById("listTrack");
        thumb = GetChildById("listThumb");
        emptyLabel = GetChildById("listEmpty")?.ViewComponent as XUiV_Label;
        nativeHost = GetChildById("listNativeScrollbar");
        capacity = content != null && content.Children != null ? content.Children.Count : 0;
        if (capacity > 0) rowHeight = Math.Max(1, content.Children[0].ViewComponent.Size.y);
        if (capacity > 1)
            stride = Math.Max(1, Math.Abs(content.Children[1].ViewComponent.Position.y - content.Children[0].ViewComponent.Position.y));
        WireScroll(this);
        if (thumb != null)
        {
            thumb.ViewComponent.EventOnDrag = true;
            thumb.ViewComponent.EventOnPress = true;
            thumb.ViewComponent.EventOnHover = true;
            thumb.OnDrag += Drag;
        }
        if (track != null) { track.ViewComponent.EventOnPress=true; track.ViewComponent.EventOnHover=true; track.OnPress += PressTrack; }
        SetItemCount(0, RebirthSurvivorUiText.L("xuiRebirthCharacterDataPending", "Waiting for character data..."));
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResetPosition();
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(nativeHost);
    }

    public override void OnClose()
    {
        dragging = false;
        base.OnClose();
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(nativeHost);
    }

    public void ResetPosition()
    {
        pixels = target = 0f;
        dragging = false;
        UpdateRange(false);
        ApplyPosition();
        UpdateBar();
    }

    public void SetItemCount(int count, string emptyText)
    {
        count = Math.Max(0, count);
        if (itemCount != count)
        {
            itemCount = count;
            if (content != null && content.ViewComponent != null)
                content.ViewComponent.Size = new Vector2i(content.ViewComponent.Size.x, Math.Max(1, ContentHeight));
            pixels = Mathf.Clamp(pixels, 0f, MaxOffset);
            target = Mathf.Clamp(target, 0f, MaxOffset);
            UpdateRange(false);
            ApplyPosition();
        }
        if (emptyLabel != null)
        {
            if (emptyLabel.Text != emptyText) emptyLabel.Text = emptyText ?? string.Empty;
            if (emptyLabel.IsVisible != (count == 0)) emptyLabel.IsVisible = count == 0;
        }
        UpdateBar();
    }

    public override void Update(float dt)
    {
        if (!Live) return;
        if (RebirthScrollbarPagingPolicy.Enabled && (RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || xui?.DragAndDropWindow?.IsEmpty() == false)) return;
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; dragging = false; }
        base.Update(dt);
        if (nativeHost != null)
        {
            if (nativeBar == null && nativeHost.Children != null)
                foreach (var child in nativeHost.Children)
                    if (child.ViewComponent is XUiV_ScrollBar bar && bar.ScrollBar != null)
                    { nativeBar = bar; RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(nativeHost); nativeBar.Connect(Scroll, NativeScrollbarChanged); nativeBar.ScrollBar.fillDirection = UIProgressBar.FillDirection.TopToBottom; UpdateBar(); break; }
            if (nativeBar?.ScrollBar != null && !Mathf.Approximately(nativeBar.ScrollBar.value, lastNativeValue))
            {
                target = pixels = RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp01(nativeBar.ScrollBar.value) * MaxOffset, MaxOffset, PagingStep, RebirthScrollbarPagingPolicy.Enabled);
                UpdateRange(true); ApplyPosition();
            }
            UpdateBar();
        }
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
        {
            dragging = false;
            return;
        }
        // Fallback is restricted to this list's hierarchy, never a global mouse-wheel owner.
        if (lastScrollFrame != Time.frameCount && PointerOverList())
        {
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Math.Abs(wheel) > 0.0001f) Scroll(this, wheel);
        }
        if (RebirthScrollbarPagingPolicy.Enabled)
        {
            float snapped = RebirthScrollbarPagingPolicy.SnapAbsolute(target, MaxOffset, PagingStep, true);
            if (pixels != snapped || target != snapped) { pixels = target = snapped; dragging = false; UpdateRange(true); ApplyPosition(); UpdateBar(); }
            return;
        }
        if (Math.Abs(pixels - target) < 0.01f) return;
        pixels = Mathf.Lerp(pixels, target, Mathf.Clamp01(Math.Max(0f, dt) * 16f));
        if (Math.Abs(pixels - target) < 0.15f) pixels = target;
        UpdateRange(true);
        ApplyPosition();
        UpdateBar();
    }

    private void NativeScrollbarChanged()
    {
        if (!Live || nativeBar?.ScrollBar == null
            || Mathf.Approximately(nativeBar.ScrollBar.value, lastNativeValue)) return;
        target = pixels = RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp01(nativeBar.ScrollBar.value) * MaxOffset, MaxOffset, PagingStep, RebirthScrollbarPagingPolicy.Enabled);
        UpdateRange(true); ApplyPosition(); UpdateBar();
    }
    private void WireScroll(XUiController c)
    {
        if (c == null) return;
        if (c.ViewComponent != null) c.ViewComponent.EventOnScroll = true;
        c.OnScroll += Scroll;
        if (c.Children == null) return;
        for (int i = 0; i < c.Children.Count; i++) WireScroll(c.Children[i]);
    }

    private void Scroll(XUiController sender, float delta)
    {
        if (!Live || RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || lastScrollFrame == Time.frameCount || MaxOffset <= 0f) return;
        lastScrollFrame = Time.frameCount;
        target = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(target, MaxOffset, PagingStep, delta > 0f ? -1 : delta < 0f ? 1 : 0) : Mathf.Clamp(target - delta * stride * 10f, 0f, MaxOffset);
        if (RebirthScrollbarPagingPolicy.Enabled) { pixels = target; UpdateRange(true); ApplyPosition(); UpdateBar(); }
    }

    private void UpdateRange(bool notify)
    {
        int firstVisible = Mathf.FloorToInt(pixels / stride);
        int visibleNeeded = Mathf.CeilToInt(Height / (float)stride) + 1;
        int start = Mathf.Clamp(FirstDataIndex, 0, Math.Max(0, itemCount - capacity));
        if (firstVisible < start || firstVisible + visibleNeeded > start + capacity)
            start = Mathf.Clamp(firstVisible - 1, 0, Math.Max(0, itemCount - capacity));
        bool changed = start != FirstDataIndex;
        FirstDataIndex = start;
        if (!changed || content == null || content.Children == null) return;
        // Rows have no auto-grid/repeat layout to overwrite these staged coordinates.
        for (int i = 0; i < capacity; i++)
        {
            XUiView row = content.Children[i].ViewComponent;
            if (row == null) continue;
            int y = -(start + i) * stride;
            if (row.Position.y != y) row.Position = new Vector2i(0, y);
            row.TryUpdatePosition();
        }
        if (changed && notify) DataRangeChanged?.Invoke();
    }

    private void ApplyPosition()
    {
        XUiView view = content?.ViewComponent;
        if (view == null) return;
        int y = Mathf.RoundToInt(pixels);
        if (view.Position.y != y) view.Position = new Vector2i(0, y);
        view.TryUpdatePosition();
    }

    private int ThumbHeight { get { return MaxOffset <= 0f ? Height : Mathf.Clamp(Mathf.RoundToInt(Height * Height / (float)Math.Max(1, ContentHeight)), Math.Min(30, Height), Height); } }
    private void UpdateBar()
    {
        bool show = IsReady && MaxOffset > 0f;
        if (nativeHost != null)
        {
            Visibility(nativeHost, show);
            Visibility(track, false); Visibility(thumb, false);
            if (nativeBar?.ScrollBar != null)
            {
                nativeBar.BarRequired = show;
                nativeBar.ScrollBar.barSize = Mathf.Clamp01(Height / (float)Math.Max(1, ContentHeight));
                nativeBar.ScrollBar.value = lastNativeValue = MaxOffset > 0 ? pixels / MaxOffset : 0;
                nativeBar.ScrollBar.alpha = show ? 1f : 0f;
                RebirthSlotPalette.ShowScrollbar(nativeBar,show);
            }
            return;
        }
        Visibility(track, show); Visibility(thumb, show);
        if (!show || track.ViewComponent == null || thumb.ViewComponent == null) return;
        XUiView view = thumb.ViewComponent;
        int travel = Math.Max(0, Height - ThumbHeight);
        int y = -Mathf.RoundToInt(travel * (pixels / MaxOffset));
        Vector2i pos = new Vector2i(track.ViewComponent.Position.x + 2, y);
        if (view.Position.x != pos.x || view.Position.y != pos.y) view.Position = pos;
        if (view.Size.x != 12 || view.Size.y != ThumbHeight) view.Size = new Vector2i(12, ThumbHeight);
        view.TryUpdatePosition();
        XUiC_RebirthCraftingInventoryScroll.CommitScrollbarGeometry(track);
        XUiC_RebirthCraftingInventoryScroll.CommitScrollbarGeometry(thumb);
    }
    private static void Visibility(XUiController c, bool show)
    {
        if (c?.ViewComponent != null && c.ViewComponent.IsVisible != show) c.ViewComponent.IsVisible = show;
    }
    private bool PointerOverList()
    {
        Transform t = UICamera.hoveredObject != null ? UICamera.hoveredObject.transform : null;
        Transform root = ViewComponent?.UiTransform;
        return t != null && root != null && (t == root || t.IsChildOf(root));
    }
    private bool PointerY(out float y)
    {
        y = 0f;
        Transform t = track?.ViewComponent?.UiTransform;
        Camera cam = UICamera.currentCamera;
        if (t == null || cam == null) return false;
        Vector2 mouse = UICamera.currentTouch != null ? UICamera.currentTouch.pos : (Vector2)Input.mousePosition;
        Ray ray = cam.ScreenPointToRay(mouse);
        float distance;
        if (!new Plane(t.forward, t.position).Raycast(ray, out distance)) return false;
        y = t.InverseTransformPoint(ray.GetPoint(distance)).y;
        return true;
    }
    private void PressTrack(XUiController sender, int mouseButton)
    {
        if (!Live || RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || MaxOffset <= 0f || (mouseButton != 0 && mouseButton != -1)) return;
        float y;
        if (!PointerY(out y)) return;
        float travel = Math.Max(1, Height - ThumbHeight);
        target = RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp((-y - ThumbHeight * 0.5f) / travel, 0f, 1f) * MaxOffset, MaxOffset, PagingStep, RebirthScrollbarPagingPolicy.Enabled);
        if (RebirthScrollbarPagingPolicy.Enabled) { pixels = target; UpdateRange(true); ApplyPosition(); UpdateBar(); }
    }
    private void Drag(XUiController sender, EDragType dragType, Vector2 delta)
    {
        if (!Live || RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || MaxOffset <= 0f) { dragging = false; return; }
        if (dragType == EDragType.DragStart) { dragging = true; dragAccumulated=0f; dragStartOffset = pixels; }
        if (!dragging) return;
        if(dragType!=EDragType.DragEnd)dragAccumulated-=delta.y;
        target = pixels = RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(dragStartOffset + dragAccumulated * MaxOffset / Math.Max(1, Height - ThumbHeight), 0f, MaxOffset), MaxOffset, PagingStep, RebirthScrollbarPagingPolicy.Enabled);
        UpdateRange(true); ApplyPosition(); UpdateBar();
        if (dragType == EDragType.DragEnd) dragging = false;
    }
}

/// <summary>Presentation-only subtree. Do not update hidden Character pages or their native gear views.</summary>
[UnityEngine.Scripting.Preserve]
public sealed class XUiC_RebirthCharacterPage : XUiController
{
    public override void Update(float dt)
    {
        if (ViewComponent == null || !ViewComponent.IsVisible || ViewComponent.UiTransform == null || !ViewComponent.UiTransform.gameObject.activeInHierarchy) return;
        base.Update(dt);
    }
}
