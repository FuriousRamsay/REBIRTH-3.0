#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthBuildScriptFilesFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbbuildfilesfresh", "rbbuildfiles2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh generated dry-run build script file information.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbbuildfilesfresh summary\n"
             + "  rbbuildfilesfresh list\n"
             + "  rbbuildfilesfresh safety\n"
             + "\n"
             + "Aliases: rbbuildfilesfresh, rbbuildfiles2\n"
             + "\n"
             + "This command is read-only. It does not execute scripts or build anything.\n";
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
                Log.Out(RebirthBuildScriptFileRegistry.GetSummaryReport());
                return;

            case "list":
            case "files":
                Log.Out(RebirthBuildScriptFileRegistry.GetReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthBuildScriptFileRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthBuildScriptFiles] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
