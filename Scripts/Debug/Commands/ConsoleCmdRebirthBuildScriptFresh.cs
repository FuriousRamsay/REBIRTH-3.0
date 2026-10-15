#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthBuildScriptFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbbuildscriptfresh", "rbbuildscript2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh build script templates.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbbuildscriptfresh summary\n"
             + "  rbbuildscriptfresh list [filter]\n"
             + "  rbbuildscriptfresh profile [profile]\n"
             + "  rbbuildscriptfresh filenames\n"
             + "  rbbuildscriptfresh safety\n"
             + "\n"
             + "Aliases: rbbuildscriptfresh, rbbuildscript2\n"
             + "\n"
             + "This command is read-only. It does not create scripts or change build files.\n";
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
                Log.Out(RebirthBuildScriptTemplateRegistry.GetSummaryReport());
                return;

            case "list":
            case "templates":
            case "actions":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthBuildScriptTemplateRegistry.GetTemplateReport(filter));
                return;

            case "profile":
            case "dryrun":
                string profile = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthBuildScriptTemplateRegistry.GetProfileTemplateReport(profile));
                return;

            case "filenames":
            case "files":
                Log.Out(RebirthBuildScriptTemplateRegistry.GetFutureFileNameReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthBuildScriptTemplateRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthBuildScripts] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
