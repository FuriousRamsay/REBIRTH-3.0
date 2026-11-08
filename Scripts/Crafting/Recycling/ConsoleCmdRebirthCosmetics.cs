using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// "rbcosmetics list" prints the armor cosmetics this character has unlocked (scrapping unlocks them, 2.6 rule);
/// "rbcosmetics reset" locks them all again (owned DLC sets stay available, as natively). Local player only.
/// </summary>
[Preserve]
public sealed class ConsoleCmdRebirthCosmetics : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "rbcosmetics" };
    public override string getDescription() => "Lists or resets the cosmetics unlocked by this character.";
    public override string getHelp() => "rbcosmetics list | reset";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var con = SingletonMonoBehaviour<SdtdConsole>.Instance;
        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player?.equipment == null) { con.Output("No local player."); return; }
        var unlocked = player.equipment.m_unlockedCosmetics;
        var names = Equipment.CosmeticMappingStringID.Where(pair => unlocked.Contains(pair.Value)).Select(pair => pair.Key).OrderBy(n => n).ToList();
        if (args.Count > 0 && args[0] == "reset")
        {
            unlocked.Clear();
            con.Output("Cosmetics locked again: " + names.Count + " removed. Owned DLC sets stay available.");
            Log.Out("[REBIRTH Cosmetics] reset " + names.Count + " unlocked cosmetics");
            return;
        }
        con.Output("Unlocked cosmetics (" + names.Count + "): " + string.Join(", ", names));
        Log.Out("[REBIRTH Cosmetics] unlocked (" + names.Count + "): " + string.Join(", ", names));
    }
}
