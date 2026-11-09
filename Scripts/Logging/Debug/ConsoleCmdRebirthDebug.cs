using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// "rbdebug" turns REBIRTH debug logging on or off for this session (Debug builds only; this file is left out of Release).
///   rbdebug                 show the state
///   rbdebug on              log every area
///   rbdebug on survivor     log one area (repeat for more): the top folder under Scripts, e.g. survivor, vehicles, npc, crafting
///   rbdebug off [area]      stop all, or one area
/// </summary>
[Preserve]
public sealed class ConsoleCmdRebirthDebug : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    // Turned on before joining a server, to log the spawn (owner 2026-11-08).
    public override bool AllowedInMainMenu => true;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "rbdebug" };
    public override string getDescription() => "Turns REBIRTH debug logging on or off (Debug builds).";
    public override string getHelp() => "rbdebug | rbdebug on [area] | rbdebug off [area]   (areas: the top folder under Scripts, e.g. survivor, vehicles, npc, crafting, purge)";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var console = SingletonMonoBehaviour<SdtdConsole>.Instance;
        string verb = args.Count > 0 ? args[0].ToLowerInvariant() : string.Empty;
        string area = args.Count > 1 ? args[1].ToLowerInvariant() : null;
        if (verb == "on") RebirthDebug.TurnOn(area);
        else if (verb == "off") RebirthDebug.TurnOff(area);
        else if (verb.Length > 0) { console.Output(getHelp()); return; }
        console.Output("REBIRTH debug logging: " + RebirthDebug.Describe());
    }
}
