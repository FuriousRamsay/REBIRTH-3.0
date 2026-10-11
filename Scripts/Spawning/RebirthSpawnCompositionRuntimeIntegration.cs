using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Explicit runtime bridge between the four supported native spawn directors and
/// RebirthSpawnCompositionService. No StackTrace/caller inference is used.
/// </summary>
public static class RebirthSpawnCompositionRuntimeIntegration
{
    private const string HarmonyId = "rebirth.spawncomposition.runtime.3.1";
    private static readonly HarmonyLib.Harmony s_harmony = new HarmonyLib.Harmony(HarmonyId);
    private static bool s_installed;
    private static int s_installedCount;

    [ThreadStatic]
    private static Stack<RebirthSpawnContext> t_contexts;

    public static bool IsInstalled { get { return s_installed; } }
    public static int InstalledPatchCount { get { return s_installedCount; } }

    public static string Install()
    {
        if (s_installed)
            return "[RebirthSpawn] Runtime integration already installed; patches=" + s_installedCount;

        int installed = 0;
        int failures = 0;

        // Install the shared resolver first. Every context depends on it, so a failure in an
        // unrelated director adapter must never leave ambient biome routing silently inert.
        InstallPatch(typeof(RebirthSpawnEntityGroupResolverPatch), ref installed, ref failures);
        InstallPatch(typeof(RebirthSpawnBiomeContextPatch), ref installed, ref failures);
        InstallPatch(typeof(RebirthPurgeBiomeZombieSpawnPatch), ref installed, ref failures);
        InstallPatch(typeof(RebirthSpawnWanderingContextPatch), ref installed, ref failures);
        InstallPatch(typeof(RebirthSpawnBloodMoonContextPatch), ref installed, ref failures);
        InstallPatch(typeof(RebirthSpawnSleeperContextPatch), ref installed, ref failures);
        InstallPatch(typeof(RebirthPoiRiskCharacterStatsBindingPatch), ref installed, ref failures);
        InstallPatch(typeof(RebirthPoiRiskPlayersListDisplayPatch), ref installed, ref failures);

        s_installedCount = installed;
        s_installed = installed > 0;
        string result = "[RebirthSpawn] Runtime integration patches=" + installed + " failures=" + failures;
        if (failures > 0) Log.Warning(result); else { if (RebirthLogSettings.RuntimeInstallLoggingEnabled) Log.Out(result); }
        return result;
    }


    private static void InstallPatch(Type patchType, ref int installed, ref int failures)
    {
        try
        {
            RebirthHarmonyBootstrap.PatchClassOnce(s_harmony, patchType);
            installed++;
        }
        catch (Exception ex)
        {
            failures++;
            Log.Error("[RebirthSpawn] Failed to install " + patchType.Name + ": "
                + ex.GetType().Name + ": " + ex.Message);
        }
    }



    internal static void Push(RebirthSpawnContext context)
    {
        if (context == null) return;
        if (t_contexts == null) t_contexts = new Stack<RebirthSpawnContext>(2);
        t_contexts.Push(context);
    }

    internal static void Pop()
    {
        if (t_contexts != null && t_contexts.Count > 0) t_contexts.Pop();
    }

    internal static RebirthSpawnContext Current
    {
        get { return t_contexts != null && t_contexts.Count > 0 ? t_contexts.Peek() : null; }
    }

    internal static bool IsAuthoritative(World world)
    {
        return world != null && !world.IsRemote();
    }

    internal static RebirthSpawnProgressionMode SelectedProgressionMode()
    {
        return RebirthSandboxOptionManager.Current.EffectiveSpawnProgression;
    }

    internal static string BiomeName(World world, Vector3 position)
    {
        if (world == null) return string.Empty;
        BiomeDefinition biome = world.GetBiome((int)position.x, (int)position.z);
        return biome != null ? biome.m_sBiomeName : string.Empty;
    }

    internal static string BiomeName(World world, Vector3i position)
    {
        if (world == null) return string.Empty;
        BiomeDefinition biome = world.GetBiome(position.x, position.z);
        return biome != null ? biome.m_sBiomeName : string.Empty;
    }

    internal static string BiomeName(World world, int biomeId)
    {
        if (world == null || world.Biomes == null) return string.Empty;
        BiomeDefinition biome = world.Biomes.GetBiome((byte)biomeId);
        return biome != null ? biome.m_sBiomeName : string.Empty;
    }

