#if DEBUG
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

// Debug-only diagnostic command. Release builds intentionally expose only the
// no-op compatibility facade, so help must not advertise this command there.
// Each Debug machine runs its own independent trace flag.
[Preserve]
public sealed class ConsoleCmdRebirthProtectCrateTrace : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient
    {
        get { return true; }
    }

    public override string[] getCommands()
    {
        return new[] { "rbcratetrace" };
    }

    public override string getDescription()
    {
        return "Toggles Debug-build rate-limited tracing for REBIRTH Protect Supply Crates.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbcratetrace on\n"
             + "  rbcratetrace off\n"
             + "  rbcratetrace status\n"
             + "\n"
             + "Writes lines prefixed \"[REBIRTH ProtectCrate Trace]\" to this machine's log.\n"
             + "Run on the server for defender-targeting evidence (server-authoritative AI)\n"
             + "and on the client for hovering-health-marker evidence (client-only UI).\n"
             + "This command is available only when the mod is compiled with DEBUG.\n";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string command = parameters == null || parameters.Count == 0
            ? "status"
            : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();

        switch (command)
        {
            case "on":
            case "true":
            case "1":
                RebirthProtectCrateTrace.SetEnabled(true);
                Output("[REBIRTH ProtectCrate Trace] tracing ON (" + RebirthProtectCrateTrace.DescribeRole() + ")");
                return;

            case "off":
            case "false":
            case "0":
                RebirthProtectCrateTrace.SetEnabled(false);
                Output("[REBIRTH ProtectCrate Trace] tracing OFF");
                return;

            case "status":
                Output("[REBIRTH ProtectCrate Trace] enabled=" + RebirthProtectCrateTrace.Enabled
                    + " " + RebirthProtectCrateTrace.DescribeRole());
                return;

            default:
                Output("[REBIRTH ProtectCrate Trace] Unknown subcommand: " + command);
                Output(getHelp());
                return;
        }
    }

    private static void Output(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        string[] lines = text.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            SdtdConsole.Instance.Output(lines[i]);
        }
    }
}
#endif
