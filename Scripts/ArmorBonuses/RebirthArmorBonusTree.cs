using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Armor bonus focus tree (spec: _Documentation/ArmorBonusFocus_Spec_20261108.md, sections 4 and 10):
/// armor type → category → effect (bonus family) → exact bonus (bonus id without its rarity word) → rarity rows.
/// Built once from the loaded catalog plus Config/_ArmorBonuses/bonus_categories.xml.
/// </summary>
public static class RebirthArmorBonusTree
{
    public sealed class Exact
    {
        public string Key, FamilyId;
        /// <summary>What it applies to (null = everything in its family, e.g. "all ranged weapons").</summary>
        public string[] Tokens;
        public RebirthArmorBonusRarity[] Rarities;
        public readonly List<RebirthArmorBonusDefinition> Rows = new List<RebirthArmorBonusDefinition>();
    }
    /// <summary>
    /// A "what it affects" pick (owner 2026-11-08, option 2): the smallest bonus of a combination family, plus every bigger
    /// bonus of that family that includes it. The rarity roll decides which of them is given; what was picked is always in it.
    /// </summary>
    public sealed class Subject
    {
        public string Key;
        public Exact Base;
        public Category Category;
        public readonly List<Exact> Members = new List<Exact>();
    }

    public sealed class Effect
    {
        public string FamilyId;
        public readonly List<Exact> Exacts = new List<Exact>();
        /// <summary>Subjects when this family combines bonuses (skill XP, loot, resistances, weapon bonuses); empty otherwise.</summary>
        public readonly List<Subject> Subjects = new List<Subject>();
        public int RowCount => Exacts.Sum(e => e.Rows.Count);
    }
    public sealed class Category
    {
        public string Id, NameKey;
        public EquipmentSlots Slot;
        public readonly List<Effect> Effects = new List<Effect>();
        public int RowCount => Effects.Sum(e => e.RowCount);
    }

    private sealed class Rule { internal string Family; internal Regex Match; internal Category Category; }

    private static bool built;
    private static readonly Dictionary<EquipmentSlots, List<Category>> bySlot = new Dictionary<EquipmentSlots, List<Category>>();
    private static readonly Dictionary<string, Category> categories = new Dictionary<string, Category>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Exact> exacts = new Dictionary<string, Exact>(StringComparer.Ordinal);
    private static readonly Dictionary<string, Category> categoryOfBonus = new Dictionary<string, Category>(StringComparer.Ordinal);
    private static readonly List<string> problems = new List<string>();
    private static readonly Dictionary<string, Subject> subjects = new Dictionary<string, Subject>(StringComparer.Ordinal);
    private static readonly HashSet<string> comboFamilies = new HashSet<string>(StringComparer.Ordinal);

    public static bool TryGetSubject(string key, out Subject subject) { EnsureBuilt(); return subjects.TryGetValue(key ?? string.Empty, out subject); }
    /// <summary>True when the family is picked by subject, not by exact combination.</summary>
    public static bool IsComboFamily(string familyId) { EnsureBuilt(); return familyId != null && comboFamilies.Contains(familyId); }
    private static readonly Regex RarityWord = new Regex("_(common|uncommon|rare|legendary)(?=_|$)", RegexOptions.Compiled);

    public static IReadOnlyList<string> Problems { get { EnsureBuilt(); return problems; } }

    /// <summary>The bonus id without its rarity word: head_skill_xp_common_cooking → head_skill_xp_cooking.</summary>
    public static string ExactKey(string bonusId) => RarityWord.Replace(bonusId ?? string.Empty, string.Empty);

    public static IReadOnlyList<Category> Categories(EquipmentSlots slot)
    {
        EnsureBuilt();
        return bySlot.TryGetValue(slot, out var list) ? (IReadOnlyList<Category>)list : new Category[0];
    }

    public static bool TryGetCategory(string id, out Category category) { EnsureBuilt(); return categories.TryGetValue(id ?? string.Empty, out category); }
    public static bool TryGetExact(string key, out Exact exact) { EnsureBuilt(); return exacts.TryGetValue(key ?? string.Empty, out exact); }
    public static Category CategoryOf(RebirthArmorBonusDefinition definition) { EnsureBuilt(); return definition != null && categoryOfBonus.TryGetValue(definition.Id, out var c) ? c : null; }