    /// <summary>
    /// Calculates ambient progression from the same local player population that
    /// makes the native biome spawn area eligible. This deliberately avoids a
    /// world-wide highest gamestage and keeps separate player regions independent.
    /// </summary>
    internal static int AmbientGameStage(World world, UnityEngine.Rect spawnArea)
    {
        if (world == null || RebirthSandboxOptionManager.Current.IsPurge) return 0;

        List<EntityPlayer> players = world.GetPlayers();
        if (players == null || players.Count == 0) return 0;

        List<int> localGameStages = new List<int>();
        for (int i = 0; i < players.Count; i++)
        {
            EntityPlayer player = players[i];
            if (player == null || !player.Spawned) continue;

            UnityEngine.Rect playerSpawnVicinity = new UnityEngine.Rect(
                player.position.x - 40f, player.position.z - 40f, 80f, 80f);
            if (playerSpawnVicinity.Overlaps(spawnArea))
                localGameStages.Add(Math.Max(0, player.gameStage));
        }

        return localGameStages.Count > 0
            ? Math.Max(0, GameStageDefinition.CalcPartyLevel(localGameStages))
            : 0;
    }
}

[HarmonyPatch(typeof(SpawnManagerBiomes), "SpawnUpdate", new[] { typeof(string), typeof(bool), typeof(ChunkAreaBiomeSpawnData) })]
public static class RebirthSpawnBiomeContextPatch
{
    public static void Prefix(SpawnManagerBiomes __instance, bool _isSpawnEnemy, ChunkAreaBiomeSpawnData _spawnData, ref bool __state)
    {
        __state = false;
        if (!_isSpawnEnemy || _spawnData == null || __instance == null || !RebirthSpawnCompositionRuntimeIntegration.IsAuthoritative(__instance.world)) return;
        RebirthSpawnCompositionRuntimeIntegration.Push(new RebirthSpawnContext
        {
            Surface = RebirthSpawnSurface.Biome,
            ProgressionMode = RebirthSpawnCompositionRuntimeIntegration.SelectedProgressionMode(),
            GameStage = RebirthSpawnCompositionRuntimeIntegration.AmbientGameStage(__instance.world, _spawnData.area),
            Biome = RebirthSpawnCompositionRuntimeIntegration.BiomeName(__instance.world, _spawnData.biomeId),
            HistoryKey = "biome:" + _spawnData.biomeId
        });
        __state = true;
    }

    public static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) RebirthSpawnCompositionRuntimeIntegration.Pop();
        return __exception;
    }
}

[HarmonyPatch(typeof(AIWanderingHordeSpawner), nameof(AIWanderingHordeSpawner.UpdateSpawn))]
public static class RebirthSpawnWanderingContextPatch
{
    public static void Prefix(AIWanderingHordeSpawner __instance, World _world, ref bool __state)
    {
        __state = false;
        if (__instance == null || __instance.spawnType == AIWanderingHordeSpawner.SpawnType.Bandits || !RebirthSpawnCompositionRuntimeIntegration.IsAuthoritative(_world)) return;
        int gs = __instance.spawner != null ? Math.Max(0, __instance.spawner.partyLevel) : 0;
        RebirthSpawnCompositionRuntimeIntegration.Push(new RebirthSpawnContext
        {
            Surface = RebirthSpawnSurface.WanderingHorde,
            ProgressionMode = RebirthSpawnCompositionRuntimeIntegration.SelectedProgressionMode(),
            GameStage = gs,
            NativeStage = gs,
            Biome = RebirthSpawnCompositionRuntimeIntegration.BiomeName(_world, __instance.startPos),
            RequestedGroup = __instance.spawner != null ? __instance.spawner.spawnGroupName : string.Empty,
            HistoryKey = "wandering"
        });
        __state = true;
    }

    public static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) RebirthSpawnCompositionRuntimeIntegration.Pop();
        return __exception;
    }
}

[HarmonyPatch(typeof(AIDirectorBloodMoonParty), nameof(AIDirectorBloodMoonParty.SpawnZombie))]
public static class RebirthSpawnBloodMoonContextPatch
{
    public static void Prefix(AIDirectorBloodMoonParty __instance, World _world, EntityPlayer _target, ref bool __state)
    {
        __state = false;
        if (__instance == null || !RebirthSpawnCompositionRuntimeIntegration.IsAuthoritative(_world)) return;
        int gs = __instance.partySpawner != null ? Math.Max(0, __instance.partySpawner.partyLevel) : 0;
        RebirthSpawnCompositionRuntimeIntegration.Push(new RebirthSpawnContext
        {
            Surface = RebirthSpawnSurface.BloodMoon,
            ProgressionMode = RebirthSpawnCompositionRuntimeIntegration.SelectedProgressionMode(),
            GameStage = gs,
            NativeStage = gs,
            Biome = RebirthSpawnCompositionRuntimeIntegration.BiomeName(_world, _target != null ? _target.position : Vector3.zero),
            RequestedGroup = __instance.partySpawner != null ? __instance.partySpawner.spawnGroupName : string.Empty,
            HistoryKey = "bloodmoon:" + (_target != null ? _target.entityId.ToString() : "party")
        });
        __state = true;
    }

