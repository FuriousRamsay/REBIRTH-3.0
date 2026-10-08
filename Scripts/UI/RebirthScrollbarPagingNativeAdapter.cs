using System;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable disable

/// <summary>Native paging integration and explicit custom-authority proxy exclusion.</summary>
internal static class RebirthScrollbarPagingNativeAdapter
{
    private sealed class Registration
    {
        internal UIScrollView[] Views;
        internal UIScrollBar[] Bars;
        internal bool SuppressMotion;
        internal int ChromeFrame = -1, AdmissionDepth;
        internal long ChromeRevision;
        internal bool LogicalOverflow;
        internal object ChromeOwner, ChromePlayerUI, ChromeManager;
        internal Func<bool> ChromeAdmission;
    }
    private sealed class Marker { internal int Owners, SuppressMotion; }
    private static readonly ConditionalWeakTable<XUiController, Registration> Registrations = new ConditionalWeakTable<XUiController, Registration>();
    private static readonly ConditionalWeakTable<UIScrollView, Marker> CustomViews = new ConditionalWeakTable<UIScrollView, Marker>();
    private static readonly ConditionalWeakTable<UIScrollBar, Marker> CustomBars = new ConditionalWeakTable<UIScrollBar, Marker>();

    internal static void RegisterCustomAuthority(XUiController proxy, bool suppressNativeMotion = false)
    {
        if (proxy?.ViewComponent?.UiTransform == null) return;
        UnregisterCustomAuthority(proxy);
        var transform = proxy.ViewComponent.UiTransform;
        var views = transform.GetComponentsInChildren<UIScrollView>(true);
        var bars = transform.GetComponentsInChildren<UIScrollBar>(true);
        var registration = new Registration { Views = views, Bars = bars, SuppressMotion = suppressNativeMotion };
        Registrations.Add(proxy, registration);
        foreach (var view in views) if (view != null) { var marker = CustomViews.GetValue(view, _ => new Marker()); marker.Owners++; if (suppressNativeMotion) marker.SuppressMotion++; }
        foreach (var bar in bars) if (bar != null) CustomBars.GetValue(bar, _ => new Marker()).Owners++;
    }

    internal static void UnregisterCustomAuthority(XUiController proxy)
    {
        Registration registration;
        if (proxy == null || !Registrations.TryGetValue(proxy, out registration)) return;
        Registrations.Remove(proxy);
        foreach (var view in registration.Views) { Marker marker; if (view != null && CustomViews.TryGetValue(view, out marker)) { if (registration.SuppressMotion) marker.SuppressMotion--; if (--marker.Owners <= 0) CustomViews.Remove(view); } }
        foreach (var bar in registration.Bars) { Marker marker; if (bar != null && CustomBars.TryGetValue(bar, out marker) && --marker.Owners <= 0) CustomBars.Remove(bar); }
    }

