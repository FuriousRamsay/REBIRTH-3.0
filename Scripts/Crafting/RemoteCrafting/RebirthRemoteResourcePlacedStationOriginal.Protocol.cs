using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static bool protocolInfrastructure;
    internal static void InstallProtocolInfrastructure()
    {
        lock(protocolInstallGate)
        {
            if(protocolInfrastructure)return;
            if(GameManager.Instance?.World!=null)throw new InvalidOperationException("Placed lifecycle observers must install before native world creation.");
            RebirthRemoteResourceDurableDispatch.Install();RebirthRemoteResourceRepairProtocol.Install();
            RebirthCookingOutputDelivery.Install();if(!RebirthCookingOutputDelivery.IsInstalled)throw new InvalidOperationException("Native owner custody guards unavailable.");
            RebirthWorkstationDamagePolicy.Install(); // Existing sole damage/explosion guard dispatch; exact C holds compose there.
            RebirthLogisticsEndpointIdentity.Install();RebirthRemoteResourcePlacedStationGuards.Install();
            RebirthRemoteResourcePlacedStationClientCustody.Install();RebirthRemoteResourcePlacedStationEligibility.Install();
            RebirthProductiveEarnedUiOwnerFile.Install(); // Sole actual FromPlayer/Save callbacks, including C owner checkpoints.
            RebirthProductiveEarnedCancellation.InstallBeforeFlushObserver();
            RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver(); // Sole actual native receive-dequeue lineage, including PlayerId.
            InstallAcceptedUpload();
            InstallCancellation();
            InstallCancellationReentry();
            ModEvents.GameUpdate.RegisterHandler(ClientUpdate);protocolInfrastructure=true;
            // Deliberately does NOT activate. Only the separate explicit
            // source-review decision may enable before world creation.
        }
    }
    internal static void Request(EntityPlayer actor,string text)
    {
        if(!ProtocolActive||!RebirthRemoteResourceRepairProtocol.SupportsServerActor(actor))return;
        try
        {
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,24*1024*1024);
            if(body.Name!="placedStationRequest"||body.Attributes().Count()!=2||(int?)body.Attribute("version")!=1||body.Elements().Count()!=1||
                (int?)body.Attribute("stage") is not int stage||stage<0||stage>4||
                !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(body.Element("fetchClientOriginal")?.ToString(SaveOptions.DisableFormatting),out var original)||
                !IsCandidate(original)||
                !XNode.DeepEquals(body,new XElement("placedStationRequest",new XAttribute("version",1),new XAttribute("stage",stage),original.Copy())))return;
            if(stage==4){RequestEndedNeverEntered(actor,original);return;}
            if(!ServerScope(actor,original))return;
            if(stage==3){ArmBeforeUpload(actor,original);return;}
            if(TryRouteColdTerminal(actor,original,stage))return;
            if(!Admit(actor,original))return;
            var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("remoteRequirements").Value));
            if(!RebirthRemoteResourceDurableDispatch.Resolve(actor,original.Epoch,original.Request,original.Kind,remote,stage!=0,out var root)||root==null)
            {ReplyProgress(actor,original,null);return;}
            if(root.Phase<2){ReplyProgress(actor,original,root);return;}
            if(TryReplyNeverEntered(actor,original,root))return;
            if(root.Phase==4)
            {if(TryReplyUsed(actor,root,out string used))NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,stage==2?RetireUsedReply(actor,root,used):used);return;}
            if(root.Phase!=2)return; // No no-effect/terminal financial shortcut.
            if(root.HasOwnerAction)
            {
                // Only positive same-primary native2/full After can begin the
                // typed host commit. A pending/unknown async publication never
                // becomes a refund or a legacy native queue callback.
                AdvanceWorldApplication(actor,root);
                if(!RebirthRemoteResourceDurableJournal.TryRead(root.Owner,root.Operation,out root,out bool pendingUnknown)||pendingUnknown||root==null)return;
                if(root.Phase==4){if(TryReplyUsed(actor,root,out string used))NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,used);return;}
                if(root.Phase!=2)return;
            }
            if(!PrepareOwnerPermission(actor,root)||!RebirthRemoteResourceDurableJournal.TryRead(root.Owner,root.Operation,out var admitted,out bool unknown)||unknown||admitted==null||
                !TryReplyPaid(actor,admitted,out string reply))return;
            NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,reply);
        }
        catch{/* Unknown retains the original; no legacy queue/bag fallback. */}
    }
    private static void ReplyProgress(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root)
    {
        if(!ServerScope(actor,original)||root!=null&&root.Phase>=2)return;
        var reply=new XElement("placedStationProgress",new XAttribute("version",1),new XAttribute("original",original.Operation.ToString("N")),
            new XAttribute("digest",original.Digest),new XAttribute("phase",root?.Phase??0));
        NetPackageRebirthRemoteRepairResult.Send(actor.entityId,original,reply.ToString(SaveOptions.DisableFormatting));
    }
}
