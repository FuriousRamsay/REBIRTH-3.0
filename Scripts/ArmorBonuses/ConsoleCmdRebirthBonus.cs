using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting;
using Random = System.Random;

#nullable disable

/// <summary>
/// Armor bonus focus checks (stage 1 of _Documentation/ArmorBonusFocus_ImplementationPlan_20261108.md):
///   rbbonus tree [slot]                                     the category tree with counts and any problems
///   rbbonus roll &lt;slot&gt; &lt;focus&gt; &lt;kitLevel&gt; &lt;quality&gt; [count]  a sample of focused rolls (focus: any | cat:ID | eff:CAT/FAMILY | exact:KEY)
///   rbbonus reroll &lt;exactKey&gt; &lt;kitLevel&gt; &lt;quality&gt; [count]    a sample of Retuning "Reroll" results for one exact bonus
///   rbbonus try &lt;kit&gt; &lt;armorItem&gt; [quality=N] [tailoring=N] [start=id:value,...] [add=FOCUS]... [row=ACTION[:FOCUS]]...
///                                                            the kit rules on a test item, without a station (nothing is given or taken)
///   rbbonus apply [add=FOCUS]... [row=ACTION[:FOCUS]]...     applies the kit in the open Modifier Station with that request (real path, MP included)
///   rbbonus selftest                                        every kit rule against real rolls (RebirthArmorBonusFocusSelfTest); prints PASS/FAIL lines
/// Output also goes to the log with the [REBIRTH BonusCheck] prefix.
/// </summary>
[Preserve]
public sealed class ConsoleCmdRebirthBonus : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "rbbonus" };
    public override string getDescription() => "Armor bonus focus tree and roll samples.";
    public override string getHelp() => "rbbonus tree [slot] | roll <slot> <focus> <kitLevel> <quality> [count] | reroll <exactKey> <kitLevel> <quality> [count] | try <kit> <armorItem> [quality=N] [tailoring=N] [start=id:value,...] [add=FOCUS] [row=ACTION[:FOCUS]] | apply [add=FOCUS] [row=ACTION[:FOCUS]] | selftest";

    private static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Legendary" };

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        string mode = args.Count > 0 ? args[0] : "tree";
        try
        {
            if (mode == "tree") Tree(args.Count > 1 ? args[1] : null);
            else if (mode == "roll" && args.Count >= 5) Sample(args);
            else if (mode == "reroll" && args.Count >= 4) Reroll(args);
            else if (mode == "try" && args.Count >= 3) Try(args);
            else if (mode == "apply") ApplyAtStation(args);
            else if (mode == "selftest") RebirthArmorBonusFocusSelfTest.Run(Say);
            else Say(getHelp());
        }
        catch (Exception e) { Say("rbbonus failed: " + e.Message); }
    }

    private static void Say(string line)
    {
        SingletonMonoBehaviour<SdtdConsole>.Instance.Output(line);
        Log.Out("[REBIRTH BonusCheck] " + line);
    }

    private static void Tree(string slotFilter)
    {
        int assigned = 0;
        foreach (EquipmentSlots slot in new[] { EquipmentSlots.Head, EquipmentSlots.Chest, EquipmentSlots.Hands, EquipmentSlots.Feet })
        {
            if (slotFilter != null && !string.Equals(slotFilter, slot.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
            var cats = RebirthArmorBonusTree.Categories(slot);
            int slotRows = cats.Sum(c => c.RowCount);
            assigned += slotRows;
            Say(slot + ": " + slotRows + " bonuses in " + cats.Count + " categories");
            foreach (var c in cats)
            {
                Say("  " + c.Id + " (" + Localization.Get(c.NameKey) + "): " + c.Effects.Count + " effects, " + c.Effects.Sum(e => e.Exacts.Count) + " exact, " + c.RowCount + " rows");
                if (slotFilter == null) continue;
                foreach (var e in c.Effects)
                    Say("    " + e.FamilyId + ": " + string.Join(", ", e.Exacts.Select(x => x.Key + "[" + string.Join("", x.Rarities.Select(r => RarityNames[(int)r][0])) + "]")));
            }
        }
        int total = RebirthArmorBonusCatalog.Definitions.Count(d => !d.Retired && (slotFilter == null || string.Equals(slotFilter, d.Slot.ToString(), StringComparison.OrdinalIgnoreCase)));
        Say("Assigned " + assigned + " of " + total + " bonuses. Problems: " + RebirthArmorBonusTree.Problems.Count);
        foreach (var p in RebirthArmorBonusTree.Problems.Take(40)) Say("  ! " + p);
    }

    private static void Sample(List<string> args)
    {
        var slot = (EquipmentSlots)Enum.Parse(typeof(EquipmentSlots), args[1], true);
        if (!RebirthArmorBonusFocus.TryParse(args[2], out var focus)) { Say("Unknown focus " + args[2]); return; }
        int level = int.Parse(args[3]), quality = int.Parse(args[4]), count = args.Count > 5 ? int.Parse(args[5]) : 1000;
        var pool = RebirthArmorBonusTree.Pool(slot, focus);
        if (pool.Count == 0) { Say("Focus " + focus + " has no bonuses on " + slot); return; }
        var random = new Random(12345);
        var rarity = new int[4];
        var byCategory = new Dictionary<string, int>();
        var byFamily = new Dictionary<string, int>();
        int outside = 0, empty = 0;
        for (int i = 0; i < count; i++)
        {
            var rolled = RebirthArmorBonusRoller.Roll(slot, new RebirthArmorRolledBonus[0], 1, level, quality, random, focus);
            if (rolled.Count == 0) { empty++; continue; }
            var d = rolled[0].Definition;
            if (!pool.Contains(d.Id)) outside++;
            if (rolled[0].Value < Math.Min(d.Min, d.Max) || rolled[0].Value > Math.Max(d.Min, d.Max)) outside++;
            rarity[(int)d.Rarity]++;
            string cat = RebirthArmorBonusTree.CategoryOf(d)?.Id ?? "?";
            byCategory[cat] = byCategory.TryGetValue(cat, out var n) ? n + 1 : 1;
            byFamily[d.FamilyId] = byFamily.TryGetValue(d.FamilyId, out var m) ? m + 1 : 1;
        }
        Say(slot + " " + focus + " kit " + level + " q" + quality + ": " + count + " rolls, pool " + pool.Count + " rows, " + byFamily.Count + " families hit, outside pool/range " + outside + ", empty " + empty);
        Say("  kit weights " + string.Join("/", RebirthArmorBonusCatalog.GetRarityWeights(level, quality)) + "; rarity " + Percentages(rarity, count));
        Say("  categories " + string.Join(", ", byCategory.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value)));
        Say("  families " + string.Join(", ", byFamily.OrderByDescending(p => p.Value).Take(12).Select(p => p.Key + " " + p.Value)));
        if (focus.Depth == RebirthArmorBonusFocusDepth.Exact && RebirthArmorBonusTree.TryGetExact(focus.ExactKey, out var exact)) Say("  expected " + Expected(exact, level, quality));
    }

    private static void Reroll(List<string> args)
    {
        if (!RebirthArmorBonusTree.TryGetExact(args[1], out var exact)) { Say("Unknown exact bonus " + args[1]); return; }
        int level = int.Parse(args[2]), quality = int.Parse(args[3]), count = args.Count > 4 ? int.Parse(args[4]) : 1000;
        var random = new Random(54321);
        var start = new RebirthArmorRolledBonus { Definition = exact.Rows[0], Value = exact.Rows[0].Min };
        var rarity = new int[4];
        int changedKey = 0;
        for (int i = 0; i < count; i++)
        {
            var next = RebirthArmorBonusRoller.RerollSame(start, level, quality, random);
            if (RebirthArmorBonusTree.ExactKey(next.Definition.Id) != exact.Key) changedKey++;
            rarity[(int)next.Definition.Rarity]++;
        }
        Say(exact.Key + " reroll kit " + level + " q" + quality + ": " + count + " rolls, key changed " + changedKey + "; rarity " + Percentages(rarity, count));
        Say("  expected " + Expected(exact, level, quality));
    }

    private static void Try(List<string> args)
    {
        var kit = ItemClass.GetItem(args[1]);
        var item = ItemClass.GetItem(args[2]);
        if (kit == null || kit.IsEmpty() || item == null || item.IsEmpty()) { Say("Unknown kit or item"); return; }
        float tailoring = 0f;
        var words = new List<string>();
        string start = null;
        foreach (string word in args.Skip(3))
        {
            if (word.StartsWith("quality=")) item.Quality = (ushort)int.Parse(word.Substring(8));
            else if (word.StartsWith("tailoring=")) tailoring = float.Parse(word.Substring(10), System.Globalization.CultureInfo.InvariantCulture);
            else if (word.StartsWith("start=")) start = word.Substring(6);
            else words.Add(word);
        }
        if (item.Quality == 0) item.Quality = 4;
        if (!string.IsNullOrEmpty(start)) item.SetMetadata(RebirthArmorBonusStorage.MetadataKey, start.Replace(',', ';'));
        if (!RebirthArmorBonusKitRequest.TryParseConsole(words, out var request, out string error)) { Say(error); return; }
        Say("try " + args[1] + " on " + args[2] + " q" + item.Quality + " tailoring " + tailoring + ": " + request);
        Say("  before: " + Plain(item));
        if (RebirthArmorBonusKits.TryApply(kit, item, request, tailoring, new Random(), out var result, out string failKey)) Say("  after:  " + Plain(result));
        else Say("  refused: " + failKey + " (" + Localization.Get(failKey) + ")");
    }

    private static string Plain(ItemValue item)
    {
        var bonuses = RebirthArmorBonusStorage.Read(item);
        return bonuses.Length == 0 ? "(none)" : string.Join(" | ", bonuses.Select(b => b.Definition.Id + " " + b.Value.ToString("0.###") + " [" + RebirthArmorBonusTree.CategoryOf(b.Definition)?.Id + "]"));
    }

    private static void ApplyAtStation(List<string> args)
    {
        var panel = XUiC_RebirthModifierPanel.Current;
        if (panel == null) { Say("Open a Modifier Station first."); return; }
        if (!RebirthArmorBonusKitRequest.TryParseConsole(args.Skip(1), out var request, out string error)) { Say(error); return; }
        Say("apply at station: " + request);
        panel.LastBefore = panel.LastAfter = string.Empty;
        panel.Apply(request);
        if (panel.LastAfter.Length > 0) Say("  before [" + panel.LastBefore + "] after [" + panel.LastAfter + "]");
    }

    private static string Percentages(int[] counts, int total) =>
        string.Join(" ", Enumerable.Range(0, 4).Select(i => RarityNames[i][0] + " " + (100.0 * counts[i] / Math.Max(1, total)).ToString("0.0") + "%"));

    private static string Expected(RebirthArmorBonusTree.Exact exact, int level, int quality)
    {
        var weights = RebirthArmorBonusCatalog.GetRarityWeights(level, quality);
        var has = new bool[4];
        foreach (var r in exact.Rarities) has[(int)r] = true;
        int total = Enumerable.Range(0, 4).Where(i => has[i]).Sum(i => weights[i]);
        return string.Join(" ", Enumerable.Range(0, 4).Select(i => RarityNames[i][0] + " " + (has[i] && total > 0 ? 100.0 * weights[i] / total : 0).ToString("0.0") + "%"));
    }
}
