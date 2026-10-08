using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Explicit sequential world-character schema migration registry.
/// Existing origin semantics are never replaced by guessed IDs.
///
/// Post-Revision-2 Chunk 2 hardening guarantees that the caller's document is not mutated unless
/// the entire migration chain succeeds. Failed/unsupported records therefore remain byte-model
/// recoverable from the original/backup file instead of being left partially translated in memory.
/// </summary>
public static class RebirthWorldCharacterMigrationRegistry
{
    private static readonly Dictionary<int, Func<XDocument, string>> Steps =
        new Dictionary<int, Func<XDocument, string>>();

    static RebirthWorldCharacterMigrationRegistry()
    {
        Register(1, Migrate1To2);
        Register(2, Migrate2To3);
        Register(3, Migrate3To4);
        Register(4, Migrate4To5);
        Register(5, Migrate5To6);
        Register(6, Migrate6To7);
        Register(7, Migrate7To8);
        Register(8, Migrate8To9);
        Register(9, Migrate9To10);
        Register(10, Migrate10To11);
        Register(11, Migrate11To12);
        Register(12, Migrate12To13);
        Register(13, Migrate13To14);
        Register(14, Migrate14To15);
        Register(15, Migrate15To16);
        Register(16, Migrate16To17);
        Register(17, Migrate17To18);
        Register(18, Migrate18To19);
    }

    public static void Register(int fromVersion, Func<XDocument, string> step)
    {
        if (fromVersion < 1 || step == null)
            throw new ArgumentException("World-character migration registration is invalid.");
        if (Steps.ContainsKey(fromVersion))
            throw new InvalidOperationException("World-character migration already registered from schema " + fromVersion.ToString(CultureInfo.InvariantCulture));
        Steps[fromVersion] = step;
    }

    private static string Migrate1To2(XDocument document)
    {
        XElement root = document != null ? document.Root : null;
        if (root == null || root.Name != "rebirthWorldCharacter")
            return "unexpected/missing world-character root";

        XElement origin = root.Element("origin");
        if (origin == null)
            return "origin section is missing";

        string existing = ((string)origin.Attribute("creationId") ?? string.Empty).Trim();
        if (existing.Length == 0)
        {
            string canonical =
                ((string)root.Attribute("stablePlayerKey") ?? string.Empty) + "|" +
                ((string)origin.Attribute("definitionHash") ?? string.Empty) + "|" +
                ((string)origin.Attribute("definitionVersion") ?? string.Empty) + "|" +
                ((string)origin.Attribute("backgroundId") ?? string.Empty) + "|" +
                ((string)origin.Attribute("dietId") ?? string.Empty) + "|" +
                ((string)origin.Attribute("committedAtUtc") ?? string.Empty);

            origin.SetAttributeValue("creationId", "legacy-" + RebirthStablePlayerIdentity.ComputeStorageKey(canonical));
        }

        root.SetAttributeValue("schemaVersion", 2);
        return string.Empty;
    }

    private static string Migrate2To3(XDocument document)
    {
        XElement root = document != null ? document.Root : null;
        if (root == null || root.Name != "rebirthWorldCharacter")
            return "unexpected/missing world-character root";
        XElement condition = root.Element("condition");
        if (condition == null)
            return "condition section is missing";
        if (condition.Attribute("severeDehydrationActiveSeconds") == null)
            condition.SetAttributeValue("severeDehydrationActiveSeconds", "0");
        if (condition.Attribute("severeMalnutritionActiveSeconds") == null)
            condition.SetAttributeValue("severeMalnutritionActiveSeconds", "0");
        root.SetAttributeValue("schemaVersion", 3);
        return string.Empty;
    }

    private static string Migrate3To4(XDocument document)
    {
        XElement root = document != null ? document.Root : null;
        if (root == null || root.Name != "rebirthWorldCharacter")
            return "unexpected/missing world-character root";
        XElement support = root.Element("support");
        if (support == null)
        {
            support = new XElement("support");
            root.Add(support);
        }
        if (support.Element("gear") == null)
            support.AddFirst(new XElement("gear"));
        root.SetAttributeValue("schemaVersion", 4);
        return string.Empty;
    }

    private static string Migrate4To5(XDocument document)
    {
        XElement root = document != null ? document.Root : null;
        if (root == null || root.Name != "rebirthWorldCharacter")
            return "unexpected/missing world-character root";
        XElement origin = root.Element("origin");
        XElement progression = root.Element("progression");
        if (origin == null || progression == null)
            return "origin/progression section is missing";

        XElement originSkills = origin.Element("skills");
        XElement progressionSkills = progression.Element("skills");
        if (originSkills == null || progressionSkills == null)
            return "origin/progression Skills section is missing";

        // Build both translations first. Neither persisted state surface is mutated unless both
        // validate successfully, which prevents a good origin from being rewritten when the
        // mutable progression section is corrupt (or vice versa).
        XElement migratedOriginSkills;
        XElement migratedProgressionSkills;
        RebirthSurvivorSkillMigrationAudit originAudit;
        RebirthSurvivorSkillMigrationAudit progressionAudit;
        string skillError;
        if (!RebirthSurvivorSkillMigrationPolicy.TryBuildMigratedSkills(originSkills, false, out migratedOriginSkills, out originAudit, out skillError))
            return "origin Skills: " + skillError;
        if (!RebirthSurvivorSkillMigrationPolicy.TryBuildMigratedSkills(progressionSkills, true, out migratedProgressionSkills, out progressionAudit, out skillError))
            return "progression Skills: " + skillError;

        if (!RebirthSurvivorDefinitionRegistry.IsReady)
            return "Revision-4 definitions are not installed; cannot migrate Skill identity safely";

        originSkills.ReplaceWith(migratedOriginSkills);
        progressionSkills.ReplaceWith(migratedProgressionSkills);

        // Schema-4 records were authored against the old Skill identity. The translated immutable
        // origin is explicitly rebound to the currently installed validated Revision-4 definition
        // identity; Background/Trait/Diet choices and every non-Skill state remain untouched.
        origin.SetAttributeValue("definitionHash", RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty);
        origin.SetAttributeValue("definitionVersion", RebirthSurvivorDefinitionRegistry.DefinitionVersion ?? string.Empty);

        // Optional schema-5 diagnostics metadata. Old schema-5 files do not need these attributes;
        // repository deserialization defaults them to a native 5->5 record. Migrated files retain
        // the source/target/policy across later saves so `rebirthsurvivor debug migration` can say
        // what actually happened instead of reporting an unknowable placeholder.
        if (root.Attribute("migrationSourceSchema") == null) root.SetAttributeValue("migrationSourceSchema", 4);
        root.SetAttributeValue("migrationTargetSchema", 5);
        root.SetAttributeValue("migrationPolicyId", RebirthSurvivorSkillMigrationPolicy.PolicyId);
        root.SetAttributeValue("migrationApplied", true);
        root.SetAttributeValue("migrationOriginAudit", originAudit.BuildSummary());
        root.SetAttributeValue("migrationProgressionAudit", progressionAudit.BuildSummary());
        root.SetAttributeValue("schemaVersion", 5);
        return string.Empty;
    }

