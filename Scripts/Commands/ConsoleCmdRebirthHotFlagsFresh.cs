using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthHotFlagsFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbhotflagsfresh", "rbhotflags2", "rbfeatureflagsfresh" };
    }

    public override string getDescription()
    {
        return "Prints the inert REBIRTH fresh hot-path safe feature flags shell.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbhotflagsfresh summary\n"
             + "  rbhotflagsfresh list\n"
             + "  rbhotflagsfresh intended\n"
             + "  rbhotflagsfresh safety\n"
             + "\n"
             + "Aliases: rbhotflagsfresh, rbhotflags2, rbfeatureflagsfresh\n"
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
                Log.Out(RebirthHotPathFeatureFlags.GetSummaryReport());
                return;

            case "list":
            case "flags":
                Log.Out(RebirthHotPathFeatureFlags.GetFlagsReport());
                return;

            case "intended":
            case "usage":
            case "use":
                Log.Out(RebirthHotPathFeatureFlags.GetIntendedUseReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthHotPathFeatureFlags.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthHotPathFeatureFlags] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
