using System;
using System.Collections.Generic;
#if REBIRTH_UI_DIAGNOSTICS
using System.Globalization;
using System.Text;
#endif
using UnityEngine;
using UnityEngine.Scripting;
#if REBIRTH_UI_DIAGNOSTICS
using Stopwatch = System.Diagnostics.Stopwatch;
#endif

#nullable disable

// Fix31: normal builds omit the UI capture code AND its hot-path call sites.
// REBIRTH_UI_DIAGNOSTICS is an explicit project-wide opt-in, not the normal DEBUG symbol.

[Preserve]
public sealed class XUiC_RebirthCreativeWorkspace : XUiController
{
    public static XUiC_RebirthCreativeWorkspace ActiveInstance;
    public bool IsWorkspaceOpen { get; private set; }
    public XUiC_RebirthCharacterBackpack BackpackView { get; private set; }
    public XUiC_RebirthCreativeCatalogueGrid Catalogue { get; private set; }
    private RebirthScreenLayout layout;
    private XUiController header;
    private bool headerVisible;
    internal bool PresentationInteractionBusy { get; private set; }

    public override void Init()
    {
        base.Init();
        BackpackView = GetChildByType<XUiC_RebirthCharacterBackpack>();
        Catalogue = GetChildByType<XUiC_RebirthCreativeCatalogueGrid>();
    }
    public override void OnOpen()
    {
        // Resolve again after XUi has finished constructing all descendants. These are cached
        // references, not per-frame hierarchy searches. Slot links are also owned by grid.Init.
        BackpackView = BackpackView ?? GetChildByType<XUiC_RebirthCharacterBackpack>();
        Catalogue = Catalogue ?? GetChildByType<XUiC_RebirthCreativeCatalogueGrid>();
        base.OnOpen(); ActiveInstance = this; IsWorkspaceOpen = true;
        layout = layout ?? new RebirthScreenLayout(this); layout.Open();
        header = xui.FindWindowGroupByName("windowpaging")?.GetChildById("windowPagingHeader");
        if (header?.ViewComponent != null) { headerVisible = header.ViewComponent.IsVisible; header.ViewComponent.IsVisible = false; }
    }
    public override void Update(float dt)
    {
        // Sample global cursor/press state once for the off-screen presentation guards.
        // No repeated button polling in every hidden backpack cell.
        var cursor = xui?.playerUI?.CursorController;
        PresentationInteractionBusy = xui?.DragAndDropWindow == null || !xui.DragAndDropWindow.IsEmpty() ||
            (cursor != null && (cursor.GetMouseButton(UICamera.MouseButton.LeftButton) ||
                cursor.GetMouseButton(UICamera.MouseButton.RightButton)));
#if REBIRTH_UI_DIAGNOSTICS
        bool diagnosticFreeze = RebirthCreativeIsolation.BeforeUpdate(this);
        long isolationStarted = RebirthCreativeIsolation.BeginWorkspace(this);
        RebirthCreativePerformance.BeginFrame(IsWorkspaceOpen);
        long started = RebirthCreativePerformance.Begin();
        try
        {
            if (!diagnosticFreeze) base.Update(dt);
            else ViewComponent?.Update(dt); // Keep root geometry; no child-controller traversal in frozen phases.
            if (IsWorkspaceOpen) layout?.Apply();
            if (IsWorkspaceOpen && header?.ViewComponent != null) header.ViewComponent.IsVisible = false;
        }
        catch (Exception)
        {
            if (RebirthCreativeIsolation.Active) RebirthCreativeIsolation.Cancel("exception in workspace update; restored before propagating");
            throw;
        }
        finally
        {
            RebirthCreativePerformance.EndWorkspace(started);
            RebirthCreativeIsolation.AfterUpdate(this, isolationStarted);
        }
#else
        base.Update(dt);
        if (IsWorkspaceOpen) layout?.Apply();
        if (IsWorkspaceOpen && header?.ViewComponent != null) header.ViewComponent.IsVisible = false;
#endif
    }
    public override void OnClose()
    {
#if REBIRTH_UI_DIAGNOSTICS
        RebirthCreativeIsolation.WorkspaceClosing(this);
#endif
        UIInput focused=UIInput.selection;
        if(focused!=null&&ViewComponent?.UiTransform!=null&&focused.transform.IsChildOf(ViewComponent.UiTransform))
        {focused.RemoveFocus();focused.isSelected=false;}
        var cursor=xui?.playerUI?.CursorController;
        if(cursor?.HoverTarget?.Controller?.IsSelfOrChildOf(this)==true)
        {cursor.HoverTarget=null;cursor.SetNavigationTarget((XUiView)null);cursor.SetNavigationLockView((XUiView)null);cursor.Locked=false;cursor.ResetNavigationTarget();}
        IsWorkspaceOpen = false; layout?.Close();
        if (header?.ViewComponent != null) header.ViewComponent.IsVisible = headerVisible;
        if (ActiveInstance == this) ActiveInstance = null;
        base.OnClose();
    }
}

/// <summary>Coalesce rapid search edits; all native filter generation, categories and commands remain native.</summary>
[Preserve]
public sealed class XUiC_RebirthCreativeCatalogueWindow : XUiC_Creative2Window
{
    private XUiC_TextInput search;
    private string observedSearch = string.Empty;
    private float searchReadyAt;
    private bool catalogueOpen;
    public override void Init()
    {
        base.Init();
        search = WindowGroup.Controller.GetChildById("searchInput") as XUiC_TextInput;
    }
    public override void OnOpen()
    {
        catalogueOpen = true;
        observedSearch = search?.Text ?? string.Empty; searchReadyAt = 0f;
        base.OnOpen(); // Includes the real native opening Refresh, with the full native filter semantics.
    }
    public override void OnClose() { catalogueOpen = false; searchReadyAt = 0f; base.OnClose(); }
    public override void Update(float dt)
    {
        if (!catalogueOpen) { base.Update(dt); return; }
        string text = search?.Text ?? string.Empty;
        if (!string.Equals(text, observedSearch, StringComparison.Ordinal))
        {
            observedSearch = text;
            searchReadyAt = Time.realtimeSinceStartup + .12f;
        }
        // RefreshList is the native b10 dirty flag, set by its existing search/category callbacks.
        // Restore it after a deferred update; never drop a pending request or replace native Refresh.
        bool defer = RefreshList && Time.realtimeSinceStartup < searchReadyAt;
        bool refreshDue = RefreshList && !defer;
        if (defer) RefreshList = false;
#if REBIRTH_UI_DIAGNOSTICS
        long started = RebirthCreativePerformance.Begin();
#endif
        try { base.Update(dt); }
        finally
        {
            if (defer) RefreshList = true;
            if (refreshDue)
            {
                searchReadyAt = 0f;
#if REBIRTH_UI_DIAGNOSTICS
                RebirthCreativePerformance.CountCatalogueRefresh();
#endif
            }
#if REBIRTH_UI_DIAGNOSTICS
            RebirthCreativePerformance.EndCatalogue(started);
#endif
        }
    }
}

[Preserve]
public sealed class XUiC_RebirthCreativeBackpackWindow : XUiC_BackpackWindow
{
    public override void OnOpen()
    {
        if (!string.IsNullOrEmpty(defaultSelectedElement) && GetChildById(defaultSelectedElement) == null)
            defaultSelectedElement = "";
        base.OnOpen();
    }
#if REBIRTH_UI_DIAGNOSTICS
    public override void Update(float dt)
    {
        long started = RebirthCreativePerformance.Begin();
        try { base.Update(dt); }
        finally { RebirthCreativePerformance.EndBackpack(started); }
    }
#endif
}

