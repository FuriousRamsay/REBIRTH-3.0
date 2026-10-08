using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

#nullable disable

public enum RebirthFireSleeperQueueReason : byte
{
    IgnitionIntersection = 0,
    FireCausedStabilityLoss = 1
}

public static class RebirthFireSleeperActivation
{
    private struct VolumeKey : IEquatable<VolumeKey>
    {
        public World World;
        public SleeperVolume Volume;

        public bool Equals(VolumeKey other)
        {
            return ReferenceEquals(World, other.World) && ReferenceEquals(Volume, other.Volume);
        }

        public override bool Equals(object obj)
        {
            return obj is VolumeKey && Equals((VolumeKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((World != null ? RuntimeHelpers.GetHashCode(World) : 0) * 397) ^
                       (Volume != null ? RuntimeHelpers.GetHashCode(Volume) : 0);
            }
        }
    }

    private struct PrefabKey : IEquatable<PrefabKey>
    {
        public World World;
        public PrefabInstance Prefab;

        public bool Equals(PrefabKey other)
        {
            return ReferenceEquals(World, other.World) && ReferenceEquals(Prefab, other.Prefab);
        }

        public override bool Equals(object obj)
        {
            return obj is PrefabKey && Equals((PrefabKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((World != null ? RuntimeHelpers.GetHashCode(World) : 0) * 397) ^
                       (Prefab != null ? RuntimeHelpers.GetHashCode(Prefab) : 0);
            }
        }
    }

    private sealed class PendingVolume
    {
        public VolumeKey Key;
        public EntityPlayer Player;
        public Vector3i SourcePosition;
        public RebirthFireSleeperQueueReason Reason;
    }

    private static readonly Queue<PendingVolume> Pending = new Queue<PendingVolume>();
    private static readonly HashSet<VolumeKey> PendingVolumes = new HashSet<VolumeKey>();
    private static readonly HashSet<VolumeKey> ActivatedVolumes = new HashSet<VolumeKey>();
    private static readonly Dictionary<PrefabKey, float> FireAffectedPrefabs = new Dictionary<PrefabKey, float>();
    private static readonly List<PrefabKey> ExpiredPrefabKeys = new List<PrefabKey>();

    private const int MaxPendingVolumes = 128;
    private const int MaxVolumeStartsPerUpdate = 2;
    private const int MaxVolumeAttemptsPerUpdate = 8;
    private const float FireCollapseAssociationSeconds = 30f;

    public static long Checks;
    public static long VolumesFound;
    public static long VolumesQueued;
    public static long VolumesActivated;
    public static long StabilityEvents;
    public static long StabilityEventsIgnored;
    public static long QueueCapRejected;
    public static long Failures;

