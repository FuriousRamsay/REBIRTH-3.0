using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

// Process-local strong custody of the EXACT qualified group. Evidence on disk is
// forensic only: it cannot recreate a World, actor, callback or restore authority.
// No lifecycle activation until the complete caller/owner reentry trace qualifies.
internal static partial class RebirthNpcSaveQuarantineBundle
{
    private sealed class Retained
    {
        internal Guid Id;internal World Original,Admission;internal string Directory,Fingerprint,EvidencePath;
        internal ulong Generation;internal RebirthNpcSaveQuarantineCapture.Snapshot Capture;
        internal RebirthNpcSaveQuarantineDomain[] Domains;internal RebirthNpcPersistenceFile.QuarantineWriterFence Fence;
        internal XmlDocument Evidence;internal bool Detached,RollbackIncomplete;
        internal RebirthNpcNetworkEpoch.ClientProjectionRetirement ClientRetirement;
        internal bool ClientFinancialCustody;
        internal RebirthNpcTradeClientRetirement TradeRetirement;
        internal RebirthNpcTradeOwnerLoad.Retirement TradeObservationRetirement;
        // This covers only the server's local financial originals. Remote
        // preparations and all other domains remain independently retained.
        internal RebirthNpcTradeClientRetirement ServerLocalTradeRetirement;
        internal RebirthNpcTradeServerRetirement ServerPreparationRetirement;
        internal RebirthNpcTradeLocalOwnerLoad.Retirement ServerTradeObservationRetirement;
        internal RebirthNpcTradeIssuedRetirement ServerIssuedRetirement;
        internal RebirthNpcTradeOriginal[] ServerLocalReentryOriginals,ServerHoldReentryOriginals;
        internal RebirthNpcSaveQuarantineSavedAdoption ServerSavedAdoption;
        internal RebirthNpcRetainedWorkCustody ServerWorkCustody;
        internal RebirthNpcRetainedControllerCustody ServerControllerCustody;
        internal RebirthNpcRetainedWorkPackages ServerWorkPackages;
        internal RebirthNpcRuntimeContinuationAdoption ServerAdvancedAdoption,ServerCheckpointAdoption;
        internal RebirthNpcRuntimeContinuationAdoption ServerDecisionAdoption,ServerSignalsAdoption;
        internal RebirthNpcRuntimeContinuationAdoption ServerOutcomeAdoption;
        internal RebirthNpcSaveQuarantineNativeSavedStore ServerIdentityAdoption,ServerWorldAdoption;
        internal RebirthNpcRuntimeContinuationAdoption[] ServerNativeOwnerAdoptions;
        internal RebirthNpcOwnerNativeContinuationAdoption[] ServerPlayerOwnerAdoptions;
        internal RebirthNpcNativeTransientAdoption[] ServerTransientAdoptions;
        internal HashSet<RebirthNpcStableId> ServerTradeNpcIds;
        internal RebirthNpcServerNativeAdmission ServerIncomingAdmission;
        internal RebirthNpcTradeOriginal[] ClientNativeOriginals,ClientNativeHolds;
        internal RebirthNpcClientNativeReentry.Source ClientIncomingSource;
        internal RebirthNpcRuntimeNativeAdoption.NativeReentry ServerRuntimeStagingSource;
        internal RebirthNpcRuntimeNativeAdoption.Outgoing ServerRuntimeStagingOriginal;
        internal RebirthNpcRuntimeNativeAdoption.Outgoing[] ServerRuntimeOriginals;
        internal readonly Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry> ServerRuntimeReentries=
            new Dictionary<RebirthNpcStableId,RebirthNpcRuntimeNativeAdoption.NativeReentry>();
        internal readonly Dictionary<Guid,RebirthNpcTradeLocalOwnerLoad.NativeReentry> ServerNativeReentries=
            new Dictionary<Guid,RebirthNpcTradeLocalOwnerLoad.NativeReentry>();
    }
    private static readonly List<Retained> retained=new List<Retained>();
    private static bool transferring;
    private static ClientBoundary clientBoundary;
    private static ServerBoundary serverBoundary;
    internal sealed class ServerBoundary
    {
        private readonly object bundle;
        private readonly World outgoing,incoming;
        private readonly string oldDirectory,newDirectory;
        private readonly bool incomingRemote;
        private readonly RebirthNpcSaveQuarantineDomain[] domains;
        internal ServerBoundary(object bundle,World outgoing,string oldDirectory,World incoming,string newDirectory,RebirthNpcSaveQuarantineDomain[] domains)
        {this.bundle=bundle;this.outgoing=outgoing;this.oldDirectory=CanonicalDirectory(oldDirectory);this.incoming=incoming;this.newDirectory=CanonicalDirectory(newDirectory);this.domains=domains;incomingRemote=incoming.IsRemote();}
        internal bool Current(World original,string originalDirectory,World current,string currentDirectory)
            =>CurrentCore(original,originalDirectory,current,currentDirectory,false);
        internal bool CanInstallIncomingEpoch(World original,string originalDirectory,World current,string currentDirectory)
            =>incomingRemote&&CanStageRetainedClientDestination(current,currentDirectory)&&CurrentCore(original,originalDirectory,current,currentDirectory,true);
        internal bool CanInstallIncomingServerEpoch(World original,string originalDirectory,World current,string currentDirectory)
            =>!incomingRemote&&CanStageRetainedServerDestination(current,currentDirectory)&&CurrentCore(original,originalDirectory,current,currentDirectory,false,true);
        internal bool ObservesIncomingServer(World current,string directory)=>CanInstallIncomingServerEpoch(outgoing,oldDirectory,current,directory);
        internal bool ObservesIncomingClient(World current,string directory)=>CanInstallIncomingEpoch(outgoing,oldDirectory,current,directory);
        private bool CurrentCore(World original,string originalDirectory,World current,string currentDirectory,bool allowRetainedClient,bool allowRetainedServer=false)
        {
            if(!ThreadManager.IsMainThread()||!AdmissionEnabled||transferring||!ReferenceEquals(serverBoundary,this)||
                !ReferenceEquals(outgoing,original)||!ReferenceEquals(incoming,current)||!ReferenceEquals(current,GameManager.Instance?.World)||
                current==null||current.IsRemote()!=incomingRemote||!IncomingRoleCurrent(current)||
                !SameDirectory(oldDirectory,originalDirectory)||!SameDirectory(newDirectory,currentDirectory)||!SameDirectory(newDirectory,GameIO.GetSaveGameDir())||
                HasRetainedOriginal(current,newDirectory)&&!HasQualifiedIncomingServer(current,newDirectory)&&!HasQualifiedIncomingClient(current,newDirectory)&&
                    !(allowRetainedClient&&CanStageRetainedClientDestination(current,newDirectory)||allowRetainedServer&&CanStageRetainedServerDestination(current,newDirectory)))return false;
            foreach(var value in retained)
                if(ReferenceEquals(value,bundle))return value.Detached&&!value.RollbackIncomplete&&ReferenceEquals(value.Original,outgoing)&&
                    ReferenceEquals(value.Admission,incoming)&&SameDirectory(value.Directory,oldDirectory)&&ReferenceEquals(value.Domains,domains)&&
                    domains!=null&&domains.Length!=0&&Array.TrueForAll(domains,domain=>domain!=null&&domain.IsDetached);
            return false;
        }
        internal void Commit(World original,string originalDirectory,World current,string currentDirectory)
        {
            if(!Current(original,originalDirectory,current,currentDirectory))throw new InvalidOperationException("Typed server boundary changed before lifecycle commit");
            serverBoundary=null;
        }
    }
    private static string CanonicalDirectory(string directory)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathRooted(directory))return null;
            string root=Path.GetPathRoot(directory)?.Replace(Path.AltDirectorySeparatorChar,Path.DirectorySeparatorChar);
            if(string.IsNullOrEmpty(root))return null;
            if(Path.DirectorySeparatorChar=='\\'&&!(root.Length==3&&char.IsLetter(root[0])&&root[1]==':'&&root[2]=='\\'||
                root.StartsWith("\\\\",StringComparison.Ordinal)&&root.Substring(2).Split(new[]{'\\'},StringSplitOptions.RemoveEmptyEntries).Length>=2))return null;
            string full=Path.GetFullPath(directory);
            return string.Equals(full,root,Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)?full:full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        }
        catch{return null;}
    }
    private static bool SameDirectory(string left,string right)
    {string first=CanonicalDirectory(left),second=CanonicalDirectory(right);return first!=null&&second!=null&&
        string.Equals(first,second,Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);}
    private static bool IncomingRoleCurrent(World world)
    {var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;return world!=null&&manager!=null&&manager.IsServer!=world.IsRemote();}
    internal static ServerBoundary CaptureServerBoundary(World original,string originalDirectory,World incoming,string incomingDirectory)
    {var value=serverBoundary;return value?.Current(original,originalDirectory,incoming,incomingDirectory)==true?value:null;}
    internal sealed class ClientBoundary
    {
        private readonly World outgoing,incoming;private readonly string oldDirectory,newDirectory;
        internal ClientBoundary(World outgoing,string oldDirectory,World incoming,string newDirectory)
        {this.outgoing=outgoing;this.oldDirectory=oldDirectory;this.incoming=incoming;this.newDirectory=newDirectory;}
        internal bool MatchesOutgoing(World world,string directory)=>ReferenceEquals(clientBoundary,this)&&ReferenceEquals(outgoing,world)&&
            SameDirectory(oldDirectory,directory)&&ReferenceEquals(incoming,GameManager.Instance?.World)&&SameDirectory(newDirectory,GameIO.GetSaveGameDir());
        internal bool ObservesIncomingClient(World current,string directory)=>CanInstallIncomingEpoch(outgoing,oldDirectory,current,directory);
        internal bool Current(World original,string originalDirectory,World current,string currentDirectory)=>ThreadManager.IsMainThread()&&
            AdmissionEnabled&&!transferring&&IncomingRoleCurrent(current)&&MatchesOutgoing(original,originalDirectory)&&ReferenceEquals(incoming,current)&&
            SameDirectory(newDirectory,currentDirectory)&&(!HasRetainedOriginal(current,currentDirectory)||HasQualifiedIncomingClient(current,currentDirectory)||HasQualifiedIncomingServer(current,currentDirectory));
        internal bool CanInstallIncomingEpoch(World original,string originalDirectory,World current,string currentDirectory)=>ThreadManager.IsMainThread()&&
            AdmissionEnabled&&!transferring&&current?.IsRemote()==true&&IncomingRoleCurrent(current)&&MatchesOutgoing(original,originalDirectory)&&
            ReferenceEquals(incoming,current)&&SameDirectory(newDirectory,currentDirectory)&&CanStageRetainedClientDestination(current,currentDirectory);
        internal bool CanInstallIncomingServerEpoch(World original,string originalDirectory,World current,string currentDirectory)=>ThreadManager.IsMainThread()&&
            AdmissionEnabled&&!transferring&&current?.IsRemote()==false&&IncomingRoleCurrent(current)&&MatchesOutgoing(original,originalDirectory)&&
            ReferenceEquals(incoming,current)&&SameDirectory(newDirectory,currentDirectory)&&CanStageRetainedServerDestination(current,currentDirectory);
        internal bool ObservesIncomingServer(World current,string directory)=>CanInstallIncomingServerEpoch(outgoing,oldDirectory,current,directory);
        internal void Commit(World original,string originalDirectory,World current,string currentDirectory)
        {
            if(!Current(original,originalDirectory,current,currentDirectory))throw new InvalidOperationException("Typed client boundary changed before lifecycle commit");
            clientBoundary=null;
        }
    }
    internal static ClientBoundary CaptureClientBoundary(World original,string originalDirectory,World incoming,string incomingDirectory)
    {var value=clientBoundary;return value?.Current(original,originalDirectory,incoming,incomingDirectory)==true?value:null;}
    internal static readonly bool AdmissionEnabled=true; // source review completed; native qualification remains separate
    internal static bool CanInstallIncomingClientEpoch(World original,string originalDirectory,World incoming,string incomingDirectory)
    {
        if(incoming?.IsRemote()!=true)return false;
        return clientBoundary?.CanInstallIncomingEpoch(original,originalDirectory,incoming,incomingDirectory)==true||
            serverBoundary?.CanInstallIncomingEpoch(original,originalDirectory,incoming,incomingDirectory)==true;
    }
    internal static bool IsTransferActive=>transferring;
    internal static void ObserveTradeRelease(RebirthNpcTradeOwnerExchange.Release release)
    {
        if(!ThreadManager.IsMainThread()||transferring||release==null||!release.Current())return;
        foreach(var original in retained)
        {
            if(original.Detached&&!original.RollbackIncomplete&&original.ServerLocalTradeRetirement!=null)
                original.ServerLocalTradeRetirement.Observe(release);
            if(original.Detached&&!original.RollbackIncomplete)
                original.ServerTradeObservationRetirement?.Observe(release);
            if(original.Detached&&!original.RollbackIncomplete&&original.ClientFinancialCustody&&original.TradeRetirement!=null)
            {
                original.TradeRetirement.Observe(release);
                if(original.TradeRetirement.Complete)original.ClientFinancialCustody=false;
            }
        }
        RetryServerNativeAdmission(GameManager.Instance?.World,GameIO.GetSaveGameDir());
    }
    internal static void ObserveServerTradeCompletion(RebirthNpcTradeService.HostClosed source)
    {
        if(!ThreadManager.IsMainThread()||transferring||source?.HasSavedSocialCompletion!=true)return;
        foreach(var original in retained)if(original.Detached&&!original.RollbackIncomplete)original.ServerPreparationRetirement?.Observe(source);
        RetryServerNativeAdmission(GameManager.Instance?.World,GameIO.GetSaveGameDir());
    }
    internal static void ObserveServerTradeCompletion(RebirthNpcTradeClosedHistory source)
    {
        if(!ThreadManager.IsMainThread()||transferring||source?.HasSavedSocialCompletion!=true)return;
        foreach(var original in retained)if(original.Detached&&!original.RollbackIncomplete)original.ServerPreparationRetirement?.Observe(source);
        RetryServerNativeAdmission(GameManager.Instance?.World,GameIO.GetSaveGameDir());
    }
    internal static void ObserveServerUnusedTrade(RebirthNpcTradeService.HostUnused source)
    {
        if(!ThreadManager.IsMainThread()||transferring||source?.ReadRetained()!=true)return;
        foreach(var original in retained)if(original.Detached&&!original.RollbackIncomplete)original.ServerIssuedRetirement?.Observe(source);
        RetryServerNativeAdmission(GameManager.Instance?.World,GameIO.GetSaveGameDir());
    }
    internal static bool TryAdmitWorld(World original,string originalDirectory,World incoming,out string error)
    {
        error=string.Empty;
        if(incoming==null)return true;
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(incoming,GameManager.Instance?.World))
        {error="Actual current native admission world required";return false;}
        RetireEndedClientProjections();
        if(incoming.IsRemote()==false&&HasQualifiedIncomingServer(incoming,GameIO.GetSaveGameDir()))return true;
        if(incoming.IsRemote()&&HasQualifiedIncomingClient(incoming,GameIO.GetSaveGameDir()))return true;
        if(HasRetainedOriginal(incoming,GameIO.GetSaveGameDir()))
        {
            // Detachment and admission are separate. A returning remote owner
            // needs empty current epoch/flow slots BEFORE its real PlayerId
            // arrives. This stages the outgoing group only; it never admits
            // the retained destination or restores old callbacks into it.
            if(AdmissionEnabled&&original!=null&&incoming.IsRemote()&&!ReferenceEquals(original,incoming)&&
                CanStageRetainedClientDestination(incoming,GameIO.GetSaveGameDir()))
            {
                if(!CanInstallIncomingClientEpoch(original,originalDirectory,incoming,GameIO.GetSaveGameDir()))
                {
                    bool staged=original.IsRemote()?TryTransferClientCore(original,originalDirectory,incoming,true,out _,out error):
                        TryTransferServerCore(original,originalDirectory,incoming,true,out _,out error);
                    if(!staged)return false;
                }
                error="Outgoing client originals detached; retained destination awaits actual native owner reentry";return false;
            }
            error="Retained same-save original has no positive owner/native reentry proof";return false;
        }
        if(original==null)return true;
        if(ReferenceEquals(original,incoming))
        {
            if(SameDirectory(originalDirectory,GameIO.GetSaveGameDir()))return true;
            error="Same native World changed save scope; original custody remains retained";return false;
        }
        if(!AdmissionEnabled){error="Whole original transfer activation awaits complete source/owner reentry review";return false;}
        if(CaptureServerBoundary(original,originalDirectory,incoming,GameIO.GetSaveGameDir())!=null)return true;
        if(original.IsRemote()&&HasDetachedClientBoundary(original,originalDirectory))return true;
        if(!original.IsRemote())return TryTransferServer(original,originalDirectory,incoming,out _,out error);
        if(!TryTransferClient(original,originalDirectory,incoming,out _,out error))return false;
        RetireEndedClientProjections();
        if(HasRetainedOriginal(incoming,GameIO.GetSaveGameDir()))
        {error="Same-save outgoing client custody detached; actual owner terminal reentry remains required";return false;}
        return true;
    }
    internal static bool HasRetainedOriginal(World world,string directory)
    {
        if(!ThreadManager.IsMainThread()||CanonicalDirectory(directory)==null)return true;
        foreach(var original in retained)if(original.RollbackIncomplete)return true;
        foreach(var original in retained)if(original.Detached&&(ReferenceEquals(original.Original,world)||
            SameDirectory(original.Directory,directory)))return true;
        return false;
    }
    // Caller must use its remembered pre-boundary World/path. Never derive A
    // from the incoming B GameIO, a terminal receipt or an equal directory name.
    internal static bool TryTransferServer(World originalWorld,string originalDirectory,World incoming,out Guid bundle,out string error)
        =>TryTransferServerCore(originalWorld,originalDirectory,incoming,false,out bundle,out error);
    private static bool TryTransferServerCore(World originalWorld,string originalDirectory,World incoming,bool stageRetainedDestination,out Guid bundle,out string error,bool stageIncomingServer=false)
    {
        bundle=Guid.Empty;error="Exact original server quarantine unavailable";
        if(!ThreadManager.IsMainThread()||transferring||serverBoundary!=null||clientBoundary!=null||originalWorld?.IsRemote()!=false||incoming==null||!IncomingRoleCurrent(incoming)||
            !ReferenceEquals(incoming,GameManager.Instance?.World)||ReferenceEquals(originalWorld,incoming))return false;
        string incomingDirectory=CanonicalDirectory(GameIO.GetSaveGameDir());
        if(CanonicalDirectory(originalDirectory)==null||incomingDirectory==null||
            SameDirectory(originalDirectory,incomingDirectory)&&!(stageIncomingServer&&incoming.IsRemote()==false))
        {error="Same-save new native World requires explicit lineage/reentry proof";return false;}
        if(HasRetainedOriginal(incoming,GameIO.GetSaveGameDir())&&
            !(stageRetainedDestination&&CanStageRetainedClientDestination(incoming,GameIO.GetSaveGameDir())||
              stageIncomingServer&&CanStageRetainedServerDestination(incoming,GameIO.GetSaveGameDir())))
        {error="Retained original requires positive current owner/native reentry proof; no old seal restoration";return false;}
        var capture=RebirthNpcSaveQuarantineCapture.Find(originalWorld,originalDirectory);
        if(capture==null||!capture.IsUnchangedOriginal(out error))return false;
        if(retained.Count>=16){error="Whole original bundle capacity retained; no eviction";return false;}
        if(!RebirthNpcPersistenceFile.TryFenceCapturedOutgoingServer(capture,incoming,out var fence,out error))return false;
        Retained next=null;RebirthNpcSaveQuarantineSavedAdoption savedAdoption=null;transferring=true;
        RebirthNpcSaveQuarantineNativeSavedStore identityAdoption=null,worldAdoption=null;
        RebirthNpcNativeTransientAdoption[] transientAdoptions=null;
        try
        {
            if(!RebirthNpcSaveQuarantinePreparation.TryPrepare(fence,capture,out var prepared,out error))return false;
            var domains=prepared.CopyQualifiedDomains();var evidence=prepared.CopyEvidence();
            var localTradeRetirement=new RebirthNpcTradeClientRetirement(
                RebirthNpcTradeService.CaptureLocalOriginalRetirement(originalWorld),RebirthNpcTradeOwnerExchange.CaptureRetirement());
            var serverTradeRetirement=RebirthNpcTradeService.CaptureServerPreparationRetirement(originalWorld);
            var serverTradeObservations=RebirthNpcTradeLocalOwnerLoad.Retirement.Capture(originalWorld);
            var serverIssuedRetirement=RebirthNpcTradeService.CaptureServerIssuedRetirement(originalWorld);
            var localReentryOriginals=RebirthNpcTradeService.CaptureLocalNativeReentryMembership(originalWorld);
            var holdReentryOriginals=RebirthNpcTradeOwnerExchange.CaptureLocalNativeReentryMembership(originalWorld);
            var tradeNpcIds=RebirthNpcTradeService.CaptureNativeCustodyIds();
            // Failure to qualify a clean primary preserves custody; it does not
            // turn an unsaved original into permission to load a replacement.
            RebirthNpcSaveQuarantineSavedAdoption.TryCapture(capture.Scope,out savedAdoption);
            var runtimeOriginals=RebirthNpcRuntimeRegistry.CaptureOutgoingNativeAdoptionMembership(originalWorld);
            RebirthNpcRetainedWorkCustody.TryCapture(capture.Scope,out var workCustody);
            RebirthNpcRetainedControllerCustody.TryCapture(capture.Scope,out var controllerCustody);
            RebirthNpcRetainedWorkPackages.TryCapture(capture.Scope,out var workPackages);
            var advancedAdoption=RebirthNpcAdvancedProgressionPersistenceStore.CaptureNativeAdoption(capture.Scope);
            var checkpointAdoption=RebirthNpcWorkCheckpointPersistenceStore.CaptureNativeAdoption(capture.Scope);
            var decisionAdoption=RebirthNpcDecisionEngine.CaptureNativeAdoption(capture.Scope);
            var signalsAdoption=RebirthNpcDecisionSignalStore.CaptureNativeAdoption(capture.Scope);
            var outcomeAdoption=RebirthNpcWorkOutcomePersistenceStore.CaptureNativeAdoption(capture.Scope);
            identityAdoption=RebirthNpcStableIdentityStore.CaptureNativeSavedAdoption(capture.Scope);
            worldAdoption=RebirthNpcWorldIntegrationService.CaptureNativeSavedAdoption(capture.Scope);
            var nativeOwnerAdoptions=new[]{RebirthCompanionBehaviorService.CaptureNativeAdoption(capture.Scope),
                RebirthCompanionInventoryLockService.CaptureNativeAdoption(capture.Scope),
                RebirthDogRuntimeService.CaptureNativeAdoption(capture.Scope),
                RebirthNpcNativeEquipmentBridge.CaptureNativeAdoption(capture.Scope),
                RebirthNpcSpatialIndex.CaptureNativeAdoption(capture.Scope),
                RebirthNpcPathBudgetCoordinator.CaptureNativeAdoption(capture.Scope),
                RebirthNpcNetworkEpoch.CaptureNativeAdoption(capture.Scope),
                RebirthNpcNetworkBaselineService.CaptureNativeAdoption(capture.Scope),
                RebirthNpcEquipmentProjectionReceipts.CaptureNativeAdoption(capture.Scope),
                RebirthNpcRecoveryProjectionNetwork.CaptureNativeAdoption(capture.Scope),
                RebirthNpcProgressionProjectionRevisionService.CaptureNativeAdoption(capture.Scope),
                RebirthNpcProgressionClientCache.CaptureNativeAdoption(capture.Scope),
                RebirthNpcExecutionPersistenceStore.CaptureNativeAdoption(capture.Scope),
                RebirthNpcWorkAssignmentService.CaptureNativeAdoption(capture.Scope),
                RebirthNpcWorkExecutorHealthRegistry.CaptureNativeAdoption(capture.Scope),
                RebirthNpcTradeService.CaptureNativeAdoption(capture.Scope),
                RebirthDogStorageService.CaptureNativeAdoption(capture.Scope),
                RebirthDogCapacitySyncService.CaptureNativeAdoption(capture.Scope),
                RebirthNpcReplicationPerformanceService.CaptureNativeAdoption(capture.Scope)};
            var playerOwnerAdoptions=new[]{RebirthDogLifecycleService.CaptureGhostNativeAdoption(capture.Scope),
                RebirthCompanionSnapshotService.CaptureNativeOwnerAdoption(capture.Scope),
                RebirthDogChunkObserverService.CaptureNativeOwnerAdoption(capture.Scope),
                RebirthDogAutoLootBridge.CaptureNativeOwnerAdoption(capture.Scope),
                RebirthDogNavigationMarkerService.CaptureNativeOwnerAdoption(capture.Scope)};
            transientAdoptions=new[]{RebirthNpcCommandGateway.CaptureNativeCommandAdoption(capture.Scope),
                RebirthNpcRemoteCommandService.CaptureNativeRemoteCommandAdoption(capture.Scope),
                RebirthNpcDialogueService.CaptureNativeDialogueAdoption(capture.Scope),
                RebirthNpcActivityRegistry.CaptureNativeActivityAdoption(capture.Scope),
                RebirthNpcActivityRecoveryRegistry.CaptureNativeRecoveryAdoption(capture.Scope),
                RebirthNpcNetworkBatcher.CaptureNativePendingAdoption(capture.Scope),
                RebirthNpcRelationshipBatcher.CaptureNativePendingAdoption(capture.Scope),
                RebirthNpcSerializationBufferPool.CaptureNativePendingAdoption(capture.Scope),
                RebirthNpcRetentionCoordinator.CaptureNativePendingAdoption(capture.Scope)};
            var id=Guid.NewGuid();evidence.DocumentElement.SetAttribute("bundle",id.ToString("N"));
            evidence.DocumentElement.SetAttribute("membership",domains.Length.ToString(CultureInfo.InvariantCulture));
            evidence.DocumentElement.SetAttribute("nativeHandles","process-local-strong-only");
            evidence.DocumentElement.AppendChild(capture.CopyEvidence(evidence));
            next=new Retained{Id=id,Original=originalWorld,Admission=incoming,Directory=capture.Scope.Directory,
                Fingerprint=capture.Scope.Fingerprint,Generation=capture.Scope.Generation,Capture=capture,
                Domains=domains,Fence=fence,Evidence=evidence,ServerLocalTradeRetirement=localTradeRetirement,ServerPreparationRetirement=serverTradeRetirement,
                ServerTradeObservationRetirement=serverTradeObservations,ServerIssuedRetirement=serverIssuedRetirement,
                ServerLocalReentryOriginals=localReentryOriginals,ServerHoldReentryOriginals=holdReentryOriginals,
                ServerSavedAdoption=savedAdoption,ServerRuntimeOriginals=runtimeOriginals,ServerWorkCustody=workCustody,
                ServerControllerCustody=controllerCustody,
                ServerWorkPackages=workPackages,
                ServerAdvancedAdoption=advancedAdoption,ServerCheckpointAdoption=checkpointAdoption,
                ServerDecisionAdoption=decisionAdoption,ServerSignalsAdoption=signalsAdoption,
                ServerOutcomeAdoption=outcomeAdoption,
                ServerIdentityAdoption=identityAdoption,ServerWorldAdoption=worldAdoption,
                ServerNativeOwnerAdoptions=nativeOwnerAdoptions,ServerPlayerOwnerAdoptions=playerOwnerAdoptions,
                ServerTransientAdoptions=transientAdoptions,ServerTradeNpcIds=tradeNpcIds};
            // Retain every original closure and immutable membership BEFORE the
            // first pointer swap. Failed evidence/rollback is also never evicted.
            retained.Add(next);
            if(!RegisterServerAdmissionSuccessors(next))throw new InvalidOperationException("Original admitted namespace successor could not be pinned");
            next.EvidencePath=WriteAndReadback(next);
            if(!fence.IsCurrent||!prepared.IsCurrent||savedAdoption!=null&&!savedAdoption.IsOriginalCurrent||
                workCustody!=null&&!workCustody.CanRegister||!capture.IsUnchangedOriginal(out error)||
                controllerCustody!=null&&!controllerCustody.CanRegister||
                workPackages!=null&&!workPackages.CanRegister||
                advancedAdoption!=null&&!advancedAdoption.IsOriginalCurrent||checkpointAdoption!=null&&!checkpointAdoption.IsOriginalCurrent||
                decisionAdoption!=null&&!decisionAdoption.IsOriginalCurrent||signalsAdoption!=null&&!signalsAdoption.IsOriginalCurrent||
                outcomeAdoption!=null&&!outcomeAdoption.IsOriginalCurrent||
                identityAdoption!=null&&!identityAdoption.IsOriginalCurrent||worldAdoption!=null&&!worldAdoption.IsOriginalCurrent||
                !Array.TrueForAll(nativeOwnerAdoptions,value=>value==null||value.IsOriginalCurrent)||
                !Array.TrueForAll(playerOwnerAdoptions,value=>value==null||value.IsOriginalCurrent)||
                !Array.TrueForAll(transientAdoptions,value=>value==null||value.IsOriginalCurrent)||
                !ReferenceEquals(incoming,GameManager.Instance?.World)||!SameDirectory(incomingDirectory,GameIO.GetSaveGameDir()))throw new InvalidOperationException("Original group changed after durable evidence");
            foreach(var domain in domains)
            {
                if(!fence.IsCurrent||!ReferenceEquals(incoming,GameManager.Instance?.World)||!SameDirectory(incomingDirectory,GameIO.GetSaveGameDir()))throw new InvalidOperationException("Original transaction fence changed");
                domain.Detach();
            }
            if(!fence.IsCurrent||!ReferenceEquals(incoming,GameManager.Instance?.World)||!SameDirectory(incomingDirectory,GameIO.GetSaveGameDir()))throw new InvalidOperationException("Original transaction lost admission world");
            next.Detached=true;
            foreach(var runtimeOriginal in runtimeOriginals)
                if(!runtimeOriginal.RegisterDetached())throw new InvalidOperationException("Original native runtime membership did not publish detached custody");
            serverBoundary=new ServerBoundary(next,originalWorld,originalDirectory,incoming,incomingDirectory,domains);
            if(workCustody!=null&&!workCustody.RegisterDetached())throw new InvalidOperationException("Original work owner custody failed to register after detach");
            if(controllerCustody!=null&&!controllerCustody.RegisterDetached())throw new InvalidOperationException("Original controller custody failed to register after detach");
            if(workPackages!=null&&!workPackages.RegisterDetached())throw new InvalidOperationException("Original native work package custody failed to register after detach");
            if(!CommitServerAdmissionSuccessors(next))throw new InvalidOperationException("Original admitted namespace successor did not commit full detach");
            bundle=id;error=string.Empty;return true;
        }
        catch(Exception exception)
        {
            bool restored=next==null||RestoreFailedAttempt(next);
            if(restored&&next?.ServerWorkCustody!=null&&!next.ServerWorkCustody.AbortAfterRollback())restored=false;
            if(restored&&next?.ServerControllerCustody!=null&&!next.ServerControllerCustody.AbortAfterRollback())restored=false;
            if(restored&&next?.ServerWorkPackages!=null&&!next.ServerWorkPackages.AbortAfterRollback())restored=false;
            if(restored&&next!=null)AbortServerAdmissionSuccessors(next);
            error="Whole original transfer retained: "+exception.GetType().Name+(restored?"; all changed pointers rolled back":"; rollback incomplete; writer fence retained");
            return false;
        }
        finally
        {
            transferring=false;
            if(next==null)savedAdoption?.Dispose();
            if(next==null){identityAdoption?.Dispose();worldAdoption?.Dispose();}
            if(next==null&&transientAdoptions!=null)foreach(var transient in transientAdoptions)transient?.Dispose();
            if(next==null||!next.RollbackIncomplete){fence.Dispose();if(next!=null)next.Fence=null;}
        }
    }
    // CLIENT projection/callback group only. No host path enumeration/read/write,
    // source/payment reset, disconnect, ACK or native action occurs here.
    internal static bool TryTransferClient(World originalWorld,string originalDirectory,World incoming,out Guid bundle,out string error)
        =>TryTransferClientCore(originalWorld,originalDirectory,incoming,false,out bundle,out error);
    private static bool HasDetachedClientBoundary(World world,string directory)
    {
        // Only the registered completed transaction receipt can authorize the
        // lifecycle boundary. A retained/equal group alone is not that receipt.
        return clientBoundary?.MatchesOutgoing(world,directory)==true;
    }
    private static bool CanStageRetainedClientDestination(World incoming,string directory)
    {
        bool found=false;
        foreach(var value in retained)
        {
            if(value.RollbackIncomplete)return false;
            if(!value.Detached||!SameDirectory(value.Directory,directory))continue;
            if(value.Original?.IsRemote()!=true||value.Fence!=null||value.Capture!=null||value.ClientRetirement==null||
                value.Domains==null||value.Domains.Length!=ClientProjectionMembers.Length)return false;
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var domain in value.Domains)if(domain==null||!domain.IsDetached||!names.Add(domain.Name))return false;
            if(!Array.TrueForAll(ClientProjectionMembers,names.Contains))return false;
            found=true;
        }
        return found&&incoming?.IsRemote()==true&&ReferenceEquals(incoming,GameManager.Instance?.World);
    }
    private static bool TryTransferClientCore(World originalWorld,string originalDirectory,World incoming,bool stageRetainedDestination,out Guid bundle,out string error)
    {
        bundle=Guid.Empty;error="Exact original client projection group unavailable";
        RetireEndedClientProjections();
        if(!ThreadManager.IsMainThread()||transferring||originalWorld?.IsRemote()!=true||incoming==null||!IncomingRoleCurrent(incoming)||
            !ReferenceEquals(incoming,GameManager.Instance?.World)||ReferenceEquals(originalWorld,incoming)||retained.Count>=16||
            clientBoundary!=null||serverBoundary!=null||
            (HasRetainedOriginal(incoming,GameIO.GetSaveGameDir())&&(!stageRetainedDestination||
                !(incoming.IsRemote()?CanStageRetainedClientDestination(incoming,GameIO.GetSaveGameDir()):CanStageRetainedServerDestination(incoming,GameIO.GetSaveGameDir()))))||
            !RebirthNpcNetworkEpoch.TryObserveRetainedClient(originalWorld,originalDirectory,out uint epoch,out uint connection))return false;
        Retained next=null;transferring=true;
        try
        {
            bool financialCustody=RebirthNpcTradeClientFlow.HasLifecycleCustody||RebirthNpcTradeOwnerExchange.HasLifecycleCustody||RebirthNpcTradeOwnerLoad.HasOutgoingLifecycleCustody;
            string incomingDirectory=CanonicalDirectory(GameIO.GetSaveGameDir());
            if(incomingDirectory==null||CanonicalDirectory(originalDirectory)==null)return false;
            var tradeRetirement=new RebirthNpcTradeClientRetirement(RebirthNpcTradeClientFlow.CaptureRetirement(),RebirthNpcTradeOwnerExchange.CaptureRetirement(),RebirthNpcTradeOwnerLoad.CaptureRetirement());
            if(financialCustody&&tradeRetirement.Complete)throw new InvalidOperationException("Trade original retirement membership missing");
            var retirement=RebirthNpcNetworkEpoch.ClientProjectionRetirement.Capture(originalWorld,originalDirectory,epoch,connection);
            if(retirement==null)return false;
            var tradeObservationRetirement=RebirthNpcTradeOwnerLoad.Retirement.Capture(originalWorld,originalDirectory,retirement);
            var clientNativeOriginals=RebirthNpcTradeClientFlow.CaptureNativeReentryMembership(originalWorld);
            var clientNativeHolds=RebirthNpcTradeOwnerExchange.CaptureClientNativeReentryMembership(originalWorld);
            RebirthNpcSaveQuarantineDomain[] domains;
            using(var budget=new RebirthNpcSaveQuarantineDomain.EvidenceBudget(RebirthNpcSaveQuarantinePreparation.MaximumEvidenceBytes))
                domains=new[]{
                    RebirthNpcRecoveryProjectionScope.PrepareClientGenerationQuarantine(originalWorld,originalDirectory,epoch,connection),
                    RebirthNpcPendingRuntimeStates.PrepareClientRuntimeQuarantine(originalWorld,epoch,connection),
                    RebirthNpcPendingEquipmentSnapshots.PrepareClientEquipmentQuarantine(originalWorld,epoch,connection),
                    RebirthNpcPendingAppearanceSnapshots.PrepareClientAppearanceQuarantine(originalWorld,epoch,connection),
                    RebirthNpcInteractionNetworkClient.PrepareInteractionCallbackQuarantine(originalWorld,connection),
                    RebirthNpcProgressionClientCache.PrepareProgressionCacheQuarantine(originalWorld,epoch,connection),
                    RebirthDogCapacitySyncService.PrepareCapacityQuarantine(originalWorld,epoch,connection),
                    RebirthDogAutoLootBridge.PrepareAutoLootQuarantine(originalWorld,epoch,connection),
                    RebirthCompanionSnapshotService.PrepareSnapshotQuarantine(originalWorld,epoch,connection),
                    RebirthDogNavigationMarkerService.PrepareMarkerQuarantine(originalWorld,epoch,connection),
                    RebirthNpcNativeEquipmentBridge.PrepareNativeProjectionQuarantine(originalWorld,originalDirectory),
                    RebirthNpcRecoveryProjectionNetwork.PrepareProjectionMailboxQuarantine(originalWorld,originalDirectory),
                    RebirthNpcRuntimeRegistry.PrepareRuntimeQuarantine(originalWorld,originalDirectory),
                    RebirthNpcNetworkEpoch.PrepareClientEpochQuarantine(originalWorld,originalDirectory,epoch,connection)};
            var evidence=new XmlDocument{XmlResolver=null};var root=evidence.CreateElement("npcClientProjectionQuarantine");evidence.AppendChild(root);
            root.SetAttribute("epoch",epoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("connection",connection.ToString(CultureInfo.InvariantCulture));
            root.SetAttribute("tradeFinancialCustody",financialCustody?"1":"0");
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var domain in domains){if(!names.Add(domain.Name)||!domain.IsCurrent)throw new InvalidOperationException("Client projection membership changed");root.AppendChild(domain.CopyEvidence(evidence));}
            if(new UTF8Encoding(false,true).GetByteCount(evidence.OuterXml)>RebirthNpcSaveQuarantinePreparation.MaximumEvidenceBytes)
                throw new InvalidDataException("Whole client projection evidence bound exceeded");
            next=new Retained{Id=Guid.NewGuid(),Original=originalWorld,Admission=incoming,Directory=originalDirectory,Domains=domains,Evidence=evidence,ClientRetirement=retirement,ClientFinancialCustody=financialCustody,TradeRetirement=tradeRetirement,TradeObservationRetirement=tradeObservationRetirement,
                ClientNativeOriginals=clientNativeOriginals,ClientNativeHolds=clientNativeHolds};
            retained.Add(next); // strong native/callback closures before ANY swap
            if(!RebirthNpcNetworkEpoch.TryObserveRetainedClient(originalWorld,originalDirectory,out uint actualEpoch,out uint actualConnection)||actualEpoch!=epoch||actualConnection!=connection)
                throw new InvalidOperationException("Original client peer/epoch changed");
            foreach(var domain in domains)
            {if(!ReferenceEquals(incoming,GameManager.Instance?.World)||!SameDirectory(GameIO.GetSaveGameDir(),incomingDirectory))throw new InvalidOperationException("Client admission World/path changed");domain.Detach();}
            if(!ReferenceEquals(incoming,GameManager.Instance?.World)||!SameDirectory(GameIO.GetSaveGameDir(),incomingDirectory))throw new InvalidOperationException("Client boundary changed after detach");
            next.Detached=true;clientBoundary=new ClientBoundary(originalWorld,originalDirectory,incoming,incomingDirectory);bundle=next.Id;error=string.Empty;return true;
        }
        catch(Exception exception)
        {
            bool restored=next==null||RestoreFailedAttempt(next);
            error="Client projection originals retained: "+exception.GetType().Name+(restored?"; all changed pointers rolled back":"; rollback incomplete; admission remains blocked");return false;
        }
        finally{transferring=false;}
    }
    private static readonly string[] ClientProjectionMembers={"client-recovery-generation","client-pending-runtime","client-pending-equipment","client-pending-appearance",
        "interaction-callbacks","progression-client-cache","dog-capacity-projection","dog-auto-loot-callbacks","companion-snapshot-callbacks","dog-navigation-markers",
        "native-equipment-projection","recovery-projection-mailbox","runtime-registry","client-network-epoch"};
    internal static void RetireEndedClientProjections()
    {
        if(!ThreadManager.IsMainThread()||transferring)return;
        foreach(var original in retained.ToArray())
        {
            try
            {
                if(!original.Detached||original.RollbackIncomplete||original.ClientFinancialCustody||original.Fence!=null||original.Capture!=null||original.ClientRetirement==null||
                    original.Original?.IsRemote()!=true||!original.ClientRetirement.Matches(original.Original,original.Directory)||
                    original.Domains==null||original.Domains.Length!=ClientProjectionMembers.Length)continue;
                var names=new HashSet<string>(StringComparer.Ordinal);bool qualified=true;
                foreach(var domain in original.Domains)if(domain==null||!domain.IsDetached||!names.Add(domain.Name)){qualified=false;break;}
                if(!qualified||!Array.TrueForAll(ClientProjectionMembers,names.Contains)||!original.ClientRetirement.HasEnded)continue;
                if(original.TradeObservationRetirement==null||!original.TradeObservationRetirement.ReleaseDetachedObservation())continue;
                // Only the typed remote projection group loses these strong
                // references. No host original, native actor, save file, owner
                // hold, callback, financial receipt or ACK is changed here.
                retained.Remove(original);
            }
            catch{/* An unqualified/unknown native transport retains the entire group. */}
        }
    }
    private static bool RestoreFailedAttempt(Retained original)
    {
        if(original==null)return false;
        if(original.Detached){original.RollbackIncomplete=true;return false;}
        bool restored=RestoreOriginalDomains(original.Domains);
        original.RollbackIncomplete=!restored;
        // Positive full original restoration leaves custody in its original
        // static owners. This attempted bundle owns no detached state now.
        // Its forensic file stays on disk; no domain or financial receipt is
        // evicted to make space. Unknown/partial restoration remains retained.
        if(restored)retained.Remove(original);
        return restored;
    }
    private static bool RestoreOriginalDomains(RebirthNpcSaveQuarantineDomain[] domains)
    {
        if(domains==null)return false;
        bool restored=true;
        // Attempt every restoration even after one fails. Only after the whole
        // group is restored can cross-domain current predicates be meaningful.
        for(int i=domains.Length-1;i>=0;i--)
        {try{if(domains[i]==null)restored=false;else domains[i].Rollback();}catch{restored=false;}}
        foreach(var domain in domains)
        {try{if(domain==null||!domain.IsCurrent)restored=false;}catch{restored=false;}}
        return restored;
    }
    private static string WriteAndReadback(Retained original)
    {
        byte[] expected=new UTF8Encoding(false,true).GetBytes(original.Evidence.OuterXml);
        if(expected.Length<1||expected.Length>RebirthNpcSaveQuarantinePreparation.MaximumEvidenceBytes)
            throw new InvalidDataException("Whole original evidence bound exceeded");
        string path=Path.Combine(original.Directory,"RebirthNpcQuarantineBundle."+original.Id.ToString("N")+".xml");
        using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
        {stream.Write(expected,0,expected.Length);stream.Flush(true);}
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length!=expected.Length)throw new InvalidDataException("Whole original evidence length changed");
            var buffer=new byte[65536];int offset=0,read;
            while((read=stream.Read(buffer,0,buffer.Length))!=0)
            {for(int i=0;i<read;i++)if(buffer[i]!=expected[offset+i])throw new InvalidDataException("Whole original evidence readback differs");offset+=read;}
            if(offset!=expected.Length)throw new InvalidDataException("Whole original evidence readback truncated");
        }
        return path;
    }
}
