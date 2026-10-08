using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Client-local HUD style override used by the REBIRTH shortcut. The configured sandbox
/// option remains the default on world load; Shift+L only changes the current play-session
/// presentation and never mutates the authoritative gameplay option/network state.
/// </summary>
public static class RebirthCompanionCardStyleRuntime
{
    private static bool hasLocalOverride;
    private static RebirthCompanionCardStyle localStyle = RebirthCompanionCardStyle.Simple;

    public static RebirthCompanionCardStyle CurrentStyle
    {
        get
        {
            return hasLocalOverride
                ? localStyle
                : RebirthSandboxOptionManager.Current.CompanionCardStyle;
        }
    }

    public static void Toggle(EntityPlayerLocal player)
    {
        RebirthCompanionCardStyle current = CurrentStyle;
        localStyle = current == RebirthCompanionCardStyle.Simple
            ? RebirthCompanionCardStyle.Default
            : RebirthCompanionCardStyle.Simple;
        hasLocalOverride = true;

        if (player != null)
        {
            string label = localStyle == RebirthCompanionCardStyle.Simple
                ? Localization.Get("xuiRebirthCompanionCardsSimple")
                : Localization.Get("xuiRebirthCompanionCardsDefault");
            try { GameManager.ShowTooltip(player, label); } catch { }
        }
    }

    public static void ResetForWorldChange()
    {
        hasLocalOverride = false;
        localStyle = RebirthCompanionCardStyle.Simple;
    }
}


internal static class RebirthOwnedCompanionIndex
{
    public static List<EntityAlive> Snapshot(EntityPlayer player)
    {
        var result=new List<EntityAlive>();var seen=new HashSet<int>();
        if(player==null||player.world==null)return result;
        if(player.Companions!=null)for(int i=0;i<player.Companions.Count;i++)Add(player.Companions[i],result,seen);
        List<OwnedEntityData> drones=player.GetOwnedEntities(EntityClass.junkDroneClass);
        if(drones!=null)for(int i=0;i<drones.Count;i++){OwnedEntityData owned=drones[i];Add(owned!=null?player.world.GetEntity(owned.Id) as EntityAlive:null,result,seen);}
        string ownerId;
        if(RebirthDogLifecycleService.TryResolveOwnerId(player,out ownerId)&&!string.IsNullOrEmpty(ownerId))
        {
            RebirthNpcRuntimeState[] states=RebirthNpcRuntimeRegistry.GetSnapshot();
            for(int i=0;i<states.Length;i++)
            {
                RebirthNpcRuntimeState state=states[i];
                if(state==null||state.Presence!=RebirthNpcPresenceState.Active||state.OwnershipKind!=RebirthNpcOwnershipKind.Player||
                   !string.Equals(state.OwnerId??string.Empty,ownerId,StringComparison.OrdinalIgnoreCase))continue;
                int entityId;if(RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId,out entityId))Add(player.world.GetEntity(entityId) as EntityAlive,result,seen);
            }
        }
        return result;
    }
    private static void Add(EntityAlive entity,List<EntityAlive> result,HashSet<int> seen)
    {if(entity!=null&&!(entity is EntityPlayer)&&!entity.IsDead()&&seen.Add(entity.entityId))result.Add(entity);}
}