    private static string Migrate5To6(XDocument document)
    {
        XElement root = document != null ? document.Root : null;
        if (root == null || root.Name != "rebirthWorldCharacter")
            return "unexpected/missing world-character root";
        XElement origin = root.Element("origin");
        XElement progression = root.Element("progression");
        if (origin == null || progression == null)
            return "origin/progression section is missing";
        if (!RebirthSurvivorDefinitionRegistry.IsReady || RebirthSurvivorDefinitionRegistry.Bundle == null || RebirthSurvivorDefinitionRegistry.Bundle.Progression == null)
            return "current Survivor definitions are unavailable; cannot seed Skill Knowledge safely";

        string backgroundId = ((string)origin.Attribute("backgroundId") ?? string.Empty).Trim();
        RebirthBackgroundDefinition background;
        if (!RebirthSurvivorDefinitionRegistry.TryGetBackground(backgroundId, out background) || background == null)
            return "unknown Background '"+backgroundId+"' while migrating Skill Knowledge";

        Dictionary<string,float> starting = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        RebirthProgressionDefinition defs = RebirthSurvivorDefinitionRegistry.Bundle.Progression;
        for (int i = 0; i < defs.Skills.Count; i++)
        {
            RebirthSkillDefinition skill = defs.Skills[i];
            if (skill != null) starting[skill.Id] = defs.SkillKnowledgeMin;
        }
        for (int i = 0; i < background.StartingSkillKnowledge.Count; i++)
        {
            RebirthStartingSkillKnowledgeDefinition k = background.StartingSkillKnowledge[i];
            if (k == null || string.IsNullOrEmpty(k.SkillId) || !starting.ContainsKey(k.SkillId))
                return "Background '"+backgroundId+"' contains invalid Skill Knowledge '"+(k != null ? k.SkillId : "<null>")+"'";
            starting[k.SkillId] = Math.Max(defs.SkillKnowledgeMin, Math.Min(defs.SkillKnowledgeMax, k.Value));
        }

        XElement originKnowledge = new XElement("skillKnowledge");
        XElement progressionKnowledge = new XElement("skillKnowledge");
        List<string> ids = new List<string>(starting.Keys); ids.Sort(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            string value = starting[id].ToString("R", CultureInfo.InvariantCulture);
            originKnowledge.Add(new XElement("skill", new XAttribute("id", id), new XAttribute("value", value)));
            progressionKnowledge.Add(new XElement("skill", new XAttribute("id", id), new XAttribute("value", value)));
        }

        XElement originSkills = origin.Element("skills");
        XElement progressionSkills = progression.Element("skills");
        if (originSkills == null || progressionSkills == null)
            return "origin/progression Skills section is missing";
        XElement oldOriginKnowledge = origin.Element("skillKnowledge");
        if (oldOriginKnowledge != null) oldOriginKnowledge.ReplaceWith(originKnowledge); else originSkills.AddAfterSelf(originKnowledge);
        XElement oldProgressionKnowledge = progression.Element("skillKnowledge");
        if (oldProgressionKnowledge != null) oldProgressionKnowledge.ReplaceWith(progressionKnowledge); else progressionSkills.AddAfterSelf(progressionKnowledge);

        // Schema 5 had no theoretical Skill Knowledge. Seeding from immutable Background authoring
        // is deterministic and does not infer theory from current Practical Skill. Legacy binary
        // Knowledge IDs remain untouched as compatibility aliases until Pass 3 converts recipes and
        // procedures to their final individual discovery records.
        origin.SetAttributeValue("definitionHash", RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty);
        origin.SetAttributeValue("definitionVersion", RebirthSurvivorDefinitionRegistry.DefinitionVersion ?? string.Empty);
        const string skillKnowledgePolicy = "world-character-v5-to-v6-skill-knowledge-background-seed-v1";
        string previousPolicy = ((string)root.Attribute("migrationPolicyId") ?? string.Empty).Trim();
        string previousProgressionAudit = ((string)root.Attribute("migrationProgressionAudit") ?? string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema", 6);
        root.SetAttributeValue("migrationPolicyId", string.IsNullOrEmpty(previousPolicy) ? skillKnowledgePolicy : previousPolicy + "+" + skillKnowledgePolicy);
        root.SetAttributeValue("migrationApplied", true);
        root.SetAttributeValue("migrationProgressionAudit", (string.IsNullOrEmpty(previousProgressionAudit) ? string.Empty : previousProgressionAudit + ";") + "skillKnowledge=seeded-from-background;legacyKnowledge=preserved");
        root.SetAttributeValue("schemaVersion", 6);
        return string.Empty;
    }

    private static string Migrate6To7(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter") return "unexpected/missing world-character root";
        XElement progression=root.Element("progression");
        if(progression==null) return "progression section is missing";
        if(progression.Element("studyProgress")==null) progression.Add(new XElement("studyProgress"));
        string previousPolicy=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim();
        const string policy="world-character-v6-to-v7-literature-study-progress-v1";
        root.SetAttributeValue("migrationTargetSchema",7);
        root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(previousPolicy)?policy:previousPolicy+"+"+policy);
        root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("schemaVersion",7);
        return string.Empty;
    }


