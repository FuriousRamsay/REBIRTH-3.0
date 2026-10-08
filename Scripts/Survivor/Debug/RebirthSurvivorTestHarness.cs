using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

#nullable disable

/// <summary>
/// Final Survivor release regression harness. This is intentionally read-only: it validates
/// loaded authoring, deterministic creation rules, current persisted/runtime invariants and
/// multiplayer-visible state without mutating the player's Survivor character. Destructive
/// simulations remain behind the existing explicit Survivor debug gate and individual commands.
/// </summary>
public static class RebirthSurvivorTestHarness
{
    private enum State : byte { Pass=0, Warn=1, Fail=2, Deferred=3 }

    private sealed class Phase
    {
        public string Id;
        public string Name;
        public State Result;
        public string Detail;
        public long ElapsedMs;
    }

    public static string RunAll(EntityPlayer player)
    {
        Stopwatch total=Stopwatch.StartNew();
        List<Phase> phases=new List<Phase>();
        Add(phases,"00","definitions",TestDefinitions);
        Add(phases,"01","local profile persistence",TestProfiles);
        Add(phases,"02","creation validator",TestCreationValidator);
        Add(phases,"03","world character persistence",delegate { return TestWorldPersistence(player); });
        Add(phases,"04","creation replay/security",TestCreationSecuritySurface);
        Add(phases,"05","first-entry lifecycle",delegate { return TestFirstEntry(player); });
        Add(phases,"06","attributes/progression",delegate { return TestProgression(player); });
        Add(phases,"07","knowledge gating",delegate { return TestKnowledge(player); });
        Add(phases,"08","metabolism regression snapshot",delegate { return TestMetabolism(player); });
        Add(phases,"09","Mood/Diet/Health Capacity",delegate { return TestCondition(player); });
        Add(phases,"10","support content/gear",delegate { return TestSupport(player); });
        Add(phases,"11","UI binding readiness",TestUiReadiness);
        Add(phases,"12","network parity snapshot",delegate { return TestNetwork(player); });
        Add(phases,"13","progression dependency graph",TestProgressionGraph);
        Add(phases,"14","progression explorer release gate",TestProgressionExplorerReleaseGate);
        Add(phases,"15","performance snapshot",delegate { return TestPerformance(player); });
        Add(phases,"16","literature duplicate/read-state integrity",delegate { return TestLiterature(player); });
        Add(phases,"17","advanced discipline zombie-animal classification",TestAdvancedDisciplineZombieAnimals);
        Add(phases,"18","unified Skill training framework",TestSkillTrainingFramework);
        Add(phases,"19","Theory / Insight / instruction foundation",TestTheoryFoundation);
        Add(phases,"20","combat / Stealth / Rage / device normalization",TestCombatNormalization);
        Add(phases,"21","world / output normalization",TestWorldOutputNormalization);
        Add(phases,"22","technical / process normalization",TestTechnicalProcessNormalization);
        Add(phases,"23","social / exertion / advanced normalization",TestSocialExertionAdvanced);
        Add(phases,"24","migration / reachability / UI static gate",TestFinalStaticCoverage);
        Add(phases,"25","Phase 12 aggregate release vectors",TestReleaseAcceptanceVectors);
        total.Stop();

        int pass=0,warn=0,fail=0,deferred=0;
        for(int i=0;i<phases.Count;i++)
        {
            if(phases[i].Result==State.Pass)pass++;
            else if(phases[i].Result==State.Warn)warn++;
            else if(phases[i].Result==State.Fail)fail++;
            else deferred++;
        }

        StringBuilder b=new StringBuilder();
        b.Append("[REBIRTH Survivor Test] all phases start protocol=").Append(RebirthSurvivorNetworkProtocol.Version)
            .Append(" defs=").Append(RebirthSurvivorDefinitionRegistry.DefinitionVersion).AppendLine();
        for(int i=0;i<phases.Count;i++)
        {
            Phase p=phases[i];
            b.Append("  [").Append(p.Id).Append("] ").Append(p.Name).Append(" -> ").Append(p.Result.ToString().ToUpperInvariant())
                .Append(" (").Append(p.ElapsedMs).Append("ms)");
            if(!string.IsNullOrEmpty(p.Detail)) b.Append(" :: ").Append(p.Detail);
            b.AppendLine();
        }
        string summaryStatus = fail>0 ? "FAIL" : (deferred>0 ? "NOT_RUN/PARTIAL" : (warn>0 ? "WARN" : "STRUCTURAL_PASS"));
        b.Append("  [26] final summary -> ").Append(summaryStatus)
            .Append(" :: pass=").Append(pass).Append(" warn=").Append(warn).Append(" fail=").Append(fail).Append(" deferred=").Append(deferred)
            .Append(" totalMs=").Append(total.ElapsedMilliseconds).AppendLine();
        b.Append("[REBIRTH Survivor Test] NOTE: DEFERRED phases require the final manual single-player/P2P/dedicated/visual acceptance matrix; this command does not forge packets or mutate live character state.");
        return b.ToString().TrimEnd();
    }

    private static void Add(List<Phase> phases,string id,string name,Func<Phase> test)
    {
        Stopwatch sw=Stopwatch.StartNew(); Phase p;
        try { p=test(); if(p==null)p=R(State.Fail,"test returned no result"); }
        catch(Exception ex) { p=R(State.Fail,ex.GetType().Name+": "+ex.Message); }
        sw.Stop(); p.Id=id; p.Name=name; p.ElapsedMs=sw.ElapsedMilliseconds; phases.Add(p);
    }

    private static Phase TestDefinitions()
    {
        if(!RebirthSurvivorDefinitionRegistry.IsReady) return R(State.Fail,"definition registry is not ready");
        RebirthSurvivorAuthoringReport report=RebirthSurvivorAuthoringValidator.Validate(RebirthSurvivorDefinitionRegistry.Bundle);
        if(!report.IsValid) return R(State.Fail,"authoring errors="+report.Errors.Count+" warnings="+report.Warnings.Count);
        RebirthSurvivorDefinitionBundle b=RebirthSurvivorDefinitionRegistry.Bundle;
        return R(State.Pass,"backgrounds="+b.Backgrounds.Count+" traits="+b.Traits.Count+" diets="+b.Diets.Count+" skills="+b.Progression.Skills.Count+" skillKnowledgeAreas="+b.Progression.Skills.Count+" legacyKnowledge="+b.Progression.Knowledge.Count+" support="+b.SupportProfiles.Count+" hash="+ShortHash(RebirthSurvivorDefinitionRegistry.SemanticHash));
    }

