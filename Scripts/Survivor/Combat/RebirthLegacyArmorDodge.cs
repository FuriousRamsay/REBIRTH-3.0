using System;

// One removed 2.6 perkDodge rank avoided five percent of incoming hits.
// Its two armor mods now own that value through native equip-update CVars.
// This runs in the existing server-authoritative damage response dispatcher;
// it does not recalculate or cache a second copy of the equipment loadout.
internal static class RebirthLegacyArmorDodge
{
    private const string ChanceCVar = "$RebirthArmorDodgeChance";
    private const string GeneticsChanceCVar = "$RebirthArmorGeneticsDodgeChance";

    internal static bool TryAvoid(EntityAlive target, ref DamageResponse response)
    {
        var world = target?.world;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) || response.Strength <= 0 ||
            target.IsDead() || !(target is EntityPlayer) && !(target is EntityRebirthNPC)) return false;

        // ModGenetics carries the same one removed perkDodge rank, but its
        // source CVar requirements disable it while an attribute mod is worn.
        float geneticsChance = target.Buffs?.GetCustomVar(GeneticsChanceCVar) ?? 0f;
        if (geneticsChance > 0f && (target.Buffs.GetCustomVar("$ModStrength") >= 1f ||
            target.Buffs.GetCustomVar("$ModDexterity") >= 1f ||
            target.Buffs.GetCustomVar("$ModConstitution") >= 1f ||
            target.Buffs.GetCustomVar("$ModIntelligence") >= 1f ||
            target.Buffs.GetCustomVar("$ModCharisma") >= 1f)) geneticsChance = 0f;
        float chance = Math.Max(target.Buffs?.GetCustomVar(ChanceCVar) ?? 0f, geneticsChance);
        if (float.IsNaN(chance) || chance <= 0f) return false;
        chance = Math.Min(0.05f, chance);

        DamageSource source = response.Source;
        if (source == null) return false;
        var attacker = world.GetEntity(source.getEntityId()) as EntityAlive;
        if (attacker == null || attacker.IsDead() || ReferenceEquals(attacker, target)) return false;

        // The source hit hooks covered weapon attacks. Do not dodge ambient,
        // falling, fire, or poison damage simply because armor is installed.
        var actions = source.AttackingItem?.ItemClass?.Actions;
        if (actions == null || actions.Length == 0) return false;
        bool melee = actions[0] is ItemActionMelee || actions[0] is ItemActionDynamicMelee;
        bool npcRanged = attacker is EntityRebirthNPC && actions[0] is ItemActionRanged;
        if (!melee && !npcRanged) return false;
        if (world.GetGameRandom().RandomFloat >= chance) return false;

        response.Strength = 0;
        response.ArmorDamage = 0;
        response.Fatal = false;
        response.Dismember = false;
        response.PainHit = false;
        response.Critical = false;
        return true;
    }
}
