#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthPathFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbpathfresh", "rbpath2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh pathing tenant reports.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbpathfresh summary\n"
             + "  rbpathfresh tenants\n"
             + "  rbpathfresh budget\n"
             + "  rbpathfresh astar\n"
             + "  rbpathfresh safety\n"
             + "  rbpathfresh hybrid\n"
             + "  rbpathfresh debug on|off\n"
             + "  rbpathfresh reset\n"
             + "\n"
             + "Aliases: rbpathfresh, rbpath2\n"
             + "\n"
             + "Hybrid status is read-only. The debug subcommand only controls shortcut logging.\n";
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
                Log.Out(RebirthPathingPipeline.GetSummaryReport());
                return;

            case "tenants":
            case "stages":
            case "pipeline":
                Log.Out(RebirthPathingPipeline.GetTenantReport());
                return;

            case "budget":
            case "budgets":
            case "localroutes":
                Log.Out(RebirthPathingPipeline.GetBudgetReport());
                return;

            case "astar":
            case "astarrisk":
            case "risk":
                Log.Out(RebirthPathingPipeline.GetAStarRiskReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthPathingPipeline.GetSafetyReport());
                return;

            case "hybrid":
            case "smoothing":
                Log.Out(RebirthHybridPathSmoothing.GetStatusReport());
                return;

            case "debug":
                if (_params != null && _params.Count > 1)
                {
                    string value = (_params[1] ?? string.Empty).Trim().ToLowerInvariant();
                    RebirthHybridPathSmoothing.DebugLogging = value == "on" || value == "true" || value == "1";
                }
                Log.Out("[RebirthHybridPath] debug=" + RebirthHybridPathSmoothing.DebugLogging);
                return;

            case "reset":
                RebirthHybridPathSmoothing.ResetCounters();
                Log.Out(RebirthHybridPathSmoothing.GetStatusReport());
                return;

            default:
                Log.Out("[RebirthPathing] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
