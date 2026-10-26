using System;
using UnityEngine;

#nullable disable

// Reuses the same authored passive buff and CVars as the player's weapon-family
// handling. Direct damage and ammunition are intentionally untouched.
internal static class RebirthHumanWeaponPassives
{
    private static float nextSync;
    private static int cursor;
    internal static void Tick()
    {
        var world = GameManager.Instance?.World;
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread() || Time.unscaledTime < nextSync) return;
        nextSync = Time.unscaledTime + 1f;
        var actors = RebirthNpcRuntimeRegistry.GetSnapshot();
        if(actors.Length == 0) { cursor = 0; return; }
        int budget = Math.Min(32, actors.Length);
        for(int i = 0; i < budget; i++)
        {
            var runtime = actors[(cursor + i) % actors.Length];
            if(!RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId,out var entityId)) continue;
            var npc = world.GetEntity(entityId) as EntityRebirthHumanoidNPC;
            if(npc == null || npc.IsDead() || npc.IsPreparedRestorationPending || npc.Buffs == null) continue;
            Sync(npc);
        }
        cursor = (cursor + budget) % actors.Length;
    }
    // Immediate refresh after an authorized equipment write changed the held cell (the 1 s tick would leave the old weapon's
    // skill passives on the new item). Server-side, main thread; failures fall back to the periodic tick.
    internal static void SyncNow(EntityRebirthHumanoidNPC npc)
    {
        if(npc == null || npc.world == null || npc.world.IsRemote() || !ThreadManager.IsMainThread() || npc.IsDead() ||
           npc.IsPreparedRestorationPending || npc.Buffs == null || npc.RebirthRuntimeState == null) return;
        // Peek first: this runs inside an equipment write and must never create or save a progression row.
        if(!RebirthHumanProgression.TryPeek(npc.RebirthRuntimeState.StableId, out _)) return;
        try { Sync(npc); } catch(Exception) { }
    }
    private static void Sync(EntityRebirthHumanoidNPC npc)
    {
        var held = npc.inventory?.holdingItemItemValue;
        string skillId = RebirthG02WeaponFamilies.Classify(held);
        float practical = 0f, effective = 0f, attribute = 0f;
        bool valid = RebirthWeaponFamilySkillService.IsChunk7Skill(skillId) &&
            RebirthSkillOutcomeValueService.TryGetPracticalAndEffective(npc.RebirthRuntimeState.StableId,skillId,
                out practical,out effective,out attribute);
        bool melee = valid && RebirthWeaponFamilySkillService.IsMeleeSkill(skillId);
        bool ranged = valid && RebirthWeaponFamilySkillService.IsRangedSkill(skillId);
        Set(npc,RebirthWeaponFamilySkillService.MeleeActiveCVar,melee?1f:0f);
        Set(npc,RebirthWeaponFamilySkillService.RangedActiveCVar,ranged?1f:0f);
        Set(npc,RebirthWeaponFamilySkillService.MeleeStaminaCVar,melee?RebirthWeaponFamilySkillService.SignedEndpoint(effective,
            RebirthProgressionRuntimeConfig.MeleeStaminaNegative,RebirthProgressionRuntimeConfig.MeleeStaminaPositive):0f);
        Set(npc,RebirthWeaponFamilySkillService.MeleeSpeedCVar,melee?RebirthWeaponFamilySkillService.SignedEndpoint(effective,
            RebirthProgressionRuntimeConfig.MeleeSpeedNegative,RebirthProgressionRuntimeConfig.MeleeSpeedPositive):0f);
        Set(npc,RebirthWeaponFamilySkillService.RangedReloadCVar,ranged?RebirthWeaponFamilySkillService.SignedEndpoint(effective,
            RebirthProgressionRuntimeConfig.RangedReloadNegative,RebirthProgressionRuntimeConfig.RangedReloadPositive):0f);
        Set(npc,RebirthWeaponFamilySkillService.RangedHandlingCVar,ranged?RebirthWeaponFamilySkillService.SignedEndpoint(effective,
            RebirthProgressionRuntimeConfig.RangedHandlingNegative,RebirthProgressionRuntimeConfig.RangedHandlingPositive):0f);
        Set(npc,RebirthWeaponFamilySkillService.RangedSpreadCVar,ranged?RebirthWeaponFamilySkillService.SignedEndpoint(effective,
            RebirthProgressionRuntimeConfig.RangedSpreadNegative,RebirthProgressionRuntimeConfig.RangedSpreadPositive):0f);
        Set(npc,RebirthWeaponFamilySkillService.RangedRecoilCVar,ranged?RebirthWeaponFamilySkillService.SignedEndpoint(effective,
            RebirthProgressionRuntimeConfig.RangedRecoilNegative,RebirthProgressionRuntimeConfig.RangedRecoilPositive):0f);
        if(valid)
        {
            if(!npc.Buffs.HasBuff(RebirthWeaponFamilySkillService.PassiveBuff)) npc.Buffs.AddBuff(RebirthWeaponFamilySkillService.PassiveBuff);
        }
        else if(npc.Buffs.HasBuff(RebirthWeaponFamilySkillService.PassiveBuff)) npc.Buffs.RemoveBuff(RebirthWeaponFamilySkillService.PassiveBuff);
        RebirthG01NpcBackgroundEffects.SyncPassives(npc); // G01: cached trait passives, idempotent
    }
    private static void Set(EntityRebirthHumanoidNPC npc,string key,float value)
    { if(Math.Abs(npc.Buffs.GetCustomVar(key)-value)>0.0005f) npc.Buffs.SetCustomVar(key,value); }
}
