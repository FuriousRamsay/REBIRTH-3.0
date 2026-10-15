#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthScenarioPolicyFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbscenariopolicyfresh", "rbscenariopolicy2", "rbspolicyfresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh scenario runtime policy cache plans.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbscenariopolicyfresh summary\n"
             + "  rbscenariopolicyfresh list [filter]\n"
             + "  rbscenariopolicyfresh hotgates\n"
             + "  rbscenariopolicyfresh authority\n"
             + "  rbscenariopolicyfresh invalidation\n"
             + "  rbscenariopolicyfresh safety\n"
             + "\n"
             + "Aliases: rbscenariopolicyfresh, rbscenariopolicy2, rbspolicyfresh\n"
             + "\n"
             + "This command is read-only. It does not build live cache state or change gameplay.\n";
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
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetSummaryReport());
                return;

            case "list":
            case "snapshots":
            case "policies":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetSnapshotReport(filter));
                return;

            case "hotgates":
            case "hot":
            case "gates":
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetHotPathGateReport());
                return;

            case "authority":
            case "network":
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetAuthorityReport());
                return;

            case "invalidation":
            case "invalidate":
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetInvalidationReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthScenarioRuntimePolicyCacheRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthScenarioPolicyCache] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
