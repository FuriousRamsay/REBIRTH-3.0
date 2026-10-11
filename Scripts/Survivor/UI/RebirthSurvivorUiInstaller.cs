using System;
using System.Collections.Generic;
using HarmonyLib;
using Platform;

#nullable disable

public static class RebirthSurvivorUiInstaller
{
    private static bool installed;
    private static readonly Harmony Harmony = new Harmony("rebirth.survivor-ui.3.1");

    public static string Install()
    {
        if (installed) return "survivor UI already installed";
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCreativeCloseInputPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthBackpackSectionEscapeReturnPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorMainMenuButtonsInitPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorShortcutRoutePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorSelectedPageRoutePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthProgressionRadialMenuRoutePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthQuestDetailsTradeContextPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthQuestDetailsEmptyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthQuestBackpackActionContextPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthQuestBackpackTraderDataPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorBackpackSetStacksPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterEquipmentInspect));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorGearQuickEquip));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterDragMenuPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthItemModifyCapturePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthItemModifyReturnPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCosmeticsReturnPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCraftingQualityCountPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPersonalCraftPositiveCountPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPersonalCraftActivationCountPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthPersonalCraftQueueAdmissionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorNativePlayerSpawnedPatch));
        // Context navigation is QoL layered on top of the authoritative native windows. A game
        // update changing one of those window lifecycles must never abort the core Survivor UI
        // installer (spawn/profile/creator ownership). Install these patches independently and
        // retain complete native container presentation if any required hook is unavailable.
        RebirthBulkTransferRefresh.Install(Harmony);
        bool contextReady = true;
        contextReady &= PatchOptionalContext(typeof(RebirthContextLootOpenPatch));
        contextReady &= PatchOptionalContext(typeof(RebirthContextLootClosePatch));
        contextReady &= PatchOptionalContext(typeof(RebirthContextBagStorageOpenPatch));
        contextReady &= PatchOptionalContext(typeof(RebirthContextBagStorageClosePatch));
        contextReady &= PatchOptionalContext(typeof(RebirthContextNativePresentationUpdatePatch));
        contextReady &= PatchOptionalContext(typeof(RebirthContextParkedActionSetPatch));
        contextReady &= PatchOptionalContext(typeof(RebirthContextHiddenTransferPatch));
        RebirthContextNavigationService.IntegrationReady = contextReady;
        if (RebirthSurvivorDebug.Enabled)
            Log.Out("[REBIRTH Container UI] integrationReady=" + contextReady + " layout=approved-11x11-v19");
        installed = true;
        return "survivor profile/creator/character/backpack UI integration installed + integrated native spawn-profile browser + native ESC ownership + staged-commit patch";
    }

    private static bool PatchOptionalContext(Type patchType)
    {
        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony, patchType);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor UI] optional context-navigation patch skipped class=" +
                (patchType != null ? patchType.Name : "<null>") + " error=" +
                ex.GetType().Name + ": " + ex.Message);
            return false;
        }
    }
}

[HarmonyPatch(typeof(XUiC_MainMenuButtons), nameof(XUiC_MainMenuButtons.Init))]
public static class RebirthSurvivorMainMenuButtonsInitPatch
{
    [HarmonyPostfix]
    public static void Postfix(XUiC_MainMenuButtons __instance)
    {
        { if (RebirthLogSettings.ExitTraceLoggingEnabled) RebirthLogSettings.TraceExit("XUiC_MainMenuButtons.Init postfix instance=" + (__instance != null)); }
        RebirthSurvivorMainMenuIntegration.TryWire(__instance);
        RebirthProgressionExplorerMainMenuIntegration.TryWire(__instance);
    }
}

public static class RebirthSurvivorMainMenuIntegration
{
    public const string ButtonId = "btnRebirthSurvivorProfiles";
    public const string WindowGroupId = "rebirthSurvivorProfiles";

    private static readonly object Sync = new object();
    private static readonly HashSet<XUiController> WiredButtons = new HashSet<XUiController>();

