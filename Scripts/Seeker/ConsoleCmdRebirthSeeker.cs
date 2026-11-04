using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

[Preserve]
public sealed class ConsoleCmdRebirthSeeker : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => false;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "rbseeker", "rbentityevent" };
    public override string getDescription() => Localization.Get("rbSeekerCommandDescription");
    public override string getHelp() => Localization.Get("rbSeekerCommandHelp");
    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var world = GameManager.Instance?.World;
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        var client = sender.RemoteClientInfo;
        EntityPlayer player = client == null ? world?.GetPrimaryPlayer() : world?.GetEntity(client.entityId) as EntityPlayer;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() || connection == null || !connection.IsServer
            || player == null || !ReferenceEquals(player.world, world) || !ReferenceEquals(world.GetEntity(player.entityId), player) || !player.IsAlive() || !player.IsSpawned()
            || (client != null ? !client.loginDone || !ReferenceEquals(connection.Clients.ForEntityId(player.entityId), client)
                : GameManager.IsDedicatedServer || !(player is EntityPlayerLocal)))
        { SdtdConsole.Instance.Output(Localization.Get("rbSeekerCommandAuth")); return; }
        var input = args == null ? new List<string>() : new List<string>(args);
        if (input.Count > 0 && input[0].Equals("seeker", StringComparison.OrdinalIgnoreCase)) input.RemoveAt(0);
        int level = player.Progression.Level, repeats = 1;
        if (input.Count > 2 || input.Count > 0 && !int.TryParse(input[0], out level)
            || input.Count > 1 && !int.TryParse(input[1], out repeats) || level < 30 || level > 1000 || repeats < 1 || repeats > 4)
        { SdtdConsole.Instance.Output(getHelp()); return; }
        int spawned = 0;
        for (int i = 0; i < repeats; i++) spawned += RebirthSeekerRuntime.SpawnEvent(world, player, RebirthSeekerPolicy.Tier(level, false), false);
        SdtdConsole.Instance.Output(string.Format(System.Globalization.CultureInfo.InvariantCulture, Localization.Get("rbSeekerCommandResult"), spawned));
    }
}
