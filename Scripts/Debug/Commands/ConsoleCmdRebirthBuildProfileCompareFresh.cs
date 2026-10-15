#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthBuildProfileCompareFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbprofilecomparefresh", "rbprofilecompare2", "rbbuildcomparefresh" };
    }

    public override string getDescription()
    {
        return "Prints a dry-run REBIRTH fresh build/profile comparison report.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbprofilecomparefresh summary\n"
             + "  rbprofilecomparefresh list [filter]\n"
             + "  rbprofilecomparefresh release\n"
             + "  rbprofilecomparefresh profiling\n"
             + "  rbprofilecomparefresh safety\n"
             + "\n"
             + "Aliases: rbprofilecomparefresh, rbprofilecompare2, rbbuildcomparefresh\n"
             + "\n"
             + "This command is dry-run only. It does not build or change compiler symbols.\n";
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
                Log.Out(RebirthBuildProfileComparisonReport.GetSummaryReport());
                return;

            case "list":
            case "compare":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthBuildProfileComparisonReport.GetComparisonReport(filter));
                return;

            case "release":
                Log.Out(RebirthBuildProfileComparisonReport.GetReleaseReport());
                return;

            case "profiling":
            case "profiles":
            case "stripped":
                Log.Out(RebirthBuildProfileComparisonReport.GetProfilingReport());
                return;

            case "safety":
                Log.Out(RebirthBuildProfileComparisonReport.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthBuildProfileCompare] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
