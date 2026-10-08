using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthPlayerFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbplayerfresh", "rbplayer2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh player movement tenant reports.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbplayerfresh summary\n"
             + "  rbplayerfresh stages\n"
             + "  rbplayerfresh safety\n"
             + "\n"
             + "Aliases: rbplayerfresh, rbplayer2\n"
             + "\n"
             + "This command is read-only. It does not patch PlayerMoveController or change movement.\n";
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
                Log.Out(RebirthPlayerMovementPipeline.GetSummaryReport());
                return;

            case "stages":
            case "pipeline":
                Log.Out(RebirthPlayerMovementPipeline.GetStageReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthPlayerMovementPipeline.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthPlayerMovement] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
