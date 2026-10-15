#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthHarmonyStrategyFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbharmonystrategyfresh", "rbharmonystrategy2", "rbpatchstrategyfresh" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh Harmony patch strategy and reflection policy.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbharmonystrategyfresh summary\n"
             + "  rbharmonystrategyfresh list [filter]\n"
             + "  rbharmonystrategyfresh hot\n"
             + "  rbharmonystrategyfresh reflection\n"
             + "  rbharmonystrategyfresh patchtypes\n"
             + "  rbharmonystrategyfresh blocked\n"
             + "  rbharmonystrategyfresh safety\n"
             + "\n"
             + "Aliases: rbharmonystrategyfresh, rbharmonystrategy2, rbpatchstrategyfresh\n"
             + "\n"
             + "This command is read-only. It does not install patches or perform reflection.\n";
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
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetSummaryReport());
                return;

            case "list":
            case "strategies":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetStrategyReport(filter));
                return;

            case "hot":
            case "hotpath":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetHotPathReport());
                return;

            case "reflection":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetReflectionReport());
                return;

            case "patchtypes":
            case "types":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetPatchTypeReport());
                return;

            case "blocked":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthHarmonyPatchStrategyRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthHarmonyStrategy] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
