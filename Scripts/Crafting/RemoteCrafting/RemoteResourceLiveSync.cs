using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Event-driven availability replication. The server rebuilds only affected players'
/// provenance snapshots when a source inventory/access state changes and pushes the
/// resulting stack projection to dedicated clients. Client XUi inventory events are
/// dispatched on receipt so open ingredient panels refresh immediately.
/// </summary>
public static class RemoteResourceLiveSync
{
    public const int AvailabilityProtocol = 0x52520201; // Version 2: scoped nonce + ordered projection.
    [ThreadStatic] private static int suppressPushDepth;
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, Vector3> DirtySources = new Dictionary<string, Vector3>(StringComparer.Ordinal);
    private static readonly Dictionary<int, EntityPlayer> DirtyPlayers = new Dictionary<int, EntityPlayer>();
    private static readonly HashSet<TileEntityWorkstation> DirtyStations = new HashSet<TileEntityWorkstation>();
    private sealed class RequestScope { public EntityPlayer Player; public long Nonce; }
    private static readonly Dictionary<int, RequestScope> Requests = new Dictionary<int, RequestScope>();
    private static World currentWorld;
    private static bool dirtyAll;
    private static long sendSequence;
    private sealed class StationProjection
    {
        public readonly MemoryStream Stream = new MemoryStream(512);
        public readonly PooledBinaryWriter Writer;
        public byte[] Previous;
        public bool Busy;
        public long AccessRevision = long.MinValue;
        public StationProjection() { Writer = new PooledBinaryWriter(); Writer.SetBaseStream(Stream); }
        public bool HasChanged(TileEntityWorkstation station)
        {
            Stream.Position = 0; Stream.SetLength(0);
            ItemStack[] slots = station.Output;
            Writer.Write(slots != null ? slots.Length : -1);
            // Native ItemValue serialization preserves every metadata/mod/quality field, rather
            // than relying on the old lossy integer revision as an equality proof.
            for (int i = 0; slots != null && i < slots.Length; i++)
            {
                ItemStack stack = slots[i]; Writer.Write(stack != null);
                if (stack == null) continue;
                Writer.Write(stack.count); Writer.Write(stack.itemValue != null);
                if (stack.itemValue != null) stack.itemValue.Write(Writer);
            }
            Writer.Flush();
            bool busy = station.IsUserAccessing() || RemoteResourceAccess.IsServerBusy(station);
            long access = RebirthWorkstationSecurityService.AccessRevision;
            int length = checked((int)Stream.Length); byte[] buffer = Stream.GetBuffer();
            bool equal = Previous != null && Previous.Length == length && Busy == busy && AccessRevision == access;
            if (equal) for (int i = 0; i < length; i++) if (Previous[i] != buffer[i]) { equal = false; break; }
            if (equal) return false;
            Previous = new byte[length]; Buffer.BlockCopy(buffer, 0, Previous, 0, length);
            Busy = busy; AccessRevision = access; return true;
        }
    }
    private static ConditionalWeakTable<TileEntityWorkstation, StationProjection> StationProjections = new ConditionalWeakTable<TileEntityWorkstation, StationProjection>();
    public static void BeginBatch() { suppressPushDepth++; }
    public static void EndBatch() { if (suppressPushDepth > 0) suppressPushDepth--; }
    private static void EnsureWorld()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (ReferenceEquals(currentWorld, world)) return;
        DirtySources.Clear(); DirtyPlayers.Clear(); DirtyStations.Clear(); Requests.Clear(); dirtyAll = false;
        StationProjections = new ConditionalWeakTable<TileEntityWorkstation, StationProjection>();
        currentWorld = world;
        RemoteResourceSnapshotCache.InvalidateAll(); RemoteResourceClientAvailability.Clear();
    }
    public static void Reset()
    {
        lock (Sync)
        {
            DirtySources.Clear(); DirtyPlayers.Clear(); DirtyStations.Clear(); Requests.Clear(); dirtyAll = false;
            StationProjections = new ConditionalWeakTable<TileEntityWorkstation, StationProjection>();
            currentWorld = null;
        }
        RemoteResourceSnapshotCache.InvalidateAll(); RemoteResourceClientAvailability.Clear();
    }
    public static void NotifyWorkstationModified(TileEntityWorkstation station)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || station == null) return;
        lock (Sync)
        {
            EnsureWorld();
            if (DirtyStations.Count >= 4096) { dirtyAll = true; return; }
            DirtyStations.Add(station);
        }
    }
    public static void NotifySourceChanged(string stableId, Vector3 position, string reason)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || string.IsNullOrEmpty(stableId)) return;
        lock (Sync)
        {
            EnsureWorld();
            if (DirtySources.Count >= 4096) { dirtyAll = true; return; }
            DirtySources[stableId] = position;
        }
    }
    public static void NotifySourcesChanged(IList<IRemoteResourceSource> sources, string reason)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || sources == null) return;
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] != null) NotifySourceChanged(sources[i].StableId, sources[i].Position, reason);
    }
    public static void NotifyGlobal(string reason)
    {
        lock (Sync) { EnsureWorld(); dirtyAll = true; }
        RemoteResourceSnapshotCache.InvalidateAll();
    }
    // Public requests coalesce; a forced refresh is rebuilt at most once for this player/tick.
    public static void PushSnapshot(EntityPlayer player, string reason)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || player == null) return;
        lock (Sync) { EnsureWorld(); DirtyPlayers[player.entityId] = player; }
    }
    public static void ProcessSnapshotRequest(World world, int playerId, PlatformUserIdentifierAbs userId, long nonce)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || world == null || world.IsRemote() || nonce <= 0L) return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        PersistentPlayerData persistent = GameManager.Instance != null
            ? GameManager.Instance.GetPersistentPlayerList().GetPlayerDataFromEntityID(playerId) : null;
        if (player == null || persistent == null || persistent.PrimaryId == null || userId == null || !persistent.PrimaryId.Equals(userId)) return;
        lock (Sync)
        {
            EnsureWorld(); RequestScope previous;
            if (Requests.TryGetValue(playerId, out previous) && ReferenceEquals(previous.Player, player) && nonce < previous.Nonce) return;
            Requests[playerId] = new RequestScope { Player = player, Nonce = nonce };
            DirtyPlayers[playerId] = player;
        }
    }
    public static void Pump()
    {
        // Lifecycle and client notification publication are independent of server authority.
        lock (Sync) EnsureWorld();
        RemoteResourceClientAvailability.PumpReceived();
        if (!RemoteResourcesRuntimePolicy.Enabled)
        {
            lock (Sync) { DirtySources.Clear(); DirtyStations.Clear(); DirtyPlayers.Clear(); dirtyAll = false; }
            RemoteResourceClientAvailability.Clear(); return;
        }
        if (suppressPushDepth != 0) return;
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = currentWorld;
        if (connection == null || !connection.IsServer || world == null || world.IsRemote())
        {
            lock (Sync) { DirtySources.Clear(); DirtyStations.Clear(); DirtyPlayers.Clear(); dirtyAll = false; }
            return;
        }
        Dictionary<string, Vector3> changes;
        Dictionary<int, EntityPlayer> requested;
        TileEntityWorkstation[] stations;
        bool all;
        lock (Sync)
        {
            if (!dirtyAll && DirtySources.Count == 0 && DirtyStations.Count == 0 && DirtyPlayers.Count == 0) return;
            changes = new Dictionary<string, Vector3>(DirtySources, StringComparer.Ordinal); DirtySources.Clear();
            requested = new Dictionary<int, EntityPlayer>(DirtyPlayers); DirtyPlayers.Clear();
            stations = new TileEntityWorkstation[DirtyStations.Count]; DirtyStations.CopyTo(stations); DirtyStations.Clear();
            all = dirtyAll; dirtyAll = false;
        }
        foreach (TileEntityWorkstation station in stations)
        {
            if (station == null || !ReferenceEquals(world.GetTileEntity(station.ToWorldPos()), station)) continue;
            try
            {
                if (!StationProjections.GetOrCreateValue(station).HasChanged(station)) continue;
                changes[RemoteResourceIdentity.Workstation(station.ToWorldPos())] = station.ToWorldPos().ToVector3();
            }
            catch (Exception ex)
            {
                // Uncertain equality is never a reason to retain an availability projection.
                changes[RemoteResourceIdentity.Workstation(station.ToWorldPos())] = station.ToWorldPos().ToVector3();
                if (RemoteResourceDiagnostics.Enabled) RemoteResourceDiagnostics.Write("workstation signature unavailable: " + ex.GetType().Name);
            }
        }
        if (all) RemoteResourceSnapshotCache.InvalidateAll();
        else RemoteResourceSnapshotCache.InvalidateSources(changes.Keys);
        if (!all && changes.Count == 0 && requested.Count == 0) return;
        // One shared player discovery for the whole station/source mutation batch.
        List<EntityPlayer> players = world.GetPlayers();
        float radius = RemoteResourcesRuntimePolicy.Radius + 2f, radiusSq = radius * radius;
        var online = new HashSet<int>();
        for (int i = 0; players != null && i < players.Count; i++)
        {
            EntityPlayer player = players[i]; if (player == null) continue; online.Add(player.entityId);
            EntityPlayer requestPlayer;
            bool affected = all || (requested.TryGetValue(player.entityId, out requestPlayer) && ReferenceEquals(requestPlayer, player));
            if (!affected) foreach (Vector3 position in changes.Values) if ((player.position - position).sqrMagnitude <= radiusSq) { affected = true; break; }
            if (!affected) continue;
            RemoteResourceSnapshotCache.InvalidatePlayer(player.entityId); // Also discovers newly eligible sources.
            EntityPlayerLocal local = player as EntityPlayerLocal;
            RequestScope scope = null;
            if (local == null)
            {
                lock (Sync) Requests.TryGetValue(player.entityId, out scope);
                // Keep invalidation above even without a subscriber; transaction reads
                // rebuild on demand. Do not discover/clone inventories for an unused push.
                if (scope == null || !ReferenceEquals(scope.Player, player)) continue;
            }
            RemoteResourceAvailabilitySnapshot snapshot = RemoteResourceSnapshotCache.Get(player);
            if (local != null) { DispatchLocalUi(local); continue; }
            connection.SendPackage(NetPackageManager.GetPackage<NetPackageRemoteResourceAvailability>()
                .Setup(snapshot.CloneStacks(), snapshot.CloneSources(), "coalesced availability", scope.Nonce, Interlocked.Increment(ref sendSequence)),
                _attachedToEntityId: player.entityId);
        }
        lock (Sync)
        {
            var gone = new List<int>();
            foreach (int id in Requests.Keys) if (!online.Contains(id)) gone.Add(id);
            foreach (int id in gone) Requests.Remove(id);
        }
    }
    public static void DispatchLocalUi(EntityPlayerLocal player)
    {
        if (player == null || player.PlayerUI == null || player.PlayerUI.xui == null || player.PlayerUI.xui.PlayerInventory == null) return;
        player.PlayerUI.xui.PlayerInventory.dispatchBackpackItemsChanged();
        player.PlayerUI.xui.PlayerInventory.dispatchToolbeltItemsChanged();
    }
}

