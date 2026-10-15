#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthHarvestAdapterFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbharvestadapterfresh", "rbharvestadapter2", "rbsalvageadapterfresh" };
    }

    public override string getDescription()
    {
        return "Prints the disabled/no-op REBIRTH fresh harvest/salvage adapter stub.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbharvestadapterfresh summary\n"
             + "  rbharvestadapterfresh preview [amount]\n"
             + "  rbharvestadapterfresh future\n"
             + "  rbharvestadapterfresh safety\n"
             + "\n"
             + "Aliases: rbharvestadapterfresh, rbharvestadapter2, rbsalvageadapterfresh\n"
             + "\n"
             + "This command is read-only. The adapter is no-op and not connected to gameplay.\n";
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
                Log.Out(RebirthHarvestSalvageImplementationAdapter.GetSummaryReport());
                return;

            case "preview":
                int amount = 10;
                if (_params != null && _params.Count > 1)
                {
                    int parsed;
                    if (int.TryParse(_params[1], out parsed))
                        amount = parsed;
                }

                Log.Out(RebirthHarvestSalvageImplementationAdapter.GetPreviewReport(amount));
                return;

            case "future":
            case "usage":
                Log.Out(RebirthHarvestSalvageImplementationAdapter.GetFutureUseReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthHarvestSalvageImplementationAdapter.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthHarvestSalvageAdapter] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