/// <summary>
/// Assigns a stable, owner-local color to each companion for the current world session.
/// The palette intentionally excludes the eight native Constants.TrackedFriendColors used
/// by player/party tracking. Slots are never reused during a session, so two companions
/// that have been assigned colors cannot accidentally collapse onto the same color.
/// </summary>
public static class RebirthCompanionColorService
{
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, int> AssignedSlots =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Deliberately distinct from stock player colors: green, blue, yellow, magenta,
    // brown, orange, dark brown and olive. The HUD can show at most 16 simple cards,
    // but a larger palette keeps non-following nav markers unique as well.
    private static readonly Color32[] CompanionPalette = new Color32[]
    {
        new Color32(0, 210, 210, 255),
        new Color32(255, 90, 90, 255),
        new Color32(80, 170, 255, 255),
        new Color32(80, 220, 150, 255),
        new Color32(155, 105, 255, 255),
        new Color32(255, 105, 180, 255),
        new Color32(150, 230, 70, 255),
        new Color32(255, 200, 70, 255),
        new Color32(0, 145, 160, 255),
        new Color32(235, 125, 90, 255),
        new Color32(115, 135, 245, 255),
        new Color32(80, 210, 185, 255),
        new Color32(185, 90, 210, 255),
        new Color32(255, 140, 200, 255),
        new Color32(185, 230, 90, 255),
        new Color32(245, 165, 55, 255),
        new Color32(70, 200, 245, 255),
        new Color32(220, 65, 100, 255),
        new Color32(60, 180, 125, 255),
        new Color32(180, 145, 255, 255),
        new Color32(40, 230, 200, 255),
        new Color32(255, 155, 110, 255),
        new Color32(100, 85, 220, 255),
        new Color32(105, 190, 95, 255),
        new Color32(55, 185, 225, 255),
        new Color32(230, 95, 145, 255),
        new Color32(120, 205, 175, 255),
        new Color32(205, 130, 245, 255),
        new Color32(235, 185, 105, 255),
        new Color32(85, 155, 205, 255),
        new Color32(210, 115, 165, 255),
        new Color32(125, 210, 115, 255)
    };

    private static float nextNavRefresh;

    public static Color GetColor(EntityAlive companion, EntityPlayerLocal localPlayer)
    {
        return GetColorForKey(BuildKey(companion));
    }

    public static string GetColorString(EntityAlive companion, EntityPlayerLocal localPlayer)
    {
        Color32 c = (Color32)GetColor(companion, localPlayer);
        return c.r + "," + c.g + "," + c.b + "," + c.a;
    }

    public static Color GetDogColor(string stableId)
    {
        return GetColorForKey("dog:" + Normalize(stableId));
    }

    public static void ApplyDogMarkerColor(NavObject marker, string stableId)
    {
        if (marker == null) return;
        ApplyNavColor(marker, GetDogColor(stableId));
    }

    public static void RefreshLocalNavColors(EntityPlayerLocal player)
    {
        if (player == null || player.world == null || Time.realtimeSinceStartup < nextNavRefresh)
            return;
        nextNavRefresh = Time.realtimeSinceStartup + 0.5f;

        List<EntityAlive> entities=RebirthOwnedCompanionIndex.Snapshot(player);
        for (int i = 0; i < entities.Count; i++)
        {
            EntityAlive alive=entities[i];

            // Dogs use a position marker independent of the live entity; its color is applied
            // by RebirthDogNavigationMarkerService.Upsert. Other live companions can use their
            // native entity NavObject directly.
            if (alive is EntityRebirthDogCompanion) continue;
            try
            {
                if (alive.NavObject != null)
                    ApplyNavColor(alive.NavObject, GetColor(alive, player));
            }
            catch { }
        }
    }

    private static void ApplyNavColor(NavObject marker, Color color)
    {
        try
        {
            marker.UseOverrideColor = true;
            marker.OverrideColor = color;
        }
        catch { }
    }

    private static Color GetColorForKey(string key)
    {
        key = string.IsNullOrEmpty(key) ? "unknown" : key;
        int slot;
        lock (Sync)
        {
            if (!AssignedSlots.TryGetValue(key, out slot))
            {
                slot = AssignedSlots.Count;
                AssignedSlots[key] = slot;
            }
        }

        if (slot < CompanionPalette.Length)
            return CompanionPalette[slot];

        // Extremely large companion rosters fall back to a deterministic golden-angle hue.
        // The slot number is unique, so exact RGB duplicates are avoided across practical
        // roster sizes even after the authored palette is exhausted.
        float hue = Mathf.Repeat(0.53f + (slot - CompanionPalette.Length) * 0.61803398875f, 1f);
        float saturation = 0.62f + 0.12f * ((slot % 3) / 2f);
        float value = 0.88f + 0.10f * ((slot % 2));
        return Color.HSVToRGB(hue, saturation, value);
    }

