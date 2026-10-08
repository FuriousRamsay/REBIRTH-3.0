using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class XUiC_RebirthChooseSurvivor : XUiController
{
    public const string WindowGroupId = "rebirthChooseSurvivor";
    private const int VisibleRows = 8;

    private readonly XUiController[] rowContainers = new XUiController[VisibleRows];
    private readonly XUiController[] rowButtons = new XUiController[VisibleRows];
    private readonly Dictionary<XUiController, int> rowIndex = new Dictionary<XUiController, int>();
    private readonly RebirthSurvivorArtTextureBinder[] artBinders = new RebirthSurvivorArtTextureBinder[VisibleRows];
    private readonly XUiController[] artControllers = new XUiController[VisibleRows];
    private readonly XUiController[] artPlaceholders = new XUiController[VisibleRows];

    private RebirthSurvivorProfile[] profiles = new RebirthSurvivorProfile[0];
    private string selectedProfileId = string.Empty;
    private int offset;
    private ulong pendingRequestId;
    private RebirthSurvivorProfile pendingSourceProfile;
    private RebirthSurvivorCreatorViewModel pendingNewModel;
    private bool waitingForOwnerReady;
    private string status = string.Empty;

    private XUiV_Label lblPage;
    private XUiV_Label lblSelectedTitle;
    private XUiV_Label lblSelectedDetails;
    private XUiV_Label lblStatus;
    private XUiController selectedArt;
    private XUiController selectedArtPlaceholder;
    private RebirthSurvivorArtTextureBinder selectedArtBinder;
    private XUiController btnUse;
    private XUiController btnReview;
    private XUiController btnCreate;
    private XUiController profileNativeScrollHost;
    private XUiController profileNativeScrollView;
    private XUiController profileNativeScrollProxy;
    private XUiController profileScrollCapture;
    private bool syncingProfileNativeScroll;
    private int profileNativeSyncFrames;
    private XUiController btnLeave;
    private bool wasCursorHidden;
    private bool embeddedPreSpawn;
    private bool interactiveSessionActive;
    private float nextEmbeddedGateRefreshTime;

    private static XUiC_RebirthChooseSurvivor embeddedPreSpawnInstance;
    private static float nextEmbeddedDeferredTraceTime;

    public static bool HasEmbeddedPreSpawnInstance
    {
        get { return embeddedPreSpawnInstance != null; }
    }

    public static bool IsEmbeddedPreSpawnVisible
    {
        get
        {
            return embeddedPreSpawnInstance != null &&
                embeddedPreSpawnInstance.ViewComponent != null &&
                embeddedPreSpawnInstance.ViewComponent.IsVisible;
        }
    }

    public static bool SetEmbeddedPreSpawnVisible(bool visible, string reason)
    {
        XUiC_RebirthChooseSurvivor instance = embeddedPreSpawnInstance;
        if (instance == null || !instance.embeddedPreSpawn)
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now >= nextEmbeddedDeferredTraceTime)
            {
                nextEmbeddedDeferredTraceTime = now + 2f;
                { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("embedded chooser visibility request deferred visible=" + visible +
                    " reason=" + (reason ?? "unspecified") + " instance=False"); }
            }
            return false;
        }
        instance.SetEmbeddedVisibilityInternal(visible, reason);
        return true;
    }

    private readonly List<KeyValuePair<XUiController, XUiEvent_OnPressEventHandler>> wiredButtons = new List<KeyValuePair<XUiController, XUiEvent_OnPressEventHandler>>();
    private readonly HashSet<XUiController> wiredScroll = new HashSet<XUiController>();
    private void UnwirePresentation()
    {
        foreach (var pair in wiredButtons) if (pair.Key != null) pair.Key.OnPress -= pair.Value;
        wiredButtons.Clear();
        foreach (XUiController controller in wiredScroll) if (controller != null) controller.OnScroll -= ProfileScroll_OnScroll;
        wiredScroll.Clear();
        foreach (XUiController button in rowButtons) if (button != null) button.OnPress -= Row_OnPressed;
        rowIndex.Clear();
        for (int i = 0; i < artBinders.Length; i++) if (artBinders[i] != null) artBinders[i].Clear();
        if (selectedArtBinder != null) selectedArtBinder.Clear();
    }
    private bool modelSubscribed;
    private void SubscribeModel()
    {
        if (modelSubscribed) return;
        RebirthSurvivorClientState.CreationResultReceived += OnCreationResult;
        RebirthSurvivorClientState.OwnerStateChanged += OnOwnerStateChanged;
        modelSubscribed = true;
    }
    private void UnsubscribeModel()
    {
        if (!modelSubscribed) return;
        RebirthSurvivorClientState.CreationResultReceived -= OnCreationResult;
        RebirthSurvivorClientState.OwnerStateChanged -= OnOwnerStateChanged;
        modelSubscribed = false;
    }
    public override void Cleanup()
    {
        UnsubscribeModel(); UnwirePresentation();
        if (embeddedPreSpawnInstance == this) embeddedPreSpawnInstance = null;
        for (int i = 0; i < artBinders.Length; i++) if (artBinders[i] != null) artBinders[i].Clear();
        if (selectedArtBinder != null) selectedArtBinder.Clear();
        base.Cleanup();
    }

    public override void Init()
    {
        UnwirePresentation();
        base.Init();
        for (int i = 0; i < VisibleRows; i++)
        {
            rowContainers[i] = GetChildById("chooseProfileRow" + i.ToString(CultureInfo.InvariantCulture));
            rowButtons[i] = GetChildById("btnChooseProfile" + i.ToString(CultureInfo.InvariantCulture));
            artControllers[i] = GetChildById("chooseProfileArt" + i.ToString(CultureInfo.InvariantCulture));
            artPlaceholders[i] = GetChildById("chooseProfileArtPlaceholder" + i.ToString(CultureInfo.InvariantCulture));
            artBinders[i] = new RebirthSurvivorArtTextureBinder(artControllers[i], 16f / 9f);
            if (rowButtons[i] != null)
            {
                rowIndex[rowButtons[i]] = i;
                rowButtons[i].OnPress += Row_OnPressed;
            }
        }
        btnUse = Wire("btnUseSelectedSurvivor", Use_OnPressed);
        btnReview = Wire("btnReviewSelectedSurvivor", Review_OnPressed);
        btnCreate = Wire("btnCreateNewSurvivor", Create_OnPressed);
        profileNativeScrollHost = GetChildById("chooseProfileNativeScrollHost");
        profileNativeScrollView = GetChildById("chooseProfileNativeScrollView");
        profileNativeScrollProxy = GetChildById("chooseProfileNativeScrollProxy");
        profileScrollCapture = GetChildById("chooseProfileScrollCapture");
        WireScroll(profileNativeScrollHost);
        WireScroll(profileNativeScrollView);
        WireScroll(profileNativeScrollProxy);
        WireScroll(profileScrollCapture);
        for (int i = 0; i < VisibleRows; i++) WireScroll(rowButtons[i]);
        btnLeave = Wire("btnLeaveSurvivorWorld", Leave_OnPressed);
        lblPage = Label("chooseProfilePageText");
        lblSelectedTitle = Label("chooseSelectedTitle");
        lblSelectedDetails = Label("chooseSelectedDetails");
        lblStatus = Label("chooseSurvivorStatus");
        selectedArt = GetChildById("chooseSelectedArt");
        selectedArtPlaceholder = GetChildById("chooseSelectedArtPlaceholder");
        selectedArtBinder = new RebirthSurvivorArtTextureBinder(selectedArt, 16f / 9f);
        SubscribeModel();
    }

    public override void OnOpen()
    {
        base.OnOpen();

        // The V3.2 first-entry gate is a RECT inside the native spawnselection window itself.
        // Identify it by its own view id rather than by whether EntityPlayerLocal exists: the
        // same native spawnselection window is also used for later respawns, where a player does
        // exist and this gate must simply remain hidden for an already-created Survivor.
        embeddedPreSpawn = ViewComponent != null &&
            string.Equals(ViewComponent.ID, "rebirthSurvivorSpawnGate", StringComparison.OrdinalIgnoreCase);
        if (embeddedPreSpawn)
        {
            embeddedPreSpawnInstance = this;
            RebirthSurvivorFirstEntryUiService.RegisterNativeSpawnSelectionHost(xui);
            bool shouldShow = RebirthSurvivorFirstEntryUiService.ShouldShowEmbeddedPreSpawnChooser();
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection Survivor gate OnOpen xui=" + (xui != null)
                + " playerUI=" + (xui != null && xui.playerUI != null)
                + " localPlayer=" + (LocalPlayer() != null)
                + " windowGroup=" + (windowGroup != null)
                + " shouldShow=" + shouldShow
                + " staged=" + RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection); }
            SetEmbeddedVisibilityInternal(shouldShow, "spawnselection-gate-OnOpen");
            return;
        }

        // Standalone in-game fallback only. This remains available if a post-spawn commit fails.
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("standalone chooser controller OnOpen xui=" + (xui != null)
            + " playerUI=" + (xui != null && xui.playerUI != null)
            + " localPlayer=" + (LocalPlayer() != null)
            + " windowGroup=" + (windowGroup != null)); }
        ActivateInteractiveSession();
        RebirthScrollbarPagingNativeAdapter.RegisterCustomAuthority(profileNativeScrollHost);
    }

    private void ActivateInteractiveSession()
    {
        if (interactiveSessionActive) return;
        interactiveSessionActive = true;
        string installReport = string.Empty;
        if (!RebirthSurvivorDefinitionRegistry.IsReady || !RebirthSurvivorInstaller.IsInstalled)
            installReport = RebirthSurvivorInstaller.Install();
        pendingRequestId = 0UL;
        pendingSourceProfile = null;
        waitingForOwnerReady = false;
        status = RebirthSurvivorInstaller.IsInstalled ? string.Empty
            : "[D6C978]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorRuntimeIncomplete", "Survivor runtime initialization is incomplete. Creation is disabled; see the output log for [REBIRTH Survivor][RuntimeInstall].") + "[-]";
        RebirthSurvivorProfileStore.Refresh();
        RefreshProfiles(true);
        if (xui != null && xui.playerUI != null && xui.playerUI.CursorController != null)
        {
            CursorControllerAbs cursor = xui.playerUI.CursorController;
            wasCursorHidden = cursor.GetCursorHidden();
            cursor.Locked = false;
            cursor.SetCursorHidden(false);
            cursor.ResetNavigationTarget();
            if (embeddedPreSpawn && ViewComponent != null)
                cursor.SetNavigationLockView(ViewComponent);
        }
        LockPlayer();
        Render();
        SelectInitial();
    }

    private void SetEmbeddedVisibilityInternal(bool visible, string reason)
    {
        if (!embeddedPreSpawn) return;
        if (ViewComponent != null) ViewComponent.IsVisible = visible;

        if (visible)
        {
            ActivateInteractiveSession();
            LockPlayer();
            Render();
            SelectInitial();
        }
        else if (interactiveSessionActive)
        {
            for (int i = 0; i < artBinders.Length; i++) if (artBinders[i] != null) artBinders[i].Clear();
            if (selectedArtBinder != null) selectedArtBinder.Clear();
            RestoreEmbeddedUiInputState();
            interactiveSessionActive = false;
        }

        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("embedded chooser visible=" + visible
            + " reason=" + (reason ?? "unspecified")
            + " viewVisible=" + (ViewComponent != null && ViewComponent.IsVisible)
            + " groupShowing=" + (windowGroup != null && windowGroup.isShowing)); }
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (RebirthScrollbarPagingPolicy.Enabled) { int before = offset; offset = ClampOffset(offset); if (before != offset) Render(); }
        if (embeddedPreSpawn)
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now >= nextEmbeddedGateRefreshTime)
            {
                nextEmbeddedGateRefreshTime = now + 0.25f;
                bool shouldShow = RebirthSurvivorFirstEntryUiService.ShouldShowEmbeddedPreSpawnChooser();
                bool currentlyVisible = ViewComponent != null && ViewComponent.IsVisible;
                if (shouldShow != currentlyVisible)
                    SetEmbeddedVisibilityInternal(shouldShow, "spawnselection-gate-refresh");
            }
            if (!interactiveSessionActive || ViewComponent == null || !ViewComponent.IsVisible)
                return;
        }
        for (int i = 0; i < artBinders.Length; i++) if (artBinders[i] != null) artBinders[i].Update();
        if (selectedArtBinder != null) selectedArtBinder.Update();
        LockPlayer();
        if (XUiUtils.HotkeysAllowedFor(viewComponent) && xui != null && xui.playerUI != null &&
            xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased)
            Leave_OnPressed(this, -1);
        PollProfileNativeScroll();
        handleDirtyUpdateDefault();
    }

    public override void OnClose()
    {
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("chooser controller OnClose embedded=" + embeddedPreSpawn
            + " staged=" + RebirthSurvivorFirstEntryUiService.HasStagedPreSpawnSelection
            + " localPlayer=" + (LocalPlayer() != null)); }
        for (int i = 0; i < artBinders.Length; i++) if (artBinders[i] != null) artBinders[i].Clear();
        if (selectedArtBinder != null) selectedArtBinder.Clear();
        if (interactiveSessionActive)
        {
            if (embeddedPreSpawn) RestoreEmbeddedUiInputState();
            else RestoreUiInputState();
        }
        interactiveSessionActive = false;
        if (object.ReferenceEquals(embeddedPreSpawnInstance, this)) embeddedPreSpawnInstance = null;
        base.OnClose();
        if (!embeddedPreSpawn) RestoreUiInputState();
        RebirthScrollbarPagingNativeAdapter.UnregisterCustomAuthority(profileNativeScrollHost);
    }

    /// <summary>
    /// The pre-spawn chooser is a CHILD WINDOW of the native spawnselection group, not its own
    /// action-set owner. Never ResetActionSets here: doing so would corrupt the native spawn group's
    /// input stack when the chooser is merely hidden to reveal the Spawn button underneath.
    /// </summary>
    private void RestoreEmbeddedUiInputState()
    {
        if (xui == null || xui.playerUI == null) return;
        UIInput selectedInput = UIInput.selection;
        if (selectedInput != null)
        {
            selectedInput.RemoveFocus();
            selectedInput.isSelected = false;
        }
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        if (cursor != null)
        {
            cursor.HoverTarget = null;
            cursor.SetNavigationTarget((XUiView)null);
            cursor.SetNavigationLockView((XUiView)null);
            cursor.SetCursorHidden(wasCursorHidden);
            cursor.Locked = false;
            cursor.ResetNavigationTarget();
        }
    }

    private void RestoreUiInputState()
    {
        if (xui == null || xui.playerUI == null) return;
        UIInput selectedInput = UIInput.selection;
        if (selectedInput != null)
        {
            selectedInput.RemoveFocus();
            selectedInput.isSelected = false;
        }
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        if (cursor != null)
        {
            cursor.HoverTarget = null;
            cursor.SetNavigationTarget((XUiView)null);
            cursor.SetNavigationLockView((XUiView)null);
            cursor.SetCursorHidden(wasCursorHidden);
            cursor.Locked = false;
            cursor.ResetNavigationTarget();
        }
        if (xui.playerUI.windowManager != null) xui.playerUI.windowManager.ResetActionSets();
    }

    private void RefreshProfiles(bool selectFirst)
    {
        profiles = RebirthSurvivorProfileStore.GetProfilesSnapshot();
        if (!string.IsNullOrEmpty(selectedProfileId) && IndexOf(selectedProfileId) < 0) selectedProfileId = string.Empty;
        if (selectFirst && string.IsNullOrEmpty(selectedProfileId) && profiles.Length > 0) selectedProfileId = profiles[0].ProfileId;
        offset = ClampOffset(offset);
        EnsureSelectedVisible();
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
                if (artBinders[i] != null) artBinders[i].Clear();
                SetVisible(artControllers[i], false);
                SetVisible(artPlaceholders[i], false);
                continue;
            }
            RebirthSurvivorProfile profile = profiles[index];
            RebirthSurvivorProfileCompatibility compatibility = RebirthSurvivorProfileStore.EvaluateCompatibility(profile);
            string marker = string.Equals(profile.ProfileId, selectedProfileId, StringComparison.OrdinalIgnoreCase) ? "[8FD18F]▶[-] " : "   ";
            SetButtonText(rowButtons[i], marker + profile.ProfileName + "   [B0B0B0]" + RebirthSurvivorUiText.ResolveDefinitionName(profile.BackgroundId) +
                " • " + RebirthSurvivorUiText.ResolveDefinitionName(profile.DietId) + "[-]\n   " + CompactTraits(profile) + "   " + CompatibilityShort(compatibility.Kind));
            RebirthBackgroundDefinition bg;
            bool hasArt = RebirthSurvivorDefinitionRegistry.TryGetBackground(profile.BackgroundId, out bg) && artBinders[i] != null && artBinders[i].BindBackground(bg);
            SetVisible(artControllers[i], hasArt);
            SetVisible(artPlaceholders[i], !hasArt);
        }

        int first = profiles.Length == 0 ? 0 : offset + 1;
        int last = Math.Min(profiles.Length, offset + VisibleRows);
        SetLabel(lblPage, profiles.Length == 0 ? RebirthSurvivorUiText.L("xuiRebirthSurvivorNoProfiles", "No reusable Survivor Profiles") :
            string.Format(CultureInfo.InvariantCulture, RebirthSurvivorUiText.L("xuiRebirthSurvivorProfilePageFormat", "Profiles {0}-{1} of {2}"), first, last, profiles.Length));

        RebirthSurvivorProfile selected = GetSelected();
        if (selected == null)
        {
            if (selectedArtBinder != null) selectedArtBinder.Clear();
            SetVisible(selectedArt, false);
            SetVisible(selectedArtPlaceholder, true);
            SetLabel(lblSelectedTitle, RebirthSurvivorUiText.L("xuiRebirthChooseNoSelection", "CREATE OR CHOOSE A SURVIVOR"));
            SetLabel(lblSelectedDetails, RebirthSurvivorUiText.L("xuiRebirthChooseNoProfilesHelp", "Use an existing reusable Survivor Profile, or create a new Survivor for this world."));
        }
        else
        {
            RebirthBackgroundDefinition selectedBackground;
            bool selectedHasArt = RebirthSurvivorDefinitionRegistry.TryGetBackground(selected.BackgroundId, out selectedBackground) && selectedArtBinder != null && selectedArtBinder.BindBackground(selectedBackground);
            SetVisible(selectedArt, selectedHasArt);
            SetVisible(selectedArtPlaceholder, !selectedHasArt);
            SetLabel(lblSelectedTitle, selected.ProfileName);
            SetLabel(lblSelectedDetails, BuildDetails(selected));
        }

        RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        bool recoveryBlocked = owner != null && owner.CreationState == RebirthSurvivorOwnerCreationState.RecoveryRequired;
        bool ready = selected != null && RebirthSurvivorProfileStore.EvaluateCompatibility(selected).Kind == RebirthSurvivorProfileCompatibilityKind.Ready;
        SetVisible(btnUse, !recoveryBlocked && ready && pendingRequestId == 0UL && !waitingForOwnerReady);
        SetVisible(btnReview, !embeddedPreSpawn && selected != null && pendingRequestId == 0UL && !waitingForOwnerReady);
        SetVisible(btnCreate, !recoveryBlocked && pendingRequestId == 0UL && !waitingForOwnerReady);
        if (recoveryBlocked && string.IsNullOrEmpty(status))
            status = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthChooseRecoveryRequired", "This world has an unreadable Survivor character record. Creation is blocked to prevent overwriting it. Restore/repair that record or leave the world.") + "[-]";
        UpdateProfileNativeScroll();
        SetLabel(lblStatus, status);
    }

    private void Row_OnPressed(XUiController sender, int mouseButton)
    {
        int local;
        if (!rowIndex.TryGetValue(sender, out local)) return;
        int index = offset + local;
        if (index < 0 || index >= profiles.Length) return;
        selectedProfileId = profiles[index].ProfileId;
        status = string.Empty;
        Render();
    }

    private void Use_OnPressed(XUiController sender, int mouseButton)
    {
        if (pendingRequestId != 0UL || waitingForOwnerReady) return;
        RebirthSurvivorProfile profile = GetSelected();
        if (profile == null) return;
        RebirthSurvivorProfileCompatibility compatibility = RebirthSurvivorProfileStore.EvaluateCompatibility(profile);
        if (compatibility.Kind != RebirthSurvivorProfileCompatibilityKind.Ready)
        {
            status = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthChooseProfileNeedsRepair", "This profile must be reviewed/repaired before it can create a character.") + "[-]";
            Render();
            return;
        }
        EntityPlayerLocal player = LocalPlayer();
        if (player == null)
        {
            // XUi_Menu pre-spawn path: the native player entity does not exist yet. Stage the
            // explicit profile choice and let the first-entry coordinator resume native spawn;
            // the authoritative commit is dispatched immediately after PlayerSpawnedInWorld.
            string stageError;
            if (!RebirthSurvivorFirstEntryUiService.StageExistingProfile(profile, out stageError))
            {
                status = "[CC6B64]" + stageError + "[-]";
                Render();
            }
            else
            {
                status = "[8FD18F]" + RebirthSurvivorUiText.L("xuiRebirthChooseStaged", "Survivor selected. Spawn options unlocked.") + "[-]";
                { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("chooser staged reusable profile id=" + profile.ProfileId + " name='" + profile.ProfileName + "'"); }
                Render();
            }
            return;
        }
        RebirthSurvivorCreationSelection selection = new RebirthSurvivorCreationSelection(profile.BackgroundId, profile.DietId, profile.TraitIds, RebirthSurvivorDefinitionRegistry.SemanticHash);
        pendingSourceProfile = profile;
        pendingNewModel = null;
        pendingRequestId = RebirthSurvivorNetworkService.RequestCommit(player, selection, profile);
        if (pendingRequestId == 0UL)
        {
            pendingSourceProfile = null;
            status = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthChooseSendFailed", "Could not send the Survivor creation request.") + "[-]";
        }
        else status = "[D6C978]" + RebirthSurvivorUiText.L("xuiRebirthChooseConfirming", "Confirming Survivor with the server…") + "[-]";
        Render();
    }

    private void Review_OnPressed(XUiController sender, int mouseButton)
    {
        RebirthSurvivorProfile profile = GetSelected();
        if (profile == null || pendingRequestId != 0UL) return;
        bool hideEmbedded = embeddedPreSpawn;
        if (hideEmbedded) SetEmbeddedVisibilityInternal(false, "open-profile-review");
        string error;
        if (!XUiC_RebirthSurvivorCreator.OpenReview(xui, profile, out error))
        {
            status = "[CC6B64]" + error + "[-]";
            if (hideEmbedded) SetEmbeddedVisibilityInternal(true, "profile-review-open-failed");
            Render();
        }
    }

    private void Create_OnPressed(XUiController sender, int mouseButton)
    {
        if (pendingRequestId != 0UL || waitingForOwnerReady) return;
        RebirthSurvivorCreatorViewModel vm = new RebirthSurvivorCreatorViewModel();
        vm.BeginFirstWorldCreate();
        string activePlayerProfile;
        string activePlayerProfileSource;
        EntityPlayerLocal activePlayer = LocalPlayer();
        if (RebirthNativePlayerProfileBridge.TryResolveActivePlayerProfileName(xui, activePlayer, out activePlayerProfile, out activePlayerProfileSource))
        {
            vm.SetPlayerProfileName(activePlayerProfile);
            { if (RebirthLogSettings.PlayerProfileBridgeLoggingEnabled) RebirthLogSettings.TracePlayerProfileBridge("FIRST-ENTRY create associated active Player Profile='" + activePlayerProfile + "' source=" + activePlayerProfileSource); }
        }
        else
        {
            Log.Warning("[REBIRTH Survivor][PlayerProfileBridge] FIRST-ENTRY create could not resolve active Player Profile before opening creator.");
        }
        bool hideEmbedded = embeddedPreSpawn;
        if (hideEmbedded) SetEmbeddedVisibilityInternal(false, "open-first-world-creator");
        string error;
        if (!XUiC_RebirthSurvivorCreator.OpenExternal(xui, vm, OnNewCreatorConfirmed, RebirthSurvivorFirstEntryUiService.ReturnToSelector, out error))
        {
            status = "[CC6B64]" + error + "[-]";
            if (hideEmbedded) SetEmbeddedVisibilityInternal(true, "first-world-creator-open-failed");
            Render();
        }
    }

    private void OnNewCreatorConfirmed(RebirthSurvivorCreatorViewModel vm)
    {
        if (vm == null || vm.Validation == null || !vm.Validation.IsValid || vm.Validation.RemainingCreationPoints != 0)
        {
            status = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthSurvivorValidationUnavailable", "Survivor selection is invalid.") + "[-]";
            RebirthSurvivorFirstEntryUiService.ReturnToSelector();
            return;
        }
        EntityPlayerLocal player = LocalPlayer();
        if (player == null)
        {
            // Menu pre-spawn path. Keep the fully validated creator model in the coordinator so
            // Save-As-Reusable semantics still occur only after the world character commit succeeds.
            // The creator has already closed itself before invoking this callback; StageNewSurvivor
            // therefore restores the native spawnselection window directly.
            string stageError;
            if (!RebirthSurvivorFirstEntryUiService.StageNewSurvivor(vm, out stageError))
            {
                Log.Error("[REBIRTH Survivor][PreSpawnTrace] newly-created Survivor could not be staged: " + stageError);
                RebirthSurvivorFirstEntryUiService.ReturnToSelector();
                return;
            }
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("chooser staged newly-created Survivor profileName='" + (vm.ProfileName ?? string.Empty) + "'; spawn options unlocked"); }
            return;
        }
        pendingSourceProfile = null;
        pendingNewModel = vm;
        pendingRequestId = RebirthSurvivorNetworkService.RequestCommit(player, vm.BuildSelection(), null, vm.GetCreationChoicesSnapshot());
        if (pendingRequestId == 0UL)
        {
            status = "[CC6B64]" + RebirthSurvivorUiText.L("xuiRebirthChooseSendFailed", "Could not send the Survivor creation request.") + "[-]";
            RebirthSurvivorFirstEntryUiService.ReturnToSelector();
        }
        else
        {
            status = "[D6C978]" + RebirthSurvivorUiText.L("xuiRebirthChooseConfirming", "Confirming Survivor with the server…") + "[-]";
            RebirthSurvivorFirstEntryUiService.ReturnToSelector();
        }
        Render();
    }

    private void OnCreationResult(RebirthSurvivorCreationNetworkResponse response)
    {
        if (response == null || pendingRequestId == 0UL || response.RequestId != pendingRequestId) return;
        pendingRequestId = 0UL;
        if (response.Status == RebirthSurvivorCreationNetworkStatus.Created || response.Status == RebirthSurvivorCreationNetworkStatus.AlreadyCreated)
        {
            if (response.Status == RebirthSurvivorCreationNetworkStatus.Created) SaveNewReusableProfileIfRequested();
            waitingForOwnerReady = true;
            status = "[8FD18F]" + RebirthSurvivorUiText.L("xuiRebirthChooseCreated", "Survivor created. Preparing the world…") + "[-]";
            Render();
            return;
        }
        waitingForOwnerReady = false;
        status = "[CC6B64]" + DescribeResponse(response) + "[-]";
        Render();
    }

    private void OnOwnerStateChanged(RebirthSurvivorOwnerStateSnapshot snapshot)
    {
        if (snapshot == null) return;
        if (snapshot.HasCharacter && snapshot.CreationState == RebirthSurvivorOwnerCreationState.Ready)
        {
            waitingForOwnerReady = false;
            pendingRequestId = 0UL;
            pendingSourceProfile = null;
            pendingNewModel = null;
        }
    }

    private void SaveNewReusableProfileIfRequested()
    {
        RebirthSurvivorCreatorViewModel vm = pendingNewModel;
        if (vm == null || !vm.SaveAsReusableProfile || !vm.HasValidProfileName) return;
        RebirthSurvivorProfile created;
        string error;
        if (!RebirthSurvivorProfileStore.TryCreate(vm.ProfileName.Trim(), vm.BuildSelection(), vm.GetCreationChoicesSnapshot(), out created, out error))
            Log.Warning("[REBIRTH Survivor UI] world character created but reusable profile save failed: " + error);
        else
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor UI] reusable profile saved after world creation id=" + created.ProfileId); }
    }

    private void Leave_OnPressed(XUiController sender, int mouseButton) { RebirthSurvivorFirstEntryUiService.LeaveWorld(); }

    private string BuildDetails(RebirthSurvivorProfile profile)
    {
        StringBuilder b = new StringBuilder(1000);
        RebirthSurvivorProfileCompatibility c = RebirthSurvivorProfileStore.EvaluateCompatibility(profile);
        string playerProfileName;
        if (!profile.CreationChoices.TryGetValue(RebirthSurvivorCreationChoiceKeys.PlayerProfileName, out playerProfileName)) playerProfileName = string.Empty;
        b.Append("[FFFFFF]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorPlayerProfileTitle", "Player Profile")).Append("[-]: ")
            .Append(string.IsNullOrEmpty(playerProfileName) ? RebirthSurvivorUiText.L("xuiRebirthSurvivorNotSelected", "Not selected") : playerProfileName).Append("\n");
        b.Append("[FFFFFF]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorBackground", "Experience")).Append("[-]: ").Append(RebirthSurvivorUiText.ResolveDefinitionName(profile.BackgroundId)).Append("\n");
        b.Append("[FFFFFF]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorDiet", "Diet")).Append("[-]: ").Append(RebirthSurvivorUiText.ResolveDefinitionName(profile.DietId)).Append("\n\n");
        b.Append(RebirthSurvivorUiText.BuildSignatureBonusSummary(profile.BackgroundId, true)).Append("\n\n");
        b.Append("[FFFFFF]").Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorTraits", "Traits")).Append("[-]\n");
        for (int i = 0; i < profile.TraitIds.Count; i++) b.Append("• ").Append(RebirthSurvivorUiText.ResolveDefinitionName(profile.TraitIds[i])).Append("\n");
        if (profile.TraitIds.Count == 0) b.Append(RebirthSurvivorUiText.L("xuiRebirthSurvivorNoTraits", "None")).Append("\n");
        b.Append("\n").Append(CompatibilityLong(c));
        if (c.Kind != RebirthSurvivorProfileCompatibilityKind.Ready)
            b.Append("\n[D6C978]").Append(RebirthSurvivorUiText.L("xuiRebirthChooseRepairInManager", "Repair this reusable profile from SURVIVOR PROFILES on the main menu before using it.")).Append("[-]");
        return b.ToString();
    }

    private static string DescribeResponse(RebirthSurvivorCreationNetworkResponse r)
    {
        if (r == null) return RebirthSurvivorUiText.L("xuiRebirthChooseUnknownFailure", "Survivor creation failed.");
        if (r.ValidationResult != null && !r.ValidationResult.IsValid) return RebirthSurvivorUiText.BuildValidationSummary(r.ValidationResult);
        switch (r.Status)
        {
            case RebirthSurvivorCreationNetworkStatus.DefinitionMismatch: return RebirthSurvivorUiText.L("xuiRebirthChooseDefinitionMismatch", "Your Survivor definitions do not match the server. Reconnect after updating REBIRTH.");
            case RebirthSurvivorCreationNetworkStatus.ProtocolMismatch: return RebirthSurvivorUiText.L("xuiRebirthChooseProtocolMismatch", "Your Survivor network protocol does not match the server.");
            case RebirthSurvivorCreationNetworkStatus.Busy: return RebirthSurvivorUiText.L("xuiRebirthChooseBusy", "A Survivor creation request is already being processed. Try again.");
            case RebirthSurvivorCreationNetworkStatus.RebirthModeDisabled: return RebirthSurvivorUiText.L("xuiRebirthChooseModeDisabled", "This world is not using Rebirth character progression.");
            default: return string.IsNullOrEmpty(r.MessageCode) ? r.Status.ToString() : r.MessageCode;
        }
    }

    private RebirthSurvivorProfile GetSelected()
    {
        int index = IndexOf(selectedProfileId);
        return index >= 0 ? profiles[index] : null;
    }
    private int IndexOf(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < profiles.Length; i++) if (string.Equals(profiles[i].ProfileId, id, StringComparison.OrdinalIgnoreCase)) return i;
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
        return (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Math.Max(0, Math.Min(max, value)), max, VisibleRows, RebirthScrollbarPagingPolicy.Enabled);
    }
    private string CompactTraits(RebirthSurvivorProfile p)
    {
        if (p == null || p.TraitIds.Count == 0) return RebirthSurvivorUiText.L("xuiRebirthSurvivorNoTraits", "No Traits");
        StringBuilder b = new StringBuilder();
        int shown = Math.Min(2, p.TraitIds.Count);
        for (int i = 0; i < shown; i++) { if (i > 0) b.Append(", "); b.Append(RebirthSurvivorUiText.ResolveDefinitionName(p.TraitIds[i])); }
        if (p.TraitIds.Count > shown) b.Append(" +").Append(p.TraitIds.Count - shown);
        return b.ToString();
    }

    private void WireScroll(XUiController controller)
    {
        if (controller != null && wiredScroll.Add(controller)) controller.OnScroll += ProfileScroll_OnScroll;
    }

    private void ProfileListScroll(float delta)
    {
        int old = offset;
        if (RebirthScrollbarPagingPolicy.Enabled) offset = (int)RebirthScrollbarPagingPolicy.Step(offset, Math.Max(0, profiles.Length - VisibleRows), VisibleRows, delta > 0f ? -1 : delta < 0f ? 1 : 0);
        else if (delta > 0f) offset--;
        else if (delta < 0f) offset++;
        offset = ClampOffset(offset);
        if (offset != old) Render();
    }

    private void UpdateProfileNativeScroll()
    {
        int maxOffset = Math.Max(0, profiles.Length - VisibleRows);
        bool needed = maxOffset > 0;
        if (profileNativeScrollHost != null && profileNativeScrollHost.ViewComponent != null)
            profileNativeScrollHost.ViewComponent.IsVisible = needed;
        if (profileScrollCapture != null && profileScrollCapture.ViewComponent != null)
            profileScrollCapture.ViewComponent.IsVisible = profiles.Length > 0;
        if (profileNativeScrollProxy == null || profileNativeScrollProxy.ViewComponent == null) return;
        int trackHeight = 646;
        int contentHeight = needed ? Math.Max(trackHeight + 1, Mathf.CeilToInt(trackHeight * (profiles.Length / (float)VisibleRows))) : trackHeight;
        profileNativeScrollProxy.ViewComponent.Size = new Vector2i(1, contentHeight);
        float normalized = maxOffset > 0 ? Mathf.Clamp01(offset / (float)maxOffset) : 0f;
        syncingProfileNativeScroll = true;
        RebirthNativeScrollbarUtil.Refresh(profileNativeScrollView);
        RebirthNativeScrollbarUtil.TrySetValue(profileNativeScrollView, normalized);
        RebirthNativeScrollbarUtil.Refresh(profileNativeScrollView);
        syncingProfileNativeScroll = false;
        profileNativeSyncFrames = Math.Max(profileNativeSyncFrames, 2);
    }

    private void PollProfileNativeScroll()
    {
        if (profileNativeSyncFrames > 0)
        {
            profileNativeSyncFrames--;
            RebirthNativeScrollbarUtil.Refresh(profileNativeScrollView);
            return;
        }
        if (syncingProfileNativeScroll) return;
        int maxOffset = Math.Max(0, profiles.Length - VisibleRows);
        if (maxOffset <= 0) return;
        float normalized;
        if (!RebirthNativeScrollbarUtil.TryGetValue(profileNativeScrollView, out normalized)) return;
        int requested = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(Mathf.RoundToInt(normalized * maxOffset), 0, maxOffset), maxOffset, VisibleRows, RebirthScrollbarPagingPolicy.Enabled);
        if (requested == offset) return;
        offset = requested;
        Render();
    }

    private void LockPlayer() { EntityPlayerLocal p = LocalPlayer(); if (p != null) { p.ClearMovementInputs(); p.SetControllable(false); } }
    private EntityPlayerLocal LocalPlayer() { return xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null; }
    private void SelectInitial()
    {
        if (xui == null || xui.playerUI == null || xui.playerUI.CursorController == null) return;
        XUiController target = profiles.Length > 0 ? rowButtons[0] : btnCreate;
        if (target != null && target.ViewComponent != null) xui.playerUI.CursorController.SetNavigationTargetLater(target.ViewComponent);
    }
    private void ProfileScroll_OnScroll(XUiController sender, float delta) { ProfileListScroll(delta); }
    private XUiController Wire(string id, XUiEvent_OnPressEventHandler handler)
    {
        XUiController c = GetChildById(id);
        if (c != null) { c.OnPress += handler; wiredButtons.Add(new KeyValuePair<XUiController, XUiEvent_OnPressEventHandler>(c, handler)); }
        return c;
    }
    private XUiV_Label Label(string id) { XUiController c = GetChildById(id); return c != null ? c.ViewComponent as XUiV_Label : null; }
    private static void SetVisible(XUiController c, bool visible) { if (c != null && c.ViewComponent != null) c.ViewComponent.IsVisible = visible; }
    private static void SetLabel(XUiV_Label l, string text) { if (l != null) l.Text = text ?? string.Empty; }
    private static void SetButtonText(XUiController c, string text) { if (c == null) return; XUiV_Label l = c.ViewComponent as XUiV_Label; if (l != null) l.Text = text ?? string.Empty; }
    private static string CompatibilityShort(RebirthSurvivorProfileCompatibilityKind kind)
    {
        switch (kind) { case RebirthSurvivorProfileCompatibilityKind.Ready: return "[8FD18F]READY[-]"; case RebirthSurvivorProfileCompatibilityKind.NeedsReview: return "[D6C978]REVIEW[-]"; default: return "[CC6B64]REPAIR[-]"; }
    }
    private static string CompatibilityLong(RebirthSurvivorProfileCompatibility c)
    {
        if (c == null) return "[CC6B64]Unavailable[-]";
        switch (c.Kind) { case RebirthSurvivorProfileCompatibilityKind.Ready: return "[8FD18F]READY FOR THIS WORLD[-]"; case RebirthSurvivorProfileCompatibilityKind.NeedsReview: return "[D6C978]DEFINITION UPDATE — REVIEW REQUIRED[-]"; default: return "[CC6B64]PROFILE REPAIR REQUIRED[-]"; }
    }
}