public static class RemoteResourceClientAvailability
{
    private static readonly Dictionary<int, int> counts = new Dictionary<int, int>();
    private static readonly List<ItemStack> stacks = new List<ItemStack>();
    private static readonly List<RemoteResourceSourceContribution> sources = new List<RemoteResourceSourceContribution>();
    private static readonly RemoteResourceSourceContribution[] emptySources = new RemoteResourceSourceContribution[0];
    private static readonly object ReceiveSync = new object();
    private static long requestCounter = DateTime.UtcNow.Ticks, requestNonce, acceptedSequence;
    private static World requestWorld;
    private static EntityPlayerLocal requestPlayer;
    private sealed class Envelope
    {
        public World World;
        public long Nonce, Sequence;
        public List<ItemStack> Stacks;
        public List<RemoteResourceSourceContribution> Sources;
        public string Reason;
    }
    private static Envelope pending;
    private static long lastRequestTicks;
    private static long lastReceiveTicks;
    private static long workstationAccessRevision;
    private static bool valid, awaitingResponse;
    private static long projectionRevision;
    public static long ProjectionRevision { get { return projectionRevision; } }
    private const long ResponseTimeoutTicks = TimeSpan.TicksPerSecond * 15L;
    private const long RequestThrottleTicks = TimeSpan.TicksPerMillisecond * 200L;
    private const long RefreshTicks = TimeSpan.TicksPerSecond * 2L;

