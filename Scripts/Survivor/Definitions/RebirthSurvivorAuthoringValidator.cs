using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

#nullable disable

public sealed class RebirthSurvivorAuthoringReport
{
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    public bool IsValid { get { return Errors.Count == 0; } }

    public string BuildText(string hash, string version)
    {
        StringBuilder b=new StringBuilder();
        b.Append("[REBIRTH Survivor] authoring validation result=").Append(IsValid?"PASS":"FAIL")
            .Append(" errors=").Append(Errors.Count).Append(" warnings=").Append(Warnings.Count)
            .Append(" version=").Append(version??string.Empty).Append(" hash=").Append(hash??string.Empty).AppendLine();
        for(int i=0;i<Errors.Count;i++) b.Append("  ERROR: ").Append(Errors[i]).AppendLine();
        for(int i=0;i<Warnings.Count;i++) b.Append("  WARN: ").Append(Warnings[i]).AppendLine();
        return b.ToString().TrimEnd();
    }
}

public static class RebirthSurvivorAuthoringValidator
{
    private static readonly string[] ExpectedBackgroundIds =
    {
        "background.clean_slate","background.personal_trainer","background.paramedic","background.pharmacist","background.lab_technician","background.librarian","background.mechanic","background.electrician","background.construction_worker","background.welder_fabricator","background.maintenance_technician","background.engineer","background.miner","background.logger","background.firefighter","background.soldier","background.police_officer","background.hunter","background.park_ranger_outdoor_guide","background.gunsmith","background.chef","background.tailor","background.farmer","background.butcher","background.bartender","background.salesperson","background.teacher","background.scavenger"
    };

