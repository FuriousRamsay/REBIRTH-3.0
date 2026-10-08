using Platform;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthWorkstationSecurityAction : NetPackage
{
    private Vector3i blockPos;
    private int playerId;
    private PlatformUserIdentifierAbs persistentPlayerId;
    private RebirthWorkstationSecurityAction action;
    private string payload;

    public NetPackageRebirthWorkstationSecurityAction Setup(
        Vector3i position,
        int entityId,
        PlatformUserIdentifierAbs playerIdentifier,
        RebirthWorkstationSecurityAction requestedAction,
        string actionPayload)
    {
        blockPos = position;
        playerId = entityId;
        persistentPlayerId = playerIdentifier;
        action = requestedAction;
        payload = actionPayload ?? string.Empty;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binaryReader = (BinaryReader)reader;
        blockPos = StreamUtils.ReadVector3i(binaryReader);
        playerId = binaryReader.ReadInt32();
        persistentPlayerId = PlatformUserIdentifierAbs.FromStream(binaryReader);
        action = (RebirthWorkstationSecurityAction)binaryReader.ReadByte();
        payload = binaryReader.ReadString();
        if (payload.Length > 160) throw new InvalidDataException("Workstation request exceeds protocol limit.");
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binaryWriter = (BinaryWriter)writer;
        StreamUtils.Write(binaryWriter, blockPos);
        binaryWriter.Write(playerId);
        persistentPlayerId.ToStream(binaryWriter);
        binaryWriter.Write((byte)action);
        binaryWriter.Write(payload ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || persistentPlayerId == null ||
            !ValidEntityIdForSender(playerId) ||
            !ValidUserIdForSender(persistentPlayerId) || world.IsRemote())
            return;

        RebirthWorkstationSecurityService.ProcessSecurityAction(
            world, blockPos, playerId, persistentPlayerId, action, payload);
    }

    public int GetLength()
    {
        return 40 + (payload != null ? payload.Length * 2 : 0);
    }
}
