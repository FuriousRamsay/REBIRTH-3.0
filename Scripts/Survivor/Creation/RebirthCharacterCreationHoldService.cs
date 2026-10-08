using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Server-owned gameplay hold for Rebirth characters that do not yet have a committed
/// world origin. The persistent repository is the truth; this runtime table stores only
/// the spawn anchor and diagnostics. A client cannot clear the hold.
/// </summary>
public static class RebirthCharacterCreationHoldService
{
    private sealed class HoldEntry
    {
        public string StableKey = string.Empty;
        public int EntityId;
        public Vector3 Anchor;
        public RespawnType EntryReason;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<int, HoldEntry> Entries = new Dictionary<int, HoldEntry>();
    private static readonly HashSet<int> ReadyEntities = new HashSet<int>();
    private static readonly Harmony Harmony = new Harmony("rebirth.survivor.creation-hold.3.1");
    private static bool installed;
    private static float nextAnchorEnforcementTime;

    public static string Install()
    {
        if (installed) return "[REBIRTH Survivor] character creation hold already installed.";
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldMovePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldNetworkMovePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldUseItemPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldPlayerDamagePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldLocalDamagePatch));
        var blockActivationTarget = AccessTools.DeclaredMethod(typeof(Block), nameof(Block.OnBlockActivated), new Type[]
        {
            typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal)
        });
        if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Survivor][RuntimeInstall][HoldTarget] Block.OnBlockActivated(string,WorldBase,Vector3i,BlockValue,EntityPlayerLocal) found=" + (blockActivationTarget != null));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldBlockActivationPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldAttackTargetPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldNativeXpPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(Harmony, typeof(RebirthCharacterCreationHoldDisconnectPatch));
        ModEvents.GameStarting.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameStartingData>(OnGameStarting));
        ModEvents.PlayerSpawnedInWorld.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SPlayerSpawnedInWorldData>(OnPlayerSpawnedInWorld));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        installed = true;
        return "[REBIRTH Survivor] server-owned character creation hold installed.";
    }

    public static bool IsHeld(EntityPlayer player)
    {
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;

        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool server = connection != null && connection.IsServer;
        if (server && RebirthWorldCharacterRepository.IsServerAuthority)
        {
            lock (Sync)
            {
                if (Entries.ContainsKey(player.entityId)) return true;
                if (ReadyEntities.Contains(player.entityId)) return false;
            }

            // Lifecycle handlers normally populate one of the two runtime sets. If an engine
            // path reaches us first, reconcile once against authoritative storage, cache the
            // result, and then stay allocation/disk-free on subsequent hot-path checks.
            RebirthStablePlayerIdentity identity;
            if (!RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity))
            {
                { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("creation hold fallback identity unresolved entity=" + player.entityId + " -> HELD"); }
                return true;
            }
            RebirthWorldCharacterRecord record;
            bool ready = RebirthWorldCharacterRepository.TryGet(identity, out record) && record != null && record.IsComplete;
            lock (Sync)
            {
                if (ready) ReadyEntities.Add(player.entityId);
                else Entries[player.entityId] = new HoldEntry
                {
                    StableKey = identity.StorageKey,
                    EntityId = player.entityId,
                    Anchor = player.position,
                    EntryReason = RespawnType.Unknown
                };
            }
            { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("creation hold fallback reconciled entity=" + player.entityId
                + " ready=" + ready + " key=" + identity.StorageKey + " -> " + (ready ? "RELEASED" : "HELD")); }
            return !ready;
        }

        EntityPlayerLocal local = player as EntityPlayerLocal;
        if (local == null) return false;
        bool rebirthMode;
        RebirthSurvivorOwnerCreationState creationState;
        if (!RebirthSurvivorClientState.TryGetOwnerCreationState(out rebirthMode, out creationState)) return true;
        return rebirthMode && creationState != RebirthSurvivorOwnerCreationState.Ready;
    }

    public static bool IsHeldEntityId(int entityId)
    {
        if (entityId <= 0 || GameManager.Instance == null || GameManager.Instance.World == null) return false;
        return IsHeld(GameManager.Instance.World.GetEntity(entityId) as EntityPlayer);
    }

    public static void OnCharacterCommitted(EntityPlayer player, RebirthStablePlayerIdentity identity)
    {
        if (player == null) return;
        lock (Sync)
        {
            Entries.Remove(player.entityId);
            ReadyEntities.Add(player.entityId);
        }
        EntityPlayerLocal local = player as EntityPlayerLocal;
        if (local != null)
        {
            local.ClearMovementInputs();
            local.SetControllable(true);
        }
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor] creation hold RELEASE entity=" + player.entityId + " key=" + (identity != null ? identity.StorageKey : string.Empty)); }
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("creation hold RELEASE entity=" + player.entityId
            + " local=" + (player is EntityPlayerLocal) + " key=" + (identity != null ? identity.StorageKey : string.Empty)); }
    }

    public static bool TryGetAnchor(int entityId, out Vector3 anchor)
    {
        lock (Sync)
        {
            HoldEntry entry;
            if (Entries.TryGetValue(entityId, out entry) && entry != null)
            {
                anchor = entry.Anchor;
                return true;
            }
        }
        anchor = Vector3.zero;
        return false;
    }

    private static void OnGameStarting(ref ModEvents.SGameStartingData data) { Reset(); }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { Reset(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { Reset(); }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || GameManager.Instance == null || GameManager.Instance.World == null) return;
        float now = Time.realtimeSinceStartup;
        if (now < nextAnchorEnforcementTime) return;
        nextAnchorEnforcementTime = now + 0.25f;

        HoldEntry[] held;
        lock (Sync)
        {
            if (Entries.Count == 0) return;
            held = new HoldEntry[Entries.Count];
            Entries.Values.CopyTo(held, 0);
        }
        World world = GameManager.Instance.World;
        for (int i = 0; i < held.Length; i++)
        {
            HoldEntry entry = held[i];
            if (entry == null) continue;
            EntityPlayer player = world.GetEntity(entry.EntityId) as EntityPlayer;
            if (player == null) continue;
            if ((player.position - entry.Anchor).sqrMagnitude > 0.0025f) player.SetPosition(entry.Anchor);
        }
    }

    private static void OnPlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)
    {
        if (!RebirthWorldCharacterRepository.IsServerAuthority || !RebirthSurvivorMode.IsEnabledForCurrentWorld())
            return;
        EntityPlayer player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetEntity(data.EntityId) as EntityPlayer : null;
        if (player == null) return;

        RebirthStablePlayerIdentity identity;
        if (data.ClientInfo != null)
        {
            RebirthStablePlayerIdentity.Remember(data.ClientInfo);
            if (!RebirthStablePlayerIdentity.TryFromClientInfo(data.ClientInfo, out identity))
            {
                Log.Warning("[REBIRTH Survivor][PreSpawnTrace] creation hold spawn identity unavailable from ClientInfo entity=" + data.EntityId);
                Enter(player, null, data.RespawnType);
                return;
            }
        }
        else if (data.IsLocalPlayer && RebirthStablePlayerIdentity.TryResolveServerEntity(player, out identity))
        {
            { if (RebirthLogSettings.PreSpawnLoggingEnabled) RebirthLogSettings.TracePreSpawn("creation hold resolved listen-server local identity without ClientInfo entity=" + data.EntityId); }
        }
        else
        {
            Log.Warning("[REBIRTH Survivor][PreSpawnTrace] creation hold spawn had no resolvable identity entity=" + data.EntityId
                + " local=" + data.IsLocalPlayer + " clientInfo=False");
            Enter(player, null, data.RespawnType);
            return;
        }
        RebirthWorldCharacterRecord record;
        if (RebirthWorldCharacterRepository.TryGet(identity, out record) && record != null && record.IsComplete)
        {
            lock (Sync)
            {
                Entries.Remove(player.entityId);
                ReadyEntities.Add(player.entityId);
            }
            return;
        }
        Enter(player, identity, data.RespawnType);
    }

    private static void Enter(EntityPlayer player, RebirthStablePlayerIdentity identity, RespawnType reason)
    {
        HoldEntry entry = new HoldEntry
        {
            StableKey = identity != null ? identity.StorageKey : string.Empty,
            EntityId = player.entityId,
            Anchor = player.position,
            EntryReason = reason
        };
        lock (Sync)
        {
            ReadyEntities.Remove(player.entityId);
            Entries[player.entityId] = entry;
        }
        EntityPlayerLocal local = player as EntityPlayerLocal;
        if (local != null)
        {
            local.ClearMovementInputs();
            local.SetControllable(false);
        }
        { if (RebirthLogSettings.AutomaticLoggingDefault) Log.Out("[REBIRTH Survivor] creation hold ENTER entity=" + player.entityId + " reason=" + reason + " key=" + entry.StableKey); }
        { if (RebirthLogSettings.SpawnFlowLoggingEnabled) RebirthLogSettings.TraceSpawnFlow("creation hold ENTER entity=" + player.entityId
            + " local=" + (player is EntityPlayerLocal) + " controllableForcedFalse=" + (local != null)
            + " reason=" + reason + " key=" + entry.StableKey); }
    }

    internal static void OnDisconnected(ClientInfo info)
    {
        if (info == null) return;
        lock (Sync)
        {
            Entries.Remove(info.entityId);
            ReadyEntities.Remove(info.entityId);
        }
    }

    private static void Reset()
    {
        lock (Sync)
        {
            Entries.Clear();
            ReadyEntities.Clear();
            nextAnchorEnforcementTime = 0f;
        }
    }
}

