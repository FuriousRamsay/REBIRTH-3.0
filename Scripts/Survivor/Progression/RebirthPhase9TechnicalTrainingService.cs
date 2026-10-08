using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using UnityEngine;

#nullable disable

/// <summary>
/// Phase 9 technical/job/repair Skill training.
/// Owns only routes whose authoritative completion already exists elsewhere; this service converts
/// those real outcomes into the locked Phase-0C normalized training amounts. It never creates a
/// successful outcome on its own and fails closed when exact authored job data cannot be resolved.
/// </summary>
public static class RebirthPhase9TechnicalTrainingService
{
    private sealed class ComponentProfile
    {
        public string ContentId=string.Empty;
        public string SlotId=string.Empty;
        public string Availability=string.Empty;
        public float Complexity;
    }

    private static readonly Dictionary<string,ComponentProfile> Components = new Dictionary<string,ComponentProfile>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<RebirthElectricalServiceKind,float> ElectricalComplexity = new Dictionary<RebirthElectricalServiceKind,float>();
    private static bool ready;

    private static float mechanicsComplexityGain=.15f;
    private static float mechanicsReplaceFactor=.8571429f;
    private static float mechanicsHotwireGain=.60f;
    private static float mechanicsRepairDefaultComplexity=3f;
    private static float maintenanceFullReference=.30f;
    private static float maintenanceDefaultComplexity=1f;
    private static float gunsmithingFullReference=.35f;
    private static float gunsmithingDefaultComplexity=1f;
    private static float tailoringFullReference=.35f;
    private static float tailoringDefaultComplexity=1f;
    private static float electricalComplexityGain=.15f;
    private static float electricalDefaultComplexity=3f;
    private static float lockpickStandardGain=.45f;
    private static float lockpickStandardTime=15f;
    private static float lockpickMinDifficulty=.50f;
    private static float lockpickMaxDifficulty=1.3333333f;

    public static float MaintenanceFullReferenceGain { get { Ensure(); return maintenanceFullReference * maintenanceDefaultComplexity; } }
    public static float GunsmithingFullReferenceGain { get { Ensure(); return gunsmithingFullReference * gunsmithingDefaultComplexity; } }
    public static float TailoringFullReferenceGain { get { Ensure(); return tailoringFullReference * tailoringDefaultComplexity; } }
    public static float ElectricalDefaultReferenceGain { get { Ensure(); return electricalComplexityGain * electricalDefaultComplexity; } }
    public static float LockpickStandardReferenceGain { get { Ensure(); return lockpickStandardGain; } }

    public static string Load()
    {
        Components.Clear(); ElectricalComplexity.Clear(); ready=false;
        string path=Path.Combine(RebirthSurvivorDefinitionLoader.ResolveConfigRoot(),"skill_training.xml");
        XDocument doc=XDocument.Load(path); XElement root=doc.Root;
        XElement p9=root!=null?root.Element("phase9_technical_work"):null;
        if(p9==null)throw new InvalidDataException("skill_training.xml missing <phase9_technical_work>.");
        mechanicsComplexityGain=F(p9,"mechanics_complexity_gain",.15f,0f,1f);
        mechanicsReplaceFactor=F(p9,"mechanics_replace_factor",.8571429f,0f,1f);
        mechanicsHotwireGain=F(p9,"mechanics_hotwire_gain",.60f,0f,1f);
        mechanicsRepairDefaultComplexity=F(p9,"mechanics_repair_default_complexity",3f,.1f,10f);
        maintenanceFullReference=F(p9,"maintenance_full_reference_gain",.30f,0f,1f);
        maintenanceDefaultComplexity=F(p9,"maintenance_default_complexity",1f,.1f,10f);
        gunsmithingFullReference=F(p9,"gunsmithing_full_reference_gain",.35f,0f,1f);
        gunsmithingDefaultComplexity=F(p9,"gunsmithing_default_complexity",1f,.1f,10f);
        tailoringFullReference=F(p9,"tailoring_full_reference_gain",.35f,0f,1f);
        tailoringDefaultComplexity=F(p9,"tailoring_default_complexity",1f,.1f,10f);
        electricalComplexityGain=F(p9,"electrical_complexity_gain",.15f,0f,1f);
        electricalDefaultComplexity=F(p9,"electrical_default_complexity",3f,.1f,10f);
        lockpickStandardGain=F(p9,"lockpick_standard_gain",.45f,0f,1f);
        lockpickStandardTime=F(p9,"lockpick_standard_time",15f,.1f,3600f);
        lockpickMinDifficulty=F(p9,"lockpick_min_difficulty_factor",.5f,.01f,10f);
        lockpickMaxDifficulty=F(p9,"lockpick_max_difficulty_factor",1.3333333f,.01f,10f);
        if(lockpickMaxDifficulty<lockpickMinDifficulty)throw new InvalidDataException("Phase9 lockpick difficulty factor range invalid.");

        XElement components=p9.Element("mechanics_components");
        if(components==null)throw new InvalidDataException("Phase9 mechanics component table missing.");
        foreach(XElement x in components.Elements("component"))
        {
            string content=Req(x,"content_id"), slot=Req(x,"slot_id"), availability=Req(x,"availability");
            float complexity=F(x,"complexity",0f,.1f,10f);
            string key=ComponentKey(content,slot);
            if(Components.ContainsKey(key))throw new InvalidDataException("Duplicate Phase9 mechanics component: "+key);
            Components.Add(key,new ComponentProfile{ContentId=content,SlotId=slot,Availability=availability,Complexity=complexity});
        }
        if(Components.Count!=202)throw new InvalidDataException("Phase9 mechanics table must contain exactly 202 exact rows; got "+Components.Count.ToString(CultureInfo.InvariantCulture)+".");

        XElement jobs=p9.Element("electrical_jobs");
        if(jobs==null)throw new InvalidDataException("Phase9 electrical job table missing.");
        foreach(XElement x in jobs.Elements("job"))
        {
            RebirthElectricalServiceKind kind;
            if(!Enum.TryParse<RebirthElectricalServiceKind>(Req(x,"kind"),true,out kind))throw new InvalidDataException("Unknown Phase9 electrical job kind.");
            if(ElectricalComplexity.ContainsKey(kind))throw new InvalidDataException("Duplicate Phase9 electrical job kind "+kind+".");
            ElectricalComplexity.Add(kind,F(x,"complexity",electricalDefaultComplexity,.1f,10f));
        }
        if(ElectricalComplexity.Count!=3)throw new InvalidDataException("Phase9 requires exactly three electrical service kinds.");
        ready=true;
        return "phase9 mechanicsRows=202 electricalJobs=3 repairFractions=live lockDifficulty=native";
    }

