using Platform;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthWorkstationOwnerRequest : NetPackage
{
    private Vector3i blockPos;
    private int playerId;
    private PlatformUserIdentifierAbs persistentPlayerId;

    public NetPackageRebirthWorkstationOwnerRequest Setup(
        Vector3i position,
        int entityId,
        PlatformUserIdentifierAbs playerIdentifier)
    {
        blockPos = position;
        playerId = entityId;
        persistentPlayerId = playerIdentifier;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binaryReader = (BinaryReader)reader;
        blockPos = StreamUtils.ReadVector3i(binaryReader);
        playerId = binaryReader.ReadInt32();
        persistentPlayerId = PlatformUserIdentifierAbs.FromStream(binaryReader);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binaryWriter = (BinaryWriter)writer;
        StreamUtils.Write(binaryWriter, blockPos);
        binaryWriter.Write(playerId);
        persistentPlayerId.ToStream(binaryWriter);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || persistentPlayerId == null ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(persistentPlayerId))
            return;

        if (!world.IsRemote())
        {
            RebirthWorkstationSecurityService.ProcessOwnerRequest(
                world, blockPos, playerId, persistentPlayerId);
        }
    }

    public int GetLength()
    {
        return 36;
    }
}
