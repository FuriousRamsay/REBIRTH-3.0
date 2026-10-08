using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Explicit client-side tuning command, available in Release as well as Debug builds.
/// Deliberate commands log once; there is no periodic/automatic opacity logging.
/// </summary>
[UnityEngine.Scripting.Preserve]
public sealed class ConsoleCmdRebirthUiOpacity : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }
    public override bool AllowedInMainMenu { get { return false; } }
    public override int DefaultPermissionLevel { get { return 1000; } }

    public override string[] getCommands()
    {
        return new[] { "rbuiopacity" };
    }

    public override string getDescription()
    {
        return "Live-tune REBIRTH Inventory/Crafting/Character Overview main panel opacity and log the exact setting.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
            + "  rbuiopacity          - show and log the current setting\n"
            + "  rbuiopacity status   - same read-only report\n"
            + "  rbuiopacity <0-100>  - set opacity percent; decimals and a trailing % are accepted\n"
            + "  rbuiopacity reset    - restore the default: 98% opacity / 2% transparency\n"
            + "  rbuiopacity help     - show this help\n"
            + "Examples: rbuiopacity 70 | rbuiopacity 72.5 | rbuiopacity 100\n"
            + "Lower = more transparent. 0 clears only the main background fills, not the controls.\n"
            + "Local in-game console only. Open a panel, open the console, set a value, then close\n"
            + "the console to inspect. A closed panel uses the value on its next open.\n"
            + "The override lasts until reset or game restart, including tab switches and window\n"
            + "reopens. It is not saved to disk. Reports include a replay command for your log.\n"
            + "Slots, buttons, entries, navigation, text, icons and global opacity options are untouched.";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        int count = _params != null ? _params.Count : 0;
        string arg = count > 0 ? (_params[0] ?? string.Empty).Trim() : "status";
        if (count > 1)
        {
            Print("[REBIRTH UI Opacity] Expected one percentage, status, reset or help. Setting unchanged.");
            return;
        }
        if (arg.Equals("help", StringComparison.OrdinalIgnoreCase) || arg == "?")
        {
            Print(getHelp());
            return;
        }

        // Do not let server/Telnet/remote invocations alter the host's local presentation.
        // The native console's local route sets IsLocalGame; these fields are the public
        // CommandSenderInfo contract already used by console commands in this project.
        if (!_senderInfo.IsLocalGame || _senderInfo.RemoteClientInfo != null || _senderInfo.NetworkConnection != null)
        {
            Print("[REBIRTH UI Opacity] Run this in your local in-game console, not on a server or remote console. Setting unchanged.");
            return;
        }
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
        if (ui == null || ui.entityPlayer == null)
        {
            Print("[REBIRTH UI Opacity] A local player must be in a world. Setting unchanged.");
            return;
        }

        XUiC_RebirthPersonalCrafting owner = XUiC_RebirthPersonalCrafting.ActiveInstance;
        if (owner != null && !owner.State.IsOpen)
            owner = null;
        XUiC_RebirthSurvivorCharacter character = XUiC_RebirthSurvivorCharacter.ActiveInstance;
        if (character != null && !character.IsCharacterWindowOpen) character = null;
        bool hasOpenSurface = owner != null || character != null;
        string surface = character != null ? "Character" : owner == null ? "closed" : (owner.IsInventoryOnlyMode ? "Inventory" : "Crafting");
        string prefix = "[REBIRTH UI Opacity] ";

        if (arg.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            Print(prefix + "action=status " + RebirthPersonalCraftingPanelOpacity.DescribeSetting()
                + " surface=" + surface + " scope=main-panels-only persistence=session");
            return;
        }

        bool reset = arg.Equals("reset", StringComparison.OrdinalIgnoreCase);
        if (reset)
            RebirthPersonalCraftingPanelOpacity.ResetToDefault();
        else
        {
            string error;
            if (!RebirthPersonalCraftingPanelOpacity.TrySetPercent(arg, out error))
            {
                Print(prefix + error);
                return;
            }
        }

        int matched;
        int changed = RebirthPersonalCraftingPanelOpacity.ApplyTo(owner, out matched);
        int characterMatched;
        changed += RebirthPersonalCraftingPanelOpacity.ApplyCharacter(character, out characterMatched);
        matched += characterMatched;
        int expected = (owner != null ? RebirthPersonalCraftingPanelOpacity.TargetCount : 0)
            + (character != null ? RebirthPersonalCraftingPanelOpacity.CharacterTargetCount : 0);
        string apply = hasOpenSurface ? "apply=live" : "apply=next-open";
        Print(prefix + "action=" + (reset ? "reset" : "set") + " "
            + RebirthPersonalCraftingPanelOpacity.DescribeSetting() + " surface=" + surface
            + " " + apply + " panels=" + matched + "/" + expected
            + " changed=" + changed + " scope=main-panels-only persistence=session"
            + (hasOpenSurface && matched != expected
                ? " WARNING=missing-panel-background-check-installed-UI-files" : string.Empty));
    }

    private static void Print(string text)
    {
        // Match the project's existing explicit command reporting: visible console output
        // plus the persistent client log, independent of periodic diagnostic logging gates.
        SdtdConsole.Instance.Output(text);
        Log.Out(text);
    }
}
