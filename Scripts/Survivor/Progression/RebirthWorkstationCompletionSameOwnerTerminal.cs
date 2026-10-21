using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    [ThreadStatic]private static NativeHostOriginal issuingSameOwnerReceived;
    internal sealed class SameOwnerReceived
    {
        internal readonly NativeHostOriginal Source;private readonly string path;private readonly XElement image;
        private SameOwnerReceived(NativeHostOriginal source,string path,XElement image){Source=source;this.path=path;this.image=new XElement(image);}
        internal bool Current()=>ThreadManager.IsMainThread()&&!Source.Closed&&
            XNode.DeepEquals(RebirthWorkstationCompletionPublication.ReadPrimary(path),image);
        internal static SameOwnerReceived Issue(NativeHostOriginal source,string path,XElement image)=>
            source!=null&&ReferenceEquals(issuingSameOwnerReceived,source)?new SameOwnerReceived(source,path,image):null;
    }
    private static void PumpSameOwnerTerminalQueries(World world)
    {
        if(!world.IsRemote())return;
        PumpCachedSameOwnerCouriers(world);
        foreach(var frame in frames.Values.ToArray())
        {
            if(frame.NativeViewerEffect==null||frame.RemoteNativeNonce==Guid.Empty||!frame.NativeAccepted||
                frame.ConfirmedOriginal?.IsConfirmed!=true||!ReferenceEquals(frame.Observation.World,world)||
                frame.SameOwnerTerminalReleased||UnityEngine.Time.realtimeSinceStartup<frame.SameOwnerTerminalNextQuery||
                frame.NativeViewerEffect.MeasureSavedEvidence()<1)continue;
            frame.SameOwnerTerminalNextQuery=UnityEngine.Time.realtimeSinceStartup+1f;
            SendSameOwnerQuery(frame,frame.SameOwnerTerminalAckSaved);
        }
    }
    private static void SendSameOwnerQuery(Frame frame,bool received)
    {
        var request=new XElement("nativeSameOwnerQuery",new XAttribute("stage",received?"received":"query"),
            new XAttribute("nonce",frame.RemoteNativeNonce.ToString("N")),new XAttribute("source",frame.RemoteNativeSourceHash));
        if(received)request.Add(new XAttribute("terminal",frame.SameOwnerTerminalDigest));
        Send(true,request,frame.Observation.Operation,frame.Observation.Viewer.entityId);
    }
    private static void AcceptSameOwnerTerminal(EntityPlayer viewer,Guid operation,XElement ack)
    {
        if(AcceptCachedSameOwnerTerminal(viewer,operation,ack))return;
        if(!frames.TryGetValue(operation,out var frame)||frame.NativeViewerEffect==null||!frame.NativeAccepted||
            frame.ConfirmedOriginal?.IsConfirmed!=true||!ReferenceEquals(frame.Observation.Viewer,viewer)||
            (string)ack.Attribute("nativeNonce")!=frame.RemoteNativeNonce.ToString("N")||
            (string)ack.Attribute("nativeSource")!=frame.RemoteNativeSourceHash||
            (string)ack.Attribute("owner")!=frame.EarlierOwner.OriginalOwnerKey||
            (string)ack.Attribute("creation")!=frame.EarlierOwner.Creation)return;
        string digest=(string)ack.Attribute("digest");
        if(digest?.Length!=64||digest.Any(ch=>!(ch>='0'&&ch<='9'||ch>='a'&&ch<='f')))return;
        if((string)ack.Attribute("stage")=="nativeSameOwnerRetired")
        {if(frame.SameOwnerTerminalAckSaved&&frame.SameOwnerTerminalDigest==digest)frame.SameOwnerTerminalReleased=true;return;}
        long measured=frame.NativeViewerEffect.MeasureSavedEvidence();
        if(measured<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measured+16L*MeasureIntent(frame.Intent)+65536),out var workspace))return;
        using(workspace)
        {
            var effect=frame.NativeViewerEffect.CopySavedEvidence();if(effect==null)return;
            var receipt=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),
                new XAttribute("world",frame.Observation.WorldGuid),new XElement("before",new XElement(frame.Intent)),
                new XElement("nativeSameOwnerAck",new XAttribute("nonce",frame.RemoteNativeNonce.ToString("N")),
                    new XAttribute("source",frame.RemoteNativeSourceHash),new XAttribute("terminal",digest),
                    effect,frame.ConfirmedOriginal.Write()));
            string path=frame.Path+".same-owner-ack";
            if(!RebirthWorkstationCompletionPublication.Write(path,receipt)||
                !XNode.DeepEquals(RebirthWorkstationCompletionPublication.ReadPrimary(path),receipt))return;
            frame.SameOwnerTerminalDigest=digest;frame.SameOwnerTerminalAckSaved=true;SendSameOwnerQuery(frame,true);
        }
    }
    private static long MeasureIntent(XElement image)=>RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(image);
    private static void QuerySameOwnerTerminal(EntityPlayer viewer,ClientInfo peer,string owner,string creation,Guid operation,XElement request)
    {
        string stage=(string)request.Attribute("stage");if(stage!="query"&&stage!="received")return;
        try
        {
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",operation.ToString("N")+".xml");
            nativeHostOriginals.TryGetValue(operation,out var source);
            if(source!=null)
            {
                if(!RebirthProductiveEarnedUiDeferredTransport.TryGetSavedSameOwnerTerminal(source,out _)&&
                    !RebirthProductiveEarnedUiDeferredTransport.TryGetSavedCachedSameOwnerTerminal(source,out _))return;
            }
            long originalLength=RebirthWorkstationCompletionPublication.PublishedLength(path),
                terminalLength=RebirthWorkstationCompletionPublication.PublishedLength(path+".same-owner-terminal");
            if(originalLength<1||terminalLength<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(
                checked(16*(originalLength+terminalLength)+65536),out var workspace))return;
            using(workspace)
            {
                var original=RebirthWorkstationCompletionPublication.ReadPrimary(path);
                var completed=RebirthWorkstationCompletionPublication.ReadPrimary(path+".same-owner-terminal");
                var before=original?.Element("before")?.Element("remoteNativeOriginal");
                var terminal=completed?.Element("before")?.Element("nativeSameOwnerSaved");
                var originalBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                var terminalBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path+".same-owner-terminal");
                string world=RebirthWorkstationCompletionOutbox.CurrentGuid(viewer.world),op=operation.ToString("N"),
                    nonce=(string)request.Attribute("nonce"),identity=(string)request.Attribute("source");
                if(before==null||terminal==null||originalBytes==null||terminalBytes==null||
                    (string)original.Attribute("operation")!=op||(string)completed.Attribute("operation")!=op||
                    (string)original.Attribute("world")!=world||(string)completed.Attribute("world")!=world||
                    (string)before.Attribute("viewer")!=owner||(string)before.Attribute("creation")!=creation||
                    (string)before.Attribute("nonce")!=nonce||(string)terminal.Attribute("nonce")!=nonce||
                    (string)terminal.Attribute("source")!=identity||HistoricalHash(originalBytes)!=identity)return;
                if(source!=null&&(source.SourceHash!=identity||source.Nonce.ToString("N")!=nonce))return;
                string digest=HistoricalHash(terminalBytes);
                if(stage=="received")
                {
                    if((string)request.Attribute("terminal")!=digest)return;
                    var record=new XElement("workstationCompletionIntent",new XAttribute("operation",op),new XAttribute("world",world),
                        new XElement(original.Element("before")),
                        new XElement("nativeSameOwnerReceived",new XAttribute("nonce",nonce),new XAttribute("source",identity),
                            new XAttribute("terminal",digest),new XAttribute("owner",owner),new XAttribute("creation",creation)));
                    string received=path+".same-owner-received";
                    if(!RebirthWorkstationCompletionPublication.Write(received,record)||
                        !XNode.DeepEquals(RebirthWorkstationCompletionPublication.ReadPrimary(received),record))return;
                    if(RebirthProductiveEarnedUiDeferredTransport.TryGetSavedCachedSameOwnerTerminal(operation,out var cachedTerminal))
                    {
                        issuingCachedPhysicalReceived=cachedTerminal;
                        try
                        {
                            var cachedReceived=CachedPhysicalReceived.Issue(cachedTerminal,received,record);
                            if(cachedReceived==null||!RetireCachedSameOwnerReceived(cachedReceived))return;
                        }
                        finally{issuingCachedPhysicalReceived=null;}
                    }
                    else if(source!=null)
                    {
                        issuingSameOwnerReceived=source;
                        try{var proof=SameOwnerReceived.Issue(source,received,record);if(proof==null||!RebirthProductiveEarnedUiDeferredTransport.RetireSavedSameOwnerResources(proof))return;}
                        finally{issuingSameOwnerReceived=null;}
                    }
                    Send(false,new XElement("ack",new XAttribute("stage","nativeSameOwnerRetired"),new XAttribute("world",world),
                        new XAttribute("viewerCreation",creation),new XAttribute("owner",owner),new XAttribute("creation",creation),
                        new XAttribute("nativeNonce",nonce),new XAttribute("nativeSource",identity),new XAttribute("digest",digest),
                        new XAttribute("challenge",(string)request.Attribute("challenge")??"")),operation,viewer.entityId);
                    return;
                }
                Send(false,new XElement("ack",new XAttribute("stage","nativeSameOwnerTerminal"),new XAttribute("world",world),
                    new XAttribute("viewerCreation",creation),new XAttribute("owner",owner),new XAttribute("creation",creation),
                    new XAttribute("nativeNonce",nonce),new XAttribute("nativeSource",identity),new XAttribute("digest",digest),
                    new XAttribute("challenge",(string)request.Attribute("challenge")??"")),operation,viewer.entityId);
            }
        }
        catch{/* Missing or conflicting saved metadata never grants a native action. */}
    }
}
