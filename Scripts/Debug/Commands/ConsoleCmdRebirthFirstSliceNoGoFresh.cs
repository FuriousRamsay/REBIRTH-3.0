#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthFirstSliceNoGoFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbfirstslicenogofresh", "rbfirstslicenogo2", "rbnogo" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh first gameplay slice no-go gate.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbfirstslicenogofresh summary\n"
             + "  rbfirstslicenogofresh list [filter]\n"
             + "  rbfirstslicenogofresh blockers\n"
             + "  rbfirstslicenogofresh safety\n"
             + "\n"
             + "Aliases: rbfirstslicenogofresh, rbfirstslicenogo2, rbnogo\n"
             + "\n"
             + "This command is read-only. It does not choose a target or install patches.\n";
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
                Log.Out(RebirthFirstSliceNoGoGate.GetSummaryReport());
                return;

            case "list":
            case "reasons":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthFirstSliceNoGoGate.GetReasonsReport(filter));
                return;

            case "blockers":
            case "blocked":
                Log.Out(RebirthFirstSliceNoGoGate.GetBlockersReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthFirstSliceNoGoGate.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthFirstSliceNoGoGate] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
