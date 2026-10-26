using System;

// B10: native access rules for an NPC-led container search. Mirrors the checks in
// TEFeatureStorage.OnBlockActivated "Search" (lockFeature.IsLocked/IsUserAllowed,
// isJammed, lockpickFeature) without a player. The NPC acts for its owner, so the
// owner's platform identity is the one tested against the native allow list.
internal enum RebirthP11NpcLootAccessResult : byte
{
    Open, Jammed, QuestLoot, LockedOwnerDenied, NeedsLockpick, NativeLockHeld, Unavailable
}

internal enum RebirthP11LockpickStep : byte { Gated, Held, Waiting, Broke, Unlocked }

internal static class RebirthP11NpcLootAccess
{
    private sealed class Attempt { internal Guid Id; internal float ReadyAt, BreakChance; }
    private static readonly System.Collections.Generic.Dictionary<string, Attempt> attempts =
        new System.Collections.Generic.Dictionary<string, Attempt>();

    // NeedsLockpick handling (gate NpcLockpickQualified). Consumption goes through the guarded G03
    // custody (write-ahead intent, decided outcome, single decrement, restart Recover); this only
    // paces the attempt by the NPC-skill-scaled pick time and applies the native success effect
    // (TEFeatureLockPickable.DowngradeToUnlockedVariant). The attempt timer is memory-only: after a
    // restart a pending ledger row is settled by Recover and a fresh attempt begins. Simplification
    // versus the player mini-game (nthPercentile / CalculateLockpicksNeeded): each attempt is one
    // timed roll against the scaled break chance; a break consumes one pick and retries.
    internal static RebirthP11LockpickStep TryLockpick(EntityRebirthHumanoidNPC npc, TEFeatureStorage source)
    {
        try
        {
            if (!RebirthP11SmartLootGate.NpcLockpickQualified) return RebirthP11LockpickStep.Gated;
            var picker = source?.lockpickFeature;
            if (npc?.RebirthRuntimeState == null || picker == null || !picker.NeedsLockpicking())
                return RebirthP11LockpickStep.Held;
            Vector3i position = source.ToWorldPos();
            string key = npc.RebirthRuntimeState.StableId + "|" + position;
            float now = UnityEngine.Time.time;
            if (!attempts.TryGetValue(key, out var attempt))
            {
                if (RebirthG03NpcLockpickCustody.HasPending(npc))
                { RebirthG03NpcLockpickCustody.Recover(npc); return RebirthP11LockpickStep.Held; }
                if (RebirthG03NpcLockpickCustody.TryBegin(npc, position, picker.lockPickTime,
                        picker.lockPickBreakChance, out var plan) != RebirthG03LockpickHold.None)
                    return RebirthP11LockpickStep.Held;
                if (attempts.Count >= 32) attempts.Clear();
                attempts[key] = new Attempt { Id = plan.Attempt, ReadyAt = now + plan.PickSeconds, BreakChance = plan.BreakChance };
                return RebirthP11LockpickStep.Waiting;
            }
            if (now < attempt.ReadyAt) return RebirthP11LockpickStep.Waiting;
            float roll = npc.rand.RandomRange(0f, 1f);
            if (RebirthG03NpcLockpickCustody.TryResolve(npc, attempt.Id, roll) != RebirthG03LockpickHold.None)
                return RebirthP11LockpickStep.Held;
            attempts.Remove(key);
            if (roll < attempt.BreakChance) return RebirthP11LockpickStep.Broke;
            picker.DowngradeToUnlockedVariant(position);
            RebirthP11LootAward.Lockpick(npc, attempt.Id);
            return RebirthP11LockpickStep.Unlocked;
        }
        catch { return RebirthP11LockpickStep.Held; }
    }

    // The NPC cannot hold a LockManager lock (LockRequestServer resolves a player
    // connection), so an NPC step is admitted only when no native lock or UI user
    // exists and is completed on the same main-thread step.
    internal static bool NoNativeLock(TEFeatureStorage source)
    {
        try
        {
            var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            return source != null && connection != null && connection.IsServer &&
                !LockManager.Instance.IsLockedServer(source) &&
                !((source.Parent as TileEntity)?.IsUserAccessing() ?? false);
        }
        catch { return false; }
    }

    internal static RebirthP11NpcLootAccessResult Classify(EntityPlayer owner, TEFeatureStorage source)
    {
        try
        {
            if (source == null || owner == null) return RebirthP11NpcLootAccessResult.Unavailable;
            if (source.isQuestLoot) return RebirthP11NpcLootAccessResult.QuestLoot;
            if (source.isJammed) return RebirthP11NpcLootAccessResult.Jammed;
            if (!NoNativeLock(source)) return RebirthP11NpcLootAccessResult.NativeLockHeld;
            var lockable = source.lockFeature;
            if (lockable != null && lockable.IsLocked())
            {
                var id = GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(owner.entityId)?.PrimaryId;
                if (id != null && lockable.IsUserAllowed(id)) return RebirthP11NpcLootAccessResult.Open;
                return source.lockpickFeature != null && source.lockpickFeature.NeedsLockpicking()
                    ? RebirthP11NpcLootAccessResult.NeedsLockpick
                    : RebirthP11NpcLootAccessResult.LockedOwnerDenied;
            }
            if (source.lockpickFeature != null && source.lockpickFeature.NeedsLockpicking())
                return RebirthP11NpcLootAccessResult.NeedsLockpick;
            return RebirthP11NpcLootAccessResult.Open;
        }
        catch { return RebirthP11NpcLootAccessResult.Unavailable; }
    }
}
