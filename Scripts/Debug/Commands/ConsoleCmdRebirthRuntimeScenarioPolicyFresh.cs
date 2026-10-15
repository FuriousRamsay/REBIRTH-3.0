#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthRuntimeScenarioPolicyFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbruntimepolicyfresh", "rbscenarioruntimefresh", "rbrpolicyfresh" };
    }

    public override string getDescription()
    {
        return "Prints the inert REBIRTH fresh runtime scenario policy cache shell.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbruntimepolicyfresh summary\n"
             + "  rbruntimepolicyfresh detail\n"
             + "  rbruntimepolicyfresh reset\n"
             + "  rbruntimepolicyfresh safety\n"
             + "\n"
             + "Aliases: rbruntimepolicyfresh, rbscenarioruntimefresh, rbrpolicyfresh\n"
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
                Log.Out(RebirthRuntimeScenarioPolicyCache.GetSummaryReport());
                return;

            case "detail":
            case "details":
                Log.Out(RebirthRuntimeScenarioPolicyCache.GetDetailReport());
                return;

            case "reset":
                RebirthRuntimeScenarioPolicyCache.ResetToExplicitDefaults("manual console reset");
                Log.Out(RebirthRuntimeScenarioPolicyCache.GetSummaryReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthRuntimeScenarioPolicyCache.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthRuntimeScenarioPolicy] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