    public static void TryWire(XUiC_MainMenuButtons controller)
    {
        if (controller == null) return;
        XUiController button = controller.GetChildById(ButtonId);
        { if (RebirthLogSettings.ExitTraceLoggingEnabled) RebirthLogSettings.TraceExit("main-menu wire attempt survivorButton=" + (button != null)); }
        if (button == null)
        {
            if (RebirthSurvivorDebug.Enabled)
                Log.Warning("[REBIRTH Survivor UI] main-menu button '" + ButtonId + "' was not found; vanilla menu left unchanged.");
            return;
        }

        bool rebirthMode = RebirthSurvivorMode.ConfiguredMode == RebirthPlayerProgressionMode.Rebirth;
        if (button.ViewComponent != null)
        {
            button.ViewComponent.IsVisible = rebirthMode;
            button.ViewComponent.Enabled = rebirthMode;
        }

        lock (Sync)
        {
            if (WiredButtons.Contains(button)) return;
            WiredButtons.Add(button);
        }
        button.OnPress += OnSurvivorProfilesPressed;
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("MAIN-MENU wiring survivorProfilesHook=True playGameHook=False rebirthMode=" + rebirthMode); }

        // CRITICAL: do not inspect, subscribe to, close, reopen, or otherwise participate in
        // btnPlayGame. The native MainMenuButtons controller owns Play Game and its Player
        // Profile page end-to-end. REBIRTH wires only its own added button above.
    }

    private static void OnSurvivorProfilesPressed(XUiController sender, int mouseButton)
    {
        if (sender == null || sender.xui == null || sender.xui.playerUI == null) return;
        XUi xui = sender.xui;
        GUIWindowManager manager = xui.playerUI.windowManager;
        if (manager == null) return;
        { if (RebirthLogSettings.UiRouteLoggingEnabled) RebirthLogSettings.TraceUiRoute("MAIN-MENU Survivor Profiles pressed mouse=" + mouseButton +
            " mainMenu=" + manager.IsWindowOpen("mainMenu") +
            " playGamePaging=" + manager.IsWindowOpen("playGamePaging") +
            " playerProfiles=" + manager.IsWindowOpen("playerProfiles") +
            " creator=" + manager.IsWindowOpen(XUiC_RebirthSurvivorCreator.WindowGroupId)); }
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth)
        {
            Log.Warning("[REBIRTH Survivor][UiRoute] Survivor Profiles main-menu destination ignored because Character Progression is not Rebirth.");
            return;
        }

        XUiController target = xui.FindWindowGroupByName(WindowGroupId);
        if (target == null || target.windowGroup == null)
        {
            Log.Error("[REBIRTH Survivor][UiRoute] Survivor Profiles window group '" + WindowGroupId + "' is not registered; leaving Main Menu open.");
            return;
        }

        XUiC_RebirthSurvivorCreator.PrepareForRebirthMenuNavigation(xui, "main-menu-survivor-profiles");
        if (manager.IsWindowOpen("playerProfilesCreate")) manager.Close("playerProfilesCreate");
        if (manager.IsWindowOpen("playerProfiles")) manager.Close("playerProfiles");
        if (manager.IsWindowOpen("playGamePaging")) manager.Close("playGamePaging");

        // V3.2 modal Open closes the Main Menu itself. The registration preflight above is
        // therefore the critical safety boundary: an unknown destination can never blank the UI.
        if (!XUiC_RebirthSurvivorProfileManager.OpenStandard(xui))
            Log.Error("[REBIRTH Survivor][UiRoute] Survivor Profiles failed to open; fallback Main Menu restoration was requested.");
    }


}

/// <summary>
/// PE-06 player-facing Main Menu entry for the neutral Progression Explorer.
/// This integration owns only the REBIRTH-added button and deliberately does not
/// subscribe to or alter vanilla Main Menu destinations.
/// </summary>
public static class RebirthProgressionExplorerMainMenuIntegration
{
    public const string ButtonId = "btnRebirthProgressionExplorer";

    private static readonly object Sync = new object();
    private static readonly HashSet<XUiController> WiredButtons = new HashSet<XUiController>();

