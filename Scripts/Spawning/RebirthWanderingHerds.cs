using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

#nullable disable

// 2.6 wandering horde and outbreak settings on the native director. An outbreak ("herd" in 2.6 code)
// keeps arriving for a few minutes, stops for horde night or scout hordes, and marks its zombies for loot.
internal static class RebirthWanderingHerds
{
    internal const string HerdVar = "$herdEntity";
    private const int FallbackMaxAlive = 30;
    private const int MaxFrequencyMultiplier = 3;
    private const int MaxSizeMultiplier = 5;

    internal sealed class SpawnerState
    {
        internal int GameStage;
        internal bool Herd;
        internal float Until;
    }

    private static readonly ConditionalWeakTable<AIWanderingHordeSpawner, SpawnerState> States =
        new ConditionalWeakTable<AIWanderingHordeSpawner, SpawnerState>();
    private static bool forceNextHerd;
    internal static int SizeMultiplier { get; private set; } = 1;

    internal static bool TryGet(AIWanderingHordeSpawner spawner, out SpawnerState state)
        => States.TryGetValue(spawner, out state);

    internal static void ForceNextHerd() { forceNextHerd = true; }

    internal static void Register(World world, AIWanderingHordeSpawner spawner)
    {
        var options = RebirthSandboxOptionManager.Current;
        bool herd = forceNextHerd || UnityEngine.Random.Range(0, 100) < options.WanderingHerdChance;
        forceNextHerd = false;
        // Native wraps the horde's party level at 50, so spawn rules read the player's real stage.
        var player = world.GetClosestPlayer(spawner.startPos, -1f, false);
        int stage = player != null ? RebirthEventSpawnPolicy.ApplyGamestage(player.gameStage) : Math.Max(0, spawner.spawner.partyLevel);
        if (herd) stage = Math.Min(stage, StageCap(options.WanderingHerdRestriction));
        float seconds = options.WanderingHerdDuration * 60f + UnityEngine.Random.Range(-30f, 30f);
        States.Remove(spawner);
        States.Add(spawner, new SpawnerState { GameStage = stage, Herd = herd, Until = Time.time + Math.Max(30f, seconds) });
    }

    private static int StageCap(RebirthWanderingHerdRestriction restriction)
    {
        // 2.6 thresholds: Feral 100, Radiated 220, Tainted 340.
        switch (restriction)
        {
            case RebirthWanderingHerdRestriction.Low: return 100;
            case RebirthWanderingHerdRestriction.Normal: return 220;
            case RebirthWanderingHerdRestriction.High: return 340;
            default: return int.MaxValue;
        }
    }

    internal static bool ShouldStop(World world, AIDirector director)
        => world?.aiDirector?.BloodMoonComponent?.BloodMoonActive == true || SkyManager.IsBloodMoonVisible()
            || director?.GetComponent<AIDirectorChunkEventComponent>()?.HasAnySpawns == true;

    internal static int ScaledMaxAlive(int maxAlive)
        => Math.Max(1, (maxAlive > 0 ? maxAlive : FallbackMaxAlive) * SizeMultiplier);

    private static int Gradual(int day, int divisor, int maximum) => Math.Min(maximum, 1 + day / divisor);

    internal static int RollFrequencyMultiplier(int day)
    {
        switch (RebirthSandboxOptionManager.Current.WanderingHordeFrequency)
        {
            case RebirthWanderingHordeFrequency.High: return 2;
            case RebirthWanderingHordeFrequency.Higher: return 3;
            case RebirthWanderingHordeFrequency.Highest: return 4;
            case RebirthWanderingHordeFrequency.GradualSlower: return Gradual(day, 28, MaxFrequencyMultiplier);
            case RebirthWanderingHordeFrequency.Gradual: return Gradual(day, 14, MaxFrequencyMultiplier);
            case RebirthWanderingHordeFrequency.GradualFaster: return Gradual(day, 7, MaxFrequencyMultiplier);
            case RebirthWanderingHordeFrequency.Random: return UnityEngine.Random.Range(1, MaxFrequencyMultiplier + 1);
            default: return 1;
        }
    }

