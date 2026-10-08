using System;
using System.Collections.Generic;
using UnityEngine;

#nullable disable

public sealed class RemoteResourceSourceContribution
{
    public string StableId;
    public string DisplayName;
    public RemoteResourceSourceKind Kind;
    public Vector3 Position;
    public int Revision;
    public readonly Dictionary<int, int> Counts = new Dictionary<int, int>();
    public readonly List<ItemStack> Stacks = new List<ItemStack>();
}

/// <summary>
/// Availability snapshot with source provenance. UI/count queries reuse the aggregate,
/// while invalidation can now target only players whose snapshot referenced a changed
/// source instead of clearing every player's cache.
/// </summary>
public sealed class RemoteResourceAvailabilitySnapshot
{
    private readonly Dictionary<int, int> counts;
    private readonly List<ItemStack> stacks;
    private readonly List<RemoteResourceSourceContribution> sources;
    private readonly HashSet<string> sourceIds;
    private readonly IList<RemoteResourceSourceContribution> sourceView;

    public RemoteResourceAvailabilitySnapshot(
        Dictionary<int, int> itemCounts,
        List<ItemStack> itemStacks,
        List<RemoteResourceSourceContribution> contributions)
    {
        counts = itemCounts ?? new Dictionary<int, int>();
        stacks = itemStacks ?? new List<ItemStack>();
        sources = contributions ?? new List<RemoteResourceSourceContribution>();
        sourceView = sources.AsReadOnly();
        sourceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null && !string.IsNullOrEmpty(sources[i].StableId)) sourceIds.Add(sources[i].StableId);
    }

    public int SourceCount { get { return sources.Count; } }
    public IList<RemoteResourceSourceContribution> Sources { get { return sourceView; } }

    public int GetCount(ItemValue itemValue)
    {
        if (itemValue == null || itemValue.IsEmpty()) return 0;
        int count;
        return counts.TryGetValue(itemValue.type, out count) ? count : 0;
    }

    public bool ContainsSource(string stableId)
    {
        return !string.IsNullOrEmpty(stableId) && sourceIds.Contains(stableId);
    }

    public void AppendClonedStacks(List<ItemStack> destination)
    {
        if (destination == null) return;
        for (int i = 0; i < stacks.Count; i++) destination.Add(stacks[i].Clone());
    }

    public List<ItemStack> CloneStacks()
    {
        List<ItemStack> result = new List<ItemStack>(stacks.Count);
        AppendClonedStacks(result);
        return result;
    }

    public List<RemoteResourceSourceContribution> CloneSources()
    {
        List<RemoteResourceSourceContribution> result =
            new List<RemoteResourceSourceContribution>(sources.Count);
        for (int i = 0; i < sources.Count; i++)
        {
            RemoteResourceSourceContribution source = sources[i];
            if (source == null) continue;

            RemoteResourceSourceContribution clone = new RemoteResourceSourceContribution
            {
                StableId = source.StableId,
                DisplayName = source.DisplayName,
                Kind = source.Kind,
                Position = source.Position,
                Revision = source.Revision
            };

            foreach (KeyValuePair<int, int> pair in source.Counts)
                clone.Counts[pair.Key] = pair.Value;

            result.Add(clone);
        }
        return result;
    }
}

public static class RemoteResourceSnapshotCache
{
    private const long LifetimeTicks = TimeSpan.TicksPerSecond * 2L;

    private sealed class CacheEntry
    {
        public Vector3 Position;
        public long ExpiresAtUtcTicks;
        public int RegistryGeneration;
        public int StateGeneration;
        public long AccessRevision;
        public World World;
        public EntityPlayer Player;
        public RemoteResourceAvailabilitySnapshot Snapshot;
    }

    private static readonly Dictionary<int, CacheEntry> entries = new Dictionary<int, CacheEntry>();
    private static long projectionRevision;
    public static long ProjectionRevision { get { return projectionRevision; } }

    public static void InvalidateAll()
    {
        entries.Clear();
        unchecked { projectionRevision++; }
    }

    public static void InvalidatePlayer(int entityId)
    {
        if (entries.Remove(entityId)) unchecked { projectionRevision++; }
    }

    public static int InvalidateSource(string stableId)
    {
        if (string.IsNullOrEmpty(stableId) || entries.Count == 0) return 0;
        List<int> remove = null;
        foreach (KeyValuePair<int, CacheEntry> pair in entries)
        {
            if (pair.Value == null || pair.Value.Snapshot == null || !pair.Value.Snapshot.ContainsSource(stableId)) continue;
            if (remove == null) remove = new List<int>();
            remove.Add(pair.Key);
        }
        if (remove == null) return 0;
        for (int i = 0; i < remove.Count; i++) entries.Remove(remove[i]);
        unchecked { projectionRevision++; }
        return remove.Count;
    }

