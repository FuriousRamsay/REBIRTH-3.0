#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthFirstSliceFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbfirstslicefresh", "rbfirstslice2", "rbgameplayslicefresh" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh first gameplay slice contract.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbfirstslicefresh summary\n"
             + "  rbfirstslicefresh list [filter]\n"
             + "  rbfirstslicefresh blocked\n"
             + "  rbfirstslicefresh recommended\n"
             + "  rbfirstslicefresh safety\n"
             + "\n"
             + "Aliases: rbfirstslicefresh, rbfirstslice2, rbgameplayslicefresh\n"
             + "\n"
             + "This command is read-only. It does not patch or migrate gameplay.\n";
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
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetSummaryReport());
                return;

            case "list":
            case "contracts":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetContractReport(filter));
                return;

            case "blocked":
            case "blockers":
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetBlockedReport());
                return;

            case "recommended":
            case "next":
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetRecommendedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthFirstGameplaySliceContractRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthFirstSliceContract] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
