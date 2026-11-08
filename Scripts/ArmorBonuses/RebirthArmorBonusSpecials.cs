using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>2.6 armor bonuses with no native stat of their own.</summary>
internal static class RebirthArmorBonusSpecials
{
    internal const float LowLightLevel = 65f;
    private static readonly FastTags<TagGroup.Global> Running = FastTags<TagGroup.Global>.Parse("running");

    internal static float Best(EntityAlive entity, EquipmentSlots slot, string special)
    {
        if (entity?.equipment == null) return 0f;
        float best = 0f;
        foreach (var bonus in RebirthArmorBonusStorage.Read(entity.equipment.GetSlotItem((int)slot)))
            if (bonus.Definition.Special == special && bonus.Value > best) best = bonus.Value;
        return best;
    }

    // XP boosts: the best worn head bonus for this skill, and any worn mod that raises all skill gain.
    internal static float SkillGainFactor(EntityPlayer player, string skillId)
    {
        if (player?.equipment == null || string.IsNullOrEmpty(skillId)) return 1f;
        float bonus = 0f;
        foreach (var rolled in RebirthArmorBonusStorage.Read(player.equipment.GetSlotItem((int)EquipmentSlots.Head)))
            if (rolled.Definition.Special == "skill_xp" && rolled.Definition.Skills != null && rolled.Definition.Skills.Contains(skillId) && rolled.Value > bonus) bonus = rolled.Value;
        return (1f + bonus) * (1f + BestWornMod(player, "RebirthSkillGain"));
    }

    internal static float AttributeGainFactor(EntityPlayer player)
    {
        return 1f + BestWornMod(player, "RebirthAttributeGain");
    }

    private static float BestWornMod(EntityPlayer player, string property)
    {
        if (player?.equipment == null) return 0f;
        float best = 0f;
        for (int i = 0; i < player.equipment.GetSlotCount(); i++)
        {
            ItemValue worn = player.equipment.GetSlotItem(i);
            if (worn == null || worn.IsEmpty() || worn.modifications == null) continue;
            foreach (var mod in worn.modifications)
            {
                var properties = mod?.ItemClass?.Properties;
                if (properties == null || !properties.Contains(property)) continue;
                best = Mathf.Max(best, properties.GetFloat(property));
            }
        }
        return best;
    }

    // Applied to movement drain only, so resting recovery stays as it is.
    internal static float StaminaDrainFactor(EntityAlive entity)
    {
        if (entity.IsCrouching && entity.CurrentMovementTag.Test_AnySet(Running) && Best(entity, EquipmentSlots.Feet, "sneak_sprint") > 0f) return 0f;
        return Mathf.Clamp01(1f - Best(entity, EquipmentSlots.Feet, "movement_stamina"));
    }

    internal static bool IsSneakHit(EntityAlive target, EntityPlayer attacker)
    {
        return attacker.IsCrouching && (target.IsSleeping || target.GetCVar("_notAlerted") >= 0.5f);
    }
}

/// <summary>Sneak damage and sleeper knockdown from boots. The server decides, as in 2.6.</summary>
[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.DamageEntity), new[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) })]
internal static class RebirthArmorBonusSleeperPatch
{
    private static void Prefix(EntityAlive __instance, DamageSource _damageSource, ref int _strength, ref EntityPlayer __state)
    {
        __state = null;
        if (__instance.world == null || __instance.world.IsRemote() || _strength <= 0 || _damageSource == null || __instance is EntityPlayer) return;
        var attacker = __instance.world.GetEntity(_damageSource.getEntityId()) as EntityPlayer;
        if (attacker == null || !RebirthArmorBonusSpecials.IsSneakHit(__instance, attacker)) return;

        float bonus = RebirthArmorBonusSpecials.Best(attacker, EquipmentSlots.Feet, "sneak_damage");
        float multiplier = _damageSource.DamageMultiplier;
        if (bonus > 0f && multiplier > 0f)
            _strength = Mathf.RoundToInt(_strength * (multiplier + bonus) / multiplier);

        float chance = RebirthArmorBonusSpecials.Best(attacker, EquipmentSlots.Feet, "sleeper_knockdown");
        if (chance > 0f && UnityEngine.Random.value <= chance) __state = attacker;
    }

    private static void Postfix(EntityAlive __instance, int __result, EntityPlayer __state)
    {
        if (__state != null && __result > 0 && !__instance.IsDead())
            __instance.Buffs.AddBuff("buffInjuryKnockdown01", __state.entityId);
    }
}
