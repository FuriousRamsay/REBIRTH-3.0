using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private static void ReceiveRemoteOutputRestart(EntityPlayer viewer,ClientInfo peer,string owner,string creation,Guid operation,XElement request)
    {
        if(viewer==null||peer==null||viewer.world?.IsRemote()!=false||viewer.IsDead()||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),peer)||
            !RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||identity.StorageKey!=owner||
            RebirthCamperCharacterAccess.CurrentCreation(viewer)!=creation)return;
        var found=remoteOutputHostSources.Where(value=>!value.Closed&&value.Operation==operation&&value.Viewer==owner&&value.Creation==creation&&
            value.Deaths==viewer.Died&&value.Nonce.ToString("N")==((string)request.Attribute("nonce"))&&value.Hash==((string)request.Attribute("source"))&&value.Current()).ToArray();
        if(found.Length!=1)return;var source=found[0];var prefix=source.FinalCallbackSaved?source.FinalLocalPrefix:source.Request.Element("nativeOutputPrefix");
        if((string)request.Attribute("localOperation")!=(string)prefix.Attribute("operation")||
            (string)request.Attribute("original")!=(string)prefix.Attribute("original")||(int?)request.Attribute("count")!=source.PrefixCount||
            (string)request.Attribute("prefixDigest")!=RemoteOutputRequestDigest(source.Request)||
            ((bool?)request.Attribute("finalCallback")??false)!=source.FinalCallbackSaved)return;
        if(source.FinalCallbackSaved&&TryAcknowledgeRemoteFinalQueue(source,viewer,peer,request))return;
        NativeHostOriginal original;
        if(!nativeHostOriginals.TryGetValue(operation,out original))original=NativeHostOriginal.FromRemoteOutputRestart(source,viewer,peer);
        if(original==null||!original.IsRemoteOutputRestart(source)||!ReferenceEquals(original.Viewer,viewer)||!ReferenceEquals(original.Sender,peer)||!original.CurrentViewer())return;
        var ack=new XElement("ack",new XAttribute("world",original.WorldGuid),new XAttribute("viewerCreation",creation),
            new XAttribute("challenge",(string)request.Attribute("challenge")??""),new XAttribute("stage","remoteOutputRestartReady"),
            new XAttribute("nativeNonce",original.Nonce.ToString("N")),new XAttribute("nativeSource",original.SourceHash),
            new XAttribute("localOperation",(string)prefix.Attribute("operation")),new XAttribute("original",(string)prefix.Attribute("original")),
            new XAttribute("count",source.PrefixCount),new XAttribute("generation",(string)source.Request.Attribute("generation")),
            new XAttribute("prefixDigest",RemoteOutputRequestDigest(source.Request)),new XAttribute("queueOwnerPurpose",RebirthProductiveEarnedUiQueueOwner.Export(original.EarlierOwner)),
            new XAttribute("finalCallback",source.FinalCallbackSaved),new XAttribute("queueOnly",source.FinalCallbackSaved));
        Send(false,ack,operation,viewer.entityId);
    }
}
