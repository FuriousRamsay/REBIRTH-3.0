using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

// B11 corpse / entity loot container source. EntityLootContainer (zombie loot bags, dropped backpacks)
// is an Entity, not a TileEntity, so the tile source original cannot name it. Identity is the native
// entity id + WorldTimeBorn + entityClass (all three are written by Entity.Write and survive a restart);
// a recycled id or a different spawn can never match the saved key. Everything here is inert until
// RebirthP11SmartLootGate.CorpseSourceQualified is flipped.
internal static class RebirthP11CorpseSource
{
    internal static string Key(Entity entity) =>
        entity.entityId + ":" + entity.WorldTimeBorn + ":" + entity.entityClass;

    internal static bool TryParseKey(string key, out int id, out ulong born, out int entityClass)
    {
        id = 0; born = 0; entityClass = 0;
        string[] part = key?.Split(':');
        return part?.Length == 3 && int.TryParse(part[0], out id) && id > 0 &&
            ulong.TryParse(part[1], out born) && int.TryParse(part[2], out entityClass) && entityClass >= 0 &&
            Key(id, born, entityClass) == key;
    }

    internal static string Key(int id, ulong born, int entityClass) => id + ":" + born + ":" + entityClass;

    internal static bool TryResolve(World world, string key, out EntityLootContainer entity)
    {
        entity = null;
        try
        {
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false ||
                !ReferenceEquals(GameManager.Instance?.World, world) ||
                !TryParseKey(key, out int id, out _, out _)) return false;
            var found = world.GetEntity(id) as EntityLootContainer;
            if (found == null || Key(found) != key || found.bRemoved || found.IsMarkedForUnload() ||
                !ReferenceEquals(found.world, world) || !found.addedToChunk ||
                found.bag?.ItemGrid?.items == null || found.bag.ItemGrid.SlotLocks == null ||
                found.bag.ItemGrid.SlotLocks.Length != found.bag.ItemGrid.items.Length ||
                ChunkOf(world, found) == null) return false;
            entity = found;
            return true;
        }
        catch { entity = null; return false; }
    }

    internal static Chunk ChunkOf(World world, Entity entity)
    {
        var chunk = world.GetChunkSync(entity.chunkPosAddedEntityTo.x, entity.chunkPosAddedEntityTo.z) as Chunk;
        return chunk == null || chunk.InProgressUnloading ? null : chunk;
    }

    // The NPC cannot hold a LockManager lock; its step is admitted only while no player has the
    // corpse open and completes on one main-thread step (same rule as RebirthP11NpcLootAccess.NoNativeLock).
    internal static bool NoNativeLock(Entity entity)
    {
        try
        {
            var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
            return entity != null && connection != null && connection.IsServer &&
                !LockManager.Instance.IsLockedServer(entity);
        }
        catch { return false; }
    }

    internal static bool OwnsLock(Entity entity, int playerId)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (entity == null || connection == null || !connection.IsServer) return false;
        var entry = new LockManager.LockEntry(entity, 0);
        if (entity.IsSharedLock(0))
        {
            if (!LockManager.Instance.sharedLocks.TryGetByValue(entry, out IReadOnlyCollection<int> owners) ||
                owners == null) return false;
            return owners.Contains(playerId);
        }
        return LockManager.Instance.singleLocks.TryGetByValue(entry, out int owner) && owner == playerId;
    }
}

// Saved-span witness for the corpse debit: the exact native EntityCreationData frame of the corpse (after
// the debit) is watched through RebirthStationSnapshotEvidence.WatchEntity, a synchronous chunk snapshot
// is taken with the same preconditions as RebirthCookingSavedDropObservation.Begin, and Saved() requires
// the published region snapshot to contain that exact frame. An entity that is reloaded from disk after a
// restart has no witness, so a pending row simply takes a fresh one over the current state.
internal sealed class RebirthP11CorpseWitness : IDisposable
{
    private const int MaximumFrame = 1024 * 1024;
    private static readonly Dictionary<Guid, RebirthP11CorpseWitness> active =
        new Dictionary<Guid, RebirthP11CorpseWitness>();

    private World world; private Chunk chunk; private EntityLootContainer entity; private string key;
    private ItemStack[] expectedGrid; private DateTime expires; private Func<bool> storageCurrent;
    private RebirthStationSnapshotEvidence.Request request;

    private bool Current()
    {
        try
        {
            return ThreadManager.IsMainThread() && ReferenceEquals(GameManager.Instance?.World, world) &&
                !world.IsRemote() && RebirthP11CorpseSource.TryResolve(world, key, out var live) &&
                ReferenceEquals(live, entity) && ReferenceEquals(world.GetChunkSync(chunk.X, chunk.Z), chunk) &&
                !chunk.InProgressUnloading && storageCurrent?.Invoke() == true &&
                GridIs(entity, expectedGrid);
        }
        catch { return false; }
    }