/// <summary>
/// Native Creative2Stack remains the item/input implementation. The read-only catalogue owns
/// explicit presentation invalidations; a generic native dirty hint is coalesced into a bounded
/// safety refresh rather than making a static cell run the full native input loop indefinitely.
/// Real input, locks, active editors and explicit row/filter changes still run native immediately.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthCreativeStatsStack : XUiC_Creative2Stack
{
    internal XUiC_RebirthCreativeCatalogueGrid Catalogue;
    internal int PoolIndex;
    internal int CatalogueIndex { get; private set; }
    private RebirthSlotPalette palette;
    private XUiV_Sprite border;
    private bool hovered, presented = true;
    private const float NativeRefreshSeconds = .5f;
    private int childFlushPasses;
    private uint presentationVersion = 1, publishedVersion;
    private float wakeUntil, nextNativeRefresh;
    private ItemValue sampledItem;
    private int sampledCount, sampledMeta, sampledQuality;
    private float sampledUseTimes;
    private bool sampledFavorite, sampledLock;
    private RebirthSlotPresentationGate presentation;

    public override void Init()
    {
        base.Init(); palette = new RebirthSlotPalette(this);
        border = GetChildById("background")?.ViewComponent as XUiV_Sprite;
        // Match the working Crafting slots instead of depending on implicit template input flags.
        ViewComponent.EventOnPress = true; ViewComponent.EventOnHover = true;
        ViewComponent.EventOnDrag = true; ViewComponent.EventOnScroll = true;
    }
    public override void OnOpen()
    {
        // Reopening is an explicit invalidation, not a reliance on a native dirty bit
        // eventually staying false for several consecutive frames.
        Catalogue = Catalogue ?? GetParentByType<XUiC_RebirthCreativeCatalogueGrid>();
        presentation?.Show();
        presented = true; wakeUntil = 0f; childFlushPasses = 0;
        InvalidatePresentation();
        nextNativeRefresh = Time.realtimeSinceStartup + (PoolIndex % 67) * (NativeRefreshSeconds / 67f);
        base.OnOpen();
    }
    public override void OnClose()
    {
        presentation?.Show();
        RebirthCharacterItemStatsTooltip.Hover(this, false);
        hovered = false; childFlushPasses = 0;
        base.OnHovered(false); base.OnClose();
    }
    public override void OnHovered(bool over)
    {
        RebirthCharacterItemStatsTooltip.Hover(this, over);
        // Repeated same-state notifications must not keep restarting the wake/settle timer.
        // A genuine enter/exit still receives the native hover and icon-grow/shrink handling.
        if (hovered == over) return;
        hovered = over;
        wakeUntil = Time.realtimeSinceStartup + .55f;
        base.OnHovered(over);
        if (!over && !IsSelected && !IsHolding && !IsDragAndDrop) CommitColor(backgroundColor);
    }
    internal void BindCatalogueItem(ItemStack stack, int index, bool show)
    {
        var workspace = XUiC_RebirthCreativeWorkspace.ActiveInstance;
        if (workspace?.xui == xui && workspace.BackpackView?.SelectedItem == this)
            workspace.BackpackView.SelectItem(null);
        // Clear only the recycled source, not a selection in the real backpack. Never let
        // an old action list silently acquire the next item assigned to the same pooled cell.
        Hovered(false);
        RebirthCharacterItemStatsTooltip.Hover(this, false);
        hovered = false;
        if (IsSelected) IsSelected = false;
        Selected(false);
        base.HandleClickComplete();
        presented = show;
        CatalogueIndex = index;
        // Native menu bookkeeping refers to the UI grid's physical controller index.
        // Keep that in range; the separate CatalogueIndex identifies the read-only source.
        SlotNumber = PoolIndex;
        if (!ReferenceEquals(ItemStack, stack) && (ItemStack == null || !ItemStack.Equals(stack)))
        {
            ItemStack = stack;
#if REBIRTH_UI_DIAGNOSTICS
            RebirthCreativePerformance.CountAssignment();
#endif
        }
        InvalidatePresentation();
        // Rebinding is not a hover animation. The dirty native pass resets the old icon tween;
        // only the child-view commit is needed after the native pass for the entering row.
        wakeUntil = 0f;
        CommitColor(backgroundColor);
    }
    internal void InvalidatePresentation()
    {
        IsDirty = true;
        unchecked { ++presentationVersion; }
        sampledItem = null;
    }
    public override bool GetBindingValueInternal(ref string value, string name)
    {
        if (name == "backgroundcolor") { value = AttributeLock ? RebirthSlotPalette.Locked : RebirthSlotPalette.Normal; return true; }
        if (name == "tooltip")
        { var text = RebirthCharacterItemStatsTooltip.Build(this, ItemStack); if (text != null) { value = text; return true; } }
        return base.GetBindingValueInternal(ref value, name);
    }
    public override void updateItemInfoWindow(XUiC_ItemStack stack)
    { XUiC_RebirthCreativeWorkspace.ActiveInstance?.BackpackView?.SelectItem(this); }

    private void CommitBorder() => CommitColor(SelectionBorderColor);
    private void CommitColor(Color color)
    {
        if (border == null) return;
        if (border.Color != color) border.Color = color;
        if (border.Sprite != null && border.Sprite.color != color) border.SetColorImmediately(color);
    }
    private void SamplePresentationChanges()
    {
        ItemValue item = ItemStack?.itemValue;
        int count = ItemStack?.count ?? 0;
        int meta = item != null ? item.Meta : 0, quality = item != null ? item.Quality : 0;
        float uses = item != null ? item.UseTimes : 0f;
        string favoriteValue = "false";
        base.GetBindingValueInternal(ref favoriteValue, "isfavorite");
        bool favorite = string.Equals(favoriteValue, "true", StringComparison.OrdinalIgnoreCase), locked = AttributeLock;
        if (!ReferenceEquals(sampledItem, item) || sampledCount != count || sampledMeta != meta ||
            sampledQuality != quality || sampledUseTimes != uses || sampledFavorite != favorite || sampledLock != locked)
        {
            sampledItem = item; sampledCount = count; sampledMeta = meta; sampledQuality = quality;
            sampledUseTimes = uses; sampledFavorite = favorite; sampledLock = locked;
            IsDirty = true;
        }
    }
    public override void Update(float dt)
    {
        if (!presented || ViewComponent?.UiTransform == null || !ViewComponent.UiTransform.gameObject.activeInHierarchy)
            return;
        var workspace = XUiC_RebirthCreativeWorkspace.ActiveInstance;
        bool pinned = IsSelected || IsHolding || IsLocked || IsDragAndDrop || hovered ||
            (Catalogue != null && Catalogue.FrameClock < wakeUntil) || workspace?.BackpackView?.SelectedItem == this || workspace?.PresentationInteractionBusy == true ||
            Catalogue?.FrameAssemblyActive == true;
        if (Catalogue != null && !Catalogue.IsInsideViewport(CatalogueIndex) && !pinned)
        {
            if (hovered) Hovered(false);
            presentation = presentation ?? new RebirthSlotPresentationGate(this);
            if (presentation.Hide())
            {
#if REBIRTH_UI_DIAGNOSTICS
                RebirthCreativePerformance.CountCulledSlot();
#endif
                return;
            }
        }
        bool wakingPresentation = presentation?.Hidden == true;
        if (wakingPresentation) InvalidatePresentation();
        try
        {
            // B10's native hit-test wakes the cell under a stationary pointer as content moves.
            bool pointerHere = ViewComponent.UiTransformIsHovered;
            if (pointerHere != hovered) OnHovered(pointerHere);
            float now = Catalogue != null ? Catalogue.FrameClock : Time.realtimeSinceStartup;
            bool due = now >= nextNativeRefresh;
            if (due)
            {
                SamplePresentationChanges();
                float phase = (PoolIndex % 67) * (NativeRefreshSeconds / 67f);
                nextNativeRefresh = (Mathf.Floor((now - phase) / NativeRefreshSeconds) + 1f) * NativeRefreshSeconds + phase;
                if (nextNativeRefresh <= now) nextNativeRefresh = now + NativeRefreshSeconds;
            }

            bool invalidated = presentationVersion != publishedVersion;
            bool cursorBusy = xui?.DragAndDropWindow == null || !xui.DragAndDropWindow.IsEmpty();
            RebirthCreativeSlotWakeReason reasons = RebirthCreativeSlotWakeReason.None;
            if (Catalogue == null) reasons |= RebirthCreativeSlotWakeReason.Unbound;
            if (invalidated) reasons |= RebirthCreativeSlotWakeReason.Invalidation;
            if (due) reasons |= RebirthCreativeSlotWakeReason.SafetyRefresh;
            if (hovered) reasons |= RebirthCreativeSlotWakeReason.Hover;
            if (IsSelected) reasons |= RebirthCreativeSlotWakeReason.Selection;
            if (IsHolding) reasons |= RebirthCreativeSlotWakeReason.Holding;
            if (IsLocked) reasons |= RebirthCreativeSlotWakeReason.Lock;
            if (IsDragAndDrop || cursorBusy) reasons |= RebirthCreativeSlotWakeReason.Drag;
            if (Catalogue != null && Catalogue.FrameAssemblyActive) reasons |= RebirthCreativeSlotWakeReason.Assembly;
            if (now < wakeUntil) reasons |= RebirthCreativeSlotWakeReason.HoverTween;

            // Dirty by itself is a hint from the inherited binding/presentation system. For this
            // read-only catalogue it is serviced by the next staggered safety refresh (<= 0.5s).
            // Never clear that hint on a skipped path. Row/filter changes explicitly increment the
            // presentation version above, and interaction does not wait for the safety interval.
            RebirthCreativeSlotUpdateMode mode = RebirthCreativeSlotUpdatePolicy.Choose(reasons, childFlushPasses);
#if REBIRTH_UI_DIAGNOSTICS
            bool dirtyAtEntry = IsDirty;
#endif
            if (mode == RebirthCreativeSlotUpdateMode.Native)
            {
                uint publishingVersion = presentationVersion;
#if REBIRTH_UI_DIAGNOSTICS
                RebirthCreativePerformance.CountWake(reasons, dirtyAtEntry);
                long started = RebirthCreativePerformance.Begin();
                try { base.Update(dt); }
                finally { RebirthCreativePerformance.EndNativeSlot(started); }
#else
                base.Update(dt);
#endif
                // Native callbacks can invalidate a source while processing an input. A version
                // acknowledgement covers only the version that this pass actually started with.
                publishedVersion = publishingVersion;
                childFlushPasses = 2;
            }
            else if (mode == RebirthCreativeSlotUpdateMode.ViewsOnly)
            {
#if REBIRTH_UI_DIAGNOSTICS
                long started = RebirthCreativePerformance.Begin();
                try
                {
                    // Native ItemStack refreshes bindings AFTER its child update. Finish those
                    // pending view writes without rerunning the complete ItemStack input body.
                    ViewComponent.Update(dt);
                    foreach (XUiController child in Children) child.Update(dt);
                }
                finally { RebirthCreativePerformance.EndVisualSlot(started, dirtyAtEntry); }
#else
                // Finish native late binding writes without rerunning item-slot input.
                ViewComponent.Update(dt);
                foreach (XUiController child in Children) child.Update(dt);
#endif
                --childFlushPasses;
            }
#if REBIRTH_UI_DIAGNOSTICS
            else RebirthCreativePerformance.CountIdleSlot(dirtyAtEntry);
#endif

            // Always repair the real border sprite, not only its cached C# property. This keeps
            // the empty-cell hover-trail correction on the full, view-flush and cached paths.
            palette?.Apply(AttributeLock);
            CommitBorder();
            if (wakingPresentation) RebirthSlotPresentationGate.FlushViews(this);
        }
        finally { if (wakingPresentation) presentation.Show(); }
        if (wakingPresentation) { palette?.Apply(AttributeLock); CommitBorder(); }
    }

}

