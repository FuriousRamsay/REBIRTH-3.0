using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthScenarioXmlFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbscenarioxmlfresh", "rbscenarioxml2", "rbsxmlfresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh scenario XML ownership.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbscenarioxmlfresh summary\n"
             + "  rbscenarioxmlfresh list [filter]\n"
             + "  rbscenarioxmlfresh scenario <scenario>\n"
             + "  rbscenarioxmlfresh layer <layer>\n"
             + "  rbscenarioxmlfresh disambiguate\n"
             + "  rbscenarioxmlfresh safety\n"
             + "\n"
             + "Aliases: rbscenarioxmlfresh, rbscenarioxml2, rbsxmlfresh\n"
             + "\n"
             + "This command is read-only. It does not parse, patch, generate, or reorganize XML.\n";
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
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetSummaryReport());
                return;

            case "list":
            case "ownership":
            case "xml":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetOwnershipReport(filter));
                return;

            case "scenario":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthScenarioXml] Usage: rbscenarioxmlfresh scenario <scenario>");
                    return;
                }
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetScenarioReport(_params[1]));
                return;

            case "layer":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthScenarioXml] Usage: rbscenarioxmlfresh layer <layer>");
                    return;
                }
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetLayerReport(_params[1]));
                return;

            case "disambiguate":
            case "disambiguation":
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetDisambiguationReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthScenarioXmlOwnershipRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthScenarioXml] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
