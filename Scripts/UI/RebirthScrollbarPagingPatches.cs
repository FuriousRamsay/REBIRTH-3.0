using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;

#nullable disable

internal static partial class RebirthScrollbarPagingInstaller
{
    private static Harmony Harmony;
    internal static void Install()
    {
        if (attempted) return;
        attempted = true;
        Available = false;
        if (!ReleaseEnabled) { UnavailableReason = "Paging deferred for this release."; return; }
        try
        {
        Harmony = new Harmony("rebirth.scrollbar.paging");
        Preflight();
        foreach (var type in new[] {
            typeof(RebirthScrollbarNativeWheelPatch), typeof(RebirthScrollbarNativePanPatch),
            typeof(RebirthScrollbarNativeDragAmountPatch), typeof(RebirthScrollbarNativeRelativeMovePatch),
            typeof(RebirthScrollbarNativePressPatch), typeof(RebirthScrollbarNativeFramePatch),
            typeof(RebirthScrollbarNativeBoundsPatch), typeof(RebirthScrollbarNativeAbsoluteBarPatch),
            typeof(RebirthScrollbarNativeBarPanPatch), typeof(RebirthScrollbarNativeBarFramePatch),
            typeof(RebirthScrollbarNativeBarConnectPatch), typeof(RebirthScrollbarNativeEnsureVisiblePatch),
            typeof(RebirthScrollbarNativeControllerScrollPatch), typeof(RebirthScrollbarNativeOwnerLoadPatch),
            typeof(RebirthScrollbarNativeListChangePatch), typeof(RebirthScrollbarNativeListAbsolutePatch),
            typeof(RebirthScrollbarNativeListForcePatch), typeof(RebirthScrollbarNativeListUpdatePatch) })
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, type);
        Available = true;
        UnavailableReason = null;
        }
        catch (Exception failure)
        {
            Available = false;
            UnavailableReason = failure.GetType().Name + ": " + failure.Message;
            try { Harmony.UnpatchSelf(); }
            catch (Exception rollbackFailure) { Log.Error("[RebirthScrollbarPaging] Patch removal failed; residual paging callbacks remain disabled: " + rollbackFailure); }
            Log.Error("[RebirthScrollbarPaging] Complete coverage unavailable; Smooth behavior retained: " + failure);
        }
    }
}

