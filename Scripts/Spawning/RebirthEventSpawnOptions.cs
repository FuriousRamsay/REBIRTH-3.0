using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

// 2.6 game options for Kamikazes, tornadoes, screamer limits and the gamestage multiplier.
internal static class RebirthEventSpawnPolicy
{
    internal static readonly int[] TornadoSteps = { 25, 50, 75, 100, 150, 200, 250, 300 };
    private const string KamikazePrefix = "rebirthSeekerKamikaze";
    private const string TornadoClass = "rebirthWindTornado";
    private const float HordeNightExtraChance = 0.01f;

    internal static int ApplyGamestage(int stage)
        => stage * RebirthSandboxOptionManager.Current.GamestageMultiplier / 100;

    private static bool IsBloodMoon()
        => GameManager.Instance?.World?.aiDirector?.BloodMoonComponent?.BloodMoonActive == true;

    private static bool AllowsKamikaze(RebirthSpawnSurface surface)
    {
        bool poi = surface == RebirthSpawnSurface.Sleeper;
        bool night = surface == RebirthSpawnSurface.BloodMoon || IsBloodMoon();
        switch (RebirthSandboxOptionManager.Current.KamikazeMode)
        {
            case RebirthKamikazeMode.Never: return false;
            case RebirthKamikazeMode.NotInPois: return !poi;
            case RebirthKamikazeMode.NotHordeNight: return !night;
            case RebirthKamikazeMode.NotHordeNightOrPois: return !poi && !night;
            default: return true;
        }
    }

    private static bool AllowsTornado(RebirthSpawnSurface surface)
    {
        switch (RebirthSandboxOptionManager.Current.TornadoMode)
        {
            case RebirthTornadoMode.BiomesOnly: return surface == RebirthSpawnSurface.Biome && !IsBloodMoon();
            case RebirthTornadoMode.HordeNightOnly: return surface == RebirthSpawnSurface.BloodMoon;
            case RebirthTornadoMode.Always: return surface == RebirthSpawnSurface.Biome || surface == RebirthSpawnSurface.BloodMoon;
            default: return false;
        }
    }

    internal static bool Allows(string name, RebirthSpawnSurface surface)
    {
        if (name == null) return true;
        if (name.StartsWith(KamikazePrefix, StringComparison.Ordinal)) return AllowsKamikaze(surface);
        return name != TornadoClass || AllowsTornado(surface);
    }

    internal static double WeightScale(string name)
        => name == TornadoClass ? RebirthSandboxOptionManager.Current.TornadoMultiplier / 100.0 : 1.0;

    // Horde night keeps its native pools, so these two join by a small replacement chance.
    internal static bool TryHordeNightExtra(int stage, out int classId)
    {
        classId = -1;
        if (AllowsTornado(RebirthSpawnSurface.BloodMoon)
            && UnityEngine.Random.value < HordeNightExtraChance * RebirthSandboxOptionManager.Current.TornadoMultiplier / 100f)
        {
            classId = EntityClass.FromString(TornadoClass);
            return EntityClass.GetEntityClass(classId) != null;
        }
        if (!AllowsKamikaze(RebirthSpawnSurface.BloodMoon) || UnityEngine.Random.value >= HordeNightExtraChance) return false;
        classId = EntityClass.FromString(KamikazePrefix + (stage >= 340 ? "Tainted" : stage >= 220 ? "Radiated" : stage >= 100 ? "Feral" : string.Empty));
        return EntityClass.GetEntityClass(classId) != null;
    }

    internal static int CountScreamersNear(World world, Vector3 position)
    {
        var found = world.GetEntitiesInBounds(typeof(EntityZombie),
            new Bounds(position, new Vector3(300f, 300f, 300f)), new List<Entity>());
        int count = 0;
        for (int i = 0; i < found.Count; i++)
        {
            var zombie = found[i] as EntityZombie;
            if (zombie != null && !zombie.IsDead() && zombie.EntityClass?.entityClassName?.IndexOf("screamer", StringComparison.OrdinalIgnoreCase) >= 0) count++;
        }
        return count;
    }
}

