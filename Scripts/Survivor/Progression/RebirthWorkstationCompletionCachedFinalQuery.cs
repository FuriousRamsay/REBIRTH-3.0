using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Extends the existing settled-cache notice query. The caller has already
    // qualified that notice, saved native physical seal and creator settlement.
    // This binds the exact final owner callback; it issues no live action.
    private static XElement DescribeCachedFinalQuery(EntityPlayer actor,Guid operation,XElement request,string cachePath,
        string cachedHash,XElement cachedOriginal,XElement physical,Func<long,bool> reserve)
    {
        try
        {
            if(actor?.world?.IsRemote()!=false||actor.IsDead()||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
                !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor))return null;
            if((bool?)request.Attribute("finalCallback")!=true||(int?)request.Attribute("count") is not int count||count<1||count>65||
                !Guid.TryParseExact((string)request.Attribute("localOperation"),"N",out var local)||local==Guid.Empty||
                (string)request.Attribute("query")!=local.ToString("N")||
                (bool?)cachedOriginal.Element("cachedOriginal")?.Attribute("remoteCreator")!=true)return null;
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(actor.entityId);
            var before=cachedOriginal.Element("before")?.Elements().SingleOrDefault();var actual=physical?.Element("actualCachedPhysical");
            var proof=actual?.Element("cachedPhysicalActorSaved");var active=ActiveBefore(physical);
            if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||
                (string)before?.Attribute("viewer")!=identity.StorageKey||(string)before.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                (int?)before.Attribute("deaths")!=actor.Died||
                (string)physical?.Attribute("operation")!=operation.ToString("N")||(string)physical.Attribute("world")!=(string)cachedOriginal.Attribute("world")||
                (string)proof?.Attribute("operation")!=operation.ToString("N")||(string)proof.Attribute("nonce")!=(string)request.Attribute("nonce")||
                (string)proof.Attribute("generation")!=(string)request.Attribute("generation")||
                !RebirthWorkstationCompletionOriginal.TryRead(actual.Element("confirmedOriginal")?.Elements().SingleOrDefault(),out var delivered)||
                delivered.Operation!=operation.ToString("N")||delivered.ViewerKey!=identity.StorageKey||delivered.ViewerCreation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                !RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedPhysicalActor(cachePath,cachedHash,proof,active,
                    delivered.Write().Element("delivery")?.Elements().SingleOrDefault(),reserve))return null;
            string path=(string)proof.Attribute("actionPath")+".viewer."+(string)proof.Attribute("generation")+".after.intent";
            long length=RebirthWorkstationCompletionPublication.PublishedLength(path);if(length<1||!reserve(checked(16*length+65536)))return null;
            var last=RebirthWorkstationCompletionPublication.ReadPrimary(path)?.Element("before")?.Element("cachedViewer");
            var prefix=last?.Element("nativeOutputOriginal")?.Element("nativeOutputPrefix");
            if(last==null||(string)last.Attribute("stage")!="after"||(string)last.Attribute("action")!=(string)proof.Attribute("nonce")||
                (string)last.Attribute("source")!=cachedHash||(string)last.Attribute("generation")!=(string)proof.Attribute("generation")||
                RemoteOutputRequestDigest(last)!=(string)request.Attribute("prefixDigest")||
                (string)prefix?.Attribute("operation")!=local.ToString("N")||(string)prefix.Attribute("original")!=(string)request.Attribute("original")||
                (int?)prefix.Attribute("count")!=count||
                !RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(peer.latestPlayerData,last.Element("nativeOwnerFrame")))return null;
            return new XElement("ack",new XAttribute("world",(string)cachedOriginal.Attribute("world")),
                new XAttribute("viewerCreation",RebirthCamperCharacterAccess.CurrentCreation(actor)),new XAttribute("challenge",(string)request.Attribute("challenge")??""),
                new XAttribute("stage","cachedOutputFinalSettled"),new XAttribute("nativeNonce",(string)proof.Attribute("nonce")),new XAttribute("nativeSource",cachedHash),
                new XAttribute("generation",(string)proof.Attribute("generation")),new XAttribute("localOperation",local.ToString("N")),
                new XAttribute("original",(string)prefix.Attribute("original")),new XAttribute("count",count),new XAttribute("prefixDigest",RemoteOutputRequestDigest(last)),
                new XAttribute("finalCallback",true),new XAttribute("queueTerminal",true));
        }
        catch{return null;}
    }
}
