using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#nullable disable

public enum RebirthProgressionExplorerMode
{
    Neutral,
    CreatorPreview,
    LiveCharacter
}

public enum RebirthProgressionRequirementState
{
    NotApplicable,
    Unknown,
    Satisfied,
    Blocked,
    RecommendationOnly
}

public enum RebirthProgressionNodeAccessState
{
    Informational,
    Unknown,
    Available,
    AvailableWithRecommendations,
    Locked
}

public sealed class RebirthProgressionNodeOverlay
{
    public string NodeId { get; private set; }
    public bool IsLearnedKnowledge { get; private set; }
    public bool IsSelectedBackground { get; private set; }
    public bool IsSelectedTrait { get; private set; }
    public bool HasSkillValue { get; private set; }
    public float SkillValue { get; private set; }
    public float SkillProgress { get; private set; }
    public bool HasSkillKnowledgeValue { get; private set; }
    public float SkillKnowledgeValue { get; private set; }
    public RebirthProgressionNodeAccessState AccessState { get; private set; }
    public ReadOnlyCollection<RebirthProgressionRequirementOverlay> Requirements { get; private set; }

    public RebirthProgressionNodeOverlay(string nodeId,bool learned,bool selectedBackground,bool selectedTrait,bool hasSkill,float skillValue,float skillProgress,bool hasSkillKnowledge,float skillKnowledgeValue,RebirthProgressionNodeAccessState access,IList<RebirthProgressionRequirementOverlay> requirements)
    {
        NodeId=nodeId??string.Empty;IsLearnedKnowledge=learned;IsSelectedBackground=selectedBackground;IsSelectedTrait=selectedTrait;
        HasSkillValue=hasSkill;SkillValue=skillValue;SkillProgress=skillProgress;HasSkillKnowledgeValue=hasSkillKnowledge;SkillKnowledgeValue=skillKnowledgeValue;AccessState=access;
        Requirements=new ReadOnlyCollection<RebirthProgressionRequirementOverlay>(new List<RebirthProgressionRequirementOverlay>(requirements??new List<RebirthProgressionRequirementOverlay>()));
    }
}

public sealed class RebirthProgressionRequirementOverlay
{
    public string RequirementId { get; private set; }
    public string Kind { get; private set; }
    public RebirthProgressionRequirementState State { get; private set; }
    public float CurrentValue { get; private set; }
    public float RequiredValue { get; private set; }
    public float RecommendedValue { get; private set; }
    public string Message { get; private set; }

    public RebirthProgressionRequirementOverlay(string id,string kind,RebirthProgressionRequirementState state,float current,float required,float recommended,string message)
    {RequirementId=id??string.Empty;Kind=kind??string.Empty;State=state;CurrentValue=current;RequiredValue=required;RecommendedValue=recommended;Message=message??string.Empty;}
}

public sealed class RebirthProgressionExplorerOverlaySnapshot
{
    private readonly Dictionary<string,RebirthProgressionNodeOverlay> byId;
    public RebirthProgressionExplorerMode Mode { get; private set; }
    public string BackgroundId { get; private set; }
    public ReadOnlyCollection<string> TraitIds { get; private set; }
    public ReadOnlyCollection<RebirthProgressionNodeOverlay> Nodes { get; private set; }

    public RebirthProgressionExplorerOverlaySnapshot(RebirthProgressionExplorerMode mode,string backgroundId,IList<string> traitIds,IList<RebirthProgressionNodeOverlay> nodes)
    {
        Mode=mode;BackgroundId=backgroundId??string.Empty;TraitIds=new ReadOnlyCollection<string>(new List<string>(traitIds??new List<string>()));
        List<RebirthProgressionNodeOverlay> copy=new List<RebirthProgressionNodeOverlay>(nodes??new List<RebirthProgressionNodeOverlay>());
        Nodes=new ReadOnlyCollection<RebirthProgressionNodeOverlay>(copy);byId=new Dictionary<string,RebirthProgressionNodeOverlay>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<copy.Count;i++)if(copy[i]!=null&&!string.IsNullOrEmpty(copy[i].NodeId))byId[copy[i].NodeId]=copy[i];
    }

    public bool TryGet(string nodeId,out RebirthProgressionNodeOverlay value){return byId.TryGetValue(nodeId??string.Empty,out value);}
}

