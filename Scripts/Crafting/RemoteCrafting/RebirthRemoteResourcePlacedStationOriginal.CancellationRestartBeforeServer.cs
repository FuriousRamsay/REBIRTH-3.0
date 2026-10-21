using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    // Before reconstruction retains a positive v2 original even when its
    // attempted state is unknown. ONLY Prepared may issue a first mutation;
    // Uncertain is a native-lifecycle-bound negative custody hold.
    private static bool ReadRestartBeforeCustody(CancellationRestartArchive archive)
    {
        if(!archive.BeforeRestart||!archive.Memory.Reserve(checked(archive.Memory.ReservedBytes+65536)))return false;
        string root=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellationHostCustody");
        long bytes=0;string prefix=archive.Operation.ToString("N");
        foreach(string folder in new[]{root,Path.Combine(root,"completed"),Path.Combine(root,"completed",prefix)})
        {CompletionAncestors(folder);if(!Directory.Exists(folder))continue;int count=0;foreach(string path in Directory.EnumerateFileSystemEntries(folder,prefix+"*"))
            {if(++count>32) return false;if(Directory.Exists(path)){if(folder==Path.Combine(root,"completed")&&Path.GetFileName(path)==prefix)continue;return false;}
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;long size=new FileInfo(path).Length;if(size>MaximumResultBytes+8192)return false;bytes=checked(bytes+size);}}
        if(!archive.Memory.Reserve(checked(archive.Memory.ReservedBytes+32*bytes+65536)))return false;
        var record=HostCustodyJournalAt(root,archive.Operation,MaximumResultBytes/2).Read(archive.Operation);
        if(record==null||record.State!=RebirthCamperTransferJournal.Phase.Prepared&&record.State!=RebirthCamperTransferJournal.Phase.Uncertain||record.Operation!=archive.Operation||record.Vehicle!=archive.Source||record.OwnerCreation!=archive.Original.Creation||record.Revision!=0||
            !record.Before.SequenceEqual(record.After)||!archive.Memory.Reserve(checked(archive.Memory.ReservedBytes+96L*record.Before.Length+4L*archive.SavedNative.Snapshot.Length+65536)))return false;
        var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),MaximumResultBytes);
        if(body.Name!="placedCancellationHostCustody"||body.Attributes().Count()!=9||body.Elements().Count()!=4||(int?)body.Attribute("version")!=2||
            (string)body.Attribute("cancel")!=archive.Operation.ToString("N")||(string)body.Attribute("source")!=archive.Source.ToString("N")||
            (string)body.Attribute("binding")!=archive.Binding||(string)body.Attribute("birth")!=archive.Birth||(string)body.Attribute("world")!=archive.Original.World||
            (string)body.Attribute("owner")!=archive.Original.Owner||(string)body.Attribute("creation")!=archive.Original.Creation||(int?)body.Attribute("deaths")!=archive.Original.Deaths||
            body.Element("request")?.Value!=archive.Header||body.Element("permission")?.Value!=archive.Permission||body.Element("acceptedNativeOriginal")?.Value!=archive.AcceptedEvidence||body.Element("savedBefore")?.Value!=archive.BeforeEvidence)return false;
        var custody=new CancellationHostCustody{Operation=archive.Operation,Source=archive.Source,Creation=archive.Original.Creation,Root=root,
            RetirementRoot=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellationHostRetirement"),Anchor=record.Before,Snapshot=archive.SavedNative.Snapshot};
        custody.MaxEscapeBytes=MeasureHostCustody(new XElement("placedCancellationHostRetirement",new XAttribute("version",1),new XAttribute("cancel",archive.Operation.ToString("N")),
            new XAttribute("key",Guid.Empty.ToString("N")),new XAttribute("decision","refuse-original"),new XAttribute("boundary",new string('X',512)),new XElement("completeCustodyOriginal",Encoding.UTF8.GetString(custody.Anchor))));
        if(!archive.Memory.Reserve(checked(archive.Memory.ReservedBytes+40L*custody.MaxEscapeBytes)))return false;
        archive.BeforeUncertain=record.State!=RebirthCamperTransferJournal.Phase.Prepared;archive.HostCustody=custody;return true;
    }
    private static void RetireRestartBeforeCustody(CancellationRestartArchive archive)
    {
        var custody=archive.HostCustody;archive.HostCustody=null;if(custody==null)return;
        lock(custody.Gate){custody.Closing=true;if(TryRetireCancellationHostCustody(custody))return;}
        lock(cancellationHostCustodyGate)if(!cancellationRetiringCustodies.Contains(custody))cancellationRetiringCustodies.Add(custody);
    }
    private static bool SavedRestartBeforeOwner(CancellationPendingOwner candidate,CancellationRestartArchive archive)
    {
        if(candidate.ReceiptPhase!=1&&candidate.ReceiptPhase!=2||!archive.BeforeRestart||candidate.Owner.CanonicalId!=archive.Original.Owner||candidate.Deaths!=archive.Original.Deaths||
            !RebirthCamperEnergySavedOwner.TryRead(candidate.Owner,out var image,out var saved)||saved.id!=archive.NativeOwner||saved.deaths!=archive.Original.Deaths||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,CancellationReceipt(archive.Operation),out float phase,out bool present)||!present||phase!=candidate.ReceiptPhase)return false;
        return candidate.ReceiptPhase==2?archive.After.Matches(image):IsCancellationOwnerPrefix(archive.Before,archive.After,image);
    }
    private static bool IsCancellationOwnerPrefix(RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,RebirthCamperEnergyActorImage actual)
    {
        var prefix=before.Copy();if(prefix.Matches(actual))return true;int changes=0;
        foreach(bool bag in new[]{true,false})
        {
            var from=bag?before.Bag:before.Belt;var to=bag?after.Bag:after.Belt;
            if(from.Length!=to.Length)return false;
            for(int i=0;i<from.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(from[i],to[i]))
            {if(++changes>32)return false;if(bag)prefix.Bag[i]=to[i].Clone();else prefix.Belt[i]=to[i].Clone();if(prefix.Matches(actual))return true;}
        }
        return false;
    }
    private static bool EnterCancellationCustodyQueueAttempt(CancellationServer original)
    {
        var custody=original.HostCustody;if(custody==null||original.QueueAttempted||!original.Current()||!CancellationHostCustodyCurrent(original))return false;
        lock(custody.Gate)
        {
            var journal=HostCustodyJournal(custody,false);var record=journal.Read(original.Operation);
            if(!HostCustodyRecord(custody,record,custody.Anchor,RebirthCamperTransferJournal.Phase.Prepared))return false;
            record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            return HostCustodyRecord(custody,record,custody.Anchor,RebirthCamperTransferJournal.Phase.Uncertain);
        }
    }
    private sealed class CancellationReceipt1Source
    {
        private readonly CancellationServer original;private readonly World world;private readonly RegionFileManager manager;private readonly string birth;
        private CancellationLoaded loaded;private TileEntityWorkstation tile;private Chunk chunk;
        private readonly Guid retainedRecovery=Guid.NewGuid();
        internal CancellationReceipt1Source(CancellationServer value,CancellationLoaded source)
        {original=value;loaded=source;world=value.Actor.world;manager=source?.Original.Manager??(world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld)?.m_RegionFileManager;
            birth=value.Birth;tile=source?.Tile??value.Station;chunk=source?.Chunk??value.Chunk;}
        internal bool Current=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(original.Actor?.world,world)&&
            ReferenceEquals(original.Station,tile)&&ReferenceEquals(original.Chunk,chunk)&&ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()),tile)&&
            ReferenceEquals(world.GetChunkSync(chunk.X,chunk.Z),chunk)&&ReferenceEquals(tile.GetChunk(),chunk)&&!chunk.InProgressUnloading&&
            world.ChunkCache?.ChunkProvider is ChunkProviderGenerateWorld provider&&ReferenceEquals(provider.m_RegionFileManager,manager)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,tile,out var currentBirth)&&currentBirth==birth&&
            (original.AfterSaved||loaded?.Installed()==true||loaded==null&&original.Accepted!=null&&original.HostCustody!=null&&!original.HostCustody.Closing&&!original.HostCustody.Closed);
        internal void AdoptAfter(CancellationLoaded value)
        {
            if(value?.Installed()!=true||!ReferenceEquals(value.Original.Original,original)||value.Original.Header!=original.Header||value.Original.Permission!=original.Permission)return;
            loaded=value;tile=value.Tile;chunk=value.Chunk;
        }
        internal string Grant(RebirthCamperEnergyActorImage prefix)=>new XElement("placedCancellationBeforeReconstruction",new XAttribute("version",1),
            new XAttribute("cancel",original.Operation.ToString("N")),new XAttribute("source",original.Source.ToString("N")),new XAttribute("birth",birth),
            new XAttribute("ownerId",original.Actor.entityId),new XAttribute("creation",original.Original.Creation),new XAttribute("deaths",original.Original.Deaths),
            new XElement("nativeLoad",loaded?.Encode()??new XElement("placedCancellationRetainedNativeOriginal",new XAttribute("version",1),new XAttribute("cancel",original.Operation.ToString("N")),
                new XAttribute("source",original.Source.ToString("N")),new XAttribute("binding",original.Binding),new XAttribute("birth",birth),new XAttribute("world",original.Original.World),
                new XAttribute("recovery",retainedRecovery.ToString("N")),new XElement("acceptedNativeOriginal",RebirthCookingRemoteAdmission.ReadProtocolXml(Encoding.UTF8.GetString(original.HostCustody.Anchor),MaximumResultBytes).Element("acceptedNativeOriginal").Value)).ToString(SaveOptions.DisableFormatting)),new XElement("savedBefore",original.Receipt1BeforeEvidence),new XElement("hostCustody",Encoding.UTF8.GetString(original.HostCustody.Anchor)),
            new XElement("ownerPrefix",RebirthRemoteResourceClientOriginal.ActorImage(prefix))).ToString(SaveOptions.DisableFormatting);
    }
    private static bool RebindReceipt1Server(CancellationServer original,EntityPlayer actor,string header)
    {
        if(cancellationNativeRequest?.Current!=true||!ReferenceEquals(cancellationNativeRequest.Actor,actor)||original.QueueAttempted||original.AfterSaved||
            original.Header!=header||original.Memory?.Current!=true||original.HostCustody==null||original.Accepted==null||!ServerScope(actor,original.Original)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RebirthCamperEnergySavedOwner.TryRead(owner,out var prefix,out var saved)||
            saved.id!=actor.entityId||actor.entityId!=(int?)RebirthCookingRemoteAdmission.ReadProtocolXml(RebirthCookingRemoteAdmission.ReadProtocolXml(original.Permission,MaximumResultBytes).Element("beforeEvidence").Value,MaximumResultBytes).Attribute("ownerId")||
            saved.deaths!=original.Original.Deaths||!IsCancellationOwnerPrefix(original.BeforeActor,original.AfterActor,prefix)||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,CancellationReceipt(original.Operation),out float phase,out bool present)||!present||phase!=1f||
            !ReadCancellationAccepted(original,original.Accepted)||!OpenRestartSource(original.Original,original.Source,out var history))return false;
        using(history)
        {
            if(!RebirthRemoteResourcePlacedStationReference.TryRead(history.Source.Copy().Element("ownerAction")?.Element("original")?.Value,out var reference)||
                !ObserveColdAccepted(history,reference)||!TryReadCompletion(history.Source,history.Artifact,reference,null,out var proof,false,history.Allocation)||proof!=original.Proof)return false;
            var origin=CancellationOrigin.Capture(actor,history.Source,history.Artifact,reference,proof,header);if(origin?.Current!=true)return false;
            CancellationLoaded loaded=original.LoadOriginal?.Receipt1Before==true?original.LoadOriginal.Loaded:null;
            var tile=loaded?.Installed()==true?loaded.Tile:original.Station;var chunk=loaded?.Installed()==true?loaded.Chunk:original.Chunk;
            if(tile==null||chunk==null||!ReferenceEquals(actor.world.GetTileEntity(tile.ToWorldPos()),tile)||!ReferenceEquals(tile.GetChunk(),chunk)||chunk.InProgressUnloading||
                !cancellationHostCustodies.TryGetValue(tile,out var custody)||!ReferenceEquals(custody,original.HostCustody)||custody.Closing||custody.Closed||
                !RebirthRemoteResourcePlacedStationCandidate.TrySerialize(tile,tile.Input,tile.Queue,tile.IsBurning,out var full,out _)||!full.SequenceEqual(original.BeforeTile))return false;
            string beforeText=ReadArchivedCancellation(original.Operation,original.Source,original.Original.Creation,"Before",original.Memory,RebirthCamperTransferJournal.Phase.Applied);
            if(beforeText==null||!ValidateCancellationSnapshot(RebirthCookingRemoteAdmission.ReadProtocolXml(beforeText,MaximumResultBytes),original.Operation,original.Source,original.Binding,original.Birth,
                original.Header,false,original.BeforeTile,original.BeforeActor,actor.entityId,original.Original.Deaths,tile.ToWorldPos(),null,out var before))return false;
            before.Canonical=beforeText;
            lock(custody.Gate)if(!HostCustodyRecord(custody,HostCustodyJournal(custody,false).Read(original.Operation),custody.Anchor,RebirthCamperTransferJournal.Phase.Prepared))return false;
            var oldActor=original.Actor;var oldPeer=original.Peer;var oldOrigin=original.Origin;var oldTile=original.Station;var oldChunk=original.Chunk;var oldSource=original.Receipt1Source;
            original.Actor=actor;original.Peer=cancellationNativeRequest.Peer;original.Origin=origin;original.Station=tile;original.Chunk=chunk;
            original.Receipt1BeforeEvidence=before.Canonical;original.Receipt1Source=new CancellationReceipt1Source(original,loaded);original.Loaded=loaded;
            if(original.Current()&&CancellationHostCustodyCurrent(original))return true;
            original.Actor=oldActor;original.Peer=oldPeer;original.Origin=oldOrigin;original.Station=oldTile;original.Chunk=oldChunk;original.Receipt1Source=oldSource;return false;
        }
    }
    private static bool RegisterReceipt1BeforeLoad(CancellationServer original)
    {
        if(original.LoadOriginal!=null)return original.LoadOriginal.Receipt1Before||original.QueueAttempted;
        if(!original.Current()||original.QueueAttempted||!ReadCancellationSnapshot(original,false,out var saved)||
            original.Actor.world.ChunkCache?.ChunkProvider is not ChunkProviderGenerateWorld provider||provider.m_RegionFileManager is not RegionFileManager manager||
            manager.snapshotUtil is not ChunkSnapshotUtil util||!ReferenceEquals(util.regionFileAccess,manager.regionFileAccess)||!ReferenceEquals(util.chunkReader.regionFileAccess,manager.regionFileAccess))return false;
        long charge=checked(16L*(saved.Snapshot.Length+saved.Tile.Length+Encoding.UTF8.GetByteCount(original.Header))+65536);
        if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+charge)))return false;
        var token=new CancellationLoadOriginal{Original=original,Receipt1Before=true,World=original.Actor.world,WorldId=original.Actor.world.worldState.Guid,OldChunk=original.Chunk,OldTile=original.Station,
            Intent=original.Original,Memory=original.Memory,Operation=original.Operation,Source=original.Source,Binding=original.Binding,Birth=original.Birth,Recovery=Guid.NewGuid(),Provider=provider,Manager=manager,
            Util=util,Access=manager.regionFileAccess,Directory=Path.GetFullPath(manager.saveDirectory),Header=original.Header,Permission=original.Permission,Evidence=saved.Canonical,
            Snapshot=saved.Snapshot,Tile=saved.Tile,Offset=saved.Offset,X=original.Chunk.X,Z=original.Chunk.Z,Retirement=new CancellationLoadRetirement()};
        if(!original.SnapshotBudget.AttachLoadRetirement(token.Retirement))return false;
        lock(cancellationLoadGate)cancellationLoads.Add(token);original.LoadOriginal=token;return true;
    }
    private static bool ResumeReceipt1Server(EntityPlayer actor,string header,CancellationRestartArchive archive,int ownerPhase=1)
    {
        if(cancellationNativeRequest?.Current!=true||!ReferenceEquals(cancellationNativeRequest.Actor,actor)||!archive.BeforeRestart||archive.BeforeUncertain||archive.Header!=header||archive.Memory?.Current!=true||!ServerScope(actor,archive.Original)||actor.entityId!=archive.NativeOwner||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RebirthCamperEnergySavedOwner.TryRead(identity,out var prefix,out var saved)||
            ownerPhase!=1&&ownerPhase!=2||saved.id!=archive.NativeOwner||saved.deaths!=archive.Original.Deaths||
            !(ownerPhase==2?archive.After.Matches(prefix):IsCancellationOwnerPrefix(archive.Before,archive.After,prefix))||
            !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,CancellationReceipt(archive.Operation),out float phase,out bool present)||!present||phase!=ownerPhase||
            archive.Load?.Loaded is not CancellationLoaded loaded||!loaded.Installed()||!ValidateLoadedRestartImage(archive,loaded)||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,loaded.Tile,out string birth)||birth!=archive.Birth||!OpenRestartSource(archive.Original,archive.Source,out var history))return false;
        using(history)
        {
            if(!RebirthRemoteResourcePlacedStationReference.TryRead(history.Source.Copy().Element("ownerAction")?.Element("original")?.Value,out var reference)||
                history.Artifact.Original.Digest!=archive.Original.Digest||reference.Source!=archive.Source||reference.Binding!=archive.Binding||
                !ObserveColdAccepted(history,reference)||!TryReadCompletion(history.Source,history.Artifact,reference,null,out var proof,false,history.Allocation)||proof!=archive.Proof)return false;
            var origin=CancellationOrigin.Capture(actor,history.Source,history.Artifact,reference,archive.Proof,archive.Header);if(origin?.Current!=true)return false;
            var native=RebirthRemoteResourcePlacedStationNativeReader.TryRead(archive.SavedNative.Tile,out var before)?before:null;
            var plan=RebirthCookingRemoteAdmission.ReadProtocolXml(archive.Permission,MaximumResultBytes);
            if(native==null||!RebirthRemoteResourcePlacedStationNativeReader.TryRead(archive.NativeAfter,out var after)||
                !RebirthRemoteResourcePlacedStationQueue.TryDecode(plan.Element("afterQueue").Value,out var afterQueue))return false;
            var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);if(peer==null)return false;
            var original=new CancellationServer{Actor=actor,Peer=peer,Original=archive.Original,Origin=origin,Operation=archive.Operation,Source=archive.Source,Header=archive.Header,
                Permission=archive.Permission,Binding=archive.Binding,Birth=archive.Birth,Proof=archive.Proof,Station=loaded.Tile,Chunk=loaded.Chunk,Directory=GameIO.GetSaveGameDir(),
                Memory=archive.Memory,BeforeSaved=true,BeforeActor=archive.Before,AfterActor=archive.After,BeforeTile=Convert.FromBase64String(plan.Element("beforeTile").Value),AfterTile=archive.NativeAfter,
                BeforeQueue=native.Queue,AfterQueue=afterQueue,AfterInput=after.Input,Slot=(int)RebirthCookingRemoteAdmission.ReadProtocolXml(archive.Header,MaximumResultBytes).Attribute("slot"),
                Marker=CancellationMarkerImage(archive.Operation,archive.Original.Operation,archive.Source,archive.Binding,archive.Birth,archive.Header),
                HostCustody=archive.HostCustody,LoadOriginal=archive.Load,Loaded=loaded,Receipt1BeforeEvidence=archive.BeforeEvidence};
            var accepted=RebirthCookingRemoteAdmission.ReadProtocolXml(archive.AcceptedEvidence,MaximumResultBytes);
            original.Accepted=new AcceptedUpload(Convert.FromBase64String(accepted.Element("before").Value),Convert.FromBase64String(accepted.Element("raw").Value),Convert.FromBase64String(accepted.Element("after").Value),(ulong)accepted.Attribute("tick"));
            original.Receipt1Source=new CancellationReceipt1Source(original,loaded);
            if(!original.Current()||!CancellationHostCustodyCurrent(original))return false;
            if(ownerPhase==2)
            {original.BeforeTwoPublication=CancellationBeforeTwoPublication.Capture(original,loaded);if(original.BeforeTwoPublication==null)return false;}
            long capacity=6L*(RebirthStationSnapshotEvidence.MaximumSnapshotBytes+RebirthStationRegionPayload.MaximumPayloadBytes);
            if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+capacity)))return false;
            original.SnapshotBudget=new CancellationSnapshotBudget();if(!original.SnapshotBudget.AttachHostCustody(original.HostCustody))return false;
            int growth;using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {writer.Write(CancellationMarkerKey(original));writer.Write(ulong.MaxValue);writer.Write(false);writer.Write((ushort)original.Marker.Length);writer.Write(original.Marker);writer.Flush();growth=checked((int)stream.Length);}
            if(!RebirthRemoteResourcePlacedStationCandidate.TryMeasureChunk(archive.SavedNative.Snapshot,archive.SavedNative.Offset,archive.SavedNative.Tile,archive.NativeAfter,growth,out int raw,out _,out long bound))return false;
            long evidence=checked(4L*((raw+bound+1024*1024+2)/3)+RebirthRemoteResourceClientOriginal.ActorImage(archive.After).Length+65536);
            original.AfterEvidenceBudget=checked(32*evidence+65536);original.NativeLoadWorkspace=checked(16L*(raw+archive.NativeAfter.Length+Encoding.UTF8.GetByteCount(header))+65536);
            if(!QualifyCancellationFuture(original))return false;original.RefundQualified=true;
            ObserveCancellationScenarioLoad(original.Operation,ownerPhase==2?"actualNativeBeforeLoad2":"actualNativeBeforeLoad",loaded);
            archive.Load.Retirement.Snapshots=original.SnapshotBudget;
            archive.Load.Original=original;archive.Load.Restart=null;archive.Load.Receipt1Before=true;
            cancellationServers.Add(original.Operation,original);restartCancellations.Remove(archive.Operation);
            archive.Memory=null;archive.HostCustody=null;archive.Load=null;archive.Dispose();return true;
        }
    }
}
