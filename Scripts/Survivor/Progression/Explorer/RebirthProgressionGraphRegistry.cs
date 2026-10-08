using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

#nullable disable

public static class RebirthProgressionGraphRegistry
{
    private static readonly Dictionary<string,RebirthProgressionGraphNode> Nodes=new Dictionary<string,RebirthProgressionGraphNode>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<RebirthProgressionGraphEdge> Edges=new List<RebirthProgressionGraphEdge>();
    private static readonly Dictionary<string,List<RebirthProgressionGraphEdge>> Outgoing=new Dictionary<string,List<RebirthProgressionGraphEdge>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,List<RebirthProgressionGraphEdge>> Incoming=new Dictionary<string,List<RebirthProgressionGraphEdge>>(StringComparer.OrdinalIgnoreCase);
    private static bool ready;
    private static string semanticHash=string.Empty;

    public static bool IsReady { get { return ready; } }
    public static int NodeCount { get { return Nodes.Count; } }
    public static int EdgeCount { get { return Edges.Count; } }
    public static string SemanticHash { get { return semanticHash; } }

    public static string BuildFromCurrentAuthority()
    {
        Clear();
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle==null || bundle.Progression==null) throw new InvalidOperationException("Survivor definitions must be installed before the progression graph is built.");
        if(!RebirthCapabilityRegistry.IsReady) throw new InvalidOperationException("Capability registry must be ready before the progression graph is built.");
        if(!RebirthCraftingProgressionRegistry.IsReady) throw new InvalidOperationException("Crafting progression registry must be ready before the progression graph is built.");
        if(!RebirthAdvancedDisciplineRegistry.IsReady) throw new InvalidOperationException("Advanced Discipline registry must be ready before the progression graph is built.");

        AddDefinitionNodes(bundle);
        AddKnowledgeAssociationEdges(bundle);
        AddBackgroundEdges(bundle);
        AddTrainingActionNodes(bundle);
        AddAdvancedDisciplineNodesAndEdges();
        AddAdvancedDisciplineExplorerPaths();
        AddCraftingProgressionPolicyNodesAndEdges();
        AddRecipeKnowledgeEdges();
        AddCapabilityEdges();
        BuildIndexes();

