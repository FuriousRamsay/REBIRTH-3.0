using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

#nullable disable

public sealed class RebirthTheoryInsightDefinition
{
    public string Id=string.Empty;
    public string SkillId=string.Empty;
    public float Amount;
    public string TriggerFamily=string.Empty;
    public string NameKey=string.Empty;
}

/// <summary>
/// Phase-6 Theory authority for finite Insights and server-authoritative NPC/specialist instruction.
/// Internal SkillKnowledge persistence remains unchanged for save/network compatibility; all player-facing
/// terminology is Theory. Specific recipes/procedures/schematics remain separate binary Knowledge.
/// </summary>
public static class RebirthTheoryProgressionService
{
    public const string InsightMarkerPrefix="theory.insight.";
    private static readonly Dictionary<string,RebirthTheoryInsightDefinition> Insights=
        new Dictionary<string,RebirthTheoryInsightDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> InstructionSubjects=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<TheoryBand> Bands=new List<TheoryBand>();
    private sealed class TheoryBand { public float Min,Max; public string Status=string.Empty; }

    public static float MinimumInstructorTheory { get; private set; }=20f;
    public static float MinimumGap { get; private set; }=5f;
    public static float MaximumInstructionTransfer { get; private set; }=3f;
    public static double InstructionCooldownSeconds { get; private set; }=1800.0;
    public static int InsightCount { get { return Insights.Count; } }
    public static int InstructionSubjectCount { get { return InstructionSubjects.Count; } }

    public static string Load()
    {
        Insights.Clear(); InstructionSubjects.Clear(); Bands.Clear();
        string root=RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
        LoadInsights(Path.Combine(root,"theory_insights.xml"));
        LoadInstruction(Path.Combine(root,"theory_instruction.xml"));
        ValidateCoverage();
        RebirthTheorySoloRegistry.Load(Path.Combine(root,"theory_solo.xml"),InstructionSubjects);
        RebirthTheorySoloService.Install();
        RebirthTheorySpecialistRegistry.Load(Path.Combine(root,"theory_specialists.xml"),
            id => RebirthNpcProductionProfileCatalogue.TryGet(id,out var profile) && profile != null
                && (profile.Capabilities & RebirthNpcCapabilities.Dialogue) != 0,
            id => InstructionSubjects.Contains(id), MinimumInstructorTheory);
        return "theoryInsights="+Insights.Count.ToString(CultureInfo.InvariantCulture)+" instructionSubjects="+InstructionSubjects.Count.ToString(CultureInfo.InvariantCulture)+" specialistProfiles="+RebirthTheorySpecialistRegistry.ProfileCount.ToString(CultureInfo.InvariantCulture);
    }

    public static bool IsInternalInsightMarker(string id)
    { return !string.IsNullOrEmpty(id)&&id.StartsWith(InsightMarkerPrefix,StringComparison.OrdinalIgnoreCase); }

    public static bool TryGetInsight(string id,out RebirthTheoryInsightDefinition definition)
    { return Insights.TryGetValue(id??string.Empty,out definition)&&definition!=null; }

    public static RebirthTheoryInsightDefinition[] GetInsightsForSkill(string skillId)
    {
        List<RebirthTheoryInsightDefinition> list=new List<RebirthTheoryInsightDefinition>();
        foreach(RebirthTheoryInsightDefinition d in Insights.Values)
            if(d!=null&&string.Equals(d.SkillId,skillId,StringComparison.OrdinalIgnoreCase))list.Add(d);
        list.Sort(delegate(RebirthTheoryInsightDefinition a,RebirthTheoryInsightDefinition b){return string.Compare(a.Id,b.Id,StringComparison.OrdinalIgnoreCase);});
        return list.ToArray();
    }

    public static RebirthTheoryInsightDefinition[] GetEarnedInsights(EntityPlayer player)
    {
        List<RebirthTheoryInsightDefinition> list=new List<RebirthTheoryInsightDefinition>();
        RebirthWorldCharacterRecord record;if(player==null||!RebirthWorldCharacterService.TryGet(player,out record)||record==null||record.Progression==null)return list.ToArray();
        foreach(RebirthTheoryInsightDefinition d in Insights.Values)
        {
            if(d==null)continue;string marker=InsightMarkerPrefix+d.Id;if(record.Progression.KnowledgeIds.Contains(marker))list.Add(d);
        }
        list.Sort(delegate(RebirthTheoryInsightDefinition a,RebirthTheoryInsightDefinition b){return string.Compare(a.Id,b.Id,StringComparison.OrdinalIgnoreCase);});
        return list.ToArray();
    }