    public static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) RebirthSpawnCompositionRuntimeIntegration.Pop();
        return __exception;
    }
}

public struct RebirthSleeperSpawnPatchState
{
    public bool Active;
    public int OriginalGameStage;
}

[HarmonyPatch(typeof(SleeperVolume), nameof(SleeperVolume.UpdateSpawn))]
public static class RebirthSpawnSleeperContextPatch
{
    public static void Prefix(SleeperVolume __instance, World _world, ref RebirthSleeperSpawnPatchState __state)
    {
        __state = default(RebirthSleeperSpawnPatchState);
        if (__instance == null || !RebirthSpawnCompositionRuntimeIntegration.IsAuthoritative(_world)) return;

        string prefab = __instance.prefabInstance != null ? __instance.prefabInstance.name : string.Empty;
        string biome = RebirthSpawnCompositionRuntimeIntegration.BiomeName(_world, __instance.BoxMin);
        RebirthSpawnProgressionMode progressionMode = RebirthSpawnCompositionRuntimeIntegration.SelectedProgressionMode();
        int nativeGameStage = Math.Max(0, __instance.gameStage);
        int poiTier = RebirthPoiRiskRuntimePolicy.GetPoiTier(__instance);
        int effectiveGameStage = progressionMode == RebirthSpawnProgressionMode.Gamestage
            ? RebirthPoiRiskRuntimePolicy.CalculateEffectiveSleeperGameStage(nativeGameStage, poiTier)
            : nativeGameStage;

        __state.Active = true;
        __state.OriginalGameStage = __instance.gameStage;
        __instance.gameStage = effectiveGameStage;

        RebirthSpawnCompositionRuntimeIntegration.Push(new RebirthSpawnContext
        {
            Surface = RebirthSpawnSurface.Sleeper,
            ProgressionMode = progressionMode,
            GameStage = effectiveGameStage,
            NativeStage = effectiveGameStage,
            Biome = biome,
            PrefabName = prefab,
            HistoryKey = "sleeper:" + prefab + ":" + __instance.BoxMin.ToString()
        });
    }

    public static Exception Finalizer(SleeperVolume __instance, Exception __exception, RebirthSleeperSpawnPatchState __state)
    {
        if (__state.Active)
        {
            if (__instance != null) __instance.gameStage = __state.OriginalGameStage;
            RebirthSpawnCompositionRuntimeIntegration.Pop();
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(EntityGroups), nameof(EntityGroups.GetRandomFromGroup))]
public static class RebirthSpawnEntityGroupResolverPatch
{
    public static bool Prefix(string _sEntityGroupName, ref int lastClassId, GameRandom random, ref int __result)
    {
        RebirthSpawnContext active = RebirthSpawnCompositionRuntimeIntegration.Current;
        if (active == null || active.DeferCompositionUntilPosition) return true;

        try
        {
            // Hostile wildlife also sets IsEnemyEntity. Preserve the requested animal pool
            // (including its native none probabilities) before selecting humanoid composition.
            if (EntityGroups.list.TryGetValue(_sEntityGroupName, out var requestedMembers)
                && requestedMembers != null && requestedMembers.Count > 0
                && EntityClass.GetEntityClass(requestedMembers[0].entityClassId)?.bIsAnimalEntity == true)
                return true;
            if (!EntityGroups.IsEnemyGroup(_sEntityGroupName)) return true;
        }
        catch
        {
            return true;
        }

        active.RequestedGroup = _sEntityGroupName ?? string.Empty;
        if (active.Surface == RebirthSpawnSurface.Sleeper
            && RebirthSpawnCompositionService.GetSleeperGroupPolicy(active.RequestedGroup) == RebirthSleeperGroupPolicy.VanillaPreserved)
            return true;

        GameRandom authoritativeRandom = random;
        if (authoritativeRandom == null && GameManager.Instance != null && GameManager.Instance.World != null)
            authoritativeRandom = GameManager.Instance.World.GetGameRandom();
        if (authoritativeRandom == null) return true;

        RebirthSpawnTrace trace;
        if (!RebirthSpawnCompositionService.TrySelect(active, delegate { return (double)authoritativeRandom.RandomFloat; }, out trace)
            || trace == null)
            return true;

        // 3.1 entity IDs are signed string hashes. Validate against the loaded registry
        // instead of rejecting every valid negative hash.
        EntityClass selectedClass = EntityClass.GetEntityClass(trace.EntityClassId);
        if (selectedClass == null)
            return true;

        __result = trace.EntityClassId;
        lastClassId = trace.EntityClassId;
        return false;
    }

