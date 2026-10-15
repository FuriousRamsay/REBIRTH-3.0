#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthFeatureTestsFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbfeaturetestsfresh", "rbfeaturetests2", "rbtestmatrixfresh" };
    }

    public override string getDescription()
    {
        return "Prints the read-only REBIRTH fresh feature test matrix.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbfeaturetestsfresh summary\n"
             + "  rbfeaturetestsfresh list [filter]\n"
             + "  rbfeaturetestsfresh critical\n"
             + "  rbfeaturetestsfresh hot\n"
             + "  rbfeaturetestsfresh stateful\n"
             + "  rbfeaturetestsfresh blocked\n"
             + "  rbfeaturetestsfresh safety\n"
             + "\n"
             + "Aliases: rbfeaturetestsfresh, rbfeaturetests2, rbtestmatrixfresh\n"
             + "\n"
             + "This command is read-only. It does not execute tests.\n";
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
                Log.Out(RebirthFeatureTestMatrixRegistry.GetSummaryReport());
                return;

            case "list":
            case "tests":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthFeatureTestMatrixRegistry.GetTestReport(filter));
                return;

            case "critical":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetCriticalReport());
                return;

            case "hot":
            case "hotpath":
            case "performance":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetHotPathReport());
                return;

            case "stateful":
            case "network":
            case "persistence":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetStatefulReport());
                return;

            case "blocked":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthFeatureTestMatrixRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthFeatureTests] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
