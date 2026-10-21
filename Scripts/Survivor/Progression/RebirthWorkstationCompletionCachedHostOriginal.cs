using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Receipt-purpose source only. It observes the original actual Add ledger
    // in saved native custody; it never reconstructs a craft or inserts output.
    internal sealed partial class CachedHostOriginal:IDisposable
    {
        private CachedCustody source;private readonly CachedLoaded loaded;
        private readonly RebirthProductiveEarnedUiQueueOwner.DeferredReceipt warm;
        private IDisposable retention;private byte[] native;private readonly int offset;
        private readonly string path;private byte[] image;
        private readonly System.Collections.Generic.Dictionary<string,RebirthProductiveEarnedUiQueueOwner.Witness> identities=new System.Collections.Generic.Dictionary<string,RebirthProductiveEarnedUiQueueOwner.Witness>(StringComparer.Ordinal);
        internal readonly Guid Operation,Nonce,Birth,CustodyBirth;
        internal readonly World World;internal readonly TileEntityWorkstation Station;
        internal readonly string SourceHash,RecipientPurpose,CreatorPurpose;
        internal bool OwnerTerminalSaved {get;private set;}
        internal RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication OwnerTerminal {get;private set;}
        internal bool IsPhysicalPending=>source!=null&&source.Active&&!source.Physical;
        internal string OriginalViewerKey=>source==null?null:(string)source.Original.Element("before").Elements().Single().Attribute("viewer");
        internal string OriginalViewerCreation=>source==null?null:(string)source.Original.Element("before").Elements().Single().Attribute("creation");
        internal int OriginalViewerDeaths=>source==null?-1:(int)source.Original.Element("before").Elements().Single().Attribute("deaths");
        internal RebirthProductiveEarnedObservationBudget Memory=>source?.Memory;
        internal NativeHostOriginal NativeSource=>source?.RemoteSource;
        internal CachedPhysicalPublication SavedPhysical=>source?.PhysicalPublication;
        internal long MeasureBefore()=>source==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Original.Element("before").Elements().Single());
        internal XElement CopyBefore()=>SourceCurrentWithoutRows()?new XElement(source.Original.Element("before").Elements().Single()):null;
        internal string IdentityPath=>path;internal byte[] IdentityImage=>image;
        internal bool ContainsOriginalPurpose(string purpose)=>source!=null&&purpose!=null&&
            (purpose==RecipientPurpose||purpose==CreatorPurpose||source.Original.Element("cachedOriginal").Element("nativeOwnerLedger").Elements().Single().Element("members")?.Elements("member").Any(member=>(string)member.Attribute("source")==purpose)==true);
        internal bool TryResolvePurpose(string purpose,out RebirthProductiveEarnedUiQueueOwner.Witness witness)
        {
            witness=null;if(!SourceCurrentWithoutRows()||!ContainsOriginalPurpose(purpose))return false;
            if(identities.TryGetValue(purpose,out witness))return witness.SourceCurrent();
            if(identities.Count>=5||!Current()||!RebirthProductiveEarnedUiQueueOwner.TryResolveCachedHostPurpose(this,purpose,out witness))return false;
            identities.Add(purpose,witness);return true;
        }
        internal bool TryGetAggregate(out CraftCompleteData row)=>RebirthProductiveEarnedUiQueueOwner.TryMapCachedHostAggregate(this,out row);
        internal bool TrySaveOwnerTerminal(RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication proof)
        {
            if(proof?.IncludesCachedOriginal(this)!=true||!SourceCurrentWithoutRows()||!ReferenceEquals(proof.Station,Station))return false;
            if(OwnerTerminalSaved)return true;
            if(ReferenceEquals(source.OwnerTerminal,proof)&&source.OwnerSavedImage!=null&&CachedCustody.EvidenceCurrent(source.OwnerSavedPath,source.OwnerSavedImage))
            {OwnerTerminal=proof;OwnerTerminalSaved=true;return true;}
            long bound=proof.GroupEvidenceBound+2L*proof.Body.Length+65536;
            if(!source.ReserveRetirementWorkspace(checked(8*bound)))return false;
            using(var workspace=proof.EnterGroupPublication(bound))
            {
                if(workspace==null)return false;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                    new XElement(source.Original.Element("before")),new XElement("cachedOwnerTerminal",new XAttribute("original",SourceHash),
                        new XAttribute("nonce",Nonce.ToString("N")),proof.CopyGroupEvidence()));
                string terminalPath=source.Path+".owner-terminal";
                if(!RebirthWorkstationCompletionPublication.Write(terminalPath,record)||!proof.Current())return false;
                string terminalImage=RebirthWorkstationCompletionPublication.ReadEvidence(terminalPath);if(terminalImage==null)return false;
                var saved=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
                    new XElement(source.Original.Element("before")),new XElement("cachedOwnerAlreadySaved",new XAttribute("ownerProof",terminalPath),
                        new XAttribute("elapsedOffset",proof.Offset),new XElement("proof",terminalImage),new XElement("native",Convert.ToBase64String(proof.Body))));
                string savedPath=source.Path+".owner-saved";
                if(!RebirthWorkstationCompletionPublication.Write(savedPath,saved)||!proof.Current())return false;
                var savedImage=RebirthWorkstationCompletionPublication.ReadEvidence(savedPath);if(savedImage==null||!proof.TryGetCachedOwnerReadFence(out var ownerReadFence))return false;
                source.OwnerSavedPath=savedPath;source.OwnerSavedImage=savedImage;source.Native=(byte[])proof.Body.Clone();source.Offset=proof.Offset;
                source.OwnerReadFence=ownerReadFence;source.HasOwnerReadFence=true;if(loaded!=null)loaded.ReadFence=ownerReadFence;
                if(cachedPhysicalActions.TryGetValue(Operation,out var action)&&ReferenceEquals(action.Issue?.Cache,source)&&
                    action.Issue.RecordedIdentity()&&action.ViewerKey==OriginalViewerKey&&action.ViewerCreation==OriginalViewerCreation&&
                    action.ViewerDeaths==OriginalViewerDeaths&&!action.PhysicalAccepted)
                    action.ObserveOwnerTerminalRead(ownerReadFence);
                source.Saved=false;source.Watch?.Dispose();source.Watch=null;OwnerTerminalSaved=true;OwnerTerminal=proof;source.OwnerTerminal=proof;
                // This distinct cache generation carries the actual saved
                // removal forward. Observe its native save before admitting a
                // new physical Before; the historical owner proof stays fixed.
                source.TrySave();source.Chunk.isModified=true;source.Station.setModified();return true;
            }
        }
        private CachedHostOriginal(CachedCustody original,CachedLoaded read,RebirthProductiveEarnedUiQueueOwner.DeferredReceipt actual,
            Guid nonce,Guid historicalBirth,Guid custodyBirth,string path,byte[] image,IDisposable retained)
        {
            source=original;loaded=read;warm=actual;retention=retained;Operation=original.Operation;Nonce=nonce;Birth=historicalBirth;CustodyBirth=custodyBirth;
            World=original.World;Station=original.Station;SourceHash=HistoricalHash(original.Image);this.path=path;this.image=image;
            native=(byte[])original.Native.Clone();offset=original.Offset;
            var ledger=original.Original.Element("cachedOriginal").Element("nativeOwnerLedger").Elements().Single();
            RecipientPurpose=(string)ledger.Attribute("recipient");CreatorPurpose=(string)ledger.Attribute("source");
        }
        internal long MeasureMembership()=>source==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(
            source.Original.Element("cachedOriginal").Element("nativeOwnerLedger").Elements().Single());
        internal XElement CopyMembership()=>SourceCurrentWithoutRows()?new XElement(source.Original.Element("cachedOriginal").Element("nativeOwnerLedger").Elements().Single()):null;
        internal byte[] PublishedBody=>native;internal int PublishedOffset=>offset;
        internal bool SourceCurrentWithoutRows()=>source!=null&&retention!=null&&source.SourceCurrent()&&
            ReferenceEquals(source.World,World)&&ReferenceEquals(source.Station,Station)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==CustodyBirth.ToString("N")&&
            (loaded!=null?loaded.InstalledSource(World):warm!=null?warm.CurrentSourceWithoutRow():source.RemoteSource?.SourceCurrent()==true)&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,image);
        internal CraftCompleteData[] ActualReceiptRows=>loaded!=null?loaded.ActualReceiptRows?.ToArray():warm!=null?new[]{warm.Receipt}:source?.RemoteReceiptRows?.ToArray();
        internal bool Current()
        {
            if(!SourceCurrentWithoutRows()||!source.Saved)return false;
            var rows=ActualReceiptRows;if(rows==null||rows.Length<1||rows.Length>4||rows.Any(row=>Station.CraftCompleteList?.Count(value=>ReferenceEquals(value,row))!=1))return false;
            using(var callback=source.Memory?.TryEnterCallbacks())return callback!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,native,offset);
        }
        internal IDisposable Retain()=>SourceCurrentWithoutRows()?source.RetainResource():null;
        public void Dispose(){foreach(var value in identities.Values)value.Dispose();identities.Clear();OwnerTerminal=null;localOwnerTerminal=null;localOwnerTerminalImage=localOwnerTerminalPath=null;native=image=null;retention?.Dispose();retention=null;source=null;}
        internal static CachedHostOriginal FromSavedOriginal(CachedCustody original,CachedLoaded read,RebirthProductiveEarnedUiQueueOwner.DeferredReceipt actual)
        {
            if(original==null||!original.Saved||!original.NativeCurrent()||
                (read!=null?read.Installed(original.World)!=true:actual!=null?!actual.CurrentAggregateMembership():
                    original.RemoteSource?.AdmissionCurrent()!=true||original.RemoteReceiptRows==null||
                    !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(original.World,original.Station,out var currentFence)||currentFence!=original.RemoteReceiptReadFence))return null;
            var ledger=original.Original.Element("cachedOriginal")?.Element("nativeOwnerLedger")?.Elements().SingleOrDefault();
            if(ledger==null||(bool?)ledger.Attribute("cachedOriginal")!=true||(string)ledger.Attribute("operation")!=original.Operation.ToString("N"))return null;
            XElement creator;
            using(var input=new System.IO.StringReader((string)ledger.Attribute("source")))
            using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096,MaxCharactersFromEntities=1}))creator=XElement.Load(reader);
            if(!Guid.TryParseExact((string)creator.Attribute("birth"),"N",out var historical)||historical==Guid.Empty||
                !RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.World,original.Station,out var birthText)||!Guid.TryParseExact(birthText,"N",out var birth)||birth==Guid.Empty||
                !original.ReserveRetirementWorkspace(checked(64*1024L+4L*original.Native.Length)))return null;
            string path=original.Path+".host-source";Guid nonce;
            var prior=RebirthWorkstationCompletionPublication.ReadPrimary(path);
            if(prior==null)nonce=Guid.NewGuid();
            else if(!Guid.TryParseExact((string)prior.Element("cachedHostSource")?.Attribute("nonce"),"N",out nonce)||nonce==Guid.Empty)return null;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("world",original.WorldGuid),
                new XElement(original.Original.Element("before")),
                new XElement("cachedHostSource",new XAttribute("nonce",nonce.ToString("N")),new XAttribute("original",HistoricalHash(original.Image)),
                    new XAttribute("birth",historical.ToString("N")),new XAttribute("cache",original.Path)));
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!original.NativeCurrent())return null;
            var bytes=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(bytes==null)return null;
            var retained=original.RetainResource();if(retained==null)return null;
            try{var result=new CachedHostOriginal(original,read,actual,nonce,historical,birth,path,bytes,retained);retained=null;return result;}
            finally{retained?.Dispose();}
        }
    }
    internal static bool TryGetCachedHostOriginal(World world,TileEntityWorkstation station,out CachedHostOriginal original)
    {
        original=null;if(world==null||world.IsRemote()||!ThreadManager.IsMainThread())return false;
        var candidates=cachedCustodies.Values.Where(value=>value.Active&&!value.Physical&&value.Original.Element("cachedOriginal")?.Element("nativeOwnerLedger")!=null&&
            (ReferenceEquals(value.World,world)&&ReferenceEquals(value.Station,station)||ReferenceEquals(value.Load?.Loaded?.Tile,station)&&value.Load.Loaded.Installed(world))).ToArray();
        if(candidates.Length!=1)return false;var source=candidates[0];
        try
        {
            if(source.HostOriginal!=null){original=source.HostOriginal;return original.Current();}
            var loaded=source.Load?.Loaded;RebirthProductiveEarnedUiQueueOwner.DeferredReceipt actual=null;
            if(loaded?.Installed(world)==true)
            {
                source.World=world;source.Station=station;source.Chunk=station.GetChunk();
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var text)||!Guid.TryParseExact(text,"N",out var birth))return false;
                source.Memory??=RebirthProductiveEarnedObservationBudget.TryOpenNativeCompletion(world,birth,source.Operation,source.Image);
            }
            else if(source.RemoteSource!=null)
            {if(!source.RemoteSource.AdmissionCurrent()||source.RemoteReceiptRows==null)return false;}
            else if(source.Frame?.NativeOriginal==null||!remoteNativeReceipts.TryGetValue(source.Operation,out actual)||
                !ReferenceEquals(actual.NativeOriginal,source.Frame.NativeOriginal))return false;
            source.HostOriginal=CachedHostOriginal.FromSavedOriginal(source,loaded?.Installed(world)==true?loaded:null,actual);
            original=source.HostOriginal;return original?.Current()==true;
        }
        catch{return false;}
    }
}