    internal static void RollSizeMultiplier(int day)
    {
        var size = RebirthSandboxOptionManager.Current.WanderingHordeSize;
        switch (size)
        {
            case RebirthWanderingHordeSize.GradualSlower: SizeMultiplier = Gradual(day, 28, MaxSizeMultiplier); break;
            case RebirthWanderingHordeSize.Gradual: SizeMultiplier = Gradual(day, 14, MaxSizeMultiplier); break;
            case RebirthWanderingHordeSize.GradualFaster: SizeMultiplier = Gradual(day, 7, MaxSizeMultiplier); break;
            case RebirthWanderingHordeSize.Random: SizeMultiplier = UnityEngine.Random.Range(2, MaxSizeMultiplier + 1); break;
            default: SizeMultiplier = (int)size + 1; break;
        }
    }
}

// Applies wherever zombies are chosen. Boss events pass no active surface and stay excluded.
internal static class RebirthDemolisherPolicy
{
    private const float ReducedKeepChance = 0.25f;

    internal static bool IsDemolisher(string name)
        => name != null && (name.IndexOf("demolition", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("demolisher", StringComparison.OrdinalIgnoreCase) >= 0);

    internal static bool IsReduced
    {
        get
        {
            var mode = RebirthSandboxOptionManager.Current.DemolisherMode;
            return mode == RebirthDemolisherMode.Reduced || mode == RebirthDemolisherMode.ReducedNotInPois;
        }
    }

    internal static double WeightScale => IsReduced ? ReducedKeepChance : 1.0;

    internal static bool Allows(RebirthSpawnSurface surface)
    {
        var mode = RebirthSandboxOptionManager.Current.DemolisherMode;
        if (mode == RebirthDemolisherMode.Never || surface == RebirthSpawnSurface.EventSpawn) return false;
        if (surface == RebirthSpawnSurface.Sleeper && (mode == RebirthDemolisherMode.NotInPois || mode == RebirthDemolisherMode.ReducedNotInPois)) return false;
        if (mode == RebirthDemolisherMode.NotHordeNight && (surface == RebirthSpawnSurface.BloodMoon
            || GameManager.Instance?.World?.aiDirector?.BloodMoonComponent?.BloodMoonActive == true)) return false;
        return true;
    }

    internal static bool RejectsNativePick(RebirthSpawnSurface surface)
        => !Allows(surface) || (IsReduced && UnityEngine.Random.value >= ReducedKeepChance);
}

[HarmonyPatch(typeof(AIDirectorWanderingHordeComponent), nameof(AIDirectorWanderingHordeComponent.StartSpawning))]
internal static class RebirthWanderingHordeStart
{
    private static bool Prefix(AIDirectorWanderingHordeComponent __instance, AIWanderingHordeSpawner.SpawnType _spawnType)
    {
        if (_spawnType != AIWanderingHordeSpawner.SpawnType.Horde
            || RebirthSandboxOptionManager.Current.WanderingHordeFrequency != RebirthWanderingHordeFrequency.Off) return true;
        __instance.CleanupType(_spawnType);
        __instance.ChooseNextTime(_spawnType);
        return false;
    }

    private static void Postfix(AIDirectorWanderingHordeComponent __instance, AIWanderingHordeSpawner.SpawnType _spawnType)
    {
        if (_spawnType != AIWanderingHordeSpawner.SpawnType.Horde || __instance.spawners.Count == 0) return;
        var newest = __instance.spawners[__instance.spawners.Count - 1];
        if (newest.spawnType != AIWanderingHordeSpawner.SpawnType.Horde || RebirthWanderingHerds.TryGet(newest, out _)) return;
        RebirthWanderingHerds.Register(__instance.Director.World, newest);
    }
}

[HarmonyPatch(typeof(AIDirectorWanderingHordeComponent), "ChooseNextTime")]
internal static class RebirthWanderingHordeFrequencyPatch
{
    private static void Postfix(AIDirectorWanderingHordeComponent __instance, AIWanderingHordeSpawner.SpawnType _spawnType)
    {
        if (_spawnType != AIWanderingHordeSpawner.SpawnType.Horde) return;
        var world = __instance.Director.World;
        int day = GameUtils.WorldTimeToDays(world.worldTime);
        RebirthWanderingHerds.RollSizeMultiplier(day);
        if (RebirthSandboxOptionManager.Current.WanderingHordeFrequency == RebirthWanderingHordeFrequency.Off) return;
        int multiplier = RebirthWanderingHerds.RollFrequencyMultiplier(day);
        __instance.HordeNextTime = world.worldTime + (ulong)UnityEngine.Random.Range(12000 / multiplier, 24000 / multiplier);
    }
}

[HarmonyPatch(typeof(AIWanderingHordeSpawner), nameof(AIWanderingHordeSpawner.UpdateSpawn))]
internal static class RebirthWanderingHerdSpawn
{
    private static void Prefix(AIWanderingHordeSpawner __instance, ref int __state)
    {
        __state = __instance.commandList.Count;
        if (!RebirthWanderingHerds.TryGet(__instance, out var herd) || !herd.Herd) return;
        // An outbreak keeps arriving, but only as fast as the native group's alive limit allows.
        if (__instance.commandList.Count >= RebirthWanderingHerds.ScaledMaxAlive(__instance.spawner.maxAlive))
            __instance.spawnDelay = 1f;
    }

