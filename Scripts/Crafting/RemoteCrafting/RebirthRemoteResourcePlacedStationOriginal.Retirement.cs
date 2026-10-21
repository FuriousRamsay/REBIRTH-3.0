using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static bool ObserveRetiredOwner(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,XElement reply,out bool acknowledged)
    {
        acknowledged=false;
        if(!RebirthRemoteResourcePlacedStationPreparation.TryReadUsedAllocation(actor,root,out _,out var allocation)||
            !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return false;
        using(scratch)
        {
            float retired=0;
            bool observed=RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&
                RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)&&saved.id==actor.entityId&&saved.deaths==original.Deaths&&
                actual.Matches(RebirthRemoteResourceClientOriginal.ReadActor(reply.Element("afterActor").Value))&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float paid,out bool hasPaid)&&hasPaid&&paid==2f&&
                RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RetiredReceipt(original.Operation),out retired,out bool hasRetired)&&hasRetired&&(retired==1f||retired==2f)&&scratch.Current;
            if(observed)acknowledged=retired==2f;return observed;
        }
    }
    // The result is already backed by the full immutable artifact and actual
    // saved After publication. Retirement adds a same-primary native receipt;
    // neither the request stage nor a transport ACK is retirement authority.
    private static string RetireUsedReply(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,string text)
    {
        try
        {
            var reply=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
            if(!RebirthRemoteResourcePlacedStationPreparation.TryReadUsedAllocation(actor,root,out var original,out _)||!ServerScope(actor,original)||!ObserveRetiredOwner(actor,root,original,reply,out bool acknowledged))return text;
            var command=new XElement("placedRetirement",new XAttribute("version",1),new XAttribute("job",original.Operation.ToString("N")),
                new XAttribute("original",original.Digest),new XAttribute("source",root.Operation.ToString("N")),new XAttribute("binding",root.Binding),
                new XAttribute("proof",(string)reply.Attribute("ownerProof")),new XAttribute("reference",reply.Element("reference").Value));
            byte[] bytes=Encoding.UTF8.GetBytes(command.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumApplicationCommand)return text;
            var journal=SmallPlacedJournal(original.Operation,SmallJournalDomain.Retirement);
            var record=journal.Prepare(original.Operation,root.Operation,original.Creation,0,bytes,bytes);
            if(record.Vehicle!=root.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return text;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return text;
            reply.SetAttributeValue("retired",true);
            if(acknowledged&&SaveFinalAcknowledgement(root,original,command)&&
                RebirthRemoteResourcePlacedStationCustody.ReleaseUsed(actor,root,original,(string)reply.Attribute("ownerProof")))
            {
                // Full artifacts remain durable for later reply/cancellation.
                // Only private, positively authenticated receipt2 reaches this
                // typed witness. No request bit or image equality mints it.
                if(TerminalArchive.TryCreate(actor,root,original,reply,out var terminal)&&RebirthRemoteResourcePlacedStationPreparation.RetireTerminal(terminal))
                {readySources.Remove(root.Operation);reply.SetAttributeValue("released",true);}
            }
            return reply.ToString(SaveOptions.DisableFormatting);
        }
        catch{return text;}
    }
    private static bool SaveFinalAcknowledgement(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,XElement retirement)
    {
        var body=new XElement(retirement);body.Name="placedFinalAck";
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumApplicationCommand)return false;
        var journal=SmallPlacedJournal(original.Operation,SmallJournalDomain.FinalAck);var record=journal.Prepare(original.Operation,root.Operation,original.Creation,0,bytes,bytes);
        if(record.Vehicle!=root.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    // Short-lived typed capability. It is never stored on a lease/receipt: the
    // retained financial graphs are dropped only while the full original and
    // publication evidence remain covered by their existing allocation.
    internal sealed class TerminalArchive
    {
        internal RebirthRemoteResourceDurableJournal.Record Root {get;private set;}
        private readonly EntityPlayer actor;private readonly RebirthRemoteResourceClientOriginal.Intent original;private readonly string proof;
        private TerminalArchive(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,string proof)
        {this.actor=actor;Root=root;this.original=original;this.proof=proof;}
        internal static bool TryCreate(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,XElement reply,out TerminalArchive receipt)
        {
            receipt=null;
            try
            {
                if(root==null||original==null||reply==null)return false;string proof=(string)reply.Attribute("ownerProof");
                if(!HasSavedRetirement(actor,root,original,proof)||!FinalAcknowledgementCurrent(root,original,proof)||
                    !ObserveRetiredOwner(actor,root,original,reply,out bool acknowledged)||!acknowledged)return false;
                receipt=new TerminalArchive(actor,root,original,proof);return true;
            }
            catch{return false;}
        }
        internal bool Current=>HasSavedRetirement(actor,Root,original,proof)&&FinalAcknowledgementCurrent(Root,original,proof);
        internal void DropReadGraphs(){Root=null;}
    }
    private static bool FinalAcknowledgementCurrent(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,string proof)
    {
        var record=SmallPlacedJournal(original.Operation,SmallJournalDomain.FinalAck).Read(original.Operation);
        if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=root.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||!record.Before.SequenceEqual(record.After))return false;
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),MaximumApplicationCommand);
        return body.Name=="placedFinalAck"&&body.Attributes().Count()==7&&!body.Nodes().Any()&&(int?)body.Attribute("version")==1&&
            (string)body.Attribute("job")==original.Operation.ToString("N")&&(string)body.Attribute("original")==original.Digest&&
            (string)body.Attribute("source")==root.Operation.ToString("N")&&(string)body.Attribute("binding")==root.Binding&&(string)body.Attribute("proof")==proof&&
            readySources.TryGetValue(root.Operation,out var ready)&&(string)body.Attribute("reference")==ready.Reference;
    }
    internal static bool RetireTerminalObservers(TerminalArchive receipt)
    {
        if(receipt?.Current!=true)return false;
        if(worldApplications.TryGetValue(receipt.Root.Operation,out var application))
        {
            application.Watch?.Dispose();application.Watch=null;
            worldApplications.Remove(receipt.Root.Operation);
            application.Actor=null;application.Root=null;application.Claim=null;application.Artifact=null;
            application.Reference=null;application.Allocation=null;
            application.Command=null;application.After=null;application.OwnerActor=null;application.OwnerBuffs=null;
        }
        if(readySources.TryGetValue(receipt.Root.Operation,out var ready))
        {readySources.Remove(receipt.Root.Operation);ready.Actor=null;ready.Claim=null;}
        receipt.DropReadGraphs();
        return true;
    }
    internal static bool HasSavedRetirement(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,string proof)
    {
        try
        {
            if(root?.Phase!=4||(string)root.Copy().Attribute("disposition")!="used"||!ServerScope(actor,original)||
                !TryObservePlacedPublication(actor,root,out string actual)||actual!=proof||(string)root.Copy().Attribute("ownerProof")!=proof)return false;
            var record=SmallPlacedJournal(original.Operation,SmallJournalDomain.Retirement).Read(original.Operation);
            if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Applied||record.Vehicle!=root.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),MaximumApplicationCommand);
            return body.Name=="placedRetirement"&&body.Attributes().Count()==7&&!body.Nodes().Any()&&(int?)body.Attribute("version")==1&&
                (string)body.Attribute("job")==original.Operation.ToString("N")&&(string)body.Attribute("original")==original.Digest&&
                (string)body.Attribute("source")==root.Operation.ToString("N")&&(string)body.Attribute("binding")==root.Binding&&
                (string)body.Attribute("proof")==proof&&readySources.TryGetValue(root.Operation,out var ready)&&(string)body.Attribute("reference")==ready.Reference;
        }
        catch{return false;}
    }
}
