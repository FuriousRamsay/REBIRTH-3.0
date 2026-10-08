using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthScenarioFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbscenariofresh", "rbscenario2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh scenario contracts.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbscenariofresh summary\n"
             + "  rbscenariofresh list [filter]\n"
             + "  rbscenariofresh detail <scenario>\n"
             + "  rbscenariofresh combinations\n"
             + "  rbscenariofresh authority\n"
             + "  rbscenariofresh safety\n"
             + "\n"
             + "Aliases: rbscenariofresh, rbscenario2\n"
             + "\n"
             + "This command is read-only. It does not activate scenarios or change gameplay.\n";
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
                Log.Out(RebirthScenarioContractRegistry.GetSummaryReport());
                return;

            case "list":
            case "contracts":
            case "scenarios":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthScenarioContractRegistry.GetContractReport(filter));
                return;

            case "detail":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthScenarioContracts] Usage: rbscenariofresh detail <scenario>");
                    return;
                }
                Log.Out(RebirthScenarioContractRegistry.GetDetailReport(_params[1]));
                return;

            case "combinations":
            case "combination":
            case "combo":
                Log.Out(RebirthScenarioContractRegistry.GetCombinationReport());
                return;

            case "authority":
            case "network":
                Log.Out(RebirthScenarioContractRegistry.GetAuthorityReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthScenarioContractRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthScenarioContracts] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
