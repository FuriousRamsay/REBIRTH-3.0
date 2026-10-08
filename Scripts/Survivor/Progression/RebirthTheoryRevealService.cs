using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

#nullable disable

/// <summary>
/// Phase-11 data-first Theory information projection. The authored 48-Skill design lives in
/// theory_reveal.xml; this service only selects how much of that information is visible at the
/// survivor's current Theory band. It does not gate practical use, recipes or specific Knowledge.
/// </summary>
public static class RebirthTheoryRevealService
{
    private sealed class Subject
    {
        public string SkillId=string.Empty;
        public string Practical=string.Empty;
        public string Represents=string.Empty;
        public string StudySources=string.Empty;
        public string SpecificBoundary=string.Empty;
    }

    private static readonly Dictionary<string,Subject> Subjects=new Dictionary<string,Subject>(StringComparer.OrdinalIgnoreCase);
    public static int SubjectCount { get { return Subjects.Count; } }

    public static string Load()
    {
        Subjects.Clear();
        string path=Path.Combine(RebirthSurvivorDefinitionLoader.ResolveConfigRoot(),"theory_reveal.xml");
        XDocument doc=XDocument.Load(path); XElement root=doc.Root;
        if(root==null||root.Name!="survivor_theory_reveal"||(string)root.Attribute("schema_version")!="1")
            throw new InvalidDataException("theory_reveal.xml requires survivor_theory_reveal schema_version=1");
        XElement subjects=root.Element("subjects"); if(subjects==null)throw new InvalidDataException("theory_reveal.xml missing subjects");
        foreach(XElement e in subjects.Elements("subject"))
        {
            Subject s=new Subject{SkillId=Req(e,"skill_id"),Practical=Req(e,"practical"),Represents=Req(e,"represents"),StudySources=Req(e,"study_sources"),SpecificBoundary=Req(e,"specific_boundary")};
            RebirthSkillDefinition def; if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(s.SkillId,out def)||def==null)throw new InvalidDataException("Theory reveal references unknown Skill: "+s.SkillId);
            if(Subjects.ContainsKey(s.SkillId))throw new InvalidDataException("Duplicate Theory reveal subject: "+s.SkillId);
            Subjects.Add(s.SkillId,s);
        }
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle==null||bundle.Progression==null||Subjects.Count!=bundle.Progression.Skills.Count)throw new InvalidDataException("Theory reveal subject count must match Skill registry");
        for(int i=0;i<bundle.Progression.Skills.Count;i++)if(!Subjects.ContainsKey(bundle.Progression.Skills[i].Id))throw new InvalidDataException("Theory reveal missing Skill "+bundle.Progression.Skills[i].Id);
        return "theoryRevealSubjects="+Subjects.Count.ToString(CultureInfo.InvariantCulture);
    }

    public static string BuildReveal(string skillId,float theory)
    {
        Subject s; if(!Subjects.TryGetValue(skillId??string.Empty,out s)||s==null)return string.Empty;
        float v=Math.Max(0f,Math.Min(100f,theory)); StringBuilder b=new StringBuilder();
        b.Append(Localization.Get("xuiRebirthTheoryRevealAbout")).Append(" ").Append(s.Represents);
        if(v>=15f)b.Append("\n").Append(Localization.Get("xuiRebirthTheoryRevealPractice")).Append(" ").Append(s.Practical);
        if(v>=45f)b.Append("\n").Append(Localization.Get("xuiRebirthTheoryRevealStudy")).Append(" ").Append(s.StudySources);
        if(v>=75f)b.Append("\n").Append(Localization.Get("xuiRebirthTheoryRevealRecipes")).Append(" ").Append(s.SpecificBoundary);
        if(v>=100f)b.Append("\n").Append(Localization.Get("xuiRebirthTheoryRevealComplete"));
        return b.ToString();
    }

    public static string RunVectors()
    {
        int pass=0,total=0;StringBuilder b=new StringBuilder("[REBIRTH Theory Reveal Vectors]");
        Check(b,ref pass,ref total,"48 reveal subjects",Subjects.Count==48);
        string id="skill.spears";
        string low=BuildReveal(id,0f),mid=BuildReveal(id,50f),high=BuildReveal(id,80f),master=BuildReveal(id,100f);
        Check(b,ref pass,ref total,"low band hides practical detail",low.IndexOf(Localization.Get("xuiRebirthTheoryRevealPractice"),StringComparison.Ordinal)<0);
        Check(b,ref pass,ref total,"mid band reveals practical + study",mid.IndexOf(Localization.Get("xuiRebirthTheoryRevealPractice"),StringComparison.Ordinal)>=0&&mid.IndexOf(Localization.Get("xuiRebirthTheoryRevealStudy"),StringComparison.Ordinal)>=0);
        Check(b,ref pass,ref total,"high band reveals specific boundary",high.IndexOf(Localization.Get("xuiRebirthTheoryRevealRecipes"),StringComparison.Ordinal)>=0);
        Check(b,ref pass,ref total,"master band complete statement",master.IndexOf(Localization.Get("xuiRebirthTheoryRevealComplete"),StringComparison.Ordinal)>=0);
        b.Append("\n  RESULT ").Append(pass).Append('/').Append(total); return b.ToString();
    }

    private static string Req(XElement e,string name){string v=((string)e.Attribute(name)??string.Empty).Trim();if(v.Length==0)throw new InvalidDataException("Missing '"+name+"' on <"+e.Name+">");return v;}
    private static void Check(StringBuilder b,ref int pass,ref int total,string name,bool ok){total++;if(ok)pass++;b.Append("\n  ").Append(ok?"PASS ":"FAIL ").Append(name);}
}