[HarmonyPatch(typeof(GameStageDefinition), nameof(GameStageDefinition.CalcPartyLevel), new[] { typeof(List<int>) })]
internal static class RebirthGamestageMultiplierPatch
{
    private static void Postfix(ref int __result) { __result = RebirthEventSpawnPolicy.ApplyGamestage(__result); }
}

[HarmonyPatch(typeof(AIDirectorChunkEventComponent), nameof(AIDirectorChunkEventComponent.SpawnScouts))]
internal static class RebirthScreamerMaxPatch
{
    private static bool Prefix(AIDirectorChunkEventComponent __instance, Vector3 targetPos)
        => RebirthEventSpawnPolicy.CountScreamersNear(__instance.Director.World, targetPos) < RebirthSandboxOptionManager.Current.ScreamerMax;
}

// 2.6 screamer nights: on a chosen night, players of level 20+ periodically draw scout screamers.
[HarmonyPatch(typeof(AIDirectorChunkEventComponent), nameof(AIDirectorChunkEventComponent.Tick))]
internal static class RebirthScreamerNights
{
    private const float CheckSeconds = 60f;
    private const int MinimumLevel = 20;
    private static float nextCheck;
    private static int nextNight = -1;
    private static bool active;

    private static void Postfix(AIDirectorChunkEventComponent __instance)
    {
        var mode = RebirthSandboxOptionManager.Current.ScreamerNights;
        if (mode == RebirthScreamerNightMode.None || Time.time < nextCheck) return;
        nextCheck = Time.time + CheckSeconds;
        var world = __instance.Director.World;
        if (!GameStats.GetBool(EnumGameStats.ZombieHordeMeter) || !GameStats.GetBool(EnumGameStats.IsSpawnEnemies)) return;
        int day = GameUtils.WorldTimeToDays(world.worldTime);
        if (world.IsDaytime())
        {
            if (active)
            {
                active = false;
                nextNight = day + (mode == RebirthScreamerNightMode.Higher ? 1
                    : mode == RebirthScreamerNightMode.Lower ? UnityEngine.Random.Range(4, 6) : UnityEngine.Random.Range(2, 4));
            }
            if (nextNight < day) nextNight = day + UnityEngine.Random.Range(0, 3);
            return;
        }
        if (!active && nextNight == day) active = true;
        if (!active) return;
        int top = mode == RebirthScreamerNightMode.Higher ? 75 : mode == RebirthScreamerNightMode.Lower ? 225 : 150;
        var players = world.GetPlayers();
        for (int i = 0; players != null && i < players.Count; i++)
        {
            var player = players[i];
            if (player == null || player.IsDead() || player.Progression == null || player.Progression.Level < MinimumLevel) continue;
            if (UnityEngine.Random.Range(0, top) < Math.Min(top, player.Progression.Level)) __instance.SpawnScouts(player.position);
        }
    }
}

// Tornado block damage runs outside horde night only, as the 2.6 buffs did.
[Preserve]
public sealed class MinEventActionRebirthTornadoBlast : MinEventActionBase
{
    public override void Execute(MinEventParams args)
    {
        var tornado = args.Self as EntityAlive;
        if (tornado == null || tornado.world == null || tornado.world.IsRemote() || tornado.IsDead()
            || tornado.world.aiDirector?.BloodMoonComponent?.BloodMoonActive == true) return;
        var data = new ExplosionData(tornado.EntityClass.Properties.Classes["Explosion"], null);
        GameManager.Instance.ExplosionServer(tornado.position, tornado.GetBlockPosition(), tornado.qrotation,
            data, tornado.entityId, 0f, false, ItemClass.GetItem("rebirthSeekerRoarSource", false));
    }
}

[Preserve]
public sealed class MinEventActionRebirthTornadoDespawn : MinEventActionBase
{
    public override void Execute(MinEventParams args)
    {
        var tornado = args.Self as EntityAlive;
        if (tornado != null && tornado.world != null && !tornado.world.IsRemote()) tornado.ForceDespawn();
    }
}
