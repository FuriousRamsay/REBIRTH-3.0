using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Independent lifecycle bootstrap for the REBIRTH Block Pickup feature.
/// This intentionally does not depend on or modify the Advanced Farming bootstrap.
/// </summary>
[Preserve]
public sealed class RebirthBlockPickupModApi : IModApi
{
    private static bool initialized;

    public void InitMod(Mod modInstance)
    {
        if (initialized) return;

        try { RebirthTemporaryProfileWriteGuard.Install(); }
        catch (System.Exception ex)
        {
            // Ownership-dependent automatic deletion stays disabled if the native save guard
            // could not be established. Do not disable unrelated block-pickup/gameplay hooks.
            Log.Error("[REBIRTH Profiles] Temporary profile write guard unavailable; automatic cleanup disabled: " + ex.Message);
        }

        string blockReport = RebirthBlockPickupPatchInstaller.Install();
        if (!RebirthBlockPickupPatchInstaller.Installed && !RebirthBlockPickupPatchInstaller.LagTestDisabled)
            throw new System.InvalidOperationException("Block Pickup patch installation incomplete: " + blockReport);
        RebirthNativeSecureAccessPatchInstaller.Install();
        if (!RebirthNativeSecureAccessPatchInstaller.IsInstalled)
            throw new System.InvalidOperationException("Native secure-access patch installation incomplete.");

        // Register lifecycle callbacks only after every required patch installer reached a
        // coherent terminal state. A failed patch attempt therefore remains retryable without
        // multiplying event subscriptions on the next InitMod call.
        ModEvents.GameUpdate.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.GameStarting.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.WorldShuttingDown.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        ModEvents.PlayerJoinedGame.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerJoinedGameData>(OnPlayerJoinedGame));
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(
            new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawnedInWorld));
        initialized = true;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        RebirthWorkstationSecurityService.PumpNotifications();
        RebirthDiagnosticPolicy.TickManualScopes();
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        RebirthDiagnosticPolicy.ResetManualScopes();
        RemoteResourceLiveSync.Reset();
        RebirthCookingJobRuntime.Reset();
        RebirthBlockPickupClassifier.ClearCache();
        RebirthBlockPickupService.ClearRuntimeCaches();
        RebirthWorkstationSecurityService.Initialize(data.AsServer);
        RebirthContainerNameRegistry.Reset(data.AsServer);
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthDiagnosticPolicy.ResetManualScopes();
        RemoteResourceLiveSync.Reset();
        RebirthCookingJobRuntime.Reset();
        RebirthWorkstationSecurityService.Shutdown(true);
        RebirthContainerNameRegistry.Reset(false);
        RebirthBlockPickupService.ClearRuntimeCaches();
    }

    private static void OnPlayerJoinedGame(ref ModEvents.SPlayerJoinedGameData data)
    {
        RebirthContainerNameRegistry.SendSnapshot(data.ClientInfo);
    }

    private static void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        // Idempotent resend covers a client whose GameStarting callback happened after the
        // early PlayerJoinedGame snapshot and cleared its prior-world name cache.
        RebirthContainerNameRegistry.SendSnapshot(data.ClientInfo);
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        RebirthDiagnosticPolicy.ResetManualScopes();
        RemoteResourceLiveSync.Reset();
        RebirthCookingJobRuntime.Reset();
        RebirthWorkstationSecurityService.Shutdown(true);
        RebirthContainerNameRegistry.Reset(false);
        RebirthBlockPickupClassifier.ClearCache();
        RebirthBlockPickupService.ClearRuntimeCaches();
    }
}
