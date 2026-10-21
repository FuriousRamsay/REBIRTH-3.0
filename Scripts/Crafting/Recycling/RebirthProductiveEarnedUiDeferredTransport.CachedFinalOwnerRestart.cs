using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        // Restores an already saved final owner event for an actual loaded
        // cached action. This does not assert PhysicalAccepted or advance TE
        // state; the queue still needs its separately observed native packet.
        internal static bool RestoreRetainedCachedFinalOwner(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,
            string generation,int count,string digest,Guid localOperation,string originalHash)
        {
            ViewerSource source=null;IDisposable memory=null,retention=null;string key=null;bool installed=false;
            try
            {
                if(action?.Current()!=true||action.Issue.RetainedAction!=action.Action||!RebirthWorkstationCompletionOriginal.ValidKey(generation)||
                    count<1||count>65||localOperation==Guid.Empty)return false;
                key=CachedKey(action,generation);string root=action.Path+".viewer."+generation;
                lock(gate)
                {
                    if(sources.TryGetValue(key,out var current))return ReferenceEquals(current.Cached,action)&&current.Current()&&
                        current.After?.Current()==true&&current.InventoryCurrent()&&current.After.Digest==digest&&
                        (string)current.After.Request.Element("nativeOutputOriginal")?.Element("nativeOutputPrefix")?.Attribute("operation")==localOperation.ToString("N")&&
                        (string)current.After.Request.Element("nativeOutputOriginal")?.Element("nativeOutputPrefix")?.Attribute("original")==originalHash&&
                        (int?)current.After.Request.Element("nativeOutputOriginal")?.Element("nativeOutputPrefix")?.Attribute("count")==count&&
                        RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(action.Sender.latestPlayerData,current.After.Frame);
                    string[] paths={root+".before.intent",root+".before",root+".before.native",root+".inventory-before",
                        root+".after.intent",root+".after",root+".after.native",root+".inventory",action.Path+".ready."+generation};
                    long bytes=0;foreach(string path in paths)
                    {long length=RebirthWorkstationCompletionPublication.PublishedLength(path);if(length<1)return false;bytes=checked(bytes+length);}
                    if(sources.Count>=12||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*bytes+65536),out memory)||
                        (retention=action.Retain())==null)return false;
                    var before=ReadCachedRestartProof(action,root+".before.intent","cachedViewer");
                    var after=ReadCachedRestartProof(action,root+".after.intent","cachedViewer");
                    var active=after?.Element("activeBefore")?.Elements().SingleOrDefault();
                    var delivery=after?.Element("physicalDelivery")?.Elements().SingleOrDefault();
                    var prefix=after?.Element("nativeOutputOriginal")?.Element("nativeOutputPrefix");
                    if(before==null||after==null||active==null||delivery?.Name!="nativeOwningDelivery"||RequestDigest(after)!=digest||
                        (string)after.Attribute("stage")!="after"||(string)after.Attribute("action")!=action.Action.ToString("N")||
                        (string)after.Attribute("source")!=action.SourceHash||(string)after.Attribute("generation")!=generation||
                        after.Element("sameOwnerEffect")!=null||after.Element("deferredNativeContribution")!=null||
                        (string)prefix?.Attribute("operation")!=localOperation.ToString("N")||(string)prefix.Attribute("original")!=originalHash||
                        (int?)prefix.Attribute("count")!=count||(string)before.Attribute("stage")!="before"||
                        (string)before.Attribute("action")!=action.Action.ToString("N")||(string)before.Attribute("source")!=action.SourceHash||
                        (string)before.Attribute("generation")!=generation||!XNode.DeepEquals(before.Element("activeBefore")?.Elements().SingleOrDefault(),active)||
                        !ValidateCachedActiveBefore(action,active)||
                        !RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(action.Sender.latestPlayerData,after.Element("nativeOwnerFrame")))return false;
                    foreach(string field in new[]{"bagLocks","beltLocks","beltCount","beltPreferred","equipmentPreferred"})
                        if((string)before.Attribute(field)!=(string)after.Attribute(field))return false;
                    source=new ViewerSource{Cached=action,CachedNativeHead=new WeakReference<byte[]>(action.Issue.Cache.Native),Generation=generation,
                        Active=new XElement(active),Memory=memory,Retention=retention,
                        BagLocks=(string)before.Attribute("bagLocks"),BeltLocks=(string)before.Attribute("beltLocks"),BeltCount=(int)before.Attribute("beltCount"),
                        BeltPreferred=(string)before.Attribute("beltPreferred"),EquipmentPreferred=(string)before.Attribute("equipmentPreferred")};memory=retention=null;
                    if(!source.Current()||!ValidLocks(source.BagLocks)||!ValidLocks(source.BeltLocks)||source.BeltCount<1||source.BeltCount>255||!ValidOwnerPreferences(source))return false;
                    source.Before=RestoreRemoteOutputStage(source,"before",before);
                    source.After=RestoreRemoteOutputStage(source,"after",after);
                    if(source.Before==null||source.After==null||source.Before.NativePath!=root+".before.native"||source.After.NativePath!=root+".after.native")return false;
                    source.BeforeInventoryPath=root+".inventory-before";source.InventoryPath=root+".inventory";
                    source.BeforeInventoryReceipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.BeforeInventoryPath);
                    source.InventoryReceipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.InventoryPath);source.InventoryAccepted=source.InventoryReceipt!=null;
                    var first=ReadCachedRestartProof(action,source.BeforeInventoryPath,"nativeViewerInventoryBeforeAccepted");
                    var last=ReadCachedRestartProof(action,source.InventoryPath,"nativeViewerInventoryAccepted");
                    if(!source.BeforeInventoryCurrent()||!source.InventoryCurrent()||(string)first?.Attribute("generation")!=generation||
                        (string)last?.Attribute("generation")!=generation||!XNode.DeepEquals(first.Element("nativeOwnerFrame"),source.Before.Frame)||
                        !XNode.DeepEquals(last.Element("nativeOwnerFrame"),source.Before.Frame)||
                        !XNode.DeepEquals(last.Element("after")?.Element("nativeOwnerFrame"),source.After.Frame)||!action.RestoreSavedBefore(source.Before))return false;
                    source.Delivery=new XElement(delivery);source.RestoredCachedInputs=new Dictionary<string,byte[]>();
                    foreach(string path in paths.Where(path=>!path.EndsWith(".native",StringComparison.Ordinal)))
                    {var image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(image==null)return false;source.RestoredCachedInputs.Add(path,image);}
                    var primary=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"primaryNative");var extras=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"extrasNative");
                    bool inventory=(string)delivery.Attribute("sink")=="Inventory";
                    if(!inventory&&(string)delivery.Attribute("sink")!="Output"||primary.Length!=1||primary[0]?.itemValue==null||
                        primary[0].count!=action.Recipe.count||primary[0].itemValue.type!=action.Recipe.itemValueType||count!=extras.Length+1||
                        delivery.Element("primaryNative")?.Value!=action.Issue.Cache.CurrentFull.Element("primaryNative")?.Value||
                        !RebirthProductiveEarnedUiRemote.ValidateExtraOutputs(action.Recipe,delivery,extras))return false;
                    // The full existing owner validator resolves this exact
                    // source by key. Installation remains transaction-local.
                    sources.Add(key,source);installed=true;
                    if(!ValidateCachedPhysicalActor(action,active,delivery,primary[0],extras,inventory)||!source.Current()||
                        !source.Before.Current()||!source.After.Current()||!source.BeforeInventoryCurrent()||!source.InventoryCurrent())return false;
                    source=null;return true;
                }
            }
            catch{return false;}
            finally
            {
                if(source!=null&&installed)lock(gate)
                    if(sources.TryGetValue(key,out var own)&&ReferenceEquals(own,source))sources.Remove(key);
                source?.Dispose();retention?.Dispose();memory?.Dispose();
            }
        }
    }
}
