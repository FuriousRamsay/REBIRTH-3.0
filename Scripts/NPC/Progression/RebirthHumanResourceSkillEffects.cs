using System;

#nullable disable

// Gives a human NPC's native HarvestCount calculation the same skill-driven passive the player has.
// The player's Mining/Logging/Salvage/Farming effect is two CVars read by RebirthSurvivorResourceFieldPassives
// (RebirthResourceFieldSkillService.BeginResourceHarvestAttack / SyncNativePassiveEffects). The NPC's value comes from its
// own saved skill, never the owner's. Scoped per harvest computation: CVars are restored and a buff that Begin installed is removed.
internal static class RebirthHumanResourceSkillEffects
{
    internal struct Scope
    {
        internal float PriorResource, PriorFarming;
        internal bool Active, InstalledBuff;
    }

    internal static Scope Begin(EntityAlive entity, ItemValue tool, Block block)
    {
        var scope = new Scope();
        var npc = entity as EntityRebirthHumanoidNPC;
        if(npc == null || npc.Buffs == null || npc.RebirthRuntimeState == null || block == null) return scope;
        scope.PriorResource = npc.Buffs.GetCustomVar(RebirthResourceFieldSkillService.ResourceHarvestCVar);
        scope.PriorFarming = npc.Buffs.GetCustomVar(RebirthResourceFieldSkillService.FarmingHarvestCVar);
        scope.Active = true;
        string skillId = RebirthProgressionRuntimeConfig.ClassifyHarvest(tool, block.GetBlockName());
        float resource = 0f, farming = 0f, practical, attribute, effective;
        var id = npc.RebirthRuntimeState.StableId;
        if(!string.IsNullOrEmpty(skillId) && (skillId == RebirthSurvivorIds.SkillMining || skillId == RebirthSurvivorIds.SkillLogging ||
            skillId == RebirthSurvivorIds.SkillSalvage) &&
            RebirthSkillOutcomeValueService.TryGetPracticalAndEffective(id, skillId, out practical, out effective, out attribute))
            resource = RebirthResourceFieldSkillService.SignedEndpoint(effective,
                RebirthProgressionRuntimeConfig.ResourceHarvestNegative, RebirthProgressionRuntimeConfig.ResourceHarvestPositive);
        if(RebirthSkillOutcomeValueService.TryGetPracticalAndEffective(id, RebirthSurvivorIds.SkillFarming, out practical, out effective, out attribute))
            farming = RebirthResourceFieldSkillService.SignedEndpoint(effective,
                RebirthProgressionRuntimeConfig.FarmingHarvestNegative, RebirthProgressionRuntimeConfig.FarmingHarvestPositive);
        Set(npc, RebirthResourceFieldSkillService.ResourceHarvestCVar, resource);
        Set(npc, RebirthResourceFieldSkillService.FarmingHarvestCVar, farming);
        if(!npc.Buffs.HasBuff(RebirthResourceFieldSkillService.PassiveBuff))
        {
            npc.Buffs.AddBuff(RebirthResourceFieldSkillService.PassiveBuff);
            scope.InstalledBuff = true;
        }
        return scope;
    }

    internal static void End(EntityAlive entity, Scope scope)
    {
        var npc = entity as EntityRebirthHumanoidNPC;
        if(!scope.Active || npc == null || npc.Buffs == null) return;
        try
        {
            Set(npc, RebirthResourceFieldSkillService.ResourceHarvestCVar, scope.PriorResource);
            Set(npc, RebirthResourceFieldSkillService.FarmingHarvestCVar, scope.PriorFarming);
        }
        finally
        {
            if(scope.InstalledBuff && npc.Buffs.HasBuff(RebirthResourceFieldSkillService.PassiveBuff))
                npc.Buffs.RemoveBuff(RebirthResourceFieldSkillService.PassiveBuff);
        }
    }

    private static void Set(EntityAlive npc, string key, float value)
    { if(Math.Abs(npc.Buffs.GetCustomVar(key) - value) > 0.0005f) npc.Buffs.SetCustomVar(key, value); }
}
