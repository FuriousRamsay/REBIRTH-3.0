using System;

#nullable disable

public static class RebirthSkillEventRouter
{
    public static void OnCombatDamageCompleted(EntityAlive target,DamageResponse response)
    {
        float fallbackDamage=Math.Max(0f,response.Strength);
        OnCombatDamageCompleted(target,response,fallbackDamage,false,target!=null?target.Health:0);
    }

    public static void OnCombatDamageCompleted(EntityAlive target,DamageResponse response,float actualHealthLoss)
    { OnCombatDamageCompleted(target,response,actualHealthLoss,false,target!=null?target.Health:0); }

    public static void OnCombatDamageCompleted(EntityAlive target,DamageResponse response,float actualHealthLoss,bool preHitStealthQualified,int preHitHealth)
    {
        if(target==null || target.world==null || target.world.IsRemote() || response.Source==null)return;
        int attackerId=response.Source.getEntityId();
        float creditedDamage=Math.Max(0f,Math.Min(actualHealthLoss,Math.Max(0,preHitHealth)));
        RebirthBlackMagicService.OnDominatedCombatDamage(target,response,attackerId,creditedDamage);
        RebirthHumanCombatPractice.OnDamageCommitted(target,response,creditedDamage,preHitHealth);
        EntityPlayer player=target.world.GetEntity(attackerId) as EntityPlayer;
        if(player==null || player==target)return;
        // Native deployable turret/drone gun fire deliberately identifies the owning player as the
        // attacker and stores the actual deployed entity in DamageSource.CreatorEntityId. Do not let
        // remote device damage masquerade as the player's own armored combat.
        if (!RebirthComplexSkillSystemService.IsRemoteOwnedDeviceSource(target.world, response.Source))
            RebirthSkillWaveAService.OnArmoredCombat(player);
        if(response.Source.AttackingItem==null)return;
        string skillId=RebirthProgressionRuntimeConfig.ClassifyCombat(response.Source.AttackingItem);
        if(skillId.Length==0)return;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record))return;

        // Credit only health actually removed. This prevents overkill and nominal weapon damage from
        // inflating training. Held weapon families and supported remote-device combat are normalized
        // against the live/source device sustained work rate; unresolved profiles fail closed.
        float amount=0f;
        RebirthWeaponSustainedDpsService.Profile profile=null;
        RebirthSkillRuntimeState state;
        bool normalizedCombat = RebirthWeaponFamilySkillService.IsChunk7Skill(skillId) ||
            string.Equals(skillId,"skill.explosives",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skillId,"skill.deployable_turrets",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(skillId,"skill.drone_operations",StringComparison.OrdinalIgnoreCase);
        if(normalizedCombat && creditedDamage>0f && record.Progression.Skills.TryGetValue(skillId,out state) && state!=null &&
            RebirthWeaponSustainedDpsService.TryGetCombatProfile(player,response.Source.AttackingItem,out profile) && profile!=null && profile.Valid && profile.SustainedDps>0f)
            amount=RebirthWeaponSustainedDpsService.CalculateCombatProgress(creditedDamage,profile.SustainedDps,state.Value+state.Progress);
        if (RebirthSkillEvalDiagnostics.On)
            Log.Out("[REBIRTH SkillEval] combat event skill=" + skillId + " item=" + response.Source.AttackingItem.ItemClass.GetItemName() +
                " target=" + target.entityId + " preHealth=" + preHitHealth + " healthLoss=" + actualHealthLoss +
                " credited=" + creditedDamage + " dps=" + (profile != null ? profile.SustainedDps : 0f) + " raw=" + amount);
        if(amount>0f) RebirthWeaponSustainedDpsService.QueueCombatProgress(player,skillId,amount);

        if(preHitStealthQualified && creditedDamage>0f)
            RebirthStealthTrainingService.OnQualifiedDamage(player,target,response.Source.AttackingItem,creditedDamage,preHitHealth,record);
        if(RebirthWeaponFamilySkillService.IsMeleeSkill(skillId) && creditedDamage>0f)
            RebirthRageService.OnMeaningfulMeleeDamage(player,target,creditedDamage,skillId,response.Source.AttackingItem);
    }

}
