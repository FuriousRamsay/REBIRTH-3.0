using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

#nullable disable

public sealed class RebirthBlackMagicZombieAnimalDefinition
{
    public string EntityClassId { get; private set; }
    public bool BlackMagicEligible { get; private set; }
    public string TierId { get; private set; }
    public int DominationCapacityCost { get; private set; }
    public int BoundCapacityCost { get; private set; }
    public string RuntimeKind { get; private set; }
    public bool Protected { get; private set; }
    public bool BindingCandidate { get; private set; }
    public string RosterStatus { get; private set; }
    public string AuditReason { get; private set; }

    public RebirthBlackMagicZombieAnimalDefinition(string entityClassId,bool eligible,string tierId,int dominationCost,int boundCost,string runtimeKind,bool protectedTarget,bool bindingCandidate,string rosterStatus,string auditReason)
    {
        EntityClassId=(entityClassId??string.Empty).Trim();
        BlackMagicEligible=eligible;
        TierId=(tierId??string.Empty).Trim().ToLowerInvariant();
        DominationCapacityCost=Math.Max(0,dominationCost);
        BoundCapacityCost=Math.Max(0,boundCost);
        RuntimeKind=(runtimeKind??string.Empty).Trim().ToLowerInvariant();
        Protected=protectedTarget;
        BindingCandidate=bindingCandidate;
        RosterStatus=(rosterStatus??string.Empty).Trim().ToLowerInvariant();
        AuditReason=(auditReason??string.Empty).Trim();
    }
}

public sealed class RebirthBlackMagicTargetClassification
{
    public string EntityClassId { get; private set; }
    public string TierId { get; private set; }
    public int DominationCapacityCost { get; private set; }
    public bool IsZombieAnimal { get; private set; }
    public bool Protected { get; private set; }
    public string Source { get; private set; }
    public RebirthBlackMagicTargetClassification(string entityClassId,string tierId,int dominationCapacityCost,bool zombieAnimal,bool protectedTarget,string source)
    { EntityClassId=entityClassId??string.Empty;TierId=(tierId??"normal").ToLowerInvariant();DominationCapacityCost=Math.Max(1,dominationCapacityCost);IsZombieAnimal=zombieAnimal;Protected=protectedTarget;Source=source??string.Empty; }
}

/// <summary>
/// PC007 / amended Chunk E source-audited zombie-animal classification contract.
/// This service does not perform mind control or binding. It only answers whether a target is
/// eligible for the later Black Magic systems, and it deliberately fails closed for unlisted or
/// runtime-incompatible zombie animals. Beastmaster exclusion remains a separate hard invariant.
/// </summary>
public static class RebirthBlackMagicTargetClassifier
{
    private static readonly Dictionary<string,RebirthBlackMagicZombieAnimalDefinition> ZombieAnimals=new Dictionary<string,RebirthBlackMagicZombieAnimalDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<KeyValuePair<string,FastTags<TagGroup.Global>>> ProtectedTags=new List<KeyValuePair<string,FastTags<TagGroup.Global>>>();
    private static bool ready;
    private static string sourcePath=string.Empty;
    private static string defaultUnlisted="deny_not_source_audited";

    public static bool IsReady { get { return ready; } }
    public static int ZombieAnimalDefinitionCount { get { Ensure(); return ZombieAnimals.Count; } }

