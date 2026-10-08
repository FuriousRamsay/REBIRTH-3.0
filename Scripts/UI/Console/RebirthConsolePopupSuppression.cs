using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Controls only automatic assertion/exception console opening. Console output and deliberate
/// manual opening remain native. This policy is independent of gameplay-input exclusivity.
/// </summary>
[Preserve]
public static class RebirthConsolePopupRuntimePolicy
{
    private static bool s_suppressAutomaticErrorPopups;

    public static bool SuppressAutomaticErrorPopups
    {
        get { return s_suppressAutomaticErrorPopups; }
    }

    public static void SetSuppressAutomaticErrorPopups(bool enabled)
    {
        s_suppressAutomaticErrorPopups = enabled;
    }
}

/// <summary>
/// PC127: read the actual registered console window, including a queued open before OnGUI.
/// There is no assumed-open flag, field-name scoring, raw F1/Escape inference or delayed unlock.
/// Native OnOpen/OnClose hooks handle boundary cleanup only; live state is authoritative even
/// when another path opens the console or a lifecycle hook was not involved in the query.
/// </summary>
[Preserve]
public static class RebirthConsoleInputGuardRuntime
{
    // PC128: preserve the native type-name registration without requiring its static ID field.
    private const string ConsoleWindowId = nameof(GUIWindowConsole);

    private static int s_closedFrame = -1;
    private static GUIWindowConsole s_consoleInstance;

    public static bool IsConsoleOpen()
    {
        // Use the console's actual manager rather than assuming menu and in-game XUi share one.
        // isShowing covers an already-visible window; IsWindowOpen also covers pending first draw.
        GUIWindowConsole console = s_consoleInstance;
        if (console != null && (console.isShowing ||
            (console.windowManager != null && console.windowManager.IsWindowOpen(ConsoleWindowId))))
            return true;
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        return ui != null && ui.windowManager != null &&
            ui.windowManager.IsWindowOpen(ConsoleWindowId);
    }

    public static bool BlocksGameplayInput()
    {
        // Consume only the console-closing frame, not a wall-clock cooldown or a wait for all
        // keys to be released. Typed/close-key edges must not become gameplay in the same frame.
        return IsConsoleOpen() || (s_closedFrame >= 0 && s_closedFrame == Time.frameCount);
    }

    public static void MarkOpenedPostfix(GUIWindowConsole __instance)
    {
        s_closedFrame = -1;
        s_consoleInstance = __instance;
        EntityPlayerLocal player = __instance?.playerUI?.entityPlayer;
        if (player != null)
        {
            ClearGameplayMovement(player);
            player.AimingGun = false;
        }
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH ConsoleInput] PC127 opened; native console owns input frame=" + Time.frameCount);
    }

    public static void ClearGameplayMovement(EntityPlayerLocal player)
    {
        if (player == null) return;
        // Native ClearMovementInputs clears translation/look only, NOT the jump/sprint flags.
        // Clear transient button state as well, without changing persistent camera/stance modes.
        if (player.movementInput != null)
        {
            player.movementInput.jump = false;
            player.movementInput.running = false;
            player.movementInput.down = false;
            player.movementInput.downToggle = false;
            player.movementInput.moveForward = 0f;
            player.movementInput.moveStrafe = 0f;
            player.movementInput.rotation = Vector3.zero;
        }
        // Console opening can occur during loading, before the native camera/controller exists.
        if (player.vp_FPController != null)
            player.ClearMovementInputs();
    }

    public static void MarkClosedPostfix()
    {
        s_closedFrame = Time.frameCount;
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        ClearGameplayMovement(ui != null ? ui.entityPlayer : null);
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH ConsoleInput] PC127 closed; close-frame edge consumed, no timed hold frame=" + Time.frameCount);
    }
}

[Preserve]
public static class RebirthConsolePopupInstaller
{
    private static bool s_installed;

    public static void Install()
    {
        if (s_installed)
            return;

        Harmony harmony = new Harmony("rebirth.fresh.consolepopupsuppression.3.1");
        harmony.CreateClassProcessor(typeof(RebirthConsolePopupPatch)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthConsoleOpenedPatch)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthConsoleClosedPatch)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthConsolePlayerMovementGuardPatch)).Patch();
        harmony.CreateClassProcessor(typeof(RebirthConsoleItemActionListGuardPatch)).Patch();
        s_installed = true;
        if (RebirthLogSettings.CraftingUiLoggingEnabled)
            Log.Out("[REBIRTH ConsoleInput] PC127 installed exact OnOpen/OnClose hooks openMethods=1 closeMethods=1; live-window input guard active.");
    }
}

[Preserve]
[HarmonyPatch(typeof(GUIWindowConsole), "openConsole", new[] { typeof(string) })]
public static class RebirthConsolePopupPatch
{
    public static bool Prefix()
    {
        // An attempted automatic open may be rejected natively as well. Only the real OnOpen
        // hook records an opening; never manufacture an input lock from a logging callback.
        return !RebirthConsolePopupRuntimePolicy.SuppressAutomaticErrorPopups;
    }
}

[Preserve]
[HarmonyPatch(typeof(GUIWindowConsole), nameof(GUIWindowConsole.OnOpen))]
internal static class RebirthConsoleOpenedPatch
{
    private static void Postfix(GUIWindowConsole __instance)
    {
        RebirthConsoleInputGuardRuntime.MarkOpenedPostfix(__instance);
    }
}

[Preserve]
[HarmonyPatch(typeof(GUIWindowConsole), nameof(GUIWindowConsole.OnClose))]
internal static class RebirthConsoleClosedPatch
{
    private static void Postfix()
    {
        RebirthConsoleInputGuardRuntime.MarkClosedPostfix();
    }
}

[Preserve]
[HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
public static class RebirthConsolePlayerMovementGuardPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(PlayerMoveController __instance)
    {
        if (!RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
            return true;

        RebirthConsoleInputGuardRuntime.ClearGameplayMovement(
            __instance != null ? __instance.entityPlayerLocal : null);
        // The console's UGUI update, text field, history, submit and close handlers run elsewhere.
        // Do not reset global keyboard state or stop the window manager to suppress gameplay.
        return false;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(PlayerMoveController __instance)
    {
        // Harmony postfixes still run when an original body is skipped. Rebirth dispatchers have
        // their own guard; clear residual movement after this patched update as a final boundary.
        if (RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
            RebirthConsoleInputGuardRuntime.ClearGameplayMovement(
                __instance != null ? __instance.entityPlayerLocal : null);
    }
}

[Preserve]
[HarmonyPatch(typeof(XUiC_ItemActionList), nameof(XUiC_ItemActionList.Update), new[] { typeof(float) })]
internal static class RebirthConsoleItemActionListGuardPatch
{
    private static bool Prefix()
    {
        // The console and underlying XUi windows share GUIActions. In particular, native item
        // action lists bypass the typing test for controller shortcuts. Suspend only their input
        // update, not the crafting queue or console UGUI, while the console has focus.
        return !RebirthConsoleInputGuardRuntime.BlocksGameplayInput();
    }
}
