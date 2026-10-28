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
        if(!RebirthNpcNetworkRetirementPreflight.CanReset)throw new InvalidOperationException("Original NPC network delivery/callback state remains retained");
        RebirthNpcRemoteCommandService.ResetForWorldChange();
        RebirthNpcNetworkBaselineService.ResetForWorldChange();
        RebirthNpcNetworkEpoch.ResetForWorldChange();
        RebirthNpcPendingRuntimeStates.Reset();RebirthNpcPendingEquipmentSnapshots.Reset();
        RebirthNpcPendingAppearanceSnapshots.Reset();
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

public static partial class RebirthNpcNetworkBaselineService
{
    private sealed class BaselineJob
    {
        public int RecipientEntityId;
        public ClientInfo Recipient;
        public ConnectionManager RecipientManager;
        public INetConnection Transport;
        public ulong Sequence;
        public bool StartReturned;
        public bool HeaderAcknowledged;
        public double HeaderRetry;
        public int InitialSkipped;
        public RebirthNpcAncillaryOriginalScope Original;
        public uint WorldEpoch;
        public uint ConnectionEpoch;
        public RebirthNpcStableId[] States;
        public int Limit;
        public int NextIndex;
        public int Sent;
        public int Skipped;
    }

    private const int MaxPendingClients = 64;
    private static readonly object Sync = new object();
    private static Dictionary<int, BaselineJob> Pending = new Dictionary<int, BaselineJob>();
    private static Queue<int> FairOrder = new Queue<int>();
    private static HashSet<int> Enqueued = new HashSet<int>();

    public static void SendTo(ClientInfo clientInfo)
    {
        RetireDisconnectedBaselines();
        if(!UseBaselineScope())return;
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (manager == null || !manager.IsServer || clientInfo == null || world == null) return;
        if(clientInfo.entityId<=0||clientInfo.disconnecting||!ReferenceEquals(manager.Clients.ForEntityId(clientInfo.entityId),clientInfo))return;
        lock(Sync){if(Pending.TryGetValue(clientInfo.entityId,out var prior)){if(ReferenceEquals(prior.Recipient,clientInfo))return;if(!RetireDisconnectedJob(prior))return;}if(Pending.Count>=MaxPendingClients)return;}

        RebirthNpcRuntimeState[] states = RebirthNpcRuntimeRegistry.GetSnapshot();
        int limit = Math.Min(states.Length, RebirthNpcNetworkProtocol.MaxBaselineActors);
        var selected=new RebirthNpcStableId[limit];for(int i=0;i<limit;i++)if(states[i]!=null)selected[i]=states[i].StableId;
        uint worldEpoch = RebirthNpcNetworkEpoch.GetServerEpoch();
        if(!TryPeerConnection(world,manager,clientInfo,out var peerConnection)||peerConnection.Sequence==ulong.MaxValue)return;
        uint connectionEpoch=peerConnection.Epoch;ulong sequence=++peerConnection.Sequence;
        BaselineJob job = new BaselineJob
        {
            RecipientEntityId = clientInfo.entityId,
            Recipient=clientInfo,RecipientManager=manager,Transport=peerConnection.Transport,Original=baselineScope,Sequence=sequence,
            WorldEpoch = worldEpoch,
            ConnectionEpoch = connectionEpoch,
            States = selected,
            Limit = limit,
            Skipped = Math.Max(0, states.Length - limit)
        };
        job.InitialSkipped=job.Skipped;

        lock (Sync)
        {
            // Existing exact native peer jobs retain their immutable original.
            Pending[clientInfo.entityId] = job;
            if (Enqueued.Add(clientInfo.entityId)) FairOrder.Enqueue(clientInfo.entityId);
        }

        EnsureBaselineStart(job,manager);
    }

