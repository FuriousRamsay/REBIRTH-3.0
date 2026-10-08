using System;

#nullable disable

/// <summary>
/// Phase-7 authoritative Stealth training. Stealth practice is credited only from actual
/// pre-hit-undetected weapon damage; movement/proximity never awards practical Stealth.
/// </summary>
public static class RebirthStealthTrainingService
{
    public static bool IsPreHitStealthQualified(EntityAlive target, DamageSource source)
    {
        if (target == null || target.world == null || target.world.IsRemote() || source == null || source.AttackingItem == null) return false;
        EntityPlayer attacker = target.world.GetEntity(source.getEntityId()) as EntityPlayer;
        if (attacker == null || attacker == target) return false;
        if (RebirthComplexSkillSystemService.IsRemoteOwnedDeviceSource(target.world, source)) return false;
        string underlying = RebirthProgressionRuntimeConfig.ClassifyCombat(source.AttackingItem);
        if (!RebirthWeaponFamilySkillService.IsChunk7Skill(underlying)) return false;

        // Capture awareness before damageEntityLocal mutates alert/revenge state. A target already
        // attacking/revenging this player is not undetected. Alerted awake targets are also rejected
        // conservatively; sleepers and genuinely unalerted targets can qualify.
        if (RebirthSkillEvalDiagnostics.On) Log.Out("[REBIRTH SkillEval] stealth check target=" + target.EntityName + " sleeping=" + target.IsSleeping + " waking=" + target.sleepingOrWakingUp + " alertTicks=" + target.GetAlertTicks() + " attackTarget=" + (target.GetAttackTarget() == attacker) + " revenge=" + (target.GetRevengeTarget() == attacker) + " skill=" + underlying);
        if (target.GetAttackTarget() == attacker || target.GetRevengeTarget() == attacker) return false;
        if (!target.IsSleeping && (target.sleepingOrWakingUp || target.GetAlertTicks() > 0)) return false;
        return true;
    }

    public static void OnQualifiedDamage(EntityPlayer player, EntityAlive target, ItemValue attackingItem,
        float actualHealthLoss, int preHitHealth, RebirthWorldCharacterRecord record)
    {
        if (player == null || target == null || attackingItem == null || record == null || record.Progression == null) return;
        float credited = Math.Max(0f, Math.Min(actualHealthLoss, Math.Max(0, preHitHealth)));
        if (credited <= 0f) return;
        RebirthSkillRuntimeState state;
        if (!record.Progression.Skills.TryGetValue(RebirthSurvivorIds.SkillStealth, out state) || state == null) return;
        RebirthWeaponSustainedDpsService.Profile profile;
        if (!RebirthWeaponSustainedDpsService.TryGetCombatProfile(player, attackingItem, out profile) || profile == null || !profile.Valid || profile.SustainedDps <= 0f) return;
        float gain = RebirthWeaponSustainedDpsService.CalculateCombatProgress(credited, profile.SustainedDps, state.Value + state.Progress);
        if (gain > 0f) RebirthWeaponSustainedDpsService.QueueCombatProgress(player, RebirthSurvivorIds.SkillStealth, gain);
    }
}
