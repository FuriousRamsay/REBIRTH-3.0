using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthToggleStateFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbtogglestatefresh", "rbtogglestate2", "rbstatefresh" };
    }

    public override string getDescription()
    {
        return "Manages read-only gameplay-safe shadow toggle states for REBIRTH fresh.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbtogglestatefresh summary\n"
             + "  rbtogglestatefresh status [filter]\n"
             + "  rbtogglestatefresh on <module-filter>\n"
             + "  rbtogglestatefresh off <module-filter>\n"
             + "  rbtogglestatefresh reset [module-filter]\n"
             + "  rbtogglestatefresh safety\n"
             + "\n"
             + "Aliases: rbtogglestatefresh, rbtogglestate2, rbstatefresh\n"
             + "\n"
             + "These are shadow states only. They do not change gameplay or patch Harmony.\n"
             + "Workflow example: rbtogglestatefresh off player.crawl, then rbperffresh sweep.\n";
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
            case "statussummary":
                Log.Out(RebirthModuleToggleStateRegistry.GetSummaryReport());
                return;

            case "status":
            case "list":
                string statusFilter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthModuleToggleStateRegistry.GetStatusReport(statusFilter));
                return;

            case "on":
            case "enable":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthToggleState] Usage: rbtogglestatefresh on <module-filter>");
                    return;
                }
                Log.Out(RebirthModuleToggleStateRegistry.SetShadowState(_params[1], true, "manual shadow enable"));
                return;

            case "off":
            case "disable":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthToggleState] Usage: rbtogglestatefresh off <module-filter>");
                    return;
                }
                Log.Out(RebirthModuleToggleStateRegistry.SetShadowState(_params[1], false, "manual shadow disable"));
                return;

            case "reset":
                string resetFilter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthModuleToggleStateRegistry.ResetShadowState(resetFilter));
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthModuleToggleStateRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthToggleState] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
