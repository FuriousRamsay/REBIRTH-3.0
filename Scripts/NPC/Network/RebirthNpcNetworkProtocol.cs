using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthNpcNetworkProtocol
{
    public const ushort CurrentVersion = 2;
    public const ushort MinimumSupportedVersion = 2;
    public const int MaxBaselineActors = 16384;
    public const int BaselineActorsPerPage = 32;
    public const int MaxDetailLength = 512;

    private static readonly object Sync = new object();
    private static long acceptedRequests;
    private static long rejectedRequests;
    private static long protocolMismatches;
    private static long duplicateRequests;
    private static long baselinesSent;
    private static long baselineActorsSent;
    private static long baselineActorsSkipped;
    private static string lastRejection = string.Empty;

    public static bool IsSupported(ushort version)
    {
        return version >= MinimumSupportedVersion && version <= CurrentVersion;
    }

    public static string BoundDetail(string detail)
    {
        string value = detail ?? string.Empty;
        return value.Length <= MaxDetailLength ? value : value.Substring(0, MaxDetailLength);
    }

    public static void RecordAcceptedRequest()
    {
        lock (Sync) acceptedRequests++;
    }

    public static void RecordRejectedRequest(string reason, bool protocolMismatch, bool duplicate)
    {
        lock (Sync)
        {
            rejectedRequests++;
            if (protocolMismatch) protocolMismatches++;
            if (duplicate) duplicateRequests++;
            lastRejection = BoundDetail(reason);
        }
    }

    public static void RecordBaseline(int sent, int skipped)
    {
        lock (Sync)
        {
            baselinesSent++;
            baselineActorsSent += sent;
            baselineActorsSkipped += skipped;
        }
    }

    public static void ResetForWorldChange()
    {
        RebirthNpcRemoteCommandService.ResetForWorldChange();
        RebirthNpcNetworkBaselineService.ResetForWorldChange();
        RebirthNpcNetworkEpoch.ResetForWorldChange();
        RebirthNpcPendingRuntimeStates.Reset();
        lock (Sync) lastRejection = string.Empty;
    }

    public static string GetReport()
    {
        lock (Sync)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("[REBIRTH NPC Network] protocol=").Append(CurrentVersion)
                .Append(" minimum=").Append(MinimumSupportedVersion)
                .Append(" acceptedRequests=").Append(acceptedRequests)
                .Append(" rejectedRequests=").Append(rejectedRequests)
                .Append(" protocolMismatches=").Append(protocolMismatches)
                .Append(" duplicateRequests=").Append(duplicateRequests)
                .Append(" baselinesSent=").Append(baselinesSent)
                .Append(" baselineActorsSent=").Append(baselineActorsSent)
                .Append(" baselineActorsSkipped=").Append(baselineActorsSkipped);
            if (!string.IsNullOrEmpty(lastRejection))
                builder.Append(" lastRejection=").Append(lastRejection);
            return builder.ToString();
        }
    }
}

public static class RebirthNpcNetworkBaselineService
{
    private sealed class BaselineJob
    {
        public int RecipientEntityId;
        public uint WorldEpoch;
        public uint ConnectionEpoch;
        public RebirthNpcRuntimeState[] States;
        public int Limit;
        public int NextIndex;
        public int Sent;
        public int Skipped;
    }

    private const int MaxPendingClients = 64;
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, BaselineJob> Pending = new Dictionary<int, BaselineJob>();
    private static readonly Queue<int> FairOrder = new Queue<int>();
    private static readonly HashSet<int> Enqueued = new HashSet<int>();

    public static void SendTo(ClientInfo clientInfo)
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (manager == null || !manager.IsServer || clientInfo == null || world == null) return;

        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        int limit = Math.Min(states.Length, RebirthNpcNetworkProtocol.MaxBaselineActors);
        uint worldEpoch = RebirthNpcNetworkEpoch.GetServerEpoch();
        uint connectionEpoch = RebirthNpcNetworkEpoch.IssueConnectionEpoch(clientInfo.entityId);
        BaselineJob job = new BaselineJob
        {
            RecipientEntityId = clientInfo.entityId,
            WorldEpoch = worldEpoch,
            ConnectionEpoch = connectionEpoch,
            States = states,
            Limit = limit,
            Skipped = Math.Max(0, states.Length - limit)
        };

