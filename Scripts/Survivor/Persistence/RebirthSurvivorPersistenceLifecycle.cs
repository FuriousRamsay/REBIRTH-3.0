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
    private static readonly object InstallSync=new object();
    private static bool startingRegistered,spawnRegistered,worldShutdownRegistered,gameShutdownRegistered;
    public static bool IsInstalled{get{lock(InstallSync)return installed;}}
    private static readonly Harmony Harmony = new Harmony("rebirth.survivor.persistence.3.0");

    public static string Install()
    {
        lock(InstallSync)
        {
        if (installed)
            return "[REBIRTH Survivor] persistence lifecycle already installed.";

        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorPersistenceSaveWorldPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthSurvivorPersistenceDisconnectPatch));
        RegisterStage(ref startingRegistered,()=>ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting)));
        RegisterStage(ref spawnRegistered,()=>ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawnedInWorld)));
        RegisterStage(ref worldShutdownRegistered,()=>ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown)));
        RegisterStage(ref gameShutdownRegistered,()=>ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown)));
        installed=true;
        return "[REBIRTH Survivor] bounded persistence lifecycle installed.";
        }
    }

    private static void RegisterStage(ref bool complete,Action register)
    {if(complete)return;register();complete=true;}

    private static void OnGameStarting(ref ModEvents.SGameStartingData data)
    {
        RebirthCamperEnergyEfficiency.ResetOwnerBonuses();
        bool worldReady=RebirthWorldCharacterRepository.CanResetAfterFlush();
        bool metabolismReady=RebirthMetabolismStateRepository.CanResetAfterFlush();
        // Each repository owns its own exact-save and pending-custody retirement guard.
        // A retained Metabolism hold must not strand the clean Survivor repository in
        // the previous World, where a returning player would be treated as uncreated.
        bool worldReset=worldReady&&RebirthWorldCharacterRepository.TryResetAfterFlush(data.AsServer);
        bool metabolismReset=metabolismReady&&RebirthMetabolismStateRepository.TryResetAfterFlush(data.AsServer);
        if(worldReset)RebirthStablePlayerIdentity.ResetRuntimeCache();
        if(!worldReset||!metabolismReset)
        {
            Log.Error("[REBIRTH Survivor][Persistence] GameStarting retained repository worldReady="+worldReady+
                " metabolismReady="+metabolismReady+" worldReset="+worldReset+
                " metabolismReset="+metabolismReset+" asServer="+data.AsServer+
                " world={"+RebirthWorldCharacterRepository.ResetDiagnostic()+"}"+
                " metabolism={"+RebirthMetabolismStateRepository.ResetDiagnostic()+"}");
        }
        // Metabolism's install-time authority probe can be false before the listen
        // server starts; its successful reset above binds the actual server authority.
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
        RebirthCamperEnergyEfficiency.PrimeOwner(data.ClientInfo);
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
    {
        RebirthCamperEnergyEfficiency.ResetOwnerBonuses();
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
        bool worldSaved=RebirthWorldCharacterRepository.TryFlush(reason);
        bool metabolismSaved=RebirthMetabolismStateRepository.TryFlush(reason);
        bool worldReady=RebirthWorldCharacterRepository.CanResetAfterFlush();
        bool metabolismReady=RebirthMetabolismStateRepository.CanResetAfterFlush();
        bool worldReset=worldSaved&&worldReady&&RebirthWorldCharacterRepository.TryResetAfterFlush(false);
        bool metabolismReset=metabolismSaved&&metabolismReady&&RebirthMetabolismStateRepository.TryResetAfterFlush(false);
        if(worldReset)RebirthStablePlayerIdentity.ResetRuntimeCache();
        if(!worldReset||!metabolismReset)
        {
            Log.Warning("[REBIRTH Survivor][Persistence] shutdown retained repositories reason="+reason+
                " worldSaved="+worldSaved+" metabolismSaved="+metabolismSaved+
                " worldReady="+worldReady+" metabolismReady="+metabolismReady+
                " worldReset="+worldReset+" metabolismReset="+metabolismReset+
                " world={"+RebirthWorldCharacterRepository.ResetDiagnostic()+"}"+
                " metabolism={"+RebirthMetabolismStateRepository.ResetDiagnostic()+"}");
        }
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