    public static bool AwardMechanics(EntityPlayer player,string action,float actualWorkFraction,string source)
    {
        Ensure(); if(player==null)return false;
        string a=(action??string.Empty).Trim().ToLowerInvariant();
        float raw; string reference;
        if(a=="hotwire")
        {
            if(actualWorkFraction<=0f)return false;
            raw=mechanicsHotwireGain; reference="validated hotwire conversion";
        }
        else if(a=="install"||a=="replace")
        {
            string content,slot;
            if(!TryParseComponentSource(source,out content,out slot))return false;
            ComponentProfile p;
            if(!Components.TryGetValue(ComponentKey(content,slot),out p)||p==null)return false;
            float fraction=Mathf.Clamp01(actualWorkFraction);
            if(fraction<=0f)return false;
            raw=mechanicsComplexityGain*p.Complexity*fraction*(a=="replace"?mechanicsReplaceFactor:1f);
            reference=a+" "+content+"/"+slot+" complexity="+p.Complexity.ToString("0.###",CultureInfo.InvariantCulture);
        }
        else if(a=="repair")
        {
            float fraction=Mathf.Clamp01(actualWorkFraction); if(fraction<=0f)return false;
            raw=mechanicsComplexityGain*mechanicsRepairDefaultComplexity*fraction;
            reference="vehicle condition restored fraction="+fraction.ToString("0.###",CultureInfo.InvariantCulture);
        }
        else return false;
        return AwardDiscrete(player,"skill.mechanics",raw,"phase9:mechanics:"+(source??a),reference,0.10f,1f);
    }

    public static bool AwardEquipmentRepair(EntityPlayer player,string itemName,float restoredUseTimes,float maxUseTimes)
    {
        Ensure(); if(player==null||restoredUseTimes<=0f||maxUseTimes<=0f)return false;
        float fraction=Mathf.Clamp01(restoredUseTimes/maxUseTimes); if(fraction<=0f)return false;
        string skill=RebirthServiceCraftSkillService.ClassifyRepairSkill(player,itemName);
        float reference,complexity;
        if(string.Equals(skill,"skill.gunsmithing",StringComparison.OrdinalIgnoreCase)){reference=gunsmithingFullReference;complexity=gunsmithingDefaultComplexity;}
        else if(string.Equals(skill,"skill.tailoring",StringComparison.OrdinalIgnoreCase)){reference=tailoringFullReference;complexity=tailoringDefaultComplexity;}
        else if(string.Equals(skill,"skill.drone_operations",StringComparison.OrdinalIgnoreCase)){reference=maintenanceFullReference;complexity=maintenanceDefaultComplexity;}
        else {skill="skill.maintenance";reference=maintenanceFullReference;complexity=maintenanceDefaultComplexity;}
        float raw=reference*fraction*complexity;
        return AwardDiscrete(player,skill,raw,"phase9:repair:"+(itemName??string.Empty),"actual condition restored fraction="+fraction.ToString("0.###",CultureInfo.InvariantCulture),.25f,1f);
    }

    public static bool AwardElectrical(EntityPlayer player,RebirthElectricalServiceKind kind,string source)
    {
        Ensure(); if(player==null)return false;
        float complexity;
        if(!ElectricalComplexity.TryGetValue(kind,out complexity))complexity=electricalDefaultComplexity;
        float raw=electricalComplexityGain*complexity;
        return AwardDiscrete(player,"skill.electrical",raw,"phase9:electrical:"+(source??kind.ToString()),"committed "+kind+" service complexity="+complexity.ToString("0.###",CultureInfo.InvariantCulture),2f,1f);
    }