// Kept separate from Unity so the exact scheduling decision is small and auditable.
[Flags]
internal enum RebirthCreativeSlotWakeReason
{
    None = 0, Unbound = 1, Invalidation = 2, SafetyRefresh = 4, Hover = 8,
    Selection = 16, Holding = 32, Lock = 64, Drag = 128, Assembly = 256, HoverTween = 512
}
internal enum RebirthCreativeSlotUpdateMode { Native, ViewsOnly, Idle }
internal static class RebirthCreativeSlotUpdatePolicy
{
    internal static RebirthCreativeSlotUpdateMode Choose(RebirthCreativeSlotWakeReason reasons, int childFlushPasses)
    {
        if (reasons != RebirthCreativeSlotWakeReason.None) return RebirthCreativeSlotUpdateMode.Native;
        return childFlushPasses > 0 ? RebirthCreativeSlotUpdateMode.ViewsOnly : RebirthCreativeSlotUpdateMode.Idle;
    }
}

#if REBIRTH_UI_DIAGNOSTICS
/// <summary>Explicit opt-in capture only. No timers/string formatting/logging in the disabled path.</summary>
internal static class RebirthCreativePerformance
{
    internal static bool Enabled { get; private set; }
    private static int frameId = -1, lastCountedFrame = -1;
    private static long workspaceCalls;
    private static long frames, workspaceTicks, catalogueTicks, gridTicks, backpackTicks, nativeSlotTicks;
    private static long fullSlots, idleSlots, visualSlots, visualSlotTicks, coalescedDirtySlots, nativeDirtySlots;
    private static long assignments, recycledRows, wheels, catalogueRefreshes, culledSlots, culledBackpack;
    private static long backpackNativeTicks, backpackViewTicks, backpackNativeCalls, backpackViewCalls;
    private static long backpackIdleCalls, backpackDirtyCalls, hiddenPopupSkips;
    private static readonly long[] wakeReasons = new long[10];
    private static readonly string[] wakeNames = {
        "unbound", "invalidation", "safety", "hover", "selected", "holding", "lock", "drag", "assembly", "hoverTween"
    };
    private static int capturedResults, capturedPool;
    private static float capturedOffset;
    private static bool hasGridSample;
    private static double frameSeconds;
    private static readonly float[] recentFrameMs = new float[4096];
    private static int recentCount, recentNext;
    internal static void Start()
    {
        frames = workspaceTicks = catalogueTicks = gridTicks = backpackTicks = nativeSlotTicks = 0;
        fullSlots = idleSlots = visualSlots = visualSlotTicks = coalescedDirtySlots = nativeDirtySlots = 0;
        assignments = recycledRows = wheels = catalogueRefreshes = culledSlots = culledBackpack = 0;
        backpackNativeTicks = backpackViewTicks = backpackNativeCalls = backpackViewCalls = 0;
        backpackIdleCalls = backpackDirtyCalls = hiddenPopupSkips = 0;
        Array.Clear(wakeReasons, 0, wakeReasons.Length);
        capturedResults = capturedPool = 0; capturedOffset = 0f; hasGridSample = false;
        frameSeconds = 0; recentCount = recentNext = 0; frameId = lastCountedFrame = -1; workspaceCalls = 0; Enabled = true;
    }
    internal static void Stop() { Enabled = false; frameId = -1; }
    internal static void BeginFrame(bool open)
    {
        frameId = Enabled && open && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput() ? Time.frameCount : -1;
    }
    internal static long Begin() => Enabled && frameId == Time.frameCount ? Stopwatch.GetTimestamp() : 0L;
    private static long Elapsed(long started) => started == 0 ? 0 : Stopwatch.GetTimestamp() - started;
    internal static void EndWorkspace(long started)
    {
        if (started == 0) return;
        workspaceTicks += Elapsed(started); ++workspaceCalls;
        if (lastCountedFrame == Time.frameCount) return;
        lastCountedFrame = Time.frameCount; ++frames;
        float seconds = Mathf.Max(0f, Time.unscaledDeltaTime);
        frameSeconds += seconds;
        recentFrameMs[recentNext] = seconds * 1000f;
        recentNext = (recentNext + 1) % recentFrameMs.Length;
        recentCount = Math.Min(recentCount + 1, recentFrameMs.Length);
    }
    internal static void EndCatalogue(long started) { if (started != 0) catalogueTicks += Elapsed(started); }
    internal static void EndGrid(long started, XUiC_RebirthCreativeCatalogueGrid grid)
    {
        if (started == 0) return;
        gridTicks += Elapsed(started);
        // Preserve the actual sampled catalogue after closing Creative to type "stop".
        // Fix26 queried ActiveInstance at report time and misleadingly printed zero then.
        capturedResults = grid.CatalogueCount; capturedPool = grid.PooledCount;
        capturedOffset = grid.PixelOffset; hasGridSample = true;
    }
    internal static void EndBackpack(long started) { if (started != 0) backpackTicks += Elapsed(started); }
    internal static void EndNativeSlot(long started) { if (started != 0) { nativeSlotTicks += Elapsed(started); ++fullSlots; } }
    internal static void EndVisualSlot(long started, bool dirty)
    {
        if (started == 0) return;
        visualSlotTicks += Elapsed(started); ++visualSlots;
        if (dirty) ++coalescedDirtySlots;
    }
    internal static void CountIdleSlot(bool dirty)
    {
        if (!Enabled || frameId != Time.frameCount) return;
        ++idleSlots; if (dirty) ++coalescedDirtySlots;
    }
    internal static void CountWake(RebirthCreativeSlotWakeReason reasons, bool dirty)
    {
        if (!Enabled || frameId != Time.frameCount) return;
        if (dirty) ++nativeDirtySlots;
        for (int i = 0; i < wakeReasons.Length; ++i)
            if (((int)reasons & (1 << i)) != 0) ++wakeReasons[i];
    }
    internal static void EndBackpackSlot(long started, bool dirty)
    {
        if (started == 0) return;
        backpackNativeTicks += Elapsed(started); ++backpackNativeCalls;
        if (dirty) ++backpackDirtyCalls;
    }
    internal static void EndBackpackViews(long started)
    {
        if (started == 0) return;
        backpackViewTicks += Elapsed(started); ++backpackViewCalls;
    }
    internal static void CountIdleBackpackSlot()
    { if (Enabled && frameId == Time.frameCount) ++backpackIdleCalls; }
    internal static void CountHiddenPopup()
    { if (Enabled && frameId == Time.frameCount) ++hiddenPopupSkips; }
    internal static void CountCulledBackpack() { if (Enabled && frameId == Time.frameCount) ++culledBackpack; }
    internal static void CountCulledSlot() { if (Enabled && frameId == Time.frameCount) ++culledSlots; }
    internal static void CountAssignment() { if (Enabled && frameId == Time.frameCount) ++assignments; }
    internal static void CountRecycledRow() { if (Enabled && frameId == Time.frameCount) ++recycledRows; }
    internal static void CountWheel() { if (Enabled) ++wheels; }
    internal static void CountCatalogueRefresh() { if (Enabled && frameId == Time.frameCount) ++catalogueRefreshes; }
    private static string F(double number) => number.ToString("0.000", CultureInfo.InvariantCulture);
    private static double Milliseconds(long ticks) => ticks * (1000.0 / Stopwatch.Frequency);
    internal static string Report()
    {
        var workspace = XUiC_RebirthCreativeWorkspace.ActiveInstance;
        var b = new StringBuilder("[REBIRTH CreativePerf Fix30] ");
        b.Append(Enabled ? "capturing" : "stopped").Append(" samples=").Append(frames)
            .Append(" results=").Append(hasGridSample ? capturedResults.ToString(CultureInfo.InvariantCulture) : "unsampled")
            .Append(" pool=").Append(hasGridSample ? capturedPool.ToString(CultureInfo.InvariantCulture) : "unsampled")
            .Append(" visible=170 offsetPx=").Append(F(capturedOffset))
            .Append(" metadata=last-sampled-grid currentWindow=")
            .Append(workspace?.IsWorkspaceOpen == true ? "open" : "closed");
        if (frames == 0)
            return b.Append("\nNo samples yet. Open Creative and close the console; console-open frames are excluded.").ToString();
        float[] sorted = new float[recentCount]; Array.Copy(recentFrameMs, sorted, recentCount); Array.Sort(sorted);
        b.Append("\nObserved GAME frame: meanMs=").Append(F(frameSeconds * 1000.0 / frames))
            .Append(" fps=").Append(F(frameSeconds > 0 ? frames / frameSeconds : 0))
            .Append(" recentP95Ms=").Append(F(sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * .95) - 1)]));
        b.Append("\nWorkspace calls/gameFrame=").Append(F(workspaceCalls / (double)frames));
        b.Append("\nCPU inclusive meanMs: workspace=").Append(F(Milliseconds(workspaceTicks) / frames))
            .Append(" catalogue=").Append(F(Milliseconds(catalogueTicks) / frames))
            .Append(" grid=").Append(F(Milliseconds(gridTicks) / frames))
            .Append(" nativeSlotBodies=").Append(F(Milliseconds(nativeSlotTicks) / frames))
            .Append(" visualSlotFlush=").Append(F(Milliseconds(visualSlotTicks) / frames))
            .Append(" backpack=").Append(F(Milliseconds(backpackTicks) / frames));
        b.Append("\nMean native slot updates/frame=").Append(F(fullSlots / (double)frames))
            .Append(" idleSkips/frame=").Append(F(idleSlots / (double)frames))
            .Append(" cataloguePresentationCulled/frame=").Append(F(culledSlots / (double)frames))
            .Append(" backpackPresentationCulled/frame=").Append(F(culledBackpack / (double)frames))
            .Append(" visualFlushes/frame=").Append(F(visualSlots / (double)frames))
            .Append(" coalescedDirtyHints/frame=").Append(F(coalescedDirtySlots / (double)frames))
            .Append(" nativeDirty/frame=").Append(F(nativeDirtySlots / (double)frames))
            .Append(" assignments=").Append(assignments).Append(" preparedRows=").Append(recycledRows)
            .Append(" wheelEvents=").Append(wheels).Append(" nativeFilterRefreshes=").Append(catalogueRefreshes);
        b.Append("\nBackpack slot work (inside backpack total): nativeMs/frame=").Append(F(Milliseconds(backpackNativeTicks) / frames))
            .Append(" viewsMs/frame=").Append(F(Milliseconds(backpackViewTicks) / frames))
            .Append(" nativeCalls/frame=").Append(F(backpackNativeCalls / (double)frames))
            .Append(" viewsCalls/frame=").Append(F(backpackViewCalls / (double)frames))
            .Append(" idleSkips/frame=").Append(F(backpackIdleCalls / (double)frames))
            .Append(" dirtyNativeCalls/frame=").Append(F(backpackDirtyCalls / (double)frames))
            .Append(" hiddenPopupSkips/frame=").Append(F(hiddenPopupSkips / (double)frames));
        b.Append("\nNative wake reasons/frame (overlap):");
        for (int i = 0; i < wakeReasons.Length; ++i)
            b.Append(' ').Append(wakeNames[i]).Append('=').Append(F(wakeReasons[i] / (double)frames));
        b.Append("\nCatalogue only: native dirty hints are coalesced; explicit invalidation, input and locks are immediate. Safety refresh <=0.5s.");
        b.Append("\nReal backpack: every dirty flag and inventory sync is immediate; only clean non-interacting slots cache. Safety refresh <=0.25s.");
        b.Append("\nNested timings: workspace includes catalogue+backpack+popup; catalogue includes grid; grid includes native slot bodies AND visual flushes. Do not sum nested totals.")
            .Append("\nThese are instrumented CPU bodies and observed game-frame times, not GPU timing, allocations or an automatic before/after comparison. Instrumentation has overhead.");
        return b.ToString();
    }
}

