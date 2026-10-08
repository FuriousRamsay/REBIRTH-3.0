using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class XUiC_RebirthSurvivorProfileManager : XUiController
{
    public const string WindowGroupId = "rebirthSurvivorProfiles";
    private const int VisibleRows = 4;
    private const int ProfileTrackHeight = 472;
    private const int ProgressionVisibleRows = 10;
    private const int TraitVisibleRows = 8;
    private const int WeaknessVisibleRows = 2;
    private const int StartingItemVisibleRows = 8;
    private const int TraitBaseIndex = ProgressionVisibleRows;
    private const int WeaknessBaseIndex = ProgressionVisibleRows + TraitVisibleRows;
    private const int DetailRows = ProgressionVisibleRows + TraitVisibleRows + WeaknessVisibleRows;
    private const int TraitTrackHeight = 254;
    private const int ProgressionTrackHeight = 318;

    private static bool spawnSelectionMode;
    public static bool IsSpawnSelectionMode { get { return spawnSelectionMode; } }

    private readonly XUiController[] rowButtons = new XUiController[VisibleRows];
    private readonly XUiController[] rowContainers = new XUiController[VisibleRows];
    private readonly Dictionary<XUiController, int> rowIndex = new Dictionary<XUiController, int>();
    private readonly RebirthSurvivorArtTextureBinder[] backgroundIconBinders = new RebirthSurvivorArtTextureBinder[VisibleRows];
    private readonly XUiController[] artControllers = new XUiController[VisibleRows];
    private readonly XUiController[] artPlaceholders = new XUiController[VisibleRows];
    private readonly XUiV_Label[] rowNames = new XUiV_Label[VisibleRows];
    private readonly XUiV_Label[] rowSummaries = new XUiV_Label[VisibleRows];
    private readonly XUiV_Label[] rowStatuses = new XUiV_Label[VisibleRows];
    private readonly XUiController[] rowSelections = new XUiController[VisibleRows];
    private readonly XUiController[] detailRows = new XUiController[DetailRows];
    private readonly XUiV_Sprite[] detailIcons = new XUiV_Sprite[DetailRows];
    private readonly XUiV_Label[] detailNames = new XUiV_Label[DetailRows];
    private readonly XUiV_Label[] detailValues = new XUiV_Label[DetailRows];
    private readonly XUiController[] detailButtons = new XUiController[DetailRows];
    private readonly XUiController[] startingItemRows = new XUiController[StartingItemVisibleRows];
    private readonly XUiV_Sprite[] startingItemIcons = new XUiV_Sprite[StartingItemVisibleRows];
    private readonly XUiV_Label[] startingItemNames = new XUiV_Label[StartingItemVisibleRows];
    private readonly XUiV_Label[] startingItemMeta = new XUiV_Label[StartingItemVisibleRows];
    private readonly string[] detailProgressionTargets = new string[DetailRows];
    private readonly Dictionary<XUiController,int> detailButtonIndex = new Dictionary<XUiController,int>();
    private RebirthSurvivorProfile[] allProfiles = new RebirthSurvivorProfile[0];
    private RebirthSurvivorProfile[] profiles = new RebirthSurvivorProfile[0];
    private string searchText = string.Empty;
    private int sortMode;
    private int offset;
    private int traitOffset;
    private int progressionOffset;
    private int selectedTraitDisplayCount;
    private int selectedProgressionDisplayCount;
    private string selectedProfileId = string.Empty;
    private bool deleteConfirmVisible;
    private string deleteConfirmProfileId = string.Empty;
    private DateTime deleteConfirmModifiedAtUtc;
    private string statusMessage = string.Empty;

    private XUiV_Label lblPage;
    private XUiV_Label lblSelectedTitle;
    private XUiV_Label lblSelectedDetails;
    private XUiV_Label lblStatus;
    private XUiV_Label lblSelectedSubtitle;
    private XUiV_Label lblSelectedDescription;
    private XUiV_Label lblSelectedIdentityMeta;
    private XUiV_Label lblSelectedSignatureHeading;
    private XUiV_Label lblSelectedSignatureName;
    private XUiV_Label lblSelectedSignatureDescription;
    private XUiV_Sprite selectedSignatureBonusIcon;
    private XUiV_Label lblSelectedStartingItems;
    private XUiV_Label lblSelectedBonuses;
    private XUiV_Label lblSelectedWeaknesses;
    private XUiV_Label lblSortMode;
    private XUiC_TextInput searchInput;
    private XUiController selectedArt;
    private XUiController selectedArtPlaceholder;
    private XUiController profileDetailScrollContent;
    private RebirthSurvivorArtTextureBinder selectedArtBinder;
    private XUiController selectedPlayerPreview;
    private string originalPlayerProfileName = string.Empty;
    private string previewPlayerProfileName = string.Empty;
    private bool keepPreviewSelection;
    private bool spawnOverlayVisibleLogged;
    private XUiController deletePanel;
    private XUiV_Label lblDeletePrompt;
    private XUiController btnCreate;
    private XUiController btnReview;
    private XUiController btnEdit;
    private XUiController btnDuplicate;
    private XUiController btnDelete;
    private XUiController btnSelectForWorld;
    private XUiController profileSelectionTimingHelp;
    private XUiController profileScrollTrack;
    private XUiController profileNativeScrollHost;
    private XUiController profileNativeScrollProxy;
    private XUiController profileNativeScrollView;
    private bool syncingProfileNativeScroll;
    private int profileNativeInitialSyncFrames;
    private XUiController progressionNativeScrollHost;
    private XUiController progressionNativeScrollProxy;
    private XUiController progressionNativeScrollView;
    private XUiController progressionScrollCapture;
    private bool syncingProgressionNativeScroll;
    private int progressionNativeInitialSyncFrames;
    private int progressionTooltipSyncFrames;
    private XUiController traitNativeScrollHost;
    private XUiController traitNativeScrollProxy;
    private XUiController traitNativeScrollView;
    private XUiController traitScrollCapture;
    private bool syncingTraitNativeScroll;
    private int traitNativeInitialSyncFrames;
    private XUiController profileScrollThumbControl;
    private XUiV_Button profileScrollThumb;
    private XUiController btnBack;
    private XUiController btnSort;
    private XUiController btnConfirmDelete;
    private XUiController btnCancelDelete;

    public override void Init()
    {
        base.Init();
        for (int i = 0; i < VisibleRows; i++)
        {
            rowContainers[i] = GetChildById("profileRow" + i.ToString(CultureInfo.InvariantCulture));
            rowButtons[i] = GetChildById("btnProfile" + i.ToString(CultureInfo.InvariantCulture));
            artControllers[i] = GetChildById("profileArt" + i.ToString(CultureInfo.InvariantCulture));
            artPlaceholders[i] = GetChildById("profileArtPlaceholder" + i.ToString(CultureInfo.InvariantCulture));
            rowNames[i] = Label("profileName" + i.ToString(CultureInfo.InvariantCulture));
            rowSummaries[i] = Label("profileSummary" + i.ToString(CultureInfo.InvariantCulture));
            rowStatuses[i] = Label("profileStatus" + i.ToString(CultureInfo.InvariantCulture));
            rowSelections[i] = GetChildById("profileSelection" + i.ToString(CultureInfo.InvariantCulture));
            backgroundIconBinders[i] = new RebirthSurvivorArtTextureBinder(artControllers[i], 1f);
            if (rowButtons[i] != null)
            {
                rowIndex[rowButtons[i]] = i;
                rowButtons[i].OnPress += ProfileRow_OnPressed;
                WireScroll(rowButtons[i], ProfileListScroll);
            }
        }
        for (int i = 0; i < DetailRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            detailRows[i] = GetChildById("profileDetailRow" + suffix);
            detailIcons[i] = Sprite("profileDetailIcon" + suffix);
            detailNames[i] = Label("profileDetailName" + suffix);
            detailValues[i] = Label("profileDetailValue" + suffix);
            detailButtons[i] = GetChildById("btnProfileDetail" + suffix);
            if (detailButtons[i] != null)
            {
                detailButtonIndex[detailButtons[i]] = i;
                detailButtons[i].OnPress += ProfileDetail_OnPressed;
            }
        }
        for (int i = 0; i < StartingItemVisibleRows; i++)
        {
            string suffix = i.ToString(CultureInfo.InvariantCulture);
            startingItemRows[i] = GetChildById("profileStartingItemRow" + suffix);
            startingItemIcons[i] = Sprite("profileStartingItemIcon" + suffix);
            startingItemNames[i] = Label("profileStartingItemName" + suffix);
            startingItemMeta[i] = Label("profileStartingItemMeta" + suffix);
        }

        btnCreate = Wire("btnCreateProfile", Create_OnPressed);
        // Survivor Profiles are reusable templates. World selection is owned exclusively by
        // the first-entry pre-spawn chooser, never by the main-menu profile manager.
        btnSort = Wire("btnProfileSort", Sort_OnPressed);
        btnReview = Wire("btnReviewProfile", Review_OnPressed);
        btnEdit = Wire("btnEditProfile", Edit_OnPressed);
        btnDuplicate = Wire("btnDuplicateProfile", Duplicate_OnPressed);
        btnDelete = Wire("btnDeleteProfile", Delete_OnPressed);
        btnSelectForWorld = Wire("btnSelectProfileForWorld", SelectForWorld_OnPressed);
        profileSelectionTimingHelp = GetChildById("profileSelectionTimingHelp");
        profileScrollTrack = GetChildById("profileManagerScrollTrackInput");
        profileScrollThumbControl = GetChildById("profileManagerScrollThumb");
        profileScrollThumb = profileScrollThumbControl != null ? profileScrollThumbControl.ViewComponent as XUiV_Button : null;
        profileNativeScrollHost = GetChildById("profileManagerScrollHost");
        profileNativeScrollProxy = GetChildById("profileManagerScrollProxy");
        profileNativeScrollView = GetChildById("profileManagerScrollView");
        WireScroll(profileNativeScrollHost, ProfileListScroll);
        WireScroll(profileNativeScrollView, ProfileListScroll);
        WireScroll(profileNativeScrollProxy, ProfileListScroll);
        WireScroll(profileScrollTrack, ProfileListScroll);
        WireScroll(profileScrollThumbControl, ProfileListScroll);
        WireThumbDrag(profileScrollThumbControl, ProfileThumbDrag);
        progressionNativeScrollHost = GetChildById("profileProgressionNativeScrollHost");
        progressionNativeScrollProxy = GetChildById("profileProgressionNativeScrollProxy");
        progressionNativeScrollView = GetChildById("profileProgressionNativeScrollView");
        progressionScrollCapture = GetChildById("profileProgressionScrollCapture");
        WireScroll(progressionNativeScrollHost, ProgressionListScroll);
        WireScroll(progressionNativeScrollView, ProgressionListScroll);
        WireScroll(progressionNativeScrollProxy, ProgressionListScroll);
        WireScroll(progressionScrollCapture, ProgressionListScroll);
        for (int i = 0; i < ProgressionVisibleRows; i++)
        {
            WireScroll(detailRows[i], ProgressionListScroll);
            WireScroll(detailButtons[i], ProgressionListScroll);
        }
        traitNativeScrollHost = GetChildById("profileTraitNativeScrollHost");
        traitNativeScrollProxy = GetChildById("profileTraitNativeScrollProxy");
        traitNativeScrollView = GetChildById("profileTraitNativeScrollView");
        traitScrollCapture = GetChildById("profileTraitScrollCapture");
        WireScroll(traitNativeScrollHost, TraitListScroll);
        WireScroll(traitNativeScrollView, TraitListScroll);
        WireScroll(traitNativeScrollProxy, TraitListScroll);
        WireScroll(traitScrollCapture, TraitListScroll);
        for (int i = TraitBaseIndex; i < TraitBaseIndex + TraitVisibleRows; i++)
        {
            WireScroll(detailRows[i], TraitListScroll);
            WireScroll(detailButtons[i], TraitListScroll);
        }
        btnBack = Wire("btnProfileManagerBack", Back_OnPressed);
        btnConfirmDelete = Wire("btnConfirmDeleteProfile", ConfirmDelete_OnPressed);
        btnCancelDelete = Wire("btnCancelDeleteProfile", CancelDelete_OnPressed);

        lblPage = Label("profilePageText");
        lblSelectedTitle = Label("selectedProfileTitle");
        lblSelectedSubtitle = Label("selectedProfileSubtitle");
        lblSelectedDescription = Label("selectedProfileDescription");
        lblSelectedIdentityMeta = Label("selectedProfileIdentityMeta");
        lblSelectedSignatureHeading = Label("selectedProfileSignatureHeading");
        lblSelectedSignatureName = Label("selectedProfileSignatureName");
        lblSelectedSignatureDescription = Label("selectedProfileSignatureDescription");
        selectedSignatureBonusIcon = Sprite("selectedProfileSignatureIcon");
        lblSelectedStartingItems = Label("selectedProfileStartingItems");
        lblSelectedBonuses = Label("selectedProfileBonuses");
        lblSelectedWeaknesses = Label("selectedProfileWeaknesses");
        lblSelectedDetails = Label("selectedProfileDetails");
        lblStatus = Label("profileManagerStatus");
        lblSortMode = Label("profileSortMode");
        searchInput = GetChildById("profileSearchInput") as XUiC_TextInput;
        if (searchInput != null) searchInput.OnChangeHandler += Search_OnChanged;
        selectedArt = GetChildById("selectedProfileArt");
        selectedArtPlaceholder = GetChildById("selectedProfileArtPlaceholder");
        profileDetailScrollContent = GetChildById("profileDetailScrollContent");
        selectedArtBinder = new RebirthSurvivorArtTextureBinder(selectedArt, 1f);
        selectedPlayerPreview = GetChildById("selectedPlayerPreview");
        lblDeletePrompt = Label("deleteProfilePrompt");
        deletePanel = GetChildById("deleteProfileConfirmPanel");
    }

    public override void OnOpen()
    {
        base.OnOpen();
        // Own Cancel/Escape in Update(). If the generic window manager is allowed to
        // close this group first, ESC can leave the menu stack with no visible window.
        if (windowGroup != null) windowGroup.isEscClosable = false;
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("SURVIVOR-MANAGER OPEN escOwned=True spawnSelectionMode=" + spawnSelectionMode); }
        deleteConfirmVisible = false;
        deleteConfirmProfileId = string.Empty;
        deleteConfirmModifiedAtUtc = default(DateTime);
        statusMessage = string.Empty;
        spawnOverlayVisibleLogged = false;

        // The profile manager can be reached from the pre-spawn XUi path before the normal
        // main-menu/creator route has forced Survivor runtime initialization.  Never render
        // compatibility, names, icons, skills or signature bonuses against an empty definition
        // registry: that makes durable profiles appear broken (raw IDs / REPAIR / None).
        if (!RebirthSurvivorDefinitionRegistry.IsReady ||
            !RebirthBackgroundBonusRegistry.IsReady ||
            !RebirthSurvivorInstaller.IsInstalled)
        {
            string installReport = RebirthSurvivorInstaller.Install();
            if (RebirthLogSettings.RuntimeInstallLoggingEnabled || RebirthLogSettings.PreSpawnLoggingEnabled)
                { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("Survivor Profiles ensured runtime before render definitionsReady=" +
                    RebirthSurvivorDefinitionRegistry.IsReady +
                    " backgroundBonusesReady=" + RebirthBackgroundBonusRegistry.IsReady +
                    " installerReady=" + RebirthSurvivorInstaller.IsInstalled +
                    " report=" + (installReport ?? string.Empty)); }
        }

        if (!RebirthSurvivorDefinitionRegistry.IsReady)
            statusMessage = "[CC6B64]Survivor definitions are unavailable; saved profiles cannot be resolved yet. See the output log for [REBIRTH Survivor].[-]";

        RebirthSurvivorArtTextureBinder.PreloadBackgroundThumbnails();
        string originalSource;
        originalPlayerProfileName = string.Empty;
        RebirthNativePlayerProfileBridge.TryResolveActivePlayerProfileName(xui, null, out originalPlayerProfileName, out originalSource);
        previewPlayerProfileName = string.Empty;
        keepPreviewSelection = false;
        traitNativeInitialSyncFrames = 2;
        progressionNativeInitialSyncFrames = 2;
        profileNativeInitialSyncFrames = 2;

        // A reused controller can retain its previous in-memory query even when the native
        // text input has already been visually cleared by the window lifecycle.  That makes
        // an apparently empty search box hide every stored profile.  Opening the manager is
        // a fresh catalogue view, so keep the model and control text in sync explicitly.
        searchText = string.Empty;
        if (searchInput != null) searchInput.Text = string.Empty;

        RebirthSurvivorProfileStore.Refresh();
        RefreshProfiles(true);
        PrepareCursor();
        Render();
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(profileNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(progressionNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(traitNativeScrollHost);
    }

    private bool pagingMode;
    public override void Update(float dt)
    {
        // During forced/normal application teardown V3.2 can dispose the GUIWindowManager before
        // this menu group's scroll views receive their final Update. Do not let native hotkey
        // polling dereference the disposed manager during shutdown.
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return;
        base.Update(dt);
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; pagingDragRemainder = 0f; }
        if (pagingNow)
        {
            int old = offset, oldProgression = progressionOffset, oldTrait = traitOffset;
            offset = ClampOffset(offset);
            progressionOffset = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(progressionOffset, Math.Max(0, selectedProgressionDisplayCount - ProgressionVisibleRows), ProgressionVisibleRows, true);
            traitOffset = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(traitOffset, Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows), TraitVisibleRows, true);
            if (old != offset || oldProgression != progressionOffset || oldTrait != traitOffset) Render();
        }
        if (spawnSelectionMode && !spawnOverlayVisibleLogged && windowGroup != null && windowGroup.isShowing)
        {
            spawnOverlayVisibleLogged = true;
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("Survivor Profiles overlay ACTUALLY SHOWING viewVisible=" +
                (ViewComponent != null && ViewComponent.IsVisible) + " spawnUnderlay=" +
                (xui != null && xui.playerUI != null && xui.playerUI.windowManager != null && xui.playerUI.windowManager.IsWindowOpen("spawnselection"))); }
        }
        RebirthNativePlayerProfileBridge.Tick(xui);
        if (traitNativeInitialSyncFrames > 0)
        {
            traitNativeInitialSyncFrames--;
            UpdateTraitNativeScroll();
        }
        if (profileNativeInitialSyncFrames > 0)
        {
            profileNativeInitialSyncFrames--;
            UpdateProfileNativeScroll();
        }
        if (progressionNativeInitialSyncFrames > 0)
        {
            progressionNativeInitialSyncFrames--;
            UpdateProgressionNativeScroll();
        }
        if (progressionTooltipSyncFrames > 0)
        {
            progressionTooltipSyncFrames--;
            RefreshProgressionTruncationTooltips();
        }
        PollProfileNativeScroll();
        PollProgressionNativeScroll();
        PollTraitNativeScroll();
        if (selectedArtBinder != null) selectedArtBinder.Update();
        if (RebirthNativePlayerProfileBridge.IsActive)
        {
            handleDirtyUpdateDefault();
            return;
        }
        if (XUiUtils.HotkeysAllowedFor(viewComponent) && xui != null && xui.playerUI != null)
        {
            if (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
            {
                { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("SURVIVOR-MANAGER ESC action=" +
                    (deleteConfirmVisible ? "cancel-delete" : "main-menu")); }
                if (deleteConfirmVisible) CancelDelete_OnPressed(this, -1);
                else Back_OnPressed(this, -1);
            }
        }
        handleDirtyUpdateDefault();
    }

    public override void OnClose()
    {
        deleteConfirmVisible = false;
        deleteConfirmProfileId = string.Empty;
        deleteConfirmModifiedAtUtc = default(DateTime);
        for (int i = 0; i < backgroundIconBinders.Length; i++) if (backgroundIconBinders[i] != null) backgroundIconBinders[i].Clear();
        if (selectedArtBinder != null) selectedArtBinder.Clear();
        if (!keepPreviewSelection && !string.IsNullOrEmpty(originalPlayerProfileName))
        {
            string restoreError;
            RebirthNativePlayerProfileBridge.TrySelectPlayerProfileForPreview(this, "selectedPlayerPreview", originalPlayerProfileName, out restoreError);
        }
        previewPlayerProfileName = string.Empty;
        base.OnClose();
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(profileNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(progressionNativeScrollHost);
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(traitNativeScrollHost);
    }

    public void RefreshAfterCreator(RebirthSurvivorProfile profile)
    {
        if (profile != null) selectedProfileId = profile.ProfileId;
        traitOffset = 0;
        traitNativeInitialSyncFrames = 2;
        RebirthSurvivorProfileStore.Refresh();
        RefreshProfiles(false);
        Render();
    }

    private void RefreshProfiles(bool preserveFirstWhenNoSelection)
    {
        allProfiles = RebirthSurvivorProfileStore.GetProfilesSnapshot();
        ApplyFilterAndSort();
        if (!string.IsNullOrEmpty(selectedProfileId) && IndexOfProfile(selectedProfileId) < 0)
            selectedProfileId = string.Empty;
        if (string.IsNullOrEmpty(selectedProfileId) && preserveFirstWhenNoSelection && profiles.Length > 0)
            selectedProfileId = profiles[0].ProfileId;
        offset = ClampOffset(offset);
        EnsureSelectionVisible();
    }

    private void ApplyFilterAndSort()
    {
        List<RebirthSurvivorProfile> filtered = new List<RebirthSurvivorProfile>();
        string query = (searchText ?? string.Empty).Trim();
        for (int i = 0; i < allProfiles.Length; i++)
        {
            RebirthSurvivorProfile profile = allProfiles[i];
            if (profile == null) continue;
            // Never hide a stored profile merely because it currently needs review.
            // In first-entry selection mode an incompatible profile must remain visible so
            // the player can understand what happened to it; the Select action is gated
            // separately below.  Hiding it here makes durable profiles appear deleted.
            if (query.Length == 0 || ProfileMatches(profile, query)) filtered.Add(profile);
        }

        filtered.Sort(delegate(RebirthSurvivorProfile a, RebirthSurvivorProfile b)
        {
            if (sortMode == 1) return string.Compare(b.ProfileName, a.ProfileName, StringComparison.CurrentCultureIgnoreCase);
            if (sortMode == 2) return b.ModifiedAtUtc.CompareTo(a.ModifiedAtUtc);
            return string.Compare(a.ProfileName, b.ProfileName, StringComparison.CurrentCultureIgnoreCase);
        });
        profiles = filtered.ToArray();
        offset = ClampOffset(offset);
    }

    private static bool ContainsIgnoreCase(string haystack, string needle)
    {
        return !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;
    }

    private static bool ProfileMatches(RebirthSurvivorProfile profile, string query)
    {
        if (profile == null) return false;
        if (ContainsIgnoreCase(profile.ProfileName, query)) return true;
        if (ContainsIgnoreCase(RebirthSurvivorUiText.ResolveDefinitionName(profile.BackgroundId), query)) return true;
        if (ContainsIgnoreCase(RebirthSurvivorUiText.ResolveDefinitionName(profile.DietId), query)) return true;
        string playerProfileName;
        return profile.CreationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out playerProfileName) && ContainsIgnoreCase(playerProfileName, query);
    }

    private void Render()
    {
        for (int i = 0; i < VisibleRows; i++)
        {
            int index = offset + i;
            bool visible = index >= 0 && index < profiles.Length;
            SetVisible(rowContainers[i], visible);
            SetVisible(rowButtons[i], visible);
            if (!visible)
            {
                if (backgroundIconBinders[i] != null) backgroundIconBinders[i].Clear();
                SetVisible(artControllers[i], false);
                SetVisible(artPlaceholders[i], false);
                SetVisible(rowSelections[i], false);
                SetLabel(rowNames[i], string.Empty);
                SetLabel(rowSummaries[i], string.Empty);
                SetLabel(rowStatuses[i], string.Empty);
                continue;
            }
            RebirthSurvivorProfile profile = profiles[index];
            RebirthSurvivorProfileCompatibility compatibility = RebirthSurvivorProfileStore.EvaluateCompatibility(profile);
            bool selected = string.Equals(profile.ProfileId, selectedProfileId, StringComparison.OrdinalIgnoreCase);
            string experience = RebirthSurvivorUiText.ResolveDefinitionName(profile.BackgroundId);
            string diet = RebirthSurvivorUiText.ResolveDefinitionName(profile.DietId);
            SetLabel(rowNames[i], profile.ProfileName);
            SetLabel(rowSummaries[i], BuildProfileRowSummary(profile, experience, diet));
            SetLabel(rowStatuses[i], CompatibilityShort(compatibility.Kind));
            SetVisible(rowSelections[i], selected);
            RebirthBackgroundDefinition rowBackground;
            bool hasArt = RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out rowBackground) &&
                rowBackground != null && backgroundIconBinders[i] != null && backgroundIconBinders[i].BindBackgroundThumbnail(rowBackground);
            SetVisible(artControllers[i], hasArt);
            SetVisible(artPlaceholders[i], !hasArt);
        }

        int first = profiles.Length == 0 ? 0 : offset + 1;
        int last = Math.Min(profiles.Length, offset + VisibleRows);
        SetLabel(lblPage, profiles.Length == 0
            ? (allProfiles.Length == 0 ? RebirthSurvivorUiText.L("xuiRebirthSurvivorNoProfiles", "No Survivor Profiles yet") : RebirthSurvivorUiText.L("xuiRebirthSurvivorNoProfileMatches", "No profiles match this search"))
            : string.Format(CultureInfo.InvariantCulture, RebirthSurvivorUiText.L("xuiRebirthSurvivorProfilePageFormat", "Showing {0}-{1} of {2}"), first, last, profiles.Length));
        SetLabel(lblSortMode, sortMode == 1 ? "NAME Z-A" : sortMode == 2 ? "RECENT" : "NAME A-Z");

        RebirthSurvivorProfile selectedProfile = GetSelectedProfile();
        bool has = selectedProfile != null;
        if (has)
        {
            SetLabel(lblSelectedTitle, selectedProfile.ProfileName);
            SetLabel(lblSelectedSubtitle, BuildProfileSubtitle(selectedProfile));
            SetLabel(lblSelectedDescription, string.Empty);
            SetLabel(lblSelectedIdentityMeta, string.Empty);
            SetLabel(lblSelectedSignatureHeading, RebirthSurvivorUiText.L("xuiRebirthSignatureBonus", "Signature Bonus").ToUpperInvariant());
            SetLabel(lblSelectedSignatureName, RebirthSurvivorUiText.SignatureBonusName(selectedProfile.BackgroundId));
            SetLabel(lblSelectedSignatureDescription, RebirthSurvivorUiText.SignatureBonusDescription(selectedProfile.BackgroundId));
            SetSprite(selectedSignatureBonusIcon, RebirthSurvivorUiText.SignatureBonusIcon(selectedProfile.BackgroundId));
            SetLabel(lblSelectedStartingItems, selectedBackgroundItemsText(selectedProfile));
            SetLabel(lblSelectedBonuses, string.Empty);
            SetLabel(lblSelectedWeaknesses, string.Empty);
            SetLabel(lblSelectedDetails, string.Empty);
            RenderSelectedProfileDetails(selectedProfile);
            if (selectedArtBinder != null) selectedArtBinder.Clear();
            SetVisible(selectedArt, false);
            SetVisible(selectedArtPlaceholder, false);
            SetVisible(selectedPlayerPreview, true);
            BindSelectedPlayerPreview(selectedProfile);
        }
        else
        {
            if (selectedArtBinder != null) selectedArtBinder.Clear();
            SetVisible(selectedArt, false);
            SetVisible(selectedArtPlaceholder, true);
            // The native player-preview widget otherwise keeps rendering the last/default
            // base-game profile even though there is no Survivor Profile selection.
            SetVisible(selectedPlayerPreview, false);
            SetLabel(lblSelectedTitle, RebirthSurvivorUiText.L("xuiRebirthSurvivorNoProfileSelected", "No profile selected"));
            SetLabel(lblSelectedSubtitle, string.Empty);
            SetLabel(lblSelectedIdentityMeta, string.Empty);
            SetLabel(lblSelectedSignatureHeading, string.Empty);
            SetLabel(lblSelectedSignatureName, string.Empty);
            SetSprite(selectedSignatureBonusIcon, string.Empty);
            SetLabel(lblSelectedSignatureDescription, profiles.Length == 0
                ? RebirthSurvivorUiText.L("xuiRebirthSurvivorCreateFirstProfile", "Create a reusable Survivor Profile to define a starting Experience, Traits, and Diet.")
                : RebirthSurvivorUiText.L("xuiRebirthSurvivorSelectProfilePrompt", "Select a Survivor Profile to inspect it."));
            SetLabel(lblSelectedStartingItems, string.Empty);
            previewPlayerProfileName = string.Empty;
            SetLabel(lblSelectedDescription, string.Empty);
            SetLabel(lblSelectedBonuses, string.Empty);
            SetLabel(lblSelectedWeaknesses, string.Empty);
            SetLabel(lblSelectedDetails, string.Empty);
            RenderSelectedProfileDetails(null);
        }
        RebirthSurvivorProfileCompatibility selectedCompatibility = has ? RebirthSurvivorProfileStore.EvaluateCompatibility(selectedProfile) : null;
        bool selectedReady = selectedCompatibility != null &&
            selectedCompatibility.Kind == RebirthSurvivorProfileCompatibilityKind.Ready;
        SetVisible(btnReview, has && !spawnSelectionMode);
        SetVisible(btnEdit, has && !spawnSelectionMode);
        SetVisible(btnDuplicate, has && !spawnSelectionMode);
        SetVisible(btnDelete, has && !spawnSelectionMode);
        SetVisible(btnSelectForWorld, spawnSelectionMode && has && selectedReady);
        SetVisible(profileSelectionTimingHelp, !spawnSelectionMode);
        if (spawnSelectionMode && has && string.IsNullOrEmpty(statusMessage))
        {
            statusMessage = selectedReady
                ? RebirthSurvivorUiText.L("xuiRebirthSurvivorSelectForWorldStatus", "Select this Survivor Profile for this world.")
                : (selectedCompatibility != null && !string.IsNullOrEmpty(selectedCompatibility.Message)
                    ? selectedCompatibility.Message
                    : RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileNeedsReview", "This Survivor Profile needs review before it can be used."));
        }
        UpdateProfileNativeScroll();
        SetLabel(lblStatus, statusMessage);
        SetVisible(deletePanel, deleteConfirmVisible);
        if (deleteConfirmVisible && selectedProfile != null)
        {
            SetLabel(lblDeletePrompt, string.Format(RebirthSurvivorUiText.L("xuiRebirthSurvivorDeleteConfirmFormat", "Delete local Survivor Profile '{0}'? Existing world characters will not be changed."), selectedProfile.ProfileName));
        }
    }

    private string BuildProfileSubtitle(RebirthSurvivorProfile profile)
    {
        if (profile == null) return string.Empty;
        return "[B58CFF]" + RebirthSurvivorUiText.ResolveDefinitionName(profile.BackgroundId) + "[-]\n" +
            "[80C8F0]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorDiet", "Diet") + "[-]: " +
            RebirthSurvivorUiText.ResolveDefinitionName(profile.DietId);
    }

    private string BuildIdentityMeta(RebirthSurvivorProfile profile)
    {
        if (profile == null) return string.Empty;
        string playerProfileName;
        if (!profile.CreationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out playerProfileName) || string.IsNullOrEmpty(playerProfileName))
            playerProfileName = RebirthSurvivorUiText.L("xuiRebirthSurvivorNotSelected", "Not selected");
        return "[8F8F98]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileTitle", "Player Profile").ToUpperInvariant() + "[-]  " + playerProfileName +
            "    [8F8F98]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorCreated", "Created").ToUpperInvariant() + "[-]  " +
            profile.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private void BindSelectedPlayerPreview(RebirthSurvivorProfile profile)
    {
        if (profile == null || selectedPlayerPreview == null) return;
        string playerProfileName;
        if (!profile.CreationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out playerProfileName) || string.IsNullOrEmpty(playerProfileName)) return;
        if (string.Equals(previewPlayerProfileName, playerProfileName, StringComparison.OrdinalIgnoreCase)) return;
        string error;
        if (RebirthNativePlayerProfileBridge.TrySelectPlayerProfileForPreview(this, "selectedPlayerPreview", playerProfileName, out error))
            previewPlayerProfileName = playerProfileName;
        else if (RebirthSurvivorDebug.Enabled)
            Log.Warning("[REBIRTH Survivor][ProfileManager] live SDCS preview failed profile='" + playerProfileName + "' error=" + error);
    }


    private string BuildProfileDescription(RebirthSurvivorProfile profile)
    {
        if (profile == null) return string.Empty;
        RebirthBackgroundDefinition background;
        if (!RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out background) || background == null) return string.Empty;
        return RebirthSurvivorUiText.L(background.DescriptionKey, background.Identity);
    }

    private string selectedBackgroundItemsText(RebirthSurvivorProfile profile)
    {
        RebirthBackgroundDefinition background;
        if (profile == null || !RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out background) || background == null) return string.Empty;
        return RebirthSurvivorUiText.StartingItemsBullets(background);
    }

    private string BuildStartingBonuses(RebirthSurvivorProfile profile)
    {
        RebirthBackgroundDefinition background;
        if (profile == null || !RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out background) || background == null) return string.Empty;

        float positiveSkillPoints = 0f;
        for (int i = 0; i < background.StartingSkills.Count; i++)
        {
            float value = ResolveStartingSkillValue(background.StartingSkills[i]);
            if (value > 0f) positiveSkillPoints += value;
        }

        StringBuilder text = new StringBuilder();
        text.Append("[9FD35D]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorStartingBonuses", "STARTING BONUSES")).Append("[-]\n");
        bool any = false;
        if (positiveSkillPoints > 0f)
        {
            text.Append("[9FD35D]+ ").Append(FormatSkillValue(positiveSkillPoints)).Append(" ")
                .Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorSkillPoints", "Starting skill levels")).Append("[-]");
            any = true;
        }
        if (background.StartingKnowledgeIds.Count > 0)
        {
            if (any) text.Append("  •  ");
            text.Append("[80C8F0]+").Append(background.StartingKnowledgeIds.Count).Append(" ")
                .Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorKnowledge", "Knowledge")).Append("[-]");
            any = true;
        }
        if (background.CreationPointModifier > 0)
        {
            text.Append("\n[F1B925]+").Append(background.CreationPointModifier).Append(" ")
                .Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitPoints", "Trait Points")).Append("[-]");
            any = true;
        }
        if (!any) text.Append("[A8A8A8]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorNone", "None")).Append("[-]");
        return text.ToString();
    }

    private string BuildStartingWeaknesses(RebirthSurvivorProfile profile)
    {
        RebirthBackgroundDefinition background;
        if (profile == null || !RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out background) || background == null) return string.Empty;

        float negativeSkillPoints = 0f;
        for (int i = 0; i < background.StartingSkills.Count; i++)
        {
            float value = ResolveStartingSkillValue(background.StartingSkills[i]);
            if (value < 0f) negativeSkillPoints += Math.Abs(value);
        }

        StringBuilder text = new StringBuilder();
        text.Append("[E85B50]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorStartingWeaknesses", "STARTING WEAKNESSES")).Append("[-]\n");
        bool any = false;
        if (negativeSkillPoints > 0f)
        {
            text.Append("[E85B50]− ").Append(FormatSkillValue(negativeSkillPoints)).Append(" ")
                .Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorSkillPoints", "Starting skill levels")).Append("[-]");
            any = true;
        }
        if (background.CreationPointModifier < 0)
        {
            if (any) text.Append("  •  ");
            text.Append("[E85B50]").Append(background.CreationPointModifier).Append(" ")
                .Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorTraitPoints", "Trait Points")).Append("[-]");
            any = true;
        }
        if (!any) text.Append("[A8A8A8]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorNone", "None")).Append("[-]");
        return text.ToString();
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

    private string BuildProfileDetails(RebirthSurvivorProfile profile)
    {
        if (profile == null) return string.Empty;
        RebirthSurvivorProfileCompatibility compatibility = RebirthSurvivorProfileStore.EvaluateCompatibility(profile);
        string playerProfileName;
        if (!profile.CreationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out playerProfileName)) playerProfileName = string.Empty;
        StringBuilder text = new StringBuilder(512);
        text.Append("[B58CFF]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorAboutProfiles", "ABOUT SURVIVOR PROFILES")).Append("[-]\n")
            .Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileManagerHelp", "Survivor Profiles store your chosen appearance and starting setup. They can be reused in any save; world progress is stored separately.")).Append("\n\n")
            .Append("[FFFFFF]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileTitle", "Player Profile")).Append("[-]: ")
            .Append(string.IsNullOrEmpty(playerProfileName) ? RebirthSurvivorUiText.L("xuiRebirthSurvivorNotSelected", "Not selected") : playerProfileName).Append("  •  ")
            .Append("[FFFFFF]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorTraits", "Traits")).Append("[-]: ").Append(profile.TraitIds.Count).Append("  •  ")
            .Append(CompatibilityLong(compatibility));
        return text.ToString();
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
                    ProfileDetailDisplay e = new ProfileDetailDisplay
                    {
                        IconKey = SkillIconKey(skill.SkillId),
                        Name = RebirthSurvivorUiText.ResolveDefinitionName(skill.SkillId),
                        Value = (value > 0f ? "[74DC54]+" : "[E41215]") + FormatSkillValue(value) + "[-]",
                        RowKind = value > 0f ? "SKILL" : "WEAKNESS",
                        ProgressionTargetId = skill.SkillId
                    };
                    if (value > 0f) progression.Add(e); else weaknesses.Add(e);
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
                        RowKind = "KNOWLEDGE",
                        ProgressionTargetId = id
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
                string value = negative
                    ? "[E41215]" + pointText + " PT[-]"
                    : "[74DC54]" + pointText + " PT[-]";
                ProfileDetailDisplay entry = new ProfileDetailDisplay
                {
                    IconKey = trait.IconKey,
                    Name = RebirthSurvivorUiText.L(trait.NameKey, trait.Id),
                    Value = value,
                    RowKind = negative ? "NEGATIVE TRAIT" : "POSITIVE TRAIT",
                    ProgressionTargetId = string.Empty
                };
                if (negative) negativeTraits.Add(entry); else positiveTraits.Add(entry);
            }

            positiveTraits.Sort(delegate(ProfileDetailDisplay x, ProfileDetailDisplay y)
            {
                return string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            negativeTraits.Sort(delegate(ProfileDetailDisplay x, ProfileDetailDisplay y)
            {
                return string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
            });
        }

        List<ProfileDetailDisplay> traits = new List<ProfileDetailDisplay>();
        traits.AddRange(positiveTraits);
        traits.AddRange(negativeTraits);
        selectedProgressionDisplayCount = progression.Count;
        progressionOffset = Mathf.Clamp(progressionOffset, 0, Math.Max(0, selectedProgressionDisplayCount - ProgressionVisibleRows));
        selectedTraitDisplayCount = traits.Count;
        traitOffset = Mathf.Clamp(traitOffset, 0, Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows));
        if (RebirthSurvivorDebug.Enabled)
        {
            Log.Out("[REBIRTH Survivor][ProfileManager][TraitScroll] render-sync total=" + selectedTraitDisplayCount +
                " visible=" + TraitVisibleRows + " offset=" + traitOffset +
                " initialFrames=" + traitNativeInitialSyncFrames);
            StringBuilder traitTrace = new StringBuilder();
            for (int traceIndex = 0; profile != null && profile.TraitIds != null && traceIndex < profile.TraitIds.Count; traceIndex++)
            {
                if (traceIndex > 0) traitTrace.Append(",");
                traitTrace.Append(profile.TraitIds[traceIndex]);
            }
            Log.Out("[REBIRTH Survivor][ProfileManager][Traits] profile='" + (profile != null ? profile.ProfileId : "<none>") +
                "' stored=" + (profile != null && profile.TraitIds != null ? profile.TraitIds.Count : 0) + " resolved=" + selectedTraitDisplayCount +
                " offset=" + traitOffset + " ids=" + traitTrace);
        }

        RenderDetailSection(0, ProgressionVisibleRows, progression, progressionOffset, false);
        progressionTooltipSyncFrames = 2;
        RenderDetailSection(TraitBaseIndex, TraitVisibleRows, traits, traitOffset, true);
        RenderDetailSection(WeaknessBaseIndex, WeaknessVisibleRows, weaknesses, 0, false);
        RenderStartingItems(startingItems);
        UpdateProgressionNativeScroll();
        UpdateTraitNativeScroll();
        SetControllerSize(profileDetailScrollContent, 848, 366);
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
            detailProgressionTargets[slot] = string.Empty;
            if (!visible)
            {
                SetVisible(detailButtons[slot], false);
                SetSprite(detailIcons[slot], string.Empty);
                SetSpriteColor(detailIcons[slot], new Color32(255,255,255,255));
                SetLabel(detailNames[slot], string.Empty);
                if (slot < ProgressionVisibleRows)
                {
                    if (detailNames[slot] != null) detailNames[slot].ToolTip = string.Empty;
                    if (detailButtons[slot] != null && detailButtons[slot].ViewComponent != null) detailButtons[slot].ViewComponent.ToolTip = string.Empty;
                }
                SetLabel(detailValues[slot], string.Empty);
                SetLabelColor(detailValues[slot], new Color32(220,220,225,255));
                continue;
            }

            ProfileDetailDisplay e = entries[index];
            detailProgressionTargets[slot] = e.ProgressionTargetId ?? string.Empty;
            SetVisible(detailButtons[slot], !string.IsNullOrEmpty(detailProgressionTargets[slot]));
            SetSprite(detailIcons[slot], e.IconKey);
            SetLabel(detailNames[slot], e.Name);
            if (slot < ProgressionVisibleRows)
            {
                if (detailNames[slot] != null) detailNames[slot].ToolTip = string.Empty;
                if (detailButtons[slot] != null && detailButtons[slot].ViewComponent != null) detailButtons[slot].ViewComponent.ToolTip = string.Empty;
            }
            SetLabel(detailValues[slot], e.Value);

            if (traitSection)
            {
                bool negative = string.Equals(e.RowKind, "NEGATIVE TRAIT", StringComparison.Ordinal);
                Color32 traitColor = negative ? new Color32(204,107,100,255) : new Color32(143,209,143,255);
                SetSpriteColor(detailIcons[slot], traitColor);
                SetLabelColor(detailValues[slot], traitColor);
            }
            else
            {
                SetSpriteColor(detailIcons[slot], new Color32(255,255,255,255));
                SetLabelColor(detailValues[slot], new Color32(220,220,225,255));
            }
        }
    }

    private void RefreshProgressionTruncationTooltips()
    {
        for (int i = 0; i < ProgressionVisibleRows; i++)
        {
            XUiV_Label label = detailNames[i];
            string full = label != null ? (label.Text ?? string.Empty) : string.Empty;
            string tooltip = string.Empty;
            try
            {
                bool rowVisible = detailRows[i] != null && detailRows[i].ViewComponent != null && detailRows[i].ViewComponent.IsVisible;
                if (rowVisible && label != null && label.label != null && full.Length > 0)
                {
                    // processedText is NGUI's final one-line text after clamp/ellipsis has been applied.
                    // Comparing it with the authored full string means short names never get a tooltip.
                    label.label.MarkAsChanged();
                    string processed = label.label.processedText ?? string.Empty;
                    string plainFull = NGUIText.StripSymbols(full) ?? string.Empty;
                    string plainProcessed = NGUIText.StripSymbols(processed) ?? string.Empty;
                    bool truncated = !string.Equals(plainFull, plainProcessed, StringComparison.Ordinal);
                    if (truncated) tooltip = full;
                }
            }
            catch
            {
                tooltip = string.Empty;
            }

            if (label != null) label.ToolTip = tooltip;
            XUiController hit = detailButtons[i];
            if (hit != null && hit.ViewComponent != null) hit.ViewComponent.ToolTip = tooltip;
        }
    }

    private void RenderStartingItems(List<RebirthStartingItemDefinition> items)
    {
        int count = items != null ? items.Count : 0;
        for (int i = 0; i < StartingItemVisibleRows; i++)
        {
            bool visible = i < count;
            SetVisible(startingItemRows[i], visible);
            if (!visible)
            {
                SetLabel(startingItemNames[i], string.Empty);
                SetLabel(startingItemMeta[i], string.Empty);
                SetStartingItemIcon(startingItemIcons[i], null);
                continue;
            }

            RebirthStartingItemDefinition item = items[i];
            SetLabel(startingItemNames[i], RebirthSurvivorUiText.L(item.NameKey, item.ItemId));
            SetLabel(startingItemMeta[i], StartingItemMeta(item));
            SetStartingItemIcon(startingItemIcons[i], item);
        }
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
            case "ItemsWeaponsCleaver001_FR":
                atlas = "ItemIconAtlas"; icon = "FR_Cleaver_icon"; return true;
            case "ItemsWeaponsJunkBaton001_FR":
                atlas = "ItemIconAtlas"; icon = "ItemsWeaponsJunkBaton001_FR"; return true;
            case "ItemsWeaponsScythe004_FR":
                atlas = "ItemIconAtlas"; icon = "ItemsWeaponsScythe004_FR"; return true;
            case "FuriousRamsayFountainPen":
                atlas = "ItemIconAtlas"; icon = "FR_FountainPen_icon"; return true;
            case "FuriousRamsayHammerPliers":
                atlas = "ItemIconAtlas"; icon = "FR_HammerPliers_icon"; return true;
            case "FuriousRamsayScrewdriver":
                atlas = "ItemIconAtlas"; icon = "FR_Screwdriver_icon"; return true;
            case "FuriousRamsayPliers":
                atlas = "ItemIconAtlas"; icon = "FR_Pliers_icon"; return true;
            case "guppyFireExtinguisherItem":
                atlas = "ItemIconAtlas"; icon = "guppyFireExtinguisher"; return true;
            case "qt_claude":
                // Base-game treasure maps inherit treasureQuestMaster, whose authored CustomIcon is
                // treasureQuestMaster. Use the native ItemIconAtlas sprite directly; do not package
                // a duplicate Survivor atlas image for this vanilla asset.
                atlas = "ItemIconAtlas"; icon = "treasureQuestMaster"; return true;
            case "FuriousRamsayPropaneTank":
                atlas = "ItemIconAtlas"; icon = "FR_SM_Propane_icon"; return true;
            case "modArmorHelmetLight":
                atlas = "ItemIconAtlas"; icon = "modArmorHelmetLight"; return true;
            case "FuriousRamsaySeedBundle":
                icon = "bundleFarm"; return true;
            case "FuriousRamsayWaterTank":
                icon = "cntBarrelPlasticSingle00"; return true;
        }
        return false;
    }

    private sealed class ProfileDetailDisplay
    {
        public string IconKey = string.Empty;
        public string Name = string.Empty;
        public string Value = string.Empty;
        public string RowKind = string.Empty;
        public string ProgressionTargetId = string.Empty;
    }

    private static string SkillIconKey(string skillId)
    {
        string id = (skillId ?? string.Empty).Trim().ToLowerInvariant();
        if (!id.StartsWith("skill.", StringComparison.Ordinal)) return "rb_skill_unarmed";
        string suffix = id.Substring("skill.".Length).Replace('.', '_').Replace('-', '_');
        return "rb_skill_" + suffix;
    }

    private void ProfileDetail_OnPressed(XUiController sender, int mouseButton)
    {
        int slot;
        if (!detailButtonIndex.TryGetValue(sender, out slot) || slot < 0 || slot >= detailProgressionTargets.Length) return;
        string focusId = (detailProgressionTargets[slot] ?? string.Empty).Trim();
        if (focusId.Length == 0) return;
        OpenProfileProgressionExplorer(focusId, sender);
    }

    private void OpenProfileProgressionExplorer(string focusId, XUiController returnFocus)
    {
        focusId = (focusId ?? string.Empty).Trim();
        if (focusId.Length == 0 || xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return;

        RebirthProgressionExplorerReturnContext returnContext = new RebirthProgressionExplorerReturnContext(
            WindowGroupId,
            string.Empty,
            RebirthSurvivorUiText.L("xuiRebirthProgressionExplorerReturnProfiles", "RETURN TO SURVIVOR PROFILES"));
        RebirthProgressionExplorerLaunchRequest request = new RebirthProgressionExplorerLaunchRequest(
            focusId,
            RebirthProgressionExplorerMode.Neutral,
            "Survivor Profile summary",
            returnContext,
            null);

        xui.playerUI.windowManager.Close(WindowGroupId);
        string error;
        if (!RebirthProgressionExplorerUiService.Open(xui, request, out error))
        {
            XUiController caller = xui.FindWindowGroupByName(WindowGroupId);
            if (caller != null && caller.windowGroup != null) xui.playerUI.windowManager.Open((GUIWindow)caller.windowGroup, true);
            statusMessage = "[E41215]" + (string.IsNullOrEmpty(error) ? "Progression Explorer could not be opened." : error) + "[-]";
            Render();
        }
    }

    public static void OpenForSpawnSelection(XUi xui)
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null)
        {
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] cannot open Survivor Profiles for spawn selection: XUi/windowManager unavailable.");
            return;
        }

        XUiController target = xui.FindWindowGroupByName(WindowGroupId);
        if (target == null || target.windowGroup == null)
        {
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] cannot open Survivor Profiles for spawn selection: window group is not registered.");
            return;
        }

        spawnSelectionMode = true;
        GUIWindowManager manager = xui.playerUI.windowManager;
        GUIWindow spawnWindow = manager.GetWindow("spawnselection");
        bool spawnOpenBefore = manager.IsWindowOpen("spawnselection");
        if (spawnWindow != null) spawnWindow.isEscClosable = false;

        // V3.2 owns the loading-screen -> spawnselection transition through the native spawn window.
        // Closing that window while its Update redirects first entry leaves the loading backdrop on
        // screen even though Survivor Profiles is only queued in windowsToOpen. Preserve the native
        // window underneath and open the full-screen Survivor selector as a non-modal overlay.
        manager.Open((GUIWindow)target.windowGroup, false, true);
        bool opened = manager.IsWindowOpen(WindowGroupId);
        bool spawnOpenAfter = manager.IsWindowOpen("spawnselection");
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("Survivor Profiles OVERLAY queued selectionMode=True beforeSpawn=" + spawnOpenBefore
            + " afterSpawn=" + spawnOpenAfter + " profilesQueuedOrOpen=" + opened); }
        if (!opened || !spawnOpenAfter)
        {
            spawnSelectionMode = false;
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] mandatory Survivor Profiles overlay contract failed profiles=" + opened
                + " spawn=" + spawnOpenAfter + "; keeping/reopening native spawnselection fail-closed.");
            if (opened) manager.Close((GUIWindow)target.windowGroup);
            if (!manager.IsWindowOpen("spawnselection")) manager.Open("spawnselection", true);
        }
    }

    public static bool OpenStandard(XUi xui)
    {
        spawnSelectionMode = false;
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return false;
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth) return false;

        XUiController target = xui.FindWindowGroupByName(WindowGroupId);
        if (target == null || target.windowGroup == null)
        {
            Log.Error("[REBIRTH Survivor][UiRoute] Cannot open Survivor Profiles: window group '" + WindowGroupId + "' is not registered.");
            return false;
        }

        GUIWindowManager manager = xui.playerUI.windowManager;
        manager.Open((GUIWindow)target.windowGroup, true, false);
        bool opened = manager.IsWindowOpen(WindowGroupId);
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("Survivor Profiles OpenStandard requested open=" + opened + " mainMenu=" + manager.IsWindowOpen("mainMenu")); }
        if (!opened)
        {
            Log.Error("[REBIRTH Survivor][UiRoute] Survivor Profiles window group was registered but did not open; restoring Main Menu if needed.");
            if (!manager.IsWindowOpen("mainMenu")) manager.Open("mainMenu", true);
        }
        return opened;
    }

    private void SelectForWorld_OnPressed(XUiController sender, int mouseButton)
    {
        if (!spawnSelectionMode) return;
        RebirthSurvivorProfile profile = GetSelectedProfile();
        if (profile == null)
        {
            statusMessage = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorSelectProfilePrompt", "Select a Survivor Profile first.") + "[-]";
            Render();
            return;
        }

        string error;
        if (!RebirthSurvivorFirstEntryUiService.StageExistingProfile(profile, out error))
        {
            statusMessage = "[CC6B64]" + (string.IsNullOrEmpty(error) ? "Survivor Profile could not be selected." : error) + "[-]";
            Render();
            return;
        }

        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("Survivor Profile selected for world profileId=" + profile.ProfileId +
            " name='" + (profile.ProfileName ?? string.Empty) + "'"); }
        spawnSelectionMode = false;
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return;
        GUIWindowManager manager = xui.playerUI.windowManager;
        if (manager.IsWindowOpen(WindowGroupId)) manager.Close(WindowGroupId);
        if (!manager.IsWindowOpen("spawnselection")) manager.Open("spawnselection", true);
    }

    private void ProfileRow_OnPressed(XUiController sender, int mouseButton)
    {
        int visibleIndex;
        if (!rowIndex.TryGetValue(sender, out visibleIndex)) return;
        int index = offset + visibleIndex;
        if (index < 0 || index >= profiles.Length) return;
        selectedProfileId = profiles[index].ProfileId;
        progressionOffset = 0;
        traitOffset = 0;
        progressionNativeInitialSyncFrames = 2;
        traitNativeInitialSyncFrames = 2;
        statusMessage = string.Empty;
        Render();
    }

    private void Create_OnPressed(XUiController sender, int mouseButton)
    {
        statusMessage = string.Empty;
        { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("SURVIVOR-PROFILE create requested from manager"); }
        CloseForCreatorTransition();
        XUiC_RebirthSurvivorCreator.OpenCreate(xui, RefreshAfterCreator);
    }

    private void CloseForCreatorTransition()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return;
        GUIWindowManager manager = xui.playerUI.windowManager;
        if (manager.IsWindowOpen(WindowGroupId))
        {
            { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("SURVIVOR-MANAGER SUSPEND-FOR-CREATOR close=True spawnSelectionMode=" + spawnSelectionMode); }
            manager.Close(WindowGroupId);
        }
    }

    private void ReopenAfterFailedCreatorTransition()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null) return;
        GUIWindowManager manager = xui.playerUI.windowManager;
        if (!manager.IsWindowOpen(WindowGroupId)) manager.Open(WindowGroupId, true);
    }

    private void Search_OnChanged(XUiController sender, string text, bool changeFromCode)
    {
        if (changeFromCode) return;
        searchText = text ?? string.Empty;
        offset = 0;
        ApplyFilterAndSort();
        if (!string.IsNullOrEmpty(selectedProfileId) && IndexOfProfile(selectedProfileId) < 0) selectedProfileId = string.Empty;
        if (string.IsNullOrEmpty(selectedProfileId) && profiles.Length > 0) selectedProfileId = profiles[0].ProfileId;
        Render();
    }

    private void Sort_OnPressed(XUiController sender, int mouseButton)
    {
        sortMode = (sortMode + 1) % 3;
        offset = 0;
        ApplyFilterAndSort();
        EnsureSelectionVisible();
        Render();
    }

    private void Review_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthSurvivorProfile profile = GetSelectedProfile();
        if (profile == null) return;
        CloseForCreatorTransition();
        string error;
        if (!XUiC_RebirthSurvivorCreator.OpenReview(xui, profile, out error))
        {
            ReopenAfterFailedCreatorTransition();
            statusMessage = "[CC6B64]" + error + "[-]";
            Render();
        }
    }

    private void Edit_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthSurvivorProfile profile = GetSelectedProfile();
        if (profile == null) return;
        CloseForCreatorTransition();
        string error;
        if (!XUiC_RebirthSurvivorCreator.OpenEdit(xui, profile, RefreshAfterCreator, out error))
        {
            ReopenAfterFailedCreatorTransition();
            statusMessage = "[CC6B64]" + error + "[-]";
            Render();
        }
    }

    private void Duplicate_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthSurvivorProfile profile = GetSelectedProfile();
        if (profile == null) return;
        string name = BuildDuplicateName(profile.ProfileName);
        RebirthSurvivorProfile duplicate;
        string error;
        if (RebirthSurvivorProfileStore.TryDuplicate(profile.ProfileId, name, out duplicate, out error))
        {
            selectedProfileId = duplicate.ProfileId;
            statusMessage = "[8FD18F]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileDuplicated", "Profile duplicated.") + "[-]";
            RefreshProfiles(false);
        }
        else statusMessage = "[CC6B64]" + error + "[-]";
        Render();
    }

    private void Delete_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthSurvivorProfile target = GetSelectedProfile();
        if (target == null) return;
        deleteConfirmProfileId = target.ProfileId ?? string.Empty;
        deleteConfirmModifiedAtUtc = target.ModifiedAtUtc;
        deleteConfirmVisible = true;
        Render();
        SelectController(btnConfirmDelete);
    }

    private void ConfirmDelete_OnPressed(XUiController sender, int mouseButton)
    {
        string targetId = deleteConfirmProfileId;
        DateTime expectedModified = deleteConfirmModifiedAtUtc;
        deleteConfirmVisible = false;
        deleteConfirmProfileId = string.Empty;
        deleteConfirmModifiedAtUtc = default(DateTime);
        RebirthSurvivorProfile profile;
        if (string.IsNullOrEmpty(targetId) || !RebirthSurvivorProfileStore.TryGet(targetId, out profile) || profile == null)
        {
            statusMessage = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileNoLongerExists", "The profile selected for deletion no longer exists.") + "[-]";
            RefreshProfiles(true);
            Render();
            return;
        }
        if (profile.ModifiedAtUtc != expectedModified)
        {
            statusMessage = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileChangedBeforeDelete", "The profile changed after confirmation was opened. Review it again before deleting.") + "[-]";
            selectedProfileId = profile.ProfileId;
            RefreshProfiles(false);
            Render();
            return;
        }
        string error;
        if (RebirthSurvivorProfileStore.TryDelete(targetId, out error))
        {
            if (string.Equals(selectedProfileId, targetId, StringComparison.OrdinalIgnoreCase)) selectedProfileId = string.Empty;
            statusMessage = "[8FD18F]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorProfileDeleted", "Local profile deleted. Existing world characters were not changed.") + "[-]";
            RefreshProfiles(true);
        }
        else statusMessage = "[CC6B64]" + error + "[-]";
        Render();
    }

    private void CancelDelete_OnPressed(XUiController sender, int mouseButton)
    {
        deleteConfirmVisible = false;
        deleteConfirmProfileId = string.Empty;
        deleteConfirmModifiedAtUtc = default(DateTime);
        Render();
        SelectController(btnDelete);
    }

    private void ProfileListScroll(float delta)
    {
        if (RebirthScrollbarPagingPolicy.Enabled) offset = (int)RebirthScrollbarPagingPolicy.Step(offset, Math.Max(0, profiles.Length - VisibleRows), VisibleRows, delta > 0f ? -1 : delta < 0f ? 1 : 0);
        else if (delta > 0f) offset--;
        else if (delta < 0f) offset++;
        offset = ClampOffset(offset);
        Render();
    }

    private float pagingDragRemainder;
    private void ProfileThumbDrag(float dy)
    {
        if (profiles.Length <= VisibleRows || dy == 0f) return;
        int thumbHeight = GetThumbHeight(profiles.Length, VisibleRows, ProfileTrackHeight);
        int travel = Math.Max(1, ProfileTrackHeight - thumbHeight);
        int max = Math.Max(1, profiles.Length - VisibleRows);
        int step = Mathf.RoundToInt(dy * (max / (float)travel));
        if (RebirthScrollbarPagingPolicy.Enabled)
        {
            pagingDragRemainder = Mathf.Clamp(offset + pagingDragRemainder + dy * (max / (float)travel), 0f, max) - offset;
            int next = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(offset + pagingDragRemainder, max, VisibleRows, true);
            pagingDragRemainder -= next - offset; offset = next;
        }
        else { if (step == 0) step = dy > 0f ? 1 : -1; offset = ClampOffset(offset + step); }
        Render();
    }

    private void ProgressionListScroll(float delta)
    {
        int oldOffset = progressionOffset;
        if (RebirthScrollbarPagingPolicy.Enabled) progressionOffset = (int)RebirthScrollbarPagingPolicy.Step(progressionOffset, Math.Max(0, selectedProgressionDisplayCount - ProgressionVisibleRows), ProgressionVisibleRows, delta > 0f ? -1 : delta < 0f ? 1 : 0);
        else if (delta > 0f) progressionOffset--;
        else if (delta < 0f) progressionOffset++;
        progressionOffset = Mathf.Clamp(progressionOffset, 0, Math.Max(0, selectedProgressionDisplayCount - ProgressionVisibleRows));
        if (RebirthSurvivorDebug.Enabled)
            Log.Out("[REBIRTH Survivor][ProfileManager][ProgressionScroll] wheel delta=" + delta +
                " total=" + selectedProgressionDisplayCount + " visible=" + ProgressionVisibleRows +
                " old=" + oldOffset + " new=" + progressionOffset);
        if (progressionOffset != oldOffset) Render();
    }

    private void TraitListScroll(float delta)
    {
        int oldOffset = traitOffset;
        if (RebirthScrollbarPagingPolicy.Enabled) traitOffset = (int)RebirthScrollbarPagingPolicy.Step(traitOffset, Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows), TraitVisibleRows, delta > 0f ? -1 : delta < 0f ? 1 : 0);
        else if (delta > 0f) traitOffset--;
        else if (delta < 0f) traitOffset++;
        traitOffset = Mathf.Clamp(traitOffset, 0, Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows));
        if (RebirthSurvivorDebug.Enabled)
            Log.Out("[REBIRTH Survivor][ProfileManager][TraitScroll] wheel delta=" + delta +
                " total=" + selectedTraitDisplayCount + " visible=" + TraitVisibleRows +
                " old=" + oldOffset + " new=" + traitOffset);
        if (traitOffset != oldOffset) Render();
    }


    private void UpdateProfileNativeScroll()
    {
        if (profileNativeScrollHost != null && profileNativeScrollHost.ViewComponent != null)
            profileNativeScrollHost.ViewComponent.IsVisible = profiles.Length > VisibleRows;
        if (profileNativeScrollProxy == null || profileNativeScrollProxy.ViewComponent == null) return;
        int contentHeight = profiles.Length > VisibleRows
            ? Math.Max(ProfileTrackHeight + 1, Mathf.CeilToInt(ProfileTrackHeight * (profiles.Length / (float)VisibleRows)))
            : ProfileTrackHeight;
        profileNativeScrollProxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        int maxOffset = Math.Max(0, profiles.Length - VisibleRows);
        float normalized = maxOffset > 0 ? Mathf.Clamp01(offset / (float)maxOffset) : 0f;
        syncingProfileNativeScroll = true;
        RefreshNativeScrollView(profileNativeScrollView);
        TrySetNativeScrollValue(profileNativeScrollView, normalized);
        RefreshNativeScrollView(profileNativeScrollView);
        syncingProfileNativeScroll = false;
    }

    private void PollProfileNativeScroll()
    {
        if (syncingProfileNativeScroll) return;
        int maxOffset = Math.Max(0, profiles.Length - VisibleRows);
        if (maxOffset <= 0) return;
        float normalized;
        if (!TryGetNativeScrollValue(profileNativeScrollView, out normalized)) return;
        int requested = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset), maxOffset, VisibleRows, RebirthScrollbarPagingPolicy.Enabled);
        if (requested == offset) return;
        offset = requested;
        Render();
    }

    private void UpdateProgressionNativeScroll()
    {
        if (progressionNativeScrollHost != null && progressionNativeScrollHost.ViewComponent != null)
            progressionNativeScrollHost.ViewComponent.IsVisible = selectedProgressionDisplayCount > ProgressionVisibleRows;
        if (progressionScrollCapture != null && progressionScrollCapture.ViewComponent != null)
            progressionScrollCapture.ViewComponent.IsVisible = selectedProgressionDisplayCount > 0;
        if (progressionNativeScrollProxy == null || progressionNativeScrollProxy.ViewComponent == null) return;

        int contentHeight = selectedProgressionDisplayCount > ProgressionVisibleRows
            ? Math.Max(ProgressionTrackHeight + 1, Mathf.CeilToInt(ProgressionTrackHeight * (selectedProgressionDisplayCount / (float)ProgressionVisibleRows)))
            : ProgressionTrackHeight;
        progressionNativeScrollProxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        int maxOffset = Math.Max(0, selectedProgressionDisplayCount - ProgressionVisibleRows);
        float normalized = maxOffset > 0 ? Mathf.Clamp01(progressionOffset / (float)maxOffset) : 0f;

        syncingProgressionNativeScroll = true;
        RefreshNativeScrollView(progressionNativeScrollView);
        TrySetNativeScrollValue(progressionNativeScrollView, normalized);
        RefreshNativeScrollView(progressionNativeScrollView);
        syncingProgressionNativeScroll = false;
    }

    private void PollProgressionNativeScroll()
    {
        if (syncingProgressionNativeScroll) return;
        int maxOffset = Math.Max(0, selectedProgressionDisplayCount - ProgressionVisibleRows);
        if (maxOffset <= 0) return;

        float normalized;
        if (!TryGetNativeScrollValue(progressionNativeScrollView, out normalized)) return;
        int requested = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset), maxOffset, ProgressionVisibleRows, RebirthScrollbarPagingPolicy.Enabled);
        if (requested == progressionOffset) return;
        int oldOffset = progressionOffset;
        progressionOffset = requested;
        if (RebirthSurvivorDebug.Enabled)
            Log.Out("[REBIRTH Survivor][ProfileManager][ProgressionScroll] native normalized=" + normalized +
                " total=" + selectedProgressionDisplayCount + " old=" + oldOffset + " new=" + progressionOffset);
        Render();
    }

    private void UpdateTraitNativeScroll()
    {
        if (traitNativeScrollHost != null && traitNativeScrollHost.ViewComponent != null)
            traitNativeScrollHost.ViewComponent.IsVisible = true;
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
        RefreshNativeScrollView(traitNativeScrollView);
        TrySetNativeScrollValue(traitNativeScrollView, normalized);
        RefreshNativeScrollView(traitNativeScrollView);
        syncingTraitNativeScroll = false;
    }

    private void PollTraitNativeScroll()
    {
        if (syncingTraitNativeScroll) return;
        int maxOffset = Math.Max(0, selectedTraitDisplayCount - TraitVisibleRows);
        if (maxOffset <= 0) return;

        float normalized;
        if (!TryGetNativeScrollValue(traitNativeScrollView, out normalized)) return;
        int requested = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * maxOffset), 0, maxOffset), maxOffset, TraitVisibleRows, RebirthScrollbarPagingPolicy.Enabled);
        if (requested == traitOffset) return;
        int oldOffset = traitOffset;
        traitOffset = requested;
        if (RebirthSurvivorDebug.Enabled)
            Log.Out("[REBIRTH Survivor][ProfileManager][TraitScroll] native normalized=" + normalized +
                " total=" + selectedTraitDisplayCount + " old=" + oldOffset + " new=" + traitOffset);
        Render();
    }

    private void Back_OnPressed(XUiController sender, int mouseButton)
    {
        if (xui == null || xui.playerUI == null) return;
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("SURVIVOR-MANAGER BACK nativePickerActive=" + RebirthNativePlayerProfileBridge.IsActive +
            " spawnSelectionMode=" + spawnSelectionMode); }
        GUIWindowManager manager = xui.playerUI.windowManager;
        if (spawnSelectionMode)
        {
            spawnSelectionMode = false;
            if (manager.IsWindowOpen("playerProfilesCreate")) manager.Close("playerProfilesCreate");
            if (manager.IsWindowOpen("playerProfiles")) manager.Close("playerProfiles");
            if (manager.IsWindowOpen("playGamePaging")) manager.Close("playGamePaging");
            if (manager.IsWindowOpen(WindowGroupId)) manager.Close(WindowGroupId);
            // Do not expose the locked native spawn window while the disconnect is deferred to
            // GameUpdate.  Back is a full cancel of the first-entry flow and must return to the
            // main menu, not leave an unusable spawn-selection underlay on screen.
            if (manager.IsWindowOpen("spawnselection")) manager.Close("spawnselection");
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("Survivor Profiles BACK during mandatory first-entry selection; closed spawnselection and leaving world."); }
            RebirthSurvivorFirstEntryUiService.LeaveWorld();
            return;
        }
        if (manager.IsWindowOpen("playerProfilesCreate")) manager.Close("playerProfilesCreate");
        if (manager.IsWindowOpen("playerProfiles")) manager.Close("playerProfiles");
        if (manager.IsWindowOpen("playGamePaging")) manager.Close("playGamePaging");
        manager.Close((GUIWindow)windowGroup);
        manager.Open("mainMenu", true);
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("SURVIVOR-MANAGER BACK result mainMenuOpen=" + manager.IsWindowOpen("mainMenu")); }
    }

    private string BuildProfileRowSummary(RebirthSurvivorProfile profile, string experienceName, string dietName)
    {
        string playerProfileName;
        if (!profile.CreationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out playerProfileName) || string.IsNullOrEmpty(playerProfileName))
            playerProfileName = RebirthSurvivorUiText.L("xuiRebirthSurvivorNotSelected", "Not selected");
        return "[B58CFF]" + experienceName + "[-]  •  [80C8F0]" + dietName + "[-]\n" +
               "[D6D6DC]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorStartingSkills", "Skills") + "[-]: " + CompactStartingSkillSummary(profile) + "\n" +
               "[92929A]" + profile.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
               "  •  " + playerProfileName + "[-]";
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

    private string CompactTraitSummary(RebirthSurvivorProfile profile)
    {
        if (profile == null || profile.TraitIds == null || profile.TraitIds.Count == 0)
            return RebirthSurvivorUiText.L("xuiRebirthSurvivorNoTraits", "No Traits");
        StringBuilder text = new StringBuilder();
        int shown = Math.Min(2, profile.TraitIds.Count);
        for (int i = 0; i < shown; i++)
        {
            if (i > 0) text.Append(", ");
            text.Append(RebirthSurvivorUiText.ResolveDefinitionName(profile.TraitIds[i]));
        }
        if (profile.TraitIds.Count > shown) text.Append(" +").Append(profile.TraitIds.Count - shown);
        return text.ToString();
    }

    private void SelectController(XUiController controller)
    {
        if (controller == null || controller.ViewComponent == null || xui == null || xui.playerUI == null || xui.playerUI.CursorController == null) return;
        xui.playerUI.CursorController.SetNavigationTargetLater(controller.ViewComponent);
    }

    private string BuildDuplicateName(string sourceName)
    {
        string root = (sourceName ?? string.Empty).Trim();
        if (root.Length == 0) root = RebirthSurvivorUiText.L("xuiRebirthSurvivorUntitled", "Survivor");
        string suffix = " " + RebirthSurvivorUiText.L("xuiRebirthSurvivorCopy", "Copy");
        int baseMax = Math.Max(1, 48 - suffix.Length - 4);
        if (root.Length > baseMax) root = root.Substring(0, baseMax);
        string candidate = root + suffix;
        int number = 2;
        while (ProfileNameExists(candidate))
        {
            string numberText = " " + number.ToString(CultureInfo.InvariantCulture);
            int room = Math.Max(1, 48 - suffix.Length - numberText.Length);
            string numberedRoot = root.Length > room ? root.Substring(0, room) : root;
            candidate = numberedRoot + suffix + numberText;
            number++;
        }
        return candidate;
    }

    private bool ProfileNameExists(string name)
    {
        // Duplicate-name proposals must inspect the complete store, not the currently filtered UI roster.
        for (int i = 0; i < allProfiles.Length; i++)
            if (allProfiles[i] != null && string.Equals(allProfiles[i].ProfileName, name, StringComparison.CurrentCultureIgnoreCase)) return true;
        return false;
    }

    private RebirthSurvivorProfile GetSelectedProfile()
    {
        if (string.IsNullOrEmpty(selectedProfileId)) return null;
        for (int i = 0; i < profiles.Length; i++)
            if (string.Equals(profiles[i].ProfileId, selectedProfileId, StringComparison.OrdinalIgnoreCase)) return profiles[i];
        return null;
    }

    private int IndexOfProfile(string id)
    {
        for (int i = 0; i < profiles.Length; i++) if (string.Equals(profiles[i].ProfileId, id, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private void EnsureSelectionVisible()
    {
        int index = IndexOfProfile(selectedProfileId);
        if (index < 0) return;
        if (index < offset) offset = index;
        else if (index >= offset + VisibleRows) offset = index - VisibleRows + 1;
        offset = ClampOffset(offset);
    }

    private int ClampOffset(int value)
    {
        return (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(value, 0, Math.Max(0, profiles.Length - VisibleRows)), Math.Max(0, profiles.Length - VisibleRows), VisibleRows, RebirthScrollbarPagingPolicy.Enabled);
    }

    private static void WireScroll(XUiController controller, Action<float> callback)
    {
        if (controller == null || callback == null) return;
        if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true;
        controller.OnScroll += delegate(XUiController sender, float delta) { callback(delta); };
    }

    private void WireThumbDrag(XUiController thumb, Action<float> callback)
    {
        if (thumb == null || callback == null) return;
        if (thumb.ViewComponent != null) thumb.ViewComponent.EventOnDrag = true;
        thumb.OnDrag += delegate(XUiController sender, EDragType type, Vector2 delta)
        {
            if (type == EDragType.DragStart) pagingDragRemainder = 0f;
            if (type != EDragType.DragEnd) callback(-delta.y);
        };
    }

    private static int GetThumbHeight(int total, int visible, int trackHeight)
    {
        if (total <= visible || total <= 0) return trackHeight;
        return Mathf.Clamp(Mathf.RoundToInt(trackHeight * ((float)visible / total)), 28, trackHeight);
    }

    private static void UpdateScrollBar(XUiController track, XUiV_Button thumb, int offset, int total, int visible, int trackHeight)
    {
        bool needed = total > visible && total > 0;
        if (track != null && track.ViewComponent != null) track.ViewComponent.IsVisible = needed;
        if (thumb != null) thumb.IsVisible = needed;
        if (!needed || thumb == null) return;
        int h = GetThumbHeight(total, visible, trackHeight);
        int max = Math.Max(1, total - visible);
        int travel = Math.Max(0, trackHeight - h);
        int y = Mathf.RoundToInt(travel * (offset / (float)max));
        thumb.Size = new Vector2i(6, h);
        thumb.Position = new Vector2i(1, -y);
        if (thumb.UiTransform != null)
        {
            Vector3 pos = thumb.UiTransform.localPosition;
            pos.x = 1f;
            pos.y = -y;
            thumb.UiTransform.localPosition = pos;
        }
    }

    private static bool TryGetNativeScrollValue(XUiController controller,out float value)
    {
        return RebirthNativeScrollbarUtil.TryGetValue(controller,out value);
    }
    private static bool TrySetNativeScrollValue(XUiController controller,float value)
    {
        return RebirthNativeScrollbarUtil.TrySetValue(controller,value);
    }
    private static void RefreshNativeScrollView(XUiController controller)
    {
        RebirthNativeScrollbarUtil.Refresh(controller);
    }

    private XUiController Wire(string id, XUiEvent_OnPressEventHandler handler)
    {
        XUiController child = GetChildById(id);
        if (child != null) child.OnPress += handler;
        return child;
    }

    private XUiV_Label Label(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Label : null;
    }

    private XUiV_Sprite Sprite(string id)
    {
        XUiController child = GetChildById(id);
        return child != null ? child.ViewComponent as XUiV_Sprite : null;
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

    private static void SetLabel(XUiV_Label label, string text)
    {
        if (label != null) label.Text = text ?? string.Empty;
    }

    private static void SetButtonText(XUiController button, string text)
    {
        if (button == null) return;
        XUiV_Label label = button.ViewComponent as XUiV_Label;
        if (label != null) label.Text = text ?? string.Empty;
    }

    private static void SetVisible(XUiController controller, bool visible)
    {
        if (controller != null && controller.ViewComponent != null) controller.ViewComponent.IsVisible = visible;
    }

    private static void SetControllerSize(XUiController controller, int width, int height)
    {
        if (controller == null || controller.ViewComponent == null) return;
        controller.ViewComponent.Size = new Vector2i(width, height);
    }

    private static string CompatibilityShort(RebirthSurvivorProfileCompatibilityKind kind)
    {
        // Do not clutter healthy profile cards with implementation-facing compatibility state.
        // Only profiles that genuinely require player action receive a visible badge.
        if (kind == RebirthSurvivorProfileCompatibilityKind.Ready) return string.Empty;
        if (kind == RebirthSurvivorProfileCompatibilityKind.NeedsReview) return "[D6C978]REVIEW[-]";
        return "[CC6B64]REPAIR[-]";
    }

    private static string CompatibilityLong(RebirthSurvivorProfileCompatibility compatibility)
    {
        if (compatibility == null) return RebirthSurvivorUiText.L("xuiRebirthSurvivorValidationUnavailable", "Unavailable");
        switch (compatibility.Kind)
        {
            case RebirthSurvivorProfileCompatibilityKind.Ready: return "[8FD18F]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorCompatibilityReady", "Ready") + "[-]";
            case RebirthSurvivorProfileCompatibilityKind.NeedsReview: return "[D6C978]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorCompatibilityReview", "Definition update — review required") + "[-]";
            case RebirthSurvivorProfileCompatibilityKind.MissingDefinition: return "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorCompatibilityMissing", "Missing definition — repair required") + "[-]";
            case RebirthSurvivorProfileCompatibilityKind.UnsupportedSchema: return "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorCompatibilitySchema", "Unsupported profile schema") + "[-]";
            default: return "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorCompatibilityInvalid", "Invalid combination — repair required") + "[-]";
        }
    }

    /// <summary>
    /// Reclaims mouse/navigation ownership after the Survivor Creator closes back onto this
    /// manager.  The manager normally remains open underneath the Creator, so OnOpen is not
    /// invoked again on return and cannot be relied on to restore cursor state.
    /// </summary>
    public void ResumeInputAfterCreator()
    {
        if (xui == null || xui.playerUI == null) return;
        if (xui.playerUI.windowManager != null) xui.playerUI.windowManager.ResetActionSets();
        PrepareCursor();
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("SURVIVOR-MANAGER RESUME-AFTER-CREATOR cursorRestored=True"); }
    }

    private void PrepareCursor()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.CursorController == null) return;
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        cursor.Locked = false;
        cursor.SetCursorHidden(false);
        cursor.HoverTarget = null;
        cursor.SetNavigationLockView((XUiView)null);
        cursor.ResetNavigationTarget();
        XUiController target = profiles.Length > 0 ? rowButtons[0] : btnCreate;
        if (target != null && target.ViewComponent != null) cursor.SetNavigationTargetLater(target.ViewComponent);
    }
}