    public static void Postfix(string _sEntityGroupName, ref int lastClassId, GameRandom random, ref int __result)
    {
        RebirthSpawnContext active = RebirthSpawnCompositionRuntimeIntegration.Current;
        if (active == null || active.DeferCompositionUntilPosition ||
            !RebirthSpawnCompositionService.IsRestrictedNoSpecialSurface(active.Surface))
            return;

        EntityClass selectedClass = EntityClass.GetEntityClass(__result);
        if (!RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(selectedClass))
            return;

        GameRandom authoritativeRandom = random;
        if (authoritativeRandom == null && GameManager.Instance != null && GameManager.Instance.World != null)
            authoritativeRandom = GameManager.Instance.World.GetGameRandom();

        RebirthSpawnTrace replacementTrace;
        if (authoritativeRandom != null)
        {
            RebirthSpawnContext replacementContext = new RebirthSpawnContext
            {
                Surface = active.Surface,
                ProgressionMode = active.ProgressionMode,
                GameStage = active.GameStage,
                Biome = active.Biome,
                RequestedGroup = string.Empty,
                PrefabName = active.PrefabName,
                HistoryKey = active.HistoryKey
            };

            if (RebirthSpawnCompositionService.TrySelect(
                    replacementContext,
                    delegate { return (double)authoritativeRandom.RandomFloat; },
                    out replacementTrace) &&
                replacementTrace != null &&
                !RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(replacementTrace.EntityClassId))
            {
                __result = replacementTrace.EntityClassId;
                lastClassId = replacementTrace.EntityClassId;
                return;
            }
        }

        // Absolute fallback: never return a screamer, crawler or demolition zombie
        // into Sleeper/Wandering/Event surfaces even when a vanilla-preserved group
        // contains only a forbidden entity or the managed replacement pool is unavailable.
        int fallbackId = EntityClass.FromString("zombieArlene");
        EntityClass fallbackClass = EntityClass.GetEntityClass(fallbackId);
        if (fallbackClass != null &&
            !RebirthSpawnCompositionService.IsForbiddenRestrictedSpawnEntity(fallbackClass))
        {
            __result = fallbackId;
            lastClassId = fallbackId;
        }
    }
}

[HarmonyPatch(typeof(XUiC_CharacterFrameWindow), nameof(XUiC_CharacterFrameWindow.GetBindingValueInternal))]
public static class RebirthPoiRiskCharacterStatsBindingPatch
{
    public static void Postfix(XUiC_CharacterFrameWindow __instance, ref string _value, string _bindingName, ref bool __result)
    {
        if (__instance == null || __instance.xui == null || __instance.xui.playerUI == null) return;
        EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
        if (player == null) return;

        if (_bindingName == "playergamestage")
        {
            _value = RebirthPoiRiskRuntimePolicy.GetDisplayedGameStage(player).ToString();
            __result = true;
            return;
        }

        if (_bindingName == "playergamestagetitle")
        {
            RebirthPoiRiskDisplaySnapshot snapshot;
            if (RebirthPoiRiskRuntimePolicy.TryGetDisplaySnapshot(player, out snapshot) && snapshot.Applied)
            {
                _value = Localization.Get("xuiRebirthPoiGameStage");
                __result = true;
            }
        }
    }
}

[HarmonyPatch(typeof(XUiC_PlayersList), "updatePlayersList")]
public static class RebirthPoiRiskPlayersListDisplayPatch
{
    public static void Postfix(XUiC_PlayersList __instance)
    {
        if (__instance == null || __instance.xui == null || __instance.xui.playerUI == null) return;
        EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
        if (player == null) return;

        XUiC_PlayersListEntry[] entries = __instance.GetChildrenByType<XUiC_PlayersListEntry>();
        if (entries == null) return;
        string displayed = RebirthPoiRiskRuntimePolicy.GetDisplayedGameStage(player).ToString();
        for (int i = 0; i < entries.Length; i++)
        {
            XUiC_PlayersListEntry entry = entries[i];
            if (entry != null && entry.IsLocalPlayer && entry.GamestageText != null)
            {
                entry.GamestageText.Text = displayed;
                break;
            }
        }
    }
}

/// <summary>
/// Dedicated bootstrap for spawn composition. This keeps the feature independent from
/// unrelated REBIRTH bootstrap work; duplicate calls are safely ignored by Install().
/// </summary>
public sealed class RebirthSpawnCompositionModApi : IModApi
{
    public void InitMod(Mod modInstance)
    {
        RebirthSpawnCompositionService.Initialize();
        RebirthSpawnCompositionRuntimeIntegration.Install();
    }
}
