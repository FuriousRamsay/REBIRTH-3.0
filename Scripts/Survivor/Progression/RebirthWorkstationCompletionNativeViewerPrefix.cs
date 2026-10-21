using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    private sealed class ViewerOutputPrefix
    {internal RebirthRecipeOutputIntegration.AdditionalOutputPrefix Prefix;internal ViewerClientStage Stage;internal bool HeaderSent,SupersededByFinal;}
    private sealed partial class NativeViewerClient
    {internal ViewerOutputPrefix Prefix;internal RebirthRecipeOutputIntegration.AdditionalOutputPrefix CapturingPrefix,PendingPrefix;}
    internal sealed class AdditionalOutputPrefixSaved
    {
        private NativeViewerClient source;private ViewerOutputPrefix prefix;private XElement acknowledgment;
        private AdditionalOutputPrefixSaved(){}
        internal RebirthRecipeOutputIntegration.AdditionalOutputPrefix Prefix=>prefix.Prefix;
        internal RebirthRecipeOutputIntegration.OwningObservation Observation=>source.Frame.Observation;
        internal XElement Request=>new XElement(prefix.Stage.Request);
        internal XElement Acknowledgment=>new XElement(acknowledgment);
        internal bool Current()=>source?.Current()==true&&ReferenceEquals(source.Prefix,prefix)&&prefix.Prefix.Matches(source.Frame.Observation)&&
            prefix.Stage.Saved&&prefix.Stage.Sent&&prefix.Stage.Captured&&
            (string)acknowledgment.Attribute("stage")=="nativeViewerPrefix"+prefix.Prefix.Count+"Saved"&&
            (string)acknowledgment.Attribute("digest")==prefix.Stage.Digest&&
            (string)acknowledgment.Attribute("nativeNonce")==source.Frame.RemoteNativeNonce.ToString("N")&&
            (string)acknowledgment.Attribute("nativeSource")==source.Frame.RemoteNativeSourceHash;
        internal static AdditionalOutputPrefixSaved Capture(object owner,object saved,XElement ack)
        {
            if(owner is not NativeViewerClient source||saved is not ViewerOutputPrefix prefix||ack==null)return null;
            var result=new AdditionalOutputPrefixSaved{source=source,prefix=prefix,acknowledgment=new XElement(ack)};return result.Current()?result:null;
        }
    }
    private static bool HasCurrentViewerPrefix(NativeViewerClient source)
        =>(source.CapturingPrefix??source.PendingPrefix??source.Prefix?.Prefix)?.Matches(source.Frame.Observation)==true||HasCurrentRemoteOutputFinal(source);
    private static bool HasCurrentRemoteOutputFinal(NativeViewerClient source)
        =>source.Frame.Observation.RemoteOutputRestart?.FinalCallbackSaved==true&&source.Frame.Observation.RemoteOutputRestart.BindingCurrent()&&source.After?.Saved==true;
    private static long ViewerPrefixVersion(NativeViewerClient source)
        =>HasCurrentViewerPrefix(source)&&source.Frame.Observation.NativeVersionAfter>0?source.Frame.Observation.NativeVersionAfter:source.Frame.Observation.NativeVersionBefore;
    private static void DisposeViewerPrefix(NativeViewerClient source)
    {source.Prefix?.Stage.ReleasePacket();source.Prefix=null;source.CapturingPrefix=source.PendingPrefix=null;}
    internal static void ObserveAdditionalOutputPrefix(XUiC_RecipeStack card,RebirthRecipeOutputIntegration.AdditionalOutputPrefix prefix)
    {
        var frame=current;
        if(frame==null)
        {
            var candidates=nativeViewerClients.Values.Where(value=>!value.Frame.Closed&&ReferenceEquals(value.Frame.Observation.Card,card)&&prefix?.Matches(value.Frame.Observation)==true).ToArray();
            if(candidates.Length!=1)return;frame=candidates[0].Frame;
        }
        if(frame==null||frame.RemoteNativeNonce==Guid.Empty||frame.CachedPhysical==null&&frame.NativeViewerEffect==null&&frame.NativeOriginal==null&&!IsLoadedDeferredFrame(frame)||
            !ReferenceEquals(frame.Observation.Card,card)||prefix?.Matches(frame.Observation)!=true||
            !nativeViewerClients.TryGetValue(frame,out var source)||source.Before?.Saved!=true)return;
        ViewerClientStage stage=null;IDisposable contributionRetention=null;
        try
        {
            source.PendingPrefix=prefix;
            if(source.Prefix!=null)
            {
                if(source.Prefix.Prefix.Count>=prefix.Count){source.PendingPrefix=null;return;}
                if(source.Prefix.HeaderSent&&!source.Prefix.Stage.Saved)return;
            }
            if(!RebirthProductiveEarnedUiOwnerFile.TryMeasureDeferredLiveFrame(frame.Observation.Viewer,new[]{frame.Observation.Recipe.GetName()},out int bytes)||bytes>8*1024*1024||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,checked(16L*(bytes+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Before.Request)+
                    (source.Prefix==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Prefix.Stage.Request)))+96L*1024*1024)))return;
            source.CapturingPrefix=prefix;
            var evidence=prefix.CopyEvidence();var effect=frame.NativeViewerEffect?.CopyEvidence();
            XElement contribution=null;
            if(IsLoadedDeferredFrame(frame)&&!TryCopyLoadedDeferredContribution(frame,out contribution,out contributionRetention))return;
            if(frame.NativeOriginal!=null&&effect==null&&frame.CachedPhysical==null&&
                (!TryFindRemoteNativeReceipt(frame.NativeOriginal.Operation,frame.Observation.World,frame.Observation.Station,out var receipt)||
                 !RebirthProductiveEarnedUiQueueOwner.TryCopyNativeContribution(receipt,out contribution,out contributionRetention)))return;
            if(evidence==null||frame.CachedPhysical==null&&effect==null&&contribution==null||!source.Current())return;
            var owner=RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(frame.Observation.Viewer,new[]{frame.Observation.Recipe.GetName()});
            stage=new ViewerClientStage{Frame=owner};capturingNativeViewer=source;capturingNativeViewerStage=stage;
            try
            {
                stage.Packet=NetPackageManager.GetPackage<NetPackagePlayerData>().Setup(frame.Observation.Viewer);
                if(!stage.Captured||!LiveViewerFrameMatches(source,stage))return;
                var raw=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.WireImage(stage.Packet.playerDataFile);
                if(raw.Length<1||raw.Length>16*1024*1024)return;
                stage.NativeDigest=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.NativeDigest(raw);stage.NativeLength=raw.Length;
                stage.Packet.RegisterSendQueue();stage.Pinned=true;
            }
            finally{capturingNativeViewer=null;capturingNativeViewerStage=null;}
            stage.Request=new XElement(frame.CachedPhysical==null?"nativeViewer":"cachedViewer",new XAttribute("version",1),new XAttribute("stage","prefix-"+prefix.Count),
                new XAttribute(frame.CachedPhysical==null?"nonce":"action",frame.RemoteNativeNonce.ToString("N")),new XAttribute("source",frame.RemoteNativeSourceHash),new XAttribute("generation",source.Generation),
                new XAttribute("bagLocks",source.BagLocks),new XAttribute("beltLocks",source.BeltLocks),new XAttribute("beltCount",source.BeltCount),
                new XAttribute("beltPreferred",source.BeltPreferred),new XAttribute("equipmentPreferred",source.EquipmentPreferred),
                new XAttribute("pdsDigest",stage.NativeDigest),new XAttribute("pdsLength",stage.NativeLength),
                new XElement("activeBefore",new XElement(ActiveBefore(frame.Intent))),new XElement(owner),new XElement(evidence),
                effect==null?null:new XElement("sameOwnerEffect",new XElement(effect)),contribution);
            long requestBytes=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(stage.Request);
            long retainedRequestBytes=checked(requestBytes+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Before.Request)+
                (source.Prefix==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Prefix.Stage.Request)));
            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,checked(16*retainedRequestBytes+96L*1024*1024)))return;
            stage.Digest=ViewerDigest(stage.Request);
            if(!SaveViewerClientStage(source,stage,true))return;
            if(source.Prefix!=null&&!source.Prefix.Stage.Saved)
            {
                var old=source.Prefix;
                if(old.HeaderSent||old.Stage.Sent||!SaveXml(frame.Path+".viewer."+source.Generation+".prefix-"+old.Prefix.Count+".unused-before-send",
                    new XElement("workstationCompletionIntent",new XAttribute("operation",frame.Observation.Operation.ToString("N")),new XAttribute("world",frame.Observation.WorldGuid),
                        new XElement("before",new XElement("nativeViewerPrefixUnusedBeforeSend",new XAttribute("digest",old.Stage.Digest),new XAttribute("successor",stage.Digest))))))return;
            }
            source.Prefix?.Stage.ReleasePacket();source.Prefix=new ViewerOutputPrefix{Prefix=prefix,Stage=stage};source.PendingPrefix=null;stage=null;
        }
        catch{/* An exact accepted insertion remains held without a fresh PDS. */}
        finally{stage?.ReleasePacket();contributionRetention?.Dispose();source.CapturingPrefix=null;}
    }
    private static bool PumpViewerOutputPrefix(NativeViewerClient source)
    {
        if(source.PendingPrefix!=null&&(source.Prefix==null||source.Prefix.Stage.Saved||!source.Prefix.HeaderSent))
            ObserveAdditionalOutputPrefix(source.Frame.Observation.Card,source.PendingPrefix);
        var prefix=source.Prefix;if(prefix==null)return source.PendingPrefix==null;var stage=prefix.Stage;if(stage.Saved)return source.PendingPrefix==null;
        if(prefix.SupersededByFinal)return source.PendingPrefix==null;
        if(TrySupersedeUnsentFullPrefix(source,prefix))return source.PendingPrefix==null;
        if(!source.Current()||!prefix.Prefix.Current()||UnityEngine.Time.realtimeSinceStartup<stage.Next)return false;
        stage.Next=UnityEngine.Time.realtimeSinceStartup+0.5;var observation=source.Frame.Observation;
        if(!stage.Armed){prefix.HeaderSent=true;Send(true,new XElement(stage.Request),observation.Operation,observation.Viewer.entityId);return false;}
        if(!stage.Sent)
        {
            if(!prefix.Prefix.OwnerCurrent())return false;
            if(!RebirthLogisticsOriginalTransport.CanSendNative(source.Connection))return false;
            if(!SaveXml(source.Frame.Path+".viewer."+source.Generation+".prefix-"+prefix.Prefix.Count+".attempt",
                new XElement("workstationCompletionIntent",new XAttribute("operation",observation.Operation.ToString("N")),new XAttribute("world",observation.WorldGuid),
                    new XElement("before",new XElement("nativeViewerUploadAttempt",new XAttribute("digest",stage.Digest))))))return false;
            stage.Sent=true;
            if(stage.Packet==null||!stage.Pinned||!stage.Captured)return false;
            var raw=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.WireImage(stage.Packet.playerDataFile);
            if(raw.Length!=stage.NativeLength||RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.NativeDigest(raw)!=stage.NativeDigest)return false;
            try{SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(stage.Packet);}finally{stage.ReleasePacket();}
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerInventory>().Setup(observation.Viewer,true,true,false,false));
        }
        Send(true,new XElement(source.Frame.CachedPhysical==null?"nativeViewer":"cachedViewer",new XAttribute("version",1),new XAttribute("stage","prefix-"+prefix.Prefix.Count+"Query"),
            new XAttribute(source.Frame.CachedPhysical==null?"nonce":"action",source.Frame.RemoteNativeNonce.ToString("N")),new XAttribute("source",source.Frame.RemoteNativeSourceHash),new XAttribute("generation",source.Generation),
            new XAttribute("digest",stage.Digest),new XAttribute("pdsDigest",stage.NativeDigest),new XAttribute("pdsLength",stage.NativeLength)),observation.Operation,observation.Viewer.entityId);
        return false;
    }
    private static bool TrySupersedeUnsentFullPrefix(NativeViewerClient source,ViewerOutputPrefix prefix)
    {
        if(source.Frame.ConfirmedOriginal==null||prefix.HeaderSent||prefix.Stage.Sent||!source.Current()||!prefix.Prefix.Current())return false;
        var delivery=source.Frame.ConfirmedOriginal.Write().Element("delivery")?.Elements().SingleOrDefault();
        var evidence=prefix.Prefix.CopyEvidence();
        if(delivery==null||evidence==null||(string)delivery.Attribute("sink")!="Inventory"||
            prefix.Prefix.Count!=1+RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"extrasNative").Length)return false;
        var receipt=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Frame.Observation.Operation.ToString("N")),
            new XAttribute("world",source.Frame.Observation.WorldGuid),new XElement("before",new XElement("nativeViewerPrefixSupersededByFinal",
                new XAttribute("digest",prefix.Stage.Digest),new XElement(evidence),new XElement(delivery))));
        if(!SaveXml(source.Frame.Path+".viewer."+source.Generation+".prefix-"+prefix.Prefix.Count+".superseded-final",receipt))return false;
        prefix.Stage.ReleasePacket();prefix.SupersededByFinal=true;return true;
    }
    private static bool AcceptViewerOutputPrefixAck(NativeViewerClient source,XElement ack)
    {
        string kind=(string)ack.Attribute("stage");if(kind?.StartsWith("nativeViewerPrefix",StringComparison.Ordinal)!=true)return false;
        if(!source.Current()||(string)ack.Attribute("nativeNonce")!=source.Frame.RemoteNativeNonce.ToString("N")||
            (string)ack.Attribute("nativeSource")!=source.Frame.RemoteNativeSourceHash)return true;
        var prefix=source.Prefix;var stage=prefix?.Stage;
        if(stage==null||kind!="nativeViewerPrefix"+prefix.Prefix.Count+"Armed"&&kind!="nativeViewerPrefix"+prefix.Prefix.Count+"Saved"||
            (string)ack.Attribute("digest")!=stage.Digest)return true;
        stage.Armed=true;
        if(kind.EndsWith("Saved",StringComparison.Ordinal)&&stage.Sent&&stage.Captured&&SaveXml(source.Frame.Path+".viewer."+source.Generation+"."+kind,
            new XElement("workstationCompletionIntent",new XAttribute("operation",source.Frame.Observation.Operation.ToString("N")),new XAttribute("world",source.Frame.Observation.WorldGuid),new XElement("before",new XElement(ack)))))
        {stage.Saved=true;var saved=AdditionalOutputPrefixSaved.Capture(source,prefix,ack);if(saved!=null)prefix.Prefix.ObserveSaved(saved);RememberRemoteOutputPrefixAudit(source);}
        return true;
    }
    internal static bool CanResumeAdditionalOutputPrefix(XUiC_RecipeStack card,int count)
    {
        var match=nativeViewerClients.Values.Where(value=>ReferenceEquals(value.Frame.Observation.Card,card)&&!value.Frame.Closed).ToArray();
        if(match.Length==0)return true;if(match.Length!=1)return false;
        if(match[0].Frame.CachedPhysical==null&&match[0].Frame.NativeViewerEffect==null&&!IsLoadedDeferredFrame(match[0].Frame))return true;
        return match[0].Current()&&(HasCurrentRemoteOutputFinal(match[0])?match[0].Frame.Observation.RemoteOutputRestart.Prefix==count:
            match[0].Prefix?.Prefix.Count==count&&match[0].Prefix.Stage.Saved);
    }
    internal static Newtonsoft.Json.Linq.JArray CaptureRemoteOutputPrefixAudit(EntityPlayer actor)
    {
        var rows=new Newtonsoft.Json.Linq.JArray();
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||actor==null||!ReferenceEquals(actor.world,GameManager.Instance?.World))return rows;
        foreach(var source in nativeViewerClients.Values.Where(value=>ReferenceEquals(value.Frame.Observation.Viewer,actor)).Take(5))
        {
            var prefix=source.Prefix;var stage=prefix?.Stage;
            rows.Add(new Newtonsoft.Json.Linq.JObject{["nativeOperation"]=source.Frame.Observation.Operation.ToString("N"),
                ["operation"]=prefix?.Prefix.Operation.ToString("N"),["original"]=prefix?.Prefix.OriginalHash,
                ["nonce"]=source.Frame.RemoteNativeNonce.ToString("N"),["source"]=source.Frame.RemoteNativeSourceHash,["generation"]=source.Generation,
                ["cached"]=source.Frame.CachedPhysical!=null,["deferredNativeAdd"]=source.Frame.NativeOriginal!=null,["current"]=source.Current(),["acceptedPrefix"]=prefix?.Prefix.Count??0,
                ["coldReattached"]=source.Frame.Observation.RemoteOutputRestart!=null,
                ["beforeSaved"]=source.Before?.Saved==true,["nativeSent"]=stage?.Sent==true,["nativeCaptured"]=stage?.Captured==true,
                ["prefixSaved"]=stage?.Saved==true,["nativeDigest"]=stage?.NativeDigest,["nativeLength"]=stage?.NativeLength??0,
                ["afterSaved"]=source.After?.Saved==true,["afterNativeSent"]=source.After?.Sent==true,["afterNativeCaptured"]=source.After?.Captured==true,
                ["afterNativeDigest"]=source.After?.NativeDigest,["afterNativeLength"]=source.After?.NativeLength??0,
                ["ownerImage"]=RebirthRecipeOutputIntegration.OutputOwnerAuditHash(source.Frame.Observation.Viewer),
                ["ownerMatchesSaved"]=source.After?.Saved==true?LiveViewerFrameMatches(source,source.After):stage?.Saved==true&&LiveViewerFrameMatches(source,stage),
                ["pendingCapture"]=source.PendingPrefix!=null});
        }
        if(remoteOutputPrefixAuditHistory.TryGetValue(actor,out var history))foreach(var entry in history.Values.Values)
            if(!rows.OfType<Newtonsoft.Json.Linq.JObject>().Any(value=>(string)value["nativeOperation"]==(string)entry["nativeOperation"]))rows.Add(entry.DeepClone());
