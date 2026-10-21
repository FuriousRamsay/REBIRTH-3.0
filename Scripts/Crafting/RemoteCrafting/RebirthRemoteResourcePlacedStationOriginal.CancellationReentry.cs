using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class CancellationNativeResult
    {
        internal NetPackageRebirthRemoteRepairResult Packet;internal World World;internal EntityPlayerLocal Actor;internal INetConnection Connection;internal ConnectionManager Manager;internal Guid Operation;internal string Text,Reconstruction,FullEvidence,FullPermission;
        internal bool Current=>ReferenceEquals(cancellationNativeResult,this)&&Packet!=null&&ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&
            Connection!=null&&ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,Manager)&&Manager.IsServer==false&&Manager.IsConnected&&
            Manager.connectionToServer?.Length>0&&ReferenceEquals(Manager.connectionToServer[0],Connection)&&!Connection.IsDisconnected()&&
            ReferenceEquals(World.GetPrimaryPlayer(),Actor)&&RebirthRemoteResourceRepairProtocol.SupportsClientTransport();
    }
    [ThreadStatic]private static CancellationNativeResult cancellationNativeResult;
    internal static void NativeCancellationResult(NetPackageRebirthRemoteRepairResult packet,World world,EntityPlayerLocal actor,Guid operation,string text,XElement body)
    {
        if(actor==null||body==null||cancellationNativeResult!=null||!RebirthRemoteResourceNetworkScope.Receive(packet,world,GameManager.Instance,false,out var received))return;
        var native=new CancellationNativeResult{Packet=packet,World=world,Actor=actor,Connection=received,Manager=SingletonMonoBehaviour<ConnectionManager>.Instance,Operation=operation,Text=text,Reconstruction=body.Element("reconstruction")?.Value,
            FullEvidence=body.Element("evidence")?.Value,FullPermission=body.Element("permission")?.Value};
        cancellationNativeResult=native;
        try{if(native.Current)CancellationResult(actor,operation,text);}finally{cancellationNativeResult=null;}
    }
    private static bool CancellationMetadataCandidate(CancellationClient original)
    {
        if(original==null||original.Stage!=3||!original.HostTerminal||original.Permission==null||original.TerminalEvidence==null||original.After==null||
            original.OwnerTerminalVersion?.IsCurrent!=true||original.Memory?.Current!=true||!ReferenceEquals(GameManager.Instance?.World,original.OriginalWorld)||!ReferenceEquals(original.Actor?.world,original.OriginalWorld)||
            original.Directory!=GameIO.GetSaveGameDir()||!RebirthRemoteResourceClientOriginal.Current(original.Actor,original.Original)||
            !cancellationClients.TryGetValue(original.Operation,out var actual)||!ReferenceEquals(actual,original)||
            original.Actor.Buffs.GetCustomVar(CancellationReceipt(original.Operation))!=3f||!original.After.Matches(RebirthCamperEnergyActorImage.Capture(original.Actor)))return false;
        var record=CancellationJournal(original.Operation,"Terminal").Read(original.Operation);byte[] full=CancellationClientTerminalBytes(original);
        return record?.State==RebirthCamperTransferJournal.Phase.Applied&&record.Vehicle==original.Source&&record.OwnerCreation==original.Original.Creation&&record.Revision==0&&
            record.Before.SequenceEqual(full)&&record.After.SequenceEqual(full);
    }
    internal sealed class CancellationMetadataReentry
    {
        private readonly CancellationClient original;private readonly World world;private readonly EntityPlayerLocal actor;private readonly string permission,evidence;
        private CancellationMetadataReentry(CancellationClient value){original=value;world=value.OriginalWorld;actor=value.Actor;permission=value.Permission;evidence=value.TerminalEvidence;}
        internal bool Current=>ReferenceEquals(original.Actor,actor)&&ReferenceEquals(GameManager.Instance?.World,world)&&ReferenceEquals(actor.world,world)&&
            original.Permission==permission&&original.TerminalEvidence==evidence&&CancellationMetadataCandidate(original);
        internal static bool TryCreate(CancellationClient original,string text,CancellationNativeEvidence saved,out CancellationMetadataReentry witness)
        {
            witness=null;var native=cancellationNativeResult;
            if(native?.Current!=true||!ReferenceEquals(native.Actor,original.Actor)||native.Operation!=original.Original.Operation||native.Reconstruction!=text||
                saved==null||!CancellationMetadataCandidate(original)||native.FullEvidence!=original.TerminalEvidence||native.FullPermission!=original.Permission)return false;
            if(text==null)
            {
                // Client-only restart need not reload a running host's TE to
                // finish metadata. Its actual accepted native join/ToPlayer
                // event supplies OWNER-body lineage; the full independently
                // checked host terminal supplies historical saved TE evidence.
                // This adopts no TE and grants no inventory or queue setter.
                if(original.RestartOwner?.AcceptedTerminal!=true)return false;
                witness=new CancellationMetadataReentry(original);return witness.Current;
            }
            if(Encoding.UTF8.GetByteCount(text)>MaximumResultBytes)return false;
            long workspace=checked(12L*Encoding.UTF8.GetByteCount(text)+65536);
            if(workspace>original.ReentryWorkspace){if(!original.Memory.Reserve(checked(original.Memory.ReservedBytes+workspace-original.ReentryWorkspace)))return false;original.ReentryWorkspace=workspace;}
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumResultBytes);
            bool queueRecovery=(int?)body.Attribute("version")==3&&(string)body.Attribute("cause")=="queue-reentry-saved-after";
            // Complete native snapshot/payload/owner images are carried once
            // by this SAME actual result's full evidence, already independently
            // validated above. The event adds native callback provenance; it
            // replaces none of those images with counts, hashes or references.
            if(native.FullEvidence!=original.TerminalEvidence||native.FullPermission!=original.Permission||
                body.Name!="placedCancellationReconstruction"||body.Attributes().Count()!=(queueRecovery?13:12)||body.Nodes().Any()||!queueRecovery&&(int?)body.Attribute("version")!=2||
                !Guid.TryParseExact((string)body.Attribute("recovery"),"N",out var recovery)||recovery==Guid.Empty||
                (string)body.Attribute("cancel")!=original.Operation.ToString("N")||(string)body.Attribute("source")!=original.Source.ToString("N")||
                (string)body.Attribute("binding")!=original.Binding||(string)body.Attribute("birth")!=original.Birth||
                (long?)body.Attribute("generation") is not long generation||generation<1||(string)body.Attribute("world")!=original.OriginalWorld.worldState.Guid||
                !ValidateInput(original.Original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                (int?)body.Attribute("chunkX")!=(position.x>>4)||(int?)body.Attribute("chunkZ")!=(position.z>>4)||
                (long?)body.Attribute("tileOffset")!=saved.Offset||(int?)body.Attribute("tileLength")!=saved.Tile.Length)return false;
            // This consumer is reached only from the authenticated native
            // result dispatcher, after complete financial/native proof checks.
            // The host positive producer is the actual persisted LoadChunk and
            // workstation.read chain below. This grants metadata release only.
            witness=new CancellationMetadataReentry(original);return witness.Current;
        }
    }
    // Registered on the main thread from an actual saved terminal original.
    // Worker callbacks may only match immutable bytes and publish strong native
    // handles under this gate. They never touch owner items, journals or World.
    private sealed class CancellationLoadOriginal
    {
        internal CancellationServer Original;internal World World;internal Chunk OldChunk;internal TileEntityWorkstation OldTile;
        internal CancellationRestartArchive Restart;internal RebirthRemoteResourceClientOriginal.Intent Intent;internal RebirthRemoteResourcePlacedStationPreparation.UploadRead Memory;
        internal Guid Operation,Source,Recovery;internal string Binding,Birth;
        internal ChunkProviderGenerateWorld Provider;internal RegionFileManager Manager;internal ChunkSnapshotUtil Util;internal RegionFileAccessAbstract Access;
        internal string Directory,Header,Permission,Evidence,WorldId;internal byte[] Snapshot,Tile;internal long Offset;
        internal int X,Z;internal bool Active=true,OldRetired;internal long Generation,NativeEpoch;internal CancellationLoaded Loaded;
        internal CancellationLoadRetirement Retirement;
        internal bool Receipt1Before;internal CancellationSavedAfterContinuation SavedAfter;
        internal CancellationBeforeContinuation Before;
    }
    private sealed class CancellationLoadRetirement:IRebirthRemoteResourceRetainedNativeBuffers
    {
        private readonly object gate=new object();private bool closed,entered;internal CancellationHostCustody Custody;internal CancellationSnapshotBudget Snapshots;
        internal bool CallbacksDrained{get{lock(gate)return !entered;}}
        public bool IsDrained{get{lock(gate)return !entered&&(Custody==null||Custody.IsDrained)&&(Snapshots==null||Snapshots.IsDrained);}}
        internal bool RetiredAndDrained{get{lock(gate)return closed&&!entered;}}
        internal void Close(){lock(gate)closed=true;}
        private sealed class Lease:IDisposable
        {private CancellationLoadRetirement owner;internal Lease(CancellationLoadRetirement value){owner=value;}public void Dispose(){var value=System.Threading.Interlocked.Exchange(ref owner,null);if(value!=null)lock(value.gate)value.entered=false;}}
        internal bool TryEnter(out IDisposable lease)
        {lease=null;lock(gate){if(closed||entered)return false;entered=true;lease=new Lease(this);return true;}}
    }
    private sealed class CancellationLoaded
    {
        internal CancellationLoadOriginal Original;internal Chunk Chunk;internal TileEntityWorkstation Tile;internal long Generation,NativeEpoch;
        internal CancellationNativeSaveStamp SaveDrain;
        internal bool Installed()=>ThreadManager.IsMainThread()&&Original.Active&&NativeEpoch==Original.NativeEpoch&&Original.Memory?.Current==true&&
            ReferenceEquals(GameManager.Instance?.World,Original.World)&&Original.World.worldState.Guid==Original.WorldId&&
            ReferenceEquals(Original.World.ChunkCache?.ChunkProvider,Original.Provider)&&ReferenceEquals(Original.Provider.m_RegionFileManager,Original.Manager)&&
            ReferenceEquals(Original.Manager.snapshotUtil,Original.Util)&&ReferenceEquals(Original.Manager.regionFileAccess,Original.Access)&&
            Path.GetFullPath(Original.Manager.saveDirectory)==Original.Directory&&ReferenceEquals(Original.World.GetChunkSync(Chunk.X,Chunk.Z),Chunk)&&
            !Chunk.InProgressUnloading&&ReferenceEquals(Chunk.GetTileEntity(Tile.localChunkPos),Tile)&&ReferenceEquals(Tile.GetChunk(),Chunk);
        internal string Encode()=>new XElement("placedCancellationReconstruction",new XAttribute("version",2),new XAttribute("cancel",Original.Operation.ToString("N")),
            new XAttribute("source",Original.Source.ToString("N")),new XAttribute("binding",Original.Binding),new XAttribute("birth",Original.Birth),
            new XAttribute("generation",Generation),new XAttribute("recovery",Original.Recovery.ToString("N")),new XAttribute("world",Original.WorldId),new XAttribute("chunkX",Chunk.X),new XAttribute("chunkZ",Chunk.Z),
            new XAttribute("tileOffset",Original.Offset),new XAttribute("tileLength",Original.Tile.Length)).ToString(SaveOptions.DisableFormatting);
    }
    private sealed class CancellationLoading
    {
        internal CancellationLoadOriginal Original;internal PooledBinaryReader Reader;internal TileEntityWorkstation Tile;internal Chunk Chunk;
        internal int Reads;internal bool ReadReturned,Loaded,Instantiated,Instantiating;internal long Start;
        internal Chunk FactoryChunk;
        internal IDisposable Retirement;
        internal CancellationNativeSaveStamp SaveDrain;
    }
    private static readonly object cancellationLoadGate=new object();
    private static readonly List<CancellationLoadOriginal> cancellationLoads=new List<CancellationLoadOriginal>();
    [ThreadStatic]private static CancellationLoading cancellationLoading;
    private static bool cancellationLoadInstalled;
    private static void InstallCancellationReentry()
    {
        if(cancellationLoadInstalled)return;QualifyCancellationNativeLifecycle();var harmony=new Harmony("rebirth.remote.placed.cancellation.native.reconstruction");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationLoad));RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationLoadStream));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationInstantiate));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationChunkReset));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationBeforeExternalization));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationHostPickup));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationNativeSavePipeline));
        InstallCancellationRestart(harmony);
        cancellationLoadInstalled=true;
    }
    private static bool ArmCancellationReentry(CancellationServer original)
    {
        if(original.LoadOriginal!=null)
        {
            if(original.LoadOriginal.Before==null&&!original.LoadOriginal.Receipt1Before)return true;
            if(!original.AfterSaved||!original.LoadOriginal.Retirement.CallbacksDrained)return false;
            DropCancellationReentry(original);
        }
        if(!original.Current()||!original.AfterSaved||!original.QueueCompleted||!ReadCancellationSnapshot(original,true,out var saved)||
            original.Actor.world.ChunkCache?.ChunkProvider is not ChunkProviderGenerateWorld provider||provider.m_RegionFileManager is not RegionFileManager manager||
            manager.snapshotUtil is not ChunkSnapshotUtil util||!ReferenceEquals(util.regionFileAccess,manager.regionFileAccess)||
            !ReferenceEquals(util.chunkReader.regionFileAccess,manager.regionFileAccess)||!original.NativeLoadReserved||
            checked(4L*(saved.Snapshot.Length+saved.Tile.Length)+65536)>original.NativeLoadWorkspace)return false;
        var token=new CancellationLoadOriginal{Original=original,World=original.Actor.world,WorldId=original.Actor.world.worldState.Guid,OldChunk=original.Chunk,OldTile=original.Station,
            Intent=original.Original,Memory=original.Memory,Operation=original.Operation,Source=original.Source,Binding=original.Binding,Birth=original.Birth,Recovery=Guid.NewGuid(),
            Provider=provider,Manager=manager,Util=util,Access=manager.regionFileAccess,Directory=Path.GetFullPath(manager.saveDirectory),Header=original.Header,Permission=original.Permission,
            Evidence=original.AfterEvidence,Snapshot=saved.Snapshot,Tile=saved.Tile,Offset=saved.Offset,X=original.Chunk.X,Z=original.Chunk.Z};
        token.Retirement=new CancellationLoadRetirement();if(!original.SnapshotBudget.AttachLoadRetirement(token.Retirement))return false;
        lock(cancellationLoadGate)cancellationLoads.Add(token);original.LoadOriginal=token;original.QueueRecoveryPublication?.BindAfter(token);return true;
    }
    private static void DropCancellationReentry(CancellationServer original)
    {
        lock(cancellationLoadGate)
        {
            var token=original.LoadOriginal;if(token==null)return;token.Active=false;token.Retirement?.Close();cancellationLoads.Remove(token);token.Loaded=null;
            token.Original=null;token.OldChunk=null;token.OldTile=null;token.World=null;token.Provider=null;token.Manager=null;token.Util=null;token.Access=null;
            token.Header=null;token.Permission=null;token.Evidence=null;token.Snapshot=null;token.Tile=null;token.Intent=null;token.Memory=null;token.Restart=null;token.SavedAfter=null;token.Before=null;original.LoadOriginal=null;original.Loaded=null;
        }
    }
    private static bool TryAdoptCancellationReentry(CancellationServer original)
    {
        if(original.Memory?.Current!=true||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(original.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.NativeBefore,out var scratch))return false;
        using(scratch)return TryAdoptCancellationReentryCore(original);
    }
    private static bool TryAdoptCancellationReentryCore(CancellationServer original)
    {
        CancellationLoaded loaded;lock(cancellationLoadGate)loaded=original.LoadOriginal?.Loaded;
        if(loaded!=null&&ReferenceEquals(original.Loaded,loaded)&&ReferenceEquals(original.Station,loaded.Tile)&&ReferenceEquals(original.Chunk,loaded.Chunk)&&loaded.Installed())return true;
        if(loaded==null||!loaded.Installed()||original.Origin?.Current!=true||!ServerScope(original.Actor,original.Original)||
            !ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(original.Actor.entityId),original.Peer)||
            original.Header!=loaded.Original.Header||original.Permission!=loaded.Original.Permission||loaded.Original.Before==null&&original.AfterEvidence!=loaded.Original.Evidence||
            !ReferenceEquals(original.Station,loaded.Original.OldTile)&&!ReferenceEquals(original.Station,loaded.Tile)||
            !ReferenceEquals(original.Chunk,loaded.Original.OldChunk)&&!ReferenceEquals(original.Chunk,loaded.Chunk)||
            !RebirthLogisticsEndpointIdentity.TryGetIncarnation(original.Actor.world,loaded.Tile,out string birth)||birth!=original.Birth||
            !RebirthRemoteResourcePlacedStationAllocation.TryAdmitUploadTargetBeforeCapture(loaded.Tile,RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes,out long workspace)||
            !ReserveCancellationEvidence(original,workspace)||
            !CaptureUploadNative(loaded.Tile,out var actualTile)||!actualTile.SequenceEqual(loaded.Original.Tile))return false;
        // The exact current immutable cancellation heads still have to agree.
        // This is subsequent consistency, never the origin of native lineage.
        bool before=loaded.Original.Before!=null;
        if(before&&!loaded.Original.Before.BeforeCurrent(loaded))return false;
        bool savedAfter=loaded.Original.SavedAfter!=null;
        if(savedAfter&&!loaded.Original.SavedAfter.Current)
        {
            // After this precise continuation has completed, receipt3 may
            // legitimately replace2. Only the exact already-Applied full
            // terminal can return to the ordinary terminal-only load path.
            var terminal=CancellationJournal(original.Operation,"Terminal",MaximumApplicationCommand).Read(original.Operation);
            byte[] expected=CancellationServerTerminalBytes(original);
            if(terminal?.State!=RebirthCamperTransferJournal.Phase.Applied||terminal.Vehicle!=original.Source||terminal.OwnerCreation!=original.Original.Creation||
                terminal.Revision!=0||!terminal.Before.SequenceEqual(expected)||!terminal.After.SequenceEqual(expected))return false;
            savedAfter=false;
        }
        foreach(var purpose in before?new[]{"Permission","Mutation"}:new[]{"Permission","Mutation","After","Terminal"})
        {
            var record=CancellationJournal(original.Operation,purpose).Read(original.Operation);
            byte[] expected=purpose=="Permission"?Encoding.UTF8.GetBytes(original.Permission):purpose=="Mutation"?CancellationMutation(original):purpose=="After"?Encoding.UTF8.GetBytes(original.AfterEvidence):CancellationServerTerminalBytes(original);
            if(record==null||(savedAfter&&purpose=="Terminal"?record.State!=RebirthCamperTransferJournal.Phase.Uncertain&&record.State!=RebirthCamperTransferJournal.Phase.Applied:
                record.State!=(purpose=="Permission"||before&&purpose=="Mutation"?RebirthCamperTransferJournal.Phase.Uncertain:RebirthCamperTransferJournal.Phase.Applied))||
                record.Vehicle!=original.Source||record.OwnerCreation!=original.Original.Creation||record.Revision!=0||!record.Before.SequenceEqual(expected)||!record.After.SequenceEqual(expected))return false;
        }
        if(!ReadCancellationAccepted(original,original.Accepted)||!RebirthRemoteResourcePlacedStationColdRead.TryOpen(original.Actor,original.Source,out var history,out _))return false;
        using(history)
        {
            var root=history.Source;var full=history.Artifact;
            if(root.Phase!=4||(string)root.Copy().Attribute("disposition")!="used"||root.Binding!=original.Binding||full.Birth!=original.Birth||
                !XNode.DeepEquals(full.Original.Copy(),original.Original.Copy())||
                !RebirthRemoteResourcePlacedStationReference.TryRead(root.Copy().Element("ownerAction")?.Element("original")?.Value,out var reference)||
                reference.Artifact!=history.ArtifactDigest||reference.Source!=original.Source||reference.Job!=original.Original.Operation||reference.Binding!=original.Binding||
                !ObserveColdAccepted(history,reference)||!TryReadCompletion(root,full,reference,null,out string proof,false,history.Allocation)||proof!=original.Proof||
                (string)root.Copy().Attribute("ownerProof")!=proof||
                RebirthCookingRemoteAdmission.ReadProtocolXml(original.Header,MaximumResultBytes).Element("paidOriginal")?.Value!=CanonicalPermission(ReplyBody(root,full,reference,true,false)))return false;
        }
        original.Station=loaded.Tile;original.Chunk=loaded.Chunk;original.Loaded=loaded;original.Receipt1Source?.AdoptAfter(loaded);
        return original.QueueRecoveryPublication==null||original.QueueRecoveryPublication.AdoptAfter(loaded);
    }
    // Composed through C's already installed actual workstation.read observer.
    private static object EnterCancellationPersistencyRead(TileEntityWorkstation tile,PooledBinaryReader reader,StreamModeRead mode,bool runOriginal)
    {
        var loading=cancellationLoading;
        if(!runOriginal||loading==null||!loading.Instantiating||mode!=StreamModeRead.Persistency||!ReferenceEquals(reader,loading.Reader)||loading.Reads!=0)return null;
        var token=loading.Original;
        lock(cancellationLoadGate)
        {
            if(!token.Active||reader.BaseStream.Position!=token.Offset-8||loading.FactoryChunk==null||!ReferenceEquals(tile.GetChunk(),loading.FactoryChunk))return null;
            loading.Reads++;loading.Tile=tile;loading.Chunk=tile.GetChunk();loading.Start=reader.BaseStream.Position;return loading;
        }
    }
    private static void ObserveCancellationPersistencyRead(PooledBinaryReader reader,bool runOriginal,object state)
    {
        if(!runOriginal||state is not CancellationLoading loading||!ReferenceEquals(loading,cancellationLoading)||!ReferenceEquals(reader,loading.Reader))return;
        lock(cancellationLoadGate)
            if(loading.Original.Active&&loading.Reads==1&&reader.BaseStream.Position==loading.Start+loading.Original.Tile.Length)loading.ReadReturned=true;
    }
    [HarmonyPatch(typeof(ChunkSnapshotUtil),nameof(ChunkSnapshotUtil.LoadChunk))]
    private static class CancellationLoad
    {
        private static void Prefix(ChunkSnapshotUtil __instance,string dir,long key,bool __runOriginal,out object __state)
        {
            __state=null;CancellationCachedPurposes purposes=null;
            try{var cached=RebirthWorkstationCompletionPipeline.EnterCachedNativeLoad(__instance,dir,key,__runOriginal);if(cached!=null)__state=purposes=new CancellationCachedPurposes{Cached=cached};}catch{/* Independent readonly purpose. */}
            try{var recycler=RebirthRecyclerPlacedSettlement.EnterRecyclerNativeLoad(__instance,dir,key,__runOriginal);if(recycler!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Recycler=recycler;}}catch{}
            try{var earned=RebirthProductiveEarnedSavedNativeSource.EnterCachedNativeLoad(__instance,dir,key,__runOriginal);if(earned!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Earned=earned;}}catch{}
            try{var action=RemoteResourceTransactions.EnterActionPaymentTargetLoad(__instance,dir,key,__runOriginal);if(action!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.ActionPayment=action;}}catch{}
            try{var source4=EnterSource4NativeLoad(__instance,dir,key,__runOriginal);if(source4!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Source4=source4;}}catch{}
            try{var initialQueue=RebirthProductiveEarnedUiQueueOwner.EnterInitialNativeLoad(__instance,dir,key,__runOriginal);if(initialQueue!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.InitialQueue=initialQueue;}}catch{}
            try{var recipeOutput=RebirthRecipeOutputIntegration.EnterRecipeOutputNativeLoad(__instance,dir,key,__runOriginal);if(recipeOutput!=null){if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.RecipeOutput=recipeOutput;}}catch{}
            try
            {
            if(!ProtocolActive||!__runOriginal||cancellationLoading!=null)return;
            lock(cancellationLoadGate)
            {
                var matches=cancellationLoads.Where(value=>value.Active&&ReferenceEquals(value.Util,__instance)&&value.Directory==Path.GetFullPath(dir)&&
                    WorldChunkCache.extractX(key)==value.X&&WorldChunkCache.extractZ(key)==value.Z).ToArray();
                if(matches.Length!=1||!matches[0].Retirement.TryEnter(out var lease))return;
                CancellationNativeSaveStamp drain=null;
                if(matches[0].Before!=null&&!CancellationNativeSaveStamp.TryCapture(matches[0].Manager,key,matches[0].OldChunk,out drain)){lease.Dispose();return;}
                var loading=new CancellationLoading{Original=matches[0],Retirement=lease,SaveDrain=drain};cancellationLoading=loading;
                if(purposes==null)__state=purposes=new CancellationCachedPurposes();purposes.Cancellation=loading;
            }
            }
            catch{/* Observation cannot alter native load behavior. */}
        }
        private static void Postfix(Chunk __result,bool __runOriginal,object __state)
        {
            try
            {
            if(!__runOriginal||(__state as CancellationCachedPurposes)?.Cancellation is not CancellationLoading loading||!ReferenceEquals(cancellationLoading,loading))return;
            lock(cancellationLoadGate)
            {
                var token=loading.Original;
                if(!token.Active||!loading.Loaded||!loading.Instantiated||!loading.ReadReturned||loading.Reads!=1||__result==null||!ReferenceEquals(__result,loading.Chunk)||
                    ReferenceEquals(__result,token.OldChunk)&&!token.OldRetired||ReferenceEquals(loading.Tile,token.OldTile)||
                    !ReferenceEquals(__result.GetTileEntity(loading.Tile.localChunkPos),loading.Tile)||loading.Reader.BaseStream.Position!=token.Snapshot.Length-8||token.Before!=null&&loading.SaveDrain?.Current!=true)return;
                token.Loaded=new CancellationLoaded{Original=token,Chunk=__result,Tile=loading.Tile,Generation=checked(++token.Generation),NativeEpoch=token.NativeEpoch,SaveDrain=loading.SaveDrain};
            }
            }
            catch{/* Incomplete witness stays unknown; native result is untouched. */}
            finally{if(__state is CancellationCachedPurposes purposes){if(purposes.Cached!=null)try{RebirthWorkstationCompletionPipeline.ObserveCachedNativeLoad(__result,__runOriginal,purposes.Cached);}catch{/* B retains unknown. */}
                if(purposes.Recycler!=null)try{RebirthRecyclerPlacedSettlement.ObserveRecyclerNativeLoad(__result,__runOriginal,purposes.Recycler);}catch{}
                if(purposes.Earned!=null)try{RebirthProductiveEarnedSavedNativeSource.ObserveCachedNativeLoad(__result,__runOriginal,purposes.Earned);}catch{}
                if(purposes.Source4!=null)try{ObserveSource4NativeLoad(__result,__runOriginal,purposes.Source4);}catch{}
                if(purposes.InitialQueue!=null)try{RebirthProductiveEarnedUiQueueOwner.ObserveInitialNativeLoad(__result,__runOriginal,purposes.InitialQueue);}catch{}
                if(purposes.RecipeOutput!=null)try{RebirthRecipeOutputIntegration.ObserveRecipeOutputNativeLoad(__result,__runOriginal,purposes.RecipeOutput);}catch{}
                if(purposes.ActionPayment!=null)try{RemoteResourceTransactions.ObserveActionPaymentTargetLoad(__result,__runOriginal,purposes.ActionPayment);}catch{}}}
        }
        private static Exception Finalizer(Exception __exception,object __state)
        {
            var purposes=__state as CancellationCachedPurposes;
            try
            {
            if(purposes?.Cancellation is CancellationLoading loading)
            {
                if(ReferenceEquals(cancellationLoading,loading))cancellationLoading=null;
                loading.Reader=null;loading.Tile=null;loading.Chunk=null;loading.FactoryChunk=null;loading.Original=null;loading.SaveDrain=null;loading.Retirement?.Dispose();loading.Retirement=null;
            }
            }
            finally
            {
            if(purposes?.Cached!=null)try{RebirthWorkstationCompletionPipeline.EndCachedNativeLoad(purposes.Cached);}catch{/* Native exception is unchanged. */}
            if(purposes?.Recycler!=null)try{RebirthRecyclerPlacedSettlement.EndRecyclerNativeLoad(purposes.Recycler);}catch{}
            if(purposes?.Earned!=null)try{RebirthProductiveEarnedSavedNativeSource.EndCachedNativeLoad(purposes.Earned);}catch{}
            if(purposes?.ActionPayment!=null)try{RemoteResourceTransactions.EndActionPaymentTargetLoad(purposes.ActionPayment);}catch{}
            if(purposes?.Source4!=null)try{EndSource4NativeLoad(purposes.Source4);}catch{}
            if(purposes?.InitialQueue!=null)try{RebirthProductiveEarnedUiQueueOwner.EndInitialNativeLoad(purposes.InitialQueue,__exception);}catch{}
            if(purposes?.RecipeOutput!=null)try{RebirthRecipeOutputIntegration.EndRecipeOutputNativeLoad(purposes.RecipeOutput,__exception);}catch{}
            if(purposes!=null){purposes.Cached=null;purposes.Cancellation=null;purposes.Recycler=null;purposes.Earned=null;purposes.ActionPayment=null;purposes.Source4=null;purposes.InitialQueue=null;purposes.RecipeOutput=null;}
            }
            return __exception;
        }
    }
    [HarmonyPatch(typeof(RegionFileChunkReader),nameof(RegionFileChunkReader.readIntoLoadStream))]
    private static class CancellationLoadStream
    {
        private static void Postfix(RegionFileChunkReader __instance,PooledBinaryReader __result,string _dir,int chunkX,int chunkZ,string ext,uint version,bool __runOriginal)
        {
            try{RebirthWorkstationCompletionPipeline.ObserveCachedNativeLoadStream(__instance,__result,_dir,chunkX,chunkZ,ext,version,__runOriginal);}catch{/* Independent readonly purpose. */}
            try{RebirthRecyclerPlacedSettlement.ObserveRecyclerNativeLoadStream(__instance,__result,_dir,chunkX,chunkZ,ext,version,__runOriginal);}catch{}
            try{RebirthProductiveEarnedSavedNativeSource.ObserveCachedNativeLoadStream(__instance,__result,_dir,chunkX,chunkZ,ext,version,__runOriginal);}catch{}
            try{RemoteResourceTransactions.ObserveActionPaymentTargetLoadStream(__instance,__result,_dir,chunkX,chunkZ,ext,version,__runOriginal);}catch{}
            try{ObserveSource4NativeLoadStream(__instance,__result,_dir,chunkX,chunkZ,ext,version,__runOriginal);}catch{}
            try{RebirthProductiveEarnedUiQueueOwner.ObserveInitialNativeLoadStream(__instance,__result,_dir,chunkX,chunkZ,ext,version,__runOriginal);}catch{}
            try{RebirthRecipeOutputIntegration.ObserveRecipeOutputNativeLoadStream(__instance,__result,_dir,chunkX,chunkZ,ext,version,__runOriginal);}catch{}
            try
            {
            var loading=cancellationLoading;if(!__runOriginal||loading==null||__result==null)return;
            lock(cancellationLoadGate)
            {
                var token=loading.Original;
                if(!token.Active||!ReferenceEquals(token.Util.chunkReader,__instance)||!ReferenceEquals(__instance.regionFileAccess,token.Access)||
                    Path.GetFullPath(_dir)!=token.Directory||ext!="7rg"||chunkX!=token.X||chunkZ!=token.Z||version!=47||
                    !ReferenceEquals(__result,__instance.loadChunkReader)||!ReferenceEquals(__result.BaseStream,__instance.loadChunkMemoryStream)||
                    __result.BaseStream.Position!=0||__result.BaseStream.Length!=token.Snapshot.Length-8)return;
                var stream=__result.BaseStream;long position=stream.Position;
                try{for(int index=8;index<token.Snapshot.Length;index++)if(stream.ReadByte()!=token.Snapshot[index])return;}
                finally{stream.Position=position;}
                loading.Reader=__result;loading.Loaded=true;
            }
            }
            catch{/* Never make a successful native load enter backup/removal. */}
        }
    }
    [HarmonyPatch(typeof(Chunk),nameof(Chunk.Reset))]
    private static class CancellationChunkReset
    {
        private sealed class Reset {internal CancellationLoadOriginal Original;internal bool OldPresent;}
        private static void Prefix(Chunk __instance,bool __runOriginal,out object __state)
        {
            __state=null;try
            {
            if(!ProtocolActive||!__runOriginal)return;
            lock(cancellationLoadGate)
            {
                var items=cancellationLoads.Where(value=>value.Active&&(ReferenceEquals(value.OldChunk,__instance)||ReferenceEquals(value.Loaded?.Chunk,__instance)))
                    .Select(value=>new Reset{Original=value,OldPresent=value.OldTile!=null&&ReferenceEquals(value.OldChunk,__instance)&&ReferenceEquals(__instance.GetTileEntity(value.OldTile.localChunkPos),value.OldTile)}).ToArray();
                if(items.Length>0)__state=items;
            }
            }
            catch{/* Native pool reset is unchanged by observer failure. */}
        }
        private static void Postfix(Chunk __instance,bool __runOriginal,object __state)
        {
            try
            {
            if(!__runOriginal||__state is not Reset[] items||__instance.tileEntities.Count!=0)return;
            lock(cancellationLoadGate)foreach(var item in items)if(item.Original.Active)
            {
                item.Original.NativeEpoch++;item.Original.Loaded=null;
                if(item.OldPresent)item.Original.OldRetired=true;
            }
            }
            catch{/* No reset/reconstruction permit is synthesized. */}
        }
    }
}
