#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthFeaturesFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbfeaturesfresh", "rbfeatures2", "rbfeaturefresh" };
    }

    public override string getDescription()
    {
        return "Prints the read-only REBIRTH fresh feature domain coverage ledger.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbfeaturesfresh summary\n"
             + "  rbfeaturesfresh list [filter]\n"
             + "  rbfeaturesfresh hot\n"
             + "  rbfeaturesfresh network\n"
             + "  rbfeaturesfresh persistence\n"
             + "  rbfeaturesfresh scenario\n"
             + "  rbfeaturesfresh blocked\n"
             + "  rbfeaturesfresh safety\n"
             + "\n"
             + "Aliases: rbfeaturesfresh, rbfeatures2, rbfeaturefresh\n"
             + "\n"
             + "This command is read-only. It does not migrate or connect gameplay behavior.\n";
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
                Log.Out(RebirthFeatureDomainRegistry.GetSummaryReport());
                return;

            case "list":
            case "domains":
            case "features":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthFeatureDomainRegistry.GetDomainReport(filter));
                return;

            case "hot":
            case "hotpath":
            case "hotpaths":
                Log.Out(RebirthFeatureDomainRegistry.GetHotPathReport());
                return;

            case "network":
            case "authority":
                Log.Out(RebirthFeatureDomainRegistry.GetNetworkReport());
                return;

            case "persistence":
            case "saveload":
                Log.Out(RebirthFeatureDomainRegistry.GetPersistenceReport());
                return;

            case "scenario":
            case "scenarios":
                Log.Out(RebirthFeatureDomainRegistry.GetScenarioReport());
                return;

            case "blocked":
                Log.Out(RebirthFeatureDomainRegistry.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthFeatureDomainRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthFeatures] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
