using Platform;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Rebirth-owned Personal Crafting window-group controller.
///
/// Important: this deliberately subclasses XUiC_CraftingWindowGroup so native actions and
/// cross-window systems can continue to discover the expected behavioral contract. Its Init()
/// does NOT call XUiC_CraftingWindowGroup.Init(), because Chunk B intentionally removes the
/// native RecipeList/CraftingInfo/Queue topology that the base Init unconditionally assumes.
/// Later chunks add Rebirth-compatible subclasses back into this single owned visual tree.
/// </summary>
[Preserve]
public sealed class XUiC_RebirthPersonalCrafting : XUiC_CraftingWindowGroup
{
    private RebirthPersonalCraftingLayoutService layoutService;
    private XUiController pagingHeader;
    private bool pagingHeaderWasVisibleBeforeOpen;
    private float nextLayoutCheck;
    private bool layoutAuditRequested = true;
    private string lastLoggedAudit = string.Empty;
    private readonly Dictionary<XUiController, bool> hudVisibilityBeforeOpen = new Dictionary<XUiController, bool>();
    private readonly Dictionary<XUiController, bool> hudEnabledBeforeOpen = new Dictionary<XUiController, bool>();
    private readonly Dictionary<XUiController, Vector3> hudScaleBeforeOpen = new Dictionary<XUiController, Vector3>();
    private readonly Dictionary<XUiController, bool> hudActiveBeforeOpen = new Dictionary<XUiController, bool>();
    private readonly Dictionary<XUiController, bool> hudDormantBeforeOpen = new Dictionary<XUiController, bool>();
    private static readonly Dictionary<Type, FieldInfo> dormantFieldCache = new Dictionary<Type, FieldInfo>();
    private float nextHudTrace;
    private float nextHudSuppressionCheck;
    private int hudReapplyCount;
    private int hudReappearSinceTrace;

    // PC127: this controller owns its cursor/focus cleanup, NOT the player's global control
    // override. GUIWindowManager owns the one action-set push/pop for the whole window session.
    // Inventory/Crafting are internal surfaces and must not acquire additional input ownership.
    private bool inputOwnershipActive;
    private bool cursorHiddenBeforeOpen;
    private int lastCancelFrame = -1;

    // Native RecipeStack.HandleOnPress ends by dirtying the ENTIRE window-group tree. For the
    // custom queue that is unnecessary and is visible as a full queue redraw. The custom cancel
    // path scopes that one legacy invalidation away, then refreshes only the UI surfaces whose
    // data actually changed (inventory / requirements / recipe state).
    private int incrementalQueueMutationDepth;

    // PC125: the public `crafting` window group now has two internal presentation surfaces.
    // Tab/native inventory opening always starts on compact Inventory. Crafting is entered only
    // through the explicit top navigation tab and can return to Inventory without closing/reopening.
    public enum SurfaceMode
    {
        Inventory = 0,
        Crafting = 1
    }

    private SurfaceMode surfaceMode = SurfaceMode.Crafting;
    private bool lastAuditInventoryMode;
    private bool hasAuditSurfaceBaseline;

    public bool IsInventoryOnlyMode => false;
    public SurfaceMode CurrentSurfaceMode => surfaceMode;

    public static XUiC_RebirthPersonalCrafting ActiveInstance { get; private set; }

    private long craftIntentEpoch;
    public long CraftIntentEpoch => craftIntentEpoch;
    public void AdvanceCraftIntentEpoch()
    {
        unchecked { ++craftIntentEpoch; }
        if (craftIntentEpoch == 0) ++craftIntentEpoch;
    }

    public RebirthPersonalCraftingState State { get; } = new RebirthPersonalCraftingState();
    public RebirthPersonalCraftingCoordinator Coordinator { get; private set; }