    public static void Clear()
    {
        lock (ReceiveSync) { requestNonce = 0L; acceptedSequence = 0L; pending = null; requestWorld = null; requestPlayer = null; }
        counts.Clear();
        stacks.Clear();
        sources.Clear();
        valid = false; awaitingResponse = false;
        unchecked { projectionRevision++; }
        lastRequestTicks = 0;
        lastReceiveTicks = 0;
    }

    public static void EnsureFresh(EntityPlayerLocal player)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || player == null) { if (valid || requestNonce != 0L) Clear(); return; }
        if (requestWorld != null && (!ReferenceEquals(requestWorld, player.world) || !ReferenceEquals(requestPlayer, player))) Clear();
        long revision = RebirthWorkstationSecurityService.AccessRevision;
        if (workstationAccessRevision != revision)
        {
            Clear();
            workstationAccessRevision = revision;
        }
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (player == null || connection == null || connection.IsServer) return;
        long now = DateTime.UtcNow.Ticks;
        if (valid && now - lastReceiveTicks <= RefreshTicks) return;
        // Keep one request nonce while its response is in flight. Replacing it every 200ms
        // would reject every response on a higher-latency connection.
        if (awaitingResponse && now - lastRequestTicks < ResponseTimeoutTicks) return;
        if (now - lastRequestTicks < RequestThrottleTicks) return;
        PersistentPlayerData persistent = GameManager.Instance != null ? GameManager.Instance.GetPersistentLocalPlayer() : null;
        if (persistent == null || persistent.PrimaryId == null) return;
        lastRequestTicks = now; awaitingResponse = true;
        lock (ReceiveSync) { requestNonce = Interlocked.Increment(ref requestCounter); requestWorld = player.world; requestPlayer = player; pending = null; }
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRemoteResourceAvailabilityRequest>()
            .Setup(player.entityId, persistent.PrimaryId, requestNonce));
    }

    public static void InvalidateAndRequest(EntityPlayerLocal player)
    {
        Clear();
        EnsureFresh(player);
    }

    public static void QueueReceived(World world, long nonce, long sequence, List<ItemStack> values,
        List<RemoteResourceSourceContribution> contributions, string reason)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled) return;
        lock (ReceiveSync)
        {
            if (nonce <= 0L || nonce != requestNonce || !ReferenceEquals(world, requestWorld) || sequence <= acceptedSequence
                || (pending != null && sequence <= pending.Sequence)) return;
            // NetPackage.read allocates these lists; later reuse assigns new lists, not mutations.
            pending = new Envelope { World = world, Nonce = nonce, Sequence = sequence, Stacks = values, Sources = contributions, Reason = reason };
        }
    }
    public static void PumpReceived()
    {
        Envelope message;
        lock (ReceiveSync) { message = pending; pending = null; }
        if (message == null) return;
        if (!RemoteResourcesRuntimePolicy.Enabled || RebirthWorkstationSecurityService.AccessRevision != workstationAccessRevision)
        { Clear(); return; }
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal player = world != null ? world.GetPrimaryPlayer() : null;
        if (!ReferenceEquals(world, message.World) || !ReferenceEquals(player, requestPlayer)) { Clear(); return; }
        lock (ReceiveSync)
        {
            if (message.Nonce != requestNonce || message.Sequence <= acceptedSequence) return;
            acceptedSequence = message.Sequence;
        }
        awaitingResponse = false;
        Receive(message.Stacks, message.Sources, message.Reason);
    }
    private static void Receive(

        List<ItemStack> remoteStacks,
        List<RemoteResourceSourceContribution> remoteSources,
        string reason)
    {
        counts.Clear();
        stacks.Clear();
        sources.Clear();
        if (remoteStacks != null)
        {
            for (int i = 0; i < remoteStacks.Count; i++)
            {
                ItemStack stack = remoteStacks[i];
                if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
                stacks.Add(stack.Clone());
                if (stack.itemValue.HasModSlots && stack.itemValue.HasMods()) continue;
                int current;
                counts.TryGetValue(stack.itemValue.type, out current);
                counts[stack.itemValue.type] = current + stack.count;
            }
        }
        if (remoteSources != null)
        {
            for (int i = 0; i < remoteSources.Count; i++)
            {
                RemoteResourceSourceContribution source = remoteSources[i];
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
                sources.Add(clone);
            }
        }
        valid = true;
        unchecked { projectionRevision++; }
        workstationAccessRevision = RebirthWorkstationSecurityService.AccessRevision;
        lastReceiveTicks = DateTime.UtcNow.Ticks;
        EntityPlayerLocal player = GameManager.Instance != null && GameManager.Instance.World != null
            ? GameManager.Instance.World.GetPrimaryPlayer()
            : null;
        if (RemoteResourceDiagnostics.Enabled) RemoteResourceDiagnostics.Write("client availability received stacks=" + stacks.Count +
            " sources=" + sources.Count + " reason='" + (reason ?? string.Empty) + "'");
        RemoteResourceLiveSync.DispatchLocalUi(player);
    }

    public static IList<RemoteResourceSourceContribution> GetSources(EntityPlayerLocal player)
    {
        EnsureFresh(player);
        return valid ? (IList<RemoteResourceSourceContribution>)sources : emptySources;
    }

    public static int GetCount(EntityPlayerLocal player, ItemValue itemValue)
    {
        EnsureFresh(player);
        if (!valid || itemValue == null || itemValue.IsEmpty()) return 0;
        int value;
        return counts.TryGetValue(itemValue.type, out value) ? value : 0;
    }

    public static void AppendStacks(EntityPlayerLocal player, List<ItemStack> destination)
    {
        EnsureFresh(player);
        if (!valid || destination == null) return;
        for (int i = 0; i < stacks.Count; i++) destination.Add(stacks[i].Clone());
    }

    public static bool HasItems(EntityPlayerLocal player, IList<ItemStack> requirements, int multiplier)
    {
        EnsureFresh(player);
        if (!valid || player == null || requirements == null || multiplier < 1) return false;
        Dictionary<int, int> requiredByType = new Dictionary<int, int>();
        for (int i = 0; i < requirements.Count; i++)
        {
            ItemStack stack = requirements[i];
            if (stack == null || stack.IsEmpty() || stack.count <= 0) continue;
            int current;
            requiredByType.TryGetValue(stack.itemValue.type, out current);
            requiredByType[stack.itemValue.type] = current + stack.count * multiplier;
        }
        foreach (KeyValuePair<int, int> pair in requiredByType)
        {
            ItemValue itemValue = new ItemValue(pair.Key);
            int local = RemoteResourceClientTransactionCoordinator.CountConsumableLocal(player, itemValue);
            int remote;
            counts.TryGetValue(pair.Key, out remote);
            if (local + remote < pair.Value) return false;
        }
        return true;
    }
}

