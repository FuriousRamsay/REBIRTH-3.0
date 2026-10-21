using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    [ThreadStatic]private static NativeAddOriginal acceptingNativeOwnerTerminal;
    private static void QueryRemoteNativeOwner(EntityPlayer viewer,ClientInfo sender,string key,string creation,Guid operation,XElement request)
    {
        if(!nativeHostOriginals.ContainsKey(operation))
        {QueryHistoricalRemoteNativeOwner(viewer,key,creation,operation,request);return;}
        if(!nativeHostOriginals.TryGetValue(operation,out var source)||!ReferenceEquals(source.Viewer,viewer)||!ReferenceEquals(source.Sender,sender)||
            source.ViewerKey!=key||source.ViewerCreation!=creation||!source.CurrentPeer()||!source.RemoteOwnerTerminalSaved||
            (string)request.Attribute("nonce")!=source.Nonce.ToString("N")||(string)request.Attribute("source")!=source.SourceHash)return;
        Ack(viewer,operation,"nativeOwnerTerminal",source.EarlierOwner.OriginalOwnerKey,source.EarlierOwner.Creation,source.SourceHash,
            (string)request.Attribute("challenge"),RebirthProductiveEarnedUiQueueOwner.Export(source.EarlierOwner),source);
    }
    private static void PumpRemoteNativeOwnerQueries(World world)
    {
        if(!world.IsRemote())return;
        foreach(var source in nativeSources.Values.ToArray())
        {
            if(!ReferenceEquals(source.World,world)||source.RemoteNativeNonce==Guid.Empty||source.RemoteTerminalSaved||!source.SourceCurrentWithoutRow()||
                !source.HasConfirmedNativeDelivery)continue;
            Send(true,new XElement("nativeOwnerQuery",new XAttribute("nonce",source.RemoteNativeNonce.ToString("N")),
                new XAttribute("source",source.RemoteNativeSourceHash)),source.Operation,world.GetPrimaryPlayer().entityId);
        }
    }
    private static void AcceptRemoteNativeOwnerAck(EntityPlayer viewer,Guid operation,XElement ack)
    {
        if(!nativeSources.ContainsKey(operation)){AcceptColdNativeOwnerAck(viewer,operation,ack);return;}
        if(!TryFindRetainedNativeAddOriginal(operation,viewer.world,
            nativeSources.TryGetValue(operation,out var found)?found.Station:null,out var source)||source.RemoteTerminalSaved||
            (string)ack.Attribute("nativeNonce")!=source.RemoteNativeNonce.ToString("N")||(string)ack.Attribute("nativeSource")!=source.RemoteNativeSourceHash||
            (string)ack.Attribute("digest")!=source.RemoteNativeSourceHash||(string)ack.Attribute("owner")!=source.EarlierOwner.OriginalOwnerKey||
            (string)ack.Attribute("creation")!=source.EarlierOwner.Creation)return;
        acceptingNativeOwnerTerminal=source;
        try{source.SaveRemoteOwnerTerminalAck(ack);}finally{acceptingNativeOwnerTerminal=null;}
    }
    internal sealed partial class NativeAddOriginal
    {
        internal bool HasConfirmedNativeDelivery=>SourceCurrentWithoutRow()&&frame.NativeAccepted&&frame.ConfirmedOriginal?.IsConfirmed==true;
        internal bool SaveRemoteOwnerTerminalAck(XElement ack)
        {
            if(!ReferenceEquals(acceptingNativeOwnerTerminal,this)||!HasConfirmedNativeDelivery)return false;
            long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(before)+
                RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(frame.Intent.Element("confirmedOriginal"))+65536;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measured),out var workspace))return false;
            using(workspace)
            {
                if(!TryCopyConfirmedDelivery(out var delivery))return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),
                    new XAttribute("world",frame.Observation.WorldGuid),new XElement("before",new XElement(before)),
                    new XElement("nativeOwnerTerminalAck",new XElement(ack),new XElement("delivery",delivery)));
                if(!RebirthWorkstationCompletionPublication.Write(path+".remote-owner-ack.xml",record))return false;
                RemoteTerminalSaved=true;RebirthProductiveEarnedUiOwnerFile.ObserveRemoteNativeOwnerTerminal(this);return true;
            }
        }
    }
}

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    internal static void ObserveRemoteNativeOwnerTerminal(RebirthWorkstationCompletionPipeline.NativeAddOriginal source)
    {
        if(!ThreadManager.IsMainThread()||source?.World.IsRemote()!=true||!source.RemoteTerminalSaved)return;
        lock(gate)foreach(var member in deferred.Where(value=>ReferenceEquals(value.NativeOriginal,source)&&!value.Closed))member.OwnerTerminalSaved=true;
    }
}
