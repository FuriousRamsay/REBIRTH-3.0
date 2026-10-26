using System;

#nullable disable

// G02: one 1..20 competence mapping for every NPC consumer of a shared (player) skill, the held-weapon -> real skill
// classification, and the work-to-skill table for Looting/Gathering.
// Provisional decisions: see _Documentation/NPC/Progression/PROVISIONAL_DECISIONS_G02_20261022.md.

// Shared skill value 0..Max -> competence level 1..20: the documented P1 mapping 1 + round(Value/Max*19).
// ToSkillValue is the D-01 migration formula value = Max * ((Level - 1 + fraction) / (MaxLevel - 1)); under the rounding
// rule a migrated level L reads back as L when fraction < 0.5 (at 0.5 Math.Round goes to even; above it reads L+1).
internal static class RebirthG02SkillScale
{
    internal const int MinLevel = 1;
    internal const int MaxLevel = 20;

    // 1.0 .. 20.0
    internal static float ToContinuousLevel(float value, float max)
    {
        if(!(max > 0f) || float.IsNaN(value)) return MinLevel;
        float ratio = value / max;
        ratio = ratio < 0f ? 0f : ratio > 1f ? 1f : ratio;
        return MinLevel + ratio * (MaxLevel - MinLevel);
    }

    // 1..20, identical to the pre-G02 formula 1 + (int)Math.Round(clamp01(Value/Max) * 19).
    internal static int ToLevel(float value, float max)
    {
        int level = 1 + (int)Math.Round(ToContinuousLevel(value, max) - MinLevel);
        return level < MinLevel ? MinLevel : level > MaxLevel ? MaxLevel : level;
    }

    // The migration direction: legacy level (1..MaxLevel) plus the fraction toward the next level (0..1) -> skill value.
    internal static float ToSkillValue(float level, float fraction, float max)
    {
        float continuous = level - 1f + (fraction < 0f ? 0f : fraction > 1f ? 1f : fraction);
        float span = MaxLevel - MinLevel;
        float ratio = continuous / span;
        ratio = ratio < 0f ? 0f : ratio > 1f ? 1f : ratio;
        return max * ratio;
    }
}

// Held weapon -> real weapon skill. Held-item classification is NOT redefined: RebirthProgressionRuntimeConfig.ClassifyCombat
// (skill_sources.xml item names and tag tokens) is the single mapping, used by the player path, RebirthHumanWeaponPassives
// and RebirthHumanCombatPractice. The broad legacy tracks (Blades, Rifles, Automatic, Heavy) stay unmapped (lead decision D-01).
internal static class RebirthG02WeaponFamilies
{
    // Player classification, plus the bare-hand item (the player's "meleeHandPlayer" is authored, an NPC fist item is
    // named meleeHand*). Empty when the item is no Chunk 7 weapon.
    internal static string Classify(ItemValue held)
    {
        if(held == null || held.ItemClass == null) return string.Empty;
        string skillId = RebirthProgressionRuntimeConfig.ClassifyCombat(held);
        if(skillId.Length > 0) return skillId;
        string name = held.ItemClass.GetItemName() ?? string.Empty;
        return name.StartsWith("meleeHand", StringComparison.OrdinalIgnoreCase) ? RebirthSurvivorIds.SkillUnarmed : string.Empty;
    }
}

// Which real player skill an NPC work profession uses for its effect/award.
// There is no player Looting or Gathering skill (the 50-skill list in progression.xml has none). Provisional:
//   Looting   -> none (opening/lockpicking uses skill.lockpicking in the P11 path; a plain container search has no skill)
//   Gathering -> skill.farming (plant and crop pickup is exactly what the player Farming harvest passive scales)
// No executor for either exists yet (only the settlement simulation, which uses the legacy tracks), so nothing calls this
// today; the first Gathering executor must use it instead of inventing a track.
internal static class RebirthG02WorkSkills
{
    internal static bool TryGetEffectSkill(RebirthNpcProfession profession, out string skillId)
    {
        if(RebirthNpcProfessionProgressionService.TryGetSharedSkill(profession, out skillId)) return true;
        if(profession == RebirthNpcProfession.Gathering) { skillId = RebirthSurvivorIds.SkillFarming; return true; }
        skillId = null;
        return false;
    }
}