    internal static bool IsCustom(UIScrollView view) { Marker marker; return view != null && CustomViews.TryGetValue(view, out marker) && marker.Owners > 0; }
    internal static bool IsCustom(UIScrollBar bar) { Marker marker; return bar != null && CustomBars.TryGetValue(bar, out marker) && marker.Owners > 0; }
    internal static bool SuppressCustomMotion(UIScrollView view)
    {
        Marker marker;
        return RebirthScrollbarPagingPolicy.Enabled && view != null && CustomViews.TryGetValue(view, out marker) && marker.SuppressMotion > 0 && !Applying(view);
    }
    internal static void ResetCustomViewport(XUiController proxy)
    {
        Registration registration;
        if (!RebirthScrollbarPagingPolicy.Enabled || proxy == null || !Registrations.TryGetValue(proxy, out registration) || !registration.SuppressMotion) return;
        foreach (var view in registration.Views)
        {
            if (view == null || !ProvenOwner(view)) continue;
            ViewOwner owner;
            XUiV_ScrollView source;
            if (!ViewOwners.TryGetValue(view, out owner) || !owner.Source.TryGetTarget(out source) || source.UiTransform == null) continue;
            bool belongs = false;
            for (var transform = source.UiTransform; transform != null; transform = transform.parent) if (ReferenceEquals(transform, proxy.ViewComponent?.UiTransform)) { belongs = true; break; }
            if (!belongs) continue;
            var state = State(view);
            bool wasApplying = state.Applying;
            state.Applying = true;
            try { Stop(view); view.SetDragAmount(0f, 0f, false); }
            finally { state.Applying = wasApplying; }
        }
    }
    private sealed class NativeState
    {
        internal bool Mode, Applying, Dragging;
        internal float RawX, RawY, Width, Height, MaxX, MaxY;
        internal int AnalogDirection;
        internal float NextAnalog;
        internal WeakReference<XUiController> ChromeProxy;
    }
    private sealed class BarOwner { internal WeakReference<UIScrollView> View; }
    private sealed class ViewOwner { internal WeakReference<XUiV_ScrollView> Source; }
    private static readonly ConditionalWeakTable<UIScrollView, ViewOwner> ViewOwners = new ConditionalWeakTable<UIScrollView, ViewOwner>();
    private static readonly ConditionalWeakTable<UIScrollView, NativeState> States = new ConditionalWeakTable<UIScrollView, NativeState>();
    private static readonly ConditionalWeakTable<UIScrollBar, BarOwner> BarOwners = new ConditionalWeakTable<UIScrollBar, BarOwner>();
    private static readonly System.Reflection.FieldInfo ScrollField = HarmonyLib.AccessTools.Field(typeof(UIScrollView), "mScroll");
    internal struct Geometry
    {
        internal float Width, Height, MaxX, MaxY, X, Y, Left, Top;
        internal UIPanel Panel;
    }
    internal static void RegisterNativeOwner(XUiV_ScrollView source)
    {
        if (source?.scrollView == null || source.Controller == null || !ReferenceEquals(source.Controller.ViewComponent, source)) return;
        var owner = ViewOwners.GetValue(source.scrollView, _ => new ViewOwner());
        XUiV_ScrollView previous;
        if (owner.Source == null || !owner.Source.TryGetTarget(out previous) || !ReferenceEquals(previous, source))
            owner.Source = new WeakReference<XUiV_ScrollView>(source);
        RememberBars(source.scrollView);
    }
    internal static bool ProvenOwner(UIScrollView view)
    {
        ViewOwner owner;
        XUiV_ScrollView source;
        return view != null && ViewOwners.TryGetValue(view, out owner) && owner.Source != null && owner.Source.TryGetTarget(out source) &&
            source != null && ReferenceEquals(source.scrollView, view) && source.Controller != null && ReferenceEquals(source.Controller.ViewComponent, source);
    }
    internal static bool Active(UIScrollView view) => RebirthScrollbarPagingPolicy.Enabled && view != null && !IsCustom(view) && ProvenOwner(view);
    internal static bool Applying(UIScrollView view) { NativeState state; return view != null && States.TryGetValue(view, out state) && state.Applying; }
    private static NativeState State(UIScrollView view) => States.GetValue(view, _ => new NativeState());
    internal static bool GeometryFor(UIScrollView view, out Geometry geometry)
    {
        geometry = default(Geometry);
        if (view == null || view.panel == null || !ProvenOwner(view)) return false;
        var panel = view.panel;
        var clip = panel.finalClipRegion;
        var bounds = view.bounds;
        if (!ProvenOwner(view) || !ReferenceEquals(view.panel, panel)) return false;
        geometry.Panel = panel;
        var softness = panel.clipping == UIDrawCall.Clipping.SoftClip ? panel.clipSoftness : Vector2.zero;
        geometry.Width = Mathf.Max(1f, clip.z - 2f * softness.x);
        geometry.Height = Mathf.Max(1f, clip.w - 2f * softness.y);
        geometry.MaxX = Mathf.Max(0f, bounds.size.x - geometry.Width);
        geometry.MaxY = Mathf.Max(0f, bounds.size.y - geometry.Height);
        geometry.Left = bounds.min.x + geometry.Width * .5f;
        geometry.Top = bounds.max.y - geometry.Height * .5f;
        geometry.X = Mathf.Clamp(clip.x - geometry.Left, 0f, geometry.MaxX);
        geometry.Y = Mathf.Clamp(geometry.Top - clip.y, 0f, geometry.MaxY);
        RememberBars(view);
        return true;
    }
    private static void RememberBars(UIScrollView view)
    {
        RememberBar(view.horizontalScrollBar as UIScrollBar, view);
        RememberBar(view.verticalScrollBar as UIScrollBar, view);
    }
    private static void RememberBar(UIScrollBar bar, UIScrollView view)
    {
        if (bar == null) return;
        var owner = BarOwners.GetValue(bar, _ => new BarOwner());
        UIScrollView previous;
        if (owner.View == null || !owner.View.TryGetTarget(out previous) || !ReferenceEquals(previous, view))
            owner.View = new WeakReference<UIScrollView>(view);
    }
    internal static bool OwnedBar(UIScrollBar bar, out UIScrollView view)
    {
        view = null;
        BarOwner owner;
        return bar != null && !IsCustom(bar) && BarOwners.TryGetValue(bar, out owner) && owner.View != null && owner.View.TryGetTarget(out view) &&
            view != null && ProvenOwner(view) && !IsCustom(view) && (ReferenceEquals(view.horizontalScrollBar, bar) || ReferenceEquals(view.verticalScrollBar, bar));
    }
    internal static void Stop(UIScrollView view)
    {
        view.DisableSpring();
        view.currentMomentum = Vector3.zero;
        ScrollField.SetValue(view, 0f);
    }
    private static void Apply(UIScrollView view, Geometry geometry, float x, float y)
    {
        if (!Active(view) || !ReferenceEquals(view.panel, geometry.Panel)) return;
        var state = State(view);
        if (state.Applying) return;
        state.Applying = true;
        try
        {
            Stop(view);
            if (!Active(view) || !ReferenceEquals(view.panel, geometry.Panel)) return;
            view.SetDragAmount(geometry.MaxX > 0f ? x / geometry.MaxX : 0f,
                geometry.MaxY > 0f ? y / geometry.MaxY : 0f, false);
            if (!Active(view) || !ReferenceEquals(view.panel, geometry.Panel)) return;
            view.UpdateScrollbars(false);
        }
        finally { state.Applying = false; }
    }
    internal static void Rebase(UIScrollView view)
    {
        NativeState state;
        if (!Active(view)) { if (view != null && States.TryGetValue(view, out state)) { state.Mode = false; state.AnalogDirection = 0; } return; }
        state = State(view);
        if (state.Applying) return;
        Geometry geometry;
        if (!GeometryFor(view, out geometry)) return;
        float x = RebirthScrollbarPagingPolicy.SnapAbsolute(geometry.X, geometry.MaxX, geometry.Width, true);
        float y = RebirthScrollbarPagingPolicy.SnapAbsolute(geometry.Y, geometry.MaxY, geometry.Height, true);
        bool changed = !state.Mode || state.Width != geometry.Width || state.Height != geometry.Height || state.MaxX != geometry.MaxX || state.MaxY != geometry.MaxY;
        if (changed) { state.RawX = x; state.RawY = y; state.Width = geometry.Width; state.Height = geometry.Height; state.MaxX = geometry.MaxX; state.MaxY = geometry.MaxY; state.Mode = true; }
        if ((view.canMoveHorizontally && Mathf.Abs(x - geometry.X) > .01f) || (view.canMoveVertically && Mathf.Abs(y - geometry.Y) > .01f)) Apply(view, geometry, x, y);
    }
    internal static void Page(UIScrollView view, float horizontal, float vertical, bool analog)
    {
        if (!Active(view) || !view.enabled || !NGUITools.GetActive(view.gameObject)) return;
        Rebase(view);
        Geometry geometry;
        if (!GeometryFor(view, out geometry)) return;
        var state = State(view);
        float axis = view.canMoveVertically ? vertical : horizontal;
        int direction = axis > 0f ? 1 : axis < 0f ? -1 : 0;
        if (analog)
        {
            if (direction == 0) { state.AnalogDirection = 0; return; }
            float now = Time.unscaledTime;
            if (state.AnalogDirection == direction && now < state.NextAnalog) return;
            state.NextAnalog = now + (state.AnalogDirection == direction ? .16f : .35f);
            state.AnalogDirection = direction;
        }
        float x = geometry.X, y = geometry.Y;
        if (view.canMoveHorizontally && horizontal != 0f) x = RebirthScrollbarPagingPolicy.Step(x, geometry.MaxX, geometry.Width, horizontal > 0f ? 1 : -1);
        if (view.canMoveVertically && vertical != 0f) y = RebirthScrollbarPagingPolicy.Step(y, geometry.MaxY, geometry.Height, vertical > 0f ? 1 : -1);
        state.RawX = x; state.RawY = y;
        Apply(view, geometry, x, y);
    }
    internal static void Press(UIScrollView view, bool pressed)
    {
        if (!Active(view) || UICamera.currentScheme == UICamera.ControlScheme.Controller) return;
        Rebase(view);
        Geometry geometry;
        if (!GeometryFor(view, out geometry)) return;
        var state = State(view);
        state.Dragging = pressed; state.RawX = geometry.X; state.RawY = geometry.Y;
    }
    internal static void Relative(UIScrollView view, Vector3 relative)
    {
        Rebase(view);
        Geometry geometry;
        if (!GeometryFor(view, out geometry)) return;
        var state = State(view);
        if (!state.Dragging) { state.RawX = geometry.X; state.RawY = geometry.Y; }
        state.RawX = Mathf.Clamp(state.RawX - relative.x, 0f, geometry.MaxX);
        state.RawY = Mathf.Clamp(state.RawY + relative.y, 0f, geometry.MaxY);
        Apply(view, geometry, RebirthScrollbarPagingPolicy.SnapAbsolute(state.RawX, geometry.MaxX, geometry.Width, true),
            RebirthScrollbarPagingPolicy.SnapAbsolute(state.RawY, geometry.MaxY, geometry.Height, true));
    }
    internal static void SnapArguments(UIScrollView view, ref float x, ref float y)
    {
        Geometry geometry;
        if (!GeometryFor(view, out geometry)) return;
        if (view.canMoveHorizontally) x = geometry.MaxX > 0f ? RebirthScrollbarPagingPolicy.SnapAbsolute(x * geometry.MaxX, geometry.MaxX, geometry.Width, true) / geometry.MaxX : 0f;
        if (view.canMoveVertically) y = geometry.MaxY > 0f ? RebirthScrollbarPagingPolicy.SnapAbsolute(y * geometry.MaxY, geometry.MaxY, geometry.Height, true) / geometry.MaxY : 0f;
    }
    internal static bool SnapBar(UIScrollBar bar, ref float value)
    {
        UIScrollView view;
        Geometry geometry;
        if (!RebirthScrollbarPagingPolicy.Enabled || !OwnedBar(bar, out view) || Applying(view) || !GeometryFor(view, out geometry)) return false;
        bool horizontal = ReferenceEquals(view.horizontalScrollBar, bar);
        float maximum = horizontal ? geometry.MaxX : geometry.MaxY;
        float step = horizontal ? geometry.Width : geometry.Height;
        value = maximum > 0f ? RebirthScrollbarPagingPolicy.SnapAbsolute(value * maximum, maximum, step, true) / maximum : 0f;
        return true;
    }
    internal static bool PanBar(UIScrollBar bar, Vector2 delta)
    {
        UIScrollView view;
        if (!RebirthScrollbarPagingPolicy.Enabled || !OwnedBar(bar, out view)) return false;
        if (!bar.enabled || !bar.isColliderEnabled) return true;
        bool horizontal = ReferenceEquals(view.horizontalScrollBar, bar);
        float axis = horizontal ? delta.x : -delta.y;
        if (bar.fillDirection == UIProgressBar.FillDirection.RightToLeft || bar.fillDirection == UIProgressBar.FillDirection.BottomToTop) axis = -axis;
        Page(view, horizontal ? axis : 0f, horizontal ? 0f : axis, true);
        return true;
    }
    internal static bool TryEnsureVisible(XUiV_ScrollView source, System.Collections.Generic.IList<Vector3> corners)
    {
        var view = source?.scrollView;
        Geometry geometry;
        if (corners == null || corners.Count < 4 || !Active(view) || source.Controller == null ||
            !ReferenceEquals(source.Controller.ViewComponent, source) || !GeometryFor(view, out geometry) ||
            !ReferenceEquals(source.scrollView, view) || !Active(view)) return false;
        // Native fallback is safe only before Rebase can mutate the viewport.
        Rebase(view);
        if (!ReferenceEquals(source.scrollView, view) || !Active(view) || !GeometryFor(view, out geometry)) return true;
        var transform = geometry.Panel.cachedTransform;
        var first = transform.InverseTransformPoint(corners[1]);
        var last = transform.InverseTransformPoint(corners[3]);
        var bounds = view.bounds;
        if (!ReferenceEquals(source.scrollView, view) || !Active(view) ||
            !ReferenceEquals(source.Controller?.ViewComponent, source) || !ReferenceEquals(view.panel, geometry.Panel)) return true;
        float x = RebirthScrollbarPagingPolicy.EnsureVisible(geometry.X, first.x - bounds.min.x, last.x - bounds.min.x,
            geometry.MaxX, geometry.Width, geometry.Width);
        float y = RebirthScrollbarPagingPolicy.EnsureVisible(geometry.Y, bounds.max.y - first.y, bounds.max.y - last.y,
            geometry.MaxY, geometry.Height, geometry.Height);
        Apply(view, geometry, x, y);
        return true;    }
    internal struct ChromeFrameState
    {
        internal object Registration;
        internal XUiController Proxy;
        internal UIScrollBar Vertical, Horizontal;
        internal float VerticalAlpha, HorizontalAlpha, Delta;
        internal long Revision;
        internal bool ShowVertical, ShowHorizontal;
    }
    private static bool Contains<T>(T[] values, T value) where T : class
    {
        foreach (var item in values) if (ReferenceEquals(item, value)) return true;
        return false;
    }
    private static void RevokeChrome(Registration registration)
    {
        registration.ChromeFrame = -1; registration.LogicalOverflow = false; registration.ChromeAdmission = null;
        registration.ChromeOwner = registration.ChromePlayerUI = registration.ChromeManager = null;
        registration.ChromeRevision++;
    }
    internal static void SetCustomLogicalScrollbarVisibility(XUiController proxy, bool overflow, Func<bool> currentScope = null)
    {
        Registration registration;
        if (proxy == null || !Registrations.TryGetValue(proxy, out registration) || !registration.SuppressMotion) return;
        if (!RebirthScrollbarPagingPolicy.Enabled || (overflow && currentScope == null) || proxy.xui?.playerUI?.windowManager == null)
        { RevokeChrome(registration); return; }
        registration.ChromeOwner = proxy.xui; registration.ChromePlayerUI = proxy.xui.playerUI;
        registration.ChromeManager = proxy.xui.playerUI.windowManager;
        registration.ChromeAdmission = currentScope; registration.LogicalOverflow = overflow;
        registration.ChromeFrame = Time.frameCount; registration.ChromeRevision++;
        foreach (var view in registration.Views)
        {
            if (view == null) continue;
            var state = State(view);
            XUiController previous;
            if (state.ChromeProxy == null || !state.ChromeProxy.TryGetTarget(out previous) || !ReferenceEquals(previous, proxy))
                state.ChromeProxy = new WeakReference<XUiController>(proxy);
        }
    }
    private static bool ChromeShape(XUiController proxy, Registration registration, UIScrollView view, long revision)
    {
        Registration current;
        var owner = proxy?.xui;
        return RebirthScrollbarPagingPolicy.Enabled && proxy != null && Registrations.TryGetValue(proxy, out current) && ReferenceEquals(current, registration) &&
            registration.SuppressMotion && registration.ChromeFrame == Time.frameCount && registration.ChromeRevision == revision &&
            Contains(registration.Views, view) && ProvenOwner(view) && SuppressCustomMotion(view) &&
            ReferenceEquals(owner, registration.ChromeOwner) && ReferenceEquals(owner?.playerUI, registration.ChromePlayerUI) &&
            ReferenceEquals(owner?.playerUI?.windowManager, registration.ChromeManager) && proxy.ViewComponent?.UiTransform != null &&
            NGUITools.GetActive(proxy.ViewComponent.UiTransform.gameObject) && view.enabled && NGUITools.GetActive(view.gameObject);
    }
    private static bool ChromeCurrent(XUiController proxy, Registration registration, UIScrollView view, long revision)
    {
        if (!ChromeShape(proxy, registration, view, revision) || registration.AdmissionDepth != 0) return false;
        for (var node = proxy; node != null; node = node.Parent)
            if (node.ViewComponent != null && !node.ViewComponent.IsVisible) return false;
        var admission = registration.ChromeAdmission;
        if (registration.LogicalOverflow && admission == null) return false;
        registration.AdmissionDepth++;
        bool admitted;
        try { admitted = admission == null || admission(); }
        catch (Exception) { admitted = false; }
        finally { registration.AdmissionDepth--; }
        return admitted && ChromeShape(proxy, registration, view, revision);
    }
    internal static ChromeFrameState CaptureChrome(UIScrollView view, int dragId, Vector3 momentum)
    {
        var result = default(ChromeFrameState);
        NativeState state; XUiController proxy; Registration registration;
        if (view == null || !States.TryGetValue(view, out state) || state.ChromeProxy == null || !state.ChromeProxy.TryGetTarget(out proxy) ||
            !Registrations.TryGetValue(proxy, out registration)) return result;
        long revision = registration.ChromeRevision;
        if (!ChromeCurrent(proxy, registration, view, revision)) { if (registration.ChromeRevision == revision) RevokeChrome(registration); return result; }
        if (view.showScrollBars == UIScrollView.ShowCondition.Always) return result; // Installed native branch skips alpha changes for Always.
        bool gesture = view.showScrollBars != UIScrollView.ShowCondition.WhenDragging || dragId != -10 || momentum.magnitude > .01f;
        result.Registration = registration; result.Proxy = proxy; result.Revision = revision; result.Delta = RealTime.deltaTime;
        var vertical = view.verticalScrollBar as UIScrollBar; var horizontal = view.horizontalScrollBar as UIScrollBar;
        if (vertical != null && Contains(registration.Bars, vertical) && vertical.enabled && NGUITools.GetActive(vertical.gameObject))
        { result.Vertical = vertical; result.VerticalAlpha = vertical.alpha; result.ShowVertical = gesture && registration.LogicalOverflow && view.canMoveVertically; }
        return result;
    }
    internal static void RestoreChrome(UIScrollView view, ChromeFrameState state)
    {
        var registration = state.Registration as Registration;
        if (registration == null) return;
        if (!ChromeCurrent(state.Proxy, registration, view, state.Revision)) { if (registration.ChromeRevision == state.Revision) RevokeChrome(registration); return; }
        if (state.Vertical != null && ReferenceEquals(view.verticalScrollBar, state.Vertical) && Contains(registration.Bars, state.Vertical) &&
            state.Vertical.enabled && NGUITools.GetActive(state.Vertical.gameObject))
            state.Vertical.alpha = Mathf.Clamp01(state.VerticalAlpha + (state.ShowVertical ? state.Delta * 6f : -state.Delta * 3f));
        if (!ChromeCurrent(state.Proxy, registration, view, state.Revision)) { if (registration.ChromeRevision == state.Revision) RevokeChrome(registration); return; }
        if (state.Horizontal != null && ReferenceEquals(view.horizontalScrollBar, state.Horizontal) && Contains(registration.Bars, state.Horizontal) &&
            state.Horizontal.enabled && NGUITools.GetActive(state.Horizontal.gameObject))
            state.Horizontal.alpha = Mathf.Clamp01(state.HorizontalAlpha + (state.ShowHorizontal ? state.Delta * 6f : -state.Delta * 3f));
    }}