[Preserve]
public sealed class ConsoleCmdRebirthCreativePerf : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override int DefaultPermissionLevel => 1000;
    public override string[] getCommands() => new[] { "rbcreativeperf" };
    public override string getDescription() => "Capture REBIRTH Creative frame times, CPU update bodies and row/slot work counts.";
    public override string getHelp() => "rbcreativeperf start | native | status | stop | isolate | cancel\n" +
        "native: optional native XUi/NGUI timings with normal interactive UI; auto-stop after 90s. " +
        "start/status/stop: Creative CPU capture. isolate: reversible 6-phase ~60s stationary test; arm, open Creative, close console. " +
        "Do not interact while sections temporarily disappear. cancel or closing Creative restores normal state. No inventory/save changes.";
    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        if (!_senderInfo.IsLocalGame || _senderInfo.RemoteClientInfo != null || _senderInfo.NetworkConnection != null)
        { Print("[REBIRTH CreativePerf] Use the local in-game console, not a server/remote console."); return; }
        string arg = _params != null && _params.Count > 0 ? (_params[0] ?? "").Trim().ToLowerInvariant() : "status";
        if (_params != null && _params.Count > 1) { Print(getHelp()); return; }
        if (arg == "native")
        {
            if (RebirthCreativeIsolation.Active) RebirthCreativeIsolation.Cancel("starting native capture");
            RebirthCreativeIsolation.ClearReport();
            Print(RebirthCreativeNativeTiming.Start());
        }
        else if (arg == "isolate")
        {
            if (RebirthCreativeNativeTiming.Enabled) RebirthCreativeNativeTiming.Stop("starting isolation");
            RebirthCreativeNativeTiming.ClearReport();
            Print(RebirthCreativeIsolation.Request());
        }
        else if (arg == "cancel")
        {
            if (RebirthCreativeNativeTiming.Enabled) Print(RebirthCreativeNativeTiming.Stop("cancelled by user"));
            else Print(RebirthCreativeIsolation.Cancel("cancelled by user"));
        }
        else if (arg == "start")
        {
            if (RebirthCreativeNativeTiming.Enabled) RebirthCreativeNativeTiming.Stop("starting standard capture");
            RebirthCreativeNativeTiming.ClearReport();
            if (RebirthCreativeIsolation.Active) RebirthCreativeIsolation.Cancel("starting standard capture");
            RebirthCreativeIsolation.ClearReport();
            RebirthCreativePerformance.Start();
            Print("[REBIRTH CreativePerf] Capture started/reset. Open Creative, close the console, browse, then run rbcreativeperf stop.");
        }
        else if (arg == "status") Print(RebirthCreativeNativeTiming.Enabled || RebirthCreativeNativeTiming.HasReport
            ? RebirthCreativeNativeTiming.Report() : RebirthCreativeIsolation.Active || RebirthCreativeIsolation.HasReport
            ? RebirthCreativeIsolation.Report() : RebirthCreativePerformance.Report());
        else if (arg == "stop")
        {
            if (RebirthCreativeNativeTiming.Enabled) Print(RebirthCreativeNativeTiming.Stop("stopped by user"));
            else if (RebirthCreativeNativeTiming.HasReport) Print(RebirthCreativeNativeTiming.Report());
            else if (RebirthCreativeIsolation.Active) Print(RebirthCreativeIsolation.Cancel("stopped by user"));
            else if (RebirthCreativeIsolation.HasReport) Print(RebirthCreativeIsolation.Report());
            else { RebirthCreativePerformance.Stop(); Print(RebirthCreativePerformance.Report()); }
        }
        else Print(getHelp());
    }
    private static void Print(string text) { SdtdConsole.Instance.Output(text); Log.Out(text); }
}

