using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Dynamic Rebirth presentation around the native personal-crafting queue.
/// The real native XUiC_RecipeStack children remain authoritative for crafting,
/// cancellation, ingredient refund and completion. Empty native slots are never shown.
/// The native queue-child hierarchy stays intact while the presentation scrolls continuously.
/// A runtime NGUI UIPanel is attached to the existing lightweight rows-host GameObject, so the
/// cards are genuinely clipped at the queue viewport without inserting an XUi controller between
/// the native queue and its slots. Cards that intersect the viewport stay rendered while sliding
/// behind that clip; fully off-screen cards are culled to avoid needless RecipeStack presentation work.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCraftingQueue : XUiC_CraftingQueue
{
    public const int MaximumQueueEntries = 50;

    private const int HeaderHeight = 46;
    private const int BottomPad = 8;
    private const int CardGap = 6;
    private int Columns => windowGroup?.Controller is XUiC_RebirthCookingStation ? 1 : 2;
    private int CardHeight => windowGroup?.Controller is XUiC_RebirthCookingStation ? Math.Max(278, ViewComponent.Size.y - HeaderHeight - 6 - 10) : 88;
    private const int ScrollbarWidth = 18;
    private const int ScrollbarTrackWidth = 16;
    private const int ScrollbarThumbWidth = 12;
    private const int MinThumbHeight = 34;
    private const float WheelPixels = 38f;
    private const float ScrollLerpSpeed = 16f;

    private XUiC_RebirthCraftingQueueEntry[] entries = Array.Empty<XUiC_RebirthCraftingQueueEntry>();
    private XUiController rowsHost;
    private UIPanel rowsClipPanel;
    private bool rowsClipWidgetsRegistered;
    private XUiController scrollTrack;
    private XUiController scrollThumb;
    private XUiController capacityLabel;
    private readonly HashSet<XUiController> scrollWired = new HashSet<XUiController>();

    private int lastCapacity = -1;
    private int lastActiveCount = -1;
    private Vector2i lastSize;
    private int viewportHeight = 1;
    private int cardWidth = 120;
    private float scrollPixels;
    private float scrollTargetPixels;
    private bool draggingThumb;
    private bool pagingMode;
    private float dragAccumulated;
    private float dragStartOffset;
    private int lastEventScrollFrame = -1;
    private float nextTrace;
    private float nextPresentationTick;

    // The native GetRecipesToCraft() API allocates a new array every call. This controller already
    // owns the same stable 50 RecipeStack-derived children, so all per-frame presentation/count
    // work uses the cached entry array instead of allocating queue snapshots repeatedly.
    public int RuntimeCapacity => Math.Min(MaximumQueueEntries, entries != null ? entries.Length : 0);
    public int ActiveCount
    {
        get
        {
            int capacity = RuntimeCapacity;
            int count = 0;
            for (int i = 0; i < capacity; i++)
                if (entries[i] != null && entries[i].HasRecipeForPresentation)
                    count++;
            return count;
        }
    }

    public override void Init()
    {
        // IMPORTANT: the 50 RecipeStack-derived entries remain under the lightweight rect host.
        // Do not insert an XUi panel/controller between this native queue and those entries.
        base.Init();

        entries = GetChildrenByType<XUiC_RebirthCraftingQueueEntry>() ?? Array.Empty<XUiC_RebirthCraftingQueueEntry>();
        // Native XUi skips each hidden ancestor independently. Keep only the paid
        // personal queue path ticking; native RecipeStack remains its sole executor.
        if (windowGroup?.Controller is XUiC_RebirthPersonalCrafting)
        {
            for (XUiController node = this; node != null; node = node.Parent) node.AlwaysUpdate = true;
            foreach (var entry in entries)
                for (XUiController node = entry; node != null && !ReferenceEquals(node, this); node = node.Parent)
                    node.AlwaysUpdate = true;
        }
        rowsHost = GetChildById("rebirthCraftingQueueRowsHost");
        scrollTrack = GetChildById("rebirthCraftingQueueScrollTrack");
        scrollThumb = GetChildById("rebirthCraftingQueueScrollThumb");
        capacityLabel = GetChildById("rebirthCraftingQueueCapacity");

        WireScrollRecursive(this);
        if (scrollThumb != null)
        {
            if (scrollThumb.ViewComponent != null)
                scrollThumb.ViewComponent.EventOnDrag = true;
            scrollThumb.OnDrag += Thumb_OnDrag;
        }
        if (scrollTrack != null)
            scrollTrack.OnPress += Track_OnPress;

        ApplyPresentation(true);
        ApplyScrollPosition();
        TraceQueueState("init", true);
    }

    public override void OnOpen()
    {
        scrollPixels = 0f;
        scrollTargetPixels = 0f;
        draggingThumb = false;
        nextTrace = 0f;
        nextPresentationTick = 0f;
        base.OnOpen();
        ApplyPresentation(true);
        ApplyScrollPosition();
        TraceQueueState("open", true);
    }

    public override void Update(float dt)
    {
        // Native queue Update remains the sole queue shifter / active-slot starter.
        base.Update(dt);

        if (windowGroup?.isShowing != true) return;

        PollMouseWheelFallback();
        float beforeScroll=scrollPixels;
        AnimateScroll(dt);
        bool scrolling=draggingThumb||Math.Abs(beforeScroll-scrollPixels)>0.01f||Math.Abs(scrollPixels-scrollTargetPixels)>0.05f;
        if(scrolling)
        {
            MaintainEntryVisibilityAndPosition();
            UpdateScrollbar();
        }

        if(Time.realtimeSinceStartup < nextPresentationTick)return;
        nextPresentationTick=Time.realtimeSinceStartup+0.10f;
        ApplyPresentation(false);
        UpdateCapacityLabelDirect();
        if(windowGroup?.Controller is XUiC_RebirthCookingStation)SetVisible(capacityLabel,false);
        if(!scrolling)
        {
            MaintainEntryVisibilityAndPosition();
            UpdateScrollbar();
        }
        TraceQueueState("update", false);
    }

    public override bool GetBindingValueInternal(ref string value, string bindingName)
    {
        switch (bindingName)
        {
            case "rebirthqueueactive":
                value = ActiveCount.ToString();
                return true;
            case "rebirthqueuecapacity":
                value = RuntimeCapacity.ToString();
                return true;
            case "rebirthqueueactivecapacity":
                string format = RebirthCraftingQueueBridge.Localize("xuiRebirthQueueActiveCount", "{0} / {1}");
                value = string.Format(format, ActiveCount, RuntimeCapacity);
                return true;
            default:
                return base.GetBindingValueInternal(ref value, bindingName);
        }
    }

    public void ScrollByDelta(float delta)
    {
        ScrollByDeltaInternal(delta);
    }

    /// <summary>
    /// Called after a synchronous native queue mutation such as ForceCancel/RefreshQueue.
    /// This performs one change-aware presentation pass immediately: existing card geometry stays
    /// untouched, shifted backing recipes update only changed text/icon values, and only the new
    /// trailing empty card is hidden.
    /// </summary>
    public void SyncVisiblePresentationAfterNativeMutation()
    {
        ApplyPresentation(false);
        UpdateCapacityLabelDirect();
        if(windowGroup?.Controller is XUiC_RebirthCookingStation)SetVisible(capacityLabel,false);
        MaintainEntryVisibilityAndPosition();
        UpdateScrollbar();
    }

    private void WireScrollRecursive(XUiController controller)
    {
        if (controller == null)
            return;
        if (!scrollWired.Contains(controller))
        {
            scrollWired.Add(controller);
            if (controller.ViewComponent != null)
                controller.ViewComponent.EventOnScroll = true;
            controller.OnScroll += HandleScroll;
        }

        if (controller.Children == null)
            return;
        for (int i = 0; i < controller.Children.Count; i++)
            WireScrollRecursive(controller.Children[i]);
    }

    private void HandleScroll(XUiController sender, float delta)
    {
        lastEventScrollFrame = Time.frameCount;
        ScrollByDeltaInternal(delta);
    }

    private void ScrollByDeltaInternal(float delta)
    {
        if (MaxPixelOffset <= 0.5f || Math.Abs(delta) < 0.001f)
            return;

        float before = scrollTargetPixels;
        scrollTargetPixels = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(scrollTargetPixels, MaxPixelOffset, RebirthScrollbarPagingPolicy.PageStep(viewportHeight + CardGap, CardHeight + CardGap), delta < 0f ? 1 : -1) : Mathf.Clamp(scrollTargetPixels + (delta < 0f ? WheelPixels : -WheelPixels), 0f, MaxPixelOffset);
        if (RebirthScrollbarPagingPolicy.Enabled) scrollPixels = scrollTargetPixels;
        if (Math.Abs(scrollTargetPixels - before) < 0.01f)
            return;

        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting QueueScroll] wheel before=" + before.ToString("0.0")
                + " target=" + scrollTargetPixels.ToString("0.0")
                + " current=" + scrollPixels.ToString("0.0")
                + " max=" + MaxPixelOffset.ToString("0.0"));
    }

    private void PollMouseWheelFallback()
    {
        if (lastEventScrollFrame == Time.frameCount || ViewComponent == null || !ViewComponent.IsVisible)
            return;

        float delta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(delta) < 0.0001f || !IsMouseOverQueueArea())
            return;

        ScrollByDeltaInternal(delta);
        lastEventScrollFrame = Time.frameCount;
    }

    private bool IsMouseOverQueueArea()
    {
        GameObject hovered = UICamera.hoveredObject;
        Transform candidate = hovered != null ? hovered.transform : null;
        if (IsDescendant(candidate, rowsHost) || IsDescendant(candidate, scrollTrack) || IsDescendant(candidate, scrollThumb))
            return true;

        for (int i = 0; i < entries.Length; i++)
        {
            XUiC_RebirthCraftingQueueEntry entry = entries[i];
            if (entry != null && entry.ViewComponent != null && entry.ViewComponent.IsVisible && IsDescendant(candidate, entry))
                return true;
        }
        return false;
    }

    private static bool IsDescendant(Transform candidate, XUiController controller)
    {
        Transform root = controller?.ViewComponent?.UiTransform;
        if (candidate == null || root == null)
            return false;
        return candidate == root || candidate.IsChildOf(root);
    }

    private int TotalRows
    {
        get { return Math.Max(0, (ActiveCount + Columns - 1) / Columns); }
    }

    private int ContentHeight
    {
        get
        {
            int rows = TotalRows;
            return rows <= 0 ? 0 : rows * CardHeight + Math.Max(0, rows - 1) * CardGap;
        }
    }

    private float MaxPixelOffset
    {
        get { return Math.Max(0f, ContentHeight - viewportHeight); }
    }

    private void AnimateScroll(float dt)
    {
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingNow && (RebirthConsoleInputGuardRuntime.BlocksGameplayInput() || xui?.DragAndDropWindow?.IsEmpty() == false)) return;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; draggingThumb = false; }
        float max = MaxPixelOffset;
        scrollTargetPixels = Mathf.Clamp(scrollTargetPixels, 0f, max);
        scrollPixels = Mathf.Clamp(scrollPixels, 0f, max);
        if (RebirthScrollbarPagingPolicy.Enabled) scrollTargetPixels = scrollPixels = RebirthScrollbarPagingPolicy.SnapAbsolute(scrollTargetPixels, max, RebirthScrollbarPagingPolicy.PageStep(viewportHeight + CardGap, CardHeight + CardGap), true);
        if (Math.Abs(scrollPixels - scrollTargetPixels) < 0.05f)
        {
            if (Math.Abs(scrollPixels - scrollTargetPixels) > 0f)
                scrollPixels = scrollTargetPixels;
            return;
        }

        float t = Mathf.Clamp01(Math.Max(0f, dt) * ScrollLerpSpeed);
        scrollPixels = Mathf.Lerp(scrollPixels, scrollTargetPixels, t);
        if (Math.Abs(scrollPixels - scrollTargetPixels) < 0.15f)
            scrollPixels = scrollTargetPixels;

        // Do not repaint here. Update() performs one geometry/visibility/scrollbar pass after
        // interpolation. PC110 could perform that work twice in the same frame while scrolling,
        // which amplified visible churn on the queue cards.
    }

    private void ApplyScrollPosition()
    {
        MaintainEntryVisibilityAndPosition();
        UpdateScrollbar();
    }

    private void MaintainEntryVisibilityAndPosition()
    {
        int denseDisplayOrder = 0;
        int stride = CardHeight + CardGap;
        int capacity = RuntimeCapacity;

        for (int nativeIndex = capacity - 1; nativeIndex >= 0; nativeIndex--)
        {
            XUiC_RebirthCraftingQueueEntry entry = entries[nativeIndex];
            if (entry == null || entry.ViewComponent == null)
                continue;

            if (!entry.HasRecipeForPresentation || denseDisplayOrder >= MaximumQueueEntries)
            {
                entry.SetCancelHitEnabled(false);
                SetVisible(entry, false);
                continue;
            }

            int displayOrder = denseDisplayOrder++;
            entry.DisplayIndex = displayOrder;

            int column = displayOrder % Columns;
            int row = displayOrder / Columns;
            float top = row * stride - scrollPixels;
            float bottom = top + CardHeight;

            // Match the backpack: keep every card that intersects the viewport alive, including a
            // partially visible edge card, but stop native RecipeStack presentation work for rows
            // that are completely off-screen. The runtime UIPanel still performs the pixel-perfect
            // clipping at the top/bottom edge.
            bool intersectsViewport = bottom > 0.01f && top < viewportHeight - 0.01f;
            bool wasVisible = entry.ViewComponent.IsVisible;

            // Stage geometry first. A newly-added card must never become renderable with its old
            // template position/size for even one frame.
            Vector2i desiredPosition = new Vector2i(column * (cardWidth + CardGap), -Mathf.RoundToInt(top));
            bool positionChanged = entry.ViewComponent.Position.x != desiredPosition.x || entry.ViewComponent.Position.y != desiredPosition.y;
            if (positionChanged)
                entry.ViewComponent.Position = desiredPosition;
            if (entry.ViewComponent.Size.x != cardWidth || entry.ViewComponent.Size.y != CardHeight)
                entry.ViewComponent.Size = new Vector2i(cardWidth, CardHeight);

            // A clipped/invisible cancellation hit target must not steal mouse input from the queue
            // header or neighboring UI. The stock X icon button is clipped by the UIPanel.
            const float cancelTop = 36f;
            const float cancelBottom = 54f;
            entry.SetCancelHitEnabled(intersectsViewport &&
                top + cancelTop >= 0f && top + cancelBottom <= viewportHeight);

            // Settle all visible field changes before exposing a new card. Existing cards receive only
            // a cheap diff sync after native CopyTo/RefreshQueue, so cancellation never blanks and
            // reconstructs the complete queue presentation.
            if (intersectsViewport && !wasVisible)
            {
                entry.RefreshPresentationNow();

                // XUiView.Position is deferred: its setter only marks positionDirty, while
                // IsVisible immediately activates the GameObject. Without flushing the staged
                // position first, a newly occupied hidden queue slot can render for one frame at
                // its template origin (top-left) before XUiView.UpdateData moves it. Force only
                // the root transform into the staged position before revealing the card.
                // TryUpdatePosition is deliberately narrow: it does not redraw/rebind the card.
                entry.ViewComponent.TryUpdatePosition();

                if (RebirthLogSettings.CraftingUiLoggingEnabled)
                {
                    Vector3 actual = entry.ViewComponent.UiTransform != null
                        ? entry.ViewComponent.UiTransform.localPosition
                        : Vector3.zero;
                    Log.Out("[REBIRTH Crafting QueuePlacement] reveal nativeIndex=" + nativeIndex
                        + " display=" + displayOrder
                        + " desired=" + desiredPosition.x + "," + desiredPosition.y
                        + " actual=" + actual.x.ToString("0") + "," + actual.y.ToString("0")
                        + " positionDirtyFlush=True");
                }
            }
            else if (intersectsViewport)
                entry.SyncPresentationAfterNativeQueuePass();

            SetVisible(entry, intersectsViewport);
        }

        for (int i = 0; i < entries.Length; i++)
        {
            XUiC_RebirthCraftingQueueEntry entry = entries[i];
            if (entry == null || entry.ViewComponent == null || entry.HasRecipeForPresentation)
                continue;
            entry.SetCancelHitEnabled(false);
            SetVisible(entry, false);
        }
    }

    private static void SetVisible(XUiController controller, bool visible)
    {
        if (controller?.ViewComponent == null || controller.ViewComponent.IsVisible == visible)
            return;
        controller.ViewComponent.IsVisible = visible;
    }

    private void ApplyPresentation(bool force)
    {
        if (ViewComponent == null)
            return;

        int capacity = RuntimeCapacity;
        int active = ActiveCount;
        Vector2i size = ViewComponent.Size;
        bool sizeChanged = size.x != lastSize.x || size.y != lastSize.y;
        bool summaryChanged = capacity != lastCapacity || active != lastActiveCount;
        if (!force && !sizeChanged && !summaryChanged)
            return;

        bool geometryChanged = force || sizeChanged;
        lastSize = size;
        lastCapacity = capacity;
        lastActiveCount = active;

        int width = Math.Max(260, size.x);
        int height = Math.Max(HeaderHeight + 6 + CardHeight + BottomPad, size.y);
        int rowsTop = HeaderHeight + 6;

        if (geometryChanged)
        {
            viewportHeight = Math.Max(CardHeight, height - rowsTop - (Columns == 1 ? 10 : BottomPad));

            // Personal crafting reserves a scrollbar gutter. Cooking uses the full single-column
            // width with equal 10px side insets; its scrollbar overlays the right edge.
            int hostWidth = Math.Max(260, width - 20 - (Columns == 1 ? 0 : ScrollbarWidth + 8));
            cardWidth = Math.Max(120, (hostWidth - (Columns - 1) * CardGap) / Columns);

            SetRectIfChanged(capacityLabel, Math.Max(110, width - 150), -9, 136, 26);
            SetRectIfChanged(rowsHost, 10, -rowsTop, hostWidth, viewportHeight);
            EnsureRowsClipPanel(hostWidth, viewportHeight);

            int scrollX = Math.Max(0, width - ScrollbarWidth - 6);
            SetRectIfChanged(scrollTrack, scrollX, -rowsTop, ScrollbarTrackWidth, viewportHeight);
        }

        scrollPixels = Mathf.Clamp(scrollPixels, 0f, MaxPixelOffset);
        scrollTargetPixels = Mathf.Clamp(scrollTargetPixels, 0f, MaxPixelOffset);

        bool needsScroll = MaxPixelOffset > 0.5f;
        SetVisible(scrollTrack, needsScroll);
        SetVisible(scrollThumb, needsScroll);
        if (!needsScroll)
        {
            scrollPixels = 0f;
            scrollTargetPixels = 0f;
        }

        XUiC_RebirthPersonalCrafting owner = GetParentByType<XUiC_RebirthPersonalCrafting>();
        owner?.Coordinator?.RecordQueue(active, capacity);
        UpdateCapacityLabelDirect();
        if(windowGroup?.Controller is XUiC_RebirthCookingStation)SetVisible(capacityLabel,false);

        // Do not reposition/repaint cards here. Update() performs exactly one stable card-layout
        // pass after native queue authority has finished shifting/copying its backing RecipeStacks.
        TraceQueueState(summaryChanged ? "summary" : (sizeChanged ? "resize" : "force"), true);
    }

    private void EnsureRowsClipPanel(int hostWidth, int hostHeight)
    {
        GameObject hostObject = rowsHost?.ViewComponent?.UiTransform?.gameObject;
        if (hostObject == null)
            return;

        if (rowsClipPanel == null)
        {
            rowsClipPanel = hostObject.GetComponent<UIPanel>();
            if (rowsClipPanel == null)
                rowsClipPanel = hostObject.AddComponent<UIPanel>();

            // Use the same real NGUI SoftClip configuration as Rebirth's scroll-view/backpack
            // implementation, but attach it to the existing rows-host GameObject at runtime so the
            // XUi controller hierarchy stays XUiC_CraftingQueue -> XUiC_RecipeStack.
            rowsClipPanel.depth = 20;
            rowsClipPanel.softBorderPadding = true;
            rowsClipPanel.clipping = UIDrawCall.Clipping.SoftClip;
            rowsClipPanel.clipSoftness = Vector2.zero;
            rowsClipWidgetsRegistered = false;
        }

        Vector4 region = new Vector4(hostWidth / 2f, -hostHeight / 2f, hostWidth, hostHeight);
        if (rowsClipPanel.baseClipRegion != region)
            rowsClipPanel.baseClipRegion = region;
        if (rowsClipPanel.clipping != UIDrawCall.Clipping.SoftClip)
            rowsClipPanel.clipping = UIDrawCall.Clipping.SoftClip;
        if (rowsClipPanel.clipSoftness != Vector2.zero)
            rowsClipPanel.clipSoftness = Vector2.zero;

        if (!rowsClipWidgetsRegistered)
        {
            // The card widgets initialize before this runtime panel exists. Cycling enabled once
            // makes NGUI register each descendant widget with its new nearest parent panel.
            // This happens once during queue initialization, never while scrolling.
            UIWidget[] widgets = hostObject.GetComponentsInChildren<UIWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                UIWidget widget = widgets[i];
                if (widget == null || !widget.enabled)
                    continue;
                widget.enabled = false;
                widget.enabled = true;
            }
            rowsClipWidgetsRegistered = true;
        }
    }

    private void UpdateCapacityLabelDirect()
    {
        XUiV_Label label = capacityLabel?.ViewComponent as XUiV_Label;
        if (label == null)
            return;

        string text = ActiveCount + " / " + RuntimeCapacity;
        if (!string.Equals(label.Text, text, StringComparison.Ordinal))
            label.Text = text;
    }

    private void UpdateScrollbar()
    {
        if (scrollTrack == null || scrollThumb == null || scrollTrack.ViewComponent == null || scrollThumb.ViewComponent == null)
            return;

        bool needed = MaxPixelOffset > 0.5f;
        SetVisible(scrollTrack, needed);
        SetVisible(scrollThumb, needed);
        if (!needed)
            return;

        // Geometry is stable; only the thumb y/height changes while scrolling.
        int trackX = scrollTrack.ViewComponent.Position.x;
        int trackY = scrollTrack.ViewComponent.Position.y;
        SetRectIfChanged(scrollTrack, trackX, trackY, ScrollbarTrackWidth, viewportHeight);

        int trackHeight = viewportHeight;
        int thumbHeight = ThumbHeight(trackHeight);
        int travel = Math.Max(0, trackHeight - thumbHeight);
        int thumbOffset = MaxPixelOffset > 0f ? Mathf.RoundToInt(travel * (scrollPixels / MaxPixelOffset)) : 0;
        Vector2i thumbPos = new Vector2i(trackX + (ScrollbarTrackWidth - ScrollbarThumbWidth) / 2, trackY - thumbOffset);
        Vector2i thumbSize = new Vector2i(ScrollbarThumbWidth, thumbHeight);
        if (scrollThumb.ViewComponent.Position.x != thumbPos.x || scrollThumb.ViewComponent.Position.y != thumbPos.y)
            scrollThumb.ViewComponent.Position = thumbPos;
        if (scrollThumb.ViewComponent.Size.x != thumbSize.x || scrollThumb.ViewComponent.Size.y != thumbSize.y)
            scrollThumb.ViewComponent.Size = thumbSize;
    }

    private int ThumbHeight(int trackHeight)
    {
        if (ContentHeight <= 0 || trackHeight <= 0)
            return trackHeight;
        float ratio = Mathf.Clamp01(viewportHeight / (float)Math.Max(viewportHeight, ContentHeight));
        return Mathf.Clamp(Mathf.RoundToInt(trackHeight * ratio), MinThumbHeight, trackHeight);
    }

    private void Thumb_OnDrag(XUiController sender, EDragType dragType, Vector2 delta)
    {
        if (MaxPixelOffset <= 0f)
            return;

        if (dragType == EDragType.DragStart)
        {
            draggingThumb = true;
            dragAccumulated = 0f;
            dragStartOffset = scrollPixels;
        }
        if (!draggingThumb)
            return;

        if (dragType != EDragType.DragEnd)
            dragAccumulated += -delta.y;

        int travel = Math.Max(1, viewportHeight - ThumbHeight(viewportHeight));
        float requested = dragStartOffset + dragAccumulated * (MaxPixelOffset / travel);
        scrollPixels = RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(requested, 0f, MaxPixelOffset), MaxPixelOffset, RebirthScrollbarPagingPolicy.PageStep(viewportHeight + CardGap, CardHeight + CardGap), RebirthScrollbarPagingPolicy.Enabled);
        scrollTargetPixels = scrollPixels;
        ApplyScrollPosition();

        if (dragType == EDragType.DragEnd)
            draggingThumb = false;
    }

    private void Track_OnPress(XUiController sender, int mouseButton)
    {
        if ((mouseButton != 0 && mouseButton != -1) || MaxPixelOffset <= 0f || scrollTrack == null || scrollTrack.ViewComponent == null)
            return;

        Collider collider = scrollTrack.ViewComponent.UiTransform != null ? scrollTrack.ViewComponent.UiTransform.GetComponent<Collider>() : null;
        Camera camera = UICamera.currentCamera;
        if (collider == null || camera == null)
        {
            scrollTargetPixels = RebirthScrollbarPagingPolicy.Enabled ? RebirthScrollbarPagingPolicy.Step(scrollTargetPixels, MaxPixelOffset, RebirthScrollbarPagingPolicy.PageStep(viewportHeight + CardGap, CardHeight + CardGap), 1) : Mathf.Clamp(scrollTargetPixels + CardHeight, 0f, MaxPixelOffset);
            if (RebirthScrollbarPagingPolicy.Enabled) scrollPixels = scrollTargetPixels;
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
        scrollTargetPixels = RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(normalized * MaxPixelOffset, 0f, MaxPixelOffset), MaxPixelOffset, RebirthScrollbarPagingPolicy.PageStep(viewportHeight + CardGap, CardHeight + CardGap), RebirthScrollbarPagingPolicy.Enabled);
        if (RebirthScrollbarPagingPolicy.Enabled) scrollPixels = scrollTargetPixels;
    }

    private void TraceQueueState(string reason, bool force)
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled)
            return;
        if (!force && Time.realtimeSinceStartup < nextTrace)
            return;
        nextTrace = Time.realtimeSinceStartup + 1f;

        int withRecipe = 0;
        int visible = 0;
        int nativeTicks = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            XUiC_RebirthCraftingQueueEntry entry = entries[i];
            if (entry == null)
                continue;
            if (entry.HasRecipeForPresentation)
                withRecipe++;
            if (entry.HasRecipeForPresentation && entry.IsCrafting)
                nativeTicks++;
            if (entry.ViewComponent != null && entry.ViewComponent.IsVisible)
                visible++;
        }

        Log.Out("[REBIRTH Crafting QueueTrace] reason=" + (reason ?? "unknown")
            + " active=" + ActiveCount + "/" + RuntimeCapacity
            + " discovered=" + entries.Length
            + " runtime=" + RuntimeCapacity
            + " withRecipe=" + withRecipe
            + " nativeTicks=" + nativeTicks
            + " visible=" + visible
            + " rows=" + TotalRows
            + " viewport=" + viewportHeight
            + " content=" + ContentHeight
            + " offset=" + scrollPixels.ToString("0.0")
            + " target=" + scrollTargetPixels.ToString("0.0")
            + " max=" + MaxPixelOffset.ToString("0.0")
            + " host=" + (rowsHost != null ? rowsHost.GetType().Name : "<null>")
            + " clip=" + (rowsClipPanel != null
                ? rowsClipPanel.clipping + ":" + rowsClipPanel.baseClipRegion.z.ToString("0") + "x" + rowsClipPanel.baseClipRegion.w.ToString("0")
                : "<null>")
            + " track=" + (scrollTrack?.ViewComponent != null ? scrollTrack.ViewComponent.Size.x + "x" + scrollTrack.ViewComponent.Size.y : "<null>")
            + " thumbW=" + (scrollThumb?.ViewComponent != null ? scrollThumb.ViewComponent.Size.x.ToString() : "<null>")
            + " label=\"" + ((capacityLabel?.ViewComponent as XUiV_Label)?.Text ?? "<null>") + "\"");
    }

    private static void SetRectIfChanged(XUiController controller, int x, int y, int width, int height)
    {
        if (controller?.ViewComponent == null)
            return;
        Vector2i position = new Vector2i(x, y);
        Vector2i size = new Vector2i(Math.Max(1, width), Math.Max(1, height));
        if (controller.ViewComponent.Position.x != position.x || controller.ViewComponent.Position.y != position.y)
            controller.ViewComponent.Position = position;
        if (controller.ViewComponent.Size.x != size.x || controller.ViewComponent.Size.y != size.y)
            controller.ViewComponent.Size = size;
    }
}
