using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthNpcLifecycleState : byte
{
    Cold = 0,
    BootstrapRegistered = 1,
    WorldActive = 2,
    WorldStopping = 3,
    GameStopping = 4
}

public sealed class RebirthNpcLifecycleSnapshot
{
    public RebirthNpcLifecycleState State { get; internal set; }
    public int BootstrapGeneration { get; internal set; }
    public int WorldGeneration { get; internal set; }
    public int ActiveEntities { get; internal set; }
    public long EntityActivations { get; internal set; }
    public long EntityDeactivations { get; internal set; }
    public long DuplicateBootstrapAttempts { get; internal set; }
    public long WorldStopCount { get; internal set; }
    public long GameStopCount { get; internal set; }
    public long ForcedWorldBoundaryResets { get; internal set; }
    public string LastTransition { get; internal set; }
    public string[] RegisteredPackages { get; internal set; }
    public string[] RegisteredServices { get; internal set; }
}

/// <summary>
/// Owns NPC bootstrap registration and world-scoped static-state cleanup.
/// Registration is process-scoped and idempotent; runtime state is world-scoped
/// and is explicitly cleared during world/game shutdown.
/// </summary>
public static class RebirthNpcLifecycle
{
    private static readonly object Sync = new object();
    private static readonly HashSet<int> ActiveEntityIds = new HashSet<int>();
    private static readonly SortedSet<string> Packages = new SortedSet<string>(StringComparer.Ordinal);
    private static readonly SortedSet<string> Services = new SortedSet<string>(StringComparer.Ordinal);

    private static bool handlersRegistered;
    private static int bootstrapGeneration;
    private static int worldGeneration;
    private static long entityActivations;
    private static long entityDeactivations;
    private static long duplicateBootstrapAttempts;
    private static long worldStopCount;
    private static long gameStopCount;
    private static long forcedWorldBoundaryResets;
    private static World activeWorldReference;
    private static string activeSaveDirectory = string.Empty;
    private static RebirthNpcLifecycleState state = RebirthNpcLifecycleState.Cold;
    private static string lastTransition = "cold";

    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (handlersRegistered)
            {
                duplicateBootstrapAttempts++;
                RebirthNpcLifecycleQualificationService.RecordBootstrap(GetSnapshot(), true);
                return;
            }

