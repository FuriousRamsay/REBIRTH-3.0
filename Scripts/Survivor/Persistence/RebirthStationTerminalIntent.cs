using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Persistent choice only. Does not prove completion, refund, delivery or reward settlement.
public sealed class RebirthStationTerminalIntent
{
    private readonly XElement image;
    public string JobId=>(string)image.Attribute("job");
    public bool IsCompletion=>(string)image.Attribute("outcome")=="completion";
    private RebirthStationTerminalIntent(XElement node){image=new XElement(node);}
    public XElement Write()=>new XElement(image);
    public RebirthStationTerminalIntent Clone()=>new RebirthStationTerminalIntent(image);
    private static string Binding(RebirthStationGridAdmission admission)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(admission.Write().ToString(SaveOptions.DisableFormatting)))).Replace("-","");}
    public static bool TryCreate(RebirthStationGridAdmission admission,bool completion,int actor,out RebirthStationTerminalIntent intent)
    {
        intent=null;if(admission==null||!admission.IsPublicationAttempted||actor<=0)return false;
        return TryRead(new XElement("stationTerminalIntent",new XAttribute("version",1),new XAttribute("phase","intent"),
            new XAttribute("job",admission.JobId),new XAttribute("creation",admission.CreationId),new XAttribute("admission",Binding(admission)),
            new XAttribute("outcome",completion?"completion":"cancellation"),new XAttribute("actor",actor)),admission,out intent);
    }
    public static bool TryRead(XElement node,RebirthStationGridAdmission admission,out RebirthStationTerminalIntent intent)
    {
        intent=null;
        try
        {
            if(node==null||admission==null||!admission.IsPublicationAttempted||node.Name!="stationTerminalIntent"||node.HasElements||
                node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||node.Attributes().Count()!=7||
                (string)node.Attribute("version")!="1"||(string)node.Attribute("phase")!="intent"||
                (string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
                (string)node.Attribute("admission")!=Binding(admission)||(int)node.Attribute("actor")<=0||
                (string)node.Attribute("outcome")!="completion"&&(string)node.Attribute("outcome")!="cancellation")return false;
            intent=new RebirthStationTerminalIntent(node);return true;
        }
        catch{return false;}
    }
    public static XElement WriteAll(IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationGridAdmission> admissions)
    {
        if(intents==null||admissions==null||intents.Count>64)throw new InvalidDataException("Invalid station terminal intents");
        var section=new XElement("stationTerminalIntents",new XAttribute("version",1));
        foreach(var pair in intents.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var admission)||
                !TryRead(pair.Value.image,admission,out var valid))throw new InvalidDataException("Invalid station terminal intent binding");
            section.Add(valid.Write());
        }
        return section;
    }
    public static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,out Dictionary<string,RebirthStationTerminalIntent> intents)
    {
        intents=new Dictionary<string,RebirthStationTerminalIntent>(StringComparer.Ordinal);
        try
        {
            var sections=progression.Elements("stationTerminalIntents").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(intents.Count>=64||job==null||intents.ContainsKey(job)||!admissions.TryGetValue(job,out var admission)||
                    !TryRead(node,admission,out var intent))return false;
                intents.Add(job,intent);
            }
            return true;
        }
        catch{return false;}
    }
}