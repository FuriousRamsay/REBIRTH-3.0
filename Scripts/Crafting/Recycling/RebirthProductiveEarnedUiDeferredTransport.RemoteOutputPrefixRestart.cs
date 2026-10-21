using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        private static SavedViewerFile RestoreRemoteOutputStage(ViewerSource source,string kind,XElement request)
        {
            string generation=(string)request.Attribute("generation");
            if(!RebirthWorkstationCompletionOriginal.ValidKey(generation))return null;
            string path=source.SourcePath+".viewer."+generation+"."+kind;
            var receipt=RebirthWorkstationCompletionPublication.ReadPrimary(path);var saved=receipt?.Element("before")?.Element("nativeViewerSaved");
            if((string)receipt?.Attribute("operation")!=source.SourceOperation.ToString("N")||(string)receipt?.Attribute("world")!=source.SourceWorldGuid||
                (string)saved?.Attribute("source")!=source.SourceHash||(string)saved?.Attribute("nonce")!=source.SourceNonce.ToString("N")||
                (string)saved?.Attribute("generation")!=generation||(string)saved?.Attribute("digest")!=RequestDigest(request)||
                !XNode.DeepEquals(saved?.Element("nativeOwnerFrame"),request.Element("nativeOwnerFrame")))return null;
            var token=SavedViewerFile.Issue(source,kind!="before",request);
            token.ReceiptPath=path;token.NativePath=(string)saved.Attribute("retainedNative");
            token.Receipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);token.Native=RebirthWorkstationCompletionPublication.CopyRetainedNativeBytes(token.NativePath);
            token.Path=(string)saved.Attribute("nativePath");token.AcceptedWire=(string)saved.Attribute("acceptedWire");token.AcceptedWireLength=(int)saved.Attribute("acceptedWireLength");
            token.Accepted=token.Published=true;token.AcceptedHead=new WeakReference<PlayerDataFile>(source.SourceSender.latestPlayerData);
            if(token.Native==null||token.Receipt==null||(int)saved.Attribute("nativeLength")!=token.Native.Length||HydrationHash(token.Native)!=(string)saved.Attribute("nativeDigest")||!token.Current())return null;
            return token;
        }
        internal static bool RestoreRemoteOutputPrefix(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,RebirthWorkstationCompletionPipeline.RemoteOutputHostSource authority)
        {
            ViewerSource source=null;IDisposable memory=null,retention=null;
            LoadedNativeContribution loadedContribution=null;
            try
            {
                if(original?.CurrentViewer()!=true||authority?.Current()!=true||authority.Native?.IdentityCurrent()!=true||
                    original.Operation!=authority.Operation||original.Nonce!=authority.Nonce||original.SourceHash!=authority.Hash||
                    !RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(original.Sender.latestPlayerData,authority.Request.Element("nativeOwnerFrame")))return false;
                string generation=(string)authority.Request.Attribute("generation"),key=Key(original,generation);
                lock(gate)
                {
                    if(sources.ContainsKey(key)||initialBefore.ContainsKey(original)||sources.Count>=12)return false;
                    if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out memory)||(retention=original.Retain())==null)return false;
                    source=new ViewerSource{Original=original,Generation=generation,Active=new XElement(authority.Before),Memory=memory,Retention=retention,
                        BagLocks=(string)authority.Request.Attribute("bagLocks"),BeltLocks=(string)authority.Request.Attribute("beltLocks"),BeltCount=(int)authority.Request.Attribute("beltCount"),
                        BeltPreferred=(string)authority.Request.Attribute("beltPreferred"),EquipmentPreferred=(string)authority.Request.Attribute("equipmentPreferred")};memory=retention=null;
                    if(!source.Current()||!ValidLocks(source.BagLocks)||!ValidLocks(source.BeltLocks)||!ValidOwnerPreferences(source))return false;
                    string beforePath=original.Path+".viewer."+generation+".before";
                    var beforeIntent=RebirthWorkstationCompletionPublication.ReadPrimary(beforePath+".intent");var beforeRequest=beforeIntent?.Element("before")?.Element("nativeViewer");
                    if(beforeRequest==null||!XNode.DeepEquals(beforeRequest.Element("activeBefore")?.Elements().SingleOrDefault(),authority.Before))return false;
                    source.Before=RestoreRemoteOutputStage(source,"before",beforeRequest);if(source.Before==null)return false;
                    source.BeforeInventoryPath=original.Path+".viewer."+generation+".inventory-before";
                    source.BeforeInventoryReceipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.BeforeInventoryPath);
                    if(!source.BeforeInventoryCurrent())return false;
                    var beforeProof=RebirthWorkstationCompletionPublication.ReadPrimary(source.BeforeInventoryPath)?.Element("before")?.Element("nativeViewerInventoryBeforeAccepted");
                    if((string)beforeProof?.Attribute("generation")!=generation||!XNode.DeepEquals(beforeProof?.Element("nativeOwnerFrame"),source.Before.Frame))return false;
                    // The complete existing direct native effect is validated
                    // against the exact saved first Before before installation.
                    SavedViewerFile initial=source.Before;
                    string firstGeneration=authority.FinalBeforeGeneration??generation;
                    if(firstGeneration!=generation)
                    {
                        var firstRequest=RebirthWorkstationCompletionPublication.ReadPrimary(original.Path+".viewer."+firstGeneration+".before.intent")?.Element("before")?.Element("nativeViewer");
                        if(firstRequest==null||!XNode.DeepEquals(firstRequest.Element("activeBefore")?.Elements().SingleOrDefault(),authority.RootBefore)||
                            (authority.DeferredAddBody==null&&!XNode.DeepEquals(firstRequest,authority.Request.Element("sameOwnerEffect")?.Elements().SingleOrDefault()?.Element("initialViewerBefore")?.Elements().SingleOrDefault())||
                                authority.DeferredAddBody!=null&&authority.Request.Element("sameOwnerEffect")!=null))return false;
                        initial=RestoreRemoteOutputStage(source,"before",firstRequest);if(initial==null)return false;
                    }
                    initialBefore.Add(original,initial);
                    if(authority.DeferredAddBody!=null&&!TryAttachLoadedNativeContribution(original,authority,out loadedContribution))
                    {initialBefore.Remove(original);return false;}
                    if(authority.FinalCallbackSaved)
                    {
                        source.After=RestoreRemoteOutputStage(source,"after",authority.Request);if(source.After==null){initialBefore.Remove(original);return false;}
                        source.InventoryPath=authority.InventoryPath;source.InventoryReceipt=authority.Inventory;source.InventoryAccepted=true;
                        source.Delivery=new XElement(authority.Request.Element("physicalDelivery").Elements().Single());
                        var finalEffect=authority.Request.Element("sameOwnerEffect")?.Elements().SingleOrDefault();
                        source.DirectEffect=finalEffect==null?null:new XElement(finalEffect);
                        var inventory=RebirthWorkstationCompletionPublication.ReadPrimary(source.InventoryPath)?.Element("before")?.Element("nativeViewerInventoryAccepted");
                        if(loadedContribution!=null)
                        {
                            if(source.DirectEffect!=null||!XNode.DeepEquals(inventory?.Element("nativeOwnerFrame"),source.Before.Frame)||
                                !XNode.DeepEquals(inventory?.Element("after")?.Element("nativeOwnerFrame"),source.After.Frame))
                            {initialBefore.Remove(original);return false;}
                            sources.Add(key,source);
                            if(!loadedContribution.QualifyFinalPhysical(authority)||!source.Current())
                            {sources.Remove(key);initialBefore.Remove(original);return false;}
                            loadedContribution.Commit();source=null;return true;
                        }
                        var contribution=new XElement("sameOwnerEffect",new XAttribute("phase",3),new XElement("activeBefore",new XElement(source.Active)),
                            new XElement("physicalDelivery",new XElement(source.Delivery)),new XElement(source.DirectEffect));
                        // The full validator resolves this exact viewer source
                        // by key. Keep installation scoped until it qualifies.
                        sources.Add(key,source);
                        if(!XNode.DeepEquals(inventory?.Element("nativeOwnerFrame"),source.Before.Frame)||
                            !XNode.DeepEquals(inventory?.Element("after")?.Element("nativeOwnerFrame"),source.After.Frame)||
                            !ValidateSavedSameOwner(original,contribution,out _)||!source.Current())
                        {sources.Remove(key);initialBefore.Remove(original);return false;}
                        source=null;return true;
                    }
                    if(!ValidateViewerOutputPrefix(source,authority.Request,authority.PrefixCount)){initialBefore.Remove(original);return false;}
                    source.Prefix=RestoreRemoteOutputStage(source,"prefix-"+authority.PrefixCount,authority.Request);if(source.Prefix==null){initialBefore.Remove(original);return false;}
                    source.Prefix.PrefixInventoryPath=authority.InventoryPath;source.Prefix.PrefixInventoryReceipt=authority.Inventory;
                    source.Prefix.PrefixBefore=new XElement(source.Before.Frame);source.PrefixCount=authority.PrefixCount;
                    var direct=authority.Request.Element("sameOwnerEffect")?.Elements().SingleOrDefault();
                    source.DirectEffect=direct==null?null:new XElement(direct);
                    if(!source.Prefix.PrefixInventoryCurrent()||!source.Current()){initialBefore.Remove(original);return false;}
                    sources.Add(key,source);source=null;loadedContribution?.Commit();return true;
                }
            }
            catch{lock(gate){initialBefore.Remove(original);if(source!=null)
                foreach(var key in sources.Where(pair=>ReferenceEquals(pair.Value,source)).Select(pair=>pair.Key).ToArray())sources.Remove(key);}return false;}
            finally{loadedContribution?.Dispose();source?.Dispose();memory?.Dispose();retention?.Dispose();}
        }
    }
}
