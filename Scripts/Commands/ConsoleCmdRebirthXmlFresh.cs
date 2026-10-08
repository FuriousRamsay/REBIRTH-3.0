using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthXmlFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbxmlfresh", "rbxml2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh XML/data ownership reports.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbxmlfresh summary\n"
             + "  rbxmlfresh layers\n"
             + "  rbxmlfresh files [filter]\n"
             + "  rbxmlfresh cleanup\n"
             + "\n"
             + "Aliases: rbxmlfresh, rbxml2\n"
             + "\n"
             + "This command is read-only. It does not parse, generate, or modify XML.\n";
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
                Log.Out(RebirthXmlLayerRegistry.GetSummaryReport());
                return;

            case "layers":
            case "layer":
                Log.Out(RebirthXmlLayerRegistry.GetLayerReport());
                return;

            case "files":
            case "file":
            case "families":
            case "family":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthXmlLayerRegistry.GetFileFamilyReport(filter));
                return;

            case "cleanup":
            case "cleanups":
                Log.Out(RebirthXmlLayerRegistry.GetCleanupReport());
                return;

            default:
                Log.Out("[RebirthXml] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
