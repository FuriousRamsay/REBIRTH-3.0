#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthPerfFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbperffresh", "rbperf2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh performance toggle dry-run plans.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbperffresh summary\n"
             + "  rbperffresh baseline\n"
             + "  rbperffresh dryrun off <module-filter>\n"
             + "  rbperffresh dryrun on <module-filter>\n"
             + "  rbperffresh sweep\n"
             + "  rbperffresh safety\n"
             + "\n"
             + "Aliases: rbperffresh, rbperf2\n"
             + "\n"
             + "This command is read-only. It does not toggle features or collect FPS yet.\n";
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
                Log.Out(RebirthPerformanceDryRunPlanner.GetSummaryReport());
                return;

            case "baseline":
                Log.Out(RebirthPerformanceDryRunPlanner.GetBaselinePlanReport());
                return;

            case "dryrun":
            case "test":
                ExecuteDryRun(_params);
                return;

            case "sweep":
                Log.Out(RebirthPerformanceDryRunPlanner.GetSweepPlanReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthPerformanceDryRunPlanner.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthPerfDryRun] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }

    private static void ExecuteDryRun(List<string> _params)
    {
        if (_params == null || _params.Count < 3)
        {
            Log.Out("[RebirthPerfDryRun] Usage: rbperffresh dryrun off <module-filter> OR rbperffresh dryrun on <module-filter>");
            return;
        }

        string requestedState = (_params[1] ?? string.Empty).Trim().ToLowerInvariant();
        bool enable;
        if (requestedState == "on" || requestedState == "enable" || requestedState == "enabled")
            enable = true;
        else if (requestedState == "off" || requestedState == "disable" || requestedState == "disabled")
            enable = false;
        else
        {
            Log.Out("[RebirthPerfDryRun] Unknown state: " + requestedState + ". Use on/off.");
            return;
        }

        string filter = _params[2];
        Log.Out(RebirthPerformanceDryRunPlanner.GetDryRunReport(enable, filter));
    }
}
#endif