    private static void Postfix(AIWanderingHordeSpawner __instance, World _world, ref bool __result, int __state)
    {
        if (!RebirthWanderingHerds.TryGet(__instance, out var herd) || !herd.Herd) return;
        for (int i = __state; i < __instance.commandList.Count; i++)
            __instance.commandList[i].Enemy?.Buffs?.SetCustomVar(RebirthWanderingHerds.HerdVar, 1f);
        if (Time.time >= herd.Until || RebirthWanderingHerds.ShouldStop(_world, __instance.director))
        {
            herd.Herd = false;
            return;
        }
        if (!__result) return;
        if (__instance.spawner.IsDone) __instance.spawner.SetPartyLevel(__instance.spawner.partyLevel);
        __result = false;
    }
}

[HarmonyPatch(typeof(AIDirectorGameStagePartySpawner), "SetupGroup")]
internal static class RebirthWanderingHordeSizePatch
{
    private static void Postfix(AIDirectorGameStagePartySpawner __instance)
    {
        int size = RebirthWanderingHerds.SizeMultiplier;
        string group = __instance.spawnGroupName;
        if (size <= 1 || group == null || group.IndexOf("wanderingHorde", StringComparison.OrdinalIgnoreCase) < 0) return;
        __instance.numToSpawn *= size;
    }
}

[HarmonyPatch(typeof(AIDirectorBloodMoonComponent), "EndBloodMoon")]
internal static class RebirthWanderingHerdAfterHordeNight
{
    private static void Prefix(AIDirectorBloodMoonComponent __instance, ref bool __state)
    {
        __state = __instance.BloodMoonActive;
    }

    private static void Postfix(AIDirectorBloodMoonComponent __instance, bool __state)
    {
        var options = RebirthSandboxOptionManager.Current;
        if (!__state || options.WanderingHordeFrequency == RebirthWanderingHordeFrequency.Off
            || UnityEngine.Random.Range(0, 100) >= options.WanderingHerdAfterHordeNight
            || !GameStats.GetBool(EnumGameStats.ZombieHordeMeter) || !GameStats.GetBool(EnumGameStats.IsSpawnEnemies)) return;
        var wandering = __instance.Director?.GetComponent<AIDirectorWanderingHordeComponent>();
        if (wandering == null) return;
        RebirthWanderingHerds.ForceNextHerd();
        wandering.StartSpawning(AIWanderingHordeSpawner.SpawnType.Horde);
    }
}

// 2.6 despawn option: zombies released by the director leave 15 s later unless a player is close or targeted.
[HarmonyPatch(typeof(AIWanderingHordeSpawner), nameof(AIWanderingHordeSpawner.UpdateHorde))]
internal static class RebirthWanderingHordeDespawnWatch
{
    private static readonly List<EntityEnemy> Before = new List<EntityEnemy>();

    private static void Prefix(AIWanderingHordeSpawner __instance)
    {
        Before.Clear();
        if (!RebirthSandboxOptionManager.Current.WanderingHordeDespawn) return;
        for (int i = 0; i < __instance.commandList.Count; i++) Before.Add(__instance.commandList[i].Enemy);
    }

    private static void Postfix(AIWanderingHordeSpawner __instance)
    {
        if (Before.Count == __instance.commandList.Count) return;
        for (int i = 0; i < Before.Count; i++)
        {
            var enemy = Before[i];
            bool released = enemy != null && !enemy.IsDead();
            for (int j = 0; released && j < __instance.commandList.Count; j++)
                if (ReferenceEquals(__instance.commandList[j].Enemy, enemy)) released = false;
            if (released) RebirthWanderingHordeDespawnTimers.Start(enemy);
        }
    }
}

[HarmonyPatch(typeof(AIDirectorWanderingHordeComponent), nameof(AIDirectorWanderingHordeComponent.Tick))]
internal static class RebirthWanderingHordeDespawnTimers
{
    private const float Delay = 15f;
    private const float Recheck = 1f;
    private const float ProtectRadiusSquared = 80f * 80f;
    private static readonly Dictionary<EntityEnemy, float> Due = new Dictionary<EntityEnemy, float>();
    private static readonly List<EntityEnemy> Scratch = new List<EntityEnemy>();
    private static float next;

