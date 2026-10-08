using System;
using System.Collections;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Client coordinator for first-world Survivor selection.
///
/// 3.2 shows the native "spawnselection" window from XUi_Menu BEFORE EntityPlayerLocal exists.
/// The original implementation incorrectly waited for PlayerSpawnedInWorld and therefore could
/// never put the Survivor chooser in front of that native spawn screen. This coordinator now has
/// an explicit pre-spawn phase: owner state is resolved before spawnselection is actionable,
/// selection/creation is staged in menu XUi, and the staged Survivor is committed immediately
/// after the native player entity is created. The authoritative server hold remains the final
/// safety boundary until that commit succeeds.
/// </summary>
public static class RebirthSurvivorFirstEntryUiService
{
    private static bool installed;
    private static bool creationUiRequired;
    private static bool selectorOpenRequested;
    private static bool leaveWorldRequested;
    private static bool leaveWorldCoroutineStarted;
    private static bool resumeNativeSpawnRequested;
    private static float nextEnforcementTime;
    private static float nextUnknownOwnerTraceTime;
    private static bool spawnSelectionWasOpen;
    private static GUIWindowManager pendingSelectorManager;
    private static GUIWindowManager nativeSpawnManager;

    private static bool stagedSelectionReady;
    private static RebirthSurvivorCreationSelection stagedSelection;
    private static RebirthSurvivorProfile stagedSourceProfile;
    private static Dictionary<string, string> stagedCreationChoices;
    private static RebirthSurvivorCreatorViewModel stagedNewModel;
    private static ulong stagedRequestId;
    private static bool stagedWaitingForOwnerReady;

    public static bool CreationUiRequired { get { return creationUiRequired; } }
    public static bool HasStagedPreSpawnSelection { get { return stagedSelectionReady; } }
    public static string StagedPreSpawnProfileName
    {
        get
        {
            if (!stagedSelectionReady) return string.Empty;
            if (stagedSourceProfile != null && !string.IsNullOrEmpty(stagedSourceProfile.ProfileName))
                return stagedSourceProfile.ProfileName;
            if (stagedNewModel != null && !string.IsNullOrEmpty(stagedNewModel.ProfileName))
                return stagedNewModel.ProfileName;
            return string.Empty;
        }
    }

    public static string StagedPreSpawnProfileId
    {
        get
        {
            if (!stagedSelectionReady || stagedSourceProfile == null) return string.Empty;
            return stagedSourceProfile.ProfileId ?? string.Empty;
        }
    }

    /// <summary>
    /// Returns whether mandatory first-entry Rebirth profile selection is still required.
    /// Selection is now a dedicated full-screen step before the stock V3.2 spawn screen; once
    /// a profile is staged, the native spawn screen resumes without any embedded profile UI.
    /// </summary>
    public static bool ShouldShowSpawnProfileSelector()
    {
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth) return false;
        // First-entry Survivor selection is a dedicated full-screen step before native spawn.
        // Once a profile is staged, return to the stock spawnselection window.
        if (stagedSelectionReady) return false;

        RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if (owner == null)
        {
            RebirthSurvivorNetworkService.TryPublishLocalPreSpawnOwnerState("spawn-profile-selector-visibility");
            owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        }
        if (owner == null) return creationUiRequired;
        if (!owner.RebirthModeEnabled || owner.HasCharacter || owner.CreationState == RebirthSurvivorOwnerCreationState.Ready)
            return false;
        return owner.CreationState == RebirthSurvivorOwnerCreationState.CreationRequired ||
            owner.CreationState == RebirthSurvivorOwnerCreationState.RecoveryRequired;
    }

    /// <summary>
    /// Capture the native XUi_Menu spawnselection manager from the lightweight routing bridge.
    /// The bridge immediately redirects mandatory first-entry selection to the full-screen
    /// Survivor Profiles group and retains this manager only to resume stock spawn afterwards.
    /// </summary>
    public static void RegisterNativeSpawnSelectionHost(XUi xui)
    {
        GUIWindowManager manager = xui != null && xui.playerUI != null ? xui.playerUI.windowManager : null;
        if (manager != null)
        {
            nativeSpawnManager = manager;
            pendingSelectorManager = manager;
            spawnSelectionWasOpen = true;
        }

        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth)
        {
            creationUiRequired = false;
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection host registered mode=Base gateRequired=False manager=" + (manager != null)); }
            return;
        }

        if (stagedSelectionReady)
        {
            creationUiRequired = false;
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection host registered staged=True gateRequired=False manager=" + (manager != null)); }
            return;
        }

        // Fail closed while resolving first-entry state: native spawn controls stay covered until
        // the local authoritative snapshot says this player already owns a Survivor.
        creationUiRequired = true;
        RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if (owner == null)
        {
            RebirthSurvivorNetworkService.TryPublishLocalPreSpawnOwnerState("spawnselection-integrated-gate");
            owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        }
        if (owner != null)
        {
            creationUiRequired = owner.RebirthModeEnabled && !owner.HasCharacter &&
                (owner.CreationState == RebirthSurvivorOwnerCreationState.CreationRequired ||
                 owner.CreationState == RebirthSurvivorOwnerCreationState.RecoveryRequired);
        }

        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("spawnselection host registered manager=" + (manager != null)
            + " owner=" + (owner != null ? owner.CreationState.ToString() : "unknown")
            + " hasCharacter=" + (owner != null && owner.HasCharacter)
            + " gateRequired=" + creationUiRequired
            + " staged=" + stagedSelectionReady); }
    }

    /// <summary>
    /// Legacy compatibility query used by the spawnselection routing bridge. No embedded chooser
    /// is rendered in V3.2; true means the bridge must redirect to full-screen Survivor Profiles.
    /// </summary>
    public static bool ShouldShowEmbeddedPreSpawnChooser()
    {
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth || stagedSelectionReady)
            return false;

        RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if (owner == null)
        {
            RebirthSurvivorNetworkService.TryPublishLocalPreSpawnOwnerState("embedded-chooser-visibility");
            owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        }
        if (owner == null)
            return creationUiRequired;
        if (!owner.RebirthModeEnabled || owner.HasCharacter || owner.CreationState == RebirthSurvivorOwnerCreationState.Ready)
            return false;
        return owner.CreationState == RebirthSurvivorOwnerCreationState.CreationRequired ||
            owner.CreationState == RebirthSurvivorOwnerCreationState.RecoveryRequired;
    }

    public static string BuildDebugSummary()
    {
        GameManager gm = GameManager.Instance;
        World world = gm != null ? gm.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        XUi activeXui = GetActiveXUi();
        return "creationUiRequired=" + creationUiRequired
            + " selectorOpenRequested=" + selectorOpenRequested
            + " leaveWorldRequested=" + leaveWorldRequested
            + " resumeNativeSpawnRequested=" + resumeNativeSpawnRequested
            + " staged=" + stagedSelectionReady
            + " stagedRequest=" + stagedRequestId
            + " stagedWaitingOwner=" + stagedWaitingForOwnerReady
            + " owner=" + (owner != null ? (owner.CreationState + "/has=" + owner.HasCharacter + "/rebirth=" + owner.RebirthModeEnabled) : "none")
            + " world=" + (world != null)
            + " player=" + (player != null ? player.entityId.ToString() : "none")
            + " xui=" + (activeXui != null)
            + " spawnSelection=" + IsNativeSpawnSelectionOpen()
            + " spawnGate=" + XUiC_RebirthSpawnSelectionSurvivorGate.IsGateVisible
            + " mode=" + RebirthSurvivorMode.ConfiguredMode;
    }

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor] first-entry UI already installed.";
        installed = true;
        RebirthSurvivorClientState.OwnerStateChanged += OnOwnerStateChanged;
        RebirthSurvivorClientState.CreationResultReceived += OnCreationResult;
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawned));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Survivor] first-entry Choose Your Survivor coordinator installed (full-screen profile selection before native spawn).";
    }


    /// <summary>
    /// Diagnostic snapshot for the native LocalPlayerUI -> EntityPlayerLocal handoff.
    /// Logging only; never mutates queue/UI/player state.
    /// </summary>
    public static void TraceNativePlayerUiHandoff(string stage, EntityPlayerLocal player, LocalPlayerUI selectedUi)
    {
        if(!RebirthLogSettings.PreSpawnLoggingEnabled)return;
        try
        {
            int queueCount = -1;
            string queueEntries = "";
            try
            {
                System.Reflection.FieldInfo queueField = typeof(LocalPlayerUI).GetField(
                    "playerUIQueueForPendingEntity",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                object queue = queueField != null ? queueField.GetValue(null) : null;
                if (queue != null)
                {
                    System.Reflection.PropertyInfo cp = queue.GetType().GetProperty("Count");
                    if (cp != null) queueCount = Convert.ToInt32(cp.GetValue(queue, null));
                    System.Collections.IEnumerable enumerable = queue as System.Collections.IEnumerable;
                    if (enumerable != null)
                    {
                        int qi = 0;
                        foreach (object entry in enumerable)
                        {
                            LocalPlayerUI ui = entry as LocalPlayerUI;
                            if (qi++ > 0) queueEntries += ";";
                            queueEntries += DescribeLocalPlayerUi(ui);
                        }
                    }
                }
            }
            catch (Exception qex)
            {
                queueEntries = "queue-read-failed:" + qex.GetType().Name;
            }

            string all = "";
            int playerUiCount = LocalPlayerUI.PlayerUIs != null ? LocalPlayerUI.PlayerUIs.Count : -1;
            if (LocalPlayerUI.PlayerUIs != null)
            {
                for (int i = 0; i < LocalPlayerUI.PlayerUIs.Count; i++)
                {
                    if (i > 0) all += ";";
                    all += "#" + i + "=" + DescribeLocalPlayerUi(LocalPlayerUI.PlayerUIs[i]);
                }
            }

            int screenEffects = -1;
            try
            {
                screenEffects = UnityEngine.Object.FindObjectsOfType<ScreenEffects>().Length;
            }
            catch { }

            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn(
                "UI-HANDOFF stage=" + stage +
                " entity=" + (player != null ? player.entityId.ToString() : "null") +
                " selected=" + DescribeLocalPlayerUi(selectedUi) +
                " primary=" + DescribeLocalPlayerUi(LocalPlayerUI.primaryUI) +
                " playerUIs=" + playerUiCount + "[" + all + "]" +
                " pendingQueue=" + queueCount + "[" + queueEntries + "]" +
                " screenEffects=" + screenEffects); }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] UI-HANDOFF logging failed stage=" + stage + ": " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static string DescribeLocalPlayerUi(LocalPlayerUI ui)
    {
        if (ui == null) return "null";
        try
        {
            UnityEngine.GameObject go = ui.gameObject;
            int cameras = 0;
            try { cameras = go != null ? go.GetComponentsInChildren<UnityEngine.Camera>(true).Length : 0; } catch { }
            return "id=" + ui.GetInstanceID() +
                ",name=" + (go != null ? go.name : "<no-go>") +
                ",active=" + (go != null && go.activeInHierarchy) +
                ",primary=" + ui.isPrimaryUI +
                ",clean=" + ui.IsCleanCopy +
                ",entity=" + (ui.entityPlayer != null ? ui.entityPlayer.entityId.ToString() : "null") +
                ",wm=" + (ui.windowManager != null) +
                ",xui=" + (ui.xui != null) +
                ",cams=" + cameras;
        }
        catch (Exception ex)
        {
            return "describe-failed:" + ex.GetType().Name;
        }
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        // A previous world can be abandoned before EntityPlayerLocal consumes the gameplay
        // LocalPlayerUI queued by StartGame. Remove such leftovers BEFORE this StartGame creates
        // its new gameplay UI; otherwise GetUIForPlayer may bind a stale/destroyed UI and Awake
        // cascades into duplicate ScreenEffects / PlayerMoveController null references.
        int abandonedUiCount = ClearAbandonedPendingPlayerUiAssignments();
        ResetSessionState();
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("GameStarting asServer=" + data.AsServer
            + " abandonedPendingUi=" + abandonedUiCount
            + " configuredMode=" + RebirthSurvivorMode.ConfiguredMode
            + " progressionOption=" + RebirthSandboxOptionManager.Current.PlayerProgression); }
    }

    private static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!data.IsLocalPlayer) return;

        EntityPlayerLocal player = GetLocalPlayer();
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("PlayerSpawned local=True entity="
            + (player != null ? player.entityId.ToString() : "none")
            + " clientInfo=" + (data.ClientInfo != null)
            + " staged=" + stagedSelectionReady
            + " mode=" + RebirthSurvivorMode.ConfiguredMode); }

        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth)
        {
            creationUiRequired = false;
            selectorOpenRequested = false;
            nextEnforcementTime = 0f;
            ClearStagedSelection();
            CloseCreationWindowsAndRestoreControl();
            return;
        }

        if (player == null)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] PlayerSpawned was local but GetPrimaryPlayer returned null.");
            return;
        }

        if (stagedSelectionReady)
        {
            DispatchStagedCommit(player);
            return;
        }

        // Returning-character fallback and diagnostic refresh. New Rebirth characters should have
        // staged a selection before reaching this point; if they did not, owner state re-opens the
        // in-game chooser while the server hold prevents gameplay.
        RebirthSurvivorNetworkService.RequestOwnerState(player, true);
    }

    private static void OnOwnerStateChanged(RebirthSurvivorOwnerStateSnapshot snapshot)
    {
        if (snapshot == null) return;

        // PC127: OwnerStateChanged also carries ordinary condition snapshots every few seconds.
        // A Ready snapshot is not a new creation completion. Capture ownership before changing
        // creationUiRequired; otherwise routine updates clear another UI's focus, rebuild its
        // action stack and overwrite a gameplay/respawn control hold that we do not own.
        bool hadPendingCreationUi = creationUiRequired || selectorOpenRequested ||
            stagedSelectionReady || stagedWaitingForOwnerReady || stagedRequestId != 0UL ||
            XUiC_RebirthSpawnSelectionSurvivorGate.IsGateVisible;

        bool locallyConfiguredForRebirth = RebirthSurvivorMode.ConfiguredMode == RebirthPlayerProgressionMode.Rebirth;
        bool required = locallyConfiguredForRebirth && snapshot.RebirthModeEnabled &&
            (snapshot.CreationState == RebirthSurvivorOwnerCreationState.CreationRequired ||
             snapshot.CreationState == RebirthSurvivorOwnerCreationState.RecoveryRequired);
        creationUiRequired = required;

        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("OwnerState rebirth=" + snapshot.RebirthModeEnabled
            + " hasCharacter=" + snapshot.HasCharacter
            + " creationState=" + snapshot.CreationState
            + " locallyConfigured=" + locallyConfiguredForRebirth
            + " staged=" + stagedSelectionReady
            + " playerExists=" + (GetLocalPlayer() != null)); }

        if (required)
        {
            // Once the player has explicitly chosen/created a Survivor pre-spawn, do not reopen the
            // chooser merely because the server still (correctly) reports CreationRequired until the
            // post-spawn commit is possible.
            if (stagedSelectionReady)
                return;

            LockLocalPlayer();
            if (GetLocalPlayer() == null)
            {
                // Pre-spawn: the chooser is a child window of the native spawnselection group itself.
                // Never open a second menu window group here; V3.2 replaces spawnselection when that
                // happens, which is exactly what caused the invisible-window dead end.
                XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(true, "owner-state-required");
            }
            else
            {
                // Defensive post-spawn fallback only. XUi_InGame still owns a standalone chooser group.
                QueueSelectorOpen();
            }
            return;
        }

        if (snapshot.HasCharacter && snapshot.CreationState == RebirthSurvivorOwnerCreationState.Ready)
        {
            if (!hadPendingCreationUi) return;
            selectorOpenRequested = false;
            nextEnforcementTime = 0f;
            stagedWaitingForOwnerReady = false;
            ClearStagedSelection();
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(false, "owner-state-ready");
            CloseCreationWindowsAndRestoreControl();
            return;
        }

        if (!locallyConfiguredForRebirth || !snapshot.RebirthModeEnabled)
        {
            if (!hadPendingCreationUi) return;
            selectorOpenRequested = false;
            nextEnforcementTime = 0f;
            ClearStagedSelection();
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(false, "owner-state-base-mode");
            CloseCreationWindowsAndRestoreControl();
        }
    }

    private static void OnCreationResult(RebirthSurvivorCreationNetworkResponse response)
    {
        if (response == null || stagedRequestId == 0UL || response.RequestId != stagedRequestId)
            return;

        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("staged creation result request=" + response.RequestId
            + " status=" + response.Status + " code=" + (response.MessageCode ?? string.Empty)); }

        stagedRequestId = 0UL;
        if (response.Status == RebirthSurvivorCreationNetworkStatus.Created ||
            response.Status == RebirthSurvivorCreationNetworkStatus.AlreadyCreated)
        {
            if (response.Status == RebirthSurvivorCreationNetworkStatus.Created)
                SaveStagedReusableProfileIfRequested();
            stagedWaitingForOwnerReady = true;
            return;
        }

        // Keep the player held and put them back into the chooser instead of leaving an apparently
        // frozen world. The detailed server failure is always emitted above.
        stagedWaitingForOwnerReady = false;
        stagedSelectionReady = false;
        stagedSelection = null;
        stagedSourceProfile = null;
        stagedCreationChoices = null;
        stagedNewModel = null;
        creationUiRequired = true;
        if (IsNativeSpawnSelectionOpen())
        {
            selectorOpenRequested = false;
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(true, "commit-failed:" + (response.MessageCode ?? string.Empty));
            { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("commit failed while native spawnselection is still open; retained integrated gate instead of opening standalone chooser code="
                + (response.MessageCode ?? string.Empty)); }
        }
        else
        {
            QueueSelectorOpen();
        }
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        TickCore("ModEvents.GameUpdate");
    }

    private static void TickCore(string source)
    {

        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth)
        {
            if (creationUiRequired || selectorOpenRequested || stagedSelectionReady)
            {
                creationUiRequired = false;
                selectorOpenRequested = false;
                nextEnforcementTime = 0f;
                ClearStagedSelection();
                CloseCreationWindowsAndRestoreControl();
            }
            spawnSelectionWasOpen = IsNativeSpawnSelectionOpen();
            return;
        }

        bool spawnSelectionOpen = IsNativeSpawnSelectionOpen();

        // Defensive fallback only. The integrated spawnselection child gate normally resolves owner state
        // from its own OnOpen; if that controller was late, refresh once the native window is present.
        if (spawnSelectionOpen && GetLocalPlayer() == null && RebirthSurvivorClientState.GetOwnerStateSnapshot() == null)
            RebirthSurvivorNetworkService.TryPublishLocalPreSpawnOwnerState("spawnselection-pump-fallback");
        if (spawnSelectionOpen != spawnSelectionWasOpen)
        {
            spawnSelectionWasOpen = spawnSelectionOpen;
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("native spawnselection open=" + spawnSelectionOpen
                + " playerExists=" + (GetLocalPlayer() != null)
                + " staged=" + stagedSelectionReady
                + " ownerKnown=" + (RebirthSurvivorClientState.GetOwnerStateSnapshot() != null)); }
        }

        if (spawnSelectionOpen && !stagedSelectionReady)
            EnforceGateAtNativeSpawnSelection();

        if (leaveWorldRequested)
        {
            leaveWorldRequested = false;
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("deferred disconnect BEGIN " + BuildDebugSummary()); }
            CloseCreationWindowsAndRestoreControl();
            GameManager manager = GameManager.Instance;
            if (manager != null)
            {
                manager.Disconnect();
                { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("deferred disconnect returned"); }
            }
            else
                Log.Warning("[REBIRTH Survivor][PreSpawnTrace] GameManager.Instance was null; disconnect skipped");
            return;
        }

        if (resumeNativeSpawnRequested)
        {
            resumeNativeSpawnRequested = false;
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(false, "resume-native-spawn");
            CloseCreatorWindowOnly();
            OpenNativeSpawnSelection();
            return;
        }

        if (stagedSelectionReady)
        {
            EntityPlayerLocal stagedPlayer = GetLocalPlayer();
            if (stagedPlayer != null && stagedRequestId == 0UL)
            {
                { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("pump detected spawned local player with staged Survivor entity=" + stagedPlayer.entityId + " source=" + (source ?? "unspecified")); }
                DispatchStagedCommit(stagedPlayer);
            }
            return;
        }

        if (!creationUiRequired)
            return;

        // Before the native player exists, the chooser is physically inside the native spawnselection
        // window itself. Do not call GUIWindowManager.Open on a second group.
        if (GetLocalPlayer() == null)
        {
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(true, "tick-pre-spawn-required");
            return;
        }

        float now = UnityEngine.Time.realtimeSinceStartup;
        if (selectorOpenRequested)
        {
            selectorOpenRequested = false;
            nextEnforcementTime = now + 0.5f;
            LockLocalPlayer();
            GUIWindowManager queuedManager = pendingSelectorManager;
            pendingSelectorManager = null;
            if (queuedManager != null) OpenSelectorFromManager(queuedManager, "queued-native-open-intercept");
            else EnsureSelectorOpen();
            return;
        }
        if (now < nextEnforcementTime) return;
        nextEnforcementTime = now + 0.5f;
        LockLocalPlayer();
        EnsureSelectorOpen();
    }

    public static void NotifyNativePlayerSpawned(ClientInfo clientInfo, RespawnType reason, Vector3i pos, int entityId)
    {
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth)
            return;

        EntityPlayerLocal local = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetEntity(entityId) as EntityPlayerLocal : null;
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("GameManager.PlayerSpawnedInWorld postfix entity=" + entityId
            + " local=" + (local != null) + " clientInfo=" + (clientInfo != null)
            + " reason=" + reason + " staged=" + stagedSelectionReady + " request=" + stagedRequestId); }

        if (local == null)
            return;

        if (stagedSelectionReady && stagedRequestId == 0UL)
        {
            DispatchStagedCommit(local);
            RebirthSurvivorNetworkService.PumpLocalCreationQueue("PlayerSpawnedInWorld-postfix");
            return;
        }

        if (!stagedSelectionReady)
        {
            // A successful staged commit clears stagedSelectionReady before the native
            // PlayerSpawnedInWorld postfix can run on some listen-server orderings. Never
            // reinterpret that successful Ready owner state as a missing selection and reopen
            // the obsolete standalone chooser.
            RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
            bool ownerReady = owner != null && owner.RebirthModeEnabled && owner.HasCharacter &&
                owner.CreationState == RebirthSurvivorOwnerCreationState.Ready;
            { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("post-spawn staged=false ownerSnapshot=" +
                (owner == null ? "null" : (owner.CreationState + "/has=" + owner.HasCharacter + "/rebirth=" + owner.RebirthModeEnabled)) +
                " ownerReady=" + ownerReady + " entity=" + entityId); }
            if (ownerReady)
            {
                creationUiRequired = false;
                selectorOpenRequested = false;
                pendingSelectorManager = null;
                { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("post-spawn owner already Ready; standalone chooser fallback suppressed entity=" + entityId); }
                return;
            }

            // True defensive fallback: no staged selection AND the authoritative owner is not Ready.
            creationUiRequired = true;
            QueueSelectorOpen();
            Log.Warning("[REBIRTH Survivor][SpawnFlow] local player spawned without a staged Survivor and owner is not Ready; forcing recovery chooser entity=" + entityId);
        }
    }

    private static bool IsSelectorOpen(GUIWindowManager manager)
    {
        if (GetLocalPlayer() == null)
            return XUiC_RebirthSpawnSelectionSurvivorGate.IsGateVisible;
        if (manager == null) return false;
        try
        {
            return manager.IsWindowOpen(XUiC_RebirthChooseSurvivor.WindowGroupId) ||
                manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId);
        }
        catch
        {
            return false;
        }
    }

    private static void OpenSelectorFromManager(GUIWindowManager manager, string reason)
    {
        if (GetLocalPlayer() == null)
        {
            if (IsNativeSpawnSelectionOpen())
            {
                XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(true, "open-selector-redirect:" + (reason ?? "unspecified"));
            }
            else
            {
                resumeNativeSpawnRequested = true;
                { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("standalone chooser open redirected to native spawnselection reopen reason=" + (reason ?? "unspecified")); }
            }
            return;
        }
        if (manager == null)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] cannot open Survivor chooser: GUIWindowManager is null reason=" + (reason ?? "unspecified"));
            selectorOpenRequested = true;
            return;
        }
        try
        {
            if (!manager.IsWindowOpen(XUiC_RebirthChooseSurvivor.WindowGroupId))
                manager.Open(XUiC_RebirthChooseSurvivor.WindowGroupId, true);
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("Survivor chooser open requested directly from GUIWindowManager reason=" + (reason ?? "unspecified")
                + " openNow=" + manager.IsWindowOpen(XUiC_RebirthChooseSurvivor.WindowGroupId)); }
        }
        catch (Exception ex)
        {
            selectorOpenRequested = true;
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] direct Survivor chooser open failed reason=" + (reason ?? "unspecified")
                + " error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void EnforceGateAtNativeSpawnSelection()
    {
        RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if (owner == null)
        {
            // Keep the native spawn window alive as the XUi_Menu render host while owner state is
            // resolved. The REBIRTH overlay itself is the interaction gate.
            creationUiRequired = true;
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(true, "enforce-owner-unknown");
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now >= nextUnknownOwnerTraceTime)
            {
                nextUnknownOwnerTraceTime = now + 2f;
                Log.Warning("[REBIRTH Survivor][PreSpawnTrace] native spawnselection is open while waiting for authoritative owner state; overlay enforced. " + BuildDebugSummary());
            }
            return;
        }

        if (!owner.RebirthModeEnabled)
        {
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(false, "enforce-owner-base");
            return;
        }

        if (owner.HasCharacter && owner.CreationState == RebirthSurvivorOwnerCreationState.Ready)
        {
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(false, "enforce-owner-ready");
            return;
        }

        creationUiRequired = owner.CreationState == RebirthSurvivorOwnerCreationState.CreationRequired ||
            owner.CreationState == RebirthSurvivorOwnerCreationState.RecoveryRequired;
        // Keep the profile browser visible after a pre-spawn profile has been staged. The native
        // Spawn button is unlocked in-place, and the player can still see/change the selected
        // Survivor without leaving spawnselection.
        XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(creationUiRequired || stagedSelectionReady,
            "enforce-owner-state:" + owner.CreationState);
    }

    /// <summary>
    /// Called by the chooser when a reusable profile is selected while no EntityPlayerLocal exists.
    /// The choice is intentionally staged rather than committed: the creation service needs the
    /// freshly spawned EntityPlayer to initialize metabolism, progression and native-facing state.
    /// </summary>
    public static bool StageExistingProfile(RebirthSurvivorProfile profile, out string error)
    {
        error = string.Empty;
        if (profile == null)
        {
            error = "Survivor Profile is missing.";
            return false;
        }
        RebirthSurvivorCreationSelection selection = new RebirthSurvivorCreationSelection(
            profile.BackgroundId, profile.DietId, profile.TraitIds, RebirthSurvivorDefinitionRegistry.SemanticHash);
        return StageSelection(selection, profile, null, null, out error);
    }

    /// <summary>Stage a newly-created Survivor from the menu creator until native spawn creates the player entity.</summary>
    public static bool StageNewSurvivor(RebirthSurvivorCreatorViewModel vm, out string error)
    {
        error = string.Empty;
        if (vm == null || vm.Validation == null || !vm.Validation.IsValid || vm.Validation.RemainingCreationPoints != 0)
        {
            error = RebirthSurvivorUiText.L("xuiRebirthSurvivorValidationUnavailable", "Survivor selection is invalid.");
            return false;
        }
        return StageSelection(vm.BuildSelection(), null, vm.GetCreationChoicesSnapshot(), vm, out error);
    }

    private static bool StageSelection(RebirthSurvivorCreationSelection selection, RebirthSurvivorProfile sourceProfile,
        IDictionary<string, string> creationChoices, RebirthSurvivorCreatorViewModel newModel, out string error)
    {
        error = string.Empty;
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth)
        {
            error = "This world is not using Rebirth character progression.";
            return false;
        }
        if (selection == null)
        {
            error = "Survivor selection is missing.";
            return false;
        }

        RebirthSurvivorCreationResult validation = RebirthSurvivorCreationValidator.ValidateForCommit(selection, true);
        if (validation == null || !validation.IsValid || validation.RemainingCreationPoints != 0)
        {
            error = RebirthSurvivorUiText.BuildValidationSummary(validation);
            if (string.IsNullOrEmpty(error)) error = "Survivor selection is invalid.";
            return false;
        }

        stagedSelection = new RebirthSurvivorCreationSelection(selection.BackgroundId, selection.DietId, selection.TraitIds, selection.DefinitionHash);
        stagedSourceProfile = sourceProfile;
        stagedCreationChoices = creationChoices != null ? new Dictionary<string, string>(creationChoices, StringComparer.OrdinalIgnoreCase) : null;
        stagedNewModel = newModel;
        stagedSelectionReady = true;
        stagedRequestId = 0UL;
        stagedWaitingForOwnerReady = false;
        selectorOpenRequested = false;
        // If an existing profile was chosen, spawnselection is already open: hiding the integrated
        // gate immediately reveals the native spawn controls. If the separate creator temporarily
        // replaced the menu group, reopen spawnselection immediately; stagedSelectionReady makes
        // the integrated gate start hidden.
        bool nativeAlreadyOpen = IsNativeSpawnSelectionOpen();
        bool profileManagerOwnsTransition = XUiC_RebirthSurvivorProfileManager.IsSpawnSelectionMode;
        // Selection is now complete. Ensure the legacy embedded gate is hidden/unlocked; the
        // Profile Manager owns the transition back to the stock V3.2 spawnselection window.
        XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(false, "selection-staged");
        resumeNativeSpawnRequested = false;
        // When the reusable Profile Manager is selecting for this world, it owns the transition
        // order: stage -> close Profile Manager -> reopen native spawnselection. Opening the native
        // group here while the Profile Manager is still active would recreate the overlapping-menu
        // failure that originally made both screens disappear.
        if (!nativeAlreadyOpen && !profileManagerOwnsTransition)
            OpenNativeSpawnSelection();

        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("staged pre-spawn Survivor source=" + (sourceProfile != null ? "profile" : "new")
            + " background=" + stagedSelection.BackgroundId
            + " diet=" + stagedSelection.DietId
            + " traits=" + stagedSelection.TraitIds.Count
            + " playerExists=" + (GetLocalPlayer() != null)
            + " nativeSpawnAlreadyOpen=" + nativeAlreadyOpen
            + " profileManagerOwnsTransition=" + profileManagerOwnsTransition
            + " resumeNative=" + resumeNativeSpawnRequested); }
        return true;
    }

    private static void DispatchStagedCommit(EntityPlayerLocal player)
    {
        if (player == null || !stagedSelectionReady || stagedSelection == null || stagedRequestId != 0UL)
            return;

        stagedRequestId = stagedSourceProfile != null
            ? RebirthSurvivorNetworkService.RequestCommit(player, stagedSelection, stagedSourceProfile)
            : RebirthSurvivorNetworkService.RequestCommit(player, stagedSelection, null, stagedCreationChoices);

        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("dispatch staged commit entity=" + player.entityId
            + " request=" + stagedRequestId
            + " source=" + (stagedSourceProfile != null ? "profile" : "new")); }

        if (stagedRequestId == 0UL)
        {
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] staged commit could not be sent; reopening chooser under creation hold.");
            stagedSelectionReady = false;
            creationUiRequired = true;
            QueueSelectorOpen();
        }
    }

    private static void SaveStagedReusableProfileIfRequested()
    {
        RebirthSurvivorCreatorViewModel vm = stagedNewModel;
        if (vm == null || !vm.SaveAsReusableProfile || !vm.HasValidProfileName) return;
        RebirthSurvivorProfile created;
        string error;
        if (!RebirthSurvivorProfileStore.TryCreate(vm.ProfileName.Trim(), vm.BuildSelection(), vm.GetCreationChoicesSnapshot(), out created, out error))
            Log.Warning("[REBIRTH Survivor UI] world character created but reusable profile save failed: " + error);
        else
            Log.Out("[REBIRTH Survivor UI] reusable profile saved after staged world creation id=" + created.ProfileId);
    }

    public static void ReturnToSelector()
    {
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth) return;
        if (!creationUiRequired || stagedSelectionReady) return;
        LockLocalPlayer();
        if (GetLocalPlayer() == null)
        {
            if (IsNativeSpawnSelectionOpen())
            {
                XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(true, "return-to-selector-native-open");
            }
            else
            {
                // The first-world creator intentionally replaces the native menu group while it is
                // open. Once it closes, immediately reopen spawnselection; the integrated child gate
                // will render there again. No GameUpdate/pump is required.
                OpenNativeSpawnSelection();
                { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("ReturnToSelector reopened native spawnselection for integrated Survivor gate."); }
            }
            return;
        }
        QueueSelectorOpen();
    }

    private static void QueueSelectorOpen(GUIWindowManager manager = null)
    {
        if (manager != null) pendingSelectorManager = manager;
        selectorOpenRequested = true;
        nextEnforcementTime = 0f;
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("chooser queued manager=" + (manager != null) + " " + BuildDebugSummary()); }
    }

    public static void LeaveWorld()
    {
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("LeaveWorld requested " + BuildDebugSummary()); }
        creationUiRequired = false;
        selectorOpenRequested = false;
        resumeNativeSpawnRequested = false;
        nextEnforcementTime = 0f;
        ClearStagedSelection();
        CloseCreationWindowsAndRestoreControl();

        // Before EntityPlayerLocal exists, closing spawnselection/profile UI can stop the menu
        // GameUpdate path that previously owned the deferred disconnect.  Use the global Unity
        // coroutine pump instead so Back reliably leaves the pre-spawn world on the next frame.
        if (GetLocalPlayer() == null)
        {
            leaveWorldRequested = false;
            if (!leaveWorldCoroutineStarted)
            {
                leaveWorldCoroutineStarted = true;
                ThreadManager.StartCoroutine(DisconnectToMainMenuNextFrame());
            }
            return;
        }

        // In an active world keep the existing GameUpdate deferral.
        leaveWorldRequested = true;
    }

    private static IEnumerator DisconnectToMainMenuNextFrame()
    {
        // Give the cancel click one frame to close the Survivor/spawn windows, then put a
        // dedicated transition overlay on the gameplay UI that is about to be torn down.
        // The bar is intentionally indeterminate: the engine does not expose a truthful
        // percentage for save/world teardown.
        yield return null;
        // The stock loading-tip screen shown while leaving a world belongs to the persistent
        // primary/menu LocalPlayerUI, not the temporary gameplay LocalPlayerUI.  Put the
        // transition on that same persistent UI so it actually renders above the loading
        // screen and survives destruction of the gameplay UI during Disconnect().
        LocalPlayerUI transitionUi = LocalPlayerUI.primaryUI;
        bool nativeProgressOpened = false;
        if (transitionUi != null && transitionUi.windowManager != null)
        {
            try
            {
                XUiC_ProgressWindow.Open(transitionUi, "Returning to Main Menu");
                nativeProgressOpened = XUiC_ProgressWindow.IsWindowOpen();
            }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH Survivor][PreSpawnTrace] native return progress open failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("return-to-menu native progress requested primaryUi=" + (transitionUi != null) +
            " opened=" + nativeProgressOpened + " progressId='" + XUiC_ProgressWindow.ID + "'"); }
        // Give the native progress window a full rendered frame before world teardown begins.
        yield return null;

        // StartGame creates and queues a gameplay LocalPlayerUI before EntityPlayerLocal exists.
        // If first-entry is cancelled, no player ever consumes that queue entry.  Leaving it in
        // the static queue lets the NEXT StartGame dequeue a destroyed UI and EntityPlayerLocal
        // then fails in Awake().  Remove pending assignments now; the actual UI object remains
        // alive long enough to render this transition and normal Disconnect cleanup owns it.
        int abandonedUiCount = ClearAbandonedPendingPlayerUiAssignments();

        leaveWorldCoroutineStarted = false;
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("pre-spawn deferred disconnect BEGIN abandonedPendingUi=" + abandonedUiCount + " " + BuildDebugSummary()); }
        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            if (transitionUi != null)
            {
                try { XUiC_ProgressWindow.SetText(transitionUi, "Saving and unloading world", true); }
                catch (Exception ex) { Log.Warning("[REBIRTH Survivor][PreSpawnTrace] native return progress text update failed: " + ex.GetType().Name + ": " + ex.Message); }
            }
            manager.Disconnect();
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("pre-spawn deferred disconnect returned"); }
            if (transitionUi != null)
            {
                try { XUiC_ProgressWindow.Close(transitionUi); }
                catch (Exception ex) { Log.Warning("[REBIRTH Survivor][PreSpawnTrace] native return progress close failed: " + ex.GetType().Name + ": " + ex.Message); }
            }
        }
        else
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] GameManager.Instance was null; pre-spawn disconnect skipped");
    }

    /// <summary>
    /// Clears only the base game's pending LocalPlayerUI assignment queue during a pre-spawn
    /// cancellation.  There is no EntityPlayerLocal at this point, therefore every queued entry
    /// belongs to an abandoned StartGame attempt.  Reflection keeps this compatible with game
    /// builds where the queue remains private in the compile surface.
    /// </summary>
    private static int ClearAbandonedPendingPlayerUiAssignments()
    {
        try
        {
            System.Reflection.FieldInfo field = typeof(LocalPlayerUI).GetField(
                "playerUIQueueForPendingEntity",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                Log.Warning("[REBIRTH Survivor][PreSpawnTrace] pending LocalPlayerUI queue field unavailable; cancellation cleanup skipped");
                return 0;
            }

            object queue = field.GetValue(null);
            if (queue == null) return 0;
            System.Reflection.PropertyInfo countProperty = queue.GetType().GetProperty("Count");
            System.Reflection.MethodInfo clearMethod = queue.GetType().GetMethod("Clear", Type.EmptyTypes);
            if (countProperty == null || clearMethod == null)
            {
                Log.Warning("[REBIRTH Survivor][PreSpawnTrace] pending LocalPlayerUI queue API unavailable; cancellation cleanup skipped");
                return 0;
            }

            int count = Convert.ToInt32(countProperty.GetValue(queue, null));
            if (count <= 0) return 0;

            // Do not merely clear the queue. StartGame creates a full gameplay LocalPlayerUI
            // GameObject before EntityPlayerLocal exists. If first-entry is cancelled, that UI
            // never gets assigned and survives in LocalPlayerUI.playerUIs unless the object is
            // destroyed. A later StartGame can then have two unassigned gameplay UIs/cameras,
            // which corrupts EntityPlayerLocal.Awake (duplicate ScreenEffects / null player UI
            // state). Snapshot and destroy only the queued, non-primary UIs owned by this
            // abandoned pre-spawn attempt, then clear the queue.
            int destroyed = 0;
            System.Collections.IEnumerable pending = queue as System.Collections.IEnumerable;
            if (pending != null)
            {
                System.Collections.Generic.List<LocalPlayerUI> abandoned = new System.Collections.Generic.List<LocalPlayerUI>();
                foreach (object entry in pending)
                {
                    LocalPlayerUI ui = entry as LocalPlayerUI;
                    if (ui != null && ui != LocalPlayerUI.primaryUI)
                        abandoned.Add(ui);
                }

                for (int i = 0; i < abandoned.Count; i++)
                {
                    LocalPlayerUI ui = abandoned[i];
                    if (ui == null) continue;
                    UnityEngine.GameObject go = ui.gameObject;
                    if (go == null) continue;
                    UnityEngine.Object.Destroy(go);
                    destroyed++;
                }
            }

            clearMethod.Invoke(queue, null);
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("cleared abandoned pending LocalPlayerUI assignments count=" + count + " destroyed=" + destroyed); }
            return count;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] pending LocalPlayerUI cancellation cleanup failed: " + ex.GetType().Name + ": " + ex.Message);
            return 0;
        }
    }

    private static void EnsureSelectorOpen()
    {
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth || stagedSelectionReady) return;

        // Before EntityPlayerLocal exists, the spawnselection bridge owns redirection to the
        // full-screen Survivor Profiles group. Request that bridge while stock spawn is open.
        if (GetLocalPlayer() == null)
        {
            if (!IsNativeSpawnSelectionOpen())
            {
                resumeNativeSpawnRequested = true;
                { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("EnsureSelectorOpen needs native spawnselection reopened for embedded chooser."); }
                return;
            }
            XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(true, "ensure-selector-embedded");
            return;
        }

        XUi xui = GetActiveXUi();
        if (xui == null || xui.playerUI == null || xui.playerUI.windowManager == null)
            return;
        GUIWindowManager manager = xui.playerUI.windowManager;
        if (manager.IsWindowOpen(XUiC_RebirthChooseSurvivor.WindowGroupId) ||
            manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId)) return;
        XUiController group = xui.FindWindowGroupByName(XUiC_RebirthChooseSurvivor.WindowGroupId);
        if (group == null || group.windowGroup == null)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] chooser group is not registered in active XUi; menu=" + (GetLocalPlayer() == null));
            return;
        }
        manager.Open((GUIWindow)group.windowGroup, true, true);
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("opened Survivor chooser xuiLayer=" + (GetLocalPlayer() == null ? "Menu" : "InGame")); }
    }

    public static bool IsNativeSpawnSelectionOpenForDiagnostics() { return IsNativeSpawnSelectionOpen(); }

    private static bool IsNativeSpawnSelectionOpen()
    {
        GUIWindowManager manager = nativeSpawnManager;
        if (manager == null)
            return false;
        try
        {
            return manager.IsWindowOpen("spawnselection");
        }
        catch
        {
            return false;
        }
    }

    private static void CloseNativeSpawnSelection(string reason)
    {
        GUIWindowManager manager = nativeSpawnManager;
        if (manager == null)
            return;
        try
        {
            if (!manager.IsWindowOpen("spawnselection")) return;
            manager.Close("spawnselection");
            spawnSelectionWasOpen = false;
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("closed native spawnselection reason=" + (reason ?? "unspecified")); }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] close native spawnselection failed reason=" + (reason ?? "unspecified")
                + " error=" + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void OpenNativeSpawnSelection()
    {
        GUIWindowManager manager = nativeSpawnManager;
        if (manager == null)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] could not resume native spawnselection: captured XUi_Menu manager unavailable.");
            resumeNativeSpawnRequested = true;
            return;
        }
        try
        {
            if (!manager.IsWindowOpen("spawnselection"))
                manager.Open("spawnselection", true);
            spawnSelectionWasOpen = true;
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("native spawnselection available after Survivor was staged."); }
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Survivor][PreSpawnTrace] resume native spawnselection failed: " + ex.GetType().Name + ": " + ex.Message);
            resumeNativeSpawnRequested = true;
        }
    }

    private static void LockLocalPlayer()
    {
        EntityPlayerLocal player = GetLocalPlayer();
        if (player == null) return;
        player.ClearMovementInputs();
        player.SetControllable(false);
    }

    private static void CloseCreationWindowsAndRestoreControl()
    {
        CloseCreationWindows(true);
    }

    private static void CloseCreatorWindowOnly()
    {
        GUIWindowManager captured = nativeSpawnManager;
        if (captured != null)
        {
            try
            {
                if (captured.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId))
                    captured.Close(XUiC_RebirthSurvivorCreator.WindowGroupId);
            }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH Survivor][PreSpawnTrace] closing creator window failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    private static void CloseCreationWindows(bool restorePlayerControl)
    {
        // In XUi_Menu, first-entry selection is owned by the full-screen Survivor Profiles group.
        // Clear any legacy bridge state; the old rebirthChooseSurvivor menu group is not used.
        XUiC_RebirthSpawnSelectionSurvivorGate.SetGateVisible(false, "close-creation-windows");
        GUIWindowManager captured = nativeSpawnManager;
        if (captured != null)
        {
            try
            {
                if (captured.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId)) captured.Close(XUiC_RebirthSurvivorCreator.WindowGroupId);
            }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH Survivor][PreSpawnTrace] closing captured pre-spawn windows failed: "
                    + ex.GetType().Name + ": " + ex.Message);
            }
        }

        XUi xui = GetActiveXUi();
        if (xui != null && xui.playerUI != null)
        {
            GUIWindowManager manager = xui.playerUI.windowManager;
            RestoreUiInputState(xui);
            if (manager != null && !object.ReferenceEquals(manager, captured))
            {
                if (manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId)) manager.Close(XUiC_RebirthSurvivorCreator.WindowGroupId);
                // Only XUi_InGame still has the standalone chooser group.
                if (GetLocalPlayer() != null && manager.IsWindowOpen(XUiC_RebirthChooseSurvivor.WindowGroupId))
                    manager.Close(XUiC_RebirthChooseSurvivor.WindowGroupId);
            }
            RestoreUiInputState(xui);
        }

        EntityPlayerLocal player = GetLocalPlayer();
        if (player != null)
        {
            player.ClearMovementInputs();
            if (restorePlayerControl) player.SetControllable(true);
        }
    }

    private static void RestoreUiInputState(XUi xui)
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
            cursor.Locked = false;
            cursor.ResetNavigationTarget();
        }
        if (xui.playerUI.windowManager != null) xui.playerUI.windowManager.ResetActionSets();
    }

    private static XUi GetActiveXUi()
    {
        EntityPlayerLocal player = GetLocalPlayer();
        if (player != null && player.PlayerUI != null && player.PlayerUI.xui != null)
            return player.PlayerUI.xui;
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        return ui != null ? ui.xui : null;
    }

    private static EntityPlayerLocal GetLocalPlayer()
    {
        return GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer() : null;
    }

    private static void ClearStagedSelection()
    {
        stagedSelectionReady = false;
        stagedSelection = null;
        stagedSourceProfile = null;
        stagedCreationChoices = null;
        stagedNewModel = null;
        stagedRequestId = 0UL;
        stagedWaitingForOwnerReady = false;
    }

    private static void ResetSessionState()
    {
        creationUiRequired = false;
        selectorOpenRequested = false;
        leaveWorldRequested = false;
        leaveWorldCoroutineStarted = false;
        resumeNativeSpawnRequested = false;
        nextEnforcementTime = 0f;
        nextUnknownOwnerTraceTime = 0f;
        spawnSelectionWasOpen = false;
        pendingSelectorManager = null;
        nativeSpawnManager = null;
        ClearStagedSelection();
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        int abandonedUiCount = ClearAbandonedPendingPlayerUiAssignments();
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("WorldShuttingDown abandonedPendingUi=" + abandonedUiCount + " " + BuildDebugSummary()); }
        ResetSessionState();
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        int abandonedUiCount = ClearAbandonedPendingPlayerUiAssignments();
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("GameShutdown abandonedPendingUi=" + abandonedUiCount + " " + BuildDebugSummary()); }
        ResetSessionState();
    }
}

