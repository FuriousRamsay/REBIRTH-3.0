#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthBuildProfileFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbprofilefresh", "rbprofile2" };
    }

    public override string getDescription()
    {
        return "Prints read-only REBIRTH fresh build profile contracts.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbprofilefresh summary\n"
             + "  rbprofilefresh list [filter]\n"
             + "  rbprofilefresh rules\n"
             + "  rbprofilefresh patchcompat\n"
             + "  rbprofilefresh safety\n"
             + "\n"
             + "Aliases: rbprofilefresh, rbprofile2\n"
             + "\n"
             + "This command is read-only. It does not change compiler symbols or build output.\n";
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
                Log.Out(RebirthBuildProfileRegistry.GetSummaryReport());
                return;

            case "list":
            case "profiles":
            case "profile":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthBuildProfileRegistry.GetProfileReport(filter));
                return;

            case "rules":
                Log.Out(RebirthBuildProfileRegistry.GetRuleReport());
                return;

            case "patchcompat":
            case "patches":
                Log.Out(RebirthBuildProfileRegistry.GetPatchAdapterCompatibilityReport());
                return;

            case "safety":
                Log.Out(RebirthBuildProfileRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthBuildProfiles] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
