using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

internal static class RebirthSeekerPolicy
{
    internal static bool IsBloodMoon(World world) => world?.aiDirector?.BloodMoonComponent?.BloodMoonActive == true;
    internal static bool IsSeeker(string name) => name != null &&
        (name.StartsWith("FuriousRamsaySeeker", StringComparison.OrdinalIgnoreCase) || name == "Seeker001");
    internal static bool Allows(bool bloodMoon, bool questing)
    {
        switch (RebirthSandboxOptionManager.Current.SeekerMode)
        {
            case RebirthSeekerMode.Never: return false;
            case RebirthSeekerMode.NotHordeNight: return !bloodMoon;
            // The legacy blood-moon producer checks Never/NotHordeNight only.
            case RebirthSeekerMode.NotQuesting: return bloodMoon || !questing;
            default: return true;
        }
    }
    internal static int Tier(int level, bool bloodMoon) => level < 30 ? 0 : Math.Min(7, 1 + (level - 30) / (bloodMoon ? 20 : 10));
    internal static int Count(int tier) => tier < 1 ? 0 : tier < 3 ? 1 : tier < 5 ? 2 : tier < 7 ? 3 : 4;
    internal static bool RestrictionAllows(RebirthSeekerRestriction restriction, bool questing, bool outdoors)
    {
        switch (restriction)
        {
            case RebirthSeekerRestriction.Off: return false;
            case RebirthSeekerRestriction.OnlyQuesting: return questing;
            case RebirthSeekerRestriction.NotQuesting: return !questing;
            case RebirthSeekerRestriction.OutdoorsOnly: return outdoors;
            case RebirthSeekerRestriction.OutdoorsAndNotQuesting: return outdoors && !questing;
            default: return true;
        }
    }
    internal static string MinionSuffix(int stage) => stage >= 380 ? "Tainted" : stage >= 260 ? "Radiated" : stage >= 140 ? "Feral" : "";
}

[Preserve]
public sealed class RebirthSeekerModApi : IModApi
{
    private static bool installed;
    public void InitMod(Mod mod)
    {
        if (installed) return;
        var h = new Harmony("rebirth.seeker");
        try
        {
            h.CreateClassProcessor(typeof(RebirthSeekerFallHook)).Patch();
            h.CreateClassProcessor(typeof(RebirthSeekerGroupHook)).Patch();
            ModEvents.GameUpdate.RegisterHandler(Update);
            ModEvents.GameStarting.RegisterHandler(Starting);
            ModEvents.WorldShuttingDown.RegisterHandler(Shutdown);
            installed = true;
        }
        catch { h.UnpatchSelf(); throw; }
    }
    private static void Update(ref ModEvents.SGameUpdateData data) { RebirthSeekerRuntime.Pulse(); }
    private static void Starting(ref ModEvents.SGameStartingData data) { RebirthSeekerRuntime.Reset(); }
    private static void Shutdown(ref ModEvents.SWorldShuttingDownData data) { RebirthSeekerRuntime.Reset(); }
}