/// <summary>
/// Lightweight pre-spawn cancellation overlay. The engine does not expose a truthful percentage
/// for world teardown, so the bar is deliberately indeterminate while stage text communicates
/// what is happening. The gameplay LocalPlayerUI owns this window and normal disconnect cleanup
/// destroys it when the main menu takes over.
/// </summary>
[UnityEngine.Scripting.Preserve]
public sealed class RebirthReturnToMainMenuProgress : XUiController
{
    public const string WindowGroupId = "rebirthReturnToMainMenuProgress";
    private const int TrackWidth = 620;
    private const int MinimumFill = 90;

    private static RebirthReturnToMainMenuProgress active;
    private static string requestedStage = "Returning to Main Menu";

    private XUiV_Label statusLabel;
    private XUiController barFill;
    private float elapsed;
    private float sweep;

    public override void Init()
    {
        base.Init();
        XUiController status = GetChildById("returnToMenuStatus");
        statusLabel = status != null ? status.ViewComponent as XUiV_Label : null;
        barFill = GetChildById("returnToMenuProgressFill");
    }

    public override void OnOpen()
    {
        base.OnOpen();
        active = this;
        elapsed = 0f;
        sweep = 0f;
        ApplyStage(requestedStage);
        if (xui != null && xui.playerUI != null && xui.playerUI.CursorController != null)
        {
            xui.playerUI.CursorController.SetNavigationTarget(null);
            xui.playerUI.CursorController.Locked = true;
            xui.playerUI.CursorController.SetCursorHidden(true);
        }
        UpdateBar();
    }

