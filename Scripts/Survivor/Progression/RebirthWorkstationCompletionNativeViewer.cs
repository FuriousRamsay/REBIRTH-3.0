using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed partial class NativeViewerClient:IDisposable
    {
        internal Frame Frame;internal string Generation;internal long Session;internal INetConnection Connection;
        internal ViewerClientStage Before,After,CachedEffect;internal IDisposable Memory;
        internal string BagLocks,BeltLocks,BeltPreferred,EquipmentPreferred;internal int BeltCount;
        internal bool Current()
        {
            var o=Frame.Observation;return !Frame.Closed&&ReferenceEquals(GameManager.Instance?.World,o.World)&&o.World.IsRemote()&&
                o.Current(Frame.ConfirmedOriginal!=null)&&o.RecipeMatches()&&ReferenceEquals(CurrentNativeServerConnection(),Connection)&&!Connection.IsDisconnected()&&
                RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(o.Window,out var session)&&session==Session&&
                RebirthRecipeOutputIntegration.IsCurrentOwningObservedVersion(o.Card,Frame.ConfirmedOriginal==null&&!o.FullBoundary?ViewerPrefixVersion(this):o.NativeVersionAfter)&&
                ViewerLocks(o.Viewer.bag.LockedSlots)==BagLocks&&ViewerLocks(o.Viewer.inventory.ItemGrid.SlotLocks)==BeltLocks&&o.Viewer.inventory.Length==BeltCount&&
                (Frame.ConfirmedOriginal!=null||
                    RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.EncodePreferences(o.Viewer.inventory.preferredItemTypes)==BeltPreferred&&
                    RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.EncodePreferences(o.Viewer.equipment.preferredItemSlots)==EquipmentPreferred)&&
                (Frame.ConfirmedOriginal!=null||HasCurrentViewerPrefix(this)||(o.FullBoundary?!o.Continued&&Frame.SavedFullBoundaryVersion==o.NativeVersionAfter:
                    o.TryCopyBeforeEvidence(out var before)&&XNode.DeepEquals(before,ActiveBefore(Frame.Intent))));
        }
        public void Dispose(){DisposeViewerPrefix(this);Before?.ReleasePacket();After?.ReleasePacket();CachedEffect?.ReleasePacket();Memory?.Dispose();Memory=null;Before=After=CachedEffect=null;Frame=null;Connection=null;}
    }
    private sealed class ViewerClientStage
    {
        internal XElement Frame,Request;internal string Digest,NativeDigest;internal int NativeLength;internal bool Armed,Sent,Captured,Saved;internal double Next;
        internal NetPackagePlayerData Packet;internal bool Pinned;
        internal void ReleasePacket(){var packet=Packet;Packet=null;if(packet==null)return;if(Pinned){Pinned=false;packet.SendQueueHandled();}else if(packet.inSendQueuesCount==0)NetPackageManager.FreePackage(packet);}
    }
    private static readonly Dictionary<Frame,NativeViewerClient> nativeViewerClients=new Dictionary<Frame,NativeViewerClient>();
    [ThreadStatic]private static NativeViewerClient capturingNativeViewer;
    [ThreadStatic]private static ViewerClientStage capturingNativeViewerStage;
    private static string ViewerLocks(PackedBoolArray locks)
    {if(locks==null)return "";if(locks.Length>255)throw new InvalidOperationException();return new string(Enumerable.Range(0,locks.Length).Select(i=>locks[i]?'1':'0').ToArray());}
    private static string ViewerDigest(XElement image)=>RebirthWorkstationCompletionOriginal.Hash(image.ToString(SaveOptions.DisableFormatting));
    private static bool EnsureNativeViewerBefore(Frame frame)
    {
        if(frame.RemoteNativeNonce==Guid.Empty)return true;
        try
        {
            string generation=ViewerDigest(ActiveBefore(frame.Intent));
            if(nativeViewerClients.TryGetValue(frame,out var old)&&old.Generation!=generation)
            {nativeViewerClients.Remove(frame);old.Dispose();}
            if(!nativeViewerClients.TryGetValue(frame,out var source))
            {
                var o=frame.Observation;
                if((frame.CachedPhysical!=null?!frame.CachedPhysical.Current():frame.NativeBefore?.Current()!=true)||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(o.Window,out var session)||
                    !RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(o.Viewer,new[]{o.Recipe.GetName()},out int bytes)||bytes>8*1024*1024||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*bytes+64L*1024*1024),out var memory))return false;
                source=new NativeViewerClient{Frame=frame,Generation=generation,Session=session,Connection=CurrentNativeServerConnection(),Memory=memory,
                    BagLocks=ViewerLocks(o.Viewer.bag.LockedSlots),BeltLocks=ViewerLocks(o.Viewer.inventory.ItemGrid.SlotLocks),BeltCount=o.Viewer.inventory.Length};
                source.BeltPreferred=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.EncodePreferences(o.Viewer.inventory.preferredItemTypes);
                source.EquipmentPreferred=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.EncodePreferences(o.Viewer.equipment.preferredItemSlots);
                try{source.Before=FreezeViewerStage(source,false);if(!source.Current()||!SaveViewerClientStage(source,source.Before,false)){source.Dispose();return false;}}
                catch{source.Dispose();return false;}nativeViewerClients.Add(frame,source);
            }
            if(!source.Current()||!LiveViewerFrameMatches(source,source.Before))return false;
            PumpViewerClientStage(source,source.Before,false);return source.Before.Saved;
        }
        catch{return false;}
    }
    private static ViewerClientStage FreezeViewerStage(NativeViewerClient source,bool after,bool cached=false)
    {
        var o=source.Frame.Observation;var frame=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(o.Viewer,new[]{o.Recipe.GetName()});
        var stage=new ViewerClientStage{Frame=frame};capturingNativeViewer=source;capturingNativeViewerStage=stage;
        try
        {
            stage.Packet=NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(o.Viewer);
            if(!stage.Captured||!LiveViewerFrameMatches(source,stage))throw new InvalidOperationException("Native viewer capture changed");
            var raw=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.WireImage(stage.Packet.playerDataFile);
            if(raw.Length<1||raw.Length>16*1024*1024)throw new InvalidOperationException("Native viewer capture bound");
            stage.NativeDigest=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.NativeDigest(raw);stage.NativeLength=raw.Length;
            stage.Packet.RegisterSendQueue();stage.Pinned=true;
        }
        catch{stage.ReleasePacket();throw;}
        finally{capturingNativeViewer=null;capturingNativeViewerStage=null;}
        try
        {
        var request=new XElement(source.Frame.CachedPhysical==null?"nativeViewer":"cachedViewer",new XAttribute("version",1),new XAttribute("stage",cached?"effect":after?"after":"before"),
            new XAttribute("nonce",source.Frame.RemoteNativeNonce.ToString("N")),new XAttribute("source",source.Frame.RemoteNativeSourceHash),
            new XAttribute("generation",source.Generation),new XAttribute("bagLocks",source.BagLocks),new XAttribute("beltLocks",source.BeltLocks),
            new XAttribute("beltCount",source.BeltCount),new XAttribute("pdsDigest",stage.NativeDigest),new XAttribute("pdsLength",stage.NativeLength),
            new XAttribute("beltPreferred",source.BeltPreferred),new XAttribute("equipmentPreferred",source.EquipmentPreferred),
            new XElement("activeBefore",new XElement(ActiveBefore(source.Frame.Intent))),new XElement(frame));
        if(source.Frame.CachedPhysical!=null)request.Add(new XAttribute("action",source.Frame.CachedPhysical.Action.ToString("N")));
        if(after)
        {
            if(cached)request.Add(new XElement("fullBoundary",new XElement(source.Frame.Observation.FullBoundaryEvidence)));
            else request.Add(new XElement("physicalDelivery",source.Frame.ConfirmedOriginal.Write().Element("delivery").Elements().Single()));
            if(!cached&&source.Prefix?.Prefix.CopyEvidence() is XElement outputOriginal)
                request.Add(new XElement("nativeOutputOriginal",outputOriginal));
            if(source.Frame.NativeViewerEffect!=null)
            {
                var effect=source.Frame.NativeViewerEffect.CopyEvidence();if(effect==null)throw new InvalidOperationException("Unknown native viewer effect");
                request.Add(new XElement("sameOwnerEffect",effect));
            }
        }
        stage.Request=request;stage.Digest=ViewerDigest(request);return stage;
        }
        catch{stage.ReleasePacket();throw;}
    }
    private static bool LiveViewerFrameMatches(NativeViewerClient source,ViewerClientStage stage)=>source.Current()&&
        XNode.DeepEquals(stage.Frame,RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(source.Frame.Observation.Viewer,new[]{source.Frame.Observation.Recipe.GetName()}));
    private static bool SaveViewerClientStage(NativeViewerClient source,ViewerClientStage stage,bool after)
        =>SaveXml(source.Frame.Path+".viewer."+source.Generation+"."+(string)stage.Request.Attribute("stage"),new XElement("workstationCompletionIntent",
            new XAttribute("operation",source.Frame.Observation.Operation.ToString("N")),new XAttribute("world",source.Frame.Observation.WorldGuid),new XElement("before",new XElement(stage.Request))));
    private static void PumpViewerClientStage(NativeViewerClient source,ViewerClientStage stage,bool after)
    {
        if(stage.Saved||!source.Current()||!stage.Sent&&!LiveViewerFrameMatches(source,stage)||UnityEngine.Time.realtimeSinceStartup<stage.Next)return;stage.Next=UnityEngine.Time.realtimeSinceStartup+0.5;
        var o=source.Frame.Observation;
        if(!stage.Armed){Send(true,new XElement(stage.Request),o.Operation,o.Viewer.entityId);return;}
        if(!stage.Sent)
        {
            if(!RebirthLogisticsOriginalTransport.CanSendNative(source.Connection))return;
            if(!SaveXml(source.Frame.Path+".viewer."+source.Generation+"."+(string)stage.Request.Attribute("stage")+".attempt",
                new XElement("workstationCompletionIntent",new XAttribute("operation",o.Operation.ToString("N")),new XAttribute("world",o.WorldGuid),
                    new XElement("before",new XElement("nativeViewerUploadAttempt",new XAttribute("digest",stage.Digest))))))return;
            stage.Sent=true;
            if(stage.Packet==null||!stage.Pinned||!stage.Captured||!LiveViewerFrameMatches(source,stage))return;
            var raw=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.WireImage(stage.Packet.playerDataFile);
            if(raw.Length!=stage.NativeLength||RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.NativeDigest(raw)!=stage.NativeDigest)return;
            try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(stage.Packet);}
            finally{stage.ReleasePacket();}
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerInventory>().Setup(o.Viewer,true,true,false,false));
        }
        // Metadata query only. An uncertain native upload is never resent.
        Send(true,new XElement(stage.Request.Name,new XAttribute("version",1),new XAttribute("stage",(string)stage.Request.Attribute("stage")+"Query"),
            source.Frame.CachedPhysical==null?null:new XAttribute("action",source.Frame.CachedPhysical.Action.ToString("N")),
            new XAttribute("nonce",source.Frame.RemoteNativeNonce.ToString("N")),new XAttribute("source",source.Frame.RemoteNativeSourceHash),
            new XAttribute("generation",source.Generation),new XAttribute("digest",stage.Digest),new XAttribute("pdsDigest",stage.NativeDigest??""),
            new XAttribute("pdsLength",stage.NativeLength)),o.Operation,o.Viewer.entityId);
    }
    internal static void ObserveNativeViewerFromPlayer(PlayerDataFile data,EntityPlayer player,bool run)
    {
        var source=capturingNativeViewer;var stage=capturingNativeViewerStage;
        if(run&&source!=null&&stage!=null&&ReferenceEquals(source.Frame.Observation.Viewer,player)&&LiveViewerFrameMatches(source,stage)&&
            data.id==player.entityId&&data.deaths==player.Died&&RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,stage.Frame))stage.Captured=true;
    }
    private static void AcceptNativeViewerAck(EntityPlayer viewer,Guid operation,XElement ack)
    {
        foreach(var source in nativeViewerClients.Values)
        {
            if(source.Frame.Observation.Operation!=operation)continue;
            if(!ReferenceEquals(source.Frame.Observation.Viewer,viewer)||!source.Current()||
                (string)ack.Attribute("nativeNonce")!=source.Frame.RemoteNativeNonce.ToString("N")||(string)ack.Attribute("nativeSource")!=source.Frame.RemoteNativeSourceHash)return;
            string kind=(string)ack.Attribute("stage");var stage=kind.StartsWith("nativeViewerBefore",StringComparison.Ordinal)||kind.StartsWith("cachedViewerBefore",StringComparison.Ordinal)?source.Before:
                kind.StartsWith("nativeViewerEffect",StringComparison.Ordinal)?source.CachedEffect:source.After;
            if(AcceptViewerOutputPrefixAck(source,ack))return;
            if(stage==null||(string)ack.Attribute("digest")!=stage.Digest)return;
            if(kind.EndsWith("Saved",StringComparison.Ordinal))stage.Armed=true;
            if(kind.EndsWith("Armed",StringComparison.Ordinal))stage.Armed=true;
            if(kind.EndsWith("Saved",StringComparison.Ordinal)&&stage.Sent&&stage.Captured&&SaveXml(source.Frame.Path+".viewer."+source.Generation+"."+kind,
                new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",source.Frame.Observation.WorldGuid),new XElement("before",new XElement(ack)))))stage.Saved=true;
            if(stage.Saved&&ReferenceEquals(stage,source.After))
            {
                RememberRemoteOutputPrefixAudit(source);
                try{var saved=AdditionalOutputOwnerSaved.Capture(source,stage,ack);if(saved!=null)RebirthRecipeOutputIntegration.ObserveUiOutputRemoteOwnerSaved(saved);}catch{}
            }
        }
    }
    private static void PumpNativeViewers(World world)
    {
        foreach(var source in nativeViewerClients.Values.ToArray())
        {
            if(!ReferenceEquals(source.Frame.Observation.World,world)||source.Frame.Closed||!frames.TryGetValue(source.Frame.Observation.Operation,out var frame)||!ReferenceEquals(frame,source.Frame))
            {nativeViewerClients.Remove(source.Frame);source.Dispose();continue;}
            if(!source.Current())continue;
            if(!PumpViewerOutputPrefix(source))continue;
            if(source.Frame.ConfirmedOriginal==null)
            {
                if(source.Frame.NativeViewerEffect==null||!source.Frame.Observation.FullBoundary||source.Frame.Observation.FullBoundaryEvidence==null)continue;
                ObserveNativeViewerFullBoundary(source.Frame);if(source.CachedEffect==null)continue;
                PumpViewerClientStage(source,source.CachedEffect,true);
                if(source.CachedEffect.Saved)ResumePhysicalNativePackets(source.Frame,RemoteNativePhase.CachedSpaceTransfer);
                continue;
            }
            var delivery=source.Frame.ConfirmedOriginal.Write().Element("delivery").Elements().Single();
            if((string)delivery.Attribute("sink")!="Inventory"&&source.Frame.NativeViewerEffect==null&&source.Frame.CachedPhysical==null)continue;
            if(source.After==null)
            {
                if(!RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(source.Frame.Observation.Viewer,new[]{source.Frame.Observation.Recipe.GetName()},out int bytes)||
                    bytes>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,
                        checked(12L*(bytes+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Before.Request)+
                            (source.CachedEffect==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.CachedEffect.Request)))+64L*1024*1024)))continue;
                var after=FreezeViewerStage(source,true);
                long measure=checked(RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Before.Request)+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(after.Request)+
                    (source.CachedEffect==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.CachedEffect.Request)));
                if(measure>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,checked(8*measure+64L*1024*1024))){after.ReleasePacket();continue;}
                source.After=after;if(!SaveViewerClientStage(source,source.After,true)){source.After.ReleasePacket();source.After=null;continue;}
            }
            PumpViewerClientStage(source,source.After,true);
            if(source.After.Saved){ResumePhysicalNativePackets(source.Frame,RemoteNativePhase.PhysicalAccepted);ResumePhysicalNativePackets(source.Frame,RemoteNativePhase.QueueAccepted);}
        }
    }
    private static void ObserveNativeViewerFullBoundary(Frame frame)
    {
        try
        {
            if(frame.NativeViewerEffect==null||frame.ConfirmedOriginal!=null||!frame.Observation.FullBoundary||
                !nativeViewerClients.TryGetValue(frame,out var source)||source.CachedEffect!=null||!source.Current())return;
            var full=frame.Observation.FullBoundaryEvidence;if(full==null)return;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(frame.Observation.Station,Convert.FromBase64String((string)full.Element("nativeAfter")),(int)full.Attribute("nativeElapsedOffset"))||
                    Grid(frame.Observation.Viewer.bag.ItemGrid.items)!=(string)full.Element("bagBefore")||Grid(frame.Observation.Viewer.inventory.ItemGrid.items)!=(string)full.Element("beltBefore"))return;
            }
            if(!RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(frame.Observation.Viewer,new[]{frame.Observation.Recipe.GetName()},out int bytes)||bytes>8*1024*1024||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,checked(12L*(bytes+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Before.Request))+64L*1024*1024)))return;
            var effect=FreezeViewerStage(source,true,true);
            long measured=checked(RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Before.Request)+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(effect.Request));
            if(measured>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,checked(8*measured+64L*1024*1024))){effect.ReleasePacket();return;}
            source.CachedEffect=effect;if(!SaveViewerClientStage(source,effect,true)){effect.ReleasePacket();source.CachedEffect=null;}
        }
        catch{/* No replacement capture after an unknown original boundary. */}
    }
    private static bool NativeViewerAllowsPhysical(Frame frame)=>frame?.ConfirmedOriginal==null||
        (frame.NativeViewerEffect==null&&frame.CachedPhysical==null&&(string)frame.ConfirmedOriginal.Write().Element("delivery").Elements().Single().Attribute("sink")!="Inventory")||
        nativeViewerClients.TryGetValue(frame,out var viewer)&&viewer.Current()&&viewer.Before.Saved&&viewer.After?.Saved==true;
    internal static bool NativeViewerCachedEffectSaved(Frame frame)=>frame?.NativeViewerEffect!=null&&
        nativeViewerClients.TryGetValue(frame,out var viewer)&&viewer.Current()&&viewer.CachedEffect?.Saved==true;
    private static bool IsSavedNativeViewerResume(Frame frame,NetPackageTileEntity packet)=>frame!=null&&
        ReferenceEquals(resumingNativeUpload?.Setup?.Packet,packet)&&ReferenceEquals(resumingNativeUpload.Setup.Frame,frame)&&NativeViewerAllowsPhysical(frame)&&
        nativeViewerClients.TryGetValue(frame,out var source)&&source.Current()&&source.Before.Saved&&source.After?.Saved==true;
    private static bool NativeViewerCanRetireFrame(Frame frame)=>(frame.NativeViewerEffect!=null?frame.SameOwnerTerminalReleased:NativeViewerAllowsPhysical(frame))&&
        !pendingNativeUploads.Any(value=>ReferenceEquals(value.Setup?.Frame,frame)&&value.NativeHold&&
            (value.Setup.Phase==RemoteNativePhase.PhysicalAccepted||value.Setup.Phase==RemoteNativePhase.QueueAccepted));
    internal static void ObserveNativeInventoryInsertion(RebirthRecipeOutputIntegration.OwningObservation observed)
    {
        var frame=current;if(frame==null||!ReferenceEquals(frame.Observation,observed)||frame.RemoteNativeNonce==Guid.Empty||observed.AcceptedSink!=RebirthRecipeOutputIntegration.OwningSink.Inventory||
            !nativeViewerClients.TryGetValue(frame,out var viewer)||!viewer.Before.Saved||!ReferenceEquals(viewer.Frame.Observation,observed))return;
        // One actual post-insertion/pre-decrement native projection. This is
        // selected immediately and held as that same pooled native packet.
        SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(observed.Station,StreamModeWrite.ToServer,observed.Station.lockHandleWaitingFor));
    }
    private static void ReceiveNativeViewer(EntityPlayer viewer,ClientInfo peer,string key,string creation,Guid operation,XElement request)
    {
        if(!nativeHostOriginals.TryGetValue(operation,out var source)||!ReferenceEquals(source.Viewer,viewer)||!ReferenceEquals(source.Sender,peer)||
            source.ViewerKey!=key||source.ViewerCreation!=creation||!source.CurrentViewer())return;
        if(RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Receive(source,request,out var stage,out var digest))
            Ack(viewer,operation,stage,source.EarlierOwner.OriginalOwnerKey,source.EarlierOwner.Creation,digest,(string)request.Attribute("challenge"),null,source);
    }
}
