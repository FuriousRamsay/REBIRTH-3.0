#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthMilestoneFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbmilestonefresh", "rbmilestone2", "rbhandoffplan" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh consolidated milestone plan.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbmilestonefresh summary\n"
             + "  rbmilestonefresh list [filter]\n"
             + "  rbmilestonefresh next\n"
             + "  rbmilestonefresh safety\n"
             + "\n"
             + "Aliases: rbmilestonefresh, rbmilestone2, rbhandoffplan\n"
             + "\n"
             + "This command is read-only. It records larger milestone cadence and handoff rules.\n";
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
                Log.Out(RebirthMilestonePlanRegistry.GetSummaryReport());
                return;

            case "list":
            case "plan":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthMilestonePlanRegistry.GetPlanReport(filter));
                return;

            case "next":
            case "recommended":
                Log.Out(RebirthMilestonePlanRegistry.GetNextReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthMilestonePlanRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthMilestonePlan] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