    public static void TryWire(XUiC_MainMenuButtons controller)
    {
        if (controller == null) return;
        XUiController button = controller.GetChildById(ButtonId);
        { if (RebirthLogSettings.ProgressionExplorerLoggingEnabled) RebirthLogSettings.TraceProgressionExplorer("main-menu wire attempt explorerButton=" + (button != null)); }
        if (button == null)
        {
            if (RebirthSurvivorDebug.Enabled)
                Log.Warning("[REBIRTH Progression Explorer] main-menu button '" + ButtonId + "' was not found; vanilla menu left unchanged.");
            return;
        }

        lock (Sync)
        {
            if (WiredButtons.Contains(button)) return;
            WiredButtons.Add(button);
        }
        bool survivorReady = RebirthSurvivorDefinitionRegistry.IsReady;
        if (survivorReady && !RebirthProgressionGraphRegistry.IsReady)
        {
            try { RebirthProgressionGraphRegistry.BuildFromCurrentAuthority(); }
            catch (Exception ex) { Log.Error("[REBIRTH Progression Explorer][PE-06] eager graph build failed; button remains enabled for retry: "+ex.GetType().Name+": "+ex.Message); }
        }
        bool rebirthMode = RebirthSurvivorMode.ConfiguredMode == RebirthPlayerProgressionMode.Rebirth;
        // V3.2 XUi_Menu cannot conditionally register patches with <if>, so player-facing
        // isolation is enforced here. In Rebirth mode the press path still performs a fresh
        // authority retry instead of permanently disabling the destination.
        if (button.ViewComponent != null)
        {
            button.ViewComponent.IsVisible = rebirthMode;
            button.ViewComponent.Enabled = rebirthMode;
        }
        button.OnPress += OnProgressionExplorerPressed;
        { if (RebirthLogSettings.ProgressionExplorerLoggingEnabled) RebirthLogSettings.TraceProgressionExplorer("MAIN-MENU wiring progressionExplorerHook=True rebirthMode=" + rebirthMode + " graphReady=" + RebirthProgressionGraphRegistry.IsReady); }
    }

    private static void OnProgressionExplorerPressed(XUiController sender, int mouseButton)
    {
        if (sender == null || sender.xui == null || sender.xui.playerUI == null) return;
        XUi xui = sender.xui;
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth) return;
        if (!RebirthProgressionGraphRegistry.IsReady)
        {
            try { RebirthProgressionGraphRegistry.BuildFromCurrentAuthority(); }
            catch (Exception ex)
            {
                Log.Error("[REBIRTH Progression Explorer][PE-06] press-time graph build failed: "+ex.GetType().Name+": "+ex.Message);
                return;
            }
        }
        GUIWindowManager manager = xui.playerUI.windowManager;
        if (manager == null) return;

        var returnContext = new RebirthProgressionExplorerReturnContext(
            "mainMenu",
            "main-menu-progression-explorer",
            Localization.Get("xuiRebirthProgressionExplorerReturnMainMenu"));
        var request = new RebirthProgressionExplorerLaunchRequest(
            string.Empty,
            RebirthProgressionExplorerMode.Neutral,
            Localization.Get("xuiRebirthProgressionExplorerMainMenuLaunchReason"),
            returnContext);

        { if (RebirthLogSettings.ProgressionExplorerLoggingEnabled) RebirthLogSettings.TraceProgressionExplorer("MAIN-MENU pressed mouse=" + mouseButton +
            " mainMenu=" + manager.IsWindowOpen("mainMenu")); }

        // Keep Main Menu open underneath the modal Explorer. This makes return/ESC atomic:
        // closing the Explorer reveals the exact same Main Menu instance instead of attempting
        // to reconstruct a window group after it has been closed.
        string error;
        if (!RebirthProgressionExplorerUiService.Open(xui, request, out error))
        {
            Log.Error("[REBIRTH Progression Explorer][PE-06] Main Menu launch failed: " + error);
            XUiController mainMenu = xui.FindWindowGroupByName("mainMenu");
            if (mainMenu != null && mainMenu.windowGroup != null)
                manager.Open((GUIWindow)mainMenu.windowGroup, true);
        }
    }
}

