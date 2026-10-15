#if DEBUG
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthCraftingUi : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }
    public override string[] getCommands() { return new[] { "rbcraftui" }; }
    public override string getDescription() { return "Reports REBIRTH Personal Crafting UI ownership, layout and interaction state."; }
    public override string getHelp()
    {
        return "rbcraftui status\n"
             + "rbcraftui guides on|off\n"
             + "Status is read-only. Guides toggle developer-only layout rectangles. Debug builds only.";
    }

    public override void Execute(List<string> args, CommandSenderInfo senderInfo)
    {
        string sub = args != null && args.Count > 0 ? (args[0] ?? string.Empty).Trim().ToLowerInvariant() : "status";
        if (sub == "guides")
        {
            if (args == null || args.Count < 2)
            {
                Log.Out("[REBIRTH Crafting] guides=" + RebirthPersonalCraftingState.LayoutDebugEnabled);
                return;
            }
            bool on = args[1].Equals("on", StringComparison.OrdinalIgnoreCase) || args[1] == "1" || args[1].Equals("true", StringComparison.OrdinalIgnoreCase);
            RebirthPersonalCraftingState.LayoutDebugEnabled = on;
            XUiC_RebirthPersonalCrafting.ActiveInstance?.RequestLayoutAudit();
            Log.Out("[REBIRTH Crafting] guides=" + on);
            return;
        }
        if (sub == "help" || sub == "?")
        {
            Log.Out(getHelp());
            return;
        }
        XUiC_RebirthPersonalCrafting active = XUiC_RebirthPersonalCrafting.ActiveInstance;
        if (active == null || active.State == null || !active.State.IsOpen)
        {
            Log.Out("[REBIRTH Crafting FinalAcceptance] custom Personal Crafting is not open.");
            return;
        }
        Log.Out(active.GetFinalAcceptanceDebugReport());
    }
}
#endif
