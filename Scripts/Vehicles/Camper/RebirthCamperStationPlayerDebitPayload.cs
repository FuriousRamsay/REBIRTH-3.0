using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Authenticated server-authored debit only; detached parsing is not custody.
internal static class RebirthCamperStationPlayerDebitPayload
{
    private const int Maximum=4*1024*1024;
    internal static bool TryCreate(EntityPlayer player,RebirthStablePlayerIdentity identity,
        RebirthStationGridAdmission admission,out byte[] payload)
    {
        payload=null;
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||player==null||identity==null||player.world==null||player.world.IsRemote()||
                !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
                player.IsDead()||!RebirthWorldCharacterService.TryGetIdentity(player,out var currentOwner)||
                currentOwner.StorageKey!=identity.StorageKey||currentOwner.CanonicalId!=identity.CanonicalId||
                !Valid(identity,admission)||RebirthCamperCharacterAccess.CurrentCreation(player)!=admission.CreationId)return false;
            var node=new XElement("camperPlayerDebit",new XAttribute("version",1),new XAttribute("actor",player.entityId),
                new XAttribute("owner",identity.StorageKey),new XAttribute("canonical",identity.CanonicalId),admission.Write());
            byte[] bytes=Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));
            if(bytes.Length<1||bytes.Length>Maximum)return false;payload=bytes;return true;
        }
        catch{return false;}
    }
    private static bool Valid(RebirthStablePlayerIdentity identity,RebirthStationGridAdmission admission)
        =>admission!=null&&admission.IsPublicationAttempted&&admission.TryGetMobileAddress(out _)&&
            admission.TryGetPlayerPayment(out var image)&&image.OwnerKey==identity.StorageKey&&image.OwnerId==identity.CanonicalId&&
            RebirthCamperStationPlayerPayment.Operation(admission)!=Guid.Empty;
    private static bool TryRead(byte[] payload,RebirthStablePlayerIdentity identity,int actor,Guid operation,
        out RebirthStationGridAdmission admission)
    {
        admission=null;
        try
        {
            if(identity==null||actor<=0||operation==Guid.Empty||payload==null||payload.Length<1||payload.Length>Maximum)return false;
            XElement node;
            var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=Maximum};
            using(var stream=new MemoryStream(payload,false))using(var xml=XmlReader.Create(stream,settings))node=XElement.Load(xml);
            if(node.Name!="camperPlayerDebit"||node.Attributes().Count()!=4||(string)node.Attribute("version")!="1"||
                (int?)node.Attribute("actor")!=actor||(string)node.Attribute("owner")!=identity.StorageKey||
                (string)node.Attribute("canonical")!=identity.CanonicalId||node.Elements().Count()!=1||
                node.Elements("stationAdmission").Count()!=1||node.Nodes().Any(n=>!(n is XElement)&&(!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)))||
                !RebirthStationGridAdmission.TryReadStored(node.Element("stationAdmission"),out admission)||!Valid(identity,admission)||
                RebirthCamperStationPlayerPayment.Operation(admission)!=operation)return false;
            return true;
        }
        catch{admission=null;return false;}
    }
    internal static bool TryApply(EntityPlayerLocal player,Guid operation,byte[] payload)
    {
        if(!ThreadManager.IsMainThread()||player==null||player.world?.IsRemote()!=true||
            !ReferenceEquals(GameManager.Instance?.World,player.world)||!ReferenceEquals(player.world.GetPrimaryPlayer(),player))return false;
        if(RebirthCamperStationNativeActorPayment.LooksNativePayload(payload))
            return !RebirthSurvivorMode.IsEnabledForCurrentWorld()&&RebirthCamperStationNativeActorPayment.TryApplyRemote(player,operation,payload);
        return RebirthSurvivorMode.IsEnabledForCurrentWorld()&&RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)&&
            TryRead(payload,identity,player.entityId,operation,out var admission)&&RebirthCamperStationPlayerPayment.TryApplyRemote(player,operation,admission);
    }
    internal static bool VerifySaved(EntityPlayer player,Guid operation,byte[] payload,Func<bool> current)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||current==null||!current())return false;
            if(RebirthCamperStationNativeActorPayment.LooksNativePayload(payload))
                return !RebirthSurvivorMode.IsEnabledForCurrentWorld()&&RebirthCamperStationNativeActorPayment.VerifyRemoteSaved(player,operation,payload,current);
            if(!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
            return current!=null&&current()&&RebirthWorldCharacterService.TryGetIdentity(player,out var identity)&&
                TryRead(payload,identity,player.entityId,operation,out var admission)&&
                RebirthCamperCharacterAccess.CurrentCreation(player)==admission.CreationId&&
                RebirthCamperStationPlayerFileWitness.HasSaved(identity,admission,operation,true)&&current();
        }
        catch{return false;}
    }
}
