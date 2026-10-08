using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

/// <summary>
/// Server-side chunk-observer lifecycle for owned dogs that are intentionally left
/// behind on Stay or Guard. This restores the 2.6 behavior without its global polling
/// coroutine: observers are created/removed on lifecycle/order transitions and owner load.
/// </summary>
public static class RebirthDogChunkObserverService
{
    private const int ObserverViewDimension = 3;

    private sealed class ObserverEntry
    {
        public RebirthNpcStableId StableId;
        public string OwnerId = string.Empty;
        public ChunkManager.ChunkObserver Observer;
        public Vector3 Position;
        public bool FollowRecoveryRequested;
        public bool ManualRecallAllOrders;
    }

    private static readonly object Sync = new object();
    private static readonly Dictionary<RebirthNpcStableId, ObserverEntry> Observers =
        new Dictionary<RebirthNpcStableId, ObserverEntry>();

    public static void Synchronize(EntityRebirthDogCompanion dog)
    {
        if (!IsServer() || dog == null || dog.RebirthRuntimeState == null)
            return;

        RebirthNpcStableId stableId = dog.RebirthRuntimeState.StableId;
        if (stableId.IsEmpty)
            return;

        RebirthNpcPersistentRecordView record;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record))
        {
            Release(stableId);
            return;
        }

        if (IsFollowRecoveryRequested(stableId))
        {
            string recoveryOwnerId = ResolveOwnerId(record);
            EntityPlayer recoveryOwner = dog.world != null
                ? RebirthDogRuntimeService.ResolveOwnerPublic(dog.world, recoveryOwnerId)
                : null;
            RebirthNpcOrderState recoveryOrder;
            bool manualAllOrders = IsManualRecallAllOrders(stableId);
            bool validRecovery = record.Dog != null &&
                record.Dog.Lifecycle == RebirthDogLifecycleKind.Active &&
                record.Order != null &&
                Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out recoveryOrder) &&
                (manualAllOrders || recoveryOrder == RebirthNpcOrderState.Follow) &&
                recoveryOwner != null;

            if (!validRecovery)
            {
                Release(stableId);
                return;
            }

            // Keep the source observer until BOTH sides of recovery are complete: the
            // original dog is live and the destination chunk is ready for an immediate
            // native chunk re-home. Never release merely because SetPosition accepted a
            // logical coordinate; doing so is what allowed v281 ghost companions.
            bool completed = manualAllOrders
                ? RebirthDogRuntimeService.TryCompleteManualRecallRecovery(dog, recoveryOwner)
                : RebirthDogRuntimeService.TryCompleteFollowRecovery(dog, recoveryOwner);
            if (completed)
            {
                Release(stableId);
                return;
            }

            // Destination is still streaming. The source observer is deliberately retained
            // and the normal dog maintenance tick will retry without changing persistence.
            return;
        }

        if (!ShouldObserve(record))
        {
            Release(stableId);
            return;
        }

        string ownerId = ResolveOwnerId(record);
        // 2.6 held the observer only while the owner was actually spawned. Do the same
        // without a global polling coroutine: a loaded dog's normal one-second maintenance
        // releases its observer shortly after the owner leaves, and PlayerSpawned restores it.
        if (dog.world == null || RebirthDogRuntimeService.ResolveOwnerPublic(dog.world, ownerId) == null)
        {
            Release(stableId);
            return;
        }

        Ensure(stableId, ownerId, ResolveObserverPosition(record, dog.position));
    }

    /// <summary>
    /// Called when an owning player enters the world. This is the important unloaded-chunk
    /// path: a persisted Stay/Guard dog gets an observer at its saved hold position before
    /// the dog entity itself necessarily exists in World.Entities.
    /// </summary>
    public static void RefreshForPlayer(EntityPlayer player)
    {
        if (!IsServer() || player == null)
            return;

        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(player, out ownerId) ||
            string.IsNullOrEmpty(ownerId))
            return;

        RebirthNpcPersistentRecordView[] records = RebirthNpcAggregatePersistenceStore.SnapshotViews();
        for (int i = 0; i < records.Length; i++)
        {
            RebirthNpcPersistentRecordView record = records[i];
            if (record == null || record.Identity == null || record.Profile == null || record.Ownership == null ||
                !string.Equals(record.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty, ownerId, StringComparison.OrdinalIgnoreCase))
                continue;

            RebirthNpcStableId stableId = record.Identity.StableNpcId;
            if (stableId.IsEmpty)
                continue;

            if (ShouldObserve(record))
                Ensure(stableId, ownerId, ResolveObserverPosition(record, player.position));
            else
                Release(stableId);
        }
    }

    /// <summary>
    /// Queues recovery for a dog that is already live. The observer is anchored to the
    /// dog's CURRENT NATIVE CHUNK rather than its logical transform. That distinction is
    /// important when repairing a v281-style mismatch where SetPosition changed the logical
    /// coordinate but chunkPosAddedEntityTo still identifies the old physical chunk.
    /// </summary>
    public static bool RequestLiveFollowRecovery(EntityRebirthDogCompanion dog, EntityPlayer owner)
    {
        if (!IsServer() || dog == null || owner == null || dog.RebirthRuntimeState == null)
            return false;

        RebirthNpcStableId stableId = dog.RebirthRuntimeState.StableId;
        if (stableId.IsEmpty || !RebirthDogLifecycleService.IsOwnedBy(dog, owner))
            return false;

        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(owner, out ownerId) || string.IsNullOrEmpty(ownerId))
            return false;

        RebirthNpcPersistentRecordView record;
        RebirthNpcOrderState order;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record) ||
            record == null || record.Dog == null || record.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
            record.Order == null || !Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order) ||
            order != RebirthNpcOrderState.Follow ||
            !string.Equals(ResolveOwnerId(record), ownerId, StringComparison.OrdinalIgnoreCase))
            return false;

        Ensure(stableId, ownerId, ResolvePhysicalObserverPosition(dog), true);
        return true;
    }

    /// <summary>
    /// Manual mass-recall variant. It intentionally accepts Stay/Guard as well as Follow,
    /// keeping the source native chunk alive until the original entity can be re-homed.
    /// </summary>
    public static bool RequestLiveRecallRecovery(EntityRebirthDogCompanion dog, EntityPlayer owner)
    {
        if (!IsServer() || dog == null || owner == null || dog.RebirthRuntimeState == null)
            return false;

        RebirthNpcStableId stableId = dog.RebirthRuntimeState.StableId;
        if (stableId.IsEmpty || !RebirthDogLifecycleService.IsOwnedBy(dog, owner)) return false;

        string ownerId;
        if (!RebirthDogLifecycleService.TryResolveOwnerId(owner, out ownerId) || string.IsNullOrEmpty(ownerId))
            return false;

        RebirthNpcPersistentRecordView record;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record) || record == null ||
            record.Dog == null || record.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
            !string.Equals(ResolveOwnerId(record), ownerId, StringComparison.OrdinalIgnoreCase))
            return false;

        Ensure(stableId, ownerId, ResolvePhysicalObserverPosition(dog), true, true);
        return true;
    }

    /// <summary>
    /// Manual mass-recall path for a dog that is already unloaded. Observe its persisted
    /// physical chunk, materialize the original StableId, then recall it exactly to owner.
    /// </summary>
    public static bool RequestRecallRecovery(RebirthNpcStableId stableId, string ownerId, Vector3 position)
    {
        if (!IsServer() || stableId.IsEmpty || string.IsNullOrEmpty(ownerId)) return false;

        RebirthNpcPersistentRecordView record;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record) || record == null ||
            record.Dog == null || record.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
            !string.Equals(ResolveOwnerId(record), ownerId, StringComparison.OrdinalIgnoreCase))
            return false;

        Ensure(stableId, ownerId, ResolvePersistedPhysicalObserverPosition(record, position), true, true);
        return true;
    }

    /// <summary>
    /// Temporarily keeps the saved chunk for an unloaded Follow dog alive so the original
    /// entity can materialize and be recalled. This avoids spawning a duplicate stable dog.
    /// </summary>
    public static bool RequestFollowRecovery(RebirthNpcStableId stableId, string ownerId, Vector3 position)
    {
        if (!IsServer() || stableId.IsEmpty || string.IsNullOrEmpty(ownerId))
            return false;

        RebirthNpcPersistentRecordView record;
        RebirthNpcOrderState order;
        if (!RebirthNpcAggregatePersistenceStore.TryGetView(stableId, out record) ||
            record == null || record.Dog == null || record.Dog.Lifecycle != RebirthDogLifecycleKind.Active ||
            record.Order == null || !Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order) ||
            order != RebirthNpcOrderState.Follow ||
            !string.Equals(ResolveOwnerId(record), ownerId, StringComparison.OrdinalIgnoreCase))
            return false;

        Ensure(stableId, ownerId, ResolvePersistedPhysicalObserverPosition(record, position), true);
        return true;
    }

    public static void Release(RebirthNpcStableId stableId)
    {
        if (stableId.IsEmpty)
            return;

        ObserverEntry entry = null;
        lock (Sync)
        {
            if (Observers.TryGetValue(stableId, out entry))
                Observers.Remove(stableId);
        }

        if (entry == null || entry.Observer == null || GameManager.Instance == null)
            return;

        try
        {
            GameManager.Instance.RemoveChunkObserver(entry.Observer);
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Dog] failed to remove chunk observer stableId=" + stableId +
                " reason=" + ex.Message);
        }
    }

    public static void ResetForWorldChange()
    {
        ObserverEntry[] entries;
        lock (Sync)
        {
            entries = new ObserverEntry[Observers.Count];
            Observers.Values.CopyTo(entries, 0);
            Observers.Clear();
        }

        if (GameManager.Instance == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            ObserverEntry entry = entries[i];
            if (entry == null || entry.Observer == null)
                continue;
            try { GameManager.Instance.RemoveChunkObserver(entry.Observer); }
            catch (Exception ex)
            {
                Log.Warning("[REBIRTH Dog] failed to remove chunk observer during world reset stableId=" +
                    entry.StableId + " reason=" + ex.Message);
            }
        }
    }

    private static void Ensure(RebirthNpcStableId stableId, string ownerId, Vector3 position, bool followRecoveryRequested = false, bool manualRecallAllOrders = false)
    {
        if (!IsServer() || GameManager.Instance == null)
            return;

        ObserverEntry existing;
        lock (Sync)
        {
            if (Observers.TryGetValue(stableId, out existing) && existing != null && existing.Observer != null)
            {
                existing.OwnerId = ownerId ?? string.Empty;
                if (followRecoveryRequested) existing.FollowRecoveryRequested = true;
                if (manualRecallAllOrders) existing.ManualRecallAllOrders = true;
                if ((existing.Position - position).sqrMagnitude > 0.01f)
                {
                    existing.Position = position;
                    existing.Observer.SetPosition(position);
                }
                return;
            }
        }

        try
        {
            // Exact 2.6 observer shape: no visual mesh, viewDim 3, no remote chunk recipient.
            ChunkManager.ChunkObserver observer =
                GameManager.Instance.AddChunkObserver(position, false, ObserverViewDimension, -1);
            ObserverEntry created = new ObserverEntry
            {
                StableId = stableId,
                OwnerId = ownerId ?? string.Empty,
                Observer = observer,
                Position = position,
                FollowRecoveryRequested = followRecoveryRequested,
                ManualRecallAllOrders = manualRecallAllOrders
            };
            lock (Sync) Observers[stableId] = created;
        }
        catch (Exception ex)
        {
            Log.Warning("[REBIRTH Dog] failed to add chunk observer stableId=" + stableId +
                " pos=" + position + " reason=" + ex.Message);
        }
    }

    private static bool IsFollowRecoveryRequested(RebirthNpcStableId stableId)
    {
        lock (Sync)
        {
            ObserverEntry entry;
            return Observers.TryGetValue(stableId, out entry) && entry != null &&
                entry.Observer != null && entry.FollowRecoveryRequested;
        }
    }

    private static bool IsManualRecallAllOrders(RebirthNpcStableId stableId)
    {
        lock (Sync)
        {
            ObserverEntry entry;
            return Observers.TryGetValue(stableId, out entry) && entry != null &&
                entry.Observer != null && entry.ManualRecallAllOrders;
        }
    }

    private static bool ShouldObserve(RebirthNpcPersistentRecordView record)
    {
        if (record == null || record.Identity == null || record.Profile == null || record.Ownership == null || record.Dog == null)
            return false;
        if (!string.Equals(record.Profile.ProfileId ?? string.Empty, RebirthDogDefinitions.ProfileId, StringComparison.OrdinalIgnoreCase))
            return false;
        if (record.Dog.Lifecycle != RebirthDogLifecycleKind.Active)
            return false;
        if (string.IsNullOrEmpty(record.Ownership.OwnerPlatformIdOrPersistentPlayerId))
            return false;
        if (record.Lifecycle != null && record.Lifecycle.TombstoneState)
            return false;

        RebirthNpcOrderState order;
        if (record.Order == null || !Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order))
            return false;
        return order == RebirthNpcOrderState.Stay || order == RebirthNpcOrderState.Guard;
    }

    private static Vector3 ResolvePersistedPhysicalObserverPosition(RebirthNpcPersistentRecordView record, Vector3 fallback)
    {
        // v282 stores the native chunk that actually owns a dog separately from its logical
        // transform. Prefer that physical key when reloading an unloaded Follow dog so an
        // interrupted teleport can never redirect recovery to a synthetic/ghost position.
        string key = string.Empty;
        if (record != null && record.Presence != null) key = record.Presence.LastKnownChunkKey ?? string.Empty;
        if (string.IsNullOrEmpty(key) && record != null && record.Transform != null) key = record.Transform.ChunkKey ?? string.Empty;

        if (!string.IsNullOrEmpty(key))
        {
            string[] parts = key.Split(',');
            int chunkX, chunkZ;
            if (parts.Length == 2 && int.TryParse(parts[0], out chunkX) && int.TryParse(parts[1], out chunkZ))
                return new Vector3(chunkX * 16f + 8f, fallback.y, chunkZ * 16f + 8f);
        }

        return fallback;
    }

    private static Vector3 ResolvePhysicalObserverPosition(EntityRebirthDogCompanion dog)
    {
        if (dog == null) return Vector3.zero;

        // Native chunk membership is the authoritative physical residency for a live dog.
        // Use the chunk center so this still works if the dog's logical position was already
        // corrupted by an interrupted long-range teleport.
        if (dog.addedToChunk)
        {
            float x = dog.chunkPosAddedEntityTo.x * 16f + 8f;
            float z = dog.chunkPosAddedEntityTo.z * 16f + 8f;
            return new Vector3(x, dog.position.y, z);
        }

        return dog.position;
    }

    private static Vector3 ResolveObserverPosition(RebirthNpcPersistentRecordView record, Vector3 fallback)
    {
        if (record != null)
        {
            RebirthNpcOrderState order;
            if (record.Order != null && Enum.TryParse(record.Order.OrderState ?? string.Empty, true, out order))
            {
                if (order == RebirthNpcOrderState.Stay && record.Dog != null && record.Dog.StayPosition.HasValue)
                    return record.Dog.StayPosition.Value;
                if (order == RebirthNpcOrderState.Guard && record.Transform != null && record.Transform.AnchorPosition.HasValue)
                    return record.Transform.AnchorPosition.Value;
            }
            if (record.Transform != null)
                return record.Transform.WorldPosition;
        }
        return fallback;
    }

    private static string ResolveOwnerId(RebirthNpcPersistentRecordView record)
    {
        return record != null && record.Ownership != null
            ? (record.Ownership.OwnerPlatformIdOrPersistentPlayerId ?? string.Empty)
            : string.Empty;
    }

    private static bool IsServer()
    {
        return SingletonMonoBehaviour<ConnectionManager>.Instance != null &&
            SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;
    }
}
