#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthBuildFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbbuildfresh", "rbbuild2" };
    }

    public override string getDescription()
    {
        return "Manages read-only in-memory REBIRTH fresh build profile selection and dry-run manifests.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbbuildfresh summary\n"
             + "  rbbuildfresh select release|profiling|stripped|devdiagnostics|compataudit\n"
             + "  rbbuildfresh status\n"
             + "  rbbuildfresh manifest [profile]\n"
             + "  rbbuildfresh reset\n"
             + "  rbbuildfresh safety\n"
             + "\n"
             + "Aliases: rbbuildfresh, rbbuild2\n"
             + "\n"
             + "This command is read-only. It does not change compiler symbols, csproj, or build output.\n";
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
                Log.Out(RebirthBuildProfileSelectionRegistry.GetStatusReport());
                return;

            case "select":
            case "set":
                if (_params == null || _params.Count < 2)
                {
                    Log.Out("[RebirthBuildProfileSelection] Usage: rbbuildfresh select release|profiling|stripped|devdiagnostics|compataudit");
                    return;
                }
                Log.Out(RebirthBuildProfileSelectionRegistry.SetProfile(_params[1]));
                return;

            case "status":
                Log.Out(RebirthBuildProfileSelectionRegistry.GetStatusReport());
                return;

            case "manifest":
            case "dryrun":
                string profile = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthBuildProfileSelectionRegistry.GetDryRunManifestReport(profile));
                return;

            case "reset":
                Log.Out(RebirthBuildProfileSelectionRegistry.Reset());
                return;

            case "safety":
                Log.Out(RebirthBuildProfileSelectionRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthBuildProfileSelection] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
