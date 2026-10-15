#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthPatchAdapterFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbpatchfresh", "rbpatch2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh patch-adapter plans.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbpatchfresh summary\n"
             + "  rbpatchfresh list [filter]\n"
             + "  rbpatchfresh excluded\n"
             + "  rbpatchfresh measure\n"
             + "  rbpatchfresh safety\n"
             + "\n"
             + "Aliases: rbpatchfresh, rbpatch2\n"
             + "\n"
             + "This command is read-only. It does not install or remove Harmony patches.\n";
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
                Log.Out(RebirthPatchAdapterPlanRegistry.GetSummaryReport());
                return;

            case "list":
            case "adapters":
            case "adapter":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthPatchAdapterPlanRegistry.GetReport(filter));
                return;

            case "excluded":
            case "exclude":
                Log.Out(RebirthPatchAdapterPlanRegistry.GetExcludedReport());
                return;

            case "measure":
            case "measurement":
            case "patchmeasure":
                Log.Out(RebirthPatchAdapterPlanRegistry.GetMeasurementReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthPatchAdapterPlanRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthPatchAdapters] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
