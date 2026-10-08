using System;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthWorkstationOwnerSync : NetPackage
{
    private const int MaxOwnerLength = 256;
    private const int MaxMetadataLength = 128;
    private const int MaxAllowedUsers = 1024;
    private const int MaxUserLength = 256;
    private Vector3i blockPos;
    private string ownerCombined;
    private bool locked;
    // Wire slot retained for framing only. RBWS2 public flags/epoch/ack, NEVER a verifier.
    private string publicMetadata;
    private string[] allowedUsers;

    public NetPackageRebirthWorkstationOwnerSync Setup(
        Vector3i position,
        string ownerIdentifier,
        bool isLocked,
        string currentPublicMetadata,
        string[] currentAllowedUsers)
    {
        blockPos = position;
        ownerCombined = ownerIdentifier ?? string.Empty;
        locked = isLocked;
        publicMetadata = currentPublicMetadata ?? string.Empty;
        allowedUsers = currentAllowedUsers ?? new string[0];
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binaryReader = (BinaryReader)reader;
        blockPos = StreamUtils.ReadVector3i(binaryReader);
        ownerCombined = RebirthSurvivorNetworkCodec.ReadBoundedString(binaryReader, MaxOwnerLength);
        locked = binaryReader.ReadBoolean();
        publicMetadata = RebirthSurvivorNetworkCodec.ReadBoundedString(binaryReader, MaxMetadataLength);
        int count = binaryReader.ReadInt32();
        if (count < 0 || count > MaxAllowedUsers)
            throw new InvalidDataException("Invalid workstation access-list count.");
        allowedUsers = new string[count];
        for (int i = 0; i < count; i++)
            allowedUsers[i] = RebirthSurvivorNetworkCodec.ReadBoundedString(binaryReader, MaxUserLength);
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binaryWriter = (BinaryWriter)writer;
        StreamUtils.Write(binaryWriter, blockPos);
        RebirthSurvivorNetworkCodec.WriteString(binaryWriter, ownerCombined, MaxOwnerLength);
        binaryWriter.Write(locked);
        RebirthSurvivorNetworkCodec.WriteString(binaryWriter, publicMetadata, MaxMetadataLength);
        int count = allowedUsers != null ? allowedUsers.Length : 0;
        if (count > MaxAllowedUsers) throw new InvalidDataException("Workstation access-list exceeds packet bound.");
        binaryWriter.Write(count);
        for (int i = 0; i < count; i++)
            RebirthSurvivorNetworkCodec.WriteString(binaryWriter, allowedUsers[i], MaxUserLength);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world != null && world.IsRemote())
        {
            RebirthWorkstationSecurityService.ReceiveOwnerSync(
                blockPos, ownerCombined, locked, publicMetadata, allowedUsers);
        }
    }

    public int GetLength()
    {
        int length = 20 +
            RebirthSurvivorNetworkCodec.EstimateString(ownerCombined, MaxOwnerLength) +
            RebirthSurvivorNetworkCodec.EstimateString(publicMetadata, MaxMetadataLength);
        if (allowedUsers != null)
        {
            int count = Math.Min(allowedUsers.Length, MaxAllowedUsers);
            for (int i = 0; i < count; i++)
                length += RebirthSurvivorNetworkCodec.EstimateString(allowedUsers[i], MaxUserLength);
        }
        return length;
    }
}
