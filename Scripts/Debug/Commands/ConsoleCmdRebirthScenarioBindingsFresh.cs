#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthScenarioBindingsFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbscenariobindingsfresh", "rbscenariobindings2", "rbsbindfresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh scenario-to-module bindings.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbscenariobindingsfresh summary\n"
             + "  rbscenariobindingsfresh list [filter]\n"
             + "  rbscenariobindingsfresh scenario <scenario>\n"
             + "  rbscenariobindingsfresh module <module>\n"
             + "  rbscenariobindingsfresh guardrails\n"
             + "  rbscenariobindingsfresh safety\n"
             + "\n"
             + "Aliases: rbscenariobindingsfresh, rbscenariobindings2, rbsbindfresh\n"
             + "\n"
             + "This command is read-only. It does not activate scenarios or change gameplay.\n";
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
                Log.Out(RebirthScenarioModuleBindingRegistry.GetSummaryReport());
                return;

            case "list":
            case "bindings":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthScenarioModuleBindingRegistry.GetBindingReport(filter));
                return;

            case "scenario":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthScenarioBindings] Usage: rbscenariobindingsfresh scenario <scenario>");
                    return;
                }
                Log.Out(RebirthScenarioModuleBindingRegistry.GetScenarioReport(_params[1]));
                return;

            case "module":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthScenarioBindings] Usage: rbscenariobindingsfresh module <module>");
                    return;
                }
                Log.Out(RebirthScenarioModuleBindingRegistry.GetModuleReport(_params[1]));
                return;

            case "guardrails":
            case "guardrail":
                Log.Out(RebirthScenarioModuleBindingRegistry.GetGuardrailReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthScenarioModuleBindingRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthScenarioBindings] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
