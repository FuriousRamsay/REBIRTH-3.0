using System;
using UnityEngine;

#nullable disable

/// <summary>
/// Pass 5A bridge for the documented final progression model:
/// Practical Skill remains dominant; the Skill's primary Attribute contributes only a small,
/// bounded Skill-equivalent offset to gameplay outcomes. This never mutates stored Skill value.
/// </summary>
public static class RebirthSkillOutcomeValueService
{
    public static float ApplyAttributeContribution(string skillId,float practicalSkill,float attributeCurrent)
    {
        float center=RebirthProgressionRuntimeConfig.AttributeOutcomeCenter;
        float maxEquivalent=Math.Max(0f,RebirthProgressionRuntimeConfig.AttributeOutcomeMaxSkillEquivalent);
        float span=Math.Max(1f,RebirthProgressionRuntimeConfig.AttributeOutcomeRange);
        float normalized=Mathf.Clamp((attributeCurrent-center)/span,-1f,1f);
        float contribution=normalized*maxEquivalent;
        return Mathf.Clamp(practicalSkill+contribution,-50f,100f);
    }

    public static bool TryGetPracticalAndEffective(EntityPlayer player,string skillId,out float practical,out float effective,out float attributeCurrent)
    {
        practical=effective=0f;
        attributeCurrent=RebirthProgressionRuntimeConfig.AttributeOutcomeCenter;
        if(player==null || string.IsNullOrEmpty(skillId))return false;

        string attributeId=RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(skillId);
        if(player.world!=null && player.world.IsRemote())
        {
            RebirthSurvivorOwnerScalars scalars;
            if (!RebirthSurvivorClientState.TryGetOwnerScalars(player, out scalars) || !scalars.TryGetSkill(skillId, out practical)) return false;
            float projectedAttribute;
            if (!string.IsNullOrEmpty(attributeId) && scalars.TryGetAttribute(attributeId, out projectedAttribute)) attributeCurrent = projectedAttribute;
            effective=string.IsNullOrEmpty(attributeId)?practical:ApplyAttributeContribution(skillId,practical,attributeCurrent);
            return true;
        }

        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record) || record==null || !record.IsComplete || record.Progression==null)return false;
        RebirthSkillRuntimeState skill;
        if(!record.Progression.Skills.TryGetValue(skillId,out skill) || skill==null)return false;
        practical=skill.Value;
        if(!string.IsNullOrEmpty(attributeId))
        {
            RebirthAttributeRuntimeState attribute;
            if(record.Progression.Attributes.TryGetValue(attributeId,out attribute) && attribute!=null)
                attributeCurrent=attribute.Current;
        }
        effective=string.IsNullOrEmpty(attributeId)?practical:ApplyAttributeContribution(skillId,practical,attributeCurrent);
        return true;
    }

    public static bool TryGetEffective(EntityPlayer player,string skillId,out float effective)
    {
        float practical,attribute;
        return TryGetPracticalAndEffective(player,skillId,out practical,out effective,out attribute);
    }

    public static float ContributionFor(float practical,float effective)
    {
        return effective-practical;
    }
}