    public static bool AwardLockpickSuccess(EntityPlayer player,float nativeBaseTime,string source)
    {
        Ensure(); if(player==null||nativeBaseTime<=0f)return false;
        // Native LockPickTime is the authored lock difficulty. sqrt compression prevents an extreme
        // custom lock from exceeding the global .60 single-event ceiling while preserving ordering.
        float difficulty=Mathf.Sqrt(Mathf.Max(.01f,nativeBaseTime/lockpickStandardTime));
        difficulty=Mathf.Clamp(difficulty,lockpickMinDifficulty,lockpickMaxDifficulty);
        float raw=lockpickStandardGain*difficulty;
        return AwardDiscrete(player,"skill.lockpicking",raw,"phase9:lockpick:"+(source??string.Empty),"successful lock; native base time="+nativeBaseTime.ToString("0.###",CultureInfo.InvariantCulture),1f,1f);
    }

    public static string RunVectors()
    {
        try{Ensure();}catch(Exception ex){return "[REBIRTH Phase9 Vectors] FAIL load="+ex.GetType().Name+": "+ex.Message;}
        int pass=0,total=0; Action<string,bool> check=(n,ok)=>{total++;if(ok)pass++;};
        check("202 components",Components.Count==202);
        ComponentProfile engine,wheel,battery;
        check("BaseMinibike Engine complexity 4",Components.TryGetValue(ComponentKey("BaseMinibike","Engine"),out engine)&&Near(engine.Complexity,4f));
        check("BaseBicycle Wheel1 complexity 1",Components.TryGetValue(ComponentKey("BaseBicycle","Wheel1"),out wheel)&&Near(wheel.Complexity,1f));
        check("BaseMinibike Battery complexity 1.5",Components.TryGetValue(ComponentKey("BaseMinibike","Battery"),out battery)&&Near(battery.Complexity,1.5f));
        check("engine install=.60",Near(mechanicsComplexityGain*4f,.60f));
        check("engine replace~=.514",Near(mechanicsComplexityGain*4f*mechanicsReplaceFactor,.5142857f));
        check("hotwire=.60",Near(mechanicsHotwireGain,.60f));
        check("maintenance full=.30",Near(maintenanceFullReference,.30f));
        check("gunsmith full=.35",Near(gunsmithingFullReference,.35f));
        check("tailoring full=.35",Near(tailoringFullReference,.35f));
        check("electrical default=.45",Near(electricalComplexityGain*electricalDefaultComplexity,.45f));
        check("lockpick standard=.45",Near(lockpickStandardGain,.45f));
        check("lockpick 26.666s caps .60",Near(lockpickStandardGain*Mathf.Clamp(Mathf.Sqrt(26.666666f/lockpickStandardTime),lockpickMinDifficulty,lockpickMaxDifficulty),.60f));
        return "[REBIRTH Phase9 Vectors] "+(pass==total?"PASS":"FAIL")+" pass="+pass+" fail="+(total-pass)+" total="+total;
    }

    private static bool AwardDiscrete(EntityPlayer player,string skill,float raw,string source,string description,float interval,float assistance)
    {
        if(raw<=0f||float.IsNaN(raw)||float.IsInfinity(raw))return false;
        RebirthSkillTrainingEvidence e=new RebirthSkillTrainingEvidence{SkillId=skill,SourceKey=source??string.Empty,ReferenceDescription=description??string.Empty,
            AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=raw,AssistanceMultiplier=Mathf.Clamp01(assistance),MinimumInterval=Math.Max(0f,interval)};
        RebirthSkillTrainingComputation ignored; float s,a;
        return RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(player,e,out ignored,out s,out a);
    }

    private static bool TryParseComponentSource(string source,out string content,out string slot)
    {
        content=slot=string.Empty; string[] p=(source??string.Empty).Split(':');
        if(p.Length<3)return false;
        if(!string.Equals(p[0],"block",StringComparison.OrdinalIgnoreCase)&&!string.Equals(p[0],"entity",StringComparison.OrdinalIgnoreCase))return false;
        content=p[1]??string.Empty;slot=p[2]??string.Empty;return content.Length>0&&slot.Length>0;
    }
    private static string ComponentKey(string content,string slot){return (content??string.Empty).Trim()+"|"+(slot??string.Empty).Trim();}
    private static void Ensure(){if(!ready)Load();}
    private static string Req(XElement e,string n){string v=((string)e.Attribute(n)??string.Empty).Trim();if(v.Length==0)throw new InvalidDataException("Missing Phase9 attribute '"+n+"'.");return v;}
    private static float F(XElement e,string n,float d,float min,float max){string s=(string)e.Attribute(n);if(string.IsNullOrEmpty(s))return d;float v;if(!float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out v)||float.IsNaN(v)||float.IsInfinity(v)||v<min||v>max)throw new InvalidDataException("Invalid Phase9 '"+n+"'.");return v;}
    private static bool Near(float a,float b){return Math.Abs(a-b)<=.0002f;}
}
