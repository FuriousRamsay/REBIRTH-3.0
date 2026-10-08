using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

#nullable disable

public sealed class RebirthAdvancedDisciplineRequirement
{
    public string Kind { get; private set; }
    public string Id { get; private set; }
    public float Minimum { get; private set; }
    public bool HasMinimum { get; private set; }
    public bool Planned { get; private set; }

    public RebirthAdvancedDisciplineRequirement(string kind,string id,float minimum,bool hasMinimum,bool planned)
    { Kind=(kind??string.Empty).Trim().ToLowerInvariant();Id=(id??string.Empty).Trim();Minimum=minimum;HasMinimum=hasMinimum;Planned=planned; }
}

public sealed class RebirthAdvancedDisciplineDefinition
{
    public string Id { get; private set; }
    public string NameKey { get; private set; }
    public string DescriptionKey { get; private set; }
    public string InitiationActionId { get; private set; }
    public ReadOnlyCollection<RebirthAdvancedDisciplineRequirement> Requirements { get; private set; }
    public ReadOnlyCollection<string> RelatedNodeIds { get; private set; }

    public RebirthAdvancedDisciplineDefinition(string id,string nameKey,string descriptionKey,string initiationActionId,IList<RebirthAdvancedDisciplineRequirement> requirements,IList<string> relatedNodeIds)
    {
        Id=(id??string.Empty).Trim();NameKey=(nameKey??string.Empty).Trim();DescriptionKey=(descriptionKey??string.Empty).Trim();InitiationActionId=(initiationActionId??string.Empty).Trim();
        Requirements=new ReadOnlyCollection<RebirthAdvancedDisciplineRequirement>(new List<RebirthAdvancedDisciplineRequirement>(requirements??new List<RebirthAdvancedDisciplineRequirement>()));
        RelatedNodeIds=new ReadOnlyCollection<string>(new List<string>(relatedNodeIds??new List<string>()));
    }
}

public sealed class RebirthAdvancedDisciplineEligibilityResult
{
    public string DisciplineId { get; private set; }
    public bool Available { get; private set; }
    public bool DeferredByFoundation { get; private set; }
    public ReadOnlyCollection<string> Reasons { get; private set; }
    public RebirthAdvancedDisciplineEligibilityResult(string id,bool available,bool deferred,IList<string> reasons)
    { DisciplineId=id??string.Empty;Available=available;DeferredByFoundation=deferred;Reasons=new ReadOnlyCollection<string>(new List<string>(reasons??new List<string>())); }
}

/// <summary>
/// Shared authority for discoverable Advanced Disciplines. Chunk A established stable identities,
/// prerequisite evaluation and central balance categories; owning feature chunks control acquisition
/// and persistence. PC007 additionally enforces the PC002 zombie-animal boundary used by Beastmaster
/// and the later Black Magic target classifier.
/// </summary>
public static class RebirthAdvancedDisciplineRegistry
{
    private static readonly Dictionary<string,RebirthAdvancedDisciplineDefinition> Definitions=new Dictionary<string,RebirthAdvancedDisciplineDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,int> AnimalCapacityWeights=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,int> ZombieTierWeights=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ExcludedEntityClasses=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ExcludedTags=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<KeyValuePair<string,FastTags<TagGroup.Global>>> ExcludedFastTags=new List<KeyValuePair<string,FastTags<TagGroup.Global>>>();
    private static readonly List<string> ExcludedEntityPrefixes=new List<string>();
    private static bool ready;
    private static bool grantsEnabled;
    private static int baseDogCapacity=1,globalCompanionSafetyLimit=8;
    private static int antiFarmDefault=60,antiFarmTarget=180,antiFarmActivation=30;
    private static string sourcePath=string.Empty;

    public static bool IsReady { get { return ready; } }
    public static string SourcePath { get { EnsureReady(); return sourcePath; } }
    public static bool GrantsEnabled { get { EnsureReady(); return grantsEnabled && RebirthSurvivorMode.IsEnabledForCurrentWorld(); } }
    public static int DefinitionCount { get { EnsureReady(); return Definitions.Count; } }
    public static int BaseDogCapacity { get { EnsureReady(); return Math.Max(1,baseDogCapacity); } }
    public static int GlobalCompanionSafetyLimit { get { EnsureReady(); return Math.Max(1,globalCompanionSafetyLimit); } }
    public static int AntiFarmDefaultWindowSeconds { get { EnsureReady(); return Math.Max(0,antiFarmDefault); } }
    public static int AntiFarmTargetWindowSeconds { get { EnsureReady(); return Math.Max(0,antiFarmTarget); } }
    public static int AntiFarmActivationWindowSeconds { get { EnsureReady(); return Math.Max(0,antiFarmActivation); } }

