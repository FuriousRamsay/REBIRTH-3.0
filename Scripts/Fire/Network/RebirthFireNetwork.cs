using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#nullable disable

public static class RebirthFireNetwork
{
    private static readonly object SnapshotIdentitySync = new object();
    private static readonly List<Vector3i> EmptyRequestPositions = new List<Vector3i>(0);
    private static World snapshotIdentityWorld;
    private static ulong snapshotWorldEpoch;
    private static ulong nextSnapshotId;

    internal static ulong GetServerWorldEpoch()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        lock (SnapshotIdentitySync)
        {
            EnsureSnapshotWorldLocked(world);
            return snapshotWorldEpoch;
        }
    }

    private static void GetSnapshotIdentity(out ulong worldEpoch, out ulong snapshotId)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        lock (SnapshotIdentitySync)
        {
            EnsureSnapshotWorldLocked(world);
            unchecked { nextSnapshotId++; }
            if (nextSnapshotId == 0) nextSnapshotId = 1;
            worldEpoch = snapshotWorldEpoch;
            snapshotId = nextSnapshotId;
        }
    }

    private static void EnsureSnapshotWorldLocked(World world)
    {
        if (ReferenceEquals(snapshotIdentityWorld, world) && snapshotWorldEpoch != 0)
            return;
        snapshotIdentityWorld = world;
        unchecked { snapshotWorldEpoch++; }
        if (snapshotWorldEpoch == 0) snapshotWorldEpoch = 1;
        nextSnapshotId = 0;
    }

    private static bool IsPackageMapped(Type packageType)
    {
        Type[] mappings = NetPackageManager.PackageMappings;
        if (mappings == null || packageType == null)
            return false;

        for (int i = 0; i < mappings.Length; i++)
        {
            if (mappings[i] == packageType)
                return true;
        }

        return false;
    }

    public static void BroadcastDeltas(int revision, IList<RebirthFireDeltaEntry> entries)
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || !manager.IsServer || entries == null || entries.Count == 0)
            return;
        if (!IsPackageMapped(typeof(NetPackageRebirthFireDeltaBatch)))
            return;

        ulong worldEpoch = GetServerWorldEpoch();
        if (!GameManager.IsDedicatedServer)
            RebirthFireVisualManager.ApplyDeltas(worldEpoch, revision, entries);

        manager.SendPackage(
            NetPackageManager.GetPackage<NetPackageRebirthFireDeltaBatch>()
                .Setup(worldEpoch, revision, entries));
    }

    public static void BroadcastSnapshot()
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || !manager.IsServer)
            return;
        if (!IsPackageMapped(typeof(NetPackageRebirthFireSnapshot)))
            return;

        int revision = RebirthFireService.Instance.Revision;
        List<RebirthFireState> fires = RebirthFireService.Instance.SnapshotFires();
        Dictionary<Vector3i, ulong> cooldowns = RebirthFireService.Instance.SnapshotCooldowns();
        ulong worldEpoch;
        ulong snapshotId;
        GetSnapshotIdentity(out worldEpoch, out snapshotId);
        if (!GameManager.IsDedicatedServer)
            RebirthFireVisualManager.ApplySnapshot(worldEpoch, revision, fires, cooldowns);

        SendSnapshotChunks(
            worldEpoch,
            snapshotId,
            revision,
            fires,
            cooldowns,
            delegate(NetPackage package) { manager.SendPackage(package); });
    }

    public static void SendSnapshot(ClientInfo client)
    {
        if (client == null || !IsPackageMapped(typeof(NetPackageRebirthFireSnapshot)))
            return;
        ulong worldEpoch;
        ulong snapshotId;
        GetSnapshotIdentity(out worldEpoch, out snapshotId);
        SendSnapshotChunks(
            worldEpoch,
            snapshotId,
            RebirthFireService.Instance.Revision,
            RebirthFireService.Instance.SnapshotFires(),
            RebirthFireService.Instance.SnapshotCooldowns(),
            delegate(NetPackage package) { client.SendPackage(package); });
    }

    public static bool RequestSnapshotResync(World world)
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || manager.IsServer || world == null)
            return false;
        EntityPlayerLocal player = world.GetPrimaryPlayer();
        if (player == null || !IsPackageMapped(typeof(NetPackageRebirthFireRequestBatch)))
            return false;
        manager.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthFireRequestBatch>()
                .Setup(RebirthFireRequestOperation.Snapshot, player.entityId, RebirthFireIgnitionCause.Scripted, EmptyRequestPositions, 0f),
            false);
        return true;
    }

    private static void SendSnapshotChunks(
        ulong worldEpoch,
        ulong snapshotId,
        int revision,
        IList<RebirthFireState> fires,
        IDictionary<Vector3i, ulong> cooldowns,
        Action<NetPackage> send)
    {
        if (fires == null || cooldowns == null || send == null)
            return;
        if (fires.Count > RebirthFireDefaults.MaxSnapshotFires ||
            cooldowns.Count > RebirthFireDefaults.MaxSnapshotCooldowns)
        {
            Log.Error("[REBIRTH Fire] Snapshot exceeds protocol aggregate bounds fires=" + fires.Count
                + " cooldowns=" + cooldowns.Count);
            return;
        }

        List<KeyValuePair<Vector3i, ulong>> cooldownList =
            new List<KeyValuePair<Vector3i, ulong>>(cooldowns);
        int fireChunks = Math.Max(1, (fires.Count + RebirthFireDefaults.MaxSnapshotFiresPerPacket - 1) /
            RebirthFireDefaults.MaxSnapshotFiresPerPacket);
        int cooldownChunks = Math.Max(1, (cooldownList.Count + RebirthFireDefaults.MaxSnapshotCooldownsPerPacket - 1) /
            RebirthFireDefaults.MaxSnapshotCooldownsPerPacket);
        int chunks = Math.Max(fireChunks, cooldownChunks);
        if (chunks > RebirthFireDefaults.MaxSnapshotChunks)
        {
            Log.Error("[REBIRTH Fire] Snapshot exceeds chunk bound chunks=" + chunks);
            return;
        }

        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int fireStart = chunk * RebirthFireDefaults.MaxSnapshotFiresPerPacket;
            int fireCount = Math.Min(
                RebirthFireDefaults.MaxSnapshotFiresPerPacket,
                Math.Max(0, fires.Count - fireStart));
            int cooldownStart = chunk * RebirthFireDefaults.MaxSnapshotCooldownsPerPacket;
            int cooldownCount = Math.Min(
                RebirthFireDefaults.MaxSnapshotCooldownsPerPacket,
                Math.Max(0, cooldownList.Count - cooldownStart));
            send(NetPackageManager.GetPackage<NetPackageRebirthFireSnapshot>()
                .Setup(
                    worldEpoch,
                    snapshotId,
                    revision,
                    chunk,
                    chunks,
                    fires.Count,
                    cooldownList.Count,
                    fires,
                    fireStart,
                    fireCount,
                    cooldownList,
                    cooldownStart,
                    cooldownCount));
        }
    }

    public static void SendRequest(
        RebirthFireRequestOperation operation,
        int sourceEntityId,
        RebirthFireIgnitionCause cause,
        IList<Vector3i> positions,
        float smokeSeconds)
    {
        #if DEBUG
        RebirthFireDiagnostics.NetworkSendCalls++;
        #endif
        if (positions != null)
        {
#if DEBUG
            RebirthFireDiagnostics.NetworkSendPositions += positions.Count;
#endif
        }
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || positions == null || positions.Count == 0)
            return;
        if (manager.IsServer)
        {
            World world = GameManager.Instance != null ? GameManager.Instance.World : null;
            ApplyAuthoritativeRequest(world, operation, sourceEntityId, cause, positions, smokeSeconds);
            return;
        }

        manager.SendToServer(
            NetPackageManager.GetPackage<NetPackageRebirthFireRequestBatch>()
                .Setup(operation, sourceEntityId, cause, positions, smokeSeconds),
            false);
    }

    internal static void ApplyAuthoritativeRequest(
        World world,
        RebirthFireRequestOperation operation,
        int sourceEntityId,
        RebirthFireIgnitionCause cause,
        IList<Vector3i> positions,
        float smokeSeconds)
    {
        #if DEBUG
        RebirthFireDiagnostics.NetworkAuthoritativeCalls++;
        #endif
        if (cause == RebirthFireIgnitionCause.Debug)
            Log.Out("[RBFireTest][Authority] request operation=" + operation + " positions=" + (positions != null ? positions.Count : 0)
                + " source=" + sourceEntityId + " runtimePolicy=" + RebirthFireRuntimePolicy.Enabled);
        if (world == null)
            return;
        if (operation == RebirthFireRequestOperation.Ignite)
        {
            #if DEBUG
            RebirthFireDiagnostics.NetworkAuthoritativeIgnitePositions += positions != null ? positions.Count : 0;
            #endif
            RebirthFireService.Instance.ScheduleIgnition(world, positions, sourceEntityId, cause, smokeSeconds);
        }
        else if (operation == RebirthFireRequestOperation.Extinguish)
            RebirthFireService.Instance.ExtinguishMany(world, positions, smokeSeconds);
        else
            RebirthFireService.Instance.RemoveMany(positions);
    }
}