            RegisterPackagesNoLock();
            RegisterServicesNoLock();
            RebirthNpcSocialGameplayEventPatchInstaller.Install();
            RebirthNpcCompatibilityQualificationService.Audit(false);
            RebirthNpcBuildQualificationService.Audit();
            RebirthNpcBaseGameAdapterRegistry.EnsureInitialized();
            RebirthNpcLegacyMigrationRegistry.EnsureInitialized();
            RebirthNpcLegacy26ImportService.EnsureInitialized();
            RebirthNpcPersistenceTimelineService.EnsureAligned();
            RebirthNpcAggregatePersistenceStore.EnsureLoaded();
            RebirthNpcScaleHardeningService.EnsureInitialized();
            RebirthNpcAdministrationSupportService.EnsureInitialized();
            RebirthNpcProgressionService.EnsureInitialized();
            RebirthNpcProgressionModifierService.EnsureInitialized();
            RebirthNpcProfessionProgressionService.EnsureInitialized();
            RebirthNpcAdvancedProgressionService.EnsureInitialized();
            RebirthNpcCategoryCombatAiRegistry.EnsureInitialized();
            RebirthNpcLifecycleGameplayService.EnsureInitialized();
            RebirthNpcWorldIntegrationService.EnsureInitialized();
            RebirthDogLifecycleService.EnsureInitialized();
            RebirthDogZombieRetaliationInstaller.Install();
            RebirthNpcEconomyLogisticsService.EnsureInitialized();
            RebirthNpcReplicationPerformanceService.EnsureInitialized();
            RebirthNpcProgressionReplicationService.GetReport();
            RebirthNpcReleaseCandidateValidationService.Run();
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(
                new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawnedInWorld));
            ModEvents.GameUpdate.RegisterHandler(
                new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
            ModEvents.WorldShuttingDown.RegisterHandler(
                new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
            ModEvents.GameShutdown.RegisterHandler(
                new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));

            handlersRegistered = true;
            bootstrapGeneration++;
            state = RebirthNpcLifecycleState.BootstrapRegistered;
            lastTransition = "bootstrap-registered";
        }

        RebirthNpcLifecycleQualificationService.RecordBootstrap(GetSnapshot(), false);
        { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH NPC Lifecycle] registered generation=" + bootstrapGeneration +
            " packages=" + Packages.Count + " services=" + Services.Count); }
    }

    public static void EnsureCurrentWorldScope()
    {
        bool needsRegistration;
        lock (Sync) needsRegistration = !handlersRegistered;
        if (needsRegistration) EnsureRegistered();
        PrepareWorldScope(GameManager.Instance != null ? GameManager.Instance.World : null);
    }

    public static void OnWorldAvailable()
    {
        EnsureCurrentWorldScope();
        bool transitioned = false;
        lock (Sync)
        {
            if (state != RebirthNpcLifecycleState.WorldActive)
            {
                worldGeneration++;
                state = RebirthNpcLifecycleState.WorldActive;
                lastTransition = "world-active:" + worldGeneration;
                transitioned = true;
            }
        }
        RebirthNpcLifecycleSnapshot snapshot = GetSnapshot();
        if (transitioned)
        {
            // InitMod has no World/save scope. Load world-owned domains here, not as
            // successful empty stores during process bootstrap.
            if (GameManager.Instance?.World != null && !GameManager.Instance.World.IsRemote())
            {
                try { RebirthNpcPersistenceCoordinator.EnsureLoaded(); }
                catch (Exception ex)
                {
                    // Preserve the failed store and its original/backup files. The checkpoint
                    // writer will refuse to overwrite a rejected domain with default data.
                    Log.Warning("[REBIRTH NPC Lifecycle] world persistence load incomplete: " + ex.GetType().Name + ": " + ex.Message);
                }
            }
            RebirthNpcLifecycleQualificationService.ObserveWorldStart(snapshot.WorldGeneration);
        }
        RebirthNpcLifecycleQualificationService.RecordTransition(transitioned ? "world-active" : "world-active-duplicate", snapshot, string.Empty);
    }

    public static void OnEntityActivated(int entityId)
    {
        OnWorldAvailable();
        bool added;
        lock (Sync)
        {
            added = ActiveEntityIds.Add(entityId);
            if (added) entityActivations++;
        }
        RebirthNpcLifecycleQualificationService.RecordTransition(added ? "entity-activated" : "entity-activation-duplicate", GetSnapshot(), "entityId=" + entityId);
    }

    public static void OnEntityDeactivated(int entityId, bool terminalRemoval)
    {
        lock (Sync)
        {
            if (ActiveEntityIds.Remove(entityId))
                entityDeactivations++;
            lastTransition = (terminalRemoval ? "entity-removed:" : "entity-unloaded:") + entityId;
        }
        RebirthNpcRuntimeRegistry.Unregister(entityId, terminalRemoval);
        RebirthNpcLifecycleQualificationService.RecordTransition(terminalRemoval ? "entity-removed" : "entity-unloaded", GetSnapshot(), "entityId=" + entityId);
    }

    public static RebirthNpcLifecycleSnapshot GetSnapshot()
    {
        lock (Sync)
        {
            return new RebirthNpcLifecycleSnapshot
            {
                State = state,
                BootstrapGeneration = bootstrapGeneration,
                WorldGeneration = worldGeneration,
                ActiveEntities = ActiveEntityIds.Count,
                EntityActivations = entityActivations,
                EntityDeactivations = entityDeactivations,
                DuplicateBootstrapAttempts = duplicateBootstrapAttempts,
                WorldStopCount = worldStopCount,
                GameStopCount = gameStopCount,
                ForcedWorldBoundaryResets = forcedWorldBoundaryResets,
                LastTransition = lastTransition,
                RegisteredPackages = Copy(Packages),
                RegisteredServices = Copy(Services)
            };
        }
    }

    public static string GetReport()
    {
        RebirthNpcLifecycleSnapshot snapshot = GetSnapshot();
        StringBuilder builder = new StringBuilder();
        builder.Append("[REBIRTH NPC Lifecycle] state=").Append(snapshot.State)
            .Append(" bootstrapGeneration=").Append(snapshot.BootstrapGeneration)
            .Append(" worldGeneration=").Append(snapshot.WorldGeneration)
            .Append(" activeEntities=").Append(snapshot.ActiveEntities)
            .Append(" activations=").Append(snapshot.EntityActivations)
            .Append(" deactivations=").Append(snapshot.EntityDeactivations)
            .Append(" duplicateBootstrapAttempts=").Append(snapshot.DuplicateBootstrapAttempts)
            .Append(" worldStops=").Append(snapshot.WorldStopCount)
            .Append(" gameStops=").Append(snapshot.GameStopCount)
            .Append(" forcedWorldBoundaryResets=").Append(snapshot.ForcedWorldBoundaryResets)
            .Append(" last=").Append(snapshot.LastTransition)
            .AppendLine();
        builder.Append("  packages=").Append(string.Join(",", snapshot.RegisteredPackages)).AppendLine();
        builder.Append("  services=").Append(string.Join(",", snapshot.RegisteredServices));
        return builder.ToString();
    }

    private static void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        OnWorldAvailable();
        RebirthNpcNetworkBaselineService.SendTo(data.ClientInfo);
        RebirthNpcProgressionReplicationService.SendBaseline(data.ClientInfo);

        // In b10, ClientInfo is null for the local/listen-server player. EntityId is
        // supplied by the spawn event for BOTH local and remote players.
        World world = GameManager.Instance?.World;
        if (world != null && !world.IsRemote() &&
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer == true)
        {
            EntityPlayer player = world.GetEntity(data.EntityId) as EntityPlayer;
            if (player != null)
            {
                // Queue before the immediate attempt, so a late persistent-player index or
                // streaming dog cannot strand the owner without a bounded retry.
                RebirthDogLifecycleService.ScheduleGhostRepair(player);
                try
                {
                    RebirthDogChunkObserverService.RefreshForPlayer(player);
                    RebirthDogLifecycleService.ReconcileOwnerProjectionForPlayer(player);
                    RebirthDogCapacitySyncService.NotifyOwner(player);
                }
                catch (Exception ex)
                {
                    Log.Warning("[REBIRTH Dog] owner-spawn reconciliation deferred player=" + player.entityId +
                        " reason=" + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
        RebirthDogCapacitySyncService.NotifyClient(data.ClientInfo);
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        RebirthNpcPreparedSchedulerAdmission.Tick();
        RebirthDogLifecycleService.TickGhostRepairRetries();
        RebirthNpcInteractionNetworkClient.Update();
        RebirthTheorySpecialistLessonService.Tick();
        RebirthTheorySpecialistLessonService.RetryPending();
        RebirthNpcNetworkBaselineService.Tick();
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        ShutdownWorld(false);
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        ShutdownWorld(true);
    }

    private static void ShutdownWorld(bool gameShutdown)
    {
        // GameShutdown commonly follows an already-completed WorldShuttingDown. Do not
        // call aggregate getters without a World: they correctly discard unavailable scopes.
        // If the earlier save failed, that call would destroy the state retained for retry.
        if (GameManager.Instance?.World == null)
        {
            bool retained;
            lock (Sync) retained = activeWorldReference != null;
            if (retained && !RebirthNpcPersistenceCoordinator.LastSaveSucceeded)
                Log.Warning("[REBIRTH NPC Lifecycle] game shutdown after failed world save; no world-bound retry is possible. Retained caches are not reloaded or cleared.");
            return;
        }

        if (GameManager.Instance.World.IsRemote())
        {
            // A multiplayer client has projections, not authority to publish NPC sidecars.
            ResetUnexpectedWorldScopedState();
            lock (Sync)
            {
                ActiveEntityIds.Clear(); activeWorldReference = null; activeSaveDirectory = string.Empty;
                if (gameShutdown) gameStopCount++; else worldStopCount++;
                state = gameShutdown ? RebirthNpcLifecycleState.GameStopping : RebirthNpcLifecycleState.BootstrapRegistered;
                lastTransition = "client-world-stopped";
            }
            return;
        }
        lock (Sync)
        {
            state = gameShutdown ? RebirthNpcLifecycleState.GameStopping : RebirthNpcLifecycleState.WorldStopping;
            if (gameShutdown) gameStopCount++; else worldStopCount++;
            lastTransition = gameShutdown ? "game-stopping" : "world-stopping";
        }

        RebirthNpcLifecycleQualificationService.RecordTransition(gameShutdown ? "game-stopping" : "world-stopping", GetSnapshot(), string.Empty);

        // Save-bearing stores are reset before volatile registries so restart
        // rehydration never observes partially cleared runtime state.
        string persistenceError;
        bool persistenceSaved = RebirthNpcPersistenceCoordinator.SaveAll(out persistenceError);
        if (!persistenceSaved)
        {
            // One bounded retry while GameIO still points at the departing save. Stores keep
            // their dirty/in-memory generation after a failed pass, so this retry cannot be
            // converted into a false-success reset.
            string retryError;
            persistenceSaved = RebirthNpcPersistenceCoordinator.SaveAll(out retryError);
            if (!persistenceSaved && !string.IsNullOrEmpty(retryError))
                persistenceError = persistenceError + " | retry: " + retryError;
        }
        RebirthNpcPreparedSchedulerAdmission.RetireSavedWorld(GameManager.Instance.World,persistenceSaved);
        ResetWorldScopedState(gameShutdown, persistenceSaved);

        lock (Sync)
        {
            ActiveEntityIds.Clear();
            if (persistenceSaved)
            {
                activeWorldReference = null;
                activeSaveDirectory = string.Empty;
            }
            state = gameShutdown ? RebirthNpcLifecycleState.GameStopping : RebirthNpcLifecycleState.BootstrapRegistered;
            lastTransition = gameShutdown ? "game-stopped" : "world-stopped";
        }
        RebirthNpcLifecycleQualificationService.RecordTransition(gameShutdown ? "game-stopped" : "world-stopped", GetSnapshot(), persistenceError);
        RebirthNpcLifecycleQualificationService.AuditWorldReset(gameShutdown ? "game-shutdown" : "world-shutdown");
    }

    /// <summary>
    /// Defends against cross-save state leakage when the engine changes World instances
    /// without delivering (or fully completing) the expected WorldShuttingDown callback.
    /// Never save the old in-memory NPC state here: GameIO now points at the NEW save, so
    /// writing would contaminate that save. Old world-scoped caches are discarded and the
    /// new world's sidecars are then loaded lazily/freshly.
    /// </summary>
    private static void PrepareWorldScope(World currentWorld)
    {
        if (currentWorld == null) return;

        string currentSaveDirectory = string.Empty;
        try { currentSaveDirectory = GameIO.GetSaveGameDir() ?? string.Empty; }
        catch { currentSaveDirectory = string.Empty; }

        bool unexpectedBoundary = false;
        string previousSaveDirectory = string.Empty;
        World previousWorld = null;
        lock (Sync)
        {
            if (activeWorldReference == null)
            {
                activeWorldReference = currentWorld;
                activeSaveDirectory = currentSaveDirectory;
                return;
            }

            bool worldChanged = !object.ReferenceEquals(activeWorldReference, currentWorld);
            bool saveChanged = !string.IsNullOrEmpty(activeSaveDirectory) &&
                               !string.IsNullOrEmpty(currentSaveDirectory) &&
                               !string.Equals(activeSaveDirectory, currentSaveDirectory, StringComparison.OrdinalIgnoreCase);

            if (!worldChanged && !saveChanged)
            {
                // The save path can become available a little after the World object.
                if (string.IsNullOrEmpty(activeSaveDirectory) && !string.IsNullOrEmpty(currentSaveDirectory))
                    activeSaveDirectory = currentSaveDirectory;
                return;
            }

            unexpectedBoundary = true;
            previousSaveDirectory = activeSaveDirectory;
            previousWorld = activeWorldReference;
        }

        if (!unexpectedBoundary) return;

        Log.Warning("[REBIRTH NPC Lifecycle] unexpected world boundary detected; discarding stale world-scoped state without saving." +
                    " oldSave='" + previousSaveDirectory + "' newSave='" + currentSaveDirectory + "'.");

        RebirthNpcPreparedSchedulerAdmission.RetireUnexpectedWorld(previousWorld,currentWorld,previousSaveDirectory,currentSaveDirectory);
        // IMPORTANT: do not call SaveAll here. At this point GameIO may already resolve to
        // the new save. Saving old records would be worse than dropping the stale cache.
        ResetUnexpectedWorldScopedState();

        lock (Sync)
        {
            ActiveEntityIds.Clear();
            forcedWorldBoundaryResets++;
            activeWorldReference = currentWorld;
            activeSaveDirectory = currentSaveDirectory;
            state = RebirthNpcLifecycleState.BootstrapRegistered;
            lastTransition = "forced-world-boundary-reset:" + forcedWorldBoundaryResets;
        }

        // Force the aggregate to bind to the new save immediately. Other domain stores are
        // lazy and now have loaded=false after ResetWorldScopedState.
        RebirthNpcPersistenceTimelineService.EnsureAligned();
        RebirthNpcAggregatePersistenceStore.EnsureLoaded();
    }

    /// <summary>
    /// Emergency no-save reset used only when a new World is already active but the old
    /// world's shutdown cleanup did not complete. Keep this list intentionally limited to
    /// reset APIs that do not write through GameIO: at this point GameIO may point at the
    /// new save, so any implicit Save() would contaminate it with the old world's data.
    /// </summary>
    private static void ResetUnexpectedWorldScopedState()
    {
        RebirthNpcPersistenceCoordinator.ResetForUnexpectedWorldBoundary();
        RebirthNpcPersistenceTimelineService.ResetForWorld();
        RebirthDogCapacitySyncService.Reset();
        RebirthDogLifecycleService.ResetGhostRepairRetries();
        RebirthCompanionService.ResetForWorldChange();
        RebirthDogStorageService.ResetForWorldChange();
        RebirthNpcProgressionReplicationService.Reset();
        RebirthNpcActivityRecoveryRegistry.ResetForWorldChange();
        RebirthNpcCommandGateway.ResetForWorldChange("Unexpected world boundary.");
        RebirthNpcDecisionEngine.ResetForWorldChange();
        RebirthNpcWorkNavigationService.ResetForWorldChange();
        RebirthNpcWorkOutcomeService.ResetForWorldChange();
        RebirthNpcTradeService.ResetForWorldChange();
        RebirthNpcDialogueService.ResetForWorldChange();
        RebirthNpcInteractionNetworkClient.Reset();
        RebirthNpcPlayerInventoryEndpointService.Reset();
        RebirthDogChunkObserverService.ResetForWorldChange();
        RebirthDogRuntimeService.Reset();
        RebirthDogAutoLootBridge.ResetForWorldChange();
        RebirthNpcRuntimeRegistry.ClearForWorldChange();
    }

    private static void ResetWorldScopedState(bool gameShutdown, bool persistenceSaved)
    {
        if (!persistenceSaved)
        {
            // Several persistence serializers read service-owned records. Resetting those
            // services after a failed save would contradict the promise to retain retry state.
            RebirthDogLifecycleService.ResetGhostRepairRetries();
            Log.Warning("[REBIRTH NPC Lifecycle] coordinated save failed after retry; stores AND backing service state retained until retry or an explicit world-boundary discard.");
            return;
        }
        RebirthNpcPersistenceCoordinator.ResetAfterSave();
        RebirthNpcPersistenceTimelineService.ResetForWorld();
        RebirthDogCapacitySyncService.Reset();
        RebirthDogLifecycleService.ResetGhostRepairRetries();
        RebirthNpcIdentityAuditLedger.ResetForWorldChange();
        RebirthNpcWorkRuntime.ResetForWorldChange(gameShutdown ? "Game is shutting down." : "World is changing.");
        RebirthNpcApiCompatibility.Reset();
        RebirthNpcCompatibilityQualificationService.ResetForWorldChange();
        RebirthNpcBuildQualificationService.ResetForWorldChange();
        RebirthNpcBaseGameAdapterRegistry.ResetForWorldChange();
        RebirthNpcLegacyMigrationRegistry.ResetForWorldChange();
        RebirthNpcLegacy26ImportService.ResetForWorldChange();
        RebirthNpcThirdPartyCapabilityRegistry.ResetForWorldChange();
        RebirthNpcNetworkProtocol.ResetForWorldChange();
        RebirthCompanionService.ResetForWorldChange();
        RebirthDogStorageService.ResetForWorldChange();
        RebirthNpcProgressionReplicationService.Reset();
        RebirthNpcActivityRecoveryRegistry.ResetForWorldChange();
        RebirthNpcCommandGateway.ResetForWorldChange(gameShutdown ? "Game is shutting down." : "World is changing.");
        RebirthNpcDecisionEngine.ResetForWorldChange();
        RebirthNpcNavigationRuntimeAdapter.ResetForWorldChange();
        RebirthNpcWorkNavigationService.ResetForWorldChange();
        RebirthNpcWorkOutcomeService.ResetForWorldChange();
        RebirthNpcTradeService.ResetForWorldChange();
        RebirthNpcDialogueService.ResetForWorldChange();
        RebirthNpcItemIntegrityService.ResetForWorldChange();
        RebirthNpcFarmingLogisticsQualificationService.ResetForWorldChange();
        RebirthNpcProductionResourceQualificationService.ResetForWorldChange();
        RebirthNpcDutyMedicalQualificationService.ResetForWorldChange();
        RebirthNpcSocialService.ResetForWorldChange();
        RebirthNpcCombatEmergencyService.ResetForWorldChange();
        RebirthNpcFactionGameplayService.ResetForWorldChange();
        RebirthNpcPerformanceQualificationService.ResetForWorldChange();
        RebirthNpcScaleHardeningService.ResetForWorldChange();
        RebirthNpcAdministrationSupportService.ResetForWorldChange();
        RebirthNpcProgressionService.ResetForWorldChange();
        RebirthNpcProfessionProgressionService.ResetForWorldChange();
        RebirthNpcAdvancedProgressionService.ResetForWorldChange();
        RebirthNpcWeaponProgressionService.ResetForWorldChange();
        RebirthNpcReleaseCandidateValidationService.ResetForWorldChange();
        RebirthNpcLifecycleGameplayService.ResetForWorldChange();
        RebirthNpcWorldIntegrationService.ResetForWorldChange();
        RebirthNpcEconomyLogisticsService.ResetForWorldChange();
        RebirthNpcReplicationPerformanceService.ResetForWorldChange();
        RebirthNpcInteractionNetworkClient.Reset();
        RebirthNpcPlayerInventoryEndpointService.Reset();
        RebirthDogChunkObserverService.ResetForWorldChange();
        RebirthDogRuntimeService.Reset();
        RebirthDogAutoLootBridge.ResetForWorldChange();
        RebirthNpcRuntimeRegistry.ClearForWorldChange();
        RebirthNpcLifecycleQualificationService.ResetForWorldChange(gameShutdown);
    }

    private static void RegisterPackagesNoLock()
    {
        Packages.Add(typeof(NetPackageRebirthNpcRuntimeState).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcCommandRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcCommandAck).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcInteractionRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcInteractionResponse).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcSemanticEquipment).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcAppearanceEquipment).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcBaselineStart).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcBaselineComplete).FullName);
        Packages.Add(typeof(NetPackageRebirthNpcProgression).FullName);
        Packages.Add(typeof(NetPackageRebirthDogCapacitySync).FullName);
        Packages.Add(typeof(NetPackageRebirthDogHireRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthDogDeployRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthDogLifecycleRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthDogPickupFeedback).FullName);
        Packages.Add(typeof(NetPackageRebirthDogRenameRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthDogNavigationSnapshot).FullName);
        Packages.Add(typeof(NetPackageRebirthDogAutoLootRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthDogAutoLootResponse).FullName);
        Packages.Add(typeof(NetPackageRebirthBeastmasterInteractionRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthBlackMagicInteractionRequest).FullName);
        Packages.Add(typeof(NetPackageRebirthBoundUndeadInteractionRequest).FullName);
    }

    private static void RegisterServicesNoLock()
    {
        Services.Add(typeof(RebirthNpcFoundationService).FullName);
        Services.Add(typeof(RebirthNpcCommandGateway).FullName);
        Services.Add(typeof(RebirthNpcActivityRegistry).FullName);
        Services.Add(typeof(RebirthNpcExecutionPersistenceStore).FullName);
        Services.Add(typeof(RebirthNpcSocialPersistenceStore).FullName);
        Services.Add(typeof(RebirthNpcStableIdentityStore).FullName);
        Services.Add(typeof(RebirthNpcInventoryAuthorityService).FullName);
        Services.Add(typeof(RebirthNpcEquipmentService).FullName);
        Services.Add(typeof(RebirthNpcItemIntegrityService).FullName);
        Services.Add(typeof(RebirthNpcFarmingLogisticsQualificationService).FullName);
        Services.Add(typeof(RebirthNpcProductionResourceQualificationService).FullName);
        Services.Add(typeof(RebirthNpcDutyMedicalQualificationService).FullName);
        Services.Add(typeof(RebirthNpcInteractionPresentationService).FullName);
        Services.Add(typeof(RebirthNpcPlayerInventoryEndpointService).FullName);
        Services.Add(typeof(RebirthNpcDirectActivationBinding).FullName);
        Services.Add(typeof(RebirthNpcTradeService).FullName);
        Services.Add(typeof(RebirthNpcDialogueService).FullName);
        Services.Add(typeof(RebirthNpcFactionGameplayService).FullName);
        Services.Add(typeof(RebirthNpcSocialGameplayEventProducers).FullName);
        Services.Add(typeof(RebirthNpcCombatEmergencyService).FullName);
        Services.Add(typeof(RebirthNpcTargetingService).FullName);
        Services.Add(typeof(RebirthNpcFriendlyFirePolicy).FullName);
        Services.Add(typeof(RebirthNpcCategoryCombatAiRegistry).FullName);
        Services.Add(typeof(RebirthNpcCombatExecutionService).FullName);
        Services.Add(typeof(RebirthNpcLifecycleGameplayService).FullName);
        Services.Add(typeof(RebirthNpcProductionProfileCatalogue).FullName);
        Services.Add(typeof(RebirthNpcWorldIntegrationService).FullName);
        Services.Add(typeof(RebirthDogLifecycleService).FullName);
        Services.Add(typeof(RebirthDogCapacitySyncService).FullName);
        Services.Add(typeof(RebirthDogStateService).FullName);
        Services.Add(typeof(RebirthDogInventoryService).FullName);
        Services.Add(typeof(RebirthDogChunkObserverService).FullName);
        Services.Add(typeof(RebirthDogRuntimeService).FullName);
        Services.Add(typeof(RebirthNpcWeaponUseBridge).FullName);
        Services.Add(typeof(RebirthNpcCombatPersistenceStore).FullName);
        Services.Add(typeof(RebirthNpcCompatibilityQualificationService).FullName);
        Services.Add(typeof(RebirthNpcBuildQualificationService).FullName);
        Services.Add(typeof(RebirthNpcLifecycleQualificationService).FullName);
        Services.Add(typeof(RebirthNpcBaseGameAdapterRegistry).FullName);
        Services.Add(typeof(RebirthNpcLegacyMigrationRegistry).FullName);
        Services.Add(typeof(RebirthNpcLegacy26ImportService).FullName);
        Services.Add(typeof(RebirthNpcAggregatePersistenceStore).FullName);
        Services.Add(typeof(RebirthNpcThirdPartyCapabilityRegistry).FullName);
        Services.Add(typeof(RebirthNpcPerformanceQualificationService).FullName);
        Services.Add(typeof(RebirthNpcScaleHardeningService).FullName);
        Services.Add(typeof(RebirthNpcSpatialIndex).FullName);
        Services.Add(typeof(RebirthNpcPathBudgetCoordinator).FullName);
        Services.Add(typeof(RebirthNpcNetworkBatcher).FullName);
        Services.Add(typeof(RebirthNpcSerializationBufferPool).FullName);
        Services.Add(typeof(RebirthNpcRelationshipBatcher).FullName);
        Services.Add(typeof(RebirthNpcReplicationPerformanceService).FullName);
        Services.Add(typeof(RebirthNpcRetentionCoordinator).FullName);
        Services.Add(typeof(RebirthNpcScaleBenchmarkService).FullName);
        Services.Add(typeof(RebirthNpcAdministrationSupportService).FullName);
        Services.Add(typeof(RebirthNpcAdminAuditLedger).FullName);
        Services.Add(typeof(RebirthNpcReleaseCandidateValidationService).FullName);
        Services.Add(typeof(RebirthNpcReleaseEvidenceLedger).FullName);
        Services.Add(typeof(RebirthNpcReleaseQualificationService).FullName);
        Services.Add(typeof(RebirthNpcRequirementCatalogue).FullName);
        Services.Add(typeof(RebirthNpcProgressionService).FullName);
        Services.Add(typeof(RebirthNpcProgressionModifierService).FullName);
        Services.Add(typeof(RebirthNpcWeaponProgressionService).FullName);
        Services.Add(typeof(RebirthNpcProgressionPersistenceStore).FullName);
        Services.Add(typeof(RebirthNpcProgressionQualificationService).FullName);
        Services.Add(typeof(RebirthNpcAdvancedProgressionService).FullName);
        Services.Add(typeof(RebirthNpcAdvancedProgressionPersistenceStore).FullName);
        Services.Add(typeof(RebirthNpcAdvancedProgressionQualificationService).FullName);
        Services.Add(typeof(RebirthNpcProgressionReplicationService).FullName);
        Services.Add(typeof(RebirthNpcProgressionProjectionService).FullName);
        Services.Add(typeof(RebirthNpcProgressionClientCache).FullName);
        Services.Add(typeof(RebirthNpcProgressionTelemetry).FullName);
        Services.Add(typeof(RebirthNpcProgressionUiService).FullName);
        Services.Add(typeof(RebirthNpcProgressionAdministration).FullName);
        Services.Add(typeof(RebirthNpcWorkProgressionSink).FullName);
        Services.Add(typeof(RebirthNpcConformanceControlPlane).FullName);
        Services.Add(typeof(RebirthNpcNetworkProtocol).FullName);
        Services.Add(typeof(RebirthNpcNetworkBaselineService).FullName);
        Services.Add(typeof(RebirthNpcNavigationRuntimeAdapter).FullName);
    }

    private static string[] Copy(SortedSet<string> values)
    {
        string[] result = new string[values.Count];
        values.CopyTo(result);
        return result;
    }
}

[Preserve]
public sealed class RebirthNpcLifecycleModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        RebirthNpcLifecycle.EnsureRegistered();
    }
}
