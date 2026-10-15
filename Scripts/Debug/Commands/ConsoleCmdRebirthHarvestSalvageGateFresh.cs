#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthHarvestSalvageGateFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbharvestgatefresh", "rbharvestgate2", "rbsalvagegatefresh" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh harvest/salvage test gate.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbharvestgatefresh summary\n"
             + "  rbharvestgatefresh list [filter]\n"
             + "  rbharvestgatefresh critical\n"
             + "  rbharvestgatefresh server\n"
             + "  rbharvestgatefresh performance\n"
             + "  rbharvestgatefresh readiness\n"
             + "  rbharvestgatefresh safety\n"
             + "\n"
             + "Aliases: rbharvestgatefresh, rbharvestgate2, rbsalvagegatefresh\n"
             + "\n"
             + "This command is read-only. It does not migrate gameplay.\n";
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
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetSummaryReport());
                return;

            case "list":
            case "tests":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetTestReport(filter));
                return;

            case "critical":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetCriticalReport());
                return;

            case "server":
            case "parity":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetServerReport());
                return;

            case "performance":
            case "perf":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetPerformanceReport());
                return;

            case "readiness":
            case "ready":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetMigrationReadinessReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthHarvestSalvageTestGateRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthHarvestSalvageGate] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