public sealed class NetPackageRebirthFireRequestBatch : NetPackage
{
    private static readonly object RateSync = new object();
    private static readonly Dictionary<int, Queue<float>> RecentRequests = new Dictionary<int, Queue<float>>();
    private const int MaxRequestsPerSecond = 8;
    private RebirthFireRequestOperation operation;
    private int sourceEntityId;
    private RebirthFireIgnitionCause cause;
    private float smokeSeconds;
    private readonly List<Vector3i> positions = new List<Vector3i>();

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToServer; }
    }

    public NetPackageRebirthFireRequestBatch Setup(
        RebirthFireRequestOperation requestOperation,
        int entityId,
        RebirthFireIgnitionCause ignitionCause,
        IList<Vector3i> requestPositions,
        float smokeDuration)
    {
        operation = requestOperation;
        sourceEntityId = entityId;
        cause = ignitionCause;
        smokeSeconds = smokeDuration;
        positions.Clear();
        int count = requestPositions != null
            ? Math.Min(requestPositions.Count, RebirthFireDefaults.MaxRequestPositions)
            : 0;
        for (int i = 0; i < count; i++)
            positions.Add(requestPositions[i]);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        operation = (RebirthFireRequestOperation)reader.ReadByte();
        sourceEntityId = reader.ReadInt32();
        cause = (RebirthFireIgnitionCause)reader.ReadByte();
        smokeSeconds = reader.ReadSingle();
        int count = reader.ReadUInt16();
        if (count < 0 || count > RebirthFireDefaults.MaxRequestPositions)
            throw new InvalidDataException("invalid fire request count");
        positions.Clear();
        for (int i = 0; i < count; i++)
            positions.Add(new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)(object)writer;
        binary.Write((byte)operation);
        binary.Write(sourceEntityId);
        binary.Write((byte)cause);
        binary.Write(smokeSeconds);
        binary.Write((ushort)positions.Count);
        for (int i = 0; i < positions.Count; i++)
        {
            binary.Write(positions[i].x);
            binary.Write(positions[i].y);
            binary.Write(positions[i].z);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        #if DEBUG
        RebirthFireDiagnostics.NetworkRequestPackages++;
        #endif
        if (world == null || Sender == null || !ValidEntityIdForSender(sourceEntityId))
        {
            #if DEBUG
            RebirthFireDiagnostics.NetworkRejectedSender++;
            #endif
            if (cause == RebirthFireIgnitionCause.Debug) Log.Out("[RBFireTest][Server] request rejected reason=sender-or-world");
            return;
        }
        if (!RebirthFireRuntimePolicy.Enabled && operation == RebirthFireRequestOperation.Ignite)
        {
            #if DEBUG
            RebirthFireDiagnostics.NetworkRejectedDisabled++;
            #endif
            if (cause == RebirthFireIgnitionCause.Debug) Log.Out("[RBFireTest][Server] request rejected reason=runtime-disabled");
            return;
        }
        if (!Enum.IsDefined(typeof(RebirthFireRequestOperation), operation) ||
            !Enum.IsDefined(typeof(RebirthFireIgnitionCause), cause))
        {
            #if DEBUG
            RebirthFireDiagnostics.NetworkRejectedEnum++;
            #endif
            if (cause == RebirthFireIgnitionCause.Debug) Log.Out("[RBFireTest][Server] request rejected reason=invalid-enum");
            return;
        }

        if (operation == RebirthFireRequestOperation.Snapshot)
        {
            if (!ConsumeRateBudget(sourceEntityId))
                return;
            RebirthFireNetwork.SendSnapshot(Sender);
            return;
        }

        // RP25: a client-authenticated entity id is not evidence that the requested fire
        // transition actually occurred. Explosion and authoritative MinEvent paths already
        // execute on the server directly. Until a native server action hook can issue a
        // one-time witness for a client-only action, network mutation requests are rejected
        // rather than accepting client-selected cause/range as a receipt.
        //
        // This intentionally preserves read/snapshot networking while failing closed for
        // Ignite/Extinguish/Remove. Do not replace this with a client-issued token.
        if (operation == RebirthFireRequestOperation.Ignite ||
            operation == RebirthFireRequestOperation.Extinguish ||
            operation == RebirthFireRequestOperation.Remove)
        {
            #if DEBUG
            RebirthFireDiagnostics.NetworkRejectedMissingSource++;
            #endif
            if (cause == RebirthFireIgnitionCause.Debug)
                Log.Out("[RBFireTest][Server] request rejected reason=no-authoritative-action-witness");
            return;
        }

        if (operation == RebirthFireRequestOperation.Ignite &&
            cause != RebirthFireIgnitionCause.Molotov && cause != RebirthFireIgnitionCause.Projectile &&
            cause != RebirthFireIgnitionCause.Explosion)
            return;
        if (!ConsumeRateBudget(sourceEntityId)) return;

        Entity source = world.GetEntity(sourceEntityId);
        if (source == null)
        {
            #if DEBUG
            RebirthFireDiagnostics.NetworkRejectedMissingSource++;
            #endif
            if (cause == RebirthFireIgnitionCause.Debug) Log.Out("[RBFireTest][Server] request rejected reason=missing-source entity=" + sourceEntityId);
            return;
        }

        float maximumDistance;
        if (operation == RebirthFireRequestOperation.Extinguish ||
            operation == RebirthFireRequestOperation.Remove)
        {
            // Client-side extinguish actions are always local interactions. Do not let a
            // caller widen their authority by labelling the request as a projectile or explosion.
            maximumDistance = 12f;
        }
        else
        {
            switch (cause)
            {
                case RebirthFireIgnitionCause.Projectile:
                    maximumDistance = 160f;
                    break;
                case RebirthFireIgnitionCause.Molotov:
                case RebirthFireIgnitionCause.Explosion:
                    maximumDistance = 80f;
                    break;
                case RebirthFireIgnitionCause.Scripted:
                    maximumDistance = 16f;
                    break;
                default:
                    maximumDistance = 6f;
                    break;
            }
        }

        float maximumDistanceSquared = maximumDistance * maximumDistance;
        List<Vector3i> validated = new List<Vector3i>(positions.Count);
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3i position = positions[i];
            if ((position.ToVector3Center() - source.position).sqrMagnitude <= maximumDistanceSquared)
                validated.Add(position);
        }
        if (validated.Count == 0)
        {
            #if DEBUG
            RebirthFireDiagnostics.NetworkRejectedDistance++;
            #endif
            if (cause == RebirthFireIgnitionCause.Debug) Log.Out("[RBFireTest][Server] request rejected reason=distance positions=" + positions.Count);
            return;
        }
        #if DEBUG
        RebirthFireDiagnostics.NetworkValidatedPositions += validated.Count;
        #endif
        if (cause == RebirthFireIgnitionCause.Debug) Log.Out("[RBFireTest][Server] request validated positions=" + validated.Count);

        RebirthFireNetwork.ApplyAuthoritativeRequest(
            world,
            operation,
            sourceEntityId,
            cause,
            validated,
            Mathf.Clamp(smokeSeconds, 0f, 600f));
    }

    private static bool ConsumeRateBudget(int entityId)
    {
        float now = Time.realtimeSinceStartup;
        lock (RateSync)
        {
            Queue<float> q;
            if (!RecentRequests.TryGetValue(entityId, out q)) { q = new Queue<float>(); RecentRequests[entityId] = q; }
            while (q.Count > 0 && now - q.Peek() >= 1f) q.Dequeue();
            if (q.Count >= MaxRequestsPerSecond) return false;
            q.Enqueue(now);
            return true;
        }
    }

    public int GetLength()
    {
        return 16 + positions.Count * 12;
    }
}

