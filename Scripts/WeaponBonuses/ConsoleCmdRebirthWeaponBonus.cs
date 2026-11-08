using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Puts a weapon bonus (the 2.6 Moonlock class effects, now carried by the weapon) on the weapon in your hand.
/// rbweaponbonus &lt;type&gt; &lt;level&gt; | rbweaponbonus remove | rbweaponbonus list
/// </summary>
[Preserve]
public sealed class ConsoleCmdRebirthWeaponBonus : ConsoleCmdAbstract
{
    // The 2.6 class each bonus came from, so either name can be typed.
    private static readonly Dictionary<string, string> ClassNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "hunter", "ArrowRain" }, { "thug", "RangedStun" }, { "butcher", "RangedBleed" }, { "soldier", "RangedDoubleDamage" },
        { "technogeek", "RangedShock" }, { "madman", "RangedExplosion" }, { "builder", "RangedDismember" },
        { "chef", "RangedSlashes" }, { "souschef", "RangedSlashes" }, { "sous-chef", "RangedSlashes" },
    };

    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "rbweaponbonus", "rbwb" };
    public override string getDescription() => "Puts a weapon bonus of the type and level you choose on the weapon in your hand.";

    public override string getHelp()
    {
        var text = new StringBuilder();
        text.AppendLine("Usage:");
        text.AppendLine("  rbweaponbonus <type> <level>   puts that bonus on the weapon in your hand (level 1-" + RebirthWeaponBonuses.MaxLevel + ")");
        text.AppendLine("  rbweaponbonus remove           takes the bonus off the weapon in your hand");
        text.AppendLine("  rbweaponbonus list             lists the types");
        text.AppendLine("  rbweaponbonus force on|off     every hit with a bonus weapon triggers its bonus (for testing); no value shows the state");
        text.AppendLine("A weapon carries one bonus; a new one replaces the old one.");
        text.AppendLine("Type is the bonus key, the key without 'Ranged', or the 2.6 class name:");
        text.Append(TypeList());
        return text.ToString();
    }

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var console = SingletonMonoBehaviour<SdtdConsole>.Instance;
        if (args == null || args.Count == 0 || args[0] == "help" || args[0] == "?") { Lines(console, getHelp()); return; }
        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase)) { Lines(console, TypeList()); return; }

        var player = GameManager.Instance?.World?.GetPrimaryPlayer();
        if (player == null) { console.Output("Run this from the console of a player in game."); return; }
        if (args[0].Equals("force", StringComparison.OrdinalIgnoreCase))
        {
            // Kept on the player (synced), so it also applies where the server rolls the hit.
            if (args.Count > 1)
            {
                string mode = args[1].ToLowerInvariant();
                bool on = mode == "on" || mode == "1" || mode == "true";
                if (!on && mode != "off" && mode != "0" && mode != "false") { console.Output("Use: rbweaponbonus force on|off"); return; }
                player.Buffs.SetCustomVar(RebirthWeaponBonuses.ForceCVar, on ? 1f : 0f);
            }
            bool forced = player.Buffs.GetCustomVar(RebirthWeaponBonuses.ForceCVar) > 0f;
            console.Output(forced ? "Weapon bonus test mode is ON: every hit with a bonus weapon triggers its bonus. Turn it off with: rbweaponbonus force off"
                                  : "Weapon bonus test mode is OFF: bonuses trigger at their normal chance.");
            return;
        }
        int slot = player.inventory.holdingItemIdx;
        var stack = player.inventory.GetItem(slot)?.Clone();
        if (stack == null || stack.IsEmpty() || !RebirthWeaponBonuses.CanHold(stack.itemValue))
        { console.Output("Hold a weapon that can carry a bonus (a melee or ranged weapon with quality)."); return; }
        string weapon = stack.itemValue.ItemClass.GetLocalizedItemName();

        if (args[0].Equals("remove", StringComparison.OrdinalIgnoreCase) || args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            RebirthWeaponBonuses.Clear(stack.itemValue);
            player.inventory.SetItem(slot, stack);
            console.Output("Removed the weapon bonus from your " + weapon + ".");
            return;
        }

        var bonus = Resolve(args[0]);
        if (bonus == null) { console.Output("Unknown bonus type '" + args[0] + "'. Types:"); Lines(console, TypeList()); return; }
        int level;
        if (args.Count < 2 || !int.TryParse(args[1], out level) || level < 1 || level > RebirthWeaponBonuses.MaxLevel)
        { console.Output("Give a level from 1 to " + RebirthWeaponBonuses.MaxLevel + ", for example: rbweaponbonus " + args[0] + " 3"); return; }

        RebirthWeaponBonuses.Write(stack.itemValue, bonus, level);
        player.inventory.SetItem(slot, stack);
        console.Output("Your " + weapon + " now has " + Localization.Get("xui" + bonus.Key) + " level " + level + ".");
        Lines(console, RebirthWeaponBonuses.Describe(stack.itemValue, player));
    }

    internal static RebirthWeaponBonuses.Bonus Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string key;
        if (ClassNames.TryGetValue(name.Trim(), out key)) return RebirthWeaponBonuses.Find(key);
        foreach (var bonus in RebirthWeaponBonuses.All)
        {
            string shortKey = bonus.Key.StartsWith("Ranged", StringComparison.Ordinal) ? bonus.Key.Substring(6) : bonus.Key;
            string display = Localization.Get("xui" + bonus.Key);
            if (name.Equals(bonus.Key, StringComparison.OrdinalIgnoreCase) || name.Equals(shortKey, StringComparison.OrdinalIgnoreCase)
                || name.Replace("_", " ").Equals(display, StringComparison.OrdinalIgnoreCase)) return bonus;
        }
        return null;
    }

    private static string TypeList()
    {
        var text = new StringBuilder();
        foreach (var bonus in RebirthWeaponBonuses.All)
        {
            string shortKey = bonus.Key.StartsWith("Ranged", StringComparison.Ordinal) ? bonus.Key.Substring(6) : bonus.Key;
            string className = "";
            foreach (var pair in ClassNames) if (pair.Value == bonus.Key) { className = pair.Key; break; }
            text.AppendLine("  " + shortKey.PadRight(14) + Localization.Get("xui" + bonus.Key).PadRight(18) + "(" + className + ")");
        }
        return text.ToString();
    }

    private static void Lines(SdtdConsole console, string text)
    {
        foreach (var line in text.Split('\n')) if (line.Trim().Length > 0) console.Output(line.TrimEnd('\r'));
    }
}
