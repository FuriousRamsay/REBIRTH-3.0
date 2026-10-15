#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthFeatureDepsFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbfeaturedepsfresh", "rbfeaturedeps2", "rbdepsfresh" };
    }

    public override string getDescription()
    {
        return "Prints the read-only REBIRTH fresh feature dependency graph.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbfeaturedepsfresh summary\n"
             + "  rbfeaturedepsfresh list [filter]\n"
             + "  rbfeaturedepsfresh upstream\n"
             + "  rbfeaturedepsfresh guardrails\n"
             + "  rbfeaturedepsfresh blocked\n"
             + "  rbfeaturedepsfresh safety\n"
             + "\n"
             + "Aliases: rbfeaturedepsfresh, rbfeaturedeps2, rbdepsfresh\n"
             + "\n"
             + "This command is read-only. It does not choose or execute migration order.\n";
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
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetSummaryReport());
                return;

            case "list":
            case "deps":
            case "dependencies":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetDependencyReport(filter));
                return;

            case "upstream":
            case "prereqs":
            case "prerequisites":
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetUpstreamReport());
                return;

            case "guardrails":
            case "guardrail":
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetGuardrailReport());
                return;

            case "blocked":
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthFeatureDependencyGraphRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthFeatureDeps] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