        RebirthProgressionGraphValidationReport report=RebirthProgressionGraphValidator.ValidateCurrent();
        if(!report.IsValid)
            Log.Error("[REBIRTH ProgressionExplorer] graph authoring validation reported errors; keeping the built graph available for Explorer diagnostics/navigation. "+report.BuildSummary());
        semanticHash=ComputeSemanticHash();
        ready=true;
        return "progressionGraph nodes="+NodeCount+" edges="+EdgeCount+" warnings="+report.WarningCount+" hash="+ShortHash(semanticHash);
    }

    public static void Clear()
    {
        ready=false; semanticHash=string.Empty; Nodes.Clear(); Edges.Clear(); Outgoing.Clear(); Incoming.Clear();
    }

    public static bool TryGetNode(string id,out RebirthProgressionGraphNode node)
    { return Nodes.TryGetValue(id??string.Empty,out node); }

    public static RebirthProgressionGraphNode[] GetNodesSnapshot()
    {
        List<RebirthProgressionGraphNode> values=new List<RebirthProgressionGraphNode>(Nodes.Values);
        values.Sort(delegate(RebirthProgressionGraphNode a,RebirthProgressionGraphNode b){return string.Compare(a.Id,b.Id,StringComparison.OrdinalIgnoreCase);});
        return values.ToArray();
    }

    public static RebirthProgressionGraphEdge[] GetEdgesSnapshot()
    { return Edges.ToArray(); }

    public static RebirthProgressionGraphEdge[] GetOutgoing(string id)
    { return Snapshot(Outgoing,id); }

    public static RebirthProgressionGraphEdge[] GetIncoming(string id)
    { return Snapshot(Incoming,id); }

    public static RebirthProgressionGraphSnapshot GetSnapshot()
    { return new RebirthProgressionGraphSnapshot(GetNodesSnapshot(),GetEdgesSnapshot(),semanticHash); }

    private static RebirthProgressionGraphEdge[] Snapshot(Dictionary<string,List<RebirthProgressionGraphEdge>> source,string id)
    {
        List<RebirthProgressionGraphEdge> values;
        if(!source.TryGetValue(id??string.Empty,out values)||values==null)return new RebirthProgressionGraphEdge[0];
        return values.ToArray();
    }

    private static void AddDefinitionNodes(RebirthSurvivorDefinitionBundle bundle)
    {
        foreach(RebirthSkillDefinition d in bundle.Progression.Skills)
            AddNode(new RebirthProgressionGraphNode(d.Id,RebirthProgressionGraphNodeType.Skill,d.NameKey,string.Empty,d.LearnByDoingSource,"skill",d.Feasibility));
        foreach(RebirthKnowledgeDefinition d in bundle.Progression.Knowledge)
            if(!RebirthLiteratureService.IsInternalReadMarker(d.Id))
                AddNode(new RebirthProgressionGraphNode(d.Id,RebirthProgressionGraphNodeType.Knowledge,d.NameKey,string.Empty,d.ExplorerStatus,"knowledge","progression.xml"));
        foreach(RebirthBackgroundDefinition d in bundle.Backgrounds)
            AddNode(new RebirthProgressionGraphNode(d.Id,RebirthProgressionGraphNodeType.Background,d.NameKey,string.Empty,BuildBackgroundDescription(d),"background",d.StartingExperience));
    }


    public static string BuildBackgroundDescription(RebirthBackgroundDefinition background)
    {
        if(background==null)return string.Empty;
        StringBuilder b=new StringBuilder();
        if(!string.IsNullOrEmpty(background.Identity))b.Append(background.Identity);
        if(b.Length>0)b.Append("\n\n");
        b.Append(RebirthSurvivorUiText.L("xuiRebirthSignatureBonus","Signature Bonus")).Append(": ")
            .Append(RebirthSurvivorUiText.SignatureBonusName(background.Id)).Append("\n")
            .Append(RebirthSurvivorUiText.SignatureBonusDescription(background.Id));
        b.Append("\n\n").Append(RebirthSurvivorUiText.L("xuiRebirthBackgroundBonusNotPrerequisite","Signature Bonuses modify related gameplay but do not make normal Skills or Knowledge background prerequisites."));
        return b.ToString();
    }


    private static void AddKnowledgeAssociationEdges(RebirthSurvivorDefinitionBundle bundle)
    {
        foreach(RebirthKnowledgeDefinition knowledge in bundle.Progression.Knowledge)
        {
            if(RebirthLiteratureService.IsInternalReadMarker(knowledge.Id))continue;
            foreach(string skillId in knowledge.AssociatedSkillIds)
                AddEdge(new RebirthProgressionGraphEdge(knowledge.Id,skillId,RebirthProgressionGraphEdgeType.RelatedTo,0f,false,0f,false,"progression.xml","associated Skill; informational relationship, not itself a gate"));
        }
    }

    private static void AddBackgroundEdges(RebirthSurvivorDefinitionBundle bundle)
    {
        foreach(RebirthBackgroundDefinition background in bundle.Backgrounds)
        {
            foreach(RebirthStartingSkillBiasDefinition skill in background.StartingSkills)
                AddEdge(new RebirthProgressionGraphEdge(background.Id,skill.SkillId,RebirthProgressionGraphEdgeType.GrantedByBackground,skill.Value,skill.HasExplicitValue,0f,false,"backgrounds.xml","starting Skill"));
            foreach(string knowledgeId in background.StartingKnowledgeIds)
                AddEdge(new RebirthProgressionGraphEdge(background.Id,knowledgeId,RebirthProgressionGraphEdgeType.GrantedByBackground,0f,false,0f,false,"backgrounds.xml","starting Knowledge"));
        }
    }

    private static void AddTrainingActionNodes(RebirthSurvivorDefinitionBundle bundle)
    {
        foreach(RebirthSkillDefinition skill in bundle.Progression.Skills)
        {
            string actionId="action.train."+StripPrefix(skill.Id,"skill.");
            AddNode(new RebirthProgressionGraphNode(actionId,RebirthProgressionGraphNodeType.Action,string.Empty,"Training: "+ResolveName(skill.NameKey,skill.Id),skill.LearnByDoingSource,"training",skill.Feasibility));
            AddEdge(new RebirthProgressionGraphEdge(actionId,skill.Id,RebirthProgressionGraphEdgeType.TrainsSkill,0f,false,0f,false,"progression.xml",skill.LearnByDoingSource));
        }
    }

    private static void AddAdvancedDisciplineNodesAndEdges()
    {
        RebirthAdvancedDisciplineDefinition[] disciplines=RebirthAdvancedDisciplineRegistry.GetDefinitionsSnapshot();
        // Pass 1 publishes every discipline/milestone/initiation node. Edges are emitted only
        // after the complete node set exists so forward discipline references are order-invariant.
        for(int i=0;i<disciplines.Length;i++)
        {
            RebirthAdvancedDisciplineDefinition d=disciplines[i];if(d==null)continue;
            AddNode(new RebirthProgressionGraphNode(d.Id,RebirthProgressionGraphNodeType.Discipline,d.NameKey,string.Empty,d.DescriptionKey,"advanced discipline","advanced_disciplines.xml"));
            for(int rr=0;rr<d.Requirements.Count;rr++)
            {
                RebirthAdvancedDisciplineRequirement ar=d.Requirements[rr];if(ar==null)continue;
                if(string.Equals(ar.Kind,"trial",StringComparison.OrdinalIgnoreCase)||string.Equals(ar.Kind,"accomplishment",StringComparison.OrdinalIgnoreCase))
                    AddNode(new RebirthProgressionGraphNode(ar.Id,RebirthProgressionGraphNodeType.Action,string.Empty,FriendlyMilestoneName(ar.Id),"Server-authoritative Advanced Discipline milestone.","discipline milestone","advanced_disciplines.xml"));
            }
            if(!string.IsNullOrEmpty(d.InitiationActionId))
                AddNode(new RebirthProgressionGraphNode(d.InitiationActionId,RebirthProgressionGraphNodeType.Action,"xuiRebirthDisciplineInitiationAction",string.Empty,"xuiRebirthDisciplineInitiationActionDesc","discipline initiation","advanced_disciplines.xml"));
        }
        // Pass 2 emits relationships against the complete canonical node set.
        for(int i=0;i<disciplines.Length;i++)
        {
            RebirthAdvancedDisciplineDefinition d=disciplines[i];if(d==null)continue;
            if(!string.IsNullOrEmpty(d.InitiationActionId))
                AddEdge(new RebirthProgressionGraphEdge(d.InitiationActionId,d.Id,RebirthProgressionGraphEdgeType.UnlocksDiscipline,0f,false,0f,false,"advanced_disciplines.xml","server-authoritative initiation route"));
            for(int j=0;j<d.RelatedNodeIds.Count;j++)
            {
                RebirthProgressionGraphNode related;
                if(TryGetNode(d.RelatedNodeIds[j],out related)&&related!=null)
                    AddEdge(new RebirthProgressionGraphEdge(d.RelatedNodeIds[j],d.Id,RebirthProgressionGraphEdgeType.RelatedTo,0f,false,0f,false,"advanced_disciplines.xml","discipline domain relationship; informational"));
            }
            for(int j=0;j<d.Requirements.Count;j++)
            {
                RebirthAdvancedDisciplineRequirement r=d.Requirements[j];if(r==null||r.Planned)continue;
                RebirthProgressionGraphNode source;if(!TryGetNode(r.Id,out source)||source==null)continue;
                if(string.Equals(r.Kind,"discipline",StringComparison.OrdinalIgnoreCase))
                    AddEdge(new RebirthProgressionGraphEdge(r.Id,d.Id,RebirthProgressionGraphEdgeType.RequiresDiscipline,r.Minimum,r.HasMinimum,0f,false,"advanced_disciplines.xml","active discipline prerequisite"));
                else if(string.Equals(r.Kind,"skill",StringComparison.OrdinalIgnoreCase))
                    AddEdge(new RebirthProgressionGraphEdge(r.Id,d.Id,RebirthProgressionGraphEdgeType.RequiresSkill,r.Minimum,r.HasMinimum,0f,false,"advanced_disciplines.xml","active Skill prerequisite"));
                else if(string.Equals(r.Kind,"knowledge",StringComparison.OrdinalIgnoreCase))
                    AddEdge(new RebirthProgressionGraphEdge(r.Id,d.Id,RebirthProgressionGraphEdgeType.RequiresKnowledge,0f,false,0f,false,"advanced_disciplines.xml","active Knowledge prerequisite"));
            }
        }
    }


    private static string FriendlyMilestoneName(string id)
    {
        string v=id??string.Empty;int dot=v.LastIndexOf('.');if(dot>=0&&dot+1<v.Length)v=v.Substring(dot+1);v=v.Replace('_',' ').Replace('-',' ');if(v.Length==0)return id??string.Empty;return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(v);
    }

    private static void AddAdvancedDisciplineExplorerPaths()
    {
        System.Xml.Linq.XDocument doc=System.Xml.Linq.XDocument.Load(RebirthAdvancedDisciplineRegistry.SourcePath);
        System.Xml.Linq.XElement paths=doc.Root==null?null:doc.Root.Element("explorer_paths");
        if(paths==null)return;
        foreach(System.Xml.Linq.XElement e in paths.Elements("path"))
        {
            string action=((string)e.Attribute("action")??string.Empty).Trim();if(action.Length==0)continue;
            string nameKey=((string)e.Attribute("name_key")??string.Empty).Trim();string descKey=((string)e.Attribute("description_key")??string.Empty).Trim();string unavailable=((string)e.Attribute("unavailable_reason")??string.Empty).Trim();string description=descKey;if(unavailable.Length>0)description=descKey;
            AddNode(new RebirthProgressionGraphNode(action,RebirthProgressionGraphNodeType.Action,nameKey,string.Empty,description,"advanced discipline technique","advanced_disciplines.xml"));
            string discipline=((string)e.Attribute("discipline")??string.Empty).Trim();if(discipline.Length>0)AddEdge(new RebirthProgressionGraphEdge(discipline,action,RebirthProgressionGraphEdgeType.RequiresDiscipline,0f,false,0f,false,"advanced_disciplines.xml","Advanced Discipline required"));
            AddPathSkill(e,"skill","skill_minimum",action);AddPathSkill(e,"secondary_skill","secondary_skill_minimum",action);
            AddPathKnowledge(e,"knowledge",action);AddPathKnowledge(e,"secondary_knowledge",action);
            string relatedRecipe=((string)e.Attribute("related_recipe")??string.Empty).Trim();
            if(relatedRecipe.Length>0)
            {
                string recipeNode=RecipeNodeId(relatedRecipe);
                AddNode(new RebirthProgressionGraphNode(recipeNode,RebirthProgressionGraphNodeType.Recipe,string.Empty,relatedRecipe,BuildRecipeDescription(relatedRecipe,string.Empty,RebirthServiceCraftSkillService.ClassifyRecipe(relatedRecipe)),"advanced discipline outcome","advanced_disciplines.xml"));
                AddEdge(new RebirthProgressionGraphEdge(action,recipeNode,RebirthProgressionGraphEdgeType.RelatedTo,0f,false,0f,false,"advanced_disciplines.xml","Related authored item/recipe; use requirements remain independently authoritative"));
            }
        }
    }

    private static void AddPathSkill(System.Xml.Linq.XElement e,string attr,string minimumAttr,string action)
    {
        string id=((string)e.Attribute(attr)??string.Empty).Trim();if(id.Length==0)return;float minimum=0f;bool has=float.TryParse(((string)e.Attribute(minimumAttr)??string.Empty).Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out minimum);
        RebirthProgressionGraphNode n;if(!TryGetNode(id,out n)||n==null)return;AddEdge(new RebirthProgressionGraphEdge(id,action,RebirthProgressionGraphEdgeType.RequiresSkill,minimum,has,0f,false,"advanced_disciplines.xml","Technique Skill requirement"));
    }

    private static void AddPathKnowledge(System.Xml.Linq.XElement e,string attr,string action)
    {
        string id=((string)e.Attribute(attr)??string.Empty).Trim();if(id.Length==0)return;RebirthProgressionGraphNode n;if(!TryGetNode(id,out n)||n==null)return;AddEdge(new RebirthProgressionGraphEdge(id,action,RebirthProgressionGraphEdgeType.RequiresKnowledge,0f,false,0f,false,"advanced_disciplines.xml","Technique Knowledge requirement"));
    }

    private static void AddCraftingProgressionPolicyNodesAndEdges()
    {
        RebirthCraftingProgressionDefinition[] policies=RebirthCraftingProgressionRegistry.GetSnapshot();
        for(int i=0;i<policies.Length;i++)
        {
            RebirthCraftingProgressionDefinition policy=policies[i];
            if(policy==null)continue;
            string recipeId=RecipeNodeId(policy.RecipeId);
            AddNode(new RebirthProgressionGraphNode(recipeId,RebirthProgressionGraphNodeType.Recipe,string.Empty,policy.RecipeId,BuildRecipeDescription(policy.RecipeId,string.Empty,policy.PrimarySkillId),policy.Family,"crafting_progression.xml"));
            if(string.IsNullOrEmpty(policy.PrimarySkillId))continue;
            AddEdge(new RebirthProgressionGraphEdge(recipeId,policy.PrimarySkillId,RebirthProgressionGraphEdgeType.RelatedTo,0f,false,0f,false,"crafting_progression.xml","primary crafting Skill / fabrication owner"));
            if(!policy.IsDisabled && RebirthServiceCraftSkillService.GetCraftAward(policy.PrimarySkillId,1)>0f)
                AddEdge(new RebirthProgressionGraphEdge(recipeId,policy.PrimarySkillId,RebirthProgressionGraphEdgeType.TrainsSkill,0f,false,0f,false,"crafting_progression.xml","completed qualified craft awards the primary crafting Skill"));
        }
    }

    private static void AddRecipeKnowledgeEdges()
    {
        RebirthRecipeKnowledgeRule[] rules=RebirthProgressionRuntimeConfig.GetRecipeRulesSnapshot();
        for(int i=0;i<rules.Length;i++)
        {
            RebirthRecipeKnowledgeRule rule=rules[i];
            string recipeId=RecipeNodeId(rule.RecipeName);
            string skillId=RebirthServiceCraftSkillService.ClassifyRecipe(rule.RecipeName);
            AddNode(new RebirthProgressionGraphNode(recipeId,RebirthProgressionGraphNodeType.Recipe,string.Empty,rule.RecipeName,BuildRecipeDescription(rule.RecipeName,rule.KnowledgeId,skillId),rule.Category,"recipe_knowledge.xml"));
            // Recipe Knowledge rules are adapted into Capability definitions by RebirthCapabilityRegistry.
            // AddCapabilityEdges() is therefore the single authority for the RequiresKnowledge edge.
            // Do not also add a second legacy requirement/unlock edge here.
        }
    }

    private static void AddCapabilityEdges()
    {
        RebirthCapabilityDefinition[] values=RebirthCapabilityRegistry.GetCapabilitiesSnapshot();
        for(int i=0;i<values.Length;i++)
        {
            RebirthCapabilityDefinition capability=values[i];
            string targetId=ResolveCapabilityTargetNode(capability);
            if(targetId.Length==0)continue;
            AddRequirementEdges(capability.Requirement,targetId,"capabilities.xml:"+capability.Id,"root",string.Empty);
        }
    }

    private static void AddRequirementEdges(RebirthCapabilityRequirement requirement,string targetId,string source,string path,string parentMode)
    {
        if(requirement==null)return;
        if(requirement.Kind==RebirthCapabilityKinds.All || requirement.Kind==RebirthCapabilityKinds.Any)
        {
            string mode=requirement.Kind==RebirthCapabilityKinds.Any?"any":"all";
            for(int i=0;i<requirement.Children.Count;i++) AddRequirementEdges(requirement.Children[i],targetId,source,path+"/"+mode+"["+i+"]",mode);
            return;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Knowledge)
        {
            AddEdge(new RebirthProgressionGraphEdge(requirement.Id,targetId,RebirthProgressionGraphEdgeType.RequiresKnowledge,requirement.Minimum,requirement.HasMinimum,requirement.Recommended,requirement.HasRecommended,source,"capability requirement",path,parentMode));
        }
        else if(requirement.Kind==RebirthCapabilityKinds.Discipline)
            AddEdge(new RebirthProgressionGraphEdge(requirement.Id,targetId,RebirthProgressionGraphEdgeType.RequiresDiscipline,0f,false,0f,false,source,"capability discipline prerequisite",path,parentMode));
        else if(requirement.Kind==RebirthCapabilityKinds.Skill)
            AddEdge(new RebirthProgressionGraphEdge(requirement.Id,targetId,RebirthProgressionGraphEdgeType.RequiresSkill,requirement.Minimum,requirement.HasMinimum,requirement.Recommended,requirement.HasRecommended,source,"capability Skill threshold",path,parentMode));

    }

    private static string ResolveCapabilityTargetNode(RebirthCapabilityDefinition capability)
    {
        if(capability==null)return string.Empty;
        if(capability.TargetType=="recipe")
        {
            string id=RecipeNodeId(capability.TargetId);
            AddNode(new RebirthProgressionGraphNode(id,RebirthProgressionGraphNodeType.Recipe,string.Empty,capability.TargetId,BuildRecipeDescription(capability.TargetId,string.Empty,RebirthServiceCraftSkillService.ClassifyRecipe(capability.TargetId)),capability.Category,"capabilities.xml"));
            return id;
        }
        return string.Empty;
    }

    private static string BuildRecipeDescription(string recipeName,string knowledgeId,string skillId)
    {
        StringBuilder b=new StringBuilder();
        string itemDescription=RebirthRecipeDescriptionText.Get(recipeName);
        if(!string.IsNullOrWhiteSpace(itemDescription)) b.Append(itemDescription).Append("  ");
        RebirthCraftingProgressionDefinition policy;
        if(RebirthCraftingProgressionRegistry.TryGetRecipe(recipeName,out policy)&&policy!=null)
        {
            if(policy.IsDisabled) b.Append(Localization.Get("xuiRebirthRecipeCraftingDisabled"));
            else if(policy.IsUniversal) b.Append(Localization.Get("xuiRebirthRecipeNoDiscoveryNeeded"));
            else if(policy.HasPlannedCapability) b.Append(Localization.Get("xuiRebirthRecipeCheckRequirements"));
            else b.Append(Localization.Get("xuiRebirthRecipeLearnRequirements"));
            if(policy.KnowledgeIds.Count>0)
            {
                b.Append("  "+Localization.Get("xuiRebirthRecipeLearnLabel")+" ");
                for(int i=0;i<policy.KnowledgeIds.Count;i++)
                {
                    if(i>0)b.Append(", ");
                    b.Append(ResolveGraphName(policy.KnowledgeIds[i]));
                }
                b.Append('.');
            }
        }
        else if(!string.IsNullOrEmpty(knowledgeId))
            b.Append(Localization.Get("xuiRebirthRecipeLearnFirstLabel")+" ").Append(ResolveGraphName(knowledgeId)).Append('.');
        else b.Append(Localization.Get("xuiRebirthRecipeRequirementsBelow"));

        Recipe recipe=null;
        try { recipe=CraftingManager.GetRecipe(recipeName??string.Empty); } catch { }
        if(recipe!=null)
        {
            string area=(recipe.craftingArea??string.Empty).Trim();
            if(area.Length>0) b.Append("  "+Localization.Get("xuiRebirthRecipeStationLabel")+" ").Append(ResolveNativeName(area)).Append('.');
            string tool=TryReadRecipeTool(recipe);
            if(tool.Length>0) b.Append("  "+Localization.Get("xuiRebirthRecipeToolLabel")+" ").Append(ResolveNativeName(tool)).Append('.');
        }

        if(!string.IsNullOrEmpty(skillId))
        {
            b.Append("  "+Localization.Get("xuiRebirthRecipeSkillLabel")+" ").Append(ResolveGraphName(skillId)).Append('.');
            if((policy==null||!policy.IsDisabled)&&RebirthServiceCraftSkillService.GetCraftAward(skillId,1)>0f)
                b.Append("  ").Append(string.Format(Localization.Get("xuiRebirthRecipeTrainsSkill"),ResolveGraphName(skillId)));
        }
        return b.ToString();
    }

    private static string ResolveGraphName(string id)
    {
        RebirthSkillDefinition skill;
        if(RebirthSurvivorDefinitionRegistry.TryGetSkill(id??string.Empty,out skill)&&skill!=null)
            return RebirthSkillDisplayNames.Get(id);
        RebirthProgressionGraphNode node;
        if(Nodes.TryGetValue(id??string.Empty,out node)&&node!=null)
        {
            string key=node.NameKey??string.Empty;
            string localized=key.Length>0?Localization.Get(key):string.Empty;
            if(!string.IsNullOrWhiteSpace(localized)&&localized!=key)return localized;
            if(!string.IsNullOrWhiteSpace(node.DisplayName))return ResolveNativeName(node.DisplayName);
        }
        return ResolveNativeName(id);
    }

    private static string ResolveNativeName(string id)
    {
        string value=(id??string.Empty).Trim();
        if(value.Length==0)return string.Empty;
        string localized=Localization.Get(value);
        if(!string.IsNullOrWhiteSpace(localized)&&localized!=value)return localized;
        try
        {
            ItemValue itemValue=ItemClass.GetItem(value,false);
            if(itemValue!=null&&!itemValue.IsEmpty()&&itemValue.ItemClass!=null)
            {
                string itemName=itemValue.ItemClass.GetLocalizedItemName();
                if(!string.IsNullOrWhiteSpace(itemName)&&itemName!=value)return itemName;
            }
        }
        catch { }
        if(value.StartsWith("skill.",StringComparison.OrdinalIgnoreCase)) value=value.Substring(6);
        value=value.Replace('.',' ');
        StringBuilder b=new StringBuilder();
        for(int i=0;i<value.Length;i++)
        {
            char c=value[i];
            if(i>0&&char.IsUpper(c)&&!char.IsUpper(value[i-1]))b.Append(' ');
            b.Append(c=='_'?' ':c);
        }
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(b.ToString());
    }

    private static string TryReadRecipeTool(Recipe recipe)
    {
        if(recipe==null)return string.Empty;
        // Current native recipes store the required item's numeric type, not a tool-name string.
        if(recipe.craftingToolType>0)
        {
            try
            {
                ItemClass tool=ItemClass.GetForId(recipe.craftingToolType);
                if(tool!=null&&!string.IsNullOrWhiteSpace(tool.GetItemName()))return tool.GetItemName();
            }
            catch { }
        }
        string[] names={"craftTool","craftingTool","CraftTool","CraftingTool","craft_tool"};
        Type type=recipe.GetType();
        for(int i=0;i<names.Length;i++)
        {
            try
            {
                System.Reflection.FieldInfo field=type.GetField(names[i],System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
                object raw=field!=null?field.GetValue(recipe):null;
                if(raw==null)
                {
                    System.Reflection.PropertyInfo property=type.GetProperty(names[i],System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
                    if(property!=null&&property.CanRead)raw=property.GetValue(recipe,null);
                }
                if(raw==null)continue;
                string text=raw as string;
                if(!string.IsNullOrEmpty(text))return FirstCsvValue(text);
                System.Collections.IEnumerable enumerable=raw as System.Collections.IEnumerable;
                if(enumerable!=null)
                {
                    foreach(object item in enumerable)
                    {
                        if(item==null)continue;
                        string candidate=item.ToString();
                        if(!string.IsNullOrEmpty(candidate))return FirstCsvValue(candidate);
                    }
                }
            }
            catch { }
        }
        return string.Empty;
    }

    private static string FirstCsvValue(string value)
    {
        string text=(value??string.Empty).Trim();
        int comma=text.IndexOf(',');
        return comma>=0?text.Substring(0,comma).Trim():text;
    }

    private static void AddNode(RebirthProgressionGraphNode node)
    {
        if(node==null||string.IsNullOrEmpty(node.Id))return;
        RebirthProgressionGraphNode existing;
        if(Nodes.TryGetValue(node.Id,out existing))return;
        Nodes.Add(node.Id,node);
    }

    private static void AddEdge(RebirthProgressionGraphEdge edge)
    {
        if(edge==null||string.IsNullOrEmpty(edge.FromId)||string.IsNullOrEmpty(edge.ToId))return;
        for(int i=0;i<Edges.Count;i++)
        {
            RebirthProgressionGraphEdge x=Edges[i];
            if(x.Type==edge.Type && string.Equals(x.FromId,edge.FromId,StringComparison.OrdinalIgnoreCase) && string.Equals(x.ToId,edge.ToId,StringComparison.OrdinalIgnoreCase)
                && x.HasMinimum==edge.HasMinimum && (!x.HasMinimum||x.Minimum.Equals(edge.Minimum))
                && x.HasRecommended==edge.HasRecommended && (!x.HasRecommended||x.Recommended.Equals(edge.Recommended))
                && string.Equals(x.RequirementGroupPath,edge.RequirementGroupPath,StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.RequirementGroupMode,edge.RequirementGroupMode,StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Source,edge.Source,StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Note,edge.Note,StringComparison.Ordinal))return;
        }
        Edges.Add(edge);
    }

    private static void BuildIndexes()
    {
        for(int i=0;i<Edges.Count;i++)
        {
            RebirthProgressionGraphEdge edge=Edges[i];
            AddIndex(Outgoing,edge.FromId,edge); AddIndex(Incoming,edge.ToId,edge);
        }
    }

    private static void AddIndex(Dictionary<string,List<RebirthProgressionGraphEdge>> index,string key,RebirthProgressionGraphEdge edge)
    {
        List<RebirthProgressionGraphEdge> list;
        if(!index.TryGetValue(key,out list)){list=new List<RebirthProgressionGraphEdge>();index.Add(key,list);} list.Add(edge);
    }

    public static string RecipeNodeId(string recipeName){return "recipe."+(recipeName??string.Empty).Trim();}
    private static string StripPrefix(string value,string prefix){return value!=null&&value.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)?value.Substring(prefix.Length):value??string.Empty;}
    private static string SanitizeId(string value){return (value??string.Empty).Replace(' ','_').Replace(':','_').Replace('/','_');}
    private static string ResolveName(string key,string fallback){string s=string.IsNullOrEmpty(key)?string.Empty:Localization.Get(key);return string.IsNullOrEmpty(s)||s==key?fallback:s;}

    private static string ComputeSemanticHash()
    {
        StringBuilder b=new StringBuilder();
        RebirthProgressionGraphNode[] nodes=GetNodesSnapshot();
        for(int i=0;i<nodes.Length;i++) b.Append(nodes[i].Id).Append('|').Append(nodes[i].Type).Append('|').Append(nodes[i].Category).Append('\n');
        List<RebirthProgressionGraphEdge> edges=new List<RebirthProgressionGraphEdge>(Edges);
        edges.Sort(delegate(RebirthProgressionGraphEdge a,RebirthProgressionGraphEdge c){int x=string.Compare(a.FromId,c.FromId,StringComparison.OrdinalIgnoreCase);if(x!=0)return x;x=string.Compare(a.ToId,c.ToId,StringComparison.OrdinalIgnoreCase);if(x!=0)return x;return a.Type.CompareTo(c.Type);});
        for(int i=0;i<edges.Count;i++){RebirthProgressionGraphEdge e=edges[i];b.Append(e.FromId).Append('>').Append(e.ToId).Append('|').Append(e.Type).Append('|').Append(e.HasMinimum?e.Minimum.ToString("R",CultureInfo.InvariantCulture):"").Append('|').Append(e.HasRecommended?e.Recommended.ToString("R",CultureInfo.InvariantCulture):"").Append('|').Append(e.RequirementGroupPath).Append('|').Append(e.RequirementGroupMode).Append('\n');}
        using(SHA256 sha=SHA256.Create()){byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()));StringBuilder x=new StringBuilder(hash.Length*2);for(int i=0;i<hash.Length;i++)x.Append(hash[i].ToString("x2",CultureInfo.InvariantCulture));return x.ToString();}
    }
    private static string ShortHash(string hash){return string.IsNullOrEmpty(hash)?string.Empty:hash.Substring(0,Math.Min(12,hash.Length));}
}
