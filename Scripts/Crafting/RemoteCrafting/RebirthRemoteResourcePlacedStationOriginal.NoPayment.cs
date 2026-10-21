using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // This branch proves never-issued payment from every actual durable
    // generation. It neither restores custody nor compares current inventories
    // to infer that an attempted effect did not happen.
    private static bool TryColdNoPayment(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourcePlacedStationColdRead.Original cold)
    {
        var root=cold.Source;var full=cold.Artifact;var image=root.Copy();
        if(root.Phase!=3&&(root.Phase!=4||(string)image.Attribute("disposition")!="unused-no-payment"))return false;
        if(!cold.Current||!ServerScope(actor,original)||full.Original.Digest!=original.Digest||!XNode.DeepEquals(full.Original.Copy(),original.Copy())||
            (int?)image.Attribute("outcome")!=2||!root.SourcesUnattempted||root.HasOwnerAction||image.Element("ownerRefund")!=null||
            image.Element("removed")?.Value!=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new ItemStack[0])))return true;
        var allocation=cold.Allocation;
        if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.HistoricalSource,out var scratch))return true;
        string proof=RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+root.Owner+"|"+root.Binding);
        using(scratch)
        {
            if(!RebirthRemoteResourceDurableJournal.NeverPreparedPlacedOwnerActionBounded(root,allocation.Header))return true;
            if(root.Phase==3)
            {
                bool Current()=>cold.Current&&ServerScope(actor,original)&&ReferenceEquals(cold.Source,root);
                if(!RebirthRemoteResourceDurableJournal.TryAdvancePlacedBounded(root,next=>{next.SetAttributeValue("phase",4);next.SetAttributeValue("disposition","unused-no-payment");next.SetAttributeValue("ownerProof",proof);},Current,allocation.Header,out var terminal))return true;
                root=terminal;
            }
            if((string)root.Copy().Attribute("ownerProof")!=proof||!scratch.Current||!cold.Current)return true;
        }
        // The same positive outcome now archives the complete no-effect
        // original before exact source/target graph metadata retirement.
        return TryReplyNeverEntered(actor,original,root);
    }
    private static void AcceptNoPaymentResult(EntityPlayerLocal player,ClientJob job,XElement reply)
    {
        if(reply.Attributes().Count()!=7||reply.Elements().Count()!=1||(int?)reply.Attribute("version")!=1||
            (string)reply.Attribute("original")!=job.Original.Operation.ToString("N")||(string)reply.Attribute("digest")!=job.Original.Digest||
            !XNode.DeepEquals(reply.Element("fetchClientOriginal"),job.Original.Copy())||
            !Guid.TryParseExact((string)reply.Attribute("source"),"N",out var source)||source==Guid.Empty||
            ClientPermissions().Read(job.Original.Operation)!=null||SmallPlacedJournal(job.Original.Operation,SmallJournalDomain.ClientUsed).Read(job.Original.Operation)!=null)return;
        string world=(string)reply.Attribute("sourceWorld"),binding=(string)reply.Attribute("binding"),proof=(string)reply.Attribute("proof");
        bool Current()=>RebirthRemoteResourceClientOriginal.Current(player,job.Original)&&job.Paid==null&&
            (player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(job.Original.Operation))==0f||player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(job.Original.Operation))==10f)&&
            ClientPermissions().Read(job.Original.Operation)==null;
        if(!Current()||!RebirthRemoteResourceClientOriginal.SaveNoPaymentTerminal(player,job.Original,world,source,binding,proof,Current)||
            !RebirthRemoteResourcePlacedStationClientCustody.ReleaseNoPayment(player,job.Original,source,binding,proof))return;
        clientJobs.Remove(job.Original.Operation);
    }
}
