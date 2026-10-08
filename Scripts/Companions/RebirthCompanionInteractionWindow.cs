using Platform;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Direct world-interaction surface for a single REBIRTH dog companion.
/// This is intentionally a focused card, not a second Companions manager:
/// identity/status on the left and the dog's immediate interaction actions on the right.
/// </summary>
public static class RebirthCompanionInteractionUiService
{
    public const string Group = "rebirthCompanionInteraction";

    public static bool Open(XUi xui, EntityRebirthDogCompanion dog, out string error)
    {
        error = string.Empty;
        if (xui == null || dog == null || dog.IsDead() || dog.RebirthRuntimeState == null)
        {
            error = Localization.Get("xuiRebirthDogInteractionUnavailable");
            return false;
        }

        EntityPlayerLocal player = xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        if (player == null)
        {
            error = Localization.Get("xuiRebirthDogInteractionUnavailable");
            return false;
        }

        bool ownedByPlayer = RebirthDogLifecycleService.IsOwnedBy(dog, player);
        bool unowned = dog.RebirthRuntimeState.OwnershipKind == RebirthNpcOwnershipKind.None;
        if (dog.RebirthOwnershipKnown && !ownedByPlayer && !unowned)
        {
            error = Localization.Get("xuiRebirthDogOwnedByAnotherPlayer");
            return false;
        }

        XUiController group = xui.FindWindowGroupByName(Group);
        XUiC_RebirthCompanionInteractionWindow controller = group != null
            ? group.GetChildByType<XUiC_RebirthCompanionInteractionWindow>()
            : null;
        if (group == null || controller == null || xui.playerUI == null || xui.playerUI.windowManager == null)
        {
            error = Localization.Get("xuiRebirthDogInteractionUnavailable");
            return false;
        }

        // Open the registered group by its authoritative XUi id after preparing the
        // dedicated controller. This avoids depending on a child controller's cached
        // windowGroup reference when Talk/E is invoked directly from world activation.
        controller.Prepare(dog);
        xui.playerUI.windowManager.Open(Group, true, true);
        return true;
    }
}

[Preserve]
public sealed class XUiC_RebirthCompanionInteractionWindow : XUiController
{
    private enum DirectActionKind : byte
    {
        Follow = 0,
        StayAtDog = 1,
        StayAtOwner = 2,
        Hunting = 3,
        FullControl = 4,
        Stop = 5,
        Resume = 6,
        Storage = 7,
        PickUp = 8,
        Dismiss = 9,
        Hire = 10,
        GuardArea = 11,
        SetRespawnPoint = 12,
        Recall = 13,
        ReportForDuty = 14
    }

    private sealed class DirectAction
    {
        public DirectActionKind Kind;
        public string Text;
        public string Icon;
        public string Atlas;
        public bool Enabled;
    }

    private sealed class ActionRowView
    {
        public XUiController Root;
        public XUiController Select;
        public XUiV_Sprite Icon;
        public XUiV_Label Name;
        public bool IconStateInitialized;
        public string IconAtlas = string.Empty;
        public string IconSprite = string.Empty;
        public UIBasicSprite.Flip IconFlip;
        public bool EnabledState;
    }

    private const int VisibleActions = 6;
    private const int ActionTrackHeight = 288;

    private readonly ActionRowView[] actionRows = new ActionRowView[VisibleActions];
    private readonly List<DirectAction> actions = new List<DirectAction>(8);

    private XUiV_Sprite dogIcon;
    private XUiV_Label dogName;
    private XUiV_Label dogBreed;
    private XUiV_Label dogLevel;
    private XUiV_Label dogStatusLabel;
    private XUiV_Label dogHealth;
    private XUiV_Label dogDistance;
    private XUiV_Label feedback;
    private XUiC_TextInput renameText;
    private XUiController actionScrollTrack;
    private XUiController actionScrollThumbControl;
    private XUiV_Button actionScrollThumb;
    private XUiController btnRename;
    private XUiController btnClose;
    private XUiController dismissConfirmOverlay, btnDismissConfirm, btnDismissCancel;
    private XUiV_Label dismissConfirmText;

    private RebirthNpcStableId stableId;
    private int dogEntityId = -1;
    private string preparedName = string.Empty;
    private int actionOffset;
    private bool wasCursorHidden;
    private float renderTimer;
    private bool cardOpen, snapshotPending;
    private float snapshotTimer;
    private int snapshotGeneration;
    private RebirthCompanionSnapshot remoteSnapshot;
    private Action<RebirthCompanionSnapshot> snapshotCallback;

