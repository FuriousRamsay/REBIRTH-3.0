using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Generates two Skill Aptitude choices for every non-Advanced Skill in the authoritative Skill registry.
/// Aptitudes are creation-only Traits: I = +5 for 1 point and II = +10 for 3 total
/// points. Level III was removed from Survivor creation in Revision 6. Only one
/// tier per Skill may be selected.
/// This avoids hand-authoring near-identical Trait records and guarantees that
/// the Aptitude catalogue stays aligned with the active Skill catalogue.
/// </summary>
public static class RebirthSkillAptitudeTraitFactory
{
    public const string Prefix = "trait.aptitude.";
    public const string DynamicModifierId = "dynamic.skill_aptitude";
    public const string NameKey = "xuiRebirthSurvivorSkillAptitudeName";
    public const string DescriptionKey = "xuiRebirthSurvivorSkillAptitudeDesc";
    public const string Category = "Skill Aptitude";
    public const int MaxTier = 2;

    private static readonly Dictionary<string, string[]> WeaknessBySkill = BuildWeaknessMap();

    public static void AppendGenerated(IList<RebirthTraitDefinition> target, IList<RebirthSkillDefinition> skills)
    {
        if (target == null || skills == null) return;
        HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < target.Count; i++)
            if (target[i] != null && !string.IsNullOrEmpty(target[i].Id)) existing.Add(target[i].Id);

        for (int i = 0; i < skills.Count; i++)
        {
            RebirthSkillDefinition skill = skills[i];
            if (skill == null || string.IsNullOrEmpty(skill.Id) || skill.Advanced) continue;
            for (int tier = 1; tier <= MaxTier; tier++)
            {
                string id = BuildId(skill.Id, tier);
                if (existing.Contains(id)) continue;
                List<string> conflicts = new List<string>();
                for (int other = 1; other <= MaxTier; other++)
                    if (other != tier) conflicts.Add(BuildId(skill.Id, other));
                string[] weaknesses;
                if (WeaknessBySkill.TryGetValue(skill.Id, out weaknesses) && weaknesses != null)
                    for (int w = 0; w < weaknesses.Length; w++) conflicts.Add(weaknesses[w]);

                target.Add(new RebirthTraitDefinition(
                    id,
                    NameKey,
                    DescriptionKey,
                    Category,
                    RebirthTraitPolarity.Positive,
                    CostForTier(tier),
                    RebirthDefinitionAvailability.Universal,
                    SkillIconKey(skill.Id),
                    DynamicModifierId,
                    new List<string>(),
                    conflicts,
                    new List<string>(),
                    "Natural aptitude starts " + skill.Id + " at +" + BonusForTier(tier) + ".",
                    "REBIRTH signed Skill starting state"));
                existing.Add(id);
            }
        }
    }

    public static bool IsAptitude(RebirthTraitDefinition trait)
    {
        return trait != null && IsAptitudeId(trait.Id);
    }

    public static bool IsAptitudeId(string id)
    {
        return !string.IsNullOrEmpty(id) && id.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParse(string id, out string skillId, out int tier, out int bonus, out int totalCost)
    {
        skillId = string.Empty; tier = 0; bonus = 0; totalCost = 0;
        if (!IsAptitudeId(id)) return false;
        string tail = id.Substring(Prefix.Length);
        int dot = tail.LastIndexOf('.');
        if (dot <= 0 || dot >= tail.Length - 1) return false;
        int parsedTier;
        if (!int.TryParse(tail.Substring(dot + 1), out parsedTier) || parsedTier < 1 || parsedTier > MaxTier) return false;
        string suffix = tail.Substring(0, dot);
        if (suffix.Length == 0) return false;
        skillId = "skill." + suffix;
        tier = parsedTier;
        bonus = BonusForTier(tier);
        totalCost = CostForTier(tier);
        return true;
    }

    public static string BuildId(string skillId, int tier)
    {
        string suffix = (skillId ?? string.Empty).Trim();
        if (suffix.StartsWith("skill.", StringComparison.OrdinalIgnoreCase)) suffix = suffix.Substring(6);
        return Prefix + suffix + "." + Math.Max(1, Math.Min(MaxTier, tier));
    }

    public static int BonusForTier(int tier)
    {
        return Math.Max(1, Math.Min(MaxTier, tier)) * 5;
    }

    public static int CostForTier(int tier)
    {
        switch (Math.Max(1, Math.Min(MaxTier, tier)))
        {
            case 1: return 1;
            case 2: return 3; // 1 + 2 cumulative design cost
            default: return 3; // Level II is the highest selectable Aptitude
        }
    }

    public static string SkillIconKey(string skillId)
    {
        string suffix = (skillId ?? string.Empty).Trim().ToLowerInvariant();
        if (suffix.StartsWith("skill.", StringComparison.Ordinal)) suffix = suffix.Substring(6);
        return "rb_skill_" + suffix.Replace('.', '_').Replace('-', '_');
    }

    private static Dictionary<string, string[]> BuildWeaknessMap()
    {
        Dictionary<string, List<string>> map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        AddWeakness(map, "trait.poor_shot", new string[] { "pistols", "revolvers", "heavy_handguns", "shotguns", "assault_rifles", "tactical_rifles", "long_range_rifles" });
        AddWeakness(map, "trait.awkward_fighter", new string[] { "spears", "clubs", "swords", "axes", "batons", "hammers", "knives", "scythes", "knuckles", "unarmed" });
        AddWeakness(map, "trait.technically_inept", new string[] { "mechanics", "maintenance", "construction", "electrical", "metalworking", "gunsmithing" });
        AddWeakness(map, "trait.poor_outdoorsman", new string[] { "tracking", "farming", "animal_processing" });
        AddWeakness(map, "trait.bad_cook", new string[] { "cooking" });
        AddWeakness(map, "trait.medically_inept", new string[] { "medicine" });
        AddWeakness(map, "trait.bad_trader", new string[] { "bartering" });
        AddWeakness(map, "trait.noisy_novice", new string[] { "stealth" });

        Dictionary<string, string[]> result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<string>> kv in map) result[kv.Key] = kv.Value.ToArray();
        // map currently keyed wrong way; rebuild skill -> weakness[] below.
        Dictionary<string, List<string>> bySkill = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string[]> kv in result)
        {
            string weakness = kv.Key;
            string[] skills = kv.Value;
            for (int i = 0; i < skills.Length; i++)
            {
                string skill = "skill." + skills[i];
                List<string> list;
                if (!bySkill.TryGetValue(skill, out list)) { list = new List<string>(); bySkill.Add(skill, list); }
                list.Add(weakness);
            }
        }
        Dictionary<string, string[]> final = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, List<string>> kv in bySkill) final[kv.Key] = kv.Value.ToArray();
        return final;
    }

    private static void AddWeakness(Dictionary<string, List<string>> map, string weaknessId, string[] skills)
    {
        List<string> list = new List<string>();
        if (skills != null) list.AddRange(skills);
        map[weaknessId] = list;
    }
}