[HarmonyPatch(typeof(UIScrollView), "Scroll", new[] { typeof(float) })]
internal static class RebirthScrollbarNativeWheelPatch
{
    private static bool Prefix(UIScrollView __instance, float delta)
    {
        if (RebirthScrollbarPagingNativeAdapter.SuppressCustomMotion(__instance)) return false;
        if (!RebirthScrollbarPagingNativeAdapter.Active(__instance)) return true;
        if (__instance.scrollWheelFactor == 0f) return false;
        RebirthScrollbarPagingNativeAdapter.Page(__instance, delta * Mathf.Sign(__instance.scrollWheelFactor), -delta * Mathf.Sign(__instance.scrollWheelFactor), false);
        return false;
    }
}
[HarmonyPatch(typeof(UIScrollView), "OnPan", new[] { typeof(Vector2) })]
internal static class RebirthScrollbarNativePanPatch
{
    private static bool Prefix(UIScrollView __instance, Vector2 delta)
    {
        if (RebirthScrollbarPagingNativeAdapter.SuppressCustomMotion(__instance)) return false;
        if (!RebirthScrollbarPagingNativeAdapter.Active(__instance)) return true;
        RebirthScrollbarPagingNativeAdapter.Page(__instance, delta.x, -delta.y, true);
        return false;
    }
}
[HarmonyPatch(typeof(UIScrollView), "SetDragAmount", new[] { typeof(float), typeof(float), typeof(bool) })]
internal static class RebirthScrollbarNativeDragAmountPatch
{
    private static bool Prefix(UIScrollView __instance, ref float x, ref float y)
    {
        if (RebirthScrollbarPagingNativeAdapter.SuppressCustomMotion(__instance)) return false;
        if (RebirthScrollbarPagingNativeAdapter.Active(__instance) && !RebirthScrollbarPagingNativeAdapter.Applying(__instance))
            RebirthScrollbarPagingNativeAdapter.SnapArguments(__instance, ref x, ref y);
        return true;
    }
    private static void Postfix(UIScrollView __instance) { RebirthScrollbarPagingNativeAdapter.Rebase(__instance); }
}
[HarmonyPatch(typeof(UIScrollView), "MoveRelative", new[] { typeof(Vector3) })]
internal static class RebirthScrollbarNativeRelativeMovePatch
{
    private static bool Prefix(UIScrollView __instance, Vector3 relative)
    {
        if (RebirthScrollbarPagingNativeAdapter.SuppressCustomMotion(__instance)) return false;
        if (!RebirthScrollbarPagingNativeAdapter.Active(__instance) || RebirthScrollbarPagingNativeAdapter.Applying(__instance)) return true;
        RebirthScrollbarPagingNativeAdapter.Relative(__instance, relative);
        return false;
    }
}
[HarmonyPatch(typeof(UIScrollView), "Press", new[] { typeof(bool) })]
internal static class RebirthScrollbarNativePressPatch
{
    private static void Postfix(UIScrollView __instance, bool pressed) { RebirthScrollbarPagingNativeAdapter.Press(__instance, pressed); }
}
[HarmonyPatch(typeof(UIScrollView), "LateUpdate")]
internal static class RebirthScrollbarNativeFramePatch
{
    private static void Prefix(UIScrollView __instance, ref Vector3 ___mMomentum, ref float ___mScroll, int ___mDragID, out RebirthScrollbarPagingNativeAdapter.ChromeFrameState __state)
    {
        __state = RebirthScrollbarPagingNativeAdapter.CaptureChrome(__instance, ___mDragID, ___mMomentum);
        if (RebirthScrollbarPagingNativeAdapter.SuppressCustomMotion(__instance)) { ___mMomentum = Vector3.zero; ___mScroll = 0f; __instance.DisableSpring(); return; }
        if (!RebirthScrollbarPagingNativeAdapter.Active(__instance)) { RebirthScrollbarPagingNativeAdapter.Rebase(__instance); return; }
        ___mMomentum = Vector3.zero; ___mScroll = 0f;
        __instance.DisableSpring();
        RebirthScrollbarPagingNativeAdapter.Rebase(__instance);
    }
    private static void Postfix(UIScrollView __instance, RebirthScrollbarPagingNativeAdapter.ChromeFrameState __state) { RebirthScrollbarPagingNativeAdapter.Rebase(__instance); RebirthScrollbarPagingNativeAdapter.RestoreChrome(__instance, __state); }
}
[HarmonyPatch(typeof(UIScrollView), "UpdateScrollbars", new[] { typeof(bool) })]
internal static class RebirthScrollbarNativeBoundsPatch
{
    private static bool Prefix(UIScrollView __instance) => !RebirthScrollbarPagingNativeAdapter.SuppressCustomMotion(__instance);
    private static void Postfix(UIScrollView __instance) { RebirthScrollbarPagingNativeAdapter.Rebase(__instance); }
}
[HarmonyPatch(typeof(UIProgressBar), "Set", new[] { typeof(float), typeof(bool) })]
internal static class RebirthScrollbarNativeAbsoluteBarPatch
{
    private static void Prefix(UIProgressBar __instance, ref float val)
    {
        var bar = __instance as UIScrollBar;
        if (bar != null) RebirthScrollbarPagingNativeAdapter.SnapBar(bar, ref val);
    }
}
[HarmonyPatch(typeof(UISlider), "OnPan", new[] { typeof(Vector2) })]
internal static class RebirthScrollbarNativeBarPanPatch
{
    private static bool Prefix(UISlider __instance, Vector2 delta)
    {
        var bar = __instance as UIScrollBar;
        return bar == null || !RebirthScrollbarPagingNativeAdapter.PanBar(bar, delta);
    }
}
[HarmonyPatch(typeof(UIProgressBar), "Update")]
internal static class RebirthScrollbarNativeBarFramePatch
{
    private static void Postfix(UIProgressBar __instance)
    {
        var bar = __instance as UIScrollBar;
        UIScrollView view;
        if (bar != null && RebirthScrollbarPagingNativeAdapter.OwnedBar(bar, out view)) RebirthScrollbarPagingNativeAdapter.Rebase(view);
    }
}
[HarmonyPatch(typeof(XUiV_ScrollBar), "Connect", new[] { typeof(XUiEvent_OnScrollEventHandler), typeof(EventDelegate.Callback) })]
internal static class RebirthScrollbarNativeBarConnectPatch
{
    private static void Postfix(XUiV_ScrollBar __instance, XUiEvent_OnScrollEventHandler _onScrolled)
    {
        var owner = _onScrolled?.Target as XUiV_ScrollView;
        RebirthScrollbarPagingNativeAdapter.Geometry geometry;
        if (owner?.scrollView != null) { RebirthScrollbarPagingNativeAdapter.RegisterNativeOwner(owner); RebirthScrollbarPagingNativeAdapter.GeometryFor(owner.scrollView, out geometry); }
    }
}
[HarmonyPatch(typeof(XUiV_ScrollView), "MakeVisible", new[] { typeof(IList<Vector3>) })]
internal static class RebirthScrollbarNativeEnsureVisiblePatch
{
    private static bool Prefix(XUiV_ScrollView __instance, IList<Vector3> _worldCorners)
    {
        if (RebirthScrollbarPagingNativeAdapter.SuppressCustomMotion(__instance.scrollView)) return false;
        if (!RebirthScrollbarPagingNativeAdapter.Active(__instance.scrollView)) return true;
        return !RebirthScrollbarPagingNativeAdapter.TryEnsureVisible(__instance, _worldCorners);
    }
}
[HarmonyPatch(typeof(XUiV_ScrollView), "controllerScroll", new[] { typeof(float) })]
internal static class RebirthScrollbarNativeControllerScrollPatch
{
    private static bool Prefix(XUiV_ScrollView __instance)
    {
        var view = __instance.scrollView;
        if (RebirthScrollbarPagingNativeAdapter.SuppressCustomMotion(view)) return false;
        if (!RebirthScrollbarPagingNativeAdapter.Active(view)) return true;
        if (view.scrollWheelFactor == 0f) return false;
        if (!XUiUtils.HotkeysAllowedFor(__instance.Controller.Parent.ViewComponent)) return false;
        var currentTarget = __instance.xui.playerUI.CursorController.CurrentTarget;
        if (!__instance.ScrollWithoutFocus && (currentTarget == null || !currentTarget.Controller.IsSelfOrChildOf(__instance.Controller.Parent))) return false;
        Vector2 vector = __instance.xui.playerUI.playerInput.GUIActions.Camera.Vector;
        float x = Mathf.Abs(vector.x), y = Mathf.Abs(vector.y);
        if (__instance.Movement == UIScrollView.Movement.Vertical)
            RebirthScrollbarPagingNativeAdapter.Page(view, 0f, y >= x * 1.8f && y > .1f ? -vector.y * Mathf.Sign(view.scrollWheelFactor) : 0f, true);
        else RebirthScrollbarPagingNativeAdapter.Page(view, x >= y * 1.8f && x > .1f ? vector.x * Mathf.Sign(view.scrollWheelFactor) : 0f, 0f, true);
        return false;
    }
}



[HarmonyPatch(typeof(XUiV_ScrollView), "OnXuiLoadDone")]
internal static class RebirthScrollbarNativeOwnerLoadPatch
{
    private static void Postfix(XUiV_ScrollView __instance) { RebirthScrollbarPagingNativeAdapter.RegisterNativeOwner(__instance); }
}




