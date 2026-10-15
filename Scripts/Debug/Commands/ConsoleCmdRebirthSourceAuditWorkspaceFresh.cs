#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class ConsoleCmdRebirthSourceAuditWorkspaceFresh : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient { get { return true; } }

    public override string[] getCommands()
    {
        return new[] { "rbsourceauditfresh", "rbsourceaudit2", "rbauditworkspace" };
    }

    public override string getDescription()
    {
        return "Prints the REBIRTH fresh Milestone 4A source audit workspace.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbsourceauditfresh summary\n"
             + "  rbsourceauditfresh files [filter]\n"
             + "  rbsourceauditfresh required\n"
             + "  rbsourceauditfresh nogo\n"
             + "  rbsourceauditfresh next\n"
             + "  rbsourceauditfresh safety\n"
             + "\n"
             + "Aliases: rbsourceauditfresh, rbsourceaudit2, rbauditworkspace\n"
             + "\n"
             + "This command is read-only. It does not inspect files or install patches.\n";
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
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetSummaryReport());
                return;

            case "files":
            case "list":
                string filter = (_params != null && _params.Count > 1) ? _params[1] : null;
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetFilesReport(filter));
                return;

            case "required":
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetRequiredFilesReport());
                return;

            case "nogo":
            case "blocked":
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetNoGoReport());
                return;

            case "next":
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetNextReport());
                return;

            case "safety":
            case "rules":
                Log.Out(RebirthSourceAuditWorkspaceRegistry.GetSafetyReport());
                return;

            default:
                Log.Out("[RebirthSourceAuditWorkspace] Unknown subcommand: " + cmd);
                Log.Out(getHelp());
                return;
        }
    }
}
#endif