[Preserve]
public sealed class NetPackageRemoteResourceAvailabilityRequest : NetPackage
{
    private int playerId;
    private long nonce;
    private PlatformUserIdentifierAbs userId;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRemoteResourceAvailabilityRequest Setup(int id, PlatformUserIdentifierAbs uid, long requestNonce)
    { playerId = id; userId = uid; nonce = requestNonce; return this; }
    public override void read(PooledBinaryReader reader)
    { PooledBinaryReader binary = reader; if (binary.ReadInt32() != RemoteResourceLiveSync.AvailabilityProtocol) throw new InvalidDataException("Remote availability protocol mismatch"); playerId = binary.ReadInt32(); userId = PlatformUserIdentifierAbs.FromStream(binary); nonce = binary.ReadInt64(); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); PooledBinaryWriter binary = writer; binary.Write(RemoteResourceLiveSync.AvailabilityProtocol); binary.Write(playerId); userId.ToStream(binary); binary.Write(nonce); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || world == null || world.IsRemote() ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(userId)) return;
        RemoteResourceLiveSync.ProcessSnapshotRequest(world, playerId, userId, nonce);
    }
    public int GetLength() { return 44; }
}

[Preserve]
public sealed class NetPackageRemoteResourceAvailability : NetPackage
{
    private List<ItemStack> stacks = new List<ItemStack>();
    private List<RemoteResourceSourceContribution> sources = new List<RemoteResourceSourceContribution>();
    private string reason = string.Empty;
    private long nonce, sequence;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    public NetPackageRemoteResourceAvailability Setup(
        List<ItemStack> value,
        List<RemoteResourceSourceContribution> sourceValues,
        string why, long requestNonce, long projectionSequence)
    {
        stacks = value ?? new List<ItemStack>();
        sources = sourceValues ?? new List<RemoteResourceSourceContribution>();
        reason = why ?? string.Empty; nonce = requestNonce; sequence = projectionSequence;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        PooledBinaryReader binary = reader;
        if (binary.ReadInt32() != RemoteResourceLiveSync.AvailabilityProtocol) throw new InvalidDataException("Remote availability protocol mismatch");
        nonce = binary.ReadInt64(); sequence = binary.ReadInt64();
        ItemStack[] values = GameUtils.ReadItemStack(binary);
        stacks = values != null ? new List<ItemStack>(values) : new List<ItemStack>();
        sources = ReadSources(binary);
        reason = binary.ReadString();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        PooledBinaryWriter binary = writer;
        binary.Write(RemoteResourceLiveSync.AvailabilityProtocol); binary.Write(nonce); binary.Write(sequence);
        GameUtils.WriteItemStack(binary, stacks);
        WriteSources(binary, sources);
        binary.Write(reason ?? string.Empty);
    }

