using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Detached completed refund history. Does not itself establish inventory durability or release a claim.
internal sealed class RebirthStationRefundArchive
{
    private readonly XElement image;
    private RebirthStationRefundArchive(XElement node){image=new XElement(node);}
    internal string JobId=>(string)image.Attribute("job");
    internal string CreationId=>(string)image.Attribute("creation");
    internal XElement Write()=>new XElement(image);
    internal RebirthStationRefundArchive Clone()=>new RebirthStationRefundArchive(image);
    internal static bool TryCreate(string owner,RebirthStationGridAdmission admission,RebirthStationPublicationRecord publication,
        RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        RebirthStationCancellationPublication cancelled,RebirthStationRefundDelivery delivered,out RebirthStationRefundArchive archive)
    {
        archive=null;
        try
        {
            return TryRead(new XElement("stationRefundArchive",new XAttribute("version",1),new XAttribute("owner",owner),
                new XAttribute("job",admission.JobId),new XAttribute("creation",admission.CreationId),admission.Write(),publication.Write(),
                intent.Write(),refund.Write(),attempt.Write(),cancelled.Write(),delivered.Write()),owner,out archive);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,string owner,out RebirthStationRefundArchive archive)
    {
        archive=null;
        try
        {
            string[] names={"stationAdmission","stationPublication","stationTerminalIntent","stationCancellationRefund",
                "stationCancellationAttempt","stationCancellationPublication","stationRefundDelivery"};
            if(node==null||node.Name!="stationRefundArchive"||node.Attributes().Count()!=4||(string)node.Attribute("version")!="1"||
                string.IsNullOrWhiteSpace(owner)||owner.Length>1024||(string)node.Attribute("owner")!=owner||node.Elements().Count()!=names.Length||
                names.Any(name=>node.Elements(name).Count()!=1)||node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting))>8*1024*1024||
                !RebirthStationGridAdmission.TryReadStored(node.Element(names[0]),out var admission)||
                (string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
                !RebirthStationPublicationRecord.TryRead(node.Element(names[1]),admission,out _)||
                !RebirthStationTerminalIntent.TryRead(node.Element(names[2]),admission,out var intent)||intent.IsCompletion||
                !RebirthStationCancellationRefund.TryReadStored(node.Element(names[3]),admission,intent,out var refund)||
                !RebirthStationCancellationAttempt.TryRead(node.Element(names[4]),admission,intent,refund,out var attempt)||
                !RebirthStationCancellationPublication.TryRead(node.Element(names[5]),admission,intent,refund,attempt,out var cancelled)||
                !RebirthStationRefundDelivery.TryRead(node.Element(names[6]),owner,admission,intent,refund,attempt,cancelled,out var delivered)||!delivered.IsSaved)return false;
            archive=new RebirthStationRefundArchive(node);return true;
        }
        catch{return false;}
    }
    internal static XElement WriteAll(System.Collections.Generic.IDictionary<string,RebirthStationRefundArchive> records,string owner)
    {
        if(records==null||records.Count>256)throw new System.IO.InvalidDataException("Invalid refund archive count");
        var section=new XElement("stationRefundArchives",new XAttribute("version",1));
        var deliveries=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        long payloadBytes=0;
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!TryRead(pair.Value.Write(),owner,out var valid)||
                !deliveries.Add((string)valid.image.Element("stationRefundDelivery").Attribute("delivery")))
                throw new System.IO.InvalidDataException("Invalid or duplicate refund archive binding");
            var detached=valid.Write();
            payloadBytes+=Encoding.UTF8.GetByteCount(detached.ToString(SaveOptions.DisableFormatting));
            section.Add(detached);
            if(payloadBytes>16*1024*1024)
                throw new System.IO.InvalidDataException("Refund archive capacity exceeded");
        }
        if(Encoding.UTF8.GetByteCount(section.ToString(SaveOptions.DisableFormatting))>16*1024*1024)
            throw new System.IO.InvalidDataException("Refund archive capacity exceeded");
        return section;
    }
    internal static bool ReadAll(XElement progression,string owner,out System.Collections.Generic.Dictionary<string,RebirthStationRefundArchive> records)
    {
        records=new System.Collections.Generic.Dictionary<string,RebirthStationRefundArchive>(StringComparer.Ordinal);
        try
        {
            if(progression==null||progression.Elements("stationRefundArchives").Count()>1)return false;
            var section=progression.Element("stationRefundArchives");if(section==null)return true;
            if(section.Attributes().Count()!=1||(string)section.Attribute("version")!="1"||section.Elements().Count()>256||
                section.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                Encoding.UTF8.GetByteCount(section.ToString(SaveOptions.DisableFormatting))>16*1024*1024)return false;
            var deliveries=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var node in section.Elements())
            {
                if(!TryRead(node,owner,out var archive)||records.ContainsKey(archive.JobId)||
                    !deliveries.Add((string)archive.image.Element("stationRefundDelivery").Attribute("delivery")))return false;
                records.Add(archive.JobId,archive);
            }
            return true;
        }
        catch{records.Clear();return false;}
    }}