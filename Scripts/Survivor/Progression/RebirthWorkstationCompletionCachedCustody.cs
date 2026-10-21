using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class CachedCustody:IDisposable
    {
        internal Guid Operation;internal string WorldGuid,Save,Path,MarkerKey,SavedPath;internal byte[] Marker,Image,Native,SavedImage,SettledNoticeImage;internal int Offset,SavedSequence=-1;
        internal XElement Original;internal Frame Frame;internal World World;internal TileEntityWorkstation Station;internal Chunk Chunk;
        internal bool Active=true,Saved,Closed,RestoreAttempted,Physical,PhysicalObserved;internal RebirthProductiveEarnedObservationBudget Memory;
        internal RebirthRemoteResourceNativePublication.Watch Watch;internal IDisposable Allocation;internal CachedLoadRequest Load;
        internal long Reserved;private long retirementWorkspace;internal string OwnerSavedPath,OwnerSavedImage;
        internal string SpacePath,SpaceImage;
        internal XElement CurrentFull;internal int Epoch;internal long AdmissionFence,WindowSession;
        internal CachedHostOriginal HostOriginal;
        internal RebirthProductiveEarnedUiDeferredTransport.HostTerminalPublication OwnerTerminal;
        internal CachedPhysicalPublication PhysicalPublication;
        internal IDisposable BorrowedMemory;internal bool OwnMemory=true;
        internal NativeHostOriginal RemoteSource;
        internal CraftCompleteData[] RemoteReceiptRows;
        internal long RemoteReceiptReadFence;
        internal CachedCreatorIssue CreatorIssue;
        internal bool HasOwnerReadFence;internal long OwnerReadFence;
        internal bool CreatorCloseObserved;internal long CreatorCloseReadFence;internal string CreatorClosePath;internal byte[] CreatorCloseImage;
        internal bool Grow(long bytes)
        {long needed=Math.Max(Reserved,bytes);if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(Allocation,needed))return false;Reserved=needed;return true;}
        internal bool ReserveRetirementWorkspace(long bytes)
        {if(bytes<=retirementWorkspace)return true;long delta=checked(bytes-retirementWorkspace);if(!Grow(checked(Reserved+delta)))return false;retirementWorkspace=bytes;return true;}
        private readonly object resourceGate=new object();private int references=1,nativeLoads;private bool released;
        internal IDisposable RetainResource(){lock(resourceGate){if(!Active||released)return null;references++;return new ResourceLease(this);}}
        internal IDisposable RetainNativeLoad(){lock(resourceGate){if(!Active||released||nativeLoads!=0)return null;nativeLoads++;references++;return new NativeLoadLease(this);}}
        private sealed class NativeLoadLease:IDisposable
        {private CachedCustody value;internal NativeLoadLease(CachedCustody source){value=source;}public void Dispose(){var source=System.Threading.Interlocked.Exchange(ref value,null);if(source==null)return;lock(source.resourceGate){source.nativeLoads--;source.ReleaseResource();}}}
        private sealed class ResourceLease:IDisposable
        {private CachedCustody value;internal ResourceLease(CachedCustody source){value=source;}public void Dispose(){System.Threading.Interlocked.Exchange(ref value,null)?.ReleaseResource();}}
        private void ReleaseResource()
        {
            lock(resourceGate)
            {
                if(--references!=0)return;released=true;
            }
            lock(cachedLoadGate){cachedLoadRequests.Remove(Load);if(Load!=null){Load.Snapshot=Load.Tile=null;Load.Loaded=null;}Load=null;}
            CreatorIssue=null;Native=Image=Marker=SavedImage=SettledNoticeImage=null;Original=CurrentFull=null;Frame=null;RemoteSource=null;RemoteReceiptRows=null;OwnerTerminal=null;World=null;Station=null;Chunk=null;
            CreatorClosePath=null;CreatorCloseImage=null;
            OwnerSavedPath=OwnerSavedImage=null;
            SpacePath=SpaceImage=null;
            if(OwnMemory)Memory?.Dispose();Memory=null;BorrowedMemory?.Dispose();BorrowedMemory=null;Allocation?.Dispose();Allocation=null;
        }
        internal bool MarkerMatches(Chunk chunk)=>Active&&chunk!=null&&chunk.ChunkCustomData.dict.TryGetValue(MarkerKey,out var marker)&&
            marker.TriggerWriteDataDelegate==null&&!marker.isSavedToNetwork&&marker.expiresInWorldTime==ulong.MaxValue&&marker.data!=null&&marker.data.SequenceEqual(Marker);
        internal bool PrimaryCurrent()=>Active&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image);
        internal static bool EvidenceCurrent(string path,string image)
        {try{return path==null||RebirthWorkstationCompletionPublication.ReadPrimary(path)!=null&&RebirthWorkstationCompletionPublication.ReadEvidence(path)==image;}catch{return false;}}
        internal bool SavedCurrent()=>Saved&&SavedPath!=null&&SavedImage!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(SavedPath,SavedImage);
        internal bool SourceCurrent()=>PrimaryCurrent()&&(!Saved||SavedCurrent())&&EvidenceCurrent(OwnerSavedPath,OwnerSavedImage)&&EvidenceCurrent(SpacePath,SpaceImage)&&World!=null&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldGuid&&System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==Save&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&ReferenceEquals(Station.GetChunk(),Chunk)&&MarkerMatches(Chunk);
        internal bool NativeCurrent()
        {
            if(!SourceCurrent()||Native==null||Memory==null)return false;
            using(var callbacks=Memory.TryEnterCallbacks())return callbacks!=null&&RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,Native,Offset);
        }
        internal bool TrySave()
        {
            if(!NativeCurrent()&&!TryAdvanceOwnerSaved())return false;if(Saved)return SavedCurrent()&&SealPhysicalSaved();
            if(Watch==null)
            {
                if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)||
                    !RebirthRemoteResourceNativeBinding.TryResolve(World,RemoteResourceIdentity.Workstation(Station.ToWorldPos()),RemoteResourceSourceKind.WorkstationOutput,birth,out var binding))return false;
                Watch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,NativeCurrent,Memory,MarkerKey);
            }
            if(Watch==null||!Watch.TryCopyPublishedTile(out var saved)||!NativeCurrent())return false;
            var snapshot=saved.CopySnapshot();var tile=saved.CopyTile();
            long bound=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(Original)+2L*(snapshot.Length+tile.Length+Native.Length)+65536+
                2L*((SpaceImage?.Length??0)+(OwnerSavedImage?.Length??0))+(CurrentFull==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(CurrentFull));
            if(!Grow(checked(64L*1024*1024+8*bound)))return false;
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                new XElement(Original.Element("before")),new XElement("cachedNativePublication",new XAttribute("version",1),
                    new XAttribute("original",HistoricalHash(Image)),new XAttribute("epoch",Epoch),new XAttribute("tileOffset",saved.TileOffset),new XAttribute("elapsedOffset",Offset),
                    new XAttribute("nativePath",saved.Publication.Path),new XElement("snapshot",Convert.ToBase64String(snapshot)),
                    new XElement("tile",Convert.ToBase64String(tile)),new XElement("native",Convert.ToBase64String(Native)),
                    new XElement("currentFull",new XElement(CurrentFull??Original.Element("cachedOriginal").Element("nativeOwningFullBoundary"))),
                    SpacePath==null?null:new XElement("actualSpace",new XAttribute("path",SpacePath),SpaceImage),
                    OwnerSavedPath==null?null:new XElement("ownerSaved",new XAttribute("path",OwnerSavedPath),OwnerSavedImage)));
            if(SavedSequence>=127)return false;
            string path=Path+".saved."+(SavedSequence+1);
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!NativeCurrent())return false;
            var seal=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(seal==null)return false;
            SavedPath=path;SavedImage=seal;SavedSequence++;Saved=true;
            lock(cachedLoadGate)
            {cachedLoadRequests.Remove(Load);Load=new CachedLoadRequest{Custody=this,Directory=Save,X=Chunk.X,Z=Chunk.Z,Snapshot=snapshot,Tile=tile,Offset=saved.TileOffset};cachedLoadRequests.Add(Load);}
            Watch.Dispose();Watch=null;
            return SealPhysicalSaved();
        }
        private bool SealPhysicalSaved()
        {
            if(!NativeCurrent())return false;
            if(Physical)
            {
                if(PhysicalPublication?.IsSameOwner==true&&!RebirthProductiveEarnedUiDeferredTransport.TrySealCachedSameOwnerTerminal(PhysicalPublication))return false;
                if(PhysicalPublication?.Previous!=null)RebirthProductiveEarnedUiDeferredTransport.ObserveSavedCachedPhysicalPublication(PhysicalPublication);
                TryPublishCachedSettledNotice(this);return true;
            }
            if(PhysicalObserved)
            {
                var observed=RebirthWorkstationCompletionPublication.ReadPrimary(Path+".physical-attempt");
                if(observed==null||!RebirthWorkstationCompletionPublication.Write(Path+".physical",observed)||!NativeCurrent())return false;
                if((bool?)observed.Element("actualCachedPhysical")?.Attribute("remote")==true)
                {
                    var seal=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                        new XElement(Original.Element("before")),
                        new XElement("remoteCachedPhysicalSaved",new XAttribute("original",HistoricalHash(Image)),
                            new XAttribute("physical",RebirthWorkstationCompletionOriginal.Hash(observed.ToString(SaveOptions.DisableFormatting))),
                            new XAttribute("savedPath",SavedPath),new XAttribute("saved",HistoricalHash(SavedImage))));
                    if(!RebirthWorkstationCompletionPublication.Write(Path+".remote-physical-saved",seal)||!NativeCurrent())return false;
                }
                Physical=true;
                if(observed.Element("actualCachedPhysical")?.Element("cachedPhysicalActorSaved")!=null&&!RetainActualSavedCachedPhysical(this))
                {Physical=false;return false;}
                if((OwnerTerminal!=null||Original.Element("cachedOriginal")?.Element("remoteCachedOwnerEffect")!=null||
                    Original.Element("cachedOriginal")?.Element("nativeOwnerLedger")!=null)&&
                    observed.Element("actualCachedPhysical")?.Element("cachedPhysicalActorSaved")!=null&&
                    !CachedPhysicalPublication.TryIssue(this,out _)){Physical=false;return false;}
                if(PhysicalPublication?.IsSameOwner==true&&!RebirthProductiveEarnedUiDeferredTransport.TrySealCachedSameOwnerTerminal(PhysicalPublication))
                {Physical=false;return false;}
                if(PhysicalPublication?.Previous!=null)RebirthProductiveEarnedUiDeferredTransport.ObserveSavedCachedPhysicalPublication(PhysicalPublication);
                TryPublishCachedSettledNotice(this);
            }
            return true;
        }
        private bool TryAdvanceOwnerSaved()
        {
            if(!SourceCurrent()||Frame?.NativeOriginal==null||!RebirthProductiveEarnedUiOwnerFile.TryGetCachedOwnerPublication(Frame.NativeOriginal,out var proof)||
                !RebirthProductiveEarnedNativeBody.SameExceptElapsed(Native,Offset,proof.Scope.NativeBefore,proof.Scope.BeforeOffset))return false;
            long bound=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(Original)+proof.Scope.NativeAfter.Length*2L+65536;
            long ownerLength=RebirthWorkstationCompletionPublication.PublishedLength(proof.Path);
            if(ownerLength<1||!Grow(checked(64L*1024*1024+8*(bound+4*ownerLength))))return false;
            string ownerImage=RebirthWorkstationCompletionPublication.ReadEvidence(proof.Path);
            if(!Grow(checked(64L*1024*1024+8*(bound+ownerImage.Length*2L))))return false;
            var candidate=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                new XElement(Original.Element("before")),new XElement("cachedOwnerAlreadySaved",new XAttribute("ownerProof",proof.Path),
                    new XAttribute("elapsedOffset",proof.Scope.AfterOffset),new XElement("proof",ownerImage),new XElement("native",Convert.ToBase64String(proof.Scope.NativeAfter))));
            string path=Path+".owner-saved";
            if(!RebirthWorkstationCompletionPublication.Write(path,candidate)||!proof.CurrentOwnerEvent()||!proof.Scope.BodyCurrent(true))return false;
            OwnerSavedPath=path;OwnerSavedImage=RebirthWorkstationCompletionPublication.ReadEvidence(path);
            Native=(byte[])proof.Scope.NativeAfter.Clone();Offset=proof.Scope.AfterOffset;Saved=false;Watch?.Dispose();Watch=null;return NativeCurrent();
        }
        public void Dispose()
        {
            if(!Active)return;Active=false;Watch?.Dispose();Watch=null;
            ForgetCachedPhysicalAction(this);
            RebirthProductiveEarnedUiDeferredTransport.ForgetCachedSameOwnerTerminal(this);
            PhysicalPublication?.Dispose();PhysicalPublication=null;OwnerTerminal=null;
            HostOriginal?.Dispose();HostOriginal=null;
            ReleaseResource();
        }
    }
    private static readonly Dictionary<Guid,CachedCustody> cachedCustodies=new Dictionary<Guid,CachedCustody>();
    internal static bool HasPersistedCachedOwnerEffect(Frame frame)=>frame?.Observation!=null&&
        cachedCustodies.TryGetValue(frame.Observation.Operation,out var source)&&source.Active&&ReferenceEquals(source.Frame,frame)&&source.PrimaryCurrent()&&
        source.Original.Element("cachedOriginal")?.Element("cachedOwnerEffect")!=null;
    private static bool cachedCustodyInstalled;
    internal static void InstallCachedCustody()
    {
        if(cachedCustodyInstalled)return;
        RebirthRemoteResourcePlacedStationOriginal.InstallCompletionCachedNativeLoadObservers();
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(ReadCachedCustodies));
        cachedCustodyInstalled=true;
    }
    private static void ObserveCachedCustody(Frame frame)
    {
        if(cachedCustodies.TryGetValue(frame.Observation.Operation,out var known)){RefreshCachedFull(known,frame);return;}
        if(frame.Observation.World.IsRemote()||!frame.Observation.FullBoundary||frame.SavedFullBoundaryVersion!=frame.Observation.NativeVersionAfter||
            frame.ConfirmedOriginal!=null||cachedCustodies.ContainsKey(frame.Observation.Operation)||cachedCustodies.Count>=4)return;
        CachedCustody value=null;IDisposable ledgerRetention=null;XElement ledger=null;
        try
        {
            var observed=frame.Observation;
            if(!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(observed.Window,out var windowSession))return;
            CachedOwnerEffectPublication ownerEffect=null;
            if(frame.EarlierOwner.StartingEntityId==observed.Viewer.entityId&&
                (!TryGetSavedCachedOwnerEffect(frame,out ownerEffect)||!ownerEffect.Current()))return;
            if(frame.NativeOriginal!=null&&(!remoteNativeReceipts.TryGetValue(frame.NativeOriginal.Operation,out var receipt)||
                !ReferenceEquals(receipt.NativeOriginal,frame.NativeOriginal)||
                !RebirthProductiveEarnedUiQueueOwner.TryCopyCachedNativeLedger(receipt,out ledger,out ledgerRetention)))return;
            long bytes=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(observed.FullBoundaryEvidence)+
                RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(ActiveBefore(frame.Intent))+(frame.NativeOriginal?.MeasureBefore()??0)+
                (ownerEffect?.MeasureImage()??0)+(ledger==null?0:RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(ledger))+65536;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(64L*1024*1024+8*bytes),out var allocation))return;
            value=new CachedCustody{Operation=observed.Operation,World=observed.World,Station=observed.Station,Chunk=observed.Station.GetChunk(),
                Frame=frame,WorldGuid=observed.WorldGuid,Save=observed.SaveDirectory,Path=frame.Path+".cache.xml",Allocation=allocation,Reserved=checked(64L*1024*1024+8*bytes),WindowSession=windowSession};
            value.Original=new XElement("workstationCompletionIntent",new XAttribute("operation",value.Operation.ToString("N")),new XAttribute("world",value.WorldGuid),
                new XElement("before",new XElement(ActiveBefore(frame.Intent))),new XElement("cachedOriginal",new XAttribute("owner",frame.Owner),
                    new XAttribute("creation",frame.Creation),new XAttribute("queueOwner",RebirthProductiveEarnedUiQueueOwner.Export(frame.EarlierOwner)),
                    new XElement(observed.FullBoundaryEvidence),ownerEffect==null?null:new XElement("cachedOwnerEffect",ownerEffect.Image),
                    frame.NativeOriginal==null?null:new XElement("nativeOwnerOriginal",frame.NativeOriginal.CopyCachedOriginal()),
                    ledger==null?null:new XElement("nativeOwnerLedger",new XElement(ledger))));
            value.CurrentFull=new XElement(observed.FullBoundaryEvidence);
            if(!RebirthWorkstationCompletionPublication.Write(value.Path,value.Original))return;
            value.Image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(value.Path);if(value.Image==null)return;
            value.MarkerKey="RebirthCompletionCachedV1:"+value.Operation.ToString("N");
            value.Marker=Encoding.ASCII.GetBytes(value.Operation.ToString("N")+":"+HistoricalHash(value.Image));
            if(value.Chunk.ChunkCustomData.dict.TryGetValue(value.MarkerKey,out var existing))
            {if(existing.TriggerWriteDataDelegate!=null||existing.data==null||!existing.data.SequenceEqual(value.Marker))return;}
            else
            {value.Chunk.ChunkCustomData.Set(value.MarkerKey,new ChunkCustomData(value.MarkerKey,ulong.MaxValue,false){data=(byte[])value.Marker.Clone()});
             value.Chunk.isModified=true;value.Station.setModified();}
            value.Native=Convert.FromBase64String((string)observed.FullBoundaryEvidence.Element("nativeAfter"));
            value.Offset=(int)observed.FullBoundaryEvidence.Attribute("nativeElapsedOffset");
            value.Memory=RebirthProductiveEarnedObservationBudget.TryOpenNativeCompletion(value.World,frame.EarlierOwner.CurrentCustodyBirth,value.Operation,value.Image);
            if(value.Memory==null||!value.NativeCurrent())return;
            cachedCustodies.Add(value.Operation,value);
            if(ownerEffect!=null)RetireCopiedCachedOwnerEffect(frame,ownerEffect);
            value=null;
        }
        catch{}
        finally{ledger=null;ledgerRetention?.Dispose();value?.Dispose();}
    }
    private static void ReadCachedCustodies(ref ModEvents.SGameStartingData data)
    {
        foreach(var value in cachedCustodies.Values){value.Frame?.CachedReentry?.Dispose();value.Dispose();}cachedCustodies.Clear();
        try
        {
            string save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()),folder=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"intents");
            RebirthWorkstationCompletionPublication.RequireSafeDirectory(folder);if(!Directory.Exists(folder))return;
            int scanned=0;
            foreach(string path in Directory.EnumerateFiles(folder,"*.cache.xml"))
            {
                if(++scanned>4096||cachedCustodies.Count>=4)break;
                CachedCustody value=null;bool classified=false;
                try
                {
                    string savedPath=LatestCachedNativePublication(path,out int savedSequence);
                    long a=RebirthWorkstationCompletionPublication.PublishedLength(path),b=savedPath==null?-1:RebirthWorkstationCompletionPublication.PublishedLength(savedPath);
                    if(a<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(64L*1024*1024+8*(a+b)),out var memory))continue;
                    value=new CachedCustody{Path=path,Save=save,Allocation=memory,Reserved=checked(64L*1024*1024+8*(a+b)),Closed=true};
                    value.Original=RebirthWorkstationCompletionPublication.ReadPrimary(path);value.Image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                    if(value.Original?.Element("cachedOriginal")==null||!Guid.TryParseExact((string)value.Original.Attribute("operation"),"N",out value.Operation)||value.Operation==Guid.Empty||
                        value.Original.Element("before")?.Elements().Count()!=1)continue;
                    value.WorldGuid=(string)value.Original.Attribute("world");
                    value.MarkerKey="RebirthCompletionCachedV1:"+value.Operation.ToString("N");value.Marker=Encoding.ASCII.GetBytes(value.Operation.ToString("N")+":"+HistoricalHash(value.Image));
                    // A valid original classifies its own native marker even if
                    // a later publication is incomplete. Classification grants
                    // no cached retry, receipt consumption or native action.
                    classified=true;
                    if(IsEmptyCacheRetired(value)||IsSucceededCacheRetired(value)||IsLoadedPhysicalCacheRetired(value)||IsRemoteCacheSuccessorRetired(value)||IsCachedPhysicalQueryRetired(value)||IsCachedGroupPhysicalRetired(value)){classified=false;continue;}
                    if(b<1)continue;
                    if(File.Exists(path+".physical")){value.PhysicalObserved=true;value.RestoreAttempted=true;}
                    var publication=RebirthWorkstationCompletionPublication.ReadPrimary(savedPath);var published=publication?.Element("cachedNativePublication");
                    if(!Guid.TryParseExact((string)value.Original.Attribute("operation"),"N",out value.Operation)||value.Operation==Guid.Empty||
                        published==null||(string)published.Attribute("version")!="1"||(string)published.Attribute("original")!=HistoricalHash(value.Image)||
                        (string)publication.Attribute("operation")!=value.Operation.ToString("N")||
                        !XNode.DeepEquals(value.Original.Element("before"),publication.Element("before")))continue;
                    value.WorldGuid=(string)value.Original.Attribute("world");if((string)publication.Attribute("world")!=value.WorldGuid)continue;
                    if(value.Original.Element("cachedOriginal")?.Element("cachedOwnerEffect") is XElement savedEffect&&
                        !MatchesSavedCachedOwnerEffect(savedEffect.Elements().SingleOrDefault()))continue;
                    if(value.Original.Element("cachedOriginal")?.Element("remoteCachedOwnerEffect") is XElement remoteEffect&&
                        !RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.MatchesSavedCachedBoundaryOwner(remoteEffect.Elements().SingleOrDefault()))continue;
                    value.Epoch=(int?)published.Attribute("epoch")??0;if(value.Epoch<0||value.Epoch>64)continue;
                    if(File.Exists(path+".restore-attempt."+value.Epoch))value.RestoreAttempted=true;
                    if(File.Exists(path+".remote-action."+value.Epoch)||File.Exists(path+".remote-action."+value.Epoch+".tmp"))value.RestoreAttempted=true;
                    if(File.Exists(path+".physical-attempt"))value.RestoreAttempted=true;
                    value.CurrentFull=published.Element("currentFull")?.Elements().SingleOrDefault();
                    if(value.CurrentFull==null||(string)value.CurrentFull.Element("primaryNative")!=(string)value.Original.Element("cachedOriginal")?.Element("nativeOwningFullBoundary")?.Element("primaryNative"))continue;
                    value.MarkerKey="RebirthCompletionCachedV1:"+value.Operation.ToString("N");value.Marker=Encoding.ASCII.GetBytes(value.Operation.ToString("N")+":"+HistoricalHash(value.Image));
                    value.Native=Convert.FromBase64String((string)published.Element("native"));value.Offset=(int)published.Attribute("elapsedOffset");
                    if(published.Element("ownerSaved") is XElement ownerSaved)
                    {
                        value.OwnerSavedPath=(string)ownerSaved.Attribute("path");value.OwnerSavedImage=ownerSaved.Value;
                        if(RebirthWorkstationCompletionPublication.ReadPrimary(value.OwnerSavedPath)==null||
                            RebirthWorkstationCompletionPublication.ReadEvidence(value.OwnerSavedPath)!=value.OwnerSavedImage)continue;
                    }
                    if(published.Element("actualSpace") is XElement actualSpace)
                    {
                        value.SpacePath=(string)actualSpace.Attribute("path");value.SpaceImage=actualSpace.Value;
                        if(!CachedCustody.EvidenceCurrent(value.SpacePath,value.SpaceImage))continue;
                    }
                    var before=value.Original.Element("before").Elements().Single();int x=(int)before.Attribute("x"),z=(int)before.Attribute("z");
                    value.Load=new CachedLoadRequest{Custody=value,Directory=save,X=x>>4,Z=z>>4,Snapshot=Convert.FromBase64String((string)published.Element("snapshot")),
                        Tile=Convert.FromBase64String((string)published.Element("tile")),Offset=(long)published.Attribute("tileOffset")};
                    if(value.Load.Snapshot.Length>RebirthStationSnapshotEvidence.MaximumSnapshotBytes||value.Load.Tile.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||
                        value.Native.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||value.Load.Offset<8||value.Load.Offset>value.Load.Snapshot.Length-value.Load.Tile.Length||
                        !value.Load.Snapshot.Skip((int)value.Load.Offset).Take(value.Load.Tile.Length).SequenceEqual(value.Load.Tile))continue;
                    value.SavedPath=savedPath;value.SavedSequence=savedSequence;value.SavedImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(savedPath);
                    if(value.SavedImage==null)continue;
                    value.Saved=true;
                    if(value.RestoreAttempted&&IsHistoricalCachedUnusedCloseSaved(value))value.RestoreAttempted=false;
                    lock(cachedLoadGate)cachedLoadRequests.Add(value.Load);cachedCustodies.Add(value.Operation,value);value=null;
                }
                catch{}
                finally
                {
                    if(value!=null&&classified&&!cachedCustodies.ContainsKey(value.Operation))
                    {value.Saved=false;value.RestoreAttempted=true;value.Load=null;cachedCustodies.Add(value.Operation,value);value=null;}
                    value?.Dispose();
                }
            }
        }
        catch{}
    }
    private static void RefreshCachedFull(CachedCustody source,Frame frame)
    {
        var observed=frame.Observation;var full=observed.FullBoundaryEvidence;
        if(!source.Active||source.Physical||!ReferenceEquals(source.Frame,frame)||!observed.FullBoundary||full==null||source.Epoch>=64||
            (long?)source.CurrentFull?.Attribute("nativeVersion")==observed.NativeVersionAfter||!observed.Current()||!observed.RecipeMatches()||
            (string)full.Element("primaryNative")!=(string)source.Original.Element("cachedOriginal").Element("nativeOwningFullBoundary").Element("primaryNative")||
            !source.NativeCurrent())return;
        long size=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(full)+65536;
        long snapshots=source.Load==null?0:2L*(source.Load.Snapshot.Length+source.Load.Tile.Length);
        if(!source.Grow(checked(64L*1024*1024+8*(size+snapshots+RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(source.Original)))))return;
        var record=new XElement("workstationCompletionIntent",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("world",source.WorldGuid),
            new XElement(source.Original.Element("before")),new XElement("actualRepeatedCachedFull",new XAttribute("epoch",source.Epoch+1),new XElement(full)));
        if(!RebirthWorkstationCompletionPublication.Write(source.Path+".full."+observed.NativeVersionAfter,record))return;
        source.CurrentFull=new XElement(full);source.Epoch++;source.RestoreAttempted=false;source.Saved=false;source.Watch?.Dispose();source.Watch=null;
        if(frame.CachedReentry!=null)frame.CachedReentry.Full=source.CurrentFull;
    }
    private static string LatestCachedNativePublication(string original,out int sequence)
    {
        sequence=-1;string result=null;int scanned=0;
        string directory=System.IO.Path.GetDirectoryName(original),prefix=System.IO.Path.GetFileName(original)+".saved.";
        foreach(string candidate in Directory.EnumerateFiles(directory,prefix+"*"))
        {
            if(++scanned>256)return original+".saved.unknown";
            string suffix=System.IO.Path.GetFileName(candidate).Substring(prefix.Length);
            if(suffix.EndsWith(".tmp",StringComparison.Ordinal))suffix=suffix.Substring(0,suffix.Length-4);
            if(!int.TryParse(suffix,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int number))continue;
            if(number<0||number>127)return original+".saved.unknown";
            if(number>sequence){sequence=number;result=original+".saved."+number;}
        }
        if(result!=null)return result;
        return File.Exists(original+".saved")||File.Exists(original+".saved.tmp")?original+".saved":null;
    }
}
