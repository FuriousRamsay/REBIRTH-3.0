using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthItemsFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbitemsfresh", "rbitems2", "rbstatsfresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh item/stat tenant reports.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbitemsfresh summary\n"
             + "  rbitemsfresh tenants\n"
             + "  rbitemsfresh rules\n"
             + "  rbitemsfresh safety\n"
             + "\n"
             + "Aliases: rbitemsfresh, rbitems2, rbstatsfresh\n"
             + "\n"
             + "This command is read-only. It does not patch ItemValue or change stats.\n";
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
                Log.Out(RebirthItemStatPipeline.GetSummaryReport());
                return;

            case "tenants":
            case "stages":
            case "pipeline":
                Log.Out(RebirthItemStatPipeline.GetTenantReport());
                return;

            case "rules":
            case "hotrules":
                Log.Out(RebirthItemStatPipeline.GetRuleReport());
                return;

            case "safety":
                Log.Out(RebirthItemStatPipeline.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthItemStats] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
