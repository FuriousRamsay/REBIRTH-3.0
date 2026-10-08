using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Native Creative catalogue data and transactions, with a fixed twelve-row presentation ring.
/// The controller deliberately lives on a RECT, not XUiV_Grid: unchanged cells need no UIGrid reflow.
/// Ten rows are visible. The two additional rows only buffer smooth clipped movement.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCreativeCatalogueGrid : XUiC_Creative2StackGrid
{
    public const int ColumnCount = 17, VisibleRowCount = 10, PoolRowCount = 12, CellPitch = 75;
    public const int ViewportWidth = ColumnCount * CellPitch, ViewportHeight = VisibleRowCount * CellPitch;
    private readonly XUiController[] rowViews = new XUiController[PoolRowCount];
    private readonly XUiC_RebirthCreativeStatsStack[][] rowSlots = new XUiC_RebirthCreativeStatsStack[PoolRowCount][];
    private readonly int[] rowBindings = new int[PoolRowCount];
    private XUiController content, scrollHost, scrollTrack, scrollThumb;
    private XUiV_Panel viewport;
    private UIPanel nativeClip;
    private ItemStack[] previous;
    private bool ready, opened, rebuild = true, thumbDragging;
    private bool pagingMode;
    private int activeStart = -1;
    private float pixels, targetPixels, dragStartY, dragStartPixels;
    private int rowCount;
    internal bool InputBlocked { get; private set; }
    internal float FrameClock { get; private set; }
    internal bool FrameAssemblyActive { get; private set; }
    internal float PixelOffset => pixels;
    internal int CatalogueCount => items?.Length ?? 0;
    internal int PooledCount => itemControllers?.Length ?? 0;
    internal int MaximumPixelOffset => Math.Max(0, rowCount - VisibleRowCount) * CellPitch;
    internal bool IsInsideViewport(int catalogueIndex)
    {
        if (catalogueIndex < 0) return true;
        float top = -(catalogueIndex / ColumnCount) * CellPitch + Mathf.RoundToInt(pixels);
        return RebirthCreativeViewportMath.Intersects(top, CellPitch, ViewportHeight);
    }

    public override void Init()
    {
        base.Init(); // Native slot discovery, delegate initialization and Length contract.
        content = GetChildById("creativeCatalogueContent");
        viewport = GetChildById("creativeCatalogueViewport")?.ViewComponent as XUiV_Panel;
        scrollHost = GetChildById("creativeCatalogueScroll");
        scrollTrack = GetChildById("creativeCatalogueTrack");
        scrollThumb = GetChildById("creativeCatalogueThumb");
        ready = content != null && viewport != null && scrollHost != null && scrollTrack != null &&
            scrollThumb != null && itemControllers != null && itemControllers.Length == ColumnCount * PoolRowCount;
        for (int r = 0; r < PoolRowCount; ++r)
        {
            rowBindings[r] = -1;
            rowViews[r] = GetChildById("creativeCatalogueRow" + r);
            rowSlots[r] = rowViews[r]?.GetChildrenByType<XUiC_RebirthCreativeStatsStack>();
            ready &= rowSlots[r] != null && rowSlots[r].Length == ColumnCount;
            if (rowSlots[r] == null) continue;
            for (int c = 0; c < rowSlots[r].Length; ++c)
            {
                var slot = rowSlots[r][c];
                slot.Catalogue = this;
                slot.PoolIndex = r * ColumnCount + c;
                slot.OnScroll += Scroll;
            }
        }
        if (scrollTrack != null) { scrollTrack.OnScroll += Scroll; scrollTrack.OnPress += TrackPressed; }
        if (scrollThumb != null) { scrollThumb.OnScroll += Scroll; scrollThumb.OnDrag += ThumbDragged; }
        if (!ready)
            Log.Error("[REBIRTH Creative] Fix26 pool/XML mismatch. Apply the matching windows.xml and rebuilt DLL together.");
    }

    public override void OnOpen()
    {
        opened = true; pixels = targetPixels = 0f; activeStart = -1; previous = null;
        rebuild = true; thumbDragging = false; rowCount = 0; InputBlocked = false;
        for (int r = 0; r < PoolRowCount; ++r) rowBindings[r] = -1;
        base.OnOpen(); // Keep native menu registration and Creative stack metadata.
        if (!ready) return;
        ConfigureViewport();
        for (int i = 0; i < itemControllers.Length; ++i)
        {
            // A catalogue is a source of cloned items, NOT an inventory array indexed by pooled cells.
            itemControllers[i].SlotChangedEvent -= handleSlotChangedDelegate;
            itemControllers[i].StackLocation = XUiC_ItemStack.StackLocationTypes.Creative;
        }
    }

    public override void OnClose()
    {
        opened = false; thumbDragging = false; previous = null; rebuild = true; FrameAssemblyActive = false;
        base.OnClose(); // Native ClearHoveredItems and UnregisterItemStackGrid remain authoritative.
    }

    public override void HandleSlotChangedEvent(int slotNumber, ItemStack stack)
    {
        // Intentionally no backend write. Native Creative2Stack owns cloning to the cursor/bag;
        // a ring position must never overwrite items[slotNumber] in the full filtered catalogue.
    }

    private void ConfigureViewport()
    {
        if (viewport == null) return;
        viewport.Size = new Vector2i(ViewportWidth, ViewportHeight);
        viewport.ClippingSize = new Vector2(ViewportWidth, ViewportHeight);
        viewport.ClippingCenter = new Vector2(ViewportWidth * .5f, -ViewportHeight * .5f);
        if (nativeClip == null && viewport.UiTransform != null)
            nativeClip = viewport.UiTransform.GetComponent<UIPanel>();
        if (nativeClip == null) return;
        nativeClip.clipping = UIDrawCall.Clipping.SoftClip;
        nativeClip.clipSoftness = Vector2.zero;
        nativeClip.baseClipRegion = new Vector4(ViewportWidth * .5f, -ViewportHeight * .5f, ViewportWidth, ViewportHeight);
    }

    private bool CanNavigate => ready && opened && !InputBlocked &&
        !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() &&
        xui?.DragAndDropWindow != null && xui.DragAndDropWindow.IsEmpty();

    private void Scroll(XUiController sender, float delta)
    {
        if (!CanNavigate || RebirthConsoleInputGuardRuntime.BlocksGameplayInput() ||
            float.IsNaN(delta) || float.IsInfinity(delta) || delta == 0f) return;
        // NGUI passes wheel-axis units (ordinary notch 0.1). Keep small/high-resolution deltas,
        // accumulate distinct events, and do not discard a second event simply for sharing a frame.
        SetOffset(RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(targetPixels, MaximumPixelOffset, ViewportHeight, delta > 0f ? -1 : 1) : targetPixels - delta * CellPitch * 10f, false);
#if REBIRTH_UI_DIAGNOSTICS
        RebirthCreativePerformance.CountWheel();
#endif
    }

    private void SetOffset(float value, bool immediate)
    {
        targetPixels = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(value, MaximumPixelOffset, ViewportHeight, true) : Mathf.Clamp(value, 0f, MaximumPixelOffset);
        if (immediate || RebirthScrollbarPagingPolicy.Enabled) pixels = targetPixels;
    }

    private bool CellInteractionInProgress()
    {
        if (xui?.DragAndDropWindow == null || !xui.DragAndDropWindow.IsEmpty()) return true;
        // Timed item actions/edit locks must finish before a selected catalogue cell is recycled.
        for (int i = 0; i < itemControllers.Length; ++i)
            if (itemControllers[i].IsLocked) return true;
        Transform pointer = UICamera.hoveredObject != null ? UICamera.hoveredObject.transform : null;
        if (pointer == null || content?.ViewComponent?.UiTransform == null ||
            !pointer.IsChildOf(content.ViewComponent.UiTransform)) return false;
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        return cursor != null && (cursor.GetMouseButton(UICamera.MouseButton.LeftButton) ||
            cursor.GetMouseButton(UICamera.MouseButton.RightButton));
    }

    public override void Update(float dt)
    {
        if (!opened || !ready || !ViewComponent.IsVisible || xui?.PlayerInventory == null || GameManager.Instance?.World == null)
            return;
#if REBIRTH_UI_DIAGNOSTICS
        long started = RebirthCreativePerformance.Begin();
        try
        {
#endif
            FrameClock = Time.realtimeSinceStartup;
            // A retained/empty AssembleItem.CurrentItem is not an open editor. Checking the
            // actual existing window groups avoids keeping every catalogue cell awake merely
            // because a model still holds an object. No assembly state is cleared or mutated.
            var manager = xui.playerUI.windowManager;
            FrameAssemblyActive = manager != null &&
                (manager.IsWindowOpen("assemble") || manager.IsWindowOpen("cosmetics"));
            InputBlocked = RebirthConsoleInputGuardRuntime.BlocksGameplayInput();
            if (nativeClip == null) ConfigureViewport();
            bool interaction = CellInteractionInProgress();
            bool changedCatalogue = !ReferenceEquals(previous, items);
            if (!InputBlocked && !interaction)
            {
                bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
                if (pagingMode != pagingNow) { pagingMode = pagingNow; thumbDragging = false; }
                if (changedCatalogue)
                {
                    previous = items;
                    rowCount = RebirthCreativeScrollMath.RowCount(CatalogueCount);
                    pixels = targetPixels = 0f;
                    activeStart = -1; rebuild = true;
                }
                targetPixels = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(targetPixels, MaximumPixelOffset, ViewportHeight, true) : Mathf.Clamp(targetPixels, 0f, MaximumPixelOffset);
                if (RebirthScrollbarPagingPolicy.Enabled) pixels = targetPixels;
                // Same clipped-content model as Crafting; interpolation never rounds to a ROW.
                float alpha = 1f - Mathf.Exp(-16f * Mathf.Clamp(dt, 0f, .25f));
                pixels = Mathf.Lerp(pixels, targetPixels, alpha);
                if (Mathf.Abs(pixels - targetPixels) < .15f) pixels = targetPixels;
                int start = RebirthCreativeScrollMath.StartRow(CatalogueCount, pixels);
                bool invalidate = IsDirty && !changedCatalogue && !rebuild;
                if (rebuild || start != activeStart || invalidate) BindRange(start, rebuild, invalidate);
                rebuild = false;
            }
            else
            {
                // Preserve the exact item being pressed/dragged/used, including across filtering.
                // Keep a pending catalogue invalidation until it is safe to replace that source.
                if (changedCatalogue || IsDirty) rebuild = true;
                targetPixels = pixels; thumbDragging = false;
            }
            CommitContentPosition();
            UpdateScrollbar();
            // We have already handled data projection. Bypass only the inherited PAGE rebind,
            // not its input/menu/child-update chain. Rect rows have no native UIGrid.Update.
            IsDirty = false; page = 0;
            base.Update(dt);
#if REBIRTH_UI_DIAGNOSTICS
        }
        finally { RebirthCreativePerformance.EndGrid(started, this); }
#endif
    }

    private void BindRange(int start, bool force, bool invalidate)
    {
        int displayRows = Math.Max(VisibleRowCount, rowCount);
        for (int logical = start; logical < start + PoolRowCount; ++logical)
        {
            int pool = logical % PoolRowCount;
            bool show = logical < displayRows;
            bool remap = force || rowBindings[pool] != logical;
            if (!remap && !invalidate) continue;
            rowBindings[pool] = logical;
            XUiController row = rowViews[pool];
            SetRowVisible(row, show);
            if (show)
            {
                Vector2i position = new Vector2i(0, -logical * CellPitch);
                if (row.ViewComponent.Position != position)
                { row.ViewComponent.Position = position; row.ViewComponent.TryUpdatePosition(); }
            }
            for (int col = 0; col < ColumnCount; ++col)
            {
                var slot = rowSlots[pool][col];
                int data = logical * ColumnCount + col;
                if (remap)
                {
                    ItemStack stack = show && items != null && data < items.Length ? items[data] : ItemStack.Empty;
                    slot.BindCatalogueItem(stack ?? ItemStack.Empty, data, show);
                }
                else if (invalidate) slot.InvalidatePresentation();
            }
#if REBIRTH_UI_DIAGNOSTICS
            if (remap && show) RebirthCreativePerformance.CountRecycledRow();
#endif
        }
        activeStart = start;
    }

    private static void SetRowVisible(XUiController row, bool show)
    {
        XUiView view = row?.ViewComponent;
        if (view == null) return;
        if (view.IsVisible != show) view.IsVisible = show;
        if (view.UiTransform != null && view.UiTransform.gameObject.activeSelf != show)
            view.UiTransform.gameObject.SetActive(show);
    }

    private void CommitContentPosition()
    {
        var view = content?.ViewComponent;
        if (view == null) return;
        int y = Mathf.RoundToInt(pixels);
        if (view.Position.y != y)
        { view.Position = new Vector2i(0, y); view.TryUpdatePosition(); }
    }

    private void UpdateScrollbar()
    {
        bool show = MaximumPixelOffset > 0;
        SetRowVisible(scrollHost, show);
        if (!show) return;
        int height = RebirthCreativeScrollMath.ThumbHeight(CatalogueCount);
        int top = Mathf.RoundToInt((ViewportHeight - height) * pixels / MaximumPixelOffset);
        CommitScrollGeometry(scrollTrack, 0, 0, 14, ViewportHeight);
        CommitScrollGeometry(scrollThumb, 2, -top, 10, height);
    }

    // Same single-owner thumb/widget/collider geometry used by the working Crafting/container
    // controls. Never combine a fixed-height XUi foreground with UIScrollBar's own drawRegion.
    private static void CommitScrollGeometry(XUiController controller, int x, int y, int width, int height)
    {
        XUiView view = controller?.ViewComponent;
        if (view == null) return;
        bool changed = view.Position != new Vector2i(x, y) || view.Size != new Vector2i(width, height);
        if (changed)
        {
            view.Position = new Vector2i(x, y); view.Size = new Vector2i(width, height);
            if (view.UiTransform != null) view.updateData();
        }
        if (view.UiTransform == null) return;
        UIWidget widget = view.UiTransform.GetComponent<UIWidget>();
        if (widget != null)
        { if (widget.width != width) widget.width = width; if (widget.height != height) widget.height = height; }
        BoxCollider collider = view.UiTransform.GetComponent<BoxCollider>();
        if (collider != null && (changed || collider.size.x != width || collider.size.y != height))
        {
            collider.center = new Vector3(width * .5f, -height * .5f, collider.center.z);
            collider.size = new Vector3(width, height, collider.size.z);
        }
    }

    private bool TryPointerY(out float localY)
    {
        localY = 0f;
        Transform host = scrollHost?.ViewComponent?.UiTransform;
        Camera camera = UICamera.currentCamera;
        if (host == null || camera == null) return false;
        Vector2 mouse = UICamera.currentTouch != null ? UICamera.currentTouch.pos : (Vector2)Input.mousePosition;
        Ray ray = camera.ScreenPointToRay(mouse);
        Plane plane = new Plane(host.forward, host.position);
        float distance;
        if (!plane.Raycast(ray, out distance)) return false;
        localY = -host.InverseTransformPoint(ray.GetPoint(distance)).y;
        return true;
    }
    private void TrackPressed(XUiController sender, int button)
    {
        if ((button != -1 && button != 0) || !CanNavigate || MaximumPixelOffset == 0) return;
        float y;
        if (!TryPointerY(out y)) return;
        int thumb = RebirthCreativeScrollMath.ThumbHeight(CatalogueCount);
        SetOffset((y - thumb * .5f) * MaximumPixelOffset / Math.Max(1, ViewportHeight - thumb), true);
    }
    private void ThumbDragged(XUiController sender, EDragType type, Vector2 delta)
    {
        if (!CanNavigate || MaximumPixelOffset == 0) { thumbDragging = false; return; }
        float y;
        if (!TryPointerY(out y)) { if (type == EDragType.DragEnd) thumbDragging = false; return; }
        if (type == EDragType.DragStart)
        { thumbDragging = true; dragStartY = y; dragStartPixels = pixels; }
        if (!thumbDragging) return;
        int thumb = RebirthCreativeScrollMath.ThumbHeight(CatalogueCount);
        SetOffset(dragStartPixels + (y - dragStartY) * MaximumPixelOffset / Math.Max(1, ViewportHeight - thumb), true);
        if (type == EDragType.DragEnd) thumbDragging = false;
    }
}

/// <summary>Pure fixed-pool geometry, independent of Unity/NGUI and testable without game assemblies.</summary>
internal static class RebirthCreativeScrollMath
{
    internal static int RowCount(int count) => count <= 0 ? 0 : 1 + (count - 1) / 17;
    internal static int StartRow(int count, float pixels)
    {
        int rows = Math.Max(10, RowCount(count));
        return Math.Max(0, Math.Min(Math.Max(0, rows - 12), (int)Math.Floor(Math.Max(0f, pixels) / 75f) - 1));
    }
    internal static int ThumbHeight(int count)
    {
        int rows = Math.Max(10, RowCount(count));
        return Math.Max(30, Math.Min(750, (int)Math.Round(7500.0 / rows)));
    }
}