    public static string GetTheoryStatus(float value)
    {
        if(float.IsNaN(value)||float.IsInfinity(value))return "Uneducated";
        float v=Math.Max(0f,Math.Min(100f,value));
        string status="Uneducated";
        float lower=float.NegativeInfinity;
        // Authored integer maxima describe whole-number labels; fractional Theory remains
        // in the lower band until the next authored minimum, rather than falling through.
        for(int i=0;i<Bands.Count;i++)
            if(v>=Bands[i].Min&&Bands[i].Min>lower){lower=Bands[i].Min;status=Bands[i].Status;}
        return status;
    }

    /// <summary>Finite one-time Insight award. Repeating the same stable Insight ID gives zero.</summary>
    public static bool TryAwardInsight(EntityPlayer player,string insightId,string sourceKey,out float applied,out bool alreadyEarned)
    {
        applied=0f;alreadyEarned=false;
        RebirthTheoryInsightDefinition d;
        if(!TryGetInsight(insightId,out d)||d==null)return false;
        string marker=InsightMarkerPrefix+d.Id;
        return RebirthSkillKnowledgeService.TryAwardOneTimeTheory(player,d.SkillId,d.Amount,marker,
            "insight:"+(sourceKey??d.TriggerFamily),out applied,out alreadyEarned);
    }

    /// <summary>
    /// Server-side NPC/specialist instruction mutation. The authoritative caller supplies the instructor's
    /// authored Theory value and stable instructor key; this service enforces subject allow-list, Theory ceiling,
    /// gap, transfer cap and persistent cooldown. No multiplayer-only state is required.
    /// </summary>
    public static bool TryApplyNpcInstruction(EntityPlayer student,string instructorKey,string skillId,float instructorTheory,float requestedAmount,
        out float applied,out string reason)
    {
        applied=0f;reason=string.Empty;
        if(student==null||string.IsNullOrEmpty(instructorKey)||string.IsNullOrEmpty(skillId)){reason="instruction request is incomplete";return false;}
        if(!InstructionSubjects.Contains(skillId)){reason="subject is not authored for instruction";return false;}
        if(float.IsNaN(instructorTheory)||float.IsInfinity(instructorTheory)||instructorTheory<MinimumInstructorTheory||instructorTheory>100f){reason="instructor Theory is outside the authored range";return false;}
        if(float.IsNaN(requestedAmount)||float.IsInfinity(requestedAmount)||requestedAmount<=0f){reason="instruction amount is invalid";return false;}

        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(student,out identity,out record)||record==null||record.Progression==null){reason="student progression state is unavailable";return false;}
        RebirthSkillKnowledgeRuntimeState state;
        if(!record.Progression.SkillKnowledge.TryGetValue(skillId,out state)||state==null){reason="student Theory state is unavailable";return false;}