#endif
        return rows;
    }
    private sealed class RemoteOutputPrefixAuditHistory{internal readonly System.Collections.Generic.Dictionary<Guid,Newtonsoft.Json.Linq.JObject> Values=new System.Collections.Generic.Dictionary<Guid,Newtonsoft.Json.Linq.JObject>();internal readonly System.Collections.Generic.Queue<Guid> Order=new System.Collections.Generic.Queue<Guid>();}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,RemoteOutputPrefixAuditHistory> remoteOutputPrefixAuditHistory=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,RemoteOutputPrefixAuditHistory>();
    private static void RememberRemoteOutputPrefixAudit(NativeViewerClient source)
    {
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||source?.Prefix==null||!source.Current())return;
        var prefix=source.Prefix;var stage=prefix.Stage;var actor=source.Frame.Observation.Viewer;
        var row=new Newtonsoft.Json.Linq.JObject{["nativeOperation"]=source.Frame.Observation.Operation.ToString("N"),["operation"]=prefix.Prefix.Operation.ToString("N"),["original"]=prefix.Prefix.OriginalHash,
            ["nonce"]=source.Frame.RemoteNativeNonce.ToString("N"),["source"]=source.Frame.RemoteNativeSourceHash,["generation"]=source.Generation,["cached"]=source.Frame.CachedPhysical!=null,["deferredNativeAdd"]=source.Frame.NativeOriginal!=null,
            ["current"]=true,["acceptedPrefix"]=prefix.Prefix.Count,["coldReattached"]=source.Frame.Observation.RemoteOutputRestart!=null,["historicalObservation"]=true,
            ["beforeSaved"]=source.Before?.Saved==true,["nativeSent"]=stage.Sent,["nativeCaptured"]=stage.Captured,["prefixSaved"]=stage.Saved,["nativeDigest"]=stage.NativeDigest,["nativeLength"]=stage.NativeLength,
            ["afterSaved"]=source.After?.Saved==true,["afterNativeSent"]=source.After?.Sent==true,["afterNativeCaptured"]=source.After?.Captured==true,
            ["afterNativeDigest"]=source.After?.NativeDigest,["afterNativeLength"]=source.After?.NativeLength??0,
            ["ownerImage"]=RebirthRecipeOutputIntegration.OutputOwnerAuditHash(actor),["ownerMatchesSaved"]=LiveViewerFrameMatches(source,source.After?.Saved==true?source.After:stage)};
        var history=remoteOutputPrefixAuditHistory.GetValue(actor,_=>new RemoteOutputPrefixAuditHistory());var operation=source.Frame.Observation.Operation;
        if(!history.Values.ContainsKey(operation))history.Order.Enqueue(operation);history.Values[operation]=row;
        while(history.Order.Count>65)history.Values.Remove(history.Order.Dequeue());
#endif
    }
}
