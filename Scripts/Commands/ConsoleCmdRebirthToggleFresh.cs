using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthToggleFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbtogglefresh", "rbtoggle2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh toggle/performance-impact contracts.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbtogglefresh summary\n"
             + "  rbtogglefresh list [filter]\n"
             + "  rbtogglefresh impact\n"
             + "  rbtogglefresh policy [filter]\n"
             + "  rbtogglefresh commands\n"
             + "  rbtogglefresh safety\n"
             + "\n"
             + "Aliases: rbtogglefresh, rbtoggle2\n"
             + "\n"
             + "This command is read-only. It does not enable/disable real features yet.\n";
    }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string cmd = (_params == null || _params.Count == 0)
            ? "summary"
            : (_params[0] ?? string.Empty).Trim().ToLowerInvariant();

        switch (cmd)
        {
            case "help":
            case "?":
                Log.Out(getHelp());
                return;

            case "summary":
            case "status":
                Log.Out(RebirthModuleToggleRegistry.GetSummaryReport());
                return;

            case "list":
            case "modules":
            case "toggles":
                string listFilter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthModuleToggleRegistry.GetListReport(listFilter));
                return;

            case "impact":
            case "perf":
            case "performance":
                Log.Out(RebirthModuleToggleRegistry.GetImpactReport());
                return;

            case "policy":
            case "policies":
                string policyFilter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthModuleToggleRegistry.GetPolicyReport(policyFilter));
                return;

            case "commands":
            case "commandplan":
                Log.Out(RebirthModuleToggleRegistry.GetCommandPlanReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthModuleToggleRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthToggles] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
