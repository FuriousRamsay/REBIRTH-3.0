using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Presents a stock draggable scrollbar while retaining a base-game XUiC_Paging controller as
/// a hidden virtualization/data mechanism. This lets REBIRTH keep the native Creative/Crafting/
/// Map/Quest/Players population and selection contracts without exposing Previous/Next paging.
///
/// PC050 keeps the native RecipeList hierarchy intact, invalidates the native UIScrollView bounds
/// when the recipe range changes, and routes direct scrollbar track/thumb input through the pager's
/// native PageUp/PageDown methods so RecipeList receives the same refresh path as mouse-wheel input.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthPagerScrollbar : XUiController
{
    private int viewportHeight = 300;
    private XUiC_Paging pager;
    private XUiController scrollView;
    private XUiController pagingRegisteredHost;
    private UIScrollView pagingRegisteredView;
    private UIProgressBar pagingRegisteredBar;
    private void RefreshPagingRegistration()
    {
        UIScrollView view = (scrollView?.ViewComponent as XUiV_ScrollView)?.scrollView;
        UIProgressBar bar = view != null ? view.verticalScrollBar : null;
        if (ReferenceEquals(pagingRegisteredHost, scrollView) && ReferenceEquals(pagingRegisteredView, view)
            && ReferenceEquals(pagingRegisteredBar, bar)) return;
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(pagingRegisteredHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(scrollView);
        pagingRegisteredHost = scrollView; pagingRegisteredView = view; pagingRegisteredBar = bar;
    }
    private XUiController proxy;
    private XUiController recipeList;
    private XUiController craftingTrackInput;
    private bool syncing;
    private int lastPage = -1;
    private int lastMaxPage = -1;
    private int settleFrames = 8;
    private int lastWheelFrame = -1;
    private readonly HashSet<XUiController> wiredScrollControllers = new HashSet<XUiController>();
    private XUiController wiredRecipeRoot;

    // Crafting-specific runtime synchronization/input fallback.
    private bool craftingRawPointerDrag;
    private float nextCraftingForcedRefreshTime;
    private int craftingRawInputLogBudget = 24;
    private int craftingRawPageChangeCount;
    private bool craftingReadyLogged;
    private int craftingRectFailureLogBudget = 3;
    // +1 means PageDown increases pager.GetPage(); -1 means PageDown decreases it.
    // Determined at runtime because the base pager naming is counterintuitive across contexts.
    private int craftingPageDownDirection;

    private bool IsCraftingBridge
    {
        get
        {
            string rootId = ViewComponent != null ? ViewComponent.ID : string.Empty;
            return rootId.IndexOf("Crafting", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public override void Init()
    {
        base.Init();
        ResolveOwnedChildren();
        ResolvePager();
        ResolveRecipeList();
        ResolveCraftingTrackInput();
        WireScroll(this);
        WireScroll(scrollView);
        WireScroll(proxy);
        WireRecipeWheelSurface();
        WireCraftingTrackInput();

        // Do not allow the Crafting bridge to disappear before RecipeList/Paging finishes its
        // delayed native population. A hidden controller may stop receiving Update, which was
        // why the scrollbar only appeared after the first mouse-wheel action.
        if (IsCraftingBridge && ViewComponent != null)
            ViewComponent.IsVisible = true;

        craftingRawPointerDrag = false;
        craftingReadyLogged = false;
        craftingRectFailureLogBudget = 3;
        craftingPageDownDirection = 0;
        nextCraftingForcedRefreshTime = Time.realtimeSinceStartup;
        SyncFromPager(true);
    }

        public override void OnClose()
    {
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(pagingRegisteredHost);
        pagingRegisteredHost = null; pagingRegisteredView = null; pagingRegisteredBar = null;
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(scrollView);
        base.OnClose();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        settleFrames = 8;
        lastPage = -1;
        lastMaxPage = -1;
        ResolveOwnedChildren();
        ResolvePager();
        ResolveRecipeList();
        ResolveCraftingTrackInput();
        WireScroll(this);
        WireScroll(scrollView);
        WireScroll(proxy);
        WireRecipeWheelSurface();
        WireCraftingTrackInput();
        if (IsCraftingBridge && ViewComponent != null)
            ViewComponent.IsVisible = true;
        craftingRawPointerDrag = false;
        craftingReadyLogged = false;
        craftingRectFailureLogBudget = 3;
        craftingPageDownDirection = 0;
        nextCraftingForcedRefreshTime = Time.realtimeSinceStartup;
        craftingRawInputLogBudget = 24;
        SyncFromPager(true);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(scrollView);
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld())
        {
            if (ViewComponent != null) ViewComponent.IsVisible = false;
            return;
        }

        if (scrollView == null || proxy == null) ResolveOwnedChildren();
        RefreshPagingRegistration();
        if (pager == null) ResolvePager();
        if (IsCraftingBridge && recipeList == null) ResolveRecipeList();
        if (IsCraftingBridge && craftingTrackInput == null) ResolveCraftingTrackInput();
        WireRecipeWheelSurface();
        WireCraftingTrackInput();

        if (pager == null) return;

        if (IsCraftingBridge && ViewComponent != null && !ViewComponent.IsVisible)
            ViewComponent.IsVisible = true;

        if (settleFrames > 0)
        {
            settleFrames--;
            SyncFromPager(true);
            if (IsCraftingBridge) PollCraftingTrackRawInput();
            return;
        }

        SyncFromPager(false);
        if (IsCraftingBridge)
        {
            RefreshCraftingScrollbarPeriodically();
            PollCraftingTrackRawInput();
        }

        if (!craftingRawPointerDrag)
            PollScrollbar();
    }

    private void ResolveOwnedChildren()
    {
        string rootId = ViewComponent != null ? ViewComponent.ID : null;

        if (!string.IsNullOrEmpty(rootId))
        {
            scrollView = GetChildById(rootId + "View");
            proxy = GetChildById(rootId + "Proxy");
        }

        if (scrollView == null) scrollView = GetChildById("rebirthPagerScrollView");
        if (proxy == null) proxy = GetChildById("rebirthPagerScrollProxy");

        if (scrollView != null && scrollView.ViewComponent != null)
            viewportHeight = Math.Max(1, scrollView.ViewComponent.Size.y);
        else if (ViewComponent != null)
            viewportHeight = Math.Max(1, ViewComponent.Size.y);
    }

    private void ResolvePager()
    {
        pager = null;

        string rootId = ViewComponent != null ? ViewComponent.ID : string.Empty;
        bool playersBridge = rootId.IndexOf("Players", StringComparison.OrdinalIgnoreCase) >= 0;
        string primaryId = playersBridge ? "playerPager" : "pager";
        string fallbackId = playersBridge ? "pager" : "playerPager";

        XUiController scope = Parent;
        while (scope != null)
        {
            XUiController found = scope.GetChildById(primaryId);
            pager = found as XUiC_Paging;
            if (pager != null) return;

            found = scope.GetChildById(fallbackId);
            pager = found as XUiC_Paging;
            if (pager != null) return;

            scope = scope.Parent;
        }
    }

    private void ResolveRecipeList()
    {
        recipeList = null;
        if (!IsCraftingBridge) return;

        XUiController scope = Parent;
        while (scope != null)
        {
            XUiController found = scope.GetChildById("recipes");
            if (found != null)
            {
                recipeList = found;
                return;
            }
            scope = scope.Parent;
        }
    }

    private void ResolveCraftingTrackInput()
    {
        if (!IsCraftingBridge)
        {
            craftingTrackInput = null;
            return;
        }
        if (craftingTrackInput == null)
            craftingTrackInput = GetChildById("rebirthCraftingPagerScrollTrackInput");
    }

    private void WireCraftingTrackInput()
    {
        if (!IsCraftingBridge || craftingTrackInput == null) return;
        if (craftingTrackInput.ViewComponent != null)
        {
            craftingTrackInput.ViewComponent.EventOnPress = true;
            craftingTrackInput.ViewComponent.EventOnDrag = true;
            craftingTrackInput.ViewComponent.EventOnScroll = true;
        }
        if (!wiredScrollControllers.Contains(craftingTrackInput))
            WireScroll(craftingTrackInput);

        // Optional compatibility path for older overlays that still contain the transparent
        // track-input control. PC049 no longer depends on this control; raw pointer polling against
        // the visible Crafting scrollbar root is the authoritative fallback.
        craftingTrackInput.OnPress -= CraftingTrack_OnPress;
        craftingTrackInput.OnPress += CraftingTrack_OnPress;
        craftingTrackInput.OnDrag -= CraftingTrack_OnDrag;
        craftingTrackInput.OnDrag += CraftingTrack_OnDrag;
    }

    private void CraftingTrack_OnPress(XUiController sender, int mouseButton)
    {
        SetCraftingPageFromPointer(sender);
    }

    private void CraftingTrack_OnDrag(XUiController sender, EDragType dragType, Vector2 mousePositionDelta)
    {
        SetCraftingPageFromPointer(sender);
    }

    private void SetCraftingPageFromPointer(XUiController sender)
    {
        if (!IsCraftingBridge || pager == null || sender == null || sender.ViewComponent == null) return;
        int maxPage = Math.Max(0, pager.GetLastPage());
        if (maxPage <= 0) return;

        float normalized;
        if (!TryGetPointerNormalizedY(sender, out normalized)) return;
        int requested = Mathf.Clamp(Mathf.RoundToInt(normalized * maxPage), 0, maxPage);
        if (requested != pager.GetPage())
            NavigateCraftingPagerNative(requested, "track-event");
        lastPage = pager.GetPage();
        SyncFromPager(true);
    }

    private static bool TryGetPointerNormalizedY(XUiController controller, out float normalized)
    {
        normalized = 0f;
        if (controller == null || controller.ViewComponent == null || controller.ViewComponent.UiTransform == null) return false;
        Transform transform = controller.ViewComponent.UiTransform;
        Collider collider = transform.GetComponent<Collider>();
        if (collider == null)
        {
            Collider[] all = transform.GetComponentsInChildren<Collider>(true);
            if (all != null && all.Length > 0) collider = all[0];
        }
        Camera camera = UICamera.currentCamera;
        if (collider == null || camera == null) return false;

        Bounds bounds = collider.bounds;
        Vector3 a = camera.WorldToScreenPoint(bounds.min);
        Vector3 b = camera.WorldToScreenPoint(bounds.max);
        float top = Mathf.Max(a.y, b.y);
        float bottom = Mathf.Min(a.y, b.y);
        float height = top - bottom;
        if (height < 1f) return false;
        normalized = Mathf.Clamp01((top - Input.mousePosition.y) / height);
        return true;
    }

    private void RefreshCraftingScrollbarPeriodically()
    {
        if (!IsCraftingBridge || pager == null || craftingRawPointerDrag) return;

        // RecipeList/Paging population happens after category/filter input and can occur after this
        // controller's normal Update ordering. A lightweight forced sync every 150 ms makes the
        // native scrollbar thumb resize as soon as GetLastPage() changes, without waiting for the
        // first wheel event.
        if (Time.realtimeSinceStartup < nextCraftingForcedRefreshTime) return;
        nextCraftingForcedRefreshTime = Time.realtimeSinceStartup + 0.15f;
        SyncFromPager(true);
    }

    private void PollCraftingTrackRawInput()
    {
        if (!IsCraftingBridge || pager == null || ViewComponent == null || !ViewComponent.IsVisible)
            return;

        float left, right, bottom, top;
        if (!TryGetControllerScreenRect(this, out left, out right, out bottom, out top))
        {
            if (craftingRectFailureLogBudget > 0)
            {
                craftingRectFailureLogBudget--;
                if (RebirthLogSettings.CraftingUiLoggingEnabled) Log.Out("[REBIRTH RecipeScroll] TRACK_READY rect=<unresolved>"
                    + " viewSize=" + (ViewComponent != null ? ViewComponent.Size.ToString() : "<null>")
                    + " root=" + (ViewComponent != null ? ViewComponent.ID : "<null>"));
            }
            return;
        }

        if (!craftingReadyLogged)
        {
            craftingReadyLogged = true;
            if (RebirthLogSettings.CraftingUiLoggingEnabled) Log.Out("[REBIRTH RecipeScroll] TRACK_READY"
                + " root=" + (ViewComponent != null ? ViewComponent.ID : "<null>")
                + " rect=[" + left.ToString("0.0")
                + "," + bottom.ToString("0.0")
                + " -> " + right.ToString("0.0")
                + "," + top.ToString("0.0") + "]"
                + " viewSize=" + ViewComponent.Size
                + " maxPage=" + Math.Max(0, pager.GetLastPage()));
        }

        Vector2 mouse = (Vector2)Input.mousePosition;
        bool inside = mouse.x >= left && mouse.x <= right && mouse.y >= bottom && mouse.y <= top;

        if (Input.GetMouseButtonDown(0) && inside)
        {
            craftingRawPointerDrag = true;
            ApplyCraftingPageFromScreenY(mouse.y, bottom, top, "mouse-down");
        }
        else if (craftingRawPointerDrag && Input.GetMouseButton(0))
        {
            ApplyCraftingPageFromScreenY(mouse.y, bottom, top, "drag");
        }

        if (craftingRawPointerDrag && Input.GetMouseButtonUp(0))
        {
            ApplyCraftingPageFromScreenY(mouse.y, bottom, top, "mouse-up");
            craftingRawPointerDrag = false;
        }
    }

    private void ApplyCraftingPageFromScreenY(float mouseY, float bottom, float top, string reason)
    {
        int maxPage = Math.Max(0, pager.GetLastPage());
        float span = top - bottom;
        if (maxPage <= 0 || span < 1f) return;

        // Screen Y increases upward; pager page 0 is the top of the recipe list.
        float normalized = Mathf.Clamp01((top - mouseY) / span);
        int requested = Mathf.Clamp(Mathf.RoundToInt(normalized * maxPage), 0, maxPage);
        int before = pager.GetPage();

        if (requested != before)
        {
            NavigateCraftingPagerNative(requested, "raw-" + reason);
            lastPage = pager.GetPage();
            if (lastPage != before)
                craftingRawPageChangeCount++;
            SyncFromPager(true);
        }

        int after = pager.GetPage();
        if (craftingRawInputLogBudget > 0 && (reason != "drag" || requested != before))
        {
            craftingRawInputLogBudget--;
            if (RebirthLogSettings.CraftingUiLoggingEnabled) Log.Out("[REBIRTH RecipeScroll] TRACK_INPUT reason=" + reason
                + " before=" + before
                + " requested=" + requested
                + " after=" + after
                + " maxPage=" + maxPage
                + " normalized=" + normalized.ToString("0.###")
                + " screen=[" + leftSafe(bottom).ToString("0.0")
                + ".." + leftSafe(top).ToString("0.0") + "]"
                + " pageChanges=" + craftingRawPageChangeCount);
        }
    }

    private void NavigateCraftingPagerNative(int requested, string reason)
    {
        if (pager == null) return;

        int maxPage = Math.Max(0, pager.GetLastPage());
        requested = Mathf.Clamp(requested, 0, maxPage);
        int start = pager.GetPage();
        if (start == requested) return;

        ResolveCraftingPagingDirection();
        int safety = maxPage + 3;
        int steps = 0;

        while (pager.GetPage() != requested && safety-- > 0)
        {
            int before = pager.GetPage();
            bool increase = requested > before;

            if (craftingPageDownDirection == 0)
                ResolveCraftingPagingDirection();

            if (craftingPageDownDirection > 0)
            {
                if (increase) pager.PageDown();
                else pager.PageUp();
            }
            else if (craftingPageDownDirection < 0)
            {
                if (increase) pager.PageUp();
                else pager.PageDown();
            }
            else
            {
                // Last-resort compatibility fallback. This should be unnecessary once the
                // native direction has been observed, but avoids a dead scrollbar if a future
                // pager implementation suppresses both step methods at a boundary.
                pager.SetPage(requested);
            }

            int after = pager.GetPage();
            steps++;
            if (after == before)
            {
                // With a known direction and a non-boundary target, a native step should move.
                // Preserve a conservative SetPage fallback for future game revisions.
                pager.SetPage(requested);
                break;
            }
        }

        if (craftingRawInputLogBudget > 0)
        {
            if (RebirthLogSettings.CraftingUiLoggingEnabled) Log.Out("[REBIRTH RecipeScroll] NATIVE_NAV reason=" + reason
                + " start=" + start
                + " requested=" + requested
                + " final=" + pager.GetPage()
                + " pageDownDirection=" + craftingPageDownDirection
                + " steps=" + steps);
        }
    }

    private void ResolveCraftingPagingDirection()
    {
        if (pager == null || craftingPageDownDirection != 0) return;
        int maxPage = Math.Max(0, pager.GetLastPage());
        if (maxPage <= 0) return;

        int before = pager.GetPage();

        pager.PageDown();
        int afterDown = pager.GetPage();
        if (afterDown != before)
        {
            craftingPageDownDirection = afterDown > before ? 1 : -1;
            pager.PageUp();
            return;
        }

        pager.PageUp();
        int afterUp = pager.GetPage();
        if (afterUp != before)
        {
            // PageUp moves opposite PageDown.
            craftingPageDownDirection = afterUp > before ? -1 : 1;
            pager.PageDown();
        }
    }

    // Kept as a tiny helper so logging never propagates NaN/Infinity formatting surprises.
    private static float leftSafe(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }

    private static bool TryGetControllerScreenRect(
        XUiController controller,
        out float left,
        out float right,
        out float bottom,
        out float top)
    {
        left = right = bottom = top = 0f;
        if (controller == null || controller.ViewComponent == null || controller.ViewComponent.UiTransform == null)
            return false;

        Camera camera = UICamera.currentCamera;
        if (camera == null) return false;

        Transform transform = controller.ViewComponent.UiTransform;

        // Prefer a live collider if one exists (native/defaultscrollbar or authored control).
        Collider collider = transform.GetComponent<Collider>();

        // Only trust a collider owned by the bridge root itself. Child colliders belong to the
        // generated scrollbar thumb/track widgets and can cover only the red thumb, which is the
        // exact failure mode PC048 exhibited for clicks in the gray track.
        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            Vector3 a = camera.WorldToScreenPoint(bounds.min);
            Vector3 b = camera.WorldToScreenPoint(bounds.max);
            left = Mathf.Min(a.x, b.x);
            right = Mathf.Max(a.x, b.x);
            bottom = Mathf.Min(a.y, b.y);
            top = Mathf.Max(a.y, b.y);
            if (right - left >= 1f && top - bottom >= 1f)
                return true;
        }

        // No collider is required for the raw fallback. XUi rect transforms use top-left origin
        // with positive X right and negative local Y downward.
        Vector2i size = controller.ViewComponent.Size;
        Vector3 p0 = camera.WorldToScreenPoint(transform.position);
        Vector3 p1 = camera.WorldToScreenPoint(transform.TransformPoint(new Vector3(size.x, -size.y, 0f)));
        left = Mathf.Min(p0.x, p1.x);
        right = Mathf.Max(p0.x, p1.x);
        bottom = Mathf.Min(p0.y, p1.y);
        top = Mathf.Max(p0.y, p1.y);
        return right - left >= 1f && top - bottom >= 1f;
    }

    private void WireRecipeWheelSurface()
    {
        if(!IsCraftingBridge||recipeList==null||ReferenceEquals(wiredRecipeRoot,recipeList))return;
        // A RecipeList generation is wired once. If native UI recreation replaces the root, the
        // new reference invalidates this gate and the replacement tree is wired exactly once.
        wiredRecipeRoot=recipeList;WireScrollRecursive(recipeList);
    }

    private void WireScrollRecursive(XUiController controller)
    {
        if (controller == null) return;
        WireScroll(controller);
        if (controller.Children == null) return;
        for (int i = 0; i < controller.Children.Count; i++)
            WireScrollRecursive(controller.Children[i]);
    }

    private void SyncFromPager(bool force)
    {
        if (pager == null || proxy == null || proxy.ViewComponent == null) return;

        int maxPage = Math.Max(0, pager.GetLastPage());
        int previousMaxPage = lastMaxPage;
        bool rangeChanged = maxPage != lastMaxPage;
        lastMaxPage = maxPage;
        bool needed = maxPage > 0;

        if (IsCraftingBridge && rangeChanged)
        {
            if (RebirthLogSettings.CraftingUiLoggingEnabled) Log.Out("[REBIRTH RecipeScroll] RANGE oldMaxPage=" + previousMaxPage
                + " newMaxPage=" + maxPage
                + " page=" + pager.GetPage()
                + " viewportHeight=" + viewportHeight);
        }

        // Crafting must remain update-active while the native RecipeList populates. Other bridges
        // retain the original visibility behavior.
        if (ViewComponent != null)
            ViewComponent.IsVisible = IsCraftingBridge || needed;

        int contentHeight = needed
            ? Math.Max(viewportHeight + 1, viewportHeight * (maxPage + 1))
            : viewportHeight;

        bool sizeChanged = proxy.ViewComponent.Size.y != contentHeight;
        if (force || sizeChanged)
            proxy.ViewComponent.Size = new Vector2i(1, contentHeight);

        if (!needed)
        {
            lastPage = 0;
            if (IsCraftingBridge && (force || rangeChanged || sizeChanged))
                RebirthNativeScrollbarUtil.Refresh(scrollView);
            return;
        }

        int current = Mathf.Clamp(pager.GetPage(), 0, maxPage);
        bool pageChanged = current != lastPage;
        lastPage = current;

        // The old bridge only refreshed when the current page changed. On initial Crafting load,
        // maxPage changes from 0 to N while current remains 0, leaving the native scrollbar stale
        // and invisible until the first wheel event. Refresh when the range changes too.
        if (!force && !rangeChanged && !sizeChanged && !pageChanged)
            return;

        syncing=true;
        try
        {
            RebirthNativeScrollbarUtil.Refresh(scrollView);
            RebirthNativeScrollbarUtil.TrySetValue(scrollView,current/(float)maxPage);
            RebirthNativeScrollbarUtil.Refresh(scrollView);
            if(IsCraftingBridge&&rangeChanged)
            {
                float nativeValue,nativeBarSize;bool haveValue=RebirthNativeScrollbarUtil.TryGetValue(scrollView,out nativeValue);bool haveBarSize=RebirthNativeScrollbarUtil.TryGetBarSize(scrollView,out nativeBarSize);
                if(RebirthLogSettings.CraftingUiLoggingEnabled)Log.Out("[REBIRTH RecipeScroll] RANGE_APPLIED maxPage="+maxPage+" current="+current+" nativeValue="+(haveValue?nativeValue.ToString("0.###"):"<unavailable>")+" nativeBarSize="+(haveBarSize?nativeBarSize.ToString("0.###"):"<unavailable>"));
            }
        }
        finally{syncing=false;}
    }

    private void PollScrollbar()
    {
        if (syncing || pager == null) return;
        int maxPage = Math.Max(0, pager.GetLastPage());
        if (maxPage <= 0) return;
        float normalized;
        if (!RebirthNativeScrollbarUtil.TryGetValue(scrollView, out normalized)) return;
        int requested = Mathf.Clamp(Mathf.RoundToInt(normalized * maxPage), 0, maxPage);
        if (requested == pager.GetPage()) return;
        if (IsCraftingBridge)
        {
            NavigateCraftingPagerNative(requested, "native-scrollbar");
            lastPage = pager.GetPage();
            SyncFromPager(true);
        }
        else
        {
            pager.SetPage(requested);
            lastPage = pager.GetPage();
        }
    }

    private void Scroll(float delta)
    {
        if (pager == null) return;
        if (delta > 0f) pager.PageDown();
        else if (delta < 0f) pager.PageUp();
        SyncFromPager(true);
    }

    private void WireScroll(XUiController controller)
    {
        if (controller == null || wiredScrollControllers.Contains(controller)) return;
        wiredScrollControllers.Add(controller);
        if (controller.ViewComponent != null)
            controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += HandleScroll;
    }

    private void HandleScroll(XUiController sender, float delta)
    {
        if (Time.frameCount == lastWheelFrame) return;
        lastWheelFrame = Time.frameCount;
        Scroll(delta);
    }
}
