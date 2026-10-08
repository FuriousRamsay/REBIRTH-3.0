using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Authored reflection balance, independent of practical XP and finite Insight markers.
internal sealed class RebirthTheorySoloRule
{
    internal string Subject,Family;
    internal int MinimumOutcomes;
    internal float Duration,Gain,DifficultyMargin,DifficultyCeiling;
    internal double Cooldown,EventSpacing;
    internal bool Relevant(float theory,float difficulty)
        =>Finite(theory)&&Finite(difficulty)&&theory>=0&&theory<100&&difficulty>=0&&difficulty<=100&&
            difficulty>=Math.Max(0,Math.Min(theory,DifficultyCeiling)-DifficultyMargin);
    private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
}
internal static class RebirthTheorySoloRegistry
{
    private static readonly Dictionary<string,RebirthTheorySoloRule> Rules=new Dictionary<string,RebirthTheorySoloRule>(StringComparer.Ordinal);
    private static RebirthTheorySoloLockpickRelevance LockpickRelevance;
    private static RebirthTheorySoloCombatRelevance CombatRelevance;
    internal static bool TryCombatDifficulty(float nativeMaxHealth,out float difficulty){difficulty=0;return CombatRelevance!=null&&CombatRelevance.TryDifficulty(nativeMaxHealth,out difficulty);}
    internal static bool TryLockpickDifficulty(float nativeSeconds,out float difficulty){difficulty=0;return LockpickRelevance!=null&&LockpickRelevance.TryDifficulty(nativeSeconds,out difficulty);}
    internal static int Count=>Rules.Count;
    internal static bool TryGet(string subject,out RebirthTheorySoloRule rule)=>Rules.TryGetValue(subject??string.Empty,out rule);
    internal static bool Matches(string subject,string family)=>TryGet(subject,out var rule)&&rule.Family==family;
    internal static void Load(string path,IEnumerable<string> expectedSubjects)
    {
        var expected=new HashSet<string>(expectedSubjects,StringComparer.Ordinal);
        var root=XDocument.Load(path).Root;
        if(expected.Count!=48||root==null||root.Name!="survivor_theory_solo"||(string)root.Attribute("schema_version")!="1")throw new InvalidDataException("Solo Theory authoring/subject count invalid");
        var policy=root.Element("policy");var subjects=root.Element("subjects");
        if(policy==null||subjects==null||root.Elements().Count()!=2)throw new InvalidDataException("Solo Theory policy/subjects missing");
        float Number(string name,float minimum,float maximum)
        {
            if(!float.TryParse((string)policy.Attribute(name),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)||float.IsNaN(n)||float.IsInfinity(n)||n<minimum||n>maximum)throw new InvalidDataException("Solo Theory policy invalid: "+name);
            return n;
        }
        float count=Number("minimum_outcomes",1,16);if(count!=(int)count)throw new InvalidDataException("Solo Theory outcome count not integer");
        if(!RebirthTheorySoloLockpickRelevance.TryReadPolicy(policy,out var lockpickRelevance))throw new InvalidDataException("Solo Theory lockpick relevance authoring invalid");
        if(!RebirthTheorySoloCombatRelevance.TryReadPolicy(policy,out var combatRelevance))throw new InvalidDataException("Solo Theory combat relevance authoring invalid");
        var next=new Dictionary<string,RebirthTheorySoloRule>(StringComparer.Ordinal);
        foreach(var row in subjects.Elements())
        {
            string subject=(string)row.Attribute("skill_id"),family=(string)row.Attribute("producer_family");
            if(row.Name!="subject"||row.HasElements||row.Attributes().Count()!=2||!expected.Contains(subject??"")||string.IsNullOrEmpty(family)||family.Length>64||family.Any(c=>!(char.IsLower(c)||c=='_'))||next.ContainsKey(subject))throw new InvalidDataException("Solo Theory subject/family invalid");
            next.Add(subject,new RebirthTheorySoloRule{Subject=subject,Family=family,MinimumOutcomes=(int)count,
                Duration=Number("duration_seconds",1,3600),Gain=Number("theory_gain",.001f,3),Cooldown=Number("cooldown_active_seconds",1,86400),
                EventSpacing=Number("minimum_event_spacing_active_seconds",1,3600),DifficultyMargin=Number("difficulty_margin",0,100),DifficultyCeiling=Number("difficulty_reference_ceiling",0,100)});
        }
        if(next.Count!=expected.Count)throw new InvalidDataException("Solo Theory subject coverage incomplete");
        Rules.Clear();foreach(var pair in next)Rules.Add(pair.Key,pair.Value);LockpickRelevance=lockpickRelevance;CombatRelevance=combatRelevance;
    }
}