    private static string BuildKey(EntityAlive companion)
    {
        if (companion == null) return "unknown";

        EntityRebirthDogCompanion dog = companion as EntityRebirthDogCompanion;
        if (dog != null && dog.RebirthRuntimeState != null && !dog.RebirthRuntimeState.StableId.IsEmpty)
            return "dog:" + dog.RebirthRuntimeState.StableId;

        EntityRebirthNPC npc = companion as EntityRebirthNPC;
        if (npc != null && npc.RebirthRuntimeState != null && !npc.RebirthRuntimeState.StableId.IsEmpty)
            return "npc:" + npc.RebirthRuntimeState.StableId;

        if (companion is EntityDrone)
            return "drone:" + companion.entityId;

        return companion.GetType().Name + ":" + companion.entityId;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) AssignedSlots.Clear();
        nextNavRefresh = 0f;
    }
}

/// <summary>
/// Keeps owned companions from physically shoving the owner or each other while several
/// are recalled to the same exact position. Physics.IgnoreCollision is pair-specific, so
/// world/block collision remains intact.
/// </summary>
public static class RebirthCompanionCollisionService
{
    private static readonly object Sync = new object();
    private static readonly HashSet<long> IgnoredEntityPairs = new HashSet<long>();
    private static float nextLocalRefresh;

    public static void PrepareForRecall(EntityAlive companion, EntityPlayer owner)
    {
        if (companion == null || owner == null) return;

        EntityRebirthDogCompanion dog = companion as EntityRebirthDogCompanion;
        if (dog != null)
        {
            // Dog runtime normally enables pass-through on its next live tick when a friendly
            // player is already within 5 m. Do it BEFORE relocation so there is no collision
            // frame at the destination.
            RebirthDogRuntimeService.SetFriendlyPlayerPassThrough(dog, true);
        }

        IgnoreEntityPair(companion, owner);

        List<EntityAlive> entities=RebirthOwnedCompanionIndex.Snapshot(owner);
        for (int i = 0; i < entities.Count; i++)
        {
            EntityAlive other=entities[i];
            if(other==companion)continue;
            IgnoreEntityPair(companion,other);
        }
    }

    public static void RefreshOwnedCompanionPlayerCollision(EntityPlayerLocal localPlayer)
    {
        if (localPlayer == null || localPlayer.world == null || Time.realtimeSinceStartup < nextLocalRefresh) return;
        nextLocalRefresh = Time.realtimeSinceStartup + 0.5f;
        List<EntityAlive> entities=RebirthOwnedCompanionIndex.Snapshot(localPlayer);
        for(int i=0;i<entities.Count;i++)IgnoreEntityPair(entities[i],localPlayer);
    }

    private static bool IsFriendlyOwnedCompanionToPlayer(EntityAlive companion, EntityPlayer player)
    {
        if (companion == null || player == null) return false;
        if (IsOwnedBy(companion, player)) return true;

        // Dogs already have a single combat/faction authority which understands owner PvP
        // protection and faction Like/Love/Leader relationships. Reuse it for collision too.
        EntityRebirthDogCompanion dog = companion as EntityRebirthDogCompanion;
        if (dog != null)
        {
            string relationReason;
            if (RebirthDogCombatRelationService.AreFriendly(dog, player, out relationReason))
                return true;
        }

        EntityPlayer companionOwner = ResolveCompanionOwner(companion, player.world);
        if (companionOwner != null)
        {
            if (companionOwner == player) return true;
            try
            {
                if (!companionOwner.FriendlyFireCheck(player)) return true;
            }
            catch { }

            try
            {
                FactionManager.Relationship ownerTier = FactionManager.Instance.GetRelationshipTier(companionOwner, player);
                if (ownerTier == FactionManager.Relationship.Like ||
                    ownerTier == FactionManager.Relationship.Love ||
                    ownerTier == FactionManager.Relationship.Leader)
                    return true;
            }
            catch { }
        }

        try
        {
            FactionManager.Relationship directTier = FactionManager.Instance.GetRelationshipTier(companion, player);
            return directTier == FactionManager.Relationship.Like ||
                   directTier == FactionManager.Relationship.Love ||
                   directTier == FactionManager.Relationship.Leader;
        }
        catch { return false; }
    }

