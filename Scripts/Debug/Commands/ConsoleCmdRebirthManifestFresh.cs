#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthManifestFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbmanifestfresh", "rbmanifest2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh consolidated module manifests.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbmanifestfresh summary\n"
             + "  rbmanifestfresh list [filter]\n"
             + "  rbmanifestfresh hot\n"
             + "  rbmanifestfresh measurable\n"
             + "  rbmanifestfresh ownership\n"
             + "  rbmanifestfresh safety\n"
             + "\n"
             + "Aliases: rbmanifestfresh, rbmanifest2\n"
             + "\n"
             + "This command is read-only. It does not toggle features or change gameplay.\n";
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
                Log.Out(RebirthModuleManifestRegistry.GetSummaryReport());
                return;

            case "list":
            case "modules":
            case "manifest":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthModuleManifestRegistry.GetManifestReport(filter));
                return;

            case "hot":
            case "hotpath":
            case "hotpaths":
                Log.Out(RebirthModuleManifestRegistry.GetHotPathManifestReport());
                return;

            case "measurable":
            case "measure":
            case "impact":
                Log.Out(RebirthModuleManifestRegistry.GetMeasurementManifestReport());
                return;

            case "ownership":
            case "owners":
                Log.Out(RebirthModuleManifestRegistry.GetOwnershipSummaryReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthModuleManifestRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthModuleManifest] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