[HarmonyPatch(typeof(PlayerMoveController), nameof(PlayerMoveController.Update))]
public static class RebirthCharacterCreationHoldMovePatch
{
    [HarmonyPrefix]
    public static bool Prefix(PlayerMoveController __instance)
    {
        EntityPlayerLocal player = __instance != null ? __instance.entityPlayerLocal : null;
        if (player == null || !RebirthCharacterCreationHoldService.IsHeld(player)) return true;
        player.ClearMovementInputs();
        player.SetControllable(false);
        return false;
    }
}

[HarmonyPatch(typeof(NetPackageEntityPosAndRot), nameof(NetPackageEntityPosAndRot.ProcessPackage), new Type[] { typeof(World), typeof(GameManager) })]
public static class RebirthCharacterCreationHoldNetworkMovePatch
{
    [HarmonyPrefix]
    public static bool Prefix(NetPackageEntityPosAndRot __instance, World _world)
    {
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (connection == null || !connection.IsServer || _world == null || __instance == null) return true;
        EntityPlayer player = _world.GetEntity(__instance.entityId) as EntityPlayer;
        return player == null || !RebirthCharacterCreationHoldService.IsHeld(player);
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.UseHoldingItem), new Type[] { typeof(int), typeof(bool) })]
public static class RebirthCharacterCreationHoldUseItemPatch
{
    [HarmonyPrefix]
    public static bool Prefix(EntityAlive __instance, ref bool __result)
    {
        EntityPlayer player = __instance as EntityPlayer;
        if (player == null || !RebirthCharacterCreationHoldService.IsHeld(player)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.DamageEntity), new Type[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) })]