/// <summary>
/// Opt-in, finite ablation of a stationary Creative window. No gameplay policy is changed.
/// Freezing means bypassing only this workspace's controller traversal; Unity/NGUI/global XUi
/// can still do work. Hiding a subtree removes its rendering AND component/input work, so a
/// difference is NOT labelled GPU time. Exact activeSelf values are restored on exit.
/// </summary>
internal static class RebirthCreativeIsolation
{
    private const float SettleSeconds = 2f, SampleSeconds = 8f;
    private static readonly string[] Names = {
        "normal-before", "controllers-frozen", "frozen-left-hidden",
        "frozen-right-hidden", "frozen-all-content-hidden", "normal-restored"
    };
    private sealed class ActiveState
    {
        internal GameObject Object;
        internal bool WasActive;
    }
    private sealed class Sample
    {
        internal int Frames, Calls, RecentCount, Next;
        internal double Seconds;
        internal long BodyTicks, HeapStart, HeapEnd;
        internal int GcStart, GcEnd;
        internal readonly float[] FrameMs = new float[4096];
        internal string Geometry;
        internal bool Complete;
        internal void AddFrame(float dt)
        {
            ++Frames; Seconds += dt;
            FrameMs[Next] = dt * 1000f; Next = (Next + 1) % FrameMs.Length;
            RecentCount = Math.Min(RecentCount + 1, FrameMs.Length);
        }
    }
    private static readonly List<ActiveState> Objects = new List<ActiveState>();
    private static readonly Sample[] Samples = new Sample[6];
    private static XUiC_RebirthCreativeWorkspace owner;
    private static GameObject left, right;
    private static bool requested, finishedPhase, canSample;
    private static int phase = -1, preparedFrame = -1, recordedFrame = -1;
    private static int catalogueCount;
    private static float offset, settle;
    private static string lastReport;
    internal static bool Active => requested;
    internal static bool HasReport => !string.IsNullOrEmpty(lastReport);

    internal static string Request()
    {
        if (requested) return "[REBIRTH CreativeIsolation 28] Already armed/running. Use rbcreativeperf cancel to restore and stop.";
        RebirthCreativePerformance.Stop();
        Objects.Clear(); Array.Clear(Samples, 0, Samples.Length);
        owner = null; left = right = null; phase = -1; catalogueCount = 0; offset = 0f;
        preparedFrame = recordedFrame = -1; finishedPhase = canSample = false;
        lastReport = null; requested = true;
        return "[REBIRTH CreativeIsolation 28] Armed. Open Creative and close the console. " +
            "Keep the pointer over the empty header; do not click, type, scroll or move for about 60 seconds. " +
            "The catalogue/backpack will temporarily disappear and restore automatically. " +
            "Esc/closing Creative or rbcreativeperf cancel restores and aborts. This is a diagnostic, not a performance fix.";
    }
    internal static void ClearReport() { lastReport = null; }