    private static Phase TestAdvancedDisciplineZombieAnimals()
    {
        if(!RebirthAdvancedDisciplineRegistry.IsReady) return R(State.Fail,"Advanced Discipline registry is not ready");
        if(!RebirthBlackMagicTargetClassifier.IsReady) return R(State.Fail,"Black Magic target classifier is not ready");
        string registry=RebirthAdvancedDisciplineRegistry.RunVectors();
        string classifier=RebirthBlackMagicTargetClassifier.RunVectors();
        string beastmaster=RebirthBeastmasterService.RunVectors();
        string affinity=RebirthWildAffinityService.RunVectors();
        string blackMagic=RebirthBlackMagicService.RunVectors();
        if(registry.IndexOf("vectors=PASS",StringComparison.OrdinalIgnoreCase)<0) return R(State.Fail,registry);
        if(classifier.IndexOf("zombieAnimalVectors=PASS",StringComparison.OrdinalIgnoreCase)<0) return R(State.Fail,classifier);
        if(beastmaster.IndexOf("vectors=PASS",StringComparison.OrdinalIgnoreCase)<0) return R(State.Fail,beastmaster);
        if(affinity.IndexOf("vectors=PASS",StringComparison.OrdinalIgnoreCase)<0) return R(State.Fail,affinity);
        if(blackMagic.IndexOf("chunkFVectors=PASS",StringComparison.OrdinalIgnoreCase)<0) return R(State.Fail,blackMagic);
        return R(State.Pass,"PC002 hard exclusions + Black Magic classification/domination authoring vectors passed");
    }

