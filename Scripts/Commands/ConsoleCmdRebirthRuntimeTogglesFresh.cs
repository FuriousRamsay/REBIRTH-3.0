using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthRuntimeTogglesFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbruntimetogglesfresh", "rbruntimetoggles2", "rbfeaturestatefresh" };
    }

    public override string getDescription()
    {
        return "Prints the inert REBIRTH fresh runtime feature toggle state shell.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbruntimetogglesfresh summary\n"
             + "  rbruntimetogglesfresh list [filter]\n"
             + "  rbruntimetogglesfresh reset\n"
             + "  rbruntimetogglesfresh safety\n"
             + "\n"
             + "Aliases: rbruntimetogglesfresh, rbruntimetoggles2, rbfeaturestatefresh\n"
             + "\n"
             + "This command is safe/inert. It does not parse XML, install Harmony patches, or connect gameplay.\n";
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
                Log.Out(RebirthRuntimeFeatureToggleState.GetSummaryReport());
                return;

            case "list":
            case "toggles":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthRuntimeFeatureToggleState.GetToggleReport(filter));
                return;

            case "reset":
                RebirthRuntimeFeatureToggleState.ResetToExplicitDefaults();
                Log.Out(RebirthRuntimeFeatureToggleState.GetSummaryReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthRuntimeFeatureToggleState.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthRuntimeFeatureToggles] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
