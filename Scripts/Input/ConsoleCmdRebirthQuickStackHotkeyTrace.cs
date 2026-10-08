using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Focused runtime diagnostics for the configurable Quick Stack shortcut.
/// This deliberately lives outside Scripts/Debug so release builds can be tested.
/// </summary>
public static class QuickStackHotkeyDiagnostics
{
    private static bool enabled;
    private static float deadline;
    private static int remainingLines;
    public static bool Enabled
    {
        get { return enabled && UnityEngine.Time.realtimeSinceStartup < deadline && remainingLines > 0; }
    }
    public static void Tick()
    {
        if (enabled && !Enabled) SetEnabled(false);
    }
    public static void SetEnabled(bool value)
    {
        if (value)
        {
            enabled = true;
            deadline = UnityEngine.Time.realtimeSinceStartup + 60f;
            remainingLines = 200;
            RebirthNativeControls.ResetQuickStackHotkeyTraceHeartbeat();
            Write("ENABLED for at most 60 seconds / 200 combined trace lines; typed text is never captured.");
            RebirthNativeControls.LogQuickStackHotkeySnapshot("trace-enabled", ResolvePrimaryPlayer());
        }
        else
        {
            bool wasEnabled = enabled;
            enabled = false;
            deadline = 0f;
            remainingLines = 0;
            if (wasEnabled) Log.Out("[QuickStackHotkey] capture stopped; independent diagnostic settings unchanged.");
        }
    }
    // Also used by QuickStackDiagnostics for lines enabled by this lease only.
    internal static bool ConsumeLine()
    {
        if (!Enabled) return false;
        remainingLines--;
        return true;
    }

    public static EntityPlayerLocal ResolvePrimaryPlayer()
    {
        try
        {
            return GameManager.Instance != null && GameManager.Instance.World != null
                ? GameManager.Instance.World.GetPrimaryPlayer()
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string text)
    {
        if (!ConsumeLine())
            return;
        Log.Out("[QuickStackHotkey] " + text);
    }

    public static string BuildStatusLine()
    {
        return "[QuickStackHotkey] trace=" + Enabled +
               " quickStackDiagnostics=" + QuickStackDiagnostics.Enabled +
               " " + RebirthNativeControls.GetQuickStackHotkeyTraceStatus(ResolvePrimaryPlayer());
    }
}

[Preserve]
public class ConsoleCmdRebirthQuickStackHotkeyTrace : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override int DefaultPermissionLevel => 1000;

    public override string[] getCommands()
    {
        return new[] { "rbqshotkeytrace", "rbqstrace", "quickstackhotkeytrace" };
    }

    public override string getDescription()
    {
        return "Traces the REBIRTH Quick Stack shortcut from physical key/input state through dispatch and transfer request.";
    }

    public override string getHelp()
    {
        return "Usage:\n" +
               "  rbqstrace on       - enable a bounded 60-second / 200-line hotkey and Quick Stack capture\n" +
               "  rbqstrace off      - stop this trace lease; independent Quick Stack diagnostics stay unchanged\n" +
               "  rbqstrace status   - print the current binding/action/gate/runtime-policy state\n" +
               "  rbqstrace snapshot - write an immediate [QuickStackHotkey] snapshot to the log\n";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string command = _params != null && _params.Count > 0
            ? (_params[0] ?? string.Empty).Trim().ToLowerInvariant()
            : "status";

        switch (command)
        {
            case "on":
            case "1":
            case "true":
                QuickStackHotkeyDiagnostics.SetEnabled(true);
                Print(QuickStackHotkeyDiagnostics.BuildStatusLine());
                Print("[QuickStackHotkey] Reproduce now: while on foot, out of God Mode and Debug Mode, press the configured Quick Stack key 2-3 times.");
                return;

            case "off":
            case "0":
            case "false":
                QuickStackHotkeyDiagnostics.SetEnabled(false);
                Print(QuickStackHotkeyDiagnostics.BuildStatusLine());
                return;

            case "snapshot":
            case "snap":
                EntityPlayerLocal player = QuickStackHotkeyDiagnostics.ResolvePrimaryPlayer();
                string status = QuickStackHotkeyDiagnostics.BuildStatusLine();
                Log.Out(status);
                if (QuickStackHotkeyDiagnostics.Enabled)
                    RebirthNativeControls.LogQuickStackHotkeySnapshot("console-snapshot", player);
                Print(status);
                return;

            case "status":
                string line = QuickStackHotkeyDiagnostics.BuildStatusLine();
                Log.Out(line);
                Print(line);
                return;

            default:
                Print(getHelp());
                return;
        }
    }

    private static void Print(string text)
    {
        SingletonMonoBehaviour<SdtdConsole>.Instance.Output(text);
    }
}
