using System;
using System.Collections.Generic;
using UnityEngine;

// B10 Follow-activity dispatcher: the only caller of RebirthP11FollowLootActivity.TryStep. It rides the
// same GameUpdate handler as the smart-loot recovery worker, is a no-op while FollowActivityQualified is
// false, and never starts base work, never changes Follow, and never moves the NPC (no navigation task:
// a container beyond ReachMeters is reached only through RebirthP6FollowApproach, which pauses Follow
// while its request is Moving and is closed unless its own gates are). Server, main thread, at most ONE NPC step per pass (PassSeconds).
// Priority: skipped while the NPC has an attack/revenge target (combat first) or the owner is farther than
// KeepUpMeters (the NPC is catching up). Container choice: the nearest loaded TEFeatureStorage in reach
// that is not on this NPC's cooldown list; a tile with nothing to take (empty, bag full, denied) is put
// on cooldown so it is not rescanned every pass. Entity containers (corpses) are handled by
// RebirthP11CorpseActivity (own journal and cooldown); both hooks are no-ops while CorpseSourceQualified is false.
internal static class RebirthP11FollowLootDispatcher
{
    private const float PassSeconds = 0.5f;
    private const float ReachMeters = 3f;
    private const float SearchFromOwnerMeters = RebirthP6FollowApproach.MaxFromOwnerMeters; // 10 m
    private const float SearchFromNpcMeters = RebirthP6FollowApproach.MaxFromNpcMeters;     // 16 m (approach bound)
    private const float ApproachPoll = 0.5f;
    private const float ApproachFailCooldown = 30f;
    private const float KeepUpMeters = 12f;
    private const float EmptyCooldown = 30f;
    private const float FullCooldown = 60f;
    private const float StepDelay = 0.25f;

    private sealed class Track
    {
        internal Vector3i Target; internal bool HasTarget; internal float NextAt;
        internal ulong ApproachId; internal bool ApproachCorpse; // D-25: only ids issued by this track are consumed
        internal readonly Dictionary<Vector3i, float> Cooldown = new Dictionary<Vector3i, float>();
    }

    private static readonly Dictionary<string, Track> tracks = new Dictionary<string, Track>();
    private static World trackWorld;
    private static float nextPass;
    private static int cursor;

