using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthNetPersistFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbnetpersistfresh", "rbnetpersist2", "rbauthorityfresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh network authority and persistence contracts.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbnetpersistfresh summary\n"
             + "  rbnetpersistfresh list [filter]\n"
             + "  rbnetpersistfresh authority\n"
             + "  rbnetpersistfresh persistence\n"
             + "  rbnetpersistfresh blocked\n"
             + "  rbnetpersistfresh safety\n"
             + "\n"
             + "Aliases: rbnetpersistfresh, rbnetpersist2, rbauthorityfresh\n"
             + "\n"
             + "This command is read-only. It does not send packets or save data.\n";
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
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetSummaryReport());
                return;

            case "list":
            case "contracts":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetContractReport(filter));
                return;

            case "authority":
            case "network":
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetAuthorityReport());
                return;

            case "persistence":
            case "saveload":
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetPersistenceReport());
                return;

            case "blocked":
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthNetworkPersistenceContractRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthNetPersist] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
