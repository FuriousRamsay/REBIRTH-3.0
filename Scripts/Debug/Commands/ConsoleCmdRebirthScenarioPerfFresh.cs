#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthScenarioPerfFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbscenarioperffresh", "rbscenarioperf2", "rbsperffresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh scenario performance contracts.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbscenarioperffresh summary\n"
             + "  rbscenarioperffresh sets [filter]\n"
             + "  rbscenarioperffresh metrics [filter]\n"
             + "  rbscenarioperffresh matrix\n"
             + "  rbscenarioperffresh blocked\n"
             + "  rbscenarioperffresh safety\n"
             + "\n"
             + "Aliases: rbscenarioperffresh, rbscenarioperf2, rbsperffresh\n"
             + "\n"
             + "This command is read-only. It does not run tests or capture metrics.\n";
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
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetSummaryReport());
                return;

            case "sets":
            case "list":
                string setFilter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetSetReport(setFilter));
                return;

            case "metrics":
                string metricFilter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetMetricReport(metricFilter));
                return;

            case "matrix":
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetMatrixReport());
                return;

            case "blocked":
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthScenarioPerformanceContractRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthScenarioPerf] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