public sealed class NetPackageRebirthFireDeltaBatch : NetPackage
{
    private ulong worldEpoch;
    private int revision;
    private readonly List<RebirthFireDeltaEntry> entries = new List<RebirthFireDeltaEntry>();

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToClient; }
    }

    public NetPackageRebirthFireDeltaBatch Setup(ulong epoch, int value, IList<RebirthFireDeltaEntry> source)
    {
        worldEpoch = epoch;
        revision = value;
        entries.Clear();
        int count = Math.Min(source.Count, RebirthFireDefaults.MaxNetworkDeltaEntries);
        for (int i = 0; i < count; i++)
            entries.Add(source[i]);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        worldEpoch = reader.ReadUInt64();
        revision = reader.ReadInt32();
        int count = reader.ReadUInt16();
        if (count < 0 || count > RebirthFireDefaults.MaxNetworkDeltaEntries)
            throw new InvalidDataException("invalid fire delta count");
        entries.Clear();
        for (int i = 0; i < count; i++)
        {
            entries.Add(new RebirthFireDeltaEntry
            {
                Position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                Operation = (RebirthFireDeltaOperation)reader.ReadByte(),
                ExpiryWorldTime = reader.ReadUInt64()
            });
        }
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)(object)writer;
        binary.Write(worldEpoch);
        binary.Write(revision);
        binary.Write((ushort)entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            RebirthFireDeltaEntry entry = entries[i];
            binary.Write(entry.Position.x);
            binary.Write(entry.Position.y);
            binary.Write(entry.Position.z);
            binary.Write((byte)entry.Operation);
            binary.Write(entry.ExpiryWorldTime);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthFireVisualManager.ApplyDeltas(worldEpoch, revision, entries);
    }

    public int GetLength()
    {
        return 18 + entries.Count * 21;
    }
}

