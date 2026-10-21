using System;using System.Linq;using System.Xml.Linq;
internal static partial class RebirthWorkstationCompletionPipeline
{
    private static XElement RemoteOutputFinalQueueReceipt(RemoteOutputHostSource source)=>new XElement("workstationCompletionIntent",
        new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",(string)source.Original.Attribute("world")),
        new XElement("before",new XElement("remoteOutputFinalQueueSaved",new XAttribute("version",1),new XAttribute("nonce",source.Nonce.ToString("N")),
            new XAttribute("source",source.Hash),new XAttribute("request",RemoteOutputRequestDigest(source.Request)),
            new XAttribute("native",HistoricalHash(source.FinalQueueBody)),new XAttribute("localOperation",(string)source.FinalLocalPrefix.Attribute("operation")),
            new XAttribute("original",(string)source.FinalLocalPrefix.Attribute("original")),new XAttribute("count",source.PrefixCount))));
    private static bool RemoteOutputFinalQueueSaved(RemoteOutputHostSource source)
        =>source?.FinalCallbackSaved==true&&source.Current()&&XNode.DeepEquals(RemoteOutputXml(source.Path+".final-queue-terminal"),RemoteOutputFinalQueueReceipt(source));
    private static bool TryAcknowledgeRemoteFinalQueue(RemoteOutputHostSource source,EntityPlayer viewer,ClientInfo peer,XElement request)
    {
        if(source?.FinalCallbackSaved!=true||!source.Current()||viewer==null||peer==null||viewer.IsDead()||!viewer.IsSpawned()||
            !ReferenceEquals(viewer.world,source.World)||!ReferenceEquals(source.World.GetEntity(viewer.entityId),viewer)||viewer.Died!=source.Deaths||
            !peer.loginDone||peer.disconnecting||peer.entityId!=viewer.entityId||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),peer)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var owner)||owner.StorageKey!=source.Viewer||
            RebirthCamperCharacterAccess.CurrentCreation(viewer)!=source.Creation||
            !RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(peer.latestPlayerData,source.Request.Element("nativeOwnerFrame")))return false;
        if(!RemoteOutputFinalQueueSaved(source))
        {
            if(source.FinalQueueNative?.Current()!=true||!RebirthWorkstationCompletionPublication.Write(source.Path+".final-queue-terminal",RemoteOutputFinalQueueReceipt(source))||
                !RemoteOutputFinalQueueSaved(source))return false;
        }
        // The loaded queue already contains the exact original decrement and
        // the owner callback is saved. This retires metadata only; no event,
        // EarlierOwner witness, native queue setter or callback is fabricated.
        source.Admitted=true;remoteOutputHostHolds.RemoveAll(value=>ReferenceEquals(value.World,source.World)&&value.Operation==source.Operation);
        var local=source.FinalLocalPrefix;
        Send(false,new XElement("ack",new XAttribute("world",(string)source.Original.Attribute("world")),new XAttribute("viewerCreation",source.Creation),
            new XAttribute("challenge",(string)request.Attribute("challenge")??""),new XAttribute("stage","remoteOutputRestartReady"),
            new XAttribute("nativeNonce",source.Nonce.ToString("N")),new XAttribute("nativeSource",source.Hash),new XAttribute("localOperation",(string)local.Attribute("operation")),
            new XAttribute("original",(string)local.Attribute("original")),new XAttribute("count",source.PrefixCount),new XAttribute("generation",(string)source.Request.Attribute("generation")),
            new XAttribute("prefixDigest",RemoteOutputRequestDigest(source.Request)),new XAttribute("finalCallback",true),new XAttribute("queueTerminal",true)),source.Operation,viewer.entityId);
        return true;
    }
}
