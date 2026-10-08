using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthDamageFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbdamagefresh", "rbdamage2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh DamageEntity tenant reports.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbdamagefresh summary\n"
             + "  rbdamagefresh stages\n"
             + "  rbdamagefresh policies\n"
             + "  rbdamagefresh safety\n"
             + "\n"
             + "Aliases: rbdamagefresh, rbdamage2\n"
             + "\n"
             + "This command is read-only. It does not patch DamageEntity or change damage.\n";
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
                Log.Out(RebirthDamagePipeline.GetSummaryReport());
                return;

            case "stages":
            case "pipeline":
                Log.Out(RebirthDamagePipeline.GetStageReport());
                return;

            case "policies":
            case "policy":
                Log.Out(RebirthDamagePipeline.GetPolicyReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthDamagePipeline.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthDamage] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