    private static void WriteSources(BinaryWriter binary, IList<RemoteResourceSourceContribution> values)
    {
        int count = values != null ? values.Count : 0;
        binary.Write(count);
        for (int i = 0; i < count; i++)
        {
            RemoteResourceSourceContribution source = values[i];
            binary.Write(source != null ? source.StableId ?? string.Empty : string.Empty);
            binary.Write(source != null ? source.DisplayName ?? string.Empty : string.Empty);
            binary.Write(source != null ? (byte)source.Kind : (byte)0);
            Vector3 position = source != null ? source.Position : Vector3.zero;
            binary.Write(position.x);
            binary.Write(position.y);
            binary.Write(position.z);
            binary.Write(source != null ? source.Revision : 0);

            int itemCount = source != null && source.Counts != null ? source.Counts.Count : 0;
            binary.Write(itemCount);
            if (source == null || source.Counts == null) continue;
            foreach (KeyValuePair<int, int> pair in source.Counts)
            {
                binary.Write(pair.Key);
                binary.Write(pair.Value);
            }
        }
    }

    private static List<RemoteResourceSourceContribution> ReadSources(BinaryReader binary)
    {
        int count = binary.ReadInt32();
        if (count < 0 || count > 4096)
            throw new InvalidDataException("Remote resource source count out of range: " + count);

        List<RemoteResourceSourceContribution> result =
            new List<RemoteResourceSourceContribution>(count);
        for (int i = 0; i < count; i++)
        {
            RemoteResourceSourceContribution source = new RemoteResourceSourceContribution
            {
                StableId = binary.ReadString(),
                DisplayName = binary.ReadString(),
                Kind = (RemoteResourceSourceKind)binary.ReadByte(),
                Position = new Vector3(binary.ReadSingle(), binary.ReadSingle(), binary.ReadSingle()),
                Revision = binary.ReadInt32()
            };

            int itemCount = binary.ReadInt32();
            if (itemCount < 0 || itemCount > 65536)
                throw new InvalidDataException("Remote resource item-count entry count out of range: " + itemCount);
            for (int item = 0; item < itemCount; item++)
                source.Counts[binary.ReadInt32()] = binary.ReadInt32();

            result.Add(source);
        }
        return result;
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    { if (world != null && world.IsRemote()) RemoteResourceClientAvailability.QueueReceived(world, nonce, sequence, stacks, sources, reason); }
    public int GetLength() { return 0; }
}

public static class RemoteResourceAccessEventPatchInstaller
{
    private static bool installed;
    public static void Install()
    {
        if (installed) return;
        Harmony harmony = new Harmony("rebirth.remote.resources.live.3.1");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceTileEntityUserAccessPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceServerLockTransitionPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceWorkstationModifiedPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceNpcInventoryApplyPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceNpcInventoryTransferPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceNpcInventoryReservePatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RemoteResourceNpcInventoryReleasePatch));
        ModEvents.GameUpdate.RegisterHandler(OnGameUpdate);
        installed = true;
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        RemoteResourceLiveSync.Pump();
        LogisticsTransferService.UpdatePendingRequest();
        LogisticsPreviewService.UpdatePendingRequest();
    }
    internal static void TileEntityChanged(TileEntity tileEntity, string reason)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || tileEntity == null) return;
        string id;
        if (tileEntity is TileEntityWorkstation)
        {
            id = RemoteResourceIdentity.Workstation(tileEntity.ToWorldPos());
        }
        else
        {
            TEFeatureStorage loot;
            if (!(tileEntity is TileEntityComposite) ||
                !tileEntity.TryGetSelfOrFeature<TEFeatureStorage>(out loot) ||
                !RemoteResourceSourcePolicy.IsSupportedStatic(tileEntity, loot)) return;
            id = RemoteResourceIdentity.Static(tileEntity.ToWorldPos());
        }
        RemoteResourceLiveSync.NotifySourceChanged(id, tileEntity.ToWorldPos().ToVector3(), reason);
    }

    internal static void LockTargetChanged(ILockTarget target)
    {
        if (target is TEFeatureAbs feature)
            TileEntityChanged(feature.Parent, "server storage lock changed");
        else if (target is TileEntity tile)
            TileEntityChanged(tile, "server tile lock changed");
        else if (target is Entity entity)
            EntityChanged(entity, "server entity lock changed");
    }

    internal static void EntityChanged(Entity entity, string reason)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled) return;
        if (entity is EntityVehicle)
            RemoteResourceLiveSync.NotifySourceChanged(RemoteResourceIdentity.Vehicle(entity.entityId), entity.position, reason);
        else if (entity is EntityDrone)
            RemoteResourceLiveSync.NotifySourceChanged(RemoteResourceIdentity.Drone(entity.entityId), entity.position, reason);
    }

    internal static void NpcChanged(RebirthNpcStableId npcId, string reason)
    {
        if (!RemoteResourcesRuntimePolicy.Enabled || npcId.IsEmpty) return;
        int entityId;
        Entity entity = null;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(npcId, out entityId) && GameManager.Instance != null && GameManager.Instance.World != null)
            entity = GameManager.Instance.World.GetEntity(entityId);
        string stableId = "N:" + npcId.ToString();
        if (entity == null)
        {
            RemoteResourceSnapshotCache.InvalidateSource(stableId);
            return;
        }
        RemoteResourceLiveSync.NotifySourceChanged(stableId, entity.position, reason);
    }
}

