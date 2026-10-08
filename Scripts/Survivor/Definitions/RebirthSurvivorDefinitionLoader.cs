using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;

#nullable disable

public static class RebirthSurvivorDefinitionLoader
{
    public const string ConfigFolderName = "_Survivor";
    private static string preferredModRoot = string.Empty;

    /// <summary>
    /// InitMod already receives the authoritative filesystem root for this mod. Remember it so
    /// Survivor loading does not depend on ModManager.GetLoadedMods() being complete while the
    /// current mod is still inside its own InitMod call.
    /// </summary>
    public static void SetPreferredModRoot(string modRoot)
    {
        if (string.IsNullOrWhiteSpace(modRoot)) return;
        try { preferredModRoot = Path.GetFullPath(modRoot.Trim()); }
        catch { preferredModRoot = modRoot.Trim(); }
    }

    public static string ResolveConfigRoot()
    {
        string candidate;

        if (TryConfigRoot(preferredModRoot, out candidate))
            return candidate;

        // RebirthUtils.dll is deployed in the mod root. This is independent of ModManager
        // enumeration order and is therefore the most reliable late-load recovery path.
        try
        {
            string assemblyPath = typeof(RebirthSurvivorDefinitionLoader).Assembly.Location;
            string assemblyRoot = !string.IsNullOrEmpty(assemblyPath) ? Path.GetDirectoryName(assemblyPath) : string.Empty;
            if (TryConfigRoot(assemblyRoot, out candidate))
            {
                SetPreferredModRoot(assemblyRoot);
                return candidate;
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor] Assembly-root config probe failed: " + ex.GetType().Name + ": " + ex.Message);
        }

        try
        {
            // Prefer the named REBIRTH mod when the loaded-mod collection is ready.
            foreach (Mod mod in ModManager.GetLoadedMods())
            {
                if (mod == null || string.IsNullOrEmpty(mod.Path)) continue;
                if (!string.Equals(mod.Name, "zzz_REBIRTH__3_0", StringComparison.OrdinalIgnoreCase)) continue;
                if (TryConfigRoot(mod.Path, out candidate))
                {
                    SetPreferredModRoot(mod.Path);
                    return candidate;
                }
            }

            // Recovery path for builds where Mod.Name differs from the folder/DisplayName. The
            // Survivor definition set is distinctive enough that checking for all three core files
            // is safer than returning an empty registry.
            foreach (Mod mod in ModManager.GetLoadedMods())
            {
                if (mod == null || string.IsNullOrEmpty(mod.Path)) continue;
                if (TryConfigRoot(mod.Path, out candidate))
                {
                    SetPreferredModRoot(mod.Path);
                    return candidate;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Survivor] Loaded-mod config scan failed: " + ex.GetType().Name + ": " + ex.Message);
        }

        // Last-resort filesystem probes. Directory.GetCurrentDirectory() is normally the game
        // root; AppDomain.BaseDirectory varies between launch modes, so probe both.
        string[] roots =
        {
            Directory.GetCurrentDirectory(),
            AppDomain.CurrentDomain.BaseDirectory
        };
        for (int i = 0; i < roots.Length; i++)
        {
            if (string.IsNullOrEmpty(roots[i])) continue;
            string modRoot = Path.Combine(roots[i], "Mods", "zzz_REBIRTH__3_0");
            if (TryConfigRoot(modRoot, out candidate))
            {
                SetPreferredModRoot(modRoot);
                return candidate;
            }
        }

        // Keep a deterministic path in the exception/report even if nothing exists.
        return Path.Combine(Directory.GetCurrentDirectory(), "Mods", "zzz_REBIRTH__3_0", "Config", ConfigFolderName);
    }

    private static bool TryConfigRoot(string modRoot, out string configRoot)
    {
        configRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(modRoot)) return false;
        try
        {
            string candidate = Path.Combine(modRoot, "Config", ConfigFolderName);
            if (!File.Exists(Path.Combine(candidate, "backgrounds.xml"))) return false;
            if (!File.Exists(Path.Combine(candidate, "traits.xml"))) return false;
            if (!File.Exists(Path.Combine(candidate, "diets.xml"))) return false;
            configRoot = Path.GetFullPath(candidate);
            return true;
        }
        catch { return false; }
    }

    public static RebirthSurvivorDefinitionBundle Load()
    {
        return Load(ResolveConfigRoot());
    }

