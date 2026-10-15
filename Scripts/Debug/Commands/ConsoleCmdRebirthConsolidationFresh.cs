#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthConsolidationFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbconsolidationfresh", "rbconsolidation2", "rbmergeplanfresh" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh single-project consolidation plan.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbconsolidationfresh summary\n"
             + "  rbconsolidationfresh list [filter]\n"
             + "  rbconsolidationfresh target\n"
             + "  rbconsolidationfresh safety\n"
             + "\n"
             + "Aliases: rbconsolidationfresh, rbconsolidation2, rbmergeplanfresh\n"
             + "\n"
             + "This command is read-only. It does not move files or change build output.\n";
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
                Log.Out(RebirthProjectConsolidationRegistry.GetSummaryReport());
                return;

            case "list":
            case "plan":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthProjectConsolidationRegistry.GetPlanReport(filter));
                return;

            case "target":
            case "shape":
                Log.Out(RebirthProjectConsolidationRegistry.GetTargetShapeReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthProjectConsolidationRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthProjectConsolidation] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