    public override void Init()
    {
        base.Init();
        dogIcon = GetChildById("dogIcon")?.ViewComponent as XUiV_Sprite;
        dogName = Label("dogName");
        dogBreed = Label("dogBreed");
        dogLevel = Label("dogLevel");
        dogStatusLabel = Label("dogStatusLabel");
        dogHealth = Label("dogHealth");
        dogDistance = Label("dogDistance");
        feedback = Label("feedback");
        renameText = GetChildById("renameText") as XUiC_TextInput;
        btnRename = GetChildById("btnRename");
        btnClose = GetChildById("btnClose");
        if (btnRename != null) btnRename.OnPress += delegate { Rename(); };
        if (btnClose != null) btnClose.OnPress += delegate { Close(); };
        dismissConfirmOverlay = GetChildById("dismissConfirmOverlay");
        dismissConfirmText = Label("dismissConfirmText");
        btnDismissConfirm = GetChildById("btnDismissConfirm");
        btnDismissCancel = GetChildById("btnDismissCancel");
        if (btnDismissConfirm != null) btnDismissConfirm.OnPress += delegate { ConfirmDismiss(); };
        if (btnDismissCancel != null) btnDismissCancel.OnPress += delegate { CancelDismissConfirmation(); };
        CancelDismissConfirmation();

        for (int i = 0; i < actionRows.Length; i++)
        {
            actionRows[i] = GetActionRow("actionRow" + i.ToString("00", CultureInfo.InvariantCulture));
            ActionRowView row = actionRows[i];
            if (row == null) continue;
            int captured = i;
            if (row.Select != null)
            {
                if (row.Select.ViewComponent != null) row.Select.ViewComponent.EventOnPress = true;
                row.Select.OnPress += delegate { ExecuteAction(captured); };
            }
            WireScroll(row.Root, ActionScroll);
            WireScroll(row.Select, ActionScroll);
            WireScroll(row.Name?.Controller, ActionScroll);
            WireScroll(row.Icon?.Controller, ActionScroll);
        }

        actionScrollTrack = GetChildById("actionScrollTrackInput");
        actionScrollThumbControl = GetChildById("actionScrollThumb");
        actionScrollThumb = actionScrollThumbControl?.ViewComponent as XUiV_Button;
        WireScroll(GetChildById("actionPaneScrollCapture"), ActionScroll);
        WireScroll(actionScrollTrack, ActionScroll);
        WireScroll(actionScrollThumbControl, ActionScroll);
        WireThumbDrag(actionScrollThumbControl, delegate(float dy)
        {
            DragScroll(ref actionOffset, actions.Count, actionRows.Length, ActionTrackHeight, dy, RenderActions);
        });
    }

    public void Prepare(EntityRebirthDogCompanion dog)
    {
        // Open() does not reopen an already-open native window. Retire the previous
        // target request here so its late callback cannot strand the replacement card.
        snapshotGeneration++;
        RebirthCompanionSnapshotService.Cancel(snapshotCallback);
        snapshotCallback = null;
        snapshotPending = false;
        remoteSnapshot = null;
        snapshotTimer = 0f;
        actions.Clear();
        stableId = dog != null && dog.RebirthRuntimeState != null
            ? dog.RebirthRuntimeState.StableId
            : default(RebirthNpcStableId);
        dogEntityId = dog != null ? dog.entityId : -1;
        preparedName = !stableId.IsEmpty
            ? RebirthNpcWorldIntegrationService.GetDisplayName(stableId)
            : string.Empty;
        actionOffset = 0;
        renderTimer = 0f;
        CancelDismissConfirmation();
        if (cardOpen) Render();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        windowGroup.isEscClosable = false;
        cardOpen = true;
        snapshotTimer = 0f;
        remoteSnapshot = null;

        CursorControllerAbs cursor = xui.playerUI.CursorController;
        if (cursor != null)
        {
            wasCursorHidden = cursor.GetCursorHidden();
            cursor.Locked = false;
            cursor.HoverTarget = null;
            cursor.SetNavigationTarget((XUiView)null);
            cursor.SetNavigationLockView((XUiView)null);
            cursor.SetCursorHidden(false);
            cursor.ResetToCenter();
        }

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null)
        {
            player.SetControllable(false);
            player.ClearMovementInputs();
        }

