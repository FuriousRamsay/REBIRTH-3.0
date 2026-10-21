using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    internal static bool CancellationQueuePlan(RecipeQueueItem[] before,int own,int actorId,out RecipeQueueItem[] after,out int[] mapping)
    {
        after=null;mapping=null;if(before==null||own<0||own>=before.Length||before.Any(row=>row==null))return false;
        var result=(RecipeQueueItem[])before.Clone();var slots=Enumerable.Range(0,before.Length).ToArray();
        RecipeQueueItem Empty(bool crafting)=>new RecipeQueueItem{StartingEntityId=actorId,Quality=1,IsCrafting=crafting};
        result[own]=Empty(false);slots[own]=-1;int previous=-1;
        // Exact native XUiC_CraftingQueue.RefreshQueue one-pass order. Move
        // references; never reconstruct an unrelated native row/recipe object.
        for(int index=result.Length-1;index>=0;index--)
        {
            if(result[index].Recipe!=null&&previous>=0&&result[previous].Recipe==null)
            {bool crafting=result[index].IsCrafting;result[previous]=result[index];slots[previous]=slots[index];result[index]=Empty(crafting);slots[index]=-1;}
            previous=index;
        }
        after=result;mapping=slots;return true;
    }
    private static string CancellationMarkerKey(CancellationServer original)=>"RebirthPlacedCancellationV1:"+original.Operation.ToString("N");
    private static byte[] CancellationMarker(CancellationServer original)=>CancellationMarkerImage(original.Operation,original.Original.Operation,original.Source,original.Binding,original.Birth,original.Header);
    private static byte[] CancellationMarkerImage(Guid operation,Guid job,Guid source,string binding,string birth,string header)=>Encoding.UTF8.GetBytes(new XElement("placedCancellation",new XAttribute("version",1),
        new XAttribute("cancel",operation.ToString("N")),new XAttribute("job",job.ToString("N")),new XAttribute("source",source.ToString("N")),
        new XAttribute("binding",binding),new XAttribute("birth",birth),new XAttribute("request",RebirthRemoteResourceDurableJournal.Digest(header))).ToString(SaveOptions.DisableFormatting));
    private static long EscapedCancellationBytes(string text)
    {long bytes=Encoding.UTF8.GetByteCount(text);foreach(char value in text)if(value=='&')bytes+=4;else if(value=='<'||value=='>')bytes+=3;else if(value=='\r')bytes+=4;return bytes;}
    private static bool CancellationMarkerCurrent(CancellationServer original)
    {
        return original.Current()&&original.Marker!=null&&original.Chunk.ChunkCustomData.dict.TryGetValue(CancellationMarkerKey(original),out var marker)&&
            marker.TriggerWriteDataDelegate==null&&marker.expiresInWorldTime==ulong.MaxValue&&!marker.isSavedToNetwork&&marker.data!=null&&marker.data.SequenceEqual(original.Marker);
    }
    private static bool PlanCancellationRefund(CancellationServer original)
    {
        if(!original.BeforeSaved||!original.FullTile(original.BeforeTile)||!ReadCancellationSnapshot(original,false,out var nativeBefore)||
            !SavedCancellationOwner(original,10f,false,out _)||
            !RebirthRemoteResourcePlacedStationQueue.TryDecode(original.Historical.Artifact.Copy().Element("plan").Element("queue").Value,out var paidQueue)||
            !ValidateInput(original.Original,out _,out _,out _,out _,out _,out _,out _,out int belt,out var payment,out _,out _)||
            !RebirthRemoteResourcePlacedStationNativeReader.TryRead(original.BeforeTile,out var before))return false;
        var paid=paidQueue.Where(row=>row.Recipe?.ingredients?.Any(item=>item?.itemValue!=null&&item.itemValue.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string id)&&id==original.Original.Operation.ToString("N"))==true).ToArray();
        if(paid.Length!=1||!RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(original.BeforeQueue[original.Slot],paid[0],original.Original.Operation,original.Source,original.Binding,out var remaining))return false;
        var actorAfter=original.BeforeActor.Copy();var input=ItemStack.Clone(before.Input);
        if(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory)
        {
            if(!RebirthRecyclerOwnerDelivery.Plan(original.BeforeActor.Bag,original.BeforeActor.Belt,original.BeforeActor.BagLocks,null,belt,remaining.ToArray(),out var bag,out var toolbelt))return false;
            actorAfter.Bag=bag;actorAfter.Belt=toolbelt;
        }
        else foreach(var fragment in remaining)
        {if(ItemStack.AddToItemStackArray(input,fragment.Clone())==-1)return false;}
        if(!CancellationQueuePlan(original.BeforeQueue,original.Slot,original.Actor.entityId,out var queue,out _))return false;
        if(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(original.Station,input,before.Output,queue,before.Burning,out var after,out _))return false;
        var body=new XElement("placedCancellationPermission",new XAttribute("version",1),new XAttribute("cancel",original.Operation.ToString("N")),
            new XAttribute("job",original.Original.Operation.ToString("N")),new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),
            new XElement("request",original.Header),new XElement("beforeTile",Convert.ToBase64String(original.BeforeTile)),new XElement("afterTile",Convert.ToBase64String(after)),
            new XElement("afterQueue",RebirthRemoteResourcePlacedStationQueue.Encode(queue)),new XElement("afterActor",RebirthRemoteResourceClientOriginal.ActorImage(actorAfter)),
            new XElement("refund",Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(remaining.ToArray()))),new XElement("beforeEvidence",nativeBefore.Canonical));
        string text=body.ToString(SaveOptions.DisableFormatting);byte[] bytes=Encoding.UTF8.GetBytes(text);
        var request=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes);
        if(bytes.Length>MaximumResultBytes||!RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationBeforeHydration(request,body,out long workspace)||
            !original.Memory.Reserve(checked(original.Memory.ReservedBytes+workspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes)))return false;
        var marker=CancellationMarker(original);int markerGrowth;
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {writer.Write(CancellationMarkerKey(original));writer.Write(ulong.MaxValue);writer.Write(false);writer.Write((ushort)marker.Length);writer.Write(marker);writer.Flush();markerGrowth=checked((int)stream.Length);}
        if(!RebirthRemoteResourcePlacedStationCandidate.TryMeasureChunk(nativeBefore.Snapshot,nativeBefore.Offset,nativeBefore.Tile,after,markerGrowth,out int raw,out _,out long bound)||
            checked(EscapedCancellationBytes(text)+4L*((raw+bound+1024*1024+2)/3)+RebirthRemoteResourceClientOriginal.ActorImage(actorAfter).Length+65536)>MaximumResultBytes||
            !SaveCancellation(original.Operation,original.Source,original.Original.Creation,"Permission",bytes,false))return false;
        original.AfterActor=actorAfter;original.AfterInput=input;original.AfterQueue=queue;original.AfterTile=after;original.Permission=text;original.Marker=marker;
        // Full new original is durable before any refund grant. Old financial
        // artifacts/history remain immutable on disk; no old source body is
        // rebound or mutated. Retire only this temporary observation graph.
        original.Historical.Dispose();original.Historical=null;
        long evidenceBound=checked(4L*((raw+bound+1024*1024+2)/3)+RebirthRemoteResourceClientOriginal.ActorImage(actorAfter).Length+65536);
        original.AfterEvidenceBudget=checked(32*evidenceBound+65536);original.NativeLoadWorkspace=checked(16L*(raw+after.Length+Encoding.UTF8.GetByteCount(original.Header))+65536);
        original.RefundQualified=QualifyCancellationFuture(original);return original.RefundQualified;
    }
    private static bool QualifyCancellationFuture(CancellationServer original)
    {
        if(!CancellationHostCustodyHooksInstalled)return false;
        if(original.Actor.world.ChunkCache?.ChunkProvider is not ChunkProviderGenerateWorld provider||provider.m_RegionFileManager?.snapshotUtil is not ChunkSnapshotUtil util||
            !ReferenceEquals(provider.m_RegionFileManager.regionFileAccess,util.regionFileAccess)||!ReferenceEquals(util.regionFileAccess,util.chunkReader.regionFileAccess)||
            original.AfterEvidenceBudget<1||original.NativeLoadWorkspace<1||!ReserveCancellationEvidence(original,original.AfterEvidenceBudget))return false;
        if(!original.NativeLoadReserved){if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+original.NativeLoadWorkspace)))return false;original.NativeLoadReserved=true;}
        return EnsureCancellationHostCustody(original);
    }
    private static byte[] CancellationMutation(CancellationServer original)=>CancellationMutation(original.Operation,original.Source,original.Binding,original.Permission);
    private static byte[] CancellationMutation(Guid operation,Guid source,string binding,string permission)=>Encoding.UTF8.GetBytes(new XElement("placedCancellationMutation",new XAttribute("version",1),
        new XAttribute("cancel",operation.ToString("N")),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),
        new XAttribute("permission",RebirthRemoteResourceDurableJournal.Digest(permission))).ToString(SaveOptions.DisableFormatting));
    private static bool ApplyCancellationQueue(CancellationServer original)
    {
        if(original.QueueAttempted||!original.RefundQualified||!original.Current()||!original.BeforeSaved||!original.FullTile(original.BeforeTile)||
            !SavedCancellationOwner(original,2f,true,out _)||!ReadCancellationSnapshot(original,false,out _)||
            CancellationJournal(original.Operation,"Mutation",MaximumApplicationCommand).Read(original.Operation)!=null)return false;
        if(HoldCancellationScenario(original.Operation,"nativeBefore"))return false;
        byte[] command=CancellationMutation(original);
        if(!SaveCancellation(original.Operation,original.Source,original.Original.Creation,"Mutation",command,false)||!original.FullTile(original.BeforeTile))return false;
        if(HoldCancellationScenario(original.Operation,"nativeIntent"))return false;
        // Observation registration precedes the first native queue attempt.
        // Unsupported providers keep the existing one-attempt semantics.
        try{ArmCancellationBeforeAttempt(original);}catch{/* Observer registration cannot replay or replace the original native attempt. */}
        var station=original.Station;
        if(!CancellationQueuePlan(original.BeforeQueue,original.Slot,original.Actor.entityId,out _,out var mapping))return false;
        var queue=new RecipeQueueItem[station.Queue.Length];for(int slot=0;slot<queue.Length;slot++)queue[slot]=mapping[slot]<0?original.AfterQueue[slot]:station.Queue[mapping[slot]];
        if(RebirthCookingRemoteAdmissionBackend.Queue(queue)!=RebirthCookingRemoteAdmissionBackend.Queue(original.AfterQueue))return false;
        if(!EnterCancellationCustodyQueueAttempt(original))return false;
        original.QueueAttempted=true;
        if(HoldCancellationScenario(original.Operation,"nativeConsumed"))return false;
        if(!SameGrid(station.Input,original.AfterInput)&&!CancellationNativeSet(original,"set_Input",original.AfterInput,()=>station.Input=original.AfterInput))return false;
        if(!CancellationNativeSet(original,"set_Queue",queue,()=>station.Queue=queue)||
            !CancellationNativeSet(original,nameof(TileEntityWorkstation.ResetTickTime),null,station.ResetTickTime)||!original.FullTile(original.AfterTile))return false;
        string key=CancellationMarkerKey(original);
        if(original.Chunk.ChunkCustomData.dict.ContainsKey(key))return false;
        original.Chunk.ChunkCustomData.Set(key,new ChunkCustomData(key,ulong.MaxValue,false){data=(byte[])original.Marker.Clone()});original.Chunk.isModified=true;
        original.QueueCompleted=CancellationMarkerCurrent(original)&&original.FullTile(original.AfterTile);
        if(original.QueueCompleted)ObserveCancellationScenarioQueue(original);
        return original.QueueCompleted;
    }
    private static bool CompleteCancellationServer(CancellationServer original)
    {
        if(!original.AfterSaved||!original.QueueCompleted||!SavedCancellationOwner(original,2f,true,out _)||!ReadCancellationSnapshot(original,true,out _))return false;
        if(original.QueueReentryAttempted&&!CompleteCancellationQueueRecovery(original))return false;
        byte[] command=CancellationMutation(original);
        if(!SaveCancellation(original.Operation,original.Source,original.Original.Creation,"Mutation",command,true))return false;
        byte[] terminal=CancellationServerTerminalBytes(original);
        bool terminalSaved=false;
        try{terminalSaved=SaveCancellation(original.Operation,original.Source,original.Original.Creation,"Terminal",terminal,true);}
        finally
        {
            // Only an exact saved Uncertain terminal plus complete saved
            // financial/native After may arm this actual-load continuation.
            if(!terminalSaved)try{ArmCancellationSavedAfter(original);}catch{/* Preserve the original metadata failure and retained financial original. */}
        }
        if(!terminalSaved)return false;
        return ArmCancellationReentry(original);
    }
    private static byte[] CancellationServerTerminalBytes(CancellationServer original)=>CancellationServerTerminalBytes(original.Operation,original.Source,original.Binding,original.Permission,original.AfterEvidence);
    private static byte[] CancellationServerTerminalBytes(Guid operation,Guid source,string binding,string permission,string evidence)=>Encoding.UTF8.GetBytes(new XElement("placedCancellationTerminal",new XAttribute("version",1),new XAttribute("cancel",operation.ToString("N")),
            new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("permission",RebirthRemoteResourceDurableJournal.Digest(permission)),
            new XAttribute("after",RebirthRemoteResourceDurableJournal.Digest(evidence))).ToString(SaveOptions.DisableFormatting));
    private static void ReplyCancellation(CancellationServer original,int phase,bool grant)
    {
        if(original.FinalReplyPending||!original.Current()||original.Permission==null||phase!=1&&phase!=4&&phase!=5)return;
        if(phase==4&&HoldCancellationScenario(original.Operation,"savedAfter"))return;
        var body=new XElement("placedStationCancelResult",new XAttribute("version",1),new XAttribute("original",original.Original.Operation.ToString("N")),
            new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("phase",phase),new XAttribute("grant",grant),
            new XElement("permission",original.Permission),new XElement("evidence",phase>=4?original.AfterEvidence:""));
        if(phase==1&&grant&&original.Receipt1Source!=null)
        {
            if(!CancellationHostCustodyCurrent(original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(original.Actor,out var owner)||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var prefix,out var saved)||saved.id!=original.Actor.entityId||saved.deaths!=original.Original.Deaths||!IsCancellationOwnerPrefix(original.BeforeActor,original.AfterActor,prefix)||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,CancellationReceipt(original.Operation),out float receipt,out bool present)||!present||receipt!=1f)return;
            long bound=checked(16L*(original.HostCustody.Anchor.Length+Encoding.UTF8.GetByteCount(original.Receipt1BeforeEvidence)+RebirthRemoteResourceClientOriginal.ActorImage(prefix).Length+65536));
            if(!ReserveCancellationEvidence(original,bound))return;
            body.Add(new XElement("reconstruction",original.Receipt1Source.Grant(prefix)));
        }
        if(phase==4&&original.BeforeTwoPublication?.Current==true)body.Add(new XElement("reconstruction",original.BeforeTwoPublication.Encode()));
        else if(phase>=4&&original.QueueRecoveryPublication?.Current==true)body.Add(new XElement("reconstruction",original.QueueRecoveryPublication.Encode()));
        else if((phase==5||phase==4&&original.LoadOriginal?.SavedAfter!=null)&&original.Loaded?.Installed()==true)body.Add(new XElement("reconstruction",original.Loaded.Encode()));
        string text=body.ToString(SaveOptions.DisableFormatting);if(Encoding.UTF8.GetByteCount(text)>MaximumResultBytes)return;
        if(phase==4&&(original.LoadOriginal?.SavedAfter!=null||original.QueueRecoveryPublication!=null))
        {
            var retainedPhase4=original.Memory;
            original.FinalReplyPending=NetPackageRebirthRemoteRepairResult.SendObserved(original.Actor.entityId,original.Original,text,sent=>
            {original.FinalReplyPending=false;if(original.Memory==null)retainedPhase4.Dispose();});
            return;
        }
        if(phase!=5){NetPackageRebirthRemoteRepairResult.Send(original.Actor.entityId,original.Original,text);return;}
        var retained=original.Memory;
        original.FinalReplyPending=NetPackageRebirthRemoteRepairResult.SendObserved(original.Actor.entityId,original.Original,text,sent=>
        {
            // False can mean an obsolete connection. The applied FinalAck
            // remains the immutable terminal receipt; no native retry follows.
            original.FinalReplyPending=false;DropCancellationServer(original);retained.Dispose();
        });
    }
}
