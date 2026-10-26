using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Natural, bounded occurrence of a human survivor group (B02). The installed
// biome spawner (spawning.xml / entitygroups.xml) has no rare per-row or
// world-cap source for humans, so REBIRTH owns a server-only slot driver:
// one deterministic roll per world-time slot, a world cap on live unowned
// leaders, spacing from players/other leaders, and the journaled
// RebirthNpcFreshGroupSpawn path. The leader is the only thing spawned here;
// RebirthHumanGroupRoles.Tick composes followers from the held leader.
// Disabled unless the leader class sets NaturalGroupEnabled=true.
internal static class RebirthP4NaturalGroupOccurrence
{
    private const string LeaderMan = "npcRebirthGroupLeaderManSDCS";
    private const string LeaderWoman = "npcRebirthGroupLeaderWomanSDCS";
    private const ulong SlotTicks = 6000UL;      // 6 game hours
    private const float CheckSeconds = 20f;
    private const int MaxPlacementTries = 4;
    private const int ResumePerCheck = 2;
    private const string KeyPrefix = "naturalgroup-";
    private static int resumeCursor;
    private static float nextStuckLog;
    private const float StuckLogSeconds = 600f;
    private const double StuckPendingHours = 24.0;
    private static readonly List<RebirthNpcStableId> completedIds = new List<RebirthNpcStableId>();
    private static readonly List<string> pendingIds = new List<string>();

    private static World boundWorld;
    private static float nextCheck;
    private static ulong rolledSlot = ulong.MaxValue;
    private static int tries;
    private static float leaderRefresh;
    private const float LeaderRefreshSeconds = 120f;
    private static readonly List<Vector3> leaderPositions = new List<Vector3>();

    private struct Policy
    {
        public bool Enabled; public int Cap; public float Chance, MinPlayer, MaxPlayer, Spacing;
    }

    internal static void Tick(World world)
    {
        if(world == null || world.IsRemote() || !ThreadManager.IsMainThread() ||
           RebirthNpcPersistenceCoordinator.IsRecoveryInstalling ||
           RebirthNpcPersistenceTimelineService.IsAlignmentFailed || Time.unscaledTime < nextCheck) return;
        if(!ReferenceEquals(boundWorld, world)) { boundWorld = world; rolledSlot = ulong.MaxValue; tries = 0; leaderRefresh = 0f; }
        nextCheck = Time.unscaledTime + CheckSeconds;
        if(!TryPolicy(out var policy) || !policy.Enabled || world.Players == null || world.Players.list.Count == 0) return;

        ulong slot = world.worldTime / SlotTicks;
        // Every pending natural request counts toward the cap, however old: a request in an unloaded
        // chunk stays in the journal (never cancelled: it may have entered the original). Resume at most
        // ResumePerCheck per check, rotating, so recovery is bounded. Identity is the deterministic key.
        pendingIds.Clear();
        if(!RebirthNpcWorldIntegrationService.TryListPendingReplayIds(KeyPrefix, pendingIds)) return;
        int inFlight = pendingIds.Count;
        WarnStuckPending();
        for(int i = 0; i < pendingIds.Count && i < ResumePerCheck; i++)
        {
            string key = pendingIds[(resumeCursor + i) % pendingIds.Count];
            if(!RebirthNpcWorldIntegrationService.TryGetPreparedRequest(key, out var pending)) continue;
            RebirthNpcFreshGroupSpawn.TryMember(world, key, pending.EntityClass,
                new Vector3(pending.X, pending.Y, pending.Z), out _, out _, pending.Yaw);
        }
        if(pendingIds.Count > 0) resumeCursor = (resumeCursor + ResumePerCheck) % pendingIds.Count;
        if(rolledSlot == slot && tries >= MaxPlacementTries) return;
        if(RebirthNpcWorldIntegrationService.TryGetCompletedBinding(Key(slot), out _) ||
           RebirthNpcWorldIntegrationService.TryGetPreparedRequest(Key(slot), out _)) return;
        if(rolledSlot != slot) { rolledSlot = slot; tries = 0; if(Roll(slot, "chance") >= policy.Chance) { tries = MaxPlacementTries; return; } }

        // Never start a natural group on a blood-moon day.
        if(RebirthBossEventEligibility.IsBloodMoonDay(world)) { tries = MaxPlacementTries; return; }
        if(!RefreshLeaders(world)) return; // journal not current primary: fail closed
        if(leaderPositions.Count + inFlight >= policy.Cap) { tries = MaxPlacementTries; return; }
        var players = world.Players.list;
        // Pick the hashed anchor, or the next live player; no live anchor consumes no try.
        EntityPlayer anchor = null;
        int first = (int)(Hash(slot, "player") % (uint)players.Count);
        for(int p = 0; p < players.Count && anchor == null; p++)
        {
            var candidate = players[(first + p) % players.Count];
            if(candidate != null && !candidate.IsDead() && ReferenceEquals(world.GetEntity(candidate.entityId), candidate)) anchor = candidate;
        }
        if(anchor == null) return;
        tries++; // only real placement attempts consume a try
        double angle = (Hash(slot, "angle" + tries) % 3600u) / 3600.0 * Math.PI * 2.0;
        float distance = policy.MinPlayer + (policy.MaxPlayer - policy.MinPlayer) * ((Hash(slot, "dist" + tries) % 1000u) / 1000f);
        float x = anchor.position.x + (float)Math.Cos(angle) * distance;
        float z = anchor.position.z + (float)Math.Sin(angle) * distance;
        int bx = Utils.Fastfloor(x), bz = Utils.Fastfloor(z);
        // Loaded chunks only; never loads terrain. Placement is revalidated by TryPrepareSpawn.
        if(!(world.GetChunkFromWorldPos(bx, bz) is Chunk chunk) || !world.IsInPlayfield(chunk)) return;
        var position = new Vector3(bx + .5f, chunk.GetHeight(World.toBlockXZ(bx), World.toBlockXZ(bz)) + 1f, bz + .5f);
        // Heightmap top can be a roof: keep out of any prefab/POI footprint. Trees are not detectable
        // cheaply; CanMobsSpawnAtPos in TryPrepareSpawn is the remaining guard.
        if(GameManager.Instance.GetDynamicPrefabDecorator()?.GetPrefabAtPosition(position) != null) return;
        float minPlayerSq = policy.MinPlayer * policy.MinPlayer, spacingSq = policy.Spacing * policy.Spacing;
        foreach(var player in players)
            if(player != null && (player.position - position).sqrMagnitude < minPlayerSq) return;
        foreach(var other in leaderPositions)
            if((other - position).sqrMagnitude < spacingSq) return;

        string leaderClass = (Hash(slot, "sex") & 1u) == 0 ? LeaderMan : LeaderWoman;
        float yaw = Hash(slot, "yaw") % 360u;
        if(RebirthNpcFreshGroupSpawn.TryMember(world, Key(slot), leaderClass, position, out _, out _, yaw))
            tries = MaxPlacementTries;
        leaderRefresh = 0f; // recount next time: a new leader now exists
    }

