using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Server-authored two-stage payload only. Detached parsing is not server custody.
internal static class RebirthCamperStationRemoteRefundPayload
{
    private const int Maximum=8*1024*1024;
    internal static bool TryPrepareOperation(Guid delivery,out Guid operation)
    {
        operation=Guid.Empty;
        if(delivery==Guid.Empty)return false;
        using(var sha=SHA256.Create())
        {
            var hash=sha.ComputeHash(Encoding.UTF8.GetBytes("camper.remote.refund.prepare.v1:"+delivery.ToString("N")));
            var bytes=new byte[16];Array.Copy(hash,bytes,16);operation=new Guid(bytes);
        }
        return operation!=Guid.Empty&&operation!=delivery;
    }
    internal static bool TryCreate(RebirthCamperStationCancellationCheckpoint checkpoint,
        RebirthCamperStationCancellationRecord cancelled,RebirthCamperStationRefundDeliveryRecord delivery,bool prepare,out byte[] payload)
    {
        payload=null;
        try
        {
            var b=checkpoint?.Binding;
            if(!ThreadManager.IsMainThread()||b==null||cancelled==null||delivery==null||
                !checkpoint.MatchesStagedOwner()||prepare==delivery.IsAttempted||delivery.IsSaved||
                !TryPrepareOperation(delivery.DeliveryId,out _))return false;
            return TryCreate(b.Player,b.Identity,b.Admission,b.Publication,checkpoint.CopyIntent(),checkpoint.CopyRefund(),
                checkpoint.CopyAttempt(),cancelled,delivery,prepare,out payload)&&checkpoint.MatchesStagedOwner();
        }
        catch{return false;}
    }
    // Caller supplies current authenticated cold/original custody; this helper
    // only validates and encodes its existing exact parent records.
    internal static bool TryCreate(EntityPlayer player,RebirthStablePlayerIdentity identity,RebirthStationGridAdmission admission,
        RebirthCamperStationPublicationRecord queued,RebirthStationTerminalIntent intent,RebirthStationCancellationRefund refund,
        RebirthStationCancellationAttempt attempt,RebirthCamperStationCancellationRecord cancelled,
        RebirthCamperStationRefundDeliveryRecord delivery,bool prepare,out byte[] payload)
    {
        payload=null;
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||identity==null||delivery==null||prepare==delivery.IsAttempted||delivery.IsSaved||
                !RebirthCamperStationRefundDeliveryRecord.TryRead(delivery.Write(),identity.StorageKey,admission,queued,intent,refund,attempt,cancelled,out _)||
                !TryPrepareOperation(delivery.DeliveryId,out _))return false;
            var node=new XElement("camperRemoteRefund",new XAttribute("version",1),new XAttribute("stage",prepare?"prepare":"apply"),
                new XAttribute("owner",identity.StorageKey),new XAttribute("canonical",identity.CanonicalId),
                new XAttribute("actor",player.entityId),admission.Write(),queued.Write(),intent.Write(),
                refund.Write(),attempt.Write(),cancelled.Write(),delivery.Write());
            var bytes=Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));
            if(bytes.Length<1||bytes.Length>Maximum)return false;
            payload=bytes;return true;
        }
        catch{return false;}
    }
    internal static bool TryApplyRefund(EntityPlayerLocal player,Guid operation,byte[] payload)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||player==null||player.world==null||!player.world.IsRemote()||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player)||
                player.IsDead()||RebirthCharacterCreationHoldService.IsHeld(player)||player.Buffs?.CVars==null||
                operation==Guid.Empty||payload==null||payload.Length<1||payload.Length>Maximum||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return false;
            XElement node;
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum};
            using(var stream=new MemoryStream(payload,false))
            using(var xml=XmlReader.Create(stream,settings))node=XElement.Load(xml);
            string[] names={"stationAdmission","camperStationPublication","stationTerminalIntent","stationCancellationRefund",
                "stationCancellationAttempt","camperStationCancellation","camperStationRefundDelivery"};
            if(node.Name!="camperRemoteRefund"||node.Attributes().Count()!=5||(string)node.Attribute("version")!="1"||
                node.Elements().Count()!=7||names.Any(name=>node.Elements(name).Count()!=1)||
                node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                (string)node.Attribute("owner")!=identity.StorageKey||(string)node.Attribute("canonical")!=identity.CanonicalId||
                (int?)node.Attribute("actor")!=player.entityId||
                !RebirthStationGridAdmission.TryReadStored(node.Element(names[0]),out var admission)||!admission.TryGetMobileAddress(out _)||
                !RebirthCamperStationPublicationRecord.TryRead(node.Element(names[1]),admission,out var queued)||
                !RebirthStationTerminalIntent.TryRead(node.Element(names[2]),admission,out var intent)||intent.IsCompletion||
                !RebirthStationCancellationRefund.TryReadStored(node.Element(names[3]),admission,intent,out var refund)||
                !RebirthStationCancellationAttempt.TryRead(node.Element(names[4]),admission,intent,refund,out var attempt)||
                !RebirthCamperStationCancellationRecord.TryRead(node.Element(names[5]),admission,queued,intent,refund,attempt,out var cancelled)||
                !RebirthCamperStationRefundDeliveryRecord.TryRead(node.Element(names[6]),identity.StorageKey,admission,queued,intent,refund,attempt,cancelled,out var delivery)||
                delivery.IsSaved||!delivery.TryCopyPlan(refund,out var plan)||!TryPrepareOperation(delivery.DeliveryId,out var prepareOperation)||
                !RebirthSurvivorRequestScope.Matches(admission.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player)))return false;
            bool prepare=(string)node.Attribute("stage")=="prepare";
            if((string)node.Attribute("stage")!=(prepare?"prepare":"apply")||prepare==delivery.IsAttempted||
                operation!=(prepare?prepareOperation:delivery.DeliveryId))return false;
            var world=player.world;var bag=player.bag;int actor=player.entityId;
            Func<bool,bool> current=after=>ThreadManager.IsMainThread()&&player.entityId==actor&&ReferenceEquals(player.world,world)&&
                ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(world.GetPrimaryPlayer(),player)&&
                ReferenceEquals(player.bag,bag)&&!player.IsDead()&&!RebirthCharacterCreationHoldService.IsHeld(player)&&
                RebirthSurvivorRequestScope.Matches(admission.CreationId,RebirthSurvivorClientState.GetProjectedCreationId(player))&&
                bag?.ItemGrid?.items!=null&&(after?plan.MatchesAfter(bag.ItemGrid.items):
                    plan.MatchesBefore(bag.ItemGrid.items,bag.ItemGrid.items.Length,slot=>bag.LockedSlots!=null&&slot<bag.LockedSlots.Length&&bag.LockedSlots[slot]));
            string key="rbStationRefund_"+delivery.DeliveryId.ToString("N");
            bool present=player.Buffs.CVars.TryGetValue(key,out var phase);
            if(prepare)
            {
                if(!current(false)||present&&phase!=3f)return false;
                if(!present)player.Buffs.SetCustomVar(key,3f,true);
                return current(false)&&player.Buffs.CVars.TryGetValue(key,out phase)&&phase==3f;
            }
            if(present&&phase==1f)return current(true); // exact native after-image only, no second credit
            if(!present||phase!=3f||!current(false))return false;
            var permit=new RebirthStationRefundPreparationService.ApplicationPermit(player,plan,key,current,()=>false,()=>false,3f,
                ()=>RebirthCamperStationPartialCancellationRewards.ApplyNative(player,admission,intent,key));
            // Transport publishes native owner data after success. Server alone
            // confirms its final primary file and saved owner delivery phase.
            return permit.TryApply();
        }
        catch{return false;}
    }
}