    /// <summary>The bonuses a focus allows on this slot (before the family rule). Any = every bonus of the slot.</summary>
    public static HashSet<string> Pool(EquipmentSlots slot, RebirthArmorBonusFocus focus)
    {
        EnsureBuilt();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        switch (focus.Depth)
        {
            case RebirthArmorBonusFocusDepth.Any:
                foreach (var c in Categories(slot)) foreach (var e in c.Effects) foreach (var x in e.Exacts) foreach (var r in x.Rows) ids.Add(r.Id);
                break;
            case RebirthArmorBonusFocusDepth.Category:
                if (categories.TryGetValue(focus.CategoryId, out var cat) && cat.Slot == slot)
                    foreach (var e in cat.Effects) foreach (var x in e.Exacts) foreach (var r in x.Rows) ids.Add(r.Id);
                break;
            case RebirthArmorBonusFocusDepth.Effect:
                if (categories.TryGetValue(focus.CategoryId, out var owner) && owner.Slot == slot)
                    foreach (var e in owner.Effects.Where(e => e.FamilyId == focus.FamilyId)) foreach (var x in e.Exacts) foreach (var r in x.Rows) ids.Add(r.Id);
                break;
            case RebirthArmorBonusFocusDepth.Exact:
                if (exacts.TryGetValue(focus.ExactKey, out var exact) && exact.Rows.Count > 0 && exact.Rows[0].Slot == slot)
                    foreach (var r in exact.Rows) ids.Add(r.Id);
                break;
            case RebirthArmorBonusFocusDepth.Subject:
                if (subjects.TryGetValue(focus.ExactKey, out var subject) && subject.Base.Rows.Count > 0 && subject.Base.Rows[0].Slot == slot)
                    foreach (var member in subject.Members) foreach (var r in member.Rows) ids.Add(r.Id);
                break;
        }
        return ids;
    }

    private static bool Includes(Exact big, Exact small) =>
        big.Tokens == null || (small.Tokens != null && small.Tokens.All(t => big.Tokens.Contains(t)));

    /// <summary>Combination families get subjects: their smallest bonuses, each with every bonus of the family that includes it.</summary>
    private static void BuildSubjects()
    {
        foreach (var family in exacts.Values.GroupBy(e => e.Rows[0].Slot + "/" + e.FamilyId))
        {
            var list = family.ToList();
            var tokened = list.Where(e => e.Tokens != null).ToList();
            if (tokened.Count < 2) continue;
            bool combo = list.Any(e => e.Tokens == null) || tokened.Any(big => tokened.Any(small => small != big && small.Tokens.Length < big.Tokens.Length && Includes(big, small)));
            if (!combo) continue;
            comboFamilies.Add(list[0].FamilyId);
            foreach (var baseExact in tokened.Where(e => !tokened.Any(small => small != e && small.Tokens.Length < e.Tokens.Length && Includes(e, small))))
            {
                var category = CategoryOf(baseExact.Rows[0]);
                var effect = category?.Effects.FirstOrDefault(x => x.FamilyId == baseExact.FamilyId);
                if (effect == null) continue;
                var subject = new Subject { Key = baseExact.Key, Base = baseExact, Category = category };
                subject.Members.AddRange(list.Where(e => e == baseExact || Includes(e, baseExact)));
                subjects[subject.Key] = subject;
                effect.Subjects.Add(subject);
            }
        }
    }

    private static void EnsureBuilt()
    {
        if (built) return;
        built = true;
        string path = Path.Combine(RebirthSurvivorDefinitionLoader.ResolveConfigRoot() ?? string.Empty, "..", "_ArmorBonuses", "bonus_categories.xml");
        var rules = new Dictionary<EquipmentSlots, List<Rule>>();
        try
        {
            foreach (var slotNode in XDocument.Load(path).Root.Elements("slot"))
            {
                var slot = (EquipmentSlots)Enum.Parse(typeof(EquipmentSlots), (string)slotNode.Attribute("id"));
                var list = new List<Rule>(); rules[slot] = list; bySlot[slot] = new List<Category>();
                foreach (var catNode in slotNode.Elements("category"))
                {
                    var category = new Category { Id = (string)catNode.Attribute("id"), NameKey = (string)catNode.Attribute("name_key"), Slot = slot };
                    categories[category.Id] = category; bySlot[slot].Add(category);
                    foreach (var ruleNode in catNode.Elements("rule"))
                    {
                        string match = (string)ruleNode.Attribute("match");
                        list.Add(new Rule { Family = (string)ruleNode.Attribute("family"), Match = string.IsNullOrEmpty(match) ? null : new Regex(match, RegexOptions.Compiled), Category = category });
                    }
                }
            }
        }
        catch (Exception e) { problems.Add("Could not load " + path + ": " + e.Message); Log.Error("[REBIRTH ArmorBonusTree] " + problems[0]); return; }

        foreach (var definition in RebirthArmorBonusCatalog.Definitions)
        {
            if (definition.Retired) continue; // never rolled or picked; items that have it keep it
            string key = ExactKey(definition.Id);
            Category category = null;
            if (rules.TryGetValue(definition.Slot, out var list))
                foreach (var rule in list)
                    if ((rule.Family == null || rule.Family == definition.FamilyId) && (rule.Match == null || rule.Match.IsMatch(key))) { category = rule.Category; break; }
            if (category == null) { problems.Add("Unassigned bonus " + definition.Id + " (" + definition.Slot + ")"); continue; }
            categoryOfBonus[definition.Id] = category;
            var effect = category.Effects.FirstOrDefault(e => e.FamilyId == definition.FamilyId);
            if (effect == null) { effect = new Effect { FamilyId = definition.FamilyId }; category.Effects.Add(effect); }
            if (!exacts.TryGetValue(key, out var exact))
            {
                exact = new Exact { Key = key, FamilyId = definition.FamilyId };
                exacts[key] = exact; effect.Exacts.Add(exact);
            }
            else if (exact.FamilyId != definition.FamilyId || CategoryOf(exact.Rows[0]) != category)
                problems.Add("Bonus " + definition.Id + " shares exact key " + key + " with another family or category");
            exact.Rows.Add(definition);
        }
        foreach (var exact in exacts.Values)
        {
            exact.Rarities = exact.Rows.Select(r => r.Rarity).Distinct().OrderBy(r => r).ToArray();
            exact.Tokens = exact.Rows[0].TokenNames;
        }
        BuildSubjects();
        foreach (var list in bySlot.Values)
            foreach (var empty in list.Where(c => c.Effects.Count == 0).ToList()) { problems.Add("Empty category " + empty.Id); list.Remove(empty); }
        int total = categoryOfBonus.Count;
        RebirthDebug.Log("armorbonuses", "[REBIRTH ArmorBonusTree] " + total + " of " + RebirthArmorBonusCatalog.Definitions.Count(d => !d.Retired) + " bonuses in " + categories.Count + " categories, "
            + exacts.Count + " exact bonuses" + (problems.Count > 0 ? ", " + problems.Count + " problem(s)" : ""));
        foreach (var problem in problems) Log.Warning("[REBIRTH ArmorBonusTree] " + problem);
    }
}