    private static string Key(ulong slot) => KeyPrefix + slot;

    private static bool TryPolicy(out Policy policy)
    {
        policy = default(Policy);
        if(EntityClass.list == null) return false;
        int id = EntityClass.FromString(LeaderMan);
        if(!EntityClass.list.TryGetValue(id, out var definition) || definition?.Properties == null) return false;
        var p = definition.Properties;
        policy.Enabled = p.GetBool("NaturalGroupEnabled");
        policy.Cap = Mathf.Clamp(p.GetInt("NaturalGroupWorldCap"), 0, 8);
        policy.Chance = Mathf.Clamp01(p.GetFloat("NaturalGroupSlotChance"));
        policy.MinPlayer = Mathf.Max(32f, p.GetFloat("NaturalGroupMinPlayerDistance"));
        policy.MaxPlayer = Mathf.Max(policy.MinPlayer + 1f, p.GetFloat("NaturalGroupMaxPlayerDistance"));
        policy.Spacing = Mathf.Max(0f, p.GetFloat("NaturalGroupMinLeaderSpacing"));
        return policy.Cap > 0 && policy.Chance > 0f;
    }

    // Counts unowned, alive leaders from persisted read views (covers unloaded chunks), completed
    // natural bindings, and loaded bootstrap leaders. Cached for LeaderRefreshSeconds, stamped only
    // after a COMPLETE count. Returns false (caller admits nothing) if the store/journal is not
    // readable; hired, dead, tombstoned or removed leaders drop out on the next refresh.
    private static bool RefreshLeaders(World world)
    {
        if(Time.unscaledTime < leaderRefresh) return true;
        RebirthNpcAggregatePersistenceStore.EnsureLoaded();
        if(!RebirthNpcAggregatePersistenceStore.IsRecoveryBackingLoaded) return false;
        var positions = new List<Vector3>();
        var seen = new HashSet<RebirthNpcStableId>();
        foreach(var view in RebirthNpcAggregatePersistenceStore.SnapshotViews())
        {
            if(view?.UnknownOptionalElements == null || !Counts(view)) continue;
            foreach(var raw in view.UnknownOptionalElements)
            {
                if(raw == null || !raw.StartsWith("<humanGroup", StringComparison.Ordinal) ||
                   !IsLeaderNode(raw)) continue;
                if(seen.Add(view.Identity.StableNpcId)) positions.Add(view.Transform.WorldPosition);
                break;
            }
        }
        // Completed natural leaders whose followers are not composed yet have no humanGroup node;
        // the completion journal still identifies them, whether or not their chunk is loaded.
        completedIds.Clear();
        if(!RebirthNpcWorldIntegrationService.TryListCompletedStables(KeyPrefix, completedIds)) return false;
        foreach(var id in completedIds)
        {
            if(seen.Contains(id)) continue;
            // A completed id with no record is a dead/removed leader. An unreadable store was ruled out above.
            if(!RebirthNpcAggregatePersistenceStore.TryGetView(id, out var view) || !Counts(view)) continue;
            seen.Add(id); positions.Add(view.Transform.WorldPosition);
        }
        var snapshot = RebirthNpcRuntimeRegistry.GetSnapshot();
        for(int i = 0; i < snapshot.Length; i++)
        {
            var runtime = snapshot[i];
            if(runtime == null || runtime.OwnershipKind != RebirthNpcOwnershipKind.None || seen.Contains(runtime.StableId) ||
               !RebirthNpcRuntimeRegistry.TryGetEntityId(runtime.StableId, out var entityId) ||
               !(world.GetEntity(entityId) is EntityRebirthHumanoidNPC npc) || npc.IsDead() ||
               EntityClass.list == null || !EntityClass.list.TryGetValue(npc.entityClass, out var cls) ||
               cls.entityClassName != LeaderMan && cls.entityClassName != LeaderWoman) continue;
            seen.Add(runtime.StableId);
            positions.Add(npc.position);
        }
        leaderPositions.Clear(); leaderPositions.AddRange(positions);
        leaderRefresh = Time.unscaledTime + LeaderRefreshSeconds;
        return true;
    }