    private static EntityPlayer ResolveCompanionOwner(EntityAlive companion, World world)
    {
        if (companion == null || world == null) return null;
        List<EntityPlayer> players = world.Players != null ? world.Players.list : null;
        if (players == null) return null;

        if (companion.belongsPlayerId >= 0)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i] != null && players[i].entityId == companion.belongsPlayerId)
                    return players[i];
        }


        EntityRebirthNPC npc = companion as EntityRebirthNPC;
        if (npc == null || npc.RebirthRuntimeState == null ||
            npc.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.Player ||
            string.IsNullOrWhiteSpace(npc.RebirthRuntimeState.OwnerId))
            return null;

        for (int i = 0; i < players.Count; i++)
        {
            EntityPlayer candidate = players[i];
            string ownerId;
            if (candidate != null && RebirthDogLifecycleService.TryResolveOwnerId(candidate, out ownerId) &&
                !string.IsNullOrEmpty(ownerId) &&
                string.Equals(ownerId, npc.RebirthRuntimeState.OwnerId, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
        return null;
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) IgnoredEntityPairs.Clear();
        nextLocalRefresh = 0f;
    }

    private static bool IsOwnedBy(EntityAlive entity, EntityPlayer owner)
    {
        if (entity == null || owner == null) return false;
        if (entity.belongsPlayerId == owner.entityId) return true;

        EntityRebirthNPC npc = entity as EntityRebirthNPC;
        if (npc == null || npc.RebirthRuntimeState == null ||
            npc.RebirthRuntimeState.OwnershipKind != RebirthNpcOwnershipKind.Player)
            return false;

        string ownerId;
        return RebirthDogLifecycleService.TryResolveOwnerId(owner, out ownerId) &&
            !string.IsNullOrEmpty(ownerId) &&
            string.Equals(npc.RebirthRuntimeState.OwnerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase);
    }

    private static void IgnoreEntityPair(Entity a, Entity b)
    {
        if (a == null || b == null || a.gameObject == null || b.gameObject == null) return;

        int firstId = a.entityId;
        int secondId = b.entityId;
        if (firstId == secondId) return;
        int low = Math.Min(firstId, secondId);
        int high = Math.Max(firstId, secondId);
        long pairKey = ((long)(uint)low << 32) | (uint)high;
        lock (Sync)
        {
            if (IgnoredEntityPairs.Contains(pairKey)) return;
        }

        try
        {
            Collider[] first = a.gameObject.GetComponentsInChildren<Collider>(true);
            Collider[] second = b.gameObject.GetComponentsInChildren<Collider>(true);
            if (first == null || second == null || first.Length == 0 || second.Length == 0) return;

            bool applied = false;
            for (int i = 0; i < first.Length; i++)
            {
                Collider ca = first[i];
                if (ca == null) continue;
                for (int j = 0; j < second.Length; j++)
                {
                    Collider cb = second[j];
                    if (cb == null) continue;
                    Physics.IgnoreCollision(ca, cb, true);
                    applied = true;
                }
            }

            if (applied)
            {
                lock (Sync) IgnoredEntityPairs.Add(pairKey);
            }
        }
        catch { }
    }
}

/// <summary>
/// Server-side owner position discontinuity detector. Remote client teleports are not always
/// executed through EntityPlayer.Teleport on the server, so this watches authoritative player
/// position and waits briefly for the new location to settle before recalling Follow companions.
/// </summary>
public static class RebirthCompanionOwnerTeleportService
{
    private sealed class PlayerTeleportState
    {
        public bool Initialized;
        public Vector3 LastPosition;
        public bool Pending;
        public Vector3 PendingPosition;
        public float SettleAt;
    }

    private const float TeleportJumpDistance = 20f;
    private const float SettleDistance = 1.5f;
    private const float SettleSeconds = 0.35f;