    // Invoked before native controller traversal. Stage transitions happen here, not halfway
    // through rendering a sampled frame. A transition and a reopened console incur warmup again.
    internal static bool BeforeUpdate(XUiC_RebirthCreativeWorkspace workspace)
    {
        if (!requested) return false;
        try
        {
            if (!workspace.IsWorkspaceOpen) return false;
            if (owner != null && owner != workspace) return false;
            if (preparedFrame == Time.frameCount) return IsFrozen;
            preparedFrame = Time.frameCount; canSample = false;
            if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
            { settle = SettleSeconds; return IsFrozen; }
            if (workspace.xui?.DragAndDropWindow == null || !workspace.xui.DragAndDropWindow.IsEmpty())
            { Cancel("cursor contains an item; no inventory action was cancelled or consumed"); return true; }
            CursorControllerAbs cursor = workspace.xui.playerUI.CursorController;
            if (cursor != null && (cursor.GetMouseButton(UICamera.MouseButton.LeftButton) ||
                cursor.GetMouseButton(UICamera.MouseButton.RightButton)))
            { Cancel("mouse press detected; stationary diagnostic aborted"); return true; }
            if (owner == null)
            {
                if (!Attach(workspace)) return false;
                EnterPhase(0); return IsFrozen;
            }
            if (owner.Catalogue == null || owner.Catalogue.CatalogueCount != catalogueCount ||
                Mathf.Abs(owner.Catalogue.PixelOffset - offset) > .2f)
            { Cancel("catalogue or scroll position changed during the stationary test"); return true; }
            if (!MaskIntact())
            { Cancel("an external component reactivated a hidden diagnostic section; mask was not maintained"); return true; }
            if (finishedPhase)
            {
                finishedPhase = false;
                if (phase + 1 == Names.Length) { Finish(); return false; }
                EnterPhase(phase + 1); return IsFrozen;
            }
            float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
            if (settle > 0f) settle = Mathf.Max(0f, settle - dt);
            else if (dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt)) canSample = true;
            return IsFrozen;
        }
        catch (Exception error)
        {
            Cancel("diagnostic exception: " + error.GetType().Name + ": " + error.Message);
            return false;
        }
    }
    private static bool IsFrozen => requested && phase >= 1 && phase <= 4;
    private static bool Attach(XUiC_RebirthCreativeWorkspace workspace)
    {
        owner = workspace;
        left = owner.GetChildById("creativeCatalogue")?.ViewComponent?.UiTransform?.gameObject;
        right = owner.GetChildById("creativeInventory")?.ViewComponent?.UiTransform?.gameObject;
        if (left == null || right == null || owner.Catalogue == null || !left.activeSelf || !right.activeSelf)
        { Cancel("expected visible Creative sections were not found; no isolation started"); return false; }
        if (owner.BackpackView?.SelectedItem != null)
        { Cancel("close/reopen Creative first so there is no selected-item action list"); return false; }
        foreach (XUiC_ItemStack slot in owner.GetChildrenByType<XUiC_ItemStack>())
            if (slot.IsLocked || slot.IsHolding)
            { Cancel("a slot has a pending lock/hold; finish that action before isolating"); return false; }
        foreach (XUiController child in owner.Children)
        {
            GameObject go = child.ViewComponent?.UiTransform?.gameObject;
            if (go == null || go == owner.ViewComponent.UiTransform.gameObject) continue;
            bool exists = false;
            for (int i = 0; i < Objects.Count; ++i) if (Objects[i].Object == go) { exists = true; break; }
            if (!exists) Objects.Add(new ActiveState { Object = go, WasActive = go.activeSelf });
        }
        if (!IsRemembered(left) || !IsRemembered(right))
        { Cancel("Creative sections are not the expected direct-child layout; no isolation started"); return false; }
        catalogueCount = owner.Catalogue.CatalogueCount; offset = owner.Catalogue.PixelOffset;
        return true;
    }
    private static bool MaskIntact()
    {
        if (phase == 2) return left != null && !left.activeSelf;
        if (phase == 3) return right != null && !right.activeSelf;
        if (phase == 4)
            for (int i = 0; i < Objects.Count; ++i)
                if (Objects[i].Object != null && Objects[i].Object.activeSelf) return false;
        return true;
    }
    private static bool IsRemembered(GameObject go)
    {
        for (int i = 0; i < Objects.Count; ++i) if (Objects[i].Object == go) return true;
        return false;
    }
    private static void EnterPhase(int next)
    {
        if (phase >= 0 && Samples[phase] != null && Samples[phase].Complete)
            Log.Out("[REBIRTH CreativeIsolation 28] " + DescribeSample(phase));
        string restoreError = RestoreObjects();
        if (!string.IsNullOrEmpty(restoreError)) throw new InvalidOperationException(restoreError);
        phase = next; settle = SettleSeconds; canSample = false; recordedFrame = -1;
        if (phase == 2) left.SetActive(false);
        else if (phase == 3) right.SetActive(false);
        else if (phase == 4)
            for (int i = 0; i < Objects.Count; ++i) if (Objects[i].Object != null) Objects[i].Object.SetActive(false);
        Samples[phase] = new Sample();
        Log.Out("[REBIRTH CreativeIsolation 28] phase " + (phase + 1) + "/6 " + Names[phase] +
            " warmup=2s sample=8s; frozen=" + IsFrozen + "; left=" + left.activeSelf + "; right=" + right.activeSelf);
    }
    internal static long BeginWorkspace(XUiC_RebirthCreativeWorkspace workspace)
    {
        if (!requested || owner != workspace || !canSample) return 0L;
        return Stopwatch.GetTimestamp();
    }
    internal static void AfterUpdate(XUiC_RebirthCreativeWorkspace workspace, long started)
    {
        if (started == 0L || !requested || owner != workspace || phase < 0 || !canSample) return;
        long ticks = Stopwatch.GetTimestamp() - started;
        Sample sample = Samples[phase];
        if (sample.Geometry == null)
        {
            // Snapshot outside the measured body and exclude this frame and a short settling
            // interval. Enumeration/formatting must not contaminate game-frame timing.
            sample.Geometry = DescribeGeometry(); settle = .25f; canSample = false; return;
        }
        // Every workspace invocation contributes CPU ticks, but game-frame time is counted
        // only once per Time.frameCount. Report the ratio instead of assuming 1 XUi update/frame.
        sample.BodyTicks += ticks; ++sample.Calls;
        if (recordedFrame == Time.frameCount) return;
        recordedFrame = Time.frameCount;
        if (sample.Frames == 0)
        { sample.HeapStart = GC.GetTotalMemory(false); sample.GcStart = GC.CollectionCount(0); }
        sample.AddFrame(Mathf.Max(0f, Time.unscaledDeltaTime));
        if (sample.Seconds < SampleSeconds) return;
        sample.HeapEnd = GC.GetTotalMemory(false); sample.GcEnd = GC.CollectionCount(0); sample.Complete = true;
        // Transition/report on the NEXT game frame, allowing any additional workspace calls
        // in this last frame to remain attributed to the same phase.
        finishedPhase = true;
    }
    private static string RestoreObjects()
    {
        string error = null;
        for (int i = 0; i < Objects.Count; ++i)
        {
            ActiveState state = Objects[i];
            if (state.Object == null) continue; // World/UI already destroyed: there is no object to restore.
            try { if (state.Object.activeSelf != state.WasActive) state.Object.SetActive(state.WasActive); }
            catch (Exception ex) { error = (error ?? "Restore failure:") + " " + ex.GetType().Name + ": " + ex.Message; }
        }
        return error;
    }
    internal static string Cancel(string reason)
    {
        if (!requested) return lastReport ?? "[REBIRTH CreativeIsolation 28] No isolation run is active.";
        string restoreError = RestoreObjects();
        requested = canSample = finishedPhase = false;
        if (phase >= 0 && Samples[phase] != null && !Samples[phase].Complete && Samples[phase].Frames > 0)
        {
            Samples[phase].HeapEnd = GC.GetTotalMemory(false);
            Samples[phase].GcEnd = GC.CollectionCount(0);
        }
        lastReport = BuildReport("aborted: " + reason, restoreError);
        owner = null; left = right = null; Objects.Clear();
        Log.Out(lastReport); return lastReport;
    }
    internal static void WorkspaceClosing(XUiC_RebirthCreativeWorkspace workspace)
    { if (requested && (owner == null || owner == workspace)) Cancel("Creative closed; original active states restored before native OnClose"); }
    private static void Finish()
    {
        string restoreError = RestoreObjects(); requested = canSample = false;
        lastReport = BuildReport("completed", restoreError);
        owner = null; left = right = null; Objects.Clear();
        Log.Out(lastReport);
    }
    internal static string Report()
    {
        if (requested) return "[REBIRTH CreativeIsolation 28] " + (phase < 0 ? "armed; open Creative and close console" :
            "phase " + (phase + 1) + "/6 " + Names[phase] + "; sampleSeconds=" + F(Samples[phase]?.Seconds ?? 0)) +
            ". Console pauses the sequence; normal state is restored on cancel/close/completion.";
        return lastReport ?? "[REBIRTH CreativeIsolation 28] No isolation report yet.";
    }
    private static string BuildReport(string status, string restoreError)
    {
        StringBuilder text = new StringBuilder("[REBIRTH CreativeIsolation 28] ").Append(status)
            .Append(" results=").Append(catalogueCount).Append(" offsetPx=").Append(F(offset))
            .Append(" restore=").Append(restoreError ?? "ok");
        for (int i = 0; i < Samples.Length; ++i) if (Samples[i] != null) text.Append('\n').Append(DescribeSample(i));
        text.Append("\nGame frame times are counted once per Time.frameCount; workspace CPU includes every invocation within those frames.")
            .Append("\nFrozen means only Creative controller traversal was bypassed; global XUi/NGUI and the world still run.")
            .Append("\nHidden subtrees remove rendering, component work and input surfaces together. Differences are NOT GPU measurements and are NOT additive.")
            .Append("\nDraw-call lists are optional reflected panel metadata, NOT measured GPU submissions. Counts below are scoped to the Creative hierarchy.")
            .Append("\nNo inventory data, graphics options, menu-open flags, save data, capacity or companion records were rewritten. This diagnostic does not claim an FPS fix.");
        return text.ToString();
    }
    private static string DescribeSample(int index)
    {
        Sample s = Samples[index];
        string prefix = Names[index] + " complete=" + s.Complete + " frames=" + s.Frames;
        if (s.Frames == 0) return prefix + " unsampled";
        float[] copy = new float[s.RecentCount]; Array.Copy(s.FrameMs, copy, s.RecentCount); Array.Sort(copy);
        float p95 = copy[Math.Min(copy.Length - 1, (int)Math.Ceiling(copy.Length * .95) - 1)];
        return prefix + " meanMs=" + F(s.Seconds * 1000.0 / s.Frames) + " fps=" + F(s.Frames / s.Seconds) +
            " recentP95Ms=" + F(p95) + " workspaceCallsPerGameFrame=" + F(s.Calls / (double)s.Frames) +
            " workspaceBodyMsPerGameFrame=" + F(s.BodyTicks * (1000.0 / Stopwatch.Frequency) / s.Frames) +
            " gc0=" + (s.GcEnd - s.GcStart) + " heapDeltaMiB=" + F((s.HeapEnd - s.HeapStart) / (1024.0 * 1024.0)) +
            " (heap delta is not allocation volume) " + (s.Geometry ?? "geometry=unavailable");
    }
    private static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    private static string DescribeGeometry()
    {
        try
        {
            Transform root = owner?.ViewComponent?.UiTransform;
            if (root == null) return "geometry=unavailable";
            int activeNodes = 0, activePanels = 0, activeRenderers = 0, calls = 0, unknown = 0;
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < nodes.Length; ++i) if (nodes[i].gameObject.activeInHierarchy) ++activeNodes;
            UIPanel[] panels = root.GetComponentsInChildren<UIPanel>(true);
            for (int i = 0; i < panels.Length; ++i)
            {
                if (!panels[i].enabled || !panels[i].gameObject.activeInHierarchy) continue;
                ++activePanels; int count = OptionalCollectionSize(OptionalMember(panels[i], "drawCalls"));
                if (count >= 0) calls += count; else ++unknown;
            }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; ++i)
                if (renderers[i].enabled && renderers[i].gameObject.activeInHierarchy) ++activeRenderers;
            return "nodes=" + activeNodes + "/" + nodes.Length + " panels=" + activePanels + "/" + panels.Length +
                " renderers=" + activeRenderers + "/" + renderers.Length + " panelDrawCallEntries=" + calls + " unknownPanels=" + unknown;
        }
        catch (Exception error) { return "geometry=unavailable:" + error.GetType().Name; }
    }
    private static object OptionalMember(object value, string name)
    {
        if (value == null) return null;
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        Type type = value.GetType();
        System.Reflection.FieldInfo field = type.GetField(name, flags);
        if (field != null) return field.GetValue(value);
        System.Reflection.PropertyInfo property = type.GetProperty(name, flags);
        return property != null && property.GetIndexParameters().Length == 0 ? property.GetValue(value, null) : null;
    }
    private static int OptionalCollectionSize(object value)
    {
        if (value == null) return -1;
        var collection = value as System.Collections.ICollection;
        if (collection != null) return collection.Count;
        object count = OptionalMember(value, "size") ?? OptionalMember(value, "Count");
        return count is int ? (int)count : -1;
    }
}

