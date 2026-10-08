using System;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Persistent attempted phase only. Never grants replay, refund or terminal outcome authority.
internal sealed class RebirthStationCancellationAttempt
{
    private readonly XElement image;
    private static string RefundDigest(RebirthStationCancellationRefund refund){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(refund.Write().ToString(SaveOptions.DisableFormatting)))).Replace("-","");}
    internal string JobId=>(string)image.Element("stationTerminalIntent").Attribute("job");
    private RebirthStationCancellationAttempt(XElement node){image=new XElement(node);}
    internal XElement Write()=>new XElement(image);
    internal RebirthStationCancellationAttempt Clone()=>new RebirthStationCancellationAttempt(image);
    internal static bool TryCreate(RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationCancellationRefund refund,RebirthStationNativeQueueSnapshot before,RebirthStationNativeQueueSnapshot after,
        RebirthStationTerminalSnapshot terminal,out RebirthStationCancellationAttempt attempt)
    {
        attempt=null;
        try
        {
            if(before==null||after==null||terminal==null||intent==null||refund==null)return false;
            return TryRead(new XElement("stationCancellationAttempt",new XAttribute("version",1),new XAttribute("phase","nativeAttempted"),
                new XAttribute("before",before.Digest()),new XAttribute("after",after.Digest()),new XAttribute("terminal",terminal.Digest()),
                new XAttribute("refund",RefundDigest(refund)),intent.Write()),admission,intent,refund,out attempt);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,
        RebirthStationCancellationRefund refund,out RebirthStationCancellationAttempt attempt)
    {
        attempt=null;
        try
        {
            if(node==null||node.Name!="stationCancellationAttempt"||node.Attributes().Count()!=6||
                (string)node.Attribute("version")!="1"||(string)node.Attribute("phase")!="nativeAttempted"||
                node.Elements().Count()!=1||node.Elements("stationTerminalIntent").Count()!=1||
                node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                intent==null||intent.IsCompletion||refund==null||
                !RebirthStationTerminalIntent.TryRead(intent.Write(),admission,out _)||
                !RebirthStationCancellationRefund.TryReadStored(refund.Write(),admission,intent,out _)||
                !XNode.DeepEquals(node.Element("stationTerminalIntent"),intent.Write())||
                (string)node.Attribute("refund")!=RefundDigest(refund))return false;
            foreach(string name in new[]{"before","after","terminal","refund"})
            {
                string value=(string)node.Attribute(name);
                if(value==null||value.Length!=64||value.Any(c=>!(c>='0'&&c<='9'||c>='A'&&c<='F')))return false;
            }
            if((string)node.Attribute("before")== (string)node.Attribute("after"))return false;
            attempt=new RebirthStationCancellationAttempt(node);return true;
        }
        catch{return false;}
    }
    internal static XElement WriteAll(IDictionary<string,RebirthStationCancellationAttempt> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationTerminalIntent> intents,
        IDictionary<string,RebirthStationCancellationRefund> refunds)
    {
        if(records==null||admissions==null||intents==null||refunds==null||records.Count>64)throw new InvalidDataException("Invalid station cancellation attempts");
        var section=new XElement("stationCancellationAttempts",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var admission)||
                !intents.TryGetValue(pair.Key,out var intent)||!refunds.TryGetValue(pair.Key,out var refund)||
                !TryRead(pair.Value.image,admission,intent,refund,out var valid))throw new InvalidDataException("Invalid station cancellation attempt binding");
            section.Add(valid.Write());
        }
        return section;
    }
    internal static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationCancellationRefund> refunds,
        out Dictionary<string,RebirthStationCancellationAttempt> records)
    {
        records=new Dictionary<string,RebirthStationCancellationAttempt>(StringComparer.Ordinal);
        try
        {
            var sections=progression.Elements("stationCancellationAttempts").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Element("stationTerminalIntent")?.Attribute("job");
                if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var admission)||
                    !intents.TryGetValue(job,out var intent)||!refunds.TryGetValue(job,out var refund)||
                    !TryRead(node,admission,intent,refund,out var record))return false;
                records.Add(job,record);
            }
            return true;
        }
        catch{return false;}
    }}