    public static void Tick()
    {
        RebirthNpcPendingEquipmentSnapshots.Tick();
        RebirthNpcPendingAppearanceSnapshots.Tick();
        RebirthNpcRecoveryProjectionNetwork.TickClient();
        RetireDisconnectedBaselines();
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (manager == null || !manager.IsServer || world == null) return;
        // SendTo binds the scope when a job is queued. With no outstanding
        // baseline, reading recovery-generation files on every game update
        // cannot advance work and only adds synchronous disk I/O.
        lock (Sync) if (Pending.Count == 0 && FairOrder.Count == 0 && Enqueued.Count == 0) return;
        if(!UseBaselineScope())return;

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
                    if(Enqueued.Add(recipient))FairOrder.Enqueue(recipient);job=null;break;
                }
                if(!BaselineJobCurrent(job)){if(RetireDisconnectedJob(job)){job=null;continue;}if(Enqueued.Add(recipient))FairOrder.Enqueue(recipient);job=null;break;}
                break;
            }
        }
        if (job == null) return;
        try
        {
        if(!EnsureBaselineStart(job,manager))return;
        int pageEnd = Math.Min(job.Limit, job.NextIndex + RebirthNpcNetworkProtocol.BaselineActorsPerPage);
        for (; job.NextIndex < pageEnd; job.NextIndex++)
        {
            if(!BaselineJobCurrent(job))return;
            RebirthNpcStableId stable = job.States[job.NextIndex];
            int entityId;
            if (stable.IsEmpty || !RebirthNpcRuntimeRegistry.TryGetEntityId(stable, out entityId))
            { job.Skipped++; continue; }
            EntityRebirthNPC npc = world.GetEntity(entityId) as EntityRebirthNPC;
            if (npc == null || !npc.NativeIdentityReadable||!ReferenceEquals(npc.world,world)||npc.RebirthRuntimeState == null || !npc.RebirthRuntimeState.StableId.Equals(stable))
            { job.Skipped++; continue; }

            manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcRuntimeStateV2>().Setup(npc),
                _attachedToEntityId: job.RecipientEntityId);
            if(!BaselineJobCurrent(job))return;
            EntityRebirthHumanoidNPC humanoid = npc as EntityRebirthHumanoidNPC;
            if (humanoid != null)
            {
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcAppearanceEquipmentV2>().Setup(humanoid),
                    _attachedToEntityId: job.RecipientEntityId);
                if(!BaselineJobCurrent(job))return;
                RebirthNpcEquipmentSnapshot equipment = RebirthNpcEquipmentService.GetSnapshot(stable);
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcSemanticEquipmentV2>().Setup(humanoid, equipment,RebirthNpcRecoveryGeneration.RequireCurrent()),
                    _attachedToEntityId: job.RecipientEntityId);
                if(!BaselineJobCurrent(job))return;
            }
            RebirthNpcRecoveryProjectionNetwork.SendName(npc,job.Recipient);if(!BaselineJobCurrent(job))return;
            job.Sent++;
        }

        if (job.NextIndex >= job.Limit)
        {
            if(!BaselineJobCurrent(job))return;
            manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcBaselineCompleteV2>()
                .Setup(job.WorldEpoch,job.ConnectionEpoch,job.Original.Generation,job.Sequence,job.Sent,job.Skipped),
                _attachedToEntityId: job.RecipientEntityId);
            if(!BaselineJobCurrent(job))return;
            lock (Sync)
            {
                BaselineJob current;
                if (Pending.TryGetValue(job.RecipientEntityId, out current) && object.ReferenceEquals(current, job))
                    Pending.Remove(job.RecipientEntityId);
            }
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
        finally{RequeueOriginalBaseline(job);}
    }

    public static void ResetForWorldChange()
    {
        lock (Sync) { if(!CanRetireEmptyBaseline)throw new InvalidOperationException("Original native baseline delivery remains retained");ResetBaselineConnections();baselineScope=null;Pending.Clear(); FairOrder.Clear(); Enqueued.Clear(); }
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
        RebirthNpcBaselineClientHeader.Start(world,worldEpoch,connectionEpoch,0,0,expectedActors,0,true);
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
        if(RebirthNpcNetworkProtocol.IsSupported(protocolVersion))RebirthNpcBaselineClientHeader.LegacyComplete(world,worldEpoch,actorCount,skippedCount);
    }

    public int GetLength() => 14;
}

public static class RebirthNpcBaselineClientEvents
{
    public static event Action<ushort, int, int> Completed;
    internal static bool HasSubscribers=>Completed!=null;

    internal static void Receive(ushort protocolVersion, int actorCount, int skippedCount)
    {
        Action<ushort, int, int> handler = Completed;
        if (handler != null) handler(protocolVersion, actorCount, skippedCount);
    }
}
