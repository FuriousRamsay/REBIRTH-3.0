using System;
using System.Collections.Generic;

#nullable disable

/// <summary>
/// Survivor network facade. Server sends only owner-scoped projections and derives identity
/// from authenticated server state. Clients submit IDs/intent only.
/// </summary>
public static class RebirthSurvivorNetworkService
{
    private sealed class LocalCreationDispatch
    {
        public int EntityId;
        public RebirthSurvivorCreationNetworkRequest Request;
    }

    private static readonly object LocalDispatchSync = new object();
    private static readonly Queue<LocalCreationDispatch> LocalCreationQueue = new Queue<LocalCreationDispatch>();
    private static bool installed;
    private static int installStage;
    private static readonly Dictionary<string, RebirthSurvivorOwnerSendHeader> LastSentHeaders = new Dictionary<string, RebirthSurvivorOwnerSendHeader>(StringComparer.Ordinal);

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor] network authority already installed.";
        if (installStage < 1) { RebirthLiteratureStudySessionService.Install(); installStage = 1; }
        if (installStage < 2) { RebirthAudiobookListeningSessionService.Install(); installStage = 2; }
        if (installStage < 3) { RebirthLegacyMusicPlaybackService.Install(); installStage = 3; }
        if (installStage < 4) { ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting)); installStage = 4; }
        if (installStage < 5) { ModEvents.PlayerJoinedGame.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerJoinedGameData>(OnPlayerJoinedGame)); installStage = 5; }
        if (installStage < 6) { ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawnedInWorld)); installStage = 6; }
        if (installStage < 7) { ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate)); installStage = 7; }
        if (installStage < 8) { ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown)); installStage = 8; }
        if (installStage < 9) { ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown)); installStage = 9; }
        installed = true; // All stages returned successfully; a failed stage remains retryable.
        return "[REBIRTH Survivor] network authority installed protocol=" + RebirthSurvivorNetworkProtocol.Version;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        RebirthSurvivorCreationTransactions.Reset();
        RebirthHudTrackingPreferenceService.ResetRuntime(true);
        RebirthTraitGameplayModifierService.ResetPassiveProjections();
        LastSentHeaders.Clear();
        // A remote server can send the owner snapshot during PlayerJoinedGame/RequestToEnterGame,
        // just before the client's GameStarting callback. Preserve that early authoritative packet
        // exactly like the sandbox-option sync does; shutdown paths already clear prior sessions.
        RebirthSurvivorOwnerStateSnapshot earlyOwner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
        bool preserveEarlyClientOwner = !data.AsServer && earlyOwner != null;
        if (!preserveEarlyClientOwner)
            RebirthSurvivorClientState.Reset();
        else
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("preserved early client owner snapshot across GameStarting creationState="
                + earlyOwner.CreationState + " hasCharacter=" + earlyOwner.HasCharacter); } }
        RebirthSurvivorDebug.Reset();
        RebirthStudyHudClientState.Reset();
        RebirthMusicLibraryClient.Reset();
        ClearLocalCreationQueue();
    }

    private static void OnPlayerJoinedGame(ref ModEvents.SPlayerJoinedGameData data)
    {
        if (!RebirthWorldCharacterRepository.IsServerAuthority)
            return;
        if (data.ClientInfo == null)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] PlayerJoinedGame had no ClientInfo; owner-state pre-spawn snapshot could not be sent.");
            return;
        }
        RebirthStablePlayerIdentity.Remember(data.ClientInfo);
        bool sent = SendOwnerState(data.ClientInfo, 0L, true, "player-joined-pre-spawn");
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("PlayerJoinedGame entity=" + data.ClientInfo.entityId + " ownerSnapshotSent=" + sent); } }
    }

    private static void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!RebirthWorldCharacterRepository.IsServerAuthority) return;
        // Starter delivery also resolves the stable player identity. Remember it
        // before either operation rather than relying on another handler's order.
        if (data.ClientInfo != null) RebirthStablePlayerIdentity.Remember(data.ClientInfo);
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world != null)
            RebirthBackgroundStarterItems.Offer(world.GetEntity(data.EntityId) as EntityPlayer);
        if (data.ClientInfo == null) return;
        SendOwnerState(data.ClientInfo, 0L, true, "player-spawned");
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        RebirthBackgroundStarterItems.Tick();
        RebirthMusicTransferClient.Tick();
        RebirthGearDeferredInitialDispatcher.Tick();
        RebirthGearClientTransferPump.Tick();
        RebirthGearSandboxAdjustmentDeferral.Tick();
        PumpLocalCreationQueue("ModEvents.GameUpdate");
        RebirthSkillAwardService.FlushOwnerPublications();
    }

    /// <summary>
    /// Drains listen-server creation requests. The persistent pre-spawn pump also calls this because
    /// V3.2 does not dispatch ModEvents.GameUpdate while the menu-owned spawnselection lifecycle is
    /// stalled. Keeping the queue drain callable from both paths prevents a staged Survivor from
    /// leaving the freshly-created player under the creation hold forever.
    /// </summary>
    public static void PumpLocalCreationQueue(string source)
    {
        // Both the normal GameUpdate and existing pre-spawn pump are main-thread paths.
        // Run this before the server-only queue guard so remote/early UI receives notifications.
        RebirthSurvivorClientState.PumpNotifications();
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || GameManager.Instance == null || GameManager.Instance.World == null) return;

        LocalCreationDispatch[] pending;
        lock (LocalDispatchSync)
        {
            if (LocalCreationQueue.Count == 0) return;
            pending = LocalCreationQueue.ToArray();
            LocalCreationQueue.Clear();
        }

        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("draining local creation queue count=" + pending.Length + " source=" + (source ?? "unspecified")); } }
        World world = GameManager.Instance.World;
        for (int i = 0; i < pending.Length; i++)
        {
            LocalCreationDispatch dispatch = pending[i];
            if (dispatch == null || dispatch.Request == null) continue;
            EntityPlayer player = world.GetEntity(dispatch.EntityId) as EntityPlayer;
            if (player == null)
            {
                Log.Warning("[REBIRTH Survivor][SpawnFlow] local creation dispatch lost entity=" + dispatch.EntityId + " request=" + dispatch.Request.RequestId);
                continue;
            }
            { if (RebirthLogSettings.SpawnFlowLoggingEnabled) { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("processing local creation request=" + dispatch.Request.RequestId + " entity=" + dispatch.EntityId); } }
            HandleServerCreationRequest(player, dispatch.Request);
        }
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthBackpackLibraryClientOffers.Reset();
        RebirthGearOfferClient.Reset();
        RebirthGearDeferredInitialDispatcher.Reset();
        RebirthGearClientTransferPump.Reset();
        RebirthGearPreparationRefusalClient.Reset();
        RebirthBackpackLibraryClientViews.Reset();
        RebirthBackpackSellStashClientViews.Reset();
        RebirthCookingRecipeLookup.Reset();
        RebirthBackgroundStarterItems.Reset();
        RebirthSurvivorCreationTransactions.Reset();
        RebirthHudTrackingPreferenceService.ResetRuntime(true);
        RebirthTraitGameplayModifierService.ResetPassiveProjections();
        LastSentHeaders.Clear();
        RebirthSurvivorClientState.Reset();
        RebirthSurvivorDebug.Reset();
        RebirthStudyHudClientState.Reset();
        RebirthMusicLibraryClient.Reset();
        ClearLocalCreationQueue();
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthBackpackLibraryClientOffers.Reset();
        RebirthGearOfferClient.Reset();
        RebirthGearDeferredInitialDispatcher.Reset();
        RebirthGearClientTransferPump.Reset();
        RebirthGearPreparationRefusalClient.Reset();
        RebirthBackpackLibraryClientViews.Reset();
        RebirthBackpackSellStashClientViews.Reset();
        RebirthCookingRecipeLookup.Reset();
        RebirthJournalGuideService.Reset();
        RebirthBackgroundStarterItems.Reset();
        RebirthSurvivorCreationTransactions.Reset();
        RebirthHudTrackingPreferenceService.ResetRuntime(true);
        RebirthTraitGameplayModifierService.ResetPassiveProjections();
        LastSentHeaders.Clear();
        RebirthSurvivorClientState.Reset();
        RebirthSurvivorDebug.Reset();
        RebirthStudyHudClientState.Reset();
        RebirthMusicLibraryClient.Reset();
        ClearLocalCreationQueue();
    }

    private static void ClearLocalCreationQueue()
    {
        lock (LocalDispatchSync) LocalCreationQueue.Clear();
    }

    private static void EnqueueLocalCreation(EntityPlayerLocal player, RebirthSurvivorCreationNetworkRequest request)
    {
        if (player == null || request == null) return;
        lock (LocalDispatchSync)
        {
            LocalCreationQueue.Enqueue(new LocalCreationDispatch
            {
                EntityId = player.entityId,
                Request = request.Clone()
            });
        }
    }

    public static ulong RequestValidation(EntityPlayerLocal player, RebirthSurvivorCreationSelection selection, RebirthSurvivorProfile sourceProfile)
    {
        return SendCreationRequest(player, RebirthSurvivorCreationNetworkOperation.Validate, selection, sourceProfile, null);
    }

    public static ulong RequestCommit(EntityPlayerLocal player, RebirthSurvivorCreationSelection selection, RebirthSurvivorProfile sourceProfile)
    {
        return SendCreationRequest(player, RebirthSurvivorCreationNetworkOperation.Commit, selection, sourceProfile, null);
    }

    public static ulong RequestCommit(EntityPlayerLocal player, RebirthSurvivorCreationSelection selection,
        RebirthSurvivorProfile sourceProfile, IDictionary<string, string> creationChoices)
    {
        return SendCreationRequest(player, RebirthSurvivorCreationNetworkOperation.Commit, selection, sourceProfile, creationChoices);
    }

    public static ulong SendCreationRequest(EntityPlayerLocal player, RebirthSurvivorCreationNetworkOperation operation,
        RebirthSurvivorCreationSelection selection, RebirthSurvivorProfile sourceProfile)
    {
        return SendCreationRequest(player, operation, selection, sourceProfile, null);
    }

    public static ulong SendCreationRequest(EntityPlayerLocal player, RebirthSurvivorCreationNetworkOperation operation,
        RebirthSurvivorCreationSelection selection, RebirthSurvivorProfile sourceProfile, IDictionary<string, string> creationChoices)
    {
        if (player == null || selection == null)
            return 0UL;
        RebirthSurvivorCreationNetworkRequest request = BuildClientRequest(player, operation, selection, sourceProfile, creationChoices);
        if (request.RequestId == 0UL) return 0UL;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null) return 0UL;
        if (connection.IsServer)
        {
            // Defer until the next game-update OR persistent pre-spawn pump tick so the caller can
            // persist RequestId before synchronous local result/owner-state callbacks fire.
            EnqueueLocalCreation(player, request);
            { if (RebirthLogSettings.SpawnFlowLoggingEnabled) { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("queued local creation request=" + request.RequestId + " entity=" + player.entityId + " operation=" + operation); } }
        }
        else
        {
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSurvivorCreationRequest>().Setup(request));
        }
        return request.RequestId;
    }

    public static void RequestOwnerState(EntityPlayerLocal player, bool force)
    {
        if (player == null)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] RequestOwnerState skipped: local player is null.");
            return;
        }
        RebirthSurvivorOwnerHeader current = RebirthSurvivorClientState.GetOwnerHeader();
        long known = current.Available ? current.CharacterRevision : 0L;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null)
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] RequestOwnerState skipped: ConnectionManager is null entity=" + player.entityId);
            return;
        }
        if (connection.IsServer)
        {
            bool sent = SendOwnerState(player, known, force, "local-state-request");
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("RequestOwnerState listenServer entity=" + player.entityId + " sent=" + sent); } }
        }
        else
        {
            connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSurvivorStateRequest>().Setup(player.entityId, known, force));
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("RequestOwnerState client entity=" + player.entityId + " knownRevision=" + known + " force=" + force); } }
        }
    }

    /// <summary>
    /// Listen-server/single-player pre-spawn path. No EntityPlayer exists while XUi_Menu's native
    /// spawnselection is open, so derive the server-owned local identity directly from the platform
    /// layer and publish the same owner snapshot used by network clients.
    /// </summary>
    public static bool TryPublishLocalPreSpawnOwnerState(string reason)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || !RebirthWorldCharacterRepository.IsServerAuthority)
            return false;
        RebirthStablePlayerIdentity identity;
        if (!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity))
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] local pre-spawn owner snapshot failed: InternalLocalUserIdentifier unavailable.");
            return false;
        }
        RebirthSurvivorOwnerStateSnapshot snapshot = CaptureOwnerState(identity);
        RebirthSurvivorClientState.ReceiveOwnerState(snapshot);
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("local pre-spawn owner snapshot hasCharacter=" + snapshot.HasCharacter
            + " creationState=" + snapshot.CreationState + " mode=" + snapshot.RebirthModeEnabled
            + " reason=" + (reason ?? "unspecified")); } }
        return true;
    }

    internal static void HandleServerCreationRequest(EntityPlayer player, RebirthSurvivorCreationNetworkRequest request)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || player == null || request == null)
            return;

        RebirthStablePlayerIdentity identity;
        RebirthSurvivorCreationNetworkResponse response;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity))
        {
            response = new RebirthSurvivorCreationNetworkResponse
            {
                RequestId = request.RequestId,
                Operation = request.Operation,
                Status = RebirthSurvivorCreationNetworkStatus.InternalError,
                ServerDefinitionHash = RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty,
                ServerDefinitionVersion = RebirthSurvivorDefinitionRegistry.DefinitionVersion ?? string.Empty,
                MessageCode = "server-identity-unavailable"
            };
        }
        else
        {
            response = RebirthSurvivorCreationTransactions.Execute(player, identity, request);
        }

        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("server creation result request=" + request.RequestId
            + " entity=" + player.entityId + " operation=" + request.Operation
            + " status=" + (response != null ? response.Status.ToString() : "null")
            + " code=" + (response != null ? (response.MessageCode ?? string.Empty) : string.Empty)); } }

        EntityPlayerLocal localPlayer = player as EntityPlayerLocal;
        if (localPlayer != null)
            RebirthSurvivorClientState.ReceiveCreationResult(response);
        else
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSurvivorCreationResult>().Setup(response), _attachedToEntityId: player.entityId);

        if (response.Status == RebirthSurvivorCreationNetworkStatus.Created || response.Status == RebirthSurvivorCreationNetworkStatus.AlreadyCreated)
            SendOwnerState(player, 0L, true, "creation-result-" + response.Status.ToString());
    }

    public static bool SendOwnerState(ClientInfo clientInfo, long knownRevision, bool force, string reason)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || clientInfo == null)
            return false;
        RebirthStablePlayerIdentity identity;
        if (!RebirthStablePlayerIdentity.TryFromClientInfo(clientInfo, out identity))
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] SendOwnerState(ClientInfo) identity unavailable entity=" + clientInfo.entityId + " reason=" + (reason ?? "unspecified"));
            return false;
        }
        if (CanSuppressOwnerBody(identity, knownRevision, force)) return false;
        RebirthSurvivorOwnerStateSnapshot snapshot = CaptureOwnerState(identity);
        EntityPlayerLocal localPlayer = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetEntity(clientInfo.entityId) as EntityPlayerLocal
            : null;
        if (localPlayer != null)
            RebirthSurvivorClientState.ReceiveOwnerState(snapshot);
        else
        {
            try
            {
                NetPackageManager.GetPackageId(typeof(NetPackageRebirthSurvivorOwnerState));
                var package = NetPackageManager.GetPackage<NetPackageRebirthSurvivorOwnerState>();
                if (package == null) return false;
                clientInfo.SendPackage(package.Setup(snapshot));
            }
            catch (Exception)
            {
                // Knowledge may already be persisted; allow a later owner-state request
                // to retry without marking this failed publication as sent.
                return false;
            }
        }
        RememberSentHeader(identity, snapshot);
        if (RebirthLogSettings.PreSpawnLoggingEnabled) { if (RebirthLogSettings.PreSpawnLoggingEnabled) { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("owner snapshot entity=" + clientInfo.entityId + " revision=" + snapshot.CharacterRevision + " hasCharacter=" + snapshot.HasCharacter + " creationState=" + snapshot.CreationState + " reason=" + (reason ?? "unspecified")); } }
        return true;
    }

    public static bool SendOwnerState(EntityPlayer player, long knownRevision, bool force, string reason)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || player == null)
            return false;
        RebirthStablePlayerIdentity identity;
        if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity))
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] SendOwnerState(EntityPlayer) identity unavailable entity=" + player.entityId + " reason=" + (reason ?? "unspecified"));
            return false;
        }
        if (CanSuppressOwnerBody(identity, knownRevision, force)) return false;
        RebirthSurvivorOwnerStateSnapshot snapshot = CaptureOwnerState(identity);
        EntityPlayerLocal localPlayer = player as EntityPlayerLocal;
        if (localPlayer != null)
            RebirthSurvivorClientState.ReceiveOwnerState(snapshot);
        else
        {
            try
            {
                NetPackageManager.GetPackageId(typeof(NetPackageRebirthSurvivorOwnerState));
                var package = NetPackageManager.GetPackage<NetPackageRebirthSurvivorOwnerState>();
                if (package == null) return false;
                connection.SendPackage(package.Setup(snapshot), _attachedToEntityId: player.entityId);
            }
            catch (Exception)
            {
                // Knowledge may already be persisted; allow a later owner-state request
                // to retry without marking this failed publication as sent.
                return false;
            }
        }
        RememberSentHeader(identity, snapshot);
        { if (RebirthLogSettings.PreSpawnLoggingEnabled) { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("owner snapshot via entity=" + player.entityId + " revision=" + snapshot.CharacterRevision
            + " hasCharacter=" + snapshot.HasCharacter + " creationState=" + snapshot.CreationState + " reason=" + (reason ?? "unspecified")); } }
        return true;
    }

    public static RebirthSurvivorOwnerStateSnapshot CaptureOwnerState(RebirthStablePlayerIdentity identity)
    {
        RebirthSurvivorOwnerStateSnapshot snapshot = new RebirthSurvivorOwnerStateSnapshot();
        snapshot.RebirthModeEnabled = RebirthSurvivorMode.IsEnabledForCurrentWorld();
        snapshot.CreationState = snapshot.RebirthModeEnabled ? RebirthSurvivorOwnerCreationState.CreationRequired : RebirthSurvivorOwnerCreationState.NotApplicable;
        snapshot.ServerDefinitionHash = RebirthSurvivorDefinitionRegistry.SemanticHash ?? string.Empty;
        snapshot.ServerDefinitionVersion = RebirthSurvivorDefinitionRegistry.DefinitionVersion ?? string.Empty;
        if (!snapshot.RebirthModeEnabled || identity == null)
            return snapshot;

        RebirthWorldCharacterRecord record;
        bool recordLoaded = RebirthWorldCharacterRepository.TryGet(identity, out record);
        if (RebirthLogSettings.PreSpawnLoggingEnabled)
        {
            string saveRoot = GameIO.GetSaveGameDir();
            string playerRoot = string.IsNullOrEmpty(saveRoot) ? string.Empty :
                System.IO.Path.Combine(saveRoot, "RebirthData", "Survivor", "Players");
            string recordPath = string.IsNullOrEmpty(playerRoot) ? string.Empty :
                System.IO.Path.Combine(playerRoot, identity.StorageKey + ".xml");
            bool finalExists = !string.IsNullOrEmpty(recordPath) && System.IO.File.Exists(recordPath);
            bool backupExists = !string.IsNullOrEmpty(recordPath) && System.IO.File.Exists(recordPath + ".bak");
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("owner-state repository identityKey=" + identity.StorageKey +
                " path='" + recordPath + "' finalExists=" + finalExists +
                " backupExists=" + backupExists +
                " tryGet=" + recordLoaded +
                " recordNull=" + (record == null) +
                " complete=" + (record != null && record.IsComplete) +
                " revision=" + (record != null ? record.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none")); }
        }
        if (!recordLoaded || record == null || !record.IsComplete)
        {
            RebirthWorldCharacterPersistenceIssue[] issues = RebirthWorldCharacterRepository.GetIssuesSnapshot();
            for (int i = issues.Length - 1; i >= 0; i--)
            {
                RebirthWorldCharacterPersistenceIssue issue = issues[i];
                if (issue != null && string.Equals(issue.StablePlayerKey, identity.StorageKey, StringComparison.Ordinal))
                {
                    if (RebirthLogSettings.PreSpawnLoggingEnabled)
                        { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("owner-state repository issue recovered=" + issue.RecoveredFromBackup +
                            " key=" + (issue.StablePlayerKey ?? string.Empty) +
                            " message=" + (issue.Reason ?? string.Empty)); }
                    if (!issue.RecoveredFromBackup)
                    {
                        snapshot.CreationState = RebirthSurvivorOwnerCreationState.RecoveryRequired;
                        break;
                    }
                }
            }
            return snapshot;
        }

        snapshot.HasCharacter = true;
        snapshot.CreationState = RebirthSurvivorOwnerCreationState.Ready;
        snapshot.CharacterRevision = Math.Max(0L, record.Revision);
        if (record.Origin != null)
        {
            snapshot.CreationId = record.Origin.CreationId ?? string.Empty;
            snapshot.OriginDefinitionHash = record.Origin.DefinitionHash ?? string.Empty;
            snapshot.OriginDefinitionVersion = record.Origin.DefinitionVersion ?? string.Empty;
            snapshot.SourceProfileName = record.Origin.SourceProfileName ?? string.Empty;
            snapshot.BackgroundId = record.Origin.BackgroundId ?? string.Empty;
            snapshot.DietId = record.Origin.DietId ?? string.Empty;
            for (int i = 0; i < record.Origin.TraitIds.Count && i < RebirthSurvivorNetworkProtocol.MaxTraitIds; i++)
                snapshot.TraitIds.Add(record.Origin.TraitIds[i]);
        }

        if (record.Progression != null)
        {
            snapshot.HealthPotential = record.Progression.HealthPotential;
            List<string> attrIds = new List<string>(record.Progression.Attributes.Keys); attrIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < attrIds.Count && i < RebirthSurvivorNetworkProtocol.MaxAttributes; i++)
            {
                RebirthAttributeRuntimeState value = record.Progression.Attributes[attrIds[i]];
                if (value != null) snapshot.Attributes.Add(new RebirthSurvivorOwnerAttributeSnapshot { Id = value.AttributeId, Current = value.Current, Potential = value.Potential });
            }
            List<string> skillIds = new List<string>(record.Progression.Skills.Keys); skillIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < skillIds.Count && i < RebirthSurvivorNetworkProtocol.MaxSkills; i++)
            {
                RebirthSkillRuntimeState value = record.Progression.Skills[skillIds[i]];
                if (value != null) snapshot.Skills.Add(new RebirthSurvivorOwnerSkillSnapshot { Id = value.SkillId, Value = value.Value, Progress = value.Progress });
            }
            List<string> skillKnowledgeIds = new List<string>(record.Progression.SkillKnowledge.Keys); skillKnowledgeIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < skillKnowledgeIds.Count && i < RebirthSurvivorNetworkProtocol.MaxSkillKnowledge; i++)
            {
                RebirthSkillKnowledgeRuntimeState value = record.Progression.SkillKnowledge[skillKnowledgeIds[i]];
                if (value != null) snapshot.SkillKnowledge.Add(new RebirthSurvivorOwnerSkillKnowledgeSnapshot { Id = value.SkillId, Value = value.Value });
            }
            foreach(var pair in record.Progression.ImprovisationProgress)
                snapshot.ImprovisationProgress[pair.Key]=pair.Value;
            List<string> knowledge = new List<string>(record.Progression.KnowledgeIds); knowledge.Sort(StringComparer.Ordinal);
            // Reserve only the three tutorial receipts within the existing bounded owner list.
            foreach(string marker in knowledge)
                if(RebirthLearningReadEvidence.IsTutorialMarker(marker))snapshot.KnowledgeIds.Add(marker);
            for (int i = 0; i < knowledge.Count && snapshot.KnowledgeIds.Count < RebirthSurvivorNetworkProtocol.MaxKnowledgeIds; i++)
            {
                if(RebirthLearningReadEvidence.IsTutorialMarker(knowledge[i]))continue;
                // Publish each owner's reading receipts for all literature icons.
                snapshot.KnowledgeIds.Add(knowledge[i]);
            }
            List<string> disciplines=new List<string>(record.Progression.AcquiredDisciplineIds);disciplines.Sort(StringComparer.Ordinal);
            for(int i=0;i<disciplines.Count&&snapshot.AcquiredDisciplineIds.Count<RebirthSurvivorNetworkProtocol.MaxDisciplineIds;i++)snapshot.AcquiredDisciplineIds.Add(disciplines[i]);
            List<string> accomplishments=new List<string>(record.Progression.AccomplishmentIds);accomplishments.Sort(StringComparer.Ordinal);for(int i=0;i<accomplishments.Count&&i<64;i++)snapshot.AccomplishmentIds.Add(accomplishments[i]);
            List<string> trials=new List<string>(record.Progression.CompletedTrialIds);trials.Sort(StringComparer.Ordinal);for(int i=0;i<trials.Count&&i<64;i++)snapshot.CompletedTrialIds.Add(trials[i]);
        }

        if (record.Condition != null)
        {
            snapshot.MoodCurrent = record.Condition.MoodCurrent;
            snapshot.MoodTarget = record.Condition.MoodTarget;
            snapshot.DietSatisfaction = record.Condition.DietSatisfaction;
            snapshot.HealthCapacity = record.Condition.HealthCapacity;
            RebirthConditionStatusSnapshot conditionStatus = RebirthSurvivorConditionService.BuildStatus(record);
            snapshot.RecentMealCount = conditionStatus.RecentMealCount;
            snapshot.RecentVarietyCount = conditionStatus.RecentVarietyCount;
            snapshot.LastMealCompatible = conditionStatus.LastMealCompatible;
            snapshot.MoodPositiveCauseId = conditionStatus.DominantPositiveCauseId;
            snapshot.MoodPositiveCauseDelta = conditionStatus.DominantPositiveCauseDelta;
            snapshot.MoodNegativeCauseId = conditionStatus.DominantNegativeCauseId;
            snapshot.MoodNegativeCauseDelta = conditionStatus.DominantNegativeCauseDelta;
        }

        if (record.Support != null)
        {
            List<string> supportIds = new List<string>(record.Support.Entries.Keys); supportIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < supportIds.Count && i < RebirthSurvivorNetworkProtocol.MaxSupportEntries; i++)
            {
                RebirthTraitSupportRuntimeState value = record.Support.Entries[supportIds[i]];
                if (value == null) continue;
                snapshot.SupportEntries.Add(new RebirthSurvivorOwnerSupportSnapshot
                {
                    ProfileId = value.SupportProfileId,
                    GraceRemainingActiveSeconds = value.GraceRemainingActiveSeconds,
                    ManagedRemainingActiveSeconds = value.ManagedRemainingActiveSeconds,
                    PositiveRemainingActiveSeconds = value.PositiveRemainingActiveSeconds,
                    CooldownRemainingActiveSeconds = value.CooldownRemainingActiveSeconds,
                    Stacks = value.Stacks
                });
            }
            snapshot.GearRevision = record.Support.GearRevision;
            List<string> gearSlots = new List<string>(record.Support.EquippedGearBySlot.Keys); gearSlots.Sort(StringComparer.Ordinal);
            for (int i = 0; i < gearSlots.Count && i < RebirthSurvivorNetworkProtocol.MaxGearSlots; i++)
            {
                string slotId = gearSlots[i];
                string itemId = record.Support.EquippedGearBySlot[slotId] ?? string.Empty;
                if (string.IsNullOrEmpty(itemId)) continue;
                snapshot.GearSlots.Add(new RebirthSurvivorOwnerGearSnapshot { SlotId = slotId, ItemId = itemId });
            }
        }
        snapshot.PhysicalBagSlots = RebirthSurvivorGearService.GetDesiredPhysicalBagSlots(record);
        return snapshot;
    }

    private static bool CanSuppressOwnerBody(RebirthStablePlayerIdentity identity, long knownRevision, bool force)
    {
        // A client-supplied revision alone is not evidence of delivery. First/forced, mode-off,
        // creation/recovery, definition, owner and auxiliary transitions always build and send.
        if (force || knownRevision <= 0L || identity == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        RebirthSurvivorOwnerSendHeader previous;
        RebirthWorldCharacterRecord record;
        return LastSentHeaders.TryGetValue(identity.StorageKey, out previous)
            && RebirthWorldCharacterRepository.TryGet(identity, out record) && previous.Matches(record, knownRevision);
    }

    private static void RememberSentHeader(RebirthStablePlayerIdentity identity, RebirthSurvivorOwnerStateSnapshot snapshot)
    {
        if (identity == null) return;
        if (snapshot == null || !snapshot.RebirthModeEnabled || !snapshot.HasCharacter)
        { LastSentHeaders.Remove(identity.StorageKey); return; }
        RebirthWorldCharacterRecord record;
        if (!RebirthWorldCharacterRepository.TryGet(identity, out record) || record == null || record.Revision != snapshot.CharacterRevision
            || record.Support == null || record.Support.GearRevision != snapshot.GearRevision
            || record.Origin == null || !string.Equals(record.Origin.CreationId, snapshot.CreationId, StringComparison.OrdinalIgnoreCase)) return;
        var header = new RebirthSurvivorOwnerSendHeader(record);
        // Only remember the exact character/gear image just sent, never later same-ID replacement state.
        if (!RebirthWorldCharacterRepository.IsCurrentCachedRecord(record) || record.Revision != snapshot.CharacterRevision
            || record.Support == null || record.Support.GearRevision != snapshot.GearRevision
            || record.Origin == null || !string.Equals(record.Origin.CreationId, snapshot.CreationId, StringComparison.OrdinalIgnoreCase)
            || !header.Matches(record, snapshot.CharacterRevision)) return;
        if (!LastSentHeaders.ContainsKey(identity.StorageKey) && LastSentHeaders.Count >= 1024) LastSentHeaders.Clear();
        LastSentHeaders[identity.StorageKey] = header;
    }

    private static RebirthSurvivorCreationNetworkRequest BuildClientRequest(EntityPlayerLocal player,
        RebirthSurvivorCreationNetworkOperation operation,
        RebirthSurvivorCreationSelection selection,
        RebirthSurvivorProfile sourceProfile,
        IDictionary<string, string> creationChoices)
    {
        RebirthSurvivorCreationNetworkRequest request = new RebirthSurvivorCreationNetworkRequest();
        request.PlayerEntityId = player.entityId;
        request.RequestId = RebirthSurvivorClientState.NextRequestId();
        request.Operation = operation;
        request.ClientDefinitionHash = RebirthSurvivorNetworkCodec.Clean(RebirthSurvivorDefinitionRegistry.SemanticHash, RebirthSurvivorNetworkProtocol.MaxHashLength);
        request.ClientDefinitionVersion = RebirthSurvivorNetworkCodec.Clean(RebirthSurvivorDefinitionRegistry.DefinitionVersion, RebirthSurvivorNetworkProtocol.MaxVersionLength);
        request.BackgroundId = RebirthSurvivorNetworkCodec.Clean(selection.BackgroundId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        request.DietId = RebirthSurvivorNetworkCodec.Clean(selection.DietId, RebirthSurvivorNetworkProtocol.MaxIdLength);
        for (int i = 0; i < selection.TraitIds.Count && i < RebirthSurvivorNetworkProtocol.MaxTraitIds; i++)
            request.TraitIds.Add(RebirthSurvivorNetworkCodec.Clean(selection.TraitIds[i], RebirthSurvivorNetworkProtocol.MaxIdLength));

        CopyCreationChoices(request, creationChoices);

        if (sourceProfile != null)
        {
            request.SourceProfileId = RebirthSurvivorNetworkCodec.Clean(sourceProfile.ProfileId, RebirthSurvivorNetworkProtocol.MaxIdLength);
            request.SourceProfileName = RebirthSurvivorNetworkCodec.Clean(sourceProfile.ProfileName, RebirthSurvivorNetworkProtocol.MaxProfileNameLength);
            CopyCreationChoices(request, sourceProfile.CreationChoices);
        }
        return request;
    }

    private static void CopyCreationChoices(RebirthSurvivorCreationNetworkRequest request, IDictionary<string, string> choices)
    {
        if (request == null || choices == null) return;
        int count = request.CreationChoices.Count;
        foreach (KeyValuePair<string, string> pair in choices)
        {
            if (count >= RebirthSurvivorNetworkProtocol.MaxCreationChoices) break;
            string key = RebirthSurvivorNetworkCodec.Clean(pair.Key, RebirthSurvivorNetworkProtocol.MaxChoiceKeyLength);
            if (key.Length == 0 || request.CreationChoices.ContainsKey(key)) continue;
            request.CreationChoices[key] = RebirthSurvivorNetworkCodec.Clean(pair.Value, RebirthSurvivorNetworkProtocol.MaxChoiceValueLength);
            count++;
        }
    }
}
