using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private sealed class CancellationRestartArchive:IDisposable
    {
        internal RebirthRemoteResourceClientOriginal.Intent Original;internal Guid Operation,Source;internal string Header,Binding,Birth,Proof,Permission,BeforeEvidence,AfterEvidence,AcceptedEvidence,ExpectedAck;
        internal RebirthCamperEnergyActorImage Before,After;internal int NativeOwner;internal byte[] NativeAfter;internal CancellationNativeEvidence SavedNative;
        internal RebirthRemoteResourcePlacedStationPreparation.UploadRead Memory;internal CancellationLoadOriginal Load;internal bool AckSaved,ReplyPending,SavedAfterRestart,BeforeRestart,BeforeUncertain;internal CancellationHostCustody HostCustody;internal long ReadBase,ReplyWorkspace;
        public void Dispose()
        {
            if(restartCancellations.TryGetValue(Operation,out var current)&&ReferenceEquals(current,this))restartCancellations.Remove(Operation);
            lock(cancellationLoadGate)if(Load!=null)
            {
                var token=Load;token.Active=false;token.Retirement?.Close();cancellationLoads.Remove(token);token.Loaded=null;token.Original=null;token.Restart=null;token.Memory=null;token.Intent=null;
                token.World=null;token.Provider=null;token.Manager=null;token.Util=null;token.Access=null;token.OldChunk=null;token.OldTile=null;
                token.Snapshot=null;token.Tile=null;token.Header=null;token.Permission=null;token.Evidence=null;Load=null;
            }
            RetireRestartBeforeCustody(this);
            var memory=Memory;Memory=null;Original=null;Before=null;After=null;NativeAfter=null;SavedNative=null;Header=null;Permission=null;BeforeEvidence=null;AfterEvidence=null;AcceptedEvidence=null;ExpectedAck=null;
            if(!ReplyPending)memory?.Dispose();
        }
    }
    private sealed class CancellationPendingOwner
    {internal RebirthStablePlayerIdentity Owner;internal Guid Operation;internal int Deaths,ReceiptPhase;}
    private static readonly Dictionary<Guid,CancellationRestartArchive> restartCancellations=new Dictionary<Guid,CancellationRestartArchive>();
    private static void InstallCancellationRestart(Harmony harmony)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationRegionManager));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationPlayerJoin));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationOwnerHydration));
    }
    internal static void InstallHireNativeHydrationObservers()
    {
        var harmony=new Harmony("rebirth.remote.placed.cancellation.native.reconstruction");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationPlayerJoin));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(CancellationOwnerHydration));
    }
    private static void DropCancellationRestart()
    {foreach(var value in restartCancellations.Values.ToArray())value.Dispose();}
    private static bool OpenRestartSource(RebirthRemoteResourceClientOriginal.Intent original,Guid source,out RebirthRemoteResourcePlacedStationColdRead.Original cold)
    {
        cold=null;RebirthRemoteResourcePlacedStationPreparation.ColdRead allocation=null;
        try
        {
            var world=GameManager.Instance?.World;
            if(world?.IsRemote()!=false||original.World!=world.worldState.Guid||!RemoteResourceWorldIdentity.TryGet(world,out string identity)||
                !RebirthRemoteResourcePlacedStationArtifactStore.TryReadHeader(source,out var header)||!RebirthRemoteResourcePlacedStationPreparation.TryReserveColdRead(header,out allocation)||
                !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.HistoricalSource,out var scratch))return false;
            RebirthRemoteResourceDurableJournal.Record root;
            using(scratch)if(!RebirthRemoteResourceDurableJournal.TryReadPlacedHistoryBounded(RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner),source,header,out root,out bool corrupt)||corrupt)return false;
            if(root==null||root.Phase!=4||!IsMappedRoot(root)||root.World!=identity||root.Owner!=RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner)||root.Creation!=original.Creation||
                !RebirthRemoteResourcePlacedStationPrepared.TryRead(root,out var artifact,out string digest,null,allocation)||
                !XNode.DeepEquals(artifact.Original.Copy(),original.Copy()))return false;
            cold=new RebirthRemoteResourcePlacedStationColdRead.Original(allocation,root,artifact,digest);allocation=null;return true;
        }
        catch{return false;}
        finally{allocation?.Dispose();}
    }
    private static bool ReadRestartHeader(Guid operation,RebirthRemoteResourcePlacedStationPreparation.UploadRead memory,out XElement request,out string header,
        out Guid source,out long headerWorkspace,out long originalWorkspace)
    {
        request=null;header=null;source=Guid.Empty;headerWorkspace=originalWorkspace=0;
        var record=CancellationJournal(operation,"Permission",MaximumResultBytes,memory).Read(operation);
        if(record?.State!=RebirthCamperTransferJournal.Phase.Uncertain||record.Vehicle==Guid.Empty||record.Revision!=0||!record.Before.SequenceEqual(record.After)||
            !memory.Reserve(checked(memory.ReservedBytes+12L*record.Before.Length+65536)))return false;
        var permission=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),MaximumResultBytes);
        header=permission.Element("request")?.Value;if(header==null)return false;request=RebirthCookingRemoteAdmission.ReadProtocolXml(header,MaximumResultBytes);
        if(request.Name!="placedStationCancelRequest"||request.Attributes().Count()!=8||request.Elements().Count()!=5||(int?)request.Attribute("version")!=1||(int?)request.Attribute("stage")!=0||
            (string)request.Attribute("cancel")!=operation.ToString("N")||!Guid.TryParseExact((string)request.Attribute("source"),"N",out source)||source!=record.Vehicle||
            !RebirthRemoteResourcePlacedStationAllocation.TryAdmitCancellationBeforeHydration(request,permission,out headerWorkspace)||
            !RebirthRemoteResourcePlacedStationAllocation.TryAdmitIntentBeforeHydration(request.Element("fetchClientOriginal"),out originalWorkspace)||
            !memory.Reserve(checked(memory.ReservedBytes+headerWorkspace+originalWorkspace-2*RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes)))return false;
        return true;
    }
    private static CancellationPendingOwner[] ReadPendingCancellationOwners()
    {
        // The established native final-file reader remains capped at 16MiB.
        // Charge the entire supported operation workspace while its detached
        // native parser runs; return only compact selection IDs, never image
        // hashes or a financial permission derived from this discovery scan.
        if(!RebirthRemoteResourcePlacedStationPreparation.TryReserveCancellationRead(Guid.NewGuid(),RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace,out var memory))return null;
        using(memory)
        {
            if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out var scratch))return null;
            using(scratch)
            {
            var players=GameManager.Instance.GetPersistentPlayerList()?.Players;if(players==null||players.Count>4096)return null;
            var result=new List<CancellationPendingOwner>();var owners=new HashSet<string>(StringComparer.Ordinal);var operations=new HashSet<Guid>();
            foreach(var persistent in players.Values)
            {
                if(!RebirthStablePlayerIdentity.TryFromPersistentPlayer(persistent,out var owner)||!owners.Add(owner.StorageKey))return null;
                if(!RebirthCamperEnergySavedOwner.TryRead(owner,out _,out var saved))continue;
                if(!RebirthRemoteResourcePlacedStationSavedReceipt.TryCancellationTerminalKeys(saved,out var terminal)||
                    !RebirthRemoteResourcePlacedStationSavedReceipt.TryCancellationSavedAfterKeys(saved,out var savedAfter)||!RebirthRemoteResourcePlacedStationSavedReceipt.TryCancellationBeforeKeys(saved,out var before)||terminal.Length+savedAfter.Length+before.Length>32)return null;
                var pending=terminal.Concat(savedAfter).Concat(before);
                foreach(var operation in pending)
                {if(result.Count>=32||!operations.Add(operation))return null;result.Add(new CancellationPendingOwner{Owner=owner,Operation=operation,Deaths=saved.deaths,ReceiptPhase=terminal.Contains(operation)?3:savedAfter.Contains(operation)?2:1});}
            }
            return result.ToArray();
            }
        }
    }
    private static void DiscoverCancellationRestart(RegionFileManager manager)
    {
        if(!ProtocolActive||!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||!AllowsReviewedPlacedMode||
            GameManager.Instance.World.ChunkCache?.ChunkProvider is not ChunkProviderGenerateWorld provider||manager.snapshotUtil is not ChunkSnapshotUtil util||
            !ReferenceEquals(manager.regionFileAccess,util.regionFileAccess)||!ReferenceEquals(util.chunkReader.regionFileAccess,manager.regionFileAccess)||
            Path.GetFullPath(manager.saveDirectory)!=Path.GetFullPath(GameIO.GetSaveGameRegionDir()))return;
        BindCancellationWorld();var pending=ReadPendingCancellationOwners();if(pending==null)return;
        foreach(var candidate in pending)
        {
            RebirthRemoteResourcePlacedStationPreparation.UploadRead memory=null;RebirthRemoteResourcePlacedStationColdRead.Original cold=null;CancellationRestartArchive archive=null;
            RebirthRemoteResourcePlacedStationAllocation.Scratch scratch=null;
            try
            {
                if(restartCancellations.ContainsKey(candidate.Operation)||cancellationServers.ContainsKey(candidate.Operation)||
                    !RebirthRemoteResourcePlacedStationPreparation.TryReserveCancellationRead(candidate.Operation,RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes,out memory)||
                    !RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.OwnerEnvelope,out scratch)||
                    !ReadRestartHeader(candidate.Operation,memory,out var request,out string header,out var source,out long headerWorkspace,out long originalWorkspace)||
                    !RebirthRemoteResourceClientOriginal.TryDecodeOriginal(request.Element("fetchClientOriginal").ToString(SaveOptions.DisableFormatting),out var original)||
                    original.Owner!=candidate.Owner.CanonicalId||original.Deaths!=candidate.Deaths||original.Creation!="native-mode:"+candidate.Owner.CanonicalId||
                    CancellationOperation(original.Operation)!=candidate.Operation||!OpenRestartSource(original,source,out cold))continue;
                var supplied=memory;memory=null;var historical=cold;cold=null;
                scratch.Dispose();scratch=null;
                archive=ReadCancellationTerminalArchive(null,request,header,candidate.Operation,source,headerWorkspace,originalWorkspace,supplied,historical,candidate.ReceiptPhase==1,candidate.ReceiptPhase==2);
                if(archive==null||!ValidateInput(archive.Original,out var position,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _)||
                    !(archive.BeforeRestart?SavedRestartBeforeOwner(candidate,archive):SavedRestartOwnerMatches(candidate,archive))||
                    !archive.Memory.Reserve(checked(archive.Memory.ReservedBytes+16L*(archive.SavedNative.Snapshot.Length+archive.SavedNative.Tile.Length+Encoding.UTF8.GetByteCount(header))+65536)))continue;
                archive.SavedAfterRestart=candidate.ReceiptPhase==2&&!archive.BeforeRestart;
                var world=GameManager.Instance.World;
                var token=new CancellationLoadOriginal{Restart=archive,World=world,WorldId=world.worldState.Guid,Intent=archive.Original,Memory=archive.Memory,Operation=archive.Operation,Source=archive.Source,
                    Binding=archive.Binding,Birth=archive.Birth,Recovery=Guid.NewGuid(),Provider=provider,Manager=manager,Util=util,Access=manager.regionFileAccess,Directory=Path.GetFullPath(manager.saveDirectory),
                    Header=archive.Header,Permission=archive.Permission,Evidence=archive.BeforeRestart?archive.BeforeEvidence:archive.AfterEvidence,Receipt1Before=archive.BeforeRestart,Snapshot=archive.SavedNative.Snapshot,Tile=archive.SavedNative.Tile,Offset=archive.SavedNative.Offset,X=position.x>>4,Z=position.z>>4};
                token.Retirement=new CancellationLoadRetirement{Custody=archive.HostCustody};if(!archive.Memory.RetainNativeBuffers(token.Retirement))continue;
                archive.ReadBase=archive.Memory.ReservedBytes;archive.Load=token;lock(cancellationLoadGate)cancellationLoads.Add(token);restartCancellations.Add(archive.Operation,archive);archive=null;
            }
            catch{/* No terminal body is reconstructed from partial discovery. */}
            finally{scratch?.Dispose();archive?.Dispose();cold?.Dispose();memory?.Dispose();}
        }
    }
    private static bool CurrentTerminalOwner(EntityPlayer actor,CancellationRestartArchive archive)
    {
        return ServerScope(actor,archive.Original)&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&
            RebirthCamperEnergySavedOwner.TryRead(owner,out var image,out var saved)&&saved.id==actor.entityId&&saved.deaths==archive.Original.Deaths&&archive.After.Matches(image)&&
            RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,CancellationReceipt(archive.Operation),out float phase,out bool present)&&present&&phase==3f;
    }
    private static void ReplyArchivedCancellation(EntityPlayer actor,XElement request,string header,Guid operation,Guid source,long headerWorkspace,long originalWorkspace)
    {
        var archive=ReadCancellationTerminalArchive(actor,request,header,operation,source,headerWorkspace,originalWorkspace);if(archive==null)return;
        try{SendRestartTerminal(actor,archive,null);}finally{if(!archive.ReplyPending)archive.Dispose();}
    }
    private static bool ReplyRestartCancellation(EntityPlayer actor,string header,Guid operation)
    {
        if(!restartCancellations.TryGetValue(operation,out var archive))return false;
        if(archive.Header!=header||archive.ReplyPending)return true;
        var loaded=archive.Load?.Loaded;SendRestartTerminal(actor,archive,loaded?.Installed()==true?loaded:null);return true;
    }
    private static void SendRestartTerminal(EntityPlayer actor,CancellationRestartArchive archive,CancellationLoaded loaded)
    {
        if(archive.SavedAfterRestart&&CurrentSavedAfterRestartOwner(actor,archive))
        {SendRestartSavedAfter(actor,archive,loaded);return;}
        if(archive.Memory?.Current!=true||!CurrentTerminalOwner(actor,archive)||archive.Load!=null&&!RevalidateRestartTerminal(archive)||loaded!=null&&!ValidateLoadedRestartImage(archive,loaded))return;
        if(!archive.AckSaved&&!archive.Memory.Reserve(checked(archive.Memory.ReservedBytes+32L*Encoding.UTF8.GetByteCount(archive.ExpectedAck)+65536)))return;
        if(!SaveCancellation(archive.Operation,archive.Source,archive.Original.Creation,"FinalAck",Encoding.UTF8.GetBytes(archive.ExpectedAck),true))return;
        archive.AckSaved=true;
        string reconstruction=loaded?.Encode();long bound=checked(1024+EscapedCancellationBytes(archive.Permission)+EscapedCancellationBytes(archive.AfterEvidence)+(reconstruction==null?0:EscapedCancellationBytes(reconstruction)));
        if(loaded!=null)ObserveCancellationScenarioEvent(archive.Operation,"actualNativeAfterLoad3",reconstruction);
        if(HoldCancellationScenario(archive.Operation,"terminalAck"))return;
        if(bound>MaximumResultBytes)return;
        long workspace=checked(8*bound+65536);
        if(workspace>archive.ReplyWorkspace){if(!archive.Memory.Reserve(checked(archive.Memory.ReservedBytes+workspace-archive.ReplyWorkspace)))return;archive.ReplyWorkspace=workspace;}
        var reply=new XElement("placedStationCancelResult",new XAttribute("version",1),new XAttribute("original",archive.Original.Operation.ToString("N")),
            new XAttribute("cancel",archive.Operation.ToString("N")),new XAttribute("phase",5),new XAttribute("grant",false),new XElement("permission",archive.Permission),new XElement("evidence",archive.AfterEvidence));
        if(reconstruction!=null)reply.Add(new XElement("reconstruction",reconstruction));
        string text=reply.ToString(SaveOptions.DisableFormatting);if(Encoding.UTF8.GetByteCount(text)>MaximumResultBytes)return;
        var retained=archive.Memory;
        archive.ReplyPending=NetPackageRebirthRemoteRepairResult.SendObserved(actor.entityId,archive.Original,text,sent=>
        {archive.ReplyPending=false;archive.Dispose();retained.Dispose();});
    }
    private static bool RevalidateRestartTerminal(CancellationRestartArchive archive)
    {
        if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(archive.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Completion,out var scratch))return false;
        using(scratch)
        {
            foreach(string purpose in new[]{"Permission","Accepted","Before","Mutation","After","Terminal"})
            {
                string text=purpose=="Permission"?archive.Permission:purpose=="Accepted"?archive.AcceptedEvidence:purpose=="Before"?archive.BeforeEvidence:purpose=="Mutation"?Encoding.UTF8.GetString(CancellationMutation(archive.Operation,archive.Source,archive.Binding,archive.Permission)):purpose=="After"?archive.AfterEvidence:Encoding.UTF8.GetString(CancellationServerTerminalBytes(archive.Operation,archive.Source,archive.Binding,archive.Permission,archive.AfterEvidence));
                var record=CancellationJournal(archive.Operation,purpose,MaximumResultBytes,archive.Memory,archive.ReadBase).Read(archive.Operation);var bytes=Encoding.UTF8.GetBytes(text);
                if(record==null||record.Vehicle!=archive.Source||record.OwnerCreation!=archive.Original.Creation||record.Revision!=0||
                    record.State!=(purpose=="Permission"?RebirthCamperTransferJournal.Phase.Uncertain:RebirthCamperTransferJournal.Phase.Applied)||!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
            }
        }
        if(!OpenRestartSource(archive.Original,archive.Source,out var history))return false;
        using(history)
        {
            var root=history.Source;var full=history.Artifact;
            if((string)root.Copy().Attribute("disposition")!="used"||root.Binding!=archive.Binding||full.Birth!=archive.Birth||
                !RebirthRemoteResourcePlacedStationReference.TryRead(root.Copy().Element("ownerAction")?.Element("original")?.Value,out var reference)||
                reference.Artifact!=history.ArtifactDigest||reference.Source!=archive.Source||reference.Job!=archive.Original.Operation||reference.Binding!=archive.Binding||
                !ObserveColdAccepted(history,reference)||!TryReadCompletion(root,full,reference,null,out string proof,false,history.Allocation)||proof!=archive.Proof||
                (string)root.Copy().Attribute("ownerProof")!=proof||
                RebirthCookingRemoteAdmission.ReadProtocolXml(archive.Header,MaximumResultBytes).Element("paidOriginal")?.Value!=CanonicalPermission(ReplyBody(root,full,reference,true,false)))return false;
        }
        return archive.Memory.Current;
    }
    private static bool ValidateLoadedRestartImage(CancellationRestartArchive archive,CancellationLoaded loaded)
    {
        if(!loaded.Installed()||!ReferenceEquals(archive.Load?.Loaded,loaded)||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(archive.Memory,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.NativeBefore,out var native))return false;
        using(native)return RebirthRemoteResourcePlacedStationAllocation.TryAdmitUploadTargetBeforeCapture(archive.Load.Loaded.Tile,RebirthRemoteResourcePlacedStationAllocation.SharedReaderScratchBytes,out long workspace)&&
            archive.Memory.Reserve(Math.Max(archive.Memory.ReservedBytes,checked(archive.ReadBase+workspace)))&&CaptureUploadNative(archive.Load.Loaded.Tile,out var actual)&&actual.SequenceEqual(archive.SavedNative.Tile);
    }
    [HarmonyPatch(typeof(RegionFileManager),MethodType.Constructor,new[]{typeof(string),typeof(string),typeof(int),typeof(bool)})]
    private static class CancellationRegionManager
    {
        private static void Postfix(RegionFileManager __instance)
        {
            try{RemoteResourceTransactions.DiscoverActionPaymentNativeTargets(__instance);}catch{}
            try{DiscoverSource4NativeOrigins(__instance);}catch{/* Independent saved enqueue source discovery. */}
            try{RebirthProductiveEarnedUiQueueOwner.DiscoverInitialQueueNativeOrigins(__instance);}catch{/* Independent actual earlier queue insertion. */}
            try{RebirthRecipeOutputIntegration.DiscoverStationOutputNativeSources(__instance);}catch{/* Independent readonly recipe-output native source discovery. */}
            try{DiscoverCancellationRestart(__instance);}catch{/* Readonly startup discovery cannot fail native region construction. */}
        }
    }
}