        if (renameText != null) renameText.Text = preparedName ?? string.Empty;
        Set(feedback, Localization.Get("xuiRebirthDogSelectAction"));
        Render();
    }

    public override void OnClose()
    {
        cardOpen = false;
        snapshotGeneration++;
        RebirthCompanionSnapshotService.Cancel(snapshotCallback);
        snapshotCallback = null;
        snapshotPending = false;
        remoteSnapshot = null;
        CancelDismissConfirmation();
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
        }

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null) player.SetControllable(true);

        base.OnClose();

        if (UIInput.selection != null) UIInput.selection.isSelected = false;
        if (cursor != null)
        {
            cursor.HoverTarget = null;
            cursor.ResetNavigationTarget();
        }
        xui.playerUI.windowManager.ResetActionSets();

        stableId = default(RebirthNpcStableId);
        dogEntityId = -1;
        preparedName = string.Empty;
        actionOffset = 0;
        actions.Clear();
    }

    private bool pagingMode;
    public override void Update(float dt)
    {
        base.Update(dt);
        if (!cardOpen) return;
        bool pagingNow = RebirthScrollbarPagingPolicy.Enabled;
        if (pagingMode != pagingNow) { pagingMode = pagingNow; pagingDragRemainder = 0f; }
        if (pagingNow) { int old = actionOffset; actionOffset = ClampOffset(actionOffset, actions.Count, actionRows.Length); if (old != actionOffset) RenderActions(); }
        RebirthCompanionSnapshotService.Update();
        snapshotTimer -= dt;
        if (snapshotTimer <= 0f && !snapshotPending) RequestRemoteSnapshot();
        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        if (player != null) player.SetControllable(false);

        renderTimer -= dt;
        if (renderTimer <= 0f)
        {
            renderTimer = 0.35f;
            Render();
        }

        if (XUiUtils.HotkeysAllowedFor(viewComponent) &&
            (xui.playerUI.playerInput.PermanentActions.Cancel.WasReleased ||
             xui.playerUI.playerInput.GUIActions.Cancel.WasReleased))
        {
            if (DismissConfirmationVisible()) CancelDismissConfirmation();
            else Close();
        }
    }

    private void RequestRemoteSnapshot()
    {
        snapshotTimer = 1f;
        EntityRebirthDogCompanion dog = ResolveDog();
        EntityPlayerLocal player = xui?.playerUI?.entityPlayer;
        if (dog == null || dog.world == null || !dog.world.IsRemote() ||
            !RebirthDogLifecycleService.IsOwnedBy(dog, player)) return;
        string selected = "N:" + stableId;
        int generation = ++snapshotGeneration;
        snapshotPending = true;
        snapshotCallback = delegate(RebirthCompanionSnapshot value)
        {
            if (!cardOpen || generation != snapshotGeneration || selected != "N:" + stableId) return;
            snapshotPending = false;
            remoteSnapshot = value != null && value.SelectedId == selected ? value : null;
            if (value != null && !string.IsNullOrEmpty(value.Feedback)) Set(feedback, value.Feedback);
            renderTimer = 0f;
        };
        RebirthCompanionSnapshotService.Request(selected, snapshotCallback);
    }

    private void RenderRemote(EntityRebirthDogCompanion dog, EntityPlayerLocal player)
    {
        RebirthCompanionListEntry entry = null;
        if (remoteSnapshot != null)
            foreach (RebirthCompanionListEntry candidate in remoteSnapshot.Companions)
                if (candidate.Id == "N:" + stableId && candidate.IsDog) { entry = candidate; break; }
        Set(dogName, entry != null ? entry.Name : dog.EntityName);
        Set(dogBreed, entry != null ? entry.DogBreed : string.Empty);
        Set(dogLevel, entry != null ? "LVL " + entry.DogLevel.ToString(CultureInfo.InvariantCulture) : string.Empty);
        if (dogIcon != null)
        {
            dogIcon.IsVisible = entry != null && !string.IsNullOrEmpty(entry.Icon);
            if (dogIcon.IsVisible)
            {
                dogIcon.UIAtlas = "ItemIconAtlas";
                dogIcon.SetSpriteImmediately(entry.Icon);
                dogIcon.SetColorImmediately(Color.white);
            }
        }
        Set(dogStatusLabel, entry != null ? BuildStatusLine(entry.Status,
            XUiC_RebirthCompanions.CompanionStatusColor(entry)) : string.Empty);
        Set(dogHealth, Localization.Get("xuiRebirthHealth") + ": " + dog.Health + " / " + dog.GetMaxHealth());
        Set(dogDistance, Localization.Get("xuiRebirthDistance") + ": " + Vector3.Distance(player.position, dog.position).ToString("0.0", CultureInfo.InvariantCulture) + " m");
        bool rename = entry != null && entry.DogLifecycle != RebirthDogLifecycleKind.AwaitingRespawn;
        SetVisible(renameText, rename);
        SetVisible(btnRename, rename);
        if (rename && renameText != null && !renameText.IsSelected) renameText.Text = entry.Name;
        actions.Clear();
        if (entry != null)
            foreach (RebirthCompanionCommandEntry command in remoteSnapshot.Commands)
            {
                DirectActionKind kind;
                if (!TryMapRemoteAction(command.Command, out kind)) continue;
                actions.Add(new DirectAction { Kind = kind, Text = command.Text, Icon = command.Icon,
                    Atlas = command.IconAtlas, Enabled = command.Enabled });
            }
        actionOffset = ClampOffset(actionOffset, actions.Count, actionRows.Length);
        RenderActions();
    }

    private static bool TryMapRemoteAction(RebirthCompanionCommand command, out DirectActionKind kind)
    {
        switch (command)
        {
            case RebirthCompanionCommand.Follow: kind = DirectActionKind.Follow; return true;
            case RebirthCompanionCommand.Stay: kind = DirectActionKind.StayAtDog; return true;
            case RebirthCompanionCommand.Guard: kind = DirectActionKind.StayAtOwner; return true;
            case RebirthCompanionCommand.GuardArea: kind = DirectActionKind.GuardArea; return true;
            case RebirthCompanionCommand.Hunting: kind = DirectActionKind.Hunting; return true;
            case RebirthCompanionCommand.FullControl: kind = DirectActionKind.FullControl; return true;
            case RebirthCompanionCommand.Stop: kind = DirectActionKind.Stop; return true;
            case RebirthCompanionCommand.Resume: kind = DirectActionKind.Resume; return true;
            case RebirthCompanionCommand.Storage: kind = DirectActionKind.Storage; return true;
            case RebirthCompanionCommand.PickUp: kind = DirectActionKind.PickUp; return true;
            case RebirthCompanionCommand.Dismiss: kind = DirectActionKind.Dismiss; return true;
            case RebirthCompanionCommand.SetRespawnPoint: kind = DirectActionKind.SetRespawnPoint; return true;
            case RebirthCompanionCommand.ReportForDuty: kind = DirectActionKind.ReportForDuty; return true;
            default: kind = default(DirectActionKind); return false; // Recall remains management-only.
        }
    }

    private void Render()
    {
        EntityRebirthDogCompanion dog = ResolveDog();
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        bool live = dog != null && player != null && !dog.IsDead() && dog.RebirthRuntimeState != null;
        bool owned = live && RebirthDogLifecycleService.IsOwnedBy(dog, player);
        bool ownershipKnown = live && dog.RebirthOwnershipKnown;
        bool unowned = ownershipKnown && dog.RebirthRuntimeState.OwnershipKind == RebirthNpcOwnershipKind.None;

        if (ownershipKnown && !owned && !unowned)
        {
            Set(feedback, Localization.Get("xuiRebirthDogOwnedByAnotherPlayer"));
            Close();
            return;
        }

        if (live && dog.world.IsRemote() && (owned || !ownershipKnown))
        {
            RenderRemote(dog, player);
            return;
        }

        string currentName = !stableId.IsEmpty
            ? RebirthNpcWorldIntegrationService.GetDisplayName(stableId)
            : preparedName;
        if (string.IsNullOrWhiteSpace(currentName)) currentName = preparedName;
        Set(dogName, currentName);

        string breedText = string.Empty;
        RebirthDogBreedDefinition breed;
        if (!stableId.IsEmpty && RebirthDogStateService.TryGetBreed(stableId, out breed) && breed != null)
        {
            breedText = Localization.Get(breed.LocalizationKey);
            if (dogIcon != null)
            {
                dogIcon.IsVisible = true;
                dogIcon.UIAtlas = "ItemIconAtlas";
                dogIcon.SetSpriteImmediately(breed.IconName);
                dogIcon.SetColorImmediately(Color.white);
            }
        }
        else if (dogIcon != null)
        {
            dogIcon.IsVisible = false;
        }
        Set(dogBreed, breedText);

        int level = !stableId.IsEmpty ? RebirthDogStateService.GetLevel(stableId) : 1;
        Set(dogLevel, "LVL " + level.ToString(CultureInfo.InvariantCulture));

        RebirthDogLifecycleKind lifecycle = RebirthDogLifecycleKind.Active;
        if (!stableId.IsEmpty)
        {
            RebirthNpcPersistentRecordView persistentRecord;
            RebirthDogPersistentRecordView dogRecord;
            if (RebirthDogStateService.TryGetView(stableId, out persistentRecord, out dogRecord) && dogRecord != null)
                lifecycle = dogRecord.Lifecycle;
        }

        if (dogStatusLabel != null) dogStatusLabel.Color = Color.white;
        if (live)
        {
            bool attackStopped = owned && !stableId.IsEmpty && RebirthDogStateService.IsAttackStopped(stableId);
            bool stationaryGuard = owned && dog.RebirthRuntimeState != null &&
                dog.RebirthRuntimeState.Order == RebirthNpcOrderState.Guard &&
                RebirthDogStateService.IsGuardStationaryStay(stableId);
            bool respawned = owned && lifecycle == RebirthDogLifecycleKind.AwaitingRespawn;
            string status = respawned
                ? Localization.Get("xuiRebirthStatusRespawned")
                : owned && dog.RebirthRuntimeState != null
                    ? RebirthCompanionService.GetDogStatus(dog.RebirthRuntimeState.Order, stationaryGuard, attackStopped)
                    : Localization.Get("xuiRebirthDogAvailableForHire");
            Color statusColor = respawned
                ? new Color32(204, 107, 100, 255)
                : owned ? DogStatusColor(dog) : new Color32(214, 201, 120, 255);
            Set(dogStatusLabel, BuildStatusLine(status, statusColor));
            Set(dogHealth, Localization.Get("xuiRebirthHealth") + ": " + dog.Health.ToString(CultureInfo.InvariantCulture) + " / " + dog.GetMaxHealth().ToString(CultureInfo.InvariantCulture));
            Set(dogDistance, Localization.Get("xuiRebirthDistance") + ": " + Vector3.Distance(player.position, dog.position).ToString("0.0", CultureInfo.InvariantCulture) + " m");
        }
        else
        {
            Set(dogStatusLabel, BuildStatusLine(Localization.Get("xuiRebirthDogInteractionUnavailable"), new Color32(204, 107, 100, 255)));
            Set(dogHealth, string.Empty);
            Set(dogDistance, string.Empty);
        }

        bool showRename = owned && lifecycle != RebirthDogLifecycleKind.AwaitingRespawn;
        SetVisible(renameText, showRename);
        SetVisible(btnRename, showRename);
        if (showRename && renameText != null && !renameText.IsSelected)
            renameText.Text = currentName ?? string.Empty;

        BuildActions(dog, owned, unowned);
        actionOffset = ClampOffset(actionOffset, actions.Count, actionRows.Length);
        RenderActions();
    }

    private void BuildActions(EntityRebirthDogCompanion dog, bool owned, bool unowned)
    {
        actions.Clear();
        if (dog == null || dog.IsDead() || dog.RebirthRuntimeState == null) return;

        if (unowned)
        {
            AddAction(DirectActionKind.Hire, "xuiRebirthDogHireAction", "ui_game_symbol_food", true);
            return;
        }
        if (!owned) return;

        RebirthNpcPersistentRecordView persistentRecord = null;
        RebirthDogPersistentRecordView dogRecord = null;
        if (!stableId.IsEmpty) RebirthDogStateService.TryGetView(stableId, out persistentRecord, out dogRecord);
        if (dogRecord != null && dogRecord.Lifecycle == RebirthDogLifecycleKind.AwaitingRespawn)
        {
            AddAction(DirectActionKind.ReportForDuty, "xuiRebirthDogReportForDuty", "ui_game_symbol_check", true);
            return;
        }

        RebirthNpcOrderState order = dog.RebirthRuntimeState.Order;
        RebirthCompanionBehaviorMode mode = !stableId.IsEmpty
            ? RebirthDogStateService.GetCombatMode(stableId)
            : RebirthCompanionBehaviorMode.FullControl;
        bool attackStopped = !stableId.IsEmpty && RebirthDogStateService.IsAttackStopped(stableId);
        EntityPlayerLocal localPlayer = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        float distance = localPlayer != null ? Vector3.Distance(dog.position, localPlayer.position) : float.MaxValue;
        bool positionalInRange = RebirthCompanionService.IsWithinPositionalCommandDistance(distance);
        bool behaviorInRange = RebirthCompanionService.IsWithinBehaviorCommandDistance(distance);

        // Direct Companion interaction-card order. It mirrors the management list except
        // Recall is intentionally omitted here; Recall is a remote-management action only.
        AddAction(DirectActionKind.Storage, "xuiStorage", "ui_game_symbol_loot_sack",
            RebirthDogInventoryPolicy.InventoryEnabled && RebirthCompanionService.IsWithinStorageDistance(distance));
        bool canPickUp = localPlayer != null &&
            (dog.position - localPlayer.position).sqrMagnitude <=
            RebirthDogLifecycleService.MaximumPickupDistance * RebirthDogLifecycleService.MaximumPickupDistance;
        AddAction(DirectActionKind.PickUp, "xuiRebirthDogPickUpCompanion", "ui_game_symbol_hand", canPickUp);
        AddAction(DirectActionKind.Follow, "xuiRebirthDogFollow", "ui_game_symbol_run",
            positionalInRange && order != RebirthNpcOrderState.Follow);
        // Keep positional action artwork identical across both companion windows.
        // Both Stay commands use the same source icon; the owner-position form is
        // mirrored by the row renderer so the relationship is visually obvious.
        AddAction(DirectActionKind.StayAtDog, "xuiRebirthDogStayWhereDogIs", "ui_game_symbol_run_and_gun", positionalInRange && order != RebirthNpcOrderState.Stay);
        string directGateReason;
        bool canOwnerStay = RebirthAnimalHandlingService.CanUseDogCommand(localPlayer, dogRecord, RebirthCompanionCommand.Guard, out directGateReason);
        bool canGuardArea = RebirthAnimalHandlingService.CanUseDogCommand(localPlayer, dogRecord, RebirthCompanionCommand.GuardArea, out directGateReason);
        AddAction(DirectActionKind.StayAtOwner, "xuiRebirthDogStayWhereOwnerIs", "ui_game_symbol_run_and_gun", positionalInRange && canOwnerStay);
        AddAction(DirectActionKind.GuardArea, "xuiRebirthDogGuardArea", "ui_game_symbol_twitch_shield", positionalInRange && canGuardArea);
        AddAction(attackStopped ? DirectActionKind.Resume : DirectActionKind.Stop,
            attackStopped ? "xuiRebirthResume" : "xuiRebirthStop",
            attackStopped ? "ui_game_symbol_twitch_play" : "ui_game_symbol_twitch_pause", behaviorInRange);
        RebirthCompanionCommand directBehaviorCommand = mode == RebirthCompanionBehaviorMode.Hunting ? RebirthCompanionCommand.FullControl : RebirthCompanionCommand.Hunting;
        bool canBehavior = RebirthAnimalHandlingService.CanUseDogCommand(localPlayer, dogRecord, directBehaviorCommand, out directGateReason);
        AddAction(mode == RebirthCompanionBehaviorMode.Hunting ? DirectActionKind.FullControl : DirectActionKind.Hunting,
            mode == RebirthCompanionBehaviorMode.Hunting ? "xuiRebirthFullControl" : "xuiRebirthHunting",
            mode == RebirthCompanionBehaviorMode.Hunting ? "ui_game_symbol_intellect" : "ui_game_symbol_spear", behaviorInRange && canBehavior);
        AddAction(DirectActionKind.SetRespawnPoint, "xuiRebirthDogSetRespawnPoint", "ui_game_symbol_drop_item", true);
        AddAction(DirectActionKind.Dismiss, "xuiRebirthDogDismissCompanion", "ui_game_symbol_x", true);
    }

    private void AddAction(DirectActionKind kind, string localizationKey, string icon, bool enabled)
    {
        actions.Add(new DirectAction
        {
            Kind = kind,
            Text = Localization.Get(localizationKey),
            Icon = icon,
            Atlas = "UIAtlas",
            Enabled = enabled
        });
    }

    private void RenderActions()
    {
        int total = actions.Count;
        actionOffset = ClampOffset(actionOffset, total, actionRows.Length);
        for (int slot = 0; slot < actionRows.Length; slot++)
        {
            ActionRowView view = actionRows[slot];
            int index = actionOffset + slot;
            bool visible = view != null && index < total;
            if (view?.Root?.ViewComponent != null) view.Root.ViewComponent.IsVisible = visible;
            if (!visible) continue;

            DirectAction action = actions[index];
            Set(view.Name, action.Text);
            string atlas = string.IsNullOrEmpty(action.Atlas) ? "UIAtlas" : action.Atlas;
            string spriteName = string.IsNullOrEmpty(action.Icon) ? "ui_game_symbol_run" : action.Icon;
            UIBasicSprite.Flip flip = action.Kind == DirectActionKind.StayAtOwner
                ? (UIBasicSprite.Flip)1
                : (UIBasicSprite.Flip)0;
            if (view.Icon != null)
            {
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
                if (iconChanged || view.EnabledState != action.Enabled)
                    view.Icon.SetColorImmediately(action.Enabled ? Color.white : new Color(0.431f, 0.431f, 0.431f, 1f));
            }
            XUiV_Button button = view.Select?.ViewComponent as XUiV_Button;
            if (button != null) button.Enabled = action.Enabled;
            if (view.Name != null)
            {
                view.Name.SupportBbCode = false;
                view.Name.Color = action.Enabled ? Color.white : new Color(0.431f, 0.431f, 0.431f, 1f);
                view.Name.Alpha = 1f;
            }
            view.EnabledState = action.Enabled;
            view.IconStateInitialized = true;
        }
        UpdateScrollBar(actionScrollTrack, actionScrollThumb, actionOffset, total, actionRows.Length, ActionTrackHeight);
    }

    private void ExecuteAction(int rowSlot)
    {
        int index = actionOffset + rowSlot;
        if (index < 0 || index >= actions.Count) return;
        DirectAction action = actions[index];
        if (action == null || !action.Enabled) return;

        switch (action.Kind)
        {
            case DirectActionKind.Follow:
                IssueCommand(RebirthCompanionCommand.Follow);
                break;
            case DirectActionKind.StayAtDog:
                IssueCommand(RebirthCompanionCommand.Stay);
                break;
            case DirectActionKind.StayAtOwner:
                IssueCommand(RebirthCompanionCommand.Guard);
                break;
            case DirectActionKind.GuardArea:
                IssueCommand(RebirthCompanionCommand.GuardArea);
                break;
            case DirectActionKind.Hunting:
                IssueCommand(RebirthCompanionCommand.Hunting);
                break;
            case DirectActionKind.FullControl:
                IssueCommand(RebirthCompanionCommand.FullControl);
                break;
            case DirectActionKind.Stop:
                IssueCommand(RebirthCompanionCommand.Stop);
                break;
            case DirectActionKind.Resume:
                IssueCommand(RebirthCompanionCommand.Resume);
                break;
            case DirectActionKind.Storage:
                // Use the same native ten-slot loot surface as the Companions command list
                // and radial Storage action. OpenLocal closes this interaction card before
                // requesting the entity lock.
                if (!stableId.IsEmpty)
                    RebirthCompanionService.Request("N:" + stableId, RebirthCompanionCommand.Storage);
                break;
            case DirectActionKind.SetRespawnPoint:
                IssueCommand(RebirthCompanionCommand.SetRespawnPoint);
                break;
            case DirectActionKind.Recall:
                IssueCommand(RebirthCompanionCommand.Recall);
                break;
            case DirectActionKind.PickUp:
                IssueCommand(RebirthCompanionCommand.PickUp);
                Close();
                break;
            case DirectActionKind.Dismiss:
                ShowDismissConfirmation();
                break;
            case DirectActionKind.ReportForDuty:
                IssueCommand(RebirthCompanionCommand.ReportForDuty);
                break;
            case DirectActionKind.Hire:
                Hire();
                break;
        }
        renderTimer = 0.12f;
    }

    private void IssueCommand(RebirthCompanionCommand command)
    {
        if (stableId.IsEmpty) return;
        RebirthCompanionService.Request("N:" + stableId, command);
        Set(feedback, Localization.Get("xuiRebirthDogCommandSent"));
    }

    private void Hire()
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityRebirthDogCompanion dog = ResolveDog();
        if (player == null || persistent == null || persistent.PrimaryId == null || world == null || dog == null || !dog.RebirthOwnershipKnown ||
            dog.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.None)
        {
            Set(feedback, Localization.Get("xuiRebirthDogHireUnavailable"));
            return;
        }

        if (!world.IsRemote())
        {
            string reason;
            RebirthDogLifecycleService.TryHire(world, player, dog, out reason);
            Set(feedback, reason);
            Render();
            return;
        }

        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthDogHireRequest>()
                .Setup(player.entityId, persistent.PrimaryId, dog.entityId));
        Set(feedback, Localization.Get("xuiRebirthDogHireRequestSent"));
    }

    private void Rename()
    {
        EntityPlayerLocal player = xui != null && xui.playerUI != null ? xui.playerUI.entityPlayer : null;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (player == null || persistent == null || persistent.PrimaryId == null || world == null || stableId.IsEmpty || renameText == null)
        {
            Set(feedback, Localization.Get("xuiRebirthDogRenameUnavailable"));
            return;
        }

        string requested = (renameText.Text ?? string.Empty).Trim();
        if (!world.IsRemote())
        {
            string reason;
            bool success = RebirthDogLifecycleService.TryRename(player, stableId, requested, out reason);
            Set(feedback, reason);
            if (success)
            {
                preparedName = requested;
                Render();
            }
            return;
        }

        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthDogRenameRequest>()
                .Setup(player.entityId, persistent.PrimaryId, stableId.ToString(), requested));
        Set(feedback, Localization.Get("xuiRebirthDogRenameRequestSent"));
    }

    private void ShowDismissConfirmation()
    {
        if (stableId.IsEmpty) return;
        string name = dogName != null ? dogName.Text : preparedName;
        if (string.IsNullOrWhiteSpace(name)) name = preparedName ?? string.Empty;
        Set(dismissConfirmText, string.Format(Localization.Get("xuiRebirthDismissCompanionConfirm"), name));
        SetVisible(dismissConfirmOverlay, true);
    }

    private void ConfirmDismiss()
    {
        if (stableId.IsEmpty)
        {
            CancelDismissConfirmation();
            return;
        }

        string targetId = "N:" + stableId;
        CancelDismissConfirmation();
        RebirthCompanionService.Request(targetId, RebirthCompanionCommand.Dismiss);
        Close();
    }

    private void CancelDismissConfirmation()
    {
        SetVisible(dismissConfirmOverlay, false);
    }

    private bool DismissConfirmationVisible()
    {
        return dismissConfirmOverlay != null && dismissConfirmOverlay.ViewComponent != null &&
            dismissConfirmOverlay.ViewComponent.IsVisible;
    }

    private static string BuildStatusLine(string value, Color color)
    {
        string hex = ColorUtility.ToHtmlStringRGB(color);
        return Localization.Get("xuiRebirthStatus") + ": [" + hex + "]" + (value ?? string.Empty) + "[-]";
    }


    private static Color DogStatusColor(EntityRebirthDogCompanion dog)
    {
        if (dog == null || dog.RebirthRuntimeState == null) return new Color32(143, 209, 143, 255);
        RebirthNpcStableId id = dog.RebirthRuntimeState.StableId;
        if (!id.IsEmpty && RebirthDogStateService.IsAttackStopped(id))
            return new Color32(204, 107, 100, 255);
        if (dog.RebirthRuntimeState.Order == RebirthNpcOrderState.Guard &&
            !id.IsEmpty && RebirthDogStateService.IsGuardStationaryStay(id))
            return new Color32(214, 201, 120, 255);
        switch (dog.RebirthRuntimeState.Order)
        {
            case RebirthNpcOrderState.Guard: return new Color32(82, 157, 224, 255);
            case RebirthNpcOrderState.Stay: return new Color32(214, 201, 120, 255);
            default: return new Color32(143, 209, 143, 255);
        }
    }

    private EntityRebirthDogCompanion ResolveDog()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null) return null;

        EntityRebirthDogCompanion dog = dogEntityId >= 0 ? world.GetEntity(dogEntityId) as EntityRebirthDogCompanion : null;
        if (dog != null && dog.RebirthRuntimeState != null && dog.RebirthRuntimeState.StableId.Equals(stableId)) return dog;

        int liveEntityId;
        if (!stableId.IsEmpty && RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out liveEntityId))
        {
            dog = world.GetEntity(liveEntityId) as EntityRebirthDogCompanion;
            if (dog != null) dogEntityId = dog.entityId;
        }
        return dog;
    }

    private ActionRowView GetActionRow(string id)
    {
        XUiController root = GetChildById(id);
        if (root == null) return null;
        return new ActionRowView
        {
            Root = root,
            Select = root.GetChildById("select"),
            Icon = root.GetChildById("icon")?.ViewComponent as XUiV_Sprite,
            Name = root.GetChildById("name")?.ViewComponent as XUiV_Label
        };
    }

    private XUiV_Label Label(string id)
    {
        XUiController controller = GetChildById(id);
        return controller != null ? controller.ViewComponent as XUiV_Label : null;
    }

    private static void Set(XUiV_Label label, string value)
    {
        if (label != null) label.Text = value ?? string.Empty;
    }

    private static void SetVisible(XUiController controller, bool visible)
    {
        if (controller?.ViewComponent != null) controller.ViewComponent.IsVisible = visible;
    }

    private void ActionScroll(float delta)
    {
        if (RebirthScrollbarPagingPolicy.Enabled) actionOffset = (int)RebirthScrollbarPagingPolicy.Step(actionOffset, Math.Max(0, actions.Count - actionRows.Length), actionRows.Length, delta > 0f ? -1 : delta < 0f ? 1 : 0);
        else if (delta > 0f) actionOffset--;
        else if (delta < 0f) actionOffset++;
        actionOffset = ClampOffset(actionOffset, actions.Count, actionRows.Length);
        RenderActions();
    }

    private static int ClampOffset(int value, int total, int visible)
    {
        return (int)RebirthScrollbarPagingPolicy.SnapAbsolute(Mathf.Clamp(value, 0, Math.Max(0, total - visible)), Math.Max(0, total - visible), visible, RebirthScrollbarPagingPolicy.Enabled);
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

    private float pagingDragRemainder;
    private void DragScroll(ref int offset, int total, int visible, int trackHeight, float dy, Action render)
    {
        if (total <= visible || dy == 0f) return;
        int thumbHeight = GetThumbHeight(total, visible, trackHeight);
        int travel = Math.Max(1, trackHeight - thumbHeight);
        int max = Math.Max(1, total - visible);
        int step = Mathf.RoundToInt(dy * (max / (float)travel));
        if (RebirthScrollbarPagingPolicy.Enabled)
        {
            pagingDragRemainder = Mathf.Clamp(offset + pagingDragRemainder + dy * (max / (float)travel), 0f, max) - offset;
            int next = (int)RebirthScrollbarPagingPolicy.SnapAbsolute(offset + pagingDragRemainder, max, visible, true);
            pagingDragRemainder -= next - offset; offset = next;
        }
        else { if (step == 0) step = dy > 0 ? 1 : -1; offset = ClampOffset(offset + step, total, visible); }
        render();
    }

    private static int GetThumbHeight(int total, int visible, int trackHeight)
    {
        if (total <= visible || total <= 0) return trackHeight;
        return Mathf.Clamp(Mathf.RoundToInt(trackHeight * ((float)visible / total)), 28, trackHeight);
    }

    private static void UpdateScrollBar(XUiController track, XUiV_Button thumb, int offset, int total, int visible, int trackHeight)
    {
        bool needed = total > visible && total > 0;
        if (track?.ViewComponent != null) track.ViewComponent.IsVisible = needed;
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
            Vector3 p = thumb.UiTransform.localPosition;
            p.x = 1;
            p.y = -y;
            thumb.UiTransform.localPosition = p;
        }
    }

    private void Close()
    {
        if (xui != null && xui.playerUI != null && windowGroup != null)
            xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
    }
}