    public static RemoteResourceAvailabilitySnapshot Get(EntityPlayer player)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || player == null)
            return Empty();

        long now = DateTime.UtcNow.Ticks;
        CacheEntry entry;
        if (entries.TryGetValue(player.entityId, out entry) &&
            ReferenceEquals(entry.Player, player) && entry.ExpiresAtUtcTicks >= now &&
            entry.RegistryGeneration == RemoteResourceRegistry.Generation &&
            entry.StateGeneration == RemoteResourceStateStore.Generation &&
            entry.AccessRevision == RebirthWorkstationSecurityService.AccessRevision &&
            ReferenceEquals(entry.World, GameManager.Instance != null ? GameManager.Instance.World : null) &&
            (entry.Position - player.position).sqrMagnitude <= 1f)
        {
            return entry.Snapshot;
        }

        RemoteResourceAvailabilitySnapshot snapshot = Build(player);
        entries[player.entityId] = new CacheEntry
        {
            Player = player, Position = player.position,
            ExpiresAtUtcTicks = now + LifetimeTicks,
            RegistryGeneration = RemoteResourceRegistry.Generation,
            StateGeneration = RemoteResourceStateStore.Generation,
            AccessRevision = RebirthWorkstationSecurityService.AccessRevision,
            World = GameManager.Instance != null ? GameManager.Instance.World : null,
            Snapshot = snapshot
        };
        return snapshot;
    }

    public static RemoteResourceAvailabilitySnapshot ForceRebuild(EntityPlayer player)
    {
        if (player == null) return Empty();
        entries.Remove(player.entityId);
        return Get(player);
    }

    private static readonly RemoteResourceAvailabilitySnapshot EmptySnapshot = new RemoteResourceAvailabilitySnapshot(
        new Dictionary<int, int>(), new List<ItemStack>(), new List<RemoteResourceSourceContribution>());
    private static RemoteResourceAvailabilitySnapshot Empty() { return EmptySnapshot; }

    private static RemoteResourceAvailabilitySnapshot Build(EntityPlayer player)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        List<ItemStack> stacks = new List<ItemStack>();
        List<RemoteResourceSourceContribution> contributions = new List<RemoteResourceSourceContribution>();
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        List<IRemoteResourceSource> sources = RemoteResourceSourceDiscovery.ResolveNearby(
            world, player, RemoteResourcesRuntimePolicy.Radius);

        for (int i = 0; i < sources.Count; i++)
        {
            IRemoteResourceSource source = sources[i];
            string reason;
            if (!RemoteResourceEligibility.CanUse(source, player, true, false, out reason))
            {
                RemoteResourceDiagnostics.WriteSource(player, source, false, reason, null);
                continue;
            }

            RemoteResourceSourceContribution contribution = new RemoteResourceSourceContribution
            {
                StableId = source.StableId,
                DisplayName = source.DisplayName,
                Kind = source.Kind,
                Position = source.Position,
                Revision = source.Revision
            };
            ItemStack[] slots = source.Slots;
            PackedBoolArray locks = source.SlotLocks;
            for (int slot = 0; slots != null && slot < slots.Length; slot++)
            {
                if (locks != null && slot < locks.Length && locks[slot]) continue;
                ItemStack stack = slots[slot];
                if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
                ItemStack clone = stack.Clone();
                contribution.Stacks.Add(clone);
                stacks.Add(clone.Clone());
                if (stack.itemValue.HasModSlots && stack.itemValue.HasMods()) continue;
                int current;
                contribution.Counts.TryGetValue(stack.itemValue.type, out current);
                contribution.Counts[stack.itemValue.type] = current + stack.count;
                counts.TryGetValue(stack.itemValue.type, out current);
                counts[stack.itemValue.type] = current + stack.count;
            }
            contributions.Add(contribution);
            RemoteResourceDiagnostics.WriteSource(player, source, true, "eligible", contribution.Counts);
        }

        if (RemoteResourceDiagnostics.Enabled) RemoteResourceDiagnostics.Write("snapshot player=" + player.entityId + " sources=" + contributions.Count + " itemTypes=" + counts.Count);
        return new RemoteResourceAvailabilitySnapshot(counts, stacks, contributions);
    }
}
