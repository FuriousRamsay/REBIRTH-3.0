using System;
using System.Linq;
using System.Xml.Linq;

// Uses the existing bounded fragmented authenticated original transport.
// Production bootstrap installs only after native owner/cancellation dependencies.
internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private static bool protocolInstalled;
    internal static bool ProtocolActive{get;private set;}
    // The reviewed bootstrap requires success before enabling the placed path.
    internal static bool InstallProductionProtocol()
    {
        if(ProtocolActive)return true;
        if(!RemoteResourceClientTransactionPatchInstaller.IsInstalled)return false;
        RebirthCookingOutputDelivery.Install();
        if(!RebirthCookingOutputDelivery.IsInstalled)return false;
        RebirthRemoteResourceDurableDispatch.Install();
        RebirthRemoteResourceRepairProtocol.Install();
        RebirthCamperStationNativeActorPayment.Install();
        InstallProtocolInfrastructure();
        // Set last: failed prerequisite installation cannot select this path.
        ProtocolActive=true;
        return true;
    }
    internal static void InstallProtocolInfrastructure()
    {
        if(protocolInstalled)return;
        RebirthRemoteResourceFetchedStationCapacity.Install();InstallQueueHydration();ModEvents.GameUpdate.RegisterHandler(ProtocolUpdate);protocolInstalled=true;
    }
    internal static void Request(EntityPlayer actor,string text)
    {
        if(!ProtocolActive||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(actor))return;
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,24*1024*1024);
            if(body.Name!="fetchedStationRequest"||body.Attributes().Count()!=2||(int?)body.Attribute("version")!=1||
                !ReadRequestOriginal(actor,body,out var original)||!IsCandidate(original))return;
            int stage=(int)body.Attribute("stage");if(stage<0||stage>9||(stage>=8?!EndedZeroScope(actor,original):!Scope(actor,original)))return;bool refundStage=stage==5||stage==6;
            var refund=body.Element("repairRefund");if(body.Elements().Count()!=(refundStage?2:1)||refundStage!=(refund!=null))return;
            var originalNode=refundStage?body.Element("originalRef"):original.Copy();if(originalNode==null||!refundStage&&body.Element("originalRef")!=null)return;
            var canonical=new XElement("fetchedStationRequest",new XAttribute("version",1),new XAttribute("stage",stage),new XElement(originalNode),refund==null?null:new XElement(refund));if(!XNode.DeepEquals(body,canonical))return;
            if(stage>=8)
            {
                if((stage==8||AcknowledgeEndedZeroTerminal(actor,original))&&ReadEndedZeroTerminal(actor,original,out var ended,out string rejectedProof))
                    NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,new XElement("fetchedStationEndedZero",new XAttribute("version",1),
                        new XAttribute("original",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),new XAttribute("sourceWorld",ended.World),
                        new XAttribute("source",ended.Operation.ToString("N")),new XAttribute("binding",ended.Binding),new XAttribute("phase",ended.Phase),new XAttribute("nativeRejected",rejectedProof),new XAttribute("terminalProof",ended.Phase==4?(string)ended.Copy().Attribute("ownerProof"):"")).ToString(SaveOptions.DisableFormatting));
                return; // Metadata ACK only; no new-life native upload or permission.
            }
            if(stage==7)
            {
                RefuseUnpaid(actor,original);
                if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var cancellingOwner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out var cancellingWorld))return;
                Guid cancellingSource=RebirthRemoteResourceDurableJournal.Operation(cancellingWorld,cancellingOwner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
                if(!RebirthRemoteResourceDurableJournal.TryRead(cancellingOwner.StorageKey,cancellingSource,out var cancellingRoot,out bool cancellingUnknown)||cancellingUnknown)return;
                if(cancellingRoot==null)
                {RebirthRemoteResourcePreAdmissionRefusal.Request(actor,new XElement("preAdmissionRefusalRequest",new XAttribute("version",1),original.Copy()).ToString(SaveOptions.DisableFormatting));return;}
                if(cancellingRoot.Phase==2&&(int)cancellingRoot.Copy().Attribute("outcome")==1)RejectUnused(actor,original,false);
                if(RebirthRemoteResourceDurableJournal.TryRead(cancellingOwner.StorageKey,cancellingSource,out var refused,out cancellingUnknown)&&!cancellingUnknown&&refused!=null)ReplyFetched(actor,original,refused);
                return;
            }
            if(!Admit(actor,original))return;
            var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value));
            if(!RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,remote,stage!=0,out var root)||root==null)
            {ReplyFetched(actor,original,null);return;}
            bool refundPermission=false;string refundDigest=refund==null?"":RebirthRemoteResourceDurableJournal.Digest(refund.ToString(SaveOptions.DisableFormatting));
            if(stage==3)RejectUnused(actor,original,false);
            else if(stage==4)RejectUnused(actor,original,true);
            else if(refundStage&&ValidateFetchedNoUseRoot(root.Copy()))RebirthRemoteResourceRepairRefund.Host(actor,original,refund,stage==6,out refundPermission);
            else if(stage==1&&root.Phase==2&&(int)root.Copy().Attribute("outcome")==1)
            {
                if(!ObserveUsed(actor,root.Operation)&&ValidateInput(original,false,out var address,out _,out _,out _,out _,out _,out _)&&
                    RebirthStationLiveAccess.TryGetMobileLease(actor,address,original.Creation,out var station,out _))
                {
                    // Existing native journal recovery, never a second plan.
                    if(RebirthCamperStationNativeActorPayment.RestorePending(station.BoundVehicle))
                    {
                        if(RebirthCamperStationNativeActorPayment.MatchesPendingOperation(station,original.Operation))
                            RebirthCamperStationNativeActorPayment.TryResume(actor,station,address,original.Creation);
                        else if(!RebirthCamperStationNativeActorPayment.IsHeld(station))
                            RebirthCamperStationNativeActorPayment.BeginFetchedOriginal(actor,station,address,original.Creation,original.Operation,root.Operation,root.Binding);
                    }
                }
            }
            else if(stage==1&&root.Phase==4&&(string)root.Copy().Attribute("disposition")=="used"&&
                ValidateInput(original,false,out var terminalAddress,out _,out _,out _,out _,out _,out _)&&
                RebirthStationLiveAccess.TryGetMobileLease(actor,terminalAddress,original.Creation,out var terminalStation,out _))
            {
                if(RebirthCamperStationNativeActorPayment.MatchesPendingOperation(terminalStation,original.Operation))
                    RebirthCamperStationNativeActorPayment.TryResume(actor,terminalStation,terminalAddress,original.Creation);
                else RebirthCamperStationNativeActorPayment.TryResumeFetchedRetirement(actor,terminalStation,terminalAddress,original.Creation,original.Operation);
            }
            else if(stage==2&&root.Phase==3)RebirthRemoteResourceDurableOutcome.AcknowledgeNoPayment(actor,root.Operation,root.Binding);
            if(RebirthRemoteResourceDurableJournal.TryRead(root.Owner,root.Operation,out var current,out bool unknown)&&!unknown&&current!=null)
            {if(current.Phase==4&&(int)current.Copy().Attribute("outcome")==2)ReleaseNoPaymentStationHold(current);ReplyFetched(actor,original,current,refundPermission,refundDigest);}
        }
        catch{/* Unknown original is neither unused nor paid completion. */}
    }
    private static bool ReadRequestOriginal(EntityPlayer actor,XElement body,out RebirthRemoteResourceClientOriginal.Intent original)
    {
        original=null;
        int stage=(int?)body.Attribute("stage")??-1;
        if(stage!=5&&stage!=6)return RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal")?.ToString(SaveOptions.DisableFormatting),out original);
        var reference=body.Element("originalRef");
        if(reference==null||reference.Attributes().Count()!=3||reference.Nodes().Any()||
            !Guid.TryParseExact((string)reference.Attribute("operation"),"N",out var operation)||!Guid.TryParseExact((string)reference.Attribute("source"),"N",out var source)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var root,out bool unknown)||unknown||root==null||
            !TryReadAdmission(root,out var retained)||retained.Operation!=operation||retained.Digest!=(string)reference.Attribute("digest"))return false;
        original=retained;return true;
    }
    private static void ReplyFetched(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,bool refundPermission=false,string refundDigest="")
    {
        if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return;
        if(root?.Phase==4&&(string)root.Copy().Attribute("disposition")=="used"&&
            (!TryObservePublication(actor.world,root,out var savedPublication)||(string)root.Copy().Attribute("ownerProof")!=savedPublication))return;
        var image=root?.Copy();Guid source=root?.Operation??RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        var reply=new XElement("fetchedStationResult",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),new XAttribute("digest",original.Digest),
            new XAttribute("sourceWorld",world),new XAttribute("source",source.ToString("N")),new XAttribute("binding",root?.Binding??""),new XAttribute("phase",root?.Phase??0),
            new XAttribute("outcome",(int?)image?.Attribute("outcome")??0),new XAttribute("disposition",(string)image?.Attribute("disposition")??""),
            new XAttribute("proof",(string)image?.Attribute("ownerProof")??""),new XAttribute("rejecting",(int?)image?.Attribute("ownerRejecting")??0),
            new XAttribute("noUseProof",(string)image?.Attribute("ownerNoUseProof")??""),new XAttribute("refundPermission",refundPermission),new XAttribute("refundDigest",refundDigest),
            new XElement("paid",image?.Element("removed")?.Value??Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(Array.Empty<ItemStack>()))));
        NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,reply.ToString(SaveOptions.DisableFormatting));
    }
}