/// <summary>
/// Opt-in, bounded native UI method timings. All hooks observe; none skip or change originals.
/// Installed only by rbcreativeperf native and removed by stop/cancel/UI teardown/90s timeout.
/// Whitelisted names are resolved once against the loaded assemblies; absent/ambiguous methods
/// are explicitly reported, not bound through invented compile-time members.
/// </summary>
internal static class RebirthCreativeNativeTiming
{
    private const string HarmonyId = "rebirth.creative.native.timings.29";
    private const int MaximumMetrics = 32;
    private const float MaximumSeconds = 90f, SettlingSeconds = 1f;
    private static readonly string[,] Targets = {
        { "XUi", "Update" }, { "XUi", "OnUpdate" }, { "XUi", "LateUpdate" },
        { "LocalPlayerUI", "Update" }, { "GUIWindowManager", "Update" }, { "NGUIWindowManager", "Update" },
        { "UICamera", "Update" }, { "UICamera", "LateUpdate" }, { "UICamera", "ProcessEvents" },
        { "UICamera", "ProcessMouse" }, { "UICamera", "Raycast" },
        { "UIPanel", "LateUpdate" }, { "UIPanel", "UpdateSelf" }, { "UIPanel", "UpdateWidgets" },
        { "UIPanel", "FillAllDrawCalls" }, { "UIPanel", "FillDrawCall" }, { "UIDrawCall", "UpdateGeometry" }
    };
    private sealed class Metric
    {
        internal System.Reflection.MethodInfo Method;
        internal string Name;
    }
    private sealed class Frame
    {
        internal int Id = -1, Mode = -1;
        internal bool Eligible;
        internal readonly long[] Ticks = new long[MaximumMetrics];
        internal readonly int[] Calls = new int[MaximumMetrics], Errors = new int[MaximumMetrics];
        internal void Reset(int id, int mode, bool eligible)
        {
            Id = id; Mode = mode; Eligible = eligible;
            Array.Clear(Ticks, 0, Ticks.Length); Array.Clear(Calls, 0, Calls.Length); Array.Clear(Errors, 0, Errors.Length);
        }
    }
    private sealed class Totals
    {
        internal long Frames;
        internal double Seconds;
        internal readonly long[] Ticks = new long[MaximumMetrics], Calls = new long[MaximumMetrics], Errors = new long[MaximumMetrics];
        internal readonly float[] Recent = new float[4096];
        internal int Count, Next;
        internal void Add(Frame frame, float seconds)
        {
            ++Frames; Seconds += seconds;
            Recent[Next] = seconds * 1000f; Next = (Next + 1) % Recent.Length; Count = Math.Min(Count + 1, Recent.Length);
            for (int i = 0; i < metrics.Count; ++i) { Ticks[i] += frame.Ticks[i]; Calls[i] += frame.Calls[i]; Errors[i] += frame.Errors[i]; }
        }
    }
    internal struct Scope
    {
        internal long Started;
        internal int FrameId, MetricIndex, Generation;
    }
    private static readonly List<Metric> metrics = new List<Metric>();
    private static readonly Dictionary<System.Reflection.MethodBase, int> metricIndex =
        new Dictionary<System.Reflection.MethodBase, int>();
    private static readonly List<System.Reflection.MethodInfo> patched = new List<System.Reflection.MethodInfo>();
    private static readonly List<string> targetStatus = new List<string>();
    private static readonly Frame[] frames = { new Frame(), new Frame(), new Frame(), new Frame() };
    private static Totals[] totals = { new Totals(), new Totals() };
    private static HarmonyLib.Harmony harmony;
    private static RebirthCreativeNativeFrameProbe probe;
    private static XUi sourceUi;
    private static int mainThread, generation, lastMode = -2, lastProbeFrame = -1, skippedFrames, recorderErrors;
    private static float readyAt, deadline;
    private static bool enabled;
    private static string lastReport = string.Empty;
    internal static bool Enabled => enabled;
    internal static bool HasReport => !string.IsNullOrEmpty(lastReport);
    internal static void ClearReport() { if (!enabled) lastReport = string.Empty; }

