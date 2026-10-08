using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Authoritative REBIRTH recipe-policy registry.
/// Owned recipes come from crafting_progression.xml. Unknown recipes are compatibility recipes and
/// remain native-allowed; they are never silently promoted into REBIRTH progression ownership.
/// </summary>
public static class RebirthCraftingProgressionRegistry
{
    private static readonly Dictionary<string,RebirthCraftingProgressionDefinition> ByRecipe=
        new Dictionary<string,RebirthCraftingProgressionDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ExternalDiagnostics=
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly object ExternalSync=new object();
    private const int ExternalDiagnosticLimit=128;
    private static bool ready;
    private static string semanticHash=string.Empty;
    private static int gatedCount;
    private static int universalCount;
    private static int disabledCount;
    private static int liveCapabilityCount;
    private static int plannedCapabilityCount;

    public static bool IsReady { get { return ready; } }
    public static int RecipeCount { get { return ByRecipe.Count; } }
    public static int GatedCount { get { return gatedCount; } }
    public static int UniversalCount { get { return universalCount; } }
    public static int DisabledCount { get { return disabledCount; } }
    public static int LiveCapabilityCount { get { return liveCapabilityCount; } }
    public static int PlannedCapabilityCount { get { return plannedCapabilityCount; } }
    public static string SemanticHash { get { return semanticHash; } }

    public static string Load(string configRoot)
    {
        Clear();
        if(string.IsNullOrEmpty(configRoot)) throw new InvalidDataException("Crafting progression config root is empty");
        RebirthCraftTrainingRules.Load(configRoot);
        string path=Path.Combine(configRoot,"crafting_progression.xml");
        if(!File.Exists(path)) throw new FileNotFoundException("Missing crafting progression policy",path);
        XDocument doc=XDocument.Load(path,LoadOptions.None);
        XElement root=doc.Root;
        if(root==null || root.Name.LocalName!="crafting_progression") throw new InvalidDataException("crafting_progression.xml root must be <crafting_progression>");
        if(A(root,"schema_version")!="1") throw new InvalidDataException("crafting_progression.xml schema_version must be 1");

        XElement recipes=root.Element("recipes");
        if(recipes==null) throw new InvalidDataException("crafting_progression.xml requires <recipes>");
        foreach(XElement node in recipes.Elements("recipe")) Add(Parse(node));
        if(ByRecipe.Count==0) throw new InvalidDataException("crafting_progression.xml contains no recipe policies");

        semanticHash=ComputeSemanticHash();
        ready=true;
        return "craftingPolicy="+RecipeCount
            +" gated="+GatedCount
            +" universal="+UniversalCount
            +" disabled="+DisabledCount
            +" liveCapabilities="+LiveCapabilityCount
            +" plannedCapabilities="+PlannedCapabilityCount
            +" policyHash="+ShortHash(semanticHash);
    }

    public static void Clear()
    {
        ready=false; semanticHash=string.Empty;
        gatedCount=universalCount=disabledCount=liveCapabilityCount=plannedCapabilityCount=0;
        ByRecipe.Clear();
        lock(ExternalSync) ExternalDiagnostics.Clear();
    }

    public static bool TryGetRecipe(string recipeName,out RebirthCraftingProgressionDefinition definition)
    { return ByRecipe.TryGetValue((recipeName??string.Empty).Trim(),out definition); }

    public static bool TryGetPrimarySkill(string recipeName,out string skillId)
    {
        skillId=string.Empty;
        RebirthCraftingProgressionDefinition definition;
        if(!TryGetRecipe(recipeName,out definition)||definition==null)return false;
        skillId=definition.PrimarySkillId??string.Empty;
        return true; // true means recipe is owned, even when this owned recipe intentionally has no primary Skill.
    }

