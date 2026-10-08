using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Makes a player's own live companions transparent to that player's weapon raycasts.
///
/// 2.6 achieved the same player-facing result by suppressing friendly companion collision
/// around the owner and by rerouting player attack rays past owned friendlies. 3.1 already
/// uses model layer 2 to temporarily remove the firing entity from its own raycasts. Reuse
/// that native mechanism narrowly around the existing ranged, melee and projectile casts so
/// all vanilla penetration, block-hit, damage and projectile behavior remains authoritative.
/// </summary>
public static class RebirthOwnedCompanionAttackPassthrough
{
    private const float CacheSeconds = 0.20f;

    internal struct LayerEntry
    {
        public EntityAlive Entity;
        public int OriginalLayer;
    }

    internal sealed class LayerState
    {
        public readonly List<LayerEntry> Entries = new List<LayerEntry>(4);
    }

    private sealed class OwnerCache
    {
        public World World;
        public float ExpiresAt;
        public readonly List<EntityAlive> Companions = new List<EntityAlive>(4);
    }

    private static readonly Dictionary<int, OwnerCache> Cache = new Dictionary<int, OwnerCache>();

    public static void ResetForWorldChange()
    {
        Cache.Clear();
    }

    internal static LayerState HideOwnedCompanions(EntityPlayer player)
    {
        if (player == null || player.world == null) return null;

        List<EntityAlive> companions = GetOwnedCompanions(player);
        if (companions == null || companions.Count == 0) return null;

        LayerState state = new LayerState();
        for (int i = 0; i < companions.Count; i++)
        {
            EntityAlive companion = companions[i];
            if (companion == null || companion.IsDead() || companion.entityId == player.entityId) continue;

            int originalLayer;
            try { originalLayer = companion.GetModelLayer(); }
            catch { continue; }

            state.Entries.Add(new LayerEntry { Entity = companion, OriginalLayer = originalLayer });
            try { companion.SetModelLayer(2); }
            catch { state.Entries.RemoveAt(state.Entries.Count - 1); }
        }

        return state.Entries.Count > 0 ? state : null;
    }

    internal static void Restore(LayerState state)
    {
        if (state == null) return;
        for (int i = state.Entries.Count - 1; i >= 0; i--)
        {
            LayerEntry entry = state.Entries[i];
            if (entry.Entity == null) continue;
            try { entry.Entity.SetModelLayer(entry.OriginalLayer); }
            catch { }
        }
    }

    private static List<EntityAlive> GetOwnedCompanions(EntityPlayer player)
    {
        OwnerCache cache;
        float now = Time.realtimeSinceStartup;
        if (!Cache.TryGetValue(player.entityId, out cache))
        {
            cache = new OwnerCache();
            Cache[player.entityId] = cache;
        }

        if (cache.World == player.world && now < cache.ExpiresAt)
            return cache.Companions;

        cache.World = player.world;
        cache.ExpiresAt = now + CacheSeconds;
        cache.Companions.Clear();

        List<Entity> loaded = player.world.Entities != null ? player.world.Entities.list : null;
        if (loaded == null) return cache.Companions;

        string ownerId = string.Empty;
        RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId);

        for (int i = 0; i < loaded.Count; i++)
        {
            EntityAlive candidate = loaded[i] as EntityAlive;
            if (candidate == null || candidate.IsDead() || candidate.entityId == player.entityId) continue;
            if (!IsOwnedCompanion(player, ownerId, candidate)) continue;
            cache.Companions.Add(candidate);
        }

        return cache.Companions;
    }

    private static bool IsOwnedCompanion(EntityPlayer player, string ownerId, EntityAlive candidate)
    {
        EntityRebirthDogCompanion dog = candidate as EntityRebirthDogCompanion;
        if (dog != null)
            return RebirthDogLifecycleService.IsOwnedBy(dog, player);

        EntityDrone drone = candidate as EntityDrone;
        if (drone != null)
            return drone.belongsPlayerId == player.entityId || player.HasOwnedEntity(drone.entityId);

        EntityRebirthNPC npc = candidate as EntityRebirthNPC;
        RebirthNpcRuntimeState state = npc != null ? npc.RebirthRuntimeState : null;
        return state != null && state.OwnershipKind == RebirthNpcOwnershipKind.Player &&
            !string.IsNullOrWhiteSpace(ownerId) &&
            string.Equals(state.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Hitscan firearms: hide the shooter's own companions while the native 3.1 fireShot
/// performs its Voxel.Raycast. This is stronger than cancelling companion damage after the
/// fact because the original shot genuinely resolves against whatever is behind the dog/drone.
/// </summary>
[HarmonyPatch(typeof(ItemActionRanged), "fireShot")]
internal static class RebirthOwnedCompanionRangedRaycastPatch
{
    private static void Prefix(ItemActionRanged.ItemActionDataRanged _actionData,
        out RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        EntityPlayer player = _actionData != null && _actionData.invData != null
            ? _actionData.invData.holdingEntity as EntityPlayer
            : null;
        __state = RebirthOwnedCompanionAttackPassthrough.HideOwnedCompanions(player);
    }

    private static void Postfix(RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        RebirthOwnedCompanionAttackPassthrough.Restore(__state);
    }

    private static Exception Finalizer(Exception __exception, RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        if (__exception != null) RebirthOwnedCompanionAttackPassthrough.Restore(__state);
        return __exception;
    }
}

/// <summary>
/// Player melee: apply the same temporary model-layer exclusion while the native melee
/// target ray/sphere cast runs. The resulting WorldRayHitInfo therefore points to the real
/// target/block behind the companion instead of producing a zero-damage companion hit.
/// </summary>
[HarmonyPatch(typeof(ItemActionMelee), nameof(ItemActionMelee.GetExecuteActionTarget))]
internal static class RebirthOwnedCompanionMeleeRaycastPatch
{
    private static void Prefix(ItemActionData _actionData,
        out RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        EntityPlayer player = _actionData != null && _actionData.invData != null
            ? _actionData.invData.holdingEntity as EntityPlayer
            : null;
        __state = RebirthOwnedCompanionAttackPassthrough.HideOwnedCompanions(player);
    }

    private static void Postfix(RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        RebirthOwnedCompanionAttackPassthrough.Restore(__state);
    }

    private static Exception Finalizer(Exception __exception, RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        if (__exception != null) RebirthOwnedCompanionAttackPassthrough.Restore(__state);
        return __exception;
    }
}

/// <summary>
/// Physical projectiles (arrows/bolts/etc.): hide only the firing player's own companions
/// during each native projectile collision cast. Vanilla then continues the projectile to
/// the next actual collision instead of sticking/dying on the companion.
/// </summary>
[HarmonyPatch(typeof(ProjectileMoveScript), "checkCollision")]
internal static class RebirthOwnedCompanionProjectileRaycastPatch
{
    private static void Prefix(ProjectileMoveScript __instance,
        out RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        EntityPlayer player = __instance != null ? __instance.firingEntity as EntityPlayer : null;
        __state = RebirthOwnedCompanionAttackPassthrough.HideOwnedCompanions(player);
    }

    private static void Postfix(RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        RebirthOwnedCompanionAttackPassthrough.Restore(__state);
    }

    private static Exception Finalizer(Exception __exception, RebirthOwnedCompanionAttackPassthrough.LayerState __state)
    {
        if (__exception != null) RebirthOwnedCompanionAttackPassthrough.Restore(__state);
        return __exception;
    }
}