    private static string Migrate7To8(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter") return "unexpected/missing world-character root";
        XElement progression=root.Element("progression");
        if(progression==null) return "progression section is missing";
        XElement knowledge=progression.Element("knowledge");
        XElement skills=progression.Element("skills");
        if(knowledge==null||skills==null) return "progression Knowledge/Skills section is missing";

        int grants=0;
        // Preserve access from the temporary Chunk E/F broad discovery bridges without making
        // those broad aliases permanent alternatives for newly-created characters.
        if(HasOwnedKnowledge(knowledge,"procedure.construction.framing"))
        {
            grants+=GrantOwnedKnowledge(knowledge,"procedure.construction.furniture_basic");
            grants+=GrantOwnedKnowledge(knowledge,"procedure.construction.upholstery");
            grants+=GrantOwnedKnowledge(knowledge,"procedure.construction.fixtures_cabinetry");
        }
        if(HasOwnedKnowledge(knowledge,"procedure.construction.reinforcement"))
        {
            grants+=GrantOwnedKnowledge(knowledge,"procedure.construction.fixtures_cabinetry");
            grants+=GrantOwnedKnowledge(knowledge,"procedure.construction.secure_storage");
        }
        if(HasOwnedKnowledge(knowledge,"knowledge.tailoring.basic"))
        {
            string[] ids={
                "pattern.tailoring.assassin_armor","pattern.tailoring.athletic_armor","pattern.tailoring.biker_armor","pattern.tailoring.commando_armor",
                "pattern.tailoring.enforcer_armor","pattern.tailoring.farmer_armor","pattern.tailoring.lumberjack_armor","pattern.tailoring.miner_armor",
                "pattern.tailoring.nerd_armor","pattern.tailoring.nomad_armor","pattern.tailoring.preacher_armor","pattern.tailoring.raider_armor",
                "pattern.tailoring.ranger_armor","pattern.tailoring.scavenger_armor","schematic.tailoring.armor_storage_mods",
                "schematic.tailoring.armor_protection_mods","schematic.tailoring.armor_stealth_mods","schematic.tailoring.armor_utility_mods"};
            for(int i=0;i<ids.Length;i++) grants+=GrantOwnedKnowledge(knowledge,ids[i]);
        }
        if(HasOwnedKnowledge(knowledge,"knowledge.farming.practical"))
        {
            grants+=GrantOwnedKnowledge(knowledge,"procedure.farming.apiary");
            grants+=GrantOwnedKnowledge(knowledge,"procedure.farming.poultry");
            grants+=GrantOwnedKnowledge(knowledge,"procedure.farming.water_harvesting");
        }
        if(HasOwnedKnowledge(knowledge,"knowledge.maintenance.general"))
        {
            grants+=GrantOwnedKnowledge(knowledge,"schematic.maintenance.club_mods");
            grants+=GrantOwnedKnowledge(knowledge,"schematic.maintenance.tool_edge_mods");
            grants+=GrantOwnedKnowledge(knowledge,"schematic.maintenance.powered_melee_mods");
        }
        if(HasOwnedKnowledge(knowledge,"knowledge.construction.basic"))
            grants+=GrantOwnedKnowledge(knowledge,"procedure.construction.concealed_traps");
        if(HasOwnedKnowledge(knowledge,"knowledge.electrical.fundamentals"))
        {
            grants+=GrantOwnedKnowledge(knowledge,"procedure.electrical.battery_service");
            grants+=GrantOwnedKnowledge(knowledge,"procedure.electrical.generator_service");
            grants+=GrantOwnedKnowledge(knowledge,"procedure.electrical.control_devices");
            grants+=GrantOwnedKnowledge(knowledge,"recipe.electricfencepost");
        }

        // Recipes that were intentionally pure Skill gates before Chunk G are grandfathered only
        // for existing schema-7 characters who had already reached that family's minimum Skill.
        grants+=GrantForSkill(skills,knowledge,"skill.construction",15f,"procedure.construction.glazing");
        grants+=GrantForSkill(skills,knowledge,"skill.explosives",20f,"formula.explosives.projectile_payloads");
        grants+=GrantForSkill(skills,knowledge,"skill.explosives",5f,"formula.explosives.improvised");
        grants+=GrantForSkill(skills,knowledge,"skill.explosives",20f,"formula.explosives.grenades");
        grants+=GrantForSkill(skills,knowledge,"skill.explosives",10f,"formula.explosives.mines");
        grants+=GrantForSkill(skills,knowledge,"skill.explosives",30f,"formula.explosives.rockets");
        grants+=GrantForSkill(skills,knowledge,"skill.archery",10f,"pattern.archery.bowmaking");
        grants+=GrantForSkill(skills,knowledge,"skill.archery",15f,"schematic.archery.crossbows_compound");
        grants+=GrantForSkill(skills,knowledge,"skill.archery",10f,"schematic.archery.specialty_ammo");
        grants+=GrantForSkill(skills,knowledge,"skill.archery",15f,"schematic.archery.bow_mods");
        grants+=GrantForSkill(skills,knowledge,"skill.metalworking",10f,"pattern.metalworking.hand_tools");
        grants+=GrantForSkill(skills,knowledge,"skill.metalworking",30f,"schematic.metalworking.power_tools");
        grants+=GrantForSkill(skills,knowledge,"skill.metalworking",10f,"procedure.metalworking.service_tools");
        grants+=GrantForSkill(skills,knowledge,"skill.metalworking",5f,"pattern.metalworking.weapon_forging");
        grants+=GrantForSkill(skills,knowledge,"skill.metalworking",5f,"procedure.metalworking.cookware");
        grants+=GrantForSkill(skills,knowledge,"skill.metalworking",15f,"schematic.metalworking.rocket_components");
        grants+=GrantForSkill(skills,knowledge,"skill.gunsmithing",10f,"schematic.gunsmithing.standard_ammo");
        grants+=GrantForSkill(skills,knowledge,"skill.electrical",5f,"procedure.electrical.service_tools");
        grants+=GrantForSkill(skills,knowledge,"skill.chemistry",10f,"formula.chemistry.fuel_blending");
        grants+=GrantForSkill(skills,knowledge,"skill.deployable_turrets",5f,"schematic.turrets.ammunition");

        const string policy="world-character-v7-to-v8-crafting-knowledge-grandfather-v1";
        string previousPolicy=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim();
        string previousAudit=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema",8);
        root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(previousPolicy)?policy:previousPolicy+"+"+policy);
        root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(previousAudit)?string.Empty:previousAudit+";")+"craftKnowledgeGrandfather="+grants.ToString(CultureInfo.InvariantCulture));
        root.SetAttributeValue("schemaVersion",8);
        return string.Empty;
    }

    private static string Migrate8To9(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter") return "unexpected/missing world-character root";
        XElement origin=root.Element("origin"), progression=root.Element("progression");
        if(origin==null||progression==null) return "origin/progression section is missing";
        XElement originSkills=origin.Element("skills"), progressionSkills=progression.Element("skills");
        XElement originTheory=origin.Element("skillKnowledge"), progressionTheory=progression.Element("skillKnowledge");
        if(originSkills==null||progressionSkills==null||originTheory==null||progressionTheory==null) return "Skill/Skill Knowledge sections are missing";
        if(!RebirthSurvivorDefinitionRegistry.IsReady) return "current Survivor definitions are unavailable; cannot migrate Animal Handling safely";

        string backgroundId=((string)origin.Attribute("backgroundId")??string.Empty).Trim();
        float starting=string.Equals(backgroundId,"background.park_ranger_outdoor_guide",StringComparison.OrdinalIgnoreCase)?3f:0f;
        EnsureSkillEntry(originSkills,RebirthAnimalHandlingService.SkillId,starting,false,true);
        EnsureSkillEntry(progressionSkills,RebirthAnimalHandlingService.SkillId,starting,true,false);
        EnsureSkillTheoryEntry(originTheory,RebirthAnimalHandlingService.SkillId,0f);
        EnsureSkillTheoryEntry(progressionTheory,RebirthAnimalHandlingService.SkillId,0f);

        origin.SetAttributeValue("definitionHash",RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty);
        origin.SetAttributeValue("definitionVersion",RebirthSurvivorDefinitionRegistry.DefinitionVersion??string.Empty);
        const string policy="world-character-v8-to-v9-animal-handling-v1";
        string previousPolicy=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim();
        string previousAudit=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema",9);
        root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(previousPolicy)?policy:previousPolicy+"+"+policy);
        root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(previousAudit)?string.Empty:previousAudit+";")+"animalHandling="+starting.ToString("R",CultureInfo.InvariantCulture)+";parkRangerSmallBias=3");
        root.SetAttributeValue("schemaVersion",9);
        return string.Empty;
    }

    private static string Migrate9To10(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter") return "unexpected/missing world-character root";
        XElement progression=root.Element("progression");if(progression==null)return "progression section is missing";
        if(progression.Element("disciplines")==null)progression.Add(new XElement("disciplines"));
        if(progression.Element("accomplishments")==null)progression.Add(new XElement("accomplishments"));
        if(progression.Element("trials")==null)progression.Add(new XElement("trials"));
        if(progression.Element("wildTaming")==null)progression.Add(new XElement("wildTaming"));
        const string policy="world-character-v9-to-v10-advanced-disciplines-v1";
        string previousPolicy=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim();
        string previousAudit=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema",10);
        root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(previousPolicy)?policy:previousPolicy+"+"+policy);
        root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(previousAudit)?string.Empty:previousAudit+";")+"advancedDisciplines=empty;beastmasterAutoGrant=false");
        root.SetAttributeValue("schemaVersion",10);
        return string.Empty;
    }
    private static string Migrate10To11(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter") return "unexpected/missing world-character root";
        XElement origin=root.Element("origin"), progression=root.Element("progression");
        if(origin==null||progression==null) return "origin/progression section is missing";
        XElement originSkills=origin.Element("skills"), progressionSkills=progression.Element("skills"), originTheory=origin.Element("skillKnowledge"), progressionTheory=progression.Element("skillKnowledge");
        if(originSkills==null||progressionSkills==null||originTheory==null||progressionTheory==null) return "Skill/Skill Knowledge sections are missing";
        EnsureSkillEntry(originSkills,RebirthSurvivorIds.SkillBlackMagic,0f,false,true);
        EnsureSkillEntry(progressionSkills,RebirthSurvivorIds.SkillBlackMagic,0f,true,false);
        EnsureSkillTheoryEntry(originTheory,RebirthSurvivorIds.SkillBlackMagic,0f);
        EnsureSkillTheoryEntry(progressionTheory,RebirthSurvivorIds.SkillBlackMagic,0f);
        XElement trials=progression.Element("trials");if(trials==null){trials=new XElement("trials");progression.Add(trials);}if(trials.Attribute("witchDoctorAttunements")==null)trials.SetAttributeValue("witchDoctorAttunements","0");
        origin.SetAttributeValue("definitionHash",RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty);
        origin.SetAttributeValue("definitionVersion",RebirthSurvivorDefinitionRegistry.DefinitionVersion??string.Empty);
        const string policy="world-character-v10-to-v11-black-magic-v1";
        string previousPolicy=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim();
        string previousAudit=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema",11);root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(previousPolicy)?policy:previousPolicy+"+"+policy);root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(previousAudit)?string.Empty:previousAudit+";")+"blackMagic=0;witchDoctorAutoGrant=false;attunements=0");root.SetAttributeValue("schemaVersion",11);return string.Empty;
    }

    private static string Migrate11To12(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter") return "unexpected/missing world-character root";
        XElement progression=root.Element("progression");
        if(progression==null) return "progression section is missing";
        XElement knowledge=progression.Element("knowledge");
        if(knowledge==null){knowledge=new XElement("knowledge");progression.Add(knowledge);}
        // Chunk G does not auto-grant Binding/Summoning Knowledge during migration.
        // Existing survivors keep exactly what they knew and unlock new techniques through
        // Black Magic milestones / later literature authoring.
        const string policy="world-character-v11-to-v12-undead-binding-v1";
        string previousPolicy=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim();
        string previousAudit=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema",12);
        root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(previousPolicy)?policy:previousPolicy+"+"+policy);
        root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(previousAudit)?string.Empty:previousAudit+";")+"undeadBindingKnowledgeAutoGrant=false;boundUndeadAggregateFormat=3");
        root.SetAttributeValue("schemaVersion",12);
        return string.Empty;
    }


    private static string Migrate12To13(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter") return "unexpected/missing world-character root";
        XElement origin=root.Element("origin"), progression=root.Element("progression");
        if(origin==null||progression==null)return "origin/progression section is missing";
        XElement os=origin.Element("skills"), ps=progression.Element("skills"), ot=origin.Element("skillKnowledge"), pt=progression.Element("skillKnowledge");
        if(os==null||ps==null||ot==null||pt==null)return "Skill/Skill Knowledge sections are missing";
        EnsureSkillEntry(os,RebirthSurvivorIds.SkillRage,0f,false,true); EnsureSkillEntry(ps,RebirthSurvivorIds.SkillRage,0f,true,false);
        EnsureSkillTheoryEntry(ot,RebirthSurvivorIds.SkillRage,0f); EnsureSkillTheoryEntry(pt,RebirthSurvivorIds.SkillRage,0f);
        XElement trials=progression.Element("trials"); if(trials==null){trials=new XElement("trials");progression.Add(trials);} if(trials.Attribute("berserkerMeleeHits")==null)trials.SetAttributeValue("berserkerMeleeHits","0");
        const string policy="world-character-v12-to-v13-rage-v1"; string pp=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim(); string pa=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema",13); root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(pp)?policy:pp+"+"+policy); root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(pa)?string.Empty:pa+";")+"rage=0;berserkerAutoGrant=false;berserkerMeleeHits=0"); root.SetAttributeValue("schemaVersion",13); return string.Empty;
    }

    private static string Migrate13To14(XDocument document)
    {
        XElement root=document!=null?document.Root:null; if(root==null||root.Name!="rebirthWorldCharacter")return "unexpected/missing world-character root";
        XElement origin=root.Element("origin"),progression=root.Element("progression"); if(origin==null||progression==null)return "origin/progression section is missing";
        XElement os=origin.Element("skills"),ps=progression.Element("skills"),ot=origin.Element("skillKnowledge"),pt=progression.Element("skillKnowledge"); if(os==null||ps==null||ot==null||pt==null)return "Skill/Skill Knowledge sections are missing";
        float ob=ProgressionSkillValue(os,RebirthSurvivorIds.SkillBartering);if(ob==float.MinValue)ob=0f;ob=Math.Min(100f,Math.Max(0f,ob));
        float pb=ProgressionSkillValue(ps,RebirthSurvivorIds.SkillBartering);if(pb==float.MinValue)pb=0f;pb=Math.Min(100f,Math.Max(0f,pb));
        float ok=ProgressionSkillValue(ot,RebirthSurvivorIds.SkillBartering);if(ok==float.MinValue)ok=0f;ok=Math.Min(100f,Math.Max(0f,ok));
        float pk=ProgressionSkillValue(pt,RebirthSurvivorIds.SkillBartering);if(pk==float.MinValue)pk=0f;pk=Math.Min(100f,Math.Max(0f,pk));
        EnsureSkillEntry(os,RebirthSurvivorIds.SkillTrading,ob,false,true);EnsureSkillEntry(ps,RebirthSurvivorIds.SkillTrading,pb,true,false);EnsureSkillTheoryEntry(ot,RebirthSurvivorIds.SkillTrading,ok);EnsureSkillTheoryEntry(pt,RebirthSurvivorIds.SkillTrading,pk);
        // PC024 introduced Drink Preparation without advancing the world-character schema. Repair
        // schema-13 records here: existing Bartenders receive the approved starting package; all
        // other backgrounds receive neutral entries. Existing schema-13 entries are preserved.
        bool bartender=string.Equals(((string)origin.Attribute("backgroundId")??string.Empty).Trim(),"background.bartender",StringComparison.OrdinalIgnoreCase);
        EnsureSkillEntry(os,"skill.drink_preparation",bartender?25f:0f,false,true);EnsureSkillEntry(ps,"skill.drink_preparation",bartender?25f:0f,true,false);EnsureSkillTheoryEntry(ot,"skill.drink_preparation",bartender?30f:0f);EnsureSkillTheoryEntry(pt,"skill.drink_preparation",bartender?30f:0f);
        origin.SetAttributeValue("definitionHash",RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty);origin.SetAttributeValue("definitionVersion",RebirthSurvivorDefinitionRegistry.DefinitionVersion??string.Empty);
        const string policy="world-character-v13-to-v14-trading-v1";string pp=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim(),pa=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema",14);root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(pp)?policy:pp+"+"+policy);root.SetAttributeValue("migrationApplied",true);root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(pa)?string.Empty:pa+";")+"tradingFromPositiveLegacyBartering=true;legacyBarteringPreserved=true;drinkPreparationSchema13Backfill=true");root.SetAttributeValue("schemaVersion",14);return string.Empty;
    }

    private static string Migrate14To15(XDocument document)
    {
        XElement root=document!=null?document.Root:null; if(root==null||root.Name!="rebirthWorldCharacter")return "unexpected/missing world-character root";
        XElement origin=root.Element("origin"),progression=root.Element("progression"); if(origin==null||progression==null)return "origin/progression section is missing";
        XElement os=origin.Element("skills"),ps=progression.Element("skills"),ot=origin.Element("skillKnowledge"),pt=progression.Element("skillKnowledge"); if(os==null||ps==null||ot==null||pt==null)return "Skill/Skill Knowledge sections are missing";
        bool teacher=string.Equals(((string)origin.Attribute("backgroundId")??string.Empty).Trim(),"background.teacher",StringComparison.OrdinalIgnoreCase);
        EnsureSkillEntry(os,RebirthSurvivorIds.SkillTeaching,teacher?40f:0f,false,true); EnsureSkillEntry(ps,RebirthSurvivorIds.SkillTeaching,teacher?40f:0f,true,false);
        EnsureSkillTheoryEntry(ot,RebirthSurvivorIds.SkillTeaching,teacher?45f:0f); EnsureSkillTheoryEntry(pt,RebirthSurvivorIds.SkillTeaching,teacher?45f:0f);
        XElement teaching=progression.Element("teaching"); if(teaching==null){teaching=new XElement("teaching",new XElement("lessons"),new XElement("history"));progression.Add(teaching);}
        if(teaching.Element("lessons")==null)teaching.Add(new XElement("lessons")); if(teaching.Element("history")==null)teaching.Add(new XElement("history"));
        origin.SetAttributeValue("definitionHash",RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty); origin.SetAttributeValue("definitionVersion",RebirthSurvivorDefinitionRegistry.DefinitionVersion??string.Empty);
        const string policy="world-character-v14-to-v15-teaching-v1"; string pp=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim(),pa=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        root.SetAttributeValue("migrationTargetSchema",15); root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(pp)?policy:pp+"+"+policy); root.SetAttributeValue("migrationApplied",true); root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(pa)?string.Empty:pa+";")+"teachingSkillBackfill=true;teacherStartingBiasPreserved=true;teachingRuntimeStateInitialized=true"); root.SetAttributeValue("schemaVersion",15); return string.Empty;
    }

    private static string Migrate15To16(XDocument document)
    {
        XElement root=document!=null?document.Root:null; if(root==null||root.Name!="rebirthWorldCharacter")return "unexpected/missing world-character root";
        XElement origin=root.Element("origin"),progression=root.Element("progression"); if(origin==null||progression==null)return "origin/progression section is missing";
        XElement os=origin.Element("skills"),ps=progression.Element("skills"),ot=origin.Element("skillKnowledge"),pt=progression.Element("skillKnowledge"); if(os==null||ps==null||ot==null||pt==null)return "Skill/Skill Knowledge sections are missing";
        string[] currentIds=RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds();
        for(int i=0;i<currentIds.Length;i++)
        {
            EnsureSkillEntryIfMissing(os,currentIds[i],false);
            EnsureSkillEntryIfMissing(ps,currentIds[i],true);
            EnsureSkillTheoryEntry(ot,currentIds[i],0f);
            EnsureSkillTheoryEntry(pt,currentIds[i],0f);
        }
        XElement teaching=progression.Element("teaching"); if(teaching==null){teaching=new XElement("teaching",new XElement("lessons"),new XElement("history"));progression.Add(teaching);}
        if(teaching.Element("lessons")==null)teaching.Add(new XElement("lessons")); if(teaching.Element("history")==null)teaching.Add(new XElement("history"));
        origin.SetAttributeValue("definitionHash",RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty); origin.SetAttributeValue("definitionVersion",RebirthSurvivorDefinitionRegistry.DefinitionVersion??string.Empty);
        const string policy="world-character-v15-to-v16-background-signature-final-reconcile-v1"; string pp=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim(),pa=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        string audit="finalSkillCatalogue="+currentIds.Length.ToString(CultureInfo.InvariantCulture)+";finalSkillKnowledgeCatalogue="+currentIds.Length.ToString(CultureInfo.InvariantCulture)+";signatureBonusOwnershipDerived=true;itemWorldProvenanceVersioned=true";
        root.SetAttributeValue("migrationTargetSchema",16); root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(pp)?policy:pp+"+"+policy); root.SetAttributeValue("migrationApplied",true); root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(pa)?string.Empty:pa+";")+audit); root.SetAttributeValue("schemaVersion",16); return string.Empty;
    }


    private static string Migrate16To17(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter")return "unexpected/missing world-character root";
        XElement origin=root.Element("origin"),progression=root.Element("progression");
        if(origin==null||progression==null)return "origin/progression section is missing";
        XElement originAttributes=origin.Element("attributes"),progressionAttributes=progression.Element("attributes");
        if(originAttributes==null||progressionAttributes==null)return "origin/progression Attributes section is missing";

        string error;
        if(!ValidateLegacyThreeAttributes(originAttributes,"origin",out error))return error;
        if(!ValidateLegacyThreeAttributes(progressionAttributes,"progression",out error))return error;

        // Preserve every legacy S/D/C value exactly and add the two restored Attributes at the
        // neutral existing-character baseline. This migration deliberately does not redistribute
        // historical Skill gains into Intelligence or Charisma.
        originAttributes.Add(NewAttribute("intelligence",50f,75f));
        originAttributes.Add(NewAttribute("charisma",50f,75f));
        progressionAttributes.Add(NewAttribute("intelligence",50f,75f));
        progressionAttributes.Add(NewAttribute("charisma",50f,75f));

        // Initialize empty persistent anti-spam state. No historical healing/exposure is
        // reconstructed and therefore no retroactive direct Constitution award is possible.
        if(progression.Element("constitutionTraining")!=null)return "schema-16 unexpectedly already contains Constitution training state";
        progression.Add(new XElement("constitutionTraining",
            new XAttribute("directWindowStartedActiveSeconds","0"),
            new XAttribute("healingFractionInWindow","0"),
            new XAttribute("rawDirectAwardInWindow","0"),
            new XElement("exposureCooldowns")));

        if(!RebirthSurvivorDefinitionRegistry.IsReady)
            return "current Survivor definitions are unavailable; cannot bind five-Attribute definition identity safely";
        origin.SetAttributeValue("definitionHash",RebirthSurvivorDefinitionRegistry.SemanticHash??string.Empty);
        origin.SetAttributeValue("definitionVersion",RebirthSurvivorDefinitionRegistry.DefinitionVersion??string.Empty);

        const string policy="world-character-v16-to-v17-five-attributes-v1";
        string pp=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim();
        string pa=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        string audit="attributes3to5=true;legacyStrengthDexterityConstitutionPreserved=true;intelligenceNeutral=50/75;charismaNeutral=50/75;historicalRedistribution=false;constitutionTrainingStateInitialized=true;retroactiveDirectConstitutionAward=false";
        root.SetAttributeValue("migrationTargetSchema",17);
        root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(pp)?policy:pp+"+"+policy);
        root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(pa)?string.Empty:pa+";")+audit);
        root.SetAttributeValue("schemaVersion",17);
        return string.Empty;
    }

    private static string Migrate17To18(XDocument document)
    {
        XElement root=document!=null?document.Root:null;
        if(root==null||root.Name!="rebirthWorldCharacter")return "unexpected/missing world-character root";
        XElement progression=root.Element("progression");
        if(progression==null)return "progression section is missing";
        if(progression.Element("skillAwardCooldowns")!=null)return "schema-17 unexpectedly already contains Skill award cooldown state";
        if(progression.Element("commerceTraining")!=null)return "schema-17 unexpectedly already contains commerce training state";

        // New anti-repeat state begins empty. Existing Skill/Attribute/Theory/study/receipt state is
        // not rewritten, and no elapsed wall time or historical transactions are fabricated.
        progression.Add(new XElement("skillAwardCooldowns"));
        progression.Add(new XElement("commerceTraining",new XAttribute("lastGlobalAwardActiveSeconds","-1")));

        const string policy="world-character-v17-to-v18-persisted-skill-antirepeat-v1";
        string pp=((string)root.Attribute("migrationPolicyId")??string.Empty).Trim();
        string pa=((string)root.Attribute("migrationProgressionAudit")??string.Empty).Trim();
        string audit="skillAwardCooldownsInitialized=true;commerceTrainingInitialized=true;activePlayClock=true;offlineTimeDoesNotConsumeCooldowns=true;existingProgressionUnchanged=true;retroactiveAwards=false";
        root.SetAttributeValue("migrationTargetSchema",18);
        root.SetAttributeValue("migrationPolicyId",string.IsNullOrEmpty(pp)?policy:pp+"+"+policy);
        root.SetAttributeValue("migrationApplied",true);
        root.SetAttributeValue("migrationProgressionAudit",(string.IsNullOrEmpty(pa)?string.Empty:pa+";")+audit);
        root.SetAttributeValue("schemaVersion",18);
        return string.Empty;
    }

    private static string Migrate18To19(XDocument document)
    {
        XElement root = document != null ? document.Root : null;
        if (root == null || root.Name != "rebirthWorldCharacter") return "unexpected/missing world-character root";
        XElement support = root.Element("support");
        if (support != null && support.Element("gearTransfers") != null)
            return "schema-18 unexpectedly contains gear transfer custody";
        if (support == null) { support = new XElement("support"); root.Add(support); }
        support.Add(new XElement("gearTransfers", new XAttribute("revision", 0)));
        const string policy = "world-character-v18-to-v19-gear-custody-v1";
        string previous = (string)root.Attribute("migrationPolicyId") ?? string.Empty;
        root.SetAttributeValue("migrationPolicyId", previous.Length == 0 ? policy : previous + "+" + policy);
        root.SetAttributeValue("migrationTargetSchema", 19);
        root.SetAttributeValue("migrationApplied", true);
        root.SetAttributeValue("schemaVersion", 19);
        return string.Empty;
    }

    private static bool ValidateLegacyThreeAttributes(XElement attributes,string owner,out string error)
    {
        error=string.Empty;
        string[] expected={"strength","dexterity","constitution"};
        Dictionary<string,XElement> seen=new Dictionary<string,XElement>(StringComparer.OrdinalIgnoreCase);
        foreach(XElement node in attributes.Elements("attribute"))
        {
            string id=((string)node.Attribute("id")??string.Empty).Trim();
            if(string.IsNullOrEmpty(id)){error=owner+" Attribute id is blank";return false;}
            if(seen.ContainsKey(id)){error=owner+" contains duplicate Attribute '"+id+"'";return false;}
            bool known=false;for(int i=0;i<expected.Length;i++)if(string.Equals(id,expected[i],StringComparison.OrdinalIgnoreCase)){known=true;break;}
            if(!known){error=owner+" schema-16 contains unexpected Attribute '"+id+"'";return false;}
            float current,potential;
            if(!float.TryParse((string)node.Attribute("current"),NumberStyles.Float,CultureInfo.InvariantCulture,out current)||float.IsNaN(current)||float.IsInfinity(current)||
               !float.TryParse((string)node.Attribute("potential"),NumberStyles.Float,CultureInfo.InvariantCulture,out potential)||float.IsNaN(potential)||float.IsInfinity(potential))
            { error=owner+" Attribute '"+id+"' has invalid Current/Potential"; return false; }
            if(current<0f||current>100f||potential<0f||potential>100f){error=owner+" Attribute '"+id+"' is outside 0..100";return false;}
            seen[id]=node;
        }
        for(int i=0;i<expected.Length;i++)if(!seen.ContainsKey(expected[i])){error=owner+" schema-16 is missing Attribute '"+expected[i]+"'";return false;}
        if(seen.Count!=3){error=owner+" schema-16 Attribute count is not exactly 3";return false;}
        return true;
    }

    private static XElement NewAttribute(string id,float current,float potential)
    {
        return new XElement("attribute",new XAttribute("id",id),new XAttribute("current",current.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("potential",potential.ToString("R",CultureInfo.InvariantCulture)));
    }

    private static void EnsureSkillEntryIfMissing(XElement skills,string id,bool includeProgress)
    {
        foreach(XElement node in skills.Elements("skill"))
            if(string.Equals(((string)node.Attribute("id")??string.Empty).Trim(),id,StringComparison.OrdinalIgnoreCase)) return;
        XElement created=new XElement("skill",new XAttribute("id",id),new XAttribute("value","0"));
        if(includeProgress)created.SetAttributeValue("progress","0");
        skills.Add(created);
    }

    private static void EnsureSkillEntry(XElement skills,string id,float minimumValue,bool includeProgress,bool immutableOrigin)
    {
        XElement found=null;
        foreach(XElement node in skills.Elements("skill"))
            if(string.Equals(((string)node.Attribute("id")??string.Empty).Trim(),id,StringComparison.OrdinalIgnoreCase)){found=node;break;}
        if(found==null)
        {
            found=new XElement("skill",new XAttribute("id",id),new XAttribute("value",minimumValue.ToString("R",CultureInfo.InvariantCulture)));
            if(includeProgress) found.SetAttributeValue("progress","0");
            skills.Add(found); return;
        }
        float current;
        if(!float.TryParse((string)found.Attribute("value"),NumberStyles.Float,CultureInfo.InvariantCulture,out current)) current=0f;
        if(immutableOrigin) current=minimumValue; else current=Math.Max(current,minimumValue);
        found.SetAttributeValue("value",current.ToString("R",CultureInfo.InvariantCulture));
        if(includeProgress&&found.Attribute("progress")==null) found.SetAttributeValue("progress","0");
    }

    private static void EnsureSkillTheoryEntry(XElement theory,string id,float value)
    {
        foreach(XElement node in theory.Elements("skill"))
            if(string.Equals(((string)node.Attribute("id")??string.Empty).Trim(),id,StringComparison.OrdinalIgnoreCase)) return;
        theory.Add(new XElement("skill",new XAttribute("id",id),new XAttribute("value",value.ToString("R",CultureInfo.InvariantCulture))));
    }

    private static bool HasOwnedKnowledge(XElement knowledge,string id)
    {
        if(knowledge==null||string.IsNullOrEmpty(id)) return false;
        foreach(XElement node in knowledge.Elements("owned"))
            if(string.Equals(((string)node.Attribute("id")??string.Empty).Trim(),id,StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static int GrantOwnedKnowledge(XElement knowledge,string id)
    {
        if(knowledge==null||string.IsNullOrEmpty(id)||HasOwnedKnowledge(knowledge,id)) return 0;
        knowledge.Add(new XElement("owned",new XAttribute("id",id)));
        return 1;
    }

    private static float ProgressionSkillValue(XElement skills,string id)
    {
        if(skills==null||string.IsNullOrEmpty(id)) return float.MinValue;
        foreach(XElement node in skills.Elements("skill"))
        {
            if(!string.Equals(((string)node.Attribute("id")??string.Empty).Trim(),id,StringComparison.OrdinalIgnoreCase)) continue;
            float value;
            if(float.TryParse((string)node.Attribute("value"),NumberStyles.Float,CultureInfo.InvariantCulture,out value)) return value;
            return float.MinValue;
        }
        return float.MinValue;
    }

    private static int GrantForSkill(XElement skills,XElement knowledge,string skillId,float minimum,string knowledgeId)
    {
        float value=ProgressionSkillValue(skills,skillId);
        return value+0.0001f>=minimum ? GrantOwnedKnowledge(knowledge,knowledgeId) : 0;
    }

    public static bool TryMigrateToCurrent(XDocument document, out bool changed, out string error)
    {
        changed = false;
        error = string.Empty;
        if (document == null || document.Root == null || document.Root.Name != "rebirthWorldCharacter")
        {
            error = "unexpected/missing world-character root";
            return false;
        }

        int originalVersion;
        if (!TrySchemaVersion(document, out originalVersion, out error))
            return false;
        if (originalVersion > RebirthWorldCharacterRecord.CurrentSchemaVersion)
        {
            error = "world-character schema " + originalVersion.ToString(CultureInfo.InvariantCulture) + " is newer than supported schema " + RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture);
            return false;
        }
        if (originalVersion == RebirthWorldCharacterRecord.CurrentSchemaVersion)
            return true;

        // Work on a clone so every migration chain is transactional from the caller's point of view.
        XDocument working = new XDocument(document);
        working.Root.SetAttributeValue("migrationSourceSchema", originalVersion);
        working.Root.SetAttributeValue("migrationTargetSchema", RebirthWorldCharacterRecord.CurrentSchemaVersion);
        working.Root.SetAttributeValue("migrationApplied", true);
        int version = originalVersion;
        while (version < RebirthWorldCharacterRecord.CurrentSchemaVersion)
        {
            Func<XDocument, string> step;
            if (!Steps.TryGetValue(version, out step) || step == null)
            {
                error = "no explicit world-character migration registered from schema " + version.ToString(CultureInfo.InvariantCulture);
                return false;
            }
            string stepError = step(working);
            if (!string.IsNullOrEmpty(stepError))
            {
                error = "world-character migration from schema " + version.ToString(CultureInfo.InvariantCulture) + " failed: " + stepError;
                return false;
            }
            int next;
            string nextError;
            if (!TrySchemaVersion(working, out next, out nextError) || next <= version)
            {
                error = "world-character migration did not advance schemaVersion from " + version.ToString(CultureInfo.InvariantCulture)
                    + (string.IsNullOrEmpty(nextError) ? string.Empty : ": " + nextError);
                return false;
            }
            version = next;
        }

        document.Declaration = working.Declaration != null
            ? new XDeclaration(working.Declaration.Version, working.Declaration.Encoding, working.Declaration.Standalone)
            : null;
        document.Root.ReplaceWith(new XElement(working.Root));
        changed = true;
        return true;
    }

    private static bool TrySchemaVersion(XDocument document, out int version, out string error)
    {
        version = 0;
        error = string.Empty;
        if (document == null || document.Root == null ||
            !int.TryParse((string)document.Root.Attribute("schemaVersion"), NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
        {
            error = "world-character schemaVersion is missing or invalid";
            return false;
        }
        return true;
    }
}