public sealed class NetPackageRebirthFireSnapshot : NetPackage
{
    private ulong worldEpoch;
    private ulong snapshotId;
    private int revision;
    private ushort chunkIndex;
    private ushort chunkCount;
    private ushort totalFires;
    private ushort totalCooldowns;
    private readonly List<RebirthFireState> fires = new List<RebirthFireState>();
    private readonly Dictionary<Vector3i, ulong> cooldowns = new Dictionary<Vector3i, ulong>();

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToClient; }
    }

    public NetPackageRebirthFireSnapshot Setup(
        ulong epoch,
        ulong id,
        int value,
        int index,
        int count,
        int fireTotal,
        int cooldownTotal,
        IList<RebirthFireState> sourceFires,
        int fireStart,
        int fireCount,
        IList<KeyValuePair<Vector3i, ulong>> sourceCooldowns,
        int cooldownStart,
        int cooldownCountValue)
    {
        if (index < 0 || index >= count || count <= 0 || count > RebirthFireDefaults.MaxSnapshotChunks)
            throw new ArgumentOutOfRangeException("index");
        if (fireTotal < 0 || fireTotal > RebirthFireDefaults.MaxSnapshotFires ||
            cooldownTotal < 0 || cooldownTotal > RebirthFireDefaults.MaxSnapshotCooldowns)
            throw new ArgumentOutOfRangeException("fireTotal");
        if (fireCount < 0 || fireCount > RebirthFireDefaults.MaxSnapshotFiresPerPacket ||
            cooldownCountValue < 0 || cooldownCountValue > RebirthFireDefaults.MaxSnapshotCooldownsPerPacket)
            throw new ArgumentOutOfRangeException("fireCount");

        worldEpoch = epoch;
        snapshotId = id;
        revision = value;
        chunkIndex = (ushort)index;
        chunkCount = (ushort)count;
        totalFires = (ushort)fireTotal;
        totalCooldowns = (ushort)cooldownTotal;
        fires.Clear();
        cooldowns.Clear();
        for (int i = 0; i < fireCount; i++)
            fires.Add(sourceFires[fireStart + i]);
        for (int i = 0; i < cooldownCountValue; i++)
        {
            KeyValuePair<Vector3i, ulong> pair = sourceCooldowns[cooldownStart + i];
            cooldowns[pair.Key] = pair.Value;
        }
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        worldEpoch = reader.ReadUInt64();
        snapshotId = reader.ReadUInt64();
        revision = reader.ReadInt32();
        chunkIndex = reader.ReadUInt16();
        chunkCount = reader.ReadUInt16();
        totalFires = reader.ReadUInt16();
        totalCooldowns = reader.ReadUInt16();
        if (chunkCount == 0 || chunkCount > RebirthFireDefaults.MaxSnapshotChunks || chunkIndex >= chunkCount)
            throw new InvalidDataException("invalid fire snapshot sequence");
        if (totalFires > RebirthFireDefaults.MaxSnapshotFires ||
            totalCooldowns > RebirthFireDefaults.MaxSnapshotCooldowns)
            throw new InvalidDataException("invalid fire snapshot aggregate count");

        int fireCount = reader.ReadUInt16();
        if (fireCount > RebirthFireDefaults.MaxSnapshotFiresPerPacket || fireCount > totalFires)
            throw new InvalidDataException("invalid fire snapshot chunk count");
        fires.Clear();
        for (int i = 0; i < fireCount; i++)
        {
            fires.Add(new RebirthFireState
            {
                Position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()),
                SourceBlockType = reader.ReadInt32()
            });
        }

        int cooldownCountValue = reader.ReadUInt16();
        if (cooldownCountValue > RebirthFireDefaults.MaxSnapshotCooldownsPerPacket ||
            cooldownCountValue > totalCooldowns)
            throw new InvalidDataException("invalid fire cooldown snapshot chunk count");
        cooldowns.Clear();
        for (int i = 0; i < cooldownCountValue; i++)
        {
            Vector3i position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            cooldowns[position] = reader.ReadUInt64();
        }
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)(object)writer;
        binary.Write(worldEpoch);
        binary.Write(snapshotId);
        binary.Write(revision);
        binary.Write(chunkIndex);
        binary.Write(chunkCount);
        binary.Write(totalFires);
        binary.Write(totalCooldowns);
        binary.Write((ushort)fires.Count);
        for (int i = 0; i < fires.Count; i++)
        {
            RebirthFireState state = fires[i];
            binary.Write(state.Position.x);
            binary.Write(state.Position.y);
            binary.Write(state.Position.z);
            binary.Write(state.SourceBlockType);
        }

        binary.Write((ushort)cooldowns.Count);
        foreach (KeyValuePair<Vector3i, ulong> pair in cooldowns)
        {
            binary.Write(pair.Key.x);
            binary.Write(pair.Key.y);
            binary.Write(pair.Key.z);
            binary.Write(pair.Value);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        RebirthFireVisualManager.ApplySnapshotChunk(
            worldEpoch, snapshotId, revision, chunkIndex, chunkCount, totalFires, totalCooldowns, fires, cooldowns);
    }

    public int GetLength()
    {
        return 36 + fires.Count * 16 + cooldowns.Count * 20;
    }
}

