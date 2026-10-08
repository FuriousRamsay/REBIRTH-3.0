using System;
using System.Collections.Generic;

#nullable disable

public static class RebirthSurvivorDefinitionRegistry
{
    private static readonly object Gate = new object();
    private static RebirthSurvivorDefinitionBundle bundle;
    private static Dictionary<string,RebirthBackgroundDefinition> backgrounds = Empty<RebirthBackgroundDefinition>();
    private static Dictionary<string,RebirthTraitDefinition> traits = Empty<RebirthTraitDefinition>();
    private static Dictionary<string,RebirthDietDefinition> diets = Empty<RebirthDietDefinition>();
    private static Dictionary<string,RebirthSkillDefinition> skills = Empty<RebirthSkillDefinition>();
    private static Dictionary<string,RebirthAttributeDefinition> attributes = Empty<RebirthAttributeDefinition>();
    private static Dictionary<string,RebirthKnowledgeDefinition> knowledge = Empty<RebirthKnowledgeDefinition>();
    private static Dictionary<string,RebirthConditionModifierProfileDefinition> modifiers = Empty<RebirthConditionModifierProfileDefinition>();
    private static Dictionary<string,RebirthTraitSupportProfileDefinition> support = Empty<RebirthTraitSupportProfileDefinition>();
    private static Dictionary<string,RebirthTraitSupportProfileDefinition> supportByItem = Empty<RebirthTraitSupportProfileDefinition>();
    private static Dictionary<string,RebirthTraitSupportProfileDefinition> supportByEquipmentCVar = Empty<RebirthTraitSupportProfileDefinition>();
    private static Dictionary<string,RebirthTraitSupportProfileDefinition> supportByGearItem = Empty<RebirthTraitSupportProfileDefinition>();
    private static string semanticHash = string.Empty;
    private static string definitionVersion = "survivor-v1-unavailable";
    private static int generation = 1;

    public static bool IsReady { get { return bundle != null; } }
    public static RebirthSurvivorDefinitionBundle Bundle { get { return bundle; } }
    public static string SemanticHash { get { return semanticHash; } }
    public static string DefinitionVersion { get { return definitionVersion; } }
    public static int Generation { get { lock(Gate) return generation; } }

    public static void Install(RebirthSurvivorDefinitionBundle loaded)
    {
        if(loaded==null||loaded.Progression==null) throw new ArgumentNullException("loaded");
        Dictionary<string,RebirthBackgroundDefinition> bg=Build(loaded.Backgrounds,d=>d.Id,"Background");
        Dictionary<string,RebirthTraitDefinition> tr=Build(loaded.Traits,d=>d.Id,"Trait");
        Dictionary<string,RebirthDietDefinition> di=Build(loaded.Diets,d=>d.Id,"Diet");
        Dictionary<string,RebirthSkillDefinition> sk=Build(loaded.Progression.Skills,d=>d.Id,"Skill");
        Dictionary<string,RebirthAttributeDefinition> at=Build(loaded.Progression.Attributes,d=>d.Id,"Attribute");
        Dictionary<string,RebirthKnowledgeDefinition> kn=Build(loaded.Progression.Knowledge,d=>d.Id,"Knowledge");
        Dictionary<string,RebirthConditionModifierProfileDefinition> mo=Build(loaded.ModifierProfiles,d=>d.Id,"Modifier profile");
        Dictionary<string,RebirthTraitSupportProfileDefinition> su=Build(loaded.SupportProfiles,d=>d.Id,"Support profile");
        Dictionary<string,RebirthTraitSupportProfileDefinition> byItem=Empty<RebirthTraitSupportProfileDefinition>();
        Dictionary<string,RebirthTraitSupportProfileDefinition> byEquip=Empty<RebirthTraitSupportProfileDefinition>();
        Dictionary<string,RebirthTraitSupportProfileDefinition> byGearItem=Empty<RebirthTraitSupportProfileDefinition>();
        foreach(RebirthTraitSupportProfileDefinition profile in loaded.SupportProfiles)
        {
            foreach(string itemId in profile.ItemBindings)
            {
                if(byItem.ContainsKey(itemId)) throw new InvalidOperationException("Support item binding is duplicated: '"+itemId+"'.");
                byItem.Add(itemId,profile);
            }
            if(!string.IsNullOrEmpty(profile.EquipmentCVar))
            {
                if(byEquip.ContainsKey(profile.EquipmentCVar)) throw new InvalidOperationException("Support equipment CVar binding is duplicated: '"+profile.EquipmentCVar+"'.");
                byEquip.Add(profile.EquipmentCVar,profile);
            }
            if(!string.IsNullOrEmpty(profile.GearItemId))
            {
                if(byGearItem.ContainsKey(profile.GearItemId)) throw new InvalidOperationException("Survivor gear item binding is duplicated: '"+profile.GearItemId+"'.");
                byGearItem.Add(profile.GearItemId,profile);
            }
        }
        string hash=RebirthSurvivorDefinitionVersion.ComputeSemanticHash(loaded);
        lock(Gate) { bundle=loaded;backgrounds=bg;traits=tr;diets=di;skills=sk;attributes=at;knowledge=kn;modifiers=mo;support=su;supportByItem=byItem;supportByEquipmentCVar=byEquip;supportByGearItem=byGearItem;semanticHash=hash;definitionVersion=RebirthSurvivorDefinitionVersion.BuildVersionLabel(hash);generation=generation==int.MaxValue?1:generation+1; }
    }