internal static class RebirthSeekerRuntime
{
    private sealed class Opportunity { internal EntityPlayer Player; internal float Next; }
    private static readonly Dictionary<int, Opportunity> opportunities = new Dictionary<int, Opportunity>();
    private static readonly List<int> retired = new List<int>();
    private static readonly FastTags<TagGroup.Poi> Underground = FastTags<TagGroup.Poi>.Parse("cave,underground");
    private static World scope;
    private static float due;
    internal static void Reset() { opportunities.Clear(); retired.Clear(); scope = null; due = 0; }
    internal static void Pulse()
    {
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote()) return;
        if (!ReferenceEquals(scope, world)) { Reset(); scope = world; }
        float now = Time.realtimeSinceStartup;
        if (now < due) return;
        due = now + 1f;
        if (RebirthSandboxOptionManager.Current.SeekerMode == RebirthSeekerMode.Never) { opportunities.Clear(); return; }
        var players = world.GetPlayers();
        if (players == null) return;
        // Native connected player population bounds this table; replaced entities never
        // inherit another player's due opportunity on reconnect or entity-ID reuse.
        retired.Clear();
        foreach (var pair in opportunities)
            if (!ReferenceEquals(world.GetEntity(pair.Key), pair.Value.Player)) retired.Add(pair.Key);
        foreach (int key in retired) opportunities.Remove(key);
        foreach (var player in players)
        {
            if (player == null) continue;
            Opportunity slot;
            if (!opportunities.TryGetValue(player.entityId, out slot) || !ReferenceEquals(slot.Player, player))
            { opportunities[player.entityId] = new Opportunity { Player = player, Next = now + (RebirthSeekerPolicy.IsBloodMoon(world) || RebirthSandboxOptionManager.Current.SeekerFrequency <= 0 ? 30f : 3000f / RebirthSandboxOptionManager.Current.SeekerFrequency) }; continue; }
            if (now < slot.Next) continue;
            bool moon = RebirthSeekerPolicy.IsBloodMoon(GameManager.Instance?.World);
            int frequency = RebirthSandboxOptionManager.Current.SeekerFrequency;
            slot.Next = now + (moon || frequency <= 0 ? 30f : 3000f / frequency);
            if (!moon && frequency <= 0) continue;
            if (!Eligible(world, player, moon)) continue;
            int tier = RebirthSeekerPolicy.Tier(player.Progression.Level, moon);
            if (tier == 0) continue;
            int roll = world.GetGameRandom().RandomRange(0, moon ? 49 : 500);
            if (moon ? roll != 0 : roll > 1) continue;
            SpawnEvent(world, player, tier);
        }
    }
    internal static bool Eligible(World world, EntityPlayer player, bool moon)
    {
        if (world == null || world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World, world)
            || player == null || !ReferenceEquals(world.GetEntity(player.entityId), player) || !player.IsSpawned()
            || !ReferenceEquals(player.world, world) || !player.IsAlive() || player.IsSpectator || player.IsFlyMode.Value || player.AttachedToEntity != null
            || player.Progression == null) return false;
        bool questing = RebirthBossEventEligibility.IsQuesting(player);
        if (!RebirthSeekerPolicy.Allows(moon, questing)) return false;
        if (!moon &&
            ((!RebirthSandboxOptionManager.Current.SeekerHordeDay && RebirthBossEventEligibility.IsBloodMoonDay(world))
             || !RebirthSeekerPolicy.RestrictionAllows(RebirthSandboxOptionManager.Current.SeekerRestriction,
                questing, RebirthBossEventPlacement.IsOutdoors(world, player.position)))) return false;
        // The legacy event producer excludes caves/underground POIs and mounted players.
        var poi = GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefabAtPosition(player.position);
        if (poi?.prefab != null) return !poi.prefab.Tags.Test_AnySet(Underground);
        if (player.position.y >= world.GetHeight((int)player.position.x, (int)player.position.z)) return true;
        // Preserve the actual legacy caveAir test. Being below terrain alone must
        // not suppress encounters in every ordinary basement or excavation.
        var center = player.GetBlockPosition();
        for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) for (int z = -2; z <= 2; z++)
            if (world.GetBlock(center + new Vector3i(x, y, z)).Block?.GetBlockName() == "caveAir") return false;
        return true;
    }
    internal static int SpawnEvent(World world, EntityPlayer player, int tier, bool natural = true)
    {
        int accepted = 0;
        string name = "FuriousRamsaySeeker" + tier.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
        for (int i = 0; i < RebirthSeekerPolicy.Count(tier); i++)
        {
            if (natural && !Eligible(world, player, RebirthSeekerPolicy.IsBloodMoon(GameManager.Instance?.World))) break;
            float angle = world.GetGameRandom().RandomFloat * Mathf.PI * 2f;
            var point = player.position + new Vector3(Mathf.Cos(angle) * 50f, 50f, Mathf.Sin(angle) * 50f);
            if (!world.IsPositionInBounds(point)) continue;
            // Keep the legacy 50m sky drop above both current player and terrain.
            point.y = Math.Max(point.y, world.GetHeightAt(point.x, point.z) + 50f);
            if (Spawn(world, player, name, point, true, 1f) != null) accepted++;
        }
        // Only an accepted native spawn produces a warning. Native MP tooltip delivery
        // keeps the message addressed to this exact player without a new wire protocol.
        if (accepted > 0 && RebirthSandboxOptionManager.Current.SeekerNotifications)
            GameManager.ShowTooltipMP(player, Localization.Get("ttSeeker"), string.Empty);
        return accepted;
    }
    internal static EntityAlive Spawn(World world, EntityPlayer player, string name, Vector3 position, bool skyDrop, float scale)
    {
        if (world == null || world.IsRemote() || !ReferenceEquals(GameManager.Instance?.World, world)
            || player == null || !ReferenceEquals(player.world, world) || !ReferenceEquals(world.GetEntity(player.entityId), player) || !player.IsAlive()) return null;
        int id = EntityClass.FromString(name);
        EntityClass definition;
        // Native3.1 class IDs are signed hashes; class registration is the test.
        if (!EntityClass.list.TryGetValue(id, out definition) || definition == null || definition.entityClassName != name) return null;
        var entity = EntityFactory.CreateEntity(id, position) as EntityAlive;
        if (entity == null) return null;
        entity.SetScale(scale);
        entity.SetAttackTarget(player, 60000);
        entity.sightRangeBase = 200;
        entity.SetMaxViewAngle(360);
        entity.OverrideSize = scale;
        entity.Buffs.SetCustomVar("$StartScale", scale);
        entity.Buffs.SetCustomVar("$eventSpawn", 1f);
        entity.Buffs.SetCustomVar("$varFuriousRamsayOpenDoors", 1f);
        if (skyDrop) entity.Buffs.SetCustomVar("$FR_SeekerSkyDropNoFallDamage", 1f);
        world.SpawnEntityInWorld(entity);
        return ReferenceEquals(world.GetEntity(entity.entityId), entity) ? entity : null;
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.DamageEntity), new Type[] { typeof(DamageSource), typeof(int), typeof(bool), typeof(float) })]
internal static class RebirthSeekerFallHook
{
    private static bool Prefix(EntityAlive __instance, DamageSource _damageSource, ref int __result)
    {
        if (!(__instance is EntityRebirthSeeker) || __instance.world?.IsRemote() != false || __instance.Buffs == null
            || _damageSource == null || _damageSource.damageType != EnumDamageTypes.Falling
            || __instance.Buffs.GetCustomVar("$FR_SeekerSkyDropNoFallDamage") != 1f) return true;
        __instance.Buffs.SetCustomVar("$FR_SeekerSkyDropNoFallDamage", 0f);
        __result = 0; return false;
    }
}

// Also honor the mode for native/custom authored groups outside managed composition.
// Returning the native no-selection sentinel preserves the caller's retry authority.
[HarmonyPatch(typeof(EntityGroups), nameof(EntityGroups.GetRandomFromGroupList))]
internal static class RebirthSeekerGroupHook
{
    private static void Postfix(ref int __result)
    {
        EntityClass definition;
        if (!EntityClass.list.TryGetValue(__result, out definition) || definition == null || !RebirthSeekerPolicy.IsSeeker(definition.entityClassName)) return;
        if (!RebirthSeekerPolicy.Allows(RebirthSeekerPolicy.IsBloodMoon(GameManager.Instance?.World), false)) __result = -1;
    }
}
