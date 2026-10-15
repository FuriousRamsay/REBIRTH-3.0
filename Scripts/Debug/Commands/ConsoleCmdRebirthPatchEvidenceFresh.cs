#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthPatchEvidenceFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbpatchevidencefresh", "rbpatchevidence2", "rbtargetevidencefresh" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh patch target evidence ledger.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbpatchevidencefresh summary\n"
             + "  rbpatchevidencefresh list [filter]\n"
             + "  rbpatchevidencefresh required\n"
             + "  rbpatchevidencefresh blocked\n"
             + "  rbpatchevidencefresh safety\n"
             + "\n"
             + "Aliases: rbpatchevidencefresh, rbpatchevidence2, rbtargetevidencefresh\n"
             + "\n"
             + "This command is read-only. It does not choose a patch target or install Harmony.\n";
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
                Log.Out(RebirthPatchTargetEvidenceLedger.GetSummaryReport());
                return;

            case "list":
            case "evidence":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthPatchTargetEvidenceLedger.GetEvidenceReport(filter));
                return;

            case "required":
                Log.Out(RebirthPatchTargetEvidenceLedger.GetRequiredReport());
                return;

            case "blocked":
            case "blockers":
                Log.Out(RebirthPatchTargetEvidenceLedger.GetBlockedReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthPatchTargetEvidenceLedger.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthPatchEvidence] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
