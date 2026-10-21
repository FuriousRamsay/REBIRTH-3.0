using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        internal static void DiscardRestoredCachedAction(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action)
        {
            lock(gate)foreach(var pair in sources.Where(pair=>ReferenceEquals(pair.Value.Cached,action)&&
                pair.Value.RestoredCachedInputs!=null&&pair.Value.Workers==0).ToArray())
            {sources.Remove(pair.Key);pair.Value.Dispose();}
        }
        private static XElement ReadCachedRestartProof(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,string path,string kind)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var before=record?.Element("before");
            return record?.Name=="workstationCompletionIntent"&&(string)record.Attribute("operation")==action.Operation.ToString("N")&&
                (string)record.Attribute("world")==action.WorldGuid&&before?.Elements().Count()==1?before.Element(kind):null;
        }
        internal sealed partial class ViewerSource
        {
            internal Dictionary<string,byte[]> RestoredCachedInputs;
            private bool RestoredCachedInputsCurrent()=>RestoredCachedInputs==null||RestoredCachedInputs.All(pair=>
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(pair.Key,pair.Value));
        }
        internal static bool RestoreRetainedCachedPrefix(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,
            string generation,int count,string digest,Guid localOperation,string originalHash)
        {
            ViewerSource source=null;IDisposable memory=null,retention=null;
            try
            {
                if(action?.Current()!=true||action.Issue.RetainedAction!=action.Action||!RebirthWorkstationCompletionOriginal.ValidKey(generation)||
                    count<1||count>65||localOperation==Guid.Empty)return false;
                string key=CachedKey(action,generation),root=action.Path+".viewer."+generation;
                lock(gate)
                {
                    if(sources.TryGetValue(key,out var current))return ReferenceEquals(current.Cached,action)&&current.Current()&&
                        current.PrefixCount==count&&current.Prefix?.Current()==true&&current.Prefix.PrefixInventoryCurrent()&&
                        current.Prefix.Digest==digest&&
                        (string)current.Prefix.Request.Element("nativeOutputPrefix")?.Attribute("operation")==localOperation.ToString("N")&&
                        (string)current.Prefix.Request.Element("nativeOutputPrefix")?.Attribute("original")==originalHash&&
                        RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(action.Sender.latestPlayerData,current.Prefix.Frame);
                    if(sources.Count>=12||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out memory)||
                        (retention=action.Retain())==null)return false;
                    var request=ReadCachedRestartProof(action,root+".prefix-"+count+".intent","cachedViewer");
                    var before=ReadCachedRestartProof(action,root+".before.intent","cachedViewer");
                    var active=request?.Element("activeBefore")?.Elements().SingleOrDefault();var prefix=request?.Element("nativeOutputPrefix");
                    if(request==null||before==null||active==null||RequestDigest(request)!=digest||
                        (string)request.Attribute("action")!=action.Action.ToString("N")||(string)request.Attribute("source")!=action.SourceHash||
                        (string)request.Attribute("generation")!=generation||(string)request.Attribute("stage")!="prefix-"+count||
                        (string)prefix?.Attribute("operation")!=localOperation.ToString("N")||(string)prefix.Attribute("original")!=originalHash||
                        (int?)prefix.Attribute("count")!=count||(string)before.Attribute("stage")!="before"||
                        (string)before.Attribute("action")!=action.Action.ToString("N")||(string)before.Attribute("source")!=action.SourceHash||
                        (string)before.Attribute("generation")!=generation||!XNode.DeepEquals(before.Element("activeBefore")?.Elements().SingleOrDefault(),active)||
                        !ValidateCachedActiveBefore(action,active)||
                        !RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(action.Sender.latestPlayerData,request.Element("nativeOwnerFrame")))return false;
                    source=new ViewerSource{Cached=action,CachedNativeHead=new WeakReference<byte[]>(action.Issue.Cache.Native),Generation=generation,
                        Active=new XElement(active),Memory=memory,Retention=retention,
                        BagLocks=(string)before.Attribute("bagLocks"),BeltLocks=(string)before.Attribute("beltLocks"),BeltCount=(int)before.Attribute("beltCount"),
                        BeltPreferred=(string)before.Attribute("beltPreferred"),EquipmentPreferred=(string)before.Attribute("equipmentPreferred")};memory=retention=null;
                    if(!source.Current()||!ValidLocks(source.BagLocks)||!ValidLocks(source.BeltLocks)||source.BeltCount<1||source.BeltCount>255||!ValidOwnerPreferences(source))return false;
                    source.Before=RestoreRemoteOutputStage(source,"before",before);if(source.Before==null||source.Before.NativePath!=root+".before.native")return false;
                    source.BeforeInventoryPath=root+".inventory-before";
                    source.BeforeInventoryReceipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.BeforeInventoryPath);
                    var beforeProof=ReadCachedRestartProof(action,source.BeforeInventoryPath,"nativeViewerInventoryBeforeAccepted");
                    if(!source.BeforeInventoryCurrent()||(string)beforeProof?.Attribute("generation")!=generation||
                        !XNode.DeepEquals(beforeProof.Element("nativeOwnerFrame"),source.Before.Frame)||!action.RestoreSavedBefore(source.Before)||
                        !ValidateViewerOutputPrefix(source,request,count))return false;
                    source.Prefix=RestoreRemoteOutputStage(source,"prefix-"+count,request);
                    if(source.Prefix==null||source.Prefix.NativePath!=root+".prefix-"+count+".native")return false;
                    source.PrefixCount=count;source.Prefix.PrefixInventoryPath=root+".prefix-"+count+".inventory";
                    source.Prefix.PrefixInventoryReceipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.Prefix.PrefixInventoryPath);
                    var inventory=ReadCachedRestartProof(action,source.Prefix.PrefixInventoryPath,"nativeViewerPrefixInventoryAccepted");
                    if(!source.Prefix.PrefixInventoryCurrent()||(string)inventory?.Attribute("generation")!=generation||
                        (int?)inventory.Attribute("count")!=count||(string)inventory.Attribute("request")!=digest||
                        inventory.Element("predecessor")?.Element("nativeOwnerFrame")==null||
                        !XNode.DeepEquals(inventory.Element("accepted")?.Element("nativeOwnerFrame"),source.Prefix.Frame))return false;
                    source.Prefix.PrefixBefore=new XElement(inventory.Element("predecessor").Element("nativeOwnerFrame"));
                    source.RestoredCachedInputs=new Dictionary<string,byte[]>();
                    foreach(string path in new[]{root+".before.intent",root+".prefix-"+count+".intent",source.BeforeInventoryPath,source.Prefix.PrefixInventoryPath})
                    {
                        var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null)return false;
                        source.RestoredCachedInputs.Add(path,bytes);
                    }
                    if(!source.Current()||!source.Before.Current()||!source.Prefix.Current()||!source.Prefix.PrefixInventoryCurrent()||
                        !RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(action.Sender.latestPlayerData,source.Prefix.Frame))return false;
                    sources.Add(key,source);source=null;return true;
                }
            }
            catch{return false;}
            finally{source?.Dispose();retention?.Dispose();memory?.Dispose();}
        }
    }
}
