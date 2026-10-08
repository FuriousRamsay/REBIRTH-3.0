using HarmonyLib;
using InControl;
using Platform;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registers the persistent REBIRTH native action set, exposes it to the stock
/// keyboard/controller Controls screens, and dispatches semantic actions to gameplay systems.
/// </summary>
public static class RebirthNativeControls
{
    private const string HarmonyId = "rebirth.native.controls.3.1";
    private const string ActionSetSaveKey = "ActionSet_" + PlayerActionsRebirth.ActionSetName;
    private const int StockKeyboardTabCount = 9;   // Generic + 8 stock binding tabs in 3.1 b14.
    private const int StockControllerTabCount = 6; // Generic + 4 stock binding tabs + Layout in 3.1 b14.

    private static readonly Harmony Harmony = new Harmony(HarmonyId);
    private static bool installed;

    public static PlayerActionsRebirth Actions { get; private set; }

    public static void Install()
    {
        if (installed)
            return;

        installed = true;

        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPlayerInputManagerConstructorPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthKeyboardControlsEntriesPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthControllerControlsEntriesPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthBindingConflictDisplayPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCruisePlayerUpdatePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCruiseVehicleMovementPatch));
        RebirthHornDoorService.Install(Harmony);

        // IMPORTANT: in 3.1 the native PlayerInputManager is normally constructed before
        // mod InitMod runs. A constructor-only Harmony postfix therefore misses the live
        // manager on normal startup. Register against the already-created native manager now.
        EnsureRegisteredFromNativePlatform("mod-install");

        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Controls] Native action registration, Controls UI integration, and gameplay dispatchers installed."); }
    }

    private static bool EnsureRegisteredFromNativePlatform(string reason)
    {
        try
        {
            IPlatform platform = PlatformManager.NativePlatform;
            PlayerInputManager input = platform == null ? null : platform.Input;
            return EnsureRegistered(input, reason);
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Controls] Could not resolve native PlayerInputManager during " + reason + ": " + ex.Message);
            return false;
        }
    }

    private static bool EnsureRegistered(PlayerInputManager inputManager, string reason)
    {
        if (inputManager == null)
            return false;

        PlayerActionsBase namedSet = inputManager.GetActionSetForName(PlayerActionsRebirth.ActionSetName);
        if (namedSet != null)
        {
            PlayerActionsRebirth existing = namedSet as PlayerActionsRebirth;
            if (existing == null)
            {
                Log.Error("[REBIRTH Controls] Action-set name collision for '" + PlayerActionsRebirth.ActionSetName + "'.");
                return false;
            }

            Actions = existing;
            return true;
        }

        PlayerActionsRebirth actions = new PlayerActionsRebirth(inputManager);
        inputManager.actionSets.Add(actions);
        Actions = actions;

        // GameOptionsControls.Load normally runs later than mod initialization and will load
        // this action set automatically. Loading here as well makes late/reinitialized input
        // managers safe and is idempotent when the normal loader subsequently runs.
        try
        {
            string saved = SdPlayerPrefs.GetString(ActionSetSaveKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
                actions.Load(saved);

            // 2026-08-19: the card-style action shipped briefly with Shift+C as its default.
            // Existing saves can therefore override the corrected Shift+L default immediately
            // after CreateDefaultKeyboardBindings(). Migrate only that exact legacy default;
            // every other user-selected binding is preserved. Persist the migrated action set so
            // GameOptionsManager.LoadControls cannot restore Shift+C later in startup.
            MigrateLegacyCompanionCardStyleBinding(actions);
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Controls] Could not restore saved REBIRTH bindings during " + reason + ": " + ex.Message);
        }

        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Controls] Registered native action set '" + PlayerActionsRebirth.ActionSetName +
                "' during " + reason + " actions=" + actions.Actions.Count + "."); }
        return true;
    }

    private static void MigrateLegacyCompanionCardStyleBinding(PlayerActionsRebirth actions)
    {
        if (actions == null || actions.ToggleCompanionCardStyle == null)
            return;

        PlayerAction action = actions.ToggleCompanionCardStyle;
        string bindingText;
        try
        {
            bindingText = action.GetBindingString(false);
        }
        catch
        {
            return;
        }

        if (!IsLegacyShiftCBinding(bindingText))
            return;

        try
        {
            action.UnbindBindingsOfType(false);
            action.AddBinding(new KeyBindingSource(new Key[] { Key.Shift, Key.L }));
            SdPlayerPrefs.SetString(ActionSetSaveKey, actions.Save());
            { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Controls] Migrated Toggle Companion Card Style default binding Shift+C -> Shift+L."); }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Controls] Could not migrate legacy companion card-style binding: " + ex.Message);
        }
    }

    private static bool IsLegacyShiftCBinding(string bindingText)
    {
        if (string.IsNullOrEmpty(bindingText))
            return false;

        char[] buffer = new char[bindingText.Length];
        int count = 0;
        for (int i = 0; i < bindingText.Length; i++)
        {
            char ch = bindingText[i];
            if (char.IsLetterOrDigit(ch))
                buffer[count++] = char.ToLowerInvariant(ch);
        }

        string compact = new string(buffer, 0, count);
        return compact == "shiftc" || compact == "cshift";
    }

    internal static void PlayerInputManagerCtorPostfix(PlayerInputManager __instance)
    {
        EnsureRegistered(__instance, "PlayerInputManager-constructor");
    }

    private static string RebirthTabCaption()
    {
        string caption = Localization.Get("inpTabRebirth");
        if (string.IsNullOrEmpty(caption) || caption.Equals("inpTabRebirth", StringComparison.OrdinalIgnoreCase))
            caption = "REBIRTH";
        return caption;
    }

    private static List<PlayerAction> GetKeyboardActionsInDisplayOrder()
    {
        List<PlayerAction> result = new List<PlayerAction>();
        if (Actions == null)
            return result;

        SortedDictionary<PlayerActionData.ActionGroup, List<PlayerAction>> groups =
            new SortedDictionary<PlayerActionData.ActionGroup, List<PlayerAction>>();

        foreach (PlayerAction action in Actions.Actions)
        {
            PlayerActionData.ActionUserData data = action.UserData as PlayerActionData.ActionUserData;
            if (data == null || data.doNotDisplay ||
                data.appliesToInputType == PlayerActionData.EAppliesToInputType.None ||
                data.appliesToInputType == PlayerActionData.EAppliesToInputType.ControllerOnly)
            {
                continue;
            }

            List<PlayerAction> groupActions;
            if (!groups.TryGetValue(data.actionGroup, out groupActions))
            {
                groupActions = new List<PlayerAction>();
                groups.Add(data.actionGroup, groupActions);
            }
            groupActions.Add(action);
        }

        int groupIndex = 0;
        foreach (KeyValuePair<PlayerActionData.ActionGroup, List<PlayerAction>> group in groups)
        {
            // null is an intentional stock-style visual spacer between REBIRTH groups.
            if (groupIndex++ > 0)
                result.Add(null);
            result.AddRange(group.Value);
        }

        return result;
    }

    private static List<PlayerAction> GetControllerActionsInDisplayOrder()
    {
        List<PlayerAction> result = new List<PlayerAction>();
        if (Actions == null)
            return result;

        foreach (PlayerAction action in Actions.ControllerRebindableActions)
        {
            PlayerActionData.ActionUserData data = action.UserData as PlayerActionData.ActionUserData;
            if (data == null || data.doNotDisplay ||
                data.appliesToInputType == PlayerActionData.EAppliesToInputType.None ||
                data.appliesToInputType == PlayerActionData.EAppliesToInputType.KbdMouseOnly)
            {
                continue;
            }
            result.Add(action);
        }

        return result;
    }

    private static int PopulateLastBindingTab(
        XUiC_OptionsControlsBase options,
        List<PlayerAction> actions,
        int stockTabCount,
        string screenName)
    {
        if (options == null || options.TabSelector == null)
            return 0;

        XUiC_TabSelector selector = options.TabSelector;
        if (selector.Tabs == null || selector.Tabs.Length <= stockTabCount)
        {
            Log.Error("[REBIRTH Controls] " + screenName +
                      " Controls XML has no appended REBIRTH binding tab. tabs=" +
                      (selector.Tabs == null ? 0 : selector.Tabs.Length) +
                      " expected>" + stockTabCount + ".");
            return 0;
        }

        int tabIndex = selector.Tabs.Length - 1;
        XUiC_TabSelectorTab tab = selector.GetTab(tabIndex);
        if (tab == null)
        {
            Log.Error("[REBIRTH Controls] Could not resolve appended " + screenName + " REBIRTH tab index=" + tabIndex + ".");
            return 0;
        }

        selector.SetTabCaption(tabIndex, RebirthTabCaption());

        XUiC_OptionsControlsBase.XUiC_BindingEntry[] rows =
            tab.GetChildrenByType<XUiC_OptionsControlsBase.XUiC_BindingEntry>();

        if (rows == null || rows.Length == 0)
        {
            Log.Error("[REBIRTH Controls] Appended " + screenName + " REBIRTH tab has no binding rows.");
            return 0;
        }

        int count = Math.Min(actions.Count, rows.Length);
        for (int i = 0; i < rows.Length; i++)
            rows[i].Action = i < count ? actions[i] : null;

        options.IsDirty = true;
        { if (RebirthLogSettings.UiRouteLoggingEnabled) Log.Out("[REBIRTH Controls] Populated " + screenName + " REBIRTH tab index=" + tabIndex +
                " rows=" + rows.Length + " entries=" + count + "."); }
        return count;
    }

    /// <summary>
    /// Let 3.1 populate every stock keyboard tab with its native method, then populate only
    /// the extra tab appended by REBIRTH. This avoids reimplementing or disturbing stock tabs.
    /// </summary>
    internal static void KeyboardCreateControlsEntriesPostfix(XUiC_OptionsControls __instance)
    {
        if (__instance == null || !EnsureRegisteredFromNativePlatform("keyboard-controls-ui"))
            return;

        PopulateLastBindingTab(
            __instance,
            GetKeyboardActionsInDisplayOrder(),
            StockKeyboardTabCount,
            "keyboard");
    }

    /// <summary>
    /// Preserve the stock controller binding tabs and the native Layout tab. The REBIRTH
    /// controller bindings are assigned only to the additional final tab supplied by the mod XML.
    /// </summary>
    internal static void ControllerCreateControlsEntriesPostfix(XUiC_OptionsController __instance)
    {
        if (__instance == null || !EnsureRegisteredFromNativePlatform("controller-controls-ui"))
            return;

        PopulateLastBindingTab(
            __instance,
            GetControllerActionsInDisplayOrder(),
            StockControllerTabCount,
            "controller");
    }

    /// <summary>
    /// Vanilla 3.1's new-binding dialog records only one conflictingAction even when a
    /// physical key/button is currently bound to several actions. Keep the stock conflict
    /// decision/Confirm behavior intact, but replace its warning text with a complete scan
    /// of every registered action set using the candidate binding.
    /// </summary>
    internal static void BindingConflictDisplayPostfix(
        XUiC_OptionsControlsNewBinding __instance,
        BindingSource _binding,
        PlayerAction _selfAction,
        bool __result)
    {
        if (!__result || __instance == null || _binding == null)
            return;

        PlayerInputManager input = PlatformManager.NativePlatform == null
            ? null
            : PlatformManager.NativePlatform.Input;
        if (input == null)
            return;

        List<PlayerAction> boundActions = new List<PlayerAction>();
        foreach (PlayerActionSet actionSet in input.ActionSets)
        {
            if (actionSet == null)
                continue;

            foreach (PlayerAction candidate in actionSet.Actions)
            {
                if (candidate == null || candidate == _selfAction ||
                    !candidate.Bindings.Contains(_binding) || boundActions.Contains(candidate))
                {
                    continue;
                }

                boundActions.Add(candidate);
            }
        }

        // One match is exactly what vanilla already displays correctly.
        if (boundActions.Count <= 1)
            return;

        List<string> names = new List<string>(boundActions.Count);
        List<string> descriptions = new List<string>(boundActions.Count);
        foreach (PlayerAction candidate in boundActions)
        {
            PlayerActionData.ActionUserData data = candidate.UserData as PlayerActionData.ActionUserData;
            string name = data == null ? candidate.Name : data.LocalizedName;
            if (string.IsNullOrEmpty(name))
                name = candidate.Name;
            names.Add(name);

            if (data != null && !string.IsNullOrEmpty(data.LocalizedDescription))
                descriptions.Add(name + ": " + data.LocalizedDescription);
        }

        XUiController inUseController = __instance.GetChildById("inUseBy");
        XUiV_Label inUseLabel = inUseController == null
            ? null
            : inUseController.ViewComponent as XUiV_Label;
        if (inUseLabel == null)
            return;

        bool forController = _binding.BindingSourceType == BindingSourceType.DeviceBindingSource;
        string bindingText = _binding.Name;
        if (forController && boundActions.Count > 0)
        {
            bindingText = boundActions[0].GetBindingString(
                true,
                PlatformManager.NativePlatform.Input.CurrentControllerInputStyle);
        }

        string localizationKey = forController
            ? "xuiNewBindingConflictingAction_Controller"
            : "xuiNewBindingConflictingAction";
        inUseLabel.Text = string.Format(
            Localization.Get(localizationKey),
            (object)bindingText,
            (object)string.Join(", ", names.ToArray()));

        if (descriptions.Count > 0)
            inUseLabel.ToolTip = string.Join("\n", descriptions.ToArray());
    }

    private static int cruiseState;
    private static int lastToggleFrame = -1;
    private static int lastQuickStackFrame = -1;
    private static int lastCompanionTeleportFrame = -1;
    private static int lastCompanionCardStyleFrame = -1;
    private static int lastCopyShapeRotationFrame = -1;
    private static int lastOreSenseFrame = -1;
    private static int activePlayerEntityId = -1;
    private static float nextQuickStackTraceHeartbeatRealtime;
    private static float nextQuickStackTraceSampleRealtime;

    public static int GetCruiseState(EntityPlayerLocal player)
    {
        if (player == null || player.entityId != activePlayerEntityId || !(player.AttachedToEntity is EntityVehicle))
            return 0;
        return cruiseState;
    }

    internal static void ResetQuickStackHotkeyTraceHeartbeat()
    {
        nextQuickStackTraceHeartbeatRealtime = 0f;
        nextQuickStackTraceSampleRealtime = 0f;
    }

    private static string DescribeAttachedEntity(EntityPlayerLocal player)
    {
        if (player == null)
            return "<player-null>";
        Entity attached = player.AttachedToEntity;
        return attached == null
            ? "<none>"
            : attached.GetType().Name + "#" + attached.entityId;
    }

    private static string DescribeActionBinding(PlayerAction action)
    {
        if (action == null)
            return "<action-null>";

        try
        {
            string value = action.GetBindingString(false);
            return string.IsNullOrEmpty(value) ? "<unbound>" : value;
        }
        catch (Exception ex)
        {
            return "<binding-error:" + ex.GetType().Name + ">";
        }
    }

    private static string DescribeOnFootBlockReason(EntityPlayerLocal player)
    {
        if (player == null)
            return "player-null";
        if (Actions == null)
            return "actions-null";
        if (!Actions.Enabled)
            return "action-set-disabled";
        if (player.AttachedToEntity is EntityVehicle)
            return "attached-vehicle:" + DescribeAttachedEntity(player);
        if (player.IsDead())
            return "player-dead";
        if (player.PlayerUI == null)
            return "player-ui-null";
        if (player.PlayerUI.windowManager == null)
            return "window-manager-null";
        if (IsRebirthPersonalCraftingOpen(player))
            return "rebirth-personal-crafting-open";
        if (player.PlayerUI.windowManager.IsInputActive())
            return "window-input-active";
        if (player.PlayerUI.windowManager.IsModalWindowOpen())
            return "modal-window-open";
        return "none";
    }

    private static bool HasDebugGodBindingConflict(PlayerAction action)
    {
        if (action == null || !GamePrefs.GetBool(EnumGamePrefs.DebugMenuEnabled))
            return false;

        try
        {
            IPlatform platform = PlatformManager.NativePlatform;
            PlayerInputManager input = platform == null ? null : platform.Input;
            PlayerActionsLocal local = input == null ? null : input.PrimaryPlayer;
            PlayerAction god = local == null ? null : local.God;
            if (god == null)
                return false;

            foreach (BindingSource binding in action.Bindings)
            {
                if (binding != null && god.Bindings.Contains(binding))
                    return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static string DescribeQuickStackHotkeyBlockReason(EntityPlayerLocal player)
    {
        if (player == null)
            return "player-null";
        if (player.AttachedToEntity is EntityVehicle)
            return "attached-vehicle";
        if (HasDebugGodBindingConflict(Actions == null ? null : Actions.QuickStack))
            return "debug-god-binding-conflict";
        return "none";
    }

    private static string DescribeCompanionTeleportHotkeyBlockReason(EntityPlayerLocal player)
    {
        if (player == null)
            return "player-null";
        if (player.AttachedToEntity is EntityVehicle)
            return "attached-vehicle";
        if (HasDebugGodBindingConflict(Actions == null ? null : Actions.CompanionTeleport))
            return "debug-god-binding-conflict";
        return "none";
    }

    public static string GetQuickStackHotkeyTraceStatus(EntityPlayerLocal player)
    {
        PlayerAction quickStack = Actions == null ? null : Actions.QuickStack;
        PlayerAction companionTeleport = Actions == null ? null : Actions.CompanionTeleport;
        PlayerAction cruise = Actions == null ? null : Actions.ToggleCruiseControl;
        PlayerAction cardStyle = Actions == null ? null : Actions.ToggleCompanionCardStyle;
        PlayerAction copy = Actions == null ? null : Actions.CopyShapeRotation;

        bool inputManagerResolved = false;
        bool registered = false;
        int actionSetCount = 0;
        try
        {
            IPlatform platform = PlatformManager.NativePlatform;
            PlayerInputManager input = platform == null ? null : platform.Input;
            inputManagerResolved = input != null;
            if (input != null)
            {
                foreach (PlayerActionSet set in input.ActionSets)
                {
                    actionSetCount++;
                    if (object.ReferenceEquals(set, Actions))
                        registered = true;
                }
            }
        }
        catch
        {
        }

        bool inputActive = player != null && player.PlayerUI != null &&
                           player.PlayerUI.windowManager != null &&
                           player.PlayerUI.windowManager.IsInputActive();
        bool modalOpen = player != null && player.PlayerUI != null &&
                         player.PlayerUI.windowManager != null &&
                         player.PlayerUI.windowManager.IsModalWindowOpen();

        return "frame=" + Time.frameCount +
               " player=" + (player == null ? "<null>" : player.entityId.ToString()) +
               " actions=" + (Actions == null ? "<null>" : "present") +
               " actionsEnabled=" + (Actions != null && Actions.Enabled) +
               " inputManager=" + inputManagerResolved +
               " registered=" + registered +
               " actionSets=" + actionSetCount +
               " quickBinding='" + DescribeActionBinding(quickStack) + "'" +
               " quickWasPressed=" + (quickStack != null && quickStack.WasPressed) +
               " quickIsPressed=" + (quickStack != null && quickStack.IsPressed) +
               " quickWasReleased=" + (quickStack != null && quickStack.WasReleased) +
               " teleportBinding='" + DescribeActionBinding(companionTeleport) + "'" +
               " teleportWasPressed=" + (companionTeleport != null && companionTeleport.WasPressed) +
               " teleportIsPressed=" + (companionTeleport != null && companionTeleport.IsPressed) +
               " cruiseWasPressed=" + (cruise != null && cruise.WasPressed) +
               " cardStyleWasPressed=" + (cardStyle != null && cardStyle.WasPressed) +
               " copyWasPressed=" + (copy != null && copy.WasPressed) +
               " rawAnyKeyDown=" + Input.anyKeyDown +
               " rawQDown=" + Input.GetKeyDown(KeyCode.Q) +
               " dead=" + (player != null && player.IsDead()) +
               " god=" + (player != null && player.IsGodMode.Value) +
               " debug=" + GamePrefs.GetBool(EnumGamePrefs.DebugMenuEnabled) +
               " attached=" + DescribeAttachedEntity(player) +
               " inputActive=" + inputActive +
               " modalOpen=" + modalOpen +
               " onFootBlock=" + DescribeOnFootBlockReason(player) +
               " quickHotkeyBlock=" + DescribeQuickStackHotkeyBlockReason(player) +
               " teleportHotkeyBlock=" + DescribeCompanionTeleportHotkeyBlockReason(player) +
               " policyEnabled=" + QuickStackRuntimePolicy.Enabled +
               " policyMode=" + QuickStackRuntimePolicy.Mode +
               " radius=" + QuickStackRuntimePolicy.Radius +
               " lastQuickStackFrame=" + lastQuickStackFrame;
    }

    internal static void LogQuickStackHotkeySnapshot(string reason, EntityPlayerLocal player)
    {
        if (!QuickStackHotkeyDiagnostics.Enabled) return;
        { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write("snapshot reason=" + reason + " " + GetQuickStackHotkeyTraceStatus(player)); }
    }

    private static bool HasQuickStackTraceInputSignal()
    {
        if (Input.anyKeyDown || Input.GetKeyDown(KeyCode.Q))
            return true;

        if (Actions == null)
            return false;

        return (Actions.QuickStack != null &&
                (Actions.QuickStack.WasPressed || Actions.QuickStack.IsPressed || Actions.QuickStack.WasReleased)) ||
               (Actions.CompanionTeleport != null &&
                (Actions.CompanionTeleport.WasPressed || Actions.CompanionTeleport.IsPressed || Actions.CompanionTeleport.WasReleased)) ||
               (Actions.ToggleCruiseControl != null && Actions.ToggleCruiseControl.WasPressed) ||
               (Actions.ToggleCompanionCardStyle != null && Actions.ToggleCompanionCardStyle.WasPressed) ||
               (Actions.CopyShapeRotation != null && Actions.CopyShapeRotation.WasPressed);
    }

    private static void TraceQuickStackHotkeyFrame(EntityPlayerLocal player)
    {
        if (!QuickStackHotkeyDiagnostics.Enabled)
            return;

        float now = Time.realtimeSinceStartup;
        if (now < nextQuickStackTraceSampleRealtime) return;
        nextQuickStackTraceSampleRealtime = now + 0.1f;
        bool inputSignal = HasQuickStackTraceInputSignal();
        if (!inputSignal && now < nextQuickStackTraceHeartbeatRealtime)
            return;

        nextQuickStackTraceHeartbeatRealtime = now + 2f;
        LogQuickStackHotkeySnapshot(inputSignal ? "input" : "heartbeat", player);
    }

    private static bool TryConsumeCruiseToggle(EntityPlayerLocal player)
    {
        if (!CanDispatchCruiseAction(player))
            return false;

        if (!Actions.ToggleCruiseControl.WasPressed)
            return false;

        int frame = Time.frameCount;
        if (lastToggleFrame == frame)
            return false;
        lastToggleFrame = frame;

        activePlayerEntityId = player.entityId;
        cruiseState = cruiseState == 0 ? 1 : cruiseState == 1 ? 2 : 0;
        return true;
    }

    private static bool CanDispatchCruiseAction(EntityPlayerLocal player)
    {
        if (player == null || Actions == null || !Actions.Enabled)
            return false;
        if (!(player.AttachedToEntity is EntityVehicle))
            return false;
        if (player.PlayerUI == null || player.PlayerUI.windowManager == null)
            return false;
        return !player.PlayerUI.windowManager.IsInputActive();
    }

    private static bool CanDispatchOnFootAction(EntityPlayerLocal player)
    {
        if (player == null || Actions == null || !Actions.Enabled)
            return false;
        if (player.AttachedToEntity is EntityVehicle || player.IsDead())
            return false;
        if (player.PlayerUI == null || player.PlayerUI.windowManager == null)
            return false;
        if (IsRebirthPersonalCraftingOpen(player))
            return false;
        return !player.PlayerUI.windowManager.IsInputActive() &&
               !player.PlayerUI.windowManager.IsModalWindowOpen();
    }

    private static bool IsRebirthPersonalCraftingOpen(EntityPlayerLocal player)
    {
        if (player == null || player.PlayerUI == null || player.PlayerUI.windowManager == null ||
            player.PlayerUI.xui == null)
            return false;
        if (!player.PlayerUI.windowManager.IsWindowOpen("crafting"))
            return false;

        XUiController group = player.PlayerUI.xui.FindWindowGroupByName("crafting");
        XUiC_RebirthPersonalCrafting controller = group as XUiC_RebirthPersonalCrafting;
        if (controller == null && group != null)
            controller = group.GetChildByType<XUiC_RebirthPersonalCrafting>();
        return controller != null && controller.State != null && controller.State.IsOpen;
    }


    private static bool CanDispatchQuickStackHotkey(EntityPlayerLocal player)
    {
        if (player == null)
            return false;

        // Debug mode alone does not suppress Quick Stack. It is ignored only when the
        // configured Quick Stack binding actually overlaps the configured stock God Mode
        // binding, which preserves the stock Q debug action without disabling unrelated
        // REBIRTH bindings. Vehicle attachment is always a hard shortcut gate.
        if (player.AttachedToEntity is EntityVehicle)
            return false;
        if (HasDebugGodBindingConflict(Actions == null ? null : Actions.QuickStack))
            return false;

        return true;
    }

    private static bool CanDispatchCompanionTeleportHotkey(EntityPlayerLocal player)
    {
        if (player == null)
            return false;
        if (player.AttachedToEntity is EntityVehicle)
            return false;
        if (HasDebugGodBindingConflict(Actions == null ? null : Actions.CompanionTeleport))
            return false;
        return true;
    }

    private static void TryConsumeCompanionCardStyleToggle(EntityPlayerLocal player)
    {
        if (player == null || Actions == null || !Actions.Enabled || Actions.ToggleCompanionCardStyle == null)
            return;
        if (!Actions.ToggleCompanionCardStyle.WasPressed)
            return;
        if (player.PlayerUI == null || player.PlayerUI.windowManager == null ||
            IsRebirthPersonalCraftingOpen(player) ||
            player.PlayerUI.windowManager.IsInputActive() || player.PlayerUI.windowManager.IsModalWindowOpen())
            return;

        int frame = Time.frameCount;
        if (lastCompanionCardStyleFrame == frame) return;
        lastCompanionCardStyleFrame = frame;
        RebirthCompanionCardStyleRuntime.Toggle(player);
    }

    private static void TryConsumeOnFootActions(EntityPlayerLocal player)
    {
        bool rawQDown = Input.GetKeyDown(KeyCode.Q);
        bool quickWasPressed = Actions != null &&
                               Actions.QuickStack != null &&
                               Actions.QuickStack.WasPressed;
        bool teleportWasPressed = Actions != null &&
                                  Actions.CompanionTeleport != null &&
                                  Actions.CompanionTeleport.WasPressed;

        if (!CanDispatchOnFootAction(player))
        {
            if (QuickStackHotkeyDiagnostics.Enabled && (rawQDown || quickWasPressed || teleportWasPressed))
            {
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                    "dispatcher blocked before actions reason=" + DescribeOnFootBlockReason(player) +
                    " rawQDown=" + rawQDown +
                    " quickWasPressed=" + quickWasPressed +
                    " teleportWasPressed=" + teleportWasPressed); }
            }
            return;
        }

        int frame = Time.frameCount;

        if (player.AttachedToEntity == null &&
            Actions.CopyShapeRotation != null &&
            Actions.CopyShapeRotation.WasPressed &&
            lastCopyShapeRotationFrame != frame)
        {
            lastCopyShapeRotationFrame = frame;
            RebirthBuildingHotkeyService.TryCopyShapeAndRotation(player);
        }

        if (Actions.OreSense != null && Actions.OreSense.WasPressed && lastOreSenseFrame != frame)
        {
            lastOreSenseFrame = frame;
            RebirthOreSenseService.TryToggle(player);
        }

        if (QuickStackHotkeyDiagnostics.Enabled && rawQDown && !quickWasPressed && !teleportWasPressed)
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "raw Q was detected but neither REBIRTH Q action fired quickBinding='" +
                DescribeActionBinding(Actions == null ? null : Actions.QuickStack) +
                "' teleportBinding='" +
                DescribeActionBinding(Actions == null ? null : Actions.CompanionTeleport) +
                "' cruiseWasPressed=" +
                (Actions != null && Actions.ToggleCruiseControl != null && Actions.ToggleCruiseControl.WasPressed)); }
        }

        // A chord such as the default Shift+Q can also make a simple Q action report a
        // press on some input backends. A Quick Stack WasPressed signal always owns that
        // frame, even if its later gate blocks dispatch, so a blocked Shift+Q can never
        // fall through and become Companion Teleport.
        if (quickWasPressed)
        {
            string hotkeyBlock = DescribeQuickStackHotkeyBlockReason(player);
            if (!CanDispatchQuickStackHotkey(player))
            {
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                    "quickstack press blocked hotkeyGate=" + hotkeyBlock +
                    " frame=" + frame); }
                return;
            }

            if (lastQuickStackFrame == frame)
            {
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                    "quickstack press blocked duplicate-frame frame=" + frame); }
                return;
            }

            lastQuickStackFrame = frame;

            if (!QuickStackRuntimePolicy.Enabled)
            {
                { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                    "quickstack press blocked runtime-policy mode=" + QuickStackRuntimePolicy.Mode); }
                return;
            }

            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "quickstack press accepted; dispatching RequestAllEligibleDeposit frame=" + frame +
                " mode=" + QuickStackRuntimePolicy.Mode +
                " radius=" + QuickStackRuntimePolicy.Radius); }
            LogisticsTransferService.RequestAllEligibleDeposit();
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "RequestAllEligibleDeposit returned to hotkey dispatcher frame=" + frame); }
            return;
        }

        if (!teleportWasPressed)
            return;

        string teleportBlock = DescribeCompanionTeleportHotkeyBlockReason(player);
        if (!CanDispatchCompanionTeleportHotkey(player))
        {
            { if (QuickStackHotkeyDiagnostics.Enabled) QuickStackHotkeyDiagnostics.Write(
                "companion teleport press blocked hotkeyGate=" + teleportBlock +
                " frame=" + frame); }
            return;
        }

        if (lastCompanionTeleportFrame == frame)
            return;

        lastCompanionTeleportFrame = frame;
        RebirthCompanionService.RequestTeleportFollowers();
    }

    internal static void PlayerMoveControllerUpdatePostfix(PlayerMoveController __instance)
    {
        EntityPlayerLocal player = __instance == null ? null : __instance.entityPlayerLocal;
        if (player == null)
            return;

        // GUIWindowConsole is a legacy GUI surface rather than an XUi modal. Do not dispatch
        // REBIRTH gameplay shortcuts from keystrokes that are being typed into the console.
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
            return;

        // Defensive recovery for unusual input-manager reconstruction/order cases.
        if (Actions == null)
            EnsureRegisteredFromNativePlatform("gameplay-dispatch");

        TraceQuickStackHotkeyFrame(player);
        RebirthCompanionColorService.RefreshLocalNavColors(player);
        RebirthCompanionCollisionService.RefreshOwnedCompanionPlayerCollision(player);
        TryConsumeCompanionCardStyleToggle(player);

        if (!(player.AttachedToEntity is EntityVehicle))
        {
            if (player.entityId == activePlayerEntityId)
            {
                cruiseState = 0;
                activePlayerEntityId = -1;
            }

            TryConsumeOnFootActions(player);
            return;
        }

        TryConsumeCruiseToggle(player);
    }

    internal static void MoveByAttachedEntityPostfix(
        EntityVehicle __instance,
        EntityPlayerLocal _player,
        ref MovementInput ___movementInput)
    {
        if (_player == null || __instance == null)
            return;

        // PC127: this vehicle postfix is independent from PlayerMoveController.Update. Do not
        // inject cruise movement or consume its shortcut behind the console's text input.
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
        {
            if (___movementInput != null) ___movementInput.Clear();
            return;
        }

        TryConsumeCruiseToggle(_player);

        LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(_player);
        PlayerActionsVehicle vehicleActions = ui == null ? null : ui.playerInput.VehicleActions;
        if (vehicleActions != null &&
            (vehicleActions.Move.Y > 0f ||
             vehicleActions.MoveBack.IsPressed ||
             (vehicleActions.Brake.IsPressed && !(__instance is EntityVHelicopter) && !(__instance is EntityVGyroCopter))))
        {
            cruiseState = 0;
        }

        if (_player.entityId != activePlayerEntityId)
            return;

        if (cruiseState == 1)
        {
            ___movementInput.moveForward = 1f;
            ___movementInput.running = false;
        }
        else if (cruiseState == 2)
        {
            ___movementInput.moveForward = 1f;
            ___movementInput.running = true;
        }
    }
}

