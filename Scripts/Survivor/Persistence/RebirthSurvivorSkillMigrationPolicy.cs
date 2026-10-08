using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// One explicit compatibility row for the pre-Revision-4 16-Skill model.
/// No migration target is inferred from display names, prefixes, ordering, or neighboring IDs.
/// </summary>
public sealed class RebirthSurvivorLegacySkillMapping
{
    public string SourceId { get; private set; }
    public ReadOnlyCollection<string> DestinationIds { get; private set; }

    public RebirthSurvivorLegacySkillMapping(string sourceId, params string[] destinationIds)
    {
        SourceId = (sourceId ?? string.Empty).Trim();
        DestinationIds = new ReadOnlyCollection<string>(new List<string>(destinationIds ?? new string[0]));
    }
}

/// <summary>
/// Read-only audit of one origin/progression Skill-node translation.
/// It exists so debug/test surfaces can prove what was preserved, copied, explicitly overridden,
/// or initialized neutral without storing mutable migration scratch state in gameplay models.
/// </summary>
public sealed class RebirthSurvivorSkillMigrationAudit
{
    public string PolicyId { get; internal set; }
    public bool HasProgress { get; internal set; }
    public int InputCount { get; internal set; }
    public int OutputCount { get; internal set; }
    public int LegacySourcesPresent { get; internal set; }
    public int ExplicitCurrentInputs { get; internal set; }
    public int NeutralInitializations { get; internal set; }
    public readonly List<string> AppliedMappings = new List<string>();
    public readonly List<string> ExplicitDestinationOverrides = new List<string>();