// Native keyboard and radial shortcuts share this instance route (including B).
[HarmonyPatch(typeof(XUiC_WindowSelector), "openSelectorAndWindow")]
internal static class RebirthSurvivorShortcutRoutePatch
{
    private static bool Prefix(XUiC_WindowSelector __instance, string _selectedPage)
    {
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth) return true;
        bool character = string.Equals(_selectedPage, "character", StringComparison.OrdinalIgnoreCase);
        bool skills = string.Equals(_selectedPage, "skills", StringComparison.OrdinalIgnoreCase);
        bool journal = string.Equals(_selectedPage, "journal", StringComparison.OrdinalIgnoreCase);
        if (!character && !skills && !journal) return true;
        RebirthProgressionWindowRouting.Witness witness;
        var admission = RebirthProgressionWindowRouting.Resolve(__instance, out witness);
        if (admission == RebirthProgressionWindowRouting.Admission.Native) return true;
        if (admission == RebirthProgressionWindowRouting.Admission.Blocked) return false;
        var manager = __instance.xui?.playerUI?.windowManager;
        if (manager == null || !ReferenceEquals(manager, witness.Manager)) return false;
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return false;
        // Native Character is B by default. Its keyboard press opens recipes;
        // radial selection and the explicit Character tab retain Character navigation.
        bool craftingShortcut = character &&
            PlatformManager.NativePlatform.Input.CurrentInputStyle == PlayerInputManager.InputStyle.Keyboard &&
            (__instance.xui.playerUI.playerInput?.PermanentActions.Character.WasPressed ?? false);
        if (craftingShortcut)
        {
            bool open = manager.IsWindowOpen("crafting");
            if (!RebirthProgressionWindowRouting.StillCurrent(__instance, witness)) return false;
            if (open) manager.Close("crafting");
            else RebirthCraftingNavigationService.Navigate(__instance, RebirthCraftingNavigationService.Destination.PersonalCrafting);
            return false;
        }
        string target = character || skills ? XUiC_RebirthSurvivorCharacter.WindowGroupId : "rebirthJournal";
        bool targetOpen = manager.IsWindowOpen(target);
        if (!RebirthProgressionWindowRouting.StillCurrent(__instance, witness)) return false;
        if (targetOpen) { manager.Close(target); return false; }
        RebirthCraftingNavigationService.Navigate(__instance, character
            ? RebirthCraftingNavigationService.Destination.Character
            : skills ? RebirthCraftingNavigationService.Destination.Skills
            : RebirthCraftingNavigationService.Destination.Journal);
        return false;
    }
}

// Selector button/radial paging can invoke OpenSelectedWindow without the keyboard route.
[HarmonyPatch(typeof(XUiC_WindowSelector), nameof(XUiC_WindowSelector.OpenSelectedWindow))]
internal static class RebirthSurvivorSelectedPageRoutePatch
{
    private static bool Prefix(XUiC_WindowSelector __instance)
    {
        if (RebirthSurvivorMode.ConfiguredMode != RebirthPlayerProgressionMode.Rebirth) return true;
        // Preserve native category advancement for unavailable selections.
        var selected = __instance.SelectedButton;
        if (selected == null || !selected.ViewComponent.IsVisible || !selected.ViewComponent.Enabled) return true;
        string page = __instance.SelectedName;
        bool character = string.Equals(page, "character", StringComparison.OrdinalIgnoreCase);
        bool skills = string.Equals(page, "skills", StringComparison.OrdinalIgnoreCase);
        if (!character && !skills) return true;
        RebirthProgressionWindowRouting.Witness witness;
        var admission = RebirthProgressionWindowRouting.Resolve(__instance, out witness);
        if (admission == RebirthProgressionWindowRouting.Admission.Native) return true;
        if (admission == RebirthProgressionWindowRouting.Admission.Blocked) return false;
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput()) return false;
        if (!string.Equals(page, __instance.SelectedName, StringComparison.OrdinalIgnoreCase)) return false;
        if (!RebirthProgressionWindowRouting.StillCurrent(__instance, witness)) return false;
        RebirthCraftingNavigationService.Navigate(__instance, skills
            ? RebirthCraftingNavigationService.Destination.Skills
            : RebirthCraftingNavigationService.Destination.Character);
        return false;
    }
}