    internal static void Start(EntityEnemy enemy) { Due[enemy] = Time.time + Delay; }

    private static void Postfix(AIDirectorWanderingHordeComponent __instance)
    {
        if (Due.Count == 0 || Time.time < next) return;
        next = Time.time + Recheck;
        Scratch.Clear();
        Scratch.AddRange(Due.Keys);
        var players = __instance.Director.World.GetPlayers();
        foreach (var enemy in Scratch)
        {
            if (enemy == null || enemy.IsDead() || !RebirthSandboxOptionManager.Current.WanderingHordeDespawn) { Due.Remove(enemy); continue; }
            if (Time.time < Due[enemy] || enemy.GetAttackTarget() is EntityPlayer) continue;
            bool near = false;
            for (int i = 0; !near && i < players.Count; i++)
                near = players[i] != null && !players[i].IsDead() && (players[i].position - enemy.position).sqrMagnitude <= ProtectRadiusSquared;
            if (near) continue;
            Due.Remove(enemy);
            enemy.ForceDespawn();
        }
    }
}

// Native wandering hordes and horde night select through this method rather than GetRandomFromGroup.
// Wandering hordes resolve through composition and never contain animals. Horde night keeps its
// native pools, so zombie animals stay available there; only the Demolisher option applies to it.
[HarmonyPatch(typeof(EntityGroups), nameof(EntityGroups.GetRandomEntityFromGroupMaxTier))]
internal static class RebirthWanderingHordeSelection
{
    private const int MaximumRerolls = 5;
    [ThreadStatic] private static int rerollDepth;

    private static bool Prefix(string _sEntityGroupName, EntityClass.EntityTierTypes maxTier, ref int lastClassId, ref int __result)
    {
        RebirthSpawnContext active = RebirthSpawnCompositionRuntimeIntegration.Current;
        if (active == null || active.Surface != RebirthSpawnSurface.WanderingHorde || active.DeferCompositionUntilPosition) return true;
        active.RequestedGroup = _sEntityGroupName ?? string.Empty;
        var random = GameManager.Instance?.World?.GetGameRandom();
        if (random == null) return true;
        if (!RebirthSpawnCompositionService.TrySelect(active, delegate { return (double)random.RandomFloat; }, out RebirthSpawnTrace trace) || trace == null) return true;
        EntityClass selected = EntityClass.GetEntityClass(trace.EntityClassId);
        EntityClass allowed = selected != null ? EntityClass.GetEntityClassWithinMaxTier(selected, maxTier) : null;
        if (allowed == null || allowed.bIsAnimalEntity) return true;
        __result = EntityClass.GetId(allowed.entityClassName);
        lastClassId = __result;
        return false;
    }

    private static void Postfix(string _sEntityGroupName, EntityClass.EntityTierTypes maxTier, ref int lastClassId, bool isEnemy, bool isAnimal, GameRandom random, ref int __result)
    {
        RebirthSpawnContext active = RebirthSpawnCompositionRuntimeIntegration.Current;
        if (__result == -1 || active == null) return;
        bool wandering = active.Surface == RebirthSpawnSurface.WanderingHorde;
        if (!wandering && active.Surface != RebirthSpawnSurface.BloodMoon) return;
        if (!wandering && rerollDepth == 0 && RebirthEventSpawnPolicy.TryHordeNightExtra(active.GameStage, out int extra))
        {
            __result = extra;
            lastClassId = extra;
            return;
        }
        EntityClass picked = EntityClass.GetEntityClass(__result);
        if (picked == null) return;
        bool reject = wandering && picked.bIsAnimalEntity;
        if (!reject && RebirthDemolisherPolicy.IsDemolisher(picked.entityClassName)) reject = RebirthDemolisherPolicy.RejectsNativePick(active.Surface);
        if (!reject) return;
        // -1 is the native "nothing allowed" result; the spawner skips this tick and tries again.
        int replacement = -1;
        if (rerollDepth < MaximumRerolls)
        {
            rerollDepth++;
            try { replacement = EntityGroups.GetRandomEntityFromGroupMaxTier(_sEntityGroupName, maxTier, ref lastClassId, isEnemy, isAnimal, random); }
            finally { rerollDepth--; }
        }
        __result = replacement;
    }
}
