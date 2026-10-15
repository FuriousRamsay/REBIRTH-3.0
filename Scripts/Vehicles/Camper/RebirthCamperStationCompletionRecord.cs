using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Immutable confirmed mobile output publication. Parsing does not authorize
// rewards, output replay, current vehicle persistence or recovery settlement.
public sealed class RebirthCamperStationCompletionRecord
{
    private readonly XElement image;
    public string JobId=>(string)image.Attribute("job");
    public bool HasRecoveryImage=>(string)image.Attribute("version")=="2"||(string)image.Attribute("version")=="3";
    private RebirthCamperStationCompletionRecord(XElement node){image=new XElement(node);}
    public XElement Write()=>new XElement(image);
    public RebirthCamperStationCompletionRecord Clone()=>new RebirthCamperStationCompletionRecord(image);
    private static string Hash(XElement node)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))).Replace("-","");}
    private static bool Hex(string text)=>text!=null&&text.Length==64&&text.All(Uri.IsHexDigit);
    internal static bool TryCreate(RebirthCamperStationCompletionCapture.SuccessfulOutput completed,
        RebirthCamperStationSaveEvidence.Publication proof,out RebirthCamperStationCompletionRecord record)
    {
        record=null;
        try
        {
            if(!ThreadManager.IsMainThread()||completed==null||proof==null||!completed.MatchesLiveOutput())return false;
            var binding=completed.Binding;var queued=binding.Publication.Write();
            if(!binding.Address.Equals(proof.Address)||!binding.Publication.MatchesSaveScope(proof.SaveDirectory)||
                proof.VehicleEntityId<=0||proof.VehicleEntityId!=binding.Vehicle.entityId||
                proof.VehicleClass!=(int)queued.Attribute("vehicleClass")||!completed.MatchesSavedOutput(proof.RestoreStationSnapshot()))return false;
            var node=new XElement("camperStationCompletion",new XAttribute("version",3),new XAttribute("phase","nativeVehicleCompletedPublished"),
                new XAttribute("job",binding.Admission.JobId),new XAttribute("creation",binding.Admission.CreationId),
                new XAttribute("admission",Hash(binding.Admission.Write())),new XAttribute("queued",Hash(queued)),
                new XAttribute("stationAddress",proof.Address.ToString()),new XAttribute("stationSnapshot",proof.StationDigest),
                new XAttribute("stationRecovery",proof.RecoveryDigest()),
                new XAttribute("entitySnapshot",proof.VehicleEntityDigest),new XAttribute("bagSnapshot",proof.VehicleBagDigest),
                new XAttribute("completedOwner",(int)queued.Attribute("queuedOwner")),new XAttribute("saveScope",(string)queued.Attribute("saveScope")),
                new XAttribute("vehicleId",(int)queued.Attribute("vehicleId")),
                new XAttribute("nativeVehicleId",proof.VehicleEntityId),new XAttribute("vehicleClass",proof.VehicleClass));
            return TryRead(node,binding.Admission,binding.Publication,out record);
        }
        catch{return false;}
    }
    public static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthCamperStationPublicationRecord queued,
        out RebirthCamperStationCompletionRecord record)
    {
        record=null;
        try
        {
            string version=(string)node?.Attribute("version");
            if(node==null||admission==null||queued==null||!RebirthCamperStationPublicationRecord.TryRead(queued.Write(),admission,out _)||
                node.Name!="camperStationCompletion"||node.HasElements||
                (version!="1"&&version!="2"&&version!="3")||node.Attributes().Count()!=(version=="3"?16:version=="2"?15:14)||
                (version!="1"?!Hex((string)node.Attribute("stationRecovery")):node.Attribute("stationRecovery")!=null)||
                (version=="3"?(int?)node.Attribute("nativeVehicleId")<=0||node.Attribute("nativeVehicleId")==null:node.Attribute("nativeVehicleId")!=null)||
                node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                (string)node.Attribute("phase")!="nativeVehicleCompletedPublished"||
                (string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
                (string)node.Attribute("admission")!=Hash(admission.Write())||(string)node.Attribute("queued")!=Hash(queued.Write())||
                !admission.TryGetMobileAddress(out var expected)||
                !RebirthCamperStationAddress.TryParse((string)node.Attribute("stationAddress"),out var actual)||!expected.Equals(actual))return false;
            var publication=queued.Write();
            foreach(var field in new[]{"stationSnapshot","entitySnapshot","bagSnapshot","saveScope"})if(!Hex((string)node.Attribute(field)))return false;
            if((int)node.Attribute("completedOwner")!=(int)publication.Attribute("queuedOwner")||
                (string)node.Attribute("saveScope")!=(string)publication.Attribute("saveScope")||
                (int)node.Attribute("vehicleId")!=(int)publication.Attribute("vehicleId")||
                (int)node.Attribute("vehicleClass")!=(int)publication.Attribute("vehicleClass"))return false;
            record=new RebirthCamperStationCompletionRecord(node);return true;
        }
        catch{return false;}
    }
    // A current primary native publication is required by the caller. This
    // comparison is only a binding filter; it grants no cold owner authority.
    internal bool MatchesRecoveryImage(RebirthCamperStationSaveEvidence.Publication proof,RebirthCamperStationPublicationRecord queued)
    {
        try
        {
            return ThreadManager.IsMainThread()&&HasRecoveryImage&&proof!=null&&queued!=null&&
                Hash(queued.Write())==(string)image.Attribute("queued")&&queued.MatchesSaveScope(proof.SaveDirectory)&&
                proof.Address.ToString()==(string)image.Attribute("stationAddress")&&
                proof.VehicleEntityId>0&&proof.VehicleClass==(int)image.Attribute("vehicleClass")&&
                proof.RecoveryDigest()==(string)image.Attribute("stationRecovery");
        }
        catch{return false;}
    }
    public static XElement WriteAll(IDictionary<string,RebirthCamperStationCompletionRecord> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthCamperStationPublicationRecord> queued)
    {
        if(records==null||admissions==null||queued==null||records.Count>64)throw new InvalidDataException("Invalid camper completions.");
        var section=new XElement("camperStationCompletions",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var admission)||
                !queued.TryGetValue(pair.Key,out var published)||!TryRead(pair.Value.image,admission,published,out var valid))
                throw new InvalidDataException("Invalid camper completion binding.");
            section.Add(valid.Write());
        }
        return section;
    }
    public static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthCamperStationPublicationRecord> queued,out Dictionary<string,RebirthCamperStationCompletionRecord> records)
    {
        records=new Dictionary<string,RebirthCamperStationCompletionRecord>(StringComparer.Ordinal);
        try
        {
            var sections=progression.Elements("camperStationCompletions").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var admission)||
                    !queued.TryGetValue(job,out var publication)||!TryRead(node,admission,publication,out var valid))return false;
                records.Add(job,valid);
            }
            return true;
        }
        catch{return false;}
    }
}
