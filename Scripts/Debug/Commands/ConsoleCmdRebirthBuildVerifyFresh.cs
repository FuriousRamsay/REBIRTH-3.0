#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthBuildVerifyFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbbuildverifyfresh", "rbbuildverify2", "rbverifyfresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh build verification readiness gates.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbbuildverifyfresh summary\n"
             + "  rbbuildverifyfresh list [filter]\n"
             + "  rbbuildverifyfresh release\n"
             + "  rbbuildverifyfresh profiling\n"
             + "  rbbuildverifyfresh hot\n"
             + "  rbbuildverifyfresh safety\n"
             + "\n"
             + "Aliases: rbbuildverifyfresh, rbbuildverify2, rbverifyfresh\n"
             + "\n"
             + "This command is read-only. It does not build or change compiler symbols.\n";
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
                Log.Out(RebirthBuildVerificationRegistry.GetSummaryReport());
                return;

            case "list":
            case "gates":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthBuildVerificationRegistry.GetGateReport(filter));
                return;

            case "release":
                Log.Out(RebirthBuildVerificationRegistry.GetReleaseReport());
                return;

            case "profiling":
            case "profiles":
            case "stripped":
                Log.Out(RebirthBuildVerificationRegistry.GetProfilingReport());
                return;

            case "hot":
            case "hotpath":
                Log.Out(RebirthBuildVerificationRegistry.GetHotPathReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthBuildVerificationRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthBuildVerify] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