    internal static void Tick()
    {
        if (!RebirthP11SmartLootGate.FollowActivityQualified) return;
        World world = GameManager.Instance?.World;
        if (!ReferenceEquals(world, trackWorld)) { trackWorld = world; tracks.Clear(); cursor = 0; nextPass = 0f; }
        if (!ThreadManager.IsMainThread() || world?.IsRemote() != false || world.Players?.list == null ||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling || RebirthNpcRuntimeRegistry.HasForeignDomain ||
            !RebirthSandboxOptionManager.Current.SmartLooting || Time.unscaledTime < nextPass) return;
        nextPass = Time.unscaledTime + PassSeconds;
        try
        {
            var states = RebirthNpcRuntimeRegistry.GetSnapshot();
            if (states.Length == 0) return;
            for (int n = 0; n < states.Length; n++)
            {
                var state = states[(cursor + n) % states.Length];
                if (state.Presence != RebirthNpcPresenceState.Active ||
                    state.OwnershipKind != RebirthNpcOwnershipKind.Player ||
                    !RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out int id) ||
                    world.GetEntity(id) is not EntityRebirthHumanoidNPC npc || npc.IsDead() ||
                    npc.IsPreparedRestorationPending || !RebirthP6WorkFollowState.IsFollowing(npc) ||
                    npc.RebirthRuntimeState == null) continue;
                var owner = FindOwner(world, state.OwnerId);
                if (owner == null || owner.IsDead() ||
                    (owner.position - npc.position).sqrMagnitude > KeepUpMeters * KeepUpMeters ||
                    npc.GetAttackTarget() != null || npc.GetRevengeTarget() != null)
                {
                    // Combat / keep-up outrank the loot walk: end a running approach; Follow resumes by itself.
                    RebirthP6FollowApproach.Cancel(npc, "Loot approach yielded to combat or keep-up.");
                    continue;
                }
                string key = npc.RebirthRuntimeState.StableId.ToString();
                if (!tracks.TryGetValue(key, out var track))
                {
                    if (tracks.Count >= 64) tracks.Clear();
                    tracks[key] = track = new Track();
                }
                if (Time.unscaledTime < track.NextAt) continue;
                cursor = (cursor + n + 1) % states.Length;
                Step(world, owner, npc, track);
                return;
            }
            cursor = (cursor + 1) % states.Length;
        }
        catch (Exception error)
        {
            Log.Warning("[REBIRTH Smart Looting] follow dispatch retained: " + error.Message);
        }
    }

    private static EntityPlayer FindOwner(World world, string ownerId)
    {
        foreach (var player in world.Players.list)
            if (player != null && RebirthStablePlayerIdentity.TryResolveServerEntity(player, out var identity) &&
                string.Equals(identity.CanonicalId, ownerId, StringComparison.OrdinalIgnoreCase)) return player;
        return null;
    }

    private static void Step(World world, EntityPlayer owner, EntityRebirthHumanoidNPC npc, Track track)
    {
        float now = Time.unscaledTime;
        // Corpse source (gate CorpseSourceQualified): a pending corpse leg is settled first.
        if (RebirthP11CorpseActivity.Settle(owner, npc, out float corpseDelay)) { track.NextAt = now + corpseDelay; return; }
        bool arrived = false;
        var phase = RebirthP6FollowApproach.Status(npc, out ulong approachId, out Vector3i approachBlock);
        if (phase != RebirthP6FollowApproach.Phase.None)
        {
            if (track.ApproachId == 0 || approachId != track.ApproachId)
            {
                // Not issued by this track: never adopt or consume it; wait while it runs.
                if (phase == RebirthP6FollowApproach.Phase.Moving) { track.NextAt = now + ApproachPoll; return; }
            }
            else if (phase == RebirthP6FollowApproach.Phase.Moving) { track.NextAt = now + ApproachPoll; return; }
            else
            {
                RebirthP6FollowApproach.Consume(npc, approachId);
                track.ApproachId = 0;
                track.Target = approachBlock;
                if (phase == RebirthP6FollowApproach.Phase.Failed)
                {
                    // A failed approach is gone (D-25); a later attempt needs a new request and a new id.
                    track.ApproachCorpse = false;
                    Park(track, now + ApproachFailCooldown); return;
                }
                arrived = true; track.HasTarget = true;
            }
        }
        TEFeatureStorage source = track.HasTarget ? StorageAt(world, track.Target) : null;
        if (arrived && source == null)
        {
            if (track.ApproachCorpse) { track.ApproachCorpse = false; track.HasTarget = false; track.NextAt = now + StepDelay; return; } // corpse leg runs next pass
            Park(track, now + ApproachFailCooldown); return;
        }
        track.ApproachCorpse = false;
        if (source == null || (!InReach(npc, source) && !InSearch(owner, npc, source)))
        { track.HasTarget = false; source = Choose(world, owner, npc, track, now); }
        if (source == null)
        {
            // No tile available: a loot entity (corpse / backpack) in reach, then one beyond reach, is next.
            if (RebirthP11CorpseActivity.TryDispatch(world, owner, npc, out float corpseNext)) { track.NextAt = now + corpseNext; return; }
            if (TryApproachCorpse(world, owner, npc, track, now)) return;
            track.NextAt = now + 1f; return;
        }
        track.Target = source.ToWorldPos(); track.HasTarget = true;
        if (!InReach(npc, source))
        {
            if (arrived) { Park(track, now + ApproachFailCooldown); return; } // arrived yet still out of reach
            RequestApproach(owner, npc, track, now, false); return;
        }

        switch (RebirthP11FollowLootActivity.TryStep(owner, npc, source, out _))
        {
            case RebirthP11FollowLootActivity.StepResult.Opened:
            case RebirthP11FollowLootActivity.StepResult.LegPending:
            case RebirthP11FollowLootActivity.StepResult.LegCompleted:
            case RebirthP11FollowLootActivity.StepResult.Lockpicking:
            case RebirthP11FollowLootActivity.StepResult.Unlocked:
                track.NextAt = now + StepDelay; break;
            case RebirthP11FollowLootActivity.StepResult.Searching:
                track.NextAt = now + 0.5f; break;
            case RebirthP11FollowLootActivity.StepResult.BagFull:
                // The remainder stays in the source; no other container receives it.
                Park(track, now + FullCooldown); break;
            case RebirthP11FollowLootActivity.StepResult.NothingToLoot:
            case RebirthP11FollowLootActivity.StepResult.Denied:
                Park(track, now + EmptyCooldown); break;
            default: // Gated, NotFollowing
                track.HasTarget = false; track.NextAt = now + 1f; break;
        }
    }

    // Issues a NEW approach request (new operation id). A refusal parks the tile like a failure.
    private static void RequestApproach(EntityPlayer owner, EntityRebirthHumanoidNPC npc, Track track, float now, bool corpse)
    {
        ulong id = RebirthP6FollowApproach.TryRequest(owner, npc, track.Target, out _);
        if (id == 0) { Park(track, now + ApproachFailCooldown); return; }
        track.ApproachId = id; track.ApproachCorpse = corpse;
        track.NextAt = now + ApproachPoll;
    }

    // Corpse beyond ReachMeters (within the approach bounds). Looting itself stays in RebirthP11CorpseActivity;
    // this only walks the NPC next to the corpse block. No-op while CorpseSourceQualified is false.
    private static bool TryApproachCorpse(World world, EntityPlayer owner, EntityRebirthHumanoidNPC npc, Track track, float now)
    {
        if (!RebirthP11SmartLootGate.CorpseSourceQualified) return false;
        var found = new List<Entity>();
        float side = SearchFromOwnerMeters * 2f;
        world.GetEntitiesInBounds(typeof(EntityLootContainer), new Bounds(owner.position, new Vector3(side, side, side)), found);
        EntityLootContainer best = null;
        float bestDistance = SearchFromNpcMeters * SearchFromNpcMeters;
        Vector3i bestBlock = default;
        for (int i = 0; i < found.Count; i++)
        {
            if (found[i] is not EntityLootContainer corpse) continue;
            float d = (npc.position - corpse.position).sqrMagnitude;
            if (d <= ReachMeters * ReachMeters || d > bestDistance ||
                (owner.position - corpse.position).sqrMagnitude > SearchFromOwnerMeters * SearchFromOwnerMeters) continue;
            var block = new Vector3i(Mathf.FloorToInt(corpse.position.x), Mathf.FloorToInt(corpse.position.y), Mathf.FloorToInt(corpse.position.z));
            if (track.Cooldown.TryGetValue(block, out float until))
            {
                if (now < until) continue;
                track.Cooldown.Remove(block);
            }
            if (!RebirthP11CorpseSource.NoNativeLock(corpse) ||
                !RebirthP11CorpseSource.TryResolve(world, RebirthP11CorpseSource.Key(corpse), out var live) || !ReferenceEquals(live, corpse)) continue;
            bestDistance = d; best = corpse; bestBlock = block;
        }
        found.Clear();
        if (best == null) return false;
        track.Target = bestBlock; track.HasTarget = true;
        RequestApproach(owner, npc, track, now, true);
        return true;
    }

    private static bool InSearch(EntityPlayer owner, EntityRebirthHumanoidNPC npc, TEFeatureStorage source)
    {
        Vector3 c = source.ToWorldCenterPos();
        return (owner.position - c).sqrMagnitude <= SearchFromOwnerMeters * SearchFromOwnerMeters &&
               (npc.position - c).sqrMagnitude <= SearchFromNpcMeters * SearchFromNpcMeters;
    }

    private static void Park(Track track, float until)
    {
        if (track.Cooldown.Count >= 32) track.Cooldown.Clear();
        track.Cooldown[track.Target] = until;
        track.HasTarget = false;
        track.NextAt = Time.unscaledTime + StepDelay;
    }

    private static bool InReach(EntityRebirthHumanoidNPC npc, TEFeatureStorage source) =>
        (npc.position - source.ToWorldCenterPos()).sqrMagnitude <= ReachMeters * ReachMeters;

    private static TEFeatureStorage StorageAt(World world, Vector3i position) =>
        (world.GetTileEntity(position) as TileEntityComposite)?.GetFeature<TEFeatureStorage>();

    // Nearest loaded storage tile within reach, from the (at most four) chunks the reach square touches.
    private static TEFeatureStorage Choose(World world, EntityPlayer owner, EntityRebirthHumanoidNPC npc, Track track, float now)
    {
        Vector3 p = npc.position;
        Vector3 o = owner.position;
        int r = Mathf.CeilToInt(SearchFromOwnerMeters);
        int minX = World.toChunkXZ(Mathf.FloorToInt(o.x) - r), maxX = World.toChunkXZ(Mathf.FloorToInt(o.x) + r);
        int minZ = World.toChunkXZ(Mathf.FloorToInt(o.z) - r), maxZ = World.toChunkXZ(Mathf.FloorToInt(o.z) + r);
        TEFeatureStorage best = null;
        float bestDistance = SearchFromNpcMeters * SearchFromNpcMeters;
        for (int cx = minX; cx <= maxX; cx++)
            for (int cz = minZ; cz <= maxZ; cz++)
            {
                Chunk chunk = world.GetChunkSync(cx, cz) as Chunk;
                if (chunk == null || chunk.InProgressUnloading) continue;
                var tiles = chunk.GetTileEntities().list;
                for (int i = 0; i < tiles.Count; i++)
                {
                    if (tiles[i] is not TileEntityComposite composite) continue;
                    var storage = composite.GetFeature<TEFeatureStorage>();
                    if (storage == null) continue;
                    Vector3i position = storage.ToWorldPos();
                    if (track.Cooldown.TryGetValue(position, out float until))
                    {
                        if (now < until) continue;
                        track.Cooldown.Remove(position);
                    }
                    Vector3 center = storage.ToWorldCenterPos();
                    if ((o - center).sqrMagnitude > SearchFromOwnerMeters * SearchFromOwnerMeters) continue;
                    float d = (p - center).sqrMagnitude;
                    if (d > bestDistance) continue;
                    bestDistance = d; best = storage;
                }
            }
        return best;
    }
}
