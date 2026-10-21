using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static string RetiredReceipt(Guid job)=>"RebirthPlacedRetiredV1_"+job.ToString("N");
    private static XElement ClientUsedHeader(ClientJob job,ClientPaid paid,string proof)=>new XElement("placedClientUsed",new XAttribute("version",1),
        new XAttribute("job",job.Original.Operation.ToString("N")),new XAttribute("original",job.Original.Digest),new XAttribute("source",paid.Source.ToString("N")),
        new XAttribute("binding",paid.Binding),new XAttribute("world",job.Original.World),new XAttribute("owner",job.Original.Owner),
        new XAttribute("creation",job.Original.Creation),new XAttribute("deaths",job.Original.Deaths),new XAttribute("proof",proof),
        new XAttribute("permission",RebirthRemoteResourceDurableJournal.Digest(paid.Canonical)));
    private static bool UsedHeaderCurrent(EntityPlayerLocal player,ClientJob job,ClientPaid paid,byte[] header,bool terminalOnly=false)
    {
        if(!RebirthRemoteResourceClientOriginal.Current(player,job.Original)||!ReferenceEquals(job.Paid,paid)||
            (!terminalOnly&&!RebirthRemoteResourcePlacedStationClientCustody.OwnsOriginal(player,job.Original))||
            player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(job.Original.Operation))!=2f||
            !paid.After.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
        var record=SmallPlacedJournal(job.Original.Operation,SmallJournalDomain.ClientUsed).Read(job.Original.Operation);
        return record!=null&&record.Vehicle==paid.Source&&record.OwnerCreation==job.Original.Creation&&record.Revision==0&&
            (record.State==RebirthCamperTransferJournal.Phase.Uncertain||record.State==RebirthCamperTransferJournal.Phase.Applied)&&
            record.Before.SequenceEqual(header)&&record.After.SequenceEqual(header);
    }
    private static void AcceptUsedResult(EntityPlayerLocal player,ClientJob job,XElement response)
    {
        if(response.Name!="placedStationResult"||(response.Attributes().Count()!=11&&response.Attributes().Count()!=12&&response.Attributes().Count()!=13)||
            (int?)response.Attribute("phase")!=4||(int?)response.Attribute("outcome")!=1||(bool?)response.Attribute("permission")!=false||
            (bool?)response.Attribute("debitPermission")!=false)return;
        string proof=(string)response.Attribute("ownerProof");
        if(proof?.Length!=64||proof.Any(value=>!(value>='0'&&value<='9'||value>='a'&&value<='f'))||
            response.Attribute("retired")!=null&&(bool?)response.Attribute("retired")==null||
            response.Attribute("released")!=null&&((bool?)response.Attribute("released")!=true||(bool?)response.Attribute("retired")!=true))return;
        var permission=new XElement(response);permission.Attribute("retired")?.Remove();permission.Attribute("released")?.Remove();permission.SetAttributeValue("phase",2);
        permission.SetAttributeValue("permission",true);permission.SetAttributeValue("ownerProof","");
        bool terminalOnly=job.Entry==null||!RebirthRemoteResourcePlacedStationClientCustody.OwnsOriginal(player,job.Original);
        if(!TryAcceptClientPermission(player,job,permission,out var paid,terminalOnly)||
            player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(job.Original.Operation))!=2f||
            !paid.After.Matches(RebirthCamperEnergyActorImage.Capture(player)))return;
        byte[] header=Encoding.UTF8.GetBytes(ClientUsedHeader(job,paid,proof).ToString(SaveOptions.DisableFormatting));if(header.Length>MaximumApplicationCommand)return;
        var journal=SmallPlacedJournal(job.Original.Operation,SmallJournalDomain.ClientUsed);
        var record=journal.Prepare(job.Original.Operation,paid.Source,job.Original.Creation,0,header,header);
        if(!record.Before.SequenceEqual(header)||!record.After.SequenceEqual(header))return;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain&&record.State!=RebirthCamperTransferJournal.Phase.Applied)return;
        bool Current()=>UsedHeaderCurrent(player,job,paid,header,terminalOnly);
        if(!Current())return;
        if(!terminalOnly&&(!RebirthRemoteResourcePlacedStationQueue.TryDecode(response.Element("queue").Value,out var queue)||
            !RebirthRemoteResourcePlacedStationClientCustody.HydrateUsed(player,job.Entry,job.Original,Convert.FromBase64String(response.Element("afterTile").Value),queue,Current)))return;
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        if(record.State!=RebirthCamperTransferJournal.Phase.Applied||!Current())return;
        // Financial SourceRoot4 is already used. This separate native scalar
        // acknowledges the saved terminal view; it grants no owner item write.
        if(player.Buffs.GetCustomVar(RetiredReceipt(job.Original.Operation))!=2f)player.Buffs.SetCustomVar(RetiredReceipt(job.Original.Operation),1f,true);
        GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
        job.Stage=2;job.Next=0;
        if((bool?)response.Attribute("retired")==true)
        {
            var terminal=SmallPlacedJournal(job.Original.Operation,SmallJournalDomain.ClientRetired);
            var retired=terminal.Prepare(job.Original.Operation,paid.Source,job.Original.Creation,0,header,header);
            if(retired.Vehicle!=paid.Source||retired.OwnerCreation!=job.Original.Creation||retired.Revision!=0||!retired.Before.SequenceEqual(header)||!retired.After.SequenceEqual(header))return;
            if(retired.State==RebirthCamperTransferJournal.Phase.Prepared)retired=terminal.Advance(retired.Operation,retired.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(retired.State==RebirthCamperTransferJournal.Phase.Uncertain)retired=terminal.Advance(retired.Operation,retired.State,RebirthCamperTransferJournal.Phase.Applied);
            if(retired.State!=RebirthCamperTransferJournal.Phase.Applied||!Current())return;
            // Saved native2 means this client actually received the host's
            // retirement ACK and durably mirrored its full financial proof.
            // Host observes it before retiring the original graph allocation.
            player.Buffs.SetCustomVar(RetiredReceipt(job.Original.Operation),2f,true);
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);
            if((bool?)response.Attribute("released")!=true)return;
            if(!RebirthRemoteResourceClientOriginal.CompletePlacedTerminal(player,job.Original,paid.Source,paid.Binding,proof)||
                !RebirthRemoteResourceOwnerAction.ReleasePlacedTerminal(player,job.Original,paid.Source,paid.Binding,proof)||
                !RebirthRemoteResourcePlacedStationClientCustody.ReleaseUsed(player,job.Original,paid.Source,paid.Binding,proof))return;
            player.Buffs.SetCustomVar(RetiredReceipt(job.Original.Operation),0f,true);player.Buffs.RemoveCustomVar(RetiredReceipt(job.Original.Operation));
            GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);clientJobs.Remove(job.Original.Operation);
        }
        // Only the separately saved native-retirement host ACK enters the
        // durable terminal mirror above. Ordinary used replies retain custody.
    }
    internal static bool HasClientRetired(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        try
        {
            if(!IsCandidate(original)||source==Guid.Empty||proof?.Length!=64)return false;
            var record=SmallPlacedJournal(original.Operation,SmallJournalDomain.ClientRetired).Read(original.Operation);
            if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=source||record.OwnerCreation!=original.Creation||record.Revision!=0||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),MaximumApplicationCommand);
            var permissionRecord=ClientPermissions().Read(original.Operation);
            if(permissionRecord==null||permissionRecord.Vehicle!=source||permissionRecord.OwnerCreation!=original.Creation||permissionRecord.Revision!=0||
                permissionRecord.State!=RebirthCamperTransferJournal.Phase.Uncertain||!permissionRecord.Before.SequenceEqual(permissionRecord.After)||
                RebirthRemoteResourceDurableJournal.Digest(new UTF8Encoding(false,true).GetString(permissionRecord.Before))!=(string)body.Attribute("permission"))return false;
            // Resolve the retained FULL immutable financial permission. The
            // small terminal reference/hash never stands in for owner/native
            // images or grants an inventory overwrite.
            var full=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(permissionRecord.Before),MaximumResultBytes);
            if(!RebirthRemoteResourcePlacedStationAllocation.TryAdmitOwnerReplyBeforeHydration(full)||full.Name!="placedStationResult"||
                (string)full.Attribute("original")!=original.Operation.ToString("N")||(string)full.Attribute("digest")!=original.Digest||
                (string)full.Attribute("source")!=source.ToString("N")||(string)full.Attribute("binding")!=binding||
                full.Element("beforeActor")?.Value!=original.Copy().Element("actorBefore").Value||
                !RebirthRemoteResourcePlacedStationReference.TryRead(full.Element("reference")?.Value,out var reference)||reference.Job!=original.Operation||reference.Original!=original.Digest||reference.Source!=source||reference.Binding!=binding||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(Convert.FromBase64String(full.Element("afterTile").Value),out _)||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(full.Element("queue").Value,out _))return false;
            RebirthRemoteResourceClientOriginal.ReadActor(full.Element("afterActor").Value);
            return body.Name=="placedClientUsed"&&body.Attributes().Count()==11&&!body.Nodes().Any()&&(int?)body.Attribute("version")==1&&
                (string)body.Attribute("job")==original.Operation.ToString("N")&&(string)body.Attribute("original")==original.Digest&&
                (string)body.Attribute("source")==source.ToString("N")&&(string)body.Attribute("binding")==binding&&(string)body.Attribute("proof")==proof&&
                (string)body.Attribute("world")==original.World&&(string)body.Attribute("owner")==original.Owner&&(string)body.Attribute("creation")==original.Creation&&
                (int?)body.Attribute("deaths")==original.Deaths&&(string)body.Attribute("permission") is string permission&&permission.Length==64;
        }
        catch{return false;}
    }
}