internal sealed class RebirthProgressionOverlayValues
{
    public RebirthProgressionExplorerMode Mode;
    public string BackgroundId=string.Empty;
    public readonly HashSet<string> TraitIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> KnowledgeIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> DisciplineIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> AccomplishmentIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly HashSet<string> CompletedTrialIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string,float> Skills=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string,float> SkillProgress=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string,float> SkillKnowledge=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
    public bool HasCharacterState;
    public bool DefinitionsCompatible=true;
}

/// <summary>
/// Builds read-only Neutral, Creator Preview and Live Character overlays over the canonical graph.
/// It does not mutate a draft or live progression state.
/// </summary>
public static class RebirthProgressionExplorerOverlayService
{
    public static RebirthProgressionExplorerOverlaySnapshot CreateNeutral(RebirthProgressionGraphNeighborhood neighborhood)
    {return Build(neighborhood,new RebirthProgressionOverlayValues{Mode=RebirthProgressionExplorerMode.Neutral,HasCharacterState=false});}

    public static RebirthProgressionExplorerOverlaySnapshot CreateCreatorPreview(RebirthProgressionGraphNeighborhood neighborhood,RebirthSurvivorCreationResult result)
    {
        RebirthProgressionOverlayValues values=new RebirthProgressionOverlayValues{Mode=RebirthProgressionExplorerMode.CreatorPreview,HasCharacterState=result!=null};
        if(result!=null)
        {
            values.BackgroundId=result.BackgroundId??string.Empty;
            for(int i=0;i<result.TraitIds.Count;i++)values.TraitIds.Add(result.TraitIds[i]);
            for(int i=0;i<result.StartingKnowledgeIds.Count;i++)values.KnowledgeIds.Add(result.StartingKnowledgeIds[i]);
            foreach(KeyValuePair<string,float> pair in result.StartingSkills)values.Skills[pair.Key]=pair.Value;
            foreach(KeyValuePair<string,float> pair in result.StartingSkillKnowledge)values.SkillKnowledge[pair.Key]=pair.Value;
        }
        return Build(neighborhood,values);
    }

    public static RebirthProgressionExplorerOverlaySnapshot CreateLiveCharacter(RebirthProgressionGraphNeighborhood neighborhood,RebirthSurvivorOwnerStateSnapshot snapshot)
    {
        RebirthProgressionOverlayValues values=new RebirthProgressionOverlayValues{Mode=RebirthProgressionExplorerMode.LiveCharacter,HasCharacterState=snapshot!=null&&snapshot.HasCharacter,DefinitionsCompatible=snapshot!=null&&snapshot.DefinitionsCompatible};
        if(snapshot!=null)
        {
            values.BackgroundId=snapshot.BackgroundId??string.Empty;
            for(int i=0;i<snapshot.TraitIds.Count;i++)values.TraitIds.Add(snapshot.TraitIds[i]);
            for(int i=0;i<snapshot.KnowledgeIds.Count;i++)values.KnowledgeIds.Add(snapshot.KnowledgeIds[i]);
            for(int i=0;i<snapshot.AcquiredDisciplineIds.Count;i++)values.DisciplineIds.Add(snapshot.AcquiredDisciplineIds[i]);
            for(int i=0;i<snapshot.AccomplishmentIds.Count;i++)values.AccomplishmentIds.Add(snapshot.AccomplishmentIds[i]);
            for(int i=0;i<snapshot.CompletedTrialIds.Count;i++)values.CompletedTrialIds.Add(snapshot.CompletedTrialIds[i]);
            for(int i=0;i<snapshot.Skills.Count;i++)if(snapshot.Skills[i]!=null){values.Skills[snapshot.Skills[i].Id]=snapshot.Skills[i].Value;values.SkillProgress[snapshot.Skills[i].Id]=snapshot.Skills[i].Progress;}
            for(int i=0;i<snapshot.SkillKnowledge.Count;i++)if(snapshot.SkillKnowledge[i]!=null)values.SkillKnowledge[snapshot.SkillKnowledge[i].Id]=snapshot.SkillKnowledge[i].Value;
        }
        return Build(neighborhood,values);
    }

