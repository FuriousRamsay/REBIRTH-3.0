using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Debug-only deterministic migration vectors for the production schema migration code.
/// No files, profiles, or live character records are mutated by this harness.
/// </summary>
public static class RebirthSurvivorMigrationVectorHarness
{
    private sealed class VectorResult
    {
        public string Name;
        public bool Passed;
        public string Detail;
    }

    public static string RunAll()
    {
        if (!RebirthSurvivorDefinitionRegistry.IsReady)
            RebirthSurvivorInstaller.Install();

        List<VectorResult> results = new List<VectorResult>();
        Add(results, "current schema character", TestCurrentSchemaNoOp);
        Add(results, "schema-5 Background Skill Knowledge seed", TestSchema5SkillKnowledgeSeed);
        Add(results, "schema-11 to 12 undead-binding migration", TestSchema11UndeadBindingMigration);
        Add(results, "schema-12 to 13 Rage/Berserker migration", TestSchema12RageMigration);
        Add(results, "schema-13 Trading/Drink Preparation migration", TestSchema13TradingDrinkMigration);
        Add(results, "schema-14 Teaching migration", TestSchema14TeachingMigration);
        Add(results, "schema-15 final catalogue reconciliation", TestSchema15FinalReconcile);
        Add(results, "schema-16 five-Attribute migration", TestSchema16FiveAttributeMigration);
        Add(results, "schema-17 persisted anti-repeat migration", TestSchema17PersistedAntiRepeatMigration);
        Add(results, "schema-4 all zero", TestAllZero);
        Add(results, "representative melee/firearm/technical", TestRepresentative);
        Add(results, "maxed old Skill values", TestMaxed);
        Add(results, "negative development values", TestNegative);
        Add(results, "known retired + unknown ID", TestUnknownAndRetired);
        Add(results, "duplicate IDs", TestDuplicate);
        Add(results, "missing Skill entries", TestMissing);
        Add(results, "profile definition-hash mismatch", TestProfileHashMismatch);
        Add(results, "world definition-hash mismatch", TestWorldHashMismatch);
        Add(results, "repeated migration idempotence", TestIdempotence);
        Add(results, "explicit exact destination wins", TestExplicitDestinationWins);

        int pass = 0;
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Survivor Migration Vectors] policy=" + RebirthSurvivorSkillMigrationPolicy.PolicyId);
        for (int i = 0; i < results.Count; i++)
        {
            VectorResult r = results[i];
            if (r.Passed) pass++;
            b.Append("  [").Append((i + 1).ToString("00", CultureInfo.InvariantCulture)).Append("] ")
                .Append(r.Name).Append(" -> ").Append(r.Passed ? "PASS" : "FAIL");
            if (!string.IsNullOrEmpty(r.Detail)) b.Append(" :: ").Append(r.Detail);
            b.AppendLine();
        }
        b.Append("  summary=").Append(pass == results.Count ? "PASS" : "FAIL")
            .Append(" pass=").Append(pass).Append(" fail=").Append(results.Count - pass)
            .Append(" total=").Append(results.Count);
        return b.ToString().TrimEnd();
    }

    private static void Add(List<VectorResult> results, string name, Func<string> test)
    {
        try
        {
            string failure = test();
            results.Add(new VectorResult { Name = name, Passed = string.IsNullOrEmpty(failure), Detail = string.IsNullOrEmpty(failure) ? "ok" : failure });
        }
        catch (Exception ex)
        {
            results.Add(new VectorResult { Name = name, Passed = false, Detail = ex.GetType().Name + ": " + ex.Message });
        }
    }

    private static string TestCurrentSchemaNoOp()
    {
        // Build through the production migration chain first, then verify the current schema is a no-op.
        XDocument doc = BuildSchema6("current-hash", RebirthSurvivorIds.BackgroundCleanSlate);
        bool firstChanged; string firstError;
        if (!TryMigrateFixture(doc, out firstChanged, out firstError)) return "setup migration rejected: " + firstError;
        if (!firstChanged || A(doc.Root, "schemaVersion") != RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)) return "setup migration did not reach current schema";
        string before = doc.ToString(SaveOptions.DisableFormatting);
        bool changed; string error;
        if (!TryMigrateFixture(doc, out changed, out error)) return "migration rejected: " + error;
        if (changed) return "current schema unexpectedly reported changed";
        if (before != doc.ToString(SaveOptions.DisableFormatting)) return "current schema document changed";
        return string.Empty;
    }

    private static string TestSchema5SkillKnowledgeSeed()
    {
        XDocument doc = BuildSchema5("legacy-schema5-hash", "background.chef");
        bool changed; string error;
        if (!TryMigrateFixture(doc, out changed, out error)) return "schema5 migration rejected: " + error;
        if (!changed) return "schema5 migration did not report changed";
        if (A(doc.Root, "schemaVersion") != RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)) return "schema5 migration did not become current schema";
        int expected=CurrentSkillCount(); if (CountSkillKnowledge(doc.Root.Element("origin").Element("skillKnowledge")) != expected || CountSkillKnowledge(doc.Root.Element("progression").Element("skillKnowledge")) != expected) return "Skill Knowledge output is not "+expected+"/"+expected;
        if (SkillKnowledgeValue(doc, "origin", "skill.cooking") != 45f || SkillKnowledgeValue(doc, "progression", "skill.cooking") != 45f) return "Chef Cooking Knowledge seed mismatch";
        if (SkillKnowledgeValue(doc, "origin", "skill.knives") != 25f || SkillKnowledgeValue(doc, "progression", "skill.knives") != 25f) return "Chef Knife Knowledge seed mismatch";
        if (SkillKnowledgeValue(doc, "origin", "skill.electrical") != 0f) return "unrelated Chef Skill Knowledge should remain 0";
        if (A(doc.Root.Element("origin"), "definitionHash") != RebirthSurvivorDefinitionRegistry.SemanticHash) return "schema5 origin definition hash was not rebound";
        if (A(doc.Root, "migrationPolicyId").IndexOf("world-character-v5-to-v6-skill-knowledge-background-seed-v1", StringComparison.Ordinal) < 0) return "schema5 Skill Knowledge migration policy missing";
        return string.Empty;
    }


    private static string TestSchema11UndeadBindingMigration()
    {
        XDocument doc = new XDocument(new XElement("rebirthWorldCharacter",
            new XAttribute("schemaVersion", 11),
            new XAttribute("migrationPolicyId", "prior-policy"),
            new XAttribute("migrationProgressionAudit", "prior-audit"),
            new XElement("origin", new XAttribute("backgroundId", RebirthSurvivorIds.BackgroundCleanSlate), BuildLegacyAttributes()),
            new XElement("progression", BuildLegacyAttributes(), new XElement("knowledge",
                new XElement("entry", new XAttribute("id", RebirthSurvivorIds.KnowledgeBlackMagicMindControlI))))));
        bool changed; string error;
        if (!TryMigrateFixture(doc, out changed, out error)) return "schema11 migration rejected: " + error;
        if (!changed) return "schema11 migration did not report changed";
        if (A(doc.Root, "schemaVersion") != RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)) return "schema11 migration did not become current schema";
        string policy = A(doc.Root, "migrationPolicyId");
        if (policy.IndexOf("world-character-v11-to-v12-undead-binding-v1", StringComparison.Ordinal) < 0) return "schema11 binding migration policy missing";
        string audit = A(doc.Root, "migrationProgressionAudit");
        if (audit.IndexOf("undeadBindingKnowledgeAutoGrant=false", StringComparison.Ordinal) < 0 || audit.IndexOf("boundUndeadAggregateFormat=3", StringComparison.Ordinal) < 0) return "schema11 binding migration audit mismatch";
        XElement knowledge = doc.Root.Element("progression").Element("knowledge");
        if (knowledge == null) return "knowledge section missing after schema11 migration";
        string[] forbidden = { RebirthSurvivorIds.KnowledgeBlackMagicUndeadConditioning, RebirthSurvivorIds.KnowledgeBlackMagicBindingRitual, RebirthSurvivorIds.KnowledgeBlackMagicFeralBinding, RebirthSurvivorIds.KnowledgeBlackMagicRadiatedBinding, RebirthSurvivorIds.KnowledgeBlackMagicChargedBinding, RebirthSurvivorIds.KnowledgeBlackMagicInfernalBinding, RebirthSurvivorIds.KnowledgeBlackMagicLesserSummoning, RebirthSurvivorIds.KnowledgeBlackMagicGreaterSummoning, RebirthSurvivorIds.KnowledgeBlackMagicArmyOfTheDead };
        for (int i = 0; i < forbidden.Length; i++) if (knowledge.DescendantsAndSelf().Any(e => string.Equals(A(e, "id"), forbidden[i], StringComparison.OrdinalIgnoreCase))) return "schema11 migration auto-granted " + forbidden[i];
        return string.Empty;
    }

    private static string TestSchema12RageMigration()
    {
        XDocument doc=new XDocument(new XElement("rebirthWorldCharacter",
            new XAttribute("schemaVersion",12),new XAttribute("migrationPolicyId","prior-policy"),new XAttribute("migrationProgressionAudit","prior-audit"),
            new XElement("origin",new XAttribute("backgroundId",RebirthSurvivorIds.BackgroundCleanSlate),BuildLegacyAttributes(),new XElement("skills"),new XElement("skillKnowledge")),
            new XElement("progression",BuildLegacyAttributes(),new XElement("skills"),new XElement("skillKnowledge"),new XElement("knowledge"),new XElement("disciplines"),new XElement("trials"))));
        bool changed;string error;if(!TryMigrateFixture(doc,out changed,out error))return "schema12 migration rejected: "+error;if(!changed)return "schema12 migration did not report changed";
        if(A(doc.Root,"schemaVersion")!=RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))return "schema12 migration did not become current schema";if(Value(doc,"origin",RebirthSurvivorIds.SkillRage)!=0f||Value(doc,"progression",RebirthSurvivorIds.SkillRage)!=0f)return "Rage Skill migration was not neutral";
        XElement tr=doc.Root.Element("progression").Element("trials");if(tr==null||A(tr,"berserkerMeleeHits")!="0")return "Berserker trial migration mismatch";
        XElement ds=doc.Root.Element("progression").Element("disciplines");if(ds!=null&&ds.DescendantsAndSelf().Any(e=>string.Equals(A(e,"id"),RebirthSurvivorIds.DisciplineBerserker,StringComparison.OrdinalIgnoreCase)))return "Berserker auto-granted during migration";
        string audit=A(doc.Root,"migrationProgressionAudit");if(audit.IndexOf("rage=0;berserkerAutoGrant=false;berserkerMeleeHits=0",StringComparison.Ordinal)<0)return "schema12 migration audit mismatch";return string.Empty;
    }

    private static string TestSchema13TradingDrinkMigration()
    {
        XDocument doc=new XDocument(new XElement("rebirthWorldCharacter",
            new XAttribute("schemaVersion",13),new XAttribute("migrationPolicyId","prior-policy"),new XAttribute("migrationProgressionAudit","prior-audit"),
            new XElement("origin",new XAttribute("backgroundId","background.bartender"),BuildLegacyAttributes(),new XElement("skills",new XElement("skill",new XAttribute("id",RebirthSurvivorIds.SkillBartering),new XAttribute("value","18"))),new XElement("skillKnowledge",new XElement("skill",new XAttribute("id",RebirthSurvivorIds.SkillBartering),new XAttribute("value","22")))),
            new XElement("progression",BuildLegacyAttributes(),new XElement("skills",new XElement("skill",new XAttribute("id",RebirthSurvivorIds.SkillBartering),new XAttribute("value","21"),new XAttribute("progress","0.4"))),new XElement("skillKnowledge",new XElement("skill",new XAttribute("id",RebirthSurvivorIds.SkillBartering),new XAttribute("value","24"))),new XElement("knowledge"),new XElement("disciplines"),new XElement("trials"))));
        bool changed;string error;if(!TryMigrateFixture(doc,out changed,out error))return "schema13 migration rejected: "+error;if(!changed)return "schema13 migration did not report changed";
        if(Value(doc,"origin",RebirthSurvivorIds.SkillTrading)!=18f||Value(doc,"progression",RebirthSurvivorIds.SkillTrading)!=21f)return "Trading migration did not preserve positive legacy Bartering";
        if(Value(doc,"origin",RebirthSurvivorIds.SkillDrinkPreparation)!=25f||Value(doc,"progression",RebirthSurvivorIds.SkillDrinkPreparation)!=25f)return "Bartender Drink Preparation migration bias mismatch";
        if(SkillKnowledgeValue(doc,"origin",RebirthSurvivorIds.SkillDrinkPreparation)!=30f||SkillKnowledgeValue(doc,"progression",RebirthSurvivorIds.SkillDrinkPreparation)!=30f)return "Bartender Drink Preparation Knowledge migration mismatch";
        string audit=A(doc.Root,"migrationProgressionAudit");if(audit.IndexOf("tradingFromPositiveLegacyBartering=true",StringComparison.Ordinal)<0||audit.IndexOf("drinkPreparationSchema13Backfill=true",StringComparison.Ordinal)<0)return "schema13 Trading/Drink Preparation audit missing";return string.Empty;
    }

    private static string TestSchema14TeachingMigration()
    {
        XDocument doc=new XDocument(new XElement("rebirthWorldCharacter",
            new XAttribute("schemaVersion",14),new XAttribute("migrationPolicyId","prior-policy"),new XAttribute("migrationProgressionAudit","prior-audit"),
            new XElement("origin",new XAttribute("backgroundId","background.teacher"),BuildLegacyAttributes(),new XElement("skills"),new XElement("skillKnowledge")),
            new XElement("progression",BuildLegacyAttributes(),new XElement("skills"),new XElement("skillKnowledge"),new XElement("knowledge"),new XElement("disciplines"),new XElement("trials"))));
        bool changed;string error;if(!TryMigrateFixture(doc,out changed,out error))return "schema14 migration rejected: "+error;if(!changed)return "schema14 migration did not report changed";
        if(Value(doc,"origin",RebirthSurvivorIds.SkillTeaching)!=40f||Value(doc,"progression",RebirthSurvivorIds.SkillTeaching)!=40f)return "Teacher Teaching Skill migration mismatch";
        if(SkillKnowledgeValue(doc,"origin",RebirthSurvivorIds.SkillTeaching)!=45f||SkillKnowledgeValue(doc,"progression",RebirthSurvivorIds.SkillTeaching)!=45f)return "Teacher Teaching Knowledge migration mismatch";
        XElement teaching=doc.Root.Element("progression").Element("teaching");if(teaching==null||teaching.Element("lessons")==null||teaching.Element("history")==null)return "Teaching runtime state was not initialized";
        if(A(doc.Root,"migrationProgressionAudit").IndexOf("teachingRuntimeStateInitialized=true",StringComparison.Ordinal)<0)return "schema14 Teaching audit missing";return string.Empty;
    }

    private static string TestSchema15FinalReconcile()
    {
        XElement os=new XElement("skills",new XElement("skill",new XAttribute("id","skill.mining"),new XAttribute("value","20")),new XElement("skill",new XAttribute("id","skill.athletics"),new XAttribute("value","-10")));
        XElement ps=new XElement("skills",new XElement("skill",new XAttribute("id","skill.mining"),new XAttribute("value","23"),new XAttribute("progress","0.25")),new XElement("skill",new XAttribute("id","skill.athletics"),new XAttribute("value","-10"),new XAttribute("progress","0.5")));
        XDocument doc=new XDocument(new XElement("rebirthWorldCharacter",new XAttribute("schemaVersion",15),new XAttribute("migrationPolicyId","prior-policy"),new XAttribute("migrationProgressionAudit","prior-audit"),
            new XElement("origin",new XAttribute("backgroundId",RebirthSurvivorIds.BackgroundCleanSlate),BuildLegacyAttributes(),os,new XElement("skillKnowledge",new XElement("skill",new XAttribute("id","skill.mining"),new XAttribute("value","12")))),
            new XElement("progression",BuildLegacyAttributes(),ps,new XElement("skillKnowledge",new XElement("skill",new XAttribute("id","skill.mining"),new XAttribute("value","14"))),new XElement("knowledge"))));
        bool changed;string error;if(!TryMigrateFixture(doc,out changed,out error))return "schema15 migration rejected: "+error;if(!changed)return "schema15 migration did not report changed";
        int expected=CurrentSkillCount();if(CountSkills(doc.Root.Element("origin").Element("skills"))!=expected||CountSkills(doc.Root.Element("progression").Element("skills"))!=expected)return "final Skill catalogue reconciliation mismatch";
        if(CountSkillKnowledge(doc.Root.Element("origin").Element("skillKnowledge"))!=expected||CountSkillKnowledge(doc.Root.Element("progression").Element("skillKnowledge"))!=expected)return "final Skill Knowledge catalogue reconciliation mismatch";
        if(Value(doc,"origin","skill.mining")!=20f||Value(doc,"progression","skill.mining")!=23f||Value(doc,"origin","skill.athletics")!=-10f)return "schema15 reconciliation changed existing Skill values";
        if(doc.Root.Descendants().Any(e=>string.Equals(e.Name.LocalName,"signatureBonus",StringComparison.OrdinalIgnoreCase)))return "migration created duplicate Signature Bonus entitlement state";
        string audit=A(doc.Root,"migrationProgressionAudit");if(audit.IndexOf("signatureBonusOwnershipDerived=true",StringComparison.Ordinal)<0||audit.IndexOf("itemWorldProvenanceVersioned=true",StringComparison.Ordinal)<0)return "final reconciliation audit missing";return string.Empty;
    }

    private static string TestSchema16FiveAttributeMigration()
    {
        XElement oa=BuildLegacyAttributes(61.25f,82f,47.5f,71f,73f,91f);
        XElement pa=BuildLegacyAttributes(64.75f,82f,51.5f,71f,79f,91f);
        XDocument doc=new XDocument(new XElement("rebirthWorldCharacter",new XAttribute("schemaVersion",16),new XAttribute("migrationPolicyId","prior-policy"),new XAttribute("migrationProgressionAudit","prior-audit"),
            new XElement("origin",new XAttribute("definitionHash","legacy16"),new XAttribute("definitionVersion","legacy16"),oa),
            new XElement("progression",pa)));
        bool changed;string error;if(!TryMigrateFixture(doc,out changed,out error))return "schema16 migration rejected: "+error;if(!changed)return "schema16 migration did not report changed";
        if(A(doc.Root,"schemaVersion")!=RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))return "schema16 migration did not become the current schema";
        XElement migratedOrigin=doc.Root.Element("origin").Element("attributes"),migratedProgression=doc.Root.Element("progression").Element("attributes");
        if(AttributeValue(migratedOrigin,"strength","current")!=61.25f||AttributeValue(migratedOrigin,"dexterity","current")!=47.5f||AttributeValue(migratedOrigin,"constitution","current")!=73f)return "origin legacy Attribute values changed";
        if(AttributeValue(migratedProgression,"strength","current")!=64.75f||AttributeValue(migratedProgression,"dexterity","current")!=51.5f||AttributeValue(migratedProgression,"constitution","current")!=79f)return "progression legacy Attribute values changed";
        if(AttributeValue(migratedOrigin,"intelligence","current")!=50f||AttributeValue(migratedOrigin,"intelligence","potential")!=75f||AttributeValue(migratedOrigin,"charisma","current")!=50f||AttributeValue(migratedOrigin,"charisma","potential")!=75f)return "origin neutral Intelligence/Charisma mismatch";
        if(AttributeValue(migratedProgression,"intelligence","current")!=50f||AttributeValue(migratedProgression,"charisma","current")!=50f)return "progression neutral Intelligence/Charisma mismatch";
        XElement constitutionTraining=doc.Root.Element("progression").Element("constitutionTraining");
        if(constitutionTraining==null||A(constitutionTraining,"directWindowStartedActiveSeconds")!="0"||A(constitutionTraining,"healingFractionInWindow")!="0"||A(constitutionTraining,"rawDirectAwardInWindow")!="0"||constitutionTraining.Element("exposureCooldowns")==null||constitutionTraining.Element("exposureCooldowns").Elements("family").Any())return "schema16 Constitution anti-spam state mismatch";
        if(A(doc.Root,"migrationPolicyId").IndexOf("world-character-v16-to-v17-five-attributes-v1",StringComparison.Ordinal)<0)return "schema16 five-Attribute policy missing";
        if(A(doc.Root,"migrationProgressionAudit").IndexOf("constitutionTrainingStateInitialized=true",StringComparison.Ordinal)<0||A(doc.Root,"migrationProgressionAudit").IndexOf("retroactiveDirectConstitutionAward=false",StringComparison.Ordinal)<0)return "schema16 Constitution migration audit missing";
        return string.Empty;
    }

    private static string TestSchema17PersistedAntiRepeatMigration()
    {
        XElement progression=new XElement("progression",
            new XAttribute("healthPotential","77"),
            new XElement("studyProgress",new XElement("item",new XAttribute("id","literature.test"),new XAttribute("progress","0.5"))),
            new XElement("skillAwardReceipts",new XElement("receipt",new XAttribute("id","receipt.keep"))));
        XDocument doc=new XDocument(new XElement("rebirthWorldCharacter",
            new XAttribute("schemaVersion",17),
            new XAttribute("migrationPolicyId","prior-policy"),
            new XAttribute("migrationProgressionAudit","prior-audit"),
            progression));
        bool changed;string error;
        if(!TryMigrateFixture(doc,out changed,out error))return "schema17 migration rejected: "+error;
        if(!changed)return "schema17 migration did not report changed";
        if(A(doc.Root,"schemaVersion")!="18")return "schema17 migration did not become schema18";
        XElement p=doc.Root.Element("progression");
        XElement cooldowns=p!=null?p.Element("skillAwardCooldowns"):null;
        XElement commerce=p!=null?p.Element("commerceTraining"):null;
        if(cooldowns==null||cooldowns.Elements("cooldown").Any())return "Skill award cooldown state was not initialized empty";
        if(commerce==null||A(commerce,"lastGlobalAwardActiveSeconds")!="-1"||commerce.Elements("item").Any())return "commerce anti-loop state was not initialized empty";
        if(p.Element("skillAwardReceipts")?.Element("receipt")?.Attribute("id")?.Value!="receipt.keep")return "existing durable receipt was changed";
        if(A(p,"healthPotential")!="77")return "existing progression value changed";
        if(A(doc.Root,"migrationPolicyId").IndexOf("world-character-v17-to-v18-persisted-skill-antirepeat-v1",StringComparison.Ordinal)<0)return "schema18 migration policy missing";
        if(A(doc.Root,"migrationProgressionAudit").IndexOf("activePlayClock=true",StringComparison.Ordinal)<0)return "schema18 active-play audit missing";
        return string.Empty;
    }

    private static string TestAllZero()
    {
        Dictionary<string,float> values = LegacyValues(0f);
        XDocument doc = BuildSchema4(values, values, 0.25f);
        string error = Migrate(doc); if (error.Length > 0) return error;
        int skillExpected=CurrentSkillCount(); if (CountSkills(doc.Root.Element("origin").Element("skills")) != skillExpected || CountSkills(doc.Root.Element("progression").Element("skills")) != skillExpected) return "Skill output is not "+skillExpected+"/"+skillExpected;
        int expected=CurrentSkillCount(); if (CountSkillKnowledge(doc.Root.Element("origin").Element("skillKnowledge")) != expected || CountSkillKnowledge(doc.Root.Element("progression").Element("skillKnowledge")) != expected) return "Skill Knowledge output is not "+expected+"/"+expected;
        foreach (XElement e in doc.Root.Element("origin").Element("skills").Elements("skill")) if (ReadFloat(e,"value") != 0f) return "non-zero origin output " + A(e,"id");
        if(Value(doc,"origin",RebirthSurvivorIds.SkillBlackMagic)!=0f||Value(doc,"progression",RebirthSurvivorIds.SkillBlackMagic)!=0f)return "Black Magic migration must initialize at zero";
        if(Value(doc,"origin",RebirthSurvivorIds.SkillRage)!=0f||Value(doc,"progression",RebirthSurvivorIds.SkillRage)!=0f)return "Rage migration must initialize at zero";
        XElement progression=doc.Root.Element("progression"),trials=progression!=null?progression.Element("trials"):null,disciplines=progression!=null?progression.Element("disciplines"):null;
        if(trials==null||A(trials,"witchDoctorAttunements")!="0")return "Witch Doctor initiation attunements migration mismatch";
        if(disciplines!=null&&disciplines.Elements().Any(e=>string.Equals(A(e,"id"),RebirthSurvivorIds.DisciplineWitchDoctor,StringComparison.OrdinalIgnoreCase)))return "Witch Doctor must not auto-grant during migration";
        return string.Empty;
    }

    private static string TestRepresentative()
    {
        Dictionary<string,float> origin = new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase) {
            {"skill.bladed_melee",12.5f},{"skill.blunt_melee",18f},{"skill.unarmed",7f},{"skill.handguns",22f},{"skill.rifles",31f},{"skill.mechanics",44f},{"skill.medicine",9f}
        };
        Dictionary<string,float> current = new Dictionary<string,float>(origin,StringComparer.OrdinalIgnoreCase);
        XDocument doc = BuildSchema4(origin,current,0.625f); string error=Migrate(doc); if(error.Length>0)return error;
        string[] swords={"skill.swords","skill.knives","skill.scythes"}; for(int i=0;i<swords.Length;i++) if(Value(doc,"origin",swords[i])!=12.5f)return swords[i]+" origin mismatch";
        string[] rifles={"skill.assault_rifles","skill.tactical_rifles","skill.long_range_rifles"}; for(int i=0;i<rifles.Length;i++){if(Value(doc,"progression",rifles[i])!=31f)return rifles[i]+" current mismatch";if(Progress(doc,rifles[i])!=0.625f)return rifles[i]+" progress mismatch";}
        if(Value(doc,"origin","skill.mechanics")!=44f||Value(doc,"progression","skill.medicine")!=9f)return "technical direct mapping mismatch";
        return string.Empty;
    }

    private static string TestMaxed()
    {
        Dictionary<string,float> values=LegacyValues(100f); XDocument doc=BuildSchema4(values,values,0.999f); string error=Migrate(doc); if(error.Length>0)return error;
        RebirthSurvivorLegacySkillMapping[] maps=RebirthSurvivorSkillMigrationPolicy.GetLegacyMappings();
        for(int i=0;i<maps.Length;i++)for(int j=0;j<maps[i].DestinationIds.Count;j++)if(Value(doc,"progression",maps[i].DestinationIds[j])!=100f)return "lost max value at "+maps[i].DestinationIds[j];
        if(Value(doc,"progression","skill.axes")!=0f)return "unrelated new Skill should remain neutral";
        return string.Empty;
    }

    private static string TestNegative()
    {
        Dictionary<string,float> values=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase){{"skill.bladed_melee",-25f},{"skill.handguns",-10f},{"skill.mining",-3f}};
        XDocument doc=BuildSchema4(values,values,0.1f); string error=Migrate(doc); if(error.Length>0)return error;
        if(Value(doc,"origin","skill.swords")!=-25f||Value(doc,"progression","skill.heavy_handguns")!=-10f||Value(doc,"progression","skill.mining")!=-3f)return "negative signed value was not preserved";
        return string.Empty;
    }

    private static string TestUnknownAndRetired()
    {
        Dictionary<string,float> retired=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase){{"skill.bladed_melee",11f}};
        XDocument accepted=BuildSchema4(retired,retired,0f); string acceptedError=Migrate(accepted); if(acceptedError.Length>0)return "known retired broad ID rejected: "+acceptedError;
        if(Value(accepted,"origin","skill.swords")!=11f)return "known retired broad ID did not map";

        XDocument unknown=BuildSchema4(retired,retired,0f);
        unknown.Root.Element("origin").Element("skills").Add(new XElement("skill",new XAttribute("id","skill.retired_unknown"),new XAttribute("value","17")));
        string before=unknown.ToString(SaveOptions.DisableFormatting); bool changed; string error;
        if(RebirthWorldCharacterMigrationRegistry.TryMigrateToCurrent(unknown,out changed,out error))return "unknown ID unexpectedly migrated";
        if(changed)return "failed migration reported changed";
        if(A(unknown.Root,"schemaVersion")!="4"||before!=unknown.ToString(SaveOptions.DisableFormatting))return "failed migration was not transactional";
        if(error.IndexOf("unknown Skill ids",StringComparison.OrdinalIgnoreCase)<0)return "unknown-ID error was not explicit: "+error;
        return string.Empty;
    }

    private static string TestDuplicate()
    {
        Dictionary<string,float> values=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase){{"skill.mining",10f}};
        XDocument doc=BuildSchema4(values,values,0.2f);
        doc.Root.Element("origin").Element("skills").Add(new XElement("skill",new XAttribute("id","skill.mining"),new XAttribute("value","11")));
        string before=doc.ToString(SaveOptions.DisableFormatting); bool changed; string error;
        if(RebirthWorldCharacterMigrationRegistry.TryMigrateToCurrent(doc,out changed,out error))return "duplicate unexpectedly migrated";
        if(changed||before!=doc.ToString(SaveOptions.DisableFormatting))return "duplicate failure mutated source document";
        if(error.IndexOf("duplicate Skill ids",StringComparison.OrdinalIgnoreCase)<0)return "duplicate error was not explicit: "+error;
        return string.Empty;
    }

    private static string TestMissing()
    {
        Dictionary<string,float> values=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase){{"skill.mining",13f},{"skill.archery",4f}};
        XDocument doc=BuildSchema4(values,values,0.3f); string error=Migrate(doc); if(error.Length>0)return error;
        if(CountSkills(doc.Root.Element("origin").Element("skills"))!=CurrentSkillCount())return "missing entries were not expanded to current Skill catalogue";
        if(Value(doc,"origin","skill.mining")!=13f||Value(doc,"origin","skill.archery")!=4f||Value(doc,"origin","skill.bartering")!=0f)return "missing-entry neutral initialization mismatch";
        return string.Empty;
    }

    private static string TestProfileHashMismatch()
    {
        RebirthSurvivorProfile profile=new RebirthSurvivorProfile(RebirthSurvivorProfile.CurrentSchemaVersion,"chunk2-vector-profile","Chunk 2 Vector","stale-definition-hash","stale-version",RebirthSurvivorIds.BackgroundCleanSlate,RebirthSurvivorIds.DietUnrestricted,new string[0],new Dictionary<string,string>(),DateTime.UtcNow,DateTime.UtcNow);
        RebirthSurvivorProfileCompatibility compatibility=RebirthSurvivorProfileStore.EvaluateCompatibility(profile);
        return compatibility.Kind==RebirthSurvivorProfileCompatibilityKind.NeedsReview?string.Empty:"expected NeedsReview, got "+compatibility.Kind;
    }

    private static string TestWorldHashMismatch()
    {
        XDocument doc=BuildSchema6("seed-hash",RebirthSurvivorIds.BackgroundCleanSlate); bool changed; string error;
        if(!TryMigrateFixture(doc,out changed,out error))return "schema6 upgrade failed: "+error;
        if(!changed)return "schema6 fixture did not perform its required schema upgrade";
        doc.Root.Element("origin").SetAttributeValue("definitionHash","stale-world-definition-hash");
        if(!TryMigrateFixture(doc,out changed,out error))return "current-schema mismatch record rejected by migration layer: "+error;
        if(changed)return "current-schema definition-hash mismatch was rewritten by migration";
        if(A(doc.Root.Element("origin"),"definitionHash")!="stale-world-definition-hash")return "current-schema mismatch hash was silently replaced";
        return string.Empty;
    }

    private static string TestIdempotence()
    {
        Dictionary<string,float> values=LegacyValues(33f); XDocument doc=BuildSchema4(values,values,0.4f); bool changed; string error;
        if(!TryMigrateFixture(doc,out changed,out error)||!changed)return "first migration failed/unchanged: "+error;
        string once=doc.ToString(SaveOptions.DisableFormatting);
        if(!TryMigrateFixture(doc,out changed,out error))return "second migration failed: "+error;
        if(changed)return "second migration reported changed";
        if(once!=doc.ToString(SaveOptions.DisableFormatting))return "second migration changed XML";
        return string.Empty;
    }

    private static string TestExplicitDestinationWins()
    {
        Dictionary<string,float> origin=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase){{"skill.rifles",20f},{"skill.assault_rifles",47f}};
        Dictionary<string,float> current=new Dictionary<string,float>(origin,StringComparer.OrdinalIgnoreCase);
        XDocument doc=BuildSchema4(origin,current,0.55f); string error=Migrate(doc); if(error.Length>0)return error;
        if(Value(doc,"origin","skill.assault_rifles")!=47f)return "explicit assault rifle value was overwritten";
        if(Value(doc,"origin","skill.tactical_rifles")!=20f||Value(doc,"origin","skill.long_range_rifles")!=20f)return "broad source did not fill missing sibling families";
        return string.Empty;
    }

    private static Dictionary<string,float> LegacyValues(float value)
    {
        Dictionary<string,float> result=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        RebirthSurvivorLegacySkillMapping[] rows=RebirthSurvivorSkillMigrationPolicy.GetLegacyMappings();
        for(int i=0;i<rows.Length;i++) result[rows[i].SourceId]=value;
        return result;
    }

    private static XDocument BuildSchema4(IDictionary<string,float> originValues, IDictionary<string,float> progressionValues, float progress)
    {
        return new XDocument(new XElement("rebirthWorldCharacter",new XAttribute("schemaVersion",4),
            new XElement("origin",new XAttribute("definitionHash","legacy-hash"),new XAttribute("definitionVersion","legacy-v4"),new XAttribute("backgroundId",RebirthSurvivorIds.BackgroundCleanSlate),BuildLegacyAttributes(),BuildSkills(originValues,false,0f)),
            new XElement("progression",BuildLegacyAttributes(),BuildSkills(progressionValues,true,progress))));
    }

    private static XDocument BuildSchema5(string definitionHash,string backgroundId)
    {
        Dictionary<string,float> values=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        string[] ids=RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds(); for(int i=0;i<ids.Length;i++)values[ids[i]]=0f;
        return new XDocument(new XElement("rebirthWorldCharacter",new XAttribute("schemaVersion",5),
            new XElement("origin",new XAttribute("definitionHash",definitionHash??string.Empty),new XAttribute("definitionVersion","schema5-vector"),new XAttribute("backgroundId",backgroundId??RebirthSurvivorIds.BackgroundCleanSlate),BuildLegacyAttributes(),BuildSkills(values,false,0f)),
            new XElement("progression",BuildLegacyAttributes(),BuildSkills(values,true,0f))));
    }

    private static XDocument BuildSchema6(string definitionHash,string backgroundId)
    {
        XDocument doc=BuildSchema5(definitionHash,backgroundId);
        Dictionary<string,float> theory=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        string[] ids=RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds(); for(int i=0;i<ids.Length;i++)theory[ids[i]]=0f;
        doc.Root.SetAttributeValue("schemaVersion",6);
        doc.Root.Element("origin").Element("skills").AddAfterSelf(BuildSkillKnowledge(theory));
        doc.Root.Element("progression").Element("skills").AddAfterSelf(BuildSkillKnowledge(theory));
        return doc;
    }

    private static XElement BuildLegacyAttributes(float strengthCurrent=50f,float strengthPotential=75f,float dexterityCurrent=50f,float dexterityPotential=75f,float constitutionCurrent=50f,float constitutionPotential=75f)
    {
        return new XElement("attributes",
            new XElement("attribute",new XAttribute("id","strength"),new XAttribute("current",F(strengthCurrent)),new XAttribute("potential",F(strengthPotential))),
            new XElement("attribute",new XAttribute("id","dexterity"),new XAttribute("current",F(dexterityCurrent)),new XAttribute("potential",F(dexterityPotential))),
            new XElement("attribute",new XAttribute("id","constitution"),new XAttribute("current",F(constitutionCurrent)),new XAttribute("potential",F(constitutionPotential))));
    }

    private static float AttributeValue(XElement attributes,string id,string field)
    {
        XElement node=attributes!=null?attributes.Elements("attribute").FirstOrDefault(e=>string.Equals(A(e,"id"),id,StringComparison.OrdinalIgnoreCase)):null;
        return node==null?float.NaN:ReadFloat(node,field);
    }

    private static XElement BuildSkillKnowledge(IDictionary<string,float> values)
    {
        XElement node=new XElement("skillKnowledge"); if(values==null)return node;
        foreach(KeyValuePair<string,float> pair in values)node.Add(new XElement("skill",new XAttribute("id",pair.Key),new XAttribute("value",F(pair.Value))));
        return node;
    }

    private static XElement BuildSkills(IDictionary<string,float> values,bool hasProgress,float progress)
    {
        XElement skills=new XElement("skills"); if(values==null)return skills;
        foreach(KeyValuePair<string,float> pair in values)
        {
            XElement e=new XElement("skill",new XAttribute("id",pair.Key),new XAttribute("value",F(pair.Value)));
            if(hasProgress)e.Add(new XAttribute("progress",F(progress))); skills.Add(e);
        }
        return skills;
    }

    private static string Migrate(XDocument doc)
    {
        bool changed; string error; if(!TryMigrateFixture(doc,out changed,out error))return error;
        if(!changed)return "schema4 migration did not report changed"; if(A(doc.Root,"schemaVersion")!=RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))return "schema did not become current";
        if(A(doc.Root,"migrationSourceSchema")!="4"||A(doc.Root,"migrationTargetSchema")!=RebirthWorldCharacterRecord.CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture))return "migration ledger source/target mismatch";
        string policy=A(doc.Root,"migrationPolicyId");
        if(policy.IndexOf(RebirthSurvivorSkillMigrationPolicy.PolicyId,StringComparison.Ordinal)<0||policy.IndexOf("world-character-v5-to-v6-skill-knowledge-background-seed-v1",StringComparison.Ordinal)<0)return "migration ledger policy chain mismatch: "+policy;
        return string.Empty;
    }

    private static int CurrentSkillCount() { return RebirthSurvivorSkillMigrationPolicy.GetCurrentSkillIds().Length; }
    private static int CountSkills(XElement node) { return node==null?0:node.Elements("skill").Count(); }
    private static int CountSkillKnowledge(XElement node) { return node==null?0:node.Elements("skill").Count(); }
    private static float SkillKnowledgeValue(XDocument doc,string owner,string id)
    {
        XElement parent=doc.Root.Element(owner); XElement section=parent!=null?parent.Element("skillKnowledge"):null;
        XElement node=section!=null?section.Elements("skill").FirstOrDefault(e=>string.Equals(A(e,"id"),id,StringComparison.OrdinalIgnoreCase)):null;
        return node==null?float.NaN:ReadFloat(node,"value");
    }
    private static float Value(XDocument doc,string owner,string id)
    {
        XElement node=doc.Root.Element(owner).Element("skills").Elements("skill").FirstOrDefault(e=>string.Equals(A(e,"id"),id,StringComparison.OrdinalIgnoreCase));
        return node==null?float.NaN:ReadFloat(node,"value");
    }
    private static float Progress(XDocument doc,string id)
    {
        XElement node=doc.Root.Element("progression").Element("skills").Elements("skill").FirstOrDefault(e=>string.Equals(A(e,"id"),id,StringComparison.OrdinalIgnoreCase));
        return node==null?float.NaN:ReadFloat(node,"progress");
    }
    /// <summary>Old-schema fixtures predate sections that later migration steps require; add them empty so each vector exercises the step it is about.</summary>
    private static bool TryMigrateFixture(XDocument doc,out bool changed,out string error)
    {
        XElement root=doc!=null?doc.Root:null;
        int schema=0;if(root!=null)int.TryParse(A(root,"schemaVersion"),out schema);
        XElement progression=root!=null?root.Element("progression"):null;
        if(progression!=null&&schema>=1&&progression.Element("knowledge")==null&&schema<8)progression.Add(new XElement("knowledge"));
        if(root!=null&&schema>=5)foreach(XElement section in new[]{root.Element("origin"),progression})
        {
            if(section==null)continue;
            if(section.Element("skills")==null)section.Add(new XElement("skills"));
            if(section.Element("skillKnowledge")==null)section.Add(new XElement("skillKnowledge"));
        }
        return RebirthWorldCharacterMigrationRegistry.TryMigrateToCurrent(doc,out changed,out error);
    }

    private static string A(XElement e,string name){XAttribute a=e!=null?e.Attribute(name):null;return a!=null?a.Value:string.Empty;}
    private static float ReadFloat(XElement e,string name){float v;return float.TryParse(A(e,name),NumberStyles.Float,CultureInfo.InvariantCulture,out v)?v:float.NaN;}
    private static string F(float v){return v.ToString("0.######",CultureInfo.InvariantCulture);}
}