    public override void OnClose()
    {
        if (ReferenceEquals(active, this)) active = null;
        base.OnClose();
    }

    public override void Update(float _dt)
    {
        base.Update(_dt);
        elapsed += UnityEngine.Mathf.Max(0f, _dt);
        sweep += UnityEngine.Mathf.Max(0f, _dt) * 0.55f;

        if (elapsed > 2.0f)
            ApplyStage("Returning to main menu");
        else if (elapsed > 0.45f && string.Equals(requestedStage, "Returning to Main Menu", System.StringComparison.Ordinal))
            ApplyStage("Saving and unloading world");

        UpdateBar();
    }

    private void UpdateBar()
    {
        if (barFill == null || barFill.ViewComponent == null) return;
        float t = UnityEngine.Mathf.Repeat(sweep, 1f);
        // Saw-tooth growth from 15% to 100%, then restart. This is an activity indicator, not a
        // fabricated completion percentage.
        int width = UnityEngine.Mathf.Clamp(UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Lerp(MinimumFill, TrackWidth, t)), MinimumFill, TrackWidth);
        barFill.ViewComponent.Size = new Vector2i(width, 18);
    }

    private void ApplyStage(string text)
    {
        if (statusLabel != null) statusLabel.Text = string.IsNullOrEmpty(text) ? "Returning to Main Menu" : text;
    }

    public static void Open(LocalPlayerUI playerUi)
    {
        requestedStage = "Returning to Main Menu";
        if (playerUi == null || playerUi.windowManager == null) return;
        try
        {
            if (!playerUi.windowManager.IsWindowOpen(WindowGroupId))
                playerUi.windowManager.Open(WindowGroupId, true);
        }
        catch (System.Exception ex)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] return-to-menu progress overlay could not open: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public static void Close(LocalPlayerUI playerUi)
    {
        if (playerUi == null || playerUi.windowManager == null) return;
        try
        {
            if (playerUi.windowManager.IsWindowOpen(WindowGroupId))
                playerUi.windowManager.Close(WindowGroupId);
        }
        catch (System.Exception ex)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] return-to-menu progress overlay could not close: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public static void SetStage(string stage)
    {
        requestedStage = string.IsNullOrEmpty(stage) ? "Returning to Main Menu" : stage;
        if (active != null) active.ApplyStage(requestedStage);
    }
}