[HarmonyPatch(typeof(TileEntity), nameof(TileEntity.SetUserAccessing))]
internal static class RemoteResourceTileEntityUserAccessPatch
{
    private static void Postfix(TileEntity __instance, bool _bUserAccessing)
    { RemoteResourceAccessEventPatchInstaller.TileEntityChanged(__instance, _bUserAccessing ? "user access opened" : "user access closed"); }
}


// Observe committed lock-table differences at the dispatcher boundary, not virtual
// callbacks: native storage features/vehicles/drones do not reliably call their base.
[HarmonyPatch]
internal static class RemoteResourceServerLockTransitionPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(LockManager), nameof(LockManager.LockRequestServer),
            new[] { typeof(int), typeof(PooledBinaryReader) })
            ?? throw new MissingMethodException(typeof(LockManager).FullName, "LockRequestServer");
        yield return AccessTools.DeclaredMethod(typeof(LockManager), nameof(LockManager.UnlockRequestServer),
            new[] { typeof(int), typeof(bool) })
            ?? throw new MissingMethodException(typeof(LockManager).FullName, "UnlockRequestServer");
    }
    internal static List<LockManager.LockEntry> Capture(LockManager manager, int playerId)
    {
        var entries = new List<LockManager.LockEntry>();
        manager.singleLocks.TryGetByKey(playerId, entries);
        manager.sharedLocks.TryGetByKey(playerId, entries);
        return entries;
    }
    [HarmonyPrefix]
    private static void Prefix(LockManager __instance, int __0, out List<LockManager.LockEntry> __state)
    {
        var connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        __state = null;
        if (!RemoteResourcesRuntimePolicy.Enabled || connection == null || !connection.IsServer) return;
        try { __state = Capture(__instance, __0); }
        catch (Exception error) { Log.Warning("[REBIRTH RemoteResources] lock observation failed: " + error.Message); }
    }
    internal static void NotifyDifferences(List<LockManager.LockEntry> before, List<LockManager.LockEntry> after)
    {
        var changed = new HashSet<ILockTarget>();
        foreach (var entry in before) if (!after.Contains(entry)) changed.Add(entry.Target);
        foreach (var entry in after) if (!before.Contains(entry)) changed.Add(entry.Target);
        foreach (var target in changed)
            RemoteResourceAccessEventPatchInstaller.LockTargetChanged(target);
    }
    [HarmonyFinalizer]
    private static Exception Finalizer(LockManager __instance, int __0,
        List<LockManager.LockEntry> __state, Exception __exception)
    {
        // Also invalidate if a native callback throws after changing lock ownership.
        // Always preserve the original exception and native acceptance decision.
        if (__state != null)
        {
            try { NotifyDifferences(__state, Capture(__instance, __0)); }
            catch (Exception error) { Log.Warning("[REBIRTH RemoteResources] lock invalidation failed: " + error.Message); }
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(TileEntityWorkstation), "setModified")]
internal static class RemoteResourceWorkstationModifiedPatch
{
    private static void Postfix(TileEntityWorkstation __instance)
    { if (RemoteResourcesRuntimePolicy.Enabled) RemoteResourceLiveSync.NotifyWorkstationModified(__instance); }
}


