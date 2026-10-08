using System;
using Platform;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class XUiC_RebirthCompanions : XUiController
{
    private enum Tab : byte { Overview, Inventory, Stats, Behavior }
    private const int VisibleCompanions = 6;
    private const int VisibleCommands = 9;
    private const int VisibleInventory = 8;
    private const int CompanionTrackHeight = 455;
    private const int CommandTrackHeight = 405;
    private const int InventoryTrackHeight = 397;

    private sealed class CompanionRowView
    {
        public XUiController Root, Select;
        public XUiV_Sprite Selection, Icon, DirectionArrow;
        public XUiV_Label Name, Type, Level, Status, Distance;
    }
    private sealed class CommandRowView
    {
        public XUiController Root, Select;
        public XUiV_Sprite Icon;
        public XUiV_Label Name;
        public bool IconStateInitialized;
        public string IconAtlas = string.Empty;
        public string IconSprite = string.Empty;
        public UIBasicSprite.Flip IconFlip;
        public bool EnabledState;
    }
    private sealed class InventoryRowView
    {
        public XUiController Root, Lock, Use;
        public XUiV_Sprite Icon, LockIcon;
        public XUiC_ToggleButton Include;
        public XUiV_Label Name, Slot;
        public XUiC_ComboBoxInt Count;
    }

    private readonly CompanionRowView[] companionRows = new CompanionRowView[VisibleCompanions];
    private readonly CommandRowView[] commandRows = new CommandRowView[VisibleCommands];
    private readonly InventoryRowView[] inventoryRows = new InventoryRowView[VisibleInventory];
    private readonly XUiController[] tabButtons = new XUiController[4];
    private readonly HashSet<string> excludedInventory = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> quantityOverrides = new Dictionary<string, int>(StringComparer.Ordinal);

    private XUiController panelOverview, panelInventory, panelStats, panelBehavior;
    private XUiV_Sprite selectedIcon;
    private XUiV_Label selectedName, selectedType, selectedStatusLabel, selectedStatusValue, selectedHealth, selectedDistance, selectedOrder, overviewDescription;
    private XUiV_Label statsText, behaviorText, inventorySummary, feedback;
    private XUiV_Label dogRenameLabel;
    private XUiC_TextInput dogRenameText;
    private XUiController btnDogRename;
    private XUiController dismissConfirmOverlay, btnDismissConfirm, btnDismissCancel;
    private XUiV_Label dismissConfirmText;
    private string pendingDismissId = string.Empty;
    private string pendingDismissName = string.Empty;
    private XUiController companionScrollTrack, companionScrollThumbControl, commandScrollTrack, commandScrollThumbControl, inventoryScrollTrack, inventoryScrollThumbControl;
    private XUiV_Button companionScrollThumb, commandScrollThumb, inventoryScrollThumb;
    private RebirthCompanionSnapshot snapshot = new RebirthCompanionSnapshot();
    private string selectedId = string.Empty;
    private Tab currentTab;
    private int companionOffset, commandOffset, inventoryOffset;
    private bool suppressSelection, suppressQuantity, requestPending;
    private int selectionVersion;
    private float refreshTimer;
    private bool wasCursorHidden;
    private bool focusRenameRequested;

    public override void Init()
    {
        base.Init();
        panelOverview = GetChildById("panelOverview");
        panelInventory = GetChildById("panelInventory");
        panelStats = GetChildById("panelStats");
        panelBehavior = GetChildById("panelBehavior");
        selectedIcon = GetChildById("selectedIcon")?.ViewComponent as XUiV_Sprite;
        selectedName = Label("selectedName"); selectedType = Label("selectedType");
        selectedStatusLabel = Label("selectedStatusLabel"); selectedStatusValue = Label("selectedStatusValue");
        selectedHealth = Label("selectedHealth"); selectedDistance = Label("selectedDistance"); selectedOrder = Label("selectedOrder");
        overviewDescription = Label("overviewDescription"); statsText = Label("statsText"); behaviorText = Label("behaviorText");
        inventorySummary = Label("inventorySummary"); feedback = Label("companionFeedback");
        dogRenameLabel = Label("dogRenameLabel");
        dogRenameText = GetChildById("dogRenameText") as XUiC_TextInput;
        btnDogRename = GetChildById("btnDogRename");
        if (btnDogRename != null) btnDogRename.OnPress += delegate { RenameSelectedCompanion(); };

        dismissConfirmOverlay = GetChildById("dismissConfirmOverlay");
        dismissConfirmText = Label("dismissConfirmText");
        btnDismissConfirm = GetChildById("btnDismissConfirm");
        btnDismissCancel = GetChildById("btnDismissCancel");
        if (btnDismissConfirm != null) btnDismissConfirm.OnPress += delegate { ConfirmDismiss(); };
        if (btnDismissCancel != null) btnDismissCancel.OnPress += delegate { CancelDismissConfirmation(); };
        CancelDismissConfirmation();

        string[] tabIds = { "btnTabOverview", "btnTabInventory", "btnTabStats", "btnTabBehavior" };
        for (int i = 0; i < tabIds.Length; i++)
        {
            tabButtons[i] = GetChildById(tabIds[i]);
            int captured = i;
            if (tabButtons[i] != null) tabButtons[i].OnPress += delegate { SelectTab((Tab)captured); };
        }

        for (int i = 0; i < companionRows.Length; i++)
        {
            companionRows[i] = GetCompanionRow("companionRow" + i.ToString("00"));
            CompanionRowView row = companionRows[i];
            if (row == null) continue;
            int captured = i;
            WireCompanionRowPress(row.Root, captured);
            WireCompanionRowPress(row.Select, captured);
            WireCompanionRowPress(row.Icon?.Controller, captured);
            WireCompanionRowPress(row.Name?.Controller, captured);
            WireCompanionRowPress(row.Type?.Controller, captured);
            WireCompanionRowPress(row.Level?.Controller, captured);
            WireCompanionRowPress(row.Status?.Controller, captured);
            WireCompanionRowPress(row.Distance?.Controller, captured);
            WireCompanionRowPress(row.DirectionArrow?.Controller, captured);
            WireScroll(row.Root, CompanionScroll); WireScroll(row.Select, CompanionScroll);
            WireScroll(row.Name?.Controller, CompanionScroll); WireScroll(row.Type?.Controller, CompanionScroll);
            WireScroll(row.Level?.Controller, CompanionScroll); WireScroll(row.Status?.Controller, CompanionScroll); WireScroll(row.Distance?.Controller, CompanionScroll);
            WireScroll(row.DirectionArrow?.Controller, CompanionScroll);
        }

        for (int i = 0; i < commandRows.Length; i++)
        {
            commandRows[i] = GetCommandRow("commandRow" + i.ToString("00"));
            CommandRowView row = commandRows[i];
            if (row == null) continue;
            int captured = i;
            if (row.Select != null) row.Select.OnPress += delegate { ExecuteCommand(captured); };
            WireScroll(row.Root, CommandScroll); WireScroll(row.Select, CommandScroll); WireScroll(row.Name?.Controller, CommandScroll);
        }

        for (int i = 0; i < inventoryRows.Length; i++)
        {
            inventoryRows[i] = GetInventoryRow("inventoryRow" + i.ToString("00"));
            InventoryRowView row = inventoryRows[i];
            if (row == null) continue;
            int captured = i;
            if (row.Include != null)
                row.Include.OnValueChanged += delegate(XUiC_ToggleButton sender, bool value) { InventorySelectionChanged(captured, value); };
            if (row.Count != null)
            {
                row.Count.OnValueChanged += delegate(XUiController sender, long oldValue, long newValue) { InventoryCountChanged(captured, newValue); };
                WireComboScroll(row.Count.GetChildById("directvalue")); WireComboScroll(row.Count.GetChildById("back")); WireComboScroll(row.Count.GetChildById("forward"));
            }
            if (row.Lock != null)
            {
                if (row.Lock.ViewComponent != null) row.Lock.ViewComponent.EventOnPress = true;
                XUiV_Button lockButton = row.Lock.ViewComponent as XUiV_Button;
                if (lockButton != null) lockButton.Enabled = true;
                row.Lock.OnPress += delegate { ToggleInventoryLock(captured); };
            }
            if (row.Use != null) row.Use.OnPress += delegate { UseInventoryItem(captured); };
            WireScroll(row.Root, InventoryScroll); WireScroll(row.Name?.Controller, InventoryScroll); WireScroll(row.Icon?.Controller, InventoryScroll);
        }

        Wire("btnInventorySelectAll", SelectAllInventory);
        Wire("btnInventoryClearAll", ClearAllInventory);
        Wire("btnInventoryTransfer", TransferSelectedInventory);
        Wire("btnClose", Close);

        companionScrollTrack = GetChildById("companionScrollTrackInput"); companionScrollThumbControl = GetChildById("companionScrollThumb"); companionScrollThumb = companionScrollThumbControl?.ViewComponent as XUiV_Button;
        commandScrollTrack = GetChildById("commandScrollTrackInput"); commandScrollThumbControl = GetChildById("commandScrollThumb"); commandScrollThumb = commandScrollThumbControl?.ViewComponent as XUiV_Button;
        inventoryScrollTrack = GetChildById("inventoryScrollTrackInput"); inventoryScrollThumbControl = GetChildById("inventoryScrollThumb"); inventoryScrollThumb = inventoryScrollThumbControl?.ViewComponent as XUiV_Button;
        WireScroll(GetChildById("companionPaneScrollCapture"), CompanionScroll); WireScroll(companionScrollTrack, CompanionScroll); WireScroll(companionScrollThumbControl, CompanionScroll);
        WireScroll(GetChildById("commandPaneScrollCapture"), CommandScroll); WireScroll(commandScrollTrack, CommandScroll); WireScroll(commandScrollThumbControl, CommandScroll);
        WireScroll(GetChildById("inventoryPaneScrollCapture"), InventoryScroll); WireScroll(inventoryScrollTrack, InventoryScroll); WireScroll(inventoryScrollThumbControl, InventoryScroll);
        WireThumbDrag(companionScrollThumbControl, delegate(float dy) { DragScroll(ref companionOffset, snapshot.Companions.Count, companionRows.Length, CompanionTrackHeight, dy, RenderCompanions); });
        WireThumbDrag(commandScrollThumbControl, delegate(float dy) { DragScroll(ref commandOffset, snapshot.Commands.Count, commandRows.Length, CommandTrackHeight, dy, RenderCommands); });
        WireThumbDrag(inventoryScrollThumbControl, delegate(float dy) { DragScroll(ref inventoryOffset, snapshot.Inventory.Count, inventoryRows.Length, InventoryTrackHeight, dy, RenderInventory); });
    }

    public void Prepare()
    {
        Prepare(string.Empty, "Overview");
    }

    public void Prepare(string requestedSelectedId, string requestedTab)
    {
        selectedId = requestedSelectedId ?? string.Empty;
        selectionVersion++;
        focusRenameRequested = string.Equals(requestedTab, "Rename", StringComparison.OrdinalIgnoreCase);
        Tab parsed;
        currentTab = focusRenameRequested ? Tab.Stats : (Enum.TryParse(requestedTab ?? string.Empty, true, out parsed) ? parsed : Tab.Overview);
        companionOffset = commandOffset = inventoryOffset = 0;
        excludedInventory.Clear(); quantityOverrides.Clear(); snapshot = new RebirthCompanionSnapshot(); refreshTimer = 0f;
        CancelDismissConfirmation();
    }

    public override void OnOpen()
    {
        base.OnOpen(); windowGroup.isEscClosable = false;
        managementOpen = true;
        CursorControllerAbs cursor = xui.playerUI.CursorController; wasCursorHidden = cursor.GetCursorHidden(); cursor.Locked = false; cursor.SetCursorHidden(false); cursor.ResetToCenter();
        EntityPlayerLocal player = xui.playerUI.entityPlayer; if (player != null) { player.SetControllable(false); player.ClearMovementInputs(); }
        SelectTab(currentTab); RefreshSnapshot(true);
    }

    public override void OnClose()
    {
        CancelDismissConfirmation();
        managementOpen = false;
        requestGeneration++;
        RebirthCompanionSnapshotService.Cancel(snapshotCallback);
        snapshotCallback = null;
        requestPending = false;
        CursorControllerAbs cursor = xui.playerUI.CursorController;
        UIInput selectedInput = UIInput.selection;
        if (selectedInput != null)
        {
            selectedInput.RemoveFocus();
            selectedInput.isSelected = false;
        }
        cursor.HoverTarget = null;
        cursor.SetNavigationTarget((XUiView)null);
        cursor.SetNavigationLockView((XUiView)null);
        cursor.SetCursorHidden(wasCursorHidden);
        cursor.Locked = false;
        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null) player.SetControllable(true);
        base.OnClose();
        if (UIInput.selection != null) UIInput.selection.isSelected = false;
        cursor.HoverTarget = null;
        cursor.ResetNavigationTarget();
        xui.playerUI.windowManager.ResetActionSets();
    }

    private bool pagingMode;
    public override void Update(float dt)
    {
        base.Update(dt);
        if (!managementOpen) return;
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; pagingDragRemainder = 0f; }
        if (pagingNow && snapshot != null)
        {
            int oldCompanion = companionOffset, oldCommand = commandOffset, oldInventory = inventoryOffset;
            ClampOffsets();
            if (oldCompanion != companionOffset) RenderCompanions();
            if (oldCommand != commandOffset) RenderCommands();
            if (oldInventory != inventoryOffset) RenderInventory();
        }
        RebirthCompanionSnapshotService.Update();
        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null)
        {
            player.SetControllable(false);
            // Direction is deliberately client-local and relative to the player's CURRENT
            // facing, so the arrow responds immediately while the player turns instead of
            // waiting for the next server snapshot.
            UpdateDirectionArrows(player);
        }
        refreshTimer -= dt; if (refreshTimer <= 0f && !requestPending) { refreshTimer = 0.75f; RefreshSnapshot(false); }
        if (XUiUtils.HotkeysAllowedFor(viewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased || xui.playerUI.playerInput.GUIActions.Cancel.WasReleased))
        {
            if (DismissConfirmationVisible()) CancelDismissConfirmation();
            else Close();
        }
    }

    private bool managementOpen;
    private int requestGeneration;
    private Action<RebirthCompanionSnapshot> snapshotCallback;

    private void RefreshSnapshot(bool immediate)
    {
        if (!managementOpen || requestPending) return;
        if (immediate) refreshTimer = 0.75f;
        requestPending = true;
        int requestedSelectionVersion = selectionVersion;
        string requestedSelectionId = selectedId ?? string.Empty;
        int generation = ++requestGeneration;
        snapshotCallback = delegate(RebirthCompanionSnapshot value)
        {
            if (!managementOpen || generation != requestGeneration) return;
            requestPending = false;

            // If the player changed selection while this request was in flight, the response is
            // authoritative for the OLD selection and must never snap the UI back. Discard
            // it and immediately request the current selection instead.
            if (requestedSelectionVersion != selectionVersion ||
                !string.Equals(requestedSelectionId, selectedId ?? string.Empty, StringComparison.Ordinal))
            {
                RefreshSnapshot(true);
                return;
            }

            if (value != null && value.RequestFailed)
            {
                // Preserve observed identity/selection, but never expose stale mutation controls.
                snapshot.RequestFailed = true;
                snapshot.Feedback = value.Feedback;
                snapshot.Commands.Clear();
                snapshot.Inventory.Clear();
                CancelDismissConfirmation();
                excludedInventory.Clear(); quantityOverrides.Clear();
                ClampOffsets(); RenderAll();
                return;
            }
            snapshot = value ?? new RebirthCompanionSnapshot();
            if (!string.Equals(selectedId, snapshot.SelectedId, StringComparison.Ordinal))
            {
                selectedId = snapshot.SelectedId ?? string.Empty;
                selectionVersion++;
                commandOffset = inventoryOffset = 0; excludedInventory.Clear(); quantityOverrides.Clear();
            }
            ClampOffsets(); RenderAll();
        };
        RebirthCompanionSnapshotService.Request(requestedSelectionId, snapshotCallback);
    }

    private void RenderAll()
    {
        RenderTabs(); RenderCompanions(); RenderSelectedDetails(); RenderCommands(); RenderInventory();
        Set(feedback, snapshot != null ? snapshot.Feedback : string.Empty);
    }

    private void SelectTab(Tab tab)
    {
        currentTab = tab; RenderTabs();
        SetVisible(panelOverview, tab == Tab.Overview); SetVisible(panelInventory, tab == Tab.Inventory); SetVisible(panelStats, tab == Tab.Stats); SetVisible(panelBehavior, tab == Tab.Behavior);
        if (tab == Tab.Inventory) RenderInventory();
    }

    private void RenderTabs()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            XUiV_Button button = tabButtons[i]?.ViewComponent as XUiV_Button;
            if (button != null) button.Selected = i == (int)currentTab;
        }
        SetVisible(panelOverview, currentTab == Tab.Overview); SetVisible(panelInventory, currentTab == Tab.Inventory);
        SetVisible(panelStats, currentTab == Tab.Stats); SetVisible(panelBehavior, currentTab == Tab.Behavior);
    }

    private void SelectCompanion(int rowSlot)
    {
        int index = companionOffset + rowSlot; if (snapshot?.Companions == null || index < 0 || index >= snapshot.Companions.Count) return;
        string next = snapshot.Companions[index].Id; if (string.Equals(next, selectedId, StringComparison.Ordinal)) return;
        selectedId = next;
        selectionVersion++;
        commandOffset = inventoryOffset = 0; excludedInventory.Clear(); quantityOverrides.Clear();

        // Never leave the old companion's actionable rows attached to the newly selected id
        // while the authoritative snapshot is being refreshed. Identity/details can update
        // immediately from the existing list, but commands/inventory wait for the new reply.
        snapshot.Commands.Clear();
        snapshot.Inventory.Clear();
        RenderCompanions();
        RenderSelectedDetails();
        RenderCommands();
        RenderInventory();
        RefreshSnapshot(true);
    }

    private void RenderCompanions()
    {
        int total = snapshot?.Companions?.Count ?? 0; companionOffset = ClampOffset(companionOffset, total, companionRows.Length);
        for (int slot = 0; slot < companionRows.Length; slot++)
        {
            CompanionRowView view = companionRows[slot]; int index = companionOffset + slot; bool visible = view != null && index < total;
            if (view?.Root?.ViewComponent != null) view.Root.ViewComponent.IsVisible = visible; if (!visible) continue;
            RebirthCompanionListEntry row = snapshot.Companions[index];
            if (view.Select?.ViewComponent != null)
            {
                view.Select.ViewComponent.EventOnPress = true;
                view.Select.ViewComponent.Enabled = true;
            }
            XUiV_Button rowButton = view.Select?.ViewComponent as XUiV_Button;
            if (rowButton != null) rowButton.Enabled = true;
            Set(view.Name, row.Name);
            Set(view.Type, row.Type);
            Set(view.Level, row.IsDog ? "LVL " + row.DogLevel.ToString(CultureInfo.InvariantCulture) : string.Empty);
            Set(view.Status, row.Status);
            Set(view.Distance, IsUnloadedDog(row) ? "Saved" : row.Distance.ToString("0.0", CultureInfo.InvariantCulture) + "m");
            if (view.DirectionArrow != null)
            {
                view.DirectionArrow.IsVisible = !IsUnloadedDog(row) && row.Distance > 0.05f;
                if (view.DirectionArrow.Controller != null && view.DirectionArrow.Controller.ViewComponent != null)
                {
                    EntityPlayerLocal localPlayer = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
                    view.DirectionArrow.Controller.ViewComponent.Rotation =
                        localPlayer != null ? RebirthCompanionService.DirectionAngle(localPlayer, row.WorldPosition) : row.DirectionAngle;
                }
            }
            bool isSelected = string.Equals(row.Id, selectedId, StringComparison.Ordinal);
            ApplyCompanionRowColors(view, row, isSelected);
            ApplyCompanionIcon(view.Icon, row);
            if (view.Selection != null) view.Selection.IsVisible = isSelected;
        }
        UpdateScrollBar(companionScrollTrack, companionScrollThumb, companionOffset, total, companionRows.Length, CompanionTrackHeight);
    }

    private void UpdateDirectionArrows(EntityPlayerLocal player)
    {
        if (player == null || snapshot?.Companions == null) return;
        int total = snapshot.Companions.Count;
        for (int slot = 0; slot < companionRows.Length; slot++)
        {
            int index = companionOffset + slot;
            if (index < 0 || index >= total) continue;
            CompanionRowView view = companionRows[slot];
            if (view?.DirectionArrow?.Controller?.ViewComponent == null) continue;

            RebirthCompanionListEntry row = snapshot.Companions[index];
            view.DirectionArrow.IsVisible = row != null && !IsUnloadedDog(row) && row.Distance > 0.05f;
            if (row != null && view.DirectionArrow.IsVisible)
                view.DirectionArrow.Controller.ViewComponent.Rotation =
                    RebirthCompanionService.DirectionAngle(player, row.WorldPosition);
        }
    }

    private RebirthCompanionListEntry Selected()
    {
        if (snapshot?.Companions == null) return null;
        for (int i = 0; i < snapshot.Companions.Count; i++) if (string.Equals(snapshot.Companions[i].Id, selectedId, StringComparison.Ordinal)) return snapshot.Companions[i];
        return null;
    }

    private void RenderSelectedDetails()
    {
        RebirthCompanionListEntry selected = Selected();
        if (selected == null)
        {
            if (selectedIcon != null) selectedIcon.IsVisible = false;
            Set(selectedName, Localization.Get("xuiRebirthNoCompanions")); Set(selectedType, string.Empty);
            Set(selectedStatusLabel, Localization.Get("xuiRebirthStatus") + ":"); Set(selectedStatusValue, string.Empty);
            Set(selectedHealth, string.Empty); Set(selectedDistance, string.Empty); Set(selectedOrder, string.Empty);
            Set(overviewDescription, string.Empty); Set(statsText, string.Empty); Set(behaviorText, string.Empty);
            if (dogRenameLabel?.Controller?.ViewComponent != null) dogRenameLabel.Controller.ViewComponent.IsVisible = false;
            if (dogRenameText?.ViewComponent != null) dogRenameText.ViewComponent.IsVisible = false;
            if (btnDogRename?.ViewComponent != null) btnDogRename.ViewComponent.IsVisible = false;
            return;
        }
        if (selectedIcon != null)
        {
            selectedIcon.IsVisible = true;
            ApplyCompanionIcon(selectedIcon, selected);
        }
        Set(selectedName, selected.Name); Set(selectedType, selected.Type);
        if (selectedStatusLabel != null) selectedStatusLabel.Color = Color.white;
        Set(selectedStatusLabel, Localization.Get("xuiRebirthStatus") + ":");
        if (selectedStatusValue != null)
        {
            selectedStatusValue.SupportBbCode = false;
            selectedStatusValue.Color = CompanionStatusColor(selected);
            selectedStatusValue.Alpha = 1f;
        }
        Set(selectedStatusValue, selected.Status);
        Set(selectedHealth, (IsUnloadedDog(selected) ? "Last known health" : Localization.Get("xuiRebirthHealth")) + ": " + selected.Health + " / " + selected.MaxHealth);
        Set(selectedDistance, (IsUnloadedDog(selected) ? "Last known distance" : Localization.Get("xuiRebirthDistance")) + ": " + selected.Distance.ToString("0.0", CultureInfo.InvariantCulture) + " m");
        Set(selectedOrder, Localization.Get("xuiRebirthOrder") + ": " + selected.Order);
        Set(overviewDescription, selected.IsBoundUndead ? Localization.Get("xuiRebirthBoundUndeadOverviewDesc") : (selected.Kind == RebirthCompanionTargetKind.Drone ? Localization.Get("xuiRebirthDroneOverviewDesc") : (selected.IsDog ? Localization.Get("xuiRebirthDogOverviewDesc") : Localization.Get("xuiRebirthNpcOverviewDesc"))));
        Set(statsText, BuildStats(selected)); Set(behaviorText, BuildBehavior(selected));
        bool missingDog = selected.IsDog &&
                          selected.DogLifecycle == RebirthDogLifecycleKind.Active &&
                          selected.EntityId < 0;
        bool canRename = !snapshot.RequestFailed && !missingDog && (selected.Kind == RebirthCompanionTargetKind.Drone ||
                         (selected.IsDog && selected.DogLifecycle != RebirthDogLifecycleKind.AwaitingRespawn));
        if (dogRenameLabel?.Controller?.ViewComponent != null) dogRenameLabel.Controller.ViewComponent.IsVisible = canRename;
        if (dogRenameText?.ViewComponent != null) dogRenameText.ViewComponent.IsVisible = canRename;
        if (btnDogRename?.ViewComponent != null) btnDogRename.ViewComponent.IsVisible = canRename;
        if (canRename && dogRenameText != null && !dogRenameText.IsSelected) dogRenameText.Text = selected.Name ?? string.Empty;
        if (canRename && focusRenameRequested && dogRenameText != null)
        {
            dogRenameText.Text = selected.Name ?? string.Empty;
            dogRenameText.SelectOrVirtualKeyboard(true);
            focusRenameRequested = false;
        }
    }

    private static string BuildStats(RebirthCompanionListEntry x)
    {
        if (x.IsBoundUndead)
            return Localization.Get("xuiRebirthName") + ": " + x.Name + "\n" +
                   Localization.Get("xuiRebirthType") + ": " + x.Type + "\n" +
                   Localization.Get("xuiRebirthBoundUndeadTier") + ": " + x.UndeadTier + "\n" +
                   Localization.Get("xuiRebirthHealth") + ": " + x.Health + " / " + x.MaxHealth + "\n" +
                   Localization.Get("xuiRebirthConditioning") + ": " + x.UndeadConditioning.ToString("0", CultureInfo.InvariantCulture) + " / 100\n" +
                   Localization.Get("xuiRebirthDogTraining") + ": " + x.UndeadTraining.ToString("0", CultureInfo.InvariantCulture) + " / 100\n" +
                   Localization.Get("xuiRebirthBindingStability") + ": " + x.UndeadBindingStability.ToString("0", CultureInfo.InvariantCulture) + " / 100\n" +
                   Localization.Get("xuiRebirthBoundUndeadCapacityCost") + ": " + x.UndeadCapacityCost + "\n" +
                   Localization.Get("xuiRebirthBoundUndeadLifecycle") + ": " + x.UndeadLifecycle + "\n" +
                   Localization.Get("xuiRebirthDistance") + ": " + x.Distance.ToString("0.0", CultureInfo.InvariantCulture) + " m\n" +
                   Localization.Get("xuiRebirthOrder") + ": " + x.Order + "\n" + StorageSummary(x);
        if (x.IsDog)
            return Localization.Get("xuiRebirthName") + ": " + x.Name + "\n" +
                   Localization.Get("xuiRebirthType") + ": " + x.Type + "\n" +
                   (x.DogIsTamedWild ? Localization.Get("xuiRebirthSpecies") : Localization.Get("xuiRebirthDogBreed")) + ": " + x.DogBreed + "\n" +
                   (x.DogIsTamedWild ? Localization.Get("xuiRebirthAnimalCapacityCost") + ": " + x.DogAnimalCapacityCost + "\n" : string.Empty) +
                   Localization.Get("xuiRebirthDogLevel") + ": " + x.DogLevel + " / 10\n" +
                   Localization.Get("xuiRebirthDogKills") + ": " + x.DogKills + "\n" +
                   Localization.Get("xuiRebirthDogTraining") + ": " + x.DogTraining.ToString("0", CultureInfo.InvariantCulture) + " / 100\n" +
                   Localization.Get("xuiRebirthDogBond") + ": " + x.DogBond.ToString("0", CultureInfo.InvariantCulture) + " / 100\n" +
                   Localization.Get("xuiRebirthDogLearnedCommands") + ": " + x.DogLearnedCommandCount + " / 6\n" +
                   Localization.Get("xuiRebirthRespawnCost") + ": " + Localization.Get("xuiRebirthFree") + "\n" +
                   (IsUnloadedDog(x) ? "Last known health" : Localization.Get("xuiRebirthHealth")) + ": " + x.Health + " / " + x.MaxHealth + "\n" +
                   (x.DogIsTamedWild ? string.Empty : Localization.Get("xuiRebirthPhysicalResistance") + ": 30\n") +
                   (IsUnloadedDog(x) ? "Last known distance" : Localization.Get("xuiRebirthDistance")) + ": " + x.Distance.ToString("0.0", CultureInfo.InvariantCulture) + " m\n" +
                   Localization.Get("xuiRebirthOrder") + ": " + x.Order + "\n" + StorageSummary(x);
        return Localization.Get("xuiRebirthType") + ": " + x.Type + "\n" +
               Localization.Get("xuiRebirthHealth") + ": " + x.Health + " / " + x.MaxHealth + "\n" +
               Localization.Get("xuiRebirthDistance") + ": " + x.Distance.ToString("0.0", CultureInfo.InvariantCulture) + " m\n" +
               Localization.Get("xuiRebirthOrder") + ": " + x.Order + "\n" +
               StorageSummary(x) + "\n" +
               Localization.Get("xuiRebirthProfile") + ": " + x.Profile + "\n" +
               Localization.Get("xuiRebirthEntityId") + ": " + x.EntityId;
    }

    private static string BuildBehavior(RebirthCompanionListEntry x)
    {
        if (x.IsBoundUndead)
            return Localization.Get("xuiRebirthAttackState") + ": " + (x.AttackStopped ? Localization.Get("xuiRebirthStopped") : Localization.Get("xuiRebirthActive")) + "\n" +
                   Localization.Get("xuiRebirthOrder") + ": " + x.Order + "\n" +
                   Localization.Get("xuiRebirthBoundUndeadLifecycle") + ": " + x.UndeadLifecycle;
        if (x.Kind == RebirthCompanionTargetKind.Drone)
            return Localization.Get("xuiRebirthOrder") + ": " + x.Order + "\n" +
                   Localization.Get("xuiRebirthLockToPlayer") + ": " + YesNo(x.DroneLockedToPlayer) + "\n" +
                   Localization.Get("xuiRebirthDroneAccessLocked") + ": " + YesNo(x.DroneAccessLocked) + "\n" +
                   Localization.Get("xuiRebirthQuietMode") + ": " + YesNo(x.DroneQuiet) + "\n" +
                   Localization.Get("xuiRebirthLight") + ": " + (x.DroneLightAttached ? YesNo(x.DroneLightOn) : Localization.Get("xuiRebirthNotInstalled")) + "\n" +
                   Localization.Get("xuiRebirthHealAlliesSetting") + ": " + (x.DroneHealAttached ? YesNo(x.DroneHealingAllies) : Localization.Get("xuiRebirthNotInstalled"));
        string behavior = Localization.Get("xuiRebirthBehaviorMode") + ": " + (x.BehaviorMode == RebirthCompanionBehaviorMode.Hunting ? Localization.Get("xuiRebirthHunting") : Localization.Get("xuiRebirthFullControl")) + "\n" +
               Localization.Get("xuiRebirthAttackState") + ": " + (x.AttackStopped ? Localization.Get("xuiRebirthStopped") : Localization.Get("xuiRebirthActive")) + "\n" +
               Localization.Get("xuiRebirthOrder") + ": " + x.Order;
        if (!x.IsDog || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return behavior;
        return behavior + "\n\n" + Localization.Get("xuiRebirthDogLearnedCommands") + ":\n" +
               "  " + Localization.Get("xuiRebirthDogStayWhereDogIs") + ": " + Localization.Get("xuiRebirthDogCommandLearned") + "\n" +
               "  " + Localization.Get("xuiRebirthDogStayWhereOwnerIs") + ": " + (x.DogKnowsOwnerPositionStay ? Localization.Get("xuiRebirthDogCommandLearned") : Localization.Get("xuiRebirthDogCommandTrainingRequired")) + "\n" +
               "  " + Localization.Get("xuiRebirthDogGuardArea") + ": " + (x.DogKnowsGuardArea ? Localization.Get("xuiRebirthDogCommandLearned") : Localization.Get("xuiRebirthDogCommandTrainingRequired")) + "\n" +
               "  " + Localization.Get("xuiRebirthHunting") + ": " + (x.DogKnowsHunting ? Localization.Get("xuiRebirthDogCommandLearned") : Localization.Get("xuiRebirthDogCommandTrainingRequired"));
    }

    private static string StorageSummary(RebirthCompanionListEntry value)
    {
        if (value == null) return string.Empty;
        if (value.IsBoundUndead) return Localization.Get("xuiRebirthBoundUndeadInventoryNotSupported");
        if (value.DogIsTamedWild) return Localization.Get("xuiRebirthWildInventoryNotSupported");
        string key = value.IsDog ? "xuiRebirthDogInventorySummary" : (value.Kind == RebirthCompanionTargetKind.Npc ? "xuiRebirthNpcInventorySummary" : "xuiRebirthInventorySummary");
        return string.Format(Localization.Get(key), value.StorageUsed, value.StorageTotal);
    }

    private static string YesNo(bool value) { return value ? Localization.Get("xuiRebirthYes") : Localization.Get("xuiRebirthNo"); }

    private void RenameSelectedCompanion()
    {
        if (snapshot == null || snapshot.RequestFailed) return;
        RebirthCompanionListEntry selected = Selected();
        EntityPlayerLocal player = xui?.playerUI?.entityPlayer;
        if (selected == null || player == null || dogRenameText == null) return;
        string requested = (dogRenameText.Text ?? string.Empty).Trim();

        if (selected.Kind == RebirthCompanionTargetKind.Drone)
        {
            RebirthDroneRenameService.Request(player, selected.EntityId, requested);
            refreshTimer = 0.18f;
            return;
        }

        if (!selected.IsDog || selected.DogLifecycle == RebirthDogLifecycleKind.AwaitingRespawn) return;
        RebirthNpcStableId stableId;
        if (!RebirthNpcStableId.TryParse(selected.StableId, out stableId)) return;
        PersistentPlayerData persistent = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId);
        if (persistent?.PrimaryId == null) return;
        World world = GameManager.Instance?.World;
        if (world == null) return;
        if (!world.IsRemote())
        {
            string reason; bool success = RebirthDogLifecycleService.TryRename(player, stableId, requested, out reason);
            Set(feedback, reason); if (success) refreshTimer = 0.05f;
            return;
        }
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthDogRenameRequest>().Setup(player.entityId, persistent.PrimaryId, stableId.ToString(), requested));
        refreshTimer = 0.25f;
    }

    private void ExecuteCommand(int rowSlot)
    {
        if (snapshot == null || snapshot.RequestFailed) return;
        int index = commandOffset + rowSlot; if (snapshot?.Commands == null || index < 0 || index >= snapshot.Commands.Count || string.IsNullOrEmpty(selectedId)) return;
        RebirthCompanionCommandEntry row = snapshot.Commands[index]; if (!row.Enabled) return;
        if (row.Command == RebirthCompanionCommand.CreateWaypoint)
        {
            CreateWaypointForMissingDog();
            return;
        }
        if (row.Command == RebirthCompanionCommand.Rename)
        {
            focusRenameRequested = true;
            SelectTab(Tab.Stats);
            RenderSelectedDetails();
            return;
        }
        if (row.Command == RebirthCompanionCommand.Dismiss)
        {
            ShowDismissConfirmation();
            return;
        }
        RebirthCompanionService.Request(selectedId, row.Command); refreshTimer = 0.18f;
    }

    private void ShowDismissConfirmation()
    {
        RebirthCompanionListEntry selected = Selected();
        if (selected == null || string.IsNullOrEmpty(selected.Id)) return;
        pendingDismissId = selected.Id;
        pendingDismissName = selected.Name ?? string.Empty;
        Set(dismissConfirmText, string.Format(Localization.Get("xuiRebirthDismissCompanionConfirm"), pendingDismissName));
        SetVisible(dismissConfirmOverlay, true);
    }

    private void ConfirmDismiss()
    {
        if (snapshot == null || snapshot.RequestFailed) return;
        string targetId = pendingDismissId;
        CancelDismissConfirmation();
        if (string.IsNullOrEmpty(targetId)) return;
        RebirthCompanionService.Request(targetId, RebirthCompanionCommand.Dismiss);
        refreshTimer = 0.05f;
    }

    private void CancelDismissConfirmation()
    {
        pendingDismissId = string.Empty;
        pendingDismissName = string.Empty;
        SetVisible(dismissConfirmOverlay, false);
    }

    private bool DismissConfirmationVisible()
    {
        return dismissConfirmOverlay != null && dismissConfirmOverlay.ViewComponent != null &&
            dismissConfirmOverlay.ViewComponent.IsVisible;
    }

    private void CreateWaypointForMissingDog()
    {
        RebirthCompanionListEntry selected = Selected();
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (selected == null || player == null || !selected.IsDog ||
            selected.DogLifecycle != RebirthDogLifecycleKind.Active || selected.EntityId >= 0)
            return;

        Waypoint waypoint = new Waypoint();
        waypoint.pos = World.worldToBlockPos(selected.WorldPosition);
        waypoint.icon = "ui_game_symbol_map_waypoint_set";
        waypoint.name.Update(selected.Name ?? string.Empty, PlatformManager.MultiPlatform.User.PlatformUserId);
        waypoint.bIsAutoWaypoint = false;
        waypoint.bUsingLocalizationId = false;

        // Preserve normal waypoint duplicate behavior. The missing-dog action is a local
        // map operation; it does not mutate the companion or require a live entity.
        if (player.Waypoints.ContainsWaypoint(waypoint))
        {
            GameManager.ShowTooltip(player, Localization.Get("ttWaypointAlreadyExists"));
            return;
        }
        for (int i = 0; i < player.Waypoints.Collection.list.Count; i++)
        {
            Waypoint existing = player.Waypoints.Collection.list[i];
            if (existing != null && existing.pos == waypoint.pos &&
                string.Equals(existing.name.Text ?? string.Empty, waypoint.name.Text ?? string.Empty, StringComparison.Ordinal))
            {
                GameManager.ShowTooltip(player, Localization.Get("ttWaypointAlreadyExists"));
                return;
            }
        }

        player.Waypoints.Collection.Add(waypoint);
        waypoint.navObject = NavObjectManager.Instance.RegisterNavObject(
            "waypoint", waypoint.pos.ToVector3(), waypoint.icon);
        if (waypoint.navObject != null)
        {
            // Make it useful immediately for finding the missing dog: the waypoint is
            // visible on the compass/map as soon as it is created.
            waypoint.navObject.IsActive = true;
            waypoint.navObject.name = waypoint.name.Text;
            waypoint.navObject.usingLocalizationId = false;
        }

        string created = "Waypoint created at " + selected.Name + "'s last known position.";
        Set(feedback, created);
        GameManager.ShowTooltip(player, created);
    }

    private void RenderCommands()
    {
        int total = snapshot?.Commands?.Count ?? 0; commandOffset = ClampOffset(commandOffset, total, commandRows.Length);
        for (int slot = 0; slot < commandRows.Length; slot++)
        {
            CommandRowView view = commandRows[slot]; int index = commandOffset + slot; bool visible = view != null && index < total;
            if (view?.Root?.ViewComponent != null) view.Root.ViewComponent.IsVisible = visible; if (!visible) continue;
            RebirthCompanionCommandEntry row = snapshot.Commands[index]; Set(view.Name, row.Text);
            string atlas = string.IsNullOrEmpty(row.IconAtlas) ? "UIAtlas" : row.IconAtlas;
            string spriteName = string.IsNullOrEmpty(row.Icon) ? "ui_game_symbol_run" : row.Icon;
            UIBasicSprite.Flip flip = row.Command == RebirthCompanionCommand.Guard &&
                string.Equals(row.Icon, "ui_game_symbol_run_and_gun", StringComparison.Ordinal)
                ? (UIBasicSprite.Flip)1
                : (UIBasicSprite.Flip)0;
            if (view.Icon != null)
            {
                // A visible row is reused as the list scrolls. Resetting atlas/sprite/flip on
                // every render makes Stay/Guard briefly disappear while NGUI rebuilds it. Only
                // touch those properties when the logical icon assigned to this row changes.
                bool iconChanged = !view.IconStateInitialized ||
                    !string.Equals(view.IconAtlas, atlas, StringComparison.Ordinal) ||
                    !string.Equals(view.IconSprite, spriteName, StringComparison.Ordinal);
                if (iconChanged)
                {
                    view.Icon.UIAtlas = atlas;
                    view.Icon.SetSpriteImmediately(spriteName);
                    view.IconAtlas = atlas;
                    view.IconSprite = spriteName;
                }
                if (!view.IconStateInitialized || view.IconFlip != flip)
                {
                    view.Icon.Flip = flip;
                    view.IconFlip = flip;
                }
                if (iconChanged || view.EnabledState != row.Enabled)
                    view.Icon.SetColorImmediately(row.Enabled ? Color.white : Color.gray);
            }
            if (view.Name != null)
            {
                view.Name.SupportBbCode = false;
                view.Name.Color = row.Enabled ? Color.white : Color.gray;
                view.Name.Alpha = 1f;
            }
            XUiV_Button button = view.Select?.ViewComponent as XUiV_Button; if (button != null) button.Enabled = row.Enabled;
            view.EnabledState = row.Enabled;
            view.IconStateInitialized = true;
        }
        UpdateScrollBar(commandScrollTrack, commandScrollThumb, commandOffset, total, commandRows.Length, CommandTrackHeight);
    }

    private void RenderInventory()
    {
        int total = snapshot?.Inventory?.Count ?? 0; inventoryOffset = ClampOffset(inventoryOffset, total, inventoryRows.Length);
        suppressSelection = suppressQuantity = true;
        try
        {
            for (int slot = 0; slot < inventoryRows.Length; slot++)
            {
                InventoryRowView view = inventoryRows[slot]; int index = inventoryOffset + slot; bool visible = view != null && index < total;
                if (view?.Root?.ViewComponent != null) view.Root.ViewComponent.IsVisible = visible; if (!visible) continue;
                RebirthCompanionInventoryEntry row = snapshot.Inventory[index]; ItemClass item = ItemClass.GetForId(row.ItemType);
                Set(view.Name, item != null ? item.GetLocalizedItemName() : row.ItemKey); Set(view.Slot, row.SlotIndex >= 0 ? "#" + (row.SlotIndex + 1) : string.Empty);
                if (view.Icon != null && item != null) { view.Icon.UIAtlas = "ItemIconAtlas"; view.Icon.SetSpriteImmediately(item.GetIconName()); view.Icon.SetColorImmediately(item.GetIconTint()); }
                // A locked inventory entry is not transferable from this window. Keep
                // its checkbox visibly cleared and disabled until the lock is removed.
                bool selected = !row.Locked && !excludedInventory.Contains(row.RowKey);
                if (view.Include != null) { view.Include.Enabled = !row.Locked; view.Include.Value = selected; }
                int count = row.Count; int value; if (quantityOverrides.TryGetValue(row.RowKey, out value)) count = Mathf.Clamp(value, 0, row.Count);
                if (view.Count != null) { view.Count.Min = 0; view.Count.Max = Math.Max(1, row.Count); view.Count.Enabled = true; view.Count.Value = count; if (view.Count.ViewComponent != null) view.Count.ViewComponent.IsVisible = true; }
                if (view.Lock?.ViewComponent != null)
                {
                    view.Lock.ViewComponent.IsVisible = true;
                    view.Lock.ViewComponent.Enabled = true;
                    view.Lock.ViewComponent.EventOnPress = true;
                }
                XUiV_Button lockButton = view.Lock?.ViewComponent as XUiV_Button;
                if (lockButton != null)
                {
                    string lockSprite = row.Locked ? "ui_game_symbol_lock" : "ui_game_symbol_unlock";
                    lockButton.Enabled = true;
                    lockButton.UIAtlas = "UIAtlas";
                    lockButton.DefaultSpriteName = lockSprite;
                    lockButton.HoverSpriteName = lockSprite;
                    lockButton.DisabledSpriteName = lockSprite;
                    lockButton.DefaultSpriteColor = Color.white;
                    lockButton.HoverSpriteColor = Color.white;
                    lockButton.DisabledSpriteColor = Color.gray;
                }
                if (view.LockIcon != null) view.LockIcon.IsVisible = false;
                XUiV_Button useButton = view.Use?.ViewComponent as XUiV_Button;
                if (view.Use?.ViewComponent != null) view.Use.ViewComponent.IsVisible = row.CanUse;
                if (useButton != null) useButton.Enabled = row.CanUse;
            }
        }
        finally { suppressSelection = suppressQuantity = false; }
        UpdateScrollBar(inventoryScrollTrack, inventoryScrollThumb, inventoryOffset, total, inventoryRows.Length, InventoryTrackHeight);
        RebirthCompanionListEntry selectedCompanion = Selected();
        if (selectedCompanion != null && selectedCompanion.Distance > RebirthCompanionService.StorageDistance)
            Set(inventorySummary, Localization.Get("xuiRebirthStorageDistanceRequirement"));
        else
            Set(inventorySummary, StorageSummary(selectedCompanion));
    }

    private void InventorySelectionChanged(int slot, bool selected)
    {
        if (suppressSelection) return; RebirthCompanionInventoryEntry row = InventoryData(slot); if (row == null) return;
        if (row.Locked)
        {
            excludedInventory.Add(row.RowKey);
            suppressSelection = true;
            try
            {
                InventoryRowView view = inventoryRows != null && slot >= 0 && slot < inventoryRows.Length ? inventoryRows[slot] : null;
                if (view?.Include != null) { view.Include.Enabled = false; view.Include.Value = false; }
            }
            finally { suppressSelection = false; }
            return;
        }
        if (selected) excludedInventory.Remove(row.RowKey); else excludedInventory.Add(row.RowKey);
    }
    private void InventoryCountChanged(int slot, long newValue)
    {
        if (suppressQuantity) return; RebirthCompanionInventoryEntry row = InventoryData(slot); if (row == null) return;
        int value = Mathf.Clamp((int)newValue, 0, row.Count); if (value == row.Count) quantityOverrides.Remove(row.RowKey); else quantityOverrides[row.RowKey] = value;
    }
    private RebirthCompanionInventoryEntry InventoryData(int slot)
    {
        int index = inventoryOffset + slot; return snapshot?.Inventory != null && index >= 0 && index < snapshot.Inventory.Count ? snapshot.Inventory[index] : null;
    }
    private void ToggleInventoryLock(int slot)
    {
        if (snapshot == null || snapshot.RequestFailed) return;
        RebirthCompanionInventoryEntry row = InventoryData(slot); if (row == null || string.IsNullOrEmpty(selectedId)) return;

        // Mirror the pending state immediately so locking cannot leave a checked row
        // transferable during the request/refresh round trip. The authoritative server
        // still revalidates and applies the actual lock state.
        bool locking = !row.Locked;
        row.Locked = locking;
        if (locking)
        {
            excludedInventory.Add(row.RowKey);
            quantityOverrides.Remove(row.RowKey);
        }
        RenderInventory();

        RebirthCompanionService.RequestInventoryAction(selectedId, RebirthCompanionInventoryAction.ToggleLock, row.SlotIndex, row.ItemType, row.ItemKey);
        refreshTimer = 0.18f;
    }
    private void UseInventoryItem(int slot)
    {
        if (snapshot == null || snapshot.RequestFailed) return;
        RebirthCompanionInventoryEntry row = InventoryData(slot); if (row == null || !row.CanUse || string.IsNullOrEmpty(selectedId)) return;
        RebirthCompanionService.RequestInventoryAction(selectedId, RebirthCompanionInventoryAction.UseOne, row.SlotIndex, row.ItemType, row.ItemKey); refreshTimer = 0.18f;
    }
    private void SelectAllInventory()
    {
        excludedInventory.Clear();
        if (snapshot?.Inventory != null)
            for (int i = 0; i < snapshot.Inventory.Count; i++)
                if (snapshot.Inventory[i] != null && snapshot.Inventory[i].Locked)
                    excludedInventory.Add(snapshot.Inventory[i].RowKey);
        RenderInventory();
    }
    private void ClearAllInventory()
    {
        excludedInventory.Clear(); if (snapshot?.Inventory != null) for (int i = 0; i < snapshot.Inventory.Count; i++) excludedInventory.Add(snapshot.Inventory[i].RowKey); RenderInventory();
    }
    private void TransferSelectedInventory()
    {
        if (snapshot == null || snapshot.RequestFailed) return;
        RebirthCompanionListEntry selected = Selected(); if (selected == null || snapshot?.Inventory == null || string.IsNullOrEmpty(selectedId)) return;
        int requests = 0;
        for (int i = 0; i < snapshot.Inventory.Count; i++)
        {
            RebirthCompanionInventoryEntry row = snapshot.Inventory[i];
            if (row == null || row.Locked || excludedInventory.Contains(row.RowKey)) continue;
            int count = row.Count, overrideCount;
            if (quantityOverrides.TryGetValue(row.RowKey, out overrideCount)) count = Mathf.Clamp(overrideCount, 0, row.Count);
            if (count <= 0) continue;
            RebirthCompanionService.RequestInventoryAction(selectedId, RebirthCompanionInventoryAction.TransferToPlayer,
                row.SlotIndex, row.ItemType, row.ItemKey, count);
            requests++;
        }
        if (requests == 0) { Set(feedback, Localization.Get("xuiRebirthNothingSelected")); return; }
        quantityOverrides.Clear(); refreshTimer = 0.25f; Set(feedback, Localization.Get("xuiRebirthTransferRequested"));
    }

    private void CompanionScroll(float delta) { int total = snapshot?.Companions?.Count ?? 0; companionOffset = ScrollOffset(companionOffset, delta, total, companionRows.Length); RenderCompanions(); }
    private void CommandScroll(float delta) { int total = snapshot?.Commands?.Count ?? 0; commandOffset = ScrollOffset(commandOffset, delta, total, commandRows.Length); RenderCommands(); }
    private void InventoryScroll(float delta) { int total = snapshot?.Inventory?.Count ?? 0; inventoryOffset = ScrollOffset(inventoryOffset, delta, total, inventoryRows.Length); RenderInventory(); }
    private static int ScrollOffset(int offset, float delta, int total, int visible) { if (RebirthScrollbarPagingPolicy.Enabled) return (int)RebirthScrollbarPagingPolicy.Step(offset, Math.Max(0, total - visible), visible, delta > 0f ? -1 : delta < 0f ? 1 : 0); if (delta > 0f) offset--; else if (delta < 0f) offset++; return ClampOffset(offset, total, visible); }
    private static int ClampOffset(int value, int total, int visible) { return (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(value, 0, Math.Max(0, total - visible)), Math.Max(0, total - visible), visible, RebirthScrollbarPagingPolicy.Enabled); }
    private void ClampOffsets() { companionOffset = ClampOffset(companionOffset, snapshot.Companions.Count, companionRows.Length); commandOffset = ClampOffset(commandOffset, snapshot.Commands.Count, commandRows.Length); inventoryOffset = ClampOffset(inventoryOffset, snapshot.Inventory.Count, inventoryRows.Length); }

    private void WireCompanionRowPress(XUiController controller, int rowSlot)
    {
        if (controller == null) return;
        if (controller.ViewComponent != null)
        {
            controller.ViewComponent.EventOnPress = true;
            controller.ViewComponent.Enabled = true;
        }
        XUiV_Button button = controller.ViewComponent as XUiV_Button;
        if (button != null) button.Enabled = true;
        controller.OnPress += delegate { SelectCompanion(rowSlot); };
    }

    private static void WireScroll(XUiController controller, Action<float> callback) { if (controller == null || callback == null) return; if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true; controller.OnScroll += delegate(XUiController sender, float delta) { callback(delta); }; }
    private void WireComboScroll(XUiController controller) { if (controller == null) return; if (controller.ViewComponent != null) controller.ViewComponent.EventOnScroll = true; controller.OnScroll += delegate(XUiController sender, float delta) { if (!InputUtils.ShiftKeyPressed) InventoryScroll(delta); }; }
    private void WireThumbDrag(XUiController thumb, Action<float> callback) { if (thumb == null) return; if (thumb.ViewComponent != null) thumb.ViewComponent.EventOnDrag = true; thumb.OnDrag += delegate(XUiController sender, EDragType type, Vector2 delta) { if (type == EDragType.DragStart) pagingDragRemainder = 0f; if (type != EDragType.DragEnd) callback(-delta.y); }; }
    private float pagingDragRemainder;
    private void DragScroll(ref int offset, int total, int visible, int trackHeight, float dy, Action render)
    {
        if (total <= visible || dy == 0f) return; int thumbHeight = GetThumbHeight(total, visible, trackHeight); int travel = Math.Max(1, trackHeight - thumbHeight); int max = Math.Max(1, total - visible);
        int step = Mathf.RoundToInt(dy * (max / (float)travel));
        if (RebirthScrollbarPagingPolicy.Enabled) { pagingDragRemainder = Mathf.Clamp(offset + pagingDragRemainder + dy * (max / (float)travel), 0f, max) - offset; int next = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(offset + pagingDragRemainder, max, visible, true); pagingDragRemainder -= next - offset; offset = next; }
        else { if (step == 0) step = dy > 0 ? 1 : -1; offset = ClampOffset(offset + step, total, visible); }
        render();
    }
    private static int GetThumbHeight(int total, int visible, int trackHeight) { if (total <= visible || total <= 0) return trackHeight; return RebirthScrollbarPresentation.ThumbHeight(trackHeight,visible,total); }
    private static void UpdateScrollBar(XUiController track,XUiV_Button thumb,int offset,int total,int visible,int trackHeight)
    {
        if(track?.ViewComponent!=null)track.ViewComponent.IsVisible=total>visible&&visible>0;
        RebirthScrollbarPresentation.RenderThumb(thumb,trackHeight,visible,total,offset,6);
    }
    private static void ApplyCompanionIcon(XUiV_Sprite sprite, RebirthCompanionListEntry row)
    {
        if (sprite == null || row == null) return;
        sprite.IsVisible = true;

        if (row.Kind == RebirthCompanionTargetKind.Drone)
        {
            ItemClass droneItem = ItemClass.GetItemClass("gunBotT3JunkDrone", false);
            sprite.UIAtlas = "ItemIconAtlas";
            sprite.SetSpriteImmediately(droneItem != null ? droneItem.GetIconName() : "gunBotT3JunkDrone");
            Color droneTint = droneItem != null ? droneItem.GetIconTint() : Color.white;
            droneTint.a = 1f;
            sprite.Color = droneTint;
            sprite.SetColorImmediately(droneTint);
            return;
        }

        if (row.IsDog && !string.IsNullOrEmpty(row.Icon))
        {
            // Dog breed icons are already authored as item/block icons. Reuse the
            // authoritative breed artwork instead of looking for it in UIAtlas.
            // Set the final opacity AFTER the atlas/sprite swap: XUi sprites can retain
            // a previous alpha across swaps, which made the selected Overview portrait
            // look almost transparent even though the same icon was correct in the list.
            sprite.UIAtlas = "ItemIconAtlas";
            sprite.SetSpriteImmediately(row.Icon);
            sprite.Color = Color.white;
            sprite.SetColorImmediately(Color.white);
            return;
        }

        sprite.UIAtlas = "UIAtlas";
        sprite.SetSpriteImmediately(string.IsNullOrEmpty(row.Icon) ? "ui_game_symbol_allies" : row.Icon);
        sprite.Color = Color.white;
        sprite.SetColorImmediately(Color.white);
    }

    private static void ApplyCompanionRowColors(CompanionRowView view, RebirthCompanionListEntry row, bool selected)
    {
        if (view == null) return;

        // Companion identity text is intentionally invariant. Selection and status
        // coloring must never tint the companion name/type. Disable BBCode here as
        // well as in XML so no localization/custom name markup can override white.
        if (view.Name != null)
        {
            view.Name.SupportBbCode = false;
            view.Name.Color = new Color32(255, 255, 255, 255);
            view.Name.Alpha = 1f;
        }
        if (view.Type != null)
        {
            view.Type.SupportBbCode = false;
            view.Type.Color = new Color32(255, 255, 255, 255);
            view.Type.Alpha = 1f;
        }

        if (view.Level != null)
            view.Level.Color = selected ? new Color32(205, 205, 205, 255) : new Color32(175, 175, 175, 255);
        if (view.Distance != null)
            view.Distance.Color = selected ? new Color32(190, 190, 190, 255) : new Color32(160, 160, 160, 255);
        if (view.Status != null)
            view.Status.Color = CompanionStatusColor(row);
    }

    private static bool IsUnloadedDog(RebirthCompanionListEntry row) => row != null && row.IsDog && row.EntityId < 0;

    internal static Color CompanionStatusColor(RebirthCompanionListEntry row)
    {
        if (row == null) return new Color32(143, 209, 143, 255);
        if ((row.IsDog && row.DogLifecycle == RebirthDogLifecycleKind.AwaitingRespawn) ||
            (row.IsBoundUndead && string.Equals(row.UndeadLifecycle, "AwaitingReturn", StringComparison.OrdinalIgnoreCase)))
            return new Color32(204, 107, 100, 255);
        if (IsUnloadedDog(row)) return new Color32(175, 175, 185, 255);
        if (row.AttackStopped || string.Equals(row.Status, Localization.Get("xuiRebirthStatusStopped"), StringComparison.OrdinalIgnoreCase))
            return new Color32(204, 107, 100, 255);
        if (row.IsDog)
        {
            if (string.Equals(row.Status, Localization.Get("xuiRebirthDogStatusStayingHere"), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Status, Localization.Get("xuiRebirthDogStatusStayingWithOwnerPosition"), StringComparison.OrdinalIgnoreCase))
                return new Color32(214, 201, 120, 255);
            RebirthNpcOrderState order;
            if (Enum.TryParse(row.Order, true, out order))
            {
                if (order == RebirthNpcOrderState.Guard) return new Color32(82, 157, 224, 255);
                if (order == RebirthNpcOrderState.Stay) return new Color32(214, 201, 120, 255);
                if (order == RebirthNpcOrderState.Follow) return new Color32(143, 209, 143, 255);
            }
        }
        if (string.Equals(row.Status, Localization.Get("xuiRebirthStatusGuarding"), StringComparison.OrdinalIgnoreCase))
            return new Color32(82, 157, 224, 255);
        if (string.Equals(row.Status, Localization.Get("xuiRebirthStatusWaiting"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(row.Status, Localization.Get("xuiRebirthDogStatusStayingHere"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(row.Status, Localization.Get("xuiRebirthDogStatusStayingWithOwnerPosition"), StringComparison.OrdinalIgnoreCase))
            return new Color32(214, 201, 120, 255);
        return new Color32(143, 209, 143, 255);
    }

    private CompanionRowView GetCompanionRow(string id) { XUiController root = GetChildById(id); if (root == null) return null; return new CompanionRowView { Root = root, Select = root.GetChildById("select"), Selection = View<XUiV_Sprite>(root,"selection"), Icon = View<XUiV_Sprite>(root,"icon"), DirectionArrow = View<XUiV_Sprite>(root,"directionArrow"), Name = View<XUiV_Label>(root,"name"), Type = View<XUiV_Label>(root,"type"), Level = View<XUiV_Label>(root,"level"), Status = View<XUiV_Label>(root,"status"), Distance = View<XUiV_Label>(root,"distance") }; }
    private CommandRowView GetCommandRow(string id) { XUiController root = GetChildById(id); if (root == null) return null; return new CommandRowView { Root = root, Select = root.GetChildById("select"), Icon = View<XUiV_Sprite>(root,"icon"), Name = View<XUiV_Label>(root,"name") }; }
    private InventoryRowView GetInventoryRow(string id) { XUiController root = GetChildById(id); if (root == null) return null; XUiController include = root.GetChildById("include"), count = root.GetChildById("count"); return new InventoryRowView { Root=root, Include=include?.GetChildByType<XUiC_ToggleButton>(), Icon=View<XUiV_Sprite>(root,"icon"), Name=View<XUiV_Label>(root,"name"), Slot=View<XUiV_Label>(root,"slot"), Count=(count as XUiC_ComboBoxInt) ?? count?.GetChildByType<XUiC_ComboBoxInt>(), Lock=root.GetChildById("lock"), LockIcon=View<XUiV_Sprite>(root,"lockIcon"), Use=root.GetChildById("use") }; }
    private static T View<T>(XUiController root, string id) where T : XUiView { XUiController child = root?.GetChildById(id); return child?.ViewComponent as T; }
    private XUiV_Label Label(string id) => GetChildById(id)?.ViewComponent as XUiV_Label;
    private void Wire(string id, Action action) { XUiController c=GetChildById(id); if(c!=null)c.OnPress+=delegate{action();}; }
    private void Close() { xui.playerUI.windowManager.Close((GUIWindow)windowGroup); }
    private static void Set(XUiV_Label label,string text){if(label!=null)label.Text=text??string.Empty;}
    private static void SetVisible(XUiController controller,bool visible){if(controller?.ViewComponent!=null)controller.ViewComponent.IsVisible=visible;}
}