    internal static bool GridIs(EntityLootContainer entity, ItemStack[] expected)
    {
        var items = entity?.bag?.ItemGrid?.items;
        if (items == null || expected == null || items.Length != expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
            if (!RebirthStationGridIngredients.IsSameStackSnapshot(items[i], expected[i])) return false;
        return true;
    }

    // true => the published snapshot contains the exact post-debit corpse frame; proof is a lower-hex digest.
    internal static bool Observe(World world, EntityLootContainer entity, Guid operation, ItemStack[] after,
        out string proof)
    {
        proof = null;
        try
        {
            if (active.TryGetValue(operation, out var existing))
            {
                if (!existing.Current() || existing.expires <= DateTime.UtcNow)
                { existing.Dispose(); active.Remove(operation); }
                else if (existing.request != null &&
                    RebirthStationSnapshotEvidence.TryGetPublishedEntity(existing.request, out var published) &&
                    existing.Current())
                {
                    proof = RebirthRemoteResourceDurableJournal.Digest("corpse|" + operation.ToString("N") + "|" +
                        existing.key + "|" + published.PayloadDigest);
                    existing.Dispose(); active.Remove(operation);
                    return true;
                }
                else return false;
            }
            if (active.Count >= 4) return false;
            var started = Begin(world, entity, after);
            if (started != null) active[operation] = started;
            return false;
        }
        catch { return false; }
    }

    internal static void Forget(Guid operation)
    {
        if (active.TryGetValue(operation, out var witness)) { witness.Dispose(); active.Remove(operation); }
    }

    private static RebirthP11CorpseWitness Begin(World world, EntityLootContainer entity, ItemStack[] after)
    {
        if (!ThreadManager.IsMainThread() || world?.IsRemote() != false || entity == null ||
            !GridIs(entity, after)) return null;
        var chunk = RebirthP11CorpseSource.ChunkOf(world, entity);
        var provider = world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
        var regions = provider?.m_RegionFileManager;
        if (chunk == null || regions == null || !(regions.regionFileAccess is RegionFileAccessSectorBased) ||
            string.IsNullOrEmpty(regions.saveDirectory)) return null;
        string directory = Path.GetFullPath(regions.saveDirectory);
        var access = regions.regionFileAccess;
        var result = new RebirthP11CorpseWitness {
            world = world, chunk = chunk, entity = entity, key = RebirthP11CorpseSource.Key(entity),
            expectedGrid = ItemStack.Clone((IList<ItemStack>)after), expires = DateTime.UtcNow.AddSeconds(30),
            storageCurrent = () => ReferenceEquals(world.ChunkCache?.ChunkProvider, provider) &&
                ReferenceEquals(provider.m_RegionFileManager, regions) &&
                ReferenceEquals(regions.regionFileAccess, access) &&
                Path.GetFullPath(regions.saveDirectory) == directory
        };
        bool handed = false;
        try
        {
            if (!result.Current() || !Monitor.TryEnter(regions.saveLock, 0)) return null;
            try
            {
                if (!RebirthLogisticsParcelBaselines.CanAdmitNativeSnapshot(regions)) return null;
                byte[] frame;
                EntityCreationData native = null;
                try
                {
                    native = new EntityCreationData(entity, StreamModeWrite.Persistency);
                    using (var stream = new MemoryStream())
                    using (var writer = MemoryPools.poolBinaryWriter.AllocSync(true))
                    {
                        writer.SetBaseStream(stream);
                        native.write(writer, StreamModeWrite.Persistency);
                        writer.Flush();
                        if (stream.Length < 1 || stream.Length > MaximumFrame) return null;
                        frame = stream.ToArray();
                    }
                }
                finally { native?.entityData?.Dispose(); native?.bagData?.Dispose(); }
                if (!result.Current()) return null;
                result.request = RebirthStationSnapshotEvidence.WatchEntity(world, chunk, chunk.X, chunk.Z,
                    entity.entityId, entity.entityClass, frame, result.Current);
                if (result.request == null) return null;
                lock (chunk)
                {
                    if (!result.Current() || chunk.IsLocked || chunk.InProgressSaving || chunk.NeedsDecoration ||
                        chunk.InProgressDecorating || chunk.NeedsLightCalculation || chunk.InProgressLighting)
                        return null;
                    chunk.InProgressSaving = true;
                }
                try { if (!result.Current()) return null; chunk.isModified = true; regions.SaveChunkSnapshot(chunk, true); }
                finally { lock (chunk) chunk.InProgressSaving = false; }
                if (!result.Current() || !RebirthLogisticsParcelBaselines.NativeQueueFits(regions)) return null;
                handed = true;
                return result;
            }
            finally { Monitor.Exit(regions.saveLock); }
        }
        catch { return null; }
        finally { if (!handed) result.Dispose(); }
    }

    public void Dispose()
    {
        request?.Dispose(); request = null;
        world = null; chunk = null; entity = null; expectedGrid = null; storageCurrent = null;
    }
}