    internal static string Start()
    {
        if (enabled) Stop("restarted");
        XUi ui = LocalPlayerUI.GetUIForPrimaryPlayer()?.xui;
        if (ui == null || GameManager.Instance?.World == null)
            return "[REBIRTH CreativeNative30] A loaded local game is required.";
        // Do not stack another capture over hooks that failed to detach on a prior attempt.
        RemoveHooks();
        if (patched.Count != 0)
            return "[REBIRTH CreativeNative30] Previous diagnostic hooks could not be removed. Restart the game before another capture.";
        metrics.Clear(); metricIndex.Clear(); targetStatus.Clear();
        totals = new[] { new Totals(), new Totals() };
        foreach (Frame frame in frames) frame.Reset(-1, -1, false);
        lastMode = -2; lastProbeFrame = -1; skippedFrames = recorderErrors = 0;
        sourceUi = ui; mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        unchecked { ++generation; }
        lastReport = string.Empty;
        try
        {
            harmony = harmony ?? new HarmonyLib.Harmony(HarmonyId);
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
            var prefix = new HarmonyLib.HarmonyMethod(typeof(RebirthCreativeNativeTiming).GetMethod(nameof(BeforeNative), flags));
            var finalizer = new HarmonyLib.HarmonyMethod(typeof(RebirthCreativeNativeTiming).GetMethod(nameof(AfterNative), flags));
            for (int i = 0; i < Targets.GetLength(0); ++i) Install(Targets[i, 0], Targets[i, 1], prefix, finalizer);
            if (metrics.Count == 0)
            {
                RemoveHooks();
                lastReport = BuildReport("no supported methods could be attached");
                return lastReport;
            }
            probe = ui.gameObject.GetComponent<RebirthCreativeNativeFrameProbe>();
            if (probe == null) probe = ui.gameObject.AddComponent<RebirthCreativeNativeFrameProbe>();
            RebirthCreativePerformance.Start();
            readyAt = Time.realtimeSinceStartup + SettlingSeconds;
            deadline = Time.realtimeSinceStartup + MaximumSeconds;
            enabled = true; probe.enabled = true;
            return "[REBIRTH CreativeNative30] Started. Normal UI remains interactive and visible. " +
                "Close the console, compare Creative closed/open or browse normally, then rbcreativeperf stop. " +
                "Automatic stop after 90 seconds. Other windows may be present in the Creative-closed bucket. " +
                "Installed native timing targets=" + metrics.Count + "/" + Targets.GetLength(0) + ".\n" + string.Join("\n", targetStatus);
        }
        catch (Exception error)
        {
            enabled = false; RebirthCreativePerformance.Stop(); RemoveHooks();
            if (probe != null) probe.enabled = false;
            lastReport = "[REBIRTH CreativeNative30] Capture setup failed without changing normal UI: " + error;
            return lastReport;
        }
    }
    private static Type ResolveType(string name)
    {
        // Exact full type names only. No assemblies/types are searched inside frame hooks.
        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(name, false);
            if (found != null) return found;
        }
        return null;
    }
    private static void Install(string typeName, string methodName, HarmonyLib.HarmonyMethod prefix, HarmonyLib.HarmonyMethod finalizer)
    {
        string label = typeName + "." + methodName;
        System.Reflection.MethodInfo target = null;
        try
        {
            Type type = ResolveType(typeName);
            if (type == null) { targetStatus.Add(label + " : unavailable (type)"); return; }
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(flags))
            {
                if (method.Name != methodName || method.ContainsGenericParameters || method.IsAbstract ||
                    (method.ReturnType != typeof(void) && method.ReturnType != typeof(bool)) || method.GetMethodBody() == null) continue;
                if (target != null) { targetStatus.Add(label + " : unavailable (ambiguous overloads; deliberately not guessed)"); return; }
                target = method;
            }
            if (target == null) { targetStatus.Add(label + " : unavailable (no declared managed body)"); return; }
            if (metrics.Count >= MaximumMetrics) { targetStatus.Add(label + " : skipped (bounded target cap)"); return; }
            harmony.Patch(target, prefix: prefix, finalizer: finalizer);
            patched.Add(target);
            metricIndex.Add(target, metrics.Count);
            metrics.Add(new Metric { Method = target, Name = label });
            targetStatus.Add(label + " : installed " + target + " module=" + target.Module.ModuleVersionId);
        }
        catch (Exception error)
        {
            if (target != null)
            {
                try { harmony.Unpatch(target, HarmonyLib.HarmonyPatchType.All, HarmonyId); }
                catch { if (!patched.Contains(target)) patched.Add(target); }
            }
            targetStatus.Add(label + " : unavailable (" + error.GetType().Name + ": " + error.Message + ")");
        }
    }
    // These methods are deliberately NOT marked HarmonyPatch/PatchAll. The command alone installs them.
    public static void BeforeNative(System.Reflection.MethodBase __originalMethod, out Scope __state)
    {
        __state = default(Scope);
        if (!enabled || System.Threading.Thread.CurrentThread.ManagedThreadId != mainThread) return;
        try
        {
            int index;
            if (!metricIndex.TryGetValue(__originalMethod, out index)) return;
            int frameId = Time.frameCount;
            Frame frame = EnsureFrame(frameId);
            if (!frame.Eligible) return;
            __state = new Scope { FrameId = frameId, MetricIndex = index, Generation = generation, Started = Stopwatch.GetTimestamp() };
        }
        catch { ++recorderErrors; } // Recording failure must not alter the original method.
    }
    public static void AfterNative(Scope __state, Exception __exception)
    {
        if (__state.Started == 0 || !enabled || __state.Generation != generation) return;
        try
        {
            Frame frame = frames[__state.FrameId & 3];
            if (frame.Id != __state.FrameId || !frame.Eligible) return;
            frame.Ticks[__state.MetricIndex] += Stopwatch.GetTimestamp() - __state.Started;
            ++frame.Calls[__state.MetricIndex];
            if (__exception != null) ++frame.Errors[__state.MetricIndex];
        }
        catch { ++recorderErrors; }
        // A void finalizer observes the exception without suppressing/replacing it.
    }
    private static int CurrentMode()
    {
        if (sourceUi == null || GameManager.Instance?.World == null ||
            RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return -1;
        var workspace = XUiC_RebirthCreativeWorkspace.ActiveInstance;
        return workspace?.xui == sourceUi && workspace.IsWorkspaceOpen ? 1 : 0;
    }
    private static Frame EnsureFrame(int id)
    {
        Frame frame = frames[id & 3];
        if (frame.Id == id) return frame;
        int mode = CurrentMode();
        float now = Time.realtimeSinceStartup;
        if (lastMode != mode) { lastMode = mode; readyAt = now + SettlingSeconds; }
        frame.Reset(id, mode, mode >= 0 && now >= readyAt);
        return frame;
    }
    internal static void Pulse(RebirthCreativeNativeFrameProbe caller)
    {
        if (!enabled || !ReferenceEquals(caller, probe)) return;
        try
        {
            int id = Time.frameCount;
            if (id == lastProbeFrame) return;
            Frame current = EnsureFrame(id);
            // LateUpdate runs before rendering. Finalize the PREVIOUS game frame here, after
            // its remaining panel/render-preparation callbacks have had a chance to finish.
            if (lastProbeFrame >= 0)
            {
                Frame prior = frames[lastProbeFrame & 3];
                float seconds = Time.unscaledDeltaTime; // Duration of the preceding game frame.
                if (prior.Id == lastProbeFrame && lastProbeFrame == id - 1 && prior.Eligible &&
                    current.Mode == prior.Mode && CurrentMode() == prior.Mode && seconds > 0f &&
                    !float.IsNaN(seconds) && !float.IsInfinity(seconds)) totals[prior.Mode].Add(prior, seconds);
                else ++skippedFrames;
            }
            if (CurrentMode() != current.Mode) current.Eligible = false; // Discard a menu/console transition frame.
            lastProbeFrame = id;
            if (sourceUi == null || GameManager.Instance?.World == null)
                Print(Stop("world/UI ended"));
            else if (Time.realtimeSinceStartup >= deadline)
                Print(Stop("90-second limit reached"));
        }
        catch (Exception error)
        {
            ++recorderErrors;
            Print(Stop("recorder stopped safely: " + error.GetType().Name));
        }
    }
    internal static void ProbeDisabled(RebirthCreativeNativeFrameProbe caller)
    {
        if (enabled && ReferenceEquals(caller, probe)) Print(Stop("local UI disabled/destroyed"));
    }
    internal static string Stop(string reason)
    {
        enabled = false; RebirthCreativePerformance.Stop();
        // The current frame may still be on a patched stack; it is intentionally not counted.
        RemoveHooks();
        if (probe != null) probe.enabled = false;
        lastReport = BuildReport(reason) + "\n" + RebirthCreativePerformance.Report();
        sourceUi = null;
        return lastReport;
    }
    private static void RemoveHooks()
    {
        for (int i = patched.Count - 1; i >= 0; --i)
        {
            try { harmony.Unpatch(patched[i], HarmonyLib.HarmonyPatchType.All, HarmonyId); patched.RemoveAt(i); }
            catch (Exception error) { targetStatus.Add("detach failed: " + patched[i] + " : " + error.Message); }
        }
    }
    internal static string Report() => enabled ? BuildReport("capturing") + "\n" + RebirthCreativePerformance.Report() : lastReport;
    private static string F(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
    private static string BuildReport(string reason)
    {
        var text = new StringBuilder("[REBIRTH CreativeNative30] ").Append(reason)
            .Append(" unique-frame capture; recorderErrors=").Append(recorderErrors)
            .Append(" excludedTransitionConsoleOrIncompleteFrames=").Append(skippedFrames)
            .Append(" remainingHooks=").Append(patched.Count);
        for (int mode = 0; mode < totals.Length; ++mode)
        {
            Totals t = totals[mode];
            text.Append("\nSTATE ").Append(mode == 1 ? "Creative-open" : "Creative-closed (not necessarily all menus closed)")
                .Append(" frames=").Append(t.Frames);
            if (t.Frames == 0) { text.Append(" unsampled"); continue; }
            float[] sorted = new float[t.Count]; Array.Copy(t.Recent, sorted, t.Count); Array.Sort(sorted);
            text.Append(" meanFrameMs=").Append(F(1000.0 * t.Seconds / t.Frames))
                .Append(" fps=").Append(F(t.Frames / t.Seconds))
                .Append(" recentP95Ms=").Append(F(sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * .95) - 1)]));
            for (int i = 0; i < metrics.Count; ++i)
                text.Append("\n  ").Append(metrics[i].Name)
                    .Append(" inclusiveMs/gameFrame=").Append(F(t.Ticks[i] * (1000.0 / Stopwatch.Frequency) / t.Frames))
                    .Append(" calls/gameFrame=").Append(F(t.Calls[i] / (double)t.Frames))
                    .Append(" exceptionsObserved=").Append(t.Errors[i]);
        }
        text.Append("\nNATIVE TARGET STATUS:\n").Append(string.Join("\n", targetStatus));
        text.Append("\nScope: main-thread calls across ALL instances of each installed native method, including other UI/HUDs. ")
            .Append("Methods can nest: do NOT sum timings. No GPU time, allocations or per-method exclusive cost is measured. ")
            .Append("An installed method with no calls is unobserved, not evidence of zero cost in an unhooked alternative. ")
            .Append("No per-widget Update hooks; capture overhead is still present. Console/transition frames are excluded. ")
            .Append("Normal input/rendering remain enabled; no sections are hidden or frozen by this capture.");
        return text.ToString();
    }
    private static void Print(string message) { SdtdConsole.Instance.Output(message); Log.Out(message); }
}

[Preserve]
public sealed class RebirthCreativeNativeFrameProbe : MonoBehaviour
{
    private void LateUpdate() { RebirthCreativeNativeTiming.Pulse(this); }
    private void OnDisable() { RebirthCreativeNativeTiming.ProbeDisabled(this); }
    private void OnDestroy() { RebirthCreativeNativeTiming.ProbeDisabled(this); }
}
#else
// Keep the familiar command as a status-only endpoint in normal builds. A command left in
// an old test checklist cannot enable profiling or install hooks in this build.
[Preserve]
public sealed class ConsoleCmdRebirthCreativePerf : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override int DefaultPermissionLevel => 1000;
    public override string[] getCommands() => new[] { "rbcreativeperf" };
    public override string getDescription() => "Report the disabled state of REBIRTH UI diagnostics.";
    public override string getHelp() => "rbcreativeperf status | off | stop | cancel\n" +
        "Fix31 normal build: UI profiling/isolation are excluded. start/native/isolate cannot start a capture.";

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        if (!_senderInfo.IsLocalGame || _senderInfo.RemoteClientInfo != null || _senderInfo.NetworkConnection != null)
        {
            SdtdConsole.Instance.Output("[REBIRTH UI Diagnostics Fix31] Use the local in-game console.");
            return;
        }
        if (_params != null && _params.Count > 1)
        {
            SdtdConsole.Instance.Output(getHelp());
            return;
        }
        string arg = _params != null && _params.Count > 0 ? (_params[0] ?? "").Trim().ToLowerInvariant() : "status";
        if (arg == "start" || arg == "native" || arg == "isolate")
        {
            SdtdConsole.Instance.Output("[REBIRTH UI Diagnostics Fix31] Disabled in this build. No capture started; no UI profiling hooks installed.");
            return;
        }
        if (arg != "status" && arg != "off" && arg != "stop" && arg != "cancel")
        {
            SdtdConsole.Instance.Output(getHelp());
            return;
        }
        SdtdConsole.Instance.Output("[REBIRTH UI Diagnostics Fix31] compiled=false active=false. UI timing/counter/isolation code is excluded. Normal UI optimizations remain active.");
    }
}
#endif