    public string BuildSummary()
    {
        return "policy=" + (PolicyId ?? string.Empty)
            + " input=" + InputCount.ToString(CultureInfo.InvariantCulture)
            + " output=" + OutputCount.ToString(CultureInfo.InvariantCulture)
            + " legacySources=" + LegacySourcesPresent.ToString(CultureInfo.InvariantCulture)
            + " explicitCurrent=" + ExplicitCurrentInputs.ToString(CultureInfo.InvariantCulture)
            + " neutral=" + NeutralInitializations.ToString(CultureInfo.InvariantCulture)
            + " overrides=" + ExplicitDestinationOverrides.Count.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Deterministic schema-4 -> schema-5 Skill compatibility policy.
///
/// Implementation status: NEW REBIRTH infrastructure, implemented by Post-Revision-2 Chunk 2.
/// The policy is intentionally pure with respect to gameplay state: it consumes one XML Skills
/// node and returns a replacement node only after the complete input has validated.
/// </summary>
public static class RebirthSurvivorSkillMigrationPolicy
{
    public const string PolicyId = "survivor-schema4-to5-revision4-v1";
    public const float RuntimeSkillMin = -50f;
    public const float RuntimeSkillMax = 100f;

    private sealed class SkillValue
    {
        public float Value;
        public float Progress;
        public bool ExplicitCurrent;
    }

    private static readonly string[] CurrentIds = new string[] {
        "skill.spears","skill.clubs","skill.swords","skill.axes","skill.batons","skill.hammers","skill.knives","skill.scythes","skill.knuckles","skill.unarmed",
        "skill.archery","skill.pistols","skill.revolvers","skill.heavy_handguns","skill.shotguns","skill.assault_rifles","skill.tactical_rifles","skill.long_range_rifles","skill.explosives","skill.deployable_turrets","skill.drone_operations",
        "skill.mining","skill.logging","skill.salvage","skill.farming","skill.animal_processing","skill.tracking","skill.animal_handling","skill.black_magic","skill.rage","skill.mechanics","skill.medicine","skill.cooking","skill.drink_preparation","skill.maintenance","skill.construction","skill.electrical","skill.metalworking","skill.gunsmithing","skill.chemistry","skill.lockpicking","skill.stealth","skill.athletics","skill.armor_proficiency","skill.bartering","skill.trading","skill.teaching","skill.tailoring"
    };

    // All 16 legacy Skills are listed explicitly, including the 11 one-to-one mappings.
    // Axes are intentionally not a child of legacy Bladed Melee because the old classifier did
    // not route axes through that Skill. Unrelated new Skills therefore initialize to neutral 0.
    private static readonly RebirthSurvivorLegacySkillMapping[] LegacyMap = new RebirthSurvivorLegacySkillMapping[] {
        new RebirthSurvivorLegacySkillMapping("skill.bladed_melee", "skill.swords", "skill.knives", "skill.scythes"),
        new RebirthSurvivorLegacySkillMapping("skill.blunt_melee", "skill.clubs", "skill.batons", "skill.hammers"),
        new RebirthSurvivorLegacySkillMapping("skill.spears", "skill.spears"),
        new RebirthSurvivorLegacySkillMapping("skill.unarmed", "skill.unarmed", "skill.knuckles"),
        new RebirthSurvivorLegacySkillMapping("skill.archery", "skill.archery"),
        new RebirthSurvivorLegacySkillMapping("skill.handguns", "skill.pistols", "skill.revolvers", "skill.heavy_handguns"),
        new RebirthSurvivorLegacySkillMapping("skill.rifles", "skill.assault_rifles", "skill.tactical_rifles", "skill.long_range_rifles"),
        new RebirthSurvivorLegacySkillMapping("skill.shotguns", "skill.shotguns"),
        new RebirthSurvivorLegacySkillMapping("skill.mining", "skill.mining"),
        new RebirthSurvivorLegacySkillMapping("skill.logging", "skill.logging"),
        new RebirthSurvivorLegacySkillMapping("skill.salvage", "skill.salvage"),
        new RebirthSurvivorLegacySkillMapping("skill.farming", "skill.farming"),
        new RebirthSurvivorLegacySkillMapping("skill.mechanics", "skill.mechanics"),
        new RebirthSurvivorLegacySkillMapping("skill.medicine", "skill.medicine"),
        new RebirthSurvivorLegacySkillMapping("skill.cooking", "skill.cooking"),
        new RebirthSurvivorLegacySkillMapping("skill.maintenance", "skill.maintenance")
    };

    private static readonly HashSet<string> CurrentIdSet = new HashSet<string>(CurrentIds, StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RebirthSurvivorLegacySkillMapping> LegacyById = BuildLegacyById();

    public static string[] GetCurrentSkillIds()
    {
        return (string[])CurrentIds.Clone();
    }

    public static RebirthSurvivorLegacySkillMapping[] GetLegacyMappings()
    {
        RebirthSurvivorLegacySkillMapping[] copy = new RebirthSurvivorLegacySkillMapping[LegacyMap.Length];
        Array.Copy(LegacyMap, copy, LegacyMap.Length);
        return copy;
    }

    public static bool IsCurrentSkillId(string id)
    {
        return !string.IsNullOrEmpty(id) && CurrentIdSet.Contains(id.Trim());
    }

    public static bool IsKnownLegacySkillId(string id)
    {
        return !string.IsNullOrEmpty(id) && LegacyById.ContainsKey(id.Trim());
    }

    /// <summary>
    /// Validates and builds a complete schema-5 Skills node without mutating the source node.
    /// Duplicate IDs, unknown IDs, non-finite values, and values outside the signed runtime range
    /// fail explicitly. Missing legacy entries are valid and lead only to neutral destinations.
    /// </summary>
    public static bool TryBuildMigratedSkills(
        XElement sourceSkills,
        bool hasProgress,
        out XElement migratedSkills,
        out RebirthSurvivorSkillMigrationAudit audit,
        out string error)
    {
        migratedSkills = null;
        error = string.Empty;
        audit = new RebirthSurvivorSkillMigrationAudit { PolicyId = PolicyId, HasProgress = hasProgress };
        if (sourceSkills == null || sourceSkills.Name != "skills")
        {
            error = "Skills node is missing or has an unexpected element name";
            return false;
        }

        Dictionary<string, SkillValue> input = new Dictionary<string, SkillValue>(StringComparer.OrdinalIgnoreCase);
        List<string> unknown = new List<string>();
        List<string> duplicate = new List<string>();
        foreach (XElement e in sourceSkills.Elements("skill"))
        {
            string id = ((string)e.Attribute("id") ?? string.Empty).Trim();
            if (id.Length == 0)
            {
                error = "Skill entry has a blank id";
                return false;
            }
            if (input.ContainsKey(id))
            {
                duplicate.Add(id);
                continue;
            }
            if (!CurrentIdSet.Contains(id) && !LegacyById.ContainsKey(id))
                unknown.Add(id);

            float value;
            string valueText = (string)e.Attribute("value");
            if (!TryFiniteFloat(valueText, out value))
            {
                error = "Skill '" + id + "' has a missing/invalid value '" + (valueText ?? string.Empty) + "'";
                return false;
            }
            if (value < RuntimeSkillMin || value > RuntimeSkillMax)
            {
                error = "Skill '" + id + "' value " + F(value) + " is outside signed runtime bounds [" + F(RuntimeSkillMin) + "," + F(RuntimeSkillMax) + "]";
                return false;
            }

            float progress = 0f;
            if (hasProgress)
            {
                string progressText = (string)e.Attribute("progress");
                if (!string.IsNullOrEmpty(progressText) && !TryFiniteFloat(progressText, out progress))
                {
                    error = "Skill '" + id + "' has invalid progress '" + progressText + "'";
                    return false;
                }
                if (progress < 0f || progress >= 1f)
                {
                    error = "Skill '" + id + "' progress " + F(progress) + " is outside [0,1)";
                    return false;
                }
            }

            input[id] = new SkillValue { Value = value, Progress = progress, ExplicitCurrent = CurrentIdSet.Contains(id) };
        }
        audit.InputCount = input.Count;

        if (duplicate.Count > 0)
        {
            duplicate.Sort(StringComparer.OrdinalIgnoreCase);
            error = "duplicate Skill ids: " + string.Join(",", duplicate.ToArray());
            return false;
        }
        if (unknown.Count > 0)
        {
            unknown.Sort(StringComparer.OrdinalIgnoreCase);
            error = "unknown Skill ids cannot be migrated without an explicit compatibility rule: " + string.Join(",", unknown.ToArray());
            return false;
        }

        Dictionary<string, SkillValue> output = new Dictionary<string, SkillValue>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, SkillValue> pair in input)
        {
            if (!CurrentIdSet.Contains(pair.Key))
                continue;
            output[pair.Key] = new SkillValue { Value = pair.Value.Value, Progress = pair.Value.Progress, ExplicitCurrent = true };
            audit.ExplicitCurrentInputs++;
        }

        for (int i = 0; i < LegacyMap.Length; i++)
        {
            RebirthSurvivorLegacySkillMapping map = LegacyMap[i];
            SkillValue legacyValue;
            if (!input.TryGetValue(map.SourceId, out legacyValue))
                continue;
            audit.LegacySourcesPresent++;

            for (int j = 0; j < map.DestinationIds.Count; j++)
            {
                string destination = map.DestinationIds[j];
                SkillValue existing;
                if (output.TryGetValue(destination, out existing))
                {
                    // A more-specific/current destination is authoritative when an intermediate
                    // save contains both old broad and new exact IDs. This is explicit and audited,
                    // never an order-dependent dictionary overwrite.
                    if (!string.Equals(destination, map.SourceId, StringComparison.OrdinalIgnoreCase))
                        audit.ExplicitDestinationOverrides.Add(map.SourceId + "->" + destination);
                    continue;
                }
                output[destination] = new SkillValue { Value = legacyValue.Value, Progress = legacyValue.Progress, ExplicitCurrent = false };
                audit.AppliedMappings.Add(map.SourceId + "->" + destination);
            }
        }

        migratedSkills = new XElement("skills");
        for (int i = 0; i < CurrentIds.Length; i++)
        {
            string id = CurrentIds[i];
            SkillValue v;
            if (!output.TryGetValue(id, out v))
            {
                v = new SkillValue { Value = 0f, Progress = 0f, ExplicitCurrent = false };
                audit.NeutralInitializations++;
            }
            XElement e = new XElement("skill",
                new XAttribute("id", id),
                new XAttribute("value", F(v.Value)));
            if (hasProgress)
                e.Add(new XAttribute("progress", F(v.Progress)));
            migratedSkills.Add(e);
        }
        audit.OutputCount = CurrentIds.Length;
        return true;
    }

    private static Dictionary<string, RebirthSurvivorLegacySkillMapping> BuildLegacyById()
    {
        Dictionary<string, RebirthSurvivorLegacySkillMapping> result = new Dictionary<string, RebirthSurvivorLegacySkillMapping>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < LegacyMap.Length; i++)
        {
            RebirthSurvivorLegacySkillMapping row = LegacyMap[i];
            if (row == null || string.IsNullOrEmpty(row.SourceId))
                throw new InvalidOperationException("Legacy Skill migration row is invalid.");
            if (result.ContainsKey(row.SourceId))
                throw new InvalidOperationException("Duplicate legacy Skill migration source '" + row.SourceId + "'.");
            if (row.DestinationIds == null || row.DestinationIds.Count == 0)
                throw new InvalidOperationException("Legacy Skill migration source '" + row.SourceId + "' has no destination.");
            for (int j = 0; j < row.DestinationIds.Count; j++)
                if (!CurrentIdSet.Contains(row.DestinationIds[j]))
                    throw new InvalidOperationException("Legacy Skill migration destination '" + row.DestinationIds[j] + "' is not a current Skill.");
            result[row.SourceId] = row;
        }
        return result;
    }

    private static bool TryFiniteFloat(string text, out float value)
    {
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static string F(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
