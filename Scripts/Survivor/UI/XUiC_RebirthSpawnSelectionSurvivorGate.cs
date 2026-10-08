using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// V3.2 first-entry Survivor routing bridge attached to the native spawnselection group.
/// The old embedded profile browser is retained only as dormant authored compatibility data;
/// mandatory Rebirth first-entry selection is redirected to the proven full-screen Survivor
/// Profiles window before the native spawn screen is allowed to remain open.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthSpawnSelectionSurvivorGate : XUiController
{
    private const int VisibleRows = 4;
    private const int ProgressionVisibleRows = 10;
    private const int TraitVisibleRows = 7;
    private const int WeaknessVisibleRows = 2;
    private const int StartingItemVisibleRows = 6;
    private const int TraitBaseIndex = ProgressionVisibleRows;
    private const int WeaknessBaseIndex = ProgressionVisibleRows + TraitVisibleRows;
    private const int DetailRows = ProgressionVisibleRows + TraitVisibleRows + WeaknessVisibleRows;
    private const int TraitTrackHeight = 176;
    private const int ProfileTrackHeight = 408;
    private const int ProgressionTrackHeight = 250;
    private const int WeaknessTrackHeight = 54;
    private const int StartingItemTrackHeight = 162;
    private const string NativeSpawnWindow = "spawnselection";

    private readonly XUiController[] rowContainers = new XUiController[VisibleRows];
    private readonly XUiController[] rowButtons = new XUiController[VisibleRows];
    private readonly XUiV_Label[] rowNames = new XUiV_Label[VisibleRows];
    private readonly XUiV_Label[] rowSummaries = new XUiV_Label[VisibleRows];
    private readonly XUiV_Label[] rowStatuses = new XUiV_Label[VisibleRows];
    private readonly XUiController[] rowSelections = new XUiController[VisibleRows];
    private readonly XUiController[] rowArt = new XUiController[VisibleRows];
    private readonly XUiController[] rowArtPlaceholder = new XUiController[VisibleRows];
    private readonly RebirthSurvivorArtTextureBinder[] rowArtBinders = new RebirthSurvivorArtTextureBinder[VisibleRows];
    private readonly Dictionary<XUiController, int> rowIndex = new Dictionary<XUiController, int>();

    private readonly XUiController[] detailRows = new XUiController[DetailRows];
    private readonly XUiV_Sprite[] detailIcons = new XUiV_Sprite[DetailRows];
    private readonly XUiV_Label[] detailNames = new XUiV_Label[DetailRows];
    private readonly XUiV_Label[] detailValues = new XUiV_Label[DetailRows];
    private readonly XUiController[] startingItemRows = new XUiController[StartingItemVisibleRows];
    private readonly XUiV_Sprite[] startingItemIcons = new XUiV_Sprite[StartingItemVisibleRows];
    private readonly XUiV_Label[] startingItemNames = new XUiV_Label[StartingItemVisibleRows];
    private readonly XUiV_Label[] startingItemMeta = new XUiV_Label[StartingItemVisibleRows];

    private RebirthSurvivorProfile[] profiles = new RebirthSurvivorProfile[0];
    private string selectedProfileId = string.Empty;
    private int offset;
    private int progressionOffset;
    private int traitOffset;
    private int weaknessOffset;
    private int startingItemOffset;
    private int selectedProgressionDisplayCount;
    private int selectedTraitDisplayCount;
    private int selectedWeaknessDisplayCount;
    private int selectedStartingItemDisplayCount;
    private string status = string.Empty;
    private float nextRefreshTime;

    private XUiV_Label lblPage;
    private XUiV_Label lblSelectedTitle;
    private XUiV_Label lblSelectedSubtitle;
    private XUiV_Label lblSelectedDescription;
    private XUiV_Label lblSelectedIdentityMeta;
    private XUiV_Label lblStatus;
    private XUiController selectedArt;
    private XUiController selectedArtPlaceholder;
    private RebirthSurvivorArtTextureBinder selectedArtBinder;
    private XUiController selectedPlayerPreview;
    private RebirthPlayerProfileModelBinder selectedPlayerModelBinder;
    private string previewPlayerProfileName = string.Empty;

    private XUiController btnCreate;
    private XUiController btnSpawnModeRandom;
    private XUiController btnSpawnModeFriend;
    private XUiController btnSpawnAction;
    private XUiController btnCancel;
    private XUiController nativeSpawnFirstTime;
    private XUiController nativeSpawn;
    private XUiController nativeSpawnNearFriend;
    private XUiController nativeNearFriendsBox;
    private XUiController nativeLeave;
    private XUiController nativeGroup;
    private XUiController nativeButtonsRect;
    private XUiController nativeHeaderRect;
    private XUiController nativeContentTable;

    private bool nativeLayoutCaptured;
    private Vector2i nativeGroupPosition;
    private Vector2i nativeGroupSize;
    private Vector2i nativeButtonsPosition;
    private Vector2i nativeButtonsSize;
    private Vector2i nativeHeaderPosition;
    private Vector2i nativeHeaderSize;
    private Vector2i nativeHeaderLabelSize;
    private Vector2i nativeContentPosition;
    private bool nativeContentVisible;

    private XUiController profileNativeScrollHost;
    private XUiController profileNativeScrollProxy;
    private XUiController profileNativeScrollView;
    private bool syncingProfileNativeScroll;
    private XUiController progressionNativeScrollHost;
    private XUiController progressionNativeScrollProxy;
    private XUiController progressionNativeScrollView;
    private bool syncingProgressionNativeScroll;
    private XUiController weaknessNativeScrollHost;
    private XUiController weaknessNativeScrollProxy;
    private XUiController weaknessNativeScrollView;
    private bool syncingWeaknessNativeScroll;
    private XUiController startingItemNativeScrollHost;
    private XUiController startingItemNativeScrollProxy;
    private XUiController startingItemNativeScrollView;
    private bool syncingStartingItemNativeScroll;
    private XUiController traitNativeScrollHost;
    private XUiController traitNativeScrollProxy;
    private XUiController traitNativeScrollView;
    private XUiController traitScrollCapture;
    private bool syncingTraitNativeScroll;
    private int traitNativeInitialSyncFrames;

    private bool lastVisible;
    private bool lastLocked;
    private bool nativeFirstTimeEnabled;
    private bool nativeSpawnEnabled;
    private bool nativeFirstTimeVisible;
    private bool nativeSpawnVisible;
    private bool nativeStatesCaptured;
    private bool nativeFirstTimePressWired;
    private bool nativeSpawnPressWired;
    private bool nativeFriendSpawnPressWired;
    private bool nativeNearFriendVisibleCaptured;
    private bool nativeNearFriendVisible;
    private bool nativeLeaveVisibleCaptured;
    private bool nativeLeaveVisible;
    private bool spawnNearFriendMode;
    private bool nativeEscStateCaptured;
    private bool nativeEscClosable;
    private const int StandaloneRedirectMaxAttempts = 20;
    private const float StandaloneRedirectRetrySeconds = 0.25f;
    private bool standaloneRedirectPending;
    private bool standaloneRedirectExhausted;
    private int standaloneRedirectAttempts;
    private float nextStandaloneRedirectRetry;
    private string standaloneRedirectReason = string.Empty;

    private static XUiC_RebirthSpawnSelectionSurvivorGate activeInstance;
    private static string preferredProfileId = string.Empty;
    private static string profileBeforeCreator = string.Empty;

    public static bool IsGateVisible
    {
        get { return XUiC_RebirthSurvivorProfileManager.IsSpawnSelectionMode; }
    }

    public static bool SetGateVisible(bool visible, string reason)
    {
        XUiC_RebirthSpawnSelectionSurvivorGate instance = activeInstance;
        if (instance == null || instance.ViewComponent == null) return false;

        bool effective = visible &&
            RebirthSurvivorMode.IsEnabledForCurrentWorld() &&
            RebirthSurvivorFirstEntryUiService.ShouldShowSpawnProfileSelector();

        // V3.2 first-entry selection is intentionally a standalone full-screen Survivor Profiles
        // step BEFORE the native spawn screen. The former embedded 1460x900 browser inherited the
        // stock spawnselection/buttons transform and became badly clipped at 1920x1080.
        instance.ViewComponent.IsVisible = false;
        if (!effective)
        {
            instance.ApplyNativeSpawnLock(false, reason);
            return true;
        }

        instance.ApplyNativeSpawnLock(true, reason);
        instance.RequestStandaloneSpawnSelector(reason);
        return true;
    }

    public override void Init()
    {
        base.Init();

        for (int i = 0; i < VisibleRows; i++)
        {
            string n = i.ToString(CultureInfo.InvariantCulture);
            rowContainers[i] = GetChildById("spawnProfileRow" + n);
            rowButtons[i] = GetChildById("btnSpawnProfileRow" + n);
            rowNames[i] = Label("spawnProfileName" + n);
            rowSummaries[i] = Label("spawnProfileSummary" + n);
            rowStatuses[i] = Label("spawnProfileStatus" + n);
            rowSelections[i] = GetChildById("spawnProfileSelection" + n);
            rowArt[i] = GetChildById("spawnProfileArt" + n);
            rowArtPlaceholder[i] = GetChildById("spawnProfileArtPlaceholder" + n);
            rowArtBinders[i] = new RebirthSurvivorArtTextureBinder(rowArt[i], 1f);
            if (rowButtons[i] != null)
            {
                rowIndex[rowButtons[i]] = i;
                rowButtons[i].OnPress += Row_OnPressed;
                WireScroll(rowButtons[i], ProfileListScroll);
            }
        }

        for (int i = 0; i < DetailRows; i++)
        {
            string n = i.ToString(CultureInfo.InvariantCulture);
            detailRows[i] = GetChildById("spawnProfileDetailRow" + n);
            detailIcons[i] = Sprite("spawnProfileDetailIcon" + n);
            detailNames[i] = Label("spawnProfileDetailName" + n);
            detailValues[i] = Label("spawnProfileDetailValue" + n);
        }

        for (int i = 0; i < StartingItemVisibleRows; i++)
        {
            string n = i.ToString(CultureInfo.InvariantCulture);
            startingItemRows[i] = GetChildById("spawnProfileStartingItemRow" + n);
            startingItemIcons[i] = Sprite("spawnProfileStartingItemIcon" + n);
            startingItemNames[i] = Label("spawnProfileStartingItemName" + n);
            startingItemMeta[i] = Label("spawnProfileStartingItemMeta" + n);
        }

        btnCreate = Wire("btnSpawnProfileCreate", Create_OnPressed);
        btnSpawnModeRandom = Wire("btnRebirthSpawnModeRandom", SpawnModeRandom_OnPressed);
        btnSpawnModeFriend = Wire("btnRebirthSpawnModeFriend", SpawnModeFriend_OnPressed);
        btnSpawnAction = Wire("btnRebirthSpawnAction", SpawnAction_OnPressed);
        btnCancel = Wire("btnRebirthSpawnCancel", Cancel_OnPressed);

        lblPage = Label("spawnProfilePageText");
        lblSelectedTitle = Label("spawnSelectedProfileTitle");
        lblSelectedSubtitle = Label("spawnSelectedProfileSubtitle");
        lblSelectedDescription = Label("spawnSelectedProfileDescription");
        lblSelectedIdentityMeta = Label("spawnSelectedProfileIdentityMeta");
        lblStatus = Label("spawnProfileStatusText");
        selectedArt = GetChildById("spawnSelectedProfileArt");
        selectedArtPlaceholder = GetChildById("spawnSelectedProfileArtPlaceholder");
        selectedArtBinder = new RebirthSurvivorArtTextureBinder(selectedArt, 1f);
        selectedPlayerPreview = GetChildById("spawnSelectedPlayerPreview");
        selectedPlayerModelBinder = new RebirthPlayerProfileModelBinder(GetChildById("spawnPlayerModelTexture"));

        profileNativeScrollHost = GetChildById("spawnProfileNativeScrollHost");
        profileNativeScrollProxy = GetChildById("spawnProfileNativeScrollProxy");
        profileNativeScrollView = GetChildById("spawnProfileNativeScrollView");
        WireScroll(profileNativeScrollHost, ProfileListScroll);
        WireScroll(profileNativeScrollView, ProfileListScroll);
        WireScroll(profileNativeScrollProxy, ProfileListScroll);

        progressionNativeScrollHost = GetChildById("spawnProfileProgressionNativeScrollHost");
        progressionNativeScrollProxy = GetChildById("spawnProfileProgressionNativeScrollProxy");
        progressionNativeScrollView = GetChildById("spawnProfileProgressionNativeScrollView");
        WireScroll(progressionNativeScrollHost, ProgressionListScroll);
        WireScroll(progressionNativeScrollView, ProgressionListScroll);
        WireScroll(progressionNativeScrollProxy, ProgressionListScroll);
        for (int i = 0; i < ProgressionVisibleRows; i++) WireScroll(detailRows[i], ProgressionListScroll);

        weaknessNativeScrollHost = GetChildById("spawnProfileWeaknessNativeScrollHost");
        weaknessNativeScrollProxy = GetChildById("spawnProfileWeaknessNativeScrollProxy");
        weaknessNativeScrollView = GetChildById("spawnProfileWeaknessNativeScrollView");
        WireScroll(weaknessNativeScrollHost, WeaknessListScroll);
        WireScroll(weaknessNativeScrollView, WeaknessListScroll);
        WireScroll(weaknessNativeScrollProxy, WeaknessListScroll);
        for (int i = WeaknessBaseIndex; i < WeaknessBaseIndex + WeaknessVisibleRows; i++) WireScroll(detailRows[i], WeaknessListScroll);

        startingItemNativeScrollHost = GetChildById("spawnProfileStartingItemNativeScrollHost");
        startingItemNativeScrollProxy = GetChildById("spawnProfileStartingItemNativeScrollProxy");
        startingItemNativeScrollView = GetChildById("spawnProfileStartingItemNativeScrollView");
        WireScroll(startingItemNativeScrollHost, StartingItemListScroll);
        WireScroll(startingItemNativeScrollView, StartingItemListScroll);
        WireScroll(startingItemNativeScrollProxy, StartingItemListScroll);
        for (int i = 0; i < StartingItemVisibleRows; i++) WireScroll(startingItemRows[i], StartingItemListScroll);

        traitNativeScrollHost = GetChildById("spawnProfileTraitNativeScrollHost");
        traitNativeScrollProxy = GetChildById("spawnProfileTraitNativeScrollProxy");
        traitNativeScrollView = GetChildById("spawnProfileTraitNativeScrollView");
        traitScrollCapture = GetChildById("spawnProfileTraitScrollCapture");
        WireScroll(traitNativeScrollHost, TraitListScroll);
        WireScroll(traitNativeScrollView, TraitListScroll);
        WireScroll(traitNativeScrollProxy, TraitListScroll);
        WireScroll(traitScrollCapture, TraitListScroll);
        for (int i = TraitBaseIndex; i < TraitBaseIndex + TraitVisibleRows; i++)
            WireScroll(detailRows[i], TraitListScroll);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        activeInstance = this;
        standaloneRedirectPending = false;
        standaloneRedirectExhausted = false;
        standaloneRedirectAttempts = 0;
        nextStandaloneRedirectRetry = 0f;
        standaloneRedirectReason = string.Empty;
        nativeStatesCaptured = false;
        nativeEscStateCaptured = false;
        nativeLayoutCaptured = false;
        RebirthSurvivorFirstEntryUiService.RegisterNativeSpawnSelectionHost(xui);
        ResolveNativeControls();
        CaptureNativeEscState();
        RebirthSurvivorArtTextureBinder.PreloadBackgroundThumbnails();

        previewPlayerProfileName = string.Empty;

        RebirthSurvivorProfileStore.Refresh();
        RefreshProfiles(true);
        offset = ClampOffset(offset);
        progressionOffset = 0;
        traitOffset = 0;
        weaknessOffset = 0;
        startingItemOffset = 0;
        traitNativeInitialSyncFrames = 2;
        // Never render the old embedded selector in V3.2. If first-entry selection is required,
        // redirect to the proven full-screen Survivor Profiles window before exposing native spawn.
        if (ViewComponent != null) ViewComponent.IsVisible = false;
        bool needsSelection = RebirthSurvivorFirstEntryUiService.ShouldShowSpawnProfileSelector();
        if (needsSelection)
        {
            ApplyNativeSpawnLock(true, "OnOpen");
            RequestStandaloneSpawnSelector("OnOpen");
        }
        else
        {
            ApplyNativeSpawnLock(false, "OnOpen-ready");
        }
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection first-entry redirect OnOpen required=" + needsSelection
            + " selectableProfiles=" + profiles.Length
            + " staged=" + RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection); }
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (ProcessStandaloneSpawnSelectorRequest()) return;
        for (int i = 0; i < rowArtBinders.Length; i++) if (rowArtBinders[i] != null) rowArtBinders[i].Update();
        if (selectedArtBinder != null) selectedArtBinder.Update();
        RebirthNativePlayerProfileBridge.Tick(xui);

        if (traitNativeInitialSyncFrames > 0)
        {
            traitNativeInitialSyncFrames--;
            UpdateTraitNativeScroll();
        }
        PollProfileNativeScroll();
        PollProgressionNativeScroll();
        PollTraitNativeScroll();
        PollWeaknessNativeScroll();
        PollStartingItemNativeScroll();

        float now = Time.realtimeSinceStartup;
        if (now >= nextRefreshTime)
        {
            nextRefreshTime = now + 0.25f;
            RefreshState("refresh");
        }
        handleDirtyUpdateDefault();
    }

    public override void OnClose()
    {
        standaloneRedirectPending = false;
        standaloneRedirectExhausted = false;
        standaloneRedirectAttempts = 0;
        nextStandaloneRedirectRetry = 0f;
        standaloneRedirectReason = string.Empty;
        ApplyNativeSpawnLock(false, "OnClose");
        RestoreNativeEscState();
        for (int i = 0; i < rowArtBinders.Length; i++) if (rowArtBinders[i] != null) rowArtBinders[i].Clear();
        if (selectedArtBinder != null) selectedArtBinder.Clear();

        if (selectedPlayerModelBinder != null) selectedPlayerModelBinder.Clear();
        previewPlayerProfileName = string.Empty;
        if (object.ReferenceEquals(activeInstance, this)) activeInstance = null;
        base.OnClose();
    }

    private void RefreshState(string reason)
    {
        bool required = RebirthSurvivorFirstEntryUiService.ShouldShowSpawnProfileSelector();
        if (ViewComponent != null) ViewComponent.IsVisible = false;
        lastVisible = false;
        if (!required)
        {
            ApplyNativeSpawnLock(false, reason);
            return;
        }

        ApplyNativeSpawnLock(true, reason);
        RequestStandaloneSpawnSelector(reason);
    }

    private void SetVisibilityInternal(bool visible, string reason)
    {
        if (ViewComponent != null) ViewComponent.IsVisible = false;
        lastVisible = false;
        if (visible)
        {
            ApplyNativeSpawnLock(true, reason);
            RequestStandaloneSpawnSelector(reason);
        }
        else
        {
            ApplyNativeSpawnLock(false, reason);
        }
    }

    private void RequestStandaloneSpawnSelector(string reason)
    {
        if (RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection) return;
        if (StandaloneSpawnSelectorQueuedOrShowing())
        {
            standaloneRedirectPending = false;
            standaloneRedirectReason = string.Empty;
            return;
        }
        if (standaloneRedirectPending || standaloneRedirectExhausted) return;
        standaloneRedirectPending = true;
        standaloneRedirectAttempts = 0;
        nextStandaloneRedirectRetry = 0f;
        standaloneRedirectReason = reason ?? "unspecified";
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("queued Survivor Profiles first-entry redirect reason=" + standaloneRedirectReason); }
    }

    private bool StandaloneSpawnSelectorQueuedOrShowing()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return false;
        GUIWindowManager manager = xui.playerUI.windowManager;
        if (manager.IsWindowOpen(XUiC_RebirthSurvivorProfileManager.WindowGroupId)) return true;
        XUiController group = xui.FindWindowGroupByName(XUiC_RebirthSurvivorProfileManager.WindowGroupId);
        return group != null && group.windowGroup != null && group.windowGroup.isShowing &&
            group.ViewComponent != null && group.ViewComponent.IsVisible;
    }

    private bool ProcessStandaloneSpawnSelectorRequest()
    {
        if (!standaloneRedirectPending || Time.realtimeSinceStartup < nextStandaloneRedirectRetry) return false;
        standaloneRedirectPending = false;
        string reason = standaloneRedirectReason;

        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() ||
            !RebirthSurvivorFirstEntryUiService.ShouldShowSpawnProfileSelector() ||
            RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection)
            return false;

        // Execute from Update, one stable window-manager frame after spawnselection.OnOpen.
        // Survivor Profiles now overlays the native window instead of replacing it, preserving the
        // V3.2 loading-screen -> spawnselection lifecycle underneath.
        bool opened = OpenStandaloneSpawnSelector("deferred:" + (reason ?? "unspecified"));
        if (opened)
        {
            standaloneRedirectReason = string.Empty;
            standaloneRedirectAttempts = 0;
            return true;
        }
        if (RebirthSurvivorFirstEntryUiService.ShouldShowSpawnProfileSelector())
        {
            standaloneRedirectAttempts++;
            if (standaloneRedirectAttempts < StandaloneRedirectMaxAttempts)
            {
                standaloneRedirectPending = true;
                nextStandaloneRedirectRetry = Time.realtimeSinceStartup + StandaloneRedirectRetrySeconds;
            }
            else
            {
                standaloneRedirectExhausted = true;
                Log.Error("[REBIRTH Survivor][PreSpawnTrace] Survivor Profiles redirect exhausted " +
                    StandaloneRedirectMaxAttempts + " bounded attempts; native spawn remains locked reason=" +
                    (standaloneRedirectReason ?? "unspecified"));
            }
        }
        return false;
    }

    private bool OpenStandaloneSpawnSelector(string reason)
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return false;
        if (RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection) return false;

        GUIWindowManager manager = xui.playerUI.windowManager;
        XUiController profilesGroup = xui.FindWindowGroupByName(XUiC_RebirthSurvivorProfileManager.WindowGroupId);
        if (profilesGroup == null || profilesGroup.windowGroup == null)
        {
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] mandatory Survivor Profiles group is not registered; native spawn remains locked.");
            return false;
        }

        if (manager.IsWindowOpen(XUiC_RebirthSurvivorProfileManager.WindowGroupId)) return true;

        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("overlaying full-screen Survivor Profiles above native spawnselection reason=" +
            (reason ?? "unspecified")); }
        XUiC_RebirthSurvivorProfileManager.OpenForSpawnSelection(xui);
        return manager.IsWindowOpen(XUiC_RebirthSurvivorProfileManager.WindowGroupId);
    }

    private void ResolveNativeControls()
    {
        nativeGroup = xui != null ? xui.FindWindowGroupByName(NativeSpawnWindow) : null;
        if (nativeGroup == null) return;
        nativeButtonsRect = nativeGroup.GetChildById("buttons");
        nativeHeaderRect = nativeGroup.GetChildById("header");
        nativeContentTable = nativeGroup.GetChildById("content");
        nativeSpawnFirstTime = nativeGroup.GetChildById("btnSpawnFirstTime");
        nativeSpawn = nativeGroup.GetChildById("btnSpawn");
        nativeSpawnNearFriend = nativeGroup.GetChildById("btnSpawnNearFriend");
        nativeNearFriendsBox = nativeGroup.GetChildById("boxSpawnNearFriend");
        nativeLeave = nativeGroup.GetChildById("btnLeave");

        if (!nativeFirstTimePressWired && nativeSpawnFirstTime != null)
        {
            nativeSpawnFirstTime.OnPress += NativeSpawn_OnPressed;
            nativeFirstTimePressWired = true;
        }
        if (!nativeSpawnPressWired && nativeSpawn != null)
        {
            nativeSpawn.OnPress += NativeSpawn_OnPressed;
            nativeSpawnPressWired = true;
        }
        if (!nativeFriendSpawnPressWired && nativeSpawnNearFriend != null)
        {
            nativeSpawnNearFriend.OnPress += NativeSpawn_OnPressed;
            nativeFriendSpawnPressWired = true;
        }
    }

    private void NativeSpawn_OnPressed(XUiController sender, int mouseButton)
    {
        string button = object.ReferenceEquals(sender, nativeSpawnFirstTime) ? "btnSpawnFirstTime" :
            (object.ReferenceEquals(sender, nativeSpawn) ? "btnSpawn" :
            (object.ReferenceEquals(sender, nativeSpawnNearFriend) ? "btnSpawnNearFriend" : "unknown"));
        bool enabled = sender != null && sender.ViewComponent != null && sender.ViewComponent.Enabled;
        bool visible = sender != null && sender.ViewComponent != null && sender.ViewComponent.IsVisible;
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("native spawn button PRESS button=" + button
            + " mouse=" + mouseButton
            + " locked=" + lastLocked
            + " staged=" + RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection
            + " profileId=" + (RebirthSurvivorFirstEntryUiService.StagedPreSpawnProfileId ?? string.Empty)
            + " enabled=" + enabled
            + " visible=" + visible
            + " spawnWindowOpen=" + RebirthSurvivorFirstEntryUiService.IsNativeSpawnSelectionOpenForDiagnostics()); }
        if (lastLocked || !RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection)
            Log.Error("[REBIRTH Survivor][SpawnFlow] native Spawn received input while Survivor selection was locked button=" + button);
    }

    private void CaptureNativeEscState()
    {
        if (nativeEscStateCaptured || windowGroup == null) return;
        nativeEscClosable = windowGroup.isEscClosable;
        nativeEscStateCaptured = true;
    }

    private void ApplyEscOwnership(bool active)
    {
        if (windowGroup == null) return;
        CaptureNativeEscState();
        windowGroup.isEscClosable = active ? false : nativeEscClosable;
    }

    private void RestoreNativeEscState()
    {
        if (windowGroup != null && nativeEscStateCaptured) windowGroup.isEscClosable = nativeEscClosable;
    }

    private void CaptureNativeLayout()
    {
        if (nativeLayoutCaptured) return;
        if (nativeGroup == null) ResolveNativeControls();
        if (nativeGroup == null || nativeGroup.ViewComponent == null) return;

        nativeGroupPosition = nativeGroup.ViewComponent.Position;
        nativeGroupSize = nativeGroup.ViewComponent.Size;

        if (nativeButtonsRect != null && nativeButtonsRect.ViewComponent != null)
        {
            nativeButtonsPosition = nativeButtonsRect.ViewComponent.Position;
            nativeButtonsSize = nativeButtonsRect.ViewComponent.Size;
        }
        if (nativeHeaderRect != null && nativeHeaderRect.ViewComponent != null)
        {
            nativeHeaderPosition = nativeHeaderRect.ViewComponent.Position;
            nativeHeaderSize = nativeHeaderRect.ViewComponent.Size;
            XUiV_Label headerLabel = FindFirstLabel(nativeHeaderRect);
            if (headerLabel != null) nativeHeaderLabelSize = headerLabel.Size;
        }
        if (nativeContentTable != null && nativeContentTable.ViewComponent != null)
        {
            nativeContentPosition = nativeContentTable.ViewComponent.Position;
            nativeContentVisible = nativeContentTable.ViewComponent.IsVisible;
        }

        nativeLayoutCaptured = true;
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("spawnselection captured native layout pos=" + nativeGroupPosition
            + " size=" + nativeGroupSize
            + " buttonsPos=" + nativeButtonsPosition
            + " buttonsSize=" + nativeButtonsSize
            + " contentPos=" + nativeContentPosition); }
    }

    private void ApplyNativeLayout(bool expanded)
    {
        if (nativeGroup == null) ResolveNativeControls();
        if (nativeGroup == null) return;
        CaptureNativeLayout();

        if (expanded)
        {
            // Expand only while mandatory first-entry Rebirth Survivor selection is active.
            SetRect(nativeGroup, -730, 505, 1460, 1010);
            if (nativeButtonsRect != null) SetRect(nativeButtonsRect, 0, 0, 1460, 1010);
            if (nativeHeaderRect != null)
            {
                SetRect(nativeHeaderRect, 0, 0, 1460, 43);
                XUiV_Label headerLabel = FindFirstLabel(nativeHeaderRect);
                if (headerLabel != null) headerLabel.Size = new Vector2i(1450, 43);
            }
            if (nativeContentTable != null && nativeContentTable.ViewComponent != null)
            {
                nativeContentTable.ViewComponent.Position = new Vector2i(810, -665);
                nativeContentTable.ViewComponent.IsVisible = spawnNearFriendMode && NativeFriendsAvailable();
            }
            ApplyNativeActionPresentation(true);
            return;
        }

        if (!nativeLayoutCaptured) return;

        // Restore the exact native geometry captured from V3.2. Never approximate it.
        if (nativeGroup.ViewComponent != null)
        {
            nativeGroup.ViewComponent.Position = nativeGroupPosition;
            nativeGroup.ViewComponent.Size = nativeGroupSize;
        }
        if (nativeButtonsRect != null && nativeButtonsRect.ViewComponent != null)
        {
            nativeButtonsRect.ViewComponent.Position = nativeButtonsPosition;
            nativeButtonsRect.ViewComponent.Size = nativeButtonsSize;
        }
        if (nativeHeaderRect != null && nativeHeaderRect.ViewComponent != null)
        {
            nativeHeaderRect.ViewComponent.Position = nativeHeaderPosition;
            nativeHeaderRect.ViewComponent.Size = nativeHeaderSize;
            XUiV_Label headerLabel = FindFirstLabel(nativeHeaderRect);
            if (headerLabel != null) headerLabel.Size = nativeHeaderLabelSize;
        }
        if (nativeContentTable != null && nativeContentTable.ViewComponent != null)
        {
            nativeContentTable.ViewComponent.Position = nativeContentPosition;
            nativeContentTable.ViewComponent.IsVisible = nativeContentVisible;
        }

        // Native spawn action visibility remains owned by the stock spawnselection controller.
        // Do not restore a stale snapshot of button visibility here.
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("spawnselection restored exact captured native geometry"); }
    }

    private void ApplyNativeActionPresentation(bool integrated)
    {
        if (nativeGroup == null) return;

        if (!nativeNearFriendVisibleCaptured && nativeSpawnNearFriend != null && nativeSpawnNearFriend.ViewComponent != null)
        {
            nativeNearFriendVisible = nativeSpawnNearFriend.ViewComponent.IsVisible;
            nativeNearFriendVisibleCaptured = true;
        }
        if (!nativeLeaveVisibleCaptured && nativeLeave != null && nativeLeave.ViewComponent != null)
        {
            nativeLeaveVisible = nativeLeave.ViewComponent.IsVisible;
            nativeLeaveVisibleCaptured = true;
        }

        if (integrated)
        {
            // Native action controllers remain alive as dispatch targets, but only the integrated
            // CREATE / CANCEL / SPAWN controls are visible to the player.
            SetNativeVisible(nativeSpawnFirstTime, false);
            SetNativeVisible(nativeSpawn, false);
            SetNativeVisible(nativeSpawnNearFriend, false);
            SetNativeVisible(nativeLeave, false);
        }
        else
        {
            if (nativeSpawnFirstTime != null && nativeSpawnFirstTime.ViewComponent != null)
                nativeSpawnFirstTime.ViewComponent.IsVisible = nativeFirstTimeVisible;
            if (nativeSpawn != null && nativeSpawn.ViewComponent != null)
                nativeSpawn.ViewComponent.IsVisible = nativeSpawnVisible;
            if (nativeSpawnNearFriend != null && nativeSpawnNearFriend.ViewComponent != null && nativeNearFriendVisibleCaptured)
                nativeSpawnNearFriend.ViewComponent.IsVisible = nativeNearFriendVisible;
            if (nativeLeave != null && nativeLeave.ViewComponent != null && nativeLeaveVisibleCaptured)
                nativeLeave.ViewComponent.IsVisible = nativeLeaveVisible;
        }
    }

    private static void SetNativeVisible(XUiController controller, bool visible)
    {
        if (controller != null && controller.ViewComponent != null)
            controller.ViewComponent.IsVisible = visible;
    }

    private bool NativeFriendsAvailable()
    {
        return nativeNearFriendsBox != null && nativeNearFriendsBox.ViewComponent != null &&
            nativeNearFriendsBox.ViewComponent.IsVisible;
    }

    private static void SetRect(XUiController c, int x, int y, int width, int height)
    {
        if (c == null || c.ViewComponent == null) return;
        c.ViewComponent.Position = new Vector2i(x, y);
        c.ViewComponent.Size = new Vector2i(width, height);
    }

    private static XUiV_Label FindFirstLabel(XUiController node)
    {
        if (node == null) return null;
        XUiV_Label own = node.ViewComponent as XUiV_Label;
        if (own != null) return own;
        if (node.Children == null) return null;
        for (int i = 0; i < node.Children.Count; i++)
        {
            XUiV_Label found = FindFirstLabel(node.Children[i]);
            if (found != null) return found;
        }
        return null;
    }

    private void ApplyNativeSpawnLock(bool locked, string reason)
    {
        if (nativeSpawnFirstTime == null && nativeSpawn == null) ResolveNativeControls();

        if (!nativeStatesCaptured)
        {
            nativeFirstTimeEnabled = nativeSpawnFirstTime != null && nativeSpawnFirstTime.ViewComponent != null && nativeSpawnFirstTime.ViewComponent.Enabled;
            nativeSpawnEnabled = nativeSpawn != null && nativeSpawn.ViewComponent != null && nativeSpawn.ViewComponent.Enabled;
            nativeFirstTimeVisible = nativeSpawnFirstTime != null && nativeSpawnFirstTime.ViewComponent != null && nativeSpawnFirstTime.ViewComponent.IsVisible;
            nativeSpawnVisible = nativeSpawn != null && nativeSpawn.ViewComponent != null && nativeSpawn.ViewComponent.IsVisible;
            nativeStatesCaptured = true;
        }

        // The real native spawn actions are the authoritative execution path. In the integrated
        // Rebirth layout they are hidden, but remain disabled until a Survivor Profile has been
        // selected. No decorative blocker is used.
        if (nativeSpawnFirstTime != null && nativeSpawnFirstTime.ViewComponent != null)
            nativeSpawnFirstTime.ViewComponent.Enabled = locked ? false : nativeFirstTimeEnabled;
        if (nativeSpawn != null && nativeSpawn.ViewComponent != null)
            nativeSpawn.ViewComponent.Enabled = locked ? false : nativeSpawnEnabled;

        UpdateSpawnOptionsUi();

        if (lastLocked != locked)
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection native Spawn locked=" + locked
                + " firstTimeFound=" + (nativeSpawnFirstTime != null)
                + " firstTimeEnabled=" + (nativeSpawnFirstTime != null && nativeSpawnFirstTime.ViewComponent != null && nativeSpawnFirstTime.ViewComponent.Enabled)
                + " spawnFound=" + (nativeSpawn != null)
                + " spawnEnabled=" + (nativeSpawn != null && nativeSpawn.ViewComponent != null && nativeSpawn.ViewComponent.Enabled)
                + " friendFound=" + (nativeSpawnNearFriend != null)
                + " friendSelectionValid=" + (nativeSpawnNearFriend != null && nativeSpawnNearFriend.ViewComponent != null && nativeSpawnNearFriend.ViewComponent.Enabled)
                + " reason=" + (reason ?? "unspecified")); }
        lastLocked = locked;
    }


    private void RefreshProfiles(bool initial)
    {
        RebirthSurvivorProfile[] all = RebirthSurvivorProfileStore.GetProfilesSnapshot();
        List<RebirthSurvivorProfile> ready = new List<RebirthSurvivorProfile>();
        for (int i = 0; i < all.Length; i++)
        {
            RebirthSurvivorProfile p = all[i];
            if (p == null) continue;
            RebirthSurvivorProfileCompatibility compatibility = RebirthSurvivorProfileStore.EvaluateCompatibility(p);
            if (compatibility != null && compatibility.Kind == RebirthSurvivorProfileCompatibilityKind.Ready)
                ready.Add(p);
        }
        ready.Sort(delegate(RebirthSurvivorProfile a, RebirthSurvivorProfile b)
        {
            return string.Compare(a.ProfileName, b.ProfileName, StringComparison.CurrentCultureIgnoreCase);
        });
        profiles = ready.ToArray();

        string stagedId = RebirthSurvivorFirstEntryUiService.StagedPreSpawnProfileId;
        string desired = selectedProfileId;
        if (initial || string.IsNullOrEmpty(desired) || IndexOf(desired) < 0)
        {
            desired = !string.IsNullOrEmpty(preferredProfileId) ? preferredProfileId
                : (!string.IsNullOrEmpty(stagedId) ? stagedId : string.Empty);
        }

        if (!string.IsNullOrEmpty(desired) && IndexOf(desired) >= 0)
            selectedProfileId = desired;
        else if (profiles.Length > 0)
            selectedProfileId = profiles[0].ProfileId;
        else
            selectedProfileId = string.Empty;

        if (!string.IsNullOrEmpty(preferredProfileId) && string.Equals(selectedProfileId, preferredProfileId, StringComparison.OrdinalIgnoreCase))
            preferredProfileId = string.Empty;

        offset = ClampOffset(offset);
        EnsureSelectedVisible();
    }

    private void Render()
    {
        if (ViewComponent == null || !ViewComponent.IsVisible) return;

        for (int i = 0; i < VisibleRows; i++)
        {
            int index = offset + i;
            bool visible = index >= 0 && index < profiles.Length;
            SetVisible(rowContainers[i], visible);
            if (!visible)
            {
                if (rowArtBinders[i] != null) rowArtBinders[i].Clear();
                SetVisible(rowArt[i], false);
                SetVisible(rowArtPlaceholder[i], false);
                SetVisible(rowSelections[i], false);
                SetLabel(rowNames[i], string.Empty);
                SetLabel(rowSummaries[i], string.Empty);
                SetLabel(rowStatuses[i], string.Empty);
                continue;
            }

            RebirthSurvivorProfile profile = profiles[index];
            bool rowSelected = string.Equals(profile.ProfileId, selectedProfileId, StringComparison.OrdinalIgnoreCase);
            SetVisible(rowSelections[i], rowSelected);
            SetLabel(rowNames[i], profile.ProfileName);
            SetLabel(rowSummaries[i], BuildProfileRowSummary(profile));
            string stagedProfileId = RebirthSurvivorFirstEntryUiService.StagedPreSpawnProfileId;
            bool stagedRow = !string.IsNullOrEmpty(stagedProfileId) &&
                string.Equals(profile.ProfileId, stagedProfileId, StringComparison.OrdinalIgnoreCase);
            SetLabel(rowStatuses[i], stagedRow ? "[8FD18F]SELECTED[-]" : "[8FD18F]READY[-]");

            RebirthBackgroundDefinition bg;
            bool hasArt = RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out bg)
                && bg != null && rowArtBinders[i] != null && rowArtBinders[i].BindBackgroundThumbnail(bg);
            SetVisible(rowArt[i], hasArt);
            SetVisible(rowArtPlaceholder[i], !hasArt);
        }

        int first = profiles.Length == 0 ? 0 : offset + 1;
        int last = Math.Min(profiles.Length, offset + VisibleRows);
        SetLabel(lblPage, profiles.Length == 0
            ? "No ready Survivor Profiles. Create a new profile to continue."
            : string.Format(CultureInfo.InvariantCulture, "Profiles {0}-{1} of {2}", first, last, profiles.Length));

        RebirthSurvivorProfile selected = GetSelected();
        if (selected == null)
        {
            if (selectedArtBinder != null) selectedArtBinder.Clear();
            SetVisible(selectedArt, false);
            SetVisible(selectedArtPlaceholder, true);
            SetLabel(lblSelectedTitle, "No profile selected");
            SetLabel(lblSelectedSubtitle, string.Empty);
            SetLabel(lblSelectedDescription, "Create a reusable Survivor Profile or select one of the ready profiles on the left.");
            SetLabel(lblSelectedIdentityMeta, string.Empty);
            previewPlayerProfileName = string.Empty;
            RenderSelectedProfileDetails(null);
        }
        else
        {
            SetLabel(lblSelectedTitle, selected.ProfileName);
            SetLabel(lblSelectedSubtitle, BuildProfileSubtitle(selected));
            SetLabel(lblSelectedDescription, BuildProfileDescription(selected));
            SetLabel(lblSelectedIdentityMeta, BuildIdentityMeta(selected));
            RebirthBackgroundDefinition bg;
            bool hasArt = RebirthSurvivorDefinitionRegistry.TryGetBackground(selected.BackgroundId, out bg)
                && bg != null && selectedArtBinder != null && selectedArtBinder.BindBackgroundThumbnail(bg);
            SetVisible(selectedArt, hasArt);
            SetVisible(selectedArtPlaceholder, !hasArt);
            BindSelectedPlayerPreview(selected);
            RenderSelectedProfileDetails(selected);
        }

        SetInteractive(btnCreate, true);
        UpdateProfileNativeScroll();

        if (RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection)
        {
            string stagedName = RebirthSurvivorFirstEntryUiService.StagedPreSpawnProfileName;
            SetLabel(lblStatus, "[8FD18F]SELECTED FOR THIS WORLD: " +
                (string.IsNullOrEmpty(stagedName) ? "SURVIVOR PROFILE" : stagedName) +
                "[-]   Click another profile at any time to change it.");
        }
        else if (!string.IsNullOrEmpty(status))
        {
            SetLabel(lblStatus, status);
        }
        else if (profiles.Length == 0)
        {
            SetLabel(lblStatus, "[D6C978]Create a new ready Survivor Profile before spawning.[-]");
        }
        else
        {
            SetLabel(lblStatus, "[D6C978]Click a READY profile to select it for this world. You can change it before spawning.[-]");
        }

        UpdateSpawnOptionsUi();
    }

    private void Row_OnPressed(XUiController sender, int mouseButton)
    {
        int local;
        if (!rowIndex.TryGetValue(sender, out local)) return;
        int index = offset + local;
        if (index < 0 || index >= profiles.Length) return;

        selectedProfileId = profiles[index].ProfileId;
        traitOffset = 0;
        traitNativeInitialSyncFrames = 2;
        status = string.Empty;

        // The approved spawn-selection design uses direct row selection. Clicking another READY
        // profile changes the staged world profile immediately; there is no sticky "first selected"
        // row and no second SELECT button to fight the user's highlight.
        StageSelectedProfile(profiles[index]);
        Render();
    }

    private void StageSelectedProfile(RebirthSurvivorProfile profile)
    {
        if (profile == null) return;

        RebirthSurvivorProfileCompatibility compatibility = RebirthSurvivorProfileStore.EvaluateCompatibility(profile);
        if (compatibility == null || compatibility.Kind != RebirthSurvivorProfileCompatibilityKind.Ready)
        {
            RefreshProfiles(false);
            status = "[CC6B64]That profile is no longer ready for selection.[-]";
            return;
        }

        string error;
        if (!RebirthSurvivorFirstEntryUiService.StageExistingProfile(profile, out error))
        {
            status = "[CC6B64]" + (string.IsNullOrEmpty(error) ? "Could not select this Survivor Profile." : error) + "[-]";
            return;
        }

        selectedProfileId = profile.ProfileId;
        status = string.Empty;
        ApplyNativeSpawnLock(false, "profile-selected");
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection profile selected id=" + profile.ProfileId +
            " name='" + profile.ProfileName + "'; integrated Spawn eligible and selection remains changeable"); }
    }

    private void SpawnModeRandom_OnPressed(XUiController sender, int mouseButton)
    {
        spawnNearFriendMode = false;
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("spawnselection mode selected=random mouse=" + mouseButton); }
        UpdateSpawnOptionsUi();
    }

    private void SpawnModeFriend_OnPressed(XUiController sender, int mouseButton)
    {
        if (!NativeFriendsAvailable())
        {
            spawnNearFriendMode = false;
            UpdateSpawnOptionsUi();
            return;
        }
        spawnNearFriendMode = true;
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("spawnselection mode selected=friend mouse=" + mouseButton); }
        UpdateSpawnOptionsUi();
    }

    private void SpawnAction_OnPressed(XUiController sender, int mouseButton)
    {
        bool staged = RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection;
        bool friendsAvailable = NativeFriendsAvailable();
        bool friendValid = nativeSpawnNearFriend != null && nativeSpawnNearFriend.ViewComponent != null &&
            nativeSpawnNearFriend.ViewComponent.Enabled;
        XUiController target = spawnNearFriendMode ? nativeSpawnNearFriend :
            (nativeFirstTimeVisible && nativeSpawnFirstTime != null ? nativeSpawnFirstTime :
            (nativeSpawnVisible && nativeSpawn != null ? nativeSpawn :
            (nativeSpawnFirstTime != null ? nativeSpawnFirstTime : nativeSpawn)));

        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("integrated Spawn PRESS mode=" +
            (spawnNearFriendMode ? "friend" : "random") +
            " staged=" + staged +
            " friendsAvailable=" + friendsAvailable +
            " friendSelectionValid=" + friendValid +
            " target=" + (spawnNearFriendMode ? "btnSpawnNearFriend" :
                (object.ReferenceEquals(target, nativeSpawnFirstTime) ? "btnSpawnFirstTime" : "btnSpawn")) +
            " targetEnabled=" + (target != null && target.ViewComponent != null && target.ViewComponent.Enabled) +
            " targetVisible=" + (target != null && target.ViewComponent != null && target.ViewComponent.IsVisible)); }

        if (!staged)
        {
            status = "[CC6B64]Select a Survivor Profile before spawning.[-]";
            Render();
            return;
        }
        if (spawnNearFriendMode && (!friendsAvailable || !friendValid))
        {
            status = "[CC6B64]Select an available friend before spawning near them.[-]";
            Render();
            return;
        }
        if (target == null)
        {
            status = "[CC6B64]The native spawn action is unavailable.[-]";
            Render();
            return;
        }

        string error;
        if (!RebirthNativePlayerProfileBridge.TryInvokeButtonPress(target, out error))
        {
            status = "[CC6B64]" + (string.IsNullOrEmpty(error) ? "The native spawn action could not be invoked." : error) + "[-]";
            Log.Error("[REBIRTH Survivor][SpawnFlow] integrated Spawn dispatch failed mode=" +
                (spawnNearFriendMode ? "friend" : "random") + " error=" + (error ?? string.Empty));
            Render();
        }
    }

    private void Cancel_OnPressed(XUiController sender, int mouseButton)
    {
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("spawnselection CANCEL press mouse=" + mouseButton +
            " staged=" + RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection); }
        RebirthSurvivorFirstEntryUiService.LeaveWorld();
    }

    private void UpdateSpawnOptionsUi()
    {
        bool staged = RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection;
        bool friendsAvailable = NativeFriendsAvailable();
        if (!friendsAvailable) spawnNearFriendMode = false;

        SetVisible(btnSpawnModeFriend, friendsAvailable);
        SetInteractive(btnSpawnModeRandom, true);
        SetInteractive(btnSpawnModeFriend, friendsAvailable);

        XUiController randomSelected = GetChildById("spawnModeRandomSelected");
        XUiController friendSelected = GetChildById("spawnModeFriendSelected");
        SetVisible(randomSelected, !spawnNearFriendMode);
        SetVisible(friendSelected, spawnNearFriendMode && friendsAvailable);

        bool friendValid = nativeSpawnNearFriend != null && nativeSpawnNearFriend.ViewComponent != null &&
            nativeSpawnNearFriend.ViewComponent.Enabled;
        XUiController randomTarget = nativeFirstTimeVisible && nativeSpawnFirstTime != null
            ? nativeSpawnFirstTime : (nativeSpawnVisible && nativeSpawn != null ? nativeSpawn : nativeSpawnFirstTime);
        bool randomValid = randomTarget != null && randomTarget.ViewComponent != null && randomTarget.ViewComponent.Enabled;
        bool canSpawn = staged && (spawnNearFriendMode
            ? (friendsAvailable && friendValid)
            : randomValid);
        SetInteractive(btnSpawnAction, canSpawn);

        XUiV_Label friendHint = Label("spawnFriendSelectionHint");
        if (friendHint != null)
        {
            if (!friendsAvailable)
                friendHint.Text = "No eligible friends are currently available. Random spawn will be used.";
            else if (!spawnNearFriendMode)
                friendHint.Text = "Choose Spawn Near Friend to display the native multiplayer friend selector.";
            else if (!friendValid)
                friendHint.Text = "Select a friend below. Spawn remains disabled until the friend selection is valid.";
            else
                friendHint.Text = "Friend selected. Press SPAWN when ready.";
        }

        if (nativeContentTable != null && nativeContentTable.ViewComponent != null && lastVisible)
            nativeContentTable.ViewComponent.IsVisible = spawnNearFriendMode && friendsAvailable;

        // Keep the native direct-action buttons hidden while the integrated first-entry UI is active.
        if (lastVisible) ApplyNativeActionPresentation(true);
    }


    private void Create_OnPressed(XUiController sender, int mouseButton)
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null)
        {
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] CREATE NEW PROFILE pressed but XUi/windowManager is unavailable.");
            return;
        }
        GUIWindowManager manager = xui.playerUI.windowManager;
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection CREATE NEW PROFILE press mouse=" + mouseButton
            + " spawnOpenBefore=" + manager.IsWindowOpen(NativeSpawnWindow)
            + " creatorOpenBefore=" + manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId)
            + " staged=" + RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection); }
        profileBeforeCreator = selectedProfileId;
        string error;
        bool opened = XUiC_RebirthSurvivorCreator.OpenCreateForSpawnSelection(xui, OnSpawnCreatorCompleted, out error);
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection CREATE NEW PROFILE result opened=" + opened
            + " spawnOpenAfter=" + manager.IsWindowOpen(NativeSpawnWindow)
            + " creatorOpenAfter=" + manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId)
            + " error='" + (error ?? string.Empty) + "'"); }
        if (!opened)
        {
            status = "[CC6B64]" + (string.IsNullOrEmpty(error) ? "Survivor creator is unavailable." : error) + "[-]";
            Render();
        }
    }

    private static void OnSpawnCreatorCompleted(XUi xui, RebirthSurvivorProfile profile)
    {
        preferredProfileId = profile != null && !string.IsNullOrEmpty(profile.ProfileId)
            ? profile.ProfileId
            : profileBeforeCreator;
        profileBeforeCreator = string.Empty;

        RebirthSurvivorProfileStore.Refresh();
        GUIWindowManager manager = xui != null && xui.playerUI != null ? xui.playerUI.windowManager : null;
        bool spawnOpenBefore = manager != null && manager.IsWindowOpen(NativeSpawnWindow);
        if (manager != null && !spawnOpenBefore)
        {
            // This is a fail-safe only. The overlay open path is designed to preserve spawnselection,
            // but never leave the player on the loading backdrop if the engine closes it anyway.
            manager.Open(NativeSpawnWindow, true);
        }

        XUiC_RebirthSpawnSelectionSurvivorGate instance = activeInstance;
        if (instance != null)
        {
            // Return from creation with the new reusable profile highlighted for review, but do
            // not stage it until the player explicitly clicks the row in spawnselection.
            if (profile != null && !string.IsNullOrEmpty(profile.ProfileId))
                instance.selectedProfileId = profile.ProfileId;
            instance.RefreshProfiles(false);
            instance.progressionOffset = 0;
            instance.traitOffset = 0;
            instance.weaknessOffset = 0;
            instance.startingItemOffset = 0;
            instance.traitNativeInitialSyncFrames = 2;
            instance.status = profile != null ? "[8FD18F]New Survivor Profile created. Click the profile to select it for this world.[-]" : string.Empty;
            instance.Render();
            instance.SelectInitialNavigationTarget();
        }
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("returned from existing Survivor Creator created=" + (profile != null)
            + " preferredProfileId=" + (preferredProfileId ?? string.Empty)
            + " spawnOpenBefore=" + spawnOpenBefore
            + " spawnOpenAfter=" + (manager != null && manager.IsWindowOpen(NativeSpawnWindow))
            + " gateInstance=" + (activeInstance != null)); }
    }


    private string BuildProfileRowSummary(RebirthSurvivorProfile profile)
    {
        if (profile == null) return string.Empty;
        return "[B58CFF]" + RebirthSurvivorUiText.ResolveDefinitionName(profile.BackgroundId) + "[-]  •  [80C8F0]" +
            RebirthSurvivorUiText.ResolveDefinitionName(profile.DietId) + "[-]\n" +
            "[D6D6DC]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorStartingSkills", "Skills") + "[-]: " + CompactStartingSkillSummary(profile);
    }

    private string CompactStartingSkillSummary(RebirthSurvivorProfile profile)
    {
        if (profile == null) return RebirthSurvivorUiText.L("xuiRebirthSurvivorNone", "None");
        RebirthBackgroundDefinition background;
        if (!RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out background) || background == null || background.StartingSkills.Count == 0)
            return RebirthSurvivorUiText.L("xuiRebirthSurvivorNone", "None");

        List<string> entries = new List<string>();
        for (int i = 0; i < background.StartingSkills.Count; i++)
        {
            RebirthStartingSkillBiasDefinition skill = background.StartingSkills[i];
            if (skill == null || string.IsNullOrEmpty(skill.SkillId)) continue;
            float value = ResolveStartingSkillValue(skill);
            if (Math.Abs(value) < 0.001f) continue;
            string signed = (value > 0f ? "+" : string.Empty) + FormatSkillValue(value);
            string color = value > 0f ? "74DC54" : "E41215";
            entries.Add("[" + color + "]" + RebirthSurvivorUiText.ResolveDefinitionName(skill.SkillId) + " " + signed + "[-]");
        }
        if (entries.Count == 0) return RebirthSurvivorUiText.L("xuiRebirthSurvivorNone", "None");
        int shown = Math.Min(3, entries.Count);
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < shown; i++)
        {
            if (i > 0) text.Append(", ");
            text.Append(entries[i]);
        }
        if (entries.Count > shown) text.Append(" [92929A]+").Append(entries.Count - shown).Append("[-]");
        return text.ToString();
    }

    private string BuildProfileSubtitle(RebirthSurvivorProfile profile)
    {
        if (profile == null) return string.Empty;
        return "[B58CFF]" + RebirthSurvivorUiText.ResolveDefinitionName(profile.BackgroundId) + "[-]\n" +
            "[80C8F0]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorDiet", "Diet") + "[-]: " + RebirthSurvivorUiText.ResolveDefinitionName(profile.DietId);
    }

    private string BuildIdentityMeta(RebirthSurvivorProfile profile)
    {
        if (profile == null) return string.Empty;
        string playerProfileName;
        if (!profile.CreationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out playerProfileName) || string.IsNullOrEmpty(playerProfileName))
            playerProfileName = RebirthSurvivorUiText.L("xuiRebirthSurvivorNotSelected", "Not selected");
        return "[8F8F98]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileTitle", "Player Profile").ToUpperInvariant() + "[-]  " + playerProfileName;
    }

    private string BuildProfileDescription(RebirthSurvivorProfile profile)
    {
        if (profile == null) return string.Empty;
        RebirthBackgroundDefinition background;
        if (!RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out background) || background == null) return string.Empty;
        return RebirthSurvivorUiText.L(background.DescriptionKey, background.Identity);
    }

    private void BindSelectedPlayerPreview(RebirthSurvivorProfile profile)
    {
        if (selectedPlayerModelBinder == null) return;
        if (profile == null)
        {
            selectedPlayerModelBinder.Clear();
            previewPlayerProfileName = string.Empty;
            return;
        }

        string playerProfileName;
        if (!profile.CreationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out playerProfileName) ||
            string.IsNullOrEmpty(playerProfileName))
        {
            selectedPlayerModelBinder.Clear();
            previewPlayerProfileName = string.Empty;
            return;
        }

        if (string.Equals(previewPlayerProfileName, playerProfileName, StringComparison.OrdinalIgnoreCase))
            return;

        bool bound = selectedPlayerModelBinder.Bind(playerProfileName);
        if (bound)
        {
            previewPlayerProfileName = playerProfileName;
            { if (RebirthLogSettings.PlayerProfilePortraitLoggingEnabled) RebirthLogSettings.TracePlayerProfilePortrait("spawnselection full-model bound profile='" +
                playerProfileName + "'"); }
        }
        else
        {
            selectedPlayerModelBinder.Clear();
            // Remember the failed name so the 250ms refresh loop does not flood the log. A profile
            // change or reopening spawnselection resets this and causes a fresh attempt.
            previewPlayerProfileName = playerProfileName;
            { if (RebirthLogSettings.PlayerProfilePortraitLoggingEnabled) RebirthLogSettings.TracePlayerProfilePortrait("spawnselection full-model unavailable profile='" +
                playerProfileName + "'"); }
        }
    }


    private void RenderSelectedProfileDetails(RebirthSurvivorProfile profile)
    {
        List<ProfileDetailDisplay> progression = new List<ProfileDetailDisplay>();
        List<ProfileDetailDisplay> positiveTraits = new List<ProfileDetailDisplay>();
        List<ProfileDetailDisplay> negativeTraits = new List<ProfileDetailDisplay>();
        List<ProfileDetailDisplay> weaknesses = new List<ProfileDetailDisplay>();
        List<RebirthStartingItemDefinition> startingItems = new List<RebirthStartingItemDefinition>();

        if (profile != null)
        {
            RebirthBackgroundDefinition background;
            if (RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out background) && background != null)
            {
                for (int i = 0; i < background.StartingSkills.Count; i++)
                {
                    RebirthStartingSkillBiasDefinition skill = background.StartingSkills[i];
                    if (skill == null || string.IsNullOrEmpty(skill.SkillId)) continue;
                    float value = ResolveStartingSkillValue(skill);
                    if (Math.Abs(value) < 0.001f) continue;
                    ProfileDetailDisplay entry = new ProfileDetailDisplay
                    {
                        IconKey = SkillIconKey(skill.SkillId),
                        Name = RebirthSurvivorUiText.ResolveDefinitionName(skill.SkillId),
                        Value = (value > 0f ? "[74DC54]+" : "[E41215]") + FormatSkillValue(value) + "[-]",
                        RowKind = value > 0f ? "SKILL" : "WEAKNESS"
                    };
                    if (value > 0f) progression.Add(entry); else weaknesses.Add(entry);
                }

                List<string> orderedKnowledge = RebirthSurvivorUiText.OrderedStartingKnowledgeIds(background.StartingKnowledgeIds);
                for (int i = 0; i < orderedKnowledge.Count; i++)
                {
                    string id = orderedKnowledge[i];
                    progression.Add(new ProfileDetailDisplay
                    {
                        IconKey = RebirthSurvivorUiText.StartingKnowledgeIconKey(id),
                        Name = RebirthSurvivorUiText.ResolveDefinitionName(id),
                        Value = "[B58CFF]KNOWN[-]",
                        RowKind = "KNOWLEDGE"
                    });
                }

                if (background.StartingItems != null)
                {
                    for (int i = 0; i < background.StartingItems.Count; i++)
                    {
                        RebirthStartingItemDefinition item = background.StartingItems[i];
                        if (item != null) startingItems.Add(item);
                    }
                }
            }

            for (int i = 0; i < profile.TraitIds.Count; i++)
            {
                RebirthTraitDefinition trait;
                if (!RebirthSurvivorDefinitionRegistry.TryGetTrait(profile.TraitIds[i], out trait) || trait == null) continue;
                bool negative = trait.Polarity == RebirthTraitPolarity.Negative;
                string pointText = RebirthSurvivorUiText.PointText(trait);
                ProfileDetailDisplay entry = new ProfileDetailDisplay
                {
                    IconKey = trait.IconKey,
                    Name = RebirthSurvivorUiText.L(trait.NameKey, trait.Id),
                    Value = negative ? "[E41215]" + pointText + " PT[-]" : "[74DC54]" + pointText + " PT[-]",
                    RowKind = negative ? "NEGATIVE TRAIT" : "POSITIVE TRAIT"
                };
                if (negative) negativeTraits.Add(entry); else positiveTraits.Add(entry);
            }

            positiveTraits.Sort(delegate(ProfileDetailDisplay a, ProfileDetailDisplay b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            negativeTraits.Sort(delegate(ProfileDetailDisplay a, ProfileDetailDisplay b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
        }

        List<ProfileDetailDisplay> traits = new List<ProfileDetailDisplay>();
        traits.AddRange(positiveTraits);
        traits.AddRange(negativeTraits);
        selectedProgressionDisplayCount = progression.Count;
        selectedTraitDisplayCount = traits.Count;
        selectedWeaknessDisplayCount = weaknesses.Count;
        selectedStartingItemDisplayCount = startingItems.Count;
        progressionOffset = Mathf.Clamp(progressionOffset, 0, Math.Max(0, selectedProgressionDisplayCount - ProgressionVisibleRows));
        traitOffset = Mathf.Clamp(traitOffset, 0, Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows));
        weaknessOffset = Mathf.Clamp(weaknessOffset, 0, Math.Max(0, selectedWeaknessDisplayCount - WeaknessVisibleRows));
        startingItemOffset = Mathf.Clamp(startingItemOffset, 0, Math.Max(0, selectedStartingItemDisplayCount - StartingItemVisibleRows));

        RenderDetailSection(0, ProgressionVisibleRows, progression, progressionOffset, false);
        RenderDetailSection(TraitBaseIndex, TraitVisibleRows, traits, traitOffset, true);
        RenderDetailSection(WeaknessBaseIndex, WeaknessVisibleRows, weaknesses, weaknessOffset, false);
        RenderStartingItems(startingItems);
        UpdateProgressionNativeScroll();
        UpdateTraitNativeScroll();
        UpdateWeaknessNativeScroll();
        UpdateStartingItemNativeScroll();
    }

    private void RenderDetailSection(int baseIndex, int capacity, List<ProfileDetailDisplay> entries, int start, bool traitSection)
    {
        int count = entries != null ? entries.Count : 0;
        for (int local = 0; local < capacity; local++)
        {
            int slot = baseIndex + local;
            int index = start + local;
            bool visible = index < count;
            SetVisible(detailRows[slot], visible);
            if (!visible)
            {
                SetSprite(detailIcons[slot], string.Empty);
                SetSpriteColor(detailIcons[slot], new Color32(255,255,255,255));
                SetLabel(detailNames[slot], string.Empty);
                SetLabel(detailValues[slot], string.Empty);
                SetLabelColor(detailValues[slot], new Color32(220,220,225,255));
                continue;
            }

            ProfileDetailDisplay entry = entries[index];
            SetSprite(detailIcons[slot], entry.IconKey);
            SetLabel(detailNames[slot], entry.Name);
            SetLabel(detailValues[slot], entry.Value);
            if (traitSection)
            {
                bool negative = string.Equals(entry.RowKind, "NEGATIVE TRAIT", StringComparison.Ordinal);
                Color32 color = negative ? new Color32(228,18,21,255) : new Color32(116,220,84,255);
                SetSpriteColor(detailIcons[slot], color);
                SetLabelColor(detailValues[slot], color);
            }
            else
            {
                SetSpriteColor(detailIcons[slot], new Color32(255,255,255,255));
                SetLabelColor(detailValues[slot], new Color32(220,220,225,255));
            }
        }
    }

    private void RenderStartingItems(List<RebirthStartingItemDefinition> items)
    {
        int count = items != null ? items.Count : 0;
        for (int i = 0; i < StartingItemVisibleRows; i++)
        {
            int index = startingItemOffset + i;
            bool visible = index < count;
            SetVisible(startingItemRows[i], visible);
            if (!visible)
            {
                SetLabel(startingItemNames[i], string.Empty);
                SetLabel(startingItemMeta[i], string.Empty);
                SetStartingItemIcon(startingItemIcons[i], null);
                continue;
            }
            RebirthStartingItemDefinition item = items[index];
            SetLabel(startingItemNames[i], RebirthSurvivorUiText.L(item.NameKey, item.ItemId));
            SetLabel(startingItemMeta[i], StartingItemMeta(item));
            SetStartingItemIcon(startingItemIcons[i], item);
        }
    }

    private void ProfileListScroll(float delta)
    {
        int old = offset;
        if (delta > 0f) offset--; else if (delta < 0f) offset++;
        offset = ClampOffset(offset);
        if (offset != old) Render();
    }

    private void ProgressionListScroll(float delta)
    {
        int old = progressionOffset;
        if (delta > 0f) progressionOffset--; else if (delta < 0f) progressionOffset++;
        progressionOffset = Mathf.Clamp(progressionOffset, 0, Math.Max(0, selectedProgressionDisplayCount - ProgressionVisibleRows));
        if (progressionOffset != old) Render();
    }

    private void WeaknessListScroll(float delta)
    {
        int old = weaknessOffset;
        if (delta > 0f) weaknessOffset--; else if (delta < 0f) weaknessOffset++;
        weaknessOffset = Mathf.Clamp(weaknessOffset, 0, Math.Max(0, selectedWeaknessDisplayCount - WeaknessVisibleRows));
        if (weaknessOffset != old) Render();
    }

    private void StartingItemListScroll(float delta)
    {
        int old = startingItemOffset;
        if (delta > 0f) startingItemOffset--; else if (delta < 0f) startingItemOffset++;
        startingItemOffset = Mathf.Clamp(startingItemOffset, 0, Math.Max(0, selectedStartingItemDisplayCount - StartingItemVisibleRows));
        if (startingItemOffset != old) Render();
    }

    private void TraitListScroll(float delta)
    {
        int oldOffset = traitOffset;
        if (delta > 0f) traitOffset--;
        else if (delta < 0f) traitOffset++;
        traitOffset = Mathf.Clamp(traitOffset, 0, Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows));
        if (traitOffset != oldOffset) Render();
    }

    private void UpdateTraitNativeScroll()
    {
        if (traitNativeScrollHost != null && traitNativeScrollHost.ViewComponent != null)
            traitNativeScrollHost.ViewComponent.IsVisible = selectedTraitDisplayCount > TraitVisibleRows;
        if (traitScrollCapture != null && traitScrollCapture.ViewComponent != null)
            traitScrollCapture.ViewComponent.IsVisible = selectedTraitDisplayCount > 0;
        if (traitNativeScrollProxy == null || traitNativeScrollProxy.ViewComponent == null) return;

        int contentHeight = selectedTraitDisplayCount > TraitVisibleRows
            ? Math.Max(TraitTrackHeight + 1, Mathf.CeilToInt(TraitTrackHeight * (selectedTraitDisplayCount / (float)TraitVisibleRows)))
            : TraitTrackHeight;
        traitNativeScrollProxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        int maxOffset = Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows);
        float normalized = maxOffset > 0 ? Mathf.Clamp01(traitOffset / (float)maxOffset) : 0f;

        syncingTraitNativeScroll = true;
        RebirthNativeScrollbarUtil.Refresh(traitNativeScrollView);
        RebirthNativeScrollbarUtil.TrySetValue(traitNativeScrollView, normalized);
        RebirthNativeScrollbarUtil.Refresh(traitNativeScrollView);
        syncingTraitNativeScroll = false;
    }

    private void PollTraitNativeScroll()
    {
        if (syncingTraitNativeScroll) return;
        int maxOffset = Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows);
        if (maxOffset <= 0) return;
        float normalized;
        if (!RebirthNativeScrollbarUtil.TryGetValue(traitNativeScrollView, out normalized)) return;
        int requested = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset);
        if (requested == traitOffset) return;
        traitOffset = requested;
        Render();
    }

    private void UpdateProfileNativeScroll() { UpdateNativeScroll(profileNativeScrollHost, profileNativeScrollView, profileNativeScrollProxy, profiles.Length, VisibleRows, offset, ProfileTrackHeight, ref syncingProfileNativeScroll); }
    private void UpdateProgressionNativeScroll() { UpdateNativeScroll(progressionNativeScrollHost, progressionNativeScrollView, progressionNativeScrollProxy, selectedProgressionDisplayCount, ProgressionVisibleRows, progressionOffset, ProgressionTrackHeight, ref syncingProgressionNativeScroll); }
    private void UpdateWeaknessNativeScroll() { UpdateNativeScroll(weaknessNativeScrollHost, weaknessNativeScrollView, weaknessNativeScrollProxy, selectedWeaknessDisplayCount, WeaknessVisibleRows, weaknessOffset, WeaknessTrackHeight, ref syncingWeaknessNativeScroll); }
    private void UpdateStartingItemNativeScroll() { UpdateNativeScroll(startingItemNativeScrollHost, startingItemNativeScrollView, startingItemNativeScrollProxy, selectedStartingItemDisplayCount, StartingItemVisibleRows, startingItemOffset, StartingItemTrackHeight, ref syncingStartingItemNativeScroll); }

    private static void UpdateNativeScroll(XUiController host, XUiController view, XUiController proxy, int count, int visibleRows, int listOffset, int trackHeight, ref bool syncing)
    {
        int maxOffset = Math.Max(0, count - visibleRows);
        if (host != null && host.ViewComponent != null) host.ViewComponent.IsVisible = maxOffset > 0;
        if (proxy == null || proxy.ViewComponent == null) return;
        int contentHeight = maxOffset > 0 ? Math.Max(trackHeight + 1, Mathf.CeilToInt(trackHeight * (count / (float)visibleRows))) : trackHeight;
        proxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        syncing = true;
        RebirthNativeScrollbarUtil.Refresh(view);
        RebirthNativeScrollbarUtil.TrySetValue(view, maxOffset > 0 ? listOffset / (float)maxOffset : 0f);
        RebirthNativeScrollbarUtil.Refresh(view);
        syncing = false;
    }

    private void PollProfileNativeScroll() { int value; if (PollNativeScroll(profileNativeScrollView, profiles.Length, VisibleRows, offset, syncingProfileNativeScroll, out value)) { offset = value; Render(); } }
    private void PollProgressionNativeScroll() { int value; if (PollNativeScroll(progressionNativeScrollView, selectedProgressionDisplayCount, ProgressionVisibleRows, progressionOffset, syncingProgressionNativeScroll, out value)) { progressionOffset = value; Render(); } }
    private void PollWeaknessNativeScroll() { int value; if (PollNativeScroll(weaknessNativeScrollView, selectedWeaknessDisplayCount, WeaknessVisibleRows, weaknessOffset, syncingWeaknessNativeScroll, out value)) { weaknessOffset = value; Render(); } }
    private void PollStartingItemNativeScroll() { int value; if (PollNativeScroll(startingItemNativeScrollView, selectedStartingItemDisplayCount, StartingItemVisibleRows, startingItemOffset, syncingStartingItemNativeScroll, out value)) { startingItemOffset = value; Render(); } }

    private static bool PollNativeScroll(XUiController view, int count, int visibleRows, int current, bool syncing, out int requested)
    {
        requested = current;
        if (syncing) return false;
        int maxOffset = Math.Max(0, count - visibleRows);
        if (maxOffset <= 0) return false;
        float normalized;
        if (!RebirthNativeScrollbarUtil.TryGetValue(view, out normalized)) return false;
        requested = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset);
        return requested != current;
    }

    private static float ResolveStartingSkillValue(RebirthStartingSkillBiasDefinition skill)
    {
        if (skill == null) return 0f;
        if (skill.HasExplicitValue) return skill.Value;
        int tierValue;
        RebirthProgressionDefinition progression = RebirthSurvivorDefinitionRegistry.Bundle != null ? RebirthSurvivorDefinitionRegistry.Bundle.Progression : null;
        if (progression != null && progression.SkillBiasTiers.TryGetValue(skill.TierId, out tierValue)) return tierValue;
        return 0f;
    }

    private static string FormatSkillValue(float value)
    {
        return Math.Abs(value - Math.Round(value)) < 0.001f ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture) : value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string SkillIconKey(string skillId)
    {
        string id = (skillId ?? string.Empty).Trim().ToLowerInvariant();
        if (!id.StartsWith("skill.", StringComparison.Ordinal)) return "rb_skill_unarmed";
        string suffix = id.Substring("skill.".Length).Replace('.', '_').Replace('-', '_');
        return "rb_skill_" + suffix;
    }

    private static string StartingItemMeta(RebirthStartingItemDefinition item)
    {
        if (item == null) return string.Empty;
        string text = item.HasQuality ? "Q" + item.Quality.ToString(CultureInfo.InvariantCulture) : string.Empty;
        if (item.Count > 1)
        {
            if (text.Length > 0) text += " ";
            text += "×" + item.Count.ToString(CultureInfo.InvariantCulture);
        }
        return text;
    }

    private static void SetStartingItemIcon(XUiV_Sprite sprite, RebirthStartingItemDefinition item)
    {
        if (sprite == null) return;
        if (item == null || string.IsNullOrEmpty(item.ItemId))
        {
            sprite.UIAtlas = "ItemIconAtlas";
            sprite.SetSpriteImmediately("ui_game_symbol_backpack");
            sprite.SetColorImmediately(Color.white);
            return;
        }

        string overrideAtlas;
        string overrideIcon;
        if (TryGetStartingItemIconOverride(item.ItemId, out overrideAtlas, out overrideIcon))
        {
            sprite.UIAtlas = overrideAtlas;
            sprite.SetSpriteImmediately(overrideIcon);
            sprite.Color = Color.white;
            sprite.SetColorImmediately(Color.white);
            return;
        }

        sprite.UIAtlas = "ItemIconAtlas";
        ItemClass itemClass = ItemClass.GetItemClass(item.ItemId, false);
        string icon = itemClass != null ? itemClass.GetIconName() : item.ItemId;
        Color tint = itemClass != null ? itemClass.GetIconTint() : Color.white;
        tint.a = 1f;
        sprite.SetSpriteImmediately(string.IsNullOrEmpty(icon) ? item.ItemId : icon);
        sprite.Color = tint;
        sprite.SetColorImmediately(tint);
    }

    private static bool TryGetStartingItemIconOverride(string itemId, out string atlas, out string icon)
    {
        atlas = "ItemIconAtlas";
        icon = string.Empty;
        if (string.IsNullOrEmpty(itemId)) return false;
        switch (itemId)
        {
            case "ItemsWeaponsCleaver001_FR": atlas = "ItemIconAtlas"; icon = "FR_Cleaver_icon"; return true;
            case "ItemsWeaponsJunkBaton001_FR": atlas = "ItemIconAtlas"; icon = "ItemsWeaponsJunkBaton001_FR"; return true;
            case "ItemsWeaponsScythe004_FR": atlas = "ItemIconAtlas"; icon = "ItemsWeaponsScythe004_FR"; return true;
            case "FuriousRamsayFountainPen": atlas = "ItemIconAtlas"; icon = "FR_FountainPen_icon"; return true;
            case "FuriousRamsayHammerPliers": atlas = "ItemIconAtlas"; icon = "FR_HammerPliers_icon"; return true;
            case "FuriousRamsayScrewdriver": atlas = "ItemIconAtlas"; icon = "FR_Screwdriver_icon"; return true;
            case "FuriousRamsayPliers": atlas = "ItemIconAtlas"; icon = "FR_Pliers_icon"; return true;
            case "guppyFireExtinguisherItem": atlas = "ItemIconAtlas"; icon = "guppyFireExtinguisher"; return true;
            case "qt_claude": atlas = "ItemIconAtlas"; icon = "treasureQuestMaster"; return true;
            case "FuriousRamsayPropaneTank": atlas = "ItemIconAtlas"; icon = "FR_SM_Propane_icon"; return true;
            case "modArmorHelmetLight": atlas = "ItemIconAtlas"; icon = "modArmorHelmetLight"; return true;
            case "FuriousRamsaySeedBundle": icon = "bundleFarm"; return true;
            case "FuriousRamsayWaterTank": icon = "cntBarrelPlasticSingle00"; return true;
        }
        return false;
    }

    private sealed class ProfileDetailDisplay
    {
        public string IconKey = string.Empty;
        public string Name = string.Empty;
        public string Value = string.Empty;
        public string RowKind = string.Empty;
    }

    private RebirthSurvivorProfile GetSelected()
    {
        int index = IndexOf(selectedProfileId);
        return index >= 0 ? profiles[index] : null;
    }

    private int IndexOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < profiles.Length; i++)
            if (string.Equals(profiles[i].ProfileId, id, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private void EnsureSelectedVisible()
    {
        int index = IndexOf(selectedProfileId);
        if (index < 0) return;
        if (index < offset) offset = index;
        else if (index >= offset + VisibleRows) offset = Math.Max(0, index - VisibleRows + 1);
    }

    private int ClampOffset(int value)
    {
        if (profiles.Length <= VisibleRows) return 0;
        int max = Math.Max(0, profiles.Length - VisibleRows);
        return Math.Max(0, Math.Min(max, value));
    }

    private void SelectInitialNavigationTarget()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.CursorController == null) return;
        XUiController target = profiles.Length > 0 ? rowButtons[0] : btnCreate;
        if (target != null && target.ViewComponent != null)
            xui.playerUI.CursorController.SetNavigationTargetLater(target.ViewComponent);
    }

    private static void WireScroll(XUiController controller, Action<float> callback)
    {
        if (controller == null || callback == null) return;
        if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += delegate(XUiController sender, float delta) { callback(delta); };
    }

    private XUiController Wire(string id, XUiEvent_OnPressEventHandler handler)
    {
        XUiController c = GetChildById(id);
        if (c != null) c.OnPress += handler;
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

    private static void SetLabel(XUiV_Label label, string text)
    {
        if (label != null) label.Text = text ?? string.Empty;
    }

    private static void SetLabelColor(XUiV_Label label, Color32 color)
    {
        if (label != null) label.Color = color;
    }

    private static void SetSpriteColor(XUiV_Sprite sprite, Color32 color)
    {
        if (sprite == null) return;
        sprite.Color = color;
        sprite.SetColorImmediately(color);
    }

    private static void SetSprite(XUiV_Sprite sprite, string name)
    {
        if (sprite == null) return;
        sprite.UIAtlas = "RebirthSurvivorIcons";
        sprite.SetSpriteImmediately(name ?? string.Empty);
        sprite.SetColorImmediately(Color.white);
    }

    private static void SetVisible(XUiController c, bool visible)
    {
        if (c != null && c.ViewComponent != null) c.ViewComponent.IsVisible = visible;
    }

    private static void SetInteractive(XUiController c, bool enabled)
    {
        if (c == null || c.ViewComponent == null) return;
        c.ViewComponent.Enabled = enabled;
        if (c.Children == null) return;
        for (int i = 0; i < c.Children.Count; i++) SetInteractive(c.Children[i], enabled);
    }
}
