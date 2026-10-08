using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Smooth wide viewport for the Crafting-only backpack presenter. Thirteen proportionally-scaled columns by four rows are visible at rest;
/// wheel movement interpolates between row positions while track/thumb input remains continuous.
/// Every additional authoritative Bag row remains reachable without rebinding item contents.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingInventoryScroll : XUiController
{
    private XUiC_RebirthPersonalCrafting personalOwner;
    private const int FallbackNativeCell = 75;
    private const int CellGap = 2;
    private const int BasePitch = FallbackNativeCell + CellGap;
    private int Columns => windowGroup?.Controller is XUiC_RebirthCookingStation ? 26 : windowGroup?.Controller is XUiC_RebirthQuestTurnInWorkspace ? 12 : RebirthCraftingInventoryBridge.Columns;
    private int VisibleRows => windowGroup?.Controller is XUiC_RebirthCookingStation ? 3 : windowGroup?.Controller is XUiC_RebirthQuestTurnInWorkspace ? 5 : RebirthCraftingInventoryBridge.VisibleRows;
    private const int ScrollWidth = 24;
    private const int ScrollbarTrackWidth = RebirthScrollbarPresentation.TrackWidth;
    private const int ScrollbarThumbWidth = RebirthScrollbarPresentation.ThumbWidth;
    private const int MinThumb = 30;

    private XUiC_RebirthCraftingInventory inventory;
    private XUiController viewport;
    private XUiController track;
    private XUiController thumb;
    private Vector2i baseGridPosition;
    private int wiredSlotGeneration = int.MinValue;
    private readonly HashSet<XUiController> scrollWired = new HashSet<XUiController>();
    private bool coreWired;
    private float nextWireCheck;
    private bool dragging;
    private bool pagingMode;
    private float dragAccumulated;
    private float dragStartOffset;
    private float pixelOffset;
    private float targetPixelOffset;
    private int totalRows = 1;
    private int effectiveCell = FallbackNativeCell;
    private int effectivePitch = BasePitch;
    private int viewportHeight = BasePitch * RebirthCraftingInventoryBridge.VisibleRows;
    private int viewportWidth = BasePitch * RebirthCraftingInventoryBridge.Columns;
    private int lastPhysical = -1;
    private Vector2i lastSize;
    private int lastEventScrollFrame = -1;
    private float nextTrace;
    private int lastHoverTraceSlot = -2;
    private int geometryCommits;

    // PC132: bind the actual components, not publicized XUi wrapper members.
    // Each cache is refreshed only if its view host changes or the component is destroyed.
    private Transform nativeGridHost;
    private UIGrid nativeGridComponent;
    private Transform nativeClipHost;
    private UIPanel nativeClipComponent;
    private Vector2i[] slotPositionPadding;

    public int CurrentViewportHeight => viewportHeight;
    public float CaptureOffset() => pixelOffset;
    public void RestoreOffset(float value)
    {
        RefreshGeometry(true);
        pixelOffset = targetPixelOffset = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(value, MaxPixelOffset, VisibleRows * effectivePitch, true) : Mathf.Clamp(value, 0f, MaxPixelOffset);
        ApplyGridPosition();
        UpdateScrollbar();
    }
    public int CurrentViewportWidth => viewportWidth;

    public int GetFirstVisibleRow()
    {
        return effectiveCell > 0
            ? Mathf.Clamp(Mathf.RoundToInt(pixelOffset / effectivePitch), 0, Math.Max(0, totalRows - VisibleRows))
            : 0;
    }

    public int GetVisibleRowIndex(int slotNumber)
    {
        if (slotNumber < 0 || effectiveCell <= 0)
            return -1;
        return slotNumber / Columns - GetFirstVisibleRow();
    }

    public bool IsSlotVisible(int slotNumber)
    {
        int row = GetVisibleRowIndex(slotNumber);
        return slotNumber >= 0 && slotNumber < lastPhysical && row >= 0 && row < VisibleRows;
    }

    internal bool IntersectsViewport(int slotNumber)
    {
        float top = slotNumber / Columns * effectivePitch - pixelOffset;
        return slotNumber >= 0 && slotNumber < lastPhysical && top < viewportHeight && top + effectiveCell > 0;
    }

    public override void Init()
    {
        base.Init();
        personalOwner = GetParentByType<XUiC_RebirthPersonalCrafting>();
        Resolve();
        Wire();
        RefreshGeometry(true);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        Resolve();
        Wire();
        pixelOffset = 0f;
        targetPixelOffset = 0f;
        nextTrace = 0f;
        RefreshGeometry(true);
        ApplyGridPosition();
        TraceState("open", true);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (personalOwner != null && !personalOwner.State.IsOpen) return;
        Resolve();
        if(Time.time>=nextWireCheck){nextWireCheck=Time.time+1f;Wire();}
        RefreshGeometry(false);
        PollMouseWheelFallback();
        AnimateScroll(dt);
        var slots = inventory?.GetItemStackControllers();
        for (int i = 0; slots != null && i < slots.Length; i++)
            (slots[i] as XUiC_RebirthCraftingInventorySlot)?.FinishViewport();
        TraceState("update", false);
        TraceHoverGeometry();
    }

    private void Resolve()
    {
        if (inventory == null)
        {
            inventory = GetChildByType<XUiC_RebirthCraftingInventory>();
            if (inventory != null && inventory.ViewComponent != null)
                baseGridPosition = inventory.ViewComponent.Position;
        }
        if (viewport == null)
            viewport = GetChildById("rebirthCraftingInventoryViewport");
        if (track == null)
            track = GetChildById("rebirthCraftingInventoryScrollTrack");
        if (thumb == null)
            thumb = GetChildById("rebirthCraftingInventoryScrollThumb");
    }

    private void Wire()
    {
        if (inventory == null)
            return;

        if (!coreWired && track != null && thumb != null)
        {
            WireScrollOnce(track);
            WireScrollOnce(thumb);
            if (thumb.ViewComponent != null)
                thumb.ViewComponent.EventOnDrag = true;
            thumb.OnDrag += Thumb_OnDrag;
            track.OnPress += Track_OnPress;
            coreWired = true;
        }

        XUiC_ItemStack[] slots=inventory.GetItemStackControllers();
        if(slots==null)return;
        int generation=17;unchecked{for(int i=0;i<slots.Length;i++)generation=generation*31+(slots[i]!=null?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(slots[i]):0);}
        if(generation==wiredSlotGeneration)return;
        wiredSlotGeneration=generation;
        // Only a changed controller generation pays the recursive wiring cost.
        WireScrollRecursive(inventory);
        for (int i = 0; i < slots.Length; i++)
        {
            XUiC_ItemStack slot = slots[i];
            if (slot == null)
                continue;
            if (slot.ViewComponent != null)
            {
                // Native ItemStack requires these event flags for mouse select/drag while our
                // viewport itself deliberately has no input collider over the slots.
                slot.ViewComponent.EventOnPress = true;
                slot.ViewComponent.EventOnDrag = true;
            }
            WireScrollRecursive(slot);
        }
    }

    private void WireScrollRecursive(XUiController controller)
    {
        if (controller == null)
            return;
        WireScrollOnce(controller);
        if (controller.Children == null)
            return;
        for (int i = 0; i < controller.Children.Count; i++)
            WireScrollRecursive(controller.Children[i]);
    }

    private void WireScrollOnce(XUiController controller)
    {
        if (controller == null || scrollWired.Contains(controller))
            return;
        scrollWired.Add(controller);
        if (controller.ViewComponent != null)
            controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += HandleScroll;
    }

    /// <summary>
    /// PC131: the layout owner calls this after assigning the current surface's scroll bounds.
    /// Commit the live grid here, not on the first wheel event or a later controller update.
    /// Unchanged bounds/capacity are a no-op; this does not reset scrolling or rebind items.
    /// </summary>
    public void ApplyLayoutGeometry()
    {
        Resolve();
        RefreshGeometry(false);
    }

    private void RefreshGeometry(bool force)
    {
        if (inventory == null || inventory.ViewComponent == null || ViewComponent == null)
            return;

        int physical = RebirthCraftingInventoryBridge.GetPhysicalSlotCount(xui);
        Vector2i size = ViewComponent.Size;
        if (!force && physical == lastPhysical && size.x == lastSize.x && size.y == lastSize.y)
            return;

        // Do not mark a layout as applied until the live grid, clip view and slot roots exist.
        // An early Init call must remain retryable with identical bounds when views are ready.
        XUiV_Grid grid = inventory.ViewComponent as XUiV_Grid;
        XUiV_Panel clipView = viewport != null ? viewport.ViewComponent as XUiV_Panel : null;
        XUiC_ItemStack[] slots = inventory.GetItemStackControllers();
        UIGrid nativeGrid = ResolveNativeGrid(grid);
        UIPanel nativeClip = ResolveNativeClip(clipView);
        if (grid == null || nativeGrid == null || grid.UiTransform == null ||
            clipView == null || nativeClip == null || clipView.UiTransform == null || slots == null || slots.Length == 0 ||
            slots.Length < Math.Min(physical, RebirthCraftingInventoryBridge.AuthoredSlotCount))
            return;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null || slots[i].ViewComponent == null || slots[i].ViewComponent.UiTransform == null)
                return;

        // Keep the same logical rows when switching between the two fitted slot pitches.
        // Both current and target offsets are converted, preserving an in-flight interpolation.
        float currentRowOffset = pixelOffset / Math.Max(1, effectivePitch);
        float targetRowOffset = targetPixelOffset / Math.Max(1, effectivePitch);
        totalRows = Math.Max(1, (physical + Columns - 1) / Columns);

        int availableW = Math.Max(Columns * 36, size.x - ScrollWidth - 4);
        int availableH = Math.Max(VisibleRows * 36, size.y);

        // PC102: derive the slot pitch from horizontal space first. V3.2 b10's live ItemStack
        // control is 75x75 (confirmed by runtime diagnostics), not the 64px assumption used by
        // PC101. Scale each complete native ItemStack root from its ACTUAL live view size so every
        // child layer/collider is transformed by one identical ratio.
        int pitchByWidth = Math.Max(1, availableW / Columns);
        effectivePitch = Math.Max(40, pitchByWidth);
        if (effectivePitch * VisibleRows > availableH)
            effectivePitch = Math.Max(40, availableH / VisibleRows);
        effectiveCell = Math.Max(38, effectivePitch - CellGap);
        viewportWidth = effectivePitch * Columns;
        viewportHeight = Math.Min(availableH, effectivePitch * VisibleRows);

        grid.Columns = Columns;
        grid.Rows = (RebirthCraftingInventoryBridge.AuthoredSlotCount + Columns - 1) / Columns;
        grid.CellWidth = effectivePitch;
        grid.CellHeight = effectivePitch;

        if (slots != null)
        {
            if (slotPositionPadding == null || slotPositionPadding.Length != slots.Length)
                slotPositionPadding = new Vector2i[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                XUiC_ItemStack slot = slots[i];
                if (slot == null || slot.ViewComponent == null || slot.ViewComponent.UiTransform == null)
                    continue;

                // Resolve the view's padded position through the already-supported public
                // position path. Unlike the internal data-refresh routine, TryUpdatePosition
                // does not clear deferred position state. Remember its padding here and mirror
                // the native grid result below, so a later XUi pass cannot restore an old slot
                // position. All of this occurs inside this single synchronous layout commit.
                XUiView slotView = slot.ViewComponent;
                Vector2i logicalPosition = slotView.Position;
                slotView.Position = logicalPosition;
                slotView.TryUpdatePosition();
                Vector3 paddedPosition = slotView.UiTransform.localPosition;
                slotPositionPadding[i] = new Vector2i(
                    Mathf.RoundToInt(paddedPosition.x) - logicalPosition.x,
                    Mathf.RoundToInt(paddedPosition.y) - logicalPosition.y);
                bool authoritative = i < physical;
                // Scale from the actual native root dimensions. ViewComponent.Size remains the
                // unscaled stock size (75x75 in V3.2 b10), so this assignment is stable across
                // repeated refreshes and proportionally scales icon, count, bars, hover/selection
                // layers and every child collider together.
                int nativeW = Math.Max(1, slot.ViewComponent.Size.x);
                int nativeH = Math.Max(1, slot.ViewComponent.Size.y);
                float uniformScale = Math.Min(effectiveCell / (float)nativeW, effectiveCell / (float)nativeH);
                slot.ViewComponent.UiTransform.localScale = authoritative ? Vector3.one * uniformScale : Vector3.zero;
                if (!authoritative)
                {
                    slot.ViewComponent.IsVisible = false;
                    slot.ViewComponent.Enabled = false;
                    slot.ViewComponent.UiTransform.gameObject.SetActive(false);
                }
            }
        }

        // The viewport's logical size and native clipping rectangle are separate state.
        // Commit both now so no frame uses the previous tab's clip with the new slot scale.
        clipView.Size = new Vector2i(viewportWidth, viewportHeight);
        clipView.ClippingSize = new Vector2(viewportWidth, viewportHeight);
        clipView.ClippingCenter = new Vector2(viewportWidth * 0.5f, -viewportHeight * 0.5f);
        clipView.TryUpdatePosition();
        Vector4 clipRegion = new Vector4(viewportWidth * 0.5f, -viewportHeight * 0.5f,
            viewportWidth, viewportHeight);
        if (nativeClip.baseClipRegion != clipRegion)
            nativeClip.baseClipRegion = clipRegion;
        if (nativeClip.clipping != UIDrawCall.Clipping.SoftClip)
            nativeClip.clipping = UIDrawCall.Clipping.SoftClip;
        if (nativeClip.clipSoftness != Vector2.zero)
            nativeClip.clipSoftness = Vector2.zero;

        int scrollX = Math.Min(Math.Max(0, size.x - 20), viewportWidth + 2);
        int trackHeight = viewportHeight;
        if (track != null && track.ViewComponent != null)
        {
            track.ViewComponent.Position = new Vector2i(scrollX, 0);
            track.ViewComponent.Size = new Vector2i(ScrollbarTrackWidth, trackHeight);
        }

        pixelOffset = Mathf.Clamp(currentRowOffset * effectivePitch, 0f, MaxPixelOffset);
        targetPixelOffset = Mathf.Clamp(targetRowOffset * effectivePitch, 0f, MaxPixelOffset);
        ApplyGridPosition();
        grid.TryUpdatePosition();

        // PC131: XUiV_Grid.CellWidth/CellHeight are only the view-side values. The live
        // UIGrid owns child transforms and may still contain the previous surface's pitch.
        // Updating only the view fields scales cells immediately but leaves their spacing
        // stale until a later native update (previously exposed by the first scroll).
        // Use the SAME native grid for the immediate commit; no second layout/slot owner.
        nativeGrid.cellWidth = effectivePitch;
        nativeGrid.cellHeight = effectivePitch;
        nativeGrid.maxPerLine = Columns;
        nativeGrid.Reposition();

        // The native grid remains the only row/column layout calculator. Copy its final
        // positions back to the XUi views (minus their padding) instead of invoking hidden
        // refresh methods. Deferred view updates will now write the SAME positions.
        for (int i = 0; i < slots.Length; i++)
        {
            XUiView slotView = slots[i].ViewComponent;
            Vector3 arrangedPosition = slotView.UiTransform.localPosition;
            Vector2i padding = slotPositionPadding[i];
            Vector2i logicalPosition = new Vector2i(
                Mathf.RoundToInt(arrangedPosition.x) - padding.x,
                Mathf.RoundToInt(arrangedPosition.y) - padding.y);
            Vector2i previous = slotView.Position;
            if (previous.x != logicalPosition.x || previous.y != logicalPosition.y)
                slotView.Position = logicalPosition;
            slotView.TryUpdatePosition();
        }

        UpdateScrollbar();
        CommitScrollbarGeometry(track);
        CommitScrollbarGeometry(thumb);
        ViewComponent.TryUpdatePosition();

        // Cache only the layout that was actually committed to live native geometry.
        lastPhysical = physical;
        lastSize = size;
        geometryCommits++;
        TraceState("layout-commit", true);
    }

    private UIGrid ResolveNativeGrid(XUiV_Grid grid)
    {
        Transform host = grid != null ? grid.UiTransform : null;
        if (host == null)
        {
            nativeGridHost = null;
            nativeGridComponent = null;
            return null;
        }
        if (nativeGridHost != host || nativeGridComponent == null)
        {
            nativeGridHost = host;
            nativeGridComponent = host.GetComponent<UIGrid>();
        }
        return nativeGridComponent;
    }

    private UIPanel ResolveNativeClip(XUiV_Panel clipView)
    {
        Transform host = clipView != null ? clipView.UiTransform : null;
        if (host == null)
        {
            nativeClipHost = null;
            nativeClipComponent = null;
            return null;
        }
        if (nativeClipHost != host || nativeClipComponent == null)
        {
            nativeClipHost = host;
            nativeClipComponent = host.GetComponent<UIPanel>();
        }
        return nativeClipComponent;
    }

    internal static void CommitScrollbarGeometry(XUiController controller) => RebirthScrollbarPresentation.CommitGeometry(controller);

    private float MaxPixelOffset
    {
        get { return Math.Max(0f, totalRows * effectivePitch - viewportHeight); }
    }

    private void HandleScroll(XUiController sender, float delta)
    {
        if (Mathf.Approximately(delta, 0f)) return;
        lastEventScrollFrame = Time.frameCount;
        float before = targetPixelOffset;
        if (delta > 0f)
            SetTargetPixelOffset(RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(targetPixelOffset, MaxPixelOffset, VisibleRows * effectivePitch, -1) : targetPixelOffset - effectivePitch);
        else if (delta < 0f)
            SetTargetPixelOffset(RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(targetPixelOffset, MaxPixelOffset, VisibleRows * effectivePitch, 1) : targetPixelOffset + effectivePitch);
        if (RebirthLogSettings.CraftingUiLoggingEnabled && Math.Abs(before - targetPixelOffset) > 0.01f)
            Log.Out("[REBIRTH Crafting InventoryTrace] scroll source=" + ControllerId(sender)
                + " delta=" + delta.ToString("0.###")
                + " current=" + pixelOffset.ToString("0.0")
                + " target=" + targetPixelOffset.ToString("0.0")
                + " max=" + MaxPixelOffset.ToString("0.0"));
    }

    private void PollMouseWheelFallback()
    {
        if (lastEventScrollFrame == Time.frameCount || ViewComponent == null || !ViewComponent.IsVisible)
            return;
        float delta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(delta) < 0.0001f || !IsMouseOverInventoryArea())
            return;
        float before = targetPixelOffset;
        if (delta > 0f)
            SetTargetPixelOffset(RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(targetPixelOffset, MaxPixelOffset, VisibleRows * effectivePitch, -1) : targetPixelOffset - effectivePitch);
        else
            SetTargetPixelOffset(RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(targetPixelOffset, MaxPixelOffset, VisibleRows * effectivePitch, 1) : targetPixelOffset + effectivePitch);
        if (RebirthLogSettings.CraftingUiLoggingEnabled && Math.Abs(before - targetPixelOffset) > 0.01f)
            Log.Out("[REBIRTH Crafting InventoryTrace] scroll source=poll delta=" + delta.ToString("0.###")
                + " current=" + pixelOffset.ToString("0.0")
                + " target=" + targetPixelOffset.ToString("0.0")
                + " max=" + MaxPixelOffset.ToString("0.0"));
    }

    private bool IsMouseOverInventoryArea()
    {
        GameObject hovered = UICamera.hoveredObject;
        Transform candidate = hovered != null ? hovered.transform : null;
        if (IsDescendant(candidate, inventory) || IsDescendant(candidate, viewport) ||
            IsDescendant(candidate, track) || IsDescendant(candidate, thumb))
            return true;

        XUiC_ItemStack[] slots = inventory != null ? inventory.GetItemStackControllers() : null;
        if (slots == null)
            return false;
        for (int i = 0; i < slots.Length; i++)
        {
            XUiC_ItemStack slot = slots[i];
            if (slot == null || slot.ViewComponent == null || !slot.ViewComponent.IsVisible)
                continue;
            if (IsMouseInsideCollider(slot.ViewComponent.UiTransform))
                return true;
        }
        return false;
    }

    private void TraceState(string reason, bool force)
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled)
            return;
        if (!force && Time.realtimeSinceStartup < nextTrace)
            return;
        nextTrace = Time.realtimeSinceStartup + 1f;
        Vector2i gridPos = inventory != null && inventory.ViewComponent != null
            ? inventory.ViewComponent.Position : new Vector2i(-9999, -9999);
        XUiC_ItemStack[] traceSlots = inventory != null ? inventory.GetItemStackControllers() : null;
        int controllerCount = traceSlots != null ? traceSlots.Length : -1;
        int visibleControllerCount = 0;
        if (traceSlots != null)
        {
            for (int i = 0; i < traceSlots.Length; i++)
                if (traceSlots[i]?.ViewComponent != null && traceSlots[i].ViewComponent.IsVisible)
                    visibleControllerCount++;
        }
        XUiV_Grid traceGrid = inventory != null ? inventory.ViewComponent as XUiV_Grid : null;
        UIGrid nativeGrid = ResolveNativeGrid(traceGrid);
        float nativePitch = nativeGrid != null ? nativeGrid.cellWidth : -1f;
        float lastColumnRight = -1f;
        if (traceSlots != null && traceSlots.Length >= Columns)
        {
            XUiView lastColumn = traceSlots[Columns - 1]?.ViewComponent;
            if (lastColumn != null && lastColumn.UiTransform != null)
                lastColumnRight = lastColumn.UiTransform.localPosition.x +
                    lastColumn.Size.x * lastColumn.UiTransform.localScale.x;
        }
        Log.Out("[REBIRTH Crafting InventoryTrace] geometry reason=" + (reason ?? "unknown")
            + " columns=" + Columns
            + " authored=" + RebirthCraftingInventoryBridge.AuthoredSlotCount
            + " physical=" + lastPhysical
            + " unencumbered=" + RebirthCraftingInventoryBridge.GetUnencumberedSlotCount(xui)
            + " totalRows=" + totalRows
            + " visibleRows=" + VisibleRows
            + " cell=" + effectiveCell
            + " pitch=" + effectivePitch
            + " nativePitch=" + nativePitch.ToString("0.###")
            + " lastColumnRight=" + lastColumnRight.ToString("0.###")
            + " geometryCommits=" + geometryCommits
            + " viewport=" + viewportWidth + "x" + viewportHeight
            + " offset=" + pixelOffset.ToString("0.0")
            + " target=" + targetPixelOffset.ToString("0.0")
            + " max=" + MaxPixelOffset.ToString("0.0")
            + " gridPos=" + gridPos.x + "," + gridPos.y
            + " controllers=" + controllerCount
            + " controllerVisible=" + visibleControllerCount
            + " inventoryVisible=" + (inventory != null && inventory.ViewComponent != null && inventory.ViewComponent.IsVisible));
    }

    private void TraceHoverGeometry()
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled || inventory == null)
            return;

        GameObject hovered = UICamera.hoveredObject;
        Transform candidate = hovered != null ? hovered.transform : null;
        XUiC_ItemStack[] slots = inventory.GetItemStackControllers();
        int hoveredSlot = -1;
        if (slots != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                XUiC_ItemStack slot = slots[i];
                if (slot != null && slot.ViewComponent != null && slot.ViewComponent.IsVisible && IsDescendant(candidate, slot))
                {
                    hoveredSlot = i;
                    break;
                }
            }
        }

        if (hoveredSlot == lastHoverTraceSlot)
            return;
        lastHoverTraceSlot = hoveredSlot;

        if (hoveredSlot < 0 || slots == null || hoveredSlot >= slots.Length || slots[hoveredSlot] == null || slots[hoveredSlot].ViewComponent == null)
        {
            Log.Out("[REBIRTH Crafting InventoryHover] slot=<none> hovered=" + (hovered != null ? hovered.name : "<null>"));
            return;
        }

        XUiC_ItemStack active = slots[hoveredSlot];
        Vector2i pos = active.ViewComponent.Position;
        Vector2i slotSize = active.ViewComponent.Size;
        Vector3 localScale = active.ViewComponent.UiTransform != null ? active.ViewComponent.UiTransform.localScale : Vector3.zero;
        int colliderCount = active.ViewComponent.UiTransform != null
            ? active.ViewComponent.UiTransform.GetComponentsInChildren<Collider>(false).Length
            : 0;
        int actualW = Mathf.RoundToInt(slotSize.x * localScale.x);
        int actualH = Mathf.RoundToInt(slotSize.y * localScale.y);
        Log.Out("[REBIRTH Crafting InventoryHover] slot=" + hoveredSlot
            + " physical=" + lastPhysical
            + " unencumbered=" + RebirthCraftingInventoryBridge.GetUnencumberedSlotCount(xui)
            + " pos=" + pos.x + "," + pos.y
            + " native=" + slotSize.x + "x" + slotSize.y
            + " scale=" + localScale.x.ToString("0.###")
            + " actual=" + actualW + "x" + actualH
            + " target=" + effectiveCell + "x" + effectiveCell
            + " pitch=" + effectivePitch
            + " colliders=" + colliderCount
            + " hovered=" + (hovered != null ? hovered.name : "<null>"));
    }

    private static string ControllerId(XUiController controller)
    {
        return controller != null && controller.ViewComponent != null && !string.IsNullOrEmpty(controller.ViewComponent.ID)
            ? controller.ViewComponent.ID : "<no-id>";
    }

    private static bool IsDescendant(Transform candidate, XUiController controller)
    {
        if (candidate == null || controller == null || controller.ViewComponent == null || controller.ViewComponent.UiTransform == null)
            return false;
        Transform root = controller.ViewComponent.UiTransform;
        while (candidate != null)
        {
            if (candidate == root)
                return true;
            candidate = candidate.parent;
        }
        return false;
    }

    private static bool IsMouseInsideCollider(Transform transform)
    {
        if (transform == null || UICamera.currentCamera == null)
            return false;
        Collider[] colliders = transform.GetComponentsInChildren<Collider>(false);
        Vector2 mouse = Input.mousePosition;
        Camera camera = UICamera.currentCamera;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
                continue;
            Bounds b = collider.bounds;
            Vector3 a = camera.WorldToScreenPoint(b.min);
            Vector3 c = camera.WorldToScreenPoint(b.max);
            if (mouse.x >= Mathf.Min(a.x, c.x) && mouse.x <= Mathf.Max(a.x, c.x) &&
                mouse.y >= Mathf.Min(a.y, c.y) && mouse.y <= Mathf.Max(a.y, c.y))
                return true;
        }
        return false;
    }


    public void EnsureSlotVisible(int slotNumber)
    {
        if (slotNumber < 0 || slotNumber >= lastPhysical || effectiveCell <= 0)
            return;

        int row = slotNumber / Columns;
        if (RebirthScrollbarPagingPolicy.Enabled)
        {
            SetTargetPixelOffset(RebirthScrollbarPagingPolicy.EnsureVisible(targetPixelOffset, row * effectivePitch, row * effectivePitch + effectiveCell, MaxPixelOffset, viewportHeight, VisibleRows * effectivePitch));
            ApplyGridPosition(); UpdateScrollbar(); return;
        }
        int firstVisibleRow = Mathf.Clamp(Mathf.RoundToInt(targetPixelOffset / effectivePitch), 0, Math.Max(0, totalRows - VisibleRows));
        if (row < firstVisibleRow)
            SetTargetPixelOffset(row * effectivePitch);
        else if (row >= firstVisibleRow + VisibleRows)
            SetTargetPixelOffset((row - VisibleRows + 1) * effectivePitch);
    }

    private void SetTargetPixelOffset(float value)
    {
        targetPixelOffset = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(value, MaxPixelOffset, VisibleRows * effectivePitch, true) : Mathf.Clamp(value, 0f, MaxPixelOffset);
        if (RebirthScrollbarPagingPolicy.Enabled) { pixelOffset = targetPixelOffset; ApplyGridPosition(); UpdateScrollbar(); }
    }

    private void SetImmediatePixelOffset(float value)
    {
        pixelOffset = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(value, MaxPixelOffset, VisibleRows * effectivePitch, true) : Mathf.Clamp(value, 0f, MaxPixelOffset);
        targetPixelOffset = pixelOffset;
        ApplyGridPosition();
        UpdateScrollbar();
    }

    private void AnimateScroll(float dt)
    {
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingNow && (RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || xui?.DragAndDropWindow?.IsEmpty() == false)) return;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; dragging = false; }
        targetPixelOffset = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.SnapAbsolute(targetPixelOffset, MaxPixelOffset, VisibleRows * effectivePitch, true) : Mathf.Clamp(targetPixelOffset, 0f, MaxPixelOffset);
        if (RebirthScrollbarPagingPolicy.Enabled) { pixelOffset = targetPixelOffset; ApplyGridPosition(); UpdateScrollbar(); }
        if (Math.Abs(pixelOffset - targetPixelOffset) < 0.05f)
        {
            if (pixelOffset != targetPixelOffset)
            {
                pixelOffset = targetPixelOffset;
                ApplyGridPosition();
                UpdateScrollbar();
            }
            return;
        }
        float t = Mathf.Clamp01(Math.Max(0f, dt) * 16f);
        pixelOffset = Mathf.Lerp(pixelOffset, targetPixelOffset, t);
        if (Math.Abs(pixelOffset - targetPixelOffset) < 0.15f)
            pixelOffset = targetPixelOffset;
        ApplyGridPosition();
        UpdateScrollbar();
    }

    private void ApplyGridPosition()
    {
        if (inventory == null || inventory.ViewComponent == null)
            return;
        // Preserve the proven PC112 backpack behavior: move the complete native grid continuously
        // and let the viewport clip it. Do NOT toggle individual ItemStack visibility while the
        // interpolation is in flight; native Backpack refreshes reassert those flags and the two
        // systems fight each other, producing the stutter/regression seen in PC113/PC114.
        Vector2i position = new Vector2i(baseGridPosition.x, baseGridPosition.y + Mathf.RoundToInt(pixelOffset));
        Vector2i previous = inventory.ViewComponent.Position;
        if (previous.x != position.x || previous.y != position.y)
            inventory.ViewComponent.Position = position;
        inventory.ViewComponent.TryUpdatePosition();
    }

    private int ThumbHeight(int trackHeight) => RebirthScrollbarPresentation.ThumbHeight(trackHeight, viewportHeight, totalRows * effectivePitch);

    private void UpdateScrollbar()
    {
        if(track?.ViewComponent == null) return;
        RebirthScrollbarPresentation.Render(track,thumb,track.ViewComponent.Position,
            viewportHeight,viewportHeight,totalRows*effectivePitch,pixelOffset);
    }

    private void Thumb_OnDrag(XUiController sender, EDragType dragType, Vector2 delta)
    {
        if (MaxPixelOffset <= 0f)
            return;
        if (dragType == EDragType.DragStart)
        {
            dragging = true;
            dragAccumulated = 0f;
            dragStartOffset = pixelOffset;
        }
        if (!dragging)
            return;

        if (dragType != EDragType.DragEnd)
            dragAccumulated += -delta.y;
        int travel = Math.Max(1, viewportHeight - ThumbHeight(viewportHeight));
        SetImmediatePixelOffset(dragStartOffset + dragAccumulated * (MaxPixelOffset / travel));
        if (dragType == EDragType.DragEnd)
            dragging = false;
    }

    private void Track_OnPress(XUiController sender, int mouseButton)
    {
        if ((mouseButton != 0 && mouseButton != -1) || MaxPixelOffset <= 0f || track == null || track.ViewComponent == null)
            return;
        Collider collider = track.ViewComponent.UiTransform != null ? track.ViewComponent.UiTransform.GetComponent<Collider>() : null;
        Camera camera = UICamera.currentCamera;
        if (collider == null || camera == null)
        {
            SetTargetPixelOffset(RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(targetPixelOffset, MaxPixelOffset, VisibleRows * effectivePitch, 1) : targetPixelOffset + effectivePitch);
            return;
        }

        Vector2 mouse = UICamera.currentTouch != null ? UICamera.currentTouch.pos : (Vector2)Input.mousePosition;
        Bounds bounds = collider.bounds;
        float y1 = camera.WorldToScreenPoint(bounds.min).y;
        float y2 = camera.WorldToScreenPoint(bounds.max).y;
        float bottom = Mathf.Min(y1, y2);
        float top = Mathf.Max(y1, y2);
        if (top - bottom < 0.01f)
            return;
        float normalized = 1f - Mathf.Clamp01((mouse.y - bottom) / (top - bottom));
        float desired = normalized * MaxPixelOffset;
        SetTargetPixelOffset(desired);
    }
}
