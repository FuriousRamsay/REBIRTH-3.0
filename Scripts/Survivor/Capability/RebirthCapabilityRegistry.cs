using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

#nullable disable

public static class RebirthCapabilityRegistry
{
    private static readonly Dictionary<string, RebirthCapabilityDefinition> ById = new Dictionary<string, RebirthCapabilityDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RebirthCapabilityDefinition> ByRecipe = new Dictionary<string, RebirthCapabilityDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RebirthProjectDefinition> Projects = new Dictionary<string, RebirthProjectDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, RebirthBlueprintDefinition> Blueprints = new Dictionary<string, RebirthBlueprintDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<RebirthCapabilityDefinition>> BySkill = new Dictionary<string, List<RebirthCapabilityDefinition>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<RebirthCapabilityDefinition>> ByKnowledge = new Dictionary<string, List<RebirthCapabilityDefinition>>(StringComparer.OrdinalIgnoreCase);
    private static bool ready;
    private static string semanticHash = string.Empty;

    public static bool IsReady { get { return ready; } }
    public static int CapabilityCount { get { return ById.Count; } }
    public static int RecipeCapabilityCount { get { return ByRecipe.Count; } }
    public static int ProjectCount { get { return Projects.Count; } }
    public static int BlueprintCount { get { return Blueprints.Count; } }
    public static string SemanticHash { get { return semanticHash; } }

    public static string Load(string configRoot, RebirthRecipeKnowledgeRule[] legacyRules)
    {
        if (string.IsNullOrEmpty(configRoot)) throw new InvalidDataException("Capability config root is empty");
        // Keep the last complete publication available if a reload fails late. Definitions are
        // immutable after construction, so shallow dictionary/list snapshots are sufficient.
        var oldById = new Dictionary<string, RebirthCapabilityDefinition>(ById, StringComparer.OrdinalIgnoreCase);
        var oldByRecipe = new Dictionary<string, RebirthCapabilityDefinition>(ByRecipe, StringComparer.OrdinalIgnoreCase);
        var oldProjects = new Dictionary<string, RebirthProjectDefinition>(Projects, StringComparer.OrdinalIgnoreCase);
        var oldBlueprints = new Dictionary<string, RebirthBlueprintDefinition>(Blueprints, StringComparer.OrdinalIgnoreCase);
        var oldBySkill = CloneIndex(BySkill); var oldByKnowledge = CloneIndex(ByKnowledge);
        bool oldReady = ready; string oldHash = semanticHash;
        Clear();
        try
        {
            LoadBlueprints(Path.Combine(configRoot, "blueprints.xml"));
            LoadProjects(Path.Combine(configRoot, "projects.xml"));
            LoadCapabilities(Path.Combine(configRoot, "capabilities.xml"));
            AdaptLegacyRules(legacyRules);
            ValidateProjectGraphs();
            BuildIndexes();
            semanticHash = ComputeSemanticHash();
            ready = true;
            return "capabilities=" + CapabilityCount + " recipeCapabilities=" + RecipeCapabilityCount + " projects=" + ProjectCount + " blueprints=" + BlueprintCount + " capabilityHash=" + ShortHash(semanticHash);
        }
        catch
        {
            Restore(ById, oldById); Restore(ByRecipe, oldByRecipe); Restore(Projects, oldProjects); Restore(Blueprints, oldBlueprints);
            RestoreIndex(BySkill, oldBySkill); RestoreIndex(ByKnowledge, oldByKnowledge); ready = oldReady; semanticHash = oldHash;
            throw;
        }
    }

