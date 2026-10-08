using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

#nullable disable

public sealed class RebirthSkillReachabilityDeclaration
{
    public string SkillId=string.Empty;
    public string Route=string.Empty;
    public string EvidenceFamily=string.Empty;
    public string NormalizationProfile=string.Empty;
    public string UiProjection=string.Empty;
    public bool SoloViable;
    public string SoloNote=string.Empty;
    public string NetworkAcceptance=string.Empty;
}

/// <summary>
/// Permanent fail-closed release invariant: every practical Skill must declare how it trains,
/// how the work is normalized/projected, how a solo player can reach it and which multiplayer
/// roles must accept it. This is authoring validation, not a second XP system.
/// </summary>
public static class RebirthSkillReachabilityValidator
{
    private static readonly Dictionary<string,RebirthSkillReachabilityDeclaration> Entries=
        new Dictionary<string,RebirthSkillReachabilityDeclaration>(StringComparer.OrdinalIgnoreCase);
    public static int Count { get { return Entries.Count; } }

    public static string LoadAndValidate()
    {
        Entries.Clear();
        string path=Path.Combine(RebirthSurvivorDefinitionLoader.ResolveConfigRoot(),"skill_reachability.xml");
        XDocument doc=XDocument.Load(path);XElement root=doc.Root;
        if(root==null||root.Name!="survivor_skill_reachability"||(string)root.Attribute("schema_version")!="1")
            throw new InvalidDataException("skill_reachability.xml requires survivor_skill_reachability schema_version=1");
        XElement skills=root.Element("skills");if(skills==null)throw new InvalidDataException("skill_reachability.xml missing skills");
        foreach(XElement e in skills.Elements("skill"))
        {
            RebirthSkillReachabilityDeclaration d=new RebirthSkillReachabilityDeclaration{
                SkillId=Req(e,"skill_id"),Route=Req(e,"route"),EvidenceFamily=Req(e,"evidence_family"),NormalizationProfile=Req(e,"normalization_profile"),
                UiProjection=Req(e,"ui_projection"),SoloViable=Bool(e,"solo_viable"),SoloNote=Req(e,"solo_note"),NetworkAcceptance=Req(e,"network_acceptance")};
            if(!d.SoloViable)throw new InvalidDataException("Core practical Skill is not solo viable: "+d.SkillId);
            if(!string.Equals(d.NetworkAcceptance,"host,p2p_client,dedicated_client",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Skill lacks full network acceptance declaration: "+d.SkillId);
            RebirthSkillDefinition skill;if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(d.SkillId,out skill)||skill==null)throw new InvalidDataException("Reachability references unknown Skill: "+d.SkillId);
            RebirthSkillTrainingProfile profile;if(!RebirthSkillTrainingProfileRegistry.TryGet(d.NormalizationProfile,out profile)||profile==null)throw new InvalidDataException("Reachability missing normalization profile: "+d.SkillId);
            if(!string.Equals(profile.Family,d.EvidenceFamily,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Reachability evidence family/profile drift: "+d.SkillId);
            if(Entries.ContainsKey(d.SkillId))throw new InvalidDataException("Duplicate reachability declaration: "+d.SkillId);
            Entries.Add(d.SkillId,d);
        }
        RebirthSurvivorDefinitionBundle bundle=RebirthSurvivorDefinitionRegistry.Bundle;
        if(bundle==null||bundle.Progression==null||Entries.Count!=bundle.Progression.Skills.Count)throw new InvalidDataException("Reachability declaration count must match Skill registry");
        for(int i=0;i<bundle.Progression.Skills.Count;i++)if(!Entries.ContainsKey(bundle.Progression.Skills[i].Id))throw new InvalidDataException("Reachability missing Skill: "+bundle.Progression.Skills[i].Id);
        return "reachability="+Entries.Count.ToString(CultureInfo.InvariantCulture)+"/"+Entries.Count.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryGet(string skillId,out RebirthSkillReachabilityDeclaration declaration)
    { return Entries.TryGetValue(skillId??string.Empty,out declaration)&&declaration!=null; }

    public static string RunVectors()
    {
        int pass=0,total=0;StringBuilder b=new StringBuilder("[REBIRTH Skill Reachability Vectors]");
        Check(b,ref pass,ref total,"48 declarations",Entries.Count==48);
        bool complete=true;
        foreach(RebirthSkillReachabilityDeclaration d in Entries.Values)complete&=d!=null&&d.Route.Length>0&&d.EvidenceFamily.Length>0&&d.NormalizationProfile.Length>0&&d.UiProjection.Length>0&&d.SoloViable&&d.SoloNote.Length>0&&d.NetworkAcceptance=="host,p2p_client,dedicated_client";
        Check(b,ref pass,ref total,"all declarations complete",complete);
        RebirthSkillReachabilityDeclaration teaching;Check(b,ref pass,ref total,"Teaching has solo NPC path",TryGet("skill.teaching",out teaching)&&teaching.SoloNote.IndexOf("NPC",StringComparison.OrdinalIgnoreCase)>=0);
        b.Append("\n  RESULT ").Append(pass).Append('/').Append(total);return b.ToString();
    }

    private static string Req(XElement e,string n){string v=((string)e.Attribute(n)??string.Empty).Trim();if(v.Length==0)throw new InvalidDataException("Missing '"+n+"' on <"+e.Name+">");return v;}
    private static bool Bool(XElement e,string n){bool v;if(!bool.TryParse(Req(e,n),out v))throw new InvalidDataException("Invalid bool '"+n+"' on <"+e.Name+">");return v;}
    private static void Check(StringBuilder b,ref int pass,ref int total,string name,bool ok){total++;if(ok)pass++;b.Append("\n  ").Append(ok?"PASS ":"FAIL ").Append(name);}
}
