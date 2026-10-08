using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Character -> Progression HUD Tracking configuration surface.
/// Owns presentation preferences only. Authoritative progression values always come from the
/// existing owner snapshot/providers; this controller sends no progression network requests.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthHudTrackingManager : XUiController
{
    private const int TrackedRows = 272;
    private const int AvailableRows = 208;
    private bool presentationActive;
    private const int RowPitch = 40;
    private const int MinimumScrollContentHeight = 438;

    private readonly XUiController[] trackedRows = new XUiController[TrackedRows];
    private readonly XUiV_Sprite[] trackedIcons = new XUiV_Sprite[TrackedRows];
    private readonly XUiV_Label[] trackedNames = new XUiV_Label[TrackedRows];
    private readonly XUiV_Label[] trackedTypes = new XUiV_Label[TrackedRows];
    private readonly XUiV_Label[] trackedValues = new XUiV_Label[TrackedRows];
    private readonly XUiController[] trackedLeftButtons = new XUiController[TrackedRows];
    private readonly XUiController[] trackedRightButtons = new XUiController[TrackedRows];
    private readonly XUiController[] trackedUpButtons = new XUiController[TrackedRows];
    private readonly XUiController[] trackedDownButtons = new XUiController[TrackedRows];
    private readonly XUiController[] trackedRemoveButtons = new XUiController[TrackedRows];
    private readonly Dictionary<XUiController, int> trackedUpIndex = new Dictionary<XUiController, int>();
    private readonly Dictionary<XUiController, int> trackedDownIndex = new Dictionary<XUiController, int>();
    private readonly Dictionary<XUiController, int> trackedRemoveIndex = new Dictionary<XUiController, int>();
    private readonly RebirthHudTrackingEntryPreference[] trackedBoundEntries = new RebirthHudTrackingEntryPreference[TrackedRows];

    private readonly XUiController[] availableRows = new XUiController[AvailableRows];
    private readonly XUiV_Sprite[] availableIcons = new XUiV_Sprite[AvailableRows];
    private readonly XUiV_Label[] availableNames = new XUiV_Label[AvailableRows];
    private readonly XUiV_Label[] availableValues = new XUiV_Label[AvailableRows];
    private readonly XUiV_Label[] availableStates = new XUiV_Label[AvailableRows];
    private readonly XUiController[] availableToggleButtons = new XUiController[AvailableRows];
    private readonly Dictionary<XUiController, int> availableToggleIndex = new Dictionary<XUiController, int>();
    private readonly RebirthHudTrackingId[] availableBoundIds = new RebirthHudTrackingId[AvailableRows];

    private XUiController overlay;
    private XUiController trackedContent;
    private XUiController availableContent;
    private XUiC_RebirthCharacterOverviewList availableList;
    private XUiV_Label headerCount;
    private XUiV_Label managerCount;
    private XUiV_Label enabledState;
    private XUiV_Label activeFilterLabel;
    private XUiV_Label trackedEmpty;
    private XUiV_Label availableEmpty;
    private RebirthHudTrackType filter = RebirthHudTrackType.Skill;
    private long lastOwnerRevision = long.MinValue;
    private RebirthSurvivorOwnerHeader lastOwnerHeader;
    private RebirthSurvivorOwnerStateSnapshot cachedOwner;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float refreshClock;
    private bool subscribed;
    private readonly Dictionary<XUiView, bool> obscuredViews = new Dictionary<XUiView, bool>();
    private RebirthHudNavigationLease navigationLease;
    private bool hadCursorLease, dataDirty = true, headerDirty = true, rangeDirty, refreshing;
    private long preferenceRevision = long.MinValue, textRevision = long.MinValue;
    private RebirthHudTrackingPreferences cachedPreferences;
    private readonly List<KeyValuePair<XUiController, XUiEvent_OnPressEventHandler>> wired = new List<KeyValuePair<XUiController, XUiEvent_OnPressEventHandler>>();
    private List<RebirthHudTrackingId> sortedAvailable;
    private RebirthHudTrackType sortedFilter;
    private object sortedDefinitions;
    private long sortedTextRevision = long.MinValue, sortedRegistryRevision = long.MinValue;
    private bool sortedHasOwner, sortedCompatible;
    private readonly Dictionary<XUiController,int> trackedDragSlots = new Dictionary<XUiController,int>();
    private int dragSource=-1;
    private long dragRevision,dragContext;
    private RebirthHudTrackingEntryPreference dragEntry;

    private void CancelTrackedDrag()
    {
        dragSource=-1;dragEntry=null;
    }

    private void TrackedCard_OnDrag(XUiController sender, EDragType type, Vector2 delta)
    {
        if(!IsManagerOpen||RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
        {CancelTrackedDrag();return;}
        if(type==EDragType.DragStart)
        {
            CancelTrackedDrag();
            int source;
            if(!trackedDragSlots.TryGetValue(sender,out source))return;
            var entry=GetTrackedBound(source);
            if(entry==null)return;
            dragSource=source;dragEntry=entry.Clone();
            dragContext=RebirthHudTrackingPreferenceService.ContextEpoch;
            dragRevision=RebirthHudTrackingPreferenceService.Revision;
        }
        if(type!=EDragType.DragEnd)return;
        int from=dragSource;var admitted=dragEntry;
        CancelTrackedDrag();
        if(from<0||admitted==null||dragContext!=RebirthHudTrackingPreferenceService.ContextEpoch||
            dragRevision!=RebirthHudTrackingPreferenceService.Revision)return;
        var cam=UICamera.currentCamera;
        if(cam==null)return;
        Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(Vector2)Input.mousePosition;
        var ray=cam.ScreenPointToRay(mouse);
        for(int target=0;target<15;target++)
        {
            var row=trackedRows[target];
            if(target==from||GetTrackedBound(target)==null||row?.ViewComponent==null||!row.ViewComponent.IsVisible)continue;
            var transform=row.ViewComponent.UiTransform;
            float distance;
            if(transform==null||!new Plane(transform.forward,transform.position).Raycast(ray,out distance))continue;
            Vector2 point=transform.InverseTransformPoint(ray.GetPoint(distance));
            var size=row.ViewComponent.Size;
            if(point.x<0||point.x>=size.x||point.y>0||point.y<=-size.y)continue;
            int destination=target;
            Mutate(p=>{
                // Recheck identity in the saved preference model, never trust a visual row index alone.
                p.NormalizeOrder();
                if(from>=p.Entries.Count||destination>=p.Entries.Count)return;
                var current=p.Entries[from];
                if(current==null||current.Type!=admitted.Type||
                    !string.Equals(current.StableId,admitted.StableId,StringComparison.OrdinalIgnoreCase))return;
                p.MoveGrid(admitted.Type,admitted.StableId,destination%3-from%3,destination/3-from/3);
            });
            return;
        }
    }

    public bool IsManagerOpen
    {
        get { return overlay != null && overlay.ViewComponent != null && overlay.ViewComponent.IsVisible; }
    }

    public override void Init()
    {
        Unwire();
        if (availableList != null) availableList.DataRangeChanged -= AvailableRangeChanged;
        trackedUpIndex.Clear(); trackedDownIndex.Clear(); trackedRemoveIndex.Clear(); availableToggleIndex.Clear();
        base.Init(); presentationActive = true;
        RebirthHudTrackingRegistry.EnsureDefaults();

        overlay = GetChildById("survivorHudTrackingOverlay");
        trackedContent = GetChildById("survivorHudTrackingTrackedContent");
        availableList = GetChildById("hudTrackingAvailableList") as XUiC_RebirthCharacterOverviewList;
        availableContent = availableList?.GetChildById("listContent");
        if (availableList != null) availableList.DataRangeChanged += AvailableRangeChanged;
        headerCount = Label("survivorHudTrackingHeaderCount");
        managerCount = Label("survivorHudTrackingManagerCount");
        enabledState = Label("survivorHudTrackingEnabledState");
        activeFilterLabel = Label("survivorHudTrackingActiveFilter");
        trackedEmpty = Label("survivorHudTrackingTrackedEmpty");
        availableEmpty = Label("survivorHudTrackingAvailableEmpty");

        Wire("btnSurvivorProgressionHudTracking", Open_OnPressed);
        Wire("btnSurvivorHudTrackingClose", Close_OnPressed);
        Wire("btnSurvivorHudTrackingToggleEnabled", ToggleEnabled_OnPressed);
        Wire("btnSurvivorHudTrackingClear", Clear_OnPressed);
        Wire("btnSurvivorHudTrackingFilterSkill", delegate { SetFilter(RebirthHudTrackType.Skill); });
        Wire("btnSurvivorHudTrackingFilterAttribute", delegate { SetFilter(RebirthHudTrackType.Attribute); });
        Wire("btnSurvivorHudTrackingFilterKnowledge", delegate { SetFilter(RebirthHudTrackType.Knowledge); });
        Wire("btnSurvivorHudTrackingFilterSummary", delegate { SetFilter(RebirthHudTrackType.Summary); });

        for (int i = 0; i < TrackedRows; i++)
        {
            string s = i.ToString(CultureInfo.InvariantCulture);
            int slot = i;
            trackedLeftButtons[i] = Wire("btnSurvivorHudTrackingTrackedLeft" + s, delegate { MoveGridSlot(slot, -1, 0); });
            trackedRightButtons[i] = Wire("btnSurvivorHudTrackingTrackedRight" + s, delegate { MoveGridSlot(slot, 1, 0); });
            trackedRows[i] = GetChildById("survivorHudTrackingTrackedRow" + s);
            if(i<15)
            {
                var drag=GetChildById("survivorHudTrackingTrackedDrag"+s);
                if(drag!=null){trackedDragSlots[drag]=i;drag.OnDrag+=TrackedCard_OnDrag;}
            }
            trackedIcons[i] = Sprite("survivorHudTrackingTrackedIcon" + s);
            trackedNames[i] = Label("survivorHudTrackingTrackedName" + s);
            trackedTypes[i] = Label("survivorHudTrackingTrackedType" + s);
            trackedValues[i] = Label("survivorHudTrackingTrackedValue" + s);
            trackedUpButtons[i] = Wire("btnSurvivorHudTrackingTrackedUp" + s, TrackedUp_OnPressed);
            trackedDownButtons[i] = Wire("btnSurvivorHudTrackingTrackedDown" + s, TrackedDown_OnPressed);
            trackedRemoveButtons[i] = Wire("btnSurvivorHudTrackingTrackedRemove" + s, TrackedRemove_OnPressed);
            if (trackedUpButtons[i] != null) trackedUpIndex[trackedUpButtons[i]] = i;
            if (trackedDownButtons[i] != null) trackedDownIndex[trackedDownButtons[i]] = i;
            if (trackedRemoveButtons[i] != null) trackedRemoveIndex[trackedRemoveButtons[i]] = i;
        }

        for (int i = 0; i < AvailableRows; i++)
        {
            string s = i.ToString(CultureInfo.InvariantCulture);
            availableRows[i] = GetChildById("survivorHudTrackingAvailableRow" + s);
            availableIcons[i] = Sprite("survivorHudTrackingAvailableIcon" + s);
            availableNames[i] = Label("survivorHudTrackingAvailableName" + s);
            availableValues[i] = Label("survivorHudTrackingAvailableValue" + s);
            availableStates[i] = Label("survivorHudTrackingAvailableState" + s);
            availableToggleButtons[i] = Wire("btnSurvivorHudTrackingAvailableToggle" + s, AvailableToggle_OnPressed);
            if (availableToggleButtons[i] != null) availableToggleIndex[availableToggleButtons[i]] = i;
        }

        SetVisible(overlay, false);
        dataDirty = headerDirty = true;
    }

    public override void OnOpen()
    {
        base.OnOpen(); presentationActive = true;
        Subscribe();
        dataDirty = headerDirty = true;
    }

    public override void OnClose()
    {
        CancelTrackedDrag();
        presentationActive = false;
        ReleaseModal();
        string ignored;
        RebirthHudTrackingPreferenceService.SaveNow(out ignored);
        Unsubscribe();
        SetVisible(overlay, false);
        base.OnClose();
    }

    public override void Cleanup()
    {
        CancelTrackedDrag();
        presentationActive = false;
        ReleaseModal(); Unsubscribe(); Unwire();
        if (availableList != null) availableList.DataRangeChanged -= AvailableRangeChanged;
        cachedOwner = null; cachedPreferences = null; sortedAvailable = null;
        base.Cleanup();
    }
    private void Unwire()
    {
        foreach (var pair in wired) if (pair.Key != null) pair.Key.OnPress -= pair.Value;
        wired.Clear();
        foreach(var pair in trackedDragSlots)pair.Key.OnDrag-=TrackedCard_OnDrag;
        trackedDragSlots.Clear();CancelTrackedDrag();
    }
    private void AvailableRangeChanged() { rangeDirty = true; }
    public override void Update(float dt)
    {
        base.Update(dt);
        refreshClock += Math.Max(0f, dt);
        if (refreshClock >= 0.25f)
        {
            refreshClock = 0f;
            string ignored; RebirthHudTrackingPreferenceService.FlushPendingIfDue(out ignored);
            long pref = RebirthHudTrackingPreferenceService.Revision;
            long text = RebirthUiProjectionTextCache.Revision;
            if (preferenceRevision != pref || textRevision != text) dataDirty = headerDirty = true;
            preferenceRevision = pref; textRevision = text;
            if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height) headerDirty = true;
        }
        if (!presentationActive) return;
        if (!IsManagerOpen)
        {
            // No owner cloning, provider evaluation or row rendering for the closed overlay.
            if (headerDirty) { headerDirty = false; RefreshHeaderOnly(); }
            return;
        }
        if (!RebirthSurvivorClientState.GetOwnerHeader().Equals(lastOwnerHeader)) dataDirty = true;
        if (sortedRegistryRevision != RebirthHudTrackingRegistry.Revision) dataDirty = true;
        if (refreshing) return;
        refreshing = true;
        try
        {
            if (dataDirty)
            {
                dataDirty = headerDirty = rangeDirty = false;
                RefreshAll(false);
            }
            else
            {
                if (headerDirty) { headerDirty = false; RefreshHeaderOnly(); }
                if (rangeDirty) { rangeDirty = false; RefreshAvailable(cachedPreferences, cachedOwner); }
            }
        }
        finally { refreshing = false; }
    }

    public void CloseManager()
    {
        CancelTrackedDrag();
        SetVisible(overlay, false);
        ReleaseModal();
        string ignored;
        RebirthHudTrackingPreferenceService.SaveNow(out ignored);
        headerDirty = true;
    }

    private void Open_OnPressed(XUiController sender, int mouseButton)
    {
        if (IsManagerOpen) return;
        var cursor = xui?.playerUI?.CursorController;
        if (cursor != null)
        {
            try { navigationLease = RebirthHudNavigationLease.Acquire(cursor, overlay?.ViewComponent, GetChildById("btnSurvivorHudTrackingClose")?.ViewComponent); }
            catch (Exception ex) { Log.Warning("[REBIRTH HUD] Navigation ownership unavailable: " + ex.Message); return; }
            if (navigationLease == null) return;
            hadCursorLease = true;
        }
        SetVisible(overlay, true);
        var character = GetParentByType<XUiC_RebirthSurvivorCharacter>();
        // Hide the three content columns as well as the attribute strip. Their own
        // UIPanels otherwise render independently of the dialog's background.
        // Hide the underlying page, including its backdrop and title, so transparency
        // shows the world rather than stacked page contents.
        var page = character?.GetChildById("survivorProgressionPanel");
        if (page != null) foreach (var child in page.Children)
        {
            if (child == this) continue;
            var view = child.ViewComponent;
            if (view == null) continue;
            obscuredViews[view] = view.IsVisible;
            view.IsVisible = false;
        }
        var opener = GetChildById("btnSurvivorProgressionHudTracking")?.ViewComponent;
        if (opener != null)
        {
            obscuredViews[opener] = opener.IsVisible;
            opener.IsVisible = false;
        }
        ResetScrollPositions();
        dataDirty = headerDirty = true;
    }

    private void ReleaseModal()
    {
        bool stillOwner = !hadCursorLease || (navigationLease != null && navigationLease.TryRelease());
        // Only restore properties that still equal our hidden value, and never restore over a
        // dialog which has replaced our navigation token. The next page open owns its own layout.
        if (stillOwner) foreach (var pair in obscuredViews)
            if (pair.Key != null && !pair.Key.IsVisible) pair.Key.IsVisible = pair.Value;
        obscuredViews.Clear(); navigationLease = null; hadCursorLease = false;
    }

    private void Close_OnPressed(XUiController sender, int mouseButton) { CloseManager(); }

    private void ToggleEnabled_OnPressed(XUiController sender, int mouseButton)
    {
        Mutate(delegate(RebirthHudTrackingPreferences p) { p.Enabled = !p.Enabled; });
    }

    private void Clear_OnPressed(XUiController sender, int mouseButton)
    {
        Mutate(delegate(RebirthHudTrackingPreferences p)
        {
            p.ClearKnownEntries();
            p.UnknownEntries.Clear();
        });
    }

    private void SetFilter(RebirthHudTrackType next)
    {
        if (filter == next)
        {
            dataDirty = true;
            return;
        }
        filter = next;
        availableList?.ResetPosition();
        dataDirty = headerDirty = true;
    }

    private void MoveGridSlot(int index, int dx, int dy)
    {
        var entry = GetTrackedBound(index);
        if (entry != null) Mutate(p => { p.MoveGrid(entry.Type, entry.StableId, dx, dy); });
    }

    private void TrackedUp_OnPressed(XUiController sender, int mouseButton)
    {
        int index;
        if (!trackedUpIndex.TryGetValue(sender, out index)) return;
        RebirthHudTrackingEntryPreference entry = GetTrackedBound(index);
        if (entry == null) return;
        Mutate(delegate(RebirthHudTrackingPreferences p) { p.MoveGrid(entry.Type, entry.StableId, 0, 1); });
    }

    private void TrackedDown_OnPressed(XUiController sender, int mouseButton)
    {
        int index;
        if (!trackedDownIndex.TryGetValue(sender, out index)) return;
        RebirthHudTrackingEntryPreference entry = GetTrackedBound(index);
        if (entry == null) return;
        Mutate(delegate(RebirthHudTrackingPreferences p) { p.MoveGrid(entry.Type, entry.StableId, 0, -1); });
    }

    private void TrackedRemove_OnPressed(XUiController sender, int mouseButton)
    {
        int index;
        if (!trackedRemoveIndex.TryGetValue(sender, out index)) return;
        RebirthHudTrackingEntryPreference entry = GetTrackedBound(index);
        if (entry == null) return;
        Mutate(delegate(RebirthHudTrackingPreferences p) { p.Remove(entry.Type, entry.StableId); });
    }

    private void AvailableToggle_OnPressed(XUiController sender, int mouseButton)
    {
        int index;
        if (!availableToggleIndex.TryGetValue(sender, out index) || index < 0 || index >= availableBoundIds.Length) return;
        RebirthHudTrackingId id = availableBoundIds[index];
        if (id == null) return;
        Mutate(delegate(RebirthHudTrackingPreferences p)
        {
            if (p.Contains(id.Type, id.StableId)) p.Remove(id.Type, id.StableId);
            else if (p.Entries.Count < 15) p.Add(id.Type, id.StableId);
        });
    }

    private RebirthHudTrackingEntryPreference GetTrackedBound(int index)
    {
        return index >= 0 && index < trackedBoundEntries.Length ? trackedBoundEntries[index] : null;
    }

    private void Mutate(Action<RebirthHudTrackingPreferences> mutation)
    {
        string error;
        if (!RebirthHudTrackingPreferenceService.Mutate(mutation, out error))
        {
            Log.Warning("[REBIRTH HUD Tracking] Preference mutation rejected: " + (error ?? string.Empty));
            return;
        }
        // PreferencesChanged is the single post-mutation invalidation path.
    }

    private void RefreshAll(bool layoutChanged)
    {
        if (!IsManagerOpen) { dataDirty = true; return; }
        RebirthSurvivorOwnerHeader header = RebirthSurvivorClientState.GetOwnerHeader();
        if (!header.Equals(lastOwnerHeader)) cachedOwner = RebirthSurvivorClientState.GetOwnerStateSnapshot(out lastOwnerHeader);
        RebirthSurvivorOwnerStateSnapshot owner = cachedOwner;
        lastOwnerRevision = owner != null ? owner.CharacterRevision : long.MinValue;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        RebirthHudTrackingPreferences preferences = cachedPreferences = RebirthHudTrackingPreferenceService.GetCurrent();
        RefreshHeader(preferences);
        RefreshTracked(preferences, owner);
        RefreshAvailable(preferences, owner);

    }

    private void RefreshHeaderOnly()
    {
        lastScreenWidth = Screen.width; lastScreenHeight = Screen.height;
        RefreshHeader(RebirthHudTrackingPreferenceService.GetCurrent());
    }

    private void RefreshHeader(RebirthHudTrackingPreferences preferences)
    {
        int selected = preferences != null ? preferences.Entries.Count : 0;
        int capacity = RebirthHudTrackingLayoutMetrics.GetSafeCapacity();
        Set(headerCount, selected.ToString(CultureInfo.InvariantCulture) + " / " + capacity.ToString(CultureInfo.InvariantCulture));
        Set(managerCount, L("xuiRebirthHudTrackingSelected", "Selected") + " " + selected.ToString(CultureInfo.InvariantCulture)
            + "  •  " + L("xuiRebirthHudTrackingSafeCapacity", "Safe display capacity") + " " + capacity.ToString(CultureInfo.InvariantCulture));
        bool enabled = preferences == null || preferences.Enabled;
        Set(enabledState, enabled ? "[8FD18F]" + L("xuiRebirthHudTrackingEnabled", "ENABLED") + "[-]" : "[CC6B64]" + L("xuiRebirthHudTrackingDisabled", "DISABLED") + "[-]");
        Set(activeFilterLabel, L("xuiRebirthHudTrackingShowing", "Showing") + ": " + TypeLabel(filter));
    }

    private void RefreshTracked(RebirthHudTrackingPreferences preferences, RebirthSurvivorOwnerStateSnapshot owner)
    {
        List<RebirthHudTrackingEntryPreference> entries = new List<RebirthHudTrackingEntryPreference>();
        if (preferences != null)
        {
            preferences.NormalizeOrder();
            for (int i = 0; i < preferences.Entries.Count; i++)
                if (preferences.Entries[i] != null) entries.Add(preferences.Entries[i]);
        }

        int count = Math.Min(entries.Count, TrackedRows);
        for (int i = 0; i < TrackedRows; i++)
        {
            bool visible = i < count;
            SetVisible(trackedRows[i], visible);
            trackedBoundEntries[i] = visible ? entries[i].Clone() : null;
            if (!visible) continue;

            RebirthHudTrackingEntryPreference entry = entries[i];
            RebirthHudTrackDisplay display;
            bool resolved = RebirthHudTrackingRegistry.TryResolveDisplay(entry, owner, out display) && display != null;
            if (resolved)
            {
                bool hasIcon = !string.IsNullOrEmpty(display.Icon);
                SetVisible(trackedIcons[i] != null ? trackedIcons[i].Controller : null, hasIcon);
                if (hasIcon) SetSprite(trackedIcons[i], display.Atlas, display.Icon, Color.white);
                Set(trackedNames[i], display.Name);
                Set(trackedValues[i], display.ValueText);
            }
            else
            {
                SetVisible(trackedIcons[i] != null ? trackedIcons[i].Controller : null, false);
                Set(trackedNames[i], entry.StableId);
                Set(trackedValues[i], L("xuiRebirthHudTrackingUnavailable", "Unavailable"));
            }
            Set(trackedTypes[i], i < 15 ? L("xuiRebirthHudTrackingRow", "ROW") + " " + (i / 3 + 1)
                + "  \u2022  " + L("xuiRebirthHudTrackingColumn", "COLUMN") + " " + (i % 3 + 1)
                : L("xuiRebirthHudTrackingNotDisplayed", "NOT DISPLAYED"));
            SetVisible(trackedUpButtons[i], RebirthHudTrackingPreferences.GridTarget(i, count, 0, 1) >= 0);
            SetVisible(trackedLeftButtons[i], RebirthHudTrackingPreferences.GridTarget(i, count, -1, 0) >= 0);
            SetVisible(trackedRightButtons[i], RebirthHudTrackingPreferences.GridTarget(i, count, 1, 0) >= 0);
            SetVisible(trackedDownButtons[i], RebirthHudTrackingPreferences.GridTarget(i, count, 0, -1) >= 0);
            SetVisible(trackedRemoveButtons[i], true);
        }
        SetVisible(trackedEmpty != null ? trackedEmpty.Controller : null, count == 0);
        if (trackedContent != null && trackedContent.ViewComponent != null)
            trackedContent.ViewComponent.Size = new Vector2i(trackedContent.ViewComponent.Size.x, Math.Max(600, 600 + Math.Max(0, count - 15) * RowPitch));
    }

    private void RefreshAvailable(RebirthHudTrackingPreferences preferences, RebirthSurvivorOwnerStateSnapshot owner)
    {
        long text = RebirthUiProjectionTextCache.Revision, registry = RebirthHudTrackingRegistry.Revision;
        bool hasOwner = owner != null && owner.HasCharacter && owner.RebirthModeEnabled;
        bool compatible = owner != null && owner.DefinitionsCompatible;
        if (sortedAvailable == null || sortedFilter != filter || sortedTextRevision != text
            || sortedRegistryRevision != registry || !ReferenceEquals(sortedDefinitions, RebirthSurvivorDefinitionRegistry.Bundle)
            || sortedHasOwner != hasOwner || sortedCompatible != compatible)
        {
            IList<RebirthHudTrackingId> ids = RebirthHudTrackingRegistry.EnumerateTrackableIds(filter);
            var pairs = new List<KeyValuePair<RebirthHudTrackingId, string>>(ids.Count);
            for (int i = 0; i < ids.Count; i++)
            {
                RebirthHudTrackingId id = ids[i]; if (id == null) continue;
                RebirthHudTrackDisplay display;
                pairs.Add(new KeyValuePair<RebirthHudTrackingId, string>(id, TryResolve(id, owner, out display) ? display.Name : id.StableId));
            }
            pairs.Sort(delegate(KeyValuePair<RebirthHudTrackingId, string> a, KeyValuePair<RebirthHudTrackingId, string> b)
            {
                int byName = string.Compare(a.Value, b.Value, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : string.Compare(a.Key.Key, b.Key.Key, StringComparison.Ordinal);
            });
            sortedAvailable = new List<RebirthHudTrackingId>(pairs.Count);
            foreach (var pair in pairs) sortedAvailable.Add(pair.Key);
            sortedFilter = filter; sortedDefinitions = RebirthSurvivorDefinitionRegistry.Bundle;
            sortedTextRevision = RebirthUiProjectionTextCache.Revision; sortedRegistryRevision = registry;
            sortedHasOwner = hasOwner; sortedCompatible = compatible;
        }
        // The pool is a viewport, not a limit on the logical result set.
        List<RebirthHudTrackingId> visibleIds = sortedAvailable;
        availableList?.SetItemCount(visibleIds.Count, "");
        int start = availableList != null ? availableList.FirstDataIndex : 0;
        int pool = availableList != null && availableList.IsReady ? Math.Min(AvailableRows, availableList.Capacity) : AvailableRows;
        int count = Math.Min(Math.Max(0, visibleIds.Count - start), pool);
        for (int i = 0; i < AvailableRows; i++)
        {
            bool visible = i < count;
            SetVisible(availableRows[i], visible);
            availableBoundIds[i] = visible ? visibleIds[start + i] : null;
            if (!visible) continue;

            RebirthHudTrackingId id = visibleIds[start + i];
            RebirthHudTrackDisplay display;
            bool resolved = TryResolve(id, owner, out display);
            if (resolved)
            {
                bool hasIcon = !string.IsNullOrEmpty(display.Icon);
                SetVisible(availableIcons[i] != null ? availableIcons[i].Controller : null, hasIcon);
                if (hasIcon) SetSprite(availableIcons[i], display.Atlas, display.Icon, Color.white);
                Set(availableNames[i], display.Name);
                Set(availableValues[i], display.ValueText);
            }
            else
            {
                SetVisible(availableIcons[i] != null ? availableIcons[i].Controller : null, false);
                Set(availableNames[i], id.StableId);
                Set(availableValues[i], L("xuiRebirthHudTrackingUnavailable", "Unavailable"));
            }
            bool tracked = preferences != null && preferences.Contains(id.Type, id.StableId);
            Set(availableStates[i], !tracked && preferences != null && preferences.Entries.Count >= 15 ? "[C5A85F]GRID FULL[-]" : tracked ? "[8FD18F]" + L("xuiRebirthHudTrackingTracked", "TRACKED") + "[-]" : "[B8B8B8]" + L("xuiRebirthHudTrackingAvailableState", "AVAILABLE") + "[-]");
        }
        SetVisible(availableEmpty != null ? availableEmpty.Controller : null, count == 0);

    }

    private static bool TryResolve(RebirthHudTrackingId id, RebirthSurvivorOwnerStateSnapshot owner, out RebirthHudTrackDisplay display)
    {
        display = null;
        if (id == null) return false;
        IRebirthHudTrackProvider provider;
        return RebirthHudTrackingRegistry.TryGetProvider(id.Type, out provider) && provider != null && provider.TryResolveDisplay(id.StableId, owner, out display) && display != null;
    }

    private void RefreshContentSizes(RebirthHudTrackingPreferences preferences)
    {
        // Size changes are primarily handled while lists render. This method exists as the common
        // layout-change extension point used by Chunk D/E when measured HUD geometry is reported.
        RefreshHeader(preferences);
    }

    private void ResetScrollPositions()
    {
        if (trackedContent != null && trackedContent.ViewComponent != null) trackedContent.ViewComponent.Position = new Vector2i(0, 0);
        availableList?.ResetPosition();
    }

    private void Subscribe()
    {
        if (subscribed) return;
        RebirthHudTrackingPreferenceService.PreferencesChanged += PreferencesChanged;
        RebirthHudTrackingLayoutMetrics.TrackerEnvelopeChanged += TrackerLayoutChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        RebirthHudTrackingPreferenceService.PreferencesChanged -= PreferencesChanged;
        RebirthHudTrackingLayoutMetrics.TrackerEnvelopeChanged -= TrackerLayoutChanged;
        subscribed = false;
    }

    private void PreferencesChanged() { dataDirty = headerDirty = true; }
    private void TrackerLayoutChanged() { headerDirty = true; }

    private XUiController Wire(string id, XUiEvent_OnPressEventHandler handler)
    {
        XUiController c = GetChildById(id);
        if (c != null && handler != null)
        {
            XUiEvent_OnPressEventHandler wrapped = delegate(XUiController sender, int mouseButton)
            { if (!RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) handler(sender, mouseButton); };
            c.OnPress += wrapped;
            wired.Add(new KeyValuePair<XUiController, XUiEvent_OnPressEventHandler>(c, wrapped));
        }
        return c;
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

    private static string TypeLabel(RebirthHudTrackType type)
    {
        switch (type)
        {
            case RebirthHudTrackType.Skill: return L("xuiRebirthHudTrackingSkills", "SKILLS");
            case RebirthHudTrackType.Attribute: return L("xuiRebirthHudTrackingAttributes", "ATTRIBUTES");
            case RebirthHudTrackType.Knowledge: return L("xuiRebirthHudTrackingKnowledge", "KNOWLEDGE");
            case RebirthHudTrackType.Summary: return L("xuiRebirthHudTrackingSummary", "SUMMARY");
            default: return type.ToString().ToUpperInvariant();
        }
    }

    private static string L(string key, string fallback) { return RebirthUiProjectionTextCache.L(key, fallback); }
}