    public override void Init()
    {
        // Install the narrowly scoped source-level HUD guards before this window can open.
        // XUiC_Location and the native tracker controllers otherwise reassert their own
        // visibility during Update, which causes the top-right HUD to flash through Crafting.
        RebirthPersonalCraftingHudSuppressionInstaller.EnsureInstalled();

        // V3.2 b10 exposes AlwaysUpdate as an inherited field, not a virtual method/property.
        AlwaysUpdate = true;

        // Coordinator must exist before child Init() so every custom child can bind to the same
        // interaction-state owner instead of creating independent state.
        Coordinator = new RebirthPersonalCraftingCoordinator(this, State);

        // Reproduce XUiController.Init() without invoking XUiC_CraftingWindowGroup.Init(),
        // which assumes the stock RecipeList/CategoryList/InfoWindow topology that the Rebirth-owned screen deliberately replaces.
        if (viewComponent != null)
            viewComponent.InitView();
        for (int i = 0; i < children.Count; i++)
            children[i].Init();
        curInputStyle = PlatformManager.NativePlatform.Input.CurrentInputStyle;

        XUiC_RebirthCraftingRecipeCatalogue rebirthRecipeList = GetChildByType<XUiC_RebirthCraftingRecipeCatalogue>();
        recipeList = rebirthRecipeList;

        // V3.2 b10 native crafting keeps searchInput outside the RecipeList controller.
        // Resolve and attach it only after the complete child tree has initialized so
        // XUiC_TextInput.Init runs through the normal generic-parent path, never through
        // XUiC_RebirthCraftingRecipeCatalogue's manual RecipeList initialization.
        XUiC_TextInput recipeSearch = GetChildById("rebirthCraftingRecipeSearch") as XUiC_TextInput;
        XUiController recipeSearchPlaceholderController = GetChildById("rebirthCraftingRecipeSearchPlaceholder");
        XUiV_Label recipeSearchPlaceholder = recipeSearchPlaceholderController != null
            ? recipeSearchPlaceholderController.ViewComponent as XUiV_Label
            : null;
        rebirthRecipeList?.AttachSearchInput(recipeSearch, recipeSearchPlaceholder);

        // Chunk E restores only the native behavioral contracts required for the real craft
        // transaction. Their stock visuals remain absent/off-screen inside the Rebirth-owned tree.
        craftingQueue = GetChildByType<XUiC_CraftingQueue>();
        craftCountControl = GetChildByType<XUiC_RecipeCraftCount>();
        categoryList = null;
        craftInfoWindow = null;
        workstation = string.Empty;

        layoutService = new RebirthPersonalCraftingLayoutService(this);
        ResolvePagingHeader();
        layoutService.Apply(true);
    }

    public void BeginIncrementalQueueMutation()
    {
        incrementalQueueMutationDepth++;
    }

    public void EndIncrementalQueueMutation()
    {
        if (incrementalQueueMutationDepth > 0)
            incrementalQueueMutationDepth--;
    }

    public bool IsIncrementalQueueMutationActive => incrementalQueueMutationDepth > 0;

    public void RefreshAfterIncrementalQueueMutation()
    {
        // Refunds alter backpack counts and therefore recipe availability/requirements. Refresh
        // those data surfaces explicitly without rebuilding every child in the crafting window.
        GetChildByType<XUiC_RebirthCraftingInventory>()?.RefreshAuthoritativePresentation(true);
        // RecipeDetails.RefreshNow already refreshes actions, requirements and Expected Outcome.
        GetChildByType<XUiC_RebirthCraftingRecipeDetails>()?.RefreshNow();
    }

    public override void OnOpen()
    {
        AdvanceCraftIntentEpoch();
        ActiveInstance = this;
        // Tab/native inventory routing opens the full Crafting surface.
        surfaceMode = SurfaceMode.Crafting;
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Crafting);

        // Open state must exist before child controllers receive OnOpen so the custom catalogue
        // can establish its selected Recipe mode without being reset afterward.
        Coordinator?.Open();

        // Capture the pre-window cursor state before native group opening can touch it.
        CaptureInputStateBeforeOpen();

