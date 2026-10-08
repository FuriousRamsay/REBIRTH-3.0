using System;
using HarmonyLib;

#nullable disable

/// <summary>
/// Bounded persistence lifecycle. World-character files are flushed on world save,
/// player disconnect, world shutdown and game shutdown; they are never written every tick.
/// </summary>
public static class RebirthSurvivorPersistenceLifecycle
{
    private static bool installed;
    private static readonly Harmony Harmony = new Harmony("rebirth.survivor.persistence.3.0");

    public static string Install()
    {
        if (installed)
            return "[REBIRTH Survivor] persistence lifecycle already installed.";
        installed = true;

        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorPersistenceSaveWorldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorPersistenceDisconnectPatch));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawnedInWorld));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Survivor] bounded persistence lifecycle installed.";
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        RebirthStablePlayerIdentity.ResetRuntimeCache();
        RebirthWorldCharacterRepository.Reset(data.AsServer);
        // Metabolism is installed before a listen server has started, so its install-time
        // authority probe can legitimately be false. Rebind repository authority at the actual
        // world lifecycle boundary where ModEvents tells us whether this process is the server.
        RebirthMetabolismStateRepository.Reset(data.AsServer);
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("persistence GameStarting asServer=" + data.AsServer
            + " worldRepoAuthority=" + RebirthWorldCharacterRepository.IsServerAuthority
            + " metabolismRepoAuthority=" + RebirthMetabolismStateRepository.IsServerAuthority
            + " metabolismServiceAuthority=" + RebirthMetabolismService.IsServerAuthority); }
        if (data.AsServer)
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor] world persistence root=" + RebirthWorldCharacterRepository.RootDirectory); }
    }

    private static void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!RebirthWorldCharacterRepository.IsServerAuthority || data.ClientInfo == null)
            return;
        RebirthStablePlayerIdentity.Remember(data.ClientInfo);
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        FlushAndReset("world-shutdown");
    }

    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data)
    {
        FlushAndReset("game-shutdown");
    }

    internal static void FlushAll(string reason)
    {
        if (!RebirthWorldCharacterRepository.IsServerAuthority)
            return;
        int count = RebirthWorldCharacterRepository.SaveAllDirty(reason);
        RebirthMetabolismStateRepository.SaveIfDirty(reason);
        if (count > 0)
            { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor] persistence flush saved=" + count + " reason=" + (reason ?? "unspecified")); }
    }

    internal static void FlushClient(ClientInfo clientInfo, string reason)
    {
        if (!RebirthWorldCharacterRepository.IsServerAuthority || clientInfo == null)
            return;
        RebirthStablePlayerIdentity identity;
        if (RebirthStablePlayerIdentity.TryFromClientInfo(clientInfo, out identity))
        {
            RebirthWorldCharacterRepository.SaveIfDirty(identity, reason);
            RebirthSurvivorCreationTransactions.ForgetPlayer(identity.StorageKey);
        }
        RebirthMetabolismStateRepository.SaveIfDirty(reason);
    }

    private static void FlushAndReset(string reason)
    {
        RebirthLiteratureStudySessionService.CheckpointForShutdown();
        RebirthAudiobookListeningSessionService.CheckpointForShutdown();
        FlushAll(reason);
        RebirthWorldCharacterRepository.Reset(false);
        RebirthMetabolismStateRepository.Reset(false);
        RebirthStablePlayerIdentity.ResetRuntimeCache();
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveWorld), new Type[] { })]
public static class RebirthSurvivorPersistenceSaveWorldPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        RebirthSurvivorPersistenceLifecycle.FlushAll("world-save");
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDisconnected))]
public static class RebirthSurvivorPersistenceDisconnectPatch
{
    [HarmonyPrefix]
    public static void Prefix(ClientInfo _cInfo)
    {
        RebirthSurvivorPersistenceLifecycle.FlushClient(_cInfo, "player-disconnect");
    }
}