        string historyKey="npc|"+instructorKey+"|"+skillId;
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            RebirthTeachingHistoryRuntimeState history;
            record.Progression.TeachingHistory.TryGetValue(historyKey,out history);
            double cooldownRemaining;
            if(!RebirthTheoryInstructionPolicy.TryCalculate(state.Value,instructorTheory,requestedAmount,
                RebirthSkillKnowledgeService.GetMinimum(),RebirthSkillKnowledgeService.GetMaximum(),
                MinimumInstructorTheory,MinimumGap,MaximumInstructionTransfer,InstructionCooldownSeconds,
                history==null?0L:history.LastCompletedUtcTicks,DateTime.UtcNow.Ticks,
                out applied,out cooldownRemaining,out reason))return false;
            float before=Math.Max(RebirthSkillKnowledgeService.GetMinimum(),Math.Min(RebirthSkillKnowledgeService.GetMaximum(),state.Value));
            float after=before+applied;
            state.Value=after;
            if(history==null){history=new RebirthTeachingHistoryRuntimeState{Key=historyKey,StudentStorageKey=identity.StorageKey,SkillId=skillId};record.Progression.TeachingHistory[historyKey]=history;}
            history.LastCompletedUtcTicks=DateTime.UtcNow.Ticks;
            history.CompletionCount=Math.Max(0,history.CompletionCount)+1;
            record.Touch("npc-theory-instruction:"+skillId);
        }
        // Preserve the exact Theory/history mutation for save-gated retry; never replay the lesson.
        RebirthSkillAwardService.QueueOwnerPublication(student);
        if(!RebirthWorldCharacterRepository.SaveIfDirty(identity,"npc-theory-instruction:"+skillId)){reason="instruction could not be persisted";return false;}
        reason="Theory increased by "+applied.ToString("0.##",CultureInfo.InvariantCulture)+".";
        return true;
    }

    /// <summary>Read-only preview from a live authored specialist; grants no lesson or permission.</summary>
    public static double SpecialistInstructionCooldownSeconds=>InstructionCooldownSeconds;
    public static bool TryPreviewNpcInstruction(EntityPlayer student,int instructorEntityId,string skillId,
        out float transfer,out double cooldownRemaining,out string reason)
    {
        transfer=0f;cooldownRemaining=0d;reason=string.Empty;
        string instructorKey;float instructorTheory;
        if(!RebirthTheorySpecialistResolver.TryResolve(student,instructorEntityId,skillId,out instructorKey,out instructorTheory))
        {reason="no live specialist is available for this subject";return false;}
        RebirthStablePlayerIdentity identity;RebirthWorldCharacterRecord record;
        if(!RebirthSkillAwardService.TryGetEligible(student,out identity,out record)||record?.Progression==null)
        {reason="student progression state is unavailable";return false;}
        lock(RebirthSkillKnowledgeService.SyncRoot)
        {
            RebirthSkillKnowledgeRuntimeState state;
            if(!record.Progression.SkillKnowledge.TryGetValue(skillId,out state)||state==null)
            {reason="student Theory state is unavailable";return false;}
            RebirthTeachingHistoryRuntimeState history;
            record.Progression.TeachingHistory.TryGetValue("npc|"+instructorKey+"|"+skillId,out history);
            return RebirthTheoryInstructionPolicy.TryCalculate(state.Value,instructorTheory,MaximumInstructionTransfer,
                RebirthSkillKnowledgeService.GetMinimum(),RebirthSkillKnowledgeService.GetMaximum(),
                MinimumInstructorTheory,MinimumGap,MaximumInstructionTransfer,InstructionCooldownSeconds,
                history==null?0L:history.LastCompletedUtcTicks,DateTime.UtcNow.Ticks,
                out transfer,out cooldownRemaining,out reason);
        }
    }
    public static string RunVectors()
    {
        int pass=0,total=0;System.Text.StringBuilder b=new System.Text.StringBuilder("[REBIRTH Theory Vectors]");
        Check(b,ref pass,ref total,"48 instruction subjects",InstructionSubjects.Count==48);
        Check(b,ref pass,ref total,"144 finite insights",Insights.Count==144);
        bool perSkill=true;RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle==null||bundle.Progression==null)perSkill=false;else for(int i=0;i<bundle.Progression.Skills.Count;i++)if(GetInsightsForSkill(bundle.Progression.Skills[i].Id).Length<3)perSkill=false;
        Check(b,ref pass,ref total,"all Skills have >=3 insights",perSkill);
        Check(b,ref pass,ref total,"Theory status 0",GetTheoryStatus(0f)=="Uneducated");
        Check(b,ref pass,ref total,"Theory status 50",GetTheoryStatus(50f)=="Knowledgeable");
        Check(b,ref pass,ref total,"Theory status 100",GetTheoryStatus(100f)=="Mastered");
        Check(b,ref pass,ref total,"instruction mirrors current min Theory",Math.Abs(MinimumInstructorTheory-RebirthTeachingService.MinimumInstructorKnowledge)<.001f);
        Check(b,ref pass,ref total,"instruction mirrors current min gap",Math.Abs(MinimumGap-RebirthTeachingService.MinimumKnowledgeGap)<.001f);
        Check(b,ref pass,ref total,"instruction cap bounded",MaximumInstructionTransfer>0f&&MaximumInstructionTransfer<=3f);
        b.Append("\n  summary=").Append(pass==total?"PASS":"FAIL").Append(" pass=").Append(pass).Append(" fail=").Append(total-pass).Append(" total=").Append(total);
        return b.ToString();
    }

    private static void LoadInsights(string path)
    {
        XDocument doc=XDocument.Load(path);XElement root=doc.Root;
        if(root==null||root.Name!="survivor_theory_insights"||(string)root.Attribute("schema_version")!="1")throw new InvalidDataException("theory_insights.xml requires survivor_theory_insights schema_version=1");
        XElement status=root.Element("status_bands");if(status==null)throw new InvalidDataException("theory_insights.xml missing status_bands");
        foreach(XElement e in status.Elements("band"))
        {
            float min=Finite(e,"min",0f,100f),max=Finite(e,"max",0f,100f);string name=Req(e,"status");if(max<min)throw new InvalidDataException("Theory status band max below min");Bands.Add(new TheoryBand{Min=min,Max=max,Status=name});
        }
        if(Bands.Count!=9)throw new InvalidDataException("theory_insights.xml must declare the locked nine Theory status bands");
        XElement entries=root.Element("insights");if(entries==null)throw new InvalidDataException("theory_insights.xml missing insights");
        foreach(XElement e in entries.Elements("insight"))
        {
            RebirthTheoryInsightDefinition d=new RebirthTheoryInsightDefinition{Id=Req(e,"id"),SkillId=Req(e,"skill_id"),Amount=Finite(e,"amount",0.01f,10f),TriggerFamily=Req(e,"trigger_family"),NameKey=Req(e,"name_key")};
            RebirthSkillDefinition skill;if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(d.SkillId,out skill)||skill==null)throw new InvalidDataException("Theory Insight references unknown Skill: "+d.Id+" -> "+d.SkillId);
            if(Insights.ContainsKey(d.Id))throw new InvalidDataException("Duplicate Theory Insight: "+d.Id);Insights.Add(d.Id,d);
        }
    }

    private static void LoadInstruction(string path)
    {
        XDocument doc=XDocument.Load(path);XElement root=doc.Root;
        if(root==null||root.Name!="survivor_theory_instruction"||(string)root.Attribute("schema_version")!="1")throw new InvalidDataException("theory_instruction.xml requires survivor_theory_instruction schema_version=1");
        XElement policy=root.Element("policy");if(policy==null)throw new InvalidDataException("theory_instruction.xml missing policy");
        MinimumInstructorTheory=Finite(policy,"minimum_instructor_theory",0f,100f);MinimumGap=Finite(policy,"minimum_gap",0.01f,100f);MaximumInstructionTransfer=Finite(policy,"max_transfer",0.01f,100f);InstructionCooldownSeconds=Finite(policy,"cooldown_seconds",0f,86400f);
        XElement subjects=root.Element("subjects");if(subjects==null)throw new InvalidDataException("theory_instruction.xml missing subjects");
        foreach(XElement e in subjects.Elements("subject"))
        {
            string skillId=Req(e,"skill_id");bool enabled=Bool(e,"enabled",true);RebirthSkillDefinition skill;if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(skillId,out skill)||skill==null)throw new InvalidDataException("Theory instruction references unknown Skill: "+skillId);
            if(enabled&&!InstructionSubjects.Add(skillId))throw new InvalidDataException("Duplicate Theory instruction subject: "+skillId);
        }
    }

    private static void ValidateCoverage()
    {
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;if(bundle==null||bundle.Progression==null)throw new InvalidDataException("Survivor definitions unavailable while validating Theory progression");
        for(int i=0;i<bundle.Progression.Skills.Count;i++)
        {
            string id=bundle.Progression.Skills[i].Id;if(GetInsightsForSkill(id).Length<3)throw new InvalidDataException("Theory Insight catalog has fewer than three authored Insights for "+id);
            if(!InstructionSubjects.Contains(id))throw new InvalidDataException("Theory instruction is missing subject "+id);
        }
        if(InstructionSubjects.Count!=bundle.Progression.Skills.Count)throw new InvalidDataException("Theory instruction subject count does not match Skill registry");
        if(Math.Abs(MinimumInstructorTheory-RebirthTeachingService.MinimumInstructorKnowledge)>.001f||Math.Abs(MinimumGap-RebirthTeachingService.MinimumKnowledgeGap)>.001f||Math.Abs(InstructionCooldownSeconds-RebirthTeachingService.PairSubjectCooldownSeconds)>.001)
            throw new InvalidDataException("NPC instruction policy must mirror the current validated Teaching thresholds/cooldown");
    }

    private static string Req(XElement e,string name){string v=((string)e.Attribute(name)??string.Empty).Trim();if(v.Length==0)throw new InvalidDataException("Missing '"+name+"' on <"+e.Name+">");return v;}
    private static float Finite(XElement e,string name,float min,float max){float v;if(!float.TryParse(Req(e,name),NumberStyles.Float,CultureInfo.InvariantCulture,out v)||float.IsNaN(v)||float.IsInfinity(v)||v<min||v>max)throw new InvalidDataException("Invalid '"+name+"' on <"+e.Name+">");return v;}
    private static bool Bool(XElement e,string name,bool fallback){bool v;return bool.TryParse((string)e.Attribute(name),out v)?v:fallback;}
    private static void Check(System.Text.StringBuilder b,ref int pass,ref int total,string name,bool ok){total++;if(ok)pass++;b.Append("\n  ").Append(ok?"PASS ":"FAIL ").Append(name);}
}
