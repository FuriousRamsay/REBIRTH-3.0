using System;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Rebirth-only layout owner for the native player BuffPopoutList.
/// It never creates, removes, reorders, or handles clicks for buffs. Native XUiC_BuffPopoutList
/// remains the content owner; this controller only supplies yOffset and measured safe-capacity input.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthHudBuffLayout : XUiController
{
    private const float RefreshStep = 0.10f;

    private float refreshClock;
    private bool dirty = true;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private float lastReservedPixels = -1f;
    private XUiC_BuffPopoutList lastList;
    private int lastYOffset = int.MinValue;
    private float lastTrackerHeight = -1f;
    private float lastScaleY = -1f;
    private bool subscribed;

    public override void Init()
    {
        base.Init();
        RebirthCompactBuffLayout.Install();
        Subscribe();
        dirty = true;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        Subscribe();
        dirty = true;
    }

    public override void OnClose()
    {
        ResetNativeOffset();
        Unsubscribe();
        base.OnClose();
    }

    public override void Cleanup()
    {
        ResetNativeOffset();
        Unsubscribe();
        base.Cleanup();
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        refreshClock += Math.Max(0f, dt);
        if (!dirty && refreshClock < RefreshStep) return;
        refreshClock = 0f;
        ApplyLayout();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        RebirthHudTrackingLayoutMetrics.TrackerFootprintChanged += OnTrackerLayoutChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        RebirthHudTrackingLayoutMetrics.TrackerFootprintChanged -= OnTrackerLayoutChanged;
        subscribed = false;
    }

    private void OnTrackerLayoutChanged()
    {
        dirty = true;

    }

    private void ApplyLayout()
    {
        XUiC_BuffPopoutList buffs = xui != null ? xui.BuffPopoutList : null;
        if (buffs == null)
        {
            dirty = true;
            return;
        }

        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth)
        {
            if (lastYOffset != 0) buffs.SetYOffset(0);
            lastYOffset = 0;
            RebirthHudTrackingLayoutMetrics.ClearMeasuredGeometry();
            dirty = false;
            return;
        }

        float reservedPixels = ((RebirthCompactBuffLayout.CountActive(buffs) + 1) / 2) * RebirthHudTrackingLayoutMetrics.CompactBuffRowPitch
            + 28f + (xui.playerUI?.entityPlayer?.IsCrouching == true ? 34f : 0f);
        float trackerHeight = RebirthHudTrackingLayoutMetrics.RenderedLowerHudHeight;
        float scaleY = 1f;
        try
        {
            if (xui != null && xui.transform != null)
                scaleY = Math.Abs(xui.transform.lossyScale.y);
        }
        catch { scaleY = 1f; }
        if (scaleY < 0.01f) scaleY = 1f;

        bool geometryChanged = dirty || lastScreenWidth != Screen.width || lastScreenHeight != Screen.height ||
            !ReferenceEquals(lastList, buffs) || Math.Abs(lastReservedPixels - reservedPixels) > 0.01f || Math.Abs(lastTrackerHeight - trackerHeight) > 0.01f || Math.Abs(lastScaleY - scaleY) > 0.001f;
        if (!geometryChanged)
        {
            // The native list can be replaced or reset during loading while our
            // cached geometry stays identical. Its setter checks the actual offset.

            return;
        }

        RebirthHudTrackingLayoutMetrics.ReportMeasuredHudEnvelopePixels(Screen.width, Screen.height, scaleY, reservedPixels);
        // Reporting only schedules the tracker. Buff geometry is applied once by its native
        // Update postfix, using the most recently completed tracker footprint.
        trackerHeight = RebirthHudTrackingLayoutMetrics.RenderedLowerHudHeight;

        int yOffset = RebirthHudTrackingLayoutMetrics.CalculateBuffYOffset(trackerHeight);

        lastYOffset = yOffset;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastReservedPixels = reservedPixels; lastList = buffs;
        lastTrackerHeight = trackerHeight;
        lastScaleY = scaleY;
        dirty = false;
    }

    private void ResetNativeOffset()
    {
        try
        {
            XUiC_BuffPopoutList buffs = xui != null ? xui.BuffPopoutList : null;
            if (buffs != null && lastYOffset != 0) buffs.SetYOffset(0);
        }
        catch { }
        lastYOffset = 0;
        dirty = true;
    }
}
