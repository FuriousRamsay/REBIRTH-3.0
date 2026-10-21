using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // The native owner2 marker is the existing permission acknowledgement.
    // In StationInput mode the complete owner image MUST remain unchanged;
    // financial refund custody is the exact guarded native input original.
    private static bool ValidateCancellationInputReentry(CancellationServer original,string permission)
    {
        if(!original.BeforeActor.Matches(original.AfterActor))return false;
        var header=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes);
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(permission,MaximumResultBytes);
        var paid=RebirthCookingRemoteAdmission.ReadProtocolXml(header.Element("paidOriginal").Value,MaximumResultBytes);
        if(!RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationBeforeHydration(header,body,out long workspace)||
            !RebirthRemoteResourcePlacedStationAllocation.TryAdmitOwnerReplyBeforeHydration(paid,out long paidWorkspace))return false;
        long needed=checked(workspace+paidWorkspace-2*RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes);
        if(needed>original.InputReentryWorkspace)
        {if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+needed-original.InputReentryWorkspace)))return false;original.InputReentryWorkspace=needed;}
        if(body.Element("request")?.Value!=original.Header||body.Element("beforeTile")?.Value!=Convert.ToBase64String(original.BeforeTile)||
            body.Element("afterTile")?.Value!=Convert.ToBase64String(original.AfterTile)||body.Element("afterActor")?.Value!=RebirthRemoteResourceClientOriginal.ActorImage(original.BeforeActor)||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode(header.Element("queue").Value,out var beforeQueue)||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode(paid.Element("queue").Value,out var paidQueue)||original.Slot<0||original.Slot>=beforeQueue.Length||
            !RebirthRemoteResourcePlacedStationNativeReader.TryRead(original.BeforeTile,out var before))return false;
        var rows=paidQueue.Where(row=>row.Recipe?.ingredients?.Any(item=>item?.itemValue!=null&&item.itemValue.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string job)&&job==original.Original.Operation.ToString("N"))==true).ToArray();
        if(rows.Length!=1||!RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(beforeQueue[original.Slot],rows[0],original.Original.Operation,original.Source,original.Binding,out var remaining)||
            body.Element("refund")?.Value!=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(remaining.ToArray()))||
            Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(before.Input))!=header.Element("input").Value||
            RebirthCookingRemoteAdmissionBackend.Queue(before.Queue)!=RebirthCookingRemoteAdmissionBackend.Queue(beforeQueue)||
            !CancellationQueuePlan(beforeQueue,original.Slot,original.Actor.entityId,out var afterQueue,out _)||
            body.Element("afterQueue")?.Value!=RebirthRemoteResourcePlacedStationQueue.Encode(afterQueue))return false;
        var input=ItemStack.Clone(before.Input);
        foreach(var fragment in remaining)if(ItemStack.AddToItemStackArray(input,fragment.Clone())==-1)return false;
        before.Input=input;before.Queue=afterQueue;
        // Full native After, not a count/total/hash or a new current plan.
        return RebirthRemoteResourcePlacedStationNativeReader.Encode(before).SequenceEqual(original.AfterTile);
    }
}
