using System;
using System.IO;
using HarmonyLib;
using UnityEngine.Scripting;

// B11 durable source identity for ordinary (generated / POI) TEFeatureStorage tiles.
// Named player containers already carry TEFeatureRebirthContainerName.Incarnation.
// Generated storage has no native per-instance token, so this mirrors the native
// chunk-metadata pattern of RebirthLogisticsEndpointIdentity: a ChunkCustomData
// marker keyed by local tile position, bound to the block id, and removed when the
// block type changes or the tile entity is removed/replaced. Everything here is
// inert until RebirthP11SmartLootGate.GeneratedStorageQualified is flipped.
internal static class RebirthP11StorageIncarnation
{
    private const string Prefix = "RebirthSmartLootStorageV1:";
    private static bool installed;

    private static string Key(Vector3i local) => Prefix + local.x + ":" + local.y + ":" + local.z;

    private static byte[] Encode(Guid id, ushort block)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((byte)1); writer.Write(id.ToByteArray()); writer.Write(block); writer.Flush();
            return stream.ToArray();
        }
    }

    private static string Read(ChunkCustomData marker, ushort block)
    {
        var bytes = marker?.data;
        if (bytes?.Length != 19 || bytes[0] != 1 || BitConverter.ToUInt16(bytes, 17) != block ||
            marker.TriggerWriteDataDelegate != null || marker.expiresInWorldTime != ulong.MaxValue ||
            marker.isSavedToNetwork) return null;
        var idBytes = new byte[16];
        Array.Copy(bytes, 1, idBytes, 0, 16);
        var id = new Guid(idBytes);
        return id == Guid.Empty ? null : id.ToString("N");
    }

    // Named containers first (stable across pack-up/placement receipts), then the
    // chunk marker. A marker is only minted by the planning path (mint=true), never
    // by a validation read, so a stale witness cannot be rebound to a new tile.
    internal static bool TryResolve(World world, TEFeatureStorage source, bool mint, out string token)
    {
        token = null;
        try
        {
            string named = (source?.Parent as TileEntityComposite)?.GetFeature<TEFeatureRebirthContainerName>()?.Incarnation;
            if (Guid.TryParseExact(named, "N", out var guid) && guid != Guid.Empty) { token = named; return true; }
            if (!RebirthP11SmartLootGate.GeneratedStorageQualified || !installed) return false;
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false ||
                !ReferenceEquals(GameManager.Instance?.World, world)) return false;
            var tile = source?.Parent as TileEntity;
            if (tile == null || !ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()), tile)) return false;
            var chunk = tile.GetChunk();
            if (chunk == null || chunk.InProgressUnloading ||
                !ReferenceEquals(world.GetChunkSync(chunk.X, chunk.Z), chunk)) return false;
            ushort block = checked((ushort)world.GetBlock(tile.ToWorldPos()).type);
            string key = Key(tile.localChunkPos);
            if (!chunk.ChunkCustomData.dict.TryGetValue(key, out var marker))
            {
                if (!mint) return false;
                marker = new ChunkCustomData(key, ulong.MaxValue, false) { data = Encode(Guid.NewGuid(), block) };
                chunk.ChunkCustomData.Set(key, marker);
                chunk.isModified = true;
            }
            token = Read(marker, block);
            if (token != null && unwitnessed.TryGetValue(token, out var witness))
            {
                // A marker minted this session is usable only after the native chunk save
                // is observed to contain its exact bytes; otherwise it is abandoned on expiry.
                if (witness.Saved()) { unwitnessed.Remove(token); witness.Dispose(); }
                else
                {
                    if (!witness.Current() || witness.Expires <= DateTime.UtcNow) Abandon(token, witness, chunk, key);
                    token = null;
                    return false;
                }
            }
            return token != null;
        }
        catch { token = null; return false; }
    }

    private static readonly System.Collections.Generic.Dictionary<string, Witness> unwitnessed =
        new System.Collections.Generic.Dictionary<string, Witness>();

    private static void Abandon(string token, Witness witness, Chunk chunk, string key)
    {
        unwitnessed.Remove(token);
        witness.Dispose();
        if (chunk.ChunkCustomData.dict.TryGetValue(key, out var marker) && ReferenceEquals(marker, witness.Marker))
        { chunk.ChunkCustomData.Remove(key); chunk.isModified = true; }
    }

    // Saved-span witness (same native evidence as RebirthLogisticsEndpointIdentity.Admission):
    // RebirthStationSnapshotEvidence watches the chunk save, the ChunkCustomData.Write patch
    // records the marker's exact serialized span, and Saved() requires the published snapshot
    // to contain that span at its offset while the chunk, tile, block and marker are unchanged.
    private sealed class Witness : IDisposable
    {
        internal World World; internal Chunk Chunk; internal Vector3i Position; internal ushort Block;
        internal string Token, Key; internal ChunkCustomData Marker; internal byte[] Image, Span;
        internal long Offset = -1; internal DateTime Expires; internal RebirthStationSnapshotEvidence.Request Request;
        internal Func<bool> ProviderCurrent;
        internal bool Current() =>
            ThreadManager.IsMainThread() && ReferenceEquals(GameManager.Instance?.World, World) && !World.IsRemote() &&
            !Chunk.InProgressUnloading && ReferenceEquals(World.GetChunkSync(Chunk.X, Chunk.Z), Chunk) &&
            World.GetBlock(Position).type == Block && ProviderCurrent?.Invoke() == true &&
            Chunk.ChunkCustomData.dict.TryGetValue(Key, out var m) && ReferenceEquals(m, Marker) &&
            Read(m, Block) == Token && System.Linq.Enumerable.SequenceEqual(m.data, Image);
        internal bool Saved()
        {
            if (!Current() || Request == null || Span == null ||
                !RebirthStationSnapshotEvidence.TryCopy(Request, out var snapshot) ||
                !RebirthStationSnapshotEvidence.HasPublished(Request) ||
                Offset < 8 || Offset > snapshot.Length - Span.Length) return false;
            for (int i = 0; i < Span.Length; i++) if (snapshot[(int)Offset + i] != Span[i]) return false;
            return Current();
        }
        public void Dispose() { Request?.Dispose(); Request = null; if (ReferenceEquals(active, this)) active = null; }
    }

    private static Witness active;   // marker currently being serialized by the watched save

    [HarmonyPatch(typeof(ChunkCustomData), nameof(ChunkCustomData.Write))]
    private static class MarkerWrite
    {
        private static void Prefix(ChunkCustomData __instance, BinaryWriter _bw, out long __state) =>
            __state = RebirthStationSnapshotEvidence.StartStationSpan(__instance, _bw.BaseStream);
        private static void Postfix(ChunkCustomData __instance, BinaryWriter _bw, long __state)
        {
            var witness = active;
            if (__state < 8 || witness == null || !ReferenceEquals(witness.Marker, __instance)) return;
            try
            {
                var stream = _bw.BaseStream;
                long end = stream.Position, length = end - __state;
                if (length <= 0 || length > 1024 || !stream.CanRead || !stream.CanSeek) return;
                var bytes = new byte[(int)length];
                try
                {
                    stream.Position = __state;
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int count = stream.Read(bytes, offset, bytes.Length - offset);
                        if (count <= 0) return;
                        offset += count;
                    }
                }
                finally { stream.Position = end; }
                witness.Offset = __state; witness.Span = bytes;
            }
            catch { /* Failed evidence must not change native serialization. */ }
        }
    }

    // Called by the planner only after every other plan check has passed, so a rejected
    // plan never leaves an unused marker on the chunk.
    internal static bool TryMint(World world, TEFeatureStorage source, string token)
    {
        try
        {
            if (!Guid.TryParseExact(token, "N", out var guid) || guid == Guid.Empty) return false;
            if (TryResolve(world, source, false, out string existing)) return existing == token;
            if (!RebirthP11SmartLootGate.GeneratedStorageQualified || !installed ||
                !ThreadManager.IsMainThread() || world?.IsRemote() != false ||
                !ReferenceEquals(GameManager.Instance?.World, world)) return false;
            var tile = source?.Parent as TileEntity;
            if (tile == null || !ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()), tile)) return false;
            var chunk = tile.GetChunk();
            if (chunk == null || chunk.InProgressUnloading ||
                !ReferenceEquals(world.GetChunkSync(chunk.X, chunk.Z), chunk)) return false;
            string key = Key(tile.localChunkPos);
            if (chunk.ChunkCustomData.dict.ContainsKey(key) || unwitnessed.Count >= 4) return false;
            var provider = world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld;
            var regions = provider?.m_RegionFileManager;
            if (regions == null || !(regions.regionFileAccess is RegionFileAccessSectorBased) ||
                string.IsNullOrEmpty(regions.saveDirectory)) return false;
            ushort block = checked((ushort)world.GetBlock(tile.ToWorldPos()).type);
            string directory = System.IO.Path.GetFullPath(regions.saveDirectory);
            var access = regions.regionFileAccess;
            var marker = new ChunkCustomData(key, ulong.MaxValue, false) { data = Encode(guid, block) };
            var witness = new Witness {
                World = world, Chunk = chunk, Position = tile.ToWorldPos(), Block = block, Token = token, Key = key,
                Marker = marker, Image = (byte[])marker.data.Clone(), Expires = DateTime.UtcNow.AddSeconds(30),
                ProviderCurrent = () => ReferenceEquals(world.ChunkCache?.ChunkProvider, provider) &&
                    ReferenceEquals(provider.m_RegionFileManager, regions) &&
                    ReferenceEquals(regions.regionFileAccess, access) &&
                    System.IO.Path.GetFullPath(regions.saveDirectory) == directory
            };
            chunk.ChunkCustomData.Set(key, marker);
            chunk.isModified = true;
            witness.Request = RebirthStationSnapshotEvidence.Watch(world, chunk, chunk.X, chunk.Z, witness.Current, marker);
            bool saved = false;
            if (witness.Request != null && witness.Current())
            {
                lock (chunk)
                {
                    if (!chunk.IsLocked && !chunk.InProgressSaving && !chunk.NeedsDecoration &&
                        !chunk.InProgressDecorating && !chunk.NeedsLightCalculation && !chunk.InProgressLighting)
                    { chunk.InProgressSaving = true; saved = true; }
                }
            }
            if (saved)
            {
                active = witness;
                try { if (witness.Current()) regions.SaveChunkSnapshot(chunk, true); }
                catch { saved = false; }
                finally { active = null; lock (chunk) chunk.InProgressSaving = false; }
            }
            unwitnessed[token] = witness;
            if (!saved) { Abandon(token, witness, chunk, key); return false; }
            // Publication may complete after this call; Saved() is re-checked by TryResolve.
            return TryResolve(world, source, false, out existing) && existing == token;
        }
        catch { return false; }
    }

    // Forces the native synchronous chunk snapshot save (same preconditions and member names as
    // RebirthLogisticsEndpointIdentity.Begin) so a just-touched container is in the region file
    // before a receipt phase advances. Returns false when the chunk cannot be saved right now;
    // callers then retain their earlier phase and retry.
    internal static bool TryPersistChunk(World world, TEFeatureStorage source)
    {
        try
        {
            if (!ThreadManager.IsMainThread() || world?.IsRemote() != false ||
                !ReferenceEquals(GameManager.Instance?.World, world)) return false;
            var tile = source?.Parent as TileEntity;
            if (tile == null || !ReferenceEquals(world.GetTileEntity(tile.ToWorldPos()), tile)) return false;
            var chunk = tile.GetChunk();
            if (chunk == null || chunk.InProgressUnloading ||
                !ReferenceEquals(world.GetChunkSync(chunk.X, chunk.Z), chunk)) return false;
            var regions = (world.ChunkCache?.ChunkProvider as ChunkProviderGenerateWorld)?.m_RegionFileManager;
            if (regions == null || !(regions.regionFileAccess is RegionFileAccessSectorBased) ||
                string.IsNullOrEmpty(regions.saveDirectory)) return false;
            chunk.isModified = true;
            lock (chunk)
            {
                if (chunk.IsLocked || chunk.InProgressSaving || chunk.NeedsDecoration ||
                    chunk.InProgressDecorating || chunk.NeedsLightCalculation || chunk.InProgressLighting) return false;
                chunk.InProgressSaving = true;
            }
            try { regions.SaveChunkSnapshot(chunk, true); return true; }
            catch { return false; }
            finally { lock (chunk) chunk.InProgressSaving = false; }
        }
        catch { return false; }
    }

    private static void Invalidate(Chunk chunk, Vector3i local)
    {
        var world = GameManager.Instance?.World;
        if (!installed || !ThreadManager.IsMainThread() || world == null || world.IsRemote() || chunk == null ||
            chunk.InProgressUnloading || !ReferenceEquals(world.GetChunkSync(chunk.X, chunk.Z), chunk)) return;
        string key = Key(local);
        if (chunk.ChunkCustomData.dict.ContainsKey(key)) { chunk.ChunkCustomData.Remove(key); chunk.isModified = true; }
    }

    private static bool IsStorage(TileEntity tile) =>
        tile is TileEntityComposite composite && composite.GetFeature<TEFeatureStorage>() != null;

    // Patches are installed only when the qualification gate is on, so the disabled
    // build adds no hook to Chunk.SetBlock.
    internal static void Install()
    {
        if (installed || !RebirthP11SmartLootGate.GeneratedStorageQualified) return;
        var harmony = new Harmony("rebirth.smartloot.storage.identity.v1");
        try
        {
            foreach (var type in new[] { typeof(BlockCommit), typeof(Remove), typeof(RemoveAll), typeof(Replace), typeof(MarkerWrite) })
                RebirthHarmonyBootstrap.PatchClassOnce(harmony, type);
            installed = true;
        }
        catch { harmony.UnpatchSelf(); throw; }
    }

    [HarmonyPatch(typeof(Chunk), "SetBlock", new[] { typeof(WorldBase), typeof(int), typeof(int), typeof(int),
        typeof(BlockValue), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(int) })]
    private static class BlockCommit
    {
        private static void Prefix(Chunk __instance, int __1, int __2, int __3, out BlockValue __state) =>
            __state = __instance.ChunkCustomData.dict.Count == 0 ? default(BlockValue)
                : __instance.GetBlock(new Vector3i(__1, __2, __3));
        private static void Postfix(Chunk __instance, int __1, int __2, int __3, BlockValue __state)
        {
            if (__instance.ChunkCustomData.dict.Count == 0) return;
            var local = new Vector3i(__1, __2, __3);
            if (__instance.GetBlock(local).type != __state.type) Invalidate(__instance, local);
        }
        private static Exception Finalizer(Chunk __instance, int __1, int __2, int __3, BlockValue __state,
            Exception __exception)
        {
            if (__exception != null) try { Postfix(__instance, __1, __2, __3, __state); } catch { }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(TileEntity), nameof(TileEntity.OnRemove))]
    private static class Remove
    {
        private static void Prefix(TileEntity __instance)
        {
            if (IsStorage(__instance)) Invalidate(__instance.GetChunk(), __instance.localChunkPos);
        }
    }

    [HarmonyPatch(typeof(Chunk), nameof(Chunk.RemoveAllTileEntities))]
    private static class RemoveAll
    {
        private static void Prefix(Chunk __instance)
        {
            foreach (var tile in __instance.tileEntities.list)
                if (IsStorage(tile)) Invalidate(__instance, tile.localChunkPos);
        }
    }

    [HarmonyPatch(typeof(Chunk), nameof(Chunk.AddTileEntity))]
    private static class Replace
    {
        private static void Prefix(Chunk __instance, TileEntity _te)
        {
            if (_te != null && __instance.tileEntities.dict.TryGetValue(_te.localChunkPos, out var old) &&
                old != null && !ReferenceEquals(old, _te)) Invalidate(__instance, _te.localChunkPos);
        }
    }
}

[Preserve]
public sealed class RebirthP11StorageIncarnationModApi : IModApi
{
    public void InitMod(Mod mod) => RebirthP11StorageIncarnation.Install();
}
