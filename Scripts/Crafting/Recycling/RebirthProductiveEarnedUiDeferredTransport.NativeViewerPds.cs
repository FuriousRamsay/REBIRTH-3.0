using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Separate native-viewer purpose. No RecipientScope/UIO original is made.
    internal static partial class NativeViewerPds
    {
        internal static string EncodePreferences(int[] values)
        {
            if(values==null||values.Length>20)throw new InvalidDataException();
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {foreach(int value in values){if(value<0)throw new InvalidDataException();writer.Write(value);}return Convert.ToBase64String(stream.ToArray());}
        }
        private static int[] ReadPreferences(string image,int count)
        {
            if(image==null||image.Length>112||count<0||count>20)throw new InvalidDataException();
            byte[] bytes=Convert.FromBase64String(image);if(bytes.Length!=4*count||Convert.ToBase64String(bytes)!=image)throw new InvalidDataException();
            var result=new int[count];using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream))
                for(int i=0;i<count;i++){result[i]=reader.ReadInt32();if(result[i]<0)throw new InvalidDataException();}
            return result;
        }
        private static bool ValidateOwnerInsertionPlan(ViewerSource source,XElement delivery,ItemStack primary,ItemStack[] extras)
        {
            try
            {
            var oldBag=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"bagBefore");
            var oldBelt=RebirthProductiveEarnedUiRemote.ReadGrid(delivery,"beltBefore");
            var items=new[]{primary}.Concat(extras).ToList();
            if(delivery.Element("equipmentBefore")==null&&delivery.Element("equipmentAfter")==null)
                return RebirthRecyclerOwnerDelivery.Plan(oldBag,oldBelt,Locks(source.BagLocks),Locks(source.BeltLocks),source.BeltCount,items,out var legacyBag,out var legacyBelt)&&
                    RebirthWorkstationCompletionPipeline.Grid(legacyBag)==(string)delivery.Element("bagAfter")&&
                    RebirthWorkstationCompletionPipeline.Grid(legacyBelt)==(string)delivery.Element("beltAfter");
            var oldEquipment=RebirthWorkstationCompletionWitnessValidator.EquipmentCells(delivery.Element("equipmentBefore"));
            var newEquipment=RebirthWorkstationCompletionWitnessValidator.EquipmentCells(delivery.Element("equipmentAfter"));
            if(oldEquipment.Any(value=>!value.IsEmpty()&&value.count!=1))return false;
            if(!RebirthRecipeOutputInventoryPrediction.TryPlan(oldBag,oldBelt,source.BeltCount,ReadPreferences(source.BeltPreferred,oldBelt.Length),
                oldEquipment.Select(value=>value.itemValue).ToArray(),ReadPreferences(source.EquipmentPreferred,oldEquipment.Length),items,
                out var bag,out var belt,out var equipment)||
                RebirthWorkstationCompletionPipeline.Grid(bag)!=(string)delivery.Element("bagAfter")||
                RebirthWorkstationCompletionPipeline.Grid(belt)!=(string)delivery.Element("beltAfter")||equipment.Length!=newEquipment.Length)return false;
            for(int i=0;i<equipment.Length;i++)
            {
                if(equipment[i].IsEmpty()){if(!newEquipment[i].IsEmpty())return false;}
                else if(newEquipment[i].count!=1||RebirthNativeItemCodec.Encode(equipment[i])!=RebirthNativeItemCodec.Encode(newEquipment[i].itemValue))return false;
            }
            return true;
            }
            catch{return false;}
        }
        private static bool ValidOwnerPreferences(ViewerSource source)
        {
            try
            {
                if(source.Active.Element("equipmentBefore")==null&&source.BeltPreferred==null&&source.EquipmentPreferred==null)return true;
                int count=RebirthProductiveEarnedUiRemote.ReadGrid(source.Active,"beltBefore").Length;
                ReadPreferences(source.BeltPreferred,count);ReadPreferences(source.EquipmentPreferred,13);
                return source.BeltCount>=0&&source.BeltCount<=count;
            }
            catch{return false;}
        }
        private static bool EquipmentFrameMatches(XElement frame,XElement evidence,string field)
            =>evidence?.Element(field)==null||(int?)frame?.Attribute("version")==2&&
                (string)frame.Element("equipment")== (string)evidence.Element(field);
        internal sealed partial class ViewerSource:IDisposable
        {
            internal RebirthWorkstationCompletionPipeline.NativeHostOriginal Original;
            internal RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction Cached;
            internal WeakReference<byte[]> CachedNativeHead;
            private bool CachedGenerationCurrent()=>Cached?.Current()==true&&(Cached.BeforeGeneration!=null?Cached.BeforeGeneration==Generation:
                CachedNativeHead!=null&&CachedNativeHead.TryGetTarget(out var head)&&ReferenceEquals(head,Cached.Issue.Cache.Native));
            internal EntityPlayer SourceViewer=>Original?.Viewer??Cached?.Viewer;
            internal ClientInfo SourceSender=>Original?.Sender??Cached?.Sender;
            internal string SourceViewerKey=>Original?.ViewerKey??Cached?.ViewerKey;
            internal string SourceViewerCreation=>Original?.ViewerCreation??Cached?.ViewerCreation;
            internal int SourceViewerDeaths=>Original?.ViewerDeaths??Cached?.ViewerDeaths??-1;
            internal Guid SourceOperation=>Original?.Operation??Cached.Operation;
            internal Guid SourceNonce=>Original?.Nonce??Cached.Nonce;
            internal string SourcePath=>Original?.Path??Cached?.Path;
            internal string SourceHash=>Original?.SourceHash??Cached?.SourceHash;
            internal string SourceWorldGuid=>Original?.WorldGuid??Cached?.WorldGuid;
            internal World SourceWorld=>Original?.World??Cached?.World;
            internal bool SourceClosed=>Original!=null?Original.Closed:Cached?.Issue?.Cache?.Active!=true;
            internal XElement Active,Delivery,DirectEffect;internal string Generation,BagLocks,BeltLocks,BeltPreferred,EquipmentPreferred;internal int BeltCount;
            internal SavedViewerFile Before,After,CachedEffect;internal IDisposable Memory,Retention;internal bool InventoryAccepted,Closed;internal int Workers;
            internal byte[] EffectInventoryReceipt;internal string EffectInventoryPath;
            internal byte[] InventoryReceipt;internal string InventoryPath;
            internal byte[] BeforeInventoryReceipt;internal string BeforeInventoryPath;
            internal bool Current()=>!Closed&&(Original!=null?Original.CurrentViewer()&&Original.IsSavedActiveBefore(Active):CachedGenerationCurrent())&&
                RebirthWorkstationCompletionOriginal.Hash(Active.ToString(SaveOptions.DisableFormatting))==Generation&&RestoredCachedInputsCurrent();
            internal bool SavedCustodyCurrent()=>!Closed&&(Original!=null?Original.SourceCurrent()&&Original.IsSavedActiveBefore(Active):CachedGenerationCurrent())&&
                RebirthWorkstationCompletionOriginal.Hash(Active.ToString(SaveOptions.DisableFormatting))==Generation&&RestoredCachedInputsCurrent();
            internal bool InventoryCurrent()=>InventoryAccepted&&InventoryReceipt!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(InventoryPath,InventoryReceipt);
            internal bool BeforeInventoryCurrent()=>BeforeInventoryReceipt!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(BeforeInventoryPath,BeforeInventoryReceipt);
            internal bool EffectInventoryCurrent()=>EffectInventoryReceipt!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(EffectInventoryPath,EffectInventoryReceipt);
            public void Dispose(){Closed=true;if(Workers!=0)return;DisposeOutputPrefixes(this);Before?.Clear();After?.Clear();CachedEffect?.Clear();Active=Delivery=DirectEffect=null;RestoredCachedInputs=null;InventoryReceipt=BeforeInventoryReceipt=EffectInventoryReceipt=null;Memory?.Dispose();Retention?.Dispose();Memory=Retention=null;}
        }
        internal sealed partial class SavedViewerFile
        {
            internal ViewerSource Source;internal bool After,Accepted,Published;internal XElement Frame,Request;
            internal string Digest,Root,Name,Path,ReceiptPath,NativePath,Kind;internal byte[] Native,Receipt;
            internal string ExpectedNative;internal int ExpectedLength;
            internal string AcceptedWire;internal int AcceptedWireLength;
            internal int? AcceptedEntity;
            internal WeakReference<PlayerDataFile> PreviousHead,AcceptedHead;internal bool PreviousHeadNull;
            internal bool MatchesPreviousHead(PlayerDataFile actual)=>PreviousHeadNull?actual==null:
                PreviousHead!=null&&PreviousHead.TryGetTarget(out var previous)&&ReferenceEquals(previous,actual);
            private SavedViewerFile(){}
            internal bool Current()=>Source.Current()&&ExpectedNative!=null&&Accepted&&Published&&Native!=null&&AcceptedWireLength==ExpectedLength&&AcceptedWire==ExpectedNative&&Receipt!=null&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(ReceiptPath,Receipt)&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(NativePath,Native);
            internal bool SavedCustodyCurrent()=>Source?.SavedCustodyCurrent()==true&&ExpectedNative!=null&&Accepted&&Published&&Native!=null&&AcceptedWireLength==ExpectedLength&&AcceptedWire==ExpectedNative&&Receipt!=null&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(ReceiptPath,Receipt)&&
                RebirthWorkstationCompletionPublication.MatchesPublishedBytes(NativePath,Native);
            internal void Clear(){Native=Receipt=PrefixInventoryReceipt=null;Frame=Request=PrefixBefore=null;PrefixInventoryPath=null;Source=null;PreviousHead=AcceptedHead=null;}
            internal static SavedViewerFile Issue(ViewerSource source,bool after,XElement request)
                =>new SavedViewerFile{Source=source,After=after,Kind=(string)request.Attribute("stage"),Frame=new XElement(request.Element("nativeOwnerFrame")),Request=new XElement(request),
                    Digest=RequestDigest(request),ExpectedNative=(string)request.Attribute("pdsDigest"),ExpectedLength=(int)request.Attribute("pdsLength"),
                    PreviousHeadNull=source.SourceSender.latestPlayerData==null,PreviousHead=source.SourceSender.latestPlayerData==null?null:new WeakReference<PlayerDataFile>(source.SourceSender.latestPlayerData)};
        }
        private sealed class Attempt {internal PlayerDataFile Data;internal SavedViewerFile[] Tokens;}
        private sealed class Writing {internal SavedViewerFile[] Tokens;internal byte[] Image;internal string Path;}
        private static readonly object gate=new object();
        private static readonly Dictionary<string,ViewerSource> sources=new Dictionary<string,ViewerSource>(StringComparer.Ordinal);
        private static readonly Dictionary<RebirthWorkstationCompletionPipeline.NativeHostOriginal,SavedViewerFile> initialBefore=
            new Dictionary<RebirthWorkstationCompletionPipeline.NativeHostOriginal,SavedViewerFile>();
        private static readonly ConditionalWeakTable<PlayerDataFile,Attempt> accepted=new ConditionalWeakTable<PlayerDataFile,Attempt>();
        private static string Key(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,string generation)=>source.Operation.ToString("N")+":"+generation;
        private static string RequestDigest(XElement request)
        {var image=new XElement(request);image.Attribute("challenge")?.Remove();return RebirthWorkstationCompletionOriginal.Hash(image.ToString(SaveOptions.DisableFormatting));}
        internal static string NativeDigest(byte[] native)=>HydrationHash(native);
        private sealed class ViewerWireStream:MemoryStream
        {
            public override void Write(byte[] bytes,int offset,int count){if(count<0||Position>16L*1024*1024-count)throw new InvalidDataException();base.Write(bytes,offset,count);}
            public override void WriteByte(byte value){if(Position>=16L*1024*1024)throw new InvalidDataException();base.WriteByte(value);}
        }
        internal static byte[] WireImage(PlayerDataFile data)
        {using(var stream=new ViewerWireStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(stream);data.WriteNetwork(writer,StreamModeWrite.ToServer);writer.Flush();return stream.ToArray();}}
        internal static bool Receive(RebirthWorkstationCompletionPipeline.NativeHostOriginal source,XElement request,out string stage,out string digest)
        {
            stage=digest=null;ViewerSource created=null;string createdKey=null;try
            {
                if(source?.CurrentViewer()!=true||(int?)request.Attribute("version")!=1||(string)request.Attribute("nonce")!=source.Nonce.ToString("N")||
                    (string)request.Attribute("source")!=source.SourceHash)return false;
                string generation=(string)request.Attribute("generation"),kind=(string)request.Attribute("stage");
                if(kind?.StartsWith("prefix-",StringComparison.Ordinal)==true)return ReceiveOutputPrefix(source,request,out stage,out digest);
                if(generation?.Length!=64)return false;bool cached=kind=="effect"||kind=="effectQuery";bool after=cached||kind=="after"||kind=="afterQuery";bool query=kind=="beforeQuery"||kind=="afterQuery"||kind=="effectQuery";
                if(!after&&!query&&kind!="before")return false;string key=Key(source,generation);
                lock(gate)
                {
                    Sweep();
                    if(!sources.TryGetValue(key,out var original))
                    {
                        if(after||query||sources.Count>=12||sources.Values.Select(value=>value.SourceOperation).Distinct().Count()>=4&&!sources.Values.Any(value=>value.SourceOperation==source.Operation)||request.Element("activeBefore")?.Elements().SingleOrDefault() is not XElement active||
                            !source.IsSavedActiveBefore(active)||RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting))!=generation||
                            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*MeasurePublicationElement(request)+96L*1024*1024),out var memory))return false;
                        var hold=source.Retain();if(hold==null){memory.Dispose();return false;}
                        original=new ViewerSource{Original=source,Generation=generation,Active=new XElement(active),Memory=memory,Retention=hold,
                            BagLocks=(string)request.Attribute("bagLocks"),BeltLocks=(string)request.Attribute("beltLocks"),BeltCount=(int)request.Attribute("beltCount"),
                            BeltPreferred=(string)request.Attribute("beltPreferred"),EquipmentPreferred=(string)request.Attribute("equipmentPreferred")};
                        if(!ValidLocks(original.BagLocks)||!ValidLocks(original.BeltLocks)||original.BeltCount<1||original.BeltCount>255||!ValidOwnerPreferences(original)){original.Dispose();return false;}
                        sources.Add(key,original);created=original;createdKey=key;
                    }
                    if(!ReferenceEquals(original.Original,source)||!original.Current())return false;
                    var token=cached?original.CachedEffect:after?original.After:original.Before;
                    if(!query)
                    {
                        if(!XNode.DeepEquals(request.Element("activeBefore")?.Elements().SingleOrDefault(),original.Active)||
                            (string)request.Attribute("bagLocks")!=original.BagLocks||(string)request.Attribute("beltLocks")!=original.BeltLocks||(int?)request.Attribute("beltCount")!=original.BeltCount||
                            (string)request.Attribute("beltPreferred")!=original.BeltPreferred||(string)request.Attribute("equipmentPreferred")!=original.EquipmentPreferred||
                            request.Element("nativeOwnerFrame") is not XElement owner||owner.Elements("recipes").Single().Elements("recipe").Count()!=1||
                            (string)owner.Element("recipes").Element("recipe").Attribute("name")!=source.Recipe.GetName())return false;
                        string expectedNative=(string)request.Attribute("pdsDigest");int expectedLength=(int?)request.Attribute("pdsLength")??0;
                        if(expectedNative?.Length!=64||expectedNative.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||expectedLength<1||expectedLength>16*1024*1024)return false;
                        if(cached)
                        {
                            var full=request.Element("fullBoundary")?.Elements().SingleOrDefault();
                            var direct=request.Element("sameOwnerEffect")?.Elements().SingleOrDefault();
                            if(original.Before?.Current()!=true||!original.BeforeInventoryCurrent()||!ValidateCachedEffectHeader(original,direct,full,owner))return false;
                            if(original.DirectEffect!=null&&!XNode.DeepEquals(original.DirectEffect,direct))return false;original.DirectEffect=new XElement(direct);
                        }
                        else if(after)
                        {
                            if(original.Before?.Current()!=true||!original.BeforeInventoryCurrent()||request.Element("physicalDelivery")?.Elements().SingleOrDefault() is not XElement delivery||
                                (string)delivery.Attribute("operation")!=source.Operation.ToString("N")||
                                ((string)delivery.Attribute("sink")!="Inventory"&&((string)delivery.Attribute("sink")!="Output"||request.Element("sameOwnerEffect")==null))||
                                (string)owner.Element("bag")!=(string)delivery.Element("bagAfter")||(string)owner.Element("belt")!=(string)delivery.Element("beltAfter")||
                                !EquipmentFrameMatches(owner,delivery,"equipmentAfter"))return false;
                            var direct=request.Element("sameOwnerEffect")?.Elements().SingleOrDefault();
                            if(direct!=null&&!ValidateDirectEffect(original,direct,delivery))return false;
                            if(original.DirectEffect!=null&&!XNode.DeepEquals(original.DirectEffect,direct))return false;
                            if(original.Delivery!=null&&!XNode.DeepEquals(original.Delivery,delivery))return false;original.Delivery=new XElement(delivery);
                            if(direct!=null)original.DirectEffect=new XElement(direct);
                        }
                        else if((string)owner.Element("bag")!=(string)original.Active.Element("bagBefore")||(string)owner.Element("belt")!=(string)original.Active.Element("beltBefore")||
                            !EquipmentFrameMatches(owner,original.Active,"equipmentBefore"))return false;
                        if(token!=null&&!XNode.DeepEquals(token.Request,request))return false;
                        if(token==null)
                        {
                            long measured=checked(MeasurePublicationElement(request)+(original.Before==null?0:MeasurePublicationElement(original.Before.Request))+
                                (original.CachedEffect==null?0:MeasurePublicationElement(original.CachedEffect.Request)));
                            if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(original.Memory,checked(16*measured+96L*1024*1024)))return false;
                            var pending=SavedViewerFile.Issue(original,after,request);
                            if(after)
                            {
                                var predecessor=cached?original.Before:original.Prefix??original.CachedEffect??original.Before;
                                if(original.Prefix!=null&&!original.Prefix.PrefixInventoryCurrent())return false;
                                if(predecessor?.AcceptedHead==null||!predecessor.AcceptedHead.TryGetTarget(out var priorHead)||!ReferenceEquals(source.Sender.latestPlayerData,priorHead))return false;
                                pending.PreviousHead=predecessor.AcceptedHead;pending.PreviousHeadNull=false;
                            }
                            var intent=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),new XElement("before",new XElement(request)));
                            if(!RebirthWorkstationCompletionPublication.Write(source.Path+".viewer."+generation+"."+(cached?"effect":after?"after":"before")+".intent",intent))return false;
                            token=pending;if(cached)original.CachedEffect=token;else if(after)original.After=token;else original.Before=token;
                        }
                    }
                    if(token==null||query&&(string)request.Attribute("digest")!=token.Digest)return false;
                    if(query)
                    {
                        string native=(string)request.Attribute("pdsDigest");int length=(int?)request.Attribute("pdsLength")??0;
                        if(native?.Length!=64||native.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'))||length<1||length>16*1024*1024||
                            token.ExpectedNative!=null&&(token.ExpectedNative!=native||token.ExpectedLength!=length))return false;
                        var capture=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                            new XElement("before",new XElement("nativeViewerCaptured",new XAttribute("stage",token.Kind),new XAttribute("source",source.SourceHash),
                                new XAttribute("nonce",source.Nonce.ToString("N")),new XAttribute("generation",generation),new XAttribute("pdsDigest",native),new XAttribute("pdsLength",length))));
                        if(!RebirthWorkstationCompletionPublication.Write(source.Path+".viewer."+generation+"."+(cached?"effect":after?"after":"before")+".capture",capture))return false;
                        token.ExpectedNative=native;token.ExpectedLength=length;
                    }
                    if(token.Accepted&&token.Published&&token.Receipt==null&&!Publish(token))return false;
                    if(!after&&token.Current()&&original.BeforeInventoryCurrent()&&XNode.DeepEquals(source.Before,original.Active)&&!initialBefore.ContainsKey(source))
                    {if(host.ContainsKey(source.Operation))return false;initialBefore.Add(source,token);}
                    if((after||!XNode.DeepEquals(source.Before,original.Active))&&(!initialBefore.TryGetValue(source,out var first)||!first.Current()))return false;
                    if(!after&&token.Current())RetirePriorCachedBefore(original);
                    bool saved=token.Current()&&(cached?original.EffectInventoryCurrent():after?original.InventoryCurrent():original.BeforeInventoryCurrent());
                    digest=token.Digest;stage="nativeViewer"+(cached?"Effect":after?"After":"Before")+(saved?"Saved":"Armed");return true;
                }
            }
            catch{return false;}
            finally{if(created!=null&&created.Before==null){lock(gate){if(sources.TryGetValue(createdKey,out var current)&&ReferenceEquals(current,created))sources.Remove(createdKey);created.Dispose();}}}
        }
        private static bool ValidLocks(string value)=>value!=null&&value.Length<=255&&value.All(c=>c=='0'||c=='1');
        private static void RetirePriorCachedBefore(ViewerSource next)
        {
            var full=next.Active.Element("cachedPredecessor")?.Elements().SingleOrDefault();
            if(full==null||!next.Original.IsSavedFullBoundaryBefore(full)||!next.Original.IsSavedActiveBefore(next.Active))return;
            foreach(var pair in sources.Where(pair=>ReferenceEquals(pair.Value.Original,next.Original)&&!ReferenceEquals(pair.Value,next)&&pair.Value.After==null&&
                pair.Value.Before?.Current()==true&&initialBefore.TryGetValue(next.Original,out var first)&&!ReferenceEquals(first,pair.Value.Before)&&
                (string)pair.Value.Active.Element("nativeBefore")== (string)full.Element("nativeBefore")).ToArray())
            {sources.Remove(pair.Key);pair.Value.Dispose();} // immutable full native originals remain on disk
        }
        private static bool[] Locks(string value)=>value.Select(c=>c=='1').ToArray();
        private static bool Publish(SavedViewerFile token)
        {
            if(!token.AcceptedEntity.HasValue)return false;
            var source=token.Source;token.ReceiptPath=source.SourcePath+".viewer."+source.Generation+"."+token.Kind;
            token.NativePath=token.ReceiptPath+".native";
            if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(token.NativePath,token.Native)||
                !RebirthWorkstationCompletionPublication.MatchesPublishedBytes(token.NativePath,token.Native))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.SourceOperation.ToString("N")),new XAttribute("world",source.SourceWorldGuid),
                new XElement("before",new XElement("nativeViewerSaved",new XAttribute("version",1),new XAttribute("source",source.SourceHash),
                    new XAttribute("nonce",source.SourceNonce.ToString("N")),new XAttribute("generation",source.Generation),new XAttribute("digest",token.Digest),
                    new XAttribute("nativePath",token.Path),new XAttribute("retainedNative",token.NativePath),new XAttribute("nativeLength",token.Native.Length),
                    new XAttribute("nativeDigest",HydrationHash(token.Native)),new XAttribute("acceptedWire",token.AcceptedWire),
                    new XAttribute("acceptedWireLength",token.AcceptedWireLength),new XAttribute("viewerEntity",token.AcceptedEntity.Value),new XElement(token.Frame))));
            if(!RebirthWorkstationCompletionPublication.Write(token.ReceiptPath,record))return false;
            token.Receipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(token.ReceiptPath);return token.Receipt!=null;
        }
        internal static void AcceptedPrefix(ClientInfo peer,PlayerDataFile data,bool run,out object state)
        {
            state=null;if(!run||!ThreadManager.IsMainThread()||data==null)return;
            lock(gate)try
            {
                Sweep();var tokens=sources.Values.Where(value=>value.Current()&&ReferenceEquals(value.SourceSender,peer)&&value.SourceViewer.entityId==data.id&&value.SourceViewerDeaths==data.deaths)
                    .SelectMany(ViewerStages).Where(value=>value!=null&&!value.Published&&value.MatchesPreviousHead(peer.latestPlayerData)&&
                        RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,value.Frame)).ToArray();
                if(tokens.Length<1||tokens.Length>8||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||tokens.Any(value=>identity.StorageKey!=value.Source.SourceViewerKey))return;
                string root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()),name=identity.CanonicalId,path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,name+"."+PlayerDataFile.EXT));
                if(System.IO.Path.GetDirectoryName(path)!=root||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return;
                var wire=WireImage(data);string rawDigest=NativeDigest(wire);tokens=tokens.Where(token=>token.ExpectedLength==wire.Length&&token.ExpectedNative==rawDigest).ToArray();if(tokens.Length==0)return;
                var bytes=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);if(bytes.Length<1||bytes.Length>16*1024*1024)return;
                if(data.bDead||tokens.Any(token=>token.Native!=null&&!token.Native.SequenceEqual(bytes)))return;
                foreach(var token in tokens){token.Native=bytes;token.Root=root;token.Name=name;token.Path=path;token.AcceptedWire=rawDigest;token.AcceptedWireLength=wire.Length;token.AcceptedEntity=data.id;}
                var attempt=new Attempt{Data=data,Tokens=tokens};accepted.Remove(data);accepted.Add(data,attempt);state=attempt;
            }
            catch{}
        }
        internal static bool CanProcessUpload(NetPackagePlayerData packet,bool run)
        {
            if(!run||!ThreadManager.IsMainThread()||packet?.Sender==null||packet.playerDataFile==null)return true;
            if(IsRetiredCachedOwnerPeer(packet))return false;
            lock(gate)
            {
                // Classification comes from an already authenticated purpose
                // header, never from a current file or from absence of one.
                var candidates=sources.Values.Where(source=>!source.Closed&&ReferenceEquals(source.SourceSender,packet.Sender))
                    .SelectMany(ViewerStages).Where(token=>token!=null&&
                        token.Source.SourceViewer.entityId==packet.playerDataFile.id&&token.Source.SourceViewerDeaths==packet.playerDataFile.deaths).ToArray();
                if(candidates.Length==0)return true;
                try
                {
                    var bytes=WireImage(packet.playerDataFile);string digest=NativeDigest(bytes);
                    if(IsRetiredOutputPrefixUpload(packet,bytes.Length,digest))return false;
                    var matches=candidates.Where(token=>token.ExpectedLength==bytes.Length&&token.ExpectedNative==digest).ToArray();
                    if(matches.Length==0)return true;
                    // A duplicate or a delayed old native packet cannot
                    // replace a later same-peer owner head. No resend occurs.
                    return matches.All(token=>!token.Accepted&&!token.Published&&token.Source.Current()&&
                        token.MatchesPreviousHead(packet.Sender.latestPlayerData)&&
                        RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(packet.playerDataFile,token.Frame));
                }
                catch{return true;} // An unclassified native upload gets no purpose evidence.
            }
        }
        internal static void AcceptedPostfix(ClientInfo peer,PlayerDataFile data,bool run,object state)
        {
            if(!run||state is not Attempt attempt||!ThreadManager.IsMainThread()||!ReferenceEquals(attempt.Data,data)||!ReferenceEquals(peer.latestPlayerData,data))return;
            lock(gate)foreach(var token in attempt.Tokens)if(token.Source?.Current()==true&&ReferenceEquals(token.Source.SourceSender,peer)&&
                ReferenceEquals(token.Source.SourceViewer.QuestJournal,data.questJournal)){token.Accepted=true;token.AcceptedHead=new WeakReference<PlayerDataFile>(data);}
        }
        internal static void BeforeSave(PlayerDataFile data,string directory,string name,bool run,out object state)
        {
            state=null;if(!run||data==null)return;lock(gate)try
            {
                if(!accepted.TryGetValue(data,out var attempt)||attempt.Tokens.Any(value=>value.Source==null||value.Source.Closed||value.Source.Workers>=2))return;
                string root=System.IO.Path.GetFullPath(directory),path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,name+"."+PlayerDataFile.EXT));
                if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return;
                var bytes=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
                if(attempt.Tokens.Any(token=>token.Native==null||token.Root!=root||token.Name!=name||token.Path!=path||!bytes.SequenceEqual(token.Native)))return;
                foreach(var token in attempt.Tokens)token.Source.Workers++;state=new Writing{Tokens=attempt.Tokens,Image=bytes,Path=path};
            }
            catch{}
        }
        internal static void AfterSave(bool run,object state)
        {
            if(state is not Writing write)return;
            try{bool saved=run&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(write.Path,write.Image);
                lock(gate)if(saved)foreach(var token in write.Tokens)if(!token.Source.Closed&&token.Native.SequenceEqual(write.Image))token.Published=true;}
            finally{lock(gate){var owners=write.Tokens.Select(token=>token.Source).Where(source=>source!=null).GroupBy(source=>source).ToArray();
                write.Image=null;write.Tokens=null;write.Path=null;
                foreach(var owner in owners){owner.Key.Workers-=owner.Count();if(owner.Key.Closed)owner.Key.Dispose();}}}
        }
        internal static void ObserveInventory(RebirthWorkstationCraftCompletion.Snapshot snapshot)
        {
            if(snapshot?.Tile!=null||snapshot?.Player==null||snapshot.World.IsRemote())return;
            string actualEquipment=null;
            lock(gate)foreach(var source in sources.Values)
            {
                if(!source.Current()||source.Before==null||!ReferenceEquals(source.SourceViewer,snapshot.Player)||source.SourceViewerKey!=snapshot.NativeViewerKey||
                    source.SourceViewerCreation!=snapshot.NativeViewerCreation)continue;
                if(source.Before.Frame.Element("equipment")!=null&&(actualEquipment==null))
                    actualEquipment=Convert.ToBase64String(RebirthRecipeOutputIntegration.OwningEquipmentNative(snapshot.Player));
                if(ObserveOutputPrefixInventory(source,snapshot,actualEquipment))continue;
                if(source.After==null&&source.CachedEffect==null&&RebirthWorkstationCompletionPipeline.Grid(snapshot.Player.bag.ItemGrid.items)==(string)source.Before.Frame.Element("bag")&&
                    RebirthWorkstationCompletionPipeline.Grid(snapshot.Player.inventory.ItemGrid.items)==(string)source.Before.Frame.Element("belt")&&
                    (source.Before.Frame.Element("equipment")==null||actualEquipment==(string)source.Before.Frame.Element("equipment")))
                {
                    var original=new XElement("workstationCompletionIntent",new XAttribute("operation",source.SourceOperation.ToString("N")),new XAttribute("world",source.SourceWorldGuid),
                        new XElement("before",new XElement("nativeViewerInventoryBeforeAccepted",new XAttribute("generation",source.Generation),new XElement("bagBefore",snapshot.NativeBag),new XElement("beltBefore",snapshot.NativeBelt),new XElement(source.Before.Frame))));
                    source.BeforeInventoryPath=source.SourcePath+".viewer."+source.Generation+".inventory-before";
                    if(RebirthWorkstationCompletionPublication.Write(source.BeforeInventoryPath,original))source.BeforeInventoryReceipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(source.BeforeInventoryPath);
                    continue;
                }
                var stage=source.After??source.CachedEffect;
                var predecessor=source.Prefix?.Frame??source.Before.Frame;
                if(stage==null||!source.BeforeInventoryCurrent()||snapshot.NativeBag!=(string)predecessor.Element("bag")||snapshot.NativeBelt!=(string)predecessor.Element("belt")||
                    RebirthWorkstationCompletionPipeline.Grid(snapshot.Player.bag.ItemGrid.items)!=(string)stage.Frame.Element("bag")||
                    RebirthWorkstationCompletionPipeline.Grid(snapshot.Player.inventory.ItemGrid.items)!=(string)stage.Frame.Element("belt")||
                    predecessor.Element("equipment")!=null&&snapshot.NativeEquipment!=(string)predecessor.Element("equipment")||
                    stage.Frame.Element("equipment")!=null&&actualEquipment!=(string)stage.Frame.Element("equipment"))continue;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.SourceOperation.ToString("N")),new XAttribute("world",source.SourceWorldGuid),
                    new XElement("before",new XElement("nativeViewerInventoryAccepted",new XAttribute("generation",source.Generation),new XElement(source.Before.Frame),new XElement("after",new XElement(stage.Frame)))));
                string path=source.SourcePath+".viewer."+source.Generation+(ReferenceEquals(stage,source.CachedEffect)?".effect-inventory":".inventory");
                if(RebirthWorkstationCompletionPublication.Write(path,record))
                {var receipt=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                    if(ReferenceEquals(stage,source.CachedEffect)){source.EffectInventoryPath=path;source.EffectInventoryReceipt=receipt;}
                    else{source.InventoryPath=path;source.InventoryReceipt=receipt;source.InventoryAccepted=receipt!=null;}}
            }
        }
        internal static bool ValidateInventory(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,XElement active,XElement delivery,ItemStack primary,ItemStack[] extras)
        {
            string generation=RebirthWorkstationCompletionOriginal.Hash(active.ToString(SaveOptions.DisableFormatting));
            lock(gate)
            {
                if(!sources.TryGetValue(Key(original,generation),out var source)||!ReferenceEquals(source.Original,original)||!source.Current()||
                    !initialBefore.TryGetValue(original,out var initial)||!initial.Current()||
                    source.Before?.Current()!=true||!source.BeforeInventoryCurrent()||source.After?.Current()!=true||!source.InventoryCurrent()||!XNode.DeepEquals(source.Delivery,delivery)||
                    (string)delivery.Element("bagBefore")!=(string)source.Before.Frame.Element("bag")||(string)delivery.Element("beltBefore")!=(string)source.Before.Frame.Element("belt")||
                    (string)delivery.Element("bagAfter")!=(string)source.After.Frame.Element("bag")||(string)delivery.Element("beltAfter")!=(string)source.After.Frame.Element("belt")||
                    !EquipmentFrameMatches(source.Before.Frame,delivery,"equipmentBefore")||!EquipmentFrameMatches(source.After.Frame,delivery,"equipmentAfter"))return false;
                return ValidateOwnerInsertionPlan(source,delivery,primary,extras);
            }
        }
        internal static void Sweep(){lock(gate)foreach(var pair in sources.Where(pair=>!ReferenceEquals(pair.Value.SourceWorld,GameManager.Instance?.World)||pair.Value.SourceClosed).ToArray()){sources.Remove(pair.Key);if(pair.Value.Original!=null)initialBefore.Remove(pair.Value.Original);pair.Value.Dispose();}}
        internal static void Release(RebirthWorkstationCompletionPipeline.NativeHostOriginal original)
        {lock(gate){initialBefore.Remove(original);foreach(var pair in sources.Where(pair=>ReferenceEquals(pair.Value.Original,original)).ToArray()){sources.Remove(pair.Key);pair.Value.Dispose();}}}
    }
}