    public static RebirthSurvivorAuthoringReport Validate(RebirthSurvivorDefinitionBundle bundle)
    {
        RebirthSurvivorAuthoringReport r=new RebirthSurvivorAuthoringReport();
        if(bundle==null||bundle.Progression==null) { r.Errors.Add("Definition bundle/progression is unavailable."); return r; }

        Dictionary<string,RebirthBackgroundDefinition> bg=Unique(bundle.Backgrounds,d=>d.Id,"Background",r);
        Dictionary<string,RebirthTraitDefinition> tr=Unique(bundle.Traits,d=>d.Id,"Trait",r);
        Dictionary<string,RebirthDietDefinition> di=Unique(bundle.Diets,d=>d.Id,"Diet",r);
        Dictionary<string,RebirthSkillDefinition> sk=Unique(bundle.Progression.Skills,d=>d.Id,"Skill",r);
        Dictionary<string,RebirthAttributeDefinition> at=Unique(bundle.Progression.Attributes,d=>d.Id,"Attribute",r);
        string[] expectedAttributeIds={"strength","dexterity","constitution","intelligence","charisma"};
        if(at.Count!=expectedAttributeIds.Length) r.Errors.Add("Expected exactly five Attributes; found "+at.Count+".");
        for(int i=0;i<expectedAttributeIds.Length;i++)
        {
            RebirthAttributeDefinition ad;
            if(!at.TryGetValue(expectedAttributeIds[i],out ad)||ad==null) { r.Errors.Add("Missing required Attribute '"+expectedAttributeIds[i]+"'."); continue; }
            if(Math.Abs(ad.BaseCurrent-50f)>0.0001f||Math.Abs(ad.BasePotential-75f)>0.0001f||Math.Abs(ad.Min)>0.0001f||Math.Abs(ad.Max-100f)>0.0001f)
                r.Errors.Add("Attribute '"+ad.Id+"' must use 0/50/75/100 min/baseCurrent/basePotential/max.");
        }
        if(at.ContainsKey("perception")) r.Errors.Add("Perception is not a Rebirth 3.0 Attribute.");
        Dictionary<string,RebirthKnowledgeDefinition> kn=Unique(bundle.Progression.Knowledge,d=>d.Id,"Knowledge",r);
        Dictionary<string,RebirthConditionModifierProfileDefinition> mo=Unique(bundle.ModifierProfiles,d=>d.Id,"Modifier profile",r);
        Dictionary<string,RebirthTraitSupportProfileDefinition> su=Unique(bundle.SupportProfiles,d=>d.Id,"Support profile",r);
        Dictionary<string,RebirthConditionCapGroupDefinition> caps=Unique(bundle.CapGroups,d=>d.Id,"Cap group",r);

        if(bg.Count!=28) r.Errors.Add("Expected exactly 28 launch Backgrounds after Experience consolidation; found "+bg.Count+".");
        for(int i=0;i<ExpectedBackgroundIds.Length;i++) if(!bg.ContainsKey(ExpectedBackgroundIds[i])) r.Errors.Add("Missing launch Background '"+ExpectedBackgroundIds[i]+"'.");
        // Trait/Diet Revision 2 keeps the old profession-style Trait definitions as Deferred for
        // profile/history compatibility, adds eight curated universal Skill weaknesses plus
        // four broad Attribute aptitude Traits, and generates two Skill Aptitudes for every Skill in the authoritative Skill registry.
        int universal=0,restricted=0,deferred=0,aptitudes=0;
        foreach(RebirthTraitDefinition t in bundle.Traits)
        {
            if(t.Availability==RebirthDefinitionAvailability.Universal) universal++;
            else if(t.Availability==RebirthDefinitionAvailability.Restricted) restricted++;
            else if(t.Availability==RebirthDefinitionAvailability.Deferred) deferred++;
            if(RebirthSkillAptitudeTraitFactory.IsAptitude(t)) aptitudes++;
        }
        int aptitudeEligibleSkills=0;foreach(RebirthSkillDefinition sd in bundle.Progression.Skills)if(sd!=null&&!sd.Advanced)aptitudeEligibleSkills++;
        int expectedAptitudes=aptitudeEligibleSkills*RebirthSkillAptitudeTraitFactory.MaxTier;
        int expectedTraitCount=145+expectedAptitudes;
        int expectedUniversal=58+expectedAptitudes;
        if(tr.Count!=expectedTraitCount) r.Errors.Add("Expected "+expectedTraitCount+" Trait definitions (145 authored + "+expectedAptitudes+" generated Aptitudes); found "+tr.Count+".");
        if(universal!=expectedUniversal) r.Errors.Add("Expected "+expectedUniversal+" universal Traits after generated Skill Aptitudes; found "+universal+".");
        if(restricted!=15) r.Errors.Add("Expected 15 active Background-restricted Traits; found "+restricted+".");
        if(deferred!=72) r.Errors.Add("Expected 72 deferred legacy/profession Traits; found "+deferred+".");
        if(aptitudes!=expectedAptitudes) r.Errors.Add("Expected exactly "+expectedAptitudes+" generated Skill Aptitudes ("+aptitudeEligibleSkills+" non-Advanced Skills x 2 tiers); found "+aptitudes+".");
        if(bundle.Progression.MaxNegativeTraitRefund!=-1) r.Errors.Add("Current Survivor Trait economy requires max_negative_trait_refund=-1 (unlimited); found "+bundle.Progression.MaxNegativeTraitRefund+".");
        if(di.Count!=4) r.Errors.Add("Expected exactly four launch Diets; found "+di.Count+".");
        string[] expectedDiets={RebirthSurvivorIds.DietUnrestricted,RebirthSurvivorIds.DietVegetarian,RebirthSurvivorIds.DietVegan,RebirthSurvivorIds.DietCarnivore};
        for(int i=0;i<expectedDiets.Length;i++) if(!di.ContainsKey(expectedDiets[i])) r.Errors.Add("Missing launch Diet '"+expectedDiets[i]+"'.");
        if(di.ContainsKey("diet.pescatarian")) r.Errors.Add("A new serialized diet.pescatarian ID must not be introduced; the Pescatarian display/rule intentionally preserves legacy id diet.vegetarian for profile compatibility.");
        if(di.ContainsKey(RebirthSurvivorIds.DietUnrestricted) && di[RebirthSurvivorIds.DietUnrestricted].Points!=0) r.Errors.Add("Unrestricted Diet must have a 0 Trait-point adjustment.");
        if(di.ContainsKey(RebirthSurvivorIds.DietVegetarian) && di[RebirthSurvivorIds.DietVegetarian].Points!=2) r.Errors.Add("Pescatarian display Diet (legacy id diet.vegetarian) must grant +2 Trait points.");
        if(di.ContainsKey(RebirthSurvivorIds.DietVegan) && di[RebirthSurvivorIds.DietVegan].Points!=4) r.Errors.Add("Vegan Diet must grant +4 Trait points in Diet Revision 2.1.");
        if(di.ContainsKey(RebirthSurvivorIds.DietCarnivore) && di[RebirthSurvivorIds.DietCarnivore].Points!=3) r.Errors.Add("Carnivore Diet must grant +3 Trait points in Diet Revision 2.1.");
        foreach(RebirthDietDefinition d in bundle.Diets)
        {
            Required(d.Id,d.NameKey,"Diet name_key",r); Required(d.Id,d.DescriptionKey,"Diet description_key",r); Required(d.Id,d.IconKey,"Diet icon_key",r); Required(d.Id,d.RuleSummary,"Diet rule_summary",r);
            string expectedRule=string.Empty;
            if(string.Equals(d.Id,RebirthSurvivorIds.DietUnrestricted,StringComparison.OrdinalIgnoreCase)) expectedRule="unrestricted";
            else if(string.Equals(d.Id,RebirthSurvivorIds.DietVegetarian,StringComparison.OrdinalIgnoreCase)) expectedRule="pescatarian";
            else if(string.Equals(d.Id,RebirthSurvivorIds.DietVegan,StringComparison.OrdinalIgnoreCase)) expectedRule="vegan";
            else if(string.Equals(d.Id,RebirthSurvivorIds.DietCarnivore,StringComparison.OrdinalIgnoreCase)) expectedRule="carnivore";
            if(expectedRule.Length==0) r.Errors.Add(d.Id+" has no approved launch Diet composition rule.");
            else if(!string.Equals(d.CompositionRule,expectedRule,StringComparison.OrdinalIgnoreCase)) r.Errors.Add(d.Id+" composition_rule must be '"+expectedRule+"'; found '"+d.CompositionRule+"'.");
        }
        string[] currentSkillIds=RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds();
        if(sk.Count!=currentSkillIds.Length) r.Errors.Add("Expected exactly "+currentSkillIds.Length+" current Skills from migration/runtime authority; found "+sk.Count+".");
        for(int i=0;i<currentSkillIds.Length;i++) if(!sk.ContainsKey(currentSkillIds[i])) r.Errors.Add("Missing current Skill '"+currentSkillIds[i]+"'.");
        foreach(RebirthSkillDefinition skill in bundle.Progression.Skills)
        {
            if(skill==null) continue;
            if(string.IsNullOrWhiteSpace(skill.PrimaryAttributeId)) r.Errors.Add(skill.Id+" is missing primary_attribute.");
            else if(!at.ContainsKey(skill.PrimaryAttributeId)) r.Errors.Add(skill.Id+" references unknown primary_attribute '"+skill.PrimaryAttributeId+"'.");
            else if(string.Equals(skill.PrimaryAttributeId,"perception",StringComparison.OrdinalIgnoreCase)) r.Errors.Add(skill.Id+" must not map to removed Perception.");
            bool animalHandling=string.Equals(skill.Id,RebirthSurvivorIds.SkillAnimalHandling,StringComparison.OrdinalIgnoreCase);
            bool blackMagic=string.Equals(skill.Id,RebirthSurvivorIds.SkillBlackMagic,StringComparison.OrdinalIgnoreCase);
            bool rage=string.Equals(skill.Id,RebirthSurvivorIds.SkillRage,StringComparison.OrdinalIgnoreCase);
            float expectedMin=(blackMagic||rage)?0f:-50f;
            if((blackMagic||rage)&&!skill.Advanced) r.Errors.Add("Black Magic must be authored as an Advanced Skill.");
            if(!blackMagic&&!rage&&skill.Advanced) r.Errors.Add(skill.Id+" is unexpectedly marked Advanced before its owning discipline chunk.");
            if(Math.Abs(skill.Min-expectedMin)>0.0001f || Math.Abs(skill.Max-100f)>0.0001f)
                r.Errors.Add(skill.Id+" must use runtime bounds "+expectedMin.ToString(System.Globalization.CultureInfo.InvariantCulture)+"..100; found ["+skill.Min+","+skill.Max+"].");
        }
        if(tr.ContainsKey(RebirthSkillAptitudeTraitFactory.BuildId(RebirthSurvivorIds.SkillBlackMagic,1))||tr.ContainsKey(RebirthSkillAptitudeTraitFactory.BuildId(RebirthSurvivorIds.SkillBlackMagic,2))) r.Errors.Add("Advanced Black Magic must not generate Survivor-creation Aptitudes.");
        if(tr.ContainsKey(RebirthSkillAptitudeTraitFactory.BuildId(RebirthSurvivorIds.SkillRage,1))||tr.ContainsKey(RebirthSkillAptitudeTraitFactory.BuildId(RebirthSurvivorIds.SkillRage,2))) r.Errors.Add("Advanced Rage must not generate Survivor-creation Aptitudes.");
        if(Math.Abs(bundle.Progression.CreationSkillMax-50f)>0.0001f)
            r.Errors.Add("Revision-4 creation Skill ceiling must be +50; found "+bundle.Progression.CreationSkillMax+".");
        if(bundle.Progression.CreationSkillMin < -50f || bundle.Progression.CreationSkillMin > bundle.Progression.CreationSkillMax)
            r.Errors.Add("Creation Skill floor is outside the signed runtime/creation contract: "+bundle.Progression.CreationSkillMin+".");
        if(Math.Abs(bundle.Progression.SkillKnowledgeMin-0f)>0.0001f || Math.Abs(bundle.Progression.SkillKnowledgeMax-100f)>0.0001f)
            r.Errors.Add("Skill Knowledge runtime bounds must be 0..100; found ["+bundle.Progression.SkillKnowledgeMin+","+bundle.Progression.SkillKnowledgeMax+"].");
        // The 27 legacy domain Knowledge definitions are retained for schema-6 compatibility,
        // while individual recipe/procedure discoveries are intentionally added as recipe.* entries.
        // Do not hard-code the total Knowledge count: it must grow as the authored discovery catalogue grows.
        int legacyKnowledgeCount=0;
        foreach(RebirthKnowledgeDefinition knowledge in bundle.Progression.Knowledge)
        {
            if(knowledge!=null && knowledge.Id!=null && knowledge.Id.StartsWith("knowledge.",StringComparison.OrdinalIgnoreCase)) legacyKnowledgeCount++;
        }
        if(legacyKnowledgeCount<27) r.Errors.Add("Expected at least the 27 compatibility domain Knowledge definitions; found "+legacyKnowledgeCount+". Total authored Knowledge including Advanced Disciplines="+kn.Count+".");
        string[] requiredLaunchSupport={"support.nicotine_patch","support.probiotic_capsules","support.electrolyte_drink","support.instant_cooling_pack","support.heat_pack","support.leisure_puzzle_book","support.seasoning_hot_sauce","support.respiratory_inhaler","support.compression_wrap","support.knee_brace_mod","support.back_support_belt_mod","support.ergonomic_grip_support_glove_mod"};
        for(int i=0;i<requiredLaunchSupport.Length;i++) if(!su.ContainsKey(requiredLaunchSupport[i])) r.Errors.Add("Missing required launch support profile '"+requiredLaunchSupport[i]+"'.");
        string[] requiredBackpacks={"gear.backpack.daypack","gear.backpack.field_pack","gear.backpack.hiking_pack","gear.backpack.expedition_pack","gear.backpack.expanded_daypack","gear.backpack.expanded_field_pack","gear.backpack.expanded_hiking_pack","gear.backpack.expanded_expedition_pack"};
        string[] requiredWeatherproofGear={"gear.outerwear.cold_weather_lining","gear.outerwear.hot_weather_shell"};
        for(int i=0;i<requiredBackpacks.Length;i++) if(!su.ContainsKey(requiredBackpacks[i])) r.Errors.Add("Missing required Survivor backpack profile '"+requiredBackpacks[i]+"'.");
        for(int i=0;i<requiredWeatherproofGear.Length;i++) if(!su.ContainsKey(requiredWeatherproofGear[i])) r.Errors.Add("Missing required Weatherproofing gear support profile '"+requiredWeatherproofGear[i]+"'.");

        foreach(RebirthBackgroundDefinition b in bundle.Backgrounds)
        {
            Required(b.Id,b.NameKey,"Background name_key",r); Required(b.Id,b.DescriptionKey,"Background description_key",r); Required(b.Id,b.BackgroundArtKey,"backgroundArtKey",r); Required(b.Id,b.BackgroundArtAltTextKey,"backgroundArtAltTextKey",r);
            ValidateRefs(b.Id,b.RestrictedTraitIds,tr,"restricted Trait",r); ValidateRefs(b.Id,b.FavoredTraitIds,tr,"favored Trait",r); ValidateRefs(b.Id,b.BlockedTraitIds,tr,"blocked Trait",r);
            foreach(string tid in b.RestrictedTraitIds)
            {
                RebirthTraitDefinition t; if(!tr.TryGetValue(tid,out t)) continue;
                if(t.Availability!=RebirthDefinitionAvailability.Restricted) r.Errors.Add(b.Id+" lists non-restricted Trait '"+tid+"' as Background-restricted.");
                if(!Contains(t.AllowedBackgroundIds,b.Id)) r.Errors.Add(b.Id+" lists restricted Trait '"+tid+"' but that Trait does not allow the Background.");
            }
            HashSet<string> backgroundAttributeIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(RebirthStartingAttributeDefinition a in b.StartingAttributes)
            {
                if(a==null) { r.Errors.Add(b.Id+" contains a null starting Attribute."); continue; }
                RebirthAttributeDefinition definition;
                if(!backgroundAttributeIds.Add(a.AttributeId)) r.Errors.Add(b.Id+" repeats starting Attribute '"+a.AttributeId+"'.");
                if(!at.TryGetValue(a.AttributeId,out definition)) { r.Errors.Add(b.Id+" references unknown starting Attribute '"+a.AttributeId+"'."); continue; }
                if(float.IsNaN(a.Current)||float.IsInfinity(a.Current)||a.Current<definition.Min||a.Current>definition.Max)
                    r.Errors.Add(b.Id+" starting Attribute '"+a.AttributeId+"' is outside its finite runtime bounds.");
            }
            float positiveSkillTotal=0f,negativeSkillTotal=0f;
            HashSet<string> backgroundSkillIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(RebirthStartingSkillBiasDefinition s in b.StartingSkills)
            {
                RebirthSkillDefinition skillDef;
                if(!backgroundSkillIds.Add(s.SkillId)) r.Errors.Add(b.Id+" declares starting Skill '"+s.SkillId+"' more than once.");
                if(!sk.TryGetValue(s.SkillId,out skillDef)) { r.Errors.Add(b.Id+" references unknown starting Skill '"+s.SkillId+"'."); continue; }
                float resolvedValue=0f;
                if(s.HasExplicitValue)
                {
                    resolvedValue=s.Value;
                    if(s.Value<skillDef.Min || s.Value>skillDef.Max) r.Errors.Add(b.Id+" starting Skill '"+s.SkillId+"' value "+s.Value+" is outside runtime ["+skillDef.Min+","+skillDef.Max+"].");
                    if(s.Value<bundle.Progression.CreationSkillMin || s.Value>bundle.Progression.CreationSkillMax) r.Errors.Add(b.Id+" starting Skill '"+s.SkillId+"' value "+s.Value+" is outside normal creation ["+bundle.Progression.CreationSkillMin+","+bundle.Progression.CreationSkillMax+"].");
                }
                else
                {
                    int tierValue;
                    if(!bundle.Progression.SkillBiasTiers.TryGetValue(s.TierId,out tierValue)) r.Errors.Add(b.Id+" references unknown Skill bias tier '"+s.TierId+"'.");
                    else resolvedValue=tierValue;
                }
                if(resolvedValue>0f) positiveSkillTotal+=resolvedValue; else if(resolvedValue<0f) negativeSkillTotal+=resolvedValue;
            }

            HashSet<string> backgroundSkillKnowledgeIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(RebirthStartingSkillKnowledgeDefinition k in b.StartingSkillKnowledge)
            {
                if(k==null) { r.Errors.Add(b.Id+" contains a null starting Skill Knowledge entry."); continue; }
                if(!backgroundSkillKnowledgeIds.Add(k.SkillId)) r.Errors.Add(b.Id+" declares starting Skill Knowledge '"+k.SkillId+"' more than once.");
                if(!sk.ContainsKey(k.SkillId)) r.Errors.Add(b.Id+" references unknown Skill Knowledge area '"+k.SkillId+"'.");
                if(float.IsNaN(k.Value)||float.IsInfinity(k.Value)||k.Value<bundle.Progression.SkillKnowledgeMin||k.Value>bundle.Progression.SkillKnowledgeMax)
                    r.Errors.Add(b.Id+" starting Skill Knowledge '"+k.SkillId+"' value "+k.Value+" is outside ["+bundle.Progression.SkillKnowledgeMin+","+bundle.Progression.SkillKnowledgeMax+"].");
            }

            // Background Balance Revision 3 contract. Clean Slate deliberately has no profession
            // strengths/weaknesses and converts that absence into maximum Trait flexibility. Every
            // profession must instead carry a real tradeoff so one career cannot receive a huge
            // positive Skill package with no starting weakness.
            bool cleanSlate=string.Equals(b.Id,RebirthSurvivorIds.BackgroundCleanSlate,StringComparison.OrdinalIgnoreCase);
            int preDietTraitPoints=bundle.Progression.BaseCreationPoints+b.CreationPointModifier+(cleanSlate?bundle.Progression.CleanSlateBonus:0);
            if(cleanSlate)
            {
                if(b.StartingSkills.Count!=0) r.Errors.Add("Clean Slate must not declare starting Skill adjustments.");
                if(b.StartingSkillKnowledge.Count!=0) r.Errors.Add("Clean Slate must not declare profession starting Skill Knowledge.");
                if(preDietTraitPoints!=12) r.Errors.Add("Background Balance Revision 3 requires Clean Slate to start with exactly 12 pre-Diet Trait points; found "+preDietTraitPoints+".");
            }
            else
            {
                if(b.StartingSkillKnowledge.Count==0) r.Errors.Add(b.Id+" must declare at least one intentional starting Skill Knowledge value.");
                if(positiveSkillTotal<=0f) r.Errors.Add(b.Id+" must have at least one positive starting Skill adjustment.");
                if(negativeSkillTotal>-10f) r.Errors.Add(b.Id+" must carry at least -10 total starting Skill weakness; found "+negativeSkillTotal+".");
                if(negativeSkillTotal<-20f) r.Errors.Add(b.Id+" exceeds the -20 starting Skill weakness cap; found "+negativeSkillTotal+".");
                if(positiveSkillTotal>75f) r.Errors.Add(b.Id+" exceeds the +75 positive starting Skill package cap; found "+positiveSkillTotal+".");
                if(preDietTraitPoints<5||preDietTraitPoints>10) r.Errors.Add(b.Id+" pre-Diet Trait points must be in the balanced profession range [5,10]; found "+preDietTraitPoints+".");
            }
            foreach(string kid in b.StartingKnowledgeIds) if(!kn.ContainsKey(kid)) r.Errors.Add(b.Id+" references unknown Knowledge '"+kid+"'.");
            if(b.StartingItems==null || b.StartingItems.Count==0) r.Errors.Add(b.Id+" must declare at least one starting item for Survivor UI/runtime authoring.");
            else
            {
                HashSet<string> starterItemIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(RebirthStartingItemDefinition item in b.StartingItems)
                {
                    if(item==null) { r.Errors.Add(b.Id+" contains a null starting item definition."); continue; }
                    Required(b.Id,item.ItemId,"starting item id",r); Required(b.Id,item.NameKey,"starting item name_key",r);
                    if(!starterItemIds.Add(item.ItemId)) r.Errors.Add(b.Id+" declares starting item '"+item.ItemId+"' more than once; use count instead.");
                    if(item.Count<1) r.Errors.Add(b.Id+" starting item '"+item.ItemId+"' must have count >= 1.");
                    if(item.HasQuality && item.Quality<1) r.Errors.Add(b.Id+" starting item '"+item.ItemId+"' must have quality >= 1.");
                }
            }
        }

        foreach(RebirthTraitDefinition t in bundle.Traits)
        {
            Required(t.Id,t.NameKey,"Trait name_key",r); Required(t.Id,t.DescriptionKey,"Trait description_key",r); Required(t.Id,t.IconKey,"Trait icon_key",r); Required(t.Id,t.ModifierId,"Trait modifier_id",r);
            if(t.Points<0) r.Errors.Add(t.Id+" has negative Points; polarity owns spend/grant semantics.");
            if(t.Polarity==RebirthTraitPolarity.Mixed&&t.Points!=0) r.Errors.Add(t.Id+" is Mixed but has non-zero points.");
            if(t.Availability==RebirthDefinitionAvailability.Restricted&&t.AllowedBackgroundIds.Count==0) r.Errors.Add(t.Id+" is restricted but has no allowed Backgrounds.");
            if(t.Availability==RebirthDefinitionAvailability.Universal&&t.AllowedBackgroundIds.Count!=0) r.Errors.Add(t.Id+" is universal but declares allowed Backgrounds.");
            ValidateRefs(t.Id,t.AllowedBackgroundIds,bg,"allowed Background",r); ValidateRefs(t.Id,t.ConflictTraitIds,tr,"conflicting Trait",r); ValidateRefs(t.Id,t.ConflictDietIds,di,"conflicting Diet",r);
            if(t.Availability==RebirthDefinitionAvailability.Restricted)
            {
                foreach(string backgroundId in t.AllowedBackgroundIds)
                {
                    RebirthBackgroundDefinition allowedBackground;
                    if(bg.TryGetValue(backgroundId,out allowedBackground) && !Contains(allowedBackground.RestrictedTraitIds,t.Id))
                        r.Errors.Add(t.Id+" allows Background '"+backgroundId+"' but that Background does not list the Trait in restricted_traits.");
                }
            }
            foreach(string otherId in t.ConflictTraitIds)
            {
                RebirthTraitDefinition other;
                if(!tr.TryGetValue(otherId,out other)) continue;
                // Generated Skill Aptitudes intentionally own their conflict edges to curated Skill-weakness Traits.
                // The authored weakness records do not enumerate 2 aptitude tiers x every affected Skill, so these
                // dynamic conflicts are valid even though the XML side is not symmetric. Runtime validation checks
                // either direction when presenting choices, and every selected Aptitude still carries the weakness ID.
                if(RebirthSkillAptitudeTraitFactory.IsAptitude(t) || RebirthSkillAptitudeTraitFactory.IsAptitude(other)) continue;
                if(!Contains(other.ConflictTraitIds,t.Id)) r.Errors.Add("Trait conflict is not symmetric: '"+t.Id+"' -> '"+otherId+"'.");
            }
            if(!RebirthSkillAptitudeTraitFactory.IsAptitude(t))
            {
                RebirthConditionModifierProfileDefinition mp; if(!mo.TryGetValue(t.ModifierId,out mp)) r.Errors.Add(t.Id+" references missing modifier profile '"+t.ModifierId+"'."); else if(!string.Equals(mp.OwnerTraitId,t.Id,StringComparison.OrdinalIgnoreCase)) r.Errors.Add(t.Id+" modifier profile owner mismatch: '"+mp.OwnerTraitId+"'.");
            }
        }

        HashSet<string> supportCoveredTraits=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(RebirthTraitSupportProfileDefinition support in bundle.SupportProfiles)
        {
            if(!string.IsNullOrEmpty(support.HabitTraitId)) supportCoveredTraits.Add(support.HabitTraitId);
            foreach(string traitId in support.SupportedTraitIds) if(!string.IsNullOrEmpty(traitId)) supportCoveredTraits.Add(traitId);
        }

        // Post-Revision-2 Chunk 3: every authored Trait has one canonical implementation state.
        // Generated Skill Aptitudes are intentionally CreationOnly and do not need modifier profiles.
        int implementedTraits=0,creationOnlyTraits=0,partialTraits=0,deferredTraits=0;
        List<string> selectablePartialTraits=new List<string>();
        foreach(RebirthTraitDefinition t in bundle.Traits)
        {
            if(t==null||RebirthSkillAptitudeTraitFactory.IsAptitude(t)) continue;
            RebirthConditionModifierProfileDefinition profile;
            if(!mo.TryGetValue(t.ModifierId,out profile)||profile==null) continue; // missing profile reported above
            RebirthTraitImplementationState state=RebirthTraitRuntimeReconciliation.Parse(profile.ImplementationState);
            if(state==RebirthTraitImplementationState.Unknown)
            {
                r.Errors.Add(t.Id+" has non-canonical implementation_state '"+profile.ImplementationState+"'. Expected IMPLEMENTED, CREATION_ONLY, PARTIAL, or DEFERRED.");
                continue;
            }
            bool hasCreation=false,hasRuntime=false;
            foreach(RebirthConditionModifierComponent c in profile.Components)
            {
                if(c==null)continue;
                if(string.Equals(c.Phase,"creation",StringComparison.OrdinalIgnoreCase))hasCreation=true;
                else if(string.Equals(c.Phase,"runtime",StringComparison.OrdinalIgnoreCase))hasRuntime=true;
            }
            bool hasSupport=supportCoveredTraits.Contains(t.Id);
            bool selectable=t.Availability!=RebirthDefinitionAvailability.Deferred;
            // Availability is a creation/selectability contract. Later implementation chunks may
            // legitimately implement the mechanics of a historically deferred profession Trait
            // without making that Trait selectable in Survivor creation. Do not conflate those.
            if(selectable && state==RebirthTraitImplementationState.Deferred)
                r.Errors.Add(t.Id+" is selectable but implementation_state is DEFERRED.");
            if(selectable && state==RebirthTraitImplementationState.Implemented && !hasRuntime && !hasSupport)
                r.Errors.Add(t.Id+" is IMPLEMENTED but has neither a runtime component nor a support binding.");
            if(selectable && state==RebirthTraitImplementationState.CreationOnly && (!hasCreation || hasRuntime))
                r.Errors.Add(t.Id+" is CREATION_ONLY but creation/runtime path is creation="+hasCreation+" runtime="+hasRuntime+".");
            if(selectable && state==RebirthTraitImplementationState.Partial && !hasCreation && !hasRuntime && !hasSupport)
                r.Errors.Add(t.Id+" is PARTIAL but has no mechanical path to reconcile.");

            switch(state)
            {
                case RebirthTraitImplementationState.Implemented:implementedTraits++;break;
                case RebirthTraitImplementationState.CreationOnly:creationOnlyTraits++;break;
                case RebirthTraitImplementationState.Partial:partialTraits++;if(t.Availability!=RebirthDefinitionAvailability.Deferred)selectablePartialTraits.Add(t.Id);break;
                case RebirthTraitImplementationState.Deferred:deferredTraits++;break;
            }
        }
        // Reconciliation counts are intentionally data-driven after the profession/background
        // implementation waves. Canonical state tokens and selectable-state safety are validated
        // above; do not freeze launch-era counts into runtime startup diagnostics.
        selectablePartialTraits.Sort(StringComparer.OrdinalIgnoreCase);
        if(selectablePartialTraits.Count>0)
            r.Warnings.Add("Selectable PARTIAL Traits require runtime smoke tests/final correction before release: "+string.Join(",",selectablePartialTraits.ToArray())+".");

        foreach(RebirthConditionModifierProfileDefinition m in bundle.ModifierProfiles)
        {
            if(!tr.ContainsKey(m.OwnerTraitId)) r.Errors.Add(m.Id+" owns unknown Trait '"+m.OwnerTraitId+"'.");
            if(!string.IsNullOrEmpty(m.CapGroup)&&!caps.ContainsKey(m.CapGroup)) r.Errors.Add(m.Id+" references unknown cap group '"+m.CapGroup+"'.");
            foreach(RebirthConditionModifierComponent c in m.Components)
            {
                if(!IsTraitComponentTarget(c.Target,c.Phase)) r.Errors.Add(m.Id+" has unsupported Trait component target/phase '"+c.Target+"' / '"+c.Phase+"'.");
                string op=(c.Operation??string.Empty).Trim().ToLowerInvariant();
                bool skillStartTarget=(c.Target??string.Empty).StartsWith("skill.start.",StringComparison.OrdinalIgnoreCase);
                bool supported=op=="add"||op=="multiply"||op=="grant"||op=="add_tier" ||
                    (skillStartTarget && (op=="set"||op=="clamp_min"||op=="clamp_max"));
                if(!supported) r.Errors.Add(m.Id+" has unsupported Trait component op '"+c.Operation+"'.");
            }
        }
        foreach(RebirthTraitDefinition t in bundle.Traits)
        {
            if(RebirthSkillAptitudeTraitFactory.IsAptitude(t)) continue;
            RebirthConditionModifierProfileDefinition profile;
            bool hasComponents=mo.TryGetValue(t.ModifierId,out profile)&&profile!=null&&profile.Components.Count>0;
            if(t.Availability!=RebirthDefinitionAvailability.Deferred && !hasComponents&&!supportCoveredTraits.Contains(t.Id))
                r.Errors.Add(t.Id+" has no mechanical launch path: no modifier components and no support-profile binding.");
        }
        HashSet<string> supportItemBindings=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> equipmentCVars=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> gearItems=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(RebirthTraitSupportProfileDefinition s in bundle.SupportProfiles)
        {
            Required(s.Id,s.NameKey,"Support name_key",r); ValidateRefs(s.Id,s.SupportedTraitIds,tr,"supported Trait",r);
            if(!IsSupportKind(s.Kind)) r.Errors.Add(s.Id+" has unsupported support kind '"+s.Kind+"'.");
            if(!IsSupportRepeatMode(s.RepeatMode)) r.Errors.Add(s.Id+" has unsupported repeat_mode '"+s.RepeatMode+"'.");
            if(!string.IsNullOrEmpty(s.HabitTraitId)&&!tr.ContainsKey(s.HabitTraitId)) r.Errors.Add(s.Id+" references unknown habit Trait '"+s.HabitTraitId+"'.");
            foreach(string itemId in s.ItemBindings) if(!supportItemBindings.Add(itemId)) r.Errors.Add("Support item binding is duplicated: '"+itemId+"'.");
            if(!string.IsNullOrEmpty(s.EquipmentCVar)&&!equipmentCVars.Add(s.EquipmentCVar)) r.Errors.Add("Support equipment CVar binding is duplicated: '"+s.EquipmentCVar+"'.");
            if(!string.IsNullOrEmpty(s.GearItemId)&&!gearItems.Add(s.GearItemId)) r.Errors.Add("Survivor gear item binding is duplicated: '"+s.GearItemId+"'.");
            if(s.Kind=="equipment_mod"&&string.IsNullOrEmpty(s.EquipmentCVar)) r.Errors.Add(s.Id+" is equipment_mod but has no equipment_cvar.");
            if(s.Kind=="survivor_gear")
            {
                if(string.IsNullOrEmpty(s.GearSlotId)) r.Errors.Add(s.Id+" is survivor_gear but has no gear_slot_id.");
                if(string.IsNullOrEmpty(s.GearSlotNameKey)) r.Errors.Add(s.Id+" is survivor_gear but has no gear_slot_name_key.");
                if(string.IsNullOrEmpty(s.GearItemId)) r.Errors.Add(s.Id+" is survivor_gear but has no gear_item_id.");
                if(!string.IsNullOrEmpty(s.EquipmentCVar)) r.Errors.Add(s.Id+" is survivor_gear and must not use the native equipment_cvar path.");
                if(s.GearBagSlotBonus>0 && !string.Equals(s.GearSlotId,RebirthSurvivorGearService.BackpackSlotId,StringComparison.OrdinalIgnoreCase)) r.Errors.Add(s.Id+" changes physical bag slots outside the Backpack Survivor slot.");
                if(s.GearBagSlotBonus<0 || RebirthSurvivorGearService.BasePhysicalBagSlots+s.GearBagSlotBonus>RebirthSurvivorGearService.MaxPhysicalBagSlots) r.Errors.Add(s.Id+" has an invalid bag_slot_bonus.");
                if(s.GearToolbeltSlotBonus>RebirthToolbeltCapacity.MaximumSlots-RebirthToolbeltCapacity.StartingSlots || (s.GearToolbeltSlotBonus>0 && s.GearSlotId!="belt")) r.Errors.Add(s.Id+" has invalid toolbelt slot bonus.");
                if(s.GearMinStrength>100f || s.GearMinConstitution>100f) r.Errors.Add(s.Id+" has a gear Attribute requirement above 100.");
            }
            // Playback equipment grants access to the music/study services, not a stat modifier.
            bool isWalkmanGear=s.Kind=="survivor_gear"
                && s.GearSlotId==RebirthSurvivorGearService.WalkmanSlotId
                && s.GearItemId==RebirthSurvivorGearService.WalkmanGearItemId;
            if(s.Effects.Count==0 && !isWalkmanGear && !(s.Kind=="survivor_gear" && (s.GearBagSlotBonus>0 || s.GearToolbeltSlotBonus>0))) r.Errors.Add(s.Id+" has no authored support effects.");
            foreach(RebirthTraitSupportEffectDefinition e in s.Effects)
            {
                string op=(e.Operation??string.Empty).ToLowerInvariant(); if(op!="multiply"&&op!="add"&&op!="relieve") r.Errors.Add(s.Id+" has unsupported effect op '"+e.Operation+"'.");
                string state=(e.State??string.Empty).ToLowerInvariant(); if(state!="positive"&&state!="managed"&&state!="unsatisfied"&&state!="equipped"&&state!="armed") r.Errors.Add(s.Id+" has unsupported effect state '"+e.State+"'.");
                if(!IsSupportEffectTarget(e.Target)) r.Errors.Add(s.Id+" has unsupported effect target '"+e.Target+"'.");
                string scope=e.Scope??string.Empty; if(scope.StartsWith("trait:",StringComparison.OrdinalIgnoreCase)&&!tr.ContainsKey(scope.Substring(6))) r.Errors.Add(s.Id+" effect references unknown scope Trait '"+scope.Substring(6)+"'.");
            }
        }

        HashSet<string> localization=ReadLocalizationKeys(bundle.ConfigRoot,r);
        if(localization!=null)
        {
            foreach(RebirthBackgroundDefinition b in bundle.Backgrounds) { CheckLoc(b.Id,b.NameKey,localization,r); CheckLoc(b.Id,b.DescriptionKey,localization,r); CheckLoc(b.Id,b.BackgroundArtAltTextKey,localization,r); }
            foreach(RebirthTraitDefinition t in bundle.Traits) { CheckLoc(t.Id,t.NameKey,localization,r); CheckLoc(t.Id,t.DescriptionKey,localization,r); }
            foreach(RebirthDietDefinition d in bundle.Diets) { CheckLoc(d.Id,d.NameKey,localization,r); CheckLoc(d.Id,d.DescriptionKey,localization,r); }
            foreach(RebirthAttributeDefinition a in bundle.Progression.Attributes) CheckLoc(a.Id,a.NameKey,localization,r);
            foreach(RebirthSkillDefinition s in bundle.Progression.Skills) { CheckLoc(s.Id,s.NameKey,localization,r); CheckLoc(s.Id,s.SourceKey,localization,r); }
            foreach(RebirthKnowledgeDefinition k in bundle.Progression.Knowledge) CheckLoc(k.Id,k.NameKey,localization,r);
            foreach(RebirthTraitSupportProfileDefinition s in bundle.SupportProfiles) { CheckLoc(s.Id,s.NameKey,localization,r); if(!string.IsNullOrEmpty(s.GearSlotNameKey)) CheckLoc(s.Id,s.GearSlotNameKey,localization,r); }
            for (int errorCodeValue = (int)RebirthSurvivorCreationErrorCode.RebirthModeDisabled; errorCodeValue <= (int)RebirthSurvivorCreationErrorCode.InvalidSkillBiasTier; errorCodeValue++)
            {
                RebirthSurvivorCreationErrorCode code = (RebirthSurvivorCreationErrorCode)errorCodeValue;
                CheckLoc("creation-error", "xuiRebirthCreationError" + code, localization, r);
            }
        }

        ValidateFinalVisualAssets(bundle,r);
        return r;
    }

