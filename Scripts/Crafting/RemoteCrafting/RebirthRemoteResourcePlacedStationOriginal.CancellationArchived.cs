using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Already-terminal metadata only. No target resolve, custody rebind,
    // inventory setter, source transition, or native queue mutation occurs.
    private static string ReadArchivedCancellation(Guid operation,Guid source,string creation,string purpose,
        RebirthRemoteResourcePlacedStationPreparation.UploadRead memory,RebirthCamperTransferJournal.Phase phase)
    {
        var record=CancellationJournal(operation,purpose,MaximumResultBytes,memory).Read(operation);
        if(record==null||record.Operation!=operation||record.Vehicle!=source||record.OwnerCreation!=creation||record.Revision!=0||record.State!=phase||
            !record.Before.SequenceEqual(record.After)||!memory.Reserve(checked(memory.ReservedBytes+12L*record.Before.Length+65536)))return null;
        return new UTF8Encoding(false,true).GetString(record.Before);
    }
    private static CancellationRestartArchive ReadCancellationTerminalArchive(EntityPlayer actor,XElement request,string header,Guid operation,Guid source,long headerWorkspace,long originalWorkspace,RebirthRemoteResourcePlacedStationPreparation.UploadRead suppliedMemory=null,RebirthRemoteResourcePlacedStationColdRead.Original suppliedCold=null,bool beforeOnly=false,bool allowBefore=false)
    {
        RebirthRemoteResourcePlacedStationPreparation.UploadRead memory=suppliedMemory;
        RebirthRemoteResourcePlacedStationColdRead.Original cold=suppliedCold;
        RebirthRemoteResourcePlacedStationAllocation.Scratch scratch=null;
        try
        {
            // Presence only selects an attempted terminal read. It grants no
            // permission and cannot stand in for the full archive below.
            if(memory==null&&!RebirthRemoteResourcePlacedStationPreparation.TryReserveCancellationRead(operation,checked(headerWorkspace+originalWorkspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes),out memory))return null;
            if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Completion,out scratch)||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(request.Element("fetchClientOriginal").ToString(SaveOptions.DisableFormatting),out var original)||
                actor!=null&&!ServerScope(actor,original)||CancellationOperation(original.Operation)!=operation||
                cold==null&&!RebirthRemoteResourcePlacedStationColdRead.TryOpen(actor,source,out cold,out _))return null;
            var root=cold.Source;var full=cold.Artifact;
            string binding=(string)request.Attribute("binding"),birth=(string)request.Attribute("birth");
            if(root.Phase!=4||(string)root.Copy().Attribute("disposition")!="used"||root.Binding!=binding||full.Birth!=birth||
                !XNode.DeepEquals(full.Original.Copy(),original.Copy())||
                !RebirthRemoteResourcePlacedStationReference.TryRead(root.Copy().Element("ownerAction")?.Element("original")?.Value,out var reference)||
                reference.Artifact!=cold.ArtifactDigest||reference.Source!=source||reference.Job!=original.Operation||reference.Original!=original.Digest||reference.Binding!=binding||
                !ObserveColdAccepted(cold,reference)||!TryReadCompletion(root,full,reference,null,out string proof,false,cold.Allocation)||
                proof!=(string)request.Attribute("proof")||(string)root.Copy().Attribute("ownerProof")!=proof||
                request.Element("paidOriginal")?.Value!=CanonicalPermission(ReplyBody(root,full,reference,true,false)))return null;
            {
                string permission=ReadArchivedCancellation(operation,source,original.Creation,"Permission",memory,RebirthCamperTransferJournal.Phase.Uncertain);
                if(permission==null)return null;var body=RebirthCookingRemoteAdmission.ReadProtocolXml(permission,MaximumResultBytes);
                if(body.Name!="placedCancellationPermission"||body.Attributes().Count()!=5||body.Elements().Count()!=7||(int?)body.Attribute("version")!=1||
                    (string)body.Attribute("cancel")!=operation.ToString("N")||(string)body.Attribute("job")!=original.Operation.ToString("N")||
                    (string)body.Attribute("source")!=source.ToString("N")||(string)body.Attribute("binding")!=binding||body.Element("request")?.Value!=header||
                    !RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationBeforeHydration(request,body,out long workspace)||
                    !memory.Reserve(checked(memory.ReservedBytes+workspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes)))return null;
                if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(request.Element("queue").Value,out var queue)||
                    (int?)request.Attribute("slot") is not int slot||slot<0||slot>=queue.Length||
                    !RebirthRemoteResourcePlacedStationQueue.TryDecode(full.Copy().Element("plan").Element("queue").Value,out var paidQueue))return null;
                int nativeOwner=(int)RebirthCookingRemoteAdmission.ReadProtocolXml(body.Element("beforeEvidence").Value,MaximumResultBytes).Attribute("ownerId");
                if(nativeOwner<1)return null;
                var paid=paidQueue.Where(row=>row.Recipe?.ingredients?.Any(item=>item?.itemValue!=null&&item.itemValue.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string id)&&id==original.Operation.ToString("N"))==true).ToArray();
                if(paid.Length!=1||!RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(queue[slot],paid[0],original.Operation,source,binding,out var remaining)||
                    !ValidateInput(original,out var position,out _,out _,out _,out _,out _,out _,out int belt,out var payment,out _,out _)||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryRead(Convert.FromBase64String(body.Element("beforeTile").Value),out var before)||
                    RebirthCookingRemoteAdmissionBackend.Queue(before.Queue)!=RebirthCookingRemoteAdmissionBackend.Queue(queue)||
                    Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(before.Input))!=request.Element("input").Value||
                    !CancellationQueuePlan(queue,slot,nativeOwner,out var afterQueue,out _))return null;
                var beforeActor=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("actorBefore").Value);var afterActor=beforeActor.Copy();
                var input=ItemStack.Clone(before.Input);
                if(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.OwnerInventory)
                {
                    if(!RebirthRecyclerOwnerDelivery.Plan(beforeActor.Bag,beforeActor.Belt,beforeActor.BagLocks,null,belt,remaining.ToArray(),out var bag,out var toolbelt))return null;
                    afterActor.Bag=bag;afterActor.Belt=toolbelt;
                }
                else foreach(var fragment in remaining)if(ItemStack.AddToItemStackArray(input,fragment.Clone())==-1)return null;
                before.Input=input;before.Queue=afterQueue;
                byte[] after=RebirthRemoteResourcePlacedStationNativeReader.Encode(before),beforeTile=Convert.FromBase64String(body.Element("beforeTile").Value);
                if(body.Element("afterTile").Value!=Convert.ToBase64String(after)||body.Element("afterQueue").Value!=RebirthRemoteResourcePlacedStationQueue.Encode(afterQueue)||
                    body.Element("afterActor").Value!=RebirthRemoteResourceClientOriginal.ActorImage(afterActor)||body.Element("refund").Value!=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(remaining.ToArray())))return null;
                string savedBefore=ReadArchivedCancellation(operation,source,original.Creation,"Before",memory,RebirthCamperTransferJournal.Phase.Applied);
                string savedAfter=beforeOnly?null:ReadArchivedCancellation(operation,source,original.Creation,"After",memory,RebirthCamperTransferJournal.Phase.Applied);
                byte[] marker=CancellationMarkerImage(operation,original.Operation,source,binding,birth,header);
                if(savedBefore==null||body.Element("beforeEvidence").Value!=savedBefore||
                    !ValidateCancellationSnapshot(RebirthCookingRemoteAdmission.ReadProtocolXml(savedBefore,MaximumResultBytes),operation,source,binding,birth,header,false,beforeTile,beforeActor,nativeOwner,original.Deaths,position,null,out var savedBeforeNative))return null;
                var savedNative=savedBeforeNative;
                bool savedAfterReady=!beforeOnly&&savedAfter!=null&&ValidateCancellationSnapshot(RebirthCookingRemoteAdmission.ReadProtocolXml(savedAfter,MaximumResultBytes),operation,source,binding,birth,header,true,after,afterActor,nativeOwner,original.Deaths,position,marker,out savedNative);
                if(!beforeOnly&&!allowBefore&&!savedAfterReady)return null;
                string acceptedText=ReadArchivedCancellation(operation,source,original.Creation,"Accepted",memory,RebirthCamperTransferJournal.Phase.Applied);
                if(acceptedText==null)return null;var accepted=RebirthCookingRemoteAdmission.ReadProtocolXml(acceptedText,MaximumResultBytes);
                if(accepted.Name!="placedCancellationAccepted"||accepted.Attributes().Count()!=2||accepted.Elements().Count()!=4||(int?)accepted.Attribute("version")!=1||
                    accepted.Element("request")?.Value!=header||(ulong?)accepted.Attribute("tick") is not ulong tick)return null;
                var actualBefore=Convert.FromBase64String(accepted.Element("before").Value);var actualAfter=Convert.FromBase64String(accepted.Element("after").Value);
                var raw=Convert.FromBase64String(accepted.Element("raw").Value);
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(actualBefore,out long beforeGraph,out long beforeScratch)||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryMeasure(actualAfter,out long afterGraph,out long afterScratch)||
                    !RebirthRemoteResourcePlacedStationNativeReader.TryMeasureToServer(raw,out _,out long rawGraph,out long rawScratch)||
                    !memory.Reserve(checked(memory.ReservedBytes+12*(beforeGraph+afterGraph+rawGraph)+Math.Max(0,Math.Max(beforeScratch,Math.Max(afterScratch,rawScratch))-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes))))return null;
                if(!RebirthRemoteResourcePlacedStationNativeReader.TryRead(actualBefore,out _)||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(actualAfter,out var native)||
                    !MatchesAcceptedNativeBody(raw,native,tick)||
                    !RebirthRemoteResourcePlacedStationCandidate.TryNormalizeClock(actualAfter,native.ClockOffset,out var normalized,out _)||!normalized.SequenceEqual(beforeTile))return null;
                if(beforeOnly||allowBefore&&!savedAfterReady)
                {
                    if(!cold.Current)return null;
                    var pending=new CancellationRestartArchive{Original=original,Operation=operation,Source=source,Header=header,Binding=binding,Birth=birth,Proof=proof,Permission=permission,
                        Before=beforeActor,After=afterActor,NativeOwner=nativeOwner,NativeAfter=after,BeforeEvidence=savedBefore,AcceptedEvidence=acceptedText,SavedNative=savedBeforeNative,Memory=memory,BeforeRestart=true};
                    if(ReadRestartBeforeCustody(pending)){memory=null;return pending;}
                    if(beforeOnly)return null;
                    // No positive Prepared source and no complete saved After:
                    // this candidate cannot issue any native continuation.
                    return null;
                }
                string mutation=ReadArchivedCancellation(operation,source,original.Creation,"Mutation",memory,RebirthCamperTransferJournal.Phase.Applied);
                string terminal=ReadArchivedCancellation(operation,source,original.Creation,"Terminal",memory,RebirthCamperTransferJournal.Phase.Applied);
                bool ackPresent=CancellationFilesExist(operation,"FinalAck");
                string ack=ackPresent?ReadArchivedCancellation(operation,source,original.Creation,"FinalAck",memory,RebirthCamperTransferJournal.Phase.Applied):null;
                if(ackPresent&&ack==null)return null; // Unknown receipt is not absent terminal metadata.
                if(mutation!=Encoding.UTF8.GetString(CancellationMutation(operation,source,binding,permission))||terminal!=Encoding.UTF8.GetString(CancellationServerTerminalBytes(operation,source,binding,permission,savedAfter)))return null;
                string expectedAck=new XElement("placedCancellationFinalAck",new XAttribute("version",1),new XAttribute("cancel",operation.ToString("N")),
                    new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("permission",RebirthRemoteResourceDurableJournal.Digest(permission)),
                    new XAttribute("terminal",Convert.ToBase64String(Encoding.UTF8.GetBytes(terminal)))).ToString(SaveOptions.DisableFormatting);
                if(ack!=null&&ack!=expectedAck||!cold.Current)return null;
                var result=new CancellationRestartArchive{Original=original,Operation=operation,Source=source,Header=header,Binding=binding,Birth=birth,Proof=proof,Permission=permission,
                    Before=beforeActor,After=afterActor,NativeOwner=nativeOwner,NativeAfter=after,BeforeEvidence=savedBefore,AfterEvidence=savedAfter,AcceptedEvidence=acceptedText,SavedNative=savedNative,ExpectedAck=expectedAck,AckSaved=ack!=null,Memory=memory};
                memory=null;return result;
            }
        }
        catch{/* Full terminal archive is unknown; no body or effect recovery. */}
        finally{scratch?.Dispose();cold?.Dispose();memory?.Dispose();}
        return null;
    }
}