    public static void QueueContainingDormantVolumes(World world, Vector3i position, RebirthFireSleeperQueueReason reason)
    {
        if (world == null || !GameStats.GetBool(EnumGameStats.IsSpawnEnemies)) return;
        Checks++;
        try
        {
            PrefabInstance prefab = FindPrefab(position);
            if (prefab == null || prefab.sleeperVolumes == null) return;

            EntityPlayer player = FindNearestPlayer(world, position);
            for (int i = 0; i < prefab.sleeperVolumes.Count; i++)
            {
                SleeperVolume volume = prefab.sleeperVolumes[i];
                if (volume == null || volume.wasCleared || volume.isSpawned || volume.isSpawning) continue;
                if (!Contains(volume, position)) continue;

                VolumesFound++;
                VolumeKey key = new VolumeKey { World = world, Volume = volume };
                if (PendingVolumes.Contains(key) || ActivatedVolumes.Contains(key)) continue;
                if (Pending.Count >= MaxPendingVolumes)
                {
                    QueueCapRejected++;
                    return;
                }

                PendingVolumes.Add(key);
                Pending.Enqueue(new PendingVolume
                {
                    Key = key,
                    Player = player,
                    SourcePosition = position,
                    Reason = reason
                });
                VolumesQueued++;
                Log.Out("[REBIRTH Fire Sleeper] queued reason=" + reason + " pos=" + position
                    + " boxMin=" + volume.BoxMin + " boxMax=" + volume.BoxMax
                    + " pending=" + Pending.Count);
            }
        }
        catch (Exception ex)
        {
            Failures++;
            Log.Warning("[REBIRTH Fire Sleeper] queue failed at " + position + ": " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public static void MarkFireDestroyedSupport(World world, Vector3i position)
    {
        if (world == null) return;
        PrefabInstance prefab = FindPrefab(position);
        if (prefab == null) return;
        FireAffectedPrefabs[new PrefabKey { World = world, Prefab = prefab }] =
            Time.realtimeSinceStartup + FireCollapseAssociationSeconds;
    }

    // Called only from the World.AddFallingBlock patch. This is not a per-frame or per-stability-value scan.
    public static void OnFallingBlock(World world, Vector3i position)
    {
        StabilityEvents++;
        if (world == null || !GameStats.GetBool(EnumGameStats.IsSpawnEnemies)) { StabilityEventsIgnored++; return; }
        PrefabInstance prefab = FindPrefab(position);
        float expiry;
        PrefabKey key = new PrefabKey { World = world, Prefab = prefab };
        if (prefab == null || !FireAffectedPrefabs.TryGetValue(key, out expiry) || expiry < Time.realtimeSinceStartup)
        {
            StabilityEventsIgnored++;
            return;
        }
        QueueContainingDormantVolumes(world, position, RebirthFireSleeperQueueReason.FireCausedStabilityLoss);
    }

    public static void Update(World world)
    {
        PruneExpiredPrefabs();
        int started = 0;
        int attempts = 0;
        while (started < MaxVolumeStartsPerUpdate && attempts < MaxVolumeAttemptsPerUpdate && Pending.Count > 0)
        {
            PendingVolume pending = Pending.Dequeue();
            attempts++;
            PendingVolumes.Remove(pending.Key);

            World pendingWorld = pending.Key.World;
            SleeperVolume volume = pending.Key.Volume;
            if (!ReferenceEquals(pendingWorld, world) || pendingWorld == null || volume == null)
                continue;
            if (volume.wasCleared || volume.isSpawned || volume.isSpawning)
                continue;

            try
            {
                // false queues UpdatePlayerTouched on the sleeper tick, which begins the real spawn sequence.
                volume.TouchGroup(pendingWorld, pending.Player, false);
                ActivatedVolumes.Add(pending.Key);
                VolumesActivated++;
                started++;
                Log.Out("[REBIRTH Fire Sleeper] spawn queued reason=" + pending.Reason
                    + " source=" + pending.SourcePosition + " boxMin=" + volume.BoxMin
                    + " boxMax=" + volume.BoxMax + " player="
                    + (pending.Player != null ? pending.Player.entityId.ToString() : "<none>"));
            }
            catch (Exception ex)
            {
                // Failed activation deliberately remains retryable: it is neither in the pending
                // set nor in the successful per-world activation set after this attempt.
                Failures++;
                Log.Warning("[REBIRTH Fire Sleeper] activation failed: " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }

    public static void Clear()
    {
        Pending.Clear();
        PendingVolumes.Clear();
        ActivatedVolumes.Clear();
        FireAffectedPrefabs.Clear();
        ExpiredPrefabKeys.Clear();
    }

    private static PrefabInstance FindPrefab(Vector3i position)
    {
        return GameManager.Instance != null && GameManager.Instance.GetDynamicPrefabDecorator() != null
            ? GameManager.Instance.GetDynamicPrefabDecorator().GetPrefabAtPosition(position.ToVector3Center())
            : null;
    }

    private static bool Contains(SleeperVolume volume, Vector3i position)
    {
        return position.x >= volume.BoxMin.x && position.x < volume.BoxMax.x
            && position.y >= volume.BoxMin.y && position.y < volume.BoxMax.y
            && position.z >= volume.BoxMin.z && position.z < volume.BoxMax.z;
    }

    private static void PruneExpiredPrefabs()
    {
        if (FireAffectedPrefabs.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        ExpiredPrefabKeys.Clear();
        foreach (KeyValuePair<PrefabKey, float> pair in FireAffectedPrefabs)
            if (pair.Value < now) ExpiredPrefabKeys.Add(pair.Key);
        for (int i = 0; i < ExpiredPrefabKeys.Count; i++) FireAffectedPrefabs.Remove(ExpiredPrefabKeys[i]);
    }

    private static EntityPlayer FindNearestPlayer(World world, Vector3i position)
    {
        if (world == null || world.Players == null || world.Players.list == null) return null;
        EntityPlayer best = null;
        float bestDistance = float.MaxValue;
        Vector3 center = position.ToVector3Center();
        for (int i = 0; i < world.Players.list.Count; i++)
        {
            EntityPlayer player = world.Players.list[i];
            if (player == null) continue;
            float distance = (player.position - center).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; best = player; }
        }
        return best;
    }
}
