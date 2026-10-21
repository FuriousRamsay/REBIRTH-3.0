using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        private static string CachedKey(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,string generation)=>
            "cached:"+action.Operation.ToString("N")+":"+action.Action.ToString("N")+":"+generation;
        internal static bool ReceiveCached(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement request,out string stage,out string digest)
        {
            stage=digest=null;ViewerSource created=null;string key=null;IDisposable pendingMemory=null,pendingRetention=null;
            try
            {
                if(action?.Current()!=true||(int?)request.Attribute("version")!=1||
                    (string)request.Attribute("action")!=action.Action.ToString("N")||(string)request.Attribute("source")!=action.SourceHash)return false;
                string kind=(string)request.Attribute("stage"),generation=(string)request.Attribute("generation");
                if(kind?.StartsWith("prefix-",StringComparison.Ordinal)==true)return ReceiveOutputPrefixCore(null,action,request,out stage,out digest);
                bool after=kind=="after"||kind=="afterQuery",query=kind=="beforeQuery"||kind=="afterQuery";
                if(kind!="before"&&!after&&!query||generation?.Length!=64)return false;key=CachedKey(action,generation);
                lock(gate)
                {
                    Sweep();
                    if(!sources.TryGetValue(key,out var source))
                    {
                        var active=request.Element("activeBefore")?.Elements().SingleOrDefault();
                        if(after||query||sources.Count>=12||sources.Values.Select(value=>value.SourceOperation).Distinct().Count()>=4&&
                            !sources.Values.Any(value=>value.SourceOperation==action.Operation)||active==null||
                            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*MeasurePublicationElement(request)+96L*1024*1024),out pendingMemory))return false;
                        if(!ValidateCachedActiveBefore(action,active)||RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting))!=generation)return false;
                        pendingRetention=action.Retain();if(pendingRetention==null)return false;
                        source=new ViewerSource{Cached=action,CachedNativeHead=new WeakReference<byte[]>(action.Issue.Cache.Native),Active=new XElement(active),Generation=generation,Memory=pendingMemory,Retention=pendingRetention,
                            BagLocks=(string)request.Attribute("bagLocks"),BeltLocks=(string)request.Attribute("beltLocks"),BeltCount=(int)request.Attribute("beltCount"),
                            BeltPreferred=(string)request.Attribute("beltPreferred"),EquipmentPreferred=(string)request.Attribute("equipmentPreferred")};
                        pendingMemory=pendingRetention=null;
                        if(!ValidLocks(source.BagLocks)||!ValidLocks(source.BeltLocks)||source.BeltCount<1||source.BeltCount>255||!ValidOwnerPreferences(source)){source.Dispose();return false;}
                        sources.Add(key,source);created=source;
                    }
                    if(!ReferenceEquals(source.Cached,action)||!source.Current())return false;
                    var token=after?source.After:source.Before;
                    if(!query)
                    {
                        var owner=request.Element("nativeOwnerFrame");
                        string native=(string)request.Attribute("pdsDigest");int length=(int?)request.Attribute("pdsLength")??0;
                        if(owner==null||owner.Elements("recipes").Single().Elements("recipe").Count()!=1||
                            (string)owner.Element("recipes").Element("recipe").Attribute("name")!=action.Recipe.GetName()||
                            !XNode.DeepEquals(request.Element("activeBefore")?.Elements().SingleOrDefault(),source.Active)||
                            (string)request.Attribute("bagLocks")!=source.BagLocks||(string)request.Attribute("beltLocks")!=source.BeltLocks||
                            (int?)request.Attribute("beltCount")!=source.BeltCount||native?.Length!=64||
                            (string)request.Attribute("beltPreferred")!=source.BeltPreferred||(string)request.Attribute("equipmentPreferred")!=source.EquipmentPreferred||
                            native.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||length<1||length>16*1024*1024)return false;
                        if(after)
                        {
                            var delivery=request.Element("physicalDelivery")?.Elements().SingleOrDefault();
                            if(source.Before?.Current()!=true||!source.BeforeInventoryCurrent()||delivery?.Name!="nativeOwningDelivery"||
                                (string)delivery.Attribute("operation")!=action.Operation.ToString("N")||
                                (string)delivery.Element("bagBefore")!=(string)source.Before.Frame.Element("bag")||
                                (string)delivery.Element("beltBefore")!=(string)source.Before.Frame.Element("belt")||
                                (string)owner.Element("bag")!=(string)delivery.Element("bagAfter")||
                                (string)owner.Element("belt")!=(string)delivery.Element("beltAfter")||
                                !EquipmentFrameMatches(source.Before.Frame,delivery,"equipmentBefore")||!EquipmentFrameMatches(owner,delivery,"equipmentAfter")||
                                source.Delivery!=null&&!XNode.DeepEquals(source.Delivery,delivery))return false;
                            source.Delivery=new XElement(delivery);
                        }
                        else if((string)owner.Element("bag")!=(string)source.Active.Element("bagBefore")||
                            (string)owner.Element("belt")!=(string)source.Active.Element("beltBefore")||!EquipmentFrameMatches(owner,source.Active,"equipmentBefore"))return false;
                        if(token!=null&&!XNode.DeepEquals(token.Request,request))return false;
                        if(token==null)
                        {
                            long measured=MeasurePublicationElement(request)+(source.Before==null?0:MeasurePublicationElement(source.Before.Request));
                            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,checked(16*measured+96L*1024*1024)))return false;
                            token=SavedViewerFile.Issue(source,after,request);
                            if(after)
                            {
                                var predecessor=source.Prefix??source.Before;
                                if(predecessor?.Current()!=true||source.Prefix!=null&&!source.Prefix.PrefixInventoryCurrent()||
                                    predecessor.AcceptedHead==null||!predecessor.AcceptedHead.TryGetTarget(out var previous)||
                                    !ReferenceEquals(action.Sender.latestPlayerData,previous))return false;
                                token.PreviousHead=predecessor.AcceptedHead;token.PreviousHeadNull=false;
                            }
                            var intent=new XElement("workstationCompletionIntent",new XAttribute("operation",action.Operation.ToString("N")),
                                new XAttribute("world",action.WorldGuid),new XElement("before",new XElement(request)));
                            if(!RebirthWorkstationCompletionPublication.Write(action.Path+".viewer."+generation+"."+(after?"after":"before")+".intent",intent))return false;
                            if(after)source.After=token;else source.Before=token;
                        }
                    }
                    if(token==null||query&&((string)request.Attribute("digest")!=token.Digest||
                        (string)request.Attribute("pdsDigest")!=token.ExpectedNative||(int?)request.Attribute("pdsLength")!=token.ExpectedLength))return false;
                    if(token.Accepted&&token.Published&&token.Receipt==null&&!Publish(token))return false;
                    bool saved=token.Current()&&(after?source.InventoryCurrent():source.BeforeInventoryCurrent());
                    digest=token.Digest;stage="cachedViewer"+(after?"After":"Before")+(saved?"Saved":"Armed");return true;
                }
            }
            catch{return false;}
            finally{pendingRetention?.Dispose();pendingMemory?.Dispose();if(created!=null&&created.Before==null){lock(gate){if(sources.TryGetValue(key,out var current)&&ReferenceEquals(current,created))sources.Remove(key);created.Dispose();}}}
        }
        private static bool ValidateCachedActiveBefore(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active)
        {
            if(action?.Current()!=true||active?.Name!="nativeOwningBefore"||(bool?)active.Attribute("retry")!=true)return false;
            var cache=action.Issue.Cache;var original=action.Before;
            foreach(string name in new[]{"operation","world","viewer","creation","deaths","x","y","z","block","slot","beforeCount","startingOwner",
                "startingOwnerKnown","startingOwnerKey","startingOwnerCreation","startingOwnerDeaths","extrasDefinition"})
                if((string)active.Attribute(name)!=(string)original.Attribute(name))return false;
            if((string)active.Element("recipeFull")!=(string)original.Element("recipeFull")||
                !XNode.DeepEquals(active.Element("cachedPredecessor")?.Elements().SingleOrDefault(),cache.CurrentFull))return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                string encoded=(string)active.Element("nativeBefore");
                int maximum=RebirthProductiveEarnedNativeBody.MaximumBodyBytes;
                if(decoder==null||encoded==null||encoded.Length<4||encoded.Length%4!=0||encoded.Length>4*((maximum+2)/3))return false;
                int padding=encoded.EndsWith("==",StringComparison.Ordinal)?2:encoded.EndsWith("=",StringComparison.Ordinal)?1:0;
                long decoded=3L*(encoded.Length/4)-padding;if(decoded<8||decoded>maximum)return false;
                byte[] native=Convert.FromBase64String(encoded);if(native.Length!=decoded||Convert.ToBase64String(native)!=encoded)return false;
                int offset=(int)active.Attribute("nativeElapsedOffset");
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(native,offset,cache.Native,cache.Offset)||
                    !RebirthProductiveEarnedNativeBody.TryRead(native,offset,out var body,out _)||
                    !RebirthRemoteResourcePlacedStationQueue.TryDecode((string)active.Element("uiQueueFull"),out var ui)||ui.Length!=body.Queue.Length)return false;
                foreach(string name in new[]{"inputBefore","fuelBefore","toolsBefore","outputBefore","bagBefore","beltBefore"})RebirthProductiveEarnedUiRemote.ReadGrid(active,name);
                if(!CachedViewerSame(body.Input,RebirthProductiveEarnedUiRemote.ReadGrid(active,"inputBefore"))||
                    !CachedViewerSame(body.Fuel,RebirthProductiveEarnedUiRemote.ReadGrid(active,"fuelBefore"))||
                    !CachedViewerSame(body.Tools,RebirthProductiveEarnedUiRemote.ReadGrid(active,"toolsBefore"))||
                    !CachedViewerSame(body.Output,RebirthProductiveEarnedUiRemote.ReadGrid(active,"outputBefore")))return false;
                int slot=(int)original.Attribute("slot");if(slot<0||slot>=ui.Length)return false;
                for(int i=0;i<ui.Length;i++)
                {
                    var expected=body.Queue[i];var actual=ui[i];
                    if(RebirthWorkstationCompletionPipeline.RecipeBinding(expected)!=RebirthWorkstationCompletionPipeline.RecipeBinding(actual)||expected.Multiplier!=actual.Multiplier)return false;
                    if(i==slot)
                    {if(actual.CraftingTimeLeft!=(float)cache.CurrentFull.Attribute("cachedTime")||actual.IsCrafting!=(bool)cache.CurrentFull.Attribute("cachedCrafting"))return false;}
                    else if(actual.CraftingTimeLeft!=expected.CraftingTimeLeft||actual.IsCrafting!=expected.IsCrafting)return false;
                }
                return true;
            }
        }
        private static bool CachedViewerSame(ItemStack[] left,ItemStack[] right)=>RebirthCamperInventorySnapshot.Capture(left).SequenceEqual(RebirthCamperInventorySnapshot.Capture(right));
        internal static bool TryReadCachedBefore(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active,out SavedViewerFile token)
        {
            token=null;if(action?.Current()!=true||active==null)return false;
            string generation=RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting));
            lock(gate)
            {
                if(!sources.TryGetValue(CachedKey(action,generation),out var source)||!ReferenceEquals(source.Cached,action)||
                    !source.Current()||!XNode.DeepEquals(source.Active,active)||source.Before?.Current()!=true||!source.BeforeInventoryCurrent())return false;
                token=source.Before;return true;
            }
        }
        internal static bool TryBindCachedSavedBefore(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action)
        {
            if(action?.Current()!=true)return false;
            lock(gate)
            {
                var values=sources.Values.Where(value=>ReferenceEquals(value.Cached,action)&&value.Current()&&value.Before?.Current()==true&&
                    value.BeforeInventoryCurrent()&&value.After==null&&value.Before.AcceptedHead!=null&&
                    value.Before.AcceptedHead.TryGetTarget(out var actual)&&ReferenceEquals(actual,action.Sender.latestPlayerData)).ToArray();
                if(values.Length!=1)return false;
                var token=values[0].Before;var bytes=WireImage(action.Sender.latestPlayerData);
                if(bytes.Length!=token.ExpectedLength||NativeDigest(bytes)!=token.ExpectedNative)return false;
                return action.BindSavedBefore(token);
            }
        }
        internal static bool TryReadCachedAfter(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active,XElement delivery,out SavedViewerFile before,out SavedViewerFile after)
        {
            before=after=null;if(!TryReadCachedBefore(action,active,out var first))return false;
            lock(gate)
            {
                var source=first.Source;
                if(source.After?.Current()!=true||!source.InventoryCurrent()||!XNode.DeepEquals(source.Delivery,delivery))return false;
                before=first;after=source.After;return true;
            }
        }
        internal static bool ValidateCachedPhysicalActor(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active,XElement delivery,ItemStack primary,ItemStack[] extras,bool inventory)
        {
            if(!TryReadCachedAfter(action,active,delivery,out var before,out var after)||
                (string)delivery.Element("bagBefore")!=(string)before.Frame.Element("bag")||
                (string)delivery.Element("beltBefore")!=(string)before.Frame.Element("belt")||
                (string)delivery.Element("bagAfter")!=(string)after.Frame.Element("bag")||
                (string)delivery.Element("beltAfter")!=(string)after.Frame.Element("belt")||
                !EquipmentFrameMatches(before.Frame,delivery,"equipmentBefore")||!EquipmentFrameMatches(after.Frame,delivery,"equipmentAfter"))return false;
            if(!inventory)return (string)delivery.Element("bagBefore")== (string)delivery.Element("bagAfter")&&
                (string)delivery.Element("beltBefore")== (string)delivery.Element("beltAfter");
            return ValidateOwnerInsertionPlan(before.Source,delivery,primary,extras);
        }
        internal static XElement CopySavedCachedPhysicalActor(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active,XElement delivery)
        {
            if(!TryReadCachedAfter(action,active,delivery,out var before,out var after))return null;
            var source=before.Source;
            long bytes=before.Receipt.LongLength+after.Receipt.LongLength+source.BeforeInventoryReceipt.LongLength+source.InventoryReceipt.LongLength+65536;
            if(!action.Issue.Cache.ReserveRetirementWorkspace(checked(16*bytes)))return null;
            return new XElement("cachedPhysicalActorSaved",new XAttribute("operation",action.Operation.ToString("N")),
                new XAttribute("source",action.SourceHash),new XAttribute("nonce",action.Action.ToString("N")),new XAttribute("generation",source.Generation),
                new XAttribute("actionPath",action.Path),new XAttribute("actionDigest",HydrationHash(action.Image)),
                new XElement("savedBefore",new XAttribute("path",before.ReceiptPath),new XAttribute("digest",HydrationHash(before.Receipt))),
                new XElement("savedAfter",new XAttribute("path",after.ReceiptPath),new XAttribute("digest",HydrationHash(after.Receipt))),
                new XElement("inventoryBefore",new XAttribute("path",source.BeforeInventoryPath),new XAttribute("digest",HydrationHash(source.BeforeInventoryReceipt))),
                new XElement("inventoryAfter",new XAttribute("path",source.InventoryPath),new XAttribute("digest",HydrationHash(source.InventoryReceipt))));
        }
        // Historical saved event qualification only. The caller must still
        // own an actual saved cache native/load source before retaining a
        // completion; this function never reconstructs an action token.
        internal static bool MatchesSavedCachedPhysicalActor(string cachePath,string original,XElement evidence,XElement active,XElement delivery,Func<long,bool> reserve)
        {
            try
            {
                if(evidence?.Name!="cachedPhysicalActorSaved"||(string)evidence.Attribute("source")!=original||
                    !Guid.TryParseExact((string)evidence.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                    !Guid.TryParseExact((string)evidence.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty)return false;
                string action=(string)evidence.Attribute("actionPath"),prefix=cachePath+".remote-action.";
                if(action==null||!action.StartsWith(prefix,StringComparison.Ordinal)||!int.TryParse(action.Substring(prefix.Length),out int epoch)||epoch<0||epoch>64)return false;
                string generation=(string)evidence.Attribute("generation");if(!RebirthWorkstationCompletionOriginal.ValidKey(generation)||
                    generation!=RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting)))return false;
                string root=action+".viewer."+generation;
                string[] paths={action,root+".before",root+".after",root+".before.intent",root+".after.intent",root+".inventory-before",root+".inventory"};
                long total=0;foreach(string path in paths){long length=RebirthWorkstationCompletionPublication.PublishedLength(path);if(length<1)return false;total=checked(total+length);}
                if(reserve==null||!reserve(checked(16*total+65536)))return false;
                var actionBytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(action);
                var request=RebirthWorkstationCompletionPublication.ReadPrimary(action)?.Element("cachedPhysicalAttempt")?.Element("cachedPhysicalPrepare");
                if(actionBytes==null||HydrationHash(actionBytes)!=(string)evidence.Attribute("actionDigest")||
                    (string)request?.Attribute("action")!=nonce.ToString("N")||(string)request.Attribute("original")!=original||
                    !HistoricalViewerSeal(evidence.Element("savedBefore"),root+".before",evidence,out var before)||
                    !HistoricalViewerSeal(evidence.Element("savedAfter"),root+".after",evidence,out var after))return false;
                var first=RebirthWorkstationCompletionPublication.ReadPrimary(root+".before.intent")?.Element("before")?.Elements().SingleOrDefault();
                var last=RebirthWorkstationCompletionPublication.ReadPrimary(root+".after.intent")?.Element("before")?.Elements().SingleOrDefault();
                if(first?.Name!="cachedViewer"||last?.Name!="cachedViewer"||(string)first.Attribute("action")!=nonce.ToString("N")||
                    (string)last.Attribute("action")!=nonce.ToString("N")||RequestDigest(first)!=(string)before.Attribute("digest")||
                    RequestDigest(last)!=(string)after.Attribute("digest")||!XNode.DeepEquals(first.Element("activeBefore")?.Elements().SingleOrDefault(),active)||
                    !XNode.DeepEquals(last.Element("physicalDelivery")?.Elements().SingleOrDefault(),delivery)||
                    !XNode.DeepEquals(first.Element("nativeOwnerFrame"),before.Element("nativeOwnerFrame"))||
                    !XNode.DeepEquals(last.Element("nativeOwnerFrame"),after.Element("nativeOwnerFrame")))return false;
                foreach(var pair in new[]{new[]{"inventoryBefore",root+".inventory-before"},new[]{"inventoryAfter",root+".inventory"}})
                {
                    var link=evidence.Element(pair[0]);var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(pair[1]);
                    if((string)link?.Attribute("path")!=pair[1]||bytes==null||HydrationHash(bytes)!=(string)link.Attribute("digest"))return false;
                }
                return true;
            }
            catch{return false;}
        }
    }
}
