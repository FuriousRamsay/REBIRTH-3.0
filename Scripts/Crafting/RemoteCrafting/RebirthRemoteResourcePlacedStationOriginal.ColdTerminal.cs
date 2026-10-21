using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Root4 terminal observation/reply only. This path cannot reconstruct a
    // target/source hold, grant owner slots, or replay any native operation.
    private static bool TryRouteColdTerminal(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,int stage)
    {
        if(stage<1||stage>2||!ServerScope(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
            !RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
        Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        if(RebirthRemoteResourcePlacedStationPreparation.HasRetainedAttempt(source)||!RebirthRemoteResourcePlacedStationArtifactStore.TryReadHeader(source,out _))return false;
        if(!RebirthRemoteResourcePlacedStationColdRead.TryOpen(actor,source,out var cold,out _))return true;
        using(cold)
        {
            var root=cold.Source;var full=cold.Artifact;var allocation=cold.Allocation;
            if(TryColdNoPayment(actor,original,cold))return true;
            if(full.Original.Digest!=original.Digest||!XNode.DeepEquals(full.Original.Copy(),original.Copy())||(root.Phase!=2&&root.Phase!=4)||
                (root.Phase==4&&(string)root.Copy().Attribute("disposition")!="used")||(int?)root.Copy().Attribute("ownerRejecting")==1||root.Copy().Attribute("ownerNoUseProof")!=null||
                !RebirthRemoteResourcePlacedStationReference.TryRead(root.Copy().Element("ownerAction")?.Element("original")?.Value,out var reference)||
                reference.Artifact!=cold.ArtifactDigest||reference.Original!=original.Digest||reference.Job!=original.Operation||reference.Source!=source||
                reference.Binding!=root.Binding||reference.Owner!=root.Owner||reference.World!=root.World||reference.Creation!=root.Creation||reference.Birth!=full.Birth||
                !ObserveColdAccepted(cold,reference)||!TryReadCompletion(root,full,reference,null,out string proof,false,allocation))return true;
            if(!PublishColdSavedEarnedOwner(actor,cold,proof))return true;
            if(root.Phase==2)
            {
                // Actual typed Applied command + full saved native After and
                // receipt2 already prove usage. This records that historical
                // fact; it grants no native/source/owner effect or reentry.
                bool Current()=>cold.Current&&ServerScope(actor,original)&&ReferenceEquals(cold.Source,root)&&full.Original.Digest==original.Digest;
                if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.HistoricalSource,out var history))return true;
                RebirthRemoteResourceDurableJournal.Record used;
                using(history)
                {
                    if(!RebirthRemoteResourceDurableJournal.TryAdvancePlacedBounded(root,image=>{image.SetAttributeValue("phase",4);image.SetAttributeValue("disposition","used");image.SetAttributeValue("ownerProof",proof);},
                        Current,allocation.Header,out used))return true;
                }
                if(!TryReadCompletion(used,full,reference,null,out string savedProof,false,allocation)||savedProof!=proof)return true;root=used;
            }
            if((string)root.Copy().Attribute("ownerProof")!=proof)return true;
            var reply=ReplyBody(root,full,reference,false,false);
            if(!FitsReply(reply)||!RebirthRemoteResourcePlacedStationAllocation.TryAdmitOwnerReplyBeforeHydration(reply,out long workspace)||
                !allocation.ReserveReadWorkspace(checked(full.ReadWorkspace+workspace))||
                !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return true;
            using(scratch)
            {
                if(!TrySavedPaidOwner(actor,full,out _,out _)||!cold.Current||!ServerScope(actor,original))return true;
                if(stage==2&&RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)&&saved.id==actor.entityId&&saved.deaths==original.Deaths&&
                    actual.Matches(RebirthRemoteResourceClientOriginal.ReadActor(full.Copy().Element("plan").Element("actorAfter").Value))&&
                    RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RetiredReceipt(original.Operation),out float retired,out bool present)&&present&&(retired==1f||retired==2f))
                {
                    if(!PrepareColdRetirement(root,original,reference,proof))return true;reply.SetAttributeValue("retired",true);
                    if(retired==2f)
                    {
                        var command=RetirementCommand(root,original,reference,proof);
                        if(!SaveFinalAcknowledgement(root,original,command))return true;
                        reply.SetAttributeValue("released",true);
                    }
                }
                if(scratch.Current&&cold.Current)NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,reply.ToString(SaveOptions.DisableFormatting));
            }
        }
        return true;
    }
    private static bool PrepareColdRetirement(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,
        RebirthRemoteResourcePlacedStationReference.Image reference,string proof)
    {
        var body=RetirementCommand(root,original,reference,proof);
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>MaximumApplicationCommand)return false;
        var journal=SmallPlacedJournal(original.Operation,SmallJournalDomain.Retirement);var record=journal.Prepare(original.Operation,root.Operation,original.Creation,0,bytes,bytes);
        if(record.Vehicle!=root.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(record.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==RebirthCamperTransferJournal.Phase.Applied;
    }
    private static XElement RetirementCommand(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,
        RebirthRemoteResourcePlacedStationReference.Image reference,string proof)=>new XElement("placedRetirement",new XAttribute("version",1),
        new XAttribute("job",original.Operation.ToString("N")),new XAttribute("original",original.Digest),new XAttribute("source",root.Operation.ToString("N")),
        new XAttribute("binding",root.Binding),new XAttribute("proof",proof),new XAttribute("reference",reference.Encode()));
}
