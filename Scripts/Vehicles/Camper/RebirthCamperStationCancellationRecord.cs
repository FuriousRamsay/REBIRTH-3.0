using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Confirmed native mobile removal only. This record never proves refund
// delivery or authorizes recreating an inventory-changing operation.
internal sealed class RebirthCamperStationCancellationRecord
{
    private readonly XElement image;
    internal string JobId=>(string)image.Attribute("job");
    private RebirthCamperStationCancellationRecord(XElement node){image=new XElement(node);}
    internal XElement Write()=>new XElement(image);
    internal RebirthCamperStationCancellationRecord Clone()=>new RebirthCamperStationCancellationRecord(image);
    private static string Hash(XElement node)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))).Replace("-","");}
    private static bool Hex(string value)=>value!=null&&value.Length==64&&value.All(Uri.IsHexDigit);
    internal static bool TryCreate(RebirthCamperStationCancellationCheckpoint checkpoint,
        RebirthCamperStationCancellationRemoval removal,out RebirthCamperStationCancellationRecord record)
    {
        record=null;
        try
        {
            if(!ThreadManager.IsMainThread()||checkpoint==null||removal==null||
                !removal.TryGetPublication(checkpoint,out var proof))return false;
            var binding=checkpoint.Binding;var queued=binding.Publication.Write();
            var intent=checkpoint.CopyIntent();var refund=checkpoint.CopyRefund();var attempt=checkpoint.CopyAttempt();
            var saved=proof.RestoreStationSnapshot();
            if(!binding.Address.Equals(proof.Address)||!binding.Publication.MatchesSaveScope(proof.SaveDirectory)||
                proof.VehicleEntityId<=0||proof.VehicleEntityId!=binding.Vehicle.entityId||
                proof.VehicleClass!=(int)queued.Attribute("vehicleClass")||!checkpoint.MatchesPlannedQueue(saved.Queue)||
                checkpoint.StationDigest(saved)!=proof.RecoveryDigest())return false;
            var node=new XElement("camperStationCancellation",new XAttribute("version",1),new XAttribute("phase","nativeVehicleCancelledPublished"),
                new XAttribute("job",binding.Admission.JobId),new XAttribute("creation",binding.Admission.CreationId),
                new XAttribute("admission",Hash(binding.Admission.Write())),new XAttribute("queued",Hash(queued)),
                new XAttribute("intent",Hash(intent.Write())),new XAttribute("refund",Hash(refund.Write())),new XAttribute("attempt",Hash(attempt.Write())),
                new XAttribute("stationAddress",proof.Address.ToString()),new XAttribute("stationRecovery",proof.RecoveryDigest()),
                new XAttribute("stationSnapshot",proof.StationDigest),new XAttribute("entitySnapshot",proof.VehicleEntityDigest),new XAttribute("bagSnapshot",proof.VehicleBagDigest),
                new XAttribute("nativeVehicleId",proof.VehicleEntityId),new XAttribute("vehicleClass",proof.VehicleClass),
                new XAttribute("saveScope",(string)queued.Attribute("saveScope")),new XAttribute("queuedOwner",(int)queued.Attribute("queuedOwner")),
                new XAttribute("cancelledOwner",(int)intent.Write().Attribute("actor")));
            return removal.IsConfirmedFor(checkpoint)&&TryRead(node,binding.Admission,binding.Publication,intent,refund,attempt,out record);
        }
        catch{return false;}
    }
    internal static bool TryRead(XElement node,RebirthStationGridAdmission admission,RebirthCamperStationPublicationRecord queued,
        RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,RebirthStationCancellationAttempt attempt,
        out RebirthCamperStationCancellationRecord record)
    {
        record=null;
        try
        {
            if(node==null||admission==null||queued==null||intent==null||intent.IsCompletion||
                !RebirthCamperStationPublicationRecord.TryRead(queued.Write(),admission,out _)||
                !RebirthStationCancellationAttempt.TryRead(attempt?.Write(),admission,intent,refund,out _)||
                node.Name!="camperStationCancellation"||node.HasElements||node.Attributes().Count()!=19||
                node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
                (string)node.Attribute("version")!="1"||(string)node.Attribute("phase")!="nativeVehicleCancelledPublished"||
                (string)node.Attribute("job")!=admission.JobId||(string)node.Attribute("creation")!=admission.CreationId||
                (string)node.Attribute("admission")!=Hash(admission.Write())||(string)node.Attribute("queued")!=Hash(queued.Write())||
                (string)node.Attribute("intent")!=Hash(intent.Write())||(string)node.Attribute("refund")!=Hash(refund.Write())||
                (string)node.Attribute("attempt")!=Hash(attempt.Write())||
                !admission.TryGetMobileAddress(out var expected)||
                !RebirthCamperStationAddress.TryParse((string)node.Attribute("stationAddress"),out var actual)||!expected.Equals(actual)||
                (int)node.Attribute("nativeVehicleId")<=0)return false;
            foreach(var name in new[]{"stationRecovery","stationSnapshot","entitySnapshot","bagSnapshot","saveScope"})
                if(!Hex((string)node.Attribute(name)))return false;
            var parent=queued.Write();
            if((int)node.Attribute("vehicleClass")!=(int)parent.Attribute("vehicleClass")||
                (string)node.Attribute("saveScope")!=(string)parent.Attribute("saveScope")||
                (int)node.Attribute("queuedOwner")!=(int)parent.Attribute("queuedOwner")||
                (int)node.Attribute("cancelledOwner")!=(int)intent.Write().Attribute("actor"))return false;
            record=new RebirthCamperStationCancellationRecord(node);return true;
        }
        catch{return false;}
    }
    // Correlation filter only; recovery separately authenticates the current
    // unique vehicle, owner and station session and reads the primary save.
    internal bool MatchesRecoveryImage(RebirthCamperStationSaveEvidence.Publication proof,RebirthCamperStationPublicationRecord queued)
    {
        try{return ThreadManager.IsMainThread()&&proof!=null&&queued!=null&&Hash(queued.Write())==(string)image.Attribute("queued")&&
            queued.MatchesSaveScope(proof.SaveDirectory)&&proof.Address.ToString()==(string)image.Attribute("stationAddress")&&
            proof.VehicleEntityId>0&&proof.VehicleClass==(int)image.Attribute("vehicleClass")&&proof.RecoveryDigest()==(string)image.Attribute("stationRecovery");}
        catch{return false;}
    }
    // Current native observation of continued cancellation, not reproduction of
    // the historical whole-station image. Unrelated station work may advance.
    internal bool MatchesCurrentCancellation(RebirthCamperStationSaveEvidence.Publication proof,RebirthCamperStationPublicationRecord queued)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||proof==null||queued==null||Hash(queued.Write())!=(string)image.Attribute("queued")||
                !queued.MatchesSaveScope(proof.SaveDirectory)||proof.Address.ToString()!=(string)image.Attribute("stationAddress")||
                proof.VehicleEntityId<=0||proof.VehicleClass!=(int)image.Attribute("vehicleClass"))return false;
            var saved=proof.RestoreStationSnapshot();
            if(saved?.Queue==null||!RebirthStationCompletionReceipt.HasNoJobReceipt(saved.CraftCompleteList,JobId))return false;
            foreach(var row in saved.Queue)
                if(row==null||RebirthStationGridQueue.IsMarked(row.Recipe)&&
                    (!RebirthStationGridQueue.TryGetJobId(row.Recipe,out var job)||job==JobId))return false;
            return true;
        }
        catch{return false;}
    }
    internal static XElement WriteAll(IDictionary<string,RebirthCamperStationCancellationRecord> records,
        IDictionary<string,RebirthStationGridAdmission> admissions,IDictionary<string,RebirthCamperStationPublicationRecord> queued,
        IDictionary<string,RebirthStationTerminalIntent> intents,IDictionary<string,RebirthStationCancellationRefund> refunds,
        IDictionary<string,RebirthStationCancellationAttempt> attempts)
    {
        if(records==null||records.Count>64)throw new InvalidDataException("Invalid camper cancellations.");
        var section=new XElement("camperStationCancellations",new XAttribute("version",1));
        foreach(var pair in records.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            if(pair.Value==null||pair.Key!=pair.Value.JobId||!admissions.TryGetValue(pair.Key,out var a)||!queued.TryGetValue(pair.Key,out var q)||
                !intents.TryGetValue(pair.Key,out var i)||!refunds.TryGetValue(pair.Key,out var r)||!attempts.TryGetValue(pair.Key,out var t)||
                !TryRead(pair.Value.image,a,q,i,r,t,out var valid))throw new InvalidDataException("Invalid camper cancellation binding.");
            section.Add(valid.Write());
        }
        return section;
    }
    internal static bool ReadAll(XElement progression,IDictionary<string,RebirthStationGridAdmission> admissions,
        IDictionary<string,RebirthCamperStationPublicationRecord> queued,IDictionary<string,RebirthStationTerminalIntent> intents,
        IDictionary<string,RebirthStationCancellationRefund> refunds,IDictionary<string,RebirthStationCancellationAttempt> attempts,
        out Dictionary<string,RebirthCamperStationCancellationRecord> records)
    {
        records=new Dictionary<string,RebirthCamperStationCancellationRecord>(StringComparer.Ordinal);
        try
        {
            var sections=progression.Elements("camperStationCancellations").ToArray();if(sections.Length==0)return true;
            if(sections.Length!=1||sections[0].Attributes().Count()!=1||(string)sections[0].Attribute("version")!="1"||
                sections[0].Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))))return false;
            foreach(var node in sections[0].Elements())
            {
                string job=(string)node.Attribute("job");
                if(records.Count>=64||job==null||records.ContainsKey(job)||!admissions.TryGetValue(job,out var a)||!queued.TryGetValue(job,out var q)||
                    !intents.TryGetValue(job,out var i)||!refunds.TryGetValue(job,out var r)||!attempts.TryGetValue(job,out var t)||
                    !TryRead(node,a,q,i,r,t,out var valid))return false;
                records.Add(job,valid);
            }
            return true;
        }
        catch{return false;}
    }
}
