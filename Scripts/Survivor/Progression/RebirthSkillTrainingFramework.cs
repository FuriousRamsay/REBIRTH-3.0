using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

#nullable disable

public enum RebirthSkillTrainingEvidenceMode
{
    ContinuousWork = 0,
    DiscreteAward = 1
}

/// <summary>
/// Server-side evidence envelope for the unified practical-Skill training pipeline.
/// Route-specific systems remain responsible for proving the real successful outcome and,
/// where needed, converting it to live work-rate or locked discrete raw-award inputs.
/// </summary>
public sealed class RebirthSkillTrainingEvidence
{
    public string SkillId = string.Empty;
    public string SourceKey = string.Empty;
    public string RepetitionKey = string.Empty;
    public string DurableReceiptId = string.Empty;
    public string ReferenceDescription = string.Empty;
    public bool AuthoritativeSuccess;
    public RebirthSkillTrainingEvidenceMode Mode;
    public float CreditedWork;
    public float LiveWorkRate;
    public float DiscreteRawAward;
    public float TaskDifficulty = -1f;
    public float RelevanceMultiplier = 1f;
    public float RepetitionMultiplier = 1f;
    public float AssistanceMultiplier = 1f;
    public float MinimumInterval;
}

public sealed class RebirthSkillTrainingProfile
{
    public string SkillId = string.Empty;
    public string Family = string.Empty;
    public string ReferenceUnit = string.Empty;
    public string Formula = string.Empty;
    public string Coefficients = string.Empty;
    public string SourceBasis = string.Empty;
    public string State = string.Empty;
}

public sealed class RebirthSkillTrainingComputation
{
    public string SkillId = string.Empty;
    public string SourceKey = string.Empty;
    public string Family = string.Empty;
    public string ReferenceUnit = string.Empty;
    public string Formula = string.Empty;
    public float PracticalSkill;
    public float CreditedWork;
    public float LiveWorkRate;
    public float EquivalentSeconds;
    public float SkillBandMultiplier = 1f;
    public float NormalizedRawGain;
    public float RelevanceMultiplier = 1f;
    public float RepetitionMultiplier = 1f;
    public float AssistanceMultiplier = 1f;
    public float RawBeforeLearningModifiers;
    public float ExpectedFinalGain;
    public string UiString = string.Empty;
}

/// <summary>Locked Phase-0C profile registry. It is read-only at runtime.</summary>
public static class RebirthSkillTrainingProfileRegistry
{
    private sealed class Band
    {
        public float Min;
        public float Max;
        public float Multiplier;
    }

    private static readonly object Gate = new object();
    private static readonly Dictionary<string,RebirthSkillTrainingProfile> Profiles = new Dictionary<string,RebirthSkillTrainingProfile>(StringComparer.OrdinalIgnoreCase);
    private static readonly List<Band> Bands = new List<Band>();
    private static bool ready;
    private static string sourcePath = string.Empty;

    public static float ContinuousRatePerSecond { get; private set; }
    public static float MaxContinuousEquivalentSeconds { get; private set; }
    public static float MaxDiscreteAward { get; private set; }
    public static float TheoryLearningMultiplier { get; private set; }
    public static bool LiveAwardsFlag { get; private set; }
    public static int ProfileCount { get { lock(Gate) return Profiles.Count; } }
    public static string SourcePath { get { return sourcePath; } }

