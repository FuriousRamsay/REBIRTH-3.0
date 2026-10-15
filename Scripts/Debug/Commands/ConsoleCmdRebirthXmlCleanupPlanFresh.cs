#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthXmlCleanupPlanFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbxmlcleanupfresh", "rbxmlcleanup2", "rbxmlplanfresh" };
    }

    public override string getDescription()
    {
        return "Prints the read-only REBIRTH fresh XML cleanup/reorganization plan.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbxmlcleanupfresh summary\n"
             + "  rbxmlcleanupfresh list [filter]\n"
             + "  rbxmlcleanupfresh rootincludes\n"
             + "  rbxmlcleanupfresh safety\n"
             + "\n"
             + "Aliases: rbxmlcleanupfresh, rbxmlcleanup2, rbxmlplanfresh\n"
             + "\n"
             + "This command is read-only. It does not move, parse, or generate XML.\n";
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
                Log.Out(RebirthXmlCleanupPlanRegistry.GetSummaryReport());
                return;

            case "list":
            case "plan":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthXmlCleanupPlanRegistry.GetPlanReport(filter));
                return;

            case "rootincludes":
            case "includes":
            case "root":
                Log.Out(RebirthXmlCleanupPlanRegistry.GetRootIncludeReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthXmlCleanupPlanRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthXmlCleanupPlan] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
