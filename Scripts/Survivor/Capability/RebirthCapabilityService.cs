using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#nullable disable

public static class RebirthCapabilityService
{
    public static RebirthCapabilityEvaluation Evaluate(EntityPlayer player,string capabilityId)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return Allowed(capabilityId,string.Empty,string.Empty);
        RebirthCapabilityDefinition definition;
        if(!RebirthCapabilityRegistry.TryGet(capabilityId,out definition)||definition==null)
            return Allowed(capabilityId,string.Empty,string.Empty);
        return EvaluateDefinition(player,definition);
    }

    public static RebirthCapabilityEvaluation EvaluateRecipe(EntityPlayer player,string recipeName)
    {
        var evaluation = EvaluateRecipePolicy(player, recipeName, false);
        if (!RebirthSurvivorMode.IsEnabledForCurrentWorld() ||
            !RebirthRecipeDiscoveryRules.TryGetRequiredReading(recipeName, out var reading)) return evaluation;
        bool read = player != null && RebirthKnowledgeService.HasKnowledge(player, RebirthLiteratureService.RecipeReadMarker(reading));
        return WithReadingRequirement(evaluation,reading,read);
    }

    internal static RebirthCapabilityEvaluation WithReadingRequirement(RebirthCapabilityEvaluation evaluation,string reading,bool read)
    {
        var requirements = new List<RebirthCapabilityRequirementEvaluation>(evaluation.Requirements);
        requirements.Insert(0, new RebirthCapabilityRequirementEvaluation {
            Kind = RebirthCapabilityKinds.Knowledge, Id = reading, Allowed = read,
            CurrentValue = read ? 1 : 0, RequiredValue = 1,
            Message = RebirthKnowledgeService.GetDisplayName(reading)
        });
        return new RebirthCapabilityEvaluation(evaluation.CapabilityId, evaluation.TargetType,
            evaluation.TargetId, evaluation.IsAllowed && read, requirements);
    }

    // Experimentation replaces only this recipe's mapped knowledge leaf. Every other authored
    // requirement retains the same AND/OR evaluation and hard/recommended semantics.
    public static RebirthCapabilityEvaluation EvaluateRecipeForDiscovery(EntityPlayer player,string recipeName)
        => EvaluateRecipePolicy(player, recipeName, true);

    private static RebirthCapabilityEvaluation EvaluateRecipePolicy(EntityPlayer player,string recipeName,bool discovery)
    {
        string discoverableKnowledge = null;
        if (discovery && RebirthProgressionRuntimeConfig.TryGetRecipeRule(recipeName, out var rule))
            discoverableKnowledge = rule.KnowledgeId;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()) return Allowed(string.Empty,"recipe",recipeName);

        // During very early boot preserve the pre-Chunk-B Capability behavior. The progression
        // installer loads both registries before installing the crafting Harmony patches, so normal
        // gameplay always reaches the explicit policy path below.
        if(!RebirthCraftingProgressionRegistry.IsReady)
        {
            RebirthCapabilityDefinition legacy;
            if(!RebirthCapabilityRegistry.TryGetRecipe(recipeName,out legacy)||legacy==null)
                return Allowed(string.Empty,"recipe",recipeName);
            return EvaluateDefinition(player,legacy,discoverableKnowledge);
        }

        RebirthCraftingProgressionDefinition policy;
        if(!RebirthCraftingProgressionRegistry.TryGetRecipe(recipeName,out policy)||policy==null)
        {
            RebirthCraftingProgressionRegistry.NoteExternalCompatibilityRecipe(recipeName,"evaluate");
            return Allowed(string.Empty,"recipe",recipeName);
        }
        if(policy.IsUniversal) return Allowed(string.Empty,"recipe",recipeName);
        if(policy.IsDisabled)
            return PolicyDenied(recipeName,"disabled",policy.Family,L("xuiRebirthCraftDisabledByProgression","Recipe disabled by REBIRTH crafting progression policy"));

        RebirthCapabilityDefinition definition;
        if(!RebirthCapabilityRegistry.TryGet(policy.CapabilityId,out definition)||definition==null)
            return PolicyDenied(recipeName,"capability",policy.CapabilityId,"Progression requirements pending implementation for "+policy.Family);
        return EvaluateDefinition(player,definition,discoverableKnowledge);
    }

    public static RebirthCapabilityEvaluation EvaluateOperation(EntityPlayer player,string projectId,string operationId)
    {
        RebirthProjectDefinition project;
        if(!RebirthCapabilityRegistry.TryGetProject(projectId,out project)||project==null)
            return new RebirthCapabilityEvaluation("","project-operation",projectId+"/"+operationId,false,new[]{Missing("project",projectId,0f,0f,"Unknown Project: "+projectId)});
        RebirthProjectOperationDefinition operation=null;
        for(int i=0;i<project.Operations.Count;i++) if(string.Equals(project.Operations[i].Id,operationId,StringComparison.OrdinalIgnoreCase)){operation=project.Operations[i];break;}
        if(operation==null) return new RebirthCapabilityEvaluation("","project-operation",projectId+"/"+operationId,false,new[]{Missing("operation",operationId,0f,0f,"Unknown Project operation: "+operationId)});
        List<RebirthCapabilityRequirementEvaluation> values=new List<RebirthCapabilityRequirementEvaluation>();
        bool allowed=EvaluateRequirement(player,operation.Requirement,values);
        return new RebirthCapabilityEvaluation("project-operation."+projectId+"."+operationId,"project-operation",projectId+"/"+operationId,allowed,values);
    }

    public static string BuildRecipeDiagnostic(EntityPlayer player,string recipeName)
    {
        RebirthCraftingProgressionDefinition policy;
        bool owned=RebirthCraftingProgressionRegistry.TryGetRecipe(recipeName,out policy)&&policy!=null;
        RebirthCapabilityDefinition definition=null;
        bool mapped=owned && !string.IsNullOrEmpty(policy.CapabilityId)
            ? RebirthCapabilityRegistry.TryGet(policy.CapabilityId,out definition)&&definition!=null
            : RebirthCapabilityRegistry.TryGetRecipe(recipeName,out definition)&&definition!=null;
        RebirthCapabilityEvaluation evaluation=EvaluateRecipe(player,recipeName);
        StringBuilder b=new StringBuilder();
        b.Append("capabilityRecipe recipe=").Append(recipeName??string.Empty)
            .Append(" policyOwned=").Append(owned)
            .Append(" policy=").Append(policy!=null?policy.Policy:"external")
            .Append(" family=").Append(policy!=null?policy.Family:string.Empty)
            .Append(" primarySkill=").Append(policy!=null?policy.PrimarySkillId:string.Empty)
            .Append(" implementation=").Append(policy!=null?policy.Implementation:string.Empty)
            .Append(" mapped=").Append(mapped)
            .Append(" capability=").Append(definition!=null?definition.Id:(policy!=null?policy.CapabilityId:string.Empty))
            .Append(" legacy=").Append(definition!=null&&definition.LegacyAdapted)
            .Append(" allowed=").Append(evaluation.IsAllowed)
            .Append(" state=").Append(evaluation.DisplayState).AppendLine();
        for(int i=0;i<evaluation.Requirements.Count;i++)
        {
            RebirthCapabilityRequirementEvaluation r=evaluation.Requirements[i];
            b.Append("  ").Append(r.Kind).Append(" ").Append(r.Id)
                .Append(" allowed=").Append(r.Allowed)
                .Append(" warning=").Append(r.WarningOnly);
            if(string.Equals(r.Kind,RebirthCapabilityKinds.Skill,StringComparison.OrdinalIgnoreCase))
                b.Append(" current=").Append(r.CurrentValue.ToString("0.##",CultureInfo.InvariantCulture))
                 .Append(" required=").Append(r.RequiredValue.ToString("0.##",CultureInfo.InvariantCulture))
                 .Append(" recommended=").Append(r.RecommendedValue.ToString("0.##",CultureInfo.InvariantCulture));
            b.Append(" message=").Append(r.Message??string.Empty).AppendLine();
        }
        return b.ToString().TrimEnd();
    }

    private static RebirthCapabilityEvaluation EvaluateDefinition(EntityPlayer player,RebirthCapabilityDefinition definition,string discoverableKnowledge = null)
    {
        List<RebirthCapabilityRequirementEvaluation> values=new List<RebirthCapabilityRequirementEvaluation>();
        bool allowed=EvaluateRequirement(player,definition.Requirement,values,discoverableKnowledge);
        return new RebirthCapabilityEvaluation(definition.Id,definition.TargetType,definition.TargetId,allowed,values);
    }

    private static bool EvaluateRequirement(EntityPlayer player,RebirthCapabilityRequirement requirement,List<RebirthCapabilityRequirementEvaluation> output,string discoverableKnowledge = null)
    {
        if(requirement==null)return true;
        if(requirement.Kind==RebirthCapabilityKinds.All)
        {
            bool allowed=true;
            for(int i=0;i<requirement.Children.Count;i++) if(!EvaluateRequirement(player,requirement.Children[i],output,discoverableKnowledge)) allowed=false;
            return allowed;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Any)
        {
            if(requirement.Children.Count==0)return false;
            List<RebirthCapabilityRequirementEvaluation> best=null; int bestMissing=int.MaxValue; float bestGap=float.MaxValue;
            for(int i=0;i<requirement.Children.Count;i++)
            {
                List<RebirthCapabilityRequirementEvaluation> branch=new List<RebirthCapabilityRequirementEvaluation>();
                bool branchAllowed=EvaluateRequirement(player,requirement.Children[i],branch,discoverableKnowledge);
                if(branchAllowed){output.AddRange(branch);return true;}
                int missing=0;float gap=0f;
                for(int j=0;j<branch.Count;j++) if(!branch[j].Allowed&&!branch[j].WarningOnly){missing++;if(branch[j].Kind==RebirthCapabilityKinds.Skill)gap+=Math.Max(0f,branch[j].RequiredValue-branch[j].CurrentValue);}
                if(best==null||missing<bestMissing||(missing==bestMissing&&gap<bestGap)){best=branch;bestMissing=missing;bestGap=gap;}
            }
            if(best!=null)output.AddRange(best);
            return false;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Knowledge)
        {
            bool discoverable = !string.IsNullOrEmpty(discoverableKnowledge)
                && string.Equals(requirement.Id, discoverableKnowledge, StringComparison.OrdinalIgnoreCase);
            bool has=discoverable || RebirthKnowledgeService.HasKnowledge(player,requirement.Id);
            output.Add(new RebirthCapabilityRequirementEvaluation{Kind=requirement.Kind,Id=requirement.Id,Allowed=has,WarningOnly=false,Message=discoverable?L("xuiRebirthRecipeDiscoveryEligible","Recipe may be learned by successful crafting"):has?L("xuiRebirthKnowledgeOwned","Recipe / technique learned"):LF("xuiRebirthMissingKnowledgeFormat","Recipe / technique required: {0}",RebirthKnowledgeService.GetDisplayName(requirement.Id))});
            return has;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Discipline)
        {
            RebirthAdvancedDisciplineDefinition discipline;
            bool known=RebirthAdvancedDisciplineRegistry.TryGetDefinition(requirement.Id,out discipline);
            bool has=false;
            if(known && player!=null && player.world!=null)
            {
                if(!player.world.IsRemote())
                {
                    RebirthWorldCharacterRecord record;
                    has=RebirthWorldCharacterService.TryGet(player,out record) && record?.Progression!=null && record.Progression.AcquiredDisciplineIds.Contains(requirement.Id);
                }
                else
                {
                    var snapshot=RebirthSurvivorClientState.GetOwnerStateSnapshot();
                    RebirthSurvivorOwnerScalars scalars;
                    has=RebirthSurvivorClientState.TryGetOwnerScalars(player,out scalars) && snapshot!=null && snapshot.AcquiredDisciplineIds.Contains(requirement.Id);
                }
            }
            string name=known?L(discipline.NameKey,requirement.Id):requirement.Id;
            output.Add(new RebirthCapabilityRequirementEvaluation{Kind=requirement.Kind,Id=requirement.Id,Allowed=has,Message="Discipline: "+name});
            return has;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Blueprint)
        {
            bool has=RebirthBlueprintService.HasBlueprint(player,requirement.Id);
            output.Add(new RebirthCapabilityRequirementEvaluation{Kind=requirement.Kind,Id=requirement.Id,Allowed=has,WarningOnly=false,Message=has?"Blueprint owned":"Missing Blueprint: "+requirement.Id});
            return has;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Skill)
        {
            float current; bool resolved=RebirthServiceCraftSkillService.TryGetPracticalSkillValue(player,requirement.Id,out current);
            string name=SkillName(requirement.Id);
            if(!resolved && requirement.HasMinimum)
            {
                output.Add(new RebirthCapabilityRequirementEvaluation{Kind=requirement.Kind,Id=requirement.Id,Allowed=false,WarningOnly=false,CurrentValue=0f,RequiredValue=requirement.Minimum,RecommendedValue=requirement.HasRecommended?requirement.Recommended:0f,Message=LF("xuiRebirthSkillUnavailableFormat","Skill state unavailable: {0}",name)});
                return false;
            }
            if(!resolved)
            {
                output.Add(new RebirthCapabilityRequirementEvaluation{Kind=requirement.Kind,Id=requirement.Id,Allowed=true,WarningOnly=true,CurrentValue=0f,RequiredValue=0f,RecommendedValue=requirement.HasRecommended?requirement.Recommended:0f,Message=LF("xuiRebirthSkillUnavailableFormat","Skill state unavailable: {0}",name)});
                return true;
            }
            bool hardAllowed=!requirement.HasMinimum || current+0.0001f>=requirement.Minimum;
            if(!hardAllowed)
            {
                output.Add(new RebirthCapabilityRequirementEvaluation{Kind=requirement.Kind,Id=requirement.Id,Allowed=false,WarningOnly=false,CurrentValue=current,RequiredValue=requirement.Minimum,RecommendedValue=requirement.HasRecommended?requirement.Recommended:0f,Message=LF("xuiRebirthSkillRequiredFormat","{0} {1} / {2} required",name,current.ToString("0.##",CultureInfo.InvariantCulture),requirement.Minimum.ToString("0.##",CultureInfo.InvariantCulture))});
                return false;
            }
            output.Add(new RebirthCapabilityRequirementEvaluation{Kind=requirement.Kind,Id=requirement.Id,Allowed=true,WarningOnly=false,CurrentValue=current,RequiredValue=requirement.HasMinimum?requirement.Minimum:0f,RecommendedValue=requirement.HasRecommended?requirement.Recommended:0f,Message=name+" requirement met"});
            if(requirement.HasRecommended && current+0.0001f<requirement.Recommended)
                output.Add(new RebirthCapabilityRequirementEvaluation{Kind=requirement.Kind,Id=requirement.Id,Allowed=true,WarningOnly=true,CurrentValue=current,RequiredValue=requirement.HasMinimum?requirement.Minimum:0f,RecommendedValue=requirement.Recommended,Message=LF("xuiRebirthSkillRecommendedFormat","{0} {1} / {2} recommended",name,current.ToString("0.##",CultureInfo.InvariantCulture),requirement.Recommended.ToString("0.##",CultureInfo.InvariantCulture))});
            return true;
        }
        output.Add(Missing(requirement.Kind,requirement.Id,0f,0f,"Unsupported Capability requirement: "+requirement.Kind));
        return false;
    }

    private static string L(string key,string fallback)
    {
        string value=Localization.Get(key??string.Empty);
        return string.IsNullOrEmpty(value)||string.Equals(value,key,StringComparison.Ordinal)?fallback:value;
    }

    private static string LF(string key,string fallback,params object[] args)
    {
        string format=L(key,fallback);
        try{return string.Format(CultureInfo.InvariantCulture,format,args??new object[0]);}
        catch{return string.Format(CultureInfo.InvariantCulture,fallback,args??new object[0]);}
    }

    private static string SkillName(string id)
    {
        return RebirthSkillDisplayNames.Get(id);
    }

    private static RebirthCapabilityRequirementEvaluation Missing(string kind,string id,float current,float required,string message)
    {return new RebirthCapabilityRequirementEvaluation{Kind=kind??string.Empty,Id=id??string.Empty,Allowed=false,WarningOnly=false,CurrentValue=current,RequiredValue=required,Message=message??string.Empty};}

    private static RebirthCapabilityEvaluation PolicyDenied(string recipeName,string kind,string id,string message)
    {
        return new RebirthCapabilityEvaluation(string.Empty,"recipe",recipeName,false,new[]{Missing("policy."+(kind??string.Empty),id,0f,0f,message)});
    }

    private static RebirthCapabilityEvaluation Allowed(string capabilityId,string targetType,string targetId)
    {return new RebirthCapabilityEvaluation(capabilityId,targetType,targetId,true,new RebirthCapabilityRequirementEvaluation[0]);}
}

/// <summary>
/// Blueprint state is intentionally non-authoritative in Chunk 01: the schema can reference known Blueprint IDs,
/// but no live Capability is allowed to require one until the world-character persistence/network migration ships.
/// Returning false fails closed if an author accidentally enables such a rule early.
/// </summary>
public static class RebirthBlueprintService
{
    public static bool HasBlueprint(EntityPlayer player,string blueprintId)
    { return string.IsNullOrEmpty(blueprintId); }
}