    public static string BuildReport()
    {
        RebirthSurvivorDefinitionBundle b=RebirthSurvivorDefinitionRegistry.Bundle;
        RebirthSurvivorAuthoringReport r=Validate(b);
        return r.BuildText(RebirthSurvivorDefinitionRegistry.SemanticHash,RebirthSurvivorDefinitionRegistry.DefinitionVersion);
    }

    private static void ValidateFinalVisualAssets(RebirthSurvivorDefinitionBundle bundle,RebirthSurvivorAuthoringReport r)
    {
        try
        {
            string modRoot=Path.GetFullPath(Path.Combine(bundle.ConfigRoot,"..",".."));
            string backgroundRoot=Path.Combine(modRoot,"UIAssets","Survivor","Backgrounds");
            string backgroundThumbnailRoot=Path.Combine(modRoot,"UIAssets","Survivor","BackgroundThumbnails");
            string iconRoot=Path.Combine(modRoot,"UIAtlases","RebirthSurvivorIcons");
            string atlasSettings=Path.Combine(iconRoot,"settings.xml");
            HashSet<string> atlasSprites=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(!File.Exists(atlasSettings)) r.Errors.Add("Survivor icon atlas settings are missing: '"+atlasSettings+"'.");
            else
            {
                XmlDocument doc=new XmlDocument(); doc.Load(atlasSettings);
                XmlNodeList nodes=doc.SelectNodes("/sprites/sprite[@name]");
                if(nodes!=null) foreach(XmlNode node in nodes)
                {
                    string name=node.Attributes!=null&&node.Attributes["name"]!=null?(node.Attributes["name"].Value??string.Empty).Trim():string.Empty;
                    if(name.Length==0) { r.Errors.Add("Survivor icon atlas contains an empty sprite registration."); continue; }
                    if(!atlasSprites.Add(name)) r.Errors.Add("Survivor icon atlas sprite is registered more than once: '"+name+"'.");
                }
            }
            foreach(RebirthBackgroundDefinition b in bundle.Backgrounds)
            {
                string path=Path.Combine(backgroundRoot,(b.BackgroundArtKey??string.Empty)+".jpg");
                if(!File.Exists(path)) r.Errors.Add(b.Id+" references missing Background artwork '"+path+"'.");

                // Background list icons are a separate authored surface from the 16:9 hero artwork.
                // The current roster requires normalized 164x92 RGBA PNGs so every list icon is
                // centered consistently and can render on the UI without a baked background.
                string thumbnailPath=Path.Combine(backgroundThumbnailRoot,(b.BackgroundArtKey??string.Empty)+".png");
                if(!File.Exists(thumbnailPath))
                    r.Errors.Add(b.Id+" references missing normalized PNG Background thumbnail '"+thumbnailPath+"'.");
                else
                {
                    int thumbnailWidth,thumbnailHeight; byte thumbnailColorType;
                    if(!TryReadPngHeader(thumbnailPath,out thumbnailWidth,out thumbnailHeight,out thumbnailColorType))
                        r.Errors.Add(b.Id+" Background thumbnail is not a readable PNG: '"+thumbnailPath+"'.");
                    else
                    {
                        if(thumbnailWidth!=164||thumbnailHeight!=92)
                            r.Errors.Add(b.Id+" Background thumbnail must be exactly 164x92; found "+thumbnailWidth+"x"+thumbnailHeight+" in '"+thumbnailPath+"'.");
                        if(thumbnailColorType!=6)
                            r.Errors.Add(b.Id+" Background thumbnail must be RGBA PNG color type 6 for transparent rendering; found color type "+thumbnailColorType+" in '"+thumbnailPath+"'.");
                    }
                }
            }
            foreach(RebirthTraitDefinition t in bundle.Traits)
            {
                string key=t.IconKey??string.Empty; string path=Path.Combine(iconRoot,key+".png");
                if(!File.Exists(path)) r.Errors.Add(t.Id+" references missing Trait icon '"+path+"'.");
                if(key.Length>0&&!atlasSprites.Contains(key)) r.Errors.Add(t.Id+" Trait icon is not registered in RebirthSurvivorIcons/settings.xml: '"+key+"'.");
            }
            foreach(RebirthDietDefinition d in bundle.Diets)
            {
                string key=d.IconKey??string.Empty; string path=Path.Combine(iconRoot,key+".png");
                if(!File.Exists(path)) r.Errors.Add(d.Id+" references missing Diet icon '"+path+"'.");
                if(key.Length>0&&!atlasSprites.Contains(key)) r.Errors.Add(d.Id+" Diet icon is not registered in RebirthSurvivorIcons/settings.xml: '"+key+"'.");
            }
            foreach(RebirthSkillDefinition skill in bundle.Progression.Skills)
            {
                string key=RebirthSkillAptitudeTraitFactory.SkillIconKey(skill.Id); string path=Path.Combine(iconRoot,key+".png");
                if(!File.Exists(path)) r.Errors.Add(skill.Id+" references missing Skill icon '"+path+"'.");
                if(key.Length>0&&!atlasSprites.Contains(key)) r.Errors.Add(skill.Id+" Skill icon is not registered in RebirthSurvivorIcons/settings.xml: '"+key+"'.");
            }
        }
        catch(Exception ex) { r.Errors.Add("Final Survivor visual-asset validation failed: "+ex.GetType().Name+": "+ex.Message); }
    }
    private static bool TryReadPngHeader(string path,out int width,out int height,out byte colorType)
    {
        width=0; height=0; colorType=0;
        try
        {
            byte[] header=new byte[26];
            using(FileStream stream=File.OpenRead(path))
            {
                int offset=0;
                while(offset<header.Length)
                {
                    int read=stream.Read(header,offset,header.Length-offset);
                    if(read<=0) return false;
                    offset+=read;
                }
            }
            byte[] signature={137,80,78,71,13,10,26,10};
            for(int i=0;i<signature.Length;i++) if(header[i]!=signature[i]) return false;
            if(header[12]!=(byte)'I'||header[13]!=(byte)'H'||header[14]!=(byte)'D'||header[15]!=(byte)'R') return false;
            width=(header[16]<<24)|(header[17]<<16)|(header[18]<<8)|header[19];
            height=(header[20]<<24)|(header[21]<<16)|(header[22]<<8)|header[23];
            colorType=header[25];
            return width>0&&height>0;
        }
        catch { return false; }
    }

