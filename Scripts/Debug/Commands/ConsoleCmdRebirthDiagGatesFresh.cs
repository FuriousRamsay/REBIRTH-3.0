#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthDiagGatesFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbdiaggatesfresh", "rbdiaggates2", "rbdebuggatesfresh" };
    }

    public override string getDescription()
    {
        return "Prints the inert REBIRTH fresh diagnostic gate helper shell.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbdiaggatesfresh summary\n"
             + "  rbdiaggatesfresh rules\n"
             + "  rbdiaggatesfresh example\n"
             + "  rbdiaggatesfresh safety\n"
             + "\n"
             + "Aliases: rbdiaggatesfresh, rbdiaggates2, rbdebuggatesfresh\n"
             + "\n"
             + "This command is read-only. The gate shell does not install patches or log by itself.\n";
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
                Log.Out(RebirthDiagnosticGates.GetSummaryReport());
                return;

            case "rules":
                Log.Out(RebirthDiagnosticGates.GetRulesReport());
                return;

            case "example":
            case "examples":
                Log.Out(RebirthDiagnosticGates.GetExampleReport());
                return;

            case "safety":
                Log.Out(RebirthDiagnosticGates.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthDiagnosticGates] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
