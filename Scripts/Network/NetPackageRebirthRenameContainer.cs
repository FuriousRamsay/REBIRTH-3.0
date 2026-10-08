using Platform;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthRenameContainer : NetPackage
{
    private Vector3i blockPos;
    private int playerId;
    private PlatformUserIdentifierAbs persistentPlayerId;
    private string newName;

    public NetPackageRebirthRenameContainer Setup(
        Vector3i position,
        int entityId,
        PersistentPlayerData persistentPlayerData,
        string name)
    {
        blockPos = position;
        playerId = entityId;
        persistentPlayerId = persistentPlayerData != null
            ? persistentPlayerData.PrimaryId
            : null;
        newName = RebirthContainerRenameService.NormalizeName(name);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binaryReader = (BinaryReader)reader;
        blockPos = StreamUtils.ReadVector3i(binaryReader);
        playerId = binaryReader.ReadInt32();
        persistentPlayerId = PlatformUserIdentifierAbs.FromStream(binaryReader);
        newName = RebirthContainerRenameService.NormalizeName(binaryReader.ReadString());
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binaryWriter = (BinaryWriter)writer;
        StreamUtils.Write(binaryWriter, blockPos);
        binaryWriter.Write(playerId);
        persistentPlayerId.ToStream(binaryWriter);
        binaryWriter.Write(newName ?? string.Empty);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || persistentPlayerId == null ||
            !ValidEntityIdForSender(playerId) || !ValidUserIdForSender(persistentPlayerId))
            return;

        if (!world.IsRemote())
        {
            RebirthContainerRenameService.ProcessServerRequest(
                world, blockPos, playerId, persistentPlayerId, newName);
        }
    }

    public int GetLength()
    {
        return 36 + (newName != null ? newName.Length * 2 : 0);
    }
}