    private static RebirthProgressionExplorerOverlaySnapshot Build(RebirthProgressionGraphNeighborhood neighborhood,RebirthProgressionOverlayValues values)
    {
        List<RebirthProgressionNodeOverlay> overlays=new List<RebirthProgressionNodeOverlay>();
        if(neighborhood==null)return new RebirthProgressionExplorerOverlaySnapshot(values.Mode,values.BackgroundId,new List<string>(values.TraitIds),overlays);
        for(int i=0;i<neighborhood.Nodes.Count;i++)
        {
            RebirthProgressionGraphNode node=neighborhood.Nodes[i];
            bool learned=node.Type==RebirthProgressionGraphNodeType.Knowledge&&values.KnowledgeIds.Contains(node.Id);
            bool selectedBackground=node.Type==RebirthProgressionGraphNodeType.Background&&string.Equals(node.Id,values.BackgroundId,StringComparison.OrdinalIgnoreCase);
            bool selectedTrait=false;
            float skillValue=0f,skillProgress=0f,skillKnowledgeValue=0f;bool hasSkill=false,hasSkillKnowledge=false;
            if(node.Type==RebirthProgressionGraphNodeType.Skill)
            {
                hasSkill=values.HasCharacterState;
                values.Skills.TryGetValue(node.Id,out skillValue);values.SkillProgress.TryGetValue(node.Id,out skillProgress);
                hasSkillKnowledge=values.HasCharacterState&&values.SkillKnowledge.TryGetValue(node.Id,out skillKnowledgeValue);
            }
            List<RebirthProgressionRequirementOverlay> requirements=new List<RebirthProgressionRequirementOverlay>();
            RebirthProgressionNodeAccessState access=EvaluateAccess(node,values,requirements);
            overlays.Add(new RebirthProgressionNodeOverlay(node.Id,learned,selectedBackground,selectedTrait,hasSkill,skillValue,skillProgress,hasSkillKnowledge,skillKnowledgeValue,access,requirements));
        }
        List<string> traits=new List<string>(values.TraitIds);traits.Sort(StringComparer.OrdinalIgnoreCase);
        return new RebirthProgressionExplorerOverlaySnapshot(values.Mode,values.BackgroundId,traits,overlays);
    }

