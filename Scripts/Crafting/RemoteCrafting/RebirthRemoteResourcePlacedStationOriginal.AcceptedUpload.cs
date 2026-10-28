using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class UploadOriginal:IDisposable
    {
        internal EntityPlayer Actor;internal ClientInfo Peer;internal TileEntityWorkstation Station;internal Chunk Chunk;
        internal RebirthRemoteResourceClientOriginal.Intent Original;internal RebirthRemoteResourcePlacedStationPreparation.UploadRead Memory;
        internal Guid Source;internal string Birth,Directory;internal long Workspace,IntentWorkspace;
        internal AcceptedUpload Accepted;
        internal CancellationServer Cancellation;
        internal bool Current()=>Cancellation!=null?Cancellation.Current():Memory?.Current==true&&Directory==GameIO.GetSaveGameDir()&&ServerScope(Actor,Original)&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Actor.entityId),Peer)&&
            RebirthRemoteResourcePlacedStationScope.TryResolveLayout(Actor,Station.ToWorldPos(),Original.Creation,out var actual,out _,out _,out _)&&ReferenceEquals(actual,Station)&&
            ReferenceEquals(Station.GetChunk(),Chunk)&&Chunk!=null&&!Chunk.InProgressUnloading&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(Actor.world,Station,out string birth)&&birth==Birth;
        public void Dispose(){Memory?.Dispose();Memory=null;Accepted=null;}
    }
    private sealed class NativeUploadInvocation
    {internal UploadOriginal Original;internal NetPackageTileEntity Packet;internal byte[] Before,Raw;internal int Reads;internal bool ReadCompleted;internal RebirthRemoteResourcePlacedStationAllocation.Scratch Scratch;}
    private sealed class AcceptedUpload
    {internal readonly byte[] Before,Raw,After;internal readonly ulong Tick;internal AcceptedUpload(byte[] before,byte[] raw,byte[] after,ulong tick){Before=before;Raw=raw;After=after;Tick=tick;}}
    private static readonly Dictionary<Guid,UploadOriginal> uploadOriginals=new Dictionary<Guid,UploadOriginal>();
    [ThreadStatic]private static NativeUploadInvocation nativeUploadInvocation;
    private static World uploadWorld;private static string uploadDirectory;
    private static bool uploadReaderInstalled;
    private static void BindUploadWorld()
    {
        BindCancellationWorld();
        var world=GameManager.Instance?.World;string directory=bindingSaveDirectory.Get();
        if(ReferenceEquals(world,uploadWorld)&&directory==uploadDirectory)return;
        foreach(var original in uploadOriginals.Values)original.Dispose();uploadOriginals.Clear();uploadWorld=world;uploadDirectory=directory;nativeUploadInvocation=null;
    }
    private static void InstallAcceptedUpload()
    {
        if(uploadReaderInstalled)return;
        RebirthProductiveEarnedRemoteHost.InstallNativeUploadObserver();
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.placed.accepted.native.reader"),typeof(AcceptedNativeRead));uploadReaderInstalled=true;
    }
    private static void ArmBeforeUpload(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        BindUploadWorld();
        if(!ServerScope(actor,original)||!ValidateInput(original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world)||
            !RebirthRemoteResourcePlacedStationScope.TryResolveLayout(actor,position,original.Creation,out var station,out _,out _,out _)||
            !RebirthRemoteResourcePlacedStationAllocation.TryAdmitIntentBeforeHydration(original.Copy(),out long intentWorkspace)||
            !RebirthRemoteResourcePlacedStationAllocation.TryAdmitUploadTargetBeforeCapture(station,intentWorkspace,out long workspace))return;
        Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
        if(uploadOriginals.TryGetValue(source,out var retained))return; // No rebinding/reset of attempted native upload.
        var peer=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(actor.entityId);
        if(peer==null||!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var nativeOwner)||nativeOwner.StorageKey!=owner.StorageKey||
            !RebirthRemoteResourceDurableJournal.IsKnownAbsentIncludingCandidates(owner.StorageKey,source)||
            !RebirthRemoteResourcePlacedStationPreparation.TryReserveUploadRead(source,workspace,out var memory))return;
        try
        {
            if(!RebirthRemoteResourcePlacedStationIdentity.EnsureFresh(actor,original,source,station)||
                !RebirthRemoteResourcePlacedStationIdentity.TryRead(actor,original,source,station,out string birth))return;
            var value=new UploadOriginal{Actor=actor,Peer=peer,Station=station,Chunk=station.GetChunk(),Original=original,Source=source,Birth=birth,Memory=memory,Workspace=workspace,IntentWorkspace=intentWorkspace,Directory=GameIO.GetSaveGameDir()};
            if(!value.Current())return;uploadOriginals.Add(source,value);memory=null;
        }
        finally{memory?.Dispose();}
    }
    internal static void EnterNativeUpload(NetPackageTileEntity packet,World world,bool runOriginal,out object state)
    {
        state=null;if(!ProtocolActive||!runOriginal||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(world,GameManager.Instance?.World)||nativeUploadInvocation!=null)return;
        BindUploadWorld();
        RebirthRemoteResourcePlacedStationAllocation.Scratch scratch=null;
        try
        {
            var original=uploadOriginals.Values.SingleOrDefault(value=>value.Station.ToWorldPos()==packet.teWorldPos&&ReferenceEquals(value.Peer,packet.Sender));
            if(original==null||original.Accepted!=null||!original.Current()||packet.ms==null||packet.ms.Length<1||packet.ms.Length>RebirthRemoteResourcePlacedStationCandidate.MaximumTileBytes||
                world.GetBlock(packet.teWorldPos).type!=packet.teBlockId||
                !RebirthRemoteResourcePlacedStationAllocation.TryAdmitUploadTargetBeforeCapture(original.Station,original.IntentWorkspace,out long workspace)||!original.Memory.Reserve(workspace)||
                !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(original.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.NativeBefore,out scratch)||
                !CaptureUploadNative(original.Station,out var before))return;
            var raw=new byte[(int)packet.ms.Length];lock(packet.ms){long position=packet.ms.Position;try{packet.ms.Position=0;int offset=0;while(offset<raw.Length){int count=packet.ms.Read(raw,offset,raw.Length-offset);if(count<1)return;offset+=count;}}finally{packet.ms.Position=position;}}
            if(!RebirthRemoteResourcePlacedStationNativeReader.TryMeasureToServer(raw,out _,out long graph,out long parserScratch)||
                !original.Memory.Reserve(checked(workspace+12*graph+Math.Max(0,parserScratch-RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes))))return;
            var invocation=new NativeUploadInvocation{Original=original,Packet=packet,Before=before,Raw=raw,Scratch=scratch};nativeUploadInvocation=invocation;state=invocation;scratch=null;
        }
        catch{/* Readonly observation failure cannot change native acceptance. */}
        finally{scratch?.Dispose();}
    }
    internal static void ObserveNativeUpload(NetPackageTileEntity packet,World world,bool runOriginal,object state)
    {
        try
        {
            var invocation=state as NativeUploadInvocation;var original=invocation?.Original;
            if(!runOriginal||invocation==null||!ReferenceEquals(nativeUploadInvocation,invocation)||!ReferenceEquals(invocation.Packet,packet)||invocation.Reads!=1||!invocation.ReadCompleted||
                original.Accepted!=null||!original.Current()||!ReferenceEquals(packet.Sender,original.Peer)||world.GetBlock(packet.teWorldPos).type!=packet.teBlockId||
                packet.ms.Position!=invocation.Raw.Length||packet.ms.Length!=invocation.Raw.Length||
                !CaptureUploadNative(original.Station,out var after)||
                !RebirthRemoteResourcePlacedStationNativeReader.TryRead(after,out var native)||!MatchesAcceptedNativeBody(invocation.Raw,native,GameTimer.Instance.ticks))return;
            if(original.Cancellation!=null){ObserveCancellationAccepted(original,invocation,after);return;}
            if(
                !ValidateInput(original.Original,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _,out var input)||!SameGrid(input,original.Station.Input))return;
            var expectedQueue=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Original.Copy().Element("action").Element("queue").Value,RebirthRemoteResourcePlacedStationQueue.MaximumBytes);
            if(expectedQueue.Element("native")?.Value!=RebirthCookingRemoteAdmissionBackend.Queue(original.Station.Queue))return;
            // Private positive event exists ONLY after actual authenticated
            // ProcessPackage and its actual native FromClient reader completed.
            var accepted=new AcceptedUpload(invocation.Before,invocation.Raw,after,GameTimer.Instance.ticks);
            if(!PublishAcceptedUpload(original,accepted)||!original.Current())return;original.Accepted=accepted;
        }
        catch{/* Actual native run occurred; unknown is retained without replay. */}
    }
    internal static void EndNativeUpload(object state)
    {if(state is NativeUploadInvocation invocation){invocation.Scratch?.Dispose();invocation.Scratch=null;if(ReferenceEquals(nativeUploadInvocation,invocation))nativeUploadInvocation=null;}}
    private static bool CaptureUploadNative(TileEntityWorkstation station,out byte[] bytes)
    {
        bytes=null;ulong clock=station.lastTickTime;
        if(!RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,station.Input,station.Queue,station.IsBurning,out var frame,out int offset)||station.lastTickTime!=clock)return false;
        // Keep full ACTUAL native clock bytes in accepted-event artifacts. Only
        // later comparisons may normalize this explicitly qualified field.
        Buffer.BlockCopy(BitConverter.GetBytes(clock),0,frame,offset,8);bytes=frame;return true;
    }
    [HarmonyPatch(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.read))]
    private static class AcceptedNativeRead
    {
        private static void Prefix(TileEntityWorkstation __instance,PooledBinaryReader _br,StreamModeRead _eStreamMode,bool __runOriginal,out object __state)
        {
            __state=null;CancellationCachedPurposes purposes=null;
            try{var cached=RebirthWorkstationCompletionPipeline.EnterCachedPersistencyRead(__instance,_br,_eStreamMode,__runOriginal);if(cached!=null)__state=purposes=new CancellationCachedPurposes{Cached=cached};}catch{/* Independent readonly purpose. */}
            try{var recycler=RebirthRecyclerPlacedSettlement.EnterRecyclerPersistencyRead(__instance,_br,_eStreamMode,__runOriginal);if(recycler!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Recycler=recycler;}}catch{}
            try{var earned=RebirthProductiveEarnedSavedNativeSource.EnterCachedPersistencyRead(__instance,_br,_eStreamMode,__runOriginal);if(earned!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Earned=earned;}}catch{}
            try{var source4=EnterSource4PersistencyRead(__instance,_br,_eStreamMode,__runOriginal);if(source4!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Source4=source4;}}catch{}
            try{var initialQueue=RebirthProductiveEarnedUiQueueOwner.EnterInitialPersistencyRead(__instance,_br,_eStreamMode,__runOriginal);if(initialQueue!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.InitialQueue=initialQueue;}}catch{}
            try{var recipeOutput=RebirthRecipeOutputIntegration.EnterRecipeOutputPersistencyRead(__instance,_br,_eStreamMode,__runOriginal);if(recipeOutput!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.RecipeOutput=recipeOutput;}}catch{}
            var cancellation=EnterCancellationPersistencyRead(__instance,_br,_eStreamMode,__runOriginal);
            if(cancellation!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Cancellation=cancellation;return;}var invocation=nativeUploadInvocation;
            if(!__runOriginal||invocation==null||_eStreamMode!=StreamModeRead.FromClient||!ReferenceEquals(__instance,invocation.Original.Station)||!ReferenceEquals(_br.BaseStream,invocation.Packet.ms)||!invocation.Original.Current())return;
            invocation.Reads++;if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Cancellation=invocation;
        }
        private static void Postfix(PooledBinaryReader _br,bool __runOriginal,object __state)
        {
            var purposes=__state as CancellationCachedPurposes;
            try{ObserveCancellationPersistencyRead(_br,__runOriginal,purposes?.Cancellation);if(__runOriginal&&purposes?.Cancellation is NativeUploadInvocation invocation&&ReferenceEquals(nativeUploadInvocation,invocation)&&invocation.Reads==1&&_br.BaseStream.Position==invocation.Raw.Length)invocation.ReadCompleted=true;}
            finally{if(purposes?.Cached!=null){purposes.CachedReadObserved=true;try{RebirthWorkstationCompletionPipeline.ObserveCachedPersistencyRead(_br,__runOriginal,purposes.Cached);}catch{/* B retains unknown. */}}
                if(purposes?.Recycler!=null){purposes.RecyclerReadObserved=true;try{RebirthRecyclerPlacedSettlement.ObserveRecyclerPersistencyRead(_br,__runOriginal,purposes.Recycler);}catch{}}
                if(purposes?.Source4!=null){purposes.Source4ReadObserved=true;try{ObserveSource4PersistencyRead(_br,__runOriginal,purposes.Source4);}catch{}}
                if(purposes?.InitialQueue!=null){purposes.InitialQueueReadObserved=true;try{RebirthProductiveEarnedUiQueueOwner.ObserveInitialPersistencyRead(_br,__runOriginal,purposes.InitialQueue);}catch{}}
                if(purposes?.RecipeOutput!=null){purposes.RecipeOutputReadObserved=true;try{RebirthRecipeOutputIntegration.ObserveRecipeOutputPersistencyRead(_br,__runOriginal,purposes.RecipeOutput);}catch{}}
                if(purposes?.Earned!=null){purposes.EarnedReadObserved=true;try{RebirthProductiveEarnedSavedNativeSource.ObserveCachedPersistencyRead(_br,__runOriginal,purposes.Earned);}catch{}}}
        }
        private static Exception Finalizer(PooledBinaryReader _br,Exception __exception,object __state)
        {
            if(__state is CancellationCachedPurposes purposes)
            {if(purposes.Cached!=null&&!purposes.CachedReadObserved)try{RebirthWorkstationCompletionPipeline.ObserveCachedPersistencyRead(_br,false,purposes.Cached);}catch{/* Preserve native exception. */}
                if(purposes.Recycler!=null&&!purposes.RecyclerReadObserved)try{RebirthRecyclerPlacedSettlement.ObserveRecyclerPersistencyRead(_br,false,purposes.Recycler);}catch{}
                if(purposes.Earned!=null&&!purposes.EarnedReadObserved)try{RebirthProductiveEarnedSavedNativeSource.ObserveCachedPersistencyRead(_br,false,purposes.Earned);}catch{}
                if(purposes.Source4!=null&&!purposes.Source4ReadObserved)try{ObserveSource4PersistencyRead(_br,false,purposes.Source4);}catch{}
                if(purposes.InitialQueue!=null&&!purposes.InitialQueueReadObserved)try{RebirthProductiveEarnedUiQueueOwner.ObserveInitialPersistencyRead(_br,false,purposes.InitialQueue);}catch{}
                if(purposes.RecipeOutput!=null&&!purposes.RecipeOutputReadObserved)try{RebirthRecipeOutputIntegration.ObserveRecipeOutputPersistencyRead(_br,false,purposes.RecipeOutput);}catch{}
                purposes.Cached=null;purposes.Cancellation=null;purposes.Recycler=null;purposes.Earned=null;purposes.Source4=null;purposes.InitialQueue=null;purposes.RecipeOutput=null;}
            return __exception;
        }
    }
    private static bool MatchesAcceptedNativeBody(byte[] raw,RebirthRemoteResourcePlacedStationNativeReader.Image image,ulong acceptedTick)
    {
        try
        {
            using(var stream=new MemoryStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                var encoding=writer.Encoding;try
                {
                    writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);
                    writer.Write(image.LocalPosition.x);writer.Write(image.LocalPosition.y);writer.Write(image.LocalPosition.z);writer.Write((byte)50);
                    foreach(var grid in new[]{image.Fuel,image.Input,image.Tools,image.Output}){writer.Write((byte)grid.Length);foreach(var stack in grid)stack.Write(writer);}
                    writer.Write((byte)image.Queue.Length);foreach(var row in image.Queue)row.Write(writer);
                    writer.Write((short)image.Completions.Length);foreach(var completed in image.Completions)completed.Write(writer);
                    writer.Write(image.Burning);writer.Write(image.BurnTime);writer.Write((byte)image.MeltTimes.Length);foreach(float time in image.MeltTimes)writer.Write(time);writer.Write(image.PlayerPlaced);
                    int offset=checked((int)stream.Position);if(offset>raw.Length-8)return false;ulong elapsed=BitConverter.ToUInt64(raw,offset);
                    if(elapsed>acceptedTick||image.Clock!=acceptedTick-elapsed)return false;writer.Write(elapsed);
                    writer.Write((byte)image.LastInput.Length);foreach(var stack in image.LastInput)stack.Write(writer);writer.Flush();
                    return stream.Length==raw.Length&&stream.ToArray().SequenceEqual(raw);
                }
                finally{writer.Encoding=encoding;}
            }
        }
        catch{return false;}
    }
}
