using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

[Preserve]
public sealed class ConsoleCmdRebirthPlayerId : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 1000;
    public override string[] getCommands() => new[] { "rebirth" };
    public override string getDescription() => "Shows your REBIRTH player ID.";
    public override string getHelp() => "rebirth playerid — prints your full persistent player ID for custom wrap assignments.";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var console = SingletonMonoBehaviour<SdtdConsole>.Instance;
        if (args == null || args.Count != 1 || !string.Equals(args[0], "playerid", StringComparison.OrdinalIgnoreCase))
        {
            console.Output(getHelp());
            return;
        }

        var manager = GameManager.Instance;
        string id;
        if (sender.RemoteClientInfo != null)
        {
            // Never substitute the host's identity for a remote caller.
            id = sender.RemoteClientInfo.InternalId?.CombinedString;
        }
        else
        {
            if (!sender.IsLocalGame || GameManager.IsDedicatedServer || manager?.World?.GetPrimaryPlayer() == null)
            {
                console.Output("[REBIRTH] Join a game as a player, then run rebirth playerid in your own console.");
                return;
            }
            id = manager.GetPersistentLocalPlayer()?.PrimaryId?.CombinedString
                ?? manager.getPersistentPlayerID(null)?.CombinedString;
        }

        console.Output(string.IsNullOrWhiteSpace(id)
            ? "[REBIRTH] Your player ID is not available yet. Try again after loading finishes."
            : "[REBIRTH] Player ID: " + id);
    }
}
