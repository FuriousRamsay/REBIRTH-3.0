using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Immutable mobile queued-publication receipt. Parsing a receipt is not proof
// of current native persistence, live ownership, output or progression.
public sealed class RebirthCamperStationPublicationRecord
{
    private readonly XElement image;
    public string JobId => (string)image.Attribute("job");
    private RebirthCamperStationPublicationRecord(XElement node){image=new XElement(node);}
    public XElement Write()=>new XElement(image);
    public RebirthCamperStationPublicationRecord Clone()=>new RebirthCamperStationPublicationRecord(image);
    private static string Hash(string text)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
    private static string AdmissionHash(RebirthStationGridAdmission admission)=>Hash(admission.Write().ToString(SaveOptions.DisableFormatting));
    private static string SaveHash(string directory)
    {
        string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        if(Path.DirectorySeparatorChar=='\\')root=root.ToUpperInvariant();
        return Hash("REBIRTH.camper.station.save.v1\n"+root);
    }
    private static bool Hex(string value)=>value!=null&&value.Length==64&&value.All(Uri.IsHexDigit);
    public static bool TryCreate(RebirthStationGridAdmission admission,int queuedOwner,
        RebirthCamperStationSaveEvidence.Publication proof,out RebirthCamperStationPublicationRecord record)
    {
        record=null;
        if(!RebirthCamperQueuedPublicationMatch.Matches(proof,admission,queuedOwner))return false;
        try
        {
            var node=new XElement("camperStationPublication",new XAttribute("version",1),
                new XAttribute("phase","nativeVehicleQueuedPublished"),new XAttribute("job",admission.JobId),
                new XAttribute("creation",admission.CreationId),new XAttribute("admission",AdmissionHash(admission)),
                new XAttribute("stationAddress",proof.Address.ToString()),new XAttribute("stationSnapshot",proof.StationDigest),
                new XAttribute("entitySnapshot",proof.VehicleEntityDigest),new XAttribute("bagSnapshot",proof.VehicleBagDigest),
                new XAttribute("queuedOwner",queuedOwner),new XAttribute("saveScope",SaveHash(proof.SaveDirectory)),
                new XAttribute("vehicleId",proof.VehicleEntityId),new XAttribute("vehicleClass",proof.VehicleClass));
            return TryRead(node,admission,out record);
        }
        catch{return false;}
    }
    public static bool TryRead(XElement node,RebirthStationGridAdmission admission,out RebirthCamperStationPublicationRecord record)
    {
        record=null;
        bool relocated=(string)node?.Attribute("version")=="2";
        if(node==null||admission==null||!admission.IsPublicationAttempted||node.Name!="camperStationPublication"
            ||node.HasElements||node.Attributes().Count()!=(relocated?17:13)||node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))
            ||(!relocated&&(string)node.Attribute("version")!="1")||(string)node.Attribute("phase")!="nativeVehicleQueuedPublished"
            ||(string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId
            ||(string)node.Attribute("admission")!=AdmissionHash(admission)
            ||!admission.TryGetMobileAddress(out var expected)
            ||!RebirthCamperStationAddress.TryParse((string)node.Attribute("stationAddress"),out var actual)||!expected.Equals(actual))return false;
        try
        {
            if(!relocated&&(string)admission.Write().Attribute("version")=="7")return false;
            if(relocated)
            {
                var wrapper=admission.Write();
                if((string)wrapper.Attribute("version")!="7")return false;
                var parentAdmission=wrapper.Element("parentAdmission")?.Elements().SingleOrDefault();
                var parentPublication=wrapper.Element("parentPublication")?.Elements().SingleOrDefault();
                if(parentAdmission==null||parentPublication==null
                    ||(string)node.Attribute("parentAdmission")!=Hash(parentAdmission.ToString(SaveOptions.DisableFormatting))
                    ||(string)node.Attribute("parentPublication")!=Hash(parentPublication.ToString(SaveOptions.DisableFormatting))
                    ||(string)node.Attribute("removal")!=(string)wrapper.Attribute("removal")
                    ||(string)node.Attribute("installation")!=(string)wrapper.Attribute("installation"))return false;
            }
            foreach(string field in new[]{"stationSnapshot","entitySnapshot","bagSnapshot","saveScope"})
                if(!Hex((string)node.Attribute(field)))return false;
            if((int)node.Attribute("queuedOwner")<=0||(int)node.Attribute("vehicleId")<=0||(int)node.Attribute("vehicleClass")<0)return false;
            record=new RebirthCamperStationPublicationRecord(node);return true;
        }
        catch{return false;}
    }
    internal static bool TryCreateRelocated(RebirthCamperStationRelocationCorrelation correlation,
        RebirthStationGridAdmission moved,out RebirthCamperStationPublicationRecord record)
    {
        record=null;
        if(correlation==null||moved==null)return false;
        try
        {
            var wrapper=moved.Write();
            var proof=correlation.DestinationProof;
            int owner=(int)correlation.OriginalPublication.Write().Attribute("queuedOwner");
            if((string)wrapper.Attribute("version")!="7"
                ||(string)wrapper.Attribute("destination")!=correlation.Destination.ToString()
                ||(string)wrapper.Attribute("removal")!=correlation.RemovalOperation.ToString("N")
                ||(string)wrapper.Attribute("installation")!=correlation.InstallationOperation.ToString("N")
                ||(string)wrapper.Attribute("removalAfter")!=correlation.RemovalAfterHash
                ||(string)wrapper.Attribute("installationAfter")!=correlation.InstallationAfterHash
                ||wrapper.Element("parentAdmission")?.Elements().Single().ToString(SaveOptions.DisableFormatting)
                    !=correlation.OriginalAdmission.Write().ToString(SaveOptions.DisableFormatting)
                ||wrapper.Element("parentPublication")?.Elements().Single().ToString(SaveOptions.DisableFormatting)
                    !=correlation.OriginalPublication.Write().ToString(SaveOptions.DisableFormatting)
                ||!RebirthCamperQueuedPublicationMatch.MatchesRestored(proof,moved,owner))return false;
            var node=correlation.OriginalPublication.Write();
            node.SetAttributeValue("version",2);
            node.SetAttributeValue("admission",AdmissionHash(moved));
            node.SetAttributeValue("stationAddress",proof.Address.ToString());
            node.SetAttributeValue("stationSnapshot",proof.StationDigest);
            node.SetAttributeValue("entitySnapshot",proof.VehicleEntityDigest);
            node.SetAttributeValue("bagSnapshot",proof.VehicleBagDigest);
            node.SetAttributeValue("saveScope",SaveHash(proof.SaveDirectory));
            node.SetAttributeValue("vehicleId",proof.VehicleEntityId);
            node.SetAttributeValue("vehicleClass",proof.VehicleClass);
            node.SetAttributeValue("parentAdmission",AdmissionHash(correlation.OriginalAdmission));
            node.SetAttributeValue("parentPublication",Hash(correlation.OriginalPublication.Write().ToString(SaveOptions.DisableFormatting)));
            node.SetAttributeValue("removal",wrapper.Attribute("removal")?.Value);
            node.SetAttributeValue("installation",wrapper.Attribute("installation")?.Value);
            return TryRead(node,moved,out record);
        }
        catch{return false;}
    }
    public bool MatchesSaveScope(string directory)
    {try{return (string)image.Attribute("saveScope")==SaveHash(directory);}catch{return false;}}
    public static XElement WriteAll(IDictionary<string,RebirthCamperStationPublicationRecord> records,
        IDictionary<string,RebirthStationGridAdmission> admissions)
    {
        if(records==null||records.Count>64)throw new InvalidDataException("Invalid mobile station publication count.");
        var section=new XElement("camperStationPublications",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var admission)
                ||!TryRead(pair.Value.image,admission,out var valid))throw new InvalidDataException("Invalid mobile publication binding.");
            section.Add(valid.Write());
        }
        return section;
    }
    public static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        out Dictionary<string,RebirthCamperStationPublicationRecord> records)
    {
        records=new Dictionary<string,RebirthCamperStationPublicationRecord>(StringComparer.Ordinal);
        var sections=progression.Elements("camperStationPublications").ToArray();
        if(sections.Length==0)return true;
        if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"
            ||sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
        foreach(var node in sections[0].Elements())
        {
            string job=(string)node.Attribute("job");
            if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var admission)
                ||!TryRead(node,admission,out var record))return false;
            records.Add(job,record);
        }
        return true;
    }
}