[HarmonyPatch(typeof(RebirthNpcInventoryTransactionService), nameof(RebirthNpcInventoryTransactionService.Apply))]
internal static class RemoteResourceNpcInventoryApplyPatch
{
    private static void Postfix(RebirthNpcInventoryTransaction transaction, RebirthNpcInventoryTransactionResult __result)
    {
        if (transaction != null && (__result == RebirthNpcInventoryTransactionResult.Applied || __result == RebirthNpcInventoryTransactionResult.Replayed))
            RemoteResourceAccessEventPatchInstaller.NpcChanged(transaction.NpcId, "companion inventory transaction");
    }
}

[HarmonyPatch(typeof(RebirthNpcInventoryTransactionService), nameof(RebirthNpcInventoryTransactionService.ApplyTransfer))]
internal static class RemoteResourceNpcInventoryTransferPatch
{
    private static void Postfix(RebirthNpcInventoryTransfer transfer, RebirthNpcInventoryTransferResult __result)
    {
        if (transfer == null || (__result != RebirthNpcInventoryTransferResult.Applied && __result != RebirthNpcInventoryTransferResult.Replayed)) return;
        RemoteResourceAccessEventPatchInstaller.NpcChanged(transfer.SourceNpcId, "companion inventory transfer source");
        RemoteResourceAccessEventPatchInstaller.NpcChanged(transfer.DestinationNpcId, "companion inventory transfer destination");
    }
}

[HarmonyPatch(typeof(RebirthNpcInventoryTransactionService), nameof(RebirthNpcInventoryTransactionService.TryReserve))]
internal static class RemoteResourceNpcInventoryReservePatch
{
    private static void Postfix(RebirthNpcStableId npcId, bool __result)
    { if (__result) RemoteResourceAccessEventPatchInstaller.NpcChanged(npcId, "companion inventory reservation"); }
}

[HarmonyPatch(typeof(RebirthNpcInventoryTransactionService), nameof(RebirthNpcInventoryTransactionService.ReleaseReservation))]
internal static class RemoteResourceNpcInventoryReleasePatch
{
    private static void Postfix(RebirthNpcStableId npcId)
    { RemoteResourceAccessEventPatchInstaller.NpcChanged(npcId, "companion inventory reservation released"); }
}
