using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthCavesFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbcavesfresh", "rbcaves2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh cave/worldgen tenant reports.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbcavesfresh summary\n"
             + "  rbcavesfresh tenants\n"
             + "  rbcavesfresh rules\n"
             + "  rbcavesfresh safety\n"
             + "\n"
             + "Aliases: rbcavesfresh, rbcaves2\n"
             + "\n"
             + "This command is read-only. It does not run cave generation or patch worldgen.\n";
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
                Log.Out(RebirthCavePipeline.GetSummaryReport());
                return;

            case "tenants":
            case "stages":
            case "pipeline":
                Log.Out(RebirthCavePipeline.GetTenantReport());
                return;

            case "rules":
            case "hotrules":
                Log.Out(RebirthCavePipeline.GetRuleReport());
                return;

            case "safety":
                Log.Out(RebirthCavePipeline.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthCaves] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