    public static RebirthSurvivorDefinitionBundle Load(string configRoot)
    {
        if (string.IsNullOrEmpty(configRoot)) throw new InvalidDataException("Survivor config root is empty.");
        RebirthProgressionDefinition progression = LoadProgression(Path.Combine(configRoot, "progression.xml"));
        List<RebirthBackgroundDefinition> backgrounds = LoadBackgrounds(Path.Combine(configRoot, "backgrounds.xml"));
        List<RebirthTraitDefinition> traits = LoadTraits(Path.Combine(configRoot, "traits.xml"));
        RebirthSkillAptitudeTraitFactory.AppendGenerated(traits, progression.Skills);
        List<RebirthDietDefinition> diets = LoadDiets(Path.Combine(configRoot, "diets.xml"));
        List<RebirthConditionCapGroupDefinition> caps;
        List<RebirthConditionModifierProfileDefinition> modifiers;
        LoadConditionProfiles(Path.Combine(configRoot, "condition_profiles.xml"), out caps, out modifiers);
        List<RebirthTraitSupportProfileDefinition> support = LoadSupportProfiles(Path.Combine(configRoot, "support_profiles.xml"));
        ValidateProgressionContracts(configRoot, progression);
        return new RebirthSurvivorDefinitionBundle(configRoot, progression, backgrounds, traits, diets, caps, modifiers, support);
    }

    private static RebirthProgressionDefinition LoadProgression(string path)
    {
        XmlElement root = LoadRoot(path, "survivor_progression");
        XmlElement creation = Child(root, "creation", true);
        int basePoints = IntAttr(creation, "base_points", 0);
        int cleanSlateBonus = IntAttr(creation, "clean_slate_bonus", 0);
        int maxNegativeTraitRefund = IntAttr(creation, "max_negative_trait_refund", -1);
        float creationSkillMin = FloatAttr(creation, "skill_start_min", -35f);
        float creationSkillMax = FloatAttr(creation, "skill_start_max", 50f);
        if (creationSkillMin > creationSkillMax) throw new InvalidDataException("creation skill_start_min cannot exceed skill_start_max.");

        XmlElement skillKnowledgeNode = Child(root, "skill_knowledge", true);
        float skillKnowledgeMin = FloatAttr(skillKnowledgeNode, "min", 0f);
        float skillKnowledgeMax = FloatAttr(skillKnowledgeNode, "max", 100f);
        if (skillKnowledgeMin < 0f || skillKnowledgeMin > skillKnowledgeMax)
            throw new InvalidDataException("skill_knowledge min/max are invalid.");

        List<RebirthAttributeDefinition> attributes = new List<RebirthAttributeDefinition>();
        XmlElement attrs = Child(root, "attributes", true);
        foreach (XmlNode node in attrs.ChildNodes)
        {
            XmlElement e = node as XmlElement; if (e == null || e.Name != "attribute") continue;
            attributes.Add(new RebirthAttributeDefinition(Req(e,"id"),Req(e,"name_key"),FloatAttr(e,"base_current",0f),FloatAttr(e,"base_potential",0f),FloatAttr(e,"min",0f),FloatAttr(e,"max",100f)));
        }

        XmlElement health = Child(root,"health",true);
        float baseHealth = FloatAttr(health,"base_potential",100f), minHealth=FloatAttr(health,"min_potential",1f), maxHealth=FloatAttr(health,"max_potential",200f);

        Dictionary<string,int> tiers = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        XmlElement tiersNode = Child(root,"skill_bias_tiers",true);
        foreach(XmlNode node in tiersNode.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="tier")continue;
            string id=Req(e,"id"); if(tiers.ContainsKey(id)) throw new InvalidDataException("Duplicate skill-bias tier '"+id+"'."); tiers.Add(id,IntAttr(e,"value",0));
        }