public static class RebirthCharacterCreationHoldPlayerDamagePatch
{
    [HarmonyPrefix]
    public static bool Prefix(EntityPlayer __instance, ref int __result)
    {
        if (__instance == null || !RebirthCharacterCreationHoldService.IsHeld(__instance)) return true;
        __result = 0;
        return false;
    }
}

[HarmonyPatch(typeof(EntityPlayerLocal), nameof(EntityPlayerLocal.DamageEntity), new Type[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) })]
public static class RebirthCharacterCreationHoldLocalDamagePatch
{
    [HarmonyPrefix]
    public static bool Prefix(EntityPlayerLocal __instance, ref int __result)
    {
        if (__instance == null || !RebirthCharacterCreationHoldService.IsHeld(__instance)) return true;
        __result = 0;
        return false;
    }
}

[HarmonyPatch(typeof(Block), nameof(Block.OnBlockActivated), new Type[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
public static class RebirthCharacterCreationHoldBlockActivationPatch
{
    [HarmonyPrefix]
    public static bool Prefix(EntityPlayerLocal __4, ref bool __result)
    {
        EntityPlayerLocal player = __4;
        if (player == null || !RebirthCharacterCreationHoldService.IsHeld(player)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.SetAttackTarget), new Type[] { typeof(EntityAlive), typeof(int) })]
public static class RebirthCharacterCreationHoldAttackTargetPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref EntityAlive _attackTarget)
    {
        EntityPlayer player = _attackTarget as EntityPlayer;
        if (player != null && RebirthCharacterCreationHoldService.IsHeld(player)) _attackTarget = null;
    }
}

[HarmonyPatch]
public static class RebirthCharacterCreationHoldNativeXpPatch
{
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        List<MethodBase> targets = new List<MethodBase>();
        MethodInfo[] methods;
        try
        {
            methods = typeof(Progression).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        catch (Exception ex)
        {
            Log.Error("[REBIRTH Survivor][RuntimeInstall][HoldTarget] Progression.AddLevelExp scan failed: " + ex.GetType().Name + ": " + ex.Message);
            return targets;
        }

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method == null || !string.Equals(method.Name, "AddLevelExp", StringComparison.Ordinal)) continue;
            targets.Add(method);
            ParameterInfo[] parameters = method.GetParameters();
            string signature = string.Empty;
            for (int p = 0; p < parameters.Length; p++)
            {
                if (p > 0) signature += ",";
                signature += parameters[p].ParameterType != null ? parameters[p].ParameterType.Name : "?";
            }
            if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out("[REBIRTH Survivor][RuntimeInstall][HoldTarget] Progression.AddLevelExp(" + signature + ") found=True");
        }

        if (targets.Count == 0)
            Log.Error("[REBIRTH Survivor][RuntimeInstall][HoldTarget] Progression.AddLevelExp overloads found=0");
        return targets;
    }

    [HarmonyPrefix]
    public static bool Prefix(Progression __instance)
    {
        EntityPlayer player = __instance != null ? __instance.parent as EntityPlayer : null;
        return player == null || !RebirthCharacterCreationHoldService.IsHeld(player);
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDisconnected), new Type[] { typeof(ClientInfo) })]
public static class RebirthCharacterCreationHoldDisconnectPatch
{
    [HarmonyPrefix]
    public static void Prefix(ClientInfo _cInfo) { RebirthCharacterCreationHoldService.OnDisconnected(_cInfo); }
}