    private static RebirthProgressionNodeAccessState EvaluateAccess(RebirthProgressionGraphNode node,RebirthProgressionOverlayValues values,List<RebirthProgressionRequirementOverlay> output)
    {
        if(node==null)return RebirthProgressionNodeAccessState.Unknown;
        if(values.Mode==RebirthProgressionExplorerMode.LiveCharacter&&!values.DefinitionsCompatible)
        {
            output.Add(new RebirthProgressionRequirementOverlay(node.Id,"definitions",RebirthProgressionRequirementState.Unknown,0f,0f,0f,"Server/client Survivor definitions are incompatible; live availability is not authoritative."));
            return RebirthProgressionNodeAccessState.Unknown;
        }
        if(node.Type==RebirthProgressionGraphNodeType.Discipline)
        {
            if(values.Mode==RebirthProgressionExplorerMode.LiveCharacter&&values.DisciplineIds.Contains(node.Id))
            { output.Add(new RebirthProgressionRequirementOverlay(node.Id,"discipline",RebirthProgressionRequirementState.Satisfied,1f,1f,0f,"Discipline acquired"));return RebirthProgressionNodeAccessState.Available; }
            AppendDisciplineRequirements(node.Id,values,output);
            if(values.Mode==RebirthProgressionExplorerMode.Neutral)return RebirthProgressionNodeAccessState.Informational;
            if(!RebirthAdvancedDisciplineRegistry.GrantsEnabled){output.Add(new RebirthProgressionRequirementOverlay(node.Id,"grant-policy",RebirthProgressionRequirementState.Blocked,0f,1f,0f,"Advanced Discipline acquisition is disabled by current authority."));return RebirthProgressionNodeAccessState.Locked;}
            bool disciplineBlocked=false,disciplineUnknown=false;for(int ri=0;ri<output.Count;ri++){if(output[ri].State==RebirthProgressionRequirementState.Blocked)disciplineBlocked=true;else if(output[ri].State==RebirthProgressionRequirementState.Unknown)disciplineUnknown=true;}
            if(disciplineBlocked)return RebirthProgressionNodeAccessState.Locked;if(disciplineUnknown)return RebirthProgressionNodeAccessState.Unknown;return RebirthProgressionNodeAccessState.Available;
        }
        if(node.Type==RebirthProgressionGraphNodeType.Action&&string.Equals(node.Category,"discipline initiation",StringComparison.OrdinalIgnoreCase)&&!RebirthAdvancedDisciplineRegistry.GrantsEnabled)
            return values.Mode==RebirthProgressionExplorerMode.Neutral?RebirthProgressionNodeAccessState.Informational:RebirthProgressionNodeAccessState.Locked;
        if(node.Type!=RebirthProgressionGraphNodeType.Recipe&&node.Type!=RebirthProgressionGraphNodeType.Action)return RebirthProgressionNodeAccessState.Informational;
        if(values.Mode==RebirthProgressionExplorerMode.Neutral||!values.HasCharacterState)
        {
            AppendGraphRequirements(node.Id,values,output,true);
            return output.Count>0?RebirthProgressionNodeAccessState.Unknown:RebirthProgressionNodeAccessState.Available;
        }

        RebirthCapabilityDefinition capability;
        if(node.Type==RebirthProgressionGraphNodeType.Recipe&&RebirthCapabilityRegistry.TryGetRecipe(StripRecipePrefix(node.Id),out capability)&&capability!=null)
        {
            bool allowed=EvaluateCapabilityRequirement(capability.Requirement,values,output);
            bool recommendations=HasRecommendations(output);
            return allowed?(recommendations?RebirthProgressionNodeAccessState.AvailableWithRecommendations:RebirthProgressionNodeAccessState.Available):RebirthProgressionNodeAccessState.Locked;
        }


        AppendGraphRequirements(node.Id,values,output,false);
        bool blocked=false,unknown=false,recommended=false;
        for(int i=0;i<output.Count;i++)
        {
            if(output[i].State==RebirthProgressionRequirementState.Blocked)blocked=true;
            else if(output[i].State==RebirthProgressionRequirementState.Unknown)unknown=true;
            else if(output[i].State==RebirthProgressionRequirementState.RecommendationOnly)recommended=true;
        }
        if(blocked)return RebirthProgressionNodeAccessState.Locked;
        if(unknown)return RebirthProgressionNodeAccessState.Unknown;
        return recommended?RebirthProgressionNodeAccessState.AvailableWithRecommendations:RebirthProgressionNodeAccessState.Available;
    }