    public static void Clear()
    {
        lock(Gate) { bundle=null;backgrounds=Empty<RebirthBackgroundDefinition>();traits=Empty<RebirthTraitDefinition>();diets=Empty<RebirthDietDefinition>();skills=Empty<RebirthSkillDefinition>();attributes=Empty<RebirthAttributeDefinition>();knowledge=Empty<RebirthKnowledgeDefinition>();modifiers=Empty<RebirthConditionModifierProfileDefinition>();support=Empty<RebirthTraitSupportProfileDefinition>();supportByItem=Empty<RebirthTraitSupportProfileDefinition>();supportByEquipmentCVar=Empty<RebirthTraitSupportProfileDefinition>();supportByGearItem=Empty<RebirthTraitSupportProfileDefinition>();semanticHash=string.Empty;definitionVersion="survivor-v"+RebirthSurvivorDefinitionVersion.SchemaVersion+"-unavailable";generation=generation==int.MaxValue?1:generation+1; }
    }

    public static bool TryGetBackground(string id,out RebirthBackgroundDefinition value)
    {
        string normalized=NormalizeLegacyBackgroundId(id);
        return backgrounds.TryGetValue(normalized,out value);
    }

    public static string NormalizeLegacyBackgroundId(string id)
    {
        string value=(id??string.Empty).Trim();
        if(string.Equals(value,"background.nurse",StringComparison.OrdinalIgnoreCase) || string.Equals(value,"background.doctor",StringComparison.OrdinalIgnoreCase)) return "background.paramedic";
        if(string.Equals(value,"background.carpenter",StringComparison.OrdinalIgnoreCase)) return "background.construction_worker";
        if(string.Equals(value,"background.machinist",StringComparison.OrdinalIgnoreCase)) return "background.welder_fabricator";
        if(string.Equals(value,"background.truck_driver",StringComparison.OrdinalIgnoreCase)) return "background.mechanic";
        if(string.Equals(value,"background.security_guard",StringComparison.OrdinalIgnoreCase)) return "background.police_officer";
        if(string.Equals(value,"background.warehouse_worker",StringComparison.OrdinalIgnoreCase)) return "background.clean_slate";
        return value;
    }
    public static bool TryGetTrait(string id,out RebirthTraitDefinition value) { return traits.TryGetValue(id??string.Empty,out value); }
    public static bool TryGetDiet(string id,out RebirthDietDefinition value) { return diets.TryGetValue(id??string.Empty,out value); }
    public static bool TryGetSkill(string id,out RebirthSkillDefinition value) { return skills.TryGetValue(id??string.Empty,out value); }
    public static bool TryGetAttribute(string id,out RebirthAttributeDefinition value) { return attributes.TryGetValue(id??string.Empty,out value); }
    public static bool TryGetKnowledge(string id,out RebirthKnowledgeDefinition value) { return knowledge.TryGetValue(id??string.Empty,out value); }
    public static bool TryGetModifier(string id,out RebirthConditionModifierProfileDefinition value) { return modifiers.TryGetValue(id??string.Empty,out value); }
    public static bool TryGetSupport(string id,out RebirthTraitSupportProfileDefinition value) { return support.TryGetValue(id??string.Empty,out value); }
    public static bool TryGetSupportByItem(string itemId,out RebirthTraitSupportProfileDefinition value) { return supportByItem.TryGetValue(itemId??string.Empty,out value); }
    public static bool TryGetSupportByEquipmentCVar(string cvar,out RebirthTraitSupportProfileDefinition value) { return supportByEquipmentCVar.TryGetValue(cvar??string.Empty,out value); }
    public static bool TryGetSupportByGearItem(string itemId,out RebirthTraitSupportProfileDefinition value) { return supportByGearItem.TryGetValue(itemId??string.Empty,out value); }

    private static Dictionary<string,T> Empty<T>() { return new Dictionary<string,T>(StringComparer.OrdinalIgnoreCase); }
    private static Dictionary<string,T> Build<T>(IEnumerable<T> source,Func<T,string> getId,string kind)
    {
        Dictionary<string,T> result=new Dictionary<string,T>(StringComparer.OrdinalIgnoreCase);
        foreach(T value in source) { string id=getId(value)??string.Empty; if(result.ContainsKey(id)) throw new InvalidOperationException(kind+" ID is duplicated: '"+id+"'."); result.Add(id,value); }
        return result;
    }
}
