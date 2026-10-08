using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Durable owner/destination journal binding. Phase data grants no inventory mutation authority.
internal sealed class RebirthStationRefundDelivery
{
    private readonly XElement image;
    private RebirthStationRefundDelivery(XElement node){image=new XElement(node);}
    internal string JobId=>(string)image.Attribute("job");
    internal string OwnerKey=>(string)image.Attribute("owner");
    internal bool IsAttempted=>(string)image.Attribute("phase")!="prepared";
    internal bool IsSaved=>(string)image.Attribute("phase")=="deliverySaved";
    internal XElement Write()=>new XElement(image);
    internal RebirthStationRefundDelivery Clone()=>new RebirthStationRefundDelivery(image);
    private static string Digest(XElement node)=>RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)));
    internal static bool TryCreate(Guid deliveryId,string ownerKey,RebirthStationGridAdmission admission,
        RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        RebirthStationCancellationPublication publication,RebirthStationRefundBackpackPlan plan,out RebirthStationRefundDelivery delivery)
    {
        delivery=null;
        try
        {
            if(deliveryId==Guid.Empty||plan==null||publication==null||refund==null)return false;
            return TryRead(new XElement("stationRefundDelivery",new XAttribute("version",1),new XAttribute("phase","prepared"),
                new XAttribute("delivery",deliveryId.ToString("N")),new XAttribute("job",admission.JobId),new XAttribute("creation",admission.CreationId),
                new XAttribute("owner",ownerKey),new XAttribute("publication",Digest(publication.Write())),new XAttribute("refund",Digest(refund.Write())),plan.Write()),
                ownerKey,admission,intent,refund,attempt,publication,out delivery);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,string ownerKey,RebirthStationGridAdmission admission,
        RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        RebirthStationCancellationPublication publication,out RebirthStationRefundDelivery delivery)
    {
        delivery=null;
        try
        {
            if(node==null||node.Name!="stationRefundDelivery"||node.Attributes().Count()!=8||node.Elements().Count()!=1||
                node.Elements("stationRefundBackpackPlan").Count()!=1||
                node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                (string)node.Attribute("version")!="1"||
                (string)node.Attribute("phase")!="prepared"&&(string)node.Attribute("phase")!="deliveryAttempted"&&(string)node.Attribute("phase")!="deliverySaved"||
                !Guid.TryParseExact((string)node.Attribute("delivery"),"N",out var id)||id==Guid.Empty||
                string.IsNullOrWhiteSpace(ownerKey)||ownerKey.Length>1024||(string)node.Attribute("owner")!=ownerKey||
                admission==null||(string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
                publication==null||refund==null||
                !RebirthStationCancellationPublication.TryRead(publication.Write(),admission,intent,refund,attempt,out _)||
                (string)node.Attribute("publication")!=Digest(publication.Write())||(string)node.Attribute("refund")!=Digest(refund.Write())||
                Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting))>2*1024*1024+4096||
                !RebirthStationRefundBackpackPlan.TryRead(node.Element("stationRefundBackpackPlan"),refund,out _))return false;
            delivery=new RebirthStationRefundDelivery(node);return true;
        }
        catch{return false;}
    }
    internal bool TryMarkAttempted(out RebirthStationRefundDelivery attempted)
    {
        attempted=null;if(IsAttempted)return false;
        var next=Write();next.SetAttributeValue("phase","deliveryAttempted");attempted=new RebirthStationRefundDelivery(next);return true;
    }
    // Caller must establish final native player-file receipt before retaining this transition.
    internal bool TryMarkSaved(out RebirthStationRefundDelivery saved)
    {
        saved=null;if((string)image.Attribute("phase")!="deliveryAttempted")return false;
        var next=Write();next.SetAttributeValue("phase","deliverySaved");saved=new RebirthStationRefundDelivery(next);return true;
    }
    internal bool TryCopyPlan(RebirthStationCancellationRefund refund,out RebirthStationRefundBackpackPlan plan)
    {return RebirthStationRefundBackpackPlan.TryRead(image.Element("stationRefundBackpackPlan"),refund,out plan);}
    internal static XElement WriteAll(IDictionary<string,RebirthStationRefundDelivery> records,string ownerKey,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthStationTerminalIntent> intents,
        IDictionary<string,RebirthStationCancellationRefund> refunds,IDictionary<string,RebirthStationCancellationAttempt> attempts,
        IDictionary<string,RebirthStationCancellationPublication> publications)
    {
        if(records==null||records.Count>64)throw new InvalidDataException("Invalid station refund delivery count");
        var section=new XElement("stationRefundDeliveries",new XAttribute("version",1));var ids=new HashSet<Guid>();
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var a)||!intents.TryGetValue(pair.Key,out var i)||
                !refunds.TryGetValue(pair.Key,out var r)||!attempts.TryGetValue(pair.Key,out var t)||!publications.TryGetValue(pair.Key,out var p)||
                !TryRead(pair.Value.image,ownerKey,a,i,r,t,p,out var valid)||!ids.Add(Guid.ParseExact((string)valid.image.Attribute("delivery"),"N")))
                throw new InvalidDataException("Invalid or duplicate refund delivery binding");
            section.Add(valid.Write());
        }
        return section;
    }
    internal static bool ReadAll(XElement progression,string ownerKey,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationCancellationRefund> refunds,
        IDictionary<string,RebirthStationCancellationAttempt> attempts,IDictionary<string,RebirthStationCancellationPublication> publications,
        out Dictionary<string,RebirthStationRefundDelivery> records)
    {
        records=new Dictionary<string,RebirthStationRefundDelivery>(StringComparer.Ordinal);
        try
        {
            var sections=progression.Elements("stationRefundDeliveries").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            var ids=new HashSet<Guid>();
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||!intents.TryGetValue(job,out var i)||
                    !refunds.TryGetValue(job,out var r)||!attempts.TryGetValue(job,out var t)||!publications.TryGetValue(job,out var p)||
                    !TryRead(node,ownerKey,a,i,r,t,p,out var record)||!ids.Add(Guid.ParseExact((string)record.image.Attribute("delivery"),"N")))return false;
                records.Add(job,record);
            }
            return true;
        }
        catch{return false;}
    }
}