    private static bool EvaluateCapabilityRequirement(RebirthCapabilityRequirement requirement,RebirthProgressionOverlayValues values,List<RebirthProgressionRequirementOverlay> output)
    {
        if(requirement==null)return true;
        if(requirement.Kind==RebirthCapabilityKinds.All)
        {
            bool allowed=true;for(int i=0;i<requirement.Children.Count;i++)if(!EvaluateCapabilityRequirement(requirement.Children[i],values,output))allowed=false;return allowed;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Any)
        {
            if(requirement.Children.Count==0)return false;
            List<RebirthProgressionRequirementOverlay> best=null;int bestBlocked=int.MaxValue;float bestGap=float.MaxValue;
            for(int i=0;i<requirement.Children.Count;i++)
            {
                List<RebirthProgressionRequirementOverlay> branch=new List<RebirthProgressionRequirementOverlay>();
                bool allowed=EvaluateCapabilityRequirement(requirement.Children[i],values,branch);
                if(allowed){output.AddRange(branch);return true;}
                int blocked=0;float gap=0f;
                for(int j=0;j<branch.Count;j++)if(branch[j].State==RebirthProgressionRequirementState.Blocked){blocked++;gap+=Math.Max(0f,branch[j].RequiredValue-branch[j].CurrentValue);}
                if(best==null||blocked<bestBlocked||(blocked==bestBlocked&&gap<bestGap)){best=branch;bestBlocked=blocked;bestGap=gap;}
            }
            if(best!=null)output.AddRange(best);return false;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Knowledge)
        {
            bool has=values.KnowledgeIds.Contains(requirement.Id);
            output.Add(new RebirthProgressionRequirementOverlay(requirement.Id,requirement.Kind,has?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked,has?1f:0f,1f,0f,has?"Knowledge owned":"Missing Knowledge"));
            return has;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Skill)
        {
            float current=0f;bool has=values.Skills.TryGetValue(requirement.Id,out current)||values.HasCharacterState;
            if(!has){output.Add(new RebirthProgressionRequirementOverlay(requirement.Id,requirement.Kind,RebirthProgressionRequirementState.Unknown,0f,requirement.HasMinimum?requirement.Minimum:0f,requirement.HasRecommended?requirement.Recommended:0f,"Skill state unavailable"));return !requirement.HasMinimum;}
            bool allowed=!requirement.HasMinimum||current+0.0001f>=requirement.Minimum;
            output.Add(new RebirthProgressionRequirementOverlay(requirement.Id,requirement.Kind,allowed?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked,current,requirement.HasMinimum?requirement.Minimum:0f,requirement.HasRecommended?requirement.Recommended:0f,allowed?"Skill requirement met":"Skill below required value"));
            if(allowed&&requirement.HasRecommended&&current+0.0001f<requirement.Recommended)output.Add(new RebirthProgressionRequirementOverlay(requirement.Id,requirement.Kind,RebirthProgressionRequirementState.RecommendationOnly,current,requirement.HasMinimum?requirement.Minimum:0f,requirement.Recommended,"Below recommended Skill"));
            return allowed;
        }

        output.Add(new RebirthProgressionRequirementOverlay(requirement.Id,requirement.Kind,RebirthProgressionRequirementState.Unknown,0f,0f,0f,"Unsupported requirement kind"));return false;
    }

    private static void AppendGraphRequirements(string targetId,RebirthProgressionOverlayValues values,List<RebirthProgressionRequirementOverlay> output,bool forceUnknown)
    {
        RebirthProgressionGraphEdge[] incoming=RebirthProgressionGraphRegistry.GetIncoming(targetId);
        for(int i=0;i<incoming.Length;i++)
        {
            RebirthProgressionGraphEdge e=incoming[i];
            if(e.Type==RebirthProgressionGraphEdgeType.RequiresKnowledge)
            {
                bool has=values.KnowledgeIds.Contains(e.FromId);RebirthProgressionRequirementState state=forceUnknown?RebirthProgressionRequirementState.Unknown:(has?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked);
                output.Add(new RebirthProgressionRequirementOverlay(e.FromId,"knowledge",state,has?1f:0f,1f,0f,has?"Knowledge owned":"Missing Knowledge"));
            }
            else if(e.Type==RebirthProgressionGraphEdgeType.RequiresSkill)
            {
                float current=0f;values.Skills.TryGetValue(e.FromId,out current);RebirthProgressionRequirementState state=forceUnknown?RebirthProgressionRequirementState.Unknown:(!e.HasMinimum||current+0.0001f>=e.Minimum?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked);
                output.Add(new RebirthProgressionRequirementOverlay(e.FromId,"skill",state,current,e.HasMinimum?e.Minimum:0f,e.HasRecommended?e.Recommended:0f,state==RebirthProgressionRequirementState.Blocked?"Skill below required value":"Skill requirement"));
            }
            else if(e.Type==RebirthProgressionGraphEdgeType.RequiresDiscipline)
            { bool has=values.DisciplineIds.Contains(e.FromId);RebirthProgressionRequirementState state=forceUnknown?RebirthProgressionRequirementState.Unknown:(has?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked);output.Add(new RebirthProgressionRequirementOverlay(e.FromId,"discipline",state,has?1f:0f,1f,0f,has?"Discipline acquired":"Missing Discipline")); }
        }
    }