[HarmonyPatch(typeof(PlayerInputManager), MethodType.Constructor)]
internal static class RebirthPlayerInputManagerConstructorPatch
{
    private static void Postfix(PlayerInputManager __instance)
    {
        RebirthNativeControls.PlayerInputManagerCtorPostfix(__instance);
    }
}

[HarmonyPatch(typeof(XUiC_OptionsControls), "createControlsEntries")]
internal static class RebirthKeyboardControlsEntriesPatch
{
    private static void Postfix(XUiC_OptionsControls __instance)
    {
        RebirthNativeControls.KeyboardCreateControlsEntriesPostfix(__instance);
    }
}

[HarmonyPatch(typeof(XUiC_OptionsController), "createControlsEntries")]
internal static class RebirthControllerControlsEntriesPatch
{
    private static void Postfix(XUiC_OptionsController __instance)
    {
        RebirthNativeControls.ControllerCreateControlsEntriesPostfix(__instance);
    }
}

[HarmonyPatch(typeof(XUiC_OptionsControlsNewBinding), "alreadyBound")]
internal static class RebirthBindingConflictDisplayPatch
{
    private static void Postfix(
        XUiC_OptionsControlsNewBinding __instance,
        BindingSource _binding,
        PlayerAction _selfAction,
        bool __result)
    {
        RebirthNativeControls.BindingConflictDisplayPostfix(
            __instance,
            _binding,
            _selfAction,
            __result);
    }
}

[HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
internal static class RebirthCruisePlayerUpdatePatch
{
    private static void Postfix(PlayerMoveController __instance)
    {
        RebirthNativeControls.PlayerMoveControllerUpdatePostfix(__instance);
    }
}

[HarmonyPatch(typeof(EntityVehicle), nameof(EntityVehicle.MoveByAttachedEntity))]
internal static class RebirthCruiseVehicleMovementPatch
{
    private static void Postfix(
        EntityVehicle __instance,
        EntityPlayerLocal _player,
        ref MovementInput ___movementInput)
    {
        RebirthNativeControls.MoveByAttachedEntityPostfix(__instance, _player, ref ___movementInput);
    }
}
