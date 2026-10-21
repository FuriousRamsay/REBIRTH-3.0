using System;
using System.Linq;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private static Newtonsoft.Json.Linq.JArray CaptureRemoteOutputColdFinalAudit(EntityPlayerLocal actor)
    {
        var rows=new Newtonsoft.Json.Linq.JArray();
#if REBIRTH_DEBUG
        if(!ThreadManager.IsMainThread()||GamePrefs.GetString(EnumGamePrefs.GameName)!="CodexTest"||loadedRemoteOutputOwner?.EpisodeCurrent()!=true||
            !ReferenceEquals(loadedRemoteOutputOwner.Actor,actor))return rows;
        foreach(var source in loadedRemoteOutputOwner.Scan?.Candidates.Where(value=>value.FinalCallbackSaved).Take(5)??Enumerable.Empty<RemoteOutputColdClient>())
            rows.Add(new Newtonsoft.Json.Linq.JObject{["operation"]=source.LocalOperation.ToString("N"),["original"]=source.OriginalHash,
                ["nativeOperation"]=source.Operation.ToString("N"),["nonce"]=(string)source.Source.Attribute("nonce"),["source"]=(string)source.Source.Attribute("source"),
                ["acceptedPrefix"]=source.Prefix,["callbackReturned"]=source.CallbackReturned,["finalCallbackSaved"]=source.FinalCallbackSaved,
                ["queueOnly"]=source.QueueOnlyAuthorized,["queueTerminalSaved"]=source.QueueTerminalSaved,["attached"]=source.Attached,
                ["coldReattached"]=source.Attached,
                ["current"]=source.EpisodeCurrent&&source.FilesCurrent()});
#endif
        return rows;
    }
    // Loaded final owner images carry returned quest/cosmetic changes. They do
    // not qualify an earlier insertion prefix, nor authorize another callback.
    internal sealed partial class RemoteOutputColdClient
    {
        private bool FinalFilesCurrent()
        {
            try
            {
                if(!ReadPersonalOutputSidecar(originalPath+".native-continuation-returned",out var returnedBytes,out var returned)||
                    !PersonalColdIdentity(returned,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",LocalOperation,OriginalHash)||
                    (int?)returned.Attribute("outputPrefix")!=Prefix||
                    !ReadPersonalOutputSidecar(originalPath+".native-continuation-entered",out _,out var entered)||
                    !PersonalColdIdentity(entered,"recipeOutputNativeContinuationEntered","operation,original,outputPrefix","",LocalOperation,OriginalHash)||
                    (int?)entered.Attribute("outputPrefix")!=Prefix||
                    !ReadPersonalOutputSidecar(ownerSavedPath,out _,out var saved)||(string)saved.Attribute("returned")!=UiOriginalHash(returnedBytes)||
                    !RebirthRemoteResourcePlacedStationQueue.TryDecode(original.Element("recipe").Value,out var rows)||rows.Length!=1||rows[0]?.Recipe==null||
                    !TryReadScrapCompletedEquipment(originalPath,LocalOperation,OriginalHash,rows[0].Recipe,Prefix,PersonalColdImage(accepted,"equipment"),out var equipment))return false;
                return Convert.ToBase64String(equipment)==(string)request.Element("nativeOwnerFrame")?.Element("equipment");
            }
            catch{return false;}
        }
        internal bool AcceptQueueTerminal(XElement ack)
        {
            if(!FinalCallbackSaved||Attached||QueueTerminalSaved||!BeforeBindingCurrent()||(bool?)ack?.Attribute("queueTerminal")!=true||
                (bool?)ack.Attribute("finalCallback")!=true||(string)ack.Attribute("nativeNonce")!=(string)remote.Attribute("nonce")||
                (string)ack.Attribute("nativeSource")!=(string)remote.Attribute("source")||(string)ack.Attribute("localOperation")!=LocalOperation.ToString("N")||
                (string)ack.Attribute("original")!=OriginalHash||(int?)ack.Attribute("count")!=Prefix||
                (string)ack.Attribute("prefixDigest")!=RebirthWorkstationCompletionPipeline.RemoteOutputRequestHash(request))return false;
            var canonicalAck=new XElement(ack);canonicalAck.Attribute("challenge")?.Remove();
            var terminal=new XElement("recipeOutputRemoteColdQueueSaved",new XAttribute("version",1),new XAttribute("operation",LocalOperation.ToString("N")),
                new XAttribute("original",OriginalHash),new XAttribute("owner",loaded.Owner),new XAttribute("creation",loaded.Creation),new XAttribute("deaths",loaded.Deaths),
                new XAttribute("nativeOperation",Operation.ToString("N")),new XAttribute("nonce",(string)remote.Attribute("nonce")),new XAttribute("source",(string)remote.Attribute("source")),
                new XAttribute("outputPrefix",Prefix),new XAttribute("callbackOwner",UiOriginalHash(ownerSavedBytes)),new XElement("acknowledgment",canonicalAck));
            if(!BeforeBindingCurrent()||!PublishPersonalColdBinding(originalPath+".remote-cold-queue-saved",UiOriginalBytes(terminal)))return false;
            QueueTerminalSaved=true;loaded.Scan?.Holds.RemoveAll(value=>value.Operation==LocalOperation);return true;
        }
        private static RemoteOutputColdClient ReadFinal(RemoteOutputOwnerLoad loaded,string path,byte[] bytes,XElement image,XElement remote,
            Guid local,Guid operation,string hash,int count,int maximum,byte[] acceptedBytes,XElement accepted,byte[] effectBytes,XElement effect)
        {
            IDisposable memory=null;
            try
            {
                if(count!=maximum||!ReadPersonalOutputSidecar(path+".native-continuation-returned",out var returnedBytes,out var returned)||
                    !PersonalColdIdentity(returned,"recipeOutputNativeContinuationReturned","operation,original,outputPrefix","",local,hash)||
                    (int?)returned.Attribute("outputPrefix")!=maximum||
                    !ReadPersonalOutputSidecar(path+".native-continuation-entered",out _,out var entered)||
                    !PersonalColdIdentity(entered,"recipeOutputNativeContinuationEntered","operation,original,outputPrefix","",local,hash)||
                    (int?)entered.Attribute("outputPrefix")!=maximum||
                    !RebirthRemoteResourcePlacedStationQueue.TryDecode(image.Element("recipe").Value,out var rows)||rows.Length!=1||rows[0]?.Recipe==null||
                    !TryReadScrapCompletedEquipment(path,local,hash,rows[0].Recipe,count,PersonalColdImage(accepted,"equipment"),out var equipment)||
                    !equipment.SequenceEqual(loaded.EquipmentFrame))return null;
                string ownerPath=path+".native-callback-owner-saved";
                if(!ReadPersonalOutputSidecar(ownerPath,out var ownerBytes,out var ownerSaved)||
                    !PersonalColdIdentity(ownerSaved,"recipeOutputRemoteCallbackOwnerSaved","version,operation,original,returned,accepted,outputPrefix,nativeOperation,nonce,source,generation,digest,nativeDigest,nativeLength","request,acknowledgment",local,hash)||
                    (int?)ownerSaved.Attribute("version")!=1||(int?)ownerSaved.Attribute("outputPrefix")!=count||
                    (string)ownerSaved.Attribute("returned")!=UiOriginalHash(returnedBytes)||(string)ownerSaved.Attribute("accepted")!=UiOriginalHash(acceptedBytes)||
                    (string)ownerSaved.Attribute("nativeOperation")!=operation.ToString("N")||(string)ownerSaved.Attribute("nonce")!=(string)remote.Attribute("nonce")||
                    (string)ownerSaved.Attribute("source")!=(string)remote.Attribute("source"))return null;
                var request=ownerSaved.Element("request")?.Elements().SingleOrDefault();var ack=ownerSaved.Element("acknowledgment")?.Elements().SingleOrDefault();
                // Final deferred receipts have no same-owner effect. Their
                // counter frame still belongs to the immutable native recipe,
                // whose original and completed equipment were checked above.
                string counterRecipe=rows[0].Recipe.GetName();
                var directEffect=request?.Element("sameOwnerEffect")?.Elements().SingleOrDefault();
                if(string.IsNullOrEmpty(counterRecipe)||directEffect!=null&&(string)directEffect.Attribute("recipe")!=counterRecipe)return null;
                string framePath=(string)remote.Attribute("intentPath"),generation=(string)ownerSaved.Attribute("generation"),requestPath=framePath+".viewer."+generation+".after";
                bool externalCached=(bool?)remote.Attribute("cached")==true;
                var savedRequest=RebirthWorkstationCompletionPublication.ReadPrimary(requestPath)?.Element("before")?.Element(externalCached?"cachedViewer":"nativeViewer");
                var localPrefix=request?.Element("nativeOutputOriginal")?.Element("nativeOutputPrefix");
                string deferredPath=null;byte[] deferredBytes=null;XElement deferred=null;
                if(externalCached&&((string)request?.Attribute("action")!=(string)remote.Attribute("nonce")||
                    request.Element("sameOwnerEffect")!=null||request.Element("deferredNativeContribution")!=null))return null;
                if(directEffect==null&&!externalCached)
                {
                    if(request?.Element("sameOwnerEffect")!=null)return null;
                    deferredPath=framePath+".viewer."+generation+".prefix-"+count;
                    var prefixRequest=RebirthWorkstationCompletionPublication.ReadPrimary(deferredPath)?.Element("before")?.Element("nativeViewer");
                    deferred=prefixRequest?.Element("deferredNativeContribution");
                    deferredBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(deferredPath);
                    if(deferred==null||deferredBytes==null||prefixRequest.Element("sameOwnerEffect")!=null||
                        (string)prefixRequest.Attribute("stage")!="prefix-"+count||(string)prefixRequest.Attribute("generation")!=generation||
                        (string)prefixRequest.Attribute("nonce")!=(string)remote.Attribute("nonce")||(string)prefixRequest.Attribute("source")!=(string)remote.Attribute("source")||
                        !XNode.DeepEquals(prefixRequest.Element("activeBefore"),remote.Element("activeBefore"))||
                        !XNode.DeepEquals(prefixRequest.Element("nativeOutputPrefix"),localPrefix))return null;
                }
                if(request==null||savedRequest==null||!XNode.DeepEquals(request,savedRequest)||(string)request.Attribute("stage")!="after"||
                    (string)localPrefix?.Attribute("operation")!=local.ToString("N")||(string)localPrefix?.Attribute("original")!=hash||
                    (int?)localPrefix?.Attribute("count")!=count||(string)localPrefix?.Attribute("accepted")!=UiOriginalHash(acceptedBytes)||
                    !MatchesRemoteColdRequestSource(remote,request,externalCached)||
                    (string)request.Attribute("generation")!=generation||RebirthWorkstationCompletionPipeline.RemoteOutputRequestHash(request)!=(string)ownerSaved.Attribute("digest")||
                    (string)request.Attribute("pdsDigest")!=(string)ownerSaved.Attribute("nativeDigest")||(int?)request.Attribute("pdsLength")!=(int?)ownerSaved.Attribute("nativeLength")||
                    (string)ack?.Attribute("stage")!=(externalCached?"cachedViewerAfterSaved":"nativeViewerAfterSaved")||(string)ack.Attribute("digest")!=(string)ownerSaved.Attribute("digest")||
                    (string)ack.Attribute("nativeNonce")!=(string)remote.Attribute("nonce")||(string)ack.Attribute("nativeSource")!=(string)remote.Attribute("source")||
                    !XNode.DeepEquals(request.Element("activeBefore"),remote.Element("activeBefore"))||
                    !XNode.DeepEquals(RebirthProductiveEarnedUiOwnerFile.CaptureDeferredLiveFrame(loaded.Actor,new[]{counterRecipe}),request.Element("nativeOwnerFrame")))return null;
                var requestBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(requestPath);
                if(requestBytes==null||!loaded.Current()||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*(bytes.Length+acceptedBytes.Length+effectBytes.Length+ownerBytes.Length+requestBytes.Length+(deferredBytes?.Length??0))+65536),out memory))return null;
                var result=new RemoteOutputColdClient{loaded=loaded,memory=memory,originalBytes=bytes,acceptedBytes=acceptedBytes,effectBytes=effectBytes,ownerSavedBytes=ownerBytes,requestBytes=requestBytes,
                    originalPath=path,acceptedPath=path+".output-"+(count-1)+".accepted",effectPath=path+".effect-observed",ownerSavedPath=ownerPath,requestPath=requestPath,
                    original=image,remote=remote,accepted=accepted,effect=effect,request=request,Operation=operation,LocalOperation=local,Prefix=count,OriginalHash=hash,FramePath=framePath,
                    deferredRequestPath=deferredPath,deferredRequestBytes=deferredBytes,deferredContribution=deferred==null?null:new XElement(deferred),
                    CallbackReturned=true,FinalCallbackSaved=true,ExternalCached=externalCached};
                memory=null;return result;
            }
            finally{memory?.Dispose();}
        }
    }
}
