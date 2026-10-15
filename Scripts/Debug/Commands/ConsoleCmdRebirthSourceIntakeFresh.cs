#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthSourceIntakeFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbsourceintakefresh", "rbsourceintake2", "rbevidenceintakefresh" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh source evidence intake template.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbsourceintakefresh summary\n"
             + "  rbsourceintakefresh list [filter]\n"
             + "  rbsourceintakefresh markdown\n"
             + "  rbsourceintakefresh safety\n"
             + "\n"
             + "Aliases: rbsourceintakefresh, rbsourceintake2, rbevidenceintakefresh\n"
             + "\n"
             + "This command is read-only. It does not inspect files or install patches.\n";
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
                Log.Out(RebirthSourceEvidenceIntakeTemplate.GetSummaryReport());
                return;

            case "list":
            case "template":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthSourceEvidenceIntakeTemplate.GetTemplateReport(filter));
                return;

            case "markdown":
            case "md":
                Log.Out(RebirthSourceEvidenceIntakeTemplate.GetBlankMarkdownTemplate());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthSourceEvidenceIntakeTemplate.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthSourceEvidenceIntake] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
