#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthMigrationOrderFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbmigrationorderfresh", "rbmigrationorder2", "rbmigrateplanfresh" };
    }

    public override string getDescription()
    {
        return "Prints the read-only REBIRTH fresh feature migration order plan.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbmigrationorderfresh summary\n"
             + "  rbmigrationorderfresh list [filter]\n"
             + "  rbmigrationorderfresh wave <filter>\n"
             + "  rbmigrationorderfresh blocked\n"
             + "  rbmigrationorderfresh next\n"
             + "  rbmigrationorderfresh safety\n"
             + "\n"
             + "Aliases: rbmigrationorderfresh, rbmigrationorder2, rbmigrateplanfresh\n"
             + "\n"
             + "This command is read-only. It does not migrate code.\n";
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
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetSummaryReport());
                return;

            case "list":
            case "order":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetOrderReport(filter));
                return;

            case "wave":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthMigrationOrder] Usage: rbmigrationorderfresh wave <filter>");
                    return;
                }
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetWaveReport(_params[1]));
                return;

            case "blocked":
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetBlockedReport());
                return;

            case "next":
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetNextPlanningReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthFeatureMigrationOrderRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthMigrationOrder] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
