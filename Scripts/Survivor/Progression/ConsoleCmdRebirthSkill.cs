using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class ConsoleCmdRebirthSkill : ConsoleCmdAbstract
{
    private struct SkillMark
    {
        public float Value;
        public float Progress;
    }

    private static readonly Dictionary<int, Dictionary<string, SkillMark>> Marks =
        new Dictionary<int, Dictionary<string, SkillMark>>();

    public override bool IsExecuteOnClient => true;
    public override bool AllowedInMainMenu => false;
    public override string[] getCommands() => new[] { "rbskill" };
    public override string getDescription() => "Inspect, snapshot, and diff REBIRTH practical skills for the active player.";
    public override string getHelp() =>
        "Usage:\n" +
        "  rbskill                 -> list current skills\n" +
        "  rbskill cooking         -> show one skill (aliases accepted)\n" +
        "  rbskill mark [skill]    -> save current value(s)\n" +
        "  rbskill diff [skill]    -> compare current value(s) to the last mark";

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        EntityPlayer player = LocalPlayerUI.GetUIForPrimaryPlayer()?.entityPlayer;
        if (player == null)
        {
            SdtdConsole.Instance.Output("[REBIRTH Skill] No local player is available.");
            return;
        }

        Dictionary<string, SkillMark> snapshot;
        if (!TryBuildSnapshot(player, out snapshot) || snapshot.Count == 0)
        {
            SdtdConsole.Instance.Output("[REBIRTH Skill] No REBIRTH skill snapshot is available yet.");
            return;
        }

        string mode = _params != null && _params.Count > 0 ? (_params[0] ?? string.Empty).Trim().ToLowerInvariant() : string.Empty;
        if (mode == "mark")
        {
            string filter = _params != null && _params.Count > 1 ? NormalizeSkillId(_params[1]) : string.Empty;
            SaveMark(player.entityId, snapshot, filter);
            return;
        }

        if (mode == "diff")
        {
            string filter = _params != null && _params.Count > 1 ? NormalizeSkillId(_params[1]) : string.Empty;
            OutputDiff(player.entityId, snapshot, filter);
            return;
        }

        string requested = NormalizeSkillId(mode);
        OutputSnapshot(snapshot, requested);
    }

    private static void SaveMark(int entityId, Dictionary<string, SkillMark> snapshot, string filter)
    {
        Dictionary<string, SkillMark> copy = new Dictionary<string, SkillMark>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, SkillMark> pair in snapshot)
        {
            if (!MatchesFilter(pair.Key, filter)) continue;
            copy[pair.Key] = pair.Value;
        }
        Marks[entityId] = copy;
        SdtdConsole.Instance.Output("[REBIRTH Skill] Saved mark for " + copy.Count.ToString(CultureInfo.InvariantCulture) + " skill(s). Use 'rbskill diff' after testing.");
    }

    private static void OutputDiff(int entityId, Dictionary<string, SkillMark> current, string filter)
    {
        Dictionary<string, SkillMark> marked;
        if (!Marks.TryGetValue(entityId, out marked) || marked == null || marked.Count == 0)
        {
            SdtdConsole.Instance.Output("[REBIRTH Skill] No mark is stored. Use 'rbskill mark' first.");
            return;
        }

        List<string> keys = current.Keys.Where(key => MatchesFilter(key, filter)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        if (keys.Count == 0)
        {
            SdtdConsole.Instance.Output("[REBIRTH Skill] No skills matched the requested filter.");
            return;
        }

        foreach (string key in keys)
        {
            SkillMark now = current[key];
            SkillMark then;
            if (!marked.TryGetValue(key, out then))
            {
                SdtdConsole.Instance.Output("[REBIRTH Skill] " + DisplayName(key) + ": no saved mark.");
                continue;
            }

            float valueDelta = now.Value - then.Value;
            float progressDelta = now.Progress - then.Progress;
            SdtdConsole.Instance.Output("[REBIRTH Skill] " + DisplayName(key) +
                " now=" + Format(now) +
                " was=" + Format(then) +
                " delta=" + valueDelta.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) +
                " progress=" + progressDelta.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture));
        }
    }

    private static void OutputSnapshot(Dictionary<string, SkillMark> snapshot, string filter)
    {
        List<string> keys = snapshot.Keys.Where(key => MatchesFilter(key, filter)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        if (keys.Count == 0)
        {
            SdtdConsole.Instance.Output("[REBIRTH Skill] No skills matched the requested filter.");
            return;
        }

        foreach (string key in keys)
            SdtdConsole.Instance.Output("[REBIRTH Skill] " + DisplayName(key) + " = " + Format(snapshot[key]));
    }

    private static string Format(SkillMark mark)
    {
        return mark.Value.ToString("0.0000", CultureInfo.InvariantCulture) + " (progress " + mark.Progress.ToString("0.0000", CultureInfo.InvariantCulture) + ")";
    }

    private static bool TryBuildSnapshot(EntityPlayer player, out Dictionary<string, SkillMark> snapshot)
    {
        snapshot = new Dictionary<string, SkillMark>(StringComparer.OrdinalIgnoreCase);
        if (player == null)
            return false;

        World world = player.world;
        if (world != null && world.IsRemote())
        {
            RebirthSurvivorOwnerStateSnapshot owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if (owner == null) return false;
            for (int i = 0; i < owner.Skills.Count; i++)
            {
                RebirthSurvivorOwnerSkillSnapshot skill = owner.Skills[i];
                if (skill == null || string.IsNullOrEmpty(skill.Id)) continue;
                snapshot[skill.Id] = new SkillMark { Value = skill.Value, Progress = skill.Progress };
            }
            return snapshot.Count > 0;
        }

        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterService.TryGet(player, out record) || record?.Progression == null)
            return false;
        foreach (KeyValuePair<string, RebirthSkillRuntimeState> pair in record.Progression.Skills)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key)) continue;
            snapshot[pair.Key] = new SkillMark { Value = pair.Value.Value, Progress = pair.Value.Progress };
        }
        return snapshot.Count > 0;
    }

    private static string NormalizeSkillId(string raw)
    {
        string value = (raw ?? string.Empty).Trim();
        if (value.Length == 0 || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (value.StartsWith("skill.", StringComparison.OrdinalIgnoreCase))
            return value;

        switch (value.ToLowerInvariant())
        {
            case "cooking": return "skill.cooking";
            case "drink":
            case "drinks":
            case "drink_preparation": return "skill.drink_preparation";
            case "medicine": return "skill.medicine";
            case "chemistry": return "skill.chemistry";
            case "mechanics": return "skill.mechanics";
            case "maintenance": return "skill.maintenance";
            case "gunsmithing": return "skill.gunsmithing";
            case "metalworking": return "skill.metalworking";
            case "tailoring": return "skill.tailoring";
            case "construction": return "skill.construction";
            case "electrical": return "skill.electrical";
            default: return "skill." + value.ToLowerInvariant();
        }
    }

    private static bool MatchesFilter(string skillId, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return true;
        return string.Equals(skillId ?? string.Empty, filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string DisplayName(string skillId)
    {
        RebirthSkillDefinition definition;
        if (RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId, out definition) && definition != null && !string.IsNullOrEmpty(definition.NameKey))
        {
            string localized = Localization.Get(definition.NameKey);
            if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, definition.NameKey, StringComparison.Ordinal))
                return localized + " [" + skillId + "]";
        }
        return skillId ?? string.Empty;
    }
}