public sealed class NetPackageRebirthFireVisibilityBatch : NetPackage
{
    private int playerEntityId;
    private readonly List<Vector3i> visible = new List<Vector3i>();

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToServer; }
    }

    public NetPackageRebirthFireVisibilityBatch Setup(int entityId, IList<Vector3i> positions)
    {
        playerEntityId = entityId;
        visible.Clear();
        int count = Math.Min(positions.Count, RebirthFireVisualManager.FireParticleCap);
        for (int i = 0; i < count; i++)
            visible.Add(positions[i]);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        playerEntityId = reader.ReadInt32();
        int count = reader.ReadByte();
        if (count < 0 || count > RebirthFireVisualManager.FireParticleCap)
            throw new InvalidDataException("invalid fire visibility count");
        visible.Clear();
        for (int i = 0; i < count; i++)
            visible.Add(new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)(object)writer;
        binary.Write(playerEntityId);
        binary.Write((byte)visible.Count);
        for (int i = 0; i < visible.Count; i++)
        {
            binary.Write(visible[i].x);
            binary.Write(visible[i].y);
            binary.Write(visible[i].z);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || Sender == null || !ValidEntityIdForSender(playerEntityId))
            return;
        RebirthFireService.Instance.RecordVisibility(playerEntityId, visible);
    }

    public int GetLength()
    {
        return 9 + visible.Count * 12;
    }
}