    public static string Load()
    {
        Clear();
        string root=RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
        string path=Path.Combine(root,"advanced_disciplines.xml");
        if(!File.Exists(path))throw new FileNotFoundException("Advanced Disciplines configuration is missing.",path);
        XDocument doc=XDocument.Load(path);XElement xroot=doc.Root;
        if(xroot==null||!string.Equals(xroot.Name.LocalName,"advanced_disciplines",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("advanced_disciplines.xml has an invalid root element.");
        string mode=Attr(xroot,"progression_mode");
        if(!string.Equals(mode,"Rebirth",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("advanced_disciplines.xml must be authored for Rebirth progression only.");
        grantsEnabled=BoolAttr(xroot,"grants_enabled",false);
        sourcePath=path;

        XElement tunables=xroot.Element("tunables");
        if(tunables!=null)
        {
            XElement anti=tunables.Element("anti_farm");
            if(anti!=null){antiFarmDefault=IntAttr(anti,"default_window_seconds",60);antiFarmTarget=IntAttr(anti,"target_window_seconds",180);antiFarmActivation=IntAttr(anti,"activation_window_seconds",30);}
            XElement global=tunables.Element("global_companion");if(global!=null){baseDogCapacity=IntAttr(global,"base_dog_capacity",1);globalCompanionSafetyLimit=IntAttr(global,"total_safety_limit",8);}
            XElement animal=tunables.Element("beastmaster_animal_capacity");if(animal!=null)foreach(XElement e in animal.Elements("category")){string id=Attr(e,"id");if(id.Length>0)AnimalCapacityWeights[id]=Math.Max(1,IntAttr(e,"weight",1));}
            XElement zombie=tunables.Element("zombie_control_capacity");if(zombie!=null)foreach(XElement e in zombie.Elements("tier")){string id=Attr(e,"id");if(id.Length>0)ZombieTierWeights[id]=Math.Max(1,IntAttr(e,"weight",1));}
        }

        XElement exclusions=xroot.Element("beastmaster_hard_exclusions");
        if(exclusions!=null)
        {
            foreach(XElement e in exclusions.Elements("entity_class")){string id=Attr(e,"id");if(id.Length>0)ExcludedEntityClasses.Add(id);}
            foreach(XElement e in exclusions.Elements("entity_class_prefix")){string id=Attr(e,"value");if(id.Length>0)ExcludedEntityPrefixes.Add(id);}
            foreach(XElement e in exclusions.Elements("tag")){string id=Attr(e,"id");if(id.Length>0&&ExcludedTags.Add(id))ExcludedFastTags.Add(new KeyValuePair<string,FastTags<TagGroup.Global>>(id,FastTags<TagGroup.Global>.Parse(id)));}
        }

        foreach(XElement e in xroot.Elements("discipline"))
        {
            string id=Attr(e,"id");if(id.Length==0)continue;
            List<RebirthAdvancedDisciplineRequirement> reqs=new List<RebirthAdvancedDisciplineRequirement>();
            foreach(XElement r in e.Elements("requirement"))
            {
                float minimum;bool hasMinimum=TryFloatAttr(r,"minimum",out minimum);
                reqs.Add(new RebirthAdvancedDisciplineRequirement(Attr(r,"kind"),Attr(r,"id"),minimum,hasMinimum,BoolAttr(r,"planned",false)));
            }
            List<string> relations=new List<string>();foreach(XElement r in e.Elements("relation")){string node=Attr(r,"node_id");if(node.Length>0)relations.Add(node);}
            if(Definitions.ContainsKey(id))throw new InvalidDataException("Duplicate Advanced Discipline ID: "+id);
            Definitions.Add(id,new RebirthAdvancedDisciplineDefinition(id,Attr(e,"name_key"),Attr(e,"description_key"),Attr(e,"initiation_action"),reqs,relations));
        }

        List<string> errors=ValidateAuthoring();if(errors.Count>0)throw new InvalidDataException("Advanced Disciplines authoring is invalid: "+string.Join(" | ",errors.ToArray()));
        ready=true;
        return "advancedDisciplines definitions="+Definitions.Count+" grants="+grantsEnabled+" baseDogCapacity="+BaseDogCapacity;
    }

    public static void Clear()
    {
        ready=false;grantsEnabled=false;baseDogCapacity=1;globalCompanionSafetyLimit=8;antiFarmDefault=60;antiFarmTarget=180;antiFarmActivation=30;sourcePath=string.Empty;
        Definitions.Clear();AnimalCapacityWeights.Clear();ZombieTierWeights.Clear();ExcludedEntityClasses.Clear();ExcludedTags.Clear();ExcludedFastTags.Clear();ExcludedEntityPrefixes.Clear();
    }

    public static RebirthAdvancedDisciplineDefinition[] GetDefinitionsSnapshot()
    { EnsureReady();List<RebirthAdvancedDisciplineDefinition> values=new List<RebirthAdvancedDisciplineDefinition>(Definitions.Values);values.Sort(delegate(RebirthAdvancedDisciplineDefinition a,RebirthAdvancedDisciplineDefinition b){return string.Compare(a.Id,b.Id,StringComparison.OrdinalIgnoreCase);});return values.ToArray(); }

    public static bool TryGetDefinition(string id,out RebirthAdvancedDisciplineDefinition definition)
    { EnsureReady();return Definitions.TryGetValue(id??string.Empty,out definition); }

    public static bool TryGetAnimalCapacityWeight(string categoryId,out int weight)
    { EnsureReady();return AnimalCapacityWeights.TryGetValue(categoryId??string.Empty,out weight); }

    public static bool TryGetZombieTierWeight(string tierId,out int weight)
    { EnsureReady();return ZombieTierWeights.TryGetValue(tierId??string.Empty,out weight); }

    public static bool IsBeastmasterAnimalHardExcluded(string entityClassName,IEnumerable<string> tags,out string reason)
    {
        EnsureReady();string cls=(entityClassName??string.Empty).Trim();
        if(cls.Length>0&&ExcludedEntityClasses.Contains(cls)){reason="entity class is explicitly excluded from Beastmaster taming/Wild Affinity: "+cls;return true;}
        if(cls.Length>0)for(int i=0;i<ExcludedEntityPrefixes.Count;i++)if(cls.StartsWith(ExcludedEntityPrefixes[i],StringComparison.OrdinalIgnoreCase)){reason="entity class matches excluded infected/zombie prefix: "+ExcludedEntityPrefixes[i];return true;}
        if(tags!=null)foreach(string raw in tags){string tag=(raw??string.Empty).Trim();if(tag.Length>0&&ExcludedTags.Contains(tag)){reason="entity has excluded infected/zombie/undead tag: "+tag;return true;}}
        reason=string.Empty;return false;
    }

    public static bool IsBeastmasterAnimalHardExcluded(EntityClass entityClass)
    {
        EnsureReady();
        if(entityClass==null)return true;
        string cls=(entityClass.entityClassName??string.Empty).Trim();
        if(cls.Length>0&&ExcludedEntityClasses.Contains(cls))return true;
        if(cls.Length>0)for(int i=0;i<ExcludedEntityPrefixes.Count;i++)if(cls.StartsWith(ExcludedEntityPrefixes[i],StringComparison.OrdinalIgnoreCase))return true;
        for(int i=0;i<ExcludedFastTags.Count;i++)if(entityClass.Tags.Test_AnySet(ExcludedFastTags[i].Value))return true;
        return false;
    }

    public static bool IsBeastmasterAnimalHardExcluded(EntityClass entityClass,out string reason)
    {
        EnsureReady();
        if(entityClass==null){reason="entity class definition is unavailable";return true;}
        string cls=(entityClass.entityClassName??string.Empty).Trim();
        if(cls.Length>0&&ExcludedEntityClasses.Contains(cls)){reason="entity class is explicitly excluded from Beastmaster taming/Wild Affinity: "+cls;return true;}
        if(cls.Length>0)for(int i=0;i<ExcludedEntityPrefixes.Count;i++)if(cls.StartsWith(ExcludedEntityPrefixes[i],StringComparison.OrdinalIgnoreCase)){reason="entity class matches excluded infected/zombie prefix: "+ExcludedEntityPrefixes[i];return true;}
        for(int i=0;i<ExcludedFastTags.Count;i++)
            if(entityClass.Tags.Test_AnySet(ExcludedFastTags[i].Value)){reason="entity has excluded infected/zombie/undead tag: "+ExcludedFastTags[i].Key;return true;}
        reason=string.Empty;return false;
    }

    public static RebirthAdvancedDisciplineEligibilityResult EvaluatePrerequisites(string disciplineId,RebirthWorldCharacterRecord record,ISet<string> acquiredDisciplines,ISet<string> completedActions,ISet<string> accomplishments,ISet<string> completedTrials)
    {
        EnsureReady();List<string> reasons=new List<string>();RebirthAdvancedDisciplineDefinition d;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){reasons.Add("Advanced Disciplines are disabled because Character Progression is not Rebirth.");return new RebirthAdvancedDisciplineEligibilityResult(disciplineId,false,false,reasons);}
        if(!Definitions.TryGetValue(disciplineId??string.Empty,out d)){reasons.Add("Unknown Advanced Discipline.");return new RebirthAdvancedDisciplineEligibilityResult(disciplineId,false,false,reasons);}
        bool ok=true,deferred=!grantsEnabled;
        if(!grantsEnabled)reasons.Add("Discipline acquisition is disabled by advanced_disciplines authoring.");
        for(int i=0;i<d.Requirements.Count;i++)
        {
            RebirthAdvancedDisciplineRequirement r=d.Requirements[i];
            if(r.Planned){deferred=true;reasons.Add("Planned prerequisite deferred to owning implementation chunk: "+r.Kind+" "+r.Id);continue;}
            if(string.Equals(r.Kind,"skill",StringComparison.OrdinalIgnoreCase))
            {
                RebirthSkillRuntimeState s=null;if(record==null||record.Progression==null||!record.Progression.Skills.TryGetValue(r.Id,out s)||s==null||r.HasMinimum&&s.Value+0.0001f<r.Minimum){ok=false;reasons.Add("Skill prerequisite not met: "+r.Id);}
            }
            else if(string.Equals(r.Kind,"melee_breadth",StringComparison.OrdinalIgnoreCase)||string.Equals(r.Kind,"melee_depth",StringComparison.OrdinalIgnoreCase))
            {
                int count=0;string[] melee=RebirthWeaponFamilySkillService.GetMeleeSkillIds();float threshold=string.Equals(r.Kind,"melee_depth",StringComparison.OrdinalIgnoreCase)?RebirthRageService.DepthLevel:RebirthRageService.BreadthLevel;
                for(int m=0;m<melee.Length;m++){RebirthSkillRuntimeState ms=null;if(record!=null&&record.Progression!=null&&record.Progression.Skills.TryGetValue(melee[m],out ms)&&ms!=null&&ms.Value+0.0001f>=threshold)count++;}
                if(count+0.0001f<r.Minimum){ok=false;reasons.Add((string.Equals(r.Kind,"melee_depth",StringComparison.OrdinalIgnoreCase)?"Melee depth":"Melee breadth")+" prerequisite not met: "+count+"/"+r.Minimum.ToString("0",CultureInfo.InvariantCulture));}
            }
            else if(string.Equals(r.Kind,"knowledge",StringComparison.OrdinalIgnoreCase))
            { if(record==null||record.Progression==null||!record.Progression.KnowledgeIds.Contains(r.Id)){ok=false;reasons.Add("Knowledge prerequisite not met: "+r.Id);} }
            else if(string.Equals(r.Kind,"discipline",StringComparison.OrdinalIgnoreCase))
            { if(acquiredDisciplines==null||!acquiredDisciplines.Contains(r.Id)){ok=false;reasons.Add("Discipline prerequisite not met: "+r.Id);} }
            else if(string.Equals(r.Kind,"action",StringComparison.OrdinalIgnoreCase))
            { if(completedActions==null||!completedActions.Contains(r.Id)){ok=false;reasons.Add("Action prerequisite not met: "+r.Id);} }
            else if(string.Equals(r.Kind,"accomplishment",StringComparison.OrdinalIgnoreCase))
            { if(accomplishments==null||!accomplishments.Contains(r.Id)){ok=false;reasons.Add("Accomplishment prerequisite not met: "+r.Id);} }
            else if(string.Equals(r.Kind,"trial",StringComparison.OrdinalIgnoreCase))
            { if(completedTrials==null||!completedTrials.Contains(r.Id)){ok=false;reasons.Add("Trial prerequisite not met: "+r.Id);} }
            else {ok=false;reasons.Add("Unsupported prerequisite kind: "+r.Kind);}
        }
        return new RebirthAdvancedDisciplineEligibilityResult(d.Id,ok&&grantsEnabled&&!deferred,deferred,reasons);
    }

    public static bool CanAcquireFromRecord(string disciplineId, RebirthWorldCharacterRecord record, string completedTrialId, out string reason)
    {
        reason = string.Empty;
        if (record == null || record.Progression == null) { reason = "Rebirth character progression is unavailable."; return false; }
        HashSet<string> disciplines = new HashSet<string>(record.Progression.AcquiredDisciplineIds, StringComparer.OrdinalIgnoreCase);
        HashSet<string> accomplishments = new HashSet<string>(record.Progression.AccomplishmentIds, StringComparer.OrdinalIgnoreCase);
        HashSet<string> trials = new HashSet<string>(record.Progression.CompletedTrialIds, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(completedTrialId)) trials.Add(completedTrialId.Trim());
        RebirthAdvancedDisciplineEligibilityResult decision = EvaluatePrerequisites(disciplineId, record, disciplines, null, accomplishments, trials);
        if (decision != null && decision.Available) return true;
        reason = decision == null || decision.Reasons == null || decision.Reasons.Count == 0
            ? "Advanced Discipline acquisition is not currently authorized."
            : string.Join(" ", new List<string>(decision.Reasons).ToArray());
        return false;
    }

    public static string BuildDebugSummary(string disciplineId)
    {
        EnsureReady();StringBuilder b=new StringBuilder();b.Append("[REBIRTH AdvancedDisciplines] ready=").Append(ready).Append(" rebirthMode=").Append(RebirthSurvivorMode.IsEnabledForCurrentWorld()).Append(" grantsEnabled=").Append(grantsEnabled).Append(" definitions=").Append(Definitions.Count).Append(" baseDogCapacity=").Append(BaseDogCapacity).Append(" globalSafetyLimit=").Append(GlobalCompanionSafetyLimit).Append(" antiFarm=").Append(antiFarmDefault).Append('/').Append(antiFarmTarget).Append('/').Append(antiFarmActivation);
        if(!string.IsNullOrEmpty(disciplineId)){RebirthAdvancedDisciplineDefinition d;if(!Definitions.TryGetValue(disciplineId,out d))return b.Append(" unknown=").Append(disciplineId).ToString();b.Append("\n  ").Append(d.Id).Append(" initiation=").Append(d.InitiationActionId);for(int i=0;i<d.Requirements.Count;i++){RebirthAdvancedDisciplineRequirement r=d.Requirements[i];b.Append("\n    requirement ").Append(r.Kind).Append(' ').Append(r.Id);if(r.HasMinimum)b.Append(" minimum=").Append(r.Minimum.ToString("0.###",CultureInfo.InvariantCulture));if(r.Planned)b.Append(" planned");}for(int i=0;i<d.RelatedNodeIds.Count;i++)b.Append("\n    related ").Append(d.RelatedNodeIds[i]);}
        return b.ToString();
    }

    public static string RunVectors()
    {
        EnsureReady();List<string> failures=new List<string>();
        if(Definitions.Count!=3)failures.Add("expected exactly 3 foundation discipline definitions");
        if(!grantsEnabled)failures.Add("Chunk D must enable non-deferred discipline acquisition");
        string reason;if(!IsBeastmasterAnimalHardExcluded("animalZombieDog",null,out reason))failures.Add("zombie dog hard exclusion missing");
        if(!IsBeastmasterAnimalHardExcluded("animalZombieBear",null,out reason))failures.Add("zombie bear hard exclusion missing");
        if(!IsBeastmasterAnimalHardExcluded("animalZombieSomethingNew",null,out reason))failures.Add("animalZombie prefix exclusion missing");
        if(!IsBeastmasterAnimalHardExcluded("futureAnimal",new string[]{"undead"},out reason))failures.Add("undead tag exclusion missing");
        if(!IsBeastmasterAnimalHardExcluded("futureAnimal",new string[]{"zombieAnimal"},out reason))failures.Add("zombieAnimal tag exclusion missing");
        if(!IsBeastmasterAnimalHardExcluded("animalDireWolf",null,out reason))failures.Add("audited Dire Wolf zombie-animal exclusion missing");
        if(!IsBeastmasterAnimalHardExcluded("animalBossGrace",null,out reason))failures.Add("audited Grace zombie-animal exclusion missing");
        RebirthAdvancedDisciplineDefinition beast;if(Definitions.TryGetValue(RebirthSurvivorIds.DisciplineBeastmaster,out beast))for(int i=0;i<beast.RelatedNodeIds.Count;i++)if(string.Equals(beast.RelatedNodeIds[i],"skill.melee",StringComparison.OrdinalIgnoreCase))failures.Add("generic melee relation is prohibited");
        return "[REBIRTH AdvancedDisciplines] vectors="+(failures.Count==0?"PASS":"FAIL")+" failures="+failures.Count+(failures.Count==0?string.Empty:" "+string.Join(" | ",failures.ToArray()));
    }

    private static List<string> ValidateAuthoring()
    {
        List<string> e=new List<string>();string[] required={RebirthSurvivorIds.DisciplineBeastmaster,RebirthSurvivorIds.DisciplineWitchDoctor,RebirthSurvivorIds.DisciplineBerserker};for(int i=0;i<required.Length;i++)if(!Definitions.ContainsKey(required[i]))e.Add("missing "+required[i]);
        foreach(KeyValuePair<string,int> pair in AnimalCapacityWeights)if(pair.Key.IndexOf("zombie",StringComparison.OrdinalIgnoreCase)>=0||pair.Key.IndexOf("undead",StringComparison.OrdinalIgnoreCase)>=0||pair.Key.IndexOf("infect",StringComparison.OrdinalIgnoreCase)>=0)e.Add("Beastmaster animal capacity cannot contain infected/undead category: "+pair.Key);
        RebirthAdvancedDisciplineDefinition beast;if(Definitions.TryGetValue(RebirthSurvivorIds.DisciplineBeastmaster,out beast))for(int i=0;i<beast.RelatedNodeIds.Count;i++)if(string.Equals(beast.RelatedNodeIds[i],"skill.melee",StringComparison.OrdinalIgnoreCase))e.Add("generic skill.melee relation is prohibited");
        if(!ExcludedEntityClasses.Contains("animalZombieDog"))e.Add("animalZombieDog hard exclusion is required by PC002");if(!ExcludedEntityClasses.Contains("animalZombieBear"))e.Add("animalZombieBear hard exclusion is required by PC002");
        if(!ExcludedEntityClasses.Contains("animalDireWolf"))e.Add("animalDireWolf hard exclusion is required by PC002");if(!ExcludedEntityClasses.Contains("animalBossGrace"))e.Add("animalBossGrace hard exclusion is required by PC002");
        if(!ExcludedTags.Contains("zombieAnimal"))e.Add("zombieAnimal hard-exclusion tag is required by PC002");if(!ExcludedTags.Contains("animalZombie"))e.Add("animalZombie hard-exclusion tag is required by PC002");
        if(baseDogCapacity<1)e.Add("base_dog_capacity must be at least 1");if(globalCompanionSafetyLimit<1)e.Add("total_safety_limit must be at least 1");
        return e;
    }

    private static void EnsureReady(){if(!ready)Load();}
    private static string Attr(XElement e,string name){return e==null?string.Empty:((string)e.Attribute(name)??string.Empty).Trim();}
    private static bool BoolAttr(XElement e,string name,bool fallback){bool value;return bool.TryParse(Attr(e,name),out value)?value:fallback;}
    private static int IntAttr(XElement e,string name,int fallback){int value;return int.TryParse(Attr(e,name),NumberStyles.Integer,CultureInfo.InvariantCulture,out value)?value:fallback;}
    private static bool TryFloatAttr(XElement e,string name,out float value){return float.TryParse(Attr(e,name),NumberStyles.Float,CultureInfo.InvariantCulture,out value);}
}