        List<RebirthSkillDefinition> skills = new List<RebirthSkillDefinition>();
        XmlElement skillsNode=Child(root,"skills",true);
        foreach(XmlNode node in skillsNode.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="skill")continue;
            skills.Add(new RebirthSkillDefinition(Req(e,"id"),Req(e,"name_key"),Req(e,"source_key"),Req(e,"lbd_source"),e.GetAttribute("feasibility"),FloatAttr(e,"min",0f),FloatAttr(e,"max",100f),Req(e,"primary_attribute"),string.Equals(e.GetAttribute("advanced"),"true",StringComparison.OrdinalIgnoreCase)));
        }

        List<RebirthKnowledgeDefinition> knowledge = new List<RebirthKnowledgeDefinition>();
        XmlElement kNode=Child(root,"knowledge",true);
        foreach(XmlNode node in kNode.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="knowledge")continue;
            List<string> associatedSkills=new List<string>();
            string associated=e.GetAttribute("associated_skills");
            if(!string.IsNullOrEmpty(associated)) foreach(string token in associated.Split(',')){string value=token.Trim();if(value.Length>0)associatedSkills.Add(value);}
            knowledge.Add(new RebirthKnowledgeDefinition(Req(e,"id"),Req(e,"name_key"),associatedSkills,e.GetAttribute("explorer_status")));
        }

        return new RebirthProgressionDefinition(basePoints,cleanSlateBonus,maxNegativeTraitRefund,creationSkillMin,creationSkillMax,skillKnowledgeMin,skillKnowledgeMax,baseHealth,minHealth,maxHealth,attributes,tiers,skills,knowledge);
    }

    private static List<RebirthBackgroundDefinition> LoadBackgrounds(string path)
    {
        XmlElement root=LoadRoot(path,"survivor_backgrounds"); List<RebirthBackgroundDefinition> result=new List<RebirthBackgroundDefinition>();
        foreach(XmlNode node in root.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="background")continue;
            List<string> restricted=ReadIdChildren(e,"restricted_traits","trait");
            List<string> favored=ReadIdChildren(e,"favored_traits","trait");
            List<string> blocked=ReadIdChildren(e,"blocked_traits","trait");
            List<RebirthStartingAttributeDefinition> attributeStarts=new List<RebirthStartingAttributeDefinition>();
            HashSet<string> attributeIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            XmlNodeList attributeSections=e.SelectNodes("starting_attributes");
            if(attributeSections.Count>1) throw new InvalidDataException("Background '"+e.GetAttribute("id")+"' repeats starting_attributes.");
            foreach(XmlNode section in attributeSections) foreach(XmlNode child in section.ChildNodes)
            {
                XmlElement ae=child as XmlElement; if(ae==null)continue;
                if(ae.Name!="attribute") throw new InvalidDataException("Unknown starting_attributes element '"+ae.Name+"'.");
                string id=Req(ae,"id"); float current;
                if(!attributeIds.Add(id)) throw new InvalidDataException("Duplicate starting Attribute '"+id+"'.");
                if(!float.TryParse(Req(ae,"current"),NumberStyles.Float,CultureInfo.InvariantCulture,out current)||float.IsNaN(current)||float.IsInfinity(current))
                    throw new InvalidDataException("Invalid starting Attribute current for '"+id+"'.");
                attributeStarts.Add(new RebirthStartingAttributeDefinition(id,current));
            }
            List<RebirthStartingSkillBiasDefinition> skills=new List<RebirthStartingSkillBiasDefinition>();
            XmlElement sNode=Child(e,"starting_skills",false);
            if(sNode!=null) foreach(XmlNode sn in sNode.ChildNodes)
            {
                XmlElement se=sn as XmlElement; if(se==null||se.Name!="skill")continue;
                string tier=(se.GetAttribute("tier")??string.Empty).Trim();
                string rawValue=(se.GetAttribute("value")??string.Empty).Trim();
                if(tier.Length>0 && rawValue.Length>0) throw new InvalidDataException("Background starting Skill '"+se.GetAttribute("id")+"' cannot declare both tier and value.");
                if(tier.Length==0 && rawValue.Length==0) throw new InvalidDataException("Background starting Skill '"+se.GetAttribute("id")+"' requires tier or value.");
                float directValue=0f; bool hasDirect=rawValue.Length>0;
                if(hasDirect && !float.TryParse(rawValue,NumberStyles.Float,CultureInfo.InvariantCulture,out directValue)) throw new InvalidDataException("Background starting Skill '"+se.GetAttribute("id")+"' has invalid value '"+rawValue+"'.");
                skills.Add(new RebirthStartingSkillBiasDefinition(Req(se,"id"),tier,directValue,hasDirect));
            }
            List<RebirthStartingSkillKnowledgeDefinition> skillKnowledge=new List<RebirthStartingSkillKnowledgeDefinition>();
            XmlElement skNode=Child(e,"starting_skill_knowledge",false);
            if(skNode!=null) foreach(XmlNode kn in skNode.ChildNodes)
            {
                XmlElement ke=kn as XmlElement; if(ke==null||ke.Name!="skill")continue;
                skillKnowledge.Add(new RebirthStartingSkillKnowledgeDefinition(Req(ke,"id"),FloatAttr(ke,"value",0f)));
            }
            List<string> knowledge=ReadIdChildren(e,"starting_knowledge","knowledge");
            List<RebirthStartingItemDefinition> startingItems=new List<RebirthStartingItemDefinition>();
            XmlElement iNode=Child(e,"starting_items",false);
            if(iNode!=null) foreach(XmlNode itemNode in iNode.ChildNodes)
            {
                XmlElement ie=itemNode as XmlElement; if(ie==null||ie.Name!="item")continue;
                string rawQuality=(ie.GetAttribute("quality")??string.Empty).Trim();
                int quality=1; bool hasQuality=rawQuality.Length>0;
                if(hasQuality && !int.TryParse(rawQuality,NumberStyles.Integer,CultureInfo.InvariantCulture,out quality)) throw new InvalidDataException("Background starting item '"+ie.GetAttribute("id")+"' has invalid quality '"+rawQuality+"'.");
                startingItems.Add(new RebirthStartingItemDefinition(Req(ie,"id"),Req(ie,"name_key"),Math.Max(1,IntAttr(ie,"count",1)),quality,hasQuality,string.Equals(ie.GetAttribute("kind"),"block",StringComparison.OrdinalIgnoreCase)));
            }
            XmlElement a=Child(e,"authoring",false);
            result.Add(new RebirthBackgroundDefinition(Req(e,"id"),Req(e,"name_key"),Req(e,"description_key"),Req(e,"background_art_key"),Req(e,"background_art_alt_text_key"),IntAttr(e,"creation_point_modifier",0),restricted,favored,blocked,skills,skillKnowledge,knowledge,startingItems,a!=null?a.GetAttribute("identity"):string.Empty,a!=null?a.GetAttribute("starting_experience"):string.Empty,a!=null?a.GetAttribute("design_note"):string.Empty,attributeStarts));
        }
        return result;
    }

    private static List<RebirthTraitDefinition> LoadTraits(string path)
    {
        XmlElement root=LoadRoot(path,"survivor_traits"); List<RebirthTraitDefinition> result=new List<RebirthTraitDefinition>();
        foreach(XmlNode node in root.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="trait")continue;
            RebirthTraitPolarity polarity;
            if(!Enum.TryParse(e.GetAttribute("polarity"),true,out polarity)) throw new InvalidDataException("Trait '"+e.GetAttribute("id")+"' has invalid polarity.");
            RebirthDefinitionAvailability availability;
            if(!Enum.TryParse(e.GetAttribute("availability"),true,out availability)) throw new InvalidDataException("Trait '"+e.GetAttribute("id")+"' has invalid availability.");
            List<string> allowed=ReadIdChildren(e,"allowed_backgrounds","background");
            List<string> conflicts=ReadIdChildren(e,"conflicts","trait");
            List<string> dietConflicts=ReadIdChildren(e,"diet_conflicts","diet");
            XmlElement a=Child(e,"authoring",false);
            result.Add(new RebirthTraitDefinition(Req(e,"id"),Req(e,"name_key"),Req(e,"description_key"),Req(e,"category"),polarity,IntAttr(e,"points",0),availability,Req(e,"icon_key"),Req(e,"modifier_id"),allowed,conflicts,dietConflicts,a!=null?a.GetAttribute("effect_summary"):string.Empty,a!=null?a.GetAttribute("implementation_surface"):string.Empty));
        }
        return result;
    }

    private static List<RebirthDietDefinition> LoadDiets(string path)
    {
        XmlElement root=LoadRoot(path,"survivor_diets"); List<RebirthDietDefinition> result=new List<RebirthDietDefinition>();
        foreach(XmlNode node in root.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="diet")continue;
            result.Add(new RebirthDietDefinition(Req(e,"id"),Req(e,"name_key"),Req(e,"description_key"),IntAttr(e,"points",0),Req(e,"icon_key"),Req(e,"composition_rule"),Req(e,"rule_summary")));
        }
        return result;
    }

    private static void LoadConditionProfiles(string path,out List<RebirthConditionCapGroupDefinition> caps,out List<RebirthConditionModifierProfileDefinition> modifiers)
    {
        XmlElement root=LoadRoot(path,"survivor_condition_profiles"); caps=new List<RebirthConditionCapGroupDefinition>(); modifiers=new List<RebirthConditionModifierProfileDefinition>();
        XmlElement capNode=Child(root,"cap_groups",true);
        foreach(XmlNode node in capNode.ChildNodes) { XmlElement e=node as XmlElement; if(e==null||e.Name!="cap_group")continue; caps.Add(new RebirthConditionCapGroupDefinition(Req(e,"id"),e.GetAttribute("description"),e.GetAttribute("tuning_state"))); }
        XmlElement modNode=Child(root,"modifier_profiles",true);
        foreach(XmlNode node in modNode.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="modifier_profile")continue; List<RebirthConditionModifierComponent> cc=new List<RebirthConditionModifierComponent>();
            foreach(XmlNode cn in e.ChildNodes) { XmlElement c=cn as XmlElement; if(c==null||c.Name!="component")continue; cc.Add(new RebirthConditionModifierComponent(Req(c,"target"),Req(c,"op"),c.GetAttribute("value"),c.GetAttribute("unit"),c.GetAttribute("phase"),c.GetAttribute("note"))); }
            modifiers.Add(new RebirthConditionModifierProfileDefinition(Req(e,"id"),Req(e,"owner_trait_id"),Req(e,"effect_summary"),Req(e,"implementation_surface"),e.GetAttribute("implementation_state"),e.GetAttribute("cap_group"),cc));
        }
    }

    private static List<RebirthTraitSupportProfileDefinition> LoadSupportProfiles(string path)
    {
        XmlElement root=LoadRoot(path,"survivor_support_profiles"); List<RebirthTraitSupportProfileDefinition> result=new List<RebirthTraitSupportProfileDefinition>();
        foreach(XmlNode node in root.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="support_profile")continue;
            List<string> items=ReadIdChildren(e,"item_bindings","item");
            List<RebirthTraitSupportEffectDefinition> effects=new List<RebirthTraitSupportEffectDefinition>();
            XmlElement effectsNode=Child(e,"effects",false);
            if(effectsNode!=null) foreach(XmlNode effectNode in effectsNode.ChildNodes)
            {
                XmlElement x=effectNode as XmlElement; if(x==null||x.Name!="effect")continue;
                effects.Add(new RebirthTraitSupportEffectDefinition(Req(x,"target"),Req(x,"op"),FloatAttr(x,"value",0f),x.GetAttribute("scope"),x.GetAttribute("state"),x.GetAttribute("note")));
            }
            result.Add(new RebirthTraitSupportProfileDefinition(Req(e,"id"),Req(e,"name_key"),Req(e,"kind"),Req(e,"effect_summary"),e.GetAttribute("timing_state"),e.GetAttribute("repeat_mode"),
                e.GetAttribute("habit_trait_id"),e.GetAttribute("global_cap_group"),e.GetAttribute("equipment_cvar"),e.GetAttribute("gear_slot_id"),e.GetAttribute("gear_slot_name_key"),e.GetAttribute("gear_item_id"),
                IntAttr(e,"bag_slot_bonus",0),FloatAttr(e,"min_strength",0f),FloatAttr(e,"min_constitution",0f),
                FloatAttr(e,"grace_seconds",0f),FloatAttr(e,"managed_seconds",0f),FloatAttr(e,"positive_seconds",0f),FloatAttr(e,"cooldown_seconds",0f),
                ReadIdChildren(e,"supported_traits","trait"),items,effects,IntAttr(e,"toolbelt_slot_bonus",0)));
        }
        return result;
    }

    private static XmlElement LoadRoot(string path,string expected)
    {
        // These are immutable authoring files shipped inside the mod, not player/save data.
        // Standard read-only System.IO is the correct path here and avoids routing absolute
        // mod paths through the SdFile save-data abstraction.
        if(!File.Exists(path)) throw new FileNotFoundException("Required Survivor definition file not found.",path);
        XmlDocument doc=new XmlDocument();
        using(FileStream stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            doc.Load(stream);
        XmlElement root=doc.DocumentElement;
        if(root==null||!string.Equals(root.Name,expected,StringComparison.Ordinal)) throw new InvalidDataException("Expected <"+expected+"> root in '"+path+"'.");
        int schema=IntAttr(root,"schema_version",0); if(schema!=RebirthSurvivorDefinitionVersion.SchemaVersion) throw new InvalidDataException("Unsupported Survivor schema_version="+schema+" in '"+path+"'.");
        return root;
    }

    private static XmlElement Child(XmlElement parent,string name,bool required)
    {
        foreach(XmlNode node in parent.ChildNodes) { XmlElement e=node as XmlElement; if(e!=null&&e.Name==name)return e; }
        if(required) throw new InvalidDataException("Missing <"+name+"> under <"+parent.Name+">."); return null;
    }

    private static List<string> ReadIdChildren(XmlElement parent,string container,string childName)
    {
        List<string> result=new List<string>(); XmlElement c=Child(parent,container,false); if(c==null)return result;
        foreach(XmlNode node in c.ChildNodes) { XmlElement e=node as XmlElement; if(e==null||e.Name!=childName)continue; result.Add(Req(e,"id")); } return result;
    }

    private static void ValidateProgressionContracts(string configRoot, RebirthProgressionDefinition progression)
    {
        XmlElement attributeTraining=ValidateContractRoot(Path.Combine(configRoot, "attribute_training.xml"), "survivor_attribute_training", "1");
        XmlElement skillTraining=ValidateContractRoot(Path.Combine(configRoot, "skill_training.xml"), "survivor_skill_training", "1");
        XmlElement theoryInsights=ValidateContractRoot(Path.Combine(configRoot, "theory_insights.xml"), "survivor_theory_insights", "1");
        XmlElement theoryInstruction=ValidateContractRoot(Path.Combine(configRoot, "theory_instruction.xml"), "survivor_theory_instruction", "1");

        XmlElement bridge=Child(attributeTraining,"skill_bridge",true);
        RequireFiniteFloatAttr(bridge,"raw_conversion",0f,1f);
        RequireFiniteFloatAttr(bridge,"potential_full_until_ratio",0f,1f);
        RequireFiniteFloatAttr(bridge,"at_potential_multiplier",0f,1f);
        RequireFiniteFloatAttr(bridge,"above_potential_multiplier",0f,1f);
        XmlElement directConstitution=Child(attributeTraining,"direct_constitution",true);
        RequireFiniteFloatAttr(directConstitution,"healing_conversion",0f,1f);
        RequireFiniteFloatAttr(directConstitution,"rolling_active_seconds",1f,float.MaxValue);
        RequireFiniteFloatAttr(directConstitution,"rolling_cap",0f,1f);
        RequireFiniteFloatAttr(directConstitution,"exposure_cooldown_active_seconds",0f,float.MaxValue);
        XmlElement healingBands=Child(directConstitution,"healing_bands",true);
        int healingBandCount=0; float previousUpper=0f;
        foreach(XmlNode node in healingBands.ChildNodes)
        {
            XmlElement band=node as XmlElement;if(band==null||band.Name!="band")continue;
            float upper=RequireFiniteFloatAttr(band,"up_to_fraction",0f,1f); RequireFiniteFloatAttr(band,"multiplier",0f,1f);
            if(upper<=previousUpper)throw new InvalidDataException("attribute_training.xml healing bands must be strictly increasing.");
            previousUpper=upper; healingBandCount++;
        }
        if(healingBandCount!=3||Math.Abs(previousUpper-1f)>.0001f)throw new InvalidDataException("attribute_training.xml must declare the locked three healing bands ending at 1.0.");
        XmlElement exposureTiers=Child(directConstitution,"exposure_tiers",true);
        string[] tierNames={"mild","standard","strong","extreme"};
        for(int i=0;i<tierNames.Length;i++)RequireFiniteFloatAttr(exposureTiers,tierNames[i],0.000001f,1f);
        XmlElement exposures=Child(directConstitution,"exposures",true);
        HashSet<string> exposureItems=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(XmlNode node in exposures.ChildNodes)
        {
            XmlElement item=node as XmlElement;if(item==null||item.Name!="item")continue;
            string id=Req(item,"id"),family=Req(item,"family"),tier=Req(item,"tier"),buff=Req(item,"effect_buff");
            if(!exposureItems.Add(id))throw new InvalidDataException("Duplicate Constitution exposure item '"+id+"'.");
            bool tierKnown=false;for(int i=0;i<tierNames.Length;i++)if(string.Equals(tier,tierNames[i],StringComparison.OrdinalIgnoreCase))tierKnown=true;
            if(!tierKnown)throw new InvalidDataException("Unknown Constitution exposure tier '"+tier+"' for '"+id+"'.");
            if(string.IsNullOrWhiteSpace(family)||string.IsNullOrWhiteSpace(buff))throw new InvalidDataException("Constitution exposure '"+id+"' has blank family/effect_buff.");
        }

        if(!string.Equals(skillTraining.GetAttribute("live_awards"),"false",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("skill_training.xml live_awards must remain false until the common training runtime is installed.");
        XmlElement common=Child(skillTraining,"common",true);
        RequireFiniteFloatAttr(common,"continuous_rate_per_second",0f,1f);
        RequireFiniteFloatAttr(common,"max_continuous_equivalent_seconds",0f,float.MaxValue);
        RequireFiniteFloatAttr(common,"max_discrete_award",0f,1f);
        int bandCount=0; foreach(XmlNode node in common.ChildNodes){XmlElement band=node as XmlElement;if(band!=null&&band.Name=="skill_band")bandCount++;}
        if(bandCount!=5)throw new InvalidDataException("skill_training.xml must declare exactly five locked Skill bands.");

        Dictionary<string,bool> profiles=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        XmlElement profilesNode=Child(skillTraining,"profiles",true);
        foreach(XmlNode node in profilesNode.ChildNodes)
        {
            XmlElement e=node as XmlElement; if(e==null||e.Name!="profile")continue;
            string id=Req(e,"skill_id"); if(profiles.ContainsKey(id))throw new InvalidDataException("Duplicate skill_training profile for '"+id+"'.");
            profiles.Add(id,true);
            Req(e,"family"); Req(e,"reference_unit"); Req(e,"formula"); Req(e,"coefficients");
            if(!string.Equals(Req(e,"state"),"LOCKED",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("skill_training profile '"+id+"' is not LOCKED.");
        }
        if(progression==null)throw new InvalidDataException("Progression definition is unavailable while validating skill_training.xml.");
        for(int i=0;i<progression.Skills.Count;i++)
        {
            RebirthSkillDefinition skill=progression.Skills[i]; if(skill==null)continue;
            if(!profiles.ContainsKey(skill.Id))throw new InvalidDataException("skill_training.xml is missing required profile for '"+skill.Id+"'.");
        }
        if(profiles.Count!=progression.Skills.Count)throw new InvalidDataException("skill_training.xml profile count does not match the authoritative Skill registry.");
        Child(theoryInsights,"insights",true);
        Child(theoryInstruction,"subjects",true);
    }

    private static XmlElement ValidateContractRoot(string path,string expectedRoot,string expectedSchema)
    {
        if(!File.Exists(path)) throw new FileNotFoundException("Missing required Survivor progression contract.",path);
        XmlDocument doc=new XmlDocument();
        using(FileStream stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            doc.Load(stream);
        XmlElement root=doc.DocumentElement;
        if(root==null||!string.Equals(root.Name,expectedRoot,StringComparison.Ordinal)) throw new InvalidDataException("Expected <"+expectedRoot+"> root in '"+path+"'.");
        string schema=Req(root,"schema_version");
        if(!string.Equals(schema,expectedSchema,StringComparison.Ordinal))throw new InvalidDataException(Path.GetFileName(path)+" schema_version must be "+expectedSchema+"; found '"+schema+"'.");
        return root;
    }

    private static float RequireFiniteFloatAttr(XmlElement e,string name,float min,float max)
    {
        string raw=Req(e,name); float value;
        if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value)||value<min||value>max)
            throw new InvalidDataException("Attribute '"+name+"' on <"+e.Name+"> must be a finite value in ["+min.ToString("R",CultureInfo.InvariantCulture)+","+max.ToString("R",CultureInfo.InvariantCulture)+"]; found '"+raw+"'.");
        return value;
    }

    private static string Req(XmlElement e,string name) { string value=e.GetAttribute(name); if(string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Missing required attribute '"+name+"' on <"+e.Name+">."); return value.Trim(); }
    private static int IntAttr(XmlElement e,string name,int fallback) { int value; return int.TryParse(e.GetAttribute(name),NumberStyles.Integer,CultureInfo.InvariantCulture,out value)?value:fallback; }
    private static float FloatAttr(XmlElement e,string name,float fallback) { float value; return float.TryParse(e.GetAttribute(name),NumberStyles.Float,CultureInfo.InvariantCulture,out value)?value:fallback; }
}
