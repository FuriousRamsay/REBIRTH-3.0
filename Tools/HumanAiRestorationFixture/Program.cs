using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static class Program
{
    private static int passed;
    private static void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    private static bool Admit(float distance,float owner=-1f,float range=10f,bool bypass=false,bool eventPlayer=false)
        =>RebirthHumanAiRules.InsideAcquisition(distance,owner,range,bypass,eventPlayer);
    private static void Main(string[] args)
    {
        Check(RebirthHumanAiRules.RetreatDistance(false)==4f,"historical non-player retreat distance");
        Check(RebirthHumanAiRules.RetreatDistance(true)==5f,"historical player retreat distance");
        Check(RebirthHumanAiRules.CanRetreat(false,false),"unowned ranged NPC may retreat");
        Check(RebirthHumanAiRules.CanRetreat(true,true),"owned following NPC may retreat");
        Check(!RebirthHumanAiRules.CanRetreat(true,false),"owned Stay/Guard/TempStay must not retreat");
        Check(RebirthHumanAiRules.AcquisitionDistance(false,false,30,10)==30,"Hunting acquisition");
        Check(RebirthHumanAiRules.AcquisitionDistance(false,true,30,10)==10,"Full Control acquisition");
        Check(RebirthHumanAiRules.AcquisitionDistance(true,true,30,10)==3,"offline local-defense override");
        Check(RebirthHumanAiRules.AcquisitionDistance(false,true,42,13)==13,"authored Full Control distance");
        Check(Admit(9.99f)&&!Admit(10f),"strict acquisition threshold");
        Check(Admit(2.99f,range:3)&&!Admit(3f,range:3),"offline boundary");
        Check(Admit(40,14.99f)&&!Admit(40,15),"owner-relative +5 m boundary");
        Check(Admit(50,bypass:true)&&!Admit(50.01f,bypass:true),"explicit threat bypass capped at 50 m");
        Check(Admit(120,eventPlayer:true)&&!Admit(120.01f,eventPlayer:true),"event player search capped at 120 m");
        Check(!Admit(float.NaN)&&!Admit(float.PositiveInfinity)&&!Admit(-1),"invalid distances rejected");
        Check(RebirthHumanAiRules.TracksElevatedTarget(true,true,4)&&!RebirthHumanAiRules.TracksElevatedTarget(true,true,4.01f),"no-melee-tracking height boundary");
        Check(RebirthHumanAiRules.TracksElevatedTarget(false,true,20)&&RebirthHumanAiRules.TracksElevatedTarget(true,false,20),"height exception requires both tags");
        string root=Path.GetFullPath(args.Length==0?".":args[0]);
        var human=XDocument.Load(Path.Combine(root,"Config/_NPC/entityclasses.xml"));
        var dog=XDocument.Load(Path.Combine(root,"Config/_NPC/dogs.xml"));
        var classes=human.Descendants("entity_class").Concat(dog.Descendants("entity_class")).ToDictionary(e=>(string)e.Attribute("name"));
        Dictionary<string,string> Properties(string name,HashSet<string> seen=null)
        {
            seen=seen??new HashSet<string>();if(!classes.TryGetValue(name,out var node))return new Dictionary<string,string>();
            if(!seen.Add(name))throw new Exception("Inheritance cycle");
            var p=Properties((string)node.Attribute("extends")??"",seen);
            foreach(var prop in node.Elements("property").Where(p=>p.Attribute("name")!=null))p[(string)prop.Attribute("name")]=(string)prop.Attribute("value");return p;
        }
        foreach(var node in human.Descendants("entity_class"))
        {
            string name=(string)node.Attribute("name");var p=Properties(name);
            Check(p["AITask-6"].StartsWith("RebirthWander,")&&p["AITask-1"].StartsWith("RebirthBreakObstruction,")&&p["AITarget-1"].StartsWith("RebirthRetaliate,"),"human task inheritance "+name);
        }
        foreach(var node in dog.Descendants("entity_class"))
        {
            string name=(string)node.Attribute("name");var p=Properties(name);
            Check(!p.Values.Any(v=>v!=null&&(v.Contains("RebirthWander,")||v.Contains("RebirthRelocateToTarget,")||v.Contains("RebirthHumanEnvironment,")||v.Contains("RebirthBreakObstruction,")||v.Contains("RebirthRetaliate,"))),"dog excludes human tasks "+name);
        }
        Console.WriteLine("RESULT: "+passed+" checks passed. Pure production rules and XML only; no game or Unity execution.");
    }
}

