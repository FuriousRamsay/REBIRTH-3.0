using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

public static class RebirthSurvivorCreationValidator
{
    public static bool SupportsCreationTraitTarget(string target)
    {
        string t=(target??string.Empty).Trim();
        return string.Equals(t,"knowledge.grant",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"skill.start_bias",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.strength.current",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.dexterity.current",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.constitution.current",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.intelligence.current",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.charisma.current",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.strength.potential",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.dexterity.potential",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.constitution.potential",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.intelligence.potential",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"attribute.charisma.potential",StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t,"inventory.unencumbered_slots",StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MutableAttribute { public RebirthAttributeDefinition Def; public float Current; public float Potential; }

    /// <summary>Shared ordered creation evaluator used by both authoritative starting-skill
    /// resolution and UI explanation. Unknown operations fail closed instead of being silently
    /// flattened into additive values.</summary>
    public static float ApplyStartingSkillOperation(float current,string operation,float operand,out bool recognized)
    {
        string op=(operation??string.Empty).Trim().ToLowerInvariant();
        recognized=true;
        switch(op)
        {
            case "":
            case "add": current+=operand; break;
            case "multiply": current*=operand; break;
            case "set": current=operand; break;
            case "clamp_min": current=Math.Max(current,operand); break;
            case "clamp_max": current=Math.Min(current,operand); break;
            default: recognized=false; break;
        }
        return current;
    }


    public static RebirthSurvivorCreationResult Validate(RebirthSurvivorCreationSelection selection)
    {
        return Validate(selection, true, false);
    }

    public static RebirthSurvivorCreationResult Validate(RebirthSurvivorCreationSelection selection, bool requireRebirthMode)
    {
        return Validate(selection, requireRebirthMode, false);
    }

    /// <summary>
    /// Authoritative/final validation. Unlike the interactive creator validation, a finalized
    /// Survivor must have an exact zero Trait-point balance. This is used when saving reusable
    /// profiles, selecting one for a new world, and committing a world character.
    /// </summary>
    public static RebirthSurvivorCreationResult ValidateForCommit(RebirthSurvivorCreationSelection selection, bool requireRebirthMode)
    {
        return Validate(selection, requireRebirthMode, true);
    }

    private static RebirthSurvivorCreationResult Validate(RebirthSurvivorCreationSelection selection, bool requireRebirthMode, bool requireZeroPointBalance)
    {
        List<RebirthSurvivorCreationError> errors=new List<RebirthSurvivorCreationError>();
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(requireRebirthMode&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()) errors.Add(E(RebirthSurvivorCreationErrorCode.RebirthModeDisabled,"", ""));
        if(bundle==null||bundle.Progression==null)
        {
            errors.Add(E(RebirthSurvivorCreationErrorCode.DefinitionsUnavailable,"",""));
            return Empty(selection,errors);
        }
        if(selection==null) selection=new RebirthSurvivorCreationSelection(string.Empty,string.Empty,null,string.Empty);
        if(!string.IsNullOrEmpty(selection.DefinitionHash)&&!string.Equals(selection.DefinitionHash,RebirthSurvivorDefinitionRegistry.SemanticHash,StringComparison.OrdinalIgnoreCase)) errors.Add(E(RebirthSurvivorCreationErrorCode.DefinitionHashMismatch,selection.DefinitionHash,RebirthSurvivorDefinitionRegistry.SemanticHash));

        RebirthBackgroundDefinition background=null; RebirthDietDefinition diet=null;
        if(string.IsNullOrEmpty(selection.BackgroundId)) errors.Add(E(RebirthSurvivorCreationErrorCode.MissingBackground,"",""));
        else if(!RebirthSurvivorDefinitionRegistry.TryGetBackground(selection.BackgroundId,out background)) errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownBackground,selection.BackgroundId,""));
        if(string.IsNullOrEmpty(selection.DietId)) errors.Add(E(RebirthSurvivorCreationErrorCode.MissingDiet,"",""));
        else if(!RebirthSurvivorDefinitionRegistry.TryGetDiet(selection.DietId,out diet)) errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownDiet,selection.DietId,""));

        List<RebirthTraitDefinition> selectedTraits=new List<RebirthTraitDefinition>(); List<string> normalizedTraitIds=new List<string>(); HashSet<string> seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string raw in selection.TraitIds)
        {
            string id=(raw??string.Empty).Trim(); if(id.Length==0) { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownTrait,id,"")); continue; }
            if(!seen.Add(id)) { errors.Add(E(RebirthSurvivorCreationErrorCode.DuplicateTrait,id,"")); continue; }
            RebirthTraitDefinition t; if(!RebirthSurvivorDefinitionRegistry.TryGetTrait(id,out t)) { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownTrait,id,"")); continue; }
            selectedTraits.Add(t); normalizedTraitIds.Add(t.Id);
        }

        if(background!=null)
        {
            foreach(RebirthTraitDefinition t in selectedTraits)
            {
                if(t.Availability==RebirthDefinitionAvailability.Deferred) errors.Add(E(RebirthSurvivorCreationErrorCode.TraitNotAllowedForBackground,t.Id,background.Id));
                if(t.Availability==RebirthDefinitionAvailability.Restricted && (!Contains(t.AllowedBackgroundIds,background.Id)||!Contains(background.RestrictedTraitIds,t.Id))) errors.Add(E(RebirthSurvivorCreationErrorCode.TraitNotAllowedForBackground,t.Id,background.Id));
                if(Contains(background.BlockedTraitIds,t.Id)) errors.Add(E(RebirthSurvivorCreationErrorCode.TraitBlockedByBackground,t.Id,background.Id));
            }
        }

        HashSet<string> selectedSet=new HashSet<string>(normalizedTraitIds,StringComparer.OrdinalIgnoreCase);
        HashSet<string> emittedConflicts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(RebirthTraitDefinition t in selectedTraits)
        {
            foreach(string other in t.ConflictTraitIds)
            {
                if(!selectedSet.Contains(other))continue; string a=string.Compare(t.Id,other,StringComparison.OrdinalIgnoreCase)<=0?t.Id:other; string z=a==t.Id?other:t.Id; string pair=a+"|"+z;
                if(emittedConflicts.Add(pair)) errors.Add(E(RebirthSurvivorCreationErrorCode.TraitConflict,a,z));
            }
            if(diet!=null) foreach(string did in t.ConflictDietIds) if(string.Equals(did,diet.Id,StringComparison.OrdinalIgnoreCase)) errors.Add(E(RebirthSurvivorCreationErrorCode.TraitDietConflict,t.Id,diet.Id));
        }

        // Trait-point accounting must exactly match the creator UI: Background and Diet adjust the
        // base allowance, positive Traits/Aptitudes spend points, and every selected negative Trait
        // refunds its authored value when max_negative_trait_refund is -1 (the current design).
        int points=bundle.Progression.BaseCreationPoints + (background!=null?background.CreationPointModifier:0) + (diet!=null?diet.Points:0);
        if(background!=null&&string.Equals(background.Id,RebirthSurvivorIds.BackgroundCleanSlate,StringComparison.OrdinalIgnoreCase)) points+=bundle.Progression.CleanSlateBonus;
        int positiveSpend=0,negativeRefundRequested=0;
        foreach(RebirthTraitDefinition t in selectedTraits)
        {
            if(t.Polarity==RebirthTraitPolarity.Positive) positiveSpend+=t.Points;
            else if(t.Polarity==RebirthTraitPolarity.Negative) negativeRefundRequested+=t.Points;
        }
        int negativeRefundApplied=RebirthSurvivorTraitPointEconomy.ApplyNegativeRefund(bundle.Progression.MaxNegativeTraitRefund,negativeRefundRequested);
        points+=negativeRefundApplied-positiveSpend;
        if(points<0) errors.Add(E(RebirthSurvivorCreationErrorCode.InsufficientCreationPoints,points.ToString(CultureInfo.InvariantCulture),""));
        else if(requireZeroPointBalance && points>0) errors.Add(E(RebirthSurvivorCreationErrorCode.UnspentCreationPoints,points.ToString(CultureInfo.InvariantCulture),""));

        Dictionary<string,MutableAttribute> attributes=new Dictionary<string,MutableAttribute>(StringComparer.OrdinalIgnoreCase);
        foreach(RebirthAttributeDefinition a in bundle.Progression.Attributes) attributes[a.Id]=new MutableAttribute{Def=a,Current=a.BaseCurrent,Potential=a.BasePotential};
        float healthPotential=bundle.Progression.BaseHealthPotential; int slotDelta=0;
        Dictionary<string,float> skills=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase); foreach(RebirthSkillDefinition s in bundle.Progression.Skills) skills[s.Id]=0f;
        Dictionary<string,float> skillKnowledge=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase); foreach(RebirthSkillDefinition s in bundle.Progression.Skills) skillKnowledge[s.Id]=bundle.Progression.SkillKnowledgeMin;
        HashSet<string> knowledge=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if(background!=null)
        {
            HashSet<string> startingAttributeIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(RebirthStartingAttributeDefinition start in background.StartingAttributes)
            {
                MutableAttribute attribute;
                if(start==null||!startingAttributeIds.Add(start.AttributeId)||!attributes.TryGetValue(start.AttributeId,out attribute))
                { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,background.Id,start==null?"starting_attributes":start.AttributeId)); continue; }
                if(float.IsNaN(start.Current)||float.IsInfinity(start.Current)||start.Current<attribute.Def.Min||start.Current>attribute.Def.Max)
                { errors.Add(E(RebirthSurvivorCreationErrorCode.AttributeOutOfRange,start.AttributeId,"background current")); continue; }
                attribute.Current=start.Current;
            }
            foreach(RebirthStartingSkillBiasDefinition b in background.StartingSkills) { if(b.HasExplicitValue) AddDirect(skills,b.SkillId,b.Value,errors,background.Id); else AddTier(skills,b.SkillId,b.TierId,bundle.Progression,errors,background.Id); }
            foreach(RebirthStartingSkillKnowledgeDefinition k in background.StartingSkillKnowledge) { if(k!=null && skillKnowledge.ContainsKey(k.SkillId)) skillKnowledge[k.SkillId]=Math.Max(bundle.Progression.SkillKnowledgeMin,Math.Min(bundle.Progression.SkillKnowledgeMax,k.Value)); else if(k!=null) errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,background.Id,k.SkillId)); }
            foreach(string kid in background.StartingKnowledgeIds) knowledge.Add(kid);
        }
        foreach(RebirthTraitDefinition t in selectedTraits)
        {
            // Generated Skill Aptitudes are intentionally not backed by an authored condition
            // modifier profile. Their dynamic modifier ID is a creation-only sentinel. Resolve
            // the selected tier directly into the authoritative starting Skill snapshot.
            if(RebirthSkillAptitudeTraitFactory.IsAptitude(t))
            {
                string aptitudeSkill; int aptitudeTier, aptitudeBonus, aptitudeCost;
                if(!RebirthSkillAptitudeTraitFactory.TryParse(t.Id,out aptitudeSkill,out aptitudeTier,out aptitudeBonus,out aptitudeCost))
                {
                    errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,t.Id,RebirthSkillAptitudeTraitFactory.DynamicModifierId));
                    continue;
                }
                AddDirect(skills,aptitudeSkill,aptitudeBonus,errors,t.Id);
                continue;
            }

            RebirthConditionModifierProfileDefinition profile;
            if(!RebirthSurvivorDefinitionRegistry.TryGetModifier(t.ModifierId,out profile)) { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,t.Id,t.ModifierId)); continue; }
            foreach(RebirthConditionModifierComponent c in profile.Components)
            {
                if(!string.Equals(c.Phase,"creation",StringComparison.OrdinalIgnoreCase))continue;
                if(c.Target.StartsWith("attribute.",StringComparison.OrdinalIgnoreCase)) ApplyAttributeComponent(attributes,c,errors,t.Id);
                else if(string.Equals(c.Target,"health.potential",StringComparison.OrdinalIgnoreCase)) healthPotential+=Float(c.Value);
                else if(string.Equals(c.Target,"inventory.unencumbered_slots",StringComparison.OrdinalIgnoreCase)) slotDelta+=(int)Float(c.Value);
                else if(string.Equals(c.Target,"knowledge.grant",StringComparison.OrdinalIgnoreCase)) knowledge.Add(c.Value);
                else if(string.Equals(c.Target,"skill.start_bias",StringComparison.OrdinalIgnoreCase)) AddTier(skills,c.Value,c.Note,bundle.Progression,errors,t.Id);
                else if(c.Target.StartsWith("skill.start.",StringComparison.OrdinalIgnoreCase))
                {
                    string skillId=c.Target.Substring("skill.start.".Length); float v;
                    if(skills.TryGetValue(skillId,out v))
                    {
                        bool recognized; float next=ApplyStartingSkillOperation(v,c.Operation,Float(c.Value),out recognized);
                        if(recognized) skills[skillId]=next;
                        else errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,t.Id,c.Target+":"+c.Operation));
                    }
                    else errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,t.Id,c.Target));
                }
            }
        }

        List<RebirthResolvedAttributeStart> resolvedAttrs=new List<RebirthResolvedAttributeStart>();
        foreach(KeyValuePair<string,MutableAttribute> kv in attributes)
        {
            MutableAttribute a=kv.Value;
            if(a.Current<a.Def.Min||a.Current>a.Def.Max||a.Potential<a.Def.Min||a.Potential>a.Def.Max) errors.Add(E(RebirthSurvivorCreationErrorCode.AttributeOutOfRange,a.Def.Id,a.Current.ToString("0.###",CultureInfo.InvariantCulture)+"/"+a.Potential.ToString("0.###",CultureInfo.InvariantCulture)));
            if(a.Current>a.Potential) errors.Add(E(RebirthSurvivorCreationErrorCode.PotentialBelowCurrent,a.Def.Id,""));
            resolvedAttrs.Add(new RebirthResolvedAttributeStart(a.Def.Id,a.Current,a.Potential));
        }
        resolvedAttrs.Sort((a,z)=>StringComparer.Ordinal.Compare(a.AttributeId,z.AttributeId));
        if(healthPotential<bundle.Progression.MinHealthPotential||healthPotential>bundle.Progression.MaxHealthPotential) errors.Add(E(RebirthSurvivorCreationErrorCode.AttributeOutOfRange,"health.potential",healthPotential.ToString("0.###",CultureInfo.InvariantCulture)));

        // Starting Skill biases are relative adjustments, including legitimate negative Background Traits.
        // Resolve the signed final origin state against the authored Skill bounds. Negative values are valid
        // proficiency weaknesses; zero is neutral proficiency rather than the minimum.
        foreach(RebirthSkillDefinition skill in bundle.Progression.Skills)
        {
            float value;
            if(!skills.TryGetValue(skill.Id,out value)) continue;
            float creationMin=Math.Max(skill.Min,bundle.Progression.CreationSkillMin);
            float creationMax=Math.Min(skill.Max,bundle.Progression.CreationSkillMax);
            skills[skill.Id]=Math.Max(creationMin,Math.Min(creationMax,value));
        }

        normalizedTraitIds.Sort(StringComparer.Ordinal); List<string> knowledgeList=new List<string>(knowledge); knowledgeList.Sort(StringComparer.Ordinal);

        return new RebirthSurvivorCreationResult(RebirthSurvivorDefinitionRegistry.SemanticHash,RebirthSurvivorDefinitionRegistry.DefinitionVersion,background!=null?background.Id:selection.BackgroundId,diet!=null?diet.Id:selection.DietId,normalizedTraitIds,points,resolvedAttrs,healthPotential,slotDelta,skills,skillKnowledge,knowledgeList,errors);
    }

    private static void ApplyAttributeComponent(Dictionary<string,MutableAttribute> attrs,RebirthConditionModifierComponent c,List<RebirthSurvivorCreationError> errors,string owner)
    {
        string[] parts=c.Target.Split('.'); if(parts.Length!=3) { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,owner,c.Target)); return; }
        MutableAttribute a; if(!attrs.TryGetValue(parts[1],out a)) { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,owner,c.Target)); return; }
        float v=Float(c.Value); if(!string.Equals(c.Operation,"add",StringComparison.OrdinalIgnoreCase)) { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,owner,c.Target+":"+c.Operation)); return; }
        if(string.Equals(parts[2],"current",StringComparison.OrdinalIgnoreCase))a.Current+=v; else if(string.Equals(parts[2],"potential",StringComparison.OrdinalIgnoreCase))a.Potential+=v; else errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,owner,c.Target));
    }

    private static void AddDirect(Dictionary<string,float> skills,string skillId,float value,List<RebirthSurvivorCreationError> errors,string owner)
    {
        float current; if(!skills.TryGetValue(skillId??string.Empty,out current)) { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,owner,skillId)); return; }
        skills[skillId]=current+value;
    }
    private static void AddTier(Dictionary<string,float> skills,string skillId,string tierId,RebirthProgressionDefinition p,List<RebirthSurvivorCreationError> errors,string owner)
    {
        float current; int tier; if(!skills.TryGetValue(skillId??string.Empty,out current)) { errors.Add(E(RebirthSurvivorCreationErrorCode.UnknownCreationModifier,owner,skillId)); return; }
        if(!p.SkillBiasTiers.TryGetValue(tierId??string.Empty,out tier)) { errors.Add(E(RebirthSurvivorCreationErrorCode.InvalidSkillBiasTier,owner,tierId)); return; }
        skills[skillId]=current+tier;
    }
    private static float Float(string value) { float v; return float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:0f; }
    private static bool Contains(IEnumerable<string> values,string wanted) { foreach(string value in values) if(string.Equals(value,wanted,StringComparison.OrdinalIgnoreCase))return true; return false; }
    private static RebirthSurvivorCreationError E(RebirthSurvivorCreationErrorCode code,string subject,string related) { return new RebirthSurvivorCreationError(code,subject,related); }
    private static RebirthSurvivorCreationResult Empty(RebirthSurvivorCreationSelection s,List<RebirthSurvivorCreationError> errors) { return new RebirthSurvivorCreationResult(RebirthSurvivorDefinitionRegistry.SemanticHash,RebirthSurvivorDefinitionRegistry.DefinitionVersion,s!=null?s.BackgroundId:string.Empty,s!=null?s.DietId:string.Empty,s!=null?new List<string>(s.TraitIds):new List<string>(),0,new List<RebirthResolvedAttributeStart>(),0f,0,new Dictionary<string,float>(),new Dictionary<string,float>(),new List<string>(),errors); }
}