    // Parses only <humanGroup> candidates (the element RebirthHumanGroupRoles writes) instead of matching text.
    private static bool IsLeaderNode(string raw)
    {
        try { return (string)System.Xml.Linq.XElement.Parse(raw).Attribute("role") == "leader"; }
        catch(System.Xml.XmlException) { return false; }
    }

    // Unowned, not tombstoned, not awaiting respawn.
    private static bool Counts(RebirthNpcPersistentRecordView view) =>
        view.Identity != null && view.Transform != null &&
        string.IsNullOrEmpty(view.Ownership?.OwnerPlatformIdOrPersistentPlayerId) &&
        view.Lifecycle?.TombstoneState != true && view.Vitals?.DeathOrIncapacitationState != "awaiting-respawn" &&
        view.Vitals?.DeathOrIncapacitationState != "dead"; // human death keeps the record with "dead" (capture writes it; only dogs are removed)

    // Pending requests are never cancelled (a native original may have been entered), so a stuck one
    // permanently uses a cap slot. Make that visible, rate limited.
    private static void WarnStuckPending()
    {
        if(Time.unscaledTime < nextStuckLog || pendingIds.Count == 0) return;
        nextStuckLog = Time.unscaledTime + StuckLogSeconds;
        string oldestKey = null; long oldest = long.MaxValue;
        foreach(var key in pendingIds)
            if(RebirthNpcWorldIntegrationService.TryGetPreparedRequest(key, out var r) && r.CreatedTicks < oldest)
            { oldest = r.CreatedTicks; oldestKey = key; }
        if(oldestKey == null) return;
        var age = TimeSpan.FromTicks(Math.Max(0L, DateTime.UtcNow.Ticks - oldest));
        if(age.TotalHours >= StuckPendingHours)
            Log.Warning("[REBIRTH P4] natural group pending request '" + oldestKey + "' is " + (int)age.TotalHours +
                " h old and still counts toward NaturalGroupWorldCap (" + pendingIds.Count + " pending); it is never cancelled.");
    }

    private static uint Hash(ulong slot, string salt)
    {
        using(var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(GameIO.GetSaveGameDir() + "|naturalgroup|" + slot + "|" + salt));
            return BitConverter.ToUInt32(bytes, 0);
        }
    }

    private static float Roll(ulong slot, string salt) => (Hash(slot, salt) % 10000u) / 10000f;
}