    private static bool IsTraitComponentTarget(string value,string phase)
    {
        string v=(value??string.Empty).Trim().ToLowerInvariant();
        string p=(phase??string.Empty).Trim().ToLowerInvariant();
        if(p=="creation")
            return v=="attribute.strength.current"||v=="attribute.strength.potential"||v=="attribute.dexterity.current"||v=="attribute.dexterity.potential"||
                v=="attribute.constitution.current"||v=="attribute.constitution.potential"||v=="attribute.intelligence.current"||v=="attribute.intelligence.potential"||
                v=="attribute.charisma.current"||v=="attribute.charisma.potential"||v=="health.potential"||v=="inventory.unencumbered_slots"||
                v=="knowledge.grant"||v=="skill.start_bias"||v.StartsWith("skill.start.",StringComparison.Ordinal);
        if(p!="runtime") return false;
        if(RebirthTraitGameplayModifierService.SupportsGenericTraitTarget(v)) return true;
        return v=="stress.gain"||v=="stress.anxiety"||v=="stress.darkness"||v=="energy.use"||v=="energy.use.strenuous"||v=="energy.use.sprint_jump"||v=="energy.use.movement"||v=="energy.use.injured"||v=="energy.use.encumbered"||
            v=="energy.recovery"||v=="hydration.demand.total"||v=="nutrition.demand.total"||v=="environment.heat"||v=="environment.cold"||
            v=="digestion.speed"||v=="digestion.fluid_absorption"||v=="digestion.nutrient_utilization"||v=="digestion.gut_resilience"||v=="digestion.recovery"||v=="digestion.baseline"||
            v=="health.capacity.loss"||v=="health.capacity.injury_loss"||v=="health.capacity.illness_loss"||v=="health.capacity.recovery"||v=="health.capacity.illness_recovery"||
            v=="mood.recovery.negative"||v=="mood.injury"||v=="mood.baseline"||v=="mood.companion.nearby"||v=="mood.companion.solo"||v=="stamina.recovery.energy"||
            v=="food.mood.all"||v=="food.mood.positive"||v=="food.mood.negative"||v=="food.mood.good_comfort"||v=="food.mood.plant_positive"||v=="food.mood.meat_positive"||v=="food.mood.sweet"||
            v=="food.repetition.simple"||v=="food.repetition.sweet"||v.StartsWith("skill.gain.",StringComparison.Ordinal)||v=="barter.buying"||v=="barter.selling"||v=="harvest.count"||v=="vehicle.fuel_use";
    }

