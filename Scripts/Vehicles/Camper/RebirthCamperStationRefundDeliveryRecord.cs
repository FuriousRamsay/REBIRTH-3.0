using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Immutable owner-bound refund destination/phase evidence. Deserialization and
// phase copies never authorize applying inventory credits or retrying them.
internal sealed class RebirthCamperStationRefundDeliveryRecord
{
    private readonly XElement image;
    internal string JobId=>(string)image.Attribute("job");
    internal Guid DeliveryId=>Guid.ParseExact((string)image.Attribute("delivery"),"N");
    internal bool IsAttempted=>(string)image.Attribute("phase")!="prepared";
    internal bool IsSaved=>(string)image.Attribute("phase")=="deliverySaved";
    private RebirthCamperStationRefundDeliveryRecord(XElement node){image=new XElement(node);}
    internal XElement Write()=>new XElement(image);
    internal RebirthCamperStationRefundDeliveryRecord Clone()=>new RebirthCamperStationRefundDeliveryRecord(image);
    private static string Hash(XElement node)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))).Replace("-","");}
    private static bool Owner(string ownerKey)=>!string.IsNullOrWhiteSpace(ownerKey)&&Encoding.UTF8.GetByteCount(ownerKey)<=512;
    internal static bool TryCreate(Guid deliveryId,string ownerKey,RebirthStationGridAdmission admission,
        RebirthCamperStationPublicationRecord queuedPublication,RebirthStationTerminalIntent intent,
        RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        RebirthCamperStationCancellationRecord mobileCancellation,RebirthStationRefundBackpackPlan backpackPlan,
        out RebirthCamperStationRefundDeliveryRecord record)
    {
        record=null;
        try
        {
            if(deliveryId==Guid.Empty||!Owner(ownerKey)||admission==null||queuedPublication==null||intent==null||
                refund==null||attempt==null||mobileCancellation==null||backpackPlan==null)return false;
            var node=new XElement("camperStationRefundDelivery",new XAttribute("version",1),new XAttribute("phase","prepared"),
                new XAttribute("job",admission.JobId),new XAttribute("creation",admission.CreationId),
                new XAttribute("delivery",deliveryId.ToString("N")),new XAttribute("owner",ownerKey),
                new XAttribute("admission",Hash(admission.Write())),new XAttribute("queued",Hash(queuedPublication.Write())),
                new XAttribute("intent",Hash(intent.Write())),new XAttribute("refund",Hash(refund.Write())),
                new XAttribute("attempt",Hash(attempt.Write())),new XAttribute("cancellation",Hash(mobileCancellation.Write())),backpackPlan.Write());
            return TryRead(node,ownerKey,admission,queuedPublication,intent,refund,attempt,mobileCancellation,out record);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,string ownerKey,RebirthStationGridAdmission admission,
        RebirthCamperStationPublicationRecord queuedPublication,RebirthStationTerminalIntent intent,
        RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        RebirthCamperStationCancellationRecord mobileCancellation,out RebirthCamperStationRefundDeliveryRecord record)
    {
        record=null;
        try
        {
            if(!Owner(ownerKey)||node==null||admission==null||queuedPublication==null||intent==null||intent.IsCompletion||
                refund==null||attempt==null||mobileCancellation==null||node.Name!="camperStationRefundDelivery"||
                node.Attributes().Count()!=12||(string)node.Attribute("version")!="1"||
                (string)node.Attribute("owner")!=ownerKey||(string)node.Attribute("job")!=admission.JobId||
                (string)node.Attribute("creation")!=admission.CreationId||
                !Guid.TryParseExact((string)node.Attribute("delivery"),"N",out var delivery)||delivery==Guid.Empty||
                delivery.ToString("N")!=(string)node.Attribute("delivery")||
                !new[]{"prepared","deliveryAttempted","deliverySaved"}.Contains((string)node.Attribute("phase"))||
                node.Elements().Count()!=1||node.Elements("stationRefundBackpackPlan").Count()!=1||
                node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                !RebirthCamperStationCancellationRecord.TryRead(mobileCancellation.Write(),admission,queuedPublication,intent,refund,attempt,out _)||
                (string)node.Attribute("admission")!=Hash(admission.Write())||
                (string)node.Attribute("queued")!=Hash(queuedPublication.Write())||
                (string)node.Attribute("intent")!=Hash(intent.Write())||(string)node.Attribute("refund")!=Hash(refund.Write())||
                (string)node.Attribute("attempt")!=Hash(attempt.Write())||(string)node.Attribute("cancellation")!=Hash(mobileCancellation.Write())||
                !RebirthStationRefundBackpackPlan.TryRead(node.Element("stationRefundBackpackPlan"),refund,out var plan)||
                !XNode.DeepEquals(node.Element("stationRefundBackpackPlan"),plan.Write()))return false;
            record=new RebirthCamperStationRefundDeliveryRecord(node);return true;
        }
        catch{return false;}
    }
    internal bool TryMarkAttempted(out RebirthCamperStationRefundDeliveryRecord record)
    {
        record=null;if(IsAttempted)return false;
        var node=Write();node.SetAttributeValue("phase","deliveryAttempted");record=new RebirthCamperStationRefundDeliveryRecord(node);return true;
    }
    internal bool TryMarkSaved(out RebirthCamperStationRefundDeliveryRecord record)
    {
        record=null;if(!IsAttempted||IsSaved)return false;
        var node=Write();node.SetAttributeValue("phase","deliverySaved");record=new RebirthCamperStationRefundDeliveryRecord(node);return true;
    }
    internal bool TryCopyPlan(RebirthStationCancellationRefund refund,out RebirthStationRefundBackpackPlan plan)
    {
        plan=null;
        try{return refund!=null&&(string)image.Attribute("refund")==Hash(refund.Write())&&
            RebirthStationRefundBackpackPlan.TryRead(image.Element("stationRefundBackpackPlan"),refund,out plan);}
        catch{return false;}
    }
    internal static XElement WriteAll(IDictionary<string,RebirthCamperStationRefundDeliveryRecord> records,string ownerKey,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthCamperStationPublicationRecord> queued,
        IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationCancellationRefund> refunds,
        IDictionary<string,RebirthStationCancellationAttempt> attempts,IDictionary<string,RebirthCamperStationCancellationRecord> cancellations)
    {
        if(records==null||records.Count>64||!Owner(ownerKey)||admissions==null||queued==null||intents==null||
            refunds==null||attempts==null||cancellations==null)throw new InvalidDataException("Invalid mobile refund deliveries.");
        var section=new XElement("camperStationRefundDeliveries",new XAttribute("version",1));var ids=new HashSet<Guid>();
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var a)||!queued.TryGetValue(pair.Key,out var q)||
                !intents.TryGetValue(pair.Key,out var i)||!refunds.TryGetValue(pair.Key,out var r)||!attempts.TryGetValue(pair.Key,out var t)||
                !cancellations.TryGetValue(pair.Key,out var c)||!TryRead(pair.Value.image,ownerKey,a,q,i,r,t,c,out var valid)||
                !ids.Add(valid.DeliveryId))throw new InvalidDataException("Invalid mobile refund delivery binding.");
            section.Add(valid.Write());
        }
        return section;
    }
    internal static bool ReadAll(XElement progression,string ownerKey,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthCamperStationPublicationRecord> queued,IDictionary<string,RebirthStationTerminalIntent> intents,
        IDictionary<string,RebirthStationCancellationRefund> refunds,IDictionary<string,RebirthStationCancellationAttempt> attempts,
        IDictionary<string,RebirthCamperStationCancellationRecord> cancellations,out Dictionary<string,RebirthCamperStationRefundDeliveryRecord> records)
    {
        records=new Dictionary<string,RebirthCamperStationRefundDeliveryRecord>(StringComparer.Ordinal);
        try
        {
            if(!Owner(ownerKey)||progression==null||admissions==null||queued==null||intents==null||
                refunds==null||attempts==null||cancellations==null)return false;
            var sections=progression.Elements("camperStationRefundDeliveries").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            var ids=new HashSet<Guid>();var parsed=new Dictionary<string,RebirthCamperStationRefundDeliveryRecord>(StringComparer.Ordinal);
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(parsed.Count>=64||job==null||parsed.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||!queued.TryGetValue(job,out var q)||
                    !intents.TryGetValue(job,out var i)||!refunds.TryGetValue(job,out var r)||!attempts.TryGetValue(job,out var t)||
                    !cancellations.TryGetValue(job,out var c)||!TryRead(node,ownerKey,a,q,i,r,t,c,out var valid)||!ids.Add(valid.DeliveryId))return false;
                parsed.Add(job,valid);
            }
            records=parsed;return true;
        }
        catch{return false;}
    }
}
