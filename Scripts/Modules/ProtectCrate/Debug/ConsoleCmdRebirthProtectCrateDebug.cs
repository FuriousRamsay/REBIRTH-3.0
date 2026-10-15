#if DEBUG
using System;
using System.Collections.Generic;
using Platform;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthProtectCrateDebug : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient
    {
        get { return true; }
    }

    public override string[] getCommands()
    {
        return new[] { "rbprotectcrate", "rbcrate" };
    }

    public override string getDescription()
    {
        return "Diagnoses REBIRTH Protect Supply Crates targeting, persistence, and native target-bar eligibility.";
    }

    public override string getHelp()
    {
        return "Usage:\n"
             + "  rbcrate status\n"
             + "  rbcrate targets\n"
             + "  rbcrate rescan\n"
             + "\n"
             + "status  : reports the option, nearest crate state, and native target-bar eligibility.\n"
             + "targets : lists loaded marked defenders, crate objective, attack target, and revenge target.\n"
             + "rescan  : server-side recovery scan that restores persisted crate objectives.\n";
    }

    public override void Execute(List<string> parameters, CommandSenderInfo senderInfo)
    {
        string command = parameters == null || parameters.Count == 0
            ? "status"
            : (parameters[0] ?? string.Empty).Trim().ToLowerInvariant();

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayer requester = ResolveRequester(world, senderInfo);

        switch (command)
        {
            case "help":
            case "?":
                OutputMultiline(getHelp());
                return;

            case "status":
            case "state":
                OutputMultiline(RebirthProtectCrateService.DebugDescribe(world, requester));
                OutputMultiline(RebirthProtectCrateNativeTargetBar.DebugDescribe());
                return;


            case "targets":
            case "defenders":
                OutputMultiline(RebirthProtectCrateService.DebugDescribeTargets(world));
                return;

            case "rescan":
            case "restore":
                OutputMultiline(RebirthProtectCrateService.DebugRescanLoadedDefenders(world));
                OutputMultiline(RebirthProtectCrateService.DebugDescribeTargets(world));
                return;

            default:
                OutputMultiline("[REBIRTH ProtectCrate Debug] Unknown subcommand: " + command);
                OutputMultiline(getHelp());
                return;
        }
    }

    private static EntityPlayer ResolveRequester(World world, CommandSenderInfo senderInfo)
    {
        if (world == null) return null;

        if (senderInfo.RemoteClientInfo != null)
        {
            return world.GetEntity(senderInfo.RemoteClientInfo.entityId) as EntityPlayer;
        }

        return world.GetPrimaryPlayer();
    }

    private static void OutputMultiline(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        string[] lines = text.Replace("\r", string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            SdtdConsole.Instance.Output(lines[i]);
            Log.Out(lines[i]);
        }
    }
}
#endif