    private static Dictionary<string,List<RebirthCapabilityDefinition>> CloneIndex(Dictionary<string,List<RebirthCapabilityDefinition>> source)
    {
        var copy=new Dictionary<string,List<RebirthCapabilityDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in source) copy[pair.Key]=new List<RebirthCapabilityDefinition>(pair.Value);
        return copy;
    }
    private static void Restore<T>(Dictionary<string,T> target,Dictionary<string,T> source)
    { target.Clear(); foreach(var pair in source)target[pair.Key]=pair.Value; }
    private static void RestoreIndex(Dictionary<string,List<RebirthCapabilityDefinition>> target,Dictionary<string,List<RebirthCapabilityDefinition>> source)
    { target.Clear(); foreach(var pair in source)target[pair.Key]=new List<RebirthCapabilityDefinition>(pair.Value); }

    public static void Clear()
    {
        ready = false; semanticHash = string.Empty;
        ById.Clear(); ByRecipe.Clear(); Projects.Clear(); Blueprints.Clear(); BySkill.Clear(); ByKnowledge.Clear();
    }

    public static bool TryGet(string id, out RebirthCapabilityDefinition definition)
    { return ById.TryGetValue(id ?? string.Empty, out definition); }

    public static bool TryGetRecipe(string recipeName, out RebirthCapabilityDefinition definition)
    { return ByRecipe.TryGetValue(recipeName ?? string.Empty, out definition); }

    public static bool TryGetProject(string id, out RebirthProjectDefinition definition)
    { return Projects.TryGetValue(id ?? string.Empty, out definition); }

    public static bool TryGetBlueprint(string id, out RebirthBlueprintDefinition definition)
    { return Blueprints.TryGetValue(id ?? string.Empty, out definition); }

    public static RebirthCapabilityDefinition[] GetCapabilitiesSnapshot()
    {
        List<RebirthCapabilityDefinition> values = new List<RebirthCapabilityDefinition>(ById.Values);
        values.Sort(delegate(RebirthCapabilityDefinition a, RebirthCapabilityDefinition b) { return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase); });
        return values.ToArray();
    }

    public static RebirthProjectDefinition[] GetProjectsSnapshot()
    {
        List<RebirthProjectDefinition> values = new List<RebirthProjectDefinition>(Projects.Values);
        values.Sort(delegate(RebirthProjectDefinition a, RebirthProjectDefinition b) { return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase); });
        return values.ToArray();
    }

    public static RebirthBlueprintDefinition[] GetBlueprintsSnapshot()
    {
        List<RebirthBlueprintDefinition> values = new List<RebirthBlueprintDefinition>(Blueprints.Values);
        values.Sort(delegate(RebirthBlueprintDefinition a, RebirthBlueprintDefinition b) { return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase); });
        return values.ToArray();
    }

    public static RebirthCapabilityDefinition[] GetBySkill(string skillId)
    { return Snapshot(BySkill, skillId); }

    public static RebirthCapabilityDefinition[] GetByKnowledge(string knowledgeId)
    { return Snapshot(ByKnowledge, knowledgeId); }

    private static RebirthCapabilityDefinition[] Snapshot(Dictionary<string, List<RebirthCapabilityDefinition>> source, string key)
    {
        List<RebirthCapabilityDefinition> values;
        if (!source.TryGetValue(key ?? string.Empty, out values) || values == null) return new RebirthCapabilityDefinition[0];
        List<RebirthCapabilityDefinition> copy = new List<RebirthCapabilityDefinition>(values);
        copy.Sort(delegate(RebirthCapabilityDefinition a, RebirthCapabilityDefinition b) { return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase); });
        return copy.ToArray();
    }


    private static string ComputeSemanticHash()
    {
        StringBuilder b=new StringBuilder();
        RebirthCapabilityDefinition[] capabilities=GetCapabilitiesSnapshot();
        for(int i=0;i<capabilities.Length;i++)
        {
            RebirthCapabilityDefinition d=capabilities[i];
            b.Append("C|").Append(d.Id).Append('|').Append(d.Category).Append('|').Append(d.TargetType).Append('|').Append(d.TargetId).Append('|').Append(d.Visibility).Append('|').Append(d.LegacyAdapted).Append('\n');
            AppendRequirement(b,d.Requirement,0);
        }
        List<string> blueprintIds=new List<string>(Blueprints.Keys);blueprintIds.Sort(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<blueprintIds.Count;i++){RebirthBlueprintDefinition d=Blueprints[blueprintIds[i]];b.Append("B|").Append(d.Id).Append('|').Append(d.NameKey).Append('|').Append(d.DescriptionKey).Append('|').Append(d.Category).Append('\n');}
        List<string> projectIds=new List<string>(Projects.Keys);projectIds.Sort(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<projectIds.Count;i++)
        {
            RebirthProjectDefinition p=Projects[projectIds[i]];b.Append("P|").Append(p.Id).Append('|').Append(p.OutputId).Append('|').Append(p.Category).Append('|').Append(p.Enabled).Append('\n');
            for(int j=0;j<p.Operations.Count;j++){RebirthProjectOperationDefinition o=p.Operations[j];b.Append("O|").Append(o.Id).Append('|').Append(o.Type).Append('|').Append(o.NativeTarget).Append('|').Append(o.WorldAction).Append('|').Append(string.Join(",",o.AfterOperationIds)).Append('\n');AppendRequirement(b,o.Requirement,1);}
        }
        using(SHA256 sha=SHA256.Create()){byte[] bytes=Encoding.UTF8.GetBytes(b.ToString());byte[] hash=sha.ComputeHash(bytes);StringBuilder hex=new StringBuilder(hash.Length*2);for(int i=0;i<hash.Length;i++)hex.Append(hash[i].ToString("x2",CultureInfo.InvariantCulture));return hex.ToString();}
    }

    private static void AppendRequirement(StringBuilder b,RebirthCapabilityRequirement r,int depth)
    {
        if(r==null)return;
        b.Append("R|").Append(depth).Append('|').Append(r.Kind).Append('|').Append(r.Id).Append('|').Append(r.HasMinimum).Append('|').Append(r.Minimum.ToString("R",CultureInfo.InvariantCulture)).Append('|').Append(r.HasRecommended).Append('|').Append(r.Recommended.ToString("R",CultureInfo.InvariantCulture)).Append('\n');
        for(int i=0;i<r.Children.Count;i++)AppendRequirement(b,r.Children[i],depth+1);
    }

    private static string ShortHash(string value){return string.IsNullOrEmpty(value)?string.Empty:(value.Length<=12?value:value.Substring(0,12));}

    private static void LoadCapabilities(string path)
    {
        XDocument doc = XDocument.Load(path); XElement root = doc.Root;
        if (root == null || root.Name != "survivor_capabilities" || (string)root.Attribute("schema_version") != "1")
            throw new InvalidDataException("capabilities.xml root/schema_version must be survivor_capabilities/1");
        foreach (XElement node in root.Elements("capability"))
        {
            string id = A(node, "id"), targetType = A(node, "target_type").ToLowerInvariant(), targetId = A(node, "target_id");
            if (id.Length == 0 || targetType.Length == 0 || targetId.Length == 0) throw new InvalidDataException("Capability requires id, target_type and target_id");
            if (targetType != "recipe" && targetType != "project") throw new InvalidDataException("Capability '" + id + "' has unsupported target_type=" + targetType);
            if (ById.ContainsKey(id)) throw new InvalidDataException("duplicate Capability ID: " + id);
            XElement requires = node.Element("requires_all");
            RebirthCapabilityRequirement requirement = requires != null ? ParseGroup(requires, RebirthCapabilityKinds.All, id) : new RebirthCapabilityRequirement(RebirthCapabilityKinds.All, string.Empty, 0f, false, 0f, false, null);
            RebirthCapabilityDefinition definition = new RebirthCapabilityDefinition(id, A(node, "category"), targetType, targetId, A(node, "visibility"), false, requirement);
            AddDefinition(definition);
        }
    }

    private static void LoadBlueprints(string path)
    {
        XDocument doc = XDocument.Load(path); XElement root = doc.Root;
        if (root == null || root.Name != "survivor_blueprints" || (string)root.Attribute("schema_version") != "1")
            throw new InvalidDataException("blueprints.xml root/schema_version must be survivor_blueprints/1");
        foreach (XElement node in root.Elements("blueprint"))
        {
            RebirthBlueprintDefinition value = new RebirthBlueprintDefinition(A(node,"id"), A(node,"name_key"), A(node,"description_key"), A(node,"category"));
            if (value.Id.Length == 0) throw new InvalidDataException("Blueprint requires id");
            if (Blueprints.ContainsKey(value.Id)) throw new InvalidDataException("duplicate Blueprint ID: " + value.Id);
            Blueprints.Add(value.Id, value);
        }
    }

    private static void LoadProjects(string path)
    {
        XDocument doc = XDocument.Load(path); XElement root = doc.Root;
        if (root == null || root.Name != "survivor_projects" || (string)root.Attribute("schema_version") != "1")
            throw new InvalidDataException("projects.xml root/schema_version must be survivor_projects/1");
        foreach (XElement node in root.Elements("project"))
        {
            string id = A(node,"id"); if (id.Length == 0) throw new InvalidDataException("Project requires id");
            if (Projects.ContainsKey(id)) throw new InvalidDataException("duplicate Project ID: " + id);
            bool enabled = B(node,"enabled",false);
            List<RebirthProjectOperationDefinition> operations = new List<RebirthProjectOperationDefinition>();
            HashSet<string> operationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement op in node.Elements("operation"))
            {
                string opId = A(op,"id"); if (opId.Length == 0 || !operationIds.Add(opId)) throw new InvalidDataException("Project '"+id+"' has missing/duplicate operation ID: "+opId);
                List<string> after = Csv(A(op,"after"));
                XElement requires = op.Element("requires_all");
                RebirthCapabilityRequirement req = requires != null ? ParseGroup(requires, RebirthCapabilityKinds.All, id+"/"+opId) : new RebirthCapabilityRequirement(RebirthCapabilityKinds.All,string.Empty,0f,false,0f,false,null);
                operations.Add(new RebirthProjectOperationDefinition(opId,A(op,"type"),A(op,"recipe"),A(op,"world_action"),after,req));
            }
            Projects.Add(id,new RebirthProjectDefinition(id,A(node,"output"),A(node,"category"),enabled,operations));
        }
    }

    private static RebirthCapabilityRequirement ParseGroup(XElement group, string kind, string owner)
    {
        List<RebirthCapabilityRequirement> children = new List<RebirthCapabilityRequirement>();
        foreach (XElement child in group.Elements())
        {
            string name = child.Name.LocalName.ToLowerInvariant();
            if (name == "requires_all") children.Add(ParseGroup(child, RebirthCapabilityKinds.All, owner));
            else if (name == "requires_any") children.Add(ParseGroup(child, RebirthCapabilityKinds.Any, owner));
            else if (name == RebirthCapabilityKinds.Skill)
            {
                string id = A(child,"id"); if (id.Length == 0) throw new InvalidDataException(owner+" Skill requirement requires id");
                RebirthSkillDefinition skill; if (!RebirthSurvivorDefinitionRegistry.TryGetSkill(id,out skill) || skill == null) throw new InvalidDataException(owner+" references unknown Skill ID: "+id);
                float minimum; bool hasMinimum = TryFloat(child,"minimum",out minimum);
                float recommended; bool hasRecommended = TryFloat(child,"recommended",out recommended);
                if (!hasMinimum && !hasRecommended) throw new InvalidDataException(owner+" Skill requirement '"+id+"' requires minimum and/or recommended");
                if (hasMinimum && (float.IsNaN(minimum) || float.IsInfinity(minimum) || minimum < -50f || minimum > 100f)) throw new InvalidDataException(owner+" Skill minimum out of range for "+id);
                if (hasRecommended && (float.IsNaN(recommended) || float.IsInfinity(recommended) || recommended < -50f || recommended > 100f)) throw new InvalidDataException(owner+" Skill recommended out of range for "+id);
                if (hasMinimum && hasRecommended && recommended < minimum) throw new InvalidDataException(owner+" Skill recommended < minimum for "+id);
                children.Add(new RebirthCapabilityRequirement(name,id,minimum,hasMinimum,recommended,hasRecommended,null));
            }
            else if (name == RebirthCapabilityKinds.Knowledge)
            {
                string id=A(child,"id"); RebirthKnowledgeDefinition knowledge;
                if (id.Length==0 || !RebirthSurvivorDefinitionRegistry.TryGetKnowledge(id,out knowledge) || knowledge==null) throw new InvalidDataException(owner+" references unknown Knowledge ID: "+id);
                children.Add(new RebirthCapabilityRequirement(name,id,0f,false,0f,false,null));
            }
            else if (name == RebirthCapabilityKinds.Discipline)
            {
                // Advanced disciplines load later; unresolved IDs fail closed during evaluation.
                string id=A(child,"id");
                if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("discipline.",StringComparison.Ordinal)) throw new InvalidDataException(owner+" has invalid Discipline ID: "+id);
                children.Add(new RebirthCapabilityRequirement(name,id,0f,false,0f,false,null));
            }
            else if (name == RebirthCapabilityKinds.Blueprint)
            {
                string id=A(child,"id"); if (id.Length==0 || !Blueprints.ContainsKey(id)) throw new InvalidDataException(owner+" references unknown Blueprint ID: "+id);
                children.Add(new RebirthCapabilityRequirement(name,id,0f,false,0f,false,null));
            }
            else throw new InvalidDataException(owner+" has unsupported requirement element: "+child.Name.LocalName);
        }
        if (children.Count == 0 && kind == RebirthCapabilityKinds.Any) throw new InvalidDataException(owner+" contains an empty requires_any group");
        return new RebirthCapabilityRequirement(kind,string.Empty,0f,false,0f,false,children);
    }

    private static void AdaptLegacyRules(RebirthRecipeKnowledgeRule[] rules)
    {
        if (rules == null) return;
        for (int i=0;i<rules.Length;i++)
        {
            RebirthRecipeKnowledgeRule rule=rules[i]; if (rule==null || string.IsNullOrEmpty(rule.RecipeName)) continue;
            RebirthCapabilityDefinition direct;
            if (ByRecipe.TryGetValue(rule.RecipeName,out direct) && direct != null)
            {
                // A direct multi-requirement Capability may supersede the legacy adapter only when
                // it preserves the exact authored recipe Knowledge dependency. This lets practical
                // Skill gates compose with recipe_knowledge.xml without silently bypassing literature.
                if (!MustRequireKnowledge(direct.Requirement,rule.KnowledgeId))
                    throw new InvalidDataException("direct Capability for recipe '"+rule.RecipeName+"' does not preserve mapped Knowledge '"+rule.KnowledgeId+"'");
                continue;
            }
            string id="legacy.recipe."+NormalizeId(rule.RecipeName);
            RebirthCapabilityRequirement knowledge = new RebirthCapabilityRequirement(RebirthCapabilityKinds.Knowledge,rule.KnowledgeId,0f,false,0f,false,null);
            RebirthCapabilityRequirement root = new RebirthCapabilityRequirement(RebirthCapabilityKinds.All,string.Empty,0f,false,0f,false,new[]{knowledge});
            AddDefinition(new RebirthCapabilityDefinition(id,rule.Category,"recipe",rule.RecipeName,"visible",true,root));
        }
    }


    private static bool MustRequireKnowledge(RebirthCapabilityRequirement requirement,string knowledgeId)
    {
        if(requirement==null || string.IsNullOrEmpty(knowledgeId)) return false;
        if(requirement.Kind==RebirthCapabilityKinds.Knowledge)
            return string.Equals(requirement.Id,knowledgeId,StringComparison.OrdinalIgnoreCase);
        if(requirement.Kind==RebirthCapabilityKinds.All)
        {
            // Every successful ALL evaluation includes every child, so one child that must
            // require the legacy knowledge is sufficient to preserve the gate.
            for(int i=0;i<requirement.Children.Count;i++)
                if(MustRequireKnowledge(requirement.Children[i],knowledgeId)) return true;
            return false;
        }
        if(requirement.Kind==RebirthCapabilityKinds.Any)
        {
            // An ANY preserves a mandatory gate only when every alternative implies it.
            if(requirement.Children.Count==0)return false;
            for(int i=0;i<requirement.Children.Count;i++)
                if(!MustRequireKnowledge(requirement.Children[i],knowledgeId)) return false;
            return true;
        }
        return false;
    }

    private static void AddDefinition(RebirthCapabilityDefinition definition)
    {
        if (definition == null) return;
        if (ById.ContainsKey(definition.Id)) throw new InvalidDataException("duplicate Capability ID: "+definition.Id);
        if (definition.TargetType == "recipe" && ByRecipe.ContainsKey(definition.TargetId)) throw new InvalidDataException("duplicate recipe Capability target: "+definition.TargetId);
        if (definition.TargetType == "project" && !Projects.ContainsKey(definition.TargetId)) throw new InvalidDataException("Capability references unknown Project ID: "+definition.TargetId);
        ById.Add(definition.Id,definition);
        if (definition.TargetType == "recipe") ByRecipe.Add(definition.TargetId,definition);
    }

    private static void ValidateProjectGraphs()
    {
        foreach (RebirthProjectDefinition project in Projects.Values)
        {
            Dictionary<string,RebirthProjectOperationDefinition> operations = new Dictionary<string,RebirthProjectOperationDefinition>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<project.Operations.Count;i++) operations[project.Operations[i].Id]=project.Operations[i];
            foreach(RebirthProjectOperationDefinition operation in project.Operations)
                for(int i=0;i<operation.AfterOperationIds.Count;i++) if(!operations.ContainsKey(operation.AfterOperationIds[i])) throw new InvalidDataException("Project '"+project.Id+"' operation '"+operation.Id+"' references unknown dependency '"+operation.AfterOperationIds[i]+"'");
            HashSet<string> visiting=new HashSet<string>(StringComparer.OrdinalIgnoreCase), visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string id in operations.Keys) VisitProject(project.Id,id,operations,visiting,visited);
        }
    }

    private static void VisitProject(string projectId,string id,Dictionary<string,RebirthProjectOperationDefinition> operations,HashSet<string> visiting,HashSet<string> visited)
    {
        if(visited.Contains(id)) return;
        if(!visiting.Add(id)) throw new InvalidDataException("Project '"+projectId+"' contains a cyclic operation dependency at '"+id+"'");
        RebirthProjectOperationDefinition op=operations[id];
        for(int i=0;i<op.AfterOperationIds.Count;i++) VisitProject(projectId,op.AfterOperationIds[i],operations,visiting,visited);
        visiting.Remove(id); visited.Add(id);
    }

    private static void BuildIndexes()
    {
        foreach(RebirthCapabilityDefinition definition in ById.Values) IndexRequirement(definition,definition.Requirement);
    }

    private static void IndexRequirement(RebirthCapabilityDefinition definition, RebirthCapabilityRequirement requirement)
    {
        if(requirement==null)return;
        if(requirement.Kind==RebirthCapabilityKinds.Skill) AddIndex(BySkill,requirement.Id,definition);
        else if(requirement.Kind==RebirthCapabilityKinds.Knowledge) AddIndex(ByKnowledge,requirement.Id,definition);
        for(int i=0;i<requirement.Children.Count;i++) IndexRequirement(definition,requirement.Children[i]);
    }

    private static void AddIndex(Dictionary<string,List<RebirthCapabilityDefinition>> index,string id,RebirthCapabilityDefinition definition)
    {
        List<RebirthCapabilityDefinition> list; if(!index.TryGetValue(id,out list)){list=new List<RebirthCapabilityDefinition>();index[id]=list;}
        if(!list.Contains(definition))list.Add(definition);
    }

    private static string A(XElement e,string name){return ((string)e.Attribute(name)??string.Empty).Trim();}
    private static bool B(XElement e,string name,bool fallback){bool v;string s=A(e,name);return s.Length>0&&bool.TryParse(s,out v)?v:fallback;}
    private static bool TryFloat(XElement e,string name,out float value){value=0f;XAttribute a=e.Attribute(name);return a!=null&&float.TryParse(a.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value);}
    private static List<string> Csv(string raw){List<string> result=new List<string>();if(string.IsNullOrWhiteSpace(raw))return result;string[] parts=raw.Split(',');for(int i=0;i<parts.Length;i++){string v=(parts[i]??string.Empty).Trim();if(v.Length>0)result.Add(v);}return result;}
    private static string NormalizeId(string raw){char[] chars=(raw??string.Empty).ToLowerInvariant().ToCharArray();for(int i=0;i<chars.Length;i++)if(!(char.IsLetterOrDigit(chars[i])||chars[i]=='_'||chars[i]=='-'))chars[i]='_';return new string(chars);}
}