        lock (Sync)
        {
            // A reconnect/rebaseline supersedes an older unfinished generation for the same recipient.
            Pending[clientInfo.entityId] = job;
            if (Enqueued.Add(clientInfo.entityId)) FairOrder.Enqueue(clientInfo.entityId);
            while (Pending.Count > MaxPendingClients && FairOrder.Count > 0)
            {
                int oldest = FairOrder.Dequeue();
                Enqueued.Remove(oldest);
                if (oldest != clientInfo.entityId) Pending.Remove(oldest);
            }
        }

        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcBaselineStart>()
            .Setup(RebirthNpcNetworkProtocol.CurrentVersion, worldEpoch, connectionEpoch, limit),
            _attachedToEntityId: clientInfo.entityId);
    }

    public static void Tick()
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (manager == null || !manager.IsServer || world == null) return;

        BaselineJob job = null;
        lock (Sync)
        {
            while (FairOrder.Count > 0)
            {
                int recipient = FairOrder.Dequeue();
                Enqueued.Remove(recipient);
                if (!Pending.TryGetValue(recipient, out job)) continue;
                if (job.WorldEpoch != RebirthNpcNetworkEpoch.GetServerEpoch())
                {
                    Pending.Remove(recipient); job = null; continue;
                }
                break;
            }
        }
        if (job == null) return;

        int pageEnd = Math.Min(job.Limit, job.NextIndex + RebirthNpcNetworkProtocol.BaselineActorsPerPage);
        for (; job.NextIndex < pageEnd; job.NextIndex++)
        {
            RebirthNpcRuntimeState state = job.States[job.NextIndex];
            int entityId;
            if (state == null || !RebirthNpcRuntimeRegistry.TryGetEntityId(state.StableId, out entityId))
            { job.Skipped++; continue; }
            EntityRebirthNPC npc = world.GetEntity(entityId) as EntityRebirthNPC;
            if (npc == null || npc.RebirthRuntimeState == null || !npc.RebirthRuntimeState.StableId.Equals(state.StableId))
            { job.Skipped++; continue; }

            manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcRuntimeState>().Setup(npc),
                _attachedToEntityId: job.RecipientEntityId);
            EntityRebirthHumanoidNPC humanoid = npc as EntityRebirthHumanoidNPC;
            if (humanoid != null)
            {
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcAppearanceEquipment>().Setup(humanoid),
                    _attachedToEntityId: job.RecipientEntityId);
                RebirthNpcEquipmentSnapshot equipment = RebirthNpcEquipmentService.GetSnapshot(state.StableId);
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcSemanticEquipment>().Setup(humanoid, equipment),
                    _attachedToEntityId: job.RecipientEntityId);
            }
            job.Sent++;
        }

        if (job.NextIndex >= job.Limit)
        {
            lock (Sync)
            {
                BaselineJob current;
                if (Pending.TryGetValue(job.RecipientEntityId, out current) && object.ReferenceEquals(current, job))
                    Pending.Remove(job.RecipientEntityId);
            }
            manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcBaselineComplete>()
                .Setup(RebirthNpcNetworkProtocol.CurrentVersion, job.WorldEpoch, job.Sent, job.Skipped),
                _attachedToEntityId: job.RecipientEntityId);
            RebirthNpcNetworkProtocol.RecordBaseline(job.Sent, job.Skipped);
            return;
        }

        lock (Sync)
        {
            BaselineJob current;
            if (Pending.TryGetValue(job.RecipientEntityId, out current) && object.ReferenceEquals(current, job) &&
                Enqueued.Add(job.RecipientEntityId)) FairOrder.Enqueue(job.RecipientEntityId);
        }
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { Pending.Clear(); FairOrder.Clear(); Enqueued.Clear(); }
    }
}

[Preserve]
public sealed class NetPackageRebirthNpcBaselineStart : NetPackage
{
    private ushort protocolVersion;
    private uint worldEpoch;
    private uint connectionEpoch;
    private int expectedActors;
    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;
    public NetPackageRebirthNpcBaselineStart Setup(ushort version, uint epoch, uint connection, int expected)
    { protocolVersion=version; worldEpoch=epoch; connectionEpoch=connection; expectedActors=Math.Max(0, expected); return this; }
    public override void read(PooledBinaryReader reader)
    { protocolVersion=reader.ReadUInt16(); worldEpoch=reader.ReadUInt32(); connectionEpoch=reader.ReadUInt32(); expectedActors=reader.ReadInt32(); }
    public override void write(PooledBinaryWriter writer)
    { base.write(writer); BinaryWriter b=(BinaryWriter)writer; b.Write(protocolVersion); b.Write(worldEpoch); b.Write(connectionEpoch); b.Write(expectedActors); }
    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (!RebirthNpcNetworkProtocol.IsSupported(protocolVersion) || worldEpoch==0 || connectionEpoch==0) return;
        RebirthNpcNetworkEpoch.BeginClientEpoch(worldEpoch, connectionEpoch);
    }
    public int GetLength()=>14;
}

[Preserve]
public sealed class NetPackageRebirthNpcBaselineComplete : NetPackage
{
    private ushort protocolVersion;
    private uint worldEpoch;
    private int actorCount;
    private int skippedCount;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthNpcBaselineComplete Setup(ushort version, uint epoch, int actors, int skipped)
    {
        protocolVersion = version;
        worldEpoch = epoch;
        actorCount = actors;
        skippedCount = skipped;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        protocolVersion = reader.ReadUInt16();
        worldEpoch = reader.ReadUInt32();
        actorCount = reader.ReadInt32();
        skippedCount = reader.ReadInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(protocolVersion);
        binary.Write(worldEpoch);
        binary.Write(actorCount);
        binary.Write(skippedCount);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (RebirthNpcNetworkEpoch.AcceptClientEpoch(worldEpoch))
            RebirthNpcBaselineClientEvents.Receive(protocolVersion, actorCount, skippedCount);
    }

    public int GetLength() => 14;
}

public static class RebirthNpcBaselineClientEvents
{
    public static event Action<ushort, int, int> Completed;

    internal static void Receive(ushort protocolVersion, int actorCount, int skippedCount)
    {
        Action<ushort, int, int> handler = Completed;
        if (handler != null) handler(protocolVersion, actorCount, skippedCount);
    }
}
