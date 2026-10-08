using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthHarvestSalvagePolicyFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbharvestpolicyfresh", "rbharvestpolicy2", "rbsalvagepolicyfresh" };
    }

    public override string getDescription()
    {
        return "Prints the inert REBIRTH fresh harvest/salvage runtime policy shell.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbharvestpolicyfresh summary\n"
             + "  rbharvestpolicyfresh detail\n"
             + "  rbharvestpolicyfresh requirements\n"
             + "  rbharvestpolicyfresh reset\n"
             + "  rbharvestpolicyfresh safety\n"
             + "\n"
             + "Aliases: rbharvestpolicyfresh, rbharvestpolicy2, rbsalvagepolicyfresh\n"
             + "\n"
             + "This command is inert. It does not migrate gameplay or install Harmony patches.\n";
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
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetSummaryReport());
                return;

            case "detail":
            case "details":
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetDetailReport());
                return;

            case "requirements":
            case "patch":
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetFuturePatchRequirementsReport());
                return;

            case "reset":
                RebirthHarvestSalvageRuntimePolicy.ResetToExplicitDefaults("manual console reset");
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetSummaryReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthHarvestSalvageRuntimePolicy.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthHarvestSalvagePolicy] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
