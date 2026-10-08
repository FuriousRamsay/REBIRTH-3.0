using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Shared safe-capacity and rendered-height contract for HUD Tracking.
/// Chunks D/E report live tracker/buff geometry so Character -> Progression and the gameplay HUD
/// use the same capacity. Stored selections are never truncated when capacity shrinks.
/// </summary>
public static class RebirthHudTrackingLayoutMetrics
{
    public const float TrackerBottomAnchorY = 20f;
    public const float TrackerRowPitch = XUiC_RebirthTrackedProgressionHud.RowPitch;
    public const float TrackerBuffGap = 8f;
    public const float CompactBuffRowPitch = 28f; // 26px authored row + native 2px spacing.
    public const float UpperSafeReserve = 260f;

    private const float ReferenceHeight = 1080f;
    private const float ReferenceBottomReserve = 220f;
    private const float ReferenceBuffReserve = 112f;

    private static readonly object Sync = new object();
    private static int measuredScreenWidth;
    private static int measuredScreenHeight;
    private static float measuredRowPitch;
    private static float measuredUsableHeight;
    private static int measuredCapacity = -1;

    private static float musicCardReserve;
    public static float MusicCardReserve { get { lock(Sync)return musicCardReserve; } }
    public static float RenderedLowerHudHeight { get { lock(Sync)return renderedTrackerHeight+musicCardReserve; } }
    public static void SetMusicCardVisible(bool visible)
    {
        float reserve=visible?40f:0f;
        lock(Sync)
        {
            if(musicCardReserve==reserve)return;
            musicCardReserve=reserve;
            measuredCapacity=-1;
        }
        Notify(TrackerEnvelopeChanged);Notify(TrackerFootprintChanged);Notify(TrackerLayoutChanged);
    }
    private static float renderedTrackerHeight;
    private static float renderedRowPitch;
    private static int renderedVisibleRows;
    private static bool renderedOverflow;
    private static int renderedScreenWidth;
    private static int renderedScreenHeight;

    public static event Action TrackerLayoutChanged; // Legacy aggregate notification.
    public static event Action TrackerEnvelopeChanged;
    public static event Action TrackerFootprintChanged;
    private static void Notify(Action handlers)
    {
        if (handlers == null) return;
        foreach (Action listener in handlers.GetInvocationList())
            try { listener(); } catch (Exception ex) { Log.Warning("[REBIRTH HUD] layout listener failed: " + ex.Message); }
    }

    public static int GetSafeCapacity()
    {
        lock (Sync)
        {
            if (measuredCapacity >= 0 && measuredScreenWidth == Screen.width && measuredScreenHeight == Screen.height)
                return Math.Min(15, measuredCapacity);
        }
        return CalculateReferenceCapacity(Screen.width, Screen.height);
    }

    public static bool IsUsingMeasuredGeometry
    {
        get
        {
            lock (Sync)
                return measuredCapacity >= 0 && measuredScreenWidth == Screen.width && measuredScreenHeight == Screen.height;
        }
    }

    public static int CalculateReferenceCapacity(int screenWidth, int screenHeight)
    {
        int safeHeight = Math.Max(1, screenHeight);
        float scale = Mathf.Clamp(safeHeight / ReferenceHeight, 0.60f, 2.50f);
        float rowPitch = Math.Max(18f, TrackerRowPitch * scale);
        float available = safeHeight
            - ReferenceBottomReserve * scale
            - UpperSafeReserve * scale
            - ReferenceBuffReserve * scale
            - TrackerBuffGap * scale - MusicCardReserve * scale;
        return Mathf.Clamp(Mathf.FloorToInt(Math.Max(0f, available) / rowPitch) * 3, 0, 15);
    }

    /// <summary>
    /// Converts live screen/XUi scale plus the native active-buff count into tracker capacity.
    /// The calculation is in XUi coordinate space so changing resolution or UI scale changes the
    /// available rows without querying an unverified preference enum.
    /// </summary>
    public static float CalculateUsableTrackerHeight(int screenHeight, float xuiScaleY, int activeBuffRows)
    {
        float scale = Math.Abs(xuiScaleY);
        if (scale < 0.01f) scale = 1f;
        float xuiScreenHeight = Math.Max(1f, screenHeight) / scale;
        int buffs = Math.Max(0, activeBuffRows);
        float buffHeight = buffs * CompactBuffRowPitch;
        float gap = buffs > 0 ? TrackerBuffGap : 0f;
        return Math.Max(0f, xuiScreenHeight - TrackerBottomAnchorY - UpperSafeReserve - buffHeight - gap - MusicCardReserve);
    }

    public static int CalculateMeasuredCapacity(int screenHeight, float xuiScaleY, int activeBuffRows)
    {
        float usable = CalculateUsableTrackerHeight(screenHeight, xuiScaleY, activeBuffRows);
        return Mathf.Clamp(Mathf.FloorToInt(usable / TrackerRowPitch) * 3, 0, 15);
    }

    public static int CalculateBuffYOffset(float trackerHeight)
    {
        float height = Math.Max(0f, trackerHeight);
        return height <= 0.01f ? 0 : Mathf.CeilToInt(height + TrackerBuffGap);
    }

