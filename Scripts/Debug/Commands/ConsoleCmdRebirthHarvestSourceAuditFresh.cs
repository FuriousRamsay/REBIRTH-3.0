#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthHarvestSourceAuditFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbharvestsourceauditfresh", "rbharvestaudit4b", "rb4baudit" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH Milestone 4B actual harvest/salvage source audit.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbharvestsourceauditfresh summary\n"
             + "  rbharvestsourceauditfresh list [filter]\n"
             + "  rbharvestsourceauditfresh selected\n"
             + "  rbharvestsourceauditfresh rejected\n"
             + "  rbharvestsourceauditfresh next\n"
             + "  rbharvestsourceauditfresh safety\n"
             + "\n"
             + "Aliases: rbharvestsourceauditfresh, rbharvestaudit4b, rb4baudit\n"
             + "\n"
             + "This command is read-only. It reports audited target selection only.\n";
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
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetSummaryReport());
                return;

            case "list":
            case "candidates":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetCandidateReport(filter));
                return;

            case "selected":
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetSelectedReport());
                return;

            case "rejected":
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetRejectedReport());
                return;

            case "next":
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetNextReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthHarvestSalvageActualSourceAuditRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthHarvestSalvageActualAudit] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