    public static string LoadDefinitions()
    {
        ready=false;ZombieAnimals.Clear();ProtectedTags.Clear();sourcePath=string.Empty;defaultUnlisted="deny_not_source_audited";
        string root=RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
        string path=Path.Combine(root,"advanced_disciplines.xml");
        if(!File.Exists(path))throw new FileNotFoundException("Advanced Disciplines configuration is missing.",path);
        XDocument doc=XDocument.Load(path);XElement xroot=doc.Root;XElement section=xroot==null?null:xroot.Element("black_magic_target_classification");
        if(section==null)throw new InvalidDataException("black_magic_target_classification is missing from advanced_disciplines.xml");
        sourcePath=path;defaultUnlisted=Attr(section,"default_unlisted");if(defaultUnlisted.Length==0)defaultUnlisted="deny_not_source_audited";
        XElement protectedTags=section.Element("protected_tags");
        if(protectedTags!=null)foreach(XElement e in protectedTags.Elements("tag")){string id=Attr(e,"id");if(id.Length>0)ProtectedTags.Add(new KeyValuePair<string,FastTags<TagGroup.Global>>(id,FastTags<TagGroup.Global>.Parse(id)));}
        XElement animals=section.Element("zombie_animals");
        if(animals==null)throw new InvalidDataException("black_magic_target_classification/zombie_animals is missing");
        foreach(XElement e in animals.Elements("target"))
        {
            string id=Attr(e,"class");if(id.Length==0)continue;if(ZombieAnimals.ContainsKey(id))throw new InvalidDataException("Duplicate Black Magic zombie-animal classification: "+id);
            RebirthBlackMagicZombieAnimalDefinition d=new RebirthBlackMagicZombieAnimalDefinition(id,BoolAttr(e,"black_magic_eligible",false),Attr(e,"tier"),IntAttr(e,"domination_capacity",0),IntAttr(e,"bound_capacity",0),Attr(e,"runtime_kind"),BoolAttr(e,"protected",false),BoolAttr(e,"binding_candidate",false),Attr(e,"roster_status"),Attr(e,"audit_reason"));
            ZombieAnimals.Add(id,d);
        }
        List<string> errors=ValidateAuthoring();if(errors.Count>0)throw new InvalidDataException("Black Magic zombie-animal authoring invalid: "+string.Join(" | ",errors.ToArray()));
        ready=true;return "blackMagicZombieAnimals definitions="+ZombieAnimals.Count+" eligible="+CountEligible()+" protectedTags="+ProtectedTags.Count;
    }

    public static RebirthBlackMagicZombieAnimalDefinition[] GetZombieAnimalDefinitionsSnapshot()
    {
        Ensure();List<RebirthBlackMagicZombieAnimalDefinition> list=new List<RebirthBlackMagicZombieAnimalDefinition>(ZombieAnimals.Values);list.Sort(delegate(RebirthBlackMagicZombieAnimalDefinition a,RebirthBlackMagicZombieAnimalDefinition b){return string.Compare(a.EntityClassId,b.EntityClassId,StringComparison.OrdinalIgnoreCase);});return list.ToArray();
    }

    public static bool TryGetZombieAnimalDefinition(string entityClassId,out RebirthBlackMagicZombieAnimalDefinition definition)
    { Ensure();return ZombieAnimals.TryGetValue((entityClassId??string.Empty).Trim(),out definition); }

