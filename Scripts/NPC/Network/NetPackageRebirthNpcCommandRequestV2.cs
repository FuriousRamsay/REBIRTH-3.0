using Platform;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;
#nullable disable

[Preserve]
public sealed class NetPackageRebirthNpcCommandRequestV2 : NetPackage
{
    private const ushort RecoveryCodecVersion = 2;
    private ulong recoveryGeneration;
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

    public NetPackageRebirthNpcCommandRequestV2 Setup(int playerId, PlatformUserIdentifierAbs persistentUserId,
        ulong requestId, int targetNpcId, RebirthNpcCommandKind commandKind, Vector3 position,
        bool hasPosition, uint revision)
    {
        recoveryGeneration=RebirthNpcRecoveryProjectionScope.Current;
        protocolVersion = RecoveryCodecVersion;
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
        recoveryGeneration=binary.ReadUInt64();
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
        binary.Write(recoveryGeneration);
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

        if (protocolVersion!=RecoveryCodecVersion||recoveryGeneration!=RebirthNpcRecoveryGeneration.RequireCurrent())
        {
            string mismatch = "Stale NPC recovery generation or unsupported mutation codec.";
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