    public static string Load()
    {
        lock(Gate)
        {
            Profiles.Clear(); Bands.Clear(); ready=false;
            string root=RebirthSurvivorDefinitionLoader.ResolveConfigRoot();
            sourcePath=Path.Combine(root,"skill_training.xml");
            XDocument doc=XDocument.Load(sourcePath); XElement x=doc.Root;
            if(x==null||x.Name!="survivor_skill_training"||(string)x.Attribute("schema_version")!="1") throw new InvalidDataException("skill_training.xml requires survivor_skill_training schema_version=1");
            LiveAwardsFlag=Bool(x,"live_awards",false);
            TheoryLearningMultiplier=Finite(x,"theory_learning_multiplier",0f,10f);
            XElement common=x.Element("common"); if(common==null)throw new InvalidDataException("skill_training.xml missing <common>.");
            ContinuousRatePerSecond=Finite(common,"continuous_rate_per_second",0.000001f,1f);
            MaxContinuousEquivalentSeconds=Finite(common,"max_continuous_equivalent_seconds",0.000001f,3600f);
            MaxDiscreteAward=Finite(common,"max_discrete_award",0.000001f,1f);
            foreach(XElement b in common.Elements("skill_band"))
            {
                Band band=new Band{Min=Finite(b,"min",-50f,100f),Max=Finite(b,"max",-50f,100f),Multiplier=Finite(b,"multiplier",0f,10f)};
                if(band.Max<band.Min)throw new InvalidDataException("skill_training.xml Skill band max is below min.");
                Bands.Add(band);
            }
            if(Bands.Count!=5)throw new InvalidDataException("skill_training.xml requires exactly five Skill bands.");
            Bands.Sort(delegate(Band a,Band b){return a.Min.CompareTo(b.Min);});
            XElement ps=x.Element("profiles"); if(ps==null)throw new InvalidDataException("skill_training.xml missing <profiles>.");
            foreach(XElement p in ps.Elements("profile"))
            {
                RebirthSkillTrainingProfile profile=new RebirthSkillTrainingProfile
                {
                    SkillId=Req(p,"skill_id"), Family=Req(p,"family"), ReferenceUnit=Req(p,"reference_unit"), Formula=Req(p,"formula"),
                    Coefficients=Req(p,"coefficients"), SourceBasis=(string)p.Attribute("source_basis")??string.Empty, State=Req(p,"state")
                };
                if(!string.Equals(profile.State,"LOCKED",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Training profile '"+profile.SkillId+"' is not LOCKED.");
                if(Profiles.ContainsKey(profile.SkillId))throw new InvalidDataException("Duplicate training profile '"+profile.SkillId+"'.");
                Profiles.Add(profile.SkillId,profile);
            }
            RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
            if(bundle==null||bundle.Progression==null)throw new InvalidDataException("Survivor definitions unavailable while loading skill_training.xml.");
            for(int i=0;i<bundle.Progression.Skills.Count;i++)
            {
                RebirthSkillDefinition skill=bundle.Progression.Skills[i]; if(skill!=null&&!Profiles.ContainsKey(skill.Id))throw new InvalidDataException("Missing locked training profile for '"+skill.Id+"'.");
            }
            if(Profiles.Count!=bundle.Progression.Skills.Count)throw new InvalidDataException("Training profile count does not match current Skill registry.");
            ready=true;
            return "trainingProfiles="+Profiles.Count.ToString(CultureInfo.InvariantCulture)+" rate="+ContinuousRatePerSecond.ToString("0.###",CultureInfo.InvariantCulture)+"/s cap="+MaxDiscreteAward.ToString("0.###",CultureInfo.InvariantCulture);
        }
    }

    public static bool TryGet(string skillId,out RebirthSkillTrainingProfile profile)
    {
        Ensure(); lock(Gate) return Profiles.TryGetValue(skillId??string.Empty,out profile)&&profile!=null;
    }

    public static float GetSkillBandMultiplier(float practicalSkill)
    {
        Ensure(); lock(Gate)
        {
            for(int i=0;i<Bands.Count;i++) if(practicalSkill>=Bands[i].Min&&practicalSkill<=Bands[i].Max)return Bands[i].Multiplier;
            if(Bands.Count==0)return 1f;
            return practicalSkill<Bands[0].Min?Bands[0].Multiplier:Bands[Bands.Count-1].Multiplier;
        }
    }

    public static RebirthSkillTrainingProfile[] Snapshot()
    {
        Ensure(); lock(Gate)
        {
            List<RebirthSkillTrainingProfile> values=new List<RebirthSkillTrainingProfile>(Profiles.Values);
            values.Sort(delegate(RebirthSkillTrainingProfile a,RebirthSkillTrainingProfile b){return string.Compare(a.SkillId,b.SkillId,StringComparison.OrdinalIgnoreCase);});
            return values.ToArray();
        }
    }

    private static void Ensure(){if(!ready)Load();}
    private static string Req(XElement e,string name){string v=((string)e.Attribute(name)??string.Empty).Trim();if(v.Length==0)throw new InvalidDataException("Missing '"+name+"' on <"+e.Name+">.");return v;}
    private static bool Bool(XElement e,string name,bool fallback){bool v;return bool.TryParse((string)e.Attribute(name),out v)?v:fallback;}
    private static float Finite(XElement e,string name,float min,float max){float v;if(!float.TryParse(Req(e,name),NumberStyles.Float,CultureInfo.InvariantCulture,out v)||float.IsNaN(v)||float.IsInfinity(v)||v<min||v>max)throw new InvalidDataException("Invalid '"+name+"' on <"+e.Name+">.");return v;}
}

/// <summary>
/// Shared non-mutating calculation layer. Legitimate learning modifiers are intentionally applied
/// later by RebirthSkillAwardService, so zero credited work can never be manufactured into progress.
/// </summary>
public static class RebirthSkillTrainingCalculator
{
    public static bool TryCalculate(RebirthSkillTrainingEvidence evidence,float practicalSkill,out RebirthSkillTrainingComputation result)
    {
        result=null;
        if(evidence==null||!evidence.AuthoritativeSuccess||string.IsNullOrEmpty(evidence.SkillId)||!Finite(practicalSkill))return false;
        if(!UnitMultiplier(evidence.RelevanceMultiplier)||!UnitMultiplier(evidence.RepetitionMultiplier)||!UnitMultiplier(evidence.AssistanceMultiplier))return false;
        if(!Finite(evidence.CreditedWork)||evidence.CreditedWork<=0f)return false;
        RebirthSkillTrainingProfile profile; if(!RebirthSkillTrainingProfileRegistry.TryGet(evidence.SkillId,out profile)||profile==null)return false;
        float normalized=0f,equivalent=0f,band=1f;
        if(evidence.Mode==RebirthSkillTrainingEvidenceMode.ContinuousWork)
        {
            if(!Finite(evidence.LiveWorkRate)||evidence.LiveWorkRate<=0f)return false;
            equivalent=Math.Min(RebirthSkillTrainingProfileRegistry.MaxContinuousEquivalentSeconds,evidence.CreditedWork/evidence.LiveWorkRate);
            if(!Finite(equivalent)||equivalent<=0f)return false;
            band=RebirthSkillTrainingProfileRegistry.GetSkillBandMultiplier(practicalSkill);
            normalized=equivalent*RebirthSkillTrainingProfileRegistry.ContinuousRatePerSecond*band;
        }
        else
        {
            if(!Finite(evidence.DiscreteRawAward)||evidence.DiscreteRawAward<=0f)return false;
            normalized=Math.Min(RebirthSkillTrainingProfileRegistry.MaxDiscreteAward,evidence.DiscreteRawAward);
        }
        if(!Finite(normalized)||normalized<=0f)return false;
        float raw=normalized*evidence.RelevanceMultiplier*evidence.RepetitionMultiplier*evidence.AssistanceMultiplier;
        if(!Finite(raw)||raw<=0f)return false;
        result=new RebirthSkillTrainingComputation
        {
            SkillId=evidence.SkillId,SourceKey=evidence.SourceKey??string.Empty,Family=profile.Family,ReferenceUnit=profile.ReferenceUnit,Formula=profile.Formula,
            PracticalSkill=practicalSkill,CreditedWork=evidence.CreditedWork,LiveWorkRate=evidence.LiveWorkRate,EquivalentSeconds=equivalent,SkillBandMultiplier=band,
            NormalizedRawGain=normalized,RelevanceMultiplier=evidence.RelevanceMultiplier,RepetitionMultiplier=evidence.RepetitionMultiplier,
            AssistanceMultiplier=evidence.AssistanceMultiplier,RawBeforeLearningModifiers=raw
        };
        return true;
    }

    private static bool UnitMultiplier(float v){return Finite(v)&&v>=0f&&v<=1f;}
    private static bool Finite(float v){return !float.IsNaN(v)&&!float.IsInfinity(v);}
}

/// <summary>Read-only projection/debug bridge. Award and preview paths consume the same calculated raw value.</summary>
public static class RebirthSkillTrainingProjectionService
{
    public static bool TryPreview(EntityPlayer player,RebirthSkillTrainingEvidence evidence,out RebirthSkillTrainingComputation result)
    {
        result=null; if(player==null||evidence==null)return false;
        float practical,effective,attribute;
        if(!RebirthSkillOutcomeValueService.TryGetPracticalAndEffective(player,evidence.SkillId,out practical,out effective,out attribute))return false;
        if(!RebirthSkillTrainingCalculator.TryCalculate(evidence,practical,out result)||result==null)return false;
        float current,expected;
        if(!RebirthSkillAwardService.TryPreview(player,evidence.SkillId,result.RawBeforeLearningModifiers,out current,out expected))return false;
        result.ExpectedFinalGain=expected;
        result.UiString="+"+expected.ToString("0.###",CultureInfo.InvariantCulture)+" "+evidence.SkillId+" expected";
        return true;
    }

    public static string BuildDebugProjection(EntityPlayer player,RebirthSkillTrainingEvidence evidence)
    {
        RebirthSkillTrainingComputation c;
        if(!TryPreview(player,evidence,out c)||c==null)return "[REBIRTH Training] projection unavailable/rejected";
        StringBuilder b=new StringBuilder("[REBIRTH Training Projection]");
        b.Append(" skill=").Append(c.SkillId).Append(" family=").Append(c.Family).Append(" source=").Append(c.SourceKey);
        b.Append("\n  creditedWork=").Append(c.CreditedWork.ToString("0.###",CultureInfo.InvariantCulture));
        if(evidence.Mode==RebirthSkillTrainingEvidenceMode.ContinuousWork)b.Append(" liveWorkRate=").Append(c.LiveWorkRate.ToString("0.###",CultureInfo.InvariantCulture)).Append(" eqSeconds=").Append(c.EquivalentSeconds.ToString("0.###",CultureInfo.InvariantCulture)).Append(" band=").Append(c.SkillBandMultiplier.ToString("0.###",CultureInfo.InvariantCulture));
        else b.Append(" discreteRaw=").Append(evidence.DiscreteRawAward.ToString("0.###",CultureInfo.InvariantCulture));
        b.Append("\n  relevance=").Append(c.RelevanceMultiplier.ToString("0.###",CultureInfo.InvariantCulture)).Append(" repetition=").Append(c.RepetitionMultiplier.ToString("0.###",CultureInfo.InvariantCulture)).Append(" assistance=").Append(c.AssistanceMultiplier.ToString("0.###",CultureInfo.InvariantCulture));
        b.Append("\n  normalizedRaw=").Append(c.NormalizedRawGain.ToString("0.#####",CultureInfo.InvariantCulture)).Append(" preLearning=").Append(c.RawBeforeLearningModifiers.ToString("0.#####",CultureInfo.InvariantCulture)).Append(" expectedFinal=").Append(c.ExpectedFinalGain.ToString("0.#####",CultureInfo.InvariantCulture));
        b.Append("\n  reference=").Append(c.ReferenceUnit).Append(" formula=").Append(c.Formula).Append(" ui='").Append(c.UiString).Append("'");
        return b.ToString();
    }
}

public static class RebirthSkillTrainingVectorHarness
{
    public static string RunAll()
    {
        try{RebirthSkillTrainingProfileRegistry.Load();}catch(Exception ex){return "[REBIRTH Training Vectors] FAIL load="+ex.GetType().Name+": "+ex.Message;}
        int pass=0,total=0;StringBuilder b=new StringBuilder("[REBIRTH Training Vectors]");
        Check(b,ref pass,ref total,"profile count 48",RebirthSkillTrainingProfileRegistry.ProfileCount==48);
        RebirthSkillTrainingComputation c;
        RebirthSkillTrainingEvidence zero=Continuous("skill.mining",0f,50f);Check(b,ref pass,ref total,"zero evidence rejects",!RebirthSkillTrainingCalculator.TryCalculate(zero,0f,out c));
        RebirthSkillTrainingEvidence normal=Continuous("skill.mining",100f,50f);bool normalOk=RebirthSkillTrainingCalculator.TryCalculate(normal,0f,out c);Check(b,ref pass,ref total,"continuous 2s normalizes",normalOk&&Near(c.EquivalentSeconds,2f)&&Near(c.RawBeforeLearningModifiers,.01f));
        RebirthSkillTrainingEvidence capped=Continuous("skill.mining",1000f,50f);bool cappedOk=RebirthSkillTrainingCalculator.TryCalculate(capped,0f,out c);Check(b,ref pass,ref total,"continuous cap 6s",cappedOk&&Near(c.EquivalentSeconds,6f)&&Near(c.RawBeforeLearningModifiers,.03f));
        bool bandOk=RebirthSkillTrainingCalculator.TryCalculate(normal,50f,out c);Check(b,ref pass,ref total,"Skill band 50 uses .60",bandOk&&Near(c.SkillBandMultiplier,.60f)&&Near(c.RawBeforeLearningModifiers,.006f));
        RebirthSkillTrainingEvidence discrete=Discrete("skill.lockpicking",1f,.90f);bool discreteOk=RebirthSkillTrainingCalculator.TryCalculate(discrete,10f,out c);Check(b,ref pass,ref total,"discrete cap .60",discreteOk&&Near(c.NormalizedRawGain,.60f));
        discrete.RelevanceMultiplier=0f;Check(b,ref pass,ref total,"zero relevance awards zero",!RebirthSkillTrainingCalculator.TryCalculate(discrete,10f,out c));
        discrete=Discrete("skill.lockpicking",1f,.45f);discrete.RelevanceMultiplier=.5f;discrete.RepetitionMultiplier=.5f;discrete.AssistanceMultiplier=.8f;bool factorsOk=RebirthSkillTrainingCalculator.TryCalculate(discrete,10f,out c);Check(b,ref pass,ref total,"ordered pre-learning factors",factorsOk&&Near(c.RawBeforeLearningModifiers,.45f*.5f*.5f*.8f));
        RebirthSkillTrainingProfile[] ps=RebirthSkillTrainingProfileRegistry.Snapshot();bool complete=ps.Length==48;for(int i=0;i<ps.Length;i++)complete&=ps[i]!=null&&ps[i].SkillId.Length>0&&ps[i].Family.Length>0&&ps[i].ReferenceUnit.Length>0&&ps[i].Formula.Length>0&&string.Equals(ps[i].State,"LOCKED",StringComparison.OrdinalIgnoreCase);Check(b,ref pass,ref total,"all profiles complete/locked",complete);
        b.Append("\n  summary=").Append(pass==total?"PASS":"FAIL").Append(" pass=").Append(pass).Append(" fail=").Append(total-pass).Append(" total=").Append(total);
        return b.ToString();
    }
    private static RebirthSkillTrainingEvidence Continuous(string skill,float work,float rate){return new RebirthSkillTrainingEvidence{SkillId=skill,SourceKey="vector",AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.ContinuousWork,CreditedWork=work,LiveWorkRate=rate};}
    private static RebirthSkillTrainingEvidence Discrete(string skill,float work,float raw){return new RebirthSkillTrainingEvidence{SkillId=skill,SourceKey="vector",AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=work,DiscreteRawAward=raw};}
    private static void Check(StringBuilder b,ref int pass,ref int total,string name,bool ok){total++;if(ok)pass++;b.Append("\n  ").Append(ok?"PASS ":"FAIL ").Append(name);}
    private static bool Near(float a,float b){return Math.Abs(a-b)<=0.00001f;}
}
