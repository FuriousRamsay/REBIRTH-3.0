#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthRuntimeHealthFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbruntimehealthfresh", "rbruntimehealth2", "rbhealthfresh" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh runtime architecture health snapshot.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbruntimehealthfresh summary\n"
             + "  rbruntimehealthfresh full\n"
             + "  rbruntimehealthfresh blockers\n"
             + "  rbruntimehealthfresh next\n"
             + "  rbruntimehealthfresh safety\n"
             + "\n"
             + "Aliases: rbruntimehealthfresh, rbruntimehealth2, rbhealthfresh\n"
             + "\n"
             + "This command is read-only. It does not connect gameplay or install Harmony patches.\n";
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
                Log.Out(RebirthRuntimeArchitectureHealth.GetSummaryReport());
                return;

            case "full":
            case "detail":
            case "details":
                Log.Out(RebirthRuntimeArchitectureHealth.GetFullReport());
                return;

            case "blockers":
            case "blocked":
                Log.Out(RebirthRuntimeArchitectureHealth.GetBlockersReport());
                return;

            case "next":
            case "recommended":
            case "recommend":
                Log.Out(RebirthRuntimeArchitectureHealth.GetRecommendedNextReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthRuntimeArchitectureHealth.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthRuntimeHealth] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
