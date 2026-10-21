using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static Guid CancellationOperation(Guid job)
    {using(var hash=SHA256.Create()){var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(job.ToString("N")+"|placed-native-cancellation-v1"));var id=new byte[16];Array.Copy(bytes,id,16);return new Guid(id);}}
    private static string CancellationReceipt(Guid operation)=>"RebirthPlacedCancelV1_"+operation.ToString("N");
    private static bool CancellationCandidate(Recipe recipe)=>recipe?.ingredients?.Any(value=>value?.itemValue!=null&&
        new[]{RebirthRemoteResourcePlacedStationPaidRow.JobKey,RebirthRemoteResourcePlacedStationPaidRow.SourceKey,RebirthRemoteResourcePlacedStationPaidRow.BindingKey}.Any(key=>value.itemValue.HasMetadata(key)))==true;
    internal sealed class CancellationClient
    {
        internal EntityPlayerLocal Actor;internal World OriginalWorld;internal RebirthRemoteResourceClientOriginal.Intent Original;internal Guid Operation,Source;
        internal string Binding,Proof,Birth,Directory,Header,Permission;internal XUiC_WorkstationWindowGroup Window;internal XUiC_RecipeStack Card;
        internal TileEntityWorkstation Station;internal Chunk Chunk;internal RebirthCamperEnergyActorImage Before,After;
        internal RebirthRemoteResourcePlacedStationPreparation.UploadRead Memory;internal int Slot,Stage,NativeOwnerId;internal float Next;
        internal CancellationOwnerProgress OwnerProgress;
        internal CancellationOwnerHydrated RestartOwner;
        internal CancellationOwnerSavedAfterHydrated RestartSavedAfterOwner;
        internal CancellationOwnerBeforeHydrated RestartBeforeOwner;
        internal RecipeQueueItem SavedPaidRow;internal string FullPaidPermission,TerminalEvidence;internal byte[] NativeAfter;internal ItemStack[] NativeInput;internal RecipeQueueItem[] NativeQueue;
        internal CancellationMetadataReentry Reentry;
        internal CancellationSavedAfterProjection SavedAfterProjection;
        internal RebirthRemoteResourceOwnerAction.PlacedCancellationSavedAfterVersion OwnerSavedAfterVersion;
        internal RebirthRemoteResourceOwnerAction.PlacedCancellationTerminalVersion OwnerTerminalVersion;
        internal readonly HashSet<string> FlushMethods=new HashSet<string>(StringComparer.Ordinal);
        internal bool FlushEntered,FlushObserved,HeaderQueued,HeaderPending,PermissionSaved,OwnerAttempted,HostTerminal,HostReleased;internal long JournalWorkspace,PermissionWorkspace,TerminalWorkspace,ReentryWorkspace;
        internal bool Current()=>RestartBeforeOwner?.Current==true||Memory?.Current==true&&Directory==GameIO.GetSaveGameDir()&&RebirthRemoteResourceClientOriginal.Current(Actor,Original)&&
            (Reentry?.Current==true||SavedAfterProjection?.Current==true||NativeBodyCurrent());
        internal bool NativeBodyCurrent()=>Actor?.world!=null&&Station!=null&&ReferenceEquals(Actor.world.GetTileEntity(Station.ToWorldPos()),Station)&&ReferenceEquals(Station.GetChunk(),Chunk)&&Chunk!=null&&!Chunk.InProgressUnloading&&
            ReferenceEquals(Window?.WorkstationData?.TileEntity,Station)&&ReferenceEquals(Window.xui?.playerUI?.entityPlayer,Actor);
        internal bool OwnerCurrent()=>Memory?.Current==true&&ReferenceEquals(GameManager.Instance?.World,OriginalWorld)&&ReferenceEquals(Actor?.world,OriginalWorld)&&
            Directory==GameIO.GetSaveGameDir()&&(RebirthRemoteResourceClientOriginal.Current(Actor,Original)||RestartOwner?.Current==true||RestartSavedAfterOwner?.Current==true||RestartBeforeOwner?.Current==true);
        internal bool NativeLease()=>RestartBeforeOwner?.FinancialCurrent==true||Current()&&NativeBodyCurrent()&&Window.windowGroup?.isShowing==true&&LockManager.Instance?.IsLockedByLocalPlayer(Station,0)==true;
    }
    private sealed class CancellationServer
    {
        internal EntityPlayer Actor;internal RebirthRemoteResourceClientOriginal.Intent Original;internal Guid Operation,Source;
        internal string Binding,Proof,Birth,Directory,Header,Permission;internal TileEntityWorkstation Station;internal Chunk Chunk;
        internal ClientInfo Peer;internal RebirthRemoteResourcePlacedStationColdRead.Original Historical;
        internal RebirthRemoteResourcePlacedStationPreparation.UploadRead Memory;internal RebirthRemoteResourceNativePublication.Watch Watch;
        internal AcceptedUpload Accepted;internal byte[] BeforeTile,AfterTile;internal RecipeQueueItem[] BeforeQueue,AfterQueue;
        internal RebirthCamperEnergyActorImage BeforeActor,AfterActor;internal ItemStack[] AfterInput;internal int Slot;
        internal CancellationSnapshotBudget SnapshotBudget;internal byte[] Marker;internal string AfterEvidence,QueueReentryCommand;internal long EvidenceWorkspace,AfterEvidenceBudget,NativeLoadWorkspace,SavedAfterWorkspace,SavedAfterReadFloor,BeforeReentryReadFloor,InputReentryWorkspace;internal bool RefundQualified,NativeLoadReserved;
        internal bool QueueReentryAttempted,QueueReentryCompleted;internal CancellationQueueRecoveryPublication QueueRecoveryPublication;
        internal CancellationHostCustody HostCustody;internal volatile bool CustodyEscaped;
        internal CancellationOrigin Origin;
        internal CancellationLoadOriginal LoadOriginal;internal CancellationLoaded Loaded;
        internal CancellationReceipt1Source Receipt1Source;internal string Receipt1BeforeEvidence;
        internal CancellationBeforeTwoPublication BeforeTwoPublication;
        internal bool QueueAttempted,QueueCompleted,BeforeSaved,AfterSaved,FinalReplyPending;
        internal bool Current()=>Memory?.Current==true&&Origin?.Current==true&&Directory==GameIO.GetSaveGameDir()&&ServerScope(Actor,Original)&&
            ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(Actor.entityId),Peer)&&
            (Receipt1Source?.Current==true||RebirthRemoteResourcePlacedStationScope.TryResolve(Actor,Station.ToWorldPos(),Original.Creation,Birth,out var actual)&&ReferenceEquals(actual,Station))&&
            ReferenceEquals(Station.GetChunk(),Chunk)&&Chunk!=null&&!Chunk.InProgressUnloading;
        internal bool FullTile(byte[] expected)=>Current()&&RebirthRemoteResourcePlacedStationCandidate.TrySerialize(Station,Station.Input,Station.Queue,Station.IsBurning,out var actual,out _)&&actual.SequenceEqual(expected);
    }
    // Private warm provenance retained after full Source4/artifact/publication
    // readback. It cannot reconstruct a body or grant a cold native effect.
    // The new cancellation permission retains the complete financial images.
    private sealed class CancellationOrigin
    {
        private readonly EntityPlayer actor;private readonly World world;private readonly string directory,owner,creation,worldKey,header;
        private readonly int deaths;private readonly Guid source;private readonly string binding,proof,original;
        private CancellationOrigin(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image full,string proof,string header)
        {this.actor=actor;world=actor.world;directory=GameIO.GetSaveGameDir();owner=root.Owner;creation=root.Creation;worldKey=root.World;deaths=full.Original.Deaths;source=root.Operation;binding=root.Binding;this.proof=proof;original=full.Original.Digest;this.header=header;}
        internal static CancellationOrigin Capture(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourcePlacedStationArtifactReader.Image full,
            RebirthRemoteResourcePlacedStationReference.Image reference,string proof,string header)
        {
            if(root?.Phase!=4||(string)root.Copy().Attribute("disposition")!="used"||(string)root.Copy().Attribute("ownerProof")!=proof||
                reference.Source!=root.Operation||reference.Binding!=root.Binding||reference.Original!=full.Original.Digest||!ServerScope(actor,full.Original))return null;
            var request=RebirthCookingRemoteAdmission.ReadProtocolXml(header,MaximumResultBytes);
            if(!XNode.DeepEquals(request.Element("fetchClientOriginal"),full.Original.Copy())||request.Element("paidOriginal")?.Value!=CanonicalPermission(ReplyBody(root,full,reference,true,false)))return null;
            return new CancellationOrigin(actor,root,full,proof,header);
        }
        internal bool Current=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&directory==GameIO.GetSaveGameDir()&&
            ReferenceEquals(world.GetEntity(actor.entityId),actor)&&actor.IsSpawned()&&!actor.IsDead()&&actor.Died==deaths&&RebirthOriginalActionScope.Matches(actor,owner,creation)&&
            RemoteResourceWorldIdentity.TryGet(world,out string actual)&&actual==worldKey;
    }
    private static readonly Dictionary<Guid,CancellationClient> cancellationClients=new Dictionary<Guid,CancellationClient>();
    private static readonly Dictionary<Guid,CancellationServer> cancellationServers=new Dictionary<Guid,CancellationServer>();
    [ThreadStatic]private static CancellationClient cancellationFlush;
    private sealed class CancellationStationPermit
    {internal CancellationServer Original;internal string Method;internal object Value;internal bool Used;}
    [ThreadStatic]private static CancellationStationPermit cancellationStationPermit;
    private static bool cancellationInstalled;
    private static World cancellationWorld;private static string cancellationDirectory;
    private static RebirthRemoteResourceClientOriginalJournal CancellationJournal(Guid operation,string purpose,int imageBytes=MaximumResultBytes,RebirthRemoteResourcePlacedStationPreparation.UploadRead terminalRead=null,long terminalReadBase=-1)
    {
        if(operation==Guid.Empty||imageBytes<1||imageBytes>MaximumResultBytes||!new[]{"Client","Accepted","Before","Permission","Mutation","QueueReentry","OwnerPlan","OwnerSlot","OwnerCheckpoint","After","Terminal","FinalAck"}.Contains(purpose))throw new InvalidDataException();
        string root=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellation"+purpose),prefix=operation.ToString("N");
        CompletionAncestors(root);int count=0;long readBytes=0;
        foreach(string folder in new[]{root,Path.Combine(root,"completed"),Path.Combine(root,"completed",prefix),Path.Combine(root,"failedCandidates")})
        {
            CompletionAncestors(folder);if(!Directory.Exists(folder))continue;
            foreach(string path in Directory.EnumerateFileSystemEntries(folder,prefix+"*"))
            {
                if(++count>32)throw new InvalidDataException();
                if(folder==Path.Combine(root,"completed")&&Path.GetFileName(path)==prefix&&Directory.Exists(path))continue;
                if(Directory.Exists(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>checked(2L*imageBytes+8192)||folder==Path.Combine(root,"failedCandidates"))throw new InvalidDataException();
                readBytes=checked(readBytes+new FileInfo(path).Length);
            }
        }
        if(terminalRead!=null&&(!terminalRead.Current||!terminalRead.Reserve(checked(terminalReadBase<0?terminalRead.ReservedBytes+4*readBytes+65536:Math.Max(terminalRead.ReservedBytes,terminalReadBase+16*readBytes+65536)))))throw new InvalidDataException();
        if(cancellationServers.TryGetValue(operation,out var server)&&server.Memory?.Current==true&&!ReserveCancellationEvidence(server,checked(4*readBytes+65536)))throw new InvalidDataException();
        if(cancellationClients.TryGetValue(operation,out var client)&&client.Memory?.Current==true)
        {
            long charge=checked(4*readBytes+65536);
            if(charge>client.JournalWorkspace){if(!client.Memory.Reserve(checked(client.Memory.ReservedBytes+charge-client.JournalWorkspace)))throw new InvalidDataException();client.JournalWorkspace=charge;}
        }
        return new RebirthRemoteResourceClientOriginalJournal(root);
    }
    private static bool CancellationFilesExist(Guid operation,string purpose)
    {
        string root=Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedCancellation"+purpose),prefix=operation.ToString("N");CompletionAncestors(root);
        foreach(string folder in new[]{root,Path.Combine(root,"completed"),Path.Combine(root,"completed",prefix),Path.Combine(root,"failedCandidates")})
        {CompletionAncestors(folder);if(Directory.Exists(folder)&&Directory.EnumerateFileSystemEntries(folder,prefix+"*").Any())return true;}
        return false;
    }
    private static bool SaveCancellation(Guid operation,Guid source,string creation,string purpose,byte[] bytes,bool applied)
    {
        if(bytes==null||bytes.Length<1||bytes.Length>MaximumResultBytes)return false;
        long charge=checked(32L*bytes.Length+65536);
        if(cancellationServers.TryGetValue(operation,out var server)&&!ReserveCancellationEvidence(server,charge))return false;
        if(cancellationClients.TryGetValue(operation,out var client)&&charge>client.JournalWorkspace)
        {if(!client.Memory.Reserve(checked(client.Memory.ReservedBytes+charge-client.JournalWorkspace)))return false;client.JournalWorkspace=charge;}
        var journal=CancellationJournal(operation,purpose,bytes.Length);var record=journal.Prepare(operation,source,creation,0,bytes,bytes);
        if(record.Vehicle!=source||record.OwnerCreation!=creation||record.Revision!=0||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(applied&&record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==(applied?RebirthCamperTransferJournal.Phase.Applied:RebirthCamperTransferJournal.Phase.Uncertain);
    }
    private static void InstallCancellation()
    {
        if(cancellationInstalled)return;RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.remote.placed.cancellation.v1"),typeof(CancellationPress));
        ModEvents.GameUpdate.RegisterHandler(CancellationUpdate);cancellationInstalled=true;
    }
    [HarmonyPatch(typeof(XUiC_RecipeStack),"HandleOnPress")]
    private static class CancellationPress
    {
        [HarmonyPriority(Priority.First)]private static bool Prefix(XUiC_RecipeStack __instance,bool __runOriginal)
        {
            if(!CancellationCandidate(__instance?.GetRecipe()))return true;
            // Candidate refusal never falls through to native multiplication
            // of total paid fragments. Only this purpose's producer may refund.
            if(__runOriginal&&ProtocolActive)BeginCancellation(__instance);return false;
        }
    }
    internal static bool CancellationBlocks(TileEntityWorkstation station)=>station!=null&&
        (HasCancellationHostCustody(station)||cancellationServers.Values.Any(value=>value.Accepted!=null&&value.Memory?.Current==true&&value.Directory==GameIO.GetSaveGameDir()&&
            (ReferenceEquals(value.Station,station)||ReferenceEquals(value.LoadOriginal?.Loaded?.Tile,station)&&value.LoadOriginal.Loaded.Installed()))||
         restartCancellations.Values.Any(value=>value.Memory?.Current==true&&ReferenceEquals(value.Load?.Loaded?.Tile,station)&&value.Load.Loaded.Installed())||
         cancellationClients.Values.Any(value=>value.Stage!=0&&ReferenceEquals(value.Station,station)&&value.Current()));
    internal static bool ConsumeCancellationStationSetter(TileEntityWorkstation station,MethodBase method,object[] arguments)
    {
        var once=cancellationStationPermit;
        if(once==null||once.Used||!ReferenceEquals(once.Original.Station,station)||!once.Original.Current()||method?.DeclaringType!=typeof(TileEntityWorkstation)||method.Name!=once.Method)return false;
        if(once.Method==nameof(TileEntityWorkstation.ResetTickTime)){if(arguments?.Length!=0)return false;}
        else if(arguments?.Length!=1||!ReferenceEquals(once.Value,arguments[0]))return false;
        once.Used=true;return true;
    }
    private static bool CancellationNativeSet(CancellationServer original,string method,object value,Action invoke)
    {
        if(cancellationStationPermit!=null||!original.Current())return false;
        var once=new CancellationStationPermit{Original=original,Method=method,Value=value};cancellationStationPermit=once;
        try{invoke();return once.Used&&original.Current();}finally{cancellationStationPermit=null;}
    }
}
