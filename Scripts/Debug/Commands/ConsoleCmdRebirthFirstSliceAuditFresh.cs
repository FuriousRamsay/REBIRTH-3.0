#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthFirstSliceAuditFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbfirstsliceauditfresh", "rbfirstsliceaudit2", "rbsliceauditfresh" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh first gameplay slice audit checklist.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbfirstsliceauditfresh summary\n"
             + "  rbfirstsliceauditfresh list [filter]\n"
             + "  rbfirstsliceauditfresh blockers\n"
             + "  rbfirstsliceauditfresh safety\n"
             + "\n"
             + "Aliases: rbfirstsliceauditfresh, rbfirstsliceaudit2, rbsliceauditfresh\n"
             + "\n"
             + "This command is read-only. It does not inspect source or install patches.\n";
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
                Log.Out(RebirthFirstSliceAuditChecklistRegistry.GetSummaryReport());
                return;

            case "list":
            case "checklist":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthFirstSliceAuditChecklistRegistry.GetChecklistReport(filter));
                return;

            case "blockers":
            case "blocked":
                Log.Out(RebirthFirstSliceAuditChecklistRegistry.GetBlockersReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthFirstSliceAuditChecklistRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthFirstSliceAuditChecklist] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