    public static void ReportMeasuredHudEnvelopePixels(int screenWidth, int screenHeight, float xuiScaleY, float reservedPixels)
    {
        float scale = Math.Abs(xuiScaleY);
        if (scale < 0.01f) scale = 1f;
        float reserve = Math.Max(0f, reservedPixels);
        float usable = Math.Max(0f, Math.Max(1f, screenHeight) / scale - TrackerBottomAnchorY - UpperSafeReserve
            - reserve - (reserve > 0f ? TrackerBuffGap : 0f) - MusicCardReserve);
        ReportMeasuredGeometry(screenWidth, screenHeight, TrackerRowPitch, usable);
    }

    public static void ReportMeasuredHudEnvelope(int screenWidth, int screenHeight, float xuiScaleY, int activeBuffRows)
    {
        ReportMeasuredGeometry(screenWidth, screenHeight, TrackerRowPitch,
            CalculateUsableTrackerHeight(screenHeight, xuiScaleY, activeBuffRows));
    }

    /// <summary>
    /// Called by the live HUD/layout owner once real scaled geometry exists.
    /// </summary>
    public static void ReportMeasuredGeometry(int screenWidth, int screenHeight, float rowPitch, float usableTrackerHeight)
    {
        if (screenWidth <= 0 || screenHeight <= 0 || rowPitch <= 0.01f || usableTrackerHeight < 0f) return;
        int capacity = Mathf.Clamp(Mathf.FloorToInt(usableTrackerHeight / rowPitch) * 3, 0, 15);
        Action changed = null;
        lock (Sync)
        {
            bool different = measuredScreenWidth != screenWidth || measuredScreenHeight != screenHeight ||
                Math.Abs(measuredRowPitch - rowPitch) > 0.01f || Math.Abs(measuredUsableHeight - usableTrackerHeight) > 0.01f ||
                measuredCapacity != capacity;
            measuredScreenWidth = screenWidth;
            measuredScreenHeight = screenHeight;
            measuredRowPitch = rowPitch;
            measuredUsableHeight = usableTrackerHeight;
            measuredCapacity = capacity;
            if (different) changed = TrackerLayoutChanged ?? delegate { };
        }
        if (changed != null) { Notify(TrackerEnvelopeChanged); Notify(changed); }
    }

    /// <summary>
    /// Reports the live tracker's current XUi-space height. Chunk E consumes this value to derive
    /// the native BuffPopoutList yOffset in the same coordinate space.
    /// </summary>
    public static void ReportRenderedTrackerState(int screenWidth, int screenHeight, float rowPitch, float renderedHeight, int visibleRows, bool overflow)
    {
        Action changed = null;
        lock (Sync)
        {
            float height = Math.Max(0f, renderedHeight);
            float pitch = Math.Max(0f, rowPitch);
            int rows = Math.Max(0, visibleRows);
            bool different = renderedScreenWidth != screenWidth || renderedScreenHeight != screenHeight ||
                Math.Abs(renderedTrackerHeight - height) > 0.01f || Math.Abs(renderedRowPitch - pitch) > 0.01f ||
                renderedVisibleRows != rows || renderedOverflow != overflow;
            renderedScreenWidth = screenWidth;
            renderedScreenHeight = screenHeight;
            renderedTrackerHeight = height;
            renderedRowPitch = pitch;
            renderedVisibleRows = rows; // Physical rows, not logical cells.
            renderedOverflow = overflow;
            if (different) changed = TrackerLayoutChanged ?? delegate { };
        }
        if (changed != null) { Notify(TrackerFootprintChanged); Notify(changed); }
    }

    public static float RenderedTrackerHeight { get { lock (Sync) return renderedTrackerHeight; } }
    public static float RenderedTrackerRowPitch { get { lock (Sync) return renderedRowPitch; } }
    public static int RenderedTrackerRows { get { lock (Sync) return renderedVisibleRows; } }
    public static bool RenderedTrackerHasOverflow { get { lock (Sync) return renderedOverflow; } }

    public static void ClearMeasuredGeometry()
    {
        Action changed = null;
        lock (Sync)
        {
            bool hadState = measuredCapacity >= 0 || renderedTrackerHeight > 0.01f || renderedVisibleRows > 0;
            measuredScreenWidth = 0;
            measuredScreenHeight = 0;
            measuredRowPitch = 0f;
            measuredUsableHeight = 0f;
            measuredCapacity = -1;
            renderedTrackerHeight = 0f;
            renderedRowPitch = 0f;
            renderedVisibleRows = 0;
            renderedOverflow = false;
            renderedScreenWidth = 0;
            renderedScreenHeight = 0;
            if (hadState) changed = TrackerLayoutChanged ?? delegate { };
        }
        if (changed != null) { Notify(TrackerEnvelopeChanged); Notify(TrackerFootprintChanged); Notify(changed); }
    }

    public static string GetDebugGeometry()
    {
        lock (Sync)
        {
            string live = " trackerHeight=" + renderedTrackerHeight.ToString("0.##") + " trackerRows=" + renderedVisibleRows + " overflow=" + renderedOverflow;
            if (measuredCapacity < 0) return "reference capacity=" + CalculateReferenceCapacity(Screen.width, Screen.height) + live;
            return "measured capacity=" + measuredCapacity + " rowPitch=" + measuredRowPitch.ToString("0.##") + " usableHeight=" + measuredUsableHeight.ToString("0.##") + live;
        }
    }
}