    private static readonly object Sync = new object();
    private static readonly Dictionary<int, PlayerTeleportState> States =
        new Dictionary<int, PlayerTeleportState>();

    public static void NotifyExplicitTeleport(EntityPlayer player)
    {
        if (player == null || player.world == null || player.world.IsRemote() || player.world.IsEditor() || player.IsDead())
            return;

        PlayerTeleportState state;
        lock (Sync)
        {
            if (!States.TryGetValue(player.entityId, out state))
            {
                state = new PlayerTeleportState();
                States[player.entityId] = state;
            }
        }

        Vector3 current = player.position;
        state.Initialized = true;
        state.LastPosition = current;
        state.Pending = true;
        state.PendingPosition = current;
        state.SettleAt = Time.realtimeSinceStartup + SettleSeconds;

        if (RebirthCompanionRecallDebug.TraceEnabled)
        {
            Log.Out("[REBIRTH CompanionRecall] OWNER_TELEPORT_EXPLICIT playerEntity=" + player.entityId +
                " pos=" + current);
        }
    }

    public static void Tick(EntityPlayer player)
    {
        if (player == null || player.world == null || player.world.IsRemote() || player.world.IsEditor())
            return;

        PlayerTeleportState state;
        lock (Sync)
        {
            if (!States.TryGetValue(player.entityId, out state))
            {
                state = new PlayerTeleportState();
                States[player.entityId] = state;
            }
        }

        Vector3 current = player.position;
        if (!state.Initialized)
        {
            state.Initialized = true;
            state.LastPosition = current;
            return;
        }

        float jumpSqr = (current - state.LastPosition).sqrMagnitude;
        state.LastPosition = current;

        // Do not reinterpret death/respawn relocation as a normal player teleport.
        if (player.IsDead())
        {
            state.Pending = false;
            state.PendingPosition = current;
            return;
        }

        if (jumpSqr >= TeleportJumpDistance * TeleportJumpDistance)
        {
            state.Pending = true;
            state.PendingPosition = current;
            state.SettleAt = Time.realtimeSinceStartup + SettleSeconds;
            if (RebirthCompanionRecallDebug.TraceEnabled)
            {
                Log.Out("[REBIRTH CompanionRecall] OWNER_TELEPORT_DETECTED playerEntity=" + player.entityId +
                    " pos=" + current + " jumpDistance=" + Mathf.Sqrt(jumpSqr).ToString("0.0"));
            }
            return;
        }

        if (!state.Pending) return;

        // A teleport can produce two or more large/streaming corrections. Keep moving the
        // pending destination until the player remains near one point for the settle window.
        if ((current - state.PendingPosition).sqrMagnitude > SettleDistance * SettleDistance)
        {
            state.PendingPosition = current;
            state.SettleAt = Time.realtimeSinceStartup + SettleSeconds;
            return;
        }

        if (Time.realtimeSinceStartup < state.SettleAt) return;

        state.Pending = false;
        if (player.IsDead()) return;

        if (RebirthCompanionRecallDebug.TraceEnabled)
        {
            Log.Out("[REBIRTH CompanionRecall] OWNER_TELEPORT_SETTLED playerEntity=" + player.entityId +
                " pos=" + current + " autoRecall=FollowOnly");
        }
        RebirthCompanionService.RecallFollowingAfterOwnerTeleport(player);
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) States.Clear();
    }
}

[HarmonyLib.HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.Teleport))]
internal static class RebirthCompanionOwnerExplicitTeleportPatch
{
    private static void Postfix(EntityPlayer __instance)
    {
        RebirthCompanionOwnerTeleportService.NotifyExplicitTeleport(__instance);
    }
}

[HarmonyLib.HarmonyPatch(typeof(EntityPlayer), nameof(EntityPlayer.OnUpdateEntity))]
internal static class RebirthCompanionOwnerTeleportPlayerUpdatePatch
{
    private static void Postfix(EntityPlayer __instance)
    {
        RebirthCompanionOwnerTeleportService.Tick(__instance);
    }
}
