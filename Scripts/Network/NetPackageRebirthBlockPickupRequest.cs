using Platform;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public class NetPackageRebirthBlockPickupRequest : NetPackage
{
    private Vector3i blockPos;
    private BlockValue blockValue;
    private int playerId;
    private PlatformUserIdentifierAbs persistentPlayerId;

    public NetPackageRebirthBlockPickupRequest Setup(
        Vector3i position,
        BlockValue value,
        int entityId,
        PersistentPlayerData persistentPlayerData)
    {
        blockPos = position;
        blockValue = value;
        playerId = entityId;
        persistentPlayerId = persistentPlayerData != null ? persistentPlayerData.PrimaryId : null;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binaryReader = (BinaryReader)reader;
        blockPos = StreamUtils.ReadVector3i(binaryReader);
        blockValue = new BlockValue(binaryReader.ReadUInt32());
        playerId = binaryReader.ReadInt32();
        persistentPlayerId = PlatformUserIdentifierAbs.FromStream(binaryReader);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binaryWriter = (BinaryWriter)writer;
        StreamUtils.Write(binaryWriter, blockPos);
        binaryWriter.Write(blockValue.rawData);
        binaryWriter.Write(playerId);
        persistentPlayerId.ToStream(binaryWriter);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || persistentPlayerId == null ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(persistentPlayerId))
            return;

        if (!world.IsRemote())
            RebirthBlockPickupService.ProcessServerRequest(world, blockPos, blockValue, playerId, persistentPlayerId);
    }

    public int GetLength()
    {
        return 36;
    }
}
