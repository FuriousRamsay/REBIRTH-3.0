using System;using System.IO;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
internal static partial class RebirthWorkstationCompletionPipeline
{
    private static RemoteOutputHostSource DiscoverRemoteOutputFinalHostSource(World world,string path,Guid operation,Guid nonce,Guid birth,string hash,byte[] image,XElement original,XElement before)
    {
        using var parsing=RebirthProductiveEarnedObservationBudget.TryEnterCandidateDecoder();if(parsing==null)return null;
        RemoteOutputHostSource result=null;
        foreach(string intentPath in Directory.EnumerateFiles(System.IO.Path.GetDirectoryName(path),System.IO.Path.GetFileName(path)+".viewer.*.after.intent"))
        {
            RemoteOutputHostSource value=null;
            try
            {
                var intent=RemoteOutputXml(intentPath);var request=intent?.Element("before")?.Element("nativeViewer");string generation=(string)request?.Attribute("generation");
                if((string)intent?.Attribute("operation")!=operation.ToString("N")||(string)intent?.Attribute("world")!=(string)original.Attribute("world")||
                    (string)request?.Attribute("stage")!="after"||!RebirthWorkstationCompletionOriginal.ValidKey(generation)||
                    intentPath!=path+".viewer."+generation+".after.intent"||(string)request.Attribute("nonce")!=nonce.ToString("N")||
                    (string)request.Attribute("source")!=hash)continue;
                var active=request.Element("activeBefore")?.Elements().SingleOrDefault();
                if(active==null||RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting))!=generation)continue;
                string continuationPath=null;XElement continuation=null,continuationSaved=null;byte[] continuationImage=null,continuationSavedImage=null;
                if(!XNode.DeepEquals(active,before)&&!TryReadRemoteOutputFinalContinuation(path,operation,nonce,hash,before,active,
                    out continuationPath,out continuation,out continuationSaved,out continuationImage,out continuationSavedImage))continue;
                string savedPath=path+".viewer."+generation+".after",inventoryPath=path+".viewer."+generation+".inventory";
                var saved=RemoteOutputXml(savedPath);var receipt=saved?.Element("before")?.Element("nativeViewerSaved");
                var inventory=RemoteOutputXml(inventoryPath);var accepted=inventory?.Element("before")?.Element("nativeViewerInventoryAccepted");
                var delivery=request.Element("physicalDelivery")?.Elements().SingleOrDefault();var effect=request.Element("sameOwnerEffect")?.Elements().SingleOrDefault();
                string digest=RemoteOutputRequestDigest(request);
                if(receipt==null||accepted==null||delivery?.Name!="nativeOwningDelivery"||
                    (string)delivery.Attribute("sink")!="Inventory"||effect!=null&&(effect.Name!="nativeViewerEffect"||(bool?)effect.Attribute("nativeOriginalReturned")!=true||
                    (string)effect.Attribute("operation")!=operation.ToString("N")||(string)effect.Attribute("nonce")!=nonce.ToString("N")||(string)effect.Attribute("source")!=hash||
                    !XNode.DeepEquals(effect.Element("initialBefore")?.Elements().SingleOrDefault(),before))||
                    (string)saved.Attribute("operation")!=operation.ToString("N")||(string)saved.Attribute("world")!=(string)original.Attribute("world")||
                    (string)inventory.Attribute("operation")!=operation.ToString("N")||(string)inventory.Attribute("world")!=(string)original.Attribute("world")||
                    (string)receipt.Attribute("source")!=hash||(string)receipt.Attribute("nonce")!=nonce.ToString("N")||(string)receipt.Attribute("generation")!=generation||
                    (string)receipt.Attribute("digest")!=digest||(string)receipt.Attribute("acceptedWire")!=(string)request.Attribute("pdsDigest")||
                    (int?)receipt.Attribute("acceptedWireLength")!=(int?)request.Attribute("pdsLength")||(string)accepted.Attribute("generation")!=generation||
                    !XNode.DeepEquals(receipt.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame"))||
                    !XNode.DeepEquals(accepted.Element("after")?.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame")))continue;
                int count=1+RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"extrasNative").Length;
                if(count<1||count>65)continue;
                string prefixPath=path+".viewer."+generation+".prefix-"+count+".intent";
                var embedded=request.Element("nativeOutputOriginal")?.Elements().SingleOrDefault();
                var prefixRequest=embedded!=null?request:RemoteOutputXml(prefixPath)?.Element("before")?.Element("nativeViewer");var local=embedded??prefixRequest?.Element("nativeOutputPrefix");
                if(embedded!=null&&embedded.Name!="nativeOutputPrefix")continue;
                if((int?)local?.Attribute("count")!=count||!Guid.TryParseExact((string)local.Attribute("operation"),"N",out var localOperation)||localOperation==Guid.Empty||
                    !RebirthWorkstationCompletionOriginal.ValidKey((string)local.Attribute("original"))||
                    (string)prefixRequest.Attribute("nonce")!=nonce.ToString("N")||(string)prefixRequest.Attribute("source")!=hash||
                    !XNode.DeepEquals(prefixRequest.Element("activeBefore")?.Elements().SingleOrDefault(),active))continue;
                string nativePath=(string)receipt.Attribute("retainedNative");
                if(nativePath!=savedPath+".native")continue;
                string firstGeneration=effect==null?RebirthWorkstationCompletionOriginal.Hash(before.ToString(SaveOptions.DisableFormatting)):
                    (string)effect.Element("initialViewerBefore")?.Elements().SingleOrDefault()?.Attribute("generation");
                if(!RebirthWorkstationCompletionOriginal.ValidKey(firstGeneration))continue;
                string firstPath=path+".viewer."+firstGeneration+".before",firstInventoryPath=path+".viewer."+firstGeneration+".inventory-before";
                var firstIntent=RemoteOutputXml(firstPath+".intent")?.Element("before")?.Element("nativeViewer");
                var firstSaved=RemoteOutputXml(firstPath)?.Element("before")?.Element("nativeViewerSaved");
                var firstInventory=RemoteOutputXml(firstInventoryPath)?.Element("before")?.Element("nativeViewerInventoryBeforeAccepted");
                string activePath=path+".viewer."+generation+".before",activeInventoryPath=path+".viewer."+generation+".inventory-before";
                var activeIntent=RemoteOutputXml(activePath+".intent")?.Element("before")?.Element("nativeViewer");
                var activeSaved=RemoteOutputXml(activePath)?.Element("before")?.Element("nativeViewerSaved");
                var activeInventory=RemoteOutputXml(activeInventoryPath)?.Element("before")?.Element("nativeViewerInventoryBeforeAccepted");
                var rootSaved=RemoteOutputXml(path+".saved")?.Element("before")?.Element("remoteNativeBeforeSaved");
                if(firstIntent==null||firstSaved==null||firstInventory==null||(string)firstIntent.Attribute("stage")!="before"||
                    !XNode.DeepEquals(firstIntent.Element("activeBefore")?.Elements().SingleOrDefault(),before)||
                    (string)firstSaved.Attribute("digest")!=RemoteOutputRequestDigest(firstIntent)||(string)firstSaved.Attribute("source")!=hash||
                    (string)firstSaved.Attribute("nonce")!=nonce.ToString("N")||(string)firstSaved.Attribute("generation")!=firstGeneration||
                    (string)rootSaved?.Attribute("source")!=hash||(string)rootSaved?.Attribute("nonce")!=nonce.ToString("N")||
                    effect!=null&&(!XNode.DeepEquals(firstIntent,effect.Element("initialViewerBefore")?.Elements().SingleOrDefault())||
                    !XNode.DeepEquals(firstSaved.Element("nativeOwnerFrame"),effect.Element("ownerBefore")?.Elements().SingleOrDefault()))||
                    !XNode.DeepEquals(firstInventory.Element("nativeOwnerFrame"),firstSaved.Element("nativeOwnerFrame"))||
                    (string)firstSaved.Attribute("retainedNative")!=firstPath+".native")continue;
                if(activeIntent==null||activeSaved==null||activeInventory==null||(string)activeIntent.Attribute("stage")!="before"||
                    (string)activeIntent.Attribute("generation")!=generation||(string)activeIntent.Attribute("nonce")!=nonce.ToString("N")||(string)activeIntent.Attribute("source")!=hash||
                    !XNode.DeepEquals(activeIntent.Element("activeBefore")?.Elements().SingleOrDefault(),active)||
                    (string)activeSaved.Attribute("digest")!=RemoteOutputRequestDigest(activeIntent)||(string)activeSaved.Attribute("source")!=hash||
                    (string)activeSaved.Attribute("nonce")!=nonce.ToString("N")||(string)activeSaved.Attribute("generation")!=generation||
                    (string)activeSaved.Attribute("retainedNative")!=activePath+".native"||
                    !XNode.DeepEquals(activeSaved.Element("nativeOwnerFrame"),activeIntent.Element("nativeOwnerFrame"))||
                    !XNode.DeepEquals(activeInventory.Element("nativeOwnerFrame"),activeSaved.Element("nativeOwnerFrame"))||
                    !XNode.DeepEquals(accepted.Element("nativeOwnerFrame"),activeSaved.Element("nativeOwnerFrame")))continue;
                var dependencies=new Dictionary<string,byte[]>();
                foreach(string dependency in new[]{firstPath+".intent",firstPath,firstInventoryPath,activePath+".intent",activePath,activeInventoryPath,path+".saved"}.Distinct())
                {var retained=RebirthWorkstationCompletionPublication.CopyPublishedBytes(dependency);if(retained==null){dependencies=null;break;}dependencies.Add(dependency,retained);}
                if(dependencies==null)continue;
                if(continuationPath!=null){dependencies.Add(continuationPath,continuationImage);dependencies.Add(continuationPath+".saved",continuationSavedImage);}
                value=new RemoteOutputHostSource{World=world,Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),Path=path,Operation=operation,Nonce=nonce,Birth=birth,
                    Hash=hash,Image=image,Original=original,Before=active,RootBefore=before,Viewer=(string)original.Element("before").Element("remoteNativeOriginal").Attribute("viewer"),
                    Creation=(string)original.Element("before").Element("remoteNativeOriginal").Attribute("creation"),Deaths=(int)original.Element("before").Element("remoteNativeOriginal").Attribute("deaths"),
                    Position=new Vector3i((int)before.Attribute("x"),(int)before.Attribute("y"),(int)before.Attribute("z")),Offset=(int)active.Attribute("nativeElapsedOffset"),
                    PrefixCount=count,Request=request,Saved=saved,PrefixIntentPath=intentPath,PrefixSavedPath=savedPath,InventoryPath=inventoryPath,
                    PrefixIntent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(intentPath),PrefixSaved=RebirthWorkstationCompletionPublication.CopyPublishedBytes(savedPath),
                    Inventory=RebirthWorkstationCompletionPublication.CopyPublishedBytes(inventoryPath),NativePath=nativePath,NativeHash=(string)receipt.Attribute("nativeDigest"),NativeLength=(int)receipt.Attribute("nativeLength"),
                    FinalCallbackSaved=true,FinalViewerEntity=(int?)receipt.Attribute("viewerEntity"),FinalLocalPrefix=new XElement(local),FinalLocalIntentPath=embedded!=null?intentPath:prefixPath,FinalLocalIntent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(embedded!=null?intentPath:prefixPath),
                    FinalDependencies=dependencies,FinalBeforeGeneration=firstGeneration,FinalBeforeNativePath=(string)firstSaved.Attribute("retainedNative"),FinalBeforeNativeHash=(string)firstSaved.Attribute("nativeDigest"),FinalBeforeNativeLength=(int)firstSaved.Attribute("nativeLength"),
                    FinalActiveBeforeNativePath=(string)activeSaved.Attribute("retainedNative"),FinalActiveBeforeNativeHash=(string)activeSaved.Attribute("nativeDigest"),FinalActiveBeforeNativeLength=(int)activeSaved.Attribute("nativeLength"),
                    FinalContinuationPath=continuationPath,FinalContinuationRecord=continuation,FinalContinuationSavedRecord=continuationSaved,FinalContinuationImage=continuationImage,FinalContinuationSavedImage=continuationSavedImage};
                if(effect==null)
                {
                    // The after PDS deliberately carries no synthetic direct
                    // effect. Recover its Add description from the separately
                    // saved accepted output prefix, then require a real native
                    // load of the full predicted terminal body below.
                    var deferredRequest=RemoteOutputXml(prefixPath)?.Element("before")?.Element("nativeViewer");
                    var deferredPrefix=deferredRequest?.Element("nativeOutputPrefix");
                    var retainedPrefix=RebirthWorkstationCompletionPublication.CopyPublishedBytes(prefixPath);
                    if(request.Element("sameOwnerEffect")!=null||!value.FinalViewerEntity.HasValue||deferredRequest?.Element("sameOwnerEffect")!=null||
                        (string)deferredRequest?.Attribute("nonce")!=nonce.ToString("N")||(string)deferredRequest.Attribute("source")!=hash||
                        (string)deferredRequest.Attribute("generation")!=generation||!XNode.DeepEquals(deferredPrefix,local)||
                        retainedPrefix==null||!BindRemoteDeferredOutputPrefix(value,deferredRequest.Element("deferredNativeContribution")))
                    {value.Dispose();continue;}
                    value.FinalDependencies[prefixPath]=retainedPrefix;
                }
                if(value.PrefixIntent==null||value.PrefixSaved==null||value.Inventory==null||value.FinalLocalIntent==null||!value.Current()||
                    !TryPredictRemoteOutputFinalQueue(value,out value.FinalQueueBody,out value.FinalQueueOffset)){value.Dispose();continue;}
                if(result!=null){value.Dispose();result.Dispose();return null;}result=value;value=null;
            }
            catch{/* No native owner/station authority is created by file parsing. */}
            finally{value?.Dispose();}
        }
        return result;
    }
    private static bool TryReadRemoteOutputFinalContinuation(string path,Guid operation,Guid nonce,string hash,XElement root,XElement active,
        out string matched,out XElement record,out XElement saved,out byte[] image,out byte[] savedImage)
    {
        matched=null;record=saved=null;image=savedImage=null;
        if(active.Name!="nativeOwningBefore"||active.Element("cachedPredecessor")==null||!QueueStableEqual((string)root.Element("nativeQueueFull"),(string)active.Element("nativeQueueFull"))||
            (string)root.Element("recipeFull")!=(string)active.Element("recipeFull"))return false;
        foreach(string field in new[]{"operation","world","viewer","creation","deaths","startingOwner","x","y","z","block","slot","beforeCount","extrasDefinition"})
            if((string)active.Attribute(field)!=(string)root.Attribute(field))return false;
        for(int index=1;index<=64;index++)
        {
            string candidate=path+".phase."+index;var body=RemoteOutputXml(candidate);var phase=body?.Element("before")?.Element("remoteNativeCachedBefore");
            var seal=RemoteOutputXml(candidate+".saved");var native=seal?.Element("before")?.Element("remoteNativeCachedBeforeSaved");
            if((string)body?.Attribute("operation")!=operation.ToString("N")||(string)seal?.Attribute("operation")!=operation.ToString("N")||
                (string)body.Attribute("world")!=(string)root.Attribute("world")||(string)seal.Attribute("world")!=(string)root.Attribute("world")||
                (string)phase?.Attribute("nonce")!=nonce.ToString("N")||(string)phase?.Attribute("source")!=hash||
                (string)native?.Attribute("nonce")!=nonce.ToString("N")||(string)native?.Attribute("source")!=hash||
                !XNode.DeepEquals(phase.Element("nativeOwningBefore"),active))continue;
            var transfer=phase.Element("actualCachedSpaceTransfer");var after=transfer?.Element("after");
            if(!XNode.DeepEquals(transfer?.Element("cachedFull")?.Elements().SingleOrDefault(),active.Element("cachedPredecessor")?.Elements().SingleOrDefault())||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(Convert.FromBase64String(after.Value),(int)after.Attribute("offset"),
                    Convert.FromBase64String(active.Element("nativeBefore").Value),(int)active.Attribute("nativeElapsedOffset"))||
                ((long?)native.Attribute("offset")??-1)<8||((int?)native.Attribute("length")??0)<1||((string)native.Attribute("tile"))?.Length!=64)return false;
            image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(candidate);savedImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(candidate+".saved");
            if(image==null||savedImage==null)return false;matched=candidate;record=body;saved=seal;return true;
        }
        return false;
    }
    private static bool TryPredictRemoteOutputFinalQueue(RemoteOutputHostSource source,out byte[] body,out int offset)
    {
        body=null;offset=0;
        try
        {
            var before=source.Before;var delivery=source.Request.Element("physicalDelivery")?.Elements().SingleOrDefault();
            foreach(string name in new[]{"operation","world","save","viewer","creation","deaths","stationBirth","extrasDefinition","startingOwner","startingOwnerKnown","startingOwnerKey","startingOwnerCreation","startingOwnerDeaths","x","y","z","block","slot","beforeCount","nativeQueueCountBefore","retry"})
                if((string)before.Attribute(name)!=(string)delivery?.Attribute(name))return false;
            byte[] original=source.DeferredAddBody??Convert.FromBase64String(before.Element("nativeBefore").Value);
            if(!RebirthProductiveEarnedNativeBody.TryRead(original,source.DeferredAddBody==null?source.Offset:source.DeferredAddOffset,out var native,out _)||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(before.Element("uiQueueFull").Value,out var ui))return false;
            int index=(int)before.Attribute("slot"),count=(int)before.Attribute("beforeCount");
            if(index<0||index>=native.Queue.Length||ui.Length!=native.Queue.Length||native.Queue[index]?.Recipe==null||ui[index]?.Recipe==null||count<1||
                native.Queue[index].Multiplier!=count||ui[index].Multiplier!=count||native.Queue[index].StartingEntityId!=(int)before.Attribute("startingOwner")||
                RebirthProductiveEarnedUiQueueOwner.Binding(ui[index])!=RebirthProductiveEarnedUiQueueOwner.Binding(native.Queue[index])||
                (int?)delivery.Attribute("afterCount")!=count-1||(string)delivery.Element("recipeFull")!=(string)before.Element("recipeFull")||
                !RebirthCamperInventorySnapshot.Capture(native.Output).SequenceEqual(RebirthCamperInventorySnapshot.Capture(RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"outputAfter"))))return false;
            var row=native.Queue[index];row.Multiplier--;
            if(row.Multiplier==0){if(source.DeferredAddBody!=null&&!source.FinalViewerEntity.HasValue)return false;
                row.Recipe=null;row.CraftingTimeLeft=0;row.IsCrafting=false;row.Quality=1;row.StartingEntityId=source.FinalViewerEntity??(int)before.Attribute("startingOwner");row.OneItemCraftTime=0;}
            else{row.CraftingTimeLeft=(bool?)before.Attribute("retry")==true?ui[index].OneItemCraftTime:ui[index].CraftingTimeLeft+ui[index].OneItemCraftTime;row.IsCrafting=ui[index].IsCrafting;}
            body=RebirthProductiveEarnedNativeBody.Encode(native,0,out offset);return true;
        }
        catch{return false;}
    }
}
