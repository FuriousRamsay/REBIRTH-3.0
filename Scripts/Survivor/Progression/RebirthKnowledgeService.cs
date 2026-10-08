using System;

#nullable disable

public static class RebirthKnowledgeService
{
    public static bool HasKnowledge(EntityPlayer player,string knowledgeId)
    {
        if(string.IsNullOrEmpty(knowledgeId))return true;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(player!=null && player.world!=null && !player.world.IsRemote() && RebirthWorldCharacterRepository.IsServerAuthority)
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(player,out identity)||identity==null)return false;
            if(!RebirthWorldCharacterRepository.TryGet(identity,out record)||record==null||record.Progression==null)return false;
            return record.Progression.KnowledgeIds.Contains(knowledgeId);
        }

        // Owning clients only inspect the server-issued owner snapshot. They never invent Knowledge.
        RebirthSurvivorOwnerScalars scalars;
        return RebirthSurvivorClientState.TryGetOwnerScalars(player, out scalars) && scalars.HasKnowledge(knowledgeId);
    }

    public static bool CanCraft(EntityPlayer player,string recipeName,out string requiredKnowledgeId)
    {
        requiredKnowledgeId=string.Empty;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return true;
        RebirthRecipeKnowledgeRule rule;
        if(!RebirthProgressionRuntimeConfig.TryGetRecipeRule(recipeName,out rule))return true;
        requiredKnowledgeId=rule.KnowledgeId;
        if(HasKnowledge(player,rule.KnowledgeId))return true;
        // Existing schema-6 characters may still own the former broad binary Knowledge. It remains
        // a compatibility alias only; new profiles/backgrounds and literature use individual recipe IDs.
        return !string.IsNullOrEmpty(rule.LegacyKnowledgeId) && HasKnowledge(player,rule.LegacyKnowledgeId);
    }

    public static bool Grant(EntityPlayer player,string knowledgeId,string reason)
    {
        RebirthKnowledgeDefinition def;
        if(!RebirthSurvivorDefinitionRegistry.TryGetKnowledge(knowledgeId,out def)||def==null)return false;
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(player,out identity,out record))return false;
        if(record.Progression.KnowledgeIds.Contains(knowledgeId))return false;
        record.Progression.KnowledgeIds.Add(knowledgeId);
        record.Touch("knowledge-grant:"+knowledgeId+":"+(reason??string.Empty));
        if (!RebirthWorldCharacterRepository.SaveIfDirty(identity,"knowledge-grant:"+knowledgeId))
        {
            record.Progression.KnowledgeIds.Remove(knowledgeId);
            record.Touch("knowledge-grant-save-rollback:"+knowledgeId);
            return false;
        }
        if (!RebirthSurvivorNetworkService.SendOwnerState(player,0L,true,"knowledge-grant:"+knowledgeId))
            RebirthSkillAwardService.QueueOwnerPublication(player);
        RebirthStatisticsService.RecordKnowledgeDiscovered(player,knowledgeId);
        return true;
    }

    public static string GetDisplayName(string knowledgeId)
    {
        return RebirthKnowledgeDisplayNames.Get(knowledgeId);
    }
}
