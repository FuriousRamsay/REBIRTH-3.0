#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthScenarioManagersFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbscenariomanagersfresh", "rbscenariomanagers2", "rbsmanagersfresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh scenario manager boundaries.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbscenariomanagersfresh summary\n"
             + "  rbscenariomanagersfresh list [filter]\n"
             + "  rbscenariomanagersfresh scenario <scenario>\n"
             + "  rbscenariomanagersfresh authority\n"
             + "  rbscenariomanagersfresh lifecycle\n"
             + "  rbscenariomanagersfresh blocked\n"
             + "  rbscenariomanagersfresh safety\n"
             + "\n"
             + "Aliases: rbscenariomanagersfresh, rbscenariomanagers2, rbsmanagersfresh\n"
             + "\n"
             + "This command is read-only. It does not instantiate managers or register scheduler jobs.\n";
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
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetSummaryReport());
                return;

            case "list":
            case "managers":
            case "boundaries":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetBoundaryReport(filter));
                return;

            case "scenario":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthScenarioManagers] Usage: rbscenariomanagersfresh scenario <scenario>");
                    return;
                }
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetScenarioReport(_params[1]));
                return;

            case "authority":
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetAuthorityReport());
                return;

            case "lifecycle":
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetLifecycleReport());
                return;

            case "blocked":
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthScenarioManagerBoundaryRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthScenarioManagers] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