/// <summary>
/// Client-confirmed contact with a rendered fire particle. The server validates sender,
/// distance, authoritative fire state, immunity, and the configured contact buff before
/// applying it. Remote players are never ignited from a stale visibility batch alone.
/// </summary>
public sealed class NetPackageRebirthFireContactRequest : NetPackage
{
    private int playerEntityId;
    private Vector3i position;

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.ToServer; }
    }

    public NetPackageRebirthFireContactRequest Setup(int entityId, Vector3i blockPosition)
    {
        playerEntityId = entityId;
        position = blockPosition;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        playerEntityId = reader.ReadInt32();
        position = new Vector3i(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)(object)writer;
        binary.Write(playerEntityId);
        binary.Write(position.x);
        binary.Write(position.y);
        binary.Write(position.z);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        #if DEBUG
        RebirthFireDiagnostics.ContactRequestsReceived++;
        #endif
        if (world == null || Sender == null || !ValidEntityIdForSender(playerEntityId))
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedSender++;
            #endif
            return;
        }
        if (!RebirthFireRuntimePolicy.Enabled)
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedDisabled++;
            #endif
            return;
        }

        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        if (player == null)
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedSender++;
            #endif
            return;
        }
        if (!RebirthFireService.Instance.IsBurning(position))
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedNoFire++;
            #endif
            return;
        }

        const float maximumContactDistance = 3f;
        if ((position.ToVector3Center() - player.position).sqrMagnitude >
            maximumContactDistance * maximumContactDistance)
        {
            #if DEBUG
            RebirthFireDiagnostics.ContactRejectedDistance++;
            #endif
            return;
        }

        RebirthFireGameplayPatches.ApplyAuthoritativeContactBuff(
            player,
            world.GetBlock(position));
    }

    public int GetLength()
    {
        return 20;
    }
}
