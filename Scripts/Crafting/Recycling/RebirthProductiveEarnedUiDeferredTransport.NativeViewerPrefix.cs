using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal static partial class NativeViewerPds
    {
        internal sealed partial class ViewerSource
        {
            internal SavedViewerFile Prefix;internal int PrefixCount;
            internal readonly List<PublishedViewerPrefix> PublishedPrefixes=new List<PublishedViewerPrefix>();
        }
        internal sealed partial class SavedViewerFile
        {
            internal XElement PrefixBefore;internal string PrefixInventoryPath;internal byte[] PrefixInventoryReceipt;
            internal bool PrefixInventoryCurrent()=>PrefixInventoryReceipt!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(PrefixInventoryPath,PrefixInventoryReceipt);
        }
        internal sealed class PublishedViewerPrefix
        {internal int Count,NativeLength;internal string Digest,NativeDigest,ReceiptPath,ReceiptHash,InventoryPath,InventoryHash,NativePath,NativeHash;}
        private static IEnumerable<SavedViewerFile> ViewerStages(ViewerSource source)=>new[]{source.Before,source.After,source.CachedEffect,source.Prefix};
        private static bool IsRetiredOutputPrefixUpload(NetPackagePlayerData packet,int length,string digest)
            =>sources.Values.Any(source=>!source.Closed&&ReferenceEquals(source.SourceSender,packet.Sender)&&
                source.SourceViewer.entityId==packet.playerDataFile.id&&source.SourceViewerDeaths==packet.playerDataFile.deaths&&
                source.PublishedPrefixes.Any(prefix=>prefix.NativeLength==length&&prefix.NativeDigest==digest));
        private static string PrefixFileHash(string path)
        {
            RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
            if(File.Exists(path+".tmp"))throw new InvalidDataException();
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var hash=SHA256.Create())
            {if(stream.Length<1||stream.Length>RebirthWorkstationCompletionPublication.MaximumBytes)throw new InvalidDataException();return Convert.ToBase64String(hash.ComputeHash(stream));}
        }
        private static string PrefixImageHash(XElement image)
        {
            using(var memory=new MemoryStream())
            {
                using(var writer=XmlWriter.Create(memory,new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false}))
                {image.Save(writer);writer.Flush();}
                using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(memory.ToArray()));
            }
        }
        private static bool ValidPrefixHash(string value)
        {try{var bytes=Convert.FromBase64String(value);return bytes.Length==32&&Convert.ToBase64String(bytes)==value;}catch{return false;}}
        private static bool ValidateViewerOutputPrefix(ViewerSource source,XElement request,int count)
        {
            var recipe=source.Original?.Recipe??source.Cached?.Recipe;
            if(recipe==null||source.Before?.Current()!=true||!source.BeforeInventoryCurrent()||
                source.Original!=null&&(!initialBefore.TryGetValue(source.Original,out var initial)||!initial.Current())||
                !XNode.DeepEquals(request.Element("activeBefore")?.Elements().SingleOrDefault(),source.Active)||
                request.Element("nativeOutputPrefix") is not XElement prefix||prefix.Name!="nativeOutputPrefix"||(int?)prefix.Attribute("version")!=1||
                (int?)prefix.Attribute("count")!=count||!Guid.TryParseExact((string)prefix.Attribute("operation"),"N",out var local)||local==Guid.Empty||
                !ValidPrefixHash((string)prefix.Attribute("original"))||!ValidPrefixHash((string)prefix.Attribute("accepted"))||
                request.Element("nativeOwnerFrame") is not XElement owner)return false;
            var effect=request.Element("sameOwnerEffect")?.Elements().SingleOrDefault();
            var counterBefore=source.Before.Frame;
            if(source.Original!=null)
            {
                if(effect!=null)
                {
                    if(request.Element("deferredNativeContribution")!=null||!ValidateDirectEffect(source,effect,prefix))return false;
                    counterBefore=effect.Element("ownerAfterGiveExp")?.Elements().SingleOrDefault();
                }
                else if(!HasActualDeferredOutputPrefix(source.Original,request.Element("deferredNativeContribution"),prefix))return false;
            }
            else if(source.Cached?.Current()!=true||!source.Cached.SavedOwnerBeforeAction||effect!=null||request.Element("deferredNativeContribution")!=null||
                prefix.Element("primaryNative")?.Value!=source.Cached.Issue.Cache.CurrentFull?.Element("primaryNative")?.Value)return false;
            if(!ProjectionCounters(counterBefore,recipe.GetName(),out int total,out float crafted)||
                !ProjectionCounters(owner,recipe.GetName(),out int nextTotal,out float nextCrafted)||total!=nextTotal||crafted!=nextCrafted)return false;
            var primary=RebirthProductiveEarnedUiRemote.ReadGrid(prefix,"primaryNative");var extras=RebirthProductiveEarnedUiRemote.ReadGrid(prefix,"extrasNative");
            if(primary.Length!=1||primary[0]?.itemValue?.ItemClass==null||primary[0].count!=recipe.count||
                primary[0].itemValue.type!=recipe.itemValueType||extras.Length>64||count<1||count>extras.Length+1||
                primary[0].itemValue.Quality!=(primary[0].itemValue.ItemClass.HasQuality?(int)source.Active.Attribute("quality"):0)||
                !RebirthProductiveEarnedUiRemote.ValidateExtraOutputs(recipe,prefix,extras))return false;
            var accepted=prefix.Element("acceptedInsertion")?.Elements().SingleOrDefault();
            if(accepted?.Name!="recipeOutputAccepted"||(string)accepted.Attribute("operation")!=local.ToString("N")||(int?)accepted.Attribute("index")!=count-1||
                (string)accepted.Attribute("original")!=(string)prefix.Attribute("original")||
                PrefixImageHash(accepted)!=(string)prefix.Attribute("accepted")||
                accepted.Element("bag")?.Value!=prefix.Element("bagAfter")?.Value||accepted.Element("belt")?.Value!=prefix.Element("beltAfter")?.Value||
                accepted.Element("equipment")?.Value!=prefix.Element("equipmentAfter")?.Value||
                owner.Element("bag")?.Value!=prefix.Element("bagAfter")?.Value||owner.Element("belt")?.Value!=prefix.Element("beltAfter")?.Value||
                owner.Element("equipment")?.Value!=prefix.Element("equipmentAfter")?.Value)return false;
            // The saved active generation owns the insertion plan. The first
            // immutable generation independently owns the already-entered XP
            // effect and can precede real cached-boundary inventory changes.
            var planBefore=source.Before.Frame;
            var plan=new XElement("nativeOutputPrefixPlan",new XElement("bagBefore",planBefore.Element("bag").Value),
                new XElement("beltBefore",planBefore.Element("belt").Value),new XElement("equipmentBefore",planBefore.Element("equipment").Value),
                new XElement("bagAfter",owner.Element("bag").Value),new XElement("beltAfter",owner.Element("belt").Value),new XElement("equipmentAfter",owner.Element("equipment").Value));
            if(!ValidateOwnerInsertionPlan(source,plan,primary[0],extras.Take(count-1).ToArray()))return false;
            if(source.Prefix!=null)
            {
                var prior=source.Prefix.Request.Element("nativeOutputPrefix");
                if((string)prior.Attribute("operation")!=(string)prefix.Attribute("operation")||(string)prior.Attribute("original")!=(string)prefix.Attribute("original")||
                    prior.Element("primaryNative")?.Value!=prefix.Element("primaryNative")?.Value||prior.Element("extrasNative")?.Value!=prefix.Element("extrasNative")?.Value)return false;
            }
            return source.DirectEffect==null||XNode.DeepEquals(source.DirectEffect,effect);
        }
        internal static bool ReceiveOutputPrefix(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,XElement request,out string stage,out string digest)
            =>ReceiveOutputPrefixCore(original,null,request,out stage,out digest);
        private static bool ReceiveOutputPrefixCore(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,
            RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction cached,XElement request,out string stage,out string digest)
        {
            stage=digest=null;
            try
            {
                string kind=(string)request.Attribute("stage"),generation=(string)request.Attribute("generation");bool query=kind?.EndsWith("Query",StringComparison.Ordinal)==true;
                string number=kind?.Substring("prefix-".Length);if(query)number=number.Substring(0,number.Length-5);
                if(!int.TryParse(number,out int count)||count<1||count>65||kind!="prefix-"+count+(query?"Query":"")||
                    (original!=null?!original.CurrentViewer():cached?.Current()!=true)||(int?)request.Attribute("version")!=1||
                    (string)request.Attribute(original==null?"action":"nonce")!=(original?.Nonce??cached.Action).ToString("N")||
                    (string)request.Attribute("source")!=(original?.SourceHash??cached.SourceHash)||generation?.Length!=64)return false;
                lock(gate)
                {
                    if(!sources.TryGetValue(original==null?CachedKey(cached,generation):Key(original,generation),out var source)||
                        !ReferenceEquals(source.Original,original)||!ReferenceEquals(source.Cached,cached)||!source.Current()||
                        source.Before?.Current()!=true||!source.BeforeInventoryCurrent()||source.After!=null||source.CachedEffect!=null)return false;
                    var old=source.PublishedPrefixes.FirstOrDefault(value=>value.Count==count);
                    if(old!=null)
                    {
                        if(!query||(string)request.Attribute("digest")!=old.Digest||(string)request.Attribute("pdsDigest")!=old.NativeDigest||(int?)request.Attribute("pdsLength")!=old.NativeLength||
                            PrefixFileHash(old.ReceiptPath)!=old.ReceiptHash||PrefixFileHash(old.InventoryPath)!=old.InventoryHash||PrefixFileHash(old.NativePath)!=old.NativeHash)return false;
                        digest=old.Digest;stage="nativeViewerPrefix"+count+"Saved";return true;
                    }
                    if(source.PrefixCount>count)return false;
                    var token=source.PrefixCount==count?source.Prefix:null;
                    if(!query)
                    {
                        if((string)request.Attribute("bagLocks")!=source.BagLocks||(string)request.Attribute("beltLocks")!=source.BeltLocks||(int?)request.Attribute("beltCount")!=source.BeltCount||
                            (string)request.Attribute("beltPreferred")!=source.BeltPreferred||(string)request.Attribute("equipmentPreferred")!=source.EquipmentPreferred||
                            !ValidateViewerOutputPrefix(source,request,count))return false;
                        string native=(string)request.Attribute("pdsDigest");int length=(int?)request.Attribute("pdsLength")??0;
                        if(native?.Length!=64||native.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||length<1||length>16*1024*1024)return false;
                        if(token!=null&&!XNode.DeepEquals(token.Request,request))return false;
                        if(token==null)
                        {
                            var predecessor=source.Prefix??source.Before;
                            if(predecessor?.Current()!=true||source.Prefix!=null&&!source.Prefix.PrefixInventoryCurrent()||source.Workers!=0||
                                predecessor.AcceptedHead==null||!predecessor.AcceptedHead.TryGetTarget(out var head)||!ReferenceEquals(source.SourceSender.latestPlayerData,head))return false;
                            long measured=checked(MeasurePublicationElement(request)+MeasurePublicationElement(source.Before.Request)+MeasurePublicationElement(predecessor.Frame));
                            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,checked(16*measured+96L*1024*1024)))return false;
                            token=SavedViewerFile.Issue(source,true,request);token.PrefixBefore=new XElement(predecessor.Frame);token.PreviousHead=predecessor.AcceptedHead;token.PreviousHeadNull=false;
                            var intent=new XElement("workstationCompletionIntent",new XAttribute("operation",source.SourceOperation.ToString("N")),new XAttribute("world",source.SourceWorldGuid),new XElement("before",new XElement(request)));
                            if(!RebirthWorkstationCompletionPublication.Write(source.SourcePath+".viewer."+generation+".prefix-"+count+".intent",intent))return false;
                            if(source.Prefix!=null)
                            {
                                var prior=source.Prefix;
                                source.PublishedPrefixes.Add(new PublishedViewerPrefix{Count=source.PrefixCount,Digest=prior.Digest,NativeDigest=prior.ExpectedNative,NativeLength=prior.ExpectedLength,
                                    ReceiptPath=prior.ReceiptPath,ReceiptHash=PrefixFileHash(prior.ReceiptPath),InventoryPath=prior.PrefixInventoryPath,InventoryHash=PrefixFileHash(prior.PrefixInventoryPath),
                                    NativePath=prior.NativePath,NativeHash=PrefixFileHash(prior.NativePath)});
                                prior.Clear();
                            }
                            source.Prefix=token;source.PrefixCount=count;
                            if(original!=null&&request.Element("sameOwnerEffect") is XElement direct)source.DirectEffect=new XElement(direct.Elements().Single());
                        }
                    }
                    if(token==null||query&&((string)request.Attribute("digest")!=token.Digest||(string)request.Attribute("pdsDigest")!=token.ExpectedNative||(int?)request.Attribute("pdsLength")!=token.ExpectedLength))return false;
                    if(token.Accepted&&token.Published&&token.Receipt==null&&!Publish(token))return false;
                    digest=token.Digest;stage="nativeViewerPrefix"+count+(token.Current()&&token.PrefixInventoryCurrent()?"Saved":"Armed");return true;
                }
            }
            catch{return false;}
        }
        private static bool ObserveOutputPrefixInventory(ViewerSource source,RebirthWorkstationCraftCompletion.Snapshot snapshot,string actualEquipment)
        {
            var token=source.Prefix;if(token==null||source.After!=null||source.CachedEffect!=null)return false;
            var before=token.PrefixBefore;var after=token.Frame;
            if(before==null||snapshot.NativeBag!=before.Element("bag")?.Value||snapshot.NativeBelt!=before.Element("belt")?.Value||
                snapshot.NativeEquipment!=before.Element("equipment")?.Value||
                RebirthWorkstationCompletionPipeline.Grid(snapshot.Player.bag.ItemGrid.items)!=after.Element("bag")?.Value||
                RebirthWorkstationCompletionPipeline.Grid(snapshot.Player.inventory.ItemGrid.items)!=after.Element("belt")?.Value||actualEquipment!=after.Element("equipment")?.Value)return true;
            var proof=new XElement("workstationCompletionIntent",new XAttribute("operation",source.SourceOperation.ToString("N")),new XAttribute("world",source.SourceWorldGuid),
                new XElement("before",new XElement("nativeViewerPrefixInventoryAccepted",new XAttribute("generation",source.Generation),new XAttribute("count",source.PrefixCount),
                    new XAttribute("request",token.Digest),new XElement("predecessor",new XElement(before)),new XElement("accepted",new XElement(after)))));
            string path=source.SourcePath+".viewer."+source.Generation+".prefix-"+source.PrefixCount+".inventory";
            if(RebirthWorkstationCompletionPublication.Write(path,proof))
            {token.PrefixInventoryPath=path;token.PrefixInventoryReceipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);}
            return true;
        }
        private static void DisposeOutputPrefixes(ViewerSource source)
        {source.Prefix?.Clear();source.Prefix=null;source.PublishedPrefixes.Clear();}
    }
}
