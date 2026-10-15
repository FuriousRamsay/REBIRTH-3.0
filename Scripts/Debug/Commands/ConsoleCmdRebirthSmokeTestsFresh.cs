#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthSmokeTestsFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbsmoketestsfresh", "rbsmoketests2", "rbsmoke" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh command smoke test matrix.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbsmoketestsfresh summary\n"
             + "  rbsmoketestsfresh list [filter]\n"
             + "  rbsmoketestsfresh critical\n"
             + "  rbsmoketestsfresh order\n"
             + "  rbsmoketestsfresh safety\n"
             + "\n"
             + "Aliases: rbsmoketestsfresh, rbsmoketests2, rbsmoke\n"
             + "\n"
             + "This command is read-only. It does not execute other commands.\n";
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
                Log.Out(RebirthCommandSmokeTestMatrix.GetSummaryReport());
                return;

            case "list":
            case "matrix":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthCommandSmokeTestMatrix.GetMatrixReport(filter));
                return;

            case "critical":
                Log.Out(RebirthCommandSmokeTestMatrix.GetCriticalReport());
                return;

            case "order":
            case "recommended":
                Log.Out(RebirthCommandSmokeTestMatrix.GetRecommendedOrderReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthCommandSmokeTestMatrix.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthCommandSmokeTests] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
