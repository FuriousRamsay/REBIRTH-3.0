using HarmonyLib;

#nullable disable

/// <summary>
/// ModAPI bootstrap for fresh REBIRTH systems.
/// The previous package had commands that could install patches manually, but no ModAPI, so
/// TileEntity.Instantiate was not patched before saved worlds/tile entities loaded.
/// </summary>
public class RebirthFreshAdvancedFarmingModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthSandboxMenuPatchInstaller.Install();
        RebirthAlwaysStaggerPatchInstaller.Install();
        RebirthHeadshotOnlyBehaviorInstaller.Install();
        RebirthAdvancedFarmingPatchInstaller.Install();
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        ModEvents.PlayerJoinedGame.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerJoinedGameData>(OnPlayerJoinedGame));
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawnedInWorld));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));

#if DEBUG
        if (AdvancedFarmingDebug.Enabled)
            AdvancedFarmingDebug.Log("bootstrap", "ModAPI InitMod completed Advanced Farming patch install and lifecycle hook registration.");
#endif
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        RebirthSandboxRuntimeService.OnGameStarting(data.AsServer);
        ResetAdvancedFarmingRuntimeState("game starting");
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        ResetAdvancedFarmingRuntimeState("world shutting down");
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        ResetAdvancedFarmingRuntimeState("game shutdown");
        RebirthSandboxRuntimeService.OnGameShutdown();
    }

    private static void OnPlayerJoinedGame(ref ModEvents.SPlayerJoinedGameData data)
    {
        // Base 3.1 invokes this at the beginning of RequestToEnterGame, before it queues the
        // world XML payloads and NetPackageGameStats. Send REBIRTH's independent sandbox code
        // at the same lifecycle stage so client-side XML/runtime policy sees server authority.
        RebirthSandboxSyncService.SendSnapshot(data.ClientInfo, "player-joined-game");
    }

    private static void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        // Targeted idempotent resend: covers reconnect/ordering edge cases without re-sending the
        // sandbox snapshot to every already-connected player whenever somebody spawns.
        RebirthSandboxSyncService.SendSnapshot(data.ClientInfo, "player-spawned-resend");
        AdvancedFarmingSyncService.BroadcastRuntimePolicy();
        AdvancedFarmingWeatherSyncService.BroadcastRainOverride();
        AdvancedFarmingSyncService.QueueNearbyPlantStateSnapshot(data.ClientInfo, data.Position);
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        AdvancedFarmingCropGrowthDiagnosticService.Pump(world);
#if DEBUG
        AdvancedFarmingStutterTraceService.Pump();
#endif
        AdvancedFarmingActiveAreaRegistry.PublishPendingSnapshot();
        AdvancedFarmingDeferredPlantStateService.Pump(world);

        if (!AdvancedFarmingRuntimePolicy.Enabled)
            return;

        // With no real REBIRTH farm plots loaded, Advanced Farming is inert.
        // Global hooks remain installed but every runtime subsystem exits here.
        if (!AdvancedFarmingActiveAreaRegistry.HasAnyFarmPlots)
        {
            AdvancedFarmingDynamicLightOpacityService.ClearPendingSunlightRefreshes();
            AdvancedFarmingDoorVisualRefreshService.ClearPendingVisualRefreshes();
            AdvancedFarmingSyncService.ClearPendingPlantStateSnapshots();
            return;
        }

        AdvancedFarmingDynamicLightOpacityService.PumpPendingSunlightRefreshesForGameUpdate();
        AdvancedFarmingSyncService.PumpPendingGeometryDrivenPlantLightRefreshes();
        AdvancedFarmingDoorVisualRefreshService.PumpPendingVisualRefreshesForGameUpdate();
        AdvancedFarmingSyncService.PumpPendingPlantStateSnapshots();
        AdvancedFarmingCatchupService.PumpCatchupWatchForGameUpdate(world);
    }

    private static void ResetAdvancedFarmingRuntimeState(string reason)
    {
        AdvancedFarmingWaterProviderRegistry.Clear();
        AdvancedFarmingActiveAreaRegistry.Clear();
        AdvancedFarmingHeatQueryService.ClearRegisteredHeatSources();
        AdvancedFarmingHoverTextService.ClearAllState();
        AdvancedFarmingLightService.ClearCache();
        AdvancedFarmingTemperatureService.ClearCache();
        AdvancedFarmingCatchupService.ResetSchedulingState();
        ConsoleCmdRebirthAdvancedFarmingFresh.ResetAutomationState(reason);
        AdvancedFarmingPerfSnapshotService.ResetSampling();
        AdvancedFarmingDynamicLightOpacityService.ClearPendingSunlightRefreshes();
        AdvancedFarmingDoorVisualRefreshService.ClearPendingVisualRefreshes();
        AdvancedFarmingSyncService.ClearPendingPlantStateSnapshots();
        AdvancedFarmingDeferredPlantStateService.Clear();
        AdvancedFarmingCropGrowthDiagnosticService.Reset(reason);
#if DEBUG
        if (AdvancedFarmingStutterTraceService.Active) AdvancedFarmingStutterTraceService.Stop();
#endif

#if DEBUG
        if (AdvancedFarmingDebug.Enabled)
            AdvancedFarmingDebug.Log("bootstrap", "Advanced Farming runtime state reset: " + reason);
#endif
    }
}