public enum RebirthArmorBonusFocusDepth { Any = 0, Category = 1, Effect = 2, Exact = 3, Subject = 4 }

/// <summary>What a new bonus is aimed at. Text form for the network: any | cat:ID | eff:CATEGORY/FAMILY | exact:KEY.</summary>
public struct RebirthArmorBonusFocus
{
    public RebirthArmorBonusFocusDepth Depth;
    public string CategoryId, FamilyId, ExactKey;

    public static RebirthArmorBonusFocus Any => default(RebirthArmorBonusFocus);
    public static RebirthArmorBonusFocus ForCategory(string id) => new RebirthArmorBonusFocus { Depth = RebirthArmorBonusFocusDepth.Category, CategoryId = id };
    public static RebirthArmorBonusFocus ForEffect(string category, string family) => new RebirthArmorBonusFocus { Depth = RebirthArmorBonusFocusDepth.Effect, CategoryId = category, FamilyId = family };
    public static RebirthArmorBonusFocus ForExact(string key) => new RebirthArmorBonusFocus { Depth = RebirthArmorBonusFocusDepth.Exact, ExactKey = key };
    /// <summary>A subject pick: ExactKey names the subject (its smallest bonus).</summary>
    public static RebirthArmorBonusFocus ForSubject(string key) => new RebirthArmorBonusFocus { Depth = RebirthArmorBonusFocusDepth.Subject, ExactKey = key };

    /// <summary>Tailoring needed for this depth (spec 3.1): category 10, effect and exact 25.</summary>
    public int RequiredTailoring => Depth == RebirthArmorBonusFocusDepth.Any ? 0 : Depth == RebirthArmorBonusFocusDepth.Category ? 10 : 25;

    public override string ToString()
    {
        switch (Depth)
        {
            case RebirthArmorBonusFocusDepth.Category: return "cat:" + CategoryId;
            case RebirthArmorBonusFocusDepth.Effect: return "eff:" + CategoryId + "/" + FamilyId;
            case RebirthArmorBonusFocusDepth.Exact: return "exact:" + ExactKey;
            case RebirthArmorBonusFocusDepth.Subject: return "subj:" + ExactKey;
            default: return "any";
        }
    }

    public static bool TryParse(string text, out RebirthArmorBonusFocus focus)
    {
        focus = Any;
        if (string.IsNullOrEmpty(text) || text == "any") return true;
        if (text.Length > 160) return false;
        if (text.StartsWith("cat:", StringComparison.Ordinal)) { focus = ForCategory(text.Substring(4)); return RebirthArmorBonusTree.TryGetCategory(focus.CategoryId, out _); }
        if (text.StartsWith("eff:", StringComparison.Ordinal))
        {
            var parts = text.Substring(4).Split('/');
            if (parts.Length != 2) return false;
            focus = ForEffect(parts[0], parts[1]);
            return RebirthArmorBonusTree.TryGetCategory(parts[0], out var c) && c.Effects.Any(e => e.FamilyId == parts[1]);
        }
        if (text.StartsWith("subj:", StringComparison.Ordinal)) { focus = ForSubject(text.Substring(5)); return RebirthArmorBonusTree.TryGetSubject(focus.ExactKey, out _); }
        if (text.StartsWith("exact:", StringComparison.Ordinal)) { focus = ForExact(text.Substring(6)); return RebirthArmorBonusTree.TryGetExact(focus.ExactKey, out _); }
        return false;
    }
}
