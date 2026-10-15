#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthScenarioLedgerFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbscenarioledgerfresh", "rbscenarioledger2", "rbsurfacefresh" };
    }

    public override string getDescription()
    {
        return "Prints the read-only REBIRTH fresh 2.6 scenario surface ledger.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbscenarioledgerfresh summary\n"
             + "  rbscenarioledgerfresh list [filter]\n"
             + "  rbscenarioledgerfresh hot\n"
             + "  rbscenarioledgerfresh disambiguate\n"
             + "  rbscenarioledgerfresh impact\n"
             + "  rbscenarioledgerfresh safety\n"
             + "\n"
             + "Aliases: rbscenarioledgerfresh, rbscenarioledger2, rbsurfacefresh\n"
             + "\n"
             + "This command is read-only. It records audited 2.6 scenario surfaces before contracts are created.\n";
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
                Log.Out(RebirthScenarioSurfaceLedger.GetSummaryReport());
                return;

            case "list":
            case "surfaces":
            case "surface":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthScenarioSurfaceLedger.GetSurfaceReport(filter));
                return;

            case "hot":
            case "hotpaths":
            case "hotpath":
                Log.Out(RebirthScenarioSurfaceLedger.GetHotPathReport());
                return;

            case "disambiguate":
            case "disambiguation":
            case "survivor":
                Log.Out(RebirthScenarioSurfaceLedger.GetDisambiguationReport());
                return;

            case "impact":
            case "architecture":
                Log.Out(RebirthScenarioSurfaceLedger.GetArchitectureImpactReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthScenarioSurfaceLedger.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthScenarioLedger] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
