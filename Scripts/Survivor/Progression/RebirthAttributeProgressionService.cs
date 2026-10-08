using System;

#nullable disable

public static class RebirthAttributeProgressionService
{
    public static bool ApplyTraining(RebirthWorldCharacterRecord record, string attributeId, float rawAmount, out float applied)
    {
        applied=0f;
        if(record==null || record.Progression==null || rawAmount<=0f || string.IsNullOrEmpty(attributeId)) return false;
        RebirthAttributeRuntimeState state;
        if(!record.Progression.Attributes.TryGetValue(attributeId,out state) || state==null) return false;
        RebirthAttributeDefinition def=FindDefinition(attributeId);
        if(def==null || state.Current>=def.Max) return false;

        float potential=Math.Max(def.Min,state.Potential);
        float ratio=potential>0.0001f?state.Current/potential:1f;
        float multiplier;
        if(state.Current>potential) multiplier=RebirthProgressionRuntimeConfig.AbovePotentialMultiplier;
        else if(ratio<=RebirthProgressionRuntimeConfig.BelowPotentialFullUntil) multiplier=1f;
        else
        {
            float span=Math.Max(0.0001f,1f-RebirthProgressionRuntimeConfig.BelowPotentialFullUntil);
            float t=Math.Max(0f,Math.Min(1f,(ratio-RebirthProgressionRuntimeConfig.BelowPotentialFullUntil)/span));
            multiplier=1f+(RebirthProgressionRuntimeConfig.AtPotentialMultiplier-1f)*t;
        }
        float delta=rawAmount*Math.Max(0f,multiplier);
        if(delta<=0f)return false;
        float before=state.Current;
        state.Current=Math.Max(def.Min,Math.Min(def.Max,state.Current+delta));
        applied=state.Current-before;
        return applied>0.00001f;
    }

    public static string GetPrimaryAttributeForSkill(string skillId)
    {
        if(string.IsNullOrEmpty(skillId))return string.Empty;
        RebirthSkillDefinition definition;
        if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId,out definition)||definition==null)return string.Empty;
        return definition.PrimaryAttributeId??string.Empty;
    }

    private static RebirthAttributeDefinition FindDefinition(string id)
    {
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle==null || bundle.Progression==null)return null;
        for(int i=0;i<bundle.Progression.Attributes.Count;i++)
            if(string.Equals(bundle.Progression.Attributes[i].Id,id,StringComparison.OrdinalIgnoreCase))return bundle.Progression.Attributes[i];
        return null;
    }
}
