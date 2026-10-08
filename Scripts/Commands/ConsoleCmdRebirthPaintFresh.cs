using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthPaintFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbpaintfresh", "rbpaint2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh paint/render tenant reports.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbpaintfresh summary\n"
             + "  rbpaintfresh tenants\n"
             + "  rbpaintfresh rules\n"
             + "  rbpaintfresh safety\n"
             + "\n"
             + "Aliases: rbpaintfresh, rbpaint2\n"
             + "\n"
             + "This command is read-only. It does not patch renderFace or change paint/tint.\n";
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
                Log.Out(RebirthPaintPipeline.GetSummaryReport());
                return;

            case "tenants":
            case "stages":
            case "pipeline":
                Log.Out(RebirthPaintPipeline.GetTenantReport());
                return;

            case "rules":
            case "hotrules":
                Log.Out(RebirthPaintPipeline.GetRuleReport());
                return;

            case "safety":
                Log.Out(RebirthPaintPipeline.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthPaint] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