        // XUiC_CraftingWindowGroup.OnOpen remains safe: the Rebirth RecipeList exists for native
        // behavioral discovery, but CategoryList is intentionally absent so native Basics/pager
        // geometry is never initialized. The hidden windowpaging route host remains functional.
        // The paging host opens its remembered page in OnOpen. Select Crafting
        // before base opens that host, or it can reopen the previous Map/Players page.
        xui.FindWindowGroupByName("windowpaging")?.GetChildByType<XUiC_WindowSelector>()?.SetSelected("crafting");
        base.OnOpen();
        AcquireInputOwnership();
        ResolvePagingHeader();
        HideNativePagingHeader();
        HideGameplayHudOverlays();
        nextHudTrace = 0f;
        nextHudSuppressionCheck = Time.realtimeSinceStartup + 0.25f;
        hudReapplyCount = 0;
        TraceHudSnapshot("open");
        nextLayoutCheck = 0f;
        layoutAuditRequested = true;
        layoutService?.Apply(true);
        TracePresentationGeometry("open");
        RunLayoutAudit(true);
    }

    public override void OnClose()
    {
        AdvanceCraftIntentEpoch();
        if (ReferenceEquals(ActiveInstance, this)) ActiveInstance = null;
        if (xui?.DragAndDropWindow != null)
            xui.DragAndDropWindow.InMenu = xui.playerUI.windowManager.IsWindowOpen("windowpaging");
        RestoreNativePagingHeader();
        Coordinator?.Close();
        ReleaseFocusedTextInput();
        try
        {
            base.OnClose();
        }
        finally
        {
            try { RestoreGameplayHudOverlays(); }
            finally { ReleaseInputOwnership(); }
        }
    }

    public override void Update(float dt)
    {
        base.Update(dt);
        if (!State.IsOpen || layoutService == null)
            return;

        // The suppressed HUD roots are inactive while Crafting is open, so there is no reason to
        // fight native visibility writers every frame. A low-frequency watchdog catches any system
        // that explicitly reactivates one.
        if (Time.realtimeSinceStartup >= nextHudSuppressionCheck)
        {
            nextHudSuppressionCheck = Time.realtimeSinceStartup + 0.25f;
            MaintainGameplayHudSuppression();
        }
        if (HandleCancelOrBack())
            return;

        if (Time.realtimeSinceStartup >= nextLayoutCheck)
        {
            nextLayoutCheck = Time.realtimeSinceStartup + 0.20f;
            if (layoutService.Apply(false))
            {
                layoutAuditRequested = true;
                TracePresentationGeometry("layout-change");
            }
        }

        // Layout audit is diagnostic and relatively expensive; run it on open or after a real
        // geometry change/request, never as an idle 5 Hz polling loop.
        if (layoutAuditRequested)
            RunLayoutAudit(false);
    }


    public void RequestLayoutAudit()
    {
        layoutAuditRequested = true;
    }

    public string GetLayoutAuditReport()
    {
        return State.LastLayoutAuditReport ?? string.Empty;
    }

    private void RunLayoutAudit(bool forceLog)
    {
        layoutAuditRequested = false;
        // Audit collection is optional; geometry application and normal input stay outside this gate.
        if (!RebirthDiagnosticPolicy.MayPrepare(RebirthPersonalCraftingState.LayoutDebugEnabled || RebirthLogSettings.CraftingUiLoggingEnabled))
            return;
        RebirthPersonalCraftingLayoutAudit.Result result = RebirthPersonalCraftingLayoutAudit.Evaluate(this);

        // Outer geometry may change only when the XUi screen size changes. Interaction states such
        // as item context, recipe selection, filters, batch count and queue activity may not mutate it.
        bool sameScreen = State.LastLayoutAuditScreen.x == State.ScreenSize.x &&
                          State.LastLayoutAuditScreen.y == State.ScreenSize.y;
        bool sameSurface = hasAuditSurfaceBaseline && lastAuditInventoryMode == IsInventoryOnlyMode;
        bool unexpectedGeometryChange = sameScreen && sameSurface && State.LastLayoutAuditFingerprint != 0 &&
                                        State.LastLayoutAuditFingerprint != result.Fingerprint;
        if (unexpectedGeometryChange)
        {
            result.Passed = false;
            result.Report += " errors=outer geometry changed without screen-size change previousFingerprint="
                + State.LastLayoutAuditFingerprint + " interaction={" + State.InteractionSummary + "}";
        }

        bool changed = State.LastLayoutAuditPassed != result.Passed ||
                       State.LastLayoutAuditReport != result.Report;
        State.LastLayoutAuditPassed = result.Passed;
        State.LastLayoutAuditReport = result.Report;
        State.LastLayoutAuditFingerprint = result.Fingerprint;
        State.LastLayoutAuditScreen = State.ScreenSize;
        if (changed) State.LayoutAuditRevision++;
        if (!result.Passed && changed) State.LayoutAuditFailureCount++;

        string logLine = "[REBIRTH Crafting LayoutAudit] " + result.Report + " state={" + State.InteractionSummary + "}";
        if (!result.Passed)
        {
            if (lastLoggedAudit != logLine)
                Log.Error(logLine);
        }
        else if (forceLog || (RebirthPersonalCraftingState.LayoutDebugEnabled && lastLoggedAudit != logLine))
        {
            Log.Out(logLine);
        }
        if (!string.IsNullOrEmpty(logLine)) lastLoggedAudit = logLine;
        lastAuditInventoryMode = IsInventoryOnlyMode;
        hasAuditSurfaceBaseline = true;
    }

    public void ShowInventorySurface()
    {
        SetSurfaceMode(SurfaceMode.Crafting);
    }

    public void ShowCraftingSurface()
    {
        SetSurfaceMode(SurfaceMode.Crafting);
    }

    private void SetSurfaceMode(SurfaceMode mode)
    {
        if (surfaceMode == mode)
        {
            GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(
                mode == SurfaceMode.Inventory
                    ? RebirthCraftingNavigationService.Destination.Crafting
                    : RebirthCraftingNavigationService.Destination.Crafting);
            return;
        }

        surfaceMode = mode;
        GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(
            mode == SurfaceMode.Inventory
                ? RebirthCraftingNavigationService.Destination.Crafting
                : RebirthCraftingNavigationService.Destination.Crafting);

        // This is a deliberate outer-layout state transition, not an interaction-driven geometry
        // mutation. Force one settled layout pass and establish a new audit baseline for the mode.
        hasAuditSurfaceBaseline = false;
        layoutService?.Apply(true);

        // Selected Item remains authoritative across the internal tab switch, but its geometry and
        // empty-state ownership differ between compact Inventory and full Crafting. Apply it after
        // layout so the mode-specific Requirements visibility wins over layout staging.
        GetChildByType<XUiC_RebirthCraftingItemContext>()?.ApplySurfaceMode();
        layoutAuditRequested = true;
        TracePresentationGeometry(mode == SurfaceMode.Inventory ? "surface-inventory" : "surface-crafting");

        CursorControllerAbs cursor = xui != null && xui.playerUI != null ? xui.playerUI.CursorController : null;
        if (cursor != null && PlatformManager.NativePlatform.Input.CurrentInputStyle != PlayerInputManager.InputStyle.Keyboard)
        {
            XUiController target = GetChildById(mode == SurfaceMode.Inventory
                ? "btnRebirthCraftingTabInventory"
                : "btnRebirthCraftingTabCrafting");
            if (target?.ViewComponent != null) cursor.SetNavigationTarget(target.ViewComponent);
        }
    }

    private void CaptureInputStateBeforeOpen()
    {
        inputOwnershipActive = false;
        lastCancelFrame = -1;
        CursorControllerAbs cursor = xui != null && xui.playerUI != null
            ? xui.playerUI.CursorController
            : null;
        cursorHiddenBeforeOpen = cursor != null && cursor.GetCursorHidden();
    }

    private void AcquireInputOwnership()
    {
        if (xui == null || xui.playerUI == null)
            return;

        // Keep explicit cursor/navigation ownership. The public crafting window still has the
        // native GUI action set and cursor area, so native window input blocks gameplay without
        // changing PlayerMoveController's persistent SetControllableOverride state.
        windowGroup.isEscClosable = false;

        CursorControllerAbs cursor = xui.playerUI.CursorController;
        if (cursor != null)
        {
            cursor.Locked = false;
            cursor.HoverTarget = null;
            XUiController root = GetChildById("rebirthPersonalCraftingRoot");
            cursor.SetNavigationLockView(root != null ? root.ViewComponent : (XUiView)null);
            cursor.SetCursorHidden(false);

            // Controller users enter on the selected Crafting tab. Keyboard/mouse users retain
            // free pointer control and can click directly into Search or the Backpack.
            if (PlatformManager.NativePlatform.Input.CurrentInputStyle != PlayerInputManager.InputStyle.Keyboard)
            {
                XUiController initial = GetChildById(IsInventoryOnlyMode
                    ? "btnRebirthCraftingTabInventory"
                    : "btnRebirthCraftingTabCrafting");
                if (initial != null && initial.ViewComponent != null)
                    cursor.SetNavigationTarget(initial.ViewComponent);
            }
        }

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null)
        {
            RebirthConsoleInputGuardRuntime.ClearGameplayMovement(player);
        }

        inputOwnershipActive = true;
        TraceInputOwnership("open-native-owner");
    }

    private bool HandleCancelOrBack()
    {
        // The console owns Escape/Cancel while open, including the frame in which it closes.
        // Do not let the same key also dismiss the item context or the underlying inventory.
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
            return false;
        if (xui == null || xui.playerUI == null || xui.playerUI.playerInput == null)
            return false;

        bool cancel = Input.GetKeyDown(KeyCode.Escape) ||
                      xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased ||
                      xui.playerUI.playerInput.GUIActions.Cancel.WasReleased;
        if (!cancel || lastCancelFrame == Time.frameCount)
            return false;
        lastCancelFrame = Time.frameCount;

        // Back is layered: dismiss the transient item context first, then close Crafting. This is
        // deliberately independent from GUIWindowManager's default ESC focus handling so a focused
        // Search field can never require an extra Escape press.
        XUiC_RebirthCraftingItemContext context = GetChildByType<XUiC_RebirthCraftingItemContext>();
        if (context != null && context.IsContextVisible)
        {
            context.ClearSelection();
            ReleaseFocusedTextInput();
            return true;
        }

        ReleaseFocusedTextInput();
        xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
        return true;
    }

    private void ReleaseFocusedTextInput()
    {
        UIInput selected = UIInput.selection;
        XUiController root = GetChildById("rebirthPersonalCraftingRoot");
        Transform rootTransform = root?.ViewComponent?.UiTransform;
        // Never clear an unrelated text field during an external route/console transition.
        if (selected == null || rootTransform == null || !selected.transform.IsChildOf(rootTransform))
            return;
        selected.RemoveFocus();
        selected.isSelected = false;
    }

    private void ReleaseInputOwnership()
    {
        if (!inputOwnershipActive)
            return;
        inputOwnershipActive = false;
        if (xui == null || xui.playerUI == null)
            return;

        ReleaseFocusedTextInput();
        RebirthConsoleInputGuardRuntime.ClearGameplayMovement(xui.playerUI.entityPlayer);

        // A console layered over this window retains its own cursor. Otherwise remove only this
        // window's navigation state. No SetControllable(true/false), global action-stack reset,
        // deferred recovery, or cached IsModalWindowOpen decision belongs in this close callback.
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        if (cursor != null && !RebirthConsoleInputGuardRuntime.IsConsoleOpen())
        {
            cursor.HoverTarget = null;
            cursor.SetNavigationTarget((XUiView)null);
            cursor.SetNavigationLockView((XUiView)null);
            cursor.SetCursorHidden(cursorHiddenBeforeOpen);
            cursor.Locked = false;
            cursor.ResetNavigationTarget();
        }

        // GUIWindowManager.Close has set isShowing=false, but has NOT yet popped this window's
        // native action set. Leave that stack intact for its matching native removal after return.
        TraceInputOwnership("close-native-pop-pending");
    }

    public void PrepareForExternalRoute()
    {
        // Native routing performs the old-window close and new-window open synchronously.
        // Only release our text focus here; do not leave a persistent gameplay-restore flag.
        ReleaseFocusedTextInput();
    }

    private void TraceInputOwnership(string reason)
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled || xui?.playerUI == null)
            return;
        ActionSetManager stack = xui.playerUI.ActionSetManager;
        string top = stack == null || stack.Empty ? "<empty>" : stack.Top.GetType().Name;
        Log.Out("[REBIRTH Crafting InputTrace] PC127 reason=" + reason
            + " surface=" + surfaceMode + " top=" + top
            + " controlOverrideWrites=0 stackResets=0");
    }

    public string GetChunkKInputReport()
    {
        if (xui == null || xui.playerUI == null)
            return "input=<no-player-ui>";
        GUIWindowManager manager = xui.playerUI.windowManager;
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        XUiC_TextInput search = GetChildById("rebirthCraftingRecipeSearch") as XUiC_TextInput;
        return "open=" + State.IsOpen +
               " ownership=" + inputOwnershipActive +
               " inputActive=" + (manager != null && manager.IsInputActive()) +
               " modal=" + (manager != null && manager.IsModalWindowOpen()) +
               " searchFocused=" + (search != null && search.IsSelected) +
               " uiSelection=" + (UIInput.selection != null) +
               " cursorHidden=" + (cursor != null && cursor.GetCursorHidden()) +
               " cursorLocked=" + (cursor != null && cursor.Locked) +
               " screen=" + State.ScreenSize.x + "x" + State.ScreenSize.y +
               " layout=" + (State.LastLayoutAuditPassed ? "PASS" : "FAIL");
    }

    /// <summary>
    /// Chunk L consolidated debug snapshot. This is read-only and intentionally reports both
    /// layout and native-ownership invariants so one capture is enough for live acceptance logs.
    /// </summary>
    public string GetFinalAcceptanceDebugReport()
    {
        const int columns = RebirthCraftingInventoryBridge.Columns;
        const int visibleRows = RebirthCraftingInventoryBridge.VisibleRows;
        XUiC_RebirthCraftingInventoryScroll inventoryScroll = GetChildByType<XUiC_RebirthCraftingInventoryScroll>();
        XUiC_RebirthCraftingRequirements requirements = GetChildByType<XUiC_RebirthCraftingRequirements>();
        int physical = Math.Max(0, State.BackpackPhysicalSlots);
        int totalRows = Math.Max(0, (physical + columns - 1) / columns);
        int firstRow = inventoryScroll != null ? inventoryScroll.GetFirstVisibleRow() : 0;
        int lastRow = totalRows <= 0 ? -1 : Math.Min(totalRows - 1, firstRow + visibleRows - 1);
        int requirementCount = requirements != null && requirements.CurrentRequirements != null
            ? requirements.CurrentRequirements.Count
            : 0;

        string[] forbiddenIds =
        {
            "windowNonPagingHeader", "windowCraftingList", "craftingInfoPanel", "backpack",
            "itemInfoPanel", "emptyInfoPanel", "windowCraftingQueue", "rebirthCraftingQueueNonStation"
        };
        int forbiddenFound = 0;
        StringBuilder forbiddenNames = new StringBuilder();
        for (int i = 0; i < forbiddenIds.Length; i++)
        {
            if (GetChildById(forbiddenIds[i]) == null) continue;
            if (forbiddenNames.Length > 0) forbiddenNames.Append(',');
            forbiddenNames.Append(forbiddenIds[i]);
            forbiddenFound++;
        }

        GUIWindowManager manager = xui != null && xui.playerUI != null ? xui.playerUI.windowManager : null;
        bool craftingOpen = manager != null && manager.IsWindowOpen("crafting");
        bool backpackOpen = manager != null && manager.IsWindowOpen("backpack");
        bool pagingOpen = manager != null && manager.IsWindowOpen("windowpaging");
        string progression = RebirthSurvivorMode.IsEnabledForCurrentWorld() ? "Rebirth" : "BaseGame";

        StringBuilder sb = new StringBuilder(1024);
        sb.Append("[REBIRTH Crafting FinalAcceptance]")
          .Append(" progression=").Append(progression)
          .Append(" groups={crafting:").Append(craftingOpen)
          .Append(",backpack:").Append(backpackOpen)
          .Append(",windowpaging:").Append(pagingOpen).Append('}')
          .Append(" safeRoot={pos:").Append(State.RootPosition.x).Append(',').Append(State.RootPosition.y)
          .Append(" size:").Append(State.RootSize.x).Append('x').Append(State.RootSize.y).Append('}')
          .Append(" screen=").Append(State.ScreenSize.x).Append('x').Append(State.ScreenSize.y)
          .Append(" inventory={cols:").Append(columns)
          .Append(" physical:").Append(physical)
          .Append(" unencumbered:").Append(State.BackpackUnencumberedSlots)
          .Append(" totalRows:").Append(totalRows)
          .Append(" visibleRows:").Append(firstRow).Append('-').Append(lastRow)
          .Append(" rowOffset:").Append(firstRow).Append('}')
          .Append(" recipe=").Append(string.IsNullOrEmpty(State.SelectedRecipeName) ? "<none>" : State.SelectedRecipeName)
          .Append(" bagSlot=").Append(State.SelectedInventorySlot)
          .Append(" batch=").Append(State.BatchCount)
          .Append(" requirements=").Append(requirementCount)
          .Append(" queue=").Append(State.QueueActiveCount).Append('/').Append(State.QueueCapacity)
          .Append(" forbiddenNativeChildren=").Append(forbiddenFound);
        if (forbiddenFound > 0) sb.Append('[').Append(forbiddenNames).Append(']');
        sb.Append(" input={").Append(GetChunkKInputReport()).Append('}')
          .Append(" layout={").Append(GetLayoutAuditReport()).Append('}');
        return sb.ToString();
    }


    /// <summary>
    /// Personal Crafting leaves the central toolbelt/vital bars visible, but suppresses the
    /// informational HUD that otherwise shows through this near-full-screen surface. Native HUD
    /// controllers can re-enable themselves during Update, so their root GameObjects are made
    /// inactive while Crafting is open, watched at low frequency, and restored exactly on close.
    /// </summary>
    private void HideGameplayHudOverlays()
    {
        hudVisibilityBeforeOpen.Clear();
        hudEnabledBeforeOpen.Clear();
        hudScaleBeforeOpen.Clear();
        hudActiveBeforeOpen.Clear();
        hudDormantBeforeOpen.Clear();
        if (xui == null)
            return;

        XUiController toolbelt = xui.FindWindowGroupByName("toolbelt");
        string[] ids =
        {
            "HUDLeftStatBars",
            "HUDRightStatBars",
            "windowEntering",
            "windowLocation",
            "windowQuestTracker",
            "windowRecipeTracker",
            "windowGroupBars",
            "rebirthPartyCompanionHud",
            "rebirthHeatMapHud",
            "rebirthStudyHud",
            "rebirthOreSenseHud",
            "windowCompass"
        };
        for (int i = 0; toolbelt != null && i < ids.Length; i++)
        {
            XUiController target = toolbelt.GetChildById(ids[i]);
            CaptureAndHide(target, ids[i]);
        }

        // These typed references are more reliable than XML ids across 3.2 revisions.
        CaptureAndHide(xui.BuffPopoutList, "xui.BuffPopoutList");

        // XUi.QuestTracker is an XUiM_Quest model in V3.2 b10, not an XUiController.
        // The visible quest tracker is already captured above through windowQuestTracker; keep
        // the model in diagnostics only so the HUD suppression path remains type-correct.
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting HUDTrace] questModel resolved=" + (xui.QuestTracker != null)
                + " type=" + (xui.QuestTracker != null ? xui.QuestTracker.GetType().Name : "<null>"));

        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            TraceVisibleControllerTree(toolbelt, "toolbelt", 0, 3, 0);
    }

    private void CaptureAndHide(XUiController controller, string source)
    {
        if (controller == null || controller.ViewComponent == null)
        {
            if (RebirthLogSettings.CraftingUiLoggingEnabled)
                Log.Out("[REBIRTH Crafting HUDTrace] capture source=" + (source ?? "<unknown>") + " resolved=false");
            return;
        }
        if (!hudVisibilityBeforeOpen.ContainsKey(controller))
            hudVisibilityBeforeOpen.Add(controller, controller.ViewComponent.IsVisible);
        if (!hudEnabledBeforeOpen.ContainsKey(controller))
            hudEnabledBeforeOpen.Add(controller, controller.ViewComponent.Enabled);
        if (!hudScaleBeforeOpen.ContainsKey(controller) && controller.ViewComponent.UiTransform != null)
            hudScaleBeforeOpen.Add(controller, controller.ViewComponent.UiTransform.localScale);
        GameObject targetObject = controller.ViewComponent.UiTransform != null
            ? controller.ViewComponent.UiTransform.gameObject : null;
        if (!hudActiveBeforeOpen.ContainsKey(controller) && targetObject != null)
            hudActiveBeforeOpen.Add(controller, targetObject.activeSelf);
        bool dormantBefore;
        if (!hudDormantBeforeOpen.ContainsKey(controller) && TryGetControllerDormant(controller, out dormantBefore))
            hudDormantBeforeOpen.Add(controller, dormantBefore);
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting HUDTrace] capture source=" + (source ?? "<unknown>")
                + " id=" + ControllerId(controller)
                + " type=" + controller.GetType().Name
                + " beforeVisible=" + controller.ViewComponent.IsVisible
                + " beforeEnabled=" + controller.ViewComponent.Enabled);
        // Disabling the view prevents later HUD-controller updates in the same XUi frame from
        // immediately turning the overlay visible again. Exact pre-open state is restored on close.
        controller.ViewComponent.IsVisible = false;
        controller.ViewComponent.Enabled = false;
        SetControllerDormant(controller, true);
        // Several V3.2 HUD controllers force IsVisible back to true later in the same frame.
        // A zero transform scale is not touched by those visibility refreshes, so it provides a
        // stable visual suppression layer while preserving the controller for exact restoration.
        if (controller.ViewComponent.UiTransform != null)
        {
            controller.ViewComponent.UiTransform.localScale = Vector3.zero;
            controller.ViewComponent.UiTransform.gameObject.SetActive(false);
        }
    }

    private void MaintainGameplayHudSuppression()
    {
        int visibleAgain = 0;
        foreach (KeyValuePair<XUiController, bool> pair in hudVisibilityBeforeOpen)
        {
            XUiController controller = pair.Key;
            if (controller == null || controller.ViewComponent == null)
                continue;

            GameObject targetObject = controller.ViewComponent.UiTransform != null
                ? controller.ViewComponent.UiTransform.gameObject : null;
            bool active = targetObject != null && targetObject.activeSelf;
            bool scaleSuppressed = controller.ViewComponent.UiTransform == null
                || controller.ViewComponent.UiTransform.localScale == Vector3.zero;

            // Several HUD controllers keep writing IsVisible/Enabled even while their GameObject is
            // inactive. Those writes are harmless visually, and fighting them caused 12 needless
            // suppression writes per second in PC114. Treat inactive + zero-scale as stable.
            if (!active && scaleSuppressed)
                continue;

            visibleAgain++;
            hudReapplyCount++;
            hudReappearSinceTrace++;
            if (controller.ViewComponent.IsVisible)
                controller.ViewComponent.IsVisible = false;
            if (controller.ViewComponent.Enabled)
                controller.ViewComponent.Enabled = false;
            if (controller.ViewComponent.UiTransform != null)
            {
                controller.ViewComponent.UiTransform.localScale = Vector3.zero;
                controller.ViewComponent.UiTransform.gameObject.SetActive(false);
            }
        }

        if (RebirthLogSettings.CraftingUiLoggingEnabled && Time.realtimeSinceStartup >= nextHudTrace)
        {
            nextHudTrace = Time.realtimeSinceStartup + 1.0f;
            Log.Out("[REBIRTH Crafting HUDTrace] maintain captured=" + hudVisibilityBeforeOpen.Count
                + " visibleAgainThisFrame=" + visibleAgain
                + " reappliedSinceLastTrace=" + hudReappearSinceTrace
                + " totalReapplies=" + hudReapplyCount);
            hudReappearSinceTrace = 0;
        }
    }


    // V3.2's public modding surface does not expose the controller dormant flag even though the
    // runtime controller still carries that flag. Access it reflectively so this project compiles
    // against the supported Assembly-CSharp API while retaining the performance suppression when
    // the runtime field exists. Reflection is cached per concrete controller type and is only used
    // on open/close plus the 4 Hz HUD watchdog.
    private static FieldInfo ResolveDormantField(Type controllerType)
    {
        if (controllerType == null)
            return null;

        FieldInfo cached;
        if (dormantFieldCache.TryGetValue(controllerType, out cached))
            return cached;

        Type current = controllerType;
        while (current != null)
        {
            FieldInfo field = current.GetField("IsDormant",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null)
            {
                field = current.GetField("isDormant",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            }
            if (field != null && field.FieldType == typeof(bool))
            {
                dormantFieldCache[controllerType] = field;
                return field;
            }
            current = current.BaseType;
        }

        dormantFieldCache[controllerType] = null;
        return null;
    }

    private static bool TryGetControllerDormant(XUiController controller, out bool dormant)
    {
        dormant = false;
        if (controller == null)
            return false;

        FieldInfo field = ResolveDormantField(controller.GetType());
        if (field == null)
            return false;

        try
        {
            object value = field.GetValue(controller);
            if (value is bool)
            {
                dormant = (bool)value;
                return true;
            }
        }
        catch
        {
            // GameObject deactivation + disabled view remain the safe fallback.
        }
        return false;
    }

    private static void SetControllerDormant(XUiController controller, bool dormant)
    {
        if (controller == null)
            return;

        FieldInfo field = ResolveDormantField(controller.GetType());
        if (field == null)
            return;

        try
        {
            field.SetValue(controller, dormant);
        }
        catch
        {
            // Do not let an inaccessible runtime field break HUD restoration/suppression.
        }
    }

    private void RestoreGameplayHudOverlays()
    {
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH Crafting HUDTrace] restore captured=" + hudVisibilityBeforeOpen.Count
                + " totalReapplies=" + hudReapplyCount);
        foreach (KeyValuePair<XUiController, bool> pair in hudVisibilityBeforeOpen)
        {
            if (pair.Key == null || pair.Key.ViewComponent == null)
                continue;
            Vector3 scale;
            if (hudScaleBeforeOpen.TryGetValue(pair.Key, out scale) && pair.Key.ViewComponent.UiTransform != null)
                pair.Key.ViewComponent.UiTransform.localScale = scale;
            pair.Key.ViewComponent.IsVisible = pair.Value;
            bool enabled;
            if (hudEnabledBeforeOpen.TryGetValue(pair.Key, out enabled))
                pair.Key.ViewComponent.Enabled = enabled;
            bool dormant;
            if (hudDormantBeforeOpen.TryGetValue(pair.Key, out dormant))
                SetControllerDormant(pair.Key, dormant);
            bool active;
            if (hudActiveBeforeOpen.TryGetValue(pair.Key, out active) && pair.Key.ViewComponent.UiTransform != null)
                pair.Key.ViewComponent.UiTransform.gameObject.SetActive(active);
        }
        hudVisibilityBeforeOpen.Clear();
        hudEnabledBeforeOpen.Clear();
        hudScaleBeforeOpen.Clear();
        hudActiveBeforeOpen.Clear();
        hudDormantBeforeOpen.Clear();
    }

    private void TraceHudSnapshot(string reason)
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled)
            return;
        XUiController toolbelt = xui != null ? xui.FindWindowGroupByName("toolbelt") : null;
        Log.Out("[REBIRTH Crafting HUDTrace] snapshot reason=" + (reason ?? "unknown")
            + " toolbelt=" + (toolbelt != null)
            + " captured=" + hudVisibilityBeforeOpen.Count);
    }

    private int TraceVisibleControllerTree(XUiController controller, string path, int depth, int maxDepth, int emitted)
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled || controller == null || depth > maxDepth || emitted >= 80)
            return emitted;

        if (controller.ViewComponent != null && controller.ViewComponent.IsVisible)
        {
            Log.Out("[REBIRTH Crafting HUDTrace] visible path=" + path
                + " id=" + ControllerId(controller)
                + " type=" + controller.GetType().Name);
            emitted++;
        }

        if (controller.Children == null)
            return emitted;
        for (int i = 0; i < controller.Children.Count && emitted < 80; i++)
        {
            XUiController child = controller.Children[i];
            if (child == null) continue;
            emitted = TraceVisibleControllerTree(child, path + "/" + ControllerId(child), depth + 1, maxDepth, emitted);
        }
        return emitted;
    }

    private static string ControllerId(XUiController controller)
    {
        if (controller == null || controller.ViewComponent == null || string.IsNullOrEmpty(controller.ViewComponent.ID))
            return "<no-id>";
        return controller.ViewComponent.ID;
    }

    private void TracePresentationGeometry(string reason)
    {
        if (!RebirthLogSettings.CraftingUiLoggingEnabled)
            return;
        XUiController icon = GetChildById("rebirthCraftingTabCraftingIcon");
        XUiController label = GetChildById("rebirthCraftingTabCraftingLabel");
        XUiController outcomeMetrics = GetChildById("rebirthCraftingOutcomeMetricsPanel");
        XUiController outcomeResults = GetChildById("rebirthCraftingOutcomeResultsPanel");
        XUiController quick = GetChildById("btnRebirthCraftingInventoryQuickStack");
        XUiController companions = GetChildById("btnRebirthCraftingInventoryCompanions");
        Log.Out("[REBIRTH Crafting LayoutTrace] reason=" + (reason ?? "unknown")
            + " tabIcon=" + RectText(icon)
            + " tabLabel=" + RectText(label)
            + " outcomeMetrics=" + RectText(outcomeMetrics)
            + " outcomeResults=" + RectText(outcomeResults)
            + " quickStack=" + RectText(quick)
            + " companions=" + RectText(companions));
    }

    private static string RectText(XUiController controller)
    {
        if (controller == null || controller.ViewComponent == null)
            return "<missing>";
        Vector2i p = controller.ViewComponent.Position;
        Vector2i z = controller.ViewComponent.Size;
        return p.x + "," + p.y + ":" + z.x + "x" + z.y
            + ":vis=" + controller.ViewComponent.IsVisible
            + ":enabled=" + controller.ViewComponent.Enabled;
    }

    private void ResolvePagingHeader()
    {
        if (xui == null)
            return;

        XUiController group = xui.FindWindowGroupByName("windowpaging");
        pagingHeader = group != null
            ? group.GetChildById("windowPagingHeader")
            : null;
    }

    private void HideNativePagingHeader()
    {
        if (pagingHeader == null || pagingHeader.ViewComponent == null)
            return;

        pagingHeaderWasVisibleBeforeOpen = pagingHeader.ViewComponent.IsVisible;
        pagingHeader.ViewComponent.IsVisible = false;
    }

    private void RestoreNativePagingHeader()
    {
        if (pagingHeader == null || pagingHeader.ViewComponent == null)
            return;

        pagingHeader.ViewComponent.IsVisible = pagingHeaderWasVisibleBeforeOpen;
    }
}
