#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthMigrationReadyFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbmigrationreadyfresh", "rbmigrationready2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh migration readiness and first-slice selection.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbmigrationreadyfresh summary\n"
             + "  rbmigrationreadyfresh list [filter]\n"
             + "  rbmigrationreadyfresh recommended\n"
             + "  rbmigrationreadyfresh blocked\n"
             + "  rbmigrationreadyfresh safety\n"
             + "\n"
             + "Aliases: rbmigrationreadyfresh, rbmigrationready2\n"
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
                Log.Out(RebirthMigrationReadinessRegistry.GetSummaryReport());
                return;

            case "list":
            case "slices":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthMigrationReadinessRegistry.GetSliceReport(filter));
                return;

            case "recommended":
            case "recommend":
            case "next":
                Log.Out(RebirthMigrationReadinessRegistry.GetRecommendedReport());
                return;

            case "blocked":
                Log.Out(RebirthMigrationReadinessRegistry.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthMigrationReadinessRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthMigrationReady] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