    private static void AppendDisciplineRequirements(string disciplineId,RebirthProgressionOverlayValues values,List<RebirthProgressionRequirementOverlay> output)
    {
        RebirthAdvancedDisciplineDefinition d;if(!RebirthAdvancedDisciplineRegistry.TryGetDefinition(disciplineId,out d)||d==null)return;
        for(int i=0;i<d.Requirements.Count;i++)
        {
            RebirthAdvancedDisciplineRequirement r=d.Requirements[i];if(r==null)continue;
            if(r.Planned){output.Add(new RebirthProgressionRequirementOverlay(r.Id,r.Kind,RebirthProgressionRequirementState.Unknown,0f,r.HasMinimum?r.Minimum:0f,0f,"Planned prerequisite; owning implementation chunk not installed yet"));continue;}
            if(string.Equals(r.Kind,"skill",StringComparison.OrdinalIgnoreCase)){float current=0f;values.Skills.TryGetValue(r.Id,out current);bool ok=!r.HasMinimum||current+0.0001f>=r.Minimum;output.Add(new RebirthProgressionRequirementOverlay(r.Id,r.Kind,ok?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked,current,r.HasMinimum?r.Minimum:0f,0f,ok?"Skill requirement met":"Skill below required value"));}
            else if(string.Equals(r.Kind,"melee_breadth",StringComparison.OrdinalIgnoreCase)||string.Equals(r.Kind,"melee_depth",StringComparison.OrdinalIgnoreCase))
            {
                int count=0;string[] melee=RebirthWeaponFamilySkillService.GetMeleeSkillIds();float threshold=string.Equals(r.Kind,"melee_depth",StringComparison.OrdinalIgnoreCase)?RebirthRageService.DepthLevel:RebirthRageService.BreadthLevel;
                for(int m=0;m<melee.Length;m++){float current=0f;if(values.Skills.TryGetValue(melee[m],out current)&&current+0.0001f>=threshold)count++;}
                bool ok=count+0.0001f>=r.Minimum;output.Add(new RebirthProgressionRequirementOverlay(r.Id,r.Kind,ok?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked,count,r.Minimum,0f,ok?"Melee mastery prerequisite met":"Melee mastery prerequisite not met"));
            }
            else if(string.Equals(r.Kind,"knowledge",StringComparison.OrdinalIgnoreCase)){bool has=values.KnowledgeIds.Contains(r.Id);output.Add(new RebirthProgressionRequirementOverlay(r.Id,r.Kind,has?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked,has?1f:0f,1f,0f,has?"Knowledge owned":"Missing Knowledge"));}
            else if(string.Equals(r.Kind,"discipline",StringComparison.OrdinalIgnoreCase)){bool has=values.DisciplineIds.Contains(r.Id);output.Add(new RebirthProgressionRequirementOverlay(r.Id,r.Kind,has?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked,has?1f:0f,1f,0f,has?"Discipline acquired":"Missing Discipline"));}
            else if(string.Equals(r.Kind,"accomplishment",StringComparison.OrdinalIgnoreCase)){bool has=values.AccomplishmentIds.Contains(r.Id);output.Add(new RebirthProgressionRequirementOverlay(r.Id,r.Kind,has?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked,has?1f:0f,1f,0f,has?"Accomplishment completed":"Accomplishment not completed"));}
            else if(string.Equals(r.Kind,"trial",StringComparison.OrdinalIgnoreCase)){bool has=values.CompletedTrialIds.Contains(r.Id);output.Add(new RebirthProgressionRequirementOverlay(r.Id,r.Kind,has?RebirthProgressionRequirementState.Satisfied:RebirthProgressionRequirementState.Blocked,has?1f:0f,1f,0f,has?"Initiation trial completed":"Initiation trial not completed"));}
            else output.Add(new RebirthProgressionRequirementOverlay(r.Id,r.Kind,RebirthProgressionRequirementState.Unknown,0f,r.HasMinimum?r.Minimum:1f,0f,"Unsupported runtime prerequisite"));
        }
    }

    private static bool HasRecommendations(List<RebirthProgressionRequirementOverlay> values){for(int i=0;i<values.Count;i++)if(values[i].State==RebirthProgressionRequirementState.RecommendationOnly)return true;return false;}
    private static string StripRecipePrefix(string id){return id!=null&&id.StartsWith("recipe.",StringComparison.OrdinalIgnoreCase)?id.Substring(7):id??string.Empty;}
}