    private static bool IsSupportKind(string value)
    {
        string v=(value??string.Empty).Trim().ToLowerInvariant();
        return v=="consumable"||v=="metabolism_drink"||v=="existing_metabolism_drink"||v=="existing_medicine"||v=="equipment_mod"||v=="survivor_gear";
    }
    private static bool IsSupportRepeatMode(string value)
    {
        string v=(value??string.Empty).Trim().ToLowerInvariant();
        return v=="refresh_nonstacking"||v=="next_meal"||v=="equipment";
    }
    private static bool IsSupportEffectTarget(string value)
    {
        string v=(value??string.Empty).Trim().ToLowerInvariant();
        return v=="mood.target"||v=="skill.gain"||v=="energy.use"||v=="energy.use.strenuous"||v=="energy.use.sprint_jump"||v=="energy.use.movement"||v=="energy.recovery"||v=="hydration.demand.total"||v=="environment.heat"||v=="environment.cold"||v=="digestion.fluid_absorption"||v=="digestion.fluid_utilization"||v=="digestion.gut_resilience"||v=="digestion.recovery"||v=="health.capacity.illness_loss"||v=="mood.injury"||v=="food.mood.next"||v=="food.repetition.next";
    }
    private static Dictionary<string,T> Unique<T>(IEnumerable<T> source,Func<T,string> id,string kind,RebirthSurvivorAuthoringReport r)
    {
        Dictionary<string,T> result=new Dictionary<string,T>(StringComparer.OrdinalIgnoreCase); if(source==null)return result;
        foreach(T value in source) { string key=id(value)??string.Empty; if(string.IsNullOrEmpty(key)) { r.Errors.Add(kind+" has an empty ID."); continue; } if(result.ContainsKey(key)) r.Errors.Add(kind+" ID is duplicated: '"+key+"'."); else result.Add(key,value); } return result;
    }
    private static void Required(string owner,string value,string field,RebirthSurvivorAuthoringReport r) { if(string.IsNullOrWhiteSpace(value))r.Errors.Add(owner+" is missing "+field+"."); }
    private static void ValidateRefs<T>(string owner,IEnumerable<string> ids,IDictionary<string,T> known,string kind,RebirthSurvivorAuthoringReport r) { foreach(string id in ids) if(!known.ContainsKey(id)) r.Errors.Add(owner+" references unknown "+kind+" '"+id+"'."); }
    private static bool Contains(IEnumerable<string> values,string wanted) { foreach(string value in values) if(string.Equals(value,wanted,StringComparison.OrdinalIgnoreCase))return true; return false; }
    private static void CheckLoc(string owner,string key,HashSet<string> known,RebirthSurvivorAuthoringReport r) { if(!known.Contains(key)) r.Errors.Add(owner+" references missing Localization key '"+key+"'."); }
    private static HashSet<string> ReadLocalizationKeys(string configRoot,RebirthSurvivorAuthoringReport r)
    {
        try
        {
            string configDir=Path.GetDirectoryName(configRoot); string path=Path.Combine(configDir??string.Empty,"Localization.csv");
            if(!File.Exists(path)) { r.Errors.Add("Localization.csv not found while validating Survivor definitions: '"+path+"'."); return null; }
            HashSet<string> result=new HashSet<string>(StringComparer.OrdinalIgnoreCase); string[] lines=File.ReadAllLines(path);
            for(int i=1;i<lines.Length;i++)
            {
                string line=lines[i]; if(string.IsNullOrWhiteSpace(line))continue;
                List<string> fields=ParseCsvRow(line);
                if(fields.Count==0) continue;
                string key=(fields[0]??string.Empty).Trim(); if(key.Length==0) continue;
                if(!result.Add(key)) r.Errors.Add("Localization key is duplicated at row "+(i+1)+": '"+key+"'.");
                if(IsSurvivorLocalizationKey(key)&&fields.Count!=2)
                    r.Errors.Add("Survivor Localization row "+(i+1)+" must preserve the two-column Key,english contract; key='"+key+"' fields="+fields.Count+".");
            }
            return result;
        }
        catch(Exception ex) { r.Errors.Add("Localization validation failed: "+ex.GetType().Name+": "+ex.Message); return null; }
    }
    private static bool IsSurvivorLocalizationKey(string key)
    {
        string k=key??string.Empty;
        return k.StartsWith("xuiRebirthSurvivor",StringComparison.OrdinalIgnoreCase)||
            k.StartsWith("xuiRebirthBackground",StringComparison.OrdinalIgnoreCase)||
            k.StartsWith("xuiRebirthTrait",StringComparison.OrdinalIgnoreCase)||
            k.StartsWith("xuiRebirthDiet",StringComparison.OrdinalIgnoreCase)||
            k.StartsWith("xuiRebirthCreationError",StringComparison.OrdinalIgnoreCase)||
            k.StartsWith("xuiRebirthPlayerProgression",StringComparison.OrdinalIgnoreCase)||
            k.StartsWith("xuiRebirthSkill",StringComparison.OrdinalIgnoreCase)||
            k.StartsWith("xuiRebirthKnowledge",StringComparison.OrdinalIgnoreCase);
    }
    private static List<string> ParseCsvRow(string line)
    {
        List<string> fields=new List<string>(); StringBuilder current=new StringBuilder(); bool quoted=false;
        for(int i=0;i<(line??string.Empty).Length;i++)
        {
            char ch=line[i];
            if(ch=='\"')
            {
                if(quoted&&i+1<line.Length&&line[i+1]=='\"') { current.Append('\"'); i++; }
                else quoted=!quoted;
            }
            else if(ch==','&&!quoted) { fields.Add(current.ToString()); current.Length=0; }
            else current.Append(ch);
        }
        fields.Add(current.ToString());
        return fields;
    }

}