[HarmonyLib.HarmonyPatch(typeof(LocalPlayerUI), "GetUIForPlayer")]
public static class RebirthSurvivorLocalPlayerUiHandoffDiagnosticPatch
{
    [HarmonyLib.HarmonyPrefix]
    public static void Prefix(EntityPlayerLocal _entityPlayer)
    {
        RebirthSurvivorFirstEntryUiService.TraceNativePlayerUiHandoff("GetUIForPlayer-PRE", _entityPlayer, null);
    }

    [HarmonyLib.HarmonyPostfix]
    public static void Postfix(EntityPlayerLocal _entityPlayer, LocalPlayerUI __result)
    {
        RebirthSurvivorFirstEntryUiService.TraceNativePlayerUiHandoff("GetUIForPlayer-POST", _entityPlayer, __result);
    }
}

[HarmonyLib.HarmonyPatch(typeof(EntityPlayerLocal), "Awake")]
public static class RebirthSurvivorEntityPlayerLocalAwakeDiagnosticPatch
{
    [HarmonyLib.HarmonyPrefix]
    public static void Prefix(EntityPlayerLocal __instance)
    {
        RebirthSurvivorFirstEntryUiService.TraceNativePlayerUiHandoff("EntityPlayerLocal.Awake-PRE", __instance, null);
    }
}