    public static bool TryClassifyZombieAnimal(EntityAlive target,out RebirthBlackMagicZombieAnimalDefinition definition,out string reason)
    {
        reason=string.Empty;Ensure();definition=null;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){reason="Black Magic target classification is inactive outside Rebirth progression.";return false;}
        if(target==null||target.EntityClass==null){reason="Target entity/class unavailable.";return false;}
        string cls=(target.EntityClass.entityClassName??string.Empty).Trim();
        if(!ZombieAnimals.TryGetValue(cls,out definition)){reason="DENY_NOT_SOURCE_AUDITED: "+cls+" ("+defaultUnlisted+")";return false;}
        string beastReason;if(!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded(target.EntityClass,out beastReason)){reason="DENY_PC002_INVARIANT: authored zombie animal is not hard-excluded from Beastmaster.";return false;}
        if(definition.Protected||HasProtectedTag(target.EntityClass,out reason)){if(string.IsNullOrEmpty(reason))reason="DENY_PROTECTED_TARGET: "+definition.AuditReason;return false;}
        if(!definition.BlackMagicEligible){reason="DENY_BLACK_MAGIC_INELIGIBLE: "+definition.AuditReason;return false;}
        if(string.Equals(definition.RuntimeKind,"zombie_sdx",StringComparison.OrdinalIgnoreCase)&&!(target is EntityZombie)){reason="DENY_RUNTIME_INCOMPATIBLE: expected EntityZombie-family runtime for "+cls;return false;}
        if(!string.Equals(definition.RuntimeKind,"zombie_sdx",StringComparison.OrdinalIgnoreCase)){reason="DENY_RUNTIME_INCOMPATIBLE: no Black Magic adapter for runtime kind "+definition.RuntimeKind;return false;}
        reason="ELIGIBLE_BLACK_MAGIC_ZOMBIE_ANIMAL tier="+definition.TierId+" dominationCost="+definition.DominationCapacityCost+" bindingCandidate="+definition.BindingCandidate;
        return true;
    }

    public static bool TryClassifyTemporaryDomination(EntityAlive target,out RebirthBlackMagicTargetClassification classification,out string reason)
    {
        Ensure();classification=null;reason=string.Empty;
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()){reason="DENY_NOT_REBIRTH_MODE";return false;}
        if(target==null||target.IsDead()||target.EntityClass==null){reason="DENY_TARGET_UNAVAILABLE";return false;}
        string cls=(target.EntityClass.entityClassName??string.Empty).Trim();
        RebirthBlackMagicZombieAnimalDefinition animal;
        if(ZombieAnimals.TryGetValue(cls,out animal))
        {
            string animalReason;if(!TryClassifyZombieAnimal(target,out animal,out animalReason)){reason=animalReason;return false;}
            classification=new RebirthBlackMagicTargetClassification(cls,animal.TierId,Math.Max(1,animal.DominationCapacityCost),true,false,"audited_zombie_animal");reason=animalReason;return true;
        }
        bool animalLike=false;try{animalLike=target.HasAnyTags(FastTags<TagGroup.Global>.Parse("animal,zombieAnimal"));}catch{}
        if(animalLike){reason="DENY_UNLISTED_ZOMBIE_ANIMAL: "+cls;return false;}
        if(!(target is EntityZombie)){reason="DENY_RUNTIME_INCOMPATIBLE: temporary domination requires EntityZombie-family runtime";return false;}
        if(HasProtectedTag(target.EntityClass,out reason))return false;
        if(RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(target.EntityClass)){reason="DENY_PROTECTED_SPECIAL_CLASS: "+cls;return false;}
        RebirthSpawnCategory category;string tier;bool managed=RebirthSpawnCompositionService.TryGetManagedCategory(cls,out category);
        if(managed&&category==RebirthSpawnCategory.Special){reason="DENY_PROTECTED_SPECIAL_CATEGORY: "+cls;return false;}
        if(!managed&&!target.HasAnyTags(FastTags<TagGroup.Global>.Parse("zombie"))){reason="DENY_NOT_AUDITED_ZOMBIE_CLASS: "+cls;return false;}
        tier=managed?TierFromSpawnCategory(category):TierFromTagsAndClass(target,cls);
        if(string.IsNullOrEmpty(tier)){reason="DENY_TIER_UNRESOLVED: "+cls;return false;}
        int weight;if(!RebirthAdvancedDisciplineRegistry.TryGetZombieTierWeight(tier,out weight)){reason="DENY_TIER_WEIGHT_UNAUTHORED: "+tier;return false;}
        classification=new RebirthBlackMagicTargetClassification(cls,tier,weight,false,false,managed?"spawn_composition":"runtime_class_tags");
        reason="ELIGIBLE_BLACK_MAGIC tier="+tier+" dominationCost="+weight+" source="+classification.Source;return true;
    }

    private static string TierFromSpawnCategory(RebirthSpawnCategory category)
    {
        switch(category)
        {
            case RebirthSpawnCategory.FeralLow:case RebirthSpawnCategory.FeralMedium:case RebirthSpawnCategory.FeralHigh:return "feral";
            case RebirthSpawnCategory.RadiatedLow:case RebirthSpawnCategory.RadiatedMedium:case RebirthSpawnCategory.RadiatedHigh:return "radiated";
            case RebirthSpawnCategory.Charged:return "charged";
            case RebirthSpawnCategory.Infernal:return "infernal";
            case RebirthSpawnCategory.RegularLow:case RebirthSpawnCategory.RegularMedium:case RebirthSpawnCategory.RegularHigh:case RebirthSpawnCategory.Other:return "normal";
            default:return string.Empty;
        }
    }

    private static string TierFromTagsAndClass(EntityAlive target,string cls)
    {
        string[] ordered={"infernal","charged","radiated","feral"};
        for(int i=0;i<ordered.Length;i++)
        {
            string id=ordered[i];try{if(target.HasAnyTags(FastTags<TagGroup.Global>.Parse(id)))return id;}catch{}
            if(cls.IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0)return id;
        }
        return "normal";
    }

    public static string BuildZombieAnimalAuditSummary()
    {
        Ensure();StringBuilder b=new StringBuilder();b.Append("[REBIRTH BlackMagic] zombie-animal audit definitions=").Append(ZombieAnimals.Count).Append(" eligible=").Append(CountEligible()).Append(" source=").Append(Path.GetFileName(sourcePath));
        RebirthBlackMagicZombieAnimalDefinition[] values=GetZombieAnimalDefinitionsSnapshot();
        for(int i=0;i<values.Length;i++)
        {
            RebirthBlackMagicZombieAnimalDefinition d=values[i];b.Append("\n  ").Append(d.EntityClassId).Append(" eligible=").Append(d.BlackMagicEligible).Append(" tier=").Append(d.TierId).Append(" domination=").Append(d.DominationCapacityCost).Append(" bound=").Append(d.BoundCapacityCost).Append(" runtime=").Append(d.RuntimeKind).Append(" protected=").Append(d.Protected).Append(" bindingCandidate=").Append(d.BindingCandidate).Append(" roster=").Append(d.RosterStatus).Append(" audit=").Append(d.AuditReason);
        }
        return b.ToString();
    }

    public static string BuildEntityDebug(EntityAlive target)
    {
        Ensure();if(target==null)return "[REBIRTH BlackMagic] target unavailable";RebirthBlackMagicTargetClassification c;string reason;bool ok=TryClassifyTemporaryDomination(target,out c,out reason);string cls=target.EntityClass==null?"<none>":target.EntityClass.entityClassName;
        RebirthBlackMagicZombieAnimalDefinition d;ZombieAnimals.TryGetValue(cls??string.Empty,out d);
        return "[REBIRTH BlackMagic] entity="+target.entityId+" class="+cls+" eligible="+ok+" tier="+(c==null?"none":c.TierId)+" domination="+(c==null?0:c.DominationCapacityCost)+" zombieAnimal="+(c!=null&&c.IsZombieAnimal)+" bound="+(d==null?0:d.BoundCapacityCost)+" bindingCandidate="+(d!=null&&d.BindingCandidate)+" reason="+reason;
    }

    public static string RunVectors()
    {
        Ensure();List<string> e=ValidateAuthoring();string r;
        string[] hard={"animalZombieDog","animalZombieDog2","animalDireWolf","animalZombieBear","animalZombieBoar","animalBossGrace","animalZombieVulture","animalZombieVultureRadiated"};
        for(int i=0;i<hard.Length;i++)if(!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded(hard[i],null,out r))e.Add("PC002 hard exclusion missing: "+hard[i]);
        if(!RebirthAdvancedDisciplineRegistry.IsBeastmasterAnimalHardExcluded("futureAnimal",new string[]{"zombieAnimal"},out r))e.Add("zombieAnimal tag hard exclusion missing");
        RebirthBlackMagicZombieAnimalDefinition d;
        if(!ZombieAnimals.TryGetValue("animalZombieDog",out d)||!d.BlackMagicEligible||d.DominationCapacityCost!=1)e.Add("Zombie Dog classification mismatch");
        if(!ZombieAnimals.TryGetValue("animalZombieDog2",out d)||!d.BlackMagicEligible||d.DominationCapacityCost<2)e.Add("Zombie Dog 2 classification mismatch");
        if(!ZombieAnimals.TryGetValue("animalDireWolf",out d)||!d.BlackMagicEligible||d.DominationCapacityCost<2)e.Add("Dire Wolf classification mismatch");
        if(!ZombieAnimals.TryGetValue("animalZombieBear",out d)||!d.BlackMagicEligible||d.DominationCapacityCost<3)e.Add("Zombie Bear classification mismatch");
        if(!ZombieAnimals.TryGetValue("animalZombieBoar",out d)||d.BlackMagicEligible||!d.Protected)e.Add("Zombie Boar protected classification mismatch");
        if(!ZombieAnimals.TryGetValue("animalBossGrace",out d)||d.BlackMagicEligible||!d.Protected)e.Add("Grace protected classification mismatch");
        if(!ZombieAnimals.TryGetValue("animalZombieVulture",out d)||d.BlackMagicEligible)e.Add("Zombie Vulture must remain unsupported");
        if(!ZombieAnimals.TryGetValue("animalZombieVultureRadiated",out d)||d.BlackMagicEligible||!string.Equals(d.TierId,"radiated",StringComparison.OrdinalIgnoreCase))e.Add("Radiated Vulture classification mismatch");
        return "[REBIRTH BlackMagic] zombieAnimalVectors="+(e.Count==0?"PASS":"FAIL")+" failures="+e.Count+(e.Count==0?string.Empty:" "+string.Join(" | ",e.ToArray()));
    }

    private static List<string> ValidateAuthoring()
    {
        List<string> e=new List<string>();string[] required={"animalZombieDog","animalZombieDog2","animalDireWolf","animalZombieBear","animalZombieBoar","animalBossGrace","animalZombieVulture","animalZombieVultureRadiated"};
        for(int i=0;i<required.Length;i++)if(!ZombieAnimals.ContainsKey(required[i]))e.Add("missing audited zombie animal: "+required[i]);
        foreach(KeyValuePair<string,RebirthBlackMagicZombieAnimalDefinition> p in ZombieAnimals)
        {
            RebirthBlackMagicZombieAnimalDefinition d=p.Value;if(string.IsNullOrEmpty(d.TierId))e.Add("missing tier: "+p.Key);if(string.IsNullOrEmpty(d.RosterStatus))e.Add("missing roster status: "+p.Key);
            int tierWeight;if(!RebirthAdvancedDisciplineRegistry.TryGetZombieTierWeight(d.TierId,out tierWeight))e.Add("unknown undead tier: "+d.TierId+" for "+p.Key);
            if(d.BlackMagicEligible){if(d.Protected)e.Add("eligible target cannot be protected: "+p.Key);if(!string.Equals(d.RuntimeKind,"zombie_sdx",StringComparison.OrdinalIgnoreCase))e.Add("eligible zombie animal lacks zombie_sdx runtime: "+p.Key);if(d.DominationCapacityCost<1)e.Add("eligible target missing domination capacity: "+p.Key);if(d.BoundCapacityCost<1)e.Add("binding candidate missing bound capacity: "+p.Key);}
        }
        return e;
    }

    private static bool HasProtectedTag(EntityClass entityClass,out string reason)
    {
        reason=string.Empty;if(entityClass==null)return false;for(int i=0;i<ProtectedTags.Count;i++)if(entityClass.Tags.Test_AnySet(ProtectedTags[i].Value)){reason="DENY_PROTECTED_TAG: "+ProtectedTags[i].Key;return true;}return false;
    }
    private static int CountEligible(){int n=0;foreach(RebirthBlackMagicZombieAnimalDefinition d in ZombieAnimals.Values)if(d.BlackMagicEligible)n++;return n;}
    private static void Ensure(){if(!ready)LoadDefinitions();}
    private static string Attr(XElement e,string name){return e==null?string.Empty:((string)e.Attribute(name)??string.Empty).Trim();}
    private static bool BoolAttr(XElement e,string name,bool fallback){bool v;return bool.TryParse(Attr(e,name),out v)?v:fallback;}
    private static int IntAttr(XElement e,string name,int fallback){int v;return int.TryParse(Attr(e,name),NumberStyles.Integer,CultureInfo.InvariantCulture,out v)?v:fallback;}
}
