using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static bool PaidSourcesCurrent(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> leases)
    {
        foreach(var source in root.Copy().Element("sources").Elements("source"))
        {
            string id=(string)source.Attribute("id"),proof=(string)source.Attribute("proof");
            if((int)source.Attribute("attempted")!=1||(int)source.Attribute("published")!=1||((int?)source.Attribute("restored")??0)!=0)return false;
            if(source.Element("npc")!=null)
            {
                if(!RebirthRemoteResourceDurableOutcome.TryReadNpcEffectPlan(actor,root.Operation,id,out var plan)||
                    !RebirthRemoteResourceDurableOutcome.TryObserveSavedNpc(plan,true,out string actual)||actual!=proof)return false;
            }
            else
            {
                if(!leases.TryGetValue(id,out var lease)||!lease.Current()||!lease.TryObserveSaved(1,out string actual)||actual!=proof||
                    !lease.ReleaseSavedDebitObservation(proof))return false;
            }
        }
        return true;
    }
    private static bool TryPaidContext(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,
        out RebirthRemoteResourceClientOriginal.Intent original,out RebirthRemoteResourcePlacedStationCustody.Claim claim,
        out RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,out RebirthRemoteResourcePlacedStationReference.Image reference)
    {
        original=null;claim=null;artifact=null;reference=null;
        BindReadyWorld();
        if(root==null||root.Phase!=2||(int)root.Copy().Attribute("outcome")!=1||(int?)root.Copy().Attribute("ownerRejecting")==1||
            root.Copy().Element("ownerRefund")!=null||root.Copy().Attribute("ownerNoUseProof")!=null||
            !RebirthRemoteResourcePlacedStationPreparation.TryReadRetained(actor,root,out var intent,out var target,out var allocation,out var sources)||
            !ServerScope(actor,intent)||!readySources.TryGetValue(root.Operation,out var ready)||!ReferenceEquals(ready.Actor,actor)||
            !ReferenceEquals(ready.Claim,target)||ready.Original!=intent.Digest||
            !RebirthRemoteResourcePlacedStationReference.TryRead(ready.Reference,out var captured)||
            !RebirthRemoteResourcePlacedStationReference.TryResolve(root,ready.Reference,allocation,out var full)||full.Original.Digest!=intent.Digest||
            !CanPublishOwnerEnvelope(root,full,captured,allocation)||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,
                RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return false;
        using(scratch)
        {
            if(!target.ExactBefore()||!ValidateInput(intent,out var position,out _,out string window,out _,out _,out _,out _,out _,out var payment,out bool turnsOn,out _)||
                !RebirthRemoteResourcePlacedStationScope.TryResolveLayout(actor,position,intent.Creation,out var station,out string actualWindow,out var actualPayment,out bool actualTurnOn)||
                !ReferenceEquals(station,target.Station)||window!=actualWindow||payment!=actualPayment||turnsOn!=actualTurnOn||!PaidSourcesCurrent(actor,root,sources)||!scratch.Current)return false;
            original=intent;claim=target;artifact=full;reference=captured;return true;
        }
    }
    private static XElement OwnerPermission(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,
        RebirthRemoteResourcePlacedStationReference.Image reference)
    {
        string proof=RebirthRemoteResourceDurableJournal.Digest("placed-owner-before|"+root.World+"|"+root.Owner+"|"+root.Creation+"|"+
            artifact.Original.Deaths+"|"+reference.Encode()+"|"+artifact.Original.Copy().Element("actorBefore").Value);
        return new XElement("ownerAction",new XAttribute("domain","native-placed-station"),new XAttribute("child",artifact.Original.Operation.ToString("N")),
            new XAttribute("nativeBeforeProof",proof),new XElement("original",reference.Encode()));
    }
    internal static bool PrepareOwnerPermission(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root)
    {
        try
        {
            if(!TryPaidContext(actor,root,out var original,out var claim,out var full,out var reference))return false;
            var permission=OwnerPermission(root,full,reference);var existing=root.Copy().Element("ownerAction");
            if(existing!=null)return XNode.DeepEquals(existing,permission);
            if(!RebirthRemoteResourcePlacedStationPreparation.TryReadRetained(actor,root,out _,out _,out var allocation,out _)||
                !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return false;
            using(scratch)
            {
            if(!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !actual.Matches(RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value))||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present||phase!=10f)return false;
            // Immutable host permission is durable BEFORE any original owner
            // payment. No remote fragments are materialized in an owner bag.
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,image=>image.Add(new XElement(permission)),
                ()=>ServerScope(actor,original)&&claim.ExactBefore(),out var admitted)&&
                XNode.DeepEquals(admitted?.Copy().Element("ownerAction"),permission);
            }
        }
        catch{return false;}
    }
    private static bool TryReplyPaid(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,out string text)
    {
        text=null;
        try
        {
            if(!TryPaidContext(actor,root,out var original,out _,out var full,out var reference)||
                !XNode.DeepEquals(root.Copy().Element("ownerAction"),OwnerPermission(root,full,reference))||
                !RebirthRemoteResourcePlacedStationPreparation.TryReadRetained(actor,root,out _,out _,out var allocation,out _)||
                !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return false;
            using(scratch)
            {
            if(
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=original.Deaths||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float phase,out bool present)||!present)return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value);
            var after=RebirthRemoteResourceClientOriginal.ReadActor(full.Copy().Element("plan").Element("actorAfter").Value);
            if(phase!=10f&&phase!=1f&&phase!=2f||(phase==2f?!actual.Matches(after):!actual.Matches(before)))return false;
            var reply=ReplyBody(root,full,reference,true,phase==1f);if(!FitsReply(reply))return false;text=reply.ToString(SaveOptions.DisableFormatting);return true;
            }
        }
        catch{text=null;return false;}
    }
    private static bool TryReplyUsed(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,out string text)
    {
        text=null;
        try
        {
            if(root?.Phase!=4||(string)root.Copy().Attribute("disposition")!="used"||
                !TryObservePlacedPublication(actor,root,out string proof)||(string)root.Copy().Attribute("ownerProof")!=proof||
                !RebirthRemoteResourcePlacedStationPreparation.TryReadUsedAllocation(actor,root,out _,out var allocation)||
                !readySources.TryGetValue(root.Operation,out var ready)||
                !RebirthRemoteResourcePlacedStationReference.TryRead(ready.Reference,out var reference)||
                !RebirthRemoteResourcePlacedStationReference.TryResolve(root,ready.Reference,allocation,out var full)||
                !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return false;
            using(scratch)
            {
            if(!TrySavedPaidOwner(actor,full,out _,out _))return false;
            var reply=ReplyBody(root,full,reference,false,false);if(!FitsReply(reply))return false;text=reply.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);return true;
            }
        }
        catch{text=null;return false;}
    }
}
