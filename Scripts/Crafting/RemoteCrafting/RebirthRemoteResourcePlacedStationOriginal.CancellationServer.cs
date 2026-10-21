using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    internal static void CancellationRequest(EntityPlayer actor,string text)
    {
        if(!ProtocolActive||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(actor))return;
        RebirthRemoteResourcePlacedStationPreparation.UploadRead memory=null;RebirthRemoteResourcePlacedStationColdRead.Original historical=null;
        try
        {
            BindCancellationWorld();var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
            if(body.Name!="placedStationCancelRequest"||body.Attributes().Count()!=8||body.Elements().Count()!=5||(int?)body.Attribute("version")!=1||
                (int?)body.Attribute("stage") is not int stage||stage<0||stage>3||
                !Guid.TryParseExact((string)body.Attribute("cancel"),"N",out var operation)||operation==Guid.Empty||
                !Guid.TryParseExact((string)body.Attribute("source"),"N",out var source)||source==Guid.Empty||
                !RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationBeforeHydration(body,null,out long headerWorkspace)||
                !RebirthRemoteResourcePlacedStationAllocation.TryAdmitIntentBeforeHydration(body.Element("fetchClientOriginal"),out long originalWorkspace))return;
            body.SetAttributeValue("stage",0);string header=body.ToString(SaveOptions.DisableFormatting);
            if(cancellationServers.TryGetValue(operation,out var existing))
            {if(existing.Header==header){if(!ReferenceEquals(existing.Actor,actor)||!existing.Current())RebindReceipt1Server(existing,actor,header);TryAdoptCancellationReentry(existing);if(existing.Current())PumpCancellationServer(existing);}return;}
            if((stage==1||stage==2)&&restartCancellations.TryGetValue(operation,out var beforeRestart)&&beforeRestart.BeforeRestart)
            {if(ResumeReceipt1Server(actor,header,beforeRestart,stage)&&cancellationServers.TryGetValue(operation,out var resumed))PumpCancellationServer(resumed);return;}
            if(stage==3){if(!ReplyRestartCancellation(actor,header,operation))ReplyArchivedCancellation(actor,body,header,operation,source,headerWorkspace,originalWorkspace);return;}
            if(stage==2&&restartCancellations.TryGetValue(operation,out var restart)&&restart.SavedAfterRestart)
            {ReplyRestartCancellation(actor,header,operation);return;}
            // Cold mixed/native1 cannot borrow a new current inventory. The
            // separately typed reconstruction contract must prove that body.
            if(stage!=0||!RebirthRemoteResourcePlacedStationPreparation.TryReserveCancellationRead(operation,
                checked(headerWorkspace+originalWorkspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes+6L*RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes),out memory)||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal").ToString(SaveOptions.DisableFormatting),out var original)||
                !ServerScope(actor,original)||CancellationOperation(original.Operation)!=operation||
                !RebirthRemoteResourcePlacedStationColdRead.TryOpen(actor,source,out historical,out _))return;
            var root=historical.Source;var full=historical.Artifact;
            if(root.Phase!=4||(string)root.Copy().Attribute("disposition")!="used"||root.Binding!=(string)body.Attribute("binding")||
                full.Original.Digest!=original.Digest||!XNode.DeepEquals(full.Original.Copy(),original.Copy())||full.Birth!=(string)body.Attribute("birth")||
                !RebirthRemoteResourcePlacedStationReference.TryRead(root.Copy().Element("ownerAction")?.Element("original")?.Value,out var reference)||
                reference.Artifact!=historical.ArtifactDigest||reference.Job!=original.Operation||reference.Original!=original.Digest||reference.Binding!=root.Binding||reference.Source!=source||
                !ObserveColdAccepted(historical,reference)||!TryReadCompletion(root,full,reference,null,out string proof,false,historical.Allocation)||
                proof!=(string)body.Attribute("proof")||(string)root.Copy().Attribute("ownerProof")!=proof||
                body.Element("paidOriginal")?.Value!=CanonicalPermission(ReplyBody(root,full,reference,true,false))||
                !ValidateInput(original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                !RebirthRemoteResourcePlacedStationScope.TryResolve(actor,position,original.Creation,reference.Birth,out var station)||
                RebirthRemoteResourcePlacedStationCustody.Blocks(station)||RebirthRemoteResourceSourceCustody.BlocksStation(station)||
                !RebirthRemoteResourcePlacedStationAllocation.TryAdmitUploadTargetBeforeCapture(station,headerWorkspace+originalWorkspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes,out long workspace)||
                !memory.Reserve(workspace)||CancellationFilesExist(operation,"Accepted")||CancellationFilesExist(operation,"Permission"))return;
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);
            if(peer==null||!RebirthRemoteResourcePlacedStationQueue.TryDecode(body.Element("queue").Value,out var queue)||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(full.Copy().Element("plan").Element("queue").Value,out var savedQueue)||
                (int?)body.Attribute("slot") is not int slot||slot<0||slot>=queue.Length||queue.Length!=station.Queue?.Length)return;
            var saved=savedQueue.Where(row=>row.Recipe?.ingredients?.Any(item=>item?.itemValue!=null&&item.itemValue.TryGetMetadata(RebirthRemoteResourcePlacedStationPaidRow.JobKey,out string id)&&id==original.Operation.ToString("N"))==true).ToArray();
            if(saved.Length!=1||!RebirthRemoteResourcePlacedStationPaidRow.TryRemainingFromOriginal(queue[slot],saved[0],original.Operation,source,root.Binding,out _))return;
            var beforeActor=RebirthRemoteResourceClientOriginal.ReadActor(body.Element("actorBefore").Value);
            var value=new CancellationServer{Actor=actor,Original=original,Operation=operation,Source=source,Binding=root.Binding,Proof=proof,Birth=reference.Birth,
                Directory=GameIO.GetSaveGameDir(),Header=header,Station=station,Chunk=station.GetChunk(),Peer=peer,Historical=historical,Memory=memory,Slot=slot,BeforeQueue=queue,BeforeActor=beforeActor,
                Origin=CancellationOrigin.Capture(actor,root,full,reference,proof,header)};
            if(!value.Current())return;
            var upload=new UploadOriginal{Actor=actor,Peer=peer,Station=station,Chunk=station.GetChunk(),Original=original,Source=operation,Birth=reference.Birth,Directory=GameIO.GetSaveGameDir(),
                Memory=memory,Workspace=workspace,IntentWorkspace=headerWorkspace+originalWorkspace-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes,Cancellation=value};
            if(uploadOriginals.ContainsKey(operation))return;
            if(!ArmPendingCancellationHostCustody(value))return;
            cancellationServers.Add(operation,value);uploadOriginals.Add(operation,upload);historical=null;memory=null;
        }
        catch{/* Retain any published original; no new native/owner effect. */}
        finally{historical?.Dispose();memory?.Dispose();}
    }
    private static void ObserveCancellationAccepted(UploadOriginal upload,NativeUploadInvocation invocation,byte[] after)
    {
        var original=upload.Cancellation;if(original==null||original.Accepted!=null||!original.Current()||
            RebirthCookingRemoteAdmissionBackend.Queue(original.Station.Queue)!=RebirthCookingRemoteAdmission.ReadProtocolXml(RebirthRemoteResourcePlacedStationQueue.Encode(original.BeforeQueue),RebirthRemoteResourcePlacedStationQueue.MaximumBytes).Element("native").Value)return;
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes);
        if(Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(original.Station.Input))!=body.Element("input").Value)return;
        var accepted=new AcceptedUpload(invocation.Before,invocation.Raw,after,GameTimer.Instance.ticks);
        var evidence=new XElement("placedCancellationAccepted",new XAttribute("version",1),new XAttribute("tick",accepted.Tick),
            new XElement("request",original.Header),new XElement("before",Convert.ToBase64String(accepted.Before)),new XElement("raw",Convert.ToBase64String(accepted.Raw)),new XElement("after",Convert.ToBase64String(accepted.After)));
        byte[] bytes=Encoding.UTF8.GetBytes(evidence.ToString(SaveOptions.DisableFormatting));
        if(bytes.Length>MaximumResultBytes||!original.Memory.Reserve(checked(original.Memory.ReservedBytes+12L*bytes.Length))||
            !SaveCancellation(original.Operation,original.Source,original.Original.Creation,"Accepted",bytes,true)||
            !ReadCancellationAccepted(original,accepted))return;
        if(original.HostCustody==null||original.HostCustody.EarlyEscaped)return;
        original.Accepted=accepted;upload.Accepted=accepted;
        if(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(original.Station,original.Station.Input,original.Station.Queue,original.Station.IsBurning,out original.BeforeTile,out _))return;
        // Freeze starts only after the actual authenticated native read/event.
        // Snapshot/refund phases run after the shared native reader scope ends.
    }
    private static bool ReadCancellationAccepted(CancellationServer original,AcceptedUpload expected)
    {
        var record=CancellationJournal(original.Operation,"Accepted").Read(original.Operation);
        if(record?.State!=RebirthCamperTransferJournal.Phase.Applied||record.Operation!=original.Operation||record.Vehicle!=original.Source||record.OwnerCreation!=original.Original.Creation||record.Revision!=0||!record.Before.SequenceEqual(record.After))return false;
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),MaximumResultBytes);
        if(body.Name!="placedCancellationAccepted"||body.Attributes().Count()!=2||body.Elements().Count()!=4||(int?)body.Attribute("version")!=1||
            (ulong?)body.Attribute("tick")!=expected.Tick||body.Element("request")?.Value!=original.Header)return false;
        var before=Convert.FromBase64String(body.Element("before").Value);var raw=Convert.FromBase64String(body.Element("raw").Value);var after=Convert.FromBase64String(body.Element("after").Value);
        return before.SequenceEqual(expected.Before)&&raw.SequenceEqual(expected.Raw)&&after.SequenceEqual(expected.After)&&
            RebirthRemoteResourcePlacedStationNativeReader.TryRead(before,out _)&&RebirthRemoteResourcePlacedStationNativeReader.TryRead(after,out var native)&&
            MatchesAcceptedNativeBody(raw,native,expected.Tick);
    }
    private static bool SavedCancellationOwner(CancellationServer original,float receipt,bool after,out PlayerDataFile saved)
    {
        saved=null;if(!original.Current()||!RebirthStablePlayerIdentity.TryResolveServerEntity(original.Actor,out var owner)||
            !RebirthCamperEnergySavedOwner.TryRead(owner,out var actual,out var current)||current.id!=original.Actor.entityId||current.deaths!=original.Original.Deaths||
            !(receipt==1f&&original.Receipt1Source!=null?IsCancellationOwnerPrefix(original.BeforeActor,original.AfterActor,actual):(after?original.AfterActor:original.BeforeActor).Matches(actual))||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(current,CancellationReceipt(original.Operation),out float phase,out bool present)||!present||phase!=receipt)return false;
        saved=current;return true;
    }
    private static void PumpCancellationServer(CancellationServer original)
    {
        if(!original.Current())TryAdoptCancellationReentry(original);
        if(original.FinalReplyPending||original.Memory==null||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(original.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.WorldApply,out var scratch))return;
        using(scratch)PumpCancellationServerCore(original);
    }
    private static void PumpCancellationServerCore(CancellationServer original)
    {
        if(!original.Current()||original.Accepted==null||original.BeforeTile==null||!ReadCancellationAccepted(original,original.Accepted))return;
        if(!original.BeforeSaved)
        {
            if(!original.FullTile(original.BeforeTile)||!SavedCancellationOwner(original,10f,false,out _))return;
            if(!ObserveCancellationSnapshot(original,false))return;original.BeforeSaved=true;
        }
        if(original.Permission==null&&!PlanCancellationRefund(original))return;
        if(!original.RefundQualified)
        {if(!QualifyCancellationFuture(original))return;original.RefundQualified=true;}
        if(!EnsureCancellationHostCustody(original)||!CancellationHostCustodyCurrent(original))return;
        if(FinalizeCancellationHost(original))return;
        if(SavedCancellationOwner(original,2f,true,out _))
        {
            if(original.QueueAttempted&&original.LoadOriginal?.Before!=null&&original.Loaded?.Installed()==true&&!original.QueueReentryAttempted&&
                !ApplyCancellationReconstructedQueue(original))return;
            if(!original.QueueAttempted&&!ApplyCancellationQueue(original))return;
            if(!original.QueueCompleted||!original.FullTile(original.AfterTile)||!ObserveCancellationSnapshot(original,true))return;
            original.AfterSaved=true;
            if(!CompleteCancellationServer(original))return;
            ReplyCancellation(original,4,false);return;
        }
        if(!original.QueueAttempted&&original.FullTile(original.BeforeTile))
        {
            bool grant=SavedCancellationOwner(original,1f,false,out _);
            if(grant||SavedCancellationOwner(original,10f,false,out _))ReplyCancellation(original,1,grant);
        }
    }
}
