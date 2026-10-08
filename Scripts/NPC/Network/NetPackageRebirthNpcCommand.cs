using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthNpcRemoteAckStage : byte
{
    Rejected = 0,
    Queued = 1,
    Completed = 2,
    Cancelled = 3,
    Failed = 4
}

public static class RebirthNpcRemoteCommandService
{
    private sealed class PendingRemote
    {
        public int PlayerEntityId;
        public ulong ClientRequestId;
        public uint ConnectionEpoch;
    }

    private const int MaxReplayEntries = 256;
    private static readonly object Sync = new object();
    private static readonly Dictionary<string, ulong> LastNonceByUser = new Dictionary<string, ulong>(StringComparer.Ordinal);
    private static readonly Queue<string> ReplayOrder = new Queue<string>();
    private static readonly Dictionary<ulong, PendingRemote> PendingByCommand = new Dictionary<ulong, PendingRemote>();
    private static ulong nextClientNonce = 1UL;

    public static ulong NextClientNonce()
    {
        lock (Sync)
        {
            ulong value = nextClientNonce++;
            if (value == 0UL) value = nextClientNonce++;
            return value;
        }
    }

    public static bool AcceptNonce(PlatformUserIdentifierAbs userId, uint connectionEpoch, ulong nonce)
    {
        if (userId == null || connectionEpoch == 0 || nonce == 0UL) return false;
        string key = userId.ToString() + "|" + connectionEpoch;
        lock (Sync)
        {
            ulong previous;
            if (LastNonceByUser.TryGetValue(key, out previous) && nonce <= previous) return false;
            if (!LastNonceByUser.ContainsKey(key)) ReplayOrder.Enqueue(key);
            LastNonceByUser[key] = nonce;
            while (ReplayOrder.Count > MaxReplayEntries)
            {
                string oldest = ReplayOrder.Dequeue();
                LastNonceByUser.Remove(oldest);
            }
            return true;
        }
    }

    public static void RegisterAccepted(ulong commandId, int playerEntityId, uint connectionEpoch, ulong clientRequestId)
    {
        lock (Sync)
            PendingByCommand[commandId] = new PendingRemote { PlayerEntityId = playerEntityId, ConnectionEpoch = connectionEpoch, ClientRequestId = clientRequestId };
    }

    public static void OnTerminal(RebirthNpcCommandRecord record)
    {
        if (record == null) return;
        PendingRemote pending;
        lock (Sync)
        {
            if (!PendingByCommand.TryGetValue(record.Request.CommandId, out pending)) return;
            PendingByCommand.Remove(record.Request.CommandId);
        }
        RebirthNpcRemoteAckStage stage;
        switch (record.Status)
        {
            case RebirthNpcCommandStatus.Completed: stage = RebirthNpcRemoteAckStage.Completed; break;
            case RebirthNpcCommandStatus.Cancelled:
            case RebirthNpcCommandStatus.Expired:
            case RebirthNpcCommandStatus.Superseded:
                stage = RebirthNpcRemoteAckStage.Cancelled; break;
            case RebirthNpcCommandStatus.Failed: stage = RebirthNpcRemoteAckStage.Failed; break;
            default: stage = RebirthNpcRemoteAckStage.Rejected; break;
        }
        SendAck(pending.PlayerEntityId, pending.ConnectionEpoch, pending.ClientRequestId, record.Request.CommandId, stage, record.Detail);
    }

    public static void ResetForWorldChange()
    {
        lock (Sync)
        {
            LastNonceByUser.Clear();
            ReplayOrder.Clear();
            PendingByCommand.Clear();
            nextClientNonce = 1UL;
        }
    }

    public static void SendAck(int playerEntityId, uint connectionEpoch, ulong clientRequestId, ulong commandId,
        RebirthNpcRemoteAckStage stage, string detail)
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (manager == null || !manager.IsServer) return;
        manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcCommandAck>()
            .Setup(connectionEpoch, clientRequestId, commandId, stage, detail), _attachedToEntityId: playerEntityId);
    }
}

[Preserve]
public sealed class NetPackageRebirthNpcCommandRequest : NetPackage
{
    private ushort protocolVersion;
    private int playerEntityId;
    private PlatformUserIdentifierAbs userId;
    private uint connectionEpoch;
    private ulong clientRequestId;
    private int npcEntityId;
    private RebirthNpcCommandKind kind;
    private Vector3 targetPosition;
    private bool hasTargetPosition;
    private uint expectedRevision;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

    public NetPackageRebirthNpcCommandRequest Setup(int playerId, PlatformUserIdentifierAbs persistentUserId,
        ulong requestId, int targetNpcId, RebirthNpcCommandKind commandKind, Vector3 position,
        bool hasPosition, uint revision)
    {
        protocolVersion = RebirthNpcNetworkProtocol.CurrentVersion;
        connectionEpoch = RebirthNpcNetworkEpoch.ClientConnectionEpoch;
        playerEntityId = playerId;
        userId = persistentUserId;
        clientRequestId = requestId;
        npcEntityId = targetNpcId;
        kind = commandKind;
        targetPosition = position;
        hasTargetPosition = hasPosition;
        expectedRevision = revision;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        protocolVersion = binary.ReadUInt16();
        connectionEpoch = binary.ReadUInt32();
        playerEntityId = binary.ReadInt32();
        userId = PlatformUserIdentifierAbs.FromStream(binary);
        clientRequestId = binary.ReadUInt64();
        npcEntityId = binary.ReadInt32();
        kind = (RebirthNpcCommandKind)binary.ReadByte();
        hasTargetPosition = binary.ReadBoolean();
        targetPosition = hasTargetPosition
            ? new Vector3(binary.ReadSingle(), binary.ReadSingle(), binary.ReadSingle())
            : Vector3.zero;
        expectedRevision = binary.ReadUInt32();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(protocolVersion);
        binary.Write(connectionEpoch);
        binary.Write(playerEntityId);
        userId.ToStream(binary);
        binary.Write(clientRequestId);
        binary.Write(npcEntityId);
        binary.Write((byte)kind);
        binary.Write(hasTargetPosition);
        if (hasTargetPosition)
        {
            binary.Write(targetPosition.x);
            binary.Write(targetPosition.y);
            binary.Write(targetPosition.z);
        }
        binary.Write(expectedRevision);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || userId == null ||
            !ValidEntityIdForSender(playerEntityId) || !ValidUserIdForSender(userId))
            return;

