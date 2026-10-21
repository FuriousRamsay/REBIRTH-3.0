using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    // Explicit remote-native source. It is never a UIO registration or a
    // locally fabricated NativeAddOriginal. Only actual authenticated Prepare
    // admission can create this same-process original.
    internal sealed partial class NativeHostOriginal:IDisposable
    {
        internal readonly Guid Operation,Nonce,Birth,CustodyBirth;
        internal readonly World World;internal readonly TileEntityWorkstation Station;
        internal readonly EntityPlayer Viewer;internal readonly ClientInfo Sender;
        private readonly INetConnection[] originalConnections;
        internal readonly RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;
        internal readonly Recipe Recipe;internal readonly int Index,BeforeCount,ViewerDeaths;
        internal readonly string ViewerKey,ViewerCreation,Save,WorldGuid,Path;
        internal XElement Before;internal byte[] Image,NativeBefore;
        internal readonly int BeforeOffset;internal readonly string SourceHash;
        internal RebirthProductiveEarnedObservationBudget Memory;
        private IDisposable ownerRetention,bodyRetention;
        private int references=1;internal bool Closed;internal bool BeforeSaved {get;private set;}
        private RebirthRemoteResourceNativePublication.Watch beforeWatch;
        private XElement savedBeforeCandidate;
        private byte[] savedBeforeImage;
        private long admissionReadFence;
        private NativeHostOriginal(EntityPlayer viewer,ClientInfo sender,TileEntityWorkstation station,
            RebirthProductiveEarnedUiQueueOwner.Witness owner,Guid operation,Guid nonce,string viewerKey,string viewerCreation,
            XElement before,byte[] image,string path,byte[] native,int offset,RebirthProductiveEarnedObservationBudget memory,IDisposable retention,IDisposable body)
        {
            Viewer=viewer;Sender=sender;originalConnections=sender.netConnection?.Cast<INetConnection>().ToArray();World=viewer.world;Station=station;EarlierOwner=owner;Operation=operation;Nonce=nonce;Birth=owner.Birth;CustodyBirth=owner.CurrentCustodyBirth;
            ViewerKey=viewerKey;ViewerCreation=viewerCreation;ViewerDeaths=viewer.Died;Save=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir());
            WorldGuid=RebirthWorkstationCompletionOutbox.CurrentGuid(World);Before=before;Image=image;Path=path;NativeBefore=native;BeforeOffset=offset;
            Index=(int)before.Attribute("slot");BeforeCount=(int)before.Attribute("beforeCount");Recipe=station.Queue[Index].Recipe;
            Memory=memory;ownerRetention=retention;bodyRetention=body;SourceHash=RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(image));
            if(!RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out admissionReadFence))throw new InvalidDataException();
        }
        internal bool SourceCurrent()=>!Closed&&ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&
            ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==Save&&
            RebirthWorkstationCompletionOutbox.CurrentGuid(World)==WorldGuid&&EarlierOwner.SourceCurrent()&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(World,Station,out var birth)&&birth==CustodyBirth.ToString("N")&&
            RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Image)&&RestartNativeIdentityCurrent();
        internal bool CurrentPeer()=>SourceCurrent()&&ReferenceEquals(World.GetEntity(Viewer.entityId),Viewer)&&!Viewer.IsDead()&&Viewer.Died==ViewerDeaths&&
            RebirthCamperCharacterAccess.CurrentCreation(Viewer)==ViewerCreation&&Sender.loginDone&&!Sender.disconnecting&&Sender.entityId==Viewer.entityId&&
            originalConnections!=null&&Sender.netConnection!=null&&Sender.netConnection.Length==originalConnections.Length&&
            !Sender.netConnection.Where((connection,index)=>!ReferenceEquals(connection,originalConnections[index])).Any()&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(Viewer.entityId),Sender)&&
            RebirthStablePlayerIdentity.TryFromClientInfo(Sender,out var identity)&&identity.StorageKey==ViewerKey;
        internal bool CurrentViewer()=>CurrentPeer()&&RebirthProductiveEarnedArtifact.RemoteAuthority(Viewer,Sender,Station,ViewerCreation);
        private bool CurrentBefore()
        {
            if(!CurrentViewer()||!AdmissionCurrent())return false;
            using(var callbacks=Memory.TryEnterCallbacks())return callbacks!=null&&
                RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,NativeBefore,BeforeOffset);
        }
        internal bool AdmissionCurrent()=>SourceCurrent()&&RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out var fence)&&fence==admissionReadFence;
        internal void ObserveAcceptedAdmissionFence(RemoteNativeEvent actual)
        {
            if(!ReferenceEquals(actual.Source,this)||!actual.IsActualAcceptedRead()||
                !RebirthProductiveEarnedUiDeferredTransport.TryReadNativeObservationFence(World,Station,out var fence))return;
            admissionReadFence=fence;
        }
        internal bool stationWorkReleased;
        internal bool TryReleaseStationWorkHold(RebirthProductiveEarnedUiDeferredTransport.NativeStationPublication proof)
        {
            if(proof==null||!ReferenceEquals(proof.Source.NativeOriginal,this)||!ReferenceEquals(proof.Source.NativePublication,proof)||
                proof.Phase!=3||proof.Source.NativePhase!=3||!proof.TrySeal())return false;
            stationWorkReleased=true;return true;
        }
        internal bool TrySealBefore()
        {
            if(!CurrentBefore())return false;
            if(BeforeSaved)return RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path+".saved",savedBeforeImage);
            if(savedBeforeCandidate==null&&beforeWatch==null)
            {
                if(!RebirthRemoteResourceNativeBinding.TryResolve(World,RemoteResourceIdentity.Workstation(Station.ToWorldPos()),
                    RemoteResourceSourceKind.WorkstationOutput,CustodyBirth.ToString("N"),out var binding))return false;
                beforeWatch=RebirthRemoteResourceNativePublication.BeginWithBudget(binding,CurrentBefore,Memory);
            }
            if(savedBeforeCandidate==null)
            {
            if(beforeWatch==null||!beforeWatch.TryReadPublishedTileDescriptor(out var published,out long offset,out int length,out string digest)||!CurrentBefore())return false;
            savedBeforeCandidate=new XElement("workstationCompletionIntent",new XAttribute("operation",Operation.ToString("N")),new XAttribute("world",WorldGuid),
                new XElement("before",new XElement("remoteNativeBeforeSaved",new XAttribute("source",SourceHash),new XAttribute("nonce",Nonce.ToString("N")),
                    new XAttribute("nativePath",published.Path),new XAttribute("payload",published.PayloadDigest),new XAttribute("offset",offset),
                    new XAttribute("length",length),new XAttribute("tile",digest))));
            }
            if(!RebirthWorkstationCompletionPublication.Write(Path+".saved",savedBeforeCandidate))return false;
            savedBeforeImage=RebirthWorkstationCompletionPublication.CopyPublishedBytes(Path+".saved");if(savedBeforeImage==null)return false;
            BeforeSaved=true;beforeWatch?.Dispose();beforeWatch=null;return true;
        }
        internal IDisposable Retain()
        {if(!SourceCurrent())return null;references++;return new Lease(this);}
        private sealed class Lease:IDisposable
        {private NativeHostOriginal source;internal Lease(NativeHostOriginal value){source=value;}public void Dispose(){System.Threading.Interlocked.Exchange(ref source,null)?.Release();}}
        private void Release()
        {
            if(--references!=0)return;ReleaseRemoteOutputRestart();ReleaseRemoteOwnerTerminal();beforeWatch?.Dispose();beforeWatch=null;ownerRetention?.Dispose();ownerRetention=null;
            ReleaseNativeContinuations();
            savedBeforeCandidate=Before=null;Image=NativeBefore=savedBeforeImage=null;
            bodyRetention?.Dispose();bodyRetention=null;Memory?.Dispose();Memory=null;
        }
        public void Dispose(){if(Closed)return;Closed=true;RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Release(this);if(nativeHostOriginals.TryGetValue(Operation,out var source)&&ReferenceEquals(source,this))nativeHostOriginals.Remove(Operation);Release();}
        internal static NativeHostOriginal ObservePrepare(EntityPlayer viewer,ClientInfo sender,string viewerKey,string creation,Guid operation,
            TileEntityWorkstation station,RebirthProductiveEarnedUiQueueOwner.Witness owner,XElement intent)
        {
            NativeHostOriginal result=null;RebirthProductiveEarnedObservationBudget memory=null;IDisposable retained=null,body=null,workspace=null;
            try
            {
                var before=ActiveBefore(intent);
                if(viewer==null||sender==null||station==null||HoldsRemoteOutputHostOriginal(station)||owner?.Current()!=true||!owner.SourcePublished()||!ReferenceEquals(viewer.world,owner.World)||
                    !ReferenceEquals(owner.Station,station)||before==null||!CorroborateQueuedOwner(viewer.world,owner)||operation==Guid.Empty||
                    sender.entityId!=viewer.entityId||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients?.ForEntityId(viewer.entityId),sender)||
                    !RebirthProductiveEarnedArtifact.RemoteAuthority(viewer,sender,station,creation))return null;
                long measured=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(before)+65536;
                if(measured>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*measured),out workspace))return null;
                string path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",operation.ToString("N")+".xml");
                string beforeHash=RebirthWorkstationCompletionOriginal.Hash(before.ToString(SaveOptions.DisableFormatting));
                if(!nativePrepareSeeds.TryGetValue(operation,out var seed))
                {
                    // A cold primary has no same-process original/nonce and
                    // cannot be adopted from its parsed contents.
                    if(nativePrepareSeeds.Count>=4||File.Exists(path)||File.Exists(path+".tmp"))return null;
                    var lease=owner.RetainPublishedSource();if(lease==null)return null;
                    seed=new NativePrepareSeed{Operation=operation,Nonce=Guid.NewGuid(),World=viewer.world,Station=station,Viewer=viewer,Sender=sender,
                        Owner=owner,BeforeHash=beforeHash,Retention=lease};nativePrepareSeeds.Add(operation,seed);
                }
                if(!ReferenceEquals(seed.World,viewer.world)||!ReferenceEquals(seed.Station,station)||!ReferenceEquals(seed.Viewer,viewer)||
                    !ReferenceEquals(seed.Sender,sender)||!ReferenceEquals(seed.Owner,owner)||seed.BeforeHash!=beforeHash||!owner.SourceCurrent())return null;
                Guid nonce=seed.Nonce;
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),
                    new XAttribute("world",RebirthWorkstationCompletionOutbox.CurrentGuid(viewer.world)),new XElement("before",new XElement("remoteNativeOriginal",
                        new XAttribute("nonce",nonce.ToString("N")),new XAttribute("birth",owner.Birth.ToString("N")),new XAttribute("viewer",viewerKey),
                        new XAttribute("creation",creation),new XAttribute("deaths",viewer.Died),new XAttribute("ownerPurpose",RebirthProductiveEarnedUiQueueOwner.Export(owner)),
                        new XElement(before))));
                if(!RebirthWorkstationCompletionPublication.Write(path,record))return null;
                byte[] image=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);
                if(image==null||image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return null;
                memory=RebirthProductiveEarnedObservationBudget.TryOpenNativeCompletion(viewer.world,owner.CurrentCustodyBirth,operation,image);if(memory==null)return null;
                var native=Convert.FromBase64String((string)before.Element("nativeBefore"));int offset=(int)before.Attribute("nativeElapsedOffset");
                if(native.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*native.Length+65536),out body))return null;
                using(var callbacks=memory.TryEnterCallbacks())if(callbacks==null||!RebirthProductiveEarnedNativeBody.MatchesCurrent(station,native,offset))return null;
                retained=seed.Retention;if(retained==null)return null;seed.Retention=null;
                result=new NativeHostOriginal(viewer,sender,station,owner,operation,nonce,viewerKey,creation,new XElement(before),image,path,native,offset,memory,retained,body);
                memory=null;retained=body=null;nativePrepareSeeds.Remove(operation);return result;
            }
            catch{return null;}
            finally{workspace?.Dispose();memory?.Dispose();retained?.Dispose();body?.Dispose();}
        }
    }
    private sealed class NativePrepareSeed
    {internal Guid Operation,Nonce;internal World World;internal TileEntityWorkstation Station;internal EntityPlayer Viewer;internal ClientInfo Sender;
     internal RebirthProductiveEarnedUiQueueOwner.Witness Owner;internal string BeforeHash;internal IDisposable Retention;}
    private static readonly Dictionary<Guid,NativePrepareSeed> nativePrepareSeeds=new Dictionary<Guid,NativePrepareSeed>();
    private static readonly Dictionary<Guid,NativeHostOriginal> nativeHostOriginals=new Dictionary<Guid,NativeHostOriginal>();
    private static bool TryPrepareNativeRemoteSource(EntityPlayer viewer,ClientInfo sender,string viewerKey,string creation,Guid operation,
        TileEntityWorkstation station,RebirthProductiveEarnedUiQueueOwner.Witness owner,XElement intent,out NativeHostOriginal source)
    {
        source=null;
        // Both native branches require this real saved Before and its actual
        // authenticated nonce. Their later effect producers remain disjoint:
        // same-owner GiveExp never manufactures a deferred Add receipt.
        if(!nativeHostOriginals.TryGetValue(operation,out source))
        {
            if(nativeHostOriginals.Count>=4)return false;
            source=NativeHostOriginal.ObservePrepare(viewer,sender,viewerKey,creation,operation,station,owner,intent);
            if(source==null)return false;nativeHostOriginals.Add(operation,source);
        }
        if(!ReferenceEquals(source.Sender,sender)||!ReferenceEquals(source.Viewer,viewer)||!ReferenceEquals(source.Station,station)||
            !ReferenceEquals(source.EarlierOwner,owner))return false;
        return XNode.DeepEquals(source.Before,ActiveBefore(intent))?
            source.TrySealBefore()&&RetireRemoteCachesAtSavedSuccessor(source)&&
                RebirthProductiveEarnedUiRemote.AcceptSavedNativeQueueBefore(source):source.TrySealContinuation(ActiveBefore(intent));
    }
    internal static void SweepNativeHostWorld(World current)
    {
        if(!ThreadManager.IsMainThread())return;
        SweepPendingNativeWorld(current);
        SweepNativeEnvelopeWorld(current);
        RebirthProductiveEarnedUiDeferredTransport.RetireSameOwnerObservationWorld(current);
        foreach(var source in nativeHostOriginals.Values.Where(value=>!ReferenceEquals(value.World,current)).ToArray())source.Dispose();
        foreach(var pair in nativePrepareSeeds.Where(value=>!ReferenceEquals(value.Value.World,current)).ToArray())
        {nativePrepareSeeds.Remove(pair.Key);pair.Value.Retention?.Dispose();pair.Value.Retention=null;}
    }
    internal static bool TryFindNativeHostOriginal(Guid operation,World world,TileEntityWorkstation station,out NativeHostOriginal original)
    {original=null;return nativeHostOriginals.TryGetValue(operation,out original)&&ReferenceEquals(original.World,world)&&ReferenceEquals(original.Station,station)&&original.SourceCurrent();}
    internal static bool CanTickRemoteNativeCompletion(TileEntityWorkstation station)
    {
        var world=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||station==null)return true;
        return !HoldsRemoteOutputHostOriginal(station)&&!remoteOutputHostSources.Any(value=>value.Holds(station))&&CanTickLocalNativeBefore(station)&&CanTickCachedPrimary(station)&&!nativeHostOriginals.Values.Any(value=>!value.Closed&&!value.stationWorkReleased&&ReferenceEquals(value.World,world)&&ReferenceEquals(value.Station,station)&&
            ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,station,out var birth)&&birth==value.CustodyBirth.ToString("N"));
    }
}
