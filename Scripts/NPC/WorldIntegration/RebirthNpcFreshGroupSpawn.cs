using System;
using System.Collections.Generic;
using UnityEngine;

// Opt-in group source only. Every follower has a deterministic replay key in
// the existing world-spawn journal before EntityFactory or native publication.
internal static class RebirthNpcFreshGroupSpawn
{
    private const string MaleFollowerClass = "npcRebirthGroupFollowerManSDCS";
    private const string FemaleFollowerClass = "npcRebirthGroupFollowerWomanSDCS";

    internal static bool TryComposeFollowers(RebirthNpcStableId leader, IList<Vector3> positions,
        out RebirthNpcStableId[] followers, out string reason)
    {
        followers = null; reason = "Group spawn is unavailable.";
        var world = GameManager.Instance?.World;
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            RebirthNpcPersistenceCoordinator.IsRecoveryInstalling || leader.IsEmpty ||
            positions == null || positions.Count < 1 || positions.Count > 4 ||
            !RebirthNpcAggregatePersistenceStore.TryGet(leader, out var leaderPerson) ||
            leaderPerson.NativeReconstruction == null ||
            leaderPerson.Profile?.ProfileId != "survivor.persistent" ||
            !RebirthNpcRuntimeRegistry.TryGetEntityId(leader, out var leaderId) ||
            !(world.GetEntity(leaderId) is EntityRebirthHumanoidNPC leaderNpc) ||
            EntityClass.list == null || !EntityClass.list.TryGetValue(leaderNpc.entityClass, out var leaderClass) ||
            (leaderClass.entityClassName != "npcRebirthGroupLeaderManSDCS" &&
             leaderClass.entityClassName != "npcRebirthGroupLeaderWomanSDCS")) return false;
        bool leaderFemale = leaderClass.entityClassName == "npcRebirthGroupLeaderWomanSDCS";
        var result = new RebirthNpcStableId[positions.Count];
        for (int i = 0; i < positions.Count; i++)
        {
            string replay = "group-" + leader + "-" + i;
            string followerClass = (i & 1) == 0 ?
                (leaderFemale ? FemaleFollowerClass : MaleFollowerClass) :
                (leaderFemale ? MaleFollowerClass : FemaleFollowerClass);
            if (!TryMember(world, replay, followerClass, positions[i], out result[i], out reason)) return false;
        }
        if (!RebirthHumanGroupRoles.TryEstablish(leader, result))
        { reason = "Group role transaction is pending reconciliation."; return false; }
        followers = result; reason = string.Empty;
        return true;
    }

    // Natural-occurrence leaders use the same journaled, resumable member path as followers.
    internal static bool TryMember(World world, string replay, string followerClass, Vector3 position,
        out RebirthNpcStableId stable, out string reason, float yaw = 0f)
    {
        stable = default(RebirthNpcStableId); reason = "Fresh group member requires reconciliation.";
        if (RebirthNpcWorldIntegrationService.TryGetCompletedBinding(replay, out var completed))
        {
            stable = completed.Stable;
            return RebirthNpcAggregatePersistenceStore.TryGet(stable, out var person) &&
                person.NativeReconstruction != null &&
                RebirthNpcRuntimeRegistry.TryGetEntityId(stable, out var nativeId) &&
                world.GetEntity(nativeId) is EntityRebirthHumanoidNPC;
        }
        if (!RebirthNpcWorldIntegrationService.TryGetPreparedRequest(replay, out var request))
        {
            if (!RebirthNpcWorldIntegrationService.TryPrepareSpawn(replay, "survivor.persistent",
                    followerClass, position, yaw, out request, out reason)) return false;
        }
        if (!RebirthNpcStableId.TryParse(request.StableId, out stable)) return false;
        Entity candidate = null;
        if (!request.IsAttempted)
        {
            if (!RebirthNpcWorldIntegrationService.TryConstructPreparedSpawn(request, out candidate, out reason)) return false;
        }
        else if (request.IsConstructed)
            candidate = world.GetEntity(request.NativeEntityId);
        // Attempted-but-not-constructed creation is uncertain. Never call the
        // factory again with this key or make a replacement identity.
        if (!(candidate is EntityRebirthHumanoidNPC npc) || !npc.IsFreshGroupBootstrapPending ||
            npc.RebirthRuntimeState?.StableId != stable) return false;
        if (world.GetEntity(candidate.entityId) == null &&
            !RebirthNpcWorldIntegrationService.TryPublishConstructedSpawn(replay, world, candidate, out reason)) return false;
        if (!ReferenceEquals(world.GetEntity(candidate.entityId), candidate)) return false;
        try
        {
            RebirthNpcAggregatePersistenceStore.CaptureRuntimeEnvelopes();
            RebirthNpcAggregatePersistenceStore.Save();
            if (!RebirthNpcAggregatePersistenceStore.TryGet(stable, out var person) ||
                person.NativeReconstruction == null ||
                !RebirthNpcWorldIntegrationService.TryCompletePublishedExistingPerson(replay, world, candidate, out reason))
                return false;
        }
        catch (Exception error)
        {
            reason = "Fresh group member checkpoint retained: " + error.GetType().Name;
            return false;
        }
        reason = string.Empty;
        return true;
    }
}
