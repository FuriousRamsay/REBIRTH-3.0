using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;

// Host half only. Custom requests never apply row/tool images. Only an actual
// authenticated native TE read may establish the After observation; native
// same-snapshot publication is required separately before Published is returned.
internal static partial class RebirthProductiveEarnedRemoteHost
{
    internal sealed class Context
    {
        internal World World;internal EntityPlayer Actor;internal ClientInfo Sender;
        internal TileEntityWorkstation Station;internal RebirthProductiveEarnedArtifact.Prepared Artifact;
        internal Guid Operation,Nonce;internal string Owner,Creation,Directory,HeaderPath,WorldKey;
        internal int Deaths;internal byte[] Header,Image;
        internal RebirthProductiveEarnedPublication.Observation Observation;
        internal RebirthProductiveEarnedObservationBudget Memory;
        internal bool Accepted,BeforeAccepted,BeforePublished,AfterPublished,UiMarkerRetired;
        internal RebirthProductiveEarnedNativeOutputPlan.Description NativeOutput;
        internal byte[] NativeOutputRecord;internal IDisposable NativeOutputRetention;
        internal bool NativeOutputAccepted,NativeOutputSaved;
        internal RebirthRemoteResourceNativePublication.Watch NativeOutputWatch;
        internal RebirthProductiveEarnedNativePendingPlan.Description NativePending;
        internal byte[] NativePendingRecord;internal IDisposable NativePendingRetention;
        internal bool NativePendingAccepted,NativePendingSaved;
        internal Context NativeChild;
        internal NativeTools Tools;
        internal RebirthProductiveEarnedCoordinator.SavedPendingToolsObservation ViewerToolsSource;
        internal byte[] ViewerToolsRecord;
        internal IDisposable ViewerToolsRetention;
        internal bool ViewerToolsOnly,ViewerToolsReleased;
        internal bool NativeMarkerReleased;
        internal RebirthRemoteResourceNativePublication.Watch NativePendingWatch;
        internal ulong BeforeAcceptedElapsed;
        internal RebirthRemoteResourceNativePublication.Watch BeforeWatch;
        internal RebirthProductiveEarnedObservationBudget.Observation BeforeAllocation;
        internal bool BeforeCurrent()
        {
            using(var allocation=Memory?.TryEnterCallbacks())
            {if(allocation==null||!Current())return false;var frame=RebirthProductiveEarnedRowProof.Decode(Image);
            return RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,frame.BeforeNative,frame.BeforeElapsedOffset);}
        }
        internal bool AfterCurrent()
        {
            using(var allocation=Memory?.TryEnterCallbacks())
            {if(allocation==null||!Current())return false;var frame=RebirthProductiveEarnedRowProof.Decode(Image);
            return RebirthProductiveEarnedNativeBody.MatchesCurrent(Station,frame.AfterNative,frame.AfterElapsedOffset);}
        }
        internal bool Current()
        {
            try{return ReferenceEquals(World,GameManager.Instance?.World)&&Actor.Died==Deaths&&
                Directory==Path.GetFullPath(GameIO.GetSaveGameDir())&&WorldKey==RebirthProductiveEarnedArtifact.WorldIdentity(World)&&
                RebirthProductiveEarnedArtifact.RemoteAuthority(Actor,Sender,Station,Creation)&&(!ViewerToolsOnly||ViewerToolsReleased||ViewerToolsSource?.RetainedContext()==true)&&
                (Artifact?.Current()==true||NativeParentCustodyCurrent(this))&&
                Safe(HeaderPath)&&File.Exists(HeaderPath)&&!File.Exists(HeaderPath+".tmp")&&Read(HeaderPath).SequenceEqual(Header);}
            catch{return false;}
        }
    }
    private static readonly Dictionary<Guid,Context> pending=new Dictionary<Guid,Context>();
    [ThreadStatic] private static Context uploading;
    private static bool installed;
    private static bool uploadObserverInstalled;
    internal static void InstallNativeUploadObserver()
    {
        if(uploadObserverInstalled)return;
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.earned.native.upload"),typeof(NativeUpload));
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.earned.native.upload"),typeof(NativeRead));
        uploadObserverInstalled=true;
    }
    private static World observedWorld;
    private static string observedSave;
    internal static void ObserveNativeWorldBoundary()
    {if(ThreadManager.IsMainThread())ObserveWorld();}
    private static void ObserveWorld()
    {
        PruneReleasedViewerTools();
        var world=GameManager.Instance?.World;string save=world==null?null:Path.GetFullPath(GameIO.GetSaveGameDir());
        if(ReferenceEquals(world,observedWorld)&&save==observedSave)return;
        foreach(var old in pending.Values){old.Tools?.Dispose();old.Observation?.Dispose();old.BeforeWatch?.Dispose();old.BeforeAllocation?.Dispose();old.NativeOutputWatch?.Dispose();old.NativeOutputRetention?.Dispose();old.NativePendingWatch?.Dispose();old.NativePendingRetention?.Dispose();old.NativeChild?.Memory?.Dispose();old.Memory?.Dispose();}
        foreach(var old in pending.Values){old.ViewerToolsSource?.Dispose();old.ViewerToolsRetention?.Dispose();}
        pending.Clear();observedWorld=world;observedSave=save;
        // Only transient observers retire. Original artifacts/headers/native
        // markers stay in their own captured save; no terminal is inferred.
    }
    internal static void Install()
    {
        if(installed)return;
        InstallNativeUploadObserver();
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.productive.earned.native.upload"),typeof(HeldTick));
        RebirthProductiveEarnedNativeBody.Install();installed=true;
    }
    internal static bool IsAcceptedUpload(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
        =>uploading!=null&&uploading.Accepted&&ReferenceEquals(uploading.Station,station)&&ReferenceEquals(uploading.Artifact,artifact)&&uploading.Current();
    internal static Func<bool> AcceptedCurrent(TileEntityWorkstation station,RebirthProductiveEarnedArtifact.Prepared artifact)
        =>IsAcceptedUpload(station,artifact)?new Func<bool>(uploading.AfterCurrent):null;
    internal static bool BlocksQueue(TileEntityWorkstation station)
        =>RebirthProductiveEarnedUiCoordinator.Blocks(station)||RebirthProductiveEarnedCoordinator.HoldsAdoptedNativeTick(station)||(RebirthProductiveEarnedCoordinator.Enabled||RebirthProductiveEarnedUiOriginal.Enabled)&&(EarlyBlocks(station)||pending.Values.Any(value=>ReferenceEquals(value.World,GameManager.Instance?.World)&&ReferenceEquals(value.Station,station)));
    internal static bool TryDescribe(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,out Guid birth)
    {
        birth=Guid.Empty;
        if(!RebirthProductiveEarnedCoordinator.Enabled&&!RebirthProductiveEarnedUiOriginal.Enabled)return false;
        ObserveWorld();
        if(RebirthProductiveEarnedCoordinator.HoldsAdoptedNativeTick(station)||!RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation))return false;
        RebirthLogisticsEndpointIdentity.Install();
        if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out var text))
        {
            using(var admission=RebirthLogisticsEndpointIdentity.Begin(actor.world,station,()=>
                RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation)))
                if(admission==null||!admission.Current())return false;
            if(!RebirthLogisticsEndpointIdentity.TryGetIncarnation(actor.world,station,out text))return false;
        }
        return Guid.TryParseExact(text,"N",out birth)&&birth!=Guid.Empty;
    }
    // Called only after packet-native sender validation. The actual Sender object
    // is rederived against the connection/actor/native exclusive lease here too.
    internal static bool Prepare(EntityPlayer actor,ClientInfo sender,TileEntityWorkstation station,string creation,byte[] image,Guid acceptedNonce,RebirthProductiveEarnedObservationBudget.Decoder decoder,
        out Guid nonce,out string digest)
    {
        nonce=Guid.Empty;digest=null;
        try
        {
            if(decoder?.IsCurrent!=true||RebirthProductiveEarnedCoordinator.HoldsAdoptedNativeTick(station)||!RebirthProductiveEarnedCoordinator.Enabled&&!RebirthProductiveEarnedUiRemote.IsPreparing(actor,sender,station,image)||!installed||!RebirthProductiveEarnedArtifact.RemoteAuthority(actor,sender,station,creation)||
                image==null||image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return false;
            ObserveWorld();
            var frame=RebirthProductiveEarnedRowProof.Decode(image);
            if(!frame.HasNativeBodies||!RebirthProductiveEarnedRowProof.TryDecodeNative(frame,out _,out _,out _,out _)||
                frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(actor.world)||
                !TryDescribe(actor,sender,station,creation,out var birth)||frame.Birth!=birth)return false;
            digest=Digest(image);
            if(!MatchesEarly(actor,station,creation,frame,image,out var heldNonce)||acceptedNonce==Guid.Empty||acceptedNonce!=heldNonce)return false;
            if(pending.TryGetValue(frame.Operation,out var current))
            {
                if(!current.Current()||current.Owner!=identity.StorageKey||current.Creation!=creation||current.Deaths!=actor.Died||
                    !ReferenceEquals(current.Station,station)||!current.Image.SequenceEqual(image))return false;
                nonce=current.Nonce;return true; // Original status only; never host work invocation.
            }
            if(pending.Count>=4||pending.Values.Any(value=>ReferenceEquals(value.World,actor.world)&&ReferenceEquals(value.Station,station)))return false;
            string directory=Path.GetFullPath(GameIO.GetSaveGameDir());
            string path=Path.Combine(directory,"RebirthProductiveEarned","remote",identity.StorageKey,frame.Operation.ToString("N")+".header");
            if(!Safe(path)||File.Exists(path+".tmp"))return false;
            byte[] header;Guid originalNonce;
            bool existing=File.Exists(path);
            if(existing)
            {
                header=Read(path);
                if(!TryReadHeader(header,identity.StorageKey,creation,actor.Died,station,frame,digest,out originalNonce)||originalNonce!=heldNonce)return false;
            }
            else
            {
                originalNonce=heldNonce;header=Header(identity.StorageKey,creation,actor.Died,station,frame,digest,originalNonce);
            }
            RebirthProductiveEarnedArtifact.Prepared artifact;
            if(existing)
            {
                if(!RebirthProductiveEarnedArtifact.TryReadPrepared(station,frame,out artifact))return false;
            }
            else
            {
                if(!RebirthProductiveEarnedArtifact.TryPrepareRemote(station,frame,actor,sender,creation,out artifact))return false;
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
                if(!Safe(path)||File.Exists(path)||File.Exists(path+".tmp"))return false;
                using(var stream=new FileStream(path+".tmp",FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {stream.Write(header,0,header.Length);stream.Flush(true);}
                if(!Read(path+".tmp").SequenceEqual(header))return false;
                File.Move(path+".tmp",path);
                if(!Read(path).SequenceEqual(header))return false;
            }
            var memory=RebirthProductiveEarnedObservationBudget.TryOpen(artifact,frame);
            if(memory==null)return false; // Before retaining the second full artifact image.
            var context=new Context{Memory=memory,World=actor.world,Actor=actor,Sender=sender,Station=station,Artifact=artifact,Operation=frame.Operation,
                Nonce=originalNonce,Owner=identity.StorageKey,Creation=creation,Deaths=actor.Died,Directory=directory,
                HeaderPath=path,Header=header,Image=(byte[])image.Clone(),WorldKey=frame.World};
            if(!context.Current()){memory.Dispose();return false;}pending.Add(frame.Operation,context);nonce=originalNonce;
            // Cold read-only reobservation requires the already existing exact
            // native marker/full After. Never recreate a missing marker here.
            if(RebirthProductiveEarnedCoordinator.TryNativeMarker(station,out var marker)&&
                RebirthProductiveEarnedRowProof.MatchesMarker(marker,image)&&artifact.MatchesAfter(station))
            {
                context.Accepted=true;var previous=uploading;uploading=context;
                try{context.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(station,artifact);}
                finally{uploading=previous;}
            }
            return true;
        }
        catch{nonce=Guid.Empty;digest=null;return false;}
    }
    internal static bool TryOriginalBirth(EntityPlayer actor,ClientInfo sender,Guid operation,Guid nonce,string digest,out Guid birth)
    {
        birth=Guid.Empty;
        try
        {
            if(!pending.TryGetValue(operation,out var context)||context.Nonce!=nonce||!ReferenceEquals(context.Actor,actor)||
                !ReferenceEquals(context.Sender,sender)||Digest(context.Image)!=digest||!context.Current())return false;
            birth=RebirthProductiveEarnedRowProof.Decode(context.Image).Birth;return birth!=Guid.Empty;
        }
        catch{return false;}
    }
    internal static bool TryBeforePublished(EntityPlayer actor,ClientInfo sender,Guid operation,Guid nonce,string digest,out bool published)
    {
        published=false;
        if(!pending.TryGetValue(operation,out var context)||context.Nonce!=nonce||!ReferenceEquals(context.Actor,actor)||
            !ReferenceEquals(context.Sender,sender)||Digest(context.Image)!=digest||!context.Current())return false;
        published=TryRetainBeforePublication(context);return true;
    }
    internal static bool TryPublished(EntityPlayer actor,ClientInfo sender,Guid operation,Guid nonce,string digest,out bool published)
    {
        published=false;
        if(!pending.TryGetValue(operation,out var context)||context.Nonce!=nonce||!ReferenceEquals(context.Actor,actor)||
            !ReferenceEquals(context.Sender,sender)||Digest(context.Image)!=digest||!context.Current())return false;
        published=TryRetainAfterPublication(context);return true;
    }
    private static byte[] Header(string owner,string creation,int deaths,TileEntityWorkstation station,
        RebirthProductiveEarnedRowProof.Frame frame,string digest,Guid nonce)
    {
        if(Encoding.UTF8.GetByteCount(creation??"")>512||owner?.Length!=64||nonce==Guid.Empty)throw new InvalidDataException();
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,new UTF8Encoding(false,true)))
        {
            writer.Write((byte)1);writer.Write(owner);writer.Write(creation);writer.Write(deaths);writer.Write(frame.Operation.ToByteArray());
            writer.Write(frame.Birth.ToByteArray());writer.Write(nonce.ToByteArray());writer.Write(frame.World);writer.Write(digest);
            var p=station.ToWorldPos();writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);writer.Flush();
            if(stream.Length>2048)throw new InvalidDataException();return stream.ToArray();
        }
    }
    private static bool TryReadHeader(byte[] bytes,string owner,string creation,int deaths,TileEntityWorkstation station,
        RebirthProductiveEarnedRowProof.Frame frame,string digest,out Guid nonce)
    {
        nonce=Guid.Empty;
        try
        {
            using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,new UTF8Encoding(false,true)))
            {
                if(reader.ReadByte()!=1||RebirthSurvivorNetworkCodec.ReadBoundedString(reader,64)!=owner||
                    RebirthSurvivorNetworkCodec.ReadBoundedString(reader,512)!=creation||reader.ReadInt32()!=deaths||
                    new Guid(reader.ReadBytes(16))!=frame.Operation||new Guid(reader.ReadBytes(16))!=frame.Birth)return false;
                nonce=new Guid(reader.ReadBytes(16));
            }
            return nonce!=Guid.Empty&&Header(owner,creation,deaths,station,frame,digest,nonce).SequenceEqual(bytes);
        }
        catch{nonce=Guid.Empty;return false;}
    }
    private static byte[] Read(string path)
    {
        if(!Safe(path))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(stream.Length<1||stream.Length>2048)throw new InvalidDataException();var bytes=new byte[(int)stream.Length];int offset=0;
         while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read<1)throw new EndOfStreamException();offset+=read;}
         if(stream.ReadByte()!=-1)throw new InvalidDataException();return bytes;}
    }
    private static bool Safe(string path)
    {
        try{string p=Path.GetFullPath(path);while(!string.IsNullOrEmpty(p)){if((File.Exists(p)||System.IO.Directory.Exists(p))&&
            (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)return false;string parent=Path.GetDirectoryName(p);if(parent==p)break;p=parent;}return true;}
        catch{return false;}
    }
    private static string Digest(byte[] image){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(image)).Replace("-","").ToLowerInvariant();}
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.UpdateTick))]
    private static class HeldTick
    {
        private sealed class TickPurposes{internal object Milling,NativeTerminal;}
        private static bool Prefix(TileEntityWorkstation __instance,bool __runOriginal,out TickPurposes __state)
        {
            ObserveNativeTickHandoff(__instance,__runOriginal);
            bool allowed=!RebirthProductiveEarnedUiQueueOwner.HoldsInitialPublication(__instance)&&!RebirthRecyclerPlacedSettlement.HoldsStation(__instance)&&!RebirthCookingMillingPublication.Held(__instance)&&!BlocksQueue(__instance)&&!RebirthProductiveEarnedRemoteClient.HasPending(__instance)&&RebirthProductiveEarnedUiDeferredTransport.CanTickAuthorityNative(__instance)&&RebirthProductiveEarnedUiDeferredTransport.CanTickKnownNativeReceipt(__instance)&&RebirthWorkstationCompletionPipeline.CanTickRemoteNativeCompletion(__instance)&&RebirthProductiveEarnedUiCoordinator.CanTickQueueHandoff(__instance)&&RebirthProductiveEarnedUiRemote.CanTickHostQueueHandoff(__instance);
            __state=new TickPurposes();try{__state.Milling=RebirthCookingMillingPublication.EnterMillingStationTick(__instance,__runOriginal&&allowed);}catch{}
            try{__state.NativeTerminal=RebirthProductiveEarnedCoordinator.EnterNativeTerminalOuterTick(__instance,__runOriginal&&allowed);}catch{}
            return allowed;
        }
        private static void Postfix(TileEntityWorkstation __instance,bool __runOriginal,TickPurposes __state)
        {
            try{RebirthCookingMillingPublication.ObserveMillingStationTickReturned(__instance,__runOriginal,__state?.Milling);}catch{}
            try{RebirthProductiveEarnedCoordinator.ObserveNativeTerminalOuterTickReturned(__instance,__runOriginal,__state?.NativeTerminal);}catch{}
        }
        private static Exception Finalizer(Exception __exception,TickPurposes __state)
        {try{RebirthCookingMillingPublication.EndMillingStationTick(__state?.Milling);}catch{}try{RebirthProductiveEarnedCoordinator.EndNativeTerminalOuterTick(__state?.NativeTerminal);}catch{}return __exception;}
    }
    [HarmonyPatch(typeof(NetPackageTileEntity),nameof(NetPackageTileEntity.ProcessPackage))]
    private static class NativeUpload
    {
        private sealed class Purposes {internal Capture Earned;internal object Fetched,UiTerminal,QueueOwner,Deferred,QueueHandoff,UiBefore,RemoteClose,Completion,Recycler;}
        internal sealed class Capture {internal Context Original;internal TileEntityWorkstation Tile;internal NetPackageTileEntity Packet;internal int Block,Reads;internal long Length;internal bool Before,Terminal,Pending,Tools,ReadCompleted;internal ulong Elapsed;internal IDisposable Allocation;}
        [ThreadStatic] internal static Capture Reading;
        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(NetPackageTileEntity __instance,World _world,bool __runOriginal,out Purposes __state)
        {
            __state=null;
            bool recyclerAllowed=RebirthRecyclerPlacedSettlement.EnterRecyclerNativeUpload(__instance,_world,__runOriginal,out var recycler);
            if(recycler!=null||!recyclerAllowed){__state=new Purposes{Recycler=recycler};return recyclerAllowed;}
            if(!RebirthRecyclerPlacedSettlement.CanProcessNativeUpload(__instance,_world))return false;
            if(Reading!=null)return false; // Reentrant native reads cannot share one original event.
            bool completionAllowed=RebirthWorkstationCompletionPipeline.EnterCachedNativePacket(__instance,_world,__runOriginal,out var completion);
            if(completion!=null||!completionAllowed){__state=new Purposes{Completion=completion};return completionAllowed;}
            bool closeAllowed=RebirthProductiveEarnedUiDeferredTransport.EnterRemoteCloseNativeUpload(__instance,_world,__runOriginal,out var remoteClose);
            if(remoteClose!=null||!closeAllowed){__state=new Purposes{RemoteClose=remoteClose};return closeAllowed;}
            RebirthProductiveEarnedUiQueueOwner.EnterNativeUpload(__instance,_world,__runOriginal,out var queueOwner);
            bool queueAllowed=RebirthProductiveEarnedUiRemote.EnterQueueNativeUpload(__instance,_world,__runOriginal,out var queueHandoff);
            bool beforeAllowed=RebirthProductiveEarnedUiRemoteBefore.EnterNativeUpload(__instance,_world,__runOriginal&&queueAllowed,out var uiBefore);
            object deferred=null;
            bool deferredAllowed=queueAllowed&&beforeAllowed&&(queueHandoff!=null||uiBefore!=null||RebirthProductiveEarnedUiDeferredTransport.EnterNativeUpload(__instance,_world,__runOriginal,out deferred));
            object ui=null;
            bool uiAllowed=queueAllowed&&deferredAllowed&&(queueHandoff!=null||uiBefore!=null||deferred!=null||RebirthProductiveEarnedUiRemote.EnterNativeUpload(__instance,_world,__runOriginal,out ui));
            Capture earned=null;
            bool allowed=uiAllowed&&(queueHandoff!=null||uiBefore!=null||deferred!=null||ui!=null||EnterEarned(__instance,_world,__runOriginal,out earned));
            RebirthRemoteResourcePlacedStationOriginal.EnterNativeUpload(__instance,_world,__runOriginal&&allowed,out var fetched);
            if(earned!=null||fetched!=null||ui!=null||queueOwner!=null||deferred!=null||queueHandoff!=null||uiBefore!=null)__state=new Purposes{Earned=earned,Fetched=fetched,UiTerminal=ui,QueueOwner=queueOwner,Deferred=deferred,QueueHandoff=queueHandoff,UiBefore=uiBefore};
            if(allowed&&__runOriginal)Reading=earned;
            return allowed;
        }
        private static bool EnterEarned(NetPackageTileEntity __instance,World _world,bool __runOriginal,out Capture __state)
        {
            __state=null;
            if(!__runOriginal||_world?.IsRemote()!=false||!ReferenceEquals(_world,GameManager.Instance?.World)||
                !RebirthProductiveEarnedCoordinator.Enabled&&!RebirthProductiveEarnedUiOriginal.Enabled)return true;
            var earlyTile=_world.GetTileEntity(__instance.teWorldPos) as TileEntityWorkstation;
            if(earlyTile!=null&&RebirthProductiveEarnedCoordinator.HoldsAdoptedNativeTick(earlyTile)&&!pending.Values.Any(value=>value.ViewerToolsOnly&&ReferenceEquals(value.Station,earlyTile)))return false;
            if(earlyTile!=null&&EarlyBlocks(earlyTile)&&!pending.Values.Any(value=>ReferenceEquals(value.World,_world)&&ReferenceEquals(value.Station,earlyTile)))return false;
            foreach(var context in pending.Values)
            {
                if(!ReferenceEquals(context.World,_world)||!context.Station.ToWorldPos().Equals(__instance.teWorldPos))continue;
                if(__instance.Sender==null||!ReferenceEquals(context.Sender,__instance.Sender)||!context.Current()||
                    _world.GetBlock(__instance.teWorldPos).type!=__instance.teBlockId||__instance.ms==null||
                    __instance.ms.Length<1||__instance.ms.Length>RebirthProductiveEarnedNativeBody.MaximumBodyBytes)return false;
                var allocation=context.Memory?.TryEnterCallbacks();if(allocation==null)return false;
                try
                {
                var frame=RebirthProductiveEarnedRowProof.Decode(context.Image);
                byte[] raw=new byte[(int)__instance.ms.Length];
                lock(__instance.ms)
                {
                    long position=__instance.ms.Position;
                    try{__instance.ms.Position=0;int offset=0;while(offset<raw.Length){int read=__instance.ms.Read(raw,offset,raw.Length-offset);if(read<1)return false;offset+=read;}}
                    finally{__instance.ms.Position=position;}
                }
                bool before=RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,frame.BeforeElapsedOffset,frame.BeforeNative,frame.BeforeElapsedOffset);
                bool after=RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,frame.AfterElapsedOffset,frame.AfterNative,frame.AfterElapsedOffset);
                if(context.Tools!=null&&context.NativePending==null)
                {
                    var tools=context.Tools;
                    if(!TrySaveNativeToolOwner(context)||!RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,tools.Plan.AfterOffset,tools.Plan.After,tools.Plan.AfterOffset)||
                        !RebirthProductiveEarnedNativeBody.MatchesCurrent(context.Station,tools.Plan.Before,tools.Plan.BeforeOffset)&&!NativeToolsCurrent(context))return false;
                    __state=new Capture{Original=context,Tile=context.Station,Packet=__instance,Block=__instance.teBlockId,Length=raw.Length,
                        Tools=true,Elapsed=BitConverter.ToUInt64(raw,tools.Plan.AfterOffset)};
                    if(__state.Elapsed>GameTimer.Instance.ticks){__state=null;return false;}
                    __state.Allocation=allocation;allocation=null;return true;
                }
                if(context.ViewerToolsOnly)return false; // Only its independently admitted tools/PDS purpose may write.
                if(context.NativePending!=null)
                {
                    var plan=context.NativePending;
                    if(!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(context.HeaderPath+".nativePending",context.NativePendingRecord,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||
                        !RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,plan.AfterOffset,plan.After,plan.AfterOffset)||
                        !NativeOutputCurrent(context)&&!NativePendingCurrent(context))return false;
                    __state=new Capture{Original=context,Tile=context.Station,Packet=__instance,Block=__instance.teBlockId,Length=raw.Length,
                        Pending=true,Elapsed=BitConverter.ToUInt64(raw,plan.AfterOffset)};
                    if(__state.Elapsed>GameTimer.Instance.ticks){__state=null;return false;}
                    __state.Allocation=allocation;allocation=null;return true;
                }
                if(context.NativeOutput!=null)
                {
                    var terminal=context.NativeOutput;
                    if(!context.AfterPublished||!RebirthProductiveEarnedNativeSuccessorRecord.IsCurrent(context.HeaderPath+".nativeOutput",context.NativeOutputRecord)||
                        !RebirthProductiveEarnedNativeBody.SameExceptElapsed(raw,terminal.Offset,terminal.Native,terminal.Offset)||
                        !after&&!RebirthProductiveEarnedNativeBody.MatchesCurrent(context.Station,frame.AfterNative,frame.AfterElapsedOffset)&&!NativeOutputCurrent(context))return false;
                    __state=new Capture{Original=context,Tile=context.Station,Packet=__instance,Block=__instance.teBlockId,Length=raw.Length,
                        Terminal=true,Elapsed=BitConverter.ToUInt64(raw,terminal.Offset)};
                    if(__state.Elapsed>GameTimer.Instance.ticks){__state=null;return false;}
                    __state.Allocation=allocation;allocation=null;return true;
                }
                if(!before&&!after)return false;
                // After permission follows actual accepted+saved Before. No
                // custom request body or missing callback supplies that proof.
                if(after&&!(TryRetainBeforePublication(context)||TryRetainAfterPublication(context)))return false;
                if(before&&!context.BeforeCurrent())return false; // Exact held CAS; normalizing elapsed only.
                if(after&&!context.BeforeCurrent()&&!context.AfterCurrent())return false;
                int elapsedOffset=before?frame.BeforeElapsedOffset:frame.AfterElapsedOffset;
                __state=new Capture{Original=context,Tile=context.Station,Packet=__instance,Block=__instance.teBlockId,Length=raw.Length,
                    Before=before,Elapsed=BitConverter.ToUInt64(raw,elapsedOffset)};
                if(__state.Elapsed>GameTimer.Instance.ticks){__state=null;return false;}
                __state.Allocation=allocation;allocation=null;
                return true;
                }
                finally{allocation?.Dispose();}
            }
            return true; // Unrelated native uploads preserve their native behavior.
        }
        private static void Postfix(NetPackageTileEntity __instance,World _world,bool __runOriginal,Purposes __state)
        {
            RebirthRecyclerPlacedSettlement.ObserveRecyclerNativeUpload(__instance,_world,__runOriginal,__state?.Recycler);
            // Native reader returned; its parsed state now belongs to the live
            // TE. Release this sequential decoder before opening a separately
            // charged same-snapshot publication observer.
            if(__state?.Earned!=null){__state.Earned.Allocation?.Dispose();__state.Earned.Allocation=null;}
            RebirthProductiveEarnedUiDeferredTransport.ObserveRemoteCloseNativeUpload(__instance,_world,__runOriginal,__state?.RemoteClose);
            RebirthProductiveEarnedUiRemote.ReleaseQueueNativeAllocation(__state?.QueueHandoff);
            RebirthProductiveEarnedUiRemoteBefore.ReleaseNativeAllocation(__state?.UiBefore);
            // Readonly earlier-source identity follows the actual native read
            // before a purpose opens its long-lived publication observation.
            RebirthProductiveEarnedUiQueueOwner.ObserveNativeUpload(__instance,_world,__runOriginal,__state?.QueueOwner);
            ObserveEarned(__instance,_world,__runOriginal,__state?.Earned);
            RebirthProductiveEarnedUiRemote.ObserveQueueNativeUpload(__instance,_world,__runOriginal,__state?.QueueHandoff);
            RebirthProductiveEarnedUiRemoteBefore.ObserveNativeUpload(__instance,_world,__runOriginal,__state?.UiBefore);
            RebirthProductiveEarnedUiDeferredTransport.ObserveNativeUpload(__instance,_world,__runOriginal,__state?.Deferred);
            RebirthProductiveEarnedUiRemote.ObserveNativeUpload(__instance,_world,__runOriginal,__state?.UiTerminal);
            RebirthRemoteResourcePlacedStationOriginal.ObserveNativeUpload(__instance,_world,__runOriginal,__state?.Fetched);
        }
        private static Exception Finalizer(Exception __exception,Purposes __state)
        {
            RebirthRecyclerPlacedSettlement.EndRecyclerNativeUpload(__state?.Recycler);
            RebirthProductiveEarnedUiDeferredTransport.EndRemoteCloseNativeUpload(__state?.RemoteClose);
            RebirthProductiveEarnedUiQueueOwner.EndNativeUpload(__state?.QueueOwner);
            RebirthProductiveEarnedUiRemote.EndQueueNativeUpload(__state?.QueueHandoff);
            RebirthProductiveEarnedUiRemoteBefore.EndNativeUpload(__state?.UiBefore);
            try{RebirthRemoteResourcePlacedStationOriginal.EndNativeUpload(__state?.Fetched);}
            catch{/* Keep the original native exception and original unknown custody. */}
            RebirthProductiveEarnedUiRemote.EndNativeUpload(__state?.UiTerminal);
            try{RebirthProductiveEarnedUiDeferredTransport.EndNativeUpload(__state?.Deferred);}
            catch{/* Preserve the native exception and unknown deferred original. */}
            if(ReferenceEquals(Reading,__state?.Earned))Reading=null;
            try{__state?.Earned?.Allocation?.Dispose();}
            catch{/* Allocation cleanup must not replace the native exception. */}
            return __exception;
        }
        private static void ObserveEarned(NetPackageTileEntity __instance,World _world,bool __runOriginal,Capture __state)
        {
            try
            {
            if(!__runOriginal||__state==null||!ReferenceEquals(Reading,__state)||__state.Reads!=1||!__state.ReadCompleted||
                !__state.Original.Current()||!ReferenceEquals(_world.GetTileEntity(__instance.teWorldPos),__state.Tile)||
                _world.GetBlock(__instance.teWorldPos).type!=__state.Block||__instance.ms.Length!=__state.Length||__instance.ms.Position!=__state.Length||
                GameTimer.Instance.ticks<__state.Elapsed||__state.Tile.lastTickTime!=GameTimer.Instance.ticks-__state.Elapsed)return;
            var context=__state.Original;
            if(__state.Tools){ObserveAcceptedNativeTools(context);return;}
            if(__state.Pending){ObserveAcceptedNativePending(context);return;}
            if(__state.Terminal){ObserveAcceptedNativeOutput(context);return;}
            if(__state.Before)
            {
                if(!context.BeforeCurrent())return;
                context.BeforeAccepted=true;context.BeforeAcceptedElapsed=__state.Elapsed;
                TryRetainBeforePublication(context);return;
            }
            if(!context.AfterCurrent()||!context.Artifact.MatchesAfter(__state.Tile))return;
            context.Accepted=true;var previous=uploading;uploading=context;
            try{context.Observation?.Dispose();context.Observation=RebirthProductiveEarnedPublication.BeginAfterNativeWork(context.Station,context.Artifact);}
            finally{uploading=previous;}
            }
            catch{/* Native read already happened. Unknown publication remains retained. */}
        }
    }
    // A returned ProcessPackage alone is not an accepted native body. Record
    // its actual one FromClient reader invocation on this exact native stream.
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.read))]
    private static class NativeRead
    {
        private static void Prefix(TileEntityWorkstation __instance,PooledBinaryReader _br,StreamModeRead _eStreamMode,bool __runOriginal,out object __state)
        {
            RebirthRecyclerPlacedSettlement.EnterRecyclerRemoteNativeRead(__instance,_br,_eStreamMode,__runOriginal);
            RebirthProductiveEarnedUiDeferredTransport.EnterRemoteCloseNativeRead(__instance,_br,_eStreamMode,__runOriginal);
            RebirthWorkstationCompletionPipeline.EnterRemoteNativeRead(__instance,_br,_eStreamMode,__runOriginal);
            RebirthProductiveEarnedUiRemote.EnterNativeRead(__instance,_br,_eStreamMode,__runOriginal);
            RebirthProductiveEarnedUiRemote.EnterQueueNativeRead(__instance,_br,_eStreamMode,__runOriginal);
            RebirthProductiveEarnedUiRemoteBefore.EnterNativeRead(__instance,_br,_eStreamMode,__runOriginal);
            RebirthProductiveEarnedUiDeferredTransport.EnterNativeRead(__instance,_br,_eStreamMode,__runOriginal);
            RebirthProductiveEarnedUiQueueOwner.EnterNativeRead(__instance,_br,_eStreamMode,__runOriginal);
            __state=null;var original=NativeUpload.Reading;
            if(!__runOriginal||original==null||_eStreamMode!=StreamModeRead.FromClient||!ReferenceEquals(__instance,original.Tile)||
                !ReferenceEquals(_br.BaseStream,original.Packet.ms)||!original.Original.Current())return;
            original.Reads++;__state=original;
        }
        private static void Postfix(PooledBinaryReader _br,bool __runOriginal,object __state)
        {
            RebirthRecyclerPlacedSettlement.ObserveRecyclerRemoteNativeRead(_br,__runOriginal);
            RebirthProductiveEarnedUiDeferredTransport.ObserveRemoteCloseNativeRead(_br,__runOriginal);
            RebirthWorkstationCompletionPipeline.ObserveRemoteNativeRead(_br,__runOriginal);
            RebirthProductiveEarnedUiRemote.ObserveNativeRead(_br,__runOriginal);
            RebirthProductiveEarnedUiRemote.ObserveQueueNativeRead(_br,__runOriginal);
            RebirthProductiveEarnedUiRemoteBefore.ObserveNativeRead(_br,__runOriginal);
            RebirthProductiveEarnedUiDeferredTransport.ObserveNativeRead(_br,__runOriginal);
            RebirthProductiveEarnedUiQueueOwner.ObserveNativeRead(_br,__runOriginal);
            if(__runOriginal&&__state is NativeUpload.Capture original&&ReferenceEquals(NativeUpload.Reading,original)&&original.Reads==1&&
                ReferenceEquals(_br.BaseStream,original.Packet.ms)&&_br.BaseStream.Position==original.Length)original.ReadCompleted=true;
        }
    }
}