        if (!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(playerEntityId, connectionEpoch))
        {
            RebirthNpcNetworkProtocol.RecordRejectedRequest("Stale or unknown connection epoch.", false, true);
            return;
        }

        if (!RebirthNpcNetworkProtocol.IsSupported(protocolVersion))
        {
            string mismatch = "Unsupported NPC protocol version " + protocolVersion +
                "; server requires " + RebirthNpcNetworkProtocol.MinimumSupportedVersion + "-" +
                RebirthNpcNetworkProtocol.CurrentVersion + ".";
            RebirthNpcNetworkProtocol.RecordRejectedRequest(mismatch, true, false);
            RebirthNpcRemoteCommandService.SendAck(playerEntityId, connectionEpoch, clientRequestId, 0UL,
                RebirthNpcRemoteAckStage.Rejected, mismatch);
            return;
        }

        if (!RebirthNpcRemoteCommandService.AcceptNonce(userId, connectionEpoch, clientRequestId))
        {
            RebirthNpcNetworkProtocol.RecordRejectedRequest("Duplicate or stale command request.", false, true);
            RebirthNpcRemoteCommandService.SendAck(playerEntityId, connectionEpoch, clientRequestId, 0UL,
                RebirthNpcRemoteAckStage.Rejected, "Duplicate or stale command request.");
            return;
        }

        EntityPlayer player = world.GetEntity(playerEntityId) as EntityPlayer;
        EntityRebirthNPC npc = world.GetEntity(npcEntityId) as EntityRebirthNPC;
        if (player == null || npc == null)
        {
            RebirthNpcRemoteCommandService.SendAck(playerEntityId, connectionEpoch, clientRequestId, 0UL,
                RebirthNpcRemoteAckStage.Rejected, "Player or target NPC is unavailable.");
            return;
        }

        string authenticatedIssuer = userId.ToString();
        RebirthNpcCommandSubmissionResult result = RebirthNpcCommandGateway.SubmitAuthenticated(
            npc, authenticatedIssuer, playerEntityId, kind, targetPosition, hasTargetPosition, expectedRevision);
        if (!result.Accepted)
        {
            RebirthNpcRemoteCommandService.SendAck(playerEntityId, connectionEpoch, clientRequestId, 0UL,
                RebirthNpcRemoteAckStage.Rejected, result.Error);
            return;
        }

        RebirthNpcNetworkProtocol.RecordAcceptedRequest();
        RebirthNpcRemoteCommandService.RegisterAccepted(result.CommandId, playerEntityId, connectionEpoch, clientRequestId);
        RebirthNpcRemoteCommandService.SendAck(playerEntityId, connectionEpoch, clientRequestId, result.CommandId,
            RebirthNpcRemoteAckStage.Queued, "Command authenticated and queued.");
    }

    public int GetLength() => 0;
}

[Preserve]
public sealed class NetPackageRebirthNpcCommandAck : NetPackage
{
    private uint connectionEpoch;
    private ulong clientRequestId;
    private ulong commandId;
    private RebirthNpcRemoteAckStage stage;
    private string detail;

    public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

    public NetPackageRebirthNpcCommandAck Setup(uint epoch, ulong requestId, ulong authoritativeCommandId,
        RebirthNpcRemoteAckStage ackStage, string message)
    {
        connectionEpoch = epoch;
        clientRequestId = requestId;
        commandId = authoritativeCommandId;
        stage = ackStage;
        detail = RebirthNpcNetworkProtocol.BoundDetail(message);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        connectionEpoch = reader.ReadUInt32();
        clientRequestId = reader.ReadUInt64();
        commandId = reader.ReadUInt64();
        stage = (RebirthNpcRemoteAckStage)reader.ReadByte();
        detail = RebirthNpcNetworkFraming.ReadString(reader, RebirthNpcNetworkProtocol.MaxDetailLength);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(connectionEpoch);
        binary.Write(clientRequestId);
        binary.Write(commandId);
        binary.Write((byte)stage);
        RebirthNpcNetworkFraming.WriteString(binary, detail, RebirthNpcNetworkProtocol.MaxDetailLength);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (connectionEpoch == RebirthNpcNetworkEpoch.ClientConnectionEpoch)
            RebirthNpcCommandClientEvents.Receive(clientRequestId, commandId, stage, detail);
    }

    public int GetLength() => 0;
}

public static class RebirthNpcCommandClientEvents
{
    public static event Action<ulong, ulong, RebirthNpcRemoteAckStage, string> Acknowledged;

    internal static void Receive(ulong clientRequestId, ulong commandId,
        RebirthNpcRemoteAckStage stage, string detail)
    {
        Action<ulong, ulong, RebirthNpcRemoteAckStage, string> handler = Acknowledged;
        if (handler != null) handler(clientRequestId, commandId, stage, detail ?? string.Empty);
    }
}