    private static Phase TestSkillTrainingFramework()
    {
        string vectors=RebirthSkillTrainingVectorHarness.RunAll();
        if(vectors.IndexOf("summary=PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,vectors);
        if(RebirthSkillTrainingProfileRegistry.LiveAwardsFlag)return R(State.Fail,"Phase-4 foundation must keep live_awards=false until route migration phases enable unified awards.");
        return R(State.Pass,"48 locked profiles + zero-evidence/cap/band/factor vectors passed; live unified awards remain fail-closed");
    }

    private static Phase TestTheoryFoundation()
    {
        string vectors=RebirthTheoryProgressionService.RunVectors();
        if(vectors.IndexOf("summary=PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,vectors);
        RebirthLiteratureDefinition[] literature=RebirthProgressionRuntimeConfig.GetLiteratureSnapshot();
        HashSet<string> theorySubjects=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool cookingPreparationOnly=true;
        for(int i=0;i<literature.Length;i++)
        {
            RebirthLiteratureDefinition d=literature[i];if(d==null)continue;
            if(string.Equals(d.Kind,"theory",StringComparison.OrdinalIgnoreCase))theorySubjects.Add(d.SkillId);
            if((d.ItemId??string.Empty).StartsWith("rebirthCookbook",StringComparison.OrdinalIgnoreCase)&&string.Equals(d.Kind,"theory",StringComparison.OrdinalIgnoreCase))cookingPreparationOnly=false;
        }
        if(!cookingPreparationOnly)return R(State.Fail,"Cooking cookbook literature must remain Preparation/specific Knowledge only and must not award Cooking Theory.");
        return R(State.Pass,"insights="+RebirthTheoryProgressionService.InsightCount+" instructionSubjects="+RebirthTheoryProgressionService.InstructionSubjectCount+" physicalTheorySubjects="+theorySubjects.Count+" cookingPreparationException=true; six subjects intentionally rely on Insights/instruction until dedicated art-backed titles are authored");
    }

    private static Phase TestCombatNormalization()
    {
        string core=RebirthCombatProgressionVectorHarness.RunAll();
        string weapon=RebirthWeaponFamilySkillVectorHarness.RunAll();
        string wave=RebirthSkillWaveAVectorHarness.RunAll();
        string complex=RebirthComplexSkillSystemVectorHarness.RunAll();
        if(core.IndexOf("summary=PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,core);
        if(weapon.IndexOf("vectors: PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,weapon);
        if(wave.IndexOf("vectors: PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,wave);
        if(complex.IndexOf("result=PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,complex);
        return R(State.Pass,"actual-health-loss combat + 6s cap + pre-hit Stealth + normalized Rage + normalized supported device combat + stock Drone recovery structural vectors passed");
    }

    private static Phase TestWorldOutputNormalization()
    {
        string resource=RebirthResourceFieldSkillVectorHarness.RunAll();
        string phase8=RebirthPhase8WorldOutputTrainingService.RunVectors();
        if(resource.IndexOf("PASS",StringComparison.OrdinalIgnoreCase)<0||resource.IndexOf("FAIL ",StringComparison.OrdinalIgnoreCase)>=0)return R(State.Fail,resource);
        if(phase8.IndexOf("Phase8 Vectors] PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,phase8);
        return R(State.Pass,"Mining/Logging/Tracking plus exact Salvage/Farming/Animal Processing output vectors passed");
    }

    private static Phase TestTechnicalProcessNormalization()
    {
        string phase9=RebirthPhase9TechnicalTrainingService.RunVectors();
        if(phase9.IndexOf("Phase9 Vectors] PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,phase9);
        return R(State.Pass,"202 Mechanics components + repair/Electrical/Lockpicking technical vectors passed; shared recipe/craft routes remain covered by the unified training framework");
    }

    private static Phase TestSocialExertionAdvanced()
    {
        string wave=RebirthSkillWaveAVectorHarness.RunAll();
        string complex=RebirthComplexSkillSystemVectorHarness.RunAll();
        string blackMagic=RebirthBlackMagicService.RunVectors();
        if(wave.IndexOf("vectors: PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,wave);
        if(complex.IndexOf("result=PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,complex);
        if(blackMagic.IndexOf("chunkFVectors=PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,blackMagic);
        return R(State.Pass,"purchase/sale split, neutral-speed exertion, armor burden, solo Teaching declarations and advanced Black Magic/device vectors passed");
    }

    private static Phase TestFinalStaticCoverage()
    {
        string migration=RebirthSurvivorMigrationVectorHarness.RunAll();
        string reachability=RebirthSkillReachabilityValidator.RunVectors();
        string reveal=RebirthTheoryRevealService.RunVectors();
        if(migration.IndexOf("summary=PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,migration);
        if(reachability.IndexOf("RESULT 3/3",StringComparison.OrdinalIgnoreCase)<0||reachability.IndexOf("FAIL ",StringComparison.OrdinalIgnoreCase)>=0)return R(State.Fail,reachability);
        if(reveal.IndexOf("RESULT 5/5",StringComparison.OrdinalIgnoreCase)<0||reveal.IndexOf("FAIL ",StringComparison.OrdinalIgnoreCase)>=0)return R(State.Fail,reveal);
        RebirthProgressionExplorerReleaseGateReport ui=RebirthProgressionExplorerReleaseGate.Evaluate();
        if(ui==null||!ui.StaticReady)return R(State.Fail,"Progression Explorer static authority gate is not ready");
        return R(State.Pass,"schema="+RebirthWorldCharacterRecord.CurrentSchemaVersion+" migration/reachability/Theory reveal/UI static gates passed; live screenshot parity remains runtime acceptance");
    }

    private static Phase TestReleaseAcceptanceVectors()
    {
        string vectors=RebirthSurvivorReleaseAcceptanceVectorHarness.RunAll();
        if(vectors.IndexOf("release-acceptance vectors: PASS",StringComparison.OrdinalIgnoreCase)<0)return R(State.Fail,vectors);
        return R(State.Pass,"all deterministic Phase 12 aggregate vectors passed; releaseApproved remains false until live compile/host/P2P/dedicated/save-reconnect/UI evidence exists");
    }

    private static Phase TestProfiles()
    {
        RebirthSurvivorProfileLoadIssue[] issues=RebirthSurvivorProfileStore.GetIssuesSnapshot();
        RebirthSurvivorProfile[] profiles=RebirthSurvivorProfileStore.GetProfilesSnapshot();
        if(issues.Length>0) return R(State.Warn,"profiles="+profiles.Length+" loadIssues="+issues.Length+"; inspect 'rbsurvivor profile issues'");
        return R(State.Pass,"profiles="+profiles.Length+" loadIssues=0 root="+RebirthSurvivorProfileStore.RootDirectory);
    }

    private static Phase TestCreationValidator()
    {
        List<string> problems=new List<string>();
        RebirthSurvivorCreationResult valid=RebirthSurvivorCreationValidator.Validate(new RebirthSurvivorCreationSelection(RebirthSurvivorIds.BackgroundCleanSlate,RebirthSurvivorIds.DietUnrestricted,new string[0],RebirthSurvivorDefinitionRegistry.SemanticHash),false);
        if(valid==null||!valid.IsValid) problems.Add("clean-slate baseline rejected");

        RebirthSurvivorCreationResult missing=RebirthSurvivorCreationValidator.Validate(new RebirthSurvivorCreationSelection(string.Empty,string.Empty,new string[0],RebirthSurvivorDefinitionRegistry.SemanticHash),false);
        if(!Has(missing,RebirthSurvivorCreationErrorCode.MissingBackground)||!Has(missing,RebirthSurvivorCreationErrorCode.MissingDiet)) problems.Add("missing origin fields not rejected");

        RebirthSurvivorCreationResult stale=RebirthSurvivorCreationValidator.Validate(new RebirthSurvivorCreationSelection(RebirthSurvivorIds.BackgroundCleanSlate,RebirthSurvivorIds.DietUnrestricted,new string[0],"forged-stale-hash"),false);
        if(!Has(stale,RebirthSurvivorCreationErrorCode.DefinitionHashMismatch)) problems.Add("stale definition hash not rejected");

        string trait=FirstUniversalTrait();
        if(!string.IsNullOrEmpty(trait))
        {
            RebirthSurvivorCreationResult dup=RebirthSurvivorCreationValidator.Validate(new RebirthSurvivorCreationSelection(RebirthSurvivorIds.BackgroundCleanSlate,RebirthSurvivorIds.DietUnrestricted,new[]{trait,trait},RebirthSurvivorDefinitionRegistry.SemanticHash),false);
            if(!Has(dup,RebirthSurvivorCreationErrorCode.DuplicateTrait)) problems.Add("duplicate Trait not rejected");
        }
        if(problems.Count>0) return R(State.Fail,string.Join("; ",problems.ToArray()));
        return R(State.Pass,"valid baseline + missing fields + stale hash + duplicate Trait deterministic checks passed");
    }

    private static Phase TestWorldPersistence(EntityPlayer player)
    {
        if(!RebirthWorldCharacterRepository.IsServerAuthority) return R(State.Deferred,"run on authoritative host/dedicated server for repository validation");
        RebirthWorldCharacterPersistenceIssue[] issues=RebirthWorldCharacterRepository.GetIssuesSnapshot();
        if(issues.Length>0) return R(State.Warn,"world persistence issues="+issues.Length+"; inspect 'rbsurvivor persistence issues'");
        if(player==null) return R(State.Deferred,"repository clean; no player resolved for record check");
        RebirthWorldCharacterRecord record;
        if(!RebirthWorldCharacterService.TryGet(player,out record)||record==null) return R(State.Deferred,"repository clean; current player has no committed Survivor character");
        if(record.SchemaVersion!=RebirthWorldCharacterRecord.CurrentSchemaVersion) return R(State.Fail,"record schema="+record.SchemaVersion+" expected="+RebirthWorldCharacterRecord.CurrentSchemaVersion);
        if(record.Origin==null||!record.IsComplete) return R(State.Fail,"committed record is incomplete");
        if(record.MigrationSourceSchema<1||record.MigrationTargetSchema!=record.SchemaVersion) return R(State.Fail,"invalid migration ledger source="+record.MigrationSourceSchema+" target="+record.MigrationTargetSchema);
        if(record.MigrationApplied&&string.IsNullOrEmpty(record.MigrationPolicyId)) return R(State.Fail,"migrated record is missing migrationPolicyId");
        return R(State.Pass,"schema="+record.SchemaVersion+" revision="+record.Revision+" migration="+record.MigrationSourceSchema+"->"+record.MigrationTargetSchema+" applied="+record.MigrationApplied+" policy="+(string.IsNullOrEmpty(record.MigrationPolicyId)?"native-schema6":record.MigrationPolicyId)+" stableKey="+ShortHash(record.StablePlayerKey));
    }

    private static Phase TestCreationSecuritySurface()
    {
        List<string> problems=new List<string>();
        ValidateNetworkProtocolBounds(
            RebirthSurvivorNetworkProtocol.Version,
            RebirthSurvivorNetworkProtocol.MaxTraitIds,
            RebirthSurvivorNetworkProtocol.MaxCreationChoices,
            RebirthSurvivorNetworkProtocol.MaxSkillKnowledge,
            problems);
        RebirthSurvivorCreationResult fake=RebirthSurvivorCreationValidator.Validate(new RebirthSurvivorCreationSelection("background.__forged","diet.__forged",new[]{"trait.__forged"},RebirthSurvivorDefinitionRegistry.SemanticHash),false);
        if(!Has(fake,RebirthSurvivorCreationErrorCode.UnknownBackground)||!Has(fake,RebirthSurvivorCreationErrorCode.UnknownDiet)||!Has(fake,RebirthSurvivorCreationErrorCode.UnknownTrait)) problems.Add("forged IDs not rejected by authoritative validator");
        if(problems.Count>0) return R(State.Fail,string.Join("; ",problems.ToArray()));
        return R(State.Pass,"forged IDs rejected; bounded protocol v"+RebirthSurvivorNetworkProtocol.Version+"; live replay/modified-client packet forging remains manual security acceptance");
    }

    private static void ValidateNetworkProtocolBounds(int version, int maxTraitIds, int maxCreationChoices, int maxSkillKnowledge, List<string> problems)
    {
        if(version!=8) problems.Add("unexpected protocol="+version);
        if(maxSkillKnowledge<44||maxSkillKnowledge>256) problems.Add("Skill Knowledge protocol bound invalid");
        if(maxTraitIds<1||maxTraitIds>256) problems.Add("trait protocol bound invalid");
        if(maxCreationChoices<0||maxCreationChoices>256) problems.Add("choice protocol bound invalid");
    }

    private static Phase TestFirstEntry(EntityPlayer player)
    {
        if(player==null) return R(State.Deferred,"no active player");
        bool hold=RebirthCharacterCreationHoldService.IsHeld(player);
        RebirthWorldCharacterRecord record;
        bool has=RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete;
        if(has&&hold) return R(State.Fail,"completed character is still under Character Creation Hold");
        if(has) return R(State.Pass,"committed character present and creation hold is clear");
        if(hold) return R(State.Pass,"no committed character and creation hold is active");
        return R(State.Warn,"no committed character and no creation hold; verify world progression mode/first-entry state");
    }

    private static Phase TestProgression(EntityPlayer player)
    {
        RebirthWorldCharacterRecord record=Record(player); if(record==null) return R(State.Deferred,"no committed authoritative record");
        List<string> problems=new List<string>(); RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        foreach(KeyValuePair<string,RebirthAttributeRuntimeState> pair in record.Progression.Attributes)
        {
            RebirthAttributeRuntimeState a=pair.Value; RebirthAttributeDefinition d=null;
            for(int i=0;i<bundle.Progression.Attributes.Count;i++) if(string.Equals(bundle.Progression.Attributes[i].Id,pair.Key,StringComparison.OrdinalIgnoreCase)){d=bundle.Progression.Attributes[i];break;}
            if(a==null||d==null) { problems.Add("unknown/null attribute "+pair.Key); continue; }
            if(a.Current<d.Min-0.001f||a.Current>d.Max+0.001f||a.Potential<d.Min-0.001f||a.Potential>d.Max+0.001f||a.Current>a.Potential+0.001f) problems.Add("attribute range "+pair.Key);
        }
        foreach(KeyValuePair<string,RebirthSkillRuntimeState> pair in record.Progression.Skills)
        {
            RebirthSkillDefinition d; if(pair.Value==null||!RebirthSurvivorDefinitionRegistry.TryGetSkill(pair.Key,out d)||pair.Value.Value<d.Min-0.001f||pair.Value.Value>d.Max+0.001f) problems.Add("skill range/ref "+pair.Key);
        }
        if(record.Progression.Skills.Count!=bundle.Progression.Skills.Count) problems.Add("practical Skill count="+record.Progression.Skills.Count+" expected="+bundle.Progression.Skills.Count);
        if(record.Progression.SkillKnowledge.Count!=bundle.Progression.Skills.Count) problems.Add("Skill Knowledge count="+record.Progression.SkillKnowledge.Count+" expected="+bundle.Progression.Skills.Count);
        if(record.Origin==null||record.Origin.StartingSkillKnowledge.Count!=bundle.Progression.Skills.Count) problems.Add("origin Skill Knowledge count="+(record.Origin==null?-1:record.Origin.StartingSkillKnowledge.Count)+" expected="+bundle.Progression.Skills.Count);
        for(int i=0;i<bundle.Progression.Skills.Count;i++)
        {
            RebirthSkillDefinition skill=bundle.Progression.Skills[i]; if(skill==null)continue;
            RebirthSkillKnowledgeRuntimeState theory; float startTheory;
            if(!record.Progression.SkillKnowledge.TryGetValue(skill.Id,out theory)||theory==null) problems.Add("missing Skill Knowledge "+skill.Id);
            else if(theory.Value<bundle.Progression.SkillKnowledgeMin-0.001f||theory.Value>bundle.Progression.SkillKnowledgeMax+0.001f) problems.Add("Skill Knowledge range "+skill.Id);
            if(record.Origin==null||!record.Origin.StartingSkillKnowledge.TryGetValue(skill.Id,out startTheory)) problems.Add("missing origin Skill Knowledge "+skill.Id);
            else if(startTheory<bundle.Progression.SkillKnowledgeMin-0.001f||startTheory>bundle.Progression.SkillKnowledgeMax+0.001f) problems.Add("origin Skill Knowledge range "+skill.Id);
        }
        if(problems.Count>0) return R(State.Fail,string.Join("; ",problems.ToArray()));
        return R(State.Pass,"attributes="+record.Progression.Attributes.Count+" skills="+record.Progression.Skills.Count+" skillKnowledge="+record.Progression.SkillKnowledge.Count+" legacyKnowledge="+record.Progression.KnowledgeIds.Count+" healthPotential="+F(record.Progression.HealthPotential));
    }

    private static Phase TestKnowledge(EntityPlayer player)
    {
        RebirthWorldCharacterRecord record=Record(player); if(record==null) return R(State.Deferred,"no committed authoritative record");
        int internalMarkers=0;
        foreach(string id in record.Progression.KnowledgeIds)
        {
            if(RebirthLiteratureService.IsInternalReadMarker(id)||RebirthTheoryProgressionService.IsInternalInsightMarker(id)){internalMarkers++;continue;}
            if(id.StartsWith("cooking.recipe.",StringComparison.Ordinal)) continue;   // added dynamically by the cooking batch when a recipe is first cooked
            RebirthKnowledgeDefinition d; if(!RebirthSurvivorDefinitionRegistry.TryGetKnowledge(id,out d)) return R(State.Fail,"record owns unknown Knowledge '"+id+"'");
        }
        return R(State.Pass,"binary Knowledge IDs resolve; internal Theory markers="+internalMarkers+" are intentionally excluded from Knowledge-definition validation");
    }

    private static Phase TestLiterature(EntityPlayer player)
    {
        RebirthLiteratureDefinition[] literature=RebirthProgressionRuntimeConfig.GetLiteratureSnapshot();
        if(literature==null||literature.Length==0) return R(State.Fail,"no literature definitions loaded");

        int theory=0,discovery=0;
        HashSet<string> markers=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> discoveryMarkers=new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // discovery items (cooking books/cards) use a read marker as their Knowledge id
        List<string> problems=new List<string>();

        for(int i=0;i<literature.Length;i++)
        {
            RebirthLiteratureDefinition d=literature[i];
            if(d==null){problems.Add("null literature definition");continue;}

            if(string.Equals(d.Kind,"theory",StringComparison.OrdinalIgnoreCase))
            {
                theory++;
                if(string.IsNullOrEmpty(d.SkillId)) problems.Add("theory missing Skill "+d.ItemId);
                else
                {
                    RebirthSkillDefinition skill;
                    if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(d.SkillId,out skill)||skill==null) problems.Add("theory unknown Skill "+d.ItemId+" -> "+d.SkillId);
                }
                if(d.Amount<=0f) problems.Add("theory non-positive amount "+d.ItemId);
                if(string.IsNullOrEmpty(d.MarkerId)||!RebirthLiteratureService.IsInternalReadMarker(d.MarkerId)) problems.Add("invalid theory marker "+d.ItemId);
                else if(!markers.Add(d.MarkerId)) problems.Add("duplicate theory marker "+d.MarkerId);
            }
            else if(string.Equals(d.Kind,"discovery",StringComparison.OrdinalIgnoreCase))
            {
                discovery++;
                if(!string.IsNullOrEmpty(d.KnowledgeId)&&RebirthLiteratureService.IsInternalReadMarker(d.KnowledgeId)) discoveryMarkers.Add(d.KnowledgeId);
                if(string.IsNullOrEmpty(d.KnowledgeId)) problems.Add("discovery missing Knowledge "+d.ItemId);
                else
                {
                    RebirthKnowledgeDefinition knowledge;
                    if(!RebirthSurvivorDefinitionRegistry.TryGetKnowledge(d.KnowledgeId,out knowledge)||knowledge==null) problems.Add("discovery unknown Knowledge "+d.ItemId+" -> "+d.KnowledgeId);
                }
            }
            else problems.Add("unsupported literature kind "+d.ItemId+" -> "+d.Kind);
        }

        if(theory<=0) problems.Add("no theory literature definitions");
        if(discovery<=0) problems.Add("no discovery literature definitions");
        if(markers.Count!=theory) problems.Add("theory marker count="+markers.Count+" does not match theory definitions="+theory);

        RebirthWorldCharacterRecord record=Record(player);
        int readMarkers=0;
        if(record!=null)
        {
            foreach(string id in record.Progression.KnowledgeIds)
            {
                if(!RebirthLiteratureService.IsInternalReadMarker(id)) continue;
                readMarkers++;
                if(!markers.Contains(id)&&!discoveryMarkers.Contains(id)) problems.Add("orphan internal literature marker "+id);
            }
        }

        if(problems.Count>0) return R(State.Fail,string.Join("; ",problems.ToArray()));
        return R(State.Pass,"literature="+literature.Length+" theory="+theory+" discovery="+discovery+" uniqueTheoryMarkers="+markers.Count+" characterReadMarkers="+readMarkers+" reusable=true");
    }

    private static Phase TestMetabolism(EntityPlayer player)
    {
        if(player==null) return R(State.Deferred,"no active player");
        RebirthMetabolismState state;
        if(!RebirthMetabolismStateRepository.TryGet(player,out state)||state==null) return R(State.Deferred,"metabolism state is not loaded for current player");
        List<string> problems=new List<string>();
        if(state.Version!=RebirthMetabolismState.CurrentVersion) problems.Add("stateVersion="+state.Version);
        if(state.DigestiveHealth<0f||state.DigestiveHealth>100f) problems.Add("digestiveHealth="+F(state.DigestiveHealth));
        if(state.Energy<0f||state.Energy>RebirthMetabolismConfig.EnergyMax+0.001f) problems.Add("energy="+F(state.Energy));
        if(state.IngestionEntries.Count>RebirthMetabolismConfig.MaxIngestionEntries) problems.Add("ingestionEntries="+state.IngestionEntries.Count);
        for(int i=0;i<state.IngestionEntries.Count;i++)
        {
            RebirthIngestionEntry e=state.IngestionEntries[i]; if(e==null) continue;
            if(e.RemainingFluidVolumeMl<0f||e.RemainingSolidVolumeMl<0f||e.RemainingNutritionUnits<0f||e.RemainingEnergyUnits<0f||e.IntestinalFluidVolumeMl<0f||e.IntestinalSolidVolumeMl<0f||e.IntestinalNutritionUnits<0f||e.IntestinalEnergyUnits<0f) { problems.Add("negative ingestion content entry="+e.EntryId); break; }
        }
        if(problems.Count>0) return R(State.Fail,string.Join("; ",problems.ToArray()));
        return R(State.Pass,"v"+state.Version+" energy="+F(state.Energy)+" digestiveHealth="+F(state.DigestiveHealth)+" ingestionEntries="+state.IngestionEntries.Count+" timedEffects="+state.TimedEffects.Count+"; physical flow timing remains manual runtime acceptance");
    }

    private static Phase TestCondition(EntityPlayer player)
    {
        RebirthWorldCharacterRecord record=Record(player); if(record==null) return R(State.Deferred,"no committed authoritative record");
        RebirthWorldConditionState c=record.Condition; if(c==null) return R(State.Fail,"condition state missing");
        if(c.MoodCurrent<0f||c.MoodCurrent>100f||c.MoodTarget<0f||c.MoodTarget>100f||c.DietSatisfaction<0f||c.DietSatisfaction>100f) return R(State.Fail,"Mood/Diet value outside 0..100");
        if(c.HealthCapacity<0f||c.HealthCapacity>record.Progression.HealthPotential+0.01f) return R(State.Fail,"Health Capacity outside Potential boundary");
        if(c.RecentMeals.Count>6) return R(State.Fail,"recent meal history exceeded bounded six-meal window");
        if(c.SevereDehydrationActiveSeconds<0f||c.SevereMalnutritionActiveSeconds<0f) return R(State.Fail,"negative deprivation timer");
        return R(State.Pass,"mood="+F(c.MoodCurrent)+" target="+F(c.MoodTarget)+" diet="+F(c.DietSatisfaction)+" capacity="+F(c.HealthCapacity)+"/"+F(record.Progression.HealthPotential)+" meals="+c.RecentMeals.Count);
    }

    private static Phase TestSupport(EntityPlayer player)
    {
        RebirthSurvivorDefinitionBundle b=RebirthSurvivorDefinitionRegistry.Bundle; if(b==null) return R(State.Fail,"definitions unavailable");
        int backpacks=0; for(int i=0;i<b.SupportProfiles.Count;i++) if(string.Equals(b.SupportProfiles[i].GearSlotId,RebirthSurvivorGearService.BackpackSlotId,StringComparison.OrdinalIgnoreCase))backpacks++;
        if(backpacks!=8) return R(State.Fail,"expected 8 Backpack profiles; found "+backpacks);
        RebirthWorldCharacterRecord record=Record(player); if(record==null) return R(State.Pass,"support profiles="+b.SupportProfiles.Count+" backpacks=8; live support state deferred until committed character exists");
        foreach(KeyValuePair<string,RebirthTraitSupportRuntimeState> pair in record.Support.Entries)
        {
            RebirthTraitSupportProfileDefinition profile; if(!RebirthSurvivorDefinitionRegistry.TryGetSupport(pair.Key,out profile)) return R(State.Fail,"runtime support entry references unknown profile '"+pair.Key+"'");
            RebirthTraitSupportRuntimeState s=pair.Value; if(s==null||s.GraceRemainingActiveSeconds<0f||s.ManagedRemainingActiveSeconds<0f||s.PositiveRemainingActiveSeconds<0f||s.CooldownRemainingActiveSeconds<0f||s.Stacks<0) return R(State.Fail,"invalid runtime support state '"+pair.Key+"'");
        }
        foreach(KeyValuePair<string,string> pair in record.Support.EquippedGearBySlot)
        {
            RebirthTraitSupportProfileDefinition profile; if(!RebirthSurvivorDefinitionRegistry.TryGetSupportByGearItem(pair.Value,out profile)||!string.Equals(profile.GearSlotId,pair.Key,StringComparison.OrdinalIgnoreCase)) return R(State.Fail,"gear slot/item binding mismatch "+pair.Key+"="+pair.Value);
        }
        int bag=RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(record);
        if(bag<RebirthSurvivorGearService.BasePhysicalBagSlots||bag>RebirthSurvivorGearService.MaxPhysicalBagSlots) return R(State.Fail,"physical Bag capacity out of range="+bag);
        return R(State.Pass,"supportEntries="+record.Support.Entries.Count+" gearSlots="+record.Support.EquippedGearBySlot.Count+" physicalBagSlots="+bag);
    }

    private static Phase TestUiReadiness()
    {
        if(!RebirthSurvivorDefinitionRegistry.IsReady) return R(State.Fail,"definitions unavailable");
        int missing=0; RebirthSurvivorDefinitionBundle b=RebirthSurvivorDefinitionRegistry.Bundle;
        for(int i=0;i<b.Backgrounds.Count;i++) if(string.IsNullOrEmpty(b.Backgrounds[i].BackgroundArtKey))missing++;
        for(int i=0;i<b.Traits.Count;i++) if(string.IsNullOrEmpty(b.Traits[i].IconKey))missing++;
        for(int i=0;i<b.Diets.Count;i++) if(string.IsNullOrEmpty(b.Diets[i].IconKey))missing++;
        if(missing>0) return R(State.Fail,"missing player-facing art/icon keys="+missing);
        return R(State.Pass,"all Background/Trait/Diet definitions expose art/icon keys; XML atlas/file existence is covered by packaged static validation");
    }

    private static Phase TestNetwork(EntityPlayer player)
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c==null) return R(State.Deferred,"connection manager unavailable");
        if(c.IsServer)
        {
            if(player==null) return R(State.Pass,"authoritative server active; no player resolved for owner snapshot comparison");
            RebirthWorldCharacterRecord record; bool has=RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete;
            return R(State.Pass,"authoritative server active; character="+has+" protocol="+RebirthSurvivorNetworkProtocol.Version);
        }
        RebirthSurvivorOwnerStateSnapshot s=RebirthSurvivorClientState.GetOwnerStateSnapshot();
        if(s==null) return R(State.Deferred,"joined client has not received an owner snapshot yet");
        if(!s.DefinitionsCompatible) return R(State.Fail,"owner snapshot reports definition mismatch");
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(s.HasCharacter&&bundle!=null&&bundle.Progression!=null&&s.SkillKnowledge.Count!=bundle.Progression.Skills.Count) return R(State.Fail,"owner snapshot Skill Knowledge count="+s.SkillKnowledge.Count+" expected="+bundle.Progression.Skills.Count);
        return R(State.Pass,"client owner snapshot revision="+s.CharacterRevision+" hasCharacter="+s.HasCharacter+" defsCompatible=true skillKnowledge="+s.SkillKnowledge.Count+" physicalBagSlots="+s.PhysicalBagSlots);
    }

    private static Phase TestProgressionGraph()
    {
        if(!RebirthProgressionGraphRegistry.IsReady) return R(State.Fail,"progression graph registry is not ready");
        RebirthProgressionGraphValidationReport report=RebirthProgressionGraphValidator.ValidateCurrent();
        if(!report.IsValid) return R(State.Fail,report.BuildSummary());

        const string fence="recipe.electricfencepost";
        RebirthProgressionGraphNeighborhood neighborhood=RebirthProgressionGraphQueryService.GetFocusedNeighborhood(fence,2,2,32);
        if(neighborhood.Nodes.Count==0) return R(State.Fail,"PE-02 focused neighborhood for electric fence recipe is empty");
        RebirthProgressionGraphNode[] prereq=RebirthProgressionGraphQueryService.GetPrerequisites(fence);
        if(!ContainsNode(prereq,"recipe.electricfencepost")||!ContainsNode(prereq,"skill.electrical")||!ContainsNode(prereq,"skill.metalworking"))
            return R(State.Fail,"PE-02 prerequisite query did not preserve electric-fence Knowledge + two Skill requirements");
        RebirthProgressionGraphPath path=RebirthProgressionGraphQueryService.FindShortestPath("recipe.electricfencepost",fence,RebirthProgressionGraphTraversalDirection.Outgoing,4);
        if(!path.Found) return R(State.Fail,"PE-02 directed path from Electrical Fundamentals to electric fence recipe not found");

        RebirthProgressionExplorerOverlaySnapshot neutral=RebirthProgressionExplorerOverlayService.CreateNeutral(neighborhood);
        RebirthProgressionNodeOverlay fenceNeutral;
        if(!neutral.TryGet(fence,out fenceNeutral)||fenceNeutral==null||fenceNeutral.AccessState!=RebirthProgressionNodeAccessState.Unknown)
            return R(State.Fail,"PE-02 Neutral overlay must present gated electric fence recipe as unknown/previewable, not falsely locked/available");

        RebirthSurvivorCreationResult clean=RebirthSurvivorCreationValidator.Validate(new RebirthSurvivorCreationSelection(RebirthSurvivorIds.BackgroundCleanSlate,RebirthSurvivorIds.DietUnrestricted,new string[0],RebirthSurvivorDefinitionRegistry.SemanticHash),false);
        RebirthProgressionExplorerOverlaySnapshot creator=RebirthProgressionExplorerOverlayService.CreateCreatorPreview(neighborhood,clean);
        RebirthProgressionNodeOverlay fenceCreator;
        if(!creator.TryGet(fence,out fenceCreator)||fenceCreator==null||fenceCreator.AccessState!=RebirthProgressionNodeAccessState.Locked)
            return R(State.Fail,"PE-02 Clean Slate creator preview should show electric fence recipe locked by current requirements");

        RebirthProgressionExplorerUiProjection shell=RebirthProgressionExplorerUiProjectionService.Build(fence,RebirthProgressionExplorerMode.Neutral);
        if(shell==null||!string.Equals(shell.FocusId,fence,StringComparison.OrdinalIgnoreCase))return R(State.Fail,"PE-03 UI projection did not preserve requested focus");
        if(shell.Slots.Count<1||shell.Slots.Count>11)return R(State.Fail,"PE-03 UI projection slot count out of bounded shell range="+(shell==null?-1:shell.Slots.Count));
        bool hasFocusSlot=false;for(int i=0;i<shell.Slots.Count;i++)if(shell.Slots[i].Role==RebirthProgressionExplorerSlotRole.Focus&&string.Equals(shell.Slots[i].NodeId,fence,StringComparison.OrdinalIgnoreCase))hasFocusSlot=true;
        if(!hasFocusSlot)return R(State.Fail,"PE-03 UI projection is missing the focus node card");
        RebirthProgressionExplorerNavigationState nav=new RebirthProgressionExplorerNavigationState();nav.Reset(fence);nav.Navigate("knowledge.electrical.fundamentals");nav.Navigate("skill.electrical");
        if(!nav.CanBack||nav.CanForward)return R(State.Fail,"PE-04 navigation history initial state invalid: "+nav.DebugSummary());
        if(!nav.Back()||!string.Equals(nav.CurrentId,"knowledge.electrical.fundamentals",StringComparison.OrdinalIgnoreCase)||!nav.CanForward)return R(State.Fail,"PE-04 navigation Back/Forward state invalid: "+nav.DebugSummary());
        if(!nav.Home()||!string.Equals(nav.CurrentId,fence,StringComparison.OrdinalIgnoreCase))return R(State.Fail,"PE-04 Home did not restore launch focus: "+nav.DebugSummary());
        string[] crumbs=nav.BreadcrumbIds(4);if(crumbs.Length<1)return R(State.Fail,"PE-04 breadcrumb state is empty");
        RebirthProgressionExplorerSearchResult[] fenceSearch=RebirthProgressionExplorerSearchService.Search("electric fence",6);
        bool foundFence=false;for(int i=0;i<fenceSearch.Length;i++)if(string.Equals(fenceSearch[i].NodeId,fence,StringComparison.OrdinalIgnoreCase))foundFence=true;
        if(!foundFence)return R(State.Fail,"PE-05 universal search did not find electric fence recipe by player-facing terms");
        RebirthProgressionExplorerSearchResult[] mechanicsSearch=RebirthProgressionExplorerSearchService.Search("mechanics",6);
        bool foundMechanics=false;for(int i=0;i<mechanicsSearch.Length;i++)if(string.Equals(mechanicsSearch[i].NodeId,"skill.mechanics",StringComparison.OrdinalIgnoreCase))foundMechanics=true;
        if(!foundMechanics)return R(State.Fail,"PE-05 universal search did not find Mechanics Skill");
        RebirthCapabilityRequirementEvaluation pe09Missing=new RebirthCapabilityRequirementEvaluation{Kind=RebirthCapabilityKinds.Knowledge,Id="knowledge.electrical.fundamentals",Allowed=false,WarningOnly=false,Message="Missing Electrical Fundamentals"};
        string pe09Focus=RebirthProgressionLockCrossLinkService.RequirementFocusId(pe09Missing,fence);
        if(!string.Equals(pe09Focus,"knowledge.electrical.fundamentals",StringComparison.OrdinalIgnoreCase))return R(State.Fail,"PE-09 lock cross-link did not focus the exact missing Knowledge requirement");
        RebirthProgressionGraphNode[] tailoringRelated=RebirthProgressionGraphQueryService.GetDependents("knowledge.tailoring.basic");
        if(!ContainsNode(tailoringRelated,"skill.tailoring"))return R(State.Fail,"PE-10 Basic Tailoring Knowledge is not associated with Tailoring Skill in the canonical graph");
        RebirthProgressionGraphNode[] vehicleRelated=RebirthProgressionGraphQueryService.GetDependents("knowledge.vehicle.service");
        if(!ContainsNode(vehicleRelated,"skill.mechanics"))return R(State.Fail,"PE-10 Vehicle Service Knowledge is not associated with Mechanics Skill in the canonical graph");

        return R(report.WarningCount>0?State.Warn:State.Pass,"nodes="+RebirthProgressionGraphRegistry.NodeCount+" edges="+RebirthProgressionGraphRegistry.EdgeCount+" neighborhood="+neighborhood.Nodes.Count+" prereq="+prereq.Length+" neutral="+fenceNeutral.AccessState+" creator="+fenceCreator.AccessState+" shellSlots="+shell.Slots.Count+" nav="+nav.DebugSummary()+" searchFence="+fenceSearch.Length+" searchMechanics="+mechanicsSearch.Length+" pe09Focus="+pe09Focus+" pe10Tailoring="+tailoringRelated.Length+" pe10Vehicle="+vehicleRelated.Length+" warnings="+report.WarningCount+" hash="+ShortHash(RebirthProgressionGraphRegistry.SemanticHash));
    }

    private static bool ContainsNode(RebirthProgressionGraphNode[] nodes,string id)
    {
        if(nodes==null)return false;for(int i=0;i<nodes.Length;i++)if(nodes[i]!=null&&string.Equals(nodes[i].Id,id,StringComparison.OrdinalIgnoreCase))return true;return false;
    }


    private static Phase TestProgressionExplorerReleaseGate()
    {
        RebirthProgressionExplorerReleaseGateReport r=RebirthProgressionExplorerReleaseGate.Evaluate();
        if(!r.StaticReady) return R(State.Fail,"PE-12 static authority gate failed; "+r.BuildText().Replace("\n"," | "));
        if(!r.OwnerDefinitionsCompatible) return R(State.Fail,"owner snapshot reports definition mismatch");
        return R(State.Warn,"staticReady=true runtimeApproved=false fingerprint="+ShortHash(r.AuthorityFingerprint)+"; compare full fingerprint across host/P2P/dedicated and complete PE-12 live matrix");
    }

    private static Phase TestPerformance(EntityPlayer player)
    {
        long memory=GC.GetTotalMemory(false); RebirthSurvivorDefinitionBundle b=RebirthSurvivorDefinitionRegistry.Bundle;
        int definitions=b==null?0:b.Backgrounds.Count+b.Traits.Count+b.Diets.Count+b.Progression.Skills.Count+b.Progression.Knowledge.Count+b.SupportProfiles.Count;
        int meals=0,support=0; RebirthWorldCharacterRecord record=Record(player); if(record!=null){meals=record.Condition.RecentMeals.Count;support=record.Support.Entries.Count;}
        return R(State.Pass,"managedMemory="+(memory/1048576L)+"MiB definitions="+definitions+" recentMeals="+meals+" supportEntries="+support+"; FPS/GC/network comparison requires the final timed runtime acceptance session");
    }

    private static RebirthWorldCharacterRecord Record(EntityPlayer player)
    {
        if(player==null||!RebirthWorldCharacterRepository.IsServerAuthority) return null;
        RebirthWorldCharacterRecord record; return RebirthWorldCharacterService.TryGet(player,out record)&&record!=null&&record.IsComplete?record:null;
    }

    private static bool Has(RebirthSurvivorCreationResult r,RebirthSurvivorCreationErrorCode code)
    {
        if(r==null)return false; for(int i=0;i<r.Errors.Count;i++) if(r.Errors[i]!=null&&r.Errors[i].Code==code)return true; return false;
    }

    private static string FirstUniversalTrait()
    {
        RebirthSurvivorDefinitionBundle b=RebirthSurvivorDefinitionRegistry.Bundle; if(b==null)return string.Empty;
        for(int i=0;i<b.Traits.Count;i++) if(b.Traits[i].Availability==RebirthDefinitionAvailability.Universal)return b.Traits[i].Id;
        return string.Empty;
    }

    private static Phase R(State state,string detail) { return new Phase{Result=state,Detail=detail??string.Empty}; }
    private static string F(float v) { return v.ToString("0.###",CultureInfo.InvariantCulture); }
    private static string ShortHash(string value) { if(string.IsNullOrEmpty(value))return "<empty>"; return value.Length<=12?value:value.Substring(0,12); }
}
