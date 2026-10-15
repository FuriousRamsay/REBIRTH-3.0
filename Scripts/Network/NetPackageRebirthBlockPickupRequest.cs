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
        ConnectionManager connection = SingletonMonoBehaviour<ConnectionManager>.Instance;
        // Native ID validators do not establish current connection/world membership.
        if (!ThreadManager.IsMainThread() || world == null || world.IsRemote() ||
            !ReferenceEquals(world, GameManager.Instance?.World) ||
            connection == null || !connection.IsServer || Sender == null ||
            !ReferenceEquals(connection.Clients?.ForEntityId(playerId), Sender) || persistentPlayerId == null ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(persistentPlayerId))
            return;

        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null || player.IsDead() || !ReferenceEquals(player.world, world) ||
            !ReferenceEquals(world.GetEntity(playerId), player)) return;

        RebirthBlockPickupService.ProcessServerRequest(world, blockPos, blockValue, playerId, persistentPlayerId);
    }

    public int GetLength()
    {
        return 36;
    }
}
