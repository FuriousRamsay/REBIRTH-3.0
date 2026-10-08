using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Gameplay bottom-left tracked progression HUD. Presentation-only: values are resolved from the
/// existing authoritative owning-client Survivor snapshot. This controller never requests or
/// mutates progression state.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthTrackedProgressionHud : XUiController
{
    public const int AuthoredRows = 64;
    public const int RowHeight = 34;
    public const int RowPitch = 36;
    public const int HudWidth = 288;
    public const float GainPulseSeconds = 1.35f;

    private sealed class PreviousDisplay
    {
        public long Revision = long.MinValue;
        public string ValueText = string.Empty;
        public float Progress;
        public float NumericValue;
        public bool HasNumericValue;
        public float PulseUntil;
        public string DeltaText = string.Empty;
    }

    private readonly XUiController[] rows = new XUiController[AuthoredRows];
    private readonly XUiV_Sprite[] borders = new XUiV_Sprite[AuthoredRows];
    private readonly XUiV_Sprite[] icons = new XUiV_Sprite[AuthoredRows];
    private readonly XUiV_Sprite[] stateIcons = new XUiV_Sprite[AuthoredRows];
    private readonly XUiV_Sprite[] typeIcons = new XUiV_Sprite[AuthoredRows];
    private readonly XUiV_Label[] names = new XUiV_Label[AuthoredRows];
    private readonly XUiV_Label[] values = new XUiV_Label[AuthoredRows];
    private readonly XUiV_Label[] deltas = new XUiV_Label[AuthoredRows];
    private readonly XUiController[] progressBackgroundControllers = new XUiController[AuthoredRows];
    private readonly XUiV_Sprite[] progressFills = new XUiV_Sprite[AuthoredRows];
    private readonly Dictionary<string, PreviousDisplay> previous = new Dictionary<string, PreviousDisplay>(StringComparer.OrdinalIgnoreCase);

    private XUiController overflowRow;
    private XUiV_Label overflowLabel;
    private bool subscribed;
    private bool wasSuppressed;
    private float refreshClock;
    private RebirthSurvivorOwnerHeader lastOwnerHeader;
    private RebirthSurvivorOwnerStateSnapshot cachedOwner;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private long lastContextEpoch = long.MinValue;
    private bool hasActivePulse;
    private bool presentationActive, dataDirty = true, layoutDirty = true, rendering, hidden = true;
    private long preferenceRevision = long.MinValue, textRevision = long.MinValue, providerRevision = long.MinValue;
    private readonly List<RebirthHudTrackDisplay> resolvedCache = new List<RebirthHudTrackDisplay>();
    private bool trackingEnabled;
    private int boundCount;
    private RebirthPlayerProgressionMode lastMode;

    public override void Init()
    {
        base.Init();
        RebirthHudTrackingRegistry.EnsureDefaults();

        for (int i = 0; i < AuthoredRows; i++)
        {
            string s = i.ToString(CultureInfo.InvariantCulture);
            rows[i] = GetChildById("rebirthTrackedProgressionRow" + s);
            borders[i] = Sprite("rebirthTrackedProgressionBorder" + s);
            icons[i] = Sprite("rebirthTrackedProgressionIcon" + s);
            stateIcons[i] = Sprite("rebirthTrackedProgressionState" + s);
            typeIcons[i] = Sprite("rebirthTrackedProgressionType" + s);
            names[i] = Label("rebirthTrackedProgressionName" + s);
            values[i] = Label("rebirthTrackedProgressionValue" + s);
            deltas[i] = Label("rebirthTrackedProgressionDelta" + s);
            progressBackgroundControllers[i] = GetChildById("rebirthTrackedProgressionProgress" + s);
            progressFills[i] = Sprite("rebirthTrackedProgressionProgressFill" + s);
            SetVisible(rows[i], false);
        }

        overflowRow = GetChildById("rebirthTrackedProgressionOverflow");
        overflowLabel = Label("rebirthTrackedProgressionOverflowText");
        SetVisible(overflowRow, false);
        HideAndReport();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        Subscribe();
        presentationActive = true; dataDirty = layoutDirty = true;
    }

    public override void OnClose()
    {
        presentationActive = false;
        Unsubscribe();
        HideAndReport();
        base.OnClose();
    }

    public override void Cleanup()
    {
        presentationActive = false;
        Unsubscribe();
        HideAndReport();
        base.Cleanup();
    }

    public override void Update(float dt)
    {
        base.Update(dt); // Native traversal is preserved even while presentation is closed.
        if (!presentationActive) return;
        RefreshContextScope();
        if(ViewComponent!=null)
        {
            var position=new Vector2i(14,10+(int)RebirthHudTrackingLayoutMetrics.MusicCardReserve);
            if(ViewComponent.Position.x!=position.x||ViewComponent.Position.y!=position.y)
            {ViewComponent.Position=position;ViewComponent.TryUpdatePosition();layoutDirty=true;}
        }
        if (lastMode != RebirthSurvivorMode.ConfiguredMode) { lastMode = RebirthSurvivorMode.ConfiguredMode; dataDirty = true; }
        bool suppressed = RebirthPersonalCraftingHudSuppressionInstaller.ShouldSuppress || xui.playerUI.windowManager.IsModalWindowOpen();
        if (suppressed) { if (!wasSuppressed) HideAndReport(); wasSuppressed = true; return; }
        if (wasSuppressed) { wasSuppressed = false; dataDirty = layoutDirty = true; }
        refreshClock += Math.Max(0f, dt);
        if (refreshClock >= 0.25f)
        {
            refreshClock = 0f;
            string ignored; RebirthHudTrackingPreferenceService.FlushPendingIfDue(out ignored);
            RebirthSurvivorOwnerHeader header = RebirthSurvivorClientState.GetOwnerHeader();
            long prefs = RebirthHudTrackingPreferenceService.Revision;
            long text = RebirthUiProjectionTextCache.Revision, providers = RebirthHudTrackingRegistry.Revision;
            if (!header.Equals(lastOwnerHeader) || preferenceRevision != prefs || textRevision != text || providerRevision != providers)
                dataDirty = true;
            preferenceRevision = prefs; textRevision = text; providerRevision = providers;
            layoutDirty |= lastScreenWidth != Screen.width || lastScreenHeight != Screen.height;
        }
        if ((dataDirty || layoutDirty) && !rendering)
        {
            rendering = true;
            bool rebuild = dataDirty; dataDirty = layoutDirty = false;
            try { Render(rebuild); } finally { rendering = false; }
        }
        else if (hasActivePulse) UpdatePulseAppearance();
    }

    private void RefreshContextScope()
    {
        long epoch = RebirthHudTrackingPreferenceService.ContextEpoch;
        if (epoch == lastContextEpoch) return;
        lastContextEpoch = epoch;
        // Owner headers/revisions can repeat across worlds. Never retain another scope's
        // snapshot, displayed rows or gain pulse while waiting for its first projection.
        previous.Clear(); resolvedCache.Clear(); cachedOwner = null;
        lastOwnerHeader = default(RebirthSurvivorOwnerHeader);
        trackingEnabled = false;
        dataDirty = layoutDirty = true;
        HideAndReport();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        RebirthHudTrackingPreferenceService.PreferencesChanged += OnPreferencesChanged;
        RebirthSurvivorClientState.OwnerProjectionChanged += OnOwnerStateChanged;
        RebirthHudTrackingLayoutMetrics.TrackerEnvelopeChanged += OnTrackerLayoutChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        RebirthHudTrackingPreferenceService.PreferencesChanged -= OnPreferencesChanged;
        RebirthSurvivorClientState.OwnerProjectionChanged -= OnOwnerStateChanged;
        RebirthHudTrackingLayoutMetrics.TrackerEnvelopeChanged -= OnTrackerLayoutChanged;
        subscribed = false;
    }

    private void OnPreferencesChanged() { dataDirty = true; }
    private void OnOwnerStateChanged() { dataDirty = true; }
    private void OnTrackerLayoutChanged() { layoutDirty = true; }

    private void Render(bool rebuildData)
    {
        lastScreenWidth = Screen.width; lastScreenHeight = Screen.height;
        preferenceRevision = RebirthHudTrackingPreferenceService.Revision;
        textRevision = RebirthUiProjectionTextCache.Revision; providerRevision = RebirthHudTrackingRegistry.Revision;
        if (rebuildData)
        {
            RebirthSurvivorOwnerHeader header = RebirthSurvivorClientState.GetOwnerHeader();
            if (cachedOwner == null || !header.Equals(lastOwnerHeader)) cachedOwner = RebirthSurvivorClientState.GetOwnerStateSnapshot(out lastOwnerHeader);
            RebirthHudTrackingPreferences preferences = RebirthHudTrackingPreferenceService.GetCurrent();
            trackingEnabled = RebirthSurvivorMode.ConfiguredMode == RebirthPlayerProgressionMode.Rebirth
                && cachedOwner != null && cachedOwner.RebirthModeEnabled && cachedOwner.HasCharacter
                && preferences != null && preferences.Enabled;
            resolvedCache.Clear();
            if (trackingEnabled)
            {
                preferences.NormalizeOrder();
                for (int i = 0; i < preferences.Entries.Count; i++)
                {
                    RebirthHudTrackingEntryPreference entry = preferences.Entries[i];
                    RebirthHudTrackDisplay display;
                    if (entry != null && entry.Enabled && RebirthHudTrackingRegistry.TryResolveDisplay(entry, cachedOwner, out display) && display != null)
                        resolvedCache.Add(display);
                }
            }
            // Bound obsolete pulse state rather than retaining every historically selected ID.
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RebirthHudTrackDisplay display in resolvedCache) if (display.Id != null) keep.Add(display.Id.Key);
            var remove = new List<string>();
            foreach (string key in previous.Keys) if (!keep.Contains(key)) remove.Add(key);
            foreach (string key in remove) previous.Remove(key);
        }
        if (!trackingEnabled) { HideAndReport(); return; }
        List<RebirthHudTrackDisplay> resolved = resolvedCache;
        int safeCapacity = Math.Min(AuthoredRows, Math.Min(15, Math.Max(0, RebirthHudTrackingLayoutMetrics.GetSafeCapacity())));
        int dataRows, overflowCount, visualRows;
        RebirthHudTrackingRenderMath.CalculateVisibleWindow(resolved.Count, safeCapacity, out dataRows, out overflowCount, out visualRows);

        boundCount = dataRows;
        hasActivePulse = false;
        for (int i = 0; i < AuthoredRows; i++)
        {
            bool visible = i < dataRows;
            SetVisible(rows[i], visible);
            if (!visible) continue;
            // Fill each row left to right, then add the next row above.
            if (rows[i]?.ViewComponent != null)
                rows[i].ViewComponent.Position = new Vector2i((i % 3) * 96, (i / 3) * RowPitch);
            BindRow(i, resolved[i]);
        }

        bool showOverflow = overflowCount > 0 && visualRows > 0;
        SetVisible(overflowRow, showOverflow);
        if (showOverflow)
        {
            if (overflowRow != null && overflowRow.ViewComponent != null)
                overflowRow.ViewComponent.Position = new Vector2i((dataRows % 3) * 96, (dataRows / 3) * RowPitch);
            Set(overflowLabel, "+" + overflowCount.ToString(CultureInfo.InvariantCulture) + " " + L("xuiRebirthHudTrackingOverflow", "more tracked"));
        }

        bool visibleRoot = visualRows > 0;
        hidden = !visibleRoot;
        if (ViewComponent != null)
        {
            ViewComponent.IsVisible = visibleRoot;
            ViewComponent.Size = new Vector2i(HudWidth, Math.Max(1, ((visualRows + 2) / 3) * RowPitch - (RowPitch - RowHeight)));
        }

        float renderedHeight = visibleRoot ? ((visualRows + 2) / 3) * RowPitch : 0f;
        RebirthHudTrackingLayoutMetrics.ReportRenderedTrackerState(Screen.width, Screen.height, RowPitch, renderedHeight, (visualRows + 2) / 3, showOverflow);
    }

    private void UpdatePulseAppearance()
    {
        hasActivePulse = false;
        for (int i = 0; i < boundCount && i < resolvedCache.Count; i++)
        {
            RebirthHudTrackDisplay display = resolvedCache[i]; PreviousDisplay state;
            bool pulse = display.Id != null && previous.TryGetValue(display.Id.Key, out state) && state.PulseUntil > Time.time;
            hasActivePulse |= pulse;
            Color target = pulse ? (Color)new Color32(204,167,56,255) : (Color)ValueColor(display);
            if (values[i] != null && values[i].Color != target) values[i].Color = target;
        }
    }

    private void BindRow(int index, RebirthHudTrackDisplay display)
    {
        bool hasIcon = display != null && !string.IsNullOrEmpty(display.Icon);
        SetVisible(icons[index] != null ? icons[index].Controller : null, hasIcon);
        if (hasIcon) SetSprite(icons[index], display.Atlas, display.Icon, Color.white);
        Set(names[index], display != null ? display.Name : string.Empty);
        if (names[index] != null) names[index].IsVisible = false;
        if (rows[index]?.ViewComponent != null) rows[index].ViewComponent.ToolTip = display == null ? string.Empty :
            display.Id.Type + ": " + display.Name + " — " + display.ValueText +
            (display.Id.Type == RebirthHudTrackType.Skill ? " (" + (display.Progress01 * 100f).ToString("0", CultureInfo.InvariantCulture) + "% to next level)" :
             display.Id.Type == RebirthHudTrackType.Knowledge && !display.Complete ? " — Study or discover this knowledge" : string.Empty);
        Set(values[index], display != null ? display.ValueText : string.Empty);

        var type = display.Id.Type;
        Color32 accent = type == RebirthHudTrackType.Attribute ? new Color32(216,187,112,255) :
            type == RebirthHudTrackType.Skill ? new Color32(88,185,190,255) : new Color32(190,151,232,255);
        if (borders[index] != null) borders[index].SetColorImmediately(accent);
        if (typeIcons[index] != null) typeIcons[index].IsVisible = false;
        bool binaryKnowledge = type == RebirthHudTrackType.Knowledge && !display.HasProgress;
        if (values[index] != null) values[index].IsVisible = !binaryKnowledge;
        if (stateIcons[index] != null)
        {
            stateIcons[index].IsVisible = binaryKnowledge;
            if (binaryKnowledge)
            {
                SetSprite(stateIcons[index], "UIAtlas", display.Complete ? "ui_game_symbol_check" : "ui_game_symbol_lock", Color.white);
                stateIcons[index].FillCenter = true;
            }
        }
        bool hasProgress = display.HasProgress;
        SetVisible(progressBackgroundControllers[index], hasProgress);
        if (hasProgress && progressFills[index] != null)
        {
            progressFills[index].Fill = display.Complete ? 1f : Mathf.Clamp01(display.Progress01);
            progressFills[index].SetColorImmediately(accent);
        }

        if (display == null || display.Id == null)
        {
            SetVisible(deltas[index] != null ? deltas[index].Controller : null, false);
            return;
        }

        string key = display.Id.Key;
        PreviousDisplay state;
        if (!previous.TryGetValue(key, out state) || state == null)
        {
            state = new PreviousDisplay();
            previous[key] = state;
        }

        float currentNumeric = 0f;
        bool currentNumericOk = display.Id.Type == RebirthHudTrackType.Skill &&
            float.TryParse(display.ValueText ?? string.Empty, NumberStyles.Float, CultureInfo.InvariantCulture, out currentNumeric);

        bool changed = state.Revision != long.MinValue && display.SourceRevision != state.Revision &&
            (!string.Equals(state.ValueText, display.ValueText ?? string.Empty, StringComparison.Ordinal) || Math.Abs(state.Progress - display.Progress01) > 0.0005f);

        if (changed)
        {
            state.PulseUntil = Time.time + GainPulseSeconds;
            state.DeltaText = string.Empty;
            if (display.Id.Type == RebirthHudTrackType.Skill && currentNumericOk && state.HasNumericValue)
            {
                float gain = RebirthHudTrackingRenderMath.CalculateSkillGain(state.NumericValue, state.Progress, currentNumeric, display.Progress01);
                if (gain > 0f) state.DeltaText = "+" + gain.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        state.Revision = display.SourceRevision;
        state.ValueText = display.ValueText ?? string.Empty;
        state.Progress = Mathf.Clamp01(display.Progress01);
        state.NumericValue = currentNumericOk ? currentNumeric : 0f;
        state.HasNumericValue = currentNumericOk;

        bool pulsing = state.PulseUntil > Time.time;
        if (pulsing) hasActivePulse = true;
        if (values[index] != null)
            values[index].Color = pulsing ? new Color32(204, 167, 56, 255) : ValueColor(display);
        bool showDelta = false; // Pulse the value color without reserving another text column.
        if (values[index] != null)
            values[index].Size = new Vector2i(42, 18);
        SetVisible(deltas[index] != null ? deltas[index].Controller : null, showDelta);
        if (showDelta)
        {
            Set(deltas[index], state.DeltaText);
            deltas[index].Color = new Color32(204, 167, 56, 255);
        }
    }

    private static Color32 ValueColor(RebirthHudTrackDisplay display)
    {
        if (display == null) return new Color32(225, 225, 225, 255);
        if (display.Id != null && display.Id.Type == RebirthHudTrackType.Knowledge)
            return display.Complete ? new Color32(143, 209, 143, 255) : new Color32(165, 165, 165, 255);
        if (display.Complete) return new Color32(143, 209, 143, 255);
        return new Color32(225, 225, 225, 255);
    }

    private void HideAndReport()
    {
        hasActivePulse = false; boundCount = 0;
        if (hidden && ViewComponent != null && !ViewComponent.IsVisible)
        {
            RebirthHudTrackingLayoutMetrics.ReportRenderedTrackerState(Screen.width, Screen.height, RowPitch, 0f, 0, false);
            return;
        }
        hidden = true;
        for (int i = 0; i < AuthoredRows; i++) SetVisible(rows[i], false);
        SetVisible(overflowRow, false);
        if (ViewComponent != null)
        {
            ViewComponent.IsVisible = false;
            ViewComponent.Size = new Vector2i(HudWidth, 1);
        }
        RebirthHudTrackingLayoutMetrics.ReportRenderedTrackerState(Screen.width, Screen.height, RowPitch, 0f, 0, false);
    }

    private XUiV_Label Label(string id)
    {
        XUiController c = GetChildById(id);
        return c != null ? c.ViewComponent as XUiV_Label : null;
    }

    private XUiV_Sprite Sprite(string id)
    {
        XUiController c = GetChildById(id);
        return c != null ? c.ViewComponent as XUiV_Sprite : null;
    }

    private static void Set(XUiV_Label label, string value) { if (label != null && label.Text != (value ?? string.Empty)) label.Text = value ?? string.Empty; }
    private static void SetVisible(XUiController controller, bool visible) { if (controller != null && controller.ViewComponent != null && controller.ViewComponent.IsVisible != visible) controller.ViewComponent.IsVisible = visible; }
    private static void SetSprite(XUiV_Sprite sprite, string atlas, string icon, Color color)
    {
        if (sprite == null) return;
        sprite.UIAtlas = string.IsNullOrEmpty(atlas) ? "UIAtlas" : atlas;
        sprite.SetSpriteImmediately(icon ?? string.Empty);
        sprite.SetColorImmediately(color);
    }
    private static string L(string key, string fallback) { return RebirthUiProjectionTextCache.L(key, fallback); }
}