    // Only explicit recipe metadata chooses the category, never the station/viewer's skill.
    public static bool TryGetImprovisationCategory(string recipeName,out string category)
    {
        category=string.Empty;
        RebirthCraftingProgressionDefinition definition;
        if(!TryGetRecipe(recipeName,out definition)||definition==null||definition.IsDisabled||
            string.IsNullOrEmpty(definition.ImprovisationCategory))return false;
        category=definition.ImprovisationCategory;
        return true;
    }
    public static bool RequiresServerAuthorization(string recipeName)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        RebirthCraftingProgressionDefinition definition;
        return TryGetRecipe(recipeName,out definition)&&definition!=null
            && RebirthCraftingProgressionPolicies.RequiresServerAuthorization(definition.Policy);
    }

    public static RebirthCraftingProgressionDefinition[] GetSnapshot()
    {
        List<RebirthCraftingProgressionDefinition> values=new List<RebirthCraftingProgressionDefinition>(ByRecipe.Values);
        values.Sort(delegate(RebirthCraftingProgressionDefinition a,RebirthCraftingProgressionDefinition b)
        { return string.Compare(a.RecipeId,b.RecipeId,StringComparison.OrdinalIgnoreCase); });
        return values.ToArray();
    }

    public static void NoteExternalCompatibilityRecipe(string recipeName,string context)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        string id=(recipeName??string.Empty).Trim();
        if(id.Length==0)return;
        lock(ExternalSync)
        {
            if(ExternalDiagnostics.Count>=ExternalDiagnosticLimit || !ExternalDiagnostics.Add(id))return;
        }
        { if (RebirthLogSettings.CraftingUiLoggingEnabled) Log.Out("[REBIRTH CraftingProgression] external compatibility recipe='"+id+"' context="+(context??string.Empty)+" policy=native-allowed"); }
    }

    public static string BuildSummary()
    {
        return "[REBIRTH CraftingProgression] ready="+IsReady
            +" recipes="+RecipeCount
            +" gated="+GatedCount
            +" universal="+UniversalCount
            +" disabled="+DisabledCount
            +" liveCapabilities="+LiveCapabilityCount
            +" plannedCapabilities="+PlannedCapabilityCount
            +" rebirthMode="+RebirthSurvivorMode.IsEnabledForCurrentWorld()
            +" hash="+ShortHash(SemanticHash);
    }

    public static string BuildRecipeDiagnostic(EntityPlayer player,string recipeName)
    {
        string id=(recipeName??string.Empty).Trim();
        StringBuilder b=new StringBuilder();
        RebirthCraftingProgressionDefinition definition;
        if(!TryGetRecipe(id,out definition)||definition==null)
        {
            b.Append("[REBIRTH CraftingProgression] recipe=").Append(id)
                .Append(" ownership=external policy=native-allowed rebirthMode=")
                .Append(RebirthSurvivorMode.IsEnabledForCurrentWorld());
            return b.ToString();
        }
        b.Append("[REBIRTH CraftingProgression] recipe=").Append(id)
            .Append(" policy=").Append(definition.Policy)
            .Append(" family=").Append(definition.Family)
            .Append(" primarySkill=").Append(definition.PrimarySkillId)
            .Append(" capability=").Append(definition.CapabilityId)
            .Append(" implementation=").Append(definition.Implementation)
            .Append(" rebirthMode=").Append(RebirthSurvivorMode.IsEnabledForCurrentWorld()).AppendLine();
        RebirthCapabilityEvaluation evaluation=RebirthCapabilityService.EvaluateRecipe(player,id);
        b.Append("allowed=").Append(evaluation.IsAllowed)
            .Append(" state=").Append(evaluation.DisplayState)
            .Append(" reason=").Append(evaluation.FirstMissingReason);
        return b.ToString();
    }

    private static RebirthCraftingProgressionDefinition Parse(XElement node)
    {
        string id=A(node,"id");
        string policy=A(node,"policy").ToLowerInvariant();
        string capability=A(node,"capability");
        string primary=A(node,"primary_skill");
        string family=A(node,"family");
        string implementation=A(node,"implementation").ToLowerInvariant();
        if(id.Length==0)throw new InvalidDataException("crafting progression recipe requires id");
        if(!RebirthCraftingProgressionPolicies.IsKnown(policy))throw new InvalidDataException("crafting progression recipe '"+id+"' has invalid policy '"+policy+"'");
        if(family.Length==0)throw new InvalidDataException("crafting progression recipe '"+id+"' requires family");
        if(primary.Length>0)
        {
            RebirthSkillDefinition skill;
            if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(primary,out skill)||skill==null)
                throw new InvalidDataException("crafting progression recipe '"+id+"' references unknown primary Skill '"+primary+"'");
        }
        List<string> knowledge=Csv(A(node,"knowledge"));
        for(int i=0;i<knowledge.Count;i++)
        {
            RebirthKnowledgeDefinition value;
            if(!RebirthSurvivorDefinitionRegistry.TryGetKnowledge(knowledge[i],out value)||value==null)
                throw new InvalidDataException("crafting progression recipe '"+id+"' references unknown Knowledge '"+knowledge[i]+"'");
        }
        if(policy==RebirthCraftingProgressionPolicies.Gated)
        {
            if(primary.Length==0)throw new InvalidDataException("GATED crafting progression recipe '"+id+"' requires primary_skill");
            if(capability.Length==0)throw new InvalidDataException("GATED crafting progression recipe '"+id+"' requires capability");
            if(implementation!="existing_capability" && implementation!="planned_capability")
                throw new InvalidDataException("GATED crafting progression recipe '"+id+"' has invalid implementation '"+implementation+"'");
            RebirthCapabilityDefinition cap;
            bool exists=RebirthCapabilityRegistry.TryGet(capability,out cap)&&cap!=null;
            if(implementation=="existing_capability" && !exists)
                throw new InvalidDataException("crafting progression recipe '"+id+"' references missing live Capability '"+capability+"'");
            if(exists && (!string.Equals(cap.TargetType,"recipe",StringComparison.OrdinalIgnoreCase) || !string.Equals(cap.TargetId,id,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("crafting progression Capability '"+capability+"' does not target recipe '"+id+"'");
        }
        else
        {
            if(capability.Length>0)throw new InvalidDataException(policy.ToUpperInvariant()+" crafting progression recipe '"+id+"' must not reference Capability '"+capability+"'");
            if(policy==RebirthCraftingProgressionPolicies.Universal && implementation!="policy_declared")
                throw new InvalidDataException("UNIVERSAL crafting progression recipe '"+id+"' must use implementation=policy_declared");
            if(policy==RebirthCraftingProgressionPolicies.Disabled && implementation!="runtime_disabled")
                throw new InvalidDataException("DISABLED crafting progression recipe '"+id+"' must use implementation=runtime_disabled in Chunk B+");
        }
        string improvisationCategory=A(node,"improvisation_category");
        if(improvisationCategory.Length>0 && !RebirthImprovisationProgressPersistence.ValidCategory(improvisationCategory))
            throw new InvalidDataException("Invalid improvisation category for recipe '"+id+"'");
        return new RebirthCraftingProgressionDefinition(id,policy,capability,primary,family,implementation,A(node,"source"),A(node,"source_status"),A(node,"decision_status"),knowledge,improvisationCategory);
    }

    private static void Add(RebirthCraftingProgressionDefinition definition)
    {
        if(definition==null)return;
        if(ByRecipe.ContainsKey(definition.RecipeId))throw new InvalidDataException("duplicate crafting progression recipe ID: "+definition.RecipeId);
        ByRecipe.Add(definition.RecipeId,definition);
        if(definition.IsGated)gatedCount++;
        else if(definition.IsUniversal)universalCount++;
        else if(definition.IsDisabled)disabledCount++;
        if(definition.HasLiveCapability)liveCapabilityCount++;
        if(definition.HasPlannedCapability)plannedCapabilityCount++;
    }

    private static string ComputeSemanticHash()
    {
        StringBuilder b=new StringBuilder();
        RebirthCraftingProgressionDefinition[] values=GetSnapshot();
        for(int i=0;i<values.Length;i++)
        {
            RebirthCraftingProgressionDefinition d=values[i];
            b.Append(d.RecipeId).Append('|').Append(d.Policy).Append('|').Append(d.CapabilityId).Append('|')
             .Append(d.PrimarySkillId).Append('|').Append(d.Family).Append('|').Append(d.Implementation).Append('|').Append(d.ImprovisationCategory).Append('|');
            for(int k=0;k<d.KnowledgeIds.Count;k++)b.Append(d.KnowledgeIds[k]).Append(',');
            b.Append('\n');
        }
        using(SHA256 sha=SHA256.Create())
        {
            byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()));
            StringBuilder x=new StringBuilder(hash.Length*2);
            for(int i=0;i<hash.Length;i++)x.Append(hash[i].ToString("x2",CultureInfo.InvariantCulture));
            return x.ToString();
        }
    }

    private static string A(XElement e,string name){return ((string)e.Attribute(name)??string.Empty).Trim();}
    private static List<string> Csv(string raw){List<string> r=new List<string>();if(string.IsNullOrWhiteSpace(raw))return r;string[] p=raw.Split(',');for(int i=0;i<p.Length;i++){string v=(p[i]??string.Empty).Trim();if(v.Length>0)r.Add(v);}return r;}
    private static string ShortHash(string value){return string.IsNullOrEmpty(value)?string.Empty:value.Substring(0,Math.Min(12,value.Length));}
}
